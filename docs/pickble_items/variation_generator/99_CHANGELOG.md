# 99 — Changelog

## 2026-11-26 — T-VAR01: генератор вариаций pickable-предметов

Добавлено:

- **Core**
  - `ItemStatId` / `ItemStatCatalog` — канонические статы, единицы, формулы.
  - `DeterministicRandom` (SplitMix64) + `VariationSeed` (FNV-1a/смешивание seed).
  - `ItemVariationTypes` — профиль, тиры, аффиксы, ролл, результат, issue.
  - `ItemVariationGenerator` — детерминированный ролл тира/аффиксов/статов/имени/
    качества/цены.
  - `ItemVariationProfileFactory` — профиль из `ItemData`, извлечение статов
    (Weapon/Clothing/Module), дефолтные тиры/аффиксы, валидация.
  - `ItemInstanceRegistry` / `ItemVariationService` — серверный фасад, `instanceId`,
    экспорт/импорт JSON.
  - `ItemVariationJson` — профиль/результат/реестр.
- **DTO**
  - `ItemInstanceDto`, `ItemStatsDto`, `ItemStatRollDto` (`INetworkSerializable`).
- **Data**
  - `ItemVariationProfileAsset` (`ScriptableObject`, меню
    `Project C/Items/Variation Profile`).
- **Editor**
  - `ItemVariationWindow` — создание профиля, редактор, preview по seed, luck,
    batch, экспорт JSON, валидация.
  - `ItemVariationProfileAssetInspector` — Capture/Validate/Open/Export.
  - `ItemVariationSelfTest` — self-test всех профилей и in-memory smoke test без
    Play Mode.
- **Документация**
  - README, дизайн, API, руководство редактора, план интеграции, этот changelog.

Проверка:

- Компиляция без ошибок (`check_compile_errors`).
- `RunSyntheticReport()`: `[PASS]`, 120/120 роллов, 14 разных имён,
  JSON профиля round-trip, `ItemVariationService` (5 инстансов), экспорт/импорт
  реестра совпадает.

Не изменялось:

- сцены (`BootstrapScene`, `WorldScene_0_0`), префабы, `.meta`, `.asmdef`;
- `InventoryItemDto`, `InventoryData`, `PickupItem`, `LootTable`, боевой расчёт,
  торговля — интеграция вынесена в T-VAR02+ и описана в `04_INTEGRATION.md`.
