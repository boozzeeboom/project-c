// =====================================================================================
// MyShipsTab.cs — вкладка "Мои корабли" в CharacterWindow (R2-SHIP-KEY-003, T-KEY-08)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/26_TKEY08_MYSHIPS_TAB_PLAN.md
//
// Назначение: отображает корабли, доступные игроку по ключам в инвентаре.
// Список кораблей получаем через scene-placed KeyRodInstanceBinding (стабильно
// между рестартами). Актуальное состояние — через ShipTelemetryClientState
// (NetworkVariable, синхронизируется NGO).
// =====================================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UI.Client
{
    /// <summary>UI вкладка "Мои корабли". Вызывается из CharacterWindow при SwitchTab("ship").</summary>
    public class MyShipsTab
    {
        // ===== UI элементы =====
        private DropdownField _selector;
        private Label _emptyLabel;
        private VisualElement _infoPanel;

        private Label _name;
        private Label _class;
        private Label _keyId;
        private Label _fuelText;
        private Label _cargoText;
        private Label _position;
        private Label _state;

        private ProgressBar _fuelBar;
        private ProgressBar _cargoBar;

        private VisualElement _modulesContainer;
        private VisualElement _modulesScroll;

        // ===== Данные =====
        /// <summary>Пары (displayName, itemId) для dropdown.</summary>
        private readonly List<string> _choices = new List<string>();
        /// <summary>Параллельный список itemId (choices[i] ↔ _itemIds[i]).</summary>
        private readonly List<int> _itemIds = new List<int>();
        /// <summary>ShipController, найденные через KeyRodInstanceBinding (по itemId).</summary>
        private readonly Dictionary<int, ProjectC.Player.ShipController> _shipByItemId = new Dictionary<int, ProjectC.Player.ShipController>();

        private int _selectedIndex = -1;
        private bool _isTelemetrySubscribed;
        private bool _isInventorySubscribed;

        // Кэш последнего telemetry snapshot (для throttle: не обновлять UI каждый кадр)
        private ProjectC.Ship.Network.ShipTelemetryState _lastDisplayed;
        private bool _hasLastDisplayed;

        // ===== Lifecycle =====

        /// <summary>Привязывает UI элементы. Вызывается из CharacterWindow.EnsureBuilt().</summary>
        public void BuildUI(CharacterWindow owner, VisualElement root)
        {
            _selector     = root.Q<DropdownField>("ship-selector");
            _emptyLabel   = root.Q<Label>("ship-empty-label");
            _infoPanel    = root.Q<VisualElement>("ship-info");

            _name         = root.Q<Label>("ship-info-name");
            _class        = root.Q<Label>("ship-info-class");
            _keyId        = root.Q<Label>("ship-info-key-id");
            _fuelText     = root.Q<Label>("ship-fuel-text");
            _cargoText    = root.Q<Label>("ship-cargo-text");
            _position     = root.Q<Label>("ship-info-position");
            _state        = root.Q<Label>("ship-info-state");

            _fuelBar      = root.Q<ProgressBar>("ship-fuel-bar");
            _cargoBar     = root.Q<ProgressBar>("ship-cargo-bar");

            _modulesScroll     = root.Q<VisualElement>("ship-modules-scroll");
            _modulesContainer  = root.Q<VisualElement>("ship-modules-container");

            if (_selector != null)
            {
                _selector.choices = _choices;
                _selector.RegisterValueChangedCallback(OnSelectorChanged);
            }

            UpdateVisibility();
        }

        /// <summary>Вызывается из CharacterWindow.SwitchTab когда вкладка становится активной.</summary>
        public void OnTabShown()
        {
            if (_infoPanel != null)
                _infoPanel.style.display = DisplayStyle.Flex;

            if (_infoPanel != null) _infoPanel.MarkDirtyRepaint();

            // Подписка на telemetry (lazy)
            TrySubscribeTelemetry();
            // T-KEY-08 fix: подписка на inventory changes
            TrySubscribeInventory();

            // Перечитать список кораблей (мог измениться инвентарь)
            RefreshShipList();

            Debug.Log("[MyShipsTab] OnTabShown");
        }

        /// <summary>Вызывается из CharacterWindow.SwitchTab когда вкладка скрывается.</summary>
        public void OnTabHidden()
        {
            // Не отписываемся от telemetry — дешёво и нужно для обновления UI при возврате.
        }

        /// <summary>Вызывается из CharacterWindow.OnDisable. Полная отписка.</summary>
        public void Unsubscribe()
        {
            if (_isTelemetrySubscribed)
            {
                var t = ProjectC.Ship.Client.ShipTelemetryClientState.Instance;
                if (t != null)
                    t.OnShipStateChanged -= HandleShipStateChanged;
                _isTelemetrySubscribed = false;
            }
            if (_isInventorySubscribed)
            {
                var inv = ProjectC.Items.Client.InventoryClientState.Instance;
                if (inv != null)
                    inv.OnSnapshotUpdated -= HandleInventorySnapshotUpdated;
                _isInventorySubscribed = false;
            }
        }

        private void TrySubscribeTelemetry()
        {
            if (_isTelemetrySubscribed) return;
            var t = ProjectC.Ship.Client.ShipTelemetryClientState.Instance;
            if (t == null) return;
            t.OnShipStateChanged += HandleShipStateChanged;
            _isTelemetrySubscribed = true;
        }

        /// <summary>T-KEY-08 fix: подписка на изменения инвентаря — обновляет dropdown
        /// при подборе/выбросе ключа в реальном времени, без перезахода во вкладку.</summary>
        private void TrySubscribeInventory()
        {
            if (_isInventorySubscribed) return;
            var inv = ProjectC.Items.Client.InventoryClientState.Instance;
            if (inv == null) return;
            inv.OnSnapshotUpdated += HandleInventorySnapshotUpdated;
            _isInventorySubscribed = true;
        }

        private void HandleInventorySnapshotUpdated(ProjectC.Items.Dto.InventorySnapshotDto snapshot)
        {
            // Снимок обновился — перечитать список кораблей
            RefreshShipList();
        }

        // ===== Ship List Resolution =====

                /// <summary>Обновляет dropdown: перечитывает Key-слоты → находит ShipController
        /// через KeyRodInstanceWorld. Без reflection — прямые вызовы.</summary>
        public void RefreshShipList()
        {
            _choices.Clear();
            _itemIds.Clear();
            _shipByItemId.Clear();

            ulong myId = Unity.Netcode.NetworkManager.Singleton != null
                ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;

            // Priority 1: прямые серверные данные (Host)
            var invWorld = ProjectC.Items.InventoryWorld.Instance;
            if (invWorld != null)
            {
                // Используем public метод GetMyShips
                var myShips = invWorld.GetMyShips(myId);
                foreach (var (instanceId, shipNetId) in myShips)
                {
                    // Найти ShipController по NetworkObjectId
                    if (!Unity.Netcode.NetworkManager.Singleton.SpawnManager.SpawnedObjects
                        .TryGetValue(shipNetId, out var netObj)) continue;
                    var sc = netObj.GetComponent<ProjectC.Player.ShipController>();
                    if (sc == null) continue;

                    string displayName = ResolveShipDisplayName(sc);
                    _choices.Add($"🚀 {displayName}");
                    _itemIds.Add(instanceId);
                    _shipByItemId[instanceId] = sc;

                    Debug.Log($"[MyShipsTab] Корабль добавлен: {displayName} (instanceId={instanceId}, shipNetId={shipNetId})");
                }

                if (_choices.Count > 0)
                {
                    Debug.Log($"[MyShipsTab] Got {_choices.Count} ships from GetMyShips()");
                }
            }

            // Priority 2: если нет серверных данных — snapshot клиента
            if (_choices.Count == 0)
            {
                var invState = ProjectC.Items.Client.InventoryClientState.Instance;
                if (invState != null && invState.CurrentSnapshot.HasValue)
                {
                    var snapshot = invState.CurrentSnapshot.Value;
                    if (snapshot.items != null)
                    {
                        foreach (var it in snapshot.items)
                        {
                            if ((ProjectC.Items.ItemType)it.type != ProjectC.Items.ItemType.Key) continue;
                            if (it.instanceId <= 0) continue;

                            // Найти ShipController через KeyRodInstanceWorld
                            if (ProjectC.Ship.Key.KeyRodInstanceWorld.IsInitialized)
                            {
                                var inst = ProjectC.Ship.Key.KeyRodInstanceWorld.GetInstance(it.instanceId);
                                if (inst != null && inst.state == ProjectC.Ship.Key.KeyRodInstanceState.Active
                                    && inst.registeredShipId != 0)
                                {
                                    if (Unity.Netcode.NetworkManager.Singleton.SpawnManager.SpawnedObjects
                                        .TryGetValue(inst.registeredShipId, out var netObj))
                                    {
                                        var sc = netObj.GetComponent<ProjectC.Player.ShipController>();
                                        if (sc != null)
                                        {
                                            string displayName = ResolveShipDisplayName(sc);
                                            _choices.Add($"🚀 {displayName}");
                                            _itemIds.Add(it.instanceId);
                                            _shipByItemId[it.instanceId] = sc;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Обновить dropdown UI
            if (_selector != null)
            {
                _selector.choices = _choices;
                if (_choices.Count > 0)
                {
                    if (_selectedIndex < 0 || _selectedIndex >= _choices.Count)
                        _selectedIndex = 0;
                    _selector.SetValueWithoutNotify(_choices[_selectedIndex]);
                }
                else
                {
                    _selectedIndex = -1;
                    _selector.SetValueWithoutNotify(string.Empty);
                }
            }

            UpdateVisibility();

            if (_selectedIndex >= 0)
                RenderSelectedShip();

            // Устанавливаем placeholder если пусто
            if (_emptyLabel != null && _choices.Count == 0)
                _emptyLabel.text = "Нет доступных кораблей. Найдите ключ в мире.";

            Debug.Log($"[MyShipsTab] RefreshShipList: {_choices.Count} кораблей");
        }private void UpdateVisibility()
        {
            bool hasShips = _choices.Count > 0;

            if (_selector != null)
                _selector.style.display = hasShips ? DisplayStyle.Flex : DisplayStyle.None;
            if (_emptyLabel != null)
                _emptyLabel.style.display = hasShips ? DisplayStyle.None : DisplayStyle.Flex;
            if (_infoPanel != null)
                _infoPanel.style.display = hasShips ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnSelectorChanged(ChangeEvent<string> evt)
        {
            int idx = _choices.IndexOf(evt.newValue);
            if (idx < 0) return;
            _selectedIndex = idx;
            RenderSelectedShip();
        }

        // ===== Render =====

        private void RenderSelectedShip()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _itemIds.Count) return;
            int itemId = _itemIds[_selectedIndex];
            if (!_shipByItemId.TryGetValue(itemId, out var sc) || sc == null) return;

            // ShipController.TelemetryState getter returns NetworkVariable.Value
            var telemetry = sc.TelemetryState;
            _lastDisplayed = telemetry;
            _hasLastDisplayed = true;

            // Name + class
            string displayName = telemetry.displayName.ToString();
            if (string.IsNullOrEmpty(displayName))
                displayName = ResolveShipDisplayName(sc);
            if (_name != null) _name.text = $"🚀 {displayName}";

            if (_class != null)
                _class.text = string.IsNullOrEmpty(telemetry.className.ToString())
                    ? "Класс: —"
                    : $"Класс: {telemetry.className}";

            if (_keyId != null)
                _keyId.text = $"🔑 Key itemId={itemId}, instanceId={telemetry.keyInstanceId}";

            // Fuel
            if (_fuelBar != null)
            {
                _fuelBar.value = telemetry.fuelNormalized * 100f;
                _fuelBar.title = $"{telemetry.fuelNormalized * 100f:F0}%";
            }
            if (_fuelText != null)
                _fuelText.text = $"Топливо: {telemetry.fuelNormalized * 100f:F1}% ({telemetry.fuelMax:F0} max)";

            // Cargo
            if (_cargoBar != null && telemetry.cargoMax > 0)
            {
                float cargoPct = (float)telemetry.cargoUsed / telemetry.cargoMax * 100f;
                _cargoBar.value = cargoPct;
                _cargoBar.title = $"{cargoPct:F0}%";
            }
            if (_cargoText != null)
                _cargoText.text = telemetry.cargoMax > 0
                    ? $"Груз: {telemetry.cargoUsed}/{telemetry.cargoMax}"
                    : $"Груз: — (нет данных)";

            // Modules
            RenderModules(sc);

            // Position
            if (_position != null)
                _position.text = $"📍 ({telemetry.position.x:F1}, {telemetry.position.y:F1}, {telemetry.position.z:F1})";

            // State
            if (_state != null)
            {
                string stateStr = ResolveShipState(telemetry.state);
                _state.text = $"Состояние: {stateStr}";
            }
        }

        private void RenderModules(ProjectC.Player.ShipController sc)
        {
            if (_modulesContainer == null) return;
            _modulesContainer.Clear();

            // Получить имена модулей через reflection (без прямой зависимости от ShipModuleManager)
            var moduleNames = TryGetModuleNames(sc);
            if (moduleNames == null || moduleNames.Count == 0)
            {
                var row = new Label($"Модулей: {0}");
                row.AddToClassList("ship-info-row");
                _modulesContainer.Add(row);
                return;
            }

            foreach (var name in moduleNames)
            {
                var row = new VisualElement();
                row.AddToClassList("ship-module-row");

                var lbl = new Label(name);
                lbl.AddToClassList("ship-module-name");
                row.Add(lbl);

                _modulesContainer.Add(row);
            }
        }

        private static List<string> TryGetModuleNames(ProjectC.Player.ShipController sc)
        {
            var result = new List<string>();
            if (sc == null) return result;

            // Попытка 1: публичное свойство Modules / InstalledModules
            var prop = sc.GetType().GetProperty("InstalledModules",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (prop != null)
            {
                var val = prop.GetValue(sc) as System.Collections.IEnumerable;
                if (val != null)
                {
                    foreach (var m in val)
                    {
                        if (m == null) continue;
                        string name = TryGetNameField(m);
                        if (!string.IsNullOrEmpty(name)) result.Add(name);
                    }
                    if (result.Count > 0) return result;
                }
            }

            // Попытка 2: поле _modules (List<...>)
            var field = sc.GetType().GetField("_modules",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                var val = field.GetValue(sc) as System.Collections.IEnumerable;
                if (val != null)
                {
                    foreach (var m in val)
                    {
                        if (m == null) continue;
                        string name = TryGetNameField(m);
                        if (!string.IsNullOrEmpty(name)) result.Add(name);
                    }
                }
            }
            return result;
        }

        private static string TryGetNameField(object module)
        {
            var t = module.GetType();
            var nf = t.GetProperty("Name",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (nf != null && nf.PropertyType == typeof(string))
            {
                var v = (string)nf.GetValue(module);
                if (!string.IsNullOrEmpty(v)) return v;
            }
            var sf = t.GetField("name",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (sf != null && sf.FieldType == typeof(string))
            {
                var v = (string)sf.GetValue(module);
                if (!string.IsNullOrEmpty(v)) return v;
            }
            var df = t.GetProperty("displayName",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (df != null)
            {
                var v = df.GetValue(module)?.ToString();
                if (!string.IsNullOrEmpty(v)) return v;
            }
            // Последний fallback — type name
            return t.Name;
        }

        private void HandleShipStateChanged(ulong shipNetId)
        {
            // Если выбран другой корабль — игнор
            if (_selectedIndex < 0 || _selectedIndex >= _itemIds.Count) return;
            int itemId = _itemIds[_selectedIndex];
            if (!_shipByItemId.TryGetValue(itemId, out var sc) || sc == null) return;
            if (sc.NetworkObjectId != shipNetId) return;

            // Получить текущее состояние через ShipController (NetworkVariable.Value)
            var currentState = sc.TelemetryState;

            // Throttle: если fuel/cargo изменились незначительно — пропускаем
            if (_hasLastDisplayed && ShipTelemetryStateEqualsApprox(_lastDisplayed, currentState)) return;
            _lastDisplayed = currentState;
            _hasLastDisplayed = true;

            RenderSelectedShip();
        }

        private static bool ShipTelemetryStateEqualsApprox(
            ProjectC.Ship.Network.ShipTelemetryState a,
            ProjectC.Ship.Network.ShipTelemetryState b)
        {
            const float eps = 0.01f;
            return Mathf.Abs(a.fuelNormalized - b.fuelNormalized) < eps
                && Mathf.Abs(a.fuelMax - b.fuelMax) < eps
                && a.cargoUsed == b.cargoUsed
                && a.cargoMax == b.cargoMax
                && a.moduleCount == b.moduleCount
                && a.state == b.state
                && Vector3.Distance(a.position, b.position) < 0.1f;
        }

        // ===== Helpers =====

        /// <summary>Имя корабля с fallback через ShipController.CustomDisplayName.</summary>
        private static string ResolveShipDisplayName(ProjectC.Player.ShipController sc)
        {
            if (sc == null) return "—";
            string n = sc.CustomDisplayName;
            if (!string.IsNullOrEmpty(n)) return n;
            return $"{sc.GetType().Name}";
        }

        /// <summary>Декодирует state byte в читаемое имя.</summary>
        private static string ResolveShipState(byte state)
        {
            // ShipState enum (если есть) — пробуем через Enum.ToObject
            var t = System.Type.GetType("ProjectC.Ship.ShipState, Assembly-CSharp");
            if (t != null && System.Enum.IsDefined(t, state))
                return System.Enum.GetName(t, state);
            // Generic byte fallback
            return state == 0 ? "Active" : $"State({state})";
        }
    }
}