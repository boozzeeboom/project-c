# T-FO06Z — dormant ShipDeckNav/passenger runtime evidence adapter

Дата: 2026-09-11. Основание: `06Y_RUNTIME_CAPTURE_FOLLOWUP_04.md`, `06V_SHIP_DECK_NAV_READINESS_GATE.md` и текущая ошибка `Failed to create agent because it is not close enough to the NavMesh`.

## 1. Граница этапа

Этап добавляет только read-only runtime evidence в уже существующий `GlobalMotionRuntimeEvidenceProbe`. Он не включает rebase, не меняет participant admission и не исправляет NavMesh/crew поведение автоматически.

Не выполнялись: Play Mode, screenshots, network sessions, scene/prefab save, transform mutation, NavMesh mutation, `Apply/Rebuild/Validate/Publish`, live manifest publication и runtime rollback.

## 2. Реализовано

Добавлены read-only свойства:

- `ShipDeckNav.IsRegistered`;
- `ShipDeckNav.IsNavMeshInstanceValid`;
- `ShipDeckNav.NavMeshDataName`;
- `NpcBrain.IsDeckNavigationActive`;
- `NpcBrain.IsDeckProxyCreated`;
- `NpcBrain.IsDeckProxyOnNavMesh`;
- `NpcBrain.AttachedShipName`;
- `NpcBrain.AttachedShipNetworkObjectId`;
- `NpcBrain.DeckNavName`.

`GlobalMotionRuntimeEvidenceProbe` теперь при включённом capture дополнительно пишет:

- каждый найденный `ShipDeckNav`: NetworkObject identity, registration state, native instance validity, `IsReady`, NavMeshData name и `NavFrameOrigin`;
- каждого NPC с explicit ship attachment request: ship identity, attachment active state, resolved deck, proxy creation, proxy `isOnNavMesh` и deck-navigation state.

Эти данные только читаются и сериализуются в существующие строки `[T-FO06Y]`. Новые компоненты на сцены/префабы не добавлялись.

## 3. Почему это следующий узкий шаг

Предыдущие captures подтвердили `20` registration logs и `20` attachment/spawn sequences, но не позволили различить:

- валидный `NavMeshDataInstance` и только завершённый вызов `Register()`;
- созданный proxy и proxy, реально находящийся на NavMesh;
- активное explicit attachment и завершённую deck navigation readiness.

Новый capture должен использовать эти поля как runtime evidence, а не считать `Registered` или attachment request достаточными.

## 4. Проверки

- Unity compile: **No compile errors**.
- `validate_script` для трёх изменённых C# файлов: `0 errors`; инструмент сообщил только общий warning о возможных аллокациях строк при runtime logging.
- Пользовательский Play Mode capture через точечный Unity MCP: `[T-FO06Y]` records присутствуют.
- Active scene: canonical `BootstrapScene`; `hasCompilationErrors=false`.
- Сцены, префабы, NavMesh и runtime rebase этим follow-up не изменялись.

## 5. Runtime capture result

В sampled `[T-FO06Y]` snapshots подтверждены:

- `decks=20`; все deck entries имеют `reg=True`, `instance=True`, `ready=True`, `data=NavMesh-DeckNavSurface`;
- `passengerCount=20`; все passengers имеют `active=True`, `proxy=True`, `onNav=True`, `navActive=True`;
- `reg=False` и `onNav=False` point filters вернули `0` совпадений;
- отдельная проверка `navActive=False` была **INCONCLUSIVE** из-за MCP timeout, хотя все sampled snapshots содержат `navActive=True`;
- observed revisions: `1 → 2 → 3 → 4 → 5 → 6`;
- `adapter=Ready`, `baselinePlaced=True`, `grounded=True`, `ccGrounded=True`, `ccEnabled=True` после rebase/placement;
- движение игрока наблюдалось по изменению позиции.

## 6. Warning qualification

Во время запуска многократно наблюдалось:

```text
Failed to create agent because it is not close enough to the NavMesh
```

Финальные sampled snapshots всё равно показывают 20/20 passengers в состоянии `active=True`, `proxy=True`, `onNav=True`, `navActive=True`. Поэтому deck/passenger state классифицируется как observed readiness с transient startup warning, а не как полностью чистый NavMesh запуск.

## 7. Gate decision

```text
shipDeckNavRuntime = OBSERVED_PASS_WITH_TRANSIENT_NAVMESH_WARNINGS
passengerProvenance = OBSERVED_PASS_WITH_TRANSIENT_NAVMESH_WARNINGS
runtimeProofComplete = false
runtimeAdapterReady = false
rollbackReady = false
admittedParticipants = 0
liveManifestPublication = false
applyRebuildValidatePublishConnected = false
runtimeRebaseReadiness = NOT_READY
```

Capture закрывает evidence gap для sampled deck/passenger state, но не разрешает concrete adapter activation, live manifest publication или общий runtime rebase. Rollback proof отсутствует; jump evidence в доступном sampled MCP window неполный.

`GroundPlane_0_0`, `FloatingOriginMP`, player-only shift и generic shared `SetParent` остаются исключёнными.

## 8. Следующий шаг

Отдельный serial gate должен проверить controlled rebase/post-rebase continuity, rollback evidence и participant/admission policy. До его завершения не подключать concrete adapters, live manifest или `Apply/Rebuild/Validate/Publish`.

## 9. Состав этапа

- `Assets/_Project/Scripts/Ship/ShipDeckNav.cs`;
- `Assets/_Project/Scripts/AI/NpcBrain.cs`;
- `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRuntimeEvidenceProbe.cs`;
- `06Z_SHIP_DECK_RUNTIME_EVIDENCE_ADAPTER.md/.json`;
- `06Z_RUNTIME_CAPTURE_FOLLOWUP_01.md/.json`;
- обновления roadmap и `Assets/_Project/Docs/ITERATIONS.md`.

Сцены и префабы этим этапом не изменялись.
