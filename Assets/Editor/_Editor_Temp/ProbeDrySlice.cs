using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ProjectC.EditorTools
{
    public static class ProbeDrySlice
    {
        public static void Execute()
        {
            var sb = new StringBuilder();
            Type tool = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("ProjectC.EditorTools.BuildSettlementColliders", false);
                if (t != null) { tool = t; break; }
            }
            if (tool == null) { Debug.Log("[Probe6] tool not found"); return; }

            Type settingsType = null;
            foreach (var t in tool.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                if (t.Name == "Settings") { settingsType = t; break; }
            if (settingsType == null) { Debug.Log("[Probe6] Settings type not found"); return; }

            MethodInfo def = tool.GetMethod("DefaultSettings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            object settings = def.Invoke(null, null);
            sb.AppendLine($"[Probe6] step={settingsType.GetField("sliceStep").GetValue(settings)} " +
                          $"adaptTol={settingsType.GetField("sliceAdaptTol").GetValue(settings)}");

            MethodInfo dry = null;
            foreach (var m in tool.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                if (m.Name == "DryRunSlicesDirect") { dry = m; break; }
            if (dry == null) { Debug.Log("[Probe6] DryRunSlicesDirect not found"); return; }

            var go = GameObject.Find("WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER/ADM_ROOF_SLAB");
            if (go == null) { Debug.Log("[Probe6] root not found"); return; }

            object report = dry.Invoke(null, new object[] { go, settings });
            var rt = report.GetType();
            var sb2 = new StringBuilder();
            foreach (var f in rt.GetFields(BindingFlags.Public | BindingFlags.Instance))
                sb2.Append($"{f.Name}={f.GetValue(report)} ");
            sb.AppendLine($"[Probe6] DRY-SLICE 'ADM_ROOF_SLAB' => {sb2}");
            Debug.Log(sb.ToString());
        }
    }
}
