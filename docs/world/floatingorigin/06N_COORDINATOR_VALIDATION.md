# T-FO06N — pure validation coverage for the rebase coordinator

Дата: 2026-09-11. Основание: `06N_REBASE_TRANSACTION_CONTRACT.md` и commit `73ea5ed1`.

## 1. Граница этапа

Этап закрывает только pure validation coverage для runtime-inert coordinator skeleton. Не изменяются сцены, префабы, catalog/profile, NGO settings, `GroundPlane_0_0`, `FloatingOriginMP`, frame publication или runtime state. Play Mode, physics simulation, NavMesh, camera и network session не запускаются.

Проверки используют только in-memory fake gate, participants и immutable snapshot objects. Они не являются доказательством работы Unity-state rollback.

## 2. Реализовано

Создан validator:

`Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionRebaseCoordinator.cs`

Добавлен пункт меню:

`ProjectC/World/Floating Origin/Validate Rebase Coordinator`

Покрыты следующие fail-closed invariants:

- request validation: transaction identity, frame generation, valid plan, expected count и manifest digest;
- immutable request fields и plain C# coordinator без `UnityEngine.Object`;
- null/missing/duplicate participant rejection;
- empty/sealed participant-set lifecycle;
- unsealed, count-mismatched и stale closed-world rejection;
- успешный порядок `REQUEST → FREEZE → PREFLIGHT → CAPTURE` с остановкой на `Captured`;
- snapshot identity mismatch;
- reverse-order rollback после `TryAbort`;
- preflight failure без лишнего restore и с release freeze gate;
- capture failure с restore только уже captured participants;
- freeze refusal без попытки release;
- `Aborted` против `Faulted` при restore/release failure;
- reset после abort/fault и повторная чистая подготовка.

## 3. Проверки

- Validator execution: **13 pure checks PASS / 0 FAIL**.
- Unity compile: **No compile errors**.
- Play Mode: **не выполнялся**.
- Сцены, префабы, frames, transforms, physics, NavMesh, camera и NGO baseline: **не изменялись и не подключались**.

## 4. Что доказано и что нет

Доказан только контракт чистого coordinator state machine на in-memory doubles: закрытый набор участников не принимает неизвестных/устаревших/дублированных участников, snapshot identity проверяется, cleanup выполняется в обратном порядке, а недоказанный cleanup переводит coordinator в `Faulted`.

Не доказаны и остаются `INCONCLUSIVE`:

- source и canonical format serialized participant manifest/digest;
- ownership active camera и camera history binding;
- exact NGO tick / FixedUpdate / physics synchronization ordering;
- runtime `ShipDeckNav` registration и связь с ship Rigidbody;
- concrete adapters для city, anchors, ships, player frame, camera и network gameplay roots;
- rollback реального Unity state, Rigidbody, collider, NavMesh и NGO baseline;
- любой `Apply`, `Rebuild`, `Validate`, `Publish` или runtime frame mutation.

## 5. Решение этапа

Pure validation coverage для текущего coordinator skeleton — **PASS**. Runtime rebase readiness — **NOT READY**.

Следующий этап — отдельный participant-manifest design/read-only slice: стабильные IDs, canonical ordering и digest contract без runtime discovery, scene mutation или подключения concrete adapters. Camera, physics, NavMesh и network ordering gates остаются обязательными блокерами до любого Apply/Rebuild.
