#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectC.Player;
using ProjectC.Ship;
using ProjectC.Ship.Combat;
using ProjectC.PeacefulShip.Stations;

namespace ProjectC.Editor
{
    /// <summary>
    /// ShipSummaryWindow — сводный редактор всех кораблей проекта.
    /// Сканирует префабы в Assets/_Project/Prefabs/Ships/, показывает таблицу
    /// с ключевыми параметрами, позволяет редактировать выбранный корабль
    /// и применять массовые изменения к группе кораблей.
    ///
    /// Документация: docs/world/PLACEMENT_SCRIPTS/Ships/SHIP_SUMMARY_TOOL.md
    /// </summary>
    public class ShipSummaryWindow : EditorWindow
    {
        // ── Константы ──
        private const string ShipsFolder = "Assets/_Project/Prefabs/Ships";
        private const string MenuPath = "Tools/Project C/Ship Summary";
        private const string DontDeletePrefix = "DONT-DELETE";
        private const string ShipLightRootName = "Ship_Light_root";

        // ── Данные ──
        private List<ShipSummaryEntry> _ships = new List<ShipSummaryEntry>();
        private Vector2 _tableScroll;
        private Vector2 _detailScroll;
        private int _selectedIndex = -1;
        private HashSet<int> _multiSelected = new HashSet<int>();
        private string _filterText = "";

        // ── Детальная панель: foldout states ──
        private bool _foldFlight = true;
        private bool _foldSmoothing;
        private bool _foldPhysics = true;
        private bool _foldStabilize;
        private bool _foldWind;
        private bool _foldCargo;
        private bool _foldFuel = true;
        private bool _foldNpc;
        private bool _foldIdentity = true;

        // ── Batch edit: значения ──
        private string _batchThrust = "";
        private string _batchMaxSpeed = "";
        private string _batchYaw = "";
        private string _batchPitch = "";
        private string _batchVertical = "";
        private string _batchMassMult = "";
        private string _batchFuelMax = "";
        private string _batchFuelConsumption = "";
        private string _batchPower = "";
        private string _batchWindExposure = "";
        private string _batchLinearDrag = "";
        private string _batchAngularDrag = "";
        private int _batchClassIndex = -1;

        // ── Стиль ──
        private GUIStyle _headerStyle;
        private GUIStyle _rowEvenStyle;
        private GUIStyle _rowOddStyle;
        private GUIStyle _rowSelectedStyle;
        private bool _stylesBuilt;

        // ═══════════════════════════════════════════════════════════════
        // DATA MODEL
        // ═══════════════════════════════════════════════════════════════

        [Serializable]
        public class ShipSummaryEntry
        {
            public string prefabPath;
            public string displayName;        // имя префаба без расширения
            public string customDisplayName;  // _customDisplayName из ShipController

            // ShipController
            public ShipFlightClass flightClass;
            public float thrustForce;
            public float maxSpeed;
            public float yawForce;
            public float pitchForce;
            public float verticalForce;
            public float yawSmoothTime;
            public float pitchSmoothTime;
            public float liftSmoothTime;
            public float thrustSmoothTime;
            public float yawDecayTime;
            public float pitchDecayTime;
            public float antiGravity;
            public float massMultiplier;
            public float massLight;
            public float massMedium;
            public float massHeavy;
            public float massHeavyII;
            public float linearDrag;
            public float angularDrag;
            public float pitchStabForce;
            public float rollStabForce;
            public float maxPitchAngle;
            public bool autoStabilize;
            public float windInfluence;
            public float windExposure;
            public float windDecayTime;
            public bool globalWindEnabled;
            public float globalWindForceScale;
            public float globalWindVerticalFactor;
            public int baseMaxCargoSlots;
            public float baseMaxCargoWeight;
            public float baseMaxCargoVolume;
            public float baseCargoPenaltyFactor;

            // ShipFuelSystem
            public float currentFuel;
            public float maxFuel;
            public float fuelConsumptionRate;
            public float fuelRegenRate;
            public float startEngineConsumption;
            public float idleConsumptionRate;
            public float atmosphericRefuelRate;
            public float thrustPenaltyDuringRefuel;
            public float speedPenaltyDuringRefuel;

            // ShipModuleManager
            public int availablePower;

            // ShipHull — HP читается из ShipDamageConfig (SO)
            public int hullHP;

            // NpcShipController
            public float npcThrustMult;
            public float npcYawMult;

            // NpcProximityZone
            public float awarenessRadius;
            public float avoidanceRadius;

            // Rigidbody
            public float rbMass;
            public float rbDrag;
            public float rbAngularDrag;
        }

        // ═══════════════════════════════════════════════════════════════
        // WINDOW LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        [MenuItem(MenuPath, false, 201)]
        public static void ShowWindow()
        {
            var window = GetWindow<ShipSummaryWindow>("Ship Summary");
            window.minSize = new Vector2(1100, 600);
            window.Show();
            window.Rescan();
        }

        private void OnEnable()
        {
            if (_ships.Count == 0)
                Rescan();
        }

        // ═══════════════════════════════════════════════════════════════
        // SCANNING
        // ═══════════════════════════════════════════════════════════════

        private void Rescan()
        {
            _ships.Clear();
            _selectedIndex = -1;
            _multiSelected.Clear();

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ShipsFolder });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Пропускаем не-корневые ассеты (sub-assets внутри папок)
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(fileName))
                    continue;
                // Пропускаем служебные
                if (fileName.StartsWith(DontDeletePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (fileName.Equals(ShipLightRootName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Проверяем что это именно корабль (есть ShipController)
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;
                var sc = prefab.GetComponent<ShipController>();
                if (sc == null)
                    continue;

                var entry = ReadShipFromPrefab(path, fileName);
                if (entry != null)
                    _ships.Add(entry);
            }

            // Сортируем по имени
            _ships = _ships.OrderBy(s => s.displayName).ToList();

            Debug.Log($"[ShipSummary] Scanned {_ships.Count} ships from {ShipsFolder}");
        }

        private ShipSummaryEntry ReadShipFromPrefab(string path, string displayName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;

            var entry = new ShipSummaryEntry
            {
                prefabPath = path,
                displayName = displayName
            };

            using (var so = new SerializedObject(prefab))
            {
                // ── ShipController ──
                var sc = prefab.GetComponent<ShipController>();
                if (sc != null)
                {
                    entry.customDisplayName = sc.CustomDisplayName ?? "";
                    entry.flightClass = sc.ShipFlightClass;

                    var scSo = new SerializedObject(sc);
                    ReadFloat(scSo, "thrustForce",           ref entry.thrustForce);
                    ReadFloat(scSo, "maxSpeed",              ref entry.maxSpeed);
                    ReadFloat(scSo, "yawForce",              ref entry.yawForce);
                    ReadFloat(scSo, "pitchForce",            ref entry.pitchForce);
                    ReadFloat(scSo, "verticalForce",         ref entry.verticalForce);
                    ReadFloat(scSo, "yawSmoothTime",         ref entry.yawSmoothTime);
                    ReadFloat(scSo, "pitchSmoothTime",       ref entry.pitchSmoothTime);
                    ReadFloat(scSo, "liftSmoothTime",        ref entry.liftSmoothTime);
                    ReadFloat(scSo, "thrustSmoothTime",      ref entry.thrustSmoothTime);
                    ReadFloat(scSo, "yawDecayTime",          ref entry.yawDecayTime);
                    ReadFloat(scSo, "pitchDecayTime",        ref entry.pitchDecayTime);
                    ReadFloat(scSo, "antiGravity",           ref entry.antiGravity);
                    ReadFloat(scSo, "massMultiplier",        ref entry.massMultiplier);
                    ReadFloat(scSo, "massLight",             ref entry.massLight);
                    ReadFloat(scSo, "massMedium",            ref entry.massMedium);
                    ReadFloat(scSo, "massHeavy",             ref entry.massHeavy);
                    ReadFloat(scSo, "massHeavyII",           ref entry.massHeavyII);
                    ReadFloat(scSo, "linearDrag",            ref entry.linearDrag);
                    ReadFloat(scSo, "angularDrag",           ref entry.angularDrag);
                    ReadFloat(scSo, "pitchStabForce",        ref entry.pitchStabForce);
                    ReadFloat(scSo, "rollStabForce",         ref entry.rollStabForce);
                    ReadFloat(scSo, "maxPitchAngle",         ref entry.maxPitchAngle);
                    ReadBool (scSo, "autoStabilize",         ref entry.autoStabilize);
                    ReadFloat(scSo, "windInfluence",         ref entry.windInfluence);
                    ReadFloat(scSo, "windExposure",          ref entry.windExposure);
                    ReadFloat(scSo, "windDecayTime",         ref entry.windDecayTime);
                    ReadBool (scSo, "_globalWindEnabled",    ref entry.globalWindEnabled);
                    ReadFloat(scSo, "_globalWindForceScale", ref entry.globalWindForceScale);
                    ReadFloat(scSo, "_globalWindVerticalFactor", ref entry.globalWindVerticalFactor);
                    ReadInt  (scSo, "baseMaxCargoSlots",     ref entry.baseMaxCargoSlots);
                    ReadFloat(scSo, "baseMaxCargoWeight",    ref entry.baseMaxCargoWeight);
                    ReadFloat(scSo, "baseMaxCargoVolume",    ref entry.baseMaxCargoVolume);
                    ReadFloat(scSo, "baseCargoPenaltyFactor", ref entry.baseCargoPenaltyFactor);
                    scSo.Dispose();
                }

                // ── ShipFuelSystem ──
                var fuel = prefab.GetComponent<ShipFuelSystem>();
                if (fuel != null)
                {
                    var fSo = new SerializedObject(fuel);
                    ReadFloat(fSo, "currentFuel",               ref entry.currentFuel);
                    ReadFloat(fSo, "maxFuel",                   ref entry.maxFuel);
                    ReadFloat(fSo, "fuelConsumptionRate",       ref entry.fuelConsumptionRate);
                    ReadFloat(fSo, "fuelRegenRate",             ref entry.fuelRegenRate);
                    ReadFloat(fSo, "startEngineConsumption",    ref entry.startEngineConsumption);
                    ReadFloat(fSo, "idleConsumptionRate",       ref entry.idleConsumptionRate);
                    ReadFloat(fSo, "atmosphericRefuelRate",     ref entry.atmosphericRefuelRate);
                    ReadFloat(fSo, "thrustPenaltyDuringRefuel", ref entry.thrustPenaltyDuringRefuel);
                    ReadFloat(fSo, "speedPenaltyDuringRefuel",  ref entry.speedPenaltyDuringRefuel);
                    fSo.Dispose();
                }

                // ── ShipModuleManager ──
                var modMgr = prefab.GetComponent<ShipModuleManager>();
                if (modMgr != null)
                {
                    var mSo = new SerializedObject(modMgr);
                    ReadInt(mSo, "availablePower", ref entry.availablePower);
                    mSo.Dispose();
                }

                // ── ShipHull — HP из конфига (GetMaxHull по flightClass) ──
                var hull = prefab.GetComponent<ShipHull>();
                if (hull != null)
                {
                    var config = hull.Config;
                    if (config != null)
                        entry.hullHP = config.GetMaxHull(entry.flightClass);
                }

                // ── NpcShipController ──
                var npcCtrl = prefab.GetComponent<NpcShipController>();
                if (npcCtrl != null)
                {
                    var nSo = new SerializedObject(npcCtrl);
                    ReadFloat(nSo, "npcThrustMult", ref entry.npcThrustMult);
                    ReadFloat(nSo, "npcYawMult",    ref entry.npcYawMult);
                    nSo.Dispose();
                }

                // ── NpcProximityZone ──
                var prox = prefab.GetComponent<NpcProximityZone>();
                if (prox != null)
                {
                    var pSo = new SerializedObject(prox);
                    ReadFloat(pSo, "awarenessRadius", ref entry.awarenessRadius);
                    ReadFloat(pSo, "avoidanceRadius", ref entry.avoidanceRadius);
                    pSo.Dispose();
                }

                // ── Rigidbody ──
                var rb = prefab.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    entry.rbMass = rb.mass;
                    entry.rbDrag = rb.linearDamping;
                    entry.rbAngularDrag = rb.angularDamping;
                }
            }

            return entry;
        }

        // ═══════════════════════════════════════════════════════════════
        // GUI
        // ═══════════════════════════════════════════════════════════════

        private void OnGUI()
        {
            BuildStyles();

            DrawToolbar();
            EditorGUILayout.Space(4);

            // Фильтрация
            var filtered = string.IsNullOrWhiteSpace(_filterText)
                ? _ships
                : _ships.Where(s =>
                    s.displayName.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                    (s.customDisplayName ?? "").Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                    s.flightClass.ToString().Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                ).ToList();

            // ── Таблица ──
            float tableHeight = position.height * 0.45f;
            Rect tableRect = EditorGUILayout.GetControlRect(false, tableHeight);
            DrawTable(tableRect, filtered);

            EditorGUILayout.Space(4);

            // ── Нижняя панель: детали или batch ──
            if (_multiSelected.Count >= 2)
            {
                DrawBatchPanel(filtered);
            }
            else if (_selectedIndex >= 0 && _selectedIndex < filtered.Count)
            {
                DrawDetailPanel(filtered[_selectedIndex]);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Выберите корабль в таблице для просмотра деталей.\n" +
                    "Ctrl+Click для мультивыделения и массового редактирования.",
                    MessageType.Info);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // TOOLBAR
        // ═══════════════════════════════════════════════════════════════

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("🔍", GUILayout.Width(20));
            _filterText = EditorGUILayout.TextField(_filterText, EditorStyles.toolbarSearchField,
                GUILayout.Width(200));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(60)))
                Rescan();

            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════════════
        // TABLE
        // ═══════════════════════════════════════════════════════════════

        private enum SortColumn { Name, Class, Thrust, MaxSpeed, Yaw, Pitch, Vertical, MassMult, FuelMax, FuelConsumption, HullHP, Power, WindExposure }
        private SortColumn _sortCol = SortColumn.Name;
        private bool _sortAsc = true;

        private void DrawTable(Rect rect, List<ShipSummaryEntry> ships)
        {
            // Сортировка
            var sorted = SortShips(ships);

            float rowHeight = 22f;
            float colName = 110f;
            float colClass = 60f;
            float colVal = 68f;
            float totalWidth = colName + colClass + colVal * 10 + 16f;

            Rect viewRect = new Rect(0, 0, totalWidth, sorted.Count * rowHeight + rowHeight);
            _tableScroll = GUI.BeginScrollView(rect, _tableScroll, viewRect);

            // Заголовок
            Rect headerRect = new Rect(0, 0, totalWidth, rowHeight);
            EditorGUI.DrawRect(headerRect, new Color(0.25f, 0.25f, 0.25f));

            float x = 0;
            DrawColHeader(ref x, colName,  "🚢 Name",        SortColumn.Name);
            DrawColHeader(ref x, colClass, "Class",           SortColumn.Class);
            DrawColHeader(ref x, colVal,   "Thrust",         SortColumn.Thrust);
            DrawColHeader(ref x, colVal,   "MaxSpd",         SortColumn.MaxSpeed);
            DrawColHeader(ref x, colVal,   "Yaw",            SortColumn.Yaw);
            DrawColHeader(ref x, colVal,   "Pitch",          SortColumn.Pitch);
            DrawColHeader(ref x, colVal,   "Vert",           SortColumn.Vertical);
            DrawColHeader(ref x, colVal,   "Mass×",          SortColumn.MassMult);
            DrawColHeader(ref x, colVal,   "FuelMax",        SortColumn.FuelMax);
            DrawColHeader(ref x, colVal,   "Fuel/s",         SortColumn.FuelConsumption);
            DrawColHeader(ref x, colVal,   "HP",             SortColumn.HullHP);
            DrawColHeader(ref x, colVal,   "Power",          SortColumn.Power);
            DrawColHeader(ref x, colVal,   "WindExp",        SortColumn.WindExposure);

            // Строки
            for (int i = 0; i < sorted.Count; i++)
            {
                var ship = sorted[i];
                Rect rowRect = new Rect(0, rowHeight + i * rowHeight, totalWidth, rowHeight);

                bool isSelected = _multiSelected.Contains(i);
                if (isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.25f, 0.45f, 0.7f, 0.5f));
                else if (i % 2 == 0)
                    EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.22f, 0.22f));
                else
                    EditorGUI.DrawRect(rowRect, new Color(0.19f, 0.19f, 0.19f));

                x = 0;
                DrawCell(rowRect, ref x, colName,  ship.displayName);
                DrawCell(rowRect, ref x, colClass, ship.flightClass.ToString());
                DrawCell(rowRect, ref x, colVal,   F(ship.thrustForce));
                DrawCell(rowRect, ref x, colVal,   F(ship.maxSpeed));
                DrawCell(rowRect, ref x, colVal,   F(ship.yawForce));
                DrawCell(rowRect, ref x, colVal,   F(ship.pitchForce));
                DrawCell(rowRect, ref x, colVal,   F(ship.verticalForce));
                DrawCell(rowRect, ref x, colVal,   F(ship.massMultiplier));
                DrawCell(rowRect, ref x, colVal,   F(ship.maxFuel));
                DrawCell(rowRect, ref x, colVal,   F2(ship.fuelConsumptionRate));
                DrawCell(rowRect, ref x, colVal,   ship.hullHP.ToString());
                DrawCell(rowRect, ref x, colVal,   ship.availablePower.ToString());
                DrawCell(rowRect, ref x, colVal,   F2(ship.windExposure));

                // Клик
                Event evt = Event.current;
                if (evt.type == EventType.MouseDown && rowRect.Contains(evt.mousePosition))
                {
                    if (evt.control || evt.command)
                    {
                        if (_multiSelected.Contains(i))
                            _multiSelected.Remove(i);
                        else
                            _multiSelected.Add(i);
                    }
                    else
                    {
                        _selectedIndex = i;
                        _multiSelected.Clear();
                        _multiSelected.Add(i);
                    }
                    evt.Use();
                    Repaint();
                }
            }

            GUI.EndScrollView();
        }

        private List<ShipSummaryEntry> SortShips(List<ShipSummaryEntry> ships)
        {
            var list = new List<ShipSummaryEntry>(ships);
            int dir = _sortAsc ? 1 : -1;
            switch (_sortCol)
            {
                case SortColumn.Name:    list.Sort((a,b) => dir * string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase)); break;
                case SortColumn.Class:   list.Sort((a,b) => dir * a.flightClass.CompareTo(b.flightClass)); break;
                case SortColumn.Thrust:  list.Sort((a,b) => dir * a.thrustForce.CompareTo(b.thrustForce)); break;
                case SortColumn.MaxSpeed: list.Sort((a,b) => dir * a.maxSpeed.CompareTo(b.maxSpeed)); break;
                case SortColumn.Yaw:     list.Sort((a,b) => dir * a.yawForce.CompareTo(b.yawForce)); break;
                case SortColumn.Pitch:   list.Sort((a,b) => dir * a.pitchForce.CompareTo(b.pitchForce)); break;
                case SortColumn.Vertical: list.Sort((a,b) => dir * a.verticalForce.CompareTo(b.verticalForce)); break;
                case SortColumn.MassMult: list.Sort((a,b) => dir * a.massMultiplier.CompareTo(b.massMultiplier)); break;
                case SortColumn.FuelMax:  list.Sort((a,b) => dir * a.maxFuel.CompareTo(b.maxFuel)); break;
                case SortColumn.FuelConsumption: list.Sort((a,b) => dir * a.fuelConsumptionRate.CompareTo(b.fuelConsumptionRate)); break;
                case SortColumn.HullHP:   list.Sort((a,b) => dir * a.hullHP.CompareTo(b.hullHP)); break;
                case SortColumn.Power:    list.Sort((a,b) => dir * a.availablePower.CompareTo(b.availablePower)); break;
                case SortColumn.WindExposure: list.Sort((a,b) => dir * a.windExposure.CompareTo(b.windExposure)); break;
            }
            return list;
        }

        private void DrawColHeader(ref float x, float w, string label, SortColumn col)
        {
            Rect r = new Rect(x, 0, w, 20);
            if (GUI.Button(r, label, _headerStyle ?? EditorStyles.boldLabel))
            {
                if (_sortCol == col) _sortAsc = !_sortAsc;
                else { _sortCol = col; _sortAsc = true; }
            }
            x += w;
        }

        private void DrawCell(Rect row, ref float x, float w, string text)
        {
            Rect r = new Rect(x + 2, row.y, w - 2, row.height);
            GUI.Label(r, text, EditorStyles.label);
            x += w;
        }

        // ═══════════════════════════════════════════════════════════════
        // DETAIL PANEL
        // ═══════════════════════════════════════════════════════════════

        private void DrawDetailPanel(ShipSummaryEntry ship)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ship.prefabPath);
            if (prefab == null) return;

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.ExpandHeight(true));

            EditorGUILayout.LabelField($"📋 {ship.displayName}", EditorStyles.largeLabel);
            if (!string.IsNullOrEmpty(ship.customDisplayName))
                EditorGUILayout.LabelField($"   Display: {ship.customDisplayName}", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            // ── Flight ──
            _foldFlight = EditorGUILayout.BeginFoldoutHeaderGroup(_foldFlight, "🚀 Flight & Movement");
            if (_foldFlight)
            {
                EditorGUI.indentLevel++;
                DrawShipField(prefab, "ShipController", "shipFlightClass");
                DrawShipField(prefab, "ShipController", "thrustForce");
                DrawShipField(prefab, "ShipController", "maxSpeed");
                DrawShipField(prefab, "ShipController", "yawForce");
                DrawShipField(prefab, "ShipController", "pitchForce");
                DrawShipField(prefab, "ShipController", "verticalForce");
                DrawShipField(prefab, "ShipController", "antiGravity");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ── Smoothing ──
            _foldSmoothing = EditorGUILayout.BeginFoldoutHeaderGroup(_foldSmoothing, "🔄 Smoothing");
            if (_foldSmoothing)
            {
                EditorGUI.indentLevel++;
                DrawShipField(prefab, "ShipController", "yawSmoothTime");
                DrawShipField(prefab, "ShipController", "pitchSmoothTime");
                DrawShipField(prefab, "ShipController", "liftSmoothTime");
                DrawShipField(prefab, "ShipController", "thrustSmoothTime");
                DrawShipField(prefab, "ShipController", "yawDecayTime");
                DrawShipField(prefab, "ShipController", "pitchDecayTime");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ── Physics ──
            _foldPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldPhysics, "⚖️ Physics & Mass");
            if (_foldPhysics)
            {
                EditorGUI.indentLevel++;
                DrawShipField(prefab, "ShipController", "massMultiplier");
                DrawShipField(prefab, "ShipController", "linearDrag");
                DrawShipField(prefab, "ShipController", "angularDrag");

                EditorGUILayout.LabelField("Base Mass per Class", EditorStyles.boldLabel);
                DrawShipField(prefab, "ShipController", "massLight");
                DrawShipField(prefab, "ShipController", "massMedium");
                DrawShipField(prefab, "ShipController", "massHeavy");
                DrawShipField(prefab, "ShipController", "massHeavyII");

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Rigidbody: mass={ship.rbMass:F0}, drag={ship.rbDrag:F2}, angDrag={ship.rbAngularDrag:F2}",
                    EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ── Stabilization ──
            _foldStabilize = EditorGUILayout.BeginFoldoutHeaderGroup(_foldStabilize, "🎯 Stabilization");
            if (_foldStabilize)
            {
                EditorGUI.indentLevel++;
                DrawShipField(prefab, "ShipController", "autoStabilize");
                DrawShipField(prefab, "ShipController", "pitchStabForce");
                DrawShipField(prefab, "ShipController", "rollStabForce");
                DrawShipField(prefab, "ShipController", "maxPitchAngle");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ── Wind ──
            _foldWind = EditorGUILayout.BeginFoldoutHeaderGroup(_foldWind, "🌬️ Wind & Environment");
            if (_foldWind)
            {
                EditorGUI.indentLevel++;
                DrawShipField(prefab, "ShipController", "windInfluence");
                DrawShipField(prefab, "ShipController", "windExposure");
                DrawShipField(prefab, "ShipController", "windDecayTime");
                DrawShipField(prefab, "ShipController", "_globalWindEnabled");
                DrawShipField(prefab, "ShipController", "_globalWindForceScale");
                DrawShipField(prefab, "ShipController", "_globalWindVerticalFactor");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ── Fuel ──
            _foldFuel = EditorGUILayout.BeginFoldoutHeaderGroup(_foldFuel, "⛽ Fuel");
            if (_foldFuel)
            {
                EditorGUI.indentLevel++;
                DrawShipField(prefab, "ShipFuelSystem", "maxFuel");
                DrawShipField(prefab, "ShipFuelSystem", "fuelConsumptionRate");
                DrawShipField(prefab, "ShipFuelSystem", "fuelRegenRate");
                DrawShipField(prefab, "ShipFuelSystem", "startEngineConsumption");
                DrawShipField(prefab, "ShipFuelSystem", "idleConsumptionRate");
                DrawShipField(prefab, "ShipFuelSystem", "atmosphericRefuelRate");
                DrawShipField(prefab, "ShipFuelSystem", "thrustPenaltyDuringRefuel");
                DrawShipField(prefab, "ShipFuelSystem", "speedPenaltyDuringRefuel");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ── Cargo ──
            _foldCargo = EditorGUILayout.BeginFoldoutHeaderGroup(_foldCargo, "📦 Cargo");
            if (_foldCargo)
            {
                EditorGUI.indentLevel++;
                DrawShipField(prefab, "ShipController", "baseMaxCargoSlots");
                DrawShipField(prefab, "ShipController", "baseMaxCargoWeight");
                DrawShipField(prefab, "ShipController", "baseMaxCargoVolume");
                DrawShipField(prefab, "ShipController", "baseCargoPenaltyFactor");
                EditorGUILayout.HelpBox("Power: " + ship.availablePower, MessageType.None);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ── NPC ──
            _foldNpc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldNpc, "🤖 NPC");
            if (_foldNpc)
            {
                EditorGUI.indentLevel++;
                DrawShipField(prefab, "NpcShipController", "npcThrustMult");
                DrawShipField(prefab, "NpcShipController", "npcYawMult");
                DrawShipField(prefab, "NpcProximityZone", "awarenessRadius");
                DrawShipField(prefab, "NpcProximityZone", "avoidanceRadius");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ── Identity ──
            _foldIdentity = EditorGUILayout.BeginFoldoutHeaderGroup(_foldIdentity, "🔑 Identity & Debug");
            if (_foldIdentity)
            {
                EditorGUI.indentLevel++;
                DrawShipField(prefab, "ShipController", "_customDisplayName");
                DrawShipField(prefab, "ShipController", "_keyItemData");
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.EndScrollView();

            // ── Кнопка Select в Project ──
            EditorGUILayout.Space(4);
            if (GUILayout.Button("📁 Select Prefab in Project", GUILayout.Height(24)))
            {
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(ship.prefabPath);
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }

        private void DrawShipField(GameObject prefab, string componentTypeName, string propertyName)
        {
            Component comp = null;
            switch (componentTypeName)
            {
                case "ShipController":     comp = prefab.GetComponent<ShipController>(); break;
                case "ShipFuelSystem":     comp = prefab.GetComponent<ShipFuelSystem>(); break;
                case "ShipModuleManager":  comp = prefab.GetComponent<ShipModuleManager>(); break;
                case "NpcShipController":  comp = prefab.GetComponent<NpcShipController>(); break;
                case "NpcProximityZone":   comp = prefab.GetComponent<NpcProximityZone>(); break;
            }

            if (comp == null) return;

            var so = new SerializedObject(comp);
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(prop, true);
                if (EditorGUI.EndChangeCheck())
                {
                    so.ApplyModifiedProperties();
                    // Помечаем префаб dirty
                    EditorUtility.SetDirty(prefab);
                    // Перечитываем данные для таблицы
                    Rescan();
                }
            }
            so.Dispose();
        }

        // ═══════════════════════════════════════════════════════════════
        // BATCH PANEL
        // ═══════════════════════════════════════════════════════════════

        private void DrawBatchPanel(List<ShipSummaryEntry> filtered)
        {
            var selectedShips = new List<ShipSummaryEntry>();
            foreach (int idx in _multiSelected)
            {
                if (idx >= 0 && idx < filtered.Count)
                    selectedShips.Add(filtered[idx]);
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"📦 Batch Edit ({selectedShips.Count} ships selected)", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ShipFlightClass
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Flight Class", GUILayout.Width(140));
            string[] classNames = System.Enum.GetNames(typeof(ShipFlightClass));
            if (_batchClassIndex < 0) _batchClassIndex = 0;
            _batchClassIndex = EditorGUILayout.Popup(_batchClassIndex, classNames, GUILayout.Width(90));
            if (GUILayout.Button("Apply to Selected", GUILayout.Width(130)))
                BatchApplyEnum(selectedShips, "ShipController", "shipFlightClass", _batchClassIndex);
            EditorGUILayout.EndHorizontal();

            BatchFloatRow("Thrust Force",          ref _batchThrust,         selectedShips, "ShipController", "thrustForce");
            BatchFloatRow("Max Speed",             ref _batchMaxSpeed,       selectedShips, "ShipController", "maxSpeed");
            BatchFloatRow("Yaw Force",             ref _batchYaw,            selectedShips, "ShipController", "yawForce");
            BatchFloatRow("Pitch Force",           ref _batchPitch,          selectedShips, "ShipController", "pitchForce");
            BatchFloatRow("Vertical Force",        ref _batchVertical,       selectedShips, "ShipController", "verticalForce");
            BatchFloatRow("Mass Multiplier",       ref _batchMassMult,       selectedShips, "ShipController", "massMultiplier");
            BatchFloatRow("Max Fuel",              ref _batchFuelMax,        selectedShips, "ShipFuelSystem", "maxFuel");
            BatchFloatRow("Fuel Consumption Rate", ref _batchFuelConsumption, selectedShips, "ShipFuelSystem", "fuelConsumptionRate");
            BatchFloatRow("Available Power",       ref _batchPower,          selectedShips, "ShipModuleManager", "availablePower");
            BatchFloatRow("Wind Exposure",         ref _batchWindExposure,   selectedShips, "ShipController", "windExposure");
            BatchFloatRow("Linear Drag",           ref _batchLinearDrag,     selectedShips, "ShipController", "linearDrag");
            BatchFloatRow("Angular Drag",          ref _batchAngularDrag,    selectedShips, "ShipController", "angularDrag");

            EditorGUILayout.EndVertical();
        }

        private void BatchFloatRow(string label, ref string field, List<ShipSummaryEntry> ships,
            string compType, string propName)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(140));
            field = EditorGUILayout.TextField(field, GUILayout.Width(80));
            if (GUILayout.Button("Apply to Selected", GUILayout.Width(130)))
            {
                if (float.TryParse(field, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float val))
                {
                    BatchApplyFloat(ships, compType, propName, val);
                }
                else
                {
                    Debug.LogWarning($"[ShipSummary] Invalid float: '{field}'");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void BatchApplyFloat(List<ShipSummaryEntry> ships, string compType, string propName, float value)
        {
            foreach (var ship in ships)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ship.prefabPath);
                if (prefab == null) continue;
                Component comp = GetComponentByName(prefab, compType);
                if (comp == null) continue;

                var so = new SerializedObject(comp);
                var prop = so.FindProperty(propName);
                if (prop != null)
                {
                    switch (prop.propertyType)
                    {
                        case SerializedPropertyType.Float:
                            prop.floatValue = value;
                            break;
                        case SerializedPropertyType.Integer:
                            prop.intValue = Mathf.RoundToInt(value);
                            break;
                    }
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(prefab);
                }
                so.Dispose();
            }
            AssetDatabase.SaveAssets();
            Rescan();
            Debug.Log($"[ShipSummary] Batch set '{propName}' = {value} on {ships.Count} ships");
        }

        private void BatchApplyEnum(List<ShipSummaryEntry> ships, string compType, string propName, int enumIndex)
        {
            foreach (var ship in ships)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ship.prefabPath);
                if (prefab == null) continue;
                Component comp = GetComponentByName(prefab, compType);
                if (comp == null) continue;

                var so = new SerializedObject(comp);
                var prop = so.FindProperty(propName);
                if (prop != null && prop.propertyType == SerializedPropertyType.Enum)
                {
                    prop.enumValueIndex = enumIndex;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(prefab);
                }
                so.Dispose();
            }
            AssetDatabase.SaveAssets();
            Rescan();
            Debug.Log($"[ShipSummary] Batch set '{propName}' = {enumIndex} on {ships.Count} ships");
        }

        private Component GetComponentByName(GameObject prefab, string typeName)
        {
            switch (typeName)
            {
                case "ShipController":     return prefab.GetComponent<ShipController>();
                case "ShipFuelSystem":     return prefab.GetComponent<ShipFuelSystem>();
                case "ShipModuleManager":  return prefab.GetComponent<ShipModuleManager>();
                case "NpcShipController":  return prefab.GetComponent<NpcShipController>();
                case "NpcProximityZone":   return prefab.GetComponent<NpcProximityZone>();
                default: return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void BuildStyles()
        {
            if (_stylesBuilt) return;
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            _stylesBuilt = true;
        }

        private static void ReadFloat(SerializedObject so, string name, ref float target)
        {
            var p = so.FindProperty(name);
            if (p != null && p.propertyType == SerializedPropertyType.Float)
                target = p.floatValue;
        }

        private static void ReadInt(SerializedObject so, string name, ref int target)
        {
            var p = so.FindProperty(name);
            if (p != null && p.propertyType == SerializedPropertyType.Integer)
                target = p.intValue;
        }

        private static void ReadBool(SerializedObject so, string name, ref bool target)
        {
            var p = so.FindProperty(name);
            if (p != null && p.propertyType == SerializedPropertyType.Boolean)
                target = p.boolValue;
        }

        private static string F(float v)  => v >= 1000 ? $"{v/1000f:F1}k" : $"{v:F0}";
        private static string F2(float v) => $"{v:F1}";
    }
}
#endif
