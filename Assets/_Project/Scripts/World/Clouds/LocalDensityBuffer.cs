// LocalDensityBuffer.cs — Phase 2.1
// Mutable 3D density field around player: wind advection + Gaussian splats.
// Toroidal window follows player (snowflow pattern), no data copy.
// CPU mirror for SampleDensity (used by MeziyHarvestProbe, Phase 2.5).

using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Core;

namespace ProjectC.World.Clouds
{
    /// <summary>
    /// Локальный 3D-буфер плотности облаков вокруг игрока.
    /// Адвекция ветром, релаксация, сплаты (врезка/добавка).
    /// Тороидальное окно: центр следует за игроком, адресация frac().
    /// </summary>
    public class LocalDensityBuffer : MonoBehaviour
    {
        public static LocalDensityBuffer Instance { get; private set; }

        [Header("Buffer")]
        [Range(48, 128)] public int Resolution = 96;
        [Range(10f, 50f)] public float TexelSize = 20f;

        [Header("Advection")]
        [Range(0f, 5f)] public float AdvectionStrength = 0.5f;
        [Range(0f, 1f)] public float RelaxationRate = 0.05f;

        [Header("Splat")]
        public float MaxSplatRadius = 150f;

        [Header("Target")]
        [Tooltip("Transform to follow (player/camera). If null, uses Camera.main.")]
        public Transform FollowTarget;

        [Header("Debug")]
        [SerializeField] private bool _verboseLogging = true;

        [Header("Compute")]
        [SerializeField] private ComputeShader _compute;

        // ── RenderTextures (ping-pong) ──
        private RenderTexture _densityA;
        private RenderTexture _densityB;
        private bool _pingPong;

        // ── Kernels ──
        private int _kernelAdvect;
        private int _kernelSplat;

        // ── Splat buffer ──
        private ComputeBuffer _splatBuffer;
        private SplatData[] _splatQueue = new SplatData[64];
        private int _splatQueueCount;

        // ── CPU mirror (Phase 2.5) ──
        private float[] _cpuDensity;

        // ── Debug readback ──
        private float _lastReadbackTime;
        private const float ReadbackInterval = 2f;

        // ── Shader property IDs ──
        private static readonly int DensityPrevId    = Shader.PropertyToID("_DensityPrev");
        private static readonly int DensityNextId    = Shader.PropertyToID("_DensityNext");
        private static readonly int CenterId         = Shader.PropertyToID("_Center");
        private static readonly int TexelSizeId      = Shader.PropertyToID("_TexelSize");
        private static readonly int ResolutionId     = Shader.PropertyToID("_Resolution");
        private static readonly int WindDirectionId  = Shader.PropertyToID("_WindDirection");
        private static readonly int AdvectionStrengthId = Shader.PropertyToID("_AdvectionStrength");
        private static readonly int RelaxationRateId = Shader.PropertyToID("_RelaxationRate");
        private static readonly int DeltaTimeId      = Shader.PropertyToID("_DeltaTime");
        private static readonly int SplatsId         = Shader.PropertyToID("_Splats");
        private static readonly int SplatCountId     = Shader.PropertyToID("_SplatCount");

        // ── 3D texture name for RT allocation ──
        private RenderTexture _activeReadRT;
        public Vector3 Center { get; private set; }

        /// <summary>
        /// Struct matching compute shader SplatData (16 bytes).
        /// </summary>
        private struct SplatData
        {
            public Vector3 center;
            public float radius;
            public float amount;
        }

        // ═══════════════════════════════════════════
        // Unity Lifecycle
        // ═══════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_compute != null)
            {
                _kernelAdvect = _compute.FindKernel("AdvectAndRelax");
                _kernelSplat  = _compute.FindKernel("ApplySplats");
                if (_kernelAdvect < 0 || _kernelSplat < 0)
                {
                    Debug.LogError($"[LocalDensityBuffer] Kernel not found. Advect={_kernelAdvect}, Splat={_kernelSplat}. Shader may have compilation errors.");
                    _compute = null;
                }
            }
            else
            {
                Debug.LogError("[LocalDensityBuffer] ComputeShader reference is null. Assign LocalDensity.compute in inspector.");
            }

            CreateTextures();
            _cpuDensity = new float[Resolution * Resolution * Resolution];

            if (_verboseLogging)
                Debug.Log($"[LocalDensityBuffer] Awake OK. Compute={_compute != null} Kernels: Adv={_kernelAdvect} Splat={_kernelSplat} RT={_densityA != null} Res={Resolution}");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (_densityA != null) { _densityA.Release(); _densityA = null; }
            if (_densityB != null) { _densityB.Release(); _densityB = null; }
            if (_splatBuffer != null) { _splatBuffer.Release(); _splatBuffer = null; }
        }

        private void Update()
        {
            if (_compute == null || _densityA == null || _kernelAdvect < 0) return;

            // Update toroidal center
            if (FollowTarget != null)
                Center = FollowTarget.position;
            else if (Camera.main != null)
                Center = Camera.main.transform.position;

            // Read wind
            Vector3 windDir = Vector3.right;
            if (WindManager.Instance != null)
                windDir = WindManager.Instance.CurrentWindDirection.normalized;

            float dt = Mathf.Min(Time.deltaTime, 0.1f); // cap to avoid spikes

            // ── Dispatch AdvectAndRelax ──
            var prev = _pingPong ? _densityB : _densityA;
            var next = _pingPong ? _densityA : _densityB;

            _compute.SetTexture(_kernelAdvect, DensityPrevId, prev);
            _compute.SetTexture(_kernelAdvect, DensityNextId, next);
            SetCommonParams(_kernelAdvect, windDir, dt);
            int groups = Mathf.CeilToInt(Resolution / 8f);
            _compute.Dispatch(_kernelAdvect, groups, groups, groups);

            // ── Dispatch ApplySplats ──
            if (_splatQueueCount > 0)
            {
                SyncSplatBuffer();
                _compute.SetTexture(_kernelSplat, DensityNextId, next);
                SetCommonParams(_kernelSplat, windDir, dt);
                _compute.SetBuffer(_kernelSplat, SplatsId, _splatBuffer);
                _compute.SetInt(SplatCountId, _splatQueueCount);
                _compute.Dispatch(_kernelSplat, groups, groups, groups);
                _splatQueueCount = 0;
            }

            // ── Ping-pong swap ──
            _pingPong = !_pingPong;
            _activeReadRT = _pingPong ? _densityB : _densityA;

            // ── CPU mirror relaxation ──
            RelaxCpuMirror(dt);
            SyncCpuFromSplats();

            // ── Debug readback: check CPU mirror center value ──
            _lastReadbackTime += dt;
            if (_verboseLogging && _lastReadbackTime > ReadbackInterval)
            {
                _lastReadbackTime = 0f;
                int centerIdx = (Resolution / 2) + (Resolution / 2) * Resolution + (Resolution / 2) * Resolution * Resolution;
                float centerVal = _cpuDensity != null && centerIdx < _cpuDensity.Length ? _cpuDensity[centerIdx] : -1f;
                float maxVal = 0f;
                if (_cpuDensity != null)
                    for (int i = 0; i < _cpuDensity.Length; i++)
                        if (_cpuDensity[i] > maxVal) maxVal = _cpuDensity[i];
                Debug.Log($"[LocalDensityBuffer] CPU mirror: center={centerVal:F4} max={maxVal:F4} centerWorld={Center} splatQueue={_splatQueueCount}");
            }

            // Debug: test splat at camera position (key T)
            if (_verboseLogging && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame && Camera.main != null)
            {
                Vector3 camPos = Camera.main.transform.position;
                SplatDensity(camPos, 100f, 0.5f);
                Debug.Log($"[LocalDensityBuffer] DEBUG SPLAT at camera {camPos} r=100 amount=0.5 totalQueue={_splatQueueCount}");
            }
        }

        // ═══════════════════════════════════════════
        // Texture Creation
        // ═══════════════════════════════════════════

        private void CreateTextures()
        {
            if (_densityA != null) _densityA.Release();
            if (_densityB != null) _densityB.Release();

            var desc = new RenderTextureDescriptor(Resolution, Resolution, RenderTextureFormat.RFloat, 0)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = Resolution,
                enableRandomWrite = true,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };

            _densityA = new RenderTexture(desc) { name = "LocalDensity_A", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            _densityB = new RenderTexture(desc) { name = "LocalDensity_B", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            _densityA.Create();
            _densityB.Create();

            _pingPong = false;
            _activeReadRT = _densityA;

            // Clear both
            ClearRT(_densityA);
            ClearRT(_densityB);
        }

        private void ClearRT(RenderTexture rt)
        {
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = prevActive;
        }

        // ═══════════════════════════════════════════
        // Common Compute Params
        // ═══════════════════════════════════════════

        private void SetCommonParams(int kernel, Vector3 windDir, float dt)
        {
            _compute.SetVector(CenterId, Center);
            _compute.SetFloat(TexelSizeId, TexelSize);
            _compute.SetInt(ResolutionId, Resolution);
            _compute.SetVector(WindDirectionId, windDir);
            _compute.SetFloat(AdvectionStrengthId, AdvectionStrength);
            _compute.SetFloat(RelaxationRateId, RelaxationRate);
            _compute.SetFloat(DeltaTimeId, dt);
        }

        private void SyncSplatBuffer()
        {
            int needed = _splatQueueCount;
            if (_splatBuffer == null || _splatBuffer.count < needed)
            {
                _splatBuffer?.Release();
                _splatBuffer = new ComputeBuffer(Mathf.Max(needed, 16), sizeof(float) * 5, ComputeBufferType.Structured);
            }
            _splatBuffer.SetData(_splatQueue, 0, 0, needed);
        }

        // ═══════════════════════════════════════════
        // Public API
        // ═══════════════════════════════════════════

        /// <summary>
        /// Текстура для передачи в raymarch-шейдер.
        /// </summary>
        public RenderTexture GetDensityRT() => _activeReadRT;

        /// <summary>
        /// Добавить гауссов сплат в очередь (положительный = добавить плотность, отрицательный = вырезать).
        /// </summary>
        public void SplatDensity(Vector3 worldPos, float radius, float amount)
        {
            if (_splatQueueCount >= _splatQueue.Length)
            {
                Debug.LogWarning("[LocalDensityBuffer] Splat queue full, dropping splat.");
                return;
            }

            radius = Mathf.Clamp(radius, 1f, MaxSplatRadius);
            _splatQueue[_splatQueueCount++] = new SplatData
            {
                center = worldPos,
                radius = radius,
                amount = amount
            };

            if (_verboseLogging && Time.frameCount % 60 == 0)
                Debug.Log($"[LocalDensityBuffer] Splat at {worldPos} r={radius} amount={amount} queue={_splatQueueCount}");

            // CPU mirror (Phase 2.5)
            ApplySplatToCpu(worldPos, radius, amount);
        }

        /// <summary>
        /// Полная очистка буфера (и GPU, и CPU).
        /// </summary>
        public void Clear()
        {
            ClearRT(_densityA);
            ClearRT(_densityB);
            _activeReadRT = _densityA;
            _pingPong = false;
            _splatQueueCount = 0;

            if (_cpuDensity != null)
                System.Array.Clear(_cpuDensity, 0, _cpuDensity.Length);
        }

        /// <summary>
        /// CPU-сэмпл плотности (для MeziyHarvestProbe, Phase 2.5).
        /// Тороидальная адресация + билинейная интерполяция по 8 соседям.
        /// </summary>
        public float SampleDensity(Vector3 worldPos)
        {
            if (_cpuDensity == null) return 0f;

            float windowSize = Resolution * TexelSize;
            Vector3 uvw = (worldPos - Center) / windowSize + new Vector3(0.5f, 0.5f, 0.5f);
            uvw = Frac(uvw); // toroidal wrap

            // Texel coordinates
            float tx = uvw.x * Resolution;
            float ty = uvw.y * Resolution;
            float tz = uvw.z * Resolution;

            int x0 = Mathf.FloorToInt(tx) % Resolution; if (x0 < 0) x0 += Resolution;
            int y0 = Mathf.FloorToInt(ty) % Resolution; if (y0 < 0) y0 += Resolution;
            int z0 = Mathf.FloorToInt(tz) % Resolution; if (z0 < 0) z0 += Resolution;
            int x1 = (x0 + 1) % Resolution;
            int y1 = (y0 + 1) % Resolution;
            int z1 = (z0 + 1) % Resolution;

            float fx = tx - Mathf.Floor(tx);
            float fy = ty - Mathf.Floor(ty);
            float fz = tz - Mathf.Floor(tz);

            float v000 = _cpuDensity[x0 + y0 * Resolution + z0 * Resolution * Resolution];
            float v100 = _cpuDensity[x1 + y0 * Resolution + z0 * Resolution * Resolution];
            float v010 = _cpuDensity[x0 + y1 * Resolution + z0 * Resolution * Resolution];
            float v110 = _cpuDensity[x1 + y1 * Resolution + z0 * Resolution * Resolution];
            float v001 = _cpuDensity[x0 + y0 * Resolution + z1 * Resolution * Resolution];
            float v101 = _cpuDensity[x1 + y0 * Resolution + z1 * Resolution * Resolution];
            float v011 = _cpuDensity[x0 + y1 * Resolution + z1 * Resolution * Resolution];
            float v111 = _cpuDensity[x1 + y1 * Resolution + z1 * Resolution * Resolution];

            return Trilinear(v000, v100, v010, v110, v001, v101, v011, v111, fx, fy, fz);
        }

        // ═══════════════════════════════════════════
        // CPU Mirror (Phase 2.5)
        // ═══════════════════════════════════════════

        private void ApplySplatToCpu(Vector3 worldPos, float radius, float amount)
        {
            float windowSize = Resolution * TexelSize;
            float sigma = radius * 0.33333f;
            float twoSigma2 = 2f * sigma * sigma;
            if (twoSigma2 < 0.001f) return;

            // Iterate only cells within radius (plus toroidal wrap for border cases)
            int cellRadius = Mathf.CeilToInt(radius / TexelSize) + 1;
            int r = Resolution;

            for (int dz = -cellRadius; dz <= cellRadius; dz++)
                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                    for (int dx = -cellRadius; dx <= cellRadius; dx++)
                    {
                        // World position of this neighbour cell
                        Vector3 cellWorld = worldPos + new Vector3(dx, dy, dz) * TexelSize;

                        Vector3 uvw = (cellWorld - Center) / windowSize + new Vector3(0.5f, 0.5f, 0.5f);
                        int ix = Mathf.FloorToInt(uvw.x * r) % r; if (ix < 0) ix += r;
                        int iy = Mathf.FloorToInt(uvw.y * r) % r; if (iy < 0) iy += r;
                        int iz = Mathf.FloorToInt(uvw.z * r) % r; if (iz < 0) iz += r;

                        float d = Vector3.Distance(cellWorld, worldPos);
                        float gauss = Mathf.Exp(-(d * d) / twoSigma2);
                        int idx = ix + iy * r + iz * r * r;
                        _cpuDensity[idx] = Mathf.Max(0f, _cpuDensity[idx] + amount * gauss);
                    }
        }

        private void SyncCpuFromSplats()
        {
            // CPU mirror is updated inline in SplatDensity/ApplySplatToCpu.
            // This method is reserved for future async GPU readback if needed.
        }

        private void RelaxCpuMirror(float dt)
        {
            float relax = RelaxationRate * dt;
            if (relax <= 0f) return;

            int len = _cpuDensity.Length;
            for (int i = 0; i < len; i++)
            {
                float v = _cpuDensity[i];
                if (v > 0f)
                    _cpuDensity[i] = Mathf.Max(0f, v - relax);
            }
        }

        private static Vector3 Frac(Vector3 v)
        {
            return new Vector3(v.x - Mathf.Floor(v.x), v.y - Mathf.Floor(v.y), v.z - Mathf.Floor(v.z));
        }

        private static float Trilinear(
            float v000, float v100, float v010, float v110,
            float v001, float v101, float v011, float v111,
            float fx, float fy, float fz)
        {
            float v00 = Mathf.Lerp(v000, v100, fx);
            float v10 = Mathf.Lerp(v010, v110, fx);
            float v01 = Mathf.Lerp(v001, v101, fx);
            float v11 = Mathf.Lerp(v011, v111, fx);
            float v0 = Mathf.Lerp(v00, v10, fy);
            float v1 = Mathf.Lerp(v01, v11, fy);
            return Mathf.Lerp(v0, v1, fz);
        }
    }
}
