# T-FO06Y — user runtime capture follow-up

Дата: 2026-09-11. Источник: предоставленный пользователем Unity Console export от `2026-09-11 18:49:19`, `768` записей, примерно `15 секунд` геймплея.

## 1. Граница этапа

Проверен предоставленный Host Play Mode log после подготовки dormant `GlobalMotionRuntimeEvidenceProbe`. Анализ выполняется только по фактически присутствующим строкам; отсутствующие строки не считаются доказательством.

Код, сцены, prefabs, runtime state, participant admission и rebase transaction в рамках этого follow-up не изменялись.

## 2. Результат instrumentation capture

В предоставленном export отсутствуют строки с префиксом:

```text
[T-FO06Y]
```

Фактическое число записей probe: `0`.

Поэтому не получены требуемые ordered markers для:

- `NetworkPlayer.FixedUpdate` и `CharacterController.Move`;
- grounding и platform carry;
- Animator/root-motion state;
- `NetworkTickSystem.Tick`, accepted control и baseline acknowledgement;
- `Physics.SyncTransforms`;
- camera owner, target, lag и collision history.

Причина отсутствия records по этому export не устанавливается. Она совместима с тем, что `Capture Enabled` не был включён на runtime instance, либо строки probe не попали в экспорт. В любом случае runtime evidence отсутствует.

## 3. Что подтверждает этот log

### 3.1 Native/player startup — PASS для данного запуска

Подтверждены:

```text
[T-FO06G] Native scene readiness: ready=True;recorded=150;pending=0;unspawned=0;retired=0;nodes=150;blocker=<none>
[T-FO06G] Spawn plan ready: client=0;frame=1;position=Global(39992, 1, 40000)
[T-FO06G] CompletePlacement finished: object=NetworkPlayer_GlobalPilot(Clone);ready=True;baseline=True
[T-FO06G] SpawnAsPlayerObject called: client=0;object=NetworkPlayer_GlobalPilot(Clone)
[T-FO06L] Local pilot player ready;startup menus hidden=2;origin=(0,0,0);local=(39992.00, 1.00, 40000.00)
```

Это подтверждает startup/native preparation и initial placement только для этого запуска. В логе нет evidence runtime rebase или post-rebase baseline continuity.

### 3.2 ShipDeckNav и экипаж — INCONCLUSIVE

Наблюдаются `20` сообщений `ShipDeckNav:... Registered at ...`, `20` explicit attachment requests и `20` named crew spawns.

Одновременно повторяются:

```text
Failed to create agent because it is not close enough to the NavMesh
```

Источники предупреждений:

- `Assets/_Project/Scripts/Ship/ShipDeckNav.cs:200`;
- `Assets/_Project/Scripts/AI/NpcBrain.cs:688`;
- `Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs:154`.

Поэтому valid `NavMeshDataInstance`, proxy-agent readiness, `isOnNavMesh`, completed attachment и passenger provenance не доказаны.

## 4. Proof matrix

| Proof requirement | Result | Основание |
|---|---|---|
| Player movement / CharacterController | **UNVERIFIED** | Нет строк `[T-FO06Y]`. |
| Grounding / platform carry | **UNVERIFIED** | Нет `PlatformCarry` или grounding samples. |
| Animator/root motion | **UNVERIFIED** | Нет Animator samples. |
| NGO tick/control/baseline ordering | **UNVERIFIED** | Нет `NetworkTick`, `ControlAccepted` или acknowledgement markers. |
| Physics ordering | **UNVERIFIED** | Нет `Physics.SyncTransforms` markers. |
| Camera ownership/history | **UNVERIFIED** | Нет camera markers и history samples. |
| ShipDeckNav readiness | **INCONCLUSIVE** | Registration есть, но NavMesh agent failures повторяются. |
| Passenger provenance | **INCONCLUSIVE** | Requests/spawns есть, completed attachment record отсутствует. |
| Unity-state rollback | **UNVERIFIED** | Transaction не запускалась и records отсутствуют. |

## 5. Jitter and admission decision

Пользовательский capture не дал instrumentation evidence, поэтому источник сохраняющегося jitter остаётся **INCONCLUSIVE** между CharacterController, platform carry, Animator, camera, NGO interpolation и float precision.

Fail-closed решение сохраняется:

```text
runtimeProofComplete = false
runtimeAdapterReady = false
rollbackReady = false
admittedParticipants = 0
```

`Apply/Rebuild/Validate/Publish`, native frame mutation, `FloatingOriginMP`, player-only shift и generic shared `SetParent` не подключались.

## 6. Следующий serial gate

Повторить user-controlled capture с явным включением `Capture Enabled` на runtime instance `NetworkPlayer_GlobalPilot(Clone)` и проверить наличие `[T-FO06Y]` строк непосредственно перед экспортом. Нужны отдельные отрезки idle, walk/run и jump на дальней позиции.

До появления этих records нельзя объявлять runtime proof complete или подключать concrete adapters, live manifest и runtime rebase.
