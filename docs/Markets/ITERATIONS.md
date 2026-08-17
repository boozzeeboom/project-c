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

**Коммит:** будет указан после фиксации изменений в git.

**Изменения:**
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — active contracts больше не удаляются регенерацией; stale/expired IDs очищаются; active count считает только Active records
- `Assets/_Project/Trade/Scripts/Dto/ContractSaveData.cs` — debts-only snapshots считаются валидными
- `docs/Markets/MARKETS_CONTRACTS_DEEP_AUDIT_2026-08-17.md` — зафиксирован результат этапа 1 и проверка компиляции
- Unity compile check: ошибок компиляции нет
- Play Mode/domain tests/screenshots не выполнялись

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
