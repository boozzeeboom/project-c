#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ProjectC.Docking.Core;

namespace ProjectC.Docking.Network
{
    /// <summary>
    /// DockStationControllerEditor — встраивает содержимое DockStationDefinition
    /// прямо в инспектор, с кнопкой Duplicate для клонирования ассета.
    /// Geometry (PlatformCenter, PlatformAltitude) — read-only из transform.position.
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
                _defSo = new SerializedObject(def);
        }

        public override void OnInspectorGUI()
        {
            var ctrl = (DockStationController)target;
            serializedObject.Update();

            // === Geometry: read-only from transform ===
            EditorGUILayout.LabelField("Geometry (from Transform)", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Vector3Field("Platform Center", ctrl.transform.position);
            EditorGUILayout.FloatField("Platform Altitude", ctrl.transform.position.y);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(4);

            // === Definition field ===
            var defProp = serializedObject.FindProperty("dockStationDefinition");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(defProp, new GUIContent("Dock Station Definition"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                var def = ctrl.StationDefinition;
                _defSo = def != null ? new SerializedObject(def) : null;
            }

            if (defProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "No DockStationDefinition assigned.\n" +
                    "Create: Assets → Create → ProjectC → Docking → DockStationDefinition\n" +
                    "Or drop an existing .asset here.",
                    MessageType.Warning);

                DrawDebugField();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // === Duplicate button ===
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📋 Duplicate Definition", GUILayout.Height(22)))
            {
                DuplicateDefinition(ctrl);
                var newDef = ctrl.StationDefinition;
                _defSo = newDef != null ? new SerializedObject(newDef) : null;
            }
            GUI.enabled = false;
            GUILayout.Button("🔗 " + ctrl.StationDefinition.name, GUILayout.Height(22));
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

                var prop = _defSo.GetIterator();
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (prop.name == "m_Script") continue;
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
            DrawDebugField();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDebugField()
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

            string defaultName = srcName + "_Copy";
            string savePath = EditorUtility.SaveFilePanelInProject(
                "Duplicate DockStationDefinition",
                defaultName,
                "asset",
                "Choose save location for the duplicated definition",
                srcDir);

            if (string.IsNullOrEmpty(savePath)) return;

            var srcSo = new SerializedObject(src);
            var dst = CreateInstance<DockStationDefinition>();
            var dstSo = new SerializedObject(dst);

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

            // Clear identity so user sets new values
            var stId = dstSo.FindProperty("stationId");
            if (stId != null) stId.stringValue = "";
            var locId = dstSo.FindProperty("locationId");
            if (locId != null) locId.stringValue = "";
            var disp = dstSo.FindProperty("displayName");
            if (disp != null) disp.stringValue = "";

            dstSo.ApplyModifiedProperties();

            string finalPath = AssetDatabase.GenerateUniqueAssetPath(savePath);
            AssetDatabase.CreateAsset(dst, finalPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

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
