# Ship Damage Subsystem — Интеграция и Ремонт

**Дата:** 2026-07-20
**Статус:** ✅ Реализовано

---

## 1. Настройка в редакторе

### 1.1 Создать ShipDamage.asset

1. В Project window: `Assets/_Project/Resources/` → ПКМ → Create → ProjectC → Ship → Damage Config
2. Назначить имя `ShipDamage` (имя файла должно совпадать — `Resources.Load("ShipDamage")`)
3. Настроить параметры (дефолты подходят для MVP)

### 1.2 Добавить ShipHull на корабли

На каждый `Ship_Root` (где уже есть `ShipController`, `Rigidbody`, `NetworkObject`):

1. Add Component → `ShipHull`
2. Компонент автоматически:
   - Найдёт `ShipController` через `GetComponent<ShipController>()`
   - Прочитает `ShipFlightClass` для инициализации maxHull
   - Зарегистрируется в `CombatServer` при `OnNetworkSpawn`

**Важно:** `ShipHull` должен быть на том же GameObject, что и `ShipController` (оба на `Ship_Root`).

### 1.3 Проверить ShipModuleServer

`ShipModuleServer` уже на корне корабля (рядом с `ShipController`). Новых компонентов не нужно — `RequestRepairHull` использует существующий `ShipController.Hull` геттер.

---

## 2. Интеграция с двигателем

### 2.1 Множитель скоростей

При `_hullBroken = true` в `ShipController.FixedUpdate`:

| Параметр | Формула | Эффект при 0 HP |
|----------|---------|-----------------|
| Thrust | `avgThrust * thrustForce * _moduleThrustMult * 0.1` | 10% тяги |
| Yaw | `avgYaw * yawForce * _moduleYawMult * 0.1` | 10% манёвренности |
| Pitch | `avgPitch * pitchForce * _modulePitchMult * 0.1` | 10% тангажа |
| Vertical | `avgVertical * verticalForce * _moduleLiftMult * 0.1` | 10% вертикали |
| MaxSpeed | `(maxSpeed + _moduleMaxSpeedMod) * 0.1` | 10% макс. скорости |

**Двигатель НЕ выключается.** AntiGravity продолжает работать. Корабль может медленно ковылять на 10% скоростей.

### 2.2 Топливо

Расход топлива не изменяется при поломке — корабль тратит топливо как обычно, но движется в 10 раз медленнее. Это означает, что сломанный корабль быстрее потратит топливо на то же расстояние.

### 2.3 Мезий-модули

Мезий-усиления ( MODULE_MEZIY_* ) не получают множителя `hullSpeedMult` — они добавляют torque/force отдельно в `ApplyMeziyEffects`. В MVP мезий работает на полную мощность даже при сломанном корпусе (post-MVP: гейтить).

---

## 3. Интеграция с грузом

### 3.1 Обнуление при поломке

При переходе `Operational → Broken`:

```csharp
private void WipeCargo()
{
    var cargo = TradeWorld.Instance.GetOrLoadCargo(NetworkObjectId, _resolvedCargoClass);
    if (cargo == null) return;
    if (cargo.Items.Count > 0)
    {
        cargo.Clear();                                    // CargoData._items.Clear()
        TradeWorld.Instance.NotifyCargoChanged(NetworkObjectId);  // event → RecalculateCargoPenalty
    }
}
```

- `cargo.Clear()` очищает `List<WarehouseEntry>` (CargoData.cs:148)
- `NotifyCargoChanged` запускает `OnCargoChanged` event
- `RecalculateCargoPenalty` пересчитывает `_serverCargoPenalty` → 1.0 (пустой трюм = нет штрафа)
- Сервер сохраняет пустой груз в репозиторий при следующем snapshot

### 3.2 Восстановление груза

После ремонта груз **не восстанавливается** — он потерян безвозвратно. Игрок должен заново загрузить товар.

---

## 4. Ремонт в доке

### 4.1 Flow

```
Игрок подходит к RepairManager NPC → жмёт E
  → RepairManagerWindow.Show(database)
  → Игрок выбирает корабль → кнопка «Ремонт корпуса»
  → ShipModuleServer.RequestRepairHull(keyInstanceId)
  → RequestRepairHullRpc [Server]:
      1. KeyRodInstanceWorld.IsOwnerOfInstance(clientId, keyInstanceId) — владение
      2. instance.registeredShipId == NetworkObjectId — ключ от этого корабля
      3. ShipController.IsDocked — корабль в доке
      4. hull.CurrentHull < hull.MaxHull — нужен ли ремонт
      5. TradeWorld.Repository.TryModifyCredits(clientId, -repairCostCredits) — списание
      6. hull.RepairFull() — HP → max
      7. ShipController.ClearHullBroken() — снять _hullBroken
      8. NotifyClientSuccess
```

### 4.2 Валидация (server-authoritative)

| Проверка | Что проверяет | Сообщение при отказе |
|----------|--------------|---------------------|
| Ключ | `KeyRodInstanceWorld.IsOwnerOfInstance` | «У вас нет ключа от этого корабля.» |
| Корабль | `instance.registeredShipId == NetworkObjectId` | «Ключ не подходит к этому кораблю.» |
| Док | `ShipController.IsDocked` | «Корабль не в доке. Ремонт возможен только в доке.» |
| Нужен ли | `hull.CurrentHull < hull.MaxHull` | «Корпус не нуждается в ремонте.» |
| Кредиты | `TryModifyCredits(-cost)` | «Недостаточно кредитов.» |

### 4.3 Стоимость

`repairCostCredits = 300` (по умолчанию в `ShipDamageConfig`). Полное восстановление за фиксированную цену. Post-MVP: пропорционально урону.

---

## 5. Интеграция с боевым движком

### 5.1 Регистрация как IDamageTarget

`ShipHull` self-register в `CombatServer` при `OnNetworkSpawn` (server-only):
```csharp
CombatServer.Instance.RegisterTarget(GetTargetId(), this);
```

После этого любой `IAttacker` (Player, NPC) может атаковать корабль через `CombatServer.RequestAttackRpc`.

### 5.2 Что происходит при атаке корабля

1. Атакующий → `CombatServer.ResolveAttack(attackerId, targetId=shipNetObjId, sourceId)`
2. `CombatServer` находит `ShipHull` в `_targets` dictionary
3. `DamageCalculator.Calculate(attacker, shipHull, source, rangePolicy)` — полная ERPR-формула
4. `shipHull.ApplyDamage(result, attackerId)` — HP падает
5. `AttackLandedTargetRpc` — broadcast всем клиентам
6. `DamageDealtEvent` — published
7. `EntityKilledEvent` — **НЕ published** (IsAlive() = true)

### 5.3 CombatServer и корабль

`CombatServer` работает с кораблем точно так же, как с NPC — через интерфейс `IDamageTarget`. Никаких специальных хаков. Единственное отличие: `IsAlive()` всегда `true`.

---

## 6. Изменённые файлы

| Файл | Изменение |
|------|-----------|
| `Assets/_Project/Scripts/Ship/Combat/ShipDamageConfig.cs` | **Новый** — SO конфиг |
| `Assets/_Project/Scripts/Ship/Combat/ShipHull.cs` | **Новый** — NetworkBehaviour, IDamageTarget |
| `Assets/_Project/Scripts/Player/ShipController.cs` | **Edit** — `_hull` кеш, `_hullBroken`, `OnHullChanged`, `WipeCargo`, `ClearHullBroken`, множители в FixedUpdate, collision → hull |
| `Assets/_Project/Scripts/Ship/ShipModuleServer.cs` | **Edit** — `RequestRepairHull` / `RequestRepairHullRpc` |
| `Assets/_Project/Resources/ShipDamage.asset` | **Создать в редакторе** — SO экземпляр |

---

## 7. Верификация (PIE)

1. На `Ship_Root` добавить `ShipHull` + создать `Resources/ShipDamage.asset`
2. Войти в PIE (host)
3. **Столкновение:** столкнуть корабль с препятствием на скорости → в консоли `[ShipHull] Collision damage: hull X → Y`. Лёгкий контакт (< 8 импульс) урона не наносит.
4. **Поломка:** свести hull к 0 (многократные столкновения) → `[ShipController] HULL BROKEN — speeds ×0.1, cargo wiped`. Корабль движется на 10% скоростей. Груз обнулён.
5. **Не уничтожение:** корабль не деспаунится, `IsAlive() = true`, `EntityKilledEvent` не публикуется.
6. **Ремонт:** пристыковаться в доке → `RepairManagerWindow` → кнопка ремонта → `[ShipModuleServer] Hull repaired` → hull = max, скорости восстановлены, `_hullBroken = false`.
7. **Клиент:** на не-хост клиенте `ShipHull.CurrentHull` реплицируется через `NetworkVariable` (сервер — истина).
8. **Боевое оружие:** атаковать корабль оружием через `CombatServer` → `[ShipHull] Combat damage: hull X → Y (dmg=N, attacker=...)`.

---

*Документация ведётся агентом Aura. 2026-07-20*
