# Trade System V2 — Integration Handbook

**Проект:** Project C: The Clouds
**Дата:** 2026-06-02
**Статус:** 🟡 Сборка системы завершена, требуется настройка сцен
**Автор:** Mavis

> Практическое руководство по подключению V2 к существующему bootstrap-проекту.
> Дизайн и обоснования: [TRADE_V2_DESIGN.md](TRADE_V2_DESIGN.md).
> GDD: [GDD_22_Economy_Trading.md](../gdd/GDD_22_Economy_Trading.md).

---

## 0. Что собрано

```
Assets/_Project/Trade/
├── Config/                                  ← SO (read-only)
│   ├── MarketConfig.cs                      — ScriptableObject рынка
│   └── MarketItemConfig.cs                  — serializable товар рынка
├── Core/                                    ← server-only POCO
│   ├── MarketState.cs                       — runtime состояние рынка
│   ├── MarketItemState.cs                   — runtime состояние позиции
│   ├── Warehouse.cs                         — склад игрока на локации
│   ├── CargoData.cs                         — груз корабля
│   ├── TradeItemDefinitionResolver.cs       — интерфейс
│   ├── DatabaseResolver.cs                  — реализация поверх TradeDatabase
│   ├── TradeWorld.cs                        — главный серверный синглтон
│   ├── TradeResult.cs                       — результат операции (НЕ DTO)
│   ├── NPCTrader.cs                         — портировано из v1, адаптировано
│   └── MarketEvent.cs                       — портировано, time-based
├── Dto/                                     ← INetworkSerializable
│   ├── TradeResultCode.cs
│   ├── MarketSnapshotDto.cs
│   ├── ShipSummaryDto.cs
│   └── TradeResultDto.cs
├── Repository/                              ← IPlayerDataRepository
│   ├── IPlayerDataRepository.cs
│   ├── PlayerPrefsRepository.cs             — default, host-friendly
│   └── ServerFileRepository.cs              — P1 stub, dedicated server
├── Service/
│   └── PriceFormula.cs                      — static helpers, formula + decay
├── Network/                                 ← MonoBehaviour, server-side
│   ├── MarketServer.cs                      — NetworkBehaviour, RPC приёмник
│   ├── MarketZone.cs                        — scene-placed
│   ├── MarketZoneRegistry.cs                — статический реестр
│   └── MarketTimeService.cs                 — server-only tick + multiplier
├── Client/                                  ← client-side projection
│   ├── MarketClientState.cs                 — singleton, держит snapshot
│   ├── MarketInteractor.cs                  — helper для NetworkPlayer
│   └── MarketWindow.cs                      — UI Toolkit контроллер
├── Resources/UI/
│   ├── MarketWindow.uxml                    ← ПРАВИТЬ ЗДЕСЬ (вместо кода)
│   └── MarketWindow.uss                     ← стили, тема
└── ... старые файлы остаются на месте до шага 8 (cleanup)
```

**Что НЕ менялось:**
- `NetworkPlayer.cs` — добавлены 3 новых метода (RPC targets + RequestSetMarketTimeMultiplier)
- `Assets/_Project/Player/NetworkPlayer.cs` — добавлена интеграция с `MarketInteractor` в E-обработчике

**Что НЕ создано автоматически** (нужны ручные шаги ниже):
- ScriptableObject assets (MarketConfig × 4, TradeDatabase)
- Prefab MarketZone
- GameObject'ы в сценах (MarketServer, MarketTimeService, MarketClientState, MarketWindow)
- Демо-настройка в WorldScene_0_0 (1 MarketZone + 2 корабля)
- PanelSettings asset для UI Toolkit

---

## 1. Создать PanelSettings (один раз)

UI Toolkit требует PanelSettings asset для рендера. Без него `UIDocument` не покажет ничего.

**В Editor:**
1. `Project` window → правый клик на `Assets/_Project/Trade/Resources/UI/`
2. `Create → UI Toolkit → Panel Settings Asset`
3. Имя: `MarketPanelSettings`
4. Оставить `Scale Mode = Constant Pixel Size` (по умолчанию)

---

## 2. Создать / обновить TradeDatabase (один раз)

**Уже существует:** `Assets/_Project/Trade/Data/TradeItemDatabase.asset`

1. Открыть его в Inspector
2. Убедиться, что в `All Items` есть:
   - `TradeItem_Mesium_v01` (itemId = `mesium_canister_v01`)
   - `TradeItem_Antigrav_v01` (itemId = `antigrav_ingot_v01`)
3. Если нет — добавить (эти два asset'а уже лежат в `Assets/_Project/Trade/Data/Items/`, перетащить в список)

---

## 3. Создать MarketConfig для каждой локации (× 4)

Через Unity Editor (один раз на каждую локацию):

1. `Project` → правый клик на `Assets/_Project/Trade/Data/Markets/`
2. `Create → ProjectC → Trade → Market Config`
3. Создать 4 файла:
   - `MarketConfig_Primium.asset` — `locationId = "primium"`, `displayName = "Примум"`
   - `MarketConfig_Secundus.asset` — `locationId = "secundus"`, `displayName = "Секунд"`
   - `MarketConfig_Tertius.asset` — `locationId = "tertius"`, `displayName = "Тертиус"`
   - `MarketConfig_Quartus.asset` — `locationId = "quartus"`, `displayName = "Квартус"`

4. В каждом — заполнить `Items` (через "+"):
   - **Primium**: мезий (basePrice 10, initialStock 80), антигравий (basePrice 50, initialStock 40)
   - **Secundus**: мезий (basePrice 14, initialStock 50), антигравий (basePrice 45, initialStock 50)
   - **Tertius**: мезий (basePrice 12, initialStock 60)
   - **Quartus**: мезий (basePrice 18, initialStock 30), антигравий (basePrice 60, initialStock 20)
   - *(значения стартовые, потом подкрутишь под баланс)*

5. Для каждой MarketItemConfig: `definition` ← перетащить соответствующий `TradeItem_Mesium_v01` или `TradeItem_Antigrav_v01` из `Assets/_Project/Trade/Data/Items/`.

> **Заметка про совместимость со старыми Market_*.asset:** старые файлы (`Market_Primium_v01.asset` и т.п.) — это `LocationMarket` SO со встроенным state. Они оставлены для совместимости со старым кодом, но **не используются** новой системой. Можно удалить после шага 8 (cleanup).

---

## 4. Создать префаб MarketZone (один раз)

1. В любой сцене создать пустой GameObject → именовать `MarketZone`
2. Inspector → `Add Component → Market Zone`
3. Настройки Inspector:
   - `Location Id`: например `primium` (для теста — один и тот же префаб можно переиспользовать, переопределяя id в каждом instance)
   - `Display Name`: `Примум`
   - `Trade Radius`: 5
   - `Ship Dock Radius`: 30
4. Сохранить как prefab: перетащить в `Assets/_Project/Trade/Prefabs/MarketZone.prefab` (папку создать если нет)
5. Удалить из сцены — будем расставлять вручную в WorldScene_X_Z

> **Важно:** префаб — шаблон, **не spawn через сеть**. Расставляется в сцене руками для каждого города/платформы. У каждого instance в сцене — свой `Location Id` (переопределение).

---

## 5. Сцена Bootstrap — добавить серверные компоненты

**Что есть:** `NetworkManager` (singleton, DontDestroyOnLoad), `NetworkManagerController`, `ClientSceneLoader`, `ScenePlacedObjectSpawner`

**Добавить:**

### 5.1. MarketServer (NetworkBehaviour, server-only)

1. В Bootstrap сцене → создать пустой GameObject → именовать `[MarketServer]`
2. `Add Component → Network Object` (RequireComponent добавится автоматически)
3. `Add Component → Market Server`
4. Inspector настройки:
   - **Trade Database**: перетащить `Assets/_Project/Trade/Data/TradeItemDatabase.asset`
   - **Market Configs**: перетащить 4 MarketConfig из `Assets/_Project/Trade/Data/Markets/`
   - **Use File Repository**: false (по умолчанию PlayerPrefs)
   - **Max Ops Per Minute**: 30

### 5.2. MarketTimeService (server-only MonoBehaviour)

1. Тот же GameObject `[MarketServer]` (или отдельный) → `Add Component → Market Time Service`
2. Inspector:
   - `Base Tick Interval Seconds`: 300 (5 мин)
   - `Market Time Multiplier`: 1.0
   - `Use Weather Factor`: false (по умолчанию выключено)
   - `Weather Controller`: оставить пустым (опционально, см. GDD 4.5)

### 5.3. MarketClientState (client-side singleton)

1. Новый GameObject → `[MarketClientState]`
2. `Add Component → Market Client State`
3. Галочка `Dont Destroy On Load` (по умолчанию true)

### 5.4. MarketWindow (UI Toolkit)

1. Новый GameObject → `[MarketWindow]`
2. `Add Component → UI Document`
   - **Panel Settings**: перетащить `MarketPanelSettings.asset` (см. шаг 1)
   - **Source Asset**: можно оставить пустым — MarketWindow подхватит UXML из `Resources/UI/MarketWindow.uxml` автоматически
   - Если хочется drag'n'drop — перетащить `Assets/_Project/Trade/Resources/UI/MarketWindow.uxml` в Source Asset
3. `Add Component → Market Window` (на тот же GameObject)
4. Inspector:
   - `Toggle Key`: E (по умолчанию)
   - `Visible On Start`: false (игрок сам открывает)

---

## 6. Сцена WorldScene_0_0 — демо-рынок

**Цель:** поставить 1 MarketZone (Примум), 2 ShipController (Light и Medium), проверить полный flow.

### 6.1. Корабли (если ещё не добавлены)

Согласно [INTEGRATION_SHIPS_TO_WORLD_0_0.md](INTEGRATION_SHIPS_TO_WORLD_0_0.md), в `WorldScene_0_0` уже есть root `ships` с 3 ShipController. Каждый должен иметь:
- `NetworkTransform` (Authority = Server) — **обязательно** для синхронизации позиции
- `CargoSystem` (Light/Medium) — **обязательно** для перевозки груза
- (опционально) `NetworkObject` уже есть (RequireComponent)

### 6.2. MarketZone (новый)

1. В `WorldScene_0_0` создать пустой GameObject → именовать `MarketZone_Primium`
2. Поставить в удобное место (например, рядом со стартовой позицией игрока, но не на пути кораблей)
3. `Add Component → Market Zone` (SphereCollider добавится автоматически)
4. Inspector:
   - `Location Id`: `primium`
   - `Display Name`: `Примум`
   - `Trade Radius`: 5
   - `Ship Dock Radius`: 30 (должен покрывать оба тестовых корабля)
5. В Scene View увидишь gizmo: голубой wire sphere для player radius, прозрачный для ship radius

### 6.3. Корабли в зоне

1. Поставить оба тестовых ShipController так, чтобы их позиция попадала в `shipDockRadius` MarketZone (30 м). Подгони позиции.
2. Запустить → в консоли должно появиться:
   ```
   [MarketServer] инициализирован: markets=4, repository=PlayerPrefsRepository
   ```

---

## 7. Проверка (verification)

### 7.1. Компиляция

После сохранения всех файлов:
1. Открыть Unity → дождаться компиляции
2. **Console → 0 errors, 0 warnings** (warnings про `CS0618: Obsolete` от старых классов — нормально, мы их не удалили ещё)
3. Если есть `CS0103: name 'X' does not exist` — проверить namespace (`ProjectC.Trade.Config`, `ProjectC.Trade.Core`, `ProjectC.Trade.Network`, `ProjectC.Trade.Client`, `ProjectC.Trade.Dto`, `ProjectC.Trade.Repository`, `ProjectC.Trade.Service`)

### 7.2. Host test

1. **Открыть BootstrapScene**, нажать Play
2. В консоли:
   ```
   [MarketServer] инициализирован: markets=4, ...
   [MarketTimeService] ...
   ```
3. Host появляется. Подойти к `MarketZone_Primium` (должен попасть в радиус 5 м)
4. Нажать **E** → должно открыться окно `MarketWindow` с товарами «Мезий» и «Антигравий»
5. Выбрать товар, нажать **КУПИТЬ** (qty=1):
   - Должно появиться зелёное сообщение «Куплено: mesium_canister_v01 x1»
   - Переключиться на вкладку СКЛАД → там 1 мезий
6. Переключиться на вкладку СКЛАД → выбрать мезий → выбрать корабль (если 1 — dropdown скрыт) → **ПОГРУЗИТЬ**
7. Должно появиться «Погрузка: mesium_canister_v01 x1»
8. Выбрать мезий на рынке → **ПРОДАТЬ** → купить снова → проверить что цена выросла (demand_factor ↑)

### 7.3. Client test (multiplayer)

1. Собрать билд (File → Build → StandaloneWindows64 → Build)
2. Запустить build (client) + Editor (host) одновременно
3. На клиенте подойти к `MarketZone_Primium` → нажать E
4. **Проверить:** окно открылось, цены совпадают с хостом, покупка на клиенте уменьшает его кредиты (а не дубль)

### 7.4. Time multiplier test

1. Editor: Inspector на `[MarketTimeService]` → `Market Time Multiplier` = 10
2. Подождать ~30 сек → цены должны заметно двинуться (несколько тиков)
3. Поставить = 0.1 → подождать → цены почти не двигаются
4. Вернуть = 1
5. **Проверить:** цены НЕ скатываются в 0 даже после многих тиков (time-based decay, half-life 30 мин)

### 7.5. Multi-ship test

1. В `WorldScene_0_0` поставить 2 корабля в зоне MarketZone
2. Запустить, открыть рынок
3. Должен появиться dropdown «Корабль: [выбор]»
4. Переключить корабль → проверить, что Load/Unload идут в выбранный

### 7.6. Position validation test

1. Сесть в корабль, уплыть далеко за пределы `MarketZone.shipDockRadius`
2. Кликнуть «ПОГРУЗИТЬ» (если окно ещё открыто)
3. **Должно появиться** красное сообщение: «Корабль не в зоне причала»
4. Вернуться в зону → должно работать

---

## 8. Cleanup (после успешной проверки)

Когда всё работает и ты готов, **отдельным коммитом** удали старое:

### Файлы для удаления:

| Файл | Замена |
|------|--------|
| `Assets/_Project/Trade/Scripts/TradeMarketServer.cs` | `MarketServer.cs` + `TradeWorld.cs` |
| `Assets/_Project/Trade/Scripts/TradeUI.cs` | `MarketWindow.cs` + UXML/USS |
| `Assets/_Project/Trade/Scripts/PlayerTradeStorage.cs` | `Warehouse.cs` (POCO) |
| `Assets/_Project/Trade/Scripts/PlayerDataStore.cs` | `IPlayerDataRepository` |
| `Assets/_Project/Trade/Scripts/LocationMarket.cs` | `MarketConfig.cs` (read-only SO) |
| `Assets/_Project/Trade/Scripts/MarketItem.cs` | `MarketItemState.cs` (POCO) |
| `Assets/_Project/Trade/Scripts/NPCTrader.cs` | новая версия в `Core/NPCTrader.cs` |
| `Assets/_Project/Trade/Scripts/MarketEvent.cs` | новая версия в `Core/MarketEvent.cs` |
| `Assets/ProjectC_1.unity` | (тестовая сцена, не используется в новой архитектуре) |
| `Assets/_Project/Scenes/Test/ProjectC_1.unity` | то же |
| `Assets/_Project/Trade/Data/Markets/Market_*.asset` (×4) | новые `MarketConfig_*.asset` |
| `docs/TRADE_SYSTEM_RAG.md` | заменён `docs/dev/TRADE_V2_DESIGN.md` |
| `docs/TRADE_DEBUG_GUIDE.md` | заменён этим handbook'ом + design'ом |

### Метод для удаления из `NetworkPlayer.cs`:

```csharp
// УДАЛИТЬ:
- [Rpc(SendTo.Server)] public void TradeBuyServerRpc(...)
- [Rpc(SendTo.Server)] public void TradeSellServerRpc(...)
- [ClientRpc] public void TradeResultClientRpc(...)
```

### Что ещё почистить:

- В `BootstrapScene` — удалить старые GameObject'ы `TradeMarketServer` (если остались)
- В `WorldScene_0_0` — удалить старые ссылки на `PlayerTradeStorage`

---

## 9. Что делать, если что-то сломалось

| Симптом | Что проверить |
|---------|---------------|
| `[MarketServer] не найден` | В Bootstrap сцене есть `[MarketServer]` GameObject с компонентом |
| Окно не открывается по E | (1) Игрок в `tradeRadius` MarketZone, (2) UXML загружен (нет ошибок в Console) |
| RPC отклоняется с `NotInZone` | (1) `MarketZone.LocationId` совпадает с locationId в MarketConfig, (2) trigger реально срабатывает (виден gizmo в Scene view) |
| `MarketItemState.config == null` | В MarketConfig не заполнен `Items` или `definition` ссылка |
| Цены все 0 | `basePrice` в MarketItemConfig = 0 или `item` ссылка потеряна |
| Snapshot не приходит клиенту | (1) MarketClientState.Instance != null на клиенте, (2) NetworkPlayer.ReceiveMarketSnapshotTargetRpc вызывается (поставить лог) |
| Двойной RPC (как в v1) | Невозможен: все RPC с `RequireOwnership=true`, `_tradeLocked` больше не нужен — сервер идемпотентен |
| `currentPrice = 0` после Recalculate | `basePrice` ≤ 0 или все множители в 0 (защита: `PriceFormula.CalculatePrice` clamp к [0.5×, 5×]) |

---

## 10. Контакты с GDD

| GDD секция | Реализация v2 |
|------------|---------------|
| 4 Pricing Model | `PriceFormula.CalculatePrice` + `MarketItemState` |
| 4.5 Time-based Economy | `MarketTimeService` + `PriceFormula.DecayFactor` |
| 5.1 NPC Trading | `MarketZone` + `MarketWindow` |
| 5.5 Multi-ship trading | `MarketZone._shipsInZone` + DropdownField в `MarketWindow.uxml` |
| 7 Cargo & Transport | `CargoData` + `CargoSystem` (без изменений класса) |
| 13 Tuning Knobs | Inspector на `[MarketTimeService]` + `[MarketServer]` |

Все formula'ы из GDD §12 реализованы в `PriceFormula` (можно подменить в одном месте).

---

**Готово к проверке.** Если что-то непонятно — спрашивай, поправлю handbook.
