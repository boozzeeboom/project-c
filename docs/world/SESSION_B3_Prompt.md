# Промт для сессии B3: Обновление слоёв облаков

**Дата:** 13 апреля 2026
**Ветка:** `qwen-gamestudio-agent-dev`
**Предыдущие сессии:** B1 (VeilSystem) ✅, B2 (Cumulonimbus) ⚠️
**Статус:** ✅ РЕАЛИЗОВАНО (код готов, требуется настройка в Unity Editor)

---

## 📋 КОНТЕКСТ

### Текущее состояние:
- ✅ CloudSystem.cs — работает, есть 3 слоя (Upper, Middle, Lower)
- ✅ CloudLayer.cs — работает, генерирует облака из CloudLayerConfig
- ✅ CloudLayerConfig.cs — ScriptableObject с параметрами слоя
- ⚠️ CloudLayerConfig.asset — ОДИН ассет, нужно 3 (по одному на слой)
- ❌ CloudClimateTinter — НЕ СУЩЕСТВУЕТ (нужно создать)
- ✅ VeilSystem — работает на Y=1200

### Проблема:
**Документация использует Y=15-90 (scaled units), но мир работает в МЕТРАХ!**

**Масштаб высот:**
- Завеса: Y=1200 (метры)
- Нижний слой: Y=1500-4000 (метры)
- Средний слой: Y=4000-7000 (метры)
- Верхний слой: Y=7000-9000 (метры)

**Источник:** `docs/world/WorldLandscape_Design.md` §5.2 (таблица слоёв)

---

## 🎯 ЦЕЛЬ СЕССИИ

Обновить 3 слоя облаков под фиксированный мир с правильными масштабами и интеграцией с VeilSystem.

---

## 📝 ЗАДАЧИ

### 1. Создать 3 CloudLayerConfig ассета
**Файлы:** `Assets/_Project/Data/Clouds/`
- `CloudLayerConfig_Upper.asset`
- `CloudLayerConfig_Middle.asset`
- `CloudLayerConfig_Lower.asset`

**Параметры (в МЕТРАХ, НЕ scaled units):**

| Параметр | Upper | Middle | Lower |
|----------|-------|--------|-------|
| minHeight | 7000 | 4000 | 1500 |
| maxHeight | 9000 | 7000 | 4000 |
| density | 0.3 (редкие) | 0.6 (средние) | 0.8 (плотные) |
| cloudSize | 150 | 100 | 80 |
| sizeVariation | 2.0 | 2.0 | 1.5 |
| moveSpeed | 0.5 | 1.0 | 2.0 |
| moveDirection | (1,0,0) | (1,0,0) | (1,0,0) |
| animateMorph | true | true | false |
| morphSpeed | 0.3 | 0.5 | N/A |
| use2DPlanes | true (перистые) | false | false |
| layerType | Upper | Middle | Lower |
| cloudMaterial | Создать с цветом #f5f0e8 | #d4d0c8 | #8a8a8a |

**Как создать ассеты:**
```
В Unity Editor:
1. Правой кнопкой в Project окне → Create → Project C → Cloud Layer Config
2. Назвать файл
3. Заполнить параметры по таблице выше
4. Повторить для всех 3 слоёв
```

### 2. Создать CloudClimateTinter.cs
**Файл:** `Assets/_Project/Scripts/World/Clouds/CloudClimateTinter.cs`

**Функционал:**
- Компонент на объекте облака (каждое облако в слое)
- При Start: определяет ближайший горный массив (по XZ координатам)
- Применяет цветовой тинт на материал облака
- Нижние облака (Y < 2000) получают фиолетовый оттенок от завесы (lerp)

**Логика тинта по массиву:**

| Массив | Цвет тинта | Область XZ |
|--------|-----------|-----------|
| Himalayan | #f0e6d0 (тёплый) | X: 0-5000, Z: 0-5000 |
| Alpine | #d4d0c8 (нейтральный) | X: -6000 - -2000, Z: 2000-6000 |
| African | #e8dcc8 (песочный) | X: 0-3000, Z: -7000 - -3000 |
| Andean | #c8c0d0 (прохладный) | X: -4000 - -1000, Z: -6000 - -2000 |
| Alaskan | #d8dce8 (холодный) | X: 2000-6000, Z: 4000-8000 |

**Логика фиолетового оттенка (нижние облака near завесы):**
```csharp
if (cloudY < 2000) {
    float t = Mathf.InverseLerp(1200, 2000, cloudY); // 0 у завесы, 1 на 2000
    Color veiledColor = Color.Lerp(purpleVeil, cloudBaseColor, t);
    material.SetColor("_BaseColor", veiledColor);
}
```

**Цвета:**
- `purpleVeil = #2d1b4e` (цвет завесы)
- `cloudBaseColor` — цвет из CloudLayerConfig

### 3. Интегрировать CloudClimateTinter в CloudLayer
**Файл:** `Assets/_Project/Scripts/Core/CloudLayer.cs`

**Изменения в CreateCloud():**
```csharp
// После создания облака — добавить тинтер
CloudClimateTinter tint = cloud.AddComponent<CloudClimateTinter>();
tint.baseColor = config.cloudMaterial.color;
tint.layerType = config.layerType;
```

### 4. Обновить CloudSystem.cs
**Файл:** `Assets/_Project/Scripts/Core/CloudSystem.cs`

**Изменения:**
- Убедиться что `upperLayerConfig`, `middleLayerConfig`, `lowerLayerConfig` назначены
- В Inspector CloudSystem — назначить 3 новых ассета

### 5. Создать материалы для слоёв
**Файлы:** `Assets/_Project/Materials/Clouds/`
- `Material_Cloud_Upper.mat` — цвет #f5f0e8
- `Material_Cloud_Middle.mat` — цвет #d4d0c8
- `Material_Cloud_Lower.mat` — цвет #8a8a8a

**Настройки материала:**
- Shader: ProjectC/CloudGhibli (если доступен)
- _BaseColor: цвет слоя с alpha=0.4-0.8
- _RimColor: #ffd4a6 (Ghibli signature)
- _RimPower: 2.0
- _Softness: 0.3-0.5
- _NoiseTex, _NoiseTex2: ProceduralNoiseGenerator textures

---

## 📂 ФАЙЛЫ ДЛЯ СОЗДАНИЯ

1. `Assets/_Project/Data/Clouds/CloudLayerConfig_Upper.asset`
2. `Assets/_Project/Data/Clouds/CloudLayerConfig_Middle.asset`
3. `Assets/_Project/Data/Clouds/CloudLayerConfig_Lower.asset`
4. `Assets/_Project/Scripts/World/Clouds/CloudClimateTinter.cs`
5. `Assets/_Project/Materials/Clouds/Material_Cloud_Upper.mat`
6. `Assets/_Project/Materials/Clouds/Material_Cloud_Middle.mat`
7. `Assets/_Project/Materials/Clouds/Material_Cloud_Lower.mat`

## 📝 ФАЙЛЫ ДЛЯ ИЗМЕНЕНИЯ

1. `Assets/_Project/Scripts/Core/CloudLayer.cs` — интеграция CloudClimateTinter
2. `Assets/_Project/Scripts/Core/CloudSystem.cs` — проверка назначенных конфигов

---

## ⚠️ КРИТИЧНЫЕ ЗАМЕЧАНИЯ

### Масштаб высот (КРИТИЧНО!)
- ✅ **ПРАВИЛЬНО:** Y=1500-9000 (метры)
- ❌ **НЕПРАВИЛЬНО:** Y=15-90 (scaled units)
- **Источник ошибки:** WorldLandscape_Design.md использует scaled units, но мир работает в метрах
- **Решение:** Умножать все высоты из документации на 100

### URP материалы
- ✅ Создавать материалы через Unity Editor UI
- ❌ НЕ создавать материалы через C# скрипты
- ✅ Shader: ProjectC/CloudGhibli ИЛИ Universal Render Pipeline/Unlit

### Производительность
- ✅ Upper слой: 100-200 облаков (density=0.3)
- ✅ Middle слой: 300-400 облаков (density=0.6)
- ✅ Lower слой: 500-600 облаков (density=0.8)
- ❌ НЕ превышать 1500 облаков суммарно

---

## 🧪 ТЕСТИРОВАНИЕ

### Шаг 1: Проверка ассетов
- В Project окне: 3 CloudLayerConfig ассета с правильными параметрами
- В Project окне: 3 материала облаков

### Шаг 2: Проверка сцены
- Найти CloudSystem на сцене
- В Inspector: назначить 3 CloudLayerConfig ассета
- Нажать Play

### Шаг 3: Визуальная проверка
1. **Upper слой (Y=7000-9000):** Редкие, золотистые, полупрозрачные
2. **Middle слой (Y=4000-7000):** Средние, серо-голубые
3. **Lower слой (Y=1500-4000):** Плотные, серые
4. **Нижние облака near Y=1200:** Фиолетовый оттенок

### Шаг 4: Проверка Console
- Нет ошибок компиляции
- `[CloudSystem] Кумуло-дождевые облака включены` (от B2)
- `[CloudLayer] Используется CloudGhibli шейдер для ...`

---

## 🔗 СВЯЗАННЫЕ ДОКУМЕНТЫ

- `docs/world/WorldLandscape_Design.md` §5.1-5.2 — параметры слоёв
- `docs/world/WorldPrototype_SessionPlan.md` §B3 — план сессии
- `docs/world/SESSION_B2_Report.md` — отчёт предыдущей сессии
- `docs/world/NEXT_STEPS_CONTEXT.md` — контекст проекта
- `Assets/_Project/Scripts/Core/CloudSystem.cs` — текущая система
- `Assets/_Project/Scripts/Core/CloudLayer.cs` — менеджер слоя
- `Assets/_Project/Scripts/Core/CloudLayerConfig.cs` — конфигурация слоя

---

## 🎯 РЕЗУЛЬТАТ СЕССИИ

### ✅ РЕАЛИЗОВАНО (код):

1. ✅ **CloudClimateTinter.cs** — тинт по горному массиву + фиолетовый оттенок near завесы
   - Файл: `Assets/_Project/Scripts/World/Clouds/CloudClimateTinter.cs`
   - Определяет ближайший горный массив по XZ координатам
   - Применяет цветовой тинт на материал облака
   - Нижние облака (Y < 2000) получают фиолетовый оттенок от завесы

2. ✅ **CloudLayer.cs** — интеграция CloudClimateTinter
   - Добавлен `using ProjectC.World.Clouds;`
   - В `CreateCloud()` добавлен компонент CloudClimateTinter на каждое облако

3. ✅ **CloudSystem.cs** — проверка назначенных конфигов и логирование
   - Добавлены логи при создании каждого слоя
   - Проверка: все ли 3 конфига назначены
   - Предупреждения если конфиги отсутствуют

4. ✅ **Директории созданы:**
   - `Assets/_Project/Data/Clouds/` — для CloudLayerConfig ассетов
   - `Assets/_Project/Materials/Clouds/` — уже существует

5. ✅ **Инструкции созданы:**
   - `docs/world/B3_CloudLayerConfig_Creation.md` — пошаговая инструкция по созданию 3 CloudLayerConfig ассетов
   - `docs/world/B3_CloudMaterials_Creation.md` — пошаговая инструкция по созданию 3 материалов облаков

### ⚠️ ТРЕБУЕТ РУЧНОЙ НАСТРОЙКИ В UNITY EDITOR:

6. ⏳ **3 CloudLayerConfig ассета** — создать через Unity Editor (см. инструкцию)
   - `CloudLayerConfig_Upper.asset`
   - `CloudLayerConfig_Middle.asset`
   - `CloudLayerConfig_Lower.asset`

7. ⏳ **3 материала облаков** — создать через Unity Editor (см. инструкцию)
   - `Material_Cloud_Upper.mat`
   - `Material_Cloud_Middle.mat`
   - `Material_Cloud_Lower.mat`

8. ⏳ **Назначить конфиги на CloudSystem** — в Inspector CloudSystem:
   - Upper Layer Config → CloudLayerConfig_Upper
   - Middle Layer Config → CloudLayerConfig_Middle
   - Lower Layer Config → CloudLayerConfig_Lower

---

**ГОТОВО К РЕАЛИЗАЦИИ** 🚀
