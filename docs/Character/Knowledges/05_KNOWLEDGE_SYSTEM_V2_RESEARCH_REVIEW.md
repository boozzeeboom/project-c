# Система Знаний v2 — Финальный ресерч-ревью (сверка с 04 + план внедрения + UI-дизайн)

> **Статус:** Ресерч завершён. Код-ревью проведён по состоянию на 2026-08-01.
> **Автор:** Mavis (Hermes)
> **Основание:** задача «внедрить систему знаний во все аспекты игры»; сверка с `04_KNOWLEDGE_SYSTEM_V2_DEEP_ANALYSIS.md`.
> **Охват:** CharacterWindow (вкладка «Репутация» → «Знания»), навыки (боевые+социальные), смерть игрока, крафт/рецепты, серверная персистенция per-player, вынос из хардкода.

---

## 0. Резюме (TL;DR)

1. **Текущая v1 системы знаний** (фракции + NPC) — работает: `QuestWorld._knownFactions/_knownNpcs` → `QuestSaveData` → `ReputationSnapshotDto/NpcAttitudeSnapshotDto` → `ReputationClientState/NpcAttitudeClientState` → вкладка «Репутация». Архитектура готова к расширению (это и было заявлено в 02/04).
2. **Первичный ресерч 04 в целом верный** (направление, 80% структуры данных), но содержит **12 реальных нестыковок с кодом** (см. §3). Ключевые:
   - счётчики навыков «35+4» неверны — в `Resources/Skills` **30 ассетов** (26 combat + 4 social);
   - код-стайл: `RecipeData` — приватные `[SerializeField]` + свойства, а не публичные поля как в 04;
   - `QuestUnlockType.Recipe = 2` **уже есть в схеме наград квестов** и нигде не обрабатывается — это готовый хук для знаний рецептов, 04 его не заметил;
   - `FactionId` имеет **16 значений**, а `FindFactionFallback` в UI знает только **5** — хардкод, который сломает отображение знаний о большинстве фракций;
   - `CraftingServer.StartCraftRpc` **не проверяет** ни знания рецепта, ни `AllowedRecipes` станции — знания без серверного гейта = эксплойт;
   - персистенция в 04 дублирует `knownSkills` (и в `QuestSaveData`, и в `SkillsSave`) — в коде навыки живут в `CharacterSaveData.skills`, а фракции/NPC/квесты — в `QuestSaveData` (два файла, не один);
   - `knownQuests` в `KnowledgeSummaryDto` редундантны — состояния квестов уже приходят в `QuestSnapshotDto` (включая `Discovered`).
3. **Смерть:** точки хука найдены точно — `PlayerTarget.TriggerDeathRespawn()` → `PlayerRespawnTracker.RespawnWithHpRestore()`. Потери знаний сейчас нет нигде.
4. **UI:** спроектирована двухпанельная вкладка «Знания» (категории слева + детали справа) с бейджами, empty-состояниями и строгим следованием правилам `docs/UI` (§5). Сохраняем канонический паттерн `EnsureBuilt` CharacterWindow (Clear+CloneTree+Resources.Load — для auto-spawned окон это правильно).
5. **Рекомендация по сетевому каналу:** НЕ переезжать с рабочих каналов v1 (фракции/NPC) на единый `KnowledgeSummaryDto` сразу. Гибрид: новый DTO несёт только новые типы (навыки + рецепты), квесты выводятся из `QuestSnapshotDto`. Меньше риск, ноль миграции.

---

## 1. Как знания работают СЕЙЧАС (v1) — фактическая карта кода

### 1.1. Сервер (QuestWorld)

| Хранилище | Тип | Код |
|---|---|---|
| `QuestWorld._knownFactions` | `Dictionary<ulong, HashSet<FactionId>>` | `Quests/Core/QuestWorld.cs` (~L729) |
| `QuestWorld._knownNpcs` | `Dictionary<ulong, HashSet<string>>` | там же (~L733) |
| `MarkNpcTalked(clientId, npcId)` | добавляет NPC **и** фракцию NPC (через `NpcDefinition.faction`) | ~L782–807 |
| `UnlockFactionKnowledge` / `UnlockNpcKnowledge` | точечные апдейты + `SavePlayer` | ~L823–846 |
| `BuildSaveData` → `QuestSaveData.knownFactions/knownNpcs` | disk persistence | ~L1311–1326 |
| `LoadPlayer` | восстановление; **Neutral добавляется всегда** | ~L1450–1472 |

### 1.2. Сеть (QuestServer + NetworkPlayer)

- `QuestServer.BuildReputationSnapshot(clientId)` итерирует **ВСЕ** `FactionId 0..15` и кладёт `knownFactionIds` — `Quests/Network/QuestServer.cs` ~L999–1021.
- `BuildNpcAttitudeSnapshot` итерирует NPC из квестовых objective + `knownNpcIds` — ~L1023–1064.
- `BroadcastKnowledgeChange(clientId)` шлёт оба снапшота после `MarkNpcTalked` — ~L1139–1147 (вызов ~L523).
- RPC на `NetworkPlayer`: `ReceiveReputationSnapshotTargetRpc` (L1899), `ReceiveNpcAttitudeSnapshotTargetRpc` (L1905).

### 1.3. Клиент (client states + UI)

- `ReputationClientState.KnownFactionIds: HashSet<byte>` (Neutral подмешивается всегда), `NpcAttitudeClientState.KnownNpcIds: HashSet<string>`.
- `CharacterWindow` вкладка `tab-reputation` → `#reputation-section`:
  - `RefreshReputationCache()` — фильтрует снапшот по `KnownFactionIds` (+fallback «пусто → Neutral»), строка `CharacterWindow.cs` ~L1003–1055;
  - `RefreshNpcAttitudeCache()` — ~L1107–1143;
  - `FormatNpcDisplayName()` — Editor: `AssetDatabase` + runtime: `Resources("Data/Npcs/")` + fallback — ~L1146–1179 (хрупко, см. §3.7);
  - `FindFactionFallback()` — **хардкод-маппинг 5 фракций** — ~L1068–1080 (см. §3.6).

---

## 2. Связанные подсистемы (полный список для навыков и крафта)

### 2.1. Навыки — карта зацепок

| Подсистема | Файл | Роль в knowledge-фиче |
|---|---|---|
| Данные навыка | `Scripts/Skills/SkillNodeConfig.cs` | + поля способа открытия (knowledge unlock) |
| Каталог на сервере | `Scripts/Skills/SkillsWorld.cs` | + `_knownSkills`, `UnlockSkillKnowledge`, `AutoOnSkillLearned`, расширение `BuildSaveData/LoadPlayer` |
| Каталог на клиенте | `Scripts/Skills/SkillsClientState.cs` | `CurrentSkills` (выученные), события `OnSkillsUpdated/OnSkillResult` |
| RPC-хаб | `Scripts/Skills/SkillsServer.cs` | + бродкаст знаний; расширение `SkillsSnapshotDto` |
| UI дерево (бой) | `Scripts/Skills/UI/SkillTreeWindow.cs` | + фильтр «неизвестные скрыты» поверх discipline-чипов |
| UI дерево (соц.) | `Scripts/Skills/UI/SocialSkillTreeWindow.cs` | то же |
| UI персонаж | `Scripts/UI/Client/CharacterWindow.cs` | вкладка «Знания»: списки навыков |
| Персистенция | `Scripts/Stats/Persistence/SkillsSave.cs` | + `knownSkillIds` (внутри `CharacterSaveData.skills`) |
| Сохранение | `StatsServer.SaveCharacter` → `JsonCharacterDataRepository` | файл `character_{clientId}.json` |

**Важно про каталог:** сервер грузит навыки через `SkillsWorld.LoadAllSkills(config)` → `Resources.LoadAll<SkillNodeConfig>(config.SkillsResourcesPath)` (default `"Skills"`), клиент — `Resources.LoadAll<SkillNodeConfig>("Skills")` жёстко. Источник один (Resources/Skills), но путь на сервере — из конфига. Если путь конфига изменится — каталоги разъедутся. Зафиксировать как инвариант: **сервер и клиент должны читать один и тот же каталог**.

**Факт по количеству:** в `Assets/_Project/Resources/Skills/` **30 ассетов**, все с уникальными `skillId`: 26 combat + 4 social (`social_basic_talk`, `social_barter`, `social_persuasion`, `social_leadership`). Цифры «35+4» из 04 неверны → бейджи в UI считать из `Resources` runtime, не хардкодить.

### 2.2. Крафт — карта зацепок

| Подсистема | Файл | Роль |
|---|---|---|
| Данные рецепта | `Scripts/Crafting/RecipeData.cs` | + поля способа открытия (private `[SerializeField]` + свойства!) |
| Сервер крафта (статик) | `Scripts/Crafting/CraftingWorld.cs` | + `_knownRecipes` static, `UnlockRecipeKnowledge/IsRecipeKnown/ApplyDeathRecipeLoss` |
| RPC | `Scripts/Crafting/CraftingServer.cs` | **серверный гейт знания в `StartCraftRpc`** (сейчас нет) |
| Станция | `Scripts/Crafting/CraftingStation.cs` | `CanStartCraft` — не проверяет ни знания, ни `AllowedRecipes` (L117–130) |
| Конфиг станции | `Scripts/Crafting/CraftingStationConfig.cs` | `AllowedRecipes` — доступность рецепта на станции |
| Клиент | `Scripts/Crafting/CraftingClientState.cs` | `_recipeCache = Resources.LoadAll<RecipeData>("Crafting/Recipes")`; нет KnownRecipeIds |
| UI | `Scripts/Crafting/CraftingWindow.cs` | `GetRecipeDisplayList()` показывает `_currentConfig.AllowedRecipes` без фильтра знаний (L256–295) |
| Награды квестов | `Quests/Quests/QuestReward.cs` | **`QuestUnlockType.Recipe = 2` уже объявлен** (`unlockId = recipeId`) — обработчика нет |

**Ключевой факт:** `QuestUnlockType` уже содержит `Recipe = 2` и `Achievement = 3` (пометка «(future)»). `grep` по `QuestUnlockType.Recipe` по всему `Quests/` — **0 использований**. Это готовый, заранее спроектированный хук: реализовать `QuestUnlockType.Recipe` в `ApplyQuestRewards` → `CraftingWorld.UnlockRecipeKnowledge`. 04 этот факт упустил и предложил параллельный механизм (`RecipeData.knowledgeQuestId`) — оба могут жить, но существующий путь дешевле и консистентнее.

### 2.3. Смерть — карта

| Точка | Файл | Что происходит |
|---|---|---|
| Получение урона | `Scripts/Combat/Implementations/PlayerTarget.cs` `ApplyDamage` (~L210–250) | HP ≤ 0 → `_isDead`, отключение ввода, старт таймера |
| Таймер смерти | `PlayerTarget.Update` (~L188–196) | по истечении → `TriggerDeathRespawn()` |
| Респавн | `TriggerDeathRespawn()` (~L280–318) → `PlayerRespawnTracker.RespawnWithHpRestore(hpPercent)` (`Scripts/Player/PlayerRespawnTracker.cs` L353–406) | телепорт → anim Idle → HP restore → ввод включён |

**Хук потери знаний:** после успешного `RespawnWithHpRestore` (внутри `TriggerDeathRespawn`, server-side, с проверкой `QuestWorld.Instance != null`). 04 предлагает то же — подтверждаю, точка верная. Нюансы:
- не вызывать при выгрузке/шатдауне — только при реальной смерти игрока;
- `RespawnWithHpRestore` может прерваться (нет точки респавна) — потерю вешать **после** успешного респавна, иначе игрок потеряет знания без возрождения.

---

## 3. Сверка с 04 — реальные нестыковки для нашего кода

### 3.1. Совпадает (подтверждено кодом)

| Утверждение 04 | Код |
|---|---|
| `SkillNodeConfig` не имеет поля «как узнаётся» | ✅ подтверждено: полей knowledge нет |
| Все навыки видны в `SkillTreeWindow` сразу | ✅ подтверждено: фильтры только по `CombatDiscipline` (чипы All/Melee/Ranged/Defense/Placed) + поиск; knowledge-фильтра нет. *Нюанс: у `SkillTreeWindow` чипы уже есть — фильтр знаний встраивается в ту же строку.* |
| `SkillsWorld`: TryLearnSkill (5 шагов), TryForgetSkill, BuildSaveData/LoadPlayer | ✅ подтверждено (`SkillsWorld.cs`) |
| `SkillsSave` только с `learnedSkillIds` | ✅ подтверждено |
| `SkillsClientState.CurrentSkills` — HashSet выученных | ✅ подтверждено |
| Смерть: `PlayerTarget.TriggerDeathRespawn` + `RespawnWithHpRestore` (30% по умолчанию) | ✅ подтверждено |
| `QuestSaveData` + `knownFactions/knownNpcs` | ✅ подтверждено |
| v1 фильтрует UI по `knownFactionIds/knownNpcIds` | ✅ подтверждено (RefreshReputationCache/RefreshNpcAttitudeCache) |
| `QuestWorld.BuildSaveData/LoadPlayer` — точка расширения | ✅ подтверждено |
| Neutral фракция изначально известна | ✅ подтверждено (и сервер, и `ReputationClientState`) |

### 3.2. Нестыковки (расхождения с фактическим кодом)

| # | Утверждение 04 | Факт в коде | Что делать |
|---|---|---|---|
| Н1 | Счётчики «35 боевых + 4 соц.» | **30 ассетов** (26 combat + 4 social) | Бейджи/заголовки считать из `Resources.LoadAll` runtime |
| Н2 | Код-стайл `RecipeData`: публичные поля (`public RecipeKnowledgeUnlockType ...`) | `RecipeData` — приватные `[SerializeField]` + публичные свойства (конвенция проекта) | Новые поля — приватные + свойства. В `SkillNodeConfig` наоборот — публичные поля (стиль файла сохраняем) |
| Н3 | 04 не упоминает `QuestUnlockType.Recipe = 2` | Уже объявлен в `QuestReward.cs`, не обрабатывается | Реализовать обработчик в `ApplyQuestRewards` — это и есть хук знаний рецептов |
| Н4 | (неявно) рецепт доступен по знанию — достаточно фильтра UI | `CraftingServer.StartCraftRpc` не проверяет ни знание, ни `AllowedRecipes` станции | Обязателен серверный гейт `IsRecipeKnown` (и заодно `AllowedRecipes`) |
| Н5 | Единый `KnowledgeSummaryDto` заменяет `ReputationSnapshotDto/NpcAttitudeSnapshotDto` | v1-каналы работают и уже потребляются UI | Гибрид: новый DTO только для навыков+рецептов; фракции/NPC не трогаем |
| Н6 | `QuestSaveData` + `knownSkills` (и `SkillsSave` + `knownSkillIds`) | Навыки персистятся в `CharacterSaveData.skills` (файл `character_{id}.json`), квесты/фракции/NPC/рецепты — в `QuestSaveData` (файл `quest_{id}.json`) | `knownSkillIds` — только в `SkillsSave`; рецепты — в `QuestSaveData`; **без дублей** |
| Н7 | `knownQuestIds` в `KnowledgeSummaryDto` | Состояния квестов (вкл. `Discovered`) уже в `QuestSnapshotDto`; клиент знает их из `QuestClientState` | Не плодить канал — категория «Квесты» строится из `QuestClientState` |
| Н8 | Код-сниппеты 04 используют `skill.prerequisites` как строки | `prerequisites` — `SkillNodeConfig[]` (прямые ссылки) | `AutoOnSkillLearned` идёт по ссылкам конфигов (`cfg.skillId`), не по строкам |
| Н9 | 04 (шаг 10) пишет знания навыков через `QuestWorld`-флоу | Реально навыки грузит `SkillsWorld.LoadPlayer(clientId, CharacterSaveData)` внутри `StatsServer.LoadCharacter` | Хук `UnlockSkillKnowledge` не требует `QuestWorld`; достаточно `SkillsServer` → `SaveCharacter` |
| Н10 | 04 предлагает новый `DialogueAction.UnlockRecipe/UnlockSkillKnowledge` | `DialogueActionType` (enum в `QuestServer`) не содержит таких значений; `FireDialogAction` — огромный switch | Добавлять новые actions — да, но это расширение enum + switch (аккуратно, не сломать существующие case) |
| Н11 | 04: UI-названия фракций берутся из `FindFactionFallback` | Хардкод **5 фракций из 16** (`FactionId` имеет Bandits/Cultists/Guards/Villagers и др.) | Вынести отображение фракций в конфиг (FactionDefinition SO) — иначе знания о 11 фракциях рендерятся «unknown» |
| Н12 | 04 (неявно) NPC-имена из `Resources("Data/Npcs/")` надёжны | `FormatNpcDisplayName`: Editor `AssetDatabase`, runtime `Resources`, fallback `"Npc 004"` | Проверить, что `NpcDefinition` реально адресуемы в runtime (Resources или каталог); иначе имена в «Знаниях» будут мусором |

### 3.3. Что в 04 может НЕ работать (риски, найденные по коду)

1. **`StartCraftRpc` без серверного гейта** — клиент может вызвать RPC напрямую и крафтить неизвестный рецепт (знания server-authoritative, а проверки нет). Это «может не работать» в худшем смысле — дыра, а не баг.
2. **`minRetainFactions`-логика 04** — `toRemove.Take(Math.Max(0, toRemove.Count - toKeep))` режет с **конца** списка (т.е. оставляет первые помеченные на удаление), а не «сохранить случайные N». Плюс не учитывает защищённые (`neverForget`) при подсчёте. Нужно переписать: считать «удаляемые» = кандидаты минус защищённые, затем случайно выбрать `max(0, кандидаты - minRetain)`, при этом `minRetain` — от числа **незащищённых**, а не всех.
3. **RNG без сида** — тесты потери знаний недетерминированы. Ввести инъекцию `System.Random`/сида в конфиг потери.
4. **Потеря знаний при неудачном респавне** — хук после успешного респавна, иначе «смерть без возрождения = потеря знаний».
5. **Каталоги навыков сервера и клиента** — сервер берёт путь из `SkillsConfig.SkillsResourcesPath`, клиент хардкодит `"Skills"`. Расхождение путей сломает знание (сервер «знает» навык, клиент нет).
6. **`SkillTreeWindow` и `SocialSkillTreeWindow` имеют свои фильтры** — knowledge-фильтр надо добавить в оба, а не только в CharacterWindow (04 упоминает только SkillTreeWindow).
7. **`ApplyQuestRewards` вызывает `SavePlayer` без `BroadcastKnowledgeChange`** — если награда квеста даёт знание, клиент не узнает, пока не перезайдёт. После обработчика знания — бродкаст.
8. **CraftingWorld — статический класс**, пересоздаётся в `CreateAndInitialize/Shutdown` (CraftingServer.OnNetworkSpawn/OnNetworkDespawn). Новый `_knownRecipes` обязан чиститься в `Shutdown` и наполняться при `LoadPlayer` — иначе при рестарте сервера знания рецептов «всплывут» у всех или потеряются.

### 3.4. Что общего (сходится с 04 без изменений)

- Полный набор категорий знаний: фракции, NPC, навыки, рецепты (+квесты как производные).
- Механика «получить знание» = серверный trigger + save + broadcast → клиентский state → UI.
- Смерть теряет **часть** знаний (не всё), выученные навыки НЕ забываются (ADR-7) — поддерживаю: забывание навыков ломает боевую сборку и требует возврата XP; терять знания о навыках = «забыл как кастуется» — это отдельная механика, не сейчас.
- Всё настраиваемое — через ScriptableObject (конфиги), не хардкод.
- UI-фильтр знаний: «не знаешь — не видишь» (для фракций/NPC уже так в v1).

---

## 4. Дизайн данных (вынос из хардкода + персистенция)

### 4.1. SkillNodeConfig — способ открытия

```csharp
// Assets/_Project/Scripts/Skills/SkillNodeConfig.cs (стиль файла: ПУБЛИЧНЫЕ поля)
public enum KnowledgeUnlockType : byte
{
    None = 0,               // по умолчанию: виден и изучаем сразу (текущее поведение)
    LearnFirst = 1,         // открывается автоматически при изучении любого prerequisite-навыка
    Blueprint = 2,          // открывается предметом/документом (id в knowledgeUnlockId)
    NpcTeach = 3,           // открывается обучением у NPC (id в knowledgeUnlockId)
    QuestReward = 4         // открывается наградой квеста (id в knowledgeUnlockId)
}

public KnowledgeUnlockType knowledgeUnlockType = KnowledgeUnlockType.None;
public string knowledgeUnlockId = "";        // предмет / NPC / квест / рецепт-источник
public string knowledgeUnlockDescription = ""; // подсказка «как узнать» (для UI «Неизвестно — способ: ...»)
```

Семантика в `SkillsWorld`:
- `None` — как сейчас (виден, изучаем).
- `LearnFirst` — при `TryLearnSkill` навыка X: для всех навыков S, у которых `S.prerequisites` содержит X → `UnlockSkillKnowledge(S)`. Обход по ссылкам конфигов (см. Н8).
- `Blueprint/NpcTeach/QuestReward` — выдаются соответственно `InventoryWorld`/`DialogueAction`/`ApplyQuestRewards` через единый `SkillsWorld.UnlockSkillKnowledge(clientId, skillId)`.

### 4.2. RecipeData — способ открытия

```csharp
// Assets/_Project/Scripts/Crafting/RecipeData.cs (стиль файла: ПРИВАТНЫЕ [SerializeField] + свойства)
[SerializeField] private RecipeKnowledgeUnlockType _knowledgeUnlockType = RecipeKnowledgeUnlockType.Blueprint;
[SerializeField] private string _knowledgeUnlockId = "";      // предмет / NPC / квест / станция
[SerializeField] private string _knowledgeUnlockDescription = "";

public RecipeKnowledgeUnlockType KnowledgeUnlockType => _knowledgeUnlockType;
public string KnowledgeUnlockId => _knowledgeUnlockId;
public string KnowledgeUnlockDescription => _knowledgeUnlockDescription;

public enum RecipeKnowledgeUnlockType : byte
{
    Blueprint = 0,   // чертёж/предмет (по умолчанию — сохранить текущее «станция разрешает»)
    NpcTeach = 1,    // обучение у NPC
    QuestReward = 2, // награда квеста (совпадает с QuestUnlockType.Recipe)
    Station = 3,     // знание при первом использовании станции
}
```

### 4.3. Конфиг потери знаний при смерти (новый SO)

```
Assets/_Project/Data/Knowledge/KnowledgeLossConfig.asset  (ScriptableObject)
```
Поля (все настраиваемые, не хардкод):
- `enabled` (bool, default true);
- `minRetainFactions` (int, default 1) — сколько фракций минимум остаётся;
- `minRetainNpcs` (int, default 3) — сколько NPC минимум остаётся;
- `factionLossChance` (0..1, default 0.5);
- `npcLossChance` (0..1, default 0.3);
- `recipeLossChance` (0..1, default 0.25);
- `skillKnowledgeLossChance` (0..1, default 0.0 — ADR-7: навыки-знания не теряем, но параметр готов);
- `neverForgetFactions` (`FactionId[]`) — защищённые (рекомендую: `Neutral`, `GuildOfThoughts`, `GuildOfCreation` — сюжетные);
- `neverForgetNpcs` (`string[]`) — защищённые NPC (сюжетные);
- `randomSeed` (int, default 0 — 0 = без сида).

### 4.4. Персистенция (два файла, без дублей)

| Знание | Файл | Класс |
|---|---|---|
| Фракции, NPC, **рецепты** | `quest_{clientId}.json` | `QuestSaveData` + `knownRecipes: List<int>` (recipeId — по аналогии с `knownFactions`) |
| Навыки (learned + known) | `character_{clientId}.json` | `SkillsSave` + `knownSkillIds: string[]` |
| Квесты | производные от состояний | НЕ сохраняем отдельно |

`CraftingWorld` — статик: `BuildRecipeKnowledgeSave(clientId)`/`LoadRecipeKnowledge(clientId, List<int>)` вызываются из `QuestWorld.BuildSaveData/LoadPlayer` (связка допустима — проект уже использует статические World-классы; задокументировать порядок вызовов).

### 4.5. Сетевой канал — рекомендация (гибрид)

**Option A (04):** единый `KnowledgeSummaryDto` + `KnowledgeClientState` заменяет всё. Минусы: миграция двух рабочих каналов, больше движущихся частей, риск регресса вкладки.

**Option B (рекомендую):**
- `SkillsSnapshotDto` расширяется полем `knownSkillIds: string[]` — `SkillsServer.SendSnapshotToOwner` уже шлёт снапшот после каждого learn/forget; бродкаст после `UnlockSkillKnowledge` — тем же методом;
- новый маленький `RecipeKnowledgeDto { int[] knownRecipeIds }` + `NetworkPlayer.ReceiveRecipeKnowledgeTargetRpc` + `RecipeKnowledgeClientState` (auto-spawn в NMC по паттерну `Create*ClientState`);
- квесты — из `QuestClientState` (0 новых каналов);
- фракции/NPC — остаются на v1-каналах (0 изменений).

Итог: **1 новый DTO + 1 RPC + 1 client state** вместо полной замены. Меньше кода, ноль миграции, v1 не трогаем.

### 4.6. FactionDefinition (вынос хардкода отображения)

Новый SO `FactionDefinition` (или дополнение существующего фракционного конфига):
- `factionId: FactionId`, `displayName` (RU/EN), `color`, `sortOrder`, `loreDescription`.
- Каталог `FactionCatalog` (Resources), грузится `CharacterWindow` и заменяет `FindFactionFallback` (сейчас 5 из 16).
- Бонус: сюда же переезжают цвета репутации из `RefreshReputationCache` (сейчас захардкожены в C#: `Color`-ветки по `factionId`) — цвета в USS-классы по имени фракции, не в C#.

---

## 5. UI — вкладка «Знания» (детальный layout по docs/UI)

### 5.1. Правила, которые применяем (docs/UI + скилл unity-ui-toolkit-cs)

1. **Канонический паттерн CharacterWindow** (auto-spawned окно): `EnsureBuilt` = Resources.Load UXML fallback → Resources.Load USS fallback → `_doc.rootVisualElement.Clear()` → `_root = uxml.CloneTree()` + USS на оба → `Add(_root)` + absolute позиционирование. Для CharacterWindow-семейства это **правильно** (generic-правило «не делать Clear+CloneTree» относится к окнам с Inspector-присвоением; у auto-spawned его нет).
2. **USS: `!important` везде** (тип-селекторы темы перебивают класс-селекторы).
3. **Скроллбары**: только `.unity-scroller*` + предок `.unity-scroll-view` (SCROLLBAR_STYLING.md; в `CharacterWindow.uss` уже есть).
4. **Таб-бар**: `<ui:Button>` + `.tab-btn`/`.tab-btn.active` — не `Toggle` (label-цвет не каскадирует).
5. **Не дублировать скроллы**: ListView внутри ScrollView — двойной скролл. Категории — фиксированная колонка (не скролл), детали — один скролл/ListView.
6. **Цвета — в USS-классах**, не inline в C#; класс-состояние снимать в `bindItem` перед добавлением (ListView переиспользует элементы).
7. **Пустые состояния** — Label `.knowledge-empty`, показывать/прятать в C# (`display`).
8. **Одна фича за раз** — внедрять вкладку отдельным этапом, с прогоном Play Mode после каждого шага (правило workflow из скилла).

### 5.2. Целевая структура

```
tab-knowledge  (бывш. tab-reputation; label «ЗНАНИЯ»)
└── #knowledge-section
    ├── .knowledge-categories            ← левая колонка, фикс. ширина 150px, flex column
    │   ├── Button.knowledge-cat-btn.active   «Фракции»      + badge 3/16
    │   ├── Button.knowledge-cat-btn          «NPC»          + badge 5/12
    │   ├── Button.knowledge-cat-btn          «Навыки: бой»  + badge 8/26
    │   ├── Button.knowledge-cat-btn          «Навыки: соц.» + badge 2/4
    │   ├── Button.knowledge-cat-btn          «Рецепты»      + badge 1/14
    │   └── Button.knowledge-cat-btn          «Квесты»       + badge 4/6
    └── #knowledge-detail                 ← правая колонка, flex:1, column, overflow hidden
        ├── .knowledge-header             ← заголовок категории + описание
        ├── #knowledge-factions-container (один из 6)
        │   ├── .section-title «Фракции — 3 из 16»
        │   ├── Label.knowledge-empty «Вы ещё не встречали представителей фракций»
        │   └── ListView#knowledge-factions-list .item-list
        ├── #knowledge-npcs-container     (аналогично)
        ├── #knowledge-skills-combat-container
        ├── #knowledge-skills-social-container
        ├── #knowledge-recipes-container
        └── #knowledge-quests-container
```

- **Бейджи** — дети кнопок категории (Button может содержать VisualElement): `<ui:Label class="knowledge-cat-badge" text="3/16"/>`. Считать runtime: фракции — `Enum.GetValues(typeof(FactionId)).Length - 1` (минус None) / `KnownFactionIds.Count`; NPC — из `NpcAttitudeClientState`; навыки — `Resources.LoadAll<SkillNodeConfig>("Skills")` (26/4 — не хардкодить!); рецепты — `Resources.LoadAll<RecipeData>("Crafting/Recipes")`; квесты — из `QuestClientState`.
- **Квесты** в знании: один ListView, строки со state-цветом (Discovered=серый, Active=белый, Completed=зелёный, Failed=красный) — данные из `QuestClientState.CurrentSnapshot`, БЕЗ нового канала (Н7). Подсказка внизу: «Полный журнал — вкладка „Квесты“».
- **Детали фракции/репутации**: строка `.knowledge-row` = название (из `FactionDefinition`) + полоска/число репутации; классы `.knowledge-rep-positive/negative/zero` (цвета в USS, не inline).
- **Детали NPC**: имя из `NpcDefinition` (проверить runtime-адресуемость, Н12) + фракция + отношение (сейчас `npc-attitude-row` переиспользуем).
- **Детали навыка**: имя + `knowledgeUnlockDescription` для неизвестных («Как узнать: ...»), статус «Известен/Изучен» — классы `.knowledge-state-known/learned/unknown`; известные-неизученные — кликабельны → открыть `SkillTreeWindow`.
- **Empty-состояния** — обязательны для всех 6 категорий (сейчас reputation-tab показывает пустые списки — неудобно).

### 5.3. USS-скелет (добавляется в CharacterWindow.uss)

```css
/* === Knowledge tab === */
#knowledge-section { flex: 1 !important; flex-direction: row !important; min-height: 300px !important; }
.knowledge-categories { width: 150px !important; flex-shrink: 0 !important; flex-direction: column !important;
    margin-right: 6px !important; overflow: hidden !important; }
.knowledge-cat-btn { flex-shrink: 0 !important; flex-direction: row !important; justify-content: space-between !important;
    align-items: center !important; height: 24px !important; padding: 0 6px !important; margin-bottom: 2px !important;
    font-size: 10px !important; border-width: 1px !important; border-color: rgba(80,100,130,0.3) !important;
    background-color: rgba(30,40,60,0.3) !important; border-radius: 3px !important; }
.knowledge-cat-btn.active { border-color: rgb(255,220,130) !important; background-color: rgba(80,110,160,0.6) !important; }
.knowledge-cat-badge { font-size: 9px !important; color: rgb(160,180,200) !important; }
.knowledge-cat-btn.active .knowledge-cat-badge { color: rgb(255,220,130) !important; }
#knowledge-detail { flex: 1 !important; flex-shrink: 1 !important; min-width: 0 !important; min-height: 0 !important;
    flex-direction: column !important; overflow: hidden !important; }
.knowledge-header { flex-shrink: 0 !important; font-size: 11px !important; color: rgb(180,200,220) !important;
    -unity-font-style: bold !important; margin-bottom: 4px !important; }
.knowledge-empty { flex-shrink: 0 !important; font-size: 10px !important; color: rgb(120,140,160) !important;
    padding: 8px 4px !important; }
.knowledge-row { flex-direction: row !important; justify-content: space-between !important; align-items: center !important;
    padding: 2px 4px !important; border-bottom-width: 1px !important; border-bottom-color: rgba(80,100,130,0.1) !important; }
/* статусы — цвета только в USS */
.knowledge-state-known { color: rgb(160,180,200) !important; }
.knowledge-state-learned { color: rgb(100,180,100) !important; }
.knowledge-state-unknown { color: rgb(90,90,90) !important; }
.knowledge-state-hostile { color: rgb(220,90,80) !important; }
.knowledge-rep-positive { color: rgb(100,200,100) !important; }
.knowledge-rep-negative { color: rgb(220,90,80) !important; }
/* 6 контейнеров деталей: скрыты, активный — flex */
.knowledge-detail-container { display: none !important; flex: 1 !important; min-height: 0 !important;
    flex-direction: column !important; }
.knowledge-detail-container.active { display: flex !important; }
/* внутри — переиспользуем .item-list / .section-title / .unity-scroll-view .unity-scroller (уже в файле) */
```

### 5.4. C#-логика (CharacterWindow.cs + новый client state)

- Поля: `_knowledgeCatButtons: List<Button>`, `_knowledgeContainers: Dictionary<string, VisualElement>`, `_knowledgeBadges: Dictionary<string, Label>`.
- `InitKnowledgeTab()` — в `EnsureBuilt`-потоке (после CloneTree): Q кнопок/контейнеров, `clicked -= / +=`, стартовая категория «Фракции» (сохранить прежнее поведение по умолчанию).
- `SwitchKnowledgeCategory(string cat)` — `active`-классы у кнопок и контейнеров + вызов refresh нужной категории.
- `RefreshKnowledgeCategory*()` — 6 методов, каждый: наполнить кэш из своего client state + `ListView.itemsSource = ...` + `RefreshItems()` + переключить `.knowledge-empty` display + обновить бейдж.
- Подписки: lazy-паттерн в `Update()` (как у остальных табов): `if (_built && !_isKnowledgeSubscribed && KnowledgeClientState.Instance != null) { subscribe; _isKnowledgeSubscribed = true; }`; в `OnDisable` — `-=`.
- События: `SkillsClientState.OnSkillsUpdated` (knownSkillIds из расширенного снапшота), `RecipeKnowledgeClientState.OnRecipeKnowledgeReceived` (новый), `ReputationClientState.OnSnapshotReceived`/`NpcAttitudeClientState` (уже подписаны — просто добавить refresh знаний), `QuestClientState.OnSnapshotUpdated`.
- Сохранить прежние `RefreshReputationCache/RefreshNpcAttitudeCache` (фракции/NPC) — они переиспользуются категориями «Фракции» и «NPC» почти без изменений.

### 5.5. Что НЕ делаем в UI

- ❌ Не вставляем ListView внутрь ScrollView (двойной скролл) — детали = одна колонка с одним скроллом.
- ❌ Не создаём элементы программно в `EnsureBuilt` (гонка с UXML-clone — правило скилла) — все кнопки/контейнеры в UXML, Q по имени.
- ❌ Не хардкодим цвета в C# (`FindFactionFallback`, цветовые ветки репутации) — переезд на `FactionDefinition` + USS-классы.
- ❌ Не меняем `EnsureBuilt`-структуру и не переименовываем `tab-*` другие кнопки — только rename `tab-reputation`→`tab-knowledge` + `#reputation-section`→`#knowledge-section`.
- ❌ Не трогаем `UIManager`/Esc-логику — вкладка внутри окна, BUG-001 не регрессит.

---

## 6. План внедрения (скорректированный относительно 04)

### Фаза A — данные и сервер (persistence first)

| # | Шаг | Файлы | Примечание |
|---|---|---|---|
| A1 | `KnowledgeUnlockType` + поля в `SkillNodeConfig` | `Scripts/Skills/SkillNodeConfig.cs` | публичные поля (стиль файла) |
| A2 | `RecipeKnowledgeUnlockType` + поля в `RecipeData` | `Scripts/Crafting/RecipeData.cs` | приватные + свойства (стиль файла) |
| A3 | `SkillsSave` + `knownSkillIds` | `Scripts/Stats/Persistence/SkillsSave.cs` | обратная совместимость: null → пусто |
| A4 | `QuestSaveData` + `knownRecipes` | `Quests/Persistence/QuestSaveData.cs` | |
| A5 | `KnowledgeLossConfig` (SO + дефолтный ассет) | `Scripts/Knowledge/` + `Data/Knowledge/` | поля по §4.3 |
| A6 | `FactionDefinition` + `FactionCatalog` | `Scripts/Knowledge/` (или рядом с Factions) | замена `FindFactionFallback` |
| A7 | `SkillsWorld`: `_knownSkills`, `IsSkillKnown`, `UnlockSkillKnowledge`, `AutoOnSkillLearned`, расширение `BuildSaveData/LoadPlayer` | `Scripts/Skills/SkillsWorld.cs` | обход prerequisites по ссылкам конфигов |
| A8 | `SkillsSnapshotDto` + `knownSkillIds`; `SkillsServer.SendSnapshotToOwner` шлёт новое поле; `SkillsClientState` читает | `Scripts/Skills/Dto/`, `SkillsServer.cs`, `SkillsClientState.cs` | бродкаст после любого `UnlockSkillKnowledge` |
| A9 | `CraftingWorld` (static): `_knownRecipes`, `IsRecipeKnown`, `UnlockRecipeKnowledge`, `GetKnownRecipeIds`, `ApplyDeathRecipeLoss`, `BuildRecipeKnowledgeSave/LoadRecipeKnowledge`, чистка в `Shutdown` | `Scripts/Crafting/CraftingWorld.cs` | |
| A10 | `CraftingServer.StartCraftRpc`: серверный гейт `IsRecipeKnown` + `AllowedRecipes` | `Scripts/Crafting/CraftingServer.cs` | закрыть эксплойт |
| A11 | `RecipeKnowledgeDto` + `NetworkPlayer.ReceiveRecipeKnowledgeTargetRpc` + `RecipeKnowledgeClientState` (+ `CreateRecipeKnowledgeClientState` в NMC) | `Scripts/Crafting/Dto/`, `NetworkPlayer.cs`, `NetworkManagerController.cs` | auto-spawn паттерн |
| A12 | `QuestWorld`: `ApplyDeathKnowledgeLoss(clientId, config)` + реализация `QuestUnlockType.Recipe` в `ApplyQuestRewards` + `knownRecipes` в `BuildSaveData/LoadPlayer` | `Quests/Core/QuestWorld.cs` | вызвать `CraftingWorld`-мосты |
| A13 | Хук смерти в `PlayerTarget.TriggerDeathRespawn` (после успешного респавна) | `Scripts/Combat/Implementations/PlayerTarget.cs` | server-only, `QuestWorld.Instance != null`, SavePlayer + бродкаст |
| A14 | `DialogueActionType` + `UnlockSkillKnowledge/UnlockRecipe` (опционально, отдельным шагом) | `QuestServer.cs` | не обязательно для первого захода |

### Фаза B — UI (вкладка «Знания»)

| # | Шаг | Файлы |
|---|---|---|
| B1 | UXML: rename таба + `#knowledge-section` (категории + 6 контейнеров) | `UI/Resources/UI/CharacterWindow.uxml` |
| B2 | USS: `.knowledge-*` классы из §5.3 | `UI/Resources/UI/CharacterWindow.uss` |
| B3 | C#: `InitKnowledgeTab`, `SwitchKnowledgeCategory`, 6 refresh-методов, lazy-subscribe, бейджи, empty-состояния | `Scripts/UI/Client/CharacterWindow.cs` |
| B4 | Фильтр знаний в `SkillTreeWindow` + `SocialSkillTreeWindow` (скрыть/показать неизвестные, «Как узнать: ...») | `Scripts/Skills/UI/*.cs` |
| B5 | Фильтр знаний в `CraftingWindow.GetRecipeDisplayList` (+ серверный гейт уже в A10) | `Scripts/Crafting/CraftingWindow.cs` |
| B6 | Полировка: FactionDefinition в деталях, NpcDefinition-имена, квесты из `QuestClientState` | CharacterWindow.cs |

### Порядок верификации (после каждой фазы)

1. Compile: Unity Editor → Console → 0 errors (после любых скрипт-правок — `refresh_unity` + `read_console`).
2. Play Mode (Host): открыть CharacterWindow → вкладка «Знания» → категории переключаются, бейджи считаются, empty-состояния корректны.
3. Диалог с новым NPC → NPC появляется в «Знания»/NPC, его фракция — в «Знания»/Фракции.
4. Крафт: попытка крафта неизвестного рецепта через RPC (Debug-консоль) → отклонено; после получения знания → разрешено.
5. Смерть → часть знаний исчезает (лог `[QuestWorld] DeathKnowledgeLoss: factions=-N npcs=-M recipes=-K`), Neutral и защищённые на месте.
6. Рестарт сервера → знания загружаются из save (фракции/NPC/рецепты/навыки совпадают с докладов).

---

## 7. Открытые вопросы (нужно решение пользователя)

1. **FactionDefinition**: создаём новый SO-каталог (16 записей) или дополняем существующий фракционный конфиг? (рекомендую новый — Factions-слой чистый)
2. **Потеря при смерти**: применять ко всем смертям (PvE/PvP/падение) или с тумблером по причине? (рекомендую: все смерти игрока, конфиг `enabled`)
3. **«Знания» о навыках vs выученные**: при `UnlockSkillKnowledge` награда квеста/NPC должна ли автоматически давать сам навык, или только «видимость»? (рекомендую: только видимость; изучение — по-прежнему через XP/INT, как сейчас)
4. **Квесты в «Знаниях»**: показывать все состояния (Discovered/Active/Completed/Failed) или только «активные знания»? (рекомендую: все, с цветами состояний — данных хватает)
5. **Рецепты: источник по умолчанию** — `Blueprint` (текущее поведение станции) сохранить как default, а `QuestUnlockType.Recipe` сделать единственным «квестовым» путём? (рекомендую: да)
6. **NpcDefinition runtime**: проверить, что ассеты NPC адресуемы через `Resources`/каталог (иначе имена в «Знаниях» = fallback «Npc XXX»). Требует быстрой проверки в Unity (или решений по месту в B6).

---

## 8. Приложение — файлы, которые трогаем/создаём

### Изменяемые (существующие)

- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs`
- `Assets/_Project/Scripts/Skills/SkillsWorld.cs`
- `Assets/_Project/Scripts/Skills/SkillsClientState.cs`
- `Assets/_Project/Scripts/Skills/SkillsServer.cs`
- `Assets/_Project/Scripts/Skills/Dto/SkillsSnapshotDto.cs`
- `Assets/_Project/Scripts/Skills/UI/SkillTreeWindow.cs`
- `Assets/_Project/Scripts/Skills/UI/SocialSkillTreeWindow.cs`
- `Assets/_Project/Scripts/Crafting/RecipeData.cs`
- `Assets/_Project/Scripts/Crafting/CraftingWorld.cs`
- `Assets/_Project/Scripts/Crafting/CraftingServer.cs`
- `Assets/_Project/Scripts/Crafting/CraftingClientState.cs`
- `Assets/_Project/Scripts/Crafting/CraftingWindow.cs`
- `Assets/_Project/Scripts/Combat/Implementations/PlayerTarget.cs`
- `Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs` (если хук туда)
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (+1 TargetRpc)
- `Assets/_Project/Scripts/Network/NetworkManagerController.cs` (auto-spawn)
- `Assets/_Project/Scripts/Stats/Persistence/SkillsSave.cs`
- `Assets/_Project/Quests/Core/QuestWorld.cs`
- `Assets/_Project/Quests/Network/QuestServer.cs`
- `Assets/_Project/Quests/Persistence/QuestSaveData.cs`
- `Assets/_Project/Quests/Quests/QuestReward.cs` (реализация Recipe unlock)
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml`
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss`

### Новые

- `Assets/_Project/Scripts/Knowledge/KnowledgeLossConfig.cs` (+ `Assets/_Project/Data/Knowledge/KnowledgeLossConfig.asset`)
- `Assets/_Project/Scripts/Knowledge/FactionDefinition.cs` (+ `FactionCatalog.cs`, + ассеты в `Data/Factions/`)
- `Assets/_Project/Scripts/Crafting/Dto/RecipeKnowledgeDto.cs`
- `Assets/_Project/Scripts/Crafting/RecipeKnowledgeClientState.cs`

### Не трогаем

- `docs/gdd/*`, `docs/WORLD_LORE_BOOK.md`, `src/`, `.meta`/`.asmdef`, `BootstrapScene.unity`, `UIManager.cs`, v1-каналы репутации/NPC.

---

## 9. История изменений

| Дата | Сессия | Изменения |
|---|---|---|
| 2026-08-01 | Mavis ресерч | Создан документ: сверка с 04 (12 нестыковок), карта зацепок навыков/крафта/смерти, дизайн данных, UI-layout «Знаний», план A/B, открытые вопросы |
