// T-CARGO-NPC-01: NpcCargoService — server-only helper для NpcShipController.
// Реализует 2 фазы dwell (D31): Unload (cargo → market.stock) + Load (market.stock → cargo).
// В randomTradeItems-режиме список buyItems не используется: рынок выбирает товары случайно до лимитов cargo.
// Вызывается синхронно из NavTick.Docked (между Docked и Undocking).
//
// Pattern: MarketTrader (Trade/Core/MarketTrader.cs) — server-only trade automation.
// D28: cargo NPC = TradeWorld._cargoCache[npcShipId], D33: TradeWorld.TryNpcBuy/TryNpcSell.
//
// Convention: один class = один .cs файл (Unity 6: T-DOCK-13c fix).

using System.Collections.Generic;
using System.Text;
using ProjectC.PeacefulShip.Core; // NpcCargoTradeListConfig, NpcShipCargoManifest, NpcShipCargoManifest, NpcCargoEntryDto
using ProjectC.Trade.Core;        // TradeWorld, TradeItemDefinitionResolver, WarehouseEntry, ShipClass
using UnityEngine;

namespace ProjectC.PeacefulShip.Network
{
    /// <summary>
    /// Server-only singleton: helper для NpcShipController, выполняет
    /// unload/load фазы dwell (D31) через TradeWorld.TryNpcSell/TryNpcBuy.
    /// Создаётся в NpcShipServer.OnNetworkSpawn.
    /// </summary>
    public class NpcCargoService : MonoBehaviour
    {
        public static NpcCargoService Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // ============================================================
        // LIFECYCLE
        // ============================================================

        /// <summary>
        /// Создаётся в NpcShipServer.OnNetworkSpawn (рядом с NpcShipWorld).
        /// </summary>
        public static void CreateAndInitialize()
        {
            if (Instance != null) return;
            var go = new GameObject("[NpcCargoService]");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<NpcCargoService>();
            Debug.Log("[NpcCargoService] Created");
        }

        public static void Shutdown()
        {
            if (Instance != null) Object.Destroy(Instance.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================================================
        // PUBLIC API (server-only, вызывается из NpcShipController.NavTick)
        // ============================================================

        /// <summary>
        /// Отчёт по результатам dwell-trade (для логов и UI).
        /// sold: список (itemId, qty) проданного.
        /// bought: список (itemId, qty, requestedQty) купленного.
        /// skipReasons: список строк с причинами отказа (stock=0, price=0, и т.д.).
        /// </summary>
        public struct DwellTradeReport
        {
            public List<(string itemId, int qty)> sold;
            public List<(string itemId, int qty, int requested)> bought;
            public List<string> skipReasons;

            public bool HasAnyActivity => (sold != null && sold.Count > 0) || (bought != null && bought.Count > 0);
        }

        /// <summary>
        /// Выполнить полный dwell-trade для NPC-корабля на станции locationId.
        /// 1) Unload: cargo → market.stock (если schedule.cargoTrade.sellAllOnArrival).
        /// 2) Load: market.stock → cargo по buyItems или случайному ассортименту рынка.
        /// Returns: DwellTradeReport (для логов NpcShipController).
        /// </summary>
        public DwellTradeReport RunDwellTrade(
            ulong npcInstanceId,
            ulong shipNetworkObjectId,
            ShipClass shipClass,
            string locationId,
            NpcCargoTradeListConfig trade)
        {
            var report = new DwellTradeReport
            {
                sold = new List<(string, int)>(),
                bought = new List<(string, int, int)>(),
                skipReasons = new List<string>()
            };

            if (trade == null) return report; // NPC без cargo trade — no-op (backward compat M3.2)
            if (string.IsNullOrEmpty(locationId))
            {
                report.skipReasons.Add("empty locationId");
                return report;
            }

            var tw = TradeWorld.Instance;
            if (tw == null)
            {
                report.skipReasons.Add("TradeWorld.Instance==null (MarketServer not spawned?)");
                return report;
            }

            // ----- Phase 1: Unload (cargo → market.stock) -----
            if (trade.sellAllOnArrival || trade.randomTradeItems)
            {
                var cargo = tw.GetOrLoadCargo(shipNetworkObjectId, shipClass);
                if (cargo == null)
                {
                    report.skipReasons.Add("unload: cargo null");
                }
                else
                {
                    // Снимем копию списка, т.к. TryRemove мутирует Items
                    var items = cargo.Items;
                    var snapshot = new List<WarehouseEntry>(items.Count);
                    for (int i = 0; i < items.Count; i++) snapshot.Add(items[i]);

                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        var entry = snapshot[i];
                        if (string.IsNullOrEmpty(entry.itemId) || entry.quantity <= 0) continue;

                        // Уважаем maxKeepQuantity: не продаём больше (entry.quantity - maxKeep).
                        int sellQty = entry.quantity;
                        if (!trade.randomTradeItems && trade.buyItems != null)
                        {
                            for (int b = 0; b < trade.buyItems.Length; b++)
                            {
                                var bi = trade.buyItems[b];
                                string configuredItemId = bi.GetResolvedItemId();
                                if (configuredItemId == entry.itemId && bi.sellOnArrival)
                                {
                                    sellQty = Mathf.Max(0, entry.quantity - Mathf.Max(0, bi.maxKeepQuantity));
                                    break;
                                }
                            }
                        }
                        if (sellQty <= 0) continue;

                        var r = tw.TryNpcSell(npcInstanceId, locationId, entry.itemId, sellQty,
                                               shipNetworkObjectId, shipClass, trade.useUnlimitedCredits);
                        if (r.IsSuccess)
                        {
                            report.sold.Add((entry.itemId, sellQty));
                        }
                        else
                        {
                            report.skipReasons.Add($"unload {entry.itemId} qty={sellQty} → {r.code} ({r.message})");
                        }
                    }
                }
            }

            // ----- Phase 2: Load (market.stock → cargo) -----
            if (trade.randomTradeItems)
            {
                LoadRandomItems(npcInstanceId, shipNetworkObjectId, shipClass, locationId, trade, tw, ref report);
            }
            else if (trade.buyConfiguredItemsAfterSell && trade.buyItems != null && trade.buyItems.Length > 0)
            {
                // Стоп-краны по слотам/весу из конфига
                int slotsLeft = trade.maxLoadSlots;
                float weightLeftKg = trade.maxLoadWeightKg;

                // Получаем cargo ОДИН раз (после unload cargo изменилась)
                var cargo = tw.GetOrLoadCargo(shipNetworkObjectId, shipClass);
                if (cargo != null && tw.Resolver != null)
                {
                    slotsLeft = Mathf.Max(0, slotsLeft - cargo.ComputeTotalSlots(tw.Resolver));
                    weightLeftKg = Mathf.Max(0f, weightLeftKg - cargo.ComputeTotalWeight(tw.Resolver));
                }

                for (int i = 0; i < trade.buyItems.Length; i++)
                {
                    var bi = trade.buyItems[i];
                    string itemId = bi.GetResolvedItemId();
                    if (string.IsNullOrEmpty(itemId) || bi.desiredQuantity <= 0) continue;

                    // Предвычислим сколько реально влезет по слотам/весу одной единицы
                    if (tw.Resolver != null)
                    {
                        int itemSlots = tw.Resolver.GetSlots(itemId);
                        float itemWeight = tw.Resolver.GetWeight(itemId);
                        if (itemSlots > 0)
                        {
                            int maxBySlots = slotsLeft / itemSlots;
                            bi.desiredQuantity = Mathf.Min(bi.desiredQuantity, maxBySlots);
                        }
                        if (itemWeight > 0f)
                        {
                            int maxByWeight = Mathf.FloorToInt(weightLeftKg / itemWeight);
                            bi.desiredQuantity = Mathf.Min(bi.desiredQuantity, maxByWeight);
                        }
                    }
                    if (bi.desiredQuantity <= 0)
                    {
                        report.skipReasons.Add($"load {itemId} → no capacity (slots={slotsLeft}, weight={weightLeftKg:F1}kg)");
                        continue;
                    }

                    var r = tw.TryNpcBuy(npcInstanceId, locationId, itemId, bi.desiredQuantity,
                                         shipNetworkObjectId, shipClass, trade.useUnlimitedCredits);
                    if (r.IsSuccess)
                    {
                        report.bought.Add((itemId, bi.desiredQuantity, bi.desiredQuantity));
                        // Обновим слоты/вес
                        if (tw.Resolver != null && cargo != null)
                        {
                            slotsLeft = Mathf.Max(0, trade.maxLoadSlots - cargo.ComputeTotalSlots(tw.Resolver));
                            weightLeftKg = Mathf.Max(0f, trade.maxLoadWeightKg - cargo.ComputeTotalWeight(tw.Resolver));
                        }
                    }
                    else
                    {
                        // Можем частично купить если рынок дал меньше (TryNpcBuy не делает partial).
                        // D33: TryNpcBuy атомарен — нельзя частично. Логируем причину, идём дальше.
                        report.skipReasons.Add($"load {itemId} qty={bi.desiredQuantity} → {r.code} ({r.message})");
                    }
                }
            }

            if (debugMode)
            {
                var sb = new StringBuilder();
                sb.Append($"[NpcCargoService] DwellTrade npc={npcInstanceId:X} loc={locationId} ship={shipNetworkObjectId}: ");
                if (report.sold.Count > 0)
                {
                    sb.Append("SOLD[");
                    for (int i = 0; i < report.sold.Count; i++)
                        sb.Append($"{report.sold[i].itemId}={report.sold[i].qty},");
                    sb.Append("] ");
                }
                if (report.bought.Count > 0)
                {
                    sb.Append("BOUGHT[");
                    for (int i = 0; i < report.bought.Count; i++)
                        sb.Append($"{report.bought[i].itemId}={report.bought[i].qty},");
                    sb.Append("]");
                }
                if (report.skipReasons.Count > 0)
                {
                    sb.Append(" SKIP[").AppendJoin(",", report.skipReasons).Append("]");
                }
                // T-CARGO-NPC-01 fix #5 (2026-07-03): emit ЛОГ ВСЕГДА, даже если активности не было.
                // Без этого при пустом отчёте (early return) юзер не видит причину.
                if (!report.HasAnyActivity)
                    sb.Append(" (no activity)");
                Debug.Log(sb.ToString());
            }

            return report;
        }

        /// <summary>
        /// В randomTradeItems-режиме случайно выбирает позиции из текущего рынка
        /// и покупает их максимально возможными партиями до заполнения лимитов cargo.
        /// Buy Items в этом режиме не используется.
        /// </summary>
        private void LoadRandomItems(
            ulong npcInstanceId,
            ulong shipNetworkObjectId,
            ShipClass shipClass,
            string locationId,
            NpcCargoTradeListConfig trade,
            TradeWorld tw,
            ref DwellTradeReport report)
        {
            var market = tw.GetMarket(locationId);
            if (market == null)
            {
                report.skipReasons.Add($"random load: market '{locationId}' not found");
                return;
            }

            var cargo = tw.GetOrLoadCargo(shipNetworkObjectId, shipClass);
            if (cargo == null || tw.Resolver == null)
            {
                report.skipReasons.Add("random load: cargo or resolver is null");
                return;
            }

            int slotsLeft = Mathf.Max(0, trade.maxLoadSlots - cargo.ComputeTotalSlots(tw.Resolver));
            float weightLeftKg = Mathf.Max(0f, trade.maxLoadWeightKg - cargo.ComputeTotalWeight(tw.Resolver));
            if (slotsLeft <= 0 || weightLeftKg <= 0f)
            {
                report.skipReasons.Add($"random load: no capacity (slots={slotsLeft}, weight={weightLeftKg:F1}kg)");
                return;
            }

            var candidates = new List<MarketItemState>();
            foreach (var kv in market.Items)
            {
                var item = kv.Value;
                if (item == null || item.config == null || item.availableStock <= 0)
                    continue;
                if (!item.config.allowBuy || string.IsNullOrEmpty(item.ItemId))
                    continue;
                if (!tw.Resolver.TryGet(item.ItemId, out var definition) || definition == null)
                    continue;

                item.RecalculatePrice(market.PriceFloorRatio, market.PriceCeilingRatio);
                if (item.currentPrice <= 0f)
                    continue;

                candidates.Add(item);
            }

            if (candidates.Count == 0)
            {
                report.skipReasons.Add($"random load: no buyable stocked items at '{locationId}'");
                return;
            }

            while (candidates.Count > 0 && slotsLeft > 0 && weightLeftKg > 0f)
            {
                int candidateIndex = Random.Range(0, candidates.Count);
                var candidate = candidates[candidateIndex];
                string itemId = candidate.ItemId;
                int requestedQuantity = CalculateMaxBuyQuantity(
                    itemId,
                    candidate.availableStock,
                    slotsLeft,
                    weightLeftKg,
                    tw.Resolver);

                if (requestedQuantity <= 0)
                {
                    candidates.RemoveAt(candidateIndex);
                    continue;
                }

                int boughtQuantity;
                TradeResult failedResult;
                if (!TryNpcBuyBestEffort(
                        tw,
                        npcInstanceId,
                        locationId,
                        itemId,
                        requestedQuantity,
                        shipNetworkObjectId,
                        shipClass,
                        trade.useUnlimitedCredits,
                        out boughtQuantity,
                        out failedResult))
                {
                    report.skipReasons.Add($"random load {itemId} qty={requestedQuantity} → {failedResult.code} ({failedResult.message})");
                    candidates.RemoveAt(candidateIndex);
                    continue;
                }

                report.bought.Add((itemId, boughtQuantity, requestedQuantity));
                slotsLeft = Mathf.Max(0, trade.maxLoadSlots - cargo.ComputeTotalSlots(tw.Resolver));
                weightLeftKg = Mathf.Max(0f, trade.maxLoadWeightKg - cargo.ComputeTotalWeight(tw.Resolver));

                int itemSlots = tw.Resolver.GetSlots(itemId);
                float itemWeight = tw.Resolver.GetWeight(itemId);
                if (boughtQuantity >= requestedQuantity || (itemSlots <= 0 && itemWeight <= 0f))
                    candidates.RemoveAt(candidateIndex);
            }
        }

        private static int CalculateMaxBuyQuantity(
            string itemId,
            int availableStock,
            int slotsLeft,
            float weightLeftKg,
            TradeItemDefinitionResolver resolver)
        {
            if (resolver == null || string.IsNullOrEmpty(itemId) || availableStock <= 0)
                return 0;
            if (slotsLeft <= 0 || weightLeftKg <= 0f)
                return 0;

            int maxQuantity = availableStock;
            int itemSlots = resolver.GetSlots(itemId);
            float itemWeight = resolver.GetWeight(itemId);

            if (itemSlots > 0)
                maxQuantity = Mathf.Min(maxQuantity, slotsLeft / itemSlots);
            if (itemWeight > 0f)
                maxQuantity = Mathf.Min(maxQuantity, Mathf.FloorToInt(weightLeftKg / itemWeight));

            return Mathf.Max(0, maxQuantity);
        }

        private static bool TryNpcBuyBestEffort(
            TradeWorld tw,
            ulong npcInstanceId,
            string locationId,
            string itemId,
            int requestedQuantity,
            ulong shipNetworkObjectId,
            ShipClass shipClass,
            bool useUnlimitedCredits,
            out int boughtQuantity,
            out TradeResult lastResult)
        {
            boughtQuantity = 0;
            lastResult = default;

            int attemptQuantity = requestedQuantity;
            while (attemptQuantity > 0)
            {
                lastResult = tw.TryNpcBuy(
                    npcInstanceId,
                    locationId,
                    itemId,
                    attemptQuantity,
                    shipNetworkObjectId,
                    shipClass,
                    useUnlimitedCredits);

                if (lastResult.IsSuccess)
                {
                    boughtQuantity = attemptQuantity;
                    return true;
                }

                if (attemptQuantity == 1)
                    break;

                attemptQuantity = Mathf.Max(1, attemptQuantity / 2);
            }

            return false;
        }

        /// <summary>
        /// Читает текущий cargo NPC-корабля и заполняет NpcShipCargoManifest.
        /// Используется для UI/дебага/логирования. D32.
        /// </summary>
        public NpcShipCargoManifest BuildManifest(
            ulong shipNetworkObjectId,
            ShipClass shipClass,
            int capacitySlots,
            float capacityWeight)
        {
            var manifest = new NpcShipCargoManifest
            {
                capacitySlots = capacitySlots,
                capacityWeight = capacityWeight,
                items = null
            };

            var tw = TradeWorld.Instance;
            if (tw == null) return manifest;

            var cargo = tw.GetOrLoadCargo(shipNetworkObjectId, shipClass);
            if (cargo == null || cargo.Items.Count == 0) return manifest;

            // Заполним items + unitPrice (из market, если NPC стоит на станции — best-effort, без locationId)
            // Без locationId мы не знаем где NPC — оставим unitPrice=0 (для UI: см. комментарий в DTO).
            var entries = cargo.Items;
            manifest.items = new NpcCargoEntryDto[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                manifest.items[i] = new NpcCargoEntryDto
                {
                    itemId = entries[i].itemId,
                    quantity = entries[i].quantity,
                    unitPrice = 0f
                };
            }
            return manifest;
        }
    }
}
