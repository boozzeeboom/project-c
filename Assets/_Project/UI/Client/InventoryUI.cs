// =====================================================================================
// InventoryUI.cs — TAB-колесо инвентаря, UI Toolkit версия (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/dev/INVENTORY_V2_REFACTOR.md — Phase 4 (UI Toolkit wheel)
//
// Phase 4 ИЗМЕНЕНИЯ:
//   • Полностью переписан с IMGUI/GL на UI Toolkit (UIDocument + UXML/USS).
//   • Подписка на InventoryClientState.OnSnapshotUpdated (вместо чтения локального Inventory).
//   • 8 секторов (Resources, Equipment, Food, Fuel, Antigrav, Meziy, Medical, Tech).
//   • Hover/select через USS-классы (не GL).
//   • Sublist: ListView с предметами выбранного сектора.
//
// LEGACY: старая IMGUI-версия лежит в git-истории (commit перед этим). Если нужно
// откатить — git revert. Cleanup — в следующей сессии (отдельный тикет).
//
// TODO (Phase 8+): заметные доработки UX
//   • Иконки предметов внутри секторов (наложить surface на 8 секторов)
//   • Анимация "вспышка" при pickup (через transition на sector-N class)
//   • Slot 9 (центр) — для ключевого/тяжёлого предмета
//   • Draggable: перетаскивание предметов между секторами
// =====================================================================================

using System.Collections.Generic;
using ProjectC.Items;
using ProjectC.Items.Client;
using ProjectC.Items.Dto;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ProjectC.UI.Client
{
    /// <summary>
    /// TAB-колесо инвентаря (UI Toolkit). Открывается по Tab (через InputAction в Awake).
    /// Подписывается на InventoryClientState.OnSnapshotUpdated — единый source of truth.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        // ============================================================
        // Inspector
        // ============================================================
        [Header("UI Assets (Resources fallback)")]
        [SerializeField] private VisualTreeAsset inventoryWheelUxml;
        [SerializeField] private StyleSheet       inventoryWheelUss;

        [Header("Behavior")]
        [SerializeField] private bool visibleOnStart = false;

        // ============================================================
        // Runtime refs
        // ============================================================
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _wheelContainer;
        private VisualElement _wheel;
        private List<VisualElement> _sectors = new List<VisualElement>(8);
        private List<Label> _sectorLabels = new List<Label>(8);
        private VisualElement _wheelCenter;
        private Label _centerTypeLabel;
        private Label _centerCountLabel;
        private Label _sublistTitle;
        private ListView _sublist;
        private Button _useBtn;
        private Button _dropBtn;
        private Button _closeBtn;
        private Label _messageLabel;

        // ============================================================
        // State
        // ============================================================
        private bool _built;
        private bool _isOpen;
        private int _hoveredSector = -1;
        private int _selectedSector = -1;     // для sublist
        private int _selectedItemIndex = -1;  // в sublist
        private List<InventoryItemDto> _sublistCache = new List<InventoryItemDto>();

        // ============================================================
        // Input
        // ============================================================
        private InputAction _toggleAction;
        private System.Action<InputAction.CallbackContext> _onTogglePerformed;

        // ============================================================
        // Lifecycle
        // ============================================================

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (inventoryWheelUxml == null) inventoryWheelUxml = Resources.Load<VisualTreeAsset>("UI/InventoryWheel");
            if (inventoryWheelUss  == null) inventoryWheelUss  = Resources.Load<StyleSheet>("UI/InventoryWheel");
            if (Instance == null) Instance = this;

            _toggleAction = new InputAction("ToggleInventory", binding: "<Keyboard>/tab", expectedControlType: "Button");
            _onTogglePerformed = _ => Toggle();
        }

        private void OnEnable()
        {
            _toggleAction.Enable();
            _toggleAction.performed += _onTogglePerformed;
            EnsureBuilt();
            // Подписка на client state (может быть null до NetworkManagerController.Awake — retry в Update)
            TrySubscribeToClientState();
        }

        private void OnDisable()
        {
            _toggleAction.Disable();
            _toggleAction.performed -= _onTogglePerformed;
            UnsubscribeFromClientState();
        }

        private void OnDestroy()
        {
            _toggleAction.Dispose();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Retry: client state может быть null если InventoryUI создан до NetworkManagerController
            if (InventoryClientState.Instance != null && !_subscribed)
            {
                TrySubscribeToClientState();
            }

            // BUGFIX T-P19: Esc закрывает TAB-колесо
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsVisible())
            {
                SetVisible(false);
            }
        }

        // ============================================================
        // Client state subscription
        // ============================================================

        private bool _subscribed;
        private void TrySubscribeToClientState()
        {
            if (_subscribed) return;
            var state = InventoryClientState.Instance;
            if (state == null) return;
            state.OnSnapshotUpdated += HandleSnapshotUpdated;
            state.OnInventoryResult += HandleResultReceived;
            _subscribed = true;
            // Первичный refresh, если данные уже есть
            HandleSnapshotUpdated(state.CurrentSnapshot ?? default);
        }

        private void UnsubscribeFromClientState()
        {
            if (!_subscribed) return;
            var state = InventoryClientState.Instance;
            if (state != null)
            {
                state.OnSnapshotUpdated -= HandleSnapshotUpdated;
                state.OnInventoryResult -= HandleResultReceived;
            }
            _subscribed = false;
        }

        // ============================================================
        // Build
        // ============================================================

        private void EnsureBuilt()
        {
            if (_doc.rootVisualElement == null) return;
            if (inventoryWheelUxml == null) inventoryWheelUxml = Resources.Load<VisualTreeAsset>("UI/InventoryWheel");
            if (inventoryWheelUss  == null) inventoryWheelUss  = Resources.Load<StyleSheet>("UI/InventoryWheel");
            if (inventoryWheelUxml == null)
            {
                Debug.LogError("[InventoryUI] UXML не найден в Resources/UI/");
                return;
            }

            _doc.rootVisualElement.Clear();
            if (inventoryWheelUss != null) _doc.rootVisualElement.styleSheets.Add(inventoryWheelUss);
            _root = inventoryWheelUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;   // невидимый корень — не ловит клики
            _doc.rootVisualElement.Add(_root);

            // Element refs
            _wheelContainer = _root.Q<VisualElement>("wheel-container");
            _wheel          = _root.Q<VisualElement>("wheel");
            _wheelCenter    = _root.Q<VisualElement>("wheel-center");
            _centerTypeLabel  = _root.Q<Label>("center-type-label");
            _centerCountLabel = _root.Q<Label>("center-count-label");
            _sublistTitle   = _root.Q<Label>("sublist-title");
            _sublist        = _root.Q<ListView>("sublist");
            _useBtn         = _root.Q<Button>("use-btn");
            _dropBtn        = _root.Q<Button>("drop-btn");
            _closeBtn       = _root.Q<Button>("close-btn");
            _messageLabel   = _root.Q<Label>("message-label");

            _sectors.Clear();
            _sectorLabels.Clear();
            for (int i = 0; i < 8; i++)
            {
                var sec = _root.Q<VisualElement>($"sector-{i}");
                var lbl = _root.Q<Label>($"label-{i}");
                _sectors.Add(sec);
                _sectorLabels.Add(lbl);

                if (sec != null)
                {
                    int idx = i;
                    sec.RegisterCallback<PointerEnterEvent>(evt => OnSectorHover(idx));
                    sec.RegisterCallback<PointerLeaveEvent>(evt => OnSectorHoverEnd(idx));
                    sec.RegisterCallback<ClickEvent>(evt => OnSectorClick(idx));
                }
            }

            // Sublist ListView
            if (_sublist != null)
            {
                _sublist.makeItem = MakeSublistRow;
                _sublist.bindItem = BindSublistRow;
                _sublist.fixedItemHeight = 32;
                _sublist.selectionType = SelectionType.Single;
                _sublist.selectedIndex = -1;
                _sublist.selectionChanged += selectedItems =>
                {
                    _selectedItemIndex = FindSelectedIndex<InventoryItemDto>(_sublist, selectedItems);
                };
            }

            // Action buttons
            if (_useBtn != null)   _useBtn.clicked   += OnUseClicked;
            if (_dropBtn != null)  _dropBtn.clicked  += OnDropClicked;
            if (_closeBtn != null) _closeBtn.clicked += OnCloseClicked;

            // Initial state — скрыт, пока Tab не нажат
            SetVisible(visibleOnStart);
            _doc.rootVisualElement.MarkDirtyRepaint();
            _doc.rootVisualElement.schedule.Execute(() => _doc.rootVisualElement.MarkDirtyRepaint()).StartingIn(50);

            _built = true;
            Debug.Log("[InventoryUI] Built");
        }

        // ============================================================
        // Snapshot handler — пересчитать сектора + sublist
        // ============================================================

        private void HandleSnapshotUpdated(InventorySnapshotDto snap)
        {
            if (!_built) return;

            var state = InventoryClientState.Instance;
            if (state == null) return;

            // Обновить каждый сектор: has-items / empty, count
            for (int i = 0; i < 8; i++)
            {
                var type = (ItemType)i;
                int count = state.GetCountByType(type);

                // T-KEY-08: сектор 1 (Equipment) объединён с Key — "ВЛАДЕНИЕ".
                // Удобно для игрока: всё что носится и все ключи в одном месте.
                if (i == 1) // Equipment
                {
                    count += state.GetCountByType(ItemType.Key);
                }

                var sec = _sectors[i];
                if (sec == null) continue;

                if (count > 0)
                {
                    sec.AddToClassList("sector-has-items");
                    sec.RemoveFromClassList("sector-empty");
                }
                else
                {
                    sec.AddToClassList("sector-empty");
                    sec.RemoveFromClassList("sector-has-items");
                }

                // Лейбл: тип + количество
                var lbl = _sectorLabels[i];
                if (lbl != null)
                {
                    lbl.text = count > 0
                        ? $"{ItemTypeNames.GetDisplayName(type)}\n[{count}]"
                        : ItemTypeNames.GetDisplayName(type);
                }
            }

            // BUGFIX 2026-06-05 (Phase 10): если sublist открыт — перезагрузить его,
            // иначе после drop последнего item (или уменьшения count) sublist показывает
            // устаревший список. Сектор обновился выше (label/class), но itemsSource
            // не обновляется до следующего выбора сектора.
            if (_selectedSector >= 0 && _sublist != null)
            {
                RefreshSublist((ItemType)_selectedSector);
            }
        }

        private void HandleResultReceived(InventoryResultDto result)
        {
            if (_messageLabel == null) return;
            if (!IsVisible()) return;

            string msg = !string.IsNullOrEmpty(result.message)
                ? result.message
                : InventoryClientState.LocalizeResultCode((InventoryResultCode)result.code);
            _messageLabel.text = msg;
            _messageLabel.style.color = result.IsSuccess
                ? new StyleColor(new Color(0.4f, 0.95f, 0.4f))
                : new StyleColor(new Color(0.95f, 0.4f, 0.4f));
        }

        // ============================================================
        // Sector hover/click
        // ============================================================

        private void OnSectorHover(int idx)
        {
            if (idx < 0 || idx >= _sectors.Count) return;
            _hoveredSector = idx;
            var sec = _sectors[idx];
            if (sec != null) sec.AddToClassList("sector-hover");
        }

        private void OnSectorHoverEnd(int idx)
        {
            if (idx < 0 || idx >= _sectors.Count) return;
            _hoveredSector = -1;
            var sec = _sectors[idx];
            if (sec != null) sec.RemoveFromClassList("sector-hover");
        }

        private void OnSectorClick(int idx)
        {
            if (idx < 0 || idx >= _sectors.Count) return;
            if (_selectedSector == idx) return;
            // Снимаем выделение со старого
            if (_selectedSector >= 0 && _selectedSector < _sectors.Count)
            {
                _sectors[_selectedSector]?.RemoveFromClassList("sector-selected");
            }
            _selectedSector = idx;
            _sectors[idx].AddToClassList("sector-selected");
            RefreshSublist((ItemType)idx);
        }

        private void RefreshSublist(ItemType type)
        {
            if (_sublist == null) return;
            var state = InventoryClientState.Instance;
            if (state == null) return;

            // T-KEY-08: сектор "ВЛАДЕНИЕ" (Equipment) объединён с Key.
            // Показываем оба типа в sublist.
            _sublistCache = state.GetItemsByType(type);
            if ((int)type == 1) // Equipment — добавляем Key
            {
                var keyItems = state.GetItemsByType(ItemType.Key);
                _sublistCache.AddRange(keyItems);
            }

            _sublist.itemsSource = _sublistCache;
            _sublist.selectedIndex = -1;
            _selectedItemIndex = -1;
            _sublist.Rebuild();

            int totalCount = _sublistCache.Count;
            if (_centerTypeLabel != null) _centerTypeLabel.text = ItemTypeNames.GetDisplayName(type);
            if (_centerCountLabel != null) _centerCountLabel.text = totalCount.ToString();
            if (_sublistTitle != null) _sublistTitle.text = $"{ItemTypeNames.GetDisplayName(type)} ({totalCount})";
        }

        // ============================================================
        // Sublist rows
        // ============================================================

        private VisualElement MakeSublistRow()
        {
            var row = new VisualElement();
            row.AddToClassList("sublist-row");
            var icon = new VisualElement { name = "row-icon" };
            icon.AddToClassList("sublist-row-icon");
            row.Add(icon);
            var name = new Label { name = "row-name" };
            name.AddToClassList("sublist-row-name");
            row.Add(name);
            var qty = new Label { name = "row-qty" };
            qty.AddToClassList("sublist-row-qty");
            row.Add(qty);
            return row;
        }

        private void BindSublistRow(VisualElement row, int index)
        {
            if (_sublist == null) return;
            if (index < 0 || index >= _sublistCache.Count) return;
            var item = _sublistCache[index];
            var state = InventoryClientState.Instance;
            var def = state != null ? state.GetItemDefinition(item.itemId) : null;

            var icon = row.Q<VisualElement>("row-icon");
            if (def != null && def.icon != null)
                icon.style.backgroundImage = new StyleBackground(def.icon);
            else
                icon.style.backgroundImage = new StyleBackground(StyleKeyword.Null);

            var name = row.Q<Label>("row-name");
            // T-KEY-08: для Key-предметов показываем имя корабля через scene-placed binding.
            string displayName;
            if (def != null && (ItemType)item.type == ItemType.Key)
            {
                displayName = ResolveKeyItemDisplayName(def, item);
            }
            else
            {
                displayName = def != null ? def.itemName : $"Item#{item.itemId}";
            }
            name.text = displayName;

            var qty = row.Q<Label>("row-qty");
            qty.text = item.quantity > 1 ? $"×{item.quantity}" : "";
        }

        /// <summary>T-KEY-08: резолвит имя корабля для Key-предмета через scene-placed KeyRodInstanceBinding.
        /// Стабильно между сессиями (не зависит от эфемерного instanceId).</summary>
        private static string ResolveKeyItemDisplayName(ProjectC.Items.ItemData def, InventoryItemDto dto)
        {
            string baseName = def != null ? def.itemName : $"Item#{dto.itemId}";
            if ((ItemType)dto.type != ItemType.Key) return baseName;

            // Priority: scene-placed KeyRodInstanceBinding по itemId
            var bindingType = System.Type.GetType("ProjectC.Ship.Key.KeyRodInstanceBinding, Assembly-CSharp");
            if (bindingType == null) return baseName;

            var itemField = bindingType.GetField("_keyItemData",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var shipField = bindingType.GetField("_ship",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (itemField == null || shipField == null) return baseName;

            var invWorld = ProjectC.Items.InventoryWorld.Instance;
            if (invWorld == null) return baseName;

            var bindings = UnityEngine.Object.FindObjectsByType(bindingType,
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var binding in bindings)
            {
                var itemData = itemField.GetValue(binding) as ProjectC.Items.ItemData;
                if (itemData == null) continue;
                int bindingItemId = invWorld.GetOrRegisterItemId(itemData);
                if (bindingItemId == dto.itemId)
                {
                    var sc = shipField.GetValue(binding) as ProjectC.Player.ShipController;
                    if (sc != null && !string.IsNullOrEmpty(sc.CustomDisplayName))
                        return $"🚀 {sc.CustomDisplayName}";
                }
            }
            return baseName;
        }

        private static int FindSelectedIndex<T>(ListView list, IEnumerable<object> selectedItems)
        {
            if (selectedItems == null) return -1;
            object first = null;
            foreach (var o in selectedItems) { first = o; break; }
            if (first == null) return -1;
            var src = list.itemsSource;
            if (src is List<T> listT)
            {
                for (int i = 0; i < listT.Count; i++)
                    if (EqualityComparer<T>.Default.Equals(listT[i], (T)first)) return i;
            }
            return -1;
        }

        // ============================================================
        // Actions
        // ============================================================

        private void OnUseClicked()
        {
            if (_selectedItemIndex < 0 || _selectedItemIndex >= _sublistCache.Count)
            {
                SetMessage("Выберите предмет для использования", true);
                return;
            }
            // MVP: пока не реализовано, даём feedback
            SetMessage("Использование предметов — TODO (Phase 8+)", false);
            // InventoryClientState.Instance?.RequestUse(_sublistCache[_selectedItemIndex].slotIndex);
        }

        // Phase 10 (INVENTORY_V2_DROP_DESIGN.md): бросить предмет в мир перед игроком.
        // Сервер уберёт из инвентаря + заспавнит PickupItem на worldPos.
        private void OnDropClicked()
        {
            if (_selectedItemIndex < 0 || _selectedItemIndex >= _sublistCache.Count)
            {
                SetMessage("Выберите предмет для броска", true);
                return;
            }
            var state = ProjectC.Items.Client.InventoryClientState.Instance;
            if (state == null)
            {
                SetMessage("Сеть не запущена", true);
                return;
            }
            var localPlayer = FindFirstObjectByType<ProjectC.Player.NetworkPlayer>();
            if (localPlayer == null)
            {
                SetMessage("Игрок не найден", true);
                return;
            }
            Vector3 playerPos = localPlayer.GetEffectivePosition();
            // Бросаем в 1.5м перед игроком (forward * 1.5m, на уровне земли)
            Vector3 dropPos = playerPos + localPlayer.transform.forward * 1.5f;
            int slotIndex = _sublistCache[_selectedItemIndex].slotIndex;
            state.RequestDrop(slotIndex, 1, dropPos, playerPos);
            SetMessage("Бросаю...", false);
        }

        private void OnCloseClicked() => SetVisible(false);

        private void SetMessage(string msg, bool isError)
        {
            if (_messageLabel == null) return;
            _messageLabel.text = msg;
            _messageLabel.style.color = isError
                ? new StyleColor(new Color(0.95f, 0.4f, 0.4f))
                : new StyleColor(new Color(0.9f, 0.9f, 0.9f));
        }

        // ============================================================
        // Toggle / Visibility
        // ============================================================

        public void Toggle()
        {
            if (!_built) EnsureBuilt();
            _isOpen = !_isOpen;
            SetVisible(_isOpen);
            if (_isOpen) InventoryClientState.Instance?.RequestRefresh();
        }

        public bool IsVisible() => _wheelContainer != null && _wheelContainer.style.display == DisplayStyle.Flex;

        public void SetVisible(bool v)
        {
            if (_wheelContainer == null) return;
            _wheelContainer.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
            if (v) _wheelContainer.pickingMode = PickingMode.Position;
            else   _wheelContainer.pickingMode = PickingMode.Ignore;

            // Cursor
            if (v)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else
            {
                _selectedSector = -1;
                _selectedItemIndex = -1;
                // Восстановим cursor только если NetworkManager жив (иначе меню отключилось — нечего восстанавливать)
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }
    }
}
