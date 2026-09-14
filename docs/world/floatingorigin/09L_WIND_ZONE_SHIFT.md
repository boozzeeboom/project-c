# T-FO09L — Сдвиг AABB сплайн-зон ветра (Spline Wind Zone)

Date: 2026-09-14. Запрос: проверить смещение Spline Wind Zone (тестовые
зоны в WorldScene_0_0).

## Аудит

- `SplineWindZone`: пассивный дескриптор, кэшей нет — всё live
  (`InverseTransformPoint`, локальные ноты сплайна, `TransformDirection`).
  Сами зоны едут с миром (корень `WindZones` в WorldScene). Правок не надо.
- `ShipWindZone`: live-позиции, кэшей нет. Правок не надо.
- **`WindManager` — НАЙДЕН баг**: `ZoneRuntimeState.worldBounds`
  (мировой AABB сплайна + corridor для предфильтра `Contains`) считается
  один раз (`boundsValid` latch) и никогда не сдвигается. После F8/автосдвига
  зоны уезжают, AABB остаётся → корабли внутри зон отсекаются предфильтром,
  ветер молча умирает. `entries` пересчитываются каждый detect (`Clear`) —
  там всё чисто.

## Решение

1. `WindManager.ApplyRebaseTranslation` — сдвиг `worldBounds.center`
   всех валидных состояний (точная операция: AABB сдвинутой геометрии =
   сдвинутый AABB). Возвращает число зон.
2. Slice `ShiftWindZones` (синглтон, best-effort) + маркер
   `WindShifted(zones=N)` в обоих путях (успех/rollback). Вызовы вне циклов
   (урок 09G-fix): по одному на путь.

## Проверка

1. Compile: `refresh_unity` (force + compile) — PASS; `read_console` —
   0 errors, 0 CS; скобки 37/37, 171/171.
2. Play Mode (user): корабль в тестовой сплайн-зоне → F8 → ветер продолжает
   действовать (маркер `WindShifted(zones>0)` при настроенных зонах).
