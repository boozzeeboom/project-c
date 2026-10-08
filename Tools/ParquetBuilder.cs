using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// ParquetBuilder — собирает Assets/_Project/Materials/Parquet/Parquet.shadergraph
// как производную от готового графа Wood: сохраняет всё дерево (кольца, волокно,
// поры, сучки, кромки, царапины) и добавляет три CF-ноды раскладки пластов
// (Layout / Joints / Tone) + 14 свойств и их обвязку.
public static class ParquetBuilder
{
    static string Root;
    static readonly List<string> Order = new List<string>();
    static readonly Dictionary<string, string> ById = new Dictionary<string, string>();
    static readonly Dictionary<string, string> TypeOf = new Dictionary<string, string>();

    class Edge { public string S; public int SS; public string D; public int DS; }
    static readonly List<Edge> Edges = new List<Edge>();

    static readonly Dictionary<string, string> NewNodeDoc = new Dictionary<string, string>();
    static readonly List<string> NewNodeOrder = new List<string>();
    static readonly List<string> NewSlotDocs = new List<string>();
    static readonly Dictionary<string, string> NewPropDoc = new Dictionary<string, string>();
    static readonly List<string> NewPropOrder = new List<string>();

    const string SrcGraph = "Assets/_Project/Materials/Wood/Wood.shadergraph";
    const string OutFolder = "Assets/_Project/Materials/Parquet";
    const string OutGraph = OutFolder + "/Parquet.shadergraph";
    const string ParquetHlsl = "Assets/_Project/Shaders/ProceduralParquetNoise.hlsl";
    const string WoodHlsl = "Assets/_Project/Shaders/ProceduralWoodNoise.hlsl";

    public static string Execute()
    {
        var log = new StringBuilder();
        Root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');

        AssetDatabase.ImportAsset(ParquetHlsl, ImportAssetOptions.ForceUpdate);
        string pGuid = AssetDatabase.AssetPathToGUID(ParquetHlsl);
        string wGuid = AssetDatabase.AssetPathToGUID(WoodHlsl);
        log.AppendLine("hlsl: parquet=" + pGuid + " wood=" + wGuid);

        string txt = File.ReadAllText(Root + "/" + SrcGraph);
        Load(txt);
        Edges.AddRange(ParseEdges(txt));
        log.AppendLine("source: docs=" + Order.Count + " ids=" + ById.Count + " edges=" + Edges.Count);

        string gdId = FirstOfType("GraphData");
        string posId = FirstOfType("PositionNode");
        string nfhId = FirstOfType("NormalFromHeightNode");
        string cfKnots = CfByFunc("ProceduralWoodKnots");
        string cfRings = CfByFunc("ProceduralWoodRings");
        string cfGrain = CfByFunc("ProceduralWoodGrain");
        string cfPores = CfByFunc("ProceduralWoodPores");
        string baseBlock = BlockOf("SurfaceDescription.BaseColor");
        string smoothBlock = BlockOf("SurfaceDescription.Smoothness");
        string woodColorPn = PropNodeOf("_Wood_Color");
        string tmplProp1 = PropNodeOfType("Vector1ShaderProperty");
        string tmplProp4 = PropNodeOfType("ColorShaderProperty");
        string tmplProp1Doc = FirstOfType("Vector1ShaderProperty");
        string tmplProp4Doc = FirstOfType("ColorShaderProperty");
        string tmplMul = FirstOfType("MultiplyNode");
        string tmplLerp = FirstOfType("LerpNode");
        string tmplSub = FirstOfType("SubtractNode");
        string tmplSat = FirstOfType("SaturateNode");
        string tmplAdd = FirstOfType("AddNode");
        string tmplVec3Slot = SlotIds(ById[cfKnots])[0];
        string tmplFloatSlot = SlotIds(ById[cfKnots])[1];
        log.AppendLine("discover: gd=" + S(gdId) + " pos=" + S(posId) + " nfh=" + S(nfhId) + " knots=" + S(cfKnots)
            + " rings=" + S(cfRings) + " grain=" + S(cfGrain) + " pores=" + S(cfPores)
            + " baseBlock=" + S(baseBlock) + " smoothBlock=" + S(smoothBlock) + " woodColorPn=" + S(woodColorPn));
        log.AppendLine("colorPropTemplate:\n" + ById[tmplProp4Doc]);
        if (tmplSub == null) tmplSub = tmplAdd;

        Edge eBase = EdgeInto(baseBlock, 0);
        Edge eSmooth = EdgeInto(smoothBlock, 0);
        Edge eHeight = EdgeInto(nfhId, 0);
        Edge eWood = EdgeFrom(woodColorPn);
        if (eBase == null || eSmooth == null || eHeight == null || eWood == null)
        {
            log.AppendLine("ABORT: не найдены входные рёбра (base/smooth/height/woodColor)");
            return log.ToString();
        }
        string lerp1 = eWood.D;
        log.AppendLine("rewire: base<=" + S(eBase.S) + "#" + eBase.SS + " smooth<=" + S(eSmooth.S) + "#" + eSmooth.SS
            + " height<=" + S(eHeight.S) + "#" + eHeight.SS + " lerp1=" + S(lerp1) + " (A slot " + eWood.DS + ")");

        // ── новые свойства ────────────────────────────────────────────────────
        string pScale = NewFloatProp("Plank Scale", "_Plank_Scale", 1.5f);
        string pAspect = NewFloatProp("Plank Aspect", "_Plank_Aspect", 4.5f);
        string pRowOff = NewFloatProp("Plank Row Offset", "_Plank_Row_Offset", 0.5f);
        string pStagger = NewFloatProp("Plank Stagger", "_Plank_Stagger", 3f);
        string pWarp = NewFloatProp("Plank Warp", "_Plank_Warp", 0.06f);
        string pSeed = NewFloatProp("Plank Seed", "_Plank_Seed", 0f);
        string pVary = NewFloatProp("Plank Vary", "_Plank_Vary", 0.65f);
        string pJwx = NewFloatProp("Joint Width X", "_Joint_Width_X", 0.04f);
        string pJwy = NewFloatProp("Joint Width Y", "_Joint_Width_Y", 0.05f);
        string pJamt = NewFloatProp("Joint Amount", "_Joint_Amount", 1f);
        string pJgloss = NewFloatProp("Joint Gloss", "_Joint_Gloss", 0.15f);
        string pJbump = NewFloatProp("Joint Bump", "_Joint_Bump", -0.6f);
        string pPlankCol = NewColorProp("Plank Color", "_Plank_Color", 0.50f, 0.34f, 0.19f);
        string pJointCol = NewColorProp("Joint Color", "_Joint_Color", 0.06f, 0.04f, 0.02f);
        log.AppendLine("props: " + NewPropOrder.Count);

        string pnScale = PropNode(tmplProp1, pScale, "Plank Scale", 0);
        string pnAspect = PropNode(tmplProp1, pAspect, "Plank Aspect", 1);
        string pnRowOff = PropNode(tmplProp1, pRowOff, "Plank Row Offset", 2);
        string pnStagger = PropNode(tmplProp1, pStagger, "Plank Stagger", 3);
        string pnWarp = PropNode(tmplProp1, pWarp, "Plank Warp", 4);
        string pnSeed = PropNode(tmplProp1, pSeed, "Plank Seed", 5);
        string pnVary = PropNode(tmplProp1, pVary, "Plank Vary", 6);
        string pnJwx = PropNode(tmplProp1, pJwx, "Joint Width X", 7);
        string pnJwy = PropNode(tmplProp1, pJwy, "Joint Width Y", 8);
        string pnJamt = PropNode(tmplProp1, pJamt, "Joint Amount", 9);
        string pnJgloss = PropNode(tmplProp1, pJgloss, "Joint Gloss", 10);
        string pnJbump = PropNode(tmplProp1, pJbump, "Joint Bump", 11);
        string pnPlankCol = PropNode(tmplProp4, pPlankCol, "Plank Color", 12);
        string pnJointCol = PropNode(tmplProp4, pJointCol, "Joint Color", 13);

        // ── CF-ноды раскладки ─────────────────────────────────────────────────
        string cfL = NewNode(cfKnots, "ProceduralParquetLayoutFaces (Custom Function)", false, null);
        NewNodeDoc[cfL] = SetStr(NewNodeDoc[cfL], "m_FunctionName", "ProceduralParquetLayoutFaces");
        NewNodeDoc[cfL] = SetStr(NewNodeDoc[cfL], "m_FunctionSource", pGuid);
        AddSlot(cfL, tmplVec3Slot, "Object Pos", "ObjectPos", 0, 0);
        AddSlot(cfL, tmplVec3Slot, "Object Normal", "ObjectNormal", 1, 0);
        AddSlot(cfL, tmplFloatSlot, "Plank Scale", "PlankScale", 2, 0);
        AddSlot(cfL, tmplFloatSlot, "Plank Aspect", "PlankAspect", 3, 0);
        AddSlot(cfL, tmplFloatSlot, "Row Offset", "RowOffset", 4, 0);
        AddSlot(cfL, tmplFloatSlot, "Stagger", "Stagger", 5, 0);
        AddSlot(cfL, tmplFloatSlot, "Plank Warp", "PlankWarp", 6, 0);
        AddSlot(cfL, tmplFloatSlot, "Plank Seed", "PlankSeed", 7, 0);
        AddSlot(cfL, tmplVec3Slot, "Plank Local", "PlankLocal", 8, 1);

        string cfJ = NewNode(cfKnots, "ProceduralParquetJointsFaces (Custom Function)", false, null);
        NewNodeDoc[cfJ] = SetStr(NewNodeDoc[cfJ], "m_FunctionName", "ProceduralParquetJointsFaces");
        NewNodeDoc[cfJ] = SetStr(NewNodeDoc[cfJ], "m_FunctionSource", pGuid);
        AddSlot(cfJ, tmplVec3Slot, "Object Pos", "ObjectPos", 0, 0);
        AddSlot(cfJ, tmplVec3Slot, "Object Normal", "ObjectNormal", 1, 0);
        AddSlot(cfJ, tmplFloatSlot, "Plank Scale", "PlankScale", 2, 0);
        AddSlot(cfJ, tmplFloatSlot, "Plank Aspect", "PlankAspect", 3, 0);
        AddSlot(cfJ, tmplFloatSlot, "Row Offset", "RowOffset", 4, 0);
        AddSlot(cfJ, tmplFloatSlot, "Stagger", "Stagger", 5, 0);
        AddSlot(cfJ, tmplFloatSlot, "Plank Warp", "PlankWarp", 6, 0);
        AddSlot(cfJ, tmplFloatSlot, "Joint Width X", "JointWidthX", 7, 0);
        AddSlot(cfJ, tmplFloatSlot, "Joint Width Y", "JointWidthY", 8, 0);
        AddSlot(cfJ, tmplFloatSlot, "Plank Seed", "PlankSeed", 9, 0);
        AddSlot(cfJ, tmplFloatSlot, "Joints", "Joints", 10, 1);

        string cfT = NewNode(cfKnots, "ProceduralParquetToneFaces (Custom Function)", false, null);
        NewNodeDoc[cfT] = SetStr(NewNodeDoc[cfT], "m_FunctionName", "ProceduralParquetToneFaces");
        NewNodeDoc[cfT] = SetStr(NewNodeDoc[cfT], "m_FunctionSource", pGuid);
        AddSlot(cfT, tmplVec3Slot, "Object Pos", "ObjectPos", 0, 0);
        AddSlot(cfT, tmplVec3Slot, "Object Normal", "ObjectNormal", 1, 0);
        AddSlot(cfT, tmplFloatSlot, "Plank Scale", "PlankScale", 2, 0);
        AddSlot(cfT, tmplFloatSlot, "Plank Aspect", "PlankAspect", 3, 0);
        AddSlot(cfT, tmplFloatSlot, "Row Offset", "RowOffset", 4, 0);
        AddSlot(cfT, tmplFloatSlot, "Stagger", "Stagger", 5, 0);
        AddSlot(cfT, tmplFloatSlot, "Plank Warp", "PlankWarp", 6, 0);
        AddSlot(cfT, tmplFloatSlot, "Plank Seed", "PlankSeed", 7, 0);
        AddSlot(cfT, tmplFloatSlot, "Plank Tone", "PlankTone", 8, 1);

        // ── нормаль объекта: выбор плоскости раскладки по грани ───────────────
        string nvObj = NewNode(posId, "Normal Vector", true, null);
        NewNodeDoc[nvObj] = SetStr(NewNodeDoc[nvObj], "m_Type", "UnityEditor.ShaderGraph.NormalVectorNode");
        Pos(nvObj, -4200, -2600);

        // ── обвязка ───────────────────────────────────────────────────────────
        string mulToneVary = NewNode(tmplMul, "Multiply", true, null);
        string lerpPlank = NewNode(tmplLerp, "Lerp", true, null);
        string mulJamt = NewNode(tmplMul, "Multiply", true, null);
        string lerpJoint = NewNode(tmplLerp, "Lerp", true, null);
        string mulJgloss = NewNode(tmplMul, "Multiply", true, null);
        string subGloss = NewNode(tmplSub, "Subtract", true, null);
        string satGloss = NewNode(tmplSat, "Saturate", true, null);
        string mulJbump = NewNode(tmplMul, "Multiply", true, null);
        string addHeight = NewNode(tmplAdd, "Add", true, null);

        int n0 = NewNodeOrder.Count - 9;
        Pos(NewNodeOrder[n0 + 0], -7200, 4200); Pos(NewNodeOrder[n0 + 1], -6600, 4200);
        Pos(NewNodeOrder[n0 + 2], -7200, 4700); Pos(NewNodeOrder[n0 + 3], -6600, 4700);
        Pos(NewNodeOrder[n0 + 4], -7200, 5200); Pos(NewNodeOrder[n0 + 5], -6600, 5200);
        Pos(NewNodeOrder[n0 + 6], -6600, 5700); Pos(NewNodeOrder[n0 + 7], -6000, 5700);
        Pos(NewNodeOrder[n0 + 8], -5400, 4200);

        var newEdges = new List<Edge>();

        newEdges.Add(E(posId, 0, cfL, 0));
        newEdges.Add(E(nvObj, 0, cfL, 1));
        newEdges.Add(E(pnScale, 0, cfL, 2));
        newEdges.Add(E(pnAspect, 0, cfL, 3));
        newEdges.Add(E(pnRowOff, 0, cfL, 4));
        newEdges.Add(E(pnStagger, 0, cfL, 5));
        newEdges.Add(E(pnWarp, 0, cfL, 6));
        newEdges.Add(E(pnSeed, 0, cfL, 7));
        newEdges.Add(E(cfL, 8, cfKnots, 0));
        newEdges.Add(E(cfL, 8, cfRings, 0));
        newEdges.Add(E(cfL, 8, cfGrain, 0));
        newEdges.Add(E(cfL, 8, cfPores, 0));

        newEdges.Add(E(posId, 0, cfJ, 0));
        newEdges.Add(E(nvObj, 0, cfJ, 1));
        newEdges.Add(E(pnScale, 0, cfJ, 2));
        newEdges.Add(E(pnAspect, 0, cfJ, 3));
        newEdges.Add(E(pnRowOff, 0, cfJ, 4));
        newEdges.Add(E(pnStagger, 0, cfJ, 5));
        newEdges.Add(E(pnWarp, 0, cfJ, 6));
        newEdges.Add(E(pnJwx, 0, cfJ, 7));
        newEdges.Add(E(pnJwy, 0, cfJ, 8));
        newEdges.Add(E(pnSeed, 0, cfJ, 9));
        newEdges.Add(E(cfJ, 10, mulJamt, 0));
        newEdges.Add(E(cfJ, 10, mulJgloss, 0));
        newEdges.Add(E(cfJ, 10, mulJbump, 0));

        newEdges.Add(E(posId, 0, cfT, 0));
        newEdges.Add(E(nvObj, 0, cfT, 1));
        newEdges.Add(E(pnScale, 0, cfT, 2));
        newEdges.Add(E(pnAspect, 0, cfT, 3));
        newEdges.Add(E(pnRowOff, 0, cfT, 4));
        newEdges.Add(E(pnStagger, 0, cfT, 5));
        newEdges.Add(E(pnWarp, 0, cfT, 6));
        newEdges.Add(E(pnSeed, 0, cfT, 7));

        newEdges.Add(E(cfT, 8, mulToneVary, 0));
        newEdges.Add(E(pnVary, 0, mulToneVary, 1));
        newEdges.Add(E(woodColorPn, 0, lerpPlank, 0));
        newEdges.Add(E(pnPlankCol, 0, lerpPlank, 1));
        newEdges.Add(E(mulToneVary, 2, lerpPlank, 2));
        newEdges.Add(E(lerpPlank, 3, lerp1, eWood.DS));

        newEdges.Add(E(pnJamt, 0, mulJamt, 1));
        newEdges.Add(E(eBase.S, eBase.SS, lerpJoint, 0));
        newEdges.Add(E(pnJointCol, 0, lerpJoint, 1));
        newEdges.Add(E(mulJamt, 2, lerpJoint, 2));
        newEdges.Add(E(lerpJoint, 3, baseBlock, eBase.DS));

        newEdges.Add(E(pnJgloss, 0, mulJgloss, 1));
        newEdges.Add(E(eSmooth.S, eSmooth.SS, subGloss, 0));
        newEdges.Add(E(mulJgloss, 2, subGloss, 1));
        newEdges.Add(E(subGloss, 2, satGloss, 0));
        newEdges.Add(E(satGloss, 1, smoothBlock, eSmooth.DS));

        newEdges.Add(E(pnJbump, 0, mulJbump, 1));
        newEdges.Add(E(eHeight.S, eHeight.SS, addHeight, 0));
        newEdges.Add(E(mulJbump, 2, addHeight, 1));
        newEdges.Add(E(addHeight, 2, nfhId, eHeight.DS));

        var drop = new List<Edge>();
        drop.Add(E(posId, 0, cfKnots, 0));
        drop.Add(E(posId, 0, cfRings, 0));
        drop.Add(E(posId, 0, cfGrain, 0));
        drop.Add(E(posId, 0, cfPores, 0));
        drop.Add(eWood);
        drop.Add(eBase);
        drop.Add(eSmooth);
        drop.Add(eHeight);

        int removed = 0;
        var finalEdges = new List<Edge>();
        foreach (Edge e in Edges)
        {
            bool kill = false;
            foreach (Edge d in drop) if (Same(e, d)) { kill = true; break; }
            if (kill) removed++; else finalEdges.Add(e);
        }
        finalEdges.AddRange(newEdges);
        log.AppendLine("edges: source=" + Edges.Count + " removed=" + removed + " added=" + newEdges.Count + " final=" + finalEdges.Count);

        // ── списки ids графа ──────────────────────────────────────────────────
        var propsAll = IdsInArray(ById[gdId], "m_Properties");
        var seen = new HashSet<string>();
        foreach (string s in propsAll) seen.Add(s);
        foreach (string id in NewPropOrder) if (seen.Add(id)) propsAll.Add(id);

        var nodesAll = IdsInArray(ById[gdId], "m_Nodes");
        var seenN = new HashSet<string>();
        foreach (string s in nodesAll) seenN.Add(s);
        foreach (string id in NewNodeOrder) if (seenN.Add(id)) nodesAll.Add(id);

        string problem = null;
        foreach (Edge e in finalEdges)
        {
            if (!seenN.Contains(e.S)) { problem = "edge src не в m_Nodes: " + S(e.S); break; }
            if (!seenN.Contains(e.D)) { problem = "edge dst не в m_Nodes: " + S(e.D); break; }
        }
        if (problem == null) foreach (string id in NewNodeOrder) if (!seenN.Contains(id)) problem = "нода не в списке: " + S(id);
        if (problem == null) foreach (string id in NewPropOrder) if (!seen.Contains(id)) problem = "свойство не в списке: " + S(id);
        if (problem != null) { log.AppendLine("ABORT: " + problem); return log.ToString(); }

        string gdNew = ById[gdId];
        gdNew = ReplaceArray(gdNew, "m_Properties", IdArray(propsAll));
        gdNew = ReplaceArray(gdNew, "m_Nodes", IdArray(nodesAll));
        gdNew = ReplaceArray(gdNew, "m_Edges", EdgeArray(finalEdges));

        var outSb = new StringBuilder();
        bool firstDoc = true;
        for (int i = 0; i < Order.Count; i++)
        {
            string id = Order[i];
            string dt = (id == gdId) ? gdNew : ById[id];
            if (!firstDoc) outSb.Append("\n\n");
            firstDoc = false;
            outSb.Append(dt);
        }
        foreach (string id in NewPropOrder) { outSb.Append("\n\n"); outSb.Append(NewPropDoc[id]); }
        foreach (string id in NewNodeOrder) { outSb.Append("\n\n"); outSb.Append(NewNodeDoc[id]); }
        foreach (string d in NewSlotDocs) { outSb.Append("\n\n"); outSb.Append(d); }

        string outText = outSb.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
        if (!outText.EndsWith("\n")) outText += "\n";

        string[] parts = Regex.Split(outText, "\n\n");
        foreach (string part in parts)
        {
            if (Regex.IsMatch(part, "\n[ \t]*\n"))
            {
                log.AppendLine("ABORT: пустая строка внутри документа");
                return log.ToString();
            }
        }
        log.AppendLine("output: docs=" + parts.Length.ToString(CultureInfo.InvariantCulture) + " props=" + propsAll.Count
            + " nodes=" + nodesAll.Count + " edges=" + finalEdges.Count + " len=" + outText.Length);

        if (!AssetDatabase.IsValidFolder(OutFolder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "Parquet");
            log.AppendLine("folder created: " + OutFolder);
        }
        File.WriteAllText(Root + "/" + OutGraph, outText);
        AssetDatabase.ImportAsset(OutGraph, ImportAssetOptions.ForceUpdate);

        Shader sh = Shader.Find("Parquet");
        if (sh == null)
        {
            log.AppendLine("IMPORT: Shader.Find(\"Parquet\") = null");
        }
        else
        {
            ShaderMessage[] msgs = ShaderUtil.GetShaderMessages(sh);
            log.AppendLine("IMPORT: shader=Parquet hasError=" + ShaderUtil.ShaderHasError(sh) + " messages=" + msgs.Length
                + " supported=" + sh.isSupported);
            int shown = 0;
            foreach (ShaderMessage m in msgs)
            {
                if (shown++ >= 8) break;
                log.AppendLine("   msg: " + m.message + " @" + m.file + ":" + m.line);
            }
        }
        log.AppendLine("newNodes: normal=" + S(nvObj) + " layout=" + S(cfL) + " joints=" + S(cfJ) + " tone=" + S(cfT)
            + " toneVary=" + S(mulToneVary) + " lerpPlank=" + S(lerpPlank) + " lerpJoint=" + S(lerpJoint));
        Debug.Log(log.ToString());
        return log.ToString();
    }

    // ── helpers ───────────────────────────────────────────────────────────────
    static string S(string id) { return id == null ? "null" : id.Substring(0, 8); }
    static string NewId() { return Guid.NewGuid().ToString("N"); }
    static string F(float v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }

    static void Load(string text)
    {
        string[] raw = Regex.Split(text, "}\\s*\\n\\s*\\{");
        for (int i = 0; i < raw.Length; i++)
        {
            string d = (i == 0) ? raw[i] : "{" + raw[i];
            if (i < raw.Length - 1) d = d + "}";
            Match mo = Regex.Match(d, "\"m_ObjectId\":\\s*\"([0-9a-f]{32})\"");
            if (!mo.Success) continue;
            string id = mo.Groups[1].Value;
            ById[id] = d;
            Match mt = Regex.Match(d, "\"m_Type\":\\s*\"([^\"]+)\"");
            string t = mt.Success ? mt.Groups[1].Value : "?";
            TypeOf[id] = t.Substring(t.LastIndexOf('.') + 1);
            Order.Add(id);
        }
    }

    static List<Edge> ParseEdges(string text)
    {
        var res = new List<Edge>();
        string pat = "\"m_OutputSlot\":\\s*\\{\\s*\"m_Node\":\\s*\\{\\s*\"m_Id\":\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\":\\s*(-?\\d+)\\s*\\}\\s*,\\s*\"m_InputSlot\":\\s*\\{\\s*\"m_Node\":\\s*\\{\\s*\"m_Id\":\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\":\\s*(-?\\d+)\\s*\\}";
        foreach (Match m in Regex.Matches(text, pat))
            res.Add(new Edge { S = m.Groups[1].Value, SS = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), D = m.Groups[3].Value, DS = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) });
        return res;
    }

    static Edge E(string s, int ss, string d, int ds) { return new Edge { S = s, SS = ss, D = d, DS = ds }; }
    static bool Same(Edge a, Edge b) { return a.S == b.S && a.SS == b.SS && a.D == b.D && a.DS == b.DS; }
    static Edge EdgeInto(string node, int slot) { foreach (Edge e in Edges) if (e.D == node && e.DS == slot) return e; return null; }
    static Edge EdgeFrom(string node) { foreach (Edge e in Edges) if (e.S == node) return e; return null; }

    static string FirstOfType(string t) { foreach (string id in Order) if (TypeOf[id] == t) return id; return null; }
    static string CfByFunc(string fn)
    {
        foreach (string id in Order)
            if (TypeOf[id] == "CustomFunctionNode" && ById[id].Contains("\"m_FunctionName\": \"" + fn + "\"")) return id;
        return null;
    }
    static string BlockOf(string desc)
    {
        foreach (string id in Order)
            if (TypeOf[id] == "BlockNode" && ById[id].Contains("\"m_SerializedDescriptor\": \"" + desc + "\"")) return id;
        return null;
    }
    static string PropNodeOf(string refName)
    {
        string propId = null;
        foreach (string id in Order)
            if (TypeOf[id].Contains("ShaderProperty") && ById[id].Contains("\"m_DefaultReferenceName\": \"" + refName + "\"")) propId = id;
        if (propId == null) return null;
        foreach (string id in Order)
            if (TypeOf[id] == "PropertyNode" && ById[id].Contains("\"m_Property\": {\"m_Id\": \"" + propId + "\"}")) return id;
        return null;
    }
    static string PropNodeOfType(string propType)
    {
        foreach (string id in Order)
        {
            if (TypeOf[id] != "PropertyNode") continue;
            Match mp = Regex.Match(ById[id], "\"m_Property\":\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"");
            if (mp.Success && TypeOf.ContainsKey(mp.Groups[1].Value) && TypeOf[mp.Groups[1].Value] == propType) return id;
        }
        return null;
    }

    static List<string> SlotIds(string doc)
    {
        var res = new List<string>();
        Match m = Regex.Match(doc, "\"m_Slots\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if (!m.Success) return res;
        foreach (Match x in Regex.Matches(m.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) res.Add(x.Groups[1].Value);
        return res;
    }

    static List<string> IdsInArray(string doc, string key)
    {
        var res = new List<string>();
        Match m = Regex.Match(doc, "\"" + key + "\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if (!m.Success) return res;
        foreach (Match x in Regex.Matches(m.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) res.Add(x.Groups[1].Value);
        return res;
    }

    static string ReplaceArray(string doc, string key, string content)
    {
        return Regex.Replace(doc, "(\"" + key + "\"\\s*:\\s*)\\[.*?\\]", "${1}" + content.Replace("$", "$$"), RegexOptions.Singleline);
    }

    static string IdArray(List<string> ids)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < ids.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("\n{\n\"m_Id\": \"" + ids[i] + "\"\n}");
        }
        sb.Append("\n]");
        return sb.ToString();
    }

    static string EdgeArray(List<Edge> es)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < es.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("\n{\n\"m_OutputSlot\": {\n\"m_Node\": {\n\"m_Id\": \"" + es[i].S + "\"\n},\n\"m_SlotId\": " + es[i].SS.ToString(CultureInfo.InvariantCulture)
                + "\n},\n\"m_InputSlot\": {\n\"m_Node\": {\n\"m_Id\": \"" + es[i].D + "\"\n},\n\"m_SlotId\": " + es[i].DS.ToString(CultureInfo.InvariantCulture) + "\n}\n}");
        }
        sb.Append("\n]");
        return sb.ToString();
    }

    static string SetObjId(string doc, string id)
    {
        return Regex.Replace(doc, "(\"m_ObjectId\"\\s*:\\s*)\"[0-9a-f]{32}\"", "${1}\"" + id + "\"");
    }
    static string SetStr(string doc, string key, string val)
    {
        return Regex.Replace(doc, "(\"" + key + "\"\\s*:\\s*)\"[^\"]*\"", "${1}\"" + val + "\"");
    }
    static string SetNum(string doc, string key, int val)
    {
        return Regex.Replace(doc, "(\"" + key + "\"\\s*:\\s*)-?\\d+", "${1}" + val.ToString(CultureInfo.InvariantCulture));
    }
    static string SetNumF(string doc, string key, float val)
    {
        return Regex.Replace(doc, "(\"" + key + "\"\\s*:\\s*)-?[0-9.eE+\\-]+", "${1}" + F(val));
    }
    static string SetSlotArray(string doc, List<string> ids)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < ids.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("{\"m_Id\": \"" + ids[i] + "\"}");
        }
        sb.Append("]");
        return Regex.Replace(doc, "\"m_Slots\"\\s*:\\s*\\[.*?\\]", "\"m_Slots\": " + sb.ToString(), RegexOptions.Singleline);
    }
    static string SetPropertyRef(string doc, string propId)
    {
        return Regex.Replace(doc, "(\"m_Property\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*)\"[0-9a-f]{32}\"", "${1}\"" + propId + "\"");
    }
    static string Pos(string nodeId, int x, int y)
    {
        string d = NewNodeDoc[nodeId];
        d = Regex.Replace(d, "(\"m_Position\"\\s*:\\s*\\{[^{}]*?\"x\"\\s*:\\s*)-?[0-9.]+([^{}]*?\"y\"\\s*:\\s*)-?[0-9.]+",
            "${1}" + x.ToString(CultureInfo.InvariantCulture) + "${2}" + y.ToString(CultureInfo.InvariantCulture), RegexOptions.Singleline);
        NewNodeDoc[nodeId] = d;
        return d;
    }

    static string NewNode(string tmplId, string name, bool cloneSlots, string slotDisplay)
    {
        string nid = NewId();
        string d = SetObjId(ById[tmplId], nid);
        if (name != null) d = SetStr(d, "m_Name", name);
        var ids = new List<string>();
        if (cloneSlots)
        {
            foreach (string sid in SlotIds(ById[tmplId]))
            {
                string ns = NewId();
                string sd = SetObjId(ById[sid], ns);
                if (slotDisplay != null) sd = SetStr(sd, "m_DisplayName", slotDisplay);
                NewSlotDocs.Add(sd);
                ids.Add(ns);
            }
        }
        d = SetSlotArray(d, ids);
        NewNodeDoc[nid] = d;
        NewNodeOrder.Add(nid);
        return nid;
    }

    static void AddSlot(string nodeId, string tmplSlotId, string display, string shader, int slotId, int io)
    {
        string ns = NewId();
        string d = SetObjId(ById[tmplSlotId], ns);
        d = SetStr(d, "m_DisplayName", display);
        d = SetStr(d, "m_ShaderOutputName", shader);
        d = SetNum(d, "m_Id", slotId);
        d = SetNum(d, "m_SlotType", io);
        NewSlotDocs.Add(d);
        var ids = SlotIds(NewNodeDoc[nodeId]);
        ids.Add(ns);
        NewNodeDoc[nodeId] = SetSlotArray(NewNodeDoc[nodeId], ids);
    }

    static string PropNode(string tmplId, string propId, string display, int col)
    {
        string nid = NewNode(tmplId, display, true, display);
        NewNodeDoc[nid] = SetPropertyRef(NewNodeDoc[nid], propId);
        Pos(nid, -6600 + 300 * (col % 2), 900 + 320 * (col / 2));
        return nid;
    }

    static string NewFloatProp(string display, string refName, float val)
    {
        string id = NewId();
        string d = SetObjId(ById[FirstOfType("Vector1ShaderProperty")], id);
        d = SetStr(d, "m_Name", display);
        d = SetStr(d, "m_DefaultReferenceName", refName);
        d = SetStr(d, "m_RefNameGeneratedByDisplayName", display);
        d = SetStr(d, "m_GuidSerialized", Guid.NewGuid().ToString());
        d = SetNumF(d, "m_Value", val);
        NewPropDoc[id] = d;
        NewPropOrder.Add(id);
        return id;
    }

    static string NewColorProp(string display, string refName, float r, float g, float b)
    {
        string id = NewId();
        string d = SetObjId(ById[FirstOfType("ColorShaderProperty")], id);
        d = SetStr(d, "m_Name", display);
        d = SetStr(d, "m_DefaultReferenceName", refName);
        d = SetStr(d, "m_RefNameGeneratedByDisplayName", display);
        d = SetStr(d, "m_GuidSerialized", Guid.NewGuid().ToString());
        int cut = d.IndexOf("\"m_Value\":");
        if (cut >= 0)
        {
            string head = d.Substring(0, cut);
            string tail = d.Substring(cut);
            if (tail.Contains("\"r\""))
            {
                tail = Regex.Replace(tail, "\"r\"\\s*:\\s*-?[0-9.eE+\\-]+", "\"r\": " + F(r));
                tail = Regex.Replace(tail, "\"g\"\\s*:\\s*-?[0-9.eE+\\-]+", "\"g\": " + F(g));
                tail = Regex.Replace(tail, "\"b\"\\s*:\\s*-?[0-9.eE+\\-]+", "\"b\": " + F(b));
            }
            d = head + tail;
        }
        NewPropDoc[id] = d;
        NewPropOrder.Add(id);
        return id;
    }
}
