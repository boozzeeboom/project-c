# T-FO04E — startup/layout/spawn/parent contracts

Дата: 2026-09-09. Baseline: `89a4a151`. Статус: **подготовительный dormant этап завершён: compile PASS, 194 pure checks PASS / 0 FAIL, read-only prefab audit выполнен**. Concrete spawn/parent bootstrap, runtime activation и полный T-FO04 НЕ завершены.

## 1. Scope

Добавлен opt-in compatibility gate ДО NGO StartHost/StartClient/StartServer, явный prefab layout fingerprint и interface будущего global-aware spawn/parent bootstrap. Сцены/префабы не редактировались, профиль-ассет не создавался, global mode и world shift не включены. Совместимость layout не является проверкой native physics/nav, auth или полной сценовой миграции.

## 2. Подтверждённые исходные границы

- NetworkManagerController запускает все три роли. Исходный путь не задавал ConnectionData payload, ConnectionApproval callback и ProtocolVersion. UnityTransport.SetConnectionData задаёт IP/port, это НЕ NGO approval payload.
- У установленного NGO NetworkConfig.ProtocolVersion — ushort, default 0. Config hash включает версию и source hashes префабов, но не их последовательность NetworkBehaviour. Он сравнивается до ConnectionApproval в ConnectionRequestMessage.
- NetworkObject.InitializeChildNetworkBehaviours перебирает GetComponentsInChildren<NetworkBehaviour>(true), фильтрует ближайший NetworkObject и назначает последовательные ushort индексы. Поэтому одного prefab GUID/hash недостаточно для ABI-проверки.
- У `Assets/_Project/Prefabs/NetworkPlayer.prefab` GlobalObjectIdHash=186599647, SynchronizeTransform=True, AutoObjectParentSync=True. В asset три NB: NetworkTransform, NetworkPlayer, PlayerRespawnTracker. PlayerAttacker и PlayerTarget добавляются позднее серверным OnNetworkSpawn; оба являются NetworkBehaviour. Их legacy добавление в этом этапе не исправляется изменением prefab layout.
- Native TrySetParent при AutoObjectParentSync=false возвращает false. Отключение native parenting требует собственного global parenting bridge, а НЕ сохранения прежних вызовов как будто они работают.
- ScenePlacedObjectSpawner вручную вызывает Spawn для Bootstrap/world сцен без frame-aware placement. В global session этот legacy путь должен уступить управление конкретному bootstrap-provider.

Проверены локальные исходники NGO: Runtime/Configuration/NetworkConfig.cs, NetworkPrefab.cs, NetworkPrefabs.cs; Runtime/Core/NetworkObject.cs, NetworkManager.cs; Runtime/Messaging/Messages/ConnectionRequestMessage.cs. PackageCache не изменялся.

## 3. Реализованный контракт

### Catalog и handshake

- `GlobalMotionNetworkContract`: SHA256 канонического каталога; порядок самих записей несущественен, но учитываются выбранный PlayerPrefab, prefab hash, явная Spatial/NonSpatial role, feature flags и упорядоченные identities NB (hierarchy slot, assembly/type, enabled bit).
- Обнаруженный при review пробел закрыт: разные выбранные PlayerPrefab внутри одинакового каталога теперь дают разные fingerprints. В live registry удалённый PlayerPrefab больше не маскируется повторным добавлением в проверяемую выборку.
- Поля hello: `PCFO`, format=1, global mode=1, protocol=`0xF001`, 32-byte prefab digest, 32-byte scene digest. Размер строго **72 байта**. Null/legacy, другая версия/режим, лишние/недостающие байты, нулевые digests и mismatches отвергаются.
- Ограничения: 2048 prefab entries, 256 NB на запись, 1024 символа identity, 4 MiB canonical byte budget. Это ограничения metadata, не измеренный performance budget реальных соединений.
- Scene digest — **заявленный hash будущего проверенного scene manifest**, а не вычисленная здесь полнота сцен. Его нельзя считать доказательством закрытого T-FO03/06 census или готовой физики.

### Read-only prefab preflight

`GlobalMotionPrefabInspector` читает metadata без Initialize/AddComponent/SetActive/parenting/save. До старта учитывает authored lists, известные runtime entries и default player; после старта читает effective runtime registry.

Spatial запись требует root NO, включённых root adapter/replicator, coordinatesRequired и отсутствия stock initial Transform sync, stock parent sync, enabled NetworkTransform/NetworkRigidbody. Для player требуются заранее включённые в layout PlayerAttacker и PlayerTarget. Nested NO, missing scripts, inactive NB objects, неизвестные flags/role и конфликтная NonSpatial классификация блокируются. Prefab overrides пока явно не поддержаны, а не молча игнорируются.

Классификация остаётся явной: отсутствие Renderer само по себе не объявляет prefab NonSpatial. Даже утверждённый NonSpatial root не доказывает, что все positional DTO/RPC его сервисов уже мигрированы.

### Session-scoped startup gate

- `GlobalMotionNetworkProfile` — только класс SO, EnforceGlobalContracts=false по умолчанию. Asset не создан.
- NetworkManagerController с unassigned/disabled profile сохраняет legacy startup. Зарезервированная global protocol version без enabled profile запрещается как несовместимая конфигурация.
- Global startup требует заранее настроенных ProtocolVersion/ConnectionApproval/ForceSamePrefabs, ClientServer topology, полного классифицированного каталога, scene digest, GlobalMotionWorld и concrete bootstrap-provider на том же GO.
- Gate **не переписывает native hash/config поля**. GetConfig(false) используется read-only, без изменения cached hash.
- Чужой approval callback или непустой ConnectionData запрещают установку gate: auth требует отдельной явной композиции. Никакие credentials не добавляются/не выводятся.
- `IGlobalMotionSpawnBootstrap.ValidateNetworkStart` — read-only утверждение **уже подготовленного** мира. Оно не должно загружать сцены, замораживать actors или стартовать сеть. Config/transport/payload/hash проверяются до/после вызова. Этот interface contract не является техническим sandbox, предотвращающим произвольные побочные эффекты реализации.
- Approval проверяет локальный контракт и hello клиента; CreatePlayerObject=false, Position/Rotation=null. После approved connection ответственность за global-aware placement/spawn передаётся PeerConnected. Сам gate никого не спавнит и не перемещает.
- Изменённый локальный каталог/владелец callback/payload блокирует дальнейший admission. Fingerprint не является аутентификацией, attestation или защитой от произвольного злонамеренного сервера.
- Дублированные notifications не запускают bootstrap повторно; disconnect очищает markers. Stop/failed start освобождают собственные hooks/payload. Чужая замена callback/payload не перезаписывается при cleanup. Host rejection использует Shutdown, поскольку штатный host approval не умеет отказаться от своего host.

### Дополнительные dormant guards

- `GlobalMotionPoseAdapter.Bind` требует установленный startup gate и отключённые native spawn/parent sync flags. Нет автоматической установки или исправления компонента.
- `NetworkPlayer.RegisterWithCombatServer` в global mode не добавляет отсутствующие NetworkBehaviour поздно на сервере. Legacy ветка остаётся прежней.
- `ScenePlacedObjectSpawner` уступает active global session concrete provider. Это **не замена NGO automatic in-scene spawning**: полный контроль initial scene objects обязан обеспечить будущий bootstrap/content слой до допуска запуска.

## 4. Фактические проверки

После всех C# правок compile-check вернул **No compile errors**. Read-only code review не нашёл опасных дефектов dormant integration. Проверяющий агент не имел execution-инструмента, поэтому не заявлял выполненные tests; после этого шесть Run() действительно вызваны отдельным in-editor Execute в стабильном Edit Mode.

| Набор | PASS | FAIL |
|---|---:|---:|
| ValidateGlobalMotionNetworkContracts | 48 | 0 |
| ValidateGlobalMotionActorReadiness | 26 | 0 |
| ValidateGlobalMotionApplication | 32 | 0 |
| ValidateGlobalMotionTransport | 32 | 0 |
| ValidateGlobalMotionProtocol | 33 | 0 |
| ValidateFloatingOriginFoundation | 23 | 0 |
| **Итого** | **194** | **0** |

Полный результат: `04E_STATIC_VALIDATION.json`. Проверены canonical ordering, selected player, malformed metadata, byte budget, все 72 одиночные byte corruptions, все усечённые длины, version/mode/digest rejection, hex parsing и compiled interface. Pure metadata/codec проверки **не упражняют живой approval callback, NGO callback ordering или фактический spawn**.

`04E_PREFAB_CONTRACT_AUDIT.json` прочитал один NetworkPrefabsList asset и **58 candidate prefab assets**: opt-in candidates=0, profile assets=0, enabled profiles на loaded controllers=0, loaded opt-in adapters=0. Spatial preflight issue в этом отчёте — гипотетическая проверка этой роли, не автоматическое решение, что каждый asset обязательно Spatial. Выборка НЕ равна всем 75 prefab assets прежнего census и НЕ включает полный unloaded scene coverage.

**Play Mode, Host/clients, тестовые GameObject, physics simulation, screenshots не запускались.** Реальная совместимость билдов, callback lifetime/pooling, late join, scene-loading race, native parenting, native readiness, gameplay и производительность остаются UNTESTED. Устранение jitter не заявляется.

## 5. Состав этапа и продолжение

Новые runtime source: GlobalMotionNetworkContract.cs, GlobalMotionNetworkProfile.cs, GlobalMotionPrefabInspector.cs, GlobalMotionNetworkStartup.cs в `Assets/_Project/Scripts/World/FloatingOrigin/Network/`.

Новые Editor source: ValidateGlobalMotionNetworkContracts.cs, AuditGlobalMotionPrefabContracts.cs в `Assets/_Project/Editor/FloatingOrigin/`. Включаются шесть Unity-generated script meta.

Изменены существующие NetworkManagerController.cs, NetworkPlayer.cs, GlobalMotionPoseAdapter.cs, ScenePlacedObjectSpawner.cs; отчёт, два generated JSON, roadmap и существующий `Assets/_Project/Docs/ITERATIONS.md`. Временный runner в Temp не входит в коммит. Несвязанный LiberationSans fallback не затрагивается.

Следующая интеграционная часть T-FO04 — concrete global spawn/parent bootstrap и проверенный scene catalog, согласованные с RPC/persistence T-FO05, content T-FO06, physics regions T-FO07 и nav/AI T-FO08. Нынешний interface не заменяет эти реализации. До закрытия обязательных границ не устанавливать профиль/компоненты на рабочие объекты, не выключать их штатный NetworkTransform и не включать world shift. Полная runtime приёмка остаётся пользовательской.

Код, meta и документация фиксируются одним Git-коммитом; отдельный коммит собственного хеша не нужен.
