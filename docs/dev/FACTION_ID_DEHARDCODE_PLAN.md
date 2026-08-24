# FACTION-DEHARDCODE: вывод FactionId из хардкода (архивный план)

> Статус: **устарел и заменён**.
> Актуальный исполняемый план и журнал выполнения находятся в `docs/NPC_quests/Faction/FACTION_ID_DATA_DRIVEN_PLAN.md`.
> Этот документ сохраняется только как исторический аудит; выполнять его этапы отдельно нельзя.
> Тикеты-родители: T-Q01 (FactionId promotion), T-FACTION-UNIFY.
> Дата: 2026-08-20.

## 1. Как устроено сейчас (as-is)

**«Общий пул» фракций — это C# enum, не ассеты и не конфиг:**

```
Assets/_Project/Quests/Factions/FactionId.cs
namespace ProjectC.Factions
public enum FactionId { None=0, GuildOfThoughts=1, ..., Villagers=15 }  // 16 значений
```

Схема «двойного определения», которая и вызывает путаницу:

1. **Пул** задан в enum (`FactionId.cs`) — хардкод в коде.
2. **Ассеты** (`Assets/_Project/Resources/Data/Factions/*.asset`, 10 штук) через поле
   `FactionDefinition.factionId` (`Quests/Factions/FactionDefinition.cs:65`) *выбирают*
   своё значение из этого enum. «Дропдаун» в инспекторе — это **встроенный enum-popup
   Unity**, а не кастомный редактор: Unity сам рисует выпадающий список для любого
   enum-поля. В YAML ассета сериализуется просто int (`factionId: 15`).
3. **Каталог** `FactionCatalog` (`Scripts/Knowledge/FactionCatalog.cs`) на старте
   грузит все `FactionDefinition` из `Resources/Data/Factions/` и строит
   `Dictionary<FactionId, FactionDefinition>` — т.е. ассеты это *контент*,
   а *ключ* всё равно enum.

Кто ещё потребляет enum (28 файлов, основные):

| Слой | Файлы |
|---|---|
| Данные (сериализованные поля `FactionId`) | `NpcDefinition.faction`, `QuestDefinition.faction`, `QuestObjective.targetFaction`, `QuestReward.faction`, `QuestPrerequisite.factionParam`, `DialogueCondition.factionParam`, `DialogueAction.factionParam`, `FactionCombatRelation.targetFaction` (`FactionRelation.cs:33`), `KnowledgeLossConfig.neverForgetFactions` |
| Runtime | `QuestWorld` (`HashSet<FactionId>`, касты byte↔enum), `QuestServer`, `KnowledgeManager`, `KnowledgeToast`, `CharacterWindow`, `WorldEvent` |
| Сеть (уже byte, не enum) | `ReputationClientState.KnownFactionIds: HashSet<byte>`, `ReputationSnapshotDto`, `QuestSaveData` |
| CSV-импортеры | `QuestCsvImporter`, `NpcCsvImporter`, `DialogCsvImporter` (`Enum.TryParse<FactionId>`), `QuestCsvSchema`, `QuestDefinitionValidator` |
| Кастомные редакторы | `NpcWorldInspectorWindow` (`EditorGUILayout.EnumPopup` строка 1348, `Enum.GetValues` строка 1628), `NpcSocialBrainEditor.DrawFactionPopup` / `NpcSpawnerConfigEditor` (popup по *ассетам* FactionDefinition), `KnowledgeRevealTriggerEditor` (лейбл), `QuestNodeGraphView` |

Важно: **сетевой wire-формат уже не зависит от enum** — по сети ходят `byte`
(`KnownFactionIds`, `ReputationSnapshotDto`), enum лишь тип-обёртка над байтом.
Это главный рычаг: пул можно сделать data-driven, не ломая сеть.

## 2. Целевая архитектура

- **Данные:** `FactionDefinition.factionKey: string` — свободное текстовое поле
  (именно то, что просит пользователь: «поле, в котором мы задаём ID фракции»,
  не дропдаун). Ассет сам declares свой ID; enum больше не нужен.
- **Сеть:** `FactionDefinition.wireId: byte` — явное поле на ассете,
  валидируется на уникальность при загрузке (как сейчас дубликаты в FactionCatalog).
  Wire-формат byte остаётся — старые клиенты/сейвы не ломаются.
- **Runtime:** `FactionCatalog` (переименовать/расширить до `FactionRegistry`)
  строит `string key → FactionDefinition` и `byte wireId → FactionDefinition`
  из тех же `Resources/Data/Factions/`. Единственный источник правды.
- **`CombatKey`** (`FactionDefinition.cs:106`, мост для string-ключей
  VengeanceMemory) становится просто `factionKey` — мост больше не нужен.

## 3. Этапы миграции (каждый — отдельный коммит, ассеты не ломаются)

### Этап 1 — не-брейкинг: добавить поля + миграционный тул
- В `FactionDefinition`: добавить `public string factionKey` и `public byte wireId`;
  старое поле `factionId` пометить `[Obsolete]` (остаётся в ассетах).
- Кастомный инспектор `FactionDefinition`: `factionKey` рисуется **plain string
  полем** (без дропдауна) + валидации: не пустое, без пробелов, PascalCase,
  уникальность по каталогу (красный лейбл при дубликате). Старое `factionId`
  скрывается за foldout «Legacy (read-only)».
- `FactionCatalog`: ключует по `factionKey`, если заполнено; иначе fallback на
  старый enum (переходный режим).
- **Editor тул** `ProjectC Factions/Tools/Migrate Faction Assets` (меню, IDEMPOTENT):
  по всем ассетам в `Resources/Data/Factions/` — если `factionKey` пуст,
  заполнить из старого enum (15 → `"Villagers"`); если `wireId == 0`,
  взять значение старого enum (15 → `15`). `EditorUtility.SetDirty` +
  `AssetDatabase.SaveAsPrefabAsset`/`SaveAssets`. YAML-файлы **не правим руками**,
  `.meta` не трогаем — Unity сам пересериализует поля.
- До прогона тула ассеты на диске не меняются; после — в YAML появляются
  `factionKey: Villagers`, `wireId: 15`, старое `factionId: 15` остаётся до этапа 3.

### Этап 2 — данные: enum-поля → string
- Заменить `FactionId` на `string` в: `NpcDefinition.faction`,
  `QuestDefinition.faction`, `QuestObjective.targetFaction`, `QuestReward.faction`,
  `QuestPrerequisite.factionParam`, `DialogueCondition.factionParam`,
  `DialogueAction.factionParam`, `FactionCombatRelation.targetFaction`,
  `KnowledgeLossConfig.neverForgetFactions`.
- **Editor тул** миграции NPC/Quest/Dialogue-ассетов: старое int-поле → строка
  имени enum (через `SerializedObject`, one-shot, идемпотент).
- CSV-импортеры: `Enum.TryParse<FactionId>` → raw-string + валидация по
  `FactionRegistry` (неизвестный ключ = импорт-ошибка с понятным сообщением,
  а не тихий `None`).
- Runtime: сравнения `== FactionId.X` → строковые сравнения (case-sensitive exact);
  `QuestWorld: HashSet<FactionId>` → `HashSet<string>`; байты на сети —
  через `wireId` из реестра.
- `FactionDefinition.CombatKey` → `factionKey`.

### Этап 3 — удалить enum
- Удалить `FactionId.cs`.
- `NpcWorldInspectorWindow`: `EditorGUILayout.EnumPopup` (1348) и
  `Enum.GetValues` (1628) → popup по ключам из `FactionRegistry`
  (тот же паттерн, что уже работает в `NpcSocialBrainEditor.DrawFactionPopup`).
- `ReputationClientState:73` `(byte)FactionId.Neutral` → wireId из реестра
  (резервированный ключ `"Neutral"`).
- `CharacterWindow` / `KnowledgeToast` / `QuestWorld`: касты `(FactionId)byte`
  → lookup в реестре по `wireId`.
- Семантика `None`: исчезает как значение — пустая строка / null = «нет фракции»;
  `"Neutral"` — закреплён за нейтральным ассетом (как сейчас `FactionId.Neutral`).

## 4. Что НЕ ломается (проверено по коду)

- **Ассеты фракций** — YAML int-поле `factionId` остаётся до этапа 3;
  миграция только добавляет поля, правится тулом, не руками.
- **Кастомные редакторы по ассетам** (`NpcSocialBrainEditor`, `NpcSpawnerConfigEditor`)
  — работают через `ObjectReference` на `FactionDefinition`, не зависят от enum;
  меняется только лейбл `[{fd.factionId}]` → `[{fd.factionKey}]`.
- **Сеть** — `byte`-DTO не меняются; маппинг key↔byte живёт в реестре.
- **VengeanceMemory** — уже string-ключи, после этапа 2 ключ = `factionKey`
  (совпадает с текущим `CombatKey` = имя enum, т.е. runtime-поведение то же).

## 5. Риски

| Риск | Митигация |
|---|---|
| Unity тихо роняет неизвестные поля при смене типа (int→string при переименовании) | Два этапа: сначала *добавить* string, потом *убрать* enum; миграция через `SerializedObject` в туле |
| Старые сейвы (`QuestSaveData`) могут хранить faction как int | Переходный legacy-маппинг int→key в реестре до полного выгорания сейвов; проверить версию сейва перед этапом 3 |
| Дубликаты `factionKey` в ассетах | Валидация в инспекторе + warning при загрузке (аналог текущего дубликат-логика FactionCatalog) |
| 28 файлов ссылаются на enum | Этапы изолированы: после этапа 2 enum используется только в редакторах и legacy-fallback; этап 3 — механическая замена |

## 6. Верификация (прогоняет пользователь)

После каждого этапа:
1. Unity Editor → Console: 0 ошибок; `[FactionCatalog] Loaded N faction definitions`
   (N = 10 на старте).
2. Инспектор `Faction_Villagers.asset`: `factionKey` — обычное текстовое поле,
   дропдауна нет; при дубликате — красный warning.
3. `NpcSocialBrain` / `NpcSpawnerConfig`: дропдаун фракций по-прежнему листит
   ассеты, выбор сохраняется.
4. `NpcWorldInspectorWindow` (после этапа 3): popup фракций жив, значения
   совпадают с ассетами.
5. CSV-импортеры: повторный импорт тех же CSV — 0 ошибок, неизвестный ключ
   даёт понятную ошибку.
6. PlayMode: репутация/знание фракций в `CharacterWindow` отображаются
   с именами и цветами как до миграции.
7. Сейв-раундтрип: `QuestSaveData` load→save без потери faction-полей.
