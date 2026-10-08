using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// ParquetVerify2 — сверка графа Parquet после перевода раскладки на выбор
// плоскости по нормали (ProceduralParquet*Faces + Normal Vector node).
// Проверяет структуру JSON, ребра/слоты и сгенерированный текст шейдера.
public static class ParquetVerify2
{
    const string Graph = "Assets/_Project/Materials/Parquet/Parquet.shadergraph";

    public static string Execute()
    {
        var log = new StringBuilder();
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string text = System.IO.File.ReadAllText(root + "/" + Graph).Replace("\r", "");

        // ── depth-aware разбор документов ────────────────────────────────────
        var docs = new List<string>();
        int depth = 0, start = -1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '{') { if (depth == 0) start = i; depth++; }
            else if (c == '}') { depth--; if (depth == 0 && start >= 0) { docs.Add(text.Substring(start, i - start + 1)); start = -1; } }
        }
        var byId = new Dictionary<string, string>();
        var typeOf = new Dictionary<string, string>();
        foreach (string d in docs)
        {
            Match mo = Regex.Match(d, "\"m_ObjectId\":\\s*\"([0-9a-f]{32})\"");
            if (!mo.Success) continue;
            string id = mo.Groups[1].Value;
            byId[id] = d;
            Match mt = Regex.Match(d, "\"m_Type\":\\s*\"([^\"]+)\"");
            string t = mt.Success ? mt.Groups[1].Value : "?";
            typeOf[id] = t.Substring(t.LastIndexOf('.') + 1);
        }
        int nodeCount = 0, slotCount = 0;
        foreach (var kv in typeOf)
        {
            if (kv.Value.EndsWith("Node") && kv.Value != "BlockNode" && kv.Key != "" ) nodeCount++;
            if (kv.Value.EndsWith("MaterialSlot") || kv.Value.EndsWith("DynamicValueMaterialSlot") || kv.Value.EndsWith("DynamicVectorMaterialSlot")) slotCount++;
        }
        Match gd = Regex.Match(text, "\"m_Type\":\\s*\"UnityEditor.ShaderGraph.GraphData\"");
        string gdDoc = null;
        foreach (string d in docs) if (d.Contains("\"UnityEditor.ShaderGraph.GraphData\"")) { gdDoc = d; break; }

        int nodesListed = Regex.Matches(Regex.Match(gdDoc, "\"m_Nodes\"\\s*:\\s*\\[(.*?)\\]\\s*,\\s*\"m_GroupDatas\"", RegexOptions.Singleline).Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"").Count;
        var edges = new List<KeyValuePair<string, string>>();
        Match em = Regex.Match(gdDoc, "\"m_Edges\"\\s*:\\s*\\[(.*)", RegexOptions.Singleline);
        string edgesBlob = em.Success ? em.Groups[1].Value : "";
        MatchCollection emc = Regex.Matches(edgesBlob,
            "\"m_OutputSlot\":\\s*\\{\\s*\"m_Node\":\\s*\\{\\s*\"m_Id\":\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\":\\s*(-?\\d+)\\s*\\}\\s*,\\s*\"m_InputSlot\":\\s*\\{\\s*\"m_Node\":\\s*\\{\\s*\"m_Id\":\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\":\\s*(-?\\d+)");
        foreach (Match m in emc) edges.Add(new KeyValuePair<string, string>(m.Groups[1].Value + "#" + m.Groups[2].Value, m.Groups[3].Value + "#" + m.Groups[4].Value));

        log.AppendLine("== STRUCTURE ==");
        log.AppendLine("   docs=" + docs.Count.ToString(CultureInfo.InvariantCulture) + " nodes(typeOf)=" + nodeCount + " nodes(listed)=" + nodesListed
            + " slots=" + slotCount + " edges=" + edges.Count);
        log.AppendLine("   graphData=" + (gd.Success ? "yes" : "NO") + " objectIds=" + byId.Count);

        // ── слоты узлов: порядок и валидность ребер ──────────────────────────
        var slotsOf = new Dictionary<string, List<string>>();
        var slotNum = new Dictionary<string, Dictionary<string, string>>();
        foreach (var kv in byId)
        {
            Match sm = Regex.Match(kv.Value, "\"m_Slots\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            var list = new List<string>();
            var map = new Dictionary<string, string>();
            if (sm.Success)
                foreach (Match x in Regex.Matches(sm.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\""))
                {
                    string sid = x.Groups[1].Value;
                    list.Add(sid);
                    if (byId.ContainsKey(sid))
                    {
                        Match mn = Regex.Match(byId[sid], "\"m_Id\"\\s*:\\s*(-?\\d+)");
                        if (mn.Success) map[mn.Groups[1].Value] = sid;
                    }
                }
            slotsOf[kv.Key] = list;
            slotNum[kv.Key] = map;
        }
        int badSlot = 0, doubleDriven = 0;
        var driven = new HashSet<string>();
        foreach (var e in edges)
        {
            string srcId = e.Key.Split('#')[0];
            string srcN = e.Key.Split('#')[1];
            string dstId = e.Value.Split('#')[0];
            string dstN = e.Value.Split('#')[1];
            if (!byId.ContainsKey(srcId) || !byId.ContainsKey(dstId)) { badSlot++; continue; }
            if (!slotNum[srcId].ContainsKey(srcN) || !slotNum[dstId].ContainsKey(dstN)) badSlot++;
            if (!driven.Add(e.Value)) doubleDriven++;
        }
        log.AppendLine("   badSlot=" + badSlot + " doubleDriven=" + doubleDriven);

        // ── Normal Vector узел ────────────────────────────────────────────────
        log.AppendLine("== NORMAL VECTOR NODE ==");
        string nvId = null;
        foreach (var kv in typeOf) if (kv.Value == "NormalVectorNode") nvId = kv.Key;
        if (nvId == null) log.AppendLine("   MISSING");
        else
        {
            log.AppendLine("   id=" + nvId.Substring(0, 8)
                + " m_Space=" + Regex.Match(byId[nvId], "\"m_Space\":\\s*(-?\\d+)").Groups[1].Value
                + " slots=" + slotsOf[nvId].Count);
            foreach (string sid in slotsOf[nvId])
                log.AppendLine("      slot " + (byId.ContainsKey(sid) ? byId[sid].Replace("\n", " ").Substring(0, Math.Min(230, byId[sid].Length)) : "??"));
        }

        // ── CF-узлы раскладки: имя функции, порядок входов ────────────────────
        log.AppendLine("== CF NODES ==");
        foreach (var kv in typeOf)
        {
            if (kv.Value != "CustomFunctionNode") continue;
            Match fn = Regex.Match(byId[kv.Key], "\"m_FunctionName\":\\s*\"([^\"]+)\"");
            string f = fn.Success ? fn.Groups[1].Value : "?";
            if (!f.StartsWith("ProceduralParquet")) continue;
            var line = new StringBuilder("   " + f + " id=" + kv.Key.Substring(0, 8) + " :");
            foreach (string sid in slotsOf[kv.Key])
            {
                if (!byId.ContainsKey(sid)) { line.Append(" ??,"); continue; }
                string nm = Regex.Match(byId[sid], "\"m_ShaderOutputName\":\\s*\"([^\"]*)\"").Groups[1].Value;
                string id = Regex.Match(byId[sid], "\"m_Id\":\\s*(-?\\d+)").Groups[1].Value;
                string io = Regex.Match(byId[sid], "\"m_SlotType\":\\s*(-?\\d+)").Groups[1].Value;
                line.Append(" " + nm + "(" + id + (io == "1" ? ",out" : "") + ")");
            }
            log.AppendLine(line.ToString());
        }

        // ── сгенерированный текст ─────────────────────────────────────────────
        string gen = null;
        Type imp = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) { imp = a.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter"); if (imp != null) break; }
        if (imp != null)
        {
            foreach (MethodInfo mi in imp.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (mi.Name != "GetShaderText") continue;
                ParameterInfo[] ps = mi.GetParameters();
                if (ps.Length != 4) continue;
                object ac = Activator.CreateInstance(ps[2].ParameterType);
                object[] args = new object[] { Graph, null, ac, null };
                try { gen = (string)mi.Invoke(null, args); }
                catch (Exception e) { log.AppendLine("   invoke failed: " + (e.InnerException != null ? e.InnerException.Message : e.Message)); }
                break;
            }
        }
        log.AppendLine("== GENERATED TEXT len=" + (gen == null ? -1 : gen.Length) + " ==");
        if (gen != null)
        {
            log.AppendLine("== SURFACE ==");
            foreach (Match m in Regex.Matches(gen, "surface\\.(BaseColor|Smoothness|NormalTS|Metallic|Emission|Occlusion)\\s*=\\s*[^;]+;"))
                log.AppendLine("   " + m.Value.Replace("\n", " ").Replace("\r", ""));

            log.AppendLine("== NFH ==");
            MatchCollection nfh = Regex.Matches(gen, "Unity_NormalFromHeight_Tangent_float\\([^;]+;");
            foreach (Match m in nfh) log.AppendLine("   " + m.Value.Replace("\n", " ").Replace("\r", ""));
            log.AppendLine("   NFH calls=" + nfh.Count);

            log.AppendLine("== PARQUET CF ==");
            foreach (string fn in new string[] { "ProceduralParquetLayoutFaces_float", "ProceduralParquetJointsFaces_float", "ProceduralParquetToneFaces_float",
                "ProceduralParquetLayout_float", "ProceduralParquetJoints_float", "ProceduralParquetTone_float",
                "PCParq_Axes", "PCParq_PlaneCell", "PCParq_Cell", "PCParq_Window" })
                log.AppendLine("   " + fn + " count=" + Regex.Matches(gen, fn).Count);
            log.AppendLine("   IN.normalOS count=" + Regex.Matches(gen, "IN\\.normalOS").Count
                + " IN.normalWS count=" + Regex.Matches(gen, "IN\\.normalWS").Count
                + " IN.ObjectSpacePosition count=" + Regex.Matches(gen, "IN\\.ObjectSpacePosition").Count
                + " _NormalVector count=" + Regex.Matches(gen, "_NormalVector").Count);

            log.AppendLine("== CALL SITES ==");
            foreach (string fn in new string[] { "ProceduralParquetLayoutFaces_float", "ProceduralParquetJointsFaces_float", "ProceduralParquetToneFaces_float" })
            {
                Match m = Regex.Match(gen, fn + "\\([^;]*;");
                log.AppendLine("   " + (m.Success ? m.Value.Replace("\n", " ").Substring(0, Math.Min(400, m.Value.Length)) : fn + " NOT CALLED"));
            }
            log.AppendLine("== WOOD CF FIRST ARG ==");
            foreach (string fn in new string[] { "Knots", "Rings", "Grain", "Pores" })
            {
                Match m = Regex.Match(gen, "ProceduralWood" + fn + "_float\\(([^,]{1,80})");
                log.AppendLine("   Wood" + fn + " firstArg=" + (m.Success ? m.Groups[1].Value : "?"));
            }
        }

        // ── шейдер ───────────────────────────────────────────────────────────
        Shader sh = Shader.Find("Parquet");
        log.AppendLine("== SHADER ==");
        log.AppendLine("   name=" + (sh == null ? "null" : sh.name)
            + " hasError=" + (sh == null ? true : ShaderUtil.ShaderHasError(sh))
            + " supported=" + (sh == null ? false : sh.isSupported)
            + " messages=" + (sh == null ? -1 : ShaderUtil.GetShaderMessages(sh).Length)
            + " props=" + (sh == null ? -1 : sh.GetPropertyCount()));
        Debug.Log(log.ToString());
        return log.ToString();
    }
}
