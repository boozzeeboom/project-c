// =====================================================================================
// InventoryTab.cs — вкладка ИНВЕНТАРЬ для CharacterWindow (Project C: The Clouds)
// =====================================================================================
// T-P19 refactor: вынесено из CharacterWindow.cs (монолит 3186 строк).
// Отвечает за список предметов, сортировку по ItemType, detail-panel,
// фильтрацию по типу, [НАДЕТЬ]-кнопку.
//
// Подписки:
//   • InventoryClientState.OnSnapshotUpdated — server-authoritative snapshot
//   • InventoryClientState.OnInventoryResult — результат операции
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using ProjectC.Items;
using ProjectC.Items.Client;
using ProjectC.Items.Dto;
using ProjectC.Ship.Client;  // T-KEY-07: ShipTelemetryClientState для имени корабля ключа
using ProjectC.Ship.Key;     // T-KEY-07: KeyRodInstanceWorld fallback
using ProjectC.Player;       // T-KEY-07: ShipController fallback
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UI.Client
{
    /// <summary>
    /// Вкладка ИНВЕНТАРЬ для CharacterWindow. Создаётся CharacterWindow в EnsureBuilt.
    /// </summary>
    public class InventoryTab
    {
        // ============================================================
        // State
        // ============================================================
        private CharacterWindow _owner;
        private VisualElement _root;

        // Shared UI refs (владеет CharacterWindow, InventoryTab их конфигурирует)
        private DropdownField _filterSource;
        private DropdownField _filterState;
        private TextField _filterSearch;
        private Label _creditsLabel;
        private Label _messageLabel;
        private Label _statCredits;

        // Inventory-specific refs
        private VisualElement _inventorySection;
        private ListView _inventoryList; // Сессия 2 ROLLBACK: обратно на ListView
        private Label _invDetailName;
        private Label _invDetailType;
        private Label _invDetailWeight;
        private Label _invDetailStat;
        private Label _invDetailDesc;

        // Caches
        private List<InventoryListItem> _inventoryCache = new List<InventoryListItem>();
        private int _selectedInventoryItem = -1;
        private List<string> _inventoryFilterSourceOptionsCache; // динамически по ItemType
        private List<string> _inventoryFilterStateOptions = new List<string> { "Все типы" };

        // Subscription flags
        private bool _isInventorySubscribed = false;

        // ============================================================
        // DTO-проекция для ListView
        // ============================================================
        private struct InventoryListItem
        {
            public string itemId;
            // T-KEY-08 fix: instanceId для уникальной идентификации Key-предметов.
            // 0 = не применимо (не Key или legacy item).
            public int instanceId;
            public string displayName;
            public ItemType type;
            public int quantity;
            public Sprite icon;
        }

        // ============================================================
        // API (вызывается из CharacterWindow)
        // ============================================================

        /// <summary>
        /// Инициализация: найти все UI-элементы в root, настроить ListView,
        /// подписаться на InventoryClientState.
        /// </summary>
        public void BuildUI(CharacterWindow owner, VisualElement root,
            DropdownField filterSource, DropdownField filterState, TextField filterSearch,
            Label creditsLabel, Label messageLabel, Label statCredits)
        {
            _owner = owner;
            _root = root;
            _filterSource = filterSource;
            _filterState = filterState;
            _filterSearch = filterSearch;
            _creditsLabel = creditsLabel;
            _messageLabel = messageLabel;
            _statCredits = statCredits;

            _inventorySection = root.Q<VisualElement>("inventory-section");

            // ---- ListView: Inventory ----
            _inventoryList = root.Q<ListView>("inventory-list");
            if (_inventoryList != null)
            {
                _inventoryList.makeItem = MakeInventoryRow;
                _inventoryList.bindItem = BindInventoryRow;
                _inventoryList.fixedItemHeight = 28;
                _inventoryList.selectionType = SelectionType.Single;
                _inventoryList.selectedIndex = -1;
                _inventoryList.selectionChanged += OnInventorySelectionChanged;
            }

            // ---- Detail labels ----
            _invDetailName = root.Q<Label>("inventory-detail-name");
            _invDetailType = root.Q<Label>("inventory-detail-type");
            _invDetailWeight = root.Q<Label>("inventory-detail-weight");
            _invDetailStat = root.Q<Label>("inventory-detail-stat");
            _invDetailDesc = root.Q<Label>("inventory-detail-desc");

            // ---- Subscribe ----
            SubscribeInventory();

            // T-P19: подписываемся на EquipmentClientState — чтобы кнопки НАДЕТЬ/СНЯТЬ
            // обновлялись при изменении экипировки (без переоткрытия таба).
            TrySubscribeEquipment();

            if (ProjectC.Items.Client.InventoryClientState.Instance == null)
            {
                Debug.LogWarning("[InventoryTab] InventoryClientState.Instance == null на момент BuildUI — Update() lazy-подпишется");
            }
        }

        /// <summary>
        /// T-P19: подписка на EquipmentClientState.OnEquipmentUpdated для обновления кнопок.
        /// </summary>
        private void TrySubscribeEquipment()
        {
            if (_isEquipmentSubscribed) return;
            var eq = ProjectC.Equipment.EquipmentClientState.Instance;
            if (eq == null) return;
            eq.OnEquipmentUpdated += OnEquipmentUpdated;
            _isEquipmentSubscribed = true;
            Debug.Log("[InventoryTab] Subscribed to EquipmentClientState.OnEquipmentUpdated");
        }

        private void OnEquipmentUpdated(ProjectC.Equipment.Dto.EquipmentSnapshotDto _snap)
        {
            // Обновляем кнопки НАДЕТЬ/СНЯТЬ в инвентаре при изменении экипировки
            if (_inventoryList != null)
                _inventoryList.RefreshItems();
        }

        private bool _isEquipmentSubscribed = false;

        /// <summary>
        /// Вызывается из CharacterWindow.SwitchTab когда активен "inventory".
        /// Обновляет display visibility, конфигурирует фильтры, триггерит refresh.
        /// </summary>
        public void OnTabShown()
        {
            if (_inventorySection != null)
                _inventorySection.style.display = DisplayStyle.Flex;

            // MarkDirtyRepaint — BUGFIX: display: none → flex не вызывает повторный layout
            if (_inventoryList != null)
                _inventoryList.MarkDirtyRepaint();

            // T-P19: schedule delayed repaint — UI Toolkit не пересчитывает layout
            // при display: none → flex на вложенных flex-контейнерах.
            // Без этого inventory-layout получает только 60% высоты от других табов.
            if (_inventorySection != null)
                _inventorySection.schedule.Execute(() =>
                {
                    if (_inventorySection != null) _inventorySection.MarkDirtyRepaint();
                    if (_inventoryList != null) _inventoryList.MarkDirtyRepaint();
                }).StartingIn(50);

            // Configure filters
            ConfigureInventoryFilters();

            // BUGFIX T-P19: RequestRefresh при каждом открытии таба.
            // InventoryClientState.CurrentSnapshot может быть пуст (ни один UI не запросил snapshot).
            // TAB-колесо (InventoryUI) это делает, но CharacterWindow должен сам.
            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (invState != null)
            {
                if (_isInventorySubscribed)
                {
                    invState.RequestRefresh();
                    Debug.Log("[InventoryTab] OnTabShown: RequestRefresh");
                }
                else
                {
                    // Не подписаны — подписываемся и запрашиваем
                    SubscribeInventory();
                    invState.RequestRefresh();
                    Debug.Log("[InventoryTab] OnTabShown: Subscribe + RequestRefresh");
                }
            }

            // Refresh data
            RefreshInventoryCache();
            ApplyInventoryFilters();
        }

        /// <summary>
        /// Вызывается из CharacterWindow.SwitchTab когда "inventory" перестаёт быть активным.
        /// </summary>
        public void OnTabHidden()
        {
            if (_inventorySection != null)
                _inventorySection.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Вызывается из CharacterWindow.OnDisable. Отписывается от всех событий.
        /// </summary>
        public void Unsubscribe()
        {
            if (_isInventorySubscribed)
            {
                var invState = ProjectC.Items.Client.InventoryClientState.Instance;
                if (invState != null)
                {
                    invState.OnSnapshotUpdated -= HandleInventorySnapshotUpdated;
                    invState.OnInventoryResult -= HandleInventoryResultReceived;
                }
                if (_inventoryList != null) _inventoryList.selectionChanged -= OnInventorySelectionChanged;
                _isInventorySubscribed = false;
            }

            // T-P19: отписка от EquipmentClientState
            if (_isEquipmentSubscribed)
            {
                var eq = ProjectC.Equipment.EquipmentClientState.Instance;
                if (eq != null) eq.OnEquipmentUpdated -= OnEquipmentUpdated;
                _isEquipmentSubscribed = false;
            }
        }

        /// <summary>
        /// Lazy-subscribe из CharacterWindow.Update (если Instance был null при BuildUI).
        /// Также дозапрашиваем EquipmentClientState.
        /// </summary>
        public void TryLazySubscribe()
        {
            if (!_isInventorySubscribed)
            {
                var invState = ProjectC.Items.Client.InventoryClientState.Instance;
                if (invState == null) return;
                SubscribeInventory();
                invState.RequestRefresh();
                Debug.Log("[InventoryTab] Lazy-subscribed to InventoryClientState");
            }

            // T-P19: lazy-subscribe для EquipmentClientState
            if (!_isEquipmentSubscribed)
            {
                TrySubscribeEquipment();
            }
        }

        /// <summary>
        /// HandleInventorySnapshotUpdated вызывается из CharacterWindow при cross-tab
        /// (credits update) + из InventoryTab подписки на OnSnapshotUpdated.
        /// </summary>
        public void HandleSnapshotUpdated(InventorySnapshotDto snap)
        {
            // DIAG: логируем
            Debug.Log($"[InventoryTab] HandleSnapshotUpdated: items={(snap.items!=null?snap.items.Length:0)}, cacheBefore={_inventoryCache.Count}");

            // Cross-tab: обновляем credits в header
            if (_creditsLabel != null)
                _creditsLabel.text = $"Кредиты: {snap.credits:F0} CR";
            if (_statCredits != null)
                _statCredits.text = $"{snap.credits:F0} CR";

            // Refresh cache unconditionally (cross-tab cache rule)
            RefreshInventoryCache();

            // Apply filters only if this tab is active
            if (_owner != null && _owner.GetActiveTab() == "inventory")
                ApplyInventoryFilters();
        }

        /// <summary>
        /// Применить фильтры (вызывается из CharacterWindow при изменении фильтра поиска/типа).
        /// </summary>
        public void ApplyFilters()
        {
            ApplyInventoryFilters();
        }

        /// <summary>
        /// HandleInventoryResultReceived — показываем feedback.
        /// </summary>
        public void HandleResultReceived(InventoryResultDto result)
        {
            if (_messageLabel == null) return;
            if (_owner != null && !_owner.IsVisible()) return;

            string msg = !string.IsNullOrEmpty(result.message)
                ? result.message
                : InventoryClientState.LocalizeResultCode((InventoryResultCode)result.code);

            _messageLabel.text = msg;
            _messageLabel.style.color = result.IsSuccess
                ? new StyleColor(new Color(0.4f, 0.95f, 0.4f))
                : new StyleColor(new Color(0.95f, 0.4f, 0.4f));
        }

        // ============================================================
        // Subscriptions
        // ============================================================

        private void SubscribeInventory()
        {
            if (_isInventorySubscribed) return;
            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (invState == null) return;
            invState.OnSnapshotUpdated += HandleInventorySnapshotUpdated;
            invState.OnInventoryResult += HandleInventoryResultReceived;
            _isInventorySubscribed = true;
        }

        private void UnsubscribeInventory()
        {
            if (!_isInventorySubscribed) return;
            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (invState == null) { _isInventorySubscribed = false; return; }
            invState.OnSnapshotUpdated -= HandleInventorySnapshotUpdated;
            invState.OnInventoryResult -= HandleInventoryResultReceived;
            if (_inventoryList != null) _inventoryList.selectionChanged -= OnInventorySelectionChanged;
            _isInventorySubscribed = false;
        }

        // ============================================================
        // Refresh + Sort
        // ============================================================

        private void RefreshInventoryCache()
        {
            _inventoryCache.Clear();

            var invState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (invState == null || !invState.CurrentSnapshot.HasValue)
            {
                // Данных ещё нет — пустой кэш. UI покажет пустой список.
                // СESSION 2 ROLLBACK: вернулись к ListView (было рабочее)
                SyncListView();
                return;
            }

            var snap = invState.CurrentSnapshot.Value;
            var items = snap.items;
            if (items == null)
            {
                SyncListView();
                return;
            }

            // T-KEY-08 fix: Key-предметы группируем по (itemId, instanceId) — иначе 2 разных
            // ключа одного типа сливаются в "x2 Pushka". Остальные предметы — по itemId.
            var groups = new Dictionary<(int itemId, int instanceId), (int totalQty, InventoryItemDto first)>();
            foreach (var dto in items)
            {
                if (dto.itemId <= 0) continue;

                int groupKey2 = (ItemType)dto.type == ItemType.Key ? dto.instanceId : 0;
                var compositeKey = (dto.itemId, groupKey2);

                if (groups.TryGetValue(compositeKey, out var existing))
                    groups[compositeKey] = (existing.totalQty + dto.quantity, existing.first);
                else
                    groups[compositeKey] = (dto.quantity, dto);
            }

            // T-P19: сортировка по (ItemType, displayName) — категории сгруппированы,
            // внутри категории — алфавитный порядок. Сначала заполняем _inventoryCache,
            // потом сортируем in-place в конце (LINQ OrderBy может иметь ленивую оценку).
            foreach (var kvp in groups)
            {
                var first = kvp.Value.first;
                ItemData def = invState.GetItemDefinition(first.itemId);
                _inventoryCache.Add(new InventoryListItem
                {
                    itemId = first.itemId.ToString(),
                    instanceId = first.instanceId,
                    displayName = ResolveKeyItemDisplayName(def, first),
                    type = (ItemType)first.type,
                    quantity = kvp.Value.totalQty,
                    icon = def != null ? def.icon : null,
                });
            }

            // T-P19: in-place Sort гарантирует порядок в самом списке
            _inventoryCache.Sort((a, b) =>
            {
                int typeCmp = ((int)a.type).CompareTo((int)b.type);
                if (typeCmp != 0) return typeCmp;
                return string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase);
            });

            // DIAG T-P19: выводим порядок сортировки
            if (_inventoryCache.Count > 0)
            {
                var diag = new System.Text.StringBuilder();
                diag.Append("[InventoryTab] Sorted items: ");
                for (int i = 0; i < _inventoryCache.Count && i < 20; i++)
                {
                    var it = _inventoryCache[i];
                    diag.Append($"#{i}={it.displayName}(type={(int)it.type}) ");
                }
                Debug.Log(diag.ToString());
            }

            SyncListView();
        }

        /// <summary>T-KEY-07: для Key-предметов с instanceId подставляет имя корабля из телепатрии.</summary>
        /// <summary>T-KEY-07: для Key-предметов с instanceId подставляет имя корабля из телепатрии.
        /// Fallback при рассинхронизации instanceId (после перезагрузки persistence) — ищет
        /// по itemId + ownership в KeyRodInstanceWorld.</summary>
        private string ResolveKeyItemDisplayName(ItemData def, InventoryItemDto first)
        {
            string baseName = def != null ? def.itemName : $"Item#{first.itemId}";
            if ((ItemType)first.type != ItemType.Key) return baseName;

            // Priority 1: ShipTelemetryClientState (все клиенты, instanceId должен совпадать)
            string telemetryName = TryGetShipNameFromTelemetry(first.instanceId);
            if (telemetryName != null) return $"🚀 {telemetryName}";

            // Priority 2: KeyRodInstanceWorld Host fallback по instanceId (работает в сессии)
            string hostName = TryGetShipNameFromKeyWorld(first.instanceId);
            if (hostName != null) return $"🚀 {hostName}";

            // Priority 3: KeyRodInstanceWorld по itemId (после перезагрузки persistence)
            string persistedName = TryGetShipNameByItemId(first.itemId);
            if (persistedName != null) return $"🚀 {persistedName}";

            return baseName;
        }

        private string TryGetShipNameFromTelemetry(int instanceId)
        {
            if (instanceId <= 0) return null;
            var telemetry = ProjectC.Ship.Client.ShipTelemetryClientState.Instance;
            if (telemetry == null) return null;
            foreach (var ship in telemetry.MyShips)
            {
                if (ship.Value.keyInstanceId == instanceId)
                {
                    string n = ship.Value.displayName.ToString();
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            return null;
        }

        private string TryGetShipNameFromKeyWorld(int instanceId)
        {
            if (instanceId <= 0) return null;
            if (!ProjectC.Ship.Key.KeyRodInstanceWorld.IsInitialized) return null;
            var inst = ProjectC.Ship.Key.KeyRodInstanceWorld.GetInstance(instanceId);
            if (inst == null) return null;
            return FindShipNameByNetworkId(inst.registeredShipId);
        }

                private string TryGetShipNameByItemId(int itemId)
        {
            if (itemId <= 0) return null;
            // Ищем в KeyRodInstanceWorld любой Active instance с этим itemId.
            // Не используем KeyRodInstanceBinding — его может не быть (server-spawned pickup).
            if (!ProjectC.Ship.Key.KeyRodInstanceWorld.IsInitialized) return null;
            var all = ProjectC.Ship.Key.KeyRodInstanceWorld.GetAllInstances();
            if (all == null) return null;
            foreach (var inst in all)
            {
                if (inst.itemId == itemId
                    && inst.state == ProjectC.Ship.Key.KeyRodInstanceState.Active
                    && inst.registeredShipId != 0)
                {
                    return FindShipNameByNetworkId(inst.registeredShipId);
                }
            }
            return null;
        }

        private static string FindShipNameByNetworkId(ulong shipNetId)
        {
            if (shipNetId == 0) return null;
            foreach (var sc in UnityEngine.Object.FindObjectsByType<ProjectC.Player.ShipController>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
            {
                if (sc.NetworkObjectId == shipNetId)
                {
                    string n = sc.CustomDisplayName;
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            return null;
        }


        private void SyncListView()
        {
            if (_inventoryList == null) return;
            // BUGFIX T-P19: ВСЕГДА проверяем itemsSource, не только при itemsSourceNull=true.
            // Без этого после первого пустого кэша itemsSource остаётся пустым списком,
            // и RefreshItems() ничего не показывает.
            if (!ReferenceEquals(_inventoryList.itemsSource, _inventoryCache))
                _inventoryList.itemsSource = _inventoryCache;
            _inventoryList.RefreshItems();
        }

        // ============================================================
        // Filters
        // ============================================================

        private void ConfigureInventoryFilters()
        {
            // Build dynamic options: "Все типы" + все 8 ItemType
            if (_inventoryFilterSourceOptionsCache == null)
            {
                _inventoryFilterSourceOptionsCache = new List<string> { "Все типы" };
                foreach (ItemType t in Enum.GetValues(typeof(ItemType)))
                {
                    _inventoryFilterSourceOptionsCache.Add(ItemTypeNames.GetDisplayName(t));
                }
            }
            if (_filterSource != null)
            {
                _filterSource.choices = _inventoryFilterSourceOptionsCache;
                if (!_inventoryFilterSourceOptionsCache.Contains(_filterSource.value))
                    _filterSource.value = "Все типы";
            }
            if (_filterState != null)
            {
                _filterState.style.display = DisplayStyle.None;
            }
        }

        private void ApplyInventoryFilters()
        {
            if (_inventoryList == null) return;
            IEnumerable<InventoryListItem> src = _inventoryCache;

            string source = _filterSource != null ? _filterSource.value : "Все типы";
            if (source != "Все типы")
            {
                src = src.Where(i => ItemTypeNames.GetDisplayName(i.type) == source);
            }
            string search = _filterSearch != null ? (_filterSearch.value ?? "").ToLowerInvariant() : "";
            if (!string.IsNullOrEmpty(search))
            {
                src = src.Where(i => (i.displayName ?? "").ToLowerInvariant().Contains(search));
            }

            var filteredList = src.ToList();
            if (!ReferenceEquals(_inventoryList.itemsSource, filteredList))
                _inventoryList.itemsSource = filteredList;
            _inventoryList.RefreshItems();
        }

        // ============================================================
        // Row factories
        // ============================================================

        private VisualElement MakeInventoryRow()
        {
            var row = new VisualElement();
            row.AddToClassList("inventory-row");
            var icon = new VisualElement { name = "row-icon" };
            icon.AddToClassList("inventory-icon");
            row.Add(icon);
            var name = new Label { name = "row-name" };
            name.AddToClassList("inventory-name");
            row.Add(name);
            var type = new Label { name = "row-type" };
            type.AddToClassList("inventory-type");
            row.Add(type);
            var qty = new Label { name = "row-qty" };
            qty.AddToClassList("inventory-qty");
            row.Add(qty);
            var equipBtn = new VisualElement { name = "row-equip-btn" };
            equipBtn.AddToClassList("inventory-equip-btn");
            var equipLabel = new Label { name = "row-equip-label", text = "НАДЕТЬ" };
            equipLabel.AddToClassList("inventory-equip-label");
            equipBtn.Add(equipLabel);
            row.Add(equipBtn);
            return row;
        }

        private void BindInventoryRow(VisualElement row, int index)
        {
            if (_inventoryList == null) return;
            var src = _inventoryList.itemsSource;
            if (src is List<InventoryListItem> list)
            {
                if (index < 0 || index >= list.Count) return;
                var item = list[index];

                var icon = row.Q<VisualElement>("row-icon");
                if (item.icon != null)
                    icon.style.backgroundImage = new StyleBackground(item.icon);
                else
                    icon.style.backgroundImage = new StyleBackground(StyleKeyword.Null);

                row.Q<Label>("row-name").text = item.displayName;
                row.Q<Label>("row-type").text = ItemTypeNames.GetDisplayName(item.type);
                row.Q<Label>("row-qty").text = $"×{item.quantity}";

                // [НАДЕТЬ] / [СНЯТЬ] — только для Equipment
                var equipBtn = row.Q<VisualElement>("row-equip-btn");
                if (equipBtn == null) return;

                bool isEquipable = item.type == ItemType.Equipment;
                if (isEquipable)
                {
                    equipBtn.style.display = DisplayStyle.Flex;

                    // T-P19: проверяем — предмет уже надет?
                    bool isEquipped = IsItemEquipped(item.itemId);
                    var label = equipBtn.Q<Label>("row-equip-label");
                    if (label != null) label.text = isEquipped ? "СНЯТЬ" : "НАДЕТЬ";
                    equipBtn.RemoveFromClassList("equipped");
                    if (isEquipped) equipBtn.AddToClassList("equipped");

                    string capturedDisplayName = item.displayName;
                    string capturedItemId = item.itemId;
                    bool capturedIsEquipped = isEquipped;
                    equipBtn.UnregisterCallback<ClickEvent>(OnInventoryEquipBtnClick);
                    equipBtn.RegisterCallback<ClickEvent>(evt =>
                    {
                        if (capturedIsEquipped)
                            OnUnequipFromInventoryClicked(capturedItemId, capturedDisplayName);
                        else
                            OnEquipFromInventoryClicked(capturedItemId, capturedDisplayName);
                        evt.StopPropagation();
                    });
                }
                else
                {
                    equipBtn.style.display = DisplayStyle.None;
                }
            }
        }

        private void OnInventoryEquipBtnClick(ClickEvent evt) { /* placeholder for Unregister */ }

        /// <summary>
        /// T-P19: проверяет, надет ли предмет с inventory itemId.
        /// Смотрит EquipmentClientState.CurrentSnapshot — все 13 слотов.
        /// </summary>
        private bool IsItemEquipped(string itemIdStr)
        {
            if (!int.TryParse(itemIdStr, out int itemId)) return false;
            var eq = ProjectC.Equipment.EquipmentClientState.Instance;
            if (eq == null || !eq.CurrentSnapshot.HasValue) return false;
            var equip = eq.CurrentSnapshot.Value.equip;
            if (equip.slotItemIds == null) return false;
            for (int i = 0; i < equip.slotItemIds.Length; i++)
            {
                if (equip.slotOccupied[i] == 1 && equip.slotItemIds[i] == itemId)
                    return true;
            }
            return false;
        }

        // ============================================================
        // Equip/Unequip from inventory
        // ============================================================

        private void OnEquipFromInventoryClicked(string itemIdStr, string displayName)
        {
            try
            {
                if (!int.TryParse(itemIdStr, out int itemId)) return;

                ProjectC.Items.ItemData def = ProjectC.Items.InventoryWorld.Instance?.GetItemDefinition(itemId);
                if (def == null)
                {
                    Debug.LogWarning("[InventoryTab] item not found in db");
                    return;
                }

                ProjectC.Equipment.EquipSlot slot = ProjectC.Equipment.EquipSlot.None;
                if (def is ProjectC.Equipment.ClothingItemData c) slot = c.slot;
                else if (def is ProjectC.Equipment.ModuleItemData m) slot = m.slot;
                else if (def is ProjectC.Equipment.WeaponItemData)
                {
                    // T-CB03: weapon → WeaponMain по умолчанию
                    slot = ProjectC.Equipment.EquipSlot.WeaponMain;
                }
                if (slot == ProjectC.Equipment.EquipSlot.None)
                {
                    Debug.LogWarning("[InventoryTab] item not equipable");
                    return;
                }

                // Resolve itemId in _itemDatabase (InventoryWorld uses definition IDs)
                int dbItemId = ResolveDbItemId(def);
                if (dbItemId <= 0) return;

                CallEquipRpc(dbItemId, slot, def.itemName);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[InventoryTab] OnEquipFromInventoryClicked error: {ex.Message}");
            }
        }

        /// <summary>
        /// T-P19: снять предмет. Находит слот в котором предмет надет,
        /// вызывает RequestUnequipRpc.
        /// </summary>
        private void OnUnequipFromInventoryClicked(string itemIdStr, string displayName)
        {
            try
            {
                if (!int.TryParse(itemIdStr, out int itemId)) return;

                // Находим слот в котором надет предмет
                ProjectC.Equipment.EquipSlot slot = FindEquippedSlot(itemId);
                if (slot == ProjectC.Equipment.EquipSlot.None)
                {
                    Debug.LogWarning("[InventoryTab] item not equipped in any slot");
                    return;
                }

                CallUnequipRpc(slot, displayName);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[InventoryTab] OnUnequipFromInventoryClicked error: {ex.Message}");
            }
        }

        /// <summary>
        /// T-P19: найти слот в котором надет itemId.
        /// </summary>
        private ProjectC.Equipment.EquipSlot FindEquippedSlot(int itemId)
        {
            var eq = ProjectC.Equipment.EquipmentClientState.Instance;
            if (eq == null || !eq.CurrentSnapshot.HasValue) return ProjectC.Equipment.EquipSlot.None;
            var equip = eq.CurrentSnapshot.Value.equip;
            if (equip.slotItemIds == null) return ProjectC.Equipment.EquipSlot.None;
            for (int i = 0; i < equip.slotItemIds.Length; i++)
            {
                if (equip.slotOccupied[i] == 1 && equip.slotItemIds[i] == itemId)
                    return ProjectC.Equipment.EquipmentData.IndexToSlot(i);
            }
            return ProjectC.Equipment.EquipSlot.None;
        }

        // Shared helpers

        private int ResolveDbItemId(ProjectC.Items.ItemData def)
        {
            var invDbField = typeof(ProjectC.Items.InventoryWorld).GetField("_itemDatabase",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var invDb = invDbField?.GetValue(ProjectC.Items.InventoryWorld.Instance)
                as System.Collections.Generic.Dictionary<int, ProjectC.Items.ItemData>;
            if (invDb != null)
            {
                foreach (var kvp in invDb)
                {
                    if (kvp.Value == def) return kvp.Key;
                }
            }
            Debug.LogWarning("[InventoryTab] item not found in db");
            return -1;
        }

        private void CallEquipRpc(int dbItemId, ProjectC.Equipment.EquipSlot slot, string itemName)
        {
            var t = System.Type.GetType("ProjectC.Equipment.EquipmentServer, Assembly-CSharp");
            if (t == null) { Debug.LogWarning("[InventoryTab] EquipmentServer type not found"); return; }
            var inst = t.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
            if (inst == null) { Debug.LogWarning("[InventoryTab] EquipmentServer.Instance is null"); return; }
            var mi = t.GetMethod("RequestEquipRpc");
            if (mi == null) { Debug.LogWarning("[InventoryTab] RequestEquipRpc not found"); return; }
            var rpcParams = System.Activator.CreateInstance(typeof(Unity.Netcode.RpcParams));
            mi.Invoke(inst, new object[] { dbItemId, slot, rpcParams });
            Debug.Log($"[InventoryTab] RequestEquipRpc: itemId={dbItemId} slot={slot} name={itemName}");
        }

        private void CallUnequipRpc(ProjectC.Equipment.EquipSlot slot, string itemName)
        {
            var t = System.Type.GetType("ProjectC.Equipment.EquipmentServer, Assembly-CSharp");
            if (t == null) { Debug.LogWarning("[InventoryTab] EquipmentServer type not found"); return; }
            var inst = t.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
            if (inst == null) { Debug.LogWarning("[InventoryTab] EquipmentServer.Instance is null"); return; }
            var mi = t.GetMethod("RequestUnequipRpc");
            if (mi == null) { Debug.LogWarning("[InventoryTab] RequestUnequipRpc not found"); return; }
            var rpcParams = System.Activator.CreateInstance(typeof(Unity.Netcode.RpcParams));
            mi.Invoke(inst, new object[] { slot, rpcParams });
            Debug.Log($"[InventoryTab] RequestUnequipRpc: slot={slot} name={itemName}");
        }

        // ============================================================
        // Detail panel
        // ============================================================

        private void OnInventorySelectionChanged(System.Collections.Generic.IEnumerable<object> selectedItems)
        {
            if (selectedItems == null) return;
            if (_inventoryList == null) return;
            int selectedIdx = _inventoryList.selectedIndex;
            if (selectedIdx < 0 || selectedIdx >= _inventoryCache.Count)
            {
                ClearInventoryDetail();
                return;
            }
            var item = _inventoryCache[selectedIdx];
            UpdateInventoryDetail(item);
        }

        private void ClearInventoryDetail()
        {
            if (_invDetailName != null) _invDetailName.text = "Выберите предмет слева";
            if (_invDetailType != null) _invDetailType.text = "—";
            if (_invDetailWeight != null) _invDetailWeight.text = "—";
            if (_invDetailStat != null) _invDetailStat.text = "—";
            if (_invDetailDesc != null) _invDetailDesc.text = "—";
        }

        private void UpdateInventoryDetail(InventoryListItem item)
        {
            // Резолвим ItemData
            ProjectC.Items.ItemData def = null;
            if (int.TryParse(item.itemId, out int parsedId))
            {
                def = ProjectC.Items.InventoryWorld.Instance?.GetItemDefinition(parsedId);
            }

            if (_invDetailName != null) _invDetailName.text = item.displayName;
            if (_invDetailType != null) _invDetailType.text = $"Тип: {ItemTypeNames.GetDisplayName(item.type)}";
            if (_invDetailWeight != null) _invDetailWeight.text = $"Вес: {(def != null ? def.weightKg : 0):F1} кг";
            if (_invDetailDesc != null)
                _invDetailDesc.text = def != null && !string.IsNullOrEmpty(def.description) ? def.description : "—";

            // Stat bonuses для ClothingItemData / ModuleItemData
            if (_invDetailStat != null)
            {
                if (def is ProjectC.Equipment.ClothingItemData c)
                {
                    string sb = "Бонусы: ";
                    if (c.strengthBonus != 0) sb += $"STR {(c.strengthBonus >= 0 ? "+" : "")}{c.strengthBonus:F0} ";
                    if (c.dexterityBonus != 0) sb += $"DEX {(c.dexterityBonus >= 0 ? "+" : "")}{c.dexterityBonus:F0} ";
                    if (c.intelligenceBonus != 0) sb += $"INT {(c.intelligenceBonus >= 0 ? "+" : "")}{c.intelligenceBonus:F0} ";
                    if (c.strengthMultiplier != 0) sb += $"\nSTR ×{c.strengthMultiplier:F2} ";
                    if (c.dexterityMultiplier != 0) sb += $"\nDEX ×{c.dexterityMultiplier:F2} ";
                    if (c.intelligenceMultiplier != 0) sb += $"\nINT ×{c.intelligenceMultiplier:F2} ";
                    _invDetailStat.text = sb;
                }
                else if (def is ProjectC.Equipment.ModuleItemData m)
                {
                    string sb = "Бонусы: ";
                    if (m.strengthBonus != 0) sb += $"STR {(m.strengthBonus >= 0 ? "+" : "")}{m.strengthBonus:F0} ";
                    if (m.dexterityBonus != 0) sb += $"DEX {(m.dexterityBonus >= 0 ? "+" : "")}{m.dexterityBonus:F0} ";
                    if (m.intelligenceBonus != 0) sb += $"INT {(m.intelligenceBonus >= 0 ? "+" : "")}{m.intelligenceBonus:F0} ";
                    if (m.weaponDamageBonus != 0) sb += $"\nWeapon DMG +{m.weaponDamageBonus:F0}";
                    if (m.sensorRangeBonus != 0) sb += $"\nSensor +{m.sensorRangeBonus:F0}";
                    if (m.craftingSpeedMultiplier != 0) sb += $"\nCraft ×{m.craftingSpeedMultiplier:F2}";
                    _invDetailStat.text = sb;
                }
                else
                {
                    _invDetailStat.text = "—";
                }
            }
        }

        // ============================================================
        // Snapshot/Result handlers (для подписки)
        // ============================================================

        private void HandleInventorySnapshotUpdated(InventorySnapshotDto snap)
        {
            HandleSnapshotUpdated(snap);
        }

        private void HandleInventoryResultReceived(InventoryResultDto result)
        {
            HandleResultReceived(result);
        }
    }
}
