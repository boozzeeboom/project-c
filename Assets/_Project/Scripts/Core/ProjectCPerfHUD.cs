// ═══════════════════════════════════════════════════════════════════════════
// ProjectC: Runtime Performance HUD — T-PERF-07
// ═══════════════════════════════════════════════════════════════════════════
//
// ■ НАЗНАЧЕНИЕ
//   Отображает ProjectCPerfCounters (NPCs, Ships, Clouds, Chunks, Combats)
//   + встроенный FPS через OnGUI. Тоггл: F3. Обновление: раз в секунду.
//
// ■ КОГДА ИСПОЛЬЗОВАТЬ
//   - Отладка производительности в DEVELOPMENT_BUILD или Editor
//   - Мониторинг количества активных сущностей в реальном времени
//
// ■ КАК ПОДКЛЮЧИТЬ
//   Добавить компонент на персистентный GameObject
//   (например, NetworkManagerController).
//   В Player-билде класс отключён через #if FALSE.
//
// ■ СТАТУС: ⏸ ОТКЛЮЧЁН (#if FALSE)
//   Причина: нестабилен, требуется доработка интеграции
//   с ProjectCPerfCounters и тестирование в билде.
//   Чтобы включить обратно — заменить #if FALSE на
//   #if DEVELOPMENT_BUILD || UNITY_EDITOR.
//
// ■ ЗАВИСИМОСТИ
//   - ProjectCPerfCounters (Assets/_Project/Scripts/Core/ProjectCPerfCounters.cs)
//
// ■ ДИЗАЙН-ДОК
//   docs/world/admin_tool/perfomance/PERFORMANCE_MONITORING_RESEARCH.md §4.2
// ═══════════════════════════════════════════════════════════════════════════
#if FALSE
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Runtime HUD showing project-specific performance counters.
    /// Toggle: F3. Updates once per second.
    /// Place on any persistent GameObject (e.g. NetworkManagerController).
    /// </summary>
    public class ProjectCPerfHUD : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool _showByDefault = false;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F3;
        [SerializeField] private float _updateInterval = 1f;

        [Header("Style")]
        [SerializeField] private int _fontSize = 14;
        [SerializeField] private Color _textColor = Color.green;
        [SerializeField] private Color _warningColor = Color.yellow;
        [SerializeField] private Color _criticalColor = Color.red;
        [SerializeField] private float _paddingX = 10f;
        [SerializeField] private float _paddingY = 10f;

        private bool _visible;
        private float _timer;
        private float _fps;

        // Cached strings to avoid per-frame allocations
        private string _displayText = "";

        private void Awake()
        {
            _visible = _showByDefault;
        }

        private void Update()
        {
            // F3 toggle — support both Input System and legacy
            if (UnityEngine.Input.GetKeyDown(_toggleKey))
                _visible = !_visible;

            if (!_visible) return;

            _timer += Time.unscaledDeltaTime;
            if (_timer < _updateInterval) return;
            _timer = 0f;

            _fps = 1f / Time.unscaledDeltaTime;

            BuildDisplayText();
        }

        private void BuildDisplayText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══ ProjectC Perf HUD ═══");
            sb.AppendLine($"FPS: {_fps:F0}");
            sb.AppendLine();
            sb.AppendLine("--- Entities ---");
            sb.AppendLine($"NPCs:    {ProjectCPerfCounters.ActiveNpcs}");
            sb.AppendLine($"Ships:   {ProjectCPerfCounters.ActiveShips}");
            sb.AppendLine($"Clouds:  {ProjectCPerfCounters.VisibleClouds}");
            sb.AppendLine($"Chunks:  {ProjectCPerfCounters.LoadedChunks}");
            sb.AppendLine($"Combats: {ProjectCPerfCounters.ActiveCombats}");
            _displayText = sb.ToString();
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (!_visible || string.IsNullOrEmpty(_displayText)) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize,
                normal = { textColor = _textColor },
                alignment = TextAnchor.UpperLeft
            };

            var rect = new Rect(_paddingX, _paddingY, 300, 400);
            GUI.Label(rect, _displayText, style);
        }
#endif
    }
}
#endif
