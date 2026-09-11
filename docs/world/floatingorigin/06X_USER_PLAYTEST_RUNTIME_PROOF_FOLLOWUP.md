# T-FO06X — user Play Mode runtime-proof follow-up

Дата: 2026-09-11. Источник: предоставленный пользователем Unity Console export от `2026-09-11 17:35:20`, `828` записей, длительность `20 секунд`.

## 1. Граница этапа

Проверен новый Host Play Mode capture против dormant proof contract T-FO06T и fail-closed admission policy T-FO06S. Код, сцены, prefabs, catalog/profile и runtime state в рамках анализа не изменялись. Пользователь сообщает: визуально игра работает, но jitter персонажа сохраняется.

Отсутствующие строки лога не считаются доказательством. Отдельно сохраняется пользовательское визуальное наблюдение; его точная составляющая — movement, Animator, camera, CharacterController, NGO interpolation, platform carry или floating-point precision — этим capture не изолирована.

Generated report:

- `docs/world/floatingorigin/06X_USER_PLAYTEST_RUNTIME_PROOF_FOLLOWUP.json`

## 2. Подтверждённые результаты

### 2.1 Native scene preparation — PASS

Лог многократно подтверждает закрытую native readiness:

```text
[T-FO06G] Native scene readiness: ready=True;recorded=150;pending=0;unspawned=0;retired=0;nodes=150;blocker=<none>
```

Это повторно подтверждает static pilot preparation, но не означает выполнения rebase transaction.

### 2.2 Host/player startup — PASS для данного запуска

Зафиксированы:

```text
worldRunning=True;scenePrepared=True;sceneReady=True
[T-FO06G] Spawn plan ready: client=0;frame=1;position=Global(39992, 1, 40000)
[T-FO06G] CompletePlacement finished: object=NetworkPlayer_GlobalPilot(Clone);ready=True;baseline=True
[T-FO06G] SpawnAsPlayerObject called: client=0;object=NetworkPlayer_GlobalPilot(Clone)
[T-FO06L] Local pilot player ready;startup menus hidden=2;origin=(0,0,0);local=(39992.00, 1.00, 40000.00)
```

`PlayerRespawnTracker` также сообщает телепорт к `(39992.00, 2502.77, 40000.00)`. В логе нет нового frame origin/rebase и нет перемещения local frame к игроку: он остаётся примерно в `40 км` от `(0,0,0)` по X/Z.

### 2.3 Ships, deck navigation and crew — observed, not admitted

В capture наблюдаются:

- `20` сообщений `ShipDeckNav:... Registered at ...` для именованных ships;
- `20` запросов explicit ship-deck attachment;
- `20` сообщений о spawn named captains/pilot.

Одновременно повторяются ошибки Unity:

```text
Failed to create agent because it is not close enough to the NavMesh
```

Они приходят из:

- `Assets/_Project/Scripts/Ship/ShipDeckNav.cs:200` при `NavMesh.AddNavMeshData`;
- `Assets/_Project/Scripts/AI/NpcBrain.cs:688` при создании `NavMeshAgent`;
- `Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs:154` при instantiate экипажа.

Поэтому строка `Registered`, запрос attachment и spawn экипажа не считаются доказательством valid `NavMeshDataInstance`, `proxyAgent.isOnNavMesh`, completed attachment или resolved passenger provenance.

## 3. Runtime proof contract

| Proof requirement | Result | Основание |
|---|---|---|
| Camera ownership/history | **UNVERIFIED** | Нет active owner, target lifetime, `Billboard.ActiveCamera` или history capture. |
| ShipDeckNav registration | **INCONCLUSIVE** | 20 registration logs есть, но повторяются NavMesh agent creation failures. |
| Passenger provenance | **INCONCLUSIVE** | Есть requests и spawns, нет completed attachment/provenance record. |
| NGO tick ordering | **UNVERIFIED** | Нет последовательности NGO tick относительно physics/rebase phases. |
| Physics ordering | **UNVERIFIED** | Нет capture `FixedUpdate`/physics sync/rebase ordering. |
| Network baseline continuity | **UNVERIFIED** | `baseline=True` относится к initial placement; post-rebase baseline отсутствует. |
| Unity-state rollback | **UNVERIFIED** | Apply/rollback transaction не запускалась. |

## 4. Jitter result

Пользовательское наблюдение `character jitter remains` принято как отрицательный runtime результат этого запуска.

Лог подтверждает важное ограничение: initial frame остаётся `origin=(0,0,0)`, а player находится в local coordinates около `(39992, 1, 40000)`. Поэтому этот capture не проверяет anti-jitter rebase и не даёт оснований заявлять исправление jitter.

Точная причина jitter остаётся **INCONCLUSIVE**. В логе нет instrumentation, которая разделяет player movement/prediction, Animator/root motion, CharacterController grounding, NGO interpolation, camera smoothing, platform carry и floating-point precision.

## 5. Admission decision

T-FO06S остаётся fail-closed:

- `runtimeProofComplete = false`;
- `runtimeAdapterReady = false`;
- `rollbackReady = false`;
- live manifest не публиковался;
- admitted participants: `0`;
- `Apply/Rebuild/Validate/Publish` и native frame mutation не выполнялись.

Static native preparation и initial Host/player startup для этого запуска — **PASS**. Runtime rebase readiness — **NOT READY**.

## 6. Warnings вне текущего gate

Повторяющиеся предупреждения о пустом `ShipCargoVisual._boxPrefabs`, ранней регистрации `CombatServer`/`MetaRequirementRegistry`, missing `ResourceNodeConfig` references, `PlayerTarget` HP initialization, отсутствующих Animator parameters `WorkVariant`/`Work` и отсутствующих ship modules зафиксированы как отдельные gameplay warnings. Их причинная связь с jitter этим логом не доказана; массовое исправление в T-FO06X не выполнялось.

## 7. Решение этапа

T-FO06X подтверждает повторяемый static pilot startup/player gate и фиксирует отрицательный визуальный результат по jitter. Он не закрывает ни один runtime proof gate и не разрешает participant admission.

Следующий шаг остаётся serial и user-controlled: подготовить отдельный instrumentation capture для camera owner/history, player movement/Animator/CharacterController, NGO tick/physics ordering и baseline continuity. NavMesh/passenger blocker сохраняется отдельно. До появления этих records нельзя подключать concrete adapters, `Apply/Rebuild/Validate/Publish` или общий transform shift.

`GroundPlane_0_0`, `FloatingOriginMP`, player-only shifting и generic shared `SetParent` остаются исключёнными.
