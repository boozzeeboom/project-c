using System;
using System.Collections.Generic;

namespace ProjectC.Admin
{
    /// <summary>
    /// T-ADM-09: модель и парсер md-файлов из docs/dev/global_needtotest
    /// для вкладки «Тесты» админ-панели (F12).
    /// Формат файлов: секции "## ", чеклисты "- [ ]"/"- [x]" + продолжение
    /// с отступом, преамбулы "> ". Чистый C# без Unity-зависимостей.
    /// </summary>
    public sealed class NeedToTestItem
    {
        public bool Done;
        public string Priority; // "red" / "yellow" / "green" / ""
        public string Text = "";
    }

    public sealed class NeedToTestSection
    {
        public string Title = "";
        public readonly List<string> Preamble = new List<string>();
        public readonly List<NeedToTestItem> Items = new List<NeedToTestItem>();
    }

    public sealed class NeedToTestFile
    {
        public string FileName = "";
        public string Title = "";
        public readonly List<string> Preamble = new List<string>();
        public readonly List<NeedToTestSection> Sections = new List<NeedToTestSection>();
    }

    public static class NeedToTestParser
    {
        public static NeedToTestFile Parse(string fileName, string markdown)
        {
            var file = new NeedToTestFile { FileName = fileName ?? "" };
            if (string.IsNullOrEmpty(file.Title)) file.Title = file.FileName;
            var lines = (markdown ?? string.Empty)
                .Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            NeedToTestSection section = null;
            NeedToTestItem last = null;

            foreach (var raw in lines)
            {
                string trimmed = raw.Trim();
                if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                {
                    section = new NeedToTestSection { Title = trimmed.Substring(3).Trim() };
                    file.Sections.Add(section);
                    last = null;
                    continue;
                }
                if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                {
                    file.Title = trimmed.Substring(2).Trim();
                    last = null;
                    continue;
                }
                if (trimmed.StartsWith(">", StringComparison.Ordinal))
                {
                    string quote = trimmed.TrimStart('>').Trim();
                    if (quote.Length > 0)
                    {
                        if (section != null) section.Preamble.Add(quote);
                        else file.Preamble.Add(quote);
                    }
                    last = null;
                    continue;
                }
                if (TryParseCheckItem(trimmed, out bool done, out string text))
                {
                    if (section == null)
                    {
                        section = new NeedToTestSection { Title = "—" };
                        file.Sections.Add(section);
                    }
                    last = new NeedToTestItem { Done = done, Priority = DetectPriority(text), Text = text };
                    section.Items.Add(last);
                    continue;
                }
                // Продолжение пункта с отступом (шаги + ожидание занимают 2–3 строки).
                if (last != null && raw.Length > 0 && char.IsWhiteSpace(raw[0]) && trimmed.Length > 0
                    && !trimmed.StartsWith("-", StringComparison.Ordinal)
                    && !trimmed.StartsWith("*", StringComparison.Ordinal)
                    && !trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    last.Text += " " + trimmed;
                    last.Priority = DetectPriority(last.Text);
                    continue;
                }
                last = null;
            }
            return file;
        }

        private static bool TryParseCheckItem(string trimmed, out bool done, out string text)
        {
            done = false;
            text = "";
            if (trimmed.Length < 5) return false;
            char bullet = trimmed[0];
            if (bullet != '-' && bullet != '*') return false;
            // "- [ ] text" / "- [x] text"
            if (trimmed[1] != ' ' || trimmed[2] != '[' || trimmed[4] != ']') return false;
            char mark = trimmed[3];
            if (mark != ' ' && mark != 'x' && mark != 'X') return false;
            done = mark == 'x' || mark == 'X';
            text = trimmed.Length > 6 ? CleanMarkdown(trimmed.Substring(6).Trim()) : "";
            return true;
        }

        private static string DetectPriority(string text)
        {
            if (text.Contains("🔴")) return "red";
            if (text.Contains("🟡")) return "yellow";
            if (text.Contains("🟢")) return "green";
            return "";
        }

        private static string CleanMarkdown(string text)
        {
            return text.Replace("**", string.Empty)
                       .Replace("__", string.Empty)
                       .Replace("`", string.Empty);
        }
    }
}
