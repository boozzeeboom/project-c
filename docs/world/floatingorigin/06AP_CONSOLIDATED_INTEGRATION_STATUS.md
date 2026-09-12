# T-FO06AP — consolidated integration status and next gate

Дата: 2026-09-12.

## Назначение тикета

`T-FO06AP` выбран как следующий свободный documentation substage после `T-FO06AO`; поиск по floating-origin отчётам не обнаружил существующего отчёта `06AP`. Найденный `tools/AuditFo06APilot.cs` является историческим инструментом аудита и не является отчётом этапа.

Цель — свести в один reviewed status результаты цепочки `T-FO06AA–T-FO06AO`, прекратить добавление эквивалентных pure micro-contracts и зафиксировать следующий действительно необходимый gate.

## Сводка выполненной цепочки

Уже интегрированы и документированы:

```text
respawn diagnosis / closure
→ explicit UserControlled driver boundary
→ sealed native adapter contract
→ runtime readiness gate
→ live manifest receipt/session evidence
→ aggregated readiness bundle
→ driver readiness authorization
→ connection evidence
→ connection authorization
→ installation intent
→ driver installation authorization
→ installation identity evidence
→ readiness rollover invalidation
→ terminal failure invalidation
```

Последние этапы `06AI–06AO` остаются runtime-independent. Они обеспечивают provenance, exact identity matching, single-use lifecycle и fail-closed invalidation, но не создают доказательств, которых нет в проекте.

## Что доказано

- Все изменённые C# contracts проходят static validation.
- Unity compile после последнего изменения: `No compile errors`.
- Driver не принимает automatic threshold trigger.
- User-controlled request закрыт без readiness authorization и installation authorization.
- Readiness rollover очищает stale installation intent.
- Terminal `Aborted/Faulted` очищает installation authorization и требует reset.
- Coordinator по-прежнему останавливается на `Captured`.
- Unity scenes, prefabs, BootstrapScene и runtime installation не изменялись.

## Что не доказано

Следующие условия остаются отсутствующими и не могут быть заменены очередным pure contract:

- concrete native adapter instances для `Transform`, `Rigidbody`, `ShipDeckNav`, `CameraHistory`, `NetworkBaseline`;
- `liveManifestPublication` и peer digest/count agreement;
- participant admission для полного reviewed manifest;
- valid `GlobalMotionRebaseReadinessBundle`;
- runtime installation driver в canonical `BootstrapScene`;
- фактические `Apply`, `Rebuild`, `Validate`, `Publish` и native rollback;
- user-controlled Play Mode evidence для controlled rebase/post-rebase continuity.

## Решение по укрупнению

Дальнейшее добавление однотипных pure authorization contracts без новых evidence запрещено этим этапом. Следующий этап должен быть одним serial/user-controlled gate, который сначала принимает runtime capture и reviewed adapter evidence, затем только при полном наборе допускает создание readiness bundle.

Нельзя объявлять `runtimeRebaseReadiness=READY` по compile PASS или по наличию контрактов.

## Следующий gate

Следующий рабочий этап — `T-FO06AQ`: user-controlled runtime evidence collection and adapter-readiness review. Он должен отдельно подтвердить:

1. publisher/server/peer manifest evidence;
2. отсутствие digest/count drift;
3. admission каждого participant;
4. readiness пяти native-state domains;
5. baseline, camera/history, physics/deck evidence;
6. rollback capture/restore evidence.

До выполнения этого gate остаются запрещены BootstrapScene installation, native mutation и объявление jitter fixed.

## Проверка этапа

```text
source audit = completed from existing 06AA–06AO reports
compile = No compile errors
Play Mode = NOT_RUN_USER_CONTROLLED
runtimeRebaseReadiness = NOT_READY
```
