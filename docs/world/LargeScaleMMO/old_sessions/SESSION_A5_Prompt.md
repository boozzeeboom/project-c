# Промт для сессии A5: FarmPlatform — фермерские префабы

**Дата:** 13 апреля 2026
**Ветка:** `qwen-gamestudio-agent-dev`
**Предыдущие сессии:** B1 (VeilSystem) ✅, B2 (Cumulonimbus) ⚠️, B3 (Cloud Layers) ✅

---

## 📋 КОНТЕКСТ

### Текущее состояние:
- ✅ WorldData.asset — главный ассет мира
- ✅ 5 MountainMassif.asset — файлы массивов
- ✅ 29 пиков в данных (но визуал — "пипки", масштаб сломан)
- ✅ 9 фермерских угодий определены в данных (координаты есть)
- ❌ FarmPlatform.prefab — НЕ СУЩЕСТВУЕТ
- ❌ TerraceBuilder.cs — НЕ СУЩЕСТВУЕТ
- ❌ FerryCableSystem.cs — НЕ СУЩЕСТВУЕТ

### Источник данных:
- `docs/world/WorldLandscape_Design.md` §4 — фермерские угодья
- `docs/world/Landscape_TechnicalDesign.md` §3 — техническая архитектура

### 9 фермерских угодий (координаты в метрах):

| Угодье | Массив | X | Y (метры) | Z | Продукция | Роль |
|--------|--------|------|-----------|------|-----------|------|
| Эверест 010 | Гималаи | +300 | 4200 | -800 | Латекс | Ферма братьев Дюшек |
| Эверест 020 | Гималаи | +600 | 3500 | -1200 | Зерновые | Фермерский пост |
| Эверест 110 | Гималаи | +900 | 3000 | -1000 | Овощи | Побочная ветка |
| Монблан С010 | Альпы | -1800 | 3200 | +2600 | Виноград | Винокурни Секунда |
| Кибо К010 | Африка | -2300 | 3800 | -3100 | Тропические | Плато у подножия |
| Аконкагуа Т010 | Анды | -4500 | 4500 | -1500 | Картофель | Ферма на хребте |
| Аконкагуа Т020 | Анды | -4800 | 3800 | -1000 | Кукуруза | Дальняя ферма |
| Денали Кв010 | Аляска | +800 | 3500 | +5000 | Теплицы | Научные теплицы |
| Дон 1010 | Гималаи | +1200 | 2500 | -600 | — | Ферма Дона Эррея |

**ВАЖНО:** Высоты в метрах (НЕ scaled units!). Y=4200 = 4200 метров, НЕ 42.00!

---

## 🎯 ЦЕЛЬ СЕССИИ

Создать FarmPlatform.prefab с модульной архитектурой и Editor-скрипт для размещения 9 ферм на карте.

---

## 📝 ЗАДАЧИ

### 1. Создать FarmPlatform.cs — компонент фермы
**Файл:** `Assets/_Project/Scripts/World/Farms/FarmPlatform.cs`

**Функционал:**
- Компонент на root-объекте фермы
- Хранит FarmData (ссылка на ScriptableObject)
- Управляет модулями (террасы, здания, теплицы, док)
- При Start: инициализирует ферму из данных

**Структура данных:**
```csharp
public class FarmPlatform : MonoBehaviour
{
    [Header("Данные фермы")]
    public FarmData farmData;

    [Header("Модули (назначаются автоматически)")]
    public Transform antiGravPlatform;
    public Transform terracesParent;
    public Transform buildingsParent;
    public Transform dockingBay;
    public Transform ferryStation;

    [Header("Настройки")]
    public float platformSizeX = 40f;
    public float platformSizeZ = 20f;
    public float platformThickness = 2f;
    public Color emissiveEdgeColor = new Color(0.31f, 0.76f, 0.97f); // #4fc3f7
    public float emissiveIntensity = 0.5f;

    public int terraceCount = 3;
    public float terraceSpacing = 5f;

    public bool hasGreenhouse = false;
    public bool hasDockingPad = true;
    public bool hasFerryStation = false;

    // При Start: создать базовую платформу
    void Start();
    
    // Создать антигравийную плиту
    void CreateAntiGravPlatform();
    
    // Создать террасы
    void CreateTerraces();
    
    // Создать здания
    void CreateBuildings();
    
    // Создать док
    void CreateDockingBay();
}
```

### 2. Создать TerraceBuilder.cs — генератор террас
**Файл:** `Assets/_Project/Scripts/World/Farms/TerraceBuilder.cs`

**Функционал:**
- Генерирует mesh террасы на основе параметров
- Терраса — это платформа с бортиками, следующая рельефу
- Для прототипа: простые прямоугольные террасы (не по рельефу)

**Алгоритм:**
```
Для каждой террасы (i от 0 до terraceCount-1):
  Y = platformY - (i * terraceSpacing)
  Размер: уменьшается (40→30→20 units по X, 20→15→10 по Z)
  Форма: прямоугольник с бортиками (height 1 unit)
  Материал: зелёный #5d8a4a
```

### 3. Создать FerryCableSystem.cs — паромные тросы
**Файл:** `Assets/_Project/Scripts/World/Farms/FerryCableSystem.cs`

**Функционал:**
- Catenary curve между двумя фермами
- LineRenderer для визуализации троса
- Анимированная вагонетка (простой куб, движущийся по тросу)
- CapsuleCollider вдоль троса (для физики)

**Алгоритм catenary:**
```csharp
// y = a * cosh(x/a) — базовая формула
// Упрощённо: parabola для прототипа
Vector3 CalculateCatenaryPoint(Vector3 a, Vector3 b, float t, float sagAmount)
{
    Vector3 point = Vector3.Lerp(a, b, t);
    // Провисание: максимум в середине (t=0.5)
    float sag = Mathf.Sin(t * Mathf.PI) * sagAmount;
    point.y -= sag;
    return point;
}
```

### 4. Создать FarmData ScriptableObject
**Файл:** `Assets/_Project/Scripts/World/Core/FarmData.cs`

**Структура:**
```csharp
[CreateAssetMenu(fileName = "FarmData", menuName = "Project C/Farm Data")]
public class FarmData : ScriptableObject
{
    public string farmId;             // "everest_010", "mb_s010", ...
    public string displayName;        // "Эверест 010"
    public int farmNumber;            // 10, 20, 110...
    public string massifName;         // "Himalayan", "Alpine", ...
    public Vector3 worldPosition;     // X, Y (метры), Z
    public int parentFarm;            // 0 для основной ветки
    public string productType;        // "Латекс", "Зерновые", ...
    public int terraceCount = 3;
    public float terraceSpacing = 5f;
    public bool hasGreenhouse = false;
    public bool hasDockingPad = true;
    public bool hasFerryStation = false;
    public string[] connectedFarmIds; // ID ферм для паромных тросов
}
```

### 5. Создать Editor-скрипт для размещения 9 ферм
**Файл:** `Assets/_Project/Scripts/Editor/FarmPlacementEditor.cs`

**Функционал:**
- Меню: Tools → Project C → Farms → Place All 9 Farms
- Создаёт 9 ферм по координатам из таблицы выше
- Назначает FarmData на каждую ферму
- Создаёт паромные тросы между связанными фермами

### 6. Создать материалы для ферм
**Файлы:** `Assets/_Project/Materials/World/`
- `Material_Terrace.mat` — зелёный #5d8a4a
- `Material_AntiGravPlatform.mat` — серый с emissive edge #4fc3f7
- `Material_Greenhouse.mat` — полупрозрачный #f0c27a
- `Material_FarmBuilding.mat` — кирпич/гранит

---

## 📂 ФАЙЛЫ ДЛЯ СОЗДАНИЯ

1. `Assets/_Project/Scripts/World/Farms/FarmPlatform.cs`
2. `Assets/_Project/Scripts/World/Farms/TerraceBuilder.cs`
3. `Assets/_Project/Scripts/World/Farms/FerryCableSystem.cs`
4. `Assets/_Project/Scripts/World/Core/FarmData.cs`
5. `Assets/_Project/Scripts/Editor/FarmPlacementEditor.cs`
6. `Assets/_Project/Materials/World/Material_Terrace.mat`
7. `Assets/_Project/Materials/World/Material_AntiGravPlatform.mat`
8. `Assets/_Project/Materials/World/Material_Greenhouse.mat`
9. `Assets/_Project/Materials/World/Material_FarmBuilding.mat`

---

## ⚠️ КРИТИЧНЫЕ ЗАМЕЧАНИЯ

### Масштаб высот (КРИТИЧНО!)
- ✅ **ПРАВИЛЬНО:** Y=2500-4500 (метры)
- ❌ **НЕПРАВИЛЬНО:** Y=25-45 (scaled units)
- **Источник ошибки:** WorldLandscape_Design.md использует scaled units (42.00), но мир работает в метрах
- **Решение:** Умножать все высоты из документации на 100

### Модульная архитектура
- ✅ Каждая ферма — отдельный GameObject с компонентами
- ✅ Террасы — дочерние объекты, НЕ часть горного меша
- ✅ Фермы независимы от ландшафта (антигравийные платформы)

### Производительность
- ✅ 9 ферм × 3 террасы = 27 террас × 128 tris = ~3,456 tris
- ✅ GPU Instancing для террас (одинаковый материал)
- ✅ Простые mesh-и для прототипа (не сложные формы)

---

## 🧪 ТЕСТИРОВАНИЕ

### Шаг 1: Проверка компиляции
- Нет ошибок компиляции
- Все скрипты в правильных namespace

### Шаг 2: Размещение ферм
- В Unity Editor: Tools → Project C → Farms → Place All 9 Farms
- В Hierarchy: 9 объектов FarmPlatform
- В Inspector каждой фермы: назначен FarmData

### Шаг 3: Визуальная проверка
1. **Антигравийная плита:** 40x20, свечение #4fc3f7 по краям
2. **Террасы:** 3 ступени, зелёные #5d8a4a, уменьшаются
3. **Здания:** 2-3 домика, посадочная площадка 15x15
4. **Теплицы:** только у Денали Кв010 (полупрозрачные)

### Шаг 4: Проверка Console
- `[FarmPlatform] Создана ферма: Эверест 010 (Y=4200м)`
- `[TerraceBuilder] Создано 3 террасы для Эверест 010`
- Нет ошибок

### Шаг 5: Полёт-тест
- Использовать WorldCamera для полёта к фермам
- Проверить видимость террас, зданий, свечения
- Проверить что фермы НЕ проваливаются сквозь горы

---

## 🔗 СВЯЗАННЫЕ ДОКУМЕНТЫ

- `docs/world/WorldLandscape_Design.md` §4 — фермерские угодья
- `docs/world/Landscape_TechnicalDesign.md` §3 — техническая архитектура
- `docs/world/WorldPrototype_SessionPlan.md` §A5 — план сессии
- `docs/world/SESSION_B3_Report.md` — отчёт предыдущей сессии
- `docs/world/NEXT_STEPS_CONTEXT.md` — контекст проекта

---

## 🎯 РЕЗУЛЬТАТ СЕССИИ

✅ FarmPlatform.cs — компонент фермы с модульной архитектурой
✅ TerraceBuilder.cs — генератор террас
✅ FerryCableSystem.cs — паромные тросы с catenary curve
✅ FarmData.cs — ScriptableObject для данных фермы
✅ FarmPlacementEditor.cs — Editor-скрипт для размещения 9 ферм
✅ 4 материала (террасы, платформа, теплица, здания)
✅ 9 ферм размещены на правильных высотах (Y=2500-4500м)
✅ Визуально: антигравийные плиты, зелёные террасы, здания, теплицы

---

**ГОТОВО К РЕАЛИЗАЦИИ** 🚀
