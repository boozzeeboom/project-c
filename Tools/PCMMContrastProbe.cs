using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ProjectCMainMenu;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class PCMMContrastProbe
{
    const string SheetPath = "Assets/Aura/Mods/ProjectCMainMenu/UI/PCMM_Menu.uss";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static float ToLinear(float c)
    {
        c = Mathf.Clamp01(c);
        return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    static float ToSrgb(float l)
    {
        l = Mathf.Clamp01(l);
        return l <= 0.0031308f ? l * 12.92f : 1.055f * Mathf.Pow(l, 1f / 2.4f) - 0.055f;
    }

    static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    static float RelLum(Color c) => 0.2126f * ToLinear(c.r) + 0.7152f * ToLinear(c.g) + 0.0722f * ToLinear(c.b);

    static float Ratio(float l1, float l2)
    {
        var hi = Mathf.Max(l1, l2);
        var lo = Mathf.Min(l1, l2);
        return (hi + 0.05f) / (lo + 0.05f);
    }

    // what the player sees: texel alpha blended in linear over the backdrop
    static float Composite(float texelA, float tintLuma, float backdrop)
    {
        var lb = ToLinear(backdrop);
        var lc = ToLinear(tintLuma);
        return ToSrgb(lb + Mathf.Clamp01(texelA) * (lc - lb));
    }

    // an sRGB-ink layer of `alpha` (UI Toolkit blends in linear) over a backdrop
    static float Overlay(float alpha, Color tint, float backdrop)
    {
        var lb = ToLinear(backdrop);
        var lc = RelLum(tint);
        return ToSrgb(lb + alpha * (lc - lb));
    }

    static void ForceLayout()
    {
        Type t = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var cand = asm.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            if (cand != null) { t = cand; break; }
        }
        if (t == null) return;
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "UpdatePanels") continue;
            var ps = m.GetParameters();
            var args = ps.Select(p => p.HasDefaultValue ? p.DefaultValue
                : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)).ToArray();
            try { m.Invoke(null, args); } catch { }
        }
    }

    static string ShadowOf(VisualElement el)
    {
        try
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var cs = el.GetType().GetProperty("computedStyle", flags)?.GetValue(el);
            if (cs == null) return "no computedStyle";
            var p = cs.GetType().GetProperty("textShadow", flags);
            if (p == null) return "no textShadow on IComputedStyle";
            var v = p.GetValue(cs);
            var offset = v.GetType().GetProperty("offset", flags)?.GetValue(v);
            var blur = v.GetType().GetProperty("blurRadius", flags)?.GetValue(v);
            var color = v.GetType().GetProperty("color", flags)?.GetValue(v);
            return "offset " + offset + " blur " + blur + " color " + color;
        }
        catch (Exception e)
        {
            return "ERR " + e.GetType().Name + " " + e.Message;
        }
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
        var text = File.ReadAllText(SheetPath);
        sb.AppendLine("=== stylesheet ===");
        sb.AppendLine("imported with errors   : " + (sheet == null ? "NULL" : sheet.importedWithErrors.ToString()));
        sb.AppendLine("imported with warnings : " + (sheet == null ? "NULL" : sheet.importedWithWarnings.ToString()));
        sb.AppendLine("import messages        : " + msgs.Count);
        foreach (var m in msgs) sb.AppendLine("   " + m);
        sb.AppendLine("braces                 : " + text.Count(ch => ch == '{') + " / " + text.Count(ch => ch == '}'));
        var filters = text.Split('\n').Count(l => l.Contains("filter:") && !l.Contains("filter: none"));
        sb.AppendLine("non-none filter        : " + filters);
        sb.AppendLine("text-shadow rules      : " + text.Split('\n').Count(l => l.Contains("text-shadow:")));
        sb.AppendLine();

        // ---- the close cross: two 1.5 px bars of --pc-ink-dim --------------------
        var inkDim = new Color32(168, 182, 172, 255);      // --pc-ink-dim #a8b6ac
        var ink = new Color32(238, 243, 239, 255);         // --pc-ink     #eef3ef
        var faint = new Color32(113, 128, 111, 255);       // --pc-ink-faint #71806f
        var accent = new Color32(120, 184, 145, 255);      // --pc-accent  #78b891

        var halo = ProjectCMainMenuRamps.CloseHalo();
        var px = halo.GetPixels32();
        var centre = px[(halo.height / 2) * halo.width + halo.width / 2];
        var centreA = centre.a / 255f;

        sb.AppendLine("=== close halo (CloseHalo) ===");
        sb.AppendLine("texel            : " + halo.name + "  " + halo.width + "x" + halo.height);
        sb.AppendLine("centre rgb/a     : " + centre.r + "," + centre.g + "," + centre.b + " / " + centreA.ToString("F3", Inv));
        float[] backdrops = { 0.80f, 0.55f, 0.30f, 0.12f };
        sb.AppendLine("backdrop   base sRGB   ink-dim bars   ink bars   accent bars");
        foreach (var bd in backdrops)
        {
            var baseSrgb = Composite(centreA, Luma(centre), bd);
            var baseL = ToLinear(baseSrgb);
            sb.AppendLine(string.Format(Inv, "  {0:F2}      {1:F3}        {2,5:F2}:1      {3,5:F2}:1     {4,5:F2}:1",
                bd, baseSrgb, Ratio(RelLum(inkDim), baseL), Ratio(RelLum(ink), baseL), Ratio(RelLum(accent), baseL)));
        }
        sb.AppendLine();

        sb.AppendLine("=== card title / tab label: dark edge rgba(4,8,6,0.9) ===");
        var shadowTint = new Color32(4, 8, 6, 255);
        sb.AppendLine("backdrop   shadowed base   ink-dim glyph   ink glyph");
        foreach (var bd in backdrops)
        {
            var baseSrgb = Overlay(0.9f, shadowTint, bd);
            var baseL = ToLinear(baseSrgb);
            sb.AppendLine(string.Format(Inv, "  {0:F2}      {1:F3}           {2,5:F2}:1        {3,5:F2}:1",
                bd, baseSrgb, Ratio(RelLum(inkDim), baseL), Ratio(RelLum(ink), baseL)));
        }
        sb.AppendLine();
        sb.AppendLine("for reference — the old inactive tone --pc-ink-faint on the bare backdrop:");
        foreach (var bd in backdrops)
            sb.AppendLine(string.Format(Inv, "  bd {0:F2}  {1,5:F2}:1", bd, Ratio(RelLum(faint), ToLinear(bd))));
        sb.AppendLine();

        // ---- what the panel actually resolves --------------------------------
        var doc = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(d => d.gameObject.name == "ProjectCMainMenuUI");
        if (doc == null)
        {
            sb.AppendLine("UIDocument 'ProjectCMainMenuUI' not found — resolved styles unavailable.");
        }
        else
        {
            var root = doc.rootVisualElement;
            root.Clear();
            if (sheet != null) root.styleSheets.Add(sheet);
            var w = new ProjectCMainMenuWindow { AutoTick = false };
            w.Mount(root);
            w.Start();
            w.OpenSettings();
            ForceLayout();

            sb.AppendLine("=== resolved in the live panel ===");
            var tabs = new List<VisualElement>();
            for (var i = 0; i < w.TabButtons.Length; i++) tabs.Add(w.TabButtons[i]);
            for (var i = 0; i < tabs.Count; i++)
            {
                var c = tabs[i].resolvedStyle.color;
                var label = tabs[i].childCount > 0 ? tabs[i][tabs[i].childCount - 1] : null;
                sb.AppendLine(string.Format(Inv, "tab{0} {1,-10} active {2,-5} color {3},{4},{5}   luma {6:F3}",
                    i, ProjectCMainMenuWindow.Tabs[i].Id,
                    tabs[i].ClassListContains("is-active"),
                    Mathf.RoundToInt(c.r * 255), Mathf.RoundToInt(c.g * 255), Mathf.RoundToInt(c.b * 255),
                    Luma(c)));
            }

            if (w.CloseButton != null)
            {
                sb.AppendLine("close bg texture  : " + (w.CloseButton.style.backgroundImage.value.texture == null
                    ? "NULL" : w.CloseButton.style.backgroundImage.value.texture.name));
                for (var i = 0; i < w.CloseButton.childCount; i++)
                {
                    var line = w.CloseButton[i];
                    var bg = line.resolvedStyle.backgroundColor;
                    sb.AppendLine(string.Format(Inv, "close line {0} : {1},{2},{3} a{4:F2}  luma {5:F3}",
                        i, Mathf.RoundToInt(bg.r * 255), Mathf.RoundToInt(bg.g * 255), Mathf.RoundToInt(bg.b * 255),
                        bg.a, Luma(bg)));
                }
            }

            var title = w.CardTitle;
            if (title != null)
                sb.AppendLine("card title luma    : " + Luma(title.resolvedStyle.color).ToString("F3", Inv));

            // text-shadow is not on IResolvedStyle; read it off the computed style
            VisualElement[] probes = { w.CardTitle, w.TabButtons[1], tabs.Count > 1 ? tabs[1][tabs[1].childCount - 1] : null };
            foreach (var el in probes)
            {
                if (el == null) continue;
                sb.AppendLine("computed shadow " + el.name + " : " + ShadowOf(el));
            }

            root.Clear();
        }

        var report = "Assets/Aura/Mods/ProjectCMainMenu/_contrast_probe.txt";
        File.WriteAllText(report, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(report, ImportAssetOptions.ForceUpdate);
        Debug.Log("[PCMMContrastProbe] done");
    }
}
