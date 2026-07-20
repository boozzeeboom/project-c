# Итерации — Complete_v2 (T-CNPC-01)

---

## Итерация от 2026-07-29 — T-FACTION-UNIFY (Этапы A-D)

**Задача:** Объединить две системы фракций — боевую (NpcFaction) и квестовую (FactionDefinition) — в одну каноническую базу FactionDefinition.

**Коммит:** `81fedb6` — T-FACTION-UNIFY: объединение NpcFaction → FactionDefinition (этапы A-D)

**План:** `docs/NPC_quests/Complete_v2/03_FACTION_UNIFICATION_PLAN.md`

**Выполнено:**

| Этап | Описание | Статус |
|------|----------|--------|
| A | Инфраструктура: +4 FactionId, FactionRelation.cs, combat-поля/методы в FactionDefinition, [Obsolete] на NpcFaction | ✅ |
| C | 4 новых FactionDefinition (Bandits, Cultists, Guards, Villagers) + обновлён Pirates | ✅ |
| B | Миграция AI-кода: NpcSocialBrain, NpcBrain, NpcGroupController, NpcSpawnerConfig | ✅ |
| D | Editor-скрипт миграции + GUID-замена в 4 спавнер-конфигах | ✅ |
| E | Верификация (PlayMode-тест) | ⏳ пользователь |
| F | Очистка: удалить NpcFaction.cs + 5 .asset | ⏳ после E |

**Изменённые файлы (12):**

| Файл | Изменение |
|------|-----------|
| `FactionId.cs` | +4 значения: Bandits=12, Cultists=13, Guards=14, Villagers=15 |
| `FactionRelation.cs` (**новый**) | enum `FactionRelation` + struct `FactionCombatRelation` в `ProjectC.Factions` |
| `FactionDefinition.cs` | + `defaultCombatRelation`, `combatRelations[]`, `CombatKey`, `GetCombatRelation()`, `IsHostileTowards()`, `IsAlliedWith()`, кеш |
| `NpcFaction.cs` | + `[Obsolete("Use FactionDefinition instead. T-FACTION-UNIFY")]` |
| `NpcSocialBrain.cs` | `NpcFaction` → `FactionDefinition`, методы `IsHostile`→`IsHostileTowards`, `IsAllied`→`IsAlliedWith`, `factionId`→`CombatKey` |
| `NpcBrain.cs` | + `using ProjectC.Factions`, `IsHostile`→`IsHostileTowards` |
| `NpcGroupController.cs` | + `using ProjectC.Factions`, все `IsAllied`/`IsHostile` → новые методы |
| `NpcSpawnerConfig.cs` | `NpcFaction faction` → `FactionDefinition faction` |
| `NpcWorldInspectorWindow.cs` | `factionId` → `CombatKey` (string fix) |
| `FactionMigrationTool.cs` (**новый**) | Editor-миграция NpcFaction→FactionDefinition |
| `CreateFactionAssets.cs` (**новый**) | Создание FactionDefinition для новых фракций |
| 4× `NpcSpawner_*.asset` | GUID-замена: NpcFaction → FactionDefinition |

**Новые ассеты (4):**
- `Faction_Bandits.asset` (factionId=Bandits, Hostile)
- `Faction_Cultists.asset` (factionId=Cultists, Hostile)
- `Faction_Guards.asset` (factionId=Guards, Neutral)
- `Faction_Villagers.asset` (factionId=Villagers, Neutral)

**Маппинг спавнеров:**
- `NpcSpawner_Default` → Faction_Bandits
- `NpcSpawner_Quest` → Faction_Cultists
- `NpcSpawner_neutral` → Faction_Villagers
- `NpcSpawner_ship_deck` → Faction_Villagers

**0 ошибок компиляции.**

---

## Итерация от 2026-07-21
=======


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
