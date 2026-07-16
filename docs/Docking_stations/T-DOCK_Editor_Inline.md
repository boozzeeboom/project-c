# DockStationController — Custom Editor

**Дата:** 2026-07-09
**Коммит:** T-DOCK-Editor: inline DockStationDefinition + Duplicate

---

## Что добавлено

Кастомный редактор `DockStationControllerEditor` для `DockStationController`:

### Inline-отображение DockStationDefinition
При назначении `DockStationDefinition` в поле — все его поля отображаются прямо в инспекторе контроллера:
- **Identity**: stationId, locationId, displayName
- **Geometry**: platformCenter, platformAltitude
- **Dispatcher**: voiceLines
- **Limits**: maxConcurrentLandings, landingWindowSeconds

Редактирование inline — изменения сохраняются в ассете автоматически.

### 📋 Duplicate Definition
Кнопка дублирования текущего `DockStationDefinition`:
1. Создаёт копию ассета через диалог сохранения
2. Обнуляет поля identity (stationId, locationId, displayName) — чтобы пользователь задал новые
3. Geometry, VoiceLines, Limits — копируются как есть
4. Назначает новый ассет в поле контроллера

**Use case:** есть `DockStationDefinition_Primium` для города 1 → нажал Duplicate → задал новые stationId/locationId/displayName → готово для города 2.

---

## Файлы

| Файл | Что |
|------|-----|
| `Docking/Network/Editor/DockStationControllerEditor.cs` | Кастомный редактор |
