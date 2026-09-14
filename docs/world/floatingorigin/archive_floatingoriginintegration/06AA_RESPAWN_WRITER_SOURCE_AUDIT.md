# T-FO06AA — respawn writer source audit

Дата: 2026-09-11.

## Цель

Проверить исходники после `06AA_RUNTIME_CAPTURE_01`, где переход `y≈-9.18 → y≈2502.77` совпал с записями `PlayerRespawnTracker`.

Аудит read-only; runtime, сцены, префабы и конфигурация не изменялись.

## Проверенные компоненты

- `Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs`
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`
- `Assets/_Project/Scripts/World/RespawnManager.cs`
- `Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs`
- связанный combat-вызов из `PlayerTarget.cs`

## PlayerRespawnTracker

`PlayerRespawnTracker` находится на объекте игрока рядом с `NetworkPlayer` и `CharacterController`.

`Update()` работает только на сервере и запускает падение-respawn при выполнении условий:

```text
transform.position.y <= _deathY
fall duration >= _respawnDelay
_isRespawning == false
_respawnManager != null
```

Текущие значения по исходнику:

```text
_deathY = 0
_respawnDelay = 0.5 s
```

Также respawn может запускаться через:

- `RequestRespawnServerRpc()`;
- `ForceDefaultRespawnServerRpc()`;
- combat death через `RespawnWithHpRestore()`;
- ship/respawn selection logic.

## Позиционные writers

### 1. PlayerRespawnTracker.TeleportToClientRpc

Основной writer для server-authoritative respawn:

1. отключает `CharacterController`;
2. выполняет `transform.position = targetPosition`;
3. включает `CharacterController`;
4. сбрасывает velocity через `NetworkPlayer.ResetVelocity()`;
5. вызывает `Physics.SyncTransforms()`;
6. снимает локальный `_isRespawning`.

Именно этот путь согласуется с найденными в runtime записями:

```text
[PlayerRespawnTracker] Respawning player 0 ... pos=(39992.00, 2502.77, 40000.00)
[PlayerRespawnTracker] Client teleported to (39992.00, 2502.77, 40000.00)
```

### 2. NetworkPlayer.TeleportToPosition

Отдельный legacy/manual writer:

- отключает `CharacterController`;
- напрямую пишет `transform.position`;
- включает controller;
- сбрасывает velocity;
- вызывает client RPC.

Для global-coordinate actor он защищён `RejectLegacyCoordinateWrite`, поэтому его участие в текущем anomaly не доказано.

### 3. SubmitSwitchModeRpc

При выходе из корабля пишет позицию через `CurrentShip.GetExitPosition()`. Это отдельный gameplay path, не совпадающий по смыслу с падением ниже `_deathY`; участие в текущем capture не обнаружено.

## NGO / NetworkTransform

`NetworkPlayer` использует owner-authority режим для owner instance, а interpolation отключён.

Legacy `_hasServerPosition` correction фактически выключена порогом `positionCorrectionThreshold = 99999f`; global path дополнительно очищает `_hasServerPosition`.

По проверенным исходникам активный NGO position-correction writer, который мог бы объяснить переход к `y=2502.77`, не подтверждён.

## Относительный порядок

Исходники не дают строгой гарантии порядка между:

```text
server PlayerRespawnTracker.Update()
ClientRpc TeleportToClientRpc()
owner NetworkPlayer.Update()/ProcessMovement()
CharacterController.Move()
```

Оба runtime-компонента используют Unity lifecycle callbacks, но проектный код не содержит единого transaction/fence на кадр respawn. Поэтому остаются inconclusive:

- мог ли `CharacterController.Move()` выполниться до ClientRpc teleport в том же кадре;
- мог ли `ProcessMovement()` выполниться после teleport и изменить уже восстановленную позицию;
- выполнялся ли повторный movement/update до следующего sampled frame;
- был ли дополнительный NGO/respawn callback между `Client teleported` и следующим `FixedUpdate`.

`CharacterController.Move()` применяет дельту, а не абсолютную позицию, поэтому один только `Move` не объясняет скачок на тысячи метров. Нужна runtime instrumentation точного порядка callbacks и writers.

## Вывод

Source audit подтверждает `PlayerRespawnTracker.TeleportToClientRpc` как реальный и наиболее вероятный writer для `y≈2502.77`, потому что:

- runtime capture содержит непосредственные `PlayerRespawnTracker` respawn/teleport markers;
- исходник этого пути напрямую пишет `transform.position` в target respawn point;
- target совпадает с наблюдаемой координатой `(39992.00, 2502.77, 40000.00)`.

Однако единственный writer пока не доказан: точный callback order с `NetworkPlayer.Update`, `FixedUpdate`, NGO delivery и `CharacterController.Move` остаётся `INCONCLUSIVE`.

T-FO06AA initial spawn gate не следует расширять до respawn gate без отдельного runtime решения. Его текущая область остаётся initial spawn lifecycle.

## Следующий serial этап

Добавить narrow runtime evidence instrumentation без изменения movement semantics:

- marker в начале и конце `PlayerRespawnTracker.Update()`;
- marker перед `PerformRespawn()`;
- marker перед/после `TeleportToClientRpc()`;
- frame/time/fixedTime и target position;
- marker в начале/конце `NetworkPlayer.Update()` и перед `CharacterController.Move()`;
- writer identity для каждой прямой записи `transform.position`;
- correlation с NGO tick.

Инструментирование должно быть read-only по поведению и проверяться одним новым пользовательским Play Mode capture после компиляции.

## Gate

```text
respawnWriterSourceAudit = PASS_WITH_ORDER_LIMIT
initialGlobalSpawnGate = PARTIAL_RUNTIME_CONFIRMATION
initialPlacementAnomaly = REPRODUCED
likelyWriter = PlayerRespawnTracker.TeleportToClientRpc
writerExclusivity = INCONCLUSIVE
controlledRebase = NOT_OBSERVED
rollback = NOT_OBSERVED
runtimeRebaseReadiness = NOT_READY
```
