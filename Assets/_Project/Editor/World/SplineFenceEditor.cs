using System.Text;
using ProjectC.World.Generation;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Инспектор забора — вместо похода в Tools на каждую правку.
    ///
    /// Почему инспектор, а не отдельное окно: окно надо держать открытым и оно теряет объект при смене
    /// выделения, а инспектор уже висит на самом сплайне и не закрывается, пока объект выделен. Кнопка
    /// «Пересобрать» стоит там же, где меняются высота и шаг секции, — правка и результат в одном месте.
    ///
    /// Правки полей пересобирают забор только при включённой галочке (EditorPrefs, общая на проект):
    /// на 100 м забора пересборка занимает доли секунды и тянуть её на каждый шаг драга мышью нельзя.
    /// </summary>
    [CustomEditor(typeof(SplineFence))]
    [CanEditMultipleObjects]
    public sealed class SplineFenceEditor : UnityEditor.Editor
    {
        private const string AutoRebuildKey = "ProjectC.SplineFence.AutoRebuild";

        private static string lastReport;

        private SerializedProperty type;
        private SerializedProperty splineIndex;
        private SerializedProperty groundOffset;
        private SerializedProperty colliderLayer;
        private SerializedProperty fenceId;
        private SerializedProperty heightOverride;
        private SerializedProperty sectionStepOverride;

        private static bool AutoRebuild
        {
            get => EditorPrefs.GetBool(AutoRebuildKey, false);
            set => EditorPrefs.SetBool(AutoRebuildKey, value);
        }

        private void OnEnable()
        {
            type = serializedObject.FindProperty(nameof(SplineFence.type));
            splineIndex = serializedObject.FindProperty(nameof(SplineFence.splineIndex));
            groundOffset = serializedObject.FindProperty(nameof(SplineFence.groundOffset));
            colliderLayer = serializedObject.FindProperty(nameof(SplineFence.colliderLayer));
            fenceId = serializedObject.FindProperty(nameof(SplineFence.fenceId));
            heightOverride = serializedObject.FindProperty(nameof(SplineFence.heightOverride));
            sectionStepOverride = serializedObject.FindProperty(nameof(SplineFence.sectionStepOverride));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var fence = (SplineFence)target;

            DrawHeader(fence);
            DrawSize(fence);
            DrawIdentity(fence);
            DrawActions();
            DrawState(fence);

            bool changed = serializedObject.ApplyModifiedProperties();

            // пересборка только на настоящем событии ввода, не на Layout/Repaint
            if (changed && AutoRebuild &&
                Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
                Run(rebuild: true);
        }

        // ============================================================
        // Секции
        // ============================================================

        private void DrawHeader(SplineFence fence)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Забор «{fence.name}»", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(type, new GUIContent("Тип", "FenceTypeDefinition: стиль, материалы, параметры."));
            EditorGUILayout.PropertyField(splineIndex, new GUIContent("Сплайн", "Индекс сплайна внутри SplineContainer."));
            EditorGUILayout.PropertyField(groundOffset, new GUIContent("Смещение Y, м", "Приподнять или утопить забор над линией сплайна."));

            if (fence.type == null)
            {
                EditorGUILayout.HelpBox("Тип не задан — пересборка невозможна.", MessageType.Error);

                if (GUILayout.Button("Подставить тип по умолчанию"))
                {
                    FenceTypeDefinition fallback = SplineFenceTool.FindDefaultType();
                    if (fallback != null) type.objectReferenceValue = fallback;
                }
            }
            else if (fence.type.style == FenceStyle.Bar)
            {
                EditorGUILayout.LabelField(
                    "Bar: балки и прутки заданы абсолютными Y в типе, поэтому высоту меняет поле ниже, а не height в ассете типа.",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSize(SplineFence fence)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Габариты (0 — взять из типа)", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(heightOverride, new GUIContent("Высота, м",
                "Высота забора. Пропорционально тянет полотно, столбы, балки, прутки и высоту коллайдера; " +
                "толщины и профили остаются."));
            EditorGUILayout.LabelField(
                $"итог {fence.EffectiveHeight:F2} м   ·   в типе {TypeHeight(fence):F2} м",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(2f);

            EditorGUILayout.PropertyField(sectionStepOverride, new GUIContent("Шаг секции, м",
                "Расстояние между столбами. От него же берётся максимальный шаг кадров пути."));
            EditorGUILayout.LabelField(
                $"итог {fence.EffectiveSectionStep:F2} м   ·   в типе {TypeStep(fence):F2} м",
                EditorStyles.miniLabel);
        }

        private void DrawIdentity(SplineFence fence)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Сгенерированное", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(colliderLayer, new GUIContent("Слой коллайдеров", "Слой, на который попадут бокс-коллайдеры."));
            EditorGUILayout.PropertyField(fenceId, new GUIContent("Id / папка мешей",
                "Имя группы сгенерированного. Пусто — берётся имя объекта. При конфликте имён инструмент допишет _2."));
            EditorGUILayout.LabelField($"папка: Generated/Fences/{fence.EffectiveId}", EditorStyles.miniLabel);
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
                "Пересобирать сразу при правке полей (на длинном заборе заметно тормозит)", auto);
            if (next != auto) AutoRebuild = next;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Play Mode: генерация выключена — меши лежат ассетами и в рантайме не переживут сцену.",
                    MessageType.Info);
        }

        private void DrawState(SplineFence fence)
        {
            int sections = fence.GetComponentsInChildren<MeshFilter>(true).Length;
            int boxes = fence.GetComponentsInChildren<BoxCollider>(true).Length;

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(
                sections == 0
                    ? "в сцене: сгенерированного нет"
                    : $"в сцене: секций {sections}, бокс-коллайдеров {boxes}",
                EditorStyles.miniLabel);

            Vector3 scale = fence.transform.lossyScale;
            if (Mathf.Abs(scale.x - 1f) > 1e-3f || Mathf.Abs(scale.y - 1f) > 1e-3f || Mathf.Abs(scale.z - 1f) > 1e-3f)
            {
                EditorGUILayout.HelpBox(
                    $"Масштаб корня {scale.x:F3}/{scale.y:F3}/{scale.z:F3} ≠ 1 — процедурные материалы работают в " +
                    "object space: кладка и шум поплывут, коллайдеры разъедутся с мешем. Ставьте 1/1/1.",
                    MessageType.Error);
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
                var fence = o as SplineFence;
                if (fence == null) continue;

                sb.AppendLine(SplineFenceTool.Rebuild(fence, !rebuild));
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
                var fence = o as SplineFence;
                if (fence == null) continue;

                SplineFenceTool.Remove(fence);
                sb.AppendLine($"«{fence.name}»: сгенерированное снесено, папка Generated/Fences/{fence.EffectiveId} удалена.");
            }

            lastReport = sb.ToString().TrimEnd();

            Repaint();
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
        }

        // ============================================================
        // Мелочи
        // ============================================================

        private static float TypeHeight(SplineFence fence) => fence.type != null ? fence.type.height : 0f;

        private static float TypeStep(SplineFence fence) => fence.type != null ? fence.type.postSpacing : 0f;
    }
}
