// ShipContrailVfx.cs — Phase 2.3
// Drives VFX Graph condensation trails behind the ship.
// Supports multiple spawn points (center + wings) computed from ship bounds.
// Finds the largest *enabled* visual mesh in children (skips disabled collider cubes).
// Uses Play/Stop for emission control (Simple_Trail template).
// VFX GameObjects are moved to trail positions each frame.

using UnityEngine;
using UnityEngine.VFX;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// Управляет VFX Graph конденсационными следами за кораблём.
    /// Поддерживает множественные точки спавна по геометрии корабля:
    /// центр + боковые кромки (крылья).
    /// Размещается на дочернем GameObject (напр. ContrailVFX), ShipController
    /// находит через GetComponentInParent.
    ///
    /// Настройка VFX Graph — см. docs/world/CLOUD_system/3.0/CONTRAIL_VFX_GUIDE.md
    /// </summary>
    public class ShipContrailVfx : MonoBehaviour
    {
        [Header("VFX")]
        [Tooltip("Основной VisualEffect (центр). Боковые создаются автоматически.")]
        public VisualEffect Vfx;

        [Header("Ship")]
        [Tooltip("ShipController (опционально). Авто-поиск через GetComponentInParent.")]
        public ShipController Ship;

        [Header("Emit Conditions")]
        [Range(0f, 30f)] public float MinSpeed = 5f;

        [Header("Trail Points")]
        [Tooltip("Количество точек спавна: 1=центр, 3=центр+бока, 5=центр+2пары")]
        [Range(1, 5)] public int TrailCount = 3;

        [Tooltip("Доля ширины корабля для боковых точек (0.3 = 30% от полуширины).")]
        [Range(0.1f, 1.5f)] public float TrailWidth = 0.6f;

        [Tooltip("Смещение назад от центра (доля от half-size.z корабля).")]
        [Range(0.5f, 2f)] public float TrailDepth = 1.1f;

        [Header("Adaptive Scale")]
        [Tooltip("Автоопределение размера корабля по визуальному мешу.")]
        public bool UseShipBounds = true;

        [Tooltip("Ручной размер корабля (если UseShipBounds=false или меш не найден).")]
        public Vector3 ManualBoundsSize = new Vector3(8f, 4f, 15f);

        [Header("VFX Parameters")]
        [Tooltip("Базовое время жизни частиц (сек). Масштабируется от размера корабля.")]
        public float BaseLifetime = 3.5f;

        [Tooltip("Базовый размер частиц. Масштабируется.")]
        public float BaseSize = 2.5f;

        [Tooltip("Базовый spawn rate. Масштабируется.")]
        public float BaseSpawnRate = 40f;

        [Header("Stop Behaviour")]
        [Tooltip("Задержка перед Stop() после падения скорости. Даёт последним частицам время на fade-in.")]
        [Range(0f, 2f)] public float StopDelay = 0.4f;

        // ── Internal ──
        private Rigidbody _rb;
        private bool _wasEmitting;
        private float _stopRequestTime = -1f;
        private VisualEffect[] _sideVfxs;
        private Vector3[] _spawnOffsets;
        private Vector3 _shipBoundsSize;

        private void Start()
        {
            if (Vfx == null) Vfx = GetComponent<VisualEffect>();
            if (Ship == null) Ship = GetComponentInParent<ShipController>();
            _rb = Ship != null ? Ship.GetComponent<Rigidbody>() : GetComponent<Rigidbody>();

            _shipBoundsSize = UseShipBounds && Ship != null
                ? GetShipVisualSize(Ship.gameObject)
                : ManualBoundsSize;

            ComputeSpawnOffsets();
            CreateSideVfxInstances();
            StopAllVfx();
        }

        /// <summary>
        /// Находит размер корабля по самому большому ENABLED MeshRenderer в иерархии.
        /// Игнорирует выключенные рендереры (dummy-коллайдеры на руте) и мелочь.
        /// </summary>
        private Vector3 GetShipVisualSize(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(false);
            Vector3 bestSize = ManualBoundsSize;
            float bestVolume = 0f;

            foreach (var mr in renderers)
            {
                if (!mr.enabled) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var sz = mf.sharedMesh.bounds.size;
                var t = mr.transform;
                sz = new Vector3(sz.x * t.lossyScale.x, sz.y * t.lossyScale.y, sz.z * t.lossyScale.z);
                float vol = sz.x * sz.y * sz.z;
                if (vol > bestVolume) { bestVolume = vol; bestSize = sz; }
            }

            return bestSize;
        }

        private void ComputeSpawnOffsets()
        {
            _spawnOffsets = new Vector3[TrailCount];
            float halfW = _shipBoundsSize.x * 0.5f * TrailWidth;
            float backZ = -_shipBoundsSize.z * 0.5f * TrailDepth;

            switch (TrailCount)
            {
                case 1:
                    _spawnOffsets[0] = new Vector3(0f, 0f, backZ); break;
                case 2:
                    _spawnOffsets[0] = new Vector3(-halfW, 0f, backZ);
                    _spawnOffsets[1] = new Vector3( halfW, 0f, backZ); break;
                case 3:
                    _spawnOffsets[0] = new Vector3(0f, 0f, backZ);
                    _spawnOffsets[1] = new Vector3(-halfW, 0f, backZ);
                    _spawnOffsets[2] = new Vector3( halfW, 0f, backZ); break;
                case 4:
                    _spawnOffsets[0] = new Vector3(-halfW * 0.7f, 0f, backZ);
                    _spawnOffsets[1] = new Vector3( halfW * 0.7f, 0f, backZ);
                    _spawnOffsets[2] = new Vector3(-halfW, 0f, backZ);
                    _spawnOffsets[3] = new Vector3( halfW, 0f, backZ); break;
                default: // 5
                    _spawnOffsets[0] = new Vector3(0f, 0f, backZ);
                    _spawnOffsets[1] = new Vector3(-halfW * 0.5f, 0f, backZ);
                    _spawnOffsets[2] = new Vector3( halfW * 0.5f, 0f, backZ);
                    _spawnOffsets[3] = new Vector3(-halfW, 0f, backZ);
                    _spawnOffsets[4] = new Vector3( halfW, 0f, backZ); break;
            }
        }

        private void CreateSideVfxInstances()
        {
            if (TrailCount <= 1) { _sideVfxs = new VisualEffect[0]; return; }
            _sideVfxs = new VisualEffect[TrailCount - 1];

            for (int i = 1; i < TrailCount; i++)
            {
                var sideGo = new GameObject($"Contrail_Side{i}");
                sideGo.transform.SetParent(transform.parent, worldPositionStays: false);
                sideGo.transform.localPosition = Vector3.zero;
                sideGo.transform.localRotation = Quaternion.identity;

                var sideVfx = sideGo.AddComponent<VisualEffect>();
                sideVfx.visualEffectAsset = Vfx != null ? Vfx.visualEffectAsset : null;
                sideVfx.Stop();

                _sideVfxs[i - 1] = sideVfx;
            }
        }

        private void StopAllVfx()
        {
            if (Vfx != null) Vfx.Stop();
            if (_sideVfxs != null)
                foreach (var v in _sideVfxs)
                    if (v != null) v.Stop();
            _wasEmitting = false;
            _stopRequestTime = -1f;
        }

        private void Update()
        {
            if (Vfx == null) return;

            float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
            bool docked = Ship != null && Ship.IsDocked;
            bool shouldEmit = !docked && speed > MinSpeed;

            float sizeScale = Mathf.Max(_shipBoundsSize.z / 15f, 0.5f);
            ApplyVfxScale(sizeScale);

            // Delayed stop: gives last particles time for fade-in (prevents hard tear-off)
            if (shouldEmit && !_wasEmitting)
            {
                PlayAllVfx();
                _wasEmitting = true;
                _stopRequestTime = -1f;
            }
            else if (!shouldEmit && _wasEmitting)
            {
                if (_stopRequestTime < 0f) _stopRequestTime = Time.time;
                if (Time.time - _stopRequestTime >= StopDelay)
                {
                    StopAllVfx();
                }
            }

            // Move VFX objects to trail positions (always while emitting or in stop-delay)
            if (shouldEmit || (_wasEmitting && _stopRequestTime >= 0f))
            {
                Vector3 shipPos = Ship != null ? Ship.transform.position : transform.position;
                Quaternion shipRot = Ship != null ? Ship.transform.rotation : transform.rotation;

                Vfx.transform.position = shipPos + shipRot * _spawnOffsets[0];
                Vfx.transform.rotation = shipRot;

                for (int i = 1; i < TrailCount; i++)
                {
                    var sideVfx = _sideVfxs[i - 1];
                    if (sideVfx == null) continue;
                    sideVfx.transform.position = shipPos + shipRot * _spawnOffsets[i];
                    sideVfx.transform.rotation = shipRot;
                }
            }
        }

        private void PlayAllVfx()
        {
            if (Vfx != null) Vfx.Play();
            if (_sideVfxs != null)
                foreach (var v in _sideVfxs)
                    if (v != null) v.Play();
        }

        private void ApplyVfxScale(float scale)
        {
            if (Vfx == null) return;
            float lifetime = BaseLifetime * scale;
            float baseSize = BaseSize * scale;
            float spawnRate = BaseSpawnRate * scale;

            // Per-frame random size (±30%) → per-particle variation without VFX Random op
            Vfx.SetFloat("TrailLifetime", lifetime);
            Vfx.SetFloat("TrailSize", baseSize * Random.Range(0.7f, 1.3f));
            Vfx.SetFloat("TrailSpawnRate", spawnRate);

            foreach (var v in _sideVfxs)
            {
                if (v == null) continue;
                v.SetFloat("TrailLifetime", lifetime);
                v.SetFloat("TrailSize", baseSize * Random.Range(0.7f, 1.3f));
                v.SetFloat("TrailSpawnRate", spawnRate);
            }
        }

        private void OnDisable() => StopAllVfx();

        private void OnDestroy()
        {
            if (_sideVfxs != null)
            {
                foreach (var v in _sideVfxs)
                    if (v != null && v.gameObject != null)
                        Destroy(v.gameObject);
                _sideVfxs = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_spawnOffsets == null) return;
            if (UseShipBounds && Ship != null)
                _shipBoundsSize = GetShipVisualSize(Ship.gameObject);
            else if (!UseShipBounds)
                _shipBoundsSize = ManualBoundsSize;
            ComputeSpawnOffsets();
        }
#endif
    }
}
