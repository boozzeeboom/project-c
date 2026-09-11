# T-FO06AA — initial global spawn gate integration

Дата: 2026-09-11.

## Цель

Перейти от повторного baseline/movement capture к отдельной implementation slice и закрыть anomaly из T-FO06Z runtime follow-up 03:

```text
y≈1 → y=-7.41 → y≈2502.77
```

Переход не имел явных rebase markers.

## Причина

До этой правки `GlobalMotionSpawnLatch` имел только состояние `Released`. До первого `GlobalMotionActorState.RecordBaseline()` значение `GlobalMotionActorLink.Required` могло оставаться `false`. В этот короткий lifecycle window `NetworkPlayer.FixedUpdate` мог пройти legacy simulation, а `CharacterController` мог применить gravity до применения global baseline.

Это согласуется с последним capture: до baseline игрок начинал падать, затем sampled position резко оказывалась в подготовленном local frame около `y=2502.77`.

## Реализация

### 1. Explicit armed state

`GlobalMotionSpawnLatch` теперь имеет:

```text
Armed
Released
Applied
```

`Arm()` вызывается для global player до активации clone. `Reset()` очищает armed state только для нового lifecycle.

### 2. Preserve gate through NGO spawn

`NetworkPlayer.OnNetworkSpawn()` сохраняет факт заранее подготовленного global spawn:

```text
preservePreparedGlobalSpawn = _globalSpawnLatch.Armed
Reset()
Arm() when prepared
```

Таким образом, `OnNetworkSpawn` не открывает окно legacy movement между factory preparation и baseline release.

### 3. Fail-closed simulation

До exact `ReleaseGlobalInitialSpawn(binding)`:

- `CanSimulateInCurrentCoordinates == false`;
- `IsGlobalMotionReady == false`;
- input очищается;
- CharacterController не получает movement/gravity.

После release сохраняются существующие ownership/input/in-ship rules; изменение не телепортирует игрока и не меняет его gameplay state.

### 4. Ordered evidence markers

Добавлены маркеры:

```text
spawn.InitialGateArmed
spawn.FactoryPosePrepared
spawn.NetworkSpawn
player.FixedUpdate.blocked(reason=global_initial_spawn_gate)
spawn.InitialGateReleased
```

Они предназначены для следующего отдельного capture и позволяют отличить initial spawn gate от controlled rebase.

## Проверка

```text
check_compile_errors = No compile errors
```

Play Mode после изменения не запускался. Сцены, префабы, NavMesh и runtime configuration не изменялись.

## Граница

Эта правка:

- не добавляет automatic threshold trigger;
- не выполняет controlled rebase;
- не подключает `Apply/Rebuild/Validate/Publish`;
- не реализует native rollback;
- не является доказательством runtime rebase readiness.

Она закрывает только initial global spawn lifecycle boundary и подготавливает доказательный marker set для следующего runtime driver этапа.

## Следующий этап

Продолжить reviewed runtime driver integration с explicit user-controlled request и ordered `rebase.*` markers. Не запускать новый одинаковый baseline/movement capture до появления driver.

## Gate

```text
initialGlobalSpawnGate = IMPLEMENTED_COMPILE_PASS
initialPlacementAnomaly = NEEDS_USER_CAPTURE
controlledRebase = NOT_IMPLEMENTED
rollback = NOT_IMPLEMENTED
runtimeRebaseReadiness = NOT_READY
```
