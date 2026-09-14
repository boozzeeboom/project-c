# T-FO06Y — runtime instrumentation probe for movement, camera and ordering

Дата: 2026-09-11.

## 1. Граница этапа

Подготовлен dormant, user-controlled instrumentation slice для изолированного `NetworkPlayer_GlobalPilot`. Инструментация не запускает Play Mode, не выполняет rebase, не публикует live manifest и не меняет participant admission.

Runtime rebase остаётся запрещённым до закрытия NavMesh/passenger, camera ownership/history, NGO/physics ordering, baseline continuity и native rollback gates.

## 2. Изменения

Создан:

- `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRuntimeEvidenceProbe.cs`

Изменены только диагностическими read-only hooks:

- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`
- `Assets/_Project/Scripts/Core/SpringArmCamera.cs`
- `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionReplicator.cs`
- `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionPoseAdapter.cs`

В isolated prefab добавлен dormant component:

- `Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab`
- `ProjectC.World.FloatingOrigin.Network.GlobalMotionRuntimeEvidenceProbe`

`_captureEnabled` по умолчанию `false`; без явного включения probe не пишет runtime evidence.

## 3. Что фиксируется

Probe агрегирует события одного кадра в одну строку `[T-FO06Y]` и сохраняет монотонный sequence для различения порядка:

- `player.FixedUpdate.begin/end` — fixed-step boundary, локальная позиция, grounded и velocity;
- `movement.CharacterController.Move.before/after` — фактический единый Move, `isGrounded`, platform carry и результат позиции;
- `movement.PlatformCarry` — имя платформы и применённая delta;
- sample `CharacterController` — `isGrounded`, enabled, velocity, `_onPlatform`, platform delta;
- sample Animator — enabled, `applyRootMotion`, `deltaPosition`, `deltaRotation`;
- `ngo.NetworkTick` — server tick и active/spawned state;
- `ngo.ControlAccepted` — revision, baseline sequence и binding generations;
- `baseline.AcknowledgeApplied`, `baseline.ActorApplied`, `baseline.AdapterReady` — initial/accepted baseline continuity points;
- `physics.SyncTransforms.begin/end` — ordering относительно baseline pose write;
- `camera.LateUpdate.begin/end/skip` — target, lag target, collision history и camera update boundary;
- sample camera — active `Billboard.ActiveCamera`, target name/hash, lag state, collision state и camera position;
- sample motion — control revision/sequence/binding и adapter status/baseline placement.

Sample interval по умолчанию — каждые 5 кадров; event limit — 128 событий на кадр. Probe не изменяет Transform, Rigidbody, CharacterController, Animator, NGO state, camera state или physics state.

## 4. Static verification

- Unity compile: **No compile errors** после исправления диагностического local-name conflict, `Billboard` namespace и Unity 6 obsolete `GetInstanceID` API.
- Pilot prefab metadata: компонент probe присутствует на root рядом с `NetworkPlayer`, `CharacterController`, `GlobalMotionReplicator` и `GlobalMotionPoseAdapter`.
- Play Mode: **не запускался автоматически**.
- Runtime records: **ещё отсутствуют**.

## 5. User-controlled capture gate

Для следующего ручного запуска пользователь должен:

1. Открыть canonical `Assets/_Project/Scenes/BootstrapScene.unity`.
2. Запустить Host обычным способом.
3. В Play Mode выбрать `NetworkPlayer_GlobalPilot(Clone)` и включить `Capture Enabled` у `GlobalMotionRuntimeEvidenceProbe`.
4. Выполнить короткие отдельные отрезки idle, walk/run и jump на текущей дальней позиции.
5. Экспортировать строки `[T-FO06Y]` вместе с обычным Console Log.

Без пользовательского capture эти boundaries остаются **UNVERIFIED**. Включение probe не является разрешением на runtime rebase.

## 6. Решение этапа

Instrumentation slice готов и compile-verified. Он даёт отдельные markers для movement/CharacterController/Animator, platform carry, NGO tick/baseline, physics synchronization и camera ownership/history, но доказательства runtime ещё не получены.

`runtimeProofComplete=false`, `runtimeAdapterReady=false`, `rollbackReady=false`, `admittedParticipants=0` остаются неизменными. `Apply/Rebuild/Validate/Publish`, native frame mutation, `FloatingOriginMP`, player-only shift и generic shared `SetParent` не подключались.
