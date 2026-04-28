# Large Scale MMO: Scene-Based Architecture (Iteration 2)

**Статус:** РЕАЛИЗОВАНО
**Дата:** 28.04.2026

---

## Описание

Двухуровневая сценовая архитектура для MMO мира:
- **Scene Layer:** 24 сцены (4 ряда × 6 колонок) × 79,999 × 79,999
- **Chunk Layer:** существующая система 2,000 × 2,000

**Цель:** Решить проблему float precision (на 100k+ начинаются артефакты) путём разделения мира на загружаемые сцены.

**Горизонтальный wrap:** сцены 0,n соединяются с сценами 0,0 (цилиндрическая топология по ширине)
**Вертикальная блокировка:** ряды 0 и 3 блокируются от прямого перехода (полярные границы)

---

## Документы

| Документ | Описание |
|----------|----------|
| [SCENE_ARCHITECTURE_DECISION.md](./SCENE_ARCHITECTURE_DECISION.md) | Архитектурное решение: почему 4×6 с overlap |
| [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) | Детальный план реализации по фазам |

---

## Ключевые решения

### 1. NGO Scene Management = Оставить включённым

`CheckObjectVisibility` работает правильно:
- Вызывается при спавне
- Возвращает `false` → объект НЕ синхронизируется клиенту
- Не нужен CustomMessaging для объектов

### 2. Scene = Unity Scene файлы

- 80,000 × 80,000 единиц на сцену
- Additive loading через SceneManager
- Preload триггеры на 10k до границы

### 3. Altitude Corridors (Flight Range: Y = 1000 to 6500)

| ID | Display Name | Min Alt | Max Alt | Type |
|----|--------------|---------|---------|------|
| veil_lower | Lower Veil Zone | 100m | 1500m | region |
| cloud_layer | Cloud Layer | 1000m | 3500m | region (main flight) |
| open_sky | Open Sky | 3500m | 5000m | region |
| high_altitude | High Altitude | 5000m | 6500m | region |
| global | Global Flight Zone | 0m | 99999m | global fallback |

### 4. Overlap для seamlessness

- 1,600 единиц (2%) перекрытие между сценами
- Terrain может не совпадать на границах (отложено)
- Clouds/farms не генерировать в overlap зонах

---

## Генераторы сцен (Editor)

| Генератор | Путь | Назначение |
|-----------|------|------------|
| WorldSceneGenerator | `Assets/_Project/Editor/WorldSceneGenerator.cs` | Создаёт все 24 сцены с runtime объектами |
| WorldSceneSetup | `Assets/_Project/Editor/WorldSceneSetup.cs` | Добавляет runtime в существующие сцены |
| TestSceneGenerator | `Assets/_Project/Editor/TestSceneGenerator.cs` | Одна тестовая сцена для отладки |
| MainMenuSceneGenerator | `Assets/_Project/Editor/MainMenuSceneGenerator.cs` | Bootstrap сцена с NetworkManager |

---

## Исправленные ошибки

### CS1061: WorldData.WorldName (TestSceneGenerator.cs:100)

**Каскад:** Generator предполагал что WorldData содержит `WorldName`, `WorldSize`, `SectorSize`, `ChunksPerSector`.
**Реальность:** WorldData содержит: `heightScale`, `distanceScale`, `worldMinX`, `worldMaxX`, `worldMinZ`, `worldMaxZ`, `massifs`, `veilHeight`, `veilColor`, `veilFogDensity`, `upperLayerConfig`, `middleLayerConfig`, `lowerLayerConfig`.

**Исправление:** Заменены на корректные поля WorldData.

### CS0117: CloudLayer.* (CloudSystem.*) — CloudLayerConfig fields

**Каскад:** Генераторы пытались установить `LayerType`, `BaseHeight`, `Thickness`, `Density`, `ScrollSpeed`, `ScrollDirection` напрямую на CloudLayer.
**Реальность:** CloudLayer имеет только `config: CloudLayerConfig`. Все параметры задаются через config ScriptableObject.

**Исправление:** Удалены прямые присваивания. CloudLayer теперь создаётся пустым, конфигурация назначается через Inspector или runtime.

---

## Вердикт

**✅ РЕАЛИЗУЕМО**

Основные компоненты:
- Фаза 1-2: SceneID, ServerSceneManager, ClientSceneLoader ✅
- Фаза 3: Тестирование с 2 клиентами (в процессе)
- Фаза 4: Preload система
- Фаза 5: Terrain overlap (ОТЛОЖЕНО)

---

## Связанные документы

- [1_Iteration/CURRENT_STATE.md](../1_Iteration/CURRENT_STATE.md) - текущее состояние chunk системы
- [1_Iteration/FLOAT_PRECISION_ISSUE.md](../1_Iteration/FLOAT_PRECISION_ISSUE.md) - анализ проблемы precision