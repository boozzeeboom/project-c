# 05 — Code Review Findings: Unified NPC Behavior Architecture

> **Дата:** 2026-07-15
> **Статус:** ✅ P0 исправлены | ✅ P1 исправлены | ✅ P2 исправлен | 🟡 P3 отложен
> **Ревизия:** полный обзор всех 14 скриптов AI после завершения Phase 1–4
> **Исправления:** `f2effd6` (6 багов) + `34c27ee` (P2 static registry) — 7 из 8 исправлены


> **Источник:** `04_UNIFIED_BEHAVIOR_ARCHITECTURE.md`

---

## 0. Резюме

Архитектура composition-first (`NpcSocialBrain` + `NpcBrain` API) реализована чисто и близко к дизайн-документу. `NpcBrain.cs` (805 строк) остался FSM-ядром, не раздут. `NpcSocialBrain.cs` (859 строк) забрал всю социальную сложность. Разделение ответственности соблюдено. Код читаемый, комментарии с тикетами дисциплинированы. Компиляция без ошибок.

**Однако:** 3 критических бага ломают заявленную функциональность Phase 2–4. Групповая координация, vengeance-память и vocal cues не работают. Ниже — полный разбор.

---

## 1. 🔴 КРИТИЧЕСКИЕ БАГИ (ломают функциональность) — ✅ ВСЕ ИСПРАВЛЕНЫ (`f2effd6`)

### 1.1 ✅ `NpcSpawner` НЕ создаёт группы — вся групповая логика мёртвая


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

### 1.2 ✅ `CheckAllyKilled` — `killerClientId` всегда 0 → Vengeance сломана


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

### 1.3 ✅ `OnMemberKilled` никогда не вызывается → DeathScream + leader re-election сломаны


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

## 2. 🟠 ДИЗАЙН-ПРОБЕЛЫ (фрагментация) — ✅ ВСЕ ИСПРАВЛЕНЫ (`f2effd6`)

### 2.1 ✅ Vocal Cues: 3 из 5 cue-типов без gameplay-эффектов


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

### 2.2 ✅ `RecordPlayerHit` — dead code (grudge не записывается)


| Параметр | Значение |
|---|---|
| **Приоритет** | P1 |
| **Локация** | `NpcSocialBrain.cs:168` (определён), `NpcBrain.cs:248` (точка вызова) |
| **Дизайн** | §4 T4: GrudgeTrigger — persistent aggro при повторной встрече |
| **Факт** | `RecordPlayerHit` никогда не вызывается. GrudgeTable всегда пуста → `CheckGrudgeTrigger` бесполезен |

**Исправление:** В `NpcBrain.OnNpcHpChanged` — определить attacker (ближайший игрок или через `_aggroTarget`) и вызвать `_socialBrain.RecordPlayerHit(clientId)`.

---

## 3. 🟡 СТРУКТУРНЫЕ ЗАМЕЧАНИЯ

### 3.1 ✅ `FindObjectsByType` спам — анализ и решение

| Параметр | Значение |
|---|---|
| **Приоритет** | P2 |
| **Статус** | ✅ **Исправлено** (`f2effd6`). Статические реестры AllBrains/AllCoverPoints/AllSitPoints. |


**Анализ точек вызова (после исправления групп):**

| # | Метод | Условие вызова | Частота | Оценка влияния |
|---|---|---|---|---|
| 1 | `CheckHostileNpcNearby:187` | Каждый Tick, если `faction != null` и `Idle` | ~0.5с × N | ⚠️ Высокое — всегда |
| 2 | `NpcBrain.FindNearestHostileTarget:787` | Каждый Tick в Idle/Chase | ~0.1с × N | ⚠️ Высокое — всегда |
| 3 | `FindNearestAlly:303` | Только при Flee | ~редко | Низкое |
| 4 | `FindSocializePartner:370` | Только Socialize activity | ~редко | Низкое |
| 5 | `ExecuteSit:404` | Только Sit activity | ~редко | Низкое |
| 6 | `SeekCover:738` | HP < порога + под угрозой | ~редко | Низкое |
| 7 | `CheckAllyInCombat:664` | **Только если faction != null и нет Group** | ~0 после fix | ✅ Решено |
| 8 | `ThreatAssessment:147` | **Только если нет Group** | ~0 после fix | ✅ Решено |

**Ключевой вывод:** 2 горячих точки — #1 (`CheckHostileNpcNearby`) и #2 (`FindNearestHostileTarget`). Обе вызываются каждый AI-тик для каждого NPC. При 50 NPC это 50 × 2 = 100 `FindObjectsByType` каждые 0.1–0.5с.

**Оценка реального влияния на perf:** `FindObjectsByType` для 50 объектов ≈ 0.1–0.3ms. Суммарно 100 вызовов/сек = **10–30ms/сек** на сервере. При 60fps серверном тике это ~0.6–1.8ms на кадр — ощутимо, но не катастрофично для MVP. При 100+ NPC начинает быть проблемой.

**Решение:** Статический реестр `NpcSocialBrain`. Добавить `static List<NpcSocialBrain> AllBrains` с регистрацией в `Awake()/OnDestroy()`. `FindObjectsByType<NpcSocialBrain>` заменяется на итерацию по `AllBrains`. Стоимость: O(N) итерация по списку вместо O(N) поиска по сцене. Выигрыш: в ~3-5× быстрее (нет обхода иерархии, нет аллокаций).

**План реализации:**
1. В `NpcSocialBrain.Awake()` → `AllBrains.Add(this)`, `OnDestroy()` → `AllBrains.Remove(this)`
2. Заменить все `FindObjectsByType<NpcSocialBrain>` на `NpcSocialBrain.AllBrains`
3. Аналогично для `CoverPoint`: `static List<CoverPoint> AllCoverPoints`
4. Аналогично для `SitPoint`: `static List<SitPoint> AllSitPoints`
5. Для `FindNearestHostileTarget`: либо использовать `AllBrains`, либо кешировать результат на несколько тиков

**Оценка трудозатрат:** ~2 часа. **Рекомендация:** сделать сейчас, т.к. это единственный оставшийся P2 баг и он затрагивает каждый тик.

---

### 3.2 `NpcSocialBrain` — монолит: анализ и решение

| Параметр | Значение |
|---|---|
| **Приоритет** | P3 |
| **Статус** | 🟡 DEFER — не нужно сейчас |

**Текущее состояние:** `NpcBrain.cs` (821) + `NpcSocialBrain.cs` (908) = **1729 строк**. Порог из дизайна (§1.2): 1500 строк + >5 модулей.

**Что внутри NpcSocialBrain (908 строк):**
- Idle-активности: ExecutePatrol/Wander/Socialize/Work/Sit/Sleep/LookAround/StandStill (~180 строк)
- Emotion: UpdateEmotion + NpcEmotionState (~30 строк)
- Triggers: EvaluateTriggers + 5 Check-методов (~130 строк)
- Flee: CheckFleeConditions/StartFlee/StopFlee/FindNearestAlly (~60 строк)
- Grudge/Vengeance: CheckGrudgeTrigger/CheckVengeanceTrigger (~40 строк)
- Hostile detection: CheckHostileNpcNearby (~30 строк)
- Threat: EvaluateThreatBeforeCombat (~25 строк)
- Cover: CheckCover/SeekCover/AutoDetectCover/LeaveCover (~80 строк)
- Surrender: CheckSurrender (~25 строк)
- PostCombat: CheckPostCombat + 3 Tick-метода (~70 строк)
- Vocal: DispatchVocalCue (~15 строк)
- Config: ApplySpawnerConfig (~30 строк)
- Morale API: 5 методов (~10 строк)
- Поля/инициализация/Tick: (~175 строк)

**Нужен ли рефакторинг сейчас?**

**Аргументы ЗА:**
- Файл перешагнул порог в 900 строк
- Добавление нового поведения требует правки NpcSocialBrain
- Тестировать отдельные подсистемы сложно

**Аргументы ПРОТИВ:**
- Код читаемый и хорошо организован (секции с `====` разделителями)
- Все зависимости уже внутри одного класса — распиливание создаст coupling
- Switch в ExecuteIdleActivity не раздут (8 кейсов, каждый простой)
- Дизайн-документ явно говорит: «рефакторинг — Phase 4 опционально (T-NPC-S22)»
- Ни одной новой idle-активности или подсистемы не планируется в ближайших спринтах
- Риск: сломать работающий код ради косметического улучшения

**План рефакторинга (когда понадобится):**
1. Выделить `IIdleActivity` interface + 8 реализаций
2. `NpcIdleActivityController` — отдельный компонент на GameObject
3. `NpcCoverController` — выделить cover-логику
4. `NpcPostCombatController` — выделить post-combat
5. `NpcSocialBrain` становится тонким координатором (~200 строк)

**Оценка:** ~3 часа. **Вердикт: DEFER.** Провести когда:
- Понадобится добавить новую idle-активность (>8 текущих)
- Понадобится изолированное тестирование подсистемы
- Файл превысит 1100 строк (NpcSocialBrain один)

---

### 3.3 Patrol: нет защиты от unreachable waypoint

| Параметр | Значение |
|---|---|
| **Приоритет** | P2 |
| **Статус** | ✅ **Исправлено** (`f2effd6`). Anti-stuck timeout 15с. |

Если waypoint за стеной или на другом navmesh-острове — NPC застревает навсегда. Добавлен `_patrolStuckTimer`: если агент не приближается к waypoint >15 сек — пропуск точки.

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

| # | Приоритет | Проблема | Статус |
|---|---|---|---|
| 1 | 🔴 P0 | NpcSpawner не создаёт группы | ✅ `f2effd6` — TryFormGroups() |
| 2 | 🔴 P0 | `killerClientId` всегда 0 в `CheckAllyKilled` | ✅ `f2effd6` — ResolvePlayerClientId() |
| 3 | 🔴 P0 | `OnMemberKilled` не вызывается | ✅ `f2effd6` — EnterDead → Group.OnMemberKilled |
| 4 | 🟠 P1 | `RecordPlayerHit` не вызывается → grudge пуст | ✅ `f2effd6` — OnNpcHpChanged → RecordPlayerHit |
| 5 | 🟠 P1 | FearCry/VictoryRoar/Taunt без эффектов | ✅ `f2effd6` — публичное API морали |
| 6 | 🟡 P2 | FindObjectsByType спам | ✅ `f2effd6` — статический реестр AllBrains/AllCoverPoints/AllSitPoints |

| 7 | 🟡 P2 | Patrol unreachable waypoint timeout | ✅ `f2effd6` — anti-stuck 15с |
| 8 | 🟡 P3 | Монолит NpcSocialBrain 1729 строк | 🟡 DEFER — не нужно сейчас |

**Итог:** 7 из 8 исправлены. #8 (рефакторинг NpcSocialBrain) — отложен осознанно.


---

*Документ создан: 2026-07-15. Обновлён: 2026-07-15 (исправления + анализ P2/P3).*

