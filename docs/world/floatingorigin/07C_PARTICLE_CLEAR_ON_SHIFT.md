# T-FO07C — Clear world-space частиц при сдвиге мира

Date: 2026-09-13. Этап плана: T-FO07, пункт 6 §2 (particles/trails/lines).

## Аудит (read-only)

- Local-space уже: `MeziyThrusterVisual` (Local), constellation/skill линии
  (`useWorldSpace=false`), wakes (`FollowTarget`, самосинк каждый кадр).
- World-space: `VeilSystem.lightningParticles`, `AdditionalVeilModule._lightningVFX`
  (молнии облаков). Эмиттеры — дети сдвигаемых корней, но уже
  просимулированные world-частицы остаются в старых координатах:
  после F8 молнии висят в 56 км до смерти частиц (секунды, само чинится).
- World-space `LineRenderer`/`TrailRenderer` в gameplay-коде не найдены.

## Решение (минимальное, best-effort)

После сдвига (успех) и после возврата (rollback): `ParticleSystem.Clear()`
по всем активным системам в поддеревьях участников (сервер) — убивает
висящие streaks одним кадром. Маркер `ParticlesCleared(n=...)`.
Второй клиент: тот же Clear scene-wide в `OnRebaseShiftMessage`
(маркер `ClientParticlesCleared`), т.к. списка участников у него нет.
Снаряды в полёте, trails игроков (local) — не трогаем: transient и local.

## Границы

- Clear на один кадр гасит и свежие эффекты (скиллы, трастеры) — незаметно
  на фоне 56 км сдвига; альтернатива (селективный clear только weather-слоя)
  оставлена на случай жалоб.
- Полный VFX-handoff (перезапуск эмиттеров с сохранением фазы) — отдельный gate.

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Play Mode (user): F8 в грозу → `ParticlesCleared(n>0)`, висящих молний
   в старых координатах нет.
