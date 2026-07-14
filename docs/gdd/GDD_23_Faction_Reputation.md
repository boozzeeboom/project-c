# GDD-23: Faction & Reputation — Project C: The Clouds

**Версия:** 3.0 | **Дата:** 14 июля 2026 г. | **Статус:** 🟢 Stage 1 реализован (FactionId, NpcAttitude, ReputationClientState, DialogActions) + NPC FactionSystem (Phase 4)
**Автор:** Малков Леонид Андреевич

---

## 1. Overview

Система фракций и репутации определяет **взаимоотношения игрока** с гильдиями, подпольными организациями, Правительством, мануфактурами и военными анклавами. Репутация влияет на доступ к миссиям, торговые цены, снаряжение, территории и контрактные маршруты.

**Архитектура:** Код разделён на два независимых слоя:
1. **Player ↔ Faction Reputation** (QuestWorld.ModifyReputation, ReputationClientState) — per-player reputation с фракциями
2. **NPC ↔ NPC FactionSystem** (Phase 4, NPC Unified Behavior) — runtime hostile/neutral/friendly между NPC разных фракций

**Связанные документы:** [GDD_25_Trade_Routes.md](GDD_25_Trade_Routes.md), [GDD_22_Economy_Trading.md](GDD_22_Economy_Trading.md), `docs/NPC_quests/02_V2_ARCHITECTURE.md`

---

## 2. Faction Overview

### 2.1 FactionId enum (реализовано, 11 lore значений)

```csharp
enum FactionId {
    None = 0,
    GuildOfThoughts = 1,   // Гильдия Мысли — наука, артефакты
    GuildOfCreation = 2,   // Гильдия Созидания — инженерия, модули
    GuildOfStrength = 3,   // Гильдия Силы — бой, охрана
    GuildOfSecrets = 4,    // Гильдия Тайн — разведка, шифры
    GuildOfSuccess = 5,    // Гильдия Успеха — торговля, коммерция
    Underground = 6,       // Подполье — контрабандисты
    Resistance = 7,        // Сопротивление — борцы за свободу
    FreeTraders = 8,       // Свободные торговцы — нейтральные купцы
    SOL_Patrol = 9,        // Патруль СОЛ — враждебная власть
    Pirates = 10,          // Пираты — рейдеры
    Neutral = 11,          // Нейтральные
}
```

Числовые значения совместимы с v1 `NpcFaction` (помечен `[Obsolete]` с алиасом).

> **Отличие от GDD-дизайна:** Вместо 12 заявленных значений (с 5 Guilds + Pirates/Smugglers/FreeTraders/Military/Mercenaries/Neutral/None) — **11** (другие названия и grouping). `Smugglers` → `Underground`; `Mercenaries` и `Military` → нет отдельных значений, покрываются `SOL_Patrol` + `Pirates`. `GuildOfExploration` и `GuildOfPreservation` отсутствуют, заменены на `GuildOfSecrets` + `GuildOfStrength`.

### 2.2 5 Гильдий (design)

| Гильдия | Сфера | Цвет | Штаб-квартира | FactionId |
|---------|-------|------|---------------|-----------|
| **Гильдия Мысли** | Наука, образование | Синий `#1E88E5` | Квартус (Мак-Кинли) | GuildOfThoughts (1) |
| **Гильдия Созидания** | Инженерия, строительство | Оранжевый `#FF9800` | Примум (Эверест) | GuildOfCreation (2) |
| **Гильдия Силы** | Охрана, военное дело | Красный `#F44336` | Секунд (К2) | GuildOfStrength (3) |
| **Гильдия Тайн** | Разведка, шифрование | Фиолетовый `#9C27B0` | Квартус (Мак-Кинли) | GuildOfSecrets (4) |
| **Гильдия Успеха** | Экономика, торговля | Зелёный `#4CAF50` | Тертиус (Аконкагуа) | GuildOfSuccess (5) |

### 2.3 Новое Правительство (НП)

| Параметр | Описание |
|----------|----------|
| Тип | Тоталитарное правительство |
| Контроль | Система СОЛ (идентификация), монополия на торговлю |
| Методы | Цензура, репрессии, патрули, налоговая проверка |
| FactionId | SOL_Patrol (9) |

### 2.4 Подпольные организации (design)

| Организация | Цель | FactionId |
|-------------|------|-----------|
| **Сопротивление** | Свержение НП | Resistance (7) |
| **Свободные торговцы** | Свободный рынок | FreeTraders (8) |
| **Подполье** | Контрабанда, чёрный рынок | Underground (6) |

> **Примечание:** Культ Фрейхейта (из v1 GDD) — заменён на Underground.

### 2.5 Военные анклавы (design)

Не имеют отдельного FactionId. Покрываются `Pirates` (10) + `SOL_Patrol` (9) для hostile factions. Полная имплементация — post-MVP.

### 2.6 Мануфактуры (design)

4 независимые производства. На данный момент **не имеют отдельных FactionId** в enum'е. Интеграция через репутацию мануфактур — post-MVP (T-X2 design discussion).

| Мануфактура | Город | Товар |
|-------------|-------|-------|
| **Аврора** | Примум | Антигравийные двигатели |
| **Титан** | Секунд | Военные модули, броня |
| **Гермес** | Тертиус | Текстиль, латекс, бытовые товары |
| **Прометей** | Квартус | МНП, научные разработки |

---

## 3. Reputation System (реализация)

### 3.1 Архитектура

```
Player Action (quest, dialog, contract)
  → QuestWorld.ModifyReputation(clientId, FactionId, delta)
    → Reputation изменяется
    → Broadcast event через OnReputationUpdated
    → Persist в JsonQuestStateRepository

ReputationClientState (singleton, AutoSpawn)
  → OnReputationUpdated event
  → CharacterWindow → таб «Репутация»
```

### 3.2 Хранение

```csharp
// В QuestWorld:
Dictionary<(ulong clientId, FactionId factionId), int> _reputation;

// Persistence в JsonQuestStateRepository.ReputationSaveEntry[]
```

### 3.3 Шкала репутации (design)

| Параметр | Значение |
|----------|----------|
| Диапазон | -100 … +100 |
| Начальное значение | 0 (нейтральный) |
| Изменение за квест | ±5 … ±25 |
| Изменение за провал | -10 … -30 |

### 3.4 Уровни репутации (design)

| Ранг | Диапазон | Привилегии |
|------|----------|-----------|
| **Враг** | -100 … -51 | Атака при виде, нет доступа |
| **Недружелюбный** | -50 … -21 | Отказ в услугах, высокие цены |
| **Нейтральный** | -20 … +20 | Базовый доступ, стандартные цены |
| **Дружелюбный** | +21 … +50 | Скидки 10%, дополнительные квесты |
| **Уважаемый** | +51 … +80 | Скидки 20%, редкие квесты, снаряжение |
| **Мастер** | +81 … +100 | Скидки 30%, уникальные квесты, лидерство |

---

## 4. NpcAttitude System (реализация)

### 4.1 NpcAttitude struct

```csharp
readonly struct NpcAttitude : IEquatable<NpcAttitude> {
    const int MinValue = -100;   // hostile
    const int MaxValue = +200;   // revered (asymmetric — positive stronger)
    
    string NpcId;
    int Value;  // clamp в ctor: [MinValue, MaxValue]
}
```

Отдельная шкала для отношений с конкретным NPC (независимо от faction reputation).

### 4.2 Хранение

```csharp
// В QuestWorld:
Dictionary<(ulong clientId, string npcId), int> _npcAttitude;

// NpcAttitudeClientState (singleton, AutoSpawn)
// → OnNpcAttitudeUpdated event
```

### 4.3 Cross-faction influence (MVP stub)

При изменении attitude одного NPC через `ModifyNpcAttitude`, рассчитывается влияние на faction reputation через `NpcDefinition.attitudeLinks[]`. Полная реализация — v2.

---

## 5. FactionDefinition + NpcDefinition (реализовано)

### 5.1 FactionDefinition (ScriptableObject)

| Поле | Описание |
|------|----------|
| factionId | FactionId |
| displayName | Локализованное имя |
| loreDescription | Описание лора |
| attitudeLinks[] | Cross-faction influence |

### 5.2 NpcDefinition (ScriptableObject)

| Поле | Описание |
|------|----------|
| npcId | Уникальный ID (string) |
| displayName | Имя NPC |
| faction | FactionId |
| questOffers[] | Какие квесты предлагает |
| questTurnIns[] | Какие квесты принимает |
| attitudeLinks[] | Cross-faction influence конфиги |

---

## 6. Dialog Integration (реализовано)

### 6.1 DialogueAction (17 типов)

Связанные с фракциями:

| Action | Описание |
|--------|----------|
| AddReputation(factionId, delta) | +репутация фракции |
| AddNpcAttitude(npcId, delta) | +отношение NPC |
| GiveCredits(amount) | Выдать кредиты |
| TakeItem(itemId, quantity) | Забрать предмет |

### 6.2 DialogueCondition (12 типов)

| Condition | Описание |
|-----------|----------|
| HasItem(itemId, quantity) | Есть предмет |
| ReputationAtLeast(factionId, min) | Репутация ≥ N |
| NpcAttitudeAtLeast(npcId, min) | Отношение ≥ N |
| QuestStateEquals(questId, state) | Статус квеста |

### 6.3 Example flow (M11 Mira E2E)

```
complete_thanks node:
  → AddReputation(GuildOfThoughts, +25)
  → AddNpcAttitude(mira_01, +10)
  → Broadcast клиенту → Mira E2E получает +25 репутации и +10 отношения
```

---

## 7. NPC FactionSystem (T-NPC-S19, июль 2026)

Реализован в рамках Phase 4 Unified NPC Behavior Architecture.

### 7.1 Компоненты

| Компонент | Описание |
|-----------|----------|
| `FactionSystem` | Отношения между фракциями (hostile/neutral/friendly) |
| `VengeanceMemory` | Память о врагах между сессиями |
| NPC-vs-NPC hostile faction combat | Фикс `b77b84e` |
| Интеграция в NpcSocialBrain | Через Phase 4 |

### 7.2 Отличие от Player Reputation

- **Player → Faction Reputation:** Персистентная, per-player, влияет на цены/доступ/квесты
- **NPC → NPC FactionSystem:** Runtime, per-NPC instance, определяет hostile/neutral/friendly в AI поведении

---

## 8. Что реализовано (Stage 1)

| Компонент | Статус |
|-----------|--------|
| **FactionId enum** (11 lore values) | ✅ DONE |
| **NpcAttitude struct** (readonly, −100..+200) | ✅ DONE |
| **NpcFaction → FactionId** migration (`[Obsolete]` alias) | ✅ DONE |
| **FactionDefinition SO** (factionId, displayName, lore, attitudeLinks) | ✅ DONE |
| **NpcDefinition SO** (npcId, faction, questOffers, questTurnIns, attitudeLinks) | ✅ DONE |
| **ReputationClientState** (singleton, AutoSpawn, OnReputationUpdated) | ✅ DONE |
| **NpcAttitudeClientState** (singleton, AutoSpawn, OnNpcAttitudeUpdated) | ✅ DONE |
| **QuestWorld.ModifyReputation** (server-side, broadcast + event + persist) | ✅ DONE |
| **QuestWorld.ModifyNpcAttitude** (server-side, broadcast + event + cross-faction MVP stub) | ✅ DONE |
| **DialogAction.AddReputation** (T-Q16) | ✅ DONE |
| **DialogAction.AddNpcAttitude** (T-Q16) | ✅ DONE |
| **CharacterWindow → таб «Репутация»** (T-Q13) | ✅ DONE |
| **Persistence** (JsonQuestStateRepository) | ✅ DONE |
| **NPC FactionSystem** (Phase 4, July 2026) | ✅ DONE |

## 9. Что открыто / TODO

| # | Задача | GDD-секция | Приоритет |
|---|--------|-----------|-----------|
| 1 | **4 мануфактуры** (Aurora/Titan/Hermes/Prometheus) как отдельные FactionId | §2, §7.1 | 🟡 Med (T-X2) |
| 2 | **Cross-faction influence — полная реализация** | §6 | 🟢 Low (MVP stub достаточно) |
| 3 | **TradeItemDefinition.Faction → FactionId migration** | §2 | 🟡 design discussion |
| 4 | **Display HUD репутации в header** | §7 | 🟢 Low |
| 5 | **Чёрный рынок** (вступление через контрабанду) | §7, §6 | 🟢 Low |
| 6 | **Военные анклавы** | §2, §7 | 🟢 Low |
| 7 | **СОЛ-стелс** | post-MVP | 🟢 Low |
| 8 | **Затухание репутации** (decay -1 в день) | §8 | 🟢 Low |
| 9 | **Квесты искупления** (reputation recovery) | §8 | 🟢 Low |

---

## 10. Формулы

| Формула | Описание | Статус |
|---------|----------|--------|
| `rep_change = base × difficulty × faction_mod` | Изменение репутации | design |
| `price_mod = 1.0 - (rep / 100) × 0.3` | Модификатор цен | 🟡 (через T-Q15 интеграцию) |
| `decay = -1 per day` | Затухание | 🔴 |
| `quest_access = rep >= threshold` | Доступ к квестам | ✅ (в DialogueCondition) |
| `ally_rep_change = rep_change × 0.5` | Изменение у союзников | 🔴 (MVP stub) |

---

## 11. Файлы (C#)

```
Quests/Factions/
├── FactionDefinition.cs     — SO: factionId, displayName, loreDescription, attitudeLinks[]
├── FactionId.cs             — enum: 11 lore значений
└── NpcAttitude.cs           — struct: -100..+200, IEquatable

Quests/Npcs/
└── NpcDefinition.cs         — SO: npcId, displayName, faction, questOffers[], attitudeLinks[]

Client states:
├── Quests/Client/QuestClientState.cs  — Reputation + NpcAttitude projection
├── Quests/Dto/ReputationSnapshotDto.cs
└── Scripts/Reputation/ (если есть — отдельный namespace)

NPC FactionSystem:
├── Scripts/AI/FactionSystem.cs
├── Scripts/AI/VengeanceMemory.cs
└── (в NPC Unified Behavior Phase 4)
```

---

*Документ создан для Project C: The Clouds.*
**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [GDD_22_Economy_Trading.md](GDD_22_Economy_Trading.md) | [GDD_25_Trade_Routes.md](GDD_25_Trade_Routes.md) | [`docs/NPC_quests/02_V2_ARCHITECTURE.md`](../NPC_quests/02_V2_ARCHITECTURE.md)
