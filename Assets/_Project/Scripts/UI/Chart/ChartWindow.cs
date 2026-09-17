using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UI.Chart
{
    /// <summary>
    /// Морская карта неба (клавиша M). v1 — каркас интерфейса:
    /// пергамент, север вверх (WorldNorth), своё место (слежение), порты
    /// из AltitudeCorridorSystem, циркуль (клик = пеленг+дистанция), зум.
    ///
    /// Честные заглушки: слои треков/меток/ветров присутствуют в легенде
    /// как «скоро», данных не выдумывают. Треки/метки/пеленг на метку —
    /// следующие шаги (см. docs/world/world_map/00_CHART_CONCEPT_2026-09-17.md §7).
    ///
    /// Открытие: M (NetworkPlayer → ToggleOpen) везде — и пешком, и в корабле.
    /// Закрытие: M / Esc (свой Esc в Update + регистрация в UIManager) / ✕.
    /// Курсор: как CommPanelWindow — открыли: свободный, закрыли: lock (в игре).
    ///
    /// 🟢 Floating Origin: карта рисуется из живых мировых координат каждый
    /// кадр (своя позиция, центры городов, сетка от мировых осей) — кэшей нет,
    /// сдвиг мира все слои двигает согласованно, хук не нужен. Север — поворот,
    /// от трансляции не зависит (см. WorldNorth).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class ChartWindow : MonoBehaviour
    {
        public static ChartWindow Instance { get; private set; }

        /// <summary>Окно открыто (для UIManager: Esc-маршрутизация, блок ввода).</summary>
        public bool IsOpen { get; private set; }

        [Header("Ссылки")]
        [Tooltip("UIDocument на этом GameObject. auto-cache в Awake.")]
        [SerializeField] private UIDocument _doc;

        [Header("Вид карты")]
        [Tooltip("Стартовый масштаб: метров на пиксель.")]
        [SerializeField] private float _metersPerPixel = 4f;
        [SerializeField] private float _minMetersPerPixel = 0.5f;
        [SerializeField] private float _maxMetersPerPixel = 64f;

        private static readonly Color Parchment = new Color(0.93f, 0.87f, 0.72f);
        private static readonly Color Ink = new Color(0.25f, 0.18f, 0.10f);
        private static readonly Color InkFaint = new Color(0.25f, 0.18f, 0.10f, 0.25f);
        private static readonly Color Accent = new Color(0.55f, 0.12f, 0.12f);

        private bool _built;
        private VisualElement _overlay;
        private VisualElement _canvas;
        private Label _readoutLabel;
        private Label _statusLabel;
        private Label _zoomLabel;

        // Легенда треков (оживает, когда журнал пишет)
        private VisualElement _tracksDot;
        private Label _tracksLabel;

        // Циркуль: мировая XZ-точка под кликом (сессионная, не сейвится)
        private bool _hasDivider;
        private Vector3 _dividerWorld;

        private ProjectC.Player.NetworkPlayer _localPlayer;
        private ProjectC.Ship.AltitudeCorridorSystem _corridorSystem;
        private float _lastPaint;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Журнал треков пишется всегда (даже с закрытой картой).
            ProjectC.World.ChartTrackRecorder.EnsureExists();

            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc != null && _doc.panelSettings == null)
                Debug.LogWarning("[ChartWindow] panelSettings не задан — назначьте ChartPanelSettings в Inspector.");

            if (transform.parent == null && Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_built) EnsureBuilt();
            if (!_built) return;

            // Esc — закрыть (свой обработчик; UIManager нас пропускает через IsAnyExternalWindowOpen)
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (IsOpen && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                SetOpen(false);
                return;
            }
            if (!IsOpen) return;

            UpdateReadout();

            // Живая точка и циркуль — перерисовка 10 Гц
            if (Time.unscaledTime - _lastPaint > 0.1f)
            {
                _lastPaint = Time.unscaledTime;
                _canvas?.MarkDirtyRepaint();
            }
        }

        // ==================== OPEN/CLOSE ====================

        public void SetOpen(bool open)
        {
            if (!_built) EnsureBuilt();
            if (!_built) return;
            IsOpen = open;
            if (_overlay != null)
            {
                _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                _overlay.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            }
            if (open)
            {
                if (_corridorSystem == null)
                    _corridorSystem = FindAnyObjectByType<ProjectC.Ship.AltitudeCorridorSystem>();
                EnsureLocalPlayer();
                _hasDivider = false;
                UpdateReadout();
                _canvas?.MarkDirtyRepaint();
                _doc?.rootVisualElement?.MarkDirtyRepaint();
            }

            // Курсор — как CommPanelWindow
            if (open)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
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

        public void ToggleOpen() => SetOpen(!IsOpen);

        // ==================== BUILD ====================

        private void EnsureBuilt()
        {
            if (_built || _doc == null || _doc.rootVisualElement == null || _doc.panelSettings == null) return;

            _overlay = new VisualElement { name = "chart-overlay" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.right = 0;
            _overlay.style.top = 0; _overlay.style.bottom = 0;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            _overlay.style.display = DisplayStyle.None;
            _overlay.pickingMode = PickingMode.Ignore;

            var panel = new VisualElement { name = "chart-panel" };
            panel.style.width = Length.Percent(72);
            panel.style.height = Length.Percent(82);
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.backgroundColor = Parchment;
            panel.style.borderTopWidth = 2; panel.style.borderBottomWidth = 2;
            panel.style.borderLeftWidth = 2; panel.style.borderRightWidth = 2;
            panel.style.borderTopColor = Ink; panel.style.borderBottomColor = Ink;
            panel.style.borderLeftColor = Ink; panel.style.borderRightColor = Ink;
            panel.style.borderTopLeftRadius = 4; panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4; panel.style.borderBottomRightRadius = 4;
            _overlay.Add(panel);

            // --- Шапка ---
            var header = new VisualElement { name = "chart-header" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 12; header.style.paddingRight = 12;
            header.style.paddingTop = 8; header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = InkFaint;
            panel.Add(header);

            var title = new Label { name = "chart-title", text = "МОРСКАЯ КАРТА" };
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Ink;
            header.Add(title);

            var subtitle = new Label { name = "chart-sub", text = "журнал плаваний • v1: своё место, порты, циркуль" };
            subtitle.style.fontSize = 11;
            subtitle.style.color = new Color(0.25f, 0.18f, 0.10f, 0.6f);
            header.Add(subtitle);

            var closeBtn = new Button(() => SetOpen(false)) { text = "✕  (M / Esc)" };
            closeBtn.style.fontSize = 12;
            closeBtn.style.color = Ink;
            header.Add(closeBtn);

            // --- Тело: canvas + легенда ---
            var body = new VisualElement { name = "chart-body" };
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.minHeight = 0;
            panel.Add(body);

            _canvas = new VisualElement { name = "chart-canvas" };
            _canvas.style.flexGrow = 1;
            _canvas.style.minWidth = 0;
            _canvas.style.backgroundColor = Parchment;
            _canvas.generateVisualContent += OnChartPaint;
            _canvas.RegisterCallback<PointerDownEvent>(OnCanvasPointerDown);
            body.Add(_canvas);

            var legend = new VisualElement { name = "chart-legend" };
            legend.style.width = 170;
            legend.style.minWidth = 170;
            legend.style.flexShrink = 0;
            legend.style.paddingLeft = 10; legend.style.paddingRight = 10;
            legend.style.paddingTop = 10;
            legend.style.borderLeftWidth = 1;
            legend.style.borderLeftColor = InkFaint;
            body.Add(legend);

            var layersHdr = new Label { text = "СЛОИ" };
            layersHdr.style.fontSize = 12;
            layersHdr.style.unityFontStyleAndWeight = FontStyle.Bold;
            layersHdr.style.color = Ink;
            layersHdr.style.marginBottom = 6;
            legend.Add(layersHdr);

            legend.Add(MakeLayerRow("● Порты", true, false));
            var tracksRow = MakeLayerRow("— Треки", false, true);
            _tracksDot = tracksRow.childCount > 0 ? tracksRow[0] : null;
            _tracksLabel = tracksRow.childCount > 1 ? tracksRow[1] as Label : null;
            legend.Add(tracksRow);
            legend.Add(MakeLayerRow("✕ Метки (скоро)", false, true));
            legend.Add(MakeLayerRow("⇴ Ветра (скоро)", false, true));

            var zoomHdr = new Label { text = "МАСШТАБ" };
            zoomHdr.style.fontSize = 12;
            zoomHdr.style.unityFontStyleAndWeight = FontStyle.Bold;
            zoomHdr.style.color = Ink;
            zoomHdr.style.marginTop = 12;
            zoomHdr.style.marginBottom = 6;
            legend.Add(zoomHdr);

            var zoomRow = new VisualElement();
            zoomRow.style.flexDirection = FlexDirection.Row;
            zoomRow.style.alignItems = Align.Center;
            legend.Add(zoomRow);

            var zoomOut = new Button(() => ChangeZoom(2f)) { text = "−" };
            zoomOut.style.width = 34;
            zoomRow.Add(zoomOut);
            var zoomIn = new Button(() => ChangeZoom(0.5f)) { text = "+" };
            zoomIn.style.width = 34;
            zoomRow.Add(zoomIn);

            _zoomLabel = new Label { text = "" };
            _zoomLabel.style.fontSize = 11;
            _zoomLabel.style.color = Ink;
            _zoomLabel.style.marginLeft = 6;
            zoomRow.Add(_zoomLabel);

            var followNote = new Label { text = "Слежение: своё место в центре, север вверху." };
            followNote.style.fontSize = 10;
            followNote.style.color = new Color(0.25f, 0.18f, 0.10f, 0.6f);
            followNote.style.marginTop = 12;
            followNote.style.whiteSpace = WhiteSpace.Normal;
            legend.Add(followNote);

            // --- Подвал: циркуль + статус ---
            var footer = new VisualElement { name = "chart-footer" };
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.paddingLeft = 12; footer.style.paddingRight = 12;
            footer.style.paddingTop = 6; footer.style.paddingBottom = 8;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = InkFaint;
            panel.Add(footer);

            _readoutLabel = new Label { text = "" };
            _readoutLabel.style.fontSize = 13;
            _readoutLabel.style.color = Ink;
            footer.Add(_readoutLabel);

            _statusLabel = new Label { text = "" };
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = new Color(0.25f, 0.18f, 0.10f, 0.6f);
            footer.Add(_statusLabel);

            _doc.rootVisualElement.Add(_overlay);
            _built = true;
        }

        private VisualElement MakeLayerRow(string text, bool on, bool dimmed)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            var dot = new VisualElement();
            dot.style.width = 10; dot.style.height = 10;
            dot.style.minWidth = 10; dot.style.minHeight = 10;
            dot.style.borderTopLeftRadius = 5; dot.style.borderTopRightRadius = 5;
            dot.style.borderBottomLeftRadius = 5; dot.style.borderBottomRightRadius = 5;
            dot.style.backgroundColor = on ? Accent : new Color(0.25f, 0.18f, 0.10f, 0.25f);
            dot.style.marginRight = 6;
            row.Add(dot);
            var label = new Label { text = text };
            label.style.fontSize = 12;
            label.style.color = dimmed
                ? new Color(0.25f, 0.18f, 0.10f, 0.45f)
                : Ink;
            row.Add(label);
            return row;
        }

        private void ChangeZoom(float mult)
        {
            _metersPerPixel = Mathf.Clamp(_metersPerPixel * mult, _minMetersPerPixel, _maxMetersPerPixel);
            _canvas?.MarkDirtyRepaint();
        }

        // ==================== INTERACTION ====================

        private void OnCanvasPointerDown(PointerDownEvent evt)
        {
            if (_canvas == null || evt.button != 0) return;
            Vector2 local = evt.localPosition;
            if (MapToWorld(local.x, local.y, out float wx, out float wz))
            {
                _dividerWorld = new Vector3(wx, 0f, wz);
                _hasDivider = true;
                UpdateReadout();
                _canvas.MarkDirtyRepaint();
            }
        }

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

        private bool GetOwnXZ(out float wx, out float wz)
        {
            EnsureLocalPlayer();
            if (_localPlayer != null)
            {
                Vector3 p = _localPlayer.transform.position;
                wx = p.x; wz = p.z;
                return true;
            }
            wx = 0f; wz = 0f;
            return false;
        }

        // Север вверху: yaw севера a = atan2(n.x, n.z);
        // sx = (dx*nz − dz*nx)*k; up = (dx*nx + dz*nz)*k (экранный y вниз = −up).
        private bool WorldToMap(float wx, float wz, float cx, float cz,
            Vector3 north, float k, float w, float h, out float sx, out float sy)
        {
            float dx = wx - cx, dz = wz - cz;
            sx = w * 0.5f + (dx * north.z - dz * north.x) * k;
            sy = h * 0.5f - (dx * north.x + dz * north.z) * k;
            return true;
        }

        private bool MapToWorld(float px, float py, out float wx, out float wz)
        {
            wx = 0f; wz = 0f;
            if (_canvas == null) return false;
            if (!GetOwnXZ(out float cx, out float cz)) return false;
            Vector3 north = ProjectC.World.WorldNorth.NorthDirection;
            float k = 1f / _metersPerPixel;
            float w = _canvas.contentRect.width, h = _canvas.contentRect.height;
            if (w <= 1f || h <= 1f || k <= 0f) return false;
            // Обратное преобразование (матрица поворота ортонормальна — транспонируем)
            float lx = (px - w * 0.5f) / k;
            float up = -(py - h * 0.5f) / k;
            wx = cx + lx * north.z + up * north.x;
            wz = cz - lx * north.x + up * north.z;
            return true;
        }

        private void UpdateReadout()
        {
            if (_readoutLabel == null) return;
            if (_hasDivider && GetOwnXZ(out float ox, out float oz))
            {
                Vector3 dir = _dividerWorld - new Vector3(ox, 0f, oz);
                dir.y = 0f;
                float dist = dir.magnitude;
                if (dist > 1f)
                {
                    float bearing = ProjectC.World.WorldNorth.GetHeadingDegrees(dir);
                    string rumba = ProjectC.World.WorldNorth.GetCardinalLabel(bearing);
                    _readoutLabel.text = $"ЦИРКУЛЬ: пеленг {bearing:F0}° {rumba} • {dist:F0} м";
                }
                else _readoutLabel.text = "ЦИРКУЛЬ: точка под тобой";
            }
            else
            {
                _readoutLabel.text = "Клик по карте — циркуль: пеленг и дистанция от тебя";
            }

            if (_statusLabel != null)
            {
                string hdg = "HDG —";
                if (_localPlayer != null)
                {
                    Vector3 fwd = _localPlayer.CurrentShip != null
                        ? _localPlayer.CurrentShip.transform.forward
                        : _localPlayer.transform.forward;
                    float h = ProjectC.World.WorldNorth.GetHeadingDegrees(fwd);
                    hdg = $"HDG {h:F0}° {ProjectC.World.WorldNorth.GetCardinalLabel(h)}";
                }
                _statusLabel.text = $"{hdg} • {_metersPerPixel:F1} м/пкс";
            }
            if (_zoomLabel != null) _zoomLabel.text = $"{_metersPerPixel:F1} м/пкс";

            // Легенда треков: оживает с первыми точками журнала
            var recorder = ProjectC.World.ChartTrackRecorder.Instance;
            int trackCount = recorder != null ? recorder.Count : 0;
            if (_tracksLabel != null)
                _tracksLabel.text = trackCount > 0 ? $"— Треки ({trackCount})" : "— Треки (лети — запишу)";
            if (_tracksDot != null)
            {
                bool live = trackCount > 0;
                _tracksDot.style.backgroundColor = live
                    ? Accent
                    : new Color(0.25f, 0.18f, 0.10f, 0.25f);
            }
        }

        // ==================== PAINT ====================

        private void OnChartPaint(MeshGenerationContext ctx)
        {
            var rect = _canvas != null ? _canvas.contentRect : new Rect(0, 0, 100, 100);
            var painter = ctx.painter2D;
            float w = rect.width, h = rect.height;
            if (w <= 1f || h <= 1f) return;

            Vector3 north = ProjectC.World.WorldNorth.NorthDirection;
            float k = 1f / _metersPerPixel;
            bool hasOwn = GetOwnXZ(out float cx, out float cz);

            // --- Сетка (шаг подбираем под зум, привязка к мировым осям) ---
            float targetPx = 90f;
            float rawStep = _metersPerPixel * targetPx;
            float step = NiceStep(rawStep);
            painter.strokeColor = InkFaint;
            painter.lineWidth = 1f;
            if (hasOwn && step > 0f)
            {
                // Вертикальные: мировые x = m*step
                float xMin = cx - (w * 0.5f) / k, xMax = cx + (w * 0.5f) / k;
                for (float gx = Mathf.Floor(xMin / step) * step; gx <= xMax; gx += step)
                {
                    // Линия постоянного мирового x — рисуем через две точки далеко по z
                    WorldToMap(gx, cz - (h / k), cx, cz, north, k, w, h, out float ax, out float ay);
                    WorldToMap(gx, cz + (h / k), cx, cz, north, k, w, h, out float bx, out float by);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(ax, ay));
                    painter.LineTo(new Vector2(bx, by));
                    painter.Stroke();
                }
                float zMin = cz - (h * 0.5f) / k, zMax = cz + (h * 0.5f) / k;
                for (float gz = Mathf.Floor(zMin / step) * step; gz <= zMax; gz += step)
                {
                    WorldToMap(cx - (w / k), gz, cx, cz, north, k, w, h, out float ax, out float ay);
                    WorldToMap(cx + (w / k), gz, cx, cz, north, k, w, h, out float bx, out float by);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(ax, ay));
                    painter.LineTo(new Vector2(bx, by));
                    painter.Stroke();
                }
            }

            // --- Порты (городские коридоры; FO-сдвиг учитывается самими данными) ---
            if (_corridorSystem == null)
                _corridorSystem = FindAnyObjectByType<ProjectC.Ship.AltitudeCorridorSystem>();
            if (_corridorSystem != null && _corridorSystem.corridors != null)
            {
                foreach (var c in _corridorSystem.corridors)
                {
                    if (c == null || c.isGlobal) continue;
                    string name = string.IsNullOrEmpty(c.displayName) ? c.corridorId : c.displayName;
                    WorldToMap(c.cityCenter.x, c.cityCenter.z, cx, cz, north, k, w, h, out float px, out float py);
                    if (px < -50 || py < -50 || px > w + 50 || py > h + 50) continue;
                    // Круг города по радиусу
                    float pr = c.cityRadius * k;
                    painter.strokeColor = Accent;
                    painter.lineWidth = 1.5f;
                    painter.BeginPath();
                    painter.Arc(new Vector2(px, py), Mathf.Max(4f, pr), 0, 360);
                    painter.Stroke();
                    // Точка-порт
                    painter.fillColor = Accent;
                    painter.BeginPath();
                    painter.Arc(new Vector2(px, py), 3.5f, 0, 360);
                    painter.Fill();
                }
            }

            // --- Треки: свой пройденный путь (давность = прозрачность) ---
            var trackRecorder = ProjectC.World.ChartTrackRecorder.Instance;
            if (trackRecorder != null && trackRecorder.Count > 1 && hasOwn)
            {
                double nowUtc = ProjectC.World.ChartTrackRecorder.NowUtcSeconds();
                var pts = trackRecorder.Points;
                bool hasPrev = false;
                float ppx = 0f, ppy = 0f;
                painter.lineWidth = 2f;
                for (int i = 0; i < pts.Count; i++)
                {
                    var tp = pts[i];
                    if (tp.source != ProjectC.World.ChartTrackSource.Self) continue; // чужие слои — позже
                    WorldToMap(tp.worldPos.x, tp.worldPos.z, cx, cz, north, k, w, h, out float tx, out float ty);
                    bool onScreen = tx > -100f && ty > -100f && tx < w + 100f && ty < h + 100f;
                    if (!onScreen) { hasPrev = false; continue; }
                    if (hasPrev)
                    {
                        // Свежий — яркий, старый — выцветает (концепт §4)
                        float ageMin = (float)((nowUtc - tp.utcSeconds) / 60.0);
                        float alpha = ageMin < 10f ? 0.85f : Mathf.Max(0.15f, 0.85f - (ageMin - 10f) / 170f * 0.7f);
                        painter.strokeColor = new Color(Ink.r, Ink.g, Ink.b, alpha);
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(ppx, ppy));
                        painter.LineTo(new Vector2(tx, ty));
                        painter.Stroke();
                    }
                    ppx = tx; ppy = ty;
                    hasPrev = true;
                }
            }

            // --- Роза: N вверху (север всегда вверх) ---
            {
                float rx = w - 26f, ry = 26f;
                painter.strokeColor = Ink;
                painter.lineWidth = 2f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rx, ry + 12f));
                painter.LineTo(new Vector2(rx, ry - 12f));
                painter.Stroke();
                painter.fillColor = Accent;
                painter.BeginPath();
                painter.Arc(new Vector2(rx, ry - 12f), 2.5f, 0, 360);
                painter.Fill();
            }

            // --- Своё место: белая точка с тёмной обводкой + риска курса ---
            if (hasOwn)
            {
                float ox = w * 0.5f, oy = h * 0.5f;
                painter.fillColor = Color.white;
                painter.BeginPath();
                painter.Arc(new Vector2(ox, oy), 6f, 0, 360);
                painter.Fill();
                painter.strokeColor = Ink;
                painter.lineWidth = 2f;
                painter.BeginPath();
                painter.Arc(new Vector2(ox, oy), 6f, 0, 360);
                painter.Stroke();

                Vector3 fwd = _localPlayer != null
                    ? (_localPlayer.CurrentShip != null
                        ? _localPlayer.CurrentShip.transform.forward
                        : _localPlayer.transform.forward)
                    : Vector3.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.001f)
                {
                    // Экранное направление носа при севере вверх
                    float hx = (fwd.x * north.z - fwd.z * north.x);
                    float hu = (fwd.x * north.x + fwd.z * north.z);
                    Vector2 nrm = new Vector2(hx, -hu).normalized;
                    painter.strokeColor = Ink;
                    painter.lineWidth = 2.5f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(ox, oy));
                    painter.LineTo(new Vector2(ox + nrm.x * 14f, oy + nrm.y * 14f));
                    painter.Stroke();
                }
            }

            // --- Циркуль: линия + крестик ---
            if (_hasDivider && hasOwn)
            {
                WorldToMap(_dividerWorld.x, _dividerWorld.z, cx, cz, north, k, w, h, out float dx, out float dy);
                float ox = w * 0.5f, oy = h * 0.5f;
                painter.strokeColor = Accent;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(ox, oy));
                painter.LineTo(new Vector2(dx, dy));
                painter.Stroke();
                float s = 7f;
                painter.lineWidth = 2.5f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(dx - s, dy - s));
                painter.LineTo(new Vector2(dx + s, dy + s));
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(dx - s, dy + s));
                painter.LineTo(new Vector2(dx + s, dy - s));
                painter.Stroke();
            }

            // --- Масштабная линейка ---
            {
                float barMeters = NiceStep(_metersPerPixel * 120f);
                float barPx = barMeters / _metersPerPixel;
                float bx = 14f, by = h - 16f;
                painter.strokeColor = Ink;
                painter.lineWidth = 2f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(bx, by));
                painter.LineTo(new Vector2(bx + barPx, by));
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(bx, by - 4f));
                painter.LineTo(new Vector2(bx, by + 4f));
                painter.Stroke();
                painter.BeginPath();
                painter.MoveTo(new Vector2(bx + barPx, by - 4f));
                painter.LineTo(new Vector2(bx + barPx, by + 4f));
                painter.Stroke();
            }
        }

        private static float NiceStep(float raw)
        {
            if (raw <= 0f) return 100f;
            float pow = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
            float m = raw / pow;
            float nice = m < 1.5f ? 1f : (m < 3.5f ? 2f : (m < 7.5f ? 5f : 10f));
            return nice * pow;
        }
    }
}
