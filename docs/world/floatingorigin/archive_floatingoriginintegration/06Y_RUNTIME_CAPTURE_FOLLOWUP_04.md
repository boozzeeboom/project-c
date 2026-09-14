# T-FO06Y — fourth user runtime capture follow-up

Дата: 2026-09-11.
Источник: точечный запрос Unity MCP Console во время активного Play Mode; полный Console export не использовался.

## 1. Состояние редактора

На момент анализа:

```text
playMode=true
activeAssetPath=Assets/_Project/Scenes/BootstrapScene.unity
selectedGameObject=NetworkPlayer_GlobalPilot
hasCompilationErrors=false
```

В Console найдено `126` агрегированных `[T-FO06Y]` записей на момент последней страницы; evidence продолжают поступать, поэтому этот отчёт фиксирует наблюдаемое окно, а не завершение всей пользовательской сессии.

## 2. Baseline и binding

Evidence window: `frame=271..1381`, `fixedTime=2.8200..31.8600`.

Binding на всём точечно прочитанном окне:

```text
5044784805265246929/94/1/1/2
```

На frame `271` зафиксирован полный baseline ordering:

```text
ngo.ControlAccepted(revision=1 active=True sequence=0)
→ physics.SyncTransforms.begin(baseline=True role=Authority)
→ physics.SyncTransforms.end(baseline=True role=Authority)
→ baseline.ActorApplied
→ baseline.AcknowledgeApplied(revision=1 sequence=0)
→ baseline.AdapterReady(status=WaitingForActors)
→ camera.LateUpdate.begin/end
→ baseline.AdapterReady(status=Ready)
```

После этого snapshots продолжали сообщать `adapter=Ready` и `baselinePlaced=True`.

## 3. Player movement

### Initial placement / grounding anomaly

Зафиксирована та же нетипичная последовательность:

```text
frame 271: pos=(39992.00, 1.00, 40000.00), grounded=False
frame 297: pos≈(39992.02, -6.00, 40000.00)
frame 298: pos≈(39992.01, 2502.77, 40000.00)
frame 300: pos≈(39992.02, 2502.17, 40000.00), ccGrounded=True
```

Причина скачка из отрицательной `y` в район `2502` по этому capture не установлена.

### Jump

В окне `frame=330..395` наблюдалось вертикальное движение:

```text
frame 330: pos.y=2502.542, velocity.y=17.889
frame 345: pos.y=2507.801, velocity.y=10.264
frame 370: pos.y=2509.958, velocity.y=-3.251
frame 395: pos.y=2503.013, velocity.y=-16.749
```

Это подтверждает реальный CharacterController jump/fall участок, без root-motion displacement (`deltaPos=(0,0,0)`).

### Controlled movement

Ненулевые горизонтальные motion и изменение позиции обнаружены в отдельных sampled frames:

```text
frame 412: pos=(39992.07, 2502.17, 40000.71), motion.z=+0.12
frame 449: pos=(39990.21, 2502.17, 40001.48), motion.z=-0.13
frame 482: pos=(39991.30, 2502.17, 40002.87), motion=(-0.23, -0.12, -0.17)
frame 522: pos=(39992.68, 2502.17, 39999.43), motion=(+0.09, -0.03, +0.02)
```

После движения позиция стабилизировалась около:

```text
(39992.77, 2502.165, 39999.45)
```

На frame `560` состояние было `grounded=True`, `ccGrounded=True`, `ccEnabled=True`, `onPlatform=False`, `inShip=False`.

## 4. NGO и camera

- Binding оставался неизменным.
- `ControlAccepted` наблюдался для revisions `1..30`.
- `NetworkTick` продолжался до tick примерно `889` в последней прочитанной записи.
- Повторные `FixedUpdate.begin` и несколько `NetworkTick` в одном frame сохраняются; причина scheduling anomaly остаётся **INCONCLUSIVE**.
- Полные `camera.LateUpdate.begin/end` наблюдались до frame `560`.
- Начиная примерно с frame `597` встречается `camera.LateUpdate.skip(target=NetworkPlayer_GlobalPilot[Clone] cursor=None)`.
- Target и lagTarget при этом сохранялись; потеря target не подтверждена.

## 5. Что точечно не найдено

По отдельным фильтрам Unity Console на момент запроса не найдены записи с текстом `rebase`, `rollback`, `ShipDeckNav` или `Failed to create agent`. Это отсутствие совпадений в текущем Console buffer, а не доказательство прохождения соответствующих gates.

Runtime rebase, post-rebase continuity, shutdown rollback, participant admission, live manifest publication и passenger/NavMesh readiness этим запросом не подтверждены.

## 6. Gate decision

```text
baselineOrderingConfirmed = true
physicsBaselineConfirmed = true
controlledMovementConfirmed = true
jumpConfirmed = true
bindingContinuityConfirmed = true
postMovementStable = true
runtimeRebaseConfirmed = false
postRebaseContinuity = false
rollbackReady = false
admittedParticipants = 0
liveManifestPublication = false
passengerNavMesh = INCONCLUSIVE
runtimeProofComplete = false
```

Итог: текущий capture закрывает baseline transition, baseline physics synchronization, jump, controlled movement и binding continuity. Он не закрывает runtime rebase, rollback, participant admission или passenger/NavMesh gate.

Код, сцены и префабы в рамках анализа не изменялись. Пользовательский Play Mode не останавливался агентом.
