# T-FO06Y — third user runtime capture follow-up

Дата: 2026-09-11. Источник: предоставленный пользователем Unity Console export от `2026-09-11 18:56:49`, `1319` записей.

## 1. Результат capture

В третьем export присутствуют строки с префиксом:

```text
[T-FO06Y]
```

Evidence window начинается с `frame=2958`, `fixedTime=44.5200` и продолжается до последнего наблюдаемого probe frame `3324`, `fixedTime=51.9800`.

Runtime pilot:

```text
NetworkPlayer_GlobalPilot(Clone)
```

На всём наблюдаемом окне сохранялась одна binding:

```text
5641052402999880809/94/1/1/2
```

Probe сообщал:

```text
adapter=Ready
baselinePlaced=True
```

Это закрывает предыдущую неопределённость о наличии instrumentation records, но не означает готовность runtime adapter или возможность выполнять rebase.

## 2. Подтверждённые runtime channels

В capture наблюдаются:

- `NetworkPlayer.FixedUpdate.begin`;
- `CharacterController.Move.before/after`;
- `grounded=True`, `ccGrounded=True`, `ccEnabled=True`;
- стабильная позиция игрока `playerPos=(39992.000, 2502.165, 40000.000)`;
- скорость `vel=(0.000, -2.000, 0.000)`;
- `onPlatform=False`, `platform=<none>`, `platformDelta=(0,0,0)`;
- `inShip=False`;
- `animatorEnabled=True`, `rootMotion=True`, `deltaPos=(0,0,0)`, `deltaRot=(0,0,0)`;
- NGO `NetworkTick` в диапазоне `tick=1269…1494`;
- `ControlAccepted` для revisions `42…50`;
- `spawned=True`, `active=True`, `server=True`;
- camera `ThirdPersonCamera_0`, `activeOwner=True`, target `NetworkPlayer_GlobalPilot(Clone)`.

После `frame=3159` в полном capture появляются полноценные пары `camera.LateUpdate.begin/end`. До этого преобладает `camera.LateUpdate.skip`.

Camera collision history изменяется при практически неизменном target. Зафиксировано смещение camera position примерно от:

```text
(39992.210, 2502.785, 39997.050)
```

до:

```text
(39994.360, 2502.785, 39998.140)
```

Это подтверждает наличие runtime camera history activity, но не доказывает continuity после rebase.

## 3. Runtime ordering observations

В capture присутствуют повторные `player.FixedUpdate.begin` в одном и том же frame, включая наблюдаемые frames `2958`, `2968`, `2989`, `2991`, `2998`, `3000` и `3002`. Также в отдельных кадрах наблюдались несколько `ngo.NetworkTick`.

В экспортированном probe context значение `inFixed=False` сохранялось. Это фиксируется как наблюдаемая scheduling/order anomaly, но её причина по одному capture не устанавливается. Нельзя выводить из этого доказательство двойного физического шага, повторной симуляции или конкретного владельца вызова.

## 4. Отсутствующая evidence

В capture не найдены доказательства:

- `Physics.SyncTransforms.begin/end`;
- `baseline.AcknowledgeApplied`;
- `baseline.ActorApplied`;
- post-rebase baseline continuity;
- controlled movement, jump или изменение позиции;
- moving-platform carry;
- ship passenger state;
- фактического runtime rebase;
- rollback readiness.

Позиция сохранилась около `(39992, 2502, 40000)`, `origin=(0,0,0)`, поэтому этот capture не является post-rebase проверкой.

NavMesh warning сохраняется, включая:

```text
Failed to create agent because it is not close enough to the NavMesh
```

Поэтому deck/passenger gate остаётся `INCONCLUSIVE`. Disconnect и shutdown в конце запуска не являются доказательством rollback readiness.

## 5. Gate decision

```text
runtimeProofComplete = false
runtimeAdapterReady = false
rollbackReady = false
admittedParticipants = 0
liveManifestPublication = false
applyRebuildValidatePublishConnected = false
```

Третий capture переводит T-FO06Y из состояния `NO_PROBE_RECORDS` в состояние `PROBE_RECORDS_PRESENT;RUNTIME_ORDERING_INCONCLUSIVE`. Он подтверждает работу части player/camera/NGO instrumentation, но не закрывает required proof contract.

Concrete adapters, live manifest publication, `Apply/Rebuild/Validate/Publish`, `FloatingOriginMP`, player-only shift и generic shared `SetParent` остаются заблокированы.

## 6. Следующий serial gate

Следующий gate должен быть отдельным user-controlled capture с:

1. полноценным camera window с самого начала запуска;
2. явными `Physics.SyncTransforms.begin/end`;
3. `baseline.AcknowledgeApplied` и `baseline.ActorApplied`;
4. controlled movement;
5. отдельным controlled rebase/post-rebase capture;
6. повторной проверкой deck/passenger readiness без NavMesh blocker.

До закрытия этих каналов runtime proof и admission остаются fail-closed.
