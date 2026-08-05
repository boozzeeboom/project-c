# План внедрения системы локализации — Project C

> **Статус:** ЧЕРНОВИК → будет уточняться по мере обсуждения
> **Дата:** 2026-07
> **Контекст:** локализации нет вообще. Все строки хардкожены на русском. Архитекторы预留или поля `"loc key в будущем"` в SO, но реализация отсутствует.

---

## 0. Текущее состояние (аудит)

### 0.1 Что УЖЕ есть под локализацию

| Файл / Класс | Поле | Текущее значение | Комментарий |
|---|---|---|---|
| `NpcDefinition` | `displayName` | литерал "Unknown NPC" | `"loc key в будущем"` |
| `NpcDefinition` | `greetingText` | литерал "Greetings, traveler." | `"loc key в будущем"` |
| `QuestDefinition` | `displayName` | литерал | `"loc key в будущем"` |
| `QuestDefinition` | `description` | литерал (TextArea) | `"loc key в будущем"` |
| `QuestStage` | `description` | литерал | `"loc key в будущем"` |
| `QuestObjective` | `description` | литерал | текст журнала |
| `FactionDefinition` | `displayName` | литерал | `"loc key в будущем, пока — литерал"` |
| `FactionDefinition` | `loreDescription` | литерал (TextArea) | |
| `ReputationTier` | `tier` | литерал (напр. "Недруг") | badge text |
| `DialogTree` | `displayName` | литерал | `"loc key в будущем"` |
| `DialogTree` | `localizationTable` | `TextAsset` (уже есть!) | заготовка под CSV |
| `DialogueNode` | `text` | литерал | `"Loc key в будущем, пока литерал"` |
| `DialogueEdge` | `label` | литерал "Continue" | `"Loc key в будущем"` |
| `ConstellationData.Constellation` | `localizedName` | литерал | уже названо `localizedName` |
| `ItemData` | `itemName` | литерал ("Железная руда") | |
| `ItemData` | `description` | литерал (TextArea) | |
| `ItemTypeNames` | `_names[]` | массив "Ресурсы", "Оборудование"... | static readonly |

### 0.2 Что хардкожено в коде (не SO)

| Место | Что | Пример |
|---|---|---|
| `InventoryClientState.LocalizeResultCode` | switch/case 10 строк | `"Инвентарь полон"` |
| `MarketClientState.LocalizeResultCode` | switch/case ~15 строк | `"Слишком далеко от торговца"` |
| `ContractClientState.LocalizeResultCode` | switch/case ~12 строк | `"Контракт не найден"` |
| `ContractServer.ContractClientState_LocalizeResultCode` | дубликат серверной локализации | (должен уйти) |
| `InventoryWorld.cs` | error-сообщения | `$"Предмет ID={itemId} не найден"` |
| `GameplaySettingsSection.cs` | UI-лейблы | `"Управление"`, `"Чувств. мыши"` |
| `CharacterWindow.cs` | фильтры + статусы | `"Все"`, `"Контракты"`, `"Игрок (Owner)"` |
| `MarketWindow.cs` | UI-строки | `"Ошибка:"`, `$"OK ({result.itemId} x{result.quantity})"` |
| `EscMenu` | все разделы настроек | Аудио, Графика, Геймплей, Управление |
| `HUDManager`, `ControlHintsUI` | подсказки управления | "Нажмите E для взаимодействия" |

### 0.3 Инфраструктура

- ✅ Unity 6 (6000+), URP
- ✅ UI Toolkit (основной UI-фреймворк)
- ✅ TextMesh Pro (есть в проекте)
- ✅ CSV-импорт/экспорт уже работает для квестов (`QuestCsvImporter`/`QuestCsvExporter`)
- ✅ `SettingsManager` — готовое место для persistence выбора языка
- ✅ В `GameplaySettingsSection` уже есть placeholder `"Выбор языка будет доступен после внедрения локализации"`
- ❌ Unity Localization Package **НЕ установлен**
- ❌ Никакой runtime locale switching нет

---

## 1. Архитектурное решение

### 1.1 Выбор пакета: Unity Localization

**Рекомендация: установить `com.unity.localization`**

Обоснование:
- Нативная интеграция с UI Toolkit (через `LocalizedString` + binding)
- Нативная интеграция с TextMesh Pro
- Готовый CSV round-trip (Export → Google Sheets → Import)
- `StringTable` per locale, `LocalizationSettings.SelectedLocale` для runtime switching
- Smart String support (плюрализация, форматирование)
- Не тянет тяжелых зависимостей
- Поддерживает Addressables (будет важно для стриминга)

**Почему НЕ кастомное решение:**
- Свой CSV-парсер + таблицы + binding = ~2 недели разработки и багфиксов
- Unity Loc уже решает: плюрализацию, interpolation, locale fallback, async loading, editor tooling
- Проект уже на Unity 6 — полная совместимость

**Почему НЕ I2 Localization:**
- Платный ($45), закрытый код
- UI Toolkit поддержка слабее
- Проект уже на Unity 6 — нативный пакет предпочтительнее

### 1.2 Организация StringTable

Предлагаю **4 таблицы** по доменам (а не одну гигантскую):

| Таблица | Назначение | Примеры ключей |
|---|---|---|
| `Static` | SO-данные: предметы, NPC, квесты, фракции, созвездия, навыки | `item.iron_ore.name`, `npc.mira.displayName`, `quest.find_artifact.description`, `faction.guild_of_thoughts.displayName` |
| `UI` | Интерфейс: меню, HUD, настройки, кнопки, фильтры | `ui.esc_menu.gameplay.title`, `ui.inventory.filter.all`, `ui.hud.interact_hint` |
| `Dialogue` | Все диалоговые реплики и выборы | `dialogue.mira.greeting`, `dialogue.mira.artifact_offer` |
| `System` | Сообщения об ошибках, коды результатов | `sys.inventory.full`, `sys.contract.not_found`, `sys.market.too_far` |

**Почему 4 таблицы:**
- Разные частоты обновления (Dialogue меняется часто, Static — при добавлении контента)
- Разные ответственные (нарративщик работает с Dialogue, дизайнер — с Static)
- Меньше merge-конфликтов в CSV
- Быстрее загрузка (не грузим Dialogue если игрок в меню)

### 1.3 Схема ключей

```
{domain}.{type}.{id}.{field}
```

Примеры:
```
static.item.iron_ore.name
static.item.iron_ore.description
static.npc.mira.displayName
static.npc.mira.greetingText
static.quest.find_artifact.displayName
static.quest.find_artifact.description
static.quest.find_artifact.stage.find_location.description
static.faction.guild_of_thoughts.displayName
static.faction.guild_of_thoughts.tier.hated
static.faction.guild_of_thoughts.tier.neutral
static.faction.guild_of_thoughts.loreDescription
ui.esc_menu.gameplay.title
ui.esc_menu.gameplay.mouse_sensitivity
ui.inventory.filter.all_types
ui.character.reputation.no_data
sys.inventory.full
sys.contract.not_found
dialogue.mira_default.greeting.text
dialogue.mira_default.help_offer.label
```

---

## 2. Пошаговый план реализации

### Этап 1: Установка и настройка пакета (≈1 час)

- [x] (пока ничего)

- [ ] **1.1** Установить `com.unity.localization` через Package Manager
- [ ] **1.2** Создать `LocalizationSettings` asset в `Assets/_Project/Settings/Localization/`
- [ ] **1.3** Настроить locales: `ru` (Russian) — default, `en` (English)
- [ ] **1.4** Создать 4 StringTable assets:
  - `Assets/_Project/Settings/Localization/Static_Table.asset`
  - `Assets/_Project/Settings/Localization/UI_Table.asset`
  - `Assets/_Project/Settings/Localization/Dialogue_Table.asset`
  - `Assets/_Project/Settings/Localization/System_Table.asset`
- [ ] **1.5** Создать `LocaleSelector` MonoBehaviour (выбор языка, сохранение в PlayerPrefs/SettingsManager)

### Этап 2: Инструмент переводчика (≈4 часа)

Это **самая важная часть** для удобства работы.

- [ ] **2.1** Создать Editor Window: `LocalizationExportWindow`
  - Путь меню: `ProjectC → Localization → Export/Import CSV`
  - Выбор таблицы(таблиц) для экспорта
  - Выбор языков (чекбоксы: ru, en, de, fr...)
  
- [ ] **2.2** Реализовать **Export All → CSV**:
  - Одна кнопка «Выгрузить всё»
  - Формат CSV:
    ```csv
    Key,ru,en
    static.item.iron_ore.name,Железная руда,Iron Ore
    static.item.iron_ore.description,"Обычная железная руда. Добывается в шахтах.","Common iron ore. Mined in caves."
    ui.esc_menu.gameplay.title,Управление,Controls
    ```
  - CSV сохраняется в `Assets/_Project/Localization/Export/` (gitignored или нет — на выбор)
  - Автоматически открывает папку в файловом менеджере

- [ ] **2.3** Реализовать **Import CSV → StringTable**:
  - Выбор CSV-файла
  - Валидация: проверка существования ключей, предупреждения о новых/удалённых
  - Заполнение StringTable для каждого языка из соответствующих колонок
  - Отчёт: сколько ключей обновлено, добавлено, удалено

- [ ] **2.4** Добавить **Google Sheets bridge** (optional, Phase 2):
  - Экспорт в `.tsv` для прямой вставки в Google Sheets
  - Импорт из `.tsv` (Google Sheets → File → Download → TSV)

### Этап 3: Базовая инфраструктура runtime (≈3 часа)

- [ ] **3.1** Реализовать `LocaleSelector`:
  ```csharp
  public class LocaleSelector : MonoBehaviour
  {
      // Публичный метод для UI
      public void SetLocale(string localeCode); // "ru", "en"
      // Сохраняет выбор в SettingsManager
      // Загружает при старте
  }
  ```

- [ ] **3.2** Интегрировать `LocaleSelector` в `SettingsManager`:
  - Новый ключ: `SettingsManager.Locale` (string, PlayerPrefs)
  - При старте: загрузить сохранённую локаль → применить

- [ ] **3.3** Добавить выбор языка в `GameplaySettingsSection`:
  - Заменить placeholder `"Выбор языка будет доступен..."` на `DropdownField`
  - Список: Русский, English
  - При изменении → `LocaleSelector.SetLocale(...)`

- [ ] **3.4** Подключить `LocalizedString` binding для UI Toolkit:
  - Убедиться что `LocalizationSettings` работает с UI Toolkit `LocalizedString`
  - Протестировать на одном Label

### Этап 4: Миграция System-сообщений (≈3 часа)

Это самое простое и быстро даёт эффект.

- [ ] **4.1** Заменить `InventoryClientState.LocalizeResultCode`:
  - Удалить switch/case с русскими строками
  - Создать ключи в System_Table: `sys.inventory.ok`, `sys.inventory.full`, `sys.inventory.not_found`, ...
  - `LocalizeResultCode` → lookup в StringTable по ключу `$"sys.inventory.{code.ToSnakeCase()}"`

- [ ] **4.2** Заменить `MarketClientState.LocalizeResultCode` — аналогично

- [ ] **4.3** Заменить `ContractClientState.LocalizeResultCode` — аналогично

- [ ] **4.4** Убрать дубликат `ContractServer.ContractClientState_LocalizeResultCode`:
  - Сервер должен слать ТОЛЬКО коды (что он уже делает)
  - Серверный fallback-метод удалить
  - Серверное поле `message` в DTO должно заполняться пустой строкой, клиент сам локализует

- [ ] **4.5** Заменить хардкоженные error-строки в `InventoryWorld.cs`:
  - Все `$"..."` строки → локализованные через `InventoryClientState.LocalizeResultCode`

### Этап 5: Миграция UI-строк (≈5 часов)

- [ ] **5.1** `GameplaySettingsSection.cs`:
  - `"Управление"` → `ui.esc_menu.gameplay.title`
  - `"Чувств. мыши"` → `ui.esc_menu.gameplay.mouse_sensitivity`
  - `"Инвертировать Y"` → `ui.esc_menu.gameplay.invert_y`
  - `"Чувств. зума"` → `ui.esc_menu.gameplay.zoom_sensitivity`
  - `"Доступность"` → `ui.esc_menu.accessibility.title`
  - `"Субтитры"` → `ui.esc_menu.accessibility.subtitles`

- [ ] **5.2** `AudioSettingsSection.cs` — аналогично

- [ ] **5.3** `GraphicsSettingsSection.cs` — аналогично

- [ ] **5.4** `CharacterWindow.cs`:
  - `"Все"`, `"Контракты"`, `"Квесты"`, `"Активные"`, `"Доступные"` → ключи UI
  - `"Все типы"` → ключ UI
  - `"Игрок (Owner)"`, `"Игрок"`, `"—"` → ключи UI
  - `"Фракций:"`, `"Нет данных о репутации"` → ключи UI

- [ ] **5.5** `MarketWindow.cs` — все UI-строки

- [ ] **5.6** `HUDManager.cs`, `ControlHintsUI.cs` — все подсказки

- [ ] **5.7** `EscMenuWindow.cs` — навигация, названия разделов

### Этап 6: Миграция ScriptableObject (≈8 часов)

Это **самая объёмная часть**. Стратегия: **аддитивная миграция** — НЕ ломаем существующие поля, добавляем loc key рядом, runtime читает loc key, fallback на строку.

- [ ] **6.1** `ItemData`:
  ```csharp
  // Добавить:
  public string itemNameLocKey;   // "static.item.iron_ore.name"
  public string descriptionLocKey; // "static.item.iron_ore.description"
  // Runtime helper:
  public string GetDisplayName() => GetLocalized(itemNameLocKey, itemName);
  ```

- [ ] **6.2** `ItemTypeNames`:
  - Заменить массив `_names[]` на lookup из StringTable
  - Ключи: `static.item_type.resources`, `static.item_type.equipment`, ...

- [ ] **6.3** `NpcDefinition`:
  ```csharp
  public string displayNameLocKey;  // "static.npc.mira.displayName"
  public string greetingTextLocKey; // "static.npc.mira.greetingText"
  ```

- [ ] **6.4** `QuestDefinition`:
  ```csharp
  public string displayNameLocKey;   // "static.quest.find_artifact.displayName"
  public string descriptionLocKey;   // "static.quest.find_artifact.description"
  ```

- [ ] **6.5** `QuestStage`:
  ```csharp
  public string descriptionLocKey; // "static.quest.find_artifact.stage.find_location.description"
  ```

- [ ] **6.6** `QuestObjective`:
  ```csharp
  public string descriptionLocKey;
  ```

- [ ] **6.7** `FactionDefinition`:
  ```csharp
  public string displayNameLocKey;
  public string loreDescriptionLocKey;
  ```

- [ ] **6.8** `ReputationTier`:
  ```csharp
  public string tierLocKey; // "static.faction.guild_of_thoughts.tier.neutral"
  ```

- [ ] **6.9** `ConstellationData.Constellation`:
  - Поле `localizedName` уже есть — можно оставить как fallback, добавить `localizedNameLocKey`

- [ ] **6.10** Написать общий runtime helper:
  ```csharp
  public static class Loc
  {
      public static string Get(string locKey, string fallback = null);
      public static string Format(string locKey, params object[] args);
  }
  ```
  Использует `LocalizationSettings.StringDatabase.GetLocalizedString`.

- [ ] **6.11** Пройти по ВСЕМ местам где читаются SO-поля и заменить прямые обращения на `Loc.Get(...)`:
  - UI код (CharacterWindow, MarketWindow, QuestTracker, DialogWindow)
  - `QuestWorld`, `QuestServer` (только для error messages)
  - HUD, Billboard

### Этап 7: Диалоги (≈4 часа)

- [ ] **7.1** Создать ключи для всех `DialogueNode.text`:
  ```
  dialogue.{treeId}.{nodeId}.text
  dialogue.{treeId}.{nodeId}.edge.{edgeIndex}.label
  ```

- [ ] **7.2** Написать миграционный скрипт (Editor):
  - Проходит по всем DialogTree в `Assets/_Project/Quests/Data/Dialogs/`
  - Для каждого DialogueNode: сохраняет текст в StringTable (ru), генерирует loc key, обновляет node.text → locKey
  - Создаёт CSV для переводчика (колонка en — пустая)

- [ ] **7.3** Обновить `DialogueNode`:
  ```csharp
  public bool useLocKey; // флаг: использовать text как locKey или как литерал
  ```

- [ ] **7.4** Обновить `DialogWindow` (UI рендеринг диалогов) — читать через `Loc.Get(node.text, node.text)`

### Этап 8: CSV-экспорт/импорт для переводчика (≈4 часа)

- [ ] **8.1** `LocalizationExportWindow` (Editor Window):
  - Вкладка «Export»:
    - Выбор доменов (Static, UI, Dialogue, System) — мультиселект
    - Выбор языков (ru, en + custom)
    - Кнопка «Export CSV»
    - Результат: CSV файл(ы) в `Assets/_Project/Localization/Export/`
  - Вкладка «Import»:
    - Выбор CSV-файла
    - Preview: показывает изменения (зелёный — новый, жёлтый — изменён, красный — удалён)
    - Кнопка «Apply Import»
    - Отчёт

- [ ] **8.2** Автоматический экспорт при билде (валидация):
  - Проверить, что для всех ключей есть переводы на всех supported языках
  - Warning если ключ без перевода на en

### Этап 9: Тестирование и полировка (≈3 часа)

- [ ] **9.1** Протестировать переключение языков в рантайме на всех экранах
- [ ] **9.2** Протестировать диалоги (самая сложная часть — динамический UI Toolkit)
- [ ] **9.3** Протестировать CSV round-trip: export → изменить в Google Sheets → import → работает
- [ ] **9.4** Проверить что сервер не шлёт локализованные строки (только коды)
- [ ] **9.5** Протестировать fallback: если loc key не найден → показывается литерал

---

## 3. Формат CSV для переводчика

```csv
Key,ru,en,de
static.item.iron_ore.name,Железная руда,Iron Ore,Eisenerz
static.item.iron_ore.description,"Обычная железная руда. Добывается в шахтах.","Common iron ore. Mined in caves.","
static.npc.mira.displayName,Мира,Mira,
static.quest.find_artifact.displayName,Найти артефакт,Find the Artifact,
ui.esc_menu.gameplay.title,Управление,Controls,
sys.inventory.full,Инвентарь полон,Inventory Full,
dialogue.mira_default.greeting.text,"Привет, путник!","Hello, traveler!",
```

**Правила:**
- Первая колонка — всегда `Key`
- Остальные колонки — locale code (`ru`, `en`, `de`, `fr`...)
- Порядок колонок языков фиксирован в настройках экспорта
- Пустая ячейка = "перевода нет, использовать fallback (ru)"
- Запятые внутри строки → ячейка в кавычках
- Переносы строк внутри ячейки → в кавычках

**Workflow переводчика:**
1. Разработчик жмёт `ProjectC → Localization → Export All CSV`
2. Открывает CSV в Google Sheets (File → Import)
3. Делится ссылкой с переводчиком
4. Переводчик заполняет колонки en, de, fr...
5. Разработчик скачивает CSV из Sheets → `ProjectC → Localization → Import CSV`
6. Готово. Все языки обновлены.

---

## 4. Схема миграции SO-данных

### Принцип: аддитивность

**НЕ трогаем существующие string-поля.** Добавляем locKey поля рядом:

```csharp
// БЫЛО:
public string displayName = "Unknown NPC";

// СТАЛО:
public string displayName = "Unknown NPC";          // fallback
public string displayNameLocKey = "";                // ключ в StringTable
```

**Runtime-чтение через helper:**
```csharp
string name = Loc.Get(npc.displayNameLocKey, npc.displayName);
//                ───────── ключ ─────────  ── fallback ──
```

Это даёт:
- Обратную совместимость (все старые SO работают без изменений)
- Постепенную миграцию (можно переводить по одному SO)
- Безопасность (если ключ не найден — покажется fallback)

---

## 5. Локализация UI Toolkit

Unity Localization поддерживает UI Toolkit через `LocalizedString` + binding. Два подхода:

### Способ A: Прямой binding (UXML)
```xml
<ui:Label text="@static_npc_mira_displayName" />
```
Минус: требует генерации алиасов, сложно дебажить.

### Способ B: Programmatic binding (C#) — РЕКОМЕНДУЕТСЯ
```csharp
var label = new Label();
var locString = new LocalizedString("Static", "static.npc.mira.displayName");
locString.StringChanged += s => label.text = s;
```

Для удобства — extension:
```csharp
public static void BindLoc(this Label label, string table, string key)
{
    var loc = new LocalizedString(table, key);
    loc.StringChanged += s => label.text = s;
}
// Использование:
npcNameLabel.BindLoc("Static", "static.npc.mira.displayName");
```

---

## 6. Порядок внедрения (приоритет)

| Приоритет | Этап | Часы | Эффект |
|---|---|---|---|
| 🔴 P0 | 1. Установка пакета + LocaleSelector | 1h | Можно переключать язык |
| 🔴 P0 | 2. Инструмент переводчика (Editor) | 4h | Удобный CSV workflow |
| 🔴 P0 | 3. Runtime инфраструктура | 3h | UI выбор языка |
| 🟡 P1 | 4. System-сообщения | 3h | Ошибки/результаты на выбранном языке |
| 🟡 P1 | 8. CSV export/import window | 4h | Инструмент готов |
| 🟢 P2 | 6. SO-данные (предметы, NPC, квесты) | 8h | Основной контент |
| 🟢 P2 | 7. Диалоги | 4h | Сюжет |
| 🟢 P2 | 5. UI-строки (меню, HUD) | 5h | Интерфейс |
| 🔵 P3 | 9. Тестирование и полировка | 3h | Quality |

**Total: ~35 часов** (при последовательной работе)

---

## 7. Что НЕ делаем (out of scope)

- ❌ Локализация через Addressables (пока не нужно — проект не достиг размеров где это важно)
- ❌ Машинный перевод (только ручной через переводчика)
- ❌ Plural systems сложнее чем Smart String `{count:plural:...}`
- ❌ Аудио-озвучка на разных языках (voice lines — потом)
- ❌ Right-to-left языки (арабский, иврит)
- ❌ Локализация шрифтов (CJK fallback)

---

## 8. Риски и предостережения

1. **UI Toolkit + Localization binding** — баг: `LocalizedString` в UI Toolkit иногда не обновляется при смене локали если элемент уже в дереве. **Решение:** пересоздавать UI при смене языка ИЛИ использовать event-driven подход с `LocalizationSettings.OnSelectedLocaleChanged`.

2. **Диалоги** — самая сложная часть. DialogueNode создаются динамически, нельзя использовать статический binding. **Решение:** `DialogueWindow.BuildNode()` всегда читает через `Loc.Get(...)`.

3. **Серверные сообщения** — сейчас сервер местами шлёт готовую русскую строку в `message` поле (ContractServer). **Решение:** убрать это. Сервер шлёт только code. Клиент локализует. Это breaking change для протокола — нужно синхронизировать с серверной командой.

4. **CSV merge conflicts** — если несколько человек правят переводы. **Решение:** экспорт/импорт по одной таблице за раз + Google Sheets как single source of truth.

---

## 9. Вопросы к обсуждению

1. Какие языки планируются кроме RU и EN? (DE? FR? ZH?)
2. Кто будет переводчиком? Нужен ли интерфейс для нетехнического человека?
3. Voice lines — нужна ли привязка субтитров к аудио?
4. Готовы ли к breaking change в серверном протоколе (убрать message из DTO)?
5. Хранить CSV экспорта в git или gitignore?

---

*Документ будет дополняться по мере реализации. Файлы итераций: `01_Phase1_Setup.md`, `02_Phase2_TranslatorTool.md`, ...*
