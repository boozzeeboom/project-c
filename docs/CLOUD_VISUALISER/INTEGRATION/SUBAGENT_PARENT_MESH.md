# Subagent Prompt: Implement Parent Mesh System for Unity Cloud Generator

## Task
Implement the Parent Mesh feature: generate spheres ON THE SURFACE of a Unity Mesh asset instead of on an ellipsoid.

## Overview

In the JS visualizer, `loadParentMesh()` loads an OBJ file and `sampleMeshSurface()` extracts ~2000 points on the mesh surface. Then `generateSphereLayer(layer, parentSpheres)` uses those points as the basis for sphere generation.

In Unity, we use a Mesh asset directly (no OBJ parsing). The key class is `CloudParentMesh.cs`.

---

## File: CloudParentMesh.cs

Create at: `Assets/CloudGenerator/Generation/CloudParentMesh.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.CloudGenerator
{
    /// <summary>
    /// Surface sampling from Unity Mesh assets for cloud generation.
    /// Monte Carlo weighted sampling based on triangle area.
    /// </summary>
    public static class CloudParentMesh
    {
        private static System.Random _rng = new System.Random();

        /// <summary>
        /// Sample random points uniformly distributed on mesh surface.
        /// Uses Monte Carlo with weighted random triangle selection.
        /// </summary>
        /// <param name="mesh">Unity Mesh to sample from</param>
        /// <param name="pointCount">Number of points to sample (default 2000)</param>
        /// <returns>List of sampled positions in local space</returns>
        public static List<Vector3> SampleSurface(Mesh mesh, int pointCount = 2000)
        {
            var result = new List<Vector3>(pointCount);

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // Build list of triangles with areas
            var triangleCount = triangles.Length / 3;
            var triangleAreas = new float[triangleCount];
            var cumulativeAreas = new float[triangleCount];

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

                if (area > 0.0001f) // filter degenerate triangles
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

            // Sample points with weighted random triangle selection
            for (int p = 0; p < pointCount; p++)
            {
                float r = (float)(_rng.NextDouble() * totalArea);

                // Binary search for triangle
                int triIndex = System.Array.BinarySearch(cumulativeAreas, r);
                if (triIndex < 0) triIndex = ~triIndex;
                if (triIndex >= triangleCount) triIndex = triangleCount - 1;

                int i0 = triangles[triIndex * 3];
                int i1 = triangles[triIndex * 3 + 1];
                int i2 = triangles[triIndex * 3 + 2];

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];

                // Barycentric coordinates with sqrt for uniform distribution
                float u = (float)_rng.NextDouble();
                float v = (float)_rng.NextDouble();
                float sqrtU = Mathf.Sqrt(u);

                float a = 1f - sqrtU;
                float b = sqrtU * (1f - v);
                float c = sqrtU * v;

                float x = a * v0.x + b * v1.x + c * v2.x;
                float y = a * v0.y + b * v1.y + c * v2.y;
                float z = a * v0.z + b * v1.z + c * v2.z;

                result.Add(new Vector3(x, y, z));
            }

            return result;
        }

        /// <summary>
        /// Apply scale, rotation (Euler ZYX), and offset to points.
        /// </summary>
        public static List<Vector3> ApplyTransform(
            List<Vector3> points,
            Vector3 scale,
            Vector3 rotationDegrees,
            Vector3 offset)
        {
            var result = new List<Vector3>(points.Count);

            float rz = rotationDegrees.z * Mathf.Deg2Rad;
            float ry = rotationDegrees.y * Mathf.Deg2Rad;
            float rx = rotationDegrees.x * Mathf.Deg2Rad;

            float cosZ = Mathf.Cos(rz), sinZ = Mathf.Sin(rz);
            float cosY = Mathf.Cos(ry), sinY = Mathf.Sin(ry);
            float cosX = Mathf.Cos(rx), sinX = Mathf.Sin(rx);

            foreach (var p in points)
            {
                // Scale
                float sx = p.x * scale.x;
                float sy = p.y * scale.y;
                float sz = p.z * scale.z;

                // Rotate Z
                float rx2 = sx * cosZ - sy * sinZ;
                float ry2 = sx * sinZ + sy * cosZ;
                float rz2 = sz;

                // Rotate Y
                float rx3 = rx2 * cosY + rz2 * sinY;
                float ry3 = ry2;
                float rz3 = -rx2 * sinY + rz2 * cosY;

                // Rotate X
                float rx4 = rx3;
                float ry4 = ry3 * cosX - rz3 * sinX;
                float rz4 = ry3 * sinX + rz3 * cosX;

                // Offset
                result.Add(new Vector3(
                    rx4 + offset.x,
                    ry4 + offset.y,
                    rz4 + offset.z
                ));
            }

            return result;
        }
    }
}
```

---

## Modifications to Existing Files

### 1. CloudTypes.cs

Add to `CloudLayerConfig`:
```csharp
// Parent Mesh support (serialized as asset path string)
public string ParentMeshPath = "";
public float ParentMeshScaleX = 1f, ParentMeshScaleY = 1f, ParentMeshScaleZ = 1f;
public float ParentMeshRotX = 0f, ParentMeshRotY = 0f, ParentMeshRotZ = 0f;
public float ParentMeshOffsetX = 0f, ParentMeshOffsetY = 0f, ParentMeshOffsetZ = 0f;
```

Use `string` for path because Unity's `JsonUtility` cannot serialize `Mesh` directly.

### 2. CloudGenerator.cs

In `GenerateSphereLayer()`, add after parent sphere setup:
```csharp
List<Vector3> parentMeshPoints = null;
if (!string.IsNullOrEmpty(layer.ParentMeshPath))
{
    var meshAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(layer.ParentMeshPath);
    if (meshAsset != null)
    {
        parentMeshPoints = CloudParentMesh.SampleSurface(meshAsset, 2000);
        parentMeshPoints = CloudParentMesh.ApplyTransform(parentMeshPoints,
            new Vector3(layer.ParentMeshScaleX, layer.ParentMeshScaleY, layer.ParentMeshScaleZ),
            new Vector3(layer.ParentMeshRotX, layer.ParentMeshRotY, layer.ParentMeshRotZ),
            new Vector3(layer.ParentMeshOffsetX, layer.ParentMeshOffsetY, layer.ParentMeshOffsetZ));
    }
}
```

**Note:** Wrap the `UnityEditor` usage in `#if UNITY_EDITOR` or create a separate editor-only helper.

Then use `parentMeshPoints` in the generation loop — for each point, treat it as a parent sphere and generate bumps on its surface.

### 3. CloudGeneratorWindow.cs (Editor Only)

Add UI for parent mesh selection:
```csharp
// In layer parameter section:
EditorGUILayout.LabelField("Parent Mesh", EditorStyles.boldLabel);
layer.ParentMeshPath = EditorGUILayout.TextField("Mesh Path", layer.ParentMeshPath);

if (!string.IsNullOrEmpty(layer.ParentMeshPath))
{
    // Transform controls
    EditorGUILayout.BeginHorizontal();
    layer.ParentMeshScaleX = EditorGUILayout.Slider("Scale X", layer.ParentMeshScaleX, 0.1f, 3f);
    layer.ParentMeshScaleY = EditorGUILayout.Slider("Scale Y", layer.ParentMeshScaleY, 0.1f, 3f);
    layer.ParentMeshScaleZ = EditorGUILayout.Slider("Scale Z", layer.ParentMeshScaleZ, 0.1f, 3f);
    EditorGUILayout.EndHorizontal();

    // ... rotation and offset sliders
}
```

---

## Key Implementation Notes

1. **Mesh format:** Use `mesh.vertices` (local space) and `mesh.triangles` (index array)
2. **Triangle count:** `mesh.triangles.Length / 3` gives number of triangles
3. **Weighted sampling:** Larger triangles = more sample points (by area)
4. **Barycentric:** `p0 + sqrt(random1) * (p1-p0) + sqrt(random1)*random2 * (p2-p0)` for uniform distribution
5. **Rotation order:** Z → Y → X (matches JS implementation in visualizer)
6. **Editor-only:** Parent mesh path resolution uses `UnityEditor.AssetDatabase` — keep separate from runtime code

---

## Reference

JS implementation to mirror:
- `sampleMeshSurface()` — lines 4153-4218 in visualizer3d.html
- `applyParentMeshTransform()` — lines 4252-4282 in visualizer3d.html
- Parent mesh integration in `generateSphereLayer()` — lines 3272-3333

---

## Context
- Project: ProjectC_client
- Unity version: 6000.4.1f1
- Visualizer: docs/CLOUD_VISUALISER/web/visualizer3d.html
- Documentation: docs/CLOUD_VISUALISER/FIXES.md