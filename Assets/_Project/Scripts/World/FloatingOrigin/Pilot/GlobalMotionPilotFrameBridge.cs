using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.World.FloatingOrigin.Network;

namespace ProjectC.World.FloatingOrigin.Pilot
{
    /// <summary>
    /// One-time pre-NGO XZ coordinate normalization for the narrow static-world pilot.
    /// It preserves the authored scene as one rigid coordinate space under an unmarked runtime anchor;
    /// it does not spawn, activate, retire or individually reposition catalog sources.
    /// Runtime rebasing, independent client origins, navigation handoff and release restoration are outside this pilot.
    /// </summary>
    public static class GlobalMotionPilotInitialFramePlan
    {
        public static bool TryCreate(GlobalPosition authoredRespawn, float maxLocalCoordinate,
            out LocalCoordinateFrame frame, out Vector3 rootTranslation, out string error)
        {
            frame = default;
            rootTranslation = default;
            error = null;
            if (!authoredRespawn.IsFinite || !GlobalPosition.IsFiniteValue(maxLocalCoordinate) || maxLocalCoordinate <= 0f)
            {
                error = "invalid_authored_respawn_or_local_limit";
                return false;
            }

            // Keep global altitude/local Y unchanged until altitude/cloud/shader migration is complete.
            var origin = new GlobalPosition(authoredRespawn.X, 0d, authoredRespawn.Z);
            if (Math.Abs(origin.X) > float.MaxValue || Math.Abs(origin.Z) > float.MaxValue)
            {
                error = "initial_frame_translation_exceeds_unity_float_range";
                return false;
            }

            frame = new LocalCoordinateFrame(origin, maxLocalCoordinate);
            if (!frame.TryToLocal(authoredRespawn, out var localRespawn))
            {
                error = "authored_respawn_outside_initial_frame";
                return false;
            }
            if (Math.Abs(localRespawn.x) > 0.001f || Math.Abs(localRespawn.z) > 0.001f)
            {
                error = "initial_frame_does_not_center_respawn_xz";
                return false;
            }

            rootTranslation = new Vector3((float)-origin.X, 0f, (float)-origin.Z);
            return true;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GlobalMotionPilotFrameBridge : MonoBehaviour
    {
        private readonly List<Transform> _translatedRoots = new List<Transform>();
        private Transform _anchor;
        private UnityEngine.SceneManagement.Scene _scene;
        private LocalCoordinateFrame _frame;
        private bool _prepared;

        public bool IsPrepared => _prepared && _anchor != null && _scene.IsValid() && _scene.isLoaded && _frame.IsValid;
        public LocalCoordinateFrame Frame => _frame;

        /// <summary>
        /// Establishes one common XZ-local frame for every existing root in the reviewed world scene.
        /// It may only run before NGO begins; every root must already be represented by a baked marker.
        /// </summary>
        public bool TryPrepare(UnityEngine.SceneManagement.Scene scene, string respawnObjectName, float maxLocalCoordinate, out string error)
        {
            error = null;
            if (_prepared)
            {
                if (IsPrepared && _scene == scene) return true;
                error = "initial_frame_bridge_state_invalid";
                return false;
            }
            if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(respawnObjectName))
            {
                error = "loaded_world_scene_and_respawn_required";
                return false;
            }

            var manager = GetComponent<Unity.Netcode.NetworkManager>();
            if (manager == null || manager.IsListening || manager.ShutdownInProgress)
            {
                error = "network_must_be_stopped_before_initial_frame_placement";
                return false;
            }

            var respawn = FindInScene(scene, respawnObjectName);
            if (respawn == null)
            {
                error = "authored_respawn_not_found:" + respawnObjectName;
                return false;
            }
            var authoredRespawn = GlobalPosition.FromLegacyAbsolute(respawn.transform.position);
            if (!GlobalMotionPilotInitialFramePlan.TryCreate(authoredRespawn, maxLocalCoordinate, out var frame, out var translation, out error))
                return false;

            var roots = scene.GetRootGameObjects();
            if (roots == null || roots.Length == 0)
            {
                error = "world_scene_has_no_roots";
                return false;
            }
            foreach (var root in roots)
            {
                if (root == null || root.GetComponentsInChildren<GlobalSceneSourceMarker>(true).Length == 0)
                {
                    error = "uncataloged_world_root_blocks_initial_frame_placement:" + (root == null ? "<null>" : root.name);
                    return false;
                }
            }

            try
            {
                var anchorObject = new GameObject("[T-FO06L] Initial Frame Root");
                anchorObject.hideFlags = HideFlags.DontSave;
                SceneManager.MoveGameObjectToScene(anchorObject, scene);
                _anchor = anchorObject.transform;
                _anchor.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _anchor.localScale = Vector3.one;

                foreach (var root in roots)
                {
                    root.transform.SetParent(_anchor, true);
                    _translatedRoots.Add(root.transform);
                }
                _anchor.position = translation;
                Physics.SyncTransforms();

                _scene = scene;
                _frame = frame;
                _prepared = true;
                Debug.Log("[T-FO06L] Initial world frame prepared: roots=" + _translatedRoots.Count + ";origin=" + frame.Origin + ";translation=" + translation, this);
                return true;
            }
            catch (Exception exception)
            {
                RestoreAfterFailedPreparation();
                error = "initial_frame_placement_failed:" + exception.GetType().Name;
                Debug.LogException(exception, this);
                return false;
            }
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

        private void RestoreAfterFailedPreparation()
        {
            if (_anchor != null) _anchor.position = Vector3.zero;
            for (int i = _translatedRoots.Count - 1; i >= 0; i--)
                if (_translatedRoots[i] != null && _translatedRoots[i].parent == _anchor)
                    _translatedRoots[i].SetParent(null, true);
            _translatedRoots.Clear();
            if (_anchor != null) Destroy(_anchor.gameObject);
            _anchor = null;
            _scene = default;
            _frame = default;
            _prepared = false;
            Physics.SyncTransforms();
        }
    }
}
