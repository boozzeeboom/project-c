using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Generates lightweight BoxCollider geometry for a detailed city FBX without touching the imported model.
    /// Colliders are created in a separate child hierarchy and are based on named structural meshes only.
    /// </summary>
    public static class GenerateSimpleCityColliders
    {
        private const string MenuPath = "Tools/ProjectC/Generate Simple City Colliders";
        private const string GeneratedRootName = "CITY_SIMPLE_COLLIDERS";
        private const string DefaultCityRootName = "gorod port_3_3_unity_1";

        [MenuItem(MenuPath)]
        private static void GenerateFromSelection()
        {
            var root = Selection.activeGameObject;
            if (root == null)
                root = GameObject.Find(DefaultCityRootName);

            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Simple City Colliders",
                    $"GameObject '{DefaultCityRootName}' не найден и корень не выбран.",
                    "OK");
                return;
            }

            GenerateFor(root);
        }

        public static void GenerateFor(GameObject root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            var oldRoot = root.transform.Find(GeneratedRootName);
            if (oldRoot != null)
                Undo.DestroyObjectImmediate(oldRoot.gameObject);

            var generatedRoot = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(generatedRoot, "Create city simple colliders");
            generatedRoot.transform.SetParent(root.transform, false);
            generatedRoot.transform.localPosition = Vector3.zero;
            generatedRoot.transform.localRotation = Quaternion.identity;
            generatedRoot.transform.localScale = Vector3.one;
            generatedRoot.layer = root.layer;
            generatedRoot.isStatic = true;

            var floors = CreateCategory(generatedRoot.transform, "Floors");
            var walls = CreateCategory(generatedRoot.transform, "Walls");
            var stairs = CreateCategory(generatedRoot.transform, "Stairs");
            var structures = CreateCategory(generatedRoot.transform, "Structures");

            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            int created = 0;
            int skipped = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.gameObject == root)
                    continue;

                if (!renderer.enabled || IsGeneratedObject(renderer.transform, generatedRoot.transform))
                {
                    skipped++;
                    continue;
                }

                string upperName = renderer.gameObject.name.ToUpperInvariant();
                if (ShouldSkip(upperName) || !IsStructural(upperName))
                {
                    skipped++;
                    continue;
                }

                Vector3 worldSize = renderer.bounds.size;
                if (worldSize.x < 0.05f || worldSize.y < 0.05f || worldSize.z < 0.05f)
                {
                    skipped++;
                    continue;
                }

                Transform category = SelectCategory(upperName, floors, walls, stairs, structures);
                var colliderObject = new GameObject($"SC_{created:0000}_{SanitizeName(renderer.gameObject.name)}");
                Undo.RegisterCreatedObjectUndo(colliderObject, "Create city simple collider");
                colliderObject.layer = root.layer;
                colliderObject.isStatic = true;
                colliderObject.transform.SetParent(category, true);
                // BoxCollider.center carries the mesh-local offset, so the object itself
                // must use the source Transform position; otherwise the offset is applied twice.
                colliderObject.transform.SetPositionAndRotation(
                    renderer.transform.position,
                    renderer.transform.rotation);
                colliderObject.transform.localScale = Divide(renderer.transform.lossyScale, category.lossyScale);

                var box = colliderObject.AddComponent<BoxCollider>();
                box.center = renderer.localBounds.center;
                box.size = renderer.localBounds.size;
                box.isTrigger = false;

                created++;
            }

            EditorUtility.SetDirty(generatedRoot);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = generatedRoot;

            Debug.Log($"[SimpleCityColliders] '{root.name}': created={created}, skipped={skipped}. Separate BoxCollider hierarchy: {generatedRoot.name}");
        }

        private static Transform CreateCategory(Transform parent, string name)
        {
            var category = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(category, "Create city collider category");
            category.transform.SetParent(parent, false);
            category.layer = parent.gameObject.layer;
            category.isStatic = true;
            return category.transform;
        }

        private static bool IsGeneratedObject(Transform candidate, Transform generatedRoot)
        {
            return candidate == generatedRoot || candidate.IsChildOf(generatedRoot);
        }

        private static bool ShouldSkip(string name)
        {
            string[] excluded =
            {
                "REFERENCE", "MARKER", "SIGN", "TEXT", "GLASS", "WINDOW", "LIGHT", "LAMP",
                "PIPE", "CABLE", "RAIL", "RIVET", "FASTENER", "JOINT", "STRIPE", "PATCH",
                "TARP", "BAND", "GLOW", "SPHERE", "ROOF", "DOOR", "BEAM", "CRANE_", "WAIT_"
            };

            for (int i = 0; i < excluded.Length; i++)
            {
                if (name.Contains(excluded[i]))
                    return true;
            }

            return false;
        }

        private static bool IsStructural(string name)
        {
            string[] structural =
            {
                "FLOOR", "SLAB", "GROUND", "PLATFORM", "DECK", "WALL", "SOLID", "COLUMN",
                "STAIR", "STEP", "RAMP", "THRESHOLD", "PLINTH", "FOUNDATION", "FRAME",
                "CANOPY", "LOWER", "UPPER", "PANEL", "WALKWAY", "SPINE", "CONNECTION",
                "LINK", "COURT", "APRON", "DOCK", "TRANSFER"
            };

            for (int i = 0; i < structural.Length; i++)
            {
                if (name.Contains(structural[i]))
                    return true;
            }

            // Important broad walkable surfaces whose source names are not English.
            return name == "НИЖНЯЯ ПАНЕЛЬ"
                || name == "СРЕДНЯЯ - ДОКИНГ 2"
                || name == "ПРИСТАНЬ ПОЕЗДОВ"
                || name == "ПЛИТА ВЕРХ ЧАСТЬ"
                || name == "ПЛИТА НИЖНЯЯ ЧАСТЬ";
        }

        private static Transform SelectCategory(
            string name,
            Transform floors,
            Transform walls,
            Transform stairs,
            Transform structures)
        {
            if (name.Contains("STAIR") || name.Contains("STEP") || name.Contains("RAMP"))
                return stairs;

            if (name.Contains("FLOOR") || name.Contains("SLAB") || name.Contains("GROUND") ||
                name.Contains("PLATFORM") || name.Contains("DECK") || name.Contains("WALKWAY") ||
                name.Contains("SPINE") || name.Contains("APRON") || name.Contains("COURT"))
                return floors;

            if (name.Contains("WALL") || name.Contains("SOLID") || name.Contains("COLUMN"))
                return walls;

            return structures;
        }

        private static Vector3 Divide(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                divisor.x == 0f ? 1f : value.x / divisor.x,
                divisor.y == 0f ? 1f : value.y / divisor.y,
                divisor.z == 0f ? 1f : value.z / divisor.z);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Mesh";

            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '/' || chars[i] == '\\' || chars[i] == ':' || chars[i] == ' ')
                    chars[i] = '_';
            }

            string result = new string(chars);
            return result.Length > 72 ? result.Substring(0, 72) : result;
        }
    }
}
