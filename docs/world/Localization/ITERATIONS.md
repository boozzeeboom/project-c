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

**Статус:** ✅ Phase 2 завершён.

## Итерация 3 — 2026-08-05

**Задача:** Phase 3 — UI-строки (EscMenu + KeybindingsWindow + ключи для character/contract/market/toast/dialog)  
**Тикет:** LOC-04  
**Коммит:** `7b1ff1f` + `df54864` — LOC-04: Phase 3 — UI локализация

**Изменения:**
- `SettingsWidgets.cs` — `MakeLabel()` auto-binds labels с `ui.` префиксом
- `GameplaySettingsSection.cs` — все label/section → ui.esc_menu.* ключи
- `AudioSettingsSection.cs` — все label/section → ui.esc_menu.* ключи
- `GraphicsSettingsSection.cs` — все label/section/AA → ui.esc_menu.* ключи
- `EscMenuWindow.cs` — навигация, кнопки, exit confirm → Loc.Get/Loc.Bind
- `KeybindingsWindow.cs` — заголовок, кнопки, секции, ЛКМ/ПКМ/СКМ → ui.keybindings.*
- UI_Table_ru: 70+ ключей (EscMenu 40+, Keybindings 10, Character 5, Contract 6, Market 3, Toast 3, Dialog 2)

**Статус:** ✅ Phase 3 завершён.

## Итерация 4 — 2026-08-05

**Задача:** Phase 4 — SO-данные (мигратор + Static_Table наполнение)  
**Тикет:** LOC-05  
**Коммит:** `5925200` — LOC-05: Phase 4 — SO-мигратор + 119 ключей Static_Table

**Изменения:**
- `LocalizationStringMigrator.cs` — Editor tool: сканирует SO, генерирует derive-ключи в Static_Table
- Static_Table_ru: +119 ключей (TradeItem 113, NPC 4, Quest 2)
- Editor menu: `ProjectC/Localization/Migrate SO Strings to Static_Table`
- Factions/Markets — 0 ключей (имена полей отличаются, требуется доработка)

**Статус:** ✅ Phase 4 (мигратор) завершён. Следующий — Phase 5 (диалоги) или Phase 6 (инструмент переводчика).

## Статистика
- 103 файла (103 new)
- 3607 строк добавлено
