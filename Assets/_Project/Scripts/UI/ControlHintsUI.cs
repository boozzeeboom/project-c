using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace ProjectC.UI
{
    /// <summary>
    /// Подсказки по управлению на экране
    /// </summary>
    public class ControlHintsUI : MonoBehaviour
    {
        [Header("Ссылки на UI элементы")]
        [Tooltip("Текст подсказок")]
        public TextMeshProUGUI hintsText;

        [Header("Настройки")]
        [Tooltip("Показывать ли подсказки")]
        [SerializeField] private bool showHints = true;

        [Header("Цвета")]
        [SerializeField] private Color titleColor = Color.yellow;
        [SerializeField] private Color keyColor = Color.cyan;
        [SerializeField] private Color textColor = Color.white;

        // Input System
        private InputAction _toggleHintsAction;

        private void Start()
        {
            if (hintsText == null)
            {
                // Пытаемся найти Text автоматически
                hintsText = FindAnyObjectByType<TextMeshProUGUI>();
            }

            if (hintsText != null && showHints)
            {
                UpdateHints();
            }
            else if (hintsText == null)
            {
                Debug.LogWarning("[ControlHintsUI] Hints Text не назначен! Подсказки не будут показаны.");
            }

            // Создаём Input Action программно
            _toggleHintsAction = new InputAction("ToggleHints", binding: "<Keyboard>/f1", expectedControlType: "Button");
            _toggleHintsAction.performed += ctx => ToggleHints();
            _toggleHintsAction.Enable();
        }

        private void OnEnable()
        {
            if (_toggleHintsAction != null)
                _toggleHintsAction.Enable();
        }

        private void OnDisable()
        {
            if (_toggleHintsAction != null)
                _toggleHintsAction.Disable();
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
                hintsText.gameObject.SetActive(showHints);
            }
        }

        public void ShowHints(bool show)
        {
            showHints = show;
            if (hintsText != null)
            {
                hintsText.gameObject.SetActive(show);
            }
        }
    }
}
