using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

// LanternMaterial — создаёт пресет M_PC_Lantern.mat из шейдера "Lantern" и
// печатает полную таблицу свойств: тип, дефолт графа и значение пресета.
// Существующий пресет НЕ перезаписывается — только снимается таблица.
// Ядро (ProceduralLanternNoise.hlsl) и граф не трогаются.
//
// Имена свойств: ref-имя выводится сборщиком как "_" + display.Replace(" ", "_").
//
// Значения пресета совпадают с дефолтами графа (сборщик записал те же числа):
// 33 float + 5 color. Эмиссия включается САМИМ графом — блок
// SurfaceDescription.Emission подключён и в URP уходит в surface.emission без
// ключевого слова, поэтому keywords у материала остаются пустыми.
//
// _Soot_Roughness ОТРИЦАТЕЛЬНА и складывается с _Glass_Smoothness: копоть
// матирует стекло (отдельной ноды «1 − x» не требуется).
public static class LanternMaterial
{
    const string MatPath = "Assets/_Project/Materials/Lantern/M_PC_Lantern.mat";
    const string ShaderName = "Lantern";

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
            log.AppendLine("== M_PC_Lantern.mat уже существует — не перезаписываю ==");
            Dump(existing, log);
            Debug.Log(log.ToString());
            return log.ToString();
        }

        var mat = new Material(sh);
        mat.name = "M_PC_Lantern";

        // ── жар за стеклом ──────────────────────────────────────────────────
        SetF(mat, "_Glow_Scale", 1.7f);
        SetF(mat, "_Glow_Stretch", 1.25f);
        SetF(mat, "_Glow_Seed", 7.1f);
        SetF(mat, "_Glow_Threshold", 0.34f);
        SetF(mat, "_Glow_Softness", 0.38f);
        SetF(mat, "_Glow_Bump", 0.22f);

        // ── ядро пламени ────────────────────────────────────────────────────
        SetF(mat, "_Core_Scale", 3.6f);
        SetF(mat, "_Core_Stretch", 1.9f);
        SetF(mat, "_Core_Seed", 21.3f);
        SetF(mat, "_Core_Threshold", 0.52f);
        SetF(mat, "_Core_Softness", 0.26f);
        SetF(mat, "_Core_Glow", 0.85f);

        // ── копоть ──────────────────────────────────────────────────────────
        SetF(mat, "_Soot_Scale", 6.5f);
        SetF(mat, "_Soot_Detail", 2.0f);
        SetF(mat, "_Soot_Seed", 33.7f);
        SetF(mat, "_Soot_Tilt", 0.40f);
        SetF(mat, "_Soot_Amount", 0.65f);
        SetF(mat, "_Soot_Roughness", -0.37f);
        SetF(mat, "_Soot_Bump", 0.18f);

        // ── стыки стёкол ────────────────────────────────────────────────────
        SetF(mat, "_Pane_Scale", 9.0f);
        SetF(mat, "_Pane_Stretch", 1.6f);
        SetF(mat, "_Pane_Width", 0.075f);
        SetF(mat, "_Pane_Seed", 15.9f);
        SetF(mat, "_Pane_Amount", 0.60f);
        SetF(mat, "_Pane_Bump", -0.30f);

        // ── свет, мерцание, глянец, рельеф ──────────────────────────────────
        SetF(mat, "_Glow_Amount", 1.0f);
        SetF(mat, "_Emission_Strength", 4.0f);
        SetF(mat, "_Flicker_Base", 0.88f);
        SetF(mat, "_Flicker_Amount", 0.12f);
        SetF(mat, "_Glass_Smoothness", 0.58f);
        SetF(mat, "_Bump_Strength", 1.0f);

        // ── анимация: СКОРОСТИ подъёма доменов ──────────────────────────────
        // Жар и ядро поднимаются вверх по локальной Y; видимая скорость =
        // _Speed x Scale / Stretch, поэтому у ядра скорость выше: оно мельче.
        SetF(mat, "_Glow_Speed", 0.10f);
        SetF(mat, "_Core_Speed", 0.22f);

        // ── цвета ───────────────────────────────────────────────────────────
        SetC(mat, "_Glass_Color", 0.045f, 0.042f, 0.038f);
        SetC(mat, "_Soot_Color", 0.018f, 0.016f, 0.014f);
        SetC(mat, "_Ember_Color", 0.420f, 0.100f, 0.020f);
        SetC(mat, "_Glow_Color", 1.000f, 0.580f, 0.160f);
        SetC(mat, "_Core_Color", 1.000f, 0.920f, 0.660f);

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
        else Debug.LogWarning("LanternMaterial: нет свойства " + n);
    }

    static void SetC(Material m, string n, float r, float g, float b)
    {
        if (m.HasProperty(n)) m.SetColor(n, new Color(r, g, b, 1f));
        else Debug.LogWarning("LanternMaterial: нет свойства " + n);
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
