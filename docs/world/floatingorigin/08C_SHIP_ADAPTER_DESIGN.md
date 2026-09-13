# T-FO08C — Ship adapter: проект (rebind пилота через ParentLocal-поток)

Date: 2026-09-13. Design-first: кода нет, реализация следующим gate после
ревью. Мотивация: T-FO07J доказал, что rebind посаженного пилота упирается
в `TryGetActor(ship)` — корабль не registered actor.

## Что уже есть (проверено по коду, не предположения)

- `GlobalMotionWorld.StartParentStream(actor, authority, parent, ...)`: требует
  `parent.World == this`, `parent.IsBaselineReady`, один `Frame` у обоих.
- `GlobalMotionWorld.ReactivateFromCurrentPose`: если у актора есть
  parent-NetworkObject — ищет его через `TryGetActor(parentObjectId)` и идёт
  в `StartParentStream`; иначе — World-поток. Т.е. весь путь пилота уже
  написан, не хватает только регистрации корабля.
- `ShipController` (`Scripts/Player/ShipController.cs:41`):
  `NetworkBehaviour, IGlobalMotionActorParticipant`, движение
  server-authoritative (`IsServer` гарды), `Rigidbody` (`_rb`), уже есть
  `Coordinates` (`GlobalMotionActorLink`), `CanSimulateInCurrentCoordinates`,
  `ConfirmNativePrepared`. Корабль уже говорит на половине протокола.

## Scope реализации (предложение)

1. Регистрация: при `OnNetworkSpawn` корабля (сервер) — `RegisterActor`
   ship-адаптера по `NetworkObjectId`; при деспавне — `UnregisterActor`.
2. Адаптер: либо `GlobalMotionPoseAdapter` компонентом на префабе корабля,
   либо мост `ShipController → PoseAdapter` (предпочтительно мост: меньше
   инвариантов трогаем, `CanApplyGlobalBaseline`/`IsGlobalMotionReady`
   уже частично реализованы через `IGlobalMotionActorParticipant`).
3. Поток корабля: `StartWorldStream` с `GlobalMotionAuthority.Owner(?)` —
   вопрос: движение server-authoritative (`_rb` на сервере), владелец-пилот
   лишь шлёт input через ServerRpc. Значит authority серверная; решить
   при реализации (`Owner` vs `Server` — смотреть `GlobalMotionAuthority`
   enum и кто пишет baseline корабля).
4. Slice 07B: для кораблей те же drain→shift→rebind; для пилота
   `ReactivateFromCurrentPose` сам свернёт в `StartParentStream`.
5. Rollback: симметрично; корабль-участник и так двигается позицией,
   поток — best-effort как у игроков.

## Риски (честно)

- `Rigidbody` + `Joint` на корабле: executor строка 249 исключает такие
  компоненты из нативных записей — ship baseline может упереться в те же
  preflight-стены, что пилот сейчас. Спасает то, что корабль — корень
  (нет сетевого предка), т.е. World-ветка чистая.
- `CanStart` требует `actor.Transport.IsSpawned` и текущий `Frame` —
  порядок спавна корабля vs старта мира проверить при реализации.
- NPC-корабли (`NpcShipController`): тот же путь, но спавн/деспавн
  динамический — регистрация обязана пережить recall/docking.
- Объём: это первый gate с новым lifecycle (регистрация на спавн),
  а не сдвиг данных. Тест: F8 в полёте → `ActorRebound(ok=True)`.

## Решение по authority — отложить до реализации

Нужен построчный аудит `ShipController` движения (кто пишет `_rb`:
сервер в `FixedUpdate`? интерполяция на клиентах?) + enum
`GlobalMotionAuthority`. Без этого выбор authority — гадание.

## Вердикт T-FO08C (2026-09-13): ОТЛОЖЕН со структурным доказательством

Построчный аудит `GlobalMotionPoseAdapter` против `ShipController`:

1. `HasCompetingWriter()` (PoseAdapter.cs:435): enabled `NetworkTransform`
   на том же GO → `Bind` невозможен. Корабль: `NetworkTransform
   (ServerAuthority)` (ShipController.cs:34) — штатная репликация движения.
   Ship-adapter требует ЗАМЕНЫ репликации корабля (NT → Replicator-потоки) —
   это миграция подсистемы, не малый gate.
2. `SupportedStructure()` (408–416): `_joints.Count != 0 → false`,
   больше одного Rigidbody в поддереве → false. Корабли с модулями/
   составными частями рискуют не пройти.
3. `Bind` (52–69): требует `GlobalMotionReplicator` на префабе,
   `_coordinatesRequired=true`, `!SynchronizeTransform`, match физсцены —
   хирургия префабов кораблей + bootstrap.

Итого: rebind в полёте остаётся на честном отказе `stream_refused`
(07J, стабильно, полёт идёт). Возврат к вопросу — только как отдельная
v2-миграция репликации кораблей с топологией для тестов (второй клиент),
не в рамках текущей серии. Без кода.
