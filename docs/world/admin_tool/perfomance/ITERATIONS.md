# Performance Monitoring — Iterations Log

## Итерация №1 — 2026-07-25

**Задача:** Phase 0 + Phase 1 исполнения плана PERFORMANCE_MONITORING_RESEARCH.md v2.0

**Коммит:** `2f42260` — T-PERF-01: ProfilerMarker instrumentation — Phase 0+1 (14 subsystems)

**Изменения:**
- `Assets/_Project/Scripts/Core/ProjectCPerfCounters.cs` — создан (реестр ProfilerMarker'ов)
- `Assets/_Project/Scripts/AI/NpcBrain.cs` — ProfilerMarker в Update + FixedUpdate, счётчик ActiveNpcs
- `Assets/_Project/Scripts/Player/ShipController.cs` — ProfilerMarker в FixedUpdate, счётчик ActiveShips
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — ProfilerMarker в Update
- `Assets/_Project/Scripts/Core/CloudManager.cs` — ProfilerMarker в Update
- `Assets/_Project/Scripts/Core/CloudLayer.cs` — ProfilerMarker в Update
- `Assets/_Project/Scripts/Core/DistantCloudManager.cs` — ProfilerMarker в Update
- `Assets/_Project/Scripts/Core/NearCloudRenderer.cs` — ProfilerMarker в Update
- `Assets/_Project/Scripts/Core/WindManager.cs` — ProfilerMarker в Update
- `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs` — ProfilerMarker в Update
- `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs` — ProfilerMarker в Update
- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs` — ProfilerMarker в LateUpdate
- `Assets/_Project/Scripts/Combat/Client/TargetLockService.cs` — ProfilerMarker в Update
- `Assets/_Project/Scripts/Crafting/CraftingTimeService.cs` — ProfilerMarker в Update
- `Packages/manifest.json` — добавлен Graphy 3.0.5 (OpenUPM)
- `ProjectSettings/PackageManagerSettings.asset` — добавлен OpenUPM scoped registry

**Отклонения от research-документа:**
- `ProfilerCounter<T>` не существует в Unity 6000.5.2f1 — заменён на статические `int` поля
- `ProfilerCategory.Ai` используется вместо `ProfilerCategory.AI` (реальное имя в Unity 6)
- ShipController не имеет `Update()` — только `FixedUpdate()` инструментирован
- CombatServer не имеет `Update()` (событийно-ориентирован) — пропущен
- CraftingServer не имеет `Update()` — вместо него инструментирован `CraftingTimeService`

**Статус:** ✅ Phase 0 + Phase 1 завершены
