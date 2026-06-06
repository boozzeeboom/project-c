// =====================================================================================
// ShipKeyToast.cs — UI-компонент тоста "Нет ключа корабля" (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/Ships/Key-subsystem/00_OVERVIEW.md
//
// Назначение: scene-placed MonoBehaviour с UIDocument. Подписывается на
// ShipKeyClientState.OnBoardDenied и показывает Label в нижней части экрана.
//
// MVP-граница: один Label, fade-out по таймеру. Без анимаций, без стекинга сообщений.
// =====================================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Ship.Key;

namespace ProjectC.Ship.Key
{
    [RequireComponent(typeof(UIDocument))]
    public class ShipKeyToast : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Длительность показа тоста в секундах.")]
        [SerializeField] private float _duration = 2.5f;

        [Tooltip("Cooldown между показами (защита от двойного deny).")]
        [SerializeField] private float _cooldown = 0.4f;

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
            // (Мы root, как ShipKeyClientState, так что DDOL безопасен.)
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
            if (ShipKeyClientState.Instance != null)
            {
                ShipKeyClientState.Instance.OnBoardDenied -= ShowToast;
            }
            _subscribed = false;
        }

        /// <summary>
        /// Per-Frame: lazy-build UI + lazy-subscribe to ShipKeyClientState.
        /// Оба idempotent'ны (флаги защищают от повторов).
        /// </summary>
        private void Update()
        {
            if (!_built) TryBuild();
            if (!_subscribed) TrySubscribe();
        }

        private void TryBuild()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null) return;
            if (_doc.rootVisualElement == null) return; // PanelSettings ещё не инициализирован
            if (_doc.panelSettings == null) return;     // нет theme → рендеринг невозможен

            var root = _doc.rootVisualElement;

            // Контейнер — фиксируется внизу экрана, поверх остального UI.
            _container = new VisualElement
            {
                name = "ship-key-toast",
                pickingMode = PickingMode.Ignore
            };
            _container.style.position = Position.Absolute;
            _container.style.bottom = 48;
            _container.style.left = 0;
            _container.style.right = 0;
            _container.style.alignItems = Align.Center;
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            _container.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.7f));
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
                name = "ship-key-toast-label",
                text = "",
                pickingMode = PickingMode.Ignore
            };
            _label.style.color = new StyleColor(new Color(1f, 0.85f, 0.3f, 1f)); // ярко-золотой
            _label.style.fontSize = 20;
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
            if (ShipKeyClientState.Instance == null) return;
            ShipKeyClientState.Instance.OnBoardDenied += ShowToast;
            _subscribed = true;
        }

        private void ShowToast(string message)
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
