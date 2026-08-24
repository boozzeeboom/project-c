# Faction ID: вывод из хардкода и переход на data-driven фракции

> **Статус:** план, код и ассеты не изменялись.
>
> **Дата повторного аудита:** 24 августа 2026 г.
>
> **Основание:** `docs/dev/FACTION_ID_DEHARDCODE_PLAN.md`
>
> **Цель:** сохранить все существующие фракции и их назначения, но дать возможность создавать новые `FactionDefinition` без добавления значения в `FactionId.cs`.

---

## 1. Требуемый результат

После выполнения плана пользователь должен иметь возможность:

1. Создать новый ассет через `ProjectC/Factions/Faction Definition`.
2. Задать ему новый уникальный ID без редактирования C# enum.
3. Задать отображаемое имя, цвет, описание, отношения и репутационные параметры.
4. Назначить новую фракцию NPC, спавнеру и AI.
5. Использовать её в боевых отношениях.
6. Использовать её в квестах и диалогах.
7. Изменять и отображать репутацию с новой фракцией.
8. Передавать новую фракцию по сети.
9. Сохранять и загружать её репутацию и знание о ней.
10. Использовать её через CSV-импорт.
11. Не изменить смысл существующих фракций и старых сохранений.

Критерий готовности — новая фракция работает так же, как существующие фракции, а не только отображается в одном инспекторе.

---

## 2. Подтверждённое состояние проекта

### 2.1. Текущий источник ограничения

Файл:

```text
Assets/_Project/Quests/Factions/FactionId.cs
```

Содержит enum из 16 значений:

```text
None = 0
GuildOfThoughts = 1
GuildOfCreation = 2
GuildOfStrength = 3
GuildOfSecrets = 4
GuildOfSuccess = 5
Underground = 6
Resistance = 7
FreeTraders = 8
SOL_Patrol = 9
Pirates = 10
Neutral = 11
Bandits = 12
Cultists = 13
Guards = 14
Villagers = 15
```

Файл:

```text
Assets/_Project/Quests/Factions/FactionDefinition.cs
```

содержит:

```csharp
public FactionId factionId = FactionId.None;
```

Поле является обычным Unity enum-полем. Выпадающий список строится автоматически на основании `FactionId.cs`.

### 2.2. Существующие фракционные ассеты

Фактический каталог:

```text
Assets/_Project/Resources/Data/Factions/
```

Сейчас содержит 10 ассетов:

| Ассет | Старое значение `FactionId` | Будущий `wireId` |
|---|---:|---:|
| `GuildOfThoughts.asset` | 1 | 1 |
| `GuildOfCreation.asset` | 2 | 2 |
| `GuildOfStrength.asset` | 3 | 3 |
| `GuildOfSecrets.asset` | 4 | 4 |
| `GuildOfSuccess.asset` | 5 | 5 |
| `FreeTraders.asset` | 8 | 8 |
| `Pirates.asset` | 10 | 10 |
| `Neutral.asset` | 11 | 11 |
| `Faction_Bandits.asset` | 12 | 12 |
| `Faction_Villagers.asset` | 15 | 15 |

Значения `6`, `7`, `9`, `13`, `14` сейчас являются legacy/reserved значениями enum и отдельными ассетами не представлены.

### 2.3. Уже data-driven части

Следующие компоненты уже используют ссылки на `FactionDefinition`, а не `FactionId`:

```text
Assets/_Project/Scripts/AI/NpcSocialBrain.cs
Assets/_Project/Scripts/AI/NpcSpawnerConfig.cs
Assets/_Project/Scripts/AI/Editor/NpcSocialBrainEditor.cs
Assets/_Project/Scripts/AI/Editor/NpcSpawnerConfigEditor.cs
```

Их нельзя переписывать полностью. После появления корректного нового ассета они должны продолжить работать через object reference.

### 2.4. Реальные блокирующие места

Новые фракции сейчас блокируются в следующих слоях:

- `FactionDefinition.factionId` — enum вместо data-driven ID;
- `FactionCombatRelation.targetFaction` — enum;
- поля фракции в `NpcDefinition`, `QuestDefinition`, `QuestObjective`, `QuestReward`, `QuestPrerequisite`, `DialogueAction`, `DialogueCondition` — enum;
- `QuestWorld` — словари и методы с ключом `FactionId`;
- `QuestServer.BuildReputationSnapshot` — перебирает `Enum.GetValues(typeof(FactionId))`;
- `ReputationClientState` — жёстко добавляет `Neutral` как число 11;
- `CharacterWindow` и `KnowledgeToast` — преобразуют сетевой byte обратно в enum;
- CSV-импортеры — используют `Enum.TryParse<FactionId>`;
- JSON-сохранения — используют числовые faction ID, но пока трактуют их как enum values.

---

## 3. Что было неверно в предыдущем аудите

### 3.1. Нельзя переписывать весь runtime на строки

Переход всех faction-полей на `string` создаёт лишнее распространение строк по runtime и редактору.

Для authoring-данных предпочтительна ссылка:

```text
FactionDefinition
```

Строковый `factionKey` используется для CSV, локализации, логов и внешних данных. Числовой `wireId` используется для сети и сохранений.

### 3.2. Network byte не полностью независим от enum

Хотя DTO содержат `byte`, текущая логика всё ещё связана с enum:

```text
Assets/_Project/Quests/Network/QuestServer.cs:883-891
```

Сервер перебирает значения `FactionId`, а клиент преобразует byte обратно в `FactionId`. Поэтому новая фракция не попадёт в snapshot только от добавления нового ассета.

### 3.3. `FactionId.cs` не нужно удалять на первом этапе

Enum нужен как legacy-маппинг для старых ассетов и старых сохранений.

Функциональная цель задачи — разрешить новые фракции, а не обязательно удалить весь старый тип. Удаление enum является отдельной будущей задачей и не входит в основной план.

### 3.4. В проекте есть рассинхронизация каталогов

Фракции находятся в:

```text
Assets/_Project/Resources/Data/Factions/
```

Но следующие системы ищут их в другом каталоге:

```text
Assets/_Project/Quests/Data/Factions
```

Проблемные места:

```text
Assets/_Project/Quests/Editor/QuestDatabaseAutoDiscover.cs:26
Assets/_Project/Editor/Tools/NpcWorldInspectorWindow.cs:1463-1464
```

При этом:

```text
Assets/_Project/Quests/Data/QuestDatabase.asset
```

содержит:

```yaml
factions: []
```

Эту проблему нужно устранить как часть работы с каталогом, иначе разные инструменты будут видеть разные наборы фракций.

---

## 4. Целевая модель

### 4.1. `FactionDefinition` остаётся главным ассетом

Каждая фракция продолжает существовать как:

```text
FactionDefinition.asset
```

В ассет добавляются два новых поля:

```text
factionKey — стабильный текстовый ключ фракции
wireId     — стабильный числовой ID для сети и сохранений
```

Старое поле:

```text
factionId : FactionId
```

остаётся временно как legacy-поле.

### 4.2. Правила идентификаторов

```text
wireId = 0       — None
wireId = 1..15   — существующие значения, сохраняются навсегда
wireId >= 16     — новые фракции
```

Запрещено:

- менять `wireId` после первого использования в сейвах или сетевой игре;
- переиспользовать старый ID;
- использовать `displayName` как ID;
- использовать имя файла как единственный ID;
- назначать одинаковый `factionKey` двум ассетам;
- назначать одинаковый `wireId` двум ассетам.

`Neutral` сохраняет `wireId = 11`.

### 4.3. Авторские поля используют ссылки на ассеты

В полях, которые пользователь заполняет в инспекторе, основной ссылкой должна быть:

```text
FactionDefinition
```

Это относится к:

```text
NpcDefinition
AttitudeLink
QuestDefinition
QuestObjective
QuestRewardReputation
QuestPrerequisite
DialogueCondition
DialogueAction
FactionCombatRelation
KnowledgeLossConfig
```

Старые enum-поля временно остаются для миграции и fallback.

### 4.4. Реестр фракций

Используется один источник истины — расширенный `FactionCatalog` либо переименованный в будущем `FactionRegistry`.

Он строит lookup:

```text
factionKey → FactionDefinition
wireId     → FactionDefinition
legacy enum value → FactionDefinition
```

Каталог загружает ассеты из фактического пути:

```text
Resources/Data/Factions
```

Дубликаты и некорректные ID должны выдавать ошибку валидации и не считаться рабочим состоянием.

---

## 5. Этапы исполнения

Каждый этап выполняется отдельным коммитом. Между этапами старые ассеты и старые значения должны оставаться рабочими.

---

### Этап 0. Зафиксировать контракт и baseline

**Изменения:** только документация/инвентаризация.

**Сделать:**

1. Зафиксировать текущую таблицу из §2.2.
2. Зафиксировать список всех потребителей `FactionId`.
3. Зафиксировать правила `factionKey` и `wireId`.
4. Зафиксировать, что `FactionId.cs` пока не удаляется.
5. Зафиксировать формат новых фракций для CSV.
6. Зафиксировать, что существующие ассеты не получают новые значения и не переименовываются.

**Выходной критерий:** есть однозначная таблица legacy-маппинга и список файлов для каждого слоя.

---

### Этап 1. Добавить identity layer без изменения поведения

**Целевые файлы:**

```text
Assets/_Project/Quests/Factions/FactionDefinition.cs
Assets/_Project/Scripts/Knowledge/FactionCatalog.cs
```

**Сделать:**

1. Добавить `factionKey`.
2. Добавить `wireId`.
3. Расширить каталог lookup-ами по ключу и wire ID.
4. Добавить проверки:
   - пустой key;
   - дубликат key;
   - дубликат wire ID;
   - `wireId = 0` для обычной фракции;
   - нарушение диапазона legacy/new ID.
5. Добавить editor-инспектор `FactionDefinition`.
6. Оставить старое enum-поле видимым как legacy/read-only.

**Поведение:** существующая система всё ещё может использовать enum.

**Выходной критерий:** каталог корректно видит все 10 существующих ассетов, но gameplay-поведение не меняется.

---

### Этап 2. Мигрировать существующие фракционные ассеты

**Сделать editor-инструмент:**

```text
ProjectC/Factions/Tools/Migrate Faction Assets
```

Инструмент должен быть идемпотентным.

Для каждого старого ассета:

```text
factionKey = имя старого enum
wireId     = числовое значение старого enum
```

Пример:

```text
Faction_Bandits.asset
legacy factionId = Bandits / 12
factionKey       = Bandits
wireId           = 12
```

**Ограничения:**

- не редактировать YAML вручную;
- не менять `.meta`;
- не менять старое поле `factionId`;
- не менять старые numeric values;
- использовать `SerializedObject`, `EditorUtility.SetDirty`, `AssetDatabase.SaveAssets`.

**Выходной критерий:** все 10 ассетов имеют новые поля, а их старые назначения совпадают с baseline.

---

### Этап 3. Вертикальный срез: новая фракция в AI и combat

**Целевые области:**

```text
FactionDefinition
FactionCombatRelation
NpcSocialBrain
NpcSpawnerConfig
NpcBrain
NpcGroupController
VengeanceMemory
```

**Сделать:**

1. Перевести `FactionCombatRelation.targetFaction` на ссылку на `FactionDefinition` с legacy fallback.
2. Перевести методы сравнения отношений на asset/key/wire lookup.
3. Сохранить object-reference подход в `NpcSocialBrain` и `NpcSpawnerConfig`.
4. Убрать зависимость создания новой фракции от `Enum.GetValues` в `NpcWorldInspectorWindow`.
5. Сделать создание нового ассета через обычный `CreateAssetMenu` либо отдельный инструмент.

**Проверка:**

1. Создать временную тестовую фракцию с `wireId = 16`.
2. Назначить её `NpcSocialBrain` и `NpcSpawnerConfig`.
3. Настроить отношения с существующей фракцией.
4. Проверить hostile/allied/neutral поведение.
5. Удалить тестовый ассет после проверки либо оставить его только по отдельному решению.

**Выходной критерий:** новая фракция работает в AI/combat, старые NPC не изменили поведение.

---

### Этап 4. Перевести authoring-поля на `FactionDefinition` references

**Целевые файлы и модели:**

```text
Assets/_Project/Quests/Npcs/NpcDefinition.cs
Assets/_Project/Quests/Quests/QuestDefinition.cs
Assets/_Project/Quests/Quests/QuestObjective.cs
Assets/_Project/Quests/Quests/QuestReward.cs
Assets/_Project/Quests/Quests/QuestPrerequisite.cs
Assets/_Project/Quests/Dialogue/DialogueAction.cs
Assets/_Project/Quests/Dialogue/DialogueCondition.cs
Assets/_Project/Quests/Factions/FactionRelation.cs
Assets/_Project/Scripts/Knowledge/KnowledgeLossConfig.cs
```

Для каждого поля:

1. Добавить `FactionDefinition` reference.
2. Оставить старый enum field как legacy.
3. Написать one-shot миграцию старого значения в reference.
4. В runtime использовать reference в первую очередь.
5. При отсутствии reference временно использовать старый enum.
6. Не менять существующие ассеты вручную.

**Выходной критерий:** существующие NPC, квесты и диалоги сохраняют свои фракции, а новые данные могут ссылаться на новую фракцию.

---

### Этап 5. Перевести runtime на registry boundary

**Целевые файлы:**

```text
Assets/_Project/Quests/Core/QuestWorld.cs
Assets/_Project/Quests/Network/QuestServer.cs
Assets/_Project/Reputation/ReputationClientState.cs
Assets/_Project/Scripts/UI/Client/CharacterWindow.cs
Assets/_Project/Scripts/Knowledge/KnowledgeToast.cs
Assets/_Project/Scripts/Knowledge/KnowledgeManager.cs
Assets/_Project/Core/WorldEvent.cs
```

**Сделать:**

1. `QuestServer.BuildReputationSnapshot` должен перебирать зарегистрированные ассеты, а не enum values.
2. Сетевой `byte` оставить без изменения структуры DTO.
3. Для отправки использовать `FactionDefinition.wireId`.
4. На клиенте искать фракцию по `wireId` через каталог.
5. Убрать прямые преобразования `(FactionId)byte` из presentation-логики.
6. Убрать жёстко зашитый `Neutral = 11`; получать Neutral через registry по legacy-маппингу или ключу.
7. Внутренние методы `QuestWorld` перевести на единый runtime-идентификатор через registry boundary.

**Выходной критерий:** новая фракция появляется в reputation snapshot и корректно отображается на клиенте.

---

### Этап 6. Сохранения и обратная совместимость

Текущая persistence-модель уже содержит числовые поля:

```text
QuestSaveData.reputation[].factionId : int
QuestSaveData.knownFactions           : List<int>
```

На первом этапе формат JSON не менять.

**Сделать:**

1. Интерпретировать сохранённые числа как `wireId`.
2. Сохранить значения `1–15` без изменений.
3. Разрешить новые значения `>=16`.
4. Сохранять `wireId`, а не порядковый индекс списка ассетов.
5. Добавить проверку неизвестного wire ID при загрузке.
6. Не удалять неизвестные записи молча — выдавать диагностическое сообщение.
7. Проверить round-trip:

```text
старый save → load → runtime → save
новый save    → load → runtime → save
```

**Выходной критерий:** старые сохранения не теряют репутацию, новые фракции сохраняются и загружаются.

---

### Этап 7. CSV, локализация и editor tools

**Целевые файлы:**

```text
Assets/_Project/Quests/Editor/QuestCsvImporter.cs
Assets/_Project/Quests/Editor/NpcCsvImporter.cs
Assets/_Project/Quests/Editor/DialogCsvImporter.cs
Assets/_Project/Quests/Editor/QuestCsvExporter.cs
Assets/_Project/Quests/Editor/QuestCsvSchema.cs
Assets/_Project/Quests/Editor/QuestDefinitionValidator.cs
Assets/_Project/Quests/Editor/QuestNodeGraphView.cs
Assets/_Project/Editor/Localization/LocalizationStringMigrator.cs
Assets/_Project/Editor/Localization/LocalizationTableRepair.cs
```

**Сделать:**

1. CSV принимает `factionKey`.
2. Импорт разрешает key через registry.
3. Неизвестный key даёт ошибку, а не заменяется молча на `None`/`Neutral`.
4. Старые enum-имена временно поддерживаются как legacy aliases.
5. Экспорт использует стабильный `factionKey`.
6. Локализация строит ключи по `factionKey`.
7. PropertyDrawer-ы показывают реальные `FactionDefinition` ассеты.

Отдельно исправить путь поиска фракций в:

```text
QuestDatabaseAutoDiscover.cs
NpcWorldInspectorWindow.cs
```

Фактический источник должен быть единым:

```text
Assets/_Project/Resources/Data/Factions/
```

`QuestDatabase.factions` не должен оставаться вторым независимым источником истины.

**Выходной критерий:** новая фракция выбирается в editor tools, импортируется из CSV и отображается в инструментах.

---

### Этап 8. Legacy policy и финальная стабилизация

`FactionId.cs` на этом этапе не удалять.

Сделать:

1. Запретить использовать enum для создания новых фракций.
2. Оставить enum только для legacy-мэппинга.
3. Добавить диагностическое предупреждение при попытке создать дубликат старого ID.
4. Обновить документацию и комментарии в GDD.
5. Отдельно зафиксировать, что удаление enum является будущей задачей, если когда-либо понадобится.

---

## 6. Матрица изменения по слоям

| Слой | Изменение | Обязательность |
|---|---|---:|
| `FactionDefinition` | `factionKey`, `wireId`, validation | Обязательно |
| `FactionCatalog` | lookup по key/wire/legacy | Обязательно |
| Старые 10 ассетов | заполнение новых полей | Обязательно |
| AI object references | сохранить существующий подход | Не переписывать |
| Combat relations | перейти с enum на asset reference | Обязательно |
| Quest/dialog authoring | добавить asset references | Обязательно для полной поддержки |
| `QuestWorld` | единый runtime lookup | Обязательно |
| `QuestServer` | перебирать registry, не enum | Обязательно |
| DTO | оставить byte-структуру | Не менять на первом этапе |
| JSON saves | трактовать int как wire ID | Обязательно |
| CSV | key → registry resolve | Обязательно |
| Localization | использовать stable key | Обязательно |
| `FactionId.cs` | оставить legacy | Не удалять |
| `VengeanceMemory` | использовать новый key из definition | Минимальная адаптация |

---

## 7. Проверка пользователем

Автоматические playtests и screenshots выполняются пользователем.

### Editor-проверка

1. Создать новый `FactionDefinition`.
2. Убедиться, что новый ID вводится обычным полем, а не enum dropdown.
3. Задать `factionKey = TestFaction` и `wireId = 16`.
4. Убедиться, что дубликаты key/wire ID показываются как ошибка.
5. Убедиться, что старые ассеты сохранили прежние значения.
6. Убедиться, что новый ассет виден в списках AI, квестов и диалогов.

### Runtime-проверка

1. Назначить новую фракцию NPC.
2. Проверить боевые отношения.
3. Изменить репутацию.
4. Убедиться, что новая фракция попала в server snapshot.
5. Убедиться, что клиент показывает display name и цвет.
6. Открыть Knowledge и проверить отображение.

### Persistence-проверка

1. Сохранить репутацию новой фракции.
2. Перезапустить сцену/сервер.
3. Загрузить состояние.
4. Проверить сохранение `wireId = 16`.
5. Проверить старый save со значениями `1–15`.

### CSV-проверка

1. Импортировать CSV со старым enum-именем.
2. Импортировать CSV с новым `factionKey`.
3. Импортировать CSV с неизвестным ключом.
4. Проверить, что неизвестный ключ выдаёт явную ошибку.
5. Экспортировать данные обратно и проверить сохранение ключей.

---

## 8. Что не входит в этот план

Не выполнять в рамках основной задачи:

- полное удаление `FactionId.cs`;
- замену всех runtime-структур на `string`;
- изменение формата сетевых DTO без необходимости;
- ручное редактирование YAML ассетов;
- переименование существующих faction ID;
- перенос фракций в другой каталог без отдельного решения;
- переписывание уже работающих AI editor-ов на другую модель;
- создание отдельной параллельной системы `FactionRegistry`, если можно расширить текущий `FactionCatalog`.

---

## 9. Риски и меры защиты

| Риск | Мера защиты |
|---|---|
| Потеря старых enum-значений при смене типа поля | Параллельные legacy и reference-поля + миграция через `SerializedObject` |
| Дубликат key или wire ID | Editor validation + загрузочная проверка registry |
| Сломанные старые saves | Старые значения `1–15` сохраняются без изменений |
| Новый ID не приходит клиенту | Сервер перебирает registry, а не `Enum.GetValues` |
| Разные каталоги видят разные фракции | Один фактический источник `Resources/Data/Factions` |
| Переиспользование старого ID | Правило immutable IDs и запрет wire ID reuse |
| Ошибка в CSV | Unknown key → import error, не silent fallback |
| Разрастание изменения на весь проект | Меняются только identity boundaries; presentation-only ссылки обновляются механически |

---

## 10. Итоговое архитектурное решение

Основной источник фракции:

```text
FactionDefinition asset
```

Идентичность:

```text
factionKey — стабильный текстовый content key
wireId     — стабильный числовой network/save ID
```

Совместимость:

```text
FactionId enum — legacy mapping для старых ассетов и сохранений
```

Новые authoring-ссылки:

```text
FactionDefinition reference
```

Единый lookup:

```text
FactionCatalog/FactionRegistry
```

Главное правило: **новая фракция добавляется созданием нового ассета и регистрацией его `factionKey`/`wireId`, а не редактированием C# enum.**

---

## 11. Журнал выполнения

### Этап 1 — выполнен 24 августа 2026 г.

Изменено:

- `FactionDefinition` получил авторские поля `factionKey` и `wireId`.
- Добавлены вычисляемые legacy-fallback свойства `EffectiveFactionKey`, `EffectiveWireId` и `HasValidIdentity`.
- `CombatKey` переведён на стабильный ключ с сохранением прежнего результата для старых ассетов.
- `FactionCatalog` расширен lookup-ами по `factionKey`, `wireId` и legacy `FactionId`.
- Добавлены проверки пустого ключа, диапазона `wireId`, несоответствия legacy ID и дубликатов.
- Старый enum `FactionId` не удалялся и gameplay-поведение не переводилось на строки.

Проверка:

- Unity compile check: `No compile errors`.
- Старые ассеты до миграции продолжали читаться через fallback на `FactionId`.

Статус: **закрыт**. Коммит: `5d998d5a`.

### Этап 2 — выполнен 24 августа 2026 г.

Изменено:

- Добавлен `Assets/_Project/Quests/Editor/FactionIdentityMigration.cs`.
- Инструмент использует `SerializedObject`, `EditorUtility.SetDirty` и `AssetDatabase.SaveAssets`.
- Заполнены `factionKey` и `wireId` для всех 10 существующих ассетов из `Resources/Data/Factions`.
- Legacy-поле `factionId`, numeric values и `.meta` не изменялись.

Проверка:

- В каталоге подтверждено 10 ассетов.
- Для всех 10 ассетов `factionKey` совпадает с прежним enum-именем, а `wireId` совпадает со старым numeric ID.
- Повторный запуск инструмента дал `scanned=10, migrated=0, unchanged=10, warnings=0`, что подтверждает идемпотентность.
- Unity compile check: `No compile errors`.

Статус: **закрыт**. Коммит: `b752c933`.

### Этап 3 — выполнен 24 августа 2026 г.

Изменено:

- `FactionCombatRelation` получил ссылку `targetFactionDefinition`; старый `targetFaction` сохранён как legacy fallback.
- Combat lookup в `FactionDefinition` переведён на стабильный `wireId`, при этом enum-overload оставлен для старых callers.
- AI-проверки в `NpcGroupController`, `NpcBrain` и `NpcSocialBrain` используют `FactionDefinition` references.
- `NpcWorldInspectorWindow` теперь сканирует `Assets/_Project/Resources/Data/Factions`.
- Создание фракции в инспекторе больше не перебирает `FactionId` и создаёт ассет с `factionId=None`, новым `factionKey` и первым свободным `wireId` из диапазона `16..255`.
- Редактор боевых отношений использует `FactionDefinition` object field и сохраняет legacy enum только когда он доступен.

Проверка:

- Unity compile check: `No compile errors`.
- Editor structural test: `catalog=10; testIdentity=Stage3_TestFaction:16; hostile=True; selfAllied=True`.
- В `NpcWorldInspectorWindow` больше нет `Enum.GetValues(typeof(FactionId))` и старого каталожного пути.
- В AI больше нет проверок боевых отношений через `o.faction.factionId`.
- Playtest и screenshots не выполнялись: по правилам проекта их делает пользователь.

Статус: **закрыт по реализации**; runtime playtest пользователя остаётся отдельной проверкой. Коммит: `aa609b0c`.

### Этап 4 — выполнен 24 августа 2026 г.

Изменено:

- В `NpcDefinition` и `AttitudeLink` добавлены `FactionDefinition` references с legacy fallback.
- В `QuestDefinition`, `QuestObjective`, `QuestRewardReputation` и `QuestPrerequisite` добавлены asset references с сохранением enum-полей.
- В `DialogueAction` и `DialogueCondition` добавлены `FactionDefinition` references с сохранением `factionParam`.
- В `KnowledgeLossConfig` добавлен массив `neverForgetFactionRefs` с сохранением `neverForgetFactions`.
- `FactionIdentityMigration` расширен one-shot миграцией вложенных NPC, Quest, DialogTree и Knowledge authoring-полей через `SerializedObject`.

Проверка:

- Unity compile check: `No compile errors`.
- Первый запуск миграции: `scanned=10, migrated=5, warnings=0`.
- Повторный запуск: `scanned=10, migrated=0, warnings=0`, что подтверждает идемпотентность.
- После миграции подтверждено: `npcs=3; npcRefs=3; quests=2; questRefs=1; dialogs=4; dialogRefs=0; knowledgeConfigs=1`.
- YAML вручную не редактировался.

Статус: **закрыт по реализации**; runtime-потребители будут переключены на references на этапе 5. Коммит: `8988a3a0`.

### Этап 5 — выполнен 24 августа 2026 г.

Изменено:

- `QuestWorld` переведён на внутренние ключи `int wireId` для reputation и faction knowledge.
- Добавлены registry-boundary overloads для `FactionDefinition`, `FactionId` и числового `wireId`.
- `QuestServer.BuildReputationSnapshot` теперь перебирает зарегистрированные `FactionDefinition`, а не `Enum.GetValues`.
- Сетевой DTO `byte` не изменён; отправляется `FactionDefinition.EffectiveWireId`.
- `ReputationClientState` больше не добавляет hardcoded `Neutral=11`.
- `CharacterWindow` и `KnowledgeToast` ищут faction по `wireId` через `FactionCatalog`.
- Neutral в сохранениях и UI разрешается через registry по legacy-маппингу.
- `WorldEvent` получил `FactionWireId`, legacy `Faction` сохранён для совместимости.
- Runtime evaluation для quest prerequisites, dialogue conditions/actions, reputation rewards и knowledge unlocks использует asset reference прежде legacy enum.

Проверка:

- Unity compile check: `No compile errors`.
- Editor structural test: `catalog=10; rep16=7; known16=True; neutralWire=0`.
- Для тестового `wireId=16` значение репутации и knowledge key сохраняются в runtime без enum cast.
- В целевых server/client presentation местах не осталось преобразований сетевого byte обратно в `FactionId`.
- Playtest и screenshots не выполнялись: по правилам проекта их делает пользователь.

Статус: **закрыт по реализации**; runtime playtest пользователя остаётся отдельной проверкой. Коммит: `16a873d7`.

### Этап 6 — выполнен 24 августа 2026 г.

Изменено:

- `QuestSaveData.FactionRepSaveEntry.factionId` документирован как стабильный `wireId`; JSON-поле и тип `int` не менялись.
- `QuestSaveData.knownFactions` документирован как список стабильных `wireId`; формат JSON не менялся.
- `QuestWorld` ограничивает runtime wire ID диапазоном `1..255`, совместимым с сетевым `byte`.
- При загрузке сохраняются значения `>=16`, а неизвестные зарегистрированному каталогу IDs не удаляются молча — выдаётся warning.
- Старые значения `1..15` остаются численно неизменными.

Проверка:

- JSON round-trip: `jsonHasWire16=True; rep16=13; known16=True; neutralKnown=True`.
- Unity compile check: `No compile errors`.
- Формат `QuestSaveData` на первом этапе не менялся.
- Playtest и screenshots не выполнялись: по правилам проекта их делает пользователь.

Статус: **закрыт по реализации**; runtime persistence playtest пользователя остаётся отдельной проверкой. Коммит: `a891d3d8`.

### Этап 7 — выполнен 24 августа 2026 г.

Изменено:

- Добавлен общий `FactionCsvResolver`: новый CSV использует `factionKey`, legacy enum names поддерживаются как aliases.
- `QuestCsvImporter` записывает `FactionDefinition` references для quest faction, NPC faction, actions и reputation rewards.
- `NpcCsvImporter` записывает `AttitudeLink.targetFactionRef` и выдаёт error для неизвестного faction key.
- `DialogCsvImporter` записывает faction references в conditions/actions и выдаёт error для неизвестного ключа.
- `QuestCsvExporter` экспортирует стабильные `factionKey` вместо enum names, если reference задан.
- Validator и Quest/Dialogue/NPC editor drawers показывают `FactionDefinition` object fields.
- `QuestDatabaseAutoDiscover` и `QuestDatabase` используют единый путь `Assets/_Project/Resources/Data/Factions`.
- После rescan `QuestDatabase` подтверждён как синхронизированный: `factions=10; npcs=3; quests=2; dialogs=4`.
- Localization migrator/repair используют `factionKey` с legacy fallback и исправленным `reputationThresholds` property.
- Graph/database/NPC/quest editor отображают stable key/wire identity.

Проверка:

- Unity compile check: `No compile errors`.
- Resolver test: `key=True:1; legacy=True:11; unknown=False:Unknown faction 'DoesNotExist'...`.
- Неизвестный ключ не заменяется молча на `None`/`Neutral`.
- Playtest, screenshots и фактический пользовательский CSV round-trip не выполнялись: это остаётся проверкой пользователя.

Статус: **закрыт по реализации**; editor/CSV user verification остаётся отдельной проверкой. Коммит: `9ee66a86`.

### Этап 8 — выполнен 24 августа 2026 г.

Изменено:

- Добавлен `FactionDefinitionEditor`: `factionKey` и `wireId` редактируются как обычные поля, legacy `factionId` отображается read-only.
- В редакторе добавлены ошибки для пустого key, недопустимого диапазона wire ID, reserved IDs `1..15` для новых ассетов, mismatch legacy ID и дубликатов key/wire.
- `FactionDefinition.OnValidate` дублирует критические identity-проверки на уровне ассета.
- `FactionId.cs` не удалён и явно документирован как legacy compatibility enum; новые enum values добавлять нельзя.
- Старый `docs/dev/FACTION_ID_DEHARDCODE_PLAN.md` помечен устаревшим и ссылается на этот исполняемый план.

Проверка:

- Unity compile check: `No compile errors`.
- Catalog validation: `catalog=10; allValid=True; newAssetIdentity=True; wire16Registered=False`.
- В проекте нет `Enum.GetValues(typeof(FactionId))` для генерации пула и нет старого каталожного пути `Assets/_Project/Quests/Data/Factions`.
- Новая in-memory identity с `factionId=None`, `factionKey=Stage8_NewFaction`, `wireId=16` проходит validation.
- `FactionId.cs` и legacy semantics значений `0..15` сохранены.
- Playtest и screenshots не выполнялись: по правилам проекта их делает пользователь.

Статус: **закрыт по реализации**; финальная editor/runtime/persistence/CSV проверка пользователем остаётся отдельной проверкой. Коммит создаётся после записи этого отчёта.
