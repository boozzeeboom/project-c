// Project C: Character Customisation — T-CUS-05
// SetupCharacterCustomisationApplier: добавляет CharacterCustomisationApplier
// компонент на NetworkPlayer.prefab (если ещё нет), сохраняет, и назначает Inspector refs:
//   _visualRoot      = Visual_Model child
//   _animator        = Animator на Visual_Model
//   _bodyRenderer    = первый SkinnedMeshRenderer на Visual_Model
//   _maleMesh        = HumanM_Model.sharedMesh (default)
//   _femaleMesh      = HumanF_Model.sharedMesh
//   _maleController  = PlayerAnimation_Default.overrideController
//   _femaleController = PlayerAnimation_Female.overrideController
//
// Запуск: Tools → ProjectC → Player → Add CharacterCustomisationApplier to NetworkPlayer.
//
// Idempotent — повторный запуск пере-присваивает refs (idempotent assignment).

using UnityEditor;
using UnityEngine;
using ProjectC.Player;  // typeof(CharacterCustomisationApplier) вместо reflection — обход cache-lag.

namespace ProjectC.Editor
{
    public static class SetupCharacterCustomisationApplier
    {
        private const string PrefabPath            = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
        // typeof использует compile-time resolution — не падает на reflection-lag при ExecuteMenuItem.

        private const string MaleModelPath         = "Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx";
        private const string FemaleModelPath       = "Assets/Kevin Iglesias/Human Animations/Models/HumanF_Model.fbx";
        private const string MaleControllerPath    = "Assets/_Project/Animations/PlayerAnimation_Default.overrideController";
        private const string FemaleControllerPath  = "Assets/_Project/Animations/PlayerAnimation_Female.overrideController";

        [MenuItem("Tools/ProjectC/Player/Add CharacterCustomisationApplier to NetworkPlayer")]
        public static void AddComponent()
        {
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (contents == null)
            {
                Debug.LogError("[SetupCCA] Failed to load prefab contents: " + PrefabPath);
                return;
            }

            try
            {
                var existing = contents.GetComponent<CharacterCustomisationApplier>();
                if (existing == null)
                {
                    existing = contents.AddComponent<CharacterCustomisationApplier>();
                    Debug.Log("[SetupCCA] Added CharacterCustomisationApplier to NetworkPlayer.prefab.");
                }
                else
                {
                    Debug.Log("[SetupCCA] CharacterCustomisationApplier already present, re-assigning refs.");
                }

                AssignRefs(existing);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[SetupCCA] Prefab saved with assigned refs.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void AssignRefs(Component comp)
        {
            var so = new SerializedObject(comp);
            var visualModel = comp.transform.Find("Visual_Model");
            if (visualModel == null)
            {
                Debug.LogWarning("[SetupCCA] 'Visual_Model' child not found on root.");
                return;
            }

            // _visualRoot = Visual_Model
            var pVisualRoot = so.FindProperty("_visualRoot");
            if (pVisualRoot != null) pVisualRoot.objectReferenceValue = visualModel;

            // _animator = Animator с непустым runtimeAnimatorController
            var animators = visualModel.GetComponentsInChildren<Animator>(true);
            Animator chosenAnimator = null;
            foreach (var a in animators)
            {
                if (a != null && a.runtimeAnimatorController != null) { chosenAnimator = a; break; }
            }
            if (chosenAnimator != null)
            {
                var pAnimator = so.FindProperty("_animator");
                if (pAnimator != null) pAnimator.objectReferenceValue = chosenAnimator;
            }
            else
            {
                Debug.LogWarning("[SetupCCA] No Animator with non-empty runtimeAnimatorController on Visual_Model.");
            }

            // _bodyRenderer = первый SkinnedMeshRenderer
            var smr = visualModel.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                var pBodyRenderer = so.FindProperty("_bodyRenderer");
                if (pBodyRenderer != null) pBodyRenderer.objectReferenceValue = smr;
            }
            else
            {
                Debug.LogWarning("[SetupCCA] No SkinnedMeshRenderer on Visual_Model.");
            }

            // _maleMesh / _femaleMesh
            var maleModel = AssetDatabase.LoadAssetAtPath<GameObject>(MaleModelPath);
            if (maleModel != null)
            {
                var maleSmr = maleModel.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (maleSmr != null && maleSmr.sharedMesh != null)
                {
                    var pMaleMesh = so.FindProperty("_maleMesh");
                    if (pMaleMesh != null) pMaleMesh.objectReferenceValue = maleSmr.sharedMesh;
                }
            }

            var femaleModel = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleModelPath);
            if (femaleModel != null)
            {
                var femaleSmr = femaleModel.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (femaleSmr != null && femaleSmr.sharedMesh != null)
                {
                    var pFemaleMesh = so.FindProperty("_femaleMesh");
                    if (pFemaleMesh != null) pFemaleMesh.objectReferenceValue = femaleSmr.sharedMesh;
                }
            }

            // _maleController / _femaleController
            var maleCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MaleControllerPath);
            if (maleCtrl != null)
            {
                var pMaleCtrl = so.FindProperty("_maleController");
                if (pMaleCtrl != null) pMaleCtrl.objectReferenceValue = maleCtrl;
            }
            else
            {
                Debug.LogWarning("[SetupCCA] MaleController not found: " + MaleControllerPath);
            }

            var femaleCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FemaleControllerPath);
            if (femaleCtrl != null)
            {
                var pFemaleCtrl = so.FindProperty("_femaleController");
                if (pFemaleCtrl != null) pFemaleCtrl.objectReferenceValue = femaleCtrl;
            }
            else
            {
                Debug.LogWarning("[SetupCCA] FemaleController not found: " + FemaleControllerPath + " — run SetupFemaleAnimationOverride first.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}