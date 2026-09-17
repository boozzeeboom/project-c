using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.World
{
    /// <summary>
    /// Мировой север — единый источник истины для всех компасов в игре
    /// (HUD корабля сегодня, пеший HUD позже — тот же API).
    ///
    /// Север задаётся объектом <see cref="CompassRose"/> в ключевой сцене
    /// (WorldScene_0_0): куда смотрит его синяя стрелка (+Z) — там север.
    /// Одна роза действует на весь мир: все WorldScene_* имеют одинаковую
    /// ориентацию сетки, отдельные розы в других сценах не нужны.
    ///
    /// 🟢 Floating Origin: сдвиг мира (GlobalMotionControlledRebaseSlice, F8) —
    /// чистая трансляция БЕЗ поворота, поэтому вектор севера и курс от сдвига
    /// не зависят. Курс считается из живых transform каждый кадр, кэшей мировых
    /// позиций нет — хук ApplyRebaseTranslation НЕ нужен.
    ///
    /// Fallback без розы в сцене: север = +Z Unity.
    /// </summary>
    public static class WorldNorth
    {
        private static readonly List<CompassRose> _roses = new List<CompassRose>();

        private static readonly string[] _cardinals =
            { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>Север по умолчанию (розы нет в сцене): +Z Unity.</summary>
        public static Vector3 DefaultNorth => Vector3.forward;

        /// <summary>Есть ли в загруженных сценах хотя бы одна роза.</summary>
        public static bool HasRose
        {
            get { EnsureResolved(); return _roses.Count > 0; }
        }

        /// <summary>Имя объекта активной розы (для HUD-диагностики), null если нет.</summary>
        public static string RoseObjectName
        {
            get { EnsureResolved(); return HasRoseDirect && _roses[0] != null ? _roses[0].name : null; }
        }

        /// <summary>
        /// Мировое направление на север (XZ, нормировано).
        /// Роза в сцене побеждает дефолт.
        /// </summary>
        public static Vector3 NorthDirection
        {
            get
            {
                EnsureResolved();
                if (HasRoseDirect && _roses[0] != null)
                {
                    Vector3 n = _roses[0].NorthDirection;
                    if (n.sqrMagnitude > 0.0001f) return n.normalized;
                }
                return DefaultNorth;
            }
        }

        /// <summary>
        /// Курс: куда смотрит нос, в градусах от севера по часовой
        /// (0 = N, 90 = E, 180 = S, 270 = W). Учитывает только yaw.
        /// </summary>
        public static float GetHeadingDegrees(Vector3 worldForward)
        {
            Vector3 flat = worldForward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return 0f;
            float a = Vector3.SignedAngle(NorthDirection, flat.normalized, Vector3.up);
            if (a < 0f) a += 360f;
            return a;
        }

        /// <summary>Курс по transform (берётся forward).</summary>
        public static float GetHeadingDegrees(Transform t) =>
            t != null ? GetHeadingDegrees(t.forward) : 0f;

        /// <summary>
        /// Подпись румба по курсу: 8 секторов (N/NE/E/SE/S/SW/W/NW),
        /// границы со сдвигом 22.5° (N: 337.5–22.5).
        /// </summary>
        public static string GetCardinalLabel(float headingDegrees)
        {
            float h = headingDegrees % 360f;
            if (h < 0f) h += 360f;
            int idx = Mathf.FloorToInt((h + 22.5f) / 45f) % 8;
            return _cardinals[idx];
        }

        internal static void Register(CompassRose rose)
        {
            if (rose == null || _roses.Contains(rose)) return;
            if (_roses.Count > 0)
                Debug.LogWarning($"[WorldNorth] Вторая роза '{rose.name}' проигнорирована — " +
                                 $"север уже задаёт '{_roses[0].name}'. " +
                                 $"Держите одну розу на весь мир (см. CompassRose).");
            else
                _roses.Add(rose);
        }

        internal static void Unregister(CompassRose rose)
        {
            _roses.Remove(rose);
        }

        // Прямая проверка без ленивого поиска (чтобы геттеры не рекурсили).
        private static bool HasRoseDirect => _roses.Count > 0;

        private static double _lastScanTime = -100.0;

        /// <summary>
        /// Ленивый fallback: если список пуст (сообщения OnEnable/OnDisable в этом
        /// контексте не стрельнули, роза подгрузилась со сценой позже и т.п.) —
        /// находим активную розу сами. Скан троттлится (раз в 2 с), кэш живёт
        /// пока роза не уйдёт через Unregister.
        /// </summary>
        private static void EnsureResolved()
        {
            // Чистим мёртвые ссылки (сцена выгружена, объект удалён).
            for (int i = _roses.Count - 1; i >= 0; i--)
                if (_roses[i] == null) _roses.RemoveAt(i);

            if (_roses.Count > 0) return;

            double now = Time.realtimeSinceStartupAsDouble;
            if (now - _lastScanTime < 2.0) return;
            _lastScanTime = now;

            var found = Object.FindObjectsByType<CompassRose>(FindObjectsSortMode.None);
            CompassRose first = null;
            int activeCount = 0;
            foreach (var r in found)
            {
                if (r == null || !r.isActiveAndEnabled) continue;
                activeCount++;
                if (first == null) first = r;
            }
            if (first != null)
            {
                _roses.Add(first);
                if (activeCount > 1)
                    Debug.LogWarning($"[WorldNorth] Найдено активных роз: {activeCount} — " +
                                     $"север задаёт '{first.name}', остальные игнорируются.");
            }
        }
    }
}
