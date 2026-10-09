using System;
using System.IO;
using System.Text;
using UnityEngine;

// LavaAnimatedPatch — одноразовая операция: делает Tools/LavaAnimatedBuilder.cs
// копией принятого Tools/LavaBuilder.cs с аддитивными правками (анимация домена
// жара и потока по локальной Y). Сам граф Lava, его HLSL и пресет НЕ трогаются.
// Каждый анкер обязан найтись ровно один раз, иначе файл не пишется вообще.
public static class LavaAnimatedPatch
{
    static int Bad;

    public static string Execute()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string src = root + "/Tools/LavaBuilder.cs";
        string dst = root + "/Tools/LavaAnimatedBuilder.cs";
        var log = new StringBuilder();
        Bad = 0;

        if (!File.Exists(src)) return "ABORT: нет исходника " + src;
        string t = File.ReadAllText(src);
        log.AppendLine("исходник: " + src + " (" + t.Length + " симв.)");

        string props  = Norm(File.ReadAllText(root + "/Tools/_lavaanim_block_props.txt"));
        string drift  = Norm(File.ReadAllText(root + "/Tools/_lavaanim_block_drift.txt"));
        string helper = Norm(File.ReadAllText(root + "/Tools/_lavaanim_block_helpers.txt"));

        // 1. заголовок и имя класса
        t = R(t, "// LavaBuilder — собирает Assets/_Project/Materials/Lava/Lava.shadergraph",
                 "// LavaAnimatedBuilder — собирает Assets/_Project/Materials/Lava_animated/Lava_animated.shadergraph", log);
        t = R(t, "public static class LavaBuilder",
                 "public static class LavaAnimatedBuilder", log);
        t = R(t, "\"LavaBuilder FAILED at stage [\"",
                 "\"LavaAnimatedBuilder FAILED at stage [\"", log);

        // 2. константы вывода: своя папка, свой файл графа, своё имя шейдера
        t = R(t, "const string OutFolder = \"Assets/_Project/Materials/Lava\";",
                 "const string OutFolder = \"Assets/_Project/Materials/Lava_animated\";", log);
        t = R(t, "const string OutGraph  = OutFolder + \"/Lava.shadergraph\";",
                 "const string OutGraph  = OutFolder + \"/Lava_animated.shadergraph\";", log);
        t = R(t, "const string ShaderName = \"Lava\";",
                 "const string ShaderName = \"Lava_animated\";", log);

        // 3. два новых свойства скорости — после цветов (y существующих PropertyNode не сдвигаются)
        t = R(t, "        Port cCore   = PropC(\"Core Color\", 1.000f, 0.940f, 0.620f);",
                 "        Port cCore   = PropC(\"Core Color\", 1.000f, 0.940f, 0.620f);" + "\n" + props, log);

        // 4. Heat и Flow получают НЕ прямой ObjectPos
        t = R(t, "        Link(OBJP, P(cfFiss, 0)); Link(OBJP, P(cfPlate, 0)); Link(OBJP, P(cfHeat, 0));",
                 "        Link(OBJP, P(cfFiss, 0)); Link(OBJP, P(cfPlate, 0));   // Heat — ниже, через дрейф (5b)", log);
        t = R(t, "        Link(OBJP, P(cfFlow, 0)); Link(OBJP, P(cfSkin, 0));",
                 "        Link(OBJP, P(cfSkin, 0));                          // Flow — ниже, через дрейф (5b)", log);

        // 5. стадия 5b: цепочки дрейфа домена
        t = R(t, "        Link(pSkinScale, P(cfSkin, 1)); Link(pSkinDetail, P(cfSkin, 2)); Link(pSkinSeed, P(cfSkin, 3));",
                 "        Link(pSkinScale, P(cfSkin, 1)); Link(pSkinDetail, P(cfSkin, 2)); Link(pSkinSeed, P(cfSkin, 3));" + "\n" + drift, log);

        // 6. хелперы новых типов нод
        t = R(t, "    static void N(string id) { ONodes.Add(id); }",
                 helper + "\n\n" + "    static void N(string id) { ONodes.Add(id); }", log);

        // 7. папка и имя дампа
        t = R(t, "AssetDatabase.CreateFolder(\"Assets/_Project/Materials\", \"Lava\");",
                 "AssetDatabase.CreateFolder(\"Assets/_Project/Materials\", \"Lava_animated\");", log);
        t = R(t, "string dump = OutFolder + \"/dump_lava.shadergraph\";",
                 "string dump = OutFolder + \"/dump_lava_animated.shadergraph\";", log);

        // ── контроль ──────────────────────────────────────────────────────────
        Check(t, "class LavaBuilder", 0, log);
        Check(t, "Lava.shadergraph", 0, log);
        Check(t, "OutFolder + \"/Lava_animated.shadergraph\";", 1, log);
        Check(t, "string ShaderName = \"Lava_animated\";", 1, log);
        Check(t, "MakeTimeNode", 2, log);
        Check(t, "MakeVector3Const", 2, log);
        Check(t, "SetEmptyArray", 3, log);
        Check(t, "SlotVal(", 4, log);
        Check(t, "Stage = \"5b drift\";", 1, log);
        Check(t, "_lavaanim", 0, log);

        if (Bad > 0)
        {
            log.AppendLine("ОТКАЗ: " + Bad + " ошибок — файл НЕ записан");
            Debug.Log(log.ToString());
            return log.ToString();
        }

        File.WriteAllText(dst, t.Replace("\r\n", "\n"), new UTF8Encoding(false));
        log.AppendLine("записан: " + dst + " (" + t.Length + " симв.)");
        log.AppendLine("свойства +2 (Heat Speed / Flow Speed), ноды +10, рёбра +12");
        Debug.Log(log.ToString());
        return log.ToString();
    }

    static string Norm(string s) { return s.Replace("\r\n", "\n").TrimEnd('\n'); }

    static string R(string t, string oldS, string newS, StringBuilder log)
    {
        int c = Cnt(t, oldS);
        if (c != 1)
        {
            log.AppendLine("ОШИБКА: анкер встречается " + c + " раз, ожидалось 1: " + Head(oldS));
            Bad++;
            return t;
        }
        log.AppendLine("ok: " + Head(oldS));
        return t.Replace(oldS, newS);
    }

    static void Check(string t, string s, int want, StringBuilder log)
    {
        int c = Cnt(t, s);
        if (c != want) { log.AppendLine("ОШИБКА контроля: \"" + s + "\" встречается " + c + " раз, ожидалось " + want); Bad++; }
        else log.AppendLine("контроль ok: \"" + s + "\" = " + c);
    }

    static int Cnt(string t, string s)
    {
        int c = 0, i = 0;
        while ((i = t.IndexOf(s, i, StringComparison.Ordinal)) >= 0) { c++; i += s.Length; }
        return c;
    }

    static string Head(string s)
    {
        s = s.Replace("\n", "\\n");
        return s.Length > 64 ? s.Substring(0, 64) + "..." : s;
    }
}
