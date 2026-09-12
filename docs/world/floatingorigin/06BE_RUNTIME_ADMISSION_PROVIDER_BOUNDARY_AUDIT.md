# T-FO06BE — runtime admission provider boundary audit

Дата: 2026-09-12  
Статус: **BLOCKED_BY_MISSING_PROVENANCE / DOCUMENTED**

## 1. Назначение

Проверить, существует ли в текущем проекте legitimate runtime provider для `IGlobalMotionRebaseRuntimeAdmissionEvidenceSource`, который может выдать все шесть admission-флагов на основании реального происхождения данных.

Этап не создаёт synthetic provider, не подключает `BootstrapScene`, не регистрирует native adapters и не изменяет runtime state.

## 2. Проверенные границы

Проверены исходники в `Assets/_Project/Scripts/World/FloatingOrigin/Network`:

- `GlobalMotionRebaseSceneRuntimeManifestSource` требует отдельный `MonoBehaviour`, реализующий `IGlobalMotionRebaseRuntimeAdmissionEvidenceSource`;
- `GlobalMotionNativeAdapterSet` остаётся закрытым-world контрактом без discovery и содержит `0` зарегистрированных adapters в текущей интеграции;
- `GlobalMotionRebaseRuntimeProofEvidence` — immutable envelope для уже полученного user-controlled capture, без runtime collector/provider;
- `UnityStateRollbackEvidence` — immutable envelope для уже полученного rollback результата, без runtime capture/restore coordinator;
- `GlobalMotionRebaseUserControlledEvidenceReviewGate` принимает готовые manifest/session/adapter/proof/rollback inputs, но не создаёт их;
- существующие Transform, Rigidbody и CameraHistory adapters являются explicit concrete classes, не self-register-ятся и не покрывают ShipDeckNav/NetworkBaseline;
- ShipDeckNav и NetworkBaseline остаются отдельными блокерами, зафиксированными в T-FO06AY/T-FO06AZ.

Поиск concrete реализации provider и runtime evidence producers в проверенной source-папке результатов не дал. Это отрицательное evidence ограничено audited path; оно не доказывает отсутствие реализации в файлах вне этого пути.

## 3. Решение

Legitimate provider на текущем контракте безопасно не реализуется:

1. all-`true` provider был бы синтетическим и нарушил бы fail-closed admission boundary;
2. provider, читающий только `GlobalMotionNativeAdapterSet`, не может доказать runtime proof и rollback evidence;
3. provider, читающий только пользовательский capture, не может доказать текущую native adapter coverage и session binding;
4. сериализованные bool-поля без provenance не являются доказательством и не должны открывать admission;
5. подключение source/bridge в `BootstrapScene` до появления provenance только переместило бы отказ в runtime и создало бы ложное ощущение интеграции.

Поэтому provider и scene binding намеренно не добавлялись.

## 4. Текущее состояние

```text
provider implementation                 = NOT IMPLEMENTED
provider provenance contract            = INSUFFICIENT FOR RUNTIME ADMISSION
native adapter set                      = EMPTY / UNSEALED
Transform/Rigidbody/Camera adapters     = DORMANT / NOT REGISTERED
ShipDeckNav adapter                     = BLOCKED
NetworkBaseline adapter                 = BLOCKED
runtime proof producer                  = NOT PRESENT
rollback evidence producer             = NOT PRESENT
bridge/source BootstrapScene binding   = NOT CONNECTED
live manifest publication               = NOT OBSERVED
runtimeRebaseReadiness                  = NOT_READY
```

## 5. Следующий допустимый gate

Следующий кодовый этап должен сначала ввести отдельные provenance-bearing source contracts для:

- sealed/current native adapter readiness;
- user-controlled runtime proof package;
- transaction-scoped rollback result;
- reviewed identity/policy/catalog receipt.

Каждый source обязан возвращать фактический immutable evidence либо fail closed с причиной. До появления concrete producers эти interfaces могут быть только dormant design/compile slice и не должны быть установлены в `BootstrapScene`.

Runtime acceptance, controlled rebase и rollback по-прежнему выполняются пользователем в Play Mode. `runtimeRebaseReadiness` остаётся `NOT_READY`.

## 6. Проверки

- source-level audit: **PASS** для проверенных путей;
- concrete provider search: **INCONCLUSIVE вне audited path**, concrete provider в audited path не найден;
- synthetic all-true provider: **не создавался**;
- BootstrapScene mutation: **NONE**;
- Play Mode: **не запускался**;
- compile check: выполняется после импорта отчётного этапа;
- `git diff --check`: выполняется перед коммитом.
