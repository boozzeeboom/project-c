using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Admin
{
    /// <summary>
    /// T-ADM-04: runtime-окно админ-панели (D5 — 8 вкладок, D6 — тоггл F12).
    /// UI построен кодом (UXML нет — см. память проекта: programmatic UI only when no UXML exists).
    /// Паттерн спавна — CharacterWindow: синглтон + Resources.Load fallback
    /// (PanelSettings берём из UI/EscMenuPanelSettings, иначе runtime-дефолт).
    /// Чтение F12 — прямое (Keyboard + legacy Input), как старые HUD-тогглы:
    /// осознанное отклонение от IsActionJustPressed (слот AdminPanel в конфиге
    /// оставлен для KeybindingsWindow-отображения и будущего ребинда L3).
    /// Все действия — через AdminFacade, логики в окне нет.
    /// </summary>
    public class AdminRuntimeWindow : MonoBehaviour
    {
        public static AdminRuntimeWindow Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _tabBar;
        private ScrollView _content;
        private Label _statusLabel;
        private string _currentTab = "fly";
        private float _slowTimer;

        // Поля телепорта (пересоздаются с вкладкой — значения держим здесь).
        private string _tpX = "0";
        private string _tpY = "500";
        private string _tpZ = "0";
        private string _peakIndex = "0";
        private string _timeOfDay = "12";

        private static readonly string[] Tabs =
        {
            "fly", "teleport", "respawn", "hud", "world", "route", "logs", "saves"
        };

        private static readonly Dictionary<string, string> TabTitles = new Dictionary<string, string>
        {
            { "fly", "Полёт" }, { "teleport", "Телепорт" }, { "respawn", "Респавн" },
            { "hud", "HUD" }, { "world", "Мир" }, { "route", "Маршрут" },
            { "logs", "Логи" }, { "saves", "Сейвы" }
        };

        public static AdminRuntimeWindow EnsureExists()
        {
            if (Instance != null) return Instance;
            var found = FindAnyObjectByType<AdminRuntimeWindow>();
            if (found != null) return found;
            var go = new GameObject("AdminWindow");
            DontDestroyOnLoad(go);
            return go.AddComponent<AdminRuntimeWindow>();
        }

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
            SetVisible(false);
        }

        private void Update()
        {
            bool f12 = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
                f12 = UnityEngine.InputSystem.Keyboard.current.f12Key.wasPressedThisFrame;
#else
            f12 = Input.GetKeyDown(KeyCode.F12);
#endif
            if (f12) SetVisible(!IsVisible);

            if (!IsVisible) return;
            _slowTimer += Time.unscaledDeltaTime;
            if (_slowTimer >= 0.5f)
            {
                _slowTimer = 0f;
                RefreshStatus();
            }
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            // Курсор — по аналогии с CraftingWindow/CustomisationWindow/CommPanelWindow:
            // открыто = свободная мышь, закрыто = лок обратно в игру (только если сеть слушаем).
            if (visible)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                SwitchTab(_currentTab);
            }
            else
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }

        // ==================== Построение ====================

        private void BuildUI()
        {
            _doc = gameObject.AddComponent<UIDocument>();
            var settings = Resources.Load<UnityEngine.UIElements.PanelSettings>("UI/EscMenuPanelSettings");
            if (settings == null) settings = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
            _doc.panelSettings = settings;
            _doc.sortingOrder = 100;

            _root = new VisualElement();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.style.flexDirection = FlexDirection.Row;
            _root.style.justifyContent = Justify.FlexEnd;
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            var panel = new VisualElement();
            panel.style.width = 400;
            panel.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.94f);
            panel.style.borderLeftWidth = 2;
            panel.style.borderLeftColor = new Color(0.2f, 0.8f, 1f, 1f);
            panel.style.paddingLeft = 6; panel.style.paddingRight = 6;
            panel.style.paddingTop = 6; panel.style.paddingBottom = 6;
            panel.pickingMode = PickingMode.Position;
            _root.Add(panel);

            var header = new Label("⚙ Админ-панель (F12)");
            header.style.fontSize = 15;
            header.style.color = Color.cyan;
            header.style.marginBottom = 4;
            panel.Add(header);

            _tabBar = new VisualElement();
            _tabBar.style.flexDirection = FlexDirection.Row;
            _tabBar.style.flexWrap = Wrap.Wrap;
            _tabBar.style.marginBottom = 6;
            panel.Add(_tabBar);
            foreach (var tab in Tabs)
            {
                var id = tab;
                var b = new Button(() => SwitchTab(id)) { text = TabTitles[tab] };
                b.style.flexGrow = 1;
                b.style.fontSize = 12;
                b.style.paddingTop = 2; b.style.paddingBottom = 2;
                b.style.marginRight = 2; b.style.marginBottom = 2;
                _tabBar.Add(b);
            }

            _content = new ScrollView();
            _content.style.flexGrow = 1;
            panel.Add(_content);

            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 12;
            _statusLabel.style.color = new Color(0.7f, 0.9f, 0.7f);
            _statusLabel.style.marginTop = 6;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(_statusLabel);

            var close = new Button(() => SetVisible(false)) { text = "Закрыть (F12)" };
            close.style.marginTop = 4;
            panel.Add(close);
        }

        private void SwitchTab(string id)
        {
            _currentTab = id;
            _content.Clear();
            switch (id)
            {
                case "fly": BuildFlyTab(); break;
                case "teleport": BuildTeleportTab(); break;
                case "respawn": BuildRespawnTab(); break;
                case "hud": BuildHudTab(); break;
                case "world": BuildWorldTab(); break;
                case "route": BuildRouteTab(); break;
                case "logs": BuildLogsTab(); break;
                case "saves": BuildSavesTab(); break;
            }
            RefreshStatus();
        }

        // ==================== Хелперы ====================

        private AdminFacade Facade() => AdminFacade.EnsureExists();

        private Button AddButton(string label, System.Action onClick)
        {
            var b = new Button(() => { onClick?.Invoke(); RefreshStatus(); }) { text = label };
            b.style.fontSize = 13;
            b.style.paddingTop = 2; b.style.paddingBottom = 2;
            b.style.marginBottom = 2;
            _content.Add(b);
            return b;
        }

        private Label AddLabel(string text, int fontSize = 12)
        {
            var l = new Label(text);
            l.style.fontSize = fontSize;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = 2;
            _content.Add(l);
            return l;
        }

        private Toggle AddToggle(string label, bool value, System.Action<bool> onChange)
        {
            var t = new Toggle(label) { value = value };
            t.RegisterValueChangedCallback(e => { onChange?.Invoke(e.newValue); RefreshStatus(); });
            t.style.marginBottom = 3;
            _content.Add(t);
            return t;
        }

        private TextField AddTextRow(string label, string value, System.Action<string> onChange)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 3;
            var l = new Label(label);
            l.style.width = 90;
            row.Add(l);
            var f = new TextField { value = value };
            f.style.flexGrow = 1;
            f.RegisterValueChangedCallback(e => onChange?.Invoke(e.newValue));
            row.Add(f);
            _content.Add(row);
            return f;
        }

        private void RefreshStatus()
        {
            if (_statusLabel == null || Facade() == null) return;
            var cheats = Facade().LocalPlayer() != null
                ? Facade().LocalPlayer().GetComponent<AdminMoveCheats>() : null;
            string move = cheats != null
                ? $"god={(cheats.GodMode ? "ON" : "off")} noclip={(cheats.Noclip ? "ON" : "off")} speed=x{cheats.SpeedMult}"
                : "no player";
            string ngo = Facade().GetNgoSummary();
            if (ngo.Length > 120) ngo = ngo.Substring(0, 120);
            _statusLabel.text = $"[{_currentTab}] {move}\n{ngo}";
        }

        // ==================== Вкладки ====================

        private void BuildFlyTab()
        {
            var cheats = Facade().LocalPlayer() != null
                ? Facade().EnsureMoveCheats() : null;
            if (cheats == null) { AddLabel("Нет локального игрока."); return; }

            AddButton("Переключить полёт камеры (V)", () => Facade().ToggleFly());
            AddToggle("GOD (бессмертие)", cheats.GodMode, v => Facade().SetGod(v));
            AddToggle("NOCLIP (сквозь объекты, WASD+E/Q)", cheats.Noclip, v => Facade().SetNoclip(v));
            AddLabel("Скорость бега:");
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            foreach (float m in new[] { 1f, 2f, 5f, 10f })
            {
                float mult = m;
                var b = new Button(() => Facade().SetSpeedMult(mult)) { text = "x" + mult };
                b.style.flexGrow = 1;
                row.Add(b);
            }
            _content.Add(row);
            AddButton("След. пик (N)", () =>
            {
                var cam = FindAnyObjectByType<ProjectC.Core.WorldCamera>();
                if (cam != null) cam.TeleportToNextPeak();
            });
            AddButton("Пред. пик (B)", () =>
            {
                var cam = FindAnyObjectByType<ProjectC.Core.WorldCamera>();
                if (cam != null) cam.TeleportToPreviousPeak();
            });
            AddButton("Случайный пик (R)", () =>
            {
                var cam = FindAnyObjectByType<ProjectC.Core.WorldCamera>();
                if (cam != null) cam.TeleportToRandomPeak();
            });
        }

        private void BuildTeleportTab()
        {
            AddTextRow("X:", _tpX, v => _tpX = v);
            AddTextRow("Y:", _tpY, v => _tpY = v);
            AddTextRow("Z:", _tpZ, v => _tpZ = v);
            AddButton("Взять текущие координаты", () =>
            {
                var p = Facade().LocalPlayer();
                if (p == null) return;
                var pos = p.transform.position;
                _tpX = pos.x.ToString("F1"); _tpY = pos.y.ToString("F1"); _tpZ = pos.z.ToString("F1");
                SwitchTab("teleport");
            });
            AddButton("ТЕЛЕПОРТ (сервер если host, иначе локально)", () =>
            {
                if (!float.TryParse(_tpX, out float x)) return;
                if (!float.TryParse(_tpY, out float y)) return;
                if (!float.TryParse(_tpZ, out float z)) return;
                var pos = new Vector3(x, y, z);
                var p = Facade().LocalPlayer();
                if (p != null && Facade().AdminTeleportPlayer(p.OwnerClientId, pos)) return;
                Facade().TeleportLocalPlayer(pos);
            });
            AddTextRow("Пик №:", _peakIndex, v => _peakIndex = v);
            AddButton("К пику №", () =>
            {
                if (int.TryParse(_peakIndex, out int i)) Facade().TeleportToPeak(i);
            });
        }

        private void BuildRespawnTab()
        {
            var points = Facade().GetRespawnPositions();
            if (points.Count == 0) { AddLabel("RespawnManager не найден или точек нет."); return; }
            for (int i = 0; i < points.Count; i++)
            {
                int idx = i;
                Vector3 pos = points[i];
                AddButton($"#{idx}: {pos} →", () => Facade().TeleportLocalPlayer(pos));
            }
            AddLabel("Телепорт локальный; серверный restore при коннекте не трогаем.", 12);
        }

        private bool _perfVisible;
        private bool _sceneVisible = true;
        private bool _daynightVisible = true;

        private void BuildHudTab()
        {
            AddToggle("Perf HUD", _perfVisible, v => { _perfVisible = v; Facade().SetPerfHud(v); });
            AddToggle("SceneDebug HUD", _sceneVisible, v => { _sceneVisible = v; Facade().SetSceneHud(v); });
            AddToggle("DayNight overlay", _daynightVisible, v => { _daynightVisible = v; Facade().SetDayNightOverlay(v); });
            AddLabel("NGO:", 13);
            AddLabel(Facade().GetNgoSummary(), 12);
        }

        private void BuildWorldTab()
        {
            var slice = FindAnyObjectByType<ProjectC.World.FloatingOrigin.Network.GlobalMotionControlledRebaseSlice>();
            AddLabel(slice != null
                ? $"Rebase: frame={slice.FrameGeneration}, кумулятив={ProjectC.World.FloatingOrigin.Network.GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset}"
                : "RebaseSlice не найден.", 12);
            AddButton("Rebase (F8)", () => Facade().RequestRebase(false));
            AddButton("Rebase + rollback (F9)", () => Facade().RequestRebase(true));
            AddButton("Загрузить чанки здесь", () => Facade().LoadChunksHere());

            var day = FindAnyObjectByType<ProjectC.Core.DayNightController>();
            if (day != null)
            {
                AddLabel($"Время суток: {day.ServerTimeOfDay:F2} (0–24)", 12);
                AddTextRow("Час:", _timeOfDay, v => _timeOfDay = v);
                AddButton("Установить время", () =>
                {
                    if (float.TryParse(_timeOfDay, out float t)) day.SetTimeOfDay(t);
                });
            }
            var storm = FindAnyObjectByType<ProjectC.Core.ServerStormManager>();
            if (storm != null)
                AddLabel($"Шторм глобально: {storm.GlobalStormIntensity:F2}", 12);
        }

        private void BuildRouteTab()
        {
            var state = ProjectC.PeacefulShip.Client.NpcShipClientState.Instance;
            if (state == null) { AddLabel("NpcShipClientState не найден."); return; }
            if (state.VisibleNpcs.Count == 0) AddLabel("NPC-кораблей в поле зрения нет.");
            foreach (var npc in state.VisibleNpcs)
                AddLabel($"{npc.displayName} — {npc.statusDisplay} @ {npc.currentStationId}", 12);
            AddLabel("Рантайм-оверлей линий маршрута — T-ADM-08.", 12);
        }

        private bool _logsMuted;

        private void BuildLogsTab()
        {
            AddToggle("MUTE всех дебаг-логов", _logsMuted, v => { _logsMuted = v; Facade().SetLogsMuted(v); });
            AddLabel("Ставит флаги: NpcSpawner, Player/ShipPositionServer, RespawnTracker, PlayerTarget, LocalDensityBuffer, DayNight, Constellation. Сами поля не удаляются.", 12);
        }

        private string _wipeArmed = "";
        private Label _wipeReport;

        private void BuildSavesTab()
        {
            AddLabel("⚠ Удаление сейвов. Первое нажатие — armed, второе — выполнить.", 12);
            AddWipeButton("Всё", () => Facade().WipeAllSaves());
            AddWipeButton("Позиции", () => Facade().WipePositions());
            AddWipeButton("Инвентарь", () => Facade().WipeInventory());
            AddWipeButton("Прогрессия", () => Facade().WipeProgression());
            AddWipeButton("Кастомизация", () => Facade().WipeCustomisation());
            AddWipeButton("Квесты", () => Facade().WipeQuests());
            AddWipeButton("Скилл-бинды", () => Facade().WipeSkillBindings());
            AddWipeButton("Ключи", () => Facade().WipeKeyInstances());
            AddWipeButton("Время мира", () => Facade().WipeWorldTime());
            AddWipeButton("Торговля", () => Facade().WipeTrade());
            _wipeReport = AddLabel("", 12);
        }

        private void AddWipeButton(string scope, System.Func<string> action)
        {
            var b = new Button(null) { text = $"Wipe: {scope}" };
            b.style.marginBottom = 3;
            b.clicked += () =>
            {
                if (_wipeArmed == scope)
                {
                    _wipeArmed = "";
                    string report = action();
                    if (_wipeReport != null) _wipeReport.text = report;
                    b.text = $"Wipe: {scope}";
                }
                else
                {
                    _wipeArmed = scope;
                    b.text = $"Точно {scope}? Жми ещё раз";
                }
                RefreshStatus();
            };
            _content.Add(b);
        }
    }
}
