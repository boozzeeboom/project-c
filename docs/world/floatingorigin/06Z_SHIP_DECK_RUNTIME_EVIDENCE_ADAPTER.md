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
- Play Mode и screenshots не выполнялись.

## 5. Gate decision

```text
runtimeProofComplete = false
runtimeAdapterReady = false
rollbackReady = false
admittedParticipants = 0
liveManifestPublication = false
applyRebuildValidatePublishConnected = false
shipDeckNavRuntime = UNVERIFIED_UNTIL_USER_CAPTURE
passengerProvenance = UNVERIFIED_UNTIL_USER_CAPTURE
```

Этот этап не устраняет jitter и не доказывает runtime rebase readiness. `GroundPlane_0_0`, `FloatingOriginMP`, player-only shift и generic shared `SetParent` остаются исключёнными.

## 6. Следующий шаг

Пользовательский serial capture из canonical `BootstrapScene` с включённым `Capture Enabled` должен проверить в строках `[T-FO06Y]` для каждого ship/crew:

1. `reg=true`, `instance=true`, `ready=true`;
2. proxy `created=true` и `onNav=true`;
3. `active=true`, resolved deck и стабильный ship NetworkObject identity;
4. сохранение этих состояний после controlled movement и отдельного deck/passenger участка.

До появления такого evidence не подключать concrete adapters, live manifest, `Apply/Rebuild/Validate/Publish` или runtime rebase.

## 7. Состав этапа

- `Assets/_Project/Scripts/Ship/ShipDeckNav.cs`;
- `Assets/_Project/Scripts/AI/NpcBrain.cs`;
- `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRuntimeEvidenceProbe.cs`;
- этот отчёт;
- `06Z_SHIP_DECK_RUNTIME_EVIDENCE_ADAPTER.json`;
- обновления roadmap и `Assets/_Project/Docs/ITERATIONS.md`.

Сцены и префабы этим этапом не изменялись.
