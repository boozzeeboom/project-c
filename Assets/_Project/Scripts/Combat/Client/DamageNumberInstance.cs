// Project C: Real-Time Combat Engine — Damage Numbers
// DamageNumberInstance: компонент на world-space TMP GameObject.
// Анимирует всплытие + затухание, затем возвращает себя в пул.
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

        private Coroutine _animRoutine;
        private Action<DamageNumberInstance> _onComplete;

        // === Lifecycle ===

        private void Awake()
        {
            // Try to resolve if not serialized (programmatic prefab creation).
            if (_tmpText == null) _tmpText = GetComponentInChildren<TextMeshProUGUI>();
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        }

        /// <summary>
        /// Инициализировать и запустить анимацию.
        /// </summary>
        /// <param name="config">DamageNumberConfig с цветами/размерами.</param>
        /// <param name="worldPos">Мировая позиция (центр над целью с учётом offset + spread).</param>
        /// <param name="damage">Финальный урон.</param>
        /// <param name="isCrit">Критический удар?</param>
        /// <param name="damageType">Тип урона (для цвета).</param>
        /// <param name="duration">Длительность анимации (из CombatConfig).</param>
        /// <param name="onComplete">Callback при завершении (возврат в пул).</param>
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

            // Позиция
            transform.position = worldPos;

            // Текст
            string text = damage.ToString();
            if (isCrit && config.showCritExclamation) text += "!";
            _tmpText.text = text;

            // Цвет
            Color color = config.GetEffectiveColor(damageType, isCrit);
            color.a = 1f; // начало полностью непрозрачное
            _tmpText.color = color;

            // Размер
            _tmpText.fontSize = config.GetEffectiveFontSize(isCrit);

            // Всегда смотрим на камеру (billboard effect)
            _canvas.transform.rotation = Quaternion.identity;

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

                // Billboard: смотреть на камеру
                if (Camera.main != null)
                {
                    _canvas.transform.LookAt(
                        _canvas.transform.position + Camera.main.transform.forward,
                        Camera.main.transform.up);
                }

                yield return null;
            }

            // Завершение
            Complete();
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
            // Если выключили до завершения анимации — просто завершаем.
            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
                _animRoutine = null;
            }
        }
    }
}
