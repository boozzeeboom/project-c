# Локализация — Журнал итераций

## Итерация 1 — 2026-08-05

**Задача:** Phase 0 (Preflight) + Phase 1 (База: Locale + таблицы + переключение)  
**Тикеты:** LOC-01, LOC-02  
**Коммит:** `2459055` — LOC-01, LOC-02: Phase 0+1 — инфраструктура локализации (9 языков, 4 таблицы, runtime-переключение)

**Изменения:**
- Созданы Locale-ассеты для 9 языков: ru, zh, en, es, de, fr, pt, ja, hi
- Созданы 4 StringTableCollection: Static_Table, UI_Table, Dialogue_Table, System_Table (×9 языков = 36 таблиц)
- LocalizationSettings.asset зарегистрирован как активный, 9 локалей в AvailableLocales
- `Loc.cs` — хелпер Get/Format/Bind с авто-роутингом по префиксу ключа
- `LocaleSelector.cs` — SetLocale/LoadSaved через LocalizationSettings + PlayerPrefs
- `SettingsManager.cs` — новое поле Locale (PlayerPrefs Settings.Locale, дефолт ru)
- `GameplaySettingsSection.cs` — дропдаун выбора языка (9 языков) вместо плейсхолдера
- `LocalizationBootstrap.cs` — Awake(-250) до UIManager(-200), добавлен на NetworkManager
- `LocalizationSetup.cs` — Editor-скрипт для разового создания инфраструктуры

**Статус:** ✅ Phase 0+1 завершены.

## Итерация 2 — 2026-08-05

**Задача:** Phase 2 — System-сообщения (sys.* ключи, удаление хардкода)  
**Тикет:** LOC-03  
**Коммит:** `8012159` — LOC-03: Phase 2 — System-сообщения (49 sys.* ключей, удаление серверных строк)

**Изменения:**
- System_Table_ru: 49 ключей (inventory 11, contract 14, market 22, shared 2)
- InventoryClientState.LocalizeResultCode → `Loc.Get("sys.inventory.*")`
- MarketClientState.LocalizeResultCode → `Loc.Get("sys.market.*")`
- ContractClientState.LocalizeResultCode → `Loc.Get("sys.contract.*")`
- Удалён `ContractServer.ContractClientState_LocalizeResultCode` (сервер не локализует)
- InventoryWorld.Fail: message=code, debugDetail → Debug.Log
- `Loc.ToSnakeCase()` — public helper

**Статус:** ✅ Phase 2 завершён. Следующий — Phase 3 (UI-строки) или Phase 6 (инструмент переводчика).

## Статистика
- 103 файла (103 new)
- 3607 строк добавлено
