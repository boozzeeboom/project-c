using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ProjectCMainMenu;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class PCMMPuffProbe
{
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

    static float CompositeLinear(float texelA, float elemOp, float tintLuma, float backdropLuma)
    {
        var a = Mathf.Clamp01(texelA * elemOp);
        var lb = ToLinear(backdropLuma);
        var lc = ToLinear(tintLuma);
        return ToSrgb(lb + a * (lc - lb));
    }

    static float MaxAlpha(Texture2D t)
    {
        var px = t.GetPixels32();
        float m = 0f;
        for (var i = 0; i < px.Length; i++) if (px[i].a > m) m = px[i].a;
        return m / 255f;
    }

    // runs one real gust and returns the observed peak element opacity of every puff
    static List<float> RunPeak(Rect rect, int count, float strength, float spread, float duration, int dir, out int pale)
    {
        var host = new VisualElement();
        var field = new ProjectCMainMenuSmokeField(host);
        var t0 = Time.realtimeSinceStartupAsDouble;
        field.Disperse(rect, count, strength, spread, duration, 0f, dir);

        var node = field.Node;
        pale = 0;
        for (var i = 0; i < node.childCount; i++)
        {
            var bg = node[i].style.backgroundImage.value.texture;
            if (bg != null && bg.name.Contains("233_242_236")) pale++;
        }

        var peaks = new float[node.childCount];
        for (var ms = 0; ms <= 2000; ms += 2)
        {
            field.Tick(t0 + ms / 1000.0);
            for (var i = 0; i < node.childCount; i++)
            {
                if (i >= peaks.Length) break;
                var op = node[i].style.opacity.value;
                if (op > peaks[i]) peaks[i] = op;
            }
        }

        return peaks.ToList();
    }

    public static void Execute()
    {
        var sb = new StringBuilder();
        var lightMax = MaxAlpha(ProjectCMainMenuSmokeTextures.Light);
        sb.AppendLine("Light sheet texel alpha max : " + lightMax.ToString("F3", Inv));
        sb.AppendLine("PalePeak                    : " + ProjectCMainMenuSmokeField.PalePeak.ToString("F3", Inv));
        sb.AppendLine();

        // the press gust exactly as Press() asks for it, on the measured btn0 rect
        var btn = new Rect(140.2f, 500.3f, 403.8f, 64.3f);
        var peaks = RunPeak(btn, 13, 1.1f, 1f, 560f, 1, out var pale);
        var maxPeak = peaks.Count == 0 ? 0f : peaks.Max();
        var minPeak = peaks.Count == 0 ? 0f : peaks.Min();

        sb.AppendLine("=== press burst on btn0 (Disperse 13, strength 1.1) ===");
        sb.AppendLine("puffs " + peaks.Count + "  pale " + pale + "  charcoal " + (peaks.Count - pale));
        sb.AppendLine("element peak opacity  min " + minPeak.ToString("F3", Inv) + "  max " + maxPeak.ToString("F3", Inv)
            + "  (authored 0.550 x PalePeak = " + (0.55f * ProjectCMainMenuSmokeField.PalePeak).ToString("F3", Inv) + ")");
        sb.AppendLine();

        sb.AppendLine("=== pale core as it reads on screen (sRGB of the puff centre) ===");
        sb.AppendLine("elemOp   backdrop   after PalePeak   before (PalePeak=1)");
        float[] ops = { 0.275f, 0.46f, 0.55f };
        float[] backdrops = { 0.18f, 0.30f, 0.42f };
        foreach (var op in ops)
        {
            foreach (var bd in backdrops)
            {
                sb.AppendLine(string.Format(Inv, "{0:F3}    {1:F2}       {2:F3}             {3:F3}",
                    op, bd,
                    CompositeLinear(lightMax, op, 0.9398f, bd),
                    CompositeLinear(lightMax, op / ProjectCMainMenuSmokeField.PalePeak, 0.9398f, bd)));
            }
        }
        sb.AppendLine();

        // the darkest case: the pale puff over the charcoal button cloud body (0.18)
        sb.AppendLine("worst case (over the button cloud, bd 0.18): "
            + CompositeLinear(lightMax, maxPeak, 0.9398f, 0.18f).ToString("F3", Inv) + " sRGB, was "
            + CompositeLinear(lightMax, maxPeak / ProjectCMainMenuSmokeField.PalePeak, 0.9398f, 0.18f).ToString("F3", Inv));

        var report = "Assets/Aura/Mods/ProjectCMainMenu/_puff_probe.txt";
        File.WriteAllText(report, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(report, ImportAssetOptions.ForceUpdate);
        Debug.Log("[PCMMPuffProbe] done");
    }
}
