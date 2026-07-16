// T-DOCK-14d: DockPadVisualMarker v4 — материалы в коде, ноль зависимости от сериализации.
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

        // Материалы создаются в коде — НЕ [SerializeField]. Ноль зависимости от GUID/сцены.
        private static Material s_neutralMat;
        private static Material s_freeMat;
        private static Material s_pendingMat;
        private static Material s_assignedToMeMat;
        private static Material s_assignedOtherMat;
        private static Material s_occupiedNpcMat;
        private static Material s_occupiedPlayerMat;
        private static bool s_matsCreated;

        // ============================================================
        // LIFECYCLE
        // ============================================================

        private void Awake()
        {
            CreateMaterialsIfNeeded();
        }

        private void Start()
        {
            CreateMaterialsIfNeeded(); // на случай если этот Start раньше чужого Awake
            _padBox = GetComponent<DockingPadTriggerBox>();
            _stateSync = GetComponentInParent<PadStateSync>();
            _props = new MaterialPropertyBlock();

            CreateVisual();
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
            if (_stateSync == null || _padBox == null) return;

            var newState = DetermineState();
            if (newState != _currentState)
            {
                _currentState = newState;
                _currentMaterial = ResolveMaterial(newState);
                ApplyMaterial();
            }

            AnimatePulse();
        }

        // ============================================================
        // MATERIALS (кодогенерация)
        // ============================================================

        private static void CreateMaterialsIfNeeded()
        {
            if (s_matsCreated) return;
            s_matsCreated = true;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            s_neutralMat        = NewMat(shader, "M_Pad_Neutral",       new Color(0.35f, 0.35f, 0.35f, 0.45f));
            s_freeMat           = NewMat(shader, "M_Pad_Free",          new Color(0.0f,  0.9f,  0.5f,  0.6f));
            s_pendingMat        = NewMat(shader, "M_Pad_Pending",       new Color(1.0f,  0.65f, 0.1f,  0.6f));
            s_assignedToMeMat   = NewMat(shader, "M_Pad_AssignedToMe",  new Color(0.15f, 0.35f, 1.0f,  0.7f));
            s_assignedOtherMat  = NewMat(shader, "M_Pad_AssignedOther", new Color(1.0f,  0.75f, 0.2f,  0.55f));
            s_occupiedNpcMat    = NewMat(shader, "M_Pad_OccupiedNpc",   new Color(1.0f,  0.45f, 0.0f,  0.65f));
            s_occupiedPlayerMat = NewMat(shader, "M_Pad_Occupied",      new Color(1.0f,  0.1f,  0.1f,  0.65f));
        }

        private static Material NewMat(Shader shader, string name, Color color)
        {
            var mat = new Material(shader);
            mat.name = name;
            mat.SetColor("_BaseColor", color);
            mat.hideFlags = HideFlags.HideAndDontSave;
            return mat;
        }

        // ============================================================
        // VISUAL CREATION
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

            _currentMaterial = s_neutralMat;
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

            if (_padBox.IsShipInside || (hasState && state.Value.isOccupied))
            {
                ulong occ = hasState ? state.Value.occupiedByClientId : 0;
                return occ > 0x7FFF_FFFF_FFFF_FFFFUL
                    ? PadVisualState.OccupiedNpc
                    : PadVisualState.OccupiedPlayer;
            }

            if (hasState && state.Value.isAssigned)
            {
                ulong localId = Unity.Netcode.NetworkManager.Singleton != null
                    ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;
                return (state.Value.assignedToClientId == localId && localId != 0)
                    ? PadVisualState.AssignedToMe
                    : PadVisualState.AssignedOther;
            }

            if (hasState && state.Value.isPending)
                return PadVisualState.Pending;

            return PadVisualState.Free;
        }

        private Material ResolveMaterial(PadVisualState state)
        {
            return state switch
            {
                PadVisualState.Free            => s_freeMat,
                PadVisualState.Pending         => s_pendingMat,
                PadVisualState.AssignedToMe    => s_assignedToMeMat,
                PadVisualState.AssignedOther   => s_assignedOtherMat,
                PadVisualState.OccupiedNpc     => s_occupiedNpcMat,
                PadVisualState.OccupiedPlayer  => s_occupiedPlayerMat,
                _                              => s_neutralMat
            };
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
