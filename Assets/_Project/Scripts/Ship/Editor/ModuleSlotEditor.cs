// =====================================================================================
// ModuleSlotEditor.cs — редакторский превью визуала модуля в Edit Mode
// =====================================================================================
// Позволяет дизайнеру:
//   1. Перетащить ShipModule SO в поле «Preview Module»
//   2. Нажать «▶ Preview» — visualPrefab спавнится как child слота с офсетами
//   3. Менять офсеты в SO → нажать «↻ Refresh» — визуал обновляется на месте
//   4. Нажать «✕ Clear» — превью удаляется
//
// Превью не сохраняется в сцену (HideFlags.DontSave).
// =====================================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectC.Ship
{
    [CustomEditor(typeof(ModuleSlot))]
    public class ModuleSlotEditor : UnityEditor.Editor
    {
        private ShipModule _previewModule;
        private GameObject _previewInstance;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(10);

            var slot = (ModuleSlot)target;

            EditorGUILayout.LabelField("👁 Module Visual Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag a ShipModule SO here to preview its visualPrefab at this slot.\n" +
                "Adjust offsets in the SO → click Refresh to see changes.\n" +
                "Preview is NOT saved to scene.",
                MessageType.Info);

            // --- Preview module field ---
            _previewModule = (ShipModule)EditorGUILayout.ObjectField(
                "Preview Module",
                _previewModule,
                typeof(ShipModule),
                allowSceneObjects: false);

            // --- Buttons row ---
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _previewModule != null && _previewModule.visualPrefab != null;
            if (GUILayout.Button("▶ Preview", GUILayout.Height(28)))
            {
                ClearPreview();
                SpawnPreview(slot, _previewModule);
            }

            GUI.enabled = _previewInstance != null;
            if (GUILayout.Button("↻ Refresh", GUILayout.Height(28)))
            {
                ClearPreview();
                if (_previewModule != null && _previewModule.visualPrefab != null)
                    SpawnPreview(slot, _previewModule);
            }

            if (GUILayout.Button("✕ Clear", GUILayout.Height(28)))
            {
                ClearPreview();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // --- Status ---
            if (_previewInstance != null)
            {
                EditorGUILayout.HelpBox(
                    $"Preview active: '{_previewInstance.name}' at slot '{slot.gameObject.name}'",
                    MessageType.None);
            }

            // --- Offset quick-edit (only when preview module is set) ---
            if (_previewModule != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Quick Offsets (edits the SO directly)", EditorStyles.miniBoldLabel);

                EditorGUI.BeginChangeCheck();

                var pos = EditorGUILayout.Vector3Field("Position Offset", _previewModule.attachPositionOffset);
                var rot = EditorGUILayout.Vector3Field("Rotation Offset", _previewModule.attachRotationOffset);
                var scl = EditorGUILayout.Vector3Field("Scale", _previewModule.attachScale);
                var axis = (ModuleAttachAxis)EditorGUILayout.EnumPopup("Attach Axis", _previewModule.attachAxis);
                var socket = EditorGUILayout.TextField("Socket Path", _previewModule.visualSocketPath);
                var colMode = (ModuleColliderMode)EditorGUILayout.EnumPopup("Collider Mode", _previewModule.colliderMode);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_previewModule, "Edit Module Visual Offsets");
                    _previewModule.attachPositionOffset = pos;
                    _previewModule.attachRotationOffset = rot;
                    _previewModule.attachScale = scl;
                    _previewModule.attachAxis = axis;
                    _previewModule.visualSocketPath = socket;
                    _previewModule.colliderMode = colMode;
                    EditorUtility.SetDirty(_previewModule);

                    // Авто-обновление превью при изменении полей
                    if (_previewInstance != null)
                    {
                        ClearPreview();
                        if (_previewModule.visualPrefab != null)
                            SpawnPreview(slot, _previewModule);
                    }
                }
            }
        }

        private void SpawnPreview(ModuleSlot slot, ShipModule module)
        {
            if (module.visualPrefab == null) return;

            // Найти родителя: слот или socket
            Transform parent = slot.transform;
            if (!string.IsNullOrEmpty(module.visualSocketPath))
            {
                var socket = parent.Find(module.visualSocketPath);
                if (socket != null) parent = socket;
            }

            _previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(module.visualPrefab, parent);
            _previewInstance.hideFlags = HideFlags.DontSave;

            _previewInstance.transform.localPosition = module.attachPositionOffset;
            _previewInstance.transform.localEulerAngles = module.attachRotationOffset;
            _previewInstance.transform.localScale = module.attachScale;

            // Attach axis
            if (module.attachAxis != ModuleAttachAxis.Slot)
            {
                var baseRot = Quaternion.Euler(module.attachRotationOffset);
                Quaternion axisRot = module.attachAxis switch
                {
                    ModuleAttachAxis.ShipForward => baseRot * Quaternion.LookRotation(parent.root.forward),
                    ModuleAttachAxis.ShipDown    => baseRot * Quaternion.LookRotation(-parent.root.up),
                    ModuleAttachAxis.WorldUp     => baseRot * Quaternion.LookRotation(Vector3.up),
                    _                            => baseRot,
                };
                _previewInstance.transform.localRotation = axisRot;
            }

            // Colliders
            switch (module.colliderMode)
            {
                case ModuleColliderMode.None:
                    foreach (var col in _previewInstance.GetComponentsInChildren<Collider>(true))
                        col.enabled = false;
                    break;
                case ModuleColliderMode.Trigger:
                    foreach (var col in _previewInstance.GetComponentsInChildren<Collider>(true))
                    { col.enabled = true; col.isTrigger = true; }
                    break;
                case ModuleColliderMode.Solid:
                    foreach (var col in _previewInstance.GetComponentsInChildren<Collider>(true))
                    { col.enabled = true; col.isTrigger = false; }
                    break;
            }

            _previewInstance.name = $"[PREVIEW] {module.displayName}";
            EditorGUIUtility.PingObject(_previewInstance);
            SceneView.RepaintAll();
        }

        private void ClearPreview()
        {
            if (_previewInstance != null)
            {
                DestroyImmediate(_previewInstance);
                _previewInstance = null;
                SceneView.RepaintAll();
            }
        }

        private void OnDisable()
        {
            ClearPreview();
        }

        private void OnDestroy()
        {
            ClearPreview();
        }
    }
}
#endif
