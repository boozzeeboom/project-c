# 05 — Анализ: авто-регистрация локализационных ключей для ScriptableObject

> **Статус:** АНАЛИЗ / РЕКОМЕНДАЦИЯ
> **Дата:** 2026-08-09
> **Контекст:** Фазы 0–7 закрыты. Пользователь спрашивает: «как сделать так, чтобы при создании нового SO-ассета (Skill, NPC, Item...) сразу резервировались ячейки в таблице перевода?»

---

## 1. Текущее состояние (что уже есть)

### 1.1 Архитектура: derive-from-ID

Ключи **не хранятся** в ScriptableObject. Вместо этого runtime-код вычисляет ключ из ID-поля:

```csharp
// Пример для SkillNodeConfig:
string name = Loc.Get($"static.skill.{skill.skillId}.displayName", skill.displayName);
//                     ───────────── ключ ────────────────  ───── fallback ─────
```

**Поля, участвующие в локализации (по типам SO):**

| Тип SO | ID-поле | Локализуемые поля | Ключ |
|---|---|---|---|
| `SkillNodeConfig` | `skillId` | `displayName`, `description` | `static.skill.{skillId}.displayName` |
| `TradeItemDefinition` | `itemId` | `displayName` | `static.item.{itemId}.displayName` |
| `NpcDefinition` | `npcId` | `displayName`, `greetingText` | `static.npc.{npcId}.displayName` |
| `QuestDefinition` | `questId` | `displayName`, `description` | `static.quest.{questId}.displayName` |
| `QuestStage` | `stageId` | `description` | `static.quest.{questId}.stage.{stageId}.description` |
| `QuestObjective` | `objectiveId` | `description` | `static.quest.{q}.stage.{s}.obj.{o}` |
| `FactionDefinition` | `factionId` (enum) | `displayName`, `loreDescription` | `static.faction.{factionId}.displayName` |
| `ReputationTier` | индекс в массиве | `tier` | `static.faction.{factionId}.tier.{index}` |
| `MarketConfig` | `locationId` | `displayName`, `description` | `static.market.{locationId}.displayName` |
| `ConstellationData.Constellation` | имя элемента | `localizedName` | `static.constellation.{name}.localizedName` |

### 1.2 Текущий инструмент миграции

`LocalizationStringMigrator.cs` — Editor MenuItem:

```
ProjectC → Localization → Migrate SO Strings to Static_Table
```

Это **одноразовый ручной запуск**. Он:
1. Находит все SO через `AssetDatabase.FindAssets`
2. Читает поля через `SerializedObject`
3. Вызывает `table.AddEntry(key, value)` для `Static_Table_ru`
4. Ключи без ID-поля пропускает

**Проблема:** дизайнер создал новый `Skill_Combat_NewSkill.asset` → ключ `static.skill.combat_new_skill.displayName` **не появился** в таблице, пока разработчик вручную не запустит мигратор.

### 1.3 Что умеет Unity Localization 1.5

| Возможность | Есть? | Комментарий |
|---|---|---|
| Авто-сканирование SO | ❌ | Такого функционала нет |
| `StringTable.AddEntry(key, value)` API | ✅ | Доступен в Editor-скриптах |
| `SharedTableData` — единый реестр ключей | ✅ | Ключ, добавленный через `AddEntry`, попадает в SharedData — виден всем локалям |
| `AssetPostprocessor` | ✅ | Стандартный Unity API, можно засечь создание/изменение .asset |
| CSV export/import (нативный) | ✅ | `LocalizationEditorSettings` + `StringTableCollection` |
| `OnValidate()` в SO | ✅ | Можно добавить в каждый класс SO |

---

## 2. Варианты решения

### Вариант A: AssetPostprocessor (рекомендованный)

**Идея:** `AssetPostprocessor.OnPostprocessAllAssets` срабатывает при каждом импорте/создании/сохранении ассета. Фильтруем известные типы SO → вычисляем ключи → вызываем `AddEntry` для `Static_Table_ru` + `SharedData`.

```csharp
// Псевдокод (НЕ реализация):
class LocalizationAssetPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, ...)
    {
        foreach (var path in imported)
        {
            if (!path.EndsWith(".asset")) continue;
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so is SkillNodeConfig skill)  RegisterSkill(skill);
            if (so is NpcDefinition npc)      RegisterNpc(npc);
            // ... etc
        }
    }
}
```

**Плюсы:**
- Полностью автоматически — дизайнер создал ассет → ключ сразу в таблице
- Не требует правок в классах SO
- Один файл, легко поддерживать
- Работает и при создании (Ctrl+N), и при дублировании (Ctrl+D), и при реимпорте

**Минусы:**
- `OnPostprocessAllAssets` вызывается часто (при любом импорте). Нужна оптимизация: проверять что изменилось и не дёргать `AddEntry` без нужды.
- Нужно знать все типы SO и их поля в одном месте (но это и так уже есть в `LocalizationStringMigrator`)

**Вариация A2: только для новых/переименованных ассетов**

Дополнительно проверять `AssetDatabase.GetImplicitAssetBundleName` или сравнивать `importedAssets` vs `deletedAssets` vs `movedAssets` — добавлять только для новых (`importedAssets` минус то что было в предыдущем кадре).

---

### Вариант B: `OnValidate()` в каждом SO

**Идея:** добавить в каждый класс SO метод `OnValidate()`, который сам регистрирует ключ в таблице:

```csharp
// В SkillNodeConfig.cs:
#if UNITY_EDITOR
private void OnValidate()
{
    // ... existing cycle detection ...
    LocalizationAutoRegister.RegisterSkill(skillId, displayName, description);
}
#endif
```

**Плюсы:**
- Срабатывает при ЛЮБОМ изменении полей в инспекторе — мгновенно
- Не зависит от AssetPostprocessor
- Каждый класс сам отвечает за свои ключи

**Минусы:**
- ❌ **Критично:** `OnValidate()` вызывается при загрузке ассета (domain reload, вход в Play Mode). `AssetDatabase`-операции в этот момент **могут** вызывать ошибки/зависания.
- ❌ Нужно править ВСЕ классы SO (SkillNodeConfig, TradeItemDefinition, NpcDefinition, QuestDefinition, FactionDefinition, MarketConfig + все будущие)
- ❌ `AddEntry` в `OnValidate` может вызывать каскадные реимпорты (изменение StringTable триггерит новый `OnPostprocessAllAssets`)

---

### Вариант C: Editor Window «Sync Missing»

**Идея:** не автоматически, а **быстрый доступный инструмент** — окно, которое:
- Показывает diff: какие ключи есть в SO, но отсутствуют в Static_Table
- Одна кнопка «Sync All Missing»
- Можно открыть и закрыть

**Плюсы:**
- Контролируемо (дизайнер жмёт кнопку когда хочет)
- Прозрачно (видно что добавится)
- Никаких сайд-эффектов от авто-срабатываний

**Минусы:**
- ❌ Всё ещё ручной шаг — можно забыть
- Не решает исходную проблему «добавил ассет — забыл синкнуть»

---

### Вариант D: CreateAssetMenu wrapper / Wizard

**Идея:** заменить/дополнить `[CreateAssetMenu]` на кастомный Wizard, который после создания SO сразу добавляет ключи.

**Плюсы:**
- Полный контроль над процессом создания
- Можно добавить валидацию (например, проверка уникальности `skillId`)

**Минусы:**
- ❌ Не покрывает дублирование (Ctrl+D), ручное копирование файлов
- ❌ Ломает привычный workflow (`Right Click → Create → ...`)
- ❌ Много кода на каждый тип SO

---

## 3. Рекомендованное решение: Гибрид A + C

### Основной механизм: AssetPostprocessor (Вариант A)

`LocalizationAssetPostprocessor` — один файл, который:

1. **На `OnPostprocessAllAssets`** — фильтрует импортированные `.asset` файлы, проверяет тип SO, вычисляет ключи, вызывает `AddEntry` **только для отсутствующих** (проверка `table.GetEntry(key) == null`).

2. **Оптимизация:** проверяет что `importedAssets` содержит **новые** пути (через `HashSet` ранее известных), чтобы не дёргать `AddEntry` на каждом чихе.

3. **Логирование:** `Debug.Log` только когда реально добавил ключи: `[LocAuto] Added 3 keys for Skill_Combat_NewSkill`.

### Дополнительный инструмент: Sync Missing (Вариант C)

Тот же `LocalizationStringMigrator` (уже существует!) — оставить как `ProjectC → Localization → Sync All SO Keys to Static_Table` для:
- Первичной миграции после добавления нового типа SO
- Массовой синхронизации после импорта группы ассетов
- Проверки целостности

### Валидация перед билдом

Дополнительно: Editor-скрипт, который при билде (или по кнопке) проверяет что для каждого SO с непустым ID есть запись в `Static_Table`. Выводит warning со списком пропущенных.

---

## 4. Технические детали реализации (без кода)

### 4.1 Как устроен `AddEntry` в Unity Localization

```
StringTable (Static_Table_ru)
  └── SharedTableData (Static_Table Shared Data)  ← единый реестр ключей
        ├── Entry "static.skill.combat_basic_strike.displayName"
        │   └── ru: "Базовый удар"    ← Static_Table_ru
        │   └── en: ""                ← Static_Table_en (пусто!)
        │   └── de: ""                ← Static_Table_de (пусто!)
        └── Entry "static.skill.combat_basic_strike.description"
            └── ru: "Стандартная атака."
```

Когда мы вызываем `Static_Table_ru.AddEntry(key, ruValue)`:
1. Ключ добавляется в **SharedTableData** (становится виден всем локалям)
2. В `Static_Table_ru` записывается ru-значение
3. В `Static_Table_en`, `Static_Table_de` и т.д. — ключ автоматически появляется с **пустым** значением

Именно это и нужно: дизайнер создал SO → ключ в SharedData → переводчик видит пустую ячейку в CSV → заполняет перевод.

### 4.2 Какие SO типы нужно покрыть

На основе анализа кодовой базы:

| Тип | ID-поле | Поля для локализации | Папка с ассетами |
|---|---|---|---|
| `SkillNodeConfig` | `skillId` | `displayName`, `description`, `knowledgeUnlockDescription` | `Resources/Skills/` |
| `TradeItemDefinition` | `itemId` | `displayName` | `Trade/Data/Items/` |
| `NpcDefinition` | `npcId` | `displayName`, `greetingText` | `Quests/Data/Npcs/` |
| `QuestDefinition` | `questId` | `displayName`, `description` + stages/objectives | `Quests/Data/Quests/` |
| `FactionDefinition` | `factionId` | `displayName`, `loreDescription` + tiers | `Quests/Data/Factions/` |
| `MarketConfig` | `locationId` | `displayName`, `description` | `Trade/Data/Markets/` |
| `ConstellationData` | имя элемента | `localizedName` | `ScriptableObjects/DayNight/` |

**Важно:** `SkillNodeConfig` — сейчас находится в `Resources/Skills/` и не был включён в `LocalizationStringMigrator`. Это нужно добавить.

### 4.3 Особый случай: `SkillNodeConfig`

`SkillNodeConfig` имеет:
- `skillId` — стабильный ID (например `"combat_basic_strike"`)
- `displayName` — «Базовый удар»
- `description` — «Стандартная атака ближнего боя»
- `knowledgeUnlockDescription` — «Открывается после изучения основ боя» (подсказка как открыть)

**Не нужно локализовать:** `skillId` (это ключ), `icon` (Sprite), `attackClip` (AnimationClip), числовые параметры.

### 4.4 Особый случай: `ReputationTier`

У `ReputationTier` нет стабильного ID — это элементы массива внутри `FactionDefinition`. Текущий подход: ключ по индексу `static.faction.{factionId}.tier.{index}`. Это хрупко (если поменяют порядок tier'ов — переводы съедут).

**Рекомендация:** добавить в `ReputationTier` поле `tierId` (string) для использования в ключе вместо индекса. Или оставить индекс, но добавить валидатор что порядок tier'ов не меняется.

---

## 5. Workflow после внедрения

### Сценарий: дизайнер создаёт новый навык

1. `Right Click → Create → Project C/Skill Node` → создан `Skill_Combat_NewSkill.asset`
2. Дизайнер заполняет в инспекторе:
   - `skillId` = `"combat_new_skill"`
   - `displayName` = `"Новый удар"`
   - `description` = `"Экспериментальная атака"`
3. **Автоматически** (AssetPostprocessor):
   ```
   [LocAuto] Skill_Combat_NewSkill: added 2 keys to Static_Table
     + static.skill.combat_new_skill.displayName = "Новый удар"
     + static.skill.combat_new_skill.description = "Экспериментальная атака"
   ```
4. Переводчик делает `Export CSV` → видит новые строки с пустыми en/de → заполняет
5. `Import CSV` → готово

### Сценарий: дизайнер переименовал displayName

1. Изменил `displayName` с `"Новый удар"` на `"Смертельный удар"`
2. AssetPostprocessor видит что ключ уже есть → **проверяет значение**:
   - Если ru-значение в таблице **совпадает** со старым литералом → обновляет
   - Если ru-значение в таблице **уже переведено и отличается** → **НЕ перезаписывает** (переводчик уже дал осмысленный перевод, не ломаем)

### Сценарий: дизайнер удалил ассет

- AssetPostprocessor НЕ удаляет ключи из таблицы (это опасно — переводы теряются безвозвратно)
- `Sync Missing` — тоже не удаляет
- Для удаления ключей: ручная операция через `Localization Tables` window или CSV

---

## 6. Что НЕ делаем (out of scope)

- ❌ **Локализация через Addressables** — проект не дорос, Static_Table и так грузится быстро
- ❌ **Авто-перевод** — машинный перевод не подходит для нарративного контента
- ❌ **Удаление ключей при удалении SO** — слишком опасно для переводов
- ❌ **Модификация runtime-классов SO** — derive-from-ID уже работает, поля `*LocKey` не добавляем
- ❌ **Google Sheets API** — пока оставляем CSV round-trip через файлы

---

## 7. План внедрения (оценка ~2–3 часа)

| Шаг | Что | Время |
|---|---|---|
| 1 | Написать `LocalizationAssetPostprocessor.cs` — один файл, регистрирует ключи для всех известных типов SO | 1.5h |
| 2 | Добавить `SkillNodeConfig` и `ConstellationData` в существующий `LocalizationStringMigrator` + в постпроцессор | 0.5h |
| 3 | Добавить валидатор «SO without keys» (кнопка `Validate SO Coverage`) | 0.5h |
| 4 | Проверить: создать новый SO → ключ в таблице → export CSV → import CSV → переключить язык | 0.5h |

---

## 8. Открытые вопросы

- **Q1.** Нужно ли авто-обновлять ru-значение при изменении `displayName` в SO? Или только добавлять новые ключи, а существующие не трогать? (Рекомендация: обновлять только если значение в таблице == старому литералу, иначе пропускать)
- **Q2.** Для `ReputationTier` — добавлять `tierId` поле или оставить ключ по индексу?
- **Q3.** Где именно лежат `ConstellationData` — их 3 копии в разных папках. Какая основная?

---

*Документ создан на основе анализа:*
- `LocalizationStringMigrator.cs` (текущий мигратор)
- `Loc.cs` (runtime-хелпер)
- `SkillNodeConfig.cs`, `TradeItemDefinition.cs`, `NpcDefinition.cs`, `QuestDefinition.cs`, `FactionDefinition.cs`, `MarketConfig.cs`
- `docs/world/Localization/02_FINAL_DESIGN_AND_PLAN.md` (архитектурный план)
- `Packages/manifest.json` → `com.unity.localization 1.5.12`
