// T-DOCK-13b: Editor util — пересоздать битые SO после domain reload.
// Запуск через Tools > Docking > Recreate SO или через runDockingAssets() в execute_code.
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

        public static ScriptableObject CreateStationDef()
        {
            var t = System.Type.GetType("ProjectC.Docking.Core.DockStationDefinition");
            if (t == null)
            {
                Debug.LogError("[DockingAssetCreator] Type DockStationDefinition not found");
                return null;
            }
            var def = ScriptableObject.CreateInstance(t);
            if (def == null) { Debug.LogError("Instance null"); return null; }

            var path = "Assets/_Project/Docking/Resources/Data/DockStationDefinition_Primium.asset";
            AssetDatabase.CreateAsset(def, path);
            
            var so = new SerializedObject(def);
            so.FindProperty("stationId").stringValue = "STN-PRM-001";
            so.FindProperty("locationId").stringValue = "PRIMIUM";
            so.FindProperty("displayName").stringValue = "Док-станция Примум";
            var pc = so.FindProperty("platformCenter");
            if (pc != null) { pc.FindPropertyRelative("x").floatValue = 40500; pc.FindPropertyRelative("y").floatValue = 2510; pc.FindPropertyRelative("z").floatValue = 40500; }
            so.FindProperty("platformAltitude").floatValue = 4348;
            var layout = AssetDatabase.LoadAssetAtPath("Assets/_Project/Docking/Resources/Data/DockPadLayout_Primium.asset", typeof(ScriptableObject));
            if (layout != null) so.FindProperty("padLayout").objectReferenceValue = layout;
            var voice = AssetDatabase.LoadAssetAtPath("Assets/_Project/Docking/Resources/Data/DispatcherVoiceLines_Default.asset", typeof(ScriptableObject));
            if (voice != null) so.FindProperty("voiceLines").objectReferenceValue = voice;
            so.FindProperty("maxConcurrentLandings").intValue = 2;
            so.ApplyModifiedProperties();
            
            Debug.Log("[DockingAssetCreator] DockStationDefinition_Primium OK");
            return def;
        }

        public static ScriptableObject CreatePadLayout()
        {
            var t = System.Type.GetType("ProjectC.Docking.Core.DockPadLayout");
            if (t == null) { Debug.LogError("Type DockPadLayout not found"); return null; }
            var lo = ScriptableObject.CreateInstance(t);
            var path = "Assets/_Project/Docking/Resources/Data/DockPadLayout_Primium.asset";
            AssetDatabase.CreateAsset(lo, path);
            
            var so = new SerializedObject(lo);
            var pads = so.FindProperty("pads");
            pads.ClearArray();
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
                { cl.InsertArrayElementAtIndex(i); cl.GetArrayElementAtIndex(i).intValue = cls[i]; }
            }
            AddPad(0, "PAD-001", -12, 0, 0, new[] { 0 });
            AddPad(1, "PAD-002", 0, 15, 90, new[] { 1 });
            AddPad(2, "PAD-003", 12, 0, 180, new[] { 0 });
            AddPad(3, "PAD-004", 0, -15, -90, new[] { 1 });
            AddPad(4, "PAD-005", 25, 25, 45, new[] { 2, 3 });
            so.ApplyModifiedProperties();
            Debug.Log("[DockingAssetCreator] DockPadLayout_Primium OK (" + pads.arraySize + " pads)");
            return lo;
        }

        public static ScriptableObject CreateVoiceLines()
        {
            var t = System.Type.GetType("ProjectC.Docking.Core.DispatcherVoiceLines");
            if (t == null) { Debug.LogError("Type DispatcherVoiceLines not found"); return null; }
            var vo = ScriptableObject.CreateInstance(t);
            var path = "Assets/_Project/Docking/Resources/Data/DispatcherVoiceLines_Default.asset";
            AssetDatabase.CreateAsset(vo, path);
            
            var so = new SerializedObject(vo);
            var phrases = so.FindProperty("phrases");
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
                { arr.InsertArrayElementAtIndex(i); arr.GetArrayElementAtIndex(i).stringValue = lines[i]; }
            }
            AddCtx("Greeting", new[]{"Примум вызывает борт, приём.", "Борт, слышите нас?"});
            AddCtx("Assigning", new[]{"Запрашиваю погоду... есть pad #N.", "Расчет... pad #N свободен."});
            AddCtx("Assigned", new[]{"Борт, разрешаю посадку на pad #N.", "Подтверждаю, pad #N ваш."});
            AddCtx("AwaitingConfirmation", new[]{"Подтвердите посадку на pad","Подтверждаете назначение?"});
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
