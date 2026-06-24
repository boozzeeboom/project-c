// T-NS M3.1.3: FixM3SceneAndSchedule — фикс 4 багов M2 (см. M2_FSM_DIAGNOSIS.md §4).
//
// 1. TESTZONENPC.SphereCollider.isTrigger = true
//    (OuterCommZone.Awake не может включить — scene override возвращает 0)
//
// 2. TESTZONENPC.OuterCommZone.commRange 242.3 → 600
//    (NPC пролетает 242м при 24 м/с за 10 сек)
//
// 3. Добавить 2 DockingPadTriggerBox в TESTZONENPC
//    (без них NPC не может сесть — AssignPadForNpc находит 0 trigger-box'ов)
//
// 4. Синхронизировать NpcShipSchedule_Courier.routes[0].toLocationId
//    "PRIMIUM_TEST_ZONE_2" → "PRIMIUM_TEST_ZONE"
//    (DockStationDefinition_TestZone.locationId = "PRIMIUM_TEST_ZONE")
//
// Также: переименовываем TESTZONENPC → DockStation_TestZone (consistency с DockStation_Primium).

using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEditor;
using ProjectC.Docking.Network;
using ProjectC.Docking.Zones;
using ProjectC.Docking.Stations;
using ProjectC.Docking.Core;
using ProjectC.PeacefulShip.Stations;

public static class FixM3SceneAndSchedule
{
    [MenuItem("Tools/ProjectC/PeacefulShip/M3.1 Fix Scene + Schedule")]
    public static void Fix()
    {
        // === Step 1: Load scene ===
        var scene = SceneManager.GetSceneByName("WorldScene_0_0");
        if (!scene.isLoaded)
        {
            Debug.LogError("[FixM3] WorldScene_0_0 not loaded");
            return;
        }
        SceneManager.SetActiveScene(scene);

        // === Step 2: Find TESTZONENPC root (current name) ===
        var root = GameObject.Find("TESTZONENPC");
        if (root == null)
        {
            Debug.LogError("[FixM3] TESTZONENPC not found in scene");
            return;
        }

        // === Step 3: SphereCollider.isTrigger = true ===
        var sphereCol = root.GetComponent<SphereCollider>();
        if (sphereCol == null)
        {
            Debug.LogError("[FixM3] TESTZONENPC has no SphereCollider");
            return;
        }
        bool triggerBefore = sphereCol.isTrigger;
        sphereCol.isTrigger = true;
        Debug.Log($"[FixM3] SphereCollider.isTrigger: {triggerBefore} → true");

        // === Step 4: OuterCommZone.commRange 242.3 → 600 ===
        var commZone = root.GetComponent<OuterCommZone>();
        if (commZone == null)
        {
            Debug.LogError("[FixM3] TESTZONENPC has no OuterCommZone");
            return;
        }
        var commSo = new SerializedObject(commZone);
        var commRangeProp = commSo.FindProperty("commRange");
        float commBefore = commRangeProp.floatValue;
        commRangeProp.floatValue = 600f;
        commSo.ApplyModifiedProperties();
        Debug.Log($"[FixM3] OuterCommZone.commRange: {commBefore} → 600");

        // === Step 5: Add 2 DockingPadTriggerBox children ===
        // Skip if already present
        var existingPads = root.GetComponentsInChildren<DockingPadTriggerBox>(true);
        if (existingPads.Length >= 2)
        {
            Debug.Log($"[FixM3] Already has {existingPads.Length} DockingPadTriggerBox children — skipping add");
        }
        else
        {
            // Add 2 pads
            for (int i = 0; i < 2; i++)
            {
                var padGo = new GameObject($"TST-PAD-00{i + 1}");
                padGo.transform.SetParent(root.transform, false);
                padGo.transform.localPosition = new Vector3(i * 8f - 4f, 0f, 0f);

                var box = padGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(12f, 5f, 12f);

                var pad = padGo.AddComponent<DockingPadTriggerBox>();
                var padSo = new SerializedObject(pad);
                padSo.FindProperty("padId").stringValue = $"TST-PAD-00{i + 1}";
                var cc = padSo.FindProperty("compatibleShipClasses");
                cc.arraySize = 1;
                cc.GetArrayElementAtIndex(0).enumValueIndex = 3; // HeavyII
                padSo.ApplyModifiedProperties();

                padGo.AddComponent<DockPadVisualMarker>();
                Debug.Log($"[FixM3] Added pad child TST-PAD-00{i + 1}");
            }
        }

        // === Step 6: Fix NpcShipSchedule_Courier.routes[0].toLocationId ===
        var schedule = AssetDatabase.LoadAssetAtPath<NpcShipSchedule>(
            "Assets/_Project/Resources/PeacefulShip/NpcShipSchedule_Courier.asset");
        if (schedule == null)
        {
            Debug.LogError("[FixM3] NpcShipSchedule_Courier.asset not found");
            return;
        }
        var schedSo = new SerializedObject(schedule);
        var routesProp = schedSo.FindProperty("routes");
        if (routesProp.arraySize > 0)
        {
            var route0 = routesProp.GetArrayElementAtIndex(0);
            var toLocProp = route0.FindPropertyRelative("toLocationId");
            string toBefore = toLocProp.stringValue;
            if (toBefore != "PRIMIUM_TEST_ZONE")
            {
                toLocProp.stringValue = "PRIMIUM_TEST_ZONE";
                schedSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(schedule);
                Debug.Log($"[FixM3] NpcShipSchedule_Courier.routes[0].toLocationId: '{toBefore}' → 'PRIMIUM_TEST_ZONE'");
            }
            else
            {
                Debug.Log("[FixM3] NpcShipSchedule_Courier.routes[0].toLocationId already 'PRIMIUM_TEST_ZONE'");
            }
        }

        // === Step 7: Rename TESTZONENPC → DockStation_TestZone (consistency) ===
        if (root.name != "DockStation_TestZone")
        {
            root.name = "DockStation_TestZone";
            Debug.Log("[FixM3] Renamed TESTZONENPC → DockStation_TestZone");
        }

        // === Step 8: Save ===
        EditorSceneManagerSaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FixM3] ✅ Scene + Schedule fixes applied. Save complete.");
    }

    private static void EditorSceneManagerSaveScene(Scene scene)
    {
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
#endif
    }
}
