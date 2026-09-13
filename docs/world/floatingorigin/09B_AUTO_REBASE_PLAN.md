# T-FO09B — Точный остаток хост-клиента + план авто-порогового rebase

Date: 2026-09-13. Статус: анализ + план, кода нет.

## 1. Точный остаток хост-клиента (по §4 плана и коду)

| Пункт §4 | Статус | Основание |
|---|---|---|
| Дальние позиции, walk/jump/бой | PASS | f8_20 (4× frame 2→5), ф8_21 (0 errors) |
| Принудительный F8 | PASS | вся серия ф8_4–ф8_21 |
| **Пороговый (автоматический) rebase** | **OPEN — единственный крупный кодовый gate** | только F8; триггера нет по дизайну (06Z: `automatic trigger не подключён`) |
| Палубы/экипаж/NPC/пикапы/камера/шторма-код/частицы/respawn/rollback/сейвы | PASS | 06DF–08B, PERSIST-verify |
| Пилотирование (поток пилота) | PARTIAL, deferred | 08C-verdict: нужна v2-миграция репликации, не малый gate |
| Посадка/выход в момент сдвига | OPEN, тест пользователя | аудит 07G «безопасно», рантайма не было |
| Far-repeat в 30с (кулдаун) | OPEN, тест пользователя | код 06DF готов |
| Шторм/дocking/recall/торговля/chest/Q001-замеры | OPEN, тесты/контент | кода не требуют (аудиты ниже) |
| Мультиплеер/late join/regions/global-double wire | DEFERRED | решение 08E |

## 2. Микро-аудиты этой итерации (без кода, закрыты)

- `ProjectileVisual`/`ThrowArcVisual` (TrailRenderer): транзиентные (секунды
  жизни), после сдвига короткая полоса — самозаживление. Кандидат в микрофикс:
  `TrailRenderer.Clear` + `LineRenderer.positionCount=0` в путь `ClearShiftedParticles`
  (та же семья, что 07C). Предлагается батчем с 09B-реализацией.
- Combat DTO `targetPosition` (`DamageResult`, `CombatClientState`): снапшот
  момента удара, потребляется немедленно (VFX). Кэша нет, правок не надо.
- `AltitudeCorridorSystem`: только live-позиции на входе
  (`GetActiveCorridor`/`ValidateAltitude`). Правок не надо.

## 3. План авторебейса (T-FO09B)

Факты из кода: порог `_threshold=256м`, квант `_quantum=256м`
(slice:32–33); `OriginRebasePlan.TryCreate` возвращает false внутри порога
→ естественный гистерезис (остаток после сдвига < порога, повторного
срабатывания нет); `Rejected(no_rebase_plan)` уже честно покрывает «рано».

Реализация (один файл, `GlobalMotionControlledRebaseSlice`):

1. `Update`: рядом с опросом F8 — таймер (`_autoCheckIntervalSec=5`,
   serialize) + `_autoCooldownSec=35` после Completed/Rollback
   (35 > 30с кулдауна палуб 06DF — палубы успевают осесть).
2. Условия срабатывания (всё сервер, `IsServer`; по умолчанию
   `_autoRebaseEnabled=true`, serialize для отключения):
   `TryCreate(_frame, playerRoot.position, ...)` true → тот же
   `TryRequestUserControlled`-путь с `reason="auto_threshold"`.
3. Маркер `Requested` уже несёт reason — авто/ручной различимы в логе.
4. Безопасность: сдвиг в полёте/бою/на палубе доказан серией ф8_15–ф8_21;
   отдельных запретов не вводим (F8-эквивалентность).
5. Rollback-путь не меняется; флаговая механика (DB/DC/DD) уже защищает
   от повторных применений.

Тест пользователя: отлёт >256м от origin → в логе `Requested(auto_threshold)`
→ `Completed`, джиттер ушёл без нажатия F8. Отключение: снять галку
`Auto Rebase Enabled` на slice в BootstrapScene (сцена НЕ правится кодом —
только инспектор пользователем).

Риски: лишние сдвиги при телепортах (портал на 10км → один сдвиг, штатно);
колебание на границе порога (гистерезис TryCreate держит); F9 после авто —
штатный rollback. Всё покрывается существующими маркерами.
