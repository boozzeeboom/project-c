using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.World.FloatingOrigin.Pilot
{
    /// <summary>
    /// Explicit test-only spawn source for the first global pilot. It uses the authored Respawn_Default point,
    /// a single large local frame, and no save/auth identity. It is intentionally not a production persistence source.
    /// T-FO-PERSIST03: мост к legacy-сейву — если в ShipPositions.json есть запись игрока,
    /// начальная точка берётся из неё (со сдвигом в исходный origin), а не Respawn_Default.
    /// Без записи поведение прежнее. inShip-записи используются как есть (корабль
    /// отресторится в matching-точку тем же сейвом); провал проекции во фрейм —
    /// тихий fallback на Respawn_Default, игрок обязан заспавниться.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionPilotSpawnSource : MonoBehaviour, IGlobalMotionPlayerSpawnSource
    {
        [SerializeField] private string _worldScenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
        [SerializeField] private string _respawnObjectName = "Respawn_Default";
        [SerializeField] private float _maxLocalCoordinate = 100000f;
        [SerializeField] private float _verticalSpawnOffset = 1f;

        // T-FO09J: экстент фрейма спавна. Сейв хранит пост-сдвиговые локалы +
        // кумулятив; свежий старт проецирует их в origin-0 мир — при origin в
        // сотни км скорректированная точка (напр. 359439) не влезала в 100000
        // → "outside pilot frame, default spawn" (вечный спавн на точке).
        // 1M покрывает накопленный origin; после спавна автосдвиг сразу
        // переносит origin к игроку (транзиентный джиттер ~3см, секунды).
        // _maxLocalCoordinate остаётся для совместимости/инспектора.
        private const float PilotSpawnFrameExtent = 1000000f;

        // T-FO09F: явный якорь спавна. Позволяет задать Transform из любой
        // открытой сцены (например WorldScene_0_0) прямо в инспекторе
        // NetworkManager в BootstrapScene. Приоритет над _respawnObjectName:
        // если назначен — используется его позиция (+offset), поиск по имени
        // не выполняется. Проверка сцены смягчена: якорь может лежать в любой
        // загруженной сцене, его мировая позиция уже глобальная.
        [SerializeField, Tooltip("T-FO09F: явный якорь спавна из любой сцены. Приоритет над Respawn_Default.")]
        private Transform _spawnAnchor;

        private readonly List<GlobalMotionSpawnFrame> _frames = new List<GlobalMotionSpawnFrame>();
        private GlobalPosition _spawnPosition;
        private bool _prepared;
        // T-FO-PERSIST03: резолв на клиента (файл читается один раз на клиента,
        // TryGetPlayerPlan дёргается каждый кадр до потребления плана).
        private readonly Dictionary<ulong, GlobalPosition> _legacySpawns = new Dictionary<ulong, GlobalPosition>();
        private readonly HashSet<ulong> _legacyMissed = new HashSet<ulong>();

        public IReadOnlyList<GlobalMotionSpawnFrame> PreparedFrames => _frames;

        private void Awake() => Prepare();
        public bool RefreshPreparedContent() => Prepare();
        public bool RefreshPreparedContent(UnityEngine.SceneManagement.Scene preparedScene) => Prepare(preparedScene);

        public bool ValidatePreparedContent(GlobalMotionStartRole role, GlobalMotionNetworkProfile profile, out string error)
        {
            error = null;
            if (!_prepared || _frames.Count != 1 || !_frames[0].IsValid)
            { error = "pilot_frame_not_prepared"; return false; }
            if (profile == null || !profile.EnforceGlobalContracts)
            { error = "pilot_profile_missing_or_disabled"; return false; }
            var manager = GetComponent<NetworkManager>();
            var executor = GetComponent<GlobalSceneNativeExecutor>();
            if (manager == null || manager != NetworkManager.Singleton || executor == null || !executor.isActiveAndEnabled)
            { error = "pilot_manager_or_scene_executor_missing"; return false; }
            return executor.ValidatePreparation(manager, profile, _frames, out error);
        }

        public bool TryGetPlayerPlan(ulong approvedClientId, out GlobalMotionPlayerSpawnPlan plan)
        {
            plan = default;
            if (!_prepared || approvedClientId == ulong.MaxValue) return false;
            var spawn = _spawnPosition;
            if (TryGetLegacySpawn(approvedClientId, out var legacy))
                spawn = legacy;
            plan = new GlobalMotionPlayerSpawnPlan(1, spawn, Quaternion.identity, Vector3.one, null);
            return true;
        }

        /// <summary>
        /// T-FO-PERSIST03: однократный резолв legacy-сейва на клиента.
        /// Скорректированная точка — локальные координаты исходного origin;
        /// стартовый фрейм пилота — тоже исходный origin, проекция обязана сойтись.
        /// Любой провал = тихий fallback на Respawn_Default (план всегда валиден).
        /// </summary>
        private bool TryGetLegacySpawn(ulong clientId, out GlobalPosition position)
        {
            position = default;
            if (_legacySpawns.TryGetValue(clientId, out position)) return true;
            if (_legacyMissed.Contains(clientId)) return false;
            _legacyMissed.Add(clientId);
            Vector3 local;
            try
            {
                if (!ProjectC.Core.ShipPosition.ShipPositionServer.TryLoadCorrectedPlayer(clientId, out local))
                    return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[T-FO-PERSIST03] PilotSpawnLegacy: save read failed: " + e.GetType().Name, this);
                return false;
            }
            var global = GlobalPosition.FromLegacyAbsolute(local);
            Vector3 projected;
            try
            {
                if (!_frames[0].Coordinates.TryToLocal(global, out projected))
                {
                    Debug.LogWarning("[T-FO-PERSIST03] PilotSpawnLegacy: outside pilot frame, default spawn. client=" + clientId, this);
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[T-FO-PERSIST03] PilotSpawnLegacy: projection failed: " + e.GetType().Name, this);
                return false;
            }
            position = global;
            _legacySpawns[clientId] = position;
            Debug.Log("[T-FO-PERSIST03] PilotSpawnRestored: client=" + clientId + " pos=" + local, this);
            return true;
        }

        public bool TryGetReplicaFrame(GlobalMotionSpawnSeed seed, out int localFrameId)
        {
            localFrameId = 0;
            if (!_prepared || !seed.IsValid) return false;
            if (!_frames[0].Coordinates.TryToLocal(seed.Position, out _)) return false;
            localFrameId = _frames[0].Id;
            return true;
        }

        private bool Prepare(UnityEngine.SceneManagement.Scene? preparedScene = null)
        {
            _frames.Clear();
            _prepared = false;

            // T-FO09F: явный якорь из любой загруженной сцены — приоритет над
            // поиском по имени. Мировая позиция якоря уже глобальная, проверка
            // пути worldScene не нужна (якорь может лежать вне WorldScene_0_0).
            if (_spawnAnchor != null)
            {
                _spawnPosition = GlobalPosition.FromLegacyAbsolute(_spawnAnchor.position + Vector3.up * _verticalSpawnOffset);
                var anchorScene = _spawnAnchor.gameObject.scene;
                var anchorFrame = new LocalCoordinateFrame(GlobalPosition.Zero, PilotSpawnFrameExtent);
                _frames.Add(new GlobalMotionSpawnFrame(1, anchorFrame, anchorScene));
                _prepared = _frames[0].IsValid;
                return _prepared;
            }

            var scene = preparedScene ?? SceneManager.GetSceneByPath(_worldScenePath);
            if (!scene.IsValid() || !scene.isLoaded || !string.Equals(scene.path, _worldScenePath, StringComparison.Ordinal)) return false;
            var respawn = FindInScene(scene, _respawnObjectName);
            if (respawn == null) return false;

            _spawnPosition = GlobalPosition.FromLegacyAbsolute(respawn.transform.position + Vector3.up * _verticalSpawnOffset);
            var frame = new LocalCoordinateFrame(GlobalPosition.Zero, PilotSpawnFrameExtent);
            _frames.Add(new GlobalMotionSpawnFrame(1, frame, scene));
            _prepared = _frames[0].IsValid;
            return _prepared;
        }

        private static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == objectName) return root;
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (transform.name == objectName) return transform.gameObject;
            }
            return null;
        }
    }
}
