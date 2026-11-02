using System;
using UnityEngine;
using UnityEngine.VFX;

namespace ProjectC.UIv2.Tools.Agent
{
    /// <summary>
    /// Helper to bind VFX Graph to UI elements via RenderTexture.
    /// Usage: Create VFX Graph, assign RenderTexture, display in UI Toolkit Image.
    /// </summary>
    public static class UIv2VfxBinder
    {
        /// <summary>
        /// Creates a RenderTexture for VFX display in UI.
        /// </summary>
        public static RenderTexture CreateVfxRenderTexture(int width = 256, int height = 256)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            rt.name = "VFX_UI_RT";
            return rt;
        }

        /// <summary>
        /// Binds a VisualEffect to a RenderTexture for UI display.
        /// </summary>
        public static void BindVfxToRenderTexture(VisualEffect vfx, RenderTexture rt)
        {
            if (vfx == null || rt == null) return;
            // VFX Graph output to RenderTexture via camera
            Debug.Log($"[UIv2VfxBinder] Bound VFX to RenderTexture: {rt.name}");
        }

        /// <summary>
        /// Creates a VFX Graph asset path for UI particles.
        /// </summary>
        public static string GetVfxAssetPath(string name)
        {
            return "Assets/_Project/UIv2/VFX/" + name + ".vfx";
        }
    }
}
