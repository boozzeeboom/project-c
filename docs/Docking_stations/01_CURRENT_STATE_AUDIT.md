# 01 — Current State Audit: Docking Stations

> **Цель:** Зафиксировать, что уже существует в проекте (GDD + код), и что
> нужно спроектировать с нуля. Без этой сводки невозможно оценить scope
> MVP и избежать дублирования.

---

## 1. Что уже спроектировано в GDD

### 1.1 GDD-10 §7 «Стыковка и Диспетчер» (дизайн-готов)

**Файл:** `docs/gdd/GDD_10_Ship_System.md:394-432` (v4.2, 2026-06-19).

**Уже описанный поток:**
```
1. Игрок входит в зону города (radius = 500-1500м)
2. Открывает CommPanel → "Запрос стыковки"
3. Запрос на сервер → DockingDispatcher
4. Диспетчер отвечает:
   ├── Pad #5, сектор B
   ├── Подход: высота 4200, курс 270
   ├── Окно посадки: 90 секунд
   └── "Борт [ID], добро пожаловать в Примум"
5. Игрок следует инструкциям → авто-наведение (с MODULE_AUTO_DOCK)
6. Касание платформы → Docked состояние → Engine Off
```

**Уже описанная структура `DispatcherMessage`:**
```csharp
public struct DispatcherMessage {
    public string padId;         // "PAD-PRM-005"
    public Vector3 approachPoint;
    public float approachAltitude;
    public float approachHeading;
    public float landingWindow;  // секунды
    public string voiceLine;     // "Борт 7-Альфа, Примум-Диспетчер..."
}
```

**Статус:** Дизайн-контент есть, реализации нет (Phase 5 плана). Наш каталог
проектирует именно реализацию.

### 1.2 GDD-10 §8 «Машина Состояний Корабля»

**Файл:** `docs/gdd/GDD_10_Ship_System.md:436-449`.

Уже определены состояния, которые мы расширим реализацией:

| Состояние | Описание | Триггер Входа | Триггер Выхода |
|-----------|----------|---------------|----------------|
| **EngineOff** | Все системы неактивны | KeyRod извлечён | KeyRod вставлен |
| **Idle** | Антиграв активен, зависание | KeyRod вставлен, нет ввода | Ввод обнаружен |
| **Flying** | Под управлением пилота | Ввод от пилота | Все пилоты вышли |
| **Docking** | Следует инструкциям диспетчера | Docking accepted | Landed / Cancelled |
| **Docked** | Заблокирован на платформе | Касание pad + скорость=0 | KeyRod извлечён / Engine On |
| **AutoHover** | Зависание (все пилоты вышли) | PilotCount = 0 | PilotCount > 0 |
| ⚠️ VeilTurbulence | Ниже коридора | Alt < minAlt | Alt >= minAlt + 50 |
| ⚠️ SystemDegrade | Выше коридора | Alt > maxAlt + 100 | Alt <= maxAlt |
| ⚠️ SOLLock | СОЛ блокирует | SOL violation timeout | Оплата штрафа / модуль Stealth |

**Наша задача (MVP):** реализовать переходы **Docking ↔ Docked ↔ EngineOff**
с корректным RPC-флоу. Остальные состояния уже работают.

### 1.3 GDD-10 §4.2 (модули)

Упомянуты два модуля, относящихся к стыковке:

| Модуль | ID | Эффект | Совместимость | Тир |
|--------|-----|--------|--------------|-----|
| **Автопилот: Стыковка** | `MODULE_AUTO_DOCK` | Автоподход + посадка по инструкции диспетчера | MD, HV, H2, SV, SS | 2 |
| **Автопилот: Навигация** | `MODULE_AUTO_NAV` | Следование по вейпоинтам | MD, HV, H2, SS | 3 |

**Статус:** `MODULE_AUTO_DOCK` — **out of MVP** (GDD-10 §10.6.3, P3). Мы
проектируем MVP **без автопилота**, но архитектура должна быть готова к
его добавлению (Phase 2) без переделки серверного хаба.

### 1.4 GDD-10 §2.2 (коридоры высот городов)

| Город | Высота города | Min | Max | Коридор |
|-------|--------------|-----|-----|---------|
| **Примум** | 4 348 м | 4 100 | 4 450 | 350 м |
| **Тертиус** | 2 462 м | 2 300 | 2 600 | 300 м |
| **Квартус** | 1 690 м | 1 500 | 1 850 | 350 м |
| **Килиманджаро** | 1 395 м | 1 200 | 1 550 | 350 м |
| **Секунд** | 1 142 м | 1 000 | 1 250 | 250 м |

**Это даёт нам жёсткое требование:** dock-станция должна быть на высоте
города (например, Примум = 4348 м), а pads находятся на платформе
города. OuterCommZone может покрывать весь коридор города.

### 1.5 GDD-01 §3.1-3.2 (управление)

`docs/gdd/GDD_01_Core_Gameplay.md:108-138`. Зарезервированные клавиши:

| Клавиша | Назначение | Этап |
|---------|-----------|------|
| **T** | Открыть инвентарь... нет, чат/коммуникация (нужно уточнить) | ? |
| C | Открыть чат | Этап 4 |
| M | Открыть карту | Этап 4 |
| J | Журнал квестов | Этап 4 |

**T-слот в GDD-01 резервировался «быстрые слоты 1-9», но потом был
переназначен на «открыть инвентарь (полный) Этап 3».** Нам нужен свой
хоткей. **Рекомендация:** для CommPanel используем **T** (как в Elite
Dangerous) — это пересекается с GDD-01, но в §3.2 «Этап 4» ещё не
закреплено. См. `07_OPEN_QUESTIONS.md` Q1.

---

## 2. Что есть в коде (use, don't reinvent)

### 2.1 Composite Ship Architecture — Phase 0-1 ✅

**Файлы:**
- `Assets/_Project/Scripts/Ship/ShipRootReference.cs` — маркер на любой части корабля, кеширует ShipController/Rigidbody/NetworkObject.
- `Assets/_Project/Scripts/Ship/ShipComponentLocator.cs` — статический хелпер для поиска ShipController от произвольной части.
- `Assets/_Project/Scripts/Ship/PilotSeatController.cs` — триггерная зона посадки с `ShipRootReference`.

**Что это даёт:** `DockStation` (композитный объект порта) можно строить
по той же схеме. **Иерархия будущего `DockStation`:**

```
DockStation_Primium (GameObject) — Rigidbody(kinematic) + NetworkObject
├── DockStationController (NetworkBehaviour)
├── StationRootReference (маркер для внешних систем)
├── OuterCommZone (SphereCollider isTrigger, radius=1000м)
├── Pad_001 (GameObject) — child с DockingPadTriggerBox + PadRootReference
├── Pad_002 (GameObject)
└── ...
```

### 2.2 MarketZone + MarketZoneRegistry — референс для OuterCommZone ✅

**Файлы:**
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` (406 строк).
- `Assets/_Project/Trade/Scripts/Network/MarketZoneRegistry.cs` (61 строка).

**Ключевые паттерны, которые используем без изменений:**
1. `SphereCollider isTrigger` для детекции игроков/кораблей.
2. `OnEnable` race-fix: подписка на `NetworkManager.OnServerStarted` /
   `OnClientStarted` (потому что `IsServerSafe()` = false до старта хоста).
3. **Debounced poll:** `PollPlayersInRadius()` + `PollShipsInRadius()` с
   `POLL_INTERVAL = 0.25s` и `MISS_THRESHOLD = 3` (~0.75s). Удаляем игрока
   из `_playersInZone` только после 3 подряд «пустых» тиков.
4. `MarketZoneRegistry` — статический реестр по `locationId`. Регистрация
   идемпотентна (проверка `_zones[locationId] == this`).
5. `LocalPlayerZone` — клиентский singleton (static), обновляется в
   `PollLocalPlayerZone()`. Используется в `NetworkPlayer.TryInteractNearest...`
   для определения «рядом ли рынок».
6. `NetworkingUtils.IsServerSafe()` / `IsClientSafe()` — утилита для
   безопасных проверок.
7. **OnDrawGizmos** — для визуализации в Editor.

**Что нужно добавить:** `PollPlayersInRadius()` сейчас ищет только
**NetworkPlayer**. Нам нужно детектить **корабли** в OuterCommZone (для
авто-уведомления «доступна связь с диспетчером»). `MarketZone` уже это
делает через `shipDockRadius` + `PollShipsInRadius()`. Скопируем паттерн.

### 2.3 QuestServer — каноничный v2 server-hub ✅

**Файл:** `Assets/_Project/Quests/Network/QuestServer.cs` (1584 строк).

**Ключевые паттерны, которые используем:**
1. `[RequireComponent(typeof(NetworkObject))]`, ставится в `BootstrapScene`,
   auto-spawn через `ScenePlacedObjectSpawner`.
2. `OnNetworkSpawn`: `if (Instance == null) Instance = this`; клиенты
   `enabled = false`.
3. **Rate limiting:** `Dictionary<ulong, List<float>> _opTimestamps` +
   `maxOpsPerMinute` (по умолчанию 30). Скопируем для `RequestDockingRpc`.
4. **WorldEventBus.Subscribe / Unsubscribe** для серверных хуков (нам не
   нужен — стыковка не event-driven).
5. **TargetRpcs для клиента:** `[Rpc(SendTo.Owner)]` / `ReceiveXxxTargetRpc`
   на стороне клиента (по `ClientState`).

### 2.4 DialogWindow + DialogClientState — референс UI Toolkit ✅

**Файлы:**
- `Assets/_Project/Quests/UI/DialogWindow.cs` (551 строк).
- `Assets/_Project/Quests/Client/QuestClientState.cs` (singleton).

**Ключевые паттерны, которые используем для CommPanel:**
1. **`Resources.Load<VisualTreeAsset>("UI/<Name>")`** — загрузка UXML из
   Resources с Inspector-fallback.
2. **StyleSheet add вручную:** `_doc.rootVisualElement.styleSheets.Add(uss)`
   — критично для применения USS-классов.
3. **`pickingMode = Ignore` на root** — модальное окно не пробрасывает клики.
4. **Singleton Instance + TrySubscribe/TryUnsubscribe на `OnEnable`/
   `OnDisable`** — отписка обязательна (race при domain reload).
5. **Subscribe в `Start()` как backup** — если `OnEnable` сработал до того,
   как `QuestClientState.Instance` создан (UIDocument.OnEnable может быть
   ПОСЛЕ `DialogWindow.OnEnable`).

**Что мы НЕ берём:** typewriter-эффект, F-skip, click-skip. CommPanel —
оператор-диспетчер, не нарратив. Нужен сразу полный текст + кнопки
действий (принять, отменить, спасибо).

### 2.5 NetworkPlayer F-key chain ✅

**Файл:** `Assets/_Project/Scripts/Player/NetworkPlayer.cs:318, 324, 433-436, 812-837`.

```
F-key pipeline (порядок приоритетов):
  1. TryInteractNearestCraftingStation() (внутри корабля) — новый, добавлено в Phase 5
  2. TryInteractNearestDoor() — добавлено в Phase 3
  3. if _inShip → SubmitSwitchModeRpc() — выход
  4. else → board ship — посадка
```

**Где добавляем наш `TryInteractNearestDockStation`:** в F-key chain **между
Door и boarding**. То есть если рядом есть станция — открываем CommPanel
(даже из корабля). Подробности в `05_FLOW_AND_INTERACTION.md`.

### 2.6 ShipController.ShipFlightClass ✅

**Файл:** `Assets/_Project/Scripts/Player/ShipController.cs:18-24, 41, 48`.

```csharp
public enum ShipFlightClass {
    Light,      // Лёгкий
    Medium,     // Средний
    Heavy,      // Тяжёлый
    HeavyII     // Тяжёлый II
}
```

Готовый источник правды для совместимости `ShipClass → DockingPad`. Каждый
`DockingPad` будет иметь поле `compatibleShipClasses[]` (enum array).
Совместимость проверяется на сервере при назначении.

### 2.7 PickupItem / ChestContainer — референс «объект с триггером + RPC» ✅

**Файлы:** `Assets/_Project/Scripts/Core/PickupItem.cs`, `ChestContainer.cs`.

Хороший шаблон для `DockingPadTriggerBox`:
- Trigger Collider в OnTriggerEnter детектит корабль.
- Серверная логика проверяет conditions.
- RPC на клиент для визуального feedback.

### 2.8 ScenePlacedObjectSpawner — спавн серверных NetworkBehaviour ✅

**Файл:** `Assets/_Project/Scripts/Network/ScenePlacedObjectSpawner.cs` (упоминается в GDD-10 §13).

Сценарии, где будут наши объекты:

| Сцена | Что ставим |
|-------|------------|
| `BootstrapScene.unity` | `[DockingServer]` (NetworkBehaviour singleton) |
| `WorldScene_0_0.unity` | `DockStation_Primium` (1 шт для MVP), `DockStation_TestPlatform` (опц.) |

`DockStation` в WorldScene будет спавниться через `ScenePlacedObjectSpawner`
так же, как `QuestServer` (это уже работает).

---

## 3. Чего нет — нужно спроектировать с нуля

### 3.1 Полный список новых компонентов (MVP)

| Компонент | Файл | Тип | Сложность |
|-----------|------|-----|-----------|
| `DockingServer` | `Assets/_Project/Scripts/Docking/Network/DockingServer.cs` | NetworkBehaviour (singleton, BootstrapScene) | Средне |
| `DockingWorld` | `Assets/_Project/Scripts/Docking/Core/DockingWorld.cs` | MonoBehaviour (singleton, DontDestroyOnLoad) | Средне |
| `DockingClientState` | `Assets/_Project/Scripts/Docking/Client/DockingClientState.cs` | MonoBehaviour (singleton) | Средне |
| `OuterCommZone` | `Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs` | MonoBehaviour (scene-placed) | Просто (копия MarketZone) |
| `DockStationController` | `Assets/_Project/Scripts/Docking/Stations/DockStationController.cs` | NetworkBehaviour (scene-placed) | Средне |
| `DockingPadTriggerBox` | `Assets/_Project/Scripts/Docking/Stations/DockingPadTriggerBox.cs` | MonoBehaviour (scene-placed, child) | Просто |
| `StationRootReference` | `Assets/_Project/Scripts/Docking/Stations/StationRootReference.cs` | MonoBehaviour (marker) | Просто |
| `StationComponentLocator` | `Assets/_Project/Scripts/Docking/Stations/StationComponentLocator.cs` | Static helper | Просто |
| `CommPanelWindow` | `Assets/_Project/Scripts/Docking/UI/CommPanelWindow.cs` | UIDocument | Средне |
| `CommPanelToast` | `Assets/_Project/Scripts/Docking/UI/CommPanelToast.cs` | UIDocument (wrong-pad warning) | Просто |
| `ShipDockingStateBehaviour` | `Assets/_Project/Scripts/Ship/ShipDockingStateBehaviour.cs` | MonoBehaviour (расширение FSM) | Средне |

### 3.2 Полный список новых SO / assets (MVP)

| Ассет | Путь | Тип |
|-------|------|-----|
| `DockStation_Primium.asset` | `Assets/_Project/ScriptableObjects/Docking/DockStation_Primium.asset` | `DockStationDefinition` (SO) |
| `DefaultDockPadLayout.asset` | `Assets/_Project/ScriptableObjects/Docking/DefaultDockPadLayout.asset` | `DockPadLayout` (SO, общая конфигурация) |
| `DefaultDispatcherVoiceLines.asset` | `Assets/_Project/ScriptableObjects/Docking/DefaultDispatcherVoiceLines.asset` | `DispatcherVoiceLines` (SO, фразы) |
| `CommPanelPanelSettings.asset` | `Assets/_Project/UI/Panels/CommPanelPanelSettings.asset` | `PanelSettings` |
| `UI/CommPanel.uxml` | `Assets/_Project/Resources/UI/CommPanel.uxml` | UXML |
| `UI/CommPanel.uss` | `Assets/_Project/Resources/UI/CommPanel.uss` | USS |
| `UI/CommPanelToast.uxml` | `Assets/_Project/Resources/UI/CommPanelToast.uxml` | UXML |
| `UI/CommPanelToast.uss` | `Assets/_Project/Resources/UI/CommPanelToast.uss` | USS |

### 3.3 Полный список модификаций существующих файлов

| Файл | Изменение | Тикет |
|------|-----------|-------|
| `NetworkPlayer.cs` | + `TryInteractNearestDockStation()` (в F-key chain) | T-DOCK-08 |
| `NetworkPlayer.cs` | + `RequestDockRpc` / `ReceiveDockResponse` вызовы | T-DOCK-08 |
| `ShipController.cs` | + FSM states `Docking` / `Docked`, RPCs для EnterDock/ExitDock | T-DOCK-09 |
| `NetworkManagerController.cs` | + `CreateDockingClientState()` в Awake | T-DOCK-04 |
| `BootstrapScene.unity` | + `[DockingServer]` NetworkObject | T-DOCK-02 |
| `WorldScene_0_0.unity` | + `DockStation_Primium` (composite), pads в правильных позициях | T-DOCK-12 |
| `ScenePlacedObjectSpawner.cs` | Возможно нужно зарегистрировать `DockStationController` как спавнящийся объект | T-DOCK-12 |

---

## 4. Анализ рисков (что может пойти не так)

### 4.1 Scene-placed NetworkObject spawn-timing

**Известная проблема** (см. `project-c-netcode-patterns` skill §26):
если `NetworkObject` в сцене и `InScenePlacedSourceGlobalObjectIdHash == 0`,
NGO не спавнит его на `StartHost()`. `ScenePlacedObjectSpawner` в
BootstrapScene находит и спавнит вручную.

**Митигация:** `DockStationController` (NetworkBehaviour на корне
`DockStation_Primium`) auto-attach не нужен — он должен быть scene-placed
для серверного singleton. Если обнаружится та же проблема — добавим
ScenePlacedObjectSpawner.FindAndSpawn<DockStationController>() в
NetworkManagerController (по аналогии с QuestServer).

### 4.2 Cross-NetworkObject dep на TradeWorld / InventoryWorld

**Известная проблема** (см. `project-c-netcode-patterns` skill §24):
серверный хаб может зависеть от TradeWorld/InventoryWorld, которые
инициализируются позже (race condition в BootstrapScene).

**Митигация:** `DockingWorld` инициализируется **отложенно** через
корутину в `DockingServer.OnNetworkSpawn` — повторяем паттерн из
`ExchangeServer` / `CraftingServer`.

### 4.3 NPC-диспетчер (статичный vs динамический)

**Риск:** дизайн-источник правды GDD-10 §7.2 говорит о `voiceLine`, но
нет спецификации, кто её генерирует. Если динамический ИИ (LLM / rule
engine) — это на порядок сложнее.

**Решение:** для MVP — статичный набор фраз из `DispatcherVoiceLines` SO
(8-12 фраз). Phase 2 — может быть реальный ИИ, но это вне scope
этого каталога. См. `07_OPEN_QUESTIONS.md` Q2.

### 4.4 Race с `ShipKeyServer` / `MetaRequirement` (F-boarding)

**Риск:** если игрок в `OuterCommZone` + рядом `PilotSeat` корабля +
нажимает F — что срабатывает первым? CommPanel или boarding?

**Решение:** в F-key chain `TryInteractNearestDockStation` имеет **приоритет
выше** `TryInteractNearestShip` (потому что мы хотим сначала
запросить стыковку). Но boarding с модулем автопилота может быть удобнее
— открытый вопрос. См. `07_OPEN_QUESTIONS.md` Q5 / Q8.

### 4.5 Persistence между перезапусками

**Известное предпочтение** (user profile 2026-06-19): "Ожидает что
persistence работает в Editor Play Mode (cross-restart), не только в
builds". Состояние pads (свободен/занят) — это server-only state.

**Решение для MVP:** **session-only** (забывается при перезапуске).
Причина: pads на сервере инициализируются пустыми, состояние занятости —
runtime. Реальная persistence (чтобы NPC-корабль оставался «припаркованным»
между сессиями) — Phase 3. См. `07_OPEN_QUESTIONS.md` Q3.

### 4.6 Какие классы кораблей совместимы с какими pads

**Источник правды:** `ShipFlightClass` (Light/Medium/Heavy/HeavyII).

**Наивный маппинг:**
- Light pads → только Light
- Medium pads → Light + Medium
- Heavy pads → Heavy + HeavyII (+ Medium?)
- HeavyII pads → только HeavyII

**Рекомендация для MVP:** средний класс совместим со всем **наверх**
(Light + Medium), но не вниз. Это даёт нам 4 комбинации:

| Pad | Совместим с |
|-----|-------------|
| Light only | Light |
| Light + Medium | Light, Medium |
| Medium + Heavy | Medium, Heavy, HeavyII |
| Heavy only | Heavy, HeavyII |

См. `07_OPEN_QUESTIONS.md` Q4 (количество pads и их распределение).

---

## 5. Чеклист «готово к проектированию»

✅ GDD-10 §7 / §8 прочитан и понятен.
✅ Composite Ship pattern понятен.
✅ MarketZone / MarketZoneRegistry pattern понятен.
✅ QuestServer / DialogWindow pattern понятен.
✅ NetworkPlayer F-key chain понятен.
✅ ShipController.ShipFlightClass — источник совместимости.
✅ ScenePlacedObjectSpawner подход понятен.
✅ Race conditions с серверными хабами знакомы.

➡️ **Можно проектировать v2 архитектуру** (см. `02_V2_ARCHITECTURE.md`).

---

## Ссылки

- `docs/gdd/GDD_10_Ship_System.md` (раздел §7, §8, §4.2, §2.2)
- `docs/gdd/GDD_01_Core_Gameplay.md` (раздел §3.1-3.2, зарезервированные клавиши)
- `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` (composite ship architecture)
- `docs/Ships/analysis-composite-ship.md` (полный анализ композитного корабля)
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` (референс zone-trigger)
- `Assets/_Project/Trade/Scripts/Network/MarketZoneRegistry.cs`
- `Assets/_Project/Quests/Network/QuestServer.cs` (референс server-hub)
- `Assets/_Project/Quests/UI/DialogWindow.cs` (референс UI Toolkit window)
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (F-key chain)
- `Assets/_Project/Scripts/Player/ShipController.cs` (FSM, ShipFlightClass)
- `Assets/_Project/Scripts/Core/InteractableManager.cs` (interactable registry)
- `Assets/_Project/Trade/Scripts/Network/MarketTimeService.cs` (NetworkingUtils)

*Создано: 2026-06-19 | Аналитическая сессия | Без кода.*