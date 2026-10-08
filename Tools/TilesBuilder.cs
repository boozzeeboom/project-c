using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// TilesBuilder — собирает Assets/_Project/Materials/Tiles/Tiles.shadergraph как
// ПРОИЗВОДНУЮ от готового графа Brick (кирпичная стена): сохраняет всё дерево
// (тон, зерно, поры, кракле, высолы, мох, обвязку цвета и глянца), но
//
//   * переименовывает свойства _Brick_/_Mortar_/_Crack_/_Efflo_/_Dirt_ → _Tile_/_Gap_/_Craze_/_Salt_/_Moss_,
//   * переключает все CF-ноды на ядро ProceduralTilesNoise.hlsl,
//   * добавляет вход TileTail в Joints и Tone и НОВУЮ CF-ноду Crown,
//   * перенаправляет арку (Crown) в подъём рельефа вместо тона,
//   * вставляет Multiply «мох × швы» перед множителем _Moss_Amount.
//
// Донор Brick — ТОЛЬКО ЧТЕНИЕ: ни один байт исходников не перезаписывается.
public static class TilesBuilder
{
    const string SrcGraph = "Assets/_Project/Materials/Brick/Brick.shadergraph";
    const string OutFolder = "Assets/_Project/Materials/Tiles";
    const string OutGraph = OutFolder + "/Tiles.shadergraph";
    const string TilesHlsl = "Assets/_Project/Shaders/ProceduralTilesNoise.hlsl";

    static string Root;

    static readonly List<string> Order = new List<string>();
    static readonly Dictionary<string, string> ById = new Dictionary<string, string>();
    static readonly Dictionary<string, string> TypeOf = new Dictionary<string, string>();

    class Edge { public string S; public int SS; public string D; public int DS; }
    static readonly List<Edge> Edges = new List<Edge>();

    static readonly Dictionary<string, string> NewDocs = new Dictionary<string, string>();
    static readonly List<string> NewOrder = new List<string>();
    static readonly List<string> NewPropIds = new List<string>();
    // ТОЛЬКО ноды: слоты и свойства лежат в том же NewOrder, но в m_Nodes их
    // подмешивать нельзя — иначе JsonData<AbstractMaterialNode> разбирается в null
    // и импорт падает в GraphData.ConcretizeGraph с ArgumentNullException(source).
    static readonly List<string> NewNodeIds = new List<string>();

    static readonly Dictionary<string, string[]> PropRenames = BuildPropRenames();
    static readonly Dictionary<string, string> FnRenames = BuildFnRenames();

    static string Stage = "start";

    public static string Execute()
    {
        try { return Run(); }
        catch (Exception ex)
        {
            string m = "TilesBuilder FAILED at stage [" + Stage + "]: " + ex.GetType().Name + ": " + ex.Message;
            Debug.Log(m + "\n" + ex.StackTrace);
            return m;
        }
    }

    static string Run()
    {
        var log = new StringBuilder();
        Root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');

        // ── ядро ──────────────────────────────────────────────────────────────
        AssetDatabase.ImportAsset(TilesHlsl, ImportAssetOptions.ForceUpdate);
        string tGuid = AssetDatabase.AssetPathToGUID(TilesHlsl);
        if (string.IsNullOrEmpty(tGuid))
        {
            log.AppendLine("ABORT: нет GUID у " + TilesHlsl);
            Debug.Log(log.ToString());
            return log.ToString();
        }
        log.AppendLine("hlsl: tiles=" + tGuid);

        // ── донор ─────────────────────────────────────────────────────────────
        string txt = File.ReadAllText(Root + "/" + SrcGraph).Replace("\r\n", "\n").Replace("\r", "\n");
        Load(txt);
        Edges.AddRange(ParseEdges(txt));

        string gdId = FirstOfType("GraphData");
        if (gdId == null) { log.AppendLine("ABORT: нет GraphData"); Debug.Log(log.ToString()); return log.ToString(); }

        int edgeSlots = Regex.Matches(ById[gdId], "\"m_OutputSlot\"").Count;
        log.AppendLine("source: docs=" + Order.Count.ToString(CultureInfo.InvariantCulture)
            + " nodes=" + IdsInArray(ById[gdId], "m_Nodes").Count.ToString(CultureInfo.InvariantCulture)
            + " props=" + IdsInArray(ById[gdId], "m_Properties").Count.ToString(CultureInfo.InvariantCulture)
            + " edges=" + Edges.Count.ToString(CultureInfo.InvariantCulture) + "/" + edgeSlots.ToString(CultureInfo.InvariantCulture));
        if (edgeSlots != Edges.Count)
        {
            log.AppendLine("ABORT: m_Edges=" + edgeSlots + ", распознано " + Edges.Count);
            Debug.Log(log.ToString());
            return log.ToString();
        }

        // ── разведка ──────────────────────────────────────────────────────────
        string posId = FirstOfType("PositionNode");
        string nfhId = FirstOfType("NormalFromHeightNode");
        string cfJoints = CfByFunc("ProceduralBrickJoints");
        string cfTone = CfByFunc("ProceduralBrickTone");
        string tmplProp1 = PropNodeOfType("Vector1ShaderProperty");
        string tmplMul = FirstOfType("MultiplyNode");
        string propLiftId = PropByRef("_Brick_Lift");
        string propMossAmtId = PropByRef("_Dirt_Amount");
        log.AppendLine("discover: pos=" + S(posId) + " nfh=" + S(nfhId) + " joints=" + S(cfJoints) + " tone=" + S(cfTone)
            + " propNode1=" + S(tmplProp1) + " mul=" + S(tmplMul) + " lift=" + S(propLiftId) + " mossAmt=" + S(propMossAmtId));

        string missing = null;
        if (posId == null) missing = "PositionNode";
        else if (nfhId == null) missing = "NormalFromHeightNode";
        else if (cfJoints == null) missing = "CF ProceduralBrickJoints";
        else if (cfTone == null) missing = "CF ProceduralBrickTone";
        else if (tmplProp1 == null) missing = "PropertyNode(Vector1)";
        else if (tmplMul == null) missing = "MultiplyNode";
        else if (propLiftId == null) missing = "_Brick_Lift";
        else if (propMossAmtId == null) missing = "_Dirt_Amount";
        if (missing != null)
        {
            log.AppendLine("ABORT: не найден " + missing);
            Debug.Log(log.ToString());
            return log.ToString();
        }

        Edge eNfhStrength = EdgeInto(nfhId, 2);
        log.AppendLine("nfh Strength <= " + (eNfhStrength == null ? "ПУСТО (дефект рельефа!)" : S(eNfhStrength.S) + "#" + eNfhStrength.SS));
        if (eNfhStrength == null)
        {
            log.AppendLine("ABORT: слот Strength ноды Normal From Height не подключён");
            Debug.Log(log.ToString());
            return log.ToString();
        }

        // ── preflight: все ли внутренние ссылки графа разрешаются ────────────
        int dangling = 0;
        foreach (string id in Order)
        {
            string d = ById[id];
            Match ms = Regex.Match(d, "\"m_Slots\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            if (ms.Success)
                foreach (Match x in Regex.Matches(ms.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\""))
                    if (!ById.ContainsKey(x.Groups[1].Value))
                    {
                        log.AppendLine("   dangling slot " + S(x.Groups[1].Value) + " в " + TypeOf[id] + " " + S(id));
                        dangling++;
                    }
            Match mpd = Regex.Match(d, "\"m_Property\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"");
            if (mpd.Success && !ById.ContainsKey(mpd.Groups[1].Value))
            {
                log.AppendLine("   dangling property " + S(mpd.Groups[1].Value) + " в " + S(id));
                dangling++;
            }
        }
        log.AppendLine("preflight: dangling=" + dangling.ToString(CultureInfo.InvariantCulture));

        // ── 1. свойства ───────────────────────────────────────────────────────
        Stage = "1 renames";
        var propDisplay = new Dictionary<string, string>();
        foreach (string id in Order)
        {
            if (!TypeOf[id].Contains("ShaderProperty")) continue;
            string oldRef = Str(id, "m_DefaultReferenceName");
            string[] r;
            if (!PropRenames.TryGetValue(oldRef, out r)) continue;
            string d = ById[id];
            d = SetStr(d, "m_Name", r[0]);
            d = SetStr(d, "m_DefaultReferenceName", r[1]);
            d = SetStr(d, "m_RefNameGeneratedByDisplayName", r[0]);
            ById[id] = d;
            propDisplay[id] = r[0];
        }
        log.AppendLine("1. переименовано свойств: " + propDisplay.Count.ToString(CultureInfo.InvariantCulture));

        int stale = 0;
        foreach (string id in Order)
        {
            if (!TypeOf[id].Contains("ShaderProperty")) continue;
            string rn = Str(id, "m_DefaultReferenceName");
            if (rn.Contains("Brick") || rn.Contains("Mortar") || rn.Contains("Crack")
                || rn.Contains("Efflo") || rn.Contains("Dirt"))
            {
                log.AppendLine("   STALE: " + rn);
                stale++;
            }
        }
        if (stale > 0)
        {
            log.AppendLine("ABORT: остались неотфильтрованные имена свойств");
            Debug.Log(log.ToString());
            return log.ToString();
        }

        // витринные имена PropertyNode и их выходных слотов
        foreach (string id in Order)
        {
            if (TypeOf[id] != "PropertyNode") continue;
            Match mp = Regex.Match(ById[id], "\"m_Property\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"");
            if (!mp.Success) continue;
            string pid = mp.Groups[1].Value;
            string disp;
            if (!propDisplay.TryGetValue(pid, out disp)) continue;
            ById[id] = SetStr(ById[id], "m_Name", disp);
            foreach (string soid in SlotIds(ById[id]))
                if (Doc(soid) != null) PutDoc(soid, SetStr(Doc(soid), "m_DisplayName", disp));
        }

        // ── 2. CF-ноды: функция и файл ────────────────────────────────────────
        Stage = "2 cf";
        int cfCount = 0;
        foreach (string id in Order)
        {
            if (TypeOf[id] != "CustomFunctionNode") continue;
            string oldFn = Str(id, "m_FunctionName");
            string newFn;
            if (!FnRenames.TryGetValue(oldFn, out newFn))
            {
                log.AppendLine("ABORT: неизвестная CF-функция " + oldFn);
                Debug.Log(log.ToString());
                return log.ToString();
            }
            string d = ById[id];
            d = SetStr(d, "m_FunctionName", newFn);
            d = SetStr(d, "m_FunctionSource", tGuid);
            d = SetStr(d, "m_Name", newFn + " (Custom Function)");
            ById[id] = d;
            cfCount++;
        }
        log.AppendLine("2. переключено CF-нод: " + cfCount.ToString(CultureInfo.InvariantCulture) + " -> " + TilesHlsl);

        // ── 3. слоты Joints / Tone: переименование, выход, TileTail ───────────
        Stage = "3 slots";
        string seedSoJoints = SlotObjOf(cfJoints, 7);
        string seedSoTone = SlotObjOf(cfTone, 5);
        if (seedSoJoints == null || seedSoTone == null)
        {
            log.AppendLine("ABORT: не найдены слоты-шаблоны для TileTail");
            Debug.Log(log.ToString());
            return log.ToString();
        }

        RenameSlot(cfJoints, 1, "TileScale");
        RenameSlot(cfJoints, 2, "TileAspect");
        RenameSlot(cfJoints, 4, "TileWarp");
        RenameSlot(cfJoints, 7, "TileSeed");
        RenumberOutput(cfJoints, 8, 9, "Joints");
        InsertInputSlotBeforeOutput(cfJoints, 8, "TileTail", seedSoJoints);

        RenameSlot(cfTone, 1, "TileScale");
        RenameSlot(cfTone, 2, "TileAspect");
        RenameSlot(cfTone, 4, "TileWarp");
        RenameSlot(cfTone, 5, "TileSeed");
        RenumberOutput(cfTone, 6, 7, "TileTone");
        InsertInputSlotBeforeOutput(cfTone, 6, "TileTail", seedSoTone);
        log.AppendLine("3. Joints: 8 входов + Joints(out#9); Tone: 6 входов + TileTone(out#7)");

        // ── 4. рёбра выходов сдвинулись вслед за слотами ──────────────────────
        Stage = "4 edges";
        int shifted = 0;
        foreach (Edge e in Edges)
        {
            if (e.S == cfJoints && e.SS == 8) { e.SS = 9; shifted++; }
            else if (e.S == cfTone && e.SS == 6) { e.SS = 7; shifted++; }
        }
        log.AppendLine("4. сдвинуто исходящих рёбер: " + shifted.ToString(CultureInfo.InvariantCulture));

        // ── 5. _Tile_Tail + PropertyNode ──────────────────────────────────────
        Stage = "5 tail";
        string pTail = NewFloatProp("Tile Tail", "_Tile_Tail", 0.32f);
        string pnTail = PropNode(tmplProp1, pTail, "Tile Tail", -5400, -1450);
        log.AppendLine("5. добавлено свойство _Tile_Tail + PropertyNode");

        // ── 6. CF-нода Crown (клон расширенного Tone) ─────────────────────────
        Stage = "6 crown";
        string cfCrown = NewId();
        string cd = SetObjId(Doc(cfTone), cfCrown);
        cd = SetStr(cd, "m_Name", "ProceduralTilesCrown (Custom Function)");
        cd = SetStr(cd, "m_FunctionName", "ProceduralTilesCrown");
        var crownSlots = new List<string>();
        foreach (string soid in SlotIds(Doc(cfTone)))
        {
            string ns = NewId();
            string sd = SetObjId(Doc(soid), ns);
            NewDocs[ns] = sd;
            NewOrder.Add(ns);
            crownSlots.Add(ns);
        }
        cd = SetSlotArray(cd, crownSlots);
        cd = SetPos(cd, 960, 60);
        NewDocs[cfCrown] = cd;
        NewOrder.Add(cfCrown);
        NewNodeIds.Add(cfCrown);
        string crownOutSo = crownSlots[crownSlots.Count - 1];
        NewDocs[crownOutSo] = SetStr(SetStr(NewDocs[crownOutSo], "m_DisplayName", "TileCrown"), "m_ShaderOutputName", "TileCrown");
        log.AppendLine("6. CF Crown: " + S(cfCrown) + " слотов=" + crownSlots.Count.ToString(CultureInfo.InvariantCulture)
            + " выход=" + S(crownOutSo) + " (#7)");

        // ── 7. Multiply «мох × швы» перед _Moss_Amount ────────────────────────
        Stage = "7 moss";
        string pnMossAmt = PropNodeOfProp(propMossAmtId);
        Edge eMossAmt = EdgeFrom(pnMossAmt);
        if (eMossAmt == null)
        {
            log.AppendLine("ABORT: _Moss_Amount без потребителя");
            Debug.Log(log.ToString());
            return log.ToString();
        }
        string mulMoss = eMossAmt.D;
        Edge eMossSrc = EdgeInto(mulMoss, 0);
        if (eMossSrc == null)
        {
            log.AppendLine("ABORT: вход A множителя _Moss_Amount пуст");
            Debug.Log(log.ToString());
            return log.ToString();
        }
        string mulMossJoints = NewNode(tmplMul, "Multiply", true, null);
        SetPosOn(mulMossJoints, -2200, -1500);
        log.AppendLine("7. Multiply «мох × швы»: " + S(mulMossJoints) + " врезан в "
            + S(eMossSrc.S) + "#" + eMossSrc.SS + " -> " + S(mulMoss) + "#" + eMossSrc.DS);

        // ── 8. арка -> подъём рельефа (Crown вместо Tone) ─────────────────────
        Stage = "8 lift";
        string pnLift = PropNodeOfProp(propLiftId);
        Edge eLift = EdgeFrom(pnLift);
        if (eLift == null)
        {
            log.AppendLine("ABORT: _Tile_Crown без потребителя");
            Debug.Log(log.ToString());
            return log.ToString();
        }
        string mulLift = eLift.D;
        Edge eToneLift = EdgeInto(mulLift, 0);
        if (eToneLift == null || eToneLift.S != cfTone)
        {
            log.AppendLine("ABORT: вход A множителя подъёма не от Tone ("
                + (eToneLift == null ? "пусто" : S(eToneLift.S) + "#" + eToneLift.SS) + ")");
            Debug.Log(log.ToString());
            return log.ToString();
        }
        log.AppendLine("8. подъём рельефа: Tone#" + eToneLift.SS + " -> Crown#7 в " + S(mulLift) + "#0");

        // ── 9. итоговый список рёбер ─────────────────────────────────────────
        Stage = "9 finaledges";
        var drop = new List<Edge>();
        drop.Add(eToneLift);
        drop.Add(eMossSrc);

        var finalEdges = new List<Edge>();
        int removed = 0;
        foreach (Edge e in Edges)
        {
            bool kill = false;
            foreach (Edge d in drop) if (Same(e, d)) { kill = true; break; }
            if (kill) removed++; else finalEdges.Add(e);
        }

        finalEdges.Add(E(eMossSrc.S, eMossSrc.SS, mulMossJoints, 0));
        finalEdges.Add(E(cfJoints, 9, mulMossJoints, 1));
        finalEdges.Add(E(mulMossJoints, 2, mulMoss, 0));
        finalEdges.Add(E(cfCrown, 7, mulLift, 0));
        finalEdges.Add(E(pnTail, 0, cfJoints, 8));
        finalEdges.Add(E(pnTail, 0, cfTone, 6));
        finalEdges.Add(E(pnTail, 0, cfCrown, 6));
        log.AppendLine("9. рёбра: было " + Edges.Count.ToString(CultureInfo.InvariantCulture)
            + " снято " + removed.ToString(CultureInfo.InvariantCulture)
            + " добавлено 7 итого " + finalEdges.Count.ToString(CultureInfo.InvariantCulture));

        // дубли рёбер недопустимы
        var seenE = new HashSet<string>();
        foreach (Edge e in finalEdges)
        {
            string k = e.S + "#" + e.SS.ToString(CultureInfo.InvariantCulture) + ">" + e.D + "#" + e.DS.ToString(CultureInfo.InvariantCulture);
            if (!seenE.Add(k))
            {
                log.AppendLine("ABORT: дубль ребра " + k);
                Debug.Log(log.ToString());
                return log.ToString();
            }
        }

        // ── 10. списки ids графа ─────────────────────────────────────────────
        Stage = "10 lists";
        var propsAll = IdsInArray(ById[gdId], "m_Properties");
        var seenP = new HashSet<string>();
        foreach (string s in propsAll) seenP.Add(s);
        foreach (string id in NewPropIds) if (seenP.Add(id)) propsAll.Add(id);

        var nodesAll = IdsInArray(ById[gdId], "m_Nodes");
        var seenN = new HashSet<string>();
        foreach (string s in nodesAll) seenN.Add(s);
        foreach (string id in NewNodeIds) if (seenN.Add(id)) nodesAll.Add(id);

        string problem = null;
        foreach (Edge e in finalEdges)
        {
            if (!seenN.Contains(e.S)) { problem = "источник ребра не в m_Nodes: " + S(e.S); break; }
            if (!seenN.Contains(e.D)) { problem = "приёмник ребра не в m_Nodes: " + S(e.D); break; }
        }
        if (problem == null)
            foreach (string id in NewNodeIds)
                if (!seenN.Contains(id)) problem = "нода не в m_Nodes: " + S(id);
        if (problem != null)
        {
            log.AppendLine("ABORT: " + problem);
            Debug.Log(log.ToString());
            return log.ToString();
        }

        string gdNew = ById[gdId];
        gdNew = ReplaceArray(gdNew, "m_Properties", IdArray(propsAll));
        gdNew = ReplaceArray(gdNew, "m_Nodes", IdArray(nodesAll));
        gdNew = ReplaceArray(gdNew, "m_Edges", EdgeArray(finalEdges));

        // ── 11. сборка текста ────────────────────────────────────────────────
        Stage = "11 write";
        var outSb = new StringBuilder();
        bool first = true;
        for (int i = 0; i < Order.Count; i++)
        {
            string id = Order[i];
            if (!first) outSb.Append("\n\n");
            first = false;
            outSb.Append(id == gdId ? gdNew : ById[id]);
        }
        foreach (string id in NewOrder)
        {
            outSb.Append("\n\n");
            outSb.Append(NewDocs[id]);
        }

        string outText = outSb.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
        if (!outText.EndsWith("\n")) outText += "\n";

        string[] parts = Regex.Split(outText, "\n\n");
        foreach (string part in parts)
        {
            if (Regex.IsMatch(part, "\n[ \t]*\n"))
            {
                log.AppendLine("ABORT: пустая строка внутри документа");
                Debug.Log(log.ToString());
                return log.ToString();
            }
        }
        log.AppendLine("11. выход: docs=" + parts.Length.ToString(CultureInfo.InvariantCulture)
            + " props=" + propsAll.Count.ToString(CultureInfo.InvariantCulture)
            + " nodes=" + nodesAll.Count.ToString(CultureInfo.InvariantCulture)
            + " edges=" + finalEdges.Count.ToString(CultureInfo.InvariantCulture)
            + " len=" + outText.Length.ToString(CultureInfo.InvariantCulture));

        if (!AssetDatabase.IsValidFolder(OutFolder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "Tiles");
            log.AppendLine("    создана папка " + OutFolder);
        }
        File.WriteAllText(Root + "/" + OutGraph, outText);
        AssetDatabase.ImportAsset(OutGraph, ImportAssetOptions.ForceUpdate);

        // ── 12. проверка импорта ─────────────────────────────────────────────
        Stage = "12 import";
        Shader sh = Shader.Find("Tiles");
        if (sh == null)
        {
            log.AppendLine("12. IMPORT: Shader.Find(\"Tiles\") = null");
            string dump = OutFolder + "/dump_tiles.shadergraph";
            File.WriteAllText(Root + "/" + dump, outText + "\n");
            log.AppendLine("    копия текста: " + dump);
        }
        else
        {
            ShaderMessage[] msgs = ShaderUtil.GetShaderMessages(sh);
            log.AppendLine("12. IMPORT: Tiles hasError=" + ShaderUtil.ShaderHasError(sh)
                + " messages=" + msgs.Length.ToString(CultureInfo.InvariantCulture)
                + " supported=" + sh.isSupported);
            int shown = 0;
            foreach (ShaderMessage m in msgs)
            {
                if (shown++ >= 8) break;
                log.AppendLine("    msg: " + m.message + " @" + m.file + ":" + m.line);
            }
        }

        Debug.Log(log.ToString());
        return log.ToString();
    }

    // ── таблицы переименований ────────────────────────────────────────────────
    static Dictionary<string, string[]> BuildPropRenames()
    {
        var m = new Dictionary<string, string[]>();
        R(m, "_Brick_Color", "Tile Color", "_Tile_Color");
        R(m, "_Brick_Light_Color", "Tile Light Color", "_Tile_Light_Color");
        R(m, "_Mortar_Color", "Gap Color", "_Gap_Color");
        R(m, "_Efflorescence_Color", "Salt Color", "_Salt_Color");
        R(m, "_Dirt_Color", "Moss Color", "_Moss_Color");
        R(m, "_Brick_Scale", "Tile Scale", "_Tile_Scale");
        R(m, "_Brick_Aspect", "Tile Aspect", "_Tile_Aspect");
        R(m, "_Joint_Warp", "Tile Warp", "_Tile_Warp");
        R(m, "_Brick_Seed", "Tile Seed", "_Tile_Seed");
        R(m, "_Brick_Tone", "Tile Tone", "_Tile_Tone");
        R(m, "_Brick_Lift", "Tile Crown", "_Tile_Crown");
        R(m, "_Face_Grain_Scale", "Tile Grain Scale", "_Tile_Grain_Scale");
        R(m, "_Face_Grain_Stretch", "Tile Grain Stretch", "_Tile_Grain_Stretch");
        R(m, "_Face_Grain_Seed", "Tile Grain Seed", "_Tile_Grain_Seed");
        R(m, "_Face_Grain_Amount", "Tile Grain Amount", "_Tile_Grain_Amount");
        R(m, "_Mortar_Grain_Scale", "Body Grain Scale", "_Body_Grain_Scale");
        R(m, "_Mortar_Grain_Seed", "Body Grain Seed", "_Body_Grain_Seed");
        R(m, "_Mortar_Grain_Amount", "Body Grain Amount", "_Body_Grain_Amount");
        R(m, "_Crack_Scale", "Craze Scale", "_Craze_Scale");
        R(m, "_Crack_Stretch", "Craze Stretch", "_Craze_Stretch");
        R(m, "_Crack_Width", "Craze Width", "_Craze_Width");
        R(m, "_Crack_Warp", "Craze Warp", "_Craze_Warp");
        R(m, "_Crack_Seed", "Craze Seed", "_Craze_Seed");
        R(m, "_Crack_Amount", "Craze Amount", "_Craze_Amount");
        R(m, "_Crack_Bump", "Craze Bump", "_Craze_Bump");
        R(m, "_Efflo_Scale", "Salt Scale", "_Salt_Scale");
        R(m, "_Efflo_Seed", "Salt Seed", "_Salt_Seed");
        R(m, "_Efflo_Threshold", "Salt Threshold", "_Salt_Threshold");
        R(m, "_Efflo_Softness", "Salt Softness", "_Salt_Softness");
        R(m, "_Efflo_Amount", "Salt Amount", "_Salt_Amount");
        R(m, "_Dirt_Scale", "Moss Scale", "_Moss_Scale");
        R(m, "_Dirt_Stretch", "Moss Stretch", "_Moss_Stretch");
        R(m, "_Dirt_Seed", "Moss Seed", "_Moss_Seed");
        R(m, "_Dirt_Threshold", "Moss Threshold", "_Moss_Threshold");
        R(m, "_Dirt_Softness", "Moss Softness", "_Moss_Softness");
        R(m, "_Dirt_Amount", "Moss Amount", "_Moss_Amount");
        R(m, "_Brick_Gloss", "Tile Gloss", "_Tile_Gloss");
        R(m, "_Mortar_Gloss", "Gap Gloss", "_Gap_Gloss");
        return m;
    }

    static void R(Dictionary<string, string[]> m, string oldRef, string disp, string newRef)
    {
        m[oldRef] = new[] { disp, newRef };
    }

    static Dictionary<string, string> BuildFnRenames()
    {
        var m = new Dictionary<string, string>();
        m["ProceduralBrickJoints"] = "ProceduralTilesJoints";
        m["ProceduralBrickTone"] = "ProceduralTilesTone";
        m["ProceduralBrickGrains"] = "ProceduralTilesGrains";
        m["ProceduralBrickPores"] = "ProceduralTilesPores";
        m["ProceduralBrickCracks"] = "ProceduralTilesCrazing";
        m["ProceduralBrickPatch"] = "ProceduralTilesPatch";
        return m;
    }

    // ── helpers ───────────────────────────────────────────────────────────────
    static string S(string id) { return id == null ? "null" : id.Substring(0, 8); }
    static string NewId() { return Guid.NewGuid().ToString("N"); }
    static string F(float v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }

    static string Str(string id, string key)
    {
        string d = Doc(id);
        if (d == null) return "?";
        Match m = Regex.Match(d, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : "?";
    }

    // документ может лежать как в исходном графе, так и среди новых
    static string Doc(string id)
    {
        string d;
        if (id != null && ById.TryGetValue(id, out d)) return d;
        if (id != null && NewDocs.TryGetValue(id, out d)) return d;
        return null;
    }

    static void PutDoc(string id, string doc)
    {
        if (ById.ContainsKey(id)) ById[id] = doc; else NewDocs[id] = doc;
    }

    static void Load(string text)
    {
        string[] parts = Regex.Split(text, "\n\n");
        for (int i = 0; i < parts.Length; i++)
        {
            string d = parts[i].Trim();
            if (d.Length == 0) continue;
            Match mo = Regex.Match(d, "\"m_ObjectId\"\\s*:\\s*\"([0-9a-f]{32})\"");
            if (!mo.Success) continue;
            string id = mo.Groups[1].Value;
            ById[id] = d;
            Match mt = Regex.Match(d, "\"m_Type\"\\s*:\\s*\"([^\"]+)\"");
            string t = mt.Success ? mt.Groups[1].Value : "?";
            TypeOf[id] = t.Substring(t.LastIndexOf('.') + 1);
            Order.Add(id);
        }
    }

    static List<Edge> ParseEdges(string text)
    {
        var res = new List<Edge>();
        string pat = "\"m_OutputSlot\"\\s*:\\s*\\{\\s*\"m_Node\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\"\\s*:\\s*(-?\\d+)\\s*\\}\\s*,\\s*\"m_InputSlot\"\\s*:\\s*\\{\\s*\"m_Node\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"\\s*\\}\\s*,\\s*\"m_SlotId\"\\s*:\\s*(-?\\d+)";
        foreach (Match m in Regex.Matches(text, pat))
            res.Add(new Edge
            {
                S = m.Groups[1].Value,
                SS = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                D = m.Groups[3].Value,
                DS = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture)
            });
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

    static string PropByRef(string refName)
    {
        foreach (string id in Order)
            if (TypeOf[id].Contains("ShaderProperty") && ById[id].Contains("\"m_DefaultReferenceName\": \"" + refName + "\"")) return id;
        return null;
    }

    static string PropNodeOfProp(string propId)
    {
        foreach (string id in Order)
            if (TypeOf[id] == "PropertyNode" && ById[id].Contains("\"m_Property\": {\"m_Id\": \"" + propId + "\"}")) return id;
        return null;
    }

    static string PropNodeOfType(string propType)
    {
        foreach (string id in Order)
        {
            if (TypeOf[id] != "PropertyNode") continue;
            Match mp = Regex.Match(ById[id], "\"m_Property\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"");
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

    static string SlotObjOf(string nodeId, int slotId)
    {
        foreach (string soid in SlotIds(Doc(nodeId)))
        {
            string sd = Doc(soid);
            if (sd == null) continue;
            Match m = Regex.Match(sd, "\"m_Id\"\\s*:\\s*(-?\\d+)");
            if (m.Success && int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) == slotId) return soid;
        }
        return null;
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

    static string SetPos(string doc, int x, int y)
    {
        return Regex.Replace(doc, "(\"m_Position\"\\s*:\\s*\\{[^{}]*?\"x\"\\s*:\\s*)-?[0-9.]+([^{}]*?\"y\"\\s*:\\s*)-?[0-9.]+",
            "${1}" + x.ToString(CultureInfo.InvariantCulture) + "${2}" + y.ToString(CultureInfo.InvariantCulture), RegexOptions.Singleline);
    }

    static void SetPosOn(string nodeId, int x, int y)
    {
        NewDocs[nodeId] = SetPos(NewDocs[nodeId], x, y);
    }

    // ── правка существующих узлов ─────────────────────────────────────────────
    static void RenameSlot(string nodeId, int slotId, string newName)
    {
        string soid = SlotObjOf(nodeId, slotId);
        if (soid == null || Doc(soid) == null) return;
        PutDoc(soid, SetStr(SetStr(Doc(soid), "m_DisplayName", newName), "m_ShaderOutputName", newName));
    }

    static void RenumberOutput(string nodeId, int oldId, int newId, string outName)
    {
        string soid = SlotObjOf(nodeId, oldId);
        if (soid == null || Doc(soid) == null) return;
        string d = SetStr(Doc(soid), "m_DisplayName", outName);
        d = SetStr(d, "m_ShaderOutputName", outName);
        d = SetNum(d, "m_Id", newId);
        PutDoc(soid, d);
    }

    // вставляет входной слот ПЕРЕД выходным, сохраняя порядок m_Slots
    static void InsertInputSlotBeforeOutput(string nodeId, int newId, string name, string tmplSoId)
    {
        string ns = NewId();
        string d = SetObjId(Doc(tmplSoId), ns);
        d = SetStr(d, "m_DisplayName", name);
        d = SetStr(d, "m_ShaderOutputName", name);
        d = SetNum(d, "m_Id", newId);
        d = SetNum(d, "m_SlotType", 0);
        NewDocs[ns] = d;
        NewOrder.Add(ns);

        var ids = SlotIds(Doc(nodeId));
        ids.Insert(ids.Count - 1, ns);
        PutDoc(nodeId, SetSlotArray(Doc(nodeId), ids));
    }

    // ── создание новых узлов и свойств ───────────────────────────────────────
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
                string sd = SetObjId(Doc(sid), ns);
                if (slotDisplay != null) sd = SetStr(sd, "m_DisplayName", slotDisplay);
                NewDocs[ns] = sd;
                NewOrder.Add(ns);
                ids.Add(ns);
            }
        }
        d = SetSlotArray(d, ids);
        NewDocs[nid] = d;
        NewOrder.Add(nid);
        NewNodeIds.Add(nid);
        return nid;
    }

    static string PropNode(string tmplId, string propId, string display, int x, int y)
    {
        string nid = NewNode(tmplId, display, true, display);
        NewDocs[nid] = SetPropertyRef(NewDocs[nid], propId);
        NewDocs[nid] = SetPos(NewDocs[nid], x, y);
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
        NewDocs[id] = d;
        NewOrder.Add(id);
        NewPropIds.Add(id);
        return id;
    }
}
