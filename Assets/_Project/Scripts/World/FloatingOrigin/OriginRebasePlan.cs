using System;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin
{
    /// <summary>
    /// Pure rebase calculation, NOT a scene mutation or a network teleport.
    /// Integrators must atomically update registered poses/caches before committing After.
    /// It is deliberately impossible for this value to move a GameObject by itself.
    /// </summary>
    public readonly struct OriginRebasePlan
    {
        public LocalCoordinateFrame Before { get; }
        public LocalCoordinateFrame After { get; }
        /// <summary>Add to a local point when the same shift is appropriate for its coordinate domain.</summary>
        public Vector3 LocalTranslation { get; }
        public bool IsValid => Before.IsValid && After.IsValid;

        private OriginRebasePlan(LocalCoordinateFrame before, LocalCoordinateFrame after, Vector3 translation)
        {
            Before = before;
            After = after;
            LocalTranslation = translation;
        }

        /// <summary>
        /// Plans a 3D shift when any local component exceeds threshold. Quantum must be
        /// positive and no greater than threshold. Bootstrap/long-range teleport must
        /// initialize a frame from its GLOBAL destination, not feed a huge local point here.
        /// </summary>
        public static bool TryCreate(LocalCoordinateFrame frame, Vector3 localFocus,
            float threshold, float quantum, out OriginRebasePlan plan)
        {
            plan = default;
            if (!frame.IsValid || !frame.ContainsLocal(localFocus))
                return false;
            if (!GlobalPosition.IsFiniteValue(threshold) || threshold <= 0f || threshold > frame.MaxLocalCoordinate)
                throw new ArgumentOutOfRangeException(nameof(threshold));
            if (!GlobalPosition.IsFiniteValue(quantum) || quantum <= 0f || quantum > threshold)
                throw new ArgumentOutOfRangeException(nameof(quantum));
            if (Math.Abs(localFocus.x) <= threshold && Math.Abs(localFocus.y) <= threshold && Math.Abs(localFocus.z) <= threshold)
                return false;

            // No hidden static origin and no repeated accumulation of a float global offset.
            double sx = Math.Round((double)localFocus.x / quantum, MidpointRounding.AwayFromZero) * quantum;
            double sy = Math.Round((double)localFocus.y / quantum, MidpointRounding.AwayFromZero) * quantum;
            double sz = Math.Round((double)localFocus.z / quantum, MidpointRounding.AwayFromZero) * quantum;
            var origin = new GlobalPosition(frame.Origin.X + sx, frame.Origin.Y + sy, frame.Origin.Z + sz);
            var after = frame.WithOrigin(origin);
            if (!after.TryToLocal(frame.ToGlobal(localFocus), out Vector3 remaining) ||
                Math.Abs(remaining.x) > threshold || Math.Abs(remaining.y) > threshold || Math.Abs(remaining.z) > threshold)
                return false;

            // Use the actual representable origin change, not merely the requested rounding.
            var translation = new Vector3((float)(frame.Origin.X - origin.X),
                (float)(frame.Origin.Y - origin.Y), (float)(frame.Origin.Z - origin.Z));
            if (!GlobalPosition.IsFiniteValue(translation.x) || !GlobalPosition.IsFiniteValue(translation.y) ||
                !GlobalPosition.IsFiniteValue(translation.z))
                return false;
            plan = new OriginRebasePlan(frame, after, translation);
            return true;
        }

        /// <summary>
        /// T-FO09H-fix: recover-план для фокуса ВНЕ фрейма. Обычный TryCreate
        /// возвращает false вне ContainsLocal — и автотриггер, и транзакция
        /// молча отказываются, а игрок остаётся с CanSimulate=false навсегда
        /// (мёртвое управление после телепорта к кораблю). Здесь: та же
        /// квантованная математика, но без гейтов ContainsLocal/порога;
        /// финальная проверка представимости сохранена. Чисто аддитивно,
        /// существующие проверки TryCreate не затрагиваются.
        /// T-FO09H-fix2: фокус внутри фрейма больше НЕ даёт валидный план с
        /// translation=0 (иначе автотриггер каждые 5с просил транзакцию и
        /// получал no_rebase_plan); остаток считается в double без
        /// frame.ToGlobal(), который бросал именно на внефреймовом фокусе.
        /// </summary>
        public static bool TryCreateRecover(LocalCoordinateFrame frame, Vector3 localFocus,
            float threshold, float quantum, out OriginRebasePlan plan)
        {
            plan = default;
            if (!frame.IsValid)
                return false;
            if (!GlobalPosition.IsFiniteValue(threshold) || threshold <= 0f || threshold > frame.MaxLocalCoordinate)
                throw new ArgumentOutOfRangeException(nameof(threshold));
            if (!GlobalPosition.IsFiniteValue(quantum) || quantum <= 0f || quantum > threshold)
                throw new ArgumentOutOfRangeException(nameof(quantum));
            if (!GlobalPosition.IsFiniteValue(localFocus.x) ||
                !GlobalPosition.IsFiniteValue(localFocus.y) ||
                !GlobalPosition.IsFiniteValue(localFocus.z))
                return false;

            double sx = Math.Round((double)localFocus.x / quantum, MidpointRounding.AwayFromZero) * quantum;
            double sy = Math.Round((double)localFocus.y / quantum, MidpointRounding.AwayFromZero) * quantum;
            double sz = Math.Round((double)localFocus.z / quantum, MidpointRounding.AwayFromZero) * quantum;
            // T-FO09H-fix2: нулевой сдвиг — не recover. Раньше внутри фрейма
            // возвращался валидный план с translation=0, автотриггер каждые 5с
            // просил транзакцию, а ExecuteTransaction честно отказывал
            // (no_rebase_plan) — бесконечный отказ вместо тишины.
            if (sx == 0d && sy == 0d && sz == 0d)
                return false;
            if (!GlobalPosition.IsFiniteValue(sx) || !GlobalPosition.IsFiniteValue(sy) || !GlobalPosition.IsFiniteValue(sz))
                return false;

            // Остаток считаем в double от того же focus: frame.ToGlobal()
            // бросает вне ContainsLocal, то есть ровно в том случае, ради
            // которого этот путь и существует.
            double rx = (double)localFocus.x - sx;
            double ry = (double)localFocus.y - sy;
            double rz = (double)localFocus.z - sz;
            if (!GlobalPosition.IsFiniteValue(rx) || !GlobalPosition.IsFiniteValue(ry) || !GlobalPosition.IsFiniteValue(rz))
                return false;
            if (Math.Abs(rx) > frame.MaxLocalCoordinate || Math.Abs(ry) > frame.MaxLocalCoordinate ||
                Math.Abs(rz) > frame.MaxLocalCoordinate)
                return false;

            var origin = new GlobalPosition(frame.Origin.X + sx, frame.Origin.Y + sy, frame.Origin.Z + sz);
            var after = frame.WithOrigin(origin);
            if (!after.IsValid)
                return false;
            // Представимость остатка в новом фрейме (та же проверка, что давал TryToLocal).
            if (!after.ContainsLocal(new Vector3((float)rx, (float)ry, (float)rz)))
                return false;

            var translation = new Vector3((float)(frame.Origin.X - origin.X),
                (float)(frame.Origin.Y - origin.Y), (float)(frame.Origin.Z - origin.Z));
            if (!GlobalPosition.IsFiniteValue(translation.x) || !GlobalPosition.IsFiniteValue(translation.y) ||
                !GlobalPosition.IsFiniteValue(translation.z))
                return false;
            if (translation.x == 0f && translation.y == 0f && translation.z == 0f)
                return false;
            plan = new OriginRebasePlan(frame, after, translation);
            return true;
        }

        /// <summary>Prefer reprojection for canonical positions rather than accumulating translations.</summary>
        public bool TryReproject(Vector3 oldLocalPosition, out Vector3 newLocalPosition)
        {
            newLocalPosition = default;
            return IsValid && Before.TryConvertTo(oldLocalPosition, After, out newLocalPosition);
        }
    }
}
