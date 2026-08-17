using System;
using ProjectC.Localization;
using ProjectC.Trade.Core;
using ProjectC.Trade.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// Общий host для окна рынка.
    /// Отвечает только за жизненный цикл UIDocument, общую шапку, навигацию,
    /// modal visibility и shared feedback. Логика вкладок находится в отдельных
    /// контроллерах.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MarketWindowHost : MonoBehaviour
    {
        [Header("UI Assets (можно Resources fallback)")]
        [SerializeField] protected VisualTreeAsset marketWindowUxml;
        [SerializeField] protected StyleSheet marketWindowUss;

        [Header("Behavior")]
        [SerializeField] protected bool visibleOnStart = false;

        protected UIDocument _doc;
        protected VisualElement _root;
        protected VisualElement _mainContainer;
        protected Label _locationLabel;
        protected Label _creditsLabel;
        protected Label _warehouseInfoLabel;
        protected Label _timeInfoLabel;
        protected Label _messageLabel;

        protected MarketTabController _marketTab;
        protected ContractsMarketTabController _contractsTab;
        protected ExchangeTabController _exchangeTab;

        private bool _built;
        private bool _visibilityInitialized;
        private bool _visible;
        private string _activeTab = "market";

        protected bool LifecycleDisabled { get; private set; }
        public string ActiveTab => _activeTab;
        public bool IsWindowVisible => _visible;
        public MarketClientState MarketState => _marketTab != null ? _marketTab.State : MarketClientState.Instance;
        public string CurrentLocationId => _marketTab != null && !string.IsNullOrEmpty(_marketTab.CurrentLocationId)
            ? _marketTab.CurrentLocationId
            : MarketZoneRegistry.LocalPlayerZone != null ? MarketZoneRegistry.LocalPlayerZone.LocationId : null;

        protected virtual void AwakeWindow()
        {
        }

        protected virtual void DestroyWindow()
        {
        }

        protected void DisableLifecycle()
        {
            LifecycleDisabled = true;
        }

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            AwakeWindow();
        }

        private void OnEnable()
        {
            if (LifecycleDisabled) return;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null)
            {
                Debug.LogError("[MarketWindowHost] нет UIDocument на GameObject");
                return;
            }

            EnsureBuilt();
            SubscribeControllers();
        }

        private void Start()
        {
            if (LifecycleDisabled) return;
            if (!_built || !IsLayoutValid())
            {
                Debug.LogWarning("[MarketWindowHost] Start(): layout invalid, rebuilding");
                EnsureBuilt();
                SubscribeControllers();
            }
        }

        private void OnDisable()
        {
            UnsubscribeControllers();
        }

        private void OnDestroy()
        {
            UnsubscribeControllers();
            DestroyWindow();
        }

        private void Update()
        {
            if (LifecycleDisabled) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && IsVisible())
            {
                Hide();
            }
        }

        private bool IsLayoutValid()
        {
            return _built && _root != null && _mainContainer != null;
        }

        private void EnsureBuilt()
        {
            if (_doc == null || _doc.rootVisualElement == null) return;
            if (IsLayoutValid()) return;

            UnsubscribeControllers();
            _built = false;

            if (marketWindowUxml == null)
                marketWindowUxml = Resources.Load<VisualTreeAsset>("UI/MarketWindow");
            if (marketWindowUss == null)
                marketWindowUss = Resources.Load<StyleSheet>("UI/MarketWindow");
            if (marketWindowUxml == null)
            {
                Debug.LogError("[MarketWindowHost] UXML не найден в Resources/UI/");
                return;
            }

            _doc.rootVisualElement.Clear();
            if (marketWindowUss != null)
                _doc.rootVisualElement.styleSheets.Add(marketWindowUss);

            _root = marketWindowUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            _mainContainer = _root.Q<VisualElement>("main-container");
            _locationLabel = _root.Q<Label>("location-label");
            _creditsLabel = _root.Q<Label>("credits-label");
            _warehouseInfoLabel = _root.Q<Label>("warehouse-info-label");
            _timeInfoLabel = _root.Q<Label>("time-info-label");
            _messageLabel = _root.Q<Label>("message-label");

            ConfigureSharedUi();

            _marketTab = new MarketTabController(this);
            _marketTab.BuildUI(_root, _locationLabel, _creditsLabel, _warehouseInfoLabel, _timeInfoLabel, _messageLabel);

            _contractsTab = new ContractsMarketTabController(this);
            _contractsTab.BuildUI(_root, _creditsLabel, _messageLabel);

            _exchangeTab = new ExchangeTabController(this, _messageLabel);
            _exchangeTab.BuildUI(_root);

            _built = true;
            if (!_visibilityInitialized)
            {
                _visibilityInitialized = true;
                _visible = visibleOnStart;
            }

            SwitchTab(_activeTab);
            SetVisible(_visible);
            _doc.rootVisualElement.MarkDirtyRepaint();
            Debug.Log($"[MarketWindowHost] Built: root.children={_doc.rootVisualElement.childCount}, styleSheets={_doc.rootVisualElement.styleSheets.count}");
        }

        private void ConfigureSharedUi()
        {
            if (_messageLabel != null)
                _messageLabel.text = Loc.Get("ui.market.label.welcome");

            var marketTabButton = _root.Q<Button>("tab-market");
            var warehouseTabButton = _root.Q<Button>("tab-warehouse");
            var contractsTabButton = _root.Q<Button>("tab-contracts");
            var exchangeTabButton = _root.Q<Button>("tab-exchanger");
            var closeButton = _root.Q<Button>("close-btn");

            if (marketTabButton != null)
            {
                marketTabButton.text = Loc.Get("ui.market.tab.market");
                marketTabButton.clicked += () => SwitchTab("market");
            }
            if (warehouseTabButton != null)
            {
                warehouseTabButton.text = Loc.Get("ui.market.tab.warehouse");
                warehouseTabButton.clicked += () => SwitchTab("warehouse");
            }
            if (contractsTabButton != null)
            {
                contractsTabButton.text = Loc.Get("ui.market.tab.contracts");
                contractsTabButton.clicked += () => SwitchTab("contracts");
            }
            if (exchangeTabButton != null)
            {
                exchangeTabButton.text = Loc.Get("ui.market.tab.exchanger");
                exchangeTabButton.clicked += () => SwitchTab("exchange");
            }
            if (closeButton != null)
            {
                closeButton.text = Loc.Get("ui.market.btn.close");
                closeButton.clicked += Hide;
            }

            var sectionTitle = _root.Q<VisualElement>("item-section")?.Q<Label>(className: "section-title");
            if (sectionTitle != null) sectionTitle.text = Loc.Get("ui.market.section.items");
            sectionTitle = _root.Q<VisualElement>("warehouse-section")?.Q<Label>(className: "section-title");
            if (sectionTitle != null) sectionTitle.text = Loc.Get("ui.market.section.warehouse");
            sectionTitle = _root.Q<VisualElement>("cargo-section")?.Q<Label>(className: "section-title");
            if (sectionTitle != null) sectionTitle.text = Loc.Get("ui.market.section.cargo");
            sectionTitle = _root.Q<VisualElement>("contracts-section")?.Q<Label>(className: "section-title");
            if (sectionTitle != null) sectionTitle.text = Loc.Get("ui.market.section.contracts");
            sectionTitle = _root.Q<VisualElement>("exchange-section")?.Q<Label>(className: "section-title");
            if (sectionTitle != null) sectionTitle.text = Loc.Get("ui.market.section.exchange");

            var qtyLabels = _root.Query<Label>(className: "qty-label").ToList();
            foreach (var label in qtyLabels) label.text = Loc.Get("ui.market.label.qty");

            var shipLabel = _root.Q<VisualElement>("ship-selector-container")?.Q<Label>(className: "ship-selector-label");
            if (shipLabel != null) shipLabel.text = Loc.Get("ui.market.label.ship");

            var exchangeTitles = _root.Q<VisualElement>("exchange-section")?.Query<Label>(className: "exchange-panel-title").ToList();
            if (exchangeTitles != null && exchangeTitles.Count >= 2)
            {
                exchangeTitles[0].text = Loc.Get("ui.market.exchange.inventory");
                exchangeTitles[1].text = Loc.Get("ui.market.exchange.warehouse");
            }
        }

        private void SubscribeControllers()
        {
            if (!_built) return;
            _marketTab?.Subscribe();
            _contractsTab?.Subscribe();
            _exchangeTab?.Subscribe();
        }

        private void UnsubscribeControllers()
        {
            _marketTab?.Unsubscribe();
            _contractsTab?.Unsubscribe();
            _exchangeTab?.Unsubscribe();
        }

        public void SwitchTab(string tab)
        {
            if (string.IsNullOrEmpty(tab)) tab = "market";
            _activeTab = tab;

            _marketTab?.SetTabVisible(tab);
            _contractsTab?.SetTabVisible(tab == "contracts");
            _exchangeTab?.SetTabVisible(tab == "exchange");
        }

        internal void SetMessage(string message, bool isError = false)
        {
            if (_messageLabel == null) return;
            _messageLabel.text = message;
            _messageLabel.style.color = isError
                ? new StyleColor(new Color(0.95f, 0.4f, 0.4f))
                : new StyleColor(new Color(0.9f, 0.9f, 0.9f));
        }

        internal void RequestMarketRefresh()
        {
            var state = MarketState ?? MarketClientState.Instance;
            var locationId = state != null ? state.CurrentLocationId : null;
            if (string.IsNullOrEmpty(locationId)) locationId = CurrentLocationId;
            if (state != null && !string.IsNullOrEmpty(locationId))
                state.RequestSubscribeMarket(locationId);
        }

        internal void RequestInventoryRefresh()
        {
            var inventoryState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (inventoryState != null) inventoryState.RequestRefresh();
        }

        private void SetVisible(bool value)
        {
            if (_mainContainer == null) _mainContainer = _root?.Q<VisualElement>("main-container");
            if (_mainContainer != null)
            {
                _mainContainer.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                if (value) ApplyInlineFallbackStyles(_mainContainer);
            }

            _visible = value;
            if (value)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else
            {
                var networkManager = Unity.Netcode.NetworkManager.Singleton;
                if (networkManager != null && networkManager.IsListening)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }

        private static void ApplyInlineFallbackStyles(VisualElement main)
        {
            main.style.position = Position.Absolute;
            main.style.top = new Length(5, LengthUnit.Percent);
            main.style.left = new Length(50, LengthUnit.Percent);
            main.style.translate = new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), 0));
            main.style.width = 640;
            main.style.maxWidth = new Length(90, LengthUnit.Percent);
            main.style.maxHeight = new Length(90, LengthUnit.Percent);
            main.style.backgroundColor = new Color(0.078f, 0.098f, 0.137f, 0.95f);
            main.style.borderTopWidth = 2;
            main.style.borderRightWidth = 2;
            main.style.borderBottomWidth = 2;
            main.style.borderLeftWidth = 2;
            main.style.borderTopColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderRightColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderBottomColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderLeftColor = new Color(0.471f, 0.588f, 0.784f, 0.8f);
            main.style.borderTopLeftRadius = 8;
            main.style.borderTopRightRadius = 8;
            main.style.borderBottomLeftRadius = 8;
            main.style.borderBottomRightRadius = 8;
            main.style.paddingTop = 12;
            main.style.paddingRight = 12;
            main.style.paddingBottom = 12;
            main.style.paddingLeft = 12;
            main.style.color = new Color(0.863f, 0.863f, 0.902f);
            main.style.fontSize = 14;
            main.style.flexDirection = FlexDirection.Column;
            main.style.alignItems = Align.Stretch;
        }

        public void Toggle()
        {
            if (!_built || !IsLayoutValid()) EnsureBuilt();
            SetVisible(!_visible);
        }

        public void Show()
        {
            if (!_built || !IsLayoutValid())
            {
                EnsureBuilt();
                SubscribeControllers();
            }
            if (_root != null) _root.pickingMode = PickingMode.Position;
            SetVisible(true);

            if (MarketState == null || !MarketState.CurrentSnapshot.HasValue)
                SetMessage(Loc.Get("ui.market.loading"));

            _doc?.rootVisualElement?.MarkDirtyRepaint();
            if (_doc?.rootVisualElement != null)
                _doc.rootVisualElement.schedule.Execute(() => _doc.rootVisualElement.MarkDirtyRepaint()).StartingIn(50);

            RequestInventoryRefresh();
            RequestMarketRefresh();
            _exchangeTab?.RefreshData();
        }

        public void Hide()
        {
            if (_root != null) _root.pickingMode = PickingMode.Ignore;
            SetVisible(false);
        }

        public bool IsVisible()
        {
            return _visible && _mainContainer != null && _mainContainer.style.display == DisplayStyle.Flex;
        }
    }
}
