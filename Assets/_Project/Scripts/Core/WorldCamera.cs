using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectC.UI;

namespace ProjectC.Core
{
    /// <summary>
    /// Камера для мира Project C
    /// Следует за игроком и поддерживает режимы полёта/хождения
    /// </summary>
    public class WorldCamera : MonoBehaviour
    {
        [Header("Настройки камеры")]
        [Tooltip("Цель для слежения (игрок)")]
        [SerializeField] private Transform target;
        
        [Tooltip("Дистанция до цели")]
        [SerializeField] private float distance = 50f;
        
        [Tooltip("Высота камеры")]
        [SerializeField] private float height = 20f;

        [Header("Стартовая позиция")]
        [Tooltip("Начальная высота камеры при старте (уровень облаков)")]
        [SerializeField] private float startHeight = 500f;
        
        [Tooltip("Автоматически лететь к первому пику при старте")]
        [SerializeField] private bool flyToFirstPeakOnStart = true;

        private void Awake()
        {
            // Принудительно устанавливаем высоту облаков, если значение слишком большое
            if (startHeight > 1000f)
            {
                Debug.LogWarning($"[WorldCamera] StartHeight слишком большой ({startHeight}м), установлено 500м");
                startHeight = 500f;
            }
        }

        [Header("Настройки полёта")]
        [Tooltip("Скорость движения камеры в режиме полёта")]
        [SerializeField] private float flySpeed = 100f;
        
        [Tooltip("Ускорение в режиме полёта")]
        [SerializeField] private float flyAcceleration = 10f;
        
        [Tooltip("Максимальная скорость")]
        [SerializeField] private float maxSpeed = 500f;

        [Header("Настройки вращения")]
        [Tooltip("Чувствительность мыши по горизонтали")]
        [SerializeField] private float mouseSensitivityX = 3f;
        
        [Tooltip("Чувствительность мыши по вертикали")]
        [SerializeField] private float mouseSensitivityY = 3f;
        
        [Tooltip("Минимальный угол обзора по вертикали")]
        [SerializeField] private float minVerticalAngle = -89f;
        
        [Tooltip("Максимальный угол обзора по вертикали")]
        [SerializeField] private float maxVerticalAngle = 89f;

        [Header("Быстрое перемещение")]
        [Tooltip("Скорость телепортации к пику")]
        [SerializeField] private float teleportSpeed = 200f;

        private float currentX = 0f;
        private float currentY = 0f;
        private Vector3 currentVelocity = Vector3.zero;
        private bool isFlying = true;
        private bool isTeleporting = false;
        private Vector3 teleportTarget = Vector3.zero;
        private WorldGenerator worldGenerator;

        private void Start()
        {
            // Скрываем курсор
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Инициализируем углы
            Vector3 angles = transform.eulerAngles;
            currentX = angles.y;
            currentY = angles.x;

            // Находим WorldGenerator для получения списка пиков
            worldGenerator = FindAnyObjectByType<WorldGenerator>();

            // Принудительно устанавливаем стартовую высоту
            transform.position = new Vector3(0, startHeight, 0);
            Debug.Log($"[WorldCamera] Стартовая высота: {startHeight}м");

            // Создаём UI подсказок автоматически, если нет на сцене
            CreateControlHintsUI();

            // Устанавливаем стартовую позицию
            if (flyToFirstPeakOnStart && worldGenerator != null)
            {
                var peaks = worldGenerator.GetAllPeaks();
                if (peaks.Count > 0)
                {
                    // Телепорт к первому пику
                    TeleportToPeak(0);
                }
            }
        }

        /// <summary>
        /// Создать UI подсказок автоматически
        /// </summary>
        private void CreateControlHintsUI()
        {
            // Проверяем, есть ли уже ControlHintsUI на сцене
            var existingHints = FindAnyObjectByType<ControlHintsUI>();
            if (existingHints != null)
            {
                Debug.Log("[WorldCamera] ControlHintsUI уже существует на сцене");
                return;
            }

            // Создаём Canvas если нет
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Debug.Log("[WorldCamera] Создан Canvas");
            }

            // Создаём TextMeshPro
            var textObj = new GameObject("ControlHintsText");
            textObj.transform.SetParent(canvas.transform);
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(20, -20);
            rectTransform.sizeDelta = new Vector2(300, 400);

            var tmpText = textObj.AddComponent<TextMeshProUGUI>();
            tmpText.fontSize = 14;
            tmpText.color = Color.white;
            tmpText.alignment = TextAlignmentOptions.TopLeft;

            // Создаём ControlHintsUI
            GameObject hintsObj = new GameObject("ControlHintsUI");
            hintsObj.transform.SetParent(canvas.transform);
            var controlHints = hintsObj.AddComponent<ControlHintsUI>();
            controlHints.hintsText = tmpText;

            Debug.Log("[WorldCamera] Создан ControlHintsUI автоматически");
        }

        private void LateUpdate()
        {
            // Обработка телепортации
            if (isTeleporting)
            {
                HandleTeleport();
                return;
            }

            if (target != null)
            {
                FollowTarget();
            }
            
            HandleRotation();
            HandleMovement();
            HandleHotkeys();
        }

        /// <summary>
        /// Обработка телепортации к пику
        /// </summary>
        private void HandleTeleport()
        {
            transform.position = Vector3.MoveTowards(transform.position, teleportTarget, teleportSpeed * Time.deltaTime);
            
            if (Vector3.Distance(transform.position, teleportTarget) < 1f)
            {
                isTeleporting = false;
                Debug.Log("[WorldCamera] Телепортация завершена");
            }
        }

        /// <summary>
        /// Горячие клавиши для управления
        /// </summary>
        private void HandleHotkeys()
        {
            // Переключение режима полёта (F)
            if (Input.GetKeyDown(KeyCode.F))
            {
                ToggleFlyMode();
            }

            // Телепорт к следующему пику (N или PageUp)
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.PageUp))
            {
                TeleportToNextPeak();
            }

            // Телепорт к предыдущему пику (B или PageDown)
            if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.PageDown))
            {
                TeleportToPreviousPeak();
            }

            // Телепорт к случайному пику (R)
            if (Input.GetKeyDown(KeyCode.R))
            {
                TeleportToRandomPeak();
            }

            // Возврат на высокую позицию (H)
            if (Input.GetKeyDown(KeyCode.H))
            {
                transform.position = new Vector3(
                    transform.position.x,
                    500f,
                    transform.position.z
                );
                Debug.Log("[WorldCamera] Возврат на высоту облаков");
            }

            // Ускорение (Left Shift)
            if (Input.GetKey(KeyCode.LeftShift))
            {
                flySpeed = maxSpeed;
            }
            else
            {
                flySpeed = 100f;
            }
        }

        /// <summary>
        /// Следование за целью
        /// </summary>
        private void FollowTarget()
        {
            if (target == null) return;

            Vector3 targetPosition = target.position + Vector3.up * height;
            
            // Плавное следование
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition - transform.forward * distance,
                ref currentVelocity,
                0.3f
            );
        }

        /// <summary>
        /// Обработка вращения камеры
        /// </summary>
        private void HandleRotation()
        {
            currentX += Input.GetAxis("Mouse X") * mouseSensitivityX;
            currentY -= Input.GetAxis("Mouse Y") * mouseSensitivityY;
            currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);

            transform.eulerAngles = new Vector3(currentY, currentX, 0);
        }

        /// <summary>
        /// Обработка движения камеры (режим полёта)
        /// </summary>
        private void HandleMovement()
        {
            if (!isFlying) return;

            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            
            // Движение вперёд/назад + стрейф
            Vector3 moveDirection = transform.right * h + transform.forward * v;
            
            // Ускорение
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                moveDirection * flySpeed,
                flyAcceleration * Time.deltaTime
            );

            transform.position += currentVelocity * Time.deltaTime;

            // Высота (вверх/вниз)
            float altitude = Input.GetAxis("Mouse ScrollWheel");
            if (altitude != 0)
            {
                transform.position += Vector3.up * altitude * 10f;
            }
        }

        /// <summary>
        /// Установить цель для камеры
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Переключить режим полёта
        /// </summary>
        public void ToggleFlyMode()
        {
            isFlying = !isFlying;
            Debug.Log($"[WorldCamera] Fly mode: {isFlying}");
        }

        /// <summary>
        /// Установить режим полёта
        /// </summary>
        public void SetFlyMode(bool flying)
        {
            isFlying = flying;
        }

        /// <summary>
        /// Телепортация к пику по индексу
        /// </summary>
        public void TeleportToPeak(int peakIndex)
        {
            if (worldGenerator == null)
            {
                Debug.LogWarning("[WorldCamera] WorldGenerator не найден!");
                return;
            }

            var peaks = worldGenerator.GetAllPeaks();
            if (peakIndex >= 0 && peakIndex < peaks.Count)
            {
                var peak = peaks[peakIndex];
                teleportTarget = peak.position + Vector3.up * (peak.height * 0.5f);
                transform.position = teleportTarget;
                isTeleporting = false;
                
                Debug.Log($"[WorldCamera] Телепорт к пику {peakIndex}: {peak.name} (высота: {peak.height:F0}м)");
            }
        }

        /// <summary>
        /// Телепорт к следующему пику
        /// </summary>
        private int currentPeakIndex = -1;
        
        public void TeleportToNextPeak()
        {
            if (worldGenerator == null) return;
            
            var peaks = worldGenerator.GetAllPeaks();
            if (peaks.Count == 0) return;
            
            currentPeakIndex = (currentPeakIndex + 1) % peaks.Count;
            TeleportToPeak(currentPeakIndex);
        }

        /// <summary>
        /// Телепорт к предыдущему пику
        /// </summary>
        public void TeleportToPreviousPeak()
        {
            if (worldGenerator == null) return;
            
            var peaks = worldGenerator.GetAllPeaks();
            if (peaks.Count == 0) return;
            
            currentPeakIndex = (currentPeakIndex - 1 + peaks.Count) % peaks.Count;
            TeleportToPeak(currentPeakIndex);
        }

        /// <summary>
        /// Телепорт к случайному пику
        /// </summary>
        public void TeleportToRandomPeak()
        {
            if (worldGenerator == null) return;
            
            var peaks = worldGenerator.GetAllPeaks();
            if (peaks.Count == 0) return;
            
            currentPeakIndex = Random.Range(0, peaks.Count);
            TeleportToPeak(currentPeakIndex);
        }
    }
}
