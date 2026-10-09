using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// FoliageVerify — приёмка графа Foliage. Только чтение.
// Проверяет: структуру JSON (документы, m_Nodes / m_Properties, рёбра, слоты),
// порядок слотов CF-нод, FO-безопасность (m_Space=0, нет AbsoluteWorld)
// и ГЛАВНОЕ — сгенерированный текст шейдера: подключение BaseColor/Smoothness/
// NormalTS, вызов NFH, порядок аргументов на местах вызова CF, отсутствие
// кирпичных имён. Плюс шейдер и пресет.
public static class FoliageVerify
{
    const string Graph = "Assets/_Project/Materials/Foliage/Foliage.shadergraph";
    const string Mat = "Assets/_Project/Materials/Foliage/M_PC_Foliage.mat";
    const string ShaderName = "Foliage";
    const string FolHlslGuid = "12a6603290cb2ea4eb0ee12056fe07b9";
    const string BrickHlslGuid = "062473832f74f9242aaaf758a1b4146d";

    static readonly string[] Props = new string[] {
        "_Leaf_Color", "_Leaf_Light_Color", "_Leaf_Deep_Color", "_Dry_Color", "_Twig_Color", "_Gap_Color",
        "_Leaf_Scale", "_Leaf_Flatten", "_Leaf_Stretch", "_Leaf_Edge", "_Leaf_Seed", "_Leaf_Tone",
        "_Leaf_Edge_Amount", "_Leaf_Gloss", "_Leaf_Edge_Gloss",
        "_Vein_Width", "_Vein_Count", "_Vein_Bump",
        "_Mass_Scale", "_Mass_Stretch", "_Mass_Seed", "_Mass_Threshold", "_Mass_Softness", "_Mass_Amount", "_Mass_Bump",
        "_Dry_Scale", "_Dry_Stretch", "_Dry_Seed", "_Dry_Threshold", "_Dry_Softness", "_Dry_Amount",
        "_Bleach_Scale", "_Bleach_Stretch", "_Bleach_Seed", "_Bleach_Threshold", "_Bleach_Softness", "_Bleach_Amount",
        "_Twig_Scale", "_Twig_Width", "_Twig_Seed", "_Twig_Amount", "_Twig_Bump",
        "_Bump_Strength", "_Smoothness"
    };

    public static string Execute()
    {
        var log = new StringBuilder();
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string path = root + "/" + Graph;
        if (!System.IO.File.Exists(path))
        {
            log.AppendLine("!! " + Graph + " отсутствует");
            Debug.Log(log.ToString());
            return log.ToString();
        }
        string text = System.IO.File.ReadAllText(path).Replace("\r", "");

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
            Match mo = Regex.Match(d, "\"m_ObjectId\"\\s*:\\s*\"([0-9a-f]{32})\"");
            if (!mo.Success) continue;
            string id = mo.Groups[1].Value;
            byId[id] = d;
            Match mt = Regex.Match(d, "\"m_Type\"\\s*:\\s*\"([^\"]+)\"");
            string t = mt.Success ? mt.Groups[1].Value : "?";
            typeOf[id] = t.Substring(t.LastIndexOf('.') + 1);
        }
        int blankInside = Regex.Matches(text, "\n[ \t]*\n").Count;

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
            + " objectIds=" + byId.Count.ToString(CultureInfo.InvariantCulture));

        int nodesExpected = 0;
        foreach (var kv in typeOf) if (kv.Value.EndsWith("Node")) nodesExpected++;

        Match nm = Regex.Match(gdDoc, "\"m_Nodes\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        var listedNodes = new List<string>();
        foreach (Match x in Regex.Matches(nm.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) listedNodes.Add(x.Groups[1].Value);

        Match pm = Regex.Match(gdDoc, "\"m_Properties\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        var listedProps = new List<string>();
        foreach (Match x in Regex.Matches(pm.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) listedProps.Add(x.Groups[1].Value);

        Match em = Regex.Match(gdDoc, "\"m_Edges\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        int edgesListed = Regex.Matches(em.Groups[1].Value, "\"m_OutputSlot\"").Count;

        log.AppendLine("   m_Nodes=" + listedNodes.Count.ToString(CultureInfo.InvariantCulture)
            + " (nodeDocs=" + nodesExpected.ToString(CultureInfo.InvariantCulture) + ")"
            + " m_Properties=" + listedProps.Count.ToString(CultureInfo.InvariantCulture)
            + " m_Edges=" + edgesListed.ToString(CultureInfo.InvariantCulture));

        int notANode = 0, notAProp = 0;
        foreach (string id in listedNodes)
        {
            string t = typeOf.ContainsKey(id) ? typeOf[id] : "MISSING DOC";
            if (!t.EndsWith("Node")) { log.AppendLine("   !! в m_Nodes не узел: " + S(id) + " type=" + t); notANode++; }
        }
        foreach (string id in listedProps)
        {
            string t = typeOf.ContainsKey(id) ? typeOf[id] : "MISSING DOC";
            if (!t.Contains("ShaderProperty")) { log.AppendLine("   !! в m_Properties не свойство: " + S(id) + " type=" + t); notAProp++; }
        }
        log.AppendLine("   notANode=" + notANode.ToString(CultureInfo.InvariantCulture)
            + " notAProp=" + notAProp.ToString(CultureInfo.InvariantCulture));

        // дубли ref-имён свойств
        var refs = new Dictionary<string, int>();
        foreach (string id in listedProps)
        {
            if (!byId.ContainsKey(id)) continue;
            Match rn = Regex.Match(byId[id], "\"m_DefaultReferenceName\"\\s*:\\s*\"([^\"]*)\"");
            if (!rn.Success) continue;
            string r = rn.Groups[1].Value;
            refs[r] = (refs.ContainsKey(r) ? refs[r] : 0) + 1;
        }
        var dupRefs = new List<string>();
        foreach (var kv in refs) if (kv.Value > 1) dupRefs.Add(kv.Key + "x" + kv.Value);
        log.AppendLine("   ref-имён=" + refs.Count.ToString(CultureInfo.InvariantCulture)
            + " дублей=" + (dupRefs.Count == 0 ? "нет" : string.Join(",", dupRefs.ToArray())));

        // ── слоты и рёбра ────────────────────────────────────────────────────
        var slotNum = new Dictionary<string, Dictionary<string, string>>();
        var slotName = new Dictionary<string, string>();
        var slotIo = new Dictionary<string, string>();
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
                    if (mn.Success)
                    {
                        map[mn.Groups[1].Value] = sid;
                        slotName[sid] = mn2.Success ? mn2.Groups[1].Value : "?";
                        Match io = Regex.Match(byId[sid], "\"m_SlotType\"\\s*:\\s*(-?\\d+)");
                        slotIo[sid] = io.Success ? io.Groups[1].Value : "?";
                    }
                }
            slotNum[kv.Key] = map;
        }

        var edges = new List<string[]>();
        foreach (Match m in Regex.Matches(gdDoc,
            "\"m_OutputSlot\"\\s*:\\s*\\{\\s*\"m_Node\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\"\\s*:\\s*(-?\\d+)\\s*\\}\\s*,\\s*\"m_InputSlot\"\\s*:\\s*\\{\\s*\"m_Node\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\"\\s*:\\s*(-?\\d+)"))
            edges.Add(new[] { m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value });

        int badSlot = 0, doubleDriven = 0, danglingNode = 0;
        var driven = new HashSet<string>();
        foreach (string[] e in edges)
        {
            if (!byId.ContainsKey(e[0]) || !byId.ContainsKey(e[2])) { danglingNode++; continue; }
            if (!slotNum[e[0]].ContainsKey(e[1]) || !slotNum[e[2]].ContainsKey(e[3])) badSlot++;
            if (!driven.Add(e[2] + "#" + e[3])) doubleDriven++;
        }
        log.AppendLine("   edges=" + edges.Count.ToString(CultureInfo.InvariantCulture)
            + " badSlot=" + badSlot.ToString(CultureInfo.InvariantCulture)
            + " doubleDriven=" + doubleDriven.ToString(CultureInfo.InvariantCulture)
            + " danglingNode=" + danglingNode.ToString(CultureInfo.InvariantCulture));

        // ── геометрия / FO ───────────────────────────────────────────────────
        log.AppendLine("== POSITION / FO ==");
        foreach (var kv in typeOf)
        {
            if (kv.Value != "PositionNode" && kv.Value != "NormalVectorNode" && kv.Value != "NormalFromHeightNode") continue;
            Match sp = Regex.Match(byId[kv.Key], "\"m_Space\"\\s*:\\s*(-?\\d+)");
            Match nmn = Regex.Match(byId[kv.Key], "\"m_Name\"\\s*:\\s*\"([^\"]*)\"");
            log.AppendLine("   " + kv.Value + " id=" + S(kv.Key)
                + " name=\"" + (nmn.Success ? nmn.Groups[1].Value : "?") + "\""
                + " m_Space=" + (sp.Success ? sp.Groups[1].Value : "n/a")
                + " m_PositionSource=" + (byId[kv.Key].Contains("m_PositionSource") ? "есть" : "нет"));
        }
        log.AppendLine("   AbsoluteWorld occ=" + Regex.Matches(text, "AbsoluteWorld").Count.ToString(CultureInfo.InvariantCulture)
            + "  m_Space values=" + string.Join(",", SpaceValues(text).ToArray()));

        // ── NFH ──────────────────────────────────────────────────────────────
        log.AppendLine("== NFH ==");
        string nfhId = null;
        foreach (var kv in typeOf) if (kv.Value == "NormalFromHeightNode") nfhId = kv.Key;
        if (nfhId == null) log.AppendLine("   !! NFH отсутствует");
        else
        {
            foreach (string k in new string[] { "0", "1", "2" })
                log.AppendLine("   slot #" + k + " -> " + Desc(slotNum[nfhId], k, byId, slotName, slotIo));
            foreach (string k in new string[] { "0", "2" })
            {
                string src = "(не подключён)";
                foreach (string[] e in edges) if (e[2] == nfhId && e[3] == k)
                    src = (typeOf.ContainsKey(e[0]) ? typeOf[e[0]] : "?") + "(" + S(e[0]) + ")#" + e[1] + (slotNum.ContainsKey(e[0]) && slotNum[e[0]].ContainsKey(e[1]) ? " " + slotName[slotNum[e[0]][e[1]]] : "");
                log.AppendLine("   NFH." + (k == "0" ? "In" : "Strength") + " <= " + src);
            }
            foreach (string[] e in edges) if (e[0] == nfhId)
                log.AppendLine("   NFH out #" + e[1] + " => " + (typeOf.ContainsKey(e[2]) ? typeOf[e[2]] : "?") + "(" + S(e[2]) + ") desc=" + Descriptor(byId, e[2]));
        }

        // ── CF-ноды ──────────────────────────────────────────────────────────
        log.AppendLine("== CF NODES ==");
        int cfCount = 0, cfBadSrc = 0;
        foreach (var kv in typeOf)
        {
            if (kv.Value != "CustomFunctionNode") continue;
            cfCount++;
            Match fn = Regex.Match(byId[kv.Key], "\"m_FunctionName\"\\s*:\\s*\"([^\"]+)\"");
            string f = fn.Success ? fn.Groups[1].Value : "?";
            Match src = Regex.Match(byId[kv.Key], "\"m_FunctionSource\"\\s*:\\s*\"([0-9a-f]*)\"");
            string sg = src.Success ? src.Groups[1].Value : "?";
            if (sg != FolHlslGuid) cfBadSrc++;
            var line = new StringBuilder("   " + f + " id=" + S(kv.Key) + " src=" + (sg.Length >= 8 ? sg.Substring(0, 8) : sg) + ":");
            foreach (var so in slotNum[kv.Key])
                line.Append(" " + slotName[so.Value] + "(" + so.Key + (slotIo.ContainsKey(so.Value) && slotIo[so.Value] == "1" ? ",out" : "") + ")");
            log.AppendLine(line.ToString());
        }
        log.AppendLine("   CF всего=" + cfCount.ToString(CultureInfo.InvariantCulture)
            + " с чужим FunctionSource=" + cfBadSrc.ToString(CultureInfo.InvariantCulture));
        log.AppendLine("   ProceduralFoliage вхождений=" + Regex.Matches(text, "ProceduralFoliage").Count.ToString(CultureInfo.InvariantCulture)
            + "  Brick HLSL guid=" + Regex.Matches(text, BrickHlslGuid).Count.ToString(CultureInfo.InvariantCulture)
            + "  Brick вхождений=" + Regex.Matches(text, "Brick").Count.ToString(CultureInfo.InvariantCulture));

        // ── сгенерированный текст ────────────────────────────────────────────
        string gen = GetGenerated();
        log.AppendLine("== GENERATED TEXT len=" + (gen == null ? -1 : gen.Length).ToString(CultureInfo.InvariantCulture) + " ==");
        if (gen != null)
        {
            log.AppendLine("== SURFACE ==");
            foreach (Match m in Regex.Matches(gen, "surface\\.(BaseColor|Smoothness|NormalTS|Metallic|Emission|Occlusion|Alpha)\\s*=\\s*[^;]+;"))
                log.AppendLine("   " + Cut(m.Value, 320));

            log.AppendLine("== 1) NFH CALL ==");
            MatchCollection nfh = Regex.Matches(gen, "Unity_NormalFromHeight_[A-Za-z]+_float\\([^;]*;");
            log.AppendLine("   calls=" + nfh.Count.ToString(CultureInfo.InvariantCulture));
            foreach (Match m in nfh) log.AppendLine("   " + Cut(m.Value, 700));

            log.AppendLine("== 2) CF CALL SITES (порядок аргументов = порядок слотов) ==");
            var uniq = new List<string>();
            foreach (Match m in Regex.Matches(gen, "ProceduralFoliage[A-Za-z]*_(float|half)\\([^;]*;"))
            {
                string u = Cut(m.Value, 700);
                if (!uniq.Contains(u)) uniq.Add(u);
            }
            log.AppendLine("   unique sites=" + uniq.Count.ToString(CultureInfo.InvariantCulture));
            foreach (string u in uniq) log.AppendLine("      " + u);

            log.AppendLine("== 3) PROPERTY USAGE ==");
            foreach (string p in Props)
                log.AppendLine("   " + p + " count=" + Regex.Matches(gen, Regex.Escape(p) + "[^A-Za-z0-9_]").Count.ToString(CultureInfo.InvariantCulture));
            log.AppendLine("   stale names:");
            foreach (string p in new string[] { "_Brick_", "_Mortar_", "_Piece_", "_Missing_", "_Efflo" })
                log.AppendLine("      " + p + " count=" + Regex.Matches(gen, Regex.Escape(p)).Count.ToString(CultureInfo.InvariantCulture));
            log.AppendLine("   IN.ObjectSpacePosition=" + Regex.Matches(gen, "IN\\.ObjectSpacePosition").Count.ToString(CultureInfo.InvariantCulture)
                + " IN.WorldSpacePosition=" + Regex.Matches(gen, "IN\\.WorldSpacePosition").Count.ToString(CultureInfo.InvariantCulture)
                + " AbsoluteWorld=" + Regex.Matches(gen, "AbsoluteWorld").Count.ToString(CultureInfo.InvariantCulture)
                + " FoliageNoise.hlsl=" + Regex.Matches(gen, "ProceduralFoliageNoise").Count.ToString(CultureInfo.InvariantCulture));
        }

        // ── шейдер и пресет ──────────────────────────────────────────────────
        Shader sh = Shader.Find(ShaderName);
        log.AppendLine("== SHADER ==");
        log.AppendLine("   name=" + (sh == null ? "null" : sh.name)
            + " hasError=" + (sh == null ? true : ShaderUtil.ShaderHasError(sh))
            + " supported=" + (sh == null ? false : sh.isSupported)
            + " messages=" + (sh == null ? -1 : ShaderUtil.GetShaderMessages(sh).Length)
            + " props=" + (sh == null ? -1 : sh.GetPropertyCount()));
        if (sh != null)
        {
            var missing = new List<string>();
            foreach (string p in Props) if (sh.FindPropertyIndex(p) < 0) missing.Add(p);
            log.AppendLine("   ожидаемых свойств нет: " + (missing.Count == 0 ? "нет" : string.Join(", ", missing.ToArray())));
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(Mat);
        log.AppendLine("== MATERIAL ==");
        if (mat == null) log.AppendLine("   " + Mat + " отсутствует");
        else
        {
            log.AppendLine("   есть, shader=" + mat.shader.name + " queue=" + mat.renderQueue.ToString(CultureInfo.InvariantCulture)
                + " keywords=" + mat.shaderKeywords.Length.ToString(CultureInfo.InvariantCulture));
            int authored = 0, missingMat = 0;
            foreach (string p in Props)
            {
                if (sh != null && sh.FindPropertyIndex(p) < 0) continue;
                authored++;
                if (!mat.HasProperty(p)) missingMat++;
            }
            log.AppendLine("   свойств прочитано=" + authored.ToString(CultureInfo.InvariantCulture)
                + " отсутствующих на материале=" + missingMat.ToString(CultureInfo.InvariantCulture));
        }

        log.AppendLine("== BRICK DONOR UNTOUCHED ==");
        string brick = root + "/Assets/_Project/Materials/Brick/Brick.shadergraph";
        if (System.IO.File.Exists(brick))
        {
            string bt = System.IO.File.ReadAllText(brick).Replace("\r", "");
            log.AppendLine("   Brick docs=" + Regex.Matches(bt, "\"m_ObjectId\"").Count.ToString(CultureInfo.InvariantCulture)
                + " edges=" + Regex.Matches(bt, "\"m_OutputSlot\"").Count.ToString(CultureInfo.InvariantCulture));
        }

        Debug.Log(log.ToString());
        return log.ToString();
    }

    static string GetGenerated()
    {
        Type imp = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) { imp = a.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter"); if (imp != null) break; }
        if (imp == null) return null;
        foreach (MethodInfo mi in imp.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (mi.Name != "GetShaderText") continue;
            ParameterInfo[] ps = mi.GetParameters();
            if (ps.Length != 4) continue;
            object ac = Activator.CreateInstance(ps[2].ParameterType);
            object[] args = new object[] { Graph, null, ac, null };
            try { return (string)mi.Invoke(null, args); }
            catch { return null; }
        }
        return null;
    }

    static string Descriptor(Dictionary<string, string> byId, string nodeId)
    {
        if (!byId.ContainsKey(nodeId)) return "?";
        Match m = Regex.Match(byId[nodeId], "\"m_SerializedDescriptor\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : "-";
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

    static string Desc(Dictionary<string, string> slots, string slotId, Dictionary<string, string> byId,
        Dictionary<string, string> slotName, Dictionary<string, string> slotIo)
    {
        if (slots == null || !slots.ContainsKey(slotId)) return "(нет слота #" + slotId + ")";
        string sid = slots[slotId];
        if (!byId.ContainsKey(sid)) return "(слот #" + slotId + " без документа)";
        return "#" + slotId + " " + (slotName.ContainsKey(sid) ? slotName[sid] : "?")
            + " io=" + (slotIo.ContainsKey(sid) ? slotIo[sid] : "?");
    }
}
