# 02 — Industry Patterns: мирный NPC-траффик

> **Цель:** Исследовать индустриальные паттерны реализации мирного NPC-траффика в играх, оценить применимость к Project C (Unity 6, NGO 2.11, URP, воздушные корабли).
>
> **Примечание:** интернет-ресерч сабагентом не удался (HTTP 404 на API). Этот документ составлен на основе знания индустрии и упоминаний в существующих проектных документах. При появлении новых источников может быть расширен.

---

## 1. TL;DR — топ-5 применимых паттернов

| # | Паттерн | Откуда | Применимость к Project C |
|---|---------|--------|-------------------------|
| 1 | **Waypoint Graph + Schedule** | Elite Dangerous, EVE Online | NPC следует по станциям на маршруте, расписание задано в SO |
| 2 | **Gaussian arrival shaping** | Anno 1800, OpenTTD | Случайное разнесение прибытий |
| 3 | **Player-first pad contention** | Elite Dangerous, Star Citizen | Игрок получает pad приоритетно, NPC ждёт/отлетает |
| 4 | **Server-authoritative movement** | Unity Naval War / Flight sim | NPC корабль — server-owned NetworkObject, клиент видит через NetworkTransform |
| 5 | **Dwell time + визуальный loading** | Sea of Thieves, No Man's Sky | NPC стоит на pad 30-90 сек, маркеры загрузки |

---

## 2. Patterns table

| Pattern | Game Source | Applicability | Complexity | Recommendation |
|---------|-------------|---------------|------------|---------------|
| Ferry schedule (fixed A↔B) | Sea of Thieves (Merchant Alliance) | Высокая — наш базовый случай | Low | **Use** в M1 |
| Trade route loop (A→B→C→A) | Elite Dangerous (trade routes) | Высокая — несколько городов | Low | **Use** в M1-M2 |
| Dwell timer + visual marker | Anno 1800 (piers) | Высокая — жизненно для живости | Low | **Use** во всех milestone |
| Gaussian arrival distribution | OpenTTD, Factorio (train scheduling) | Высокая — предотвращает пачки | Low | **Use** в M1 |
| Player priority landing | Elite Dangerous (outpost landing) | Высокая — core contention | Medium | **Use** в M1 |
| NPC divert if pad busy | EVE Online (station docking) | Средняя — альтернатива ожиданию | Medium | **Use** в M1 |
| Cargo manifest (virtual) | Elite Dangerous (commodity trader) | V2 — привязка рынка | Medium | **Adapt** в v2 |
| Player autopilot через shared API | Star Citizen (quantum travel) | Q1 hook — потенциал | Medium | **v2 feature** |
| BDI agent (Belief-Desire-Intention) | GDC AI Summit 2019 | Слишком сложно для MVP | High | **Skip** в M1 |
| GOAP (Goal-Oriented Action Planning) | F.E.A.R., Killzone | Overkill для траффика | Very High | **Skip** |
| NavMesh for flying ships | Unity AI Navigation | Не подходит — 3D flight | N/A | **Skip** — waypoints path |

---

## 3. Schedule & traffic shaping (deep dive)

### 3.1 Детерминизм vs стохастика

- **Детерминированные расписания** (корабль A прибывает в Примум каждые 15 минут) — предсказуемы для игрока, но могут выглядеть неестественно.
- **Стохастические** (Gaussian вокруг среднего) — живые, но игрок не может «запланировать» встречу.

**Рекомендация: гибрид.** В `NpcShipSchedule` задаётся `meanArrivalIntervalSec` (например, 480 сек = 8 минут) с `arrivalIntervalStdDev` (90 сек). 99.7% прибытий в диапазоне [3.5, 12.5] минут. Игрок может примерно ожидать корабль, но не может «запарковаться на его pad за 2 секунды до прибытия».

### 3.2 Gaussian arrival algorithm

```python
# Box-Muller transform
def sample_gaussian(mean, std_dev):
    u1, u2 = random(), random()
    z = sqrt(-2 * log(u1)) * cos(2 * pi * u2)
    return mean + std_dev * z

# Per-station shaping
def schedule_next_arrival(station_id, schedule, now):
    raw = sample_gaussian(schedule.mean_interval, schedule.std_dev)
    clamped = clamp(raw, schedule.min_spacing, mean_interval * 2)
    if clamped - last_arrival(station_id) < min_spacing_sec:
        clamped = last_arrival(station_id) + min_spacing_sec
    return now + clamped + random_uniform(-jitter, +jitter)
```

### 3.3 Min spacing (для 4 NPC, см. Q11)

При 4 NPC и mean=8 min → ~0.5 NPC/min. С `minArrivalSpacingSec = 60` → макс 1 NPC в минуту на станцию. Игрок видит размеренный трафик, не пачки.

---

## 4. Dwell time & visual life

**Рекомендуемые значения для M1 (Q5):**

| Фаза | Что происходит | Длительность |
|------|---------------|--------------|
| **Approaching** | NPC снижается, заходит на pad | 10-20 сек |
| **Touchdown → Docked** | Приземлился, замок двигателя | 1 сек |
| **Loading (M1: no-op visual)** | Горят грузовые огни, flaps | **30-90 сек (Q5)** |
| **Undocking → Departing** | Снимается kinematic, отход от pad + anti-grav boost | 5-10 сек (+5 сек anti-grav, Q8) |

**Визуальные маркеры живости (M1 → v2):**
- **M1:** `DockPadVisualMarker` меняет цвет (красный/зелёный). Этого достаточно.
- **M1.5:** Particle effects (дым/пар при загрузке) — добавляются позже.
- **V2:** World-space label над NPC‑кораблём «Грузоперевозчик «Терциус» — загрузка 65%».

---

## 5. Pad contention & priority

### 5.1 Player-first — архитектура

1. Игрок запрашивает `RequestDockingRpc` → `DockingWorld.AssignPad()`:
   - Сначала ищем свободный pad (стандартно)
   - Если все pads заняты NPC → **Выбираем NPC на самом старом pad** (наибольший dwellTime) → Displacement
   - Если все pads заняты игроками → `NO_SUITABLE_PAD` (ждём очереди)
   - Проверяем `maxConcurrentLandings` (Q6) — общий лимит для всех

2. NPC запрашивает `AssignPadForNpc()`:
   - Только если pad свободен И `maxConcurrentLandings` не достигнут
   - Если все pads заняты или лимит → `Holding` (5 сек) → retry или `Diverting`

### 5.2 NPC displacement by player

Когда игрок `ConfirmAssignment` и целевой pad занят NPC:

```
1. DockingWorld проверяет: occupant is NPC? (IsNpcInstanceId())
2. Event: NpcShipWorld.OnPadTakenByPlayer(npcId, stationId, padId)
3. NPC FSM: Docked → Diverting
4. Server: ExitDocked() → ReleaseAssignment()
5. NPC берёт курс на следующую станцию
6. Player: занял pad
```

### 5.3 maxConcurrentLandings (Q6) — единый лимит

И `AssignPad()` (для игрока) и `AssignPadForNpc()` (для NPC) проверяют `DockStationDefinition.MaxConcurrentLandings`. NPC не имеет привилегий — равные условия с игроком.

### 5.4 Pacing (Anno 1800 подход)

Каждый NPC после вылета имеет `minCooldownBeforeNextDock` (сек). Это предотвращает цикл «пристыковался → диверт → тут же стыкуется на соседний pad». В M1 не реализовано, v2 hook.

---

## 6. Movement AI для летающих кораблей

### 6.1 Почему не NavMesh

NavMesh — для пешеходов/наземных юнитов. Воздушные корабли в 3D пространстве без препятствий (открытое небо) не нуждаются в pathfinding. Достаточно:

- **Waypoint** (прямая линия от A к B) — ideal для открытого мира
- **Smooth arrival slowdown** — NPC замедляется при подходе к станции
- **Station approach arc** — заход по дуге (не таран носом в pad)

### 6.2 Реализация в Unity

```
Vector3 targetPos = station.transform.position + approachOffset;
float dist = Vector3.Distance(rb.position, targetPos);

if (dist > approachRadius):
    // InTransit — летим прямо
    thrust = npcThrustMult * 0.6f
    yaw = CalcBearing(rb.position, targetPos) - rb.rotation.eulerAngles.y
elif (dist > landingRadius):
    // Approaching — замедляемся, снижаемся
    thrust *= (dist - landingRadius) / approachRadius
    yaw = CalcBearing(rb.position, targetPos) - rb.rotation.eulerAngles.y
    vertical = -0.2f  // descent
else:
    // Docking — финальное позиционирование (server snap)
    ServerTeleport(targetPos, targetRot)
```

### 6.3 Anti-gravity после Undocking (Q8 — Star Citizen подход)

В Star Citizen при выходе из стыковки корабль получает **auto-stabilizer boost** на несколько секунд, чтобы не упасть. Мы повторяем это в M1:

```csharp
// При ExitDocked:
ship.AntiGravity = 1.5f;  // boost
yield return new WaitForSeconds(5f);
ship.AntiGravity = 1.0f;  // normal
```

---

## 7. V2: Cargo / market integration

**EVE Online model (most relevant):**
- NPC-корабли везут `itemId, quantity, fromLocationId, toLocationId`
- При разгрузке в порту → `TradeWorld.AddToWarehouse(stationId, itemId, qty)` — меняет локальные цены
- При погрузке → `TradeWorld.RemoveFromWarehouse(stationId, itemId, qty)` — снижает предложение
- `MarketTimeService` тикает цены на основе supply/demand от NPC

**Sea of Thieves model (simpler):**
- NPC-корабли продают конкретные товары на outpost'ах
- Игрок может перехватить маршрут и украсть груз

**Рекомендация для Project C v2:** EVE-like, через `NpcShipWorld.OnNpcShipLoaded/Unloaded` события без прямого доступа к TradeWorld из NPC-кода.

---

## 8. V2: Player Autopilot (Q1 hook)

Пользователь отметил, что `ShipController.ApplyServerInput()` может стать основой для **player autopilot**. В Star Citizen / No Man's Space есть аналогичный паттерн:

- Игрок задаёт destination в HUD
- AutoPilot-компонент вычисляет bearing/distance
- Периодически вызывает `ship.ApplyServerInput(thrust, yaw, pitch, vertical, boost)`
- Корабль летит автономно, игрок может отменить в любой момент

**Преимущество Q1-решения:** API общий — не нужен специальный NPC-pilot компонент, тот же метод для автопилота игрока.

---

## 9. Anti-patterns

| Anti-pattern | Почему плохо |
|--------------|-------------|
| **Телепортация NPC** | Разрушает иллюзию живого мира. NPC должен быть физически виден в полёте |
| **Все NPC прибывают одновременно** | «Стая» из 8 кораблей на 4 pads → contention-шторм. Решение: Gaussian spacing |
| **NPC всегда занимает pad без проверки maxConcurrentLandings** | Конкуренция с игроком за ресурсы станции — несправедливо |
| **NPC исчезает в полёте при unload сцены** | Мир кажется пустым. Решение: NPC должен финишировать свой маршрут или persistence |
| **Каждый NPC — unique prefab** | Ад для дизайнера. 1-2 префаба, различаются color/material на scene placement |
| **Complex FSM с 15+ состояниями** | Ненужная сложность для M1. 6 состояний: Idle → Departing → InTransit → Approaching → Docked → Undocking |
| **NPC без anti-gravity после ExitDocked** | Корабль «падает» под gravity пока pilot не подаст вход. Решение: Q8 boost 5 сек |
| **NPC без `_hasNpcPilot` флага** | Неявная логика через `_sumXxx > 0` сложна для отладки. Решение: Q2 explicit flag |

---

## 10. References (industry)

- **Elite Dangerous — NPC traffic:** Faction-specific ship traffic, pads reserved for mission NPCs
- **Sea of Thieves — emergent NPC ships:** Merchant Alliance supply ships at outposts, visual cargo indicators
- **EVE Online — station docking:** NPC traffic in stations, docking slot contention per system
- **Anno 1800 — shipping AI:** Trade routes with sliders for priority, min/max cargo thresholds
- **OpenTTD — cargo distribution:** Gaussian arrival distribution, station waiting penalty, cargo routing by demand
- **Star Citizen — quantum travel + auto-stabilizer:** Anti-gravity boost при выходе из дока (Q8 inspiration)
- **GDC AI Summit 2019:** «Taming the Traffic: AI in Cities: Skylines» — road hierarchy, despawning, vehicle budget
- **Unity Asset Store:** `NPC Ship AI` (устаревший, 2019), `Flight AI` — не соответствует NGO 2.11

---

## 11. Заметка об ограничениях исследования

Один из трёх сабагентов (интернет-ресерч) **не смог выполнить запрос** — API поиска вернул HTTP 404. Поэтому раздел 10 «References» содержит ссылки по моему знанию индустрии, а не проверенные в реальном времени источники.

Если в будущем понадобятся более точные ссылки или специфические паттерны (например, конкретная реализация contention у Frontier Developments для Elite Dangerous), можно перезапустить web research сабагент с другим провайдером.