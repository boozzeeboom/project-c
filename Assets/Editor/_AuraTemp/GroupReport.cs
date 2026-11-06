using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class GroupReport
{
    const string Root = "Assets/Generated_Models";
    const string Out = "Assets/_AuraTemp/groups.txt";

    static string Category(string name)
    {
        if (name.StartsWith("PC_Char_Guild_")) return "01_Characters/Guilds";
        if (name.StartsWith("PC_Char_Pilot")) return "01_Characters/Pilots";
        if (name.StartsWith("PC_Char_Medic_") || name.StartsWith("PC_Char_Stylized_Medic")) return "01_Characters/Medics";
        if (name.StartsWith("PC_Char_City_")) return "01_Characters/City";
        if (name.StartsWith("PC_Char_Winter_")) return "01_Characters/Winter";
        if (name.StartsWith("PC_Char_Creation_Chem_")) return "01_Characters/CreationChem";
        if (name.StartsWith("PC_Char_Engineer_")) return "01_Characters/Engineers";
        if (name.StartsWith("PC_Char_Explorer_")) return "01_Characters/Explorers";
        if (name.StartsWith("PC_Char_")) return "01_Characters/Archetypes";
        if (name.StartsWith("CL_F_WPN_")) return "03_Weapons";
        if (name.StartsWith("CL_F_GADGET_")) return "04_Gadgets";
        if (name.StartsWith("CL_F_")) return "02_Armor";
        return "05_Residents_EarlyOct";
    }

    public static void Execute()
    {
        var sb = new StringBuilder();
        var dirs = Directory.GetDirectories(Root).Select(Path.GetFileName)
                            .OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var groups = dirs.GroupBy(Category).OrderBy(g => g.Key);

        int totalFbx = 0; long totalSize = 0; long totalFbxSize = 0;
        foreach (var g in groups)
        {
            var list = g.ToArray();
            long fbxSize = 0;
            foreach (var d in list)
            {
                foreach (var f in Directory.GetFiles(Path.Combine(Root, d), "*.fbx", SearchOption.AllDirectories))
                    fbxSize += new FileInfo(f).Length;
            }
            sb.AppendLine(g.Key + "\tfolders=" + list.Length + "\tfbxMB=" + (fbxSize / 1048576.0).ToString("F1"));
            foreach (var d in list) sb.AppendLine("\t" + d);
            totalFbxSize += fbxSize;
        }
        foreach (var d in dirs)
            foreach (var f in Directory.GetFiles(Path.Combine(Root, d), "*", SearchOption.AllDirectories))
                totalSize += new FileInfo(f).Length;

        sb.AppendLine();
        sb.AppendLine("TOTAL folders=" + dirs.Length + " totalMB=" + (totalSize / 1048576.0).ToString("F0")
                      + " fbxMB=" + (totalFbxSize / 1048576.0).ToString("F0"));

        // old ones
        sb.AppendLine();
        sb.AppendLine("=== PRE-AUGUST / AUGUST (excluded) ===");
        foreach (var d in dirs)
        {
            var files = Directory.GetFiles(Path.Combine(Root, d), "*", SearchOption.AllDirectories)
                                 .Where(f => !f.EndsWith(".meta")).ToArray();
            var last = files.Length > 0 ? files.Max(f => File.GetLastWriteTime(f)) : Directory.GetLastWriteTime(Path.Combine(Root, d));
            if (last < new DateTime(2026, 9, 1))
                sb.AppendLine(d + "\tlast=" + last.ToString("yyyy-MM-dd") + "\tfiles=" + files.Length);
        }

        File.WriteAllText(Out, sb.ToString());
        Debug.Log("[GROUPS] written " + Out);
    }
}
