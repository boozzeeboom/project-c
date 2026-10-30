using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProjectC.Ship;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// PRIMUM: сборка префабов дверей из спецификации (меши-боксы → ассеты → префаб).
    ///
    /// Зачем: <c>*.prefab</c> и <c>*.fbx</c> в этом проекте не версионируются (живут в Lore VCS),
    /// поэтому префабы должны пересобираться 1:1 из кода. Этот скрипт — источник истины по геометрии
    /// двустворчатой двери и «страховка» для трёх одиночных.
    ///
    /// Меню: <c>ProjectC → World → PRIMUM → Двери: собрать двустворчатый префаб</c>.
    ///
    /// Контракт (как у одиночных): пивот = низ по центру проёма, лицевая сторона +Z,
    /// слой PRIMUM (8), <b>static НЕ ставить</b> (иначе объект отрывается при ребейзе мира, F8),
    /// полотно — отдельный узел с пивотом на петлях, у корня BoxCollider-триггер зоны взаимодействия.
    /// Нормали: каждый бокс строится наружу, знаковый объём меша проверяется (см. постмортем выворотки).
    /// </summary>
    public static class DoorPrefabBuilder
    {
        private const string PrefabDir = "Assets/_Project/Prefabs/City/Doors";
        private const string MeshDir = PrefabDir + "/Meshes";
        private const string MatDir = "Assets/_Project/Materials/Primum/Doors";
        private const int DoorLayer = 8; // PRIMUM

        // ---------------------------------------------------------------- габариты двустворчатой

        /// <summary>Общая ширина по порогу (сил), м.</summary>
        public const float DoubleSillW = 1.36f;
        /// <summary>Ширина проёма по раме (= триггер взаимодействия), м.</summary>
        public const float DoubleFrameW = 1.30f;
        /// <summary>Общая высота, м.</summary>
        public const float DoubleH = 2.50f;
        /// <summary>Толщина рамы по Z, м.</summary>
        private const float FrameDepth = 0.16f;
        /// <summary>Глубина порога по Z, м.</summary>
        private const float SillDepth = 0.22f;
        /// <summary>Ширина стойки рамы, м.</summary>
        private const float Jamb = 0.09f;
        /// <summary>Высота перекладины, м.</summary>
        private const float Lintel = 0.10f;
        /// <summary>Зазор полотна по низу, м.</summary>
        private const float LeafGap = 0.04f;
        /// <summary>Толщина полотна, м.</summary>
        private const float LeafThick = 0.072f;

        /// <summary>Ширина одной створки двустворчатой, м (0.56).</summary>
        public static float LeafW => (DoubleFrameW - 2f * Jamb) * 0.5f;
        /// <summary>Высота полотна двустворчатой, м (2.36).</summary>
        public static float LeafH => DoubleH - Lintel - LeafGap;
        /// <summary>Отступ пивота створки от центра (0.56).</summary>
        public static float LeafPivotX => DoubleFrameW * 0.5f - Jamb;

        private static string _product = "PM_DOOR";

        // ---------------------------------------------------------------- меню

        [MenuItem("ProjectC/World/PRIMUM/Двери: собрать двустворчатый префаб", false, 320)]
        public static void MenuBuildDouble()
        {
            string path = BuildDouble();
            Debug.Log("[PRIMUM/Doors] Двустворчатый префаб собран: " + path + "\n" + DescribeDouble());
        }

        [MenuItem("ProjectC/World/PRIMUM/Двери: пересобрать 3 одиночных (восстановление)", false, 321)]
        public static void MenuBuildSingles()
        {
            // Аварийное восстановление: перезаписывает существующие префабы.
            // Геометрия та же по классам/габаритам; рисунок филёнок может отличаться от исходного.
            BuildSingle("PLANK_A", 0.96f, 0.90f, 2.10f, "PM_DOOR_WOOD_DARK", 0.09f, 0.10f, false);
            BuildSingle("PANEL_B", 1.06f, 1.00f, 2.15f, "PM_DOOR_WOOD_DARK", 0.10f, 0.12f, true);
            BuildSingle("ARCH_C", 1.20f, 1.10f, 2.20f, "PM_DOOR_STONE", 0.10f, 0.12f, true, 2.36f);
            Debug.Log("[PRIMUM/Doors] Три одиночных префаба пересобраны.");
        }

        // ---------------------------------------------------------------- двустворчатая

        /// <summary>Собирает PM_DOOR_DOUBLE_A.prefab. Возвращает путь ассета.</summary>
        public static string BuildDouble()
        {
            _product = "PM_DOOR_DOUBLE_A";
            var root = new GameObject(_product);
            root.layer = DoorLayer;

            Material woodDark = LoadMat("PM_DOOR_WOOD_DARK");
            Material woodMid = LoadMat("PM_DOOR_WOOD_MID");
            Material stone = LoadMat("PM_DOOR_STONE");
            Material iron = LoadMat("PM_DOOR_IRON");
            Material bronze = LoadMat("PM_DOOR_BRONZE");

            float halfFrame = DoubleFrameW * 0.5f;
            float jambH = DoubleH - Lintel;

            // --- рама: две стойки + перекладина
            var frame = new Boxes();
            frame.Add(new Vector3(-halfFrame + Jamb * 0.5f, jambH * 0.5f, 0f), new Vector3(Jamb, jambH, FrameDepth));
            frame.Add(new Vector3(halfFrame - Jamb * 0.5f, jambH * 0.5f, 0f), new Vector3(Jamb, jambH, FrameDepth));
            frame.Add(new Vector3(0f, DoubleH - Lintel * 0.5f, 0f), new Vector3(DoubleFrameW, Lintel, FrameDepth));
            AddMeshChild(root.transform, "Frame", frame, new[] { woodDark });

            // --- порог
            var sill = new Boxes();
            sill.Add(new Vector3(0f, 0.02f, 0f), new Vector3(DoubleSillW, 0.04f, SillDepth));
            AddMeshChild(root.transform, "Sill", sill, new[] { stone });

            // --- створки: пивот на петлях (левая — левый край, правая — правый)
            BuildLeaf(root.transform, "LeafL", -LeafPivotX, false, woodDark, woodMid, iron, bronze);
            BuildLeaf(root.transform, "LeafR", LeafPivotX, true, woodDark, woodMid, iron, bronze);

            // --- триггер зоны взаимодействия на корне
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, DoubleH * 0.5f, 0f);
            trigger.size = new Vector3(DoubleFrameW, DoubleH, 0.20f);

            // --- DoorController: двустворчатая распашная
            var dc = root.AddComponent<DoorController>();
            var so = new SerializedObject(dc);
            so.FindProperty("doorType").enumValueIndex = (int)DoorController.DoorType.HingedDouble;
            so.FindProperty("startOpen").boolValue = false;
            so.FindProperty("openDuration").floatValue = 1.2f;
            so.FindProperty("autoCloseDelay").floatValue = 0f;
            so.FindProperty("leftPivot").objectReferenceValue = root.transform.Find("LeafL");
            so.FindProperty("leftHingePanel").objectReferenceValue = root.transform.Find("LeafL/LeafL_Wood");
            so.FindProperty("leftHingeSide").enumValueIndex = (int)DoorController.HingeSide.Left;
            so.FindProperty("rightPivot").objectReferenceValue = root.transform.Find("LeafR");
            so.FindProperty("rightHingePanel").objectReferenceValue = root.transform.Find("LeafR/LeafR_Wood");
            so.FindProperty("rightHingeSide").enumValueIndex = (int)DoorController.HingeSide.Right;
            so.FindProperty("doubleOpenAngle").floatValue = 90f;
            so.FindProperty("invertDoubleSwing").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetNotStaticRecursive(root);

            string path = PrefabDir + "/" + _product + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return path;
        }

        // ---------------------------------------------------------------- дворцовая двустворчатая (Э2, приоритет 1)

        [MenuItem("ProjectC/World/PRIMUM/Двери: собрать дворцовую двустворчатую A (1.50x2.31)", false, 322)]
        public static void MenuBuildPalaceA() { Debug.Log("[PRIMUM/Doors] " + BuildPalaceDouble("A", 1.50f, 2.31f)); }

        [MenuItem("ProjectC/World/PRIMUM/Двери: собрать дворцовую двустворчатую B (2.14x2.82)", false, 323)]
        public static void MenuBuildPalaceB() { Debug.Log("[PRIMUM/Doors] " + BuildPalaceDouble("B", 2.14f, 2.82f)); }

        [MenuItem("ProjectC/World/PRIMUM/Двери: собрать дворцовую двустворчатую C (2.78x2.86)", false, 324)]
        public static void MenuBuildPalaceC() { Debug.Log("[PRIMUM/Doors] " + BuildPalaceDouble("C", 2.78f, 2.86f)); }

        /// <summary>Собирает PM_DOOR_PALACE_DOUBLE_*.prefab. Возвращает путь ассета.</summary>
        public static string BuildPalaceDouble(string id, float sillW, float totalH)
        {
            _product = "PM_DOOR_PALACE_DOUBLE_" + id;
            var root = new GameObject(_product);
            root.layer = DoorLayer;

            Material stone = LoadMat("PM_DOOR_STONE");
            Material woodDark = LoadMat("PM_DOOR_WOOD_DARK");
            Material bronze = LoadMat("PM_DOOR_BRONZE");
            Material iron = LoadMat("PM_DOOR_IRON");

            float jamb = Mathf.Max(0.09f, sillW * 0.06f);
            float lintel = Mathf.Max(0.10f, totalH * 0.04f);
            float frameW = sillW - 0.06f;
            float halfFrame = frameW * 0.5f;
            float jambH = totalH - lintel;

            var frame = new Boxes();
            frame.Add(new Vector3(-halfFrame + jamb * 0.5f, jambH * 0.5f, 0f), new Vector3(jamb, jambH, FrameDepth));
            frame.Add(new Vector3(halfFrame - jamb * 0.5f, jambH * 0.5f, 0f), new Vector3(jamb, jambH, FrameDepth));
            frame.Add(new Vector3(0f, totalH - lintel * 0.5f, 0f), new Vector3(frameW, lintel, FrameDepth));
            // декоративные колонны у стоек
            frame.Add(new Vector3(-halfFrame + jamb * 0.5f, totalH * 0.5f, FrameDepth * 0.5f + 0.02f), new Vector3(jamb * 1.4f, totalH * 0.6f, 0.04f));
            frame.Add(new Vector3(halfFrame - jamb * 0.5f, totalH * 0.5f, FrameDepth * 0.5f + 0.02f), new Vector3(jamb * 1.4f, totalH * 0.6f, 0.04f));
            AddMeshChild(root.transform, "Frame", frame, new[] { stone });

            var sill = new Boxes();
            sill.Add(new Vector3(0f, 0.02f, 0f), new Vector3(sillW, 0.04f, SillDepth));
            AddMeshChild(root.transform, "Sill", sill, new[] { stone });

            float leafW = (frameW - 2f * jamb) * 0.5f;
            float leafH = totalH - lintel - LeafGap;
            float leafPivotX = frameW * 0.5f - jamb;
            BuildPalaceLeaf(root.transform, "LeafL", -leafPivotX, false, leafW, leafH, woodDark, bronze, iron);
            BuildPalaceLeaf(root.transform, "LeafR", leafPivotX, true, leafW, leafH, woodDark, bronze, iron);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, totalH * 0.5f, 0f);
            trigger.size = new Vector3(frameW, totalH, 0.20f);

            var dc = root.AddComponent<DoorController>();
            var so = new SerializedObject(dc);
            so.FindProperty("doorType").enumValueIndex = (int)DoorController.DoorType.HingedDouble;
            so.FindProperty("startOpen").boolValue = false;
            so.FindProperty("openDuration").floatValue = 1.2f;
            so.FindProperty("autoCloseDelay").floatValue = 0f;
            so.FindProperty("leftPivot").objectReferenceValue = root.transform.Find("LeafL");
            so.FindProperty("leftHingePanel").objectReferenceValue = root.transform.Find("LeafL/LeafL_Wood");
            so.FindProperty("leftHingeSide").enumValueIndex = (int)DoorController.HingeSide.Left;
            so.FindProperty("rightPivot").objectReferenceValue = root.transform.Find("LeafR");
            so.FindProperty("rightHingePanel").objectReferenceValue = root.transform.Find("LeafR/LeafR_Wood");
            so.FindProperty("rightHingeSide").enumValueIndex = (int)DoorController.HingeSide.Right;
            so.FindProperty("doubleOpenAngle").floatValue = 90f;
            so.FindProperty("invertDoubleSwing").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetNotStaticRecursive(root);

            string path = PrefabDir + "/" + _product + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return path;
        }

        private static void BuildPalaceLeaf(Transform parent, string nodeName, float pivotX, bool mirroredRight,
                                           float leafW, float leafH, Material wood, Material bronze, Material iron)
        {
            var leaf = new GameObject(nodeName);
            leaf.layer = DoorLayer;
            leaf.transform.SetParent(parent, false);
            leaf.transform.localPosition = new Vector3(pivotX, LeafGap, 0f);

            float x0 = mirroredRight ? -leafW : 0f;
            float x1 = mirroredRight ? 0f : leafW;
            float cx = (x0 + x1) * 0.5f;

            var woodBoxes = new Boxes();
            woodBoxes.Add(new Vector3(cx, leafH * 0.5f, 0f), new Vector3(leafW, leafH, LeafThick));
            AddMeshChild(leaf.transform, nodeName + "_Wood", woodBoxes, new[] { wood });

            var panels = new Boxes();
            float panelW = leafW - 0.16f;
            foreach (float py in new[] { 0.62f, 1.72f })
            {
                if (py > leafH * 0.9f) continue;
                panels.Add(new Vector3(cx, py, LeafThick * 0.5f + 0.008f), new Vector3(panelW, 0.40f, 0.016f));
                panels.Add(new Vector3(cx, py, -LeafThick * 0.5f - 0.008f), new Vector3(panelW, 0.40f, 0.016f));
            }
            AddMeshChild(leaf.transform, nodeName + "_Panels", panels, new[] { bronze });

            var ir = new Boxes();
            float hingeX = mirroredRight ? x1 - 0.03f : x0 + 0.03f;
            foreach (float hy in new[] { 0.35f, leafH * 0.5f, leafH - 0.35f })
                ir.Add(new Vector3(hingeX, hy, 0f), new Vector3(0.12f, 0.14f, 0.035f));
            AddMeshChild(leaf.transform, nodeName + "_Iron", ir, new[] { iron });

            var br = new Boxes();
            float handleX = mirroredRight ? x0 + 0.06f : x1 - 0.06f;
            float leverX = handleX + (mirroredRight ? 0.07f : -0.07f);
            br.Add(new Vector3(handleX, leafH * 0.47f, 0.05f), new Vector3(0.05f, 0.18f, 0.02f));
            br.Add(new Vector3(leverX, leafH * 0.47f, 0.06f), new Vector3(0.14f, 0.045f, 0.045f));
            AddMeshChild(leaf.transform, nodeName + "_Bronze", br, new[] { bronze });

            var col = leaf.AddComponent<BoxCollider>();
            col.isTrigger = false;
            col.center = new Vector3(cx, leafH * 0.5f, 0.012f);
            col.size = new Vector3(leafW, leafH - 0.06f, 0.07f);
        }

        // ---------------------------------------------------------------- ворота (Э2, приоритет 3)

        [MenuItem("ProjectC/World/PRIMUM/Двери: собрать ворота A (2.89x2.78)", false, 325)]
        public static void MenuBuildGateA() { Debug.Log("[PRIMUM/Doors] " + BuildGateSingle("A", 2.89f, 2.78f)); }

        [MenuItem("ProjectC/World/PRIMUM/Двери: собрать ворота B (3.53x3.57)", false, 326)]
        public static void MenuBuildGateB() { Debug.Log("[PRIMUM/Doors] " + BuildGateSingle("B", 3.53f, 3.57f)); }

        /// <summary>Собирает PM_DOOR_GATE_*.prefab (одностворчатые ворота). Возвращает путь.</summary>
        public static string BuildGateSingle(string id, float sillW, float totalH)
        {
            _product = "PM_DOOR_GATE_" + id;
            var root = new GameObject(_product);
            root.layer = DoorLayer;

            Material iron = LoadMat("PM_DOOR_IRON");
            Material stone = LoadMat("PM_DOOR_STONE");
            Material bronze = LoadMat("PM_DOOR_BRONZE");

            float jamb = Mathf.Max(0.10f, sillW * 0.04f);
            float lintel = Mathf.Max(0.12f, totalH * 0.05f);
            float frameW = sillW - 0.06f;
            float halfFrame = frameW * 0.5f;
            float jambH = totalH - lintel;

            var frame = new Boxes();
            frame.Add(new Vector3(-halfFrame + jamb * 0.5f, jambH * 0.5f, 0f), new Vector3(jamb, jambH, FrameDepth));
            frame.Add(new Vector3(halfFrame - jamb * 0.5f, jambH * 0.5f, 0f), new Vector3(jamb, jambH, FrameDepth));
            frame.Add(new Vector3(0f, totalH - lintel * 0.5f, 0f), new Vector3(frameW, lintel, FrameDepth));
            AddMeshChild(root.transform, "Frame", frame, new[] { stone });

            var panel = new Boxes();
            float leafW = frameW - 2f * jamb;
            float leafH = totalH - lintel - LeafGap;
            panel.Add(new Vector3(0f, leafH * 0.5f + LeafGap, 0f), new Vector3(leafW, leafH, LeafThick * 2f));
            // рёбра жёсткости
            for (float py = 0.4f; py < leafH; py += 0.6f)
                panel.Add(new Vector3(0f, py + LeafGap, LeafThick), new Vector3(leafW * 0.9f, 0.06f, 0.04f));
            AddMeshChild(root.transform, "Leaf", panel, new[] { iron });

            var sill = new Boxes();
            sill.Add(new Vector3(0f, 0.02f, 0f), new Vector3(sillW, 0.04f, SillDepth));
            AddMeshChild(root.transform, "Sill", sill, new[] { stone });

            var leaf = new GameObject("Leaf");
            leaf.layer = DoorLayer;
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.localPosition = new Vector3(-leafW * 0.5f, LeafGap, 0f);

            // блокирующий коллайдер
            var leafCol = leaf.AddComponent<BoxCollider>();
            leafCol.isTrigger = false;
            leafCol.center = new Vector3(leafW * 0.5f, leafH * 0.5f, 0.012f);
            leafCol.size = new Vector3(leafW, leafH, 0.10f);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, totalH * 0.5f, 0f);
            trigger.size = new Vector3(frameW, totalH, 0.20f);

            var dc = root.AddComponent<DoorController>();
            var so = new SerializedObject(dc);
            so.FindProperty("doorType").enumValueIndex = (int)DoorController.DoorType.HingedSingle;
            so.FindProperty("startOpen").boolValue = false;
            so.FindProperty("openDuration").floatValue = 1.5f;
            so.FindProperty("autoCloseDelay").floatValue = 0f;
            so.FindProperty("hingePivot").objectReferenceValue = leaf.transform;
            so.FindProperty("hingePanel").objectReferenceValue = leaf.transform.Find("Leaf");
            so.FindProperty("hingeSide").enumValueIndex = (int)DoorController.HingeSide.Left;
            so.FindProperty("openAngle").floatValue = 90f;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetNotStaticRecursive(root);

            string path = PrefabDir + "/" + _product + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return path;
        }

        // ---------------------------------------------------------------- узкие двери (Э2, приоритет 4)

        [MenuItem("ProjectC/World/PRIMUM/Двери: собрать узкую дверь (0.56x2.40)", false, 327)]
        public static void MenuBuildNarrow() { Debug.Log("[PRIMUM/Doors] " + BuildNarrowSingle("A", 0.56f, 2.40f)); }

        public static string BuildNarrowSingle(string id, float sillW, float totalH)
        {
            return BuildSingle("NARROW_" + id, sillW, sillW - 0.06f, totalH, "PM_DOOR_WOOD_DARK", 0.06f, 0.10f, false);
        }

        // ---------------------------------------------------------------- широкие двери (Э2, приоритет 4)

        [MenuItem("ProjectC/World/PRIMUM/Двери: собрать широкую дверь (2.00x2.20)", false, 328)]
        public static void MenuBuildWide() { Debug.Log("[PRIMUM/Doors] " + BuildWideSingle("A", 2.00f, 2.20f)); }

        public static string BuildWideSingle(string id, float sillW, float totalH)
        {
            return BuildSingle("WIDE_" + id, sillW, sillW - 0.08f, totalH, "PM_DOOR_WOOD_DARK", 0.08f, 0.12f, true);
        }

        /// <summary>Одна створка: полотно + филёнки + петли + ручка, блокирующий коллайдер.</summary>
        private static void BuildLeaf(Transform parent, string nodeName, float pivotX, bool mirroredRight,
                                      Material woodDark, Material woodMid, Material iron, Material bronze)
        {
            var leaf = new GameObject(nodeName);
            leaf.layer = DoorLayer;
            leaf.transform.SetParent(parent, false);
            leaf.transform.localPosition = new Vector3(pivotX, LeafGap, 0f);

            float w = LeafW;
            float h = LeafH;
            // в осях створки: x = 0..w (петля слева) либо -w..0 (петля справа)
            float x0 = mirroredRight ? -w : 0f;
            float x1 = mirroredRight ? 0f : w;
            float cx = (x0 + x1) * 0.5f;

            // --- полотно (сплошная плита)
            var wood = new Boxes();
            wood.Add(new Vector3(cx, h * 0.5f, 0f), new Vector3(w, h, LeafThick));
            AddMeshChild(leaf.transform, nodeName + "_Wood", wood, new[] { woodDark });

            // --- филёнки с двух сторон (второй материал)
            var panels = new Boxes();
            float panelW = w - 0.16f;
            float panelH = 0.82f;
            foreach (float py in new[] { 0.62f, 1.72f })
            {
                panels.Add(new Vector3(cx, py, LeafThick * 0.5f + 0.008f), new Vector3(panelW, panelH, 0.016f));
                panels.Add(new Vector3(cx, py, -LeafThick * 0.5f - 0.008f), new Vector3(panelW, panelH, 0.016f));
            }
            AddMeshChild(leaf.transform, nodeName + "_Panels", panels, new[] { woodMid });

            // --- петли у кромки петель (3 шт.)
            var ir = new Boxes();
            float hingeX = mirroredRight ? x1 - 0.03f : x0 + 0.03f;
            foreach (float hy in new[] { 0.35f, h * 0.5f, h - 0.35f })
                ir.Add(new Vector3(hingeX, hy, 0f), new Vector3(0.12f, 0.14f, 0.035f));
            AddMeshChild(leaf.transform, nodeName + "_Iron", ir, new[] { iron });

            // --- ручка у притвора (свободная кромка)
            var br = new Boxes();
            float handleX = mirroredRight ? x0 + 0.06f : x1 - 0.06f;
            float leverX = handleX + (mirroredRight ? 0.07f : -0.07f);
            br.Add(new Vector3(handleX, 1.10f, 0.05f), new Vector3(0.05f, 0.18f, 0.02f));
            br.Add(new Vector3(leverX, 1.10f, 0.06f), new Vector3(0.14f, 0.045f, 0.045f));
            AddMeshChild(leaf.transform, nodeName + "_Bronze", br, new[] { bronze });

            // --- блокирующий объём створки (едет со створкой при открытии)
            var col = leaf.AddComponent<BoxCollider>();
            col.isTrigger = false;
            col.center = new Vector3(cx, h * 0.5f, 0.012f);
            col.size = new Vector3(w - 0.04f, h - 0.06f, 0.07f);
        }

        // ---------------------------------------------------------------- одиночная (восстановление)

        /// <summary>Собирает одиночный префаб по габаритам (аварийное восстановление).</summary>
        public static string BuildSingle(string id, float sillW, float clearW, float h,
                                         string frameMat, float jamb, float lintel, bool paneled,
                                         float archTop = 0f)
        {
            _product = "PM_DOOR_SINGLE_" + id;
            var root = new GameObject(_product);
            root.layer = DoorLayer;
            Material frameMaterial = LoadMat(frameMat);
            Material wood = LoadMat(paneled ? "PM_DOOR_WOOD_MID" : "PM_DOOR_WOOD_DARK");
            Material stone = LoadMat("PM_DOOR_STONE");
            Material iron = LoadMat("PM_DOOR_IRON");
            Material bronze = LoadMat("PM_DOOR_BRONZE");

            float jambH = h - lintel;
            var frame = new Boxes();
            frame.Add(new Vector3(-clearW * 0.5f + jamb * 0.5f, jambH * 0.5f, 0f), new Vector3(jamb, jambH, FrameDepth));
            frame.Add(new Vector3(clearW * 0.5f - jamb * 0.5f, jambH * 0.5f, 0f), new Vector3(jamb, jambH, FrameDepth));
            frame.Add(new Vector3(0f, h - lintel * 0.5f, 0f), new Vector3(clearW, lintel, FrameDepth));
            AddMeshChild(root.transform, "Frame", frame, new[] { frameMaterial });

            var sill = new Boxes();
            sill.Add(new Vector3(0f, 0.02f, 0f), new Vector3(sillW, 0.04f, SillDepth));
            AddMeshChild(root.transform, "Sill", sill, new[] { stone });

            float leafW = clearW - 2f * jamb - 0.06f;
            float leafH = h - lintel - LeafGap;
            var leaf = new GameObject("Leaf");
            leaf.layer = DoorLayer;
            leaf.transform.SetParent(root.transform, false);
            leaf.transform.localPosition = new Vector3(-leafW * 0.5f, LeafGap, 0f);

            var woodBoxes = new Boxes();
            woodBoxes.Add(new Vector3(leafW * 0.5f, leafH * 0.5f, 0f), new Vector3(leafW, leafH, LeafThick));
            AddMeshChild(leaf.transform, "LeafWood", woodBoxes, new[] { wood });

            if (paneled)
            {
                var panels = new Boxes();
                float pw = leafW - 0.14f;
                foreach (float py in new[] { 0.62f, 1.20f, 1.78f })
                    panels.Add(new Vector3(leafW * 0.5f, py, LeafThick * 0.5f + 0.008f), new Vector3(pw, 0.34f, 0.016f));
                AddMeshChild(leaf.transform, "LeafPanels", panels, new[] { stone });
            }

            var irBoxes = new Boxes();
            foreach (float hy in new[] { 0.35f, leafH * 0.5f, leafH - 0.35f })
                irBoxes.Add(new Vector3(0.03f, hy, 0f), new Vector3(0.12f, 0.14f, 0.035f));
            AddMeshChild(leaf.transform, "LeafIron", irBoxes, new[] { iron });

            var brBoxes = new Boxes();
            brBoxes.Add(new Vector3(leafW - 0.06f, leafH * 0.47f, 0.05f), new Vector3(0.05f, 0.18f, 0.02f));
            brBoxes.Add(new Vector3(leafW - 0.13f, leafH * 0.47f, 0.06f), new Vector3(0.14f, 0.045f, 0.045f));
            AddMeshChild(leaf.transform, "LeafBronze", brBoxes, new[] { bronze });

            var leafCol = leaf.AddComponent<BoxCollider>();
            leafCol.isTrigger = false;
            leafCol.center = new Vector3(leafW * 0.5f, leafH * 0.5f, 0.012f);
            leafCol.size = new Vector3(leafW, leafH - 0.02f, 0.07f);

            if (archTop > 0f)
            {
                var arch = new Boxes();
                arch.Add(new Vector3(0f, archTop - 0.07f, 0f), new Vector3(sillW, 0.14f, 0.20f));
                AddMeshChild(root.transform, "ArchTrim", arch, new[] { frameMaterial });
            }

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, h * 0.5f, 0f);
            trigger.size = new Vector3(clearW, h, 0.20f);

            var dc = root.AddComponent<DoorController>();
            var so = new SerializedObject(dc);
            so.FindProperty("doorType").enumValueIndex = (int)DoorController.DoorType.HingedSingle;
            so.FindProperty("startOpen").boolValue = false;
            so.FindProperty("openDuration").floatValue = 1.2f;
            so.FindProperty("autoCloseDelay").floatValue = 0f;
            so.FindProperty("hingePivot").objectReferenceValue = leaf.transform;
            so.FindProperty("hingePanel").objectReferenceValue = leaf.transform.Find("LeafWood");
            so.FindProperty("hingeSide").enumValueIndex = (int)DoorController.HingeSide.Left;
            so.FindProperty("openAngle").floatValue = 90f;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetNotStaticRecursive(root);

            string path = PrefabDir + "/" + _product + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return path;
        }

        // ---------------------------------------------------------------- вспомогательное

        /// <summary>Боксы → меш-ассет → узел с MeshFilter/MeshRenderer.</summary>
        private static void AddMeshChild(Transform parent, string nodeName, Boxes boxes, Material[] mats)
        {
            if (boxes.V.Count == 0) return;

            string meshName = _product + "_" + nodeName;
            var go = new GameObject(nodeName);
            go.layer = DoorLayer;
            go.transform.SetParent(parent, false);

            Mesh mesh = boxes.ToMesh(meshName);
            float volume = SignedVolume(mesh);
            if (volume <= 0f)
                Debug.LogError("[PRIMUM/Doors] " + meshName + ": знаковый объём " +
                               volume.ToString("F6", CultureInfo.InvariantCulture) + " <= 0 — вывернутые нормали!");

            AssetDatabase.CreateAsset(mesh, MeshDir + "/" + meshName + ".asset");

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
        }

        /// <summary>Карта материалов дверей → процедурные материалы проекта (Э3.1: двери без `URP/Lit`).</summary>
        private static readonly Dictionary<string, string> MatMap = new Dictionary<string, string>
        {
            { "PM_DOOR_WOOD_DARK", "Assets/_Project/Materials/Wood/M_PC_Wood_Dark.mat" },
            { "PM_DOOR_WOOD_MID",  "Assets/_Project/Materials/Wood/M_PC_Wood.mat" },
            { "PM_DOOR_STONE",     "Assets/_Project/Materials/Concrete/M_PC_Concrete.mat" },
            { "PM_DOOR_IRON",      "Assets/_Project/Materials/Iron/M_PC_IronRust.mat" },
            { "PM_DOOR_BRONZE",    "Assets/_Project/Materials/Bronze/M_PC_Bronze.mat" },
        };

        private static Material LoadMat(string name)
        {
            string mapped;
            var path = MatMap.TryGetValue(name, out mapped) ? mapped : MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) Debug.LogError("[PRIMUM/Doors] Не найден материал: " + name);
            return m;
        }

        private static void SetNotStaticRecursive(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)0);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)0);
        }

        /// <summary>Знаковый объём замкнутого меша: &gt; 0 = нормали наружу (см. постмортем выворотки).</summary>
        public static float SignedVolume(Mesh m)
        {
            Vector3[] v = m.vertices;
            int[] t = m.triangles;
            double sum = 0.0;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                sum += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
            }
            return (float)sum;
        }

        /// <summary>Простейший конструктор мешей из ориентированных наружу боксов.</summary>
        private class Boxes
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<int> T = new List<int>();

            public void Add(Vector3 center, Vector3 size)
            {
                Vector3 h = size * 0.5f;
                float x0 = center.x - h.x, x1 = center.x + h.x;
                float y0 = center.y - h.y, y1 = center.y + h.y;
                float z0 = center.z - h.z, z1 = center.z + h.z;

                Quad(center, new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0));
                Quad(center, new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1));
                Quad(center, new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0));
                Quad(center, new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1));
                Quad(center, new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1));
                Quad(center, new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0));
            }

            /// <summary>Квад наружу: если нормаль смотрит внутрь бокса — порядок разворачивается.</summary>
            private void Quad(Vector3 boxCenter, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, a - boxCenter) < 0f)
                {
                    Vector3 tmp = b; b = d; d = tmp;
                }
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
                T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(V);
                m.SetTriangles(T, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Сводка по собранному двустворчатому префабу (для отчёта/проверки).</summary>
        public static string DescribeDouble()
        {
            var sb = new StringBuilder();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/PM_DOOR_DOUBLE_A.prefab");
            if (go == null) return "PM_DOOR_DOUBLE_A.prefab не найден";
            sb.Append("PM_DOOR_DOUBLE_A: общий габарит " + DoubleSillW.ToString("F2") + " x " + DoubleH.ToString("F2") +
                      " x " + SillDepth.ToString("F2") + ", створка " + LeafW.ToString("F2") + " x " + LeafH.ToString("F2"));
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var mf = t.GetComponent<MeshFilter>();
                sb.Append("\n  " + t.name);
                if (mf != null && mf.sharedMesh != null)
                    sb.Append("  verts=" + mf.sharedMesh.vertexCount + " vol=" + SignedVolume(mf.sharedMesh).ToString("F5"));
                var bc = t.GetComponent<BoxCollider>();
                if (bc != null) sb.Append("  col=" + bc.size.ToString("F3") + (bc.isTrigger ? "(trig)" : ""));
                if (t.localPosition != Vector3.zero) sb.Append("  pos=" + t.localPosition.ToString("F3"));
            }
            return sb.ToString();
        }
    }
}
