# REMESH FEATURE ANALYSIS

**Дата:** 2026-05-10
**Документ:** Технический анализ реализации Remesh и Merge для Cloud Generator
**Статус:** REMESH реализован v7.0, MERGE реализован v7.4

---

## 1. Проблема

### 1.1 Текущее поведение
- Каждый слой генерирует независимый набор сфер
- При экспорте (OBJ / Mesh Positions) экспортируются **ВСЕ** сферы без разбора
- Внутренние сферы (скрытые другими) экспортируются наравне с внешними
- Результат: избыточная геометрия, "шум" из внутренних сфер

### 1.2 Ожидаемое поведение после Remesh
- Кнопка "Remesh" рядом с Generate/Export
- Создает **один объект** по внешнему контуру видимых сфер
- Слои сворачиваются в 1 визуальный слой
- Внутренние сферы удаляются из геометрии

---

## 2. Архитектура: что нужно изменить

### 2.1 Где хранятся сферы

```
currentSpheres[]     ← все сферы после generateCloud()
_spheres             ← Three.js mesh (Group)
_layers              ← параметры слоев
_advancedLayers      ← расширенные параметры (UI)
```

### 2.2 Что экспортируется сейчас

| Функция | Файл | Строки | Что делает |
|---------|------|--------|-------------|
| `exportMeshOBJ()` | visualizer3d.html | 2339-2402 | Итерирует все сферы, каждую превращает в UV-сферу |
| `exportMeshPositions()` | visualizer3d.html | 2405-2489 | Экспортирует x,y,z,radius,density массивом |
| `rebuildMesh()` | visualizer3d.html | ~3500+ | Рендерит сферы через Three.js |

### 2.3 Что придется переписать/добавить

#### A. Remesh алгоритм (НОВОЕ)
- Функция `performRemesh(spheres)` — создает hull-геометрию
- Функция `extractSurface(spheres)` — извлекает только внешнюю поверхность
- Функция `isSphereExposed(sphere, allSpheres)` — проверка видимости

#### B. UI изменения (МИНИМАЛЬНОЕ)
- Добавить кнопку "Remesh" в `.buttons` panel (рядом с Generate/Export)
- Добавить состояние `isRemeshed` для управления отображением

#### C. Рендеринг (ЧАСТИЧНОЕ)
- После `remesh()` — рендерить не `currentSpheres[]` как отдельные сферы, а **единый merged mesh**
- Three.js: `BufferGeometry` с всеми вершинами объединенного меша

#### D. Экспорт (ЧАСТИЧНОЕ)
- `exportMeshOBJ()` — изменить чтобы работал с merged mesh, а не с individual spheres
- `exportMeshPositions()` — возможно, сохранить как есть (это raw data)

---

## 3. Алгоритмы Remesh — сравнение

### 3.1 Опции реализации

| # | Метод | Точность | Сложность | Время | Качество |
|---|-------|----------|-----------|-------|----------|
| 1 | **Convex Hull** | Низкая — convex hull режет concave формы | O(n log n) | Быстро | Плохо для кучевых облаков |
| 2 | **Sphere Visibility** | Средняя — удаляет внутренние, оставляет выпуклые | O(n²) | Медленно | Хорошо для внешнего вида |
| 3 | **Volumetric + Marching Cubes** | Высокая — точное извлечение iso-surface | O(res³) | Медленно | Лучшее качество |
| 4 | **Approximate Shell** | Средняя — упрощенный shell | O(n²) | Средне | Приемлемо |

### 3.2 Рекомендация для Web Visualizer

**Вариант 2 (Sphere Visibility) как первая итерация:**

```
Алгоритм:
1. Для каждой сферы:
   a. Найти все сферы, которые её перекрывают
   b. Проверить — вся ли сфера внутри других?
   c. Если ДА — пометить как internal
2. Удалить internal сферы
3. Оставшиеся сферы — экспортировать как есть
   (или optionally: merge в один меш)
```

**Преимущества:**
- Реалистично для "облака из сфер" — внутренние сферы обычно глубоко внутри
- Проще чем marching cubes
- Понятная логика

**Недостатки:**
- O(n²) — может быть медленным для 5000+ сфер
- Сферы на границе "съедят" друг друга при пересечении

**Вариант 3 (Volumetric) как улучшенная версия:**

```
Алгоритм:
1. Создать 3D grid (voxelization)
2. Для каждого voxel's center:
   - Проверить, находится ли внутри какой-либо сферы
   - Если ДА — пометить voxel как "filled"
3. Применить Marching Cubes к density field
4. Извлечь surface mesh
```

**Преимущества:**
- Лучшее качество — гладкая поверхность
- True mesh, не sphere meshes

**Недостатки:**
- Сложнее в реализации
- Marching Cubes требует библиотеку или свою реализацию

---

## 4. Пошаговый план реализации

### Phase 1: Core Remesh (Visibility-based)
**Файл:** `visualizer3d.html`

```
1.1 Добавить функции:
    - bool isSphereFullyInternal(sphere, allSpheres)
    - Sphere[] getVisibleSpheres(spheres[])
    - MeshData extractExposedSurface(spheres[])

1.2 Добавить UI:
    - Кнопка "Remesh" в .buttons section
    - State: _isRemeshed, _remeshedMesh

1.3 Модифицировать rebuildMesh():
    - Если _isRemeshed — рендерить объединенный меш
    - Иначе — текущее поведение (individual spheres)

1.4 Модифицировать exportMeshOBJ():
    - Если _isRemeshed — экспортировать merged mesh
    - Иначе — текущее поведение
```

### Phase 2: Mesh Merging
**Цель:** Объединить сферы в единый mesh для экспорта

```
2.1 Для каждой visible sphere:
    - Сгенерировать UV sphere geometry (lat/lon)
    - Добавить в общий BufferGeometry

2.2 Опционально:
    - Merge vertices для одинаковых позиций
    - Deduplicate vertices
```

### Phase 3: Advanced (Volumetric) — Optional
```
3.1 Добавить marching cubes implementation
3.2 Voxelization step
3.3 Surface extraction
```

---

## 5. Изменения в коде

### 5.1 Новые функции (добавить после generateCloud)

```javascript
// Проверяет, является ли сфера полностью внутренней
function isSphereFullyInternal(sphere, allSpheres) {
    // Сфера внутренняя, если ∀ направление ∈ [0, 2π]×[0, π]
    // ∃ другая сфера, которая first перекрывает
    // Упрощение: проверяем пересечение с другими сферами
}

// Возвращает только видимые сферы
function getVisibleSpheres(allSpheres) {
    return allSpheres.filter(s => !isSphereFullyInternal(s, allSpheres));
}

// Выполняет remesh — возвращает merged geometry
function performRemesh(spheres) {
    const visible = getVisibleSpheres(spheres);
    // Объединяем в единый mesh
    return mergeSphereGeometries(visible);
}
```

### 5.2 UI изменения

В `.buttons` section (строка ~576):
```html
<div class="buttons">
    <button onclick="generate()">Generate</button>
    <button onclick="performRemesh()">Remesh</button>  <!-- НОВОЕ -->
    <button onclick="showExportDialog()">Export</button>
</div>
```

### 5.3 State переменные

```javascript
let _isRemeshed = false;
let _remeshedMesh = null;
let currentSpheres = []; // сферы до remesh
let _visibleSpheres = []; // только видимые после remesh
```

---

## 6. Экспорт и его изменения

### 6.1 exportMeshOBJ()

**Текущее поведение:**
```javascript
spheres.forEach((s, idx) => {
    // Генерирует UV sphere geometry для КАЖДОЙ сферы
    // Все сферы, включая внутренние
});
```

**После Remesh:**
```javascript
if (_isRemeshed && _remeshedMesh) {
    // Экспортировать объединенный меш
    exportMergedMeshOBJ(_remeshedMesh);
} else {
    // Текущее поведение
}
```

### 6.2 exportMeshPositions()

Эта функция экспортирует **raw данные** — позиции сфер. Может остаться как есть:
- Для тех, кто хочет получить данные для процедурного рендеринга
- Remesh — это визуальное представление, не данные

---

## 7. Вопросы для уточнения

### 7.1 UX Flow
После нажатия "Remesh":
- Слои остаются редактируемыми в UI?
- Или сворачиваются в 1 слой?
- Что происходит при изменении параметров слоя после remesh?

### 7.2 Производительность
- При каком количестве сфер (>5000) remesh должен работать?
- Нужно ли показывать прогресс для long-running операций?

### 7.3 Экспорт
- Remeshed mesh экспортировать как один OBJ файл?
- Или как array of merged spheres?

---

## 8. Timeline Estimate

| Phase | Сложность | Время |
|-------|-----------|-------|
| Phase 1: Core Visibility | Medium | 2-3 hours |
| Phase 2: Mesh Merging | Medium | 2-3 hours |
| Phase 3: Volumetric (optional) | High | 4-6 hours |

---

## 9. Риски

| Риск | Вероятность | Mitigation |
|------|-------------|------------|
| O(n²) visibility проверка слишком медленная | Medium | Grid-based spatial partitioning |
| Merge создает слишком много vertices | Medium | LOD или decimate после merge |
| Сферы на границе "съедят" друг друга | Medium | Threshold-based epsilon |
| Marching cubes сложно реализовать корректно | High | Использовать готовую библиотеку (THREE.MarchingCubes или simpsons执法) |

---

## 10. Альтернативы Remesh

### 10.1 Instanced Rendering (без изменения данных)
- Не удалять сферы, а использовать depth testing в GPU
- Визуально — единый объект
- Но экспорт всё равно экспортирует все сферы

### 10.2 Level-of-Detail (LOD)
- Близкие сферы объединяются в larger spheres
- Уменьшает количество, но не решает "internal" проблему

### 10.3 Alpha Cutoff
- Для рендеринга: использовать opacity/density threshold
- Визуально — только плотные области
- Не меняет геометрию для экспорта

---

## 11. Рекомендация

**Начать с Phase 1 + 2 (Visibility + Mesh Merging):**

1. Это даст видимый результат быстро
2. Покрывает 80% use case
3. Архитектура позволяет later добавить volumetric

**Ключевые точки интеграции:**
- `performRemesh()` — новая функция
- `rebuildMesh()` — модификация для поддержки merged mesh
- `exportMeshOBJ()` — модификация для remeshed экспорта
- UI — кнопка "Remesh"

---

## 12. Файлы для изменения

| Файл | Изменения |
|------|-----------|
| `docs/CLOUD_VISUALISER/web/visualizer3d.html` | Основные изменения |
| `docs/CLOUD_VISUALISER/REMESH_ANALYSIS.md` | Этот документ |

**Следующие шаги:**
1. Подтвердить UX flow
2. Определить приоритет Phase 1 vs Phase 2
3. Приступить к реализации

---

## 13. Реализация v7.0 (2026-05-10)

### 13.1 Что реализовано

**State переменные:**
```javascript
let _isRemeshed = false;
let _remeshedGeometry = null;
let _visibleSpheres = [];
```

**Новые функции:**
- `isSphereFullyInternal(sphere, allSpheres, epsilon = 0.1)` — проверяет, находится ли сфера полностью внутри других
- `getVisibleSpheres(allSpheres)` — фильтрует только видимые сферы
- `performRemesh()` — основная функция remesh

**Модифицированные функции:**
- `rebuildMesh()` — добавлена поддержка `_isRemeshed` режима
- `exportMeshOBJ()` — использует `_visibleSpheres` если remeshed
- `exportMeshPositions()` — использует `_visibleSpheres` если remeshed
- `generate()` — сбрасывает `_isRemeshed` при новой генерации

**UI:**
- Добавлена кнопка "Remesh" рядом с Generate/Export

### 13.2 Алгоритм

**Visibility check (isSphereFullyInternal):**
```
Для каждой сферы S:
    Для каждой другой сферы O:
        Если distance(S center, O center) + S.radius <= O.radius + epsilon
            → S полностью внутри O
    Если ни одна O не перекрывает полностью → S видима
```

**Remesh process (performRemesh):**
1. Получить все видимые сферы через `getVisibleSpheres()`
2. Для каждой видимой сферы:
   - Сгенерировать UV sphere geometry (lat/lon segments)
   - Добавить vertices и colors в BufferGeometry
3. Применить `computeVertexNormals()`
4. Перерендерить через `rebuildMesh()`

### 13.3 Известные ограничения

1. **O(n²) visibility check** — для 5000+ сфер может быть медленным
2. **Spheres are merged as cluster** — сферы объединены как vertices в одном mesh, но не создают гладкий hull
3. **Segment count fixed at 16** — можно добавить параметр для настройки

### 13.4 Реализованные улучшения (v7.0 финал)

1. **Indices добавлены** — proper triangle faces для рендеринга
2. **16 сегментов** — гладкие сферы (было 6 в ранней версии)
3. **Export integration** — OBJ и Mesh Positions экспорты работают с remeshed данными

### 13.5 Следующие улучшения (Roadmap)

1. Spatial hashing для O(n log n) visibility check
2. Mesh simplification после merge
3. Marching cubes для гладкой поверхности (альтернатива当前的 UV sphere approach)
4. Конфигурируемый segment count

### 13.6 Строки кода (приблизительные)

| Функция | Line |
|---------|------|
| performRemesh | ~3931-3996 |
| isSphereFullyInternal | ~3892-3908 |
| getVisibleSpheres | ~3910-3912 |
| rebuildMesh (mod) | ~3851-3890 |
| exportMeshOBJ (mod) | ~2346-2352 |
| exportMeshPositions (mod) | ~2415-2421 |

---

## 14. MERGE FEATURE (v7.1)

### 14.1 Концепция

**MERGE** объединяет все видимые слои в один "запечённый" (frozen) слой, который:
- **НЕ редактируется** — параметры генерации скрыты
- **ПОДДЕРЖИВАЕТ transform** — offset, size, rotation

### 14.2 Отличие от Remesh

| | Remesh | MERGE |
|---|---|---|
| **Цель** | Удалить внутренние сферы | Объединить слои |
| **Результат** | BufferGeometry | Merged слой |
| **Transform** | Нет | Да |
| **Редактирование** | Нет | Нет (запечён) |
| **Unmerge** | Нет | Да (восстанавливает слои) |

### 14.3 State переменные

```javascript
let _isMerged = false;
let _mergedSpheres = []; // сферы до transform
let _mergedLayerData = null; // для unmerge

// merged layer structure
{
  name: 'Merged Layer',
  isMerged: true,
  enabled: true,
  // Transform - ЕДИНСТВЕННОЕ что можно менять
  offsetX: 0, offsetY: 0, offsetZ: 0,
  sizeX: 1.0, sizeY: 1.0, sizeZ: 1.0,
  rotationX: 0, rotationY: 0, rotationZ: 0
}
```

### 14.4 Новые функции

| Функция | Описание |
|---------|----------|
| `performMerge()` | Объединяет все слои в один frozen слой |
| `applyMergedTransform()` | Применяет transform к сферам |
| `updateMergedLayerField()` | Обновляет transform и перерендеривает |
| `unmergeLayers()` | Восстанавливает сферы как 1 базовый слой |

### 14.5 UI

- **Кнопка Merge Layers** — рядом с Add Layer (2 колонки)
- **Merged слой** — special UI с только Transform секцией
- **Кнопка Unmerge** — разворачивает обратно

### 14.6 Реализация

1. При MERGE:
   - Если уже есть merged → добавляем новые сферы к существующему
   - Если новый merge → собираем все редактируемые сферы в `_mergedSpheres`
   - Создаём merged слой в `window._advancedLayers`
   - Рендерим: merged + editable сферы через `rebuildMesh()`

2. При изменении transform:
   - `applyMergedTransform(_mergedSpheres)` → transformed spheres
   - Обновляем mesh напрямую

3. При Unmerge (НЕ РАБОТАЕТ корректно):
   - Создаёт 1 базовый sphere слой
   - Возвращает сферы в `currentSpheres`
   - **TODO**: восстанавливает реальную структуру слоёв вместо базовой

### 14.7 Workflow

```
1. Добавили 1,2...n слоёв → Generate → сферы сгенерированы
2. Merge → все сферы в _mergedSpheres, 1 merged слой в UI
3. Добавили новый слой → generate() делает merge невидимым → рендерит все сферы
4. Ещё Merge → старый merge + новые сферы → в один merged
5. Remesh → работает с transformed merged + editable сферами
```

### 14.8 Известные проблемы

| Проблема | Статус |
|----------|--------|
| Unmerge возвращает 1 базовый sphere слой | **НЕ РАБОТАЕТ** — нужно исправить |
| Remesh при наличии merge | Работает |
| Export при merge | Работает (OBJ, C# positions) |
| Добавление слоёв при active merge | Работает |
| Transform controls | Работает |
