// T-DOCK-14d: DockPadVisualMarker v2 — визуальный маркер пада с holographic-эффектами.
// Полная переработка старой заглушки (Quad + OverlapSphere).
//
// Архитектура:
//   • MonoBehaviour на pad-детях (НЕ NetworkBehaviour — пады не NetworkObject)
//   • Читает PadStateSync (parent) + DockingClientState (singleton)
//   • 7 визуальных состояний с разными материалами и анимациями
//   • Создаёт визуальные объекты: padSurface (диск) + padRing (кольцо)
//
// Зависимости: PadStateSync, DockingClientState, DockingPadTriggerBox

using ProjectC.Docking.Client;   // DockingClientState
using ProjectC.Docking.Network;  // DockingZoneRegistry
using UnityEngine;

namespace ProjectC.Docking.Stations
{
    public enum PadVisualState
    {
        Neutral,         // серый тусклый — вне зоны внимания
        Free,            // зелёный/бирюзовый, мягкое пульсирование
        Pending,         // жёлтый — ждёт подтверждения
        AssignedToMe,    // синий, активное свечение + ring — МОЙ пад
        AssignedOther,   // жёлтый steady — зарезервирован другим
        OccupiedNpc,     // оранжевый — NPC на паду
        OccupiedPlayer   // красный — игрок на паду
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(DockingPadTriggerBox))]
    [ExecuteAlways]
    public class DockPadVisualMarker : MonoBehaviour
    {
        [Header("Материалы (7 состояний)")]
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
        [SerializeField] private float ringThickness = 0.3f;
        [SerializeField] private float ringExtraSize = 1.5f; // насколько ring больше padSurface

        [Header("Анимация")]
        [SerializeField] private float pulseSpeed = 2.5f;
        [SerializeField] private float ringRotateSpeed = 45f; // градусов/сек для AssignedToMe

        // Runtime
        private DockingPadTriggerBox _padBox;
        private PadStateSync _stateSync;
        private PadVisualState _currentState = PadVisualState.Neutral;
        private Material _currentMaterial;

        // Визуальные объекты
        private GameObject _padSurface;
        private GameObject _padRing;
        private MeshRenderer _surfaceRenderer;
        private MeshRenderer _ringRenderer;

        // Анимация
        private float _pulsePhase;
        private MaterialPropertyBlock _surfaceProps;
        private MaterialPropertyBlock _ringProps;

        private const string EMISSION_COLOR = "_EmissionColor";
        private const string BASE_COLOR = "_BaseColor";

        // ============================================================
        // LIFECYCLE
        // ============================================================

        private void Awake()
        {
            _padBox = GetComponent<DockingPadTriggerBox>();
            _surfaceProps = new MaterialPropertyBlock();
            _ringProps = new MaterialPropertyBlock();

            // T-DOCK-14d: [ExecuteAlways] — не дублируем визуалы при перекомпиляции
            if (transform.Find("_PadSurface") == null)
                BuildVisuals();
        }

        private void Start()
        {
            // T-DOCK-14d: PadStateSync нужен только в Play Mode
            if (!Application.isPlaying) return;
            _stateSync = GetComponentInParent<PadStateSync>();
        }

        private void OnDestroy()
        {
            CleanupVisuals();
        }

        private void LateUpdate()
        {
            // T-DOCK-14d: обновление состояния — только в Play Mode
            if (!Application.isPlaying) return;
            if (_stateSync == null) return;

            var newState = DetermineState();
            if (newState != _currentState)
            {
                _currentState = newState;
                ApplyState();
            }
            AnimatePulse();
        }

        // ============================================================
        // BUILD VISUALS
        // ============================================================

        private void BuildVisuals()
        {
            var box = GetComponent<BoxCollider>();
            Vector3 localCenter = box != null ? box.center : Vector3.zero;
            Vector3 baseSize = box != null ? box.size : Vector3.one * 6f;

            // Pad surface — плоский диск/quad над триггер-боксом
            _padSurface = CreateQuadChild(
                "_PadSurface",
                localCenter + Vector3.up * (baseSize.y * 0.5f + markerHeight),
                new Vector3(markerSize, markerSize, 1f)
            );
            _surfaceRenderer = _padSurface.GetComponent<MeshRenderer>();

            // Pad ring — ring вокруг пада
            _padRing = CreateQuadChild(
                "_PadRing",
                localCenter + Vector3.up * (baseSize.y * 0.5f + markerHeight + 0.02f),
                new Vector3(markerSize + ringExtraSize, markerSize + ringExtraSize, 1f)
            );
            _ringRenderer = _padRing.GetComponent<MeshRenderer>();

            // Начальное состояние
            _currentState = PadVisualState.Neutral;
            _currentMaterial = neutralMat;
            ApplyMaterial();
        }

        private GameObject CreateQuadChild(string name, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // плашмя
            go.transform.localScale = scale;

            var mf = go.AddComponent<MeshFilter>();
            var quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (quadMesh == null) quadMesh = Resources.GetBuiltinResource<Mesh>("Quad");
            mf.sharedMesh = quadMesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            return go;
        }

        private void CleanupVisuals()
        {
            if (_padSurface != null)
            {
                if (Application.isPlaying) Destroy(_padSurface);
                else DestroyImmediate(_padSurface);
                _padSurface = null;
            }
            if (_padRing != null)
            {
                if (Application.isPlaying) Destroy(_padRing);
                else DestroyImmediate(_padRing);
                _padRing = null;
            }
        }

        // ============================================================
        // STATE DETERMINATION
        // ============================================================

        private PadVisualState DetermineState()
        {
            var state = _stateSync.GetState(_padBox.PadId);
            bool hasState = state.HasValue;

            // 1. Физически занят кораблём?
            if (_padBox.IsShipInside || (hasState && state.Value.isOccupied))
            {
                ulong occupant = hasState ? state.Value.occupiedByClientId : 0;
                // NPC detection: NPC id has high bit set
                bool isNpc = occupant > 0x7FFF_FFFF_FFFF_FFFFUL;
                return isNpc ? PadVisualState.OccupiedNpc : PadVisualState.OccupiedPlayer;
            }

            // 2. Назначен кому-то?
            if (hasState && state.Value.isAssigned)
            {
                // Мой ли это клиент?
                var clientState = DockingClientState.Instance;
                // Проверяем: assignedToClientId совпадает с local client?
                // Используем NetworkManager.LocalClientId
                ulong localId = Unity.Netcode.NetworkManager.Singleton != null
                    ? Unity.Netcode.NetworkManager.Singleton.LocalClientId
                    : 0;
                if (state.Value.assignedToClientId == localId && localId != 0)
                    return PadVisualState.AssignedToMe;
                else
                    return PadVisualState.AssignedOther;
            }

            // 3. Pending (ждёт подтверждения другого игрока)?
            if (hasState && state.Value.isPending)
                return PadVisualState.Pending;

            // 4. Свободен
            return PadVisualState.Free;
        }

        // ============================================================
        // APPLY STATE
        // ============================================================

        private void ApplyState()
        {
            _currentMaterial = GetMaterialForState(_currentState);
            ApplyMaterial();
            _pulsePhase = 0f;

            // Ring visibility: только для AssignedToMe активен
            if (_ringRenderer != null)
                _ringRenderer.enabled = (_currentState == PadVisualState.AssignedToMe);
        }

        private Material GetMaterialForState(PadVisualState state)
        {
            return state switch
            {
                PadVisualState.Free => freeMat ?? neutralMat,
                PadVisualState.Pending => pendingMat ?? neutralMat,
                PadVisualState.AssignedToMe => assignedToMeMat ?? freeMat,
                PadVisualState.AssignedOther => assignedOtherMat ?? pendingMat,
                PadVisualState.OccupiedNpc => occupiedNpcMat ?? occupiedPlayerMat,
                PadVisualState.OccupiedPlayer => occupiedPlayerMat ?? occupiedNpcMat,
                _ => neutralMat
            };
        }

        private void ApplyMaterial()
        {
            if (_surfaceRenderer != null && _currentMaterial != null)
                _surfaceRenderer.sharedMaterial = _currentMaterial;
            if (_ringRenderer != null && _currentMaterial != null)
                _ringRenderer.sharedMaterial = _currentMaterial;
        }

        // ============================================================
        // ANIMATION
        // ============================================================

        private void AnimatePulse()
        {
            _pulsePhase += Time.deltaTime * pulseSpeed;

            float pulse01 = (Mathf.Sin(_pulsePhase) + 1f) * 0.5f;
            float emissionMultiplier = 1f;

            switch (_currentState)
            {
                case PadVisualState.Free:
                    emissionMultiplier = 0.6f + pulse01 * 0.4f;
                    break;
                case PadVisualState.Pending:
                    emissionMultiplier = 0.5f + pulse01 * 0.5f; // заметное мигание
                    break;
                case PadVisualState.AssignedToMe:
                    emissionMultiplier = 0.8f + pulse01 * 0.5f;
                    // Вращение ring
                    if (_padRing != null)
                        _padRing.transform.localRotation = Quaternion.Euler(90f, _pulsePhase * ringRotateSpeed / pulseSpeed, 0f);
                    break;
                case PadVisualState.AssignedOther:
                    emissionMultiplier = 0.5f + pulse01 * 0.2f;
                    break;
                case PadVisualState.OccupiedNpc:
                case PadVisualState.OccupiedPlayer:
                    emissionMultiplier = 0.7f + pulse01 * 0.15f; // слабое дыхание
                    break;
                default:
                    emissionMultiplier = 0.4f + pulse01 * 0.15f;
                    break;
            }

            // Применяем пульсацию emission через MaterialPropertyBlock
            if (_surfaceRenderer != null && _currentMaterial != null)
            {
                _surfaceRenderer.GetPropertyBlock(_surfaceProps);
                Color baseEmission = _currentMaterial.GetColor(EMISSION_COLOR);
                if (baseEmission == Color.clear) baseEmission = _currentMaterial.GetColor(BASE_COLOR) * 0.5f;
                _surfaceProps.SetColor(EMISSION_COLOR, baseEmission * emissionMultiplier);
                _surfaceRenderer.SetPropertyBlock(_surfaceProps);
            }
        }

        // ============================================================
        // EDITOR
        // ============================================================

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Color gizmoColor = _currentState switch
            {
                PadVisualState.Free => new Color(0, 1, 0.6f, 0.3f),
                PadVisualState.Pending => new Color(1, 0.7f, 0.1f, 0.3f),
                PadVisualState.AssignedToMe => new Color(0.2f, 0.4f, 1f, 0.4f),
                PadVisualState.AssignedOther => new Color(1, 0.7f, 0.1f, 0.25f),
                PadVisualState.OccupiedNpc => new Color(1, 0.5f, 0, 0.35f),
                PadVisualState.OccupiedPlayer => new Color(1, 0.15f, 0.15f, 0.35f),
                _ => new Color(0.4f, 0.4f, 0.4f, 0.2f)
            };
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.5f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;

            // Label
            var padBox = GetComponent<DockingPadTriggerBox>();
            if (padBox != null)
            {
                var style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.fontSize = 16;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;
                Vector3 labelPos = transform.position + Vector3.up * 5f;
                UnityEditor.Handles.Label(labelPos, padBox.PadId, style);
            }
        }
#endif
    }
}
