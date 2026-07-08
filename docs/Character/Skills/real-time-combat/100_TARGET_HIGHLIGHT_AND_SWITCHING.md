# Подсвечивание цели, переключение Q/E и приоритет урона

> **Дата:** 2026-07-27  
> **Статус:** ✅ Реализовано (фазы 1-3)  
> **Контекст:** `docs/Character/Skills/real-time-combat/90_RANGED_AND_THROWABLES.md`

---

## 0. Результаты анализа текущего кода

### 0.1. Как сейчас работает поиск цели

```
ЛКМ → SkillInputService.Update() → IsBindingPressed → TryActivate(slot)
    → TargetFinder() (делегат из NetworkPlayer)
        → 1) raycast от character-forward (TargetingService.TryGetTarget)
        → 2) fallback: FindNearestNpcInRange(rangedMaxRange) — только для Bows/Crossbows
    → targetId передаётся в server.RequestSkillCastRpc / RequestAttackRpc
```

**Ключевой вывод:** targetId вычисляется **каждый кадр заново** при нажатии. Нет persistent targeting state.

### 0.2. Что есть в проекте

| Компонент | Файл | Роль |
|-----------|------|------|
| `TargetingService` | `Combat/Core/TargetingService.cs` | Raycast + AOE collection (static) |
| `SkillInputService` | `Skills/SkillInputService.cs` | Ввод → поиск цели → RPC |
| `CombatServer` | `Combat/Network/CombatServer.cs` | Server-side resolve: `ResolveSkillCast` / `ResolveAttack` |
| `NpcTarget` | `Combat/Implementations/NpcTarget.cs` | `IDamageTarget` для NPC, `NetworkVariable<int>` HP |
| `PlayerTarget` | `Combat/Implementations/PlayerTarget.cs` | `IDamageTarget` для игрока |
| `CombatClientState` | `Combat/Client/CombatClientState.cs` | Client-side event bus (AttackLanded, DamageDealt, EntityKilled) |
| `InputBindingsConfig` | `Input/InputBindingsConfig.cs` | SO с полным списком биндов |
| `NpcController` | `Quests/NpcController.cs` | В комментарии: `faction-based outline (зелёный/красный)` — не реализовано |

### 0.3. Чего НЕТ (и нужно создать)

- ❌ **Outline shader/material** — нет в проекте
- ❌ **Persistent target lock** — target вычисляется per-activation
- ❌ **Q/E target switching** — Q/E заняты ShipVerticalUp/Down (только в корабле)
- ❌ **Server-side obstruction check** — `ResolveSkillCast` доверяет `primaryTargetId` с клиента без проверки LOS
- ❌ **Highlight manager** — нет инфраструктуры подсвечивания

---

## 1. План реализации (3 фазы)

> **🔑 Ключевые аксиомы:**
> 1. **Q/E работают ТОЛЬКО в пешем режиме.** В корабле — Q/E управляют VerticalUp/Down без помех.
> 2. **Locked target — для ВСЕХ скиллов.** `TryActivate` (единый вход для ЛКМ/ПКМ/Ctrl+ЛКМ/Ctrl+ПКМ/Shift+ЛКМ/Shift+ПКМ) → locked target priority → obstruction check. Без исключений.

### Фаза 1: Outline Highlighting (подсвечивание текущей цели)

**Цель:** когда игрок наводится на цель (raycast) или навык авто-таргетирует кого-то — эта цель подсвечивается outline'ом на ~1 сек.

#### 1.1. Создать `TargetHighlightService`

- **Файл:** `Assets/_Project/Scripts/Combat/Client/TargetHighlightService.cs`
- **Тип:** client-only MonoBehaviour singleton
- **Жизненный цикл:** создаётся в `NetworkManagerController.CreateCombatClientState()` рядом с `CombatClientState`
- **Хранение:** `_currentHighlighted` — `GameObject` (или `NetworkObjectId`), на котором сейчас outline
- **Timeout:** `_highlightExpireTime` — float, после которого outline гаснет

#### 1.2. Outline shader/material

- **Шейдер:** URP Unlit + vertex extrusion по нормали + back-face rendering (классический inverted hull outline)
  - Или проще: использовать `Quick Outline` free asset подход — скрипт, который добавляет material override на SkinnedMeshRenderer/MeshRenderer
- **Материал:** `Assets/_Project/Resources/Materials/M_TargetOutline.mat`
  - Цвет: оранжевый/жёлтый `(1.0, 0.6, 0.0)`
  - Ширина: 2-3% от bounding box

#### 1.3. API `TargetHighlightService`

```csharp
public class TargetHighlightService : MonoBehaviour
{
    public static TargetHighlightService Instance { get; private set; }
    
    /// Выделить цель на duration секунд. Передаёт 0/NULL — снять подсветку.
    public void Highlight(GameObject target, float duration = 1.5f);
    
    /// Снять подсветку немедленно.
    public void Clear();
}
```

#### 1.4. Интеграция в `SkillInputService` и `TargetFinder`

- В `NetworkPlayer.InitializeSkillInputService()` — после вычисления `targetId` через `TargetFinder`:
  ```csharp
  // Нашли цель → подсветили
  if (targetId != 0) {
      var targetObj = FindGameObjectByTargetId(targetId);
      TargetHighlightService.Instance?.Highlight(targetObj, 1.5f);
  }
  ```
- Или через подписку на event: когда `TryActivate` находит target → дёргает `TargetHighlightService.Highlight`

**Рекомендация:** добавить вызов в `SkillInputService.TryActivate` в блоке 6.8 (после fallback-поиска), до RPC.

---

### Фаза 2: Persistent Target Lock + Q/E Switching

#### 2.1. Создать `TargetLockService`

- **Файл:** `Assets/_Project/Scripts/Combat/Client/TargetLockService.cs`
- **Тип:** client-only MonoBehaviour singleton
- **Семантика:** «замок» на цель, который живёт между кадрами

```csharp
public class TargetLockService : MonoBehaviour
{
    public static TargetLockService Instance { get; private set; }
    
    public ulong LockedTargetId { get; private set; }  // 0 = нет лока
    public GameObject LockedTargetObject { get; private set; }
    
    /// Захватить цель (по targetId). Если цель та же — снять лок (toggle).
    public void Lock(ulong targetId);
    
    /// Снять лок.
    public void Unlock();
    
    /// Переключить на предыдущую / следующую цель.
    public void CyclePrev();
    public void CycleNext();
    
    /// Событие: цель изменилась (для UI / outline).
    public event System.Action<GameObject, GameObject> OnTargetChanged; // (old, new)
}
```

#### 2.2. Алгоритм CyclePrev/CycleNext

1. Собрать всех живых `IDamageTarget` в радиусе `maxTargetCycleRange` (настраивается, default 50м)
2. Отсортировать по углу от `Camera.main.transform.forward` (слева-направо в screen-space)
3. Если есть `LockedTargetId` — найти его индекс в отсортированном списке, взять prev/next
4. Если нет лока — взять ближайшего к центру экрана (или первого в списке)
5. `Lock(newTargetId)` → `TargetHighlightService.Highlight(newTarget, Mathf.Infinity)` (пока замок активен)
6. Fire `OnTargetChanged`

#### 2.3. Добавить Q/E как TargetPrev/TargetNext в `InputBindingsConfig`

В `InputBindingsConfig.cs` добавить новые поля:

```csharp
[Header("Target Cycling (Q/E on foot)")]
[Tooltip("Клавиша для предыдущей цели. Только на суше.")]
public Key targetPrevKey = Key.Q;
[Tooltip("Клавиша для следующей цели. Только на суше.")]
public Key targetNextKey = Key.E;
```

**Конфликт с кораблём:** Q/E в корабле = ShipVerticalUp/Down. Решение: проверка `_ownerPlayer.IsInShip` — в корабле target cycling игнорируется.

#### 2.4. Polling Q/E в `SkillInputService.Update()`

Добавить в `SkillInputService.Update()` после блока опроса combatSkills:

```csharp
// Target cycling (Q/E, only on foot)
if (_ownerPlayer != null && !_ownerPlayer.IsInShip && TargetLockService.Instance != null)
{
    var kb = Keyboard.current;
    var cfg = InputBindingsRuntime.Instance?.Config;
    if (cfg != null)
    {
        if (kb != null && kb[cfg.targetPrevKey].wasPressedThisFrame)
            TargetLockService.Instance.CyclePrev();
        if (kb != null && kb[cfg.targetNextKey].wasPressedThisFrame)
            TargetLockService.Instance.CycleNext();
    }
}
```

#### 2.5. Интеграция LockedTarget в `TryActivate` — единая точка для ВСЕХ скиллов

**Критически важно:** `TryActivate` — это **единственный вход** для всех боевых действий:
- `Primary` = ЛКМ (обычная атака / basic strike)
- `Secondary` = ПКМ (блок / парирование)
- `Slot1` = Ctrl+ЛКМ (быстрый слот 1)
- `Slot2` = Ctrl+ПКМ (быстрый слот 2)
- `Slot3` = Shift+ЛКМ (быстрый слот 3)
- `Slot4` = Shift+ПКМ (быстрый слот 4)

Все они проходят через один метод → **locked target priority применяется автоматически ко всем скиллам без исключений**.

В `SkillInputService.TryActivate`, шаг 5 (поиск target):

```csharp
ulong targetId = 0UL;

// ▸ ПРИОРИТЕТ 1: Locked target (Q/E) — для ВСЕХ скиллов (Primary/Secondary/Slot1-4)
if (TargetLockService.Instance != null && TargetLockService.Instance.LockedTargetId != 0UL)
{
    var lockedTarget = TargetLockService.Instance.LockedTargetObject;
    if (lockedTarget != null)
    {
        var dt = lockedTarget.GetComponentInParent<IDamageTarget>();
        if (dt != null && dt.IsAlive())
        {
            targetId = TargetLockService.Instance.LockedTargetId;
        }
        else
        {
            TargetLockService.Instance.Unlock(); // цель умерла — снять лок
        }
    }
}

// ▸ ПРИОРИТЕТ 2: TargetFinder (raycast + fallback) — только если нет лока
if (targetId == 0UL && TargetFinder != null)
{
    try { targetId = TargetFinder(); } catch ...
}
```

**Итог:** выбрал цель через Q/E → любой скилл (ЛКМ/ПКМ/Ctrl+ЛКМ/Shift+ПКМ/...) идёт в locked target.

---

### 2.6. Q/E: только пеший режим, кораблю не мешают

**Гарантия:** Q/E polling спрятан за двумя проверками:

```csharp
// В SkillInputService.Update():
if (_ownerPlayer != null && !_ownerPlayer.IsInShip && TargetLockService.Instance != null)
```

| Режим | Q | E |
|-------|---|---|
| **Пеший** | `TargetLockService.CyclePrev()` — выбор предыдущей цели | `TargetLockService.CycleNext()` — выбор следующей цели |
| **Корабль** | `ShipVerticalUp` — корабль вверх (без изменений) | `ShipVerticalDown` — корабль вниз (без изменений) |

Никакого конфликта: `IsInShip == true` → блок target-cycling полностью пропускается, корабельные Q/E работают как раньше.

---
=======
#### 2.5. Интеграция LockedTarget в `TryActivate`

В `SkillInputService.TryActivate`, шаг 5 (поиск target):

```csharp
ulong targetId = 0UL;

// Приоритет 1: Locked target (если есть)
if (TargetLockService.Instance != null && TargetLockService.Instance.LockedTargetId != 0UL)
{
    var lockedTarget = TargetLockService.Instance.LockedTargetObject;
    if (lockedTarget != null)
    {
        var dt = lockedTarget.GetComponentInParent<IDamageTarget>();
        if (dt != null && dt.IsAlive())
        {
            targetId = TargetLockService.Instance.LockedTargetId;
        }
        else
        {
            TargetLockService.Instance.Unlock(); // цель умерла
        }
    }
}

// Приоритет 2: TargetFinder (raycast + fallback) — только если нет лока
if (targetId == 0UL && TargetFinder != null)
{
    try { targetId = TargetFinder(); } catch ...
}
```

---

### Фаза 3: Damage Prioritization с Obstruction Check

**Полный flow для любого скилла (ЛКМ/ПКМ/Ctrl+Shift+ЛКМ/ПКМ) при активном локе:**

```
Игрок жмёт скилл (любой слот)
  → SkillInputService.TryActivate(slot)
    → targetId = LockedTargetId (если есть лок Q/E)
    → server.RequestSkillCastRpc(skillId, targetId, sourceId)
      → CombatServer.ResolveSkillCast
        → Server-side raycast attacker → target
          ├─ попал в target → OK, бьём target
          ├─ попал в другой IDamageTarget → REDIRECT, бьём obstruction
          └─ попал в стену/пол → MISS (LineOfSightBlocked)
```

#### 3.1. Server-side raycast в `CombatServer.ResolveSkillCast`

Когда `primaryTargetId != 0` (клиент прислал locked target):

```csharp
if (primaryTargetId != 0 && _targets.TryGetValue(primaryTargetId, out var preferredTarget))
{
    // Server-side raycast: attacker → preferredTarget
    Vector3 origin = attacker.GetPosition() + Vector3.up * 1.2f;
    Vector3 toTarget = preferredTarget.GetPosition() - origin;
    float dist = toTarget.magnitude;
    
    if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
    {
        var obstruction = hit.collider.GetComponentInParent<IDamageTarget>();
        if (obstruction != null && obstruction.GetTargetId() != primaryTargetId)
        {
            // Нашли препятствие — бьём его вместо preferredTarget
            primaryTargetId = obstruction.GetTargetId();
            Debug.Log($"[CombatServer/Obstruction] skill='{skillId}': target blocked by {obstruction.GetDisplayName()} ({primaryTargetId}), redirecting damage");
        }
        // else: raycast попал в preferredTarget — OK
    }
}
```

#### 3.2. AOE Obstruction: per-target raycast

В цикле `for (int i = 0; i < results.Count; i++)` добавить перед damage calculation:

```csharp
// Obstruction check: если между attacker и target есть другой IDamageTarget — бьём препятствие
if (!useTargetPoint)
{
    Vector3 toTarget = target.GetPosition() - attacker.GetPosition();
    if (Physics.Raycast(attacker.GetPosition(), toTarget.normalized, out RaycastHit hit, 
        toTarget.magnitude, ~0, QueryTriggerInteraction.Ignore))
    {
        var obstruction = hit.collider.GetComponentInParent<IDamageTarget>();
        if (obstruction != null && obstruction.GetTargetId() != target.GetTargetId())
        {
            // Redirect damage to obstruction
            target = obstruction;
        }
    }
}
```

#### 3.3. Поведение при заблокированном выстреле

- Если raycast от attacker к locked-target попадает в **non-damageable** collider (стена, земля) → **miss** (skill cast fails silently, send error "LineOfSightBlocked")
- Если попадает в **другого IDamageTarget** → **damage redirected** (бьём obstruction)

---

## 2. Порядок реализации (6 шагов)

| # | Шаг | Файлы | Оценка |
|---|-----|-------|--------|
| 1 | **Outline shader + material** | Новый `M_TargetOutline.mat` + `TargetOutline.shader` | 0.5h |
| 2 | **`TargetHighlightService`** | Новый `Combat/Client/TargetHighlightService.cs` | 1h |
| 3 | **Интеграция highlight в `TryActivate`** | `SkillInputService.cs`, `NetworkPlayer.cs` | 0.5h |
| 4 | **`TargetLockService` + Q/E polling** | Новый `Combat/Client/TargetLockService.cs`, правка `SkillInputService.cs`, `InputBindingsConfig.cs` | 2h |
| 5 | **Server-side obstruction check** | `CombatServer.cs` — `ResolveSkillCast` | 1h |
| 6 | **Тестирование + документирование** | Play Mode, `90_RANGED_AND_THROWABLES.md` update | 0.5h |

**Итого: ~5.5 часов**

---

## 3. Риски и открытые вопросы

| # | Вопрос | Ответ / План |
|---|--------|-------------|
| R1 | Outline на SkinnedMeshRenderer (анимированные NPC) | Inverted hull outline работает через отдельный материал в `materials[]` array — нужно учитывать анимацию. Альтернатива: пост-процесс outline (URP Renderer Feature) — проще, но выделяет всё на слое. **Решение:** inverted hull per-object для точности. |
| R2 | Конфликт Q/E: корабль vs target switching | `onlyOnFoot` флаг. В `SkillInputService.Update` проверяем `_ownerPlayer.IsInShip` — в корабле target cycling игнорируется. |
| R3 | Locked target умирает во время каста | `TargetLockService` проверяет `IsAlive()` перед использованием. Если мёртв → Unlock + авто-переход на CycleNext. |
| R4 | Outline на игроках (PvP) | Outline должен работать и на `PlayerTarget`. Проверить что `PlayerTarget` GameObject имеет `SkinnedMeshRenderer`. |
| R5 | Производительность CyclePrev/CycleNext | `FindObjectsByType<NpcTarget>()` каждый раз — дорого. **Решение:** кэшировать список в `TargetLockService`, обновлять раз в 0.3 сек. |
| R6 | `TargetLockService` должен знать обо всех IDamageTarget | Использовать `CombatServer._targets` registry? Нет — он server-side. **Решение:** client-side кэш через `FindObjectsByType<NpcTarget>()` + `FindObjectsByType<PlayerTarget>()` с интервальным обновлением. |

---

## 4. Следующие шаги после реализации

- ❌ **Target UI widget** — иконка/health bar над locked target
- ❌ **Aim-assist** на locked target (доворачивать камеру/персонажа)
- ❌ **Multi-target lock** (для AoE скиллов — показывать зону поражения)
- ❌ **Звук** lock-on / switch target
