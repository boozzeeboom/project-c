# M16 — QuestDatabaseWindow Editor Tool

> **Дата:** 2026-06-09
> **Сессия:** M16 (T-Q09)
> **Roadmap:** расширяет `08_ROADMAP.md` §8.3.4
> **Статус:** ✅ DONE 2026-06-09 (verified by Roslyn)
> **Зависимости:** M13 ✅, M14 ✅, M15 ✅

---

## 1. Проблема (audit 2026-06-09)

**До M16:**
- ✅ `QuestDatabaseAutoDiscover` (auto-scan папок при editor load/save)
- ✅ `QuestDefinitionValidator` (validation on save)
- ✅ `DialogueConditionDrawer` (custom inspector)
- ❌ **Нет UI для просмотра списка квестов** — чтобы найти конкретный квест, надо:
  - Открыть `Project` window
  - Navigate `Assets/_Project/Quests/Data/Quests/`
  - Кликнуть на .asset
  - Откроется Inspector (узкий, сразу все поля)
  - Нет cross-reference на связанные DialogTree / NPC / Faction

**Жалоба:** "при добавлении нового квеста не вижу сразу что у меня в базе, как quest взаимодействует с NPC"

## 2. Что сделано

`Assets/_Project/Quests/Editor/QuestDatabaseWindow.cs` (367 lines, new):

- **UI Toolkit EditorWindow** (no IMGUI)
- **Меню:** `Tools > ProjectC > Quests > Quest Database Explorer`
- **Layout:**
  - **Left pane (TreeView):**
    - 📜 Quests (N)
    - 💬 Dialogs (N)
    - 👤 NPCs (N)
    - 🏛 Factions (N)
    - 🔄 Re-scan DB button
  - **Right pane (ScrollView):**
    - Detail view выбранного asset
- **Detail views (4 типа):**
  - **Quest:** questId, displayName, description, faction, minRep, oneShot, discoverable
    + stages (все с objectives + onEnter/onComplete counts)
    + rewards (CR, items, reputation)
    + "Open in Inspector" / "Ping Asset" кнопки
  - **Dialog:** treeId, displayName, rootNodeId, nodes list
  - **NPC:** npcId, displayName, questOffers, questTurnIns
  - **Faction:** factionId, displayName, loreDescription
- **Status bar:** bottom-left, счётчики Quests/Dialogs/NPCs/Factions

## 3. Архитектурные решения

- **UI Toolkit over IMGUI:**
  - Не блокирует mouse (R3-005 pitfall IMGUI OnGUI)
  - Современный look
  - TreeView с встроенным virtualization
- **Reuse `QuestDatabaseAutoDiscover.Rescan()`:** не дублировать scan logic
- **Cross-reference через Inspector/Ping:** "Open in Inspector" → стандартный Unity Inspector с focused selection
- **Per-kind detail builders:** отдельный метод для каждого типа (BuildQuestDetail/BuildDialogDetail/BuildNpcDetail/BuildFactionDetail) — extensible

## 4. Что НЕ сделано (out of scope M16)

- ❌ Create/Edit quest UI — это M17 (GraphView) или M18 (advanced editor)
- ❌ Cross-graph visualization — это M17
- ❌ Drag-drop quest to NPC — M17
- ❌ Live validation panel — есть `QuestDefinitionValidator` (separate)
- ❌ Search/filter — пока всё видно в TreeView (в будущем — search bar)
- ❌ Localization support — только русский display name

## 5. Файлы

**New:**
- `Assets/_Project/Quests/Editor/QuestDatabaseWindow.cs`

**Modified:** none

## 6. Критерии готовности

- [x] Window открывается через `Tools > ProjectC > Quests > Quest Database Explorer`
- [x] TreeView показывает 4 группы (Quests, Dialogs, NPCs, Factions)
- [x] Клик на quest → detail view (stages, objectives, rewards)
- [x] Клик на dialog/NPC/faction → соответствующий detail view
- [x] Re-scan button обновляет счётчики
- [x] "Open in Inspector" работает
- [x] 0 compile errors

## 7. Verify

**Roslyn verify (2026-06-09):**
```
Found in: Assembly-CSharp-Editor
Type: ProjectC.Quests.Editor.QuestDatabaseWindow
Window opened
```

**Manual verify (user):**
- Меню `Tools > ProjectC > Quests > Quest Database Explorer` → window открывается
- 4 группы в TreeView с актуальными счётчиками (3 / 5 / 1 / 12 для текущей базы)
- Клик на quest → detail view с правильными stages + objectives

## 8. Следующие шаги (M17, M18 — будущее)

- **M17 (GraphView):** узловая визуализация quest → stages → objectives → actions. ~6-8 ч.
- **M18 (CRUD UI):** встроенный редактор нового quest с template wizard. ~3 ч.
- **M19 (Search/Filter):** search bar в TreeView. ~1 ч.

M17 уже deferred (T-Q09b в original roadmap).
