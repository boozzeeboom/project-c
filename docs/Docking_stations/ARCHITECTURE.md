# Docking System — Architecture Document
> Проект C: The Clouds | Подсистема стыковочных портов
> Создан: 2026-06-20 | Статус: черновик

## 1. Назначение

Система docking-станций позволяет игроку:
1. Подлететь к станции в радиусе связи
2. Открыть панель связи (CommPanel) по клавише T
3. Запросить посадку у диспетчера
4. Получить назначение на конкретный pad (номер парковки)
5. Приземлиться на pad → стыковка
6. Отстыковаться и покинуть станцию

Фаза 2 (план): автопилот, маршрутизация, multiple станции.

## 2. Иерархия компонентов

```
BootstrapScene
  └── DockingServer (NetworkBehaviour, singleton)
        └── Назначает pads через DockingWorld (server-only)

WorldScene_X_Z
  └── DockStation_Primium (GameObject, root)
        ├── NetworkObject (обязателен для scene-placed)
        ├── DockStationController (NetworkBehaviour)
        │     └── dockStationDefinition: DockStationDefinition (SO) ← **сейчас null**
        ├── OuterCommZone (MonoBehaviour)
        │     └── SphereCollider (trigger, radius=commRange)
        ├── StationRootReference (MonoBehaviour, marker)
        ├── [Mesh/Visual — Phase 2]
        └── Pads group
              ├── DockingPadTriggerBox: PAD-001 (BoxCollider trigger)
              ├── DockingPadTriggerBox: PAD-002
              ├── DockPadVisualMarker (Phase 2)
              └── ...

Runtime (DontDestroyOnLoad)
  └── [DockingClientState] (MonoBehaviour singleton)
        └── events: OnAwaitingConfirmation, OnAssignmentFailed, OnStatusReceived, etc.

UI (per-panel, не DontDestroyOnLoad)
  └── CommPanelWindow (UIDocument, scene-placed)
        └── подписан на DockingClientState events
```

## 3. Data flow

```
[Pilot в зоне связи] → нажать T
  → CommPanelWindow.SetOpen(true)
  → UI показывает "Запросить посадку"
  → нажать [Запросить посадку]
  → DockingServer.RequestDockingRpc (ServerRpc, Everyone)
  → DockingWorld.AssignPad() — ищет свободный pad, совместимый с классом корабля
  → DockingServer.SendDockingAssignmentTargetRpc (TargetRpc)
  → DockingClientState.HandleAssignmentReceived()
  → CommPanelWindow: AwaitingConfirmation state
  → [Хорошо] → RequestConfirmAssignmentRpc(accept=true)
  → DockingWorld.ConfirmAssignment() — занимает pad (SOT)
  → DockingServer.SendDockingStatusTargetRpc(Assigned)
  → Пилот летит к pad'у
  → DockingPadTriggerBox.OnTriggerEnter
  → NotifyTouchedDownRpc (ServerRpc, Everyone)
  → DockingWorld.ConfirmTouchdown() — проверка padId == assigned
  → DockingServer.SendDockingStatusTargetRpc(Docked/WrongPad)
```

## 4. SO Assets (ScriptableObjects)

| SO | Файл | Назначение |
|----|------|-----------|
| `DockStationDefinition` | `DockStationDefinition_Primium.asset` | Паспорт станции: ID, location, pads, altitude |
| `DockPadLayout` | `DockPadLayout_Primium.asset` | Список pads с позициями и совместимостью |
| `DispatcherVoiceLines` | `DispatcherVoiceLines_Default.asset` | Фразы диспетчера по контекстам |

Все три создаются через `DockingAssetCreator.RecreateAll()`.

## 5. Namespaces

| Namespace | Назначение |
|-----------|-----------|
| `ProjectC.Docking.Core` | DockingDefinitions (SOs), DockingWorld (server singleton) |
| `ProjectC.Docking.Network` | DockStationController, DockingServer, DockingZoneRegistry |
| `ProjectC.Docking.Client` | DockingClientState |
| `ProjectC.Docking.Zones` | OuterCommZone |
| `ProjectC.Docking.Stations` | DockingPadTriggerBox, StationRootReference, DockPadVisualMarker, StationComponentLocator, PadTriggerReference |
| `ProjectC.Docking.UI` | CommPanelWindow |
| `ProjectC.Docking.Dto` | DTOs (INetworkSerializable structs) |
| `ProjectC.Docking.EditorTools` | DockingAssetCreator |

## 6. Паттерны проекта (канон)

Система следует проектной архитектуре:

- **V2 Server Hub pattern**: DockingServer — аналог QuestServer, ExchangeServer, CraftingWishServer
- **V2 State singleton pattern**: DockingWorld — аналог QuestWorld, CraftingWishWorld
- **V2 ClientState pattern**: DockingClientState — аналог QuestClientState, MarketClientState
- **V2 UI pattern**: CommPanelWindow — аналог DialogWindow, CharacterWindow
- **V2 Zone pattern**: OuterCommZone — аналог MarketZone
- **V2 Registry pattern**: DockingZoneRegistry — аналог MarketZoneRegistry
- **V2 DTO pattern**: INetworkSerializable structs — аналог QuestResultDto
