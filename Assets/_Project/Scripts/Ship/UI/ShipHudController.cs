using UnityEngine;
using UnityEngine.UIElements;

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

        // S-HUD-03c: Flight column (K2) — 4 строки LIFT/TURN/PITCH/BANK
        // Каждый элемент — массив [lift, turn, pitch, bank]
        private Label[] _flightLabels;     // левая часть "LIFT" / "TURN" / ...
        private Label[] _flightValues;     // правая "+1.2" / "30°/s" / ...
        private VisualElement[] _flightNegFill;  // bar левый сегмент (отрицательный)
        private VisualElement[] _flightPosFill;  // bar правый сегмент (положительный)

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
                // Positive: right fill visible, left fill 0
                _flightNegFill[idx].style.width = Length.Percent(0);
                _flightPosFill[idx].style.width = Length.Percent(pct * 50f);
            }
            else
            {
                // Negative: left fill visible, right fill 0
                _flightNegFill[idx].style.width = Length.Percent(Mathf.Abs(pct) * 50f);
                _flightPosFill[idx].style.width = Length.Percent(0);
            }
        }
    }
}
