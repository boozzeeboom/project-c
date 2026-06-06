// =====================================================================================
// MetaRequirementToast.cs — UI-компонент тоста отказа в доступе (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/MetaRequirement/00_OVERVIEW.md
//
// Назначение: scene-placed MonoBehaviour с UIDocument. Подписывается на
// MetaRequirementClientState.OnAccessDenied и показывает Label в нижней части экрана.
//
// MVP-граница: один Label, fade-out по таймеру. Без анимаций, без стекинга сообщений.
// Показывает reason (human-readable) как есть. В v2 — multiline с иконками предметов.
// =====================================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.MetaRequirement
{
    [RequireComponent(typeof(UIDocument))]
    public class MetaRequirementToast : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Длительность показа тоста в секундах.")]
        [SerializeField] private float _duration = 2.5f;

        [Tooltip("Cooldown между показами (защита от двойного deny).")]
        [SerializeField] private float _cooldown = 0.4f;

        [Header("Внешний вид")]
        [Tooltip("Цвет текста тоста.")]
        [SerializeField] private Color _textColor = new Color(1f, 0.85f, 0.3f, 1f); // ярко-золотой, как у Ship Key
        [Tooltip("Цвет фона (alpha < 1 = полупрозрачный).")]
        [SerializeField] private Color _backgroundColor = new Color(0f, 0f, 0f, 0.7f);
        [Tooltip("Размер шрифта.")]
        [SerializeField] private int _fontSize = 20;

        private UIDocument _doc;
        private VisualElement _container;
        private Label _label;
        private Coroutine _hideCoroutine;
        private float _lastShowTime = -10f;
        private bool _built = false;
        private bool _subscribed = false;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            // Делаем этот объект DontDestroyOnLoad — toast должен переживать стриминг сцен.
            // (Мы root, как MetaRequirementClientState, так что DDOL безопасен.)
            if (transform.parent == null && Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_hideCoroutine != null) { StopCoroutine(_hideCoroutine); _hideCoroutine = null; }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (MetaRequirementClientState.Instance != null)
            {
                MetaRequirementClientState.Instance.OnAccessDenied -= ShowToast;
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
                name = "meta-requirement-toast",
                pickingMode = PickingMode.Ignore
            };
            _container.style.position = Position.Absolute;
            _container.style.bottom = 48;
            _container.style.left = 0;
            _container.style.right = 0;
            _container.style.alignItems = Align.Center;
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            _container.style.backgroundColor = new StyleColor(_backgroundColor);
            _container.style.paddingTop = 10;
            _container.style.paddingBottom = 10;
            _container.style.paddingLeft = 24;
            _container.style.paddingRight = 24;
            _container.style.borderTopLeftRadius = 8;
            _container.style.borderTopRightRadius = 8;
            _container.style.borderBottomLeftRadius = 8;
            _container.style.borderBottomRightRadius = 8;
            _container.style.marginLeft = 24;
            _container.style.marginRight = 24;

            _label = new Label
            {
                name = "meta-requirement-toast-label",
                text = "",
                pickingMode = PickingMode.Ignore
            };
            _label.style.color = new StyleColor(_textColor);
            _label.style.fontSize = _fontSize;
            _label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.whiteSpace = WhiteSpace.Normal;
            _label.style.textShadow = new TextShadow
            {
                offset = new Vector2(1, 1),
                blurRadius = 2,
                color = new Color(0, 0, 0, 0.9f)
            };

            _container.Add(_label);
            root.Add(_container);
            _built = true;
        }

        private void TrySubscribe()
        {
            if (MetaRequirementClientState.Instance == null) return;
            MetaRequirementClientState.Instance.OnAccessDenied += ShowToast;
            _subscribed = true;
        }

        /// <summary>Public API: показать toast с заданным сообщением (для тестов / внешних вызовов).</summary>
        public void ShowToastExternal(string message) => ShowToast(0, message);

        private void ShowToast(ulong netId, string message)
        {
            if (!_built) TryBuild();
            if (_container == null || _label == null) return;
            if (Time.unscaledTime - _lastShowTime < _cooldown) return;
            _lastShowTime = Time.unscaledTime;

            _label.text = message;
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);

            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfter(_duration));
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_container != null)
            {
                _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            }
            _hideCoroutine = null;
        }
    }
}
