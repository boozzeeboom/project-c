#  ПРОМТ ДЛЯ СЕССИИ 2: Altitude Corridors

**Дата:** 11 апреля 2026  
**Проект:** Project C — MMO/Co-Op авиасимулятор над облаками  
**Ветка:** `qwen-gamestudio-agent-dev`  
**Текущий тег:** `backup-1session-ship-improved`

---

## 📋 КРАТКАЯ ИНСТРУКЦИЯ ДЛЯ НОВОЙ СЕССИИ

Ты продолжаешь работу над **Project C** — MMO/Co-Op игрой над облаками по книге «Интеграл Пьявица».

**Контекст:** Сессия 1 завершена ✅ — ShipController v2.0 с smooth movement, 4 класса кораблей, стабилизация.

**Задача Сессии 2:** Реализовать **систему коридоров высот** — корабли могут летать только в определённом диапазоне высот, сервер валидирует высоту, при выходе за границы — предупреждения и эффекты.

---

## 📂 Ключевые Документы (ЧИТАТЬ В ЭТОМ ПОРЯДКЕ)

### 1. Начни отсюда:
| Документ | Путь | Зачем |
|----------|------|-------|
| **Сессия 1: Полная документация** | `docs/Ships/SESSION_1_COMPLETE.md` | История, ошибки, решения, lessons learned |
| **GDD_10 v4.0 (секция 2)** | `docs/gdd/GDD_10_Ship_System.md` | Дизайн коридоров, серверная валидация, зоны |
| **ShipClass Presets** | `docs/Ships/SHIP_CLASS_PRESETS.md` | 4 класса кораблей (Light/Medium/Heavy/HeavyII) |

### 2. Текущий код:
| Файл | Путь | Что делает |
|------|------|------------|
| **ShipController.cs** | `Assets/_Project/Scripts/Player/ShipController.cs` | v2.0 — smooth movement, ShipFlightClass, стабилизация |
| **CreateTestShip.cs** | `Assets/_Project/Editor/CreateTestShip.cs` | Editor утилита для создания кораблей |

### 3. Лор и мир:
| Документ | Путь | Зачем |
|----------|------|-------|
| **WORLD_LORE_BOOK** | `docs/WORLD_LORE_BOOK.md` | Лор мира, Завеса, облака |
| **GDD_02 World** | `docs/gdd/GDD_02_World_Environment.md` | Мир, города, высоты |

---

## 🎯 Сессия 2: Altitude Corridor System — ЧТО ДЕЛАТЬ

### Проблема
Сейчас корабль может летать на любой высоте. Нет ограничений, нет предупреждений, нет связи с миром.

### Решение
Система коридоров высот:

```
Глобальный коридор: 1200м — 4450м

Городские коридоры (локальные):
├── Примум: 4100м — 4450м (высота города: 4348м)
├── Тертиус: 2300м — 2600м (высота города: 2462м)
├── Квартус: 1500м — 1850м (высота города: 1690м)
├── Килиманджаро: 1200м — 1550м (высота города: 1395м)
└── Секунд: 1000м — 1250м (высота города: 1142м)
```

### Зоны и Эффекты

| Зона | Высота | Эффект | Реализация |
|------|--------|--------|------------|
| **В коридоре** | minAlt — maxAlt | Всё OK | Зелёный HUD |
| **Приближение к нижней** | minAlt + 100м | Warning | Жёлтый HUD |
| **Ниже коридора** | < minAlt | Турбулентность | Красный HUD + тряска |
| **Приближение к верхней** | maxAlt - 100м | Warning | Жёлтый HUD |
| **Выше коридора** | > maxAlt + 200м | Система деградации | Красный HUD + замедление |

---

## 🏗️ Архитектура (План Реализации)

### 1. AltitudeCorridorData (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "ProjectC/Ship/Altitude Corridor Data")]
public class AltitudeCorridorData : ScriptableObject
{
    public string corridorId;
    public string displayName;
    public float minAltitude;
    public float maxAltitude;
    public bool isGlobal; // true = глобальный, false = городской
    public Vector3 cityCenter; // если городской
    public float cityRadius; // радиус города
}
```

### 2. AltitudeCorridorSystem (Manager)

```csharp
public class AltitudeCorridorSystem : MonoBehaviour
{
    public List<AltitudeCorridorData> corridors;
    
    // Получить активный коридор для позиции корабля
    public AltitudeCorridorData GetActiveCorridor(Vector3 shipPosition);
    
    // Проверить высоту корабля
    public AltitudeStatus ValidateAltitude(Vector3 position, AltitudeCorridorData corridor);
    
    // Зоны города
    public bool IsInCityZone(Vector3 position, string cityId);
}
```

### 3. AltitudeStatus (Enum)

```csharp
public enum AltitudeStatus
{
    Safe,              // В коридоре
    WarningLower,      // Приближение к нижней границе
    WarningUpper,      // Приближение к верхней границе
    DangerLower,       // Ниже коридора
    DangerUpper        // Выше коридора
}
```

### 4. Интеграция в ShipController

```csharp
// В FixedUpdate():
AltitudeCorridorData activeCorridor = corridorSystem.GetActiveCorridor(transform.position);
AltitudeStatus status = corridorSystem.ValidateAltitude(transform.position, activeCorridor);

switch (status)
{
    case AltitudeStatus.Safe:
        // Всё OK
        break;
    case AltitudeStatus.WarningLower:
    case AltitudeStatus.WarningUpper:
        // Показать warning UI
        break;
    case AltitudeStatus.DangerLower:
        // Применить турбулентность
        ApplyTurbulence();
        break;
    case AltitudeStatus.DangerUpper:
        // Применить деградацию систем
        ApplySystemDegradation();
        break;
}
```

---

## 🧪 Тесты

### Unity PlayMode тесты

1. **GlobalCorridor_BelowMin_Turbulence**
   - Телепортировать корабль на 1100м (ниже 1200м)
   - Проверить что turbulenceIntensity > 0

2. **GlobalCorridor_AboveMax_Degradation**
   - Телепортировать корабль на 4700м (выше 4450м)
   - Проверить что systemDegradation > 0

3. **CityCorridor_Approach_Warning**
   - Создать тестовый город с коридором
   - Лететь к городу с неправильной высоты
   - Проверить что показывается warning

4. **CityCorridor_Registered_Access**
   - Зарегистрированный корабль входит в город
   - Проверить что коридор города применяется

5. **AltitudeTransition_SmoothWarning**
   - Лететь от безопасной высоты к опасной
   - Проверить что warning появляется за 100м до границы

---

## ⚠️ Важные Принципы

1. **НЕ ломать Сессию 1:** SmoothDamp, ShipFlightClass, стабилизация должны остаться
2. **Серверная валидация:** Проверка высоты только на сервере (IsServer check)
3. **Клиентская репликация:** Статус высоты реплицируется клиентам через RPC
4. **ScriptableObject:** Данные коридоров — SO, легко настраивать в Inspector
5. **Города = триггеры:** При входе в радиус города — проверка регистрации корабля

---

## 📝 Что НЕ делать (Lessons Learned из Сессии 1)

1. ❌ **НЕ создавать asmdef файлы** без полного анализа зависимостей
2. ❌ **НЕ коммитить без проверки компиляции** в Unity
3. ❌ **НЕ создавать несколько файлов одновременно** без промежуточной проверки
4. ❌ **НЕ забывать про Burst** — test assemblies могут мешать
5. ❌ **НЕ игнорировать существующие enum** (как ShipClass из CargoSystem)

---

## 🔧 Быстрый Старт (Пошагово)

```
1. Прочитать: docs/Ships/SESSION_1_COMPLETE.md (10 мин)
2. Прочитать: docs/gdd/GDD_10_Ship_System.md секция 2 (15 мин)
3. Создать: AltitudeCorridorData.cs (ScriptableObject)
4. Создать: AltitudeCorridorSystem.cs (Manager)
5. Создать: 6 ScriptableObject ассетов (1 глобальный + 5 городских)
6. Интегрировать: ShipController.cs (ValidateAltitude в FixedUpdate)
7. Создать: AltitudeUI.cs (warning HUD)
8. Создать: тесты (5 тестов)
9. Проверить в Unity → фидбек → итерация
10. Коммит → пуш
```

---

## 🎨 Визуал (UI Warning)

```
[HUD — верхний центр экрана]

┌─────────────────────────────────────┐
│  🟢 SAFE: Altitude 3245m            │  ← Зелёный (в коридоре)
│  Corridor: [1200m — 4450m]          │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  🟡 WARNING: Approaching lower limit│  ← Жёлтый (minAlt + 100м)
│  Altitude: 1280m / Min: 1200m       │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  🔴 DANGER: BELOW CORRIDOR!         │  ← Красный (< minAlt)
│  Altitude: 1150m — TURBULENCE!      │
└─────────────────────────────────────┘
```

---

## 📊 Статус Репо

- **Ветка:** `qwen-gamestudio-agent-dev`
- **Последний коммит:** `fix: переименовать ShipClass → ShipFlightClass (конфликт с CargoSystem)`
- **Upstream:** GitHub `boozzeeboom/project-c`
- **Команда пуша:** `git push upstream qwen-gamestudio-agent-dev`

---

## ️ Агенты для Оркестрации из game-studio/

| Агент | Роль в Сессии 2 |
|-------|-----------------|
| **@engine-programmer** | AltitudeCorridorSystem архитектура, серверная валидация |
| **@gameplay-programmer** | Баланс зон, эффекты турбулентности, деградация |
| **@unity-specialist** | ScriptableObject ассеты, тесты, UI integration |
| **@ui-programmer** | AltitudeWarning HUD, цвета, анимации |
| **@lead-programmer** | Интеграция в ShipController, RPC для клиентов |

---

## 📈 Критерии Приёмки

- [ ] AltitudeCorridorData ScriptableObject создан
- [ ] 6 коридоров настроены (1 глобальный + 5 городских)
- [ ] AltitudeCorridorSystem менеджер работает
- [ ] ShipController валидирует высоту в FixedUpdate
- [ ] Предупреждения показываются на границах
- [ ] Турбулентность применяется ниже minAlt
- [ ] Деградация применяется выше maxAlt
- [ ] 5 Unity тестов проходят
- [ ] UI warning HUD работает
- [ ] Сетевая репликация статуса высоты

---

*Документ создан: 11 апреля 2026*  
*Сессия 1 завершена ✅ — тег: backup-1session-ship-improved*  
*Сессия 2 готова к началу*
