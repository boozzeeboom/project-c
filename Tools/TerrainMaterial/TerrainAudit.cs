// TerrainAudit.cs — только чтение: структурная проверка Terrain.shadergraph и статус импорта.
// Запуск: execute_script filePath=Tools/TerrainMaterial/TerrainAudit.cs methodName=Execute

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class TerrainAudit
{
    const string GraphPath = "Assets/_Project/Materials/Terrain/Terrain.shadergraph";

    static string Str(string doc, string key)
    {
        var m = Regex.Match(doc, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    static int Num(string doc, string key)
    {
        var m = Regex.Match(doc, "\"" + key + "\"\\s*:\\s*(-?[0-9]+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : -999;
    }

    public static string Execute()
    {
        var sb = new StringBuilder();
        if (!File.Exists(GraphPath)) return "ФАЙЛ НЕ СОЗДАН: " + GraphPath;

        string text = File.ReadAllText(GraphPath);
        string[] parts = text.Split(new string[] { "}\n\n{" }, StringSplitOptions.None);
        var docs = new List<string>();
        for (int i = 0; i < parts.Length; i++)
            docs.Add(((i == 0 ? "" : "{") + parts[i] + (i == parts.Length - 1 ? "" : "}")).Trim());
        sb.Append("docs=" + docs.Count + " eol=" + (text.Contains("\r\n") ? "CRLF" : "LF")
                  + " bytes=" + text.Length);

        // ---------- разбор документов ----------
        var slotInfo = new Dictionary<string, int[]>();      // objectId -> { num, kind }
        var propIds = new HashSet<string>();
        var propRef = new Dictionary<string, string>();      // property objectId -> _RefName
        var propNodeRef = new Dictionary<string, string>();  // PropertyNode objectId -> property objectId
        var nodeIds = new HashSet<string>();
        var nodeSlots = new Dictionary<string, List<int[]>>();   // nodeObjectId -> список { num, kind }
        var graphDoc = (string)null;
        int blockCount = 0;

        foreach (var d in docs)
        {
            string t = Str(d, "m_Type");
            if (t == null) continue;
            string oid = Str(d, "m_ObjectId");
            if (oid == null) continue;
            string st = t.Substring(t.LastIndexOf('.') + 1);

            if (d.Contains("\"m_SlotType\""))
            {
                slotInfo[oid] = new int[] { Num(d, "m_Id"), Num(d, "m_SlotType") };
                continue;
            }
            if (st == "GraphData") { graphDoc = d; continue; }
            if (st.EndsWith("Target") || st.Contains("ShaderProperty"))
            {
                if (st.Contains("ShaderProperty")) { propIds.Add(oid); propRef[oid] = Str(d, "m_DefaultReferenceName"); }
                continue;
            }
            if (!st.EndsWith("Node")) continue;

            if (st == "PropertyNode")
            {
                var pm = Regex.Match(d, "\"m_Property\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"");
                if (pm.Success) propNodeRef[oid] = pm.Groups[1].Value;
            }

            nodeIds.Add(oid);
            var list = new List<int[]>();
            foreach (Match mm in Regex.Matches(d, "\"m_Slots\"\\s*:\\s*\\[([^\\]]*)\\]"))
                foreach (Match im in Regex.Matches(mm.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\""))
                {
                    string sid = im.Groups[1].Value;
                    list.Add(slotInfo.ContainsKey(sid) ? slotInfo[sid] : new int[] { -1, -1 });
                }
            nodeSlots[oid] = list;
            if (d.Contains("\"m_SerializedDescriptor\"")) blockCount++;
        }
        sb.Append(" | nodes=" + nodeIds.Count + " blocks=" + blockCount + " props=" + propIds.Count);

        // ---------- GraphData-списки ----------
        var problems = new List<string>();

        string mNodes, mProps, mEdges;
        mNodes = ArrayBody(graphDoc, "m_Nodes");
        mProps = ArrayBody(graphDoc, "m_Properties");
        mEdges = ArrayBody(graphDoc, "m_Edges");

        int listedNodes = Regex.Matches(mNodes, "\"m_Id\"").Count;
        int listedProps = Regex.Matches(mProps, "\"m_Id\"").Count;
        sb.Append(" | graphData: listedNodes=" + listedNodes + " listedProps=" + listedProps);

        if (listedNodes != nodeIds.Count) problems.Add("m_Nodes=" + listedNodes + " != документов нод " + nodeIds.Count);
        if (listedProps != propIds.Count) problems.Add("m_Properties=" + listedProps + " != документов свойств " + propIds.Count);

        foreach (Match mm in Regex.Matches(mNodes, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\""))
            if (!nodeIds.Contains(mm.Groups[1].Value)) problems.Add("m_Nodes ссылается на несуществующую ноду " + mm.Groups[1].Value);
        foreach (Match mm in Regex.Matches(mProps, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\""))
            if (!propIds.Contains(mm.Groups[1].Value)) problems.Add("m_Properties ссылается на несуществующее свойство " + mm.Groups[1].Value);

        // ---------- рёбра ----------
        var edgeRe = new Regex(
            "\"m_OutputSlot\"\\s*:\\s*\\{\\s*\"m_Node\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\"\\s*:\\s*(-?[0-9]+)\\s*\\}\\s*,\\s*" +
            "\"m_InputSlot\"\\s*:\\s*\\{\\s*\"m_Node\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\"\\s*:\\s*(-?[0-9]+)\\s*\\}");
        var driven = new HashSet<string>();
        int edgeCount = 0, nfhIn = 0;

        foreach (Match em in edgeRe.Matches(mEdges))
        {
            edgeCount++;
            string on = em.Groups[1].Value, inN = em.Groups[3].Value;
            int os = int.Parse(em.Groups[2].Value), isl = int.Parse(em.Groups[4].Value);

            if (!nodeSlots.ContainsKey(on)) problems.Add("ребро из несуществующей ноды " + on);
            else if (!HasSlot(nodeSlots[on], os, 1)) problems.Add("нода " + on + " (тип " + NodeType(docs, on) + ") без выходного слота " + os);

            if (!nodeSlots.ContainsKey(inN)) problems.Add("ребро в несуществующую ноду " + inN);
            else if (!HasSlot(nodeSlots[inN], isl, 0)) problems.Add("нода " + inN + " (тип " + NodeType(docs, inN) + ") без входного слота " + isl);

            if (!driven.Add(inN + ":" + isl)) problems.Add("вход " + inN + ":" + isl + " подключён дважды");

            if (NodeType(docs, inN) == "NormalFromHeightNode")
            {
                nfhIn++;
                string src = NodeType(docs, on) + ":" + os;
                string po;
                if (propNodeRef.TryGetValue(on, out po) && propRef.ContainsKey(po)) src += "(" + propRef[po] + ")";
                sb.Append(" | NFH>node=" + inN + " slot=" + isl + " from " + src);
            }
        }
        sb.Append(" | edges=" + edgeCount + " nfhEdges=" + nfhIn);
        if (nfhIn != 2) problems.Add("у NormalFromHeight должно быть 2 входящих ребра (In, Strength), найдено " + nfhIn);

        // ---------- импорт: ShaderGraphMetadata / UniversalMetadata (в этой версии GraphData не грузится как объект) ----------
        int metaProps = -1;
        string deps = "";
        bool sawMeta = false;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(GraphPath))
        {
            var so = new SerializedObject(o);

            var cd = so.FindProperty("categoryDatas");
            if (cd != null)
            {
                sawMeta = true;
                var names = new List<string>();
                for (int c = 0; c < cd.arraySize; c++)
                {
                    var pd = cd.GetArrayElementAtIndex(c).FindPropertyRelative("propertyDatas");
                    if (pd == null) continue;
                    for (int i = 0; i < pd.arraySize; i++)
                    {
                        var rn = pd.GetArrayElementAtIndex(i).FindPropertyRelative("referenceName");
                        if (rn != null) names.Add(rn.stringValue);
                    }
                }
                metaProps = names.Count;
                sb.Append(" | METADATA props=" + metaProps);
                string[] must = new string[] { "_Rock_Color", "_Rock_Deep_Color", "_Scree_Color", "_Low_Color", "_Low_Patch_Color",
                                               "_Snow_Color", "_Ice_Color", "_Dust_Color", "_Seed", "_Low_Low", "_Ice_High",
                                               "_Rock_Smooth_Base", "_Low_Smooth", "_Snow_Smooth", "_Ice_Smooth", "_Bump_Strength" };
                var missing = new List<string>();
                foreach (var m in must) if (!names.Contains(m)) missing.Add(m);
                if (missing.Count > 0) problems.Add("нет свойств: " + string.Join(",", missing.ToArray()));
                if (metaProps != propIds.Count) problems.Add("свойств в метаданных " + metaProps + " != в файле " + propIds.Count);
                if (names.Count > 0 && names[0] != "_Rock_Color") problems.Add("порядок свойств в метаданных начинается с " + names[0]);
            }

            var ad = so.FindProperty("assetDependencies");
            if (ad != null) deps = ad.arraySize.ToString();

            var ust = so.FindProperty("m_SurfaceType");
            if (ust != null)
                sb.Append(" | UNIVERSAL surfaceType=" + ust.intValue
                          + " alphaMode=" + so.FindProperty("m_AlphaMode").intValue
                          + " castShadows=" + so.FindProperty("m_CastShadows").boolValue
                          + " allowOverride=" + so.FindProperty("m_AllowMaterialOverride").boolValue
                          + " shaderID=" + so.FindProperty("m_ShaderID").stringValue);
        }
        sb.Append(" | depsCount=" + deps);
        if (!sawMeta) problems.Add("нет объекта ShaderGraphMetadata — граф не импортировался");

        // ---------- шейдер ----------
        Shader sh = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(GraphPath))
        {
            var s = o as Shader;
            if (s != null) sh = s;
        }
        if (sh == null) problems.Add("у графа нет sub-asset Shader");
        else
        {
            var msgs = ShaderUtil.GetShaderMessages(sh);
            sb.Append(" | SHADER name=" + sh.name + " supported=" + sh.isSupported
                      + " hasError=" + ShaderUtil.ShaderHasError(sh) + " msgs=" + msgs.Length);
            foreach (var m in msgs) problems.Add("shader: " + m.severity + " " + m.message);
            if (ShaderUtil.ShaderHasError(sh)) problems.Add("ShaderHasError = true");
        }

        sb.Append(problems.Count == 0 ? " | OK" : " | ПРОБЛЕМЫ(" + problems.Count + "): " + string.Join(" ;; ", problems.ToArray()));
        return sb.ToString();
    }

    static bool HasSlot(List<int[]> slots, int num, int kind)
    {
        foreach (var s in slots) if (s[0] == num && s[1] == kind) return true;
        return false;
    }

    static string NodeType(List<string> docs, string objectId)
    {
        foreach (var d in docs)
        {
            if (Str(d, "m_ObjectId") != objectId) continue;
            string t = Str(d, "m_Type");
            if (t != null) return t.Substring(t.LastIndexOf('.') + 1);
        }
        return "?";
    }

    // вырезает тело массива по ключу в документе GraphData
    static string ArrayBody(string doc, string key)
    {
        int k = doc.IndexOf("\"" + key + "\"");
        if (k < 0) return "";
        int open = doc.IndexOf('[', k);
        int depth = 0; bool inStr = false;
        for (int i = open; i < doc.Length; i++)
        {
            char c = doc[i];
            if (inStr) { if (c == '\\') { i++; continue; } if (c == '"') inStr = false; continue; }
            if (c == '"') { inStr = true; continue; }
            if (c == '[') depth++;
            else if (c == ']') { depth--; if (depth == 0) return doc.Substring(open + 1, i - open - 1); }
        }
        return "";
    }
}
