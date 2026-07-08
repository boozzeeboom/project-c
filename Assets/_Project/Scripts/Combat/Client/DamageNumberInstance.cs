// Project C: Real-Time Combat Engine — Damage Numbers
// DamageNumberInstance: компонент на world-space TMP GameObject.
// Анимирует всплытие + затухание, затем возвращает себя в пул.
// Использует существующий Billboard.cs для поворота к камере.
// Масштабирует canvas по расстоянию для унифицированного размера.
// Design: docs/Character/Skills/real-time-combat/110_DAMAGE_NUMBERS.md

using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace ProjectC.Combat.Client
{
    /// <summary>
    /// Один экземпляр всплывающей цифры урона. Живёт на world-space GameObject с TMP.
    /// Управляется DamageNumberService через object pool.
    /// </summary>
    public class DamageNumberInstance : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _tmpText;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _rectTransform;

        [Header("Distance Scaling")]
        [Tooltip("Эталонное расстояние (метры), на котором canvas имеет исходный размер.")]
        [SerializeField] private float _referenceDistance = 10f;
        [Tooltip("Базовый scale canvas (при _referenceDistance метров от камеры).")]
        [SerializeField] private Vector3 _baseScale = new Vector3(0.01f, 0.01f, 0.01f);

        private Coroutine _animRoutine;
        private Action<DamageNumberInstance> _onComplete;
        private ProjectC.UI.Billboard _billboard;
        private Vector3 _baseScaleActual;

        // === Lifecycle ===

        private void Awake()
        {
            // Try to resolve if not serialized (programmatic prefab creation).
            if (_tmpText == null) _tmpText = GetComponentInChildren<TextMeshProUGUI>();
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();

            // Billboard: используем существующий компонент из ProjectC.UI
            _billboard = GetComponent<ProjectC.UI.Billboard>();
            if (_billboard == null)
            {
                _billboard = gameObject.AddComponent<ProjectC.UI.Billboard>();
                _billboard.keepVertical = true;
            }

            _baseScaleActual = _baseScale;
        }

        /// <summary>
        /// Инициализировать и запустить анимацию.
        /// </summary>
        public void Spawn(
            Config.DamageNumberConfig config,
            Vector3 worldPos,
            int damage,
            bool isCrit,
            Combat.Core.DamageType damageType,
            float duration,
            Action<DamageNumberInstance> onComplete)
        {
            if (config == null)
            {
                Debug.LogWarning("[DamageNumberInstance] Spawn: config is null.");
                onComplete?.Invoke(this);
                return;
            }

            _onComplete = onComplete;
            transform.position = worldPos;

            // Текст
            string text = damage.ToString();
            if (isCrit && config.showCritExclamation) text += "!";
            _tmpText.text = text;

            // Цвет
            Color color = config.GetEffectiveColor(damageType, isCrit);
            color.a = 1f;
            _tmpText.color = color;

            // Размер шрифта
            _tmpText.fontSize = config.GetEffectiveFontSize(isCrit);

            // Сброс scale перед стартом
            transform.localScale = _baseScaleActual;

            gameObject.SetActive(true);

            // Запуск анимации
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimateRoutine(config, worldPos, duration));
        }

        private IEnumerator AnimateRoutine(Config.DamageNumberConfig config, Vector3 startPos, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Подъём вверх
                transform.position = startPos + Vector3.up * config.floatSpeed * elapsed;

                // Прозрачность по кривой
                Color c = _tmpText.color;
                c.a = config.fadeCurve.Evaluate(t);
                _tmpText.color = c;

                // Унифицированный размер: scale ∝ distance от камеры
                ApplyDistanceScale();

                yield return null;
            }

            Complete();
        }

        private void ApplyDistanceScale()
        {
            Transform cam = ProjectC.UI.Billboard.ActiveCamera;
            if (cam == null)
            {
                var mainCam = Camera.main;
                if (mainCam != null) cam = mainCam.transform;
            }
            if (cam == null) return;

            float dist = Vector3.Distance(cam.position, transform.position);
            if (dist < 0.1f) dist = 0.1f;

            float scaleMult = dist / _referenceDistance;
            transform.localScale = _baseScaleActual * scaleMult;
        }

        private void Complete()
        {
            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
                _animRoutine = null;
            }

            gameObject.SetActive(false);
            _onComplete?.Invoke(this);
            _onComplete = null;
        }

        private void OnDisable()
        {
            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
                _animRoutine = null;
            }
        }
    }
}
