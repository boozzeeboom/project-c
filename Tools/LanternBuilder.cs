using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// LanternBuilder — собирает Assets/_Project/Materials/Lantern/Lantern.shadergraph
// С НУЛЯ: от донора Brick берутся ТОЛЬКО шаблоны документов, дерево Brick не
// наследуется. Копируются как есть и получают новые ObjectId:
//   GraphData (переписываются m_Properties/m_Nodes/m_Edges/m_Blocks/m_ActiveTargets),
//   UniversalTarget, UniversalLitSubTarget, 9 блоков мастер-стека, PositionNode,
//   NormalFromHeightNode, математические ноды, PropertyNode, Vector1ShaderProperty,
//   ColorShaderProperty, CustomFunctionNode.
//
//   Донор Brick — ТОЛЬКО ЧТЕНИЕ: ни один байт исходников не перезаписывается.
//
// Слои фонаря (см. Assets/_Project/Shaders/ProceduralLanternNoise.hlsl):
//   Glow — крупное поле жара за стеклом
//   Core — тонкие нити «хребта» fbm: ядро пламени
//   Soot — копоть на стекле (садится вверх по локальной Y)
//   Pane — стыки стёкол (границы ячеек Worley)
//
// АЛЬБЕДО — 4 Lerp: стекло -> копоть -> раскалённое поле -> ядро -> стыки.
// ГЛЯНЕЦ — Saturate(_Glass_Smoothness + копоть x _Soot_Roughness), где
// _Soot_Roughness отрицательна: копоть матирует стекло.
// ЭМИССИЯ — НЕ из альбедо: своя палитра (Lerp(_Glow_Color -> _Core_Color по
// ядру) x (жар + ядро x _Core_Glow) x _Glow_Amount x _Emission_Strength x
// мерцание). Блок SurfaceDescription.Emission — ColorRGBMaterialSlot (RGB,
// ColorMode = HDR), в URP PBRForwardPass его значение уходит в surface.emission
// БЕЗ ключевого слова, поэтому подключение блока и есть включение эмиссии.
// МЕРЦАНИЕ — _Flicker_Base + Time.SineTime x _Flicker_Amount: живой огонь, а не
// постоянная яркость.
// РЕЛЬЕФ — Add(копоть x _Soot_Bump, жар x _Glow_Bump, стыки x _Pane_Bump) ->
//          NormalFromHeight.In; NFH.Strength <- _Bump_Strength (шаблонный
//          дефолт 0.01 гасил бы рельеф стократно).
//
// АНИМАЦИЯ. Время подаётся в ObjectPos нод Glow и Core (не в сид: сид — скаляр,
// он увёл бы домен по всем трём осям сразу), направление (0,-1,0) — рисунок
// поднимается ВВЕРХ по локальной Y (жар идёт вверх, в отличие от лавы).
// Soot и Pane не дрейфуют: стекло фонаря стоит на месте.
//
// ПРАВИЛО (T-BRONZE01): функции CF-нод одно-выходные (File-режим); порядок
// слотов CF-ноды ОБЯЗАН совпадать с порядком параметров HLSL, `out` — последним;
// связывание позиционное. Ни одна существующая сигнатура не меняется.
public static class LanternBuilder
{
    const string SrcGraph  = "Assets/_Project/Materials/Brick/Brick.shadergraph";
    const string OutFolder = "Assets/_Project/Materials/Lantern";
    const string OutGraph  = OutFolder + "/Lantern.shadergraph";
    const string LanternHlsl = "Assets/_Project/Shaders/ProceduralLanternNoise.hlsl";
    const string ShaderName = "Lantern";

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
            string m = "LanternBuilder FAILED at stage [" + Stage + "]: " + ex.GetType().Name + ": " + ex.Message;
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
        AssetDatabase.ImportAsset(LanternHlsl, ImportAssetOptions.ForceUpdate);
        HlslGuid = AssetDatabase.AssetPathToGUID(LanternHlsl);
        if (string.IsNullOrEmpty(HlslGuid))
        {
            log.AppendLine("ABORT: нет GUID у " + LanternHlsl);
            Debug.Log(log.ToString());
            return log.ToString();
        }
        log.AppendLine("hlsl: lantern=" + HlslGuid);

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
        // Объектная нормаль фонарю не нужна: копоть и жар — 3D-поля, а не
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
        // жар за стеклом
        Port pGlowScale     = PropF("Glow Scale", 1.7f);
        Port pGlowStretch   = PropF("Glow Stretch", 1.25f);
        Port pGlowSeed      = PropF("Glow Seed", 7.1f);
        Port pGlowThreshold = PropF("Glow Threshold", 0.34f);
        Port pGlowSoftness  = PropF("Glow Softness", 0.38f);
        Port pGlowBump      = PropF("Glow Bump", 0.22f);

        // ядро пламени
        Port pCoreScale     = PropF("Core Scale", 3.6f);
        Port pCoreStretch   = PropF("Core Stretch", 1.9f);
        Port pCoreSeed      = PropF("Core Seed", 21.3f);
        Port pCoreThreshold = PropF("Core Threshold", 0.52f);
        Port pCoreSoftness  = PropF("Core Softness", 0.26f);
        Port pCoreGlow      = PropF("Core Glow", 0.85f);

        // копоть
        Port pSootScale  = PropF("Soot Scale", 6.5f);
        Port pSootDetail = PropF("Soot Detail", 2.0f);
        Port pSootSeed   = PropF("Soot Seed", 33.7f);
        Port pSootTilt   = PropF("Soot Tilt", 0.40f);
        Port pSootAmount = PropF("Soot Amount", 0.65f);
        Port pSootRough  = PropF("Soot Roughness", -0.37f);
        Port pSootBump   = PropF("Soot Bump", 0.18f);

        // стыки стёкол
        Port pPaneScale   = PropF("Pane Scale", 9.0f);
        Port pPaneStretch = PropF("Pane Stretch", 1.6f);
        Port pPaneWidth   = PropF("Pane Width", 0.075f);
        Port pPaneSeed    = PropF("Pane Seed", 15.9f);
        Port pPaneAmount  = PropF("Pane Amount", 0.60f);
        Port pPaneBump    = PropF("Pane Bump", -0.30f);

        // свет, мерцание, глянец, рельеф
        Port pGlowAmount       = PropF("Glow Amount", 1.0f);
        Port pEmissionStrength = PropF("Emission Strength", 4.0f);
        Port pFlickerBase      = PropF("Flicker Base", 0.88f);
        Port pFlickerAmount    = PropF("Flicker Amount", 0.12f);
        Port pSmoothness       = PropF("Glass Smoothness", 0.58f);
        Port pBumpStrength     = PropF("Bump Strength", 1.0f);

        Port cGlass  = PropC("Glass Color", 0.045f, 0.042f, 0.038f);
        Port cSoot   = PropC("Soot Color", 0.018f, 0.016f, 0.014f);
        Port cEmber  = PropC("Ember Color", 0.420f, 0.100f, 0.020f);
        Port cGlow   = PropC("Glow Color", 1.000f, 0.580f, 0.160f);
        Port cCore   = PropC("Core Color", 1.000f, 0.920f, 0.660f);
        // анимация: СКОРОСТИ подъёма доменов жара и ядра (см. стадию 5b).
        // Создаются ПОСЛЕ цветов, чтобы y-координаты уже существующих
        // PropertyNode не сдвинулись.
        Port pGlowSpeed = PropF("Glow Speed", 0.10f);
        Port pCoreSpeed = PropF("Core Speed", 0.22f);

        log.AppendLine("4. свойства: float=" + OProps.Count.ToString(CultureInfo.InvariantCulture));

        // ── 5. CF-ноды ───────────────────────────────────────────────────────
        Stage = "5 cf";
        string cfGlow = MakeCf("ProceduralLanternGlow", "Glow",
            new[] { "ObjectPos", "GlowScale", "GlowStretch", "GlowSeed" },
            new[] { true, false, false, false }, "Glow", -3200, -2600);
        string cfCore = MakeCf("ProceduralLanternCore", "Core",
            new[] { "ObjectPos", "CoreScale", "CoreStretch", "CoreSeed" },
            new[] { true, false, false, false }, "Core", -3200, -1400);
        string cfSoot = MakeCf("ProceduralLanternSoot", "Soot",
            new[] { "ObjectPos", "SootScale", "SootDetail", "SootSeed", "SootTilt", "SootAmount" },
            new[] { true, false, false, false, false, false }, "Soot", -3200, -200);
        string cfPane = MakeCf("ProceduralLanternPane", "Pane",
            new[] { "ObjectPos", "PaneScale", "PaneStretch", "PaneWidth", "PaneSeed" },
            new[] { true, false, false, false, false }, "Pane", -3200, 1000);
        log.AppendLine("5. CF-нод: 4 (Glow/Core/Soot/Pane) -> " + LanternHlsl);

        Port glowOut = P(cfGlow, 4);
        Port coreOut = P(cfCore, 4);
        Port sootOut = P(cfSoot, 6);
        Port paneOut = P(cfPane, 5);

        // входы ObjectPos нод Glow и Core подаются в 5b, общим для каждой пары
        // (дрейф), входы Soot и Pane — прямо здесь: стекло не движется.
        Link(OBJP, P(cfSoot, 0));
        Link(OBJP, P(cfPane, 0));

        Link(pGlowScale, P(cfGlow, 1)); Link(pGlowStretch, P(cfGlow, 2)); Link(pGlowSeed, P(cfGlow, 3));
        Link(pCoreScale, P(cfCore, 1)); Link(pCoreStretch, P(cfCore, 2)); Link(pCoreSeed, P(cfCore, 3));
        Link(pSootScale, P(cfSoot, 1)); Link(pSootDetail, P(cfSoot, 2)); Link(pSootSeed, P(cfSoot, 3));
        Link(pSootTilt, P(cfSoot, 4)); Link(pSootAmount, P(cfSoot, 5));
        Link(pPaneScale, P(cfPane, 1)); Link(pPaneStretch, P(cfPane, 2));
        Link(pPaneWidth, P(cfPane, 3)); Link(pPaneSeed, P(cfPane, 4));

        // ── 5b. анимация домена: жар и ядро ПОДНИМАЮТСЯ ─────────────────────
        // Время подаётся в ObjectPos нод Glow и Core, а не в их сид: сид —
        // скаляр, он увёл бы домен по всем трём осям сразу. HLSL при этом не
        // меняется вообще, ядро фонаря остаётся без времени.
        //   Направление дрейфа (0,-1,0). Смещение добавляется к object-space
        //   позиции ДО умножения на масштаб слоя (p = ObjectPos * Scale + Seed),
        //   поэтому ВИДИМАЯ скорость = _Speed x Scale / Stretch, а знак обратен
        //   знаку сдвига: положительная скорость с направлением (0,-1,0)
        //   поднимает рисунок ВВЕРХ по локальной Y (жар идёт вверх — это не
        //   лава, которая течёт вниз). Отсюда разные _Glow_Speed и _Core_Speed.
        //   Смещается только проба: VertexDescription.Position по-прежнему
        //   получает прямой OBJP, геометрия не едет.
        Stage = "5b rise";
        string timeN = MakeTimeNode(-6600, 3900);
        Port timeOut = P(timeN, 0);
        string riseDir = MakeVector3Const(0f, -1f, 0f, -6600, 4300);
        Port dirOut = P(riseDir, 0);

        string mulGlowSpeed = Op(TmplMul, "Time x Glow Speed", -6000, 3900);
        Link(timeOut, P(mulGlowSpeed, 0)); Link(pGlowSpeed, P(mulGlowSpeed, 1));
        string mulGlowRise = Op(TmplMul, "Glow Rise Y", -5400, 3900);
        Link(P(mulGlowSpeed, 2), P(mulGlowRise, 0)); Link(dirOut, P(mulGlowRise, 1));
        string addGlowPos = Op(TmplAdd, "Glow Domain Rise", -4800, 3900);
        Link(OBJP, P(addGlowPos, 0)); Link(P(mulGlowRise, 2), P(addGlowPos, 1));
        Link(P(addGlowPos, 2), P(cfGlow, 0));

        string mulCoreSpeed = Op(TmplMul, "Time x Core Speed", -6000, 4500);
        Link(timeOut, P(mulCoreSpeed, 0)); Link(pCoreSpeed, P(mulCoreSpeed, 1));
        string mulCoreRise = Op(TmplMul, "Core Rise Y", -5400, 4500);
        Link(P(mulCoreSpeed, 2), P(mulCoreRise, 0)); Link(dirOut, P(mulCoreRise, 1));
        string addCorePos = Op(TmplAdd, "Core Domain Rise", -4800, 4500);
        Link(OBJP, P(addCorePos, 0)); Link(P(mulCoreRise, 2), P(addCorePos, 1));
        Link(P(addCorePos, 2), P(cfCore, 0));
        log.AppendLine("5b. подъём домена: time=" + S(timeN) + " dir=" + S(riseDir)
            + " glow=" + S(addGlowPos) + " core=" + S(addCorePos));

        // ── 6. маски: Smoothstep(порог, порог+мягкость, рисунок) ─────────────
        // Пороги подаются в ЕСТЕСТВЕННОМ порядке (значение в In), как в
        // Concrete/Brick/Plate: у бронзы входы Smoothstep перепутаны, из-за чего
        // износ садится на середину грани вместо кромки.
        Stage = "6 masks";
        string glowMask = Mask("Glow Mask", glowOut, pGlowThreshold, pGlowSoftness, -2100, 200);
        string coreMask = Mask("Core Mask", coreOut, pCoreThreshold, pCoreSoftness, -2100, 1400);

        // ── 7. производные величины ──────────────────────────────────────────
        Stage = "7 derived";
        // ядро добавляет яркости поверх тела фонаря, но не заменяет его:
        // фонарь светит весь, ядро — только самое горячее место
        string mulCoreGlow = Op(TmplMul, "Core Mask x Core Glow", -1500, 1400);
        Link(P(coreMask, 3), P(mulCoreGlow, 0)); Link(pCoreGlow, P(mulCoreGlow, 1));

        string addGlowSum = Op(TmplAdd, "Glow Sum", -900, 200);
        Link(P(glowMask, 3), P(addGlowSum, 0)); Link(P(mulCoreGlow, 2), P(addGlowSum, 1));

        string mulPane = Op(TmplMul, "Pane x Pane Amount", -1500, 200);
        Link(paneOut, P(mulPane, 0)); Link(pPaneAmount, P(mulPane, 1));

        // ── 8. альбедо: 4 Lerp ───────────────────────────────────────────────
        // Порядок наложения — от старого к новому: стекло -> копоть ->
        // раскалённое поле -> ядро -> стыки стёкол.
        Stage = "8 albedo";
        string lerp1 = Op(TmplLerp, "Glass by Soot", -900, -3200);
        Link(cGlass, P(lerp1, 0)); Link(cSoot, P(lerp1, 1)); Link(sootOut, P(lerp1, 2));

        string lerp2 = Op(TmplLerp, "Hot Glass", -300, -3200);
        Link(P(lerp1, 3), P(lerp2, 0)); Link(cEmber, P(lerp2, 1)); Link(P(glowMask, 3), P(lerp2, 2));

        string lerp3 = Op(TmplLerp, "Flame Core", 300, -3200);
        Link(P(lerp2, 3), P(lerp3, 0)); Link(cCore, P(lerp3, 1)); Link(P(coreMask, 3), P(lerp3, 2));

        string lerp4 = Op(TmplLerp, "Pane Seams", 900, -3200);
        Link(P(lerp3, 3), P(lerp4, 0)); Link(cSoot, P(lerp4, 1)); Link(P(mulPane, 2), P(lerp4, 2));

        Link(P(lerp4, 3), P(blockOf["SurfaceDescription.BaseColor"], 0));

        // ── 9. мерцание и эмиссия ────────────────────────────────────────────
        // Мерцание = _Flicker_Base + Time.SineTime x _Flicker_Amount: синус
        // даёт диапазон -1..1, поэтому итог ходит в
        // (_Flicker_Base - _Flicker_Amount) .. (_Flicker_Base + _Flicker_Amount),
        // то есть 0.76..1.00 при дефолтах. _Flicker_Amount = 0 — ровный свет.
        Stage = "9 emission";
        Port sineN = P(timeN, 1);   // Sine Time
        string mulFlick = Op(TmplMul, "Sine x Flicker Amount", -4800, 5100);
        Link(sineN, P(mulFlick, 0)); Link(pFlickerAmount, P(mulFlick, 1));
        string addFlick = Op(TmplAdd, "Flicker Base + Sine", -4200, 5100);
        Link(pFlickerBase, P(addFlick, 0)); Link(P(mulFlick, 2), P(addFlick, 1));

        // Цвет эмиссии — СВОЯ палитра (не альбедо): копоть видна тоном стекла,
        // но свет лампы от неё не выводится, иначе верх фонаря гаснет целиком.
        string emisCol = Op(TmplLerp, "Glow Color to Core Color", 3300, -3200);
        Link(cGlow, P(emisCol, 0)); Link(cCore, P(emisCol, 1)); Link(P(coreMask, 3), P(emisCol, 2));

        string mulEmis1 = Op(TmplMul, "Glow Color x Glow Sum", 3900, -3200);
        Link(P(emisCol, 3), P(mulEmis1, 0)); Link(P(addGlowSum, 2), P(mulEmis1, 1));
        string mulEmis2 = Op(TmplMul, "x Glow Amount", 4500, -3200);
        Link(P(mulEmis1, 2), P(mulEmis2, 0)); Link(pGlowAmount, P(mulEmis2, 1));
        string mulEmis3 = Op(TmplMul, "x Emission Strength", 5100, -3200);
        Link(P(mulEmis2, 2), P(mulEmis3, 0)); Link(pEmissionStrength, P(mulEmis3, 1));
        string mulEmis4 = Op(TmplMul, "x Flicker", 5700, -3200);
        Link(P(mulEmis3, 2), P(mulEmis4, 0)); Link(P(addFlick, 2), P(mulEmis4, 1));
        Link(P(mulEmis4, 2), P(blockOf["SurfaceDescription.Emission"], 0));

        // ── 10. глянец ───────────────────────────────────────────────────────
        // Стекло глянцевое, копоть матовая: _Soot_Roughness отрицательна и
        // складывается с гладкостью, а не вычитается отдельной нодой.
        Stage = "10 gloss";
        string mulSootRough = Op(TmplMul, "Soot x Soot Roughness", 900, 900);
        Link(sootOut, P(mulSootRough, 0)); Link(pSootRough, P(mulSootRough, 1));
        string addGloss = Op(TmplAdd, "Glass Smoothness + Soot", 1500, 900);
        Link(pSmoothness, P(addGloss, 0)); Link(P(mulSootRough, 2), P(addGloss, 1));
        string satGloss = Op(TmplSat, "Smoothness Clamp", 2100, 900);
        Link(P(addGloss, 2), P(satGloss, 0));
        Link(P(satGloss, 1), P(blockOf["SurfaceDescription.Smoothness"], 0));

        // ── 11. рельеф ───────────────────────────────────────────────────────
        // Ядро в рельеф НЕ входит: это свечение за стеклом, а не форма
        // поверхности.
        Stage = "11 relief";
        string mulSootBump = Op(TmplMul, "Soot Bump", -1500, 2000);
        Link(sootOut, P(mulSootBump, 0)); Link(pSootBump, P(mulSootBump, 1));
        string mulGlowBump = Op(TmplMul, "Glow Bump", -900, 2000);
        Link(glowOut, P(mulGlowBump, 0)); Link(pGlowBump, P(mulGlowBump, 1));
        string mulPaneBump = Op(TmplMul, "Pane Bump", -300, 2000);
        Link(paneOut, P(mulPaneBump, 0)); Link(pPaneBump, P(mulPaneBump, 1));

        string addH1 = Op(TmplAdd, "Soot + Glow", -1500, 2400);
        Link(P(mulSootBump, 2), P(addH1, 0)); Link(P(mulGlowBump, 2), P(addH1, 1));
        string addH2 = Op(TmplAdd, "+ Pane", -900, 2400);
        Link(P(addH1, 2), P(addH2, 0)); Link(P(mulPaneBump, 2), P(addH2, 1));

        string nfhN = Copy(TmplNfh, true, out none);
        N(nfhN);
        OById[nfhN] = SetStr(OById[nfhN], "m_Name", "Glass Relief");
        SetPosOn(nfhN, 900, 2400);
        Link(P(addH2, 2), P(nfhN, 0));          // In   (runtime id 0)
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
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "Lantern");
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
            string dump = "Tools/_lantern_dump.shadergraph";
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

    // ── ноды, шаблонов которых у донора нет ──────────────────────────────────
    // TimeNode и Vector3Node в Brick не встречаются, поэтому их документы
    // строятся из каркаса AbstractMaterialNode (шаблон MultiplyNode — из
    // собственных полей у него только базовые) и получают собственные слоты.
    static string MakeTimeNode(float x, float y)
    {
        List<string> none;
        string nid = Copy(TmplMul, false, out none);
        string d = SetStr(OById[nid], "m_Type", "UnityEditor.ShaderGraph.TimeNode");
        d = SetStr(d, "m_Name", "Time");
        d = SetEmptyArray(d, "synonyms");

        // Слоты TimeNode: 5 выходов Vector1, id 0..4. id 0 = IN.TimeParameters.x
        // (секунды с запуска), id 1/2 — синус и косинус, id 3/4 — дельта.
        var slots = new List<string>();
        slots.Add(CopySlot(TmplCfIn1, 0, "Time", 1));
        slots.Add(CopySlot(TmplCfIn1, 1, "Sine Time", 1));
        slots.Add(CopySlot(TmplCfIn1, 2, "Cosine Time", 1));
        slots.Add(CopySlot(TmplCfIn1, 3, "Delta Time", 1));
        slots.Add(CopySlot(TmplCfIn1, 4, "Smooth Delta", 1));

        OById[nid] = SetSlotArray(d, slots);
        OSlots[nid] = slots;
        N(nid);
        SetPosOn(nid, x, y);
        return nid;
    }

    // Vector3Node: Out = id 0 (Vector3MaterialSlot), входы X/Y/Z = id 1/2/3.
    // Код узла — precision3(X, Y, Z) из ЗНАЧЕНИЙ входных слотов, поэтому
    // заполняются и слоты, и собственное поле m_Value: в разных версиях пакета
    // источником истины оказывается то одно, то другое.
    static string MakeVector3Const(float vx, float vy, float vz, float x, float y)
    {
        List<string> none;
        string nid = Copy(TmplMul, false, out none);
        string d = SetStr(OById[nid], "m_Type", "UnityEditor.ShaderGraph.Vector3Node");
        d = SetStr(d, "m_Name", "Vector 3");
        d = SetEmptyArray(d, "synonyms");

        var slots = new List<string>();
        slots.Add(SlotVal(CopySlot(TmplCfIn1, 1, "X", 0), vx));
        slots.Add(SlotVal(CopySlot(TmplCfIn1, 2, "Y", 0), vy));
        slots.Add(SlotVal(CopySlot(TmplCfIn1, 3, "Z", 0), vz));
        slots.Add(CopySlot(TmplCfIn3, 0, "Out", 1));

        d = SetSlotArray(d, slots);
        d = Regex.Replace(d, "\\}\\s*$",
            ",\n    \"m_Value\": {\"x\":" + F(vx) + ",\"y\":" + F(vy) + ",\"z\":" + F(vz) + "}\n}");

        OById[nid] = d;
        OSlots[nid] = slots;
        N(nid);
        SetPosOn(nid, x, y);
        return nid;
    }

    static string SlotVal(string slotId, float v)
    {
        OById[slotId] = SetNumF(OById[slotId], "m_Value", v);
        return slotId;
    }

    static string SetEmptyArray(string doc, string key)
    {
        return Regex.Replace(doc, "(\"" + key + "\"\\s*:\\s*)\\[[^\\]]*\\]", "${1}[]", RegexOptions.Singleline);
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
