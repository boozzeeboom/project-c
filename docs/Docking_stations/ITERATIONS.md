# Итерации — Docking Stations

## Итерация от 2026-07-16 (фикс визуалов)

**Задача:** Починка DockPadVisualMarker — розовые/серые пады в Play Mode, v3→v5.

**Коммиты:** `b5a93f8` → `00592b6` → `c712e1c` → `d2eeeea` → `ab68de3`

**Проблемы и решения:**
1. `[ExecuteAlways]` + `Destroy()` отложенный → старые `_PadSurface` дети с битыми GUID висели до конца кадра поверх новых → **убран `[ExecuteAlways]`, заменён на `DestroyImmediate`**
2. Сцена потеряла пользовательские фермы/пады (перезапись YAML) → **восстановлена из `b79b72e`**
3. `[SerializeField] Material` поля — все `null` в рантайме несмотря на правильные GUID в YAML → **v4: хардкод-материалы через `new Material(Shader.Find(...))`**
4. Хардкод неприемлем → **v5: гибрид — сериализованные поля + `?? s_default*` кодогенерация как fallback**

**Архитектура v5:**
- `[SerializeField] Material neutralMat/freeMat/...` — можно задать в инспекторе, используется если не null
- `static Material s_defaultNeutral/...` — создаются в коде один раз (`Awake`, `HideAndDontSave`), если поле не задано
- `ResolveMaterial(state)` → `field ?? s_default*` — приоритет: инспектор > код
- `CreateVisual()` → один Quad (`_PadVisual`) над триггер-боксом
- `LateUpdate()` → `DetermineState()` из `PadStateSync` → смена материала при изменении состояния
- 7 состояний: Neutral, Free, Pending, AssignedToMe, AssignedOther, OccupiedNpc, OccupiedPlayer

**Изменения:**
- `Assets/_Project/Scripts/Docking/Stations/DockPadVisualMarker.cs` — v5 финальная версия

---

## Итерация от 2026-07-12 (реализация)

**Задача:** Реализация T-DOCK-14a..14e — PadStateSync, интеграция с DockingWorld, материалы, DockPadVisualMarker v2.

**План:** `docs/Docking_stations/11_VISUAL_MARKERS_PLAN.md`

**Изменения:**
- `Assets/_Project/Scripts/Docking/Stations/PadStateSync.cs` — новый NetworkBehaviour (ClientRpc-синхронизация падов)
- `Assets/_Project/Scripts/Docking/Stations/DockPadVisualMarker.cs` — полный rewrite (7 состояний, holographic-эффекты)
- `Assets/_Project/Scripts/Docking/Core/DockingWorld.cs` — интеграция с PadStateSync (6 точек вызова)
- `Assets/_Project/Scripts/Docking/Network/DockStationController.cs` — +RequireComponent PadStateSync
- `Assets/_Project/Scripts/Editor/PortStationCreator.cs` — обновлён под v2 материалы
- `Assets/_Project/Prefabs/NPC_ZONES/Pad_01.prefab` — обновлён (7 материалов)
- `Assets/_Project/Materials/Docking/` — 7 новых материалов (M_Pad_*)
- `Assets/Generated_Models/PadRing/` — ring mesh
- `docs/Docking_stations/06_ROADMAP.md` — T-DOCK-14 → ✅
- `docs/Docking_stations/00_README.md` — Known issues обновлён

## Итерация от 2026-07-12 (план)

**Задача:** Глубокий анализ `DockPadVisualMarker` + план v2 с holographic-маркерами.

**План:** `docs/Docking_stations/11_VISUAL_MARKERS_PLAN.md` — 6 тикетов T-DOCK-14a..14f
**Коммит:** `6c65546` — T-DOCK-14: План переработки DockPadVisualMarker v2

**Изменения:**
- `docs/Docking_stations/11_VISUAL_MARKERS_PLAN.md` — новый документ
- `docs/Docking_stations/00_README.md` — обновлён Known issues + навигация
- `docs/Docking_stations/CHANGELOG.md` — запись 2026-07-12

---

## Итерация от 2026-07-07

**Задача:** Создать Custom Editors для NpcShipController и NpcShipSchedule — фолдауты, inline-редактирование, dropdown станций из сцены, кнопка Create New Schedule.

**Коммит:** `7064204` — T-NPCEDIT01: Custom Editors для NpcShipController и NpcShipSchedule

**Изменения:**
- `Assets/_Project/Scripts/PeacefulShip/Editor/NpcShipControllerEditor.cs` — новый файл
- `Assets/_Project/Scripts/PeacefulShip/Editor/NpcShipScheduleEditor.cs` — новый файл
- `Assets/_Project/Scripts/PeacefulShip/Editor/NpcShipRouteDrawer.cs` — новый файл
- `docs/Docking_stations/10_NPC_SHIP_EDITOR.md` — документация
