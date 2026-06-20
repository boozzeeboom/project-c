# Docking System — Сводка для старта

## Три P0 блокера — и система оживёт

| Баг | Симптом | Файл | Фикс |
|-----|---------|------|------|
| **B-001** SO `m_Script: {fileID: 0}` | `dockStationDefinition is null` + `no StationId` | `DockingAssetCreator.cs:25-45` | Исправить создание SO (см. BUG_AUDIT.md) |
| **B-003** sortingOrder = 0 | UI за HUD панелью | `CommPanelWindow.cs:130` | Добавить `_doc.sortingOrder = 10` |
| **B-004** PickingMode.Ignore | Кнопки не жмутся | `CommPanelWindow.cs:144` | Убрать `_root.pickingMode = PickingMode.Ignore` |

## Документы
- `ARCHITECTURE.md` — схема системы, namespaces, data flow
- `BUG_AUDIT.md` — все 11 багов с корневыми причинами
- `REFACTOR_PLAN.md` — 3 фазы: P0 фикс → функциональность → Phase 2

## Что предлагаю делать
1. Исправить DockingAssetCreator.cs (убрать 2 несуществующих поля + m_Script фикс)
2. Пересоздать SO → привязать к сцене
3. Добавить sortingOrder + убрать PickingMode.Ignore
4. Play Mode — проверить
