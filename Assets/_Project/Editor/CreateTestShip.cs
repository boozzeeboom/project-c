#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Player;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor utility для быстрого создания тестового корабля.
    /// Меню: Tools → Create Test Ship
    /// </summary>
    public class CreateTestShip : EditorWindow
    {
        [MenuItem("Tools/Create Test Ship")]
        public static void Create()
        {
            // Проверить что сцена не в Play Mode
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Ошибка", "Выйдите из Play Mode перед созданием корабля", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Test Ship");

            // 1. Создать платформу
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform_01";
            platform.transform.localScale = new Vector3(20, 0.5f, 20);
            platform.transform.position = Vector3.zero;
            
            // Удалить стандартный MeshRenderer (платформа не нужна визуалка)
            // Или оставить — можно настроить материал позже
            var platMat = platform.GetComponent<MeshRenderer>();
            if (platMat != null)
            {
                platMat.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                platMat.sharedMaterial.color = new Color(0.4f, 0.4f, 0.4f); // серый
            }

            Undo.RegisterCreatedObjectUndo(platform, "Create Platform");

            // 2. Создать корабль
            var ship = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ship.name = "Ship_Test";
            ship.transform.localScale = new Vector3(8, 1.5f, 4);
            ship.transform.position = new Vector3(0, 1.5f, 0);

            // Назначить тег "Ship"
            if (!TagExists("Ship"))
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(ship, "Create Ship");
            }
            ship.tag = "Ship";

            // Настроить материал корабля
            var shipMat = ship.GetComponent<MeshRenderer>();
            if (shipMat != null)
            {
                shipMat.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                shipMat.sharedMaterial.color = new Color(0.8f, 0.3f, 0.3f); // красноватый
            }

            // 3. Добавить Rigidbody
            var rb = ship.AddComponent<Rigidbody>();
            rb.mass = 1000f;
            rb.drag = 0f;
            rb.angularDrag = 0f; // ShipController сам управляет
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            // 4. Добавить ShipController
            var sc = ship.AddComponent<ShipController>();
            // Параметры уже настроены по умолчанию в ShipController.cs

            // 5. Добавить NetworkObject
            var netObj = ship.AddComponent<NetworkObject>();

            // 6. Добавить NetworkTransform
            var netTransform = ship.AddComponent<NetworkTransform>();
            netTransform.SyncMode = NetworkTransform.SyncModeType.Server;
            netTransform.UseHalfFloatPrecision = false;
            netTransform.UseUnreliableDeltas = false;
            netTransform.InLocalSpace = false;
            netTransform.SlerpPosition = false;

            // 7. Выбрать корабль в Hierarchy
            Selection.activeGameObject = ship;

            Debug.Log("✅ Test ship created!");
            Debug.Log($"  - Ship: {ship.name} (Scale: {ship.transform.localScale})");
            Debug.Log($"  - Platform: {platform.name} (Scale: {platform.transform.localScale})");
            Debug.Log($"  - Rigidbody Mass: {rb.mass}");
            Debug.Log($"  - Tag: {ship.tag}");
        }

        private static bool TagExists(string tag)
        {
            foreach (var t in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (t == tag) return true;
            }
            return false;
        }
    }
}
#endif
