# NPC + Player Animation System (T-NPC-12/13)

> **Дата:** 2026-06-26
> **Статус:** ✅ Завершено (сессия P2 — базовый цикл + directional movement)
> **Связанные документы:** `70_NPC_ENEMIES.md §11` (pitfalls), `85_DIRECTIONAL_MOVEMENT.md`

---

## 1. Архитектура

### Контроллеры

| Контроллер | Ассет | Состояний | Параметров | Назначение |
|---|---|---|---|---|
| NpcAnimatorController | `Assets/_Project/Animation/AI/NpcAnimatorController.controller` | 10 | 8 | База NPC |
| NpcAnimator_Goblin | `Assets/_Project/Animation/AI/NpcAnimator_Goblin.overrideController` | — | — | Override для дизайнера |
| PlayerAnimation | `Assets/_Project/Animations/PlayerAnimation.controller` | 13 | 11 | База игрока |
| PlayerAnimation_Default | `Assets/_Project/Animations/PlayerAnimation_Default.overrideController` | — | — | Override для дизайнера |

### Принцип Override Controller

Дизайнер **не редактирует** `.controller` напрямую. Вместо этого:
1. На префабе стоит `Animator.runtimeAnimatorController` = `.overrideController`
2. `.overrideController` ссылается на базовый `.controller` + таблицу замен
3. В инспекторе override'а — столбец Original (только чтение) + Override (drag-and-drop)
4. Клик на override → инспектор показывает слоты по именам клипов

**Где лежат:**
- NPC: `Assets/_Project/Animation/AI/NpcAnimator_Goblin.overrideController`
- Player: `Assets/_Project/Animations/PlayerAnimation_Default.overrideController`

**Проверка:** Animator на префабе → Controller = overrideController (не базовый).

---

## 2. NpcAnimatorController (10 states, 8 params)

### Параметры

| Name | Type | Назначение |
|---|---|---|
| `Speed` | Float (0..6+) | Локомоция (Idle 0, Walk 0.1-4, Run 4+) |
| `IsAttacking` | Bool | Legacy (NpcBrain использует вместо Attack trigger) |
| `Attack` | Trigger | Атака |
| `Damage` | Trigger | Получение урона |
| `Death` | Trigger | Смерть |
| `TurnLeft` | Bool | Поворот налево |
| `TurnRight` | Bool | Поворот направо |
| `IsGrounded` | Bool | Для Fall → SetBool("IsGrounded", true) в NpcBrain |

### Состояния и клипы

| State | Motion | Clip | Loop | Transitions |
|---|---|---|---|---|
| Idle | HumanM@Idle01 | 2.70s | yes | → Walk, → Damage, → Attack, → Fall, → TurnL/R |
| Walk | HumanM@Walk01_Forward | 0.80s | yes | → Idle, → Run, → Damage, → Attack, → Fall |
| Run | HumanM@Run01_Forward | 0.60s | yes | → Walk, → Damage, → Attack, → Fall |
| Attack | HumanM@Attack1H01_L | 1.10s | no | → Idle (exit 0.95) |
| Damage | HumanM@CombatDamage01 | 1.00s | no | → Idle (exit 0.95) |
| Death | HumanM@Death01 | 0.73s | no | — (финальный) |
| Fall | HumanM@Fall01 | 1.00s | yes | → Land, → Idle (IsGrounded) |
| Land | HumanM@Jump01 - Land | 0.60s | no | → Idle (exit 0.95) |
| TurnLeft | HumanM@Turn01_Left | 1.00s | yes | → Idle (exit 0.6) |
| TurnRight | HumanM@Turn01_Right | 1.00s | yes | → Idle (exit 0.6) |

---

## 3. PlayerAnimation (13 states, 11 params)

### Параметры

| Name | Type | Назначение |
|---|---|---|
| `Speed` | Float | Локомоция (Idle/Walk/Run/Sprint) |
| `IsGrounded` | Bool | Для Jump/Fall transition |
| `Jump` | Trigger | Прыжок |
| `InCombat` | Bool | true = CombatIdle (с оружием), false = обычный Idle |
| `Attack` | Trigger | Атака |
| `Damage` | Trigger | Получение урона |
| `Death` | Trigger | Смерть |
| `TurnLeft` | Bool | Поворот налево |
| `TurnRight` | Bool | Поворот направо |
| `MoveX` | Float (-1..1) | BlendTree direction X (left/right) |
| `MoveY` | Float (-1..1) | BlendTree direction Y (forward/backward) |

### Состояния и клипы

| State | Motion | Clip / BlendTree | Loop | Transitions |
|---|---|---|---|---|
| Idle (default) | HumanM@Idle01 | 2.70s | yes | → Walk, → Sprint, → CombatIdle, → Attack, → Damage, → Jump, → Fall, → TurnL/R |
| Walk | **Walk_BlendTree** | 8-dir SimpleDirectional2D | yes | → Idle, → Run, → CombatIdle, → Attack, → Damage, → Fall |
| Run | **Run_BlendTree** | 8-dir SimpleDirectional2D | yes | → Walk, → Sprint, → Attack, → Damage, → Fall |
| Sprint | **Sprint_BlendTree** | 5-dir SimpleDirectional2D | yes | → Run, → Attack, → Damage, → Fall |
| Jump | HumanM@Jump01 | 1.53s | no | → Idle (IsGrounded), → Fall (!IsGrounded) |
| Fall | HumanM@Fall01 | 1.00s | yes | → Land, → Idle (IsGrounded) |
| Land | HumanM@Jump01 - Land | 0.60s | no | → Idle (exit 0.95) |
| CombatIdle | HumanM@CombatIdle1H01 | 1.33s | yes | → Idle, → Attack, → Damage |
| Attack1H | HumanM@Attack1H01_L | 1.10s | no | → Idle (exit 0.95) |
| Damage | HumanM@CombatDamage01 | 1.00s | no | → Idle (exit 0.95) |
| Death | HumanM@Death01 | 0.73s | no | — (финальный) |
| TurnLeft | HumanM@Turn01_Left | 1.00s | yes | → Idle (exit 0.6) |
| TurnRight | HumanM@Turn01_Right | 1.00s | yes | → Idle (exit 0.6) |

### Speed thresholds

| State | Speed range |
|---|---|
| Idle | 0 (Speed < 0.1) |
| Walk | 0.1 < Speed < 5 |
| Run | 5 < Speed < 8 |
| Sprint | Speed > 8 |

---

## 4. BlendTree — Directional Movement (T-NPC-13)

### Walk_BlendTree (8 children)

```
Position (X=MoveX, Y=MoveY):
  (0,  1)   ← Walk01_Forward
  (0, -1)   ← Walk01_Backward
  (-1, 0)   ← Walk01_Left
  (1,  0)   ← Walk01_Right
  (-0.71,  0.71) ← Walk01_ForwardLeft
  (0.71,  0.71) ← Walk01_ForwardRight
  (-0.71, -0.71) ← Walk01_BackwardLeft
  (0.71, -0.71) ← Walk01_BackwardRight
```

### Run_BlendTree (8 children)

То же, но с Run01_* клипами.

### Sprint_BlendTree (5 children)

```
(0,  1)   ← Sprint01_Forward
(-1, 0)   ← Sprint01_Left
(1,  0)   ← Sprint01_Right
(-0.71,  0.71) ← Sprint01_ForwardLeft
(0.71,  0.71) ← Sprint01_ForwardRight
```

### Параметры управления

- `MoveX` = `moveInput.x` (A=-1, D=+1, idle=0)
- `MoveY` = `moveInput.y` (W=+1, S=-1, idle=0)
- Устанавливаются в `NetworkPlayer.ProcessMovement()` после вычисления `hasInput`

---

## 5. Code triggers

| Событие | Кто шлёт | Параметры | Место |
|---|---|---|---|
| Player movement | `NetworkPlayer.ProcessMovement()` | `Speed`, `IsGrounded`, `MoveX`, `MoveY` | Ежефреймово в Update |
| Combat mode | `NetworkPlayer.ProcessMovement()` | `InCombat` (bool) | При экипировке WeaponMain |
| Player attack (K key) | `NetworkPlayer.Update()` | `Attack` (trigger) | По нажатию K (временный debug) |
| Player takes damage | `PlayerTarget.ApplyDamage()` | `Damage` (trigger) | При HP>0 после удара (server) |
| Player death | `PlayerTarget.ApplyDamage()` | `Death` (trigger) | При HP=0 |
| NPC takes damage | `NpcTarget.ApplyDamage()` | `Damage` (trigger) | При HP>0 после удара (server) |
| NPC death | `NpcTarget.OnKilled()` | `Death` (trigger) | Существовал ранее |
| NPC movement | `NpcBrain.UpdateAnimator()` | `Speed`, `IsAttacking`, `IsGrounded` | Ежефреймово в Tick |

---

## 6. Все клипы

```
Assets/Kevin Iglesias/Human Animations/Animations/Male/
├── Idles/
│   └── HumanM@Idle01.fbx                  2.70s loop
├── Movement/
│   ├── Walk/       HumanM@Walk01_*.fbx    8 dir × 0.80s loop
│   ├── Run/        HumanM@Run01_*.fbx     8 dir × 0.60s loop
│   ├── Sprint/     HumanM@Sprint01_*.fbx  5 dir × 0.53s loop
│   ├── Jump/       HumanM@Jump01.fbx         1.53s no-loop
│   │              HumanM@Fall01.fbx          1.00s loop
│   │              HumanM@Jump01 - Land.fbx   0.60s no-loop
│   └── Turn/       HumanM@Turn01_Left/Right  1.00s loop
└── Combat/
    ├── HumanM@CombatDamage01.fbx          1.00s no-loop
    ├── HumanM@Death01.fbx                 0.73s no-loop
    └── 1H/
        ├── HumanM@Attack1H01_L.fbx        1.10s no-loop
        └── HumanM@CombatIdle1H01.fbx      1.33s loop
```

---

## 7. Питфоллы (P2 сессия)

| # | Проблема | Причина | Фикс |
|---|---|---|---|
| 1 | T-pose на префабе | `Animator.controller == null` | `SerializedObject.FindProperty("m_Controller")` |
| 2 | Два Animator: Visual + HumanM_Model | nested FBX импорт | `HumanM_Model/Animator.enabled = false` |
| 3 | `Animator.avatar == null` | не назначен при сборке | `SerializedObject.FindProperty("m_Avatar")` |
| 4 | Transition states fileID mismatch | controller создан не через API | Пересоздал через `CreateAnimatorControllerAtPath()` |
| 5 | Attack trigger работал только из CombatIdle | не было переходов из Idle/Walk/Run | Добавлены Attack → 1H из всех состояний |
| 6 | Player InCombat=false (нет WeaponMain) | кинжал не экипирован в слот | Attack работает из всех состояний, не только CombatIdle |
| 7 | **NPC провалились в Fall** | `IsGrounded=false` по умолчанию, code не выставлял | `NpcBrain.UpdateAnimator()` → `SetBool("IsGrounded", true)` |
| 8 | **Walk/Run/Sprint без анимации** | BlendTree создан без `AddObjectToAsset` → `m_Motion: {fileID: 0}` | `AssetDatabase.AddObjectToAsset(bt, controller)` |
| 9 | **Directional movement не работал** | `ChildMotion.position` = `Vector3(x,0,y)` → сериализовался как (x,0) | `Vector3(x, y, 0)` → позиция сохраняется как 2D (x,y) |

---

## 8. Префабы

| Префаб | Animator | Controller |
|---|---|---|
| `Assets/_Project/Prefabs/AI/Npc_Goblin.prefab` | `Visual` child | `NpcAnimator_Goblin.overrideController` |
| `Assets/_Project/Prefabs/NetworkPlayer.prefab` | `Visual_Model` child | `PlayerAnimation_Default.overrideController` |

---

## 9. Что осталось на потом

- Player death stub (`IsAlive()` всегда true — не умирает)
- Анимации навыков (spell cast, 2H, Polearm, Shield, Thrown)
- Разные анимации атаки для разного оружия (кинжал vs меч vs топор)
- StrafeRun BlendTree для боевого бокового движения
- Walk/Run directional для NPC (сейчас только Forward single-clip)
- Blend Tree 2D Freeform для более плавного перехода direction (сейчас SimpleDirectional)
