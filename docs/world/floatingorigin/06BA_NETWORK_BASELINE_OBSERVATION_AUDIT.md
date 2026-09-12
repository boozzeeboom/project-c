# T-FO06BA — NetworkBaseline observation-only audit

Дата: 2026-09-12.

## 1. Назначение

Проверить полный пользовательский `[T-FO06Y]` log against the pure `T-FO06AZ` NetworkBaseline readiness/continuity contract, не создавая concrete adapter и не меняя `FullTransaction`.

Это serial evidence audit, а не runtime installation stage.

## 2. Источник и область аудита

Источник:

```text
Q:\Project-c_logs\01.txt
```

Анализированы только строки с `[T-FO06Y]`:

- `7995` строк;
- rendered frames `885–8879`;
- binding `4780900133407987940/94/1/1/2` сохраняется в наблюдаемом capture;
- максимальные значения в sampled stream: `revision=198`, `sequence=5227`, `NetworkTick=5942`.

## 3. Evidence по NetworkBaseline

| Требование T-FO06AZ | Наблюдение | Классификация |
|---|---|---|
| session/object/spawn binding | `4780900133407987940/94/1/1/2` | OBSERVED |
| initial control acceptance | `frame=885`, `revision=1`, `sequence=0` | CONFIRMED |
| physics baseline sync | `frame=885`, `SyncTransforms.begin/end` | CONFIRMED |
| actor baseline application | source line `3742`, `baseline.ActorApplied` | CONFIRMED |
| initial gate release | source line `3742`, `spawn.InitialGateReleased` | CONFIRMED |
| initial acknowledgement | source line `3742`, `baseline.AcknowledgeApplied` | CONFIRMED |
| tick continuity | stream до `NetworkTick=5942` | OBSERVED_INITIAL_STREAM |
| sequence/revision progression | `revision=1..198`, `sequence` до `5227` | OBSERVED_INITIAL_STREAM |
| post-rebase binding transition | отсутствует | NOT_PROVEN |
| restore capability | отсутствует | NOT_PROVEN |
| ownership/lifetime rollback | отсутствует | NOT_PROVEN |

## 4. Отрицательные доказательства

В полном файле отсутствуют совпадения для `rebase`, `rollback`, `manifest` и `admission`. Нет peer digest/count agreement, participant admission, controlled rebase, post-rebase acknowledgement lineage или rollback restore.

`baseline.AdapterReady(status=Ready)` и `baselinePlaced=True` подтверждают существующий initial-baseline pipeline. Они не являются доказательством native `NetworkBaseline` adapter, restorable capability или post-rebase continuity.

`247` сообщений `Failed to create agent because it is not close enough to the NavMesh` сохраняют отдельный ShipDeckNav/NavMesh lifecycle blocker.

## 5. Gate result

```text
NetworkBaseline identity observation     = CONFIRMED
initial baseline/acknowledgement         = CONFIRMED
initial tick/sequence continuity         = OBSERVED
observation-only classification          = ALLOWED
restorable classification                 = NOT_ALLOWED
controlled rebase continuity             = NOT_PROVEN
rollback capability                      = NOT_PROVEN
participant admission                    = NOT_READY
ShipDeckNav lifecycle                    = BLOCKED_BY_NAVMESH_WARNINGS
concrete NetworkBaseline adapter         = BLOCKED
GlobalMotionNativeAdapterSet             = EMPTY / UNSEALED
runtimeRebaseReadiness                   = NOT_READY
```

## 6. Decision

Evidence достаточно только для `ObservationOnly` interpretation of the NetworkBaseline stream. Оно не может быть передано в текущий `FullTransaction`, не создаёт `GlobalMotionNetworkBaselineNativeAdapter` и не разрешает регистрацию в `GlobalMotionNativeAdapterSet`.

Следующий admissible runtime gate остаётся peer manifest agreement + participant admission + controlled rebase/post-rebase capture + rollback evidence. До него BootstrapScene и runtime installation не изменяются.
