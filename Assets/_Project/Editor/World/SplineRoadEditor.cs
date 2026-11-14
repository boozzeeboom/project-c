using System.Text;
using ProjectC.World.Generation;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Инспектор дорожки — все параметры и «Пересобрать» в одном месте, без похода в меню Tools.
    ///
    /// Правки полей пересобирают дорожку только при включённой галочке (EditorPrefs, общая на проект):
    /// на длинной трассе пересборка занимает заметное время, тянуть её на каждый шаг драга нельзя.
    /// </summary>
    [CustomEditor(typeof(SplineRoad))]
    [CanEditMultipleObjects]
    public sealed class SplineRoadEditor : UnityEditor.Editor
    {
        private const string AutoRebuildKey = "ProjectC.SplineRoad.AutoRebuild";

        private static string lastReport;

        private SerializedProperty surface;
        private SerializedProperty splineIndex;
        private SerializedProperty groundOffset;

        private SerializedProperty roadMaterial;
        private SerializedProperty curbMaterial;

        private SerializedProperty width;
        private SerializedProperty maxStep;
        private SerializedProperty maxDeviation;
        private SerializedProperty miterLimit;

        private SerializedProperty curbWidth;
        private SerializedProperty curbHeight;

        private SerializedProperty generateColliders;
        private SerializedProperty colliderLayer;
        private SerializedProperty colliderThickness;
        private SerializedProperty colliderStep;

        private SerializedProperty snapToSurface;
        private SerializedProperty surfaceMask;
        private SerializedProperty surfaceProbeUp;
        private SerializedProperty surfaceProbeDown;
        private SerializedProperty surfaceOffset;

        private SerializedProperty roadId;

        private static bool AutoRebuild
        {
            get => EditorPrefs.GetBool(AutoRebuildKey, false);
            set => EditorPrefs.SetBool(AutoRebuildKey, value);
        }

        private void OnEnable()
        {
            surface = serializedObject.FindProperty(nameof(SplineRoad.surface));
            splineIndex = serializedObject.FindProperty(nameof(SplineRoad.splineIndex));
            groundOffset = serializedObject.FindProperty(nameof(SplineRoad.groundOffset));

            roadMaterial = serializedObject.FindProperty(nameof(SplineRoad.roadMaterial));
            curbMaterial = serializedObject.FindProperty(nameof(SplineRoad.curbMaterial));

            width = serializedObject.FindProperty(nameof(SplineRoad.width));
            maxStep = serializedObject.FindProperty(nameof(SplineRoad.maxStep));
            maxDeviation = serializedObject.FindProperty(nameof(SplineRoad.maxDeviation));
            miterLimit = serializedObject.FindProperty(nameof(SplineRoad.miterLimit));

            curbWidth = serializedObject.FindProperty(nameof(SplineRoad.curbWidth));
            curbHeight = serializedObject.FindProperty(nameof(SplineRoad.curbHeight));

            generateColliders = serializedObject.FindProperty(nameof(SplineRoad.generateColliders));
            colliderLayer = serializedObject.FindProperty(nameof(SplineRoad.colliderLayer));
            colliderThickness = serializedObject.FindProperty(nameof(SplineRoad.colliderThickness));
            colliderStep = serializedObject.FindProperty(nameof(SplineRoad.colliderStep));

            snapToSurface = serializedObject.FindProperty(nameof(SplineRoad.snapToSurface));
            surfaceMask = serializedObject.FindProperty(nameof(SplineRoad.surfaceMask));
            surfaceProbeUp = serializedObject.FindProperty(nameof(SplineRoad.surfaceProbeUp));
            surfaceProbeDown = serializedObject.FindProperty(nameof(SplineRoad.surfaceProbeDown));
            surfaceOffset = serializedObject.FindProperty(nameof(SplineRoad.surfaceOffset));

            roadId = serializedObject.FindProperty(nameof(SplineRoad.roadId));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var road = (SplineRoad)target;

            DrawType(road);
            DrawMaterials(road);
            DrawGeometry(road);
            DrawColliders(road);
            DrawSurface(road);
            DrawIdentity(road);
            DrawActions();
            DrawState(road);

            bool changed = serializedObject.ApplyModifiedProperties();

            if (changed && AutoRebuild &&
                Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
                Run(rebuild: true);
        }

        // ============================================================
        // Секции
        // ============================================================

        private void DrawType(SplineRoad road)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Дорожка «{road.name}»", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(surface, new GUIContent("Тип",
                "Plane — только покрытие. PlaneWithCurbs — покрытие с бордюрами по обоим краям."));
            EditorGUILayout.PropertyField(splineIndex, new GUIContent("Сплайн", "Индекс сплайна внутри SplineContainer."));
            EditorGUILayout.PropertyField(groundOffset, new GUIContent("Смещение Y, м",
                "Подъём покрытия над линией сплайна (или над найденной поверхностью при включённой привязке)."));

            EditorGUILayout.EndVertical();
        }

        private void DrawMaterials(SplineRoad road)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Материалы", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(roadMaterial, new GUIContent("Покрытие"));
            EditorGUILayout.PropertyField(curbMaterial, new GUIContent("Бордюры",
                "Пусто — бордюры пойдут материалом покрытия."));

            if (road.roadMaterial == null)
                EditorGUILayout.HelpBox("Покрытие без материала: слот пустой, дорожку не видно.", MessageType.Warning);
        }

        private void DrawGeometry(SplineRoad road)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Геометрия", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(width, new GUIContent("Ширина, м"));

            using (new EditorGUI.DisabledScope(surface.enumValueIndex != (int)RoadSurface.PlaneWithCurbs))
            {
                EditorGUILayout.PropertyField(curbWidth, new GUIContent("Ширина бордюра, м",
                    "Лежит внутри покрытия — дорожка от бордюра не становится шире."));
                EditorGUILayout.PropertyField(curbHeight, new GUIContent("Высота бордюра, м"));
            }

            EditorGUILayout.Space(2f);

            EditorGUILayout.PropertyField(maxStep, new GUIContent("Макс. шаг кадров, м",
                "Верхняя граница шага тесселяции. На дуге шаг сам уходит ниже, чтобы держать прогиб."));
            EditorGUILayout.PropertyField(maxDeviation, new GUIContent("Стрелка прогиба, м",
                "Допустимое отклонение хорды от дуги. Меньше — больше треугольников."));
            EditorGUILayout.PropertyField(miterLimit, new GUIContent("Предел miter, ×",
                "Во сколько полуширин может вытянуться угол. 3 ≈ изломы до 140°; круче — фаска с предупреждением."));
        }

        private void DrawColliders(SplineRoad road)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Коллайдеры", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(generateColliders, new GUIContent("Создавать коллайдеры",
                "Выключено — покрытие чисто визуальное: сквозь него можно пролететь."));

            using (new EditorGUI.DisabledScope(!generateColliders.boolValue))
            {
                EditorGUILayout.PropertyField(colliderLayer, new GUIContent("Слой коллайдеров"));
                EditorGUILayout.PropertyField(colliderThickness, new GUIContent("Толщина плиты, м",
                    "Верх плиты стоит ровно на поверхности покрытия."));
                EditorGUILayout.PropertyField(colliderStep, new GUIContent("Шаг плит, м",
                    "Одна плита на столько метров. Мельче — точнее на дуге, но больше объектов."));
            }
        }

        private void DrawSurface(SplineRoad road)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Привязка к поверхности", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(snapToSurface, new GUIContent("Прикрепить к поверхности",
                "Луч вниз из каждого кадра: Y покрытия берётся с ближайшей поверхности, а не со сплайна. " +
                "Выключено — дорожка идёт строго по сплайну и может висеть в воздухе."));

            using (new EditorGUI.DisabledScope(!snapToSurface.boolValue))
            {
                EditorGUILayout.PropertyField(surfaceMask, new GUIContent("Маска поверхностей"));
                EditorGUILayout.PropertyField(surfaceProbeUp, new GUIContent("Старт луча выше, м",
                    "Насколько выше кадра начинать луч. Сплайн, нарисованный над землёй, требует запаса."));
                EditorGUILayout.PropertyField(surfaceProbeDown, new GUIContent("Глубина луча ниже, м"));
                EditorGUILayout.PropertyField(surfaceOffset, new GUIContent("Зазор до поверхности, м"));

                if (generateColliders.boolValue &&
                    (surfaceMask.intValue & (1 << colliderLayer.intValue)) != 0)
                {
                    EditorGUILayout.HelpBox(
                        $"Маска привязки включает слой коллайдеров дорожки ({LayerMask.LayerToName(colliderLayer.intValue)}). " +
                        "Луч будет попадать в собственную плиту — исключите этот слой из маски.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawIdentity(SplineRoad road)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Сгенерированное", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(roadId, new GUIContent("Id / папка мешей",
                "Имя группы сгенерированного. Пусто — берётся имя объекта. При конфликте имён инструмент допишет _2."));
            EditorGUILayout.LabelField($"папка: Generated/Roads/{road.EffectiveId}", EditorStyles.miniLabel);
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                var big = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 12 };

                if (GUILayout.Button("ПЕРЕСОБРАТЬ", big, GUILayout.Height(32f)))
                    Run(rebuild: true);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Сухой прогон (в лог)", GUILayout.Height(24f)))
                        Run(rebuild: false);

                    if (GUILayout.Button("Снести сгенерированное", GUILayout.Height(24f)))
                        Remove();
                }
            }

            bool auto = AutoRebuild;
            bool next = EditorGUILayout.ToggleLeft(
                "Пересобирать сразу при правке полей (на длинной трассе заметно тормозит)", auto);
            if (next != auto) AutoRebuild = next;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Play Mode: генерация выключена — меши лежат ассетами и в рантайме не переживут сцену.",
                    MessageType.Info);
        }

        private void DrawState(SplineRoad road)
        {
            int meshes = road.GetComponentsInChildren<MeshFilter>(true).Length;
            int boxes = road.GetComponentsInChildren<BoxCollider>(true).Length;

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(
                meshes == 0
                    ? "в сцене: сгенерированного нет"
                    : $"в сцене: меш-объектов {meshes}, бокс-коллайдеров {boxes}" +
                      (road.generateColliders ? "" : " (коллайдеры выключены)"),
                EditorStyles.miniLabel);

            Vector3 scale = road.transform.lossyScale;
            if (Mathf.Abs(scale.x - 1f) > 1e-3f || Mathf.Abs(scale.y - 1f) > 1e-3f || Mathf.Abs(scale.z - 1f) > 1e-3f)
            {
                EditorGUILayout.HelpBox(
                    $"Масштаб корня {scale.x:F3}/{scale.y:F3}/{scale.z:F3} ≠ 1 — ширина покрытия и коллайдеры " +
                    "разъедутся. Ставьте 1/1/1.", MessageType.Error);
            }

            if (!string.IsNullOrEmpty(lastReport))
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("Последняя операция", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(lastReport, MessageType.None);
            }
        }

        // ============================================================
        // Действия
        // ============================================================

        private void Run(bool rebuild)
        {
            var sb = new StringBuilder();

            foreach (Object o in targets)
            {
                var road = o as SplineRoad;
                if (road == null) continue;

                sb.AppendLine(SplineRoadTool.Rebuild(road, !rebuild));
            }

            lastReport = sb.ToString().TrimEnd();
            if (lastReport.Length > 0) Debug.Log(lastReport);

            Repaint();
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
        }

        private void Remove()
        {
            var sb = new StringBuilder();

            foreach (Object o in targets)
            {
                var road = o as SplineRoad;
                if (road == null) continue;

                SplineRoadTool.Remove(road);
                sb.AppendLine($"«{road.name}»: сгенерированное снесено, папка Generated/Roads/{road.EffectiveId} удалена.");
            }

            lastReport = sb.ToString().TrimEnd();

            Repaint();
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
        }
    }
}
