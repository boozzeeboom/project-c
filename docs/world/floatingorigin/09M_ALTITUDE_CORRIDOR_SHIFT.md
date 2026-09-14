# T-FO09M: сдвиг высотных коридоров вместе с миром

## Диагноз (блокер: корабли трясёт «порывами» после FO-интеграций)

Наблюдение: пустые корабли дёргает рывками даже при `_shipWindMultiplier = 0`
(влияние ветра на корабли отключено в WindManager). Стоя на площадке видно,
как корабль сдувает порывами.

Корень — **не ветер, а турбулентность высотных коридоров**:

- `ShipController.FixedUpdate` (isIdle-ветка: двигатель включён, пилотов нет):
  `currentAlt = transform.position.y` → `GetActiveCorridor(position)` →
  при `altitude < minAltitude` → `TurbulenceEffect.Update(severity=1)` навсегда.
- Глобальный коридор: `minAltitude = 1200`, `maxAltitude = 4450`.
  После F8 мир едет на translation (типовой `dy = -2560`, `dx/dz = ±40000`):
  корабль с `y ≈ 2500` оказывается на `y ≈ -60` → глубина ниже границы
  `(1200-(-60))/200 ≫ 1` → `severity = 1` **перманентно, на всех кораблях**.
- Силы огромные: `totalForce = severity · mass · forceMultiplier(50)`,
  для mass=2000 → ~100 000 Н случайных сил каждые 0.05 с + моменты
  (`turbulenceIntensity`, `verticalMultiplier 2.5`, `horizontalMultiplier 1.8`).
  Визуально это и есть «рывки порывами».
- Независимость от ветра — по построению: `_shipWindMultiplier` гейтит
  только `ApplyGlobalWind`; турбулентность идёт отдельным путём
  (`ApplyTurbulence`), зонный ветер (`ApplyWind` по триггерам) множитель
  тоже не видит. Поэтому отключение ветра в менеджере ничего не дало.
- Вторая половина: городские коридоры (`cityCenter`, `cityRadius`) не едут →
  `GetActiveCorridor` после сдвига считает дистанцию до старых центров
  (десятки км) → все корабли валятся в global-fallback с чужими порогами.

Отброшенные кандидаты:
- `NpcTestSpeedBooster` (50000 Н) — отсутствует в Scenes/Prefabs, только код.
- `ShipPositionServer.ApplyRestore` — обнуляет velocities, одноразовый старт.
- FO `Target.position += T` + `SyncTransforms` + `NetworkTeleport` — корректны;
  скорости/углы не трогают, что для сдвига правильно.

## Фикс (по аналогии с T-FO07F шторма / T-FO09L ветер)

1. `AltitudeCorridorSystem.ApplyRebaseTranslation(Vector3)`:
   `minAltitude/maxAltitude += t.y` (мировые высоты),
   `cityCenter += t` (только `!isGlobal`), дедуп через HashSet
   (global может дублироваться в списке). Возвращает число.
   Мутация runtime-only (SO в play mode / baked в билде), рестарт — чисто.
2. Slice `ShiftAltitudeCorridors` (best-effort, синглтон напрямую) +
   маркер `CorridorsShifted(cells=N)` в success-пути, rollback (`-T`),
   client-handler (`;corridors=`).

## Границы

- Severity пересчитывается live → после сдвига турбулентность гаснет сама.
- Клиентские реплики физику не считают (server-блок) — сдвиг у клиента
  только для консистентности данных.
- Зонный ветер в обход множителя и `windExposure` — отдельные треки, не здесь.

## Проверка

- Compile + 0 errors; F8 → `CorridorsShifted(cells>=1)`;
  пустые корабли стоят, `TURBULENCE!` в логе отсутствует.
