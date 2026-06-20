// T-DOCK-13: DockPadVisualMarker — визуальный маркер pad'а (Q13).
// Работает только в Editor (Gizmos). Навешивается на каждый Pad_* child.
// Отображает номер pad'а (padId) + цвет в зависимости от IsDocked состояния корабля.
// Phase 2: текстовое mesh-обозначение (через TextMeshPro) под Gizmos-условием.

using ProjectC.Docking.Network; // DockStationController
using ProjectC.Player;          // ShipController
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Docking.Stations
{
    /// <summary>
    /// Маркер для визуализации pad'ов в редакторе.
    /// Показывает padId зелёным (свободен) или красным (занят) в Gizmos.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class DockPadVisualMarker : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Color freeColor = new Color(0.2f, 1f, 0.2f, 0.25f);
        [SerializeField] private Color occupiedColor = new Color(1f, 0.2f, 0.2f, 0.35f);
        [SerializeField] private bool drawGizmos = true;

        // Кешированные ссылки
        private DockingPadTriggerBox _padBox;
        private BoxCollider _box;
        private DockStationController _station;

        private void Awake()
        {
            _padBox = GetComponent<DockingPadTriggerBox>();
            _box = GetComponent<BoxCollider>();
            _station = GetComponentInParent<DockStationController>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Pad ID label (всегда)
            var padBox = _padBox ?? GetComponent<DockingPadTriggerBox>();
            var box = _box ?? GetComponent<BoxCollider>();
            var station = _station ?? GetComponentInParent<DockStationController>();

            string label = padBox != null ? padBox.PadId : gameObject.name;

            // Цвет: занят/свободен
            // Простейшая проверка — если IsServer и есть cooldown — не пытаемся
            bool padOccupied = false;
            if (station != null && !string.IsNullOrEmpty(station.StationId))
            {
                // Editor-only: не обращаемся к DockingWorld так как она runtime-only.
                // Gizmos всегда зелёные в редакторе — runtime определяет DockingWorld.
            }

            Color gizmoColor = freeColor;

            // Wire box с цветом
            Gizmos.color = gizmoColor;

            // Рисуем box от BoxCollider
            if (box != null)
            {
                Matrix4x4 originalMatrix = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.4f);
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.matrix = originalMatrix;
            }

            // Label в центре бокса
            var style = new GUIStyle();
            style.normal.textColor = gizmoColor;
            style.fontSize = 18;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;

            Vector3 labelPos = transform.TransformPoint(box != null ? box.center : Vector3.zero);
            labelPos.y += 5f;
            UnityEditor.Handles.Label(labelPos, label, style);

            // Arrow pointing "approach direction"
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.5f);
            Vector3 arrowStart = labelPos;
            Vector3 arrowEnd = labelPos + transform.forward * 4f;
            Gizmos.DrawLine(arrowStart, arrowEnd);
            Gizmos.DrawSphere(arrowEnd, 0.3f);
        }
#endif
    }
}
