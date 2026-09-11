# T-FO06W — camera ownership/history и Unity-state rollback proof contracts

Дата: 2026-09-11. Предыдущий подтверждённый коммит: `112e53b3 T-FO06V: add ShipDeckNav readiness gate`.

## 1. Сверка с архитектурным планом

Главное направление остаётся корректным: архитектура требует сначала закрыть pure contracts и fail-closed gates, затем получить пользовательское runtime evidence, и только после этого переходить к `Apply/Rebuild/Validate/Publish`. Runtime rebase, player-only shift, `FloatingOriginMP` и общий `SetParent` не включаются.

Главный план `00_ARCHITECTURE_AND_PLAN.md` отставал от фактически завершённых этапов `T-FO06N–T-FO06V`. В этот этап добавлена отдельная актуализация статуса без переписывания исторических записей.

## 2. Почему эти правки можно объединить

Camera ownership/history и Unity-state rollback являются независимыми proof requirements из T-FO06T и отдельными gates раздела 6 T-FO06N. Оба контракта:

- immutable и runtime-independent;
- не требуют Unity objects, scenes, prefabs, NavMesh, NGO или physics;
- не меняют runtime startup;
- не требуют Play Mode;
- проверяются тем же pure Editor validator pattern.

Поэтому они объединены в один статический этап T-FO06W. Это не объединение runtime-правок и не разрешение rebase.

## 3. Реализация

Созданы:

- `Assets/_Project/Scripts/World/FloatingOrigin/Network/CameraOwnershipHistoryContract.cs`;
- `Assets/_Project/Editor/FloatingOrigin/ValidateCameraOwnershipHistoryContract.cs`;
- `Assets/_Project/Scripts/World/FloatingOrigin/Network/UnityStateRollbackContract.cs`;
- `Assets/_Project/Editor/FloatingOrigin/ValidateUnityStateRollbackContract.cs`.

### 3.1 Camera ownership/history

Контракт требует:

- camera, owner, target и active-camera identity;
- binding и history generations;
- подтверждённую active camera;
- target binding;
- camera history capture;
- collision history capture;
- continuity verification;
- `Billboard.ActiveCamera` binding verification.

`CameraOwnershipHistoryContract` проверяет также, что активный camera ID совпадает с evidence camera ID.

### 3.2 Unity-state rollback

Контракт требует:

- transaction, participant и snapshot identity;
- совпадающие target/snapshot frame generations;
- непустой supported rollback coverage;
- полное captured/restored coverage без лишних флагов;
- snapshot identity;
- restore attempt и success;
- восстановленную participant identity;
- подтверждённый reverse-order rollback;
- валидные participant count и rollback ordinal.

Coverage поддерживает `Transform`, `Rigidbody`, `ShipDeckNav`, `CameraHistory` и `NetworkBaseline`. Контракт не выполняет snapshot или restore.

## 4. Проверки

Menu validators:

```text
ProjectC/World/Floating Origin/Validate Camera Ownership History Contract
ProjectC/World/Floating Origin/Validate Unity State Rollback Contract
```

Фактический результат:

```text
[T-FO06W] Camera ownership/history contract: 15 pure checks PASS / 0 FAIL
[T-FO06W] Unity-state rollback contract: 20 pure checks PASS / 0 FAIL
```

Итого: **35 pure checks PASS / 0 FAIL**. Unity compile: **No compile errors**.

## 5. Что сознательно не пакетировалось

В этот статический пакет не включены:

- исправление `ShipDeckNav.cs`, `NpcBrain.cs`, `ShipCrewSpawner.cs`;
- NavMesh diagnosis и исправление фактической позиции agents;
- финализация participant mapping без owner review;
- классификация 55 scene-owned gameplay roots с последующим admission;
- runtime camera capture;
- NGO/FixedUpdate/physics ordering capture;
- passenger attachment completion capture;
- Apply/Rebuild/Validate/Publish и rollback native Unity state;
- scenes, prefabs, catalog/profile и runtime startup.

Эти пункты требуют отдельного serial/user-controlled evidence или explicit owner policy и не должны маскироваться batch-коммитом pure contracts.

## 6. Решение

T-FO06W закрывает статическую часть двух архитектурных gates: **PASS**.

Runtime camera ownership/history, native Unity snapshot/restore, NavMesh readiness, passenger provenance, NGO ordering и runtime rebase остаются **UNVERIFIED/INCONCLUSIVE**. Runtime rebase readiness — **NOT READY**, admitted participants — `0`.

Следующий интеграционный шаг должен быть выбран только после owner/user evidence для runtime boundaries; автоматический Play Mode не запускается.

## 7. Состав этапа

Код контрактов, два Editor validator-а, JSON report, этот отчёт, актуализация главного плана и существующего `Assets/_Project/Docs/ITERATIONS.md`. Сцены и prefabs не изменялись.
