using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// ParquetNfh — сравнивает слот Strength у NormalFromHeight в Parquet и в Wood-доноре,
// и печатает реальные сигнатуры GetShaderText у импортера.
public static class ParquetNfh
{
    public static string Execute()
    {
        var log = new StringBuilder();
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        log.AppendLine("=== NFH Parquet ===");
        log.AppendLine(DumpNfh(File.ReadAllText(root + "/Assets/_Project/Materials/Parquet/Parquet.shadergraph")));
        log.AppendLine("=== NFH Wood (донор) ===");
        log.AppendLine(DumpNfh(File.ReadAllText(root + "/Assets/_Project/Materials/Wood/Wood.shadergraph")));

        log.AppendLine("=== ShaderGraphImporter string-методы ===");
        Type imp = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            imp = a.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter");
            if (imp != null) break;
        }
        if (imp == null) log.AppendLine("type not found");
        else
        {
            foreach (MethodInfo mi in imp.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (mi.ReturnType != typeof(string)) continue;
                var sb = new StringBuilder("   " + mi.Name + "(");
                ParameterInfo[] ps = mi.GetParameters();
                for (int i = 0; i < ps.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(ps[i].ParameterType.Name + " " + ps[i].Name);
                }
                sb.Append(")");
                log.AppendLine(sb.ToString());
            }
        }

        Debug.Log(log.ToString());
        return log.ToString();
    }

    static string DumpNfh(string text)
    {
        var sb = new StringBuilder();
        int t = text.IndexOf("\"m_Type\": \"UnityEditor.ShaderGraph.NormalFromHeightNode\"");
        if (t < 0) { sb.AppendLine("   NFH node не найден"); return sb.ToString(); }
        int s = text.IndexOf("\"m_Slots\"", t);
        if (s < 0) { sb.AppendLine("   m_Slots не найден"); return sb.ToString(); }
        int e = text.IndexOf("]", s);
        string arr = text.Substring(s, e - s + 1);
        List<string> ids = new List<string>();
        foreach (Match x in Regex.Matches(arr, "\"m_Id\": \"([0-9a-f]{32})\"")) ids.Add(x.Groups[1].Value);
        sb.AppendLine("   m_Slots = " + arr.Replace("\n", " ").Replace("\r", ""));
        sb.AppendLine("   slot count=" + ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            int so = text.IndexOf("\"m_ObjectId\": \"" + ids[i] + "\"");
            if (so < 0) { sb.AppendLine("   slot[" + i + "] DOC MISSING"); continue; }
            int de = text.IndexOf("\n}", so);
            string doc = de > so ? text.Substring(so, de - so) : text.Substring(so, 700);
            Match disp = Regex.Match(doc, "\"m_DisplayName\": \"([^\"]*)\"");
            Match num = Regex.Match(doc, "\"m_Id\": (-?\\d+),");
            Match io = Regex.Match(doc, "\"m_SlotType\": (-?\\d+)");
            Match val = Regex.Match(doc, "\"m_Value\": ([^,\\n]+)");
            sb.AppendLine("   slot[" + i + "] display=" + (disp.Success ? disp.Groups[1].Value : "?")
                + " m_Id=" + (num.Success ? num.Groups[1].Value : "?")
                + " io=" + (io.Success ? io.Groups[1].Value : "?")
                + " m_Value=" + (val.Success ? val.Groups[1].Value : "?")
                + " objId=" + ids[i].Substring(0, 8));
        }
        return sb.ToString();
    }
}
