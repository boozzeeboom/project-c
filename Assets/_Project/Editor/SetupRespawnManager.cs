using UnityEditor;
using UnityEngine;
using ProjectC.World;
using ProjectC.Player;

namespace ProjectC.Editor
{
    public static class SetupRespawnManager
    {
        [MenuItem("ProjectC/World/Setup RespawnManager")]
        public static void Execute()
        {
            var existing = Object.FindAnyObjectByType<RespawnManager>();
            if (existing != null)
            {
                // Already exists — just update fallback
                var so = new SerializedObject(existing);
                var points = so.FindProperty("_respawnPoints");

                if (points.arraySize > 0)
                {
                    var elem = points.GetArrayElementAtIndex(0);
                    elem.FindPropertyRelative("fallbackPosition").vector3Value =
                        new Vector3(39992f, 2502.77f, 40000f);
                    so.ApplyModifiedProperties();
                    Debug.Log("[SetupRespawnManager] Updated fallback position to (39992, 2502.77, 40000)");
                }
                else
                {
                    points.arraySize = 1;
                    var elem = points.GetArrayElementAtIndex(0);
                    elem.FindPropertyRelative("fallbackPosition").vector3Value =
                        new Vector3(39992f, 2502.77f, 40000f);
                    so.ApplyModifiedProperties();
                    Debug.Log("[SetupRespawnManager] Added fallback point (39992, 2502.77, 40000)");
                }

                EditorUtility.SetDirty(existing);
                return;
            }

            // Create new
            var go = new GameObject("RespawnManager");
            go.transform.position = Vector3.zero;

            var rm = go.AddComponent<RespawnManager>();
            var so2 = new SerializedObject(rm);
            var pts = so2.FindProperty("_respawnPoints");
            pts.arraySize = 1;
            var el = pts.GetArrayElementAtIndex(0);
            el.FindPropertyRelative("fallbackPosition").vector3Value =
                new Vector3(39992f, 2502.77f, 40000f);
            so2.ApplyModifiedProperties();

            EditorUtility.SetDirty(rm);

            Debug.Log("[SetupRespawnManager] Created RespawnManager with fallback (39992, 2502.77, 40000)");
        }
    }
}
