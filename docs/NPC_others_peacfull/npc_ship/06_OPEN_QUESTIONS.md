# 06 — Open Questions → Final Decisions

> **Статус:** Решения приняты 2026-06-22, актуализировано под них.
> **Профиль:** project-c
> **Принято:** 13 ответов пользователя → все решения зафиксированы в этом файле и пропагированы через `00_README`, `03_V2_ARCHITECTURE`, `04_LIVING_BEHAVIOR`, `05_ROADMAP`.

---

## Сводка решений

### A. Movement & ShipController API

| Q | Вопрос | Решение | Распространено в |
|---|--------|---------|-----------------|
| **Q1** | `ShipController.ApplyServerInput()` дизайн | **A** — новый public server-only метод. Дополнительно: пользователь отметил, что этот API **может стать системой автопилота для игрока** (v2 hook) | `03_V2_ARCHITECTURE.md §5`, `04_LIVING_BEHAVIOR.md §6` |
| **Q2** | `_hasNpcPilot` флаг? | **Custom** — пользователь хочет явный `_hasNpcPilot` (bool, server-only). Отход от рекомендации B в пользу явного флага | `03_V2_ARCHITECTURE.md §5`, `04_LIVING_BEHAVIOR.md §2.1` |

### B. Scene & Ownership

| Q | Вопрос | Решение | Распространено в |
|---|--------|---------|-----------------|
| **Q3** | NPC InstanceId generation | **A** — `NetworkObjectId | 0x8000_0000_0000_0000UL` | `03_V2_ARCHITECTURE.md §4.1`, `04_LIVING_BEHAVIOR.md §` |
| **Q4** | `ShipOwnershipRegistry` для NPC | **A** — не регистрировать NPC. Они управляются NpcShipWorld, а не PlayerShip systems | `03_V2_ARCHITECTURE.md §` |

### C. Docking Integration

| Q | Вопрос | Решение | Распространено в |
|---|--------|---------|-----------------|
| **Q5** | `NpcShipStatus.Loading` в M1 | **A** — 30-90 сек пауза для визуального интереса | `04_LIVING_BEHAVIOR.md §2`, §4 |
| **Q6** | `maxConcurrentLandings` для NPC | **A** — учитывать лимит. NPC не могут превысить | `03_V2_ARCHITECTURE.md §7`, `04_LIVING_BEHAVIOR.md §5` |
| **Q7** | Multi-station per location | **B** — оставить single station. v2 сделает multi | `03_V2_ARCHITECTURE.md §`, limitation |

### D. Movement & Physics

| Q | Вопрос | Решение | Распространено в |
|---|--------|---------|-----------------|
| **Q8** | Gravity после `ExitDocked` | **A** — anti-gravity override в `NpcShipController` на время `ExitDocked → Departing` (5 сек) | `04_LIVING_BEHAVIOR.md §2.1`, `03_V2_ARCHITECTURE.md §4.5` |
| **Q9** | Rate limiting для NPC | **A** — не нужен. FSM сама ограничивает | OK |

### E. v2 Forward-compat

| Q | Вопрос | Решение | Распространено в |
|---|--------|---------|-----------------|
| **Q10** | Cargo manifest в M1 или v2? | **A** — пустой struct в M1. DTO contract стабилен | OK |

### F. Скоп

| Q | Вопрос | Решение | Распространено в |
|---|--------|---------|-----------------|
| **Q11** | Минимальное количество NPC | **Custom** — 4 NPC для теста, расширим позже. **Отход от рекомендации B (6 NPC)** | Все цифры в `04_LIVING_BEHAVIOR.md §3.2`, `05_ROADMAP.md §` |
| **Q12** | Маршрут — одна пара городов или больше? | **Custom** — Примум + ещё 1 зона вблизи (мини-тест). 2 станции в 1 сцене | `04_LIVING_BEHAVIOR.md §3.2`, `05_ROADMAP.md §` |
| **Q13** | NPC стартовое состояние | **A** — Docked на pad при старте | OK |

---

## Резюме отклонений от рекомендаций

Три отклонения:

1. **Q2:** Пользователь хочет явный `_hasNpcPilot` flag вместо implicit "проверяем _sumXxx > 0". Причина: ясность кода + возможно будущее расширение (e.g. multiplayer-NPC, owned-NPC). Уточнено в архитектуре.

2. **Q11:** Только 4 NPC для теста вместо 6. Причина: lean MVP. Расширим после первого smoke test.

3. **Q12:** Примум + ещё 1 зона в той же сцене (мини-тест) вместо полноценной второй станции в другой сцене. Причина: сразу тестируемо в `WorldScene_0_0` без новой стриминговой сцены.

---

## Архитектурные добавления (по новым решениям)

### ApplyServerInput → Автопилот для игрока (v2 hook)

Пользователь отметил, что `ApplyServerInput()` может стать **API для автопилота игрока в будущем**. Это значит:

- Метод должен быть спроектирован достаточно general, чтобы принимать input не только от NPC-pilot, но и от player-autopilot-компонента.
- Не должно быть hard-coded ссылок на `NpcShipController`.
- Сигнатура `ApplyServerInput(thrust, yaw, pitch, vertical, boost)` — уже generic.

**V2 hook:** `ProjectC.Player.AutoPilot.AutoPilotController` может вызывать тот же `ship.ApplyServerInput(...)` для движения корабля игрока по маршруту.

### `_hasNpcPilot` явный flag

Вместо неявной проверки `_sumXxx > 0` — явный flag, выставляемый при регистрации NPC в NpcShipWorld. Это даёт:

- Понятный API: `ship.EnableNpcPilot()` / `ship.DisableNpcPilot()`
- Возможность disable NPC-pilot в runtime (например, debug-режим)
- Расширяемость (multi-mode: player-controlled / NPC-controlled / autopilot)

### Anti-gravity override в `NpcShipController`

```csharp
// В NpcShipController при выходе из docked state:
private void OnExitDocked()
{
    StartCoroutine(AntiGravityBoostRoutine(5f)); // 5 sec anti-grav
}

private IEnumerator AntiGravityBoostRoutine(float duration)
{
    var ship = GetComponent<ShipController>();
    float originalAntiGrav = ship.AntiGravity; // new public getter
    ship.AntiGravity = 1.5f; // boost
    yield return new WaitForSeconds(duration);
    ship.AntiGravity = originalAntiGrav;
}
```

### `maxConcurrentLandings` для NPC

В `DockingWorld.AssignPadForNpc` дополнительно проверять `maxConcurrentLandings`. Если достигнут — `NO_SUITABLE_PAD` (count same as for player).

---

## Статус

✅ Все 13 решений зафиксированы  
✅ `03_V2_ARCHITECTURE.md`, `04_LIVING_BEHAVIOR.md`, `05_ROADMAP.md` обновлены  
✅ `CHANGELOG.md` создан

**Готовность к коду:** T-NS00.