// T-DOCK-14d: DockPadVisualMarker v3 — минимальный, надёжный.
// Создаёт Quad-диск над триггер-боксом. Цвет меняется по состоянию из PadStateSync.

using UnityEngine;

namespace ProjectC.Docking.Stations
{
    public enum PadVisualState
    {
        Neutral, Free, Pending, AssignedToMe, AssignedOther, OccupiedNpc, OccupiedPlayer
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(DockingPadTriggerBox))]
    public class DockPadVisualMarker : MonoBehaviour
    {
        [Header("Материалы")]
        [SerializeField] private Material neutralMat;
        [SerializeField] private Material freeMat;
        [SerializeField] private Material pendingMat;
        [SerializeField] private Material assignedToMeMat;
        [SerializeField] private Material assignedOtherMat;
        [SerializeField] private Material occupiedNpcMat;
        [SerializeField] private Material occupiedPlayerMat;

        [Header("Размеры")]
        [SerializeField] private float markerSize = 8f;
        [SerializeField] private float markerHeight = 0.1f;
        [SerializeField] private float pulseSpeed = 2.5f;

        private DockingPadTriggerBox _padBox;
        private PadStateSync _stateSync;
        private MeshRenderer _surfaceRenderer;
        private Material _currentMaterial;
        private PadVisualState _currentState = PadVisualState.Neutral;
        private float _pulsePhase;
        private MaterialPropertyBlock _props;
        private GameObject _visualRoot;

        private static Material s_fallbackMat;

        // ============================================================
        // LIFECYCLE
        // ============================================================

        private void Start()
        {
            _padBox = GetComponent<DockingPadTriggerBox>();
            _stateSync = GetComponentInParent<PadStateSync>();
            _props = new MaterialPropertyBlock();

            CreateVisual();

            if (name == "Pad_005")
            {
                Debug.Log($"[Pad_005] Start: padBox={_padBox?.PadId}, stateSync={(_stateSync != null)}, " +
                    $"neutralMat={neutralMat?.name ?? "NULL"}, freeMat={freeMat?.name ?? "NULL"}, " +
                    $"pendingMat={pendingMat?.name ?? "NULL"}, assignedToMeMat={assignedToMeMat?.name ?? "NULL"}, " +
                    $"surfaceRenderer={_surfaceRenderer != null}, currentMat={_currentMaterial?.name ?? "NULL"}",
                    this);
            }
        }

        private void OnDestroy()
        {
            if (_visualRoot != null)
            {
                if (Application.isPlaying) Destroy(_visualRoot);
                else DestroyImmediate(_visualRoot);
            }
        }

        private void LateUpdate()
        {
            if (_stateSync == null || _padBox == null)
            {
                if (name == "Pad_005")
                    Debug.LogWarning($"[Pad_005] LateUpdate SKIP: stateSync={_stateSync != null} padBox={_padBox != null}", this);
                return;
            }

            var newState = DetermineState();
            if (newState != _currentState)
            {
                if (name == "Pad_005")
                    Debug.Log($"[Pad_005] State change: {_currentState} -> {newState}, mat={ResolveMaterial(newState)?.name ?? "NULL"}", this);
                _currentState = newState;
                _currentMaterial = ResolveMaterial(newState);
                ApplyMaterial();
            }

            AnimatePulse();
        }

        // ============================================================
        // VISUAL CREATION (один раз в Start)
        // ============================================================

        private void CreateVisual()
        {
            var box = GetComponent<BoxCollider>();
            Vector3 center = box != null ? box.center : Vector3.zero;
            float height = box != null ? box.size.y : 6f;

            _visualRoot = new GameObject("_PadVisual");
            _visualRoot.transform.SetParent(transform, false);
            _visualRoot.transform.localPosition = center + Vector3.up * (height * 0.5f + markerHeight);
            _visualRoot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _visualRoot.transform.localScale = new Vector3(markerSize, markerSize, 1f);

            var mf = _visualRoot.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

            _surfaceRenderer = _visualRoot.AddComponent<MeshRenderer>();
            _surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _surfaceRenderer.receiveShadows = false;

            _currentMaterial = neutralMat;
            ApplyMaterial();
        }

        // ============================================================
        // STATE
        // ============================================================

        private PadVisualState DetermineState()
        {
            if (_stateSync == null) return PadVisualState.Neutral;

            var state = _stateSync.GetState(_padBox.PadId);
            bool hasState = state.HasValue;

            if (name == "Pad_005")
            {
                if (hasState)
                    Debug.Log($"[Pad_005] DetermineState: hasState=YES assigned={state.Value.isAssigned} pending={state.Value.isPending} occupied={state.Value.isOccupied}", this);
                else
                    Debug.Log($"[Pad_005] DetermineState: hasState=NO", this);
            }

            // 1. Занят кораблём?
            if (_padBox.IsShipInside || (hasState && state.Value.isOccupied))
            {
                ulong occ = hasState ? state.Value.occupiedByClientId : 0;
                return occ > 0x7FFF_FFFF_FFFF_FFFFUL
                    ? PadVisualState.OccupiedNpc
                    : PadVisualState.OccupiedPlayer;
            }

            // 2. Назначен?
            if (hasState && state.Value.isAssigned)
            {
                ulong localId = Unity.Netcode.NetworkManager.Singleton != null
                    ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;
                return (state.Value.assignedToClientId == localId && localId != 0)
                    ? PadVisualState.AssignedToMe
                    : PadVisualState.AssignedOther;
            }

            // 3. Pending?
            if (hasState && state.Value.isPending)
                return PadVisualState.Pending;

            return PadVisualState.Free;
        }

        private Material ResolveMaterial(PadVisualState state)
        {
            Material m = state switch
            {
                PadVisualState.Free            => freeMat,
                PadVisualState.Pending         => pendingMat,
                PadVisualState.AssignedToMe    => assignedToMeMat,
                PadVisualState.AssignedOther   => assignedOtherMat,
                PadVisualState.OccupiedNpc     => occupiedNpcMat,
                PadVisualState.OccupiedPlayer  => occupiedPlayerMat,
                _                              => neutralMat
            };

            if (m != null) return m;

            // Fallback
            if (s_fallbackMat == null)
            {
                s_fallbackMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                s_fallbackMat.SetColor("_BaseColor", new Color(1f, 0f, 1f, 0.9f)); // MAGENTA — сразу видно fallback
            }
            return s_fallbackMat;
        }

        private void ApplyMaterial()
        {
            if (_surfaceRenderer != null && _currentMaterial != null)
                _surfaceRenderer.sharedMaterial = _currentMaterial;
        }

        // ============================================================
        // ANIMATION
        // ============================================================

        private void AnimatePulse()
        {
            if (_surfaceRenderer == null || _currentMaterial == null) return;

            _pulsePhase += Time.deltaTime * pulseSpeed;
            float t = (Mathf.Sin(_pulsePhase) + 1f) * 0.5f;

            float mult = _currentState switch
            {
                PadVisualState.Free            => 0.6f + t * 0.4f,
                PadVisualState.Pending         => 0.5f + t * 0.5f,
                PadVisualState.AssignedToMe    => 0.8f + t * 0.5f,
                PadVisualState.OccupiedPlayer  => 0.7f + t * 0.15f,
                PadVisualState.OccupiedNpc     => 0.7f + t * 0.15f,
                _                              => 0.4f + t * 0.15f
            };

            _surfaceRenderer.GetPropertyBlock(_props);
            Color c = _currentMaterial.GetColor("_BaseColor");
            _props.SetColor("_BaseColor", new Color(c.r, c.g, c.b, c.a * mult));
            _surfaceRenderer.SetPropertyBlock(_props);
        }

        // ============================================================
        // EDITOR
        // ============================================================

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;

            Color c = _currentState switch
            {
                PadVisualState.Free            => new Color(0, 1, 0.6f, 0.3f),
                PadVisualState.Pending         => new Color(1, 0.7f, 0.1f, 0.3f),
                PadVisualState.AssignedToMe    => new Color(0.2f, 0.4f, 1f, 0.4f),
                PadVisualState.AssignedOther   => new Color(1, 0.7f, 0.1f, 0.25f),
                PadVisualState.OccupiedNpc     => new Color(1, 0.5f, 0, 0.35f),
                PadVisualState.OccupiedPlayer  => new Color(1, 0.15f, 0.15f, 0.35f),
                _                              => new Color(0.4f, 0.4f, 0.4f, 0.2f)
            };

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = c;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.color = new Color(c.r, c.g, c.b, c.a * 0.5f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
