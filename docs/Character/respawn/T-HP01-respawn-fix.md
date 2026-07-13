# T-HP01: Death → Respawn Fix — Итерации и корневая причина

**Дата:** 2026-07-16  
**Файлы:** `PlayerTarget.cs`, `PlayerRespawnTracker.cs`, `CombatServer.cs`

---

## Симптом

Игрок умирал (HP=0), но респавн не срабатывал:
- Нет логов `PlayerRespawnTracker`
- `TriggerDeathRespawn` abort'ился с `IsServer=False`
- Персонаж оставался мёртвым, HP=0, мобы игнорировали

---

## Итерация 1: Coroutine → Timer

**Гипотеза:** `StartCoroutine` на `NetworkBehaviour` теряет контекст.

**Сделано:** заменил `StartCoroutine(DeathRespawnCoroutine)` на timer в `Update()`.

**Результат:** FAIL. `IsServer` всё равно `False` в `Update()`.

---

## Итерация 2: IsServer → IsSpawned

**Гипотеза:** `IsServer=False` потому что NetworkObject не заспавнился (warning "NetworkVariable written before spawn").

**Сделано:** заменил `if (!IsServer)` на `if (!IsSpawned)` с retry-логикой.

**Результат:** FAIL. `IsSpawned=False` **навсегда** → бесконечный retry-цикл.
Оказалось, хост-игрок имеет `OwnerClientId=0`, и проверки `id==0` ломали его.

---

## Итерация 3: IsSpawned без clientId=0

**Гипотеза:** Хост (clientId=0) ≠ ghost (IsSpawned=False).

**Сделано:** убрал проверки `clientId==0`, оставил только `IsSpawned`.

**Результат:** FAIL. Хост-игрок имеет `IsSpawned=False` (баг NGO 2.x timing) → урон игнорируется.

---

## Итерация 4 (FINAL): NetworkManager.Singleton.IsServer

**Гипотеза:** `NetworkBehaviour.IsServer` и `IsSpawned` оба ненадёжны в определённых timing-условиях NGO 2.x. Единственный стабильный источник правды — `NetworkManager.Singleton.IsServer`.

**Сделано:**

| Метод | Файл | Было | Стало |
|-------|------|------|-------|
| `TriggerDeathRespawn` | `PlayerTarget.cs` | `if (!IsServer)` | `NetworkManager.Singleton.IsServer` |
| `RespawnWithHpRestore` | `PlayerRespawnTracker.cs` | `if (!IsServer)` | `NetworkManager.Singleton.IsServer` |
| `SetHp` | `PlayerTarget.cs` | `if (!IsServer)` | `NetworkManager.Singleton.IsServer` |
| `ApplyDamage` | `PlayerTarget.cs` | `IsSpawned` guard | убран |
| `RecoverExistingEntities` | `CombatServer.cs` | `IsSpawned` guard | убран |
| `DeathRespawnCoroutine` | `PlayerTarget.cs` | `StartCoroutine` | timer в `Update()` |

**Результат:** ✅ Респавн работает.

---

## Корневая причина

**`NetworkBehaviour.IsServer` возвращает `false` в трёх сценариях:**

1. **Coroutine continuation:** `StartCoroutine` + `WaitForSeconds` → после await'а сбрасывается состояние `NetworkBehaviour`
2. **Timer в Update():** тот же эффект, если GameObject не полностью инициализирован
3. **Spawn timing:** NPC может атаковать до завершения `OnNetworkSpawn` → `IsServer` ещё не установлен

**`NetworkBehaviour.IsSpawned` тоже ненадёжен:**
- Для хост-игрока (OwnerClientId=0) возвращает `False` из-за timing race в NGO 2.x

**Правильное решение:**
```csharp
bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
```
Статический singleton, не зависит от состояния конкретного `NetworkBehaviour` или `NetworkObject`.
