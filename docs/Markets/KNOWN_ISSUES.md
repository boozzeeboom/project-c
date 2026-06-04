# Markets — Known Issues

Мелкие недочёты, оставленные на точечную правку. **Не блокеры** — полный цикл BUY/LOAD/UNLOAD/SELL работает. Подтверждено пользователем 2026-06-04: "сейчас верстка работает рынок открывается товары покупаются и продаются".

---

## 1. Diagnostic-логи оставлены в production коде

**Где:**
- `MarketZone.cs:147-196` — `PollLocalPlayerZone` (throttled, раз в ~5с)
- `MarketInteractor.cs:27, 50, 59, 88-104` — `TryOpenMarket` + `FindNearestZone` (каждый вызов)
- `MarketServer.cs:95, 121, 137, 153, 159, 175, 181, 197, 202, 205, 264, 585, 838` — `OnNetworkSpawn`, валидации, snapshot send, RPC receive
- `MarketClientState.cs:58, 89` — `OnSnapshotReceived`, `RequestSubscribeMarket`
- `MarketWindow.cs:262, 628, 641, 673, 682` — Build, Toggle, Show, Hide

**Симптом:** Окно Console забито `[MarketServer] SendSnapshotToClient: client=0 ...` (каждые 5 мин при tick), `[MarketClientState] OnSnapshotReceived ...`, `[MarketInteractor] TryOpenMarket ...`. Не критично, но шумно.

**План:** После стабилизации (через 1-2 недели активного использования) обернуть в `[Conditional("MARKET_DEBUG")]` или `#if UNITY_EDITOR` блоки. **Не убирать** до того как пользователь не подтвердит что всё ОК.

**Приоритет:** Low. Не блокирует.

---

## 2. Initial layout `w=0 h=0` warning при первом Show()

**Где:** `MarketWindow.cs:672-674`
```csharp
if (_mainContainer != null) {
    var rs = _mainContainer.resolvedStyle;
    Debug.Log($"[MarketWindow] Show(): main w={rs.width:F0} h={rs.height:F0} pos={rs.position} bg={rs.backgroundColor}");
}
```

**Симптом:** Первый вызов `Show()` после `EnsureBuilt()` логирует `w=0 h=0 pos=relative bg=...` — USS ещё не применился. Через кадр всё ОК (см. `ApplyInlineFallbackStyles`).

**Причина:** UI Toolkit layout pass отложен. Уже митигировано через `ApplyInlineFallbackStyles` (`MarketWindow.cs:592-622`) — дублирует ключевые правила USS inline при `Show()`. USS всё равно выигрывает для дочерних элементов.

**План:** Убрать лог (он служит только для отладки начального layout'а). Или понизить с `Log` до `LogWarning` только если после `StartingIn(50)` MarkDirtyRepaint layout всё ещё 0.

**Приоритет:** Low. Косметика.

---

## 3. Старая v1 архитектура не удалена

**Где:** 16 файлов в `Assets/_Project/Trade/Scripts/` (root) — `TradeUI.cs`, `TradeMarketServer.cs`, `PlayerTradeStorage.cs`, `PlayerDataStore.cs`, `LocationMarket.cs`, `MarketItem.cs`, `MarketEvent.cs` (старый), `NPCTrader.cs` (старый), `CargoSystem.cs` (частично активен), `AutoTradeZone.cs`, `TradeTrigger.cs`, `PlayerCreditsManager.cs`, `PlayerDebt.cs`, `TradeSetup.cs`, `TradeSceneSetup.cs`, `TradeDebugTest.cs`, `TradeDebugTools.cs`. Плюс 4 `Market_*.asset` (старые `LocationMarket` SO) в `Assets/_Project/Trade/Data/Markets/`.

**Симптом:** ~4000 строк dead code компилируются, но не используются новой подсистемой. `NetworkPlayer.cs:588-617, 626-662` имеет dead RPC (`TradeBuyServerRpc`, `TradeSellServerRpc`, `TradeResultClientRpc`) — **не вызывается** (Update E-handler идёт через `MarketInteractor.TryOpenMarket`).

**Проверка:** `grep "TradeMarketServer.Instance"` в `.cs` даёт 8+ ссылок, но все внутри мёртвых файлов (`TradeUI`, `TradeDebugTools`, `TradeMarketServer` сам) или в `NetworkPlayer.TradeBuyServerRpc` (тоже мёртвый). **Новая** подсистема `MarketServer.Instance` (без `Trade`-префикса) — НЕ задета.

**План:** Cleanup, отдельный тикет:
- Удалить 13 файлов (TradeUI, TradeMarketServer, PlayerTradeStorage, PlayerDataStore, LocationMarket, MarketItem, MarketEvent, NPCTrader, AutoTradeZone, TradeTrigger, PlayerCreditsManager, PlayerDebt, TradeSetup, TradeSceneSetup, TradeDebugTest, TradeDebugTools) — `CargoSystem.cs` ОСТАВИТЬ (MarketServer читает `shipClass` из него)
- Удалить `NetworkPlayer.TradeBuyServerRpc` / `TradeSellServerRpc` / `TradeResultClientRpc` (lines 583-662, 80 строк)
- Удалить `NetworkPlayer.ContractRequestServerRpc` / `ContractAcceptServerRpc` / `ContractCompleteServerRpc` / `ContractFailServerRpc` / `ContractListClientRpc` / `ContractResultClientRpc` (если ContractSystem будет мигрирован отдельно — это можно оставить)
- Удалить 4 `Market_*.asset` (LEGACY `LocationMarket` SO)

**Риск:** Контракты (`ContractSystem.cs:720`) ссылаются на `TradeMarketServer.Instance` (старый). Если удалить старый `TradeMarketServer` раньше чем мигрировать контракты — **компиляция сломается**. Нужно мигрировать контракты на новый `MarketServer` СНАЧАЛА.

**Приоритет:** Medium. Не блокирует Stage 2.5, но при росте проекта станет проблемой.

---

## 4. Контракты (ContractSystem) не мигрированы

**Где:** `Assets/_Project/Trade/Scripts/ContractSystem.cs:720` (ссылается на `TradeMarketServer.Instance`), `ContractBoardUI.cs:462`, `ContractData.cs:223`, `ContractTrigger.cs:109`.

**Симптом:** Контрактная система работает только через старую инфраструктуру. UI в `ContractBoardUI` — старый (UGUI, не UI Toolkit).

**План:** Отдельная большая сессия (как `INTEGRATION_SHIPS_TO_WORLD_0_0.md` для кораблей). Мигрировать на:
- `MarketServer.RequestXxxRpc` (новые) для операций
- `MarketClientState` + UI Toolkit для UI
- `MarketZone.ContractBoard` как scene-placed триггер

**Приоритет:** Low-Medium. Не блокирует Stage 2.5 (контракты не критичны для визуального прототипа).

---

## 5. ServerFileRepository — P1 stub, не реализован

**Где:** `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs`

**Симптом:** В коде стоит TODO. Для dedicated server данные о кредитах/складах/грузе хранятся в PlayerPrefs **каждого процесса** (host vs client) → рассинхрон при cross-process.

**План:** Реализовать JSON-сериализацию в `Application.persistentDataPath/ServerData/{clientId}.json` для dedicated. Для host оставить PlayerPrefsRepository.

**Приоритет:** P1. Не блокирует Stage 2.5 (только host testing).

---

## 6. `RateLimited` error code нигде не возвращается

**Где:** `TradeResultCode.cs:RateLimited` (enum value) + `MarketClientState.LocalizeResultCode("Слишком много запросов")`.

**Симптом:** Код `RateLimited` определён и локализован, но `MarketServer.CheckRateLimit` просто `return` (без уведомления клиента) при превышении 30 ops/min. Клиент не получает feedback.

**План:** `CheckRateLimit` → если `false`, слать `TradeResultDto_Fail(RateLimited, ...)` клиенту.

**Приоритет:** Low. Anti-exploit фича (GDD §10.3), 30 ops/min это ~1 op/2sec — вручную не нажмёшь столько.

---

## 7. `Reputation discount` (GDD §4) не реализован

**Где:** GDD_22 §4 упоминает `reputation_discount` в формуле цены. `PriceFormula.CalculatePrice` — без репутации.

**Симптом:** `reputation_discount` нет в `PriceFormula`. Цены одинаковые для всех игроков.

**План:** Добавить `IPlayerReputationRepository`, передавать в `MarketItemState.RecalculatePrice(ulong clientId)` или `PriceFormula.CalculatePrice(item, reputation)`. ОТДЕЛЬНЫЙ ТИКЕТ (см. GDD_23_Faction_Reputation).

**Приоритет:** Stage 5+ (по GDD §5.1).

---

## 8. NPC-трейдеры в коде, но нет UI для их настройки

**Где:** `TradeWorld.InitDefaultNPCTraders()` — 4 hard-coded трейдера (`ГосКонвой`, `Ветер`, `Караванщик`, `Челнок`) с фиксированными маршрутами. `NPCTrader.cs` — `[Serializable]` класс, но не ScriptableObject.

**Симптом:** Баланс NPC-трафика правится только в коде (rebuild required).

**План:** Сделать `NPCTrader` ScriptableObject (или ScriptableObject list в `MarketConfig`), грузить из ассетов. Editor tool для генерации.

**Приоритет:** Low. Stage 3+.

---

## 9. `marketVersion` нигде не используется клиентом

**Где:** `MarketState.ComputeVersion()` инкрементируется, `MarketSnapshotDto.marketVersion` шлётся, но клиент не различает «новый снапшот» от «старый» (всегда `Rebuild()`).

**Симптом:** `Rebuild()` лишний раз вызывается при каждом snapshot, даже если цены не изменились.

**План:** Кешировать `_lastMarketVersion` в `MarketClientState`, если совпадает — не дёргать `OnSnapshotUpdated`. Или `MarketWindow.HandleSnapshot` сам сравнивает.

**Приоритет:** Low. Оптимизация, не функциональный баг.

---

## 10. Cargo не синхронизируется с `ShipController` (visual)

**Где:** `ShipController` (вне Trade) не имеет ссылки на `CargoData`. Груз визуально не отображается на корабле (нет mesh/icon для ящиков в трюме).

**Симптом:** Торговля работает, склад/груз на UI обновляются, но визуально корабль «пустой».

**План:** Stage 4 (GDD §5.5). Визуализация груза: `ShipController.LoadCargoVisual` через префабы ящиков, привязанные к `ship.cargoData.Items`.

**Приоритет:** Stage 4. Не блокирует Stage 2.5.

---

## 11. Нет хоткеев для быстрых операций

**Где:** `MarketWindow` — все операции через кнопки. GDD не специфицирует, но было бы удобно.

**Симптом:** Q/W/E/R/Y — пешие/корабельные хоткеи уже заняты. Свободные: Tab, B, U, I, O, P, [, ], \\, etc.

**План:** Stage 3+. Добавить `B` = Buy, `S` = Sell, `L` = Load, `U` = Unload (или взять что не конфликтует).

**Приоритет:** Low.

---

## 12. `TradeDebugTools` / `TradeDebugTest` ссылки на старый API

**Где:** `Assets/_Project/Trade/Scripts/TradeDebugTools.cs:318`, `TradeDebugTest.cs:269` — ссылаются на `TradeUI.Instance`, `TradeMarketServer.Instance`, `PlayerDataStore.Instance`, `PlayerTradeStorage` (все устарели).

**Симптом:** Если их прицепить к сцене, будут NRE.

**План:** Удалить файлы как часть cleanup (§3).

**Приоритет:** Medium (cleanup). Не блокирует.

---

## Резюме приоритетов

| # | Issue | Priority | Блокирует Stage 2.5? |
|---|-------|----------|---------------------|
| 1 | Diagnostic логи | Low | No |
| 2 | Layout w=0 h=0 | Low | No |
| 3 | Старая архитектура не удалена | Medium | No |
| 4 | Контракты не мигрированы | Low-Medium | No |
| 5 | ServerFileRepository stub | P1 | No (только host) |
| 6 | RateLimited не возвращается | Low | No |
| 7 | Reputation discount | Stage 5+ | No |
| 8 | NPC трейдеры в коде | Low | No |
| 9 | marketVersion не используется | Low | No |
| 10 | Cargo visual | Stage 4 | No |
| 11 | Хоткеи | Low | No |
| 12 | TradeDebug* tools → старый API | Medium (cleanup) | No |
