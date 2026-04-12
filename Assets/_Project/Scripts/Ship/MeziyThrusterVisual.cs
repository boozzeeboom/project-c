using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// MeziyThrusterVisual — визуальный эффект сопел при активации мезиевой тяги.
    /// Сессия 5: Meziy Thrust & Advanced Modules.
    /// 
    /// Опциональный компонент. Если не назначен — мезиевая тяга работает без визуала.
    /// Подключается к MeziyModuleActivator через события активации/деактивации.
    /// </summary>
    public class MeziyThrusterVisual : MonoBehaviour
    {
        [Header("Визуал")]
        [Tooltip("ParticleSystem сопла (огонь/энергия)")]
        public ParticleSystem thrustParticle;

        [Tooltip("Свечение при активации")]
        public Light glowLight;

        [Tooltip("Интенсивность частиц (0..1)")]
        [Range(0f, 1f)]
        public float particleIntensity = 1f;

        private bool _isActive = false;

        /// <summary>
        /// Включить частицы + свечение.
        /// Вызывается из ShipController при активации мезиевого модуля.
        /// </summary>
        public void Activate()
        {
            if (_isActive) return;
            _isActive = true;

            if (thrustParticle != null)
            {
                // Проверить что ParticleSystem настроен
                var main = thrustParticle.main;
                main.startColor = new Color(1f, 0.6f, 0.1f, particleIntensity);
                
                // Если emission module отключён — включим
                var emission = thrustParticle.emission;
                emission.enabled = true;
                
                if (!thrustParticle.isPlaying)
                {
                    thrustParticle.Play();
                    Debug.Log("[MeziyThrusterVisual] ParticleSystem started");
                }
            }
            else
            {
                Debug.LogWarning("[MeziyThrusterVisual] thrustParticle is null! Assign a ParticleSystem in Inspector.");
            }

            if (glowLight != null)
            {
                glowLight.enabled = true;
                glowLight.color = new Color(1f, 0.5f, 0.1f);
                glowLight.intensity = 2f;
                Debug.Log("[MeziyThrusterVisual] Glow light enabled");
            }
            else
            {
                Debug.LogWarning("[MeziyThrusterVisual] glowLight is null! Assign a Light in Inspector.");
            }
        }

        /// <summary>
        /// Выключить частицы + свечение.
        /// Вызывается из ShipController когда мезиевый эффект завершается.
        /// </summary>
        public void Deactivate()
        {
            if (!_isActive) return;
            _isActive = false;

            if (thrustParticle != null && thrustParticle.isPlaying)
            {
                thrustParticle.Stop();
            }

            if (glowLight != null)
            {
                glowLight.enabled = false;
            }
        }

        /// <summary>
        /// Установить интенсивность частиц (0..1).
        /// </summary>
        public void SetIntensity(float intensity)
        {
            particleIntensity = Mathf.Clamp01(intensity);

            if (thrustParticle != null)
            {
                var main = thrustParticle.main;
                main.startColor = new Color(1f, 0.6f, 0.1f, particleIntensity);
            }
        }
    }
}
