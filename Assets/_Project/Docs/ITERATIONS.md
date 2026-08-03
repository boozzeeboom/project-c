# Журнал итераций

## Итерация от 2026-07-14

**Задача:** Исправить баг: при перезаходе теряется доступ к кораблю (ключ в инвентаре, но корабль заблокирован)  
**Коммит:** `4b95e65` — T-KEY-FIX: persistentShipId для KeyRodInstance — фикс потери доступа к кораблю между сессиями  
**Изменения:**
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstance.cs` — добавлено поле `persistentShipId`
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceRepository.cs` — `persistentShipId` в DTO и SaveAll
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` — индекс `_instancesByPersistentId`, rebind `registeredShipId` при спавне, очистка stale-инстансов
- `Assets/_Project/Scripts/Player/ShipController.cs` — `CreateKeyInstanceWhenReady` передаёт `ShipPersistentId`

**Корень бага:** `NetworkObjectId` нестабилен между сессиями → дубликаты `KeyRodInstance` → проверка владения находила новый instance с `owner=NONE`

---

## Итерация от 2026-07 (v2)
=======


**Задача:** Исправить микротряску персонажа при standing  
**Коммит:** `3866c59` — T-JITTER01-v2: корневая причина — NetworkTransform.Interpolate конфликтует с CharacterController.Move/NavMeshAgent  
**Изменения:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — `OnNetworkSpawn`: `nt.Interpolate = false` для owner; `using Unity.Netcode.Components`; фильтрация sleeping Rigidbody + delta threshold в platform carry
- `Assets/_Project/Scripts/AI/NpcBrain.cs` — `OnNetworkSpawn`: `nt.Interpolate = false` на хосте; `using Unity.Netcode.Components`
- `Assets/_Project/Docs/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — полный v2-диагноз
- `Assets/_Editor/InvestigateAnimator.cs` — diagnostic tool (создан)

**Стратегия отката:** `git revert 3866c59`

