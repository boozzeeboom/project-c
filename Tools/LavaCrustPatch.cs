using System;
using System.IO;
using System.Text;
using UnityEngine;

// LavaCrustPatch — одноразовая операция: добавляет в Tools/LavaAnimatedBuilder.cs
// дрейф домена КОРКИ (стадия 5c) и свойство _Crust_Speed. HLSL при этом НЕ
// меняется вообще: сдвиг подаётся в ObjectPos CF-нод, ядро лавы остаётся без
// времени, сигнатуры CF-функций не трогаются.
//
// Корка — ОДИН домен на три ноды (Fissures, PlateTone, Skin), поэтому им нужен
// ОДИН общий сдвиг: разные сдвиги разъехали бы тон плиты с её границами.
//
// Каждый анкер обязан найтись ровно один раз, иначе файл не пишется вообще.
public static class LavaCrustPatch
{
    static int Bad;

    public static string Execute()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string src = root + "/Tools/LavaAnimatedBuilder.cs";
        string bak = root + "/Tools/_backup_LavaAnimatedBuilder.cs.txt";
        var log = new StringBuilder();
        Bad = 0;

        if (!File.Exists(src)) return "ABORT: нет " + src;
        var utf8 = new UTF8Encoding(false);
        string t = File.ReadAllText(src, utf8);
        log.AppendLine("исходник: " + src + " (" + t.Length + " симв., CR=" + Cnt(t, "\r") + ")");

        string cprops = Norm(File.ReadAllText(root + "/Tools/_lavaanim_block_crustprops.txt", utf8));
        string cdrift = Norm(File.ReadAllText(root + "/Tools/_lavaanim_block_crustdrift.txt", utf8));

        // 1. свойство скорости корки — после скоростей жара и потока
        t = R(t, "        Port pFlowSpeed = PropF(\"Flow Speed\", 1.50f);",
                 "        Port pFlowSpeed = PropF(\"Flow Speed\", 1.50f);\n" + cprops, log);

        // 2. заголовок 5b: корка больше не «остаётся на месте»
        t = R(t, "5b. анимация домена: время двигает ТОЛЬКО жар и поток",
                 "5b. анимация домена: жар и поток", log);

        // 3. тело комментария 5b
        t = R(t,
            "        // Корка (Fissures/PlateTone/Skin) остаётся на месте — это твёрдая\n"
          + "        // плита; движется расплав ПОД ней. Поэтому время подаётся в ObjectPos\n"
          + "        // нод Heat и Flow, а не в их сид: сид — скаляр, он увёл бы домен по\n"
          + "        // всем трём осям сразу. HLSL при этом не меняется вообще, ядро лавы\n"
          + "        // остаётся без времени.",
            "        // Время подаётся в ObjectPos нод Heat и Flow, а не в их сид: сид —\n"
          + "        // скаляр, он увёл бы домен по всем трём осям сразу. HLSL при этом не\n"
          + "        // меняется вообще, ядро лавы остаётся без времени. Корка дрейфует\n"
          + "        // ОТДЕЛЬНЫМ, более медленным сдвигом — стадия 5c.", log);

        // 4. ObjectPos корковых CF-нод УБИРАЕТСЯ из стадии 5: его подаёт 5c.
        //    addCrustPos объявляется в 5c, то есть НИЖЕ, поэтому ссылаться на
        //    него здесь нельзя — связи корки целиком живут в блоке 5c.
        t = R(t, "Link(OBJP, P(cfFiss, 0)); Link(OBJP, P(cfPlate, 0));", "", log);
        t = R(t, "Link(OBJP, P(cfSkin, 0));", "", log);
        t = R(t, "// Heat — ниже, через дрейф (5b)",
                 "// корка (Fissures/PlateTone/Skin): ObjectPos подаётся в 5c, общим дрейфом", log);
        t = R(t, "// Flow — ниже, через дрейф (5b)",
                 "// (та же связь корки, см. стадию 5c)", log);

        // 5. стадия 5c
        t = R(t,
            "            + \" heat=\" + S(addHeatPos) + \" flow=\" + S(addFlowPos));",
            "            + \" heat=\" + S(addHeatPos) + \" flow=\" + S(addFlowPos));\n" + cdrift, log);

        // ── контроль ──────────────────────────────────────────────────────────
        Check(t, "class LavaAnimatedBuilder", 1, log);
        Check(t, "Port pCrustSpeed = PropF(\"Crust Speed\", 0.004f);", 1, log);
        Check(t, "Stage = \"5c crust drift\";", 1, log);
        Check(t, "Link(P(addCrustPos, 2), P(cfFiss, 0));", 1, log);
        Check(t, "Link(P(addCrustPos, 2), P(cfPlate, 0));", 1, log);
        Check(t, "Link(P(addCrustPos, 2), P(cfSkin, 0));", 1, log);
        Check(t, "Link(OBJP, P(cfFiss, 0));", 0, log);
        Check(t, "Link(OBJP, P(cfPlate, 0));", 0, log);
        Check(t, "Link(OBJP, P(cfSkin, 0));", 0, log);
        Check(t, "Остаётся на месте", 0, log);
        Check(t, "MakeTimeNode", 2, log);
        Check(t, "MakeVector3Const", 2, log);
        Check(t, "_lavaanim", 0, log);

        if (Bad > 0)
        {
            log.AppendLine("ОТКАЗ: " + Bad + " ошибок — файл НЕ записан (бэкап тоже не сделан)");
            Debug.Log(log.ToString());
            return log.ToString();
        }

        if (File.Exists(bak)) { log.AppendLine("ОТКАЗ: бэкап уже есть — патч применён ранее: " + bak); Debug.Log(log.ToString()); return log.ToString(); }
        File.WriteAllText(bak, t, utf8);
        File.WriteAllText(src, t.Replace("\r\n", "\n"), utf8);
        log.AppendLine("бэкап: " + bak);
        log.AppendLine("записан: " + src + " (" + t.Length + " симв.)  ноды +3, рёбра net +6, свойства +1");
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
