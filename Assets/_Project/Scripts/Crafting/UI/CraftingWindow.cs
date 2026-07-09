// CraftingWindow.cs (T-C06) - player-facing UIDocument window.
// Pattern: DialogWindow (T-Q11c). Subscribes to CraftingClientState events.
//   - OnSnapshotUpdated -> rebuild UI (recipe list, buffer, progress, buttons)
//   - OnCraftingDenied -> show message in MessageLabel
// MVP: no drag-and-drop. +1/+All buttons рядом с каждым ингредиентом.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Crafting.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class CraftingWindow : MonoBehaviour
    {
        public static CraftingWindow Instance { get; private set; }

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset craftingWindowUxml;
        [SerializeField] private StyleSheet craftingWindowUss;
        [SerializeField] private PanelSettings craftingPanelSettings;

        // runtime refs
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _panel;
        private Label _stationNameLabel;
        private Label _recipeTitleLabel;
        private Label _recipeDescLabel;
        private VisualElement _ingredientsContainer;
        private VisualElement _bufferGrid;
        private ProgressBar _progressBar;
        private Label _messageLabel;
        private Button _startBtn;
        private Button _cancelBtn;
        private Button _collectBtn;
        private Button _closeBtn;
        private ListView _recipeList;

        // state
        private ulong _currentStationNetId;
        private CraftingStationConfig _currentConfig;
        private int _selectedRecipeId = -1;
        private bool _built;
        private bool _subscribed;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            // Resources fallback
            if (craftingWindowUxml == null) craftingWindowUxml = Resources.Load<VisualTreeAsset>("UI/CraftingWindow");
            if (craftingWindowUss == null) craftingWindowUss = Resources.Load<StyleSheet>("UI/CraftingWindow");
            if (craftingPanelSettings == null) craftingPanelSettings = Resources.Load<PanelSettings>("UI/CraftingPanelSettings");
        }

        private void OnEnable()
        {
            EnsureBuilt();
            TrySubscribe();
        }

        private void Start() { EnsureBuilt(); }

        private void OnDisable() { /* nothing — subscriptions are event-based */ }

        private void OnDestroy()
        {
            TryUnsubscribe();
            if (Instance == this) Instance = null;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            var state = CraftingClientState.Instance;
            if (state == null) return;
            state.OnSnapshotUpdated += HandleSnapshotUpdated;
            state.OnCraftingProgress += HandleProgress;
            state.OnCraftingCompleted += HandleCompleted;
            state.OnCraftingDenied += HandleDenied;
            state.OnCraftingCancelled += HandleCancelled;
            state.OnCraftingInterrupted += HandleInterrupted;
            _subscribed = true;
        }

        private void TryUnsubscribe()
        {
            if (!_subscribed) return;
            var state = CraftingClientState.Instance;
            if (state == null) { _subscribed = false; return; }
            state.OnSnapshotUpdated -= HandleSnapshotUpdated;
            state.OnCraftingProgress -= HandleProgress;
            state.OnCraftingCompleted -= HandleCompleted;
            state.OnCraftingDenied -= HandleDenied;
            state.OnCraftingCancelled -= HandleCancelled;
            state.OnCraftingInterrupted -= HandleInterrupted;
            _subscribed = false;
        }

        private void Update()
        {
            if (!_built) EnsureBuilt();
            if (!_subscribed) TrySubscribe();
            // T-C06: ESC закрывает окно
            if (IsOpen && UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void EnsureBuilt()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null) return;
            if (_doc.rootVisualElement == null) return;
            if (craftingWindowUxml == null)
            {
                craftingWindowUxml = Resources.Load<VisualTreeAsset>("UI/CraftingWindow");
                if (craftingWindowUxml == null) return;
            }
            if (craftingWindowUss == null) craftingWindowUss = Resources.Load<StyleSheet>("UI/CraftingWindow");
            if (craftingPanelSettings == null) craftingPanelSettings = Resources.Load<PanelSettings>("UI/CraftingPanelSettings");

            // Auto-bind PanelSettings (как DialogWindow делает)
            if (craftingPanelSettings != null && _doc.panelSettings == null)
            {
                _doc.panelSettings = craftingPanelSettings;
            }

            _doc.rootVisualElement.Clear();
            if (craftingWindowUss != null) _doc.rootVisualElement.styleSheets.Add(craftingWindowUss);

            _root = craftingWindowUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore; // пока скрыто — клики пробрасываются в game
            _doc.rootVisualElement.Add(_root);

            _panel = _root.Q<VisualElement>("crafting-panel");
            _stationNameLabel = _root.Q<Label>("station-name");
            _recipeTitleLabel = _root.Q<Label>("recipe-title");
            _recipeDescLabel = _root.Q<Label>("recipe-description");
            _ingredientsContainer = _root.Q<VisualElement>("ingredients-container");
            _bufferGrid = _root.Q<VisualElement>("buffer-grid");
            _progressBar = _root.Q<ProgressBar>("crafting-progress-bar");
            _messageLabel = _root.Q<Label>("message-label");
            _startBtn = _root.Q<Button>("start-btn");
            _cancelBtn = _root.Q<Button>("cancel-btn");
            _collectBtn = _root.Q<Button>("collect-btn");
            _closeBtn = _root.Q<Button>("close-btn");
            _recipeList = _root.Q<ListView>("recipe-list");

            if (_closeBtn != null) _closeBtn.clicked += OnCloseClicked;
            if (_startBtn != null) _startBtn.clicked += OnStartClicked;
            if (_cancelBtn != null) _cancelBtn.clicked += OnCancelClicked;
            if (_collectBtn != null) _collectBtn.clicked += OnCollectClicked;

            // Initial hidden
            if (_root != null) _root.style.display = DisplayStyle.None;
            _built = true;
        }

        // ==========================================================
        // Public API (вызывается из NetworkPlayer/ClientState)
        // ==========================================================
        public void Show(ulong stationNetId, CraftingStationConfig config)
        {
            if (!_built) EnsureBuilt();
            if (_root == null) return;
            _currentStationNetId = stationNetId;
            _currentConfig = config;
            if (_stationNameLabel != null) _stationNameLabel.text = config != null ? config.DisplayName : "Станция";
            BuildRecipeList();
            if (_messageLabel != null) _messageLabel.text = "";
            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
                _root.pickingMode = PickingMode.Position;
            }
            // Cursor unlock
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            IsOpen = true;
        }

        public void Close()
        {
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
                _root.pickingMode = PickingMode.Ignore;
            }
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
            // FIX T-C07: НЕ отписываемся — подписка живёт пока станция активна.
            // Тост прогресса продолжает получать snapshot'ы даже при закрытом окне.
            _currentStationNetId = 0;
            _currentConfig = null;
            _selectedRecipeId = -1;
            IsOpen = false;
        }

        /// <summary>T-C07: переключиться на другую станцию (окно уже открыто). Не отписывается от старой.</summary>
        public void SwitchStation(ulong newStationNetId, CraftingStationConfig newConfig)
        {
            if (!_built) EnsureBuilt();
            if (_root == null) return;

            // Unsubscribe old — НЕ делаем, подписка остаётся активной для тоста прогресса.
            // Просто переключаем отображение окна на новую станцию.
            // Если новая станция ещё не подписана — снапшот придёт, когда клиент вызовет RequestSubscribe.
            // А RequestSubscribe вызывается из NetworkPlayer.TryInteractNearestCraftingStation ПОСЛЕ SwitchStation.

            _currentStationNetId = newStationNetId;
            _currentConfig = newConfig;
            _selectedRecipeId = -1;

            // Reset UI
            if (_stationNameLabel != null) _stationNameLabel.text = newConfig != null ? newConfig.DisplayName : "Станция";
            if (_recipeTitleLabel != null) _recipeTitleLabel.text = "Выберите рецепт";
            if (_recipeDescLabel != null) _recipeDescLabel.text = "";
            if (_progressBar != null) _progressBar.value = 0f;
            if (_messageLabel != null) _messageLabel.text = "Станция переключена";
            if (_ingredientsContainer != null) _ingredientsContainer.Clear();
            if (_bufferGrid != null) _bufferGrid.Clear();
            if (_recipeList != null) _recipeList.Clear();

            BuildRecipeList();

            // Show если скрыто
            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
                _root.pickingMode = PickingMode.Position;
            }
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            IsOpen = true;
        }

        // ==========================================================
        // UI Builders
        // ==========================================================
        private void BuildRecipeList()
        {
            if (_recipeList == null) return;
            _recipeList.Clear();
            _recipeList.itemsSource = GetRecipeDisplayList();
            _recipeList.makeItem = () =>
            {
                var lbl = new Label();
                lbl.AddToClassList("crafting-recipe-item");
                lbl.style.color = new StyleColor(new Color(0.86f, 0.82f, 0.74f));
                lbl.style.paddingTop = 4; lbl.style.paddingBottom = 4; lbl.style.paddingLeft = 8; lbl.style.paddingRight = 8;
                lbl.style.fontSize = 13;
                lbl.pickingMode = PickingMode.Position;
                return lbl;
            };
            _recipeList.bindItem = (el, i) =>
            {
                var items = GetRecipeDisplayList();
                if (i < 0 || i >= items.Count) return;
                var pair = items[i];
                (el as Label).text = pair.Value;
            };
            _recipeList.selectionType = SelectionType.Single;
            _recipeList.fixedItemHeight = 28;
            _recipeList.selectionChanged += OnRecipeSelected;
        }

        private List<KeyValuePair<int, string>> GetRecipeDisplayList()
        {
            var list = new List<KeyValuePair<int, string>>();
            if (_currentConfig == null) return list;
            for (int i = 0; i < _currentConfig.AllowedRecipes.Count; i++)
            {
                var r = _currentConfig.AllowedRecipes[i];
                if (r == null) continue;
                int recipeId = CraftingWorld.RegisterRecipe(r);
                string displayName = CraftingClientState.Instance != null
                    ? CraftingClientState.Instance.GetRecipeDisplayName(recipeId)
                    : r.DisplayName;
                list.Add(new KeyValuePair<int, string>(recipeId, $"{displayName} ({r.CraftSeconds:0.#}с)"));
            }
            return list;
        }

        private void OnRecipeSelected(IEnumerable<object> sel)
        {
            foreach (var s in sel)
            {
                if (s is KeyValuePair<int, string> pair)
                {
                    _selectedRecipeId = pair.Key;
                    var recipe = CraftingClientState.Instance != null
                        ? CraftingClientState.Instance.GetRecipe(_selectedRecipeId)
                        : CraftingWorld.GetRecipe(_selectedRecipeId);
                    if (recipe != null) BuildIngredientsPanel(recipe);
                    return;
                }
            }
        }

        private void BuildIngredientsPanel(RecipeData recipe)
        {
            if (_ingredientsContainer == null || _recipeTitleLabel == null) return;
            _ingredientsContainer.Clear();
            _recipeTitleLabel.text = recipe.DisplayName;
            _recipeDescLabel.text = recipe.Description ?? "";

            foreach (var ing in recipe.Ingredients)
            {
                var row = new VisualElement();
                row.AddToClassList("crafting-ingredient-row");

                var nameLbl = new Label { text = ing.item != null ? ing.item.itemName : "?", pickingMode = PickingMode.Ignore };
                nameLbl.AddToClassList("crafting-ingredient-name");

                var qtyLbl = new Label { text = "× " + ing.quantity, pickingMode = PickingMode.Ignore };
                qtyLbl.AddToClassList("crafting-ingredient-qty");

                var plusBtn = new Button(() => OnAddIngredientClicked(ing.item, ing.quantity)) { text = "+1" };
                plusBtn.AddToClassList("crafting-btn");
                plusBtn.AddToClassList("crafting-btn-secondary");
                plusBtn.style.minWidth = 48;
                plusBtn.style.height = 24;
                plusBtn.style.fontSize = 12;

                var allBtn = new Button(() => OnAddIngredientClicked(ing.item, int.MaxValue)) { text = "Все" };
                allBtn.AddToClassList("crafting-btn");
                allBtn.AddToClassList("crafting-btn-secondary");
                allBtn.style.minWidth = 48;
                allBtn.style.height = 24;
                allBtn.style.fontSize = 12;

                row.Add(nameLbl);
                row.Add(qtyLbl);
                row.Add(plusBtn);
                row.Add(allBtn);
                _ingredientsContainer.Add(row);
            }
        }

        private void OnAddIngredientClicked(ProjectC.Items.ItemData item, int qty)
        {
            if (item == null) return;
            // T3: используем CraftingClientState для получения itemId (InventoryWorld на клиенте)
            if (CraftingClientState.Instance == null) return;
            int itemId = CraftingClientState.Instance.GetItemId(item);
            if (itemId < 0) return;
            if (_currentStationNetId == 0) return;
            CraftingClientState.Instance?.RequestAddIngredient(_currentStationNetId, itemId, qty);
            if (_messageLabel != null) _messageLabel.text = $"Добавлено: {item.itemName} × {qty}";
        }

        private void BuildBufferPanel(CraftingSnapshotDto snap)
        {
            if (_bufferGrid == null) return;
            _bufferGrid.Clear();
            if (snap.buffer == null) return;
            for (int i = 0; i < snap.buffer.Length; i++)
            {
                var b = snap.buffer[i];
                // T3: используем CraftingClientState (InventoryWorld через него)
                var item = CraftingClientState.Instance?.GetItem(b.itemId);
                var row = new VisualElement();
                row.AddToClassList("crafting-buffer-row");
                var lbl = new Label { text = $"{item?.itemName ?? "?"} × {b.quantity}", pickingMode = PickingMode.Ignore };
                lbl.style.color = new StyleColor(new Color(0.8f, 1f, 0.8f));
                lbl.style.fontSize = 12;
                row.Add(lbl);
                _bufferGrid.Add(row);
            }
        }

        // ==========================================================
        // Event handlers
        // ==========================================================
        private void HandleSnapshotUpdated(CraftingSnapshotDto snap)
        {
            if (!_built) EnsureBuilt();
            if (snap.stationNetId != _currentStationNetId) return;
            BuildBufferPanel(snap);
            UpdateButtonsVisibility((CraftingJobState)snap.jobState);
            // FIX T-C07: показать что крафтится + прогресс
            var state = (CraftingJobState)snap.jobState;
            if (state == CraftingJobState.InProgress && _progressBar != null)
            {
                _progressBar.value = Mathf.Clamp01(snap.progress);
                if (_recipeTitleLabel != null && !string.IsNullOrEmpty(snap.resultItemName))
                    _recipeTitleLabel.text = snap.resultItemName;
            }
            // T-C07: прячем кнопки +1 в ингредиентах при InProgress (сервер блокирует добавление)
            if (_ingredientsContainer != null)
            {
                _ingredientsContainer.style.display = (state == CraftingJobState.InProgress)
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
            // Message при Completed
            if (state == CraftingJobState.Completed && _messageLabel != null)
            {
                _messageLabel.text = "✅ Готово: " + snap.resultItemName + " — нажмите «Забрать»";
            }
            // Message при Empty после Collect
            if (state == CraftingJobState.Empty && snap.activeRecipeId == -1 && _messageLabel != null)
            {
                _messageLabel.text = "Выберите рецепт и добавьте ингредиенты";
            }
        }

        private void HandleProgress(ulong stationNetId, float progress, string resultItemName)
        {
            if (stationNetId != _currentStationNetId) return;
            if (_progressBar != null) _progressBar.value = Mathf.Clamp01(progress);
            // FIX T-C07: обновляем название рецепта при каждом тике прогресса
            if (_recipeTitleLabel != null && !string.IsNullOrEmpty(resultItemName))
                _recipeTitleLabel.text = resultItemName;
        }

        private void HandleCompleted(ulong stationNetId, string resultItemName)
        {
            if (stationNetId != _currentStationNetId) return;
            if (_messageLabel != null) _messageLabel.text = "✅ Готово: " + resultItemName + " — нажмите «Забрать»";
            if (_progressBar != null) _progressBar.value = 1f;
        }

        private void HandleDenied(ulong stationNetId, string reason)
        {
            if (stationNetId != _currentStationNetId) return;
            if (_messageLabel != null) _messageLabel.text = "❌ " + (reason ?? "Отказано");
        }

        private void HandleCancelled(ulong stationNetId)
        {
            if (stationNetId != _currentStationNetId) return;
            if (_messageLabel != null) _messageLabel.text = "Крафт отменён";
            if (_progressBar != null) _progressBar.value = 0f;
        }

        private void HandleInterrupted(ulong stationNetId, string reason)
        {
            if (stationNetId != _currentStationNetId) return;
            if (_messageLabel != null) _messageLabel.text = "⚠ " + (reason ?? "Прервано");
            if (_progressBar != null) _progressBar.value = 0f;
        }

        private void UpdateButtonsVisibility(CraftingJobState state)
        {
            if (_startBtn == null || _cancelBtn == null || _collectBtn == null) return;
            switch (state)
            {
                case CraftingJobState.Empty:
                    _startBtn.style.display = DisplayStyle.Flex;
                    _cancelBtn.style.display = DisplayStyle.None;
                    _collectBtn.style.display = DisplayStyle.None;
                    _startBtn.SetEnabled(_selectedRecipeId >= 0);
                    break;
                case CraftingJobState.Buffered:
                    _startBtn.style.display = DisplayStyle.Flex;
                    _cancelBtn.style.display = DisplayStyle.Flex;
                    _collectBtn.style.display = DisplayStyle.None;
                    _startBtn.SetEnabled(_selectedRecipeId >= 0);
                    break;
                case CraftingJobState.InProgress:
                    _startBtn.style.display = DisplayStyle.None;
                    _cancelBtn.style.display = DisplayStyle.Flex;
                    _collectBtn.style.display = DisplayStyle.None;
                    break;
                case CraftingJobState.Completed:
                    _startBtn.style.display = DisplayStyle.None;
                    _cancelBtn.style.display = DisplayStyle.None;
                    _collectBtn.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        private void OnCloseClicked()
        {
            Close();
        }

        private void OnStartClicked()
        {
            if (_currentStationNetId == 0 || _selectedRecipeId < 0) return;
            CraftingClientState.Instance?.RequestStartCraft(_currentStationNetId, _selectedRecipeId);
            if (_messageLabel != null) _messageLabel.text = "Крафт запущен…";
        }

        private void OnCancelClicked()
        {
            if (_currentStationNetId == 0) return;
            CraftingClientState.Instance?.RequestCancelCraft(_currentStationNetId);
        }

        private void OnCollectClicked()
        {
            if (_currentStationNetId == 0) return;
            CraftingClientState.Instance?.RequestCollect(_currentStationNetId);
            if (_messageLabel != null) _messageLabel.text = "Забираете результат…";
        }
    }
}