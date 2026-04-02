using UnityEngine;
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
        [SerializeField] private TextMeshProUGUI hintsText;
        
        [Header("Настройки")]
        [Tooltip("Показывать ли подсказки")]
        [SerializeField] private bool showHints = true;
        
        [Tooltip("Прозрачность фона")]
        [SerializeField] private float backgroundAlpha = 0.7f;

        [Header("Цвета")]
        [SerializeField] private Color titleColor = Color.yellow;
        [SerializeField] private Color keyColor = Color.cyan;
        [SerializeField] private Color textColor = Color.white;

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
        }

        /// <summary>
        /// Обновить текст подсказок
        /// </summary>
        public void UpdateHints()
        {
            if (hintsText == null) return;

            string hints = $@"<color=#{ColorToHex(titleColor)}><b>🎮 Управление</b></color>

<color=#{ColorToHex(keyColor)}><b>Навигация</b></color>
<color=#{ColorToHex(textColor)}><b>W A S D</b></color> - Полёт
<color=#{ColorToHex(textColor)}><b>Мышь</b></color> - Обзор
<color=#{ColorToHex(textColor)}><b>Shift</b></color> - Ускорение

<color=#{ColorToHex(keyColor)}><b>Телепортация</b></color>
<color=#{ColorToHex(textColor)}><b>N / PgUp</b></color> - Следующий пик ⬆️
<color=#{ColorToHex(textColor)}><b>B / PgDown</b></color> - Предыдущий пик ⬇️
<color=#{ColorToHex(textColor)}><b>R</b></color> - Случайный пик 🎲
<color=#{ColorToHex(textColor)}><b>H</b></color> - На высоту ⬆️

<color=#{ColorToHex(keyColor)}><b>Режимы</b></color>
<color=#{ColorToHex(textColor)}><b>F</b></color> - Полёт вкл/выкл
<color=#{ColorToHex(textColor)}><b>Колёсико</b></color> - Высота";

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

        /// <summary>
        /// Переключить видимость подсказок (F1)
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                ToggleHints();
            }
        }

        public void ToggleHints()
        {
            showHints = !showHints;
            if (hintsText != null)
            {
                hintsText.gameObject.SetActive(showHints);
            }
            Debug.Log($"[ControlHints] Подсказки: {(showHints ? "вкл" : "выкл")}");
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
