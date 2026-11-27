// T-Q05: QuestWorld — server-side POCO singleton. Source of truth for all
// per-player quest state. См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.4.
//
// Initialized by QuestServer.OnNetworkSpawn (server-only). On clients this
// type is not used — clients consume DTOs via QuestClientState (T-Q07).
//
// T-Q05 scope: skeleton (init, getters, registry by questId). Full logic
// (TryOffer/TryAdvanceObjective/ModifyReputation/Save/Load) lands in T-Q06+.

using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Core;
using ProjectC.Factions;
using ProjectC.Quests.Dto;
using ProjectC.Localization;
using ProjectC.Knowledge;

namespace ProjectC.Quests
{
    /// <summary>
    /// Server-side quest/reputation/attitude state. Singleton — one per server
    /// process. NOT replicated (clients receive DTOs).
    /// </summary>
    public class QuestWorld
    {
        public static QuestWorld Instance { get; private set; }

        /// <summary>
        /// Static quest definitions. Set by QuestServer.OnNetworkSpawn via
        /// questDatabase.allQuests field (a new SO registry, T-Q09).
        /// T-Q05: list populated manually by tests; full integration in T-Q09.
        /// </summary>
        private readonly Dictionary<string, QuestDefinition> _questById = new Dictionary<string, QuestDefinition>();

        /// <summary>T-Q15: NPC lookup reference (для TryTurnIn NPC validation). Set via CreateAndInitialize.</summary>
        public QuestDatabase Database { get; private set; }

        /// <summary>T-Q15: cap на max active quests per player (consumed in TryAccept).</summary>
        public int MaxActiveQuestsPerPlayer { get; set; } = 20;

        /// <summary>T-Q18: persistence repository (optional, nullable). Set by QuestServer.OnNetworkSpawn via SetRepository.</summary>
        public ProjectC.Quests.Persistence.IQuestStateRepository Repository { get; private set; }

        /// <summary>T-Q18: assign persistence backend. Pass null для disable persistence (test mode).</summary>
        public void SetRepository(ProjectC.Quests.Persistence.IQuestStateRepository repo)
        {
            Repository = repo;
            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] Repository set: {(repo == null ? "null (disabled)" : repo.GetType().Name)}");
        }

        /// <summary>Per-player quest instances (key: clientId).</summary>
        private readonly Dictionary<ulong, List<QuestInstance>> _questsByPlayer = new Dictionary<ulong, List<QuestInstance>>();

        /// <summary>Per-player faction reputation (key: (clientId, wireId)).</summary>
        private readonly Dictionary<(ulong, int), int> _reputation = new Dictionary<(ulong, int), int>();

        /// <summary>Per-player NPC attitude (key: (clientId, npcId)).</summary>
        private readonly Dictionary<(ulong, string), int> _npcAttitude = new Dictionary<(ulong, string), int>();

        /// <summary>Per-player world flags (key: (clientId, flagId)).</summary>
        private readonly Dictionary<(ulong, string), bool> _worldFlags = new Dictionary<(ulong, string), bool>();

        // === T-MERCH01: Серверная память торговли с NPC (+0.1 репутации/отношения за каждые 100 CR) ===
        /// <summary>Дробный остаток отношения с NPC от торговли (0..0.999) по (clientId, npcId).</summary>
        private readonly Dictionary<(ulong, string), float> _npcTradeRepRemainder = new Dictionary<(ulong, string), float>();

        /// <summary>Дробный остаток репутации фракции от торговли (0..0.999) по (clientId, factionWireId).</summary>
        private readonly Dictionary<(ulong, int), float> _factionTradeRepRemainder = new Dictionary<(ulong, int), float>();

        /// <summary>Рантайм-остатки лимитированных товаров у NPC на сервере по (npcId, itemId).</summary>
        private readonly Dictionary<(string, int), int> _merchantRuntimeStock = new Dictionary<(string, int), int>();

        /// <summary>Резервный серверный баланс кредитов на случай, если TradeWorld.Repository не поднят в изолированной сцене.</summary>
        private readonly Dictionary<ulong, float> _fallbackCredits = new Dictionary<ulong, float>();

        // === T-SMITH01: Серверная память улучшенных у кузнеца предметов (на базе ItemVariation) ===
        private readonly Dictionary<(ulong, int), ProjectC.Quests.Persistence.BlacksmithUpgradedItemSaveEntry> _upgradedItemsByPlayerItem
            = new Dictionary<(ulong, int), ProjectC.Quests.Persistence.BlacksmithUpgradedItemSaveEntry>();
        private readonly ProjectC.Items.Variation.ItemVariationService _variationService
            = new ProjectC.Items.Variation.ItemVariationService();

        /// <summary>Per-player active dialog session (key: clientId). Содержит treeId+currentNodeId для resume.</summary>
        private readonly Dictionary<ulong, DialogSession> _dialogByPlayer = new Dictionary<ulong, DialogSession>();

        /// <summary>Active dialog session state (in-memory only, не persisted).</summary>
        public class DialogSession
        {
            public string treeId = "";
            /// <summary>T-Q10: SO ref for fast access (no GetDialogTree lookup per advance).</summary>
            public Dialogue.DialogTree tree;
            public string currentNodeId = "";
            public string npcId = "";            // T-Q10: server stores npcId for session validation
            public ulong startedAtUnix = 0;
            /// <summary>T-Q21: filtered visible edges (hideIfUnavailable).</summary>
            public System.Collections.Generic.List<ProjectC.Dialogue.DialogueEdge> visibleEdges;
        }

        // ============ Lifecycle ============

        /// <summary>Create and assign singleton. Idempotent (call again replaces; for tests).</summary>
        public static void CreateAndInitialize(QuestDefinition[] questDefinitions, QuestDatabase database = null, int maxActiveQuestsPerPlayer = 20)
        {
            if (Instance != null)
            {
                Debug.LogWarning("[QuestWorld] Already initialized — replacing.");
            }
            Instance = new QuestWorld();
            Instance.RegisterQuests(questDefinitions);
            Instance.Database = database;
            Instance.MaxActiveQuestsPerPlayer = maxActiveQuestsPerPlayer;
            Debug.Log($"[QuestWorld] Initialized: {Instance._questById.Count} quest definitions registered, maxActive={maxActiveQuestsPerPlayer}, dbNPCs={(database?.npcs?.Length ?? 0)}.");
        }

        /// <summary>Reset singleton. Editor/test only.</summary>
        public static void DestroyInstance()
        {
            Instance = null;
        }

        private void RegisterQuests(QuestDefinition[] quests)
        {
            if (quests == null) return;
            for (int i = 0; i < quests.Length; i++)
            {
                var q = quests[i];
                if (q == null || string.IsNullOrEmpty(q.questId)) continue;
                _questById[q.questId] = q;
            }
        }

        // ============ Quest registry accessors ============

        public QuestDefinition GetQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return null;
            return _questById.TryGetValue(questId, out var q) ? q : null;
        }

        public QuestDefinition[] GetAllQuests()
        {
            var result = new QuestDefinition[_questById.Count];
            int i = 0;
            foreach (var q in _questById.Values) result[i++] = q;
            return result;
        }

        public int QuestCount => _questById.Count;

        private static FactionCatalog GetFactionCatalog()
        {
            if (FactionCatalog.Instance == null)
                new FactionCatalog();
            return FactionCatalog.Instance;
        }

        private static int GetNeutralWireId()
        {
            var neutral = GetFactionCatalog().Get(FactionId.Neutral);
            return neutral != null ? neutral.EffectiveWireId : 0;
        }

        // ============ Per-player state accessors (T-Q06+ fills real logic) ============

        public List<QuestInstance> GetPlayerQuests(ulong clientId)
        {
            if (!_questsByPlayer.TryGetValue(clientId, out var list))
            {
                list = new List<QuestInstance>();
                _questsByPlayer[clientId] = list;
            }
            return list;
        }

        public int GetReputation(ulong clientId, FactionId faction)
            => GetReputation(clientId, (int)faction);

        public int GetReputation(ulong clientId, FactionDefinition faction)
            => GetReputation(clientId, faction != null ? faction.EffectiveWireId : 0);

        public int GetReputation(ulong clientId, int wireId)
        {
            if (wireId <= 0 || wireId > byte.MaxValue) return 0;
            return _reputation.TryGetValue((clientId, wireId), out var v) ? v : 0;
        }

        public int GetNpcAttitude(ulong clientId, string npcId)
        {
            return _npcAttitude.TryGetValue((clientId, npcId), out var v) ? v : 0;
        }

        // ============ T-MERCH01: Exact (float) Attitude/Reputation + Merchant Logic ============

        public float GetNpcTradeRepRemainder(ulong clientId, string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return 0f;
            return _npcTradeRepRemainder.TryGetValue((clientId, npcId), out var r) ? r : 0f;
        }

        public float GetFactionTradeRepRemainder(ulong clientId, int wireId)
        {
            if (wireId <= 0 || wireId > byte.MaxValue) return 0f;
            return _factionTradeRepRemainder.TryGetValue((clientId, wireId), out var r) ? r : 0f;
        }

        public float GetNpcAttitudeExact(ulong clientId, string npcId)
        {
            return GetNpcAttitude(clientId, npcId) + GetNpcTradeRepRemainder(clientId, npcId);
        }

        public float GetReputationExact(ulong clientId, int wireId)
        {
            return GetReputation(clientId, wireId) + GetFactionTradeRepRemainder(clientId, wireId);
        }

        /// <summary>
        /// T-MERCH01: Начислить репутацию с NPC и его фракцией за полученные от игрока кредиты:
        /// скорость +0.1 за каждые 100 CR (= 0.001 за 1 CR).
        /// Дробная часть хранится в серверной памяти (и сохраняется в JSON), а при накоплении >= 1.0
        /// переносится в основную таблицу через ModifyNpcAttitude / ModifyReputation.
        /// </summary>
        public (float gain, float exactNpcAttitude, float exactFactionRep) ApplyMerchantTradeReputation(
            ulong clientId, NpcDefinition npc, int creditsReceivedFromPlayer)
        {
            if (npc == null || string.IsNullOrEmpty(npc.npcId) || creditsReceivedFromPlayer <= 0)
            {
                string id = npc != null ? npc.npcId : "";
                int fw = npc != null ? npc.EffectiveFactionWireId : 0;
                return (0f, GetNpcAttitudeExact(clientId, id), GetReputationExact(clientId, fw));
            }

            float gain = MerchantPriceResolver.CalculateReputationGain(creditsReceivedFromPlayer);
            string npcId = npc.npcId;
            int factionWireId = npc.EffectiveFactionWireId;

            // 1. Личное отношение с NPC
            float npcRem = GetNpcTradeRepRemainder(clientId, npcId) + gain;
            int wholeNpcDelta = Mathf.FloorToInt(npcRem + 1e-5f);
            if (wholeNpcDelta >= 1)
            {
                npcRem = Mathf.Max(0f, npcRem - wholeNpcDelta);
                _npcTradeRepRemainder[(clientId, npcId)] = npcRem;
                ModifyNpcAttitude(clientId, npcId, wholeNpcDelta, npc, Database);
            }
            else
            {
                _npcTradeRepRemainder[(clientId, npcId)] = npcRem;
            }

            // 2. Репутация с фракцией этого NPC
            if (factionWireId > 0 && factionWireId <= byte.MaxValue)
            {
                float facRem = GetFactionTradeRepRemainder(clientId, factionWireId) + gain;
                int wholeFacDelta = Mathf.FloorToInt(facRem + 1e-5f);
                if (wholeFacDelta >= 1)
                {
                    facRem = Mathf.Max(0f, facRem - wholeFacDelta);
                    _factionTradeRepRemainder[(clientId, factionWireId)] = facRem;
                    ModifyReputation(clientId, factionWireId, wholeFacDelta);
                }
                else
                {
                    _factionTradeRepRemainder[(clientId, factionWireId)] = facRem;
                }
            }

            // Сохраняем в серверный JSON даже если изменился только дробный остаток (< 1.0)
            SavePlayer(clientId);

            float exactAtt = GetNpcAttitudeExact(clientId, npcId);
            float exactRep = GetReputationExact(clientId, factionWireId);

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[QuestWorld.Merchant] Player {clientId} spent {creditsReceivedFromPlayer} CR at NPC '{npcId}' → " +
                          $"gain=+{gain:0.###}, attitude={exactAtt:0.##}, faction({factionWireId})={exactRep:0.##}");
            }

            return (gain, exactAtt, exactRep);
        }

        /// <summary>Получить баланс кредитов игрока на сервере (TradeWorld.Repository или fallback).</summary>
        public float GetPlayerCredits(ulong clientId)
        {
            var repo = ProjectC.Trade.Core.TradeWorld.Instance?.Repository;
            if (repo != null)
                return repo.GetCredits(clientId);
            if (!_fallbackCredits.TryGetValue(clientId, out float c))
            {
                c = 1000f;
                _fallbackCredits[clientId] = c;
            }
            return c;
        }

        /// <summary>Установить баланс кредитов игрока на сервере.</summary>
        public void SetPlayerCredits(ulong clientId, float newCredits)
        {
            float clamped = Mathf.Max(0f, newCredits);
            var repo = ProjectC.Trade.Core.TradeWorld.Instance?.Repository;
            if (repo != null)
            {
                repo.SetCredits(clientId, clamped);
            }
            _fallbackCredits[clientId] = clamped;
        }

        public int GetMerchantItemStock(string npcId, int itemId, int configuredStock)
        {
            if (configuredStock < 0) return -1; // бесконечный сток
            var key = (npcId ?? "", itemId);
            if (!_merchantRuntimeStock.TryGetValue(key, out int cur))
            {
                cur = configuredStock;
                _merchantRuntimeStock[key] = cur;
            }
            return cur;
        }

        public void SetMerchantItemStock(string npcId, int itemId, int newStock)
        {
            var key = (npcId ?? "", itemId);
            _merchantRuntimeStock[key] = Mathf.Max(0, newStock);
        }

        /// <summary>
        /// T-MERCH01: Построить полный серверный снапшот магазина NPC для клиента.
        /// </summary>
        public MerchantShopSnapshotDto BuildMerchantShopSnapshot(ulong clientId, NpcDefinition npc)
        {
            if (npc == null || !npc.IsMerchant)
            {
                return new MerchantShopSnapshotDto
                {
                    npcId = npc != null ? npc.npcId : "",
                    isMerchant = false,
                    shopItems = Array.Empty<MerchantItemDto>(),
                    playerSellableItems = Array.Empty<MerchantPlayerSellItemDto>()
                };
            }

            var inv = ProjectC.Items.InventoryWorld.Instance;
            int usedSlots = inv != null ? inv.GetOrCreate(clientId).TotalCount : 0;
            int maxSlots = inv != null ? inv.MaxSlots : 32;
            float playerCredits = GetPlayerCredits(clientId);

            int factionWireId = npc.EffectiveFactionWireId;
            float exactAtt = GetNpcAttitudeExact(clientId, npc.npcId);
            float exactRep = GetReputationExact(clientId, factionWireId);

            string factionName = "";
            if (factionWireId > 0)
            {
                var fDef = GetFactionCatalog().GetByWireId(factionWireId);
                factionName = fDef != null && !string.IsNullOrEmpty(fDef.displayName)
                    ? fDef.displayName
                    : (npc.factionRef != null ? npc.factionRef.EffectiveFactionKey : npc.faction.ToString());
            }

            var table = npc.merchantTable;
            bool isTestAll = npc.sellAllItemsForTesting;
            float priceMult = table != null ? table.priceMultiplier : 1.0f;
            bool allowSell = isTestAll ? npc.allowPlayerSellInTestMode : (table != null ? table.allowPlayerSellToNpc : npc.allowPlayerSellInTestMode);
            float sellBackMult = table != null ? table.sellBackPriceMultiplier : 0.5f;
            bool enableDiscount = table != null ? table.enableReputationDiscount : true;
            float maxDisc = table != null ? table.EffectiveMaxDiscountRatio : MerchantPriceResolver.DefaultMaxDiscountRatio;
            float maxMarkup = table != null ? table.maxReputationMarkupRatio : MerchantPriceResolver.DefaultMaxMarkupRatio;
            bool enableRefusal = table != null ? table.refuseTradeOnLowReputation : true;
            float refuseAtt = table != null ? table.refuseTradeNpcAttitudeThreshold : MerchantPriceResolver.DefaultRefuseNpcAttitude;
            float refuseFac = table != null ? table.refuseTradeFactionRepThreshold : MerchantPriceResolver.DefaultRefuseFactionRep;
            float refuseScore = table != null ? table.refuseTradeStandingScoreThreshold : MerchantPriceResolver.DefaultRefuseStandingScore;
            int defaultPrice = npc.testModeDefaultPrice > 0 ? npc.testModeDefaultPrice : 25;

            var (isRefused, refusedReason) = MerchantPriceResolver.EvaluateTradeRefusal(
                exactAtt, exactRep, factionName, enableRefusal, refuseAtt, refuseFac, refuseScore);
            var standingTier = MerchantPriceResolver.ResolveStandingTier(
                exactAtt, exactRep, enableRefusal, refuseAtt, refuseFac, refuseScore);
            float standingScore = MerchantPriceResolver.CalculateStandingScore(exactAtt, exactRep);

            float attFactor = MerchantPriceResolver.CalculateAttitudePriceFactor(exactAtt, exactRep, enableDiscount, maxDisc, maxMarkup);
            int discountPct = Mathf.RoundToInt((attFactor - 1f) * 100f);

            string shopTitle = (table != null && !string.IsNullOrEmpty(table.shopTitle))
                ? table.shopTitle
                : (!string.IsNullOrEmpty(npc.displayName) ? $"Лавка: {Loc.Get(npc.displayName, npc.displayName)}" : $"Лавка {npc.npcId}");

            var shopList = new List<MerchantItemDto>();
            var customSellPriceByItemId = new Dictionary<int, (int customBuy, int customSell, int currentBuy)>();

            if (isTestAll)
            {
                // Режим "Продаёт ВСЁ" (Test Mode): берём все зарегистрированные pickable items (кроме Key)
                if (inv != null)
                {
                    foreach (var kvp in inv.GetAllItems())
                    {
                        int id = kvp.Key;
                        var itemDef = kvp.Value;
                        if (itemDef == null || itemDef.itemType == ProjectC.Items.ItemType.Key) continue;

                        int buyP = MerchantPriceResolver.ResolveBuyPrice(
                            itemDef, 0, priceMult, defaultPrice, exactAtt, exactRep, enableDiscount, maxDisc, maxMarkup);
                        int sellP = MerchantPriceResolver.ResolveSellPrice(
                            itemDef, 0, 0, sellBackMult, defaultPrice, buyP, exactAtt, exactRep, enableDiscount);

                        customSellPriceByItemId[id] = (0, 0, buyP);
                        shopList.Add(new MerchantItemDto
                        {
                            itemId = id,
                            itemType = (byte)itemDef.itemType,
                            itemName = itemDef.itemName ?? $"Item #{id}",
                            buyPrice = buyP,
                            sellPrice = sellP,
                            stock = -1,
                            playerOwnedCount = inv.CountOf(clientId, id),
                            isUnlocked = !isRefused,
                            lockedReason = isRefused ? "Отказ от торговли" : ""
                        });
                    }
                }
                shopList.Sort((a, b) =>
                {
                    int t = a.itemType.CompareTo(b.itemType);
                    return t != 0 ? t : string.Compare(a.itemName, b.itemName, StringComparison.OrdinalIgnoreCase);
                });
            }
            else if (table != null && table.items != null)
            {
                var seenIds = new HashSet<int>();
                for (int i = 0; i < table.items.Length; i++)
                {
                    var entry = table.items[i];
                    if (entry == null || entry.item == null) continue;
                    int id = ResolveItemId(null, entry.item);
                    if (id <= 0 || !seenIds.Add(id)) continue;

                    int buyP = MerchantPriceResolver.ResolveBuyPrice(
                        entry.item, entry.customBuyPrice, priceMult, defaultPrice, exactAtt, exactRep, enableDiscount, maxDisc, maxMarkup);
                    int sellP = MerchantPriceResolver.ResolveSellPrice(
                        entry.item, entry.customBuyPrice, entry.customSellPrice, sellBackMult, defaultPrice, buyP, exactAtt, exactRep, enableDiscount);
                    int curStock = GetMerchantItemStock(npc.npcId, id, entry.stock);

                    var (unlocked, reason) = CheckMerchantEntryUnlocked(clientId, npc, entry);
                    if (isRefused)
                    {
                        unlocked = false;
                        reason = "Отказ от торговли";
                    }
                    customSellPriceByItemId[id] = (entry.customBuyPrice, entry.customSellPrice, buyP);

                    shopList.Add(new MerchantItemDto
                    {
                        itemId = id,
                        itemType = (byte)entry.item.itemType,
                        itemName = entry.item.itemName ?? $"Item #{id}",
                        buyPrice = buyP,
                        sellPrice = sellP,
                        stock = curStock,
                        playerOwnedCount = inv != null ? inv.CountOf(clientId, id) : 0,
                        isUnlocked = unlocked,
                        lockedReason = reason ?? ""
                    });
                }
            }

            // Собираем предметы игрока для вкладки «Продать»
            var sellList = new List<MerchantPlayerSellItemDto>();
            if (allowSell && inv != null)
            {
                var snap = inv.BuildSnapshot(clientId, null);
                if (snap.items != null)
                {
                    var grouped = new Dictionary<int, (byte type, string name, int count)>();
                    for (int i = 0; i < snap.items.Length; i++)
                    {
                        var it = snap.items[i];
                        if (it.quantity <= 0 || it.itemId <= 0) continue;
                        if ((ProjectC.Items.ItemType)it.type == ProjectC.Items.ItemType.Key) continue; // ключи кораблей не продаются
                        if (grouped.TryGetValue(it.itemId, out var prev))
                            grouped[it.itemId] = (prev.type, prev.name, prev.count + it.quantity);
                        else
                            grouped[it.itemId] = (it.type, it.itemName, it.quantity);
                    }

                    foreach (var kvp in grouped)
                    {
                        int id = kvp.Key;
                        var itemDef = inv.GetItemDefinition(id);
                        int customBuy = 0, customSell = 0, buyLimit = 0;
                        if (customSellPriceByItemId.TryGetValue(id, out var customTuple))
                        {
                            customBuy = customTuple.customBuy;
                            customSell = customTuple.customSell;
                            buyLimit = customTuple.currentBuy;
                        }
                        string displayItemName = itemDef != null && !string.IsNullOrEmpty(itemDef.itemName) ? itemDef.itemName : kvp.Value.name;
                        if (TryGetPlayerUpgradedItem(clientId, id, out var upgEntry) && upgEntry != null)
                        {
                            if (!string.IsNullOrEmpty(upgEntry.displayName))
                                displayItemName = upgEntry.displayName;
                            if (upgEntry.finalPrice > 0)
                            {
                                customBuy = upgEntry.finalPrice;
                                customSell = 0;
                                buyLimit = 0;
                            }
                        }
                        if (buyLimit <= 0)
                        {
                            buyLimit = MerchantPriceResolver.ResolveBuyPrice(
                                itemDef, customBuy, priceMult, defaultPrice, exactAtt, exactRep, enableDiscount, maxDisc, maxMarkup);
                        }
                        int sellP = MerchantPriceResolver.ResolveSellPrice(
                            itemDef, customBuy, customSell, sellBackMult, defaultPrice, buyLimit, exactAtt, exactRep, enableDiscount);

                        sellList.Add(new MerchantPlayerSellItemDto
                        {
                            itemId = id,
                            itemType = kvp.Value.type,
                            itemName = displayItemName,
                            ownedCount = kvp.Value.count,
                            sellPrice = sellP
                        });
                    }
                }
            }

            return new MerchantShopSnapshotDto
            {
                npcId = npc.npcId,
                shopTitle = shopTitle,
                factionDisplayName = factionName,
                isMerchant = true,
                isTestSellAllMode = isTestAll,
                allowPlayerSell = allowSell,
                playerCredits = playerCredits,
                inventoryUsedSlots = usedSlots,
                inventoryMaxSlots = maxSlots,
                npcAttitudeExact = exactAtt,
                factionReputationExact = exactRep,
                discountPercent = discountPct,
                standingTier = (byte)standingTier,
                standingScore = standingScore,
                isTradeRefused = isRefused,
                tradeRefusedReason = refusedReason ?? "",
                shopItems = shopList.ToArray(),
                playerSellableItems = sellList.ToArray()
            };
        }

        private (bool unlocked, string reason) CheckMerchantEntryUnlocked(ulong clientId, NpcDefinition npc, NpcMerchantItemEntry entry)
        {
            if (entry == null) return (false, "Недоступно");
            float exactAtt = GetNpcAttitudeExact(clientId, npc.npcId);
            if (entry.minNpcAttitude > -100 && exactAtt + 1e-4f < entry.minNpcAttitude)
            {
                return (false, $"Нужно отношение ❤ {(entry.minNpcAttitude > 0 ? "+" : "")}{entry.minNpcAttitude}");
            }

            int factionWireId = npc.EffectiveFactionWireId;
            if (factionWireId > 0 && entry.minFactionReputation > -100)
            {
                float exactRep = GetReputationExact(clientId, factionWireId);
                if (exactRep + 1e-4f < entry.minFactionReputation)
                {
                    return (false, $"Нужна репутация ⚑ {(entry.minFactionReputation > 0 ? "+" : "")}{entry.minFactionReputation}");
                }
            }

            string reqQuestId = entry.GetRequiredQuestId();
            if (!string.IsNullOrEmpty(reqQuestId))
            {
                var st = GetPlayerQuestState(clientId, reqQuestId);
                if (!st.HasValue || (st.Value != QuestState.Completed && st.Value != QuestState.TurnedIn))
                {
                    var qDef = GetQuest(reqQuestId);
                    string qName = qDef != null && !string.IsNullOrEmpty(qDef.displayName) ? qDef.displayName : reqQuestId;
                    return (false, $"Нужен квест: {qName}");
                }
            }

            return (true, "");
        }

        /// <summary>
        /// T-MERCH01: Серверная покупка предмета у NPC за кредиты.
        /// Начисляет +0.1 отношения с NPC и +0.1 репутации его фракции за каждые 100 CR.
        /// </summary>
        public MerchantTradeResultDto TryMerchantBuy(ulong clientId, NpcDefinition npc, int itemId, int quantity)
        {
            if (npc == null)
                return FailMerchant(MerchantTradeResultCode.NpcNotFound, true, "", itemId, quantity, "NPC не найден");
            if (!npc.IsMerchant)
                return FailMerchant(MerchantTradeResultCode.NotMerchant, true, npc.npcId, itemId, quantity, "Этот NPC не торгует");
            if (itemId <= 0 || quantity <= 0)
                return FailMerchant(MerchantTradeResultCode.InvalidArgs, true, npc.npcId, itemId, quantity, "Некорректное количество");

            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv == null)
                return FailMerchant(MerchantTradeResultCode.InternalError, true, npc.npcId, itemId, quantity, "Инвентарь не инициализирован");

            var itemDef = inv.GetItemDefinition(itemId);
            if (itemDef == null || itemDef.itemType == ProjectC.Items.ItemType.Key)
                return FailMerchant(MerchantTradeResultCode.ItemNotInShop, true, npc.npcId, itemId, quantity, "Предмет недоступен для покупки");

            var table = npc.merchantTable;
            bool isTestAll = npc.sellAllItemsForTesting;
            float priceMult = table != null ? table.priceMultiplier : 1.0f;
            bool enableDiscount = table != null ? table.enableReputationDiscount : true;
            float maxDisc = table != null ? table.EffectiveMaxDiscountRatio : MerchantPriceResolver.DefaultMaxDiscountRatio;
            float maxMarkup = table != null ? table.maxReputationMarkupRatio : MerchantPriceResolver.DefaultMaxMarkupRatio;
            bool enableRefusal = table != null ? table.refuseTradeOnLowReputation : true;
            float refuseAtt = table != null ? table.refuseTradeNpcAttitudeThreshold : MerchantPriceResolver.DefaultRefuseNpcAttitude;
            float refuseFac = table != null ? table.refuseTradeFactionRepThreshold : MerchantPriceResolver.DefaultRefuseFactionRep;
            float refuseScore = table != null ? table.refuseTradeStandingScoreThreshold : MerchantPriceResolver.DefaultRefuseStandingScore;
            int defaultPrice = npc.testModeDefaultPrice > 0 ? npc.testModeDefaultPrice : 25;

            int factionWireId = npc.EffectiveFactionWireId;
            float exactAtt = GetNpcAttitudeExact(clientId, npc.npcId);
            float exactRep = GetReputationExact(clientId, factionWireId);

            var (isRefused, refusedReason) = MerchantPriceResolver.EvaluateTradeRefusal(
                exactAtt, exactRep, "", enableRefusal, refuseAtt, refuseFac, refuseScore);
            if (isRefused)
                return FailMerchant(MerchantTradeResultCode.TradeRefused, true, npc.npcId, itemId, quantity, refusedReason);

            int customBuyPrice = 0;
            int configuredStock = -1;

            if (!isTestAll)
            {
                if (table == null || table.items == null)
                    return FailMerchant(MerchantTradeResultCode.ItemNotInShop, true, npc.npcId, itemId, quantity, "Ассортимент пуст");

                NpcMerchantItemEntry foundEntry = null;
                for (int i = 0; i < table.items.Length; i++)
                {
                    var e = table.items[i];
                    if (e == null || e.item == null) continue;
                    if (ResolveItemId(null, e.item) == itemId)
                    {
                        foundEntry = e;
                        break;
                    }
                }
                if (foundEntry == null)
                    return FailMerchant(MerchantTradeResultCode.ItemNotInShop, true, npc.npcId, itemId, quantity, "Товар отсутствует у этого торговца");

                var (unlocked, reason) = CheckMerchantEntryUnlocked(clientId, npc, foundEntry);
                if (!unlocked)
                    return FailMerchant(MerchantTradeResultCode.RequirementsNotMet, true, npc.npcId, itemId, quantity, reason);

                customBuyPrice = foundEntry.customBuyPrice;
                configuredStock = foundEntry.stock;
            }

            int curStock = GetMerchantItemStock(npc.npcId, itemId, configuredStock);
            if (curStock >= 0 && curStock < quantity)
                return FailMerchant(MerchantTradeResultCode.OutOfStock, true, npc.npcId, itemId, quantity, $"В наличии только {curStock} шт.");

            // Проверка свободных слотов в рюкзаке игрока ДО списания кредитов
            int currentSlots = inv.GetOrCreate(clientId).TotalCount;
            if (currentSlots + quantity > inv.MaxSlots)
                return FailMerchant(MerchantTradeResultCode.InventoryFull, true, npc.npcId, itemId, quantity,
                    $"Недостаточно места в рюкзаке ({currentSlots}/{inv.MaxSlots})");

            int unitPrice = MerchantPriceResolver.ResolveBuyPrice(
                itemDef, customBuyPrice, priceMult, defaultPrice, exactAtt, exactRep, enableDiscount, maxDisc, maxMarkup);
            int totalCost = unitPrice * quantity;

            float playerCredits = GetPlayerCredits(clientId);
            if (playerCredits + 1e-3f < totalCost)
                return FailMerchant(MerchantTradeResultCode.InsufficientCredits, true, npc.npcId, itemId, quantity,
                    $"Недостаточно кредитов: нужно {totalCost} CR, у вас {Mathf.FloorToInt(playerCredits)} CR");

            // Выдаём предметы на сервере
            int addedCount = 0;
            for (int n = 0; n < quantity; n++)
            {
                var addRes = inv.AddItemDirect(clientId, itemId, itemDef.itemType);
                if (!addRes.IsSuccess)
                    break;
                addedCount++;
            }

            if (addedCount <= 0)
                return FailMerchant(MerchantTradeResultCode.InventoryFull, true, npc.npcId, itemId, quantity, "Не удалось поместить предмет в рюкзак");

            int actualCost = unitPrice * addedCount;
            float newCredits = Mathf.Max(0f, playerCredits - actualCost);
            SetPlayerCredits(clientId, newCredits);

            if (curStock >= 0)
                SetMerchantItemStock(npc.npcId, itemId, curStock - addedCount);

            // Начисляем +0.1 отношения и репутации фракции за каждые 100 CR, полученные от игрока
            var (repGain, newExactAtt, newExactRep) = ApplyMerchantTradeReputation(clientId, npc, actualCost);

            string itemName = itemDef.itemName ?? $"Item #{itemId}";
            string msg = repGain > 0f
                ? $"Куплено: {itemName} x{addedCount} (-{actualCost} CR, +{repGain:0.##} ❤/⚑)"
                : $"Куплено: {itemName} x{addedCount} (-{actualCost} CR)";

            return new MerchantTradeResultDto
            {
                code = (byte)MerchantTradeResultCode.Ok,
                isBuy = true,
                npcId = npc.npcId,
                itemId = itemId,
                itemName = itemName,
                quantity = addedCount,
                totalCreditsDelta = actualCost,
                newCredits = newCredits,
                reputationGain = repGain,
                newNpcAttitudeExact = newExactAtt,
                newFactionReputationExact = newExactRep,
                message = msg
            };
        }

        /// <summary>
        /// T-MERCH01: Серверная продажа предмета из инвентаря игрока торговцу NPC.
        /// </summary>
        public MerchantTradeResultDto TryMerchantSell(ulong clientId, NpcDefinition npc, int itemId, int quantity)
        {
            if (npc == null)
                return FailMerchant(MerchantTradeResultCode.NpcNotFound, false, "", itemId, quantity, "NPC не найден");
            if (!npc.IsMerchant)
                return FailMerchant(MerchantTradeResultCode.NotMerchant, false, npc.npcId, itemId, quantity, "Этот NPC не торгует");
            if (itemId <= 0 || quantity <= 0)
                return FailMerchant(MerchantTradeResultCode.InvalidArgs, false, npc.npcId, itemId, quantity, "Некорректное количество");

            var table = npc.merchantTable;
            bool isTestAll = npc.sellAllItemsForTesting;
            bool allowSell = isTestAll ? npc.allowPlayerSellInTestMode : (table != null ? table.allowPlayerSellToNpc : npc.allowPlayerSellInTestMode);
            if (!allowSell)
                return FailMerchant(MerchantTradeResultCode.SellDisabled, false, npc.npcId, itemId, quantity, "Этот торговец не скупает предметы");

            int factionWireId = npc.EffectiveFactionWireId;
            float exactAtt = GetNpcAttitudeExact(clientId, npc.npcId);
            float exactRep = GetReputationExact(clientId, factionWireId);
            bool enableRefusal = table != null ? table.refuseTradeOnLowReputation : true;
            float refuseAtt = table != null ? table.refuseTradeNpcAttitudeThreshold : MerchantPriceResolver.DefaultRefuseNpcAttitude;
            float refuseFac = table != null ? table.refuseTradeFactionRepThreshold : MerchantPriceResolver.DefaultRefuseFactionRep;
            float refuseScore = table != null ? table.refuseTradeStandingScoreThreshold : MerchantPriceResolver.DefaultRefuseStandingScore;

            var (isRefused, refusedReason) = MerchantPriceResolver.EvaluateTradeRefusal(
                exactAtt, exactRep, "", enableRefusal, refuseAtt, refuseFac, refuseScore);
            if (isRefused)
                return FailMerchant(MerchantTradeResultCode.TradeRefused, false, npc.npcId, itemId, quantity, refusedReason);

            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv == null)
                return FailMerchant(MerchantTradeResultCode.InternalError, false, npc.npcId, itemId, quantity, "Инвентарь не инициализирован");

            var itemDef = inv.GetItemDefinition(itemId);
            if (itemDef == null || itemDef.itemType == ProjectC.Items.ItemType.Key)
                return FailMerchant(MerchantTradeResultCode.SellDisabled, false, npc.npcId, itemId, quantity, "Этот предмет нельзя продать");

            int owned = inv.CountOf(clientId, itemId);
            if (owned < quantity)
                return FailMerchant(MerchantTradeResultCode.ItemNotOwned, false, npc.npcId, itemId, quantity,
                    $"В рюкзаке только {owned} шт.");

            float priceMult = table != null ? table.priceMultiplier : 1.0f;
            float sellBackMult = table != null ? table.sellBackPriceMultiplier : 0.5f;
            bool enableDiscount = table != null ? table.enableReputationDiscount : true;
            float maxDisc = table != null ? table.EffectiveMaxDiscountRatio : MerchantPriceResolver.DefaultMaxDiscountRatio;
            float maxMarkup = table != null ? table.maxReputationMarkupRatio : MerchantPriceResolver.DefaultMaxMarkupRatio;
            int defaultPrice = npc.testModeDefaultPrice > 0 ? npc.testModeDefaultPrice : 25;

            int customBuy = 0, customSell = 0, configuredStock = -1;
            if (table != null && table.items != null)
            {
                for (int i = 0; i < table.items.Length; i++)
                {
                    var e = table.items[i];
                    if (e != null && e.item != null && ResolveItemId(null, e.item) == itemId)
                    {
                        customBuy = e.customBuyPrice;
                        customSell = e.customSellPrice;
                        configuredStock = e.stock;
                        break;
                    }
                }
            }

            int currentBuyLimit = MerchantPriceResolver.ResolveBuyPrice(
                itemDef, customBuy, priceMult, defaultPrice, exactAtt, exactRep, enableDiscount, maxDisc, maxMarkup);
            string soldItemName = itemDef.itemName ?? $"Item #{itemId}";
            if (TryGetPlayerUpgradedItem(clientId, itemId, out var soldUpg) && soldUpg != null)
            {
                if (!string.IsNullOrEmpty(soldUpg.displayName))
                    soldItemName = soldUpg.displayName;
                if (soldUpg.finalPrice > 0)
                {
                    customBuy = soldUpg.finalPrice;
                    customSell = 0;
                    currentBuyLimit = MerchantPriceResolver.ResolveBuyPrice(
                        itemDef, customBuy, priceMult, defaultPrice, exactAtt, exactRep, enableDiscount, maxDisc, maxMarkup);
                }
            }
            int unitSellPrice = MerchantPriceResolver.ResolveSellPrice(
                itemDef, customBuy, customSell, sellBackMult, defaultPrice, currentBuyLimit, exactAtt, exactRep, enableDiscount);

            var remRes = inv.RemoveItems(clientId, itemId, itemDef.itemType, quantity);
            if (!remRes.IsSuccess)
                return FailMerchant(MerchantTradeResultCode.ItemNotOwned, false, npc.npcId, itemId, quantity, "Не удалось списать предмет из рюкзака");

            // Если все экземпляры этого предмета проданы — сбрасываем запись улучшения для этого itemId
            if (inv.CountOf(clientId, itemId) <= 0)
            {
                _upgradedItemsByPlayerItem.Remove((clientId, itemId));
                SavePlayer(clientId);
            }

            int totalEarned = unitSellPrice * quantity;
            float newCredits = GetPlayerCredits(clientId) + totalEarned;
            SetPlayerCredits(clientId, newCredits);

            // Если товар есть в лимитированном стоке торговца — пополняем его сток
            int curStock = GetMerchantItemStock(npc.npcId, itemId, configuredStock);
            if (curStock >= 0)
                SetMerchantItemStock(npc.npcId, itemId, curStock + quantity);

            return new MerchantTradeResultDto
            {
                code = (byte)MerchantTradeResultCode.Ok,
                isBuy = false,
                npcId = npc.npcId,
                itemId = itemId,
                itemName = soldItemName,
                quantity = quantity,
                totalCreditsDelta = totalEarned,
                newCredits = newCredits,
                reputationGain = 0f,
                newNpcAttitudeExact = exactAtt,
                newFactionReputationExact = exactRep,
                message = $"Продано: {soldItemName} x{quantity} (+{totalEarned} CR)"
            };
        }

        // ============ T-SMITH01: Серверная логика Кузнеца (+0.2 реп за 100 CR) ============

        public bool TryGetPlayerUpgradedItem(
            ulong clientId,
            int itemId,
            out ProjectC.Quests.Persistence.BlacksmithUpgradedItemSaveEntry entry)
        {
            return _upgradedItemsByPlayerItem.TryGetValue((clientId, itemId), out entry) && entry != null;
        }

        /// <summary>
        /// T-SMITH01: Начислить репутацию с кузнецом и его фракцией со скоростью +0.2 за каждые 100 CR,
        /// потраченные игроком у кузнеца (= 0.002 за 1 CR).
        /// </summary>
        public (float gain, float exactNpcAttitude, float exactFactionRep) ApplyBlacksmithUpgradeReputation(
            ulong clientId, NpcDefinition npc, int creditsSpentByPlayer)
        {
            if (npc == null || string.IsNullOrEmpty(npc.npcId) || creditsSpentByPlayer <= 0)
            {
                string id = npc != null ? npc.npcId : "";
                int fw = npc != null ? npc.EffectiveFactionWireId : 0;
                return (0f, GetNpcAttitudeExact(clientId, id), GetReputationExact(clientId, fw));
            }

            float gain = BlacksmithUpgradeEngine.CalculateReputationGain(creditsSpentByPlayer);
            string npcId = npc.npcId;
            int factionWireId = npc.EffectiveFactionWireId;

            float npcRem = GetNpcTradeRepRemainder(clientId, npcId) + gain;
            int wholeNpcDelta = Mathf.FloorToInt(npcRem + 1e-5f);
            if (wholeNpcDelta >= 1)
            {
                npcRem = Mathf.Max(0f, npcRem - wholeNpcDelta);
                _npcTradeRepRemainder[(clientId, npcId)] = npcRem;
                ModifyNpcAttitude(clientId, npcId, wholeNpcDelta, npc, Database);
            }
            else
            {
                _npcTradeRepRemainder[(clientId, npcId)] = npcRem;
            }

            if (factionWireId > 0 && factionWireId <= byte.MaxValue)
            {
                float facRem = GetFactionTradeRepRemainder(clientId, factionWireId) + gain;
                int wholeFacDelta = Mathf.FloorToInt(facRem + 1e-5f);
                if (wholeFacDelta >= 1)
                {
                    facRem = Mathf.Max(0f, facRem - wholeFacDelta);
                    _factionTradeRepRemainder[(clientId, factionWireId)] = facRem;
                    ModifyReputation(clientId, factionWireId, wholeFacDelta);
                }
                else
                {
                    _factionTradeRepRemainder[(clientId, factionWireId)] = facRem;
                }
            }

            SavePlayer(clientId);
            return (gain, GetNpcAttitudeExact(clientId, npcId), GetReputationExact(clientId, factionWireId));
        }

        /// <summary>
        /// T-SMITH01: Построить полный серверный снимок Кузницы NPC для клиента.
        /// Включает все pickable-предметы в рюкзаке игрока (от яблок и ресурсов до оружия и брони)
        /// и расчёт всех 4 тиров (Бронза, Сталь, Композит, Антигравий) с двумя режимами оплаты.
        /// </summary>
        public BlacksmithShopSnapshotDto BuildBlacksmithShopSnapshot(ulong clientId, NpcDefinition npc)
        {
            if (npc == null || !npc.IsBlacksmith)
            {
                return new BlacksmithShopSnapshotDto
                {
                    npcId = npc != null ? npc.npcId : "",
                    isBlacksmith = false,
                    playerItems = Array.Empty<BlacksmithTargetItemDto>()
                };
            }

            var inv = ProjectC.Items.InventoryWorld.Instance;
            float playerCredits = GetPlayerCredits(clientId);
            int factionWireId = npc.EffectiveFactionWireId;
            float exactAtt = GetNpcAttitudeExact(clientId, npc.npcId);
            float exactRep = GetReputationExact(clientId, factionWireId);

            string factionName = "";
            if (factionWireId > 0)
            {
                var fDef = GetFactionCatalog().GetByWireId(factionWireId);
                factionName = fDef != null && !string.IsNullOrEmpty(fDef.displayName)
                    ? fDef.displayName
                    : (npc.factionRef != null ? npc.factionRef.EffectiveFactionKey : npc.faction.ToString());
            }

            var cfg = npc.blacksmithConfig;
            BlacksmithTierMask allowedMask = npc.EffectiveBlacksmithTiers;
            bool enableDisc = cfg != null ? cfg.enableReputationDiscount : true;
            float maxDisc = cfg != null ? cfg.EffectiveMaxDiscountRatio : MerchantPriceResolver.DefaultMaxDiscountRatio;
            float maxMarkup = cfg != null ? cfg.maxReputationMarkupRatio : MerchantPriceResolver.DefaultMaxMarkupRatio;
            bool enableRefusal = cfg != null ? cfg.refuseServiceOnLowReputation : true;
            var (isRefused, refusedReason) = MerchantPriceResolver.EvaluateTradeRefusal(
                exactAtt, exactRep, factionName, enableRefusal);
            var standingTier = MerchantPriceResolver.ResolveStandingTier(exactAtt, exactRep, enableRefusal);
            float standingScore = MerchantPriceResolver.CalculateStandingScore(exactAtt, exactRep);
            float attFactor = MerchantPriceResolver.CalculateAttitudePriceFactor(exactAtt, exactRep, enableDisc, maxDisc, maxMarkup);
            int discountPct = Mathf.RoundToInt((attFactor - 1f) * 100f);

            string smithyTitle = (cfg != null && !string.IsNullOrEmpty(cfg.smithyTitle))
                ? cfg.smithyTitle
                : (!string.IsNullOrEmpty(npc.displayName) ? $"Кузница: {Loc.Get(npc.displayName, npc.displayName)}" : $"Кузница {npc.npcId}");

            var itemsList = new List<BlacksmithTargetItemDto>();
            if (inv != null)
            {
                var snap = inv.BuildSnapshot(clientId, null);
                if (snap.items != null)
                {
                    var grouped = new Dictionary<int, (byte type, string name, int count)>();
                    for (int i = 0; i < snap.items.Length; i++)
                    {
                        var it = snap.items[i];
                        if (it.quantity <= 0 || it.itemId <= 0) continue;
                        if ((ProjectC.Items.ItemType)it.type == ProjectC.Items.ItemType.Key) continue;
                        if (grouped.TryGetValue(it.itemId, out var prev))
                            grouped[it.itemId] = (prev.type, prev.name, prev.count + it.quantity);
                        else
                            grouped[it.itemId] = (it.type, it.itemName, it.quantity);
                    }

                    foreach (var kvp in grouped)
                    {
                        int id = kvp.Key;
                        var itemDef = inv.GetItemDefinition(id);
                        if (itemDef == null) continue;

                        int curTier = -1;
                        string dispName = itemDef.itemName ?? kvp.Value.name;
                        string curStats = MerchantPriceResolver.BuildItemSpecsSummary(itemDef);
                        if (TryGetPlayerUpgradedItem(clientId, id, out var upg) && upg != null)
                        {
                            curTier = upg.upgradedTier;
                            if (!string.IsNullOrEmpty(upg.displayName))
                                dispName = upg.displayName;
                            if (!string.IsNullOrEmpty(upg.statsDiffSummary))
                                curStats = $"Текущее улучшение: {upg.statsDiffSummary}";
                        }

                        var tierOpts = new BlacksmithTierOptionDto[4];
                        for (int ti = 0; ti < 4; ti++)
                        {
                            var tierEnum = (BlacksmithUpgradeTier)ti;
                            bool allowed = BlacksmithUpgradeEngine.IsTierAllowed(allowedMask, tierEnum);
                            var (unlockedByRep, repReason) = CheckBlacksmithTierRepUnlocked(clientId, npc, cfg, tierEnum);
                            var (resId, resName, reqResCount, playerResCount) =
                                BlacksmithUpgradeEngine.ResolveTierResourceForPlayer(clientId, tierEnum, cfg);
                            var (withResCredits, creditsOnlyCost) =
                                BlacksmithUpgradeEngine.CalculateUpgradeCosts(itemDef, tierEnum, cfg, exactAtt, exactRep);

                            long previewSeed = ((long)id * 10007L) ^ ((long)(ti + 1) * 99991L);
                            var previewRes = BlacksmithUpgradeEngine.GenerateUpgradedItemResult(itemDef, id, tierEnum, previewSeed);
                            string diffSummary = BlacksmithUpgradeEngine.BuildUpgradeDiffSummary(previewRes.baseStats, previewRes.finalStats);

                            string lockMsg = "";
                            if (isRefused)
                                lockMsg = refusedReason;
                            else if (!allowed)
                                lockMsg = "Кузнец не работает с этим материалом";
                            else if (!unlockedByRep)
                                lockMsg = repReason;
                            else if (curTier >= ti)
                                lockMsg = "Уже улучшено до этого или более высокого тира";

                            tierOpts[ti] = new BlacksmithTierOptionDto
                            {
                                tier = (byte)ti,
                                tierName = BlacksmithUpgradeEngine.GetTierDisplayName(tierEnum),
                                colorHex = BlacksmithUpgradeEngine.GetTierColorHex(tierEnum),
                                isAllowedByNpc = allowed,
                                isUnlockedByRep = !isRefused && unlockedByRep && curTier < ti,
                                lockedReason = lockMsg,
                                resourceItemId = resId,
                                resourceName = resName ?? "",
                                requiredResourceCount = reqResCount,
                                playerResourceCount = playerResCount,
                                withResourceCreditsCost = withResCredits,
                                creditsOnlyCost = creditsOnlyCost,
                                previewNewName = previewRes.displayName ?? "",
                                previewDiffSummary = diffSummary ?? ""
                            };
                        }

                        itemsList.Add(new BlacksmithTargetItemDto
                        {
                            itemId = id,
                            itemType = kvp.Value.type,
                            displayName = dispName,
                            ownedCount = kvp.Value.count,
                            currentUpgradeTier = curTier,
                            currentStatsSummary = curStats,
                            tierOptions = tierOpts
                        });
                    }
                }
            }

            return new BlacksmithShopSnapshotDto
            {
                npcId = npc.npcId,
                smithyTitle = smithyTitle,
                factionDisplayName = factionName,
                isBlacksmith = true,
                isTestAllTiersMode = npc.blacksmithAllowAllTiersForTesting,
                allowedTiersMask = (byte)allowedMask,
                playerCredits = playerCredits,
                npcAttitudeExact = exactAtt,
                factionReputationExact = exactRep,
                discountPercent = discountPct,
                standingTier = (byte)standingTier,
                standingScore = standingScore,
                isTradeRefused = isRefused,
                tradeRefusedReason = refusedReason ?? "",
                playerItems = itemsList.ToArray()
            };
        }

        private (bool unlocked, string reason) CheckBlacksmithTierRepUnlocked(
            ulong clientId, NpcDefinition npc, NpcBlacksmithConfig cfg, BlacksmithUpgradeTier tier)
        {
            var entryOverride = cfg != null ? cfg.FindOverride(tier) : null;
            if (entryOverride == null) return (true, "");

            float exactAtt = GetNpcAttitudeExact(clientId, npc.npcId);
            if (entryOverride.minNpcAttitude > -100 && exactAtt + 1e-4f < entryOverride.minNpcAttitude)
                return (false, $"Нужно отношение ❤ {(entryOverride.minNpcAttitude > 0 ? "+" : "")}{entryOverride.minNpcAttitude}");

            int factionWireId = npc.EffectiveFactionWireId;
            if (factionWireId > 0 && entryOverride.minFactionReputation > -100)
            {
                float exactRep = GetReputationExact(clientId, factionWireId);
                if (exactRep + 1e-4f < entryOverride.minFactionReputation)
                    return (false, $"Нужна репутация ⚑ {(entryOverride.minFactionReputation > 0 ? "+" : "")}{entryOverride.minFactionReputation}");
            }
            return (true, "");
        }

        /// <summary>
        /// T-SMITH01: Серверная обработка улучшения предмета у кузнеца.
        /// Начисляет +0.2 отношения с NPC и +0.2 репутации его фракции за каждые 100 CR.
        /// </summary>
        public BlacksmithUpgradeResultDto TryBlacksmithUpgrade(
            ulong clientId,
            NpcDefinition npc,
            int targetItemId,
            BlacksmithUpgradeTier targetTier,
            BlacksmithPaymentMode paymentMode)
        {
            if (npc == null)
                return FailBlacksmith(BlacksmithUpgradeResultCode.NpcNotFound, "", targetItemId, targetTier, paymentMode, "NPC не найден");
            if (!npc.IsBlacksmith)
                return FailBlacksmith(BlacksmithUpgradeResultCode.NotBlacksmith, npc.npcId, targetItemId, targetTier, paymentMode, "Этот NPC не является кузнецом");
            if (targetItemId <= 0)
                return FailBlacksmith(BlacksmithUpgradeResultCode.InvalidArgs, npc.npcId, targetItemId, targetTier, paymentMode, "Предмет не выбран");

            BlacksmithTierMask allowedMask = npc.EffectiveBlacksmithTiers;
            if (!BlacksmithUpgradeEngine.IsTierAllowed(allowedMask, targetTier))
                return FailBlacksmith(BlacksmithUpgradeResultCode.TierNotSupported, npc.npcId, targetItemId, targetTier, paymentMode,
                    $"Кузнец не умеет работать с материалом «{BlacksmithUpgradeEngine.GetTierDisplayName(targetTier)}»");

            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv == null)
                return FailBlacksmith(BlacksmithUpgradeResultCode.InternalError, npc.npcId, targetItemId, targetTier, paymentMode, "Инвентарь не инициализирован");

            var itemDef = inv.GetItemDefinition(targetItemId);
            if (itemDef == null || itemDef.itemType == ProjectC.Items.ItemType.Key)
                return FailBlacksmith(BlacksmithUpgradeResultCode.InvalidArgs, npc.npcId, targetItemId, targetTier, paymentMode, "Этот предмет нельзя улучшить");

            int ownedTargetCount = inv.CountOf(clientId, targetItemId);
            if (ownedTargetCount <= 0)
                return FailBlacksmith(BlacksmithUpgradeResultCode.ItemNotOwned, npc.npcId, targetItemId, targetTier, paymentMode, "Предмет отсутствует в вашем рюкзаке");

            if (TryGetPlayerUpgradedItem(clientId, targetItemId, out var existingUpg) && existingUpg != null)
            {
                if (existingUpg.upgradedTier >= (int)targetTier)
                    return FailBlacksmith(BlacksmithUpgradeResultCode.AlreadyAtOrAboveTier, npc.npcId, targetItemId, targetTier, paymentMode,
                        "Предмет уже улучшен до этого или более высокого тира");
            }

            var cfg = npc.blacksmithConfig;
            int factionWireId = npc.EffectiveFactionWireId;
            float exactAtt = GetNpcAttitudeExact(clientId, npc.npcId);
            float exactRep = GetReputationExact(clientId, factionWireId);
            bool enableRefusal = cfg != null ? cfg.refuseServiceOnLowReputation : true;
            var (isRefused, refusedReason) = MerchantPriceResolver.EvaluateTradeRefusal(
                exactAtt, exactRep, "", enableRefusal);
            if (isRefused)
                return FailBlacksmith(BlacksmithUpgradeResultCode.TradeRefused, npc.npcId, targetItemId, targetTier, paymentMode, refusedReason);

            var (unlockedByRep, repReason) = CheckBlacksmithTierRepUnlocked(clientId, npc, cfg, targetTier);
            if (!unlockedByRep)
                return FailBlacksmith(BlacksmithUpgradeResultCode.RequirementsNotMet, npc.npcId, targetItemId, targetTier, paymentMode, repReason);

            var (withResCredits, creditsOnlyCost) =
                BlacksmithUpgradeEngine.CalculateUpgradeCosts(itemDef, targetTier, cfg, exactAtt, exactRep);

            int requiredCredits = paymentMode == BlacksmithPaymentMode.WithPlayerResource
                ? withResCredits
                : creditsOnlyCost;

            float playerCredits = GetPlayerCredits(clientId);
            if (playerCredits + 1e-3f < requiredCredits)
                return FailBlacksmith(BlacksmithUpgradeResultCode.InsufficientCredits, npc.npcId, targetItemId, targetTier, paymentMode,
                    $"Недостаточно кредитов: нужно {requiredCredits} CR, у вас {Mathf.FloorToInt(playerCredits)} CR");

            // Если выбран режим "Предмет + Ресурс игрока + Доп. кредиты" — проверяем и списываем ресурс
            if (paymentMode == BlacksmithPaymentMode.WithPlayerResource)
            {
                var (resId, resName, reqResCount, playerResCount) =
                    BlacksmithUpgradeEngine.ResolveTierResourceForPlayer(clientId, targetTier, cfg);

                // Если улучшаемый предмет и есть тот самый ресурс (например, игрок улучшает сам слиток бронзы бронзой),
                // то нужно иметь в рюкзаке на 1 штуку больше (1 как предмет + reqResCount как расходный ресурс)!
                int neededInBag = (resId == targetItemId) ? (reqResCount + 1) : reqResCount;
                if (resId <= 0 || playerResCount < neededInBag)
                {
                    return FailBlacksmith(BlacksmithUpgradeResultCode.MissingResource, npc.npcId, targetItemId, targetTier, paymentMode,
                        $"Не хватает ресурса «{resName}» ({playerResCount}/{neededInBag} в рюкзаке). Выберите опцию «Только за кредиты» или добудьте ресурс.");
                }

                var resDef = inv.GetItemDefinition(resId);
                var remRes = inv.RemoveItems(clientId, resId, resDef != null ? resDef.itemType : ProjectC.Items.ItemType.Resources, reqResCount);
                if (!remRes.IsSuccess)
                {
                    return FailBlacksmith(BlacksmithUpgradeResultCode.MissingResource, npc.npcId, targetItemId, targetTier, paymentMode,
                        $"Не удалось списать ресурс «{resName}»");
                }
            }

            // Списываем кредиты
            float newCredits = Mathf.Max(0f, playerCredits - requiredCredits);
            SetPlayerCredits(clientId, newCredits);

            // Генерируем улучшенный предмет на базе ItemVariationGenerator
            long seed = ((long)clientId * 31337L) ^ ((long)targetItemId * 10007L) ^ ((long)((int)targetTier + 1) * 99991L);
            var varResult = BlacksmithUpgradeEngine.GenerateUpgradedItemResult(itemDef, targetItemId, targetTier, seed);
            int instanceId = _variationService.Instances.Add(varResult);
            var instanceDto = ProjectC.Items.Variation.Dto.ItemInstanceDto.FromResult(varResult, instanceId);
            string diffSummary = BlacksmithUpgradeEngine.BuildUpgradeDiffSummary(varResult.baseStats, varResult.finalStats);

            _upgradedItemsByPlayerItem[(clientId, targetItemId)] = new ProjectC.Quests.Persistence.BlacksmithUpgradedItemSaveEntry
            {
                itemId = targetItemId,
                instanceId = instanceId,
                upgradedTier = (int)targetTier,
                seed = seed,
                displayName = varResult.displayName ?? itemDef.itemName,
                statsDiffSummary = diffSummary,
                finalPrice = varResult.finalPrice,
                finalStats = varResult.finalStats
            };

            // Начисляем +0.2 отношения с NPC и +0.2 репутации его фракции за каждые 100 CR
            var (repGain, newExactAtt, newExactRep) = ApplyBlacksmithUpgradeReputation(clientId, npc, requiredCredits);

            string msg = $"⚒ Выковано: {varResult.displayName}! ({diffSummary}) [−{requiredCredits} CR, +{repGain:0.##} ❤/⚑]";
            return new BlacksmithUpgradeResultDto
            {
                code = (byte)BlacksmithUpgradeResultCode.Ok,
                npcId = npc.npcId,
                itemId = targetItemId,
                upgradedToTier = (byte)targetTier,
                paymentMode = (byte)paymentMode,
                upgradedDisplayName = varResult.displayName ?? itemDef.itemName,
                statsDiffSummary = diffSummary,
                creditsSpent = requiredCredits,
                newCredits = newCredits,
                reputationGain = repGain,
                newNpcAttitudeExact = newExactAtt,
                newFactionReputationExact = newExactRep,
                upgradedInstance = instanceDto,
                message = msg
            };
        }

        private static BlacksmithUpgradeResultDto FailBlacksmith(
            BlacksmithUpgradeResultCode code,
            string npcId,
            int itemId,
            BlacksmithUpgradeTier tier,
            BlacksmithPaymentMode mode,
            string msg)
        {
            return new BlacksmithUpgradeResultDto
            {
                code = (byte)code,
                npcId = npcId ?? "",
                itemId = itemId,
                upgradedToTier = (byte)tier,
                paymentMode = (byte)mode,
                upgradedDisplayName = "",
                statsDiffSummary = "",
                creditsSpent = 0,
                newCredits = -1f,
                reputationGain = 0f,
                message = msg ?? code.ToString()
            };
        }

        private MerchantTradeResultDto FailMerchant(
            MerchantTradeResultCode code, bool isBuy, string npcId, int itemId, int qty, string msg)
        {
            return new MerchantTradeResultDto
            {
                code = (byte)code,
                isBuy = isBuy,
                npcId = npcId ?? "",
                itemId = itemId,
                itemName = "",
                quantity = qty,
                totalCreditsDelta = 0,
                newCredits = -1f,
                reputationGain = 0f,
                newNpcAttitudeExact = 0f,
                newFactionReputationExact = 0f,
                message = msg ?? code.ToString()
            };
        }

        // ============ T-Q28: Prerequisite check (для цепочек через prereqQuest в CSV) ============

        /// <summary>
        /// T-Q28: возвращает QuestState текущего квеста у игрока (если в логе), иначе null.
        /// Используется в IsPrerequisiteMet для QuestCompleted/QuestActive.
        /// </summary>
        public QuestState? GetPlayerQuestState(ulong clientId, string questId)
        {
            if (string.IsNullOrEmpty(questId)) return null;
            var quests = GetPlayerQuests(clientId);
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i].questId == questId) return quests[i].state;
            }
            return null;
        }

        /// <summary>
        /// T-Q28: проверка одного atomic prerequisite для данного игрока.
        /// Покрывает все типы из QuestPrerequisiteType (QuestCompleted/QuestActive/
        /// ReputationAtLeast/NpcAttitudeAtLeast/HaveItem/FlagIsSet/PlayerFaction).
        /// AND-комбинация — caller'у (TryOffer/TryAccept).
        /// </summary>
        public bool IsPrerequisiteMet(ulong clientId, QuestPrerequisite prereq)
        {
            if (prereq == null) return true;
            switch (prereq.type)
            {
                case QuestPrerequisiteType.QuestCompleted:
                {
                    string questId = prereq.requiredQuest != null ? prereq.requiredQuest.questId : prereq.stringParam;
                    var s = GetPlayerQuestState(clientId, questId);
                    if (!s.HasValue) return false;
                    return s.Value == QuestState.Completed || s.Value == QuestState.TurnedIn;
                }
                case QuestPrerequisiteType.QuestActive:
                {
                    string questId = prereq.requiredQuest != null ? prereq.requiredQuest.questId : prereq.stringParam;
                    var s = GetPlayerQuestState(clientId, questId);
                    return s.HasValue && s.Value == QuestState.Active;
                }
                case QuestPrerequisiteType.ReputationAtLeast:
                    return GetReputation(clientId, prereq.EffectiveFactionWireId) >= prereq.intParam;
                case QuestPrerequisiteType.NpcAttitudeAtLeast:
                {
                    string npcId = prereq.requiredNpc != null ? prereq.requiredNpc.npcId : prereq.stringParam;
                    return GetNpcAttitude(clientId, npcId) >= prereq.intParam;
                }
                case QuestPrerequisiteType.HaveItem:
                {
                    // T-Q28: resolve tradeItemId → int itemId via QuestWorld.ResolveItemId
                    int itemId = ResolveItemId(prereq.stringParam);
                    if (itemId <= 0) return false;
                    return ProjectC.Items.InventoryWorld.Instance != null
                        && ProjectC.Items.InventoryWorld.Instance.CountOf(clientId, itemId) >= prereq.intParam;
                }
                case QuestPrerequisiteType.FlagIsSet:
                    return GetFlag(clientId, prereq.stringParam);
                case QuestPrerequisiteType.PlayerFaction:
                    return false; // deprecated в v2 — use ReputationAtLeast
                default:
                    return true; // unknown type — пропускаем (не блокируем)
            }
        }

        /// <summary>
        /// T-Q28: проверка ВСЕХ prerequisites (AND-комбинация). Возвращает (bool met, string failedReason).
        /// failedReason = null если met=true. Используется в TryOffer/TryAccept и в BuildFallbackDialogTree.
        /// </summary>
        public (bool met, string reason) ArePrerequisitesMet(ulong clientId, QuestDefinition def)
        {
            if (def == null) return (true, null);

            // S5: minReputation — pre-prerequisite по faction (в дополнение к prerequisites[]).
            if (def.EffectiveFactionWireId > 0 && def.minReputation > 0)
            {
                int rep = GetReputation(clientId, def.EffectiveFactionWireId);
                string factionLabel = def.factionRef != null
                    ? def.factionRef.EffectiveFactionKey
                    : def.faction.ToString();
                if (rep < def.minReputation)
                    return (false, Loc.Format("ui.quest.prereq.reputation", factionLabel, def.minReputation));
            }

            if (def.prerequisites == null || def.prerequisites.Length == 0) return (true, null);
            for (int i = 0; i < def.prerequisites.Length; i++)
            {
                var p = def.prerequisites[i];
                if (p == null) continue;
                if (!IsPrerequisiteMet(clientId, p))
                {
                    string reason = p.type switch
                    {
                        QuestPrerequisiteType.QuestCompleted => Loc.Format("ui.quest.prereq.complete_first", p.stringParam ?? ""),
                        QuestPrerequisiteType.QuestActive => Loc.Format("ui.quest.prereq.activate_first", p.stringParam ?? ""),
                        QuestPrerequisiteType.ReputationAtLeast => Loc.Format("ui.quest.prereq.reputation",
                            p.factionRef != null ? p.factionRef.EffectiveFactionKey : p.factionParam.ToString(), p.intParam),
                        QuestPrerequisiteType.NpcAttitudeAtLeast => Loc.Format("ui.quest.prereq.npc_attitude", p.requiredNpc != null ? p.requiredNpc.displayName : (p.stringParam ?? "?"), p.intParam),
                        QuestPrerequisiteType.HaveItem => Loc.Format("ui.quest.prereq.have_item", p.stringParam ?? "", p.intParam),
                        QuestPrerequisiteType.FlagIsSet => Loc.Format("ui.quest.prereq.flag", p.stringParam ?? ""),
                        _ => Loc.Format("ui.quest.prereq.unknown", i)
                    };
                    return (false, reason);
                }
            }
            return (true, null);
        }

        // ============ T-Q13: Reputation + NpcAttitude modifiers (with cross-faction influence) ============

        /// <summary>
        /// T-Q13: apply delta to per-player faction reputation (clamped to [min..max]).
        /// Publishes <see cref="ReputationChangedEvent"/> через WorldEventBus.
        /// T-Q15+: вызывается из DialogueAction.AddReputation и QuestRewardReputation grant.
        /// </summary>
        /// <param name="silent">true = НЕ publish event (для cross-fallback внутри ModifyNpcAttitude).</param>
        /// <returns>New value after clamp.</returns>
        public int ModifyReputation(ulong clientId, FactionId faction, int delta, int min = -100, int max = 100, bool silent = false)
            => ModifyReputation(clientId, (int)faction, delta, min, max, silent);

        public int ModifyReputation(ulong clientId, FactionDefinition faction, int delta, int min = -100, int max = 100, bool silent = false)
            => ModifyReputation(clientId, faction != null ? faction.EffectiveWireId : 0, delta, min, max, silent);

        public int ModifyReputation(ulong clientId, int wireId, int delta, int min = -100, int max = 100, bool silent = false)
        {
            if (wireId <= 0 || wireId > byte.MaxValue) return 0;
            int oldVal = GetReputation(clientId, wireId);
            int newVal = oldVal + delta;
            if (newVal < min) newVal = min;
            if (newVal > max) newVal = max;
            if (newVal == oldVal) return newVal;
            _reputation[(clientId, wireId)] = newVal;
            if (!silent)
            {
                WorldEventBus.Publish(new ReputationChangedEvent
                {
                    PlayerId = clientId,
                    Faction = (FactionId)wireId,
                    FactionWireId = wireId,
                    NewValue = newVal,
                    Delta = newVal - oldVal
                });
            }
            if (Debug.isDebugBuild)
            {
                var definition = GetFactionCatalog().GetByWireId(wireId);
                string label = definition != null ? definition.EffectiveFactionKey : wireId.ToString();
                Debug.Log($"[QuestWorld] ModifyReputation player={clientId} faction={label} wireId={wireId} delta={delta} {oldVal}→{newVal}");
            }
            SavePlayer(clientId); // T-Q18
            return newVal;
        }

        /// <summary>
        /// T-Q13: apply delta to per-player NpcAttitude (clamped to NpcDefinition.personalAttitudeMin/Max,
        /// fallback NpcAttitude.MinValue/MaxValue = -100..200). Cross-faction influence:
        /// для каждой link в NpcDefinition.attitudeLinks[] — применить deltaOnLike (delta > 0)
        /// или deltaOnDislike (delta < 0) к target faction (silent — избежать recursion).
        /// Publishes <see cref="NpcAttitudeChangedEvent"/>.
        /// </summary>
        public int ModifyNpcAttitude(ulong clientId, string npcId, int delta, NpcDefinition npcDef = null, QuestDatabase database = null)
        {
            if (string.IsNullOrEmpty(npcId)) return 0;
            int oldVal = GetNpcAttitude(clientId, npcId);
            int newVal = oldVal + delta;
            int min = NpcAttitude.MinValue;
            int max = NpcAttitude.MaxValue;
            if (npcDef != null)
            {
                min = npcDef.personalAttitudeMin;
                max = npcDef.personalAttitudeMax;
            }
            if (newVal < min) newVal = min;
            if (newVal > max) newVal = max;
            if (newVal == oldVal) return newVal;
            _npcAttitude[(clientId, npcId)] = newVal;

            // Cross-faction influence: для каждой link применить deltaOnLike/dislike к faction
            // (только если NpcDefinition известен и delta реально изменил attitude).
            if (npcDef != null && npcDef.attitudeLinks != null)
            {
                for (int i = 0; i < npcDef.attitudeLinks.Length; i++)
                {
                    var link = npcDef.attitudeLinks[i];
                    if (link == null) continue;
                    int crossDelta = delta > 0 ? link.deltaOnLike : link.deltaOnDislike;
                    if (crossDelta == 0) continue;
                    ModifyReputation(clientId, link.EffectiveFactionWireId, crossDelta, silent: true);
                }
            }

            WorldEventBus.Publish(new NpcAttitudeChangedEvent
            {
                PlayerId = clientId,
                NpcId = npcId,
                NewValue = newVal,
                Delta = newVal - oldVal
            });
            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] ModifyNpcAttitude player={clientId} npc={npcId} delta={delta} {oldVal}→{newVal}");
            SavePlayer(clientId); // T-Q18
            return newVal;
        }

        // ============ T-Q15: Quest state transitions (Accept / TurnIn / Track) ============

        /// <summary>
        /// T-Q15 fix: QuestServer.FireDialogAction.OfferQuest stub → real impl.
        /// Server-side offer quest to player (Discovered state). Если уже есть в log (любой state) — return Ok (idempotent).
        /// Edge action: action.stringParam = questId. НЕ auto-accept.
        /// </summary>
        public QuestResultDto TryOffer(ulong clientId, string questId)
        {
            if (string.IsNullOrEmpty(questId))
                return Fail(QuestResultCode.NotFound, "questId empty", questId);
            var def = GetQuest(questId);
            if (def == null)
                return Fail(QuestResultCode.NotFound, $"Quest '{questId}' not found", questId);

            var playerQuests = GetPlayerQuests(clientId);

            // Idempotency: если уже в log — success (no-op).
            for (int i = 0; i < playerQuests.Count; i++)
            {
                if (playerQuests[i].questId == questId)
                {
                    return Ok($"Already in log (state={playerQuests[i].state})", questId);
                }
            }

            // T-Q28: проверяем prerequisites (prereqQuest в CSV → QuestDefinition.prerequisites[]).
            // Если не выполнены — возвращаем fail БЕЗ добавления в log.
            var (prereqMet, prereqReason) = ArePrerequisitesMet(clientId, def);
            if (!prereqMet)
            {
                if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryOffer: client={clientId} quest={questId} prereq NOT met: {prereqReason}");
                return Fail(QuestResultCode.PrerequisitesNotMet, prereqReason, questId);
            }

            // Create new QuestInstance in Discovered state.
            var instance = new QuestInstance
            {
                questId = questId,
                state = QuestState.Discovered,
                isTracked = false
            };
            playerQuests.Add(instance);

            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryOffer: client={clientId} quest={questId} → Discovered");

            // T-Q18: persist on every state change (per §H, no debounce).
            SavePlayer(clientId);

            return new QuestResultDto
            {
                code = (byte)QuestResultCode.Discovered, // signal "newly discovered" to UI
                questId = questId,
                message = "Discovered"
            };
        }

        /// <summary>
        /// T-Q15: accept a Discovered/Offered quest → Active.
        /// Server-authoritative: validates state transition, quest exists, max-active cap.
        /// Out of scope T-Q15: applying reward / finalizing quest (T-Q16+).
        /// </summary>
        public QuestResultDto TryAccept(ulong clientId, string questId, string fromNpcId = "")
        {
            if (string.IsNullOrEmpty(questId))
                return Fail(QuestResultCode.NotFound, "questId empty", questId);
            var def = GetQuest(questId);
            if (def == null)
                return Fail(QuestResultCode.NotFound, $"Quest '{questId}' not found", questId);

            var playerQuests = GetPlayerQuests(clientId);

            // Idempotency: если уже Active/Completed/TurnedIn — OK (success — no-op).
            for (int i = 0; i < playerQuests.Count; i++)
            {
                var existing = playerQuests[i];
                if (existing.questId == questId)
                {
                    if (existing.state == QuestState.Active || existing.state == QuestState.Completed || existing.state == QuestState.TurnedIn)
                    {
                        if (existing.state == QuestState.Active)
                            existing.EnsureObjectiveProgress(def.GetStage(existing.currentStageId));
                        return Ok($"Already {existing.state}", questId);
                    }
                    if (!QuestStateTransition.IsAllowed(existing.state, QuestState.Active))
                        return Fail(QuestResultCode.InvalidState,
                            $"Cannot accept from state {existing.state}", questId);
                    ClearNpcTalkEvents(clientId);
                    existing.state = QuestState.Active;
                    if (string.IsNullOrEmpty(existing.currentStageId))
                    {
                        var entry = def.GetEntryStage();
                        if (entry != null) existing.currentStageId = entry.stageId;
                        // T-Q22 fix: fire onEnterActions of entry stage on accept.
                        if (entry != null && entry.onEnterActions != null && entry.onEnterActions.Length > 0)
                        {
                            OnFireDialogActions?.Invoke(clientId, fromNpcId ?? "", entry.onEnterActions);
                        }
                    }
                    existing.EnsureObjectiveProgress(def.GetStage(existing.currentStageId));
                    return Ok("Accepted", questId);
                }
            }

            // Max active cap (считаем только Active — Discovered/Offered не лимитируем).
            int activeCount = 0;
            for (int i = 0; i < playerQuests.Count; i++)
                if (playerQuests[i].state == QuestState.Active) activeCount++;
            if (activeCount >= MaxActiveQuestsPerPlayer)
                return Fail(QuestResultCode.PrerequisitesNotMet,
                    $"Max active quests reached ({activeCount}/{MaxActiveQuestsPerPlayer})", questId);

            // T-Q28: проверяем prerequisites (prereqQuest) — для пути "новый инстанс" тоже.
            // (Idempotency path выше не трогаем — там квест уже в логе, prereq не применим.)
            var (prereqMet, prereqReason) = ArePrerequisitesMet(clientId, def);
            if (!prereqMet)
            {
                if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryAccept: client={clientId} quest={questId} prereq NOT met: {prereqReason}");
                return Fail(QuestResultCode.PrerequisitesNotMet, prereqReason, questId);
            }

            // A talk that happened before acceptance must not satisfy the first quest stage.
            ClearNpcTalkEvents(clientId);

            // Create new QuestInstance
            var instance = new QuestInstance
            {
                questId = questId,
                state = QuestState.Active,
                isTracked = false
            };
            var entryStage = def.GetEntryStage();
            if (entryStage != null) instance.currentStageId = entryStage.stageId;
            instance.ResetObjectiveProgress(entryStage);
            playerQuests.Add(instance);

            // T-Q22 fix: fire onEnterActions of entry stage on accept.
            if (entryStage != null && entryStage.onEnterActions != null && entryStage.onEnterActions.Length > 0)
            {
                OnFireDialogActions?.Invoke(clientId, fromNpcId ?? "", entryStage.onEnterActions);
            }

            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryAccept: client={clientId} quest={questId} fromNpc={fromNpcId} → Active");
            SavePlayer(clientId); // T-Q18
            return Ok("Accepted", questId);
        }

        /// <summary>
        /// T-Q15: turn in a Completed quest at the right NPC → TurnedIn.
        /// validates: state=Completed, NPC is in npc.GetQuestTurnInIds().
        /// Out of scope T-Q15: applying rewards (T-Q16: ApplyQuestRewards).
        /// </summary>
        public QuestResultDto TryTurnIn(ulong clientId, string questId, string toNpcId)
        {
            if (string.IsNullOrEmpty(questId))
                return Fail(QuestResultCode.NotFound, "questId empty", questId);
            var def = GetQuest(questId);
            if (def == null)
                return Fail(QuestResultCode.NotFound, $"Quest '{questId}' not found", questId);

            // Find player's quest instance
            var playerQuests = GetPlayerQuests(clientId);
            QuestInstance instance = null;
            for (int i = 0; i < playerQuests.Count; i++)
            {
                if (playerQuests[i].questId == questId) { instance = playerQuests[i]; break; }
            }
            if (instance == null)
                return Fail(QuestResultCode.NotFound, $"Quest not in player's log", questId);

            // State: Active → Completed. Turn-in is only valid when the current stage's
            // required objectives are all complete. C1 fix: TryAdvanceStage does NOT verify
            // objectives itself — it fires onCompleteActions and advances. So the objective
            // check MUST happen here, before TryAdvanceStage.
            if (instance.state == QuestState.Active)
            {
                var def2 = GetQuest(questId);
                if (def2 != null && !string.IsNullOrEmpty(instance.currentStageId))
                {
                    var curStage = def2.GetStage(instance.currentStageId);
                    if (curStage != null)
                    {
                        if (!instance.AreAllRequiredComplete(curStage))
                            return Fail(QuestResultCode.InvalidState,
                                $"Quest '{questId}' objectives not complete", questId);
                        TryAdvanceStage(clientId, instance, def2, curStage);
                    }
                }
            }
            if (instance.state != QuestState.Completed)
                return Fail(QuestResultCode.InvalidState,
                    $"Quest must be Completed (current={instance.state})", questId);

            // Validate NPC can turn-in
            if (!string.IsNullOrEmpty(toNpcId))
            {
                // NPC must be registered в questDatabase
                var npc = Database != null ? Database.GetNpc(toNpcId) : null;
                if (npc == null)
                    return Fail(QuestResultCode.NotFound, $"NPC '{toNpcId}' not found", questId);
                // Optional: check npc.GetQuestTurnInIds() contains questId
                var turnInIds = npc.GetQuestTurnInIds();
                if (turnInIds != null && turnInIds.Length > 0)
                {
                    bool found = false;
                    for (int i = 0; i < turnInIds.Length; i++)
                    {
                        if (turnInIds[i] == questId) { found = true; break; }
                    }
                    if (!found)
                        return Fail(QuestResultCode.PrerequisitesNotMet,
                            $"NPC '{toNpcId}' cannot turn-in quest '{questId}'", questId);
                }
            }

            // S3: consume DeliverItem objective items before finalizing turn-in.
            if (!ConsumeDeliverItems(clientId, def))
                return Fail(QuestResultCode.InvalidState,
                    $"Quest '{questId}' turn-in failed — deliver items missing", questId);

            // Transition: Completed → TurnedIn
            if (!QuestStateTransition.IsAllowed(instance.state, QuestState.TurnedIn))
                return Fail(QuestResultCode.InvalidState, $"Cannot turn-in from {instance.state}", questId);
            instance.state = QuestState.TurnedIn;

            // T-Q18: ApplyQuestRewards (real impl) — credits + items + reputation + npcAttitude + flags.
            // def.rewards is a QuestReward (T-Q04).
            if (def.rewards != null)
            {
                ApplyQuestRewards(clientId, def.rewards, toNpcId);
            }

            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryTurnIn: client={clientId} quest={questId} toNpc={toNpcId} → TurnedIn");
            SavePlayer(clientId); // T-Q18
            return Ok("Turned in", questId);
        }

        /// <summary>
        /// T-Q18: apply QuestReward к player. Items → InventoryServer.AddItemDirect (ItemType.Resources, int.Parse tradeItemId).
        /// Cargo items — out of scope T-Q18 (no active ship tracking). Reputation → ModifyReputation. NPC attitude → ModifyNpcAttitude.
        /// Unlocks (DialogTree/Zone) → log only (T-Q19 cleanup will full impl).
        /// </summary>
        private void ApplyQuestRewards(ulong clientId, QuestReward reward, string turnInNpcId)
        {
            if (reward == null) return;

            // 1) Credits: repository SetCredits.
            if (reward.credits != 0)
            {
                var repo = ProjectC.Trade.Core.TradeWorld.Instance != null ? ProjectC.Trade.Core.TradeWorld.Instance.Repository : null;
                if (repo != null)
                {
                    float currentCredits = repo.GetCredits(clientId);
                    float newCredits = Mathf.Max(0f, currentCredits + reward.credits);
                    repo.SetCredits(clientId, newCredits);
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] ApplyQuestRewards: credits {currentCredits:F0} → {newCredits:F0} (+{reward.credits})");
                }
                else
                {
                    Debug.LogWarning("[QuestWorld] ApplyQuestRewards: TradeWorld.Repository == null, credits skipped");
                }
            }

            // 2) Items: AddItemDirect via InventoryWorld.
            if (reward.items != null)
            {
                for (int i = 0; i < reward.items.Length; i++)
                {
                    var ri = reward.items[i];
                    if (ri == null || ri.count <= 0) continue;

                    // T-QREWARD: resolve item id — pickupItem ref first, then string tradeItemId fallback.
                    int itemId = 0;
                    if (ri.pickupItem != null)
                    {
                        itemId = ResolveItemId(null, ri.pickupItem);
                    }
                    if (itemId <= 0 && !string.IsNullOrEmpty(ri.tradeItemId))
                    {
                        itemId = ResolveItemId(ri.tradeItemId);
                    }
                    if (itemId <= 0)
                    {
                        Debug.LogWarning($"[QuestWorld] ApplyQuestRewards: items[{i}] не удалось разрешить item (pickupItem={ri.pickupItem?.name} tradeItemId='{ri.tradeItemId}')");
                        continue;
                    }

                    var inv = ProjectC.Items.InventoryWorld.Instance;
                    if (inv == null)
                    {
                        Debug.LogWarning($"[QuestWorld] ApplyQuestRewards: InventoryWorld == null, items[{i}] skipped");
                        break;
                    }
                    // S1 fix: honour ri.count. AddItemDirect adds ONE item per call → loop.
                    // Resolve the actual definition too, so a legacy string-only Key reward
                    // cannot silently fall back to Resources and lose its unique instance.
                    var rewardDefinition = inv.GetItemDefinition(itemId);
                    var rewardItemType = rewardDefinition != null
                        ? rewardDefinition.itemType
                        : (ri.pickupItem != null ? ri.pickupItem.itemType : ProjectC.Items.ItemType.Resources);
                    for (int n = 0; n < ri.count; n++)
                    {
                        var result = rewardItemType == ProjectC.Items.ItemType.Key
                            ? inv.AddKeyItemDirect(clientId, itemId)
                            : inv.AddItemDirect(clientId, itemId, rewardItemType);
                        if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] ApplyQuestRewards: items[{i}] id={itemId} type={rewardItemType} x{ri.count} ({n + 1}/{ri.count}) → code={result.code} message={result.message}");
                    }
                }
            }

            // 3) Cargo items: out of scope T-Q18 (need active ship tracking, T-Q17+ or T-Q22).
            if (reward.cargoItems != null && reward.cargoItems.Length > 0)
            {
                Debug.LogWarning($"[QuestWorld] ApplyQuestRewards: cargoItems ({reward.cargoItems.Length}) skipped — T-Q18 out of scope (active ship tracking TBD)");
            }

            // 4) Reputation deltas.
            if (reward.reputation != null)
            {
                for (int i = 0; i < reward.reputation.Length; i++)
                {
                    var rr = reward.reputation[i];
                    if (rr == null || rr.EffectiveFactionWireId <= 0) continue;
                    int newVal = ModifyReputation(clientId, rr.EffectiveFactionWireId, rr.value, silent: true);
                    if (Debug.isDebugBuild)
                    {
                        string label = rr.factionRef != null ? rr.factionRef.EffectiveFactionKey : rr.faction.ToString();
                        Debug.Log($"[QuestWorld] ApplyQuestRewards: reputation faction={label} wireId={rr.EffectiveFactionWireId} delta={rr.value} → {newVal}");
                    }
                }
            }

            // 5) Unlocks: log only. Real impl в T-Q19 (dialog tree unlock, zone unlock, etc).
            if (reward.unlocks != null && reward.unlocks.Length > 0)
            {
                for (int i = 0; i < reward.unlocks.Length; i++)
                {
                    var ul = reward.unlocks[i];
                    if (ul == null) continue;
                    Debug.Log($"[QuestWorld] ApplyQuestRewards: unlock type={ul.unlockType} id='{ul.unlockId}' (T-Q19+ impl)");
                }
            }
        }

        /// <summary>
        /// S3: consume DeliverItem objective items on turn-in. Removes requiredQuantity of each
        /// DeliverItem's item from the player inventory (via InventoryServer.TryRemove).
        /// Returns false WITHOUT consuming further if any removal fails, so turn-in can be rejected cleanly.
        /// </summary>
        private bool ConsumeDeliverItems(ulong clientId, QuestDefinition def)
        {
            if (def == null || def.stages == null) return true; // nothing to consume
            for (int s = 0; s < def.stages.Length; s++)
            {
                var stage = def.stages[s];
                if (stage == null || stage.objectives == null) continue;
                for (int o = 0; o < stage.objectives.Length; o++)
                {
                    var obj = stage.objectives[o];
                    if (obj == null || obj.objectiveType != QuestObjectiveType.DeliverItem) continue;

                    int itemId = ResolveItemId(obj.itemTradeItemId, obj.pickupItem);
                    if (itemId <= 0)
                    {
                        Debug.LogWarning($"[QuestWorld] ConsumeDeliverItems: quest={def.questId} obj={obj.objectiveId} — не удалось разрешить item, пропускаю");
                        continue;
                    }

                    int need = obj.requiredQuantity > 0 ? obj.requiredQuantity : 1;
                    // ItemData ref несёт точный itemType; строковый tradeItemId → Resources (как в reward-пайплайне).
                    ProjectC.Items.ItemType itemType =
                        obj.pickupItem != null ? obj.pickupItem.itemType : ProjectC.Items.ItemType.Resources;

                    bool removed;
                    var invServer = ProjectC.Items.Network.InventoryServer.Instance;
                    if (invServer != null)
                    {
                        removed = invServer.TryRemove(clientId, itemId, itemType, need);
                    }
                    else
                    {
                        var inv = ProjectC.Items.InventoryWorld.Instance;
                        removed = inv != null && inv.RemoveItems(clientId, itemId, itemType, need).IsSuccess;
                    }

                    if (!removed)
                    {
                        Debug.LogWarning($"[QuestWorld] ConsumeDeliverItems: quest={def.questId} obj={obj.objectiveId} — не удалось изъять {need}x item={itemId} ({itemType})");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// T-Q15: toggle isTracked on player's quest instance. Snapshot rebuild reflects new value.
        /// </summary>
        public QuestResultDto SetTracked(ulong clientId, string questId, bool track)
        {
            if (string.IsNullOrEmpty(questId))
                return Fail(QuestResultCode.NotFound, "questId empty", questId);
            var playerQuests = GetPlayerQuests(clientId);
            for (int i = 0; i < playerQuests.Count; i++)
            {
                if (playerQuests[i].questId == questId)
                {
                    playerQuests[i].isTracked = track;
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] SetTracked: client={clientId} quest={questId} tracked={track}");
                    SavePlayer(clientId); // T-Q18
                    return Ok(track ? "Tracking" : "Untracked", questId);
                }
            }
            return Fail(QuestResultCode.NotFound, $"Quest not in player's log", questId);
        }

        /// <summary>
        /// C8: transition a quest to Failed. Allowed from Discovered/Offered/Active
        /// (Failed/TurnedIn are terminal). Used by DialogueAction.FailQuest.
        /// </summary>
        public QuestResultDto TryFailQuest(ulong clientId, string questId, string reason)
        {
            if (string.IsNullOrEmpty(questId))
                return Fail(QuestResultCode.NotFound, "questId empty", questId);
            var playerQuests = GetPlayerQuests(clientId);
            for (int i = 0; i < playerQuests.Count; i++)
            {
                if (playerQuests[i].questId == questId)
                {
                    var inst = playerQuests[i];
                    if (!QuestStateTransition.IsAllowed(inst.state, QuestState.Failed))
                        return Fail(QuestResultCode.InvalidState, $"Cannot fail from state {inst.state}", questId);
                    inst.state = QuestState.Failed;
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] TryFailQuest: client={clientId} quest={questId} reason='{reason}' → Failed");
                    SavePlayer(clientId); // T-Q18
                    return Ok(string.IsNullOrEmpty(reason) ? "Failed" : $"Failed: {reason}", questId);
                }
            }
            return Fail(QuestResultCode.NotFound, $"Quest not in player's log", questId);
        }

        // ============ T-Q15: Result helpers (alias for QuestWorld → avoid duplication) ============

        private static QuestResultDto Ok(string message, string questId)
        {
            return new QuestResultDto
            {
                code = (byte)QuestResultCode.Ok,
                questId = questId,
                message = message
            };
        }
        private static QuestResultDto Fail(QuestResultCode code, string message, string questId)
        {
            return new QuestResultDto
            {
                code = (byte)code,
                questId = questId,
                message = message
            };
        }

        public bool GetFlag(ulong clientId, string flagId)
        {
            return _worldFlags.TryGetValue((clientId, flagId), out var v) && v;
        }

        public DialogSession GetDialogSession(ulong clientId)
        {
            return _dialogByPlayer.TryGetValue(clientId, out var s) ? s : null;
        }

        /// <summary>T-Q10: open new dialog session. Returns null если session уже open.</summary>
        public DialogSession OpenDialog(ulong clientId, string npcId, Dialogue.DialogTree tree)
        {
            if (tree == null) return null;
            if (_dialogByPlayer.ContainsKey(clientId)) return null; // already in dialog
            var session = new DialogSession
            {
                treeId = tree.treeId,
                tree = tree,
                currentNodeId = string.IsNullOrEmpty(tree.rootNodeId) ? "" : tree.rootNodeId,
                npcId = npcId ?? "",
                startedAtUnix = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            _dialogByPlayer[clientId] = session;
            return session;
        }

        /// <summary>T-Q10: close dialog session. No-op if not open.</summary>
        public void CloseDialog(ulong clientId)
        {
            _dialogByPlayer.Remove(clientId);
        }

        // ============ T-Q06: NPC talk + Custom event tracking ============

        /// <summary>Per-player set of NPC ids the player has talked to at least once.</summary>
        private readonly Dictionary<ulong, HashSet<string>> _npcTalkedTo = new Dictionary<ulong, HashSet<string>>();

        /// <summary>
        /// Transient per-tick talk events used by TalkToNpc objectives.
        /// This is intentionally separate from _npcTalkedTo: historical knowledge must not
        /// complete a later TalkToNpc stage automatically.
        /// </summary>
        private readonly Dictionary<ulong, HashSet<string>> _npcTalkEvents = new Dictionary<ulong, HashSet<string>>();

        // ============ T-KNOW: Knowledge System ============

        /// <summary>T-KNOW: какие фракции игрок «знает» (key: stable wireId).</summary>
        private readonly Dictionary<ulong, HashSet<int>> _knownFactions = new();

        /// <summary>T-KNOW: какие NPC игрок «знает». Отдельно от _npcTalkedTo — в будущем
        /// знание может открываться через книги/квесты, а не только через диалог.</summary>
        private readonly Dictionary<ulong, HashSet<string>> _knownNpcs = new();

        /// <summary>Per-player set of custom event ids that have occurred (DialogueAction.EmitEvent).</summary>
        private readonly Dictionary<ulong, HashSet<string>> _eventsOccurred = new Dictionary<ulong, HashSet<string>>();

        // T-Q15: contract tracking (для ContractCompletedTrigger / ContractAcceptedTrigger).
        private readonly Dictionary<ulong, HashSet<string>> _contractsCompleted = new Dictionary<ulong, HashSet<string>>();
        private readonly Dictionary<ulong, HashSet<string>> _contractsAccepted = new Dictionary<ulong, HashSet<string>>();

        public bool HasContractCompleted(ulong clientId, string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return false;
            return _contractsCompleted.TryGetValue(clientId, out var set) && set.Contains(contractId);
        }

        public void MarkContractCompleted(ulong clientId, string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return;
            if (!_contractsCompleted.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _contractsCompleted[clientId] = set;
            }
            if (set.Add(contractId)) SavePlayer(clientId); // T-Q18
        }

        public bool HasContractAccepted(ulong clientId, string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return false;
            return _contractsAccepted.TryGetValue(clientId, out var set) && set.Contains(contractId);
        }

        public void MarkContractAccepted(ulong clientId, string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return;
            if (!_contractsAccepted.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _contractsAccepted[clientId] = set;
            }
            if (set.Add(contractId)) SavePlayer(clientId); // T-Q18
        }

        public bool HasNpcTalkedTo(ulong clientId, string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return false;
            return _npcTalkedTo.TryGetValue(clientId, out var set) && set.Contains(npcId);
        }

        /// <summary>
        /// Returns true only for a talk interaction received since the last quest tick.
        /// It is not persisted and must not be used for NPC knowledge.
        /// </summary>
        public bool HasNpcTalkEvent(ulong clientId, string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return false;
            return _npcTalkEvents.TryGetValue(clientId, out var set) && set.Contains(npcId);
        }

        /// <summary>Removes transient talk events after the current tick or quest reset.</summary>
        public void ClearNpcTalkEvents(ulong clientId)
        {
            _npcTalkEvents.Remove(clientId);
        }

        public void MarkNpcTalked(ulong clientId, string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return;

            if (!_npcTalkEvents.TryGetValue(clientId, out var talkEvents))
            {
                talkEvents = new HashSet<string>();
                _npcTalkEvents[clientId] = talkEvents;
            }
            talkEvents.Add(npcId);

            if (!_npcTalkedTo.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _npcTalkedTo[clientId] = set;
            }
            bool needSave = set.Add(npcId); // T-Q18

            // T-KNOW: unlock NPC knowledge (always — даже если уже был в _npcTalkedTo,
            // чтобы наверняка знание было; idempotent через HashSet)
            UnlockNpcKnowledge(clientId, npcId);

            // T-KNOW: unlock faction knowledge via npcDefinition.faction
            if (Database != null && !string.IsNullOrEmpty(npcId))
            {
                var npcDef = Database.GetNpc(npcId);
                if (npcDef != null && npcDef.EffectiveFactionWireId > 0)
                {
                    UnlockFactionKnowledge(clientId, npcDef.EffectiveFactionWireId);
                }
            }

            if (needSave) SavePlayer(clientId);
        }

        // ============ T-KNOW: Knowledge ============

        public bool IsFactionKnown(ulong clientId, FactionId faction)
            => IsFactionKnown(clientId, (int)faction);

        public bool IsFactionKnown(ulong clientId, FactionDefinition faction)
            => IsFactionKnown(clientId, faction != null ? faction.EffectiveWireId : 0);

        public bool IsFactionKnown(ulong clientId, int wireId)
        {
            if (wireId <= 0) return true; // None is always "known" (not filtered)
            if (wireId > byte.MaxValue) return false;
            return _knownFactions.TryGetValue(clientId, out var set) && set.Contains(wireId);
        }

        public bool IsNpcKnown(ulong clientId, string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return false;
            return _knownNpcs.TryGetValue(clientId, out var set) && set.Contains(npcId);
        }

        public void UnlockFactionKnowledge(ulong clientId, FactionId faction)
            => UnlockFactionKnowledge(clientId, (int)faction);

        public void UnlockFactionKnowledge(ulong clientId, FactionDefinition faction)
            => UnlockFactionKnowledge(clientId, faction != null ? faction.EffectiveWireId : 0);

        public void UnlockFactionKnowledge(ulong clientId, int wireId)
        {
            if (wireId <= 0 || wireId > byte.MaxValue) return;
            if (!_knownFactions.TryGetValue(clientId, out var set))
            {
                set = new HashSet<int>();
                _knownFactions[clientId] = set;
            }
            if (set.Add(wireId))
            {
                if (Debug.isDebugBuild)
                {
                    var definition = GetFactionCatalog().GetByWireId(wireId);
                    string label = definition != null ? definition.EffectiveFactionKey : wireId.ToString();
                    Debug.Log($"[QuestWorld] Knowledge unlocked: player={clientId} faction={label} wireId={wireId}");
                }
                // NOT calling SavePlayer here — caller (MarkNpcTalked) already does
            }
        }

        public void UnlockNpcKnowledge(ulong clientId, string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return;
            if (!_knownNpcs.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _knownNpcs[clientId] = set;
            }
            set.Add(npcId);
            // NOT calling SavePlayer — caller already does
        }

        public bool HasEventOccurred(ulong clientId, string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return false;
            return _eventsOccurred.TryGetValue(clientId, out var set) && set.Contains(eventId);
        }

        public void MarkEventOccurred(ulong clientId, string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            if (!_eventsOccurred.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _eventsOccurred[clientId] = set;
            }
            if (set.Add(eventId)) SavePlayer(clientId); // T-Q18
        }

        // ============ T-Q06: Quest advancement (minimal) ============

        // ============ T-Q20: Real-time tick + stage transitions ============

        /// <summary>T-Q20: tick interval (seconds). QuestServer.Update accumulates Time.deltaTime.</summary>
        public float TickInterval = 5f;

        /// <summary>T-Q20: accumulator used by QuestServer-driven ticks.</summary>
        public float TickAccumulator = 0f;

        /// <summary>T-Q20: QuestWorld raises this when stage actions must fire (e.g. onCompleteActions,
        /// onEnterActions). QuestServer subscribes and routes to FireDialogAction.
        /// Args: (clientId, npcIdHint, actions[]). npcIdHint может быть пуст (stage transition не привязан к NPC).</summary>
        public event System.Action<ulong, string, ProjectC.Dialogue.DialogueAction[]> OnFireDialogActions;

        /// <summary>T-Q20: QuestWorld raises this когда stage transitioned. QuestServer
        /// подписывается → SendQuestSnapshotToClient + SavePlayer.
        /// Args: (clientId, questId, fromStageId, toStageId).</summary>
        public event System.Action<ulong, string, string, string> OnStageTransition;

        /// <summary>T-Q20: position lookup. Server подменяет этот делегат на NetworkPlayer.transform.position lookup.
        /// Args: clientId, returns Vector3 или Vector3.zero если не найден.</summary>
        public System.Func<ulong, Vector3> PlayerPositionProvider;

        /// <summary>T-Q20: resolve item id from objective.itemTradeItemId or direct ItemData ref. Tries:
        /// 0) Direct ItemData ref (pickupItem) — T-QREWARD: priority over string id.
        /// 1) int.TryParse (direct id) — for legacy configs.
        /// 2) T-Q26: ItemRegistry.TryGetIdByName (single source of truth) — preferred.
        /// 3) Resources/Items/ scan — fallback if ItemRegistry not loaded.
        /// Returns 0 если ничего не найдено.
        /// </summary>
        public static int ResolveItemId(string itemTradeItemId, ProjectC.Items.ItemData pickupItem = null)
        {
            // 0. Direct ItemData ref (T-QREWARD: drag-and-drop, приоритет).
            if (pickupItem != null)
            {
                var inv = ProjectC.Items.InventoryWorld.Instance;
                if (inv != null)
                {
                    int id = inv.GetOrRegisterItemId(pickupItem);
                    if (id > 0) return id;
                }
                // Fallback: если InventoryWorld нет — ищем в Resources/Items/ по имени
                var all = Resources.LoadAll<ProjectC.Items.ItemData>("Items");
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                        if (all[i] == pickupItem) return i + 1;
                }
            }

            if (string.IsNullOrEmpty(itemTradeItemId)) return 0;
            // 1. Direct int.
            if (int.TryParse(itemTradeItemId, out int direct) && direct > 0) return direct;
            // 2. T-Q26: ItemRegistry lookup (preferred — single source of truth).
            var registry = ProjectC.Items.ItemRegistry.Instance;
            if (registry == null)
            {
                // Runtime registry lives under Resources/Items/Data/.
                registry = Resources.Load<ProjectC.Items.ItemRegistry>("ItemRegistry");
                if (registry == null)
                    registry = Resources.Load<ProjectC.Items.ItemRegistry>("Items/Data/ItemRegistry");
                if (registry != null) ProjectC.Items.ItemRegistry.SetInstance(registry);
            }
            if (registry != null && registry.TryGetIdByName(itemTradeItemId, out int regId) && regId > 0)
                return regId;
            // 3. Fallback: Resources/Items/ scan (for tests without ItemRegistry).
            var allItems = Resources.LoadAll<ProjectC.Items.ItemData>("Items");
            if (allItems == null) return 0;
            for (int i = 0; i < allItems.Length; i++)
            {
                if (allItems[i] == null) continue;
                if (allItems[i].itemName == itemTradeItemId) return i + 1;
            }
            return 0;
        }

        /// <summary>
        /// T-Q20: server-driven tick. Accumulate Time.deltaTime, evaluate all active
        /// quests for all players when interval reached.
        /// </summary>
        public void TickAll(float dt)
        {
            if (dt < 0f) return;
            TickAccumulator += dt;
            if (TickAccumulator < TickInterval) return;
            TickAccumulator = 0f;

            // Snapshot keys (нельзя менять dictionary во время итерации)
            if (_questsByPlayer == null || _questsByPlayer.Count == 0) return;
            var playerIds = new List<ulong>(_questsByPlayer.Keys);
            for (int i = 0; i < playerIds.Count; i++)
            {
                TickPlayer(playerIds[i]);
            }
        }

        /// <summary>
        /// T-Q20: tick one player. Walks all their Active quests, evaluates current stage.
        /// </summary>
        public void TickPlayer(ulong clientId)
        {
            if (!_questsByPlayer.TryGetValue(clientId, out var quests) || quests == null)
            {
                ClearNpcTalkEvents(clientId);
                return;
            }

            // Snapshot quest list (TryAdvanceStage может менять state, но не add/remove)
            for (int i = 0; i < quests.Count; i++)
            {
                var inst = quests[i];
                if (inst == null) continue;
                if (inst.state != QuestState.Active) continue;
                if (string.IsNullOrEmpty(inst.currentStageId)) continue;

                var def = GetQuest(inst.questId);
                if (def == null) continue;

                var stage = def.GetStage(inst.currentStageId);
                if (stage == null) continue;

                // Repairs old saves and guarantees the snapshot has the active stage objective.
                inst.EnsureObjectiveProgress(stage);
                EvaluateAndAdvanceStage(clientId, inst, def, stage);
            }

            // TalkToNpc is an interaction event, not an "ever talked" flag.
            // Keep it available for all active quests in this tick, then consume it globally.
            ClearNpcTalkEvents(clientId);
        }

        /// <summary>
        /// T-Q20: evaluate all required objectives в current stage. Если все satisfied
        /// → TryAdvanceStage. Returns true если что-то изменилось.
        /// </summary>
        private bool EvaluateAndAdvanceStage(ulong clientId, QuestInstance instance, QuestDefinition def, QuestStage stage)
        {
            if (stage.objectives == null || stage.objectives.Length == 0)
            {
                // No objectives → auto-advance immediately (degenerate stage)
                return TryAdvanceStage(clientId, instance, def, stage);
            }

            bool changed = false;
            for (int i = 0; i < stage.objectives.Length; i++)
            {
                var obj = stage.objectives[i];
                if (obj == null) continue;
                if (!obj.required || obj.optional) continue;

                var progress = instance.GetOrCreateProgress(obj.objectiveId);
                int prevCount = progress.currentCount;
                bool prevCompleted = progress.completed;
                if (progress.completed) continue;

                if (EvaluateObjective(clientId, instance, obj, progress))
                {
                    progress.completed = true;
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] Objective completed: quest={def.questId} stage={stage.stageId} obj={obj.objectiveId}");
                    changed = true;
                }
                else if (progress.currentCount != prevCount || progress.completed != prevCompleted)
                {
                    // T-Q21: progress изменился (e.g. picked up item, qty 0→1) — push snapshot чтобы UI обновил (1/3).
                    changed = true;
                }
            }

            // If all required objectives now complete → advance.
            if (instance.AreAllRequiredComplete(stage))
            {
                if (TryAdvanceStage(clientId, instance, def, stage))
                {
                    changed = true;
                }
            }

            if (changed) {
                SavePlayer(clientId);
                // T-Q21: push snapshot на client чтобы UI обновил счётчик objective (1/3, 2/3 ...).
                if (QuestServer.Instance != null) {
                    var method = typeof(QuestServer).GetMethod("SendQuestSnapshotToClient",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                        null, new System.Type[] { typeof(ulong) }, null);
                    if (method != null) method.Invoke(QuestServer.Instance, new object[] { clientId });
                }
            }
            return changed;
        }

        /// <summary>
        /// T-Q20: evaluate single objective (event-driven + tick-friendly hybrid).
        /// Updates progress.currentCount (for HaveItem, ReputationAtLeast).
        /// </summary>
        private bool EvaluateObjective(ulong clientId, QuestInstance instance, QuestObjective obj, QuestInstance.ObjectiveProgress progress)
        {
            if (obj == null) return false;
            switch (obj.objectiveType)
            {
                case QuestObjectiveType.TalkToNpc:
                {
                    string npcId = obj.targetNpc != null ? obj.targetNpc.npcId : obj.targetNpcId;
                    return HasNpcTalkEvent(clientId, npcId);
                }

                case QuestObjectiveType.HaveItem:
                {
                    int itemId = ResolveItemId(obj.itemTradeItemId, obj.pickupItem);
                    if (itemId <= 0) return false;
                    int count = ProjectC.Items.InventoryWorld.Instance != null
                        ? ProjectC.Items.InventoryWorld.Instance.CountOf(clientId, itemId)
                        : 0;
                    progress.currentCount = count;
                    return count >= obj.requiredQuantity;
                }

                case QuestObjectiveType.DeliverItem:
                {
                    // Same as HaveItem for MVP (turn-in handled в TryTurnIn).
                    int itemId = ResolveItemId(obj.itemTradeItemId, obj.pickupItem);
                    if (itemId <= 0) return false;
                    int count = ProjectC.Items.InventoryWorld.Instance != null
                        ? ProjectC.Items.InventoryWorld.Instance.CountOf(clientId, itemId)
                        : 0;
                    progress.currentCount = count;
                    return count >= obj.requiredQuantity;
                }

                case QuestObjectiveType.ReachLocation:
                {
                    if (PlayerPositionProvider == null) return false;
                    var playerPos = PlayerPositionProvider(clientId);
                    if (playerPos == Vector3.zero) return false; // not on a real map
                    float dist = Vector3.Distance(playerPos, obj.targetPosition);
                    progress.currentCount = obj.targetRadius > 0f ? (int)(dist * 100f / obj.targetRadius) : 0;
                    return dist <= obj.targetRadius;
                }

                case QuestObjectiveType.ReputationAtLeast:
                {
                    int val = GetReputation(clientId, obj.EffectiveFactionWireId);
                    progress.currentCount = val;
                    return val >= obj.reputationValue;
                }

                case QuestObjectiveType.WaitForEvent:
                case QuestObjectiveType.EventDriven:
                    return HasEventOccurred(clientId, obj.eventId);

                case QuestObjectiveType.KillEntity:
                {
                    string entityId = obj.targetEntity != null ? obj.targetEntity.npcId : obj.targetEntityType;
                    // STUB: combat system не реализован. Always false.
                    if (Debug.isDebugBuild && Time.frameCount % 600 == 0)
                        Debug.Log($"[QuestWorld] Objective {obj.objectiveType} ({obj.objectiveId}) entity={entityId} is STUB — always unsatisfied");
                    return false;
                }

                default:
                    return false;
            }
        }

        /// <summary>
        /// T-Q20 + T-Q22: advance current stage to next. Fire onCompleteActions →
        /// transition currentStageId → nextStageId (or Completed) → fire onEnterActions.
        /// </summary>
        /// <returns>True if a transition happened.</returns>
        private bool TryAdvanceStage(ulong clientId, QuestInstance instance, QuestDefinition def, QuestStage currentStage)
        {
            if (instance == null || def == null || currentStage == null) return false;

            string fromStage = currentStage.stageId;
            string toStage = currentStage.nextStageId;

            // 1. Fire onCompleteActions of CURRENT stage (before transition).
            // C2 fix: награды выдаются ТОЛЬКО в TryTurnIn (единая точка). Здесь НЕ применяем
            // def.rewards — иначе финальный stage с onCompleteActions давал двойную выдачу
            // (награда применялась здесь и повторно в TryTurnIn).
            if (currentStage.onCompleteActions != null && currentStage.onCompleteActions.Length > 0)
            {
                OnFireDialogActions?.Invoke(clientId, "", currentStage.onCompleteActions);
            }

            // 2. Transition.
            if (string.IsNullOrEmpty(toStage))
            {
                // Final stage: Active → Completed
                if (QuestStateTransition.IsAllowed(instance.state, QuestState.Completed))
                {
                    instance.state = QuestState.Completed;
                    instance.completedAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] State transitioned: {def.questId} {fromStage} → Completed");
                }
                OnStageTransition?.Invoke(clientId, def.questId, fromStage, "");
                return true;
            }
            else
            {
                instance.currentStageId = toStage;
                var nextStage = def.GetStage(toStage);
                instance.ResetObjectiveProgress(nextStage);
                if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] Stage advanced: {def.questId} {fromStage} → {toStage}");
                OnStageTransition?.Invoke(clientId, def.questId, fromStage, toStage);

                // 3. Fire onEnterActions of NEW stage.
                if (nextStage != null && nextStage.onEnterActions != null && nextStage.onEnterActions.Length > 0)
                {
                    OnFireDialogActions?.Invoke(clientId, "", nextStage.onEnterActions);
                }
                return true;
            }
        }

        // ============ T-Q06: TryAdvanceObjective (kept) ============

        /// <summary>
        /// T-Q06 scope: minimal TryAdvanceObjective. Mark objective как completed если
        /// есть matching QuestInstance.Active quest с этим objective. Полная логика
        /// (TryOffer, TryAccept, TryTurnIn, state transitions, onCompleteActions) — T-Q15+.
        /// </summary>
        /// <returns>True если objective был marked completed.</returns>
        public bool TryAdvanceObjective(ulong clientId, string questId, string objectiveId)
        {
            if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(objectiveId)) return false;
            var questDef = GetQuest(questId);
            if (questDef == null) return false;

            var playerQuests = GetPlayerQuests(clientId);
            QuestInstance instance = null;
            for (int i = 0; i < playerQuests.Count; i++)
            {
                if (playerQuests[i].questId == questId)
                {
                    instance = playerQuests[i];
                    break;
                }
            }
            if (instance == null) return false;
            if (instance.state != QuestState.Active) return false;
            if (instance.currentStageId != questDef.GetEntryStage()?.stageId
                && !string.IsNullOrEmpty(instance.currentStageId))
            {
                // Try matching against current stage
            }

            // Find stage containing this objective
            QuestStage stage = null;
            for (int i = 0; i < questDef.stages.Length; i++)
            {
                for (int j = 0; j < questDef.stages[i].objectives.Length; j++)
                {
                    if (questDef.stages[i].objectives[j].objectiveId == objectiveId)
                    {
                        stage = questDef.stages[i];
                        break;
                    }
                }
                if (stage != null) break;
            }
            if (stage == null) return false;
            if (stage.stageId != instance.currentStageId) return false;

            // Mark progress
            var progress = instance.GetOrCreateProgress(objectiveId);
            if (progress.completed) return false;
            progress.completed = true;
            return true;
        }

        // ============ T-Q18: Persistence ============

        /// <summary>
        /// T-Q18: build complete QuestSaveData для clientId. Aggregates quests +
        /// reputation + npcAttitude + sets (events/contracts/npcTalkedTo/worldFlags).
        /// Returns null if no data (не должен — всегда хотя бы empty lists).
        /// </summary>
        public ProjectC.Quests.Persistence.QuestSaveData BuildSaveData(ulong clientId)
        {
            var data = new ProjectC.Quests.Persistence.QuestSaveData();
            data.version = 1;

            // Quests
            if (_questsByPlayer.TryGetValue(clientId, out var quests))
            {
                for (int i = 0; i < quests.Count; i++)
                {
                    var q = quests[i];
                    var entry = new ProjectC.Quests.Persistence.QuestSaveEntry
                    {
                        questId = q.questId,
                        state = (byte)q.state,
                        currentStageId = q.currentStageId ?? "",
                        acceptedAtUnix = q.acceptedAtUnix,
                        completedAtUnix = q.completedAtUnix,
                        isTracked = q.isTracked
                    };
                    if (q.objectiveProgress != null)
                    {
                        for (int j = 0; j < q.objectiveProgress.Count; j++)
                        {
                            var op = q.objectiveProgress[j];
                            entry.objectiveProgress.Add(new ProjectC.Quests.Persistence.ObjectiveSaveEntry
                            {
                                objectiveId = op.objectiveId,
                                currentCount = op.currentCount,
                                completed = op.completed
                            });
                        }
                    }
                    data.quests.Add(entry);
                }
            }

            // Reputation: scan all keys with this clientId
            foreach (var kv in _reputation)
            {
                if (kv.Key.Item1 != clientId) continue;
                data.reputation.Add(new ProjectC.Quests.Persistence.FactionRepSaveEntry
                {
                    factionId = (int)kv.Key.Item2,
                    value = kv.Value
                });
            }

            // NPC attitude
            foreach (var kv in _npcAttitude)
            {
                if (kv.Key.Item1 != clientId) continue;
                data.npcAttitude.Add(new ProjectC.Quests.Persistence.NpcAttitudeSaveEntry
                {
                    npcId = kv.Key.Item2,
                    value = kv.Value
                });
            }

            // String sets
            AddStringSetIfPresent(data, clientId, "eventsOccurred", _eventsOccurred);
            AddStringSetIfPresent(data, clientId, "contractsCompleted", _contractsCompleted);
            AddStringSetIfPresent(data, clientId, "contractsAccepted", _contractsAccepted);
            AddStringSetIfPresent(data, clientId, "npcTalkedTo", _npcTalkedTo);

            // worldFlags: Dictionary<(ulong, string), bool> → key.Item1=clientId, key.Item2=flagName, value=bool
            // Only persist true flags (false = default).
            var flagsEntry = new ProjectC.Quests.Persistence.StringSetSaveEntry { setName = "worldFlags" };
            foreach (var kv in _worldFlags)
            {
                if (kv.Key.Item1 != clientId) continue;
                if (kv.Value) flagsEntry.values.Add(kv.Key.Item2);
            }
            if (flagsEntry.values.Count > 0) data.stringSets.Add(flagsEntry);

            // T-KNOW: known factions
            if (_knownFactions.TryGetValue(clientId, out var knownFactionsSet) && knownFactionsSet.Count > 0)
            {
                foreach (var fid in knownFactionsSet)
                    data.knownFactions.Add((int)fid);
            }

            // T-KNOW: known NPCs
            if (_knownNpcs.TryGetValue(clientId, out var knownNpcsSet) && knownNpcsSet.Count > 0)
            {
                data.knownNpcs.AddRange(knownNpcsSet);
            }

            // T-KNOW: Neutral is resolved through the faction registry, not a numeric literal.
            int neutralWireId = GetNeutralWireId();
            if (neutralWireId > 0 && !data.knownFactions.Contains(neutralWireId))
                data.knownFactions.Add(neutralWireId);

            // T-KNOWLEDGE-V2: known recipes (from CraftingWorld)
            var knownRecipeIds = Crafting.CraftingWorld.GetKnownRecipeIds(clientId);
            if (knownRecipeIds != null && knownRecipeIds.Count > 0)
            {
                foreach (var rid in knownRecipeIds)
                    data.knownRecipes.Add(rid);
            }

            // T-MERCH01: сохраняем дробные остатки репутации от торговли
            foreach (var kv in _npcTradeRepRemainder)
            {
                if (kv.Key.Item1 != clientId || kv.Value <= 1e-5f) continue;
                data.npcTradeRepRemainders.Add(new ProjectC.Quests.Persistence.NpcTradeRepSaveEntry
                {
                    npcId = kv.Key.Item2,
                    remainder = kv.Value
                });
            }
            foreach (var kv in _factionTradeRepRemainder)
            {
                if (kv.Key.Item1 != clientId || kv.Value <= 1e-5f) continue;
                data.factionTradeRepRemainders.Add(new ProjectC.Quests.Persistence.FactionTradeRepSaveEntry
                {
                    factionId = kv.Key.Item2,
                    remainder = kv.Value
                });
            }

            // T-SMITH01: сохраняем улучшенные у кузнеца предметы игрока
            foreach (var kv in _upgradedItemsByPlayerItem)
            {
                if (kv.Key.Item1 != clientId || kv.Value == null) continue;
                data.upgradedInventoryItems.Add(kv.Value);
            }

            return data;
        }

        private static void AddStringSetIfPresent(ProjectC.Quests.Persistence.QuestSaveData data, ulong clientId, string setName,
            Dictionary<ulong, HashSet<string>> source)
        {
            if (!source.TryGetValue(clientId, out var set) || set == null || set.Count == 0) return;
            var entry = new ProjectC.Quests.Persistence.StringSetSaveEntry { setName = setName };
            entry.values.AddRange(set);
            data.stringSets.Add(entry);
        }

        /// <summary>T-Q18: save player state via Repository. No-op если Repository == null.</summary>
        public void SavePlayer(ulong clientId)
        {
            if (Repository == null) return;
            var data = BuildSaveData(clientId);
            Repository.Save(clientId, data);
        }

        /// <summary>T-Q18: load player state from Repository → populate dictionaries. Returns true если loaded.</summary>
        public bool LoadPlayer(ulong clientId)
        {
            if (Repository == null) return false;
            var data = Repository.Load(clientId);
            if (data == null) return false;

            // Wipe existing state for this client first (avoid stale data from prior in-memory session)
            _questsByPlayer.Remove(clientId);
            // Don't wipe reputation/attitude — caller decides if they want fresh start.
            // For simplicity, also wipe:
            var repToRemove = new List<(ulong, int)>();
            foreach (var kv in _reputation) if (kv.Key.Item1 == clientId) repToRemove.Add(kv.Key);
            foreach (var k in repToRemove) _reputation.Remove(k);
            var attToRemove = new List<(ulong, string)>();
            foreach (var kv in _npcAttitude) if (kv.Key.Item1 == clientId) attToRemove.Add(kv.Key);
            foreach (var k in attToRemove) _npcAttitude.Remove(k);
            _eventsOccurred.Remove(clientId);
            _contractsCompleted.Remove(clientId);
            _contractsAccepted.Remove(clientId);
            _npcTalkedTo.Remove(clientId);
            _npcTalkEvents.Remove(clientId);
            _knownFactions.Remove(clientId);
            _knownNpcs.Remove(clientId);
            var flagToRemove = new List<(ulong, string)>();
            foreach (var kv in _worldFlags) if (kv.Key.Item1 == clientId) flagToRemove.Add(kv.Key);
            foreach (var k in flagToRemove) _worldFlags.Remove(k);
            var npcRemToRemove = new List<(ulong, string)>();
            foreach (var kv in _npcTradeRepRemainder) if (kv.Key.Item1 == clientId) npcRemToRemove.Add(kv.Key);
            foreach (var k in npcRemToRemove) _npcTradeRepRemainder.Remove(k);
            var facRemToRemove = new List<(ulong, int)>();
            foreach (var kv in _factionTradeRepRemainder) if (kv.Key.Item1 == clientId) facRemToRemove.Add(kv.Key);
            foreach (var k in facRemToRemove) _factionTradeRepRemainder.Remove(k);
            var upgToRemove = new List<(ulong, int)>();
            foreach (var kv in _upgradedItemsByPlayerItem) if (kv.Key.Item1 == clientId) upgToRemove.Add(kv.Key);
            foreach (var k in upgToRemove) _upgradedItemsByPlayerItem.Remove(k);

            // Apply quests
            if (data.quests != null)
            {
                var list = new List<QuestInstance>();
                for (int i = 0; i < data.quests.Count; i++)
                {
                    var e = data.quests[i];
                    var inst = new QuestInstance
                    {
                        questId = e.questId,
                        state = (QuestState)e.state,
                        currentStageId = e.currentStageId ?? "",
                        acceptedAtUnix = e.acceptedAtUnix,
                        completedAtUnix = e.completedAtUnix,
                        isTracked = e.isTracked
                    };
                    if (e.objectiveProgress != null)
                    {
                        for (int j = 0; j < e.objectiveProgress.Count; j++)
                        {
                            var op = e.objectiveProgress[j];
                            inst.objectiveProgress.Add(new QuestInstance.ObjectiveProgress
                            {
                                objectiveId = op.objectiveId,
                                currentCount = op.currentCount,
                                completed = op.completed
                            });
                        }
                    }
                    list.Add(inst);
                }
                if (list.Count > 0) _questsByPlayer[clientId] = list;
            }

            // Apply reputation
            if (data.reputation != null)
            {
                for (int i = 0; i < data.reputation.Count; i++)
                {
                    var e = data.reputation[i];
                    if (e.factionId <= 0 || e.factionId > byte.MaxValue)
                    {
                        Debug.LogWarning($"[QuestWorld] LoadPlayer: ignoring invalid faction wireId={e.factionId} for client={clientId}");
                        continue;
                    }
                    if (GetFactionCatalog().GetByWireId(e.factionId) == null)
                        Debug.LogWarning($"[QuestWorld] LoadPlayer: faction wireId={e.factionId} is not registered; preserving it in runtime state.");
                    _reputation[(clientId, e.factionId)] = e.value;
                }
            }

            // Apply npcAttitude
            if (data.npcAttitude != null)
            {
                for (int i = 0; i < data.npcAttitude.Count; i++)
                {
                    var e = data.npcAttitude[i];
                    if (string.IsNullOrEmpty(e.npcId)) continue;
                    _npcAttitude[(clientId, e.npcId)] = e.value;
                }
            }

            // Apply string sets
            if (data.stringSets != null)
            {
                for (int i = 0; i < data.stringSets.Count; i++)
                {
                    var e = data.stringSets[i];
                    if (e.values == null || e.values.Count == 0) continue;
                    switch (e.setName)
                    {
                        case "eventsOccurred": _eventsOccurred[clientId] = new HashSet<string>(e.values); break;
                        case "contractsCompleted": _contractsCompleted[clientId] = new HashSet<string>(e.values); break;
                        case "contractsAccepted": _contractsAccepted[clientId] = new HashSet<string>(e.values); break;
                        case "npcTalkedTo": _npcTalkedTo[clientId] = new HashSet<string>(e.values); break;
                        case "worldFlags":
                            for (int j = 0; j < e.values.Count; j++) _worldFlags[(clientId, e.values[j])] = true;
                            break;
                    }
                }
            }

            // T-KNOW: restore known factions
            if (data.knownFactions != null && data.knownFactions.Count > 0)
            {
                var knownF = new HashSet<int>();
                foreach (int id in data.knownFactions)
                {
                    if (id <= 0 || id > byte.MaxValue)
                    {
                        Debug.LogWarning($"[QuestWorld] LoadPlayer: ignoring invalid known faction wireId={id} for client={clientId}");
                        continue;
                    }
                    if (GetFactionCatalog().GetByWireId(id) == null)
                        Debug.LogWarning($"[QuestWorld] LoadPlayer: known faction wireId={id} is not registered; preserving it in runtime state.");
                    knownF.Add(id);
                }
                _knownFactions[clientId] = knownF;
            }
            else
            {
                int neutralWireId = GetNeutralWireId();
                var knownF = new HashSet<int>();
                if (neutralWireId > 0) knownF.Add(neutralWireId);
                _knownFactions[clientId] = knownF;
            }

            // T-KNOW: restore known NPCs
            if (data.knownNpcs != null && data.knownNpcs.Count > 0)
            {
                _knownNpcs[clientId] = new HashSet<string>(data.knownNpcs);
            }
            else
            {
                _knownNpcs[clientId] = new HashSet<string>();
            }

            // T-KNOWLEDGE-V2: restore known recipes
            Crafting.CraftingWorld.LoadRecipeKnowledge(clientId, data.knownRecipes);

            // T-MERCH01: restore fractional trade reputation remainders
            if (data.npcTradeRepRemainders != null)
            {
                for (int i = 0; i < data.npcTradeRepRemainders.Count; i++)
                {
                    var e = data.npcTradeRepRemainders[i];
                    if (e != null && !string.IsNullOrEmpty(e.npcId) && e.remainder > 0f)
                        _npcTradeRepRemainder[(clientId, e.npcId)] = Mathf.Clamp(e.remainder, 0f, 0.9999f);
                }
            }
            if (data.factionTradeRepRemainders != null)
            {
                for (int i = 0; i < data.factionTradeRepRemainders.Count; i++)
                {
                    var e = data.factionTradeRepRemainders[i];
                    if (e != null && e.factionId > 0 && e.factionId <= byte.MaxValue && e.remainder > 0f)
                        _factionTradeRepRemainder[(clientId, e.factionId)] = Mathf.Clamp(e.remainder, 0f, 0.9999f);
                }
            }

            // T-SMITH01: restore blacksmith upgraded items
            if (data.upgradedInventoryItems != null)
            {
                for (int i = 0; i < data.upgradedInventoryItems.Count; i++)
                {
                    var e = data.upgradedInventoryItems[i];
                    if (e != null && e.itemId > 0)
                        _upgradedItemsByPlayerItem[(clientId, e.itemId)] = e;
                }
            }

            if (Debug.isDebugBuild) Debug.Log($"[QuestWorld] LoadPlayer: client={clientId} restored {data.quests?.Count ?? 0} quests, {data.reputation?.Count ?? 0} factions, {data.npcAttitude?.Count ?? 0} npcAttitudes");
            return true;
        }

        // ============ T-KNOWLEDGE-V2: Death Knowledge Loss ============

        /// <summary>
        /// При смерти персонажа — потеря знания (рецепты/навыки) с вероятностью lossChance.
        /// Вызывается из PlayerTarget.TriggerDeathRespawn.
        /// Возвращает количество потерянных знаний (recipes + skills суммарно).
        /// </summary>
        public int ApplyDeathKnowledgeLoss(ulong clientId, float lossChance, System.Random rng)
        {
            int totalLost = 0;

            // Recipe loss (CraftingWorld)
            if (Crafting.CraftingWorld.IsInitialized)
                totalLost += Crafting.CraftingWorld.ApplyDeathRecipeLoss(clientId, lossChance, rng);

            // Skill knowledge loss (SkillsWorld)
            if (Skills.SkillsWorld.Instance != null)
                totalLost += Skills.SkillsWorld.Instance.ApplyDeathSkillKnowledgeLoss(clientId, lossChance, rng);

            if (totalLost > 0 && Debug.isDebugBuild)
                Debug.Log($"[QuestWorld] ApplyDeathKnowledgeLoss: client={clientId} totalLost={totalLost}");

            return totalLost;
        }

        // ============ Shutdown ============

        /// <summary>
        /// Cleanup on QuestServer.OnNetworkDespawn. T-Q05: clears singleton.
        /// T-Q18: flush SaveAll() first.
        /// </summary>
        public void Shutdown()
        {
            Debug.Log("[QuestWorld] Shutdown.");
            // T-Q18: flush all players
            if (Repository != null)
            {
                foreach (var clientId in _questsByPlayer.Keys)
                {
                    SavePlayer(clientId);
                }
            }
            _questById.Clear();
            _questsByPlayer.Clear();
            _reputation.Clear();
            _npcAttitude.Clear();
            _worldFlags.Clear();
            _knownFactions.Clear();
            _knownNpcs.Clear();
            _dialogByPlayer.Clear();
            _npcTradeRepRemainder.Clear();
            _factionTradeRepRemainder.Clear();
            _merchantRuntimeStock.Clear();
            _fallbackCredits.Clear();
            _upgradedItemsByPlayerItem.Clear();
            _variationService.ClearInstances();
        }
    }
}
