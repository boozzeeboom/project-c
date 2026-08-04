// ShipContrailVfx.cs — Phase 2.3
// Drives VFX Graph condensation trails behind the ship.
// Supports multiple spawn points (center + wings) computed from ship bounds.
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
    ///
    /// Настройка VFX Graph — см. docs/world/CLOUD_system/3.0/CONTRAIL_VFX_GUIDE.md
    /// </summary>
    public class ShipContrailVfx : MonoBehaviour
    {
        [Header("VFX")]
        [Tooltip("Основной VisualEffect (центр). Боковые создаются автоматически.")]
        public VisualEffect Vfx;

        [Header("Ship")]
        [Tooltip("ShipController (опционально).")]
        public ShipController Ship;

        [Header("Emit Conditions")]
        [Range(0f, 30f)] public float MinSpeed = 5f;

        [Header("Trail Points")]
        [Tooltip("Количество точек спавна: 1=центр, 3=центр+бока, 5=центр+2пары")]
        [Range(1, 5)] public int TrailCount = 3;

        [Tooltip("Доля ширины корабля для боковых точек (0.3 = 30% от полуширины).")]
        [Range(0.1f, 1.5f)] public float TrailWidth = 0.6f;

        [Tooltip("Базовое смещение назад от центра корабля (умножается на bounds.extents.z).")]
        [Range(0.5f, 2f)] public float TrailDepth = 1.1f;

        [Header("Adaptive Scale")]
        [Tooltip("Автоопределение размера корабля из MeshFilter. Отключи для ручного задания.")]
        public bool UseShipBounds = true;

        [Tooltip("Ручной размер корабля (если UseShipBounds=false).")]
        public Vector3 ManualBoundsSize = new Vector3(8f, 4f, 15f);

        [Header("VFX Parameters")]
        [Tooltip("Базовое время жизни частиц (сек). Масштабируется от размера корабля.")]
        public float BaseLifetime = 3.5f;

        [Tooltip("Базовый размер частиц. Масштабируется.")]
        public float BaseSize = 2.5f;

        [Tooltip("Базовый spawn rate. Масштабируется.")]
        public float BaseSpawnRate = 40f;

        // ── Internal ──
        private Rigidbody _rb;
        private bool _wasEmitting;
        private VisualEffect[] _sideVfxs; // боковые VFX (инстансы)
        private Vector3[] _spawnOffsets;   // локальные смещения точек спавна
        private Vector3 _shipBoundsSize;

        private void Start()
        {
            if (Vfx == null)
                Vfx = GetComponent<VisualEffect>();

            if (Ship == null)
                Ship = GetComponentInParent<ShipController>();

            _rb = Ship != null ? Ship.GetComponent<Rigidbody>() : GetComponent<Rigidbody>();

            // Determine ship size
            _shipBoundsSize = ManualBoundsSize;
            if (UseShipBounds && Ship != null)
            {
                var mf = Ship.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    _shipBoundsSize = mf.sharedMesh.bounds.size;
                else
                    _shipBoundsSize = ManualBoundsSize;
            }

            // Compute spawn offsets
            ComputeSpawnOffsets();

            // Create side VFX instances
            CreateSideVfxInstances();

            // Stop all
            StopAllVfx();
        }

        private void ComputeSpawnOffsets()
        {
            _spawnOffsets = new Vector3[TrailCount];
            float halfW = _shipBoundsSize.x * 0.5f * TrailWidth;
            float backZ = -_shipBoundsSize.z * 0.5f * TrailDepth;

            switch (TrailCount)
            {
                case 1:
                    _spawnOffsets[0] = new Vector3(0f, 0f, backZ);
                    break;
                case 2:
                    _spawnOffsets[0] = new Vector3(-halfW, 0f, backZ);
                    _spawnOffsets[1] = new Vector3( halfW, 0f, backZ);
                    break;
                case 3:
                    _spawnOffsets[0] = new Vector3(0f, 0f, backZ);
                    _spawnOffsets[1] = new Vector3(-halfW, 0f, backZ);
                    _spawnOffsets[2] = new Vector3( halfW, 0f, backZ);
                    break;
                case 4:
                    _spawnOffsets[0] = new Vector3(-halfW * 0.7f, 0f, backZ);
                    _spawnOffsets[1] = new Vector3( halfW * 0.7f, 0f, backZ);
                    _spawnOffsets[2] = new Vector3(-halfW, 0f, backZ);
                    _spawnOffsets[3] = new Vector3( halfW, 0f, backZ);
                    break;
                default: // 5
                    _spawnOffsets[0] = new Vector3(0f, 0f, backZ);
                    _spawnOffsets[1] = new Vector3(-halfW * 0.5f, 0f, backZ);
                    _spawnOffsets[2] = new Vector3( halfW * 0.5f, 0f, backZ);
                    _spawnOffsets[3] = new Vector3(-halfW, 0f, backZ);
                    _spawnOffsets[4] = new Vector3( halfW, 0f, backZ);
                    break;
            }
        }

        private void CreateSideVfxInstances()
        {
            // First VFX is the main one on this GameObject (index 0)
            // Additional VFX instances for side trails
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

                // Copy renderer settings from main
                var mainRenderer = Vfx != null ? Vfx.GetComponent<UnityEngine.VFX.VFXRenderer>() : null;
                if (mainRenderer != null)
                {
                    var sideRenderer = sideVfx.GetComponent<UnityEngine.VFX.VFXRenderer>();
                    if (sideRenderer != null)
                    {
                        sideRenderer.castShadows = mainRenderer.castShadows;
                        sideRenderer.receiveShadows = mainRenderer.receiveShadows;
                    }
                }

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
        }

        private void Update()
        {
            if (Vfx == null) return;

            // Determine emission state
            float speed = 0f;
            if (_rb != null) speed = _rb.linearVelocity.magnitude;
            bool docked = Ship != null && Ship.IsDocked;
            bool shouldEmit = !docked && speed > MinSpeed;

            // Scale VFX parameters by ship size
            float sizeScale = Mathf.Max(_shipBoundsSize.z / 15f, 0.5f);
            ApplyVfxScale(sizeScale);

            // Play/Stop control
            if (shouldEmit && !_wasEmitting)
            {
                PlayAllVfx();
                _wasEmitting = true;
            }
            else if (!shouldEmit && _wasEmitting)
            {
                StopAllVfx();
            }

            // Move VFX GameObjects to trail positions
            if (shouldEmit)
            {
                Vector3 shipPos = Ship != null ? Ship.transform.position : transform.position;
                Quaternion shipRot = Ship != null ? Ship.transform.rotation : transform.rotation;

                // Main (center) VFX
                Vfx.transform.position = shipPos + shipRot * _spawnOffsets[0];
                Vfx.transform.rotation = shipRot;

                // Side VFX instances
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
            float size = BaseSize * scale;
            float spawnRate = BaseSpawnRate * scale;

            Vfx.SetFloat("TrailLifetime", lifetime);
            Vfx.SetFloat("TrailSize", size);
            Vfx.SetFloat("TrailSpawnRate", spawnRate);

            foreach (var v in _sideVfxs)
            {
                if (v == null) continue;
                v.SetFloat("TrailLifetime", lifetime);
                v.SetFloat("TrailSize", size);
                v.SetFloat("TrailSpawnRate", spawnRate);
            }
        }

        private void OnDisable()
        {
            StopAllVfx();
        }

        private void OnDestroy()
        {
            // Clean up side VFX GameObjects
            if (_sideVfxs != null)
            {
                foreach (var v in _sideVfxs)
                {
                    if (v != null && v.gameObject != null)
                        Destroy(v.gameObject);
                }
                _sideVfxs = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_spawnOffsets == null) return;
            // Recompute offsets in editor when params change
            if (UseShipBounds && Ship != null)
            {
                var mf = Ship.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    _shipBoundsSize = mf.sharedMesh.bounds.size;
            }
            else if (!UseShipBounds)
            {
                _shipBoundsSize = ManualBoundsSize;
            }
            ComputeSpawnOffsets();
        }
#endif
    }
}
