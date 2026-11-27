# Iterations

## Итерация от 2026-07-31

**Задача:** Unified Quest Graph v5 — единый нодовый редактор, связывающий NpcDefinition + DialogTree + QuestDefinition в одном GraphView. Архитектура: промежуточный QuestGraphModel (write-through адаптер), семантические порты, 6 типов нод.

**Коммит:** `c2b6494db8a2a962239a9de7b83820c7339fb6c3` — T-QEDIT: Unified Quest Graph v5

**Изменения:**
- `Assets/_Project/Quests/ARCHITECTURE_PLAN.md` — архитектурный план (новый)
- `Assets/_Project/Quests/Editor/QuestGraphModel.cs` — модель-адаптер (новый)
- `Assets/_Project/Quests/Editor/GraphNodes.cs` — 6 классов нод (новый)
- `Assets/_Project/Quests/Editor/UnifiedQuestGraphView.cs` — GraphView + Window (переписан)
- `Assets/_Project/Quests/Editor/UnifiedQuestGraphView_DEPRECATED.txt` — старый v4 (архивирован)

## Итерация от 2026-11-27 — T-MERCH80

**Задача:** 80 именных торговцев PRIMUM на базе канонического префаба Миры: свои `NpcDefinition`, свои `NpcMerchantTable`, живые диалоги, расстановка в `WorldScene_0_0` группой `NPC_PRIMUM_MERCHANTS`, FO-регистрация.

**Коммит:** T-MERCH80 (см. `git log`).

**Изменения:**
- `Assets/_Project/Quests/Data/Npcs/Merchants/` — 80 `NpcDefinition`.
- `Assets/_Project/Quests/Data/Merchants/` — 80 `NpcMerchantTable`.
- `Assets/_Project/Quests/Data/Dialogs/Merchants/` — 80 `DialogTree`.
- `Assets/_Project/Quests/Data/QuestDatabase.asset` — аддитивная регистрация.
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — группа `NPC_PRIMUM_MERCHANTS` (80 экземпляров).
- `Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotSceneCatalog.asset`, `GlobalMotionPilotProfile.asset` — FO-записи и digest.
- `docs/dev/T_MERCH80_PRIMUM_MERCHANTS.md` — описание, группы, проверка.

## Итерация от 2026-11-27 — T-MERCH80b

**Задача:** задать торговцам из T-MERCH80 правильные фракции (только `GuildOfSuccess` / `FreeTraders` / `Villagers`) и подобрать по полу/профилю модель из `Assets/Generated_Models_Rigged_NPC`, вместо одинаковой модели Миры.

**Коммит:** T-MERCH80b (см. `git log`).

**Изменения:**
- `Assets/_Project/Quests/Data/Npcs/Merchants/Merchant_001..080.asset` — `factionRef` + legacy `faction` по группе товара (30 Guild of Success, 21 Free Traders, 29 Villagers).
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — у 80 экземпляров обновлён `NpcSocialBrain.faction`, модель `Visual/HumanM_Model` заменена на gender-matched `Model_*` из каталога; ANIMATOR `NpcAnimator_Goblin`, root motion off, humanoid avatar.
- `Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotSceneCatalog.asset`, `GlobalMotionPilotProfile.asset` — перепечён каталог после смены вложенных префабов (210 entries, digest сходится).
- `Tools/MerchantPrimumn80/Generator.cs` — `PreviewModels`, `ApplyFactionsAndModels`, `ProbeModelBounds`, `VerifyFactionsAndModels`, `AuditAllTradeNpcs`.
- `docs/dev/T_MERCH80_PRIMUM_MERCHANTS.md` — таблица фракций и моделей.
