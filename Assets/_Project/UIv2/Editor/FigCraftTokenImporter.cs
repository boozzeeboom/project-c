using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ProjectC.UIv2.Tools.FigCraft
{
    /// <summary>
    /// Imports design tokens from a FigCraft variable.export (CSS) file into
    /// Assets/_Project/UIv2/DesignSystem/DesignTokens.uss.
    ///
    /// Workflow:
    ///   1. In FigCraft: variable.export { format: "css", path: "tokens.css" }
    ///   2. Copy tokens.css into the Unity project (e.g. Assets/_Project/UIv2/Tools/FigCraft/Import/tokens.css)
    ///   3. Run: Project C / UIv2 / Import FigCraft Tokens
    /// </summary>
    public static class FigCraftTokenImporter
    {
        private const string ImportFolder = "Assets/_Project/UIv2/Tools/FigCraft/Import";
        private const string OutputPath = "Assets/_Project/UIv2/DesignSystem/DesignTokens.uss";

        [MenuItem("Project C/UIv2/Import FigCraft Tokens (CSS)")]
        public static void ImportTokens()
        {
            string cssPath = Path.Combine(ImportFolder, "tokens.css");
            if (!File.Exists(cssPath))
            {
                // Allow picking a file manually
                cssPath = EditorUtility.OpenFilePanel("Select FigCraft tokens.css", ImportFolder, "css");
                if (string.IsNullOrEmpty(cssPath)) return;
            }

            string css = File.ReadAllText(cssPath);

            // Extract :root { ... } block (or first block) custom properties
            var rootMatch = Regex.Match(css, @":root\s*\{([^}]*)\}", RegexOptions.Singleline);
            string block = rootMatch.Success ? rootMatch.Groups[1].Value : css;

            // Parse --name: value; pairs
            var propRegex = new Regex(@"(--[\w-]+)\s*:\s*([^;]+);", RegexOptions.Multiline);
            var matches = propRegex.Matches(block);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("/* DesignTokens.uss — imported from FigCraft variable.export (CSS). */");
            sb.AppendLine("/* Source: " + cssPath + " */");
            sb.AppendLine();
            sb.AppendLine(":root {");

            foreach (Match m in matches)
            {
                string name = m.Groups[1].Value.Trim();
                string value = m.Groups[2].Value.Trim();
                sb.AppendLine($"    {name}: {value};");
            }

            sb.AppendLine("}");
            sb.AppendLine();

            // Preserve theme overrides (light/compact) if present in CSS via [data-*] selectors
            // FigCraft emits: [data-<collection>="<mode>"] { ... }
            var modeRegex = new Regex(@"\[data-[\w-]+=""([^""]+)""\]\s*\{([^}]*)\}", RegexOptions.Singleline);
            foreach (Match mm in modeRegex.Matches(css))
            {
                string mode = mm.Groups[1].Value;
                string modeBlock = mm.Groups[2].Value;
                var modeProps = propRegex.Matches(modeBlock);
                if (modeProps.Count == 0) continue;

                string className = mode.ToLower().Contains("light") ? "theme-light"
                                 : mode.ToLower().Contains("dark") ? "theme-dark"
                                 : "theme-" + mode.ToLower().Replace(" ", "-");

                sb.AppendLine($"/* Mode: {mode} */");
                sb.AppendLine($".{className} {{");
                foreach (Match p in modeProps)
                {
                    sb.AppendLine($"    {p.Groups[1].Value.Trim()}: {p.Groups[2].Value.Trim()};");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }

            // Ensure output directory exists
            string outDir = Path.GetDirectoryName(OutputPath);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            File.WriteAllText(OutputPath, sb.ToString());
            AssetDatabase.Refresh();

            Debug.Log($"[FigCraftTokenImporter] Imported {matches.Count} tokens → {OutputPath}");
            EditorUtility.DisplayDialog("FigCraft Tokens Imported",
                $"Imported {matches.Count} custom properties into:\n{OutputPath}", "OK");
        }

        [MenuItem("Project C/UIv2/Open FigCraft Import Folder")]
        public static void OpenImportFolder()
        {
            if (!Directory.Exists(ImportFolder)) Directory.CreateDirectory(ImportFolder);
            EditorUtility.RevealInFinder(ImportFolder);
        }
    }
}
