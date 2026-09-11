# T-FO06R — catalog and NetworkObject policy cross-audit

Дата: 2026-09-11. Основание: `GlobalMotionPilotSceneCatalog.asset`, T-FO06O census и T-FO06P mapping.

## 1. Граница этапа

Read-only Edit Mode cross-audit загруженных canonical scenes против существующего reviewed scene catalog. Сцены, catalog asset, prefabs, NetworkObjects и runtime state не изменялись. Play Mode, spawn, ownership mutation, physics/NavMesh mutation и rebase не выполнялись.

Catalog source:

`Assets/_Project/Prefabs/FloatingOrigin/GlobalMotionPilotSceneCatalog.asset`

Generated report:

`docs/world/floatingorigin/06R_CATALOG_NETWORK_POLICY_AUDIT.json`

Audit tool:

`Assets/_Project/Editor/FloatingOrigin/AuditGlobalMotionRebaseCatalogNetworkPolicies.cs`

## 2. Проверки

Команда Unity:

`ProjectC/World/Floating Origin/Audit Catalog Network Policies (Read Only)`

Tool проверяет:

- обе canonical scenes загружены и clean;
- catalog asset существует и содержит entries;
- каждый live scene-owned NetworkObject имеет `GlobalSceneSourceMarker`;
- source marker `(sceneGuid, sourceId)` находится в catalog;
- ownership, treatment, spatial и poseKind фиксируются из catalog;
- ship roots проверяются отдельно по Rigidbody и root identity;
- ни один candidate не получает runtime admission автоматически.

## 3. Результат

Unity compile — **PASS / No compile errors**.

Лог аудита:

```text
[T-FO06R] Catalog/network audit: records=96;matched=96;missing=0;admitted=0;blocked=96
```

Catalog summary:

| Проверка | Результат |
|---|---:|
| Catalog entries | `150` |
| Catalog scenes | `2` |
| Live NetworkObject records | `96` |
| Catalog matches | `96` |
| Missing catalog entries | `0` |
| Automatically admitted | `0` |
| Blocked/review required | `96` |

Ownership/policy classification:

| Scene / ownership | Count | Catalog state | Policy |
|---|---:|---|---|
| BootstrapScene / `BootstrapService` | `19` | `treatment=Unmanaged`, `spatial=false`, `poseKind=None` | explicit non-spatial review |
| WorldScene / `SceneOwnedNetworkGameplay` | `55` | `treatment=Unmanaged`, `spatial=false`, `poseKind=None` | `NETWORK_GAMEPLAY_ROOT` review required |
| WorldScene / `ShipOrRigidbodyRoot` | `22` | `treatment=Unmanaged`, `spatial=false`, `poseKind=None` | ship-root catalog match review required |

All `96` NetworkObject records currently have `spatial=false`, `poseKind=None` and `treatment=Unmanaged` in the pilot catalog. Therefore the catalog identity/ownership match is complete, but it does not yet describe a runtime spatial rebase adapter for any of these objects.

## 4. Decision

The catalog binding gate is **PASS**:

- all `96/96` live NetworkObject candidates bind to reviewed catalog entries;
- there are no missing source markers or catalog entries in this loaded scope;
- ownership categories are explicit and separated between bootstrap services, world gameplay and ship roots.

Runtime participant admission remains **NOT READY**:

- `admitted=0` by design;
- no `NETWORK_GAMEPLAY_ROOT/<stable-id>` entries are published;
- `SceneOwnedNetworkGameplay` records remain spatial-policy review required;
- `ShipOrRigidbodyRoot` records still need runtime Rigidbody/NavMesh/passenger proofs;
- catalog `spatial=false`/`poseKind=None` is insufficient for `Apply/Rebuild/Validate/Publish`.

## 5. Inconclusive boundaries

The audit does not prove:

- NetworkObject ownership, spawn lifetime or authority at runtime;
- NGO tick/baseline ordering;
- active camera ownership/history;
- runtime `ShipDeckNav` registration or passenger provenance;
- Rigidbody interpolation/physics state during rebase;
- native rollback after partial Apply.

No live manifest or runtime adapter is created by this stage. `GroundPlane_0_0`, `FloatingOriginMP`, player-only shift and generic `SetParent` remain excluded.

## 6. Следующий шаг

Use this result as the source gate for an explicit policy decision: either keep the 55 world gameplay roots and 22 ship roots outside the live rebase manifest until runtime evidence exists, or author separate reviewed spatial entries with adapters and rollback contracts. Do not infer spatial admission from catalog ownership alone.
