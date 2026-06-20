// T-DOCK-13b: Editor util — пересоздать SO после domain reload.
// Запуск через Tools > Docking > Recreate SO или через execute_code.
// FIX T-DOCK-13c: все SO создаются через CreateInstance<T>() с compile-time типом
// (не Type.GetType), чтобы m_Script GUID проставлялся корректно.
using ProjectC.Docking.Core;
using UnityEditor;
using UnityEngine;

namespace ProjectC.Docking.EditorTools
{
    public static class DockingAssetCreator
    {
        [MenuItem("Tools/Docking/Recreate SO Assets")]
        public static void RecreateAll()
        {
            CreateStationDef();
            CreatePadLayout();
            CreateVoiceLines();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DockingAssetCreator] Все SO пересозданы");
        }

        public static DockStationDefinition CreateStationDef()
        {
            var def = ScriptableObject.CreateInstance<DockStationDefinition>();
            var path = "Assets/_Project/Docking/Resources/Data/DockStationDefinition_Primium.asset";
            AssetDatabase.CreateAsset(def, path);

            // FIX T-DOCK-13c: m_Script проставляется автоматически при CreateInstance<T>().
            // Убираем MonoScript.FromScriptableObject — он не нужен.

            var so = new SerializedObject(def);
            so.FindProperty("stationId").stringValue = "STN-PRM-001";
            so.FindProperty("locationId").stringValue = "PRIMIUM";
            so.FindProperty("displayName").stringValue = "Док-станция Примум";
            var pc = so.FindProperty("platformCenter");
            if (pc != null) {
                pc.FindPropertyRelative("x").floatValue = 40500;
                pc.FindPropertyRelative("y").floatValue = 2510;
                pc.FindPropertyRelative("z").floatValue = 40500;
            }
            so.FindProperty("maxConcurrentLandings").intValue = 2;
            so.ApplyModifiedProperties();

            Debug.Log("[DockingAssetCreator] DockStationDefinition_Primium OK");
            return def;
        }

        public static DockPadLayout CreatePadLayout()
        {
            var lo = ScriptableObject.CreateInstance<DockPadLayout>();
            var path = "Assets/_Project/Docking/Resources/Data/DockPadLayout_Primium.asset";
            AssetDatabase.CreateAsset(lo, path);

            var so = new SerializedObject(lo);
            var pads = so.FindProperty("pads");
            pads.ClearArray();

            // ShipFlightClass: Light=0, Medium=1, Heavy=2, Capital=3
            void AddPad(int idx, string id, float x, float z, float yRot, int[] cls)
            {
                pads.InsertArrayElementAtIndex(idx);
                var e = pads.GetArrayElementAtIndex(idx);
                e.FindPropertyRelative("padId").stringValue = id;
                var pos = e.FindPropertyRelative("localPosition");
                pos.FindPropertyRelative("x").floatValue = x;
                pos.FindPropertyRelative("y").floatValue = 0;
                pos.FindPropertyRelative("z").floatValue = z;
                var rot = e.FindPropertyRelative("localEulerAngles");
                rot.FindPropertyRelative("x").floatValue = 0;
                rot.FindPropertyRelative("y").floatValue = yRot;
                rot.FindPropertyRelative("z").floatValue = 0;
                var cl = e.FindPropertyRelative("compatibleShipClasses");
                cl.ClearArray();
                for (int i = 0; i < cls.Length; i++)
                {
                    cl.InsertArrayElementAtIndex(i);
                    cl.GetArrayElementAtIndex(i).intValue = cls[i];
                }
            }

            AddPad(0, "PAD-001", -12, 0, 0, new[] { 0 });       // Light
            AddPad(1, "PAD-002", 0, 15, 90, new[] { 1 });        // Medium
            AddPad(2, "PAD-003", 12, 0, 180, new[] { 0 });       // Light
            AddPad(3, "PAD-004", 0, -15, -90, new[] { 1 });      // Medium
            AddPad(4, "PAD-005", 25, 25, 45, new[] { 2, 3 });    // Heavy / Capital
            so.ApplyModifiedProperties();

            Debug.Log("[DockingAssetCreator] DockPadLayout_Primium OK (" + pads.arraySize + " pads)");
            return lo;
        }

        public static DispatcherVoiceLines CreateVoiceLines()
        {
            var vo = ScriptableObject.CreateInstance<DispatcherVoiceLines>();
            var path = "Assets/_Project/Docking/Resources/Data/DispatcherVoiceLines_Default.asset";
            AssetDatabase.CreateAsset(vo, path);

            var so = new SerializedObject(vo);
            var phrases = so.FindProperty("phraseSets");
            phrases.ClearArray();

            void AddCtx(string ctx, string[] lines)
            {
                int idx = phrases.arraySize;
                phrases.InsertArrayElementAtIndex(idx);
                var e = phrases.GetArrayElementAtIndex(idx);
                e.FindPropertyRelative("context").stringValue = ctx;
                var arr = e.FindPropertyRelative("lines");
                arr.ClearArray();
                for (int i = 0; i < lines.Length; i++)
                {
                    arr.InsertArrayElementAtIndex(i);
                    arr.GetArrayElementAtIndex(i).stringValue = lines[i];
                }
            }

            AddCtx("Greeting", new[]{"Примум вызывает борт, приём.", "Борт, слышите нас?"});
            AddCtx("Assigning", new[]{"Запрашиваю погоду... есть pad #N.", "Расчет... pad #N свободен."});
            AddCtx("Assigned", new[]{"Борт, разрешаю посадку на pad #N.", "Подтверждаю, pad #N ваш."});
            AddCtx("AwaitingConfirmation", new[]{"Подтвердите посадку на pad", "Подтверждаете назначение?"});
            AddCtx("Touchdown", new[]{"Посадка зафиксирована. Добро пожаловать."});
            AddCtx("WrongPad", new[]{"Борт, вы заняли чужой pad. Перепаркуйтесь."});
            AddCtx("Takeoff", new[]{"Отстыковка разрешена. Счастливого пути."});
            AddCtx("WindowExpired", new[]{"Время вышло. Повторите запрос."});
            AddCtx("Occupied", new[]{"Все pad'ы заняты. Ждите очереди."});
            so.ApplyModifiedProperties();

            Debug.Log("[DockingAssetCreator] DispatcherVoiceLines_Default OK (" + phrases.arraySize + " contexts)");
            return vo;
        }
    }
}
