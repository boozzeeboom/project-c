using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectC.Core
{
    /// <summary>
    /// UI панель для навигации по горным пикам.
    /// Отладочный инструмент — доступен только в Editor или при включённом debug-флаге.
    /// </summary>
    public class PeakNavigationUI : MonoBehaviour
    {
        [Header("Ссылки на UI элементы")]
        [SerializeField] private WorldCamera worldCamera;
        [SerializeField] private Transform contentPanel;
        [SerializeField] private GameObject peakButtonPrefab;
        [SerializeField] private TextMeshProUGUI currentPeakText;
        
        [Header("Настройки")]
        [SerializeField] private bool autoPopulate = true;

        [Header("Debug")]
        [Tooltip("Показывать ли PeakNavigationUI. В production build — всегда скрыт.")]
        [SerializeField] private bool showInBuild = false;

        private List<Button> peakButtons = new List<Button>();
        private int currentPeakIndex = -1;
        private WorldGenerator _cachedWorldGenerator;

        private void Start()
        {
            // В production build скрываем, если showInBuild не включён
            bool isEditor = false;
#if UNITY_EDITOR
            isEditor = true;
#endif
            if (!isEditor && !showInBuild)
            {
                gameObject.SetActive(false);
                return;
            }

            if (worldCamera == null)
            {
                worldCamera = FindAnyObjectByType<WorldCamera>();
            }

            if (autoPopulate)
            {
                PopulatePeakList();
            }
        }

        /// <summary>
        /// Заполнить список пиков
        /// </summary>
        public void PopulatePeakList()
        {
            if (contentPanel == null)
            {
                Debug.LogError("[PeakNavigationUI] Content Panel не назначен!");
                return;
            }

            // Очищаем старые кнопки
            foreach (var button in peakButtons)
            {
                Destroy(button.gameObject);
            }
            peakButtons.Clear();

            // Получаем пики
            var worldGenerator = FindAnyObjectByType<WorldGenerator>();
            if (worldGenerator == null)
            {
                Debug.LogWarning("[PeakNavigationUI] WorldGenerator не найден!");
                return;
            }

            var peaks = worldGenerator.GetAllPeaks();

            // Создаём кнопки
            for (int i = 0; i < peaks.Count; i++)
            {
                var peak = peaks[i];
                GameObject buttonObj = Instantiate(peakButtonPrefab, contentPanel);

                TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = $"{i + 1}. {peak.name} ({peak.height:F0}м)";
                }

                int index = i;
                Button button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(() => TeleportToPeak(index));
                    peakButtons.Add(button);
                }
            }
        }

        /// <summary>
        /// Телепорт к пику по индексу
        /// </summary>
        public void TeleportToPeak(int index)
        {
            if (worldCamera != null)
            {
                worldCamera.TeleportToPeak(index);
                currentPeakIndex = index;
                UpdateCurrentPeakText();
            }
        }

        /// <summary>
        /// Следующий пик
        /// </summary>
        public void NextPeak()
        {
            if (worldCamera != null)
            {
                worldCamera.TeleportToNextPeak();
                UpdateCurrentPeakText();
            }
        }

        /// <summary>
        /// Предыдущий пик
        /// </summary>
        public void PreviousPeak()
        {
            if (worldCamera != null)
            {
                worldCamera.TeleportToPreviousPeak();
                UpdateCurrentPeakText();
            }
        }

        /// <summary>
        /// Случайный пик
        /// </summary>
        public void RandomPeak()
        {
            if (worldCamera != null)
            {
                worldCamera.TeleportToRandomPeak();
                UpdateCurrentPeakText();
            }
        }

        /// <summary>
        /// Обновить текст текущего пика
        /// </summary>
        private void UpdateCurrentPeakText()
        {
            if (currentPeakText != null && currentPeakIndex >= 0)
            {
                if (_cachedWorldGenerator == null)
                    _cachedWorldGenerator = FindAnyObjectByType<WorldGenerator>();
                if (_cachedWorldGenerator != null)
                {
                    var peaks = _cachedWorldGenerator.GetAllPeaks();
                    if (currentPeakIndex < peaks.Count)
                    {
                        currentPeakText.text = $"Текущий: {peaks[currentPeakIndex].name} ({peaks[currentPeakIndex].height:F0}м)";
                    }
                }
            }
        }
    }
}
