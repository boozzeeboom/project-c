// T-DOCK-13: DockPadVisualMarker — визуальный маркер pad'а (Q13).
// Работает в runtime: меняет цвет Quad-метки на паде (зелёный/красный).
// TMP label с номером — отдельный child, создаётся вручную на сцене.
//
// Логика:
//   • IsFree (зелёный) = _padBox.IsShipInside == false
//   • IsOccupied (красный) = _padBox.IsShipInside == true
//
// Свойство visible управляется из Inspector (можно скрывать все маркеры разом).

using ProjectC.Docking.Network; // DockStationController
using ProjectC.Player;          // ShipController
using UnityEngine;

namespace ProjectC.Docking.Stations
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(DockingPadTriggerBox))]
    public class DockPadVisualMarker : MonoBehaviour
    {
        [Header("Цвета (меняются в runtime)")]
        [SerializeField] private Color freeColor = new Color(0.0f, 1.0f, 0.2f, 0.6f);
        [SerializeField] private Color occupiedColor = new Color(1.0f, 0.1f, 0.1f, 0.8f);

        [Header("Метка")]
        [SerializeField] private float markerSize = 6f;        // размер Quad
        [SerializeField] private float markerHeight = 0.05f;   // над поверхностью (z-fighting)

        // Runtime
        private DockingPadTriggerBox _padBox;
        private BoxCollider _box;
        private GameObject _markerGO;
        private MeshRenderer _markerRenderer;
        private Material _markerMat;
        private Color _currentColor;
        private bool _isFree = true;
        private bool _initialized = false;

        private void Awake()
        {
            _padBox = GetComponent<DockingPadTriggerBox>();
            _box = GetComponent<BoxCollider>();
            BuildMarker();
            _initialized = true;
        }

        private void OnDestroy()
        {
            if (_markerMat != null)
            {
                if (Application.isPlaying) Destroy(_markerMat);
                else DestroyImmediate(_markerMat);
            }
        }

        private void BuildMarker()
        {
            // Создаём дочерний Quad строго над BoxCollider
            _markerGO = new GameObject("_PadMarker");
            _markerGO.transform.SetParent(transform, false);
            _markerGO.transform.localPosition = _box != null
                ? _box.center + Vector3.up * (_box.size.y * 0.5f + markerHeight)
                : Vector3.up * markerHeight;
            // T-DOCK-13: Quad по умолчанию стоит вертикально (normal = Z).
            // Поворачиваем на 90° по X — плашмя на pad`е.
            _markerGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _markerGO.transform.localScale = new Vector3(markerSize, markerSize, 1f);

            var meshFilter = _markerGO.AddComponent<MeshFilter>();
            var quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (quadMesh == null) quadMesh = Resources.GetBuiltinResource<Mesh>("Quad");
            meshFilter.sharedMesh = quadMesh;

            _markerRenderer = _markerGO.AddComponent<MeshRenderer>();
            _markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _markerRenderer.receiveShadows = false;

            // Unlit/Color — стандартный shader (есть во всех проектах)
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            _markerMat = new Material(shader);
            if (_markerMat.HasProperty("_Color")) _markerMat.SetColor("_Color", freeColor);
            else if (_markerMat.HasProperty("_BaseColor")) _markerMat.SetColor("_BaseColor", freeColor);
            _markerRenderer.sharedMaterial = _markerMat;

            UpdateColor(freeColor);
        }

        private void UpdateColor(Color color)
        {
            if (_markerMat == null) return;
            _currentColor = color;
            if (_markerMat.HasProperty("_Color")) _markerMat.SetColor("_Color", color);
            if (_markerMat.HasProperty("_BaseColor")) _markerMat.SetColor("_BaseColor", color);
        }

        private void Update()
        {
            if (!_initialized) return;

            // Throttle: проверяем раз в 0.5 сек
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = 0.5f;

            // T-DOCK-13 v2: проверяем занятость через Physics.OverlapSphere напрямую.
            // Это надёжнее чем _padBox.IsShipInside (триггер-бокс может не сработать
            // для кораблей, уже стоящих на паде при старте сцены).
            // Ищем любой ShipController в радиусе pad'а.
            Vector3 center = transform.position;
            float radius = 10f;
            bool occupied = false;
            Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponentInParent<ShipController>() != null)
                {
                    occupied = true;
                    break;
                }
            }

            if (occupied != _isFree)
            {
                _isFree = !occupied;
                UpdateColor(occupied ? occupiedColor : freeColor);
            }
        }

        private float _checkTimer;

#if UNITY_EDITOR
        // Отрисовка в Editor Gizmos (для превью сцены)
        private void OnDrawGizmos()
        {
            var box = _box ?? GetComponent<BoxCollider>();
            var padBox = _padBox ?? GetComponent<DockingPadTriggerBox>();
            if (box != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Color gizmoColor = _isFree ? freeColor : occupiedColor;
                Gizmos.color = gizmoColor;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.3f);
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            // Label — если в Editor и нет _PadMarker
            if (padBox != null)
            {
                var style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.fontSize = 18;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;
                Vector3 labelPos = transform.position + Vector3.up * 5f;
                UnityEditor.Handles.Label(labelPos, padBox.PadId, style);
            }
        }
#endif
    }
}