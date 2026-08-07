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

**Статус:** ✅ Phase 4 завершён.

## Итерация 5 — 2026-08-05

**Задача:** Phase 5 — Диалоги (DialogWindow fixes + dialogue.* ключи)  
**Тикет:** LOC-09  
**Коммит:** `010fe9a` — LOC-09: Phase 5 — Диалоги

**Изменения:**
- `DialogWindow.cs`: speakerNpcId → Loc.Get(static.npc.{id}.displayName) вместо сырого ID
- `DialogWindow.cs`: speakerText → Loc.Get(key, fallback) для локализации реплик
- `DialogWindow.cs`: [Недоступно] → Loc.Format(ui.dialog.unavailable)
- `DialogWindow.cs`: [Конец] → Loc.Get(ui.dialog.end)
- Dialogue_Table_ru: 14 ключей из 3 DialogTree (node texts + edge labels)

**Статус:** ✅ Phase 5 завершён.

## Итерация 6 — 2026-08-05

**Задача:** Phase 6 — Инструмент переводчика (CSV Export/Import)  
**Тикет:** LOC-10  
**Коммит:** `10ee059` — LOC-10: Phase 6 — Инструмент переводчика

**Изменения:**
- `LocalizationToolWindow.cs` — EditorWindow: Export All / Import CSV / Проверить покрытие
- CSV export: 4 таблицы × 9 локалей → Key | ru | en | zh | es | de | fr | pt | ja | hi
- CSV import: парсинг с quoted-полями, запись в StringTable entries всех локалей
- Coverage check: подсчёт ключей без перевода (исключая ru source)
- Menu: `ProjectC/Localization/Export/Import Tool`

**Статус:** ✅ Phase 6 завершён.

---

## Сводка — все фазы выполнены

| Фаза | Тикет | Коммит | Ключей |
|---|---|---|---|
| Phase 0+1 — Инфраструктура | LOC-01/02 | `2459055` | — |
| Phase 2 — System-сообщения | LOC-03 | `8012159` | 49 sys.* |
| Phase 3 — UI-строки | LOC-04 | `7b1ff1f`, `df54864` | 70+ ui.* |
| Phase 4 — SO-данные | LOC-05 | `5925200` | 119 static.* |
| Phase 5 — Диалоги | LOC-09 | `010fe9a` | 14 dialogue.* |
| Phase 6 — Инструмент | LOC-10 | `10ee059` | — |

**Всего: 252+ ключей в 4 таблицах, 9 языков, полный цикл export→translate→import.**
Phase 7 (верификация) — за пользователем (playtests).

## Итерация 7 — 2026-08-06

**Задача:** Диагностика и исправление дропдауна языков в ESC-меню (попап не закрывался, не синхронизировался SettingsManager.Locale)
**Тикет:** T-LOC-ESC
**Коммит:** `b08eade` — T-LOC-ESC: фикс дропдауна языков в ESC-меню — попап не закрывался, не синхронизировался SettingsManager.Locale

**Диагностика (5 багов):**
1. Попап CustomDropdown остаётся в panel.visualTree при закрытии ESC-меню
2. Попап выходит за границы окна меню (позиционирование в корне панели)
3. OnButtonPointerDown без StopPropagation — клик всплывает до root
4. LocaleSelector.SetLocale() не обновляет SettingsManager.Locale
5. CustomDropdown.Cleanup() нигде не вызывается

**Изменения:**
- `CustomDropdown.cs`: StopPropagation/StopImmediatePropagation в OnButtonPointerDown; статический HashSet трекинг + CloseAllPopups()
- `EscMenuWindow.cs`: вызов CustomDropdown.CloseAllPopups() в SetOpen(false)
- `LocaleSelector.cs`: вызов SettingsManager.SetLocale(code) для синхронизации
- `SettingsManager.cs`: новый метод SetLocale(code) для обновления свойства Locale

**Статус:** ✅ Исправлено.

**Дополнительные коммиты (2-й раунд):**
- `1971488` — fix CloseAllPopups (snapshot перед итерацией) + вызовы в NavigateTo/Back/Root
- `764d29e` — debug-логи в CustomDropdown + GameplaySettingsSection
- `5089afb` — позиционирование попапа через worldBound вместо ChangeCoordinatesTo

## Итерация 8 — 2026-08-06

**Задача:** Phase 8 — Глубокая UI-локализация (все пропущенные окна и строки)  
**Тикет:** LOC-11  
**Коммит:** `2046b42` — LOC-11: Phase 8 — глубокая UI-локализация (87 новых ключей, 7 файлов C#)

**Изменения:**
- UI_Table SharedData: +73 character/contract/market/ship/cargo ключей, +14 repair ключей (всего 139)
- `EscMenuWindow.cs` — главные кнопки ПРОДОЛЖИТЬ/НАСТРОЙКИ/СПАСЕНИЕ/ВЫХОД + title МЕНЮ
- `CharacterWindow.cs` — все фильтры, статусы, quest states, track/skill кнопки, сообщения
- `ContractsTab.cs` — фильтры, типы контрактов, ранги локаций, сообщения
- `InventoryTab.cs` — фильтры, НАДЕТЬ/СНЯТЬ/БРОСИТЬ, Бонусы, detail
- `MyShipsTab.cs` — все хардкоженные строки (7)
- `MarketWindow.cs` — сообщения, названия операций Куплено/Продано/Погрузка/Разгрузка
- `ShipCargoConsoleWindow.cs` — 4 статусных сообщения
- `RepairManagerWindow.cs` — 16 строк (ремонт, установка, покраска, статусы)
- `LocalizationTableRepair.cs` — расширен UIKeyMap до ~130 ключей

**Статус:** ✅ Phase 8 завершён.

## Статистика
- 103 файла (103 new)
- 3607 строк добавлено

## Итерация 9 — 2026-08-07

**Задача:** Phase 9 — DEEP-UP: финальная обёртка всех оставшихся контроллеров + C# runtime-строк  
**Тикет:** LOC-12  

**Изменения (C# Runtime — 16 строк в 3 файлах):**
- `KnowledgeToast.cs` — `"📖 Открыто знание — {0}: {1}"`, `"и ещё {0}"`
- `DialogWindow.cs` — `"+1/-1 предмет"`, `"Репутация"`, `"Отношение"`, `"Цель выполнена"`, `"[Недоступно:]"`
- `InventoryUI.cs` — `"Выберите предмет..."`, `"Сеть не запущена"`, `"Игрок не найден"`, `"Бросаю..."`

**Изменения (UXML → Loc.Get — 12 контроллеров, ~180 строк):**
- `MarketWindow.cs` — section titles, exchange labels, pack/unpack, qty, ship, toggle
- `CharacterWindow.cs` — 5 отладочных SetMessage(), accept/complete/fail кнопки, section titles (25+), knowledge/quest sub-titles
- `EscMenuWindow.cs` — back button `"← НАЗАД"`, title `"МЕНЮ"`
- `InventoryUI.cs` — wheel title/hint, sublist, кнопки ИСПОЛЬЗОВАТЬ/БРОСИТЬ/ЗАКРЫТЬ
- `CraftingWindow.cs` — `"Рецепты"/"Ингредиенты:"/"В буфере:"`, кнопки крафта
- `CustomisationWindow.cs` — title, 3 section titles, slider labels, body cards, кнопки, статус
- `ShipCargoConsoleWindow.cs` — title format, column headers, `"Паков:"`, кнопки store/retrieve
- `KeybindingsWindow.cs` — title, `"Боевые навыки"/"Действия"`, footer
- `SkillBindingWindow.cs` — title, subtitle, modal title
- `RebindPromptWindow.cs` — `"Переназначение клавиши:"`, hint, cancel
- `QuestTracker.cs` — `"Цель: —"`, `"Скрыть"`
- `CommPanelWindow.cs` — `"Запросить посадку"`, `"Отмена"`

**Новые ключи в UI_Table:** +134 ключа (cargo 8, system 3, character btn 3, + ранее пропущенные)

**Уже было хорошо (проверено, не требует правок):**
- `SkillTreeWindow` — `Loc.Bind`
- `RepairManagerWindow` — `Loc.Get/Loc.Format`
- `NetworkUI` — `Loc.Format`

**Статус:** ✅ Phase 9 завершён.

## Итерация 10 — 2026-08-07

**Задача:** Фикс оставшихся кнопок CharacterWindow + docs update  

**Изменения:**
- `CharacterWindow.cs` — 5 отладочных `SetMessage()` (`"QuestTracker недоступен"`, `"QuestClientState недоступен"`, `"ContractClientState недоступен"` ×3) → `Loc.Get("ui.system.*")`
- `CharacterWindow.cs` — кнопки `_acceptBtn/_completeBtn/_failBtn` → `Loc.Get("ui.character.btn.accept_contract/complete_contract/fail_contract")`
- Добавлены ключи: `ui.system.questtracker_unavailable`, `ui.system.queststate_unavailable`, `ui.system.contractstate_unavailable`, `ui.character.btn.accept_contract`, `ui.character.btn.complete_contract`, `ui.character.btn.fail_contract`

**Статус:** ✅ Итерация 10 завершена.

## Общая сводка

| Фаза | Тикет | Коммит | Ключей |
|---|---|---|---|
| Phase 0+1 — Инфраструктура | LOC-01/02 | `2459055` | — |
| Phase 2 — System-сообщения | LOC-03 | `8012159` | 49 sys.* |
| Phase 3 — UI-строки | LOC-04 | `7b1ff1f`, `df54864` | 70+ ui.* |
| Phase 4 — SO-данные | LOC-05 | `5925200` | 119 static.* |
| Phase 5 — Диалоги | LOC-09 | `010fe9a` | 14 dialogue.* |
| Phase 6 — Инструмент | LOC-10 | `10ee059` | — |
| Phase 8 — Глубокая UI-локализация | LOC-11 | `2046b42` | 87 ui.* |
| Phase 9 — Финальная обёртка | LOC-12 | _pending_ | 134 ui.* |
| Iter 10 — Фикс кнопок | LOC-12 | _pending_ | 6 ui.* |

**Всего: ~420+ ключей в UI_Table + 119 в Static_Table + 49 в System_Table + 14 в Dialogue_Table = ~600 ключей.**
Phase 7 (верификация) — за пользователем (playtests).
Phase 10 (ассеты: скилы/NPC/квесты) — запланирован, не начат.
