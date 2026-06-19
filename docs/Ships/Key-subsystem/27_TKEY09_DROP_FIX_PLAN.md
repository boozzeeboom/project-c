# T-KEY-09: Drop key не синхронизирует ownership — план

**Дата:** 2026-06-19 | **Статус:** 📋 Planned (не реализован) | **Тикет:** T-KEY-09

---

## §1. Проблема

При drop'е Key-предмета из инвентаря, `KeyRodInstanceWorld` остаётся с `ownerPlayerId = playerId, state = Active`. Игрок **может продолжать управлять** кораблём, потому что `IsOwnerOfShip` возвращает `true` (state=Active, owner=playerId).

Только удаление `KeyRodInstances.json` помогает — после рестарта KeyRodInstanceWorld пуст, и IsOwnerOfShip → false.

---

## §2. Root cause (4 проблемы найдены в Play Mode анализе)

| # | Bug | Описание | Файл:строка |
|---|---|---|---|
| 1 | **Drop не синхронизирует ownership** | `TryDrop` вызывает `TransferInstance` только если `droppedKeyInstanceId > 0`. Если `instanceId=0` (pickup без binding) — instance остаётся у player'а | `InventoryWorld.TryDrop:465` |
| 2 | **Параллельные списки _keyIds/_keySlots** | `GetIdsForType(Key)` возвращает `_keyIds`, но `instanceId` в `_keySlots` с тем же индексом. Если индексы расходятся — `droppedKeyInstanceId` из неправильного места | `InventoryData` |
| 3 | **`HasKeyInstance` не используется в TryDrop** | Instance ищется по `instanceId`, не по (itemId, ownerPlayerId). Race condition | `InventoryWorld.TryDrop` |
| 4 | **Pickup без KeyRodInstanceBinding** | Drop'нутый ключ в мире без scene-placed binding → `instanceId=0` → при drop — `instanceId > 0` не срабатывает → ownership не сбрасывается | `PickupItem.Collect` + `TryPickup` |

---

## §3. План фиксов (минимальный — Шаг 1)

### Шаг 1: правильный поиск instance при drop

**Файл:** `InventoryWorld.cs` — `TryDrop`

**Замена:**
```csharp
int droppedKeyInstanceId = -1;
if (foundType == ItemType.Key)
{
    droppedKeyInstanceId = data.GetKeySlotAt(indexInList).instanceId;
    data.RemoveKeySlotAt(indexInList);
}
else { ... }
```

**На:**
```csharp
int droppedKeyInstanceId = -1;
if (foundType == ItemType.Key)
{
    droppedKeyInstanceId = data.GetKeySlotAt(indexInList).instanceId;
    data.RemoveKeySlotAt(indexInList);
    
    // T-KEY-09: если instanceId=0 (drop'нутый ключ или legacy), ищем instance
    // по (itemId, owner=clientId, state=Active).
    if (droppedKeyInstanceId <= 0)
    {
        var krw = typeof(ProjectC.Ship.Key.KeyRodInstanceWorld);
        var getForPlayer = krw.GetMethod("GetInstancesForPlayer",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var getInstance = krw.GetMethod("GetInstance",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (getForPlayer != null && getInstance != null)
        {
            var playerInsts = getForPlayer.Invoke(null, new object[] { clientId })
                as System.Collections.IList;
            if (playerInsts != null)
            {
                foreach (int iid in playerInsts)
                {
                    var inst = getInstance.Invoke(null, new object[] { iid });
                    if (inst == null) continue;
                    var ii = (int)inst.GetType().GetField("itemId").GetValue(inst);
                    var st = (int)inst.GetType().GetField("state").GetValue(inst);
                    if (ii == foundItemId && st == 0) // state==Active
                    {
                        droppedKeyInstanceId = iid;
                        break;
                    }
                }
            }
        }
    }
}
```

---

## §4. Шаги 2-3 (опциональные)

### Шаг 2: drop'нутый ключ создаёт новый instance при pickup

**Файл:** `PickupItem.Collect` + `TryPickup`

**Проблема:** drop'нутый ключ в мире не имеет `KeyRodInstanceBinding` → `instanceId=0` при pickup → невозможно отличить от других.

**Фикс:** при pickup без instanceId (drop'нутый ключ) — создать новый instance через `KeyRodInstanceWorld.CreateInstance(itemId, 0, clientId)`, обновить slot.

### Шаг 3: KeyRodInstanceBinding регистрация в Awake

**Файл:** `KeyRodInstanceBinding.cs`

**Проблема:** `TryRegister` стартует в `Start`, ждёт `IsSpawned`. Если рестарт — retries исчерпываются.

**Фикс:** пытаться зарегистрироваться сразу в `Awake` если IsServer + InventoryWorld есть. Fallback — существующий retry loop.

---

## §5. Effort

| Шаг | Что | Effort | Зависимости |
|---|---|---|---|
| Шаг 1 | Drop: поиск instance по (itemId, owner) | 30min | — |
| Шаг 2 | Pickup: создать instance для drop'нутых ключей | 1h | Шаг 1 |
| Шаг 3 | KeyRodInstanceBinding: register в Awake | 30min | — |

**Итого MVP:** ~2.5h

---

## §6. Тест-план

| Шаг | Ожидание |
|---|---|
| 1. Подобрать 1 ключ | Dropdown показывает корабль |
| 2. Drop ключа (через TAB → БРОСИТЬ) | Ключ в мире, instance.state=Lost, instance.owner=NONE |
| 3. F у корабля | ❌ Доступ запрещён (IsOwnerOfShip → false) |
| 4. Restart → Play Host | Persistence: instance остался state=Lost, owner=NONE |
| 5. F у корабля | ❌ Всё ещё запрещён |
| 6. Pickup drop'нутого ключа | instance.state=Active, owner=playerId, новый instanceId |
| 7. F у корабля | ✅ Доступ разрешён |

---

## §7. Альтернативная архитектура

См. документ `28_KEY_ARCHITECTURE_REVIEW.md` для анализа альтернативного подхода `(itemId, instanceId)` который может упростить всю систему.

---

*Changelog ведёт агент Mavis.*
