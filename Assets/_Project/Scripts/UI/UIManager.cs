using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.UI
{
    /// <summary>
    /// Централизованный менеджер UI:
    /// - Отслеживает открытые панели (z-ordering)
    /// - Управляет приоритетами ввода (верхняя панель получает ввод)
    /// - Unified close (Escape закрывает верхнюю панель)
    /// - Cursor lock/unlock автоматически
    /// 
    /// Спринт 3: Задачи 3.2 + 3.6
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Input Settings")]
        [Tooltip("Клавиша для закрытия верхней панели")]
        public KeyCode CloseKey = KeyCode.Escape;

        [Header("Audio Feedback")]
        [Tooltip("Звук клика по кнопке")]
        public AudioClip ClickSound;
        [Tooltip("Звук открытия панели")]
        public AudioClip OpenSound;
        [Tooltip("Звук закрытия панели")]
        public AudioClip CloseSound;
        [Tooltip("Звук ошибки")]
        public AudioClip ErrorSound;

        private AudioSource _audioSource;

        /// <summary>
        /// Стек открытых UI панелей (верхняя = последняя в списке)
        /// </summary>
        private List<UIPanelInfo> _openPanels = new List<UIPanelInfo>();

        /// <summary>
        /// Информация об открытой UI панели
        /// </summary>
        public class UIPanelInfo
        {
            public string PanelName;
            public int Priority; // Чем выше — тем важнее (получает ввод)
            public System.Action OnClose;
            public GameObject PanelObject;

            public UIPanelInfo(string name, int priority, System.Action onClose = null, GameObject obj = null)
            {
                PanelName = name;
                Priority = priority;
                OnClose = onClose;
                PanelObject = obj;
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Создаём AudioSource для UI звуков
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.outputAudioMixerGroup = null;
        }

        private void Update()
        {
            HandleGlobalInput();
        }

        /// <summary>
        /// Обработка глобального ввода (Escape для закрытия)
        /// </summary>
        private void HandleGlobalInput()
        {
            if (_openPanels.Count == 0) return;

            if (Keyboard.current == null) return;

            var key = KeyCodeToInputKey(CloseKey);
            if (key == UnityEngine.InputSystem.Key.None) return;

            if (Keyboard.current[key].wasPressedThisFrame)
            {
                CloseTopPanel();
            }
        }

        /// <summary>
        /// Конвертировать KeyCode в Key (Input System)
        /// </summary>
        private static UnityEngine.InputSystem.Key KeyCodeToInputKey(KeyCode keyCode)
        {
            // Простая конвертация через enum cast (совпадает для большинства клавиш)
            if (System.Enum.IsDefined(typeof(UnityEngine.InputSystem.Key), (int)keyCode))
            {
                return (UnityEngine.InputSystem.Key)(int)keyCode;
            }
            return UnityEngine.InputSystem.Key.None;
        }

        // ==================== PANEL MANAGEMENT ====================

        /// <summary>
        /// Открыть UI панель
        /// </summary>
        /// <param name="panelName">Уникальное имя панели</param>
        /// <param name="priority">Приоритет (выше = поверх остальных)</param>
        /// <param name="onClose">Callback при закрытии</param>
        /// <param name="panelObj">GameObject панели (для cursor management)</param>
        public void OpenPanel(string panelName, int priority, System.Action onClose = null, GameObject panelObj = null)
        {
            // Проверяем не открыта ли уже
            if (IsPanelOpen(panelName))
            {
                Debug.LogWarning($"[UIManager] Панель {panelName} уже открыта");
                return;
            }

            var info = new UIPanelInfo(panelName, priority, onClose, panelObj);
            _openPanels.Add(info);

            // Сортируем по приоритету (верхняя = highest priority)
            _openPanels.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            // Разблокируем курсор
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Звук открытия
            PlaySound(OpenSound);

            Debug.Log($"[UIManager] Открыта панель: {panelName} (priority: {priority})");
        }

        /// <summary>
        /// Закрыть UI панель
        /// </summary>
        public void ClosePanel(string panelName)
        {
            var info = _openPanels.Find(p => p.PanelName == panelName);
            if (info == null)
            {
                Debug.LogWarning($"[UIManager] Панель {panelName} не найдена для закрытия");
                return;
            }

            _openPanels.Remove(info);

            // Вызываем callback закрытия
            info.OnClose?.Invoke();

            // Если панелей больше нет — блокируем курсор
            if (_openPanels.Count == 0)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Звук закрытия
            PlaySound(CloseSound);

            Debug.Log($"[UIManager] Закрыта панель: {panelName}");
        }

        /// <summary>
        /// Закрыть верхнюю панель (по приоритету)
        /// </summary>
        public void CloseTopPanel()
        {
            if (_openPanels.Count == 0) return;

            var topPanel = _openPanels[0]; // Уже отсортировано
            ClosePanel(topPanel.PanelName);
        }

        /// <summary>
        /// Проверить открыта ли панель
        /// </summary>
        public bool IsPanelOpen(string panelName)
        {
            return _openPanels.Exists(p => p.PanelName == panelName);
        }

        /// <summary>
        /// Получить верхнюю открытую панель
        /// </summary>
        public UIPanelInfo GetTopPanel()
        {
            return _openPanels.Count > 0 ? _openPanels[0] : null;
        }

        /// <summary>
        /// Проверить может ли панель получать ввод (она верхняя?)
        /// </summary>
        public bool CanReceiveInput(string panelName)
        {
            var top = GetTopPanel();
            return top != null && top.PanelName == panelName;
        }

        /// <summary>
        /// Закрыть все открытые панели
        /// </summary>
        public void CloseAllPanels()
        {
            while (_openPanels.Count > 0)
            {
                CloseTopPanel();
            }
        }

        // ==================== AUDIO FEEDBACK ====================

        /// <summary>
        /// Воспроизвести UI звук
        /// </summary>
        public void PlaySound(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;

            _audioSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Воспроизвести звук клика (для кнопок)
        /// </summary>
        public void PlayClick()
        {
            PlaySound(ClickSound);
        }

        /// <summary>
        /// Воспроизвести звук ошибки
        /// </summary>
        public void PlayError()
        {
            PlaySound(ErrorSound);
        }

        // ==================== HELPERS ====================

        /// <summary>
        /// Создать UIManager если ещё не существует
        /// </summary>
        public static UIManager EnsureExists()
        {
            if (Instance == null)
            {
                var go = new GameObject("[UIManager]");
                Instance = go.AddComponent<UIManager>();
                Debug.Log("[UIManager] Создан автоматически");
            }
            return Instance;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
