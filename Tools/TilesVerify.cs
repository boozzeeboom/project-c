using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// TilesVerify — приёмка графа Tiles: структура JSON (документы, списки m_Nodes /
// m_Properties, рёбра, слоты), порядок слотов CF-нод, и ГЛАВНОЕ — сгенерированный
// текст шейдера: три обязательных пункта рельефа (NFH вызывает
// Unity_NormalFromHeight_*, Strength заведён на _Bump_Strength, сумма высоты
// включает швы/поры/кракле/арку), плюс порядок аргументов на местах вызова CF.
// Только чтение.
public static class TilesVerify
{
    const string Graph = "Assets/_Project/Materials/Tiles/Tiles.shadergraph";

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
        // пустые строки: между документами это штатный разделитель, внутри документа — поломка
        int blankInside = Regex.Matches(text, "\\n[ \\t]*\\n").Count;

        string gdDoc = null;
        foreach (string d in docs) if (d.Contains("\"UnityEditor.ShaderGraph.GraphData\"")) { gdDoc = d; break; }
        log.AppendLine("== STRUCTURE ==");
        if (gdDoc == null)
        {
            log.AppendLine("   !! GraphData не найден — разбор документов сломан");
            Debug.Log(log.ToString());
            return log.ToString();
        }
        log.AppendLine("   docs=" + docs.Count.ToString(CultureInfo.InvariantCulture)
            + " separators(blankLines)=" + blankInside.ToString(CultureInfo.InvariantCulture)
            + " objectIds=" + byId.Count.ToString(CultureInfo.InvariantCulture)
            + " graphData=" + (gdDoc != null ? "yes" : "NO"));

        int stringsPropertyDeclared = byId.Count;

        // ожидаемое число узлов = все документы типов, оканчивающихся на "Node"
        int nodesExpected = 0;
        foreach (var kv in typeOf) if (kv.Value.EndsWith("Node")) nodesExpected++;

        Match nm = Regex.Match(gdDoc, "\"m_Nodes\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        var listedNodes = new List<string>();
        foreach (Match x in Regex.Matches(nm.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) listedNodes.Add(x.Groups[1].Value);

        Match pm = Regex.Match(gdDoc, "\"m_Properties\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        var listedProps = new List<string>();
        foreach (Match x in Regex.Matches(pm.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) listedProps.Add(x.Groups[1].Value);

        log.AppendLine("   m_Nodes=" + listedNodes.Count.ToString(CultureInfo.InvariantCulture)
            + " (nodeDocs=" + nodesExpected.ToString(CultureInfo.InvariantCulture) + ")"
            + " m_Properties=" + listedProps.Count.ToString(CultureInfo.InvariantCulture));

        int notANode = 0;
        foreach (string id in listedNodes)
        {
            string t = typeOf.ContainsKey(id) ? typeOf[id] : "MISSING DOC";
            if (!t.EndsWith("Node"))
            {
                log.AppendLine("   !! в m_Nodes не узел: " + S(id) + " type=" + t);
                notANode++;
            }
        }
        int notAProp = 0;
        foreach (string id in listedProps)
        {
            string t = typeOf.ContainsKey(id) ? typeOf[id] : "MISSING DOC";
            if (!t.Contains("ShaderProperty"))
            {
                log.AppendLine("   !! в m_Properties не свойство: " + S(id) + " type=" + t);
                notAProp++;
            }
        }
        log.AppendLine("   notANode=" + notANode.ToString(CultureInfo.InvariantCulture)
            + " notAProp=" + notAProp.ToString(CultureInfo.InvariantCulture)
            + " (шаблон " + stringsPropertyDeclared.ToString(CultureInfo.InvariantCulture) + " док.)");

        // ── слоты и рёбра ────────────────────────────────────────────────────
        var slotNum = new Dictionary<string, Dictionary<string, string>>();
        var slotName = new Dictionary<string, string>();
        foreach (var kv in byId)
        {
            Match sm = Regex.Match(kv.Value, "\"m_Slots\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            var map = new Dictionary<string, string>();
            if (sm.Success)
                foreach (Match x in Regex.Matches(sm.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\""))
                {
                    string sid = x.Groups[1].Value;
                    if (!byId.ContainsKey(sid)) continue;
                    Match mn = Regex.Match(byId[sid], "\"m_Id\"\\s*:\\s*(-?\\d+)");
                    Match mn2 = Regex.Match(byId[sid], "\"m_ShaderOutputName\"\\s*:\\s*\"([^\"]*)\"");
                    if (mn.Success) { map[mn.Groups[1].Value] = sid; slotName[sid] = mn2.Success ? mn2.Groups[1].Value : "?"; }
                }
            slotNum[kv.Key] = map;
        }

        var edges = new List<string[]>();
        foreach (Match m in Regex.Matches(gdDoc,
            "\"m_OutputSlot\"\\s*:\\s*\\{\\s*\"m_Node\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\"\\s*:\\s*(-?\\d+)\\s*\\}\\s*,\\s*\"m_InputSlot\"\\s*:\\s*\\{\\s*\"m_Node\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\"\\s*:\\s*(-?\\d+)"))
            edges.Add(new[] { m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value });

        int badSlot = 0, doubleDriven = 0;
        var driven = new HashSet<string>();
        foreach (string[] e in edges)
        {
            if (!byId.ContainsKey(e[0]) || !byId.ContainsKey(e[2])) { badSlot++; continue; }
            if (!slotNum[e[0]].ContainsKey(e[1]) || !slotNum[e[2]].ContainsKey(e[3])) badSlot++;
            if (!driven.Add(e[2] + "#" + e[3])) doubleDriven++;
        }
        log.AppendLine("   edges=" + edges.Count.ToString(CultureInfo.InvariantCulture)
            + " badSlot=" + badSlot.ToString(CultureInfo.InvariantCulture)
            + " doubleDriven=" + doubleDriven.ToString(CultureInfo.InvariantCulture));

        // ── Position / NFH / AbsoluteWorld ───────────────────────────────────
        log.AppendLine("== POSITION / NFH ==");
        foreach (var kv in typeOf)
        {
            if (kv.Value != "PositionNode" && kv.Value != "NormalFromHeightNode") continue;
            Match sp = Regex.Match(byId[kv.Key], "\"m_Space\":\\s*(-?\\d+)");
            log.AppendLine("   " + kv.Value + " id=" + S(kv.Key) + " m_Space=" + (sp.Success ? sp.Groups[1].Value : "n/a"));
        }
        log.AppendLine("   AbsoluteWorld occurrences=" + Regex.Matches(gdDoc + text, "AbsoluteWorld").Count
            + "  m_Space values=" + string.Join(",", SpaceValues(text)));

        log.AppendLine("== NFH STRENGTH EDGE ==");
        string nfhId = null;
        foreach (var kv in typeOf) if (kv.Value == "NormalFromHeightNode") nfhId = kv.Key;
        if (nfhId == null) log.AppendLine("   MISSING NFH");
        else
        {
            log.AppendLine("   Strength slot id -> " + Desc(slotNum[nfhId], "2", byId, typeOf, slotName));
            log.AppendLine("   In slot id       -> " + Desc(slotNum[nfhId], "0", byId, typeOf, slotName));
        }

        // ── CF-узлы ──────────────────────────────────────────────────────────
        log.AppendLine("== CF NODES ==");
        foreach (var kv in typeOf)
        {
            if (kv.Value != "CustomFunctionNode") continue;
            Match fn = Regex.Match(byId[kv.Key], "\"m_FunctionName\":\\s*\"([^\"]+)\"");
            string f = fn.Success ? fn.Groups[1].Value : "?";
            Match src = Regex.Match(byId[kv.Key], "\"m_FunctionSource\":\\s*\"([0-9a-f]*)\"");
            var line = new StringBuilder("   " + f + " id=" + S(kv.Key) + " src=" + (src.Success ? src.Groups[1].Value.Substring(0, 8) : "?") + ":");
            foreach (var so in slotNum[kv.Key])
            {
                string sid = so.Value;
                string io = Regex.Match(byId[sid], "\"m_SlotType\":\\s*(-?\\d+)").Groups[1].Value;
                line.Append(" " + slotName[sid] + "(" + so.Key + (io == "1" ? ",out" : "") + ")");
            }
            log.AppendLine(line.ToString());
        }

        // ── кто кормит TileTail ──────────────────────────────────────────────
        log.AppendLine("== TileTail FEED ==");
        foreach (var kv in typeOf)
        {
            if (kv.Value != "CustomFunctionNode") continue;
            foreach (var so in slotNum[kv.Key])
            {
                if (slotName[so.Value] != "TileTail") continue;
                string src = "(пусто)";
                foreach (string[] e in edges)
                    if (e[2] == kv.Key && e[3] == so.Key)
                        src = typeOf.ContainsKey(e[0]) ? typeOf[e[0]] + "(" + S(e[0]) + ")  prop=" + PropOf(e[0], byId) : "??";
                log.AppendLine("   " + Regex.Match(byId[kv.Key], "\"m_FunctionName\":\\s*\"([^\"]+)\"").Groups[1].Value
                    + " #" + so.Key + " <= " + src);
            }
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
        log.AppendLine("== GENERATED TEXT len=" + (gen == null ? -1 : gen.Length).ToString(CultureInfo.InvariantCulture) + " ==");
        if (gen != null)
        {
            log.AppendLine("== SURFACE ==");
            foreach (Match m in Regex.Matches(gen, "surface\\.(BaseColor|Smoothness|NormalTS|Metallic|Emission|Occlusion|Alpha)\\s*=\\s*[^;]+;"))
                log.AppendLine("   " + Cut(m.Value, 300));

            log.AppendLine("== 1) NFH CALL (обязательный пункт) ==");
            MatchCollection nfh = Regex.Matches(gen, "Unity_NormalFromHeight_[A-Za-z]+_float\\([^;]*;");
            log.AppendLine("   calls=" + nfh.Count.ToString(CultureInfo.InvariantCulture));
            foreach (Match m in nfh) log.AppendLine("   " + Cut(m.Value, 700));

            log.AppendLine("== 2) height sum feeding NFH ==");
            string chain = "(137930f4|267dc525|a17a9341|cb4e5685|79ba556d|f413245f)";
            MatchCollection cl = Regex.Matches(gen, "(?m)^[^\\n]*" + chain + "[^\\n]*$");
            log.AppendLine("   lines=" + cl.Count.ToString(CultureInfo.InvariantCulture));
            foreach (Match m in cl) log.AppendLine("   " + Cut(m.Value, 900));

            log.AppendLine("== 3) CF CALL SITES (уникальные, порядок аргументов = порядок слотов) ==");
            foreach (string fn in new string[] { "ProceduralTilesJoints_float", "ProceduralTilesTone_float", "ProceduralTilesCrown_float",
                "ProceduralTilesGrains_float", "ProceduralTilesPores_float", "ProceduralTilesCrazing_float", "ProceduralTilesPatch_float" })
            {
                MatchCollection mc = Regex.Matches(gen, Regex.Escape(fn) + "\\([^;]*;");
                var uniq = new List<string>();
                foreach (Match m in mc) { string u = Cut(m.Value, 900); if (!uniq.Contains(u)) uniq.Add(u); }
                log.AppendLine("   " + fn + " count=" + mc.Count.ToString(CultureInfo.InvariantCulture)
                    + " unique=" + uniq.Count.ToString(CultureInfo.InvariantCulture));
                foreach (string u in uniq) log.AppendLine("      " + u);
            }

            log.AppendLine("== 4) property usage in generated shader ==");
            foreach (string p in new string[] { "_Tile_Tail", "_Tile_Scale", "_Tile_Aspect", "_Tile_Warp", "_Tile_Seed", "_Tile_Tone",
                "_Tile_Crown", "_Tile_Gloss", "_Gap_Color", "_Gap_Gloss", "_Salt_Color", "_Moss_Color", "_Craze_Scale", "_Bump_Strength" })
                log.AppendLine("   " + p + " count=" + Regex.Matches(gen, Regex.Escape(p) + "[^A-Za-z0-9_]").Count.ToString(CultureInfo.InvariantCulture));
            log.AppendLine("   stale names (Brick/Mortar/Crack/Efflo/Dirt):");
            foreach (string p in new string[] { "_Brick_", "_Mortar_", "_Crack_", "_Efflo_", "_Dirt_", "_Efflorescence_" })
                log.AppendLine("      " + p + " count=" + Regex.Matches(gen, Regex.Escape(p)).Count.ToString(CultureInfo.InvariantCulture));
            log.AppendLine("   IN.ObjectSpacePosition=" + Regex.Matches(gen, "IN\\.ObjectSpacePosition").Count.ToString(CultureInfo.InvariantCulture)
                + " AbsoluteWorld=" + Regex.Matches(gen, "AbsoluteWorld").Count.ToString(CultureInfo.InvariantCulture));
        }

        // ── шейдер и материал ────────────────────────────────────────────────
        Shader sh = Shader.Find("Tiles");
        log.AppendLine("== SHADER ==");
        log.AppendLine("   name=" + (sh == null ? "null" : sh.name)
            + " hasError=" + (sh == null ? true : ShaderUtil.ShaderHasError(sh))
            + " supported=" + (sh == null ? false : sh.isSupported)
            + " messages=" + (sh == null ? -1 : ShaderUtil.GetShaderMessages(sh).Length)
            + " props=" + (sh == null ? -1 : sh.GetPropertyCount()));
        if (sh != null)
        {
            var missing = new List<string>();
            foreach (string p in new string[] { "_Tile_Color", "_Tile_Light_Color", "_Gap_Color", "_Salt_Color", "_Moss_Color",
                "_Tile_Scale", "_Tile_Aspect", "_Row_Offset", "_Tile_Warp", "_Joint_Width_X", "_Joint_Width_Y", "_Tile_Seed", "_Tile_Tail",
                "_Tile_Tone", "_Joint_Amount", "_Tile_Crown", "_Joint_Bump", "_Tile_Grain_Scale", "_Tile_Grain_Stretch", "_Tile_Grain_Seed",
                "_Tile_Grain_Amount", "_Body_Grain_Scale", "_Body_Grain_Seed", "_Body_Grain_Amount", "_Pore_Scale", "_Pore_Width", "_Pore_Seed",
                "_Pore_Amount", "_Pore_Bump", "_Craze_Scale", "_Craze_Stretch", "_Craze_Width", "_Craze_Warp", "_Craze_Seed", "_Craze_Amount",
                "_Craze_Bump", "_Salt_Scale", "_Salt_Seed", "_Salt_Threshold", "_Salt_Softness", "_Salt_Amount", "_Moss_Scale", "_Moss_Stretch",
                "_Moss_Seed", "_Moss_Threshold", "_Moss_Softness", "_Moss_Amount", "_Smoothness", "_Tile_Gloss", "_Gap_Gloss", "_Bump_Strength" })
                if (sh.FindPropertyIndex(p) < 0) missing.Add(p);
            log.AppendLine("   ожидаемых свойств нет: " + (missing.Count == 0 ? "нет" : string.Join(", ", missing.ToArray())));
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Tiles/M_PC_Tiles.mat");
        log.AppendLine("== MATERIAL ==");
        log.AppendLine("   " + (mat == null ? "M_PC_Tiles.mat отсутствует" : "есть, shader=" + mat.shader.name
            + " queue=" + mat.renderQueue.ToString(CultureInfo.InvariantCulture)));

        Debug.Log(log.ToString());
        return log.ToString();
    }

    static string S(string id) { return id == null ? "null" : id.Substring(0, 8); }

    static string Cut(string s, int n)
    {
        string c = s.Replace("\n", " ").Replace("\r", "").Replace("\t", " ");
        while (c.Contains("  ")) c = c.Replace("  ", " ");
        return c.Length <= n ? c : c.Substring(0, n) + "…";
    }

    static List<int> SpaceValues(string text)
    {
        var res = new List<int>();
        foreach (Match m in Regex.Matches(text, "\"m_Space\"\\s*:\\s*(-?\\d+)"))
        {
            int v = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!res.Contains(v)) res.Add(v);
        }
        res.Sort();
        return res;
    }

    static string PropOf(string nodeId, Dictionary<string, string> byId)
    {
        if (!byId.ContainsKey(nodeId)) return "-";
        Match mp = Regex.Match(byId[nodeId], "\"m_Property\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"");
        if (!mp.Success) return "-";
        string pid = mp.Groups[1].Value;
        if (!byId.ContainsKey(pid)) return "??";
        Match rn = Regex.Match(byId[pid], "\"m_DefaultReferenceName\"\\s*:\\s*\"([^\"]*)\"");
        return rn.Success ? rn.Groups[1].Value : "?";
    }

    static string Desc(Dictionary<string, string> slots, string slotId, Dictionary<string, string> byId,
        Dictionary<string, string> typeOf, Dictionary<string, string> slotName)
    {
        if (slots == null || !slots.ContainsKey(slotId)) return "(нет слота #" + slotId + ")";
        string sid = slots[slotId];
        if (!byId.ContainsKey(sid)) return "(слот #" + slotId + " без документа)";
        Match io = Regex.Match(byId[sid], "\"m_SlotType\":\\s*(-?\\d+)");
        return "#" + slotId + " " + slotName[sid] + " io=" + (io.Success ? io.Groups[1].Value : "?");
    }
}
