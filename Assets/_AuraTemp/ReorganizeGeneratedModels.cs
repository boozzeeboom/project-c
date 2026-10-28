using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ReorganizeGeneratedModels
{
    const string Root = "Assets/Generated_Models";
    const string Out = "Assets/_AuraTemp/move.txt";

    static readonly Dictionary<string, string> CharGroups = new Dictionary<string, string>();
    static readonly string[] CharBuckets =
    {
        "Guilds", "Pilots", "Medics", "City", "Winter",
        "CreationChem", "Engineers", "Explorers", "Archetypes"
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

    static readonly HashSet<string> Legacy = new HashSet<string>
    {
        "CargoPlatform_Freighter_v01", "CargoTransport_T21", "PadRing",
        "Character_17866748877224", "Female_Pilot", "Female_Pilot_Rigged", "Testing blender",
        "Character_HighQuality_Apose", "male_character_game_ready", "male_character_game_ready_rigged"
    };

    // returns target folder path for a top-level folder name, or null when it stays put
    static string Target(string name, out string note)
    {
        note = "";
        if (Legacy.Contains(name)) return Root + "/_Legacy_PreAugust";

        if (name.StartsWith("PC_Char_")) return Root + "/01_Characters/" + CharGroup(name);
        if (name.StartsWith("CL_F_WPN_")) return Root + "/03_Weapons";
        if (name.StartsWith("CL_F_GADGET_")) return Root + "/04_Gadgets";
        if (name.StartsWith("CL_F_"))
        {
            var g = ArmorGroup(name);
            if (g != null) return Root + "/02_Armor/" + g;
            note = "unmatched CL_F_";
            return null;
        }
        // Oct 2026 un-prefixed resident / pilot / body meshes
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

        // 1. create all target folder shells first
        var tops = new[] { "01_Characters", "02_Armor", "03_Weapons", "04_Gadgets", "05_Residents_EarlyOct", "_Legacy_PreAugust" };
        foreach (var t in tops) EnsureFolder(Root + "/" + t);
        foreach (var b in CharBuckets) EnsureFolder(Root + "/01_Characters/" + b);
        foreach (var b in new[] { "Head_Helmet", "Chest", "Shoulders", "Hands", "Legs", "Feet", "Backpacks_Airtanks", "Utility_Belts" })
            EnsureFolder(Root + "/02_Armor/" + b);
        AssetDatabase.Refresh();

        // 2. move
        var names = Directory.GetDirectories(Root).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToList();
        int moved = 0, skipped = 0, failed = 0;
        foreach (var name in names)
        {
            string note;
            var target = Target(name, out note);
            if (target == null) { sb.AppendLine("NO-TARGET\t" + name + "\t" + note); skipped++; continue; }

            var src = Root + "/" + name;
            var dst = target + "/" + name;
            if (src == dst) { skipped++; continue; }
            if (!AssetDatabase.IsValidFolder(src)) { skipped++; continue; }

            var err = AssetDatabase.MoveAsset(src, dst);
            if (string.IsNullOrEmpty(err))
            {
                moved++;
                sb.AppendLine("OK\t" + name + "\t-> " + dst.Substring(Root.Length + 1));
            }
            else
            {
                failed++;
                sb.AppendLine("FAIL\t" + name + "\t" + err);
            }
        }

        AssetDatabase.Refresh();
        sb.Insert(0, "moved=" + moved + " skipped=" + skipped + " failed=" + failed + "\n");
        sb.AppendLine();
        sb.AppendLine("=== RESULT TREE ===");
        foreach (var d in Directory.GetDirectories(Root).OrderBy(x => x, StringComparer.Ordinal))
        {
            var n = Path.GetFileName(d);
            var direct = Directory.GetDirectories(d).Length;
            var allDirs = Directory.GetDirectories(d, "*", SearchOption.AllDirectories).Length;
            var rest = Directory.GetDirectories(Root).Count() - 1;
            sb.AppendLine(n + "\tdirect=" + direct + "\tall=" + allDirs);
        }

        File.WriteAllText(Out, sb.ToString());
        Debug.Log("[REORG] moved=" + moved + " failed=" + failed + " -> " + Out);
    }
}
