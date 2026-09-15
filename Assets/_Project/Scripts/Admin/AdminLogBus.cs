using System;
using UnityEngine;

namespace ProjectC.Admin
{
    /// <summary>
    /// T-ADM-02: центральный рубильник отладочных логов (D4).
    /// Существующие per-system bool НЕ удаляются — шина только ставит их
    /// через SetDebug*-сеттеры (T-ADM-03) из AdminFacade.SetLogsMuted.
    /// Событие OnMutedChanged — задел для подписок из нового кода.
    /// </summary>
    public static class AdminLogBus
    {
        public static bool Muted { get; private set; }

        public static event Action<bool> OnMutedChanged;

        public static void SetMuted(bool muted)
        {
            if (Muted == muted) return;
            Muted = muted;
            try { OnMutedChanged?.Invoke(muted); }
            catch (Exception ex) { Debug.LogWarning($"[AdminLogBus] OnMutedChanged failed: {ex.Message}"); }
            Debug.Log($"[AdminLogBus] Debug logs {(muted ? "MUTED" : "UNMUTED")}");
        }
    }
}
