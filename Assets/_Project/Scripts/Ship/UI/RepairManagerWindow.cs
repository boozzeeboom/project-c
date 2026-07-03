// =====================================================================================
// RepairManagerWindow.cs — UI Toolkit окно ремонтного менеджера (док)
// =====================================================================================
// Паттерн: канон из docs/UI/UI_TOOLKIT_GUIDE.md §3.
//
// Функции:
//   - Выбор корабля игрока (по ключам в инвентаре)
//   - Просмотр слотов модулей выбранного корабля
//   - Список совместимых модулей из каталога (ModuleShopDatabase)
//   - Установка / снятие модуля через ShipModuleServer RPC
// =====================================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Player;
using ProjectC.Ship.Key;

namespace ProjectC.Ship.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class RepairManagerWindow : MonoBehaviour
    {
        public static RepairManagerWindow Instance { get; private set; }

        [Header("UI Assets (назначить в Inspector ОБЯЗАТЕЛЬНО)")]
        [SerializeField] private VisualTreeAsset repairUxml;
        [SerializeField] private StyleSheet repairUss;

        [Header("Каталог модулей")]
        [SerializeField] private ModuleShopDatabase shopDatabase;

        // UI refs (инициализируются в EnsureBuilt)
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _container;
        private Button _closeBtn;
        private VisualElement _shipDropdownContainer;
        private Label _shipClassLabel;
        private Label _shipPowerLabel;
        private VisualElement _slotsContainer;
        private VisualElement _modulesContainer;
        private Label _modulesHeader;
        private Label _creditsLabel;
        private Label _statusLabel;

        // State
        private bool _built;
        private ModuleShopDatabase _activeDatabase;

        // Корабли игрока
        private readonly List<int> _keyInstanceIds = new List<int>();
        private readonly Dictionary<int, ShipController> _shipByKeyId = new Dictionary<int, ShipController>();
        private int _selectedKeyId = -1;

        // Выбранный слот
        private string _selectedSlotName;

        // IsOpen property
        public bool IsOpen { get; private set; }

        // ============================================================
        // Lifecycle (канон §3)
        // ============================================================

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            // UXML fallback на Resources (VisualTreeAsset работает)
            if (repairUxml == null)
                repairUxml = Resources.Load<VisualTreeAsset>("UI/RepairManagerWindow");
            // USS fallback НЕ делаем — см. UI_TOOLKIT_GUIDE.md §2 Ошибка 1
        }

        private void OnEnable()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            EnsureBuilt();
        }

        private void OnDisable()
        {
            if (IsOpen)
                SetOpen(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!IsOpen || !_built) return;

            // ESC — закрыть окно (паттерн из BUGS_PHASE_2.md: каждый window сам обрабатывает ESC)
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
            }
        }

        // ============================================================
        // Build (канон §3)
        // ============================================================

        private void EnsureBuilt()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null || _doc.rootVisualElement == null) return;

            if (repairUxml == null)
            {
                Debug.LogError("[RepairManagerWindow] UXML не назначен ни в Inspector, ни в Resources/UI/", this);
                return;
            }

            // ✅ Используем rootVisualElement от UIDocument — он САМ подгрузил UXML
            _root = _doc.rootVisualElement;

            // ✅ Добавляем USS ОДИН раз
            if (repairUss != null && !_root.styleSheets.Contains(repairUss))
                _root.styleSheets.Add(repairUss);

            // ✅ sortingOrder — окно поверх других UI
            _doc.sortingOrder = 10;

            // ✅ Ищем элементы через Q<T>
            _container = _root.Q<VisualElement>("repair-root");
            _closeBtn = _root.Q<Button>("repair-close-btn");
            _shipDropdownContainer = _root.Q<VisualElement>("repair-ship-dropdown-container");
            _shipClassLabel = _root.Q<Label>("repair-ship-class");
            _shipPowerLabel = _root.Q<Label>("repair-ship-power");
            _slotsContainer = _root.Q<VisualElement>("repair-slots-container");
            _modulesContainer = _root.Q<VisualElement>("repair-modules-container");
            _modulesHeader = _root.Q<Label>("repair-modules-header");
            _creditsLabel = _root.Q<Label>("repair-credits-label");
            _statusLabel = _root.Q<Label>("repair-status-label");

            // De-dup подписок
            if (_closeBtn != null)
            {
                _closeBtn.clicked -= OnCloseClicked;
                _closeBtn.clicked += OnCloseClicked;
            }

            _built = true;

            // ✅ Окно СКРЫТО по умолчанию
            SetOpen(false);

            // Инициализировать каталог модулей
            if (shopDatabase != null)
                ShipModuleCatalog.Initialize(shopDatabase);

            Debug.Log($"[RepairManagerWindow] Built: rootVE.children={_root.childCount}, styleSheets={_root.styleSheets.count}");
        }

        // ============================================================
        // Show / Hide (канон §3 — SetOpen+IsOpen+pickingMode+cursor)
        // ============================================================

        public void SetOpen(bool open)
        {
            if (!_built) EnsureBuilt();
            if (!_built) return;

            var target = _container != null ? _container : _root;
            if (target != null)
            {
                target.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                target.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            }

            IsOpen = open;

            // Cursor
            if (open)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else
            {
                // Восстанавливаем locked только если в игре (не в главном меню)
                if (Unity.Netcode.NetworkManager.Singleton != null &&
                    Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }

        public void Show(ModuleShopDatabase database)
        {
            _activeDatabase = database ?? shopDatabase;

            if (_activeDatabase != null)
                ShipModuleCatalog.Initialize(_activeDatabase);

            RefreshShipList();
            SetOpen(true);
        }

        public void Hide() => SetOpen(false);

        public bool IsVisible() => IsOpen;

        private void OnCloseClicked() => Hide();

        /// <summary>Предвыбрать корабль (вызывается из MyShipsTab).</summary>
        public void PreselectShip(int keyInstanceId)
        {
            _selectedKeyId = keyInstanceId;
        }

        // ============================================================
        // Ship List
        // ============================================================

        private void RefreshShipList()
        {
            _keyInstanceIds.Clear();
            _shipByKeyId.Clear();

            ulong myId = Unity.Netcode.NetworkManager.Singleton != null
                ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;

            var invWorld = ProjectC.Items.InventoryWorld.Instance;
            if (invWorld != null)
            {
                var myShips = invWorld.GetMyShips(myId);
                foreach (var (instanceId, shipNetId) in myShips)
                {
                    if (!Unity.Netcode.NetworkManager.Singleton.SpawnManager.SpawnedObjects
                        .TryGetValue(shipNetId, out var netObj)) continue;
                    var sc = netObj.GetComponent<ShipController>();
                    if (sc == null) continue;

                    _keyInstanceIds.Add(instanceId);
                    _shipByKeyId[instanceId] = sc;
                }
            }

            // Fallback: через KeyRodInstanceWorld
            if (_keyInstanceIds.Count == 0 && KeyRodInstanceWorld.IsInitialized)
            {
                var instanceIds = KeyRodInstanceWorld.GetInstancesForPlayer(myId);
                foreach (int id in instanceIds)
                {
                    var inst = KeyRodInstanceWorld.GetInstance(id);
                    if (inst == null || inst.registeredShipId == 0) continue;
                    if (!Unity.Netcode.NetworkManager.Singleton.SpawnManager.SpawnedObjects
                        .TryGetValue(inst.registeredShipId, out var netObj)) continue;
                    var sc = netObj.GetComponent<ShipController>();
                    if (sc == null) continue;

                    _keyInstanceIds.Add(id);
                    _shipByKeyId[id] = sc;
                }
            }

            BuildShipDropdown();

            // Автовыбор
            if (_keyInstanceIds.Count > 0)
            {
                int idx = _selectedKeyId > 0 ? _keyInstanceIds.IndexOf(_selectedKeyId) : -1;
                if (idx < 0) idx = 0;
                SelectShip(idx);
            }
            else
            {
                _selectedKeyId = -1;
                ClearShipView();
            }
        }

        private void BuildShipDropdown()
        {
            if (_shipDropdownContainer == null) return;
            _shipDropdownContainer.Clear();

            var dd = new CustomDropdown();
            var choices = new List<string>();
            foreach (int id in _keyInstanceIds)
            {
                if (_shipByKeyId.TryGetValue(id, out var sc))
                    choices.Add($"🚀 {ResolveDisplayName(sc)}");
                else
                    choices.Add($"🔑 Key #{id}");
            }
            dd.SetChoices(choices, -1);
            dd.OnSelectionChanged += (idx) =>
            {
                if (idx >= 0 && idx < _keyInstanceIds.Count)
                    SelectShip(idx);
            };

            _shipDropdownContainer.Add(dd);
        }

        /// <summary>Кастомный дропдаун как в MyShipsTab.</summary>
        private class CustomDropdown : VisualElement
        {
            private readonly PopupElement _popup;
            private readonly Label _label;
            private int _selectedIdx = -1;
            private List<string> _choices = new List<string>();

            public int SelectedIndex => _selectedIdx;
            public System.Action<int> OnSelectionChanged;

            public CustomDropdown()
            {
                AddToClassList("repair-dropdown");
                _label = new Label("Выберите корабль...");
                _label.AddToClassList("repair-dropdown-label");
                Add(_label);

                _popup = new PopupElement();
                _popup.style.display = DisplayStyle.None;

                _label.RegisterCallback<ClickEvent>(evt =>
                {
                    _popup.style.display = _popup.style.display == DisplayStyle.Flex
                        ? DisplayStyle.None : DisplayStyle.Flex;
                });
            }

            public void SetChoices(List<string> choices, int defaultIdx)
            {
                _choices = choices;
                _popup.Clear();
                for (int i = 0; i < choices.Count; i++)
                {
                    int idx = i;
                    var item = new Label(choices[i]);
                    item.AddToClassList("repair-dropdown-item");
                    item.RegisterCallback<ClickEvent>(evt =>
                    {
                        _selectedIdx = idx;
                        _label.text = choices[idx];
                        _popup.style.display = DisplayStyle.None;
                        OnSelectionChanged?.Invoke(idx);
                    });
                    _popup.Add(item);
                }
                if (defaultIdx >= 0 && defaultIdx < choices.Count)
                {
                    _selectedIdx = defaultIdx;
                    _label.text = choices[defaultIdx];
                }
            }

            public void Cleanup() { _popup.Clear(); }
        }

        private class PopupElement : VisualElement
        {
            public PopupElement()
            {
                AddToClassList("repair-dropdown-popup");
            }
        }

        // ============================================================
        // Ship Selection
        // ============================================================

        private void SelectShip(int index)
        {
            if (index < 0 || index >= _keyInstanceIds.Count) return;
            _selectedKeyId = _keyInstanceIds[index];
            RenderShip();
        }

        private void RenderShip()
        {
            if (!_shipByKeyId.TryGetValue(_selectedKeyId, out var sc) || sc == null) return;

            if (_shipClassLabel != null)
                _shipClassLabel.text = $"Класс: {sc.ShipFlightClass}";

            var mm = sc.ShipModuleManager;
            if (_shipPowerLabel != null && mm != null)
                _shipPowerLabel.text = $"Энергия: {mm.currentPowerUsage}/{mm.availablePower}";

            RenderSlots(sc);
            _selectedSlotName = null;
            ClearModulesView();
        }

        private void ClearShipView()
        {
            if (_shipClassLabel != null) _shipClassLabel.text = "Класс: —";
            if (_shipPowerLabel != null) _shipPowerLabel.text = "Энергия: —";
            if (_slotsContainer != null) _slotsContainer.Clear();
            ClearModulesView();
        }

        // ============================================================
        // Slots
        // ============================================================

        private void RenderSlots(ShipController sc)
        {
            if (_slotsContainer == null) return;
            _slotsContainer.Clear();

            var mm = sc.ShipModuleManager;
            if (mm == null || mm.slots == null || mm.slots.Count == 0)
            {
                var empty = new Label("Слоты не найдены");
                empty.AddToClassList("repair-empty-label");
                _slotsContainer.Add(empty);
                return;
            }

            foreach (var slot in mm.slots)
            {
                if (slot == null) continue;

                var row = new VisualElement();
                row.AddToClassList("repair-slot-row");

                string slotName = slot.gameObject.name;
                bool occupied = slot.isOccupied;
                string modName = occupied ? slot.installedModule.displayName : "пусто";

                var nameLbl = new Label($"{slotName}: {modName}");
                nameLbl.AddToClassList("repair-slot-name");
                if (!occupied) nameLbl.AddToClassList("repair-slot-empty");
                row.Add(nameLbl);

                var btnRow = new VisualElement();
                btnRow.AddToClassList("repair-slot-btns");

                if (occupied)
                {
                    var removeBtn = new Button(() => OnRemoveClicked(slotName));
                    removeBtn.text = "Снять";
                    removeBtn.AddToClassList("repair-btn");
                    removeBtn.AddToClassList("repair-btn-remove");
                    btnRow.Add(removeBtn);
                }

                var selectBtn = new Button(() => OnSlotSelected(slotName));
                selectBtn.text = "Выбрать";
                selectBtn.AddToClassList("repair-btn");
                selectBtn.AddToClassList("repair-btn-select");
                btnRow.Add(selectBtn);

                row.Add(btnRow);
                _slotsContainer.Add(row);
            }
        }

        private void OnSlotSelected(string slotName)
        {
            _selectedSlotName = slotName;
            RenderCompatibleModules(slotName);
        }

        private void OnRemoveClicked(string slotName)
        {
            if (_selectedKeyId <= 0) return;

            if (!_shipByKeyId.TryGetValue(_selectedKeyId, out var sc)) return;
            var server = sc.GetComponent<ShipModuleServer>();
            if (server != null)
            {
                server.RequestRemoveModule(_selectedKeyId, slotName);
                if (_statusLabel != null)
                    _statusLabel.text = $"Запрос на снятие модуля из '{slotName}' отправлен...";
            }
            else
            {
                Debug.LogWarning("[RepairManagerWindow] ShipModuleServer not found on ship");
            }
        }

        // ============================================================
        // Compatible Modules
        // ============================================================

        private void RenderCompatibleModules(string slotName)
        {
            if (_modulesContainer == null) return;
            _modulesContainer.Clear();

            if (_modulesHeader != null)
                _modulesHeader.text = $"Модули для слота '{slotName}':";

            if (_activeDatabase == null)
            {
                var err = new Label("База модулей не задана.");
                err.AddToClassList("repair-empty-label");
                _modulesContainer.Add(err);
                return;
            }

            if (!_shipByKeyId.TryGetValue(_selectedKeyId, out var sc))
            {
                _modulesContainer.Add(new Label("Корабль не выбран."));
                return;
            }

            var mm = sc.ShipModuleManager;
            ModuleSlot targetSlot = null;
            if (mm != null)
            {
                foreach (var s in mm.slots)
                {
                    if (s != null && s.gameObject.name == slotName) { targetSlot = s; break; }
                }
            }

            bool any = false;
            foreach (var entry in _activeDatabase.entries)
            {
                if (entry == null || entry.module == null) continue;

                var mod = entry.module;

                if (targetSlot != null && !targetSlot.ValidateCompatibility(mod))
                    continue;

                if (!mod.IsCompatibleWithClass(sc.ShipFlightClass))
                    continue;

                if (targetSlot != null && targetSlot.isOccupied &&
                    targetSlot.installedModuleId == mod.moduleId)
                    continue;

                any = true;

                var row = new VisualElement();
                row.AddToClassList("repair-module-row");

                string tierStr = new string('★', mod.tier);
                var tierLbl = new Label(tierStr);
                tierLbl.AddToClassList("repair-module-tier");
                row.Add(tierLbl);

                var infoCol = new VisualElement();
                infoCol.AddToClassList("repair-module-info");

                var nameLbl = new Label(mod.displayName);
                nameLbl.AddToClassList("repair-module-name");
                infoCol.Add(nameLbl);

                string priceStr = $"💰 {entry.costCredits} кр.";
                if (entry.requiredResources != null && entry.requiredResources.Length > 0)
                {
                    var resParts = new List<string>();
                    foreach (var rr in entry.requiredResources)
                        resParts.Add($"{rr.itemId} ×{rr.amount}");
                    priceStr += " + " + string.Join(", ", resParts);
                }

                if (mod.powerConsumption > 0)
                {
                    int avail = mm != null ? mm.GetAvailablePower() : 0;
                    priceStr += $" ⚡ {mod.powerConsumption} (доступно {avail})";
                }

                var priceLbl = new Label(priceStr);
                priceLbl.AddToClassList("repair-module-price");
                infoCol.Add(priceLbl);

                row.Add(infoCol);

                var installBtn = new Button(() => OnInstallClicked(slotName, mod.moduleId));
                installBtn.text = "Установить";
                installBtn.AddToClassList("repair-btn");
                installBtn.AddToClassList("repair-btn-install");

                int neededPower = mod.powerConsumption;
                int availPower = mm != null ? mm.GetAvailablePower() : 0;
                if (targetSlot != null && targetSlot.isOccupied)
                    availPower += targetSlot.installedModule.powerConsumption;

                if (neededPower > availPower)
                {
                    installBtn.SetEnabled(false);
                    installBtn.tooltip = "Недостаточно энергии";
                }

                row.Add(installBtn);
                _modulesContainer.Add(row);
            }

            if (!any)
            {
                var none = new Label("Нет совместимых модулей для этого слота.");
                none.AddToClassList("repair-empty-label");
                _modulesContainer.Add(none);
            }
        }

        private void OnInstallClicked(string slotName, string moduleId)
        {
            if (_selectedKeyId <= 0) return;

            if (!_shipByKeyId.TryGetValue(_selectedKeyId, out var sc)) return;
            var server = sc.GetComponent<ShipModuleServer>();
            if (server != null)
            {
                server.RequestInstallModule(_selectedKeyId, slotName, moduleId);
                if (_statusLabel != null)
                    _statusLabel.text = $"Запрос на установку '{moduleId}' в '{slotName}' отправлен...";
            }
            else
            {
                Debug.LogWarning("[RepairManagerWindow] ShipModuleServer not found on ship");
            }
        }

        private void ClearModulesView()
        {
            if (_modulesContainer != null) _modulesContainer.Clear();
            if (_modulesHeader != null) _modulesHeader.text = "Доступные модули:";
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static string ResolveDisplayName(ShipController sc)
        {
            if (sc == null) return "—";
            string n = sc.CustomDisplayName;
            if (!string.IsNullOrEmpty(n)) return n;
            return sc.GetType().Name;
        }
    }
}
