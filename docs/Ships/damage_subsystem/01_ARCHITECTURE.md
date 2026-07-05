# Ship Damage Subsystem — Архитектура

**Дата:** 2026-07-20
**Статус:** ✅ Реализовано

---

## 1. Ключевые компоненты

| Компонент | Тип | Назначение |
|-----------|-----|-----------|
| `ShipDamageConfig` | ScriptableObject | Параметры: maxHull по классу, armorHull, формула столкновений, стоимость ремонта |
| `ShipHull` | NetworkBehaviour, `IDamageTarget` | HP корпуса: `NetworkVariable<int>`, `ApplyDamage`, `ApplyCollisionDamage`, `OnHullChanged` |
| `ShipController` | NetworkBehaviour (edit) | Кеш `_hull`, подписка `OnHullChanged`, флаг `_hullBroken`, `WipeCargo()`, множители в `FixedUpdate` |
| `ShipModuleServer` | NetworkBehaviour (edit) | `RequestRepairHullRpc` — валидация + ремонт за кредиты |

---

## 2. Структура файлов

```
Assets/_Project/Scripts/Ship/Combat/
├── ShipDamageConfig.cs     # SO: параметры системы повреждений
└── ShipHull.cs             # NetworkBehaviour: IDamageTarget, HP, события

Assets/_Project/Scripts/Player/
└── ShipController.cs       # (edit) интеграция: collision → hull, _hullBroken, множители

Assets/_Project/Scripts/Ship/
└── ShipModuleServer.cs     # (edit) RequestRepairHullRpc

Assets/_Project/Resources/
└── ShipDamage.asset        # (create в редакторе) SO экземпляр конфига
```

---

## 3. ShipHull — NetworkBehaviour

### 3.1 Network Variables

```csharp
private readonly NetworkVariable<int> _hull = new NetworkVariable<int>(
    100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

private readonly NetworkVariable<int> _maxHull = new NetworkVariable<int>(
    100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
```

- **Сервер** пишет (единственный авторитативный источник)
- **Все клиенты** читают → HUD/UI могут читать `CurrentHull` / `MaxHull`

### 3.2 Событие

```csharp
public event Action<int, int, HullState> OnHullChanged;
// (newHull, deltaHp, state)
```

Подписчик: `ShipController.OnHullChanged` (server-only).

### 3.3 Регистрация в CombatServer

```csharp
// OnNetworkSpawn (server-only):
CombatServer.Instance.RegisterTarget(GetTargetId(), this);

// OnNetworkDespawn (server-only):
CombatServer.Instance.UnregisterTarget(GetTargetId());
```

`GetTargetId()` = `NetworkObject.NetworkObjectId` (как `NpcTarget`).

### 3.4 IDamageTarget — реализация

| Метод | Реализация |
|-------|-----------|
| `GetPosition()` | `transform.position` |
| `GetCurrentHp()` | `_hull.Value` |
| `GetMaxHp()` | `_maxHull.Value` |
| `GetArmorDefense()` | `ShipDamageConfig.Default.armorHull` |
| `ApplyDamage(result, attackerId)` | `_hull.Value -= result.finalDamage` (server-only) |
| `IsAlive()` | **`true`** (всегда — корабль не «труп») |
| `IsPlayer()` | `false` |
| `GetDisplayName()` | `ShipController.CustomDisplayName` или `"Ship {id}"` |
| `GetTargetId()` | `NetworkObject.NetworkObjectId` |

### 3.5 ApplyCollisionDamage

```csharp
public void ApplyCollisionDamage(float impactEnergy)
{
    if (!IsServer) return;
    if (_hull.Value <= 0) return;

    var cfg = ShipDamageConfig.Default;
    if (impactEnergy < cfg.collisionEnergyThreshold) return;

    int damage = Mathf.FloorToInt(
        (impactEnergy - cfg.collisionEnergyThreshold)
        * cfg.collisionDamageCoefficient);
    damage = Mathf.Min(damage, cfg.collisionDamageCap);
    if (damage <= 0) return;

    // Применить урон, вызвать OnHullChanged
}
```

### 3.6 RepairFull

```csharp
public void RepairFull()
{
    if (!IsServer) return;
    _hull.Value = _maxHull.Value;
    OnHullChanged?.Invoke(_hull.Value, delta, HullState.Operational);
}
```

Вызывается из `ShipModuleServer.RequestRepairHullRpc`.

---

## 4. ShipController — интеграция

### 4.1 Кеширование и подписка

```csharp
// Поля:
private ShipHull _hull;
private bool _hullBroken = false;

// OnNetworkSpawn (server):
_hull = GetComponent<ShipHull>();
if (_hull != null)
    _hull.OnHullChanged += OnHullChanged;
```

### 4.2 OnCollisionEnter — урон корпусу

```csharp
// После TryDamageCargo (урон грузу):
if (_hull != null)
{
    _hull.ApplyCollisionDamage(energy);
}
```

### 4.3 OnHullChanged — реакция на поломку

```csharp
private void OnHullChanged(int newHull, int deltaHp, HullState state)
{
    if (!IsServer) return;

    if (state == HullState.Broken && !_hullBroken)
    {
        _hullBroken = true;
        WipeCargo();  // CargoData.Clear() + NotifyCargoChanged
    }
    else if (state == HullState.Operational && _hullBroken)
    {
        _hullBroken = false;
    }
}
```

### 4.4 Множитель скоростей в FixedUpdate

```csharp
float hullSpeedMult = _hullBroken ? ShipDamageConfig.Default.brokenSpeedMultiplier : 1f;

// Применяется к:
//   targetThrust    = avgThrust * thrustForce * _moduleThrustMult * hullSpeedMult
//   targetYawRate   = avgYaw * yawForce * _moduleYawMult * hullSpeedMult
//   targetPitchRate = avgPitch * pitchForce * _modulePitchMult * hullSpeedMult
//   targetLift      = avgVertical * verticalForce * _moduleLiftMult * hullSpeedMult
//   effectiveMaxSpeed = (maxSpeed + _moduleMaxSpeedMod) * hullSpeedMult
```

### 4.5 WipeCargo

```csharp
private void WipeCargo()
{
    var cargo = TradeWorld.Instance.GetOrLoadCargo(NetworkObjectId, _resolvedCargoClass);
    if (cargo == null) return;
    if (cargo.Items.Count > 0)
    {
        cargo.Clear();
        TradeWorld.Instance.NotifyCargoChanged(NetworkObjectId);
    }
}
```

### 4.6 ClearHullBroken

```csharp
public void ClearHullBroken()
{
    if (!IsServer) return;
    _hullBroken = false;
}
```

Вызывается из `ShipModuleServer.RequestRepairHullRpc` после `hull.RepairFull()`.

---

## 5. Поток данных

### 5.1 Столкновение

```
Physics → OnCollisionEnter (server)
  → ShipHull.ApplyCollisionDamage(energy)
    → _hull.Value -= damage
    → OnHullChanged?.Invoke(newHull, -delta, state)
      → ShipController.OnHullChanged
        → if Broken: _hullBroken = true, WipeCargo()
        → FixedUpdate: hullSpeedMult = 0.1
```

### 5.2 Боевое оружие

```
Player/NPC → CombatServer.RequestAttackRpc
  → CombatServer.ResolveAttack
    → DamageCalculator.Calculate(attacker, target=ShipHull, source, rangePolicy)
    → target.ApplyDamage(result, attackerId)  // ShipHull.ApplyDamage
      → _hull.Value -= result.finalDamage
      → OnHullChanged?.Invoke(...)
        → ShipController.OnHullChanged
          → if Broken: _hullBroken = true, WipeCargo()

// CombatServer НЕ публикует EntityKilledEvent (IsAlive() = true)
// CombatServer публикует AttackLandedEvent + DamageDealtEvent (нормально)
```

### 5.3 Ремонт

```
RepairManagerWindow → ShipModuleServer.RequestRepairHull(keyInstanceId)
  → RequestRepairHullRpc [Server]:
      1. KeyRodInstanceWorld.IsOwnerOfInstance — владение ключом
      2. instance.registeredShipId == NetworkObjectId — ключ от этого корабля
      3. ShipController.IsDocked — корабль в доке
      4. hull.CurrentHull < hull.MaxHull — нужен ремонт
      5. TradeWorld.Repository.TryModifyCredits(-cost) — списание
      6. hull.RepairFull() — HP → max
      7. ShipController.ClearHullBroken() — снять флаг
      8. NotifyClientSuccess
```

---

## 6. ShipDamageConfig — SO

### 6.1 Default Loader

```csharp
public static ShipDamageConfig Default
{
    get
    {
        if (_default != null) return _default;
        _default = Resources.Load<ShipDamageConfig>("ShipDamage");
        if (_default == null)
        {
            Debug.LogWarning("[ShipDamageConfig] Asset not found, using hardcoded defaults.");
            _default = CreateInstance<ShipDamageConfig>();
        }
        return _default;
    }
}
```

Паттерн: `Resources/ShipDamage.asset` → fallback в памяти. Создать через `Assets > Create > ProjectC > Ship > Damage Config`.

### 6.2 Поля инспектора

| Поле | Тип | Default | Описание |
|------|-----|---------|----------|
| `maxHullLight` | int | 100 | HP лёгкого корабля |
| `maxHullMedium` | int | 200 | HP среднего корабля |
| `maxHullHeavy` | int | 400 | HP тяжёлого корабля |
| `maxHullHeavyII` | int | 600 | HP тяжёлого II |
| `armorHull` | int | 5 | Броня корпуса |
| `collisionEnergyThreshold` | float | 8 | Мин. импульс для урона |
| `collisionDamageCoefficient` | float | 0.5 | energy → HP |
| `collisionDamageCap` | int | 50 | Макс. урон за удар |
| `brokenSpeedMultiplier` | float | 0.1 | Множитель скоростей при 0 HP |
| `repairCostCredits` | int | 300 | Стоимость ремонта |
| `verboseLogging` | bool | true | Подробные логи |

---

## 7. Безопасность

- **Server-authoritative**: все мутации `_hull.Value` только на сервере (`NetworkVariableWritePermission.Server`)
- **CombatServer**: валидация через `IAttacker`/`IDamageTarget` — корабль неотличим от NPC/Player как цель
- **Ремонт**: валидация ключа (`KeyRodInstanceWorld.IsOwnerOfInstance`) + док (`IsDocked`) + кредиты (`TryModifyCredits`)
- **IsAlive() = true**: предотвращает `EntityKilledEvent` и деспаун корабля боевым движком

---

*Документация ведётся агентом Aura. 2026-07-20*
