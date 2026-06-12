# M19 — Инструкция для Content Writer'а: 3 базы CSV

> **Версия:** 2026-06-09 (M19-T19.3 финал)
> **Для кого:** контент-райтер, никогда не открывавший Unity
> **Что:** три CSV-файла (1 база, 2 опциональные), 1 импорт — готовая игра

---

## 0. Общая структура (3 файла)

```
Assets/_Project/Quests/Import/
├── mygame_quests.csv        ← ОБЯЗАТЕЛЬНО (создаёт квесты + NPC + связи)
├── mygame_npcs.csv          ← ОПЦИОНАЛЬНО (детали NPC: services, attitude, voice)
└── mygame_dialogs.csv       ← ОПЦИОНАЛЬНО (реплики NPC в диалогах)
```

**Все три файла можно класть рядом.** При импорте Unity сама найдёт `*_npcs.csv` и `*_dialogs.csv` по соседству.

**Кодировка:** UTF-8 with BOM (Excel сохраняет по умолчанию).
**Разделитель:** запятая `,`
**Многострочный текст:** обернуть в `"..."`, внутри удвоить кавычки `""`.
**Пустая ячейка** = используется default или fallback.

---

## 1. БАЗА 1 — `mygame_quests.csv` (ОБЯЗАТЕЛЬНО)

**Что создаёт:** все квесты, stages, objectives, rewards, NPC (с базовыми настройками), questOffers, questTurnIns.

**1 строка = 1 objective в квесте.** Multi-stage квест = несколько строк с одинаковым `questId` и разным `stageNum`.

### 1.1 Обязательные колонки (без них строка не импортируется)

| Колонка | Что писать | Пример |
|---------|-----------|--------|
| `questId` | Уникальный ID (латиница, без пробелов) | `q_002_0` |
| `displayName` | Название квеста (игрок видит в журнале) | `Мистер Фринли: Срочный заказ` |
| `stageNum` | Номер этапа: 0, 1, 2... | `0` |
| `objectiveType` | Тип цели (см. ниже) | `HaveItem` |
| `qty` | Сколько нужно собрать/поговорить | `3` |

### 1.2 Необязательные колонки (можно оставлять пустыми)

| Колонка | Что делает | Default / Fallback | Пример |
|---------|-----------|--------------------|--------|
| `description` | Описание квеста в журнале | пусто | `Найдите древний артефакт в руинах` |
| `faction` | Фракция квеста (11 enum'ов: GuildOfSuccess, Neutral, Pirates, Underground, SOL_Patrol, FreeTraders, GuildOfThoughts, GuildOfStrength, GuildOfCreation, GuildOfSecrets, Resistance) | `Neutral` | `GuildOfSuccess` |
| `oneShot` | Одноразовый квест (y/n) | `n` | `y` |
| `prereqQuest` | questId который надо завершить ДО этого | пусто (доступен сразу) | `q_001_0` |
| `stageId` | ID этапа (для nextStageId связки) | auto `stage_{N}` | `gather` |
| `stageDescription` | Описание этапа | пусто | `Добудьте руду в шахте` |
| `onEnterActions` | Действия при входе в этап | пусто | `AddNpcAttitude:mira_01:5` |
| `objectiveId` | ID цели | auto `obj_{stage}_{i}` | `gather_ore` |
| `itemName` | Название предмета (для `HaveItem`) | пусто | `Медная руда` |
| `npcId` | ID NPC (для `TalkToNpc`) | пусто | `npc_002` |
| `npcDisplayName` | Отображаемое имя NPC при auto-create | `npcId` | `Мистер Фринли` |
| `npcFaction` | Фракция NPC при auto-create | `Neutral` | `GuildOfSuccess` |
| `onCompleteActions` | Действия при завершении этапа | пусто | `GiveCredits::200;AddReputation::25:GuildOfSuccess` |
| `rewardCR` | Награда кредитами | `0` | `200` |
| `rewardRep` | Награда репутацией (FactionId:value) | пусто | `GuildOfSuccess:25` |
| `rewardItem` | Награда предметом (itemName:count;...) | пусто | `Port Access Token:1;Steel Cable 6mm:2` |

### 1.3 Типы целей (objectiveType)

| Тип | Когда использовать | Обязательные колонки | Пример строки |
|-----|-------------------|----------------------|---------------|
| `HaveItem` | Собрать предмет | `itemName`, `qty` | `HaveItem,gather_ore,Медная руда,npc_002,3` |
| `TalkToNpc` | Поговорить с NPC | `npcId`, `qty=1` | `TalkToNpc,talk_mira,,mira_01,1` |
| `StandOnTrigger` | Дойти до места | (другая логика) | `StandOnTrigger,at_zone,World0_0_ZoneA,,,1` |
| `CompleteObjective` | Системное | обычно не используется writer'ом | — |
| `KilledEntity` | Убить | другая система | — |
| `EventDriven` | Серверное событие | (другая система) | — |
| `CargoHasItem` | Груз корабля | (другая система) | — |

### 1.4 Формат actions (`onEnterActions`, `onCompleteActions`)

**Формат одной action:** `Type:stringParam:intParam:factionParam`
**Несколько actions:** разделяются `;`

| Action | Формат | Пример |
|--------|--------|--------|
| `GiveCredits` | `GiveCredits::amount` | `GiveCredits::200` |
| `AddReputation` | `AddReputation:faction:amount` | `AddReputation::25:GuildOfSuccess` |
| `AddNpcAttitude` | `AddNpcAttitude:npcId:amount` | `AddNpcAttitude:mira_01:5` |
| `GiveItem` | `GiveItem:itemName:count:type` | `GiveItem:TestStageItem:1:Resources` |
| `TakeItem` | `TakeItem:itemName:count:type` | `TakeItem:Medvedka:1:Resources` |
| `OfferQuest` | `OfferQuest:questId` | `OfferQuest:q_002_1` |
| `AcceptQuest` | `AcceptQuest:questId` | `AcceptQuest:q_002_0` |
| `CompleteObjective` | `CompleteObjective:objId` | `CompleteObjective:gather_ore` |
| `DiscoverQuest` | `DiscoverQuest:questId` | `DiscoverQuest:q_005_0` |

**Множественные actions через `;`:**
```
GiveCredits::200;AddReputation::25:GuildOfSuccess;AddNpcAttitude:mira_01:5
```

### 1.5 Как авто-создаются NPC

Если `npcId` есть в строке, но `NpcDefinition` asset не существует:
- Создаётся `NpcDefinition.asset` автоматически
- `displayName` = значение `npcDisplayName` ИЛИ `npcId` (fallback)
- `faction` = значение `npcFaction` ИЛИ `Neutral` (fallback)
- `questOffers[]` = все questId где этот NPC в stage 0
- `questTurnIns[]` = все questId где этот NPC в последнем stage

**Пример строки с авто-NPC:**
```csv
my_quest,Мой квест,0,HaveItem,obj_1,Медная руда,npc_mike,Михаил,GuildOfSuccess,3,GiveCredits::100,100,GuildOfSuccess:10
```

Это создаст:
- Квест `my_quest` "Мой квест"
- NPC `npc_mike` (если не существует) с displayName="Михаил", faction=GuildOfSuccess
- Привяжет questOffer к этому NPC

### 1.6 Полный пример `mygame_quests.csv`

```csv
questId,displayName,description,faction,oneShot,prereqQuest,stageNum,stageId,stageDescription,onEnterActions,objectiveType,objectiveId,itemName,npcId,npcDisplayName,npcFaction,qty,onCompleteActions,rewardCR,rewardRep,rewardItem
q_002_0,Мистер Фринли: Срочный заказ,Срочный заказ от Гильдии Успеха,GuildOfSuccess,y,,0,talk_0_0,Встретьтесь с Фринли,,TalkToNpc,obj_q_002_0_s0,,npc_002,Мистер Фринли,GuildOfSuccess,1,,,,
q_002_0,Мистер Фринли: Срочный заказ,Срочный заказ от Гильдии Успеха,GuildOfSuccess,y,,1,gather_0_1,Найдите 4 письма,,HaveItem,obj_q_002_0_s1,Letter of Thanks,npc_002,Мистер Фринли,GuildOfSuccess,4,,,,
q_002_0,Мистер Фринли: Срочный заказ,Срочный заказ от Гильдии Успеха,GuildOfSuccess,y,,2,gather_0_2,Найдите 5 серебряных портсигаров,,HaveItem,obj_q_002_0_s2,Silver Cigarette Case,npc_002,Мистер Фринли,GuildOfSuccess,5,,,,
q_002_0,Мистер Фринли: Срочный заказ,Срочный заказ от Гильдии Успеха,GuildOfSuccess,y,,9,talk_0_9,Финальная встреча с Фринли,,TalkToNpc,obj_q_002_0_s9,,npc_002,Мистер Фринли,GuildOfSuccess,1,GiveCredits::75;AddReputation::50:GuildOfSuccess;GiveItem:Port Access Token::1,75,GuildOfSuccess:50,Port Access Token:1
q_002_1,Помощь Мистеру Фринли,Помогите Фринли,GuildOfSuccess,,,0,talk_1_0,Встретьтесь с Фринли,,TalkToNpc,obj_q_002_1_s0,,npc_002,Мистер Фринли,GuildOfSuccess,1,,,,
q_002_1,Помощь Мистеру Фринли,Помогите Фринли,GuildOfSuccess,,,1,gather_1_1,Соберите 3 кольца,,HaveItem,obj_q_002_1_s1,Guild Signet Ring,npc_002,Мистер Фринли,GuildOfSuccess,3,,,,
q_002_1,Помощь Мистеру Фринли,Помогите Фринли,GuildOfSuccess,,,4,talk_1_4,Финальная встреча,,TalkToNpc,obj_q_002_1_s4,,npc_002,Мистер Фринли,GuildOfSuccess,1,GiveCredits::75;GiveItem:SOL Memory Cartridge::1,75,,SOL Memory Cartridge:1
```

**Этот файл создаёт 2 квеста (q_002_0 и q_002_1), 1 NPC (npc_002 = "Мистер Фринли").**

### 1.7 Что подтвердится после импорта

После импорта в Inspector NPC `npc_002`:
- `displayName = "Мистер Фринли"`
- `faction = GuildOfSuccess`
- `questOffers = ["q_002_0", "q_002_1"]` (auto)
- `questTurnIns = ["q_002_0", "q_002_1"]` (auto, т.к. последний stage = TalkToNpc с npc_002)
- `defaultDialogTree` — если есть `npc_002_default.asset`

---

## 2. БАЗА 2 — `mygame_npcs.csv` (ОПЦИОНАЛЬНО)

**Что создаёт:** доп. настройки NPC (services, attitude, greeting, voice, radius).
**Когда использовать:** если NPC должен быть торговцем/ремонтником/иметь особое приветствие.

**1 строка = 1 NPC.** Только `npcId` обязателен, остальное опционально.

### 2.1 Колонки

| Колонка | Что делает | Default | Пример |
|---------|-----------|---------|--------|
| `npcId` | ID NPC (должен уже существовать — создаётся из quests.csv) | — | `npc_002` |
| `services` | Битовая маска (см. ниже) | `None` | `Trade;Repair` |
| `attitudeLinks` | Cross-faction (см. ниже) | пусто | `Pirates:-15;Underground:5` |
| `attitudeMin` | Минимальное отношение (-100..200) | `-100` | `0` |
| `attitudeMax` | Максимальное отношение (-100..200) | `200` | `100` |
| `greetingText` | Текст при подходе | `Greetings, traveler.` | `Приветствую, путник!` |
| `voicePrefix` | Префикс для voice lines | пусто | `npc_002_` |
| `interactionRadius` | Радиус interact в метрах | `3.0` | `2.5` |
| `showGreeting` | Показывать ли greeting (y/n) | `y` | `y` |

### 2.2 Services (services)

Битовая маска, несколько значений через `;`:

| Service | Что делает |
|---------|-----------|
| `Trade` | NPC — торговец (можно покупать/продавать) |
| `Repair` | NPC — ремонтник (чинит корабль/модули) |
| `Refuel` | NPC — заправщик |
| `Restock` | NPC — пополняет стандартный ассортимент |
| `Banking` | (future) банкир |
| `Healing` | (future) лекарь |

**Пример:** `Trade;Repair` (торговец и ремонтник в одном лице).

### 2.3 Attitude Links (attitudeLinks)

**Формат:** `FactionId:delta;FactionId:delta`

| Поле | Что |
|------|-----|
| FactionId | одна из 11 фракций |
| delta | целое число (отрицательное = ухудшает, положительное = улучшает) |

**Пример:** `Pirates:-15;Underground:5`
- Прокачка отношений с этим NPC улучшает репутацию с Underground на +5
- И ухудшает репутацию с Pirates на -15

### 2.4 Полный пример `mygame_npcs.csv`

```csv
npcId,services,attitudeLinks,attitudeMin,attitudeMax,greetingText,voicePrefix,interactionRadius,showGreeting
npc_002,Trade,Pirates:-15,0,100,"Приветствую, путник! Что привело тебя в наш квартал?",npc_002_,3.0,y
npc_005,Repair,Neutral:5,-50,200,"Здравствуй. Чем могу помочь?",npc_005_,2.5,y
npc_010,Trade;Repair,GuildOfSuccess:-10;Underground:5,0,200,"Приветствую. Мистер Эстик к вашим услугам.",npc_010_,3.0,y
npc_019,Repair;Refuel,Underground:10;Pirates:-20,0,150,"Чем могу помочь, путник?",npc_019_,2.5,y
```

**Файл создаёт НЕ новых NPC**, только **обновляет** существующих. Если NPC с `npcId` не существует, строка **пропускается** с warning.

---

## 3. БАЗА 3 — `mygame_dialogs.csv` (ОПЦИОНАЛЬНО)

**Что создаёт:** кастомные диалоги (DialogTree assets). Auto-link к NPC если treeId = `{npcId}_default`.

**1 строка = 1 edge (переход между нодами).** Узлы создаются автоматически.

### 3.1 Колонки

| Колонка | Обязательно | Что | Пример |
|---------|-------------|-----|--------|
| `treeId` | да | ID диалога (для auto-link к NPC) | `mira_default` или `npc_002_default` |
| `fromNodeId` | да | ID ноды-источника | `greeting` |
| `fromText` | да | Текст реплики (что говорит NPC) | `Привет, путник!` |
| `fromSpeaker` | нет | `Npc: npcId` / `Player` / `Narrator` | `Npc: npc_002` |
| `edgeLabel` | да | Текст кнопки выбора | `У меня есть работа` |
| `toNodeId` | да | ID ноды-цели (пусто = EndConversation) | `offer_quest` |
| `hideIfUnavailable` | нет | y/n (default y) | `y` |
| `conditionType` | нет | Условие (см. ниже) | `QuestCompleted` |
| `conditionStringParam` | нет | questId / itemId / npcId | `q_001_0` |
| `conditionIntParam` | нет | quantity / value | `3` |
| `conditionFactionParam` | нет | FactionId | `GuildOfSuccess` |
| `actionType` | нет | Действие при выборе (см. ниже) | `OfferQuest` |
| `actionStringParam` | нет | questId / itemId / npcId | `q_002_0` |
| `actionIntParam` | нет | amount / delta | `1` |
| `actionFactionParam` | нет | FactionId (для AddReputation) | `GuildOfSuccess` |

### 3.2 Условия (conditionType) — полный список

| Условие | Что проверяет | Параметры |
|---------|---------------|-----------|
| `HasItem` | Есть предмет | `conditionStringParam=itemId`, `conditionIntParam=qty` |
| `QuestCompleted` | Квест завершён | `conditionStringParam=questId` |
| `QuestActive` | Квест активен | `conditionStringParam=questId` |
| `QuestDiscovered` | Квест обнаружен | `conditionStringParam=questId` |
| `QuestStateEquals` | Квест в конкретном состоянии | `conditionStringParam=questId` |
| `ReputationAtLeast` | Репутация >= value | `conditionFactionParam=FactionId`, `conditionIntParam=value` |
| `ReputationAtMost` | Репутация <= value | `conditionFactionParam=FactionId`, `conditionIntParam=value` |
| `NpcAttitudeAtLeast` | Отношение NPC >= value | `conditionStringParam=npcId`, `conditionIntParam=value` |
| `FlagIsSet` | Global flag = true | `conditionStringParam=flagId` |
| `WasNodeVisited` | Этот dialog node уже был показан | — |
| `TimeOfDay` | Время суток | `conditionStringParam=day/night` |

### 3.3 Действия (actionType) — полный список

| Действие | Что делает | Параметры |
|----------|-----------|-----------|
| `OfferQuest` | Предложить квест | `actionStringParam=questId` |
| `AcceptQuest` | Сразу принять | `actionStringParam=questId` |
| `CompleteObjective` | Отметить objective | `actionStringParam=questId`, `actionStringParam2=objectiveId` |
| `FailQuest` | Провалить квест | `actionStringParam=questId` |
| `DiscoverQuest` | Добавить в discovered | `actionStringParam=questId` |
| `GiveItem` | Выдать предмет | `actionStringParam=itemName`, `actionIntParam=qty` |
| `TakeItem` | Забрать предмет | `actionStringParam=itemName`, `actionIntParam=qty` |
| `GiveCargoItem` | Дать груз кораблю | `actionStringParam=itemName`, `actionIntParam=qty` |
| `TakeCargoItem` | Забрать груз | `actionStringParam=itemName`, `actionIntParam=qty` |
| `GiveCredits` | Дать кредиты | `actionIntParam=amount` |
| `AddReputation` | Изменить репутацию | `actionFactionParam=FactionId`, `actionIntParam=delta` |
| `AddNpcAttitude` | Изменить отношение NPC | `actionStringParam=npcId`, `actionIntParam=delta` |
| `OpenMarket` | Открыть магазин | `actionStringParam=zoneId` |
| `OpenService` | Открыть сервис | — |
| `SetFlag` | Поставить global flag | `actionStringParam=flagId` |
| `SwitchDialogTree` | Переключить на другой dialog | `actionStringParam=treeId` |
| `EndConversation` | Завершить разговор | — |

### 3.4 Принцип auto-link к NPC

Если `treeId` соответствует формату `{npcId}_default`, после импорта:
- Создаётся `DialogTree.asset` (например `mira_default.asset`)
- NPC с `npcId="mira"` получает `defaultDialogTree = mira_default`

**Пример:** `treeId = npc_002_default` → link к NPC `npc_002`.

### 3.5 Полный пример `mygame_dialogs.csv`

```csv
treeId,fromNodeId,fromText,fromSpeaker,edgeLabel,toNodeId,hideIfUnavailable,conditionType,conditionStringParam,conditionIntParam,conditionFactionParam,actionType,actionStringParam,actionIntParam,actionFactionParam
npc_002_default,greeting,Приветствую! Я Мистер Фринли. Что привело тебя?,Npc: npc_002,У тебя есть работа?,quest_offer,FALSE,,,,,OfferQuest,q_002_0,,
npc_002_default,greeting,Приветствую! Я Мистер Фринли. Что привело тебя?,Npc: npc_002,Просто осматриваюсь,end,FALSE,,,,,,
npc_002_default,quest_offer,У меня срочный заказ от Гильдии Успеха. 4 письма и 5 портсигаров.,Npc: npc_002,Согласен!,greeting,FALSE,,,,,AcceptQuest,q_002_0,,
npc_002_default,quest_offer,У меня срочный заказ от Гильдии Успеха. 4 письма и 5 портсигаров.,Npc: npc_002,Слишком опасно,reject,FALSE,,,,,,
npc_002_default,end,,,,Player,Закончить разговор,,FALSE,,,,EndConversation,,
```

**Что создаётся:**
- 1 DialogTree asset `npc_002_default.asset`
- 4 ноды: `greeting`, `quest_offer`, `end` + сирота `reject`
- 4 edges (greeting→quest_offer, greeting→end, quest_offer→greeting, end→...)
- NPC `npc_002.defaultDialogTree = npc_002_default`

### 3.6 Правила для текста с запятыми

**Запятая** в тексте → обернуть в кавычки:
```
greeting,"Привет, путник! Как дела?",...
```

**Кавычки** внутри текста → удвоить:
```
greeting,"Он сказал: ""Привет"" и ушёл",...
```

**Многострочный** текст — обернуть в кавычки, `\n` работает.

---

## 4. ПОЛНЫЙ WORKFLOW (writer's view)

### Шаг 1: Создать `mygame_quests.csv`
- Excel/Google Sheets: 21 колонка (см. §1.1, §1.2)
- Заполнить: questId, displayName, stages, objectives
- Сохранить как CSV (UTF-8) → `Assets/_Project/Quests/Import/mygame_quests.csv`

### Шаг 2 (опционально): Создать `mygame_npcs.csv`
- Excel/Google Sheets: 9 колонок
- Заполнить npcId + желаемые services/attitude/greeting
- Сохранить как CSV → `Assets/_Project/Quests/Import/mygame_npcs.csv`

### Шаг 3 (опционально): Создать `mygame_dialogs.csv`
- Excel/Google Sheets: 15 колонок
- Заполнить treeId + edges
- Сохранить как CSV → `Assets/_Project/Quests/Import/mygame_dialogs.csv`

### Шаг 4: Импорт в Unity
- Открыть Unity Editor
- **Tools → ProjectC → Quests → CSV Import/Export**
- **Browse...** → выбрать `mygame_quests.csv`
- (опц.) Поле `npcs.csv` и `dialogs.csv` — auto-fill (если файлы рядом)
- Галочки: ☐ Import Quests, ☑ Auto-create missing NPCs, ☑ Auto-create Dialogs
- **Preview** → проверка ошибок
- **▶ Import** → диалог с результатом
- Все quest'ы, NPC, dialogs — созданы/обновлены

### Шаг 5: Проверить
- **Project window:** `Assets/_Project/Quests/Data/Quests/`, `Npcs/`, `Dialogs/`
- **Inspector** на любой NPC: displayName, faction, questOffers[], questTurnIns[], services, attitude
- **Play Mode:** E → NPC → диалог → принять квест → выполнить → reward

---

## 5. ШПАРГАЛКА (для быстрого старта)

### Минимальный квест (1 строка, без NPC):

```csv
questId,displayName,stageNum,objectiveType,itemName,qty
my_first_quest,Мой первый квест,0,HaveItem,Медная руда,3
```

### Квест с NPC + reward (4 строки):

```csv
questId,displayName,faction,stageNum,objectiveType,itemName,npcId,npcDisplayName,qty,rewardCR,rewardRep
q_my,Помоги мне,GuildOfSuccess,0,HaveItem,Медная руда,npc_my,Мой Друг,3,100,GuildOfSuccess:10
q_my,Помоги мне,GuildOfSuccess,1,HaveItem,Серебряный слиток,npc_my,Мой Друг,1,0,
q_my,Помоги мне,GuildOfSuccess,2,HaveItem,Золотой слиток,npc_my,Мой Друг,1,0,
q_my,Помоги мне,GuildOfSuccess,3,TalkToNpc,,npc_my,Мой Друг,1,0,
```

### NPC-торговец (npcs.csv):

```csv
npcId,services,greetingText
npc_my,Trade;Repair,"Добро пожаловать в мой магазин!"
```

### Диалог для NPC (dialogs.csv, минимальный):

```csv
treeId,fromNodeId,fromText,fromSpeaker,edgeLabel,toNodeId,actionType,actionStringParam
npc_my_default,greeting,Привет, чем могу помочь?,Npc: npc_my,У тебя есть работа?,quest_offer,OfferQuest,q_my
npc_my_default,greeting,Привет, чем могу помочь?,Npc: npc_my,Пока,end,EndConversation,
```

---

## 6. ЧАСТЫЕ ОШИБКИ

| Ошибка | Симптом | Решение |
|--------|---------|--------|
| Пустой `stageId` при multi-stage | Квест не соединяется правильно | Пишите `stageId` явно (`gather`, `deliver`) |
| `faction=Гильдия Успеха` (кириллица) | Warning: unknown faction | Только латиница: `GuildOfSuccess` |
| Запятая в `displayName` без кавычек | CSV парсится криво | Обернуть: `"Собрать 3, или больше"` |
| `rewardRep=GuildOfSuccess:25` без `;` | Только последняя строка применяется | Reward в последней строке квеста |
| `prereqQuest=quest_doesnt_exist` | Warning: unknown quest | Убедиться что questId существует в этом же CSV |
| `objectiveType=KillNPC` | Warning: unknown type | Используй `KilledEntity` (case-sensitive!) |
| `npcId=mira 01` (с пробелом) | NPC не создастся | Только латиница + подчёркивание |
| `qty=-1` или `qty=0` | Error: invalid quantity | Только `qty > 0` |
| Multi-stage `stageNum=0,2,3` (пропущен 1) | Warning: stage numbers not sequential | Пронумеруй `0,1,2,3` без пропусков |
| `treeId=mira` (без `_default`) | Не привяжется к NPC автоматически | Имя: `{npcId}_default` или `npc_002_default` |

---

## 7. РЕЗЮМЕ (что каждая база делает)

| База | Что создаёт | Что заполняет | Что не делает |
|------|------------|---------------|---------------|
| `*_quests.csv` | **Квесты** + **NPC (базово)** + **связи** | displayName, faction, questOffers, questTurnIns, quests, stages, objectives, rewards | Не заполняет: services, attitude, greeting, voice, dialogs |
| `*_npcs.csv` | **Обновляет NPC** | services, attitudeLinks, attitudeMin/Max, greetingText, voicePrefix, radius | Не создаёт NPC (только обновляет существующих) |
| `*_dialogs.csv` | **Создаёт DialogTree** + **link к NPC** | node text, edges, conditions, actions | Не заменяет existing dialogs (создаёт новые) |

**Главный файл — `*_quests.csv`.** Без него ничего не создастся.
**Остальные два — для тюнинга.** Можно импортировать только quests, игра будет работать.
