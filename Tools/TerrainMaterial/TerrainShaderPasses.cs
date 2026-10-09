using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Разбор пассов шейдера Terrain + негативный контроль по логу.
//
// 1) какие пассы есть в каждом сабшейдере (LightMode) и что среди них есть ForwardLit;
// 2) принудительная компиляция именно ForwardLit (там и падало);
// 3) сколько всего строк "Shader error in 'Terrain'" в Logs/Editor.log и насколько
//    последняя из них старая относительно конца файла — это доказывает, что
//    найденная ранее ошибка историческая, а не появившаяся от нашего переимпорта.
public static class TerrainShaderPasses
{
    const string GraphPath = "Assets/_Project/Materials/Terrain/Terrain.shadergraph";
    const string MatPath = "Assets/_Project/Materials/Terrain/M_PC_Terrain.mat";
    const string LogPath = "Logs/Editor.log";

    public static string Execute()
    {
        var sb = new StringBuilder();

        var shader = Shader.Find("Terrain");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (shader == null || mat == null) return "FAIL: shader or material missing";

        long logLen = 0;
        try { logLen = new FileInfo(LogPath).Length; } catch { }

        int forwardLitPass = -1;
        for (int sub = 0; sub < shader.subshaderCount; sub++)
        {
            int pc = shader.GetPassCountInSubshader(sub);
            sb.AppendLine("subshader[" + sub + "] passes=" + pc);
            for (int p = 0; p < pc; p++)
            {
                string lm;
                try { lm = shader.FindPassTagValue(p, new ShaderTagId("LightMode")).name; }
                catch (Exception e) { lm = "<err:" + e.GetType().Name + ">"; }
                sb.AppendLine("  sub" + sub + " pass[" + p + "] LightMode=" + lm);
                if (sub == 0 && lm == "UniversalForward" || sub == 0 && lm == "ForwardLit")
                {
                    if (forwardLitPass < 0) forwardLitPass = p;
                }
            }
        }

        sb.AppendLine("forwardLitPassIndex=" + forwardLitPass);

        long before = LogLen();
        ShaderUtil.ClearShaderMessages(shader);
        for (int p = 0; p < shader.GetPassCountInSubshader(0); p++)
            ShaderUtil.CompilePass(mat, p, true);
        long after = LogLen();

        sb.AppendLine("logBytesBefore=" + before + " logBytesAfter=" + after + " delta=" + (after - before));
        sb.AppendLine("ShaderHasError=" + ShaderUtil.ShaderHasError(shader));
        var msgs = ShaderUtil.GetShaderMessages(shader);
        sb.AppendLine("shaderMessages=" + (msgs == null ? -1 : msgs.Length));
        if (msgs != null)
            foreach (var m in msgs) sb.AppendLine("  [" + m.severity + "] " + m.message + " @" + m.file + ":" + m.line);

        // ---- история ошибки в логе ----
        string wholeLog = ReadAll(LogPath);
        int total = 0;
        int lastEndIndex = -1;
        int scanFrom = 0;
        while (true)
        {
            int idx = wholeLog.IndexOf("Shader error in 'Terrain'", scanFrom, StringComparison.Ordinal);
            if (idx < 0) break;
            total++;
            int lineEnd = wholeLog.IndexOf('\n', idx);
            lastEndIndex = lineEnd < 0 ? wholeLog.Length : lineEnd;
            scanFrom = idx + 1;
        }
        sb.AppendLine("totalHistoricalShaderErrorsInLog=" + total);
        if (lastEndIndex >= 0)
        {
            int lineStart = wholeLog.LastIndexOf('\n', Math.Max(0, lastEndIndex - 1)) + 1;
            sb.AppendLine("lastErrorLine=" + wholeLog.Substring(lineStart, lastEndIndex - lineStart).Trim());
            sb.AppendLine("bytesFromLastErrorToLogEnd=" + (logLen - lastEndIndex)
                          + "  (большое число => ошибка осталась в истории и новых не было)");
        }

        sb.AppendLine("VERDICT=" + (ShaderUtil.ShaderHasError(shader) || (after - before) != 0 ? "FAIL" : "PASS"));
        return sb.ToString();
    }

    static long LogLen()
    {
        try { return new FileInfo(LogPath).Exists ? new FileInfo(LogPath).Length : 0; }
        catch { return 0; }
    }

    static string ReadAll(string path)
    {
        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
                return sr.ReadToEnd();
        }
        catch (Exception e) { return "<read failed: " + e.Message + ">"; }
    }
}
