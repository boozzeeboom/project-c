# T-FO06Y — source analysis of runtime evidence gates

Дата: 2026-09-11. Основание: третий пользовательский capture от `2026-09-11 18:56:49` и исходники T-FO06Y probe/adapter/replicator/camera.

## 1. Цель этапа

Проверить, почему третий capture содержит player/camera/NGO evidence, но не содержит `Physics.SyncTransforms`, `baseline.AcknowledgeApplied` и `baseline.ActorApplied`, и отделить ожидаемое поведение instrumentation от неисправности probe.

Этап read-only: сцены, префабы, runtime state и координатная логика не изменялись.

## 2. Подтверждённые условия записи

### Probe activation and flush

`GlobalMotionRuntimeEvidenceProbe.RecordEvent(...)` записывает событие только когда существует активный probe и `_captureEnabled == true`. Буфер очищается после каждого `LateUpdate` flush. Строка `[T-FO06Y]` печатается на sample frame либо когда в текущем кадре были events/dropped events.

Следствие: отсутствие конкретной фазы в export означает только, что она не была вызвана во время активного capture window. Это не доказывает отсутствие вызова до включения capture или в другом runtime object.

### Baseline acknowledgement

`GlobalMotionReplicator.AcknowledgeBaselineApplied(...)` пишет `baseline.AcknowledgeApplied` только при переходе `_baselineApplied` из `false` в `true`. Повторное подтверждение уже применённого binding не создаёт новую evidence строку.

### Actor application

`GlobalMotionPoseAdapter.PrepareBaseline()` пишет `baseline.ActorApplied` только в ветке нового binding, когда `_hasApplied == false` либо binding изменился. При уже применённом binding выполняется revalidation без повторного `ActorApplied`.

### Physics synchronization

`GlobalMotionPoseAdapter.WritePose(...)` пишет `physics.SyncTransforms.begin/end` только при фактической записи baseline либо когда у объекта отсутствует Rigidbody (`baseline || _body == null`). Если новый baseline не применялся во время capture, этот канал закономерно не появляется.

### Camera ordering

`SpringArmCamera.LateUpdate()` пишет `camera.LateUpdate.skip`, пока отсутствует target или `Cursor.lockState != Locked`. Полные `begin/end` появляются только после прохождения этих условий. Поэтому преобладание `skip` в начале capture согласуется с source contract и не является само по себе ошибкой камеры.

## 3. Сопоставление с третьим capture

Третий capture подтверждает, что probe действительно был активен: присутствуют `AdapterReady`, player movement, NGO ticks/control и camera records. Поэтому прежняя гипотеза «все строки потеряны при экспорте» больше не является основной.

Отсутствующие baseline/physics строки совместимы с тем, что:

1. initial baseline был применён до фактического начала evidence window; и/или
2. в окне `frame=2958..3324` не было нового binding/rebaseline; и/или
3. фактический `WritePose` не выполнялся в режиме, который вызывает `SyncTransforms` hook.

Точно различить эти варианты по текущему capture нельзя, так как отдельные begin/skip/branch markers для `PrepareBaseline` не записываются.

## 4. Что остаётся доказанным и недоказанным

Подтверждено:

- instrumentation работает на runtime pilot clone;
- binding стабилен;
- adapter находится в состоянии `Ready` и сообщает `baselinePlaced=True`;
- player/CharacterController/Animator/NGO/camera channels наблюдаемы;
- camera history действительно обновляется позднее в окне.

Не подтверждено:

- точный момент initial baseline application;
- физический порядок `Physics.SyncTransforms` относительно movement и NGO tick;
- повторный baseline после controlled rebind/rebase;
- post-rebase `ActorApplied` и `AcknowledgeApplied` continuity;
- native Unity rollback;
- passenger/deck readiness из-за повторных NavMesh warnings.

Причина runtime scheduling anomaly с повторными `FixedUpdate.begin` и несколькими `NetworkTick` в одном frame остаётся **INCONCLUSIVE**. Source analysis не позволяет приписать её конкретному callback, физическому шагу или NGO scheduler.

## 5. Следующий serial capture gate

Чтобы закрыть отсутствующие каналы, следующий пользовательский capture должен обеспечить evidence window, включающий именно переход baseline, а не только idle-состояние уже готового объекта:

- активный probe до начала baseline handshake либо отдельный reviewed baseline transition;
- controlled movement в отдельном участке окна;
- отдельный controlled rebase/post-rebase участок;
- экспорт всех `[T-FO06Y]` строк вместе с `Physics.SyncTransforms` и baseline phases;
- отдельная проверка passenger/deck readiness после устранения или изоляции NavMesh warning.

До получения такого capture `runtimeProofComplete`, `runtimeAdapterReady`, `rollbackReady` и participant admission остаются `false`. Concrete adapters и `Apply/Rebuild/Validate/Publish` не подключаются.
