# T-FO06Z runtime capture follow-up 01 — ShipDeckNav/passenger evidence

Дата: 2026-09-11.

## 1. Граница проверки

Пользователь вручную выполнил Play Mode-тест из canonical `BootstrapScene`: движение и прыжок игрока. Проверка выполнена точечно через Unity MCP без загрузки полного Console Log в контекст.

Включался только runtime capture `GlobalMotionRuntimeEvidenceProbe` на `NetworkPlayer_GlobalPilot(Clone)`. `FloatingOriginMP`, concrete adapters, participant admission, live manifest и `Apply/Rebuild/Validate/Publish` не включались.

## 2. Editor/runtime state

- Play Mode: `true`.
- Active scene: `Assets/_Project/Scenes/BootstrapScene.unity`.
- Unity compilation errors: `0`.
- `[T-FO06Y]` records присутствуют.
- Ошибки `MCP-FOR-UNITY` с `ObjectDisposedException`/разорванным transport connection относятся к инструментальному каналу и не являются игровыми compile/runtime исключениями.

## 3. ShipDeckNav evidence

В sampled `[T-FO06Y]` snapshots зафиксировано:

```text
decks=20
```

Для всех 20 deck entries наблюдались:

```text
reg=True
instance=True
ready=True
data=NavMesh-DeckNavSurface
```

Точечный поиск `reg=False` вернул `0` совпадений.

## 4. Passenger evidence

В sampled snapshots зафиксировано:

```text
passengerCount=20
```

Для всех 20 explicit passengers наблюдались:

```text
active=True
proxy=True
onNav=True
navActive=True
```

Точечный поиск `onNav=False` вернул `0` совпадений. Отдельный MCP-фильтр `navActive=False` завершился timeout, поэтому этот поиск помечен как **INCONCLUSIVE**; при этом все sampled `[T-FO06Y]` snapshots содержат `navActive=True` для всех 20 passengers.

## 5. Player/rebase evidence

В runtime records подтверждены переходы ревизий `1 → 2 → 3 → 4 → 5 → 6` при сохранении:

```text
adapter=Ready
baselinePlaced=True
```

После rebase/placement игрок находился примерно в:

```text
(39992.00, 2502.17, 40000.00)
```

с состояниями:

```text
grounded=True
ccGrounded=True
ccEnabled=True
```

Движение подтверждено изменением позиции примерно до:

```text
(39990.43, 2502.17, 40001.40)
```

В доступном sampled-фрагменте положительный вертикальный импульс прыжка отдельно не выделен, поэтому jump evidence по MCP остаётся неполным, несмотря на пользовательское сообщение о выполненном прыжке.

## 6. Warning qualification

Во время запуска Unity Console многократно содержала:

```text
Failed to create agent because it is not close enough to the NavMesh
```

Предупреждения наблюдались примерно в период `19:39:37–19:42:45`. Финальные sampled snapshots всё равно показывают 20/20 passengers с `active=True`, `proxy=True`, `onNav=True`, `navActive=True`. Поэтому результат классифицируется как observed readiness с transient startup warning, а не как доказательство полностью чистого NavMesh запуска.

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

Этот capture закрывает evidence gap для sampled deck/passenger state, но не разрешает concrete adapter activation, live manifest publication или общий runtime rebase. Повторные NavMesh warnings, неполный jump evidence и отсутствие rollback proof сохраняют этап fail-closed.

## 8. Следующий этап

Следующий serial gate должен отдельно проверить concrete rebase boundary: controlled rebase/post-rebase continuity, rollback evidence и participant/admission policy. До его завершения не включать `FloatingOriginMP`, player-only shift, concrete adapters, live manifest или `Apply/Rebuild/Validate/Publish`.
