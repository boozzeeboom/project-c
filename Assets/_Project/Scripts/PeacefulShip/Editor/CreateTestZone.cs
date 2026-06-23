using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEditor;
using ProjectC.Docking.Core;
using ProjectC.Docking.Network;
using ProjectC.Docking.Zones;
using ProjectC.Docking.Stations;

public static class CreateTestZone
{
    [MenuItem("Tools/ProjectC/PeacefulShip/Create Test Zone")]
    public static void Create()
    {
        var scene = SceneManager.GetSceneByName("WorldScene_0_0");
        if (!scene.isLoaded) { Debug.LogError("WorldScene_0_0 not loaded"); return; }
        SceneManager.SetActiveScene(scene);

        // Position ~1000 units from Primium (40500, 2500, 40500)
        var zonePos = new Vector3(41000f, 2500f, 41500f);

        // Create DockStationDefinition SO
        var def = ScriptableObject.CreateInstance<DockStationDefinition>();
        var defSo = new SerializedObject(def);
        defSo.FindProperty("stationId").stringValue = "STN-TST-001";
        defSo.FindProperty("locationId").stringValue = "PRIMIUM_TEST_ZONE";
        defSo.FindProperty("displayName").stringValue = "Тестовая Зона";
        defSo.FindProperty("platformAltitude").floatValue = 2440f;
        defSo.ApplyModifiedProperties();

        var defPath = "Assets/_Project/Resources/PeacefulShip/DockStationDefinition_TestZone.asset";
        AssetDatabase.CreateAsset(def, defPath);

        // Create DockPadLayout SO (1 pad, Medium)
        var layout = ScriptableObject.CreateInstance<DockPadLayout>();
        var laySo = new SerializedObject(layout);
        var padsProp = laySo.FindProperty("pads");
        padsProp.arraySize = 1;
        var padProp = padsProp.GetArrayElementAtIndex(0);
        padProp.FindPropertyRelative("padId").stringValue = "TST-PAD-001";
        padProp.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
        padProp.FindPropertyRelative("triggerBoxSize").vector3Value = new Vector3(10f, 5f, 10f);
        var compatProp = padProp.FindPropertyRelative("compatibleShipClasses");
        compatProp.arraySize = 1;
        compatProp.GetArrayElementAtIndex(0).enumValueIndex = 3; // HeavyII
        var defSizeProp = laySo.FindProperty("defaultTriggerBoxSize");
        defSizeProp.vector3Value = new Vector3(10f, 5f, 10f);
        laySo.ApplyModifiedProperties();

        var layPath = "Assets/_Project/Resources/PeacefulShip/DockPadLayout_TestZone.asset";
        AssetDatabase.CreateAsset(layout, layPath);

        // Assign pad layout to definition
        var defSo2 = new SerializedObject(def);
        defSo2.FindProperty("padLayout").objectReferenceValue = layout;
        defSo2.FindProperty("maxConcurrentLandings").intValue = 2;
        defSo2.ApplyModifiedProperties();
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();

        // Create scene object
        var zoneGo = new GameObject("DockStation_TestZone");
        zoneGo.transform.position = zonePos;

        // NetworkObject
        var net = zoneGo.AddComponent<Unity.Netcode.NetworkObject>();

        // OuterCommZone
        var comm = zoneGo.AddComponent<OuterCommZone>();
        var commSo = new SerializedObject(comm);
        commSo.FindProperty("commRange").floatValue = 150f;
        commSo.FindProperty("stationId").stringValue = "STN-TST-001";
        commSo.FindProperty("locationId").stringValue = "PRIMIUM_TEST_ZONE";
        commSo.ApplyModifiedProperties();

        // DockStationController
        var station = zoneGo.AddComponent<DockStationController>();
        var stSo = new SerializedObject(station);
        stSo.FindProperty("dockStationDefinition").objectReferenceValue = def;
        stSo.ApplyModifiedProperties();

        // Pad trigger box child
        var padGo = new GameObject("TST-PAD-001");
        padGo.transform.SetParent(zoneGo.transform);
        padGo.transform.localPosition = Vector3.zero;
        padGo.transform.localRotation = Quaternion.identity;

        var box = padGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(10f, 5f, 10f);

        var pad = padGo.AddComponent<DockingPadTriggerBox>();
        var padSo = new SerializedObject(pad);
        padSo.FindProperty("padId").stringValue = "TST-PAD-001";
        var compClasses = padSo.FindProperty("compatibleShipClasses");
        compClasses.arraySize = 1;
        compClasses.GetArrayElementAtIndex(0).enumValueIndex = 3; // HeavyII
        padSo.ApplyModifiedProperties();

        // Visual marker
        padGo.AddComponent<DockPadVisualMarker>();

        Debug.Log($"[CreateTestZone] Created DockStation_TestZone at {zonePos}");
        Debug.Log($"[CreateTestZone] SOs: {defPath}, {layPath}");
    }
}
