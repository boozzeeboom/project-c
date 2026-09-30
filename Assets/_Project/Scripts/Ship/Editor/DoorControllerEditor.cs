// =====================================================================================
// DoorControllerEditor.cs — понятный инспектор для DoorController
// =====================================================================================
// Показывает ТОЛЬКО поля выбранного типа двери (не всё в кучу):
// тип → направление словами → дистанция «по габариту» → превью.
// Плюс кнопки: превью открыть/закрыть, создание пивотов петель, подстановка габарита.
// =====================================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Ship
{
    [CustomEditor(typeof(DoorController))]
    public class DoorControllerEditor : UnityEditor.Editor
    {
        private static readonly GUIContent[] TypeLabels =
        {
            new GUIContent("Сдвижная · одна створка", "Обычная дверь каюты: панель отъезжает в сторону"),
            new GUIContent("Сдвижная · две створки (разъезд)", "Широкие проёмы и АНГАРНЫЕ ворота: створки разъезжаются"),
            new GUIContent("Распашная · одна створка", "Дверь на петлях: петли слева или справа"),
            new GUIContent("Распашная · две створки", "Двустворчатые ворота на петлях"),
            new GUIContent("Подъёмная (ангарная шторка)", "АНГАР/гараж: панель уезжает вверх"),
        };

        private static readonly GUIContent[] SlideSideLabels =
        {
            new GUIContent("Влево (−X)"), new GUIContent("Вправо (+X)"),
            new GUIContent("Вверх (+Y)"), new GUIContent("Вниз (−Y)"),
            new GUIContent("Вперёд (+Z)"), new GUIContent("Назад (−Z)"),
        };

        private static readonly GUIContent[] DistanceModeLabels =
        {
            new GUIContent("По габариту створки (не улетит)", "Дистанция = ширина створки × множитель. Рекомендуется."),
            new GUIContent("Вручную (метры)", "Дистанция задаётся числом — следите за предупреждением про «улёт»."),
        };

        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        private SerializedProperty P(string n) => serializedObject.FindProperty(n);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var door = (DoorController)target;

            // ---------- Тип ----------
            EditorGUILayout.LabelField("Тип двери", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var typeProp = P("doorType");
            int newType = EditorGUILayout.Popup(
                new GUIContent("Тип", "Какой механизм у двери"),
                typeProp.enumValueIndex, TypeLabels);
            if (EditorGUI.EndChangeCheck())
            {
                typeProp.enumValueIndex = newType;
                door.EndPreview();
            }
            DrawTypeHint((DoorController.DoorType)typeProp.enumValueIndex);

            EditorGUILayout.Space(4);

            // ---------- Общее ----------
            EditorGUILayout.LabelField("Общее", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(P("startOpen"), new GUIContent("Открыта на старте"));
            EditorGUILayout.Slider(P("openDuration"), 0.1f, 5f,
                new GUIContent("Длительность (с)", "Фиксированное время открытия — предсказуемо при любой дистанции"));
            EditorGUILayout.PropertyField(P("openCurve"), new GUIContent("Плавность"));
            EditorGUILayout.PropertyField(P("autoCloseDelay"),
                new GUIContent("Автозакрытие (с)", "0 — не закрывать автоматически"));
            EditorGUILayout.PropertyField(P("onOpened"));
            EditorGUILayout.PropertyField(P("onClosed"));

            EditorGUILayout.Space(2);
            if (GUILayout.Button("🧭 Оси рамки = мировые (Y вверх)", GUILayout.Height(24)))
                AlignFrameToWorld(door);
            EditorGUILayout.HelpBox(
                "Все направления задаются В ОСЯХ РАМКИ (этого объекта). " +
                "Если корень корабля повёрнут (как heavyII: 270/90) — стрелки рамки не совпадают " +
                "с верхом/бортами корабля: нажмите кнопку, стрелки встанут по миру, " +
                "и «Вверх/Вправо» будут значить то, что видно. Корабль увозит оси с собой.",
                MessageType.None);

            EditorGUILayout.Space(4);

            // ---------- Секция типа ----------
            switch ((DoorController.DoorType)typeProp.enumValueIndex)
            {
                case DoorController.DoorType.SlidingSingle:
                    DrawSlidingSingle(door);
                    break;
                case DoorController.DoorType.SlidingDouble:
                    DrawSlidingDouble(door);
                    break;
                case DoorController.DoorType.HingedSingle:
                    DrawHingedSingle(door);
                    break;
                case DoorController.DoorType.HingedDouble:
                    DrawHingedDouble(door);
                    break;
                case DoorController.DoorType.LiftUp:
                    DrawLiftUp(door);
                    break;
            }

            EditorGUILayout.Space(6);

            // ---------- Проверка ----------
            DrawIssues(door);

            EditorGUILayout.Space(4);

            // ---------- Превью ----------
            EditorGUILayout.LabelField("Проверка в редакторе", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ Открыть (превью)", GUILayout.Height(26)))
                Preview(door, true);
            if (GUILayout.Button("■ Закрыть (превью)", GUILayout.Height(26)))
                Preview(door, false);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "Превью двигает панель прямо в сцене. Отмена — Ctrl+Z.\n" +
                "Зелёная стрелка/дужка в Scene View показывает, куда поедет дверь.",
                MessageType.None);

            if (serializedObject.ApplyModifiedProperties())
                door.EndPreview();
        }

        // ============================ Секции ============================

        private void DrawTypeHint(DoorController.DoorType type)
        {
            string hint;
            switch (type)
            {
                case DoorController.DoorType.SlidingSingle:
                    hint = "Одна панель отъезжает в сторону. Для кают и коридоров.";
                    break;
                case DoorController.DoorType.SlidingDouble:
                    hint = "Две панели разъезжаются от центра. Для широких проёмов и АНГАРНЫХ ворот.";
                    break;
                case DoorController.DoorType.HingedSingle:
                    hint = "Панель крутится вокруг своего края СРАЗУ — пивот не нужен. " +
                           "Для точной линии петель: кнопка ниже или перетащите пивот в поле.";
                    break;
                case DoorController.DoorType.HingedDouble:
                    hint = "Каждая створка крутится вокруг своего внешнего края СРАЗУ. " +
                           "Пивоты нужны только для точной линии петель.";
                    break;
                case DoorController.DoorType.LiftUp:
                    hint = "Панель уезжает строго вверх. Для АНГАРНЫХ шторок и гаражей.";
                    break;
                default:
                    hint = "";
                    break;
            }
            EditorGUILayout.HelpBox(hint, MessageType.Info);
        }

        private void DrawSlidingSingle(DoorController door)
        {
            EditorGUILayout.LabelField("Створка", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(P("doorModel"), new GUIContent("Панель",
                "Пусто = двигается сам объект (так в старых префабах). Для проёма лучше child-панель."));
            DrawSlideDirection(P("slideSide"));
            DrawDistanceBlock(door, P("distanceMode"), P("sizeFactor"), P("manualDistance"),
                ResolvePanel(door, P("doorModel")), SlideAxisOf(P("slideSide")));
        }

        private void DrawSlidingDouble(DoorController door)
        {
            EditorGUILayout.LabelField("Створки разъезда", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(P("leftPanel"), new GUIContent("Левая створка"));
            EditorGUILayout.PropertyField(P("rightPanel"), new GUIContent("Правая створка"));
            EditorGUILayout.PropertyField(P("doubleAxis"), new GUIContent("Ось разъезда",
                "X — влево-вправо, Z — вперёд-назад"));
            Vector3 dblAxis = P("doubleAxis").enumValueIndex == 0 ? Vector3.right : Vector3.forward;
            if (GUILayout.Button("📏 Ось по створкам"))
                DetectDoubleAxis(door);
            EditorGUILayout.PropertyField(P("doubleDistanceMode"), new GUIContent("Дистанция",
                "По габариту — каждая створка отъедет на свою ширину"));
            var mode = (DoorController.OpenDistanceMode)P("doubleDistanceMode").enumValueIndex;
            if (mode == DoorController.OpenDistanceMode.AutoBySize)
            {
                EditorGUILayout.Slider(P("doubleSizeFactor"), 0.1f, 2f, new GUIContent("Множитель габарита"));
            }
            else
            {
                EditorGUILayout.PropertyField(P("doubleManualDistance"),
                    new GUIContent("Дистанция каждой (м)"));
            }
            DrawMeasuredInfo(door, P("leftPanel"), "Левая", dblAxis);
            DrawMeasuredInfo(door, P("rightPanel"), "Правая", dblAxis);
            if (mode == DoorController.OpenDistanceMode.Manual && GUILayout.Button("📐 Подставить габариты обеих"))
                ApplyMeasured(P("doubleManualDistance"),
                    MaxMeasured(door, P("leftPanel"), P("rightPanel"), dblAxis));
        }

        private void DrawHingedSingle(DoorController door)
        {
            EditorGUILayout.LabelField("Петли", EditorStyles.boldLabel);
            if (GUILayout.Button("⚙ Сделать распашной из ЭТОГО объекта", GUILayout.Height(28)))
                ConvertSelfToHinged(door, P("hingeSide").enumValueIndex == 0);
            EditorGUILayout.HelpBox(
                "Один клик: пивот на краю объекта + объект ребёнком + вертикаль оси. " +
                "Сторона края — из поля «Петли» ниже. Дальше жмите превью.",
                MessageType.None);
            EditorGUILayout.PropertyField(P("hingePivot"), new GUIContent("Пивот петель (необязательно)",
                "Точная линия петель. Пусто = створка крутится вокруг собственного края (превью работает сразу)."));
            if (P("hingePivot").objectReferenceValue == null)
                EditorGUILayout.HelpBox("Пивота нет — используется край створки. Превью работает.", MessageType.Info);
            EditorGUILayout.HelpBox(
                "Пивотом может быть сам объект двери (перетащите его в поле) — " +
                "удобно, если иерархия петель уже приехала из импорта.",
                MessageType.None);
            EditorGUILayout.PropertyField(P("hingePanel"), new GUIContent("Панель (створка)",
                "Створка двери. Пусто = сам объект двери. Без пивота крутится вокруг своего края."));
            EditorGUILayout.PropertyField(P("hingeSide"), new GUIContent("Петли",
                "С какой стороны петли (вид спереди). Влияет только на знак по умолчанию."));
            EditorGUILayout.Slider(P("openAngle"), -180f, 180f, new GUIContent("Угол открытия°",
                "Знак разворачивает сторону. Открылась не туда — поменяйте знак."));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⚙ Пивот слева")) CreatePivot(door, P("hingePivot"), P("hingePanel"), true);
            if (GUILayout.Button("⚙ Пивот справа")) CreatePivot(door, P("hingePivot"), P("hingePanel"), false);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("🎯 Ось пивота вертикально (Y вверх)"))
                AlignPivotVertical(door, P("hingePivot"));
            DrawAttachButton(door, P("hingePivot"), P("hingePanel"));
            EditorGUILayout.HelpBox(
                "Кнопка ⚙ ставит пивот на край панели по её габариту и сажает панель его ребёнком " +
                "(мировое положение панели сохраняется). Предполагается, что ширина двери — вдоль X рамки.\n" +
                "Кнопка 🎯 доворачивает пивот так, чтобы его Y смотрел строго вверх, " +
                "а створка при этом ОСТАЛАСЬ на месте (дверь распахивается вертикально, а не как форточка).",
                MessageType.None);
        }

        private void DrawHingedDouble(DoorController door)
        {
            EditorGUILayout.LabelField("Петли двух створок", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(P("leftPivot"), new GUIContent("Левый пивот (необязательно)"));
            EditorGUILayout.PropertyField(P("rightPivot"), new GUIContent("Правый пивот (необязательно)"));
            EditorGUILayout.PropertyField(P("leftHingePanel"), new GUIContent("Левая панель (створка)"));
            EditorGUILayout.PropertyField(P("rightHingePanel"), new GUIContent("Правая панель (створка)"));
            EditorGUILayout.Slider(P("doubleOpenAngle"), 5f, 170f, new GUIContent("Угол каждой°"));
            EditorGUILayout.PropertyField(P("invertDoubleSwing"),
                new GUIContent("Инвертировать обе", "Если створки открылись не туда"));
            if (GUILayout.Button("⚙ Создать оба пивота по краям"))
            {
                CreatePivot(door, P("leftPivot"), P("leftHingePanel"), true);
                CreatePivot(door, P("rightPivot"), P("rightHingePanel"), false);
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 Левый вертикально")) AlignPivotVertical(door, P("leftPivot"));
            if (GUILayout.Button("🎯 Правый вертикально")) AlignPivotVertical(door, P("rightPivot"));
            EditorGUILayout.EndHorizontal();
            DrawAttachButton(door, P("leftPivot"), P("leftHingePanel"));
            DrawAttachButton(door, P("rightPivot"), P("rightHingePanel"));
        }

        private void DrawLiftUp(DoorController door)
        {
            EditorGUILayout.LabelField("Шторка", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(P("liftPanel"), new GUIContent("Панель шторки",
                "Пусто = двигается сам объект."));
            DrawDistanceBlock(door, P("liftDistanceMode"), P("liftSizeFactor"), P("liftManualHeight"),
                liftPanelOf(door), Vector3.up);
        }

        // ============================ Общие виджеты ============================

        private void DrawSlideDirection(SerializedProperty sideProp)
        {
            sideProp.enumValueIndex = EditorGUILayout.Popup(
                new GUIContent("Куда отъезжает", "В осях РАМКИ (объекта двери) — её стрелки видно в Scene View"),
                sideProp.enumValueIndex, SlideSideLabels);
        }

        private void DrawDistanceBlock(DoorController door,
            SerializedProperty modeProp, SerializedProperty factorProp, SerializedProperty manualProp,
            Transform panel, Vector3 localAxis)
        {
            modeProp.enumValueIndex = EditorGUILayout.Popup(
                new GUIContent("Дистанция"), modeProp.enumValueIndex, DistanceModeLabels);
            var mode = (DoorController.OpenDistanceMode)modeProp.enumValueIndex;
            if (mode == DoorController.OpenDistanceMode.AutoBySize)
            {
                EditorGUILayout.Slider(factorProp, 0.1f, 2f,
                    new GUIContent("Множитель габарита", "1 = ровно на свою ширину/высоту"));
                DrawMeasuredInfo(door, panel, "Створка", localAxis);
            }
            else
            {
                EditorGUILayout.PropertyField(manualProp, new GUIContent("Дистанция (м)"));
                DrawMeasuredInfo(door, panel, "Створка", localAxis);
                if (panel != null && GUILayout.Button("📐 Подставить габарит"))
                    ApplyMeasured(manualProp, MeasureOf(door, panel, localAxis));
            }
        }

        private void DrawMeasuredInfo(DoorController door, SerializedProperty panelProp, string label, Vector3 axis)
        {
            Transform panel = panelProp != null && panelProp.propertyType == SerializedPropertyType.ObjectReference
                ? panelProp.objectReferenceValue as Transform
                : null;
            DrawMeasuredInfo(door, panel, label, axis);
        }

        private void DrawMeasuredInfo(DoorController door, Transform panel, string label, Vector3 axis)
        {
            if (panel == null)
            {
                EditorGUILayout.LabelField($"{label}: панель не задана", EditorStyles.miniLabel);
                return;
            }
            if (DoorController.TryMeasurePanelSize(panel, door.transform, axis, out float size))
                EditorGUILayout.LabelField($"{label}: габарит вдоль оси — {size:F2} м", EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField($"{label}: габарит не измерился (нет меша/коллайдера)",
                    EditorStyles.miniLabel);
        }

        private void DrawIssues(DoorController door)
        {
            _errors.Clear();
            _warnings.Clear();
            door.GetSetupIssues(_errors, _warnings);

            foreach (string e in _errors)
                EditorGUILayout.HelpBox("⛔ " + e, MessageType.Error);
            foreach (string w in _warnings)
                EditorGUILayout.HelpBox("⚠ " + w, MessageType.Warning);
            if (_errors.Count == 0 && _warnings.Count == 0)
                EditorGUILayout.HelpBox("✔ Настройка в порядке.", MessageType.Info);
        }

        // ============================ Действия ============================

        private void Preview(DoorController door, bool open)
        {
            // Без обязательных ссылок превью молча ничего не двигало — объясняем причину.
            _errors.Clear();
            _warnings.Clear();
            door.GetSetupIssues(_errors, _warnings);
            if (_errors.Count > 0)
            {
                EditorUtility.DisplayDialog("Превью невозможно",
                    "Сначала исправьте:\n• " + string.Join("\n• ", _errors.ToArray()),
                    "Понятно");
                return;
            }
            // Undo для всех двигаемых трансформов
            var moved = CollectMoved(door);
            if (moved.Count > 0)
                Undo.RecordObjects(moved.ToArray(), open ? "Door preview open" : "Door preview close");
            door.ApplyPreviewPose(open);
            if (!open) door.EndPreview();
            EditorUtility.SetDirty(door);
            SceneView.RepaintAll();
        }

        private static List<Object> CollectMoved(DoorController door)
        {
            var so = new SerializedObject(door);
            var list = new List<Object>();
            void Add(string n)
            {
                var t = so.FindProperty(n)?.objectReferenceValue as Transform;
                if (t != null && !list.Contains(t)) list.Add(t);
            }
            var type = (DoorController.DoorType)so.FindProperty("doorType").enumValueIndex;
            switch (type)
            {
                case DoorController.DoorType.SlidingSingle:
                    Add("doorModel");
                    break;
                case DoorController.DoorType.SlidingDouble:
                    Add("leftPanel"); Add("rightPanel");
                    break;
                case DoorController.DoorType.HingedSingle:
                    Add("hingePivot");
                    break;
                case DoorController.DoorType.HingedDouble:
                    Add("leftPivot"); Add("rightPivot");
                    break;
                case DoorController.DoorType.LiftUp:
                    Add("liftPanel");
                    break;
            }
            if (list.Count == 0) list.Add(door.transform); // панель = сам объект
            return list;
        }

        /// <summary>
        /// Ширина панели для постановки пивота: максимальный габарит по X/Z рамки
        /// (дверь может быть развёрнута — ширина не обязана лежать вдоль X).
        /// </summary>
        private static float PanelWidth(DoorController door, Transform panel)
        {
            bool hx = DoorController.TryMeasurePanelSize(panel, door.transform, Vector3.right, out float sx);
            bool hz = DoorController.TryMeasurePanelSize(panel, door.transform, Vector3.forward, out float sz);
            if (hx && (!hz || sx >= sz)) return Mathf.Max(sx, 0.01f);
            if (hz) return Mathf.Max(sz, 0.01f);
            return 1f;
        }

        private void CreatePivot(DoorController door, SerializedProperty pivotProp,
            SerializedProperty panelProp, bool leftSide)
        {
            Transform panel = panelProp.objectReferenceValue as Transform;
            if (panel == null)
            {
                // Без явной панели кнопка посадила бы под пивот ВЕСЬ объект двери —
                // запрещаем и объясняем вместо молчаливой поломки иерархии.
                EditorUtility.DisplayDialog("Сначала назначьте панель",
                    "Перетащите створку в поле «Панель», затем нажмите кнопку снова.\n\n" +
                    "Если меша створки нет вообще (только маркеры петель) — " +
                    "продублируйте меш (например, стену проёма) и назначьте дубликат.",
                    "Понятно");
                return;
            }
            if (panel.parent == null)
            {
                EditorUtility.DisplayDialog("Пивот не создан",
                    "Панель — корень иерархии (без родителя). Положите дверь под родителя и повторите.",
                    "Понятно");
                return;
            }

            float w = PanelWidth(door, panel);

            Transform parent = panel.parent;
            Vector3 edgeLocal = panel.localPosition + new Vector3(leftSide ? -w * 0.5f : w * 0.5f, 0f, 0f);

            var go = new GameObject(panel.name + (leftSide ? "_HingeL" : "_HingeR"));
            Undo.RegisterCreatedObjectUndo(go, "Создать пивот петли");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = edgeLocal;
            go.transform.localRotation = panel.localRotation;
            go.transform.localScale = Vector3.one;

            // Панель — ребёнок пивота, мировое положение сохраняется
            Undo.SetTransformParent(panel, go.transform, true, "Посадить панель на пивот");

            pivotProp.objectReferenceValue = go.transform;
            serializedObject.ApplyModifiedProperties();
            door.EndPreview();
            EditorGUIUtility.PingObject(go);
        }

        /// <summary>
        /// One-click распашная из объекта двери: пивот на краю + объект ребёнком
        /// + вертикаль оси + назначение полей. Ноль ручной возни — как у слайдера.
        /// Работает и в префабе. Требует меш/коллайдер на объекте.
        /// </summary>
        private void ConvertSelfToHinged(DoorController door, bool leftSide)
        {
            Transform panel = door.transform;
            if (panel.parent == null)
            {
                EditorUtility.DisplayDialog("Нельзя",
                    "Объект двери — корень иерархии (без родителя). Положите дверь под родителя и повторите.",
                    "Понятно");
                return;
            }
            if (!DoorController.HasGeometry(panel))
            {
                EditorUtility.DisplayDialog("Некого вешать на петли",
                    $"У «{panel.name}» нет ни меша, ни коллайдера — это маркер, а не створка.\n\n" +
                    "Продублируйте меш створки (например, стену проёма), назовите и повторите на нём.",
                    "Понятно");
                return;
            }

            float w = PanelWidth(door, panel);
            Transform parent = panel.parent;
            Vector3 edgeLocal = panel.localPosition + new Vector3(leftSide ? -w * 0.5f : w * 0.5f, 0f, 0f);

            var go = new GameObject(panel.name + (leftSide ? "_HingeL" : "_HingeR"));
            Undo.RegisterCreatedObjectUndo(go, "Пивот для распашной двери");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = edgeLocal;
            go.transform.localRotation = panel.localRotation;
            go.transform.localScale = Vector3.one;

            Undo.SetTransformParent(panel, go.transform, true, "Дверь на петли");

            P("hingePivot").objectReferenceValue = go.transform;
            P("hingePanel").objectReferenceValue = panel;
            serializedObject.ApplyModifiedProperties();

            VerticalizePivot(go.transform);

            door.EndPreview();
            EditorUtility.SetDirty(door);
            SceneView.RepaintAll();
            EditorGUIUtility.PingObject(go);
        }

        // ============================ Ориентация ============================

        /// <summary>
        /// Повернуть рамку (объект двери) так, чтобы её оси совпали с мировыми.
        /// После этого «Вверх/Вправо/Вперёд» значат то, что видит дизайнер.
        /// Дети рамки повернутся вместе с ней (Ctrl+Z отменяет).
        /// </summary>
        private void AlignFrameToWorld(DoorController door)
        {
            Undo.RecordObject(door.transform, "Выровнять рамку двери");
            door.transform.rotation = Quaternion.identity;
            door.EndPreview();
            EditorUtility.SetDirty(door);
            SceneView.RepaintAll();
        }

        /// <summary>Выбрать ось разъезда по фактическому расположению створок (в осях рамки).</summary>
        private void DetectDoubleAxis(DoorController door)
        {
            var l = P("leftPanel").objectReferenceValue as Transform;
            var r = P("rightPanel").objectReferenceValue as Transform;
            if (l == null || r == null)
            {
                EditorUtility.DisplayDialog("Ось не определена",
                    "Сначала назначьте обе створки.", "Понятно");
                return;
            }
            Vector3 d = door.transform.InverseTransformDirection(r.position - l.position);
            if (d.sqrMagnitude < 1e-8f)
            {
                EditorUtility.DisplayDialog("Ось не определена",
                    "Створки стоят в одной точке — разнесите их по проёму.", "Понятно");
                return;
            }
            d.Normalize();
            float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
            if (ay > ax && ay > az)
            {
                EditorUtility.DisplayDialog("Ось не подошла",
                    "Створки разнесены ПО ВЕРТИКАЛИ — разъезд влево-вправо/вперёд-назад не подойдёт. " +
                    "Используйте две шторки LiftUp или переставьте створки.",
                    "Понятно");
                return;
            }
            P("doubleAxis").enumValueIndex = ax >= az ? 0 : 1;
            serializedObject.ApplyModifiedProperties();
            door.EndPreview();
        }

        /// <summary>
        /// Довернуть пивот минимальным поворотом так, чтобы его Y смотрел строго вверх.
        /// Вся ветка потомков сохраняет МИРОВЫЕ позы (закрытый вид не меняется),
        /// а hinge-вращение становится вертикальным вместо «форточки».
        /// </summary>
        private void AlignPivotVertical(DoorController door, SerializedProperty pivotProp)
        {
            var piv = pivotProp.objectReferenceValue as Transform;
            if (piv == null)
            {
                EditorUtility.DisplayDialog("Пивот не задан",
                    "Сначала задайте или создайте пивот.", "Понятно");
                return;
            }
            VerticalizePivot(piv);
            door.EndPreview();
            EditorUtility.SetDirty(door);
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Довернуть пивот минимальным поворотом так, чтобы его Y смотрел строго вверх.
        /// Вся ветка потомков сохраняет МИРОВЫЕ позы (закрытый вид не меняется).
        /// </summary>
        private static void VerticalizePivot(Transform piv)
        {
            var subtree = piv.GetComponentsInChildren<Transform>(true);
            var rec = new List<Object> { piv };
            foreach (var c in subtree)
                if (c != piv) rec.Add(c);
            Undo.RecordObjects(rec.ToArray(), "Вертикаль пивота");

            Vector3 curY = piv.rotation * Vector3.up;
            Quaternion q1;
            if (Vector3.Dot(curY, Vector3.up) < -0.9999f)
                q1 = Quaternion.AngleAxis(180f, piv.rotation * Vector3.forward) * piv.rotation;
            else
                q1 = Quaternion.FromToRotation(curY, Vector3.up) * piv.rotation;

            var pos = new List<Vector3>();
            var rot = new List<Quaternion>();
            foreach (var c in subtree)
            {
                if (c == piv) continue;
                pos.Add(c.position);
                rot.Add(c.rotation);
            }
            piv.rotation = q1;
            int i = 0;
            foreach (var c in subtree)
            {
                if (c == piv) continue;
                c.position = pos[i];
                c.rotation = rot[i];
                i++;
            }
        }

        /// <summary>
        /// Кнопка «посадить панель на пивот», если она назначена, но висит мимо.
        /// Именно этот случай даёт «гизмо едет, меш стоит»: пивот крутит пустоту.
        /// </summary>
        private void DrawAttachButton(DoorController door, SerializedProperty pivotProp, SerializedProperty panelProp)
        {
            var piv = pivotProp.objectReferenceValue as Transform;
            var pan = panelProp.objectReferenceValue as Transform;
            if (piv == null || pan == null || pan == piv) return;
            bool attached = false;
            for (var p = pan; p != null; p = p.parent)
                if (p == piv) { attached = true; break; }
            if (attached) return;

            EditorGUILayout.HelpBox(
                $"Панель «{pan.name}» НЕ под пивотом «{piv.name}» — превью будет крутить пустоту, а меш стоять.",
                MessageType.Error);
            if (GUILayout.Button("🔗 Посадить панель на пивот (место сохранить)", GUILayout.Height(26)))
            {
                Undo.SetTransformParent(pan, piv, true, "Посадить панель на пивот");
                door.EndPreview();
                EditorUtility.SetDirty(door);
                SceneView.RepaintAll();
            }
        }

        // ============================ Хелперы ============================

        private static Transform ResolvePanel(DoorController door, SerializedProperty panelProp)
        {
            var t = panelProp.objectReferenceValue as Transform;
            return t != null ? t : door.transform;
        }

        private static Transform liftPanelOf(DoorController door)
        {
            var so = new SerializedObject(door);
            var t = so.FindProperty("liftPanel").objectReferenceValue as Transform;
            return t != null ? t : door.transform;
        }

        private static Vector3 SlideAxisOf(SerializedProperty sideProp)
        {
            return DoorController.SlideSideToLocal((DoorController.SlideSide)sideProp.enumValueIndex);
        }

        private static float MeasureOf(DoorController door, Transform panel, Vector3 axis)
        {
            if (panel != null && DoorController.TryMeasurePanelSize(panel, door.transform, axis, out float s) && s > 1e-4f)
                return s;
            return 1.5f;
        }

        private static float MaxMeasured(DoorController door, SerializedProperty a,
            SerializedProperty b, Vector3 axis)
        {
            float best = 0f;
            foreach (var p in new[] { a, b })
            {
                var t = p.objectReferenceValue as Transform;
                if (t != null && DoorController.TryMeasurePanelSize(t, door.transform, axis, out float s))
                    best = Mathf.Max(best, s);
            }
            return best > 1e-4f ? best : 1.5f;
        }

        private static void ApplyMeasured(SerializedProperty manualProp, float value)
        {
            manualProp.floatValue = value;
            manualProp.serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
