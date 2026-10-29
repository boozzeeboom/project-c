// Cloudy Navigator — шаг 7 плана переноса (docs/UI/full rebuild plans/Cloudy_redesign/04_PORT_RULES_AND_PLAN.md §4).
//
// ЕДИНСТВЕННОЕ место порта, откуда вызываются игровые Request*-методы. Список действий — ровно таблица
// 02_INTEGRATION_ARCHITECTURE.md §7, ни строкой больше:
//   следить/снять квест       → QuestTracker.GetOrFindInstance().Toggle(questId)   (локально, как легаси)
//   принять квест             → QuestClientState.RequestAcceptQuest(questId, "")   (как кнопка легаси, fromNpcId = "")
//   отказаться от квеста      → НЕ вызывается: сервера отказа нет, только честное сообщение Loc
//   сдать квест               → НЕ вызывается: сдача идёт через диалог с NPC (02 §7)
//   контракты                 → ContractClientState.RequestAccept / Complete / Fail / ReceiveCargo
//   багаж ↔ трюм              → ShipCargoServer.RequestStoreToCargoRpc / RequestRetrieveFromCargoRpc,
//                               только в триггер-зоне обменника (рынок либо консоль трюма корабля) и только
//                               кратными курсом упаковки порциями (ExchangeRateEntry.inventoryQty / warehouseQty)
//
// Окно ничего не выдумывает: вне зоны обменника перенос не выполняется и возвращается причина
// «подойди к обменнику» (решение пользователя №2), а количество и результат пишутся теми числами,
// которые получились из реального курса.
//
// Ни один файл Assets/_Project не правится: используются только публичные члены существующих систем.
using ProjectC.Core;
using ProjectC.Items.Client;
using ProjectC.Localization;
using ProjectC.Quests.Client;
using ProjectC.Quests.UI;
using ProjectC.Trade;
using ProjectC.Trade.Client;
using ProjectC.Trade.Config;
using ProjectC.Trade.Core;
using ProjectC.Trade.Network;
using Unity.Netcode;
using UnityEngine;

namespace ProjectCCloudyNavigator.Actions
{
    /// <summary>Результат действия: ok и сообщение для строки состояния окна.</summary>
    public struct CnActionResult
    {
        public bool ok;
        public string message;

        public static CnActionResult Ok(string message) => new CnActionResult { ok = true, message = message };
        public static CnActionResult Fail(string message) => new CnActionResult { ok = false, message = message };
    }

    /// <summary>
    /// Триггер-зона обменника. Разрешён ровно один из двух входов: зона рынка (MarketZoneRegistry) либо
    /// консоль трюма корабля (InteractableManager). Вне зоны allowed = false и label = «подойди к обменнику».
    /// </summary>
    public struct CnExchangeZone
    {
        public bool allowed;
        public bool isMarket;
        public string label;
        public string marketLocationId;
        public ulong consoleShipNetworkObjectId;
    }

    /// <summary>
    /// Действия окна. Состояния не держит (кроме кэша зоны для подписи): каждый вызов читает игру заново,
    /// поэтому после нажатия и после перемещения игрока показывается актуальный результат.
    /// </summary>
    public sealed class CnActions
    {
        /// <summary>Шаг опроса зоны для подписи в доке; сами действия зону перечитывают всегда.</summary>
        private const float ZonePollInterval = 0.2f;

        /// <summary>Тот же радиус, что NetworkPlayer.pickupRange: по нему игра ищет консоль трюма.</summary>
        private const float ConsoleRange = 3f;

        private float _zoneTimer;

        /// <summary>Последняя прочитанная зона — только для подписи; действия зовут <see cref="ReadZone"/> сами.</summary>
        public CnExchangeZone Zone { get; private set; }

        public CnActions()
        {
            Zone = ReadZone();
            _zoneTimer = ZonePollInterval;
        }

        public void Tick(float dt)
        {
            _zoneTimer -= dt;
            if (_zoneTimer > 0f) return;
            _zoneTimer = ZonePollInterval;
            Zone = ReadZone();
        }

        // ------------------------------------------------------------------ зона обменника

        /// <summary>
        /// Свежая проверка зоны. Рынок — MarketZoneRegistry.LocalPlayerZone (ставится триггером рынка);
        /// консоль трюма — консоль, зарегистрированная в InteractableManager (регистрация идёт в OnTriggerEnter,
        /// поэтому найденная консоль и означает «игрок в её триггере»).
        /// </summary>
        public CnExchangeZone ReadZone()
        {
            var market = MarketZoneRegistry.LocalPlayerZone;
            if (market != null)
            {
                string marketName = string.IsNullOrWhiteSpace(market.DisplayName) ? market.LocationId : market.DisplayName;
                return new CnExchangeZone
                {
                    allowed = true,
                    isMarket = true,
                    marketLocationId = market.LocationId,
                    label = Messages.MarketPrefix + ": " + marketName
                };
            }

            var nm = NetworkManager.Singleton;
            var player = nm != null ? nm.LocalClient?.PlayerObject : null;
            if (player != null)
            {
                var console = InteractableManager.FindNearestShipCargoConsole(player.transform.position, ConsoleRange);
                var ship = console != null ? console.Ship : null;
                if (ship != null && ship.IsSpawned)
                {
                    return new CnExchangeZone
                    {
                        allowed = true,
                        isMarket = false,
                        consoleShipNetworkObjectId = ship.NetworkObjectId,
                        label = Messages.ConsolePrefix + ": " + console.DisplayName
                    };
                }
            }

            return new CnExchangeZone { allowed = false, label = Messages.Approach };
        }

        // ------------------------------------------------------------------ квесты

        /// <summary>Следить / снять. Локальное действие трекера — ровно то, что делает легаси.</summary>
        public CnActionResult ToggleQuest(string questId, string displayName)
        {
            if (string.IsNullOrEmpty(questId)) return CnActionResult.Fail(Messages.NoQuest);

            var tracker = QuestTracker.GetOrFindInstance();
            if (tracker == null)
                return CnActionResult.Fail(Loc.Get("ui.character.questtracker_unavailable", Messages.TrackerUnavailable));

            tracker.Toggle(questId);
            return CnActionResult.Ok(tracker.TrackedQuestId == questId
                ? Messages.Tracking + ": " + displayName
                : Messages.Untracked + ": " + displayName);
        }

        /// <summary>
        /// Принять квест. fromNpcId пустой — как в легаси CharacterWindow (OnAcceptQuestClicked/OnAcceptQuest):
        /// окно не знает, какой NPC предложил квест (в QuestProgressDto этого поля нет), и не придумывает его.
        /// </summary>
        public CnActionResult AcceptQuest(string questId, string displayName)
        {
            if (string.IsNullOrEmpty(questId)) return CnActionResult.Fail(Messages.NoQuest);

            var state = QuestClientState.Instance;
            if (state == null)
                return CnActionResult.Fail(Loc.Get("ui.system.queststate_unavailable", Messages.QuestUnavailable));

            state.RequestAcceptQuest(questId, string.Empty);
            return CnActionResult.Ok(Messages.AcceptSent + ": " + displayName);
        }

        /// <summary>Отказ от квеста: серверной части нет — окно только повторяет честное сообщение игры.</summary>
        public CnActionResult RejectQuest()
        {
            return CnActionResult.Fail(Loc.Get("ui.quest.reject_unavailable", Messages.RejectUnavailable));
        }

        // ------------------------------------------------------------------ контракты

        public CnActionResult AcceptContract(string contractId, byte contractState)
        {
            if (string.IsNullOrEmpty(contractId)) return CnActionResult.Fail(Messages.NoContract);

            var state = ContractClientState.Instance;
            if (state == null) return CnActionResult.Fail(Messages.ContractUnavailable);
            if (contractState != (byte)ContractState.Pending)
                return CnActionResult.Fail(Loc.Get("ui.character.contract_unavailable", Messages.ContractUnavailable));

            state.RequestAccept(contractId);
            return CnActionResult.Ok(Messages.RequestSent);
        }

        public CnActionResult CompleteContract(string contractId, byte contractState)
        {
            if (string.IsNullOrEmpty(contractId)) return CnActionResult.Fail(Messages.NoContract);

            var state = ContractClientState.Instance;
            if (state == null) return CnActionResult.Fail(Messages.ContractUnavailable);
            if (contractState != (byte)ContractState.Active)
                return CnActionResult.Fail(Loc.Get("ui.character.contract_not_active", Messages.ContractNotActive));

            state.RequestComplete(contractId);
            return CnActionResult.Ok(Messages.RequestSent);
        }

        public CnActionResult FailContract(string contractId, byte contractState)
        {
            if (string.IsNullOrEmpty(contractId)) return CnActionResult.Fail(Messages.NoContract);

            var state = ContractClientState.Instance;
            if (state == null) return CnActionResult.Fail(Messages.ContractUnavailable);
            if (contractState != (byte)ContractState.Active)
                return CnActionResult.Fail(Loc.Get("ui.character.contract_not_active", Messages.ContractNotActive));

            state.RequestFail(contractId);
            return CnActionResult.Ok(Messages.RequestSent);
        }

        /// <summary>Получить груз по расписке — как в ContractsMarketTabController: только активный и невыданный.</summary>
        public CnActionResult ReceiveCargo(string contractId, byte contractState, bool isReceiptContract, bool receiptCargoIssued)
        {
            if (string.IsNullOrEmpty(contractId)) return CnActionResult.Fail(Messages.NoContract);

            var state = ContractClientState.Instance;
            if (state == null) return CnActionResult.Fail(Messages.ContractUnavailable);
            if (contractState != (byte)ContractState.Active || !isReceiptContract)
                return CnActionResult.Fail(Loc.Get("ui.character.contract_not_active", Messages.ContractNotActive));
            if (receiptCargoIssued) return CnActionResult.Fail(Messages.ReceiptIssued);

            state.RequestReceiveCargo(contractId);
            return CnActionResult.Ok(Messages.RequestSent);
        }

        // ------------------------------------------------------------------ багаж ↔ трюм

        /// <summary>Сколько полных упаковок есть у игрока по этому предмету (курс ищется по ItemData.itemName).</summary>
        public int PacksForStore(int inventoryItemId, int have)
        {
            var rate = FindStoreRate(inventoryItemId);
            if (rate == null || rate.Value.inventoryQty <= 0) return 0;
            return Mathf.Max(0, have) / rate.Value.inventoryQty;
        }

        /// <summary>Сколько полных упаковок лежит в трюме по этой строке груза (курс ищется по warehouseItemId).</summary>
        public int PacksForRetrieve(string cargoItemId, int have)
        {
            var rate = FindRetrieveRate(cargoItemId);
            if (rate == null || rate.Value.warehouseQty <= 0) return 0;
            return Mathf.Max(0, have) / rate.Value.warehouseQty;
        }

        /// <summary>Курс упаковки строкой для подсказки: «100 шт = 1 ящ.» либо пусто, если курса нет.</summary>
        public string StoreRateLabel(int inventoryItemId)
        {
            var rate = FindStoreRate(inventoryItemId);
            return rate == null ? string.Empty : FormatRate(rate.Value);
        }

        public string RetrieveRateLabel(string cargoItemId)
        {
            var rate = FindRetrieveRate(cargoItemId);
            return rate == null ? string.Empty : FormatRate(rate.Value);
        }

        /// <summary>
        /// Багаж → трюм. Порция кратна курсу; packs ≤ 0 означает «взять все полные упаковки».
        /// Вне зоны обменника не выполняется и возвращает «подойди к обменнику».
        /// </summary>
        public CnActionResult StoreToShip(ulong shipNetworkObjectId, int inventoryItemId, string displayName, int have, int packs)
        {
            var zone = ReadZone();
            if (!zone.allowed) return CnActionResult.Fail(zone.label);
            if (shipNetworkObjectId == 0) return CnActionResult.Fail(Messages.NoShip);

            var rateOpt = FindStoreRate(inventoryItemId);
            if (rateOpt == null) return CnActionResult.Fail(Messages.NoRate + ": " + displayName);
            var rate = rateOpt.Value;
            if (rate.inventoryQty <= 0) return CnActionResult.Fail(Messages.NoRate + ": " + displayName);

            int available = Mathf.Max(0, have) / rate.inventoryQty;
            if (available <= 0) return CnActionResult.Fail(Messages.NoPack(rate.inventoryQty, rate.warehouseQty));

            int take = Mathf.Clamp(packs <= 0 ? available : packs, 1, available) * rate.inventoryQty;

            var server = ShipCargoServer.Instance;
            if (server == null) return CnActionResult.Fail(Messages.CargoServerUnavailable);

            server.RequestStoreToCargoRpc(shipNetworkObjectId, inventoryItemId, take);
            return CnActionResult.Ok(Messages.Stored(take, (take / rate.inventoryQty) * rate.warehouseQty, displayName));
        }

        /// <summary>Трюм → багаж. Порция кратна курсу упаковки; вне зоны обменника недоступно.</summary>
        public CnActionResult RetrieveFromShip(ulong shipNetworkObjectId, string cargoItemId, string displayName, int have, int packs)
        {
            var zone = ReadZone();
            if (!zone.allowed) return CnActionResult.Fail(zone.label);
            if (shipNetworkObjectId == 0) return CnActionResult.Fail(Messages.NoShip);

            var rateOpt = FindRetrieveRate(cargoItemId);
            if (rateOpt == null) return CnActionResult.Fail(Messages.NoRate + ": " + displayName);
            var rate = rateOpt.Value;
            if (rate.warehouseQty <= 0) return CnActionResult.Fail(Messages.NoRate + ": " + displayName);

            int available = Mathf.Max(0, have) / rate.warehouseQty;
            if (available <= 0) return CnActionResult.Fail(Messages.NoPack(rate.warehouseQty, rate.inventoryQty));

            int take = Mathf.Clamp(packs <= 0 ? available : packs, 1, available) * rate.warehouseQty;

            var server = ShipCargoServer.Instance;
            if (server == null) return CnActionResult.Fail(Messages.CargoServerUnavailable);

            server.RequestRetrieveFromCargoRpc(shipNetworkObjectId, cargoItemId, take);
            return CnActionResult.Ok(Messages.Retrieved(take, (take / rate.warehouseQty) * rate.inventoryQty, displayName));
        }

        // ------------------------------------------------------------------ курс обмена

        /// <summary>
        /// Курс для упаковки ищется по ItemData.itemName — ровно как ShipCargoConsoleWindow.RefreshInventory:
        /// id строки инвентаря и имя определения разные вещи, а курс задан именем.
        /// </summary>
        private static ExchangeRateEntry? FindStoreRate(int inventoryItemId)
        {
            var resolver = ResourceExchangeResolver.Default;
            if (resolver == null) return null;

            var inv = InventoryClientState.Instance;
            var def = inv != null ? inv.GetItemDefinition(inventoryItemId) : null;
            if (def == null || string.IsNullOrEmpty(def.itemName)) return null;

            return resolver.FindRateForItemName(def.itemName);
        }

        /// <summary>Курс для распаковки ищется по warehouseItemId строки груза — как ShipCargoConsoleWindow.RefreshCargo.</summary>
        private static ExchangeRateEntry? FindRetrieveRate(string cargoItemId)
        {
            var resolver = ResourceExchangeResolver.Default;
            if (resolver == null || string.IsNullOrEmpty(cargoItemId)) return null;
            return resolver.FindRateForWarehouseItem(cargoItemId);
        }

        private static string FormatRate(ExchangeRateEntry rate)
        {
            return rate.inventoryQty + " шт = " + rate.warehouseQty + " ящ.";
        }

        /// <summary>
        /// Подписи окна. Ключей ui.cn.* в проекте нет (04 §6, D18), поэтому тексты живут здесь одним блоком
        /// с русским фолбэком у Loc.Get: пока ключей нет — показывается понятная строка, а не id ключа.
        /// </summary>
        private static class Messages
        {
            public const string Approach = "подойди к обменнику";
            public const string MarketPrefix = "рынок";
            public const string ConsolePrefix = "консоль трюма";

            public const string NoQuest = "квест не выбран";
            public const string NoContract = "контракт не выбран";
            public const string NoShip = "нет корабля";
            public const string NoRate = "курс обмена не найден";
            public const string RequestSent = "запрос отправлен";
            public const string Tracking = "слежу";
            public const string Untracked = "слежение снято";
            public const string AcceptSent = "запрос на принятие отправлен";
            public const string ReceiptIssued = "груз по расписке уже выдан";

            public const string TrackerUnavailable = "трекер квестов недоступен";
            public const string QuestUnavailable = "состояние квестов недоступно";
            public const string ContractUnavailable = "контракт недоступен";
            public const string ContractNotActive = "контракт не активен";
            public const string CargoServerUnavailable = "сервер груза недоступен";
            public const string RejectUnavailable = "отказ от квеста пока не реализован (ждёт серверную часть)";

            public static string NoPack(int need, int gives)
            {
                return "нет полной упаковки (курс: " + need + " шт = " + gives + " ящ.)";
            }

            public static string Stored(int items, int boxes, string displayName)
            {
                return "упаковано " + items + " × " + displayName + " → " + boxes + " ящ. в трюм";
            }

            public static string Retrieved(int boxes, int items, string displayName)
            {
                return "распаковано " + boxes + " ящ. " + displayName + " → " + items + " шт. в багаж";
            }
        }
    }
}
