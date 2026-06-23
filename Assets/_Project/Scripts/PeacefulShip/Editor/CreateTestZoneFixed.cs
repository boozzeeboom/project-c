using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEditor;
using ProjectC.Docking.Core;
using ProjectC.Docking.Network;
using ProjectC.Docking.Zones;
using ProjectC.Docking.Stations;

public static class CreateTestZoneFixed
{
    [MenuItem("Tools/ProjectC/PeacefulShip/Fix Test Zone")]
    public static void Create()
    {
        var scene = SceneManager.GetSceneByName("WorldScene_0_0");
        SceneManager.SetActiveScene(scene);

        // Delete old
        var old = GameObject.Find("DockStation_TestZone");
        if (old != null) GameObject.DestroyImmediate(old);

        var zonePos = new Vector3(41000f, 2500f, 41500f);

        // Load SOs
        var def = AssetDatabase.LoadAssetAtPath<DockStationDefinition>("Assets/_Project/Resources/PeacefulShip/DockStationDefinition_TestZone.asset");
        var layout = AssetDatabase.LoadAssetAtPath<DockPadLayout>("Assets/_Project/Resources/PeacefulShip/DockPadLayout_TestZone.asset");

        // Create if missing
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<DockStationDefinition>();
            var s = new SerializedObject(def);
            s.FindProperty("stationId").stringValue = "STN-TST-001";
            s.FindProperty("locationId").stringValue = "PRIMIUM_TEST_ZONE_2";
            s.FindProperty("displayName").stringValue = "Тестовая Зона";
            s.FindProperty("platformAltitude").floatValue = 2440f;
            s.FindProperty("padLayout").objectReferenceValue = layout;
            s.FindProperty("maxConcurrentLandings").intValue = 2;
            s.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(def, "Assets/_Project/Resources/PeacefulShip/DockStationDefinition_TestZone.asset");
        }
        if (layout == null)
        {
            layout = ScriptableObject.CreateInstance<DockPadLayout>();
            var l = new SerializedObject(layout);
            var pads = l.FindProperty("pads");
            pads.arraySize = 1;
            var p = pads.GetArrayElementAtIndex(0);
            p.FindPropertyRelative("padId").stringValue = "TST-PAD-001";
            p.FindPropertyRelative("triggerBoxSize").vector3Value = new Vector3(12f, 5f, 12f);
            var c = p.FindPropertyRelative("compatibleShipClasses");
            c.arraySize = 1;
            c.GetArrayElementAtIndex(0).enumValueIndex = 3; // HeavyII
            l.FindProperty("defaultTriggerBoxSize").vector3Value = new Vector3(12f, 5f, 12f);
            l.ApplyModifiedProperties();
            AssetDatabase.CreateAsset(layout, "Assets/_Project/Resources/PeacefulShip/DockPadLayout_TestZone.asset");
        }
        AssetDatabase.SaveAssets();

        // Update def with layout
        var def2 = new SerializedObject(def);
        def2.FindProperty("padLayout").objectReferenceValue = layout;
        def2.ApplyModifiedProperties();
        EditorUtility.SetDirty(def);

        // Create scene object
        var go = new GameObject("DockStation_TestZone");
        go.transform.position = zonePos;
        go.AddComponent<NetworkObject>();

        var comm = go.AddComponent<OuterCommZone>();
        var cs = new SerializedObject(comm);
        cs.FindProperty("commRange").floatValue = 150f;
        cs.FindProperty("stationId").stringValue = "STN-TST-001";
        cs.ApplyModifiedProperties();

        var station = go.AddComponent<DockStationController>();
        var ss = new SerializedObject(station);
        ss.FindProperty("dockStationDefinition").objectReferenceValue = def;
        ss.ApplyModifiedProperties();

        // Pad child
        var padGo = new GameObject("TST-PAD-001");
        padGo.transform.SetParent(go.transform);
        padGo.transform.localPosition = Vector3.zero;
        var box = padGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(12f, 5f, 12f);

        var pad = padGo.AddComponent<DockingPadTriggerBox>();
        var ps = new SerializedObject(pad);
        ps.FindProperty("padId").stringValue = "TST-PAD-001";
        var cc = ps.FindProperty("compatibleShipClasses");
        cc.arraySize = 1;
        cc.GetArrayElementAtIndex(0).enumValueIndex = 3; // HeavyII
        ps.ApplyModifiedProperties();
        padGo.AddComponent<DockPadVisualMarker>();

        Debug.Log($"[CreateTestZoneFixed] Done at {zonePos} locationId=PRIMIUM_TEST_ZONE_2");
    }
}
