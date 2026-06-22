using UnityEditor;
using ProjectC.PeacefulShip.Stations;
using ProjectC.PeacefulShip.Core;
using ProjectC.Player;
using UnityEngine;

public static class CreateNpcShipSchedules
{
    [MenuItem("Tools/ProjectC/PeacefulShip/Create Test Schedules")]
    public static void Create()
    {
        var path = "Assets/_Project/Resources/PeacefulShip/";
        System.IO.Directory.CreateDirectory(path);

        // SO 1: Courier (Primium → TestZone, RoundTrip)
        var s1 = ScriptableObject.CreateInstance<NpcShipSchedule>();
        s1.scheduleId = "SCH-NPC-001";
        s1.displayName = "Курьер Примум-Тест";
        s1.scheduleType = NpcShipSchedule.ScheduleType.RoundTrip;
        s1.routes = new NpcShipRoute[1];
        s1.routes[0] = new NpcShipRoute { fromLocationId = "PRIMIUM", toLocationId = "PRIMIUM_TEST_ZONE_2",
            dwellTimeSec = 60f, flightDurationSec = 120f, preferredShipClass = ShipFlightClass.Light, demandCategory = NpcShipDemandCategory.Generic };
        s1.meanArrivalIntervalSec = 480f;
        s1.arrivalIntervalStdDev = 90f;
        s1.minArrivalSpacingSec = 60f;
        s1.minDwellTimeSec = 60f;
        s1.maxDwellTimeSec = 90f;
        AssetDatabase.CreateAsset(s1, path + "NpcShipSchedule_Courier.asset");

        // SO 2: Trader (TestZone → Primium, Loop)
        var s2 = ScriptableObject.CreateInstance<NpcShipSchedule>();
        s2.scheduleId = "SCH-NPC-002";
        s2.displayName = "Торговец ТестЗона-Примум";
        s2.scheduleType = NpcShipSchedule.ScheduleType.Loop;
        s2.routes = new NpcShipRoute[1];
        s2.routes[0] = new NpcShipRoute { fromLocationId = "PRIMIUM_TEST_ZONE_2", toLocationId = "PRIMIUM",
            dwellTimeSec = 90f, flightDurationSec = 120f, preferredShipClass = ShipFlightClass.Medium, demandCategory = NpcShipDemandCategory.Generic };
        s2.meanArrivalIntervalSec = 600f;
        s2.arrivalIntervalStdDev = 120f;
        s2.minArrivalSpacingSec = 90f;
        s2.minDwellTimeSec = 60f;
        s2.maxDwellTimeSec = 120f;
        AssetDatabase.CreateAsset(s2, path + "NpcShipSchedule_Trader.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CreateNpcShipSchedules] Created {path}*.asset");
    }
}