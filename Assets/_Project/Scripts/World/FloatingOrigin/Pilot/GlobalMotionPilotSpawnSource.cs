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
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionPilotSpawnSource : MonoBehaviour, IGlobalMotionPlayerSpawnSource
    {
        [SerializeField] private string _worldScenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
        [SerializeField] private string _respawnObjectName = "Respawn_Default";
        [SerializeField] private float _maxLocalCoordinate = 100000f;
        [SerializeField] private float _verticalSpawnOffset = 1f;

        private readonly List<GlobalMotionSpawnFrame> _frames = new List<GlobalMotionSpawnFrame>();
        private GlobalPosition _spawnPosition;
        private bool _prepared;

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
            plan = new GlobalMotionPlayerSpawnPlan(1, _spawnPosition, Quaternion.identity, Vector3.one, null);
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
            var scene = preparedScene ?? SceneManager.GetSceneByPath(_worldScenePath);
            if (!scene.IsValid() || !scene.isLoaded || !string.Equals(scene.path, _worldScenePath, StringComparison.Ordinal)) return false;
            var respawn = FindInScene(scene, _respawnObjectName);
            if (respawn == null) return false;

            _spawnPosition = GlobalPosition.FromLegacyAbsolute(respawn.transform.position + Vector3.up * _verticalSpawnOffset);
            var frame = new LocalCoordinateFrame(GlobalPosition.Zero, _maxLocalCoordinate);
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
