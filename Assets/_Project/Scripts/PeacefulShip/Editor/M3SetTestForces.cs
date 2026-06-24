// T-NS M3.1.6: M3.1.6 Set Test Forces — унифицировать yawForce/thrustForce/verticalForce/pitchForce
// для всех 4 NPC в WorldScene_0_0. Сейчас значения разные (50/50/500/5000) — пользователь
// тестировал кто с чем справляется. Ставим разумные значения, одинаковые для всех.

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using ProjectC.PeacefulShip.Stations;
using ProjectC.Player;

public static class M3SetTestForces
{
    [MenuItem("Tools/ProjectC/PeacefulShip/M3.1.6 Set Test Forces")]
    public static void Set()
    {
        var scene = SceneManager.GetSceneByName("WorldScene_0_0");
        if (!scene.isLoaded)
        {
            Debug.LogError("[M3.1.6] WorldScene_0_0 not loaded — open scene first");
            return;
        }
        SceneManager.SetActiveScene(scene);

        // Универсальные значения (компромисс между игроком и физикой корабля):
        // - thrustForce=650 (как у дефолтного Light корабля игрока)
        // - yawForce=200 (×2 от дефолта чтобы NPC быстро разворачивались)
        // - verticalForce=1200 (как у Heavy класса)
        // - pitchForce=0 (NPC не использует pitch input)
        // - antiGravity=1.0 (норма, не boost)
        const float THRUST = 650f;
        const float YAW = 200f;
        const float VERTICAL = 1200f;
        const float PITCH = 0f;
        const float AGRAV = 1f;

        var npcs = Object.FindObjectsByType<NpcShipController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (npcs.Length == 0)
        {
            Debug.LogError("[M3.1.6] No NpcShipController found in scene");
            return;
        }

        int updated = 0;
        foreach (var n in npcs)
        {
            var ship = n.GetComponent<ShipController>();
            if (ship == null) continue;
            var so = new SerializedObject(ship);
            so.FindProperty("thrustForce").floatValue = THRUST;
            so.FindProperty("yawForce").floatValue = YAW;
            so.FindProperty("verticalForce").floatValue = VERTICAL;
            so.FindProperty("pitchForce").floatValue = PITCH;
            so.FindProperty("antiGravity").floatValue = AGRAV;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ship);
            updated++;
            Debug.Log($"[M3.1.6] {n.name}: thrust={THRUST} yaw={YAW} vert={VERTICAL} pitch={PITCH} aGrav={AGRAV}");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log($"[M3.1.6] ✅ Updated {updated} NPC ships. Save complete.");
    }
}
