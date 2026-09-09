# T-FO04I — baked source binding и ограниченный native executor

Дата: 2026-09-09. Baseline: `41db8405`. **Код ограниченного executor реализован; compile PASS; 365 pure PASS / 0 FAIL. Ни одна native операция executor фактически не запускалась.** Global mode/world shift выключены, scenes/prefabs не редактировались.

## 1. Результат и пределы

Добавлены `GlobalSceneSourceMarker`, pure `GlobalSceneExecutionPolicy`, `GlobalSceneNativeExecutor` и Editor validator `ValidateGlobalSceneExecution`. G player bootstrap требует executor, вызывает подготовку до NGO Start и ждёт server scene receipts перед спавном игроков. Startup approval требует `IGlobalMotionSceneAdmission`: remote peer не принимается до готовности начальных сценовых sources; настоящий host-local client может пройти раннюю approval, иначе возникает цикл до OnServerStarted.

**Поддерживаемый I slice:** статический PreserveContent с явной World pose, заранее неактивный в hierarchy; сценовые **NONSPATIAL** NetworkObject-сервисы, заранее self-inactive, с enabled NetworkObject и явным activation intent; обычная non-spatial инфраструктура без перемещения. Для static content разрешены Transform/MeshFilter/Renderer/static Collider и marker, но не произвольные writer scripts.

**Не поддержаны:** spatial scene network actors, Rigidbody/joints/CC/nav/2D, replacements/exclusions, DDOL migration, динамический streaming, pool/region handoff, distributed unload transaction и full gameplay bridges. Текущая игра не может opt-in только от появления этих файлов. Genuine prepared-content source G по-прежнему отсутствует; catalog/source markers/profile ещё не подготовлены.

## 2. Проверенные особенности NGO 2.13.0

Проверены установленные исходники, PackageCache не изменён:

- `NetworkSpawnManager.ServerSpawnSceneObjectsOnStartSweep` вызывает `FindObjects.ByType<NetworkObject>(orderByIdentifier: true)`. Внутренний helper имеет `includeInactive=false` по умолчанию, затем sweep проверяет `InScenePlaced`. Это не фильтр `prefab hash != 0` и не фильтр `NetworkObject.enabled`.
- Sweep выполняется **до OnServerStarted**: server `NetworkManager.cs` 1366→1370, host 1525→1530.
- `NetworkSceneManager.PopulateScenePlacedObjects` перечисляет inactive-inclusive, но добавляет в lookup лишь `isActiveAndEnabled` (2766–2780). Ожидать IsSpawned перед включением client source нельзя: lookup не найдёт такой объект.
- Поэтому сервер оставляет network sources inactive до завершения sweep, а **клиент активирует заранее размещённые источники в OnClientStarted до входящей scene synchronization**, без ожидания IsSpawned. Server sets public `SetClientSynchronizationMode(Additive)` до remote admission, чтобы клиент переиспользовал подготовленные instances/handles.
- I требует **весь catalog scene set уже загруженным на каждом peer**. Unknown/extra/missing scenes, duplicate scene instances и дальнейшее изменение набора блокируются. Разные streaming subsets требуют следующего loading bridge; этот ограниченный режим не объявляется MMO streaming.

Private NGO setters, runtime GlobalObjectId, патчи пакета и фиктивные player/owner IDs не применяются.

## 3. Binding и pre-start preparation

`GlobalSceneSourceMarker` — обычный MonoBehaviour, не NetworkBehaviour. Только serialized authoring поля: sourceId, local frameId, activateWhenReady. Runtime не генерирует sourceId из имени, позиции или NGO id. Marker должен быть заранее baked на каждом H observed root/NetworkObject. Baker и сами scene edits этим этапом не выполнялись; после будущего baking нужно повторить structural/dependency audit и review каталога.

Read-only `ValidatePreparation` проверяет:

1. Stopped Singleton manager, enabled scene management, reviewed catalog и computed/declared digest.
2. Точный набор preloaded scenes и взаимно-однозначные bindings всех root/NetworkObject candidates к catalog source IDs. Unknown roots не игнорируются ради прохождения validation. Все source paths и parent bindings должны совпадать; DDOL relocation не подменяется guessed origin.
3. Каждый frame задан явно, valid/loaded, соответствует source Scene/PhysicsScene; static World point проецируется в его budget. Non-spatial binding имеет frameId=0.
4. Все NetworkObject, включая найденные outside/inactive/DDOL, должны быть известны scope, принадлежать manager, быть unspawned/self-inactive/hierarchy-inactive. Native sync/parent/scene migration flags и DontDestroyWithOwner для управляемых services выключены.
5. Ancestors обязательных источников смогут стать active; неизвестный inactive ancestor или marker без activation intent блокирует путь. Активная игра **не деактивируется автоматически**.
6. Unsupported native/writer components требуют отдельного bridge. Initial root/NetworkObject coverage — не semantic proof всех gameplay systems.

`PrepareBeforeNetworkStart` дополнительно требует user-started Play Mode/player runtime. Оно сохраняет local transform snapshots, размещает **только уже inactive static sources** по явному GlobalPosition/local frame и подписывает started callbacks. Parent hierarchy не меняется, HP/velocity/Animator/game state не сбрасываются. При отказе **до networking/активации** исходные transforms могут быть восстановлены в reverse order, только если identity и inactivity не изменились. После начала networking общего rollback нет.

## 4. Native spawn, receipts и admission

После started callback:

- Создаётся локальный bookkeeping issuer/ledger. Его nonce/generation — local receipt identity, **не wire motion authority/session**, а NetworkObjectId берётся из реального spawned объекта.
- На client отдельный parent-first activation pass готовит NGO lookup; client сам не вызывает Spawn.
- На server parent-first `Advance` активирует разрешённый source, вызывает публичный `NetworkObject.Spawn(destroyWithScene:true)`, проверяет IsSpawned/manager/source identity и **только затем** записывает ledger receipt.
- PreserveContent получает receipt после activation/identity checks. Receipt учитывает источник, но не доказывает исправность произвольного OnNetworkSpawn или native physics.
- Remote connection при неготовых scene receipts отвергается с retry reason, не получает legacy/default player. G server player queue ждёт readiness. Ожидание initial receipts ограничено 60 секундами.
- Callback-driven shutdown/release после SetActive/Spawn/Despawn проверяется до дальнейшего доступа к ledger. Потеря scope/identity/frame ведёт к fault и запросу shutdown.

Синхронные ошибки source callbacks, которые NGO может сам перехватить, не превращаются от одного IsSpawned в доказанный gameplay PASS. Реальный порядок с сетью остаётся UNTESTED.

## 5. Retirement и восстановление

`GlobalSceneLifecycleLedger.CanRecordRetired` добавлен как **read-only** preflight без потребления receipt. Executor использует его до необратимой операции. Затем server вызывает `Despawn(false)` для scene object, проверяет результат, делает owned source inactive и записывает `TryRecordRetired` только после факта.

Local `TryRetireScene` идёт child-first. Unknown runtime roots (в том числе появившиеся players), unknown descendant NetworkObjects, неполный учёт и внешняя persistent инфраструктура блокируют retirement. На клиенте caller обязан дождаться NGO despawn сообщений; client не изображает серверную authority. Физическая scene unload и её согласование **со всеми peers — ответственность следующего streaming coordinator**, не этой функции.

При partial failure предыдущие despawn/deactivation не откатываются: fault/stop, дальнейшее native recovery обязательно. После retirement новые admissions закрыты; повторная streaming load/rebind тем же executor не реализована. Release после сетевого завершения не возвращает работающий мир к большим legacy coordinates; before-start rollback ограничен inactive неизменными sources.

## 6. Фактическая проверка

Compile: **No compile errors**. В ходе статического review исправлен client inactive/IsSpawned lookup deadlock, добавлены Additive sync, explicit full-preload scope, enabled/ancestor guards, post-native reentrancy checks и descendant NetworkObject retirement guard. Нельзя считать эти исправления runtime validation.

Фактически выполнен `Temp/Aura/ValidateFo04I.cs` в стабильном Edit Mode:

| Suite | PASS | FAIL |
|---|---:|---:|
| ValidateGlobalSceneExecution | 31 | 0 |
| ValidateGlobalSceneCatalog | 56 | 0 |
| ValidateGlobalMotionSpawn | 44 | 0 |
| ValidateGlobalMotionHierarchy | 40 | 0 |
| ValidateGlobalMotionNetworkContracts | 48 | 0 |
| ValidateGlobalMotionActorReadiness | 26 | 0 |
| ValidateGlobalMotionApplication | 32 | 0 |
| ValidateGlobalMotionTransport | 32 | 0 |
| ValidateGlobalMotionProtocol | 33 | 0 |
| ValidateFloatingOriginFoundation | 23 | 0 |
| **Total** | **365** | **0** |

31 новая проверка покрывает pure support/activation/admission/rollback policy, non-consuming retire preflight и compiled API seams. **Native binding/placement/Spawn/Despawn/SetActive executor не вызывались.**

`04I_STATIC_VALIDATION.json`: 58 prefab candidates, opt-in/profile assets/loaded assigned profiles/adapters=0, loadedSourceMarkers=0, loadedSceneExecutors=0. Это ограниченный audit scope, не полнота всех runtime paths.

`04I_SCENE_CATALOG_AUDIT.json`: прежние 26 кандидатов сцен, 1 loaded/dirty, 25 uninspected, 61 Unreviewed observations, catalog assets=0. Три missing-component subtree diagnostics остаются в Inventory, Inventory/[ShipKeyServer], Toasts_and_meta; пересекающиеся поддеревья не равны числу уникальных отсутствующих scripts. Находки не исправлялись, dirty scene не сохранялась.

**UNTESTED:** реальный Host/clients/late join, NGO scene-origin/sync lifecycle, native service callbacks, prepared static collider activation, distributed retire/unload/reconnect, runtime identity baking/coverage, NPC/ship/nav/body bridges и performance. Без Play Mode, игровых соединений, physics simulation, GameObject-тестов и screenshots. Jitter fixed не заявляется.

## 7. Совместимость и продолжение

Protocol **0xF005** требует новой initial scene admission/placement семантики; H и более ранние global реализации несовместимы. Legacy protocol=0 при неназначенном profile не меняется. H/G исторические отчёты и JSON сохранены.

Следующие обязательные части: подготовка/baking/проверка реальных source bindings и reviewed catalog, genuine G prepared-content/global-persistence provider; далее spatial actor, replacement/exclusion, DDOL и loading transaction в согласовании с T-FO05–08. Нельзя активировать I на текущем неподготовленном Bootstrap или считать full-preload subset завершением MMO-архитектуры. Dirty/missing-component/uninspected blockers H остаются открыты.

Один коммит кода/meta/результатов/roadmap/существующего ITERATIONS. Temp runner и несвязанный LiberationSans fallback вне коммита. Ни scenes, ни prefabs, ни profile/catalog assets не менялись.
