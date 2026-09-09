# T-FO04F — custom hierarchy from trusted baselines

Дата: 2026-09-09. Baseline: `d25f1dbf`. Статус: **ограниченный parent/unparent слой реализован, compile PASS, 234 pure checks PASS / 0 FAIL**. Это подэтап T-FO04, не завершение spawn bootstrap, native migration или runtime приёмки. Global mode не включён.

## 1. Подтверждённая проблема

После T-FO04E native AutoObjectParentSync должен быть выключен, но PoseAdapter.TryParent и transport activation требовали заранее правильный Transform.parent. Получатель нового ParentLocal control не мог сам установить hierarchy.

Проверенный локальный NGO `NetworkObject.cs`: TrySetParent возвращает false при AutoObjectParentSync=false; OnTransformParentChanged на строках 2368–2374 тоже сразу возвращает. Следовательно, custom SetParent в этом режиме не обеспечивает штатный NGO parenting callback/Stop/reparent. Его нельзя использовать как недостающий канал доставки hierarchy.

## 2. Реализованная последовательность

1. Сервер вызывает `GlobalMotionWorld.StartParentStream` с **явными parent-local position/rotation/local scale** либо `StartWorldStream` с **GlobalPosition, world rotation и явным local scale**. Caller не должен предварительно менять Transform.parent.
2. Replicator формирует новый binding/discontinuity и candidate control, но ещё не публикует его.
3. `GlobalMotionPoseAdapter.CanPrepareControl` проверяет candidate role, frame, prospective hierarchy, pose projection, native driver ограничения и participant preflight. После callbacks перечитываются структура и план.
4. Replicator повторно проверяет spawned/server/enabled/listening, competing writer, custom sync config, owner/revision/session. Только после этого устанавливает и публикует control. Обычный отказ до публикации не заменяет старый control; неиспользованная первая allocation может оставить пропуск generation, но не публикуется.
5. Получатель нового baseline сначала разрешает parent identity и готовность, не требуя уже установленного Transform.parent. При отсутствии готового parent остаётся в WaitingForParent без записи и без ACK; появление готового parent допускает повторный проход.
6. После повторного participant preflight и проверки актуальности выполняются `SetParent(desiredParent, false)` и запись **явной новой позы**, затем cache callbacks. При World baseline убирается управляемый network ancestor; обычный non-network content container сохраняется.
7. ACK выдаётся только после callbacks, participant readiness и повторной проверки `IsBaselinePlaced`/exact binding. Native approvals D остаются отдельным обязательным gate.

Обычные motion samples и повтор того же binding **не выполняют SetParent**. Если hierarchy произвольно изменилась при прежнем binding, путь блокируется, а не пытается исправить её каждым кадром. Parent change не выводится из величины Vector3 или полученного origin.

## 3. Ограничения и guards

- Parent должен быть зарегистрирован в том же GlobalMotionWorld, иметь тот же immutable frame и **ту же Unity Scene**. Одна PhysicsScene ещё не даёт права менять ownership/unload-семантику разных сцен.
- Проверяются точные session/object/spawn identity и готовность parent. NetworkObjectId=0 допускается как настоящий ID, не трактуется как отсутствие parent.
- Self/descendant Transform cycles вычисляются через фактическую hierarchy; при цикле не вызывается рекурсивная readiness-проверка. Pending cyclic/unready control chains не разрешаются автоматически: они остаются заблокированными. Pure bool-policy тест не является тестом native Transform graph.
- Нельзя detach от неизвестного/unmanaged network ancestor. Обычный non-network container не снимается без нужды.
- Смена hierarchy запрещена для Rigidbody/joints в поддереве, произвольных Collider/2D и включённого NavMeshAgent. Native body/nav state не замораживается и не переносится этим слоем.
- Root CharacterController допускается без дополнительных colliders: enabled временно выключается и восстанавливается в finally; масштаб prospective parent должен быть единичным. Это ещё не доказанная игровая проходимость/physics-регрессия.
- Server-side projection не читает interpolated Rigidbody render transform родителя, в том числе для server authority. Client owner/view сохраняют разрешённую прежнюю render-policy; server physics bridge должен предоставить корректное представление отдельно.
- Velocity/angular velocity, HP, docking, pilots, aggro, Animator и native paths не переписываются ради hierarchy. Не используется запись velocity у kinematic тела.
- Native SynchronizeTransform/AutoObjectParentSync и startup lease проверяются не только при Bind, но и в текущем context.

### Ошибки после начала записи

SetParent может вызвать другие MonoBehaviour callbacks даже при выключенном NGO auto-parenting. После него проверяются actual parent, context/frame и binding. Ошибка установки/pose/cache callback приводит к Faulted, revoke ACK и StopServer при живой сети.

**Автоматического rollback hierarchy/physics нет.** После частичной записи parent/pose могут остаться изменёнными, CharacterController.enabled восстанавливается. Это не транзакция всего игрового мира и не гарантия остановки автономной физики: дальнейшее восстановление/паузу обязан обеспечить внешний native coordinator. Нельзя возобновлять gameplay только потому, что метод вернул false.

## 4. Совместимость

Новая семантика требует native protocol **0xF002** вместо T-FO04E **0xF001**. Strict hello содержит новую версию; смешение этих global реализаций отвергается. Диапазон **0xF000–0xF0FF** зарезервирован: старую global конфигурацию нельзя трактовать как legacy при unassigned/disabled profile. Нормальный legacy protocol=0 не меняется.

Motion snapshot/control serialization format остаётся version=1, новых RPC или float parenting payload не добавлено. Исторические E отчёты/JSON сохраняют результаты E и не переписаны под F.

## 5. Фактическая проверка

Первый compile нового Editor validator нашёл две CS1729: internal конструктор GlobalMotionPose недоступен editor assembly. Исправлено использованием публичного GlobalMotionBuffer.BeginStream/TrySample, как в существующем application validator; visibility production API не расширялась.

Source review также привёл к повторным post-preflight guards, явной передаче вычисленного wouldCycle, повторному structural preflight и общему FaultActor с listening guard. Финальная проверка компиляции: **No compile errors**. Все семь Run() фактически выполнены in-editor в стабильном Edit Mode, не только прочитаны.

| Набор | PASS | FAIL |
|---|---:|---:|
| ValidateGlobalMotionHierarchy | 40 | 0 |
| ValidateGlobalMotionNetworkContracts | 48 | 0 |
| ValidateGlobalMotionActorReadiness | 26 | 0 |
| ValidateGlobalMotionApplication | 32 | 0 |
| ValidateGlobalMotionTransport | 32 | 0 |
| ValidateGlobalMotionProtocol | 33 | 0 |
| ValidateFloatingOriginFoundation | 23 | 0 |
| **Итого** | **234** | **0** |

Артефакт: `04F_STATIC_VALIDATION.json`. Новые проверки покрывают metadata identity/generations/zero id, frame/scene/cycle/readiness facts, native-driver policy, parent projection при origin порядка 10^9, независимые origins и explicit-global detach, reserved protocol/old hello rejection и compiled preflight APIs.

Повторный read-only asset guard: 1 prefab list, **58 candidate prefabs**, opt-in candidates=0, profile assets=0, loaded enabled profiles=0, loaded opt-in adapters=0. Это та же ограниченная выборка E, не весь проект и не unloaded scene coverage.

**UNTESTED:** реальные SetParent callbacks, transient physics representation, CharacterController поведение, actual late join/reconnect/pooling/parent destruction, Host + clients, native physics/nav и gameplay. Play Mode, сетевые соединения, тестовые GameObject, physics simulation и screenshots не запускались. Static/pure PASS не подтверждает устранение jitter.

## 6. Файлы и продолжение

- Новый `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionHierarchy.cs` и его Unity-generated meta.
- Новый `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionHierarchy.cs` и его Unity-generated meta.
- Изменены GlobalMotionPoseAdapter.cs, GlobalMotionReplicator.cs, GlobalMotionWorld.cs, GlobalMotionNetworkContract.cs и NetworkManagerController.cs.
- Обновлены этот отчёт, generated F JSON, roadmap и существующий `Assets/_Project/Docs/ITERATIONS.md`.
- Сцены/префабы/профиль не редактировались. NpcBrain, ShipCrewSpawner и player gameplay parenting в этом подэтапе не перенаправлены: им ещё нужны physics/nav/deck/proxy/RPC bridges.

Следующая часть T-FO04 — concrete global spawn bootstrap и initial frame/pose placement, scene catalog и lifecycle children в согласовании с T-FO05–08. Работающий на текущих NPC/кораблях full parent handoff не объявляется. World shift и включение global режима остаются закрыты до интеграционных gates и пользовательской приёмки.

Один коммит кода/meta/документации, без отдельного коммита собственного хеша. Temp runner и несвязанный LiberationSans fallback не включаются.
