# 31_KEY_ANALYSIS_2026-07-21 — Фактический анализ перед P1 рефакторингом

**Дата:** 2026-07-21
**Ветка:** `refactor/key-subsystem-p1-2026-07-21` → merged to main
**Статус:** ✅ P1 выполнен (5 коммитов)
**Источник:** реальный код, не документация

---

## §1. Фактическая архитектура (по коду)

После 20+ итераций (v1–v20), система работает стабильно. Reflection удалён полностью (Phase C, v16).

```
Pickup (E)  ────────→  InventoryWorld.TryPickup
                          ├─ FindLostInstance (KeyRodInstanceWorld)
                          ├─ FindActiveKeyInstance (guard дубликата)
                          └─ CreateInstance (itemId, shipId=0, owner)

Drop (TAB) ─────────→  InventoryWorld.TryDrop
                          └─ TransferInstance + UpdateState(Lost)

F-key ──────────────→  MetaRequirementClientState.RequestCanUse
                          → MetaRequirementRegistry.RequestCanUseRpc
                          → ShipOwnershipRequirement.CanPlayerUse
                          → KeyRodInstanceWorld.IsOwnerOfShip

KeyRodInstanceWorld (static, server-only)
  ├─ _instancesById      Dict<int, KeyRodInstance>
  ├─ _primaryInstanceByShipId  Dict<ulong, int>
  ├─ _instancesByPlayer  Dict<ulong, List<int>>
  └─ event OnOwnershipChanged

ShipOwnershipRegistry (NetworkBehaviour, дублирует KeyRodInstanceWorld)
  ├─ NetworkList<OwnershipEntry>
  └─ → ShipTelemetryClientState (client cache)

ShipOwnershipRequirement (NetworkBehaviour на каждом ShipController)
  └─ → MetaRequirementRegistry (приоритетная проверка)
```

---

## §2. 5 найденных проблем

### 🔴 A: Obsolete-файлы — НЕ «пустые алиасы»

| Файл | Строк | Реальность |
|------|-------|-----------|
| `ShipKeyBinding.cs` | 30 | Пустой алиас → MetaRequirement ✅ |
| `ShipKeyServer.cs` | 210 | **НЕ алиас**: свой _bindings dict, RPC RequestCanBoardRpc, CanPlayerBoard |
| `ShipKeyClientState.cs` | 120 | **НЕ алиас**: OnCanBoardResponse → SubmitSwitchModeRpc |
| `ShipKeyToast.cs` | 167 | **НЕ алиас**: полноценный UI-компонент |

**Но:** NetworkPlayer F-key идёт через MetaRequirement*, а не через ShipKey* (v9). Эти файлы — мёртвый код.

### 🟡 B: KeyRodInstanceBinding + retry-loop

164 строки. Scene-placed компонент на PickupItem с retry-loop (15 попыток × 1 сек). Создаёт instance для ключа в мире.

### 🟡 C: ShipOwnershipRegistry дублирует KeyRodInstanceWorld

200+ строк NetworkBehaviour, который зеркалит ownership в NetworkList. Избыточный слой.

### 🟡 D: registeredShipId=0 при pickup без Lost-instance

Если persistence повреждён, drop→pickup создаёт instance без привязки к кораблю.

### 🟡 E: ShipTelemetryState без ownerClientId

Из-за этого нужен отдельный ShipOwnershipRegistry. Если добавить поле — registry не нужен.

---

## §3. План действий (этапы)

### Этап 1: Удаление Obsolete legacy (безопасно, ~1h)
1. grep по сценам на ShipKey* ссылки
2. Удалить компоненты из сцен (если есть)
3. Удалить 4 файла
4. compile-check → commit

### Этап 2: Fix registeredShipId=0 (безопасно, ~30min)
1. Пробросить shipId через FindActiveKeyInstance
2. compile-check → commit

### Этап 3: ShipOwnershipRegistry → KeyRodInstanceWorld (средний риск, ~3h)
1. Добавить ownerClientId в ShipTelemetryState
2. ShipTelemetryClientState читает ownerClientId из telemetry
3. Удалить ShipOwnershipRegistry
4. compile-check → Play Mode test → commit

### Этап 4: KeyRodInstanceBinding → ShipController (средний риск, ~2h)
1. ShipController.OnNetworkSpawn создаёт KeyRodInstance
2. Механизм спавна pickup-а
3. Удалить KeyRodInstanceBinding
4. compile-check → Play Mode test → commit

---

## §4. Что НЕ трогаем (работает идеально)

- KeyRodInstanceWorld — static facade, 0 reflection ✅
- KeyRodInstance — POCO, правильная структура ✅
- ShipOwnershipRequirement — чистая проверка ✅
- MetaRequirementRegistry — универсальный хаб ✅
- InventoryData._keySlots — сериализуется ✅
- InventoryWorld.TryPickup/TryDrop — прямые вызовы ✅

---

*Анализ проведён Aura, 2026-07-21.*
