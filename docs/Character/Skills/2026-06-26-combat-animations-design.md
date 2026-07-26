# Combat Animations: NPC + Player

> **Дата:** 2026-06-26
> **Статус:** 📝 design note для T-NPC-12 (combat animations) и T-Player-combat.
> **Источник:** задача "базовые анимации Combat" в Kevin Iglesias.

---

## 0. Цель

Подключить Combat-анимации Kevin Iglesias к NPC и Player:
- **Death** (NPC + Player) — `HumanM@Death01.fbx` (0.73s, no-loop)
- **Damage / GetHit** — `HumanM@CombatDamage01.fbx` (1.0s, no-loop)
- **Combat Idle** — `HumanM@CombatIdle01.fbx` (1.33s, loop)
- **Attack 1H** — `HumanM@Attack1H01_L.fbx` (1.10s, no-loop). Выбор L (любая рука, цикл повторяется).

---

## 1. Animator Parameters (общие для NPC и Player)

| Name | Type | Назначение |
|---|---|---|
| `Speed` | Float | 0..6+ — для blend tree (Idle ↔ Walk ↔ Run) |
| `IsAttacking` | Bool | legacy, дублирует Attack trigger (используется NpcBrain) |
| `Attack` | Trigger | одноразовый — проиграть Attack clip |
| `Damage` | Trigger | одноразовый — проиграть CombatDamage |
| `Death` | Trigger | одноразовый — проиграть Death |

`IsGrounded` и `Jump` остаются в PlayerAnimation для совместимости с NetworkPlayer.Update (R2-NONE: animator parameters).

---

## 2. Animator States (8 на NPC, 9 на Player)

### NPC (NpcAnimatorController)

| State | Motion | Loop | Переходы |
|---|---|---|---|
| **Idle** (default) | HumanM@Idle01 | loop | → Walk (Speed>0.1), → Damage (trigger), → Attack (trigger), → Death (trigger) |
| **Walk** | HumanM@Walk01_Forward | loop | → Idle (Speed<0.05), → Run (Speed>4), → Damage (trigger), → Attack (trigger), → Death (trigger) |
| **Run** | HumanM@Run01_Forward | loop | → Walk (Speed<3.5), → Damage, → Attack, → Death |
| **CombatIdle** | HumanM@CombatIdle01 | loop | → Damage, → Attack, → Death |
| **Attack** | HumanM@Attack1H01_L | no-loop | → CombatIdle (exit 0.95) |
| **Damage** | HumanM@CombatDamage01 | no-loop | → Idle (exit 0.95) |
| **Death** | HumanM@Death01 | no-loop | (финальное — нет переходов) |

**State Machine (anti-restrictive):**
- Damage/Attack/Death доступны из **любого** базового state через trigger (через transition с condition).
- Death → AnyState переход тоже работает (на случай смерти в Idle).
- NPC не имеет "combat mode" — у NPC всегда CombatIdle когда Speed=0 (нет Walk/Idle разделения при combat).

**Важное упрощение:** NPC переходит Idle→CombatIdle автоматически по Speed<0.05, не по "оружию". У NPC нет состояния "без оружия" (у моба всегда оружие).

### Player (PlayerAnimation)

| State | Motion | Loop | Переходы |
|---|---|---|---|
| **Idle** (default) | HumanM@Idle01 | loop | → Walk, → Damage, → Death |
| **Walk** | HumanM@Walk01_Forward | loop | → Idle, → Run |
| **Run** | HumanM@Run01_Forward | loop | → Walk |
| **CombatIdle** | HumanM@CombatIdle1H01 | loop | → Attack (trigger), → Damage, → Death |
| **Attack1H** | HumanM@Attack1H01_L | no-loop | → CombatIdle (exit 0.95) |
| **Damage** | HumanM@CombatDamage01 | no-loop | → Idle (exit 0.95) |
| **Death** | HumanM@Death01 | no-loop | (финальное) |
| **Jump** | HumanM@Jump01 | no-loop | (existing — для совместимости) |

**Решение для Player:** как только игрок получает **WeaponMain экипировку** — переключаемся на CombatIdle. Пока без оружия — обычный Idle. Реализация: bool параметр `InCombat` (true если экипировано оружие) → condition на переходы Idle↔CombatIdle, Walk↔CombatIdle.

---

## 3. Transitions (NPC)

### Из каждого базового состояния (Idle/Walk/Run/CombatIdle):

```
Idle → Walk        (Speed > 0.1, no exit time, dur 0.2)
Idle → CombatIdle  (Speed < 0.05, no exit time, dur 0.2)
Idle → Damage      (Damage trigger, no exit time, dur 0.1)
Idle → Attack      (Attack trigger, no exit time, dur 0.1)
Walk → Idle        (Speed < 0.05, no exit time, dur 0.2)
Walk → Run         (Speed > 4.0, no exit time, dur 0.2)
Walk → CombatIdle  (Speed < 0.05 && has weapon, no exit time, dur 0.2)
Walk → Damage      (Damage trigger)
Walk → Attack      (Attack trigger)
Run → Walk         (Speed < 3.5)
Run → Damage
Run → Attack
CombatIdle → Idle  (Speed > 0.1)  ← NPC начинает преследование
CombatIdle → Damage
CombatIdle → Attack
Attack → CombatIdle (exit time 0.95)
Damage → Idle       (exit time 0.95)
AnyState → Death    (Death trigger)
```

### Простое правило для NPC (MVP):
- Нет отдельных состояний для разного оружия. У NPC всегда есть оружие, поэтому CombatIdle = Idle когда Speed=0.
- Переход из Idle в CombatIdle не нужен — они играют одну роль.

**Упрощённый NPC controller (для MVP):**

| State | Motion | Loop |
|---|---|---|
| Idle (default) | HumanM@Idle01 | loop |
| Walk | HumanM@Walk01_Forward | loop |
| Run | HumanM@Run01_Forward | loop |
| Damage | HumanM@CombatDamage01 | no-loop |
| Attack | HumanM@Attack1H01_L | no-loop |
| Death | HumanM@Death01 | no-loop |

Damage/Attack/Death из всех базовых через AnyState? Нет — лучше target transition на конкретные states. Это стандартный паттерн Animator.

---

## 4. Code changes

### NpcBrain.cs (FSM обновление):

**Новое состояние в BrainState enum:** `TakeHit` (между Chase/Attack и Idle, короткая пауза 1 сек на анимацию).

**Логика:**
```csharp
case BrainState.TakeHit:
    // damage animation уже играет (триггер Damage), ждём выхода.
    if (_animator != null)
    {
        var info = _animator.GetCurrentAnimatorStateInfo(0);
        if (info.IsName("Damage") && info.normalizedTime >= 0.95f)
        {
            // Если цель ещё в aggroRange → вернуться в Chase/Attack
            if (_aggroTarget != null && _aggroTarget.IsAlive())
                EnterChase();
            else
                EnterIdle();
        }
    }
    break;
```

**Trigger Damage вызывается извне** через `NpcTarget.OnDamage` → `brain.OnTakeHit()` → `_animator.SetTrigger("Damage")` + переход в state TakeHit.

### NpcTarget.cs:

В `ApplyDamage` после расчёта урона:
```csharp
// Если HP > 0 после удара → запускаем Damage animation (моб жив, его качнуло).
if (newHp > 0 && animator != null && animator.runtimeAnimatorController != null)
{
    foreach (var p in animator.parameters)
    {
        if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Damage")
        {
            animator.SetTrigger("Damage");
            break;
        }
    }
}
```

### NetworkPlayer.cs:

- При атаке: в `Update` рядом с existing animator.SetFloat("Speed") добавить обработку атаки (уже есть `if (_animator != null) _animator.SetTrigger("Jump")` — добавить Attack при нажатии mouse1).
- При получении урона: подписка на событие `CombatServer.OnEntityDamaged` или просто в PlayerTarget (когда появится).

### PlayerAttacker.cs:

- В `Initialize` и при смене экипировки: устанавливать `_animator.SetBool("InCombat", true)` если экипировано основное оружие.

---

## 5. Чек-лист реализации

- [ ] NPC AnimatorController: 6 states, motions из Combat клипов
- [ ] Player AnimatorController: 9 states (8 новых + Jump для совместимости)
- [ ] NpcBrain: добавить BrainState.TakeHit + transition trigger
- [ ] NpcTarget: послать Damage trigger в ApplyDamage (когда HP > 0)
- [ ] NetworkPlayer: послать Attack trigger при атаке (mouse1)
- [ ] PlayerAttacker: устанавливать InCombat bool при экипировке
- [ ] PlayerTarget (когда появится): послать Damage trigger игроку при получении урона, Death trigger при HP=0
- [ ] Verify: refresh + read_console → 0 CS errors
- [ ] Play Mode test: NPC получают урон → покачивается; NPC умирают → death anim; Player атакует → attack anim

---

## 6. Что остаётся на потом (post-MVP)

- Player умирает (сейчас stub `IsAlive()` всегда true)
- Skills animation (spell cast) — отдельная секция
- Different attack types (2H, Polearm, Shield, Thrown) — отдельные Animator layers или sub-controllers
- Damage reaction variations (headshot, knockdown)
- Combat idle transitions при unequip weapon
- Multiple damage types (physical stagger vs magic hit)