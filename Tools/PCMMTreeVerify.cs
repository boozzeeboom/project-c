using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ProjectCMainMenu;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class PCMMTreeVerify
{
    const string SheetPath = "Assets/Aura/Mods/ProjectCMainMenu/UI/PCMM_Menu.uss";

    static void Walk(VisualElement e, List<VisualElement> into)
    {
        into.Add(e);
        for (var i = 0; i < e.childCount; i++) Walk(e.hierarchy[i], into);
    }

    public static void Execute()
    {
        var sb = new StringBuilder();
        var msgs = new List<string>();
        Application.LogCallback handler = (c, s, t) =>
        {
            if (t == LogType.Warning || t == LogType.Error || t == LogType.Exception)
                msgs.Add(t + ": " + c.Replace("\n", " | "));
        };

        AssetDatabase.Refresh();
        Application.logMessageReceived += handler;
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceUpdate);
        Application.logMessageReceived -= handler;

        var sheet = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(SheetPath);
        sb.AppendLine("USS errors  : " + (sheet == null ? "NULL" : sheet.importedWithErrors.ToString()));
        sb.AppendLine("USS warnings: " + (sheet == null ? "NULL" : sheet.importedWithWarnings.ToString()));
        sb.AppendLine("USS messages: " + msgs.Count);
        foreach (var m in msgs) sb.AppendLine("   " + m);
        sb.AppendLine();

        var host = new VisualElement();
        var w = new ProjectCMainMenuWindow();
        w.Mount(host);

        var all = new List<VisualElement>();
        Walk(host, all);

        sb.AppendLine("total elements: " + all.Count);
        sb.AppendLine();

        string[] classes =
        {
            "pc-ui-root", "pc-ui", "pc-scan", "pc-scan__sweep",
            "pc-main", "pc-main__scrim", "pc-title-block", "pc-eyebrow", "pc-wordmark",
            "pc-wordmark-text", "pc-wordmark-c", "pc-rule", "pc-subtitle",
            "pc-nav-row", "pc-nav", "pc-btn", "pc-btn-cloud", "pc-btn-cloud__rim", "pc-blob",
            "pc-btn-label", "pc-menu-note",
            "pc-alt", "pc-alt-tape", "pc-alt-band", "pc-alt-readout", "pc-alt__fade-top",
            "pc-alt__fade-bottom",
            "pc-settings", "pc-card", "pc-card-cloud", "pc-card-cloud__rim", "pc-cld",
            "pc-card__mist", "pc-card-head", "pc-card-head__rule", "pc-card-title",
            "pc-close", "pc-close-line", "pc-tabs", "pc-tabs__rule", "pc-tab", "pc-tab__glow",
            "pc-panels", "pc-panel", "pc-group-title", "pc-row", "pc-row-text", "pc-row-label",
            "pc-row-note", "pc-note-line", "pc-ctl", "pc-range", "pc-range-track",
            "pc-range-thumb", "pc-out", "pc-switch", "pc-switch-knob", "pc-switch__glow",
            "pc-select-wrap", "pc-select", "pc-select-wrap__chevron", "pc-keys", "pc-kbd",
            "pc-toast", "pc-foot", "pc-hint", "pc-hint-line", "pc-hint-keys", "pc-hint-key",
            "pc-build",
        };

        foreach (var c in classes)
        {
            var n = all.Count(e => e.ClassListContains(c));
            sb.AppendLine("  " + c.PadRight(24) + n);
        }

        sb.AppendLine();
        sb.AppendLine("is-first-in-group : " + all.Count(e => e.ClassListContains("is-first-in-group")));
        sb.AppendLine("is-hidden         : " + all.Count(e => e.ClassListContains("is-hidden")));
        sb.AppendLine("is-active         : " + all.Count(e => e.ClassListContains("is-active")));
        sb.AppendLine("pc-btn-n1..n4     : " + all.Count(e => e.ClassListContains("pc-btn-n1")) + "/"
                                                  + all.Count(e => e.ClassListContains("pc-btn-n2")) + "/"
                                                  + all.Count(e => e.ClassListContains("pc-btn-n3")) + "/"
                                                  + all.Count(e => e.ClassListContains("pc-btn-n4")));
        sb.AppendLine("focusable         : " + all.Count(e => e.focusable));
        sb.AppendLine("picking Position  : " + all.Count(e => e.pickingMode == PickingMode.Position));

        sb.AppendLine();
        sb.AppendLine("settings hidden   : " + w.Settings.ClassListContains("is-hidden"));
        sb.AppendLine("tab0 active       : " + w.TabButtons[0].ClassListContains("is-active"));
        sb.AppendLine("panel0 active     : " + w.Panels[0].ClassListContains("is-active"));
        sb.AppendLine("panel1 hidden     : " + w.Panels[1].ClassListContains("is-hidden"));
        sb.AppendLine("ui children       : " + string.Join(", ", Enumerable.Range(0, w.Ui.childCount)
                              .Select(i => w.Ui[i].GetClasses().FirstOrDefault())));
        sb.AppendLine("nav children      : " + w.Nav.childCount);
        sb.AppendLine("button->action ok : " + (w.ButtonFor("host") != null) + "/" + (w.ButtonFor("quit") != null));

        var report = "Assets/Aura/Mods/ProjectCMainMenu/_tree_verify.txt";
        File.WriteAllText(report, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(report, ImportAssetOptions.ForceUpdate);
        Debug.Log("[PCMMTreeVerify] elements=" + all.Count + " ussMsgs=" + msgs.Count);
    }
}
