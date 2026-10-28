// ProjectC Main Menu — airborne wind dust (src/wind.js, THREE.Points -> ParticleSystem,
// because the original draws gl_PointSize/gl_PointCoord sprites).
//
// The mod: WIND_MAX = 280 specks, ~62% of them drawn at density 1, each a soft additive
// dot of uPixel * (0.65 + 1.5 * size) px (uPixel = frameH / 300, size 0.35..1.4) with its
// own alpha 0.26..1 x uAmount, drifting slowly in its own direction across the WHOLE
// frame. The first port emitted from a 24 x 14 box one unit in front of the camera,
// where the visible frame is ~1.65 x 0.93 — so ~99% of the specks were off screen — and
// drew them with the default (opaque) URP particle material, so the few that showed were
// hard white squares. This version fills exactly the visible frame, sizes the specks from
// the mod's formula, and draws them with PCMM_Dust (procedural soft dot, additive).
using UnityEngine;

namespace ProjectCMainMenu
{
    public class ProjectCMainMenuWindDust : MonoBehaviour
    {
        [SerializeField] private int maxParticles = 280;
        [SerializeField] private float baseRate = 34f;

        private const string MaterialResource = "PCMM_WindDust";
        private const string DustShader = "ProjectC/Mods/MainMenu/Dust";
        private const float Lifetime = 9f;
        private const float Depth = 1f;           // the dust plane, in front of the camera

        private ParticleSystem _system;
        private ParticleSystemRenderer _renderer;
        private ParticleSystem.MainModule _main;
        private ParticleSystem.EmissionModule _emission;
        private ParticleSystem.ShapeModule _shape;
        private ParticleSystem.VelocityOverLifetimeModule _velocity;
        private Material _material;
        private bool _built;
        private float _viewW = 1.66f;
        private float _viewH = 0.93f;
        private float _pixel = 3.6f;

        public int Count { get; private set; }
        public float Amount { get; private set; }

        public void Initialize()
        {
            if (_built) return;
            _built = true;

            _system = gameObject.GetComponent<ParticleSystem>();
            if (_system == null) _system = gameObject.AddComponent<ParticleSystem>();
            _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _main = _system.main;
            _main.loop = true;
            _main.playOnAwake = true;
            _main.prewarm = true;                 // the frame is already full of dust at boot
            _main.startLifetime = Lifetime;
            _main.startSpeed = 0f;
            _main.startRotation = 0f;
            _main.maxParticles = Mathf.Max(16, maxParticles);
            _main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ApplySize();

            _emission = _system.emission;
            _emission.enabled = true;
            _emission.rateOverTime = baseRate;

            _shape = _system.shape;
            _shape.enabled = true;
            _shape.shapeType = ParticleSystemShapeType.Box;
            ApplyShape();

            // each speck keeps its own heading at its own speed (wind.js: t * 0.028 per
            // frame unit, t scaled by 0.22 + 0.7 * speed) ...
            _velocity = _system.velocityOverLifetime;
            _velocity.enabled = true;
            _velocity.space = ParticleSystemSimulationSpace.Local;
            ApplyVelocity();

            // ... and curls on a slow vortex plus fine turbulence
            var noise = _system.noise;
            noise.enabled = true;
            noise.strength = 0.028f;
            noise.frequency = 0.9f;
            noise.scrollSpeed = 0.18f;
            noise.damping = false;

            var sizeOverLife = _system.sizeOverLifetime;
            sizeOverLife.enabled = false;

            // the mod wraps the specks around the frame; a finite lifetime fades them in and
            // out instead of letting them pop
            var colorOverLife = _system.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            _material = LoadMaterial();

            _renderer = _system.GetComponent<ParticleSystemRenderer>();
            if (_renderer != null)
            {
                if (_material != null) _renderer.sharedMaterial = _material;
                _renderer.renderMode = ParticleSystemRenderMode.Billboard;
                _renderer.sortingOrder = 46;
                _renderer.minParticleSize = 0f;
                _renderer.maxParticleSize = 0.05f;
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
            }

            _system.Play();
        }

        /// <summary>
        /// PCMM_WindDust.mat lives in Resources so the shader ships in a player build; the
        /// shader lookup is only the fallback for an editor that has not imported it yet.
        /// </summary>
        private static Material LoadMaterial()
        {
            var shared = Resources.Load<Material>(MaterialResource);
            if (shared != null) return shared;

            var shader = Shader.Find(DustShader);
            return shader != null ? new Material(shader) { name = MaterialResource, hideFlags = HideFlags.DontSave } : null;
        }

        /// <summary>
        /// The visible frame at the dust plane (world units at Depth) and the frame height in
        /// pixels — the atmosphere calls this whenever the camera's frustum changes.
        /// </summary>
        public void SetView(float viewW, float viewH, float frameHeightPx)
        {
            _viewW = Mathf.Max(0.01f, viewW * Depth);
            _viewH = Mathf.Max(0.01f, viewH * Depth);
            _pixel = Mathf.Clamp(frameHeightPx / 300f, 1f, 5f);
            if (!_built) return;

            ApplyShape();
            ApplySize();
            ApplyVelocity();
        }

        private void ApplyShape()
        {
            // a little wider than the frame, so specks drift in from the edges
            _shape.scale = new Vector3(_viewW * 1.08f, _viewH * 1.08f, 0.002f);
            _shape.position = new Vector3(0f, 0f, Depth);
        }

        private void ApplySize()
        {
            // gl_PointSize = uPixel * (0.65 + 1.5 * size), size 0.35..1.4 -> pixels -> world
            var frameH = Mathf.Max(1f, _pixel * 300f);
            var toWorld = _viewH / frameH;
            var min = _pixel * (0.65f + 1.5f * 0.35f) * toWorld;
            var max = _pixel * (0.65f + 1.5f * 1.40f) * toWorld;
            _main.startSize = new ParticleSystem.MinMaxCurve(min, max);
        }

        private void ApplyVelocity()
        {
            // 0.028 frame units per t, t = (0.22 .. 0.92) * time
            var vx = 0.028f * 0.92f * _viewW;
            var vy = 0.028f * 0.92f * _viewH;
            _velocity.x = new ParticleSystem.MinMaxCurve(-vx, vx);
            _velocity.y = new ParticleSystem.MinMaxCurve(-vy, vy);
            _velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
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
            var g = Mathf.Clamp(gust, 0f, 2f);

            // wind.js: barely there when everything rests, thick while the veil is blown apart
            var amount = Mathf.Clamp(0.045f + 0.12f * k + g * 0.42f * Mathf.Clamp(k, 0f, 1.6f), 0f, 0.85f);
            Amount = amount;
            Count = Mathf.RoundToInt(Mathf.Max(10f, maxParticles * Mathf.Clamp(0.14f + 0.48f * k, 0.1f, 1f)));

            // a steady population of Count specks over their lifetime
            _emission.rateOverTime = Count / Lifetime;
            _main.maxParticles = Mathf.Max(16, Count);

            // the gust speeds the flight up (wind.js: t *= 1 + 0.9 * uGust)
            _velocity.speedModifier = 1f + 0.9f * g;

            // vAlpha = 0.26 .. 1 per speck, times uAmount; tint follows the day phase
            var tint = Mathf.Clamp(exposure, 0.15f, 1f);
            var scale = 0.45f + 0.55f * tint;
            var rgb = new Color(0.78f * scale, 0.86f * scale, 0.80f * scale);
            _main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(rgb.r, rgb.g, rgb.b, 0.26f * amount),
                new Color(rgb.r, rgb.g, rgb.b, amount));

            gameObject.SetActive(amount > 0.012f);
        }
    }
}
