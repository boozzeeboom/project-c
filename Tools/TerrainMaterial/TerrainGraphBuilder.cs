// TerrainGraphBuilder.cs — сборка Assets/_Project/Materials/Terrain/Terrain.shadergraph
// из шаблонов документов существующих графов проекта (только чтение чужих графов).
//
// Запуск: execute_script filePath=Tools/TerrainMaterial/TerrainGraphBuilder.cs methodName=Execute
//
// Формат .shadergraph: поток JSON-документов, разделённых пустой строкой ("}\n\n{").
// Первый документ — GraphData (в нём же m_Edges), далее target/subtarget, далее на каждый
// элемент: [документ свойства][документы слотов][документ ноды].
// Слоты нод ссылаются на документы слотов по m_ObjectId; в документе слота m_Id — ЧИСЛОВОЙ
// рантайм-id, именно он используется в m_Edges (у NormalFromHeight он не совпадает с порядком
// массива: In = 0, Strength = 2, Out = 1 — поэтому шаблоны клонируются как есть).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class TerrainGraphBuilder
{
    const string OutPath  = "Assets/_Project/Materials/Terrain/Terrain.shadergraph";
    const string HlslPath = "Assets/_Project/Shaders/ProceduralTerrainNoise.hlsl";

    // ---------------------------------------------------------------- шаблоны

    class SlotT
    {
        public string doc;      // полный документ слота
        public string obj;      // m_ObjectId донора
        public int num;         // m_Id (рантайм-id)
        public string disp;     // m_DisplayName
        public int kind;        // m_SlotType: 0 = вход, 1 = выход
        public string stype;    // m_Type
    }

    class NodeT
    {
        public string doc;
        public string obj;
        public string stype;
        public List<SlotT> slots = new List<SlotT>();
        public int InputCount { get { int c = 0; foreach (var s in slots) if (s.kind == 0) c++; return c; } }
        public SlotT Out { get { foreach (var s in slots) if (s.kind == 1) return s; return null; } }
        public SlotT In(int i)
        {
            int c = 0;
            foreach (var s in slots) { if (s.kind == 0) { if (c == i) return s; c++; } }
            return null;
        }
        public SlotT ByName(string disp, int kind = -1)
        {
            foreach (var s in slots) if (s.disp == disp && (kind < 0 || s.kind == kind)) return s;
            return null;
        }
    }

    static readonly Dictionary<string, NodeT> nodeTpl = new Dictionary<string, NodeT>();
    static readonly Dictionary<string, string> slotTplDoc = new Dictionary<string, string>();
    static readonly Dictionary<string, string> slotTplObj = new Dictionary<string, string>();
    static readonly Dictionary<string, string> slotTplDisp = new Dictionary<string, string>();
    static string graphDataTpl, targetTpl, subTargetTpl, propFloatTpl, propColorTpl;
    static string propFloatObj, propColorObj, graphDataObj;
    static bool blocksListedInNodes;

    // ---------------------------------------------------------------- модель

    class SlotRec
    {
        public string doc;
        public string obj;
        public int num;
        public string disp;
        public int kind;
    }

    class SNode
    {
        public string kind;                 // "CF" | "Prop" | "Block" | donor node type
        public string name;
        public string id;
        public string tplDoc;
        public string tplObj;
        public List<SlotRec> slots = new List<SlotRec>();
        public List<KeyValuePair<string, string>> ov = new List<KeyValuePair<string, string>>();
        public float px, py;
        public string propDoc;              // для PropertyNode — документ свойства
        public int OutNum { get { foreach (var s in slots) if (s.kind == 1) return s.num; throw new Exception("нет выходного слота у " + kind); } }
    }

    class Port { public SNode node; public int slot; public Port(SNode n, int s) { node = n; slot = s; } }

    static readonly List<SNode> nodes = new List<SNode>();
    static readonly List<SNode> blocks = new List<SNode>();
    static readonly List<string> propDocs = new List<string>();
    static readonly List<string> propObjs = new List<string>();
    static readonly List<string[]> edges = new List<string[]>();
    static readonly Dictionary<string, SNode> propNodes = new Dictionary<string, SNode>();
    static readonly Dictionary<string, string> propRefToObj = new Dictionary<string, string>();
    static readonly List<SNode> blockOutOrder = new List<SNode>();
    static float cursorX = -6000, cursorY = -3000;

    // ---------------------------------------------------------------- утилиты

    static string GuidN() { return Guid.NewGuid().ToString("N"); }
    static string GuidD() { return Guid.NewGuid().ToString("D"); }

    static string Str(string doc, string key)
    {
        var m = Regex.Match(doc, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    static string Num(string doc, string key)
    {
        var m = Regex.Match(doc, "\"" + key + "\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)");
        return m.Success ? m.Groups[1].Value : null;
    }

    static string SetNum(string doc, string key, string value)
    {
        return Regex.Replace(doc, "(\"" + key + "\"\\s*:\\s*)-?[0-9]+(?:\\.[0-9]+)?", "${1}" + value, RegexOptions.None);
    }

    static string SetStr(string doc, string key, string value)
    {
        return Regex.Replace(doc, "(\"" + key + "\"\\s*:\\s*\")[^\"]*(\")", "${1}" + value + "${2}");
    }

    static string SetRaw(string doc, string key, string raw)
    {
        return Regex.Replace(doc,
            "(\"" + key + "\"\\s*:\\s*)(\\{[^{}]*\\}|\\[[^\\[\\]]*\\]|\"[^\"]*\"|-?[0-9]+(?:\\.[0-9]+)?|true|false)",
            "${1}" + raw);
    }

    static int MatchClose(string s, int openIdx)
    {
        char open = s[openIdx];
        char close = open == '[' ? ']' : '}';
        int depth = 0;
        bool inStr = false;
        for (int i = openIdx; i < s.Length; i++)
        {
            char c = s[i];
            if (inStr) { if (c == '\\') { i++; continue; } if (c == '"') inStr = false; continue; }
            if (c == '"') { inStr = true; continue; }
            if (c == open) depth++;
            else if (c == close) { depth--; if (depth == 0) return i; }
        }
        throw new Exception("незакрытая скобка на " + openIdx);
    }

    // заменяет массив по ключу; from = с какого индекса документа искать ключ
    static string SetArray(string doc, string key, string newArray, int from = 0)
    {
        int k = doc.IndexOf("\"" + key + "\"", from);
        if (k < 0) throw new Exception("ключ не найден: " + key);
        int open = doc.IndexOf('[', k);
        int close = MatchClose(doc, open);
        return doc.Substring(0, open) + newArray + doc.Substring(close + 1);
    }

    static string TrySetArray(string doc, string key, string newArray)
    {
        return doc.Contains("\"" + key + "\"") ? SetArray(doc, key, newArray) : doc;
    }

    static string SetPosition(string doc, float x, float y)
    {
        int k = doc.IndexOf("\"m_Position\"");
        if (k < 0) return doc;
        int open = doc.IndexOf('{', k);
        int close = MatchClose(doc, open);
        string block = doc.Substring(open, close - open + 1);
        block = Regex.Replace(block, "(\"x\"\\s*:\\s*)-?[0-9.]+", "${1}" + x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        block = Regex.Replace(block, "(\"y\"\\s*:\\s*)-?[0-9.]+", "${1}" + y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return doc.Substring(0, open) + block + doc.Substring(close + 1);
    }

    // Разделитель "}\n\n{" съедает ЗАКРЫВАЮЩУЮ скобку предыдущего документа и ОТКРЫВАЮЩУЮ
    // следующего — восстанавливаем обе.
    static List<string> SplitDocs(string text)
    {
        string[] parts = text.Split(new string[] { "}\n\n{" }, StringSplitOptions.None);
        var res = new List<string>();
        for (int i = 0; i < parts.Length; i++)
            res.Add(((i == 0 ? "" : "{") + parts[i] + (i == parts.Length - 1 ? "" : "}")).Trim());
        return res;
    }

    static string FindGraph(string fileName)
    {
        string[] hits = Directory.GetFiles("Assets/_Project/Materials", fileName, SearchOption.AllDirectories);
        return hits.Length == 0 ? null : hits[0].Replace('\\', '/');
    }

    // ---------------------------------------------------------------- загрузка доноров

    static void LoadDonors()
    {
        string[] donors = new string[]
        {
            FindGraph("Parquet.shadergraph"),
            FindGraph("Brick.shadergraph"),
            FindGraph("Glass.shadergraph"),
            FindGraph("IronRust.shadergraph"),
            FindGraph("Bark.shadergraph"),
        };

        var used = new List<string>();
        foreach (var p in donors) if (!string.IsNullOrEmpty(p)) used.Add(p);

        foreach (var path in used)
        {
            var docs = SplitDocs(File.ReadAllText(path));
            var byObj = new Dictionary<string, string>();
            for (int i = 0; i < docs.Count; i++)
            {
                string o = Str(docs[i], "m_ObjectId");
                if (!string.IsNullOrEmpty(o)) byObj[o] = docs[i];
            }

            for (int i = 0; i < docs.Count; i++)
            {
                string d = docs[i];
                string type = Str(d, "m_Type");
                if (string.IsNullOrEmpty(type)) continue;
                string shortType = type.Substring(type.LastIndexOf('.') + 1);

                // скелет графа
                if (shortType == "GraphData" && graphDataTpl == null)
                {
                    graphDataTpl = d; graphDataObj = Str(d, "m_ObjectId");
                    // включены ли блоки в m_Nodes?
                    int nk = d.IndexOf("\"m_Nodes\"");
                    int no = d.IndexOf('[', nk);
                    int nc = MatchClose(d, no);
                    string nodesArr = d.Substring(no, nc - no + 1);
                    foreach (var dd in docs)
                    {
                        string desc = Str(dd, "m_SerializedDescriptor");
                        if (desc == null) continue;
                        string bid = Str(dd, "m_ObjectId");
                        if (bid != null && nodesArr.Contains(bid)) blocksListedInNodes = true;
                    }
                    continue;
                }
                if (shortType == "UniversalTarget" && targetTpl == null) { targetTpl = d; continue; }
                if (shortType == "UniversalLitSubTarget" && subTargetTpl == null) { subTargetTpl = d; continue; }

                // шаблоны свойств
                if (shortType == "Vector1ShaderProperty" && propFloatTpl == null) { propFloatTpl = d; propFloatObj = Str(d, "m_ObjectId"); continue; }
                if (shortType == "ColorShaderProperty" && propColorTpl == null) { propColorTpl = d; propColorObj = Str(d, "m_ObjectId"); continue; }

                // шаблоны слотов (по типу слота)
                if (shortType.Contains("MaterialSlot") && !slotTplDoc.ContainsKey(shortType))
                {
                    slotTplDoc[shortType] = d;
                    slotTplObj[shortType] = Str(d, "m_ObjectId");
                    slotTplDisp[shortType] = Str(d, "m_DisplayName");
                    continue;
                }

                // шаблоны нод
                if (shortType.EndsWith("Node"))
                {
                    string key = Str(d, "m_SerializedDescriptor") != null ? "BlockNode:" + Str(d, "m_SerializedDescriptor") : shortType;
                    if (nodeTpl.ContainsKey(key)) continue;
                    var nt = new NodeT();
                    nt.doc = d; nt.obj = Str(d, "m_ObjectId"); nt.stype = shortType;
                    var refs = Regex.Matches(d, "\"m_Id\":\\s*\"([0-9a-f]{32})\"");
                    foreach (Match rm in refs)
                    {
                        string sid = rm.Groups[1].Value;
                        if (!byObj.ContainsKey(sid)) continue;
                        string sd = byObj[sid];
                        if (!sd.Contains("m_SlotType")) continue;
                        var st = new SlotT();
                        st.doc = sd; st.obj = sid; st.disp = Str(sd, "m_DisplayName");
                        st.num = int.Parse(Num(sd, "m_Id"));
                        st.kind = int.Parse(Num(sd, "m_SlotType"));
                        st.stype = Str(sd, "m_Type").Substring(Str(sd, "m_Type").LastIndexOf('.') + 1);
                        nt.slots.Add(st);
                    }
                    nodeTpl[key] = nt;
                }
            }
        }

        if (graphDataTpl == null || targetTpl == null || subTargetTpl == null)
            throw new Exception("не загружен скелет графа");
    }

    // ---------------------------------------------------------------- создание нод

    static SlotRec CloneSlot(string slotType, string display, int num, int kind, int cap, string valueRaw)
    {
        if (!slotTplDoc.ContainsKey(slotType)) throw new Exception("нет шаблона слота " + slotType);
        string d = slotTplDoc[slotType];
        string obj = GuidN();
        d = d.Replace(slotTplObj[slotType], obj);           // m_ObjectId донора уникален в документе
        d = SetNum(d, "m_Id", num.ToString());
        d = SetStr(d, "m_DisplayName", display);
        d = SetStr(d, "m_ShaderOutputName", display);
        d = SetNum(d, "m_SlotType", kind.ToString());
        d = SetNum(d, "m_StageCapability", cap.ToString());
        if (valueRaw != null) d = SetRaw(d, "m_Value", valueRaw);
        return new SlotRec { doc = d, obj = obj, num = num, disp = display, kind = kind };
    }

    static SNode FromTemplate(string key, string myName, params string[] overrides)
    {
        if (!nodeTpl.ContainsKey(key))
        {
            var all = new List<string>(nodeTpl.Keys); all.Sort();
            throw new Exception("нет шаблона ноды " + key + "; доступны: " + string.Join(",", all.ToArray()));
        }
        var t = nodeTpl[key];
        var n = new SNode();
        n.kind = key; n.name = myName;
        n.id = GuidN();
        n.tplDoc = t.doc; n.tplObj = t.obj;
        foreach (var s in t.slots)
            n.slots.Add(new SlotRec { doc = null, obj = GuidN(), num = s.num, disp = s.disp, kind = s.kind });
        for (int i = 0; i + 1 < overrides.Length; i += 2) n.ov.Add(new KeyValuePair<string, string>(overrides[i], overrides[i + 1]));
        n.px = cursorX; n.py = cursorY; cursorY += 120;
        nodes.Add(n);
        return n;
    }

    // копирует шаблонные документы слотов донора, подменяя m_ObjectId
    static void MaterializeSlots(SNode n)
    {
        var t = nodeTpl[n.kind];
        for (int i = 0; i < n.slots.Count; i++)
        {
            string src = t.slots[i].doc;
            if (src == null) continue;
            n.slots[i].doc = src.Replace(t.slots[i].obj, n.slots[i].obj);
        }
    }

    static SNode Op(string key, params Port[] inputs)
    {
        var n = FromTemplate(key, null);
        MaterializeSlots(n);
        var t = nodeTpl[key];
        if (inputs.Length != t.InputCount)
            throw new Exception(key + ": ожидалось входов " + t.InputCount + ", дано " + inputs.Length);
        for (int i = 0; i < inputs.Length; i++)
        {
            var inSlot = t.In(i);
            Connect(inputs[i], n, inSlot.num);
        }
        return n;
    }

    static void Connect(Port from, SNode to, int toSlot)
    {
        edges.Add(new string[] { from.node.id, from.slot.ToString(), to.id, toSlot.ToString() });
    }

    static Port P(SNode n) { return new Port(n, n.OutNum); }
    static Port P(SNode n, string outDisplay)
    {
        foreach (var s in n.slots) if (s.disp == outDisplay) return new Port(n, s.num);
        var names = new List<string>(); foreach (var s in n.slots) names.Add(s.disp);
        throw new Exception(n.kind + ": нет выходного слота " + outDisplay + " (есть: " + string.Join(",", names.ToArray()) + ")");
    }

    // ---------------------------------------------------------------- свойства

    static SNode AddProperty(string refName, bool color, Vector4 value)
    {
        string display = refName.Substring(1).Replace("_", " ");
        string obj = GuidN();
        string d;
        if (color)
        {
            d = propColorTpl.Replace(propColorObj, obj);
            d = SetStr(d, "m_Name", display);
            d = SetStr(d, "m_RefNameGeneratedByDisplayName", display);
            d = SetStr(d, "m_DefaultReferenceName", refName);
            d = SetStr(d, "m_OverrideReferenceName", "");
            d = SetRaw(d, "m_Value", string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{{ \"r\": {0}, \"g\": {1}, \"b\": {2}, \"a\": {3} }}", value.x, value.y, value.z, value.w));
            d = Regex.Replace(d, "(\"m_GuidSerialized\"\\s*:\\s*\")[^\"]*(\")", "${1}" + GuidD() + "${2}");
        }
        else
        {
            d = propFloatTpl.Replace(propFloatObj, obj);
            d = SetStr(d, "m_Name", display);
            d = SetStr(d, "m_RefNameGeneratedByDisplayName", display);
            d = SetStr(d, "m_DefaultReferenceName", refName);
            d = SetStr(d, "m_OverrideReferenceName", "");
            d = SetNum(d, "m_Value", value.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            d = Regex.Replace(d, "(\"m_GuidSerialized\"\\s*:\\s*\")[^\"]*(\")", "${1}" + GuidD() + "${2}");
        }
        propDocs.Add(d);
        propObjs.Add(obj);
        propRefToObj[refName] = obj;
        return null;
    }

    static SNode Prop(string refName)
    {
        if (propNodes.ContainsKey(refName)) return propNodes[refName];
        if (!propRefToObj.ContainsKey(refName)) throw new Exception("свойство не объявлено: " + refName);
        var n = FromTemplate("PropertyNode", refName);
        // выходной слот по типу свойства
        bool color = propDocs[propObjs.IndexOf(propRefToObj[refName])].Contains("ColorShaderProperty");
        string st = color ? "Vector4MaterialSlot" : "Vector1MaterialSlot";
        string display = refName.Substring(1).Replace("_", " ");
        n.slots.Clear();
        n.slots.Add(CloneSlot(st, display, 0, 1, 3, null));
        n.ov.Add(new KeyValuePair<string, string>("m_Property", "{ \"m_Id\": \"" + propRefToObj[refName] + "\" }"));
        n.propDoc = propDocs[propObjs.IndexOf(propRefToObj[refName])];
        propNodes[refName] = n;
        return n;
    }

    static SNode CF(string fn, params Port[] inputs)
    {
        var n = FromTemplate("CustomFunctionNode", fn + " (Custom Function)");
        n.ov.Add(new KeyValuePair<string, string>("m_FunctionName", fn));
        n.ov.Add(new KeyValuePair<string, string>("m_FunctionSource", AssetDatabase.AssetPathToGUID(HlslPath)));
        n.ov.Add(new KeyValuePair<string, string>("m_FunctionBody", "// single-output CF"));
        string[] par = new string[inputs.Length];
        // имена параметров совпадают с сигнатурами ProceduralTerrainNoise.hlsl
        if (fn == "PCTrn_SlopeAngle") par = new string[] { "NormalWS" };
        else if (fn == "PCTrn_ZoneWarp") par = new string[] { "ObjectPos", "Scale", "Seed" };
        else par = new string[] { "ObjectPos", "Scale", "Amount", "Seed" };
        if (par.Length != inputs.Length) throw new Exception(fn + ": параметров " + par.Length + ", входов " + inputs.Length);
        n.slots.Clear();
        for (int i = 0; i < par.Length; i++)
        {
            string st = i == 0 ? "Vector3MaterialSlot" : "Vector1MaterialSlot";
            n.slots.Add(CloneSlot(st, par[i], i, 0, 2, null));
        }
        n.slots.Add(CloneSlot("Vector1MaterialSlot", "Out", par.Length, 1, 2, null));
        for (int i = 0; i < inputs.Length; i++) Connect(inputs[i], n, i);
        return n;
    }

    // ---------------------------------------------------------------- сборка

    public static string Execute()
    {
        AssetDatabase.ImportAsset(HlslPath, ImportAssetOptions.ForceUpdate);
        string hlslGuid = AssetDatabase.AssetPathToGUID(HlslPath);
        if (string.IsNullOrEmpty(hlslGuid)) throw new Exception("нет GUID у " + HlslPath);

        LoadDonors();

        // ---- свойства ----
        var C = new Func<string, float, float, float, SNode>((refName, r, g, b) =>
        { AddProperty(refName, true, new Vector4(r, g, b, 1f)); return null; });
        Func<string, float, SNode> F = (refName, v) => { AddProperty(refName, false, new Vector4(v, 0, 0, 0)); return null; };
        Func<string, SNode> S = refName => Prop(refName);

        // Порядок: сначала цвета, потом числа (порядок в инспекторе материала)
        C("_Rock_Color", 0.36f, 0.34f, 0.31f);
        C("_Rock_Deep_Color", 0.19f, 0.18f, 0.17f);
        C("_Scree_Color", 0.47f, 0.44f, 0.40f);
        C("_Low_Color", 0.28f, 0.36f, 0.22f);
        C("_Low_Patch_Color", 0.44f, 0.42f, 0.28f);
        C("_Snow_Color", 0.92f, 0.95f, 0.98f);
        C("_Ice_Color", 0.70f, 0.84f, 0.92f);
        C("_Dust_Color", 0.53f, 0.47f, 0.37f);

        F("_Seed", 0f);
        F("_Zone_Warp", 150f); F("_Zone_Warp_Scale", 0.0009f);
        F("_Low_Low", 480f); F("_Low_High", 760f);
        F("_Snow_Low", 1850f); F("_Snow_High", 2300f);
        F("_Ice_Low", 3000f); F("_Ice_High", 3400f);
        F("_Rock_Slope_Start", 26f); F("_Rock_Slope_Full", 38f);
        F("_Rock_Strata_Scale", 0.0016f); F("_Rock_Strata_Amount", 0.45f);
        F("_Rock_Joint_Scale", 0.006f); F("_Rock_Joint_Amount", 0.50f);
        F("_Rock_Block_Scale", 0.02f); F("_Rock_Block_Amount", 0.30f);
        F("_Rock_Detail_Scale", 0.05f); F("_Rock_Detail_Amount", 0.25f);
        F("_Rock_Bright_Base", 0.90f); F("_Rock_Bright_Amount", 0.20f);
        F("_Scree_Scale", 0.004f); F("_Scree_Amount", 0.45f);
        F("_Low_Mottle_Scale", 0.0025f); F("_Low_Mottle_Amount", 0.35f);
        F("_Low_Patch_Scale", 0.0007f); F("_Low_Patch_Amount", 0.40f);
        F("_Low_Tone", 0.82f); F("_Low_Tone_Range", 0.18f); F("_Low_Smooth", 0.15f);
        F("_Snow_Drift_Scale", 0.0009f); F("_Snow_Drift_Amount", 0.35f);
        F("_Snow_Grain_Scale", 0.02f); F("_Snow_Grain_Amount", 0.20f);
        F("_Snow_Tone", 0.86f); F("_Snow_Tone_Range", 0.14f); F("_Snow_Smooth", 0.35f);
        F("_Ice_Crack_Scale", 0.004f); F("_Ice_Crack_Amount", 0.35f);
        F("_Ice_Tone", 0.80f); F("_Ice_Tone_Range", 0.20f); F("_Ice_Smooth", 0.90f);
        F("_Dust_Scale", 0.003f); F("_Dust_Amount", 0.25f);
        F("_Rock_Smooth_Base", 0.12f);
        F("_Smooth_Vary", 0.08f);
        F("_Rock_Bump", 0.60f); F("_Crack_Bump", 0.40f);
        F("_Snow_Bump", 0.20f); F("_Ice_Bump", 0.30f); F("_Bump_Strength", 0.50f);

        // ---- входы положения и нормали ----
        var pos = FromTemplate("PositionNode", "Position");
        MaterializeSlots(pos);
        var posY = P(Op("SplitNode", P(pos)), "G");
        var normal = FromTemplate("NormalVectorNode", "Normal Vector");
        MaterializeSlots(normal);
        var slope = CF("PCTrn_SlopeAngle", P(normal));

        // ---- шумовые слои ----
        var strata = CF("PCTrn_RockStrata", P(pos), P(S("_Rock_Strata_Scale")), P(S("_Rock_Strata_Amount")), P(S("_Seed")));
        var joints = CF("PCTrn_RockJoints", P(pos), P(S("_Rock_Joint_Scale")), P(S("_Rock_Joint_Amount")), P(S("_Seed")));
        var blockTone = CF("PCTrn_RockBlockTone", P(pos), P(S("_Rock_Block_Scale")), P(S("_Rock_Block_Amount")), P(S("_Seed")));
        var detail = CF("PCTrn_RockDetail", P(pos), P(S("_Rock_Detail_Scale")), P(S("_Rock_Detail_Amount")), P(S("_Seed")));
        var screeN = CF("PCTrn_Scree", P(pos), P(S("_Scree_Scale")), P(S("_Scree_Amount")), P(S("_Seed")));
        var drift = CF("PCTrn_SnowDrift", P(pos), P(S("_Snow_Drift_Scale")), P(S("_Snow_Drift_Amount")), P(S("_Seed")));
        var grain = CF("PCTrn_SnowGrain", P(pos), P(S("_Snow_Grain_Scale")), P(S("_Snow_Grain_Amount")), P(S("_Seed")));
        var iceCrack = CF("PCTrn_IceCracks", P(pos), P(S("_Ice_Crack_Scale")), P(S("_Ice_Crack_Amount")), P(S("_Seed")));
        var mottle = CF("PCTrn_LowMottle", P(pos), P(S("_Low_Mottle_Scale")), P(S("_Low_Mottle_Amount")), P(S("_Seed")));
        var patches = CF("PCTrn_LowPatches", P(pos), P(S("_Low_Patch_Scale")), P(S("_Low_Patch_Amount")), P(S("_Seed")));
        var dust = CF("PCTrn_Dust", P(pos), P(S("_Dust_Scale")), P(S("_Dust_Amount")), P(S("_Seed")));

        // ---- зоны ----
        var warp = CF("PCTrn_ZoneWarp", P(pos), P(S("_Zone_Warp_Scale")), P(S("_Seed")));
        var hWarped = P(Op("AddNode", posY, P(Op("MultiplyNode", P(warp), P(S("_Zone_Warp"))))));

        // Smoothstep(Edge1 = нижний порог, Edge2 = верхний, In = значение)
        var lowGate = P(Op("OneMinusNode", P(Op("SmoothstepNode", P(S("_Low_Low")), P(S("_Low_High")), hWarped))));
        var snowH = P(Op("SmoothstepNode", P(S("_Snow_Low")), P(S("_Snow_High")), hWarped));
        var iceH = P(Op("SmoothstepNode", P(S("_Ice_Low")), P(S("_Ice_High")), hWarped));
        var rockS = P(Op("SmoothstepNode", P(S("_Rock_Slope_Start")), P(S("_Rock_Slope_Full")), P(slope)));
        var flat = P(Op("OneMinusNode", rockS));
        var flatSq = P(Op("MultiplyNode", flat, flat));
        var snowMask = P(Op("MultiplyNode", snowH, flat));
        var iceMask = P(Op("MultiplyNode", iceH, flatSq));
        var screeMask = P(Op("MultiplyNode", P(screeN), flat));
        var dustMask = P(Op("MultiplyNode", P(dust), flat));

        // ---- порода ----
        var tone = P(Op("SaturateNode", P(Op("AddNode", P(strata), P(joints)))));
        var rockCol = P(Op("LerpNode", P(S("_Rock_Color")), P(S("_Rock_Deep_Color")), tone));
        var vary = P(Op("SaturateNode", P(Op("AddNode", P(blockTone), P(detail)))));
        var bright = P(Op("AddNode", P(S("_Rock_Bright_Base")), P(Op("MultiplyNode", vary, P(S("_Rock_Bright_Amount"))))));
        var rockCol2 = P(Op("MultiplyNode", rockCol, bright));
        var rockCol3 = P(Op("LerpNode", rockCol2, P(S("_Scree_Color")), screeMask));

        // ---- низины ----
        var lowCol = P(Op("LerpNode", P(S("_Low_Color")), P(S("_Low_Patch_Color")), P(patches)));
        var lowTone = P(Op("AddNode", P(S("_Low_Tone")), P(Op("MultiplyNode", P(mottle), P(S("_Low_Tone_Range"))))));
        var lowCol2 = P(Op("MultiplyNode", lowCol, lowTone));

        // ---- снег ----
        var snowTone = P(Op("AddNode", P(S("_Snow_Tone")), P(Op("MultiplyNode", P(drift), P(S("_Snow_Tone_Range"))))));
        var snowCol = P(Op("MultiplyNode", P(S("_Snow_Color")), snowTone));

        // ---- лёд ----
        var iceTone = P(Op("AddNode", P(S("_Ice_Tone")), P(Op("MultiplyNode", P(iceCrack), P(S("_Ice_Tone_Range"))))));
        var iceCol = P(Op("MultiplyNode", P(S("_Ice_Color")), iceTone));

        // ---- смешение зон: порода → низины → снег → лёд → пыль ----
        var c1 = P(Op("LerpNode", rockCol3, lowCol2, lowGate));
        var c2 = P(Op("LerpNode", c1, snowCol, snowMask));
        var c3 = P(Op("LerpNode", c2, iceCol, iceMask));
        var baseColor = P(Op("LerpNode", c3, P(S("_Dust_Color")), dustMask));

        // ---- гладкость ----
        var s1 = P(Op("LerpNode", P(S("_Rock_Smooth_Base")), P(S("_Low_Smooth")), lowGate));
        var s2 = P(Op("LerpNode", s1, P(S("_Snow_Smooth")), snowMask));
        var s3 = P(Op("LerpNode", s2, P(S("_Ice_Smooth")), iceMask));
        var smoothness = P(Op("SaturateNode", P(Op("AddNode", s3, P(Op("MultiplyNode", P(joints), P(S("_Smooth_Vary"))))))));

        // ---- рельеф (Normal From Height) ----
        var bump = P(Op("AddNode",
            P(Op("AddNode",
                P(Op("AddNode", P(Op("MultiplyNode", P(strata), P(S("_Rock_Bump")))), P(Op("MultiplyNode", P(joints), P(S("_Crack_Bump")))))),
                P(Op("MultiplyNode", P(Op("AddNode", P(drift), P(grain))), P(S("_Snow_Bump")))))),
            P(Op("MultiplyNode", P(iceCrack), P(S("_Ice_Bump"))))));
        // рантайм-ids NFH не совпадают с порядком массива (In = 0, Strength = 2, Out = 1),
        // поэтому берём их из шаблона по имени слота, а не по индексу.
        var nfhTpl = nodeTpl["NormalFromHeightNode"];
        var nfhIn = nfhTpl.ByName("In", 0);
        var nfhSt = nfhTpl.ByName("Strength", 0);
        var nfhOut = nfhTpl.Out;
        if (nfhIn == null || nfhSt == null || nfhOut == null)
            throw new Exception("NormalFromHeightNode: не найдены слоты In/Strength/Out");
        var nfh = FromTemplate("NormalFromHeightNode", "Normal From Height");
        MaterializeSlots(nfh);
        Connect(bump, nfh, nfhIn.num);
        Connect(P(S("_Bump_Strength")), nfh, nfhSt.num);
        var normalTS = new Port(nfh, nfhOut.num);

        // ---- блоки ----
        AddBlock("SurfaceDescription.BaseColor", baseColor);
        AddBlock("SurfaceDescription.Smoothness", smoothness);
        AddBlock("SurfaceDescription.NormalTS", normalTS);
        AddBlockValue("SurfaceDescription.Metallic", 0f);
        AddBlockValue("SurfaceDescription.Occlusion", 1f);
        AddBlockValue("SurfaceDescription.Emission", 0f);
        AddVertexBlock("VertexDescription.Position");
        AddVertexBlock("VertexDescription.Normal");
        AddVertexBlock("VertexDescription.Tangent");

        // ------------------------------------------------------------ запись
        var outDocs = new List<string>();
        var gd = graphDataTpl;

        // контексты: заменить m_Blocks
        int vk = gd.IndexOf("\"m_VertexContext\"");
        string vBlocks = "[";
        string fBlocks = "[";
        foreach (var b in blocks)
        {
            bool vertex = b.kind.Contains("VertexDescription");
            string entry = "{ \"m_Id\": \"" + b.id + "\" }";
            if (vertex) vBlocks += (vBlocks.Length > 1 ? ", " : "") + entry;
            else fBlocks += (fBlocks.Length > 1 ? ", " : "") + entry;
        }
        vBlocks += "]"; fBlocks += "]";
        // m_Blocks внутри m_VertexContext
        int vOpen = gd.IndexOf('{', vk);
        int vClose = MatchClose(gd, vOpen);
        string vCtx = gd.Substring(vOpen, vClose - vOpen + 1);
        vCtx = SetArray(vCtx, "m_Blocks", vBlocks);
        gd = gd.Substring(0, vOpen) + vCtx + gd.Substring(vClose + 1);
        int fk = gd.IndexOf("\"m_FragmentContext\"");
        int fOpen = gd.IndexOf('{', fk);
        int fClose = MatchClose(gd, fOpen);
        string fCtx = gd.Substring(fOpen, fClose - fOpen + 1);
        fCtx = SetArray(fCtx, "m_Blocks", fBlocks);
        gd = gd.Substring(0, fOpen) + fCtx + gd.Substring(fClose + 1);

        // списки свойств, нод, рёбер
        var propList = new StringBuilder("[");
        for (int i = 0; i < propObjs.Count; i++) propList.Append((i > 0 ? ", " : "") + "{ \"m_Id\": \"" + propObjs[i] + "\" }");
        propList.Append("]");
        gd = SetArray(gd, "m_Properties", propList.ToString());
        // группы/стикеры/ключевые слова/дропдауны донора ссылаются на его ноды — чистим
        gd = TrySetArray(gd, "m_CategoryData", "[]");
        gd = TrySetArray(gd, "m_GroupDatas", "[]");
        gd = TrySetArray(gd, "m_StickyNoteDatas", "[]");
        gd = TrySetArray(gd, "m_SubDatas", "[]");
        gd = TrySetArray(gd, "m_Dropdowns", "[]");
        gd = TrySetArray(gd, "m_Keywords", "[]");

        var nodeList = new StringBuilder("[");
        var allNodes = new List<SNode>();
        allNodes.AddRange(nodes);
        allNodes.AddRange(blocks);
        for (int i = 0; i < allNodes.Count; i++)
        {
            if (!blocksListedInNodes && allNodes[i].kind.Contains("Description")) continue;
            nodeList.Append((nodeList.Length > 1 ? ", " : "") + "{ \"m_Id\": \"" + allNodes[i].id + "\" }");
        }
        nodeList.Append("]");
        gd = SetArray(gd, "m_Nodes", nodeList.ToString());

        var edgeList = new StringBuilder("[");
        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i];
            edgeList.Append((i > 0 ? ", " : "") + "{ \"m_OutputSlot\": { \"m_Node\": { \"m_Id\": \"" + e[0] + "\" }, \"m_SlotId\": " + e[1] + " }, \"m_InputSlot\": { \"m_Node\": { \"m_Id\": \"" + e[2] + "\" }, \"m_SlotId\": " + e[3] + " } }");
        }
        edgeList.Append("]");
        gd = SetArray(gd, "m_Edges", edgeList.ToString());

        outDocs.Add(gd);
        outDocs.Add(targetTpl);
        outDocs.Add(subTargetTpl);

        foreach (var n in nodes)
        {
            if (n.propDoc != null) { outDocs.Add(n.propDoc); n.propDoc = null; }   // свойство — один раз, перед своей нодой
            foreach (var s in n.slots) if (s.doc != null) outDocs.Add(s.doc);
            outDocs.Add(EmitNode(n));
        }
        foreach (var b in blocks)
        {
            foreach (var s in b.slots) if (s.doc != null) outDocs.Add(s.doc);
            outDocs.Add(EmitNode(b));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
        AssetDatabase.Refresh();
        File.WriteAllText(OutPath, string.Join("\n\n", outDocs.ToArray()) + "\n");
        AssetDatabase.ImportAsset(OutPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        int cfCount = 0, propCount = 0;
        foreach (var n in nodes) { if (n.kind == "CustomFunctionNode") cfCount++; if (n.kind == "PropertyNode") propCount++; }

        return "docs=" + outDocs.Count + " nodes=" + (nodes.Count + blocks.Count) + " (CF=" + cfCount + ", Property=" + propCount + ", math=" + (nodes.Count - cfCount - propCount) + ") blocks=" + blocks.Count
            + " props=" + propObjs.Count + " edges=" + edges.Count + " hlslGuid=" + hlslGuid + " sha=" + AssetDatabase.AssetPathToGUID(OutPath)
            + " bytes=" + new FileInfo(OutPath).Length;
    }

    // FromTemplate кладёт ноду в nodes; для блоков это неверно — они живут только в blocks.
    static SNode BlockFrom(string descriptor)
    {
        var b = FromTemplate("BlockNode:" + descriptor, descriptor);
        MaterializeSlots(b);
        nodes.Remove(b);
        blocks.Add(b);
        return b;
    }

    static void AddBlock(string descriptor, Port input)
    {
        var b = BlockFrom(descriptor);
        Connect(input, b, b.slots[0].num);
    }

    // литерал m_Value строим по типу слота блока: Vector1 -> число, Vector3/4 -> объект
    static void AddBlockValue(string descriptor, float v)
    {
        string key = "BlockNode:" + descriptor;
        var b = BlockFrom(descriptor);
        string st = nodeTpl[key].slots[0].stype;
        string n = v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string raw;
        if (st == "Vector1MaterialSlot") raw = n;
        else if (st == "Vector4MaterialSlot") raw = "{\"x\":" + n + ",\"y\":" + n + ",\"z\":" + n + ",\"w\":" + n + "}";
        else raw = "{\"x\":" + n + ",\"y\":" + n + ",\"z\":" + n + "}";
        b.slots[0].doc = SetRaw(b.slots[0].doc, "m_Value", raw);
    }

    static void AddVertexBlock(string descriptor)
    {
        BlockFrom(descriptor);
    }

    static string EmitNode(SNode n)
    {
        string d;
        if (n.tplDoc != null)
        {
            d = n.tplDoc;
            d = d.Replace(n.tplObj, n.id);
            if (n.name != null && !n.kind.Contains("Description")) d = SetStr(d, "m_Name", n.name);
            var refs = new StringBuilder("[");
            for (int i = 0; i < n.slots.Count; i++) refs.Append((i > 0 ? ", " : "") + "{ \"m_Id\": \"" + n.slots[i].obj + "\" }");
            refs.Append("]");
            d = SetArray(d, "m_Slots", refs.ToString());
            foreach (var kv in n.ov)
            {
                if (kv.Key == "m_Property") d = SetRaw(d, "m_Property", kv.Value);
                else if (kv.Key == "m_FunctionName" || kv.Key == "m_FunctionSource" || kv.Key == "m_FunctionBody") d = SetStr(d, kv.Key, kv.Value);
                else d = SetRaw(d, kv.Key, kv.Value);
            }
            d = SetPosition(d, n.px, n.py);
            d = SetRaw(d, "m_Group", "{\"m_Id\": \"\"}");
        }
        else throw new Exception("нет документа для ноды " + n.kind);
        return d.Trim();
    }
}
