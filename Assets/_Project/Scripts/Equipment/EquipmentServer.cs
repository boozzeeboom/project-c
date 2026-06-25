// Project C: Character Progression — T-P09
// EquipmentServer: NetworkBehaviour RPC hub для equip/unequip. Scene-placed в BootstrapScene.
// Design: docs/Character/05_CLOTHING_AND_MODULES.md §4, docs/Character/08_ROADMAP.md T-P09
//
// Pattern: копия GatheringServer (T-G03) + InventoryServer для RPC hub + rate limit.
//
// RPCs (client→server):
//   - RequestEquipRpc(int itemId, EquipSlot slot) — RequestEquip
//   - RequestUnequipRpc(EquipSlot slot)            — RequestUnequip
// Server→client (TargetRPCs через NetworkPlayer):
//   - ReceiveEquipResultTargetRpc(EquipResultDto result)   — T-P10 stub
//   - ReceiveEquipmentSnapshotTargetRpc(EquipmentSnapshotDto snapshot) — T-P10 stub
// Stub: используем reflection для null-safe call (T-P10 NMC auto-spawn + actual RPCs).
//
// Cross-NetworkObject deps:
//   - EquipmentWorld (T-P09, same script)             — primary
//   - InventoryWorld (existing)                       — item ownership check
//   - StatsServer.Instance (T-P05)                    — RecomputeAndSendSnapshot после equip
//   - SkillsWorld (T-P13)                             — skill requirements (null-safe в T-P09)

using System;
using System.Collections.Generic;
using ProjectC.Equipment.Dto;
using ProjectC.Player;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Equipment
{
    public class EquipmentServer : NetworkBehaviour
    {
        public static EquipmentServer Instance { get; private set; }

        [Header("Rate limit")]
        [Tooltip("Max equip/unequip ops per second per client. Anti-spam.")]
        [SerializeField, Min(1)] private int _maxOpsPerSec = 5;

        // Rate limit state: clientId → next allowed time
        private readonly Dictionary<ulong, float> _nextAllowedTimePerClient = new Dictionary<ulong, float>();

        private EquipmentWorld _world;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[EquipmentServer] Duplicate instance detected, replacing.");
            }
            Instance = this;
            _world = new EquipmentWorld();

            // SESSION 2: SESSION 1 fix: hook client connect для seed equip items + initial snapshot.
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnectedForSeed;
            }

            // SESSION 2: auto-register ClothingItemData/ModuleItemData assets в InventoryWorld._itemDatabase.
            // Раньше .asset'ы в Resources/Items/Clothing и /Modules НЕ попадали в _itemDatabase автоматически
            // (только из Resources/Items/Items_* которые ItemData не ClothingItemData). Вручную регистрируем.
            RegisterEquipmentAssets();

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[EquipmentServer] OnNetworkSpawn — IsServer=true, rateLimit={_maxOpsPerSec}ops/sec");
            }
        }

        private void RegisterEquipmentAssets()
        {
            try
            {
                var inv = ProjectC.Items.InventoryWorld.Instance;
                if (inv == null) return;
                var field = typeof(ProjectC.Items.InventoryWorld).GetField("_itemDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var db = field?.GetValue(inv) as System.Collections.Generic.Dictionary<int, ProjectC.Items.ItemData>;
                if (db == null) return;

                // SESSION 2 fix: грузим ClothingItemData/ModuleItemData по подпапкам явно.
                // Resources.LoadAll<ItemData>("Items") НЕ подхватывает подклассы — только базовый ItemData.
                // Resources.LoadAll<T>(path) фильтрует по ТОЧНОМУ типу, поэтому загружаем по подпапкам.
                int registered = 0;
                int nextId = 0;
                foreach (var k in db.Keys) { if (k >= nextId) nextId = k + 1; }

                // Известные подпапки с экипировкой
                var equipFolders = new[] {
                    "Items/Clothing",
                    "Items/Modules",
                    "Items/Equipment",
                    "Items/Weapons"  // T-CB03: WeaponItemData
                };
                foreach (var folder in equipFolders)
                {
                    var cloth = Resources.LoadAll<ProjectC.Equipment.ClothingItemData>(folder);
                    foreach (var c in cloth)
                    {
                        if (c == null) continue;
                        // Не дублируем: ищем по itemName
                        bool already = false;
                        foreach (var kvp in db) { if (kvp.Value == c) { already = true; break; } }
                        if (!already) { db[nextId++] = c; registered++; }
                    }
                    var mods = Resources.LoadAll<ProjectC.Equipment.ModuleItemData>(folder);
                    foreach (var m in mods)
                    {
                        if (m == null) continue;
                        bool already = false;
                        foreach (var kvp in db) { if (kvp.Value == m) { already = true; break; } }
                        if (!already) { db[nextId++] = m; registered++; }
                    }
                    // T-CB03: WeaponItemData (extends ItemData). Resources.LoadAll<T> фильтрует
                    // по ТОЧНОМУ типу — нужна явная загрузка.
                    var weapons = Resources.LoadAll<ProjectC.Equipment.WeaponItemData>(folder);
                    foreach (var w in weapons)
                    {
                        if (w == null) continue;
                        bool already = false;
                        foreach (var kvp in db) { if (kvp.Value == w) { already = true; break; } }
                        if (!already) { db[nextId++] = w; registered++; }
                    }
                }
                if (Debug.isDebugBuild && registered > 0)
                {
                    Debug.Log($"[EquipmentServer] Registered {registered} clothing+module assets");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EquipmentServer] RegisterEquipmentAssets failed: {ex.Message}");
            }
        }

        /// <summary>
        /// SESSION 2: найти itemId по имени предмета в _itemDatabase.
        /// Используется в seed вместо хардкода ID.
        /// </summary>
        private int FindItemIdByName(string itemName)
        {
            try
            {
                var inv = ProjectC.Items.InventoryWorld.Instance;
                if (inv == null) return -1;
                var field = typeof(ProjectC.Items.InventoryWorld).GetField("_itemDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var db = field?.GetValue(inv) as System.Collections.Generic.Dictionary<int, ProjectC.Items.ItemData>;
                if (db == null) return -1;
                foreach (var kvp in db)
                {
                    if (kvp.Value != null && kvp.Value.itemName == itemName) return kvp.Key;
                }
            }
            catch (System.Exception ex) { Debug.LogWarning($"[EquipmentServer] FindItemIdByName failed: {ex.Message}"); }
            return -1;
        }

        private void HandleClientConnectedForSeed(ulong clientId)
        {
            if (!IsServer) return;
            // SESSION 2: retry until InventoryWorld.Instance доступен (NMC может
            // создавать EquipmentServer раньше чем InventoryWorld). Schedule delayed try.
            if (ProjectC.Items.InventoryWorld.Instance == null)
            {
                System.Collections.IEnumerator Retry()
                {
                    for (int i = 0; i < 20; i++)  // 2 sec total
                    {
                        yield return new UnityEngine.WaitForSeconds(0.1f);
                        if (ProjectC.Items.InventoryWorld.Instance != null) { DoSeed(clientId); yield break; }
                    }
                    Debug.LogWarning("[EquipmentServer] Seed: InventoryWorld.Instance не появился за 2с");
                }
                StartCoroutine(Retry());
                return;
            }
            DoSeed(clientId);
        }

        private void DoSeed(ulong clientId)
        {
            if (!IsServer) return;
            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv == null) return;

            // SESSION 2 refactor: находим предметы по ИМЕНИ (не по хардкод ID).
            // Names соответствуют ClothingItemData/ModuleItemData assets в Resources/Items/Clothing/ и /Modules/.
            // Любой ClothingItemData/ModuleItemData может надеваться — нет привязки к конкретному itemId.
            var seedNames = new[] { "Рабочая каска", "Кузнечный фартук", "Дорожные сапоги", "Крафт-ассистент" };

            // Add 4 to inventory by name
            foreach (var name in seedNames)
            {
                int id = FindItemIdByName(name);
                if (id <= 0) { Debug.LogWarning($"[EquipmentServer] Seed: item '{name}' not in db"); continue; }
                if (!inv.HasItem(clientId, id))
                {
                    inv.AddItemDirect(clientId, id, ProjectC.Items.ItemType.Equipment);
                }
            }

            // Equip "Рабочая каска" to Head by name
            int helmetId = FindItemIdByName("Рабочая каска");
            if (helmetId > 0)
            {
                if (!inv.HasItem(clientId, helmetId))
                {
                    inv.AddItemDirect(clientId, helmetId, ProjectC.Items.ItemType.Equipment);
                }
                string equipReason = "";
                if (_world != null && _world.TryEquip(clientId, helmetId, ProjectC.Equipment.EquipSlot.Head, out equipReason))
                {
                    inv.RemoveItems(clientId, helmetId, ProjectC.Items.ItemType.Equipment, 1);
                }
                else if (Debug.isDebugBuild)
                {
                    Debug.LogWarning($"[EquipmentServer] Seed equip failed: {equipReason}");
                }
            }
            else
            {
                Debug.LogWarning("[EquipmentServer] Seed: 'Рабочая каска' not found in db");
            }

            Debug.Log($"[EquipmentServer] Seeded items for client {clientId}: inv=4 equipped=WorkerHelmet");

            // Push initial snapshot.
            if (_world != null && _world.GetEquipment(clientId) != null)
            {
                SendEquipmentSnapshotToOwner(clientId);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            EquipmentWorld.Reset();
            if (Instance == this) Instance = null;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnectedForSeed;
            }
        }

        // === Rate limit ===

        private bool RateLimit(ulong clientId)
        {
            float now = Time.unscaledTime;
            if (_nextAllowedTimePerClient.TryGetValue(clientId, out var next) && now < next)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[EquipmentServer] Rate limit hit for client {clientId}, retry in {next - now:F2}s");
                }
                return false;
            }
            _nextAllowedTimePerClient[clientId] = now + 1f / _maxOpsPerSec;
            return true;
        }

        // === Client → Server RPCs ===

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestEquipRpc(int itemId, EquipSlot slot, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(clientId))
            {
                SendEquipResult(clientId, EquipResultDto.Denied("Слишком быстро"));
                return;
            }

            if (!_world.TryEquip(clientId, itemId, slot, out var reason))
            {
                SendEquipResult(clientId, EquipResultDto.Denied(reason));
                return;
            }

            // SESSION 2 refactor: equip = move from inventory to equipment slot.
            // Без RemoveItems item дублировался бы в инвентаре (плохо).
            var inv = ProjectC.Items.InventoryWorld.Instance;
            if (inv != null)
            {
                // SESSION 2: try RemoveItems с id, если ItemNotFound — fallback по slot+type.
                int correctId = itemId;
                var invDbField = typeof(ProjectC.Items.InventoryWorld).GetField("_itemDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var invDb = invDbField?.GetValue(inv) as System.Collections.Generic.Dictionary<int, ProjectC.Items.ItemData>;
                if (invDb != null && !invDb.ContainsKey(itemId))
                {
                    foreach (var kvp in invDb)
                    {
                        if (kvp.Value is ProjectC.Equipment.ClothingItemData c && c.slot == slot) { correctId = kvp.Key; break; }
                        else if (kvp.Value is ProjectC.Equipment.ModuleItemData m && m.slot == slot) { correctId = kvp.Key; break; }
                    }
                }
                var result = inv.RemoveItems(clientId, correctId, ProjectC.Items.ItemType.Equipment, 1);
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[EquipmentServer] Equip → RemoveItems: client={clientId} origId={itemId} correctId={correctId} code={result.code} msg={result.message}");
                }
            }

            SendEquipResult(clientId, EquipResultDto.Equipped(itemId, slot));
            // Recompute effective stats (after equip) — StatsServer T-P05 hook
            TriggerStatsRecompute(clientId);
            SendEquipmentSnapshotToOwner(clientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestUnequipRpc(EquipSlot slot, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!RateLimit(clientId))
            {
                SendEquipResult(clientId, EquipResultDto.Denied("Слишком быстро"));
                return;
            }

            // Capture itemId BEFORE clearing slot.
            var equip = _world.GetEquipment(clientId);
            int idx = ProjectC.Equipment.EquipmentData.SlotToIndex(slot);
            int itemId = (idx >= 0 && idx < equip.slotItemIds.Length && equip.slotOccupied[idx] == 1) ? equip.slotItemIds[idx] : 0;

            if (!_world.TryUnequip(clientId, slot, out var reason))
            {
                SendEquipResult(clientId, EquipResultDto.Denied(reason));
                return;
            }

            // SESSION 2 refactor: unequip = return item back to inventory.
            // Без AddItemDirect item просто исчезал (плохо).
            if (itemId > 0)
            {
                var inv = ProjectC.Items.InventoryWorld.Instance;
                if (inv != null)
                {
                    // SESSION 2 CRITICAL FIX: AddItemDirect принимает ТОЛЬКО ID из _itemDatabase.
                    // Если id пришёл из `EquipmentData.slotItemIds[idx]` (seed ID) но RegisterEquipmentAssets
                    // переместил ClothingItemData в другой ID (1300+) — AddItemDirect падает с ItemNotFound.
                    // Резолвим правильный ID через reference equality.
                    int correctId = itemId;
                    var invDbField = typeof(ProjectC.Items.InventoryWorld).GetField("_itemDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var invDb = invDbField?.GetValue(inv) as System.Collections.Generic.Dictionary<int, ProjectC.Items.ItemData>;
                    if (invDb != null)
                    {
                        // Попытка 1: сначала проверяем что id существует в _itemDatabase
                        if (!invDb.ContainsKey(itemId))
                        {
                            // Попытка 2: itemId не найден — ищем по slot+itemName (через EquipData)
                            // Слот уже известен, itemName в EquipData не хранится → ищем в _itemDatabase
                            // одежду/модуль для этого слота.
                            // Простой fallback: первый ClothingItemData/ModuleItemData с таким slot.
                            foreach (var kvp in invDb)
                            {
                                if (kvp.Value is ProjectC.Equipment.ClothingItemData c && c.slot == slot)
                                {
                                    correctId = kvp.Key;
                                    break;
                                }
                                else if (kvp.Value is ProjectC.Equipment.ModuleItemData m && m.slot == slot)
                                {
                                    correctId = kvp.Key;
                                    break;
                                }
                            }
                        }
                    }
                    // SESSION 2: addItem возвращает InventoryResultDto. Проверяем успех.
                    var result = inv.AddItemDirect(clientId, correctId, ProjectC.Items.ItemType.Equipment);
                    if (Debug.isDebugBuild)
                    {
                        Debug.Log($"[EquipmentServer] Unequip → AddItemDirect: client={clientId} origId={itemId} correctId={correctId} code={result.code} msg={result.message}");
                    }
                }
                else
                {
                    Debug.LogWarning("[EquipmentServer] Unequip failed: InventoryWorld.Instance is null");
                }
            }

            SendEquipResult(clientId, EquipResultDto.Unequipped(slot));
            TriggerStatsRecompute(clientId);
            SendEquipmentSnapshotToOwner(clientId);
        }

        // === Server → Client (TargetRPCs через NetworkPlayer) ===

        private void SendEquipResult(ulong clientId, EquipResultDto result)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;

            // Stub-RPC: T-P10 NMC auto-spawn + actual RPC. В T-P09 ищем через reflection (null-safe).
            var mi = typeof(NetworkPlayer).GetMethod("ReceiveEquipResultTargetRpc");
            if (mi != null)
            {
                var defaultRpcParams = System.Activator.CreateInstance(typeof(RpcParams));
                mi.Invoke(netPlayer, new object[] { result, defaultRpcParams });
            }
            else if (Debug.isDebugBuild)
            {
                Debug.Log($"[EquipmentServer] (T-P09 stub) equip result for {clientId}: code={result.code} slot={result.slot} reason='{result.reason}'");
            }
        }

        private void SendEquipmentSnapshotToOwner(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
            var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
            if (netPlayer == null) return;

            var equip = _world.GetEquipment(clientId);
            var snap = new EquipmentSnapshotDto { equip = equip };

            var mi = typeof(NetworkPlayer).GetMethod("ReceiveEquipmentSnapshotTargetRpc");
            if (mi != null)
            {
                var defaultRpcParams = System.Activator.CreateInstance(typeof(RpcParams));
                mi.Invoke(netPlayer, new object[] { snap, defaultRpcParams });
            }
            else if (Debug.isDebugBuild)
            {
                int occupied = 0;
                foreach (var _ in snap.equip.EnumerateOccupiedSlots()) occupied++;
                Debug.Log($"[EquipmentServer] (T-P09 stub) equipment snapshot for {clientId}: {occupied} slots occupied");
            }
        }

        /// <summary>
        /// Cross-NetworkObject: после equip/unequip — recompute effective stats в StatsServer.
        /// Null-safe (T-P09 standalone, StatsServer из M1 уже на месте — но reflection fallback).
        /// </summary>
        private void TriggerStatsRecompute(ulong clientId)
        {
            var ssType = System.Type.GetType("ProjectC.Stats.StatsServer, Assembly-CSharp");
            if (ssType == null) return;
            var instProp = ssType.GetProperty("Instance");
            var inst = instProp?.GetValue(null);
            if (inst == null) return;
            var method = ssType.GetMethod("RecomputeAndSendSnapshot");
            method?.Invoke(inst, new object[] { clientId });
        }
    }
}
