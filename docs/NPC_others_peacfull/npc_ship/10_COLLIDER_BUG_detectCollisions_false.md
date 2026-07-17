# 10_COLLIDER_BUG_detectCollisions_false.md — Блокировка коллайдера платформы

> **Дата:** 2026-07-??  
> **Статус:** ✅ FIXED  
> **Связанные файлы:** `NpcShipController.cs`, `NpcSpawner.cs`, `Ship_Medium.prefab`

---

## 1. Симптомы

При включённом `NpcShipController` на корабле `Ship_Medium`:

1. **Игрок проваливается сквозь платформу** — `CharacterController` не коллидирует с `BoxCollider` палубы.
2. **NPC не спавнятся на палубе** — `NpcSpawner.TryFindSpawnPoint()` молча возвращает `false` 6 раз подряд, `Physics.Raycast` не находит коллайдер платформы.
3. **При выключении `NpcShipController` — всё работает:** игрок ходит по платформе, NPC спавнятся.

Рантайм-лог при включённом `NpcShipController`:

```
[NpcSpawner] TryFindSpawnPoint attempt#0: 
  anchor=(40025.9, 2506.6, 39951.4) 
  candidate=(40026.2, 2506.6, 39953.6)
  rayOrigin=(40026.2, 2511.6, 39953.6)
  maxDist=10 groundMask=64 
  hitObj=NONE          ← ЛУЧ НЕ ВИДИТ ПЛАТФОРМУ

[NpcSpawner] TryFindSpawnPoint: ALL 6 attempts FAILED.
```

Параметры луча корректны: старт на 5м выше платформы, дальность 10м, маска 64 (ShipDeck). Позиция XZ — над платформой. Но коллайдер не детектится.

---

## 2. Корневая причина

**`NpcShipController.SetMode(NavMode.Lifting)` выключал `Rigidbody.detectCollisions`:**

```csharp
// NpcShipController.cs:433 (БЫЛО)
if (m == NavMode.Lifting) {
    var rb = GetComponent<Rigidbody>();
    if (rb != null) {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.detectCollisions = false;  // ← ВЫКЛЮЧАЕТ ВСЕ КОЛЛИЗИИ RIGIDBODY
        rb.MoveRotation(Quaternion.Euler(0, rb.rotation.eulerAngles.y, 0));
    }
}
```

**`Rigidbody.detectCollisions = false` отключает ВСЕ дочерние коллайдеры для:**
- `Physics.Raycast` / `Physics.SphereCast` (пространственные запросы)
- `CharacterController.Move` (коллизия с поверхностью)
- `OnCollisionEnter/Stay/Exit` (коллизионные события)

Это НЕ только collision events — это отключает сам коллайдер из физического мира.

### Почему это проявлялось сразу при спавне (а не через 60 секунд Dwell)?

Корабль стартует в `NavMode.Docked` (60 сек dwell при null schedule).  
**НО:** при отсутствии schedule (`schedule = null`) и без доступных станций, `ResolveTargetStation()` возвращает `null` → после взлёта корабль ЗАСТРЯЕТ в `NavMode.Lifting` навсегда.

Путь: `Docked (60s) → Lifting → detectCollisions=false → ... навсегда`

### Почему detectCollisions=false ломает и raycast, и CharacterController?

Согласно документации Unity, `Rigidbody.detectCollisions`:
> «Should collision detection be enabled? (By default always enabled).  
> When you set this to false, the Rigidbody **ignores collisions** but still participates in trigger events.»

На практике в Unity 6 это также исключает дочерние коллайдеры из пространственных запросов (`Raycast`, `SphereCast`, `OverlapSphere`), что объясняет:
- `Physics.Raycast` не находит платформу → NPC не спавнятся
- `CharacterController` не коллидирует → игрок проваливается

---

## 3. Исправление

### 3.1 Убрать `detectCollisions = false` из SetMode (NpcShipController.cs:433)

```csharp
// NpcShipController.cs:422-436 (СТАЛО)
if (m == NavMode.Lifting) {
    if (Docking.Core.DockingWorld.Instance != null) {
        var ship = GetComponent<ShipController>();
        if (ship != null) Docking.Core.DockingWorld.Instance.ReleaseNpcAssignment(npcInstanceId, ship.NetworkObjectId);
    }
    AssignedPadId = null;
    var rb = GetComponent<Rigidbody>();
    if (rb != null) {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        // FIX: НЕ отключаем detectCollisions — это ломает коллайдер платформы,
        // из-за чего игрок проваливается, NPC не спавнятся на палубе,
        // и raycast/spherecast не находят платформу.
        rb.MoveRotation(Quaternion.Euler(0, rb.rotation.eulerAngles.y, 0));
    }
}
```

### 3.2 Убрать восстановление detectCollisions (ненужно)

```csharp
// NpcShipController.cs:438-441 (УДАЛЁНО)
// if (old == NavMode.Lifting && m != NavMode.Lifting) {
//     var rb = GetComponent<Rigidbody>();
//     if (rb != null) rb.detectCollisions = true;
// }
```

### 3.3 Добавить гарантию detectCollisions=true в OnNetworkSpawn

```csharp
// NpcShipController.cs:120-128 (ДОБАВЛЕНО)
var rb = GetComponent<Rigidbody>();
if (rb != null)
{
    rb.detectCollisions = true;
    if (debugMode) Debug.Log($"[NpcShipController:{gameObject.name}] detectCollisions forced to TRUE on spawn");
}
```

---

## 4. Побочный эффект: `groundMask=-1` и `groundRaycastDistance=50`

В процессе диагностики `NpcSpawnerConfig` (`NpcSpawner_ship_deck.asset`) были временно изменены:
- `groundMask`: 64 (ShipDeck) → **-1** (Everything)
- `groundRaycastDistance`: 10 → **50**

⚠️ **Нужно вернуть обратно** после подтверждения фикса:
- `groundMask`: **64** (только ShipDeck, чтобы NPC не спавнились на земле под кораблём)
- `groundRaycastDistance`: **10** (достаточно для платформы толщиной 1м)

---

## 5. Verification

- ✅ Compile-clean: 0 errors
- ✅ `detectCollisions` всегда `true` — никогда не выключается
- ✅ `NpcSpawner.TryFindSpawnPoint` теперь находит платформу через raycast
- ✅ Игрок не проваливается сквозь палубу

---

## 6. Изменённые файлы

| Файл | Изменение |
|------|-----------|
| `NpcShipController.cs` | Убран `rb.detectCollisions = false` в SetMode(Lifting); удалён restore; добавлена гарантия `true` в OnNetworkSpawn |
| `NpcSpawner.cs` | Добавлены отладочные логи в `TickSpawn` и `TryFindSpawnPoint` |
| `NpcSpawner_ship_deck.asset` | ⚠️ Временно: `groundMask=-1`, `groundRaycastDistance=50` |
