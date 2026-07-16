# DockStationController — Custom Editor

**Дата:** 2026-07-09
**Коммит:** T-DOCK-Editor: inline Definition + Duplicate + Geometry from Transform

---

## Архитектура

### Было (проблема)
`DockStationDefinition` содержал поля `platformCenter` и `platformAltitude` — **world-координаты**, которые дизайнер должен был вбивать вручную в SO. При этом:
- Эти поля **никогда не читались** ни одним рантайм-кодом
- При перемещении GameObject'а на сцене — SO устаревал
- Дублирование данных: позиция есть и в transform, и в SO

### Стало
- `platformCenter` / `platformAltitude` **удалены** из `DockStationDefinition`
- `DockStationController.PlatformCenter` = `transform.position` (read-only из transform)
- `DockStationController.PlatformAltitude` = `transform.position.y`
- В редакторе показываются read-only поля Geometry из текущей позиции объекта

**Логика:** передвинул объект на сцене → PlatformCenter/Altitude автоматически обновились.

---

## Инспектор

```
┌─ Geometry (from Transform) ────────┐
│ Platform Center  [40500, 2510, ...]│  ← read-only, из transform
│ Platform Altitude  2510            │
├────────────────────────────────────┤
│ Dock Station Definition  [DSPrimium]│
├────────────────────────────────────┤
│ 📋 Duplicate Definition            │  ← клонирует SO, обнуляет identity
├────────────────────────────────────┤
│ ▼ 📄 DSPrimium (inline)            │
│   Station Id     "STN-PRM-001"     │  ← редактируется прямо здесь
│   Location Id    "PRIMIUM"         │
│   Display Name   "Примум"          │
│   Voice Lines    [VoiceLines]      │
│   Max Landings   1                 │
│   Landing Window 90                │
└────────────────────────────────────┘
```

---

## Duplicate workflow

1. Назначил `DockStationDefinition_Primium` на контроллер города 1
2. Нажал **📋 Duplicate Definition**
3. Выбрал путь (например `DockStation_Farm_0_0.asset`)
4. Новый SO: identity пустые, остальное скопировано
5. Заполнил stationId/locationId/displayName для города 2
6. Готово

---

## Затронутые файлы

| Файл | Изменение |
|------|-----------|
| `DockStationDefinition.cs` | Удалены platformCenter, platformAltitude |
| `DockStationController.cs` | +PlatformCenter, +PlatformAltitude из transform |
| `DockStationControllerEditor.cs` | Geometry read-only + inline Definition + Duplicate |
| `CreateTestZone.cs` | Убран platformAltitude |
| `CreateTestZoneFixed.cs` | Убраны platformAltitude, padLayout |
