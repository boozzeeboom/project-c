// StormCellDirectorEditor.cs — T-CLOUD39
// Custom inspector with Regenerate, Respawn, Save/Load Defaults buttons.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectC.World.Clouds
{
    [CustomEditor(typeof(StormCellDirector))]
    public class StormCellDirectorEditor : UnityEditor.Editor
    {
        private bool _showDefaultsInfo;

        private bool HasSavedDefaults()
        {
            return EditorPrefs.HasKey("StormCellDirector.StormDensityMultiplier");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var director = (StormCellDirector)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            // ── Row 1: Regenerate + Respawn ──
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("⟳ Regenerate Storm", GUILayout.Height(28)))
            {
                director.ForceRegenerateStorm();
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.5f, 0.9f, 0.5f);
            if (GUILayout.Button("⛈ Respawn Test Cells", GUILayout.Height(28)))
            {
                if (Application.isPlaying)
                {
                    var cells = director.GetCells();
                    int count = cells.Count;
                    for (int i = count - 1; i >= 0; i--)
                        director.RemoveCell(i);

                    var method = typeof(StormCellDirector).GetMethod(
                        "SpawnTestCellsAroundPosition",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var playerPos = typeof(StormCellDirector).GetMethod(
                        "GetPlayerPosition",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (method != null && playerPos != null)
                    {
                        Vector3 pos = (Vector3)playerPos.Invoke(null, null);
                        method.Invoke(director, new object[] { pos });
                        director.ForceRegenerateStorm();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Not in Play Mode",
                        "Respawn Test Cells works only in Play Mode.", "OK");
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // ── Row 2: Save + Load Defaults ──
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.9f, 0.8f, 0.4f);
            if (GUILayout.Button("💾 Save as Defaults", GUILayout.Height(28)))
            {
                director.SaveToEditorPrefs();
                EditorUtility.DisplayDialog("Defaults Saved",
                    "Current values saved.\nThey will auto-load on next Play Mode.\n" +
                    "Click 'Apply to Scene' to persist in Edit Mode.",
                    "OK");
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.7f, 0.8f, 0.95f);
            if (GUILayout.Button("📂 Apply to Scene", GUILayout.Height(28)))
            {
                if (HasSavedDefaults())
                {
                    // Load from EditorPrefs and apply to serialized object (persists in scene)
                    director.LoadFromEditorPrefs();
                    EditorUtility.SetDirty(director);
                    Undo.RecordObject(director, "Apply Storm Defaults");
                    Debug.Log("[StormCellDirector] 📂 Applied saved defaults to scene.");
                }
                else
                {
                    EditorUtility.DisplayDialog("No Saved Defaults",
                        "Save some defaults first (in Play Mode → tweak → Save as Defaults).",
                        "OK");
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // ── Status line ──
            string status = HasSavedDefaults()
                ? "✅ Saved defaults exist — will auto-load on Play Mode."
                : "❌ No saved defaults yet. Tweak in Play Mode → Save.";
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Workflow:\n" +
                "1. Play Mode: Respawn → tweak + Regenerate until happy\n" +
                "2. Click 'Save as Defaults' while still in Play Mode\n" +
                "3. Stop Play Mode → defaults persist in EditorPrefs\n" +
                "4. Next Play Mode: values auto-restored\n" +
                "5. (Optional) 'Apply to Scene' — writes to scene file\n\n" +
                "Form tips:\n" +
                "• Octaves=1: most organic, less layering\n" +
                "• NoiseScale 200–600: tighter clusters\n" +
                "• Contrast 0.15–0.3: softer edges\n" +
                "• VerticalPeak randomizes on each Respawn",
                MessageType.Info);

            // Auto-regenerate on inspector change during play mode
            if (GUI.changed && Application.isPlaying)
            {
                director.ForceRegenerateStorm();
            }
        }
    }
}
#endif
