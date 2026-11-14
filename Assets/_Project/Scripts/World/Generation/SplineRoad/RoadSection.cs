using UnityEngine;

namespace ProjectC.World.Generation
{
    /// <summary>
    /// Маркер сгенерированного объекта дорожки (меш-объект и группа коллайдеров).
    /// По нему инструмент находит своё и сносит перед пересборкой — ручные объекты рядом не трогаются.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadSection : MonoBehaviour
    {
        public SplineRoad owner;
    }
}
