using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Ship;

namespace ProjectC.Ship.UI
{
    /// <summary>
    /// ShipHudController — компактный полётный HUD, появляется только когда
    /// NetworkPlayer сидит в PilotSeatController. Заменяет ShipDebugHUD (F3) и
    /// MeziyStatusHUD_Legacy (F4). Только чтение из ShipController / WindManager /
    /// AltitudeCorridorSystem, никаких RPC.
    ///
    /// Архитектура: runtime-constructed VisualElement (как ShipKeyToast.cs).
    /// Не использует UXML/USS, всё в коде — проще менять, нет thin-strip багов.
    ///
    /// Иерархия root (5 колонок, см. docs/Ships/UI/HUD/00_OVERVIEW.md §3.2):
    ///   _root (top:8, height≈96, align-items:center)
    ///     └─ _centerRow (flex-row)
    ///         ├─ _colModules      (240px)   K1: модули
    ///         ├─ _colFlight       (200px)   K2: полёт (LIFT/TURN/PITCH/BANK)
    ///         ├─ _colSpeed        (180px)   K3: скорость (центр)
    ///         ├─ _colEnv          (220px)   K4: WIND + ALTITUDE
    ///         └─ _colDispatch     (200px)   K5: DISPATCHER/REGION/CORRIDOR (заглушки)
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class ShipHudController : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("UIDocument на этом GameObject. auto-cache в Awake.")]
        [SerializeField] private UIDocument _doc;

        [Header("Auto-spawn")]
        [Tooltip("Если PanelSettings не задан в Inspector, грузим по этому пути из Resources/.")]
        [SerializeField] private string _panelSettingsResourcePath = "UI/ShipHudPanelSettings";

        [Header("Размеры (см. §3.2 design doc)")]
        [SerializeField] private float _rootTopOffset = 8f;
        [SerializeField] private float _rootHeight = 62f;  // S-HUD-03b fix: 96→62 (x1.5 уменьшение)

        // Стек построения (5-step guards по project-c-ui-toolkit-runtime §0)
        private bool _built = false;
        private bool _wasShown = false;

        // Root-элементы (построены в TryBuild)
        private VisualElement _root;
        private VisualElement _centerRow;
        private VisualElement _colModules;
        private VisualElement _colFlight;
        private VisualElement _colSpeed;
        private VisualElement _colEnv;
        private VisualElement _colDispatch;

        // S-HUD-03b: Speed column (K3)
        private Label _speedValueLabel;
        private VisualElement _speedBarFill;
        private Label _maxSpeedLabel;

        // Fuel display under speed (K3-b)
        private VisualElement _fuelBarFill;
        private Label _fuelLabel;
        private VisualElement _refuelDot;   // зелёный кружок при заправке
        private Label _refuelLabel;          // "REFUEL +2.0/s"

        // ENGINE-STATE: статус двигателя под топливом (K3-c)
        private Label _engineStatusLabel;

        // S-HUD-03c: Flight column (K2) — 4 строки LIFT/TURN/PITCH/BANK
        // Каждый элемент — массив [lift, turn, pitch, bank]
        private Label[] _flightLabels;     // левая часть "LIFT" / "TURN" / ...
        private Label[] _flightValues;     // правая "+1.2" / "30°/s" / ...
        private VisualElement[] _flightNegFill;  // bar левый сегмент (отрицательный)
        private VisualElement[] _flightPosFill;  // bar правый сегмент (положительный)

        // S-HUD-03d: Modules column (K1) — контейнер для строк модулей
        private VisualElement _modulesContainer;

        // S-HUD-03e: Environment column (K4) — WIND + ALTITUDE
        private Label _windSpeedLabel;
        private VisualElement _windCompass;
        private float _lastCompassAngle = -999f;
        private Label _altValueLabel;
        private Label _altCorridorLabel;
        private VisualElement _altBarFill; // сюда добавляются/удаляются строки

        // LocalPlayer (кешируем после нахождения; null если не найден)
        private ProjectC.Player.NetworkPlayer _localPlayer;

        private void Awake()
        {
            // S-HUD-03a: cache UIDocument, fallback PanelSettings, persist scene
            if (_doc == null) _doc = GetComponent<UIDocument>();
            bool docFound = _doc != null;
            bool psFromInspector = docFound && _doc.panelSettings != null;

            // FALLBACK PanelSettings (как ShipKeyToast.cs — критично для не-thin-strip)
            if (_doc != null && _doc.panelSettings == null)
            {
                var ps = Resources.Load<PanelSettings>(_panelSettingsResourcePath);
                if (ps != null) _doc.panelSettings = ps;
                else Debug.LogWarning($"[ShipHudController] PanelSettings not found at Resources/{_panelSettingsResourcePath} — HUD не отрисуется.");
            }

            // DontDestroyOnLoad только для root GO (per project-c-ui-toolkit-runtime §3.2)
            if (transform.parent == null && Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            // S-HUD-03a: 5-step guard (panelSettings обязателен ДО построения дерева)
            if (!_built)
            {
                TryBuild();
                if (!_built) { /* guards не прошли — попробуем на следующем кадре */ return; }
                Debug.Log("[ShipHudController] TryBuild SUCCESS — HUD дерево построено.");
            }

            // 1. Найти локального игрока (race-handling: не заспавнен = HUD скрыт)
            EnsureLocalPlayer();
            if (_localPlayer == null)
            {
                if (_wasShown) SetVisible(false);
                return;
            }

            // 2. Видимость по состоянию
            bool shouldShow = _localPlayer.IsInShip && _localPlayer.CurrentShip != null;
            if (shouldShow != _wasShown)
            {
                Debug.Log($"[ShipHudController] SetVisible({shouldShow}): IsInShip={_localPlayer.IsInShip}, CurrentShip={(_localPlayer.CurrentShip != null ? _localPlayer.CurrentShip.name : "null")}");
                SetVisible(shouldShow);
            }
            if (!shouldShow) return;

            // 3. Обновить данные (пока пусто — S-HUD-03b..f наполнят)
            Refresh(_localPlayer.CurrentShip);
        }

        /// <summary>
        /// TryBuild — 5-step guard per project-c-ui-toolkit-runtime §0.
        /// Шаги 1-3: doc + rootVE + panelSettings (без panelSettings рендер невозможен).
        /// Шаг 4: styleSheets (нет — runtime-constructed, USS не используем).
        /// Шаг 5: Inspector binding (делается в scene placement S-HUD-04).
        /// </summary>
        private void TryBuild()
        {
            // S-HUD-03a debug guard chain
            if (_doc == null) { Debug.LogWarning("[ShipHudController] TryBuild BLOCKED: _doc == null (Awake не нашёл UIDocument?)"); return; }
            if (_doc.rootVisualElement == null) { Debug.LogWarning("[ShipHudController] TryBuild BLOCKED: rootVisualElement == null (UIDocument не инициализирован)"); return; }
            if (_doc.panelSettings == null) { Debug.LogWarning("[ShipHudController] TryBuild BLOCKED: panelSettings == null (ShipHudPanelSettings не загружен)"); return; }

            // Root — absolute-positioned overlay, top-anchored, центрированный по горизонтали
            _root = new VisualElement { name = "ship-hud-root", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.top = _rootTopOffset;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.height = _rootHeight;
            _root.style.alignItems = Align.Center;      // горизонтальное центрирование centerRow
            _root.style.justifyContent = Justify.Center;
            _root.style.display = DisplayStyle.None;     // по умолчанию скрыт — покажется когда IsInShip

            // CenterRow — flex-row, все 5 колонок внутри
            _centerRow = new VisualElement { name = "ship-hud-row" };
            _centerRow.style.flexDirection = FlexDirection.Row;
            _centerRow.style.height = Length.Percent(100);
            _centerRow.style.alignItems = Align.Center;

            // 5 колонок
            _colModules   = MakeColumn("col-modules",   240f, flexShrink: 1f);
            _colFlight    = MakeColumn("col-flight",    200f, flexShrink: 0f);  // критические данные — не сжимаем
            _colSpeed     = MakeColumn("col-speed",     180f, flexShrink: 0f);
            _colEnv       = MakeColumn("col-env",       220f, flexShrink: 1f);
            _colDispatch  = MakeColumn("col-dispatch",  200f, flexShrink: 1f);

            // S-HUD-03b: наполнить _colSpeed (K3 — скорость, центр)
            BuildSpeedColumn();

            // S-HUD-03c: наполнить _colFlight (K2 — полёт, левая)
            BuildFlightColumn();

            // S-HUD-03d: наполнить _colModules (K1 — модули, самая левая)
            BuildModulesColumn();

            // S-HUD-03e: наполнить _colEnv (K4 — WIND + ALTITUDE)
            BuildEnvColumn();

            // S-HUD-03f: наполнить _colDispatch (K5 — заглушки)
            BuildDispatchColumn();

            _centerRow.Add(_colModules);
            _centerRow.Add(_colFlight);
            _centerRow.Add(_colSpeed);
            _centerRow.Add(_colEnv);
            _centerRow.Add(_colDispatch);
            _root.Add(_centerRow);

            _doc.rootVisualElement.Add(_root);

            _built = true;
            Debug.Log("[ShipHudController] TryBuild: built (5 empty columns). Колонки пустые до S-HUD-03b..f.");
        }

        private VisualElement MakeColumn(string name, float widthPx, float flexShrink)
        {
            var col = new VisualElement { name = name };
            col.style.width = widthPx;
            col.style.height = Length.Percent(100);
            col.style.flexShrink = flexShrink;
            col.style.flexDirection = FlexDirection.Column;
            col.style.justifyContent = Justify.FlexStart;
            col.style.alignItems = Align.Stretch;          // дети заполняют по ширине
            col.style.marginLeft = 4;
            col.style.marginRight = 4;
            return col;
        }

        private void SetVisible(bool visible)
        {
            if (_root == null) return;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            // pickingMode=Position когда виден (пропускает клики сквозь прозрачные области HUD)
            // pickingMode=Ignore когда скрыт (не блокирует клики по миру — per §2.5)
            _root.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
            _wasShown = visible;
        }

        /// <summary>
        /// Найти локального NetworkPlayer (того, чьим IsOwner=true).
        /// Кешируем после первого нахождения. Если singleton ещё не спавнен (race при
        /// scene transition), возвращаем null → HUD скрыт.
        /// </summary>
        private void EnsureLocalPlayer()
        {
            if (_localPlayer != null) return;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            var players = nm.SpawnManager?.PlayerObjects;
            if (players == null) return;
            foreach (var no in players)
            {
                if (no == null) continue;
                var np = no.GetComponent<ProjectC.Player.NetworkPlayer>();
                if (np != null && np.IsOwner) { _localPlayer = np; return; }
            }
        }

        /// <summary>
        /// Refresh — обновляет цифры/бары в колонках. S-HUD-03a: пустой.
        /// S-HUD-03b..f добавят UpdateSpeedColumn, UpdateFlightColumn, и т.д.
        /// </summary>
        private void Refresh(ProjectC.Player.ShipController ship)
        {
            UpdateSpeedColumn(ship);
            UpdateFlightColumn(ship);
            UpdateModulesColumn(ship);
            UpdateEnvColumn(ship);
            UpdateDispatchColumn();  // T-DOCK-HUD-2: docking zone status
        }

        // ==================== S-HUD-03b: Speed (K3) ====================

        /// <summary>
        /// Построить визуальные элементы колонки скорости (вызывается 1 раз в TryBuild).
        /// _colSpeed уже создан как flex-контейнер. Добавляем дочерние VEs.
        /// </summary>
        private void BuildSpeedColumn()
        {
            if (_colSpeed == null) return;

            // SPEED цифра (крупный текст)
            _speedValueLabel = new Label { name = "speed-value" };
            _speedValueLabel.text = "SPEED\n0 км/ч";
            _speedValueLabel.style.fontSize = 18;                  // 26→18 (x1.5)
            _speedValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _speedValueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _speedValueLabel.style.color = Color.white;
            _speedValueLabel.style.marginBottom = 3;
            _colSpeed.Add(_speedValueLabel);

            // Progress bar (горизонтальный)
            var barTrack = new VisualElement { name = "speed-bar-track" };
            barTrack.style.height = 4;                              // 6→4
            barTrack.style.minHeight = 4;
            barTrack.style.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 0.8f);
            barTrack.style.borderTopLeftRadius = 2;
            barTrack.style.borderTopRightRadius = 2;
            barTrack.style.borderBottomLeftRadius = 2;
            barTrack.style.borderBottomRightRadius = 2;
            barTrack.style.overflow = Overflow.Hidden;
            barTrack.style.flexShrink = 0;

            _speedBarFill = new VisualElement { name = "speed-bar-fill" };
            _speedBarFill.style.height = Length.Percent(100);
            _speedBarFill.style.width = Length.Percent(0);
            _speedBarFill.style.backgroundColor = new Color(0.31f, 0.78f, 0.47f); // зелёный
            barTrack.Add(_speedBarFill);
            _colSpeed.Add(barTrack);

            // MAX текст
            _maxSpeedLabel = new Label { name = "speed-max" };
            _maxSpeedLabel.text = "MAX 0 км/ч";
            _maxSpeedLabel.style.fontSize = 8;                     // 10→8
            _maxSpeedLabel.style.color = new Color(1, 1, 1, 0.5f);
            _maxSpeedLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _maxSpeedLabel.style.marginTop = 1;
            _colSpeed.Add(_maxSpeedLabel);

            // ── FUEL bar (под MAX) ──
            var fuelTrack = new VisualElement { name = "fuel-bar-track" };
            fuelTrack.style.height = 3;
            fuelTrack.style.minHeight = 3;
            fuelTrack.style.marginTop = 2;
            fuelTrack.style.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 0.8f);
            fuelTrack.style.borderTopLeftRadius = 2;
            fuelTrack.style.borderTopRightRadius = 2;
            fuelTrack.style.borderBottomLeftRadius = 2;
            fuelTrack.style.borderBottomRightRadius = 2;
            fuelTrack.style.overflow = Overflow.Hidden;
            fuelTrack.style.flexShrink = 0;

            _fuelBarFill = new VisualElement { name = "fuel-bar-fill" };
            _fuelBarFill.style.height = Length.Percent(100);
            _fuelBarFill.style.width = Length.Percent(100);
            _fuelBarFill.style.backgroundColor = new Color(0.2f, 0.6f, 1.0f); // синий
            fuelTrack.Add(_fuelBarFill);
            _colSpeed.Add(fuelTrack);

            // FUEL текст + refuel строка
            _fuelLabel = new Label { name = "fuel-label" };
            _fuelLabel.text = "FUEL 100/100";
            _fuelLabel.style.fontSize = 8;
            _fuelLabel.style.color = new Color(1, 1, 1, 0.85f);
            _fuelLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _fuelLabel.style.marginTop = 1;
            _colSpeed.Add(_fuelLabel);

            // REFUEL строка (скрыта, показывается только при isRefueling)
            var refuelRow = new VisualElement { name = "refuel-row" };
            refuelRow.style.flexDirection = FlexDirection.Row;
            refuelRow.style.justifyContent = Justify.Center;
            refuelRow.style.alignItems = Align.Center;
            refuelRow.style.marginTop = 1;
            refuelRow.style.display = DisplayStyle.None;

            _refuelDot = new VisualElement { name = "refuel-dot" };
            _refuelDot.style.width = 6;
            _refuelDot.style.height = 6;
            _refuelDot.style.minWidth = 6;
            _refuelDot.style.minHeight = 6;
            _refuelDot.style.borderTopLeftRadius = 3;
            _refuelDot.style.borderTopRightRadius = 3;
            _refuelDot.style.borderBottomLeftRadius = 3;
            _refuelDot.style.borderBottomRightRadius = 3;
            _refuelDot.style.backgroundColor = new Color(0.31f, 0.78f, 0.47f); // зелёный
            _refuelDot.style.flexShrink = 0;
            _refuelDot.style.marginRight = 3;
            refuelRow.Add(_refuelDot);

            _refuelLabel = new Label { name = "refuel-label" };
            _refuelLabel.text = "REFUEL +2.0/s";
            _refuelLabel.style.fontSize = 7;
            _refuelLabel.style.color = new Color(0.31f, 0.78f, 0.47f);
            _refuelLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            refuelRow.Add(_refuelLabel);

            _colSpeed.Add(refuelRow);

            // ── ENGINE STATUS (под FUEL/REFUEL) ──
            _engineStatusLabel = new Label { name = "engine-status" };
            _engineStatusLabel.text = "ENGINE OFF";
            _engineStatusLabel.style.fontSize = 8;
            _engineStatusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _engineStatusLabel.style.marginTop = 2;
            _colSpeed.Add(_engineStatusLabel);
        }

        /// <summary>
        /// Обновить данные скорости (вызывается каждый кадр когда IsInShip == true).
        /// </summary>
        private void UpdateSpeedColumn(ProjectC.Player.ShipController ship)
        {
            if (_speedValueLabel == null || ship == null) return;

            // Только forward component (W/S) → км/ч
            float speedKmh = Mathf.Abs(ship.ForwardSpeedMps) * 3.6f;
            float maxSpeedKmh = ship.MaxSpeed * 3.6f;

            _speedValueLabel.text = $"SPEED\n{speedKmh:F0} км/ч";
            _maxSpeedLabel.text = $"MAX {maxSpeedKmh:F0} км/ч";

            float fill = maxSpeedKmh > 1f ? Mathf.Clamp01(speedKmh / maxSpeedKmh) : 0f;
            _speedBarFill.style.width = Length.Percent(fill * 100f);

            Color barColor;
            if (fill < 0.5f) barColor = new Color(0.31f, 0.78f, 0.47f);
            else if (fill < 0.8f) barColor = new Color(0.94f, 0.78f, 0.31f);
            else barColor = new Color(0.86f, 0.31f, 0.31f);
            _speedBarFill.style.backgroundColor = barColor;

            // ── FUEL: bar + label + refuel indicator ──
            var fs = ship.FuelSystem;
            if (fs != null)
            {
                float fuelPct = fs.FuelPercent;
                _fuelBarFill.style.width = Length.Percent(fuelPct * 100f);

                // Цвет fuel bar: зелёный > 0.4, жёлтый > 0.2, красный
                Color fuelColor;
                if (fuelPct > 0.4f) fuelColor = new Color(0.31f, 0.78f, 0.47f);
                else if (fuelPct > 0.2f) fuelColor = new Color(0.94f, 0.78f, 0.31f);
                else fuelColor = new Color(0.86f, 0.31f, 0.31f);
                _fuelBarFill.style.backgroundColor = fuelColor;

                _fuelLabel.text = $"FUEL {fs.CurrentFuel:F0}/{fs.MaxFuel:F0}";

                // REFUEL indicator
                var refuelRow = _colSpeed?.Q("refuel-row");
                if (refuelRow != null)
                {
                    bool isRefueling = fs.isRefueling;
                    refuelRow.style.display = isRefueling ? DisplayStyle.Flex : DisplayStyle.None;
                    if (isRefueling && _refuelLabel != null)
                    {
                        _refuelLabel.text = $"REFUEL +{fs.AtmosphericRefuelRate:F1}/s";
                    }
                }
            }

            // ── ENGINE STATUS ──
            if (_engineStatusLabel != null)
            {
                bool engineOn = ship.IsEngineRunning;
                _engineStatusLabel.text = engineOn ? "ENGINE ON" : "ENGINE OFF";
                _engineStatusLabel.style.color = engineOn
                    ? new Color(0.31f, 0.78f, 0.47f)   // зелёный
                    : new Color(0.86f, 0.31f, 0.31f);   // красный
            }
        }

        // ==================== S-HUD-03c: Flight (K2) ====================

        /// <summary>
        /// Имена строк и их единицы измерения для Flight колонки.
        /// </summary>
        private static readonly string[] FlightNames = { "LIFT", "TURN", "PITCH", "BANK" };

        private void BuildFlightColumn()
        {
            if (_colFlight == null) return;
            int n = FlightNames.Length;

            _flightLabels   = new Label[n];
            _flightValues   = new Label[n];
            _flightNegFill  = new VisualElement[n];
            _flightPosFill  = new VisualElement[n];

            // Header
            var hdr = new Label { name = "flight-header", text = "FLIGHT" };
            hdr.style.fontSize = 8;
            hdr.style.color = new Color(1, 1, 1, 0.6f);
            hdr.style.unityTextAlign = TextAnchor.MiddleLeft;
            hdr.style.marginBottom = 1;
            hdr.style.marginLeft = 2;
            _colFlight.Add(hdr);

            for (int i = 0; i < n; i++)
            {
                var row = new VisualElement { name = FlightNames[i] };
                row.style.flexDirection = FlexDirection.Column;
                // Каждая строка: [label  |  value] + [bar: neg│pos]
                row.style.height = 13;
                row.style.flexShrink = 0;
                row.style.marginBottom = 0;
                row.style.marginLeft = 2;
                row.style.marginRight = 2;

                // Верхняя строка: имя + число
                var topLine = new VisualElement { name = FlightNames[i] + "-top" };
                topLine.style.flexDirection = FlexDirection.Row;
                topLine.style.justifyContent = Justify.SpaceBetween;
                topLine.style.width = Length.Percent(100);
                topLine.style.height = 10;

                var nameLabel = new Label { name = FlightNames[i] + "-label" };
                nameLabel.text = FlightNames[i];
                nameLabel.style.fontSize = 9;
                nameLabel.style.color = new Color(1, 1, 1, 0.85f);
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                nameLabel.style.flexShrink = 0;
                _flightLabels[i] = nameLabel;
                topLine.Add(nameLabel);

                var valLabel = new Label { name = FlightNames[i] + "-val" };
                valLabel.text = "0.0";
                valLabel.style.fontSize = 9;
                valLabel.style.color = new Color(1, 1, 1, 0.85f);
                valLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                valLabel.style.flexShrink = 0;
                _flightValues[i] = valLabel;
                topLine.Add(valLabel);

                row.Add(topLine);

                // Bar line: center-zero (neg│pos)
                var barLine = new VisualElement { name = FlightNames[i] + "-bar" };
                barLine.style.flexDirection = FlexDirection.Row;
                barLine.style.height = 3;
                barLine.style.minHeight = 3;
                barLine.style.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 0.6f);
                barLine.style.borderTopLeftRadius = 1;
                barLine.style.borderTopRightRadius = 1;
                barLine.style.borderBottomLeftRadius = 1;
                barLine.style.borderBottomRightRadius = 1;
                barLine.style.overflow = Overflow.Hidden;
                barLine.style.flexShrink = 0;
                barLine.style.width = Length.Percent(100);

                // neg fill (red, left side)
                var neg = new VisualElement { name = FlightNames[i] + "-neg" };
                neg.style.height = Length.Percent(100);
                neg.style.width = Length.Percent(0);
                neg.style.backgroundColor = new Color(0.86f, 0.31f, 0.31f, 0.9f);
                _flightNegFill[i] = neg;
                barLine.Add(neg);

                // center marker (always visible)
                var ctr = new VisualElement { name = FlightNames[i] + "-ctr" };
                ctr.style.width = 2;
                ctr.style.height = Length.Percent(100);
                ctr.style.backgroundColor = new Color(1, 1, 1, 0.35f);
                ctr.style.flexShrink = 0;
                barLine.Add(ctr);

                // pos fill (green, right side)
                var pos = new VisualElement { name = FlightNames[i] + "-pos" };
                pos.style.height = Length.Percent(100);
                pos.style.width = Length.Percent(0);
                pos.style.backgroundColor = new Color(0.31f, 0.78f, 0.47f, 0.9f);
                _flightPosFill[i] = pos;
                barLine.Add(pos);

                row.Add(barLine);
                _colFlight.Add(row);
            }
        }

        /// <summary>
        /// Обновить 4 строки полётных данных каждый кадр.
        /// </summary>
        private void UpdateFlightColumn(ProjectC.Player.ShipController ship)
        {
            if (_flightLabels == null || ship == null) return;

            // LIFT: VerticalSpeed (м/с), range ±10
            UpdateFlightRow(0, ship.VerticalSpeed, 10f, " м/с");

            // TURN: angularVelocity.y (rad/s → deg/s), range ±180
            float yawDeg = ship.AngularVelocity.y * Mathf.Rad2Deg;
            UpdateFlightRow(1, yawDeg, 180f, "°/с");

            // PITCH: nose up/down in deg, range ±20 (maxPitchAngle)
            UpdateFlightRow(2, ship.PitchAngleDegrees, 20f, "\u00B0");

            // BANK: roll in deg, range ±90
            UpdateFlightRow(3, ship.RollAngleDegrees, 90f, "\u00B0");
        }

        private void UpdateFlightRow(int idx, float value, float range, string unit)
        {
            if (idx < 0 || idx >= _flightLabels.Length) return;

            string sign = value >= 0f ? "+" : "";
            _flightValues[idx].text = $"{sign}{value:F1}{unit}";
            _flightLabels[idx].text = FlightNames[idx];

            // Center-zero bar: value от -range до +range
            float clamped = Mathf.Clamp(value, -range, range);
            float pct = range > 0.01f ? clamped / range : 0f; // -1..+1

            if (pct >= 0f)
            {
                _flightNegFill[idx].style.width = Length.Percent(0);
                _flightPosFill[idx].style.width = Length.Percent(pct * 50f);
            }
            else
            {
                _flightNegFill[idx].style.width = Length.Percent(Mathf.Abs(pct) * 50f);
                _flightPosFill[idx].style.width = Length.Percent(0);
            }
        }

        // ==================== S-HUD-03d: Modules (K1) ====================

        private void BuildModulesColumn()
        {
            if (_colModules == null) return;

            // Header
            var hdr = new Label { name = "modules-header", text = "MODULES" };
            hdr.style.fontSize = 8;
            hdr.style.color = new Color(1, 1, 1, 0.6f);
            hdr.style.unityTextAlign = TextAnchor.MiddleLeft;
            hdr.style.marginBottom = 1;
            hdr.style.marginLeft = 2;
            _colModules.Add(hdr);

            // Container for rows (ребуилдится каждый кадр в UpdateModulesColumn)
            _modulesContainer = new VisualElement { name = "modules-rows" };
            _modulesContainer.style.flexDirection = FlexDirection.Column;
            _modulesContainer.style.alignItems = Align.Stretch;
            _modulesContainer.style.overflow = Overflow.Hidden;
            _modulesContainer.style.flexShrink = 1;
            _modulesContainer.style.flexGrow = 1;
            _colModules.Add(_modulesContainer);

            // "+N more" label (скрыт, показывается если слотов > 4)
            // Создаётся в Build, текст обновляется в Update
            var moreLabel = new Label { name = "modules-more", text = "" };
            moreLabel.style.fontSize = 7;
            moreLabel.style.color = new Color(1, 1, 1, 0.4f);
            moreLabel.style.display = DisplayStyle.None;
            _modulesContainer.Add(moreLabel);
        }

        private void UpdateModulesColumn(ProjectC.Player.ShipController ship)
        {
            if (_modulesContainer == null || ship == null) return;

            // Очищаем контейнер — перестраиваем строки заново
            _modulesContainer.Clear();

            // "+N more" создаётся заново после Clear
            var moreLabel = new Label { name = "modules-more", text = "" };
            moreLabel.style.fontSize = 7;
            moreLabel.style.color = new Color(1, 1, 1, 0.4f);
            moreLabel.style.display = DisplayStyle.None;

            var mm = ship.ShipModuleManager;
            if (mm == null || mm.slots == null) return;

            // Типичный корабль: 4-6 слотов
            const int maxVisible = 4;
            int occupiedCount = 0;
            int visibleCount = 0;

            foreach (var slot in mm.slots)
            {
                if (slot == null || !slot.isOccupied) continue; // NOTE: slot.isOccupied
                occupiedCount++;
                if (visibleCount >= maxVisible) continue; // за лимитом — только считаем

                visibleCount++;

                var module = slot.installedModule;
                if (module == null) continue;

                // Имя: последний токен после "_". "MODULE_MEZIY_PITCH" → "PITCH"
                string shortName = module.moduleId;
                int lastUnderscore = shortName.LastIndexOf('_');
                if (lastUnderscore >= 0 && lastUnderscore < shortName.Length - 1)
                    shortName = shortName.Substring(lastUnderscore + 1);

                // Состояние через MeziyModuleActivator
                var activator = ship.MeziyModuleActivator;
                var state = activator != null ? activator.GetState(module.moduleId) : null;

                // Цвет кружка
                Color circleColor;
                if (state == null) circleColor = new Color(0.47f, 0.47f, 0.47f); // серый (не мезий)
                else if (state.isOnCooldown) circleColor = new Color(0.86f, 0.31f, 0.31f); // красный
                else if (state.isActive) circleColor = new Color(0.94f, 0.63f, 0.24f);    // оранжевый
                else circleColor = new Color(0.31f, 0.78f, 0.47f);                         // зелёный

                var row = new VisualElement { name = "mod-" + shortName };
                row.style.flexDirection = FlexDirection.Row;
                row.style.height = 12;
                row.style.minHeight = 12;
                row.style.flexShrink = 0;
                row.style.alignItems = Align.Center;
                row.style.marginLeft = 2;
                row.style.marginRight = 2;
                row.style.marginBottom = 0;

                // Кружок
                var dot = new VisualElement { name = shortName + "-dot" };
                dot.style.width = 8;
                dot.style.height = 8;
                dot.style.borderTopLeftRadius = 4;
                dot.style.borderTopRightRadius = 4;
                dot.style.borderBottomLeftRadius = 4;
                dot.style.borderBottomRightRadius = 4;
                dot.style.backgroundColor = circleColor;
                dot.style.flexShrink = 0;
                dot.style.marginRight = 4;
                row.Add(dot);

                // Имя
                var label = new Label { name = shortName + "-name", text = shortName };
                label.style.fontSize = 9;
                label.style.color = new Color(1, 1, 1, 0.85f);
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.flexShrink = 1;
                row.Add(label);

                // Процент перегрева
                if (state != null && state.isActive)
                {
                    float heat = activator != null ? activator.GetOverheatProgress(module.moduleId) : 0f;
                    if (heat > 0.01f)
                    {
                        var pct = new Label { name = shortName + "-heat" };
                        pct.text = $"{heat * 100f:F0}%";
                        pct.style.fontSize = 7;
                        pct.style.color = new Color(0.94f, 0.63f, 0.24f);
                        pct.style.unityTextAlign = TextAnchor.MiddleRight;
                        pct.style.flexShrink = 0;
                        pct.style.marginLeft = 2;
                        row.Add(pct);
                    }
                }

                _modulesContainer.Add(row);
            }

            // "+N more" если есть скрытые
            string moreText = "";
            int hidden = occupiedCount - maxVisible;
            if (hidden > 0) moreText = $"+{hidden} more";
            bool hasMore = hidden > 0;

            if (moreLabel != null)
            {
                moreLabel.text = moreText;
                moreLabel.style.display = hasMore ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasMore) _modulesContainer.Add(moreLabel);
            }

            // Грязный хак: если слотов всего 0 — покажем "—"
            if (occupiedCount == 0)
            {
                var emptyLabel = new Label { name = "mods-empty", text = "—" };
                emptyLabel.style.fontSize = 9;
                emptyLabel.style.color = new Color(1, 1, 1, 0.3f);
                emptyLabel.style.marginLeft = 2;
                _modulesContainer.Add(emptyLabel);
            }
        }

        // ==================== S-HUD-03e: Environment (K4 — WIND + ALTITUDE) ====================

        private void BuildEnvColumn()
        {
            if (_colEnv == null) return;

            // Header
            var hdr = new Label { name = "env-header", text = "ENV" };
            hdr.style.fontSize = 8;
            hdr.style.color = new Color(1, 1, 1, 0.6f);
            hdr.style.marginBottom = 2;
            hdr.style.marginLeft = 2;
            _colEnv.Add(hdr);

            // ── WIND row ──
            var windRow = new VisualElement { name = "env-wind" };
            windRow.style.flexDirection = FlexDirection.Row;
            windRow.style.height = 24;
            windRow.style.minHeight = 24;
            windRow.style.flexShrink = 0;
            windRow.style.alignItems = Align.Center;
            windRow.style.marginLeft = 2;
            windRow.style.marginRight = 2;

            // Левая половина WIND — цифра + направление текстом
            var windLeft = new VisualElement { name = "env-wind-left" };
            windLeft.style.flexDirection = FlexDirection.Column;
            windLeft.style.flexGrow = 1;
            windLeft.style.justifyContent = Justify.Center;

            _windSpeedLabel = new Label { name = "wind-speed" };
            _windSpeedLabel.text = "0 м/с";
            _windSpeedLabel.style.fontSize = 12;
            _windSpeedLabel.style.color = Color.white;
            _windSpeedLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            windLeft.Add(_windSpeedLabel);

            var windDirLabel = new Label { name = "wind-dir-label" };
            windDirLabel.text = "→";
            windDirLabel.style.fontSize = 9;
            windDirLabel.style.color = new Color(1, 1, 1, 0.5f);
            windDirLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            windDirLabel.style.marginTop = -1;
            windLeft.Add(windDirLabel);
            windRow.Add(windLeft);

            // Правая половина WIND — мини-компас 28×28
            _windCompass = new VisualElement { name = "wind-compass" };
            _windCompass.style.width = 28;
            _windCompass.style.minWidth = 28;
            _windCompass.style.height = 28;
            _windCompass.style.minHeight = 28;
            _windCompass.style.flexShrink = 0;
            _windCompass.style.marginLeft = 4;
            _windCompass.generateVisualContent += OnCompassGenerateContent;
            windRow.Add(_windCompass);

            _colEnv.Add(windRow);

            // ── ALT row ──
            var altRow = new VisualElement { name = "env-alt" };
            altRow.style.flexDirection = FlexDirection.Row;
            altRow.style.height = 24;
            altRow.style.minHeight = 24;
            altRow.style.flexShrink = 0;
            altRow.style.alignItems = Align.Center;
            altRow.style.marginLeft = 2;
            altRow.style.marginRight = 2;

            // Левая половина ALT — высота + имя коридора
            var altLeft = new VisualElement { name = "env-alt-left" };
            altLeft.style.flexDirection = FlexDirection.Column;
            altLeft.style.flexGrow = 1;
            altLeft.style.justifyContent = Justify.Center;

            _altValueLabel = new Label { name = "alt-value" };
            _altValueLabel.text = "0 м";
            _altValueLabel.style.fontSize = 12;
            _altValueLabel.style.color = Color.white;
            _altValueLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            altLeft.Add(_altValueLabel);

            _altCorridorLabel = new Label { name = "alt-corridor" };
            _altCorridorLabel.text = "---";
            _altCorridorLabel.style.fontSize = 9;
            _altCorridorLabel.style.color = new Color(1, 1, 1, 0.5f);
            _altCorridorLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _altCorridorLabel.style.marginTop = -1;
            altLeft.Add(_altCorridorLabel);
            altRow.Add(altLeft);

            // Правая половина ALT — вертикальный bar
            var altBarTrack = new VisualElement { name = "alt-bar-track" };
            altBarTrack.style.width = 6;
            altBarTrack.style.minWidth = 6;
            altBarTrack.style.height = 22;
            altBarTrack.style.minHeight = 22;
            altBarTrack.style.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 0.8f);
            altBarTrack.style.borderTopLeftRadius = 3;
            altBarTrack.style.borderTopRightRadius = 3;
            altBarTrack.style.borderBottomLeftRadius = 3;
            altBarTrack.style.borderBottomRightRadius = 3;
            altBarTrack.style.overflow = Overflow.Hidden;
            altBarTrack.style.flexShrink = 0;
            altBarTrack.style.marginLeft = 4;

            _altBarFill = new VisualElement { name = "alt-bar-fill" };
            _altBarFill.style.width = Length.Percent(100);
            _altBarFill.style.height = Length.Percent(0);
            _altBarFill.style.backgroundColor = new Color(0.31f, 0.78f, 0.47f);
            _altBarFill.style.alignSelf = Align.FlexEnd; // растёт СНИЗУ вверх
            altBarTrack.Add(_altBarFill);
            altRow.Add(altBarTrack);

            _colEnv.Add(altRow);
        }

        private void UpdateEnvColumn(ProjectC.Player.ShipController ship)
        {
            if (_windSpeedLabel == null || ship == null) return;

            // === WIND ===
            float speed = 0f;
            Vector3 windDir = Vector3.forward;
            var wm = ProjectC.Core.WindManager.Instance;
            if (wm != null)
            {
                speed = wm.CurrentWindSpeed;
                windDir = wm.CurrentWindDirection;
            }
            _windSpeedLabel.text = $"{speed:F1} м/с";

            // Компас: угол ветра относительно носа корабля
            float compassAngle = 0f;
            if (wm != null && speed > 0.5f)
            {
                Vector3 shipForward = ship.transform.forward;
                shipForward.y = 0; // проекция на горизонталь
                if (shipForward.sqrMagnitude > 0.001f)
                {
                    Vector3 windFlat = windDir;
                    windFlat.y = 0;
                    if (windFlat.sqrMagnitude > 0.001f)
                    {
                        compassAngle = Vector3.SignedAngle(shipForward.normalized, windFlat.normalized, Vector3.up);
                    }
                }
            }

            // Стрелка компаса — текстовая
            string arrow;
            float absAng = Mathf.Abs(compassAngle);
            if (absAng < 22.5f) arrow = "↑";
            else if (absAng < 67.5f) arrow = compassAngle > 0 ? "↗" : "↖";
            else if (absAng < 112.5f) arrow = compassAngle > 0 ? "→" : "←";
            else if (absAng < 157.5f) arrow = compassAngle > 0 ? "↘" : "↙";
            else arrow = "↓";
            // Находим label направления (первый child windLeft)
            var windRow = _colEnv?.Q("env-wind");
            var windDirLabel = windRow?.Q("wind-dir-label") as Label;
            if (windDirLabel != null) windDirLabel.text = arrow;

            // Компас repaint только при изменении угла > 1°
            if (Mathf.Abs(compassAngle - _lastCompassAngle) > 1f)
            {
                _lastCompassAngle = compassAngle;
                _windCompass?.MarkDirtyRepaint();
            }

            // === ALTITUDE ===
            float alt = ship.transform.position.y;
            string altStr = alt >= 10000f ? $"{alt / 1000f:F1}k" : $"{alt:F0}";
            _altValueLabel.text = $"{altStr} м";

            string corrName = "---";
            float fillPct = 0f;
            Color fillColor = new Color(0.31f, 0.78f, 0.47f);

            var corridor = ship.ActiveCorridor;
            if (corridor != null)
            {
                corrName = corridor.displayName;
                float range = corridor.maxAltitude - corridor.minAltitude;
                if (range > 1f)
                {
                    // Позиция в коридоре: 0 = min, 1 = max
                    float pos = (alt - corridor.minAltitude) / range;
                    fillPct = Mathf.Clamp01(pos) * 100f;
                }

                // Цвет по статусу
                var status = ship.CurrentAltitudeStatus;
                switch (status)
                {
                    case AltitudeStatus.WarningLower:
                    case AltitudeStatus.WarningUpper:
                        fillColor = new Color(0.94f, 0.78f, 0.31f);
                        break;
                    case AltitudeStatus.DangerLower:
                    case AltitudeStatus.DangerUpper:
                        fillColor = new Color(0.86f, 0.31f, 0.31f);
                        break;
                    default:
                        fillColor = new Color(0.31f, 0.78f, 0.47f);
                        break;
                }
            }

            _altCorridorLabel.text = corrName;
            _altBarFill.style.height = Length.Percent(fillPct);
            _altBarFill.style.backgroundColor = fillColor;
        }

        // ==================== S-HUD-03f: Dispatch (K5 — docking system) ====================

        // T-DOCK-HUD-1: ссылки на labels для обновления в Update()
        private Label _dispatchDot;   // ● — индикатор: красный/зелёный
        private Label _dispatcherLabel;
        private Label _regionLabel;
        private Label _corridorLabel;

        private void BuildDispatchColumn()
        {
            if (_colDispatch == null) return;

            var hdr = new Label { name = "dispatch-header", text = "DISPATCH" };
            hdr.style.fontSize = 8;
            hdr.style.color = new Color(1, 1, 1, 0.6f);
            hdr.style.marginBottom = 6;
            hdr.style.marginLeft = 2;
            _colDispatch.Add(hdr);

            // T-DOCK-HUD-1: 3 строки, по макету из docs/Ships/UI/HUD/00_OVERVIEW.md §3.6,
            // но живые — Dispatcher (●), Region (station id), Corridor (comm range).
            // Когда игрок в OuterCommZone:
            //   • ● зелёный
            //   • DISPATCHER: stationId
            //   • REGION: "T — связь" (подсказка нажать T)
            // Вне зоны:
            //   • ● красный
            //   • DISPATCHER: "---"
            //   • REGION: "---"

            _dispatchDot = new Label { text = "●" };
            _dispatchDot.style.fontSize = 10;
            _dispatchDot.style.color = new Color(1f, 0.3f, 0.3f, 0.9f); // красный = не в зоне
            _dispatchDot.style.unityFontStyleAndWeight = FontStyle.Bold;
            _dispatchDot.style.marginBottom = 2;
            _dispatchDot.style.marginLeft = 2;
            _colDispatch.Add(_dispatchDot);

            _dispatcherLabel = new Label { text = "DISPATCHER  ---" };
            _dispatcherLabel.style.fontSize = 9;
            _dispatcherLabel.style.color = new Color(1, 1, 1, 0.5f);
            _dispatcherLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _dispatcherLabel.style.marginBottom = 2;
            _dispatcherLabel.style.marginLeft = 2;
            _colDispatch.Add(_dispatcherLabel);

            _regionLabel = new Label { text = "REGION      ---" };
            _regionLabel.style.fontSize = 9;
            _regionLabel.style.color = new Color(1, 1, 1, 0.5f);
            _regionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _regionLabel.style.marginBottom = 2;
            _regionLabel.style.marginLeft = 2;
            _colDispatch.Add(_regionLabel);

            _corridorLabel = new Label { text = "CORRIDOR    ---" };
            _corridorLabel.style.fontSize = 9;
            _corridorLabel.style.color = new Color(1, 1, 1, 0.5f);
            _corridorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _corridorLabel.style.marginBottom = 2;
            _corridorLabel.style.marginLeft = 2;
            _colDispatch.Add(_corridorLabel);
        }

        /// <summary>
        /// T-DOCK-HUD-2: обновляет K5 (Dispatch) — показывает состояние связи с
        /// OuterCommZone + station id + подсказку нажать T.
        /// </summary>
        private void UpdateDispatchColumn()
        {
            if (_dispatchDot == null || _dispatcherLabel == null) return;

            // Берём ближайшую станцию — LocalPlayerShipStation (для игрока в корабле)
            // или LocalPlayerStation (для пешего).
            var station = ProjectC.Docking.Network.DockingZoneRegistry.LocalPlayerShipStation
                          ?? ProjectC.Docking.Network.DockingZoneRegistry.LocalPlayerStation;

            if (station != null && !string.IsNullOrEmpty(station.StationId))
            {
                // В зоне — зелёная точка
                _dispatchDot.style.color = new Color(0.3f, 1f, 0.4f, 1f);
                _dispatcherLabel.text = "DISPATCHER  " + station.StationId;
                _dispatcherLabel.style.color = new Color(0.3f, 1f, 0.4f, 0.95f);
                _regionLabel.text = "REGION      " + (station.DisplayName ?? "");
                _regionLabel.style.color = new Color(1, 1, 1, 0.75f);
                _corridorLabel.text = "T — связаться";
                _corridorLabel.style.color = new Color(1, 1, 1, 0.75f);
            }
            else
            {
                // Вне зоны — красная точка
                _dispatchDot.style.color = new Color(1f, 0.3f, 0.3f, 0.9f);
                _dispatcherLabel.text = "DISPATCHER  ---";
                _dispatcherLabel.style.color = new Color(1, 1, 1, 0.4f);
                _regionLabel.text = "REGION      ---";
                _regionLabel.style.color = new Color(1, 1, 1, 0.4f);
                _corridorLabel.text = "CORRIDOR    ---";
                _corridorLabel.style.color = new Color(1, 1, 1, 0.4f);
            }
        }

        /// <summary>
        /// Callback для рендеринга мини-компаса через Painter2D (UI Toolkit).
        /// </summary>
        private void OnCompassGenerateContent(MeshGenerationContext ctx)
        {
            var rect = _windCompass?.contentRect ?? new Rect(0, 0, 28, 28);
            var painter = ctx.painter2D;

            // Фон круга
            painter.fillColor = new Color(0.06f, 0.08f, 0.12f, 0.9f);
            painter.BeginPath();
            painter.Arc(new Vector2(rect.width / 2, rect.height / 2), rect.width / 2 - 1, 0, 360);
            painter.Fill();

            // Обводка
            painter.strokeColor = new Color(0.4f, 0.4f, 0.5f, 0.6f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.Arc(new Vector2(rect.width / 2, rect.height / 2), rect.width / 2 - 1, 0, 360);
            painter.Stroke();

            // Стрелка ветра
            float angleRad = _lastCompassAngle * Mathf.Deg2Rad;
            float cx = rect.width / 2;
            float cy = rect.height / 2;
            float radius = rect.width / 2 - 4;

            float ex = cx + Mathf.Sin(angleRad) * radius;
            float ey = cy - Mathf.Cos(angleRad) * radius;

            painter.strokeColor = new Color(0.86f, 0.31f, 0.31f, 0.9f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx, cy));
            painter.LineTo(new Vector2(ex, ey));
            painter.Stroke();

            // Маленький кружок в центре
            painter.fillColor = new Color(0.86f, 0.31f, 0.31f, 0.6f);
            painter.BeginPath();
            painter.Arc(new Vector2(cx, cy), 1.5f, 0, 360);
            painter.Fill();
        }
    }
}
