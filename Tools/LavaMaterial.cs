using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

// LavaMaterial — создаёт пресет M_PC_Lava.mat из шейдера "Lava" и печатает
// полную таблицу свойств: тип, дефолт графа и значение пресета.
// Существующий пресет НЕ перезаписывается — только снимается таблица.
// Ядро (ProceduralLavaNoise.hlsl) и граф не трогаются.
//
// Имена свойств: ref-имя выводится сборщиком как "_" + display.Replace(" ", "_").
//
// Значения пресета совпадают с дефолтами графа (сборщик записал те же числа):
// 31 float + 7 color. Эмиссия включается САМИМ графом — блок
// SurfaceDescription.Emission подключён и в URP уходит в surface.emission без
// ключевого слова, поэтому keywords у материала остаются пустыми.
public static class LavaMaterial
{
    const string MatPath = "Assets/_Project/Materials/Lava/M_PC_Lava.mat";
    const string ShaderName = "Lava";

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
            log.AppendLine("== M_PC_Lava.mat уже существует — не перезаписываю ==");
            Dump(existing, log);
            Debug.Log(log.ToString());
            return log.ToString();
        }

        var mat = new Material(sh);
        mat.name = "M_PC_Lava";

        // ── корка ───────────────────────────────────────────────────────────
        SetF(mat, "_Crust_Scale", 1.5f);
        SetF(mat, "_Crust_Stretch", 2.4f);
        SetF(mat, "_Crust_Width", 0.09f);
        SetF(mat, "_Crust_Seed", 4.3f);
        SetF(mat, "_Plate_Vary", 0.55f);
        SetF(mat, "_Plate_Bump", 0.18f);
        SetF(mat, "_Fissure_Bump", -0.55f);

        // ── жар и расплав ───────────────────────────────────────────────────
        SetF(mat, "_Heat_Scale", 0.85f);
        SetF(mat, "_Heat_Stretch", 1.5f);
        SetF(mat, "_Heat_Seed", 12.7f);
        SetF(mat, "_Heat_Threshold", 0.40f);
        SetF(mat, "_Heat_Softness", 0.34f);
        SetF(mat, "_Heat_Bump", 0.30f);
        SetF(mat, "_Molten_Threshold", 0.62f);
        SetF(mat, "_Molten_Softness", 0.22f);

        // ── поток ───────────────────────────────────────────────────────────
        SetF(mat, "_Flow_Scale", 2.6f);
        SetF(mat, "_Flow_Stretch", 5.0f);
        SetF(mat, "_Flow_Width", 0.25f);
        SetF(mat, "_Flow_Seed", 27.1f);
        SetF(mat, "_Flow_Amount", 0.55f);
        SetF(mat, "_Flow_Bump", 0.35f);

        // ── шлак ────────────────────────────────────────────────────────────
        SetF(mat, "_Skin_Scale", 14.0f);
        SetF(mat, "_Skin_Detail", 2.4f);
        SetF(mat, "_Skin_Seed", 8.9f);
        SetF(mat, "_Skin_Amount", 0.40f);
        SetF(mat, "_Skin_Bump", 0.28f);

        // ── свечение, глянец, рельеф ────────────────────────────────────────
        SetF(mat, "_Glow_Amount", 1.0f);
        SetF(mat, "_Emission_Strength", 3.0f);
        SetF(mat, "_Molten_Gloss", 0.55f);
        SetF(mat, "_Smoothness", 0.34f);
        SetF(mat, "_Bump_Strength", 1.0f);

        // ── цвета ───────────────────────────────────────────────────────────
        SetC(mat, "_Crust_Color", 0.090f, 0.085f, 0.080f);
        SetC(mat, "_Plate_Color", 0.160f, 0.150f, 0.140f);
        SetC(mat, "_Ash_Color", 0.260f, 0.250f, 0.240f);
        SetC(mat, "_Ember_Color", 0.420f, 0.075f, 0.020f);
        SetC(mat, "_Melt_Color", 1.000f, 0.360f, 0.050f);
        SetC(mat, "_Stream_Color", 1.000f, 0.720f, 0.120f);
        SetC(mat, "_Core_Color", 1.000f, 0.940f, 0.620f);

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
        else Debug.LogWarning("LavaMaterial: нет свойства " + n);
    }

    static void SetC(Material m, string n, float r, float g, float b)
    {
        if (m.HasProperty(n)) m.SetColor(n, new Color(r, g, b, 1f));
        else Debug.LogWarning("LavaMaterial: нет свойства " + n);
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
