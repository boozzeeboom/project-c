// Project C: Character Progression — T-P05
// StatsServer: server-side XP hub. NetworkBehaviour scene-placed в BootstrapScene.
// Design: docs/Character/02_V2_ARCHITECTURE.md §2, docs/Character/04_STATS_PROGRESSION.md §2-§4
//         docs/Character/08_ROADMAP.md T-P05
//
// Что делает:
//   - Subscribe к 9 WorldEventBus событиям (8 + DialogVisitedEvent уже есть).
//   - FixedUpdate distance tracker: walk XP (per 1m) для on-foot, pilot XP — через ShipPilotTickEvent.
//   - ApplyXp: central XP application с tier promotion loop + per-stat mapping (HARDCODED в StatsConfig).
//   - RecomputeAndSendSnapshot: после equip/unequip (T-P09) → пересчитать effective stats → send to owner.
//   - ApplyXpDirect: для SkillsServer (T-P13) — spend XP на learn skill (xp<0).
//   - Unique-event dialog tracking (Q1.4: per-(player, npc, dialogNode), НЕ cooldown).
//
// Scene-placed (T-P18): [StatsServer] GameObject рядом с [GatheringServer], [CraftingServer].
// DontDestroyOnLoad не нужен — bootstrap-scene singleton, scene не unload'ится (per AGENTS.md).

using System;
using System.Collections.Generic;
using ProjectC.Core;
using ProjectC.Player;
using ProjectC.Stats.Dto;
using ProjectC.Stats.Persistence;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Stats
{
    /// <summary>
    /// Server-only XP/Stats hub. Singleton, scene-placed в BootstrapScene.
    /// </summary>
    public class StatsServer : NetworkBehaviour
    {
        public static StatsServer Instance { get; private set; }

        [Header("Config (P4 refactor: split StatsConfig → 3 SO)")]
        [Tooltip("ExperienceConfig.asset: per-source XP multipliers, формула роста, global multiplier.")]
        [SerializeField] private ExperienceConfig _expConfig;

        [Tooltip("StatSourceMapConfig.asset: XpSource → StatType mapping.")]
        [SerializeField] private StatSourceMapConfig _sourceMapConfig;

        [Tooltip("StatDebugConfig.asset: debug logging, distance thresholds, announce tier-up.")]
        [SerializeField] private StatDebugConfig _debugConfig;

        [Header("Health (T-HP01)")]
        [Tooltip("HealthConfig.asset: base HP, STR→HP multiplier, respawn HP %.")]
        [SerializeField] private HealthConfig _healthConfig;

        // === WorldEventBus subscriber handles (cached для Unsubscribe) ===
        private Action<MiningCompletedEvent> _handleMining;
        private Action<CraftingCompletedEvent> _handleCrafting;
        private Action<ExchangeCompletedEvent> _handleExchange;
        private Action<MarketTradedEvent> _handleMarket;
        private Action<QuestAcceptedEvent> _handleQuestAccepted;
        private Action<QuestCompletedEvent> _handleQuestCompleted;
        private Action<DialogVisitedEvent> _handleDialog;
        private Action<ShipPilotTickEvent> _handlePilot;
        private Action<PlayerJumpedEvent> _handleJumped;

        // === State (server-side) ===
        private StatsWorld _world;
        private ICharacterDataRepository _repo;

        // Walk distance tracker (per-player last position)
        private readonly Dictionary<ulong, Vector3> _lastWalkPosPerPlayer = new Dictionary<ulong, Vector3>();
        private readonly Dictionary<ulong, float> _walkDistanceBuffer = new Dictionary<ulong, float>();

        // Pilot distance buffer (per-player)
        private readonly Dictionary<ulong, float> _pilotDistanceBuffer = new Dictionary<ulong, float>();

        // Total walked/piloted distance (Q1.5) — для ачивок/трекеров
        private readonly Dictionary<ulong, float> _totalWalkedDistance = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _totalPilotedDistance = new Dictionary<ulong, float>();

        // Unique-event dialog tracking (Q1.4: НЕ cooldown, per (player, npc, dialogNode))
        private readonly Dictionary<ulong, HashSet<string>> _uniqueDialogEvents = new Dictionary<ulong, HashSet<string>>();

        // === Lifecycle ===

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[StatsServer] Duplicate instance detected, replacing. Old instance will be overwritten.");
            }
            Instance = this;

            // P4 refactor: load 3 split configs. Допускаем null — wire-up в инспекторе или Resources.Load fallback.
            if (_expConfig == null)
            {
                _expConfig = Resources.Load<ExperienceConfig>("Stats/ExperienceConfig_Default");
                if (_expConfig == null)
                    Debug.LogWarning("[StatsServer] ExperienceConfig not assigned/found. XP formula will not work.");
            }
            if (_sourceMapConfig == null)
            {
                _sourceMapConfig = Resources.Load<StatSourceMapConfig>("Stats/StatSourceMapConfig_Default");
                if (_sourceMapConfig == null)
                    Debug.LogWarning("[StatsServer] StatSourceMapConfig not assigned/found. Source→Stat mapping will default to INT.");
            }
            if (_debugConfig == null)
            {
                _debugConfig = Resources.Load<StatDebugConfig>("Stats/StatDebugConfig_Default");
                if (_debugConfig == null)
                    Debug.LogWarning("[StatsServer] StatDebugConfig not assigned/found. Debug settings default to off.");
            }
            if (_healthConfig == null)
            {
                _healthConfig = Resources.Load<HealthConfig>("Stats/HealthConfig_Default");
                if (_healthConfig == null)
                    Debug.LogWarning("[StatsServer] HealthConfig not assigned/found. HP will use defaults (base=100, multiplier=10).");
            }

            // StatsWorld singleton (server-only)
            _world = new StatsWorld();

            // Persistence (T-P06): per-clientId JSON repository
            _repo = new JsonCharacterDataRepository();

            // Hook OnClientConnected/Disconnected for load/save
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedForStats;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedForStats;

            // Subscribe к 9 WorldEventBus событиям
            _handleMining        = OnMiningCompleted;
            _handleCrafting      = OnCraftingCompleted;
            _handleExchange      = OnExchangeCompleted;
            _handleMarket        = OnMarketTraded;
            _handleQuestAccepted = OnQuestAccepted;
            _handleQuestCompleted= OnQuestCompleted;
            _handleDialog        = OnDialogVisited;
            _handlePilot         = OnPilotTick;
            _handleJumped        = OnJumped;

            WorldEventBus.Subscribe(_handleMining);
            WorldEventBus.Subscribe(_handleCrafting);
            WorldEventBus.Subscribe(_handleExchange);
            WorldEventBus.Subscribe(_handleMarket);
            WorldEventBus.Subscribe(_handleQuestAccepted);
            WorldEventBus.Subscribe(_handleQuestCompleted);
            WorldEventBus.Subscribe(_handleDialog);
            WorldEventBus.Subscribe(_handlePilot);
            WorldEventBus.Subscribe(_handleJumped);

            if (_debugConfig != null && _debugConfig.DebugLogging)
            {
                Debug.Log($"[StatsServer] OnNetworkSpawn — subscribed to 9 WorldEventBus events. " +
                          $"Config: base={(_expConfig != null ? _expConfig.TierBaseXp : 100f)} growth={(_expConfig != null ? _expConfig.TierGrowthRate : 1.5f)} globalMult={(_expConfig != null ? _expConfig.GlobalMultiplier : 1f)}");
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;

            // SESSION 1 fix: FLUSH SAVE для всех players перед unhook.
            // Раньше OnNetworkDespawn сразу unhook'ал — клиенты никогда не успевали
            // поймать OnClientDisconnectCallback и save не срабатывал.
            if (_world != null && _repo != null)
            {
                foreach (var clientId in _world.GetAllPlayerIds())
                {
                    try
                    {
                        var data = _world.BuildSaveData(clientId);
                        if (data != null) _repo.Save(clientId, data);
                        Debug.Log($"[StatsServer] OnNetworkDespawn: FLUSHED save for client={clientId}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[StatsServer] Flush save failed for client={clientId}: {ex.Message}");
                    }
                }
            }

            // T-P06: unhook persistence
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedForStats;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedForStats;
            }

            // Unsubscribe (mirror)
            if (_handleMining != null)         WorldEventBus.Unsubscribe(_handleMining);
            if (_handleCrafting != null)       WorldEventBus.Unsubscribe(_handleCrafting);
            if (_handleExchange != null)       WorldEventBus.Unsubscribe(_handleExchange);
            if (_handleMarket != null)         WorldEventBus.Unsubscribe(_handleMarket);
            if (_handleQuestAccepted != null)  WorldEventBus.Unsubscribe(_handleQuestAccepted);
            if (_handleQuestCompleted != null) WorldEventBus.Unsubscribe(_handleQuestCompleted);
            if (_handleDialog != null)         WorldEventBus.Unsubscribe(_handleDialog);
            if (_handlePilot != null)          WorldEventBus.Unsubscribe(_handlePilot);
            if (_handleJumped != null)         WorldEventBus.Unsubscribe(_handleJumped);

            StatsWorld.Reset();
            ProjectC.Skills.SkillsWorld.Reset();
            if (Instance == this) Instance = null;
        }

        // === T-P06: Persistence hooks (server-authoritative load/save) ===

        private void OnClientConnectedForStats(ulong clientId)
        {
            if (_repo == null || _world == null) return;
            if (_repo.TryLoad(clientId, out var data))
            {
                _world.LoadPlayer(clientId, data);
                // T-P12/T-P13: also load skills
                var skillsWorld = ProjectC.Skills.SkillsWorld.Instance;
                if (skillsWorld != null)
                {
                    Debug.Log($"[StatsServer.OnClientConnectedForStats] Loading skills: learnedSkillIds in JSON = {(data.skills?.learnedSkillIds != null ? string.Join(",", data.skills.learnedSkillIds) : "null")}");
                    skillsWorld.LoadPlayer(clientId, data);
                    var loadedCount = skillsWorld.GetLearnedSkillIds(clientId)?.Count ?? 0;
                    Debug.Log($"[StatsServer.OnClientConnectedForStats] After LoadPlayer: {loadedCount} skills loaded");
                    // Push loaded skills to client (mirrors SendSnapshotToOwner after load)
                    var ss = ProjectC.Skills.SkillsServer.Instance;
                    if (ss != null)
                    {
                        ss.SendSnapshotToOwner(clientId);
                        Debug.Log($"[StatsServer.OnClientConnectedForStats] SendSnapshotToOwner called");
                    }
                    else
                    {
                        Debug.LogWarning($"[StatsServer.OnClientConnectedForStats] SkillsServer.Instance is NULL — snapshot NOT sent!");
                    }
                }
                else
                {
                    Debug.LogWarning($"[StatsServer.OnClientConnectedForStats] SkillsWorld.Instance is NULL — skills NOT loaded!");
                }
                // T-P09: also load equipment
                var eqWorld = ProjectC.Equipment.EquipmentWorld.Instance;
                if (eqWorld != null)
                    eqWorld.LoadPlayer(clientId, data);
                if (_debugConfig != null && _debugConfig.DebugLogging)
                {
                    var stats = _world.GetOrCreateStats(clientId);
                    Debug.Log($"[StatsServer] OnClientConnectedForStats: client={clientId} — loaded " +
                              $"STR={stats.strength.xp:F1}/T{stats.strength.tier} DEX={stats.dexterity.xp:F1}/T{stats.dexterity.tier} " +
                              $"INT={stats.intelligence.xp:F1}/T{stats.intelligence.tier}");
                }
            }
            else
            {
                if (_debugConfig != null && _debugConfig.DebugLogging)
                {
                    Debug.Log($"[StatsServer] OnClientConnectedForStats: client={clientId} — no save file, starting fresh");
                }
            }
            // Send initial snapshot (loaded or default)
            SendSnapshotToOwner(clientId);
        }

        private void OnClientDisconnectedForStats(ulong clientId)
        {
            if (_repo == null || _world == null) return;
            // Build save DTO from current state + atomic save
            var data = _world.BuildSaveData(clientId);
            _repo.Save(clientId, data);
            if (_debugConfig != null && _debugConfig.DebugLogging)
            {
                Debug.Log($"[StatsServer] OnClientDisconnectedForStats: client={clientId} — saved to {_repo.GetSavePath(clientId)}");
            }
            OnPlayerDisconnected(clientId);
        }

        // === WorldEventBus handlers ===

        private void OnMiningCompleted(MiningCompletedEvent ev)
        {
            if (ev.PlayerId == 0 || ev.Quantity <= 0) return;
            var stat = _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(XpSource.Mining) : StatType.Intelligence;
            float xp = (_expConfig != null ? _expConfig.GetBaseXp(XpSource.Mining) : 0f) * ev.Quantity;
            ApplyXp(ev.PlayerId, stat, xp, $"Mining ×{ev.Quantity}");
        }

        private void OnCraftingCompleted(CraftingCompletedEvent ev)
        {
            if (ev.PlayerId == 0 || ev.Quantity <= 0) return;
            var stat = _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(XpSource.Crafting) : StatType.Intelligence;
            float xp = (_expConfig != null ? _expConfig.GetBaseXp(XpSource.Crafting) : 0f) * ev.Quantity;
            ApplyXp(ev.PlayerId, stat, xp, $"Crafting ×{ev.Quantity}");
        }

        private void OnExchangeCompleted(ExchangeCompletedEvent ev)
        {
            if (ev.PlayerId == 0 || ev.Quantity <= 0) return;
            var stat = _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(XpSource.Exchange) : StatType.Intelligence;
            float xp = (_expConfig != null ? _expConfig.GetBaseXp(XpSource.Exchange) : 0f) * ev.Quantity;
            ApplyXp(ev.PlayerId, stat, xp, $"Exchange({ev.Op}) ×{ev.Quantity}");
        }

        private void OnMarketTraded(MarketTradedEvent ev)
        {
            if (ev.PlayerId == 0 || ev.Quantity <= 0) return;
            var stat = _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(XpSource.Market) : StatType.Intelligence;
            float xp = (_expConfig != null ? _expConfig.GetBaseXp(XpSource.Market) : 0f) * ev.Quantity;
            ApplyXp(ev.PlayerId, stat, xp, $"Market({ev.Op}) ×{ev.Quantity}");
        }

        private void OnQuestAccepted(QuestAcceptedEvent ev)
        {
            if (ev.PlayerId == 0 || string.IsNullOrEmpty(ev.QuestId)) return;
            var stat = _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(XpSource.QuestAccepted) : StatType.Intelligence;
            float xp = _expConfig != null ? _expConfig.GetBaseXp(XpSource.QuestAccepted) : 0f;
            ApplyXp(ev.PlayerId, stat, xp, $"QuestAccepted:{ev.QuestId}");
        }

        private void OnQuestCompleted(QuestCompletedEvent ev)
        {
            if (ev.PlayerId == 0 || string.IsNullOrEmpty(ev.QuestId)) return;
            var stat = _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(XpSource.QuestCompleted) : StatType.Intelligence;
            float xp = _expConfig != null ? _expConfig.GetBaseXp(XpSource.QuestCompleted) : 0f;
            ApplyXp(ev.PlayerId, stat, xp, $"QuestCompleted:{ev.QuestId}");
        }

        private void OnDialogVisited(DialogVisitedEvent ev)
        {
            if (ev.PlayerId == 0 || string.IsNullOrEmpty(ev.NpcId)) return;
            if (_expConfig == null) return;

            // Q1.4: unique-event tracking. eventKey включает NodeId если есть.
            string eventKey = string.IsNullOrEmpty(ev.NodeId) ? ev.NpcId : $"{ev.NpcId}:{ev.NodeId}";
            if (!IsUniqueDialogEvent(ev.PlayerId, eventKey))
            {
                if (_debugConfig != null && _debugConfig.DebugLogging)
                {
                    Debug.Log($"[StatsServer] Player {ev.PlayerId} repeat dialog '{eventKey}' — no XP");
                }
                return;
            }

            var stat = _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(XpSource.Dialog) : StatType.Intelligence;
            float xp = _expConfig != null ? _expConfig.GetBaseXp(XpSource.Dialog) : 0f;
            ApplyXp(ev.PlayerId, stat, xp, $"Dialog '{eventKey}' (unique)");

            if (_debugConfig != null && _debugConfig.DebugLogging)
            {
                Debug.Log($"[StatsServer] Player {ev.PlayerId} NEW unique dialog '{eventKey}' → +{xp} INT");
            }
        }

        private void OnPilotTick(ShipPilotTickEvent ev)
        {
            if (ev.PlayerId == 0 || ev.DeltaDistance <= 0f || _sourceMapConfig == null) return;

            // Q1.5: total piloted distance
            if ((_debugConfig != null && _debugConfig.TrackTotalDistance))
            {
                if (!_totalPilotedDistance.TryGetValue(ev.PlayerId, out var total)) total = 0f;
                total += ev.DeltaDistance;
                _totalPilotedDistance[ev.PlayerId] = total;
            }

            if (!_pilotDistanceBuffer.TryGetValue(ev.PlayerId, out var buffer)) buffer = 0f;
            buffer += ev.DeltaDistance;
            if (buffer >= (_debugConfig != null ? _debugConfig.PilotDistanceXpThreshold : 10f))
            {
                float overshoot = buffer - (_debugConfig != null ? _debugConfig.PilotDistanceXpThreshold : 10f);
                float xp = (_expConfig != null ? _expConfig.GetBaseXp(XpSource.Pilot) : 0f) * (buffer / (_debugConfig != null ? _debugConfig.PilotDistanceXpThreshold : 10f));
                var stat = _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(XpSource.Pilot) : StatType.Intelligence;
                ApplyXp(ev.PlayerId, stat, xp, $"Pilot +{buffer:F1}m");
                _pilotDistanceBuffer[ev.PlayerId] = overshoot;
            }
            else
            {
                _pilotDistanceBuffer[ev.PlayerId] = buffer;
            }
        }

        private void OnJumped(PlayerJumpedEvent ev)
        {
            if (ev.PlayerId == 0 || _expConfig == null) return;
            var stat = _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(XpSource.Jump) : StatType.Intelligence;
            float xp = _expConfig != null ? _expConfig.GetBaseXp(XpSource.Jump) : 0f;
            ApplyXp(ev.PlayerId, stat, xp, "Jump");
        }

        // === Central ApplyXp (core) ===

        /// <summary>
        /// Применить rawXp к стату игрока. Tier promotion loop внутри. Snapshot шлётся после.
        /// </summary>
        public void ApplyXp(ulong clientId, StatType stat, float rawXp, string reasonForLog = null)
        {
            if (_expConfig == null || rawXp == 0f) return;

            if (_debugConfig != null && _debugConfig.DebugLogging)
            {
                Debug.Log($"[StatsServer] ApplyXp: client={clientId} stat={stat} xp={rawXp:F2} reason='{reasonForLog}'");
            }

            float xp = _expConfig != null ? _expConfig.ApplyGlobalMultiplier(rawXp) : rawXp;
            if (xp <= 0f) return;

            var stats = _world.GetOrCreateStats(clientId);
            ref float currentXp   = ref PlayerStats.Xp(ref stats, stat);
            ref int   currentTier = ref PlayerStats.Tier(ref stats, stat);
            ref float totalXp     = ref PlayerStats.TotalXp(ref stats, stat);

            currentXp += xp;
            totalXp   += xp;

            // Tier promotion loop (no cap by design)
            int promotionsThisCall = 0;
            while (currentXp >= (_expConfig != null ? _expConfig.XpForNextTier(currentTier) : 100f))
            {
                currentXp -= (_expConfig != null ? _expConfig.XpForNextTier(currentTier) : 100f);
                currentTier++;
                promotionsThisCall++;
            }

            _world.SetStats(clientId, stats);

            if (_debugConfig != null && _debugConfig.DebugLogging)
            {
                string reason = string.IsNullOrEmpty(reasonForLog) ? "" : $" ({reasonForLog})";
                Debug.Log($"[StatsServer] Player {clientId} gained {xp:F1} XP {stat}{reason}. " +
                          $"Now {currentXp:F1}/{(_expConfig != null ? _expConfig.XpForNextTier(currentTier) : 100f):F1} T{currentTier}, total={totalXp:F1}" +
                          (promotionsThisCall > 0 ? $", PROMOTED ×{promotionsThisCall}!" : ""));
            }

            // Snapshot push
            SendSnapshotToOwner(clientId);
        }

        /// <summary>
        /// Spend XP (отрицательное значение) — для SkillsServer.TryLearnSkill (T-P13).
        /// Тот же ApplyXp flow, но с проверкой что у игрока достаточно XP.
        /// </summary>
        public bool ApplyXpDirect(ulong clientId, StatType stat, float xpDelta, out string reason)
        {
            reason = "";
            if (_expConfig == null) { reason = "StatsServer config not ready"; return false; }
            if (xpDelta >= 0f) { reason = "ApplyXpDirect only for spending (negative delta)"; return false; }

            var stats = _world.GetOrCreateStats(clientId);
            ref float currentXp = ref PlayerStats.Xp(ref stats, stat);
            if (currentXp < -xpDelta)
            {
                reason = $"Не хватает XP (есть {currentXp:F1}, нужно {-xpDelta:F1})";
                return false;
            }

            ApplyXp(clientId, stat, xpDelta, "Direct spend");
            return true;
        }

        /// <summary>
        /// Recompute effective stats (после equip/unequip/learn/forget) и шлёт snapshot.
        /// SESSION 2: effective = base + equip bonuses. UI использует effective для отображения.
        /// </summary>
        public void RecomputeAndSendSnapshot(ulong clientId)
        {
            var stats = _world.GetOrCreateStats(clientId);
            // Берём бонусы от экипировки (EquipmentWorld — singleton)
            float bonusStr = 0f, bonusDex = 0f, bonusInt = 0f;
            float multStr = 0f, multDex = 0f, multInt = 0f;
            var eqWorld = ProjectC.Equipment.EquipmentWorld.Instance;
            if (eqWorld != null)
            {
                eqWorld.GetEquipStatBonuses(clientId,
                    out bonusStr, out bonusDex, out bonusInt,
                    out multStr, out multDex, out multInt);
            }
            _pendingEquipBonus[clientId] = (bonusStr, bonusDex, bonusInt);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[StatsServer] Recompute equip: client={clientId} +STR={bonusStr} +DEX={bonusDex} +INT={bonusInt}");
            }
            SendSnapshotToOwner(clientId);
        }

        // SESSION 2: cache для бонусов экипировки (заполняется в Recompute, читается в SendSnapshot)
        private readonly System.Collections.Generic.Dictionary<ulong, (float str, float dex, float iq)> _pendingEquipBonus = new System.Collections.Generic.Dictionary<ulong, (float, float, float)>();

        private void SendSnapshotToOwner(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;

            var stats = _world.GetOrCreateStats(clientId);
            // P8: equip bonuses + multipliers. effective = (base + flat) * (1 + mult).
            float bonusStr = 0f, bonusDex = 0f, bonusInt = 0f;
            float multStr = 0f, multDex = 0f, multInt = 0f;
            var eqWorld = ProjectC.Equipment.EquipmentWorld.Instance;
            if (eqWorld != null)
            {
                eqWorld.GetEquipStatBonuses(clientId,
                    out bonusStr, out bonusDex, out bonusInt,
                    out multStr, out multDex, out multInt);
            }
            // T-HP01: read HP from PlayerTarget (server-side).
            // Если HP ещё не проинициализирован (0) — вычисляем maxHp из STR и считаем что полное.
            int currentHp = 0, maxHp = 0;
            var pt = netPlayer.GetComponent<ProjectC.Combat.PlayerTarget>();
            if (pt != null)
            {
                currentHp = pt.GetCurrentHp();
                maxHp = pt.GetMaxHp();
            }
            if (maxHp <= 0)
            {
                maxHp = ComputeMaxHp(clientId);
                currentHp = maxHp; // fresh spawn = full HP
            }

            var snap = new StatsSnapshotDto
            {
                strength                  = stats.strength.xp,
                dexterity                 = stats.dexterity.xp,
                intelligence              = stats.intelligence.xp,
                strengthTier              = stats.strength.tier,
                dexterityTier             = stats.dexterity.tier,
                intelligenceTier          = stats.intelligence.tier,
                strengthXpForNextTier     = _expConfig != null ? _expConfig.XpForNextTier(stats.strength.tier) : 0f,
                dexterityXpForNextTier    = _expConfig != null ? _expConfig.XpForNextTier(stats.dexterity.tier) : 0f,
                intelligenceXpForNextTier = _expConfig != null ? _expConfig.XpForNextTier(stats.intelligence.tier) : 0f,
                strengthTotalXp           = stats.strength.totalXp,
                dexterityTotalXp          = stats.dexterity.totalXp,
                intelligenceTotalXp       = stats.intelligence.totalXp,
                effectiveStrength         = (PlayerStats.StatsToFlat(stats.strength.tier) + bonusStr) * (1f + multStr),
                effectiveDexterity        = (PlayerStats.StatsToFlat(stats.dexterity.tier) + bonusDex) * (1f + multDex),
                effectiveIntelligence     = (PlayerStats.StatsToFlat(stats.intelligence.tier) + bonusInt) * (1f + multInt),
                currentHp                 = currentHp,
                maxHp                     = maxHp,
            };

            // T-P06: ReceiveStatsSnapshotTargetRpc добавлен в NetworkPlayer (owner→server).
            // Сигнатура: ReceiveStatsSnapshotTargetRpc(StatsSnapshotDto snapshot, RpcParams rpcParams).
            // При reflection-вызове NGO RPC нужно передать ОБА параметра (default RpcParams через Activator.CreateInstance).
            // Вызываем через reflection — работает в обоих направлениях:
            //   T-P05 (RPC не существовал) → fall-back на Debug.Log
            //   T-P06+ → reflection находит метод и вызывает его с (snap, default RpcParams)
            var mi = typeof(NetworkPlayer).GetMethod("ReceiveStatsSnapshotTargetRpc");
            if (mi != null)
            {
                // default RpcParams — это struct, Activator.CreateInstance работает для value-types
                var defaultRpcParams = System.Activator.CreateInstance(typeof(RpcParams));
                mi.Invoke(netPlayer, new object[] { snap, defaultRpcParams });
            }
            else if (_debugConfig != null && _debugConfig.DebugLogging)
            {
                Debug.Log($"[StatsServer] snapshot built for client {clientId} but RPC not found: " +
                          $"STR={snap.strength:F1}/T{snap.strengthTier} DEX={snap.dexterity:F1}/T{snap.dexterityTier} " +
                          $"INT={snap.intelligence:F1}/T{snap.intelligenceTier}");
            }
        }

        // === FixedUpdate distance tracker (walk XP, per 1m) ===

        private void FixedUpdate()
        {
            if (!IsServer || _debugConfig == null) return;
            if (NetworkManager.Singleton == null) return;

            // SESSION 1 fix: periodic auto-save (каждые AutoSaveInterval секунд).
            // Защита от crash / быстрого exit — данные не теряются.
            if (_repo != null && _world != null)
            {
                if (Time.unscaledTime >= _nextAutoSaveUtc)
                {
                    _nextAutoSaveUtc = Time.unscaledTime + AutoSaveInterval;
                    foreach (var cid in _world.GetAllPlayerIds())
                    {
                        try
                        {
                            var d = _world.BuildSaveData(cid);
                            if (d != null) _repo.Save(cid, d);
                        }
                        catch { /* silent on periodic; main flow catches errors */ }
                    }
                }
            }

            // SESSION 1 fix: sane max per FixedUpdate — игрок не может переместиться больше чем
            // MaxWalkDeltaPerFixedUpdate метров за 1/50 сек. Защита от teleport/scene-load спайков
            // которые раньше давали 10M+ XP за один кадр.
            const float MaxWalkDeltaPerFixedUpdate = 5.0f; // 5 m/frame = 250 m/s — всё равно слишком быстро


            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                var player = NetworkManager.Singleton.ConnectedClients[clientId]?.PlayerObject;
                if (player == null) continue;
                var netPlayer = player.GetComponent<NetworkPlayer>();
                if (netPlayer == null) continue;

                // Skip if в корабле (pilot distance обрабатывается через ShipPilotTickEvent)
                if (netPlayer.IsInShip)
                {
                    // Обновить lastPos чтобы при disembark не было гигантского delta
                    _lastWalkPosPerPlayer[clientId] = netPlayer.transform.position;
                    continue;
                }

                Vector3 currentPos = netPlayer.transform.position;
                if (_lastWalkPosPerPlayer.TryGetValue(clientId, out var lastPos))
                {
                    float dist = Vector3.Distance(currentPos, lastPos);
                    if (dist > 0.01f && dist < MaxWalkDeltaPerFixedUpdate)  // SESSION 1 fix: clamp teleport
                    {
                        AccumulateWalkedXp(clientId, dist);
                    }
                    else if (dist >= MaxWalkDeltaPerFixedUpdate)
                    {
                        // Телепорт или scene load — обновить lastPos без XP
                        _lastWalkPosPerPlayer[clientId] = currentPos;
                        if (_debugConfig != null && _debugConfig.DebugLogging) Debug.Log($"[StatsServer] Walk delta {dist:F1}m ignored (likely teleport/scene-load)");
                    }
                }
                _lastWalkPosPerPlayer[clientId] = currentPos;
            }
        }

        private void AccumulateWalkedXp(ulong clientId, float deltaDistance)
        {
            // Q1.5: total walked distance
            if ((_debugConfig != null && _debugConfig.TrackTotalDistance))
            {
                if (!_totalWalkedDistance.TryGetValue(clientId, out var total)) total = 0f;
                total += deltaDistance;
                _totalWalkedDistance[clientId] = total;
            }

            if (!_walkDistanceBuffer.TryGetValue(clientId, out var buffer)) buffer = 0f;
            buffer += deltaDistance;
            if (buffer >= (_debugConfig != null ? _debugConfig.WalkDistanceXpThreshold : 1f))
            {
                float overshoot = buffer - (_debugConfig != null ? _debugConfig.WalkDistanceXpThreshold : 1f);
                float xp = _expConfig != null ? _expConfig.GetBaseXp(XpSource.Walk) : 0f * (buffer / (_debugConfig != null ? _debugConfig.WalkDistanceXpThreshold : 1f));
                var stat = _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(XpSource.Walk) : StatType.Intelligence;
                ApplyXp(clientId, stat, xp, $"Walk +{buffer:F2}m");
                _walkDistanceBuffer[clientId] = overshoot;
            }
            else
            {
                _walkDistanceBuffer[clientId] = buffer;
            }
        }

        // === Q1.4: unique-event dialog tracking ===
        private const float AutoSaveInterval = 30.0f; // SESSION 1: save every 30 sec
        private float _nextAutoSaveUtc;

        private bool IsUniqueDialogEvent(ulong clientId, string eventKey)
        {
            if (!_uniqueDialogEvents.TryGetValue(clientId, out var set))
            {
                set = new HashSet<string>();
                _uniqueDialogEvents[clientId] = set;
            }
            return set.Add(eventKey);
        }

        // === Public read API (для Getters в T-P09/T-P15 UI) ===

        public float GetTotalWalkedDistance(ulong clientId) =>
            _totalWalkedDistance.TryGetValue(clientId, out var v) ? v : 0f;

        public float GetTotalPilotedDistance(ulong clientId) =>
            _totalPilotedDistance.TryGetValue(clientId, out var v) ? v : 0f;

        public PlayerStats GetPlayerStats(ulong clientId) =>
            _world != null ? _world.GetOrCreateStats(clientId) : PlayerStats.Default;

        /// <summary>P5 fix: expose config stat mapping for external callers (GatheringServer).</summary>
        public StatType GetStatFor(XpSource source) => _sourceMapConfig != null ? _sourceMapConfig.GetStatFor(source) : StatType.Strength;

        /// <summary>T-HP01: Public access to HealthConfig for PlayerTarget/PlayerRespawnTracker.</summary>
        public HealthConfig HealthConfig => _healthConfig;

        /// <summary>
        /// T-HP01: Вычислить максимальное HP для игрока по STR tier.
        /// Использует HealthConfig формулу: baseHp + STR_flat × multiplier.
        /// Если HealthConfig не назначен — fallback на дефолт (100 + tier*5+10 * 10).
        /// </summary>
        public int ComputeMaxHp(ulong clientId)
        {
            if (_world == null) return 0;
            var stats = _world.GetOrCreateStats(clientId);
            int strFlat = PlayerStats.StatsToFlat(stats.strength.tier);

            if (_healthConfig != null)
                return _healthConfig.ComputeMaxHp(strFlat);

            // Fallback defaults (если HealthConfig не загружен)
            return Mathf.Max(1, Mathf.RoundToInt(100f + strFlat * 10f));
        }

        /// <summary>
        /// T-P13: Trigger immediate save for a player (called by SkillsServer after learn/forget).
        /// Builds full CharacterSaveData (stats + skills + equipment) and writes to disk.
        /// </summary>
        public void SaveCharacter(ulong clientId)
        {
            if (_repo == null || _world == null)
            {
                Debug.LogWarning($"[StatsServer.SaveCharacter] client={clientId} SKIP: _repo={_repo != null} _world={_world != null}");
                return;
            }
            var data = _world.BuildSaveData(clientId);
            Debug.Log($"[StatsServer.SaveCharacter] client={clientId} saving... skillsCount={data?.skills?.learnedSkillIds?.Length ?? 0}");
            _repo.Save(clientId, data);
            Debug.Log($"[StatsServer.SaveCharacter] client={clientId} SAVED to {_repo.GetSavePath(clientId)}");
        }

        /// <summary>Disconnect cleanup (T-P18 hook через NetworkManagerController).</summary>
        public void OnPlayerDisconnected(ulong clientId)
        {
            if (_world == null) return;
            _world.RemovePlayer(clientId);
            _lastWalkPosPerPlayer.Remove(clientId);
            _walkDistanceBuffer.Remove(clientId);
            _pilotDistanceBuffer.Remove(clientId);
            _totalWalkedDistance.Remove(clientId);
            _totalPilotedDistance.Remove(clientId);
            _uniqueDialogEvents.Remove(clientId);
        }
    }
}
