# T-FO04D — dormant actor readiness и baseline cache hooks

Дата: 2026-09-09. Baseline: `bc128bf8`. Статус: **dormant actor hooks реализованы; compile PASS, 146 чистых проверок PASS / 0 FAIL**. Полная spawn/prefab/physics/nav миграция ещё не выполнена. Код и документация фиксируются одним коммитом.

## Ограниченный scope

Добавить opt-in координатный режим (по умолчанию false), отдельные procedural readiness gates для NetworkPlayer/ShipController/NpcBrain и навыков, контракт baseline callbacks до publication acknowledgement. Не устанавливать компоненты на сценах/префабах. Existing legacy path остаётся прежним, если global mode не запрошен.

Это не полный runtime gate и не автоматический rebase/physics/nav handoff. Native Rigidbody/NavMeshAgent нельзя заморозить одним early return: для будущей активации physics/nav должны быть явно подготовлены ДО baseline и подтверждены после применения позы. Если это не сделано, baseline/публикация блокируются. Не считать getters готовой миграцией всех RPC, persistence, камер, навигационных целей и серверных регионов.

## Проверенные зависимости и ограничения

- NetworkPlayer.Update вызывает ProcessMovement, а тот CharacterController.Move. FixedUpdate отвечает за legacy correction. Поэтому gate нужен в обоих местах и в ProcessMovement, а не только FixedUpdate.
- SkillInputService самостоятельно опрашивает ввод, TryActivate вызывается также напрямую. SkillAnimationPlayer имеет delayed trigger, root-motion Y clamp, animation event и fallback impact — все требуют собственного gate.
- ShipController.FixedUpdate НЕ единственный writer: NPC flight NavTick управляет Rigidbody отдельно. Оба пути входят в проверяемую границу.
- NpcBrain.Update/FixedUpdate и direct ForceChase/ForceFlee используют координаты. NpcSocialBrain имеет внешний Tick, не отдельный Update.
- SetInputEnabled, ExitDocked, ApplyPersistenceFreeze и ForceSurrender НЕ используются для coordinate pause: они меняют death/input/docking/behavior state.
- Кэши player platform-last-position, input deltas, NPC ride/proxy-last-position и skill cast-Y отделены от настоящих velocity, HP, aggro, docking и cooldown. Sandbox proxy не сдвигать как world point. NPC spawn anchor инициализировать только для новой lifetime; ordinary baseline не переносит home автоматически.

## Контракт

Baseline placement и simulation-ready становятся двумя отдельными стадиями. Root participants выполняют preflight и cache callbacks; ошибочный callback не разрешает публикацию. Ship/NPC authority дополнительно требуют exact-binding подтверждения подготовленной native physics/nav. Confirm API не замораживает/не размораживает native компонент и сам не выполняет подготовку. Mode latch не разрешает скрытый возврат к legacy при удалении/disable adapter.

### Новый API и порядок

1. `GlobalMotionPoseAdapter._coordinatesRequired` — private serialized opt-in, default false. Bind и ContextValid теперь требуют opt-in. На текущих сценах/префабах adapter не установлен; игровой режим не меняется.
2. `GlobalMotionActorState` хранит необратимый для этой instance mode latch, applied binding и отдельный native-prepared token. Новый session/object/spawn/authority/discontinuity/parent binding отзывает старую native approval. Повтор ТОГО ЖЕ binding её не сбрасывает. Despawn очищает lifetime/token, но не позволяет молча вернуться к legacy.
3. `IGlobalMotionActorParticipant`: `CanApplyGlobalBaseline(role)` → `OnGlobalBaselineApplied(binding)` → `IsGlobalMotionReady(role)`. Список root participants читается при Bind. Ошибка/уничтожение обязательного участника блокирует pipeline; callback exception даёт Faulted + revoke acknowledgement/StopServer.
4. `IsBaselinePlaced` означает только проверенную применённую позу и завершённые cache callbacks. Это НЕ разрешение движения. `IsBaselineReady` дополнительно требует actor readiness; только затем выполняется acknowledgement и становится возможна публикация. Reentrancy guards не разрешают callback получить готовность до окончания baseline.
5. `StartWorldStream`/`StartParentStream` могут вернуть true после успешного placement с `WaitingForActors`. Вызывающий обязан проверить readiness отдельно. Нельзя трактовать true как подтверждение работающей native simulation.
6. Для authority ShipController нужны externally prepared Rigidbody и `ConfirmGlobalPhysicsPrepared(binding)`, для NpcBrain — подготовленный world/deck nav и `ConfirmGlobalNavigationPrepared(binding)`. Confirm проверяет exact current placed binding. Bridge, который реально выполняет эту подготовку, **ещё не создан**.
7. Remote pilot input использует `CanObserveInCurrentCoordinates`, а не local-authority gate: клиент наблюдает серверный корабль и всё равно должен иметь возможность посылать pilot input. Серверный приемник и direct NPC input используют строгий simulation gate.

### Фактически затронутые границы

| Файл / путь | Добавленный gate или callback |
|---|---|
| `Scripts/Player/NetworkPlayer.cs` | Update, FixedUpdate, ProcessMovement; очистка queued input, platform deltas, proximity/pending-interaction caches, legacy correction cache; lifecycle reset |
| `Scripts/Player/ShipController.cs` | FixedUpdate до telemetry/forces, ApplyServerInput, SubmitShipInputRpc, SendShipInput; input accumulator reset; native-ready token |
| `Scripts/AI/NpcBrain.cs` | Update/FixedUpdate, ForceChase/ForceFlee; first-lifetime spawn point, ride/proxy last-pose refresh; native-nav token |
| `Scripts/AI/NpcSocialBrain.cs` | Внешний Tick |
| `Scripts/PeacefulShip/Stations/NpcShipController.cs` | Отдельный NavTick перед Rigidbody/autopilot logic |
| `Scripts/Skills/SkillInputService.cs` | Независимый polling и direct TryActivate |
| `Scripts/Skills/SkillAnimationPlayer.cs` | Deferred trigger, cast Y clamp, Play, event/fallback impact; coordinate-mode stale cast cancellation и безопасная смена ссылки Animator |

Пути таблицы относительны `Assets/_Project/`. Native freeze не подменяется EnterDocked/ExitDocked, ForceSurrender, SetInputEnabled или ApplyPersistenceFreeze. Ни одна новая coordinate-pause ветка не переключает Rigidbody.isKinematic, не включает NavMeshAgent и не меняет HP/pilots/aggro/cooldown.

Player baseline callback не меняет `_velocity`, `_inputEnabled`, CharacterController.enabled и piloting state. Ship callback не меняет настоящую физическую скорость, smoothing state, engine/pilots/docking/cargo. NPC callback не меняет цели/состояние/родителя и не делает Warp; `_proxyLastPos` обновляется из настоящего sandbox proxy без world offset.

Skill cast отменяется только в opt-in режиме при недоступных координатах или новом teleport/handoff baseline: delayed trigger и impact не должны проиграться позже в другом контексте. Используется существующий Restore с сохранением прежнего root-motion флага; Animator.enabled и клипы не меняются. **Этот callback нельзя применять как обработчик ordinary origin rebase**: для rebase потребуется отдельный перенос cache без отмены действия.

### Legacy-position firewall игрока

В opt-in режиме блокируются старые `Vector3` пути TeleportLocal/TeleportToPosition/TeleportAllClientRpc, legacy position correction и вызов старого player position restore. Они не могут перезаписать уже локализованный Transform. Возвращается warning вместо неявной интерпретации Vector3. Legacy mode проходит исходный код без этих ограничений. Формат RPC/DTO и сохранений здесь не мигрируется; global-aware замена ещё нужна в T-FO05.

### Важные ограничения

- Native Rigidbody продолжает симуляцию независимо от раннего return в ShipController; NavMeshAgent также автономен. Этот этап НЕ доказывает, что native объекты заморожены при ожидании frame. Preflight запрещает baseline, пока внешняя native подготовка не выполнена.
- Confirm API является явным утверждением доверенного physics/nav bridge, а не автоматической проверкой всех игровых механик. Перед Confirm должны быть восстановлены region, paths/goals, social/respawn и NPC ship navigation caches. Сейчас ни один рабочий объект это подтверждение не вызывает.
- Lookup adapter кэшируется на actor instance. Runtime AddComponent/замена NetworkBehaviour после Awake не поддерживается и запрещена контрактом prefab migration; layout должен быть одинаков на обеих сторонах. Режим нельзя прозрачно переключать обратно в legacy на уже работавшей global instance.
- Готовность не является полным closed-world census: остаются positional DTO/RPC, камеры, persistent restore остальных объектов, event/coroutine paths, специальные physics writers, wind/altitude/shaders, social waypoints/activities, NPC respawn и полная world/deck/sandbox интеграция.
- Native approvals в этом этапе относятся к motion binding. Будущая rebase/region транзакция обязана отдельно учесть frame lifetime/epoch; простая замена frame без транзакции по-прежнему запрещена.

## Файлы нового слоя

- `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionActorContract.cs` — новый contract/state/link.
- `GlobalMotionPoseAdapter.cs` и `GlobalMotionWorld.cs` в том же каталоге — opt-in и two-stage baseline.
- `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionActorReadiness.cs` — новый pure validator.
- Семь игровых исходников из таблицы — targeted hooks/gates. Unity-generated meta нового contract и validator включаются в коммит.

## Фактические проверки

Последняя compile-проверка после всех C# изменений: **No compile errors**. Source review подтвердил неактивность hooks при отсутствии adapter/opt-in=false и отсутствие добавления нового режима на сцены/префабы.

Фактически вызваны пять Run() в стабильном Edit Mode:

| Набор | PASS | FAIL |
|---|---:|---:|
| ValidateGlobalMotionActorReadiness | 26 | 0 |
| ValidateGlobalMotionApplication | 32 | 0 |
| ValidateGlobalMotionTransport | 32 | 0 |
| ValidateGlobalMotionProtocol | 33 | 0 |
| ValidateFloatingOriginFoundation | 23 | 0 |
| **Всего** | **146** | **0** |

Новые проверки: legacy/no-adapter policy, sticky requirement, placed/native-approved separation, stale/foreign/default binding rejection, same-binding preservation, authority/parent/teleport/lifetime changes, reset/pool reuse (1000 samples в одном случае), наличие compiled actor interfaces и confirm/gate APIs. Reflection проверяет наличие контрактов, а не выполнение Unity callbacks.

**Play Mode, native simulation, тестовые GameObject, реальные Host/clients и screenshots не запускались.** Native callback order, реальное восстановление физики/nav, поведение Animator/cast и gameplay-регрессии остаются UNTESTED. Чистые проверки не подтверждают устранение тряски.

## Следующий этап

T-FO04E — подготовка согласованных NGO spawn/parent/prefab activation contracts и совместимости сетевого layout, с учётом ещё незавершённых RPC/persistence/content/native adapters. Не включать новый режим на игровых префабах до закрытия обязательных границ; не включать world shift. Пользовательская игровая приёмка потребуется после полноценного подключения, не для нынешних dormant hooks.
