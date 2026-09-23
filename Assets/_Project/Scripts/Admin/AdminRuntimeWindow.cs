using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
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
        private float _routeTimer;

        // Поля телепорта (пересоздаются с вкладкой — значения держим здесь).
        private string _tpX = "0";
        private string _tpY = "500";
        private string _tpZ = "0";
        private string _peakIndex = "0";
        private string _timeOfDay = "12";

        private static readonly string[] Tabs =
        {
            "fly", "teleport", "respawn", "hud", "world", "route", "tests", "logs", "saves"
        };

        private static readonly Dictionary<string, string> TabTitles = new Dictionary<string, string>
        {
            { "fly", "Полёт" }, { "teleport", "Телепорт" }, { "respawn", "Респавн" },
            { "hud", "HUD" }, { "world", "Мир" }, { "route", "Маршрут" },
            { "tests", "Тесты" }, { "logs", "Логи" }, { "saves", "Сейвы" }
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
            // T-ADM-08: живой список маршрутов — пересборка раз в 2с со сохранением скролла.
            if (_currentTab == "route")
            {
                _routeTimer += Time.unscaledDeltaTime;
                if (_routeTimer >= 2f)
                {
                    _routeTimer = 0f;
                    Vector2 scroll = _content != null ? _content.scrollOffset : Vector2.zero;
                    SwitchTab("route");
                    if (_content != null) _content.scrollOffset = scroll;
                }
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
            _content.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
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
            _routeTimer = 0f;
            _content.Clear();
            switch (id)
            {
                case "fly": BuildFlyTab(); break;
                case "teleport": BuildTeleportTab(); break;
                case "respawn": BuildRespawnTab(); break;
                case "hud": BuildHudTab(); break;
                case "world": BuildWorldTab(); break;
                case "route": BuildRouteTab(); break;
                case "tests": BuildTestsTab(); break;
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
            b.style.fontSize = 14;
            b.style.color = Color.white;
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.paddingTop = 3; b.style.paddingBottom = 3;
            b.style.marginBottom = 2;
            _content.Add(b);
            return b;
        }

        private Label AddLabel(string text, int fontSize = 13)
        {
            var l = new Label(text);
            l.style.fontSize = fontSize;
            l.style.color = new Color(0.92f, 0.92f, 0.92f);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = 2;
            _content.Add(l);
            return l;
        }

        private Toggle AddToggle(string label, bool value, System.Action<bool> onChange)
        {
            var t = new Toggle(label) { value = value };
            t.style.fontSize = 14;
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
            f.style.minWidth = 0;
            f.style.fontSize = 14;
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

            AddToggle("GOD (бессмертие)", cheats.GodMode, v => Facade().SetGod(v));
            AddToggle("✈ ПОЛЁТ (WASD+E/Q, Shift×4)", cheats.Noclip, v => Facade().SetNoclip(v));
            AddLabel("Скорость бега:");
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            foreach (float m in new[] { 1f, 2f, 5f, 10f })
            {
                float mult = m;
                var b = new Button(() => Facade().SetSpeedMult(mult)) { text = "x" + mult };
                b.style.flexGrow = 1;
                b.style.fontSize = 13;
                row.Add(b);
            }
            _content.Add(row);

            // Пики: телепорт ИГРОКА (WorldCamera в рантайме нет — см. AdminFacade).
            var peaks = Facade().GetPeaks();
            AddLabel($"Пики ({peaks.Count}):", 13);
            if (peaks.Count == 0)
            {
                AddLabel("WorldGenerator не найден или пиков нет.");
            }
            else
            {
                var nav = new VisualElement();
                nav.style.flexDirection = FlexDirection.Row;
                var prev = new Button(() => PeakStep(-1)) { text = "◀" };
                prev.style.flexGrow = 1; prev.style.fontSize = 14;
                var next = new Button(() => PeakStep(1)) { text = "▶" };
                next.style.flexGrow = 1; next.style.fontSize = 14;
                nav.Add(prev); nav.Add(next);
                _content.Add(nav);
                int show = Mathf.Min(peaks.Count, 15);
                for (int i = 0; i < show; i++)
                {
                    int idx = i;
                    AddButton($"#{idx} {peaks[i].name}", () => Facade().TeleportPlayerToPeak(idx));
                }
                if (peaks.Count > show)
                    AddLabel($"…и ещё {peaks.Count - show} (листай индексом во вкладке Телепорт).", 12);
            }
        }

        private int _peakCursor = -1;

        private void PeakStep(int dir)
        {
            var peaks = Facade().GetPeaks();
            if (peaks.Count == 0) return;
            _peakCursor = (_peakCursor + dir + peaks.Count) % peaks.Count;
            string name = Facade().TeleportPlayerToPeak(_peakCursor);
            if (name != null) _peakIndex = _peakCursor.ToString();
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

        // T-ADM-08: вкладка «Маршрут» — все NPC-корабли + стадия + телепорт 📌.
        // Источники (по приоритету, дедуп по NpcInstanceId):
        //  1) NpcShipZoneRegistry.All — сервер/host, живой реестр контроллеров;
        //  2) FindObjectsByType<NpcShipController> — страховка если реестр пуст;
        //  3) NpcShipClientState.VisibleNpcs — чистый клиент (без transform, только текст).
        // Стадия = NavMode (куда летит прямо сейчас) + NpcShipStatus (FSM) + leg from→to.
        private void BuildRouteTab()
        {
            var controllers = CollectRouteControllers();
            var world = ProjectC.PeacefulShip.Core.NpcShipWorld.Instance;
            var clientState = ProjectC.PeacefulShip.Client.NpcShipClientState.Instance;
            var player = Facade().LocalPlayer();
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            bool hasPlayer = player != null;

            int worldCount = world != null ? world.AllNpcCount : 0;
            AddLabel($"NPC-кораблей: {controllers.Count} (world={worldCount}, клиент-видимых={clientState?.VisibleNpcs.Count ?? 0})", 13);
            AddButton("🔄 Обновить", () => SwitchTab("route"));

            if (controllers.Count == 0 && (clientState == null || clientState.VisibleNpcs.Count == 0))
            {
                AddLabel("NPC-кораблей не найдено: ни контроллеров на сцене, ни записей клиента.");
                AddLabel("Host/сервер: проверь NpcShipServer (BootstrapScene) и schedule у NpcShipController.", 12);
                return;
            }

            foreach (var c in controllers)
            {
                if (c == null) continue;
                ulong id = c.NpcInstanceId;
                var st = world != null ? world.GetNpc(id) : null;
                var sched = world != null ? world.GetSchedule(id) : null;
                int legCount = sched?.routes?.Length ?? 0;

                string fsm = st != null ? st.Status.ToString() : "—";
                string leg = st != null ? $"{st.CurrentRoute.fromLocationId}→{st.CurrentRoute.toLocationId}" : "—";
                string legIdx = st != null && legCount > 0 ? $"leg {st.ScheduleIndex}/{legCount}" : (st != null ? $"leg {st.ScheduleIndex}" : "");
                string mode = c.CurrentMode.ToString();
                if (c.IsPlayerControlled) mode += " (пилот-игрок!)";
                string pad = string.IsNullOrEmpty(c.AssignedPadId) ? "—" : c.AssignedPadId;
                Vector3 pos = c.transform.position;
                float dist = hasPlayer ? Vector3.Distance(playerPos, pos) : -1f;
                string distStr = hasPlayer ? $"{dist:F0}м" : "—";
                string dwell = c.CurrentMode == ProjectC.PeacefulShip.Stations.NpcShipController.NavMode.Docked
                    ? $"dwell={c.DwellTime:F0}с" : "";

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 3;
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = new Color(0.25f, 0.25f, 0.3f, 1f);
                row.style.paddingBottom = 3;

                var info = new Label(
                    $"🚢 {c.gameObject.name} (id={id:X})\n" +
                    $"[{mode}] {fsm} | {leg} {legIdx}\n" +
                    $"@({pos.x:F0},{pos.y:F0},{pos.z:F0}) дист={distStr} пад={pad} {dwell}");
                info.style.fontSize = 12;
                info.style.color = new Color(0.92f, 0.92f, 0.92f);
                info.style.whiteSpace = WhiteSpace.Normal;
                info.style.flexGrow = 1;
                info.style.minWidth = 0;
                row.Add(info);

                var pin = new Button(() =>
                {
                    Facade().TeleportPlayerToNpcShip(c.transform.position, c.gameObject.name);
                    RefreshStatus();
                })
                { text = "📌" };
                pin.style.width = 44;
                pin.style.fontSize = 16;
                pin.tooltip = $"Телепорт к {c.gameObject.name}";
                row.Add(pin);
                _content.Add(row);
            }

            // Чистый клиент: записи без контроллеров (transform нет — телепорт невозможен).
            if (clientState != null)
            {
                foreach (var v in clientState.VisibleNpcs)
                {
                    bool dup = false;
                    foreach (var c in controllers)
                        if (c != null && c.NpcInstanceId == v.npcInstanceId) { dup = true; break; }
                    if (dup) continue;
                    AddLabel($"🚢 {v.displayName} — {v.statusDisplay} @ {v.currentStationId} (только клиент-запись, transform нет)", 12);
                }
            }

            AddLabel("📌 = телепорт игрока к кораблю (+20м вверх, +10м вбок — корабль в поле зрения).", 12);
        }

        /// <summary>Собрать все NpcShipController сцены, дедуп по NpcInstanceId (0 = без id — по ссылке).</summary>
        private System.Collections.Generic.List<ProjectC.PeacefulShip.Stations.NpcShipController> CollectRouteControllers()
        {
            var result = new System.Collections.Generic.List<ProjectC.PeacefulShip.Stations.NpcShipController>();
            var seen = new System.Collections.Generic.HashSet<ulong>();
            foreach (var kv in ProjectC.PeacefulShip.Network.NpcShipZoneRegistry.All)
            {
                var c = kv.Value;
                if (c == null) continue;
                ulong id = c.NpcInstanceId;
                if (id != 0 && !seen.Add(id)) continue;
                result.Add(c);
            }
            var scene = FindObjectsByType<ProjectC.PeacefulShip.Stations.NpcShipController>();
            foreach (var c in scene)
            {
                if (c == null) continue;
                ulong id = c.NpcInstanceId;
                if (id != 0)
                {
                    if (!seen.Add(id)) continue;
                }
                else if (result.Contains(c)) continue;
                result.Add(c);
            }
            result.Sort((a, b) => string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.Ordinal));
            return result;
        }

        // ==================== Вкладка «Тесты» (T-ADM-09) ====================
        // Читает docs/dev/global_needtotest из открытого git по сети —
        // тот же приём что changelog в MainMenuWindow (raw.githubusercontent).
        // Список файлов папки — через GitHub Contents API, сами файлы — по download_url.
        // Только чтение: галочек нет, счётчики считаются из [ ]/[x] в файлах.
        // Состояние Foldout'ов живёт в _testsOpen и переживает пересборки.

        private const string TestsApiUrl =
            "https://api.github.com/repos/boozzeeboom/project-c/contents/docs/dev/global_needtotest?ref=main";

        private Coroutine _testsCoroutine;
        private string _testsStatus = "Не загружено.";
        private readonly Dictionary<string, NeedToTestFile> _testsParsed = new Dictionary<string, NeedToTestFile>();
        private readonly HashSet<string> _testsOpen = new HashSet<string>();
        private VisualElement _testsBody;

        [System.Serializable]
        private struct GitHubContentEntry
        {
            public string name;
            public string type;
            public string download_url;
        }

        [System.Serializable]
        private class GitHubContentList
        {
            public GitHubContentEntry[] items;
        }

        private void BuildTestsTab()
        {
            AddLabel("Ручные тесты из git (docs/dev/global_needtotest). Только чтение.", 12);
            AddLabel(_testsStatus, 12);
            AddButton("🔄 Обновить из GitHub", () => FetchTestsTab());
            _testsBody = new VisualElement();
            _content.Add(_testsBody);
            if (_testsParsed.Count == 0)
            {
                if (_testsCoroutine == null) FetchTestsTab();
                else AddLabelTo(_testsBody, "Загрузка…", 12);
            }
            else RenderTestsTab();
        }

        private void FetchTestsTab()
        {
            if (_testsCoroutine != null) StopCoroutine(_testsCoroutine);
            _testsCoroutine = StartCoroutine(TestsFetchRoutine());
        }

        private IEnumerator TestsFetchRoutine()
        {
            _testsStatus = "Список файлов…";
            RebuildTestsStatus();
            var files = new Dictionary<string, string>(); // fileName → markdown

            // Шаг 1: список .md папки через GitHub Contents API (нужен User-Agent).
            string listError = null;
            using (var req = UnityWebRequest.Get(TestsApiUrl))
            {
                req.timeout = 10;
                req.SetRequestHeader("User-Agent", "ProjectC-AdminPanel");
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    listError = string.IsNullOrEmpty(req.error) ? $"HTTP {req.responseCode}" : req.error;
                else
                    CollectTestsApiEntries(req.downloadHandler.text);
            }

            // Шаг 2: скачать каждый .md по download_url (заглушки без URL пропускаем).
            var urls = new List<KeyValuePair<string, string>>(_testsPendingUrls);
            urls.Sort((a, b) => string.Compare(a.Key, b.Key, System.StringComparison.Ordinal));
            _testsPendingUrls.Clear();
            foreach (var kv in urls)
            {
                string fileName = kv.Key;
                _testsStatus = $"Загрузка {fileName}…";
                RebuildTestsStatus();
                using (var req = UnityWebRequest.Get(kv.Value))
                {
                    req.timeout = 10;
                    req.SetRequestHeader("User-Agent", "ProjectC-AdminPanel");
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                        files[fileName] = req.downloadHandler.text;
                    else
                        Debug.LogWarning($"[AdminRuntimeWindow] Tests: {fileName} не скачан: {req.error}");
                }
            }

            // Шаг 3: fallback — локальная рабочая копия, если сеть не отдала ничего.
            if (files.Count == 0)
            {
                string localDir = Path.Combine(Application.dataPath, "..", "docs", "dev", "global_needtotest");
                if (Directory.Exists(localDir))
                {
                    foreach (var path in Directory.GetFiles(localDir, "*.md"))
                    {
                        try { files[Path.GetFileName(path)] = File.ReadAllText(path); }
                        catch (System.Exception e) { Debug.LogWarning($"[AdminRuntimeWindow] Tests: не прочитан {path}: {e.Message}"); }
                    }
                    if (files.Count > 0)
                        _testsStatus = $"GitHub недоступен ({listError ?? "пусто"}) — показана локальная копия.";
                }
                if (files.Count == 0)
                    _testsStatus = $"Не загружено: {listError ?? "файлов нет"}. Проверь сеть / GitHub.";
            }
            else
            {
                _testsStatus = $"Обновлено из GitHub ({files.Count} ф.).";
            }

            _testsParsed.Clear();
            var names = new List<string>(files.Keys);
            names.Sort(System.StringComparer.Ordinal);
            foreach (var name in names)
                _testsParsed[name] = NeedToTestParser.Parse(name, files[name]);

            _testsCoroutine = null;
            // Пересобрать вкладку только если пользователь всё ещё на ней.
            if (IsVisible && _currentTab == "tests") SwitchTab("tests");
            else RefreshStatus();
        }

        private readonly Dictionary<string, string> _testsPendingUrls = new Dictionary<string, string>();

        private void CollectTestsApiEntries(string json)
        {
            _testsPendingUrls.Clear();
            if (string.IsNullOrEmpty(json)) return;
            GitHubContentList list = null;
            try { list = JsonUtility.FromJson<GitHubContentList>("{\"items\":" + json + "}"); }
            catch (System.Exception e) { Debug.LogWarning($"[AdminRuntimeWindow] Tests: API JSON не разобран: {e.Message}"); }
            if (list?.items == null) return;
            foreach (var e in list.items)
            {
                if (e.type != null && !e.type.Equals("file", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(e.name) || !e.name.EndsWith(".md", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(e.download_url)) continue;
                _testsPendingUrls[e.name] = e.download_url;
            }
            // files заполнится на шаге 2; здесь только очередь URL.
        }

        private void RebuildTestsStatus()
        {
            if (IsVisible && _currentTab == "tests") SwitchTab("tests");
        }

        private void RenderTestsTab()
        {
            if (_testsBody == null) return;
            _testsBody.Clear();
            var names = new List<string>(_testsParsed.Keys);
            names.Sort(System.StringComparer.Ordinal);
            if (names.Count == 0) { AddLabelTo(_testsBody, "Файлов нет.", 12); return; }
            foreach (var name in names)
            {
                var file = _testsParsed[name];
                int total = 0, done = 0, red = 0, yellow = 0, green = 0;
                foreach (var s in file.Sections)
                    foreach (var it in s.Items)
                    {
                        total++;
                        if (it.Done) done++;
                        else if (it.Priority == "red") red++;
                        else if (it.Priority == "yellow") yellow++;
                        else if (it.Priority == "green") green++;
                    }
                string fileKey = "f:" + name;
                var fileFold = new Foldout
                {
                    text = $"📄 {name} — ✅{done}/{total} (🔴{red} 🟡{yellow} 🟢{green})",
                    value = _testsOpen.Contains(fileKey) || _testsParsed.Count <= 3
                };
                if (fileFold.value) _testsOpen.Add(fileKey);
                fileFold.style.fontSize = 14;
                fileFold.RegisterValueChangedCallback(e =>
                {
                    if (e.newValue) _testsOpen.Add(fileKey);
                    else _testsOpen.Remove(fileKey);
                });
                _testsBody.Add(fileFold);
                if (!string.IsNullOrEmpty(file.Title) && file.Title != name)
                    AddLabelTo(fileFold, file.Title, 13);
                foreach (var q in file.Preamble)
                    AddLabelTo(fileFold, q, 11, new Color(0.65f, 0.7f, 0.75f));
                foreach (var s in file.Sections)
                {
                    int st = s.Items.Count, sd = 0;
                    foreach (var it in s.Items) if (it.Done) sd++;
                    string secKey = fileKey + "|s:" + s.Title;
                    var secFold = new Foldout
                    {
                        text = $"{s.Title} — ✅{sd}/{st}",
                        value = _testsOpen.Contains(secKey)
                    };
                    secFold.style.fontSize = 13;
                    secFold.RegisterValueChangedCallback(e =>
                    {
                        if (e.newValue) _testsOpen.Add(secKey);
                        else _testsOpen.Remove(secKey);
                    });
                    fileFold.Add(secFold);
                    foreach (var q in s.Preamble)
                        AddLabelTo(secFold, q, 11, new Color(0.65f, 0.7f, 0.75f));
                    foreach (var it in s.Items)
                    {
                        var l = new Label((it.Done ? "[x] " : "[ ] ") + it.Text);
                        l.style.fontSize = 12;
                        l.style.whiteSpace = WhiteSpace.Normal;
                        l.style.marginBottom = 2;
                        l.style.color = TestsItemColor(it);
                        secFold.Add(l);
                    }
                }
            }
        }

        private static Color TestsItemColor(NeedToTestItem it)
        {
            if (it.Done) return new Color(0.55f, 0.75f, 0.55f);
            switch (it.Priority)
            {
                case "red": return new Color(1f, 0.55f, 0.55f);
                case "yellow": return new Color(1f, 0.9f, 0.55f);
                case "green": return new Color(0.65f, 0.95f, 0.65f);
                default: return new Color(0.92f, 0.92f, 0.92f);
            }
        }

        private static Label AddLabelTo(VisualElement parent, string text, int fontSize = 13, Color? color = null)
        {
            var l = new Label(text);
            l.style.fontSize = fontSize;
            l.style.color = color ?? new Color(0.92f, 0.92f, 0.92f);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = 2;
            parent.Add(l);
            return l;
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
