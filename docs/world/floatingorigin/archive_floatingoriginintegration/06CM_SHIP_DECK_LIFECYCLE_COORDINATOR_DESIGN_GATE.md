# T-FO06CM — ShipDeck lifecycle coordinator design gate

Дата: 2026-09-12  
Статус: **DESIGN-ONLY / READ-ONLY CENSUS / DESIGN SELECTED / IMPLEMENTATION BLOCKED**

## 1. Цель

После `T-FO06CL` расширить read-only census всех ShipDeck lifecycle boundaries и выбрать одного protocol-owned server lifecycle coordinator/ledger owner для будущего `IGlobalMotionShipDeckPassengerGenerationSource`, не создавая реализацию и не выполняя binding.

Аудит выполнен read-only. `NpcShipController`, `ShipCrewSpawner`, `NpcBrain`, `ShipDeckNav`, NGO lifecycle, provider, adapter set, combined transaction host, `BootstrapScene` и runtime state не изменялись.

## 2. Проверенные источники

```text
Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs
Assets/_Project/Scripts/PeacefulShip/Crew/ShipCrewSpawner.cs
Assets/_Project/Scripts/AI/NpcBrain.cs
Assets/_Project/Scripts/Ship/ShipDeckNav.cs
Assets/_Project/Scripts/World/Scene/ScenePlacedObjectSpawner.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerGenerationContract.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleProducerContract.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleSourceBindingContract.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckPassengerLifecycleBindingHandoffContract.cs
Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionShipDeckCombinedTransactionHost.cs
```

## 3. Broadened lifecycle census

В audited paths подтверждены следующие раздельные seams, но не единый protocol-owned ledger:

- `NpcShipController`: server-only `OnNetworkSpawn` регистрирует NPC ship в `NpcShipZoneRegistry` и `NpcShipWorld`; `OnNetworkDespawn` снимает эти регистрации. При отсутствии явного `npcInstanceId` текущий код выводит его из `NetworkObjectId`. Это ship registration/lifetime seam, но не explicit monotonic ship lifetime/spawn generation и не lifecycle receipt producer.
- `ShipCrewSpawner`: server-only `OnNetworkSpawn` запускает `EnsureCrewSpawned`; `SpawnMember` создаёт crew `NetworkObject` и вызывает NGO `Spawn(destroyWithScene: true)`. Existing crew переиспользуется по текущим runtime checks, stale entries удаляются из локальной map по `IsSpawned`, а `OnNetworkDespawn` очищает map. У spawner нет protocol receipt для каждой accepted spawn/despawn epoch, passenger ledger или terminal ship invalidation.
- `NpcBrain`: server-only `AttachToShipDeck`/`DetachFromShipDeck` задают requested/active attachment. Активность требует spawned ship, успешного parent и затем `ShipDeckNav.IsReady`; detach очищает attachment state и ride state. Это фактический attach/detach seam, но текущие flags, object reference и `NetworkObjectId` не являются generation source и не дают ordered immutable receipts.
- `ShipDeckNav`: server-side `OnNetworkSpawn` ставит регистрацию в asynchronous queue, `IsReady` появляется только после успешной NavMesh registration, `OnNetworkDespawn` выполняет unregister. Внутренний `RegistrationGeneration` относится к NavMesh registration и не является ship lifetime generation или passenger attachment generation. Readiness является prerequisite для attachment evidence, но не ledger owner.
- NGO boundaries: `ScenePlacedObjectSpawner` и `ShipCrewSpawner` показывают отдельные server-side `Spawn` paths; `OnNetworkSpawn`/`OnNetworkDespawn`, `IsSpawned`, `NetworkObjectId` и ownership state остаются object/lifecycle observations. В audited paths нет одной protocol-atomic границы, которая одновременно владеет ship spawn/despawn, passenger attach/detach и ownership/invalidation order. Ownership boundary не может быть восстановлена из текущего owner value или callback order.

Следствие: `NpcBrain` и `ShipCrewSpawner` имеют нужные локальные seams, но каждый покрывает только часть lifecycle. `NpcShipController` ближе к ship registration, однако его registry callbacks также не владеют passenger ledger, NGO ownership transition и ordered invalidation.

## 4. Выбранный design owner

Выбран один будущий protocol-owned server owner: **`GlobalMotionShipDeckPassengerLifecycleCoordinator`**.

Это design name для единственного server-side coordinator и ledger owner, а не существующий класс и не создаваемый в этом этапе компонент. Он должен владеть immutable lifecycle ledger, server/protocol authority и выдачей receipts для одного ship lifetime. Existing components остаются источниками explicit transition facts:

- `NpcShipController` сообщает reviewed ship registration/lifetime boundary;
- `ShipCrewSpawner` сообщает фактический accepted crew spawn/despawn boundary;
- `NpcBrain` сообщает только explicit attach/detach completion;
- `ShipDeckNav` сообщает readiness evidence, необходимое до `PassengerAttached`.

`NpcBrain` намеренно **не выбран** owner-ом: он passenger-local и не владеет ship registration/despawn или NGO ownership. `ShipCrewSpawner` намеренно **не выбран** owner-ом: он spawn/cleanup seam и не владеет attach/detach completion, ship lifetime invalidation или protocol ordering. Выбор сделан по полноте server lifecycle authority, а не по наличию существующих callbacks.

## 5. Receipt mapping

Будущий coordinator/ledger должен выдавать только explicit receipts следующего типа:

| Receipt | Source fact и момент выпуска | Обязательная роль |
|---|---|---|
| `ShipRegistered` | Server подтверждает ship registration и начало нового ship lifetime после accepted spawn boundary. | Открывает ship ledger с explicit ship identity и новым monotonic ship lifetime/spawn generation. Не создаётся из одного `IsSpawned` или `NetworkObjectId`. |
| `PassengerAttached` | Server подтверждает active `NpcBrain` attachment, matching ship lifetime, parent/attachment completion и `ShipDeckNav.IsReady`. | Открывает passenger attachment epoch с новым explicit monotonic attachment generation; requested или merely spawned state недостаточны. |
| `PassengerDetached` | Server подтверждает завершённый detach для активной passenger attachment epoch. | Закрывает matching attachment generation; следующий attach получает строго большую generation. Не выводится из destroyed reference, `IsSpawned=false` или cleanup map. |
| `ShipInvalidated` | Server coordinator принимает terminal ship despawn/invalidation boundary. | Закрывает ship lifetime, фиксирует last ledger order и invalidation reason, явно перечисляет незакрытые passenger epochs если они есть; после него новые attach/detach receipts запрещены. Это не synthetic `PassengerDetached`. |

Каждый receipt должен включать protocol ship identity, explicit ship lifetime/spawn generation, server/protocol ownership, monotonic ledger order и только применимые passenger identity/attachment generation. `PassengerDetached` повторяет закрываемую attachment generation; новая generation выдаётся только новой accepted attachment epoch. Дубликаты, stale receipts и receipts после terminal invalidation отклоняются fail-closed.

## 6. Generation, identity и order requirements

- Ship lifetime/spawn generation выдаётся coordinator-ом server-side и строго возрастает для каждого нового accepted ship lifetime; despawn/recreate не может повторно использовать generation.
- Passenger attachment generation выдаётся coordinator-ом для конкретной explicit ship identity + passenger identity; новая attachment epoch получает строго большее значение, а detach закрывает именно активное значение.
- Ship identity и passenger identity должны быть explicit и стабильными в protocol scope. `NetworkObjectId` может быть дополнительной текущей NGO transport identity, но не generation и не доказательством lifetime continuity.
- Каждый receipt имеет строгий monotonic ledger order. Допустимый порядок: `ShipRegistered` → zero or more ordered `PassengerAttached`/`PassengerDetached` pairs → terminal `ShipInvalidated`; attach до `ShipRegistered`, detach без matching active attach и любое событие после invalidation отвергаются.
- Receipt обязан нести server authority и protocol ownership. Client-local observations, host-only callbacks и текущий NGO owner value не могут выпускать или подтверждать ledger transition; ownership change требует отдельной reviewed server boundary и не переносит старую generation молча.
- Ship lifetime identity, passenger identity, attachment generation, ledger order, ownership/authority и invalidation reason проверяются вместе. Несовпадение любой части, повторное использование generation или поздний callback делает receipt stale и блокирует binding.

Запрещены synthetic generation и inference из `NetworkObject.IsSpawned`, `NetworkObjectId`, Unity/NGO object references и host-local binding generation. В частности, `ShipDeckNav.RegistrationGeneration` и binding generation combined host не могут быть преобразованы в ship/passenger lifecycle generation.

## 7. Будущий binding path

После отдельной реализации и server-only verification выбранный coordinator должен:

1. принимать explicit transition facts от перечисленных seams, не обнаруживать их через object scan;
2. вести protocol-owned ledger и выпускать reviewed immutable `ShipRegistered`, `PassengerAttached`, `PassengerDetached`, `ShipInvalidated` receipts;
3. реализовать `IGlobalMotionShipDeckPassengerGenerationSource` поверх этого ledger, возвращая только один согласованный ship lifetime/passenger generation view;
4. пройти существующий lifecycle source binding contract и затем explicit handoff в `GlobalMotionShipDeckCombinedTransactionHost` через reviewed lifecycle binding receipt;
5. только после owner review пройти provider/adapter binding и runtime integration отдельным gate.

Combined transaction host должен получать reviewed binding receipt от coordinator/source, а не прямые callbacks `NpcBrain`, `ShipCrewSpawner` или `ShipDeckNav`. На этом этапе ни coordinator, ни source implementation, ни binding path не создаются.

## 8. Вывод и границы

Вердикт: **DESIGN SELECTED / IMPLEMENTATION BLOCKED**.

Broadened census выбрал один protocol-owned design owner — `GlobalMotionShipDeckPassengerLifecycleCoordinator` — и зафиксировал receipt/generation/order/invalidation contract. Это не доказательство, что такой producer уже существует, и не разрешение адаптировать существующий component seam без новой реализации.

```text
protocol-owned lifecycle coordinator = DESIGN SELECTED / NOT IMPLEMENTED
IGlobalMotionShipDeckPassengerGenerationSource = NOT IMPLEMENTED / NOT BOUND
provider binding = NOT EXECUTED
adapter binding = NOT EXECUTED
combined transaction host binding = NOT EXECUTED
BootstrapScene = UNCHANGED
Play Mode = NOT RUN
runtimeRebaseReadiness = NOT_READY
```

Поиск за пределами перечисленных audited paths остаётся **INCONCLUSIVE**. Это не является доказательством отсутствия другого lifecycle path во всём проекте. Следующий допустимый этап — отдельная implementation gate для выбранного coordinator, но только после explicit owner review и без synthetic generation shortcuts.
