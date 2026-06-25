using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;

namespace ProjectC.Editor
{
    /// <summary>
    /// R2-NONE: Замена капсулы NetworkPlayer на HumanM_Model (Kevin Iglesias)
    /// + AnimatorController с базовыми анимациями (Idle, Walk, Run, Jump).
    /// </summary>
    public static class SetupPlayerVisual
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
        private const string ModelPath = "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
        private const string AnimDir = "Assets/_Project/Animations";
        private const string CtrlPath = "Assets/_Project/Animations/PlayerAnimation.controller";

        [MenuItem("Tools/ProjectC/Player/Setup NetworkPlayer Visual")]
        public static void Setup()
        {
            // 1. Load the prefab
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("[SetupPlayerVisual] Prefab not found: " + PrefabPath); return; }

            // 2. Open prefab in edit mode
            var prefabContents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabContents == null) { Debug.LogError("[SetupPlayerVisual] Failed to open prefab"); return; }

            try
            {
                // 3. Delete old "Visual" child
                var oldVisual = prefabContents.transform.Find("Visual");
                if (oldVisual != null)
                {
                    GameObject.DestroyImmediate(oldVisual.gameObject);
                    Debug.Log("[SetupPlayerVisual] Deleted old 'Visual' capsule child");
                }

                // 4. Add HumanM_Model as child
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (modelAsset == null) { Debug.LogError("[SetupPlayerVisual] Model not found: " + ModelPath); return; }

                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, prefabContents.transform);
                modelInstance.name = "Visual_Model";
                modelInstance.transform.SetParent(prefabContents.transform);
                modelInstance.transform.localPosition = new Vector3(0, 0, 0);
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                Debug.Log("[SetupPlayerVisual] Added HumanM_Model as 'Visual_Model' child");

                // 5. Create Animator Controller
                EnsureAnimDir();
                var controller = CreateOrGetController(prefabContents);
                if (controller == null) { Debug.LogError("[SetupPlayerVisual] Failed to create controller"); return; }

                // 6. Setup Animator on the model root
                var animator = modelInstance.GetComponent<Animator>();
                if (animator == null) animator = modelInstance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                Debug.Log("[SetupPlayerVisual] Animator assigned with controller: " + controller.name);

                // 7. Adjust CharacterController height to match human (~1.8m)
                var charCtrl = prefabContents.GetComponent<CharacterController>();
                if (charCtrl != null)
                {
                    charCtrl.height = 1.8f;
                    charCtrl.center = new Vector3(0, 0.9f, 0);
                    charCtrl.radius = 0.3f;
                    Debug.Log("[SetupPlayerVisual] CharacterController adjusted: height=1.8, center=(0,0.9,0)");
                }

                // 8. Save prefab
                PrefabUtility.SaveAsPrefabAsset(prefabContents, PrefabPath);
                Debug.Log("[SetupPlayerVisual] NetworkPlayer prefab saved successfully!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            AssetDatabase.Refresh();
        }

        private static void EnsureAnimDir()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Animations"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Animations");
                AssetDatabase.Refresh();
            }
        }

        private static AnimatorController CreateOrGetController(GameObject context)
        {
            // Check if controller already exists
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
            if (existing != null) return existing;

            // Create new controller
            var controller = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
            if (controller == null) return null;

            // Add parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            // Get the base layer
            var layer = controller.layers[0];
            var stateMachine = layer.stateMachine;

            // --- Idle state ---
            var idleState = stateMachine.AddState("Idle");
            idleState.motion = null; // T-pose / no animation

            // --- Walk state ---
            var walkClip = TryLoadClip(
                "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Walk/HumanM@Walk01_Forward.fbx",
                "Walk01_Forward");
            var walkState = stateMachine.AddState("Walk");
            walkState.motion = walkClip;

            // --- Run state ---
            var runClip = TryLoadClip(
                "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Run/HumanM@Run01_Forward.fbx",
                "Run01_Forward");
            var runState = stateMachine.AddState("Run");
            runState.motion = runClip;

            // --- Jump state ---
            var jumpClip = TryLoadClip(
                "Assets/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Jump01.fbx",
                "Jump01");
            var jumpState = stateMachine.AddState("Jump");
            jumpState.motion = jumpClip;

            // Set default state
            stateMachine.defaultState = idleState;

            // --- Transitions ---
            // Idle -> Walk (Speed > 0.1)
            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.1f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // Walk -> Idle (Speed < 0.1)
            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.1f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Walk -> Run (Speed > 5)
            var walkToRun = walkState.AddTransition(runState);
            walkToRun.hasExitTime = false;
            walkToRun.duration = 0.2f;
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 5f, "Speed");

            // Run -> Walk (Speed < 5)
            var runToWalk = runState.AddTransition(walkState);
            runToWalk.hasExitTime = false;
            runToWalk.duration = 0.2f;
            runToWalk.AddCondition(AnimatorConditionMode.Less, 5f, "Speed");

            // Any -> Jump (Jump trigger)
            var anyToJump = stateMachine.AddAnyStateTransition(jumpState);
            anyToJump.duration = 0.05f;
            anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            // Jump -> Idle (IsGrounded == true)
            var jumpToIdle = jumpState.AddTransition(idleState);
            jumpToIdle.hasExitTime = false;
            jumpToIdle.duration = 0.05f;
            jumpToIdle.AddCondition(AnimatorConditionMode.If, 1, "IsGrounded");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[SetupPlayerVisual] Created AnimatorController with Idle/Walk/Run/Jump states");
            return controller;
        }

        private static AnimationClip TryLoadClip(string fbxPath, string clipName)
        {
            // Try loading the specific clip from the FBX
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxPath);
            if (clip != null) return clip;

            // Try with @ naming convention (Unity uses name@clip convention)
            var clip2 = AssetDatabase.LoadAssetAtPath<AnimationClip>(fbxPath);
            if (clip2 != null) return clip2;

            // Fallback: load all sub-assets from the FBX and find by name
            var all = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
            foreach (var obj in all)
            {
                if (obj is AnimationClip ac && ac.name.Contains(clipName.Replace("HumanM@", "")))
                    return ac;
            }
            foreach (var obj in all)
            {
                if (obj is AnimationClip ac)
                    return ac; // first clip found
            }

            Debug.LogWarning("[SetupPlayerVisual] Clip not found: " + fbxPath);
            return null;
        }
    }
}
