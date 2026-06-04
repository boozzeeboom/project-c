# Markets — Known Issues

Мелкие недочёты, оставленные на точечную правку. **Не блокеры** — полный цикл BUY/LOAD/UNLOAD/SELL работает + per-ship cargo cache. Подтверждено пользователем 2026-06-05: cargo не теряется при переключении между кораблями, multi-ship сценарий стабилен.

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

## 13. ~~INVESTIGATION OPEN~~ RESOLVED 2026-06-04: E не открывает рынок (ghost PlayerSpawner)

**Корневая причина:** scene-placed `PlayerSpawner` GameObject в `BootstrapScene` имеет компонент `NetworkPlayerSpawner` и `NetworkPlayer` с `IsOwner=True` (NGO 2.x footgun: `OwnerClientId=0` ставится на все scene-placed NetworkObject'ы на хосте, и `IsOwner==true` совпадает с локальным клиентом). Методы `MarketInteractor.FindLocalPlayer` и `MarketZone.FindLocalPlayer` итерировали все `NetworkPlayer` через `FindObjectsByType` и возвращали ПЕРВОГО с `IsOwner=True` — этим первым оказывался ghost (его InstanceID ниже, чем у свежеспавненного `NetworkPlayer(Clone)`). Ghost сидит в точке спавна (39999.5, 2510, 39999.5) в 100+ метрах от ближайшей `MarketZone`. Все distance-check'и (`GetEffectivePosition` → `PollLocalPlayerZone`, `FindNearestZone`) мерили от позиции ghost → dist=128..171м > tradeRadius → `LocalPlayerZone` всегда null → `TryOpenMarket` → `FindNearestZone` → best=null → рынок не открывается. При этом реальный `NetworkPlayer(Clone)` может стоять в 5м от центра зоны.

**Воспроизводимость:** "intermittent" — на самом деле воспроизводится на каждом запуске в `BootstrapScene`+`WorldScene_0_0`, но из-за предыдущей FIX 2026-06-04 (`GetEffectivePosition` для подлета на корабле) и характерной ошибки "игрок не дошёл до зоны" в логах сцена не доходила до воспроизведения.

**Где зафиксировано:**
- `Assets/_Project/Trade/Scripts/Client/MarketInteractor.cs:110-129` — `FindLocalPlayer` теперь skip'ит GameObject с компонентом `NetworkPlayerSpawner` (discriminator из `NetworkPlayer.OnNetworkSpawn:148`).
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs:198-219` — `FindLocalPlayer` тот же guard.

**Симптом был:**
> "если нажать E сразу после спавна (вне зоны), а потом войти в зону и нажать E — окно не открывается. Без предварительного E вне зоны — работает." (позже уточнено: "рынок просто при каком-то старте - не открывается вообще. персонаж в зоне действия а рынок не открывается")

**Подтверждение фикса (через `unityMCP_execute_code` после фикса, host):**
```
1) E OUTSIDE (500м от primium):  TryOpenMarket=False  ✓
2) E INSIDE  (0м от primium):    TryOpenMarket=True   ✓
   LocalPlayerZone=primium
   MarketWindow.IsVisible=True
```

**Что не делали:**
- ❌ Не трогали `NetworkPlayer.cs` / E-handler / `InteractableManager` — гипотеза A отвергнута, корень был в `FindLocalPlayer`.
- ❌ Не удаляли scene-placed `PlayerSpawner` GameObject — на нём висят `NetworkPlayerSpawner` (маркер), `CharacterController`, `PlayerInputReader`, `NetworkObject` с референсами из других систем (см. `NetworkPlayerSpawner.cs:1-26`).
- ❌ Не рефакторили `MarketInteractor.TryOpenMarket`/`FindNearestZone` — fallback-логика корректна, проблема была только в `FindLocalPlayer`.

**См. также:** [FIXES_HISTORY.md → "2026-06-04 — FIX: ghost PlayerSpawner маскирует реального игрока в MarketZone.FindLocalPlayer"](FIXES_HISTORY.md).

**Приоритет:** RESOLVED 2026-06-04. Полный цикл BUY/LOAD/UNLOAD/SELL работает, рынок открывается в зоне.

---

## 14. ~~OPEN~~ RESOLVED 2026-06-05: cargo теряется при переключении корабля в UI

**Симптом был (из юзерского репорта):**
> «Загружаю товар на ship_light. Переключаюсь на ship_medium, загружаю туда. Переключаюсь обратно на ship_light — cargo пустой. Потеряно.»

**Корневая причина:** `MarketClientState` хранил **только** `WarehouseEntryDto[] _cargoCache` для текущего выбранного корабля. Сервер рассылал cargo **только одного** корабля (`cargo` поле). При переключении dropdown'а snapshot не приходил (тик раз в ~5 мин) → `_cargoCache` показывал stale/чужой cargo. Серверный слой (`TradeWorld._cargoCache[shipId]`, persistent в `PlayerPrefs` под `cargo:{shipNetworkObjectId}`) был корректен — баг был только в **клиентской проекции**.

**Фикс:** в `MarketSnapshotDto` добавлено поле `shipCargos[]` (cargo ВСЕХ nearby ships). `MarketClientState` хранит `CurrentShipCargos: IReadOnlyDictionary<ulong, WarehouseEntryDto[]>`. Ship-selector callback переключает UI мгновенно из локального кэша. `HandleTradeResult` обновляет кэш точечно для затронутого корабля. `SetSelectedShipRpc` отправляет свежий snapshot как safety net.

**Подробности:** [FIXES_HISTORY.md → "2026-06-05 — FIX: потеря cargo при переключении между кораблями"](FIXES_HISTORY.md).

**Приоритет:** RESOLVED 2026-06-05. UI переключение мгновенное, cargo всех кораблей в зоне кэшируется на клиенте, мульти-корабельный сценарий работает.

---

## 15. OPEN: нет ownership-проверки для Load/Unload/View cargo

**Симптом:** Любой клиент в зоне может вызвать `Load/Unload` для **любого** корабля (`TradeWorld.SetCargo(shipId, ...)` не проверяет, что `shipId` принадлежит `clientId`). Через `shipCargos[]` в snapshot чужой клиент видит чужой cargo.

**План:** ввести `ShipOwnershipService: Dictionary<ulong /*shipId*/, ulong /*ownerClientId*/>`. Валидация в `Load/Unload/SetSelectedShip` RPC. Фильтрация `shipCargos[]` в snapshot — отдавать cargo только владельцу, остальным `ShipSummaryDto` с обезличенным `cargoUsed` (без состава). Подробности и P1..P4 в [FIXES_HISTORY.md → "Архитектурный план для будущего расширения"](FIXES_HISTORY.md).

**Приоритет:** P1 для MMO-сценария. Для текущего host-only прототипа не блокирует (single-player в dedicated server режиме ещё не тестировался).

---

## 16. OPEN: persistence `cargo:{shipId}` в PlayerPrefs не работает на dedicated server

**Симптом:** `PlayerPrefsRepository` хранит cargo в `PlayerPrefs` — это **локально на клиенте**. На dedicated server `PlayerPrefs` относится к серверному процессу, а не к клиенту → cargo "забывается" при перезапуске dedicated server.

**План:** ввести `IServerCargoRepository` (SQLite / `Application.persistentDataPath/shipCargo.json`). `TradeWorld` при `IsServer==true` использует server-репозиторий, при `IsClient==true` — PlayerPrefs (или оба: клиент кэширует, сервер истина). Stub `ServerFileRepository` уже в `Assets/_Project/Trade/Scripts/Repository/` — см. issue §5.

**Приоритет:** P1 для dedicated server. Сейчас тестируется только host (server+client в одном процессе) — PlayerPrefs работает.

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
| 13 | ~~E не открывает рынок после E вне зоны~~ RESOLVED 2026-06-04 (ghost PlayerSpawner в FindLocalPlayer) | — | No |
| 14 | ~~Cargo теряется при переключении корабля~~ RESOLVED 2026-06-05 (per-ship client cache + shipCargos[] DTO) | — | No |
| 15 | Нет ownership-проверки Load/Unload + утечка cargo чужим игрокам в snapshot | P1 | No (single-player only) |
| 16 | Cargo persistence в PlayerPrefs не работает на dedicated server | P1 | No (host only) |
