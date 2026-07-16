#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Docking.Core;

namespace ProjectC.Docking.Network
{
    /// <summary>
    /// DockStationControllerEditor — встраивает содержимое DockStationDefinition
    /// прямо в инспектор, с кнопкой Duplicate для клонирования ассета.
    /// </summary>
    [CustomEditor(typeof(DockStationController))]
    public class DockStationControllerEditor : UnityEditor.Editor
    {
        private SerializedObject _defSo;
        private bool _defFoldout = true;

        private void OnEnable()
        {
            var ctrl = (DockStationController)target;
            var def = ctrl.StationDefinition;
            if (def != null)
            {
                _defSo = new SerializedObject(def);
            }
        }

        private void OnDisable()
        {
        }

        public override void OnInspectorGUI()
        {
            var ctrl = (DockStationController)target;
            serializedObject.Update();

            // === Definition field ===
            var defProp = serializedObject.FindProperty("dockStationDefinition");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(defProp, new GUIContent("Dock Station Definition"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                // Refresh nested SO
                var def = ctrl.StationDefinition;
                if (def != null)
                    _defSo = new SerializedObject(def);
                else
                    _defSo = null;
            }

            if (defProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "No DockStationDefinition assigned.\n" +
                    "Create: Assets → Create → ProjectC → Docking → DockStationDefinition\n" +
                    "Or drop an existing .asset here.",
                    MessageType.Warning);

                DrawRemainingFields();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // === Duplicate button ===
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📋 Duplicate Definition", GUILayout.Height(22)))
            {
                DuplicateDefinition(ctrl);
                // Refresh after duplication
                var newDef = ctrl.StationDefinition;
                _defSo = newDef != null ? new SerializedObject(newDef) : null;
            }
            GUI.enabled = false;
            GUILayout.Button("🔗 " + (ctrl.StationDefinition != null ? ctrl.StationDefinition.name : ""), GUILayout.Height(22));
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // === Inline Definition ===
            _defFoldout = EditorGUILayout.Foldout(_defFoldout,
                $"📄 {ctrl.StationDefinition.name} (inline)", true, EditorStyles.foldoutHeader);

            if (_defFoldout && _defSo != null)
            {
                EditorGUI.indentLevel++;
                _defSo.Update();

                // Draw all properties inline
                var prop = _defSo.GetIterator();
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (prop.name == "m_Script") continue; // skip script reference
                    EditorGUILayout.PropertyField(prop, true);
                }

                EditorGUI.indentLevel--;
                if (_defSo.hasModifiedProperties)
                {
                    _defSo.ApplyModifiedProperties();
                    AssetDatabase.SaveAssetIfDirty(ctrl.StationDefinition);
                }
            }

            EditorGUILayout.Space(4);

            DrawRemainingFields();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRemainingFields()
        {
            var debugProp = serializedObject.FindProperty("debugMode");
            if (debugProp != null)
                EditorGUILayout.PropertyField(debugProp);
        }

        private void DuplicateDefinition(DockStationController ctrl)
        {
            var src = ctrl.StationDefinition;
            if (src == null)
            {
                EditorUtility.DisplayDialog("Duplicate", "Assign a DockStationDefinition first.", "OK");
                return;
            }

            string srcPath = AssetDatabase.GetAssetPath(src);
            string srcDir = System.IO.Path.GetDirectoryName(srcPath);
            string srcName = System.IO.Path.GetFileNameWithoutExtension(srcPath);

            // Determine save path
            string defaultName = srcName + "_Copy";
            string defaultPath = System.IO.Path.Combine(srcDir, defaultName + ".asset");
            string savePath = EditorUtility.SaveFilePanelInProject(
                "Duplicate DockStationDefinition",
                defaultName,
                "asset",
                "Choose save location for the duplicated definition",
                srcDir);

            if (string.IsNullOrEmpty(savePath)) return; // cancelled

            // Read source as SerializedObject
            var srcSo = new SerializedObject(src);
            var dst = CreateInstance<DockStationDefinition>();
            var dstSo = new SerializedObject(dst);

            // Copy all serialized properties
            var prop = srcSo.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script") continue;
                var dstProp = dstSo.FindProperty(prop.propertyPath);
                if (dstProp != null)
                    dstSo.CopyFromSerializedProperty(prop);
            }

            // Zero out identity fields so user sets them
            var stId = dstSo.FindProperty("stationId");
            if (stId != null) stId.stringValue = "";
            var locId = dstSo.FindProperty("locationId");
            if (locId != null) locId.stringValue = "";
            var disp = dstSo.FindProperty("displayName");
            if (disp != null) disp.stringValue = "";

            dstSo.ApplyModifiedProperties();

            // Create unique path
            string finalPath = AssetDatabase.GenerateUniqueAssetPath(savePath);
            AssetDatabase.CreateAsset(dst, finalPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Assign to controller
            var newDef = AssetDatabase.LoadAssetAtPath<DockStationDefinition>(finalPath);
            var ctrlSo = new SerializedObject(ctrl);
            ctrlSo.FindProperty("dockStationDefinition").objectReferenceValue = newDef;
            ctrlSo.ApplyModifiedProperties();

            EditorGUIUtility.PingObject(newDef);
            Debug.Log($"[DockStation] Duplicated: {srcPath} → {finalPath}");
        }
    }
}
#endif
