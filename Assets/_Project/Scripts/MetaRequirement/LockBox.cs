// =====================================================================================
// LockBox.cs — анимированный блок с замком (Project C: The Clouds, MetaRequirement test)
// =====================================================================================
// Документация:
//   • docs/MetaRequirement/00_OVERVIEW.md
//   • docs/MetaRequirement/RECIPES.md (Recipe 1: 1 ключ)
//   • docs/dev/META_REQUIREMENT_IMPL_NOTES.md
//
// Назначение: тестовый не-корабельный MetaRequirement. Стоит на сцене рядом с
// Pickup-ключом своего цвета. Игрок подбирает ключ (E), потом E на блок → сервер
// авторизует через MetaRequirementRegistry → клиент получает OnAccessAllowed →
// проигрывается анимация scale-up + emissive flash. Без ключа — toast.
//
// MVP-граница (R2-META-REQ-001):
//   • Анимация клиентская (визуальный feedback). Логика авторизации — на сервере.
//   • _consumeOnUse НЕ используется (ключ остаётся в инвентаре).
//   • _openedState не сохраняется между сессиями (TODO persistence).
// =====================================================================================

using System.Collections;
using UnityEngine;
using ProjectC.MetaRequirement;

namespace ProjectC.MetaRequirement.Test
{
    /// <summary>
    /// Тестовый блок-«сундук» с замком. Включается анимацией (scale + emissive flash)
    /// при успешном MetaRequirement-доступе. Подписывается на client event, не на
    /// прямой вызов с сервера (визуал — клиентская фича).
    /// </summary>
    public class LockBox : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Базовый цвет блока (виден в инспекторе + используется для анимации).")]
        [SerializeField] private Color _baseColor = new Color(0.2f, 0.6f, 1f); // дефолт — голубой
        [Tooltip("Цвет emissive в покое.")]
        [SerializeField] private Color _baseEmission = new Color(0.05f, 0.15f, 0.3f);

        [Header("Анимация (при успешном открытии)")]
        [Tooltip("Длительность анимации в секундах.")]
        [SerializeField] private float _animDuration = 0.6f;
        [Tooltip("Множитель scale при открытии (1.0 = нет анимации, 1.2 = +20%).")]
        [SerializeField] private float _animScaleMultiplier = 1.2f;
        [Tooltip("Множитель emissive intensity в пике (1.0 = базовый, 3.0 = тройная яркость).")]
        [SerializeField] private float _animEmissionMultiplier = 3.0f;

        [Header("Частота повторной анимации")]
        [Tooltip("Минимальный интервал между повторными анимациями (защита от спама).")]
        [SerializeField] private float _reopenCooldown = 0.5f;

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private static readonly int _emissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");

        private Vector3 _baseScale;
        private float _lastOpenedTime = -10f;
        private Coroutine _animCoroutine;

        // ===========================================================
        // Lifecycle
        // ===========================================================

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            ApplyBaseAppearance();
        }

        private void OnEnable()
        {
            // Подписка на client event (авторизация уже прошла на сервере)
            if (MetaRequirementClientState.Instance != null)
            {
                MetaRequirementClientState.Instance.OnAccessAllowed += OnAccessAllowed;
            }
        }

        private void OnDisable()
        {
            if (MetaRequirementClientState.Instance != null)
            {
                MetaRequirementClientState.Instance.OnAccessAllowed -= OnAccessAllowed;
            }
            if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
        }

        private void Update()
        {
            // Lazy-subscribe (на случай если MetaRequirementClientState был создан ПОСЛЕ OnEnable)
            if (MetaRequirementClientState.Instance != null
                && !_subscribed)
            {
                MetaRequirementClientState.Instance.OnAccessAllowed += OnAccessAllowed;
                _subscribed = true;
            }
        }

        private bool _subscribed = false;

        // ===========================================================
        // Client event handler
        // ===========================================================

        private void OnAccessAllowed(ulong netId)
        {
            // Срабатываем ТОЛЬКО если event — про наш NetworkObject
            // (LockBox — НЕ NetworkBehaviour сам по себе; для проверки netId
            //  смотрим на sibling NetworkObject на этом GameObject)
            var no = GetComponent<Unity.Netcode.NetworkObject>();
            if (no == null) return;
            if (no.NetworkObjectId != netId) return;

            if (Time.unscaledTime - _lastOpenedTime < _reopenCooldown) return;
            _lastOpenedTime = Time.unscaledTime;

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateOpen());
        }

        // ===========================================================
        // Animations
        // ===========================================================

        private IEnumerator AnimateOpen()
        {
            // Phase 1: ramp-up (scale + emission)
            float t = 0f;
            Vector3 targetScale = _baseScale * _animScaleMultiplier;
            Color targetEmission = _baseEmission * _animEmissionMultiplier;
            while (t < _animDuration * 0.5f)
            {
                t += Time.deltaTime;
                float k = t / (_animDuration * 0.5f);
                transform.localScale = Vector3.Lerp(_baseScale, targetScale, k);
                SetEmission(Color.Lerp(_baseEmission, targetEmission, k));
                yield return null;
            }

            // Phase 2: ramp-down (back to base, but emission stays a bit brighter for a moment)
            t = 0f;
            while (t < _animDuration * 0.5f)
            {
                t += Time.deltaTime;
                float k = t / (_animDuration * 0.5f);
                transform.localScale = Vector3.Lerp(targetScale, _baseScale, k);
                SetEmission(Color.Lerp(targetEmission, _baseEmission * 1.5f, k));
                yield return null;
            }

            // Final: exactly base
            transform.localScale = _baseScale;
            ApplyBaseAppearance();
            _animCoroutine = null;
        }

        // ===========================================================
        // Material helpers
        // ===========================================================

        private void ApplyBaseAppearance()
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(_baseColorId, _baseColor);
            _mpb.SetColor(_emissionColorId, _baseEmission);
            _renderer.SetPropertyBlock(_mpb);
        }

        private void SetEmission(Color c)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionColorId, c);
            _renderer.SetPropertyBlock(_mpb);
        }

        // ===========================================================
        // Editor visualization
        // ===========================================================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // При выделении в редакторе — рисуем сферу радиуса взаимодействия
            Gizmos.color = _baseColor;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 1.5f);
        }

        private void OnValidate()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            // Обновим базовый цвет в редакторе сразу (визуальный feedback)
            if (_renderer != null && !Application.isPlaying)
            {
                ApplyBaseAppearance();
            }
        }
#endif
    }
}
