# GDD-23: Faction & Reputation — Project C: The Clouds

**Версия:** 3.1 | **Дата:** 4 августа 2026 г. | **Статус:** 🟢 Stage 1 реализован (FactionId, NpcAttitude, ReputationClientState, DialogActions) + NPC FactionSystem (Phase 4) + Activity Anchors (T-NPC-S23)
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

### 2.1 FactionId enum (реализовано)

```csharp
enum FactionId {
    None = 0,
    GuildOfThoughts = 1,   // Гильдия Мысли — инженерия, физика, точные науки
    GuildOfCreation = 2,   // Гильдия Созидания — химия, медицина
    GuildOfStrength = 3,   // Гильдия Силы — боевое крыло, служба безопасности
    GuildOfSecrets = 4,    // Гильдия Тайн — городское управление
    GuildOfSuccess = 5,    // Гильдия Успеха — городские службы, финансовая система
    Underground = 6,       // [legacy] зарезервировано (больше не в лоре)
    Resistance = 7,        // [legacy] зарезервировано (больше не в лоре)
    FreeTraders = 8,       // [legacy] зарезервировано (больше не в лоре)
    SOL_Patrol = 9,        // [legacy] зарезервировано (больше не в лоре)
    Pirates = 10,          // Ржавые пираты — самая известная пиратская группировка
    Neutral = 11,          // Фермеры — мирные жители фермерских поселений
    Bandits = 12,          // Бандиты — нецентрализованные преступники
    Cultists = 13,         // [legacy] зарезервировано (больше не в лоре)
    Guards = 14,           // [legacy] зарезервировано (больше не в лоре)
    Villagers = 15,        // Городские жители Нового Правительства
}
```

Числовые значения совместимы с v1 `NpcFaction` (помечен `[Obsolete]` с алиасом) и расширением T-FACTION-UNIFY (12-15).

> **Текущие lore-фракции:** 5 Гильдий (1-5) — созданы Новым Правительством, allied между собой; Ржавые пираты (10); Фермеры (11, neutral); Бандиты (12); городские жители (15). Значения 6-9, 13, 14 — legacy: зарезервированы в коде для совместимости сериализованных данных, из лора исключены.

### 2.2 5 Гильдий (design)

Все 5 Гильдий созданы Новым Правительством и **allied между собой** (взаимоподдерживаемые): репутация и отношения с одной Гильдией косвенно влияют на остальные (см. `ally_rep_change`, §10).

| Гильдия | Сфера | Цвет | Штаб-квартира | FactionId |
|---------|-------|------|---------------|-----------|
| **Гильдия Мысли** | Инженеры-строители, физика и точные науки | Оранжевый | Секунд (К2) | GuildOfThoughts (1) |
| **Гильдия Созидания** | Химия и медицина, обработка мезия | Зелёный | Тертиус (Аконкагуа) | GuildOfCreation (2) |
| **Гильдия Силы** | Боевое крыло и служба безопасности объединённых городов НП | Красный | Примум (Эверест) | GuildOfStrength (3) |
| **Гильдия Тайн** | Городское управление (малоизвестная, вызывает вопросы у горожан) | Фиолетовый | [TBD] | GuildOfSecrets (4) |
| **Гильдия Успеха** | Управление городских служб и финансовая система НП | Жёлтый | [TBD] | GuildOfSuccess (5) |

**Боевая специализация (для будущего наполнения боевой системы):**

| Гильдия | Оружие |
|---------|--------|
| **Созидания** | Специализированное мезиевое оружие, шокеры, огнеметы |
| **Тайн** | Скрытые клинки и гравитационное оружие |
| **Мысли** | Антигравитационное оружие, силовые установки и экзоскелеты |
| **Успеха** | Арбалеты и пневматические винтовки |
| **Силы** | Любое вооружение, кроме специализированного гильдейского |

### 2.3 Новое Правительство (НП)

| Параметр | Описание |
|----------|----------|
| Тип | Верховная власть над объединёнными городами |
| Структура | Создало и поддерживает 5 Гильдий — allied между собой |
| Сила и безопасность | Гильдия Силы (боевое крыло, патрули, служба безопасности) |
| Городское управление | Гильдия Тайн |
| Финансы и службы | Гильдия Успеха (финансовая система, городские службы) |
| FactionId | Отдельного FactionId нет — в игре НП представлено Гильдиями (3, 4, 5) |

### 2.4 Игровые фракции (раскрываются со временем)

Объявлены как игровые, но раскрываются перед игроком постепенно:

| Фракция | Описание | Стихия | FactionId |
|---------|----------|--------|-----------|
| **Бандиты** | Нцентрализованная совокупность преступивших общие нормы поведения и законы Нового Правительства | Враждебны ко всем | Bandits (12) |
| **Ржавые пираты** | Самая известная пиратская группировка | Враждебны (рейды) | Pirates (10) |
| **Фермеры** | Мирные жители фермерских поселений | Нейтральные | Neutral (11) |
| **Городские жители (Villagers)** | Городские жители Нового Правительства | Дружелюбные/нейтральные | Villagers (15) |

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
| **FactionId enum** (16 значений, 9 lore-фракций) | ✅ DONE |
| **NpcAttitude struct** (readonly, −100..+200) | ✅ DONE |
| **NpcFaction → FactionDefinition объединение** (T-FACTION-UNIFY, 2026-07-20) | ✅ DONE |
| **FactionDefinition SO** (factionId, displayName, lore, attitudeLinks) | ✅ DONE |
| **NpcDefinition SO** (npcId, faction, questOffers, questTurnIns, attitudeLinks) | ✅ DONE |
| **Knowledge System** (T-KNOW, 2026-07-20) — server-authoritative faction/NPC knowledge с UI filtering | ✅ DONE |
| **ReputationClientState** (singleton, AutoSpawn, OnReputationUpdated) | ✅ DONE |
| **NpcAttitudeClientState** (singleton, AutoSpawn, OnNpcAttitudeUpdated) | ✅ DONE |
| **QuestWorld.ModifyReputation** (server-side, broadcast + event + persist) | ✅ DONE |
| **QuestWorld.ModifyNpcAttitude** (server-side, broadcast + event + cross-faction MVP stub) | ✅ DONE |
| **DialogAction.AddReputation** (T-Q16) | ✅ DONE |
| **DialogAction.AddNpcAttitude** (T-Q16) | ✅ DONE |
| **CharacterWindow → таб «Репутация»** (T-Q13) | ✅ DONE |
| **Persistence** (JsonQuestStateRepository) | ✅ DONE |
| **NPC FactionSystem** (Phase 4, July 2026) | ✅ DONE |
| **NPC Activity Anchors** (T-NPC-S23, 2026-07-29) | ✅ DONE |

## 9. Что открыто / TODO

| # | Задача | GDD-секция | Приоритет |
|---|--------|-----------|-----------|
| 1 | **Cross-faction influence — полная реализация** (allied-гильдии: ripple репутации между 5 Гильдиями) | §6, §10 | 🟡 Med |
| 2 | **TradeItemDefinition.Faction → FactionId migration** | §2 | 🟡 design discussion |
| 3 | **Display HUD репутации в header** | §7 | 🟢 Low |
| 4 | **Затухание репутации** (decay -1 в день) | §8 | 🟢 Low |
| 5 | **Квесты искупления** (reputation recovery) | §8 | 🟢 Low |

---

## 10. Формулы

| Формула | Описание | Статус |
|---------|----------|--------|
| `rep_change = base × difficulty × faction_mod` | Изменение репутации | design |
| `price_mod = 1.0 - (rep / 100) × 0.3` | Модификатор цен | 🟡 (через T-Q15 интеграцию) |
| `decay = -1 per day` | Затухание | 🔴 |
| `quest_access = rep >= threshold` | Доступ к квестам | ✅ (в DialogueCondition) |
| `ally_rep_change = rep_change × 0.5` | Изменение у allied-фракций (5 Гильдий) | 🟡 (актуально в новом лоре, MVP stub) |

---

## 11. Файлы (C#)

```
Quests/Factions/
├── FactionDefinition.cs     — SO: factionId, displayName, loreDescription, attitudeLinks[]
├── FactionId.cs             — enum: 16 значений (9 lore-фракций + legacy)
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
