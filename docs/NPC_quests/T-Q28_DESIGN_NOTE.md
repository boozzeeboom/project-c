# T-Q28 — Prerequisite-фильтрация + Toast для locked квестов

> **Дата:** 2026-06-14
> **Сессия:** T-Q28 (runtime fallback dialog + prereq проверка)
> **Статус:** ✅ DONE
> **Зависимости:** M19 (CSV pipeline), T-Q27 (runtime fallback dialog tree)

---

## 1. Проблема

После M19-импорта `quests_bd_v1.csv` (796 квестов, 105 NPC) в диалоге с NPC появлялись **все** квесты из `questOffers[]` без какой-либо фильтрации. Это давало:

- NPC с **11-16 кнопками** "Взять квест: ..." — свалка
- `prereqQuest` в CSV **импортировался** в `QuestDefinition.prerequisites[]` (работало), но **не проверялся** в `QuestWorld.TryOffer`/`TryAccept` (не работало)
- Игрок брал `q_002_5` сразу, не проходя `q_002_0..q_002_4`
- 171 квест с `prereqQuest` по базе не давал цепочечного опыта

**Вторая проблема:** при клике на квест с невыполненным prereq:
- `DialogActionResultDto { success=false }` возвращался клиенту
- `QuestToast.HandleDialogActionResult` игнорировал `!success` — **диалог закрывался без feedback**

---

## 2. Что изменилось

### 2.1 QuestWorld.cs — новый helper + проверка в TryOffer/TryAccept

**3 публичных метода** (T-Q28 секция после `GetNpcAttitude`):

| Метод | Сигнатура | Что делает |
|-------|-----------|-----------|
| `GetPlayerQuestState` | `(ulong clientId, string questId) → QuestState?` | Возвращает state квеста в логе игрока, или null если нет |
| `IsPrerequisiteMet` | `(ulong clientId, QuestPrerequisite) → bool` | Проверка одного atomic prereq (7 типов: QuestCompleted, QuestActive, ReputationAtLeast, NpcAttitudeAtLeast, HaveItem, FlagIsSet, PlayerFaction) |
| `ArePrerequisitesMet` | `(ulong clientId, QuestDefinition) → (bool met, string reason)` | AND-комбинация всех prerequisites. reason — русская строка для UI |

**TryOffer** (строка 351-358): после idempotency check, перед созданием `QuestInstance`:
```csharp
var (prereqMet, prereqReason) = ArePrerequisitesMet(clientId, def);
if (!prereqMet) return Fail(QuestResultCode.PrerequisitesNotMet, prereqReason, questId);
```

**TryAccept** (строка 433-438): в пути "новый инстанс", перед max active cap:
```csharp
var (prereqMet, prereqReason) = ArePrerequisitesMet(clientId, def);
if (!prereqMet) return Fail(QuestResultCode.PrerequisitesNotMet, prereqReason, questId);
```

### 2.2 QuestServer.cs — фильтрация в BuildFallbackDialogTree

**per-player cache** — ключ `{npcId}@{clientId}` (раньше был просто `{npcId}`, что ломало бы цепочки — state разный у разных игроков).

**Новый enum `TQ28QuestAvailability`:**
```
Available        → можно взять (показываем "Взять квест: {name}")
Locked           → prereq не выполнены (показываем серым "🔒 {reason} ({name})")
AlreadyActive    → уже в логе (не показываем — turnIn покроет)
AlreadyCompleted → oneShot=true сдан (не показываем)
Hidden           → не показываем (failed, невалидный questId)
```

**Метод `CheckQuestAvailability`** (clientId, QuestDefinition) → `TQ28QuestAvailability`:
1. Если квест в логе — фильтр по state (Active → AlreadyActive, Completed+oneShot → AlreadyCompleted, Failed → Hidden)
2. Если не в логе — проверка `ArePrerequisitesMet` → Available/Locked

**Цикл `questOffers[]`:**
```
for each questOffer:
  def = GetQuest(questId); if null → Hidden
  avail = CheckAvailability(clientId, def)
  if Hidden/AlreadyCompleted/AlreadyActive → skip
  if Available → add edge "Взять квест: {questName}" → OfferQuest(questId)
  if Locked   → add edge "🔒 {reason} ({questName}" → OfferQuest(questId) (hideIfUnavailable=false)
```

**Цикл `questTurnIns[]`:** фильтруется так же — показывается только если квест в логе и можно сдать.

### 2.3 QuestToast.cs — toast на провал OfferQuest

В `HandleDialogActionResult` до этого был:
```csharp
if (!result.success) return; // молча
```

Теперь:
```csharp
if (!result.success)
{
    if (actionType == OfferQuest || actionType == AcceptQuest)
        ShowToast($"🔒 {result.resultData ?? "Не удалось взять квест"}");
    return;
}
```

---

## 3. Data flow (CSV → Runtime)

```
quests_bd_v1.csv
  └─ колонка "prereqQuest" — questId который нужно завершить
       │
       ▼
QuestCsvImporter.ImportQuest (строка 449-470)
  └─ prereqStr.Split(';', ',') → QuestPrerequisite[] { type=QuestCompleted, stringParam=questId }
       │
       ▼
QuestDefinition.prerequisites[] (SO asset)
       │
       ▼
QuestWorld.TryOffer(clientId, questId) ──→ ArePrerequisitesMet(clientId, def)
       │                                       └─ IsPrerequisiteMet(clientId, prereq)
       │                                             └─ GetPlayerQuestState(clientId, questId)
       │                                                   └─ _questsByPlayer[clientId] → state
       │
       ├─ met=true  → create QuestInstance (Discovered)
       └─ met=false → fail с "Сначала выполните квест «{questId}»"
```

### Где НЕ check, и почему

- `QuestReward` — не prereq, а поощрение после сдачи
- `minReputation` (поле `QuestDefinition`) — не CSV-колонка `prereqQuest`; добавится как `ReputationAtLeast` prereq если понадобится
- `QuestDefinition.faction` — только для display/grouping; faction-gating — через отдельный `ReputationAtLeast` prereq
- `oneShot` — влияет на видимость в диалоге (уже сданный oneShot скрыт), но не на проверку в TryOffer

---

## 4. Форматы сообщений

| Ситуация | Toast | Dialog-кнопка |
|----------|-------|--------------|
| Квест доступен | "✨ Найден квест: {name}" | "Взять квест: {name}" |
| Prereq не выполнен | "🔒 Сначала выполните квест «{questId}»" | 🔒 Сначала выполните квест «{questId}» ({name}) |
| Уже взят | скрыт | скрыт |
| oneShot сдан | скрыт | скрыт |
| Успешная сдача | "✅ Turned in: {name}" | "Сдать квест: {name}" |

---

## 5. Изменённые файлы

| Файл | Что | Строк добавилось |
|------|-----|-----------------|
| `Assets/_Project/Quests/Core/QuestWorld.cs` | 3 новых public-метода (prereq helpers) + проверка в TryOffer / TryAccept | ~100 |
| `Assets/_Project/Quests/Network/QuestServer.cs` | per-player cache, CheckQuestAvailability, фильтрация offers/turnIns, диалог | ~120 |
| `Assets/_Project/Quests/UI/QuestToast.cs` | HandleDialogActionResult — toast при OfferQuest/AcceptQuest с success=false | ~15 |

**0 новых ассетов. 0 новых CSV-колонок.** Всё работает на существующем `prereqQuest`.

---

## 6. Что не вошло

- **hover-tooltip на 🔒 кнопках** — UI Toolkit автоматически рендерит `unavailableReason` через `DialogOptionDto.hintIfUnavailable` (есть в DTO), но клиентский DialogWindow его не показывает. Опционально.
- **dialog НЕ удерживается при провале** — Toast компенсирует, но диалог всё ещё закрывается. Можно доработать: если `success=false && actionType=OfferQuest` → не закрывать диалог, показать hint в самом диалоге.
- **Cache TTL** — per-player cache живет пока игрок не поговорит с NPC снова. При взятии квеста в одном talk → новый talk строит новый tree. OK для текущих масштабов.
