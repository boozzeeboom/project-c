# 🧩 CombineMeshesToCollider — Пайплайн импорта Blender-схем

**Версия:** 1.0 (2026-06-02)
**Скрипт:** `Assets/_Project/Editor/CombineMeshesToCollider.cs`
**Категория:** World / World Prototype / Editor Tools
**Статус:** ✅ Работает (проверено на `Primum/gorod port` — 143 дочерних объекта → 1 MeshCollider)

---

## 🎯 Зачем этот скрипт

### Проблема

При импорте схем городов из Blender (`.blend` → `.fbx` → Unity) каждое здание, панель, ангар, антенна приходит **отдельным GameObject'ом** с собственным `MeshFilter + MeshRenderer`, но **без коллайдера**. Типичный город = 100–500 объектов.

Если повесить `MeshCollider` вручную на каждый — получаем:

- ❌ **Performance hit.** 300+ MeshCollider'ов = PhysX работает в 5–10× медленнее, чем с одним.
- ❌ **Любая правка в Blender** → пересоздание всех коллайдеров вручную.
- ❌ **Новые импорты** → та же рутина.

Если оставить как есть — персонаж **проваливается сквозь все здания**, потому что у мешей нет физической формы.

### Решение

Editor-скрипт, который:

1. Берёт **все дочерние `MeshFilter`** у выбранного корня.
2. Склеивает меши в **один комбинированный `Mesh`** с учётом мировых трансформов (в локальных координатах корня).
3. Сохраняет результат как **ассет** (чтобы пережил перезапуск Unity).
4. Прицепляет его в **`MeshCollider`** на корне с `convex = false`.
5. По желанию — удаляет `MeshFilter + MeshRenderer` с детей и ставит общий меш-рендер на корень.

Один коллайдер на весь город, обновляется одной командой из меню.

---

## 📁 Файлы

| Путь | Что |
|---|---|
| `Assets/_Project/Editor/CombineMeshesToCollider.cs` | Сам Editor-скрипт |
| `Assets/_Project/Generated/<RootName>_Combined.asset` | Комбинированный меш (создаётся автоматически) |

> ⚠️ Папка `Assets/_Project/Generated/` создаётся скриптом автоматически при первом запуске. **Не удалять вручную** — там живут все скомбинированные меши.

---

## 🚀 Как использовать

### Быстрый путь (через меню)

1. Выделить в Hierarchy корневой объект, у которого нужно сделать общую коллизию
   (например, `Primum/gorod port`).
2. Меню: **Tools → ProjectC → Combine Children Meshes → Collider**.
3. Появится диалог:

   | Кнопка | Что делает |
   |---|---|
   | **Yes (один меш на корне)** | Удаляет `MeshFilter + MeshRenderer` со всех детей, ставит общий `MeshFilter + MeshRenderer + MeshCollider` на корень. |
   | **No (только коллайдер)** | Оставляет рендер на детях как был, добавляет на корень **только** `MeshCollider`. Дети продолжают рендериться самостоятельно. |
   | **Cancel** | Ничего не делать. |

4. Готово. В Console будет лог `[CombineMeshes] OK: …` с числом склеенных мешей.

### Программный путь (из кода / MCP / CI)

```csharp
using ProjectC.EditorTools;

// Где root — GameObject, у которого нужен общий MeshCollider
// removeChildRenderers = true → один меш-рендер на корне, false → оставить рендер на детях
CombineMeshesToCollider.CombineInto(root, removeChildRenderers: false);
```

Метод `public static`, диалогов не показывает, идемпотентный (повторный запуск пересоберёт ассет).

---

## ⚙️ Как это работает (технические детали)

### 1. Сбор дочерних `MeshFilter`

```csharp
var filters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
```

Рекурсивно обходим **всех** потомков корня, включая неактивные. Дети без `sharedMesh` пропускаются (бывает при битом импорте).

### 2. КРИТИЧНО: преобразование world → local

```csharp
Matrix4x4 rootWorldToLocal = root.transform.worldToLocalMatrix;

combine.Add(new CombineInstance {
    mesh = f.sharedMesh,
    transform = rootWorldToLocal * f.transform.localToWorldMatrix,
    // ...
});
```

**Почему это важно.** По умолчанию `CombineMeshes` берёт меш в координатах, в которых он нарисован. Если `gorod port` находится в `(41598, 2501, 38788)` и дети — на ±500м вокруг, то в склеенном меше вершины окажутся в **мировых** координатах. После присвоения в `MeshCollider` на `gorod port` PhysX будет считать, что коллайдер живёт в `(41598, 2501, 38788)` — и весь меш окажется в `(0, 0, 0)`. Коллизия улетит.

Решение — `rootWorldToLocal * localToWorldMatrix` → меш пересчитывается в **локальные координаты корня**. Коллайдер «привязан» к трансформу корня, и при перемещении родителя едет вместе с ним.

### 3. `RecalculateBounds()` + `RecalculateNormals()`

PhysX-кэш для MeshCollider **обязательно** требует правильный `bounds`. После `CombineMeshes` они выставляются как `Vector3.zero` по умолчанию — и PhysX выкидывает предупреждение *"Physics.Raycast may not behave correctly"*. Решение — `RecalculateBounds()` сразу после склейки.

Нормали тоже пересчитываем, потому что `CombineMeshes` теряет корректные нормали на стыках (для PhysX не критично, но полезно, если потом использовать меш и для рендера).

### 4. Сохранение как ассет

```csharp
string assetPath = "Assets/_Project/Generated/<RootName>_Combined.asset";
AssetDatabase.CreateAsset(combined, assetPath);
```

Mesh — это ScriptableObject-like ассет, его можно сериализовать на диск. **Без этого** комбинированный меш жил бы только в памяти редактора и исчезал при перезапуске. Тогда при следующем открытии сцены `MeshCollider` показал бы `sharedMesh = null` (Unity не умеет сериализовать runtime-меши в сцену).

### 5. Настройка `MeshCollider`

```csharp
col.sharedMesh = combined;
col.convex = false;
col.cookingOptions = CookForFasterSimulation
                   | EnableMeshCleaning
                   | WeldColocatedVertices
                   | UseFastMidphase;
```

| Параметр | Почему так |
|---|---|
| `convex = false` | **CharacterController** (используется в `PlayerController.cs`) сталкивается с **любыми** коллайдерами, в т.ч. невыпуклыми. Выпуклость нужна только для **Rigidbody** (физические объекты). |
| `CookForFasterSimulation` | PhysX прогревает меш в BVH-дерево заранее — Raycast и коллизии работают быстрее. |
| `EnableMeshCleaning` | Удаляет вырожденные треугольники (в Blender-экспорте их много из-за coincident vertices). |
| `WeldColocatedVertices` | Сливает вершины, которые оказались в одной точке (типичный артефакт `Ctrl+J` в Blender). |
| `UseFastMidphase` | Быстрая фаза поиска коллизий — PhysX использует AABB-tree вместо полного BVH. Подходит для статичной геометрии. |

### 6. Пометка сцены как dirty

```csharp
EditorSceneManager.MarkSceneDirty(root.scene);
```

Без этого Unity не подхватит изменения в `.unity`-файле при следующем сохранении, и коллайдер «исчезнет» после Ctrl+S. С этой строкой — Unity сама предложит сохранить сцену, либо наш `EditorSceneManager.SaveScene` через MCP сразу записывает.

---

## ✅ Что работает (verified)

| Сценарий | Поведение |
|---|---|
| CharacterController (PlayerController.cs) ходит/стоит на городе | ✅ **Работает** — коллизия есть, игрок не проваливается. |
| `Physics.Raycast` сверху на город | ✅ **Попадает** в новый коллайдер (проверено: 194м дистанции). |
| Перемещение родителя `Primum` | ✅ **Работает** — меш в локальных координатах, едет за трансформом. |
| Повторный запуск скрипта | ✅ **Идемпотентно** — пересоберёт ассет, перезапишет `sharedMesh`. |

## ⚠️ Известные ограничения

| Ограничение | Объяснение | Что делать |
|---|---|---|
| **Rigidbody** (физические бочки, ящики) **не будет сталкиваться** с `convex = false` коллайдером | Требование PhysX: динамические тела работают только с convex-коллайдерами | Для динамических объектов добавлять свой convex-MeshCollider или BoxCollider на самом объекте |
| PhysX предупреждает о «больших треугольниках» (>500 единиц) | Город 1.5×0.85 км → большие плиты | **Игнорировать** для статичной геометрии, на стабильность не влияет |
| Изменили `.blend` → переимпортировали | Скрипт **не вызывается автоматически** | После переимпорта: выделить корень → Tools → ProjectC → Combine… |
| Дети имеют **вложенные** пустые родители с собственными трансформами | Скрипт это поддерживает (использует `localToWorldMatrix` каждого меша), но размер комбинированного меша может вырасти | ОК для прототипа; для prod — флэтнуть иерархию в Blender |

---

## 🔄 Жизненный цикл одного импорта

```
1. Леонид экспортирует Primum_v3.fbx из Blender
   ↓
2. Перетаскивает в Unity → .fbx раскладывается в 143 GameObject'а
   ↓
3. Перетаскивает .fbx на сцену под WorldRoot_0_0/Primum
   ↓
4. Выделяет Primum/gorod port
   ↓
5. Tools → ProjectC → Combine Children Meshes → Collider → Yes
   ↓
6. ~1-2 сек — меш склеен, MeshCollider готов
   ↓
7. Play → персонаж стоит на крышах, не проваливается ✅
```

---

## 🛠️ Расширение (если понадобится)

| Задача | Как добавить |
|---|---|
| Прогревать коллизию автоматически при импорте `.fbx` | Подписаться на `AssetPostprocessor` в отдельном Editor-скрипте, вызывать `CombineInto` в `OnPostprocessModel` |
| Разделять город на чанки для дальних лодов | Добавить параметр `maxVerticesPerChunk` (65535 для UInt16, ~2M для UInt32) и собирать несколько мешей |
| Заменить на примитивы (BoxCollider) для low-end | Написать упрощённый вариант с `Mesh.bounds` → авто-BoxCollider на каждый дом |
| Прогревать коллизию в Job'е (Burst) | Сейчас `CombineMeshes` синхронный; для 500+ объектов может занять ~5 сек — норма для Editor-time, не для runtime |

---

## 📚 Связанные документы

- [`docs/STEP_BY_STEP_DEVELOPMENT.md`](STEP_BY_STEP_DEVELOPMENT.md) — общие правила пошаговой разработки
- [`docs/world/MOUNTAIN_MESH_V2_PLAN.md`](world/MOUNTAIN_MESH_V2_PLAN.md) — аналогичный пайплайн для гор (для справки по стилю)
- [`docs/unity6/Unity6_BestPractices.md`](unity6/Unity6_BestPractices.md) — про MeshCollider и PhysX в Unity 6

---

**Автор скрипта:** Mavis (Mavis), 2026-06-02
**Задача:** R2-primum-import-collider (тикет трекера проекта)
