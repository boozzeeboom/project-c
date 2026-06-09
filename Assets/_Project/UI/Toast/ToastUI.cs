using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UI.Toast
{
    /// <summary>
    /// T-Q23: singleton MonoBehaviour с UIDocument overlay (top-right).
    /// Queue: max 3 видимых toasts, fade out 3 сек, fade in 0.2 сек.
    /// Вызов: ToastService.Show("msg", ToastKind.Success) → instance.ShowToast(...).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ToastUI : MonoBehaviour
    {
        public static ToastUI Instance { get; private set; }

        [Header("UI Assets (Resources fallback)")]
        [SerializeField] private VisualTreeAsset toastUxml;
        [SerializeField] private StyleSheet toastUss;
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Behavior")]
        [SerializeField] private int maxVisible = 3;
        [SerializeField] private float visibleSeconds = 3f;
        [SerializeField] private float fadeInSeconds = 0.2f;
        [SerializeField] private float fadeOutSeconds = 0.5f;
        [SerializeField] private float verticalSpacing = 6f;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _container;
        private bool _built;

        private struct ToastEntry
        {
            public VisualElement element;
            public float spawnTime;
            public string message;
            public ToastKind kind;
        }
        private readonly List<ToastEntry> _active = new List<ToastEntry>();

        // T-Q23: lazy Instance init — fallback если Awake не отработал (race при первом Show()).
        private static ToastUI _cachedInstance;
        public static ToastUI GetOrFindInstance()
        {
            if (Instance != null) return Instance;
            if (_cachedInstance != null) return _cachedInstance;
            if (!Application.isPlaying) return null;
            try
            {
                var go = GameObject.Find("[ToastService]");
                if (go != null)
                {
                    _cachedInstance = go.GetComponent<ToastUI>();
                    if (_cachedInstance != null) return _cachedInstance;
                }
            }
            catch (System.Exception) { return null; }
            return null;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            if (toastUxml == null) toastUxml = Resources.Load<VisualTreeAsset>("UI/ToastUI");
            if (toastUss == null) toastUss = Resources.Load<StyleSheet>("UI/ToastUI");

            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EnsureBuilt();
        }

        private void Start()
        {
            EnsureBuilt();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_cachedInstance == this) _cachedInstance = null;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null) { Debug.LogError("[ToastUI] нет UIDocument"); return; }
            if (_doc.rootVisualElement == null) return;

            if (toastUxml == null) toastUxml = Resources.Load<VisualTreeAsset>("UI/ToastUI");
            if (toastUss == null) toastUss = Resources.Load<StyleSheet>("UI/ToastUI");
            if (toastUxml == null)
            {
                Debug.LogError("[ToastUI] UXML не найден (Resources/UI/ToastUI)");
                return;
            }

            // Root overlay: top-right, pointer-events: none
            _doc.rootVisualElement.Clear();
            if (toastUss != null) _doc.rootVisualElement.styleSheets.Add(toastUss);

            _root = toastUxml.CloneTree();
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_root);

            _container = _root.Q<VisualElement>("toast-container");
            if (_container == null)
            {
                Debug.LogError("[ToastUI] UXML не содержит 'toast-container'");
                return;
            }

            _built = true;
        }

        /// <summary>Показать toast (вызывается из ToastService).</summary>
        public void ShowToast(string message, ToastKind kind)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (!_built) EnsureBuilt();
            if (!_built || _container == null) return;

            // Если уже max visible — удалить самый старый.
            while (_active.Count >= maxVisible)
            {
                if (_active.Count == 0) break;
                var oldest = _active[0];
                _active.RemoveAt(0);
                if (oldest.element != null) oldest.element.RemoveFromHierarchy();
            }

            var el = BuildToastElement(message, kind);
            _container.Add(el);
            _active.Add(new ToastEntry
            {
                element = el,
                spawnTime = Time.unscaledTime,
                message = message,
                kind = kind
            });

            // Fade in
            el.style.opacity = 0f;
            el.schedule.Execute(() => el.style.opacity = 1f).StartingIn((long)(fadeInSeconds * 1000f));
        }

        private VisualElement BuildToastElement(string message, ToastKind kind)
        {
            var el = new VisualElement();
            el.AddToClassList("toast-entry");
            el.AddToClassList("toast-entry-" + kind.ToString().ToLower());

            var icon = new Label(GetIconForKind(kind));
            icon.AddToClassList("toast-icon");
            el.Add(icon);

            var text = new Label(message);
            text.AddToClassList("toast-text");
            el.Add(text);

            return el;
        }

        private static string GetIconForKind(ToastKind kind)
        {
            switch (kind)
            {
                case ToastKind.Success: return "✅";
                case ToastKind.Warning: return "⚠";
                case ToastKind.Error: return "❌";
                default: return "ℹ";
            }
        }

        private void Update()
        {
            if (!_built || _active.Count == 0) return;

            // Remove expired toasts (fade out)
            float now = Time.unscaledTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var entry = _active[i];
                float age = now - entry.spawnTime;
                float fadeOutStart = visibleSeconds;

                if (age >= fadeOutStart && entry.element != null)
                {
                    // Start fade out (only once)
                    if (entry.element.style.opacity.value > 0.01f)
                    {
                        entry.element.style.opacity = 0f;
                        // Remove after fade animation
                        int capturedIdx = i;
                        entry.element.schedule.Execute(() =>
                        {
                            if (capturedIdx < _active.Count && _active[capturedIdx].element == entry.element)
                            {
                                _active.RemoveAt(capturedIdx);
                            }
                            entry.element.RemoveFromHierarchy();
                        }).StartingIn((long)(fadeOutSeconds * 1000f));
                    }
                }
            }
        }
    }
}
