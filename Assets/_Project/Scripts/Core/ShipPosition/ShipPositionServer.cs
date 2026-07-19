// =====================================================================================
// ShipPositionServer.cs — periodic save + restore позиций кораблей (T-PERSIST-SERVER)
// =====================================================================================
// Документация:
//   • docs/Ships/SHIP_POSITION_PERSISTENCE_FINAL.md §5.4
//
// Server-only singleton. DontDestroyOnLoad.
// Save: каждые saveIntervalSec (5s) собирает позиции всех ShipController → JSON.
// Restore: через 3.5s после OnServerStarted матчит по ShipPersistentId → ApplyRestore.
//
// Жизненный цикл:
//   Server start → ScenePlacedObjectSpawner → NpcShipServer (2s) → Restore (3.5s)
//   Update → Save каждые 5s
// =====================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectC.PeacefulShip.Stations;
using ProjectC.Player;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Core.ShipPosition
{
    /// <summary>
    /// Server-only persistence для позиций всех кораблей (player + NPC).
    /// Сохраняет каждые 5 сек в ShipPositions.json.
    /// Восстанавливает при старте сервера (с задержкой 3.5s).
    /// </summary>
    public class ShipPositionServer : MonoBehaviour
    {
        public static ShipPositionServer Instance { get; private set; }

        [Header("Save")]
        [SerializeField] private float saveIntervalSec = 5f;

        [Header("Restore")]
        [Tooltip("Задержка перед restore после старта сервера (даём ScenePlacedObjectSpawner + DiscoverNpcShipsDelayed).")]
        [SerializeField] private float restoreDelaySec = 3.5f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        private IShipPositionRepository _repo;
        private float _nextSaveTime;

        // === Lifecycle ===

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _repo = new JsonShipPositionRepository();
        }

        private void Start()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            if (Instance == this) Instance = null;
        }

        // === Save ===

        private void Update()
        {
            if (!IsServerSafe()) return;
            if (Time.time < _nextSaveTime) return;
            _nextSaveTime = Time.time + saveIntervalSec;

            var allShips = FindObjectsByType<ShipController>(FindObjectsSortMode.None);
            var allData = new List<ShipPositionSaveData>(allShips.Length);

            foreach (var ship in allShips)
            {
                if (!ship.IsSpawned) continue;

                var data = new ShipPositionSaveData
                {
                    shipId = ship.ShipPersistentId,
                    sceneName = ship.gameObject.scene.name,
                    isNpc = false,
                    px = ship.transform.position.x,
                    py = ship.transform.position.y,
                    pz = ship.transform.position.z,
                    rx = ship.transform.rotation.x,
                    ry = ship.transform.rotation.y,
                    rz = ship.transform.rotation.z,
                    rw = ship.transform.rotation.w,
                    isDocked = ship.IsDocked,
                    savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                // NPC-specific: дополняем из NpcShipController
                var npc = ship.GetComponent<NpcShipController>();
                if (npc != null)
                {
                    data.isNpc = true;
                    data.navMode = (int)npc.CurrentMode;
                    data.dwellTime = npc.DwellTime;
                    data.dockedSinceTimeOffset = (npc.CurrentMode == NpcShipController.NavMode.Docked
                        && npc.DockedSinceTime > 0)
                        ? Time.time - npc.DockedSinceTime
                        : 0f;
                    data.scheduleAdvancedAfterDock = npc.ScheduleAdvancedAfterDock;
                    data.cargoTradeDone = npc.CargoTradeDone;
                    data.assignedPadId = npc.AssignedPadId ?? "";
                    data.pxCruise = npc.CruiseTargetPos.x;
                    data.pyCruise = npc.CruiseTargetPos.y;
                    data.pzCruise = npc.CruiseTargetPos.z;
                    data.liftStartY = npc.LiftStartY;

                    var state = PeacefulShip.Core.NpcShipWorld.Instance?.GetNpc(npc.NpcInstanceId);
                    if (state != null)
                    {
                        data.scheduleIndex = state.ScheduleIndex;
                        data.fromLocationId = state.CurrentRoute.fromLocationId ?? "";
                        data.toLocationId = state.CurrentRoute.toLocationId ?? "";
                    }
                }

                allData.Add(data);
            }

            _repo.SaveAll(allData);

            if (debugMode)
                Debug.Log($"[ShipPositionServer] Saved {allData.Count} ships");
        }

        // === Restore ===

        private void OnServerStarted()
        {
            StartCoroutine(RestoreCoroutine());
        }

        private IEnumerator RestoreCoroutine()
        {
            // Ждём ScenePlacedObjectSpawner + DiscoverNpcShipsDelayed (2s) + запас
            yield return new WaitForSeconds(restoreDelaySec);

            var savedList = _repo.LoadAll();
            if (savedList.Count == 0)
            {
                Debug.Log("[ShipPositionServer] No saved positions. Skip restore.");
                yield break;
            }

            var allShips = FindObjectsByType<ShipController>(FindObjectsSortMode.None);
            int restored = 0;

            foreach (var ship in allShips)
            {
                if (!ship.IsSpawned) continue;

                var match = savedList.Find(s => s.shipId == ship.ShipPersistentId);
                if (match == null)
                {
                    if (debugMode)
                        Debug.Log($"[ShipPositionServer] No save for {ship.ShipPersistentId} — keeping scene position");
                    continue;
                }

                ApplyRestore(ship, match);
                restored++;
            }

            Debug.Log($"[ShipPositionServer] Restored {restored}/{savedList.Count} ships from save");
        }

        private void ApplyRestore(ShipController ship, ShipPositionSaveData data)
        {
            var rb = ship.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = new Vector3(data.px, data.py, data.pz);
                rb.rotation = new Quaternion(data.rx, data.ry, data.rz, data.rw);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // restore docking state
            if (data.isDocked && !ship.IsDocked)
                ship.EnterDocked();
            else if (!data.isDocked && ship.IsDocked)
                ship.ExitDocked();

            if (!data.isNpc) return; // всё, player ship готов

            var npc = ship.GetComponent<NpcShipController>();
            if (npc != null)
                npc.RestoreFromSave(data);
        }

        // === Helpers ===

        private static bool IsServerSafe()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer;
        }
    }
}
