# Ship System — полноценное код-ревью (2026-09-18)

> Scope: `Assets/_Project/Scripts/Player/ShipController.cs` (2425 строк), `ShipInputReader.cs`,
> `Scripts/Ship/**` (Fuel, Hull, DamageConfig, Modules, Cargo, Repair, Meziy, Telemetry, Key, DeckNav,
> Contrail/VFX, Corridors), `Trade/.../ShipCargoServer.cs`, `ShipCargoConsoleWindow.cs`,
> `Core/ShipPosition/ShipPositionServer.cs` + все доки в `docs/Ships/`.
> Метод: чтение файлов целиком + сверка с доками + точечная верификация P0 по коду.

## Code Review: Ship System

### Standards Compliance: 4/9 passing

- ❌ Паблики без доков (`ShipContrailVfx.cs:28-65` — 12 public полей; `ShipInputReader.cs:26-39` — events без доков).
- ❌ Сложность >10: `ShipController.FixedUpdate:1233-1547` (~300 строк), `ShipInputReader.ProcessMeziyInput:165-259` (~12 ветвей), `ShipController.UpdateTelemetryState:1020-1143` (~120 строк при лимите 40).
- ❌ Методы >40 строк: `FixedUpdate`, `UpdateTelemetryState`, `ProcessMeziyInput`, `Meziy Apply` — все превышены.
- ❌ Статик-синглтоны для состояния: `ShipModuleCatalog`, `ShipCargoRegistry`, `TradeWorld.Instance`, `KeyRodInstanceWorld`, `AltitudeCorridorSystem.Instance:30-31` — вместо DI.
- ❌ Конфиги не в одном месте: тюнинг классов разъехался на 3 файла — `ShipController.ApplyShipClass` + `ShipFuelSystem.Initialize:224-240` (`switch shipClass`) + `ShipDamageConfig.GetMaxHull:97-107`. Новый класс = правки в 3 местах.
- ✅ Неймспейсы `ProjectC.*` — соблюдены везде.
- ❌ Нейминг: инспекторные поля `ShipController.cs:305-426` без `_` (`thrustForce`, `maxSpeed`, … вместо `_thrustForce`); `ShipFuelSystem.cs:110/115/120` — `isRefueling/thrustPenaltyMult/speedPenaltyMult` вместо `PascalCase`; `ModuleSlot.cs:33/38` — `isOccupied/installedModuleId`; `ShipModuleManager.cs:25` — `currentPowerUsage`; `MeziyModuleActivator.cs:63-84` — без `_`.
- ❌ Комменты: `ShipContrailVfx.cs:1-6,90-93` на английском против «комменты на русском» (AGENTS.md).
- ❌ Интерфейсы: прямые статики вместо абстракций (`TradeWorld`, `KeyRodInstanceWorld`, `ShipCargoRegistry`, `WindManager.Instance` в `ShipController`).

### Unity Patterns: ISSUES FOUND

- `GetComponent<CharacterController>()` в `OnTriggerEnter/Exit` (`ShipController.cs:576,584`).
- `FindAnyObjectByType<AltitudeCorridorSystem>` в `Awake` каждого корабля (`ShipController.cs:671`); `FindObjectsOfType` на пути pickup (`KeyRodInstanceWorld.TryClaimOrFindInstanceForItem:238` — устаревший API); `FindObjectsByType` на каждый ребаз (`ShipDeckNav.NotifyWorldRebased:339`).
- `GetComponentsInChildren` + `new MaterialPropertyBlock` на каждый рендерер при каждом `ApplyShipColor` (`ShipController.cs:982-1011`).
- `GetComponentsInChildren` + `GetComponent<Collider>` в цикле (`FindNearestPilotSeat:2347-2352`).
- `GetComponentsInChildren` каждый кадр (`ShipCargoVisual.SetOverflowAlpha:534-548`).
- `GameObject.CreatePrimitive` в рантайме (`ShipCargoVisual.GetPooledBox:380`, `SpawnOverflowIndicator:493` — лишний Collider + draw calls); `new GameObject + AddComponent<VisualEffect>` в `Start` (`ShipContrailVfx:154,159`).
- `GameObject+ParticleSystem+Light` в рантайме при первом `Activate` (`MeziyThrusterVisual:83-84` — и на сервере тоже).
- `SphereCollider` добавляется в рантайме (`ShipCargoConsole.Awake:62-68`).
- `ForceRefresh:560-569` вызывает `Awake()` вручную (`ShipCargoVisual`) — нарушение lifecycle, риск двойной подписки.
- `VFX ApplyVfxScale` каждый кадр: `HasFloat+SetFloat ×3 ×N эмиттеров` + `Random.Range` на размер (`ShipContrailVfx:185-186,246,253`) — мерцание + стоимость 60 fps.
- Client-only визуалы крутятся на сервере без гарда (waste на dedicated/host): `ShipContrailVfx`, `ShipPartShake`, `EngineThrusterVisual`, `MeziyThrusterVisual`, `ShipModuleVisualApplier` (инстанцирует визуалы и на сервере).
- `ShipInputReader` опрашивает `Keyboard.current.wKey/aKey/...` + `Mouse.current.delta` (`:88-114`) вместо Input Actions — против AGENTS.md (паттерн `PlayerInputReader` + New Input System 1.13.1). Неребиндимый WASD/EQ/CVXZ, без геймпада.
- `IsGrounded:1866` — `Physics.Raycast` без маски, 1.5 м при корабле 8–15 м — почти всегда `false`.
- `AltitudeCorridorSystem`: `GetActiveCorridor:86-107` — `Vector3.Distance` (sqrt) по всем коридорам на корабль каждый `FixedUpdate` (`ShipController.cs:1792`) → `sqrMagnitude`; `GetCityCorridors:142-151` аллоцирует список на вызов; `EnsureRuntimeCorridors:176-188` — клоны без дедупликации и без `Destroy` старых в `SetCorridors:157-164` (течь); `SetupDefaultCorridors/CreateCorridorAsset:219-270` — `CreateInstance` без `AssetDatabase.CreateAsset` (живёт до стопа редактора, пункт меню вводит в заблуждение); синглтон с `Destroy(gameObject):38` опасен при 24 аддитивных сценах.
- SO-дефолт `verboseLogging = true` в обоих конфигах (`ShipDamageConfig.cs:69`, `ShipCollisionDamageConfig.cs:43`) — спам логов в проде (`ShipController.cs:626`, `ShipHull.cs:145`). Дефолт обязан быть `false`.
- Мёртвое поле `repairCostCredits` с `[HideInInspector]` + «УСТАРЕЛО» (`ShipDamageConfig.cs:65`) — удалить, история в git.
- `MeziyThrusterVisualEditor:15-32` (CustomEditor) лежит в рантайм-файле — вынести в `Editor/`.
- `ShipDeckNav`: коммент обещает `≤1 AddNavMeshData за кадр (:99-100)`, но `ProcessPendingRegistrations` вызывается из `LateUpdate` каждого корабля (`:239`) → при N кораблях N регистраций за кадр; `ConfigureEditorLogging:108-111` меняет глобальный `SetStackTraceLogType`.
- `ShipPositionServer.PrepareForServerStart:143` сбрасывает чужой глобал `CumulativeRebaseOffset` — сайд-эффект чужой подсистемы.

### Architecture: VIOLATIONS FOUND

- **God Object:** `ShipController` — физика + коридоры + ветер + модули + мезия + топливо + груз + докинг + телеметрия + перекраска + recall + persistence + NPC-pilot + ввод. Минимум 10 зон в одном `NetworkBehaviour`.
- **Расщеплённая логика (подтверждено):**
  - Ввод мезии: press-события в `ShipInputReader.ProcessMeziyInput`, а release — опросом `IsKeyDown` в `ShipController:1352-1396` → риск рассинхрона.
  - Порог урона: фильтр `minCollisionRelativeSpeed` в `ShipController.OnCollisionEnter:655`, а порог — в конфиге (`ShipHull`/`ShipDamageConfig`) — одна проверка на два файла.
  - Поиск корабля: `ShipCargoRegistry.Get` vs `ShipCargoServer.FindShipController:387-393` (через `SpawnManager`) — два lookup-пути на один корабль. Унифицировать на реестре.
  - Лимиты груза: сервер резолвит `shipClass (:121,248)` + `GetOrLoadCargo + TryAdd`, а телеметрия показывает эффективные лимиты (`ShipController.GetEffectiveCargoLimits:1193-1217`, base + бонусы модулей через `ShipCargoRegistry`). **P1-check перед фиксом:** подтвердить, что `TradeWorld.TryAdd/GetSpeedPenalty` читает `ShipCargoRegistry.GetEffectiveLimits()`, а не статику `ShipClassLimits` — иначе сервер отвергает то, что HUD показывает свободным (или наоборот).
  - Курс обмена: клиент (`ShipCargoConsoleWindow`) читает `ResourceExchangeResolver.Default`, сервер — инжектированный `_exchangeRateConfig` → «packable» на клиенте ≠ «не поддерживает» на сервере.
  - Цвет: `SetShipColor + ApplyShipColor:436-437` (`ShipModuleServer`) — двойное применение на сервере.
- **Дубли (кандидаты на generic/хелпер):**
  - Поиск слота по `gameObject.name` ×5 (`ShipModuleServer:100-108,189-197,260-268,312-320` + ClientRpc) → `FindSlot(slotName)`.
  - Ownership-check ×5 (`:71-84,161-173,234-246,398-410,473-485`) → `ValidateOwnership()`.
  - `ReplaceModule:125-152` (`ShipModuleManager`) vs ручной `Remove+Install` в `ShipModuleServer:124-140`.
  - 7 геттеров-множителей (`GetThrust/Yaw/Pitch/Lift/Roll/MaxSpeed/WindExposure`) — copy-paste `foreach slots` → один агрегатор.
  - `Default`-лоадер (`Resources.Load + CreateInstance` без `hideFlags`) продублирован в `ShipDamageConfig:76-92` и `ShipCollisionDamageConfig:50-67` → generic.
  - `ShipTelemetryClientState` vs `ShipCargoClientState` — только нейминг общий, логики общей нет (безвредно).
- **Хрупкие связки:**
  - `ModuleSlot.ValidateCompatibility:91` — `module.type == (ModuleType)slotType` работает только пока порядок enum-ов совпадает → явный switch.
  - `ShipClassMapping.Resolve` → `null`, вызывающие молча фолбэчатся на `Medium` (`ShipCargoServer:121-122`, `ShipController:1077-1078`) — тяжёлый корабль с битым маппингом получит лимиты Medium без варнинга. + `Default` кэшируется навсегда, нет `Reset` для тестов, нет валидации полноты (все 4 `ShipFlightClass`?).
  - `ShipModuleCatalog.Initialize:19` — второй вызов no-op → смена `ModuleShopDatabase` у NPC не подхватывается (`RepairManagerWindow.Show:363`); дубли `moduleId` молча игнорируются (`:31-32`).
  - `ShipModuleManager` геттеры/Recalculate не проверяют `slot == null` (NRE на уничтоженном слоте), сервер — проверяет. `InstallModule` возвращает `false` если занято (`ModuleSlot:53-57`) → сервер вынужден снимать вручную.
  - `ShipModule.compatibleClasses` пуст = «всем» (`:158-164`) — неявный default, в UI не подсвечен.
  - `cargoPenaltyReduction` — инвертированная семантика («отрицательный бонус = хорошо», `ShipModule.cs:93-94`, `ShipModuleManager:253-258`, `ShipController:1207`) — выстрелит при настройке.
  - `ShipModule` тащит мезий-блок (`isMeziyModule/meziyForce/...:109-123`), причём `meziyDuration/meziyCooldown` никто не читает (`MeziyModuleActivator` живёт на своих `overheatThreshold/cooldownDuration`).
  - `MeziyModuleActivator.GetPassiveModifier:240-259` захардкожены `"MODULE_MEZIY_PITCH/ROLL/YAW/THRUST"` — переименование в каталоге ломает связь.
- **События:** `ShipModuleVisualApplier.OnModuleChanged` — `static` глобальное (`:62`), каждый аппликатор каждого корабля получает события всех → O(N²). Плюс `GetAttachmentRotation:157-166` смешивает local/world (`baseRot * LookRotation(transform.forward)`), `LookRotation(Vector3.up)` вырожден — проверить визуально. Если `visualPrefab` содержит `NetworkObject` — спавн неспавненного объекта на клиентах.
- **Синглтоны с двумя лицами:** `ShipCargoServer.Instance` выставляется и на клиенте (`:52`, затем `enabled=false :56`) — `ShipCargoConsoleWindow:539,567` дергает RPC через клиентскую копию (работает, но хрупко). `ShipTelemetryClientState.Awake:108-111` без `DontDestroyOnLoad`/уничтожения дубликата — два инстанса молча подменяют `Instance`.
- **Утечки подписок:** `ShipController` не отписывается от `ShipHull.OnHullChanged (:795)`, от `TradeWorld.OnCargoChanged (:803)` — отписка условна (`:848`, если `Instance==null` — делегат утекает); `ShipHull:229-237` отписывается только от `CombatServer`; `ShipTelemetryClientState.SubscribeToShip:90` подписывает лямбду, `Unsubscribe:98-103` чистит только словарь (отписки от события нет, двойной Subscribe → двойные ивенты); корутины `RegisterCargoWhenReady/CreateKeyInstanceWhenReady (:807,812)` не останавливаются в `OnNetworkDespawn`; очередь `ShipDeckNav.s_pendingRegistrations` не чистится от деспавненных; `ShipCargoRegistry` хранит прямые ссылки на `ShipController (:37)` — пропущенный `Unregister` = stale-лимиты; `ShipCargoLimitsConfig.entries` — публичный мутабельный массив; `MeziyModuleActivator.GetActiveStates:314` отдаёт живой `Dictionary` наружу.
- **Persistence split-brain:** `ShipPositionServer` персистит только transform/dock/engine (`:380-395`), матч по стабильному `ShipPersistentId (:197)` — правильно; но модули/карго/цвет восстанавливаются другими системами (или нет) → позиция вернулась, обвес сброшен в префабный. `ApplyRestore:335-336` ставит `rb.position/rotation` напрямую без форсинга `NetworkTransform` (рывки); `TryLoadCorrectedPlayer:288-304` делает файловый IO на пути размещения пилота (хитч).

### SOLID: ISSUES FOUND

- SRP: `ShipController` (10+ зон), `FixedUpdate` (~300 строк), `MeziyModuleActivator` + топливо/визуал в одном.
- OCP: `switch (ShipFlightClass)` (`ShipController:701`, `ShipFuelSystem:224-240`, `ShipDamageConfig:97-107`); `switch` по строковым `moduleId` (`ShipController:2001-2020`); `switch KeyCode→Key` (`:2085-2120`) — новый класс/модуль/клавиша = правка ядра.
- LSP: `ShipHull.IsAlive() => true (:164)` врёт контракту `IDamageTarget` — `CombatServer` обязан спецкейсить корабли, иначе `EntityKilledEvent` никогда не придёт.
- ISP/DIP: жирная зависимость на статики вместо абстракций (см. выше).

### Game-Specific Concerns (сеть/баланс/P0 — верифицировано чтением кода)

**P0 — читы/десинхрон (фиксить первым):**

1. **Сервер читает свою клавиатуру для всех кораблей.** `ShipController.FixedUpdate` (серверный путь): дозаправка `IsKeyDown(KeyCode.L) (:1324)`, мезия `C/V/Z/X/Shift+A/D/Shift+W/S (:1352-1396)`, крен `GetCurrentRollInput (:2128-2133 → :1413)`. Нажатия клиентов игнорируются; нажатия на хосте двигают чужие корабли. Перевести мезию/ролл/дозаправку на RPC от владельца. Мёртвый код `GetCurrentPitchInput (:2139)` / `GetCurrentYawInput (:2149)` — удалить.
2. **`SubmitShipInputRpc (:1553)` без Clamp.** Клиент шлёт `thrust=1e6`, сервер лишь умножает на `thrustForce`. Нужен `Mathf.Clamp(-1,1)`. Рядом: `AddPilotRpc/RemovePilotRpc SendTo.Everyone (:1900-1921)` + `AddPilot (:1892)` без ownership-check — любой клиент вписывает любого пилота.
3. **Client-authoritative цены — фарм кредитов.** `ShipModuleServer`: `RequestSellModuleRpc` принимает `sellCredits` от клиента (`:227`) → `sellCredits=int.MaxValue`; `RequestRepaintShipRpc` / `RequestRepairHullRpc` принимают `cost` (`:388`, `:464`) → `cost=0`, а при `cost<0` строка `TryModifyCredits(clientId, -cost)` (`:425`, `:515`) **начислит** кредиты. Сервер обязан смотреть в `ModuleShopDatabase`, клиентскую цифру игнорировать/только сверять. Нет rate-limit (у `ShipCargoServer` есть `_maxOpsPerMinute`, тут нет) + нет проверки дистанции до NPC (только `IsDocked`).
4. **`RecallShipToPadServerRpc (:2381, Everyone)` доверяет клиенту.** `padPosition` и `cost` приходят от клиента (`:2382`); проверок владения/дистанции нет; `cost=0` + телепорт любого корабля в любую точку через `_rb.position (:2409)` вместо сетевой синхронизации (резиновый бэнд). Нужны: ownership-check, серверный `cost`, валидация пада из реестра, списание до телепорта.
5. **`_rb.AddForce(... * dt, ForceMode.Force)` (:2033)** — двойной `dt` (`Force` уже интегрирует). Мезия-тяга слабее задуманной ~в 50 раз. Убрать `* dt`.
6. **`ShipFuelSystem.currentFuel` — plain float (`:33`), обычный `MonoBehaviour`, репликации нет.** Сервер мутирует, клиент читает stale (`ShipController.cs:1887` в HUD). Нужен `NetworkVariable` или чтение только через телеметрию.
7. **Телеметрия 5 Гц full-snapshot.** `_telemetryState (:931)` + `UpdateTelemetryState (:1020-1143)`: `new CargoDetailDto[cap]` каждые 0.2 с на корабль + `cargoDetail[32]` с `FixedString64` в `NetworkVariable` каждые 0.2 с. Детали груза — в on-demand RPC, не бродкастом. Плюс `ShipTelemetryState.Equals` не сравнивает `lastUpdateServerTime` (мёртвое поле для дельты), а `position` — точно (`:138`) → позиция дёргается → полный снапшот уходит 5 Гц с каждого корабля. `GetHashCode:168-193` не включает `cargoDetail` (контракт с `Equals` хромает). Cap 32 (`:87`, `ShipController:1085`): `HeavyII maxSlots 30 + бонусы` обрежут хвост без индикации.
8. **`OnModuleChangedClientRpc:305-341` — SendTo.Everyone + применение через `slot.InstallModule` напрямую**, минуя `ShipModuleManager` (без energy/compat-проверок) → расхождение сервер/клиенты на граничных условиях. `NotifyErrorClientRpc:360` / `NotifySuccess:371` — `SendTo.Everyone` + фильтр `LocalClientId == target` вместо TargetRpc (лик + трафик); `NotifySuccess` пустой (`:373-375`); `// TODO: TargetRpc:350`. Хост выполняет ClientRpc повторно (`:329-331` — двойная работа + двойной `OnModuleChanged`).

**P1 — баги логики (верифицировано):**

9. **`MeziyModuleActivator` stale после рантайм-смены модулей.** `Initialize:90-134` строит `meziyStates` один раз, подписки на `OnModuleChanged` нет → вновь установленный мезий не работает до рестарта, снятый остаётся «активным». Ключ словаря — `moduleId (:84)`, не слот → два одинаковых модуля, второй игнорируется (`:113`). Топливо: `TryActivate:156` проверяет разовую `meziyFuelCost`, а `ConsumeFuelForActiveModules:277` списывает `meziyFuelCost * 2 * dt`/сек — единицы не сходятся.
10. **Застывшая поза визуалов.** `ShipPartShake:99-100` и `EngineThrusterVisual:130-131` — ранний выход при заглушенном двигателе без сброса трансформа (сброс только в `OnDisable:144`). Та же серия: `_shakeCurve.Evaluate (:130)` без нулл-гарда; `EngineThrusterVisual` — магическая `0.3f` в `SmoothDamp (:164)` вместо поля; NPC-fallback `angularVelocity.y (:148-152)` — мировая Y, не локальная ось (врёт при крене/тангаже); `InverseTransformPoint/TransformPoint + normalized` каждый кадр (`:180-212`); источник thrust в `ShipPartShake:105-113` — клиентский `_inputReader` vs fallback на скорость RB → сервер/клиент показывают разное.
11. **`ShipCargoConsoleWindow.RefreshInventory:397-419` считает слоты, не стаки** (`grouped[itemId]++`) → `entry.count` занижен → `OnStoreClicked:543` хронически «Недостаточно», `GetInvQtyMax:593-598` врёт. Плюс `HandleResult:623-633` делает `RefreshData()` сразу, телеметрия отстаёт 200мс+ → мигание stale. `Retrieve:306-319` (`ShipCargoServer`) — `AddItemDirect` по одному, O(n) на пачку 100; частичный успех откатывается через `RemoveItems`, чей фейл только логируется (`:447-450`). `_opTimestamps (:47)` растёт на клиента, очистки при дисконнекте нет. Нет `IsDocked`/дистанции гейта (в отличие от модулей) — грузить можно с любой точки карты (если задумано — зафиксировать в доке).
12. **Физика не под массу.** `ClampPitchAngle:1771,1776` — коррекция `overshoot * 10f` без `dt` и массы (у HeavyII 20000 — ничтожна). Торки `yawForce=25 / pitchForce=20 (:310-312)` против масс 8000–20000 — на 3 порядка меньше заявленных `~25°/s`, проверить в плей-тесте. `maxLiftForce (:1470)`: `скорость * масса * g` — каша размерностей, работает как произвольный кап, коммент врёт.
13. **Геттер с сайд-эффектом:** `ShipPersistentId` мутирует `[SerializeField]` в геттере (`ShipController.cs:86-96`). Геттер обязан быть чистым.
14. **`ShipCargoConsole.InstanceId:37-38`** — до спавна `_ship.NetworkObjectId == 0` → у всех неспавненных одинаковый `"0_cargo"`. `Awake` радиус синкается только в Editor-`OnValidate`.
15. **Детект игроков inconsistent:** `RepairManager.OnTriggerEnter/Exit:52-63` — только `Tag("Player")`, `ShipCargoConsole:87` — ещё и `CharacterController`; у `RepairManager` нет `OnDisable→Unregister` (у консоли есть `:101-104`). `_shopDatabase null` → окно фолбэчится на свой инспекторный (`Show:357`) — неоднозначный источник каталога. Эмодзи в `_interactHint:40` в обход локализации.
16. **`ShipCargoVisual.RefreshVisual:302`** клампит к `min(visible, zone)` — `cargoUsed` (слоты = `qty*slots`) трактуется как штуки ящиков. `SpawnOverflowIndicator:504-511` мутирует `r.materials` (копии-утечка); alpha на opaque не видна без transparent. `_shipNetId` в `Awake:196` + ленивый перерезолв (`:227-231`) — при респавне с новым netId подписка stale. `CalculateBoxPosition:415` предполагает `_spawnZone` на том же GO.
17. **`KeyRodInstanceWorld`:** `AutoSave` на каждую мутацию (`:458,514,531,546,566`) — полный JSON на трансфер; `instanceId` не персистится (`KeyRodInstanceSaveData:47-48`) — ссылки ломаются после рестарта; `CreateInstance:350-356` валидирует `itemId` через `InventoryWorld.Instance` — порядок инициализации (ключи раньше инвентаря → `-1`).
18. **`AltitudeCorridorData.GetStatus:58-73`** — асимметрия (снизу `Danger` сразу, сверху зазор `criticalUpperMargin` с `Warning`). Если задумано — дописать в коммент, сейчас выглядит багом.
19. **`ShipDamageConfig.GetMaxHull:97-107`** — `default → Medium` молча, без `Warn`. In-memory fallback (`CreateInstance` без `hideFlags`) течёт по объекту на отсутствие ассета.
20. **`ShipPositionServer`:** `TryLoadCorrectedPlayer:288-304` — файловый IO на пути размещения пилота (хитч). `ShiftWrapperByOffset` безопасен только пока каждый вызов читает свежий файл.

### Нестыковки в docs/Ships (10 шт — сверено чтением доков)

1. **Key OVERVIEW внутренне противоречив (P1-долг):** `§2.1/§2.4/§12` — `ShipKeyBinding/Server/ClientState/Toast, KeyRodInstanceBinding, ShipOwnershipRegistry` удалены, создание в `ShipController`; но `§1.1a рецепт + §1.1b troubleshooting + §6.1 + §7 + §8` описывают до-P1 (`Add Ship Key Binding`, `ShipKeyServer.RegisterBinding/PushBindingsRpc/CanBoard`, `[Ship_KeyServer]` в Bootstrap). По рецепту сейчас не собрать.
2. **Где создаётся instance:** `21 §2-3` — в `KeyRodInstanceBinding.OnNetworkSpawn` на `[KeyRod_*]`; `00_OVERVIEW §2.1 P1` — в `ShipController.OnNetworkSpawn`. Актуален P1-вариант, `21` устарел.
3. **`28` vs P1:** `28 §5/§7` предлагает выкинуть `KeyRodInstanceWorld` → `KeyRegistry/KeyInstance (itemId=123)`, 13ч; P1 сделал наоборот (World оставили SSOT, снесли обёртки). `28` не реализован, его метрики (`11 файлов, 5 правд`) невалидны после P1.
4. **Cargo «нужен/не нужен»:** `CARGO_DIAGNOSIS §2` фиксирует — `roadmap-integration:200, analysis-composite-ship:30, 00_COMPOSITE_SUMMARY:67` врут (`CargoSystem отсутствует/не делаем`); чинит `SHIP_REFACTOR P3` (`Trade v2 + T-CARGO-01..06`). Старые доки без поправки P3 читать нельзя.
5. **Требует ли карго ключ:** `Key 00_OVERVIEW §1.3` — «Загрузить товары ✓ (без ключа)» vs `CARGO_DIAGNOSIS §3.6/§6Q4` (guard нет = дыра) vs `CARGO_OWNERSHIP_DESIGN + P5` (guard добавлен, `NotOwner=36`). Таблица в `00_OVERVIEW` после P5 ложна.
6. **Куда ставить guard:** `SHIP_REFACTOR P5` — `ShipCargoServer + TradeWorld.TryLoad/Unload`; факт (`CARGO_OWNERSHIP_DESIGN` + P5) — `ShipCargoServer + MarketServer.RequestLoad/UnloadRpc`. TradeWorld-уровень не закрыт, только RPC.
7. **Engine vs Broken:** `ENGINE` — `OFF=падение, fuel==0=авто-OFF`; `DAMAGE 00 §4 + 02 §2` — `Broken: двигатель НЕ выключается, 10% скоростей, расход как обычно, мезий 100%`. Следствие (сломанный жрёт x10 топлива на дистанцию → потом падает по engine-правилу) нигде не зафиксировано, кросс-ссылки нет.
8. **L1 Visual done/не начат:** `customisation/00_SUMMARY (04.07)` — `D visualPrefab ❌`; `SHIP_REFACTOR P4` — `done`; `ITERATIONS 01-06.07` — `Module Visual Preview (Editor) done`; `Modul_system/01 (19.07)` про `visualPrefab` молчит. Вероятно сделан только Editor-preview, не runtime `ShipModuleVisualApplier`.
9. **Bootstrap трогать/не трогать:** `Modul_system/02 §1.3` — создать `[RepairManagerWindow]` в `BootstrapScene/DontDestroyOnLoad`; `ITERATIONS T-ENG02` — `BootstrapScene 🚫 НЕ ТРОГАТЬ, залочена`.
10. **Recall vs Persist/Dock:** `Modul_system/02 §6 Recall (22.07)` — `rb.position=pad + _frozenByNoPilot=false + TryModifyCredits`, без связи с `ShipPositionServer` persistence, `IsDocked/postUndockGrace` (Damage) и без описанной серверной проверки владения (код `ShipController.RecallShipToPadServerRpc:2381-2422` это подтверждает: владения/пада не проверяет).

### Positive Observations

- `ShipHull` — в целом грамотный сервер-авторитетный HP (`NetworkVariable`, сервер-гарды `:118,179-181`, грейс-периоды `:124-128`, `IDamageTarget`), `Update` нет.
- `ShipModuleCatalog/ShipRootReference/ShipComponentLocator` — правильное направление (единая точка поиска, уход от разрозненных `GetComponentInParent`).
- `ShipCargoServer` — самый аккуратный серверный файл: rate-limit, rollback, TargetRpc-ответы.
- `ShipFuelSystem` — чистый SRP, без `Update`/`Find`/`GetComponent`/аллокаций.
- `ShipCargoVisual` — пул ящиков, ленивый перерезолв netId для хоста.
- `ShipPositionServer` матч по стабильному `ShipPersistentId`, а не эфемерному netId — правильно.
- Доки `docs/Ships` — редкий уровень честности (диагнозы Mavis, `KNOWN_ISSUES`, явные «устарело») — по ним это ревью и сверялось.

### Required Changes (must-fix перед approve)

1. Мезию/ролл/дозаправку с серверной клавиатуры (`ShipController:1324,1352-1396,2128-2133`) → на RPC от владельца; удалить `GetCurrentPitchInput/GetCurrentYawInput`.
2. `SubmitShipInputRpc:1553` — `Clamp(-1,1)`; `AddPilot:1892` — ownership/authority-check.
3. `ShipModuleServer:227,388,464` — серверный прайс из `ModuleShopDatabase`, клиентские `sellCredits/cost` не доверять; `cost<=0` отклонить (закрыть фарм через `-cost`); добавить rate-limit + дистанцию до NPC.
4. `RecallShipToPadServerRpc:2381` — ownership-check, серверный `cost`, пад из реестра, списание до телепорта, синхронизация через `NetworkTransform`.
5. `ShipController:2033` — убрать `* dt` при `ForceMode.Force`.
6. `ShipFuelSystem.currentFuel` — репликация (`NetworkVariable`) или запрет прямых клиентских чтений.
7. `cargoDetail` из 5-Гц `NetworkVariable` — в on-demand RPC; починить `Equals/GetHashCode` (`ShipTelemetryState:132-193`).
8. `verboseLogging` дефолт `false` в обоих SO; удалить `repairCostCredits`.
9. Отписки `OnHullChanged/OnCargoChanged` + остановка корутин в `OnNetworkDespawn`; отписка `ShipTelemetryClientState` от корабля; `DontDestroyOnLoad`/уничтожение дубликата.
10. Визуалы — client-only guard + сброс позы при выключенном двигателе (`Contrail/PartShake/ThrusterVisual`).
11. `MeziyModuleActivator` — подписка на `OnModuleChanged` (stale-фикс) + ключ по слоту, не `moduleId`.
12. `GetActiveCorridor` — `sqrMagnitude`; доки `Key 00_OVERVIEW §1/§6-8`, таблица `§1.3`, `21`, `28`-метрики, `customisation`-статус P4 — обновить (несостыковки №1-3,5,8).

### Suggestions (nice-to-have)

- Разбить `ShipController` (God Object): вынести `ShipFlightPhysics`, `ShipDocking`, `ShipTelemetryPublisher`, `ShipRecallService`; `FixedUpdate` декомпозировать до методов ≤40 строк.
- Один SO тюнинга классов вместо 3 `switch`; generic-агрегатор множителей модулей; `FindSlot()/ValidateOwnership()`; generic `Default`-лоадер SO.
- `ModuleSlot.installedModule` — private + методы; `isOccupied/installedModuleId/currentPowerUsage` — `PascalCase`; инспекторные поля — `_camelCase`; зафиксировать в стандарте исключение «SO — public поля ок».
- `OnModuleChangedClientRpc/Notify*` — TargetRpc вместо `SendTo.Everyone` + фильтр.
- `ShipModuleCatalog.Reset()` при смене сцен/БД + error-лог на дубль `moduleId`; `ShipClassMapping` — `Warn` вместо молчаливого `Medium` + валидация полноты.
- `MeziyThrusterVisual`: материалы/частицы — пул, `emission.rate` в `SetIntensity`, проверка пропертей шейдера.
- `ShipCargoConsoleWindow`: считать стаки, а не слоты; дождаться телеметрии перед `RefreshData`; убрать `#if UNITY_EDITOR`-спам.
- `ShipPositionServer`: модули/карго/цвет в тот же снапшот (убрать split-brain); файловый IO с пути пилота — в кэш/асинхрон.
- Физика под массу: пересчитать `yaw/pitchForce` и `ClampPitchAngle` с `dt` и инерцией; `IsGrounded` — маска + длина под размер; `maxLiftForce` — починить размерность/коммент.
- Доки: кросс-ссылка Engine↔Broken (x10 расход), зафиксировать «груз с любой точки — задумано/нет», `Recall` — связь с Persist/Dock/Ownership.

### Verdict: CHANGES REQUIRED

Пункты P0 №1–5 — читы/десинхрон, а не вкусовщина: серверный ввод с хост-клавиатуры, отсутствие Clamp, client-authoritative цены/стоимость (`sellCredits`, `cost`, `Recall cost/padPosition`). Плюс фундамент: God Object `ShipController`, stale мезий после ребилда, 5-Гц full-snapshot телеметрии, утечки подписок. После P0 — плей-тест: install/sell с подменённым `sellCredits`, F8/rehost (модули и ящики на месте), `cargoUsed/cargoMax` на Heavy с расширителями, перегрев мезия после переустановки в доке, Compile → 0 errors.

---
*Файл-отчёт: `docs/Ships/SHIP_CODE_REVIEW_2026-09-18.md`. Проверки после правок: Compile (Console → 0 errors), Test Runner, Manual по Verdict-абзацу.*
