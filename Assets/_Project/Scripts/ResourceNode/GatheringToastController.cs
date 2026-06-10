// =====================================================================================
// GatheringToastController.cs — UI-компонент тоста с ProgressBar (T-G04)
// =====================================================================================
// Документация:
//   • docs/Mining/10_DESIGN.md §1.4
//   • docs/Mining/ROADMAP.md T-G04
//
// Паттерн скопирован из QuestToast.cs (T-Q23):
//   - Runtime-constructed VisualElement + ProgressBar (НЕ UXML/USS — T-G04 упрощение)
//   - Bottom-center positioned
//   - Singleton subscribed to GatheringClientState events
//   - Queue-based (без cooldown, как T-Q25 fix)
//
// Отличия от QuestToast:
//   - Есть ProgressBar (UI Toolkit <ui:ProgressBar>)
//   - Показывается при старте сбора (InProgress) и заполняется 0..1
//   - При Completed → ProgressBar.value = 1.0 + "Добыто: X × N" (через 0.5s скрыть)
//   - При Interrupted/Denied → ProgressBar.value = 1.0 (flash-fill) + reason (через 1s скрыть)
//
// Создание: scene-placed GameObject `[GatheringToast]` в BootstrapScene.unity.
// =====================================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.ResourceNode
{
    [RequireComponent(typeof(UIDocument))]
    public class GatheringToastController : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Длительность показа финального тоста (Completed) в секундах.")]
        [SerializeField] private float _completeDuration = 0.5f;

        [Tooltip("Длительность показа тоста при прерывании/отказе в секундах.")]
        [SerializeField] private float _interruptDuration = 1.0f;

        [Tooltip("Длительность flash-fill (progress 0→1) при Interrupted/Denied в секундах.")]
        [SerializeField] private float _interruptFlashFill = 0.2f;

        [Header("Layout")]
        [Tooltip("Отступ снизу в пикселях (выше квестового тоста).")]
        [SerializeField] private float _bottomOffset = 200f;

        [Tooltip("Ширина тоста в пикселях.")]
        [SerializeField] private float _width = 320f;

        [Tooltip("Размер шрифта.")]
        [SerializeField] private int _fontSize = 16;

        // ==========================================================
        // State
        // ==========================================================

        private UIDocument _doc;
        private VisualElement _container;
        private Label _label;
        private ProgressBar _progressBar;
        private Coroutine _activeCoroutine;
        private bool _built = false;
        private bool _subscribed = false;

        // ==========================================================
        // Lifecycle
        // ==========================================================

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_activeCoroutine != null) { StopCoroutine(_activeCoroutine); _activeCoroutine = null; }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            var state = GatheringClientState.Instance;
            if (state != null)
            {
                state.OnGatherProgress -= HandleProgress;
                state.OnGatherCompleted -= HandleCompleted;
                state.OnGatherInterrupted -= HandleInterrupted;
                state.OnGatherDenied -= HandleDenied;
                state.OnGatherCancelled -= HandleCancelled;
            }
            _subscribed = false;
        }

        private void Update()
        {
            if (!_built) TryBuild();
            if (!_subscribed) TrySubscribe();
        }

        // ==========================================================
        // Build
        // ==========================================================

        private void TryBuild()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null) return;
            if (_doc.rootVisualElement == null) return;
            if (_doc.panelSettings == null) return;

            var root = _doc.rootVisualElement;

            _container = new VisualElement
            {
                name = "gathering-toast",
                pickingMode = PickingMode.Ignore
            };
            _container.style.position = Position.Absolute;
            _container.style.bottom = _bottomOffset;
            _container.style.left = 0;
            _container.style.right = 0;
            _container.style.alignItems = Align.Center;
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            // Internal container with width
            var inner = new VisualElement
            {
                name = "gathering-toast-inner",
                pickingMode = PickingMode.Ignore
            };
            inner.style.width = _width;
            inner.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.1f, 0.88f));
            inner.style.borderTopLeftRadius = 8;
            inner.style.borderTopRightRadius = 8;
            inner.style.borderBottomLeftRadius = 8;
            inner.style.borderBottomRightRadius = 8;
            inner.style.paddingTop = 10;
            inner.style.paddingBottom = 10;
            inner.style.paddingLeft = 12;
            inner.style.paddingRight = 12;

            _label = new Label
            {
                name = "gathering-toast-label",
                text = "Сбор ресурса…",
                pickingMode = PickingMode.Ignore
            };
            _label.style.color = new StyleColor(Color.white);
            _label.style.fontSize = _fontSize;
            _label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.whiteSpace = WhiteSpace.Normal;
            _label.style.marginBottom = 6;

            _progressBar = new ProgressBar
            {
                name = "gathering-progress-bar",
                lowValue = 0f,
                highValue = 1f,
                value = 0f,
            };
            _progressBar.style.height = 14;
            _progressBar.style.minWidth = _width - 24;

            inner.Add(_label);
            inner.Add(_progressBar);
            _container.Add(inner);
            root.Add(_container);
            _built = true;
        }

        // ==========================================================
        // Subscribe
        // ==========================================================

        private void TrySubscribe()
        {
            var state = GatheringClientState.Instance;
            if (state == null) return;
            state.OnGatherProgress += HandleProgress;
            state.OnGatherCompleted += HandleCompleted;
            state.OnGatherInterrupted += HandleInterrupted;
            state.OnGatherDenied += HandleDenied;
            state.OnGatherCancelled += HandleCancelled;
            _subscribed = true;
        }

        // ==========================================================
        // Event handlers
        // ==========================================================

        private void HandleProgress(float progress)
        {
            if (!_built) TryBuild();
            if (_container == null || _progressBar == null) return;
            // Первый InProgress (progress=0) — показать
            if (_activeCoroutine == null)
            {
                _label.text = "Сбор ресурса…";
                _progressBar.value = 0f;
                _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            }
            _progressBar.value = Mathf.Clamp01(progress);
        }

        private void HandleCompleted(string itemName, int quantity, bool isDepleted)
        {
            if (!_built) TryBuild();
            if (_container == null || _progressBar == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _activeCoroutine = StartCoroutine(ShowCompletedAndHide(itemName, quantity, isDepleted));
        }

        private void HandleInterrupted(string reason)
        {
            if (!_built) TryBuild();
            if (_container == null || _progressBar == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _activeCoroutine = StartCoroutine(ShowInterruptAndHide(reason));
        }

        private void HandleDenied(string reason)
        {
            // Denied — как Interrupted, но чуть дольше видим (1.5s) — игрок должен прочитать
            if (!_built) TryBuild();
            if (_container == null || _progressBar == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _activeCoroutine = StartCoroutine(ShowInterruptAndHide("❌ " + (reason ?? "Отказано в доступе"), extended: true));
        }

        private void HandleCancelled()
        {
            if (!_built) TryBuild();
            if (_container == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            _activeCoroutine = null;
        }

        // ==========================================================
        // Coroutines
        // ==========================================================

        private IEnumerator ShowCompletedAndHide(string itemName, int quantity, bool isDepleted)
        {
            _label.text = "✅ Добыто: " + (string.IsNullOrEmpty(itemName) ? "Ресурс" : itemName) + " × " + quantity;
            if (isDepleted) _label.text += " (узел истощён)";
            _progressBar.value = 1f;
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            yield return new WaitForSecondsRealtime(_completeDuration);
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            _activeCoroutine = null;
        }

        private IEnumerator ShowInterruptAndHide(string reason, bool extended = false)
        {
            _label.text = reason;
            // Flash-fill прогресс-бара 0 → 1 за 0.2 сек
            float elapsed = 0f;
            float start = _progressBar.value;
            float end = 1f;
            while (elapsed < _interruptFlashFill)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _interruptFlashFill);
                _progressBar.value = Mathf.Lerp(start, end, t);
                yield return null;
            }
            _progressBar.value = 1f;
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            yield return new WaitForSecondsRealtime(extended ? 1.5f : _interruptDuration);
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            _activeCoroutine = null;
        }
    }
}
