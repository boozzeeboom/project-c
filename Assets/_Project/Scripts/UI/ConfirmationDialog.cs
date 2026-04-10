using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectC.UI
{
    /// <summary>
    /// Система диалогов подтверждения для предотвращения случайных действий.
    /// Спринт 3: Задача 3.4
    /// 
    /// Использование:
    /// ConfirmationDialog.Show(
    ///     title: "Подтверждение покупки",
    ///     message: "Купить 1x antigrav_ingot_v01 за 25 CR?",
    ///     onConfirm: () => BuyItem(),
    ///     confirmText: "КУПИТЬ",
    ///     cancelText: "ОТМЕНА"
    /// );
    /// </summary>
    public class ConfirmationDialog : MonoBehaviour
    {
        public static ConfirmationDialog Instance { get; private set; }

        private GameObject _rootCanvas;
        private GameObject _dialogPanel;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _messageText;
        private Button _confirmBtn;
        private Button _cancelBtn;
        private TextMeshProUGUI _confirmText;
        private TextMeshProUGUI _cancelText;

        private System.Action _onConfirm;
        private System.Action _onCancel;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Показать диалог подтверждения
        /// </summary>
        /// <param name="title">Заголовок диалога</param>
        /// <param name="message">Сообщение</param>
        /// <param name="onConfirm">Callback при подтверждении</param>
        /// <param name="onCancel">Callback при отмене (null = просто закрыть)</param>
        /// <param name="confirmText">Текст на кнопке подтверждения</param>
        /// <param name="cancelText">Текст на кнопке отмены</param>
        /// <param name="priority">Приоритет панели (по умолчанию 999 — поверх всего)</param>
        public static void Show(
            string title,
            string message,
            System.Action onConfirm,
            System.Action onCancel = null,
            string confirmText = "ПОДТВЕРДИТЬ",
            string cancelText = "ОТМЕНА",
            int priority = 999)
        {
            var dialog = EnsureExists();
            dialog.ShowDialog(title, message, onConfirm, onCancel, confirmText, cancelText, priority);
        }

        /// <summary>
        /// Скрыть диалог
        /// </summary>
        public static void Hide()
        {
            if (Instance != null)
            {
                Instance.HideDialog();
            }
        }

        private void ShowDialog(
            string title,
            string message,
            System.Action onConfirm,
            System.Action onCancel,
            string confirmText,
            string cancelText,
            int priority)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            // Создаём UI если ещё не создан
            if (_rootCanvas == null)
            {
                BuildUI();
            }

            if (_rootCanvas == null)
            {
                Debug.LogError("[ConfirmationDialog] Не удалось создать UI!");
                onConfirm?.Invoke(); // Fallback — выполняем действие без подтверждения
                return;
            }

            // Устанавливаем тексты
            if (_titleText != null) _titleText.text = title;
            if (_messageText != null) _messageText.text = message;
            if (_confirmText != null) _confirmText.text = confirmText;
            if (_cancelText != null) _cancelText.text = cancelText;

            // Показываем панель
            _dialogPanel.SetActive(true);
            _dialogPanel.transform.SetAsLastSibling();

            // Регистрируем в UIManager
            UIManager.EnsureExists().OpenPanel("ConfirmationDialog", priority, OnDialogClosed, _dialogPanel);

            Debug.Log($"[ConfirmationDialog] Показан: {title}");
        }

        private void HideDialog()
        {
            if (_dialogPanel != null)
            {
                _dialogPanel.SetActive(false);
            }

            // Закрываем через UIManager
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ClosePanel("ConfirmationDialog");
            }

            _onConfirm = null;
            _onCancel = null;
        }

        private void OnDialogClosed()
        {
            // Ничего не делаем — курсор заблокируется автоматически через UIManager
        }

        // ==================== КНОПКИ ====================

        private void OnConfirmClicked()
        {
            UIManager.Instance?.PlayClick();
            HideDialog();
            _onConfirm?.Invoke();
        }

        private void OnCancelClicked()
        {
            UIManager.Instance?.PlayClick();
            HideDialog();
            _onCancel?.Invoke();
        }

        // ==================== UI BUILD ====================

        private void BuildUI()
        {
            var theme = UITheme.Default;

            // Root Canvas
            _rootCanvas = UIFactory.CreateRootCanvas("[ConfirmationDialog]_RootCanvas", 10000);

            // Затемнение фона
            var bgPanel = UIFactory.CreatePanel("DialogBackground", _rootCanvas.transform, 0, 0, Screen.width, Screen.height);
            var bgImage = bgPanel.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = new Color(0f, 0f, 0f, 0.7f);
            }

            // Диалоговая панель (центр)
            _dialogPanel = UIFactory.CreatePanel("DialogPanel", _rootCanvas.transform, 0, 0, 400, 250);
            _dialogPanel.SetActive(false);

            // Заголовок
            _titleText = UIFactory.CreateLabel(
                "DialogTitle",
                _dialogPanel.transform,
                "Подтверждение",
                0, 90,
                theme.FontSizeSubheading,
                theme.TextTitle,
                360
            );

            // Сообщение
            _messageText = UIFactory.CreateLabel(
                "DialogMessage",
                _dialogPanel.transform,
                "Вы уверены?",
                0, 40,
                theme.FontSizeBody,
                theme.TextPrimary,
                360
            );

            // Кнопка подтверждения
            _confirmBtn = UIFactory.CreateButton(
                "ConfirmBtn",
                _dialogPanel.transform,
                "ПОДТВЕРДИТЬ",
                0, -60,
                160, 40,
                OnConfirmClicked
            );
            _confirmText = _confirmBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Кнопка отмены
            _cancelBtn = UIFactory.CreateButton(
                "CancelBtn",
                _dialogPanel.transform,
                "ОТМЕНА",
                0, -110,
                160, 40,
                OnCancelClicked
            );
            _cancelText = _cancelBtn.GetComponentInChildren<TextMeshProUGUI>();
        }

        private static ConfirmationDialog EnsureExists()
        {
            if (Instance == null)
            {
                var go = new GameObject("[ConfirmationDialog]");
                Instance = go.AddComponent<ConfirmationDialog>();
                Debug.Log("[ConfirmationDialog] Создан автоматически");
            }
            return Instance;
        }
    }
}
