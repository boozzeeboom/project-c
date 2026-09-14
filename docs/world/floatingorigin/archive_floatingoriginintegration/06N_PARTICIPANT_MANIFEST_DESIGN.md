# T-FO06N — participant-manifest design/read-only slice

Дата: 2026-09-11. Основание: `06N_REBASE_TRANSACTION_CONTRACT.md` и validation commit `7af9d2e3`.

## 1. Граница этапа

Этап задаёт pure contract для будущего serialized/read-only participant manifest. Он не выполняет scene census, runtime discovery, scene mutation, DDOL relocation, `SetParent`, frame publication или runtime rebase. Сцены, префабы, catalog/profile, NGO settings, `GroundPlane_0_0` и `FloatingOriginMP` не изменялись. Play Mode не запускался.

## 2. Реализовано

Создан runtime-independent manifest contract:

`Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseParticipantManifest.cs`

Создан Editor validator:

`Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionRebaseParticipantManifest.cs`

### Canonical identity format

Каждая запись содержит:

- `ParticipantId` — стабильный ASCII token с явным namespace/prefix;
- `Kind` — один из существующих `GlobalMotionRebaseParticipantKind`;
- `SourceIdentity` — reviewed source identity, которая не вычисляется автоматически из неизвестного Unity object;
- `Ordinal` — стабильный порядковый номер внутри повторяющейся группы.

Canonical ordering выполняется по `ParticipantId`, затем `Kind`, `SourceIdentity` и `Ordinal`. Digest строится из versioned binary canonical payload с заголовком `projectc.global-motion-rebase-participants`, version `1`, count и полями каждой записи; используется lowercase SHA-256 hex.

### Fixed coverage contract

Pure contract требует присутствия:

- `CITY_STATIC`;
- `WORLD_ANCHORS`;
- `PLAYER_FRAME`;
- `CAMERA`;
- `SHIP_ROOT/01` … `SHIP_ROOT/22`;
- `SHIP_DECK_NAV/01` … `SHIP_DECK_NAV/20`.

`NETWORK_GAMEPLAY_ROOT/<stable-id>` допускается только как explicit manifest entry. Его количество намеренно не угадывается: неизвестные scene-owned NetworkObject/NPC/crew roots не принимаются автоматически.

## 3. Проверки

- Manifest validator: **10 pure checks PASS / 0 FAIL**.
- Unity compile: **No compile errors**.
- Проверены duplicate participant ID, duplicate kind/source identity, invalid token, canonical ordering, digest sensitivity, defensive copies, fixed coverage, missing/wrong-kind coverage и explicit dynamic network roots.
- Play Mode, scene census, actual camera binding, runtime `ShipDeckNav`, NGO ordering и live manifest binding: **не выполнялись**.

## 4. Что доказано и что остаётся inconclusive

Доказано, что будущий manifest может иметь deterministic canonical bytes/digest и что fixed participant coverage не зависит от входного порядка. Доказано также, что dynamic network gameplay roots должны быть перечислены явно и не могут появляться через discovery.

Не доказаны и остаются `INCONCLUSIVE`:

- реальные `SourceIdentity` для 22 ship roots и 20 runtime `ShipDeckNav` boundaries;
- serialized asset/file format и место хранения manifest;
- соответствие записей actual loaded scene handles, NetworkObject lifetimes и Rigidbody identities;
- active camera owner/history binding;
- runtime deck registration и passenger provenance;
- concrete adapter registration и digest publication в request;
- rollback Unity state и первый Apply/Rebuild.

## 5. Решение этапа

Participant-manifest design/read-only slice — **PASS**. Live participant manifest — **NOT CREATED**; runtime rebase readiness — **NOT READY**.

Следующий этап должен закрыть read-only source census/identity mapping для fixed entries или отдельно получить runtime instrumentation camera/NGO/physics/NavMesh. До этого concrete adapters и Apply/Rebuild не подключаются.
