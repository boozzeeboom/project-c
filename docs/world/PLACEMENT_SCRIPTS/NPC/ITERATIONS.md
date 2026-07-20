# Итерации NPC World Inspector

## Итерация от 2026-07 (v1.1 — Factions tab)
**Задача:** Добавить вкладку «🏛 Factions» — сканирование, редактирование, создание FactionDefinition SO. Cross-reference с NPC из сцен.
**Коммит:** `8267c82` — T-FACT01: таб «🏛 Factions» в NpcWorldInspector — сканирование, редактирование и создание FactionDefinition SO
**Изменения:**
- `Assets/_Project/Editor/Tools/NpcWorldInspectorData.cs` — FactionEntry, FactionCombatRelationEntry, FactionScanResult
- `Assets/_Project/Editor/Tools/NpcWorldInspectorWindow.cs` — третий таб Factions, ScanFactions, SaveFactionChanges, CreateNewFaction, DeleteFaction
- `docs/world/PLACEMENT_SCRIPTS/NPC/README.md` — документация v1.1

## Итерация от 2026-07 (v1.0 — создание инструмента)
**Задача:** Разработать NpcWorldInspectorWindow — единый EditorWindow для инспектирования NPC по всем WorldScene.
**Коммит:** `9bc6623` — feat(editor): add NpcWorldInspector — unified NPC inspector across all WorldScenes
**Изменения:**
- `Assets/_Project/Editor/Tools/NpcWorldInspectorData.cs`
- `Assets/_Project/Editor/Tools/NpcWorldInspectorWindow.cs`
- `docs/world/PLACEMENT_SCRIPTS/NPC/README.md`
