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
        private static ShipClass selectedClass = ShipClass.Medium;

        [MenuItem("Tools/Create Test Ship")]
        public static void Create()
        {
            // Проверить что сцена не в Play Mode
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Ошибка", "Выйдите из Play Mode перед созданием корабля", "OK");
                return;
            }

            // Показать окно выбора класса
            var window = GetWindow<CreateTestShip>("Create Test Ship");
            window.minSize = new Vector2(300, 150);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            GUILayout.Label("Выберите класс корабля:", EditorStyles.boldLabel);
            selectedClass = (ShipClass)EditorGUILayout.EnumPopup("Ship Class:", selectedClass);

            GUILayout.Space(10);

            if (GUILayout.Button("Создать корабль", GUILayout.Height(30)))
            {
                CreateShip(selectedClass);
                Close();
            }

            if (GUILayout.Button("Отмена"))
            {
                Close();
            }
        }

        private static void CreateShip(ShipClass shipClass)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Create {shipClass} Ship");

            // 1. Создать платформу
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform_01";
            platform.transform.localScale = new Vector3(20, 0.5f, 20);
            platform.transform.position = Vector3.zero;
            
            var platMat = platform.GetComponent<MeshRenderer>();
            if (platMat != null)
            {
                platMat.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                platMat.sharedMaterial.color = new Color(0.4f, 0.4f, 0.4f);
            }

            Undo.RegisterCreatedObjectUndo(platform, "Create Platform");

            // 2. Создать корабль
            var ship = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ship.name = $"Ship_{shipClass}";
            ship.transform.localScale = new Vector3(8, 1.5f, 4);
            ship.transform.position = new Vector3(0, 1.5f, 0);

            ship.tag = "Ship";

            var shipMat = ship.GetComponent<MeshRenderer>();
            if (shipMat != null)
            {
                shipMat.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                // Разные цвета для разных классов
                shipMat.sharedMaterial.color = shipClass switch
                {
                    ShipClass.Light => new Color(0.3f, 0.8f, 0.3f),   // Зелёный
                    ShipClass.Medium => new Color(0.8f, 0.3f, 0.3f), // Красный
                    ShipClass.Heavy => new Color(0.3f, 0.3f, 0.8f),  // Синий
                    ShipClass.HeavyII => new Color(0.8f, 0.8f, 0.3f), // Жёлтый
                    _ => Color.white
                };
            }

            // 3. Добавить Rigidbody
            var rb = ship.AddComponent<Rigidbody>();
            rb.drag = 0f;
            rb.angularDrag = 0f;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            // 4. Добавить ShipController
            var sc = ship.AddComponent<ShipController>();
            
            // Установить класс через reflection (поле private)
            var shipClassField = sc.GetType().GetField("shipClass", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shipClassField?.SetValue(sc, shipClass);
            
            // Вызвать ApplyShipClass через reflection
            var applyMethod = sc.GetType().GetMethod("ApplyShipClass", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            applyMethod?.Invoke(sc, null);

            // 5. Добавить NetworkObject
            ship.AddComponent<NetworkObject>();

            // 6. NetworkTransform — добавить вручную если установлен пакет
            //    В Unity 6 / NGO 2.x может требовать отдельный пакет:
            //    com.unity.netcode.gameobjects (NetworkTransform компонент)
            //    Добавить: Add Component → NetworkTransform
            //    Sync Mode: Server Authority

            // 7. Выбрать корабль в Hierarchy
            Selection.activeGameObject = ship;

            Debug.Log($"✅ Test ship created: {shipClass}");
            Debug.Log($"  - Ship: {ship.name} (Scale: {ship.transform.localScale})");
            Debug.Log($"  - Platform: {platform.name} (Scale: {platform.transform.localScale})");
            Debug.Log($"  - Rigidbody Mass: {rb.mass}");
            Debug.Log($"  - Tag: {ship.tag}");
            Debug.Log($"  - Class: {shipClass}");
            Debug.Log($"  ⚠️ NetworkTransform нужно добавить вручную (Add Component → NetworkTransform → Server Authority)");
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
