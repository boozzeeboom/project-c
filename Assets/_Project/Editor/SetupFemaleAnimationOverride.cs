// Project C: Character Customisation — T-CUS-04
// SetupFemaleAnimationOverride: создаёт PlayerAnimation_Female.overrideController
// путём обхода PlayerAnimation.controller и замены HumanM@* clip-ов на HumanF@*.
//
// Конвенция имён в Kevin Iglesias FREE pack:
//   /Male/.../HumanM@Foo.fbx → /Female/.../HumanF@Foo.fbx (тот же Foo)
//
// Запуск: Tools → ProjectC → Player → Setup Female Animation Override.

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectC.Editor
{
    public static class SetupFemaleAnimationOverride
    {
        private const string BaseControllerPath = "Assets/_Project/Animations/PlayerAnimation.controller";
        private const string OverridePath      = "Assets/_Project/Animations/PlayerAnimation_Female.overrideController";

        [MenuItem("Tools/ProjectC/Player/Setup Female Animation Override")]
        public static void Setup()
        {
            var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
            if (baseController == null)
            {
                Debug.LogError("[SetupFemale] Base controller not found: " + BaseControllerPath);
                return;
            }

            AnimatorOverrideController overrideCtrl = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
            if (overrideCtrl == null)
            {
                overrideCtrl = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(overrideCtrl, OverridePath);
                Debug.Log("[SetupFemale] Created new AnimatorOverrideController at " + OverridePath);
            }
            else
            {
                // Переинициализируем controller-ref, чтобы убедиться, что это override от правильного base.
                overrideCtrl.runtimeAnimatorController = baseController;
                Debug.Log("[SetupFemale] Reusing existing AnimatorOverrideController, re-linked to base.");
            }

            // Собрать все AnimationClip-ы из base controller (states + blend trees + sub-state-machines).
            var clipPairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            GatherClipsFromController(baseController, clipPairs);

            int swapped = 0, missing = 0;
            foreach (var kvp in clipPairs)
            {
                var maleClip = kvp.Key;
                if (maleClip == null) continue;

                string malePath = AssetDatabase.GetAssetPath(maleClip);
                if (string.IsNullOrEmpty(malePath)) continue;

                // Skip clips не из Kevin Iglesias (например, placeholder-ы).
                if (!malePath.Contains("/Male/") && !malePath.Contains("HumanM@"))
                {
                    continue;
                }

                // Конвенция: /Male/ → /Female/, HumanM@ → HumanF@
                string femalePath = malePath.Replace("/Male/", "/Female/").Replace("HumanM@", "HumanF@");
                var femaleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(femalePath);

                if (femaleClip != null)
                {
                    overrideCtrl[maleClip] = femaleClip;
                    swapped++;
                }
                else
                {
                    missing++;
                    Debug.LogWarning($"[SetupFemale] F-version not found for '{maleClip.name}' (expected at '{femalePath}'). Keeping male clip as fallback.");
                }
            }

            EditorUtility.SetDirty(overrideCtrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SetupFemale] DONE. Swapped={swapped}, missing={missing}. → {OverridePath}");
        }

        private static void GatherClipsFromController(AnimatorController ctrl, List<KeyValuePair<AnimationClip, AnimationClip>> clipPairs)
        {
            foreach (var layer in ctrl.layers)
            {
                if (layer.stateMachine != null)
                    GatherFromStateMachine(layer.stateMachine, clipPairs);
            }
        }

        private static void GatherFromStateMachine(AnimatorStateMachine sm, List<KeyValuePair<AnimationClip, AnimationClip>> clipPairs)
        {
            if (sm == null) return;
            foreach (var state in sm.states)
            {
                if (state.state == null) continue;
                if (state.state.motion is AnimationClip clip) clipPairs.Add(new(clip, null));
                else if (state.state.motion is BlendTree tree) GatherFromBlendTree(tree, clipPairs);
            }
            foreach (var sub in sm.stateMachines)
            {
                GatherFromStateMachine(sub.stateMachine, clipPairs);
            }
        }

        private static void GatherFromBlendTree(BlendTree tree, List<KeyValuePair<AnimationClip, AnimationClip>> clipPairs)
        {
            if (tree == null) return;
            foreach (var child in tree.children)
            {
                if (child.motion is AnimationClip c) clipPairs.Add(new(c, null));
            }
        }
    }
}