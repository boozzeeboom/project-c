using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ReorganizeV2
{
    const string Root = "Assets/Generated_Models";
    const string Out = "Assets/_AuraTemp/move2.txt";

    static readonly HashSet<string> Reserved = new HashSet<string>
    {
        "01_Characters", "02_Armor", "03_Weapons", "04_Gadgets", "05_Residents_EarlyOct", "_Legacy_PreAugust"
    };

    static readonly HashSet<string> Legacy = new HashSet<string>
    {
        "CargoPlatform_Freighter_v01", "CargoTransport_T21", "PadRing",
        "Character_17866748877224", "Female_Pilot", "Female_Pilot_Rigged", "Testing blender",
        "Character_HighQuality_Apose", "male_character_game_ready", "male_character_game_ready_rigged"
    };

    static string CharGroup(string n)
    {
        if (n.StartsWith("PC_Char_Guild_")) return "Guilds";
        if (n.StartsWith("PC_Char_Pilot")) return "Pilots";
        if (n.StartsWith("PC_Char_Medic_") || n.StartsWith("PC_Char_Stylized_Medic")) return "Medics";
        if (n.StartsWith("PC_Char_City_")) return "City";
        if (n.StartsWith("PC_Char_Winter_")) return "Winter";
        if (n.StartsWith("PC_Char_Creation_Chem_")) return "CreationChem";
        if (n.StartsWith("PC_Char_Engineer_")) return "Engineers";
        if (n.StartsWith("PC_Char_Explorer_")) return "Explorers";
        return "Archetypes";
    }

    static string ArmorGroup(string n)
    {
        if (n.StartsWith("CL_F_HEAD_HELMET")) return "Head_Helmet";
        if (n.StartsWith("CL_F_CHEST")) return "Chest";
        if (n.StartsWith("CL_F_ACC_")) return "Shoulders";
        if (n.StartsWith("CL_F_HANDS_")) return "Hands";
        if (n.StartsWith("CL_F_LEGS_")) return "Legs";
        if (n.StartsWith("CL_F_FEET_")) return "Feet";
        if (n.StartsWith("CL_F_BACKPACK") || n.StartsWith("CL_F_AIRTANK")) return "Backpacks_Airtanks";
        if (n.StartsWith("CL_F_BELT")) return "Utility_Belts";
        return null;
    }

    static string Target(string name)
    {
        if (Legacy.Contains(name)) return Root + "/_Legacy_PreAugust";
        if (name.StartsWith("PC_Char_")) return Root + "/01_Characters/" + CharGroup(name);
        if (name.StartsWith("CL_F_WPN_")) return Root + "/03_Weapons";
        if (name.StartsWith("CL_F_GADGET_")) return Root + "/04_Gadgets";
        if (name.StartsWith("CL_F_"))
        {
            var g = ArmorGroup(name);
            return g == null ? null : Root + "/02_Armor/" + g;
        }
        return Root + "/05_Residents_EarlyOct";
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static void Execute()
    {
        var sb = new StringBuilder();

        // ---- phase 1: pull the shells that were accidentally nested back to the root
        var strayParent = Root + "/05_Residents_EarlyOct";
        foreach (var t in Reserved)
        {
            if (t == "05_Residents_EarlyOct") continue;
            var stray = strayParent + "/" + t;
            if (!AssetDatabase.IsValidFolder(stray)) continue;
            var dest = Root + "/" + t;
            if (AssetDatabase.IsValidFolder(dest)) { sb.AppendLine("STRAY-KEEP\t" + stray); continue; }
            var e = AssetDatabase.MoveAsset(stray, dest);
            sb.AppendLine((string.IsNullOrEmpty(e) ? "RESTORE-OK\t" : "RESTORE-FAIL\t") + t + "\t" + e);
        }

        // ---- phase 2: snapshot sources (root children that are not reserved shells)
        var sources = Directory.GetDirectories(Root).Select(Path.GetFileName)
                              .Where(n => !Reserved.Contains(n) && !n.StartsWith("_"))
                              .OrderBy(n => n, StringComparer.Ordinal).ToList();
        sb.AppendLine("sources=" + sources.Count);

        // ---- phase 3: create every needed destination, then move
        foreach (var name in sources)
        {
            var target = Target(name);
            if (target == null) { sb.AppendLine("NO-TARGET\t" + name); continue; }
            var src = Root + "/" + name;
            var dst = target + "/" + name;
            if (!AssetDatabase.IsValidFolder(src)) { sb.AppendLine("MISSING\t" + name); continue; }
            EnsureFolder(target);
            var err = AssetDatabase.MoveAsset(src, dst);
            sb.AppendLine((string.IsNullOrEmpty(err) ? "OK\t" : "FAIL\t") + name + "\t" + (err ?? dst.Substring(Root.Length + 1)));
        }

        AssetDatabase.Refresh();

        // ---- phase 4: verify tree
        sb.AppendLine();
        sb.AppendLine("=== TREE ===");
        foreach (var d in Directory.GetDirectories(Root).OrderBy(x => x, StringComparer.Ordinal))
        {
            var n = Path.GetFileName(d);
            var kids = Directory.GetDirectories(d);
            int files = Directory.GetFiles(d, "*", SearchOption.AllDirectories).Count(f => !f.EndsWith(".meta"));
            sb.AppendLine(n + "\tdirect=" + kids.Length + "\tfiles=" + files);
            if (n.StartsWith("01_") || n.StartsWith("02_"))
                foreach (var k in kids.OrderBy(x => x, StringComparer.Ordinal))
                    sb.AppendLine("    " + Path.GetFileName(k) + "\t" + Directory.GetDirectories(k).Length);
        }
        int left = Directory.GetDirectories(Root).Length;
        sb.AppendLine("rootDirs=" + left);

        File.WriteAllText(Out, sb.ToString());
        Debug.Log("[REORG2] done");
    }
}
