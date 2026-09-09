using System;
using ProjectC.World.Scene;
using ProjectC.World.Streaming;

namespace ProjectC.World.FloatingOrigin
{
    /// <summary>
    /// Double-precision content addressing. Does not change the semantics or callers of
    /// legacy SceneID/WorldChunkManager APIs and does not load any scene or chunk.
    /// Negative indices are mathematical results; SceneID.IsValid remains the registry policy.
    /// </summary>
    public static class GlobalGridCoordinates
    {
        public static bool TryGetScene(GlobalPosition position, out SceneID scene)
        {
            scene = default;
            if (!position.IsFinite ||
                !TryGetIndex(position.X, SceneID.SCENE_SIZE, out int x) ||
                !TryGetIndex(position.Z, SceneID.SCENE_SIZE, out int z))
                return false;
            scene = new SceneID(x, z);
            return true;
        }

        public static bool TryGetChunk(GlobalPosition position, out ChunkId chunk)
        {
            chunk = default;
            if (!position.IsFinite ||
                !TryGetIndex(position.X, WorldChunkManager.ChunkSize, out int x) ||
                !TryGetIndex(position.Z, WorldChunkManager.ChunkSize, out int z))
                return false;
            chunk = new ChunkId(x, z);
            return true;
        }

        public static GlobalPosition GetSceneOrigin(SceneID scene)
        {
            return new GlobalPosition((double)scene.GridX * SceneID.SCENE_SIZE, 0d,
                (double)scene.GridZ * SceneID.SCENE_SIZE);
        }

        public static GlobalPosition GetSceneCenter(SceneID scene)
        {
            double size = SceneID.SCENE_SIZE;
            return new GlobalPosition((double)scene.GridX * size + size * 0.5d, 0d,
                (double)scene.GridZ * size + size * 0.5d);
        }

        public static bool TryGetIndex(double coordinate, double cellSize, out int index)
        {
            index = default;
            if (!GlobalPosition.IsFiniteValue(coordinate) ||
                !GlobalPosition.IsFiniteValue(cellSize) || cellSize <= 0d)
                return false;
            double value = Math.Floor(coordinate / cellSize);
            if (!GlobalPosition.IsFiniteValue(value) || value < int.MinValue || value > int.MaxValue)
                return false;
            index = (int)value;
            return true;
        }
    }
}
