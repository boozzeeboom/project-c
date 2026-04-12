using UnityEngine;

namespace ProjectC.Ship
{
    /// <summary>
    /// MeziyThrusterVisual — визуальный эффект сопел при активации мезиевой тяги.
    /// Сессия 5: Meziy Thrust & Advanced Modules.
    ///
    /// Опциональный компонент. Если ParticleSystem/Light не назначены — они создадутся
    /// автоматически при первом вызове Activate().
    /// </summary>
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectC.Ship
{
    /// <summary>
    /// MeziyThrusterVisual — визуальный эффект сопел при активации мезиевой тяги.
    /// Сессия 5: Meziy Thrust & Advanced Modules.
    ///
    /// Опциональный компонент. Если ParticleSystem/Light не назначены — они создадутся
    /// автоматически при первом вызове Activate().
    /// </summary>
#if UNITY_EDITOR
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MeziyThrusterVisual))]
    public class MeziyThrusterVisualEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var visual = (MeziyThrusterVisual)target;
            EditorGUILayout.Space();

            if (GUILayout.Button("Auto-Create Thruster Particles", GUILayout.Height(30)))
            {
                visual.AutoCreateParticles();
            }
        }
    }
#endif

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
        /// Если ParticleSystem/Light не назначены — создадутся автоматически.
        /// </summary>
        public void Activate()
        {
            if (_isActive) return;
            _isActive = true;

            // Авто-создание если не назначены
            if (thrustParticle == null)
                AutoCreateParticles();

            if (thrustParticle != null)
            {
                var main = thrustParticle.main;
                main.startColor = new Color(1f, 0.6f, 0.1f, particleIntensity);
                
                var emission = thrustParticle.emission;
                emission.enabled = true;
                
                if (!thrustParticle.isPlaying)
                {
                    thrustParticle.Play();
                }
            }

            if (glowLight != null)
            {
                glowLight.enabled = true;
                glowLight.color = new Color(1f, 0.5f, 0.1f);
                glowLight.intensity = 2f;
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

        /// <summary>
        /// Автоматически создать дочерний объект с ParticleSystem и Light.
        /// Вызывается из Editor кнопки или при первом Activate().
        /// </summary>
        public void AutoCreateParticles()
        {
            // Создать дочерний объект
            var go = new GameObject("MeziyThruster");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.back * 1.5f; // Позиция "сопла" (сзади корабля)
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Направлен назад

            // Создать ParticleSystem
            thrustParticle = go.AddComponent<ParticleSystem>();

            var main = thrustParticle.main;
            main.loop = true;
            main.prewarm = false;
            main.startDelay = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startColor = new Color(1f, 0.6f, 0.1f, particleIntensity);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = thrustParticle.emission;
            emission.rateOverTime = 80f;
            emission.burstCount = 0;

            var shape = thrustParticle.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.3f;
            shape.radiusThickness = 1f;

            var renderer = thrustParticle.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetDefaultParticleMaterial();

            // Создать Light для свечения
            var lightGo = new GameObject("MeziyGlow");
            lightGo.transform.SetParent(go.transform);
            lightGo.transform.localPosition = Vector3.zero;
            glowLight = lightGo.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.5f, 0.1f);
            glowLight.intensity = 0f; // Выключен по умолчанию
            glowLight.range = 5f;

            Debug.Log("[MeziyThrusterVisual] Auto-created ParticleSystem + Light on 'MeziyThruster' child object.");
        }

        /// <summary>
        /// Получить дефолтный материал для частиц (Additive).
        /// </summary>
        private Material GetDefaultParticleMaterial()
        {
            // Попробовать стандартный Default-Particle
            var mat = Resources.GetBuiltinResource<Material>("Default-Particle.mat");
            if (mat != null) return mat;

            // Фоллбэк: создать свой
            mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.SetFloat("_Mode", 2); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            return mat;
        }
    }
}
