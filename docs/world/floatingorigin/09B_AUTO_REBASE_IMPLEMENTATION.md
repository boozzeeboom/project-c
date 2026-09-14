# T-FO09B — Авто-пороговый rebase: реализация

Date: 2026-09-13. По плану `09B_AUTO_REBASE_PLAN.md`.

## Изменения (`GlobalMotionControlledRebaseSlice.cs`, +53/−4)

1. Автотриггер: поля `_autoRebaseEnabled=true`, `_autoCheckIntervalSec=5`,
   `_autoCooldownSec=35`, `TryAutoRebase()` в `Update` (ветвь после F8/F9).
   Только сервер; предпроверка тем же `OriginRebasePlan.TryCreate`
   (гистерезис бесплатен); запуск тем же путём с `reason="auto_threshold"`.
2. Reason plumbing: `RequestControlledRebase(bool, string="user_controlled")`
   → `ExecuteTransaction(reason)` → маркер `Requested` несёт reason.
   Контекстные меню и старые вызовы совместимы (default).
3. Кулдаун: `_lastShiftEndTime` ставится в конце Completed и Rollback
   (оба исхода). 35с > 30с кулдауна палуб (06DF).
4. Микрофикс батчем: `TrailRenderer.Clear` + `LineRenderer.positionCount=0`
   в `ClearShiftedParticles` (та же семья что 07C; счётчик `ParticlesCleared`).

## Почему безопасно

- Транзакция та же, что F8 (участники, фазы, флаги, broadcast, rollback).
- F8-эквивалентность покрыта серией ф8_15–ф8_21 (полёт, бой, палуба).
- Отключение: галка `Auto Rebase Enabled` на slice в BootstrapScene
  (инспектор, без правок сцены кодом).

## Проверка

1. Compile: `refresh_unity` (force + compile) — PASS; `read_console` —
   0 errors, 0 CS; скобки 162/162.
2. Play Mode (user): отлёт >256м → `Requested(reason=auto_threshold)` →
   `Completed`; повторных срабатываний подряд нет (кулдаун);
   F8/F9 ручные работают как раньше (`reason=user_controlled`).

## Приложение: свёртка плана 09B_PLAN (2026-09-14)

- Остаток хоста на момент плана: OPEN-кодом был только авторебейс
  (реализован выше); остальное — PASS/тесты пользователя/DEFERRED.
- Микроаудиты (закрыты без отдельного кода):
  `ProjectileVisual`/`ThrowArcVisual` — transient, самозаживление;
  закрыты батчем 09B (`TrailRenderer.Clear` + `LineRenderer.positionCount=0`
  в `ClearShiftedParticles`). Combat DTO `targetPosition` — снапшот момента
  удара, кэша нет. `AltitudeCorridorSystem` на тот момент — live-входы
  (позже найден защёлкнутый кэш — см. 09M, исправлено clone-on-write).
- Параметры: порог/квант 256м, опрос 5с, кулдаун 35с (>30с палуб 06DF);
  гистерезис естественный (остаток < порога); F9 после авто — штатный rollback.
