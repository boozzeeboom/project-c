# 03 — Расследование: почему фазы локализации сделаны неправильно + план исправления

> **Статус:** ФАКТЫ (проверено по коду, ассетам и исходникам пакета 1.5.12)
> **Дата:** 2026-08-05
> **Связь:** LOC-02…LOC-10; пересматривает выводы `POSTMORTEM-LOC-10.md`
> **Метод:** независимая проверка ассетов (`git show`, YAML .asset, `Library/PackageCache/com.unity.localization@1.5.12`) → сверка с `02_FINAL_DESIGN_AND_PLAN.md`, `ITERATIONS.md`, `POSTMORTEM-LOC-10.md`.

---

## 1. Вердикт (TL;DR)

Одна корневая причина ломает фазы 2–6: **записи добавлялись в ru-таблицы без регистрации ключей в SharedTableData**. Во всех четырёх коллекциях `*_Table Shared Data.asset` → `m_Entries: []`, при этом сами ru-таблицы содержат записи (119/49/75/14 по `m_Id`).

Следствия:
1. **Рантайм-локализация мертва.** `StringTable.GetEntry(string key)` → `FindKeyId(key, false)` → 0 → null (`DetailedLocalizationTable.cs:512`). `Loc.Get(...)` всегда возвращает fallback. Все 252 ключа невидимы.
2. **Нативный CSV-экспорт (готовое решение пакета) выдаёт пустые файлы** — он читает `SharedData.Entries`.
3. **Кастомный тул фазы 6 — симптом-обход этой поломки** через reflection в `m_TableData`, сам по себе нерабочий и по плану не нужный (370 строк вместо тонкой обёртки).
4. **Фаза 5 по сути не выполнена:** T-Q18 (текст → ключ) в SO-данные не записан, 14 записей `Dialogue_Table_ru` без имён ключей; диалоги работают только через fallback.

---

## 2. Корневая причина — механизм (доказательства)

### 2.1 Как записи попали в таблицы без SharedData

Два пути, оба неверные:

**Путь A — ручное редактирование YAML (фазы 2, 3, 5).**
Коммиты `8012159` (+227 строк в `System_Table_ru.asset`), `7b1ff1f` (+191), `df548647` (+124), `010fe9a` (+64) добавляли в `m_TableData` только `m_Id` + `m_Localized`. Имя ключа живёт ТОЛЬКО в `SharedData.m_Entries` — его туда никто не писал. Имена ключей в ассетах **потеряны**.

**Путь B — `table.AddEntry(key, value)` (фаза 4, мигратор).**
`LocalizationStringMigrator.cs` (строки 63, 68, 92, 97, 121, 126, 143, 158, 186, 191, 206, 231, 258) вызывает `AddEntry(string key, string value)`. Реализация: `DetailedLocalizationTable.cs:389` → `FindKeyId(key, true)` → `SharedData.GetId(key, true)` → `AddKeyInternal` — добавляет ключ в SharedData **в памяти**, но **не помечает SharedData-ассет dirty**. `EditorUtility.SetDirty(table)` + `AssetDatabase.SaveAssets()` сохраняют только таблицу → на диске SharedData пуст, а в таблице остаются записи с «осиротевшими» long Id.

> Уточнение к POSTMORTEM: это не «баг/особенность версии `AddEntry`». API корректно добавляет ключ в SharedData в памяти. Проблема — save-дисциплина (нет `SetDirty` на SharedData/collection) и ручное YAML-редактирование, где ключи не добавлялись вообще.

### 2.2 Почему runtime мёртв

```csharp
// DetailedLocalizationTable.cs:512
public TEntry GetEntry(string key)
{
    var keyId = FindKeyId(key, false);   // ищет в SharedData
    return keyId == 0 ? null : GetEntry(keyId);
}
```
`Loc.cs:145` (`table.GetEntry(entryKey)`) → null → fallback. На любом языке показывается fallback (литерал/ключ), т.е. поведение идентично отсутствию локализации.

### 2.3 Почему «проверки» фаз проходили

На ru-языке поломка невидима: fallback = русский литерал = «выглядит ок». Проверочные шаги плана («переключить en → английский») при пустых таблицах дают тот же русский текст — и это не отлавливали как ошибку. Фазы 2–6 помечены ✅ в `ITERATIONS.md` при сломанных данных.

### 2.4 Что уцелело (для восстановления)

| Таблица | Имена ключей | Значения (ru) |
|---|---|---|
| Static (119) | ✅ детерминированы от SO: `static.item.{itemId}.*`, `static.npc.{npcId}.*`, `static.quest.{questId}.*`, `static.faction.*`, `static.market.*`, `static.item_type.{i}` | ✅ в SO-литералах (мигратор читал их же) |
| System (49) | ✅ в коде: `sys.{domain}.{snake}` от enum-кодов (`InventoryClientState.cs:266`, `MarketClientState.cs:182`, `ContractClientState.cs:113`) | ✅ в git-истории удалённых switch/case (напр. `8012159^:InventoryClientState.cs`) |
| UI (75) | ✅ 52 литерала в коде (`ui.esc_menu.*`, `ui.keybindings.*`, `ui.dialog.end`) | ⚠️ старые литералы в git-диффах фаз 3 + 75 значений в YAML (23 «лишних» — разобрать) |
| Dialogue (14) | ❌ не в коде и не в SO — T-Q18 не применён | ⚠️ 14 значений в YAML, но без связи с ключами |

---

## 3. Пофазовая таблица: план vs факт

| Фаза | Тикет | План (§6 02_FINAL) | Факт | Вердикт |
|---|---|---|---|---|
| 0+1 Инфраструктура | LOC-01/02 | Locale + таблицы через Package Manager/официальный API | `LocalizationSetup.cs` использует официальные `CreateLocale` / `LocalizationEditorSettings.CreateStringTableCollection` / `ActiveLocalizationSettings` — **ок**; но: `ProjectLocaleIdentifier=en` вместо ru; `SpecificLocaleSelector(en)` в StartupSelectors; fallback locale ru не настроен; создано 9 локалей (CJK) вопреки Q1 (ru/en/de) | ⚠️ инфраструктура создана правильно, конфиг расходится с планом |
| 2 System-сообщения | LOC-03 | Loc.cs + `sys.*` через API | Код по плану (`Loc.Get("sys.inventory.{snake}")`, серверный локализатор удалён). Данные: **ручная правка YAML** (+227), SharedData пуст | ❌ данные сломаны |
| 3 UI-строки | LOC-04 | `Loc.BindAll` по `data-loc-key` + ключи | `SettingsWidgets.MakeLabel` биндит программные лейблы по ключу (ок); **`data-loc-key`/UXML-стратегия не реализована** — UXML-тексты остаются ru-fallback. Данные: ручной YAML (+191/+124), SharedData пуст | ❌ частично; данные сломаны |
| 4 SO-данные | LOC-05 | Мигратор SO → Static_Table | `LocalizationStringMigrator` через `table.AddEntry(key, value)` без `SetDirty(SharedData)` → 119 записей, SharedData пуст | ❌ API неправильный; но ключи детерминированы — восстановимы 1:1 |
| 5 Диалоги | LOC-09 | T-Q18: `DialogueNode.text` → ключ, литерал → таблица | `DialogWindow.cs` сделан key-aware (`Loc.Get(text, text)` — graceful fallback), но **SO-данные не мигрированы**; 14 записей `Dialogue_Table_ru` без имён ключей; диалоги локализованы только «по fallback» | ❌ не выполнена по сути |
| 6 Инструмент | LOC-10 | Тонкая обёртка над нативным CSV (§5.3), `LocalizationCsvService` | Кастомный CSV-движок: 370 строк reflection в `m_TableData` (`LocalizationToolWindow.cs:118-206`), свой CSV-парсер; `LocalizationCsvService` не написан | ❌ архитектурная ошибка + нерабочий |

**Сквозная причина по фазам:** отсутствие проверки `SharedData.Entries > 0` и нативного export/import round-trip после каждого этапа наполнения.

---

## 4. Сверка с POSTMORTEM-LOC-10

| Утверждение постмортема | Вердикт |
|---|---|
| Пакет уже содержит CSV Export/Import, Google Sheets, XLIFF | ✅ подтверждено (`Editor/Plugins/CSV|Google|Xliff/` в 1.5.12) |
| «Данные не экспортируются» | ✅ подтверждено; причина — пустой SharedData |
| Правильный подход: `sharedData.AddKey(key)` → `table.AddEntry(sharedEntry.Id, value)` | ✅ подтверждено; дополнение — обязателен `EditorUtility.SetDirty` на SharedData и коллекцию |
| Проблема была «баг/особенность версии AddEntry» | ⚠️ уточнение: API корректен; проблема в save-дисциплине + ручном YAML |
| Удалить `LocalizationToolWindow.cs`, использовать штатное окно | ✅ подтверждено |
| Поправить populate-скрипты | ✅ + нужно пересоздание таблиц (ключи потеряны), а не только фикс API |

---

## 5. План исправления

### A. Починить данные — блокер (~2–4ч)

Новый Editor-скрипт `LocalizationTableRepair.cs`, меню `ProjectC → Localization → Rebuild Tables (SharedData fix)`. Для каждой коллекции: **очистить** `m_TableData` + `SharedData` (иначе останутся orphan-записи со старыми Id), затем наполнить правильным API:

1. **Static_Table** — перезапуск исправленного мигратора:
   `sharedData.AddKey(key)` → `sharedEntry.Id` → `table.AddEntry(sharedEntry.Id, value)`; затем `EditorUtility.SetDirty(table)`, `SetDirty(collection.SharedData)`, `SetDirty(collection)`, `AssetDatabase.SaveAssets()`. Ключи детерминированы от SO → 119 ключей восстановимо 1:1, значения — из SO-литералов.
2. **System_Table** — rebuild из enum-кодов (`InventoryResultCode`, `MarketResultCode`, `ContractResultCode`): ключ `sys.{domain}.{Loc.ToSnakeCase(code)}`, значение — RU-карта из git-истории удалённых switch/case (список можно подготовить точным).
3. **UI_Table** — rebuild из 52 ключей-литералов кода; значения — сверка с git-диффами фаз 3 (старый литерал → ключ) либо из YAML по порядку с ручной сверкой. 23 «лишних» значения разобрать (вероятно заготовки под character/market/toast — решить, подключать ли).
4. **Dialogue_Table** — rebuild из DialogTree SO по схеме плана §5.1 (`dialogue.{treeId}.{nodeId}.text`, `.edge.{i}.label`), значения = `node.text` литералы; **попутно применить T-Q18** (`node.text` → ключ в SO), иначе диалоги останутся на fallback.

Контроль после каждого шага: `SharedData.Entries` == ожидаемому; нативный Export CSV даёт непустой файл.

### B. Убрать кастомный тул, подключить нативное решение (~1–2ч)

5. Удалить `Assets/_Project/Editor/Localization/LocalizationToolWindow.cs` (+ `.meta` через Unity).
6. Рабочий процесс — штатное окно `Window → Asset Management → Localization Tables` → коллекция → `⋮`/контекст → **Export → CSV…** / **Import → CSV…** (колонки Id/Type/Comment отключаются в настройках экспорта). Формат `Key | ru | en | de | …` — нативный, без кастомного парсера.
7. Совместный перевод — встроенный **Google Sheets Provider** (там же, `⋮ → Google Sheets…`).
8. Опционально тонкая обёртка `LocalizationCsvService.ExportAll()` — только вызов нативного экспорта для 4 коллекций («одна кнопка»), БЕЗ парсера/рефлексии. Нужна ли — вопрос пользователю.

### C. Конфиг LocalizationSettings (~0.5ч)

9. `m_ProjectLocaleIdentifier` → `ru`.
10. StartupSelectors: убрать `SpecificLocaleSelector(en)` (оставить System + CommandLine) — иначе стартовый язык en до бутстрапа.
11. Fallback locale = `ru` (LocalizedStringDatabase), включить `UseFallback` — чтобы пустая en-ячейка падала на ru, а не на ключ.
12. Судьба 9 локалей: план §9 Q1 = ru/en/de; CJK (zh/ja/hi) без шрифтов не отображается — либо удалить Locale-ассеты, либо отдельный тикет на шрифты.

### D. Процесс — чтобы не повторить

13. **Не редактировать .asset YAML вручную** (таблицы/SharedData) — только через API пакета или нативное окно. Правило уровня AGENTS.md («как с .meta»).
14. «Проверка» фазы = фактическая: `SharedData.Entries > 0` + нативный export/import round-trip + смена языка в Play Mode. ru-fallback маскирует поломку.
15. Перед написанием кастомного Editor-инструмента — открыть штатный UI пакета (урок POSTMORTEM зафиксировать в правилах, а не только в постмортеме).
16. Фаза помечается ✅ только после сквозной проверки (в ITERATIONS фазы 2–6 ✅ при мёртвых данных).

---

## 6. Команды верификации (пользователь)

```powershell
# 1. Перекомпиляция
#    Unity → Console → 0 errors

# 2. SharedData наполнен (после ремонта)
#    Window → Asset Management → Localization Tables
#    → 4 коллекции; в каждой число ключей > 0 (Static 119, System 49, UI 52–75, Dialogue 14)

# 3. Нативный экспорт непустой
#    Localization Tables → коллекция → ⋮ → Export → CSV… → файл содержит Key + ru + en…, строк == ключей

# 4. Runtime переключение
#    Play Mode (Bootstrap/WorldScene_0_0) → EscMenu → Настройки → Язык → English
#    → меню на англ.; пустые ячейки → ru fallback

# 5. Round-trip
#    Export CSV → заполнить en вручную → Import CSV → смена языка → переводы применились
```

---

## 7. Открытые вопросы

- **Q1.** Удалять `LocalizationToolWindow` полностью или оставить тонкую обёртку (Export All одной кнопкой поверх нативного CSV)? План §5.3 предусматривал обёртку.
- **Q2.** Google Sheets подключать сейчас (нужен Google API credential) или начать с CSV-файлов?
- **Q3.** 9 локалей или 3 (ru/en/de)? CJK без шрифтов.
- **Q4.** 23 «лишних» значения в `UI_Table_ru` (75 записей vs 52 ключа в коде) — заготовки под Character/Market/Toast? Подключать в код или удалить?
- **Q5.** Фаза 5: применять T-Q18 сейчас (node.text → ключ в SO) или оставить `Loc.Get(text, text)` как есть до полного rollout диалогов?
