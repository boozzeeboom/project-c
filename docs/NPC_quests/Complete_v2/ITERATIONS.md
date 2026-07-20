# Итерации — Complete_v2 (T-CNPC-01)

---

## Итерация от 2026-07-21

**Задача:** Исправить списание репутации NPC за агрессию: точная атрибуция attackerClientId, убрать guard _socialBrain, push снапшотов клиенту.

**Изменения:**

| Файл | Что |
|------|-----|
| `NpcTarget.cs` | `OnHpChanged` → `Action<int, int, ulong>` (добавлен `attackerClientId`) |
| `NpcBrain.cs` | `OnNpcHpChanged(int, int, ulong)` — прямая атрибуция, `ModifyNpcAttitude(-2)` без guard'а `_socialBrain`, убран `FindNearestPlayerTarget` |
| `QuestServer.cs` | `OnNpcAttitudeChanged` + `OnReputationChanged` → push снапшота клиенту (`BroadcastNpcAttitudeChange` / `BroadcastReputationChange`) |

**Проблемы до:**

1. `OnHpChanged` терял `attackerClientId` — `OnNpcHpChanged` угадывал обидчика через `FindNearestPlayerTarget` (неверно для ranged-атак)
2. Штраф `-2` применялся только при `_socialBrain != null && enableGrudgeMemory` — NPC без `NpcSocialBrain` не штрафовались
3. После боевого штрафа снепшот не пушился клиенту — DialogWindow/CharacterWindow показывали замороженные значения

**После:**

- `ModifyNpcAttitude(attackerClientId, npcId, -2)` — всегда, для любого NPC
- `ModifyNpcAttitude(attackerClientId, npcId, -20)` — при убийстве (без изменений, уже работало)
- UI обновляется сразу после каждого изменения
