using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

// FoliageMaterial — создаёт пресет M_PC_Foliage.mat из шейдера "Foliage" и печатает
// полную таблицу свойств: тип, дефолт графа и значение пресета.
// Существующий пресет НЕ перезаписывается — только снимается таблица.
// Ядро (ProceduralFoliageNoise.hlsl) и граф не трогаются.
//
// Имена свойств: ref-имя выводится сборщиком как "_" + display.Replace(" ", "_").
public static class FoliageMaterial
{
    const string MatPath = "Assets/_Project/Materials/Foliage/M_PC_Foliage.mat";
    const string ShaderName = "Foliage";

    public static string Execute()
    {
        var log = new StringBuilder();
        Shader sh = Shader.Find(ShaderName);
        if (sh == null)
        {
            log.AppendLine("!! шейдер " + ShaderName + " не найден");
            Debug.Log(log.ToString());
            return log.ToString();
        }

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (existing != null)
        {
            log.AppendLine("== M_PC_Foliage.mat уже существует — не перезаписываю ==");
            Dump(existing, log);
            Debug.Log(log.ToString());
            return log.ToString();
        }

        var mat = new Material(sh);
        mat.name = "M_PC_Foliage";

        // ── лист ────────────────────────────────────────────────────────────
        SetF(mat, "_Leaf_Scale", 6.5f);
        SetF(mat, "_Leaf_Flatten", 5.0f);
        SetF(mat, "_Leaf_Stretch", 1.6f);
        SetF(mat, "_Leaf_Edge", 0.45f);
        SetF(mat, "_Leaf_Seed", 3.7f);
        SetF(mat, "_Leaf_Tone", 0.85f);
        SetF(mat, "_Leaf_Edge_Amount", 0.55f);
        SetF(mat, "_Leaf_Gloss", 0.10f);
        SetF(mat, "_Leaf_Edge_Gloss", 0.28f);

        // ── жилки ───────────────────────────────────────────────────────────
        SetF(mat, "_Vein_Width", 0.035f);
        SetF(mat, "_Vein_Count", 5.0f);
        SetF(mat, "_Vein_Bump", -0.30f);

        // ── масса кроны ─────────────────────────────────────────────────────
        SetF(mat, "_Mass_Scale", 0.9f);
        SetF(mat, "_Mass_Stretch", 1.0f);
        SetF(mat, "_Mass_Seed", 7.1f);
        SetF(mat, "_Mass_Threshold", 0.42f);
        SetF(mat, "_Mass_Softness", 0.28f);
        SetF(mat, "_Mass_Amount", 0.75f);
        SetF(mat, "_Mass_Bump", 0.55f);

        // ── сухость ─────────────────────────────────────────────────────────
        SetF(mat, "_Dry_Scale", 1.7f);
        SetF(mat, "_Dry_Stretch", 1.9f);
        SetF(mat, "_Dry_Seed", 13.4f);
        SetF(mat, "_Dry_Threshold", 0.50f);
        SetF(mat, "_Dry_Softness", 0.22f);
        SetF(mat, "_Dry_Amount", 0.60f);

        // ── выгорание ───────────────────────────────────────────────────────
        SetF(mat, "_Bleach_Scale", 0.7f);
        SetF(mat, "_Bleach_Stretch", 0.8f);
        SetF(mat, "_Bleach_Seed", 31.7f);
        SetF(mat, "_Bleach_Threshold", 0.52f);
        SetF(mat, "_Bleach_Softness", 0.26f);
        SetF(mat, "_Bleach_Amount", 0.45f);

        // ── сучья ───────────────────────────────────────────────────────────
        SetF(mat, "_Twig_Scale", 1.9f);
        SetF(mat, "_Twig_Width", 0.10f);
        SetF(mat, "_Twig_Seed", 5.3f);
        SetF(mat, "_Twig_Amount", 0.45f);
        SetF(mat, "_Twig_Bump", -0.35f);

        // ── рельеф и глянец ─────────────────────────────────────────────────
        SetF(mat, "_Bump_Strength", 1.0f);
        SetF(mat, "_Smoothness", 0.22f);

        // ── цвета ───────────────────────────────────────────────────────────
        SetC(mat, "_Leaf_Color", 0.24f, 0.36f, 0.16f);
        SetC(mat, "_Leaf_Light_Color", 0.46f, 0.55f, 0.24f);
        SetC(mat, "_Leaf_Deep_Color", 0.10f, 0.17f, 0.09f);
        SetC(mat, "_Dry_Color", 0.44f, 0.33f, 0.15f);
        SetC(mat, "_Twig_Color", 0.19f, 0.14f, 0.10f);
        SetC(mat, "_Gap_Color", 0.05f, 0.07f, 0.04f);

        AssetDatabase.CreateAsset(mat, MatPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Material made = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        log.AppendLine("== CREATED " + MatPath + " ==");
        Dump(made, log);
        Debug.Log(log.ToString());
        return log.ToString();
    }

    static void SetF(Material m, string n, float v)
    {
        if (m.HasProperty(n)) m.SetFloat(n, v);
        else Debug.LogWarning("FoliageMaterial: нет свойства " + n);
    }

    static void SetC(Material m, string n, float r, float g, float b)
    {
        if (m.HasProperty(n)) m.SetColor(n, new Color(r, g, b, 1f));
        else Debug.LogWarning("FoliageMaterial: нет свойства " + n);
    }

    static void Dump(Material m, StringBuilder log)
    {
        if (m == null) { log.AppendLine("   материал не загрузился"); return; }
        Shader sh = m.shader;
        log.AppendLine("   shader=" + sh.name + " queue=" + m.renderQueue.ToString(CultureInfo.InvariantCulture)
            + " keywords=" + m.shaderKeywords.Length.ToString(CultureInfo.InvariantCulture)
            + " shaderProps=" + sh.GetPropertyCount().ToString(CultureInfo.InvariantCulture));
        var names = new List<string>();
        for (int i = 0; i < sh.GetPropertyCount(); i++) names.Add(sh.GetPropertyName(i));
        names.Sort(StringComparer.Ordinal);
        for (int i = 0; i < names.Count; i++)
        {
            string n = names[i];
            int idx = sh.FindPropertyIndex(n);
            if (idx < 0) continue;
            UnityEngine.Rendering.ShaderPropertyType pt = sh.GetPropertyType(idx);
            string def = "-", val = "-";
            switch (pt)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    def = sh.GetPropertyDefaultFloatValue(idx).ToString("0.#####", CultureInfo.InvariantCulture);
                    val = m.GetFloat(n).ToString("0.#####", CultureInfo.InvariantCulture);
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    Vector4 dv = sh.GetPropertyDefaultVectorValue(idx);
                    def = F4(dv);
                    Color c = m.GetColor(n);
                    val = F4(new Vector4(c.r, c.g, c.b, c.a));
                    break;
                default:
                    val = "(текстура)";
                    break;
            }
            log.AppendLine("   " + n + " | " + pt + " | default=" + def + " | preset=" + val);
        }
    }

    static string F4(Vector4 v)
    {
        return v.x.ToString("0.###", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.###", CultureInfo.InvariantCulture)
            + "," + v.z.ToString("0.###", CultureInfo.InvariantCulture) + "," + v.w.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
