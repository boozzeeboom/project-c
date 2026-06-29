# Resource Gathering — Changelog

> **Файл:** `docs/Mining/99_CHANGELOG.md`

| Дата | Версия | Изменение |
|------|--------|-----------|
| 2026-06-10 | v0.0.3 | Визуальная обратная связь: GatheringToast с ProgressBar (UI Toolkit), ResourceNode animation (scale-pulse + emissive loop, паттерн LockBox). Player animation deferred. |
| 2026-06-29 | T-G08 | **GatherType enum** в `ResourceNodeConfig` (Mining/Lambering/Gathering). Заполнено в 3-х .asset (IronVein/CopperVein=Mining, PlantHerb=Gathering). Анимация игрока пока hardcoded `GatherPulseLoop` — реальные clip'ы подключаются отдельным тикетом T-G09+ после согласования архитектуры (override vs new state). |
