# T-FO04C — session/frame coordinator и pose adapter

Дата: 2026-09-09. Baseline: `47fc69fa`. Статус: **T-FO04C реализован как неподключённый интеграционный слой; компиляция и 120 чистых проверок пройдены**. Это НЕ полный T-FO04 и не runtime/physics PASS. Код и документация фиксируются одним коммитом.

## Scope и защита работающих механик

Добавить явный координатор одной NGO-сессии и применение global/parent-local снимков в Unity. Не устанавливать компоненты на действующие сцены/префабы до интеграции gameplay readiness и content placement. Регистрация frame НЕ перемещает мир. Изменение origin уже зарегистрированного frame здесь не разрешается: это отдельная rebase-транзакция T-FO06.

Подтверждённые зависимости:
- NetworkPlayer.FixedUpdate/Move и `_platformLastPos/_platformDelta` требуют readiness/cache hooks; текущая реализация игрока в этом этапе не меняется.
- ShipController использует серверную Rigidbody-физику. Remote snapshot нельзя записывать поверх dynamic authoritative Rigidbody; client proxy должен быть подготовлен как kinematic отдельной интеграцией.
- NpcBrain/ShipDeckNav используют отдельные world/deck/nav пространства. Активный NavMeshAgent нельзя телепортировать обычным Transform setter вместо согласованного Warp/path handoff.
- SkillAnimationPlayer действительно включает Animator.applyRootMotion (260–263) и использует animation events. Прежний краткий поиск, не обнаруживший root motion, недостаточен; Animator и события не отключать.

## Выбранная реализация

1. GlobalMotionWorld на том же GameObject, что NetworkManager: явная session lifecycle, один server issuer, frame registry, actor registry и ранний baseline/server-pose pump. Не добавляется автоматически в Bootstrap.
2. Frame immutable и привязан к конкретной PhysicsScene и поколению запуска. Не разрешать два frame на одной PhysicsScene, заменять используемый frame или переносить объект между frame без отдельной процедуры. Остановка сессии инвалидирует регистрации.
3. GlobalMotionPoseAdapter на корне того же NetworkObject, что transport. Bind только явно к зарегистрированному frame после placement. Отдельно различать локальную authority, server replica удалённого owner и client replica: CanPublish=false до baseline НЕ означает, что объект является remote.
4. Parent-local снимок применять только при совпадении session/object/lifetime родителя, его готовом baseline, настоящем Transform-parent и общем frame/PhysicsScene. Нет fallback к world `(0,0,0)` или автоматического SetParent.
5. Baseline применяется один раз на binding до acknowledgement. После этого authority только публикует собственную позицию; обычные эхо-снимки ей не применяются. Server replica использует latest accepted без interpolation; remote client — buffered sample с явной задержкой.
6. CharacterController.enabled сохраняется через try/finally. Dynamic Rigidbody допустим для authoritative baseline; регулярная remote запись разрешена только kinematic Rigidbody и только в FixedUpdate. Velocities/isKinematic/constraints/Animator не перенастраиваются автоматически.
7. Активный NavMeshAgent, который пишет pose, блокирует обычную запись. Для authoritative начального baseline допускается только подтверждение уже совпадающей позы без её перемещения. Nested Rigidbody/joints и перемещение NavMesh baseline требуют специализированной интеграции, не молчаливого упрощения.
8. При неготовом frame/родителе/driver публикация приостанавливается. Возобновление того же binding не откатывает local send sequence к запоздавшему echo.
9. Game-specific movement/input/root-motion readiness и cache reset ещё должны быть подключены к NetworkPlayer/ShipController/NpcBrain. Публичный IsReadyForSimulation — контракт для этой интеграции, не заявление, что старые контроллеры уже его читают.
10. ServerReplica с интерполируемым Rigidbody-родителем блокируется: render Transform родителя нельзя выдавать за его текущую physics pose. ClientReplica использует visual-parent представление; полное согласование ship/passenger physics timeline остаётся отдельной интеграцией. Интерполяция родителя не отключается автоматически.
11. Adapter — NetworkBehaviour с синхронным OnNetworkDespawn cleanup. Pool despawn+respawn не зависит от того, успеет ли Update увидеть IsSpawned=false. Набор и порядок NetworkBehaviour обязаны совпадать на сервере и клиентах при будущей миграции префабов.

## Источники

Просмотрены официальные Unity 6.5 API Rigidbody.MovePosition, Rigidbody.position, Physics.SyncTransforms; публичные NetworkManager start/stop events и physics API проверяются reflection. Установленные Unity/NGO не обновляются.
- `https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Rigidbody.MovePosition.html`
- `https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Rigidbody-position.html`
- `https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Physics.SyncTransforms.html`

Первый веб-запрос страницы PhysicsSceneExtensions не дал результата. Последующий reflection подтвердил `UnityEngine.PhysicsSceneExtensions` из UnityEngine.PhysicsModule; компиляция scene.GetPhysicsScene() успешна. Первый timeout при проверке Physics.SyncTransforms устранён повторным read-only запросом, метод подтверждён. Пакеты не менялись.

## Файлы

В `Assets/_Project/Scripts/World/FloatingOrigin/Network/`:
- `GlobalMotionApplication.cs` — pure role/projection/driver policy, итоговый MotionUnityPose, monotonic resume sequence.
- `GlobalMotionWorld.cs` — один issuer на живую NGO-сессию, immutable frame registrations по PhysicsScene, actor registry и baseline/FixedUpdate pump. Не создаёт PhysicsScene и не загружает/перемещает контент.
- `GlobalMotionPoseAdapter.cs` — explicit Bind, lifecycle, authoritative capture, baseline acknowledgement, remote Transform/CC/kinematic Rigidbody application, parent-chain readiness и fail-closed driver guards.
- `GlobalMotionReplicator.cs` — добавлен локальный RevokeBaselineAcknowledgement и сохранение уже отправленной sequence при pause/resume того же binding. Новый binding/despawn сбрасывает этот флаг. Wire format не изменён.

Новый validator: `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionApplication.cs`.
Меню: `ProjectC/World/Floating Origin/Validate Frame Pose Contracts`.
Unity-generated meta четырёх новых скриптов включаются вместе с исходниками.

## Явный порядок интеграции (ещё не выполнен на игровых объектах)

1. Добавить coordinator на GameObject NetworkManager и одинаковый transport/adapter layout на обе стороны ТОЛЬКО в отдельном этапе миграции.
2. После действительного content placement зарегистрировать local frame через RegisterFrame(id, coordinates, physicsScene). Регистрация — утверждение готовности вызывающим загрузчиком, не автоматическое преобразование legacy сцены.
3. После NGO spawn вызвать adapter.Bind(world, frameId). Bound context проверяет manager, NetworkObject-root, PhysicsScene и конфликтующий NT.
4. Сервер вызывает StartWorldStream с настоящей global позицией либо StartParentStream с готовым parent adapter. Для remote owner нужны новые game-specific validation rules; callback дополнен проверкой frame/binding/parent. Одна проверка координат не является готовым anti-cheat.
5. Coordinator применяет/подтверждает baseline до публикации. Authority поздно захватывает pose (у Rigidbody — physics position/rotation, а не interpolated visual Transform); server replica обновляется до physics из latest accepted, обычные client replicas используют render-time buffer. Задержка по умолчанию 0.1 сек — не игровая настройка, подтверждённая тестом.
6. После смены владельца/родителя game coordinator ЯВНО вызывает ReactivateFromCurrentPose или Start*Stream с актуальными правилами. Bind сам поток не воскрешает. Нельзя автоматически реактивировать произвольно остановленный поток.

### Disable / shutdown / pooling

- Отключение adapter намеренно выполняет Unbind и StopServer на сервере. Это безопасная остановка, не прозрачная пауза; повторное включение требует Bind и явной серверной реактивации.
- Отключение GlobalMotionWorld освобождает frames/actors, но сохраняет issuer, пока та же NGO-сессия жива. Иначе старые replicators отвергли бы новый issuer как чужую сессию. Start/stop callbacks остаются подписанными при disable; реальный network stop очищает issuer; OnDestroy отписывает callbacks.
- Despawn отделяет actor синхронно и без отправки Stop RPC из callback. Pool reuse требует нового явного Bind. Повторное создание самого coordinator посреди живой сессии не является поддержанной заменой сервиса.
- Ошибка Unity pose setter переводит adapter в Faulted и останавливает публикацию/серверный stream. Здесь нет обещания transactional rollback Transform или полной остановки старых gameplay scripts: их readiness hooks ещё не подключены.

## Фактически выполненные проверки

Последняя проверка компиляции после всех C# изменений: **No compile errors**.
Фактически вызваны все четыре Run() в стабильном Edit Mode, без GameObject creation, Play Mode и physics simulation:

| Набор | PASS | FAIL |
|---|---:|---:|
| ValidateGlobalMotionApplication | 32 | 0 |
| ValidateGlobalMotionTransport | 32 | 0 |
| ValidateGlobalMotionProtocol | 33 | 0 |
| ValidateFloatingOriginFoundation | 23 | 0 |
| **Всего** | **120** | **0** |

Новые проверки покрывают роли до acknowledgement, owner handoff gap, запрет authority echo/remote dynamic-body writes, активный NavMesh writer и matching baseline, parent lifetime/session/id, affine/scaled/rotated projection, invalid/out-of-frame rejection, точность около 10^9 м, 1000 parent projection samples, sequence resume/wrap, immutable descriptor, наличие NGO despawn override, execution-order attributes и запрет interpolated parent для server replica. 1000 samples — один тестовый случай, не 1000 отдельных PASS.

Рецензия исходников подтвердила, что компоненты не установлены на игровые сцены/префабы и не отключают Animator/gameplay scripts. Lifecycle native callbacks, реальная последовательность NGO callbacks, restore CharacterController flags, Rigidbody interpolation/sleep и spatial registry в живой сессии **не тестировались**. Pure policy PASS не доказывает физическое поведение.

## Остаётся / следующий T-FO04D

Подключить dormant readiness/lifecycle/cache hooks к реальным actor controllers и подготовить согласованную spawn/parent/prefab миграцию. Не включать старый/новый world shift до готовности content placement, позиционных RPC/persistence и обязательных physics/nav границ. Нерешённые задачи: global-aware NGO spawn placement, начальная готовность игроков/камеры, game-specific validation, NavMesh warp/path handoff, физический корабль/пассажиры и вложенные тела/joints, распределённые server regions, root-motion и platform caches.

Game-ready Host + 2 clients, disable/enable, pool reuse, late join/reconnect, ownership/parent transitions, moving deck и screenshots остаются пользовательской приёмкой после подключения. Сейчас игровой прогон этих компонентов не требуется, потому что они не участвуют в работающей игре.
