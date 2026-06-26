# NPC + Player Animation System (T-NPC-12)

> **Дата:** 2026-06-26
> **Статус:** ✅ Завершено (базовый цикл анимаций)
> **Связанные документы:** `70_NPC_ENEMIES.md §11` (pitfalls)

---

## 1. Архитектура

### Контроллеры

| Контроллер | Ассет | Назначение |
|---|---|---|
| NpcAnimatorController | `Assets/_Project/Animation/AI/NpcAnimatorController.controller` | База NPC: 6 состояний, 5 параметров |
| NpcAnimator_Goblin | `Assets/_Project/Animation/AI/NpcAnimator_Goblin.overrideController` | **Override для дизайнера** — drag-and-drop слоты |
| PlayerAnimation | `Assets/_Project/Animations/PlayerAnimation.controller` | База игрока: 8 состояний, 7 параметров |
| PlayerAnimation_Default | `Assets/_Project/Animations/PlayerAnimation_Default.overrideController` | **Override для дизайнера** — drag-and-drop слоты |

### Принцип Override Controller

Дизайнер **не редактирует** `.controller` напрямую. Вместо этого:
1. На префабе стоит `Animator.runtimeAnimatorController` = `.overrideController`
2. `.overrideController` ссылается на базовый `.controller` + содержит таблицу замен
3. В инспекторе override'а — столбец с Original (только чтение) и столбец Override (drag-and-drop)
4. Клик на override → инспектор показывает слоты по именам клипов

**Где лежат:**
- NPC: `Assets/_Project/Animation/AI/NpcAnimator_Goblin.overrideController`
- Player: `Assets/_Project/Animations/PlayerAnimation_Default.overrideController`

**Проверка:** Animator на префабе → Controller = overrideController (не базовый).

---

## 2. NpcAnimatorController (6 states, 5 params)

### Параметры

| Name | Type | Назначение |
|---|---|---|
| `Speed` | Float (0..6+) | Локомоция (Idle 0, Walk 0.1-4, Run 4+) |
| `IsAttacking` | Bool | Для совместимости с NpcBrain (legacy) |
| `Attack` | Trigger | Одноразовая атака (1H меч) |
| `Damage` | Trigger | Реакция на получение урона |
| `Death` | Trigger | Смерть |

### Состояния и клипы

| State | Motion | Clip | Loop | Transitions |
|---|---|---|---|---|
| Idle | HumanM@Idle01 | 2.70s | yes | → Walk, → Damage, → Attack, → Death(any) |
| Walk | HumanM@Walk01_Forward | 0.80s | yes | → Idle, → Run, → Damage, → Attack, → Death(any) |
| Run | HumanM@Run01_Forward | 0.60s | yes | → Walk, → Damage, → Attack, → Death(any) |
| Attack | HumanM@Attack1H01_L | 1.10s | no | → Idle (exit 0.95) |
| Damage | HumanM@CombatDamage01 | 1.00s | no | → Idle (exit 0.95) |
| Death | HumanM@Death01 | 0.73s | no | — (финальный) |

---

## 3. PlayerAnimation (8 states, 7 params)

### Параметры

| Name | Type | Назначение |
|---|---|---|
| `Speed` | Float | Локомоция (Idle/Walk/Run) |
| `IsGrounded` | Bool | Для Jump (существовал ранее) |
| `Jump` | Trigger | Прыжок |
| `InCombat` | Bool | true = CombatIdle (с оружием), false = обычный Idle |
| `Attack` | Trigger | Атака |
| `Damage` | Trigger | Получение урона |
| `Death` | Trigger | Смерть |

### Состояния и клипы

| State | Motion | Clip | Loop | Transitions |
|---|---|---|---|---|
| Idle (default) | HumanM@Idle01 | 2.70s | yes | → Walk, → CombatIdle, → Attack, → Damage, → Jump |
| Walk | HumanM@Walk01_Forward | 0.80s | yes | → Idle, → Run, → CombatIdle, → Attack, → Damage |
| Run | HumanM@Run01_Forward | 0.60s | yes | → Walk, → Attack, → Damage |
| Jump | HumanM@Jump01 | 1.53s | no | → Idle (IsGrounded) |
| CombatIdle | HumanM@CombatIdle1H01 | 1.33s | yes | → Idle, → Attack, → Damage |
| Attack1H | HumanM@Attack1H01_L | 1.10s | no | → Idle (exit 0.95) |
| Damage | HumanM@CombatDamage01 | 1.00s | no | → Idle (exit 0.95) |
| Death | HumanM@Death01 | 0.73s | no | — (финальный) |

---

## 4. Code triggers

| Событие | Кто шлёт | Trigger | Где |
|---|---|---|---|
| Player movement | `NetworkPlayer.ProcessMovement()` | `Speed` (float), `IsGrounded` (bool) | Ежeно в Update |
| Combat mode | `NetworkPlayer.ProcessMovement()` | `InCombat` (bool) | При экипировке WeaponMain |
| Player attack (K key) | `NetworkPlayer.Update()` | `Attack` (trigger) | По нажатию K |
| Player takes damage | `PlayerTarget.ApplyDamage()` | `Damage` (trigger) | При HP>0 после удара |
| Player death | `PlayerTarget.ApplyDamage()` | `Death` (trigger) | При HP=0 |
| NPC takes damage | `NpcTarget.ApplyDamage()` | `Damage` (trigger) | При HP>0 после удара |
| NPC death | `NpcTarget.OnKilled()` | `Death` (trigger) | Уже существовал |
| NPC movement + attack state | `NpcBrain.UpdateAnimator()` | `Speed` (float), `IsAttacking` (bool), `Attack` (trigger) | Ежeно в Tick |

Anti-restrictive:
- Все триггеры проверяют `animator.runtimeAnimatorController != null` и наличие параметра через `animator.parameters`.
- Если параметра нет — вызов `SetTrigger()` без ошибки (NpcTarget, PlayerTarget, NetworkPlayer).

---

## 5. Клипы и где лежат

```
Assets/Kevin Iglesias/Human Animations/Animations/Male/
├── Idles/
│   └── HumanM@Idle01.fbx          ← 2.70s loop
├── Movement/
│   ├── Walk/HumanM@Walk01_Forward.fbx  ← 0.80s loop
│   ├── Run/HumanM@Run01_Forward.fbx    ← 0.60s loop
│   └── Jump/HumanM@Jump01.fbx          ← 1.53s no-loop
└── Combat/
    ├── HumanM@CombatDamage01.fbx   ← 1.00s no-loop
    ├── HumanM@Death01.fbx          ← 0.73s no-loop
    └── 1H/
        ├── HumanM@Attack1H01_L.fbx ← 1.10s no-loop
        └── HumanM@CombatIdle1H01.fbx ← 1.33s loop
```

---

## 6. Питфоллы (что пошло не так в P2)

Подробно — в `70_NPC_ENEMIES.md §11`.

### Коротко

| # | Причина T-pose | Как фиксили |
|---|---|---|
| 1 | `Animator.controller == null` на префабе | `SerializedObject.FindProperty("m_Controller")` |
| 2 | Два Animator: Visual + HumanM_Model (nested FBX) | `HumanM_Model/Animator.enabled = false` |
| 3 | `Animator.avatar == null` | `SerializedObject.FindProperty("m_Avatar")` из HumanM_Model.fbx |
| 4 | Transition states не существовали (fileID mismatch) | Пересоздан controller через `CreateAnimatorControllerAtPath()` |
| 5 | Attack trigger не срабатывал из Idle/Walk/Run | Добавлены переходы Attack→1H из всех базовых состояний |
| 6 | Player InCombat=false (нет WeaponMain) | Attack trigger работает из Idle/Walk/Run, не только CombatIdle |

---

## 7. Префабы

| Префаб | Animator | Controller |
|---|---|---|
| `Assets/_Project/Prefabs/AI/Npc_Goblin.prefab` | `Visual` child | `NpcAnimator_Goblin.overrideController` |
| `Assets/_Project/Prefabs/NetworkPlayer.prefab` | `Visual_Model` child | `PlayerAnimation_Default.overrideController` |

---

## 8. Что осталось на потом

- Player death stub (`IsAlive()` всегда true — не умирает)
- Анимации навыков (spell cast, 2H, Polearm, Shield, Thrown)
- Разные анимации атаки для разного оружия (кинжал vs меч vs топор)
- Proper weapon detection для InCombat (сейчас проверка EquipmentWorld)
- Отдельный слой анимации для Upper Body (оружие в руке)
