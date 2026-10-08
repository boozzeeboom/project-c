using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// ParquetProbe — ищет невалидный JSON в собранном графе (аналог MultiJsonInternal),
// печатает контекст и разбирает новые CF-ноды/слоты.
public static class ParquetProbe
{
    static string Text;
    static int Pos;
    static List<string> Errs = new List<string>();

    public static string Execute()
    {
        var log = new StringBuilder();
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        Text = File.ReadAllText(root + "/Assets/_Project/Materials/Parquet/Parquet.shadergraph");
        log.AppendLine("len=" + Text.Length);

        List<string> docs = SplitDocs(Text);
        log.AppendLine("docsByDepth=" + docs.Count);

        int bad = 0;
        for (int i = 0; i < docs.Count; i++)
        {
            string saved = Text;
            int savedPos = Pos;
            Text = docs[i];
            Pos = 0;
            Errs = new List<string>();
            Ws();
            if (Pos < Text.Length && Text[Pos] == '{') { Pos++; Object("doc" + i); Ws(); if (Pos < Text.Length) Errs.Add("TRAILING DATA at " + Pos); }
            else Errs.Add("DOES NOT START WITH { : " + Head(Text));
            Text = saved; Pos = savedPos;
            if (Errs.Count > 0)
            {
                bad++;
                if (bad <= 5)
                {
                    log.AppendLine("--- BAD DOC #" + i + " len=" + docs[i].Length);
                    foreach (string e in Errs) log.AppendLine("    " + e);
                    Match oid = Regex.Match(docs[i], "\"m_ObjectId\":\\s*\"([0-9a-f]{32})\"");
                    Match typ = Regex.Match(docs[i], "\"m_Type\":\\s*\"([^\"]+)\"");
                    log.AppendLine("    type=" + (typ.Success ? typ.Groups[1].Value : "?") + " objId=" + (oid.Success ? oid.Groups[1].Value : "?"));
                }
            }
        }
        log.AppendLine("BAD DOCS total=" + bad);

        // ── как выглядят новые CF-ноды и их слоты ────────────────────────────
        foreach (string fn in new string[] { "ProceduralParquetLayout", "ProceduralParquetJoints", "ProceduralParquetTone" })
        {
            int at = Text.IndexOf("\"m_FunctionName\": \"" + fn + "\"");
            if (at < 0) { log.AppendLine("CF node " + fn + " NOT FOUND"); continue; }
            int docStart = Text.LastIndexOf("\n{", at);
            log.AppendLine("=== " + fn + " (doc ~offset " + docStart + ")");
            List<string> sl = new List<string>();
            Match ms = Regex.Match(Text.Substring(at, 900), "\"m_Slots\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            if (ms.Success)
                foreach (Match x in Regex.Matches(ms.Groups[1].Value, "\"m_Id\":\\s*\"([0-9a-f]{32})\"")) sl.Add(x.Groups[1].Value);
            log.AppendLine("   m_Slots raw = " + (ms.Success ? ms.Value.Replace("\n", " ").Replace("\r", "") : "NOT FOUND"));
            int shown = 0;
            foreach (string sid in sl)
            {
                int so = Text.IndexOf("\"m_ObjectId\": \"" + sid + "\"");
                if (so < 0) { log.AppendLine("      slot " + sid.Substring(0, 6) + " DOC MISSING"); continue; }
                string seg = Text.Substring(so, 700);
                int e = seg.IndexOf("\n}");
                if (e > 0) seg = seg.Substring(0, e + 2);
                log.AppendLine("      slot " + sid.Substring(0, 6) + " -> " + seg.Replace("\n", " | ").Replace("\r", ""));
                if (++shown >= 2) break;
            }
        }

        // ── один новый slot-док целиком ──────────────────────────────────────
        int pl = Text.IndexOf("\"PlankLocal\"");
        if (pl < 0) log.AppendLine("PlankLocal NOT FOUND");
        else
        {
            int ds = Text.LastIndexOf("\n\n{", pl);
            int de = Text.IndexOf("\n}", pl);
            log.AppendLine("=== FULL SLOT DOC (PlankLocal) ===");
            log.AppendLine(ds >= 0 && de > ds ? Text.Substring(ds + 1, de - ds + 1) : "context: " + CtxAt(pl, 300));
        }

        // ── GraphData: массивы ───────────────────────────────────────────────
        Match gd = Regex.Match(Text, "\"m_Type\":\\s*\"UnityEditor.ShaderGraph.GraphData\"");
        if (gd.Success)
        {
            string g = Text.Substring(gd.Index, 400);
            log.AppendLine("=== GraphData head ===");
            log.AppendLine(g.Replace("\n", " | ").Replace("\r", ""));
        }
        log.AppendLine("m_Edges occurrences=" + Regex.Matches(Text, "\"m_Edges\"").Count + " m_OutputSlot=" + Regex.Matches(Text, "\"m_OutputSlot\"").Count);

        Debug.Log(log.ToString());
        return log.ToString();
    }

    static string Head(string s) { return s.Length > 100 ? s.Substring(0, 100) : s; }
    static string CtxAt(int idx, int span)
    {
        int a = Math.Max(0, idx - span / 2);
        int b = Math.Min(Text.Length, idx + span / 2);
        int ds = Text.LastIndexOf("\n\n{", idx);
        int de = Text.IndexOf("\n}", idx);
        if (ds >= 0 && de > ds && de - ds < span) return Text.Substring(ds + 1, de - ds + 1);
        return Text.Substring(a, b - a);
    }

    static List<string> SplitDocs(string t)
    {
        var res = new List<string>();
        int start = 0, depth = 0;
        bool inStr = false;
        for (int i = 0; i < t.Length; i++)
        {
            char ch = t[i];
            if (inStr)
            {
                if (ch == '\\') { i++; continue; }
                if (ch == '"') inStr = false;
                continue;
            }
            if (ch == '"') { inStr = true; continue; }
            if (ch == '{' || ch == '[') depth++;
            else if (ch == '}' || ch == ']')
            {
                depth--;
                if (depth == 0)
                {
                    int j = i + 1;
                    while (j < t.Length && char.IsWhiteSpace(t[j])) j++;
                    if (j < t.Length && t[j] == '{')
                    {
                        res.Add(t.Substring(start, i - start + 1));
                        start = j;
                        i = j - 1;
                    }
                }
            }
        }
        if (start < t.Length) res.Add(t.Substring(start));
        return res;
    }

    static void Ws() { while (Pos < Text.Length && char.IsWhiteSpace(Text[Pos])) Pos++; }

    static void Value(string path)
    {
        Ws();
        if (Pos >= Text.Length) { Errs.Add("EOF at " + path); return; }
        char c = Text[Pos];
        if (c == '{') { Pos++; Object(path); }
        else if (c == '[') { Pos++; Arr(path); }
        else if (c == '"') Str(path);
        else
        {
            int st = Pos;
            while (Pos < Text.Length && ",}] \t\r\n".IndexOf(Text[Pos]) < 0) Pos++;
            if (Pos == st) Errs.Add("EMPTY VALUE at " + path + " off " + Pos + " near " + Ctx());
        }
    }

    static void Object(string path)
    {
        Ws();
        if (Pos < Text.Length && Text[Pos] == '}') { Pos++; return; }
        while (true)
        {
            Ws();
            if (Pos >= Text.Length) { Errs.Add("EOF in object " + path + " off " + Pos); return; }
            if (Text[Pos] != '"')
            {
                Errs.Add("MISSING NAME in " + path + " off " + Pos + " near " + Ctx());
                return;
            }
            Str(path);
            Ws();
            if (Pos >= Text.Length || Text[Pos] != ':') { Errs.Add("MISSING COLON in " + path + " off " + Pos + " near " + Ctx()); return; }
            Pos++;
            Value(path);
            Ws();
            if (Pos < Text.Length && Text[Pos] == ',') { Pos++; continue; }
            if (Pos < Text.Length && Text[Pos] == '}') { Pos++; return; }
            Errs.Add("EXPECTED , or } in " + path + " off " + Pos + " near " + Ctx());
            return;
        }
    }

    static void Arr(string path)
    {
        Ws();
        if (Pos < Text.Length && Text[Pos] == ']') { Pos++; return; }
        while (true)
        {
            Value(path + "[]");
            Ws();
            if (Pos < Text.Length && Text[Pos] == ',') { Pos++; continue; }
            if (Pos < Text.Length && Text[Pos] == ']') { Pos++; return; }
            Errs.Add("EXPECTED , or ] in " + path + " off " + Pos + " near " + Ctx());
            return;
        }
    }

    static void Str(string path)
    {
        Pos++;
        while (Pos < Text.Length)
        {
            if (Text[Pos] == '\\') { Pos += 2; continue; }
            if (Text[Pos] == '"') { Pos++; return; }
            Pos++;
        }
        Errs.Add("UNTERMINATED STRING at " + path);
    }

    static string Ctx()
    {
        int a = Math.Max(0, Pos - 80);
        int b = Math.Min(Text.Length, Pos + 80);
        return Text.Substring(a, b - a).Replace("\n", "\\n").Replace("\r", "");
    }
}
