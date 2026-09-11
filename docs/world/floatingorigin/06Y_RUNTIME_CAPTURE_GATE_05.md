# T-FO06Y — next serial runtime evidence gate

Дата: 2026-09-11.
Основание: `06Y_RUNTIME_CAPTURE_FOLLOWUP_03.md/.json`, `06Y_RUNTIME_EVIDENCE_SOURCE_ANALYSIS_04.md/.json`, текущий Git status.

## 1. Назначение этапа

Зафиксировать следующий serial gate после source analysis 04 и подготовить интеграцию к пользовательскому capture, не выдавая частичную instrumentation evidence за готовность runtime rebase.

Этап документационный и fail-closed. Play Mode, сетевые сессии, screenshots, scene/prefab save и runtime mutation агентом не выполнялись.

## 2. Текущее подтверждённое состояние

Третий пользовательский capture подтвердил:

- наличие активных `[T-FO06Y]` records на `frame=2958..3324`;
- стабильный binding `5641052402999880809/94/1/1/2`;
- `adapter=Ready` и `baselinePlaced=True` в probe snapshots;
- player/CharacterController, grounding, Animator/root-motion, NGO tick/control и camera owner/history channels;
- позднее появление полноценных `camera.LateUpdate.begin/end` после периода `camera.LateUpdate.skip`.

Остаются недоказанными:

- `Physics.SyncTransforms.begin/end` в пределах baseline transition;
- `baseline.ActorApplied` и `baseline.AcknowledgeApplied`;
- controlled movement, jump и platform carry;
- runtime rebase и post-rebase continuity;
- shutdown/disconnect rollback;
- passenger/deck/NavMesh readiness;
- participant admission и live manifest publication.

Повторные `player.FixedUpdate.begin` и несколько `ngo.NetworkTick` в одном frame сохраняются как `RUNTIME_ORDERING_INCONCLUSIVE`. Их нельзя трактовать как доказанную двойную физическую симуляцию.

## 3. Интеграционная граница

Текущие gate values остаются:

```text
runtimeProofComplete = false
runtimeAdapterReady = false
rollbackReady = false
admittedParticipants = 0
liveManifestPublication = false
applyRebuildValidatePublishConnected = false
```

До получения нового user-controlled capture запрещены:

- concrete adapter activation;
- live manifest publication;
- `Apply/Rebuild/Validate/Publish` connection;
- `FloatingOriginMP` activation;
- player-only shift;
- generic shared `SetParent` migration;
- claims of jitter resolution or runtime rebase readiness.

## 4. Следующий пользовательский capture gate

Один capture должен содержать отдельные участки в следующем порядке:

1. старт с активным probe до baseline handshake либо отдельный reviewed baseline transition;
2. `baseline.ActorApplied` и `baseline.AcknowledgeApplied`;
3. `physics.SyncTransforms.begin/end` непосредственно вокруг baseline write;
4. idle baseline после `adapter=Ready`;
5. controlled walk/run и отдельный jump участок;
6. controlled rebase/post-rebase участок с тем же player/camera/binding identity;
7. post-rebase movement и camera continuity;
8. controlled shutdown/disconnect с явным rollback evidence;
9. отдельная passenger/deck readiness проверка после изоляции NavMesh blocker.

В экспорт должны попасть все строки `[T-FO06Y]` вместе с обычным Console Log. Binding identity между запусками не требуется сохранять: проверяется continuity внутри одного capture.

## 5. Рабочее дерево

На момент фиксации этапа обнаружено несвязанное с документационным коммитом незакоммиченное изменение:

`Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab`

Изменение включает `_captureEnabled: 1` и Unity-пересчёт `GlobalObjectIdHash`. Оно не изменялось и не включается в этот коммит. Причина: committed contract описывает probe как выключенный по умолчанию, а изменение prefab layout/hash требует отдельного user-reviewed решения. Пользовательский capture должен выполняться только после проверки этого состояния в isolated pilot prefab.

## 6. Решение этапа

Следующий шаг — serial user-controlled capture по разделу 4. Runtime integration остаётся **BLOCKED / NOT READY** до появления evidence перехода baseline, rebase, rollback и passenger readiness. Этот документ не расширяет runtime scope и не меняет активный gameplay path.
