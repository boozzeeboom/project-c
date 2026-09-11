# T-FO06O — participant identity census

Дата: 2026-09-11. Основание: `06N_PARTICIPANT_MANIFEST_DESIGN.md`, `06N_REBASE_TRANSACTION_CONTRACT.md` и read-only implementation slice для canonical pilot scope.

## 1. Граница этапа

Этап выполняет только Edit Mode/read-only census уже загруженных canonical scenes:

- `Assets/_Project/Scenes/BootstrapScene.unity`;
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity`.

Не выполнялись scene discovery, scene mutation, save, prefab modification, Play Mode, runtime rebase, transform mutation, physics simulation, NavMesh operation, camera ownership mutation или NGO baseline mutation. `FloatingOriginMP` и `GroundPlane_0_0` не затрагивались.

## 2. Реализация и артефакты

Создан Editor-only audit tool:

`Assets/_Project/Editor/FloatingOrigin/AuditGlobalMotionRebaseParticipantIdentities.cs`

Команда Unity:

`ProjectC/World/Floating Origin/Audit Rebase Participant Identities (Read Only)`

Результат записывается в:

`docs/world/floatingorigin/06O_PARTICIPANT_IDENTITY_CENSUS.json`

Каждая запись содержит scene path, loaded hierarchy path, `GlobalObjectId`, root path, root `GlobalObjectId`, component type names и matched diagnostic roles. Источник identity остаётся доказательным census record, а не serialized participant manifest.

## 3. Подтверждённые результаты

Unity compile — **PASS / No compile errors**.

Обе canonical scenes были загружены и чисты:

- `BootstrapScene`: `56` roots, `isDirty=false`;
- `WorldScene_0_0`: `58` roots, `isDirty=false`.

Identity census:

| Категория | Количество |
|---|---:|
| Required scenes loaded | `2/2` |
| Ship-root candidates (`Rigidbody + NetworkObject`, root-level) | `22` |
| `ShipDeckNav` candidates | `20` |
| Camera candidates | `1` |
| `NetworkObject` candidates | `96` |

Дополнительно подтверждено:

- `WorldRoot_0_0` и `Respawn_Default` найдены как root-level world marker candidates в `WorldScene_0_0`;
- единственный camera candidate — `BootstrapScene/MainCamera`;
- 20 ship-root candidates имеют `ShipDeckNav` в subtree;
- 2 light/reference ship-root candidates не имеют `ShipDeckNav`;
- report сохраняет component classifications и exact loaded hierarchy paths для reviewed mapping.

## 4. Что census не доказывает

Следующие свойства намеренно не выводятся из наличия компонентов или hierarchy path:

- active camera ownership, target binding и camera history;
- runtime `ShipDeckNav` registration, NavMesh origin и связь с Rigidbody;
- `NetworkObject` ownership, spawn lifetime, authority и participant policy;
- пригодность любого candidate для автоматического включения в runtime manifest;
- reviewed mapping candidates к `SHIP_ROOT/01–22` и `SHIP_DECK_NAV/01–20`;
- explicit `CITY_STATIC`, `WORLD_ANCHORS` и `NETWORK_GAMEPLAY_ROOT` manifest boundaries.

## 5. Решение этапа

Read-only participant identity census — **PASS**.

Identity evidence теперь существует для canonical loaded scene scope, но live participant manifest не создан. Automatic adoption, runtime discovery, concrete adapters, `Apply/Rebuild/Validate/Publish` и runtime frame mutation остаются запрещёнными. Runtime rebase readiness — **NOT READY**.

## 6. Следующий шаг

Выполнить reviewed mapping exact census records к fixed manifest IDs, отдельно определить explicit `CITY_STATIC` и `WORLD_ANCHORS` boundaries, а также классифицировать scene-owned `NetworkObject` candidates. До закрытия camera ownership, NGO/physics ordering, runtime `ShipDeckNav` registration и dynamic participant policy не подключать runtime rebase.
