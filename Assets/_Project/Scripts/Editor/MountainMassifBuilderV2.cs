using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ProjectC.World.Core;
using ProjectC.World.Generation;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor: генерация горных мешей V2 из MountainMassif ассетов.
    /// Заменяет MountainMassifBuilder (V1).
    /// 
    /// Использование: Tools → Project C → Build All Mountain Meshes (V2)
    /// 
    /// Новая система (ADR-0001):
    /// - Power-Law Cone profile (НЕ cylinder/ellipsoid/dome)
    /// - Явные meshHeight/baseRadius (НЕ формулы!)
    /// - MeshCollider вместо CapsuleCollider
    /// - 3-layer noise для natural variation
    /// </summary>
    public class MountainMassifBuilderV2 : EditorWindow
    {
        private Vector2 _scrollPos;
        private bool _clearExisting = true;
        private Material _himalayanRock;
        private Material _alpineRock;
        private Material _africanRock;
        private Material _andeanRock;
        private Material _alaskanRock;
        private Material _snowMaterial;

        [MenuItem("Tools/Project C/Build All Mountain Meshes (V2)")]
        public static void ShowWindow()
        {
            GetWindow<MountainMassifBuilderV2>("Mountain Mesh Builder V2");
        }

        void OnEnable()
        {
            _himalayanRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Rock_Himalayan.mat");
            _alpineRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Rock_Alpine.mat");
            _africanRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Rock_African.mat");
            _andeanRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Rock_Andean.mat");
            _alaskanRock = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Rock_Alaskan.mat");
            _snowMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/Snow_Generic.mat");
        }

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Mountain Mesh Builder V2 (ADR-0001)", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Генерирует 29 пиков из MountainMassif ассетов (V2).\n\n" +
                "НОВАЯ СИСТЕМА:\n" +
                "- Power-Law Cone profile\n" +
                "- Явные meshHeight/baseRadius\n" +
                "- MeshCollider (convex=false)\n" +
                "- 3-layer noise\n\n" +
                "Требования:\n" +
                "1. PeakData заполнены\n" +
                "2. PeakDataScaler запущен (рекомендуется)\n" +
                "3. Материалы скал созданы",
                MessageType.Info);

            EditorGUILayout.Space();

            _clearExisting = EditorGUILayout.Toggle("Удалить существующие горы", _clearExisting);

            EditorGUILayout.Space();

            _himalayanRock = (Material)EditorGUILayout.ObjectField("Himalayan Rock", _himalayanRock, typeof(Material), false);
            _alpineRock = (Material)EditorGUILayout.ObjectField("Alpine Rock", _alpineRock, typeof(Material), false);
            _africanRock = (Material)EditorGUILayout.ObjectField("African Rock", _africanRock, typeof(Material), false);
            _andeanRock = (Material)EditorGUILayout.ObjectField("Andean Rock", _andeanRock, typeof(Material), false);
            _alaskanRock = (Material)EditorGUILayout.ObjectField("Alaskan Rock", _alaskanRock, typeof(Material), false);
            _snowMaterial = (Material)EditorGUILayout.ObjectField("Snow", _snowMaterial, typeof(Material), false);

            EditorGUILayout.Space();

            // Кнопка построения ВСЕХ гор
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Построить ВСЕ горы (29 пиков) — V2", GUILayout.Height(50)))
            {
                BuildAllMountains();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Кнопки для отдельных массивов
            if (GUILayout.Button("Гималайский массив (8 пиков) — V2"))
                BuildMassif("HimalayanMassif");

            if (GUILayout.Button("Альпийский массив (6 пиков) — V2"))
                BuildMassif("AlpineMassif");

            if (GUILayout.Button("Африканский массив (4 пика) — V2"))
                BuildMassif("AfricanMassif");

            if (GUILayout.Button("Андийский массив (6 пиков) — V2"))
                BuildMassif("AndeanMassif");

            if (GUILayout.Button("Аляскинский массив (5 пиков) — V2"))
                BuildMassif("AlaskanMassif");

            EditorGUILayout.EndScrollView();
        }

        private void BuildAllMountains()
        {
            if (_clearExisting)
            {
                ClearExistingMountains();
            }

            BuildMassif("HimalayanMassif");
            BuildMassif("AlpineMassif");
            BuildMassif("AfricanMassif");
            BuildMassif("AndeanMassif");
            BuildMassif("AlaskanMassif");

            EditorUtility.DisplayDialog("Готово! (V2)",
                "Все 29 горных мешей построены (V2).\n\n" +
                "Новая система:\n" +
                "- Power-Law Cone profile\n" +
                "- Явные размеры\n" +
                "- MeshCollider\n\n" +
                "Проверьте в Scene view!",
                "OK");
        }

        private void ClearExistingMountains()
        {
            var mountainsRoot = GameObject.Find("Mountains");
            if (mountainsRoot != null)
            {
                for (int i = mountainsRoot.transform.childCount - 1; i >= 0; i--)
                {
                    var child = mountainsRoot.transform.GetChild(i).gameObject;
                    if (Application.isPlaying)
                        Object.Destroy(child);
                    else
                        Object.DestroyImmediate(child);
                }
                Debug.Log("[MountainMassifBuilderV2] Cleared existing mountains.");
            }
        }

        private void BuildMassif(string massifFileName)
        {
            var massif = FindMassif(massifFileName);
            if (massif == null)
            {
                Debug.LogError($"[MountainMassifBuilderV2] Massif not found: {massifFileName}");
                return;
            }

            if (massif.peaks == null || massif.peaks.Count == 0)
            {
                Debug.LogWarning($"[MountainMassifBuilderV2] {massif.displayName} has no peaks.");
                return;
            }

            var mountainsRoot = GameObject.Find("Mountains");
            if (mountainsRoot == null)
            {
                mountainsRoot = new GameObject("Mountains");
                Undo.RegisterCreatedObjectUndo(mountainsRoot, "Create Mountains root");
            }

            var massifRoot = new GameObject(massif.displayName);
            massifRoot.transform.SetParent(mountainsRoot.transform);
            Undo.RegisterCreatedObjectUndo(massifRoot, "Create massif root");

            Material rockMat = GetRockMaterialForMassif(massif);

            int builtCount = 0;
            foreach (var peak in massif.peaks)
            {
                if (peak == null) continue;
                BuildPeakGameObject(peak, massifRoot.transform, rockMat);
                builtCount++;
            }

            Debug.Log($"[MountainMassifBuilderV2] {massif.displayName}: {builtCount} peaks built (V2).");
        }

        private void BuildPeakGameObject(PeakData peak, Transform parent, Material rockMaterial)
        {
            var peakGO = new GameObject(peak.displayName);
            peakGO.transform.SetParent(parent);

            var builder = peakGO.AddComponent<MountainMeshBuilderV2>();
            builder.peakData = peak;
            builder.rockMaterial = rockMaterial;
            builder.snowMaterial = _snowMaterial;
            builder.snowLineY = peak.snowLineY;
            builder.showDebugInfo = true;

            Undo.RegisterCreatedObjectUndo(peakGO, $"Create {peak.displayName}");

            if (!Application.isPlaying)
            {
                BuildPeakMeshInEditor(builder, peak);
            }
        }

        private void BuildPeakMeshInEditor(MountainMeshBuilderV2 builder, PeakData peak)
        {
            var meshFilter = builder.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = builder.gameObject.AddComponent<MeshFilter>();

            var meshRenderer = builder.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = builder.gameObject.AddComponent<MeshRenderer>();

            var meshCollider = builder.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = builder.gameObject.AddComponent<MeshCollider>();

            // meshHeight/baseRadius — явные значения (V2)
            float meshHeight = MountainMeshGenerator.CalculateMeshHeight(peak);
            float baseRadius = MountainMeshGenerator.CalculateBaseRadius(peak, meshHeight);

            // Создать profile
            MountainProfile profile = MountainProfile.CreatePreset(peak.shapeType);
            if (peak.hasCrater)
            {
                profile.hasCrater = true;
            }

            // Generate mesh
            Mesh mesh = MountainMeshGenerator.GenerateMountainMesh(
                profile,
                meshHeight,
                baseRadius,
                builder.lod0Segments,
                builder.lod0Rings,
                seed: peak.displayName.GetHashCode());

            // Assign
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = GetRockMaterialForPeak(peak);

            // MeshCollider (simplified)
            Mesh colliderMesh = MountainMeshGenerator.GenerateColliderMesh(
                profile,
                meshHeight,
                baseRadius);
            meshCollider.sharedMesh = colliderMesh;
            meshCollider.convex = false;
            meshCollider.isTrigger = false;

            // Позиция: основание на Y=0
            builder.transform.position = new Vector3(
                peak.worldPosition.x,
                0f,
                peak.worldPosition.z
            );

            Debug.Log($"[MountainMassifBuilderV2] {peak.displayName} ({peak.shapeType}): " +
                      $"baseY=0, topY={meshHeight:F1} | " +
                      $"meshH={meshHeight:F1}, baseR={baseRadius:F1}, h/r={meshHeight / baseRadius:F2} | " +
                      $"posXZ=({builder.transform.position.x:F0}, {builder.transform.position.z:F0}) | " +
                      $"{mesh.vertexCount}v, {mesh.triangles.Length / 3}t");
        }

        #region Helper Methods

        private MountainMassif FindMassif(string fileName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:ScriptableObject {fileName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"{fileName}.asset"))
                {
                    return AssetDatabase.LoadAssetAtPath<MountainMassif>(path);
                }
            }

            string standardPath = $"Assets/_Project/Data/World/Massifs/{fileName}.asset";
            var massif = AssetDatabase.LoadAssetAtPath<MountainMassif>(standardPath);
            if (massif != null) return massif;

            Debug.LogError($"[MountainMassifBuilderV2] Cannot find MountainMassif asset: {fileName}");
            return null;
        }

        private Material GetRockMaterialForMassif(MountainMassif massif)
        {
            return massif.displayName switch
            {
                "Himalayan" => _himalayanRock,
                "Alpine" => _alpineRock,
                "African" => _africanRock,
                "Andean" => _andeanRock,
                "Alaskan" => _alaskanRock,
                _ => _himalayanRock
            };
        }

        private Material GetRockMaterialForPeak(PeakData peak)
        {
            // Try to find massif for this peak
            string[] massifNames = { "HimalayanMassif", "AlpineMassif", "AfricanMassif", "AndeanMassif", "AlaskanMassif" };
            
            foreach (var massifName in massifNames)
            {
                var massif = FindMassif(massifName);
                if (massif != null && massif.peaks.Contains(peak))
                {
                    return GetRockMaterialForMassif(massif);
                }
            }

            return _himalayanRock; // fallback
        }

        #endregion
    }
}
