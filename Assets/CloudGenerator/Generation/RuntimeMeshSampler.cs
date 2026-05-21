using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.CloudGenerator
{
    public static class RuntimeMeshSampler
    {
        private static System.Random _rng = new System.Random();

        public static List<Vector3> SampleSurface(Mesh targetMesh, int pointCount = 2000)
        {
            if (targetMesh == null) return new List<Vector3>();

            var result = new List<Vector3>(pointCount);
            Vector3[] vertices = targetMesh.vertices;
            int[] triangles = targetMesh.triangles;

            int triangleCount = triangles.Length / 3;
            float[] triangleAreas = new float[triangleCount];
            float[] cumulativeAreas = new float[triangleCount];

            float totalArea = 0f;
            for (int i = 0; i < triangleCount; i++)
            {
                int i0 = triangles[i * 3];
                int i1 = triangles[i * 3 + 1];
                int i2 = triangles[i * 3 + 2];

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];

                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;
                float area = Vector3.Cross(edge1, edge2).magnitude * 0.5f;

                if (area > 0.0001f)
                {
                    triangleAreas[i] = area;
                    totalArea += area;
                    cumulativeAreas[i] = totalArea;
                }
                else
                {
                    triangleAreas[i] = 0f;
                    cumulativeAreas[i] = totalArea;
                }
            }

            if (totalArea <= 0f) return result;

            for (int p = 0; p < pointCount; p++)
            {
                float r = (float)(_rng.NextDouble() * totalArea);

                int triIndex = Array.BinarySearch(cumulativeAreas, r);
                if (triIndex < 0) triIndex = ~triIndex;
                if (triIndex >= triangleCount) triIndex = triangleCount - 1;

                int i0 = triangles[triIndex * 3];
                int i1 = triangles[triIndex * 3 + 1];
                int i2 = triangles[triIndex * 3 + 2];

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];

                float u = (float)_rng.NextDouble();
                float v = (float)_rng.NextDouble();
                float sqrtU = Mathf.Sqrt(u);

                float a = 1f - sqrtU;
                float b = sqrtU * (1f - v);
                float c = sqrtU * v;

                result.Add(new Vector3(
                    a * v0.x + b * v1.x + c * v2.x,
                    a * v0.y + b * v1.y + c * v2.y,
                    a * v0.z + b * v1.z + c * v2.z
                ));
            }

            return result;
        }

        public static List<Vector3> ApplyTransform(
            List<Vector3> points,
            Vector3 scale,
            Vector3 rotationDegrees,
            Vector3 offset)
        {
            if (points == null || points.Count == 0) return new List<Vector3>();

            var result = new List<Vector3>(points.Count);

            float rz = rotationDegrees.z * Mathf.Deg2Rad;
            float ry = rotationDegrees.y * Mathf.Deg2Rad;
            float rx = rotationDegrees.x * Mathf.Deg2Rad;

            float cosZ = Mathf.Cos(rz), sinZ = Mathf.Sin(rz);
            float cosY = Mathf.Cos(ry), sinY = Mathf.Sin(ry);
            float cosX = Mathf.Cos(rx), sinX = Mathf.Sin(rx);

            foreach (var p in points)
            {
                float sx = p.x * scale.x;
                float sy = p.y * scale.y;
                float sz = p.z * scale.z;

                float rx2 = sx * cosZ - sy * sinZ;
                float ry2 = sx * sinZ + sy * cosZ;
                float rz2 = sz;

                float rx3 = rx2 * cosY + rz2 * sinY;
                float ry3 = ry2;
                float rz3 = -rx2 * sinY + rz2 * cosY;

                float rx4 = rx3;
                float ry4 = ry3 * cosX - rz3 * sinX;
                float rz4 = ry3 * sinX + rz3 * cosX;

                result.Add(new Vector3(
                    rx4 + offset.x,
                    ry4 + offset.y,
                    rz4 + offset.z
                ));
            }

            return result;
        }

        public static Mesh LoadMeshFromResources(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;

            string cleanPath = resourcePath;
            cleanPath = cleanPath.Replace(".mesh", "");
            cleanPath = cleanPath.Replace("Assets/Resources/", "");

            var loaded = Resources.Load<Mesh>(cleanPath);
            return loaded;
        }

        public static List<Vector3> SampleMeshFromResources(
            string resourcePath,
            int pointCount,
            Vector3 scale,
            Vector3 rotation,
            Vector3 offset)
        {
            var mesh = LoadMeshFromResources(resourcePath);
            if (mesh == null)
            {
                Debug.LogWarning($"[RuntimeMeshSampler] Failed to load mesh at path: {resourcePath}");
                return new List<Vector3>();
            }

            var rawPoints = SampleSurface(mesh, pointCount);
            if (rawPoints.Count == 0) return rawPoints;

            return ApplyTransform(rawPoints, scale, rotation, offset);
        }
    }
}