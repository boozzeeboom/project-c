// =====================================================================================
// GatheringToastController.cs — UI-компонент тоста с ProgressBar (T-G04, v2)
// =====================================================================================
// Паттерн скопирован из ShipKeyToast (работает снизу): position Absolute bottom=48.
// ВЕРСИЯ 2: стили на самом _container, без inner — 1:1 как ShipKeyToast.
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

        private UIDocument _doc;
        private VisualElement _container;
        private Label _label;
        private ProgressBar _progressBar;
        private Coroutine _activeCoroutine;
        private bool _built = false;
        private bool _subscribed = false;

        private void Awake() { _doc = GetComponent<UIDocument>(); }

        private void OnEnable()
        {
            if (transform.parent == null && Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_activeCoroutine != null) { StopCoroutine(_activeCoroutine); _activeCoroutine = null; }
        }

        private void OnDestroy() { Unsubscribe(); }

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

        private void TryBuild()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null) return;
            if (_doc.rootVisualElement == null) return;
            if (_doc.panelSettings == null) return;

            var root = _doc.rootVisualElement;

            // Контейнер: только позиционирование — absolute, bottom 48, растянут по ширине.
            // Без фона, без padding. Внутри — inner с реальным оформлением.
            _container = new VisualElement
            {
                name = "gathering-toast",
                pickingMode = PickingMode.Ignore
            };
            _container.style.position = Position.Absolute;
            _container.style.bottom = 48;
            _container.style.left = 0;
            _container.style.right = 0;
            _container.style.alignItems = Align.Center;
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            // Inner: сам тост-бокс фиксированной ширины с фоном.
            var inner = new VisualElement
            {
                name = "gathering-toast-inner",
                pickingMode = PickingMode.Ignore
            };
            inner.style.width = 320;
            inner.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.1f, 0.88f));
            inner.style.borderTopLeftRadius = 8;
            inner.style.borderTopRightRadius = 8;
            inner.style.borderBottomLeftRadius = 8;
            inner.style.borderBottomRightRadius = 8;
            inner.style.paddingTop = 10;
            inner.style.paddingBottom = 10;
            inner.style.paddingLeft = 16;
            inner.style.paddingRight = 16;
            inner.style.marginLeft = 32;
            inner.style.marginRight = 32;

            _label = new Label
            {
                name = "gathering-toast-label",
                text = "Сбор ресурса…",
                pickingMode = PickingMode.Ignore
            };
            _label.style.color = new StyleColor(Color.white);
            _label.style.fontSize = 16;
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
            _progressBar.style.flexGrow = 1; // растягивается на всю ширину inner

            inner.Add(_label);
            inner.Add(_progressBar);
            _container.Add(inner);
            root.Add(_container);
            _built = true;
        }

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

        private void HandleProgress(float progress)
        {
            if (!_built) TryBuild();
            if (_container == null || _progressBar == null) return;
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
            float elapsed = 0f;
            float start = _progressBar.value;
            while (elapsed < _interruptFlashFill)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _interruptFlashFill);
                _progressBar.value = Mathf.Lerp(start, 1f, t);
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
