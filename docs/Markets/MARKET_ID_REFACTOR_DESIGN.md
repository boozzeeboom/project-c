# MARKET ID REFACTOR — Дизайн-документ

**Версия:** 1.0  
**Дата:** 2026-07-03  
**Контекст:** T-CARGO-NPC-01 / баг `MarketNotFound`

---

## 1. Текущее состояние (AS-IS)

### 1.1 Участники системы market-ID

| Компонент | Тип | Где лежит | Поле | Пример значения |
|---|---|---|---|---|
| `MarketConfig` | ScriptableObject | `Trade/Data/Markets/` | `locationId` | `"primium"` |
| `MarketServer` | NetworkBehaviour | `BootstrapScene` | `List<MarketConfig> marketConfigs` | сериализованный список |
| `MarketZone` | MonoBehaviour | `WorldScene_X_Z` | `locationId` (string) | `"PRIMIUM_TEST_ZONE"` |
| `DockStationDefinition` | ScriptableObject | `Docking/Resources/Data/` | `locationId` | `"PRIMIUM"` |
| `NpcShipRoute` | поле в `NpcShipSchedule` SO | `PeacefulShip/Data/` | `fromLocationId`, `toLocationId` | `"PRIMIUM"` → `"PRIMIUM_TEST_ZONE"` |

### 1.2 Поток данных (NPC trade)

```
NpcShipController.NavTick(Docked)
  → RunDwellCargoTrade()
    → locationId = state.CurrentRoute.fromLocationId   // "PRIMIUM_TEST_ZONE"
    → NpcCargoService.RunDwellTrade(npcId, shipId, shipClass, locationId, trade)
      → TradeWorld.Instance.TryNpcBuy(npcId, locationId, itemId, qty, ...)
        → _markets.ContainsKey(locationId)             // ❌ FAILS — market not found
```

### 1.3 Как TradeWorld получает markets

```
MarketServer.OnNetworkSpawn()
  → TradeWorld.CreateAndInitialize(marketConfigs, ...)
    → foreach cfg in configs:
        _markets[cfg.locationId] = new MarketState(cfg.locationId, cfg)
```

`marketConfigs` — **жёстко зашитый** `[SerializeField] private List<MarketConfig>` в BootstrapScene.

### 1.4 Реестры (locationId → объект)

| Реестр | Ключ | Значение |
|---|---|---|
| `TradeWorld._markets` | `cfg.locationId` | `MarketState` |
| `MarketZoneRegistry._zones` | `zone.locationId` | `MarketZone` |
| `DockingZoneRegistry._stationsByLocation` | `def.LocationId` | `DockStationController` |

**Все три реестра используют `Dictionary<string, T>` без нормализации регистра.**

---

## 2. Выявленные проблемы

### P1: Жёсткая привязка MarketConfig → MarketServer
`MarketServer.marketConfigs` — ручной список в BootstrapScene.  
Чтобы добавить новый рынок, нужно зайти в BootstrapScene и перетащить SO в инспектор.  
При добавлении MarketZone в WorldScene об этом легко забыть.

### P2: Разный регистр locationId
- `MarketConfig_Primium.locationId` = `"primium"` (lowercase)
- `NpcShipRoute.fromLocationId` = `"PRIMIUM"` (uppercase)
- `Dictionary.TryGetValue` чувствителен к регистру → **silent failure**

Это уже случилось: NPC не может торговать ни на одной из двух станций маршрута.

### P3: Три независимых источника locationId
Каждый компонент хранит свой `locationId` строкой. Никакой валидации на совпадение.  
Опечатка в одном месте → часы дебага.

### P4: Нет авто-обнаружения MarketConfig'ов
MarketConfig'и лежат в `Trade/Data/Markets/`, но MarketServer о них не знает, пока их явно не добавят в список. Нет ни `Resources.LoadAll`, ни перебора сцен.

### P5: Multi-step ручной процесс добавления рынка
1. Создать `MarketConfig_XXX.asset`
2. Добавить в `MarketServer.marketConfigs` (BootstrapScene)
3. Разместить `MarketZone` GO в WorldScene с тем же `locationId`
4. Создать/обновить `DockStationDefinition` с тем же `locationId`
5. Прописать `NpcShipRoute.fromLocationId` / `toLocationId`

---

## 3. Целевое состояние (TO-BE)

### Принцип: «разместил MarketZone в WorldScene — рынок работает»

Добавление нового рынка:
1. Создать `MarketConfig_XXX.asset` (один раз)
2. Разместить `MarketZone` GO в WorldScene, назначить `MarketConfig` в инспектор
3. `DockStationDefinition` и NPC-маршруты ссылаются на тот же `locationId` (как сейчас)

**BootstrapScene не трогаем.** MarketServer сам находит все MarketZone в загруженных сценах.

### 3.1 Новая архитектура

```
MarketZone (scene GO)
  ├─ [SerializeField] MarketConfig marketConfig   ← NEW
  ├─ locationId = marketConfig.locationId          ← derived, auto
  └─ OnEnable → MarketZoneRegistry.Register(this)

MarketServer.OnNetworkSpawn()
  → MarketConfigCollector.CollectFromLoadedScenes()   ← NEW
    → FindObjectsByType<MarketZone>(IncludeInactive)
    → Distinct(), filter null, deduplicate by locationId
    → return List<MarketConfig>
  → TradeWorld.CreateAndInitialize(collectedConfigs, ...)
```

### 3.2 Нормализация locationId

Все операции lookup (registration + query) нормализуют `locationId` через:

```csharp
internal static string NormalizeLocationId(string id)
    => string.IsNullOrEmpty(id) ? id : id.ToUpperInvariant();
```

Точки нормализации:
- `TradeWorld.Initialize` → ключ в `_markets`
- `MarketZoneRegistry.Register/Get` → ключ в `_zones`
- `DockingZoneRegistry.Register/GetByLocation` → ключ в `_stationsByLocation`
- `MarketZone.locationId` → при записи из MarketConfig
- `NpcShipController.RunDwellCargoTrade` → перед запросом к TradeWorld

### 3.3 Упрощённый флоу добавления рынка (TO-BE)

```
ШАГ 1: Assets > Create > ProjectC > Trade > Market Config
        → locationId = "MY_ZONE", items = [...]

ШАГ 2: В WorldScene_X_Z:
        → Create Empty "MarketZone_MyZone"
        → Add Component<MarketZone>
        → Перетащить MarketConfig_MyZone.asset в поле MarketConfig
        → Настроить tradeRadius, shipDockRadius

ШАГ 3: DockStationDefinition (если нужна станция):
        → locationId = "MY_ZONE" (должен совпадать)

ГОТОВО. MarketServer при старте подхватит MarketConfig из MarketZone.
```

---

## 4. Детальные изменения

### 4.1 MarketZone — новое поле

```csharp
public class MarketZone : MonoBehaviour
{
    [Header("Market Config")]
    [SerializeField] private MarketConfig _marketConfig;   // ← NEW

    public MarketConfig Config => _marketConfig;

    // locationId теперь derived:
    public string LocationId =>
        _marketConfig != null ? _marketConfig.locationId : _locationIdFallback;

    // fallback для обратной совместимости (старые сцены без MarketConfig)
    [SerializeField, HideInInspector]
    private string _locationIdFallback = "";
}
```

### 4.2 MarketConfigCollector — новый static helper

```csharp
namespace ProjectC.Trade.Config
{
    public static class MarketConfigCollector
    {
        /// <summary>
        /// Собирает уникальные MarketConfig из всех MarketZone в загруженных сценах.
        /// </summary>
        public static List<MarketConfig> CollectFromLoadedScenes()
        {
            var zones = Object.FindObjectsByType<MarketZone>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var seen = new HashSet<string>();
            var configs = new List<MarketConfig>();
            foreach (var zone in zones)
            {
                var cfg = zone.Config;
                if (cfg == null) continue;
                var normId = NormalizeLocationId(cfg.locationId);
                if (string.IsNullOrEmpty(normId) || !seen.Add(normId)) continue;
                configs.Add(cfg);
            }
            return configs;
        }

        internal static string NormalizeLocationId(string id)
            => string.IsNullOrEmpty(id) ? id : id.ToUpperInvariant();
    }
}
```

### 4.3 MarketServer — замена ручного списка на авто-сбор

```csharp
// СТАРОЕ:
// TradeWorld.CreateAndInitialize(marketConfigs, _repository, _resolver);

// НОВОЕ:
var autoConfigs = MarketConfigCollector.CollectFromLoadedScenes();
if (autoConfigs.Count == 0)
{
    // fallback: если в сценах нет MarketZone с MarketConfig, берём ручной список
    Debug.LogWarning("[MarketServer] No MarketConfig found via MarketZone collection, falling back to serialized list");
    TradeWorld.CreateAndInitialize(marketConfigs, _repository, _resolver);
}
else
{
    // MERGE: сценарные + ручные (ручные — для backward compat)
    var merged = new List<MarketConfig>(autoConfigs);
    foreach (var cfg in marketConfigs)
    {
        if (cfg == null) continue;
        var normId = MarketConfigCollector.NormalizeLocationId(cfg.locationId);
        if (!string.IsNullOrEmpty(normId) && merged.All(c => MarketConfigCollector.NormalizeLocationId(c.locationId) != normId))
            merged.Add(cfg);
    }
    TradeWorld.CreateAndInitialize(merged, _repository, _resolver);
}
```

### 4.4 Нормализация locationId в реестрах

**TradeWorld.Initialize:**
```csharp
var key = NormalizeLocationId(cfg.locationId);
_markets[key] = new MarketState(cfg.locationId, cfg);  // MarketState хранит оригинал
```

**MarketZoneRegistry:**
```csharp
public static void Register(MarketZone zone)
{
    var key = NormalizeLocationId(zone.LocationId);
    _zones[key] = zone;
}
public static MarketZone Get(string locationId)
{
    _zones.TryGetValue(NormalizeLocationId(locationId), out var z);
    return z;
}
```

**DockingZoneRegistry (аналогично).**

**NpcShipController.RunDwellCargoTrade:**
```csharp
string locationId = NormalizeLocationId(state.CurrentRoute.fromLocationId);
```

### 4.5 MarketConfig — автозаглавные буквы (опционально)

Добавить `OnValidate` для авто-нормализации при редактировании:

```csharp
#if UNITY_EDITOR
private void OnValidate()
{
    if (!string.IsNullOrEmpty(locationId))
        locationId = locationId.ToUpperInvariant();
}
#endif
```

---

## 5. Миграция существующих сцен

### 5.1 BootstrapScene
- MarketServer: поле `marketConfigs` остаётся (fallback), но больше не единственный источник.
- Можно постепенно опустошить список, перенеся конфиги в MarketZone сцен.

### 5.2 WorldScene_0_0
- Найти все `MarketZone` GO.
- Назначить каждому соответствующее `MarketConfig` SO в поле `_marketConfig`.
- Установить `_locationIdFallback` = текущему значению `locationId` (backward compat).

### 5.3 Editor-инструмент миграции (одноразовый)

```
Tools > ProjectC > Trade > Migrate MarketZones to MarketConfig refs
```

Скрипт:
1. Находит все MarketZone в загруженных сценах
2. Ищет MarketConfig с совпадающим locationId в `Trade/Data/Markets/`
3. Назначает ссылку, если находит
4. Если не находит — логирует предупреждение

---

## 6. План реализации (фазы)

| Фаза | Описание | Оценка |
|---|---|---|
| **Фаза 1** | `MarketConfigCollector` + нормализация + авто-сбор в MarketServer | 1-2 ч |
| **Фаза 2** | Новое поле `_marketConfig` в MarketZone + derived `LocationId` | 0.5 ч |
| **Фаза 3** | Нормализация во всех реестрах (MarketZoneRegistry, DockingZoneRegistry, TradeWorld) | 1 ч |
| **Фаза 4** | Editor-мигратор MarketZone → MarketConfig refs | 0.5 ч |
| **Фаза 5** | Миграция WorldScene_0_0: назначить MarketConfig в MarketZone | 15 мин |
| **Фаза 6** | Верификация: NPC-trade на "PRIMIUM" ↔ "PRIMIUM_TEST_ZONE" | 30 мин |

**Итого:** ~4-5 часов

---

## 7. Резюме

**Сейчас:** 5 ручных шагов, 3 несвязанных строки `locationId`, case-sensitive баги.

**После:** Разместил `MarketZone` в сцене + назначил `MarketConfig` SO → готово.  
`locationId` нормализуется везде. Добавление рынка = работа в одной сцене, без BootstrapScene.
