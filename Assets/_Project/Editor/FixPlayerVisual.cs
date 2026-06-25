using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectC.Editor
{
    /// <summary>
    /// R2-NONE: Fix two issues with NetworkPlayer visual:
    /// 1) Assign idle animation clip to the Idle state
    /// 2) Lift the model so feet align with CharacterController bottom
    /// </summary>
    public static class FixPlayerVisual
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
        private const string CtrlPath = "Assets/_Project/Animations/PlayerAnimation.controller";
        private const string IdleClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle01.fbx";

        [MenuItem("Tools/ProjectC/Player/Fix NetworkPlayer Visual")]
        public static void Fix()
        {
            // 1. Fix Idle animation
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (controller != null)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
                if (clip != null)
                {
                    foreach (var layer in controller.layers)
                    {
                        foreach (var state in layer.stateMachine.states)
                        {
                            if (state.state.name == "Idle")
                            {
                                state.state.motion = clip;
                                Debug.Log("[FixPlayerVisual] Assigned " + clip.name + " to Idle state");
                            }
                        }
                    }
                    EditorUtility.SetDirty(controller);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[FixPlayerVisual] AnimatorController saved");
                }
                else
                {
                    Debug.LogError("[FixPlayerVisual] Idle clip not found: " + IdleClipPath);
                }
            }

            // 2. Fix model Y offset
            var prefabContents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabContents == null) { Debug.LogError("[FixPlayerVisual] Failed to open prefab"); return; }

            try
            {
                var model = prefabContents.transform.Find("Visual_Model");
                if (model != null)
                {
                    // Lift the model up so feet align with CC bottom (y=0)
                    model.localPosition = new Vector3(0, 0.9f, 0);
                    Debug.Log("[FixPlayerVisual] Set Visual_Model.localPosition to (0, 0.9, 0)");
                }
                else
                {
                    Debug.LogError("[FixPlayerVisual] Visual_Model child not found");
                }

                PrefabUtility.SaveAsPrefabAsset(prefabContents, PrefabPath);
                Debug.Log("[FixPlayerVisual] Prefab saved successfully!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            AssetDatabase.Refresh();
        }
    }
}
