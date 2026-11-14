using UnityEngine;
using UnityEngine.Splines;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Маркер сгенерированного объекта забора. По нему инструмент находит своё и сносит
    /// перед пересборкой — ручные объекты рядом не трогаются.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FenceSection : MonoBehaviour
    {
        public SplineFence owner;
    }
}
