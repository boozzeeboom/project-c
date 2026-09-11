# T-FO06U — user Play Mode runtime-proof audit

Дата: 2026-09-11. Источник: предоставленный пользователем Unity Console export от `2026-09-11 17:13:54`, `868` записей, длительность примерно `15 секунд`.

## 1. Граница этапа

Проверен предоставленный runtime log против dormant proof contract из T-FO06T и fail-closed admission policy из T-FO06S. На этом этапе код, сцены, prefabs, catalog/profile и runtime state не изменялись. Анализ выполнен по фактически присутствующим строкам лога; отсутствующие строки не считаются доказательством.

Generated report:

- `docs/world/floatingorigin/06U_USER_PLAYTEST_RUNTIME_PROOF_AUDIT.json`

## 2. Подтверждённые runtime результаты

### 2.1 Native scene preparation — PASS

Многократно зафиксирована полная готовность native scene scope:

```text
[T-FO06G] Native scene readiness: ready=True;recorded=150;pending=0;unspawned=0;retired=0;nodes=150;blocker=<none>
```

### 2.2 Host/player startup — PASS для этого запуска

Подтверждены:

```text
worldRunning=True;scenePrepared=True;sceneReady=True
CompletePlacement finished;ready=True;baseline=True
SpawnAsPlayerObject called
origin=(0,0,0);local=(39992.00, 1.00, 40000.00)
```

Это закрывает startup/player gate данного запуска, но не является доказательством floating-origin rebase.

### 2.3 Ship and crew presence — observed, not admitted

В логе наблюдаются `20` сообщений `ShipDeckNav:... Registered at ...`, а также `20` запросов explicit ship-deck attachment и `20` сообщений о spawn named captains.

Это подтверждает, что ship/deck/crew lifecycle был активен в течение запуска. Однако рядом присутствуют повторяющиеся ошибки:

```text
Failed to create agent because it is not close enough to the NavMesh
```

Ошибки идут из `ShipDeckNav.cs:200`, `NpcBrain.cs:688` и `ShipCrewSpawner.cs:154`. Поэтому runtime NavMesh readiness и завершённая passenger provenance остаются **INCONCLUSIVE**.

## 3. Proof contract result

| Proof requirement | Result | Основание |
|---|---|---|
| Camera ownership/history | **UNVERIFIED** | В логе нет runtime owner/history capture. |
| ShipDeckNav registration | **INCONCLUSIVE** | 20 registrations есть, но NavMesh agent creation repeatedly fails. |
| Passenger provenance | **INCONCLUSIVE** | Attachment requested/spawned, completed attachment state не записан. |
| NGO tick ordering | **UNVERIFIED** | Нет tick-order capture относительно rebase phases. |
| Physics ordering | **UNVERIFIED** | Есть collision log, но нет rebase/physics ordering capture. |
| Network baseline continuity | **UNVERIFIED** | `baseline=True` относится к initial placement, не к post-rebase continuity. |
| Unity-state rollback | **UNVERIFIED** | Apply/rollback transaction не запускалась. |

## 4. Admission decision

T-FO06S admission остаётся **FAIL-CLOSED**:

- `runtimeProofComplete = false`;
- `runtimeAdapterReady = false`;
- `rollbackReady = false`;
- live manifest не публиковался;
- admitted participants: `0`.

Наличие catalog match, native readiness, initial player baseline или `ShipDeckNav Registered` не даёт права на runtime participant admission.

## 5. Warnings вне текущего proof gate

В логе также есть предупреждения о Meziy modules, пустом `_boxPrefabs`, раннем отсутствии `CombatServer`/`MetaRequirementRegistry`, missing ResourceNode references, `PlayerTarget` HP initialization, отсутствующих Animator parameters и отсутствующем `ShipDamageConfig`.

Они зафиксированы как отдельные gameplay/runtime warnings и не исправляются массово в T-FO06U: их причинная связь с rebase proof этим capture не доказана.

## 6. Decision

Пользовательский capture закрывает **PASS** для static pilot native preparation и initial Host/player startup.

Runtime rebase proof — **INCOMPLETE**. Особенно блокируют следующий admission gate:

1. NavMesh/ShipDeckNav failure при создании agents;
2. отсутствие camera ownership/history capture;
3. отсутствие completed passenger provenance;
4. отсутствие NGO tick/physics/baseline ordering capture;
5. отсутствие rollback attempt и доказательства Unity-state restore.

Runtime rebase readiness остаётся **NOT READY**. `GroundPlane_0_0`, `FloatingOriginMP`, player-only shifting и generic shared `SetParent` остаются исключёнными.

## 7. Следующий шаг

Не исправлять несвязанные gameplay warnings в рамках этого этапа. Следующий integration slice должен отдельно подготовить минимальный user-controlled runtime capture для camera/deck/NGO/physics/baseline boundaries и зафиксировать NavMesh agent failure как отдельный blocker до admission.
