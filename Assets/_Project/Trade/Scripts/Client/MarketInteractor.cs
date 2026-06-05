using System.Collections.Generic;
using ProjectC.Network;
using ProjectC.Player;
using ProjectC.Trade.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// Маленький helper на стороне NetworkPlayer, чтобы открывать рынок
    /// нажатием E в зоне MarketZone. Не путать с pickup'ом (E у сундука
    /// и E у рынка — приоритет у сундука, проверяется в NetworkPlayer).
    ///
    /// Также автоматически подписывается на сервер при входе в зону —
    /// сервер начинает слать snapshot'ы без необходимости что-то нажимать.
    /// </summary>
    public static class MarketInteractor
    {
        /// <summary>
        /// Вызывается из NetworkPlayer при нажатии E. Возвращает true, если
        /// E «поглощён» для открытия рынка (NetworkPlayer должен тогда не
        /// делать pickup).
        /// </summary>
        public static bool TryOpenMarket()
        {
            var zone = MarketZoneRegistry.LocalPlayerZone;
            // DIAG: всегда логируем состояние Registry при попытке открыть рынок
            Debug.Log($"[MarketInteractor] TryOpenMarket: enter — LocalPlayerZone={(zone == null ? "null" : zone.LocationId)}, Registry.All.Count={MarketZoneRegistry.All.Count}");
            if (zone == null)
            {
                // FIX: race condition — MarketZone.PollLocalPlayerZone() обновляет
                // LocalPlayerZone раз в 0.25с, а OnTriggerEnter может пропустить
                // (CharacterController + SphereCollider Trigger). Если пользователь
                // нажал E до следующего poll, ищем ближайший MarketZone напрямую.
                zone = FindNearestZone();
                if (zone == null)
                {
                    Debug.Log("[MarketInteractor] TryOpenMarket: LocalPlayerZone is null and no zone in range");
                    return false;
                }
                Debug.Log($"[MarketInteractor] TryOpenMarket: LocalPlayerZone was stale, using nearest='{zone.LocationId}'");
                // Кешируем на будущее
                MarketZoneRegistry.LocalPlayerZone = zone;
            }
            var state = MarketClientState.Instance;
            if (state == null)
            {
                Debug.LogWarning("[MarketInteractor] TryOpenMarket: MarketClientState.Instance is null");
                return false;
            }
            Debug.Log($"[MarketInteractor] TryOpenMarket: zone='{zone.LocationId}'");
            state.RequestSubscribeMarket(zone.LocationId);

            // C2-refactor: подписка на контракты для этой же локации (таб КОНТРАКТЫ внутри MarketWindow).
            // Контракты живут в отдельном singleton-проекции ContractClientState.
            // Без этого таб "КОНТРАКТЫ" будет пустой (CurrentSnapshot == null).
            var contractState = ProjectC.Trade.Client.ContractClientState.Instance;
            if (contractState != null) contractState.RequestList(zone.LocationId);
            else Debug.LogWarning("[MarketInteractor] TryOpenMarket: ContractClientState.Instance is null (контракты не будут загружены)");

            // FIX: подписка на снапшот — это полдела. UI ещё надо ПОКАЗАТЬ.
            // Раньше это делал MarketWindow.Update по E — но он не проверял зону,
            // и окно открывалось даже когда игрок вне маркета. Теперь MarketWindow
            // слушает только Esc (закрыть), а открытие идёт строго отсюда, в зоне.
            var window = MarketWindow.Instance;
            if (window != null) window.Show();
            else Debug.LogWarning("[MarketInteractor] TryOpenMarket: MarketWindow.Instance is null (no [MarketWindow] GO in scene?)");

            return true;
        }

        private static MarketZone FindNearestZone()
        {
            var localPlayer = FindLocalPlayer();
            if (localPlayer == null)
            {
                // DIAG: почему не нашли local player?
                var allPlayers = Object.FindObjectsByType<ProjectC.Player.NetworkPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int total = allPlayers.Length;
                int owners = 0, spawned = 0;
                for (int i = 0; i < total; i++)
                {
                    if (allPlayers[i] == null) continue;
                    if (allPlayers[i].IsSpawned) spawned++;
                    if (allPlayers[i].IsOwner) owners++;
                }
                Debug.LogWarning($"[MarketInteractor] FindNearestZone: FindLocalPlayer=null (total NetworkPlayers={total}, IsSpawned={spawned}, IsOwner={owners})");
                return null;
            }
            var lpPos = localPlayer.GetEffectivePosition();

            MarketZone best = null;
            float bestSqr = float.MaxValue;
            // DIAG: пройдём ВСЕ зоны и залогируем дистанции к каждой
            var diag = new System.Text.StringBuilder();
            diag.Append($"[MarketInteractor] FindNearestZone: localPlayerPos={lpPos}, zones={MarketZoneRegistry.All.Count} — ");
            foreach (var kv in MarketZoneRegistry.All)
            {
                var z = kv.Value;
                if (z == null) { diag.Append($"{kv.Key}=NULL "); continue; }
                float r = z.TradeRadius;
                float dSqr = (z.transform.position - lpPos).sqrMagnitude;
                float d = Mathf.Sqrt(dSqr);
                diag.Append($"{kv.Key}(d={d:F1}/r={r:F1}@{z.transform.position}) ");
                if (dSqr <= r * r && dSqr < bestSqr)
                {
                    best = z;
                    bestSqr = dSqr;
                }
            }
            diag.Append($"=> best={(best == null ? "null" : best.LocationId)}");
            Debug.Log(diag.ToString());
            return best;
        }

        private static ProjectC.Player.NetworkPlayer FindLocalPlayer()
        {
            var players = Object.FindObjectsByType<ProjectC.Player.NetworkPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || !players[i].IsOwner) continue;
                // FIX (2026-06-04): skip scene-placed `PlayerSpawner` ghost.
                // NGO 2.x на хосте даёт OwnerClientId=0 (server-owned) и scene-placed
                // NetworkObject'ам — на таком GO `IsOwner==true` (footgun). Ghost сидит
                // в точке спавна (~80 000 ед. от любой MarketZone) → GetEffectivePosition()
                // возвращает спавн → dist=128..171м → рынок «не в зоне» → окно не открывается,
                // хотя реальный NetworkPlayer(Clone) уже внутри tradeRadius.
                // Дискриминатор: наличие `NetworkPlayerSpawner` маркера на GameObject
                // (см. NetworkPlayer.OnNetworkSpawn). Реальный player из PlayerPrefab
                // этого компонента НЕ имеет.
                if (players[i].GetComponent<NetworkPlayerSpawner>() != null) continue;
                return players[i];
            }
            return null;
        }

        /// <summary>
        /// Вызывается из NetworkPlayer при входе в триггер MarketZone
        /// (если хочется «жадно» подписываться, без нажатия E).
        /// </summary>
        public static void AutoSubscribeIfInZone()
        {
            var zone = MarketZoneRegistry.LocalPlayerZone;
            if (zone == null) return;
            var state = MarketClientState.Instance;
            if (state == null) return;
            state.RequestSubscribeMarket(zone.LocationId);
        }
    }
}
