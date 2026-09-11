# T-FO06T — dormant runtime-proof contract

Дата: 2026-09-11. Основание: T-FO06Q boundary audit, T-FO06S admission policy gate и `06N_REBASE_TRANSACTION_CONTRACT.md`.

## 1. Граница этапа

Создан immutable runtime-independent envelope для будущих user-controlled Play Mode captures. Контракт описывает, какие runtime boundaries должны быть явно доказаны, но сам не собирает Unity state, не запускает Play Mode, не создаёт adapters и не подключается к live admission.

Изменённые исходники:

- `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionRebaseRuntimeProofContract.cs`
- `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionRebaseRuntimeProofContract.cs`

Generated report:

- `docs/world/floatingorigin/06T_RUNTIME_PROOF_CONTRACT.json`

## 2. Explicit proof scope

Поддержаны семь независимых требований:

1. `camera_ownership_history` — active owner и camera history continuity.
2. `ship_deck_nav_registration` — runtime registration/re-registration `ShipDeckNav`.
3. `passenger_provenance` — связь passenger с конкретным ship/deck root.
4. `network_tick_ordering` — порядок относительно NGO tick.
5. `physics_ordering` — порядок physics/transform synchronization и Rigidbody state.
6. `network_baseline_continuity` — continuity NGO/global baseline.
7. `unity_state_rollback` — доказанное восстановление Unity state.

Для каждого capture обязательны `CaptureId`, `ParticipantId`, `FrameGeneration`, непустой `EvidenceReference`, non-empty required scope и verified flags, полностью покрывающие required scope.

## 3. Fail-closed rules

Контракт отклоняет:

- отсутствующую capture identity;
- отсутствующую participant identity;
- нулевое frame generation;
- отсутствующую ссылку на evidence;
- пустой required proof scope;
- неподдерживаемые proof flags;
- verified flags вне required scope;
- неполное покрытие required scope.

Первый отсутствующий proof определяется детерминированно в порядке:

```text
camera_ownership_history
ship_deck_nav_registration
passenger_provenance
network_tick_ordering
physics_ordering
network_baseline_continuity
unity_state_rollback
```

## 4. Проверки

Unity menu:

`ProjectC/World/Floating Origin/Validate Runtime Proof Contract`

Выполнены `11` pure checks:

- complete proof validates;
- missing capture identity fails closed;
- missing participant identity fails closed;
- missing frame generation fails closed;
- missing evidence reference fails closed;
- empty required proof fails closed;
- unsupported required proof fails closed;
- verified proof outside required scope fails closed;
- incomplete proof reports the deterministic first missing requirement;
- independent proof requirements compose without implicit admission;
- validation does not mutate evidence.

## 5. Результат

Unity compile — **PASS / No compile errors**.

Первоначальная ошибка `CS1503` в byte-mask conversion исправлена; повторная compile-проверка прошла успешно.

Validator log:

```text
[T-FO06T] Runtime proof contract: 11 pure checks PASS / 0 FAIL; probes and live admission remain unimplemented.
```

## 6. Decision

Dormant runtime-proof contract — **PASS**.

Runtime evidence — **NOT PROVIDED**:

- Play Mode probes не создавались и не запускались;
- контракт не обращается к `Transform`, `Rigidbody`, `NavMesh`, camera, NGO или scenes;
- контракт не подключён к `GlobalMotionRebaseParticipantAdmissionPolicy`;
- live manifest publication и automatic participant admission отсутствуют;
- текущие `96/96` NetworkObject catalog matches остаются вне admission.

## 7. Inconclusive boundaries

Этап не доказывает active camera ownership/history, runtime `ShipDeckNav` registration, passenger provenance, NGO tick/baseline ordering, physics/Rigidbody ordering или native Unity-state rollback. Сцены, prefabs, catalog/profile и runtime state не изменялись; Play Mode и rebase не выполнялись.

`GroundPlane_0_0`, `FloatingOriginMP`, player-only shifting и generic shared `SetParent` остаются исключёнными.

## 8. Следующий шаг

Использовать этот контракт только как входной формат для отдельного user-controlled Play Mode capture. До получения фактических runtime records не изменять admission policy, не создавать live manifest и не подключать rebase Apply/Rebuild/Validate/Publish.
