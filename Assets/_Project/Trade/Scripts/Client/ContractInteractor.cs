using ProjectC.Network;
using ProjectC.Player;
using ProjectC.Trade.Network;
using UnityEngine;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// Маленький helper на стороне NetworkPlayer, чтобы открывать доску контрактов
    /// нажатием C (или другого key) у NPC-агента НП. Не путать с E (рынок) и
    /// F (смена режима). Приоритет: E (рынок) → C (контракты) → F (смена режима).
    ///
    /// Также автоматически подписывается на сервер при входе в зону — сервер
    /// начинает слать snapshot'ы без необходимости что-то нажимать.
    ///
    /// C2-этап миграции контрактов на v2-архитектуру.
    /// </summary>
    public static class ContractInteractor
    {
        /// <summary>
        /// Вызывается из NetworkPlayer при нажатии C. Возвращает true, если
        /// C «поглощён» для открытия контрактов (NetworkPlayer должен тогда не
        /// делать другие действия).
        /// </summary>
        public static bool TryOpenContractBoard()
        {
            var zone = ContractZoneRegistry.LocalPlayerZone;
            if (zone == null)
            {
                // Race condition fallback — ищем ближайшую зону
                zone = FindNearestZone();
                if (zone == null)
                {
                    Debug.Log("[ContractInteractor] TryOpenContractBoard: no zone in range");
                    return false;
                }
                ContractZoneRegistry.LocalPlayerZone = zone;
            }
            var state = ContractClientState.Instance;
            if (state == null)
            {
                Debug.LogWarning("[ContractInteractor] TryOpenContractBoard: ContractClientState.Instance is null");
                return false;
            }
            Debug.Log($"[ContractInteractor] TryOpenContractBoard: zone='{zone.LocationId}'");
            state.RequestList(zone.LocationId);

            // Открываем UI Toolkit окно (если есть в сцене)
            var window = ContractBoardWindow.Instance;
            if (window != null) window.Show();
            else Debug.LogWarning("[ContractInteractor] TryOpenContractBoard: ContractBoardWindow.Instance is null (no [ContractBoardWindow] GO in scene?)");

            return true;
        }

        private static ContractZone FindNearestZone()
        {
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null) return null;
            var lpPos = localPlayer.GetEffectivePosition();

            ContractZone best = null;
            float bestSqr = float.MaxValue;
            foreach (var kv in ContractZoneRegistry.All)
            {
                var z = kv.Value;
                if (z == null) continue;
                float r = z.TradeRadius;
                float dSqr = (z.transform.position - lpPos).sqrMagnitude;
                if (dSqr <= r * r && dSqr < bestSqr)
                {
                    best = z;
                    bestSqr = dSqr;
                }
            }
            return best;
        }

        private static ProjectC.Player.NetworkPlayer FindLocalPlayer()
        {
            var players = Object.FindObjectsByType<ProjectC.Player.NetworkPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || !players[i].IsOwner) continue;
                if (players[i].GetComponent<NetworkPlayerSpawner>() != null) continue;
                return players[i];
            }
            return null;
        }

        /// <summary>Вызывается из NetworkPlayer при входе в триггер ContractZone (если хочется «жадно» подписываться, без нажатия).</summary>
        public static void AutoSubscribeIfInZone()
        {
            var zone = ContractZoneRegistry.LocalPlayerZone;
            if (zone == null) return;
            var state = ContractClientState.Instance;
            if (state == null) return;
            state.RequestList(zone.LocationId);
        }
    }
}
