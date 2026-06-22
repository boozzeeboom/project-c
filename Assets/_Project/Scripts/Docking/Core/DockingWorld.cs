// T-DOCK-00: DockingWorld — server-only state machine для стыковочных портов.
// Singleton MonoBehaviour (DontDestroyOnLoad), создаётся из DockingServer.OnNetworkSpawn.
// Паттерн: see Assets/_Project/Quests/Core/QuestWorld.cs (CreateAndInitialize pattern).
//
// Q3 (принято 2026-06-19): Сервер — single source of truth занятости pads.
// Q7 (принято 2026-06-19): Двусторонняя связь — pending assignment (ждёт подтверждения игрока).

using System.Collections.Generic;
using ProjectC.Docking.Dto;
using ProjectC.Docking.Network; // DockStationController
using ProjectC.Docking.Stations; // DockingPadTriggerBox
using ProjectC.Player;
using Unity.Netcode;  // T-NS03: NetworkManager для ReleaseNpcAssignmentStub
using UnityEngine;
// NetworkingUtils живёт в ProjectC.Trade.Network — см. MarketTimeService.cs:157.
// Используем fully-qualified name ниже чтобы не пачкать using-секцию.

namespace ProjectC.Docking.Core
{
    public class DockingWorld : MonoBehaviour
    {
        public static DockingWorld Instance { get; private set; }

        // === server-only state (Q3: SOT) ===
        // Occupant = кто занимает pad. ulong = clientId для игрока, или NPC ID (Phase 2).
        private readonly Dictionary<string, ulong> _occupiedPads = new Dictionary<string, ulong>();

        // Pending assignments: ждут RequestConfirmAssignmentRpc от клиента
        private readonly Dictionary<ulong, ActiveAssignment> _pendingByClient = new Dictionary<ulong, ActiveAssignment>();
        private readonly Dictionary<ulong, ActiveAssignment> _pendingByShip = new Dictionary<ulong, ActiveAssignment>();

        // Confirmed assignments: окно посадки идёт
        private readonly Dictionary<ulong, ActiveAssignment> _assignmentsByClient = new Dictionary<ulong, ActiveAssignment>();
        private readonly Dictionary<ulong, ActiveAssignment> _assignmentsByShip = new Dictionary<ulong, ActiveAssignment>();

        // Q3 prep: struct ActiveAssignment — public для возврата из GetAssignment.
        public struct ActiveAssignment
        {
            public string stationId;
            public string padId;
            public ulong shipNetId;
            public ulong clientId;       // для NPC (Phase 2): clientId = NpcInstanceId
            public float assignedAt;
            public float landingWindowSec;
            public bool used;             // уже приземлился
        }

        // === Lifecycle ===

        public static void CreateAndInitialize()
        {
            if (Instance != null) return;
            var go = new GameObject("[DockingWorld]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DockingWorld>();
            Debug.Log("[DockingWorld] Created");
        }

        private void Start()
        {
            // T-DOCK-SRV-6: сканируем пады при старте — корабли, уже стоящие на падах,
            // регистрируются как занятые + получают EnterDocked (блокировка двигателя).
            // Задержка 1 секунда — чтобы все станции успели зарегистрироваться в
            // DockingZoneRegistry через OnEnable.
            StartCoroutine(ScanExistingOccupantsDelayed());
        }

        private System.Collections.IEnumerator ScanExistingOccupantsDelayed()
        {
            yield return new UnityEngine.WaitForSeconds(1f);
            ScanExistingOccupants();
        }

        /// <summary>
        /// T-DOCK-SRV-6: сканирует все зарегистрированные станции, находит корабли
        /// внутри DockingPadTriggerBox и регистрирует их как occupants.
        /// </summary>
        public void ScanExistingOccupants()
        {
            var stations = DockingZoneRegistry.All;
            if (stations == null || stations.Count == 0)
            {
                Debug.Log("[DockingWorld] ScanExistingOccupants: no stations registered");
                return;
            }
            int totalOccupied = 0;
            foreach (var kv in stations)
            {
                var station = kv.Value;
                if (station == null || station.StationDefinition == null) continue;
                var triggerBoxes = station.GetComponentsInChildren<DockingPadTriggerBox>();
                foreach (var tb in triggerBoxes)
                {
                    Vector3 padWorldPos = tb.transform.position;
                    Vector3 boxSize = Vector3.one * 10f; // fallback
                    var box = tb.GetComponent<BoxCollider>();
                    if (box != null) boxSize = box.size;

                    Collider[] hits = Physics.OverlapBox(padWorldPos, boxSize * 0.5f, tb.transform.rotation, ~0, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var ship = hits[i].GetComponentInParent<ShipController>();
                        if (ship != null)
                        {
                            string padKey = PadKey(station.StationId, tb.PadId);
                            if (!_occupiedPads.ContainsKey(padKey))
                            {
                                _occupiedPads[padKey] = ship.NetworkObject != null ? ship.NetworkObject.NetworkObjectId : 0;
                                // Блокируем двигатель
                                if (ship.IsServer) ship.EnterDocked();
                                Debug.Log($"[DockingWorld] ScanExistingOccupants: pad={tb.PadId} station={station.StationId} ship={ship.name} — registered + engine locked");
                                totalOccupied++;
                            }
                        }
                    }
                }
            }
            Debug.Log($"[DockingWorld] ScanExistingOccupants: completed, {totalOccupied} pads occupied");
        }

        public static void Shutdown()
        {
            if (Instance != null) Destroy(Instance.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // === Public API для DockingServer (T-DOCK-01) ===

        /// <summary>
        /// Назначить свободный pad. НЕ регистрирует сразу — только в pending (Q7).
        /// </summary>
        public DockingAssignmentDto AssignPad(
            DockStationController station,
            ShipController ship,
            ShipFlightClass shipClass)
        {
            var def = station.StationDefinition;
            if (def == null) return MakeFail("STATION_NO_DEFINITION", ship.NetworkObjectId);
            if (def.PadLayout == null) return MakeFail("NO_PAD_LAYOUT", ship.NetworkObjectId);

            // Q4: без хардкода — берём все pads из layout, проверяем каждый
            var defaultTriggerSize = def.PadLayout.DefaultTriggerBoxSize;

            // T-DOCK-SRV-5: собираем override`ы из scene-placed DockingPadTriggerBox
            // (пользователь может настроить compatibleShipClasses прямо на триггер-боксе,
            // а не только в PadLayout SO).
            var triggerBoxOverrides = new Dictionary<string, ShipFlightClass[]>();
            var triggerBoxes = station.GetComponentsInChildren<DockingPadTriggerBox>();
            foreach (var tb in triggerBoxes)
            {
                if (tb.CompatibleShipClasses != null && tb.CompatibleShipClasses.Length > 0)
                    triggerBoxOverrides[tb.PadId] = tb.CompatibleShipClasses;
            }

            Debug.Log($"[DockingWorld] AssignPad: station={def.StationId} ship={ship.name} shipClass={shipClass} padsCount={def.PadLayout.Pads.Count}");
            foreach (var pad in def.PadLayout.Pads)
            {
                // T-DOCK-SRV-5: используем override из триггер-бокса, если есть, иначе из SO
                ShipFlightClass[] effectiveCompatible = triggerBoxOverrides.Count > 0 && triggerBoxOverrides.TryGetValue(pad.padId, out var ovr)
                    ? ovr : pad.compatibleShipClasses;

                string clsStr = "";
                if (effectiveCompatible != null)
                    foreach (var c in effectiveCompatible) clsStr += c.ToString() + ",";
                Debug.Log($"[DockingWorld]  checking pad={pad.padId} effectiveCompatible=[{clsStr}] shipClass={shipClass} " +
                    $"compatible={IsCompatible(effectiveCompatible, shipClass)}");

                if (!IsCompatible(effectiveCompatible, shipClass)) continue;
                string padKey = PadKey(def.StationId, pad.padId);
                if (_occupiedPads.ContainsKey(padKey)) continue;   // уже занят (SOT check)
                if (IsPending(def.StationId, pad.padId)) continue; // ждёт подтверждения

                // T-DOCK-SRV-3: физическая проверка — есть ли корабль внутри trigger зоны пада.
                // Если да — пад считается занятым даже без формального Assign/Confirm.
                // Важно для стартового состояния, когда на падах уже стоят npc-корабли.
                Vector3 worldPadPos = station.transform.TransformPoint(pad.localPosition);
                Quaternion worldPadRot = station.transform.rotation * Quaternion.Euler(pad.localEulerAngles);
                Vector3 boxSize = pad.triggerBoxSize.sqrMagnitude > 0.001f ? pad.triggerBoxSize : defaultTriggerSize;
                Collider[] hits = Physics.OverlapBox(worldPadPos, boxSize * 0.5f, worldPadRot, ~0, QueryTriggerInteraction.Collide);
                bool physicallyOccupied = false;
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].GetComponentInParent<ShipController>() != null)
                    {
                        physicallyOccupied = true;
                        Debug.Log($"[DockingWorld] Pad {pad.padId} physically occupied by ship inside trigger — skipping");
                        break;
                    }
                }
                if (physicallyOccupied) continue;

                // OK — назначаем (но не регистрируем)
                return new DockingAssignmentDto
                {
                    stationId = def.StationId,
                    padId = pad.padId,
                    approachPoint = worldPadPos,
                    approachAltitude = station.transform.position.y + 30f,
                    approachHeading = station.transform.eulerAngles.y + pad.localEulerAngles.y,
                    landingWindowSeconds = def.LandingWindowSeconds,
                    voiceLine = def.VoiceLines != null
                        ? def.VoiceLines.GetRandomLine("Assigning")
                        : "",
                    shipNetworkObjectId = ship.NetworkObjectId,
                    success = true
                };
            }
            return MakeFail("NO_SUITABLE_PAD", ship.NetworkObjectId);
        }

        /// <summary>
        /// Q7: после RequestDockingRpc клиент получает assignment, шлёт
        /// RequestConfirmAssignmentRpc(accept=true) → ConfirmAssignment.
        /// </summary>
        public void RegisterPendingAssignment(ulong clientId, ulong shipNetId, DockingAssignmentDto assignment)
        {
            if (!assignment.success) return;
            var a = new ActiveAssignment
            {
                stationId = assignment.stationId,
                padId = assignment.padId,
                shipNetId = shipNetId,
                clientId = clientId,
                assignedAt = Time.time,
                landingWindowSec = assignment.landingWindowSeconds,
                used = false
            };
            _pendingByClient[clientId] = a;
            _pendingByShip[shipNetId] = a;
            // Pending assignment не занимает pad — другой игрок может запросить этот же
            // pad, если pending истечёт. После Confirm — pad блокируется.
        }

        public void ConfirmAssignment(ulong clientId, ulong shipNetId)
        {
            if (!_pendingByClient.TryGetValue(clientId, out var a)) return;
            if (a.shipNetId != shipNetId) return;
            string padKey = PadKey(a.stationId, a.padId);
            // Q3: занимаем pad (SOT)
            _occupiedPads[padKey] = clientId;
            _assignmentsByClient[clientId] = a;
            _assignmentsByShip[shipNetId] = a;
            // Удаляем из pending
            _pendingByClient.Remove(clientId);
            _pendingByShip.Remove(shipNetId);
        }

        public void CancelPendingAssignment(ulong clientId, ulong shipNetId)
        {
            _pendingByClient.Remove(clientId);
            _pendingByShip.Remove(shipNetId);
        }

        public ActiveAssignment? GetAssignment(ulong clientId, ulong shipNetId)
        {
            if (_assignmentsByClient.TryGetValue(clientId, out var a)) return a;
            return null;
        }

        public DockingStatusDto ConfirmTouchdown(ulong clientId, ulong shipNetId, string padId, string stationId)
        {
            // T-DOCK-SRV-1: если у этого корабля нет confirmed assignment — игрок
            // коснулся pad'а БЕЗ предварительного запроса (подлетел, не делал T).
            // Это НЕ WrongPad (на диздоку WrongPad = assignment есть, но коснулся
            // другого pad'а). Здесь — "вы ещё не запросили стыковку".
            // Возвращаем Idle чтобы UI не показывал toast "перепаркуйтесь".
            if (!_assignmentsByShip.TryGetValue(shipNetId, out var a) || a.clientId != clientId)
            {
                // T-DOCK-SRV-1: нет assignment → игрок ещё не делал RequestDocking.
                // Возвращаем Idle (НЕ WrongPad) — см. AUDIT_AND_REFACTOR.md §1.6.
                return MakeStatus(DockingStatus.Idle, stationId, padId);
            }
            // Есть assignment, но коснулся чужого pad'а
            if (a.padId != padId)
            {
                return MakeStatus(DockingStatus.WrongPad, stationId, padId);
            }
            a.used = true;
            _assignmentsByShip[shipNetId] = a;
            _assignmentsByClient[clientId] = a;
            return MakeStatus(DockingStatus.Docked, stationId, padId);
        }

        public void ReleaseAssignment(ulong clientId, ulong shipNetId)
        {
            if (_assignmentsByClient.TryGetValue(clientId, out var a))
            {
                string padKey = PadKey(a.stationId, a.padId);
                _occupiedPads.Remove(padKey);  // Q3: освобождаем pad
                _assignmentsByClient.Remove(clientId);
                _assignmentsByShip.Remove(shipNetId);
            }
            else if (shipNetId != 0)
            {
                // T-DOCK-SRV-6: если assignment нет (корабль был зарегистрирован через
                // ScanExistingOccupants, а не через формальное Assign+Confirm), ищем
                // занятый pad по ship сетевому идентификатору.
                foreach (var kv in _occupiedPads)
                {
                    if (kv.Value == shipNetId)
                    {
                        _occupiedPads.Remove(kv.Key);
                        break;
                    }
                }
            }
            // Также чистим pending (если был)
            _pendingByClient.Remove(clientId);
            _pendingByShip.Remove(shipNetId);
        }

        public bool IsPadOccupied(string stationId, string padId)
        {
            return _occupiedPads.ContainsKey(PadKey(stationId, padId));
        }

        public bool IsPending(string stationId, string padId)
        {
            string padKey = PadKey(stationId, padId);
            foreach (var kv in _pendingByClient)
            {
                if (PadKey(kv.Value.stationId, kv.Value.padId) == padKey) return true;
            }
            return false;
        }

        // === Helpers ===

        private bool IsCompatible(ShipFlightClass[] allowed, ShipFlightClass shipClass)
        {
            if (allowed == null || allowed.Length == 0) return true;  // пустой = для всех
            foreach (var s in allowed) if (s == shipClass) return true;
            return false;
        }

        private static string PadKey(string stationId, string padId) => $"{stationId}/{padId}";

        private static DockingAssignmentDto MakeFail(string reason, ulong shipNetId) =>
            new DockingAssignmentDto
            {
                success = false,
                failReason = reason,
                shipNetworkObjectId = shipNetId
            };

        private static DockingStatusDto MakeStatus(DockingStatus status, string stationId, string padId) =>
            new DockingStatusDto
            {
                status = status,
                stationId = stationId,
                padId = padId,
                timestamp = Time.time
            };

        // === Update: expiration check ===

        private void Update()
        {
            // NetworkingUtils lives in ProjectC.Trade.Network — fully qualified.
            if (!ProjectC.Trade.Network.NetworkingUtils.IsServerSafe()) return;

            // Истечение pending assignment (клиент не подтвердил за 30 сек)
            var expiredPending = new List<ulong>();
            foreach (var kv in _pendingByClient)
            {
                if (Time.time - kv.Value.assignedAt > 30f)
                {
                    expiredPending.Add(kv.Key);
                }
            }
            foreach (var cId in expiredPending)
            {
                if (_pendingByClient.TryGetValue(cId, out var a))
                {
                    CancelPendingAssignment(cId, a.shipNetId);
                    // T-DOCK-01 sends status to client. Skip RPC here — DockingServer
                    // will be wired in T-DOCK-01; placeholder log for now.
                    Debug.Log($"[DockingWorld] pending expired for client {cId}");
                }
            }

            // Истечение окна посадки (confirmed, но не приземлился)
            var expiredAssigned = new List<ulong>();
            foreach (var kv in _assignmentsByClient)
            {
                if (Time.time - kv.Value.assignedAt > kv.Value.landingWindowSec && !kv.Value.used)
                {
                    expiredAssigned.Add(kv.Key);
                }
            }
            foreach (var cId in expiredAssigned)
            {
                if (_assignmentsByClient.TryGetValue(cId, out var a))
                {
                    ReleaseAssignment(cId, a.shipNetId);
                    Debug.Log($"[DockingWorld] assigned window expired for client {cId}");
                }
            }
        }

        // === Q3: Pad occupancy snapshot (для клиента при необходимости) ===

        public struct PadStatusInfo
        {
            public string padId;
            public bool isOccupied;
            public bool isPending;
        }

        public List<PadStatusInfo> GetPadStatusSnapshot(
            string stationId,
            DockStationController station)
        {
            var result = new List<PadStatusInfo>();
            if (station == null || station.StationDefinition == null
                || station.StationDefinition.PadLayout == null) return result;
            foreach (var pad in station.StationDefinition.PadLayout.Pads)
            {
                result.Add(new PadStatusInfo
                {
                    padId = pad.padId,
                    isOccupied = IsPadOccupied(stationId, pad.padId),
                    isPending = IsPending(stationId, pad.padId)
                });
            }
            return result;
        }

        // === T-NS05: NPC ship integration ===
        // Полная реализация AssignPadForNpc + ReleaseNpcAssignment.

        /// <summary>
        /// T-NS05: Проверить, является ли ulong id NPC-идентификатором (sentinel bit = 1).
        /// Q3: NpcInstanceId = NetworkObjectId | 0x8000_0000_0000_0000UL.
        /// </summary>
        private static bool IsNpcInstanceId(ulong id)
        {
            return id > 0x7FFF_FFFF_FFFF_FFFFUL;
        }

        /// <summary>
        /// T-NS05: Подсчитать количество текущих посадок (pending + confirmed) на станции.
        /// Используется для проверки maxConcurrentLandings (Q6).
        /// </summary>
        private int CountLandingsAtStation(string stationId)
        {
            int count = 0;
            foreach (var kv in _assignmentsByClient)
            {
                if (kv.Value.stationId == stationId) count++;
            }
            foreach (var kv in _pendingByClient)
            {
                if (kv.Value.stationId == stationId) count++;
            }
            return count;
        }

        /// <summary>
        /// T-NS05: server-internal путь для NPC-кораблей.
        /// Обходит RPC, валидирует maxConcurrentLandings (Q6), назначает pad + подтверждает в один шаг.
        /// Возвращает true если pad назначен и корабль застыкован.
        /// </summary>
        public bool AssignPadForNpc(
            DockStationController station,
            ShipController ship,
            ShipFlightClass shipClass,
            ulong npcInstanceId)
        {
            if (!ProjectC.Trade.Network.NetworkingUtils.IsServerSafe()) return false;
            if (station == null || ship == null || npcInstanceId == 0) return false;

            // Q6: проверка maxConcurrentLandings
            var stationDef = station.StationDefinition;
            if (stationDef != null)
            {
                int currentLandings = CountLandingsAtStation(stationDef.StationId);
                if (currentLandings >= stationDef.MaxConcurrentLandings)
                {
                    return false; // станция полна
                }
            }

            // Проверка: уже есть assignment?
            if (_assignmentsByClient.ContainsKey(npcInstanceId))
            {
                return false; // уже назначен
            }

            var assignment = AssignPad(station, ship, shipClass);
            if (!assignment.success) return false;

            // RegisterPending + Confirm в один шаг (NPC не имеет QA-стадии)
            RegisterPendingAssignment(npcInstanceId, ship.NetworkObjectId, assignment);
            ConfirmAssignment(npcInstanceId, ship.NetworkObjectId);
            if (ship.IsServer) ship.EnterDocked();

            return true;
        }

        /// <summary>
        /// T-NS05: освободить assignment NPC (Release + ExitDocked).
        /// </summary>
        public void ReleaseNpcAssignment(ulong npcInstanceId, ulong shipNetId)
        {
            if (!ProjectC.Trade.Network.NetworkingUtils.IsServerSafe()) return;
            ReleaseAssignment(npcInstanceId, shipNetId);
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.SpawnManager == null) return;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(shipNetId, out var no)) return;
            var ship = no.GetComponent<ShipController>();
            if (ship != null && ship.IsServer) ship.ExitDocked();
        }
    }
}
