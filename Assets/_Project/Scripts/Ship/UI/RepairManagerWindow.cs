// =====================================================================================
// RepairManagerWindow.cs — UI Toolkit окно ремонтного менеджера (док)
// =====================================================================================
// Паттерн: канон из docs/UI/UI_TOOLKIT_GUIDE.md §3.
//
// Функции:
//   - Выбор корабля игрока (по ключам в инвентаре)
//   - Выбор слота модуля из дропдауна
//   - Список совместимых модулей из каталога (занимает основную площадь)
//   - Установка / продажа модуля через ShipModuleServer RPC
//   - Камера наблюдения корабля (ShipObservationCamera)
// =====================================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Player;
using ProjectC.Ship.Key;
using ProjectC.UI.Client;
using ProjectC.Core;

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

        // UI refs
        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _container;
        private Button _closeBtn;
        private VisualElement _shipDropdownContainer;
        private VisualElement _slotDropdownContainer;
        private Label _installedLabel;
        private VisualElement _installedActions;
        private Label _shipClassLabel;
        private Label _shipPowerLabel;
        private Label _hullLabel;          // T-HULL
        private VisualElement _hullBarFill;
        private Button _hullBtn;
        private VisualElement _modulesContainer;
        private Label _modulesHeader;
        private Label _creditsLabel;
        private Label _statusLabel;

        // Camera observation
        private ShipObservationCamera _obsCamera;
        private Camera _playerCam;
        private VisualElement _cameraArrows;

        // Ship repainting
        private int _repaintCost;
        private int _hullRepairCost;
        private Color? _selectedPaintColor;
        private VisualElement _paintSection;
        private Button _paintApplyBtn;
        private Label _paintCostLabel;

        // Ship Recall
        private VisualElement _recallSection;
        private Button _recallBtn;
        private Label _recallCostLabel;
        private int _shipRecallCost = 500;

        // State

        private bool _built;
        private ModuleShopDatabase _activeDatabase;

        // Корабли игрока
        private readonly List<int> _keyInstanceIds = new List<int>();
        private readonly Dictionary<int, ShipController> _shipByKeyId = new Dictionary<int, ShipController>();
        private int _selectedKeyId = -1;

        // Выбранный слот
        private string _selectedSlotName;

        public bool IsOpen { get; private set; }

        // ============================================================
        // Lifecycle
        // ============================================================

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            if (repairUxml == null)
                repairUxml = Resources.Load<VisualTreeAsset>("UI/RepairManagerWindow");

            // Создать камеру наблюдения если ещё нет
            if (_obsCamera == null)
            {
                var camGo = new GameObject("[ShipObservationCamera]");
                camGo.transform.SetParent(transform, false);
                _obsCamera = camGo.AddComponent<ShipObservationCamera>();
            }

            // Кэшировать камеру игрока
            CachePlayerCamera();
        }

        private void OnEnable()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            EnsureBuilt();
        }

        private void OnDisable()
        {
            if (IsOpen) SetOpen(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!IsOpen || !_built) return;

            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
                return;
            }

            // Вращение камеры наблюдения при зажатых стрелках
            HandleCameraArrowHeld();
        }

        // ============================================================
        // Build
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

            _root = _doc.rootVisualElement;

            // USS должен быть и на _root (основное дерево), и на panel.visualTree (попапы дропдаунов)
            if (repairUss != null)
            {
                if (!_root.styleSheets.Contains(repairUss))
                    _root.styleSheets.Add(repairUss);

                var panelRoot = _root.panel?.visualTree;
                if (panelRoot != null && panelRoot != _root && !panelRoot.styleSheets.Contains(repairUss))
                    panelRoot.styleSheets.Add(repairUss);
            }

            _doc.sortingOrder = 10;

            _container = _root.Q<VisualElement>("repair-root");
            _closeBtn = _root.Q<Button>("repair-close-btn");
            _shipDropdownContainer = _root.Q<VisualElement>("repair-ship-dropdown-container");
            _slotDropdownContainer = _root.Q<VisualElement>("repair-slot-dropdown-container");
            _installedLabel = _root.Q<Label>("repair-installed-label");
            _installedActions = _root.Q<VisualElement>("repair-installed-actions");
            _shipClassLabel = _root.Q<Label>("repair-ship-class");
            _shipPowerLabel = _root.Q<Label>("repair-ship-power");
            _hullLabel = _root.Q<Label>("repair-hull-label");
            _hullBarFill = _root.Q<VisualElement>("repair-hull-bar-fill");
            _hullBtn = _root.Q<Button>("repair-hull-btn");
            _modulesContainer = _root.Q<VisualElement>("repair-modules-container");
            _modulesHeader = _root.Q<Label>("repair-modules-header");
            _creditsLabel = _root.Q<Label>("repair-credits-label");
            _statusLabel = _root.Q<Label>("repair-status-label");

            // Camera arrows
            _cameraArrows = _root.Q<VisualElement>("camera-arrows");

            // Paint section
            _paintSection = _root.Q<VisualElement>("repair-paint-section");
            _paintApplyBtn = _root.Q<Button>("repair-paint-apply-btn");
            _paintCostLabel = _root.Q<Label>("repair-paint-cost-label");

            if (_closeBtn != null)

            {
                _closeBtn.clicked -= OnCloseClicked;
                _closeBtn.clicked += OnCloseClicked;
            }

            if (_hullBtn != null)
            {
                _hullBtn.clicked -= OnRepairHullClicked;
                _hullBtn.clicked += OnRepairHullClicked;
            }

            // Ship Recall
            _recallSection = _root.Q<VisualElement>("repair-recall-section");
            _recallBtn = _root.Q<Button>("repair-recall-btn");
            _recallCostLabel = _root.Q<Label>("repair-recall-cost-label");

            if (_recallBtn != null)
            {
                _recallBtn.clicked -= OnRecallShipClicked;
                _recallBtn.clicked += OnRecallShipClicked;
            }

            WireCameraArrows();
            BuildPaintPalette();
            WirePaintApplyButton();

            _built = true;

            SetOpen(false);

            if (shopDatabase != null)
                ShipModuleCatalog.Initialize(shopDatabase);

            Debug.Log($"[RepairManagerWindow] Built: rootVE.children={_root.childCount}, " +
                $"recallSection={(_recallSection != null)}, recallBtn={(_recallBtn != null)}, " +
                $"recallCostLabel={(_recallCostLabel != null)}");
        }

        // ============================================================
        // Show / Hide
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

            if (open)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;

                // Кэшируем камеру игрока при открытии
                CachePlayerCamera();
            }
            else
            {
                // Возвращаем камеру к игроку
                if (_obsCamera != null && _obsCamera.IsActive)
                    _obsCamera.ReturnToPlayer();

                if (Unity.Netcode.NetworkManager.Singleton != null &&
                    Unity.Netcode.NetworkManager.Singleton.IsListening)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }

        public void Show(ModuleShopDatabase database, int repaintCost = 0, int shipRecallCost = 500, int hullRepairCost = 300)
        {
            _activeDatabase = database ?? shopDatabase;
            _repaintCost = repaintCost;
            _shipRecallCost = shipRecallCost;
            _hullRepairCost = hullRepairCost;

            if (_activeDatabase != null)
                ShipModuleCatalog.Initialize(_activeDatabase);

            if (_recallCostLabel != null)
                _recallCostLabel.text = $"{_shipRecallCost} кр.";

            RefreshShipList();
            RefreshCredits();
            SetOpen(true);
        }

        private void RefreshCredits()
        {
            if (_creditsLabel == null) return;
            float credits = 0f;
            var trade = ProjectC.Trade.Core.TradeWorld.Instance;
            if (trade?.Repository != null)
            {
                ulong myId = Unity.Netcode.NetworkManager.Singleton != null
                    ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;
                credits = trade.Repository.GetCredits(myId);
            }
            _creditsLabel.text = $"💰 Кредиты: {credits:F0}";
        }


        public void Hide() => SetOpen(false);

        public bool IsVisible() => IsOpen;

        private void OnCloseClicked() => Hide();

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

        // ============================================================
        // Slot Dropdown
        // ============================================================

        private void BuildSlotDropdown(ShipController sc)
        {
            if (_slotDropdownContainer == null) return;
            _slotDropdownContainer.Clear();

            var mm = sc.ShipModuleManager;
            if (mm == null || mm.slots == null || mm.slots.Count == 0) return;

            var dd = new CustomDropdown();
            var choices = new List<string>();
            var slotNames = new List<string>();
            foreach (var slot in mm.slots)
            {
                if (slot == null) continue;
                string name = slot.gameObject.name;
                string suffix = slot.isOccupied ? $" [✓ {slot.installedModule.displayName}]" : " [пусто]";
                choices.Add($"🔧 {name}{suffix}");
                slotNames.Add(name);
            }

            dd.SetChoices(choices, -1);
            dd.OnSelectionChanged += (idx) =>
            {
                if (idx >= 0 && idx < slotNames.Count)
                    OnSlotSelected(slotNames[idx]);
            };

            _slotDropdownContainer.Add(dd);
        }

        // ============================================================
        // Ship Selection
        // ============================================================

        private void SelectShip(int index)
        {
            if (index < 0 || index >= _keyInstanceIds.Count) return;
            _selectedKeyId = _keyInstanceIds[index];

            // Камера: переключиться на выбранный корабль
            if (_shipByKeyId.TryGetValue(_selectedKeyId, out var sc) && sc != null)
            {
                CachePlayerCamera();
                if (_obsCamera != null && _playerCam != null)
                    _obsCamera.FlyToShip(sc.transform, _playerCam);
            }

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

            UpdateHullInfo(sc);

            BuildSlotDropdown(sc);
            _selectedSlotName = null;
            UpdateInstalledInfo();
            ClearModulesView();
        }

        // ============================================================
        // T-HULL: Hull durability + repair
        // ============================================================

        private void UpdateHullInfo(ShipController sc)
        {
            var hull = sc != null ? sc.Hull : null;

            if (hull == null)
            {
                if (_hullLabel != null) _hullLabel.text = "Прочность: —";
                if (_hullBarFill != null) _hullBarFill.style.width = Length.Percent(0);
                if (_hullBtn != null) _hullBtn.SetEnabled(false);
                return;
            }

            int cur = hull.CurrentHull;
            int max = hull.MaxHull;
            float pct = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;

            if (_hullBarFill != null)
            {
                _hullBarFill.style.width = Length.Percent(pct * 100f);
                Color c;
                if (pct > 0.5f) c = new Color(0.47f, 0.86f, 0.59f, 0.9f);
                else if (pct > 0.25f) c = new Color(0.94f, 0.78f, 0.31f, 0.9f);
                else c = new Color(0.86f, 0.31f, 0.31f, 0.9f);
                _hullBarFill.style.backgroundColor = c;
            }

            if (_hullLabel != null)
            {
                _hullLabel.text = hull.IsBroken
                    ? $"Прочность: СЛОМАН ({cur}/{max})"
                    : $"Прочность: {cur}/{max}";
            }

            if (_hullBtn != null)
            {
                bool needsRepair = cur < max;
                bool isDocked = sc.IsDocked;
                _hullBtn.SetEnabled(needsRepair && isDocked);
                _hullBtn.text = needsRepair
                    ? $"🔧 Починить ({_hullRepairCost} кр.)"
                    : "✓ Целый";
                _hullBtn.tooltip = !isDocked ? "Корабль должен быть в доке" : string.Empty;
            }
        }

        private void OnRepairHullClicked()
        {
            if (_selectedKeyId <= 0) return;
            if (!_shipByKeyId.TryGetValue(_selectedKeyId, out var sc)) return;

            var server = sc.GetComponent<ShipModuleServer>();
            if (server != null)
            {
                server.RequestRepairHull(_selectedKeyId, _hullRepairCost);
                if (_statusLabel != null)
                    _statusLabel.text = "Запрос на ремонт корпуса отправлен...";
                StartCoroutine(DelayedRefresh(0.5f));
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Ship Recall — вызвать корабль на ближайший свободный пад
        // ═══════════════════════════════════════════════════════════

        private void OnRecallShipClicked()
        {
            if (_selectedKeyId <= 0) return;
            if (!_shipByKeyId.TryGetValue(_selectedKeyId, out var sc)) return;

            // Проверить credits
            float credits = 0f;
            var trade = ProjectC.Trade.Core.TradeWorld.Instance;
            if (trade?.Repository != null)
            {
                ulong myId = Unity.Netcode.NetworkManager.Singleton != null
                    ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;
                credits = trade.Repository.GetCredits(myId);
            }

            if (credits < _shipRecallCost)
            {
                if (_statusLabel != null)
                    _statusLabel.text = $"Недостаточно кредитов! Нужно {_shipRecallCost}, есть {credits:F0}";
                return;
            }

            // Найти ближайший свободный пад
            var pads = FindObjectsByType<ProjectC.Docking.Stations.DockingPadTriggerBox>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            ProjectC.Docking.Stations.DockingPadTriggerBox nearestPad = null;
            float nearestDist = float.MaxValue;

            foreach (var pad in pads)
            {
                if (pad.IsShipInside) continue; // занят

                float dist = Vector3.Distance(
                    _playerCam != null ? _playerCam.transform.position : Vector3.zero,
                    pad.transform.position);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestPad = pad;
                }
            }

            if (nearestPad == null)
            {
                if (_statusLabel != null)
                    _statusLabel.text = "Нет свободных падов!";
                return;
            }

            // Отправить RPC на корабль
            if (sc.IsSpawned)
            {
                sc.RecallShipToPadServerRpc(nearestPad.transform.position, _shipRecallCost);
                if (_statusLabel != null)
                    _statusLabel.text = $"Корабль вызван на пад {nearestPad.PadId}...";
                StartCoroutine(DelayedRefresh(1f));
            }
            else
            {
                if (_statusLabel != null)
                    _statusLabel.text = "Корабль не заспавнен.";
            }
        }

        private void ClearShipView()
        {
            if (_shipClassLabel != null) _shipClassLabel.text = "Класс: —";
            if (_shipPowerLabel != null) _shipPowerLabel.text = "Энергия: —";
            if (_hullLabel != null) _hullLabel.text = "Прочность: —";
            if (_hullBarFill != null) _hullBarFill.style.width = Length.Percent(0);
            if (_hullBtn != null) _hullBtn.SetEnabled(false);
            if (_slotDropdownContainer != null) _slotDropdownContainer.Clear();
            if (_installedLabel != null) _installedLabel.text = "Установлено: —";
            if (_installedActions != null) _installedActions.Clear();
            ClearModulesView();
        }

        // ============================================================
        // Installed Module Info + Sell Button
        // ============================================================

        private void UpdateInstalledInfo()
        {
            if (_installedLabel == null || _installedActions == null) return;
            _installedActions.Clear();

            if (string.IsNullOrEmpty(_selectedSlotName) || _selectedKeyId <= 0)
            {
                _installedLabel.text = "Установлено: —";
                return;
            }

            if (!_shipByKeyId.TryGetValue(_selectedKeyId, out var sc)) return;
            var mm = sc.ShipModuleManager;
            if (mm == null) { _installedLabel.text = "Установлено: —"; return; }

            ModuleSlot targetSlot = null;
            foreach (var slot in mm.slots)
            {
                if (slot != null && slot.gameObject.name == _selectedSlotName)
                { targetSlot = slot; break; }
            }

            if (targetSlot == null || !targetSlot.isOccupied)
            {
                _installedLabel.text = "Установлено: пусто";
                return;
            }

            var mod = targetSlot.installedModule;
            _installedLabel.text = $"Установлено: {mod.displayName} (★{mod.tier})";

            // Найти цену продажи
            int sellPrice = ComputeSellPrice(mod.moduleId);

            var sellBtn = new Button(() => OnSellClicked(_selectedSlotName, sellPrice));
            sellBtn.text = $"💰 Продать (+{sellPrice} кр.)";
            sellBtn.AddToClassList("repair-btn");
            sellBtn.AddToClassList("repair-btn-sell");
            _installedActions.Add(sellBtn);
        }

        private int ComputeSellPrice(string moduleId)
        {
            if (_activeDatabase == null) return 0;
            foreach (var mod in _activeDatabase.entries)
            {
                if (mod != null && mod.moduleId == moduleId)
                    return Mathf.Max(1, mod.costCredits / 2);
            }
            return 0;
        }

        private void OnSellClicked(string slotName, int sellCredits)
        {
            if (_selectedKeyId <= 0) return;

            if (!_shipByKeyId.TryGetValue(_selectedKeyId, out var sc)) return;
            var server = sc.GetComponent<ShipModuleServer>();
            if (server != null)
            {
                server.RequestSellModule(_selectedKeyId, slotName, sellCredits);
                if (_statusLabel != null)
                    _statusLabel.text = $"Продажа модуля из '{slotName}' (+{sellCredits} кр.)...";
                StartCoroutine(DelayedRefresh(0.5f));
            }
            else
            {
                Debug.LogWarning("[RepairManagerWindow] ShipModuleServer not found on ship");
            }
        }

        // ============================================================
        // Slot Selection → Modules
        // ============================================================

        private void OnSlotSelected(string slotName)
        {
            _selectedSlotName = slotName;
            UpdateInstalledInfo();
            RenderCompatibleModules(slotName);
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
            foreach (var mod in _activeDatabase.entries)
            {
                if (mod == null) continue;

                // Совместимость со слотом
                if (targetSlot != null && !targetSlot.ValidateCompatibility(mod))
                    continue;

                // Совместимость с классом корабля
                if (!mod.IsCompatibleWithClass(sc.ShipFlightClass))
                    continue;

                // Уже установлен в этом слоте
                if (targetSlot != null && targetSlot.isOccupied &&
                    targetSlot.installedModuleId == mod.moduleId)
                    continue;

                any = true;

                var row = new VisualElement();
                row.AddToClassList("repair-module-row");

                // Tier
                string tierStr = new string('★', mod.tier);
                var tierLbl = new Label(tierStr);
                tierLbl.AddToClassList("repair-module-tier");
                row.Add(tierLbl);

                // Info
                var infoCol = new VisualElement();
                infoCol.AddToClassList("repair-module-info");

                var nameLbl = new Label(mod.displayName);
                nameLbl.AddToClassList("repair-module-name");
                infoCol.Add(nameLbl);

                // Price + power
                string priceStr = $"💰 {mod.costCredits} кр.";
                if (mod.requiredResources != null && mod.requiredResources.Length > 0)
                {
                    var resParts = new List<string>();
                    foreach (var rr in mod.requiredResources)
                        resParts.Add($"{rr.itemId} ×{rr.amount}");
                    priceStr += " + " + string.Join(", ", resParts);
                }

                if (mod.powerConsumption > 0)
                {
                    int avail = mm != null ? mm.GetAvailablePower() : 0;
                    // Если слот занят — старый модуль освободит энергию
                    if (targetSlot != null && targetSlot.isOccupied)
                        avail += targetSlot.installedModule.powerConsumption;
                    priceStr += $" ⚡ {mod.powerConsumption} (свободно {avail})";
                }

                var priceLbl = new Label(priceStr);
                priceLbl.AddToClassList("repair-module-price");
                infoCol.Add(priceLbl);

                row.Add(infoCol);

                // Install button
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
                StartCoroutine(DelayedRefresh(0.5f));
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

        private IEnumerator DelayedRefresh(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (IsOpen && _selectedKeyId > 0)
            {
                RefreshCredits();
                RenderShip();
                if (!string.IsNullOrEmpty(_selectedSlotName))
                    RenderCompatibleModules(_selectedSlotName);
                if (_statusLabel != null)
                    _statusLabel.text = "Готово ✓";
            }
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

        // ============================================================
        // Camera Observation Helpers
        // ============================================================

        /// <summary>Найти и закэшировать камеру игрока (ThirdPersonCamera).</summary>
        private void CachePlayerCamera()
        {
            if (_playerCam != null) return;

            var tpc = FindAnyObjectByType<ThirdPersonCamera>();
            if (tpc != null)
                _playerCam = tpc.GetComponent<Camera>();
        }

        /// <summary>Подписаться на PointerDown/PointerUp для стрелок камеры.</summary>
        private void WireCameraArrows()
        {
            if (_cameraArrows == null) return;

            WireArrowButton("cam-arrow-up");
            WireArrowButton("cam-arrow-down");
            WireArrowButton("cam-arrow-left");
            WireArrowButton("cam-arrow-right");
        }

        private void WireArrowButton(string btnName)
        {
            var btn = _cameraArrows.Q<Button>(btnName);
            if (btn == null) return;

            btn.RegisterCallback<PointerDownEvent>(OnArrowDown, TrickleDown.TrickleDown);
            btn.RegisterCallback<PointerUpEvent>(OnArrowUp, TrickleDown.TrickleDown);
            btn.RegisterCallback<PointerLeaveEvent>(OnArrowLeave, TrickleDown.TrickleDown);
        }

        // Отслеживание зажатых стрелок
        private bool _arrowUpHeld;
        private bool _arrowDownHeld;
        private bool _arrowLeftHeld;
        private bool _arrowRightHeld;

        private void OnArrowDown(PointerDownEvent evt)
        {
            var btn = evt.target as Button;
            if (btn == null) return;
            SetArrowHeld(btn.name, true);
        }

        private void OnArrowUp(PointerUpEvent evt)
        {
            var btn = evt.target as Button;
            if (btn == null) return;
            SetArrowHeld(btn.name, false);
        }

        private void OnArrowLeave(PointerLeaveEvent evt)
        {
            var btn = evt.target as Button;
            if (btn == null) return;
            SetArrowHeld(btn.name, false);
        }

        private void SetArrowHeld(string btnName, bool held)
        {
            switch (btnName)
            {
                case "cam-arrow-up":    _arrowUpHeld = held; break;
                case "cam-arrow-down":  _arrowDownHeld = held; break;
                case "cam-arrow-left":  _arrowLeftHeld = held; break;
                case "cam-arrow-right": _arrowRightHeld = held; break;
            }
        }

        private void HandleCameraArrowHeld()
        {
            if (_obsCamera == null || !_obsCamera.IsActive) return;

            float yawDelta = 0f;
            float pitchDelta = 0f;

            if (_arrowLeftHeld)  yawDelta -= 1f;
            if (_arrowRightHeld) yawDelta += 1f;
            if (_arrowUpHeld)    pitchDelta += 1f;
            if (_arrowDownHeld)  pitchDelta -= 1f;

            if (Mathf.Approximately(yawDelta, 0f) && Mathf.Approximately(pitchDelta, 0f))
                return;

            float speed = 60f; // градусов/сек
            _obsCamera.Rotate(yawDelta * speed * Time.deltaTime, pitchDelta * speed * Time.deltaTime);
        }

        // ============================================================
        // Ship Repainting — Color Palette
        // ============================================================

        private static readonly (string name, Color color)[] PaintPresets =
        {
            ("⚪ Белый",     new Color(0.9f, 0.9f, 0.9f)),
            ("🔘 Серый",     new Color(0.5f, 0.5f, 0.5f)),
            ("⚫ Чёрный",    new Color(0.15f, 0.15f, 0.15f)),
            ("🔴 Красный",   new Color(0.85f, 0.2f, 0.2f)),
            ("🔵 Синий",     new Color(0.2f, 0.4f, 0.85f)),
            ("🟢 Зелёный",   new Color(0.2f, 0.75f, 0.35f)),
            ("🟡 Жёлтый",    new Color(0.9f, 0.8f, 0.15f)),
            ("🟠 Оранжевый", new Color(0.9f, 0.5f, 0.1f)),
            ("🟣 Фиолетовый",new Color(0.6f, 0.2f, 0.8f)),
            ("🔷 Бирюзовый", new Color(0.1f, 0.7f, 0.7f)),
        };

        private void BuildPaintPalette()
        {
            if (_paintSection == null) return;

            // Найти или создать контейнер для кнопок-цветов
            var paletteContainer = _paintSection.Q<VisualElement>("repair-paint-palette");
            if (paletteContainer == null) return;
            paletteContainer.Clear();

            foreach (var (name, color) in PaintPresets)
            {
                var btn = new Button();
                btn.text = name;
                btn.AddToClassList("paint-color-btn");
                btn.style.backgroundColor = new StyleColor(color);
                btn.tooltip = name;
                btn.userData = color;

                btn.clicked += () =>
                {
                    _selectedPaintColor = (Color)btn.userData;
                    UpdatePaintUI();
                };

                paletteContainer.Add(btn);
            }

            UpdatePaintUI();
        }

        private void WirePaintApplyButton()
        {
            if (_paintApplyBtn != null)
            {
                _paintApplyBtn.clicked -= OnPaintClicked;
                _paintApplyBtn.clicked += OnPaintClicked;
            }
        }

        private void UpdatePaintUI()
        {
            if (_paintCostLabel != null)
                _paintCostLabel.text = _repaintCost > 0 ? $"Стоимость: {_repaintCost} кр." : "Бесплатно";

            if (_paintApplyBtn != null)
            {
                _paintApplyBtn.SetEnabled(_selectedPaintColor.HasValue && _selectedKeyId > 0);
                string label = _selectedPaintColor.HasValue ? $"🎨 Покрасить ({_repaintCost} кр.)" : "🎨 Выберите цвет";
                _paintApplyBtn.text = label;
            }

            // Подсветка активного цвета
            var paletteContainer = _paintSection?.Q<VisualElement>("repair-paint-palette");
            if (paletteContainer != null)
            {
                foreach (var child in paletteContainer.Children())
                {
                    var btn = child as Button;
                    if (btn == null) continue;
                    bool isSelected = _selectedPaintColor.HasValue &&
                        ((Color)btn.userData) == _selectedPaintColor.Value;
                    btn.EnableInClassList("paint-color-btn--selected", isSelected);
                }
            }
        }

        private void OnPaintClicked()
        {
            if (!_selectedPaintColor.HasValue || _selectedKeyId <= 0) return;
            if (!_shipByKeyId.TryGetValue(_selectedKeyId, out var sc)) return;

            var server = sc.GetComponent<ShipModuleServer>();
            if (server != null)
            {
                server.RequestRepaintShip(_selectedKeyId, _selectedPaintColor.Value, _repaintCost);
                if (_statusLabel != null)
                    _statusLabel.text = $"Запрос на покраску отправлен...";
                StartCoroutine(DelayedRefresh(0.5f));
            }
            else
            {
                Debug.LogWarning("[RepairManagerWindow] ShipModuleServer not found on ship");
            }
        }
    }
}

