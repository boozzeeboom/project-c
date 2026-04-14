#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectC.Editor
{
    /// <summary>
    /// Автоматически добавляет StreamingTest_AutoRun на MainCamera при запуске Play Mode.
    /// Это позволяет тестировать World Streaming без ручного добавления компонента.
    /// </summary>
    [InitializeOnLoad]
    public class StreamingTestAutoRunner
    {
        private const string COMPONENT_NAME = "StreamingTest_AutoRun";
        private static readonly string[] testComponentsToRemove = { "StreamingTest" };
        
        static StreamingTestAutoRunner()
        {
            // Подписываемся на события Editor
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }
        
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Добавляем компонент на камеру
                AddComponentToMainCamera();
            }
        }
        
        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (Application.isPlaying)
            {
                // Добавляем компонент при открытии новой сцены в Play Mode
                AddComponentToMainCamera();
            }
        }
        
        /// <summary>
        /// Найти MainCamera или создать её, затем добавить StreamingTest_AutoRun.
        /// </summary>
        [MenuItem("Tools/Project C/World/Add Test Component to Camera")]
        public static void AddComponentToMainCamera()
        {
            // Удаляем старые тестовые компоненты
            RemoveOldTestComponents();
            
            // Ищем Main Camera
            Camera mainCamera = Camera.main;
            
            if (mainCamera == null)
            {
                // Пробуем найти любую камеру
                mainCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
                
                if (mainCamera == null)
                {
                    Debug.LogWarning("[StreamingTestAutoRunner] ⚠️ Камера не найдена! Создайте камеру или включите MainCamera тег.");
                    return;
                }
                
                Debug.Log($"[StreamingTestAutoRunner] ℹ️ MainCamera не найдена, используем: {mainCamera.name}");
            }
            
            // Проверяем есть ли уже компонент
            var existingComponent = mainCamera.GetComponent<ProjectC.World.StreamingTest_AutoRun>();
            if (existingComponent != null)
            {
                Debug.Log($"[StreamingTestAutoRunner] ✅ StreamingTest_AutoRun уже добавлен на {mainCamera.name}");
                return;
            }
            
            // Добавляем компонент
            var testComponent = mainCamera.gameObject.AddComponent<ProjectC.World.StreamingTest_AutoRun>();
            
            // Отмечаем сцену как изменённую
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(mainCamera.gameObject.scene);
            }
            
            Debug.Log($"[StreamingTestAutoRunner] ✅ StreamingTest_AutoRun добавлен на {mainCamera.name}!");
            Debug.Log($"[StreamingTestAutoRunner] 🎮 Управление: F5=след.точка, F6=пред.точка, F7=загрузить чанки, F8=сбросить FloatingOrigin, F9=ChunkGrid(меню), F10=HUD");
        }
        
        /// <summary>
        /// Удалить старые тестовые компоненты StreamingTest.
        /// </summary>
        private static void RemoveOldTestComponents()
        {
            foreach (var componentName in testComponentsToRemove)
            {
                // Находим все объекты с компонентом
                var components = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
                foreach (var component in components)
                {
                    if (component != null && component.GetType().Name == componentName)
                    {
                        Debug.Log($"[StreamingTestAutoRunner] 🗑️ Удаляем старый компонент: {componentName}");
                        UnityEngine.Object.DestroyImmediate(component);
                    }
                }
            }
        }
        
        /// <summary>
        /// Удалить StreamingTest_AutoRun со всех камер.
        /// </summary>
        [MenuItem("Tools/Project C/World/Remove Test Component from Camera")]
        public static void RemoveComponentFromCamera()
        {
            var components = UnityEngine.Object.FindObjectsByType<ProjectC.World.StreamingTest_AutoRun>(FindObjectsInactive.Exclude);
            
            foreach (var component in components)
            {
                if (component != null)
                {
                    Debug.Log($"[StreamingTestAutoRunner] 🗑️ Удаляем StreamingTest_AutoRun с {component.gameObject.name}");
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
            
            if (components.Length == 0)
            {
                Debug.Log("[StreamingTestAutoRunner] ℹ️ StreamingTest_AutoRun не найден.");
            }
        }
    }
}
#endif
