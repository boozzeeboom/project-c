using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using ProjectC.Localization;
using ProjectC.Player;

namespace ProjectC.UI
{
    public enum InteractionHintKind
    {
        None,
        Talk,
        UseE,
        Use,
    }

    /// <summary>
    /// Подсказки по управлению на экране
    /// </summary>
    public class ControlHintsUI : MonoBehaviour
    {
        public static ControlHintsUI Instance { get; private set; }

        [Header("Ссылки на UI элементы")]
        [Tooltip("Текст подсказок")]
        public TextMeshProUGUI hintsText;

        [Tooltip("Контекстная подсказка взаимодействия")]
        public TextMeshProUGUI interactionHintText;

        [Header("Настройки")]
        [Tooltip("Показывать ли подсказки")]
        [SerializeField] private bool showHints = true;

        [Header("Цвета")]
        [SerializeField] private Color titleColor = Color.yellow;
        [SerializeField] private Color keyColor = Color.cyan;
        [SerializeField] private Color textColor = Color.white;

        // Input System
        private InputAction _toggleHintsAction;
        private InteractionHintKind _interactionHintKind = InteractionHintKind.None;
        private bool _localeSubscribed;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (hintsText == null)
            {
                hintsText = GetComponent<TextMeshProUGUI>();
            }

            if (hintsText == interactionHintText)
            {
                hintsText = null;
            }

            if (hintsText == null)
            {
                // Пытаемся найти Text автоматически
                hintsText = FindAnyObjectByType<TextMeshProUGUI>();
                if (hintsText == interactionHintText)
                {
                    hintsText = null;
                }
            }

            EnsureInteractionHintText();

            if (hintsText != null)
            {
                UpdateHints();
                hintsText.enabled = showHints;
            }
            else
            {
                Debug.LogWarning("[ControlHintsUI] Hints Text не назначен! Подсказки не будут показаны.");
            }

            SubscribeToLocaleChanges();
            RefreshInteractionHint();

            // Создаём Input Action программно
            _toggleHintsAction = new InputAction("ToggleHints", binding: "<Keyboard>/f1", expectedControlType: "Button");
            _toggleHintsAction.performed += ctx => ToggleHints();
            _toggleHintsAction.Enable();
        }

        private void OnEnable()
        {
            if (_toggleHintsAction != null)
                _toggleHintsAction.Enable();

            SubscribeToLocaleChanges();
        }

        private void OnDisable()
        {
            if (_toggleHintsAction != null)
                _toggleHintsAction.Disable();

            UnsubscribeFromLocaleChanges();
        }

        private void OnDestroy()
        {
            UnsubscribeFromLocaleChanges();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void SubscribeToLocaleChanges()
        {
            if (_localeSubscribed) return;
            Loc.OnLocaleChanged += HandleLocaleChanged;
            _localeSubscribed = true;
        }

        private void UnsubscribeFromLocaleChanges()
        {
            if (!_localeSubscribed) return;
            Loc.OnLocaleChanged -= HandleLocaleChanged;
            _localeSubscribed = false;
        }

        private void HandleLocaleChanged()
        {
            RefreshInteractionHint();
        }

        private void EnsureInteractionHintText()
        {
            if (interactionHintText == null)
            {
                var child = transform.Find("InteractionHintText");
                if (child != null)
                {
                    interactionHintText = child.GetComponent<TextMeshProUGUI>();
                }
            }

            var canvas = GetComponentInParent<Canvas>();
            if (interactionHintText == null && canvas != null)
            {
                var hintObject = new GameObject(
                    "InteractionHintText",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                hintObject.transform.SetParent(canvas.transform, false);
                interactionHintText = hintObject.GetComponent<TextMeshProUGUI>();
            }

            if (interactionHintText == null) return;

            if (canvas != null && interactionHintText.transform.parent != canvas.transform)
            {
                interactionHintText.transform.SetParent(canvas.transform, false);
            }

            var rect = interactionHintText.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-20f, 20f);
            rect.sizeDelta = new Vector2(500f, 50f);

            interactionHintText.alignment = TextAlignmentOptions.BottomRight;
            interactionHintText.fontSize = 24f;
            interactionHintText.color = Color.white;
            interactionHintText.raycastTarget = false;
            interactionHintText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        public void SetInteractionHint(InteractionHintKind kind)
        {
            if (_interactionHintKind == kind) return;
            _interactionHintKind = kind;
            RefreshInteractionHint();
        }

        public InteractionHintKind GetInteractionHintKind() => _interactionHintKind;

        private void RefreshInteractionHint()
        {
            if (interactionHintText == null) return;

            bool visible = _interactionHintKind != InteractionHintKind.None;
            if (interactionHintText.gameObject.activeSelf != visible)
            {
                interactionHintText.gameObject.SetActive(visible);
            }

            if (!visible) return;

            string key;
            string fallback;
            switch (_interactionHintKind)
            {
                case InteractionHintKind.Talk:
                    key = "ui.interaction_hint.talk";
                    fallback = "Нажмите E, чтобы поговорить";
                    break;
                case InteractionHintKind.UseE:
                    key = "ui.interaction_hint.use_e";
                    fallback = "Нажмите E, чтобы использовать";
                    break;
                case InteractionHintKind.Use:
                    key = "ui.interaction_hint.use";
                    fallback = "Нажмите F, чтобы использовать";
                    break;
                default:
                    return;
            }

            string localized = Loc.Get(key, fallback);
            if (interactionHintText.text != localized)
            {
                interactionHintText.text = localized;
            }
        }

        /// <summary>
        /// Обновить текст подсказок
        /// </summary>
        public void UpdateHints()
        {
            if (hintsText == null) return;

            string hints = $@"<color=#{ColorToHex(titleColor)}><b>Управление</b></color>

<color=#{ColorToHex(keyColor)}><b>Персонаж</b></color>
<color=#{ColorToHex(textColor)}><b>W</b></color> - Вперёд
<color=#{ColorToHex(textColor)}><b>S</b></color> - Назад
<color=#{ColorToHex(textColor)}><b>A D</b></color> - Стрейф
<color=#{ColorToHex(textColor)}><b>Мышь</b></color> - Вращение камеры
<color=#{ColorToHex(textColor)}><b>Space</b></color> - Прыжок
<color=#{ColorToHex(textColor)}><b>Left Shift</b></color> - Бег
<color=#{ColorToHex(textColor)}><b>F</b></color> - Сесть в корабль / выйти
<color=#{ColorToHex(textColor)}><b>E</b></color> - Подобрать предмет / открыть сундук
<color=#{ColorToHex(textColor)}><b>Tab</b></color> - Открыть инвентарь

<color=#{ColorToHex(keyColor)}><b>Корабль</b></color>
<color=#{ColorToHex(textColor)}><b>W/S</b></color> - Тяга
<color=#{ColorToHex(textColor)}><b>A/D</b></color> - Рыскание
<color=#{ColorToHex(textColor)}><b>Q/E</b></color> - Вниз/Вверх (лифт)
<color=#{ColorToHex(textColor)}><b>Мышь</b></color> - Тангаж
<color=#{ColorToHex(textColor)}><b>Shift</b></color> - Ускорение

<color=#{ColorToHex(keyColor)}><b>F1</b></color> - Скрыть/показать";

            hintsText.text = hints;
        }

        /// <summary>
        /// Конвертация цвета в HEX
        /// </summary>
        private string ColorToHex(Color color)
        {
            return string.Format("{0:X2}{1:X2}{2:X2}",
                (int)(color.r * 255),
                (int)(color.g * 255),
                (int)(color.b * 255));
        }

        public void ToggleHints()
        {
            showHints = !showHints;
            if (hintsText != null)
            {
                hintsText.enabled = showHints;
            }
        }

        public void ShowHints(bool show)
        {
            showHints = show;
            if (hintsText != null)
            {
                hintsText.enabled = show;
            }
        }
    }
}
