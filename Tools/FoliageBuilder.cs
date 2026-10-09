using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// FoliageBuilder — собирает Assets/_Project/Materials/Foliage/Foliage.shadergraph
// С НУЛЯ: от донора Brick берутся ТОЛЬКО шаблоны документов, дерево Brick не
// наследуется. Копируются как есть и получают новые ObjectId:
//   GraphData (переписываются m_Properties/m_Nodes/m_Edges/m_Blocks/m_ActiveTargets),
//   UniversalTarget, UniversalLitSubTarget, 9 блоков мастер-стека, PositionNode,
//   NormalFromHeightNode, математические ноды, PropertyNode, Vector1ShaderProperty,
//   ColorShaderProperty, CustomFunctionNode.
//
//   Донор Brick — ТОЛЬКО ЧТЕНИЕ: ни один байт исходников не перезаписывается.
//
// Слои листвы (см. Assets/_Project/Shaders/ProceduralFoliageNoise.hlsl):
//   Leaf LeafTone LeafVein  — лист: маска, тон (хеш ячейки), жилки
//   Patch x3                — один рисунок fbm в трёх ролях (масса, сухость, выгорание)
//   Twigs                   — сучья, просвечивающие сквозь массу
//
// АЛЬБЕДО — 6 Lerp: тон листа -> просвет по разрежению массы -> выгорание ->
// сухость по кромкам -> затемнение кромки к глубокому тону -> сучья.
// ГЛЯНЕЦ — Saturate(_Smoothness + тон*_Leaf_Gloss + кромка*_Leaf_Edge_Gloss).
// РЕЛЬЕФ — Add(Leaf, LeafVein*_Vein_Bump, Twigs*_Twig_Bump, Mass*_Mass_Bump)
//          -> NormalFromHeight.In; NFH.Strength <- _Bump_Strength (шаблонный
//          дефолт 0.01 гасил бы рельеф стократно).
//
// ПРАВИЛО (T-BRONZE01): функции CF-нод одно-выходные (File-режим); порядок слотов
// CF-ноды ОБЯЗАН совпадать с порядком параметров HLSL, `out` — последним; связывание
// позиционное. Ни одна существующая сигнатура не меняется.
public static class FoliageBuilder
{
    const string SrcGraph  = "Assets/_Project/Materials/Brick/Brick.shadergraph";
    const string OutFolder = "Assets/_Project/Materials/Foliage";
    const string OutGraph  = OutFolder + "/Foliage.shadergraph";
    const string FolHlsl   = "Assets/_Project/Shaders/ProceduralFoliageNoise.hlsl";
    const string ShaderName = "Foliage";

    static string Root;
    static string HlslGuid;

    // ── донор (только чтение) ─────────────────────────────────────────────────
    static readonly List<string> DOrder = new List<string>();
    static readonly Dictionary<string, string> DById = new Dictionary<string, string>();
    static readonly Dictionary<string, string> DType = new Dictionary<string, string>();

    // ── выход ─────────────────────────────────────────────────────────────────
    static readonly List<string> OOrder = new List<string>();                 // порядок документов в файле
    static readonly Dictionary<string, string> OById = new Dictionary<string, string>();
    static readonly List<string> ONodes = new List<string>();                 // m_Nodes: ТОЛЬКО ноды
    static readonly List<string> OProps = new List<string>();                 // m_Properties
    static readonly Dictionary<string, List<string>> OSlots = new Dictionary<string, List<string>>();

    struct Port { public string N; public int S; }
    struct Edge { public string S; public int SS; public string D; public int DS; }
    static readonly List<Edge> Edges = new List<Edge>();

    static string Stage = "start";
    static float PropY = -4400f;

    // ── шаблоны донора ────────────────────────────────────────────────────────
    static string TmplGraph, TmplTarget, TmplSub, TmplPos, TmplNfh, TmplCf;
    static string TmplMul, TmplAdd, TmplLerp, TmplSmooth, TmplSat;
    static string TmplPropF, TmplPropC, TmplPnF, TmplPnC;
    static string TmplCfIn3, TmplCfIn1, TmplCfOut;

    public static string Execute()
    {
        try { return Run(); }
        catch (Exception ex)
        {
            string m = "FoliageBuilder FAILED at stage [" + Stage + "]: " + ex.GetType().Name + ": " + ex.Message;
            Debug.Log(m + "\n" + ex.StackTrace);
            return m;
        }
    }

    static string Run()
    {
        var log = new StringBuilder();
        Root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');

        // ── 0. ядро HLSL ─────────────────────────────────────────────────────
        Stage = "0 hlsl";
        AssetDatabase.ImportAsset(FolHlsl, ImportAssetOptions.ForceUpdate);
        HlslGuid = AssetDatabase.AssetPathToGUID(FolHlsl);
        if (string.IsNullOrEmpty(HlslGuid))
        {
            log.AppendLine("ABORT: нет GUID у " + FolHlsl);
            Debug.Log(log.ToString());
            return log.ToString();
        }
        log.AppendLine("hlsl: foliage=" + HlslGuid);

        // ── 1. донор и шаблоны ───────────────────────────────────────────────
        Stage = "1 donor";
        string txt = File.ReadAllText(Root + "/" + SrcGraph).Replace("\r\n", "\n").Replace("\r", "\n");
        LoadDonor(txt);
        log.AppendLine("donor: docs=" + DOrder.Count.ToString(CultureInfo.InvariantCulture));

        string missing = DiscoverTemplates();
        if (missing != null)
        {
            log.AppendLine("ABORT: в доноре не найден шаблон " + missing);
            Debug.Log(log.ToString());
            return log.ToString();
        }
        log.AppendLine("templates: graph=" + S(TmplGraph) + " target=" + S(TmplTarget) + " sub=" + S(TmplSub)
            + " pos=" + S(TmplPos) + " nfh=" + S(TmplNfh) + " cf=" + S(TmplCf)
            + " mul=" + S(TmplMul) + " add=" + S(TmplAdd) + " lerp=" + S(TmplLerp)
            + " smooth=" + S(TmplSmooth) + " sat=" + S(TmplSat)
            + " propF=" + S(TmplPropF) + " propC=" + S(TmplPropC)
            + " pnF=" + S(TmplPnF) + " pnC=" + S(TmplPnC)
            + " cfIn3=" + S(TmplCfIn3) + " cfIn1=" + S(TmplCfIn1) + " cfOut=" + S(TmplCfOut));

        // GraphData донора обязан быть пуст по группам/заметкам/ключевым словам
        string gdSrc = DById[TmplGraph];
        log.AppendLine("donor GraphData: keywords=" + Cnt(gdSrc, "m_Keywords") + " dropdowns=" + Cnt(gdSrc, "m_Dropdowns")
            + " categories=" + Cnt(gdSrc, "m_CategoryData") + " groups=" + Cnt(gdSrc, "m_GroupDatas")
            + " stickies=" + Cnt(gdSrc, "m_StickyNoteDatas") + " subdatas=" + Cnt(gdSrc, "m_SubDatas"));
        string bad = null;
        foreach (string k in new[] { "m_Keywords", "m_Dropdowns", "m_CategoryData", "m_GroupDatas", "m_StickyNoteDatas", "m_SubDatas" })
        {
            if (Cnt(gdSrc, k) != 0) { bad = k; break; }
        }
        if (bad != null)
        {
            log.AppendLine("ABORT: GraphData донора не пуст по " + bad + " — эти массивы не переносятся сборщиком");
            Debug.Log(log.ToString());
            return log.ToString();
        }

        // ── 2. каркас: GraphData, таргеты, 9 блоков ───────────────────────────
        Stage = "2 frame";
        List<string> none;
        string gdNew = Copy(TmplGraph, false, out none);   // первым в файле
        string tgtNew = Copy(TmplTarget, false, out none);
        string subNew = Copy(TmplSub, false, out none);

        var blockOf = new Dictionary<string, string>();
        foreach (string old in DOrder)
        {
            if (DType[old] != "BlockNode") continue;
            string nd = Copy(old, true, out none);
            N(nd);
            blockOf[StrD(old, "m_SerializedDescriptor")] = nd;
        }
        string missBlk = null;
        foreach (string d in new[] {
            "VertexDescription.Position", "VertexDescription.Normal", "VertexDescription.Tangent",
            "SurfaceDescription.BaseColor", "SurfaceDescription.Smoothness", "SurfaceDescription.NormalTS",
            "SurfaceDescription.Emission", "SurfaceDescription.Occlusion", "SurfaceDescription.Metallic" })
            if (!blockOf.ContainsKey(d)) { missBlk = d; break; }
        if (missBlk != null)
        {
            log.AppendLine("ABORT: не найден блок мастер-стека " + missBlk);
            Debug.Log(log.ToString());
            return log.ToString();
        }

        var vBlocks = new List<string>();
        foreach (string o in BlockIds(gdSrc, "m_VertexContext")) vBlocks.Add(blockOf[StrD(o, "m_SerializedDescriptor")]);
        var fBlocks = new List<string>();
        foreach (string o in BlockIds(gdSrc, "m_FragmentContext")) fBlocks.Add(blockOf[StrD(o, "m_SerializedDescriptor")]);
        log.AppendLine("2. каркас: target=" + S(tgtNew) + " sub=" + S(subNew) + " blocks=" + blockOf.Count.ToString(CultureInfo.InvariantCulture)
            + " (vertex " + vBlocks.Count.ToString(CultureInfo.InvariantCulture)
            + ", fragment " + fBlocks.Count.ToString(CultureInfo.InvariantCulture) + ")");

        // ── 3. входы: Position (Object) и Normal Vector (Object) ──────────────
        Stage = "3 inputs";
        string posN = Copy(TmplPos, true, out none);
        N(posN);
        OById[posN] = SetStr(OById[posN], "m_Name", "Object Position");
        SetPosOn(posN, -6600, 3300);
        Port OBJP = P(posN, 0);

        string nrmN = Copy(TmplPos, true, out none);       // NormalVectorNode в доноре отсутствует
        N(nrmN);
        OById[nrmN] = SetStr(OById[nrmN], "m_Type", "UnityEditor.ShaderGraph.NormalVectorNode");
        OById[nrmN] = SetStr(OById[nrmN], "m_Name", "Object Normal");
        OById[nrmN] = Regex.Replace(OById[nrmN], ",\\s*\"m_PositionSource\"\\s*:\\s*-?\\d+", "");
        SetPosOn(nrmN, -6600, 3700);
        Port OBJN = P(nrmN, 0);
        log.AppendLine("3. входы: position=" + S(posN) + " (m_Space=" + Num(OById[posN], "m_Space") + ")"
            + " normalVector=" + S(nrmN) + " (m_Space=" + Num(OById[nrmN], "m_Space")
            + ", m_PositionSource=" + (OById[nrmN].Contains("m_PositionSource") ? "ОСТАЛСЯ" : "убран") + ")");

        // ── 4. свойства ──────────────────────────────────────────────────────
        Stage = "4 props";
        Port pLeafScale   = PropF("Leaf Scale", 6.5f);
        Port pLeafFlatten = PropF("Leaf Flatten", 5.0f);
        Port pLeafStretch = PropF("Leaf Stretch", 1.6f);
        Port pLeafEdge    = PropF("Leaf Edge", 0.45f);
        Port pLeafSeed    = PropF("Leaf Seed", 3.7f);

        Port pLeafTone      = PropF("Leaf Tone", 0.85f);
        Port pLeafEdgeAmt   = PropF("Leaf Edge Amount", 0.55f);
        Port pLeafGloss     = PropF("Leaf Gloss", 0.10f);
        Port pLeafEdgeGloss = PropF("Leaf Edge Gloss", 0.28f);

        Port pVeinWidth = PropF("Vein Width", 0.035f);
        Port pVeinCount = PropF("Vein Count", 5.0f);
        Port pVeinBump  = PropF("Vein Bump", -0.30f);

        Port pMassScale     = PropF("Mass Scale", 0.9f);
        Port pMassStretch   = PropF("Mass Stretch", 1.0f);
        Port pMassSeed      = PropF("Mass Seed", 7.1f);
        Port pMassThreshold = PropF("Mass Threshold", 0.42f);
        Port pMassSoftness  = PropF("Mass Softness", 0.28f);
        Port pMassAmount    = PropF("Mass Amount", 0.75f);
        Port pMassBump      = PropF("Mass Bump", 0.55f);

        Port pDryScale     = PropF("Dry Scale", 1.7f);
        Port pDryStretch   = PropF("Dry Stretch", 1.9f);
        Port pDrySeed      = PropF("Dry Seed", 13.4f);
        Port pDryThreshold = PropF("Dry Threshold", 0.50f);
        Port pDrySoftness  = PropF("Dry Softness", 0.22f);
        Port pDryAmount    = PropF("Dry Amount", 0.60f);

        Port pBleachScale     = PropF("Bleach Scale", 0.7f);
        Port pBleachStretch   = PropF("Bleach Stretch", 0.8f);
        Port pBleachSeed      = PropF("Bleach Seed", 31.7f);
        Port pBleachThreshold = PropF("Bleach Threshold", 0.52f);
        Port pBleachSoftness  = PropF("Bleach Softness", 0.26f);
        Port pBleachAmount    = PropF("Bleach Amount", 0.45f);

        Port pTwigScale  = PropF("Twig Scale", 1.9f);
        Port pTwigWidth  = PropF("Twig Width", 0.10f);
        Port pTwigSeed   = PropF("Twig Seed", 5.3f);
        Port pTwigAmount = PropF("Twig Amount", 0.45f);
        Port pTwigBump   = PropF("Twig Bump", -0.35f);

        Port pBumpStrength = PropF("Bump Strength", 1.0f);
        Port pSmoothness   = PropF("Smoothness", 0.22f);

        Port cLeaf      = PropC("Leaf Color", 0.24f, 0.36f, 0.16f);
        Port cLeafLight = PropC("Leaf Light Color", 0.46f, 0.55f, 0.24f);
        Port cLeafDeep  = PropC("Leaf Deep Color", 0.10f, 0.17f, 0.09f);
        Port cDry       = PropC("Dry Color", 0.44f, 0.33f, 0.15f);
        Port cTwig      = PropC("Twig Color", 0.19f, 0.14f, 0.10f);
        Port cGap       = PropC("Gap Color", 0.05f, 0.07f, 0.04f);

        log.AppendLine("4. свойства: float=" + OProps.Count.ToString(CultureInfo.InvariantCulture));

        // ── 5. CF-ноды ───────────────────────────────────────────────────────
        Stage = "5 cf";
        string cfLeaf = MakeCf("ProceduralFoliageLeaf", "Leaf",
            new[] { "ObjectPos", "ObjectNormal", "LeafScale", "LeafFlatten", "LeafStretch", "LeafEdge", "LeafSeed" },
            new[] { true, true, false, false, false, false, false }, "Leaf", -3200, -3400);
        string cfTone = MakeCf("ProceduralFoliageLeafTone", "Leaf Tone",
            new[] { "ObjectPos", "ObjectNormal", "LeafScale", "LeafFlatten", "LeafStretch", "LeafSeed" },
            new[] { true, true, false, false, false, false }, "LeafTone", -3200, -2200);
        string cfVein = MakeCf("ProceduralFoliageLeafVein", "Leaf Vein",
            new[] { "ObjectPos", "ObjectNormal", "LeafScale", "LeafFlatten", "LeafStretch", "LeafVeinWidth", "LeafVeinCount", "LeafSeed" },
            new[] { true, true, false, false, false, false, false, false }, "LeafVein", -3200, -1000);
        string cfMass = MakeCf("ProceduralFoliagePatch", "Mass Patch",
            new[] { "ObjectPos", "PatchScale", "PatchStretch", "PatchSeed" },
            new[] { true, false, false, false }, "Patch", -3200, 200);
        string cfDry = MakeCf("ProceduralFoliagePatch", "Dry Patch",
            new[] { "ObjectPos", "PatchScale", "PatchStretch", "PatchSeed" },
            new[] { true, false, false, false }, "Patch", -3200, 1400);
        string cfBleach = MakeCf("ProceduralFoliagePatch", "Bleach Patch",
            new[] { "ObjectPos", "PatchScale", "PatchStretch", "PatchSeed" },
            new[] { true, false, false, false }, "Patch", -3200, 2600);
        string cfTwig = MakeCf("ProceduralFoliageTwigs", "Twigs",
            new[] { "ObjectPos", "TwigScale", "TwigWidth", "TwigSeed" },
            new[] { true, false, false, false }, "Twigs", -3200, 3800);
        log.AppendLine("5. CF-нод: 7 (Leaf/Tone/Vein/Patch x3/Twigs) -> " + FolHlsl);

        Port leafOut = P(cfLeaf, 7);
        Port toneOut = P(cfTone, 6);
        Port veinOut = P(cfVein, 8);
        Port massOut = P(cfMass, 4);
        Port dryOut  = P(cfDry, 4);
        Port bleaOut = P(cfBleach, 4);
        Port twigOut = P(cfTwig, 4);

        // общие входы
        Link(OBJP, P(cfLeaf, 0)); Link(OBJP, P(cfTone, 0)); Link(OBJP, P(cfVein, 0));
        Link(OBJP, P(cfMass, 0)); Link(OBJP, P(cfDry, 0));  Link(OBJP, P(cfBleach, 0));
        Link(OBJP, P(cfTwig, 0));
        Link(OBJN, P(cfLeaf, 1)); Link(OBJN, P(cfTone, 1)); Link(OBJN, P(cfVein, 1));

        Link(pLeafScale, P(cfLeaf, 2)); Link(pLeafScale, P(cfTone, 2)); Link(pLeafScale, P(cfVein, 2));
        Link(pLeafFlatten, P(cfLeaf, 3)); Link(pLeafFlatten, P(cfTone, 3)); Link(pLeafFlatten, P(cfVein, 3));
        Link(pLeafStretch, P(cfLeaf, 4)); Link(pLeafStretch, P(cfTone, 4)); Link(pLeafStretch, P(cfVein, 4));
        Link(pLeafSeed, P(cfLeaf, 6)); Link(pLeafSeed, P(cfTone, 5)); Link(pLeafSeed, P(cfVein, 7));
        Link(pLeafEdge, P(cfLeaf, 5));
        Link(pVeinWidth, P(cfVein, 5)); Link(pVeinCount, P(cfVein, 6));

        Link(pMassScale, P(cfMass, 1)); Link(pMassStretch, P(cfMass, 2)); Link(pMassSeed, P(cfMass, 3));
        Link(pDryScale, P(cfDry, 1)); Link(pDryStretch, P(cfDry, 2)); Link(pDrySeed, P(cfDry, 3));
        Link(pBleachScale, P(cfBleach, 1)); Link(pBleachStretch, P(cfBleach, 2)); Link(pBleachSeed, P(cfBleach, 3));
        Link(pTwigScale, P(cfTwig, 1)); Link(pTwigWidth, P(cfTwig, 2)); Link(pTwigSeed, P(cfTwig, 3));

        // ── 6. маски: Smoothstep(порог, порог+мягкость, рисунок) ──────────────
        Stage = "6 masks";
        string massMask = Mask("Mass Mask", massOut, pMassThreshold, pMassSoftness, -2100, 140);
        string dryMask  = Mask("Dry Mask",  dryOut,  pDryThreshold,  pDrySoftness,  -2100, 1340);
        string bleaMask = Mask("Bleach Mask", bleaOut, pBleachThreshold, pBleachSoftness, -2100, 2540);

        // ── 7. альбедо: 6 Lerp ───────────────────────────────────────────────
        Stage = "7 albedo";
        // L1: тон листа
        string mulTone = Op(TmplMul, "Leaf Tone x Tone", -2100, -3400);
        Link(toneOut, P(mulTone, 0)); Link(pLeafTone, P(mulTone, 1));
        string lerp1 = Op(TmplLerp, "Leaf Color by Tone", -1500, -3400);
        Link(cLeaf, P(lerp1, 0)); Link(cLeafLight, P(lerp1, 1)); Link(P(mulTone, 2), P(lerp1, 2));

        // L2: просвет — где масса разрежена, видно в глубину кроны
        string lerpInv = Op(TmplLerp, "One Minus Mass", -2100, -2700);
        SetVecConst(lerpInv, 0, 1f, 1f, 1f, 1f);
        SetVecConst(lerpInv, 1, 0f, 0f, 0f, 0f);
        Link(P(massMask, 3), P(lerpInv, 2));
        string mulGap = Op(TmplMul, "See Through x Mass Amount", -1500, -2600);
        Link(P(lerpInv, 3), P(mulGap, 0)); Link(pMassAmount, P(mulGap, 1));
        string lerp2 = Op(TmplLerp, "Gap Color by See Through", -900, -2900);
        Link(P(lerp1, 3), P(lerp2, 0)); Link(cGap, P(lerp2, 1)); Link(P(mulGap, 2), P(lerp2, 2));

        // L3: выгорание
        string mulBlea = Op(TmplMul, "Bleach x Bleach Amount", -300, -2300);
        Link(P(bleaMask, 3), P(mulBlea, 0)); Link(pBleachAmount, P(mulBlea, 1));
        string lerp3 = Op(TmplLerp, "Bleach Color", 300, -2300);
        Link(P(lerp2, 3), P(lerp3, 0)); Link(cLeafLight, P(lerp3, 1)); Link(P(mulBlea, 2), P(lerp3, 2));

        // L4: сухость по кромкам (только в теле листа)
        string mulDry1 = Op(TmplMul, "Dry x Dry Amount", -300, -1700);
        Link(P(dryMask, 3), P(mulDry1, 0)); Link(pDryAmount, P(mulDry1, 1));
        string mulDry2 = Op(TmplMul, "Dry x Leaf", 300, -1700);
        Link(P(mulDry1, 2), P(mulDry2, 0)); Link(leafOut, P(mulDry2, 1));
        string lerp4 = Op(TmplLerp, "Dry Color", 900, -1700);
        Link(P(lerp3, 3), P(lerp4, 0)); Link(cDry, P(lerp4, 1)); Link(P(mulDry2, 2), P(lerp4, 2));

        // L5: затемнение кромки к глубокому тону
        string lerpEdge = Op(TmplLerp, "One Minus Leaf", -2100, -900);
        SetVecConst(lerpEdge, 0, 1f, 1f, 1f, 1f);
        SetVecConst(lerpEdge, 1, 0f, 0f, 0f, 0f);
        Link(leafOut, P(lerpEdge, 2));
        string mulEdge = Op(TmplMul, "Edge x Edge Amount", -900, -1100);
        Link(P(lerpEdge, 3), P(mulEdge, 0)); Link(pLeafEdgeAmt, P(mulEdge, 1));
        string lerp5 = Op(TmplLerp, "Deep Color by Edge", 1500, -1100);
        Link(P(lerp4, 3), P(lerp5, 0)); Link(cLeafDeep, P(lerp5, 1)); Link(P(mulEdge, 2), P(lerp5, 2));

        // L6: сучья
        string mulTwig = Op(TmplMul, "Twigs x Twig Amount", 900, -500);
        Link(twigOut, P(mulTwig, 0)); Link(pTwigAmount, P(mulTwig, 1));
        string lerp6 = Op(TmplLerp, "Twig Color", 2100, -500);
        Link(P(lerp5, 3), P(lerp6, 0)); Link(cTwig, P(lerp6, 1)); Link(P(mulTwig, 2), P(lerp6, 2));

        Link(P(lerp6, 3), P(blockOf["SurfaceDescription.BaseColor"], 0));

        // ── 8. глянец ────────────────────────────────────────────────────────
        Stage = "8 gloss";
        string mulGloss1 = Op(TmplMul, "Tone x Leaf Gloss", 900, 900);
        Link(toneOut, P(mulGloss1, 0)); Link(pLeafGloss, P(mulGloss1, 1));
        string mulGloss2 = Op(TmplMul, "Edge x Edge Gloss", 900, 1200);
        Link(P(lerpEdge, 3), P(mulGloss2, 0)); Link(pLeafEdgeGloss, P(mulGloss2, 1));
        string addGloss1 = Op(TmplAdd, "Smoothness + Tone Gloss", 1500, 900);
        Link(pSmoothness, P(addGloss1, 0)); Link(P(mulGloss1, 2), P(addGloss1, 1));
        string addGloss2 = Op(TmplAdd, "+ Edge Gloss", 2100, 900);
        Link(P(addGloss1, 2), P(addGloss2, 0)); Link(P(mulGloss2, 2), P(addGloss2, 1));
        string satGloss = Op(TmplSat, "Smoothness Clamp", 2700, 900);
        Link(P(addGloss2, 2), P(satGloss, 0));
        Link(P(satGloss, 1), P(blockOf["SurfaceDescription.Smoothness"], 0));

        // ── 9. рельеф ────────────────────────────────────────────────────────
        Stage = "9 relief";
        string mulVeinBump = Op(TmplMul, "Vein Bump", -1500, 2000);
        Link(veinOut, P(mulVeinBump, 0)); Link(pVeinBump, P(mulVeinBump, 1));
        string mulTwigBump = Op(TmplMul, "Twig Bump", -900, 2000);
        Link(twigOut, P(mulTwigBump, 0)); Link(pTwigBump, P(mulTwigBump, 1));
        string mulMassBump = Op(TmplMul, "Mass Bump", -300, 2000);
        Link(P(massMask, 3), P(mulMassBump, 0)); Link(pMassBump, P(mulMassBump, 1));

        string addH1 = Op(TmplAdd, "Leaf + Vein", -1500, 2400);
        Link(leafOut, P(addH1, 0)); Link(P(mulVeinBump, 2), P(addH1, 1));
        string addH2 = Op(TmplAdd, "+ Twigs", -900, 2400);
        Link(P(addH1, 2), P(addH2, 0)); Link(P(mulTwigBump, 2), P(addH2, 1));
        string addH3 = Op(TmplAdd, "+ Mass", -300, 2400);
        Link(P(addH2, 2), P(addH3, 0)); Link(P(mulMassBump, 2), P(addH3, 1));

        string nfhN = Copy(TmplNfh, true, out none);
        N(nfhN);
        OById[nfhN] = SetStr(OById[nfhN], "m_Name", "Leaf Relief");
        SetPosOn(nfhN, 900, 2400);
        Link(P(addH3, 2), P(nfhN, 0));          // In   (runtime id 0)
        Link(pBumpStrength, P(nfhN, 2));        // Strength (runtime id 2) — дефолт 0.01 гасит рельеф
        Link(P(nfhN, 1), P(blockOf["SurfaceDescription.NormalTS"], 0));   // Out (runtime id 1)

        // VertexDescription.Position <- Object Position (тождественная подстановка)
        Link(OBJP, P(blockOf["VertexDescription.Position"], 0));

        log.AppendLine("6..9. ноды: " + ONodes.Count.ToString(CultureInfo.InvariantCulture)
            + " рёбер: " + Edges.Count.ToString(CultureInfo.InvariantCulture));

        // ── 10. сборка GraphData ─────────────────────────────────────────────
        Stage = "10 graphdata";
        var seenP = new HashSet<string>();
        foreach (string p in OProps) if (!seenP.Add(p)) { log.AppendLine("ABORT: дубль свойства"); Debug.Log(log.ToString()); return log.ToString(); }
        var seenN = new HashSet<string>();
        foreach (string n in ONodes) if (!seenN.Add(n)) { log.AppendLine("ABORT: дубль ноды"); Debug.Log(log.ToString()); return log.ToString(); }

        string problem = null;
        var seenDest = new HashSet<string>();
        foreach (Edge e in Edges)
        {
            if (e.S == null || e.D == null) { problem = "ребро с пустым концом"; break; }
            if (!seenN.Contains(e.S)) { problem = "источник ребра не в m_Nodes: " + S(e.S); break; }
            if (!seenN.Contains(e.D)) { problem = "приёмник ребра не в m_Nodes: " + S(e.D); break; }
            if (!HasSlot(e.S, e.SS, true)) { problem = "у " + S(e.S) + " нет выходного слота #" + e.SS; break; }
            if (!HasSlot(e.D, e.DS, false)) { problem = "у " + S(e.D) + " нет входного слота #" + e.DS; break; }
            if (!seenDest.Add(e.D + "#" + e.DS.ToString(CultureInfo.InvariantCulture)))
            { problem = "вход " + S(e.D) + "#" + e.DS + " подключён дважды"; break; }
        }
        if (problem != null)
        {
            log.AppendLine("ABORT: " + problem);
            Debug.Log(log.ToString());
            return log.ToString();
        }
        // каждый вход мастера и каждый CF-вход не обязан быть занят, но у блоков
        // BaseColor/Smoothness/NormalTS он обязан быть (иначе слой не подключён)
        foreach (string d in new[] { "SurfaceDescription.BaseColor", "SurfaceDescription.Smoothness", "SurfaceDescription.NormalTS" })
            if (!seenDest.Contains(blockOf[d] + "#0"))
            {
                log.AppendLine("ABORT: блок " + d + " не подключён");
                Debug.Log(log.ToString());
                return log.ToString();
            }

        string gdOut = OById[gdNew];
        gdOut = ReplaceArray(gdOut, "m_Properties", IdArray(OProps));
        gdOut = ReplaceArray(gdOut, "m_Nodes", IdArray(ONodes));
        gdOut = ReplaceArray(gdOut, "m_Edges", EdgeArray(Edges));
        gdOut = ReplaceArray(gdOut, "m_ActiveTargets", IdArray(new List<string>(new[] { tgtNew })));
        int bi = 0;
        gdOut = Regex.Replace(gdOut, "\"m_Blocks\"\\s*:\\s*\\[[^\\]]*\\]", me =>
        {
            string content = bi == 0 ? IdArray(vBlocks) : IdArray(fBlocks);
            bi++;
            return "\"m_Blocks\": " + content;
        }, RegexOptions.Singleline);
        if (bi != 2)
        {
            log.AppendLine("ABORT: контекстов m_Blocks найдено " + bi + " вместо 2");
            Debug.Log(log.ToString());
            return log.ToString();
        }
        OById[gdNew] = gdOut;

        // ── 11. запись ──────────────────────────────────────────────────────
        Stage = "11 write";
        var sb = new StringBuilder();
        for (int i = 0; i < OOrder.Count; i++)
        {
            if (i > 0) sb.Append("\n\n");
            sb.Append(OById[OOrder[i]]);
        }
        string outText = sb.ToString().Replace("\r\n", "\n").Replace("\r", "\n");
        if (!outText.EndsWith("\n")) outText += "\n";

        string[] parts = Regex.Split(outText, "\n\n");
        foreach (string part in parts)
            if (Regex.IsMatch(part, "\n[ \\t]*\n"))
            {
                log.AppendLine("ABORT: пустая строка внутри документа");
                Debug.Log(log.ToString());
                return log.ToString();
            }
        foreach (string part in parts)
            if (part.Trim().Length > 0 && !part.TrimStart().StartsWith("{"))
            {
                log.AppendLine("ABORT: документ не начинается с { : " + part.Substring(0, Math.Min(60, part.Length)));
                Debug.Log(log.ToString());
                return log.ToString();
            }

        log.AppendLine("11. выход: docs=" + parts.Length.ToString(CultureInfo.InvariantCulture)
            + " props=" + OProps.Count.ToString(CultureInfo.InvariantCulture)
            + " nodes=" + ONodes.Count.ToString(CultureInfo.InvariantCulture)
            + " edges=" + Edges.Count.ToString(CultureInfo.InvariantCulture)
            + " chars=" + outText.Length.ToString(CultureInfo.InvariantCulture));

        if (!AssetDatabase.IsValidFolder(OutFolder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "Foliage");
            log.AppendLine("    создана папка " + OutFolder);
        }
        File.WriteAllText(Root + "/" + OutGraph, outText, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutGraph, ImportAssetOptions.ForceUpdate);

        // ── 12. импорт ──────────────────────────────────────────────────────
        Stage = "12 import";
        Shader sh = Shader.Find(ShaderName);
        if (sh == null)
        {
            log.AppendLine("12. IMPORT: Shader.Find(\"" + ShaderName + "\") = null");
            string dump = OutFolder + "/dump_foliage.shadergraph";
            File.WriteAllText(Root + "/" + dump, outText + "\n");
            log.AppendLine("    копия текста: " + dump);
        }
        else
        {
            log.AppendLine("12. IMPORT: " + ShaderName + " hasError=" + ShaderUtil.ShaderHasError(sh)
                + " messages=" + ShaderUtil.GetShaderMessages(sh).Length.ToString(CultureInfo.InvariantCulture)
                + " supported=" + sh.isSupported);
        }

        // ── 13. лог редактора: единственный надёжный источник ошибок шейдера ─
        Stage = "13 log";
        log.AppendLine("13. " + ScanLog());

        Debug.Log(log.ToString());
        return log.ToString();
    }

    // ── построение ────────────────────────────────────────────────────────────

    static string Mask(string name, Port patch, Port threshold, Port softness, float x, float y)
    {
        string add = Op(TmplAdd, name + " Upper Edge", x - 500, y - 400);
        Link(threshold, P(add, 0)); Link(softness, P(add, 1));
        string ss = Op(TmplSmooth, name, x, y);
        Link(threshold, P(ss, 0));          // Edge1
        Link(P(add, 2), P(ss, 1));          // Edge2 = порог + мягкость
        Link(patch, P(ss, 2));              // In
        return ss;
    }

    static string Op(string tmplId, string name, float x, float y)
    {
        List<string> none;
        string nid = Copy(tmplId, true, out none);
        N(nid);
        OById[nid] = SetStr(OById[nid], "m_Name", name);
        SetPosOn(nid, x, y);
        return nid;
    }

    static string MakeCf(string fn, string display, string[] ins, bool[] vec3, string outName, float x, float y)
    {
        List<string> none;
        string nid = Copy(TmplCf, false, out none);
        N(nid);
        OById[nid] = SetStr(OById[nid], "m_FunctionName", fn);
        OById[nid] = SetStr(OById[nid], "m_Name", display + " (Custom Function)");
        OById[nid] = SetStr(OById[nid], "m_FunctionSource", HlslGuid);
        SetPosOn(nid, x, y);

        var slots = new List<string>();
        for (int i = 0; i < ins.Length; i++)
            slots.Add(CopySlot(vec3[i] ? TmplCfIn3 : TmplCfIn1, i, ins[i], 0));
        slots.Add(CopySlot(TmplCfOut, ins.Length, outName, 1));
        OById[nid] = SetSlotArray(OById[nid], slots);
        OSlots[nid] = slots;
        return nid;
    }

    static Port PropF(string display, float val)
    {
        string pid = NewId();
        string d = SetObjId(DById[TmplPropF], pid);
        d = SetStr(d, "m_Name", display);
        d = SetStr(d, "m_DefaultReferenceName", RefOf(display));
        d = SetStr(d, "m_RefNameGeneratedByDisplayName", display);
        d = SetStr(d, "m_GuidSerialized", Guid.NewGuid().ToString());
        d = SetNumF(d, "m_Value", val);
        OById[pid] = d; OOrder.Add(pid); OProps.Add(pid);
        return PropNode(TmplPnF, pid, display);
    }

    static Port PropC(string display, float r, float g, float b)
    {
        string pid = NewId();
        string d = SetObjId(DById[TmplPropC], pid);
        d = SetStr(d, "m_Name", display);
        d = SetStr(d, "m_DefaultReferenceName", RefOf(display));
        d = SetStr(d, "m_RefNameGeneratedByDisplayName", display);
        d = SetStr(d, "m_GuidSerialized", Guid.NewGuid().ToString());
        d = SetColor(d, r, g, b, 1f);
        OById[pid] = d; OOrder.Add(pid); OProps.Add(pid);
        return PropNode(TmplPnC, pid, display);
    }

    static Port PropNode(string tmplId, string propId, string display)
    {
        List<string> slots;
        string nid = Copy(tmplId, true, out slots);
        N(nid);
        OById[nid] = SetPropertyRef(OById[nid], propId);
        OById[nid] = SetStr(OById[nid], "m_Name", display);
        SetPosOn(nid, -6600, PropY);
        PropY += 190f;
        foreach (string s in slots) OById[s] = SetStr(OById[s], "m_DisplayName", display);
        return P(nid, 0);
    }

    static string RefOf(string display) { return "_" + display.Replace(" ", "_"); }

    // ── порты и рёбра ─────────────────────────────────────────────────────────
    static Port P(string node, int slot) { return new Port { N = node, S = slot }; }

    static void Link(Port src, Port dst)
    {
        Edges.Add(new Edge { S = src.N, SS = src.S, D = dst.N, DS = dst.S });
    }

    static bool HasSlot(string nodeId, int num, bool isOutput)
    {
        List<string> ss;
        if (!OSlots.TryGetValue(nodeId, out ss)) return false;
        foreach (string s in ss)
        {
            if (Num(OById[s], "m_Id") != num) continue;
            return Num(OById[s], "m_SlotType") == (isOutput ? 1 : 0);
        }
        return false;
    }

    static string SlotObjOf(string nodeId, int num)
    {
        List<string> ss;
        if (!OSlots.TryGetValue(nodeId, out ss)) return null;
        foreach (string s in ss) if (Num(OById[s], "m_Id") == num) return s;
        return null;
    }

    static void SetVecConst(string nodeId, int slotNum, float x, float y, float z, float w)
    {
        string so = SlotObjOf(nodeId, slotNum);
        if (so == null) return;
        OById[so] = Regex.Replace(OById[so], "(\"m_Value\"\\s*:\\s*)\\{[^}]*\\}",
            "${1}{\"x\":" + F(x) + ",\"y\":" + F(y) + ",\"z\":" + F(z) + ",\"w\":" + F(w) + "}");
    }

    static void N(string id) { ONodes.Add(id); }

    // ── копирование документов донора ────────────────────────────────────────
    static string Copy(string srcId, bool withSlots, out List<string> newSlots)
    {
        var map = new Dictionary<string, string>();
        string nid = NewId();
        map[srcId] = nid;

        var pairs = new List<string[]>();
        if (withSlots)
        {
            foreach (string os in DSlots(DById[srcId]))
            {
                string ns = NewId();
                map[os] = ns;
                pairs.Add(new[] { os, ns });
            }
        }

        OById[nid] = Remap(SetObjId(DById[srcId], nid), map);
        OOrder.Add(nid);

        newSlots = new List<string>();
        foreach (string[] p in pairs)
        {
            OById[p[1]] = Remap(SetObjId(DById[p[0]], p[1]), map);
            OOrder.Add(p[1]);
            newSlots.Add(p[1]);
        }
        if (withSlots) OSlots[nid] = newSlots;
        return nid;
    }

    static string CopySlot(string srcSlotId, int num, string name, int slotType)
    {
        string ns = NewId();
        string d = SetObjId(DById[srcSlotId], ns);
        d = SetNum(d, "m_Id", num);
        d = SetStr(d, "m_DisplayName", name);
        d = SetStr(d, "m_ShaderOutputName", name);
        d = SetNum(d, "m_SlotType", slotType);
        OById[ns] = d;
        OOrder.Add(ns);
        return ns;
    }

    static void SetPosOn(string nodeId, float x, float y)
    {
        OById[nodeId] = SetPos(OById[nodeId], (int)x, (int)y);
    }

    static string Remap(string doc, Dictionary<string, string> map)
    {
        return Regex.Replace(doc, "\"([0-9a-f]{32})\"", me =>
        {
            string v;
            return map.TryGetValue(me.Groups[1].Value, out v) ? "\"" + v + "\"" : me.Value;
        });
    }

    // ── разведка донора ──────────────────────────────────────────────────────
    static void LoadDonor(string text)
    {
        foreach (string raw in Regex.Split(text, "\n\n"))
        {
            string d = raw.Trim();
            if (d.Length == 0) continue;
            Match mo = Regex.Match(d, "\"m_ObjectId\"\\s*:\\s*\"([0-9a-f]{32})\"");
            if (!mo.Success) continue;
            string id = mo.Groups[1].Value;
            DById[id] = d;
            DOrder.Add(id);
            Match mt = Regex.Match(d, "\"m_Type\"\\s*:\\s*\"([^\"]+)\"");
            string t = mt.Success ? mt.Groups[1].Value : "?";
            DType[id] = t.Substring(t.LastIndexOf('.') + 1);
        }
    }

    static string DiscoverTemplates()
    {
        TmplGraph = FirstOfType("GraphData");
        TmplTarget = FirstOfType("UniversalTarget");
        TmplSub = FirstOfType("UniversalLitSubTarget");
        TmplPos = FirstOfType("PositionNode");
        TmplNfh = FirstOfType("NormalFromHeightNode");
        TmplMul = FirstOfType("MultiplyNode");
        TmplAdd = FirstOfType("AddNode");
        TmplLerp = FirstOfType("LerpNode");
        TmplSmooth = FirstOfType("SmoothstepNode");
        TmplSat = FirstOfType("SaturateNode");
        TmplPropF = FirstOfType("Vector1ShaderProperty");
        TmplPropC = FirstOfType("ColorShaderProperty");
        TmplPnF = PropNodeOfType("Vector1ShaderProperty");
        TmplPnC = PropNodeOfType("ColorShaderProperty");
        TmplCf = CfByFunc("ProceduralBrickPatch");

        if (TmplGraph == null) return "GraphData";
        if (TmplTarget == null) return "UniversalTarget";
        if (TmplSub == null) return "UniversalLitSubTarget";
        if (TmplPos == null) return "PositionNode";
        if (TmplNfh == null) return "NormalFromHeightNode";
        if (TmplMul == null) return "MultiplyNode";
        if (TmplAdd == null) return "AddNode";
        if (TmplLerp == null) return "LerpNode";
        if (TmplSmooth == null) return "SmoothstepNode";
        if (TmplSat == null) return "SaturateNode";
        if (TmplPropF == null) return "Vector1ShaderProperty";
        if (TmplPropC == null) return "ColorShaderProperty";
        if (TmplPnF == null) return "PropertyNode(Vector1)";
        if (TmplPnC == null) return "PropertyNode(Color)";
        if (TmplCf == null) return "CustomFunctionNode ProceduralBrickPatch";

        // слоты CF-шаблона: float3-вход / скалярный вход / выход
        string in3 = null, in1 = null, outS = null;
        foreach (string so in DSlots(DById[TmplCf]))
        {
            string sid = DById[so];
            if (Num(sid, "m_SlotType") == 1) { if (outS == null) outS = so; }
            else if (DType[so] == "Vector3MaterialSlot" && in3 == null) in3 = so;
            else if (DType[so] == "Vector1MaterialSlot" && in1 == null) in1 = so;
        }
        TmplCfIn3 = in3; TmplCfIn1 = in1; TmplCfOut = outS;
        if (in3 == null) return "слот CF float3";
        if (in1 == null) return "слот CF скаляр";
        if (outS == null) return "слот CF выход";
        return null;
    }

    static string FirstOfType(string t) { foreach (string id in DOrder) if (DType[id] == t) return id; return null; }

    static string CfByFunc(string fn)
    {
        foreach (string id in DOrder)
            if (DType[id] == "CustomFunctionNode" && DById[id].Contains("\"m_FunctionName\": \"" + fn + "\"")) return id;
        return null;
    }

    static string PropNodeOfType(string propType)
    {
        foreach (string id in DOrder)
        {
            if (DType[id] != "PropertyNode") continue;
            Match mp = Regex.Match(DById[id], "\"m_Property\"\\s*:\\s*\\{\\s*\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"");
            if (mp.Success && DType.ContainsKey(mp.Groups[1].Value) && DType[mp.Groups[1].Value] == propType) return id;
        }
        return null;
    }

    static string StrD(string id, string key)
    {
        Match m = Regex.Match(DById[id], "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : "?";
    }

    static int Num(string doc, string key)
    {
        Match m = Regex.Match(doc, "\"" + key + "\"\\s*:\\s*(-?[0-9]+)");
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : -99999;
    }

    static int Cnt(string doc, string key)
    {
        Match m = Regex.Match(doc, "\"" + key + "\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if (!m.Success) return -1;
        return Regex.Matches(m.Groups[1].Value, "\\{").Count;
    }

    static List<string> DSlots(string doc)
    {
        var res = new List<string>();
        Match m = Regex.Match(doc, "\"m_Slots\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if (!m.Success) return res;
        foreach (Match x in Regex.Matches(m.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) res.Add(x.Groups[1].Value);
        return res;
    }

    static List<string> BlockIds(string gdDoc, string outerKey)
    {
        var res = new List<string>();
        int i = gdDoc.IndexOf("\"" + outerKey + "\"");
        if (i < 0) return res;
        Match m = Regex.Match(gdDoc.Substring(i), "\"m_Blocks\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if (!m.Success) return res;
        foreach (Match x in Regex.Matches(m.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) res.Add(x.Groups[1].Value);
        return res;
    }

    // ── текстовые правки ────────────────────────────────────────────────────
    static string S(string id) { return id == null ? "null" : id.Substring(0, 8); }
    static string NewId() { return Guid.NewGuid().ToString("N"); }
    static string F(float v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }

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

    static string SetColor(string doc, float r, float g, float b, float a)
    {
        return Regex.Replace(doc, "(\"m_Value\"\\s*:\\s*)\\{[^}]*\\}",
            "${1}{\"r\":" + F(r) + ",\"g\":" + F(g) + ",\"b\":" + F(b) + ",\"a\":" + F(a) + "}");
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
            sb.Append("\n{\n\"m_OutputSlot\": {\n\"m_Node\": {\n\"m_Id\": \"" + es[i].S + "\"\n},\n\"m_SlotId\": "
                + es[i].SS.ToString(CultureInfo.InvariantCulture)
                + "\n},\n\"m_InputSlot\": {\n\"m_Node\": {\n\"m_Id\": \"" + es[i].D + "\"\n},\n\"m_SlotId\": "
                + es[i].DS.ToString(CultureInfo.InvariantCulture) + "\n}\n}");
        }
        sb.Append("\n]");
        return sb.ToString();
    }

    // ── лог редактора ───────────────────────────────────────────────────────
    static string ScanLog()
    {
        string p = Root + "/Logs/Editor.log";
        if (!File.Exists(p)) return "Logs/Editor.log не найден";
        using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            long start = Math.Max(0, fs.Length - 600000);
            fs.Seek(start, SeekOrigin.Begin);
            var buf = new byte[fs.Length - start];
            int read = fs.Read(buf, 0, buf.Length);
            string lt = new UTF8Encoding(false).GetString(buf, 0, read);
            MatchCollection mc = Regex.Matches(lt, "Shader error in '" + ShaderName + "':[^\\n]*");
            var sb = new StringBuilder("Logs/Editor.log: \"Shader error in '" + ShaderName + "'\" = "
                + mc.Count.ToString(CultureInfo.InvariantCulture));
            int shown = 0;
            foreach (Match m in mc)
            {
                if (shown++ >= 4) break;
                sb.Append("\n    " + m.Value.Trim());
            }
            return sb.ToString();
        }
    }
}
