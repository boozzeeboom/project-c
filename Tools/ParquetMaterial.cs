using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

// ParquetMaterial — создаёт M_PC_Parquet.mat: копирует боевые значения из
// M_PC_Wood.mat по общим именам свойств и выставляет новые _Plank_*/_Joint_*.
public static class ParquetMaterial
{
    public static string Execute()
    {
        var log = new StringBuilder();
        const string woodPath = "Assets/_Project/Materials/Wood/M_PC_Wood.mat";
        const string outFolder = "Assets/_Project/Materials/Parquet";
        const string outPath = outFolder + "/M_PC_Parquet.mat";

        Shader parquet = Shader.Find("Parquet");
        Material wood = AssetDatabase.LoadAssetAtPath<Material>(woodPath);
        log.AppendLine("parquetShader=" + (parquet == null ? "null" : parquet.name));
        log.AppendLine("woodMat=" + (wood == null ? "null" : wood.name) + " woodShader=" + (wood == null ? "-" : wood.shader.name));
        if (parquet == null || wood == null) { log.AppendLine("ABORT"); Debug.Log(log.ToString()); return log.ToString(); }

        HashSet<string> pq = new HashSet<string>();
        for (int i = 0; i < parquet.GetPropertyCount(); i++) pq.Add(parquet.GetPropertyName(i));

        Material m = new Material(parquet);
        Shader ws = wood.shader;
        int copied = 0;
        var copiedNames = new List<string>();
        for (int i = 0; i < ws.GetPropertyCount(); i++)
        {
            string n = ws.GetPropertyName(i);
            if (!pq.Contains(n)) continue;
            UnityEngine.Rendering.ShaderPropertyType t = ws.GetPropertyType(i);
            if (t == UnityEngine.Rendering.ShaderPropertyType.Color) m.SetColor(n, wood.GetColor(n));
            else if (t == UnityEngine.Rendering.ShaderPropertyType.Vector) m.SetVector(n, wood.GetVector(n));
            else if (t == UnityEngine.Rendering.ShaderPropertyType.Float || t == UnityEngine.Rendering.ShaderPropertyType.Range) m.SetFloat(n, wood.GetFloat(n));
            else if (t == UnityEngine.Rendering.ShaderPropertyType.Texture)
            {
                m.SetTexture(n, wood.GetTexture(n));
                m.SetTextureScale(n, wood.GetTextureScale(n));
                m.SetTextureOffset(n, wood.GetTextureOffset(n));
            }
            copied++;
            copiedNames.Add(n);
        }
        log.AppendLine("copied from wood=" + copied);

        SetF(m, "_Plank_Scale", 1.5f);
        SetF(m, "_Plank_Aspect", 4.5f);
        SetF(m, "_Plank_Row_Offset", 0.5f);
        SetF(m, "_Plank_Stagger", 3f);
        SetF(m, "_Plank_Warp", 0.06f);
        SetF(m, "_Plank_Seed", 0f);
        SetF(m, "_Plank_Vary", 0.65f);
        SetF(m, "_Joint_Width_X", 0.04f);
        SetF(m, "_Joint_Width_Y", 0.05f);
        SetF(m, "_Joint_Amount", 1f);
        SetF(m, "_Joint_Gloss", 0.15f);
        SetF(m, "_Joint_Bump", -0.6f);
        m.SetColor("_Plank_Color", new Color(0.50f, 0.34f, 0.19f, 1f));
        m.SetColor("_Joint_Color", new Color(0.06f, 0.04f, 0.02f, 1f));

        m.renderQueue = 2000;
        m.name = "M_PC_Parquet";

        AssetDatabase.CreateAsset(m, outPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);

        Material saved = AssetDatabase.LoadAssetAtPath<Material>(outPath);
        log.AppendLine("== READ BACK ==");
        if (saved == null) { log.AppendLine("NOT SAVED"); Debug.Log(log.ToString()); return log.ToString(); }
        log.AppendLine("name=" + saved.name + " shader=" + saved.shader.name + " queue=" + saved.renderQueue
            + " keywords=" + saved.shaderKeywords.Length + " guid=" + AssetDatabase.AssetPathToGUID(outPath));
        string[] newOnes = new string[] { "_Plank_Scale", "_Plank_Aspect", "_Plank_Row_Offset", "_Plank_Stagger", "_Plank_Warp", "_Plank_Seed", "_Plank_Vary",
            "_Joint_Width_X", "_Joint_Width_Y", "_Joint_Amount", "_Joint_Gloss", "_Joint_Bump" };
        foreach (string n in newOnes) log.AppendLine("   " + n + " = " + saved.GetFloat(n).ToString("0.####", CultureInfo.InvariantCulture));
        log.AppendLine("   _Plank_Color = " + saved.GetColor("_Plank_Color").ToString());
        log.AppendLine("   _Joint_Color = " + saved.GetColor("_Joint_Color").ToString());
        log.AppendLine("== WOOD VALUES CARRIED ==");
        foreach (string n in new string[] { "_Rings_Scale", "_Rings_Sharpness", "_Rings_Warp", "_Knots_Scale", "_Grain_Scale", "_Grain_Stretch", "_Grain_Detail",
            "_Pores_Scale", "_Smoothness", "_LateWood_Smoothness", "_Wear_Smoothness", "_Edge_Position", "_Edge_Width", "_Edge_Wear",
            "_Scratch_Stretch", "_Scratch_Threshold", "_Scratch_Amount", "_Grain_Bump", "_Pore_Bump", "_Rings_Bump", "_Scratch_Bump" })
            log.AppendLine("   " + n + " wood=" + wood.GetFloat(n).ToString("0.####", CultureInfo.InvariantCulture)
                + " parquet=" + saved.GetFloat(n).ToString("0.####", CultureInfo.InvariantCulture));
        log.AppendLine("   _Wood_Color wood=" + wood.GetColor("_Wood_Color").ToString() + " parquet=" + saved.GetColor("_Wood_Color").ToString());
        log.AppendLine("total parquet props=" + saved.shader.GetPropertyCount());

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        return log.ToString();
    }

    static void SetF(Material m, string n, float v)
    {
        if (m.HasProperty(n)) m.SetFloat(n, v);
    }
}
