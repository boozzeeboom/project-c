# T-FO07B — Перепривязка живых акторов через сдвиг фрейма (drain → shift → rebind)

Date: 2026-09-13. Продолжение 07A: `WorldFrameShifted → frame_has_bound_actors=1`.

## Последовательность (success-путь, best-effort, маркеры на актора)

Заменяет прямой вызов `ShiftWorldFrameOrigins` (он остаётся для фреймов,
но вызывается внутри новой последовательности):

1. **Drain.** Сервер собирает адаптеры игроков (`ConnectedClients[].PlayerObject`
   → `GetComponent<GlobalMotionPoseAdapter>()`), запоминает `(adapter, frameId)`
   (`adapter.Frame.Id` до отвязки) и вызывает `Unbind()`. Поток останавливается
   (`StopServer`, reliable inactive control клиентам). Маркер `ActorDrained`.
2. **Shift.** Существующий `ShiftWorldFrameOrigins`: реестр + definition.
   Теперь bound нет — сдвиг проходит (`ok=True`).
3. **Rebind.** На актора: `Bind(world, frameId)` → `TryCaptureWorld` (уже новый
   фрейм: `newOrigin + local_new = old global`, захват правды заранее не нужен) →
   `world.StartWorldStream(adapter, Owner, truth, rot, scale, null)` →
   новый binding через `NextDiscontinuity` (lineage из протокола) →
   `PrepareBaseline` (поза уже на месте — запись no-op).
   Маркер `ActorRebound(ok=...)`.
4. **Rollback** акторов не трогает: позиции возвращены, binding не менялся,
   поток непрерывен (в окне apply публикаций не было — транзакция синхронна).

## Заранее известные ограничения (в маркерах, не молча)

- `gameRules=null`: проходит только publisher == ServerClientId. Пилот хоста —
  да; пилот второго клиента (Owner authority, чужой owner) — отказ
  `Initial global stream refused`-класса, его поток остаётся остановленным
  до следующего gate. Single-host не страдает.
- Ошибка rebind актора: best-effort, маркер `ActorRebound(ok=False:...)`,
  игра продолжается локально (движение — CharacterController, не поток).
  Поток этого актора down до ручного переподключения/респавна.
- Клиентские реплики увидят inactive → новый binding: пере-baseline
  на клиентах, рывка позы нет (global та же).

## Почему не трогаем ядро и координатор

Используются только существующие публичные пути (`Unbind/Bind/StartWorldStream`
— те же, что стартап пилота в `OnActorPostSpawn`). Новых контрактов,
изменений протокола и мутаций репликатора нет.

## Проверка

1. Compile: `refresh_unity` + `read_console` — 0 errors, 0 CS.
2. Play Mode (user, Host): F8 → `ActorDrained` → `WorldFrameShifted(id=1;ok=True)` →
   `ActorRebound(ok=True)`; ревизии потока продолжают расти (новый binding —
   discontinuity, не разрыв); игрок стоит/ходит; ошибок 0.
3. F9: без actor-маркеров (откат позиций, поток непрерывен).
