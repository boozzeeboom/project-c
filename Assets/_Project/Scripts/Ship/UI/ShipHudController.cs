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
        [SerializeField] private float _rootHeight = 96f;

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

        // LocalPlayer (кешируем после нахождения; null если не найден)
        private ProjectC.Player.NetworkPlayer _localPlayer;

        private void Awake()
        {
            // S-HUD-03a: cache UIDocument, fallback PanelSettings, persist scene
            if (_doc == null) _doc = GetComponent<UIDocument>();

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
                if (!_built) return; // guards не прошли — выходим, попробуем на следующем кадре
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
            if (shouldShow != _wasShown) SetVisible(shouldShow);
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
            if (_doc == null) return;
            if (_doc.rootVisualElement == null) return;
            if (_doc.panelSettings == null) return;

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

            // 5 пустых колонок-плейсхолдеров (наполняются в S-HUD-03b..f)
            _colModules   = MakeColumn("col-modules",   240f, flexShrink: 1f);
            _colFlight    = MakeColumn("col-flight",    200f, flexShrink: 0f);  // критические данные — не сжимаем
            _colSpeed     = MakeColumn("col-speed",     180f, flexShrink: 0f);
            _colEnv       = MakeColumn("col-env",       220f, flexShrink: 1f);
            _colDispatch  = MakeColumn("col-dispatch",  200f, flexShrink: 1f);

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
            // S-HUD-03a: пока пусто. Колонки-плейсхолдеры рендерятся, но без данных.
        }
    }
}
