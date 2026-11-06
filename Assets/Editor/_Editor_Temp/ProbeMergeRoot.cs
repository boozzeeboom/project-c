using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ProjectC.EditorTools
{
    public static class ProbeMergeRoot
    {
        static void Dump(StringBuilder sb, string label, Type tool, MethodInfo dry, Type scopeType, Type repType, string path)
        {
            var go = GameObject.Find(path);
            if (go == null) { sb.AppendLine($"[Probe7] {label}: NOT FOUND {path}"); return; }
            object scope = Enum.Parse(scopeType, "SplitSlabsWalls");
            object rep = dry.Invoke(null, new object[] { go, 0.10f, 0.02f, "", scope });
            var sb2 = new StringBuilder();
            foreach (var f in repType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                sb2.Append($"{f.Name}={f.GetValue(rep)} ");
            sb.AppendLine($"[Probe7] {label} ('{go.name}') => {sb2}");
        }

        public static void Execute()
        {
            var sb = new StringBuilder();
            Type tool = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("ProjectC.EditorTools.BuildSettlementColliders", false);
                if (t != null) { tool = t; break; }
            }
            if (tool == null) { Debug.Log("[Probe7] tool not found"); return; }

            Type scopeType = null, repType = null;
            foreach (var t in tool.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (t.Name == "MergeScope") scopeType = t;
                if (t.Name == "MergeReport") repType = t;
            }
            MethodInfo dry = null;
            foreach (var m in tool.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                if (m.Name == "DryRunMerge") { dry = m; break; }
            if (scopeType == null || repType == null || dry == null) { Debug.Log("[Probe7] members not found"); return; }

            Dump(sb, "исходный корень", tool, dry, scopeType, repType, "WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER/ADM_DRUM");
            Dump(sb, "выделен сгенерированный корень", tool, dry, scopeType, repType, "WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER/ADM_DRUM/ADM_DRUM_Colliders");
            Dump(sb, "вложенный Root-группа", tool, dry, scopeType, repType, "WorldRoot_0_0/PRIMUM/ADMIN_FOR_COLLIDER/ADM_DRUM/ADM_DRUM_Colliders/Root");
            Debug.Log(sb.ToString());
        }
    }
}
