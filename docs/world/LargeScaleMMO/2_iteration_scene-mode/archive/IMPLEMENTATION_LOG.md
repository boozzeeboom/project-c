# Scene System Implementation Log

**Дата:** 28.04.2026
**Проект:** ProjectC_client
**Статус:** Phase 1-2 РЕАЛИЗОВАНО + Editor Script

---

## История изменений

### 28.04.2026 - Обновление

#### 1. WorldSceneGenerator.cs (НОВЫЙ)
**Путь:** `Assets/_Project/Editor/WorldSceneGenerator.cs`

Editor script для генерации 24 сцен мира в сетке 4x6:
- 4 ряда (меридианы) x 6 колонок (параллели)
- Размер сцены: 79,999 x 79,999 единиц
- Горизонтальный wrap (как Земля по долготе)
- Вертикальная блокировка полюсов (ряд 3 не связан с рядом 0)

**Функции:**
- `ShowWindow()` - открывает editor window
- `GenerateAllScenes()` - создаёт все 24 сцены
- `CreateScene()` - создаёт одну сцену со всем содержимым
- `CreateGroundMaterial()` - создаёт URP материал
- `CreateBoundaryColliders()` - создаёт физические границы
- `CreatePoleBlocker()` - создаёт блокаторы полюсов

**Содержимое каждой сцены:**
- WorldRoot (пустой GameObject)
- DirectionalLight (с тенями)
- GroundPlane (Plane с материалом)
- SceneLabel (TextMeshPro)
- Boundaries (коллайдеры север/юг/восток/запад + блокаторы полюсов)

#### 2. SceneRegistry.cs (ОБНОВЛЁН)
**Изменения:**
```csharp
// БЫЛО:
GridColumns = 8;
GridRows = 8;

// СТАЛО:
GridColumns = 6;  // параллели (горизонтальный wrap)
GridRows = 4;     // меридианы (вертикальная блокировка)
```

#### 3. WorldSceneManager.cs (ОБНОВЛЁН)
**Изменения:**
- Добавлена проверка `sceneRegistry.GridColumns - 1` вместо hardcoded `7`
- Добавлена проверка `sceneRegistry.GridRows - 1` вместо hardcoded `3`

```csharp
// БЫЛО:
if (playerScene.GridX < 7)

// СТАЛО:
int maxGridX = sceneRegistry != null ? sceneRegistry.GridColumns - 1 : 5;
if (playerScene.GridX < maxGridX)
```

---

## Архитектура мира (4x6)

```
Сетка: 6 колонок (параллели) x 4 ряда (меридианы)

|0,0|0,1|0,2|0,3|0,4|0,5|  <- Ряд 0: Экватор
|1,0|1,1|1,2|1,3|1,4|1,5|  <- Ряд 1: Умеренный
|2,0|2,1|2,2|2,3|2,4|2,5|  <- Ряд 2: Умеренный
|3,0|3,1|3,2|3,3|3,4|3,5|  <- Ряд 3: Полюса (ЗАБЛОКИРОВАНЫ)
```

### Горизонтальный wrap (параллели):
- 0,0 ↔ 0,5 ↔ 0,1 (как Земля)
- Движение влево из 0,0 → 0,5
- Движение вправо из 0,0 → 0,1

### Вертикальная блокировка (меридианы):
- Ряд 3 (полюса) НЕ соединён с рядом 0 (экватор)
- Физические коллайдеры с тегом `PoleBlocker` блокируют переход

---

## Размеры мира

- Размер одной сцены: 79,999 x 79,999 единиц
- Мир по X: 6 × 79,999 ≈ **479,994** единиц
- Мир по Z: 4 × 79,999 ≈ **319,996** единиц

### Координаты сцен (world origin):
| Scene | World X | World Z |
|-------|--------|---------|
| 0,0   | 0      | 0       |
| 0,1   | 79,999 | 0       |
| 0,5   | 399,995| 0       |
| 1,0   | 0      | 79,999  |
| 3,5   | 399,995| 239,997 |

---

## Все файлы системы

### Scene Layer (Assets/_Project/Scripts/World/Scene/):

| Файл | Описание |
|------|---------|
| `SceneID.cs` | Struct с INetworkSerializable, конвертациями координат |
| `SceneRegistry.cs` | ScriptableObject реестр сцен (6x4 grid) |
| `ServerSceneManager.cs` | Server-side управление сценами, RPC |
| `ClientSceneLoader.cs` | Client-side additive загрузка сцен |
| `SceneTransitionCoordinator.cs` | RPC bridge |
| `SceneBoundNetworkObject.cs` | CheckObjectVisibility filtering |

### World Coordinator (Assets/_Project/Scripts/World/):

| Файл | Описание |
|------|---------|
| `WorldSceneManager.cs` | Coordination layer, preload, FloatingOrigin |

### Modified Files:

| Файл | Изменения |
|------|-----------|
| `PlayerChunkTracker.cs` | +SceneID tracking |
| `WorldStreamingManager.cs` | +scene-aware chunk loading |

### Editor Scripts (Assets/_Project/Editor/):

| Файл | Описание |
|------|---------|
| `WorldSceneGenerator.cs` | Генерация 24 сцен мира |

---

## Использование WorldSceneGenerator

1. **Unity Editor** → Menu: **ProjectC → World → Generate World Scenes**
2. Нажать **Generate All Scenes**
3. Подождать ~10 секунд
4. Сцены появятся в `Assets/_Project/Scenes/World/`
5. Создать **SceneRegistry**: Menu: **ProjectC → World → Scene Registry**
   - Grid Columns: **6**
   - Grid Rows: **4**

---

## Теги для блокировки

- `PoleBlocker`: Физические коллайдеры которые блокируют переход через полюса

Пример использования в коде:
```csharp
if (collision.gameObject.CompareTag("PoleBlocker"))
{
    // Блокировать переход сцены
}
```

---

## Known Issues Fixed

1. **isActive error** - `CreateBoundaryVisualization` использовал `isActive` из другого метода. Исправлено на проверку `color == Color.red`.

---

## Следующие шаги

### Phase 3: Тестирование
- [ ] Запустить WorldSceneGenerator
- [ ] Проверить 24 сцены созданы
- [ ] Тест с 2 игроками в разных сценах
- [ ] Проверить NGO visibility

### Phase 4: Preload оптимизация
- [ ] Добавить гистерезис для preload триггеров

### Phase 5: Terrain Overlap (отложено)
- [ ] Реализация overlap зон для бесшовного ландшафта