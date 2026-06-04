# Markets — Fixes History
Хронология багов, диагнозов и фиксов рыночной подсистемы. Текущая версия (2026-06-04) — стабильная: полный цикл BUY/LOAD/UNLOAD/SELL работает.

## 2026-06-04 — FIX: рынок не открывается, если игрок подлетел на корабле (GetEffectivePosition)

**Файлы:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs:104-116` — новый helper `GetEffectivePosition()`
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs:169, 287` — использовать effective position в `PollLocalPlayerZone` и `OnTriggerEnter`
- `Assets/_Project/Trade/Scripts/Client/MarketInteractor.cs:2, 83` — `using ProjectC.Player;` + использовать effective position в `FindNearestZone`

**Симптом (из лога теста, см. unity-mcp):**
```
[MarketZone:primium] server detected player in zone: clientId=0
[MarketZone:primium] DIAG PollLocalPlayerZone: outside zone, dist=4045,9, tradeRadius=36,0, localPlayerPos=(39820.91, -1128.69, 41888.07), zonePos=(40096.50, 2510.00, 40140.60)
```
Сервер видит игрока в зоне (через OverlapSphere — попадает в коллайдер корабля), клиент упорно сообщает дистанцию 1600–4000м. `MarketInteractor.TryOpenMarket` уходит в `FindNearestZone`, который тоже мерит от `localPlayer.transform.position` — получает best=null → возвращает false → окно рынка не открывается. UI выглядит зависшим.

**Сценарий:** игрок сидит в корабле и подлетает к причалу рынка. Если до посадки/входа в зону нажать E (выход из зоны → `LocalPlayerZone` сбрасывается), а потом залететь в зону, E перестаёт открывать рынок. Пешком — работает, потому что `CharacterController` обновляет `transform.position` каждый кадр.

**Корневая причина:** в `ApplyShipState` (`NetworkPlayer.cs:441-448`) `_controller.enabled = false` — игрок больше не двигается через `CharacterController`, его `transform.position` заморожен на точке посадки. Реально в мире летит корабль, а пилот «висит» в воздухе в исходной точке. Все клиентские дистанционные проверки (рынок, OnTriggerEnter) брали `localPlayer.transform.position` напрямую — получали замороженную позицию, хотя сервер через `Physics.OverlapSphere` корректно детектил коллайдер корабля внутри `tradeRadius`.

**Фикс (один helper, 3 точки использования):**
- В `NetworkPlayer` добавлен публичный `GetEffectivePosition()`: возвращает `_currentShip.transform.position` если `_inShip && _currentShip != null`, иначе `transform.position`.
- `MarketZone.PollLocalPlayerZone` (client-side, обновление `LocalPlayerZone`): `Vector3.Distance(zone, localPlayer.GetEffectivePosition())`
- `MarketZone.OnTriggerEnter` (client-side, ранняя установка `LocalPlayerZone` при срабатывании SphereCollider): `Vector3.Distance(zone, np.GetEffectivePosition())`
- `MarketInteractor.FindNearestZone` (fallback, когда `LocalPlayerZone == null`): использует тот же `GetEffectivePosition()`

**Что не делали (важно):**
- ❌ Не рефакторили `ApplyShipState` чтобы «правильно» парентить игрока к кораблю или двигать `transform.position` — это сломало бы `NetworkTransform` репликацию, камеру и CharacterController при выходе.
- ❌ Не трогали `MarketZone.PollPlayersInRadius` (server-side) — там `OverlapSphere` уже корректно находит коллайдер корабля и через `GetComponentInParent<NetworkPlayer>` матчит пилота.
- ❌ Не убирали diagnostic-логи из `MarketInteractor`/`MarketZone` — оставлены на случай следующих регрессий (KNOWN_ISSUES §1).
- ❌ Не рефакторили legacy `TradeTrigger` / `AutoTradeZone` / `TradeUI` (KNOWN_ISSUES §3) — отдельный cleanup.

**Что проверить вручную (в Play Mode, host):**
1. Сесть в корабль (F) → улететь за пределы зоны рынка (X<40000, Y<2000) → нажать E в полёте → должна быть `[MarketInteractor] LocalPlayerZone is null and no zone in range`.
2. Залететь в зону на корабле → в консоли появится `[MarketZone:primium] client: local player entered zone (dist=~0..36)`.
3. Нажать E → откроется окно рынка, в консоли `[MarketInteractor] TryOpenMarket: zone='primium'`.
4. Сойти с корабля (F) на палубе внутри зоны → `LocalPlayerZone` остаётся `this` (расстояние меряется так же — от корабля, но корабль в той же точке, что игрок).
5. Обычный сценарий (пешком) — регрессий быть не должно.

---

## 2026-06-04 — UI верстка (4 фикса + 1 fix жизненного цикла + 3 диагностических лога)

### FIX 1 — ListView selection не обновлял `_selectedMarketItem`

**Файл:** `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs:177-216`

**Симптом:** Клик по строке в списке товаров не выделял её. Кнопки КУПИТЬ/ПРОДАТЬ сразу выходили по `if (_selectedMarketItem < 0) return;`. Покупка не работала, хотя цены отображались.

**Корневая причина:** В коде не было `selectionType` / `selectionChanged` callback на ListView. ListView обновлял свой внутренний `selectedIndex`, но UI-контроллер не получал уведомления. Плюс в Unity 6 `onSelectionChange` deprecated — нужно `selectionChanged` с `IEnumerable<object> selectedItems` (сами объекты, а не индексы).

**Фикс:**
- На всех 3 ListView (`_itemList`, `_warehouseList`, `_cargoList`):
  - `_list.selectionType = SelectionType.Single`
  - `_list.selectedIndex = -1` (стартовое)
  - `_list.selectionChanged += selectedItems => { _index = FindSelectedItemIndex<T>(list, selectedItems); _list.Rebuild(); }`
- Новый helper `FindSelectedItemIndex<T>` (MarketWindow.cs:520-538) — ищет объект в `itemsSource` через `Array.IndexOf` или линейный поиск, возвращает индекс или -1.

### FIX 2 — `IsLayoutValid()` был слишком строгим

**Файл:** `MarketWindow.cs:107-114`

**Симптом:** Первый E после запуска сцены — `EnsureBuilt()` не вызывался (или вызывался лишний раз). UI не появлялся до второго нажатия E.

**Корневая причина:** Старая проверка полагалась на `resolvedStyle.width` — на первом кадре после `Clear() + CloneTree()` он бывает `NaN/0` (USS layout не успел посчитаться). Это приводило к двойной пересборке или, наоборот, пропуску пересборки.

**Фикс:** Проверяем только что дерево существует: `return _built && _root != null && _mainContainer != null;`. Не полагаемся на `resolvedStyle`.

### FIX 3 — `MarketClientState.Instance == null` на хосте

**Файл:** `Assets/_Project/Scripts/Core/NetworkManagerController.cs` (в `Awake()`)

**Симптом:** Сервер видел игрока в зоне, отправлял `Subscribe OK`, но клиент (на том же процессе) не получал `OnSnapshotReceived` — `MarketClientState.Instance == null` в `NetworkPlayer.ReceiveMarketSnapshotTargetRpc`.

**Корневая причина:** `MarketClientState` GO не существовал на старте — `[MarketClientState]` GameObject нужно было создавать вручную в `BootstrapScene`. Если забыли — NRE.

**Фикс:** В `NetworkManagerController.Awake()` создаём `[MarketClientState]` как root GameObject (DontDestroyOnLoad) с компонентом `MarketClientState`. Гарантирует наличие singleton до старта `NetworkManager`.

### FIX 4 — `pickingMode` на `_root` ломал UGUI клики

**Файл:** `MarketWindow.cs:138-148, 647, 685`

**Симптом:** Когда окно рынка было **закрыто** (display:None на main-container, но `_root` TemplateContainer растянут на весь rootVE с position:Absolute, inset:0), невидимый `_root` перехватывал ВСЕ клики → UGUI кнопки (Host, Connect, ...) не реагировали.

**Корневая причина:** UI Toolkit PanelSettings получает pointer events РАНЬШЕ UGUI Canvas (InputSystemUIInputModule маршрутизирует так в Unity 6). По умолчанию `pickingMode = Position`, который перехватывает клики по всему растянутому root.

**Фикс:**
- В `EnsureBuilt()`: `_root.pickingMode = PickingMode.Ignore;` (по умолчанию)
- В `Show()`: `_root.pickingMode = PickingMode.Position;` (включаем только когда окно открыто)
- В `Hide()`: `_root.pickingMode = PickingMode.Ignore;` (возвращаем)

### FIX 4b — `.list-section` flex-shrink ломал layout

**Файл:** `Assets/_Project/Trade/Resources/UI/MarketWindow.uss`

**Симптом:** Списки товаров/склада/груза схлопывались до 0px высоты. Заголовки "Товары на рынке / Ваш склад / Груз корабля" висели одновременно (FIX 4a тоже, но это была другая причина).

**Корневая причина:** В USS на `.list-section` стояло `flex-shrink: 1` и `min-height: 0`. Внутри `flex-direction: column` с фиксированной высотой это приводит к сжатию секции до 0. Контейнер `main-container` имеет `flex-direction: column; align-items: stretch;`, и секции конкурировали за вертикальное пространство.

**Фикс:** Убрали `flex-shrink: 1` и `min-height: 0` на `.list-section`. Теперь секции занимают естественную высоту. Дополнительно (FIX для одновременных заголовков) — `SwitchTab("market")` в `MarketWindow.cs:488-502` скрывает через `display:None` всю секцию (заголовок + список), а не только ListView.

### FIX 5 (diagnostic) — `MarketZone.PollLocalPlayerZone` логирует дистанцию

**Файл:** `MarketZone.cs:147-196`

**Назначение:** Throttled debug-логи (раз в ~5 сек, при `_diagTickCounter % 20 == 0`) для диагностики «игрок не в зоне, хотя кажется что в зоне»:
- `Debug.Log("[MarketZone:primium] DIAG PollLocalPlayerZone: outside zone, dist=344,3, tradeRadius=30, ...")`
- `Debug.Log("[MarketZone:primium] DIAG PollLocalPlayerZone: FindLocalPlayer=null (total NetworkPlayers=1, IsSpawned=1, IsOwner=1)")`

Это помогло выявить, что tradeRadius реально 30м (а не 5 как в спеке), и что LocalPlayerZone не обновлялся из-за guard `if (LocalPlayerZone == this) return;` в старой версии — игрок мог уйти на 100м, а LocalPlayerZone оставался `this`.

### FIX 6 (diagnostic) — `MarketInteractor.TryOpenMarket` логирует Registry

**Файл:** `MarketInteractor.cs:27, 50, 59, 88-104`

**Назначение:** Логирует `LocalPlayerZone` и `Registry.All.Count` при каждом вызове E. Плюс `FindNearestZone` логирует дистанции ко ВСЕМ зонам, чтобы видеть какие вообще зарегистрированы и какие в радиусе.

### FIX 7 (diagnostic) — `MarketInteractor.FindNearestZone` логирует каждую зону

**Файл:** `MarketInteractor.cs:64-106`

**Назначение:** Когда `LocalPlayerZone == null`, fallback `FindNearestZone` логирует:
```
[MarketInteractor] FindNearestZone: localPlayerPos=(x,y,z), zones=1 — primium(d=28,7/r=30,0@(x,y,z)) => best=primium
```

## Что ещё было исправлено (более ранние сессии)

### Race condition: `MarketZone.OnEnable` до `NetworkManager.Start`

**Файл:** `MarketZone.cs:68-88`

**Симптом:** Zone не регистрировалась в `MarketZoneRegistry` если сцена грузилась раньше старта NetworkManager. Клиент потом не находил зону через `FindNearestZone`, сервер не находил через `MarketZoneRegistry.Get`.

**Фикс:** Всегда регистрируем в `OnEnable` + подписываемся на `NetworkManager.OnServerStarted`/`OnClientStarted` для повторной регистрации. Дублирующая регистрация безопасна (`Register` проверяет `_zones[locationId] == this`).

### Guard `if (LocalPlayerZone == this) return;` блокировал cleanup

**Файл:** `MarketZone.cs:170-195` (PollLocalPlayerZone)

**Симптом:** Игрок уходил из зоны (dist > tradeRadius), но `LocalPlayerZone` оставался `this`. TryOpenMarket работал, но игрок был далеко.

**Фикс:** Убран ранний return. Poll ВСЕГДА пересчитывает дистанцию и ставит/сбрасывает `LocalPlayerZone` строго по факту попадания.

### Debounce на `_playersInZone` remove

**Файл:** `MarketZone.cs:208-256` (PollPlayersInRadius)

**Симптом:** CharacterController + SphereCollider Trigger timing → OverlapSphere иногда «промахивался» (NetworkTransform interpolation, физика), игрок удалялся из `_playersInZone` на 250мс → следующий RPC получал `NotInZone`.

**Фикс:** `MISS_THRESHOLD = 3` подряд пропусков (~0.75с) перед удалением. `Dictionary<ulong, int> _missingTicks` счётчик.

### SphereCollider radius = max(tradeRadius, shipDockRadius) = 591м

**Файл:** `MarketZone.cs:55-66` (Awake)

**Симптом:** Awake ставил `sphere.radius = Mathf.Max(tradeRadius, shipDockRadius)`. SphereCollider детектил игрока в 591м от центра зоны, `OnTriggerEnter` срабатывал преждевременно → `LocalPlayerZone = this` до того, как игрок в реальном tradeRadius.

**Фикс:** `_sphere.radius = tradeRadius` (только для player detection). Корабли детектятся через `PollShipsInRadius` (OverlapSphere с shipDockRadius) — для них SphereCollider не нужен. Дополнительная defense-in-depth проверка `dist ≤ tradeRadius` в `OnTriggerEnter` (MarketZone.cs:287-288).

## Известные ограничения, оставшиеся после 2026-06-04

См. [KNOWN_ISSUES.md](KNOWN_ISSUES.md):
- §1 Diagnostic-логи остаются — можно убрать после стабилизации
- §2 Initial `wh=0` → `wh=1` warning в `[MarketWindow] Show(): main w=0 h=0` — косметика
- §3 Старая v1 архитектура (`TradeUI`, `TradeMarketServer`, `PlayerTradeStorage`, ...) не удалена
- §4 NetworkPlayer.TradeBuyServerRpc/SellServerRpc (lines 588-617) — dead code, не вызывается
