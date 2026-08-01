# Система Знаний (Knowledge System) — Анализ и План Интеграции

> **Статус:** Анализ завершён. План готов к реализации.
> **Дата:** 2026-07-17
> **Затронутые подсистемы:** Quests, Reputation, Persistence, CharacterWindow UI

---

## 1. Текущая Архитектура (Audit)

### 1.1. Server-Side: QuestWorld

`Assets/_Project/Quests/Core/QuestWorld.cs` — серверный singleton (T-Q05). Ключевые структуры, релевантные для Knowledge:

| Структура | Тип | Описание |
|---|---|---|
| `_reputation` | `Dictionary<(ulong, FactionId), int>` | Репутация игрока с фракцией |
| `_npcAttitude` | `Dictionary<(ulong, string), int>` | Персональное отношение с NPC |
| `_npcTalkedTo` | `Dictionary<ulong, HashSet<string>>` | **Уже существует!** Сет NPC, с которыми игрок говорил |
| `_worldFlags` | `Dictionary<(ulong, string), bool>` | Мировые флаги |
| `_eventsOccurred` | `Dictionary<ulong, HashSet<string>>` | Произошедшие события |

### 1.2. Связь NPC ↔ Faction

`NpcDefinition.faction` (тип `FactionId`) — каждый NPC привязан к фракции.  
`FactionDefinition.factionId` (тип `FactionId`) — идентификатор фракции.

Всего: **15 фракций** (FactionId: None=0, GuildOfThoughts=1, ..., Villagers=15).  
Всего: **~100+ NPC** (npc_002..npc_106, Mira).

### 1.3. Точка входа: "поговорил с NPC"

`QuestWorld.MarkNpcTalked(clientId, npcId)` вызывается в двух местах `QuestServer`:
1. `RequestOpenDialogueRpc` — игрок нажал E на NPC (строка 506)
2. `FireDialogAction` — при обработке диалогового события (строка 850)

**Важно:** `MarkNpcTalked` уже вызывает `SavePlayer(clientId)` — данные сразу персистятся.

### 1.4. Persistence (QuestSaveData)

`Assets/_Project/Quests/Persistence/QuestSaveData.cs` — POCO для JSON-сериализации:

```csharp
public class QuestSaveData {
    public int version = 1;
    public List<QuestSaveEntry> quests;
    public List<FactionRepSaveEntry> reputation;
    public List<NpcAttitudeSaveEntry> npcAttitude;
    public List<StringSetSaveEntry> stringSets; // включает "npcTalkedTo"
}
```

Сет `npcTalkedTo` уже сохраняется в `stringSets` как `StringSetSaveEntry { setName = "npcTalkedTo", values = [...] }`.

### 1.5. Client-Side: Reputation → CharacterWindow

Цепочка:  
`QuestWorld._reputation` → `ReputationSnapshotDto` (NetworkSerializer) → `NetworkPlayer.ReceiveReputationSnapshotTargetRpc` → `ReputationClientState.OnReputationUpdated` → `CharacterWindow.RefreshReputationCache()`

**Текущее поведение CharacterWindow (вкладка "Репутация"):**
- `RefreshReputationCache()` (строка 959) показывает **ВСЕ фракции** — либо из снапшота, либо `FactionFallback` (5 фракций GDD-23 как placeholder)
- Фильтрация **отсутствует**

Аналогично для NPC Attitude: `RefreshNpcAttitudeCache()` показывает всех NPC из снапшота без фильтрации.

---

## 2. Что Нужно Реализовать

### 2.1. Концепт

**Knowledge** — это знание игрока о существовании фракции/NPC. Пока knowledge нет — игрок не видит эту фракцию/NPC в UI (CharacterWindow → Репутация).

**Правило разблокировки (v1):**
- Поговорил с NPC фракции X → открыл knowledge об этом NPC **и** о фракции X.
- Автоматически: фракция `Neutral` (FactionId=11) известна всем с начала игры (нужно для NPC без явной фракции).
- В будущем: knowledge может открываться через квесты, предметы (книги), world events и т.д.

### 2.2. Что УЖЕ Есть (не нужно делать заново)

| Компонент | Статус |
|---|---|
| Трекинг "поговорил с NPC" (`_npcTalkedTo`) | ✅ Есть в QuestWorld |
| Персистенция npcTalkedTo в сейв | ✅ Есть в QuestSaveData.stringSets |
| Связь NPC → Faction (`NpcDefinition.faction`) | ✅ Есть |
| Вызов MarkNpcTalked при диалоге | ✅ Есть в QuestServer |
| Отправка reputation на клиент | ✅ Есть (ReputationSnapshotDto) |
| Отображение reputation в CharacterWindow | ✅ Есть |

### 2.3. Что Нужно Добавить

| # | Компонент | Описание |
|---|---|---|
| 1 | `_knownFactions` в QuestWorld | `Dictionary<ulong, HashSet<FactionId>>` — какие фракции известны игроку |
| 2 | `_knownNpcs` в QuestWorld | `Dictionary<ulong, HashSet<string>>` — какие NPC известны (можно реиспользовать `_npcTalkedTo`, но семантически это Knowledge, а не TalkedTo — в будущем могут разойтись) |
| 3 | Авто-разблокировка в `MarkNpcTalked` | При talk с NPC → auto-add npc.faction в _knownFactions + npc.npcId в _knownNpcs |
| 4 | Knowledge в `QuestSaveData` | Новые поля: `knownFactions` (List<int>), `knownNpcs` (List<string>) |
| 5 | Knowledge в `BuildSaveData` / `LoadPlayer` | Сериализация/десериализация knowledge |
| 6 | Knowledge в DTO | Новый `KnowledgeSnapshotDto` ИЛИ расширение `ReputationSnapshotDto` полем `knownFactionIds` |
| 7 | `KnowledgeClientState` (или расширение `ReputationClientState`) | Клиентский стейт для known factions/NPCs |
| 8 | Фильтрация в `CharacterWindow` | `RefreshReputationCache()` — показывать только фракции из knownFactions |
| 9 | Фильтрация NPC Attitude | `RefreshNpcAttitudeCache()` — только NPC из knownNpcs |
| 10 | Инициализация: Neutral всегда known | При `LoadPlayer` / первом входе — `Neutral` (FactionId=11) auto-known |

---

## 3. План Интеграции (Пошаговый)

### Шаг 1: Расширить QuestSaveData (Persistence)

**Файл:** `Assets/_Project/Quests/Persistence/QuestSaveData.cs`

Добавить поля:
```csharp
public List<int> knownFactions = new List<int>();   // FactionId как int
public List<string> knownNpcs = new List<string>();  // npcId как string
```

**Риск:** низкий. Новые поля — обратная совместимость: старые сейвы загрузятся с пустыми списками, Knowledge разблокируется через MarkNpcTalked при следующем диалоге.

### Шаг 2: Добавить _knownFactions / _knownNpcs в QuestWorld

**Файл:** `Assets/_Project/Quests/Core/QuestWorld.cs`

```csharp
private readonly Dictionary<ulong, HashSet<FactionId>> _knownFactions = new();
private readonly Dictionary<ulong, HashSet<string>> _knownNpcs = new(); // или реиспользовать _npcTalkedTo
```

Добавить методы:
```csharp
public bool IsFactionKnown(ulong clientId, FactionId faction)
public bool IsNpcKnown(ulong clientId, string npcId)
public void UnlockFactionKnowledge(ulong clientId, FactionId faction)
public void UnlockNpcKnowledge(ulong clientId, string npcId)
```

### Шаг 3: Модифицировать MarkNpcTalked → авто-unlock фракции

В `QuestWorld.MarkNpcTalked` добавить:
```csharp
public void MarkNpcTalked(ulong clientId, string npcId)
{
    // ... существующий код ...
    
    // NEW: Knowledge unlock
    if (Database != null)
    {
        var npcDef = Database.GetNpc(npcId);
        if (npcDef != null && npcDef.faction != FactionId.None)
        {
            UnlockFactionKnowledge(clientId, npcDef.faction);
        }
    }
    UnlockNpcKnowledge(clientId, npcId);
}
```

### Шаг 4: Интегрировать Knowledge в Save/Load

**BuildSaveData:** добавить `knownFactions` и `knownNpcs` в `QuestSaveData`.  
**LoadPlayer:** восстановить `_knownFactions` и `_knownNpcs` из сейва.  
**Инициализация:** при первом входе (новый сейв) — auto-unlock `FactionId.Neutral`.

### Шаг 5: Создать KnowledgeSnapshotDto

**Новый файл:** `Assets/_Project/Quests/Dto/KnowledgeSnapshotDto.cs`

```csharp
public struct KnowledgeSnapshotDto : INetworkSerializable
{
    public byte[] knownFactionIds;  // FactionId как byte
    public string[] knownNpcIds;
    // NetworkSerialize...
}
```

Либо — более простой путь: добавить `public byte[] knownFactionIds` прямо в `ReputationSnapshotDto` и `public string[] knownNpcIds` в `NpcAttitudeSnapshotDto`. Это минимизирует количество новых файлов и RPC.

**Рекомендация:** расширить существующие DTO — меньше точек отказа.

### Шаг 6: Создать/Расширить KnowledgeClientState

**Вариант A (рекомендуемый):** расширить `ReputationClientState`:
- Добавить `public HashSet<byte> KnownFactionIds`
- Добавить `public HashSet<string> KnownNpcIds`
- Заполнять в `OnReputationSnapshotReceived` / новом обработчике

**Вариант B:** отдельный `KnowledgeClientState` — чище архитектурно, но больше кода.

**Рекомендация:** Вариант A (минимальные изменения).

### Шаг 7: Server → Client проброс Knowledge

В `QuestServer.SendReputationSnapshotToClient`:
- При построении `ReputationSnapshotDto` включить `knownFactionIds` из `QuestWorld._knownFactions[clientId]`

В `QuestServer.SendNpcAttitudeSnapshotToClient`:
- Включить `knownNpcIds` из `QuestWorld._knownNpcs[clientId]`

### Шаг 8: Фильтрация в CharacterWindow

**Файл:** `Assets/_Project/UI/Client/CharacterWindow.cs`

В `RefreshReputationCache()`:
```csharp
var knownFactions = ReputationClientState.Instance?.KnownFactionIds;
// Фильтровать _reputationCache: только factionId in knownFactions
// + всегда показывать Neutral
```

В `RefreshNpcAttitudeCache()`:
```csharp
var knownNpcs = ReputationClientState.Instance?.KnownNpcIds;
// Фильтровать: только npcId in knownNpcs
```

### Шаг 9: Тестирование

1. **Новый персонаж** → CharacterWindow → Репутация: только `Neutral` (или пусто, если Neutral не added).
2. **Поговорить с NPC фракции FreeTraders** → Knowledge открывается → Репутация: видно FreeTraders.
3. **Сейв/Лоад** → Knowledge сохраняется.
4. **Поговорить с NPC фракции GuildOfSuccess** → Knowledge открывается → видно обе фракции.

---

## 4. Оценка Рисков

| Риск | Вероятность | Влияние | Митигация |
|---|---|---|---|
| Слом обратной совместимости сейвов | Низкая | Высокое | Новые поля в QuestSaveData — старые сейвы загрузятся с пустыми списками |
| Нарушение работы репутации | Низкая | Высокое | Фильтрация только в UI (CharacterWindow), серверная логика не меняется |
| Пропущенные NPC без фракции | Средняя | Низкое | Auto-unlock Neutral для NPC с faction=None |
| Перфоманс (лишние данные в DTO) | Низкая | Низкое | byte[] для factionIds — максимум 16 байт |

---

## 5. Что НЕ Трогаем

- ✅ **QuestWorld.ModifyReputation** — без изменений
- ✅ **QuestWorld.ModifyNpcAttitude** — без изменений
- ✅ **Диалоговая система** — `MarkNpcTalked` уже вызывается, мы только добавляем side-effect
- ✅ **QuestServer** — минимальные изменения (добавить knownFactions в снапшоты)
- ✅ **Контракты, квесты, инвентарь** — не затрагиваются

---

## 6. Файлы, Которые Будут Изменены

| Файл | Тип изменений |
|---|---|
| `Assets/_Project/Quests/Persistence/QuestSaveData.cs` | +2 поля (knownFactions, knownNpcs) |
| `Assets/_Project/Quests/Core/QuestWorld.cs` | +2 Dictionary, +4 метода, модификация MarkNpcTalked, BuildSaveData, LoadPlayer |
| `Assets/_Project/Quests/Dto/ReputationSnapshotDto.cs` | +1 поле (knownFactionIds) |
| `Assets/_Project/Quests/Dto/ReputationSnapshotDto.cs` (NpcAttitude) | +1 поле (knownNpcIds) |
| `Assets/_Project/Reputation/ReputationClientState.cs` | +2 HashSet, +обработка в OnReputationSnapshotReceived |
| `Assets/_Project/Quests/Network/QuestServer.cs` | Включение knownFactions/Npcs в снапшоты |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | Фильтрация в RefreshReputationCache / RefreshNpcAttitudeCache |

**Новых файлов:** 0 (все изменения — расширение существующих).

---

## 7. Оценка Трудозатрат

| Шаг | Часы |
|---|---|
| 1-4: Server-side (QuestSaveData + QuestWorld + Save/Load) | 2-3ч |
| 5-7: DTO + ClientState + QuestServer wiring | 1-2ч |
| 8: CharacterWindow filtering | 1ч |
| 9: Тестирование | 1ч |
| **Итого** | **5-7ч** |

---

## 8. Будущее Расширение (v2+)

Knowledge system спроектирован с учётом расширения на другие сущности:
- **Локации** (LocationDefinition): узнал о локации → она появилась на карте
- **Предметы/Рецепты**: узнал рецепт → можешь крафтить
- **Квесты**: некоторые квесты скрыты до получения knowledge
- **Корабли/Фракции NPC-кораблей**: знание о фракции → видишь их корабли с правильной маркировкой

Архитектура позволяет: добавляем новый `Dictionary<ulong, HashSet<T>>` в QuestWorld, новый список в QuestSaveData, новый массив в DTO — паттерн един для всех типов knowledge.

---

**Вывод:** Система Knowledge интегрируется в существующую архитектуру с минимальными изменениями. 80% необходимой инфраструктуры уже существует (npcTalkedTo tracking, persistence, client states, UI). Основная работа — добавить `knownFactions`/`knownNpcs` как отдельную концепцию и фильтрацию в UI.
