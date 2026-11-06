using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class AuditGeneratedModels
{
    const string Root = "Assets/Generated_Models";
    const string Out = "Assets/_AuraTemp/audit.txt";

    public static void Execute()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== ROOT FILES ===");
        foreach (var f in Directory.GetFiles(Root).Where(f => !f.EndsWith(".meta")))
            sb.AppendLine(Path.GetFileName(f) + "\t" + File.GetLastWriteTime(f).ToString("yyyy-MM-dd HH:mm") + "\t" + new FileInfo(f).Length);

        sb.AppendLine("=== DIRS (sorted by newest file) ===");
        var dirs = Directory.GetDirectories(Root);
        var rows = new List<(DateTime last, string line)>();
        foreach (var d in dirs)
        {
            var name = Path.GetFileName(d);
            var files = Directory.GetFiles(d, "*", SearchOption.AllDirectories)
                                 .Where(f => !f.EndsWith(".meta")).ToArray();
            var fbx = files.Where(f => f.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).ToArray();
            long size = 0;
            foreach (var f in files) size += new FileInfo(f).Length;
            var last = files.Length > 0 ? files.Max(f => File.GetLastWriteTime(f)) : Directory.GetLastWriteTime(d);
            var line = string.Join("\t", new[]
            {
                name,
                "files=" + files.Length,
                "fbx=" + fbx.Length,
                "MB=" + (size / 1048576.0).ToString("F1"),
                "last=" + last.ToString("yyyy-MM-dd HH:mm"),
                "dirLast=" + Directory.GetLastWriteTime(d).ToString("yyyy-MM-dd HH:mm"),
                "ext=" + string.Join(",", files.Select(f => Path.GetExtension(f).ToLowerInvariant())
                                               .Distinct().OrderBy(x => x))
            });
            rows.Add((last, line));
        }
        foreach (var r in rows.OrderByDescending(r => r.last)) sb.AppendLine(r.line);

        File.WriteAllText(Out, sb.ToString());
        Debug.Log("[AUDIT] dirs=" + rows.Count + " written to " + Out);
    }
}
