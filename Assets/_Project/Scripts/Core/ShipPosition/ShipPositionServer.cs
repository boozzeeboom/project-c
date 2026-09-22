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
//
// PERF (2026-07-26): кэш ShipController'ов вместо FindObjectsByType каждый save-тик.
//   InvalidateShipCache() вызывается извне при спавне/деспавне корабля.
// =====================================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectC.PeacefulShip.Stations;
using ProjectC.Player;
using ProjectC.World.FloatingOrigin.Network;
using ProjectC.World.Parom; // T-PAROM-20: персистенция веток
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
        [SerializeField] private bool debugMode = false;


        private IShipPositionRepository _repo;
        private float _nextSaveTime;
        private bool _restoreCompleted;

        // PERF: кэш ShipController'ов вместо FindObjectsByType каждый save-тик.
        private readonly List<ShipController> _cachedShips = new List<ShipController>();
        private int _cachedShipsFrame;
        private const int SHIP_CACHE_TTL_FRAMES = 300; // 5s при 60fps — совпадает с saveIntervalSec
        private bool _shipCacheDirty = true;

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
            _nextSaveTime = Time.time + saveIntervalSec;

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

        /// <summary>
        /// PERF: инвалидировать кэш кораблей. Вызывается извне при спавне/деспавне корабля.
        /// </summary>
        public void InvalidateShipCache() => _shipCacheDirty = true;

        /// <summary>T-ADM-03: сеттер для AdminLogBus (мастер-mute). Поле и if'ы не трогаем.</summary>
        public void SetDebugMode(bool v) => debugMode = v;

        /// <summary>
        /// PERF: получить список ShipController'ов с кэшированием на ~5 секунд.
        /// </summary>
        private List<ShipController> GetCachedShips()
        {
            if (!_shipCacheDirty && _cachedShipsFrame > 0 && Time.frameCount - _cachedShipsFrame < SHIP_CACHE_TTL_FRAMES)
                return _cachedShips;

            _cachedShips.Clear();
            var allShips = FindObjectsByType<ShipController>();
            foreach (var ship in allShips)
                if (ship != null && ship.IsSpawned)
                    _cachedShips.Add(ship);
            _cachedShipsFrame = Time.frameCount;
            _shipCacheDirty = false;
            return _cachedShips;
        }

        private void Update()
        {
            if (!IsServerSafe()) return;
            if (!_restoreCompleted) return;
            if (Time.time < _nextSaveTime) return;
            _nextSaveTime = Time.time + saveIntervalSec;
            SaveCurrentState();
        }

        // === Restore ===

        /// <summary>
        /// Сбрасывает состояние persistence перед каждым новым host/server-сеансом.
        /// Вызывается до StartHost/StartServer, чтобы новый NetworkPlayer не увидел
        /// DataLoaded/RestoreCompleted от предыдущего сеанса.
        /// </summary>
        public void PrepareForServerStart()
        {
            _restoreCompleted = false;
            _nextSaveTime = float.PositiveInfinity;
            // T-FO-RESET: мир свежий (origin-0), а статика кумулятива могла пережить
            // rehost в том же процессе. Сбросить до любых restore/save, иначе
            // restore/save посчитаются от чужого сдвига. Старое значение в лог —
            // ненулевое означает пойманный rehost-residue.
            var prevOffset = GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset;
            var prevFrame = GlobalMotionControlledRebaseSlice.CumulativeRebaseFrame;
            GlobalMotionControlledRebaseSlice.ResetCumulativeRebase();
            if (prevOffset != UnityEngine.Vector3.zero)
                Debug.LogWarning($"[ShipPositionServer] T-FO-RESET discarded stale cumulative: offset={prevOffset} frame={prevFrame}");
            PlayerPositionServer.Instance?.BeginRestore();
        }

        private void OnServerStarted()
        {
            PrepareForServerStart();
            StartCoroutine(RestoreCoroutine());
        }

        /// <summary>
        /// True после завершения общего restore ships + players.
        /// </summary>
        public bool RestoreCompleted => _restoreCompleted;

        private IEnumerator RestoreCoroutine()
        {
            Debug.Log($"[ShipPositionServer] RestoreCoroutine: waiting {restoreDelaySec}s for scenes to load...");

            yield return new WaitForSeconds(restoreDelaySec);

            Debug.Log("[ShipPositionServer] RestoreCoroutine: loading save...");

            var wrapper = _repo.LoadAllWrapper();

            Debug.Log($"[ShipPositionServer] Loaded: {wrapper.ships?.Count ?? 0} ships, {wrapper.players?.Count ?? 0} players");

            // T-FO-PERSIST01: привести сейв из сдвинутого фрейма к исходному origin
            // до применения (корабли + игроки ниже читают уже скорректированные списки).
            ApplyRebaseCorrection(wrapper);

            if (PlayerPositionServer.Instance != null)
                PlayerPositionServer.Instance.LoadSavedPlayers(wrapper.players);

            // T-PAROM-20: restore веток раньше игроков (те читают кабинку живьём).
            RestoreParoms(wrapper.paroms);

            var savedList = wrapper.ships;
            if (savedList == null || savedList.Count == 0)
            {
                Debug.LogWarning("[ShipPositionServer] No saved ships in file. Skip restore.");
                _restoreCompleted = true;
                _nextSaveTime = Time.time + saveIntervalSec;
                yield break;
            }

            _shipCacheDirty = true;
            var allShips = GetCachedShips();
            Debug.Log($"[ShipPositionServer] Found {allShips.Count} ShipControllers in loaded scenes");
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

            _restoreCompleted = true;
            _nextSaveTime = Time.time + saveIntervalSec;
        }

        /// <summary>
        /// T-FO-PERSIST01: сейв хранит координаты в сдвинутом фрейме (пост-F8),
        /// свежий старт грузит мир в исходном origin. Вычитаем суммарный сдвиг
        /// из позиций до применения, иначе restore кладёт всё в пустоту со
        /// смещением на десятки км, а игрок падает → респавн на спавн.
        /// Ротация не трогается; inShip-игроки резолвятся через exit живого
        /// корабля (он уже скорректирован); padId — ID, не координаты.
        /// </summary>
        private void ApplyRebaseCorrection(ShipPositionListWrapper wrapper)
        {
            if (wrapper == null) return;
            var offset = new Vector3(wrapper.rbx, wrapper.rby, wrapper.rbz);
            if (offset == Vector3.zero) return;
            ShiftWrapperByOffset(wrapper);
            Debug.Log($"[ShipPositionServer] T-FO-PERSIST01 RebaseCorrected: offset={offset} frame={wrapper.rbFrame}");
        }

        /// <summary>
        /// T-FO-PERSIST03: единая точка сдвига сейва в исходный origin.
        /// Используется и restore-путем, и мостом пилота (pilot source читает
        /// файл раньше RestoreCoroutine). Чистая функция над wrapper.
        /// T-FO-RESTORE-FRAME: сейв минус файловый сдвиг даёт origin-0, но живой
        /// мир к моменту restore уже может быть сдвинут (авто-rebase срабатывает
        /// сразу после спавна, restore идёт на ~9.5с). Поэтому после вычитания
        /// файлового сдвига прибавляем накопленный сдвиг ТЕКУЩЕЙ сессии —
        /// иначе restore кладёт корабли/точки в origin-0 координаты живого
        /// сдвинутого фрейма (~80км мимо, всё теряется). До первого rebase
        /// сессии добавка нулевая, поведение прежнее.
        /// </summary>
        public static void ShiftWrapperByOffset(ShipPositionListWrapper wrapper)
        {
            if (wrapper == null) return;
            var offset = new Vector3(wrapper.rbx, wrapper.rby, wrapper.rbz);
            if (offset == Vector3.zero) return;
            if (wrapper.ships != null)
                foreach (var s in wrapper.ships)
                {
                    if (s == null) continue;
                    s.px -= offset.x; s.py -= offset.y; s.pz -= offset.z;
                    s.pxCruise -= offset.x; s.pyCruise -= offset.y; s.pzCruise -= offset.z;
                    s.liftStartY -= offset.y;
                }
            if (wrapper.players != null)
                foreach (var p in wrapper.players)
                {
                    if (p == null) continue;
                    p.px -= offset.x; p.py -= offset.y; p.pz -= offset.z;
                }
            var live = GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset;
            if (live == Vector3.zero) return;
            if (wrapper.ships != null)
                foreach (var s in wrapper.ships)
                {
                    if (s == null) continue;
                    s.px += live.x; s.py += live.y; s.pz += live.z;
                    s.pxCruise += live.x; s.pyCruise += live.y; s.pzCruise += live.z;
                    s.liftStartY += live.y;
                }
            if (wrapper.players != null)
                foreach (var p in wrapper.players)
                {
                    if (p == null) continue;
                    p.px += live.x; p.py += live.y; p.pz += live.z;
                }
            Debug.Log($"[ShipPositionServer] T-FO-RESTORE-FRAME live shift applied: fileOffset={offset} liveCumulative={live}");
        }

        /// <summary>
        /// T-FO-PERSIST03: синхронное чтение скорректированной позиции игрока из файла.
        /// Для моста пилота: placement происходит раньше RestoreCoroutine (~3.5с против
        /// ~9.5с), ждать корутину нельзя. Один маленький JSON на размещение, не тик.
        /// Возвращает false без записи в position, если файла/записи нет.
        /// </summary>
        public static bool TryLoadCorrectedPlayer(ulong clientId, out Vector3 position)
        {
            position = default(Vector3);
            ShipPositionListWrapper wrapper;
            try { wrapper = new JsonShipPositionRepository().LoadAllWrapper(); }
            catch (Exception) { return false; }
            if (wrapper == null || wrapper.players == null) return false;
            ShiftWrapperByOffset(wrapper);
            foreach (var p in wrapper.players)
            {
                if (p == null || p.clientId != clientId) continue;
                if (!float.IsFinite(p.px) || !float.IsFinite(p.py) || !float.IsFinite(p.pz)) return false;
                position = new Vector3(p.px, p.py, p.pz);
                return true;
            }
            return false;
        }

        /// <summary>
        /// T-FO09H: inShip/shipId записи сейва для моста пилота. Pilot placement
        /// ставит игрока по сырым координатам (позиция корабля на момент сейва),
        /// но сам корабль ресторится позже (~3.5с) — игрок падает рядом с пустым
        /// местом. Вызывающий ждёт RestoreCompleted и телепортирует на exit
        /// живого корабля. Сдвиг не нужен — нужен только ID.
        /// </summary>
        public static bool TryLoadPlayerShip(ulong clientId, out string shipPersistentId)
        {
            shipPersistentId = null;
            ShipPositionListWrapper wrapper;
            try { wrapper = new JsonShipPositionRepository().LoadAllWrapper(); }
            catch (Exception) { return false; }
            if (wrapper == null || wrapper.players == null) return false;
            foreach (var p in wrapper.players)
            {
                if (p == null || p.clientId != clientId) continue;
                if (!p.inShip || string.IsNullOrEmpty(p.shipPersistentId)) return false;
                shipPersistentId = p.shipPersistentId;
                return true;
            }
            return false;
        }

        /// <summary>
        /// T-PAROM-20: restore паромных веток. Матчинг по routeId + sceneName
        /// (ветка живёт в конкретной сцене). Нет записи — сцена как есть.
        /// s — скаляр: FO-коррекция не нужна (см. ParomRouteSaveData).
        /// Только сервер; клиенты следуют через NetworkVariable.
        /// </summary>
        private void RestoreParoms(List<ParomRouteSaveData> savedParoms)
        {
            if (savedParoms == null || savedParoms.Count == 0)
            {
                if (debugMode) Debug.Log("[ShipPositionServer] No saved paroms. Skip parom restore.");
                return;
            }
            var routes = FindObjectsByType<ParomRoute>();
            int restored = 0;
            foreach (var route in routes)
            {
                if (route == null || !route.IsSpawned) continue;
                var match = savedParoms.Find(p => p != null && p.routeId == route.RouteId);
                if (match == null)
                {
                    if (debugMode)
                        Debug.Log($"[ShipPositionServer] No save for parom '{route.RouteId}' — keeping scene state");
                    continue;
                }
                if (match.sceneName != route.gameObject.scene.name)
                {
                    Debug.LogWarning($"[ShipPositionServer] Parom '{route.RouteId}' scene mismatch (save={match.sceneName}, live={route.gameObject.scene.name}) — keeping scene state");
                    continue;
                }
                route.ApplyRestoredState(match.s, match.dir, match.station, match.dwelling, match.dwellRemaining);
                restored++;
            }
            Debug.Log($"[ShipPositionServer] Restored {restored}/{savedParoms.Count} parom routes from save");
        }

        private void ApplyRestore(ShipController ship, ShipPositionSaveData data)
        {
            var rb = ship.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = new Vector3(data.px, data.py, data.pz);
                rb.rotation = new Quaternion(data.rx, data.ry, data.rz, data.rw);
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            if (data.isDocked && !ship.IsDocked)
                ship.EnterDocked();
            else if (!data.isDocked && ship.IsDocked)
                ship.ExitDocked();

            if (!data.isNpc && data.isEngineRunning)
            {
                ship.SetEngineRunning(true);
                ship.ApplyPersistenceFreeze();
            }

            if (!data.isNpc) return;

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
    

        private void SaveCurrentState()
        {
            var allShips = GetCachedShips();
            var allData = new List<ShipPositionSaveData>(allShips.Count);

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
                    isEngineRunning = ship.IsEngineRunning,
                    savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

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

            List<PlayerPositionSaveData> playerData = new();
            if (PlayerPositionServer.Instance != null)
            {
                PlayerPositionServer.Instance.CollectPlayers();
                playerData = PlayerPositionServer.Instance.GetPendingPlayers();
            }

            // T-PAROM-20: снапшот веток тем же тиком (s/dir/station/dwelling).
            var paromData = new List<ParomRouteSaveData>();
            var routes = FindObjectsByType<ParomRoute>();
            foreach (var route in routes)
            {
                if (route == null || !route.IsSpawned) continue;
                route.GetServerState(out float s, out int dir, out int station, out bool dwelling, out float dwellRemaining);
                paromData.Add(new ParomRouteSaveData
                {
                    routeId = route.RouteId,
                    sceneName = route.gameObject.scene.name,
                    s = s,
                    dir = dir,
                    station = station,
                    dwelling = dwelling,
                    dwellRemaining = dwellRemaining,
                    savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }

            var wrapper = new ShipPositionListWrapper { ships = allData, players = playerData, paroms = paromData };
            // T-FO-PERSIST01: зафиксировать фрейм сейва (суммарный сдвиг мира).
            // Свежий старт вычтет его обратно (ApplyRebaseCorrection).
            var rebaseOffset = GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset;
            wrapper.rbx = rebaseOffset.x; wrapper.rby = rebaseOffset.y; wrapper.rbz = rebaseOffset.z;
            wrapper.rbFrame = GlobalMotionControlledRebaseSlice.CumulativeRebaseFrame;
            _repo.SaveAll(wrapper);

#if UNITY_EDITOR
            if (debugMode)
                Debug.Log($"[ShipPositionServer] Saved: {allData.Count} ships + {playerData.Count} players");
#endif
        }


        public bool SaveNow()
        {
            if (!IsServerSafe())
            {
                Debug.LogWarning("[ShipPositionServer] SaveNow skipped: server is not active");
                return false;
            }

            if (!_restoreCompleted)
            {
                Debug.LogWarning("[ShipPositionServer] SaveNow skipped: restore is not complete; refusing to overwrite the last valid save");
                return false;
            }

            SaveCurrentState();
            _nextSaveTime = Time.time + saveIntervalSec;
            Debug.Log("[ShipPositionServer] SaveNow: current server state persisted before shutdown");
            return true;
        }
}
}
