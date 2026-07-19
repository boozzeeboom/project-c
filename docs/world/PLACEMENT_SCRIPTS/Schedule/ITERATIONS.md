# Итерации — NpcShipScheduleOverviewWindow

## Итерация от 2026-07-18 (v2 — inline editing)

**Задача:** Добавить inline-редактирование во все три вкладки: маршруты, назначение schedule кораблям, cargo trade.

**Изменения:**
- `Assets/_Project/Editor/Tools/NpcShipScheduleOverviewWindow.cs`:
  - Tab 1: expandable foldout-строки → inline-редактор через SerializedObject (identity, traffic, routes + Add/Remove)
  - Tab 2: выпадающий список schedule для каждого корабля → переоткрытие сцены, запись schedule, сохранение
  - Tab 3: редактируемые поля вместо read-only (toggles, limits, buyItems list + Add/Remove)
  - Поиск schedule через `AssetDatabase.FindAssets("t:NpcShipSchedule")` (project-wide, не только Resources)
- `docs/world/PLACEMENT_SCRIPTS/Schedule/README.md` — обновлено описание возможностей
- `docs/world/PLACEMENT_SCRIPTS/Schedule/ITERATIONS.md` — эта запись

---

## Итерация от 2026-07-18 (v1)

**Задача:** Создать EditorWindow для обзора всех расписаний NPC-кораблей, маршрутов, привязки к кораблям и cargo-trade

**Коммит:** `500e5df` — T-NS-TOOL01: NpcShipScheduleOverviewWindow — EditorWindow для обзора расписаний NPC-кораблей

**Изменения:**
- `Assets/_Project/Editor/Tools/NpcShipScheduleOverviewWindow.cs` — EditorWindow, 3 вкладки
- `docs/world/PLACEMENT_SCRIPTS/Schedule/README.md` — документация системы
- `docs/world/PLACEMENT_SCRIPTS/Schedule/ITERATIONS.md` — итерационная документация

