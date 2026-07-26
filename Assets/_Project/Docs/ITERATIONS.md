# Журнал итераций

## Итерация от 2026-07 (v2)

**Задача:** Исправить микротряску персонажа при standing  
**Коммит:** `3866c59` — T-JITTER01-v2: корневая причина — NetworkTransform.Interpolate конфликтует с CharacterController.Move/NavMeshAgent  
**Изменения:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — `OnNetworkSpawn`: `nt.Interpolate = false` для owner; `using Unity.Netcode.Components`; фильтрация sleeping Rigidbody + delta threshold в platform carry
- `Assets/_Project/Scripts/AI/NpcBrain.cs` — `OnNetworkSpawn`: `nt.Interpolate = false` на хосте; `using Unity.Netcode.Components`
- `Assets/_Project/Docs/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — полный v2-диагноз
- `Assets/_Editor/InvestigateAnimator.cs` — diagnostic tool (создан)

**Стратегия отката:** `git revert 3866c59`

