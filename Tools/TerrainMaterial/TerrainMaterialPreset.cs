// TerrainMaterialPreset.cs — создаёт Assets/_Project/Materials/Terrain/M_PC_Terrain.mat
// из шейдера Terrain и читает пресет обратно.
// Запуск: execute_script filePath=Tools/TerrainMaterial/TerrainMaterialPreset.cs methodName=Execute

using System.Text;
using UnityEditor;
using UnityEngine;

public static class TerrainMaterialPreset
{
    const string GraphPath = "Assets/_Project/Materials/Terrain/Terrain.shadergraph";
    const string MatPath = "Assets/_Project/Materials/Terrain/M_PC_Terrain.mat";

    public static string Execute()
    {
        var sh = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
        if (sh == null) return "НЕТ Shader по пути " + GraphPath;

        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (m == null)
        {
            m = new Material(sh);
            m.name = "M_PC_Terrain";
            AssetDatabase.CreateAsset(m, MatPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MatPath, ImportAssetOptions.ForceUpdate);
            m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        }
        if (m == null) return "материал не создан: " + MatPath;

        var sb = new StringBuilder();
        sb.Append("mat=" + MatPath + " shader=" + m.shader.name + " queue=" + m.renderQueue
                  + " props=" + m.shader.GetPropertyCount());

        string[] probe = new string[] { "_Rock_Color", "_Rock_Deep_Color", "_Scree_Color", "_Low_Color", "_Low_Patch_Color",
                                        "_Snow_Color", "_Ice_Color", "_Dust_Color", "_Seed", "_Zone_Warp", "_Zone_Warp_Scale",
                                        "_Low_Low", "_Low_High", "_Snow_Low", "_Snow_High", "_Ice_Low", "_Ice_High",
                                        "_Rock_Slope_Start", "_Rock_Slope_Full", "_Rock_Bright_Base", "_Rock_Bright_Amount",
                                        "_Low_Tone", "_Low_Smooth", "_Snow_Tone", "_Snow_Smooth", "_Ice_Tone", "_Ice_Smooth",
                                        "_Rock_Smooth_Base", "_Smooth_Vary", "_Rock_Bump", "_Crack_Bump", "_Snow_Bump",
                                        "_Ice_Bump", "_Bump_Strength" };
        string[] colorProps = new string[] { "_Rock_Color", "_Rock_Deep_Color", "_Scree_Color", "_Low_Color",
                                             "_Low_Patch_Color", "_Snow_Color", "_Ice_Color", "_Dust_Color" };
        foreach (var p in probe)
        {
            if (!m.HasProperty(p)) { sb.Append(" | НЕТ " + p); continue; }
            if (System.Array.IndexOf(colorProps, p) >= 0)
            {
                var c = m.GetColor(p);
                sb.Append(" | " + p + "=" + c.r + "," + c.g + "," + c.b);
            }
            else
            {
                sb.Append(" | " + p + "=" + m.GetFloat(p).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        return sb.ToString();
    }
}
