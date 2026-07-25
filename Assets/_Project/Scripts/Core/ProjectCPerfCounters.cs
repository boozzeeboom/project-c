// ProjectC: Performance Counters — T-PERF-01
// Design: docs/world/admin_tool/perfomance/PERFORMANCE_MONITORING_RESEARCH.md
// Unity 6 (6000.5.2f1): ProfilerCounter<T> удалён из API. Используем ProfilerMarker + статические поля-счётчики.
using Unity.Profiling;

namespace ProjectC.Core
{
    /// <summary>
    /// Центральный реестр ProfilerMarker'ов для всех подсистем ProjectC.
    /// Каждый маркер — static readonly, zero-allocation после инициализации.
    /// Использование: using var _ = ProjectCPerfCounters.NpcBrainUpdate.Auto();
    /// </summary>
    public static class ProjectCPerfCounters
    {
        // ==================== AI ====================
        public static readonly ProfilerMarker NpcBrainUpdate =
            new(ProfilerCategory.Ai, "AI.NpcBrain.Update");
        public static readonly ProfilerMarker NpcBrainFixedUpdate =
            new(ProfilerCategory.Ai, "AI.NpcBrain.FixedUpdate");
        public static readonly ProfilerMarker NpcSocialTick =
            new(ProfilerCategory.Ai, "AI.NpcSocialBrain.Tick");
        public static readonly ProfilerMarker NpcSpawnerTick =
            new(ProfilerCategory.Ai, "AI.NpcSpawner.Tick");

        // Счётчики активных сущностей (read-only для HUD)
        public static int ActiveNpcs;

        // ==================== Ships ====================
        public static readonly ProfilerMarker ShipControllerUpdate =
            new(ProfilerCategory.Scripts, "Ship.Controller.Update");
        public static readonly ProfilerMarker ShipControllerFixedUpdate =
            new(ProfilerCategory.Scripts, "Ship.Controller.FixedUpdate");
        public static readonly ProfilerMarker ShipFuelUpdate =
            new(ProfilerCategory.Scripts, "Ship.FuelSystem.Update");
        public static readonly ProfilerMarker ShipModulesUpdate =
            new(ProfilerCategory.Scripts, "Ship.Modules.Update");

        public static int ActiveShips;

        // ==================== Clouds ====================
        public static readonly ProfilerMarker CloudManagerUpdate =
            new(ProfilerCategory.Scripts, "Clouds.Manager.Update");
        public static readonly ProfilerMarker CloudLayerUpdate =
            new(ProfilerCategory.Scripts, "Clouds.Layer.Update");
        public static readonly ProfilerMarker DistantCloudUpdate =
            new(ProfilerCategory.Scripts, "Clouds.Distant.Update");
        public static readonly ProfilerMarker NearCloudUpdate =
            new(ProfilerCategory.Scripts, "Clouds.Near.Update");

        public static int VisibleClouds;

        // ==================== World Streaming ====================
        public static readonly ProfilerMarker StreamingUpdate =
            new(ProfilerCategory.Scripts, "World.Streaming.Update");
        public static readonly ProfilerMarker ChunkLoadOp =
            new(ProfilerCategory.Scripts, "World.Streaming.ChunkLoad");
        public static readonly ProfilerMarker ChunkUnloadOp =
            new(ProfilerCategory.Scripts, "World.Streaming.ChunkUnload");

        public static int LoadedChunks;

        // ==================== Combat ====================
        public static readonly ProfilerMarker CombatServerTick =
            new(ProfilerCategory.Scripts, "Combat.Server.Tick");
        public static readonly ProfilerMarker TargetLockUpdate =
            new(ProfilerCategory.Scripts, "Combat.TargetLock.Update");
        public static readonly ProfilerMarker CombatClientTick =
            new(ProfilerCategory.Scripts, "Combat.Client.Tick");

        public static int ActiveCombats;

        // ==================== Player ====================
        public static readonly ProfilerMarker PlayerUpdate =
            new(ProfilerCategory.Scripts, "Player.NetworkPlayer.Update");
        public static readonly ProfilerMarker CameraUpdate =
            new(ProfilerCategory.Scripts, "Player.Camera.Update");

        // ==================== Network ====================
        public static int RpcSentPerInterval;
        public static int RpcReceivedPerInterval;
        public static float NetworkRttMs;

        // ==================== Misc ====================
        public static readonly ProfilerMarker CraftingServerTick =
            new(ProfilerCategory.Scripts, "Crafting.Server.Tick");
        public static readonly ProfilerMarker DockingUpdate =
            new(ProfilerCategory.Scripts, "Docking.Update");
        public static readonly ProfilerMarker DayNightUpdate =
            new(ProfilerCategory.Scripts, "DayNight.Controller.Update");
        public static readonly ProfilerMarker WindUpdate =
            new(ProfilerCategory.Scripts, "Wind.Manager.Update");
        public static readonly ProfilerMarker FloatingOriginUpdate =
            new(ProfilerCategory.Scripts, "World.FloatingOrigin.Update");
    }
}
