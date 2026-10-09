using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// FoliageProbe — РАЗОВЫЙ зонд: снимает с донора Brick шаблоны документов,
// которые нужны FoliageBuilder. Brick — только чтение.
// Пишет Tools/_foliage_donor_probe.txt. После работы скрипт удаляется.
public static class FoliageProbe
{
    const string Src = "Assets/_Project/Materials/Brick/Brick.shadergraph";
    const string Out = "Tools/_foliage_donor_probe.txt";

    static string Root;
    static readonly List<string> Order = new List<string>();
    static readonly Dictionary<string, string> ById = new Dictionary<string, string>();
    static readonly Dictionary<string, string> TypeOf = new Dictionary<string, string>();

    public static string Execute()
    {
        Root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string txt = File.ReadAllText(Root + "/" + Src).Replace("\r\n", "\n").Replace("\r", "\n");
        foreach (string raw in Regex.Split(txt, "\n\n"))
        {
            string d = raw.Trim();
            if (d.Length == 0) continue;
            Match mo = Regex.Match(d, "\"m_ObjectId\"\\s*:\\s*\"([0-9a-f]{32})\"");
            if (!mo.Success) continue;
            string id = mo.Groups[1].Value;
            ById[id] = d; Order.Add(id);
            Match mt = Regex.Match(d, "\"m_Type\"\\s*:\\s*\"([^\"]+)\"");
            string t = mt.Success ? mt.Groups[1].Value : "?";
            TypeOf[id] = t.Substring(t.LastIndexOf('.') + 1);
        }

        var sb = new StringBuilder();
        sb.AppendLine("== inventory: docs=" + Order.Count);

        var counts = new Dictionary<string, int>();
        foreach (string id in Order) { int c; counts.TryGetValue(TypeOf[id], out c); counts[TypeOf[id]] = c + 1; }
        var keys = new List<string>(counts.Keys); keys.Sort(StringComparer.Ordinal);
        foreach (string k in keys) sb.AppendLine("  " + k + " = " + counts[k]);

        string gd = FirstOfType("GraphData");
        string gdc = ById[gd];
        foreach (string a in new[] { "m_Nodes", "m_Properties", "m_Edges", "m_Keywords", "m_Dropdowns",
                                     "m_CategoryData", "m_GroupDatas", "m_StickyNoteDatas", "m_Blocks",
                                     "m_SerializedViewStates", "m_ActiveTargets" })
            gdc = Abbrev(gdc, a);
        sb.AppendLine("\n== GraphData " + S(gd) + " (arrays abbreviated)");
        sb.AppendLine(gdc);

        DumpType(sb, "PositionNode", 1, true);
        DumpType(sb, "NormalFromHeightNode", 1, true);
        DumpType(sb, "MultiplyNode", 1, true);
        DumpType(sb, "LerpNode", 1, true);
        DumpType(sb, "SmoothstepNode", 1, true);
        DumpType(sb, "SaturateNode", 1, true);
        DumpType(sb, "AddNode", 1, true);
        DumpType(sb, "Vector1ShaderProperty", 1, false);
        DumpType(sb, "ColorShaderProperty", 1, false);
        DumpType(sb, "BlockNode", 1, true);

        // PropertyNode: по одному на Vector1 и на Color
        string pn1 = null, pnColor = null;
        foreach (string id in Order)
        {
            if (TypeOf[id] != "PropertyNode") continue;
            Match mp = Regex.Match(ById[id], "\"m_Property\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"");
            if (!mp.Success) continue;
            string pt = TypeOf.ContainsKey(mp.Groups[1].Value) ? TypeOf[mp.Groups[1].Value] : "?";
            if (pt == "Vector1ShaderProperty" && pn1 == null) pn1 = id;
            if (pt == "ColorShaderProperty" && pnColor == null) pnColor = id;
        }
        DumpDoc(sb, "PropertyNode(float) " + S(pn1), pn1, true);
        DumpDoc(sb, "PropertyNode(color) " + S(pnColor), pnColor, true);

        // CF-нода Patch: самый полный шаблон (float3 + 3 скаляра + выход)
        string cf = null;
        foreach (string id in Order)
            if (TypeOf[id] == "CustomFunctionNode" && ById[id].Contains("\"m_FunctionName\": \"ProceduralBrickPatch\"")) cf = id;
        DumpDoc(sb, "CustomFunctionNode(Patch) " + S(cf), cf, true);

        // все CF-ноды: имя функции, число слотов, типы слотов
        sb.AppendLine("\n== CF nodes");
        foreach (string id in Order)
        {
            if (TypeOf[id] != "CustomFunctionNode") continue;
            sb.AppendLine("  " + S(id) + " fn=" + Str(id, "m_FunctionName")
                + " name=" + Str(id, "m_Name")
                + " prec=" + Str(id, "m_Precision")
                + " srcType=" + Str(id, "m_SourceType")
                + " slots=" + SlotIds(ById[id]).Count);
        }

        // входные слоты всех CF-нод: id / тип / m_Id / m_SlotType
        sb.AppendLine("\n== CF slot templates");
        foreach (string id in Order)
        {
            if (TypeOf[id] != "CustomFunctionNode") continue;
            foreach (string so in SlotIds(ById[id]))
                sb.AppendLine("  " + TypeOf[so] + " obj=" + S(so)
                    + " slotId=" + Str(so, "m_Id")
                    + " slotType=" + Str(so, "m_SlotType")
                    + " disp=" + Str(so, "m_DisplayName"));
            sb.AppendLine("  --");
        }

        File.WriteAllText(Root + "/" + Out, sb.ToString(), new UTF8Encoding(false));
        return "wrote " + Out + " chars=" + sb.Length;
    }

    static string Abbrev(string doc, string key)
    {
        return Regex.Replace(doc, "\"" + key + "\"\\s*:\\s*\\[.*?\\]", me =>
            "\"" + key + "\": [ <<" + Regex.Matches(me.Value, "\\{").Count + " obj>> ]", RegexOptions.Singleline);
    }

    static void DumpType(StringBuilder sb, string type, int n, bool slots)
    {
        int i = 0;
        foreach (string id in Order)
        {
            if (TypeOf[id] != type) continue;
            DumpDoc(sb, type + " " + S(id), id, slots);
            if (++i >= n) break;
        }
    }

    static void DumpDoc(StringBuilder sb, string title, string id, bool slots)
    {
        if (id == null) { sb.AppendLine("\n== " + title + " : НЕ НАЙДЕН"); return; }
        sb.AppendLine("\n== " + title);
        sb.AppendLine(ById[id]);
        if (!slots) return;
        foreach (string so in SlotIds(ById[id]))
        {
            if (!ById.ContainsKey(so)) { sb.AppendLine("  -- dangling slot " + S(so)); continue; }
            sb.AppendLine("  -- slot " + S(so));
            sb.AppendLine(ById[so]);
        }
    }

    static string S(string id) { return id == null ? "null" : id.Substring(0, 8); }

    static string Str(string id, string key)
    {
        Match m = Regex.Match(ById[id], "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : "?";
    }

    static string FirstOfType(string t) { foreach (string id in Order) if (TypeOf[id] == t) return id; return null; }

    static List<string> SlotIds(string doc)
    {
        var res = new List<string>();
        Match m = Regex.Match(doc, "\"m_Slots\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if (!m.Success) return res;
        foreach (Match x in Regex.Matches(m.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) res.Add(x.Groups[1].Value);
        return res;
    }
}
