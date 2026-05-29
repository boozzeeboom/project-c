using UnityEngine;
using UnityEditor;

namespace ProjectC.Core.Editor
{
    /// <summary>
    /// Editor-only script to test star field creation in Edit Mode.
    /// Attach to ConstellationController and click button to initialize stars.
    /// </summary>
    [ExecuteInEditMode]
    public class StarFieldTest : MonoBehaviour
    {
        [ContextMenu("Initialize Star Field")]
        public void InitializeStarField()
        {
            var cc = GetComponent<ProjectC.Core.ConstellationController>();
            if (cc != null)
            {
                Debug.Log("[StarFieldTest] Initializing star field...");
                
                // Destroy existing sky dome if any
                var existing = transform.Find("SkyDome_Stars");
                if (existing != null)
                {
                    Debug.Log("[StarFieldTest] Destroying existing SkyDome_Stars");
                    DestroyImmediate(existing.gameObject);
                }
                
                // Call BuildSkyDome manually
                var method = cc.GetType().GetMethod("BuildSkyDome", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(cc, null);
                    Debug.Log("[StarFieldTest] BuildSkyDome called successfully!");
                }
                else
                {
                    Debug.LogError("[StarFieldTest] BuildSkyDome method not found!");
                }
            }
            else
            {
                Debug.LogError("[StarFieldTest] ConstellationController not found on this GameObject!");
            }
        }

        [ContextMenu("Force Star Visibility")]
        public void ForceVisibility()
        {
            var cc = GetComponent<ProjectC.Core.ConstellationController>();
            if (cc != null)
            {
                var method = cc.GetType().GetMethod("SetStarVisibility", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(cc, new object[] { 1f });
                    Debug.Log("[StarFieldTest] SetStarVisibility(1f) called!");
                }
            }
        }
    }
}
