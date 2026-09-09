namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>Pure parent identity and conservative native-driver policy. Does not touch a Unity hierarchy.</summary>
    public static class GlobalMotionHierarchy
    {
        public static bool ParentMatches(MotionStreamBinding child, MotionStreamBinding parent,
            bool parentReady, bool sameFrame, bool sameScene, bool wouldCycle)
        {
            return child.IsValid && child.Space == MotionCoordinateSpace.ParentLocal && parent.IsValid &&
                parentReady && sameFrame && sameScene && !wouldCycle && child.NetworkObjectId != parent.NetworkObjectId &&
                child.SessionId == parent.SessionId && child.ParentNetworkObjectId == parent.NetworkObjectId &&
                child.ParentSpawnGeneration == parent.SpawnGeneration;
        }

        public static bool CanChangeHierarchy(MotionPoseRole role, bool customSyncConfigured,
            bool hasBodyOrJoint, bool agentEnabled, bool hasUnsupportedCollider)
        {
            return (role == MotionPoseRole.Authority || role == MotionPoseRole.ServerReplica || role == MotionPoseRole.ClientReplica) &&
                customSyncConfigured && !hasBodyOrJoint && !agentEnabled && !hasUnsupportedCollider;
        }

        public static bool CanUseParentPose(MotionPoseRole role, bool isServer, bool interpolatedParentBody)
        {
            return GlobalMotionApplication.CanUseParentTransform(role, interpolatedParentBody) &&
                !(isServer && interpolatedParentBody);
        }
    }
}
