# GDD_12.1: Scene-Based World Streaming System

**Статус:** ✅ Реализовано
**Дата:** 1 мая 2026
**Версия:** v0.0.17

---

## 1. Обзор системы

Система распределённого мира на основе 24 сцен для поддержки MMO-масштаба. Каждая сцена имеет фиксированный размер 79,999 × 79,999 units. Мир организован в виде сетки 4×6 (4 ряда, 6 колонок).

```
Grid Layout (Z is north-south, X is east-west):

    X→0     X→1     X→2     X→3     X→4     X→5
  ┌───────┬───────┬───────┬───────┬───────┬───────┐
Z→0 │ 0_0  │ 1_0  │ 2_0  │ 3_0  │ 4_0  │ 5_0  │
  ├───────┼───────┼───────┼───────┼───────┼───────┤
Z→1 │ 0_1  │ 1_1  │ 2_1  │ 3_1  │ 4_1  │ 5_1  │
  ├───────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┤
Z→2 │ 0_2  │ 1_2  │ 2_2  │ 3_2  │ 4_2  │ 5_2  │
  ├───────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┤
Z→3 │ 0_3  │ 1_3  │ 2_3  │ 3_3  │ 4_3  │ 5_3  │
  └───────┴───────┴───────┴───────┴───────┴───────┘
```

**Ключевые характеристики:**
- **Всего сцен:** 24
- **Размер сцены:** 79,999 × 79,999 units
- **Общий размер мира:** ~480,000 × ~320,000 units
- **Именование:** `WorldScene_{GridX}_{GridZ}` (напр., `WorldScene_0_0`)

---

## 2. Архитектура загрузки

### 2.1 Стратегия "1+1"

- **Current:** Только сцена, где находится игрок
- **Preload:** Одна соседняя сцена при приближении к границе (10,000 units)
- **Max loaded:** 4 сцены одновременно
- **Unload:** Сцены >10,000 units от игрока выгружаются

### 2.2 Определение границ сцен

```
Границы по Z: z=79999, 159998, 239997, 319996
Границы по X: x=79999, 159998, 239997, 319996, 399995
```

### 2.3 Расчёт сцены по позиции

```csharp
SceneID.FromWorldPosition(position)
// gridX = floor(worldPos.x / 79999f)
// gridZ = floor(worldPos.z / 79999f)
```

---

## 3. Компоненты системы

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `ClientSceneLoader` | `Scripts/World/Scene/ClientSceneLoader.cs` | Основной клиентский загрузчик |
| `ServerSceneManager` | `Scripts/World/Scene/ServerSceneManager.cs` | Серверное отслеживание позиций |
| `SceneID` | `Scripts/World/Scene/SceneID.cs` | Структура координат сцены |
| `SceneRegistry` | `Scripts/World/Scene/SceneRegistry.cs` | ScriptableObject с метаданными |
| `WorldSceneManager` | `Scripts/World/WorldSceneManager.cs` | Центральный координатор |

### 3.1 ClientSceneLoader

**Настройки:**
- `preloadDistance = 10000` — начало предзагрузки
- `unloadDistance = 10000` — выгрузка при удалении
- `maxLoadedScenes = 4` — максимум загруженных сцен

**Ключевые методы:**
- `Update()` — определение границ, прелод/выгрузка
- `LoadSceneBoundaryBased()` — загрузка текущей сцены
- `CalculatePreloadScene()` — расчёт соседней сцены для предзагрузки
- `CheckDistanceBasedUnload()` — выгрузка далёких сцен
- `LoadSceneAsync()` — асинхронная загрузка через SceneManager

### 3.2 SceneID

```csharp
public struct SceneID
{
    public int GridX;  // Колонка (0-5)
    public int GridZ;  // Ряд (0-3)
}
```

### 3.3 SceneRegistry

ScriptableObject содержащий:
- Grid dimensions (6 columns × 4 rows)
- Scene naming prefix ("WorldScene_")
- Helper methods: `GetSceneGrid3x3()`, `GetSceneGrid5x5()`, `IsValid()`

---

## 4. Интеграция с сетевой подсистемой

### 4.1 FloatingOriginMP

- **PlayerSpawner** — в `excludeFromShift` списке, отслеживает мировую позицию
- **NetworkPlayer(Clone)** — в `excludeFromShift` списке, локальная позиция

### 4.2 RPC для переходов

- `LoadSceneTransitionClientRpc` — отправка данных перехода клиенту
- `SceneTransitionData` — содержит целевую сцену и локальную позицию спавна

### 4.3 Player Position Tracking

Система использует **PlayerSpawner** для мировой позиции т.к.:
- PlayerSpawner смещается FloatingOriginMP при сдвиге мира
- NetworkPlayer(Clone) остаётся в локальных координатах (~40,000)

---

## 5. Поток загрузки

```
1. Игрок пересекает границу сцены
   ↓
2. SceneID.FromWorldPosition() вычисляет новую сцену
   ↓
3. Обнаружено несоответствие (_currentScene ≠ playerScene)
   ↓
4. LoadSceneBoundaryBased()
   - Загрузка новой сцены
   - Установка _currentScene
   - Вызов UnloadDistantScenes()
   ↓
5. Каждый кадр в Update():
   - Проверка приближения к границе → предзагрузка
   - Проверка далёких сцен → выгрузка
```

---

## 6. Конфигурация сцены

**Расположение:** `Assets/_Project/Scenes/World/`

**Именование:** `WorldScene_{GridX}_{GridZ}.unity`

**Примеры:**
- `WorldScene_0_0.unity` — колонка 0, ряд 0
- `WorldScene_2_3.unity` — колонка 2, ряд 3
- `WorldScene_5_3.unity` — последняя сцена

---

## 7. Известные ограничения

| # | Ограничение | Приоритет | План |
|---|-------------|-----------|------|
| 1 | Визуальная задержка загрузки чанков в новых сценах | P2 | Оптимизация LOD и пулинг объектов |
| 2 | Коррекция позиции отключена (threshold=99999) | P2 | Полноценная реализация для мультиплеера |
| 3 | Y спавна = 3000 (для тестирования) | P3 | Вернуть к нормальному значению (Y≈3) |

---

## 8. Тестирование

1. Launch as Host в Unity
2. Игрок спавнится в Scene(0,0) center: (39999, 3000, 39999)
3. Лететь к z=80000+ → Scene(0,1) загружается, Scene(0,0) выгружается
4. HUD показывает: Player Scene, Loaded count, Player Position
5. Консоль: preload triggers, distance-based unloads

---

## 9. Связь с GDD

- **GDD_12:** Network & Multiplayer — базовая сетевая архитектура
- **GDD_02:** World & Environment — устройство мира (15 пиков, 4 города)
- **Этап 2.1:** Соответствует этапу `Этап 2.1: Масштабный мир (24 сцены)` в плане разработки

---

## 10. Git Commits

```
9b36e47 - Iteration 2 complete: Scene-based world streaming
01ed79e - New strategy: boundary-based scene loading
d8fdcce - Fix: distance-based unloading
3531640 - Fix: Disable position correction
2438a34 - Fix: FindLocalPlayer uses PlayerSpawner
7eda914 - Fix: ClientSceneLoader singleton
```