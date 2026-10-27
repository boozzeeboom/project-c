// ProjectC Main Menu — one-shot editor builder for the atmosphere prefab.
// Creates the PCMM_Atmosphere layer, the seven procedural materials and
// Prefabs/ProjectCMainMenu.prefab. Builds the prefab inside a throw-away additive
// scene so the working scene is never touched or dirtied.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectCMainMenu;

namespace ProjectCMainMenu.EditorTools
{
    public static class ProjectCMainMenuBuilder
    {
        private const string RootPath = "Assets/Aura/Mods/ProjectCMainMenu";
        private const string MaterialPath = RootPath + "/Materials";
        private const string PrefabFolder = RootPath + "/Prefabs";
        private const string PrefabPath = PrefabFolder + "/ProjectCMainMenu.prefab";
        private const string LayerName = "PCMM_Atmosphere";

        private struct MaterialSet
        {
            public Material Sky;
            public Material Gas;
            public Material Stars;
            public Material Veil;
            public Material Rope;
            public Material Hull;
            public Material Glow;
        }

        [MenuItem("Tools/ProjectC/MainMenu/Build Atmosphere Prefab")]
        public static void BuildAll()
        {
            EnsureFolder(MaterialPath);
            EnsureFolder(PrefabFolder);

            EnsureLayer(LayerName);

            if (!TryBuildMaterials(out var materials)) return;

            BuildPrefab(materials);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PCMM] prefab ready: " + PrefabPath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void EnsureLayer(string layerName)
        {
            if (LayerMask.NameToLayer(layerName) >= 0) return;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[PCMM] TagManager.asset not found, cannot create layer " + layerName);
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray)
            {
                Debug.LogError("[PCMM] layers array not found in TagManager.asset");
                return;
            }

            for (var i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"[PCMM] created layer '{layerName}' at index {i}");
                return;
            }

            Debug.LogError($"[PCMM] no free user layer slot for '{layerName}'");
        }

        private static bool TryBuildMaterials(out MaterialSet set)
        {
            set = default;

            var ok = true;
            set.Sky = EnsureMaterial("PCMM_Sky", "ProjectC/Mods/MainMenu/Sky", ref ok);
            set.Gas = EnsureMaterial("PCMM_Gas", "ProjectC/Mods/MainMenu/Gas", ref ok);
            set.Stars = EnsureMaterial("PCMM_Stars", "ProjectC/Mods/MainMenu/Stars", ref ok);
            set.Veil = EnsureMaterial("PCMM_Veil", "ProjectC/Mods/MainMenu/Veil", ref ok);
            set.Rope = EnsureMaterial("PCMM_Rope", "ProjectC/Mods/MainMenu/Rope", ref ok);
            set.Hull = EnsureMaterial("PCMM_Hull", "ProjectC/Mods/MainMenu/Hull", ref ok);
            set.Glow = EnsureMaterial("PCMM_Glow", "ProjectC/Mods/MainMenu/Glow", ref ok);

            return ok;
        }

        private static Material EnsureMaterial(string name, string shaderName, ref bool ok)
        {
            var path = MaterialPath + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[PCMM] shader not found: {shaderName}");
                ok = false;
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static void BuildPrefab(MaterialSet materials)
        {
            var previousActive = SceneManager.GetActiveScene();
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temp);

            GameObject root = null;

            try
            {
                root = new GameObject("ProjectCMainMenu");

                var atmosphere = root.AddComponent<ProjectCMainMenuAtmosphere>();
                var bridge = root.AddComponent<ProjectCMainMenuMenuBridge>();

                var cableGo = new GameObject("Cables");
                cableGo.transform.SetParent(root.transform, false);
                var ropeRig = cableGo.AddComponent<ProjectCMainMenuRopeRig>();

                var dustGo = new GameObject("WindDust");
                dustGo.transform.SetParent(root.transform, false);
                var windDust = dustGo.AddComponent<ProjectCMainMenuWindDust>();

                var atmosphereSo = new SerializedObject(atmosphere);
                SetRef(atmosphereSo, "skyMaterial", materials.Sky);
                SetRef(atmosphereSo, "gasMaterial", materials.Gas);
                SetRef(atmosphereSo, "starsMaterial", materials.Stars);
                SetRef(atmosphereSo, "veilMaterial", materials.Veil);
                SetRef(atmosphereSo, "ropeRig", ropeRig);
                SetRef(atmosphereSo, "windDust", windDust);
                SetString(atmosphereSo, "atmosphereLayerName", LayerName);
                atmosphereSo.ApplyModifiedPropertiesWithoutUndo();

                var ropeSo = new SerializedObject(ropeRig);
                SetRef(ropeSo, "ropeMaterial", materials.Rope);
                SetRef(ropeSo, "hullMaterial", materials.Hull);
                SetRef(ropeSo, "glowMaterial", materials.Glow);
                ropeSo.ApplyModifiedPropertiesWithoutUndo();

                var bridgeSo = new SerializedObject(bridge);
                SetRef(bridgeSo, "atmosphere", atmosphere);
                bridgeSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(temp, true);
            }
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[PCMM] field not found: {so.targetObject.GetType().Name}.{field}");
                return;
            }
            property.objectReferenceValue = value;
        }

        private static void SetString(SerializedObject so, string field, string value)
        {
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[PCMM] field not found: {so.targetObject.GetType().Name}.{field}");
                return;
            }
            property.stringValue = value;
        }
    }
}
