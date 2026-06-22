# 06 — Roadmap: Docking Stations

> **Статус (2026-06-20):** ✅ **MVP реализован.** Все тикеты `T-DOCK-00..13` выполнены, compile = 0 errors.
> Известное ограничение: `DockPadVisualMarker` — не меняет цвет при посадке/отстыковке (требует переработки, тикет `T-DOCK-14`).
> **Цель:** По-тикетный план реализации MVP стыковочных портов. Каждый
> тикет — отдельная PR/commit (по правилу AGENTS.md: «1 ticket = 1 PR =
> 1 session = 30-150 min coding»). Фазы сгруппированы по логическим
> блокам, в одной сессии можно взять 1-2 смежных тикета.

---

## 1. Milestones (M-DOCK-1 .. M-DOCK-5) — ✅ все завершены

**Q1 (T-key), Q3 (SOT), Q4 (без хардкода), Q7 (двусторонняя), Q8 (Departure
= отдельная подсистема), Q10 (T вне кресла), Q11 (KeyRod не обрабатываем),
Q13 (цифры на mesh'е), Q15 (bool-флаги) — все приняты 2026-06-19, реализованы 2026-06-20.**

### Docking (T-DOCK-*) — ✅ MVP complete

| Milestone | Название | Что внутри | Тикетов | Статус |
|-----------|----------|------------|---------|--------|
| **M-DOCK-1** | Server hub skeleton | DockingWorld + DTOs + Server hub + RPCs (включая Q7 `RequestConfirmAssignmentRpc`) + ClientState + реестр | T-DOCK-00..03 (4) | ✅ |
| **M-DOCK-2** | Zones & scene objects | OuterCommZone + DockStationController + composite + SOs (Q4 без хардкода) | T-DOCK-04..06 (3) | ✅ |
| **M-DOCK-3** | UI: CommPanel + Toast | UXML/USS + Window (Q7 AwaitingConfirmation) + Toast + T-key (Q10 check) | T-DOCK-07..08 (2) | ✅ |
| **M-DOCK-4** | FSM + integration | ShipController.IsDocked (Q15 bool) + F-key (Q9 boarding always) | T-DOCK-09..10 (2) | ✅ |
| **M-DOCK-5** | Test scene + smoke | SOs (Q4) + scene placement (Q6 40500,2510,40500) + тест-сценарий | T-DOCK-11..13 (3) | ✅ |
| **M-DOCK-6** | Post-MVP фиксы (UI, RPC null-safety, авто-отстыковка) | `AUDIT_AND_REFACTOR.md`, `CHANGELOG.md` | — | ✅ |

### Bonus (не из roadmap, добавлено в ходе разработки)

- ✅ **T-DOCK-HUD** — подключение Dispatch column (К5) к docking system в `ShipHudController`
- ✅ **T-DOCK-SRV-5** — `compatibleShipClasses` override на `DockingPadTriggerBox` (плюс к SO)
- ✅ **T-DOCK-SRV-6** — `ScanExistingOccupants` при старте сервера (корабли на падах)
- ✅ **T-DOCK-SRV-7** — `IsShipInside` на trigger-box для маркера + UI Docked-state detection

### Departure (T-DEPART-*) — **отдельная подсистема (Q8)** — ⏳ Phase 1.5

| Milestone | Название | Что внутри | Тикетов | Статус |
|-----------|----------|------------|---------|--------|
| **M-DEPART-1** | DepartureServer | Server hub + RPCs (RequestDeparturePermission) | T-DEPART-00..02 (3) | ⏳ |
| **M-DEPART-2** | Departure UI + violation toast | UI + violation warning | T-DEPART-03..05 (3) | ⏳ |

**Всего:** 14 docking + 6 departure = **20 тикетов**, ~35-50 часов.

**Departure — Phase 1.5** (после docking MVP). Не блокирует T-DOCK-*.

---

## 2. Зависимости тикетов (граф)

```
T-DOCK-00 (DockingWorld — server state)
  ↓
T-DOCK-01 (DockingServer hub + RPCs)
  ↓
T-DOCK-02 (BootstrapScene placement)
  ↓
T-DOCK-03 (DockingClientState singleton + NetworkManagerController)
  ↓
T-DOCK-04 (StationRootReference + Locator + DockStationDefinition SO)
  ↓
T-DOCK-05 (OuterCommZone)
  ↓
T-DOCK-06 (DockStationController + DockingPadTriggerBox)
  ↓
T-DOCK-07 (CommPanelWindow + UXML/USS)
  ↓
T-DOCK-08 (T-key + F-key integration + NetworkPlayer changes)
  ↓
T-DOCK-09 (ShipController.IsDocked FSM + EnterDocked/ExitDocked)
  ↓
T-DOCK-10 (CommPanelToast + Wrong-pad warning)
  ↓
T-DOCK-11 (DockPadLayout SO + DefaultDockPadLayout.asset)
  ↓
T-DOCK-12 (DockStation_Primium + расстановка в WorldScene_0_0)
  ↓
T-DOCK-13 (Smoke test scenario + документация для QA)
```

---

## 3. Детальные тикеты

### T-DOCK-00: `DockingWorld` — server state singleton
**Milestone:** M-DOCK-1
**Оценка:** ~60 мин (150 LOC, ~3 файла)

**Что делаем:**
- `Assets/_Project/Scripts/Docking/Core/DockingWorld.cs` (singleton MonoBehaviour, DontDestroyOnLoad).
- `Assets/_Project/Scripts/Docking/Dto/DockStationInfoDto.cs` (struct INetworkSerializable).
- `Assets/_Project/Scripts/Docking/Dto/DockPadInfoDto.cs`.
- `Assets/_Project/Scripts/Docking/Dto/DockingAssignmentDto.cs`.
- `Assets/_Project/Scripts/Docking/Dto/DockingStatusDto.cs`.
- `DockingWorld.CreateAndInitialize()` / `Shutdown()` API.
- `AssignPad()`, `RegisterAssignment()`, `ReleaseAssignment()`, `ConfirmTouchdown()`.
- Update loop: expiration check.

**Acceptance:**
- Compile: 0 errors.
- После `StartHost`: `DockingWorld.Instance != null`.
- Console: `[DockingWorld] Created`.

**Открытые вопросы для этого тикета:** Q3 (persistence), Q4 (количество pads).

---

### T-DOCK-01: `DockingServer` hub + RPCs
**Milestone:** M-DOCK-1
**Оценка:** ~120 мин (300 LOC, 3 файла)
**Включает Q7:** `RequestConfirmAssignmentRpc` для двусторонней связи.

**Зависит от:** T-DOCK-00.

**Что делаем:**
- `Assets/_Project/Scripts/Docking/Network/DockingServer.cs` (NetworkBehaviour singleton).
- Rate limiting (copy-paste из QuestServer).
- `RequestDockingRpc` / `RequestTakeoffRpc` / `NotifyTouchedDownRpc` (server-side методы, `[Rpc(SendTo.Server)]`).
- **`RequestConfirmAssignmentRpc(shipNetId, accept)`** — Q7: подтверждение/отбой после назначения.
- `SendDockingAssignmentTargetRpc` / `SendDockingStatusTargetRpc` / `SendTakeoffApprovedTargetRpc` (`[Rpc(SendTo.SpecifiedInParams)]`).
- `OnNetworkSpawn`: `DockingWorld.CreateAndInitialize()`.
- `OnNetworkDespawn`: `DockingWorld.Shutdown()`.
- `OnClientDisconnected`: `DockingWorld.ReleaseAssignment(clientId, 0)` + pending cleanup.

**Acceptance:**
- Compile: 0 errors.
- После `StartHost`: `[DockingServer] OnNetworkSpawn — IsServer=true`.

**Открытые вопросы:** Q1 (T-key ✓), Q8 (F-key не меняется ✓).

---

### T-DOCK-02: `DockingServer` placement в BootstrapScene
**Milestone:** M-DOCK-1
**Оценка:** ~30 мин (1 сцена + MCP-команды)

**Зависит от:** T-DOCK-01.

**Что делаем:**
- В `Assets/_Project/Scenes/BootstrapScene.unity` добавить GameObject `[DockingServer]` с NetworkObject + DockingServer.
- Через MCP: `mcp__unityMCP__create_gameobject` + `add_component` + `manage_scene save_scene`.

**Acceptance:**
- В Hierarchy BootstrapScene: `[DockingServer]` (DontDestroyOnLoad).
- После запуска и StartHost — сервер спавнится.
- ScenePlacedObjectSpawner находит и спавнит.

---

### T-DOCK-03: `DockingClientState` singleton
**Milestone:** M-DOCK-1
**Оценка:** ~45 мин (60 LOC, 2 файла)

**Зависит от:** T-DOCK-02.

**Что делаем:**
- `Assets/_Project/Scripts/Docking/Client/DockingClientState.cs` (singleton MonoBehaviour, DontDestroyOnLoad).
- `HandleAssignmentReceived` / `HandleStatusReceived` / `HandleTakeoffApproved`.
- `RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)` auto-create.
- `NetworkManagerController.Awake`: добавить `CreateDockingClientState()` (по канону `CreateQuestClientState`).

**Acceptance:**
- Compile: 0 errors.
- После StartHost: `DockingClientState.Instance != null` на клиенте.

---

### T-DOCK-04: `StationRootReference` + `StationComponentLocator` + `DockStationDefinition` SO
**Milestone:** M-DOCK-2
**Оценка:** ~45 мин (90 LOC, 3 файла)

**Что делаем:**
- `Assets/_Project/Scripts/Docking/Stations/StationRootReference.cs` (marker MonoBehaviour).
- `Assets/_Project/Scripts/Docking/Stations/StationComponentLocator.cs` (static helper).
- `Assets/_Project/Scripts/Docking/Core/DockStationDefinition.cs` (ScriptableObject).
- `Assets/_Project/Scripts/Docking/Core/DockPadLayout.cs` (ScriptableObject).
- `Assets/_Project/Scripts/Docking/Core/DispatcherVoiceLines.cs` (ScriptableObject).

**Acceptance:**
- Compile: 0 errors.
- `[CreateAssetMenu]` в инспекторе работает (можно создать SO).

---

### T-DOCK-05: `OuterCommZone`
**Milestone:** M-DOCK-2
**Оценка:** ~90 мин (200 LOC, 1 файл + assets)

**Зависит от:** T-DOCK-04.

**Что делаем:**
- `Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs` (MonoBehaviour, scene-placed).
- Копия паттерна `MarketZone`: SphereCollider + debounced poll + OnEnable race-fix + IsServerSafe.
- `PollLocalPlayerZone`: обновляет `DockingZoneRegistry.LocalPlayerStation` / `LocalPlayerShipStation`.
- `DockingZoneRegistry` (static class).

**Acceptance:**
- Compile: 0 errors.
- В Play Mode: подход к OuterCommZone → log `entered zone`.

---

### T-DOCK-06: `DockStationController` + `DockingPadTriggerBox`
**Milestone:** M-DOCK-2
**Оценка:** ~75 мин (180 LOC, 3 файла)

**Зависит от:** T-DOCK-04.

**Что делаем:**
- `Assets/_Project/Scripts/Docking/Network/DockStationController.cs` (NetworkBehaviour scene-placed).
- `Assets/_Project/Scripts/Docking/Stations/DockingPadTriggerBox.cs` (MonoBehaviour scene-placed, child).
- `Assets/_Project/Scripts/Docking/Stations/PadTriggerReference.cs` (marker).
- `DockStationController.OnNetworkSpawn` лог.
- `DockingPadTriggerBox.OnTriggerEnter` отправляет `NotifyTouchedDownRpc` через `NetworkPlayer.FindOwnerOfShip`.

**Acceptance:**
- Compile: 0 errors.
- В Play Mode: касание pad'а → log `OnTriggerEnter`.

---

### T-DOCK-07: `CommPanelWindow` + UXML/USS + PanelSettings
**Milestone:** M-DOCK-3
**Оценка:** ~120 мин (300 LOC, 5 файлов)

**Зависит от:** T-DOCK-03.

**Что делаем:**
- `Assets/_Project/Resources/UI/CommPanel.uxml` (по шаблону из `04_DIALOG_AND_DISPATCHER_UI.md` §3).
- `Assets/_Project/Resources/UI/CommPanel.uss` (по §4).
- `Assets/_Project/UI/Panels/CommPanelPanelSettings.asset` (PanelSettings с themeUss).
- `Assets/_Project/Scripts/Docking/UI/CommPanelWindow.cs` (UIDocument, ~250 LOC).
- `NetworkManagerController.Awake`: добавить `CreateCommPanelUI()` (CommPanelWindow + PanelSettings).

**Acceptance:**
- Compile: 0 errors.
- В Play Mode: после StartHost → `[CommPanelWindow]` GameObject создан.
- T (без станции) → silent. T в зоне → панель открывается.

---

### T-DOCK-08: `PlayerInputReader` T-key + `NetworkPlayer` integration
**Milestone:** M-DOCK-3
**Оценка:** ~75 мин (60 LOC + изменения в 2 файлах)
**Включает Q10:** T игнорируется вне кресла пилота.

**Зависит от:** T-DOCK-07.

**Что делаем:**
- `Assets/_Project/Scripts/Player/PlayerInputReader.cs`: добавить `OnCommPanelPressed` event + InputAction binding (T).
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`: добавить `OnCommPanelPressed()` handler.
  - **Q10:** проверка `DockingClientState.IsLocalPlayerPilotingShip()` — если false, ignore.
  - `DockingZoneRegistry.LocalPlayerStation` / `LocalPlayerShipStation` — если null, ignore.
  - `CommPanelWindow.Instance.ToggleOpen()`.
- `NetworkPlayer.F-key handler` (Q9): добавить `if (CommPanelWindow.Instance.IsOpen) { CommPanelWindow.SetOpen(false); }` в начало.
- `NetworkPlayer.EscPressed` handler: закрыть CommPanel если открыт.

**Acceptance:**
- Compile: 0 errors.
- В Play Mode: T → открывает/закрывает CommPanel в OuterCommZone (только если в корабле).
- T вне кресла → silently ignore.
- Esc → закрывает CommPanel.

---

### T-DOCK-09: `ShipController.IsDocked` + `EnterDocked` / `ExitDocked`
**Milestone:** M-DOCK-4
**Оценка:** ~60 мин (90 LOC, 1 файл)
**Q15 (принято):** bool-флаги, не enum.
**Q11 (принято):** НЕ блокируем KeyRod-извлечение (F = выход из кресла).

**Зависит от:** T-DOCK-06.

**Что делаем:**
- `Assets/_Project/Scripts/Player/ShipController.cs`:
  - Добавить `_isDockingAssigned`, `_isDocked`, `_assignedPadId`, `_assignedStationId` (Q15: bool).
  - `EnterDockingAssigned(stationId, padId)` — подавить thrust/lift, разрешить yaw/pitch.
  - `EnterDocked()` — `rb.isKinematic = true`, обнулить velocity.
  - `ExitDocked()` — `rb.isKinematic = false`.
  - В `FixedUpdate`: если `_isDockingAssigned` → игнорировать thrust/lift input.
  - В `F_handler` boarding-логики: если `IsDocked` → `RequestTakeoffRpc` вместо boarding (Q9: стандартное F поведение).

**Acceptance:**
- Compile: 0 errors.
- В Play Mode: после Docked корабль не реагирует на W/A/S/D.
- F → отстыковка → корабль снова двигается.

**Q11 (не делаем):** НЕ блокируем KeyRod-извлечение во время Docked. F = выход из кресла = «выключает» корабль, имитируя KeyRod. Полная блокировка → Phase 2 тикет `T-DOCK-14`.

---

### T-DOCK-10: `CommPanelToast` (wrong-pad warning)
**Milestone:** M-DOCK-4
**Оценка:** ~45 мин (80 LOC, 3 файла)

**Зависит от:** T-DOCK-07.

**Что делаем:**
- `Assets/_Project/Resources/UI/CommPanelToast.uxml`.
- `Assets/_Project/Resources/UI/CommPanelToast.uss`.
- `Assets/_Project/Scripts/Docking/UI/CommPanelToast.cs` (UIDocument, fade-out 4 сек).
- В `CommPanelWindow.HandleStatusReceived`: если `WrongPad` → `toast.ShowWrongPadWarning`.
- В `NetworkManagerController.CreateCommPanelUI`: создать `[CommPanelToast]`.

**Acceptance:**
- Compile: 0 errors.
- В Play Mode: после wrong pad → toast появляется в правом нижнем углу, fade-out через 4 сек.

---

### T-DOCK-11: `DefaultDockPadLayout.asset` + `DefaultDispatcherVoiceLines.asset` + `DockStation_Primium.asset`
**Milestone:** M-DOCK-5
**Оценка:** ~30 мин (3 SO файла в Inspector)
**Q4 (принято):** **НЕ создаём pads по умолчанию** — дизайнер сам наполняет.

**Зависит от:** T-DOCK-04.

**Что делаем:**
- Создать `Assets/_Project/ScriptableObjects/Docking/DefaultDockPadLayout.asset`:
  - **Q4: пустой список pads.** Дизайнер добавляет столько, сколько нужно.
  - `defaultTriggerBoxSize = 8×3×8`.
- Создать `Assets/_Project/ScriptableObjects/Docking/DefaultDispatcherVoiceLines.asset`:
  - 10 контекстов: Greeting, Assigning, AssignedLight/Medium/Heavy, WindowExpired, Touchdown, Takeoff, Goodbye, Occupied, WrongPad, **AwaitingConfirmation** (Q7).
  - По 2-4 фразы на контекст.
- Создать `Assets/_Project/ScriptableObjects/Docking/DockStation_Primium.asset`:
  - stationId = `STN-PRM-001`, locationId = `PRIMIUM`, displayName = "Примум".
  - padLayout → DefaultDockPadLayout, voiceLines → DefaultDispatcherVoiceLines.
  - platformAltitude = 4348 (из GDD-10 §2.2).

**Acceptance:**
- Все 3 SO созданы, поля заполнены.
- В инспекторе `DockStationController` → `dockStationDefinition` = `DockStation_Primium`.
- `DefaultDockPadLayout` имеет **0 pads** — дизайнер наполняет в T-DOCK-12.

---

### T-DOCK-12: Расстановка `DockStation_Primium` в `WorldScene_0_0.unity`
**Milestone:** M-DOCK-5
**Оценка:** ~60 мин (MCP-команды)
**Q6 (принято):** координаты (40500, 2510, 40500).
**Q4 (принято):** дизайнер расставляет pads, не код.
**Q13 (принято):** цифры на mesh'е (ProBuilder/Blender).

**Зависит от:** T-DOCK-06, T-DOCK-11.

**Что делаем:**
- В `Assets/_Project/Scenes/WorldScene_0_0.unity` создать GameObject `DockStation_Primium` (root).
- **Q6:** координаты (40500, 2510, 40500) — 500м к NE от Chest_Main.
- Добавить компоненты: NetworkObject, DockStationController (dockStationDefinition = `DockStation_Primium.asset`), Rigidbody (kinematic), StationRootReference, OuterCommZone.
- **Q4:** создать N child GameObject'ов `Pad_001` .. `Pad_N` (N = сколько нужно):
  - Каждый с BoxCollider (isTrigger) и DockingPadTriggerBox (padId уникален).
  - PadTriggerReference.
  - Локальные позиции: дизайнер расставляет сам (примеры в `03_ZONES_AND_TRIGGERS.md` §8.1).
- **Q13:** на каждом pad'е нарисовать цифру на mesh'е (текстура или ProBuilder-цифра).
- Добавить pads в `DefaultDockPadLayout` SO: для каждого pad'а указать `padId`, `localPosition`, `localEulerAngles`, `compatibleShipClasses`.
- Сохранить сцену.

**Acceptance:**
- Compile: 0 errors.
- В Play Mode: при загрузке WorldScene_0_0 → `DockStation_Primium` существует, pads зарегистрированы.
- Лог: `[OuterCommZone:STN-PRM-001] OnEnable`.
- Дизайнер наполнил `DefaultDockPadLayout` (например, 6 pads как в примере `03 §8.1`).

---

### T-DOCK-13: Smoke test scenario + документация для QA
**Milestone:** M-DOCK-5
**Оценка:** ~30 мин (документация)

**Зависит от:** T-DOCK-12.

**Что делаем:**
- Создать `docs/Docking_stations/TESTING_GUIDE.md` со сценариями:
  - Smoke test (по `05_FLOW_AND_INTERACTION.md` §6.1).
  - Wrong-pad test.
  - Window expiry test.
  - No-station test.
- Чеклист expected console logs.
- Список edge-cases для ручной проверки.

**Acceptance:**
- Документ существует, покрывает все сценарии из `05_FLOW_AND_INTERACTION.md` §6.

---

## 4. Оценка усилий (LOC / часы)

### Docking (T-DOCK-*)

| Тикет | LOC | Часов |
|-------|-----|-------|
| T-DOCK-00 | 300 | 4.5 |
| T-DOCK-01 (+ Q7) | 350 | 5 |
| T-DOCK-02 | 30 (MCP) | 1 |
| T-DOCK-03 | 90 | 1.5 |
| T-DOCK-04 | 120 | 2.5 |
| T-DOCK-05 | 200 | 3 |
| T-DOCK-06 | 180 | 3 |
| T-DOCK-07 (+ Q7) | 350 | 5 |
| T-DOCK-08 (+ Q10) | 80 | 1.5 |
| T-DOCK-09 (+ Q15, Q11) | 100 | 1.5 |
| T-DOCK-10 | 90 | 2 |
| T-DOCK-11 (Q4: пустой layout) | 30 (Inspector) | 1 |
| T-DOCK-12 (Q4+Q6+Q13) | 30 (MCP) | 2.5 |
| T-DOCK-13 | 200 (doc) | 2 |
| **Итого Docking** | **~2150 LOC** | **~36 часов** |

### Departure (T-DEPART-*) — Phase 1.5

| Тикет | LOC | Часов |
|-------|-----|-------|
| T-DEPART-00 | 200 | 3 |
| T-DEPART-01 | 250 | 4 |
| T-DEPART-02 | 30 | 1 |
| T-DEPART-03 | 200 | 3 |
| T-DEPART-04 | 150 | 2.5 |
| T-DEPART-05 | 100 (doc) | 1.5 |
| **Итого Departure** | **~930 LOC** | **~15 часов** |

### Общая оценка

| Категория | LOC | Часов |
|-----------|-----|-------|
| Docking | ~2150 | ~36 |
| Departure | ~930 | ~15 |
| **Итого** | **~3080 LOC** | **~51 час** |

**Реалистичная оценка с учётом отладки и verify rounds:** **~70-90 часов**.

---

## 5. Сессии (как разбить)

### Docking MVP (8 сессий)

| Сессия | Тикеты | Тема |
|--------|--------|------|
| **1** | T-DOCK-00 | DockingWorld + DTOs (фундамент) |
| **2** | T-DOCK-01, T-DOCK-02 | DockingServer hub (Q7) + placement |
| **3** | T-DOCK-03, T-DOCK-04 | ClientState + markers + SOs |
| **4** | T-DOCK-05, T-DOCK-06 | OuterCommZone + DockStationController + PadTriggerBox |
| **5** | T-DOCK-07, T-DOCK-08 | CommPanel UI (Q7) + T-key (Q10) + F-key (Q9) |
| **6** | T-DOCK-09, T-DOCK-10 | ShipController FSM (Q15) + CommPanelToast |
| **7** | T-DOCK-11, T-DOCK-12 | SO assets (Q4) + scene placement (Q4+Q6+Q13) |
| **8** | T-DOCK-13 | Testing guide + verify |

### Departure (Phase 1.5, 3-4 сессии)

| Сессия | Тикеты | Тема |
|--------|--------|------|
| **9** | T-DEPART-00, T-DEPART-01 | DepartureServer + RPCs |
| **10** | T-DEPART-02, T-DEPART-03 | DepartureClientState + UI |
| **11** | T-DEPART-04, T-DEPART-05 | Violation toast + testing |

**11 сессий × 60-90 мин** = 11-16 часов чистого coding + verify rounds.

---

## 6. Phase 2 hooks (не входит в MVP, но планируется)

### 6.1 Автопилот стыковки

**Источник:** GDD-10 §7.1 шаг 5 «авто-наведение (с MODULE_AUTO_DOCK)».

**Что нужно в Phase 2:**
- `MODULE_AUTO_DOCK` — новый ShipModule в `Assets/_Project/ScriptableObjects/Modules/`.
- `ShipController.ApproachToPad(assignment)` — lerp позиции/ротации к `assignment.approachPoint`.
- Проверка `ShipModuleManager.HasModule<ModuleAutoDock>(ship)` перед активацией.

**Что уже готово в MVP:**
- `DockingAssignmentDto.approachPoint / approachAltitude / approachHeading` — данные для автопилота.
- `DockingWorld.AssignPad()` — возвращает эти данные.
- `ShipController.IsDockingAssigned` — флаг готовности.

**Phase 2 тикеты (не сейчас):**
- `T-DOCK-AUTO-01..05` — автопилот модуль + поведение.

### 6.2 NPC-диспетчер с ИИ

**Источник:** GDD-10 §7.2 `voiceLine`.

**Phase 2:**
- Заменить `DispatcherVoiceLines` (статичный) на `IDispatcherStrategy` interface.
- Реализации: `StaticDispatcherStrategy` (как сейчас), `RuleBasedDispatcherStrategy`, `LLMDispatcherStrategy` (Phase 5+).
- Без изменений в `DockingServer` / `DockingWorld` / `DockingClientState`.

### 6.3 Traffic controller

**Phase 2:**
- `DockingWorld._pendingRequests` queue.
- Timeout-based dispatcher (если все pads заняты, добавить в очередь).
- Уведомление «pad освободился, вы следующий» через RPC.

### 6.4 Persistence между сессиями

**Phase 3:**
- `JsonDockOccupancyRepository : IDockOccupancyRepository` (по канону `JsonKeyRodInstanceRepository`).
- `DockStation_Primium.asset.dockState` — какие pads заняты NPC-кораблями (например, persistent NPC dock).
- Сохранение в `DockOccupancy.json`.

### 6.5 Sky-карта (для реальной высоты 4348м)

**Phase 5+:**
- Новая сцена `WorldScene_0_4348.unity` или streaming-переход на высоту.
- DockStation_Primium переносится туда.

### 6.6 Reputation + docking fee

**Phase 3:**
- `ReputationClientState.OnDocked(stationId)` event → +X reputation.
- `InventoryWorld.RemoveCredits(amount)` при стыковке (если есть docking fee).

---

## 7. Риски roadmap'а

### 7.1 Scope creep

**Риск:** добавление автопилота / traffic controller / persistence в MVP.
**Митигация:** жёсткое следование MVP-скоупу из `00_README.md`. Phase 2+
тикеты явно отложены.

### 7.2 Технический долг от FSM-через-bool

**Риск:** `ShipController.IsDocked = true/false` — быстрое MVP-решение,
но при росте состояний (Docking/Docked/AutoHover/Turbulence/SOLLock)
становится неуправляемым.

**Митигация:** Phase 3 — рефакторинг в `enum ShipState` + `ShipStateBehaviour`
(по канону `PlayerStateMachine`). Но это **отдельный** тикет `T-SHIP-FSM-01`,
не входит в scope docking.

### 7.3 Race condition с ScenePlacedObjectSpawner

**Риск:** `DockStationController` в WorldScene_0_0 не спавнится на
`StartHost()` (см. `project-c-netcode-patterns` §26).

**Митигация:** T-DOCK-12 включает ручную проверку. Если проблема
повторится — добавим в `ScenePlacedObjectSpawner.FindAndSpawn<DockStationController>()`.

### 7.4 Composite DockStation + IsTrigger

**Риск:** корневой BoxCollider `DockStation` (если добавим для физики)
может перехватывать `OnTriggerEnter` раньше `DockingPadTriggerBox`.

**Митигация:** корневой collider **НЕ** ставим — `DockStation` не имеет
физического тела. OuterCommZone (sphere) — единственный collider на root.
Pads — на child с собственными BoxCollider'ами.

---

## 8. Что нужно от пользователя перед стартом T-DOCK-00

Прежде чем начать кодить, нужно закрыть открытые вопросы в
`07_OPEN_QUESTIONS.md`:

**Критичные (блокируют T-DOCK-00):**
- Q1 — какая клавиша для CommPanel (T? или другая?)
- Q3 — persistence pads (session-only или JSON?)
- Q4 — количество pads и их распределение

**Желательные (можно принять рекомендацию и идти дальше):**
- Q2 (фразы диспетчера), Q5 (wrong-pad предупреждение), Q6 (размещение)
- Q8 (F-key приоритет DockStation vs boarding)

**Некритичные (можно решить в процессе):**
- Q7 (двусторонняя связь), Q9 (F внутри CommPanel), Q10-Q15

См. `07_OPEN_QUESTIONS.md` для деталей.

---

## 9. Связь с другими документами

| Документ | Что используем |
|----------|----------------|
| `00_README.md` | обзор каталога |
| `01_CURRENT_STATE_AUDIT.md` | что уже есть в проекте |
| `02_V2_ARCHITECTURE.md` | целевая архитектура |
| `03_ZONES_AND_TRIGGERS.md` | иерархия объектов |
| `04_DIALOG_AND_DISPATCHER_UI.md` | UI Toolkit |
| `05_FLOW_AND_INTERACTION.md` | полный flow + edge-cases |
| `07_OPEN_QUESTIONS.md` | что нужно решить перед кодом |
| `08_REFERENCES.md` | все file:line ссылки |

---

*Создано: 2026-06-19 | Аналитическая сессия | Без кода.*