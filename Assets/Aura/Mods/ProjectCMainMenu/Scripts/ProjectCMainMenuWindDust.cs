// ProjectC Main Menu — airborne wind dust (src/wind.js, THREE.Points -> ParticleSystem,
// because the original draws gl_PointSize/gl_PointCoord sprites).
using UnityEngine;

namespace ProjectCMainMenu
{
    public class ProjectCMainMenuWindDust : MonoBehaviour
    {
        [SerializeField] private int maxParticles = 280;
        [SerializeField] private float baseRate = 34f;

        private ParticleSystem _system;
        private ParticleSystemRenderer _renderer;
        private ParticleSystem.MainModule _main;
        private ParticleSystem.EmissionModule _emission;
        private Texture2D _dot;
        private Material _material;
        private float _alpha;
        private bool _built;

        public int Count { get; private set; }
        public float Amount { get; private set; }

        public void Initialize()
        {
            if (_built) return;
            _built = true;

            _system = gameObject.GetComponent<ParticleSystem>();
            if (_system == null) _system = gameObject.AddComponent<ParticleSystem>();

            _main = _system.main;
            _main.loop = true;
            _main.playOnAwake = true;
            _main.startLifetime = 9f;
            _main.startSpeed = 0f;
            _main.startSize = 0.006f;
            _main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            _main.maxParticles = Mathf.Max(16, maxParticles);
            _main.simulationSpace = ParticleSystemSimulationSpace.Local;
            _main.startColor = new Color(0.76f, 0.85f, 0.79f, 0.18f);

            _emission = _system.emission;
            _emission.enabled = true;
            _emission.rateOverTime = baseRate;

            var shape = _system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 14f, 0.4f);
            shape.position = new Vector3(0f, 0f, -1f);

            var velocity = _system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = _system.noise;
            noise.enabled = true;
            noise.strength = 0.035f;
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.12f;

            var sizeOverLife = _system.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1f));

            var colorOverLife = _system.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            _dot = CreateDot();
            _material = new Material(FindParticleShader()) { name = "PCMM_WindDust" };
            if (_dot != null) _material.mainTexture = _dot;

            _renderer = _system.GetComponent<ParticleSystemRenderer>();
            if (_renderer != null)
            {
                _renderer.sharedMaterial = _material;
                _renderer.renderMode = ParticleSystemRenderMode.Billboard;
                _renderer.sortingOrder = 46;
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
            }

            _system.Play();
        }

        private static Shader FindParticleShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return shader;
        }

        private static Texture2D CreateDot()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "PCMM_WindDot",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size - 0.5f;
                    var dy = (y + 0.5f) / size - 0.5f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    var a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>The atmosphere camera only sees its own layer, so the dust must live on it.</summary>
        public void ApplyLayer(int layer)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        public void Tick(float density, float gust, float exposure)
        {
            if (!_built) Initialize();
            if (_system == null) return;

            var k = Mathf.Clamp(density, 0f, 2f);
            var amount = Mathf.Clamp(0.045f + 0.12f * k + Mathf.Clamp(gust, 0f, 2f) * 0.42f * Mathf.Clamp(k, 0f, 1.6f), 0f, 0.85f);
            Amount = amount;
            Count = Mathf.RoundToInt(Mathf.Max(10f, maxParticles * Mathf.Clamp(0.14f + 0.48f * k, 0.1f, 1f)));

            _emission.rateOverTime = baseRate * Mathf.Clamp(0.35f + 0.55f * k, 0f, 2f);
            _main.maxParticles = Mathf.Max(16, Count);

            var tint = Mathf.Clamp(exposure, 0.15f, 1f);
            var scale = 0.45f + 0.55f * tint;
            _main.startColor = new Color(0.78f * scale, 0.86f * scale, 0.80f * scale, Mathf.Clamp(0.10f + amount * 0.55f, 0f, 0.9f));

            gameObject.SetActive(amount > 0.012f);
        }
    }
}
