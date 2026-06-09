using UnityEngine;

namespace ProjectC.UI.Toast
{
    /// <summary>
    /// T-Q23: static API для показа toast-уведомлений. Делегирует в ToastUI.Instance.
    /// Fire-and-forget: Show("msg", ToastKind.Success).
    /// Безопасно вызывать из любого места (UI, server callback handler, gameplay).
    /// </summary>
    public static class ToastService
    {
        /// <summary>Показать toast. Если ToastUI нет в сцене — silently no-op + warning в Console.</summary>
        public static void Show(string message, ToastKind kind = ToastKind.Info)
        {
            var ui = ToastUI.GetOrFindInstance();
            if (ui == null)
            {
                Debug.LogWarning($"[ToastService] No ToastUI instance — toast dropped: '{message}'");
                return;
            }
            ui.ShowToast(message, kind);
        }

        // Удобные шорткаты
        public static void Info(string message) => Show(message, ToastKind.Info);
        public static void Success(string message) => Show(message, ToastKind.Success);
        public static void Warning(string message) => Show(message, ToastKind.Warning);
        public static void Error(string message) => Show(message, ToastKind.Error);
    }
}
