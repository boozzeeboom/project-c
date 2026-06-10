// CraftingProgressController.cs (T-C05) - runtime UI Toolkit toast с ProgressBar.
// Pattern: GatheringToastController (T-G04). Показывает прогресс крафта и финал (✅ Готово).

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.Crafting
{
    [RequireComponent(typeof(UIDocument))]
    public class CraftingProgressController : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Длительность показа финального тоста (Completed) в секундах.")]
        [SerializeField] private float _completeDuration = 0.6f;

        [Tooltip("Длительность показа тоста при прерывании/отказе в секундах.")]
        [SerializeField] private float _interruptDuration = 1.2f;

        [Tooltip("Длительность flash-fill (progress 0→1) при Interrupted/Denied в секундах.")]
        [SerializeField] private float _interruptFlashFill = 0.2f;

        private UIDocument _doc;
        private VisualElement _container;
        private Label _label;
        private ProgressBar _progressBar;
        private Coroutine _activeCoroutine;
        private bool _built = false;
        private bool _subscribed = false;
        private ulong _currentStationNetId;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            // T-C07: auto-bind panelSettings если не задан в Inspector
            if (_doc != null && _doc.panelSettings == null)
            {
                var ps = Resources.Load<PanelSettings>("UI/CraftingPanelSettings");
                if (ps != null) _doc.panelSettings = ps;
            }
        }

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
            var state = CraftingClientState.Instance;
            if (state != null)
            {
                state.OnCraftingProgress -= HandleProgress;
                state.OnCraftingCompleted -= HandleCompleted;
                state.OnCraftingInterrupted -= HandleInterrupted;
                state.OnCraftingDenied -= HandleDenied;
                state.OnCraftingCancelled -= HandleCancelled;
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

            _container = new VisualElement
            {
                name = "crafting-progress-toast",
                pickingMode = PickingMode.Ignore
            };
            _container.style.position = Position.Absolute;
            _container.style.bottom = 48;
            _container.style.left = 0;
            _container.style.right = 0;
            _container.style.alignItems = Align.Center;
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            var inner = new VisualElement
            {
                name = "crafting-progress-toast-inner",
                pickingMode = PickingMode.Ignore
            };
            inner.style.width = 360;
            inner.style.backgroundColor = new StyleColor(new Color(0.08f, 0.06f, 0.04f, 0.88f));
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
                name = "crafting-progress-label",
                text = "Крафт…",
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
                name = "crafting-progress-bar",
                lowValue = 0f,
                highValue = 1f,
                value = 0f,
            };
            _progressBar.style.height = 14;
            _progressBar.style.flexGrow = 1;

            inner.Add(_label);
            inner.Add(_progressBar);
            _container.Add(inner);
            root.Add(_container);
            _built = true;
        }

        private void TrySubscribe()
        {
            var state = CraftingClientState.Instance;
            if (state == null) return;
            state.OnCraftingProgress += HandleProgress;
            state.OnCraftingCompleted += HandleCompleted;
            state.OnCraftingInterrupted += HandleInterrupted;
            state.OnCraftingDenied += HandleDenied;
            state.OnCraftingCancelled += HandleCancelled;
            _subscribed = true;
        }

        private void HandleProgress(ulong stationNetId, float progress, string resultItemName)
        {
            if (!_built) TryBuild();
            if (_container == null || _progressBar == null) return;
            _currentStationNetId = stationNetId;
            if (_activeCoroutine == null)
            {
                _label.text = string.IsNullOrEmpty(resultItemName) ? "Крафт…" : "Крафт: " + resultItemName;
                _progressBar.value = 0f;
                _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            }
            _progressBar.value = Mathf.Clamp01(progress);
        }

        private void HandleCompleted(ulong stationNetId, string resultItemName)
        {
            if (!_built) TryBuild();
            if (_container == null || _progressBar == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _activeCoroutine = StartCoroutine(ShowCompletedAndHide(resultItemName));
        }

        private void HandleInterrupted(ulong stationNetId, string reason)
        {
            if (!_built) TryBuild();
            if (_container == null || _progressBar == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _activeCoroutine = StartCoroutine(ShowInterruptAndHide(reason, extended: true));
        }

        private void HandleDenied(ulong stationNetId, string reason)
        {
            if (!_built) TryBuild();
            if (_container == null || _progressBar == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _activeCoroutine = StartCoroutine(ShowInterruptAndHide("❌ " + (reason ?? "Отказано"), extended: true));
        }

        private void HandleCancelled(ulong stationNetId)
        {
            if (!_built) TryBuild();
            if (_container == null) return;
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            _activeCoroutine = null;
        }

        private IEnumerator ShowCompletedAndHide(string resultItemName)
        {
            _label.text = "✅ Готово: " + (string.IsNullOrEmpty(resultItemName) ? "Предмет" : resultItemName);
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
            yield return new WaitForSecondsRealtime(extended ? 1.8f : _interruptDuration);
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            _activeCoroutine = null;
        }
    }
}