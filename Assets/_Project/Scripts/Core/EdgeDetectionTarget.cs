using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Rendering
{
    /// <summary>
    /// Per-object control for the fullscreen edge detection pass.
    /// The first active target supplies the shared override values; all active targets are masked.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class EdgeDetectionTarget : MonoBehaviour
    {
        private static readonly List<EdgeDetectionTarget> ActiveTargets = new();

        [Header("Global Edge Handling")]
        [Tooltip("Removes this object from the global edge pass, including the pixels around its silhouette.")]
        public bool ExcludeFromGlobal = true;

        [Tooltip("When enabled, this object uses the per-object edge settings below instead of disabling the outline.")]
        public bool UseTargetSettings = false;

        [Header("Target Edge")]
        [ColorUsage(false, false)]
        public Color TargetEdgeColor = new Color(0.02f, 0.02f, 0.04f, 1f);

        [Range(0.1f, 8f)]
        public float TargetEdgeWidth = 0.75f;

        [Range(1f, 500f)]
        public float TargetMaxEdgeDistance = 80f;

        [Range(0f, 2f)]
        public float TargetDepthFalloff = 0.8f;

        [Header("Target Depth Edges")]
        public bool TargetUseDepthEdges = true;

        [Range(0.1f, 8f)]
        public float TargetDepthSensitivity = 1f;

        [Range(0f, 0.5f)]
        public float TargetDepthThreshold = 0.12f;

        [Header("Target Normal Edges")]
        public bool TargetUseNormalEdges = true;

        [Range(0.1f, 4f)]
        public float TargetNormalSensitivity = 0.6f;

        [Range(0f, 0.8f)]
        public float TargetNormalThreshold = 0.45f;

        [Header("Target Adaptive Color")]
        public bool TargetUseAdaptiveColor = false;

        [Range(0f, 1f)]
        public float TargetAdaptiveStrength = 0.6f;

        [Header("Target Pencil Stroke")]
        public bool TargetUsePencilStroke = false;

        [Range(0f, 1f)]
        public float TargetPencilTaper = 0.7f;

        [Range(0f, 0.3f)]
        public float TargetPencilGrain = 0.08f;

        [Header("Target Softness")]
        [Range(0.005f, 0.2f)]
        public float TargetLineSoftness = 0.03f;

        public static EdgeDetectionTarget GetActiveTarget()
        {
            CleanupTargets();

            for (int i = 0; i < ActiveTargets.Count; i++)
            {
                EdgeDetectionTarget target = ActiveTargets[i];
                if (target != null && target.isActiveAndEnabled)
                    return target;
            }

            return null;
        }

        internal static void CollectActiveRenderers(List<Renderer> renderers)
        {
            renderers.Clear();
            CleanupTargets();

            for (int i = 0; i < ActiveTargets.Count; i++)
            {
                EdgeDetectionTarget target = ActiveTargets[i];
                if (target == null || !target.isActiveAndEnabled)
                    continue;

                Renderer[] targetRenderers = target.GetComponentsInChildren<Renderer>(true);
                for (int j = 0; j < targetRenderers.Length; j++)
                {
                    Renderer renderer = targetRenderers[j];
                    if (renderer != null && !renderers.Contains(renderer))
                        renderers.Add(renderer);
                }
            }
        }

        private static void CleanupTargets()
        {
            for (int i = ActiveTargets.Count - 1; i >= 0; i--)
            {
                if (ActiveTargets[i] == null)
                    ActiveTargets.RemoveAt(i);
            }
        }

        private void OnEnable()
        {
            if (!ActiveTargets.Contains(this))
                ActiveTargets.Add(this);
        }

        private void OnDisable()
        {
            ActiveTargets.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveTargets.Remove(this);
        }
    }
}
