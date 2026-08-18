# Итерации разработки Markets

---

## Итерация от 2026-08-17

**Задача:** Глубокий аудит системы рынков и контрактов, выявление архитектурных дефектов и подготовка приоритизированного плана рефакторинга.

**Коммит:** `5515162b613c571bfeb02ff0724df31747b37fdb` — T-MKT01: Глубокий аудит рынков и контрактов

**Изменения:**
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — статический аудит, P0–P3 findings, целевая модель данных, backlog и verification plan
- Проверки runtime/build/Play Mode в рамках аудита не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Этап 1 реализации аудита — исправление целостности active contract index, безопасная регенерация доски и защита debts-only persistence.

**Коммит:** `339ee385cb60d740e56e8a91996e3170c411612f` — T-MKT02: Исправить целостность контрактов

**Изменения:**
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — active contracts больше не удаляются регенерацией; stale/expired IDs очищаются; active count считает только Active records
- `Assets/_Project/Trade/Scripts/Dto/ContractSaveData.cs` — debts-only snapshots считаются валидными
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксирован результат этапа 1 и проверка компиляции
- Unity compile check: ошибок компиляции нет
- Play Mode/domain tests/screenshots не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Этап 2 реализации аудита — единый network result contract для market rate limit и contract failures.

**Коммит:** `b8fe8659285b234d5ece7b920c6f4decd64d5ee1` — T-MKT03: Унифицировать network feedback рынков

**Изменения:**
- `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` — rate-limited market RPC теперь возвращает `TradeResultDto`
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — технические enum names не передаются в UI message
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксирован результат этапа 2
- Unity compile check: ошибок компиляции нет
- Play Mode/network smoke test/screenshots не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Этап 3 реализации аудита — schema markers для market/contract snapshots и atomic file writes для dedicated repository.

**Коммит:** `c6c0598b0aaea34029061705d10135b19c49b8e8` — T-MKT05: Защитить завершение delivery-контрактов списанием груза

**Изменения:**
- `Assets/_Project/Trade/Scripts/Dto/ContractSaveData.cs` — добавлена schema version 1
- `Assets/_Project/Trade/Scripts/Dto/MarketSaveData.cs` — добавлена schema version 1 и null-safe HasData
- `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs` — atomic temp-file replace с `.bak` backup для markets/contracts
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксированы ограничения и compile correction
- Unity compile check: ошибок компиляции нет после исправления `System.PlatformNotSupportedException`
- Persistence round-trip/Play Mode/screenshots не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Вынести таймеры завершения контрактов из хардкода в настройки `[ContractServer]`.

**Коммит:** `811066c66d953d91889ba8a170e3a5ffef1b1e99` — T-TRADE02: Настройки таймеров контрактов

**Изменения:**
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — добавлены поля таймеров Standard/Urgent/Receipt в инспекторе
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — передача настроек таймеров в генерацию контрактов
- `Assets/_Project/Trade/Scripts/ContractData.cs` — применение переданных лимитов с сохранением старых значений по умолчанию
- Проверка Unity: компиляция без ошибок

---

## Итерация от 2026-07-07

**Задача:** Персистенция runtime-состояния рынков (план `market_persistence_v1.md`)

**Коммит:** `34db10d957777e84ff46f374dda29e5a92c7744d` — T-MARKET-PERSIST: Персистенция runtime-состояния рынков через IPlayerDataRepository

**Изменения:**
- `Assets/_Project/Trade/Scripts/Dto/MarketSaveData.cs` — новый DTO
- `Assets/_Project/Trade/Scripts/Repository/IPlayerDataRepository.cs` — + SaveMarkets / TryLoadMarkets
- `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs` — JSON `markets.json`
- `Assets/_Project/Trade/Scripts/Repository/PlayerPrefsRepository.cs` — key `PD2_Markets`
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` — SaveAll / LoadAll + dirty flag
- `Assets/_Project/Trade/Scripts/Core/MarketState.cs` — обновлён комментарий
- `docs/Markets/MARKET_PERSISTENCE.md` — документация

---

## Итерация от 2026-08-17

**Задача:** Этап 4 реализации аудита — server-side validation и consumption cargo при delivery completion (`MKT-CON-003`).

**Коммит:** `487c2c99eae88441b06c0fb385d3657d94496882` — T-MKT05: Защитить завершение delivery-контрактов списанием груза

**Изменения:**
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` — добавлен `TryConsumeDeliveryCargo(...)` с проверкой суммарного количества, списанием из cargo/warehouse и compensating rollback при persistence error
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — delivery completion требует успешного cargo consumption до `Completed` и reward; Receipt flow не изменён
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — `RequestCompleteRpc` принимает ship ID и проверяет zone/ownership
- `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs` — передаёт текущий ship ID, `0` оставляет warehouse-only completion
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксирован результат этапа 4 и оставшиеся ограничения
- Unity compile check: ошибок компиляции нет
- Play Mode/domain tests/persistence round-trip/network smoke test/screenshots не выполнялись


---

## Итерация от 2026-08-17

**Задача:** Этап 7 реализации аудита — explicit persistence load status для valid-empty snapshots (`MKT-PER-001`).

**Коммит:** `d68a09a79623445da3cfc18768b743d6dbfef13b` — T-MKT08: Развести статусы пустых и отсутствующих snapshot

**Изменения:**
- `Assets/_Project/Trade/Scripts/Repository/IPlayerDataRepository.cs` — введён `RepositoryLoadStatus`: `NoSaveFound`, `Loaded`, `ValidEmptySave`, `CorruptSave`, `UnsupportedSchema`
- `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs` и `PlayerPrefsRepository.cs` — explicit status, valid-empty handling и schema normalization
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — regeneration только при `NoSaveFound`; rejected snapshots не перезаписываются
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` — overlay без `HasData`-false positive для valid-empty
- `docs/Markets/*PERSISTENCE*.md`, `MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — синхронизирована документация этапа 7
- Unity compile check: ошибок компиляции нет
- Play Mode, persistence round-trip, corrupt-primary recovery, future-schema runtime test и screenshots не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Этап 6 реализации аудита — schema migration, future-version guard и backup recovery для dedicated JSON repository (`MKT-PER-003`).

**Коммит:** `4713847c5eeaec4e1cf45751679403ccc09d266d` — T-MKT07: Добавить миграцию и recovery persistence

**Изменения:**
- `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs` — primary/`.bak` recovery, schema 0 → 1 migration, future schema rejection и безопасная очистка `.tmp`
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксированы результаты этапа 6 и оставшиеся ограничения
- Unity compile check: ошибок компиляции нет
- Persistence round-trip/corrupt-primary recovery/future-schema rejection/Play Mode/screenshots не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Этап 5 реализации аудита — fail-closed политика для неполного Receipt flow (`MKT-CON-004`).

**Коммит:** `0150a771bdc7137b220ca0618754f391d40183c5` — T-MKT06: Заблокировать неполный Receipt flow

**Изменения:**
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — Receipt больше не генерируется и не принимается; старые active Receipt не могут получить reward через completion
- `Assets/_Project/Trade/Scripts/Dto/ContractResultCode.cs` — добавлен `UnsupportedContractType = 14`
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксирован безопасный fail-closed режим и оставшиеся ограничения
- Unity compile check: ошибок компиляции нет
- Play Mode/domain tests/persistence round-trip/network smoke test/screenshots не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Этап 8 реализации аудита — retention terminal records (`MKT-PER-002`).

**Коммит:** `dd8f7871c3c2905971b9a4faeed5e64422cb8e61` — T-MKT09: Ограничить terminal history контрактов

**Изменения:**
- `Assets/_Project/Trade/Scripts/ContractData.cs` — добавлен UTC terminal timestamp для Completed/Failed records
- `Assets/_Project/Trade/Scripts/Dto/ContractSaveData.cs` — schema version повышена до `2`
- `Assets/_Project/Trade/Scripts/Repository/IPlayerDataRepository.cs` — `SaveContracts()` возвращает результат записи
- `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs` и `PlayerPrefsRepository.cs` — bool save result и безопасная обработка ошибок
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — retention до `50` terminal records на игрока после успешного save
- `docs/Markets/CONTRACT_PERSISTENCE.md`, `MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксировано состояние этапа 8
- Unity compile check: ошибок компиляции нет
- Play Mode/domain tests/persistence round-trip/retention stress test/screenshots не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Этап 9 реализации аудита — best-effort PlayerPrefs recovery для market/contract snapshots (`MKT-PER-003`).

**Коммит:** `98d79bee5a7c9fbaf9784b8ac0b1348769850828` — T-MKT10: Добавить recovery protocol для PlayerPrefs

**Изменения:**
- `Assets/_Project/Trade/Scripts/Repository/PlayerPrefsRepository.cs` — primary, `_bak` и `_tmp` keys; recovery из temp/backup при повреждении primary
- `docs/Markets/CONTRACT_PERSISTENCE.md` и `MARKET_PERSISTENCE.md` — описан host recovery protocol
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксирована частичная реализация `MKT-PER-003`
- Unity compile check: ошибок компиляции нет
- PlayerPrefs corruption/restart recovery, Play Mode и screenshots не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Этап 10 реализации аудита — concurrency lock и transaction policy для credits/cargo/markets/contracts (`MKT-PER-003`).
**Коммит:** `1bb3e0475e76d9ed41be835df36ef7638037ca8e` — T-MKT11: Сериализовать мутации экономики

**Изменения:**
- `Assets/_Project/Trade/Scripts/Repository/RepositoryTransactionScope.cs` — process-wide re-entrant critical section и transaction execution helper
- `Assets/_Project/Trade/Scripts/Repository/IPlayerDataRepository.cs` — lock API и bool persistence results для credits/warehouse/cargo/markets
- `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs` и `PlayerPrefsRepository.cs` — bool write results
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` — transaction scope для trade/cargo/market mutations и rollback-aware delivery persistence
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — transaction scope и rollback delivery completion при persistence failure
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксирована transaction policy
- Unity compile check: без ошибок; Play Mode, stress tests и screenshots не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Этап 11 реализации аудита — удаление dead server→client RPCs и фиксация единственного owner delivery path (`MKT-NET-003`).
**Коммит:** `c4dd4fd86a796794d3ebab10a93bda7b93c93433` — T-MKT12: Удалить dead RPC рынков и контрактов

**Изменения:**
- `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` — удалены `ReceiveMarketSnapshotClientRpc` и `ReceiveTradeResultClientRpc`
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — удалены `ReceiveContractSnapshotClientRpc` и `ReceiveContractResultClientRpc`
- `docs/Markets/ARCHITECTURE.md`, `INTEGRATION.md`, `FIXES_HISTORY.md` — зафиксирован единый delivery path через `NetworkPlayer.*TargetRpc`
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — закрыт `MKT-NET-003`, добавлен результат этапа 11
- Project-wide `grep` по `Assets`: ссылок на удалённые RPC нет
- Unity compile check: без ошибок; Play Mode, integration/network smoke test и screenshots не выполнялись

---

## Итерация от 2026-08-17

**Задача:** Этап 12 реализации аудита — устранение дублирующего contract UI в `CharacterWindow` (`MKT-UI-001/002`).
**Коммит:** `f65a28d8ea54ba8345aa17ee5e562656495e319a` — T-MKT13: Удалить дублирующий contract UI

**Изменения:**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — удалены legacy contract handlers, duplicate row factories, cache/list state и повреждённый `ApplyContractFilters()`
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/ContractsTab.cs` — оставлен единственным владельцем contract UI и подписок
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — закрыты `MKT-UI-001/002`, зафиксирован этап 12
- `validate_script(CharacterWindow.cs)`: ошибок нет; Unity compile check: без ошибок
- Play Mode/UI smoke test не выполнялись; stale refresh после accept/fail остаётся на повторную проверку после `MKT-UI-003`

---

## Итерация от 2026-08-17

**Задача:** Этап 13 реализации аудита — разделение монолитного `MarketWindow` и устранение stale refresh после contract result (`MKT-UI-003`).

**Коммит:** `4017962c8f8177c0c8737fafe3f7ae1524b30324` — MKT-UI-003: Разделить MarketWindow и исправить refresh контрактов

**Изменения:**
- `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` — совместимый facade с прежним `Instance` и window API
- `Assets/_Project/Trade/Scripts/Client/MarketWindowHost.cs` — lifecycle UIDocument, modal visibility, shared feedback и tab navigation
- `Assets/_Project/Trade/Scripts/Client/MarketTabController.cs` — market/warehouse/cargo UI и trade actions
- `Assets/_Project/Trade/Scripts/Client/ContractsMarketTabController.cs` — contract UI и явный `RequestList()` после accept/complete/fail result
- `Assets/_Project/Trade/Scripts/Client/ExchangeTabController.cs` — exchange UI и Pack/Unpack actions
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксирован этап 13 и текущий verification gap
- Unity compile check: **No compile errors**
- Play Mode/UI smoke test/screenshots не выполнялись; требуется ручная проверка без перелистывания вкладок

---

## Итерация от 2026-08-18

**Задача:** Этап 14 реализации аудита — сделать lifecycle общего `MarketZoneRegistry` явным (`MKT-DOM-002` / `REF-4004`).

**Коммит:** `01d26981` — MKT-DOM-002: Сделать lifecycle MarketZoneRegistry явным

**Изменения:**
- `Assets/_Project/Trade/Scripts/Network/MarketZoneRegistry.cs` — session-owner lifecycle; очистка только после освобождения последнего server owner
- `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` — acquire/release registry session ownership
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — acquire/release registry session ownership
- `docs/Markets/ARCHITECTURE.md` — зафиксирован explicit lifecycle общего registry
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — добавлен реализованный этап 14 и обновлён verification gap
- Первый compile check выявил устаревший `GetInstanceID()` в Unity 6; исправлено на reference-based ownership без obsolete API
- После исправления Unity editor отключился во время domain reload, поэтому финальный `check_compile_errors` через bridge недоступен; `git diff --check` пройден
- Play Mode, scene/session despawn-order smoke test и screenshots не выполнялись

---

## Итерация от 2026-08-18

**Задача:** Этап 15A реализации аудита — унифицировать canonical `LocationId` normalization (`MKT-DOM-001` / безопасная часть `REF-4002`).

**Коммит:** `78e8bed4` — MKT-DOM-001: Унифицировать canonical LocationId

**Изменения:**
- `Assets/_Project/Trade/Scripts/Config/MarketConfigCollector.cs` — единый `Trim().ToUpperInvariant()` normalizer
- `Assets/_Project/Trade/Scripts/Config/MarketConfig.cs` — `OnValidate()` использует canonical helper
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` — canonical market/warehouse state and cache keys
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` и `ContractData.cs` — canonical route IDs, legacy snapshot normalization и location mapping deduplication
- `Assets/_Project/Trade/Scripts/Network/MarketServer.cs` — canonical selected-ship cache key
- `Assets/_Project/Scripts/Docking/Network/DockingZoneRegistry.cs` — общий normalizer вместо локального `Norm()`
- документация аудита и `MARKET_ID_REFACTOR_DESIGN.md` синхронизированы
- `check_compile_errors`: **No compile errors**
- Play Mode, NPC trade smoke test и screenshots не выполнялись

---

## Итерация от 2026-08-18

**Задача:** Этап 15C реализации аудита — синхронизировать `ContractCatalog` с `MarketConfig` через custom editor (`MKT-DOM-001`).

**Коммит:** `a0edfd0c` — MKT-DOM-001: Синхронизировать ContractCatalog с MarketConfig

**Изменения:**
- `Assets/_Project/Trade/Scripts/Editor/ContractCatalogEditor.cs` — custom editor с кнопкой сканирования `MarketConfig` assets
- `Assets/_Project/Trade/Resources/ContractCatalog.asset` — автоматически добавлены 10 найденных locations как disabled entries
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — добавлен этап 15C и зафиксирована политика disabled locations до настройки distances
- `docs/Markets/MARKET_ID_REFACTOR_DESIGN.md` — описана editor-синхронизация каталога
- `docs/Markets/FILES_INDEX.md` — добавлены `ContractCatalog` и `ContractCatalogEditor`
- `refresh_unity(scope=scripts, compile=request)` выполнен
- `check_compile_errors`: **No compile errors**
- Custom editor создан Unity Editor'ом как `ProjectC.Trade.Editor.ContractCatalogEditor`; catalog validation: **valid**
- Scene YAML, Play Mode и screenshots не изменялись/не выполнялись

---

## Итерация от 2026-08-18

**Задача:** Этап 15B реализации аудита — вынести locations, distances и contract type definitions в validated `ContractCatalog` (`MKT-DOM-001` / `REF-4003`).

**Коммит:** `efc25821` — MKT-DOM-001: Вынести locations и contract types в ContractCatalog

**Изменения:**
- `Assets/_Project/Trade/Scripts/Config/ContractCatalog.cs` — новый validated ScriptableObject-каталог
- `Assets/_Project/Trade/Resources/ContractCatalog.asset` — текущие локации, GDD_25 distance graph и publishable `Standard/Urgent` definitions
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — генерация по catalog definitions, без hardcoded location list/distance matrix/fixed branches
- `Assets/_Project/Trade/Scripts/ContractData.cs` — `CreateConfigured()` для reward/time parameters из каталога; legacy `Create()` сохранён
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — загрузка каталога из инспектора или Resources
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` и `MARKET_ID_REFACTOR_DESIGN.md` — синхронизирован статус `MKT-DOM-001`
- `check_compile_errors`: **No compile errors**
- Scene YAML и serialized scene references не изменялись
- Play Mode, NPC trade smoke test и screenshots не выполнялись
