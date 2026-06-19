# 09 — References

> **Каталог:** все референсные файлы проекта, на которые ссылается этот
> документ. Все ссылки file:line для удобной навигации.

> **Обновлено 2026-06-19:** переименовано из `08_REFERENCES.md` (новый
> `08_DEPARTURE_SUBSYSTEM.md`). Добавлены ссылки на T-DEPART-* тикеты.

---

## 1. GDD (Game Design Documents)

| Документ | Раздел | Описание |
|----------|--------|----------|
| `docs/gdd/GDD_10_Ship_System.md` | §7 (строки 394-432) | Дизайн стыковки (поток + DispatcherMessage struct) |
| `docs/gdd/GDD_10_Ship_System.md` | §8 (строки 436-449) | FSM корабля (включая Docking/Docked) |
| `docs/gdd/GDD_10_Ship_System.md` | §4.2 (строки 287-292) | Модули MODULE_AUTO_DOCK, MODULE_AUTO_NAV |
| `docs/gdd/GDD_10_Ship_System.md` | §2.2 (строки 49-58) | Коридоры высот городов (Примум 4348м и др.) |
| `docs/gdd/GDD_10_Ship_System.md` | §10.5 (строки 504-512) | Фаза 5: Co-Op & Docking (задачи 5.3-5.5) |
| `docs/gdd/GDD_10_Ship_System.md` | §10.6 (строки 513-521) | Фаза 6: Advanced (задачи 6.3 MODULE_AUTO_DOCK) |
| `docs/gdd/GDD_10_Ship_System.md` | §13.1-13.4 (строки 587-795) | Реализация в коде (Key, MetaRequirement) |
| `docs/gdd/GDD_01_Core_Gameplay.md` | §3.1-3.2 (строки 56-87) | Режимы пеший/корабль (управление) |
| `docs/gdd/GDD_01_Core_Gameplay.md` | §3 (строки 108-138) | Полная таблица клавиш + зарезервированные |
| `docs/gdd/GDD_INDEX.md` | — | Общий индекс GDD |

---

## 2. Проектная документация

### 2.1 Composite Ship (фундамент для DockStation composite)

| Документ | Описание |
|----------|----------|
| `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` | Обзор композитного корабля (Phase 0-1) |
| `docs/Ships/analysis-composite-ship.md` | Полный анализ (29 KB, 12 разделов) |
| `docs/Ships/roadmap-integration.md` | План реализации composite |

### 2.2 Key Subsystem (паттерн серверного hub)

| Документ | Описание |
|----------|----------|
| `docs/Ships/Key-subsystem/00_OVERVIEW.md` | Обзор Key subsystem |
| `docs/Ships/Key-subsystem/22_SHIP_TELEMETRY_PLAN.md` | Telemetry план (для DockingClientState паттерна) |
| `docs/Ships/Key-subsystem/28_KEY_ARCHITECTURE_REVIEW.md` | Глубокий обзор архитектуры (11 проблем) |
| `docs/Ships/Key-subsystem/99_CHANGELOG.md` | История изменений |
| `docs/Ships/cargo_system/CARGO_REFACTOR_PLAN_2026-06-17.md` | Cargo pattern |

### 2.3 NPC Quests (паттерн dialog + UI Toolkit)

| Документ | Описание |
|----------|----------|
| `docs/NPC_quests/00_README.md` | Обзор NPC+Quest подсистемы |
| `docs/NPC_quests/02_V2_ARCHITECTURE.md` | V2 архитектура (server-hub + DTO + ClientState) |
| `docs/NPC_quests/04_DIALOG_AND_QUEST_UI.md` | Dialog UI |
| `docs/NPC_quests/08_ROADMAP.md` | 50+ тикетов |

### 2.4 Markets (паттерн зоны + RPC)

| Документ | Описание |
|----------|----------|
| `docs/Markets/ARCHITECTURE.md` | Архитектура |
| `docs/Markets/FLOW_TRADE.md` | Поток |
| `docs/Markets/INTEGRATION.md` | Интеграция |
| `docs/Markets/TRADE_V2_DESIGN.md` | V2 дизайн |
| `docs/Markets/MARKETS_V2_AUDIT_2026-06-05.md` | Аудит |

### 2.5 MetaRequirement (паттерн для E-key chains)

| Документ | Описание |
|----------|----------|
| `docs/MetaRequirement/00_OVERVIEW.md` | Обзор |
| `docs/MetaRequirement/10_IMPLEMENTATION_GUIDE.md` | Гайд реализации |
| `docs/MetaRequirement/30_RUNTIME_FLOW.md` | Runtime flow |

### 2.6 Crafting (паттерн scene-placed NetworkBehaviour)

| Документ | Описание |
|----------|----------|
| `docs/Crafting_system/00_OVERVIEW.md` | Обзор |
| `docs/Crafting_system/10_DESIGN.md` | Дизайн |
| `docs/Crafting_system/20_IMPLEMENTATION_PLAN.md` | План |

---

## 3. Исходный код (по подсистемам)

### 3.1 Ship / Player (FSM, ShipFlightClass)

| Файл | Строки | Описание |
|------|--------|----------|
| `Assets/_Project/Scripts/Player/ShipController.cs` | 18-24 | enum `ShipFlightClass` |
| `Assets/_Project/Scripts/Player/ShipController.cs` | 41, 48 | поле `shipFlightClass` + геттер |
| `Assets/_Project/Scripts/Player/ShipController.cs` | 376, 387, 398, 409 | switch по ShipFlightClass |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 318 | F-key chain start |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 324 | TryInteractNearestCraftingStation call |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 433-436 | F-key chain mid (NPC, MetaRequirement) |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 662 | TryInteractNearestMetaRequirement method |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 720-738 | TryInteractNearestCraftingStation |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 770-810 | TryInteractNearestDoor |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 812-837 | TryInteractNearestNpc |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 837 | RequestTalkToNpcRpc call |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | 1323 | RequestTalkToNpcRpc с params |
| `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | (full) | Input action reference (для T-key) |
| `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` | (full) | Mode state (Walking/Ship) |

### 3.2 Ship composite (фундамент для DockStation)

| Файл | Строки | Описание |
|------|--------|----------|
| `Assets/_Project/Scripts/Ship/ShipRootReference.cs` | (full) | Marker pattern |
| `Assets/_Project/Scripts/Ship/ShipComponentLocator.cs` | (full) | Static helper |
| `Assets/_Project/Scripts/Ship/PilotSeatController.cs` | (full) | Per-part controller (пример для PadTriggerBox) |
| `Assets/_Project/Scripts/Ship/DoorController.cs` | (full) | Per-part controller (пример для коммуникации) |
| `Assets/_Project/Scripts/Ship/ModuleSlot.cs` | (full) | Module slot (будущее для AUTO_DOCK) |

### 3.3 Market (референс для OuterCommZone + DockZoneRegistry)

| Файл | Строки | Описание |
|------|--------|----------|
| `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` | 25-405 | Полный файл (406 строк) — главный референс |
| `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` | 56-67 | Awake (SphereCollider setup) |
| `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` | 69-122 | OnEnable + race-fix + register |
| `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` | 132-198 | Update + PollLocalPlayerZone |
| `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` | 218-282 | PollPlayersInRadius + PollShipsInRadius (debounced) |
| `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` | 285-327 | OnTriggerEnter / Exit |
| `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` | 336-393 | BuildNearbyShipsDtos (пример DTO) |
| `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` | 395-404 | OnDrawGizmos |
| `Assets/_Project/Trade/Scripts/Network/MarketZoneRegistry.cs` | 18-61 | Static registry (референс для DockingZoneRegistry) |
| `Assets/_Project/Trade/Scripts/Network/MarketZoneRegistry.cs` | 28-32 | LocalPlayerZone (паттерн) |
| `Assets/_Project/Trade/Scripts/Network/MarketTimeService.cs` | 28-150 | Singleton pattern |
| `Assets/_Project/Trade/Scripts/Network/MarketTimeService.cs` | 153-169 | NetworkingUtils (используем как есть) |

### 3.4 Quest (референс для серверного hub + dialog)

| Файл | Строки | Описание |
|------|--------|----------|
| `Assets/_Project/Quests/Network/QuestServer.cs` | 34-114 | Server hub skeleton |
| `Assets/_Project/Quests/Network/QuestServer.cs` | 50-58 | Rate limiting (copy-paste) |
| `Assets/_Project/Quests/Network/QuestServer.cs` | 494 | RequestTalkToNpcRpc (пример RPC) |
| `Assets/_Project/Quests/UI/DialogWindow.cs` | 32-114 | Singleton + UIDocument lifecycle |
| `Assets/_Project/Quests/UI/DialogWindow.cs` | 116-131 | Input subscription (F-skip) |
| `Assets/_Project/Quests/UI/DialogWindow.cs` | 193-205 | EnsureBuilt (UXML/USS loading) |
| `Assets/_Project/Quests/Client/QuestClientState.cs` | (full) | Singleton projection (референс для DockingClientState) |

### 3.5 Exchange (референс для singleton + scene-placed NetworkBehaviour)

| Файл | Описание |
|------|----------|
| `Assets/_Project/Trade/Exchange/Network/ExchangeServer.cs` | Singleton hub |
| `Assets/_Project/Scripts/Crafting/CraftingServer.cs` | Singleton hub |
| `Assets/_Project/Scripts/Crafting/CraftingClientState.cs` | ClientState |
| `Assets/_Project/Scripts/Crafting/UI/CraftingWindow.cs` | UI (в NetworkPlayer.TryInteractNearestCraftingStation) |

### 3.6 Networking utils

| Файл | Строки | Описание |
|------|--------|----------|
| `Assets/_Project/Trade/Scripts/Network/MarketTimeService.cs` | 153-169 | `NetworkingUtils.IsServerSafe/IsClientSafe` |

### 3.7 MetaRequirement (референс для E-key chain)

| Файл | Описание |
|------|----------|
| `Assets/_Project/Scripts/MetaRequirement/MetaRequirementClientState.cs` | ClientState (референс) |
| `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs` | MonoBehaviour (lock check) |

### 3.8 Scene-placed spawn

| Файл | Описание |
|------|----------|
| `Assets/_Project/Scripts/Network/ScenePlacedObjectSpawner.cs` | Auto-spawn scene-placed NetworkObjects |

### 3.9 Core / UI

| Файл | Описание |
|------|----------|
| `Assets/_Project/Scripts/Core/InteractableManager.cs` | Interactable registry (250+ строк) |
| `Assets/_Project/Scripts/Core/InteractableManager.cs` | 16-21 | Static lists of interactables |
| `Assets/_Project/Scripts/Core/InteractableManager.cs` | 244-298 | FindNearestShip (composite pattern) |
| `Assets/_Project/Scripts/UI/UIFactory.cs` | UI factory |
| `Assets/_Project/Scripts/UI/UIManager.cs` | UI manager |

---

## 4. Skills (Hermes)

| Skill | Когда использовать |
|-------|-------------------|
| `project-c-bootstrap` | Базовый контекст, hard rules |
| `project-c-design-doc-session` | Если делаем design-doc (как сейчас) |
| `project-c-composite-object-architecture` | Для DockStation как композитного объекта |
| `project-c-netcode-patterns` | Для NGO 2.x RPC, scene-placed NetworkObject |
| `unity-mcp-orchestrator` | Когда кодим (расстановка в сценах) |
| `unity-mcp-serializedobject-binding` | Для Editor-time привязки SO в Inspector |
| `project-c-v2-subsystem-migration` | Если рефакторим существующее (не для greenfield) |

---

## 5. Принципы и конвенции (AGENTS.md)

| Принцип | Где |
|---------|-----|
| Namespace `ProjectC.<Subsystem>` | AGENTS.md (project-c-bootstrap) |
| `[SerializeField] private _camelCase` для Inspector | AGENTS.md |
| `public static Instance { get; private set; }` для singleton | AGENTS.md |
| Server-hub + DTO + ClientState pattern | QuestServer, ExchangeServer, CraftingServer |
| `[Rpc(SendTo.Server)]` + `[Rpc(SendTo.SpecifiedInParams)]` | QuestServer.cs:494 |
| `INetworkSerializable` для DTO | `ProjectC.Quests.Dto.DialogStepDto` |
| `ScenePlacedObjectSpawner` для спавна | BootstrapScene |
| `using ProjectC.Player;` в `ProjectC.Ship` файлах | AGENTS.md (project-c-composite-object-architecture) |
| НЕ писать `.meta` файлы | AGENTS.md |
| НЕ писать `.asmdef` файлы спекулятивно | AGENTS.md |
| НЕ `git commit` / `git push` | AGENTS.md |
| НЕ `run_tests` MCP | AGENTS.md |

---

## 6. Pitfall-лист (из реальных багов проекта)

| # | Pitfall | Источник | Митигация в нашем дизайне |
|---|---------|----------|--------------------------|
| 1 | Scene-placed NetworkObject не спавнится | project-c-netcode-patterns §26 | ScenePlacedObjectSpawner handles |
| 2 | Cross-NetworkObject dep race | §24 | Отложенный init через корутину (DockingWorld) |
| 3 | Direct InventoryWorld.AddItem bypass snapshot | §25 | DockingWorld НЕ трогает Inventory (Phase 2+) |
| 4 | Silent IsReady → клиент ждёт | §21 | SendFail DTO с reason (DockingAssignmentDto.failReason) |
| 5 | `[Rpc(SendTo.X)]` deprecated `[ServerRpc]/[ClientRpc]` | AGENTS.md | Используем `[Rpc(SendTo.Server)]` и `[Rpc(SendTo.SpecifiedInParams)]` |
| 6 | ShipRootReference collision (root + child) | composite-architecture | `[DisallowMultipleComponent]` на StationRootReference |
| 7 | Rigidbody mass reset on child add | composite-architecture | DockStation НЕ имеет физического тела (kinematic root) |
| 8 | Root BoxCollider collide with child triggers | composite-architecture | DockStation root НЕ имеет BoxCollider |
| 9 | writing `.meta`/`.asmdef` сломал бы GUID | AGENTS.md | НЕ пишем (Unity создаёт сам) |
| 10 | UIDocument.OnEnable race с нашим OnEnable | DialogWindow.cs:96-100 | Start() backup для EnsureBuilt |

---

## 7. Внешние источники (для контекста)

| Источник | Когда полезен |
|----------|---------------|
| Elite Dangerous — docking computer | UX-паттерн «назначен pad, лети по индикатору» |
| Star Citizen — ATC | UX-паттерн «диспетчер с фразами» |
| EVE Online — station services | UX-паттерн «always-on services в станции» |
| Starfield — docking | UX-паттерн «auto-dock on request» |

(Не цитируются в коде — только для архитектурного inspiration.)

---

## 8. Чеклист «всё процитировано»

- [x] GDD-10 §7, §8, §4.2, §2.2
- [x] GDD-01 §3 (Controls + reserved keys)
- [x] Composite Ship (00_SUMMARY + analysis-composite-ship)
- [x] MarketZone (полный файл) + MarketZoneRegistry
- [x] QuestServer + DialogWindow + QuestClientState
- [x] ExchangeServer + CraftingServer + CraftingClientState
- [x] NetworkingUtils
- [x] NetworkPlayer F-key chain + TryInteractNearestCraftingStation/Door/Npc/MetaRequirement
- [x] ShipController.ShipFlightClass
- [x] InteractableManager.FindNearestShip
- [x] ScenePlacedObjectSpawner
- [x] MetaRequirement pattern (E-key chain)
- [x] 5 skills из Hermes
- [x] AGENTS.md hard rules
- [x] 10 pitfalls из реальных багов
- [x] **T-DEPART-* (Phase 1.5)** — см. `08_DEPARTURE_SUBSYSTEM.md`

---

*Создано: 2026-06-19 | Аналитическая сессия | Без кода.*