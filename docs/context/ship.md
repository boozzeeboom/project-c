# Ship Context — Project C

**Теги:** `ship`, `shipcontroller`, `modules`, `fuel`, `altitude`, `wind`, `turbulence`

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
| `Player/ShipController.cs` | Физика корабля, ввод |
| `Ship/ShipFuelSystem.cs` | Расход мезия, управление |
| `Ship/ShipModuleManager.cs` | Модули (Slot + Module) |
| `Ship/TurbulenceEffect.cs` | Случайные силы + torque |
| `Ship/WindZone.cs` | Ветровые зоны |
| `Ship/AltitudeCorridorSystem.cs` | 6 коридоров высот |

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

---

## 📖 Подробнее

- `docs/SHIP_SYSTEM_DOCUMENTATION.md` — текущая реализация
- `docs/SHIP_LORE_AND_MECHANICS.md` — механики из лора
- `docs/SHIP_CONTROLLER_PLAN.md` — план разработки
- `docs/world/LargeScaleMMO/SESSION_2026-04-14.md` — сессия 2

---

**Обновлено:** 2026-04-15
