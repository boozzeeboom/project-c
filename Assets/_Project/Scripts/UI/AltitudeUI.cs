using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectC.Ship;
using ProjectC.Player;

namespace ProjectC.UI
{
    /// <summary>
    /// UI для отображения предупреждений о высоте корабля.
    /// Показывает статус коридора в HUD (верхний центр экрана).
    /// 
    /// Настройка в Unity:
    /// 1. Создать Canvas → HUD → AltitudeWarning
    /// 2. Добавить TextMeshProUGUI для: statusIcon, statusText, altitudeText, corridorText
    /// 3. Добавить Image для background (фон панели)
    /// 4. Повесить этот скрипт на корень панели
    /// 5. Назначить ссылки в Inspector
    /// 
    /// Цвета статусов:
    /// 🟢 Safe — зелёный
    /// 🟡 Warning — жёлтый
    /// 🔴 Danger — красный
    /// </summary>
    public class AltitudeUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [Tooltip("Icon статуса (🟢/🟡/🔴)")]
        [SerializeField] private TextMeshProUGUI statusIcon;

        [Tooltip("Текст статуса (SAFE/WARNING/DANGER)")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Tooltip("Текст высоты (Altitude: XXXXm)")]
        [SerializeField] private TextMeshProUGUI altitudeText;

        [Tooltip("Текст коридора (Corridor: [1200m — 4450m])")]
        [SerializeField] private TextMeshProUGUI corridorText;

        [Tooltip("Фон панели (для изменения цвета)")]
        [SerializeField] private Image background;

        [Header("Цвета Статусов")]
        [SerializeField] private Color safeColor = new Color(0.2f, 0.8f, 0.2f);       // Зелёный
        [SerializeField] private Color warningColor = new Color(1f, 0.85f, 0f);       // Жёлтый
        [SerializeField] private Color dangerColor = new Color(1f, 0.2f, 0.2f);       // Красный

        [Header("Настройки Обновления")]
        [Tooltip("Как часто обновлять UI (сек)")]
        [SerializeField] private float updateInterval = 0.2f;

        private float _updateTimer;
        private AltitudeStatus _currentStatus;

        // Ссылка на ShipController (устанавливается извне)
        private ShipController _shipController;

        /// <summary>
        /// Инициализировать UI с ссылкой на контроллер корабля.
        /// </summary>
        public void Initialize(ShipController shipController)
        {
            _shipController = shipController;
        }

        private void Update()
        {
            if (_shipController == null) return;

            _updateTimer += Time.deltaTime;
            if (_updateTimer < updateInterval) return;
            _updateTimer = 0f;

            UpdateUI();
        }

        /// <summary>
        /// Обновить UI на основе текущего статуса корабля.
        /// </summary>
        private void UpdateUI()
        {
            // Получаем данные из ShipController
            // Примечание: в текущей версии ShipController не экспортирует эти данные публично
            // Поэтому используем заглушку для демонстрации
            // В продакшене нужно добавить public свойства в ShipController

            float currentAlt = _shipController.transform.position.y;

            // Заглушка: используем глобальный коридор (будет обновлено когда ShipController экспортирует данные)
            AltitudeCorridorData corridor = null;
            AltitudeStatus status = AltitudeStatus.Safe;

            // Пытаемся получить систему коридоров
            var corridorSystem = AltitudeCorridorSystem.Instance;
            if (corridorSystem != null)
            {
                corridor = corridorSystem.GetActiveCorridor(_shipController.transform.position);
                status = corridorSystem.ValidateAltitude(_shipController.transform.position, corridor);
            }

            // Обновляем UI
            SetStatus(status, currentAlt, corridor);
        }

        /// <summary>
        /// Установить статус и обновить визуал.
        /// </summary>
        private void SetStatus(AltitudeStatus status, float altitude, AltitudeCorridorData corridor)
        {
            _currentStatus = status;

            // Обновляем тексты
            if (altitudeText != null)
                altitudeText.text = $"Altitude: {altitude:F0}m";

            if (corridorText != null && corridor != null)
                corridorText.text = $"Corridor: [{corridor.minAltitude:F0}m — {corridor.maxAltitude:F0}m]";

            // Устанавливаем цвета и иконки в зависимости от статуса
            Color textColor;
            string icon;
            string statusLabel;

            switch (status)
            {
                case AltitudeStatus.Safe:
                    icon = "🟢";
                    statusLabel = "SAFE";
                    textColor = safeColor;
                    break;

                case AltitudeStatus.WarningLower:
                    icon = "🟡";
                    statusLabel = "WARNING: Approaching lower limit";
                    textColor = warningColor;
                    break;

                case AltitudeStatus.WarningUpper:
                    icon = "🟡";
                    statusLabel = "WARNING: Approaching upper limit";
                    textColor = warningColor;
                    break;

                case AltitudeStatus.DangerLower:
                    icon = "🔴";
                    statusLabel = "DANGER: BELOW CORRIDOR! TURBULENCE!";
                    textColor = dangerColor;
                    break;

                case AltitudeStatus.DangerUpper:
                    icon = "🔴";
                    statusLabel = "DANGER: ABOVE CRITICAL ALTITUDE!";
                    textColor = dangerColor;
                    break;

                default:
                    icon = "⚪";
                    statusLabel = "UNKNOWN";
                    textColor = Color.white;
                    break;
            }

            if (statusIcon != null)
                statusIcon.text = icon;

            if (statusText != null)
                statusText.text = statusLabel;

            // Применяем цвет ко всем текстам
            if (statusIcon != null) statusIcon.color = textColor;
            if (statusText != null) statusText.color = textColor;
            if (altitudeText != null) altitudeText.color = textColor;
            if (corridorText != null) corridorText.color = textColor;

            // Меняем цвет фона
            if (background != null)
            {
                Color bgColor = textColor;
                bgColor.a = 0.3f; // Полупрозрачный фон
                background.color = bgColor;
            }
        }

        /// <summary>
        /// Скрыть панель предупреждений.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Показать панель предупреждений.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }
    }
}
