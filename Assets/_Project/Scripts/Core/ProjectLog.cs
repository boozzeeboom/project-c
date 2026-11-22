// ProjectC: единый мастер-выключатель info-логов — T-PERF05/F
// Design/замер: docs/world/optimization/22112026/03_FO_NULL_COMPONENT_AND_CONSOLE.md
using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Мастер-выключатель «информационных» логов проекта.
    ///
    /// Зачем. Замер F (`ProjectC_client_2026-11-22_11-57-08.data`): 557 из 1 063 записей консоли —
    /// наши info-логи. `LogStringToConsole` за сессию 21 247 KB, из них 10 295 KB — нативная цена
    /// 20 регистраций палуб (`NavMeshManager.NotifyNavMeshAdded`), т.е. ~10.9 MB (17% всего GC)
    /// приходится на строки вида «5–6 сообщений на корабль при спавне» (23 корабля ⇒ 23 повтора).
    ///
    /// Как пользоваться:
    ///   в горячих местах — guard, чтобы строка не строилась вовсе:
    ///     if (ProjectLog.Verbose) Debug.Log($"…");
    ///   в холодных — helper:
    ///     ProjectLog.Info($"…");
    ///
    /// Что НЕ глушится: `Debug.LogWarning` / `Debug.LogError`. Реальные недоработки данных
    /// (например `[ShipCargoVisual] _boxPrefabs array is empty`, `[EngineThrusterVisual]` плечо
    /// pivot→visuals) остаются в консоли.
    ///
    /// Локальные флаги компонентов (`_debugLog`, `verboseLog`) сохраняют свой смысл — «детально по
    /// этой системе» — и работают вместе с мастер-флагом: `if (_debugLog &amp;&amp; ProjectLog.Verbose)`.
    /// </summary>
    public static class ProjectLog
    {
        /// <summary>
        /// Мастер-выключатель info-логов. По умолчанию выключен — и в Editor, и в билде
        /// (в билде эти же строки пишутся в Player.log, т.е. это тоже цена).
        /// Включается из кода (меню/тест/консоль), без правки ассетов и сцены.
        /// </summary>
        public static bool Verbose;

        /// <summary>
        /// Без domain reload статика живёт между сессиями Play Mode — не тащим включённое состояние
        /// из прошлого прогона.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Verbose = false;

        /// <summary>Info-лог под мастер-флагом (для мест, где строка дешёвая).</summary>
        public static void Info(string message)
        {
            if (Verbose) Debug.Log(message);
        }

        /// <summary>Info-лог под мастер-флагом с контекстом объекта.</summary>
        public static void Info(string message, Object context)
        {
            if (Verbose) Debug.Log(message, context);
        }
    }
}
