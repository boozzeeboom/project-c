# Ship Collision Analysis — Ship_Light_root vs Ship_Medium

> **Дата:** 2026-07-17 | **Статус:** В процессе

---

## 1. КЛЮЧЕВОЕ РАЗЛИЧИЕ — СЛОЙ

| Параметр | Ship_Light_root/Platform | Ship_Medium/platform |
|----------|--------------------------|----------------------|
| **Layer** | **0 — Default** | **6 — ShipDeck** |
| BoxCollider | isTrigger=false, enabled=true, size=(1,1,1) | isTrigger=false, enabled=true, size=(1,1,1) |
| Rigidbody на корне | detectCollisions=true | detectCollisions=true (в префабе) |
| NpcShipController | НЕТ | ЕСТЬ |

**Physics collision matrix:** `Default(0) ↔ ShipDeck(6)` = ENABLED в обе стороны.
**NetworkPlayer:** Layer 0 (Default), CharacterController.

---

## 2. Потенциальные причины

### 2.1 `rb.detectCollisions = false` в SetMode(Lifting)

`NpcShipController.SetMode(NavMode.Lifting)` (строка 433):
```csharp
rb.detectCollisions = false;  // отключить collision detection при взлёте
```

Это каскадно отключает ВСЕ коллайдеры на корабле (включая platform).
Восстанавливается при выходе из Lifting (строка 440):
```csharp
if (old == NavMode.Lifting && m != NavMode.Lifting)
    rb.detectCollisions = true;
```

**Но это только в рантайме на сервере.** На клиенте `enabled = false`.

### 2.2 `rb.isKinematic = true` в Docked

`NavTick` в режиме Docked (строка 366):
```csharp
if (!rb.isKinematic) rb.isKinematic = true;
```

CharacterController должен сталкиваться с kinematic Rigidbody, но поведение может зависеть от версии Unity.

### 2.3 Слой ShipDeck

Ship_Medium/platform на слое 6 (ShipDeck), Ship_Light_root/Platform на слое 0 (Default).
Physics collision matrix разрешает Default↔ShipDeck — но нужна проверка в рантайме.

---

## 3. Нужно проверить

1. **В каком режиме тестируете?** (Editor/Play Mode, Host/Client?)
2. **Корабль статичен или движется?** (Docked на паде?)
3. **Консоль:** есть ли `[NpcShipController] NavMode ... → ...` или `[ShipDeckNav]` логи?

---

## 4. Структурные различия префабов

| Компонент | Ship_Light_root | Ship_Medium |
|-----------|----------------|-------------|
| Root MeshFilter+MeshRenderer | ЕСТЬ | НЕТ |
| Root BoxCollider | ЕСТЬ (disabled) | НЕТ |
| Root ShipRootReference | ЕСТЬ | НЕТ |
| Root layer | Default (0) | Default (0) |
| Platform layer | **Default (0)** | **ShipDeck (6)** |
| NpcShipController | НЕТ | ЕСТЬ |
| NpcProximityZone | НЕТ | ЕСТЬ |
| ShipDeckNav | НЕТ | ЕСТЬ |
| DeckNavSurface (NavMeshSurface) | НЕТ | ЕСТЬ |
| PilotSeat BoxCollider | isTrigger=false | **isTrigger=true** |
