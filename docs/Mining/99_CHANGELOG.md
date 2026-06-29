# Resource Gathering — Changelog

> **Файл:** `docs/Mining/99_CHANGELOG.md`

| Дата | Версия | Изменение |
|------|--------|-----------|
| 2026-06-10 | v0.0.3 | Визуальная обратная связь: GatheringToast с ProgressBar (UI Toolkit), ResourceNode animation (scale-pulse + emissive loop, паттерн LockBox). Player animation deferred. |
| 2026-06-29 | T-G08 | **GatherType enum** в `ResourceNodeConfig` (Mining/Lambering/Gathering). Заполнено в 3-х .asset (IronVein/CopperVein=Mining, PlantHerb=Gathering). Анимация игрока пока hardcoded `GatherPulseLoop` — реальные clip'ы подключаются отдельным тикетом T-G09+ после согласования архитектуры (override vs new state). |
| 2026-06-29 | T-G09 | **Player gather animation per GatherType** — добавлены 3 state (Mine/Lumber/Gather) и 3 trigger-param (MinePlay/LumberPlay/GatherPlay) в `PlayerAnimation.controller` через Editor API (не YAML). Clips: Mining=`HumanM@MiningOneHand01_R - Ground` (Kevin Iglesias), Lambering=`Standing Melee Attack Downward` (Combat), Gathering=`HumanM@Gathering02` (Kevin Iglesias). AnyState→Mine/Lumber/Gather по триггерам, exit→Idle (0.95). Override controller пересоздан, 34 slot'а в инспекторе (3 новых для gather). `NetworkPlayer.OnGatherProgress` теперь `SetTrigger("MinePlay"/"LumberPlay"/"GatherPlay")` по `node.Config.GatherType`; hardcoded `GatherPulseLoop` остался как FALLBACK (`_gatherScaleAmplitude` default 0 = отключён). |
