# T-FO06BD — runtime manifest source compile gate

Дата: 2026-09-12
Статус: **COMPILE_VERIFIED / RUNTIME_NOT_CONNECTED**

## 1. Назначение

Зафиксировать первый code slice после dormant `T-FO06BC`: runtime source, который выполняет fail-closed census реальных ship/deck/passenger объектов и делегирует admission evidence отдельному owner-reviewed provider.

Этап не авторизует runtime rebase, не устанавливает native adapters и не изменяет `BootstrapScene`.

## 2. Реализация

Создан файл:

`Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseSceneRuntimeManifestSource.cs`

Source:

- реализует `IGlobalMotionRebaseLiveManifestRuntimeSource`;
- проверяет reviewed fixed identities для `CITY_STATIC`, `WORLD_ANCHORS`, `PLAYER_FRAME` и `CAMERA`;
- требует provider `IGlobalMotionRebaseRuntimeAdmissionEvidenceSource`;
- обнаруживает `ShipController`, `ShipDeckNav` и `NpcBrain` через Unity runtime census;
- требует ровно `22` ship roots, `20` deck-nav объектов и `20` passengers;
- сортирует runtime objects по имени для детерминированных ordinals;
- отклоняет duplicate/missing identities;
- проверяет deck registration, NavMesh validity/readiness;
- проверяет explicit passenger attachment, deck proxy и navigation readiness;
- не создаёт admission evidence самостоятельно.

Во время compile gate была найдена и исправлена ошибка namespace: `ShipController` находится в `ProjectC.Player`, поэтому добавлен `using ProjectC.Player;`.

## 3. Проверки

- `check_compile_errors`: **No compile errors**.
- `git diff --check`: **PASS** до подготовки коммита.
- BootstrapScene serialized binding: **не подтверждён**; component/source в сцену не устанавливались.
- Play Mode: **не запускался** по правилу проекта; runtime counts, NavMesh readiness и manifest publication не проверены.

## 4. Границы и отрицательное evidence

```text
runtime source code              = IMPLEMENTED
compile/domain reload            = PASS
bridge source binding            = NOT_CONNECTED
BootstrapScene mutation          = NONE
admission evidence provider      = NOT_IMPLEMENTED
live manifest publication        = NOT_OBSERVED
peer digest/count agreement      = NOT_OBSERVED
GlobalMotionNativeAdapterSet     = EMPTY / UNSEALED
FullTransaction                  = NOT_CONNECTED
runtime rebase readiness         = NOT_READY
```

Provider с фиктивными `true` значениями не создавался. До появления фактического provider, который доказывает adapter readiness, complete runtime proof и rollback readiness, source должен оставаться fail-closed.

## 5. Следующий gate

Следующий этап должен отдельно решить legitimate admission-evidence provider и только после его source-level проверки рассматривать serial binding в canonical `BootstrapScene`. Runtime acceptance выполняется пользователем через Play Mode; до этого live publication и readiness не заявляются.
