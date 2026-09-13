# T-FO09F — Явный якорь спавна из любой сцены

Date: 2026-09-13. Запрос: «не могу якорь из другой сцены задать —
scene mismatch crosscene... исправь, чтобы можно было дать якорь из других
сцен».

## Контекст

Спавн пилота резолвится только поиском по имени `Respawn_Default` в
WorldScene_0_0 (строгая проверка `scene.path == _worldScenePath`).
Назначить Transform-якорь из другой открытой сцены в инспектор было
невозможно — поля не существовало. («scene mismatch» при ручном
SetParent-действии — ограничение Unity-инспектора, обходится якорем.)

## Решение (`GlobalMotionPilotSpawnSource.cs`)

Новое serialize-поле `_spawnAnchor` (Transform) на
`GlobalMotionPilotSpawnSource` (компонент NetworkManager в BootstrapScene):

- Приоритет над `_respawnObjectName`: если назначен — позиция якоря
  (+`_verticalSpawnOffset`) становится точкой спавна, поиск по имени не
  выполняется.
- Проверка пути worldScene смягчена для якоря: якорь может лежать в любой
  загруженной сцене (например WorldScene_0_0), его мировая позиция уже
  глобальная; фрейм создаётся со сценой якоря.
- Без якоря поведение прежнее (Respawn_Default → strict path check).
- Пустой якорь (сцена выгружена) = fallback на прежний путь
  (`_spawnAnchor != null` — Unity null).

## Проверка

1. Compile: `refresh_unity` (force + compile) — PASS; `read_console` —
   0 errors, 0 CS (правка CS0136 shadowing после первого прогона).
2. Editor (user): BootstrapScene → NetworkManager →
   GlobalMotionPilotSpawnSource → Spawn Anchor — задать Empty из
   WorldScene_0_0 на доказанной земле (палуба хангара ≈ 40315, 2503, 39869).
   В рантайме лог: `Local pilot player ready; local=<позиция якоря>`.
