using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>Baked authoring identity. No runtime GlobalObjectId lookup, parenting or automatic activation.</summary>
    [DisallowMultipleComponent]
    public sealed class GlobalSceneSourceMarker : MonoBehaviour
    {
        [SerializeField] private string _sourceId = "";
        [SerializeField] private int _frameId;
        [SerializeField] private bool _activateWhenReady;
        public string SourceId => _sourceId;
        public int FrameId => _frameId;
        public bool ActivateWhenReady => _activateWhenReady;
    }

    public interface IGlobalMotionSceneAdmission
    {
        bool CanAcceptScenePeer { get; }
    }

    /// <summary>Pure facts of the deliberately limited I executor. Facts are collected from real objects by its binder.</summary>
    public struct GlobalSceneExecutionFacts
    {
        public GlobalSceneTreatment Treatment;
        public bool Spatial, NetworkObject, ActiveSelf, ActiveInHierarchy, ActivateWhenReady, FrameValid, UnsupportedNative;
    }
    public static class GlobalSceneExecutionPolicy
    {
        public static string Validate(GlobalSceneExecutionFacts facts)
        {
            // Reviewed-but-unmanaged sources keep their authored state: no placement, activation, spawn or
            // native subtree restriction applies, so only the "nothing is managed" invariant is checked.
            if (facts.Treatment == GlobalSceneTreatment.Unmanaged)
                return facts.Spatial ? "unmanaged_source_cannot_be_spatial" : null;
            if (facts.Treatment != GlobalSceneTreatment.PreserveContent && facts.Treatment != GlobalSceneTreatment.SceneNetworkObject)
                return "replacement_and_exclusion_need_extended_executor";
            if (facts.NetworkObject != (facts.Treatment == GlobalSceneTreatment.SceneNetworkObject)) return "source_network_kind_mismatch";
            if (facts.UnsupportedNative) return "native_or_writer_bridge_missing";
            if (facts.Spatial && facts.NetworkObject) return "spatial_scene_network_actor_bridge_missing";
            if (facts.Spatial && (!facts.FrameValid || facts.ActiveInHierarchy || !facts.ActivateWhenReady)) return "static_content_must_be_staged_in_explicit_frame";
            // Native startup sweep ignores inactive GameObjects, NOT disabled NetworkObject components.
            if (facts.NetworkObject && (facts.ActiveSelf || facts.ActiveInHierarchy || !facts.ActivateWhenReady)) return "scene_network_source_must_be_self_inactive_before_start";
            return null;
        }
        public static bool CanAcceptPeer(bool isHostLocalPeer, bool nativeSceneReady) => isHostLocalPeer || nativeSceneReady;
        public static bool CanRestorePreparation(bool networkRan, bool stillInactive, bool identityUnchanged) => !networkRan && stillInactive && identityUnchanged;
    }
}
