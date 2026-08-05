# Локализация Project C: The Clouds — ФИНАЛЬНЫЙ дизайн и план реализации

> **Статус:** УТВЕРЖДАЕТСЯ (после обсуждения открытых вопросов §9)
> **Дата:** 2026-08-05
> **Источники:** независимое исследование кодовой базы (file:line ниже) + сверка с `00_PLAN.md` (черновик) и `01_AUDIT_HardcodedStrings.md` (аудит).
> **Пакет:** `com.unity.localization` **1.5.12 УЖЕ УСТАНОВЛЕН** (Packages/manifest.json:17) — в отличие от того, что написано в 00_PLAN.md §0.3 («НЕ установлен»).
> **Тикеты:** новые тикеты LOC-01…LOC-12 (для комментариев в коде и чек-листов). Существующие T-Q18 / T-ESC02 / M19-T2 сохраняются.

---

## 1. Резюме (verdict)

Локализации в проекте нет: **все строки хардкожены на русском** в трёх слоях:

| Слой | Объём | Где |
|---|---|---|
| UXML (UI Toolkit) | ~241 `text="..."` в 18 файлах | `Assets/_Project/**/Resources/UI/*.uxml` |
| C# runtime | ~2331 строк с кириллицей (часть — комментарии/логи, но большинство — user-facing) | `Assets/_Project/Scripts/**`, `Trade/Scripts/**`, `Quests/**` |
| ScriptableObject-данные | предметы (113 TradeItem + ItemRegistry), NPC, квесты, фракции, навыки, рынки | `Assets/_Project/Data/**`, `Quests/Data/**`, `Trade/Data/**` |

**Решение: единая система на Unity Localization 1.5.12** (уже в manifest), без кастомного парсера. Ключевые принципы:

1. **Ключи вместо строк в данных** — там, где есть стабильный ID (`questId`, `npcId`, `itemId`), ключ **выводится из ID** на лету: `static.npc.mira.displayName`. Никаких новых полей в SO — нулевая миграция существующих ассетов, fallback = литерал.
2. **Сервер шлёт ID/коды, клиент локализует** — правило уже заложено в архитектуре (DialogStepDto «T-Q18: localization key», аудит §1.4 «сервер должен слать ТОЛЬКО коды»).
3. **Runtime-переключение** — `LocalizationSettings.SelectedLocale` + персист в `SettingsManager` (новый ключ `Settings.Locale`), выбор языка в EscMenu через существующий `SettingsWidgets.CreateDropdown`.
4. **Инструмент переводчика — CSV round-trip** поверх нативного CSV пакета: простейшая таблица `Key | ru | en | de | ...`, кнопка «Выгрузить всё» и «Загрузить». Переводчику не нужно открывать Unity.
5. **Аддитивность** — ничего не ломается: отсутствие перевода → fallback ru → литерал. Миграция идёт по одному подсистемному слою за раз.

Оценка: **~30–40 часов** на полный rollout (ниже §7 — пофазна).

---

## 2. Независимое исследование кодовой базы (факты)

### 2.1 Инфраструктура, которая уже готова под локализацию

| Факт | Evidence |
|---|---|
| Пакет `com.unity.localization` 1.5.12 установлен | `Packages/manifest.json:17` |
| SO-поля помечены «loc key в будущем» — архитекторы закладывали локализацию | `QuestDefinition.cs:26,29` (`displayName`, `description`), `NpcDefinition.cs:66,144,162` (`displayName`, `greetingText`, `voicePrefix`), `DialogueNode.cs:28,66` (`label`, `text`), `QuestObjective.cs:37` |
| `DialogTree.localizationTable` (TextAsset) — заготовка под CSV-таблицу диалогов | `DialogTree.cs:39` |
| Сетевой слой уже передаёт ID, а не текст: `QuestProgressDto.questId`, `ObjectiveProgressDto.objectiveId`, `DialogStepDto.speakerNpcId` | `QuestProgressDto.cs:10-23`, `DialogStepDto.cs:9-14` |
| `DialogStepDto.speakerText` явно помечен «T-Q18: localization key» — ключ поверх сети | `DialogStepDto.cs:12` |
| `SettingsManager` — статический синглтон с PlayerPrefs, готовое место для `Locale` | `SettingsManager.cs:13-198` |
| Плейсхолдер выбора языка уже стоит в настройках | `GameplaySettingsSection.cs:42` («Выбор языка будет доступен после внедрения локализации.») |
| CSV-тулинг — устоявшийся паттерн проекта: QuestCsvImporter/Exporter/NpcCsvImporter/QuestCsvWindow | `Assets/_Project/Quests/Editor/*.cs` |
| UI Toolkit окна — единый паттерн Clear+CloneTree + `Resources.Load` fallback | `EscMenuWindow.cs:51-70`, `CharacterWindow.cs` header |
| `SettingsWidgets.CreateDropdown(label, choices, ...)` — готовый виджет для выбора языка | `SettingsWidgets.cs:104` |

### 2.2 Где живут тексты (полный реестр подсистем)

**A. UXML-статика (18 файлов, ~241 `text=`):**
`CommPanel.uxml`, `DialogWindow.uxml`, `QuestTracker.uxml`, `CustomisationWindow.uxml`, `EscMenuWindow.uxml`, `KeybindingsWindow.uxml`, `RebindPromptWindow.uxml`, `RepairManagerWindow.uxml`, `SkillBindingWindow.uxml`, `SkillTreeWindow.uxml`, `ShipHud.uxml`, `ShipHudPanel.uxml`, `MarketWindow.uxml`, `CraftingWindow.uxml`, `CharacterWindow.uxml` (57 text), `InventoryWheel.uxml`, `ShipCargoConsoleWindow.uxml`.

**B. C# runtime-строки (главные кластеры):**
- `CharacterWindow.cs` (3777 строк): фильтры «Все/Активные/Завершённые», статусы, типы контрактов «Обычный/Срочный/Квитанция», ранги «Примум/Секундус/Терциус/Квартус», `"Активных: {N}"`, `"Кредиты: {N} CR"` — `CharacterWindow.cs:54,329-332,358-359,383,396,464-466,492`
- `MarketWindow.cs`: `"Ошибка: "`, `"Рынок: {displayName}"`, `"{displayName} — {price} CR (сток: {stock})"` — `MarketWindow.cs:549,584,604,633,824`
- `ContractsTab.cs`: дубли фильтров + ранги контрактов — `ContractsTab.cs:54,188-189,329-332,358-359`
- EscMenu секции: `GameplaySettingsSection.cs:23-38`, `AudioSettingsSection.cs`, `GraphicsSettingsSection.cs`
- `KeybindingsWindow.cs:147-226`: «Настройки клавиш», «СОХР/ЗАГР/СБРОС», «Боевые навыки», «Действия», «ЛКМ/ПКМ/СКМ»
- `CraftingWindow.cs` (15 строк), `KnowledgeToast.cs:189-247` (тосты «Навык/Рецепт/Фракция»), `GatheringToastController.cs`, `MetaRequirementToast.cs`
- Legacy uGUI/TMP HUD: `AltitudeUI.cs`, `ControlHintsUI.cs`, `HUDManager.cs`, `PeakNavigationUI.cs`, `NetworkUI.cs`, `ConfirmationDialog.cs`, `UIFactory.cs`
- `DialogWindow.cs:364-389,414`: подставляет `speakerNpcId` как имя NPC (сырой ID!) и `[Недоступно: {reason}]`

**C. ScriptableObject-данные:**
- `TradeItemDefinition.displayName` — **113 ассетов** (`Trade/Data/Items/`, 14 рынков `Trade/Data/Markets/`)
- `ItemData.itemName/description` — `ItemType.cs:23-29`, `ItemTypeNames._names[]` — `ItemTypeNames.cs` (8 категорий: «Ресурсы», «Оборудование»...)
- `NpcDefinition.displayName/greetingText` — 2+ NPC (`Quests/Data/Npcs/`)
- `QuestDefinition.displayName/description`, `QuestStage.description`, `QuestObjective.description` — `Quests/Data/Quests/`
- `FactionDefinition.displayName/loreDescription` + `ReputationTier.tier` — `FactionDefinition.cs:68,80`
- `SkillNodeConfig.displayName` — `SkillNodeConfig.cs:100` (читается в `SkillTreeWindow.cs:403,472`)
- `ConstellationData.Constellation.localizedName` — уже названо правильно
- `MarketConfig.displayName` — `MarketConfig.cs:29`

### 2.3 Критичные архитектурные детали (влияют на дизайн)

1. **`QuestServer.BuildQuestSnapshot` шлёт `displayName`/`description` поверх сети** — `QuestServer.cs:965,985`. Клиент их показывает (`QuestTracker.cs:283`). Для локализации правильнее: сервер шлёт только `questId`/`objectiveId`/`currentStageId` (уже есть), клиент резолвит текст локально. Поля DTO остаются (обратная совместимость), клиент просто перестаёт их использовать.
2. **Описания целей с количеством** — сервер шлёт `currentValue`/`requiredQuantity` (`QuestServer.cs:977-978`). Текст цели — шаблон: `Loc.Format("static.quest.{q}.stage.{stage}.obj.{obj}", cur, req)` + Smart String плюрализация для русского.
3. **Диалоги** — `DialogStepDto.speakerText` это `DialogueNode.text` с сервера. После миграции `text` = ключ `dialogue.{treeId}.{nodeId}.text` (T-Q18). `unavailableReason` — код вместо сырой строки.
4. **Host-режим**: сервер и клиент в одном билде. Серверные строки (Debug.Log, валидация) НЕ локализуем — только client-facing. Правило «сервер шлёт коды» уже частично реализовано, но в `ContractServer.cs:408-411` есть дубликат-локализатор — удалить (см. аудит §1.4).
5. **`DialogWindow` показывает сырой `speakerNpcId` вместо имени** — `DialogWindow.cs:365`. Это и баг, и возможность: имя NPC должно резолвиться через `static.npc.{id}.displayName`.

---

## 3. Сверка с первичными анализами

| Пункт 00_PLAN / 01_AUDIT | Вердикт | Комментарий |
|---|---|---|
| «Пакет НЕ установлен» | ❌ **УСТАРЕЛО** | Установлен 1.5.12 (manifest.json:17). Этап 1.1 черновика выполнен. |
| 4 таблицы: Static/UI/Dialogue/System | ✅ **Подтверждаю** | Оптимально по частоте обновления и ответственности. |
| Схема ключей `{domain}.{type}.{id}.{field}` | ✅ **Подтверждаю** | Уточнение: ключ **выводится из ID на лету**, без полей `*LocKey` в каждом SO (см. §4.2). |
| Аддитивная миграция (locKey рядом с литералом) | ✅ **Принцип верен** | Реализуем дешевле: derive-from-ID, явные поля только там, где нет ID. |
| Инструмент: Editor Window + CSV | ✅ **Подтверждаю** | Упрощение: используем нативный CSV пакета + тонкую обёртку «одна кнопка» (см. §5). |
| «Сервер шлёт только коды» | ✅ **Подтверждаю** | Дополнительно: `DialogStepDto.speakerText` → ключ (T-Q18). |
| 01_AUDIT реестр строк | ✅ **Точен** | Все перечисленные файлы и строки подтверждены grep-ом в §2.2. |
| UI Toolkit binding «Способ B programmatic» | ✅ **Подтверждаю** | + план по UXML: атрибут `data-loc-key`, текст в UXML остаётся fallback'ом (см. §4.5). |
| Риск «LocalizedString не обновляется в дереве» | ✅ **Реальный** | Решение: `Loc.Bind` через `StringChanged` + пересборка окон при смене языка (паттерн EnsureBuilt уже пересоздаёт UI). |
| §7 out of scope (Addressables, RTL, CJK, озвучка) | ✅ **Подтверждаю** | + уточнение: шрифты для en/de/fr — не нужны (кириллица уже покрывает Latin). |
| Оценка ~35 часов | ⚠️ **Уточняю** | ~30–40ч с учётом уже установленного пакета и derive-подхода (меньше миграций). |

**Новые находки, которых НЕТ в первичных анализах:**
- `DialogTree.localizationTable` (TextAsset) уже существует — решить судьбу (рекомендация: использовать как источник диалоговых CSV-ключей или удалить после миграции, см. §9 Q5).
- `QuestProgressDto` уже несёт `questId`+`objectiveId` — клиентский локализующий резолвер не требует изменения протокола.
- 113 TradeItem — самый большой блок контента, требует скрипт миграции (не вручную).
- `DialogWindow.cs:365` показывает сырой `speakerNpcId` — фикс заодно с локализацией имён.
- В `CharacterWindow`/`ContractsTab` дублируются строки фильтров и типов контрактов — локализация должна идти через общий хелпер, чтобы не разойтись.

---

## 4. Архитектура (финал)

### 4.1 Состав

```
Assets/_Project/Settings/Localization/
├── LocalizationSettings.asset          ← создаётся через Package Manager (Localization)
├── Locale_ru.asset                     ← source locale
├── Locale_en.asset                     (+ de, fr по решению §9 Q1)
├── Static_Table.asset                  ← предметы, NPC, квесты, фракции, навыки, рынки, созвездия
├── UI_Table.asset                      ← UXML + runtime UI строки
├── Dialogue_Table.asset                ← реплики и выборы
└── System_Table.asset                  ← коды результатов, ошибки

Assets/_Project/Scripts/Localization/   ← namespace ProjectC.Localization
├── Loc.cs                              ← хелпер: Get/Format/Bind, авто-роутинг по префиксу
├── LocaleSelector.cs                   ← SetLocale/Load, обёртка над LocalizationSettings
└── (в SettingsManager) Locale = PlayerPrefs "Settings.Locale"

Assets/_Project/Editor/Localization/
├── LocalizationToolWindow.cs           ← «Выгрузить всё» / «Загрузить CSV» (одна кнопка каждая)
├── LocalizationStringMigrator.cs       ← SO-литералы → ru-таблица + ключи
└── LocalizationCsvService.cs           ← merge/validation поверх нативного CSV пакета
```

### 4.2 Схема ключей и derive-from-ID

Префикс определяет таблицу (`Loc.Get` роутит автоматически):

| Префикс | Таблица | Формат | Пример |
|---|---|---|---|
| `static.` | Static | `static.{type}.{id}.{field}` | `static.item.mesium_canister_v01.name`, `static.npc.mira.displayName`, `static.quest.find_artifact.description`, `static.quest.find_artifact.stage.{stageId}.obj.{objectiveId}`, `static.faction.{factionId}.tier.{index}` |
| `ui.` | UI | `ui.{window}.{element}` | `ui.esc_menu.gameplay.title`, `ui.inventory.filter.all`, `ui.character.contracts.primium` |
| `dialogue.` | Dialogue | `dialogue.{treeId}.{nodeId}.text` / `.edge.{i}.label` | `dialogue.mira_default.greeting.text` |
| `sys.` | System | `sys.{domain}.{code}` | `sys.inventory.full`, `sys.contract.not_found` |

**Runtime-резолвер** (ядро — никаких новых полей в SO):

```csharp
// Loc.cs — выжимка API
public static class Loc
{
    public static string Get(string key, string fallback = null);          // авто-таблица по префиксу
    public static string Format(string key, params object[] args);         // Smart String + плюрализация
    public static string Get(string table, string key, string fallback);   // явная таблица
    public static bool TryGet(string key, out string value);
    public static void Bind(Label l, string key, string fallback = null);       // UI Toolkit live-binding
    public static void Bind(TextMeshProUGUI t, string key, string fallback = null); // legacy HUD
    public static event Action OnLocaleChanged;                            // обёртка OnSelectedLocaleChanged
}

// Использование для данных:
string name = Loc.Get($"static.npc.{npc.npcId}.displayName", npc.displayName);
string obj  = Loc.Format($"static.quest.{q.questId}.stage.{stageId}.obj.{objectiveId}", cur, req);
```

Fallback-цепочка: перевод → ru (fallback locale) → переданный литерал → сам ключ. Ничего не падает.

**Явные `*LocKey` поля добавляем ТОЛЬКО там, где нет стабильного ID:**
- `ItemTypeNames._names[]` (static array) → ключи `static.item_type.{index}` или конфиг-таблица
- `ReputationTier.tier` → derive по индексу `static.faction.{factionId}.tier.{index}` (порядок стабилен)
- Диалоги: `DialogueNode.text` СТАНОВИТСЯ ключом (T-Q18), старый литерал уезжает в ru-таблицу при миграции.

### 4.3 Сеть: ключи поверх провода

| DTO | Сейчас | После | Breaking? |
|---|---|---|---|
| `QuestProgressDto` | шлёт `displayName`/`description` | шлёт (для совместимости), клиент резолвит по `questId`/`objectiveId` | нет |
| `ObjectiveProgressDto` | шлёт `description` | клиент форматирует сам по `objectiveId`+`currentValue`/`requiredQuantity` | нет (поля остаются) |
| `DialogStepDto.speakerText` | литерал | ключ `dialogue.{treeId}.{nodeId}.text` (T-Q18) | условно: клиент `Loc.Get(text, text)` — старый литерал работает как fallback |
| `DialogOptionDto.unavailableReason` | русская строка с сервера | код `{domain}.{reason_code}` | да, синхронизировать сервер+клиент |
| `ContractServer.ContractClientState_LocalizeResultCode` | дубликат-локализатор | **удалить**, слать только код | нет (клиент уже умеет) |

Принцип: **сервер никогда не локализует**; исключение — только Debug.Log (не локализуем).

### 4.4 Runtime-переключение языка

1. `SettingsManager.Locale` (string, PlayerPrefs `Settings.Locale`), дефолт `ru`.
2. При старте (статический ctor SettingsManager / `LocalizationBootstrap.Awake` в BootstrapScene до первого UI): `LocalizationSettings.SelectedLocale = LocaleSelector.GetLocale(saved)`.
3. Смена: `LocaleSelector.SetLocale(code)` → `LocalizationSettings.SelectedLocale = ...` → `PlayerPrefs` → событие.
4. Все `Loc.Bind`-метки обновляются автоматически (`StringChanged`). Окна с разовой отрисовкой (CharacterWindow, MarketWindow) — пересоздать (EnsureBuilt уже это делает при открытии; на открытом окне — подписка на `Loc.OnLocaleChanged` → пересборка контента).
5. UI: `GameplaySettingsSection` — заменить плейсхолдер (`GameplaySettingsSection.cs:42`) на `SettingsWidgets.CreateDropdown("Язык", locales, ...)`.

### 4.5 UI Toolkit: стратегия UXML

**Рекомендация: «UXML-текст = fallback, runtime-привязка по атрибуту».**

- В UXML у значимых Label'ов добавляем `data-loc-key="ui.esc_menu.gameplay.title"` (атрибут, текст остаётся русским).
- После `CloneTree()` в каждом окне вызываем `Loc.BindAll(root)` — обход `Label`, у кого есть `data-loc-key` → `Loc.Bind(label, key, label.text)`.
- Плюсы: 241 строка UXML не переписывается (fallback уже там), переключение live, дебаг по имени атрибута, миграция окно-за-окном.
- Альтернатива «text=\"@key\"» в UXML (нативная подстановка пакета) — проверить в спринте на одном Label (`EscMenuWindow.uxml`), если работает чисто — использовать для новых окон (см. §9 Q4).

### 4.6 Legacy uGUI/TMP HUD

`AltitudeUI`, `ControlHintsUI`, `HUDManager`, `NetworkUI`, `ConfirmationDialog`, `UIFactory` используют `TextMeshProUGUI`. Для них: `Loc.Bind(TextMeshProUGUI, ...)` (тот же `StringChanged`-паттерн). Объём мал (подсказки, статусы) — делается в том же этапе, что и UI-строки.

---

## 5. Инструмент переводчика (финальный дизайн)

**Цель (по ТЗ):** «переводчик не копается в ассетах — выгрузил таблицу, заполнил, загрузил».

### 5.1 Формат таблицы

```
Key,ru,en,de
static.item.mesium_canister_v01.name,Мезий в канистре,Mezium Canister,
static.item.mesium_canister_v01.description,"Мезий — протекает при столкновении.",Mezium leaks on impact.,
ui.esc_menu.gameplay.title,Управление,Controls,
```

- Первая колонка — `Key`, остальные — языки (заголовок = locale code).
- Пустая ячейка = «перевода нет» → fallback ru.
- Многострочность/запятые — стандартный CSV quoting.
- Это **нативный формат экспорта Unity Localization** (колонки `Id/Type/Comment` отключаются чекбоксами) — не кастомный парсер.

### 5.2 Workflow

1. Разработчик: `ProjectC → Localization → Export All CSV` → файл(ы) в `Assets/_Project/Localization/Export/` (или на рабочий стол), папка открывается автоматически.
2. Отправка переводчику (файл / Google Sheets через File→Import).
3. Переводчик заполняет колонки en/de/... — больше ничего не трогает.
4. Разработчик: `ProjectC → Localization → Import CSV` → preview diff (новые/изменённые/удалённые) → Apply → таблицы обновлены.
5. В рантайме: смена языка в EscMenu → всё на выбранном языке. В редакторе: таблицы перечитываются автоматически.

### 5.3 Что делает обёртка (минимально, без костылей)

| Возможность | Реализация |
|---|---|
| «Выгрузить всё» (все 4 таблицы, все локали, одна кнопка) | `LocalizationCsvService.ExportAll()` → вызов нативного CSV экспорта пакета для каждой таблицы (формат: Key + locale колонки, без Id/Type/Comment) |
| «Загрузить CSV» | нативный импорт + валидация: неизвестные ключи (warning), ключи без перевода (info), мусорные строки (error) |
| Отчёт | количество обновлено/добавлено/пропущено, список проблем |
| Авто-открытие папки | `EditorUtility.RevealInFinder` |
| (Опция) Google Sheets | экспорт .tsv для вставки; импорт из скачанного .tsv — см. §9 Q3 |

**Почему не пишем свой CSV-движок:** Unity Localization уже делает round-trip (включая smart strings и метаданные). Обёртка — это только UX «одна кнопка» + валидация + merge по всем таблицам.

---

## 6. Пошаговый план реализации

> Правило: каждый этап = маленький diff → `refresh_unity` (force, compile) → `read_console` → рекомендация пользователю проверить. Коммиты делает пользователь.

### Phase 0 — Preflight (LOC-01, ~0.5ч)
- [ ] 0.1 Проверить, что `com.unity.localization` 1.5.12 в manifest и нет ошибок импорта (`read_console` errors=0).
- [ ] 0.2 Создать папки `Assets/_Project/Settings/Localization/`, `Assets/_Project/Scripts/Localization/`, `Assets/_Project/Editor/Localization/`, `Assets/_Project/Localization/Export/`.
- [ ] 0.3 Зафиксировать набор языков (см. §9 Q1) — влияет на создание Locale-ассетов.
- **Проверка:** Unity открыт, консоль 0 errors.

### Phase 1 — База: Locale + таблицы + переключение (LOC-02, ~2-3ч)
- [ ] 1.1 Создать `LocalizationSettings.asset` (Package Manager → Localization → Settings), `Locale_ru` (source), `Locale_en` (+ по решению Q1); `LocaleGenerator` генерирует.
- [ ] 1.2 Создать 4 StringTable Collection: Static/UI/Dialogue/System в `Assets/_Project/Settings/Localization/`.
- [ ] 1.3 `LocaleSelector.cs` (`ProjectC.Localization`): `SetLocale(code)`, `LoadSaved()`, сохранение через `SettingsManager.Locale`.
- [ ] 1.4 `SettingsManager`: поле `Locale` (PlayerPrefs `Settings.Locale`, дефолт `"ru"`), применяется в `ApplyAll()`.
- [ ] 1.5 Бутстрап: в `NetworkManagerController`/`UIManager.Awake` (BootstrapScene) — `LocaleSelector.LoadSaved()` ДО первого UI (порядок Awake: `UIManager` -200 → EscMenu -150 → окна 0; проверить фактический порядок).
- [ ] 1.6 `GameplaySettingsSection.cs:42`: плейсхолдер → `SettingsWidgets.CreateDropdown("Язык/Language", ["Русский","English",...], ...)` → `LocaleSelector.SetLocale`.
- **Проверка:** EscMenu → язык меняется → заголовки, переключаются у локализованных элементов (пока 1-2 тестовых ключа в UI_Table).

### Phase 2 — Loc-хелпер + System-сообщения (LOC-03, ~3ч)
- [ ] 2.1 `Loc.cs`: Get/Format/Bind + авто-роутинг префиксов + `OnLocaleChanged`.
- [ ] 2.2 `sys.*` ключи по реестру 01_AUDIT §1 (inventory 11, market ~15, contract 15, итого ~41 ключ) → System_Table (ru заполнена, en пустая).
- [ ] 2.3 Заменить `InventoryClientState.LocalizeResultCode` (`Items/Client/InventoryClientState.cs:264-280`) на `Loc.Get($"sys.inventory.{code}")`.
- [ ] 2.4 То же: `MarketClientState.LocalizeResultCode`, `ContractClientState.LocalizeResultCode`.
- [ ] 2.5 **Удалить** `ContractServer.ContractClientState_LocalizeResultCode` (`ContractServer.cs:408-411`) — сервер шлёт код.
- [ ] 2.6 `InventoryWorld.cs` error-строки → коды (аудит §2): `Fail(...)` только с кодом.
- **Проверка:** подбор предмета/торговля/контракты → сообщения на ru; переключили en → английский (пустые → ru fallback).

### Phase 3 — UI-строки: UXML + runtime UI (LOC-04, ~5-6ч)
- [ ] 3.1 `Loc.BindAll(root)` (обход `data-loc-key`) + `Loc.Bind(TextMeshProUGUI,...)`.
- [ ] 3.2 EscMenu: `GameplaySettingsSection`, `AudioSettingsSection`, `GraphicsSettingsSection`, `EscMenuWindow` (навигация) — ключи `ui.esc_menu.*`; UXML-кнопки «ПРОДОЛЖИТЬ/НАСТРОЙКИ/...» — `data-loc-key`.
- [ ] 3.3 `KeybindingsWindow`/`SkillBindingWindow`/`RebindPromptWindow` — `ui.keybindings.*` (включая «ЛКМ/ПКМ/СКМ»).
- [ ] 3.4 `CharacterWindow` + `ContractsTab` (общие ключи для общих строк): фильтры `ui.character.filter.*`, типы контрактов `ui.contract.type.standard/urgent/receipt`, ранги `ui.contract.rank.primium/...`, `ui.character.credits`, `ui.character.reputation.*`.
- [ ] 3.5 `MarketWindow`: `ui.market.*` (ошибки, «Рынок:», строки строк таблиц) — ~20 строк.
- [ ] 3.6 `CraftingWindow` (15), тосты (`KnowledgeToast`, `GatheringToastController`, `MetaRequirementToast`) — `ui.toast.*`.
- [ ] 3.7 Legacy HUD: `ControlHintsUI` (подсказки), `HUDManager`, `AltitudeUI`, `PeakNavigationUI`, `NetworkUI`, `ConfirmationDialog` — `ui.hud.*`.
- [ ] 3.8 `DialogWindow` фиксы: `_npcNameLabel` → `Loc.Get($"static.npc.{speakerNpcId}.displayName", speakerNpcId)`; `[Недоступно: ...]` → `ui.dialog.unavailable` + причина из кода; `[Конец]` → `ui.dialog.end`.
- **Проверка:** пройти все окна на ru (визуально идентично) → переключить en → все тексты англ. (пустые → ru).

### Phase 4 — SO-данные (LOC-05..08, ~8-10ч)
- [ ] 4.1 **Мигратор** `LocalizationStringMigrator` (Editor): сканирует ассеты, для каждого текстового поля генерирует ru-запись в Static_Table по derive-ключу; **сами SO не меняет** (ключ выводится из ID). Отчёт: ключей создано, дубликатов, полей без ID (список на ручной разбор).
- [ ] 4.2 Прогнать мигратор по: `TradeItemDefinition` (113), `ItemData`/`ItemTypeNames`, `NpcDefinition`, `QuestDefinition` (+ stages/objectives), `FactionDefinition` (+ tiers), `SkillNodeConfig`, `ConstellationData`, `MarketConfig` (14).
- [ ] 4.3 Runtime-чтение: пройти все места чтения `displayName`/`itemName`/`description` → `Loc.Get(derive-key, literal)`:
  - `MarketWindow.cs:549,584,604,633,824`, `QuestTracker.cs:283,289`, `SkillTreeWindow.cs:403,472`, `CharacterWindow` списки, `KnowledgeToast` (`GetSkillDisplayName`/`GetRecipeDisplayName`/`GetFactionDisplayName`).
- [ ] 4.4 Шаблонные строки: objective-тексты с количеством → `Loc.Format` + Smart String плюрализация (ru: 1/2/5 форм).
- [ ] 4.5 Поля без ID (ItemTypeNames, ReputationTier) — явные ключи (`static.item_type.*`, `static.faction.{id}.tier.{index}`).
- **Проверка:** журнал квестов, рынок, персонаж, дерево навыков — тексты данных на ru; en — где переведено.

### Phase 5 — Диалоги: ключи поверх сети (LOC-09, ~4ч)
- [ ] 5.1 Миграция DialogTree: для каждого `DialogueNode.text`/`DialogueEdge.label` → ключ `dialogue.{treeId}.{nodeId}.text` / `.edge.{i}.label`, литерал → ru-таблица (Dialogue_Table). Сам `text` становится ключом (T-Q18). `useLocKey`-флаг НЕ нужен — `Loc.Get(text, text)` (литерал-фолбэк работает и для старых деревьев).
- [ ] 5.2 `DialogServer`/обработчик шага: `speakerText` = ключ (уже читает `node.text` — изменения минимальны); `unavailableReason` → код.
- [ ] 5.3 `DialogWindow`: `Loc.Get(speakerText, speakerText)` для текста и кнопок.
- [ ] 5.4 Решить судьбу `DialogTree.localizationTable` (TextAsset) — §9 Q5.
- **Проверка:** диалог с NPC (Mira) — ru идентично; en — реплики англ.

### Phase 6 — Инструмент переводчика (LOC-10, ~3-4ч)
- [ ] 6.1 `LocalizationCsvService`: ExportAll (4 таблицы → CSV «Key + локали», без метаданных; опционально .tsv) / Import (валидация + отчёт).
- [ ] 6.2 `LocalizationToolWindow` (`ProjectC → Localization → Export/Import`): кнопки «Выгрузить всё», «Выбрать CSV», «Загрузить», отчёт, `RevealInFinder`.
- [ ] 6.3 Валидация перед билдом (Editor): ключи без перевода на всех supported локалях → warning список (можно в `LocalizationToolWindow` отдельной кнопкой «Проверить покрытие»).
- **Проверка:** Export → заполнить en вручную → Import → переключить язык → работает. Round-trip без потерь.

### Phase 7 — Верификация и полировка (LOC-11..12, ~3-4ч)
- [ ] 7.1 Полный чек-лист экранов на ru/en: EscMenu, Character (5 табов), Market, Crafting, InventoryWheel, ShipHud, Dock/CommPanel, Dialog, QuestTracker, Keybindings, Customisation, RepairManager, SkillTree, toasts.
- [ ] 7.2 Runtime switch на открытых окнах (без перезахода) — проверка `Loc.Bind` + пересборки.
- [ ] 7.3 Fallback-тесты: удалить ключ → показывается ru → удалить ru → литерал.
- [ ] 7.4 Сеть: сервер шлёт коды (нет русских строк в DTO), клиент локализует; host + 2 клиента.
- [ ] 7.5 CSV round-trip: export → правка → import → смена языка → без багов.
- [ ] 7.6 Производительность: `Loc.Get` в горячих циклах (HUD, трекер) — кэш строк, без аллокаций на кадр.
- **Проверка:** Play Mode full flow (подбор → инвентарь → экипировка → атака + диалог + торговля) на двух языках.

---

## 7. Оценка и приоритеты

| Приоритет | Этап | Часы | Эффект |
|---|---|---|---|
| 🔴 P0 | 0-1: база (locale, таблицы, переключение) | 3.5 | Язык выбирается, инфраструктура готова |
| 🔴 P0 | 6: инструмент переводчика | 4 | Переводчику можно работать уже после P0 (все 4 таблицы пустые/ru) |
| 🟡 P1 | 2: System-сообщения | 3 | Ошибки/результаты |
| 🟡 P1 | 3: UI-строки | 6 | Интерфейс |
| 🟢 P2 | 4: SO-данные | 10 | Контент (предметы, NPC, квесты) |
| 🟢 P2 | 5: диалоги | 4 | Сюжет |
| 🔵 P3 | 7: верификация | 4 | Quality |

**Итого ~30–40 часов** при последовательной работе. Параллелизация: P1-этапы независимы и могут идти параллельно.

---

## 8. Риски и предостережения

1. **UI Toolkit live-update** — при смене локали на открытом окне часть label'ов может не обновиться (известное поведение пакета). Митигация: все окна слушают `Loc.OnLocaleChanged` → пересборка контента (или пересоздание root — паттерн EnsureBuilt уже есть).
2. **Плюрализация русского** — «1 руда / 2 руды / 5 руд». Smart String `{count:plural:...}` поддерживает locale-aware формы; шаблоны целей (Phase 4.4) делать через `Loc.Format`, НЕ конкатенацией.
3. **Смена языка в мультиплеере** — локализация строго клиентская; серверные сообщения-коды не зависят от языка. Игроки в одном сеансе могут иметь разные языки — это норм.
4. **Отравление ключами при переименовании ID** — если `itemId`/`questId` поменяют, переводы осиротеют (ключи derive-from-ID). Митигация: правило «ID не менять после релиза» уже зафиксировано в Tooltip'ах SO; валидатор 6.3 подсветит orphan-ключи.
5. **CSV-конфликты** — несколько переводчиков правят один CSV. Митигация: Google Sheets как single source (см. Q3) или по-табличные CSV.
6. **`unavailableReason` (DialogOptionDto)** — breaking change протокола; синхронизировать сервер+клиент в одном коммите/тикете.
7. **Legacy uGUI** — часть HUD на TMP; не забыть `Loc.Bind(TMP)`, иначе «половина интерфейса» не переключится.
8. **Производительность** — `LocalizationSettings.StringDatabase.GetLocalizedString` кэширует, но в горячих циклах (тик трекера) лучше кэшировать значения самому.

---

## 9. Открытые вопросы (нужно решение до/вовремя Phase 1)

- **Q1. Набор языков?** Минимум: ru + en. Предлагаю старт: ru (source), en, de. CJK (китайский/японский) потребует шрифтов — вынести из скоупа. RTL — отдельный проект.
- **Q2. Формат обмена с переводчиком:** просто CSV-файл(ы) или Google Sheets (TSV bridge)? Влияет на Phase 6 (добавляем .tsv экспорт/импорт).
- **Q3. Где живут экспортированные CSV:** `Assets/_Project/Localization/Export/` (в git) или вне проекта? Рекомендация: **в git**, чтобы diff переводов был виден в PR.
- **Q4. UXML-привязка:** проверить нативный `text="@key"` в UXML в спринте Phase 3; если работает — использовать для новых окон, `data-loc-key` остаётся для мигрируемых.
- **Q5. Судьба `DialogTree.localizationTable` (TextAsset):** использовать как источник диалоговых ключей (если там уже что-то есть) или удалить после Phase 5.
- **Q6. Порядок миграции:** P1 (system) → P3 (UI) → P4 (data) → P5 (диалоги) — согласовать, или пользователь хочет начать с «видимого» (UI/данные)?

---

## 10. Что НЕ делаем (out of scope — подтверждено)

- ❌ Addressables-локализация (проект не дорос; стриминг-сцены — отдельная тема)
- ❌ Машинный перевод
- ❌ Озвучка/voice lines (поле `voicePrefix` в NpcDefinition — задел, не реализация)
- ❌ RTL-языки (арабский, иврит)
- ❌ CJK-шрифты (до решения Q1)
- ❌ Кастомный CSV-движок (используем нативный пакет)
- ❌ Локализация Debug.Log/серверных логов (только client-facing)

---

## 11. Связь с тикетами и документацией

- Новые тикеты: LOC-01…LOC-12 (см. §6).
- Существующие заделы: T-Q18 (dialog key over wire — формализуется в Phase 5), T-ESC02 (SettingsManager — расширяется в Phase 1), M19-T2 (QuestCsvImporter — паттерн для CSV-инструмента Phase 6).
- Обновлять после каждого этапа: этот документ (чекбоксы), `docs/STEP_BY_STEP_DEVELOPMENT.md`, `docs/QWEN_CONTEXT.md` (по согласованию).
- GDD: `docs/gdd/` не трогаем; если требуется правка — предложение отдельно.
