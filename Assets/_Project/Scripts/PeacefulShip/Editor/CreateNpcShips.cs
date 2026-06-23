using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEditor;
using ProjectC.Player;
using ProjectC.PeacefulShip.Stations;
using ProjectC.PeacefulShip.Core;

public static class CreateNpcShips
{
    [MenuItem("Tools/ProjectC/PeacefulShip/Create 4x HeavyII NPC Ships")]
    public static void Create()
    {
        var scene = SceneManager.GetSceneByName("WorldScene_0_0");
        if (!scene.isLoaded) { Debug.LogError("WorldScene_0_0 not loaded"); return; }
        SceneManager.SetActiveScene(scene);

        // Pad positions
        var pads = new Vector3[] {
            new Vector3(40099.70f, 2502.98f, 39791.50f), // Pad_009
            new Vector3(40099.70f, 2502.98f, 39791.50f), // Pad_008 (same pos, adjacent)
            new Vector3(40231.20f, 2502.98f, 39826.90f), // Pad_006
            new Vector3(40325.00f, 2502.98f, 39833.20f), // Pad_007
        };

        // Load schedule SO
        var schedule = AssetDatabase.LoadAssetAtPath<NpcShipSchedule>("Assets/_Project/Resources/PeacefulShip/NpcShipSchedule_Courier.asset");

        for (int i = 0; i < pads.Length; i++)
        {
            var go = new GameObject("NPC_Ship_HeavyII_0" + (i + 1));
            go.transform.position = pads[i] + Vector3.up * 2f;
            go.transform.localScale = new Vector3(4f, 2.5f, 6f); // Heavy II

            // Rigidbody
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 2000f;
            rb.useGravity = true;
            rb.linearDamping = 0.4f;
            rb.angularDamping = 8f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // NetworkObject
            go.AddComponent<NetworkObject>();

            // ShipController + set HeavyII
            var ship = go.AddComponent<ShipController>();
            var so = new SerializedObject(ship);
            var clsProp = so.FindProperty("shipFlightClass");
            if (clsProp != null)
            {
                clsProp.enumValueIndex = 3; // HeavyII
                so.ApplyModifiedProperties();
            }

            // NpcShipController
            var npc = go.AddComponent<NpcShipController>();

            // Assign schedule
            if (schedule != null)
            {
                var npcSo = new SerializedObject(npc);
                npcSo.FindProperty("schedule").objectReferenceValue = schedule;
                npcSo.ApplyModifiedProperties();
            }

            Debug.Log($"[CreateNpcShips] Created {go.name} at {go.transform.position}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[CreateNpcShips] All 4 Heavy II NPC ships created. Save the scene!");
    }
}
