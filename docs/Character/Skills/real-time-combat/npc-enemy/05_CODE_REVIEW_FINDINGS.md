# 05 — Code Review Findings: Unified NPC Behavior Architecture

> **Дата:** 2026-07-15
> **Статус:** ⚠️ Требует исправлений (3 P0, 2 P1, 3 P2)
> **Ревизия:** полный обзор всех 14 скриптов AI после завершения Phase 1–4
> **Источник:** `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md`

---

## 0. Резюме

Архитектура composition-first (`NpcSocialBrain` + `NpcBrain` API) реализована чисто и близко к дизайн-документу. `NpcBrain.cs` (805 строк) остался FSM-ядром, не раздут. `NpcSocialBrain.cs` (859 строк) забрал всю социальную сложность. Разделение ответственности соблюдено. Код читаемый, комментарии с тикетами дисциплинированы. Компиляция без ошибок.

**Однако:** 3 критических бага ломают заявленную функциональность Phase 2–4. Групповая координация, vengeance-память и vocal cues не работают. Ниже — полный разбор.

---

## 1. 🔴 КРИТИЧЕСКИЕ БАГИ (ломают функциональность)

### 1.1 `NpcSpawner` НЕ создаёт группы — вся групповая логика мёртвая

| Параметр | Значение |
|---|---|
| **Приоритет** | P0 |
| **Локация** | `NpcSpawner.cs:TrySpawnAtPoint()` (стр. 205–281) |
| **Дизайн** | §7.1: Spawner спавнит NPC, создаёт `NpcGroupController`, назначает лидера |
| **Факт** | Spawner спавнит по одному. Нет `Instantiate` для `NpcGroupController`, нет `AddMember()` |

**Последствия:** `NpcGroupController` (465 строк) — dead code. Все проверки `Group != null` не проходят:
- `CheckAllyKilled()` → всегда `return false`
- `CheckLeaderAggrod()` → всегда `return false`
- `CheckOutnumbered()` → всегда `return false`
- `CheckReinforcementNearby()` → всегда `return false`
- `BroadcastAlarm()` / `OnVocalCue()` / `FocusFire()` / формации → никогда не вызываются
- `EvaluateTriggers` очищает `_activeTriggers`, T1-T4 всегда пусты, работают только T5-T7 (модификаторы)

**Исправление:** В `NpcSpawner` после спавна группы NPC — `Instantiate` новый GameObject с `NpcGroupController`, вызвать `AddMember()` для каждого.

---

### 1.2 `CheckAllyKilled` — `killerClientId` всегда 0 → Vengeance сломана

| Параметр | Значение |
|---|---|
| **Приоритет** | P0 |
| **Локация** | `NpcSocialBrain.cs:577` (сигнатура) и `:586` (проверка) |
| **Дизайн** | §4 T1: AllyKilled → forced aggro на killer + vengeance-запись |
| **Факт** | `killerClientId` инициализируется в 0 и никогда не заполняется |

Стр. 586:
```csharp
if (enableVengeanceMemory && faction != null && VengeanceMemory.Instance != null && killerClientId != 0)
```
Условие `killerClientId != 0` всегда ложно. `VengeanceMemory.RegisterKill()` никогда не вызывается.

**Исправление:** Определить `killerClientId` через `killerTarget` (если это PlayerTarget → clientId). Либо передавать `attackerClientId` через `NpcBrain.OnNpcHpChanged`.

---

### 1.3 `OnMemberKilled` никогда не вызывается → DeathScream + leader re-election сломаны

| Параметр | Значение |
|---|---|
| **Приоритет** | P0 |
| **Локация** | `NpcGroupController.cs:157` (определён, но не вызывается) |
| **Дизайн** | §7.2: OnMemberKilled вызывается при смерти NPC |
| **Факт** | Ни один код в проекте не вызывает `Group.OnMemberKilled(victim)` |

**Последствия:**
- `DeathScream` при смерти никогда не dispatch'ится
- `ElectNewLeader()` при смерти лидера не вызывается
- Morale penalty -0.3 от смерти лидера (стр. 170–178) — закомментировано как TODO

**Исправление:** В `NpcBrain.EnterDead()` (после установки `_state = Dead`) вызвать `_socialBrain?.Group?.OnMemberKilled(_socialBrain)`. Также убрать TODO и применить мораль-пенальти через новый публичный метод `NpcSocialBrain.ApplyMoraleDelta`.

---

## 2. 🟠 ДИЗАЙН-ПРОБЕЛЫ (фрагментация)

### 2.1 Vocal Cues: 3 из 5 cue-типов без gameplay-эффектов

| Параметр | Значение |
|---|---|
| **Приоритет** | P1 |
| **Локация** | `NpcGroupController.cs:248-268` (пустые case-блоки) |
| **Дизайн** | §6: все 5 cue имеют gameplay-эффекты |

| Cue | Требование | Реализация |
|---|---|---|
| **FearCry** | -0.05 morale союзникам | `NpcMoraleData.OnFearCryHeard()` есть, но **не вызывается** |
| **VictoryRoar** | +0.1 союзникам, -0.05 врагам | `OnVictoryRoarHeard()` / `OnEnemyVictoryRoarHeard()` есть, не вызываются |
| **Taunt** | Debuff цели | Не реализован совсем |
| AlertCall | ✅ Работает | `ForceChaseTarget` при получении |
| DeathScream | Не dispatch'ится | См. баг #1.3 |

**Корень:** `NpcSocialBrain._morale` — приватное поле struct без публичного API. `NpcGroupController` не может достучаться.

**Исправление:** Добавить публичные методы `ApplyMoraleDelta(float)` и `GetMorale()` в `NpcSocialBrain`. В `OnVocalCue` — вызывать `member.ApplyMoraleDelta(...)`.

---

### 2.2 `RecordPlayerHit` — dead code (grudge не записывается)

| Параметр | Значение |
|---|---|
| **Приоритет** | P1 |
| **Локация** | `NpcSocialBrain.cs:168` (определён), `NpcBrain.cs:248` (точка вызова) |
| **Дизайн** | §4 T4: GrudgeTrigger — persistent aggro при повторной встрече |
| **Факт** | `RecordPlayerHit` никогда не вызывается. GrudgeTable всегда пуста → `CheckGrudgeTrigger` бесполезен |

**Исправление:** В `NpcBrain.OnNpcHpChanged` — определить attacker (ближайший игрок или через `_aggroTarget`) и вызвать `_socialBrain.RecordPlayerHit(clientId)`.

---

## 3. 🟡 СТРУКТУРНЫЕ ЗАМЕЧАНИЯ

### 3.1 `NpcSocialBrain` — монолит 859 строк

| Параметр | Значение |
|---|---|
| **Приоритет** | P3 |
| **Порог из дизайна** | §1.2: «если NpcBrain + NpcSocialBrain > 1500 строк И модулей > 5 — рефакторинг» |
| **Факт** | 805 + 859 = 1664 строк. Порог превышен |

`ExecuteIdleActivity` — switch на 8 кейсов. Новые idle-активности (Phase 4) увеличат файл. Паттерн Strategy для idle-активностей решит проблему.

---

### 3.2 `FindObjectsByType` спам

| Параметр | Значение |
|---|---|
| **Приоритет** | P2 |
| **Локация** | `NpcSocialBrain.cs` — `CheckHostileNpcNearby`, `CheckAllyInCombat` (fallback), `FindNearestAlly`, `FindSocializePartner`, `ExecuteSit`, `SeekCover` |

Каждый SocialTick (~0.5с) потенциально вызывает 6+ `FindObjectsByType`. При 50 NPC = 300 поисков каждые 0.5с. Частично решается кешем через `NpcGroupController` после исправления бага #1.1.

---

### 3.3 Patrol: нет защиты от unreachable waypoint

| Параметр | Значение |
|---|---|
| **Приоритет** | P2 |
| **Локация** | `NpcSocialBrain.cs:ExecutePatrol()` |

Если waypoint за стеной или на другом navmesh-острове — NPC застревает навсегда. Нет таймаута, нет fallback-пропуска точки.

---

## 4. ✅ ЧТО РАБОТАЕТ (без замечаний)

- NpcBrain FSM (Idle/Chase/Attack/Dead/Surrendered)
- Platform carry / DeckNav
- Passive/Neutral/Aggressive behavior types
- NpcEmotionState + NpcEmotion enum (6 состояний)
- NpcMoraleData struct + все модификаторы
- NpcPersonalityConfig SO (5 traits)
- SocialRoleConfig SO + 5 factory-пресетов
- NpcFaction SO + relation system + 5 faction-ассетов
- VengeanceMemory (синглтон + API) — сам класс корректен, не вызывается
- CoverPoint / SitPoint маркеры с Editor Gizmos
- ThreatAssessment — static evaluation
- GrudgeTable — сам класс корректен
- NpcSpawnerConfig — все поля
- SocialTrigger enum + SocialTriggerData struct
- Anti-restrictive: `socialEnabled=false` → старый FSM

---

## 5. СВОДНАЯ ТАБЛИЦА ИСПРАВЛЕНИЙ

| # | Приоритет | Проблема | Локация |
|---|---|---|---|
| 1 | 🔴 P0 | NpcSpawner не создаёт группы | `NpcSpawner.cs` — добавить `NpcGroupController` + `AddMember` |
| 2 | 🔴 P0 | `killerClientId` всегда 0 в `CheckAllyKilled` | `NpcSocialBrain.cs:577` — заполнить clientId |
| 3 | 🔴 P0 | `OnMemberKilled` не вызывается | `NpcBrain.EnterDead` — вызвать `Group.OnMemberKilled` |
| 4 | 🟠 P1 | `RecordPlayerHit` не вызывается → grudge пуст | `NpcBrain.OnNpcHpChanged` — определить attacker |
| 5 | 🟠 P1 | FearCry/VictoryRoar/Taunt без эффектов | `NpcGroupController.OnVocalCue` — применить мораль |
| 6 | 🟡 P2 | FindObjectsByType спам | Кеш через `NpcGroupController` |
| 7 | 🟡 P2 | Patrol unreachable waypoint timeout | `NpcSocialBrain.ExecutePatrol` |
| 8 | 🟡 P3 | Монолит NpcSocialBrain 1664 строк | Рефакторинг idle на Strategy |

---

*Документ создан: 2026-07-15. Полное код-ревью после завершения Phase 1–4.*
