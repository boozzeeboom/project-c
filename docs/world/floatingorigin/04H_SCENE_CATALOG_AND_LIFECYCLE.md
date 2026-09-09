# T-FO04H — reviewed scene catalog и lifecycle receipts

Дата: 2026-09-09. Baseline: `b77c3404`. **Подготовительные contracts/validator/audit реализованы; compile PASS, 334 pure PASS / 0 FAIL. Реальный каталог НЕ утверждён; native executor и runtime coverage НЕ реализованы. Global mode/world shift выключены.**

## 1. Реализовано

В начале этапа в рабочем дереве уже присутствовал untracked `GlobalMotionSceneCatalog.cs` со схемой authoring data и immutable plan. Он прочитан, сохранён без перезаписи и включён как зависимость этого этапа. Catalog asset не создавался.

Добавлены:

- `GlobalSceneCatalogCompiler`: строгая проверка замкнутости **заявленного** набора scene GUID и observed roots/NetworkObjects; deterministic SHA256; parent-first topological plan и reverse retirement order; immutable snapshot уже предусмотрен схемой.
- `GlobalSceneLifecycleLedger`: исполняемая pure state machine учёта результатов внешних операций, с scene-load tickets и registration/retirement tokens. Она не вызывает Spawn/Despawn/SetParent/load/unload и не выдаёт native ACK.
- `AuditGlobalSceneCatalog`: read-only authoring snapshot только уже открытых сцен; unloaded scenes явно остаются uninspected. Никакой автоматической классификации/утверждения или копирования legacy Transform в GlobalPosition.
- `ValidateGlobalSceneCatalog`: 56 чистых проверок data/ordering/digest/ledger.

`GlobalMotionNetworkProfile` теперь содержит ссылку на каталог. `GlobalMotionPrefabInspector.TryBuildHello` требует успешной компиляции каталога и равенства его вычисленного digest заявленному `SceneLayoutDigest`. Для replacement entries проверяется наличие matching hash **и Spatial/NonSpatial role** в классифицированном prefab catalog. Произвольной строки hash больше недостаточно. Ошибка global-контракта не переключает запуск в legacy.

Global protocol **0xF004** вместо G **0xF003**. Hello остаётся 72 bytes; snapshot/control/seed formats не менялись. Legacy protocol=0 при unassigned profile сохраняет рабочую ветку. Исторический G validator допускает новые spawn-capable global версии, но продолжает отвергать F.

## 2. Каталог: точные границы

Source identity имеет canonical вид `sceneGuid:GlobalObjectId.targetObjectId:targetPrefabId`, не runtime NetworkObjectId. GUID lowercase; authored object ID ненулевой; prefab ID может быть нулём. GlobalObjectId используется только в Editor; runtime resolver/baked scene identity ещё не реализован.

Compiler требует:

1. Точное соответствие expected scene GUIDs и source descriptors, без дублей, неосмотренных или dirty sources; scene paths под Assets и saved dependency stamps.
2. Ровно одну review entry для каждого наблюдения, без лишних/неизвестных IDs. Unreviewed, пустая reviewNote и unknown treatment блокируют компиляцию.
3. Явную World/ParentLocal pose для spatial entry. Неиспользуемая координатная ветка не должна нести другую точку; non-spatial/excluded entries не получают world pose. Finite/rotation/scale guards применяются до digest.
4. Согласованность treatment с наблюдаемым NetworkObject: его нельзя объявить обычным PreserveContent. Replacement требует prefab hash, остальные treatment не могут его скрыто нести.
5. Initial parentSourceId соответствует observed authored parent. Этот catalog slice не перестраивает исходную hierarchy: динамическое gameplay parenting остаётся слоем F/native bridge. Missing/cross-scene/self/cyclic parent, spatial child under unframed parent, ошибочный ParentLocal и World под network parent отвергаются.
6. Exclude/replacement исходного ancestor требует явного Exclude старых observed descendants. Новый prefab имеет отдельный layout contract.

Hash включает schema, scene GUID/path/dependency stamps, observations со structural hashes, все review decisions и координатные поля. Порядок массивов и CultureInfo не меняют hash. План копирует entries, scene set и digest; последующие изменения authoring arrays не изменяют полученный plan.

**Это не доказательство всего игрового мира.** Scope — authored roots + объекты с NetworkObject; прочие вложенные GameObjects входят в structural subtree hash, но не получают самостоятельную semantic review entry. Набор runtime loading paths, native writers и динамические spawns ещё нужно закрыть. Editable `inspectedComplete/saved` в авторском asset — review assertions, не защищённая runtime attestation.

## 3. Lifecycle ledger и ограничения

- Load ticket связан с session, конкретным экземпляром ledger (Guid nonce), scene GUID и monotonically increasing load generation. Старый callback не применяется к новой загрузке или другому ledger.
- Регистрация разрешена только для известного non-Exclude source. Parent должен быть зарегистрирован первым; spatial representation требует positive local frame ID и тот же frame, что parent. Non-spatial receipt имеет frame=0, без координатной семантики.
- Network receipt требует явных session/object/spawn generations. Object ID=0 допускается как настоящий ID. Duplicate live object и повтор/понижение ранее учтённой spawn generation блокируются.
- Каждый receipt, включая обычный non-network content, получает уникальный registration token. Старое завершение retirement не удаляет новую representation того же source.
- Retirement и Exclude acknowledgements — child-first. Descendant checks используют заранее построенный child index, не полный scan каталога на каждый child.
- `AllSourcesAccountedFor` означает только наличие текущих receipts/exclusion acknowledgements; НЕ `IsReadyForSimulation`.
- EndLoad требует исторического учёта всех sources и отсутствия live receipts. Unseen sources нельзя молча забыть. Exclusion acknowledgement должен поступать только после фактического устранения исходного объекта внешним executor.
- Fault блокирует новые live admissions и coverage; cleanup уже зарегистрированных объектов остаётся возможен. Если fault произошёл с Unseen sources, scope намеренно нельзя просто Clear/Abandon: требуется внешнее native recovery и завершение сессии. Создание нового ledger само по себе не убирает физические объекты и не считается recovery.
- Ledger не вызывает native pool/NGO callbacks и не исполняет reparent. High-water identity history сохраняется на срок ledger; allocation/performance и реальный pool lifecycle ещё не профилировались.

## 4. Реальный read-only аудит

Артефакт: `04H_SCENE_CATALOG_AUDIT.json`. Candidate scope: `Assets/_Project/Scenes`, enabled EditorBuildSettings scenes, уже loaded scenes с сохранённым asset path. Сцены дополнительно не открывались и не сохранялись.

| Наблюдение | Результат |
|---|---:|
| Scene candidates | 26 |
| Loaded inspected | 1 |
| Uninspected | 25 |
| Dirty loaded scenes | 1 |
| Root/NetworkObject observations | 61 |
| Catalog assets / valid catalog assets | 0 / 0 |
| Missing-component subtree diagnostics | 3 |

Диагностики в Bootstrap относятся к поддеревьям:
- `Inventory`, source `336a190646b19bc46b22dd4e78f99800:1633574711:0`;
- `Inventory/[ShipKeyServer]`, source `336a190646b19bc46b22dd4e78f99800:582163927:0`;
- `Toasts_and_meta`, source `336a190646b19bc46b22dd4e78f99800:502945014:0`.

**Это три subtree-диагностики, не обязательно три различных отсутствующих скрипта**: наблюдаемые поддеревья могут пересекаться. Они не исправлялись в этом этапе. Dirty scene не сохранена. 25 сцен не выдаются за осмотренные. Все 61 draft entries остаются Unreviewed, poses не выведены из legacy floats. Эти находки блокируют утверждение реального каталога и не являются падением pure fixture tests.

Structural hash включает descendant IDs, hierarchy order, local transforms, active/layer/tag/static metadata, component slots/types/enabled и NGO sync flags. Он не заменяет semantic audit каждого writer; dependency hash относится к сохранённым asset dependencies. Для уже существующих catalog assets audit также сравнивает свежие loaded observations, saved dependency hash и dirty state.

## 5. Проверки

Фактически выполнен `Temp/Aura/ValidateFo04H.cs` в стабильном Edit Mode:

| Suite | PASS | FAIL |
|---|---:|---:|
| ValidateGlobalSceneCatalog | 56 | 0 |
| ValidateGlobalMotionSpawn | 44 | 0 |
| ValidateGlobalMotionHierarchy | 40 | 0 |
| ValidateGlobalMotionNetworkContracts | 48 | 0 |
| ValidateGlobalMotionActorReadiness | 26 | 0 |
| ValidateGlobalMotionApplication | 32 | 0 |
| ValidateGlobalMotionTransport | 32 | 0 |
| ValidateGlobalMotionProtocol | 33 | 0 |
| ValidateFloatingOriginFoundation | 23 | 0 |
| **Total** | **334** | **0** |

Compile: **No compile errors**. Результаты: `04H_STATIC_VALIDATION.json`. Повторный E asset guard: 58 prefab candidates, opt-in=0, profile assets=0, loaded assigned profiles/opt-in adapters=0. Эта выборка не является полным prefab/scene census.

**UNTESTED:** реальные scene loads/unloads, NGO callbacks, runtime identities, native executor, physics/nav, late join/ownership/parent/pool transitions. Play Mode, игровые сессии, GameObject-тесты, screenshots, scene/prefab edits не выполнялись. Устранение jitter не заявляется.

## 6. Следующий integration gate

Установленный NGO `NetworkManager.cs` вызывает `ServerSpawnSceneObjectsOnStartSweep` **до OnServerStarted** (server: 1366→1370; host: 1525→1530). Поэтому просто подключить ledger или новый callback после старта недостаточно: native in-scene spawn может обойти prepared-frame placement. Legacy `ScenePlacedObjectSpawner` в global mode уже уступает startup lease, но это не отменяет native sweep.

Следующая часть T-FO04 — native scene-source binding и безопасный spawn/retire executor с контролем этой pre-start границы, загрузочной транзакцией и genuine prepared-content receipts в согласовании с T-FO05–08. Утверждение реального scene scope, missing-component/dirty blockers и обследование unloaded scenes остаются отдельными обязательными пунктами. Каталог-asset/profile, baked IDs, source provider и world shift пока не включать.

Один commit кода/meta/отчётов/roadmap/существующего `Assets/_Project/Docs/ITERATIONS.md`. Temp runner, несвязанный LiberationSans fallback и исторические G/F artifacts не включаются/не переписываются.
