# Markets V2 — Audit & Next-Steps Handoff

**Дата:** 2026-06-05
**Автор:** Mavis (audit), предназначено для следующей сессии
**Скоуп:** полный аудит подсистемы торговли (`Assets/_Project/Trade/` + `NetworkPlayer.cs` + сцены), сравнение с GDD_22, выявление пробелов.

---

## 0. Что проверено

- ✅ `AGENTS.md` (правила для Mavis)
- ✅ `docs/Markets/*` (9 файлов)
- ✅ `Assets/_Project/Trade/Scripts/**` (53 .cs файла)
- ✅ `Assets/_Project/Scenes/BootstrapScene.unity`
- ✅ `Assets/_Project/Scenes/World/WorldScene_0_0.unity`
- ✅ `Assets/_Project/Scenes/Test/ProjectC_1.unity`
- ✅ `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (строки 296-911)
- ✅ `docs/gdd/GDD_22_Economy_Trading.md`
- ✅ `ProjectSettings/EditorBuildSettings.asset` (25 сцен, Bootstrap + 24 World)

**Не проверено в этой сессии** (если потребуется — следующая сессия):
- `Assets/_Project/Scripts/Player/ShipController.cs` (читается косвенно через `CargoSystem`)
- `Assets/_Project/Player/` если отличается от `_Project/`
- `Packages/manifest.json` (известно: NGO 2.11.0, URP 17.0.3)
- `docs/gdd/GDD_25_Trade_Routes.md` (упомянут в GDD_22 §1)
- `docs/gdd/GDD_23_Faction_Reputation.md` (упомянут в GDD_22 §1)

---

## 1. Что работает (подтверждено пользователем 2026-06-04)

| Фича | Где | Статус |
|------|-----|--------|
| BUY/SELL/LOAD/UNLOAD полный цикл | `MarketServer.cs:124-198` + `TradeWorld.cs:169-324` | ✅ работает |
| Multi-ship dropdown (если >1 корабль) | `MarketWindow.cs:423-441` + `MarketZone.cs` (BuildNearbyShipsDtos) | ✅ работает |
| Time-multiplier (0.1x..100x) | `MarketTimeService.cs:33-58` + `RequestSetTimeMultiplierRpc:240-247` | ✅ работает |
| UI Toolkit окно | `MarketWindow.uxml` + `MarketWindow.uss` + `MarketWindow.cs` | ✅ работает (4 фикса 2026-06-04) |
| Position validation | `MarketZone.PollLocalPlayerZone` + `MarketServer.ValidateInZone:424-429` | ✅ работает (фикс #13) |
| SetSelectedShip → cargo в snapshot | `MarketServer.cs:204-218, 296-309` + `MarketSnapshotDto.cargo:42` | ✅ FIX 2026-06-04 |
| PlayerPrefsRepository (host) | `PlayerPrefsRepository.cs` | ✅ работает |

**Найдено и подтверждено в коде** (вдобавок к заявленному в docs):
- Snapshot теперь включает `cargo` выбранного корабля (фикс UI-bug с stale cargo cache, `MarketServer.cs:296-309` + `MarketSnapshotDto.cargo:42` + `MarketWindow.cs:405-417`)
- `MarketItemConfig.allowBuy/allowSell/factionRestriction` есть в `Config/MarketItemConfig.cs:39-43`, но **`factionRestriction` не валидируется** (см. #6)
- `IsPlayerInZone` уже работает корректно (фикс #13 в `KNOWN_ISSUES.md`)

---

## 2. Что НЕ реализовано / нужно доделать

### 2.1 🔴 Cleanup dead code (блокирует чистый билд)

| # | Что | Где | Действие |
|---|-----|-----|----------|
| **C1** | 13 legacy v1 .cs файлов в `Assets/_Project/Trade/Scripts/` (root) | `TradeMarketServer.cs` (49KB), `TradeUI.cs` (56KB), `PlayerTradeStorage.cs` (13KB), `PlayerDataStore.cs` (5KB), `LocationMarket.cs` (7KB), `MarketItem.cs` (8KB), `AutoTradeZone.cs` (7KB), `TradeTrigger.cs` (3KB), `PlayerCreditsManager.cs` (1KB), `PlayerDebt.cs` (8KB), `TradeSetup.cs` (1KB), `TradeSceneSetup.cs` (3KB), `TradeDebugTest.cs` (10KB), `TradeDebugTools.cs` (13KB) | **Удалить все 14**. `CargoSystem.cs` (root) оставить — `MarketServer.ResolveShipClass:447-453` его читает. Старые `MarketEvent.cs` (root) и `NPCTrader.cs` (root) — оставить до миграции контрактов (C2), потом удалить (заменены `Core/MarketEvent.cs`, `Core/NPCTrader.cs`) |
| **C2** | Миграция контрактов на новый `MarketServer` | `Assets/_Project/Trade/Scripts/ContractSystem.cs:817-825` ссылается на старый `TradeMarketServer.Instance` и `PlayerTradeStorage`. `ContractBoardUI.cs:267-270, 471` ссылается на `TradeUI.Instance`. `ContractData.cs:223`, `ContractTrigger.cs:109` | **Сначала** мигрировать, **потом** удалять v1 файлы (C1). Иначе компиляция упадёт. UI Toolkit аналог `ContractBoardUI` — новый файл. |
| **C3** | 4 legacy `Market_*.asset` | `Assets/_Project/Trade/Data/Markets/Market_Primium_v01.asset`, `Market_Secundus_v01.asset`, `Market_Tertius_v01.asset`, `Market_Quartus_v01.asset` — старые `LocationMarket` SO с mutable state | Удалить. Новые `MarketConfig_*.asset` (×4) уже созданы и используются. |
| **C4** | 4 legacy `NetworkPlayer` RPC | `Assets/_Project/Scripts/Player/NetworkPlayer.cs:641-697` (`TradeBuyServerRpc`, `TradeSellServerRpc`, `TradeResultClientRpc`) — мёртвые, новый flow идёт через `MarketServer.RequestBuyRpc:124` | Удалить. **Но** сначала проверить через grep, что никто не зовёт `NetworkPlayer.TradeBuy/SellServerRpc(...)` — иначе удаление сломает runtime (маловероятно: новая подсистема использует `MarketClientState.RequestXxx`) |
| **C5** | 6 legacy `NetworkPlayer` Contract RPC | `NetworkPlayer.cs:725-815` (`ContractRequestServerRpc`, `ContractAcceptServerRpc`, `ContractCompleteServerRpc`, `ContractFailServerRpc`, `ContractListClientRpc`, `ContractResultClientRpc`) | Удалить **только после C2** (миграция контрактов). |
| **C6** | `TradeDebugTools` компонент в `BootstrapScene` | `BootstrapScene.unity:1157-1158` — GameObject `DEBUG_UI_MANAGER`, MonoBehaviour `ProjectC.Trade.TradeDebugTools` (m_Enabled: 0). Скрипт файл удалится в C1, scene-reference надо убрать отдельно | Удалить MonoBehaviour из GameObject (или удалить весь `DEBUG_UI_MANAGER`, если больше ничего полезного на нём нет) |
| **C7** | Тестовая сцена `ProjectC_1.unity` целиком на v1 | `Assets/_Project/Scenes/Test/ProjectC_1.unity` — содержит `TradeUI`, `PlayerTradeStorage`, `AutoTradeZone` (×3), `TradeTrigger`, `TradeSceneSetup`, `TradeMarketServer` (guid `d3705a62...`), `ContractTrigger` (×3), `ContractBoardUI`. **Не в Build Settings** (это `Scenes/Test/`, а Build Settings имеет только `BootstrapScene` + 24 `WorldScene_X_Z`) | **Безопасно удалить** `.unity` + `.meta` (выключен из build). Подтвердить, что никто не держит ссылку — `grep -r "ProjectC_1"` в проекте |
| **C8** | 3 временных тест-файла USS | `Assets/_Project/Trade/Resources/UI/_TestA.uss`, `_TestB.uss`, `_TestUss.uss` — debug-стили «красный квадрат 200×200», остались от ручной отладки UI Toolkit layout. **Никем не реферятся** (проверить grep) | Удалить `.uss` + `.meta` (×6 файлов) |

### 2.2 🟡 Тяжёлые фичи (отдельные тикеты)

| # | Что | GDD_22 ссылка | Текущее состояние | Что нужно |
|---|-----|---------------|-------------------|-----------|
| **F1** | Reputation discount | §4 формула цены: `× reputation_discount`. §13: `reputation_discount` 0.7..1.3 | Поле `MarketItemConfig.factionRestriction:37` есть, но `TradeWorld.TryBuy:169-221` / `TrySell:226-265` **не проверяет фракцию игрока**. `PriceFormula.CalculatePrice:39` не принимает параметр reputation. Цены одинаковые для всех. | Создать `IPlayerReputationRepository` (ServerFileRepository-style), передавать в `TradeWorld.TryBuy/TrySell`, пробрасывать в `PriceFormula.CalculatePrice(..., reputation)`. См. `docs/Markets/KNOWN_ISSUES.md#7`. **Stage 5+** |
| **F2** | `RateLimited` feedback клиенту | §10.3 «Лимит транзакций» | `TradeResultCode.RateLimited:24` определён, `MarketClientState.LocalizeResultCode:142` локализован, но `MarketServer.CheckRateLimit:431-445` при превышении 30 ops/min делает `return` (без отправки `TradeResultDto` клиенту). Проверить все 4 RPC: `RequestBuyRpc:128`, `RequestSellRpc:144`, `RequestLoadToShipRpc:160`, `RequestUnloadFromShipRpc:182`. | Заменить `if (!CheckRateLimit(clientId)) return;` на `if (!CheckRateLimit(clientId)) { SendTradeResultToOwner(clientId, TradeResultDto_Fail(TradeResultCode.RateLimited, ...)); return; }`. Также GDD §10.2 говорит «10 сделок/мин», а в коде 30 (`MarketServer.maxOpsPerMinute:51`) — выровнять по спеке или оставить 30 (GDD-автор решит) |
| **F3** | NPC-трейдеры как ScriptableObject | §5.1 «NPC-торговцы», §13 `npc_trader_count` 2..20 | `TradeWorld.InitDefaultNPCTraders:109-127` — 4 hard-coded (ГосКонвой primium→tertius, Ветер primium→secundus, Караванщик tertius→quartus, Челнок secundus→primium). `NPCTrader.cs` в `Core/` — `[Serializable]` POCO. | Сделать `NPCTrader` ScriptableObject, грузить из `MarketConfig.npcTraders: List<NPCTraderConfig>`. Editor tool для генерации. См. `KNOWN_ISSUES.md#8`. |
| **F4** | ServerFileRepository (dedicated server) | нужен для dedicated | `ServerFileRepository.cs:6043` — код есть, но `MarketServer.OnNetworkSpawn:82-86` создаёт `PlayerPrefsRepository` по умолчанию. `useFileRepository:44` — `false` в инспекторе. | Доделать `ServerFileRepository` (JSON в `Application.persistentDataPath/ServerData/{clientId}.json`), протестировать на dedicated. Включить в `MarketServer` через инспектор. **P1** (не блокирует Stage 2.5 — host-only testing). |
| **F5** | Контрабанда (чёрный рынок) | §5.4 | Не начато | Stage 5+ — отдельный тикет. GDD §13 `contraband_detect_base:0.15` |
| **F6** | P2P торговля | §5.3 | Не начато | Stage 3 — отдельный тикет |
| **F7** | «Под расписку» (туториал) | §5.2 | Частично через старый `ContractSystem.cs:720` (ещё не мигрирован, см. C2) | Сначала C2, потом полировка |
| **F8** | Дополнительные товары | §3 | В `TradeItemDatabase` только 3: `mesium_canister_v01`, `antigrav_ingot_v01`, `latex_roll_v01`. GDD упоминает ещё: МНП (100 CR), Двигатель (500), Броня (200), Продовольствие (8), Контрабанда (300) | Создать `TradeItem_*.asset` для каждого, добавить в `TradeItemDatabase.asset`, обновить `MarketConfig_*.asset` |
| **F9** | `demand_decay_half_life_seconds` как tuning knob | §13: 60..86400, current 1800 | `PriceFormula.DEFAULT_HALF_LIFE_SECONDS:34` — `const` 1800, не в инспекторе | Сделать `[SerializeField]` в `MarketTimeService` или `MarketConfig`, прокинуть в `PriceFormula.DecayFactor` (убрать default param) |

### 2.3 🟢 LOW priority (KNOWN_ISSUES.md)

Все 12 issue в `docs/Markets/KNOWN_ISSUES.md` (#1-#12) актуальны. Конкретные ссылки на код:

| # | Что | Где в коде |
|---|-----|------------|
| **L1** | Diagnostic-логи в production | `MarketZone.cs:147-196`, `MarketInteractor.cs:27-104`, `MarketServer.cs:95-264, 585, 838`, `MarketClientState.cs:58-89`, `MarketWindow.cs:262, 628, 641, 673, 682` |
| **L2** | `marketVersion` не используется клиентом для delta-sync | `MarketState.ComputeVersion` инкрементирует, `MarketSnapshotDto.marketVersion:20` шлётся, `MarketClientState.OnSnapshotReceived:56-61` не сравнивает, `MarketWindow.HandleSnapshot:392-443` всегда `Rebuild()`. Оптимизация. |
| **L3** | Cargo visual на корабле | `ShipController` не имеет `LoadCargoVisual`. Stage 4. |
| **L4** | Hotkeys (B/S/L/U) | `MarketWindow` (нет). Stage 3+. |
| **L5** | Layout `w=0 h=0` warning | `MarketWindow.cs:672-674` (косметика, уже митигировано `ApplyInlineFallbackStyles:637-667`) |
| **L6** | No-ship-in-zone UI message | UI показывает «0 кораблей» в `MarketWindow`, но не говорит «Load невозможен — нет корабля у причала». Логика `OnLoadClicked:506-516` обрабатывает `shipId==0` через `SetMessage` — **ОК** |
| **L7** | Edge: `factionRestriction` enum не валидируется | см. F1 |
| **L8** | TradeDebugTools NREs if attached | Удалится в C1 |

### 2.4 📝 Документация рассинхронизирована

| # | Что | Где |
|---|-----|-----|
| **D1** | GDD_22 §11 показывает старую v1 архитектуру | `docs/gdd/GDD_22_Economy_Trading.md:374-447` (схемы `TradeMarketServer`, `PlayerTradeStorage`, `TradeUI`, `LocationMarket`, `MarketItem` со state). Заменить на V2 диаграмму. По AGENTS.md: «⚠️ Ask before: editing `docs/gdd/`» — **нужен user approval**, подготовить patch. |
| **D2** | GDD_22 §14 имеет 6 неподтверждённых критериев v4.0 | Строки 604-609: #15-20 (time multiplier, multi-ship dropdown, position validation, ship-not-in-zone). Требуется ручной тест пользователем (1-2 часа по `INTEGRATION.md §7`). |
| **D3** | `KNOWN_ISSUES.md` не отражает C1-C8 / F1-F9 | В следующей сессии после cleanup — обновить (пометить RESOLVED) |
| **D4** | `MarketZone_Sellshittest` vs `MarketZone_Selltest` typo | `docs/Markets/INTEGRATION.md:150, 152` упоминает `MarketZone_Sellshittest`, но в `WorldScene_0_0.unity:925` имя — `MarketZone_Selltest`. Косметика. |
| **D5** | `docs/Markets/README.md:35` говорит «Подтверждено пользователем 2026-06-04» — ОК, актуально | — |

---

## 3. Конкретные ссылки на файлы (быстрая навигация для следующей сессии)

### Активный код (НЕ трогать без причины)
- `Assets/_Project/Trade/Scripts/Network/MarketServer.cs:1-490` — RPC hub, валидация, DTO builders
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs:1-500` (примерно) — scene-placed зона, player/ship polling
- `Assets/_Project/Trade/Scripts/Network/MarketTimeService.cs:1-170` — tick loop, multiplier
- `Assets/_Project/Trade/Scripts/Network/MarketZoneRegistry.cs` — статический реестр зон
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs:1-486` — серверный singleton, TryBuy/Sell/Load/Unload
- `Assets/_Project/Trade/Scripts/Core/MarketState.cs`, `MarketItemState.cs`, `Warehouse.cs`, `CargoData.cs` — POCO
- `Assets/_Project/Trade/Scripts/Core/MarketEvent.cs`, `NPCTrader.cs` — V2-версии
- `Assets/_Project/Trade/Scripts/Core/TradeResult.cs`, `TradeItemDefinitionResolver.cs`, `DatabaseResolver.cs`
- `Assets/_Project/Trade/Scripts/Config/MarketConfig.cs`, `MarketItemConfig.cs` — ScriptableObject конфиги
- `Assets/_Project/Trade/Scripts/Client/MarketClientState.cs:1-181` — singleton projection
- `Assets/_Project/Trade/Scripts/Client/MarketInteractor.cs` — E-handler helper (вызывается из `NetworkPlayer.cs:359`)
- `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs:1-774` — UI Toolkit контроллер
- `Assets/_Project/Trade/Scripts/Dto/*.cs` — `MarketSnapshotDto`, `TradeResultDto`, `ShipSummaryDto`, `TradeResultCode`
- `Assets/_Project/Trade/Scripts/Service/PriceFormula.cs:1-103` — формула цены, time-based decay
- `Assets/_Project/Trade/Scripts/Repository/IPlayerDataRepository.cs`, `PlayerPrefsRepository.cs`, `ServerFileRepository.cs`
- `Assets/_Project/Trade/Resources/UI/MarketWindow.uxml`, `MarketWindow.uss`, `MarketPanelSettings.asset`
- `Assets/_Project/Trade/Data/TradeItemDatabase.asset`, `Items/TradeItem_*.asset` (×3), `Markets/MarketConfig_*.asset` (×4)

### Интеграция в не-Trade код
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs:296-312` — E-handler, `MarketInteractor.TryOpenMarket()` приоритет
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs:888-908` — `ReceiveMarketSnapshotTargetRpc`, `ReceiveTradeResultTargetRpc`, `RequestSetMarketTimeMultiplier`
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs:Awake` — создаёт `[MarketClientState]` GO (FIX 3)
- `Assets/_Project/Scripts/Player/CargoSystem.cs` (вне Trade) — `MarketServer.ResolveShipClass:447-453` читает `.shipClass`

### Активные сцены
- `Assets/_Project/Scenes/BootstrapScene.unity`:
  - `[MarketServer]` GO (line 23644) + `MarketServer` + `MarketTimeService` (line 23701)
  - `[MarketClientState]` GO (line 23827) — auto-spawn по FIX 3
  - `[MarketWindow]` GO (line 17087) + `UIDocument` + `MarketWindow`
  - Parent: `Markets` GO (line 8136, transform (40031.2, 0, 43493.46)) — **НЕ position, это какой-то другой root, см. ниже**
  - Wait: `m_Father: {fileID: 639769240}` — Markets. Это и есть parent.
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity`:
  - `MarketZone_Primium` GO (line 1874) — `locationId=primium`, `tradeRadius=30`, `shipDockRadius=30`
  - `MarketZone_Selltest` GO (line 925) — `locationId=TEST_1` (debug-зона)
  - 3 корабля в `ships` с `CargoSystem` (строки 645, 1673, 2940)

### Dead code (CANDIDATES FOR C1-C8)
- 14 v1 .cs файлов в `Assets/_Project/Trade/Scripts/` (root) — см. таблицу C1
- 6 dead RPC в `NetworkPlayer.cs:641-815` — см. таблицы C4, C5
- 4 legacy `Market_*.asset` в `Assets/_Project/Trade/Data/Markets/` — см. C3
- `TradeDebugTools` в `BootstrapScene:1157-1158` — см. C6
- `ProjectC_1.unity` — см. C7
- 3 `_Test*.uss` файла — см. C8

---

## 4. Рекомендованный порядок работ (следующая сессия)

### Этап 1: Миграция контрактов (C2) — без этого нельзя C1

**Подзадачи:**
1. Прочитать `Assets/_Project/Trade/Scripts/ContractSystem.cs` целиком (720 строк), понять public API
2. Прочитать `ContractBoardUI.cs` (462 строки) — понять как клиент показывает контракты
3. Создать `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` (аналог `MarketServer`):
   - RPC: `RequestContractList`, `RequestAcceptContract`, `RequestCompleteContract`, `RequestFailContract`
   - Валидация: позиция игрока в зоне ContractBoard
4. Создать `Assets/_Project/Trade/Scripts/Dto/ContractDto.cs` (`INetworkSerializable`)
5. Создать `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs` (аналог `MarketClientState`)
6. Создать `Assets/_Project/Trade/Scripts/Client/ContractBoardWindow.cs` (UI Toolkit аналог `ContractBoardUI`)
7. UXML: `Assets/_Project/Trade/Resources/UI/ContractBoardWindow.uxml`
8. Мигрировать `ContractSystem.cs` (логика) → `Core/ContractWorld.cs` (POCO) — параллельно с `TradeWorld`
9. Обновить `ContractTrigger.cs` чтобы дёргал новый `ContractServer` вместо старого `TradeMarketServer`
10. Поставить новый `ContractServer` в `BootstrapScene` (рядом с `[MarketServer]`)
11. **Тест:** в host — взять контракт → доставить → сдать → кредиты/репутация обновились

**Риски:**
- Контракты ссылаются на `PlayerTradeStorage` (lines 817-825) для доступа к грузу игрока. Нужно мигрировать на новый `TradeWorld.GetOrLoadCargo(shipId, shipClass)`. См. `MarketServer.cs:447-453` как это делается.
- Возможно потребуется RPC для чтения cargo клиентом (сейчас cargo приходит в `MarketSnapshotDto`, но `ContractServer` — отдельный)
- Репутация (Faction) упоминается в контрактах — см. F1, без `IPlayerReputationRepository` контракты с faction-restriction работать не будут

**Оценка:** 1-2 сессии работы (большой рефакторинг, параллельно с FIX-ами).

### Этап 2: Cleanup dead code (C1, C3, C4, C5, C6, C7, C8)

**Подзадачи:**
1. Удалить 14 legacy .cs файлов (C1) — `Remove-Item` в PowerShell
2. Удалить 4 legacy `Market_*.asset` + `.meta` (C3)
3. Удалить 4 мёртвых trade RPC из `NetworkPlayer.cs:641-697` (C4) — `TradeBuyServerRpc`, `TradeSellServerRpc`, `TradeResultClientRpc`
4. Удалить 6 мёртвых contract RPC из `NetworkPlayer.cs:725-815` (C5) — после C2
5. Удалить `TradeDebugTools` MonoBehaviour из `BootstrapScene` (C6) — через MCP `manage_components` с `action: remove`
6. Удалить `ProjectC_1.unity` + `.meta` (C7) — `Remove-Item`
7. Удалить 3 `_Test*.uss` + `.meta` (C8) — `Remove-Item`
8. **Verify:** открыть Unity, дождаться компиляции → `0 errors, 0 warnings`
9. **Verify:** host test → подойти к `MarketZone_Primium` → E → BUY/SELL/LOAD/UNLOAD работает (регресс)
10. **Verify:** client test → 2 инстанса → торговля синхронизируется

**Риски:** низкие. Все удаляемые файлы либо unused, либо `m_Enabled: 0`. После C2 контракты работают через новый путь.

**Оценка:** 1 сессия (механическая работа + регресс).

### Этап 3: Patch GDD_22 §11 (D1)

**Подзадачи:**
1. Подготовить патч `docs/gdd/GDD_22_Economy_Trading.md:374-447` — заменить v1-архитектуру на v2-диаграмму (можно взять ASCII art из `docs/Markets/ARCHITECTURE.md:6-80`)
2. Показать пользователю diff
3. **Дождаться user approval** (по AGENTS.md: «⚠️ Ask before: editing `docs/gdd/`»)
4. Применить правку

**Оценка:** 30 мин.

### Этап 4: Подтверждение acceptance criteria (D2)

**Подзадачи:**
1. Прочитать `GDD_22 §14` критерии #15-20
2. Для каждого: подготовить краткий test plan (что открыть, что нажать, что увидеть)
3. Передать пользователю — он сам пройдёт тесты (по AGENTS.md Mavis не запускает `run_tests` MCP)
4. После подтверждения — обновить §14 на ✅

**Оценка:** 30 мин подготовки + пользователь тестирует.

### Этап 5: Средние фичи (F1-F9)

Каждая — отдельный тикет. Приоритет F1 > F2 > F3 > F9 > F4 > F5 > F6 > F7 > F8. Можно делать параллельно с разными сессиями.

---

## 5. Что НЕ надо трогать в этой подсистеме

- ❌ Не менять `NetworkManagerController.Awake` (FIX 3 создание `[MarketClientState]`) — работает
- ❌ Не менять `BootstrapScene.unity` структуру `Markets` GO hierarchy — `MarketServer` + `MarketClientState` + `MarketWindow` правильно настроены
- ❌ Не менять `MarketWindow.uxml/uss` без причины (4 FIX'а 2026-06-04 были кропотливые)
- ❌ Не удалять `[MarketClientState]` GO из `BootstrapScene` — он реально нужен, хоть и создаётся в Awake (scene reference приоритетнее runtime)
- ❌ Не добавлять `.asmdef` для Trade (AGENTS.md HARD RULE — нужны user approval + refactor plan)
- ❌ Не включать `useFileRepository` в `MarketServer` пока F4 не сделан
- ❌ Не менять `InvokePermission = RpcInvokePermission.Owner` на что-то другое — work-around для NGO 2.x footgun

---

## 6. Быстрая проверка перед началом работы

```powershell
# Проверить, что текущий код компилируется
# (Unity Editor → Console → 0 errors)

# Проверить, что текущая subsystem работает
# (Play → подойти к MarketZone_Primium → E → BUY mesium → SELL mesium → проверить credits)

# Проверить количество .cs в Trade (должно быть ~53)
Get-ChildItem -LiteralPath "Assets\_Project\Trade\Scripts" -Recurse -Filter "*.cs" | Measure-Object

# Проверить, что legacy v1 не используется
grep -r "TradeMarketServer\.Instance" Assets\_Project\Scripts
# Ожидаемый результат: 0 совпадений в Assets/_Project/Scripts/ (вне Trade)
# (8+ совпадений внутри Trade — это legacy, удалится в C1)

# Проверить, что NetworkPlayer не вызывает мёртвые RPC
grep -n "TradeBuyServerRpc\|TradeSellServerRpc\|TradeResultClientRpc" Assets\_Project\Scripts\Player\NetworkPlayer.cs
# Ожидаемый результат: 0 совпадений (после C4)
# Сейчас: 17 совпадений
```

---

## 7. Контекст для следующей сессии (что я не знаю)

- **Не знаю текущее состояние `BootstrapScene` и `WorldScene_0_0`** в редакторе (открыт ли Unity, есть ли unsaved changes)
- **Не знаю, делал ли пользователь что-то ещё после 2026-06-04** (эта дата из `KNOWN_ISSUES.md` и `FIXES_HISTORY.md`)
- **Не знаю, есть ли Git WIP** — в этой сессии `git status` не проверял. Перед удалением файлов желательно коммитнуть checkpoint
- **Не проверял `docs/Markets/FIXES_HISTORY.md`** — там могут быть дополнительные контексты, рекомендую прочитать в следующей сессии
- **Не проверял `docs/QWEN_CONTEXT.md`** — другой агент (Qwen) мог там оставить заметки

**Рекомендация:** начать следующую сессию с `git status` + `git log --oneline -10` + чтения `docs/Markets/FIXES_HISTORY.md` + чтения этого файла.

---

**Связанные документы:**
- `docs/Markets/README.md` — индекс
- `docs/Markets/ARCHITECTURE.md` — слои
- `docs/Markets/FILES_INDEX.md` — каталог 53 .cs файлов
- `docs/Markets/KNOWN_ISSUES.md` — 13 issue (12 open, 1 RESOLVED)
- `docs/Markets/INTEGRATION.md` — связи с остальным проектом
- `docs/Markets/FLOW_TRADE.md` — полный flow BUY/LOAD/UNLOAD/SELL
- `docs/Markets/TRADE_V2_DESIGN.md` — оригинальный дизайн
- `docs/Markets/TRADE_V2_INTEGRATION.md` — integration handbook
- `docs/gdd/GDD_22_Economy_Trading.md` — GDD (нужен patch §11)
- `AGENTS.md` — правила Mavis
