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
