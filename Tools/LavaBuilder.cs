using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// LavaBuilder — собирает Assets/_Project/Materials/Lava/Lava.shadergraph
// С НУЛЯ: от донора Brick берутся ТОЛЬКО шаблоны документов, дерево Brick не
// наследуется. Копируются как есть и получают новые ObjectId:
//   GraphData (переписываются m_Properties/m_Nodes/m_Edges/m_Blocks/m_ActiveTargets),
//   UniversalTarget, UniversalLitSubTarget, 9 блоков мастер-стека, PositionNode,
//   NormalFromHeightNode, математические ноды, PropertyNode, Vector1ShaderProperty,
//   ColorShaderProperty, CustomFunctionNode.
//
//   Донор Brick — ТОЛЬКО ЧТЕНИЕ: ни один байт исходников не перезаписывается.
//
// Слои лавы (см. Assets/_Project/Shaders/ProceduralLavaNoise.hlsl):
//   Fissures  — трещины между плитами корки (Worley F2 − F1)
//   PlateTone — тон плиты корки (хеш ТОЙ ЖЕ ячейки, что и трещины)
//   Heat      — крупное поле жара (пороги в графе: heat и molten)
//   Flow      — линии потока вдоль локальной Y
//   Skin      — мелкая шлаковая корка
//
// АЛЬБЕДО — 7 Lerp: тон плиты -> зола -> жар -> расплав -> трещина -> ядро ->
// тёмные «верёвки» потока поверх расплава.
// ГЛЯНЕЦ — Saturate(_Smoothness + расплав*_Molten_Gloss): корка матовая,
// расплав глянцевый.
// ЭМИССИЯ — Lerp-цепочка (тот же цвет, что и альбедо) x Glow x
// _Emission_Strength, где Glow = Saturate(трещина x жар + расплав) x
// _Glow_Amount. Блок SurfaceDescription.Emission — ColorRGBMaterialSlot (RGB,
// ColorMode = HDR), в URP PBRForwardPass его значение уходит в surface.emission
// БЕЗ ключевого слова, поэтому подключение блока и есть включение эмиссии.
// РЕЛЬЕФ — Add(тон плиты*_Plate_Bump, трещина*_Fissure_Bump, шлак*_Skin_Bump,
//          поток*_Flow_Bump, жар*_Heat_Bump) -> NormalFromHeight.In;
//          NFH.Strength <- _Bump_Strength (шаблонный дефолт 0.01 гасил бы
//          рельеф стократно).
//
// ПРАВИЛО (T-BRONZE01): функции CF-нод одно-выходные (File-режим); порядок
// слотов CF-ноды ОБЯЗАН совпадать с порядком параметров HLSL, `out` — последним;
// связывание позиционное. Ни одна существующая сигнатура не меняется.
public static class LavaBuilder
{
    const string SrcGraph  = "Assets/_Project/Materials/Brick/Brick.shadergraph";
    const string OutFolder = "Assets/_Project/Materials/Lava";
    const string OutGraph  = OutFolder + "/Lava.shadergraph";
    const string LavaHlsl  = "Assets/_Project/Shaders/ProceduralLavaNoise.hlsl";
    const string ShaderName = "Lava";

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
            string m = "LavaBuilder FAILED at stage [" + Stage + "]: " + ex.GetType().Name + ": " + ex.Message;
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
        AssetDatabase.ImportAsset(LavaHlsl, ImportAssetOptions.ForceUpdate);
        HlslGuid = AssetDatabase.AssetPathToGUID(LavaHlsl);
        if (string.IsNullOrEmpty(HlslGuid))
        {
            log.AppendLine("ABORT: нет GUID у " + LavaHlsl);
            Debug.Log(log.ToString());
            return log.ToString();
        }
        log.AppendLine("hlsl: lava=" + HlslGuid);

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

        // ── 3. вход: Position (Object) ───────────────────────────────────────
        // Объектная нормаль лаве не нужна: корка — 3D-сетка ячеек, а не
        // поверхностный рисунок (в отличие от листвы, где нормаль сплющивала
        // ячейку в лист).
        Stage = "3 input";
        string posN = Copy(TmplPos, true, out none);
        N(posN);
        OById[posN] = SetStr(OById[posN], "m_Name", "Object Position");
        SetPosOn(posN, -6600, 3300);
        Port OBJP = P(posN, 0);
        log.AppendLine("3. вход: position=" + S(posN) + " (m_Space=" + Num(OById[posN], "m_Space") + ")");

        // ── 4. свойства ──────────────────────────────────────────────────────
        Stage = "4 props";
        // корка
        Port pCrustScale   = PropF("Crust Scale", 1.5f);
        Port pCrustStretch = PropF("Crust Stretch", 2.4f);
        Port pCrustWidth   = PropF("Crust Width", 0.09f);
        Port pCrustSeed    = PropF("Crust Seed", 4.3f);
        Port pPlateVary    = PropF("Plate Vary", 0.55f);
        Port pPlateBump    = PropF("Plate Bump", 0.18f);
        Port pFissureBump  = PropF("Fissure Bump", -0.55f);

        // жар
        Port pHeatScale     = PropF("Heat Scale", 0.85f);
        Port pHeatStretch   = PropF("Heat Stretch", 1.5f);
        Port pHeatSeed      = PropF("Heat Seed", 12.7f);
        Port pHeatThreshold = PropF("Heat Threshold", 0.40f);
        Port pHeatSoftness  = PropF("Heat Softness", 0.34f);
        Port pHeatBump      = PropF("Heat Bump", 0.30f);

        // расплав
        Port pMoltenThreshold = PropF("Molten Threshold", 0.62f);
        Port pMoltenSoftness  = PropF("Molten Softness", 0.22f);

        // поток
        Port pFlowScale   = PropF("Flow Scale", 2.6f);
        Port pFlowStretch = PropF("Flow Stretch", 5.0f);
        Port pFlowWidth   = PropF("Flow Width", 0.25f);
        Port pFlowSeed    = PropF("Flow Seed", 27.1f);
        Port pFlowAmount  = PropF("Flow Amount", 0.55f);
        Port pFlowBump    = PropF("Flow Bump", 0.35f);

        // шлак
        Port pSkinScale  = PropF("Skin Scale", 14.0f);
        Port pSkinDetail = PropF("Skin Detail", 2.4f);
        Port pSkinSeed   = PropF("Skin Seed", 8.9f);
        Port pSkinAmount = PropF("Skin Amount", 0.40f);
        Port pSkinBump   = PropF("Skin Bump", 0.28f);

        // свечение, глянец, рельеф
        Port pGlowAmount       = PropF("Glow Amount", 1.0f);
        Port pEmissionStrength = PropF("Emission Strength", 3.0f);
        Port pMoltenGloss      = PropF("Molten Gloss", 0.55f);
        Port pSmoothness       = PropF("Smoothness", 0.34f);
        Port pBumpStrength     = PropF("Bump Strength", 1.0f);

        Port cCrust  = PropC("Crust Color", 0.055f, 0.050f, 0.048f);
        Port cPlate  = PropC("Plate Color", 0.110f, 0.100f, 0.095f);
        Port cAsh    = PropC("Ash Color", 0.170f, 0.160f, 0.150f);
        Port cEmber  = PropC("Ember Color", 0.420f, 0.075f, 0.020f);
        Port cMelt   = PropC("Melt Color", 1.000f, 0.360f, 0.050f);
        Port cStream = PropC("Stream Color", 1.000f, 0.720f, 0.120f);
        Port cCore   = PropC("Core Color", 1.000f, 0.940f, 0.620f);

        log.AppendLine("4. свойства: float=" + OProps.Count.ToString(CultureInfo.InvariantCulture));

        // ── 5. CF-ноды ───────────────────────────────────────────────────────
        Stage = "5 cf";
        string cfFiss = MakeCf("ProceduralLavaFissures", "Fissures",
            new[] { "ObjectPos", "CrustScale", "CrustStretch", "CrustWidth", "CrustSeed" },
            new[] { true, false, false, false, false }, "Fissures", -3200, -3400);
        string cfPlate = MakeCf("ProceduralLavaPlateTone", "Plate Tone",
            new[] { "ObjectPos", "CrustScale", "CrustStretch", "CrustSeed" },
            new[] { true, false, false, false }, "PlateTone", -3200, -2200);
        string cfHeat = MakeCf("ProceduralLavaHeat", "Heat",
            new[] { "ObjectPos", "HeatScale", "HeatStretch", "HeatSeed" },
            new[] { true, false, false, false }, "Heat", -3200, -1000);
        string cfFlow = MakeCf("ProceduralLavaFlow", "Flow",
            new[] { "ObjectPos", "FlowScale", "FlowStretch", "FlowWidth", "FlowSeed" },
            new[] { true, false, false, false, false }, "Flow", -3200, 200);
        string cfSkin = MakeCf("ProceduralLavaSkin", "Skin",
            new[] { "ObjectPos", "SkinScale", "SkinDetail", "SkinSeed" },
            new[] { true, false, false, false }, "Skin", -3200, 1400);
        log.AppendLine("5. CF-нод: 5 (Fissures/PlateTone/Heat/Flow/Skin) -> " + LavaHlsl);

        Port fissOut  = P(cfFiss, 5);
        Port plateOut = P(cfPlate, 4);
        Port heatOut  = P(cfHeat, 4);
        Port flowOut  = P(cfFlow, 5);
        Port skinOut  = P(cfSkin, 4);

        Link(OBJP, P(cfFiss, 0)); Link(OBJP, P(cfPlate, 0)); Link(OBJP, P(cfHeat, 0));
        Link(OBJP, P(cfFlow, 0)); Link(OBJP, P(cfSkin, 0));

        // трещины и тон плиты считаются ОДНИМ доменом (один и тот же Worley):
        // Scale/Stretch/Seed подаются из одних и тех же свойств
        Link(pCrustScale, P(cfFiss, 1)); Link(pCrustScale, P(cfPlate, 1));
        Link(pCrustStretch, P(cfFiss, 2)); Link(pCrustStretch, P(cfPlate, 2));
        Link(pCrustWidth, P(cfFiss, 3));
        Link(pCrustSeed, P(cfFiss, 4)); Link(pCrustSeed, P(cfPlate, 3));

        Link(pHeatScale, P(cfHeat, 1)); Link(pHeatStretch, P(cfHeat, 2)); Link(pHeatSeed, P(cfHeat, 3));

        Link(pFlowScale, P(cfFlow, 1)); Link(pFlowStretch, P(cfFlow, 2));
        Link(pFlowWidth, P(cfFlow, 3)); Link(pFlowSeed, P(cfFlow, 4));

        Link(pSkinScale, P(cfSkin, 1)); Link(pSkinDetail, P(cfSkin, 2)); Link(pSkinSeed, P(cfSkin, 3));

        // ── 6. маски: Smoothstep(порог, порог+мягкость, рисунок) ─────────────
        // Одно поле жара в двух ролях: «жар» (корка тонка, видно свечение) и
        // «расплав» (корки нет вовсе) — второй порог выше первого.
        Stage = "6 masks";
        string heatMask = Mask("Heat Mask", heatOut, pHeatThreshold, pHeatSoftness, -2100, 140);
        string moltMask = Mask("Molten Mask", heatOut, pMoltenThreshold, pMoltenSoftness, -2100, 1340);

        // ── 7. производные величины ──────────────────────────────────────────
        Stage = "7 derived";
        string mulPlate = Op(TmplMul, "Plate Tone x Plate Vary", -2100, -3400);
        Link(plateOut, P(mulPlate, 0)); Link(pPlateVary, P(mulPlate, 1));

        string mulAsh = Op(TmplMul, "Skin x Skin Amount", -2100, -2600);
        Link(skinOut, P(mulAsh, 0)); Link(pSkinAmount, P(mulAsh, 1));

        // свечение трещины = трещина x жар: без гашения жаром вся сетка ячеек
        // светилась бы целиком и читалась бы паутиной, а не лавой
        string mulCrack = Op(TmplMul, "Fissure x Heat", -2100, -1800);
        Link(fissOut, P(mulCrack, 0)); Link(P(heatMask, 3), P(mulCrack, 1));

        string addGlow = Op(TmplAdd, "Crack + Molten", -1500, -1800);
        Link(P(mulCrack, 2), P(addGlow, 0)); Link(P(moltMask, 3), P(addGlow, 1));

        string mulGlowAmt = Op(TmplMul, "Glow x Glow Amount", -900, -1800);
        Link(P(addGlow, 2), P(mulGlowAmt, 0)); Link(pGlowAmount, P(mulGlowAmt, 1));

        string glowFin = Op(TmplSat, "Glow", -300, -1800);
        Link(P(mulGlowAmt, 2), P(glowFin, 0));

        // белокалёное ядро: трещина В расплаве
        string mulCore = Op(TmplMul, "Molten x Fissure", -1500, -1100);
        Link(P(moltMask, 3), P(mulCore, 0)); Link(fissOut, P(mulCore, 1));

        // «верёвки» потока: тёмная корка на гребнях струй поверх расплава
        string mulRope1 = Op(TmplMul, "Flow x Molten", -1500, -400);
        Link(flowOut, P(mulRope1, 0)); Link(P(moltMask, 3), P(mulRope1, 1));
        string mulRope2 = Op(TmplMul, "Flow Ropes x Flow Amount", -900, -400);
        Link(P(mulRope1, 2), P(mulRope2, 0)); Link(pFlowAmount, P(mulRope2, 1));

        // ── 8. альбедо: 7 Lerp ───────────────────────────────────────────────
        // Порядок наложения — от старого к новому: корка -> зола -> жар ->
        // расплав -> трещина -> ядро -> верёвки (корка остывает последней).
        Stage = "8 albedo";
        string lerp1 = Op(TmplLerp, "Crust by Plate", -900, -3400);
        Link(cCrust, P(lerp1, 0)); Link(cPlate, P(lerp1, 1)); Link(P(mulPlate, 2), P(lerp1, 2));

        string lerp2 = Op(TmplLerp, "Ash", -300, -3400);
        Link(P(lerp1, 3), P(lerp2, 0)); Link(cAsh, P(lerp2, 1)); Link(P(mulAsh, 2), P(lerp2, 2));

        string lerp3 = Op(TmplLerp, "Ember Pools", 300, -3400);
        Link(P(lerp2, 3), P(lerp3, 0)); Link(cEmber, P(lerp3, 1)); Link(P(heatMask, 3), P(lerp3, 2));

        string lerp4 = Op(TmplLerp, "Melt", 900, -3400);
        Link(P(lerp3, 3), P(lerp4, 0)); Link(cMelt, P(lerp4, 1)); Link(P(moltMask, 3), P(lerp4, 2));

        string lerp5 = Op(TmplLerp, "Fissure Line", 1500, -3400);
        Link(P(lerp4, 3), P(lerp5, 0)); Link(cStream, P(lerp5, 1)); Link(P(mulCrack, 2), P(lerp5, 2));

        string lerp6 = Op(TmplLerp, "Core", 2100, -3400);
        Link(P(lerp5, 3), P(lerp6, 0)); Link(cCore, P(lerp6, 1)); Link(P(mulCore, 2), P(lerp6, 2));

        string lerp7 = Op(TmplLerp, "Flow Ropes", 2700, -3400);
        Link(P(lerp6, 3), P(lerp7, 0)); Link(cCrust, P(lerp7, 1)); Link(P(mulRope2, 2), P(lerp7, 2));

        Link(P(lerp7, 3), P(blockOf["SurfaceDescription.BaseColor"], 0));

        // ── 9. эмиссия ───────────────────────────────────────────────────────
        // Цвет эмиссии — та же Lerp-цепочка: горячая трещина светится своим
        // цветом, остывшая корка не светится вовсе (Glow там 0).
        Stage = "9 emission";
        string mulEmis1 = Op(TmplMul, "Albedo x Glow", 3300, -3400);
        Link(P(lerp7, 3), P(mulEmis1, 0)); Link(P(glowFin, 1), P(mulEmis1, 1));
        string mulEmis2 = Op(TmplMul, "x Emission Strength", 3900, -3400);
        Link(P(mulEmis1, 2), P(mulEmis2, 0)); Link(pEmissionStrength, P(mulEmis2, 1));
        Link(P(mulEmis2, 2), P(blockOf["SurfaceDescription.Emission"], 0));

        // ── 10. глянец ───────────────────────────────────────────────────────
        Stage = "10 gloss";
        string mulMoltGloss = Op(TmplMul, "Molten x Molten Gloss", 900, 900);
        Link(P(moltMask, 3), P(mulMoltGloss, 0)); Link(pMoltenGloss, P(mulMoltGloss, 1));
        string addGloss = Op(TmplAdd, "Smoothness + Molten Gloss", 1500, 900);
        Link(pSmoothness, P(addGloss, 0)); Link(P(mulMoltGloss, 2), P(addGloss, 1));
        string satGloss = Op(TmplSat, "Smoothness Clamp", 2100, 900);
        Link(P(addGloss, 2), P(satGloss, 0));
        Link(P(satGloss, 1), P(blockOf["SurfaceDescription.Smoothness"], 0));

        // ── 11. рельеф ───────────────────────────────────────────────────────
        Stage = "11 relief";
        string mulPlateBump = Op(TmplMul, "Plate Bump", -1500, 2000);
        Link(P(mulPlate, 2), P(mulPlateBump, 0)); Link(pPlateBump, P(mulPlateBump, 1));
        string mulFissBump = Op(TmplMul, "Fissure Bump", -900, 2000);
        Link(fissOut, P(mulFissBump, 0)); Link(pFissureBump, P(mulFissBump, 1));
        string mulSkinBump = Op(TmplMul, "Skin Bump", -300, 2000);
        Link(skinOut, P(mulSkinBump, 0)); Link(pSkinBump, P(mulSkinBump, 1));
        string mulFlowBump = Op(TmplMul, "Flow Bump", 300, 2000);
        Link(flowOut, P(mulFlowBump, 0)); Link(pFlowBump, P(mulFlowBump, 1));
        string mulHeatBump = Op(TmplMul, "Heat Bump", 900, 2000);
        Link(heatOut, P(mulHeatBump, 0)); Link(pHeatBump, P(mulHeatBump, 1));

        string addH1 = Op(TmplAdd, "Skin + Plate", -1500, 2400);
        Link(P(mulSkinBump, 2), P(addH1, 0)); Link(P(mulPlateBump, 2), P(addH1, 1));
        string addH2 = Op(TmplAdd, "+ Fissures", -900, 2400);
        Link(P(addH1, 2), P(addH2, 0)); Link(P(mulFissBump, 2), P(addH2, 1));
        string addH3 = Op(TmplAdd, "+ Flow", -300, 2400);
        Link(P(addH2, 2), P(addH3, 0)); Link(P(mulFlowBump, 2), P(addH3, 1));
        string addH4 = Op(TmplAdd, "+ Heat", 300, 2400);
        Link(P(addH3, 2), P(addH4, 0)); Link(P(mulHeatBump, 2), P(addH4, 1));

        string nfhN = Copy(TmplNfh, true, out none);
        N(nfhN);
        OById[nfhN] = SetStr(OById[nfhN], "m_Name", "Crust Relief");
        SetPosOn(nfhN, 900, 2400);
        Link(P(addH4, 2), P(nfhN, 0));          // In   (runtime id 0)
        Link(pBumpStrength, P(nfhN, 2));        // Strength (runtime id 2) — дефолт 0.01 гасит рельеф
        Link(P(nfhN, 1), P(blockOf["SurfaceDescription.NormalTS"], 0));   // Out (runtime id 1)

        // VertexDescription.Position <- Object Position (тождественная подстановка)
        Link(OBJP, P(blockOf["VertexDescription.Position"], 0));

        log.AppendLine("6..11. ноды: " + ONodes.Count.ToString(CultureInfo.InvariantCulture)
            + " рёбер: " + Edges.Count.ToString(CultureInfo.InvariantCulture));

        // ── 12. сборка GraphData ─────────────────────────────────────────────
        Stage = "12 graphdata";
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
        // BaseColor/Smoothness/NormalTS/Emission он обязан быть (иначе слой не подключён)
        foreach (string d in new[] {
            "SurfaceDescription.BaseColor", "SurfaceDescription.Smoothness",
            "SurfaceDescription.NormalTS", "SurfaceDescription.Emission" })
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

        // ── 13. запись ──────────────────────────────────────────────────────
        Stage = "13 write";
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
            if (Regex.IsMatch(part, "\n[ \t]*\n"))
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

        log.AppendLine("13. выход: docs=" + parts.Length.ToString(CultureInfo.InvariantCulture)
            + " props=" + OProps.Count.ToString(CultureInfo.InvariantCulture)
            + " nodes=" + ONodes.Count.ToString(CultureInfo.InvariantCulture)
            + " edges=" + Edges.Count.ToString(CultureInfo.InvariantCulture)
            + " chars=" + outText.Length.ToString(CultureInfo.InvariantCulture));

        if (!AssetDatabase.IsValidFolder(OutFolder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "Lava");
            log.AppendLine("    создана папка " + OutFolder);
        }
        File.WriteAllText(Root + "/" + OutGraph, outText, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutGraph, ImportAssetOptions.ForceUpdate);

        // ── 14. импорт ──────────────────────────────────────────────────────
        Stage = "14 import";
        Shader sh = Shader.Find(ShaderName);
        if (sh == null)
        {
            log.AppendLine("14. IMPORT: Shader.Find(\"" + ShaderName + "\") = null");
            string dump = OutFolder + "/dump_lava.shadergraph";
            File.WriteAllText(Root + "/" + dump, outText + "\n");
            log.AppendLine("    копия текста: " + dump);
        }
        else
        {
            log.AppendLine("14. IMPORT: " + ShaderName + " hasError=" + ShaderUtil.ShaderHasError(sh)
                + " messages=" + ShaderUtil.GetShaderMessages(sh).Length.ToString(CultureInfo.InvariantCulture)
                + " supported=" + sh.isSupported);
        }

        // ── 15. лог редактора: единственный надёжный источник ошибок шейдера ─
        Stage = "15 log";
        log.AppendLine("15. " + ScanLog());

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
