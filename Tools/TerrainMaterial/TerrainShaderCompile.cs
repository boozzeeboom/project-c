using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

// Принудительная реальная компиляция шейдера Terrain и сбор диагностики.
//
// Зачем: ShaderUtil.ShaderHasError/GetShaderMessages до реальной компиляции варианта
// возвращают пусто, а check_compile_errors вообще не видит ошибки HLSL. Ошибка
// вида "undeclared identifier 'PCTrn_RockStrata_float'" видна только в Logs/Editor.log
// и только после того, как материал заставит Unity реально собрать вариант.
//
// Здесь это делается детерминированно:
//   1) переимпорт HLSL + .shadergraph (граф регенерирует шейдер);
//   2) ShaderUtil.CompilePass(mat, pass, true) по всем пассам — форсированная
//      синхронная сборка;
//   3) вычитка прироста Logs/Editor.log на строки "Shader error in 'Terrain'".
public static class TerrainShaderCompile
{
    const string HlslPath  = "Assets/_Project/Shaders/ProceduralTerrainNoise.hlsl";
    const string GraphPath = "Assets/_Project/Materials/Terrain/Terrain.shadergraph";
    const string MatPath   = "Assets/_Project/Materials/Terrain/M_PC_Terrain.mat";
    const string LogPath   = "Logs/Editor.log";

    public static string Execute()
    {
        var sb = new StringBuilder();

        AssetDatabase.ImportAsset(HlslPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        var shader = Shader.Find("Terrain");
        if (shader == null)
        {
            return "FAIL: Shader.Find(\"Terrain\") == null";
        }
        sb.AppendLine("shader=" + shader.name + " isSupported=" + shader.isSupported
                      + " subshaders=" + shader.subshaderCount + " passes(sub0)=" + shader.GetPassCountInSubshader(0));

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            return sb.Append("FAIL: no material at " + MatPath).ToString();
        }
        sb.AppendLine("material=" + mat.name + " shaderOfMaterial=" + (mat.shader == null ? "<null>" : mat.shader.name));

        long logBefore = LogLength();

        ShaderUtil.ClearShaderMessages(shader);

        var perPass = new List<string>();
        int passCount = shader.GetPassCountInSubshader(0);
        for (int p = 0; p < passCount; p++)
        {
            string before = CountLogErrors(logBefore);
            ShaderUtil.CompilePass(mat, p, true);
            bool hasErr = ShaderUtil.ShaderHasError(shader);
            perPass.Add("pass[" + p + "] hasErrorAfter=" + hasErr + " logDelta=" + before);
        }
        foreach (var line in perPass) sb.AppendLine(line);

        sb.AppendLine("ShaderHasError(final)=" + ShaderUtil.ShaderHasError(shader));
        sb.AppendLine("hasWarnings=" + ShaderUtil.ShaderHasWarnings(shader));

        var msgs = ShaderUtil.GetShaderMessages(shader);
        sb.AppendLine("messages(defaultPlatform)=" + (msgs == null ? -1 : msgs.Length));
        if (msgs != null)
        {
            foreach (var m in msgs)
            {
                sb.AppendLine("  [" + m.severity + "] " + m.message + " @" + m.file + ":" + m.line);
            }
        }

        var msgs11 = ShaderUtil.GetShaderMessages(shader, ShaderCompilerPlatform.D3D);
        sb.AppendLine("messages(d3d)=" + (msgs11 == null ? -1 : msgs11.Length));
        if (msgs11 != null)
        {
            foreach (var m in msgs11)
            {
                sb.AppendLine("  [" + m.severity + "] " + m.message + " @" + m.file + ":" + m.line);
            }
        }

        // ---- прирост лога ----
        string delta = ReadLogFrom(logBefore);
        int errCount = 0;
        foreach (var raw in delta.Split('\n'))
        {
            string l = raw.TrimEnd('\r');
            if (l.Contains("Shader error in 'Terrain'") || l.Contains("Shader warning in 'Terrain'")
                || l.Contains("PCTrn_") && (l.Contains("undeclared") || l.Contains("error")))
            {
                errCount++;
                sb.AppendLine("LOG> " + (l.Length > 400 ? l.Substring(0, 400) : l));
            }
        }
        sb.AppendLine("logShaderErrorLines=" + errCount);

        // ---- доказательство формы вызова (обёртки обязаны существовать) ----
        string hlsl = ReadAll(HlslPath);
        string[] expected =
        {
            "PCTrn_SlopeAngle", "PCTrn_ZoneWarp", "PCTrn_RockStrata", "PCTrn_RockJoints",
            "PCTrn_RockBlockTone", "PCTrn_RockDetail", "PCTrn_Scree", "PCTrn_SnowDrift",
            "PCTrn_SnowGrain", "PCTrn_IceCracks", "PCTrn_LowMottle", "PCTrn_LowPatches", "PCTrn_Dust"
        };
        foreach (var fn in expected)
        {
            bool f = hlsl.Contains("void " + fn + "_float(");
            bool h = hlsl.Contains("void " + fn + "_half(");
            if (!f || !h) sb.AppendLine("MISSING WRAPPER: " + fn + " float=" + f + " half=" + h);
        }
        sb.AppendLine("wrappersChecked=" + expected.Length);

        sb.AppendLine("VERDICT=" + (errCount == 0 && !ShaderUtil.ShaderHasError(shader) ? "PASS" : "FAIL"));
        return sb.ToString();
    }

    static long LogLength()
    {
        try { return new FileInfo(LogPath).Exists ? new FileInfo(LogPath).Length : 0; }
        catch { return 0; }
    }

    static string CountLogErrors(long from)
    {
        string d = ReadLogFrom(from);
        int n = 0;
        foreach (var raw in d.Split('\n'))
            if (raw.Contains("Shader error in 'Terrain'")) n++;
        return n.ToString();
    }

    static string ReadLogFrom(long offset)
    {
        try
        {
            if (!new FileInfo(LogPath).Exists) return "";
            using (var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (offset > fs.Length) offset = 0;
                fs.Seek(offset, SeekOrigin.Begin);
                using (var sr = new StreamReader(fs))
                    return sr.ReadToEnd();
            }
        }
        catch (Exception e) { return "<log read failed: " + e.Message + ">"; }
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
