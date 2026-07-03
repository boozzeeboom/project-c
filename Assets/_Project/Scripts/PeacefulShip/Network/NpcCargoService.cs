// T-CARGO-NPC-01: NpcCargoService — server-only helper для NpcShipController.
// Реализует 2 фазы dwell (D31): Unload (cargo → market.stock) + Load (market.stock → cargo).
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
        /// 2) Load: market.stock → cargo по schedule.cargoTrade.buyItems (если buyConfiguredItemsAfterSell).
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
            if (trade.sellAllOnArrival)
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
                        if (trade.buyItems != null)
                        {
                            for (int b = 0; b < trade.buyItems.Length; b++)
                            {
                                var bi = trade.buyItems[b];
                                if (bi.itemId == entry.itemId && bi.sellOnArrival)
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
            if (trade.buyConfiguredItemsAfterSell && trade.buyItems != null && trade.buyItems.Length > 0)
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
                    if (string.IsNullOrEmpty(bi.itemId) || bi.desiredQuantity <= 0) continue;

                    // Предвычислим сколько реально влезет по слотам/весу одной единицы
                    if (tw.Resolver != null)
                    {
                        int itemSlots = tw.Resolver.GetSlots(bi.itemId);
                        float itemWeight = tw.Resolver.GetWeight(bi.itemId);
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
                        report.skipReasons.Add($"load {bi.itemId} → no capacity (slots={slotsLeft}, weight={weightLeftKg:F1}kg)");
                        continue;
                    }

                    var r = tw.TryNpcBuy(npcInstanceId, locationId, bi.itemId, bi.desiredQuantity,
                                         shipNetworkObjectId, shipClass, trade.useUnlimitedCredits);
                    if (r.IsSuccess)
                    {
                        report.bought.Add((bi.itemId, bi.desiredQuantity, bi.desiredQuantity));
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
                        report.skipReasons.Add($"load {bi.itemId} qty={bi.desiredQuantity} → {r.code} ({r.message})");
                    }
                }
            }

            if (debugMode && report.HasAnyActivity)
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
                Debug.Log(sb.ToString());
            }

            return report;
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
