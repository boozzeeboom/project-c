# Ship Context — Project C

**Теги:** `ship`, `shipcontroller`, `modules`, `fuel`, `altitude`, `wind`, `turbulence`, `networktransform`, `scene-placed`

---

## 🚀 Система Кораблей (Сессия 2 завершена)

### Архитектура

```
ShipController (основной контроллер)
├── ShipFuelSystem — топливо (мезий)
├── ShipModuleManager — модули
├── TurbulenceEffect — турбулентность
├── WindZone — ветровые зоны
└── AltitudeCorridorSystem — коридоры высот
```

### Ключевые Файлы

| Файл | Назначение |
|------|------------|
| `Player/ShipController.cs` | Физика корабля, ввод, RPC |
| `Ship/ShipFuelSystem.cs` | Расход мезия, управление |
| `Ship/ShipModuleManager.cs` | Модули (Slot + Module) |
| `Ship/TurbulenceEffect.cs` | Случайные силы + torque |
| `Ship/WindZone.cs` | Ветровые зоны |
| `Ship/AltitudeCorridorSystem.cs` | 6 коридоров высот |

### Сетевые компоненты на префабе/GameObject корабля

| Компонент | Обязательно | Зачем |
|---|---|---|
| `Rigidbody` | ✅ | Физика полёта |
| `NetworkObject` | ✅ (RequireComponent) | Сетевая идентификация. У scene-placed объектов `IsSceneObject=true` |
| `NetworkTransform` | ✅ **(Authority: Server)** | Репликация позиции/поворота от сервера к клиентам. Без этого клиенты видят корабль неподвижно в стартовой точке |
| `ShipController.cs` | ✅ | Сетевой контроллер (NetworkBehaviour) |
| `SceneBoundNetworkObject` | ⏳ TODO | Per-scene фильтрация видимости через `ServerSceneManager.HideSceneObjectsFromClient`. **Не добавлен** на текущей фазе — нужен, когда стриминг мира будет доведён |

> **См. также:** `docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md` — диагноз, фикс, что не делаем и почему.

### Scene-placed корабли (текущая фаза)

Корабли размещаются прямо в сцене (`WorldScene_0_0` — три тестовых: `Ship_Light`, `Ship_Medium`, `Ship_Heavy`). При запуске через `BootstrapScene → StartHost()`:

1. NGO через `NetworkSceneManager` с `EnableSceneManagement=1` загружает ВСЕ 24 сцены из Build Settings
2. **НО:** у scene-placed `NetworkObject`, добавленных вручную (не через префаб), `InScenePlacedSourceGlobalObjectIdHash == 0` — NGO **не считает** их scene-placed и **не спавнит автоматически**
3. `ScenePlacedObjectSpawner` (компонент в `BootstrapScene`) подписан на `ClientSceneLoader.OnSceneLoaded` — на сервере при загрузке сцены находит все `NetworkObject` с `!IsSpawned` и вызывает `Spawn(destroyWithScene: true)` вручную
4. После этого `ShipController.IsSpawned == true`, RPC (`AddPilot`, `SubmitShipInput`) работают

**Альтернатива (если `InScenePlacedSourceGlobalObjectIdHash != 0`):** NGO спавнит scene-placed автоматически, `ScenePlacedObjectSpawner` скипает их (видит `IsSpawned == true`). Спавнер — идемпотентный, безопасен в обоих случаях.

**Если `IsSpawned == false` в Play Mode:** в Console должно быть `[ScenePlacedObjectSpawner] Scene (0,0): spawned=N, already=M, failed=K` (для 0_0 ожидаемо `N=3, M=0, K=0`). Если 0, 0, 3 — смотри warning'и `Failed to spawn {name}`.

---

## 🎮 Управление Кораблём

| Клавиша | Действие |
|---------|----------|
| **W/S** | Тяга вперёд/назад |
| **A/D** | Рыскание (поворот) |
| **Q/E** | Лифт вниз/вверх |
| **Shift** | Boost (×2 тяга) |
| **F** | Выйти из корабля |
| **Мышь** | Тангаж (нос вверх/вниз) |

> **TODO:** в `NetworkPlayer.Update` ввод читается через `Keyboard.current.*.isPressed` напрямую, в обход `PlayerInputReader` (см. AGENTS.md). Отдельный рефактор, не блокер.

---

## ⛽ Система Топлива (Мезій)

```csharp
// ShipFuelSystem
public class ShipFuelSystem : MonoBehaviour
{
    public float currentFuel;      // Текущий мезий
    public float maxFuel = 100f;  // Максимум
    public float consumptionRate;  // Расход в секунду

    public void ConsumeFuel(float amount);
    public void Refuel(float amount);
    public bool HasFuel();        // Проверка для boost
}
```

### Восстановление
- **R** — восстановить мезий (по умолчанию выключено)

---

## 🏔️ Altitude Corridor System (Коридоры высот)

### 6 Коридоров

| Коридор | Высота (units) | Turbulence | Degradation |
|---------|---------------|------------|--------------|
| Global | 0-100,000 | 0.0 | 1.0 |
| Primus | 2,000-6,000 | 0.5 | 0.9 |
| Secundus | 6,000-10,000 | 1.0 | 0.8 |
| Tertius | 10,000-15,000 | 1.5 | 0.7 |
| Quartus | 15,000-20,000 | 2.0 | 0.6 |
| Kilimanjaro | 20,000-25,000 | 2.5 | 0.5 |

### Эффекты

```csharp
// TurbulenceEffect
turbulenceForce = Random.insideUnitSphere * turbulenceIntensity * mass * 50f;

// SystemDegradationEffect
thrustModifier *= degradationFactor;  // 0.5-1.0
handlingModifier *= degradationFactor;
dragModifier *= (2f - degradationFactor);
```

---

## 🧩 Модули Корабля

```csharp
public class ShipModule : ScriptableObject
{
    public string moduleName;
    public ModuleType type;       // Engine, Shield, Cargo, etc.
    public float thrustBonus;
    public float handlingBonus;
    public float fuelEfficiency;
}

public class ModuleSlot : MonoBehaviour
{
    public ModuleType acceptedType;
    public ShipModule equippedModule;

    public bool ValidateModule(ShipModule module);
    public void EquipModule(ShipModule module);
}
```

### Типы модулей
- **Engine** — дополнительная тяга
- **Shield** — защита
- **Cargo** — грузоподъёмность
- **Fuel** — эффективность расхода

---

## 🌬️ Wind Zones (Ветровые зоны)

```csharp
public class WindZone : MonoBehaviour
{
    public Vector3 windDirection;
    public float windStrength;
    public float radius;

    private void OnTriggerStay(Collider other)
    {
        // Применяет силу ветра к кораблю
    }
}
```

### Применение
- Корабль в зоне получает дополнительную силу
- Влияет на управляемость

---

## ⚠️ Известные Проблемы

| Приоритет | Проблема | Статус |
|-----------|----------|--------|
| UI | AltitudeUI HUD не отображается | Требует @unity-ui-specialist |
| Модель | Корабль — примитив (сфера) | Заменить на FBX |
| Network | `SceneBoundNetworkObject` не добавлен на корабли | TODO: когда стриминг мира будет доведён |
| Network | `DefaultNetworkPrefabs.asset` пуст и не присвоен | Known issue, отдельный тикет (см. `INTEGRATION_SHIPS_TO_WORLD_0_0.md` §6) |
| Network | `NetworkPlayer.Update` использует `Keyboard.current.*` напрямую вместо `PlayerInputReader` | TODO: отдельный рефактор, не блокер |

---

## 📖 Подробнее

- `docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md` — **полный разбор интеграции кораблей в 0_0, диагноз NRE, фикс**
- `docs/SHIP_SYSTEM_DOCUMENTATION.md` — текущая реализация
- `docs/SHIP_LORE_AND_MECHANICS.md` — механики из лора
- `docs/SHIP_CONTROLLER_PLAN.md` — план разработки
- `docs/world/LargeScaleMMO/SESSION_2026-04-14.md` — сессия 2

---

**Обновлено:** 2026-06-02 (добавлен раздел про NetworkTransform, scene-placed, ссылки на INTEGRATION_SHIPS_TO_WORLD_0_0.md)
