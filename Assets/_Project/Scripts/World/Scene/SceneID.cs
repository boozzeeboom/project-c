using System;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.Scene
{
    /// <summary>
    /// Идентификатор сцены в grid-системе мира (80,000 x 80,000 units per scene).
    /// Верхнеуровневая система поверх chunk layer (2,000 x 2,000).
    /// </summary>
    [Serializable]
    public struct SceneID : IEquatable<SceneID>, INetworkSerializable
    {
        public int GridX;
        public int GridZ;

        public const float SCENE_SIZE = 79999f;
        public const float OVERLAP_SIZE = 1600f;
        public const float SCENE_SIZE_WITH_OVERLAP = SCENE_SIZE + OVERLAP_SIZE;

        public SceneID(int gridX, int gridZ)
        {
            GridX = gridX;
            GridZ = gridZ;
        }

        public bool IsValid => GridX >= 0 && GridZ >= 0;

        public Vector3 WorldOrigin => new Vector3(GridX * SCENE_SIZE, 0, GridZ * SCENE_SIZE);

        public Vector3 WorldCenter => new Vector3(
            (GridX * SCENE_SIZE) + (SCENE_SIZE / 2f),
            0,
            (GridZ * SCENE_SIZE) + (SCENE_SIZE / 2f)
        );

        public static SceneID FromWorldPosition(Vector3 worldPos)
        {
            int gridX = Mathf.FloorToInt(worldPos.x / SCENE_SIZE);
            int gridZ = Mathf.FloorToInt(worldPos.z / SCENE_SIZE);
            return new SceneID(gridX, gridZ);
        }

        public Vector3 ToLocalPosition(Vector3 worldPos)
        {
            return new Vector3(
                worldPos.x - (GridX * SCENE_SIZE),
                worldPos.y,
                worldPos.z - (GridZ * SCENE_SIZE)
            );
        }

        public Vector3 ToWorldPosition(Vector3 localPos)
        {
            return new Vector3(
                localPos.x + (GridX * SCENE_SIZE),
                localPos.y,
                localPos.z + (GridZ * SCENE_SIZE)
            );
        }

        public bool IsNearBoundary(Vector3 worldPos, float threshold = 10000f)
        {
            Vector3 local = ToLocalPosition(worldPos);
            return local.x > (SCENE_SIZE - threshold) ||
                   local.x < threshold ||
                   local.z > (SCENE_SIZE - threshold) ||
                   local.z < threshold;
        }

        public SceneID GetNeighbor(Direction direction)
        {
            return direction switch
            {
                Direction.X_plus => new SceneID(GridX + 1, GridZ),
                Direction.X_minus => new SceneID(GridX - 1, GridZ),
                Direction.Z_plus => new SceneID(GridX, GridZ + 1),
                Direction.Z_minus => new SceneID(GridX, GridZ - 1),
                _ => this
            };
        }

        public bool Equals(SceneID other) => GridX == other.GridX && GridZ == other.GridZ;

        public override bool Equals(object obj) => obj is SceneID other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + GridX;
                hash = hash * 31 + GridZ;
                return hash;
            }
        }

        public override string ToString() => $"Scene({GridX}, {GridZ})";

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref GridX);
            serializer.SerializeValue(ref GridZ);
        }

        public static bool operator ==(SceneID left, SceneID right) => left.Equals(right);
        public static bool operator !=(SceneID left, SceneID right) => !left.Equals(right);
    }

    public enum Direction
    {
        X_plus,
        X_minus,
        Z_plus,
        Z_minus
    }

    /// <summary>
    /// Данные для перехода между сценами (сериализуются для RPC).
    /// </summary>
    [Serializable]
    public struct SceneTransitionData : INetworkSerializable
    {
        public int TargetGridX;
        public int TargetGridZ;
        public float LocalPosX;
        public float LocalPosY;
        public float LocalPosZ;

        public SceneID TargetScene => new SceneID(TargetGridX, TargetGridZ);
        public Vector3 LocalPosition => new Vector3(LocalPosX, LocalPosY, LocalPosZ);

        public SceneTransitionData(SceneID scene, Vector3 localPos)
        {
            TargetGridX = scene.GridX;
            TargetGridZ = scene.GridZ;
            LocalPosX = localPos.x;
            LocalPosY = localPos.y;
            LocalPosZ = localPos.z;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TargetGridX);
            serializer.SerializeValue(ref TargetGridZ);
            serializer.SerializeValue(ref LocalPosX);
            serializer.SerializeValue(ref LocalPosY);
            serializer.SerializeValue(ref LocalPosZ);
        }
    }
}