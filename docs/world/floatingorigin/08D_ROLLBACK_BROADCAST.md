# T-FO08D — Broadcast отката вторым клиентам (-T)

Date: 2026-09-13. Найдено статическим аудитом клиентского пути (без лога:
топологии второго клиента пока нет).

## Дефект проектного предположения

Комментарий T-FO06DH утверждал: «Rollback вещественного сдвига не делает
(apply + restore = net zero) — broadcast только на success-пути». Это верно
для сервера, но неверно для вторых клиентов: их handler
(`OnRebaseShiftMessage`) уже применил +T к локальному состоянию (камера,
deathY/платформа, частицы, пикапы-carry, шторма). На F9 сервер возвращается
net zero, а клиенты навсегда остаются в +T — перманентный десинк локального
состояния, без ошибок в логе.

## Решение (1 вызов + правка комментария)

`Rollback`: при `restored` — `BroadcastRebaseShift(...,
-request.Plan.LocalTranslation, request.FrameGeneration)`. Handler
знаконезависимый (применяет translation как есть), -T отработает
симметрично. Сервер свои broadcast игнорирует
(`manager.IsServer → return`), двойного применения на хосте нет.
Только при `restored` — незавершённый откат ничего клиентам не сообщает.
Best-effort, результат во втором маркере `BroadcastShifted`.

## Проверка

1. Compile: `refresh_unity` (force + compile) — PASS; `read_console` —
   0 errors, 0 CS.
2. Runtime (user, когда будет топология): F8 → F9 на связке хост+клиент →
   два маркера `BroadcastShifted` (frame одинаковый, знаки разные);
   клиентские пикапы/камера net zero.
