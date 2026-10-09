using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// LanternVerify — приёмка материала Lantern. Только чтение.
//
// Проверяет:
//  1. структуру JSON (документы, m_Nodes / m_Properties, рёбра, слоты, LF-only);
//  2. порядок слотов CF-нод против сигнатур HLSL и то, что КАЖДЫЙ вход CF-ноды
//     подключён (в Tiles неподключённые входы #0…#5 убили слой арки);
//  3. дрейф домена: Glow/Core поднимаются (по одному ребру из Glow/Core Domain
//     Rise в ObjectPos CF), Soot/Pane получают ObjectPos напрямую;
//  4. FO-безопасность: m_Space=0, AbsoluteWorld = 0;
//  5. обязательные три пункта рельефа (слоты NFH In/Strength/Out = 0/2/1,
//     второй аргумент вызова NFH — свойство, float(0.01) = 0);
//  6. эмиссию: подключена и не константа;
//  7. шейдер и пресет; донор Brick не тронут.
public static class LanternVerify
{
    const string Graph = "Assets/_Project/Materials/Lantern/Lantern.shadergraph";
    const string Mat = "Assets/_Project/Materials/Lantern/M_PC_Lantern.mat";
    const string ShaderName = "Lantern";
    const string Hlsl = "Assets/_Project/Shaders/ProceduralLanternNoise.hlsl";
    const string BrickHlsl = "Assets/_Project/Shaders/ProceduralBrickNoise.hlsl";

    // fn, слоты по порядку id: входа 0..n-1, последний — выход
    static readonly string[][] CfSpec = new string[][] {
        new[] { "ProceduralLanternGlow", "ObjectPos", "GlowScale", "GlowStretch", "GlowSeed", "Glow" },
        new[] { "ProceduralLanternCore", "ObjectPos", "CoreScale", "CoreStretch", "CoreSeed", "Core" },
        new[] { "ProceduralLanternSoot", "ObjectPos", "SootScale", "SootDetail", "SootSeed", "SootTilt", "SootAmount", "Soot" },
        new[] { "ProceduralLanternPane", "ObjectPos", "PaneScale", "PaneStretch", "PaneWidth", "PaneSeed", "Pane" }
    };

    static readonly string[] Props = new string[] {
        "_Glow_Scale", "_Glow_Stretch", "_Glow_Seed", "_Glow_Threshold", "_Glow_Softness", "_Glow_Bump",
        "_Core_Scale", "_Core_Stretch", "_Core_Seed", "_Core_Threshold", "_Core_Softness", "_Core_Glow",
        "_Soot_Scale", "_Soot_Detail", "_Soot_Seed", "_Soot_Tilt", "_Soot_Amount", "_Soot_Roughness", "_Soot_Bump",
        "_Pane_Scale", "_Pane_Stretch", "_Pane_Width", "_Pane_Seed", "_Pane_Amount", "_Pane_Bump",
        "_Glow_Amount", "_Emission_Strength", "_Flicker_Base", "_Flicker_Amount", "_Glass_Smoothness", "_Bump_Strength",
        "_Glow_Speed", "_Core_Speed",
        "_Glass_Color", "_Soot_Color", "_Ember_Color", "_Glow_Color", "_Core_Color"
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
        string hlslGuid = AssetDatabase.AssetPathToGUID(Hlsl);
        string brickGuid = AssetDatabase.AssetPathToGUID(BrickHlsl);
        string raw = System.IO.File.ReadAllText(path);
        int crCount = Regex.Matches(raw, "\r").Count;
        string text = raw.Replace("\r", "");

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
        var nameOf = new Dictionary<string, string>();
        var idByName = new Dictionary<string, string>();
        foreach (string d in docs)
        {
            Match mo = Regex.Match(d, "\"m_ObjectId\"\\s*:\\s*\"([0-9a-f]{32})\"");
            if (!mo.Success) continue;
            string id = mo.Groups[1].Value;
            byId[id] = d;
            Match mt = Regex.Match(d, "\"m_Type\"\\s*:\\s*\"([^\"]+)\"");
            string t = mt.Success ? mt.Groups[1].Value : "?";
            typeOf[id] = t.Substring(t.LastIndexOf('.') + 1);
            Match mn = Regex.Match(d, "\"m_Name\"\\s*:\\s*\"([^\"]*)\"");
            if (mn.Success) { nameOf[id] = mn.Groups[1].Value; idByName[mn.Groups[1].Value] = id; }
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
            + " blankLines=" + blankInside.ToString(CultureInfo.InvariantCulture)
            + " CR=" + crCount.ToString(CultureInfo.InvariantCulture) + (crCount == 0 ? " (LF-only OK)" : " (!! CRLF)"));

        Match nm = Regex.Match(gdDoc, "\"m_Nodes\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        var listedNodes = new List<string>();
        foreach (Match x in Regex.Matches(nm.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) listedNodes.Add(x.Groups[1].Value);
        Match pm = Regex.Match(gdDoc, "\"m_Properties\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        var listedProps = new List<string>();
        foreach (Match x in Regex.Matches(pm.Groups[1].Value, "\"m_Id\"\\s*:\\s*\"([0-9a-f]{32})\"")) listedProps.Add(x.Groups[1].Value);
        Match em = Regex.Match(gdDoc, "\"m_Edges\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        int edgesListed = Regex.Matches(em.Groups[1].Value, "\"m_OutputSlot\"").Count;

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
        var missingRef = new List<string>();
        foreach (string p in Props) if (!refs.ContainsKey(p)) missingRef.Add(p);
        log.AppendLine("   m_Nodes=" + listedNodes.Count.ToString(CultureInfo.InvariantCulture)
            + " m_Properties=" + listedProps.Count.ToString(CultureInfo.InvariantCulture)
            + " m_Edges=" + edgesListed.ToString(CultureInfo.InvariantCulture)
            + " (ожидалось свойств " + Props.Length.ToString(CultureInfo.InvariantCulture) + ")");
        log.AppendLine("   ref-имён=" + refs.Count.ToString(CultureInfo.InvariantCulture)
            + " дублей=" + (dupRefs.Count == 0 ? "нет" : string.Join(",", dupRefs.ToArray()))
            + "  не найдено ожидаемых=" + (missingRef.Count == 0 ? "нет" : string.Join(", ", missingRef.ToArray())));

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

        // ── CF-ноды: сигнатуры, порядок слотов, полнота входов ───────────────
        log.AppendLine("== CF NODES ==");
        var cfByFn = new Dictionary<string, string>();
        foreach (var kv in typeOf)
        {
            if (kv.Value != "CustomFunctionNode") continue;
            Match fn = Regex.Match(byId[kv.Key], "\"m_FunctionName\"\\s*:\\s*\"([^\"]+)\"");
            if (fn.Success) cfByFn[fn.Groups[1].Value] = kv.Key;
        }
        int cfBadSrc = 0, cfUnconnected = 0;
        foreach (string[] spec in CfSpec)
        {
            string fn = spec[0];
            if (!cfByFn.ContainsKey(fn)) { log.AppendLine("   !! нет CF-ноды " + fn); continue; }
            string nid = cfByFn[fn];
            Match src = Regex.Match(byId[nid], "\"m_FunctionSource\"\\s*:\\s*\"([0-9a-f]*)\"");
            string sg = src.Success ? src.Groups[1].Value : "?";
            if (sg != hlslGuid) cfBadSrc++;
            var keys = new List<string>(slotNum[nid].Keys);
            keys.Sort(delegate (string a, string b) { return int.Parse(a).CompareTo(int.Parse(b)); });
            var actual = new List<string>();
            foreach (string k in keys) actual.Add(slotName[slotNum[nid][k]] + (slotIo[slotNum[nid][k]] == "1" ? "(out)" : ""));
            var want = new List<string>();
            for (int i = 1; i < spec.Length; i++)
                want.Add(spec[i] + (i == spec.Length - 1 ? "(out)" : "") + "#" + (i - 1));
            bool ok = actual.Count == want.Count;
            if (ok) for (int i = 0; i < want.Count; i++) if (actual[i] != want[i].Substring(0, want[i].LastIndexOf('#'))) ok = false;
            log.AppendLine("   " + fn + " id=" + S(nid) + " src=" + (sg.Length >= 8 ? sg.Substring(0, 8) : sg)
                + (sg == hlslGuid ? "(lantern)" : "(ЧУЖОЙ)") + " slots=" + string.Join(",", actual.ToArray())
                + (ok ? "  OK (порядок = порядок параметров HLSL)" : "  !! ОЖИДАЛОСЬ " + string.Join(",", want.ToArray())));
            // каждый ВХОД (слот 0..spec.Length-3) обязан быть подключён: последний
            // элемент spec — выход, поэтому входов ровно spec.Length-2
            for (int i = 0; i < spec.Length - 2; i++)
            {
                bool fed = false;
                foreach (string[] e in edges) if (e[2] == nid && e[3] == i.ToString(CultureInfo.InvariantCulture)) { fed = true; break; }
                if (!fed) { cfUnconnected++; log.AppendLine("      !! вход #" + i + " (" + spec[i + 1] + ") НЕ подключён"); }
            }
        }
        log.AppendLine("   CF всего=" + cfByFn.Count.ToString(CultureInfo.InvariantCulture)
            + " с чужим FunctionSource=" + cfBadSrc.ToString(CultureInfo.InvariantCulture)
            + " неподключённых входов=" + cfUnconnected.ToString(CultureInfo.InvariantCulture));
        log.AppendLine("   ProceduralLantern вхождений=" + Regex.Matches(text, "ProceduralLantern").Count.ToString(CultureInfo.InvariantCulture)
            + "  brick HLSL guid вхождений=" + Regex.Matches(text, brickGuid).Count.ToString(CultureInfo.InvariantCulture)
            + "  Brick вхождений=" + Regex.Matches(text, "Brick").Count.ToString(CultureInfo.InvariantCulture));

        // ── дрейф домена ─────────────────────────────────────────────────────
        log.AppendLine("== DOMAIN RISE (Glow/Core поднимаются, Soot/Pane стоят) ==");
        CheckRise(log, "Glow Domain Rise", cfByFn.ContainsKey("ProceduralLanternGlow") ? cfByFn["ProceduralLanternGlow"] : null, idByName, edges);
        CheckRise(log, "Core Domain Rise", cfByFn.ContainsKey("ProceduralLanternCore") ? cfByFn["ProceduralLanternCore"] : null, idByName, edges);
        // Soot/Pane: ObjectPos прямо из OBJP, ноль лишних рёбер
        string objId = idByName.ContainsKey("Object Position") ? idByName["Object Position"] : null;
        foreach (string fn in new[] { "ProceduralLanternSoot", "ProceduralLanternPane" })
        {
            if (!cfByFn.ContainsKey(fn)) continue;
            string nid = cfByFn[fn];
            string src = "(нет)";
            foreach (string[] e in edges) if (e[2] == nid && e[3] == "0") src = S(e[0]);
            log.AppendLine("   " + fn + ".ObjectPos <= " + src
                + (objId != null && src == S(objId) ? " (Object Position — прямо, без дрейфа, OK)" : "  !! ОЖИДАЛСЯ Object Position"));
        }
        // общая база дрейфа: Time и вектор направления
        int riseDirs = 0;
        foreach (var kv in nameOf) if (kv.Value == "Vector 3" || kv.Value == "Time") riseDirs++;
        log.AppendLine("   нод Time/Vector 3 найдено=" + riseDirs.ToString(CultureInfo.InvariantCulture));
        if (objId != null)
        {
            foreach (string nmn in new[] { "Glow Rise Y", "Core Rise Y" })
            {
                string id = idByName.ContainsKey(nmn) ? idByName[nmn] : null;
                if (id == null) { log.AppendLine("   !! нет ноды " + nmn); continue; }
                string a = null, b = null;
                foreach (string[] e in edges) if (e[2] == id) { a = typeOf[e[0]]; b = slotName[slotNum[e[0]][e[1]]]; }
                log.AppendLine("   " + nmn + " A=" + a + " B=" + b + (b == "Out" ? " (Vector 3 Out — OK)" : "  !! ожидался выход Vector 3"));
            }
        }

        // ── FO ───────────────────────────────────────────────────────────────
        log.AppendLine("== POSITION / FO ==");
        foreach (var kv in typeOf)
        {
            if (kv.Value != "PositionNode" && kv.Value != "NormalVectorNode") continue;
            Match sp = Regex.Match(byId[kv.Key], "\"m_Space\"\\s*:\\s*(-?\\d+)");
            log.AppendLine("   " + kv.Value + " name=\"" + (nameOf.ContainsKey(kv.Key) ? nameOf[kv.Key] : "?")
                + "\" m_Space=" + (sp.Success ? sp.Groups[1].Value : "n/a"));
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
            for (int i = 0; i <= 2; i++)
            {
                string k = i.ToString(CultureInfo.InvariantCulture);
                string sid = slotNum[nfhId].ContainsKey(k) ? slotNum[nfhId][k] : null;
                log.AppendLine("   slot #" + k + " -> " + (sid == null ? "(нет)" : slotName[sid] + " io=" + slotIo[sid]));
            }
            foreach (string k in new string[] { "0", "2" })
            {
                string src = "(не подключён)";
                foreach (string[] e in edges) if (e[2] == nfhId && e[3] == k)
                    src = (typeOf.ContainsKey(e[0]) ? typeOf[e[0]] : "?") + "(" + S(e[0]) + ")#" + e[1]
                        + (slotNum.ContainsKey(e[0]) && slotNum[e[0]].ContainsKey(e[1]) ? " " + slotName[slotNum[e[0]][e[1]]] : "");
                log.AppendLine("   NFH." + (k == "0" ? "In" : "Strength") + " <= " + src);
            }
            foreach (string[] e in edges) if (e[0] == nfhId)
                log.AppendLine("   NFH out #" + e[1] + " => " + Descriptor(byId, e[2]));
        }

        // ── блоки ────────────────────────────────────────────────────────────
        log.AppendLine("== BLOCKS ==");
        foreach (var kv in typeOf)
        {
            if (kv.Value != "BlockNode") continue;
            string desc = Descriptor(byId, kv.Key);
            if (desc == "-") continue;
            string feeder = "(не подключён)";
            foreach (string[] e in edges) if (e[2] == kv.Key && e[3] == "0")
                feeder = (typeOf.ContainsKey(e[0]) ? typeOf[e[0]] : "?") + "(" + S(e[0]) + ")#" + e[1];
            log.AppendLine("   " + desc + " <= " + feeder);
        }

        // ── сгенерированный текст ────────────────────────────────────────────
        string gen = GetGenerated();
        log.AppendLine("== GENERATED TEXT len=" + (gen == null ? -1 : gen.Length).ToString(CultureInfo.InvariantCulture) + " ==");
        if (gen != null)
        {
            log.AppendLine("== SURFACE ==");
            foreach (Match m in Regex.Matches(gen, "surface\\.(BaseColor|Smoothness|NormalTS|Metallic|Emission|Occlusion|Alpha)\\s*=\\s*[^;]+;"))
                log.AppendLine("   " + Cut(m.Value, 300));

            log.AppendLine("== 1) EMISSION (несущий слой этого материала) ==");
            MatchCollection emc = Regex.Matches(gen, "surface\\.Emission\\s*=\\s*([^;]+);");
            log.AppendLine("   присвоений surface.Emission=" + emc.Count.ToString(CultureInfo.InvariantCulture));
            foreach (Match m in emc)
            {
                string v = m.Groups[1].Value.Trim();
                bool constant = v.StartsWith("float3(0") || v.StartsWith("float3( 0") || v == "0" || v.StartsWith("(0");
                log.AppendLine("   value=" + Cut(v, 300) + "  константа-ноль=" + constant);
            }
            log.AppendLine("   _Emission_Strength count=" + Regex.Matches(gen, "_Emission_Strength").Count.ToString(CultureInfo.InvariantCulture)
                + "  _Glow_Amount count=" + Regex.Matches(gen, "_Glow_Amount").Count.ToString(CultureInfo.InvariantCulture)
                + "  _Flicker_Base count=" + Regex.Matches(gen, "_Flicker_Base").Count.ToString(CultureInfo.InvariantCulture)
                + "  TimeParameters count=" + Regex.Matches(gen, "TimeParameters").Count.ToString(CultureInfo.InvariantCulture));

            log.AppendLine("== 2) NFH CALL ==");
            MatchCollection nfh = Regex.Matches(gen, "Unity_NormalFromHeight_[A-Za-z]+_float\\([^;]*;");
            log.AppendLine("   calls=" + nfh.Count.ToString(CultureInfo.InvariantCulture));
            foreach (Match m in nfh) log.AppendLine("   " + Cut(m.Value, 600));
            log.AppendLine("   float(0.01) count=" + Regex.Matches(gen, "float\\(0\\.01\\)").Count.ToString(CultureInfo.InvariantCulture)
                + "  0.009999999776482582 count=" + Regex.Matches(gen, "0\\.009999999776482582").Count.ToString(CultureInfo.InvariantCulture));

            log.AppendLine("== 3) CF CALL SITES ==");
            var uniq = new List<string>();
            foreach (Match m in Regex.Matches(gen, "ProceduralLantern[A-Za-z]*_(float|half)\\([^;]*;"))
            {
                string u = Cut(m.Value, 600);
                if (!uniq.Contains(u)) uniq.Add(u);
            }
            log.AppendLine("   unique sites=" + uniq.Count.ToString(CultureInfo.InvariantCulture));
            foreach (string u in uniq) log.AppendLine("      " + u);

            log.AppendLine("== 4) PROPERTY USAGE ==");
            int unused = 0;
            foreach (string p in Props)
            {
                int c = Regex.Matches(gen, Regex.Escape(p) + "[^A-Za-z0-9_]").Count;
                if (c == 0) { unused++; log.AppendLine("   !! не используется: " + p); }
            }
            log.AppendLine("   неиспользованных свойств=" + unused.ToString(CultureInfo.InvariantCulture)
                + " (проверено " + Props.Length.ToString(CultureInfo.InvariantCulture) + ")");
            log.AppendLine("   IN.ObjectSpacePosition=" + Regex.Matches(gen, "IN\\.ObjectSpacePosition").Count.ToString(CultureInfo.InvariantCulture)
                + " IN.WorldSpacePosition=" + Regex.Matches(gen, "IN\\.WorldSpacePosition").Count.ToString(CultureInfo.InvariantCulture)
                + " LanternNoise.hlsl=" + Regex.Matches(gen, "ProceduralLanternNoise").Count.ToString(CultureInfo.InvariantCulture));
        }
        else log.AppendLine("   !! сгенерированный текст не получен (ShaderGraphImporter.GetShaderText)");

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

        // ── донор и соседи не тронуты ────────────────────────────────────────
        log.AppendLine("== DONOR / NEIGHBOURS UNTOUCHED ==");
        Count(log, root, "Assets/_Project/Materials/Brick/Brick.shadergraph");
        Count(log, root, "Assets/_Project/Materials/Lava/Lava.shadergraph");
        Count(log, root, "Assets/_Project/Materials/Lava_animated/Lava_animated.shadergraph");

        log.AppendLine("== EDITOR LOG ==");
        log.AppendLine("   " + ScanEditorLog(root));

        Debug.Log(log.ToString());
        return log.ToString();
    }

    static void CheckRise(StringBuilder log, string nodeName, string cfId, Dictionary<string, string> idByName,
        List<string[]> edges)
    {
        if (!idByName.ContainsKey(nodeName)) { log.AppendLine("   !! нет ноды " + nodeName); return; }
        string id = idByName[nodeName];
        var outs = new List<string>();
        foreach (string[] e in edges) if (e[0] == id) outs.Add(S(e[2]) + "#" + e[3]);
        log.AppendLine("   " + nodeName + " исходящих=" + outs.Count.ToString(CultureInfo.InvariantCulture)
            + " -> " + string.Join(",", outs.ToArray())
            + (cfId != null && outs.Count == 1 && outs[0] == S(cfId) + "#0" ? "  OK (ровно в ObjectPos CF)" : "  !! ОЖИДАЛОСЬ одно ребро в ObjectPos CF"));
    }

    static void Count(StringBuilder log, string root, string rel)
    {
        string p = root + "/" + rel;
        if (!System.IO.File.Exists(p)) { log.AppendLine("   " + rel + " отсутствует"); return; }
        string t = System.IO.File.ReadAllText(p).Replace("\r", "");
        log.AppendLine("   " + rel + ": objectIds=" + Regex.Matches(t, "\"m_ObjectId\"").Count.ToString(CultureInfo.InvariantCulture)
            + " edges=" + Regex.Matches(t, "\"m_OutputSlot\"").Count.ToString(CultureInfo.InvariantCulture)
            + " ProceduralLantern=" + Regex.Matches(t, "ProceduralLantern").Count.ToString(CultureInfo.InvariantCulture));
    }

    static string ScanEditorLog(string root)
    {
        string p = root + "/Logs/Editor.log";
        if (!System.IO.File.Exists(p)) return "Logs/Editor.log не найден";
        using (var fs = new System.IO.FileStream(p, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
        {
            long start = Math.Max(0, fs.Length - 600000);
            fs.Seek(start, System.IO.SeekOrigin.Begin);
            var buf = new byte[fs.Length - start];
            int read = fs.Read(buf, 0, buf.Length);
            string lt = new UTF8Encoding(false).GetString(buf, 0, read);
            MatchCollection mc = Regex.Matches(lt, "Shader error in 'Lantern':[^\\n]*");
            var sb = new StringBuilder("\"Shader error in 'Lantern'\" = " + mc.Count.ToString(CultureInfo.InvariantCulture));
            int shown = 0;
            foreach (Match m in mc) { if (shown++ >= 4) break; sb.Append("\n      " + m.Value.Trim()); }
            return sb.ToString();
        }
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
}
