# Post-Mortem: NPC Movement Failure (2026-06-23)

**Статус:** ❌ Movement работает неправильно. Coрабли улетают вверх, кружат, не садятся.

**Корневая причина:** Логика движения **зависит от конкретных значений сил двигателя** и **не железобетонная**.

---

## Что пытались сделать

Цель: NPC-корабли курсируют между станциями A → B → A:
1. Взлетают с пада A
2. Летят к зоне коммуникации станции B (OuterCommZone)
3. Получают назначение на пад
4. Летят к паду
5. Стыкуются через trigger-зону
6. Dwell + Loading
7. Отстыковываются → обратно

---

## Что фактически происходило (по сессии)

### Симптомы по хронологии

| # | Симптом | Что пытался сделать | Что осталось |
|---|---------|---------------------|--------------|
| 1 | Корабли улетели вверх, не долетели до точки | Убрал collider на паде чтобы не застревали | Collider убран, но корневая причина не тронута |
| 2 | Не взлетали — врезались в пад | Сделал BoxCollider isTrigger=true | Работало частично |
| 3 | Танцевали с nose-up pitch | Убрал pitch (он и так отсутствовал в логике) | Бесполезный фикс |
| 4 | Змея вокруг оси | Yaw gain 0.5 → 0.02 | Поломали скорость поворота |
| 5 | Улетали вверх на 400+ метров | Magic threshold 30м | Всё ещё улетают |
| 6 | Встали над 1 падом | Refactor AssignPadForNpc | Помог частично |
| 7 | Не могут физически войти в trigger | Solid vs trigger collider | Не решено окончательно |
| 8 | Улетели вверх, минуя цели | PD-controller для Y | Применили, но не проверено |

### Текущее состояние (последний лог)

```
NPC 28 InTransit  pos=(40175, 2891, 40248)  Y=2891 (~390м выше старта 2505)
NPC 29 InTransit  pos=(40123, 2847, 40240)  Y=2847 (~345м)
NPC 2A InTransit  pos=(40250, 2967, 40190)  Y=2967 (~465м)
NPC 2B Departing pos=(40325, 2505, 39841)  Y=2505
```

**3 из 4 NPC поднялись на 350-465м вместо целевых 30м.** Это значит condition перехода не сработал корректно.

---

## Корневые проблемы (то, что я не понимал достаточно долго)

### Проблема #1: "Magic numbers" вместо физических измерений

Вся логика построена на hardcoded значениях:
- `cruiseAltitude = 30f`
- `if (currentY > departStartY + cruiseAlt)` — магическое число
- `if (Mathf.Abs(bearing) > 15f)` — магическое число для hysteresis
- `if (dist > 30f)` — магическое число для InTransit→Approaching
- `if (dist < 50f)` для триггер-зоны порта

**Почему это плохо:**
- При изменении сил двигателей все условия ломаются
- При изменении mass/thrust корабля поведение непредсказуемо
- При уменьшении drag NPC не успевают достичь условия → зависают
- При увеличении thrust перелетают цель → condition срабатывает слишком поздно

### Проблема #2: Input-based control вместо feedback-based

Логика: "пока не достигли цели → подавай input"
```
vertical: 0.8f // input
while (currentY < target) { apply(); }
```

**Должна быть:** feedback control — измерять ошибку и корректировать:
```
vertical_cmd = PD_controller(targetY - currentY, -currentVertVel)
apply(vertical_cmd)
```

ПД-регулятор автоматически подстраивается под силу двигателя. Я применил PD-controller в `ba4b0ba`, но **НЕ подтверждено в Play Mode** — после фикса нет результатов тестов.

### Проблема #3: Velocity drift между состояниями

При Departing накапливается `linearVelocity.y > 0`. В InTransit пишем `vertical: 0`, но **физика не останавливается мгновенно**. Корабль продолжает подниматься по инерции.

**Фикс в коде:** `KillVerticalVelocity(state.Ship)` при TransitionTo → `rb.linearVelocity.y = 0`. Применён, но **не проверен**.

### Проблема #4: Staggering и threshold offset не нужны

Был механизм stagger offset (0-200м случайный threshold для каждого NPC) — **это костыль**, скрывающий баг с тем что все NPC входят в Approaching одновременно. Правильное решение — стартовать NPC в разное время через schedule staggering.

### Проблема #5: AssignPadForNpc возвращал bool без информации о паде

Когда `EnterDocked()` вызывался сразу после AssignPadForNpc:
1. NPC «стыковался» в воздухе над станцией, не над падом
2. Несколько NPC могли сесть на 1 пад (collision check сработал, но spatially разные позиции)
3. Никакой Y-tolerant проверки — `dist < 5м` от центра пада (а не от BoxCollider)

**Фикс:** AssignPadForNpc → string padId, NPC летит к нему через `ResolvePadWorldPos`, стыковка по Unity OnTriggerEnter (BoxCollider.bounds.Contains). Применён.

### Проблема #6: Двойное хранение падов (SO + scene)

Были:
- `DockPadLayout_TEST_NPC.asset` — SO с 4 падами в координатах SO
- `TESTZONENPC` в сцене — 4 DockingPadTriggerBox в других координатах

Система использовала SO координаты, игнорируя scene. Расхождение → NPC стыковались не там.

**Фикс:** Убрал `DockPadLayout` SO, `DockingWorld.AssignPad` использует только `station.GetComponentsInChildren<DockingPadTriggerBox>()`.

### Проблема #7: Pad ID дублировались

На Примуме было 4 пада с `padId = "PAD-001"`. Все они давали одинаковый ключ `PadKey = "STN-PRM-001_PAD-001"`. Только 1 назначался.

**Фикс:** Пользователь сам переименовал в `NPC-PAD-01..04`. Это должно быть **prefab pattern с автогенерацией**, а не ручное переименование.

---

## Что было сделано (по коммитам, в обратном порядке)

| Commit | Что | Статус |
|--------|-----|--------|
| `ba4b0ba` | PD-controller altitude + StartCruiseY + KillVerticalVelocity | Не проверено в Play |
| `1809a09` | NPC solid collider + Y-tolerant IsShipInside polling | Не проверено в Play |
| `6d0a4ce` | Merge resolved | OK |
| `a4a662a` | Crane/manipulator: lift→turn→thrust→turn→lift | Был первичный вариант, перекрыт PD |
| `4a810c2` | Yaw 0.15, pad dist check, min thrust, stagger | Перекрыт |
| `02ce943` | Yaw gain 0.5 → было 0.02 | Yaw oscillation fix |
| `405b703` | AssignPadForNpc returns string padId | Архитектурный фикс |
| `8c07800` | Remove DockPadLayout ref from definition | Scene-only |
| `bb808b8` | AssignPad uses only scene trigger boxes | Scene-only |

---

## Что НЕ работает (по текущему логу)

✅ NPC взлетают (вертикальный lift работает)
✅ NPC крутятся, летят к станции (yaw работает)
✅ TryAssignPadForNpc назначает пады (все 4 получили разные)
❌ NPC улетают на Y=2900 вместо Y=2535 (departing не останавливается)
❌ NPC не стыкуются (Approaching → Docking не происходит, нет логов)
❌ Return path не тестировался (один полный цикл не завершился)
❌ Подъём обратно после Undocking не тестировался

---

## Что нужно сделать для V2 (M2)

### A. Переосмыслить логику с нуля

**Не делать:** больше input-based logic с magic numbers.
**Делать:** measurement-based control с обратной связью.

**Принципы:**
1. Никаких "подождать 3 секунды" — только "достигли состояния Y"
2. Никаких "dist < 5м" — только "BoxCollider.bounds.Contains(shipPos)"
3. Никаких "yaw=0.5 если bearing > 5°" — только "ПД-регулятор bearing→yaw"
4. Никаких hardcoded stagger — стартовать NPC в schedule с разным временем

### B. Тестовый сценарий M1-minimal

Один NPC, один маршрут PRIMIUM → PRIMIUM_TEST_ZONE_2, без dwelling, без loading:
1. Docked → Departing
2. Взлёт до Y=2535
3. Thrust к станции
4. Вход в OuterCommZone
5. Request pad
6. Fly to pad
7. Touchdown → Docked
8. ExitDocked → loop

Если это работает — добавлять сложность.

### C. Prefab pattern для падов

Вместо ручного переименования:
- `PadTriggerBox` с автогенерацией `padId = "{stationId}_PAD_{index}"`
- Совместимость через `compatibleShipClasses` массив
- Inspector wizard "Create DockStation" → создаёт parent + 4 триггер-бокса

### D. Debug инструменты

- Gizmos для пути NPC (line of current target)
- Server console команда `npc.status` → dump state всех NPC
- Server console команда `npc.teleport <id> <loc>` для быстрой отладки

---

## Вывод

Movement NPC **не готов**. Требуется полный рефакторинг в M2. Текущий код — набор ad-hoc фиксов поверх багов. Архитектурные фиксы (PD-controller, scene-only pads, IsShipInside) применены, но **не проверены end-to-end в Play Mode**.

**Следующий шаг:** остановить все улучшения, написать минимальный тест (1 NPC, 1 маршрут), отладить его до полного прохождения цикла. После этого масштабировать.