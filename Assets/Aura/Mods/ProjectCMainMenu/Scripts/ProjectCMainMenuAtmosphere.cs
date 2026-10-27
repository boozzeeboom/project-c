// ProjectC Main Menu — the whole atmosphere layer, ported from the mod's
// src/atmosphere.js: a camera-parented stack of full-screen quads
//   sky -> stars -> 4 cloud-veil layers -> mesmium gas -> cables/ferry -> dust
// driven by the menu's own gestures (Blow / SetFocus) and the day-night clock.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectCMainMenu
{
    public class ProjectCMainMenuAtmosphere : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera atmosphereCamera;
        [SerializeField] private Material skyMaterial;
        [SerializeField] private Material gasMaterial;
        [SerializeField] private Material starsMaterial;
        [SerializeField] private Material veilMaterial;
        [SerializeField] private ProjectCMainMenuRopeRig ropeRig;
        [SerializeField] private ProjectCMainMenuWindDust windDust;
        [SerializeField] private string atmosphereLayerName = "PCMM_Atmosphere";

        [Header("Atmosphere tunables (mod panel defaults)")]
        [SerializeField, Range(0f, 2f)] private float cloudDensity = 1f;
        [SerializeField, Range(0f, 2f)] private float gustStrength = 1f;
        [SerializeField, Range(0.05f, 1.2f)] private float gasBase = 0.52f;
        [SerializeField, Range(0f, 1f)] private float veilDarkness = 1f;
        [SerializeField, Range(0f, 3f)] private float driftSpeed = 1f;
        [SerializeField, Range(0f, 2f)] private float ropeSway = 1f;
        [SerializeField, Range(0f, 3f)] private float cartSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float cartDepth = 0.84f;
        [SerializeField, Range(0f, 2f)] private float starStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float starTwinkle = 1f;
        [SerializeField, Range(0.2f, 3f)] private float daySpeed = 1f;
        [SerializeField, Range(0f, 2f)] private float gasSensitivity = 1f;
        [SerializeField, Range(0f, 2f)] private float windDensity = 1f;
        [SerializeField, Range(0.05f, 0.7f)] private float exitHeight = 0.22f;
        [SerializeField] private float dayCycleSeconds = 190f;
        [SerializeField] private int cloudTextureSize = 192;

        private struct VeilLayer
        {
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;
            public float BaseOpacity;
            public float PlaneScale;
            public float Parallax;
            public float Seed;
        }

        private readonly VeilLayer[] _veils = new VeilLayer[4];
        private static readonly float[] VeilPlaneScale = { 1.16f, 1.28f, 1.41f, 1.55f };
        private static readonly float[] VeilOpacityPreset = { 0.50f, 0.60f, 0.68f, 0.76f };
        private static readonly float[] VeilRepeat = { 1.70f, 1.92f, 2.15f, 2.40f };
        private static readonly int[] VeilSeed = { 1301, 4409, 7717, 2741 };
        private static readonly float[] VeilCoverage = { 0.46f, 0.54f, 0.61f, 0.68f };
        private static readonly float[] VeilSoftness = { 0.80f, 0.68f, 0.58f, 0.48f };
        private static readonly int[] VeilPuffs = { 10, 12, 12, 13 };
        private static readonly Vector2[] VeilBand = { new Vector2(0.50f, 0.58f), new Vector2(0.44f, 0.54f), new Vector2(0.38f, 0.50f), new Vector2(0.36f, 0.46f) };

        private MeshRenderer _sky;
        private MeshRenderer _stars;
        private MeshRenderer _gas;
        private Material _skyMat;
        private Material _starMat;
        private Material _gasMat;

        private float _skyScale = 1f;
        private float _starScale = 1f;

        private Mesh _quad;
        private int _layer;
        private bool _built;

        // Index of PCMM_Renderer inside ProjectC_URP's renderer list (Assets/_Project/Settings).
        private const int PcmmRendererIndex = 1;

        private float _time;
        private float _qW = 1f;
        private float _qH = 1f;
        private float _viewAspect = 1.777f;
        private float _lastQW = -1f;
        private float _lastQH = -1f;

        private ProjectCMainMenuDayNight _dayNight;
        private ProjectCMainMenuDaySample _sample;

        // gust envelope (clouds.js createGustSpring)
        private bool _gustActive;
        private float _gustT;
        private float _gustDuration = 0.7f;
        private float _gustAmp = 1f;
        private float _gustValue;
        private Vector2 _gustDir = new Vector2(1f, -0.2f);
        private Vector2 _gustDirTarget = new Vector2(1f, -0.2f);
        private Vector2 _gustOrigin = new Vector2(0.5f, 0.55f);
        private Vector2 _gustOriginTarget = new Vector2(0.5f, 0.55f);

        private float _focusValue;
        private float _focusTarget;
        private float _lift = 0.5f;
        private float _pointerLift = 0.5f;

        public float GustValue => _gustValue;
        public float FocusValue => _focusValue;
        public float PointerLift { get => _pointerLift; set => _pointerLift = Mathf.Clamp01(value); }
        public float GasTop { get; private set; }
        public float VeilOpacity { get; private set; }
        public string DayLabel => _sample.Label;
        public Camera Camera => atmosphereCamera;

        public void SetCloudDensity(float value) => cloudDensity = Mathf.Clamp(value, 0f, 2f);
        public void SetVeilDarkness(float value) => veilDarkness = Mathf.Clamp01(value);
        public void SetStarStrength(float value) => starStrength = Mathf.Clamp(value, 0f, 2f);
        public void SetRopeSway(float value) => ropeSway = Mathf.Clamp(value, 0f, 2f);

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            if (_built) return;
            _built = true;

            if (atmosphereCamera == null)
            {
                var go = new GameObject("AtmosphereCamera");
                go.transform.SetParent(transform, false);
                atmosphereCamera = go.AddComponent<Camera>();
            }

            _layer = LayerMask.NameToLayer(atmosphereLayerName);
            if (_layer < 0)
            {
                Debug.LogWarning($"[PCMM] layer '{atmosphereLayerName}' not found, falling back to layer 0");
                _layer = 0;
            }

            atmosphereCamera.clearFlags = CameraClearFlags.SolidColor;
            atmosphereCamera.backgroundColor = ProjectCMainMenuPalette.GasDeep;
            atmosphereCamera.orthographic = false;
            atmosphereCamera.fieldOfView = 50f;
            atmosphereCamera.depth = 100f;
            atmosphereCamera.cullingMask = 1 << _layer;
            atmosphereCamera.allowHDR = true;
            atmosphereCamera.allowMSAA = false;
            atmosphereCamera.nearClipPlane = 0.01f;
            atmosphereCamera.farClipPlane = 10f;

            // The menu renders on its own renderer. ProjectC_URP_Renderer carries the project's
            // volumetric clouds, edge detection and far-field focus passes, and they run on every
            // camera except Preview (VolumetricCloudsRenderFeature.cs:246, DistantFocusRenderFeature.cs:78).
            // On this camera they painted over the Sky quad - the only quad in the Background queue -
            // while the Transparent layers still survived, so the backdrop looked half applied.
            // PCMM_Renderer is a copy of that renderer with no features; it sits at index 1.
            var cameraData = atmosphereCamera.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(PcmmRendererIndex);
            cameraData.renderPostProcessing = false;   // the menu palette must not follow the world Volume

            _quad = BuildQuadMesh();

            // Unity's camera looks down +Z (three.js, where this stack was ported from,
            // looks down -Z). Every quad therefore sits at a POSITIVE local z; with the old
            // negative values the whole atmosphere was behind the camera and nothing but
            // the clear colour was ever visible.
            _skyScale = 1.012f;
            _starScale = 1.006f;
            _sky = MakeQuad("Sky", skyMaterial, -100, 1.000f);
            _stars = MakeQuad("Stars", starsMaterial, -90, 0.9985f);
            for (var i = 0; i < _veils.Length; i++)
            {
                var renderer = MakeQuad("Veil_" + i, veilMaterial, 20 + i, 0.998f);
                _veils[i] = new VeilLayer
                {
                    Renderer = renderer,
                    Block = new MaterialPropertyBlock(),
                    BaseOpacity = VeilOpacityPreset[i],
                    PlaneScale = VeilPlaneScale[i],
                    Parallax = 0.28f + i * 0.22f,
                    Seed = (VeilSeed[i] % 97) / 97f,
                };

                var texture = ProjectCMainMenuCloudTexture.Create(
                    cloudTextureSize, VeilSeed[i], VeilCoverage[i], VeilSoftness[i], VeilPuffs[i], "PCMM_Cloud_" + VeilSeed[i]);
                var block = _veils[i].Block;
                renderer.GetPropertyBlock(block);
                block.SetTexture("_Map", texture);
                block.SetFloat("_Parallax", _veils[i].Parallax);
                block.SetFloat("_Seed", _veils[i].Seed);
                block.SetFloat("_Opacity", _veils[i].BaseOpacity);
                block.SetVector("_PlaneScale", new Vector4(VeilPlaneScale[i], VeilPlaneScale[i], 0f, 0f));
                block.SetVector("_Repeat", new Vector4(VeilRepeat[i], VeilRepeat[i], 0f, 0f));
                block.SetVector("_Band", new Vector4(VeilBand[i].x, VeilBand[i].y, 0f, 0f));
                renderer.SetPropertyBlock(block);
            }

            _gas = MakeQuad("Gas", gasMaterial, 30, 0.999f);

            _skyMat = _sky.material;
            _starMat = _stars.material;
            _gasMat = _gas.material;

            if (ropeRig == null) ropeRig = GetComponentInChildren<ProjectCMainMenuRopeRig>(true);
            if (ropeRig != null)
            {
                ropeRig.Initialize();
                ropeRig.transform.SetParent(atmosphereCamera.transform, false);
                ropeRig.ApplyLayer(_layer);
            }

            if (windDust == null) windDust = GetComponentInChildren<ProjectCMainMenuWindDust>(true);
            if (windDust != null)
            {
                windDust.Initialize();
                windDust.transform.SetParent(atmosphereCamera.transform, false);
                windDust.transform.localPosition = new Vector3(0f, 0f, 0f);
                windDust.ApplyLayer(_layer);
            }

            _dayNight = new ProjectCMainMenuDayNight(0.08f, dayCycleSeconds);
            _sample = _dayNight.Sample;

            Measure(true);
            Debug.Log($"[PCMM] atmosphere built (layer={_layer}, veil layers={_veils.Length}, ropes={(ropeRig != null)}, dust={(windDust != null)})");
        }

        private Mesh BuildQuadMesh()
        {
            var mesh = new Mesh { name = "PCMM_Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private MeshRenderer MakeQuad(string name, Material material, int order, float z)
        {
            var go = new GameObject(name);
            go.layer = _layer;
            go.transform.SetParent(atmosphereCamera.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = order;
            renderer.rendererPriority = order;
            renderer.transform.localScale = new Vector3(_qW, _qH, 1f);
            return renderer;
        }

        private void Measure(bool force)
        {
            if (atmosphereCamera == null) return;
            var fov = atmosphereCamera.fieldOfView * Mathf.Deg2Rad;
            var qh = 2f * Mathf.Tan(fov * 0.5f);
            var aspect = atmosphereCamera.aspect > 0.01f ? atmosphereCamera.aspect : 1.777f;
            var qw = qh * aspect;
            _viewAspect = qw / Mathf.Max(0.0001f, qh);

            if (!force && Mathf.Abs(qw - _lastQW) < 1e-4f && Mathf.Abs(qh - _lastQH) < 1e-4f) return;

            _lastQW = qw;
            _lastQH = qh;
            _qW = qw;
            _qH = qh;

            Layout();
        }

        private void Layout()
        {
            if (_sky != null) _sky.transform.localScale = new Vector3(_qW * _skyScale, _qH * _skyScale, 1f);
            if (_stars != null) _stars.transform.localScale = new Vector3(_qW * _starScale, _qH * _starScale, 1f);
            if (_gas != null) _gas.transform.localScale = new Vector3(_qW, _qH, 1f);

            for (var i = 0; i < _veils.Length; i++)
            {
                var renderer = _veils[i].Renderer;
                if (renderer == null) continue;
                var s = _veils[i].PlaneScale;
                renderer.transform.localScale = new Vector3(_qW * s, _qH * s, 1f);
            }

            if (ropeRig != null) ropeRig.Resize(_qW, _qH);
        }

        /// <summary>One scripted gust: starts now and is back at zero after durationMs.</summary>
        public void Blow(Vector2 direction, Vector2 origin01, float strength, float durationMs)
        {
            if (direction.sqrMagnitude < 1e-6f) direction = new Vector2(1f, -0.2f);
            _gustDirTarget = direction.normalized;
            _gustOriginTarget = new Vector2(
                Mathf.Clamp(origin01.x, -0.5f, 1.5f),
                Mathf.Clamp(origin01.y, -0.5f, 1.5f));
            _gustAmp = Mathf.Clamp(strength, 0f, 2.5f) * Mathf.Clamp(gustStrength, 0f, 3f);
            _gustDuration = Mathf.Clamp(durationMs / 1000f, 0.14f, 4f);
            _gustT = 0f;
            _gustValue = 0f;
            _gustActive = true;
            Debug.Log($"[PCMM] gust dir=({_gustDirTarget.x:F2},{_gustDirTarget.y:F2}) origin=({_gustOriginTarget.x:F2},{_gustOriginTarget.y:F2}) amp={_gustAmp:F2} dur={_gustDuration:F2}s");
        }

        /// <summary>0 = menu, 1 = a window is open (clouds part to the sides).</summary>
        public void SetFocus(float value)
        {
            _focusTarget = Mathf.Clamp01(value);
        }

        public void PulseDayNight(float seconds)
        {
            _dayNight?.Boost(seconds <= 0f ? 1.7f : seconds);
        }

        public void SetDayPhase(float t)
        {
            _dayNight?.SetPhase(t);
        }

        public void SetVisible(bool visible)
        {
            if (atmosphereCamera != null && atmosphereCamera.enabled != visible)
            {
                atmosphereCamera.enabled = visible;
                Debug.Log($"[PCMM] atmosphere camera {(visible ? "shown" : "hidden")}");
            }
        }

        // ------------------------------------------------------- day cycle pulse

        private float _dayBoost;
        private float _dayBoostRate;

        /// <summary>Baseline day/night speed — the inspector's daySpeed slider.</summary>
        public float DaySpeed
        {
            get => daySpeed;
            set => daySpeed = Mathf.Clamp(value, 0.2f, 3f);
        }

        /// <summary>Pulse currently added on top of <see cref="DaySpeed"/>; 0 when idle.</summary>
        public float DayBoost => _dayBoost;

        /// <summary>Speed the cycle is actually running at this frame.</summary>
        public float CurrentDaySpeed => Mathf.Clamp(daySpeed + _dayBoost, 0.2f, 8f);

        /// <summary>
        /// Kicks the day/night cycle for a moment: the sky turns visibly faster while
        /// the gesture lasts, then settles back to <see cref="DaySpeed"/>. The boost
        /// decays linearly over <paramref name="seconds"/>, so a second gesture mid-decay
        /// simply raises the peak again instead of restarting a timer.
        /// </summary>
        public void PulseDaySpeed(float boost, float seconds)
        {
            if (boost <= 0f) return;

            _dayBoost = Mathf.Max(_dayBoost, boost);
            _dayBoostRate = _dayBoost / Mathf.Max(0.05f, seconds);
        }

        private void Update()
        {
            if (!_built) Build();

            var dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.05f);
            if (dt <= 0f) return;

            _time += dt;

            UpdateGust(dt);
            _focusValue = ProjectCMainMenuPalette.Damp(_focusValue, _focusTarget, 3.4f, dt);

            var liftRaw = Mathf.Clamp01(_pointerLift);
            var sens = Mathf.Clamp(gasSensitivity, 0f, 2f);
            var liftTarget = Mathf.Clamp01(0.5f + (liftRaw - 0.5f) * sens);
            _lift = ProjectCMainMenuPalette.Damp(_lift, liftTarget, 3.2f, dt);
            var dark = Mathf.Pow(Mathf.Clamp01((0.55f - _lift) / 0.55f), 1.3f);

            _dayBoost = Mathf.MoveTowards(_dayBoost, 0f, _dayBoostRate * dt);
            _dayNight.CycleSeconds = Mathf.Clamp(dayCycleSeconds / Mathf.Max(0.2f, CurrentDaySpeed), 24f, 1200f);
            _dayNight.Update(dt);
            _sample = _dayNight.Sample;

            Measure(false);

            // ---- sky ------------------------------------------------------------
            if (_skyMat != null)
            {
                _skyMat.SetColor("_Top", _sample.SkyTop);
                _skyMat.SetColor("_Mid", _sample.SkyMid);
                _skyMat.SetColor("_Low", _sample.SkyLow);
                _skyMat.SetFloat("_Dim", 0.9f * dark);
            }

            // ---- stars ----------------------------------------------------------
            if (_starMat != null)
            {
                var twinkle = Mathf.Clamp(starTwinkle, 0f, 2f) * (0.35f + 0.65f * dark);
                var strength = Mathf.Clamp(starStrength, 0f, 2f) * Mathf.Clamp(0.75f + 0.55f * _sample.Stars + 0.45f * dark, 0f, 1.9f);
                _starMat.SetFloat("_Twinkle", twinkle);
                _starMat.SetFloat("_Strength", strength);
                _starMat.SetColor("_Color", ProjectCMainMenuPalette.Ink);
            }

            // ---- mesmium --------------------------------------------------------
            var exitY = Mathf.Clamp(exitHeight, 0.06f, 0.7f);
            var topLift = Mathf.Clamp(exitY + 0.07f, 0.14f, 0.9f);
            var topLow = Mathf.Clamp(exitY * 0.3f, 0.08f, 0.4f);
            var baseMul = Mathf.Clamp(gasBase / 0.52f, 0.15f, 2.2f);
            GasTop = Mathf.Clamp(Mathf.Lerp(topLow, topLift, _lift) * baseMul, 0.03f, 1.15f);

            if (_gasMat != null)
            {
                _gasMat.SetColor("_Deep", _sample.GasDeep);
                _gasMat.SetColor("_Mid", _sample.GasMid);
                _gasMat.SetColor("_Bright", _sample.GasBright);
                _gasMat.SetColor("_Accent", _sample.GasAccent);
                _gasMat.SetFloat("_Height", GasTop);
                _gasMat.SetFloat("_Aspect", _viewAspect);
                _gasMat.SetFloat("_Focus", _focusValue);
            }

            // ---- cloud veil -----------------------------------------------------
            var density = Mathf.Clamp(cloudDensity, 0f, 2f);
            var darkness = Mathf.Clamp01(veilDarkness);
            var bandShift = (_lift - 0.5f) * 0.18f;
            var peak = 0f;

            var darkCol = Color.Lerp(ProjectCMainMenuPalette.VeilMid, ProjectCMainMenuPalette.VeilDark, darkness);
            var softCol = Color.Lerp(ProjectCMainMenuPalette.VeilEdge, ProjectCMainMenuPalette.VeilHi, darkness);
            var edgeCol = Color.Lerp(ProjectCMainMenuPalette.VeilEdge, ProjectCMainMenuPalette.VeilHi, darkness * 0.5f);

            for (var i = 0; i < _veils.Length; i++)
            {
                var veil = _veils[i];
                if (veil.Renderer == null) continue;

                veil.Renderer.GetPropertyBlock(veil.Block);
                veil.Block.SetFloat("_Opacity", veil.BaseOpacity * Mathf.Clamp(density * (0.92f + 0.3f * _lift), 0f, 2f));
                veil.Block.SetFloat("_Gust", Mathf.Clamp(_gustValue, 0f, 2.5f));
                veil.Block.SetFloat("_Focus", _focusValue);
                veil.Block.SetFloat("_BandShift", Mathf.Clamp(bandShift, -0.3f, 0.3f));
                veil.Block.SetVector("_GustDir", new Vector4(_gustDir.x, _gustDir.y, 0f, 0f));
                veil.Block.SetVector("_GustOrigin", new Vector4(_gustOrigin.x, _gustOrigin.y, 0f, 0f));
                veil.Block.SetColor("_Dark", Multiply(darkCol, _sample.Veil));
                veil.Block.SetColor("_Soft", Multiply(softCol, _sample.Veil));
                veil.Block.SetColor("_Edge", Multiply(edgeCol, _sample.Veil));
                veil.Block.SetColor("_Tint", _sample.Veil);
                veil.Renderer.SetPropertyBlock(veil.Block);

                peak = Mathf.Max(peak, veil.BaseOpacity * density);
            }

            VeilOpacity = Mathf.Clamp01(peak * (1f - 0.5f * _gustValue) * (1f - 0.45f * _focusValue));

            // ---- cables, ferry, dust -------------------------------------------
            if (ropeRig != null)
            {
                ropeRig.Tick(dt, Mathf.Clamp(ropeSway, 0f, 2.5f), Mathf.Clamp(cartSpeed, 0f, 4f),
                    Mathf.Clamp(cartDepth, 0f, 1f), 0.75f + 0.35f * density);
            }

            if (windDust != null)
            {
                windDust.Tick(Mathf.Clamp(windDensity, 0f, 2f), _gustValue, Mathf.Clamp(_sample.Exposure, 0.15f, 1f));
            }
        }

        private static Color Multiply(Color a, Color b)
        {
            return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a);
        }

        private void UpdateGust(float dt)
        {
            if (_gustActive)
            {
                _gustT += dt;
                var u = Mathf.Clamp01(_gustT / Mathf.Max(0.0001f, _gustDuration));
                _gustValue = Mathf.Clamp(_gustAmp * Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.6f)), 0f, 2.5f);
                if (u >= 1f)
                {
                    _gustActive = false;
                    _gustValue = 0f;
                }
            }
            else
            {
                _gustValue = Mathf.MoveTowards(_gustValue, 0f, dt * 2.5f);
            }

            var l = 1f - Mathf.Exp(-14f * dt);
            _gustDir = Vector2.Lerp(_gustDir, _gustDirTarget, l);
            _gustOrigin = Vector2.Lerp(_gustOrigin, _gustOriginTarget, l);
        }
    }
}
