// T-NS M3.1: NavTarget — nullable-обёртка для Vector3.
// UnityEngine.Vector3 — структура, нельзя использовать Vector3? (CS0266).
// Этот wrapper даёт чистый API для optional targets.

using UnityEngine;

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Опциональная Vector3 цель. Замена Vector3? для Unity (Vector3 — struct, не nullable).
    /// </summary>
    public readonly struct NavTarget
    {
        public readonly bool HasValue;
        public readonly Vector3 Value;

        public NavTarget(Vector3 v) { HasValue = true; Value = v; }
        public static NavTarget None => default;
        public static implicit operator NavTarget(Vector3 v) => new NavTarget(v);
    }
}
