# C1 Cleanup Plan — Markets v1 Legacy Code Removal

**Дата:** 2026-06-05
**Автор:** Mavis
**Скоуп:** точечное удаление legacy v1 кода/ассетов из `Assets/_Project/Trade/`, оставшегося после v2-миграции (см. `MARKETS_V2_AUDIT_2026-06-05.md` §2.1) и C2-refactor контрактов (коммит `f3839c7` от 2026-06-05).
**Цель:** чистый билд без 4000+ строк dead code + ассетов-mock'ов + выключенных компонентов.

---

## 0. Предусловия

- ✅ v2-система работает: BUY/LOAD/UNLOAD/SELL полный цикл + per-ship cargo cache + контракты (3-й таб MarketWindow) — подтверждено пользователем 2026-06-05.
- ✅ C2 (контракты) выполнен: `ContractBoardWindow.cs`, `ContractInteractor.cs`, `ContractZone.cs`, `ContractZoneRegistry.cs` удалены; `ContractBoardWindow.uxml/uss` удалены; `[ContractBoardWindow]`, `[NPCAgent_Primium]` GO удалены из сцен. v2-цепочка: `ContractServer` (RPC hub) + `ContractWorld` (POCO) + `ContractClientState` (projection) живёт в `BootstrapScene` и подключена как 3-й таб `MarketWindow`.
- ✅ Active scenes (`BootstrapScene.unity`, `WorldScene_0_0.unity`) НЕ содержат ссылок на legacy v1 классы — только v2 (`ProjectC.Trade.Network.MarketServer`, `Client.MarketWindow`, и т.д.).
- ⚠️ Legacy v1 файлы `ContractSystem.cs`, `ContractBoardUI.cs`, `ContractData.cs`, `ContractTrigger.cs` остаются активными — на них держится `NetworkPlayer.ContractXxxServerRpc` (lines 720-815) И префаб `TradeMarketServer.prefab` (содержит legacy `ContractSystem`). Удаление этих 4-х файлов — **отдельный этап C5/C1-coupled**, не входит в текущий план. См. §7 "Что НЕ удаляем".
- ⚠️ `NetworkPlayer.cs:583-714` (`TradeBuyServerRpc`, `TradeSellServerRpc`, `TradeResultClientRpc`) — dead code, но удаление — **отдельный тикет C4**, не в этом плане. Удаление влияет на 0 runtime, но это крупное изменение (80 строк + 17 ссылок grep), требует отдельной сессии.

---

## 1. Что удаляем (16 .cs файлов + 9 .asset/.prefab/.uss/.unity + 1 MonoBehaviour)

### Группа A: Legacy v1 .cs (root Scripts/) — 14 файлов

| Файл | Размер | Почему удаляем |
|------|------:|----------------|
| `TradeMarketServer.cs` | 49 KB | Заменён `Network/MarketServer.cs` (RPC hub). Только TradeUI/TradeDebugTools/NetworkPlayer.TradeBuyServerRpc ссылаются, все мертвы. |
| `TradeUI.cs` | 56 KB | Заменён `Client/MarketWindow.cs` (UI Toolkit). Никто не держит ссылку в runtime. |
| `PlayerTradeStorage.cs` | 13 KB | Заменён `Core/Warehouse.cs` (POCO). Только legacy `ContractSystem.cs:817-825` ссылается. |
| `PlayerDataStore.cs` | 5 KB | Заменён `Repository/PlayerPrefsRepository.cs`. Никто не держит ссылку. |
| `LocationMarket.cs` | 7 KB | Заменён `Config/MarketConfig.cs` (read-only SO). 4 Market_*.asset — единственные потребители. |
| `MarketItem.cs` | 8 KB | Заменён `Config/MarketItemConfig.cs` + `Core/MarketItemState.cs`. Только AutoTradeZone ссылается. |
| `AutoTradeZone.cs` | 7 KB | Заменён `Network/MarketZone.cs` (scene-placed). |
| `TradeTrigger.cs` | 3 KB | Заменён `Network/MarketZone.cs`. |
| `PlayerCreditsManager.cs` | 1 KB | Никто не использует. |
| `PlayerDebt.cs` | 8 KB | Заменён `Core/ContractDebt.cs` (контракты) + `PlayerPrefsRepository` (credits). |
| `TradeSetup.cs` | 1 KB | Старый setup-helper, нигде не вызывается. |
| `TradeSceneSetup.cs` | 3 KB | Заменён `Editor/MarketAssetGenerator.cs`. |
| `TradeDebugTest.cs` | 10 KB | Старый test scene, нигде не используется. |
| `TradeDebugTools.cs` | 13 KB | Заменён `Client/MarketWindow.cs`. **Дополнительно**: `NetworkManagerController.cs:71-72, 97-111` ещё дёргает `CreateTradeDebugTools()` (старый diagnostic-вызов) — **вырезаем заодно** (см. §4 Шаг 2.5). После вырезки `TradeDebugTools.cs` безопасно удалить. |

**Дополнительно** (audit-таблица C1, плюс "оставить до C2, потом удалить"):

| Файл | Размер | Почему удаляем |
|------|------:|----------------|
| `MarketEvent.cs` (root) | ~7 KB | C2 выполнен → заменён `Core/MarketEvent.cs` (time-based). |
| `NPCTrader.cs` (root) | ~6 KB | C2 выполнен → заменён `Core/NPCTrader.cs`. |

**ОСТАВЛЯЕМ** (audit §5 "Что НЕ надо трогать" + C1 footnote):
- `CargoSystem.cs` (root) — `MarketServer.ResolveShipClass:447-453` читает `shipClass` через `SpawnManager.SpawnedObjects[shipId].GetComponent<CargoSystem>()`.
- `TradeItemDefinition.cs` + `TradeDatabase.cs` (root namespace `ProjectC.Trade`) — активны, используются новой подсистемой.
- `Dto/TradeResultCode.cs` — v2 enum, активен (несмотря на root namespace — старая привычка, не bug).
- `Core/*` (MarketState, MarketItemState, Warehouse, CargoData, TradeWorld, PriceFormula, ContractWorld, ContractDebt, NPCTrader, MarketEvent, TradeItemDefinitionResolver, DatabaseResolver, TradeResult) — все v2-active.
- `Network/*` (MarketServer, MarketZone, MarketZoneRegistry, MarketTimeService, ContractServer) — v2-active, в сценах.
- `Client/*` (MarketClientState, MarketInteractor, MarketWindow, ContractClientState) — v2-active, в сценах.
- `Config/*` (MarketConfig, MarketItemConfig) — v2-active.
- `Dto/*` (MarketSnapshotDto, TradeResultDto, ShipSummaryDto, ContractDto, ContractSnapshotDto, ContractResultDto, ContractResultCode) — v2-active.
- `Repository/*` (IPlayerDataRepository, PlayerPrefsRepository, ServerFileRepository) — v2-active.
- `Service/PriceFormula.cs` — v2-active.
- `Editor/MarketAssetGenerator.cs`, `MarketItemIDInitializer.cs`, `TradeAssetGenerator.cs` — v2-active Editor tools.
- `Editor/TradeSceneSetupTool.cs` — **помечаем как legacy** (использует `ProjectC.Trade.TradeMarketServer`), но **ОСТАВЛЯЕМ** (отдельный cleanup, не в этом плане).

### Группа B: Legacy v1 ассеты

| Путь | Почему удаляем |
|------|----------------|
| `Assets/_Project/Trade/Data/Markets/Market_Primium_v01.asset` (+ .meta) | Legacy `LocationMarket` SO, заменён `MarketConfig_Primium.asset`. |
| `Assets/_Project/Trade/Data/Markets/Market_Secundus_v01.asset` (+ .meta) | То же. |
| `Assets/_Project/Trade/Data/Markets/Market_Tertius_v01.asset` (+ .meta) | То же. |
| `Assets/_Project/Trade/Data/Markets/Market_Quartus_v01.asset` (+ .meta) | То же. |

### Группа C: Test/debug ассеты

| Путь | Почему удаляем |
|------|----------------|
| `Assets/_Project/Trade/Resources/UI/_TestA.uss` (+ .meta) | Debug-стиль "красный квадрат", grep → 0 ссылок. |
| `Assets/_Project/Trade/Resources/UI/_TestB.uss` (+ .meta) | Debug-стиль "зелёный квадрат", grep → 0 ссылок. |
| `Assets/_Project/Trade/Resources/UI/_TestUss.uss` (+ .meta) | Debug-стиль "минимальный красный блок", grep → 0 ссылок. |
| `Assets/_Project/Scenes/Test/ProjectC_1.unity` (+ .meta) | Тестовая сцена на v1, **не в Build Settings** (audit C7). Editor-скрипты упоминают только в комментариях, не загружают. |
| `Assets/ProjectC_1.unity` (+ .meta) | Дубль `Scenes/Test/ProjectC_1.unity` (md5 совпадает, но GUID разный). Тот же статус. |
| `Assets/_Project/Prefabs/TradeMarketServer.prefab` (+ .meta) | Содержит legacy `TradeMarketServer` + legacy `ProjectC.Trade.ContractSystem`. Никем не инстанцируется (grep по .prefab → 0, не в BootstrapScene, не в WorldScene_0_0). |
| `Assets/_Project/Prefabs/ContractBoard.prefab` (+ .meta) | Содержит только `ContractBoardUI` script (legacy). Никем не инстанцируется. |

### Группа D: Выключенный компонент в активной сцене (C6)

| Что | Где | Действие |
|-----|-----|----------|
| `TradeDebugTools` MonoBehaviour на `DEBUG_UI_MANAGER` | `Assets/_Project/Scenes/BootstrapScene.unity:1147-1163` (`m_Enabled: 0`) | Снять компонент через `manage_components action: remove`. НЕ удалять GameObject — на нём ещё висят активные `SceneDebugHUD`, `MeziyStatusHUD`, `HUDManager` (все с m_Enabled=0, но это part of project HUD system, не trade). |

---

## 2. Что НЕ удаляем (и почему)

### Legacy v1 Контракты: `ContractSystem.cs`, `ContractBoardUI.cs`, `ContractData.cs`, `ContractTrigger.cs` (root Scripts/)

**Причина:**
- `NetworkPlayer.cs:725-815` (6 RPC) ещё **декларированы** и **проксируют** в `ContractSystem.Instance.XxxServerRpc` (lines 727-781) и в `ContractBoardUI.Instance` (lines 791, 807). Удаление 4-х legacy .cs → NRE при `RPC invoke` (даже если v2 цепочка `ContractServer → ContractClientState` работает параллельно).
- `Assets/_Project/Prefabs/TradeMarketServer.prefab` (если оставить префаб) держит `ProjectC.Trade.ContractSystem` MonoBehaviour. **Мы удалим префаб**, что уберёт prefab-ссылку, **НО** в `NetworkPlayer.cs` RPC-прокси останутся.
- Безопасный сценарий — `if (ContractSystem.Instance != null) ...` уже стоит в коде (lines 727, 743, 759, 775, 791, 807). Если `ContractSystem` нигде не инстанцирован (префаб удалён, GO в сцене нет), то `Instance == null` → блок else → только Debug.LogWarning. NRE нет.
- **Но** это шум в console и лишний код. Чистое решение — отдельный тикет C5:
  1. Удалить 4 legacy .cs.
  2. Удалить 6 RPC из `NetworkPlayer.cs:725-815`.
  3. Удалить `using ProjectC.Trade;` (line 7) если не нужен.
  4. Заменить `if (ContractSystem.Instance != null) ...` на простые заглушки или удалить.
  - **Оценка:** 30-60 мин + регресс-тест контрактов.

**Решение:** отдельный тикет, **не в этом плане**. Текущий план удаляет **только** префаб `TradeMarketServer.prefab` (после удаления legacy .cs в Группе A `ContractSystem.Instance` не сможет инстанцироваться через legacy-путь, но legacy-RPC в NetworkPlayer останутся с NullSafe-блоком — OK).

### Legacy v1 Trade RPC в `NetworkPlayer.cs:583-714`

`TradeBuyServerRpc`, `TradeSellServerRpc`, `TradeResultClientRpc` — dead code. Audit C4 рекомендует удалить. **Но:**
- 17 grep-совпадений в `NetworkPlayer.cs` (RPC + вызовы).
- 80 строк кода.
- Требует отдельной сессии с регресс-тестом.

**Решение:** оставляем в этом плане, **отдельный тикет C4**. Текущий план не трогает `NetworkPlayer.cs` (за исключением implicit ничего — Unity не дёргает RPC, если нет сцены/префаба с `TradeMarketServer`).

### `ProjectC_ChunkTest_1.unity` (Test/)

**Причина:** `PrepareTestScene.cs:30` жёстко использует `"ProjectC_ChunkTest_1"` как имя создаваемой сцены, но **не загружает** файл автоматически. Editor-скрипт может работать без существующего файла (создаёт новый). **Оставляем** — это функциональная тестовая сцена для стриминга (out of scope Markets).

### `Editor/TradeSceneSetupTool.cs`

**Причина:** использует `ProjectC.Trade.TradeMarketServer` (legacy, удалится в Группе A). После удаления **скомпилируется с ошибкой CS0246**. **Действие:** **тоже удаляем** в Группе A. Audit §2.1 C1 не упоминает его явно, но по той же логике (legacy) он в списке.

Проверим:
- `ProjectC.Trade.TradeAssetGenerator.cs` — использует `using ProjectC.Trade;` для `TradeItemDefinition` — ОК, остаётся.
- `ProjectC.Trade.MarketItemIDInitializer.cs` — то же, ОК.
- `ProjectC.Trade.MarketAssetGenerator.cs` — то же, ОК.

### `BootstrapScene.unity` → `DEBUG_UI_MANAGER` GameObject (целиком)

**Причина:** на нём 4 MonoBehaviour, **3 из них — НЕ trade**:
- `TradeDebugTools` (m_Enabled=0) — **удаляем** (C6, отдельный шаг через MCP).
- `SceneDebugHUD` (m_Enabled=0) — общий scene-debug HUD, оставляем.
- `MeziyStatusHUD` (m_Enabled=0) — HUD для Мезий-системы (Ships), оставляем.
- `HUDManager` (m_Enabled=0) — главный HUD проекта, оставляем.

GameObject остаётся, снимаем только `TradeDebugTools`.

---

## 3. Контрольный список для "точечно и внимательно"

| Проверка | Результат |
|----------|-----------|
| `grep "ProjectC.Trade.TradeMarketServer\\|ProjectC.Trade.TradeUI\\|ProjectC.Trade.PlayerTradeStorage"` в `Assets/_Project/Scenes/BootstrapScene.unity` | 0 совпадений ✅ |
| `grep "...same..."` в `Assets/_Project/Scenes/World/WorldScene_0_0.unity` | 0 совпадений ✅ |
| `grep "...same..."` в `Assets/_Project/Prefabs/NetworkPlayer.prefab` | 0 совпадений (проверим перед удалением) |
| `_Test*.uss` grep по всему проекту | 0 совпадений ✅ |
| `ProjectC_1.unity` grep по `.cs` файлам | только в комментариях 2 Editor-скриптов ✅ |
| `Market_*.asset` grep | только 4 самих файла + 1 grep в `AutoTradeZone.cs:139` (легаси-файл, удалится) ✅ |
| `ContractSystem` в `BootstrapScene` grep | 0 совпадений (только `ContractServer` v2) ✅ |
| `ContractBoardUI` в `BootstrapScene` grep | 0 совпадений ✅ |
| `ProjectSettings/EditorBuildSettings.asset` | 0 ссылок на Test/ProjectC_1 ✅ |

---

## 4. Порядок выполнения (защита от регрессий)

1. **Шаг 1: Снять `TradeDebugTools` с `DEBUG_UI_MANAGER` в `BootstrapScene.unity`** (через MCP `manage_components action: remove`).
   - **Предварительно:** Unity должен быть запущен. Без Unity — нельзя (scene-serialization).
   - **После:** сохранить сцену (`manage_scene action: save`).
   - **Verify:** `read_console` → 0 ошибок.

2. **Шаг 2: Вырезать `CreateTradeDebugTools()` из `NetworkManagerController.cs`** (18 строк: вызов + метод + doc-комментарий).
   - Это **обязательно** перед удалением `TradeDebugTools.cs` — иначе compile error CS0246 на `FindObjectsByType<ProjectC.Trade.TradeDebugTools>` (line 99) и `AddComponent<ProjectC.Trade.TradeDebugTools>` (line 107).
   - Безопасно — приватный метод, нигде больше не вызывается (grep подтверждает).
   - **Verify:** `refresh_unity mode=force compile=request wait_for_ready=true` → 0 errors.
   - **Verify:** `read_console types=[error,warning] count=20`.

3. **Шаг 3: Удалить 16 legacy v1 .cs (Группа A: 14 файлов + MarketEvent + NPCTrader).**
   - Через `Remove-Item` (bash) — 16 файлов + 16 .meta.
   - **Verify:** `refresh_unity mode=force compile=request wait_for_ready=true` → 0 errors, 0 warnings (новые).
   - **Verify:** `read_console types=[error,warning] count=20`.
   - **Ожидаемые warning'и** (не блокируют, оставляем до C4/C5): возможны warning'и от `NetworkPlayer.cs:644, 661, 690, 700, 811` (NullSafe-блоки для legacy RPC — не ошибка, не warning от C# компилятора; в Console будут LogWarning'и только в runtime, не в compile).

4. **Шаг 4: Удалить `Editor/TradeSceneSetupTool.cs` (+ .meta).**
   - Использует `ProjectC.Trade.TradeMarketServer` (уже удалён в Шаге 3) → скомпилируется только **после** Шага 3.
   - **Verify:** refresh + read_console (тот же, что и Шаг 3).

5. **Шаг 5: Удалить 4 `Market_*.asset` + .meta (Группа B).**
   - Через `Remove-Item`.
   - **Verify:** refresh_unity (compile=None — нет .cs изменений) → 0 errors.

6. **Шаг 6: Удалить 3 `_Test*.uss` + .meta (Группа C, тест-стили).**
   - **Verify:** refresh_unity → 0 errors.

7. **Шаг 7: Удалить 2 `ProjectC_1.unity` + .meta (Группа C, тест-сцены).**
   - **Verify:** refresh_unity → 0 errors.

8. **Шаг 8: Удалить 2 legacy префаба + .meta (Группа C, TradeMarketServer + ContractBoard).**
   - **Verify:** refresh_unity → 0 errors.

9. **Шаг 9: Финальная компиляция + чтение console.**
   - `refresh_unity mode=force compile=request wait_for_ready=true`.
   - `read_console types=[error,warning] count=30`.
   - Ожидание: 0 errors. Warnings (если есть) — только от deprecation в `NetworkPlayer.cs:583-714` (C4 — отдельный тикет).

---

## 5. Verification для пользователя

После завершения (все шаги 1-8):

```powershell
# 1. Compile check
# Open Unity Editor → Console → 0 errors expected

# 2. Регресс — ручной test
# - Open BootstrapScene → Play
# - Console: [MarketServer] инициализирован: markets=4 ... (без ContractSystem warning'ов, т.к. он не инстанцирован, но NetworkPlayer.ContractXxxRpc теперь с NullSafe-блоком = только Debug.LogWarning, не ошибка)
# - Подойти к MarketZone_Primium → E → окно открылось → переключиться на таб КОНТРАКТЫ → видны контракты
# - BUY mesium → SELL mesium → credits обновились

# 3. EditMode tests (когда asmdef появится — отдельный тикет)
# Window → General → Test Runner → EditMode → Run All
```

---

## 6. Что в следующей сессии (отдельные тикеты)

| # | Тикет | Скоуп |
|---|-------|-------|
| **C4** | Удалить 3 dead trade RPC из `NetworkPlayer.cs:583-714` | 80 строк, 17 grep-ссылок, регресс-тест |
| **C5** | Удалить legacy v1 контракты (`ContractSystem`, `ContractBoardUI`, `ContractData`, `ContractTrigger`, root namespace) + 6 RPC из `NetworkPlayer.cs:725-815` | 4 .cs + 6 RPC, NullSafe-cleanup, регресс-тест контрактов через v2 таб |
| **D3** | Обновить `KNOWN_ISSUES.md` §3 → отметить C1-C8 как RESOLVED, оставить открытыми §4 sub-points (auto-complete, receipt cargo) | 5 мин |
| **D4** | Поправить typo `MarketZone_Sellshittest` → `MarketZone_Selltest` в `INTEGRATION.md` | 1 мин |
| **F2** | `RateLimited` → слать `TradeResultDto_Fail(RateLimited, ...)` клиенту | 10 строк, 4 RPC |
| **F3** | NPC трейдеры → ScriptableObject | средняя фича |

---

## 7. Что НЕ делаем (вне scope)

- ❌ НЕ удаляем legacy v1 классы контрактов (C5 — отдельный тикет).
- ❌ НЕ удаляем dead trade RPC в `NetworkPlayer.cs` (C4 — отдельный тикет).
- ❌ НЕ удаляем `ProjectC_ChunkTest_1.unity` (нужен для Phase 2 streaming).
- ❌ НЕ удаляем `DEBUG_UI_MANAGER` GameObject (там ещё 3 не-trade компонента).
- ❌ НЕ создаём `.asmdef` для Trade (AGENTS.md HARD RULE).
- ❌ НЕ модифицируем `docs/gdd/GDD_22` §11 (нужен user approval).
- ❌ НЕ коммитим изменения (пользователь коммитит).

---

## 8. Связанные документы

- `docs/Markets/MARKETS_V2_AUDIT_2026-06-05.md` §2.1 (C1-C8) — оригинальный cleanup-план.
- `docs/Markets/FILES_INDEX.md` — статус каждого файла (LEGACY vs АКТИВЕН).
- `docs/Markets/KNOWN_ISSUES.md` §3 — почему это ещё не сделано.
- `docs/Markets/INTEGRATION.md` §8 — cleanup checklist (пост-C2).
- `docs/dev/CONTRACTS_AS_MARKET_TAB_REFACTOR.md` — почему C2-блок снят и можно удалять legacy.
- Коммиты: `f3839c7` (C2), `17562ed` (отладка контрактов), `3395d8e` (per-ship cargo).
