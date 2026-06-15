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

        [Header("Config")]
        [Tooltip("StatsConfig.asset: per-source XP multipliers, формула роста, global multiplier. " +
                 "Загружается в OnNetworkSpawn через Resources.Load.")]
        [SerializeField] private StatsConfig _config;

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

            // Load config (допускаем null в MVP: ручной wire-up в инспекторе, fallback на default)
            if (_config == null)
            {
                _config = Resources.Load<StatsConfig>("Stats/StatsConfig_Default");
                if (_config == null)
                {
                    Debug.LogWarning("[StatsServer] StatsConfig not assigned and Resources/Stats/StatsConfig_Default.asset not found. " +
                                     "Stats will not gain XP until config is wired up. (see T-P01)");
                }
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

            if (_config != null && _config.DebugLogging)
            {
                Debug.Log($"[StatsServer] OnNetworkSpawn — subscribed to 9 WorldEventBus events. " +
                          $"Config: base={_config.TierBaseXp} growth={_config.TierGrowthRate} globalMult={_config.GlobalMultiplier}");
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
            if (Instance == this) Instance = null;
        }

        // === T-P06: Persistence hooks (server-authoritative load/save) ===

        private void OnClientConnectedForStats(ulong clientId)
        {
            if (_repo == null || _world == null) return;
            if (_repo.TryLoad(clientId, out var data))
            {
                _world.LoadPlayer(clientId, data);
                if (_config != null && _config.DebugLogging)
                {
                    var stats = _world.GetOrCreateStats(clientId);
                    Debug.Log($"[StatsServer] OnClientConnectedForStats: client={clientId} — loaded " +
                              $"STR={stats.strength:F1}/T{stats.strengthTier} DEX={stats.dexterity:F1}/T{stats.dexterityTier} " +
                              $"INT={stats.intelligence:F1}/T{stats.intelligenceTier}");
                }
            }
            else
            {
                if (_config != null && _config.DebugLogging)
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
            if (_config != null && _config.DebugLogging)
            {
                Debug.Log($"[StatsServer] OnClientDisconnectedForStats: client={clientId} — saved to {_repo.GetSavePath(clientId)}");
            }
            OnPlayerDisconnected(clientId);
        }

        // === WorldEventBus handlers ===

        private void OnMiningCompleted(MiningCompletedEvent ev)
        {
            if (ev.PlayerId == 0 || ev.Quantity <= 0) return;
            var stat = _config.GetStatFor(XpSource.Mining);
            float xp = _config.GetBaseXp(XpSource.Mining) * ev.Quantity;
            ApplyXp(ev.PlayerId, stat, xp, $"Mining ×{ev.Quantity}");
        }

        private void OnCraftingCompleted(CraftingCompletedEvent ev)
        {
            if (ev.PlayerId == 0 || ev.Quantity <= 0) return;
            var stat = _config.GetStatFor(XpSource.Crafting);
            float xp = _config.GetBaseXp(XpSource.Crafting) * ev.Quantity;
            ApplyXp(ev.PlayerId, stat, xp, $"Crafting ×{ev.Quantity}");
        }

        private void OnExchangeCompleted(ExchangeCompletedEvent ev)
        {
            if (ev.PlayerId == 0 || ev.Quantity <= 0) return;
            var stat = _config.GetStatFor(XpSource.Exchange);
            float xp = _config.GetBaseXp(XpSource.Exchange) * ev.Quantity;
            ApplyXp(ev.PlayerId, stat, xp, $"Exchange({ev.Op}) ×{ev.Quantity}");
        }

        private void OnMarketTraded(MarketTradedEvent ev)
        {
            if (ev.PlayerId == 0 || ev.Quantity <= 0) return;
            var stat = _config.GetStatFor(XpSource.Market);
            float xp = _config.GetBaseXp(XpSource.Market) * ev.Quantity;
            ApplyXp(ev.PlayerId, stat, xp, $"Market({ev.Op}) ×{ev.Quantity}");
        }

        private void OnQuestAccepted(QuestAcceptedEvent ev)
        {
            if (ev.PlayerId == 0 || string.IsNullOrEmpty(ev.QuestId)) return;
            var stat = _config.GetStatFor(XpSource.QuestAccepted);
            float xp = _config.GetBaseXp(XpSource.QuestAccepted);
            ApplyXp(ev.PlayerId, stat, xp, $"QuestAccepted:{ev.QuestId}");
        }

        private void OnQuestCompleted(QuestCompletedEvent ev)
        {
            if (ev.PlayerId == 0 || string.IsNullOrEmpty(ev.QuestId)) return;
            var stat = _config.GetStatFor(XpSource.QuestCompleted);
            float xp = _config.GetBaseXp(XpSource.QuestCompleted);
            ApplyXp(ev.PlayerId, stat, xp, $"QuestCompleted:{ev.QuestId}");
        }

        private void OnDialogVisited(DialogVisitedEvent ev)
        {
            if (ev.PlayerId == 0 || string.IsNullOrEmpty(ev.NpcId)) return;
            if (_config == null) return;

            // Q1.4: unique-event tracking. eventKey включает NodeId если есть.
            string eventKey = string.IsNullOrEmpty(ev.NodeId) ? ev.NpcId : $"{ev.NpcId}:{ev.NodeId}";
            if (!IsUniqueDialogEvent(ev.PlayerId, eventKey))
            {
                if (_config.DebugLogging)
                {
                    Debug.Log($"[StatsServer] Player {ev.PlayerId} repeat dialog '{eventKey}' — no XP");
                }
                return;
            }

            var stat = _config.GetStatFor(XpSource.Dialog);
            float xp = _config.GetBaseXp(XpSource.Dialog);
            ApplyXp(ev.PlayerId, stat, xp, $"Dialog '{eventKey}' (unique)");

            if (_config.DebugLogging)
            {
                Debug.Log($"[StatsServer] Player {ev.PlayerId} NEW unique dialog '{eventKey}' → +{xp} INT");
            }
        }

        private void OnPilotTick(ShipPilotTickEvent ev)
        {
            if (ev.PlayerId == 0 || ev.DeltaDistance <= 0f || _config == null) return;

            // Q1.5: total piloted distance
            if (_config.TrackTotalDistance)
            {
                if (!_totalPilotedDistance.TryGetValue(ev.PlayerId, out var total)) total = 0f;
                total += ev.DeltaDistance;
                _totalPilotedDistance[ev.PlayerId] = total;
            }

            if (!_pilotDistanceBuffer.TryGetValue(ev.PlayerId, out var buffer)) buffer = 0f;
            buffer += ev.DeltaDistance;
            if (buffer >= _config.PilotDistanceXpThreshold)
            {
                float overshoot = buffer - _config.PilotDistanceXpThreshold;
                float xp = _config.GetBaseXp(XpSource.Pilot) * (buffer / _config.PilotDistanceXpThreshold);
                var stat = _config.GetStatFor(XpSource.Pilot);
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
            if (ev.PlayerId == 0 || _config == null) return;
            var stat = _config.GetStatFor(XpSource.Jump);
            float xp = _config.GetBaseXp(XpSource.Jump);
            ApplyXp(ev.PlayerId, stat, xp, "Jump");
        }

        // === Central ApplyXp (core) ===

        /// <summary>
        /// Применить rawXp к стату игрока. Tier promotion loop внутри. Snapshot шлётся после.
        /// </summary>
        public void ApplyXp(ulong clientId, StatType stat, float rawXp, string reasonForLog = null)
        {
            if (_config == null || rawXp == 0f) return;

            if (_config.DebugLogging || Debug.isDebugBuild)
            {
                Debug.Log($"[StatsServer] ApplyXp: client={clientId} stat={stat} xp={rawXp:F2} reason='{reasonForLog}'");
            }

            float xp = _config.ApplyGlobalMultiplier(rawXp);
            if (xp <= 0f) return;

            var stats = _world.GetOrCreateStats(clientId);
            ref float currentXp   = ref PlayerStatsRef.GetXpRef(ref stats, stat);
            ref int   currentTier = ref PlayerStatsRef.GetTierRef(ref stats, stat);
            ref float totalXp     = ref PlayerStatsRef.GetTotalXpRef(ref stats, stat);

            currentXp += xp;
            totalXp   += xp;

            // Tier promotion loop (no cap by design)
            int promotionsThisCall = 0;
            while (currentXp >= _config.XpForNextTier(currentTier))
            {
                currentXp -= _config.XpForNextTier(currentTier);
                currentTier++;
                promotionsThisCall++;
            }

            _world.SetStats(clientId, stats);

            if (_config.DebugLogging)
            {
                string reason = string.IsNullOrEmpty(reasonForLog) ? "" : $" ({reasonForLog})";
                Debug.Log($"[StatsServer] Player {clientId} gained {xp:F1} XP {stat}{reason}. " +
                          $"Now {currentXp:F1}/{_config.XpForNextTier(currentTier):F1} T{currentTier}, total={totalXp:F1}" +
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
            if (_config == null) { reason = "StatsServer config not ready"; return false; }
            if (xpDelta >= 0f) { reason = "ApplyXpDirect only for spending (negative delta)"; return false; }

            var stats = _world.GetOrCreateStats(clientId);
            ref float currentXp = ref PlayerStatsRef.GetXpRef(ref stats, stat);
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
        /// В T-P05 — пока просто snapshot (без effective calculation, это будет в T-P09).
        /// </summary>
        public void RecomputeAndSendSnapshot(ulong clientId)
        {
            // TODO T-P09: effective stat = (base + additive bonuses from equipment) * (1 + multiplicative bonuses)
            SendSnapshotToOwner(clientId);
        }

        private void SendSnapshotToOwner(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;

            var stats = _world.GetOrCreateStats(clientId);
            var snap = new StatsSnapshotDto
            {
                strength                  = stats.strength,
                dexterity                 = stats.dexterity,
                intelligence              = stats.intelligence,
                strengthTier              = stats.strengthTier,
                dexterityTier             = stats.dexterityTier,
                intelligenceTier          = stats.intelligenceTier,
                strengthXpForNextTier     = _config != null ? _config.XpForNextTier(stats.strengthTier) : 0f,
                dexterityXpForNextTier    = _config != null ? _config.XpForNextTier(stats.dexterityTier) : 0f,
                intelligenceXpForNextTier = _config != null ? _config.XpForNextTier(stats.intelligenceTier) : 0f,
                strengthTotalXp           = stats.strengthTotalXp,
                dexterityTotalXp          = stats.dexterityTotalXp,
                intelligenceTotalXp       = stats.intelligenceTotalXp,
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
            else if (_config != null && _config.DebugLogging)
            {
                Debug.Log($"[StatsServer] snapshot built for client {clientId} but RPC not found: " +
                          $"STR={snap.strength:F1}/T{snap.strengthTier} DEX={snap.dexterity:F1}/T{snap.dexterityTier} " +
                          $"INT={snap.intelligence:F1}/T{snap.intelligenceTier}");
            }
        }

        // === FixedUpdate distance tracker (walk XP, per 1m) ===

        private void FixedUpdate()
        {
            if (!IsServer || _config == null) return;
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
            const float MaxPilotDeltaPerFixedUpdate = 20.0f;

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
                        if (_config.DebugLogging) Debug.Log($"[StatsServer] Walk delta {dist:F1}m ignored (likely teleport/scene-load)");
                    }
                }
                _lastWalkPosPerPlayer[clientId] = currentPos;
            }
        }

        private void AccumulateWalkedXp(ulong clientId, float deltaDistance)
        {
            // Q1.5: total walked distance
            if (_config.TrackTotalDistance)
            {
                if (!_totalWalkedDistance.TryGetValue(clientId, out var total)) total = 0f;
                total += deltaDistance;
                _totalWalkedDistance[clientId] = total;
            }

            if (!_walkDistanceBuffer.TryGetValue(clientId, out var buffer)) buffer = 0f;
            buffer += deltaDistance;
            if (buffer >= _config.WalkDistanceXpThreshold)
            {
                float overshoot = buffer - _config.WalkDistanceXpThreshold;
                float xp = _config.GetBaseXp(XpSource.Walk) * (buffer / _config.WalkDistanceXpThreshold);
                var stat = _config.GetStatFor(XpSource.Walk);
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
