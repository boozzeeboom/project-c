using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

// TilesMaterial — создаёт пресет M_PC_Tiles.mat из шейдера "Tiles" и печатает
// полную таблицу свойств: тип, дефолт графа и значение пресета.
// Существующий пресет НЕ перезаписывается — только снимается таблица.
// Ядро и граф не трогаются.
public static class TilesMaterial
{
    const string MatPath = "Assets/_Project/Materials/Tiles/M_PC_Tiles.mat";
    const string ShaderName = "Tiles";

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
            log.AppendLine("== M_PC_Tiles.mat уже существует — не перезаписываю ==");
            Dump(existing, log);
            Debug.Log(log.ToString());
            return log.ToString();
        }

        var mat = new Material(sh);
        mat.name = "M_PC_Tiles";

        SetF(mat, "_Tile_Scale", 3.1f);
        SetF(mat, "_Tile_Aspect", 1.28f);
        SetF(mat, "_Tile_Tail", 0.32f);
        SetF(mat, "_Tile_Warp", 0.08f);
        SetF(mat, "_Joint_Width_X", 0.06f);
        SetF(mat, "_Joint_Width_Y", 0.10f);
        SetF(mat, "_Tile_Tone", 0.75f);
        SetF(mat, "_Joint_Amount", 0.8f);
        SetF(mat, "_Tile_Crown", 0.18f);
        SetF(mat, "_Joint_Bump", -0.55f);
        SetF(mat, "_Tile_Gloss", 0.08f);
        SetF(mat, "_Gap_Gloss", -0.10f);
        SetF(mat, "_Bump_Strength", 1f);
        SetF(mat, "_Smoothness", 0.30f);
        SetF(mat, "_Craze_Scale", 140f);
        SetF(mat, "_Craze_Width", 0.045f);
        SetF(mat, "_Pore_Scale", 38f);
        SetF(mat, "_Tile_Grain_Scale", 55f);
        SetF(mat, "_Moss_Scale", 3.4f);
        SetF(mat, "_Moss_Stretch", 5f);

        SetC(mat, "_Tile_Color", 0.46f, 0.20f, 0.13f);
        SetC(mat, "_Tile_Light_Color", 0.62f, 0.34f, 0.22f);
        SetC(mat, "_Gap_Color", 0.09f, 0.07f, 0.05f);
        SetC(mat, "_Salt_Color", 0.80f, 0.78f, 0.72f);
        SetC(mat, "_Moss_Color", 0.20f, 0.26f, 0.13f);

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
        else Debug.LogWarning("TilesMaterial: нет свойства " + n);
    }

    static void SetC(Material m, string n, float r, float g, float b)
    {
        if (m.HasProperty(n)) m.SetColor(n, new Color(r, g, b, 1f));
        else Debug.LogWarning("TilesMaterial: нет свойства " + n);
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
