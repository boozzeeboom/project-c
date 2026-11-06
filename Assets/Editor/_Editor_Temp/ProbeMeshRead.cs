using System;
using System.Reflection;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace ProjectC.TempProbes
{
    public static class ProbeMeshRead
    {
        public static void Execute()
        {
            var sb = new StringBuilder();

            string[] names =
            {
                "WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER/ADM_STAIR_ENTRY_1",
                "WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER/ADM_DOME",
                "WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER/ADM_ROOF_SLAB"
            };

            Type bsc = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                bsc = asm.GetType("ProjectC.EditorTools.BuildSettlementColliders", false);
                if (bsc != null) break;
            }
            var tryRead = bsc?.GetMethod("TryReadMeshData", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

            foreach (string path in names)
            {
                var go = GameObject.Find(path);
                if (go == null) { sb.AppendLine($"[Probe11] НЕТ '{path}'"); continue; }
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) { sb.AppendLine($"[Probe11] '{go.name}': нет меша"); continue; }
                Mesh m = mf.sharedMesh;

                sb.AppendLine($"[Probe11] '{go.name}': isReadable={m.isReadable} vertexCount={m.vertexCount} " +
                              $"subMeshes={m.subMeshCount} idx0={m.GetIndexCount(0)}");

                // 1) mesh.vertices
                try
                {
                    var v = m.vertices;
                    sb.AppendLine($"[Probe11]   mesh.vertices -> {v.Length}");
                }
                catch (Exception e) { sb.AppendLine($"[Probe11]   mesh.vertices EX: {e.GetType().Name}: {e.Message}"); }

                // 2) AcquireReadOnlyMeshData
                try
                {
                    using (var data = Mesh.AcquireReadOnlyMeshData(m))
                    {
                        if (data.Length == 0) sb.AppendLine("[Probe11]   AcquireReadOnlyMeshData: Length=0");
                        else
                        {
                            Mesh.MeshData d = data[0];
                            sb.AppendLine($"[Probe11]   MeshData: vertexCount={d.vertexCount} subMeshes={d.subMeshCount} " +
                                          $"vertexBufferCount={d.vertexBufferCount}");
                            try
                            {
                                var vb = new NativeArray<Vector3>(d.vertexCount, Allocator.Temp);
                                try
                                {
                                    d.GetVertices(vb);
                                    sb.AppendLine($"[Probe11]   GetVertices<Vector3> OK: v0={vb[0]}");
                                }
                                finally { vb.Dispose(); }
                            }
                            catch (Exception e) { sb.AppendLine($"[Probe11]   GetVertices<Vector3> EX: {e.GetType().Name}: {e.Message}"); }
                        }
                    }
                }
                catch (Exception e) { sb.AppendLine($"[Probe11]   AcquireReadOnlyMeshData EX: {e.GetType().Name}: {e.Message}"); }

                // 3) сам метод инструмента
                if (tryRead != null)
                {
                    var args = new object[] { m, null, null };
                    try
                    {
                        bool ok = (bool)tryRead.Invoke(null, args);
                        var v = args[1] as Vector3[];
                        var t = args[2] as int[];
                        sb.AppendLine($"[Probe11]   TryReadMeshData -> {ok} verts={(v?.Length ?? -1)} tris={(t?.Length ?? -1)}");
                    }
                    catch (Exception e)
                    {
                        var inner = e.InnerException ?? e;
                        sb.AppendLine($"[Probe11]   TryReadMeshData EX: {inner.GetType().Name}: {inner.Message}");
                    }
                }
            }

            Debug.Log(sb.ToString());
        }
    }
}
