using UnityEngine;
using UnityEditor;
using ProjectC.World.Core;
using ProjectC.Core;

namespace ProjectC.Editor
{
    /// <summary>
    /// Editor: автоматическая настройка Floating Origin для большого мира.
    ///
    /// Использование: Tools → Project C → Setup Floating Origin
    ///
    /// Что делает:
    /// 1. Находит все камеры в сцене (WorldCamera, ThirdPersonCamera)
    /// 2. Добавляет компонент FloatingOrigin
    /// 3. Назначает worldRoot (Mountains, Farms, и т.д.)
    /// 4. Настраивает threshold = 100,000 units
    /// </summary>
    public class FloatingOriginSetup : EditorWindow
    {
        private Vector2 _scrollPos;
        private Transform _worldRoot;
        private float _threshold = 100000f;
        private bool _showDebugLogs = false;

        [MenuItem("Tools/Project C/Setup Floating Origin")]
        public static void ShowWindow()
        {
            GetWindow<FloatingOriginSetup>("Floating Origin Setup");
        }

        void OnEnable()
        {
            // Попытка автоматически найти world root
            GameObject mountains = GameObject.Find("Mountains");
            if (mountains != null)
            {
                _worldRoot = mountains.transform;
            }
            else
            {
                // Используем тот же метод что и в камерах — ищем объект с наибольшим количеством детей
                GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
                int maxChildren = 0;

                foreach (var obj in allObjects)
                {
                    if (obj.transform.childCount > maxChildren && obj.transform.parent == null)
                    {
                        maxChildren = obj.transform.childCount;
                        _worldRoot = obj.transform;
                    }
                }
            }
        }

        void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Floating Origin Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Настраивает Floating Origin для всех камер сцены.\n\n" +
                "Floating Origin предотвращает floating point jitter\n" +
                "при координатах XZ > 100,000 units.\n\n" +
                "Когда камера уходит далеко от (0,0,0), весь мир\n" +
                "сдвигается обратно к origin — камера всегда в центре.",
                MessageType.Info);

            EditorGUILayout.Space();

            _worldRoot = (Transform)EditorGUILayout.ObjectField(
                "World Root (объект мира)",
                _worldRoot,
                typeof(Transform),
                true);

            _threshold = EditorGUILayout.FloatField("Threshold (units)", _threshold);
            _showDebugLogs = EditorGUILayout.Toggle("Show Debug Logs", _showDebugLogs);

            EditorGUILayout.Space();

            // Кнопка настройки всех камер
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Настроить ВСЕ камеры в сцене", GUILayout.Height(40)))
            {
                SetupAllCameras();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            // Кнопка настройки конкретной камеры
            if (GUILayout.Button("Настроить выбранную камеру"))
            {
                if (Selection.activeGameObject != null)
                {
                    var cam = Selection.activeGameObject.GetComponent<Camera>();
                    if (cam != null)
                    {
                        SetupCamera(cam);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Ошибка",
                            "Выбранный объект не имеет Camera компонента!",
                            "OK");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Ошибка",
                        "Выберите камеру в Hierarchy!",
                        "OK");
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void SetupAllCameras()
        {
            if (_worldRoot == null)
            {
                EditorUtility.DisplayDialog("Ошибка",
                    "Назначьте World Root перед настройкой!",
                    "OK");
                return;
            }

            int setupCount = 0;

            // Находим все камеры в сцене
            Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);

            foreach (var cam in allCameras)
            {
                // Пропускаем scene view camera и UI cameras
                if (cam.CompareTag("MainCamera") ||
                    cam.GetComponent<WorldCamera>() != null ||
                    cam.GetComponent<ThirdPersonCamera>() != null)
                {
                    SetupCamera(cam);
                    setupCount++;
                }
            }

            EditorUtility.DisplayDialog("Готово!",
                $"Настроено {setupCount} камер с Floating Origin.\n\n" +
                $"World Root: {_worldRoot.name}\n" +
                $"Threshold: {_threshold:N0} units\n\n" +
                "Проверьте что горы двигаются вместе с камерой!",
                "OK");
        }

        private void SetupCamera(Camera cam)
        {
            // Проверяем есть ли уже FloatingOrigin
            var floatingOrigin = cam.GetComponent<FloatingOrigin>();
            if (floatingOrigin == null)
            {
                floatingOrigin = cam.gameObject.AddComponent<FloatingOrigin>();
            }

            // Настраиваем параметры
            floatingOrigin.worldRoot = _worldRoot;
            floatingOrigin.threshold = _threshold;
            floatingOrigin.showDebugLogs = _showDebugLogs;

            Debug.Log($"[FloatingOriginSetup] Configured FloatingOrigin on {cam.gameObject.name}");
        }
    }
}
