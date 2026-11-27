using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ProjectC.Equipment;
using ProjectC.Equipment.Visual;
using ProjectC.Items;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.Editor.Equipment
{
    public static class ArmorKitBuilder
    {
        private const string ArmorSourceRoot = "Assets/Generated_Models_Rebaked_Decimated/02_Armor";
        private const string MirrorMeshDir = "Assets/_Project/Generated/Armor/Mirror";
        private const string MaterialDir = "Assets/_Project/Materials/Armor";
        private const string PrefabDir = "Assets/_Project/Prefabs/Equipment/Armor";
        private const string PreviewPrefabDir = "Assets/_Project/Prefabs/Equipment/Armor/_Preview";
        private const string PreviewPrefabPath = "Assets/_Project/Prefabs/Equipment/Armor/_Preview/ArmorPreview.prefab";
        private const string PickupPrefabDir = "Assets/_Project/Prefabs/Items";
        private const string PickupPrefabPath = "Assets/_Project/Prefabs/Items/Pickup_Armor.prefab";
        private const string ClothingDir = "Assets/_Project/Resources/Equipment/Clothing/Armor";
        private const string ItemRegistryPath = "Assets/_Project/Resources/Items/Data/ItemRegistry.asset";
        private const string SlotIconsDir = "Assets/_Project/UI/Icons/Equipment/Slots";
        private const string ReportDir = "Tools/Blender/CharacterRetopo/_reports/armor_items";
        private const string PreviewRenderDir = "Tools/Blender/CharacterRetopo/_reports/armor_items/preview";

        public enum PartKind
        {
            Head,
            Chest,
            Back,
            UtilityBelt,
            Mantle,
            Shoulders,
            Hands,
            Feet,
            LegsBelt,
            LegsThigh,
            LegsKnee
        }

        public class ArmorPartSpec
        {
            public string categoryFolder;
            public string modelName;
            public PartKind kind;
            public bool isPaired;
            public string nodeName;
            public HumanBodyBones boneL;
            public HumanBodyBones boneR;
            public float baseScale;
            public bool swapLR;
            public float yawOffsetL;
            public float yawOffsetR;
            public float pitchOffset;
            public float rollOffset;
            public Vector3 worldOffsetL;
            public Vector3 worldOffsetR;
        }

        public class ArmorItemSpec
        {
            public string line;
            public string slotTag;
            public string displayName;
            public EquipSlot equipSlot;
            public int tier;
            public int baseDef;
            public float weightKg;
            public List<ArmorPartSpec> parts = new List<ArmorPartSpec>();
        }

        [MenuItem("Tools/Project C/Armor/Build Armor Kits (Idempotent)")]
        public static void BuildArmorKits()
        {
            EnsureFolders();
            FixFbxScaleInternal();

            List<ArmorItemSpec> specs = GetAllItemSpecs();

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/NetworkPlayer.prefab");
            Animator refAnimator = playerPrefab != null ? playerPrefab.GetComponentInChildren<Animator>(true) : null;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[ArmorKitBuilder] Shader 'Universal Render Pipeline/Lit' not found!");
                return;
            }

            var meshCache = new Dictionary<string, (Mesh single, Mesh mirror, float split, float twoLobe, bool wasCut)>(StringComparer.Ordinal);

            foreach (var item in specs)
            {
                foreach (var part in item.parts)
                {
                    if (meshCache.ContainsKey(part.modelName)) continue;

                    string fbxPath = $"{ArmorSourceRoot}/{part.categoryFolder}/{part.modelName}/{part.modelName}_Dec.fbx";
                    Mesh srcMesh = LoadMeshFromFbx(fbxPath);
                    if (srcMesh == null)
                    {
                        Debug.LogError($"[ArmorKitBuilder] Missing mesh at {fbxPath}");
                        continue;
                    }

                    AnalyzeMeshForCut(srcMesh, part.isPaired, part.modelName, out float split, out float twoLobe, out bool shouldCut);
                    Mesh singleMesh = srcMesh;
                    Mesh mirrorMesh = null;

                    if (part.isPaired)
                    {
                        string mirrorAssetPath = $"{MirrorMeshDir}/{part.modelName}_Mirror.asset";
                        if (shouldCut)
                        {
                            singleMesh = CutLargestHalf(srcMesh, $"{part.modelName}_Single");
                        }
                        mirrorMesh = CreateOrUpdateMirrorMeshAsset(singleMesh, shouldCut ? singleMesh : null, mirrorAssetPath, $"{part.modelName}_Mirror");
                    }

                    meshCache[part.modelName] = (singleMesh, mirrorMesh, split, twoLobe, shouldCut);
                }
            }

            var createdItems = new List<(ArmorItemSpec spec, ClothingItemData data)>();
            foreach (var spec in specs)
            {
                string matPath = $"{MaterialDir}/M_ARM_{spec.line}_{spec.slotTag}.mat";
                Dictionary<string, Material> partMats = CreateOrUpdateItemMaterials(spec, matPath, urpLit);

                string visualPrefabPath = $"{PrefabDir}/PF_ARM_{spec.line}_{spec.slotTag}.prefab";
                GameObject visualPrefab = CreateOrUpdateVisualPrefab(spec, visualPrefabPath, meshCache, partMats);

                string eqPrefabPath = $"{PrefabDir}/PF_ARM_{spec.line}_{spec.slotTag}_Eq.prefab";
                GameObject eqPrefab = CreateOrUpdateEquippedPrefab(spec, eqPrefabPath, meshCache, partMats, refAnimator);

                string itemAssetPath = $"{ClothingDir}/Clothing_ARM_{spec.line}_{spec.slotTag}.asset";
                ClothingItemData itemData = CreateOrUpdateClothingItem(spec, itemAssetPath, visualPrefab, eqPrefab);
                createdItems.Add((spec, itemData));
            }

            UpdateItemRegistryAppendOnly(createdItems.Select(x => x.data).ToList());
            CreateOrUpdatePickupArmorPrefab(createdItems.FirstOrDefault().data);
            CreateOrUpdateArmorPreviewPrefab(specs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArmorKitBuilder] Successfully built {specs.Count} armor kits, materials, prefabs, ClothingItemData, ItemRegistry, Pickup_Armor, and ArmorPreview!");
        }

        [MenuItem("Tools/Project C/Armor/4. Render ArmorPreview in Prefab Mode")]
        public static void RenderArmorPreviewInPrefabMode()
        {
            Directory.CreateDirectory(PreviewRenderDir);
            PrefabStage stage = PrefabStageUtility.OpenPrefab(PreviewPrefabPath);
            if (stage == null || stage.prefabContentsRoot == null)
            {
                Debug.LogError($"[ArmorKitBuilder] Failed to open PrefabStage for {PreviewPrefabPath}");
                return;
            }

            GameObject root = stage.prefabContentsRoot;
            var camGo = new GameObject("__PreviewCam__");
            var lightGo1 = new GameObject("__PreviewLightKey__");
            var lightGo2 = new GameObject("__PreviewLightFill__");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, stage.scene);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo1, stage.scene);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo2, stage.scene);

            try
            {
                var l1 = lightGo1.AddComponent<Light>();
                l1.type = LightType.Directional;
                l1.intensity = 1.35f;
                l1.color = Color.white;
                lightGo1.transform.rotation = Quaternion.Euler(35f, 155f, 0f);

                var l2 = lightGo2.AddComponent<Light>();
                l2.type = LightType.Directional;
                l2.intensity = 1.0f;
                l2.color = new Color(0.92f, 0.95f, 1.0f);
                lightGo2.transform.rotation = Quaternion.Euler(25f, -25f, 0f);

                var cam = camGo.AddComponent<Camera>();
                cam.scene = stage.scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.16f, 0.18f, 0.21f, 1f);
                cam.orthographic = true;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 50f;

                Bounds totalBounds = CalculateHierarchyBounds(root);

                // 1. Render overall front / side / back at 1024x1024
                RenderOrthoView(cam, totalBounds.center, new Vector3(0f, 0f, 1f), Vector3.up, Mathf.Max(totalBounds.extents.x, totalBounds.extents.y) * 1.08f, 1024, Path.Combine(PreviewRenderDir, "front.png"));
                RenderOrthoView(cam, totalBounds.center, new Vector3(1f, 0f, 0f), Vector3.up, Mathf.Max(totalBounds.extents.z, totalBounds.extents.y) * 1.08f, 1024, Path.Combine(PreviewRenderDir, "side.png"));
                RenderOrthoView(cam, totalBounds.center, new Vector3(0f, 0f, -1f), Vector3.up, Mathf.Max(totalBounds.extents.x, totalBounds.extents.y) * 1.08f, 1024, Path.Combine(PreviewRenderDir, "back.png"));

                // 2. Render per-mannequin grid contact sheet (front, side, back) so every mannequin is crystal clear
                var mannequins = new List<Transform>();
                foreach (Transform child in root.transform)
                {
                    if (child.name.StartsWith("Mannequin_", StringComparison.Ordinal))
                        mannequins.Add(child);
                }

                RenderMannequinSheet(cam, mannequins, new Vector3(0f, 0f, 1f), Path.Combine(PreviewRenderDir, "mannequins_front.png"));
                RenderMannequinSheet(cam, mannequins, new Vector3(1f, 0f, 0f), Path.Combine(PreviewRenderDir, "mannequins_side.png"));
                RenderMannequinSheet(cam, mannequins, new Vector3(0f, 0f, -1f), Path.Combine(PreviewRenderDir, "mannequins_back.png"));

                Debug.Log($"[ArmorKitBuilder] Rendered Prefab Mode views (front/side/back + mannequin sheets) to {PreviewRenderDir}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(lightGo1);
                UnityEngine.Object.DestroyImmediate(lightGo2);
                StageUtility.GoToMainStage();
            }
        }

        private static Bounds CalculateHierarchyBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }
            return b;
        }

        private static void RenderOrthoView(Camera cam, Vector3 center, Vector3 camOffsetDir, Vector3 up, float orthoSize, int res, string outPath)
        {
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(0.5f, orthoSize);
            cam.transform.position = center + camOffsetDir.normalized * 12f;
            cam.transform.LookAt(center, up);

            var rt = RenderTexture.GetTemporary(res, res, 24, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
                tex.Apply();
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static void RenderMannequinSheet(Camera cam, List<Transform> mannequins, Vector3 viewDir, string outPath)
        {
            // Isolate each mannequin temporarily so neighbors don't overlap in side view
            const int CellW = 340;
            const int CellH = 512;
            const int Cols = 6;
            int rows = Mathf.CeilToInt((float)mannequins.Count / Cols);
            var sheet = new Texture2D(Cols * CellW, rows * CellH, TextureFormat.RGB24, false);
            var rt = RenderTexture.GetTemporary(CellW, CellH, 24, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;

            try
            {
                for (int i = 0; i < mannequins.Count; i++)
                {
                    for (int j = 0; j < mannequins.Count; j++)
                        mannequins[j].gameObject.SetActive(j == i);

                    Bounds b = CalculateHierarchyBounds(mannequins[i].gameObject);
                    cam.orthographic = true;
                    cam.orthographicSize = Mathf.Max(0.95f, b.extents.y * 1.06f);
                    cam.transform.position = b.center + viewDir.normalized * 8f;
                    cam.transform.LookAt(b.center, Vector3.up);

                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;

                    var cell = new Texture2D(CellW, CellH, TextureFormat.RGB24, false);
                    cell.ReadPixels(new Rect(0, 0, CellW, CellH), 0, 0);
                    cell.Apply();

                    int col = i % Cols;
                    int row = i / Cols;
                    int yFlip = (rows - 1 - row) * CellH;
                    sheet.SetPixels(col * CellW, yFlip, CellW, CellH, cell.GetPixels());
                    UnityEngine.Object.DestroyImmediate(cell);
                }
            }
            finally
            {
                for (int j = 0; j < mannequins.Count; j++)
                    mannequins[j].gameObject.SetActive(true);
                cam.targetTexture = null;
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }

            sheet.Apply();
            File.WriteAllBytes(outPath, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        private static void EnsureFolders()
        {
            string[] folders = {
                MirrorMeshDir,
                MaterialDir,
                PrefabDir,
                PreviewPrefabDir,
                PickupPrefabDir,
                ClothingDir,
                ReportDir,
                PreviewRenderDir
            };
            foreach (string f in folders)
            {
                if (!Directory.Exists(f))
                {
                    Directory.CreateDirectory(f);
                }
            }
            AssetDatabase.Refresh();
        }

        private static void FixFbxScaleInternal()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ArmorSourceRoot });
            var toFix = new List<ModelImporter>();
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.EndsWith("_Dec.fbx", StringComparison.OrdinalIgnoreCase)) continue;
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp != null && (!imp.useFileScale || Mathf.Abs(imp.globalScale - 100f) > 0.001f))
                {
                    toFix.Add(imp);
                }
            }
            if (toFix.Count == 0) return;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var imp in toFix)
                {
                    imp.useFileScale = true;
                    imp.globalScale = 100f;
                    imp.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();
        }

        private static Mesh LoadMeshFromFbx(string fbxPath)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(fbxPath);
            if (mesh != null) return mesh;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is Mesh m) return m;
            }
            return null;
        }

        public static void AnalyzeMeshForCut(Mesh mesh, bool isPairedSlot, string modelName, out float split, out float twoLobe, out bool shouldCut)
        {
            AnalyzeMeshSymmetry(mesh, out int triCount, out split, out float twoLobeXY, out float twoLobeXZ, out int crossCenterTris, out int compCount);
            twoLobe = Mathf.Max(twoLobeXY, twoLobeXZ);
            shouldCut = isPairedSlot && (split >= 0.35f && split <= 0.65f) && (twoLobe >= 0.30f) && (crossCenterTris == 0);
        }

        private static Mesh CutLargestHalf(Mesh src, string newName)
        {
            Vector3[] verts = src.vertices;
            Vector3[] norms = src.normals;
            Vector4[] tangs = src.tangents;
            Vector2[] uvs = src.uv;
            int[] tris = src.triangles;
            int triCount = tris.Length / 3;
            float cx = src.bounds.center.x;

            var leftTris = new List<int>();
            var rightTris = new List<int>();
            for (int t = 0; t < triCount; t++)
            {
                int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];
                float cX = (verts[i0].x + verts[i1].x + verts[i2].x) / 3f;
                if (cX < cx)
                {
                    leftTris.Add(i0); leftTris.Add(i1); leftTris.Add(i2);
                }
                else
                {
                    rightTris.Add(i0); rightTris.Add(i1); rightTris.Add(i2);
                }
            }

            List<int> chosen = leftTris.Count >= rightTris.Count ? leftTris : rightTris;
            var oldToNew = new Dictionary<int, int>();
            var newVerts = new List<Vector3>();
            var newNorms = new List<Vector3>();
            var newTangs = new List<Vector4>();
            var newUvs = new List<Vector2>();
            var newTris = new int[chosen.Count];

            bool hasNorms = norms != null && norms.Length == verts.Length;
            bool hasTangs = tangs != null && tangs.Length == verts.Length;
            bool hasUvs = uvs != null && uvs.Length == verts.Length;

            for (int i = 0; i < chosen.Count; i++)
            {
                int oldIdx = chosen[i];
                if (!oldToNew.TryGetValue(oldIdx, out int newIdx))
                {
                    newIdx = newVerts.Count;
                    oldToNew[oldIdx] = newIdx;
                    newVerts.Add(verts[oldIdx]);
                    if (hasNorms) newNorms.Add(norms[oldIdx]);
                    if (hasTangs) newTangs.Add(tangs[oldIdx]);
                    if (hasUvs) newUvs.Add(uvs[oldIdx]);
                }
                newTris[i] = newIdx;
            }

            var m = new Mesh { name = newName };
            if (newVerts.Count > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(newVerts);
            if (hasNorms) m.SetNormals(newNorms);
            if (hasTangs) m.SetTangents(newTangs);
            if (hasUvs) m.SetUVs(0, newUvs);
            m.triangles = newTris;
            m.RecalculateBounds();

            Vector3 offset = m.bounds.center;
            for (int i = 0; i < newVerts.Count; i++) newVerts[i] -= offset;
            m.SetVertices(newVerts);
            m.RecalculateBounds();
            return m;
        }

        private static Mesh CreateOrUpdateMirrorMeshAsset(Mesh srcSingle, Mesh cutSingleToSave, string assetPath, string mirrorName)
        {
            Vector3[] verts = srcSingle.vertices;
            Vector3[] norms = srcSingle.normals;
            Vector4[] tangs = srcSingle.tangents;
            Vector2[] uvs = srcSingle.uv;
            int[] tris = srcSingle.triangles;

            var mVerts = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                mVerts[i] = new Vector3(-v.x, v.y, v.z);
            }

            var mNorms = new Vector3[norms != null ? norms.Length : 0];
            for (int i = 0; i < mNorms.Length; i++)
            {
                Vector3 n = norms[i];
                mNorms[i] = new Vector3(-n.x, n.y, n.z);
            }

            var mTangs = new Vector4[tangs != null ? tangs.Length : 0];
            for (int i = 0; i < mTangs.Length; i++)
            {
                Vector4 tg = tangs[i];
                mTangs[i] = new Vector4(-tg.x, tg.y, tg.z, tg.w);
            }

            var mTris = new int[tris.Length];
            int triCount = tris.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                mTris[t * 3] = tris[t * 3];
                mTris[t * 3 + 1] = tris[t * 3 + 2];
                mTris[t * 3 + 2] = tris[t * 3 + 1];
            }

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                existing = new Mesh { name = mirrorName };
                FillMeshData(existing, mirrorName, mVerts, mNorms, mTangs, uvs, mTris);
                AssetDatabase.CreateAsset(existing, assetPath);
            }
            else
            {
                existing.Clear();
                FillMeshData(existing, mirrorName, mVerts, mNorms, mTangs, uvs, mTris);
                EditorUtility.SetDirty(existing);
            }

            if (cutSingleToSave != null)
            {
                Mesh existingSingle = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<Mesh>()
                    .FirstOrDefault(x => x != existing && x.name == cutSingleToSave.name);
                if (existingSingle == null)
                {
                    AssetDatabase.AddObjectToAsset(cutSingleToSave, assetPath);
                }
                else
                {
                    existingSingle.Clear();
                    FillMeshData(existingSingle, cutSingleToSave.name, cutSingleToSave.vertices, cutSingleToSave.normals, cutSingleToSave.tangents, cutSingleToSave.uv, cutSingleToSave.triangles);
                    EditorUtility.SetDirty(existingSingle);
                }
            }

            return existing;
        }

        private static void FillMeshData(Mesh m, string name, Vector3[] verts, Vector3[] norms, Vector4[] tangs, Vector2[] uvs, int[] tris)
        {
            m.name = name;
            if (verts.Length > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = verts;
            if (norms != null && norms.Length == verts.Length) m.normals = norms;
            if (tangs != null && tangs.Length == verts.Length) m.tangents = tangs;
            if (uvs != null && uvs.Length == verts.Length) m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
        }

        private static Dictionary<string, Material> CreateOrUpdateItemMaterials(ArmorItemSpec spec, string matPath, Shader urpLit)
        {
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);

            ArmorPartSpec primaryPart = spec.parts.FirstOrDefault(p => p.kind == PartKind.LegsThigh) ?? spec.parts[0];
            string primaryTexPath = $"{ArmorSourceRoot}/{primaryPart.categoryFolder}/{primaryPart.modelName}/{primaryPart.modelName}_Dec_BaseColor.png";
            Texture2D primaryTex = AssetDatabase.LoadAssetAtPath<Texture2D>(primaryTexPath);

            Material mainMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            string mainMatName = Path.GetFileNameWithoutExtension(matPath);
            if (mainMat == null)
            {
                mainMat = new Material(urpLit) { name = mainMatName };
                ApplyArmorMaterialSettings(mainMat, urpLit, primaryTex);
                AssetDatabase.CreateAsset(mainMat, matPath);
            }
            else
            {
                mainMat.name = mainMatName;
                ApplyArmorMaterialSettings(mainMat, urpLit, primaryTex);
                EditorUtility.SetDirty(mainMat);
            }
            result[primaryPart.modelName] = mainMat;

            var allSubMats = AssetDatabase.LoadAllAssetsAtPath(matPath).OfType<Material>().ToList();
            foreach (var part in spec.parts)
            {
                if (result.ContainsKey(part.modelName)) continue;
                string subMatName = $"{mainMatName}_{part.nodeName}";
                string texPath = $"{ArmorSourceRoot}/{part.categoryFolder}/{part.modelName}/{part.modelName}_Dec_BaseColor.png";
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                Material subMat = allSubMats.FirstOrDefault(m => m != mainMat && m.name == subMatName);
                if (subMat == null)
                {
                    subMat = new Material(urpLit) { name = subMatName };
                    ApplyArmorMaterialSettings(subMat, urpLit, tex);
                    AssetDatabase.AddObjectToAsset(subMat, matPath);
                }
                else
                {
                    ApplyArmorMaterialSettings(subMat, urpLit, tex);
                    EditorUtility.SetDirty(subMat);
                }
                result[part.modelName] = subMat;
            }

            return result;
        }

        private static void ApplyArmorMaterialSettings(Material mat, Shader urpLit, Texture2D baseMap)
        {
            mat.shader = urpLit;
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);
            mat.mainTexture = baseMap;
            mat.color = Color.white;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
        }

        private static GameObject CreateOrUpdateVisualPrefab(
            ArmorItemSpec spec,
            string prefabPath,
            Dictionary<string, (Mesh single, Mesh mirror, float split, float twoLobe, bool wasCut)> meshCache,
            Dictionary<string, Material> partMats)
        {
            ArmorPartSpec primaryPart = spec.parts.FirstOrDefault(p => p.kind == PartKind.LegsThigh) ?? spec.parts[0];
            Mesh mesh = meshCache[primaryPart.modelName].single;
            Material mat = partMats[primaryPart.modelName];

            string rootName = Path.GetFileNameWithoutExtension(prefabPath);
            var root = new GameObject(rootName);
            try
            {
                var mf = root.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = root.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;

                ComputeWorldOrientations(primaryPart, mesh, out Quaternion worldRotL, out _, out _, out _);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = worldRotL;
                root.transform.localScale = Vector3.one * primaryPart.baseScale;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateOrUpdateEquippedPrefab(
            ArmorItemSpec spec,
            string prefabPath,
            Dictionary<string, (Mesh single, Mesh mirror, float split, float twoLobe, bool wasCut)> meshCache,
            Dictionary<string, Material> partMats,
            Animator refAnimator)
        {
            string rootName = Path.GetFileNameWithoutExtension(prefabPath);
            var root = new GameObject(rootName);
            try
            {
                foreach (var part in spec.parts)
                {
                    var (singleMesh, mirrorMesh, _, _, _) = meshCache[part.modelName];
                    Material mat = partMats[part.modelName];

                    ComputeWorldOrientations(part, singleMesh, out Quaternion worldRotL, out Quaternion worldRotR, out Vector3 worldOffL, out Vector3 worldOffR);

                    if (!part.isPaired)
                    {
                        var node = new GameObject(part.nodeName);
                        node.transform.SetParent(root.transform, false);
                        var mf = node.AddComponent<MeshFilter>();
                        mf.sharedMesh = singleMesh;
                        var mr = node.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = mat;
                        var mount = node.AddComponent<EquipmentMirrorMount>();
                        mount.bone = part.boneL;
                        ComputeBoneLocalMount(refAnimator, part.boneL, worldRotL, worldOffL, part.baseScale, singleMesh, out mount.localPosition, out mount.localEulerAngles, out mount.localScale);
                        node.transform.localPosition = mount.localPosition;
                        node.transform.localEulerAngles = mount.localEulerAngles;
                        node.transform.localScale = mount.localScale;
                    }
                    else
                    {
                        Mesh meshForL = part.swapLR ? mirrorMesh : singleMesh;
                        Mesh meshForR = part.swapLR ? singleMesh : mirrorMesh;

                        var nodeL = new GameObject($"{part.nodeName}_L");
                        nodeL.transform.SetParent(root.transform, false);
                        nodeL.AddComponent<MeshFilter>().sharedMesh = meshForL;
                        nodeL.AddComponent<MeshRenderer>().sharedMaterial = mat;
                        var mountL = nodeL.AddComponent<EquipmentMirrorMount>();
                        mountL.bone = part.boneL;
                        ComputeBoneLocalMount(refAnimator, part.boneL, worldRotL, worldOffL, part.baseScale, meshForL, out mountL.localPosition, out mountL.localEulerAngles, out mountL.localScale);
                        nodeL.transform.localPosition = mountL.localPosition;
                        nodeL.transform.localEulerAngles = mountL.localEulerAngles;
                        nodeL.transform.localScale = mountL.localScale;

                        var nodeR = new GameObject($"{part.nodeName}_R");
                        nodeR.transform.SetParent(root.transform, false);
                        nodeR.AddComponent<MeshFilter>().sharedMesh = meshForR;
                        nodeR.AddComponent<MeshRenderer>().sharedMaterial = mat;
                        var mountR = nodeR.AddComponent<EquipmentMirrorMount>();
                        mountR.bone = part.boneR;
                        ComputeBoneLocalMount(refAnimator, part.boneR, worldRotR, worldOffR, part.baseScale, meshForR, out mountR.localPosition, out mountR.localEulerAngles, out mountR.localScale);
                        nodeR.transform.localPosition = mountR.localPosition;
                        nodeR.transform.localEulerAngles = mountR.localEulerAngles;
                        nodeR.transform.localScale = mountR.localScale;
                    }
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ComputeBoneLocalMount(
            Animator refAnimator,
            HumanBodyBones bone,
            Quaternion desiredWorldRot,
            Vector3 desiredWorldOffsetFromBone,
            float scale,
            Mesh mesh,
            out Vector3 localPos,
            out Vector3 localEuler,
            out Vector3 localScale)
        {
            localScale = Vector3.one * scale;
            Transform bt = (refAnimator != null && bone != HumanBodyBones.LastBone) ? refAnimator.GetBoneTransform(bone) : null;
            if (bt == null)
            {
                localPos = desiredWorldOffsetFromBone;
                localEuler = desiredWorldRot.eulerAngles;
                return;
            }

            Vector3 meshCenterShift = mesh != null ? (desiredWorldRot * (mesh.bounds.center * scale)) : Vector3.zero;
            Vector3 targetWorldPos = bt.position + desiredWorldOffsetFromBone - meshCenterShift;
            localPos = bt.InverseTransformPoint(targetWorldPos);
            Quaternion localRot = Quaternion.Inverse(bt.rotation) * desiredWorldRot;
            localEuler = localRot.eulerAngles;
        }

        private static void ComputeWorldOrientations(
            ArmorPartSpec part,
            Mesh srcMesh,
            out Quaternion worldRotL,
            out Quaternion worldRotR,
            out Vector3 worldOffL,
            out Vector3 worldOffR)
        {
            Quaternion baseFront = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-90f, 0f, 0f);
            Quaternion baseBack = Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(-90f, 0f, 0f);

            worldOffL = part.worldOffsetL;
            worldOffR = part.worldOffsetR;

            switch (part.kind)
            {
                case PartKind.Head:
                case PartKind.Chest:
                case PartKind.Mantle:
                case PartKind.LegsBelt:
                case PartKind.UtilityBelt:
                    worldRotL = Quaternion.Euler(0f, part.yawOffsetL, 0f) * baseFront;
                    worldRotR = worldRotL;
                    break;
                case PartKind.Back:
                    worldRotL = Quaternion.Euler(0f, part.yawOffsetL, 0f) * baseBack;
                    worldRotR = worldRotL;
                    break;
                case PartKind.Feet:
                case PartKind.LegsKnee:
                case PartKind.LegsThigh:
                case PartKind.Shoulders:
                case PartKind.Hands:
                {
                    Quaternion rx = Quaternion.Euler(-90f, 0f, 0f);
                    worldRotL = Quaternion.Euler(part.pitchOffset, part.yawOffsetL, part.rollOffset) * rx;
                    Vector3 fwdL = worldRotL * Vector3.forward;
                    Vector3 upL = worldRotL * Vector3.up;
                    Vector3 fwdR = new Vector3(-fwdL.x, fwdL.y, fwdL.z);
                    Vector3 upR = new Vector3(-upL.x, upL.y, upL.z);
                    worldRotR = Quaternion.LookRotation(fwdR, upR);
                    break;
                }
                default:
                    worldRotL = baseFront;
                    worldRotR = baseFront;
                    break;
            }
        }

        private static ClothingItemData CreateOrUpdateClothingItem(
            ArmorItemSpec spec,
            string assetPath,
            GameObject visualPrefab,
            GameObject eqPrefab)
        {
            ClothingItemData item = AssetDatabase.LoadAssetAtPath<ClothingItemData>(assetPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ClothingItemData>();
                PopulateClothingItem(item, spec, visualPrefab, eqPrefab);
                AssetDatabase.CreateAsset(item, assetPath);
            }
            else
            {
                PopulateClothingItem(item, spec, visualPrefab, eqPrefab);
                EditorUtility.SetDirty(item);
            }
            return item;
        }

        private static void PopulateClothingItem(
            ClothingItemData item,
            ArmorItemSpec spec,
            GameObject visualPrefab,
            GameObject eqPrefab)
        {
            item.itemName = spec.displayName;
            item.itemType = ItemType.Equipment;
            item.description = $"Armor set {spec.line} ({spec.slotTag}), Tier {spec.tier}.";
            item.icon = LoadSlotIcon(spec.equipSlot);
            item.equipSlot = EquipSlot.None;
            item.slot = spec.equipSlot;
            item.maxStack = 1;
            item.tier = spec.tier;
            item.armorDefense = Mathf.RoundToInt(spec.baseDef * (1f + 0.15f * (spec.tier - 1)));
            item.weightKg = spec.weightKg;
            item.basePrice = 120 * spec.tier;
            item.visualPrefab = visualPrefab;
            item.equippedVisualPrefab = eqPrefab;
            item.attachBoneOverride = HumanBodyBones.LastBone;
            item.attachPositionOffset = Vector3.zero;
            item.attachRotationOffset = Vector3.zero;
            item.attachScale = Vector3.one;
        }

        private static Sprite LoadSlotIcon(EquipSlot slot)
        {
            string iconName = slot switch
            {
                EquipSlot.Head => "Icon_Slot_Head.png",
                EquipSlot.Chest => "Icon_Slot_Chest.png",
                EquipSlot.Legs => "Icon_Slot_Legs.png",
                EquipSlot.Feet => "Icon_Slot_Feet.png",
                EquipSlot.Back => "Icon_Slot_Back.png",
                EquipSlot.Hands => "Icon_Slot_Hands.png",
                EquipSlot.Accessory1 => "Icon_Slot_Accessory.png",
                EquipSlot.Accessory2 => "Icon_Slot_Accessory.png",
                _ => "Icon_Slot_Chest.png"
            };
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{SlotIconsDir}/{iconName}");
        }

        private static void UpdateItemRegistryAppendOnly(List<ClothingItemData> items)
        {
            ItemRegistry registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(ItemRegistryPath);
            if (registry == null)
            {
                Debug.LogError($"[ArmorKitBuilder] ItemRegistry not found at {ItemRegistryPath}");
                return;
            }

            var existingEntries = registry.GetEntries();
            int maxId = 0;
            var registeredSet = new HashSet<ItemData>();
            foreach (var e in existingEntries)
            {
                if (e.id > maxId) maxId = e.id;
                if (e.item != null) registeredSet.Add(e.item);
            }

            int added = 0;
            foreach (var item in items)
            {
                if (item == null || registeredSet.Contains(item)) continue;
                maxId++;
                registry.AddEntry(maxId, item);
                registeredSet.Add(item);
                added++;
            }

            if (added > 0)
            {
                EditorUtility.SetDirty(registry);
            }
            Debug.Log($"[ArmorKitBuilder] ItemRegistry updated (append-only): added {added} new items, total entries = {registry.Count}");
        }

        private static void CreateOrUpdatePickupArmorPrefab(ClothingItemData defaultItem)
        {
            var root = new GameObject("Pickup_Armor");
            try
            {
                root.AddComponent<NetworkObject>();
                var box = root.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 0.25f, 0f);
                box.size = new Vector3(0.6f, 0.6f, 0.6f);
                var pickup = root.AddComponent<PickupItem>();
                pickup.itemData = defaultItem;

                PrefabUtility.SaveAsPrefabAsset(root, PickupPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateOrUpdateArmorPreviewPrefab(List<ArmorItemSpec> specs)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/NetworkPlayer.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("[ArmorKitBuilder] NetworkPlayer.prefab not found!");
                return;
            }
            Animator playerAnim = playerPrefab.GetComponentInChildren<Animator>(true);
            GameObject bodyTemplate = playerAnim != null ? playerAnim.gameObject : playerPrefab;

            var previewRoot = new GameObject("ArmorPreview");
            try
            {
                var lineGroups = new List<(string label, List<ArmorItemSpec> items)>();
                string[] orderedLines = {
                    "01_STEEL", "02_COMPOSITE", "03_BRONZE", "04_AGRAV",
                    "05_MEDICAL", "05_MEDICAL_FIELD", "06_CHEMICAL", "07_THOUGHTS_ENGINEER",
                    "07_ENGINEER", "08_RESEARCH", "09_SUPERHEAVY", "09_SUPERHEAVY_LAMES",
                    "10_TANK", "10_TANK_LAMES", "11_RANGER", "12_CHROME",
                    "13_RUNIC", "14_KORU"
                };

                foreach (string line in orderedLines)
                {
                    var matching = specs.Where(s => s.line == line).ToList();
                    if (matching.Count > 0)
                    {
                        lineGroups.Add((line, matching));
                    }
                }

                const int Cols = 6;
                const float SpacingX = 1.45f;
                const float SpacingZ = 1.80f;

                for (int i = 0; i < lineGroups.Count; i++)
                {
                    var (lineLabel, groupItems) = lineGroups[i];
                    int col = i % Cols;
                    int row = i / Cols;
                    float x = (col - (Cols - 1) * 0.5f) * SpacingX;
                    float z = -row * SpacingZ;

                    var mannequinHolder = new GameObject($"Mannequin_{lineLabel}");
                    mannequinHolder.transform.SetParent(previewRoot.transform, false);
                    mannequinHolder.transform.localPosition = new Vector3(x, 0f, z);

                    GameObject bodyInst = UnityEngine.Object.Instantiate(bodyTemplate, mannequinHolder.transform, false);
                    bodyInst.name = "Body";
                    bodyInst.transform.localPosition = Vector3.zero;
                    bodyInst.transform.localRotation = Quaternion.identity;
                    bodyInst.transform.localScale = Vector3.one;

                    Animator anim = bodyInst.GetComponent<Animator>();
                    if (anim == null) anim = bodyInst.GetComponentInChildren<Animator>(true);

                    foreach (var spec in groupItems)
                    {
                        string eqPath = $"{PrefabDir}/PF_ARM_{spec.line}_{spec.slotTag}_Eq.prefab";
                        GameObject eqPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(eqPath);
                        if (eqPrefab == null || anim == null) continue;

                        GameObject eqInst = UnityEngine.Object.Instantiate(eqPrefab);
                        var mounts = eqInst.GetComponentsInChildren<EquipmentMirrorMount>(true);
                        foreach (var mount in mounts)
                        {
                            Transform bone = anim.GetBoneTransform(mount.bone);
                            if (bone == null) continue;
                            GameObject node = mount.gameObject;
                            node.name = $"{spec.line}_{spec.slotTag}_{node.name}";
                            node.transform.SetParent(bone, false);
                            node.transform.localPosition = mount.localPosition;
                            node.transform.localEulerAngles = mount.localEulerAngles;
                            node.transform.localScale = mount.localScale;
                        }
                        UnityEngine.Object.DestroyImmediate(eqInst);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(previewRoot, PreviewPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(previewRoot);
            }
        }

        public static List<ArmorItemSpec> GetAllItemSpecs()
        {
            var list = new List<ArmorItemSpec>();

            int GetTier(string line) => line switch
            {
                "01_STEEL" => 1,
                "02_COMPOSITE" => 2,
                "03_BRONZE" => 3,
                "04_AGRAV" => 4,
                "05_MEDICAL" => 5,
                "05_MEDICAL_FIELD" => 5,
                "06_CHEMICAL" => 3,
                "07_THOUGHTS_ENGINEER" => 5,
                "07_ENGINEER" => 5,
                "08_RESEARCH" => 4,
                "09_SUPERHEAVY" => 8,
                "09_SUPERHEAVY_LAMES" => 8,
                "10_TANK" => 9,
                "10_TANK_LAMES" => 9,
                "11_RANGER" => 6,
                "12_CHROME" => 7,
                "13_RUNIC" => 7,
                "14_KORU" => 10,
                _ => 1
            };

            // 1. HEAD (4)
            (string line, string model)[] heads = {
                ("01_STEEL", "CL_F_HEAD_HELMET_01_STEEL"),
                ("02_COMPOSITE", "CL_F_HEAD_HELMET_02_COMPOSITE"),
                ("03_BRONZE", "CL_F_HEAD_HELMET_03_BRONZE"),
                ("04_AGRAV", "CL_F_HEAD_HELMET_04_AGRAV"),
            };
            foreach (var h in heads)
            {
                list.Add(new ArmorItemSpec
                {
                    line = h.line,
                    slotTag = "HEAD",
                    displayName = $"Clothing_ARM_{h.line}_HEAD",
                    equipSlot = EquipSlot.Head,
                    tier = GetTier(h.line),
                    baseDef = 4,
                    weightKg = 1.6f,
                    parts = {
                        new ArmorPartSpec
                        {
                            categoryFolder = "Head_Helmet",
                            modelName = h.model,
                            kind = PartKind.Head,
                            isPaired = false,
                            nodeName = "HEAD",
                            boneL = HumanBodyBones.Head,
                            baseScale = 0.30f,
                            worldOffsetL = new Vector3(0f, 0.08f, 0.01f)
                        }
                    }
                });
            }

            // 2. CHEST (15)
            (string line, string model)[] chests = {
                ("01_STEEL", "CL_F_CHEST_PLATE_01_STEEL"),
                ("02_COMPOSITE", "CL_F_CHEST_PLATE_02_COMPOSITE"),
                ("03_BRONZE", "CL_F_CHEST_PLATE_03_BRONZE"),
                ("04_AGRAV", "CL_F_CHEST_PLATE_04_AGRAV"),
                ("05_MEDICAL", "CL_F_CHEST_PLATE_05_MEDICAL"),
                ("05_MEDICAL_FIELD", "CL_F_CHEST_PLATE_05_MEDICAL_FIELD"),
                ("07_THOUGHTS_ENGINEER", "CL_F_CHEST_PLATE_07_THOUGHTS_ENGINEER_v2"),
                ("09_SUPERHEAVY", "CL_F_CHEST_PLATE_09_SUPERHEAVY_v3b"),
                ("09_SUPERHEAVY_LAMES", "CL_F_CHEST_LAMES_09_SUPERHEAVY_v3b"),
                ("10_TANK", "CL_F_CHEST_PLATE_10_TANK"),
                ("10_TANK_LAMES", "CL_F_CHEST_LAMES_10_TANK"),
                ("11_RANGER", "CL_F_CHEST_PLATE_11_RANGER"),
                ("12_CHROME", "CL_F_CHEST_PLATE_12_CHROME"),
                ("13_RUNIC", "CL_F_CHEST_PLATE_13_RUNIC"),
                ("14_KORU", "CL_F_CHEST_PLATE_14_KORU"),
            };
            foreach (var c in chests)
            {
                bool isLames = c.line.EndsWith("_LAMES", StringComparison.Ordinal);
                float chestYaw = c.line == "10_TANK_LAMES" ? 90f : 0f;
                list.Add(new ArmorItemSpec
                {
                    line = c.line,
                    slotTag = "CHEST",
                    displayName = $"Clothing_ARM_{c.line}_CHEST",
                    equipSlot = EquipSlot.Chest,
                    tier = GetTier(c.line),
                    baseDef = 9,
                    weightKg = 6.5f,
                    parts = {
                        new ArmorPartSpec
                        {
                            categoryFolder = "Chest",
                            modelName = c.model,
                            kind = PartKind.Chest,
                            isPaired = false,
                            nodeName = "CHEST",
                            boneL = HumanBodyBones.Spine,
                            baseScale = 0.55f,
                            yawOffsetL = chestYaw,
                            worldOffsetL = isLames ? new Vector3(0f, 0.06f, 0.00f) : new Vector3(0f, 0.21f, 0.00f)
                        }
                    }
                });
            }

            // 3. SHOULDERS (13)
            (string line, string model, bool isMantle, float yawL, float yawR, bool swap)[] shoulders = {
                ("01_STEEL", "CL_F_ACC_PAULDRON_01", false, 0f, 0f, false),
                ("02_COMPOSITE", "CL_F_ACC_PAULDRON_02_COMPOSITE", false, 0f, 0f, false),
                ("03_BRONZE", "CL_F_ACC_PAULDRON_03_BRONZE", false, 0f, 0f, false),
                ("04_AGRAV", "CL_F_ACC_PAULDRON_04_AGRAV", false, 0f, 0f, false),
                ("05_MEDICAL", "CL_F_ACC_PAULDRON_05_MEDICAL", false, 0f, 0f, false),
                ("05_MEDICAL_FIELD", "CL_F_ACC_PAULDRON_05_MEDICAL_FIELD", false, 0f, 0f, false),
                ("07_THOUGHTS_ENGINEER", "CL_F_ACC_PAULDRON_07_THOUGHTS_ENGINEER_v2", false, 0f, 0f, false),
                ("09_SUPERHEAVY", "CL_F_ACC_PAULDRON_09_SUPERHEAVY_v3b", false, 0f, 0f, false),
                ("10_TANK", "CL_F_ACC_PAULDRON_10_TANK", false, 0f, 0f, false),
                ("11_RANGER", "CL_F_ACC_MANTLE_11_RANGER", true, 0f, 0f, false),
                ("12_CHROME", "CL_F_ACC_PAULDRON_12_CHROME", false, 0f, 0f, false),
                ("13_RUNIC", "CL_F_ACC_PAULDRON_13_RUNIC", false, 0f, 0f, false),
                ("14_KORU", "CL_F_ACC_PAULDRON_14_KORU", false, 0f, 0f, false),
            };
            foreach (var s in shoulders)
            {
                if (s.isMantle)
                {
                    list.Add(new ArmorItemSpec
                    {
                        line = s.line,
                        slotTag = "SHOULDERS",
                        displayName = $"Clothing_ARM_{s.line}_SHOULDERS",
                        equipSlot = EquipSlot.Accessory1,
                        tier = GetTier(s.line),
                        baseDef = 2,
                        weightKg = 1.4f,
                        parts = {
                            new ArmorPartSpec
                            {
                                categoryFolder = "Shoulders",
                                modelName = s.model,
                                kind = PartKind.Mantle,
                                isPaired = false,
                                nodeName = "MANTLE",
                                boneL = HumanBodyBones.Spine,
                                baseScale = 0.55f,
                                worldOffsetL = new Vector3(0f, 0.34f, -0.01f)
                            }
                        }
                    });
                }
                else
                {
                    list.Add(new ArmorItemSpec
                    {
                        line = s.line,
                        slotTag = "SHOULDERS",
                        displayName = $"Clothing_ARM_{s.line}_SHOULDERS",
                        equipSlot = EquipSlot.Accessory1,
                        tier = GetTier(s.line),
                        baseDef = 2,
                        weightKg = 1.4f,
                        parts = {
                            new ArmorPartSpec
                            {
                                categoryFolder = "Shoulders",
                                modelName = s.model,
                                kind = PartKind.Shoulders,
                                isPaired = true,
                                nodeName = "SHOULDERS",
                                boneL = HumanBodyBones.LeftShoulder,
                                boneR = HumanBodyBones.RightShoulder,
                                baseScale = 0.24f,
                                yawOffsetL = s.yawL,
                                yawOffsetR = s.yawR,
                                swapLR = s.swap,
                                worldOffsetL = new Vector3(-0.20f, 0.01f, -0.01f),
                                worldOffsetR = new Vector3(0.20f, 0.01f, -0.01f)
                            }
                        }
                    });
                }
            }

            // 4. HANDS (6)
            (string line, string model, float yawL, float yawR, float pitch, float roll, Vector3 offL, Vector3 offR)[] hands = {
                ("09_SUPERHEAVY", "CL_F_HANDS_GAUNTLET_09_SUPERHEAVY_v3b", 0f, 0f, 0f, 90f, new Vector3(0.02f, 0.00f, 0f), new Vector3(-0.02f, 0.00f, 0f)),
                ("10_TANK", "CL_F_HANDS_GAUNTLET_10_TANK", -90f, 0f, 0f, 0f, new Vector3(0.08f, 0.00f, 0f), new Vector3(-0.08f, 0.00f, 0f)),
                ("11_RANGER", "CL_F_HANDS_BRACER_11_RANGER_v2", 0f, 0f, 0f, 90f, new Vector3(0.09f, 0.00f, 0f), new Vector3(-0.09f, 0.00f, 0f)),
                ("12_CHROME", "CL_F_HANDS_BRACER_12_CHROME", 0f, 0f, 0f, 0f, new Vector3(0.09f, 0.00f, 0f), new Vector3(-0.09f, 0.00f, 0f)),
                ("13_RUNIC", "CL_F_HANDS_BRACER_13_RUNIC", 0f, 0f, 0f, 90f, new Vector3(0.09f, 0.00f, 0f), new Vector3(-0.09f, 0.00f, 0f)),
                ("14_KORU", "CL_F_HANDS_BRACER_14_KORU", 0f, 0f, 0f, 0f, new Vector3(0.09f, 0.00f, 0f), new Vector3(-0.09f, 0.00f, 0f)),
            };
            foreach (var h in hands)
            {
                list.Add(new ArmorItemSpec
                {
                    line = h.line,
                    slotTag = "HANDS",
                    displayName = $"Clothing_ARM_{h.line}_HANDS",
                    equipSlot = EquipSlot.Hands,
                    tier = GetTier(h.line),
                    baseDef = 2,
                    weightKg = 0.7f,
                    parts = {
                        new ArmorPartSpec
                        {
                            categoryFolder = "Hands",
                            modelName = h.model,
                            kind = PartKind.Hands,
                            isPaired = true,
                            nodeName = "HANDS",
                            boneL = HumanBodyBones.LeftHand,
                            boneR = HumanBodyBones.RightHand,
                            baseScale = 0.20f,
                            yawOffsetL = h.yawL,
                            yawOffsetR = h.yawR,
                            pitchOffset = h.pitch,
                            rollOffset = h.roll,
                            worldOffsetL = h.offL,
                            worldOffsetR = h.offR
                        }
                    }
                });
            }

            // 5. FEET (13)
            (string line, string model, float yaw)[] feet = {
                ("01_STEEL", "CL_F_FEET_BOOT_01_STEEL", 0f),
                ("02_COMPOSITE", "CL_F_FEET_BOOT_03_COMPOSITE", 0f),
                ("03_BRONZE", "CL_F_FEET_BOOT_04_BRONZE", 0f),
                ("04_AGRAV", "CL_F_FEET_BOOT_05_AGRAV", 0f),
                ("05_MEDICAL", "CL_F_FEET_BOOT_05_MEDICAL", 0f),
                ("05_MEDICAL_FIELD", "CL_F_FEET_BOOT_05_MEDICAL_FIELD", 0f),
                ("07_THOUGHTS_ENGINEER", "CL_F_FEET_BOOT_07_THOUGHTS_ENGINEER_v2", 0f),
                ("09_SUPERHEAVY", "CL_F_FEET_BOOT_09_SUPERHEAVY_v3b", 0f),
                ("10_TANK", "CL_F_FEET_BOOT_10_TANK", 0f),
                ("11_RANGER", "CL_F_FEET_BOOT_11_RANGER", 0f),
                ("12_CHROME", "CL_F_FEET_BOOT_12_CHROME", 0f),
                ("13_RUNIC", "CL_F_FEET_BOOT_13_RUNIC", 0f),
                ("14_KORU", "CL_F_FEET_BOOT_14_KORU", 0f),
            };
            foreach (var f in feet)
            {
                list.Add(new ArmorItemSpec
                {
                    line = f.line,
                    slotTag = "FEET",
                    displayName = $"Clothing_ARM_{f.line}_FEET",
                    equipSlot = EquipSlot.Feet,
                    tier = GetTier(f.line),
                    baseDef = 3,
                    weightKg = 1.3f,
                    parts = {
                        new ArmorPartSpec
                        {
                            categoryFolder = "Feet",
                            modelName = f.model,
                            kind = PartKind.Feet,
                            isPaired = true,
                            nodeName = "FEET",
                            boneL = HumanBodyBones.LeftFoot,
                            boneR = HumanBodyBones.RightFoot,
                            baseScale = 0.36f,
                            yawOffsetL = f.yaw,
                            yawOffsetR = f.yaw,
                            worldOffsetL = new Vector3(0f, 0.04f, 0.03f),
                            worldOffsetR = new Vector3(0f, 0.04f, 0.03f)
                        }
                    }
                });
            }

            // 6. BACK (8)
            (string line, string model)[] backs = {
                ("01_STEEL", "CL_F_AIRTANK_01_STEEL"),
                ("02_COMPOSITE", "CL_F_AIRTANK_02_COMPOSITE"),
                ("03_BRONZE", "CL_F_AIRTANK_03_BRONZE"),
                ("04_AGRAV", "CL_F_AIRTANK_04_AGRAV"),
                ("05_MEDICAL", "CL_F_BACKPACK_05_MEDICAL"),
                ("06_CHEMICAL", "CL_F_BACKPACK_06_CHEMICAL"),
                ("07_ENGINEER", "CL_F_BACKPACK_07_ENGINEER"),
                ("08_RESEARCH", "CL_F_BACKPACK_08_RESEARCH"),
            };
            foreach (var b in backs)
            {
                list.Add(new ArmorItemSpec
                {
                    line = b.line,
                    slotTag = "BACK",
                    displayName = $"Clothing_ARM_{b.line}_BACK",
                    equipSlot = EquipSlot.Back,
                    tier = GetTier(b.line),
                    baseDef = 2,
                    weightKg = 2.6f,
                    parts = {
                        new ArmorPartSpec
                        {
                            categoryFolder = "Backpacks_Airtanks",
                            modelName = b.model,
                            kind = PartKind.Back,
                            isPaired = false,
                            nodeName = "BACK",
                            boneL = HumanBodyBones.Spine,
                            baseScale = 0.55f,
                            worldOffsetL = new Vector3(0f, 0.22f, -0.17f)
                        }
                    }
                });
            }

            // 7. UTILITY BELTS (4)
            (string line, string model, float yaw)[] uBelts = {
                ("05_MEDICAL", "CL_F_BELT_05_MEDICAL", 180f),
                ("06_CHEMICAL", "CL_F_BELT_06_CHEMICAL", 180f),
                ("07_ENGINEER", "CL_F_BELT_07_ENGINEER", 180f),
                ("08_RESEARCH", "CL_F_BELT_08_RESEARCH", 0f),
            };
            foreach (var ub in uBelts)
            {
                list.Add(new ArmorItemSpec
                {
                    line = ub.line,
                    slotTag = "BELT",
                    displayName = $"Clothing_ARM_{ub.line}_BELT",
                    equipSlot = EquipSlot.Accessory2,
                    tier = GetTier(ub.line),
                    baseDef = 1,
                    weightKg = 0.9f,
                    parts = {
                        new ArmorPartSpec
                        {
                            categoryFolder = "Utility_Belts",
                            modelName = ub.model,
                            kind = PartKind.UtilityBelt,
                            isPaired = false,
                            nodeName = "BELT",
                            boneL = HumanBodyBones.Hips,
                            baseScale = 0.44f,
                            yawOffsetL = ub.yaw,
                            worldOffsetL = new Vector3(0f, 0.03f, 0f)
                        }
                    }
                });
            }

            // 8. LEGS (13)
            (string line, string beltModel, string thighModel, string kneeModel)[] legs = {
                ("01_STEEL", "CL_F_LEGS_BELT_01_STEEL", "CL_F_LEGS_THIGH_01_STEEL", null),
                ("02_COMPOSITE", "CL_F_LEGS_BELT_02_COMPOSITE", "CL_F_LEGS_THIGH_02_COMPOSITE", null),
                ("03_BRONZE", "CL_F_LEGS_BELT_03_BRONZE", "CL_F_LEGS_THIGH_03_BRONZE", null),
                ("04_AGRAV", "CL_F_LEGS_BELT_04_AGRAV", "CL_F_LEGS_THIGH_04_AGRAV", null),
                ("05_MEDICAL", null, "CL_F_LEGS_THIGH_05_MEDICAL", "CL_F_LEGS_KNEE_05_MEDICAL"),
                ("05_MEDICAL_FIELD", null, "CL_F_LEGS_THIGH_05_MEDICAL_FIELD", "CL_F_LEGS_KNEE_05_MEDICAL_FIELD"),
                ("07_THOUGHTS_ENGINEER", null, "CL_F_LEGS_THIGH_07_THOUGHTS_ENGINEER_v2", "CL_F_LEGS_KNEE_07_THOUGHTS_ENGINEER_v2"),
                ("09_SUPERHEAVY", null, "CL_F_LEGS_THIGH_09_SUPERHEAVY_v3b", "CL_F_LEGS_KNEE_09_SUPERHEAVY_v3b"),
                ("10_TANK", null, "CL_F_LEGS_THIGH_10_TANK", "CL_F_LEGS_KNEE_10_TANK"),
                ("11_RANGER", "CL_F_LEGS_BELT_11_RANGER", "CL_F_LEGS_THIGH_11_RANGER", "CL_F_LEGS_KNEE_11_RANGER_v2"),
                ("12_CHROME", "CL_F_LEGS_BELT_12_CHROME", "CL_F_LEGS_THIGH_12_CHROME", "CL_F_LEGS_KNEE_12_CHROME"),
                ("13_RUNIC", "CL_F_LEGS_BELT_13_RUNIC", "CL_F_LEGS_THIGH_13_RUNIC", "CL_F_LEGS_KNEE_13_RUNIC"),
                ("14_KORU", "CL_F_LEGS_BELT_14_KORU", "CL_F_LEGS_THIGH_14_KORU", "CL_F_LEGS_KNEE_14_KORU"),
            };

            foreach (var lg in legs)
            {
                var spec = new ArmorItemSpec
                {
                    line = lg.line,
                    slotTag = "LEGS",
                    displayName = $"Clothing_ARM_{lg.line}_LEGS",
                    equipSlot = EquipSlot.Legs,
                    tier = GetTier(lg.line),
                    baseDef = 7,
                    weightKg = 5.0f
                };

                if (!string.IsNullOrEmpty(lg.beltModel))
                {
                    spec.parts.Add(new ArmorPartSpec
                    {
                        categoryFolder = "Legs",
                        modelName = lg.beltModel,
                        kind = PartKind.LegsBelt,
                        isPaired = false,
                        nodeName = "BELT",
                        boneL = HumanBodyBones.Hips,
                        baseScale = 0.48f,
                        worldOffsetL = new Vector3(0f, 0.03f, 0f)
                    });
                }

                if (!string.IsNullOrEmpty(lg.thighModel))
                {
                    GetThighOrientation(lg.thighModel, out float yawL, out float yawR, out bool swap);
                    spec.parts.Add(new ArmorPartSpec
                    {
                        categoryFolder = "Legs",
                        modelName = lg.thighModel,
                        kind = PartKind.LegsThigh,
                        isPaired = true,
                        nodeName = "THIGH",
                        boneL = HumanBodyBones.LeftUpperLeg,
                        boneR = HumanBodyBones.RightUpperLeg,
                        baseScale = 0.46f,
                        yawOffsetL = yawL,
                        yawOffsetR = yawR,
                        swapLR = swap,
                        worldOffsetL = new Vector3(0.00f, -0.17f, 0.00f),
                        worldOffsetR = new Vector3(0.00f, -0.17f, 0.00f)
                    });
                }

                if (!string.IsNullOrEmpty(lg.kneeModel))
                {
                    GetKneeOrientation(lg.kneeModel, out float yawL, out float yawR, out bool swap);
                    spec.parts.Add(new ArmorPartSpec
                    {
                        categoryFolder = "Legs",
                        modelName = lg.kneeModel,
                        kind = PartKind.LegsKnee,
                        isPaired = true,
                        nodeName = "KNEE",
                        boneL = HumanBodyBones.LeftLowerLeg,
                        boneR = HumanBodyBones.RightLowerLeg,
                        baseScale = 0.20f,
                        yawOffsetL = yawL,
                        yawOffsetR = yawR,
                        swapLR = swap,
                        worldOffsetL = new Vector3(0.00f, -0.02f, 0.04f),
                        worldOffsetR = new Vector3(0.00f, -0.02f, 0.04f)
                    });
                }

                list.Add(spec);
            }

            return list;
        }

        private static void GetThighOrientation(string model, out float yawL, out float yawR, out bool swap)
        {
            switch (model)
            {
                case "CL_F_LEGS_THIGH_10_TANK":
                    yawL = 90f; yawR = 0f; swap = false;
                    break;
                default:
                    yawL = 0f; yawR = 0f; swap = false;
                    break;
            }
        }

        private static void GetKneeOrientation(string model, out float yawL, out float yawR, out bool swap)
        {
            yawL = 90f;
            yawR = 0f;
            swap = false;
        }

        [MenuItem("Tools/Project C/Armor/0. Fix FBX Scale & Inspect Meshes")]
        public static void FixFbxScaleAndInspect()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ArmorSourceRoot });
            var fbxPaths = new List<string>();
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith("_Dec.fbx", StringComparison.OrdinalIgnoreCase))
                {
                    fbxPaths.Add(p);
                }
            }
            fbxPaths.Sort(StringComparer.Ordinal);

            int changedCount = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in fbxPaths)
                {
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null) continue;
                    bool dirty = false;
                    if (!importer.useFileScale)
                    {
                        importer.useFileScale = true;
                        dirty = true;
                    }
                    if (Mathf.Abs(importer.globalScale - 100f) > 0.001f)
                    {
                        importer.globalScale = 100f;
                        dirty = true;
                    }
                    if (dirty)
                    {
                        importer.SaveAndReimport();
                        changedCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (changedCount > 0)
            {
                AssetDatabase.Refresh();
            }

            Directory.CreateDirectory(ReportDir);
            var sb = new StringBuilder();
            sb.AppendLine("# Mesh Inspection Report (93 Decimated Armor Models)");
            sb.AppendLine($"Total FBX found: {fbxPaths.Count}, Reimported with globalScale=100: {changedCount}");
            sb.AppendLine();
            sb.AppendLine("| Model | globalScale | maxDim | Size (X,Y,Z) | Center (X,Y,Z) | Tris | Split | TwoLobeXY | TwoLobeXZ | CrossCenterTris | Components |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");

            foreach (string path in fbxPaths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    var allObjs = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var o in allObjs)
                    {
                        if (o is Mesh m) { mesh = m; break; }
                    }
                }
                if (mesh == null)
                {
                    sb.AppendLine($"| {Path.GetFileNameWithoutExtension(path)} | ERR | NO MESH | - | - | - | - | - | - | - | - |");
                    continue;
                }

                Bounds b = mesh.bounds;
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                AnalyzeMeshSymmetry(mesh, out int triCount, out float split, out float twoLobeXY, out float twoLobeXZ, out int crossCenterTris, out int compCount);
                string modelName = Path.GetFileNameWithoutExtension(path);
                float gs = importer != null ? importer.globalScale : -1f;
                sb.AppendLine($"| {modelName} | {gs:F0} | {maxDim:F4} | ({b.size.x:F3}, {b.size.y:F3}, {b.size.z:F3}) | ({b.center.x:F3}, {b.center.y:F3}, {b.center.z:F3}) | {triCount} | {split:F3} | {twoLobeXY:F3} | {twoLobeXZ:F3} | {crossCenterTris} | {compCount} |");
            }

            string outPath = Path.Combine(ReportDir, "mesh_inspection.md");
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[ArmorKitBuilder] Fixed {changedCount}/{fbxPaths.Count} FBX globalScale=100. Report written to {outPath}");
        }

        public static void AnalyzeMeshSymmetry(
            Mesh mesh,
            out int triCount,
            out float split,
            out float twoLobeXY,
            out float twoLobeXZ,
            out int crossCenterTris,
            out int connectedComponents)
        {
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            triCount = tris.Length / 3;
            Bounds b = mesh.bounds;
            float cx = b.center.x;
            float deadzone = b.extents.x * 0.10f;

            int leftCount = 0;
            int rightCount = 0;
            crossCenterTris = 0;

            const int Slices = 20;
            bool[] xyHasLeft = new bool[Slices];
            bool[] xyHasRight = new bool[Slices];
            bool[] xyHasCenter = new bool[Slices];

            bool[] xzHasLeft = new bool[Slices];
            bool[] xzHasRight = new bool[Slices];
            bool[] xzHasCenter = new bool[Slices];

            float minY = b.min.y, sizeY = Mathf.Max(1e-5f, b.size.y);
            float minZ = b.min.z, sizeZ = Mathf.Max(1e-5f, b.size.z);

            for (int t = 0; t < triCount; t++)
            {
                Vector3 v0 = verts[tris[t * 3]];
                Vector3 v1 = verts[tris[t * 3 + 1]];
                Vector3 v2 = verts[tris[t * 3 + 2]];
                Vector3 c = (v0 + v1 + v2) / 3f;

                if (c.x < cx) leftCount++;
                else rightCount++;

                float tMinX = Mathf.Min(v0.x, Mathf.Min(v1.x, v2.x));
                float tMaxX = Mathf.Max(v0.x, Mathf.Max(v1.x, v2.x));
                bool crossesCenter = (tMinX <= cx + deadzone && tMaxX >= cx - deadzone);
                if (tMinX < cx && tMaxX > cx) crossCenterTris++;

                int sy = Mathf.Clamp((int)(((c.y - minY) / sizeY) * Slices), 0, Slices - 1);
                int sz = Mathf.Clamp((int)(((c.z - minZ) / sizeZ) * Slices), 0, Slices - 1);

                if (c.x < cx - deadzone)
                {
                    xyHasLeft[sy] = true;
                    xzHasLeft[sz] = true;
                }
                else if (c.x > cx + deadzone)
                {
                    xyHasRight[sy] = true;
                    xzHasRight[sz] = true;
                }
                else
                {
                    xyHasCenter[sy] = true;
                    xzHasCenter[sz] = true;
                }

                if (crossesCenter)
                {
                    xyHasCenter[sy] = true;
                    xzHasCenter[sz] = true;
                }
            }

            split = triCount > 0 ? (float)Mathf.Min(leftCount, rightCount) / triCount : 0f;

            int xyActive = 0, xyTwoLobe = 0;
            int xzActive = 0, xzTwoLobe = 0;
            for (int i = 0; i < Slices; i++)
            {
                if (xyHasLeft[i] || xyHasRight[i] || xyHasCenter[i])
                {
                    xyActive++;
                    if (xyHasLeft[i] && xyHasRight[i] && !xyHasCenter[i]) xyTwoLobe++;
                }
                if (xzHasLeft[i] || xzHasRight[i] || xzHasCenter[i])
                {
                    xzActive++;
                    if (xzHasLeft[i] && xzHasRight[i] && !xzHasCenter[i]) xzTwoLobe++;
                }
            }

            twoLobeXY = xyActive > 0 ? (float)xyTwoLobe / xyActive : 0f;
            twoLobeXZ = xzActive > 0 ? (float)xzTwoLobe / xzActive : 0f;

            // Count connected components along X (left vs right clusters) using spatial welding
            connectedComponents = CountSpatialLeftRightIslands(verts, tris, cx);
        }

        private static int CountSpatialLeftRightIslands(Vector3[] verts, int[] tris, float cx)
        {
            // Check if any triangle spans across cx
            int triCount = tris.Length / 3;
            bool hasLeft = false, hasRight = false, bridges = false;
            for (int t = 0; t < triCount; t++)
            {
                float x0 = verts[tris[t * 3]].x;
                float x1 = verts[tris[t * 3 + 1]].x;
                float x2 = verts[tris[t * 3 + 2]].x;
                float minX = Mathf.Min(x0, Mathf.Min(x1, x2));
                float maxX = Mathf.Max(x0, Mathf.Max(x1, x2));
                if (maxX < cx) hasLeft = true;
                else if (minX > cx) hasRight = true;
                else bridges = true;
            }
            if (!bridges && hasLeft && hasRight) return 2;
            return 1;
        }

        [MenuItem("Tools/Project C/Armor/0b. Inspect Topology & Skeleton Bones")]
        public static void InspectTopologyAndBones()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Skeleton & Topology Inspection");

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/NetworkPlayer.prefab");
            if (playerPrefab != null)
            {
                var anim = playerPrefab.GetComponentInChildren<Animator>(true);
                if (anim != null && anim.isHuman)
                {
                    sb.AppendLine("## NetworkPlayer Humanoid Bones");
                    HumanBodyBones[] testBones = {
                        HumanBodyBones.Head, HumanBodyBones.Spine, HumanBodyBones.Hips,
                        HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
                        HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
                        HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
                        HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
                        HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
                        HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
                        HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot
                    };
                    foreach (var hb in testBones)
                    {
                        Transform bt = anim.GetBoneTransform(hb);
                        if (bt != null)
                        {
                            sb.AppendLine($"- {hb} ({bt.name}): worldPos={bt.position:F3}, worldEuler={bt.eulerAngles:F1}, localScale={bt.localScale:F3}, lossyScale={bt.lossyScale:F3}, up={bt.up:F2}, forward={bt.forward:F2}, right={bt.right:F2}");
                        }
                    }
                }
            }

            string sampleFbx = "Assets/Generated_Models_Rebaked_Decimated/02_Armor/Chest/CL_F_CHEST_PLATE_01_STEEL/CL_F_CHEST_PLATE_01_STEEL_Dec.fbx";
            var fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>(sampleFbx);
            if (fbxGo != null)
            {
                sb.AppendLine();
                sb.AppendLine($"## Sample FBX Root GameObject ({fbxGo.name})");
                sb.AppendLine($"- root localRotation={fbxGo.transform.localEulerAngles}, localScale={fbxGo.transform.localScale}");
                foreach (Transform child in fbxGo.transform)
                {
                    sb.AppendLine($"- child '{child.name}' localRotation={child.localEulerAngles}, localScale={child.localScale}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("## Paired Models Topological Islands & Center Gap Analysis");
            sb.AppendLine("| Model | TotalTris | Islands (Major >=5% tris) | LeftIslandTris | RightIslandTris | SplitFormula | GapTwoLobeXY | GapTwoLobeXZ |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ArmorSourceRoot });
            var fbxPaths = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith("_Dec.fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            foreach (string path in fbxPaths)
            {
                if (!path.Contains("/Shoulders/") && !path.Contains("/Hands/") && !path.Contains("/Feet/") && !path.Contains("/Legs/"))
                    continue;
                if (path.Contains("LEGS_BELT") || path.Contains("ACC_MANTLE"))
                    continue;

                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) continue;

                AnalyzeTopologicalIslands(mesh, out int totalTris, out int majorIslands, out int leftIslandTris, out int rightIslandTris, out float split, out float gapXY, out float gapXZ);
                sb.AppendLine($"| {Path.GetFileNameWithoutExtension(path)} | {totalTris} | {majorIslands} | {leftIslandTris} | {rightIslandTris} | {split:F3} | {gapXY:F3} | {gapXZ:F3} |");
            }

            string outPath = Path.Combine(ReportDir, "topology_and_bones.md");
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[ArmorKitBuilder] Wrote topology & bones report to {outPath}");
        }

        private static void AnalyzeTopologicalIslands(
            Mesh mesh,
            out int totalTris,
            out int majorIslands,
            out int leftIslandTris,
            out int rightIslandTris,
            out float split,
            out float gapXY,
            out float gapXZ)
        {
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            totalTris = tris.Length / 3;
            Bounds b = mesh.bounds;
            float cx = b.center.x;

            int nVerts = verts.Length;
            int[] parent = new int[nVerts];
            for (int i = 0; i < nVerts; i++) parent[i] = i;
            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }
            void Union(int a, int bIdx)
            {
                int ra = Find(a), rb = Find(bIdx);
                if (ra != rb) parent[ra] = rb;
            }

            var posMap = new Dictionary<(int, int, int), int>();
            for (int i = 0; i < nVerts; i++)
            {
                Vector3 v = verts[i];
                var key = (Mathf.RoundToInt(v.x * 2000f), Mathf.RoundToInt(v.y * 2000f), Mathf.RoundToInt(v.z * 2000f));
                if (posMap.TryGetValue(key, out int prev)) Union(i, prev);
                else posMap[key] = i;
            }
            for (int t = 0; t < totalTris; t++)
            {
                int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];
                Union(i0, i1);
                Union(i1, i2);
            }

            var compTris = new Dictionary<int, int>();
            var compSumX = new Dictionary<int, float>();
            int leftTris = 0, rightTris = 0;
            for (int t = 0; t < totalTris; t++)
            {
                int i0 = tris[t * 3], i1 = tris[t * 3 + 1], i2 = tris[t * 3 + 2];
                Vector3 c = (verts[i0] + verts[i1] + verts[i2]) / 3f;
                if (c.x < cx) leftTris++; else rightTris++;
                int root = Find(i0);
                compTris[root] = compTris.TryGetValue(root, out int ct) ? ct + 1 : 1;
                compSumX[root] = compSumX.TryGetValue(root, out float sx) ? sx + c.x : c.x;
            }

            split = totalTris > 0 ? (float)Mathf.Min(leftTris, rightTris) / totalTris : 0f;
            majorIslands = 0;
            leftIslandTris = 0;
            rightIslandTris = 0;
            foreach (var kv in compTris)
            {
                if (kv.Value >= totalTris * 0.05f) majorIslands++;
                float avgX = compSumX[kv.Key] / kv.Value;
                if (avgX < cx) leftIslandTris += kv.Value;
                else rightIslandTris += kv.Value;
            }

            const int Slices = 20;
            float band = b.size.x * 0.05f;
            int activeY = 0, twoLobeY = 0;
            int activeZ = 0, twoLobeZ = 0;
            for (int s = 0; s < Slices; s++)
            {
                float y0 = b.min.y + (b.size.y * s) / Slices;
                float y1 = b.min.y + (b.size.y * (s + 1)) / Slices;
                bool lY = false, rY = false, cY = false;

                float z0 = b.min.z + (b.size.z * s) / Slices;
                float z1 = b.min.z + (b.size.z * (s + 1)) / Slices;
                bool lZ = false, rZ = false, cZ = false;

                for (int t = 0; t < totalTris; t++)
                {
                    Vector3 v0 = verts[tris[t * 3]], v1 = verts[tris[t * 3 + 1]], v2 = verts[tris[t * 3 + 2]];
                    Vector3 c = (v0 + v1 + v2) / 3f;
                    float minX = Mathf.Min(v0.x, Mathf.Min(v1.x, v2.x));
                    float maxX = Mathf.Max(v0.x, Mathf.Max(v1.x, v2.x));
                    bool hitsCenter = (minX <= cx + band && maxX >= cx - band);

                    if (c.y >= y0 && c.y <= y1)
                    {
                        if (hitsCenter) cY = true;
                        else if (c.x < cx - band) lY = true;
                        else if (c.x > cx + band) rY = true;
                    }
                    if (c.z >= z0 && c.z <= z1)
                    {
                        if (hitsCenter) cZ = true;
                        else if (c.x < cx - band) lZ = true;
                        else if (c.x > cx + band) rZ = true;
                    }
                }
                if (lY || rY || cY) { activeY++; if (lY && rY && !cY) twoLobeY++; }
                if (lZ || rZ || cZ) { activeZ++; if (lZ && rZ && !cZ) twoLobeZ++; }
            }
            gapXY = activeY > 0 ? (float)twoLobeY / activeY : 0f;
            gapXZ = activeZ > 0 ? (float)twoLobeZ / activeZ : 0f;
        }

        [MenuItem("Tools/Project C/Armor/0c. Render Contact Sheet of Paired Models")]
        public static void RenderPairedContactSheet()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ArmorSourceRoot });
            var fbxPaths = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith("_Dec.fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            const int CellSize = 256;
            const int Cols = 8;
            int rows = Mathf.CeilToInt((float)fbxPaths.Count / Cols);
            var atlas = new Texture2D(Cols * CellSize, rows * CellSize, TextureFormat.RGBA32, false);
            var clearPixels = new Color[atlas.width * atlas.height];
            for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = new Color(0.18f, 0.20f, 0.22f, 1f);
            atlas.SetPixels(clearPixels);

            var pru = new PreviewRenderUtility();
            pru.cameraFieldOfView = 30f;
            pru.camera.nearClipPlane = 0.05f;
            pru.camera.farClipPlane = 20f;
            pru.camera.clearFlags = CameraClearFlags.SolidColor;
            pru.camera.backgroundColor = new Color(0.18f, 0.20f, 0.22f, 1f);
            if (pru.lights.Length > 0 && pru.lights[0] != null)
            {
                pru.lights[0].intensity = 1.4f;
                pru.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            }
            if (pru.lights.Length > 1 && pru.lights[1] != null)
            {
                pru.lights[1].intensity = 0.9f;
                pru.lights[1].transform.rotation = Quaternion.Euler(340f, 215f, 0f);
            }

            var tempMat = new Material(litShader);
            var sb = new StringBuilder();
            sb.AppendLine("# Contact Sheet Index (Row, Col -> Model)");

            try
            {
                for (int idx = 0; idx < fbxPaths.Count; idx++)
                {
                    string path = fbxPaths[idx];
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null) continue;
                    string texPath = path.Replace("_Dec.fbx", "_Dec_BaseColor.png");
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    tempMat.SetTexture("_BaseMap", tex);
                    tempMat.mainTexture = tex;

                    // Render with FBX root rotation (-90, 0, 0) so Z-up Blender mesh stands Y-up in Unity
                    Quaternion rot = Quaternion.Euler(-90f, 180f, 0f);
                    Bounds b = mesh.bounds;
                    Vector3 center = rot * b.center;
                    float radius = b.extents.magnitude;
                    float dist = Mathf.Max(0.5f, radius / Mathf.Tan(pru.cameraFieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f);

                    pru.camera.transform.position = center + new Vector3(0f, 0f, -dist);
                    pru.camera.transform.LookAt(center, Vector3.up);

                    var rect = new Rect(0, 0, CellSize, CellSize);
                    pru.BeginStaticPreview(rect);
                    pru.DrawMesh(mesh, Matrix4x4.TRS(Vector3.zero, rot, Vector3.one), tempMat, 0);
                    pru.camera.Render();
                    Texture2D cellTex = pru.EndStaticPreview();

                    int col = idx % Cols;
                    int row = idx / Cols;
                    int yFlip = (rows - 1 - row) * CellSize;
                    atlas.SetPixels(col * CellSize, yFlip, CellSize, CellSize, cellTex.GetPixels());
                    UnityEngine.Object.DestroyImmediate(cellTex);

                    sb.AppendLine($"- [{row}, {col}] (#{idx}): {Path.GetFileNameWithoutExtension(path)}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempMat);
                pru.Cleanup();
            }

            atlas.Apply();
            Directory.CreateDirectory(ReportDir);
            string pngPath = Path.Combine(ReportDir, "all_93_contact_sheet.png");
            File.WriteAllBytes(pngPath, atlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(atlas);
            File.WriteAllText(Path.Combine(ReportDir, "all_93_contact_sheet.md"), sb.ToString(), Encoding.UTF8);
            Debug.Log($"[ArmorKitBuilder] Rendered 93 models contact sheet to {pngPath}");
        }

        [MenuItem("Tools/Project C/Armor/0d. Render Paired & Problem Models Front+Side")]
        public static void RenderPairedFrontSide()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ArmorSourceRoot });
            var fbxPaths = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith("_Dec.fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# Paired Models Split & TwoLobe Analysis (X axis vs Y axis)");
            sb.AppendLine("| # | Category | Model | Size (X,Y,Z) | SplitX | TwoLobeX | SplitY | TwoLobeY |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");

            var pairedPaths = new List<string>();
            for (int i = 0; i < fbxPaths.Count; i++)
            {
                string p = fbxPaths[i];
                if (!p.Contains("/Feet/") && !p.Contains("/Hands/") && !p.Contains("/Shoulders/") && !p.Contains("/Legs/"))
                    continue;
                if (p.Contains("LEGS_BELT") || p.Contains("MANTLE"))
                    continue;
                pairedPaths.Add(p);

                Mesh m = LoadMeshFromFbx(p);
                if (m == null) continue;
                ComputeSplitAndTwoLobeAxis(m, axis: 0, out float splitX, out float twoLobeX);
                ComputeSplitAndTwoLobeAxis(m, axis: 1, out float splitY, out float twoLobeY);
                Vector3 s = m.bounds.size;
                string cat = p.Split('/')[3];
                sb.AppendLine($"| {pairedPaths.Count - 1} | {cat} | {Path.GetFileNameWithoutExtension(p)} | ({s.x:F3}, {s.y:F3}, {s.z:F3}) | {splitX:F3} | {twoLobeX:F3} | {splitY:F3} | {twoLobeY:F3} |");
            }

            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(Path.Combine(ReportDir, "orientation_analysis.md"), sb.ToString(), Encoding.UTF8);

            // Also render a contact sheet of the 53 paired models viewed along X (showing Y left-right and Z up-down)
            const int Cols = 8;
            int rows = (pairedPaths.Count + Cols - 1) / Cols;
            const int CellSize = 220;
            var atlas = new Texture2D(Cols * CellSize, rows * CellSize, TextureFormat.RGBA32, false);
            var pru = new PreviewRenderUtility();
            pru.camera.fieldOfView = 30f;
            pru.camera.nearClipPlane = 0.05f;
            pru.camera.farClipPlane = 20f;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var tempMat = new Material(urpLit);
            try
            {
                for (int idx = 0; idx < pairedPaths.Count; idx++)
                {
                    string path = pairedPaths[idx];
                    Mesh mesh = LoadMeshFromFbx(path);
                    if (mesh == null) continue;
                    string dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
                    string texPath = $"{dir}/{Path.GetFileNameWithoutExtension(path)}_BaseColor.png";
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    tempMat.SetTexture("_BaseMap", tex);
                    tempMat.mainTexture = tex;

                    // Rotate so +Z_mesh is UP (+Y), -X_mesh is TOWARD CAMERA (-Z), +Y_mesh is RIGHT (+X)
                    Quaternion rot = Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(-90f, 0f, 0f);
                    Bounds b = mesh.bounds;
                    float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    float dist = Mathf.Max(0.5f, maxDim * 2.1f);
                    pru.camera.transform.position = new Vector3(0f, 0f, -dist);
                    pru.camera.transform.rotation = Quaternion.identity;
                    Vector3 rotatedCenter = rot * b.center;
                    pru.BeginStaticPreview(new Rect(0, 0, CellSize, CellSize));
                    pru.DrawMesh(mesh, -rotatedCenter, rot, tempMat, 0);
                    pru.camera.Render();
                    Texture2D cellTex = pru.EndStaticPreview();
                    int col = idx % Cols;
                    int row = idx / Cols;
                    int yFlip = (rows - 1 - row) * CellSize;
                    atlas.SetPixels(col * CellSize, yFlip, CellSize, CellSize, cellTex.GetPixels());
                    UnityEngine.Object.DestroyImmediate(cellTex);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempMat);
                pru.Cleanup();
            }
            atlas.Apply();
            File.WriteAllBytes(Path.Combine(ReportDir, "paired_53_view_along_X.png"), atlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(atlas);
            Debug.Log("[ArmorKitBuilder] Wrote orientation_analysis.md and paired_53_view_along_X.png");
        }

        public static void ComputeSplitAndTwoLobeAxis(Mesh mesh, int axis, out float split, out float twoLobe)
        {
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            Bounds b = mesh.bounds;
            int triCount = tris.Length / 3;
            if (triCount == 0) { split = 0f; twoLobe = 0f; return; }

            float centerA = axis == 0 ? b.center.x : b.center.y;
            float sizeA = axis == 0 ? b.size.x : b.size.y;
            float deadzone = sizeA * 0.06f;

            int otherAxis1 = axis == 0 ? 1 : 0;
            float min1 = otherAxis1 == 0 ? b.min.x : b.min.y;
            float size1 = Mathf.Max(0.0001f, otherAxis1 == 0 ? b.size.x : b.size.y);
            float min2 = b.min.z;
            float size2 = Mathf.Max(0.0001f, b.size.z);

            const int Slices = 20;
            bool[] s1L = new bool[Slices], s1R = new bool[Slices], s1C = new bool[Slices];
            bool[] s2L = new bool[Slices], s2R = new bool[Slices], s2C = new bool[Slices];

            int leftCount = 0, rightCount = 0;
            for (int t = 0; t < triCount; t++)
            {
                Vector3 v0 = verts[tris[t * 3]];
                Vector3 v1 = verts[tris[t * 3 + 1]];
                Vector3 v2 = verts[tris[t * 3 + 2]];
                Vector3 c = (v0 + v1 + v2) / 3f;
                float ca = axis == 0 ? c.x : c.y;
                if (ca < centerA) leftCount++; else rightCount++;

                float c1 = otherAxis1 == 0 ? c.x : c.y;
                float c2 = c.z;
                int idx1 = Mathf.Clamp((int)(((c1 - min1) / size1) * Slices), 0, Slices - 1);
                int idx2 = Mathf.Clamp((int)(((c2 - min2) / size2) * Slices), 0, Slices - 1);

                if (ca < centerA - deadzone) { s1L[idx1] = true; s2L[idx2] = true; }
                else if (ca > centerA + deadzone) { s1R[idx1] = true; s2R[idx2] = true; }
                else { s1C[idx1] = true; s2C[idx2] = true; }
            }

            split = (float)Mathf.Min(leftCount, rightCount) / triCount;
            int act1 = 0, two1 = 0, act2 = 0, two2 = 0;
            for (int s = 0; s < Slices; s++)
            {
                if (s1L[s] || s1R[s] || s1C[s]) { act1++; if (s1L[s] && s1R[s] && !s1C[s]) two1++; }
                if (s2L[s] || s2R[s] || s2C[s]) { act2++; if (s2L[s] && s2R[s] && !s2C[s]) two2++; }
            }
            float lobe1 = act1 > 0 ? (float)two1 / act1 : 0f;
            float lobe2 = act2 > 0 ? (float)two2 / act2 : 0f;
            twoLobe = Mathf.Max(lobe1, lobe2);
        }
    }
}
