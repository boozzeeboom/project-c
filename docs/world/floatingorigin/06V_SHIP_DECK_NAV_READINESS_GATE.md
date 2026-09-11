# T-FO06V — ShipDeckNav/NavMesh readiness gate и passenger attachment contract

Дата: 2026-09-11. Предыдущий подтверждённый коммит: `389c3aee T-FO06U: audit user runtime proof capture`.

## 1. Граница этапа

Этап изолирует fail-closed контракт готовности `ShipDeckNav`/NavMesh и completed passenger attachment. Runtime startup остаётся неизменённым.

В этап не входят:

- изменения `ShipDeckNav.cs`, `NpcBrain.cs` или `ShipCrewSpawner.cs`;
- Play Mode, user runtime capture и screenshots;
- runtime rebase, live manifest admission и scene/prefab mutation;
- создание или регистрация NavMesh agents;
- исправление остальных gameplay/runtime warnings.

## 2. Причина отдельного gate

В пользовательском T-FO06U capture наблюдались одновременно:

- `20` сообщений `ShipDeckNav Registered`;
- `20` explicit ship-deck attachment requests;
- `20` named crew spawns;
- повторяющиеся ошибки `Failed to create agent because it is not close enough to the NavMesh` из `ShipDeckNav.cs:200`, `NpcBrain.cs:688` и `ShipCrewSpawner.cs:154`.

Следовательно, строка `Registered` не является достаточным доказательством готовности. Запрос attachment также не является доказательством completed passenger attachment.

## 3. Реализация

Созданы:

- `Assets/_Project/Scripts/World/FloatingOrigin/Network/ShipDeckNavReadinessContract.cs`;
- `Assets/_Project/Editor/FloatingOrigin/ValidateShipDeckNavReadinessContract.cs`.

Pure evidence contract требует одновременно:

1. стабильные `ShipId`, `ShipDeckNavId` и `PassengerId`;
2. подтверждённую регистрацию `ShipDeckNav`;
3. отдельный идентификатор и валидный `NavMeshDataInstance`;
4. созданный proxy agent;
5. `proxyAgentIsOnNavMesh=true`;
6. explicit attachment request;
7. разрешённый passenger anchor;
8. completed passenger attachment;
9. attachment evidence identity;
10. записанную passenger provenance.

Контракт не обращается к Unity objects и не считает одну регистрацию NavMesh достаточной.

## 4. Детерминированные причины отказа

Validator проверяет отказ в фиксированном порядке. Основные причины:

```text
ship_deck_nav_not_registered:<id>
nav_mesh_data_id_required
nav_mesh_data_instance_invalid:<id>
proxy_agent_not_created:<id>
proxy_agent_not_on_nav_mesh:<id>
passenger_attachment_not_requested:<id>
passenger_anchor_unresolved:<id>
passenger_attachment_incomplete:<id>
attachment_evidence_id_required
passenger_provenance_missing:<id>
```

Проверка `Registered alone is insufficient` специально фиксирует блокирующее наблюдение T-FO06U: при `Registered=true`, но невалидном `NavMeshDataInstance`, контракт отклоняет evidence с `nav_mesh_data_instance_invalid`.

## 5. Pure validation result

Ожидаемый Edit Mode menu validator:

```text
ProjectC/World/Floating Origin/Validate ShipDeckNav Readiness Contract
```

Validator содержит `15` pure checks, включая complete evidence, отсутствие NavMeshData validity, отсутствие proxy-agent readiness, attachment request без completion, deterministic precedence и immutability evidence.

## 6. Решение

T-FO06V закрывает только static contract/validator slice: **PASS после compile и validator checks**.

Runtime ShipDeckNav readiness, фактический `NavMeshDataInstance.valid`, proxy-agent `isOnNavMesh`, completed passenger attachment и provenance остаются **UNVERIFIED/INCONCLUSIVE** до отдельного пользовательского Play Mode capture. Runtime rebase readiness остаётся **NOT READY**; все участники остаются вне admission.

## 7. Следующий шаг

Не менять runtime startup и не подключать этот dormant contract автоматически. Следующий отдельный этап может подготовить user-controlled runtime evidence adapter только после согласования точных boundaries; до этого `ShipDeckNav Registered` и attachment request должны классифицироваться как observed lifecycle, а не readiness proof.

## 8. Состав этапа

Код, Editor validator, JSON report, этот документ и существующий `Assets/_Project/Docs/ITERATIONS.md`. Сцены, prefabs, `ShipDeckNav.cs`, `NpcBrain.cs`, `ShipCrewSpawner.cs`, runtime startup и Play Mode не изменялись.
