using UnityEngine;

namespace ProjectC.Admin
{
    /// <summary>
    /// T-ADM-04 fix: автосоздание админ-слоя при старте рантайма.
    /// BootstrapScene НЕ трогаем (AGENTS.md) — вместо этого RuntimeInitializeOnLoadMethod,
    /// который поднимает AdminFacade + AdminRuntimeWindow (скрыто) на DontDestroyOnLoad-объектах.
    /// Без этого F12 некому читать: ни фасад, ни окно нигде в сценах не лежат.
    /// </summary>
    public static class AdminBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var facade = AdminFacade.EnsureExists();
            var window = AdminRuntimeWindow.EnsureExists();
            if (facade == null || window == null)
                Debug.LogWarning("[AdminBootstrap] Failed to create admin layer");
            else
                Debug.Log("[AdminBootstrap] Admin layer ready (F12)");
        }
    }
}
