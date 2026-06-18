# Cargo System — план рефакторинга (2026-06-17)

**Автор:** Mavis (Mavis)
**Статус:** 📐 План реализации (после диагноза [CARGO_DIAGNOSIS_2026-06-17.md](CARGO_DIAGNOSIS_2026-06-17.md))
**Scope:** Интеграция ShipController ↔ Trade v2 Cargo, удаление legacy `ProjectC.Player.CargoSystem`
**Зависит от:**
- `unity-v2-subsystem-migration` skill (канонический паттерн)
- `project-c-netcode-patterns` skill §24-26
- `project-c-composite-object-architecture` skill (ShipController как часть composite ship)

> **Принцип:** Cargo — подсистема Trade, не Ship. Никаких параллельных хранилищ. Один источник истины — `TradeWorld._cargoCache[shipId]`. ShipController становится **тонким клиентом** для серверного состояния.

---

## Обновления плана (2026-06-17, после ответов пользователя)

Все 5 открытых вопросов закрыты. План приведён в соответствие с v2-стеком:

| Изменение | Было | Стало |
|---|---|---|
| Источник `ShipClass` | Новый компонент `ShipClassMarker` (лишний) | Маппинг `ShipFlightClass → ShipClass` через `ShipClassMappingConfig` SO (D5/D5a) |
| Обновление `_serverCargoPenalty` | Open question Q2 | Event-driven через `TradeWorld.OnCargoChanged` (D10) — как v2: `MarketTimeService`, `ContractServer` |
| Параметры leak/fragile | Open question Q3 (хардкод defaults) | `ShipCollisionDamageConfig` SO с default + fallback (D9) |
| Ownership | Open question Q4 | OUT OF SCOPE (отдельная задача) |
| `OnCollisionEnter` | Open question Q5 (отдельный компонент) | Inline в `ShipController` (D7) |

**Новые файлы:** 3 .cs + 2 .asset. **Модификации:** 4 .cs. **Удаления:** 1 .cs (`CargoSystem`). **Оценка:** ~10.5 ч.

---

## 0. Решения (фиксируем)

| # | Решение | Обоснование |
|---|---|---|
| **D1** | Cargo остаётся в `Trade/` подсистеме | Уже реализовано на 70%, persistence + DTO работают. Писать параллельную Cargo-подсистему = костыль |
| **D2** | Старый `ProjectC.Player.CargoSystem` (MonoBehaviour) — **УДАЛЯЕМ** | Дубликат `CargoData` + путаница namespace. Никогда не тестировался |
| **D3** | Новая папка: `Assets/_Project/Scripts/Ship/Cargo/` НЕ создаётся | Cargo — не Ship-подсистема. ShipController ссылается на `ProjectC.Trade.Core.CargoData` / `ShipClassLimits` |
| **D4** | `ShipClass` (cargo) **остаётся отдельным enum'ом** от `ShipFlightClass` (физика) | Разные ответственности: cargo = грузоподъёмность, flight = маневренность. GDD разделяет |
| **D5** | `ShipClass` берётся из `ShipController.shipFlightClass` (УЖЕ ЕСТЬ, см. `ShipController.cs:35`) | Ответ пользователя Q1: в `ShipController` уже есть выбор класса. Делаем маппинг `ShipFlightClass → ShipClass` (Light→Light, Medium→Medium, Heavy→HeavyI, HeavyII→HeavyII). `ShipClassMarker` НЕ создаём — лишний компонент |
| **D5a** | Маппинг `FlightClass → CargoClass` — **ScriptableObject `ShipClassMappingConfig`** в `Assets/_Project/Data/Ship/`. Default значения (Light→Light и т.д.), но правится в инспекторе | Ответ Q3: «не в хардкод, возможность править в инспекторе». Не enum-словарь, а SO с 4 маппингами. Переживает ребут, A/B-тестится, без перекомпиляции |
| **D6** | Физика скорости читается с сервера через `NetworkVariable<float> _serverCargoPenalty` | Избегаем рассинхрона клиент/сервер. NetworkVariable — канон v2 |
| **D7** | Столкновения (leak/fragile) — на СЕРВЕРЕ, через `ShipController.OnCollisionEnter` (inline в ShipController) | Cargo state — server-authoritative. Event-driven обновление `_serverCargoPenalty` через `TradeWorld.OnCargoChanged` (как у нас в v2 делает `MarketTimeService`) |
| **D8** | UI трюма — **ОТДЕЛЬНАЯ задача** (T-Cargo-UI), не в этом плане | Сначала интеграция данных, потом UI. Не смешивать scope |
| **D9** | Параметры leak/fragile (порог энергии, % утечки) — **ScriptableObject `ShipCollisionDamageConfig`** в `Assets/_Project/Data/Ship/`. Default из старого кода (5%×10% leak, 10% fragile), правится в инспекторе | Ответ Q3: «не в хардкод, дать править в инспекторе». Этап 4 |
| **D10** | Event-driven обновление `_serverCargoPenalty` через `TradeWorld.OnCargoChanged` | Ответ Q2: «как работает наш рынок и остальное, без костылей, v2 значит v2». В v2 событийная модель (MarketTimeService, ContractServer) |
| **D11** | **Per-instance базовые лимиты трюма** в `ShipController` (НЕ статический switch в `ShipClassLimits`). Поля: `baseMaxCargoSlots/Weight/Volume/PenaltyFactor` (inspector-editable) | Решение Q-06: «лёгкий с большим хранилищем» — per-instance. Модули stackable flat, penaltyReduction отрицательный = уменьшает штраф. `ShipClassLimits` остаётся **fallback по умолчанию**, если на корабле не выставлено |
| **D12** | **`ShipCargoRegistry`** — статический `Dictionary<ulong, ShipController>`. `OnNetworkSpawn` → register, `OnNetworkDespawn` → unregister | Мост: TradeWorld (POCO) вызывает per-instance лимиты без хранения ссылки на GameObject. `MarketZone` использует `sc.GetEffectiveCargoLimits()` напрямую (уже имеет sc) |
| **D13** | **Cargo-бонусы** в `ShipModule` (flat): `cargoSlotsBonus/WeightBonus/VolumeBonus/PenaltyReduction`. Stackable. Без cooldown | Q-06.2: «бонус — flat, penalty factor уменьшается, без кулдауна». `ShipModuleManager.GetCargoXxxBonus()` суммирует со всех занятых слотов |

---

## Дополнение T-CARGO-06 (per-instance лимиты + модули)

Закрыто 2026-06-17. Дополнение к основному плану (Этапы 1-5).

**Что создано (поверх D1-D10):**
- `Assets/_Project/Scripts/Ship/ShipCargoRegistry.cs` — static registry по NetworkObjectId
- `Assets/_Project/Data/Ship/Modules/MODULE_CARGO_BAY_01.asset` — тестовый модуль (Utility tier=2, +6 слотов, +50кг, +2м³, -0.02 penalty)

**Что модифицировано:**
- `ShipModule.cs` — 4 новых поля cargo-бонусов (flat)
- `ShipModuleManager.cs` — 4 новых метода `GetCargoSlotsBonus/WeightBonus/VolumeBonus/PenaltyReduction()`
- `ShipController.cs` — 4 новых base-поля + `GetEffectiveCargoLimits()` + регистрация в registry
- `TradeWorld.cs` — `TryLoadToShip` (pre-check через registry) + `GetSpeedPenalty` (через registry)
- `MarketZone.cs` — `sc.GetEffectiveCargoLimits()` вместо `ShipClassLimits.Get(cls)`

**Архитектурный принцип:** Cargo лимиты живут **per-instance на ShipController** (D11), модули stackable flat (D13), `ShipCargoRegistry` — мост к серверной POCO-логике Trade (D12). Никаких хардкодных switch'ей — все базовые значения правятся в инспекторе.

**Что увидишь в Play Mode:**

| Действие | Результат |
|---|---|
| `Ship_Light_root` в Inspector → `Cargo` секция → `Base Max Cargo Slots: 4 → 10` | После Save: можно загрузить 10 слотов вместо 4 (MarketWindow покажет max=10) |
| Установить `MODULE_CARGO_BAY_01` в Utility-слот корабля | +6 слотов, +50кг, +2м³, -0.02 penalty → maxSlots=10, penaltyFactor=0.03 |
| Загрузить antigrav_ingot × 20 (было слишком много) | Поместится, penalty = 1.0 - 0.5×0.03 = 0.985 |

**Файлы документации:**
- `docs/Ships/cargo_system/CARGO_DIAGNOSIS_2026-06-17.md` — обновлён (финальное состояние T-CARGO-01..05)
- `docs/Ships/cargo_system/CARGO_REFACTOR_PLAN_2026-06-17.md` — этот файл (D11-D13 + новая секция выше)

---

## 1. Этапы (что и в каком порядке)

### Этап 0: Подготовка (без правок кода)

**Задачи:**
- ✅ Диагноз зафиксирован: [CARGO_DIAGNOSIS_2026-06-17.md](CARGO_DIAGNOSIS_2026-06-17.md)
- ✅ Все 5 открытых вопросов закрыты (см. §3 ниже)
- ⏳ Прочитать GDD_25 секции 4.1, 4.3, 4.4 целиком (для точных формул)

**Выход:** подтверждённый scope, никаких сюрпризов в середине. Можно начинать Этап 1.

---

### Этап 1: `ShipClassMappingConfig` — маппинг FlightClass → CargoClass

**Цель:** дать проекту конфигурируемый маппинг `ShipFlightClass → ShipClass` (для лимитов cargo), **ДО** удаления `CargoSystem`. Не плодим лишний компонент.

**Что создаём:**

| Файл | Тип | Содержимое |
|---|---|---|
| `Assets/_Project/Scripts/Ship/ShipClassMapping.cs` | `[Serializable] struct`, `namespace ProjectC.Ship` | `public ShipFlightClass flightClass; public ProjectC.Trade.ShipClass cargoClass;` — пара маппинга |
| `Assets/_Project/Scripts/Ship/ShipClassMappingConfig.cs` | ScriptableObject, `namespace ProjectC.Ship` | `[CreateAssetMenu(fileName = "ShipClassMapping", menuName = "ProjectC/Ship/Class Mapping")]` + `public List<ShipClassMapping> mappings = new()` (4 записи по умолчанию) + `public ProjectC.Trade.ShipClass Resolve(ShipFlightClass flight)` |
| `Assets/_Project/Data/Ship/ShipClassMapping.asset` | Экземпляр SO | Default: Light→Light, Medium→Medium, Heavy→HeavyI, HeavyII→HeavyII. Создаётся через `[CreateAssetMenu]` или вручную (пользователь) |
| `Assets/_Project/Scripts/Ship/ShipClassMappingConfig.cs` (default initializer) | static class, `namespace ProjectC.Ship` | `public static ShipClassMappingConfig Default { get; }` — `Resources.Load<ShipClassMappingConfig>("ShipClassMapping")` + fallback на hardcoded defaults если asset не найден. Fallback логирует warning |

**Что меняем:**

| Файл | Что |
|---|---|
| `Assets/_Project/Trade/Scripts/Network/MarketZone.cs:351` | `ShipClass cls = cargoComp != null ? cargoComp.shipClass : ShipClass.Light;` → `var flightClass = ship.GetComponent<ShipController>()?.ShipFlightClass; ShipClass cls = ShipClassMappingConfig.Default.Resolve(flightClass) ?? ShipClass.Light;` (нужен публичный геттер в ShipController — добавить) |
| `Assets/_Project/Scripts/Player/ShipController.cs:35` | Поле `shipFlightClass` уже есть. **Добавить публичный геттер:** `public ShipFlightClass ShipFlightClass => shipFlightClass;` (минимальная правка) |

**Что НЕ трогаем:**
- `ShipController.cs` — кроме одного геттера
- `CargoSystem.cs` — пока нужен для обратной совместимости (MarketZone временно может использовать оба источника)
- `ShipClassLimits` — пока в `CargoData.cs:138` (static class, оставляем)

**Тест:** Unity Editor — создать `ShipClassMapping.asset`, поставить Heavy→HeavyII (нестандартный маппинг), проверить `MarketZone.GetNearbyShips()` возвращает `HeavyII` для Heavy корабля.

**Verify:**
- `mavis mcp call unityMCP refresh_unity '{"mode":"force","compile":"request","wait_for_ready":true}'`
- `mavis mcp call unityMCP read_console '{"action":"get","types":["error","warning"],"count":"20"}'` — должно быть 0 errors
- Создать asset через `[CreateAssetMenu]` или `mavis mcp call unityMCP manage_asset ...`
- Проверить `Resources.Load` в PlayMode

**Чек-лист "готово":**
- [ ] `ShipClassMapping.cs` + `ShipClassMappingConfig.cs` созданы
- [ ] `ShipClassMapping.asset` создан в `Assets/_Project/Data/Ship/` (default значения)
- [ ] `ShipController.ShipFlightClass` геттер добавлен
- [ ] `MarketZone.cs:351` использует маппинг, не `cargoComp`
- [ ] `cargoComp` остался как fallback (не сломался, не удалён)
- [ ] Compile OK, 0 errors
- [ ] Ручной тест: сменить маппинг в инспекторе → результат меняется в `MarketZone`

---

### Этап 2: Серверный хук `ShipController` ↔ `TradeWorld`

**Цель:** при `OnNetworkSpawn` корабль регистрируется в `TradeWorld._cargoCache` (через `GetOrLoadCargo`); при `OnNetworkDespawn` — `InvalidateCargo`.

**Что создаём:**

| Файл | Тип | Содержимое |
|---|---|---|
| `Assets/_Project/Scripts/Ship/ShipCargoLink.cs` | MonoBehaviour, `namespace ProjectC.Ship` | NetworkBehaviour-подобный хелпер? **НЕТ** — лучше просто код в `ShipController`. Чтобы не плодить компоненты, делаем inline в ShipController. **УДАЛЕНО из плана** |

**Что меняем:**

| Файл | Что |
|---|---|
| `Assets/_Project/Scripts/Player/ShipController.cs:171-211` (`Awake`/`OnDestroy`) | Добавить серверный `OnNetworkSpawn` / `OnNetworkDespawn` override. На сервере: `if (IsServer) TradeWorld.Instance?.GetOrLoadCargo(NetworkObjectId, _shipClassMarker.ShipClass);`. На despawn: `TradeWorld.Instance?.InvalidateCargo(NetworkObjectId);` |
| `Assets/_Project/Scripts/Player/ShipController.cs:97` | `[SerializeField] private ShipClassMarker classMarker;` (вместо `cargoSystem`). Migration: scene auto-resolve при отсутствии ссылки |

**Важно:**
- **Защита от race condition** (NGO не гарантирует порядок `OnNetworkSpawn` между `MarketServer` и `ShipController`): если `TradeWorld.Instance == null` — отложить регистрацию через корутину с таймаутом 5с (см. `ExchangeServer.cs:76-90` как образец).
- Только на сервере: `if (!IsServer) return;`

**Тест:** Start Host → 1 корабль в зоне → `TradeWorld._cargoCache` содержит запись → при disconnect корабля запись инвалидируется.

**Verify:**
- В `ShipController.OnNetworkSpawn` добавить `Debug.Log($"[ShipController] Register cargo shipId={NetworkObjectId} class={_shipClassMarker.ShipClass}");`
- В `OnNetworkDespawn` — `Debug.Log($"[ShipController] Invalidate cargo shipId={NetworkObjectId}");`
- Проверить в Console после `StartHost` + `StopHost`

**Чек-лист "готово":**
- [ ] `ShipController` имеет `classMarker` (вместо `cargoSystem`)
- [ ] `OnNetworkSpawn` (сервер) регистрирует cargo
- [ ] `OnNetworkDespawn` (сервер) инвалидирует
- [ ] Race protection через корутину
- [ ] Compile OK
- [ ] Console логи появляются

---

### Этап 3: Физика скорости от серверного cargo

**Цель:** `ShipController.AddForce` умножает на `cargoPenalty` с сервера, не с локального `CargoSystem`.

**Что создаём (код):**

| Файл | Тип | Содержимое |
|---|---|---|
| `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` (правка) | +public `event Action<ulong> OnCargoChanged` | Сигнал "груз корабля изменился" — для всех подписчиков (ShipController, UI) |
| `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` (правка) | +public `float GetSpeedPenalty(ulong shipId, ProjectC.Trade.ShipClass cls)` | Серверная формула, перенесённая из старого `CargoSystem.GetSpeedPenalty()`. Использует `ShipClassLimits.Get(cls)` + `CargoData.ComputeTotalWeight` |

**Подписка в `OnCargoChanged`:** вызывается из `TryAdd`, `TryRemove`, `TryLoadToShip`, `TryUnloadFromShip`, `InvalidateCargo` (последний — с проверкой, что кэш реально изменился). Сигнатура: `void Handler(ulong shipNetworkObjectId)`.

**Что меняем в `ShipController`:**

| Файл | Что |
|---|---|
| `Assets/_Project/Scripts/Player/ShipController.cs` (новое поле) | `private readonly NetworkVariable<float> _serverCargoPenalty = new NetworkVariable<float>(1.0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);` |
| `Assets/_Project/Scripts/Player/ShipController.cs` (OnNetworkSpawn) | После регистрации cargo (Этап 2): подписаться на `TradeWorld.Instance.OnCargoChanged += RecalculateCargoPenalty` |
| `Assets/_Project/Scripts/Player/ShipController.cs` (OnNetworkDespawn) | Отписаться: `if (TradeWorld.Instance != null) TradeWorld.Instance.OnCargoChanged -= RecalculateCargoPenalty;` |
| `Assets/_Project/Scripts/Player/ShipController.cs` (новый метод) | `private void RecalculateCargoPenalty(ulong shipId) { if (!IsServer || shipId != NetworkObjectId) return; _serverCargoPenalty.Value = TradeWorld.Instance.GetSpeedPenalty(NetworkObjectId, _cargoClass); }` |
| `Assets/_Project/Scripts/Player/ShipController.cs:97` | **Удалить** `[SerializeField] private ProjectC.Player.CargoSystem cargoSystem;` |
| `Assets/_Project/Scripts/Player/ShipController.cs:631-642` (ApplyThrust) | Удалить `cargoPenalty = cargoSystem?.GetSpeedPenalty() ?? 1.0f;`. Использовать `_serverCargoPenalty.Value` |

**Важно:**
- `OnCargoChanged` вызывается ПОСЛЕ мутации `_cargoCache[shipId]` (атомарность не нужна — event в Unity main thread, вызывающий код не увидит промежуточного состояния).
- `GetSpeedPenalty` — **единственное место** с формулой. `ShipClassLimits` пока остаётся static class, но в Этапе 5 можно вынести в `ShipClassConfig` SO (см. §6 backlog).
- NetworkVariable требует инициализации до `OnNetworkSpawn` — поле readonly, OK.
- Старый `CargoSystem.GetSpeedPenalty()` — помечаем `[Obsolete]` пока, удаляем в Этапе 5.

**Тест:** загрузить груз на корабль через рынок → cargoPenalty сразу меняется → скорость падает (визуально или в `ShipDebugHUD` добавить поле `Cargo Penalty`).

**Verify:**
- В `RecalculateCargoPenalty` добавить `Debug.Log($"[ShipController] cargoPenalty={val:F2} shipId={NetworkObjectId}");`
- В `TradeWorld` добавить `Debug.Log($"[TradeWorld] OnCargoChanged shipId={shipId}");` в месте вызова event
- Load item через MarketWindow → должны появиться оба лога

**Чек-лист "готово":**
- [ ] `TradeWorld.OnCargoChanged` event объявлен и вызывается из 5 мест
- [ ] `TradeWorld.GetSpeedPenalty(shipId, cls)` реализован
- [ ] `_serverCargoPenalty` NetworkVariable в ShipController
- [ ] ShipController подписывается/отписывается в OnNetworkSpawn/Despawn
- [ ] `ApplyThrust` использует `_serverCargoPenalty.Value`
- [ ] `cargoSystem` поле **УДАЛЕНО** из ShipController
- [ ] Старый `CargoSystem.GetSpeedPenalty()` помечен `[Obsolete]`
- [ ] Compile OK (warning о obsolete допустим)
- [ ] Тест с реальной загрузкой груза пройден

---

### Этап 4: Столкновения (leak / fragile)

**Цель:** при столкновении сервер проверяет опасный/хрупкий груз, удаляет часть с шансом. Параметры — в SO, не в хардкоде.

**Что создаём:**

| Файл | Тип | Содержимое |
|---|---|---|
| `Assets/_Project/Scripts/Ship/ShipCollisionDamageConfig.cs` | ScriptableObject, `namespace ProjectC.Ship` | `[CreateAssetMenu(fileName = "ShipCollisionDamage", menuName = "ProjectC/Ship/Collision Damage")]` + поля: `float impactEnergyThreshold = 5f;` (минимальная энергия для срабатывания), `float leakChancePerDangerous = 0.05f;` (5%), `float leakPercentOfStack = 0.1f;` (10% утечки), `float fragileChancePerItem = 0.10f;` (10%), `bool verboseLogging = true;` |
| `Assets/_Project/Data/Ship/ShipCollisionDamage.asset` | Экземпляр SO | Default значения как в старом коде. Создаётся через `[CreateAssetMenu]` или MCP |
| `Assets/_Project/Scripts/Ship/ShipCollisionDamageConfig.cs` (default loader) | static method | `public static ShipCollisionDamageConfig Default` через `Resources.Load<ShipCollisionDamageConfig>("ShipCollisionDamage")` + fallback на hardcoded defaults (с warning) |

**Что меняем:**

| Файл | Что |
|---|---|
| `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs` (новый метод) | `public bool TryDamageCargo(ulong shipId, ProjectC.Trade.ShipClass cls, float impactEnergy, out string failReason, out int leakedAmount, out int damagedAmount)` — единая точка мутации cargo от столкновения. Использует `ShipCollisionDamageConfig.Default` для параметров. Возвращает что именно произошло (leak/fragile) для логов |
| `Assets/_Project/Trade/Scripts/Core/CargoData.cs` (опц.) | +`public IEnumerable<CargoData> GetDangerousItems()` / `GetFragileItems()` — удобные итераторы (если TradeWorld.TryDamageCargo нужны; иначе inline) |
| `Assets/_Project/Scripts/Player/ShipController.cs` (новый метод) | `private void OnCollisionEnter(Collision col) { if (!IsServer) return; float energy = col.impulse.magnitude; var (cls, ...) = _resolvedCargoClass; if (TradeWorld.Instance == null) return; if (TradeWorld.Instance.TryDamageCargo(NetworkObjectId, cls, energy, out _, out var leaked, out var damaged)) { Debug.Log($"[ShipController] collision energy={energy:F1} leak={leaked} damaged={damaged}"); } }` |

**Важно:**
- `OnCollisionEnter` — **Unity callback**, работает когда Rigidbody сталкивается. Уже есть на ShipController (проверить — если нет, добавить).
- Только на сервере (`IsServer`) — клиент не мутирует cargo.
- `ShipController` хранит кэш `_resolvedCargoClass` (вычисляется в `OnNetworkSpawn` через `ShipClassMappingConfig.Default.Resolve(shipFlightClass)`), чтобы не дёргать Resolve каждый collision.
- Старый `CargoSystem.CheckLeak/CheckFragile` — помечаем `[Obsolete]`, удаляем в Этапе 5.
- **Energy threshold** — если в конфиге = 5f, мелкие столкновения игнорируются. Это и есть то, что делает систему настраиваемой, а не "рандом 5% при любом контакте".

**Тест:** спавнить корабль с dangerous item, столкнуть с чем-то (использовать `Debug.Break` или `Physics.SphereCast` для force-collision) → лог `[ShipController] collision energy=X leak=Y damaged=Z`, cargo снимется, `_serverCargoPenalty` обновится через `OnCargoChanged` → скорость упадёт.

**Verify:**
- Debug-логи в `ShipController.OnCollisionEnter` и `TradeWorld.TryDamageCargo`
- В `TradeWorld.TryDamageCargo` — в конце вызвать `OnCargoChanged?.Invoke(shipId)` чтобы penalty обновился

**Чек-лист "готово":**
- [ ] `ShipCollisionDamageConfig.cs` SO создан
- [ ] `ShipCollisionDamage.asset` создан с default значениями
- [ ] `TradeWorld.TryDamageCargo` реализован + вызывает `OnCargoChanged` в конце
- [ ] `ShipController.OnCollisionEnter` (server) дёргает `TryDamageCargo`
- [ ] Старый `CargoSystem.CheckLeak/CheckFragile` помечены `[Obsolete]`
- [ ] Compile OK
- [ ] Тест: столкновение с dangerous item → лог + cargo изменён

---

### Этап 5: Удаление `ProjectC.Player.CargoSystem`

**Только после того, как все 4 предыдущих этапа работают.**

**Что делаем:**

1. **Чистим сцену:** открыть `WorldScene_0_0.unity` через MCP `manage_scene` → найти 3 GameObject'а с компонентом `ProjectC.Player.CargoSystem` → удалить компонент (или весь GO если других компонентов нет). **Пользователь делает сам** (мы не редактируем сцены через MCP без явного запроса — см. AGENTS.md).
2. **Удаляем файл:** `git rm Assets/_Project/Trade/Scripts/CargoSystem.cs` (пользователь коммитит)
3. **Чистим `CreateTestShip.cs`:** строки, упоминающие `CargoSystem` → удалить
4. **Чистим `ShipController.cs:97`:** поле `cargoSystem` уже удалено в Этапе 3, проверить
5. **Чистим доки:**
   - `docs/Ships/roadmap-integration.md:200` → "Не делаем CargoSystem" → УДАЛИТЬ (Cargo есть)
   - `docs/Ships/analysis-composite-ship.md:30` → "❌ Отсутствует" → "✅ Готово (Trade v2)"
   - `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md:67` → "❌ Не существует" → "✅ Готово (Trade v2)"
   - `docs/Ships/legacy/HOWTO_CREATE_SHIP.md:97` → убрать строку "Cargo System"
6. **Обновляем MMO_Development_Plan.md** — добавить секцию "Cargo (Trade Routes)" в ЧТО НОВОГО

**Verify:**
- `grep -r "ProjectC.Player.CargoSystem" Assets/` → пусто
- `grep -r "ProjectC.Player.CargoSystem" docs/` → пусто
- `mavis mcp call unityMCP read_console` после recompile → 0 errors
- Загрузка/выгрузка груза работает
- Скорость от груза работает
- Столкновения работают

**Чек-лист "готово":**
- [ ] `CargoSystem.cs` удалён
- [ ] Сцена `WorldScene_0_0.unity` без broken references
- [ ] Все доки обновлены
- [ ] MMO_Development_Plan обновлён
- [ ] 0 compile errors
- [ ] Полный end-to-end тест (load → fly → collision → unload) пройден

---

## 2. Оценка трудозатрат

| Этап | Часы | Зависимости | Блокирует |
|---|---|---|---|
| 0. Подготовка | 0.5 | — | — |
| 1. `ShipClassMappingConfig` (SO + маппинг) | 1.5 | — (D5/D5a закрыты) | Этап 2, 3, 4 |
| 2. Серверный хук `ShipController` ↔ `TradeWorld` | 2 | Этап 1 | Этап 3 |
| 3. `TradeWorld.OnCargoChanged` + `GetSpeedPenalty` + физика | 2.5 | Этап 2 | Этап 4 |
| 4. `ShipCollisionDamageConfig` + `TradeWorld.TryDamageCargo` + `OnCollisionEnter` | 2.5 | Этап 3 | Этап 5 |
| 5. Удаление `ProjectC.Player.CargoSystem` + чистка доков | 1.5 | Все предыдущие | — |
| **ИТОГО** | **~10.5 ч** | | |

Распределение по типу работы:
- Код (Этапы 1-4): ~8.5 ч
- Чистка legacy + доки (Этап 5): ~1.5 ч
- Подготовка: ~0.5 ч

Сравнение: рефакторинг старой `CargoSystem` с сохранением `ProjectC.Player.ShipClass` namespace занял бы ~12-14ч и оставил бы ту же путаницу. Чистая перезапись = **экономия + ясность + v2-события**.

---

## 3. Критические решения (все закрыты 2026-06-17)

| # | Вопрос | Решение (D5–D10) |
|---|---|---|
| **Q1** | Где `ShipController` берёт свой `ShipClass`? | D5: из `shipFlightClass` (уже есть в ShipController.cs:35) + D5a: маппинг через `ShipClassMappingConfig` SO в инспекторе |
| **Q2** | Event-driven или polling для обновления `_serverCargoPenalty`? | D10: event-driven через `TradeWorld.OnCargoChanged` (как v2: `MarketTimeService`, `ContractServer`) |
| **Q3** | GDD_25 §4.3 — конкретные цифры? | D9: `ShipCollisionDamageConfig` SO с default значениями (5%/10%/10%), правится в инспекторе. Fallback на hardcoded если asset не найден |
| **Q4** | Ownership: кто может грузить в чужой корабль? | **OUT OF SCOPE** — отдельная задача (security phase). Сейчас как есть (любой в зоне) |
| **Q5** | Расположение `OnCollisionEnter`? | Inline в `ShipController` (D7) — лишний компонент не нужен, 1 метод не стоит отдельного файла |

---

## 4. Что НЕ делаем в этом плане (явно out of scope)

- ❌ UI трюма (T-Cargo-UI — отдельный план)
- ❌ Ownership / security модель (отдельная задача)
- ❌ Multi-ship stacking (Phase 4+, GDD_25)
- ❌ Cargo decay / spoilage (Phase 4+)
- ❌ NPC traders cargo (уже работает в TradeWorld)
- ❌ Migration старых сохранений (нет сохранений — CargoSystem не тестировалась)

---

## 5. Риски и митигация

| Риск | Митигация |
|---|---|
| Удаление `cargoSystem` поля из ShipController сломает сцену (3 broken references) | В Этапе 3 — оставляем `[FormerlySerializedAs("cargoSystem")]` если возможно, иначе сцену чинит пользователь через MCP `manage_scene` |
| NetworkVariable не обновляется у клиента | Проверить `OnNetworkSpawn` на клиенте — NetworkVariable синхронизируется автоматически |
| Race condition `TradeWorld.Instance == null` | Корутина с таймаутом 5с, как в `ExchangeServer` |
| Регрессия физики (корабль не летит) | Сохранить `cargoPenalty=1.0f` как default — без груза поведение не меняется |
| Потеря данных cargo при disconnect | Repository.SetCargo вызывается в TradeWorld (уже есть) — проверить persist при despawn |

---

## 6. Verification commands (для пользователя)

После каждого этапа:

```bash
# Compile
mavis mcp call unityMCP refresh_unity '{"mode":"force","compile":"request","wait_for_ready":true}'
mavis mcp call unityMCP read_console '{"action":"get","types":["error"],"count":"20"}'
# Ожидаем: 0 errors

# После Этапа 5 — финальная проверка
grep -r "ProjectC.Player.CargoSystem" Assets/ docs/ 2>/dev/null
# Ожидаем: пусто

# Тест в Play Mode
# 1. Open BootstrapScene
# 2. Start Host
# 3. Place ship with ShipClassMarker (HeavyI) в зоне рынка
# 4. Open MarketWindow → выбрать корабль → Load 5 mesium_canister
# 5. Console: должны быть логи регистрации/обновления cargoPenalty
# 6. Fly to a platform → столкновение → если dangerous — лог leak
```

---

## 7. Структура файлов (после плана)

```
Assets/_Project/
├── Scripts/Ship/                          ← новые компоненты (D5a, D9)
│   ├── ShipClassMapping.cs                (Этап 1) NEW — [Serializable] struct
│   ├── ShipClassMappingConfig.cs          (Этап 1) NEW — SO + default loader
│   └── ShipCollisionDamageConfig.cs       (Этап 4) NEW — SO + default loader
├── Data/Ship/                             ← SO assets
│   ├── ShipClassMapping.asset             (Этап 1) NEW — default маппинг
│   └── ShipCollisionDamage.asset          (Этап 4) NEW — default параметры
├── Scripts/Player/
│   └── ShipController.cs                  ← правки (Этапы 1-4) MODIFIED
│       + геттер ShipFlightClass (Этап 1)
│       + OnNetworkSpawn регистрация cargo (Этап 2)
│       + OnNetworkDespawn отписка (Этап 2)
│       + _serverCargoPenalty NetworkVariable (Этап 3)
│       + RecalculateCargoPenalty (Этап 3)
│       - cargoSystem поле (Этап 3)
│       + OnCollisionEnter (Этап 4)
└── Trade/Scripts/
    ├── Core/
    │   ├── CargoData.cs                   (Этап 4) +GetDangerous/Fragile MODIFIED
    │   └── TradeWorld.cs                  (Этапы 3, 4) +OnCargoChanged, +GetSpeedPenalty, +TryDamageCargo MODIFIED
    ├── Network/
    │   └── MarketZone.cs                  (Этап 1) использовать ShipClassMappingConfig MODIFIED
    └── Scripts/
        └── CargoSystem.cs                 (Этап 5) УДАЛЁН
```

**Итого:**
- Создаётся: 3 новых .cs + 2 .asset
- Модифицируется: 4 .cs (`ShipController`, `TradeWorld`, `CargoData`, `MarketZone`)
- Удаляется: 1 .cs (`CargoSystem`)
- Чистятся 4 legacy-дока

Никаких новых namespace'ов (используем `ProjectC.Ship` для новых компонентов — уже есть в проекте), никаких новых папок кроме `Assets/_Project/Data/Ship/`.

---

## 8. Чек-лист "весь план готов"

- [ ] Все 5 открытых вопросов закрыты
- [ ] Этап 1 — ShipClassMarker работает
- [ ] Этап 2 — серверная регистрация работает
- [ ] Этап 3 — физика от сервера работает
- [ ] Этап 4 — столкновения работают
- [ ] Этап 5 — `CargoSystem.cs` удалён, доки обновлены
- [ ] End-to-end тест (load → fly → collide → unload) пройден
- [ ] 0 compile errors, 0 broken references в сцене
- [ ] MMO_Development_Plan обновлён
- [ ] Пользователь подтвердил «хорошо, можно отчитаться»

---

## 9. Связанные документы

- [CARGO_DIAGNOSIS_2026-06-17.md](CARGO_DIAGNOSIS_2026-06-17.md) — диагноз
- `docs/Ships/roadmap-integration.md` — обновить после Этапа 5
- `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` — обновить после Этапа 5
- `docs/Ships/analysis-composite-ship.md` — обновить после Этапа 5
- `docs/Ships/legacy/HOWTO_CREATE_SHIP.md` — обновить после Этапа 5
- `docs/MMO_Development_Plan.md` — обновить после Этапа 5
- `docs/gdd/GDD_25_Trade_Routes.md` §4.1, §4.3, §4.4 — дизайн-источник
- `unity-v2-subsystem-migration` skill — паттерн
- `project-c-netcode-patterns` skill §24-26 — pitfall'ы scene-placed spawn
