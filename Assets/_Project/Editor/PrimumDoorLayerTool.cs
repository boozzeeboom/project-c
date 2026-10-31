// Project C: назначение слоя PRIMUM_DOORS дверным префабам (T-CULL01 / Stage 2, H3)
// Слой (ProjectSettings/TagManager.asset) — в git, а .prefab лежат в Lore VCS (.gitignore:151–154).
// Поэтому назначение слоя сделано инструментом, который можно повторить на любой машине:
//   Project C → Culling → Door Layer → Dry Run / Apply
// Меняет ТОЛЬКО m_Layer у корня и детей перечисленных префабов; меши, материалы, коллайдеры,
// DoorController, пивоты и структура не трогаются. Инстансы в сцене наследуют слой сами.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools
{
    public static class PrimumDoorLayerTool
    {
        private const string DoorPrefabFolder = "Assets/_Project/Prefabs/City/Doors";
        private const int DoorLayer = 10;
        private const string DoorLayerName = "PRIMUM_DOORS";

        [MenuItem("Project C/Culling/Door Layer/Dry Run", priority = 200)]
        public static void DryRun()
        {
            Run(apply: false);
        }

        [MenuItem("Project C/Culling/Door Layer/Apply", priority = 201)]
        public static void Apply()
        {
            Run(apply: true);
        }

        private static void Run(bool apply)
        {
            if (LayerMask.LayerToName(DoorLayer) != DoorLayerName)
            {
                Debug.LogError("[DoorLayer] Слой " + DoorLayer + " в TagManager называется '" +
                               LayerMask.LayerToName(DoorLayer) + "', ожидался '" + DoorLayerName + "'. Отмена.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { DoorPrefabFolder });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogError("[DoorLayer] В " + DoorPrefabFolder + " нет префабов.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[DoorLayer] " + (apply ? "APPLY" : "DRY RUN") + " | префабов: " + guids.Length +
                          " | слой " + DoorLayer + " (" + DoorLayerName + ")");

            int totalChanged = 0, totalObjects = 0, totalRenderers = 0, totalStatic = 0, prefabsTouched = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (!path.EndsWith(".prefab")) continue;

                GameObject content = PrefabUtility.LoadPrefabContents(path);
                if (content == null) continue;
                try
                {
                    var transforms = content.GetComponentsInChildren<Transform>(true);
                    int changed = 0;
                    int renderers = 0;
                    int staticOnes = 0;

                    for (int t = 0; t < transforms.Length; t++)
                    {
                        GameObject go = transforms[t].gameObject;
                        if (go.layer != DoorLayer) changed++;
                        var meshRenderer = go.GetComponent<Renderer>();
                        if (meshRenderer != null)
                        {
                            renderers++;
                            if (go.isStatic) staticOnes++;
                        }
                        if (apply) go.layer = DoorLayer;
                    }

                    totalChanged += changed;
                    totalObjects += transforms.Length;
                    totalRenderers += renderers;
                    totalStatic += staticOnes;

                    if (changed > 0)
                    {
                        prefabsTouched++;
                        sb.AppendLine("  " + Path.GetFileName(path) + ": объектов " + transforms.Length +
                                      ", рендереров " + renderers + ", static " + staticOnes +
                                      ", сменить слой " + changed);
                    }

                    if (apply && changed > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(content, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(content);
                }
            }

            sb.AppendLine("  ИТОГО: объектов " + totalObjects + ", рендереров " + totalRenderers +
                          ", static " + totalStatic + ", объектов со сменой слоя " + totalChanged +
                          ", префабов затронуто " + prefabsTouched);

            if (apply)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(sb.ToString());
        }
    }
}
