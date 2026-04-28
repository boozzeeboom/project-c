# World Scene Generator - Инструкция

**Скрипт:** `Assets/_Project/Editor/WorldSceneGenerator.cs`
**Сетка:** 4 ряда x 6 колонок (меридианы x параллели)

---

## Использование

1. Открыть Unity Editor
2. Menu: **ProjectC → World → Generate World Scenes**
3. В окне настроить опции:
   - Output Path: `Assets/_Project/Scenes/World`
   - Ground Plane: ✓
   - Boundary Colliders: ✓
   - Scene Labels: ✓
   - Directional Light: ✓
   - Add to Build Settings: ✓
4. Нажать **Generate All Scenes**

---

## Структура мира

```
Сетка: 6 колонок (параллели) x 4 ряда (меридианы)

|0,0|0,1|0,2|0,3|0,4|0,5|  <- Ряд 0: Экватор (wraps left-right)
|1,0|1,1|1,2|1,3|1,4|1,5|  <- Ряд 1: Умеренный
|2,0|2,1|2,2|2,3|2,4|2,5|  <- Ряд 2: Умеренный
|3,0|3,1|3,2|3,3|3,4|3,5|  <- Ряд 3: Полюса (НЕ связан с рядом 0)
```

### Горизонтальный wrap (параллели):
- Сцена 0,0 соединена с 0,5 и 0,1
- Движение влево из 0,0 → 0,5
- Движение вправо из 0,0 → 0,1

### Вертикальная блокировка (меридианы):
- Ряд 3 (полюса) НЕ соединён с рядом 0 (экватор)
- Физические коллайдеры блокируют переход через полюса
- Теги: `PoleBlocker` для детекции

---

## Размеры

- Размер сцены: 79,999 x 79,999 единиц
- Мир: 6 * 79,999 ≈ 480,000 по X (ширина)
- Мир: 4 * 79,999 ≈ 320,000 по Z (высота)

### Координаты сцен (origin):
- Scene(0,0): (0, 0, 0)
- Scene(0,1): (79999, 0, 0)
- Scene(0,5): (399995, 0, 0)
- Scene(1,0): (0, 0, 79999)
- Scene(3,5): (399995, 0, 239997)

---

## Компоненты сцен

Каждая сцена содержит:

### WorldRoot (пустой GameObject)
- Позиция: (0, 0, 0) локально

### DirectionalLight
- Интенсивность: 1.0
- Тени: Soft
- Позиция: центр сцены + 100 по Y
- rotation: (50°, -30°, 0°)

### GroundPlane
- Plane primitive
- Масштаб: 7999.9 (что даёт 79,999 x 79,999 единиц)
- Материал: URP Lit (зелёный/серый)
- Коллайдер: удалён

### SceneLabel
- TextMeshPro
- Отображает: "Scene X,Y\nWorld: (coordX, coordZ)"
- Высота: 50 единиц
- Повёрнут на 90° (смотрит вниз)

### Boundaries
- Родительский объект с дочерними коллайдерами:
  - North: верхняя граница
  - South: нижняя граница (только для ряда 0 - PoleBlocker)
  - East: правая граница
  - West: левая граница (wraps с соседом)
  - SouthPoleBlocker: Trigger для ряда 0
  - NorthPoleBlocker: Trigger для ряда 3

---

## Теги

- `PoleBlocker`: Объекты которые блокируют переход между рядами

---

## Настройка SceneRegistry

После генерации сцен создать `SceneRegistry`:
1. Menu: **ProjectC → World → Scene Registry**
2. Установить:
   - Grid Columns: 6
   - Grid Rows: 4
3. Сохранить в `Assets/_Project/Data/World/SceneRegistry.asset`

---

## Совместимость с кодом

- SceneID.GridX = колонка (0-5)
- SceneID.GridZ = ряд (0-3)
- SceneRegistry.GridColumns = 6
- SceneRegistry.GridRows = 4
- WorldSceneManager использует SceneRegistry для границ
- PoleBlocker тег для блокировки переходов

---

## Материалы

Материал ground создаётся один раз в `Assets/_Project/Materials/World/WorldGroundMaterial.mat`

Цвета по рядам:
- Ряд 0 (экватор): тёмно-зелёный
- Ряд 1-2 (умеренные): светло-зелёный
- Ряд 3 (полюса): серо-голубой