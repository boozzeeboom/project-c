# T-FO09G — Fallback-точки RespawnManager едут с миром

Date: 2026-09-13. Жалоба: «якорь в инспекторе — scene mismatch и там и там;
телепортирует после сдвига в пустоту далеко (за километры от спавна)».

## Диагноз (по коду и YAML)

1. **Scene mismatch у RespawnManager** — поля-якоря там нет и быть не может
   в текущем виде: `RespawnPointData.spawnPoint` — обычный Transform-референс,
   Unity не даёт сослаться на объект другой сцены (инспектор). Моё поле
   `_spawnAnchor` (09F) стоит на `GlobalMotionPilotSpawnSource`
   (NetworkManager) — оно только для СТАРТОВОГО спавна пилота, к
   респавну/спасению отношения не имеет. Извиняюсь за путаницу.
2. **Реальная причина «за километры»**: `RespawnManager` лежит в
   BootstrapScene, его `fallbackPosition` — печёные мировые данные.
   Обход сдвигов (`ShiftCarryCaches`) идёт только по поддеревьям
   участников (корни WorldScene) — RespawnManager не покрыт. После F8/
   автосдвига мир уезжает, а точка респавна остаётся в досдвиговых
   координатах → респавн/спасение возвращают игрока в пустоту за
   километры от спавна. (Замечу: пилотный старт
   `GlobalMotionPilotSpawnSource` читает `Respawn_Default` из WorldScene
   при Awake — тоже мимо сдвига, но стартовая точка пересчитывается
   каждый запуск; внутри сессии спасение было виновато.)

## Решение

1. `RespawnManager.ApplyRebaseTranslation` — сдвиг всех
   `fallbackPosition` в списке (структура в списке — перезапись элемента);
   live-якоря `spawnPoint` не трогаются (двигаются сами).
2. Slice: `FindObjectsByType<RespawnManager>` по всей сцене (не по
   участникам) + маркер `RespawnPointsShifted(managers=N)`. Вызов внутри
   `ShiftCarryCaches` — rollback-путь (с −T) покрывается автоматически.
3. Пилотный старт: без изменений (читает актуальную позицию при Awake
   каждого запуска; после сдвига внутри сессии якорь нужен только для
   НОВОЙ точки спавна — и 09F это покрывает).

## Проверка

1. Compile: `refresh_unity` (force + compile) — PASS; `read_console` —
   0 errors, 0 CS; скобки 165/165.
2. Play Mode (user): F8 → `RespawnPointsShifted(managers>=1)` → Esc-спасение
   → телепорт РЯДОМ со спавном (`Move.after grounded=True`), не за
   километры.
