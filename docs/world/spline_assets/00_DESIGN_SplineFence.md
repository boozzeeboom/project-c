# 🧱 Spline Fence — генеративный забор по сплайну

**Версия:** 0.2 — дизайн + шаг 1 реализован (2026-11-14)
**Статус:** ✅ `FencePath` + self-check 12/12 (Play Mode не запускался). Остальное — дизайн.
**Рабочая сцена:** `Assets/_Project/Scenes/World/WorldScene_0_0.unity`
**План меню:** `Tools → ProjectC → World → Spline Fence/…`

---

## 0. Задача и границы пилота

Художник рисует сплайн (`SplineContainer`) в мировой сцене → инструмент строит по нему забор:

| Требование | Как закрывается |
|---|---|
| Рисуешь сплайн — получаешь забор | `SplineFence` висит на том же GameObject, что `SplineContainer`; пересборка по кнопке/автоматически |
| Простой BoxCollider | отдельная группа `<Fence>_COLLIDER` с боксами на панель и на столб |
| Переключение между типами | `FenceTypeDefinition` (ScriptableObject) — 2 ассета пилота, смена = пересборка меша |
| 2 типа пилота | `Bar` (прутковый, сварная арматура) и `Masonry` (кирпичная кладка из блоков) |
| Округлое построение по сплайну | секции идут по кадру сплайна, тесселяция по стрелке прогиба |
| 90° и другие резкие углы | классификация узла по **углу излома** → miter-подрезка + угловой столб |
| Без ручного переключателя | переключатель типа меняет только «что строим по кадру», углы считает один общий код (§5) |
| Наши процедурные материалы | `M_PC_Steel_v2`, `M_PC_Brick`, `M_PC_Concrete` (§9) |

---

## 1. Факты проекта, на которые опирается дизайн

Проверено по проекту (не по памяти):

| Факт | Источник |
|---|---|
| Unity `6000.5.2f1`, URP `17.5.0` | `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json` |
| `com.unity.splines` **2.9.0**, но он **не в** `manifest.json` — приходит транзитивно | `Packages/packages-lock.json`: `com.unity.cinemachine 3.1.7` → `com.unity.splines 2.0.0`, резолвится 2.9.0, `depth: 2` |
| Пространство имён пакета — `UnityEngine.Splines` (не `Unity.Splines`) | `Library/PackageCache/com.unity.splines@e6e1d5900acc/Runtime/SplineContainer.cs:4` |
| `ProBuilder 6.1.2` установлен; runtime-API `ProBuilderMesh.Create(IEnumerable<Vector3>, IEnumerable<Face>)` доступен | `Runtime/Core/ProBuilderMeshFunction.cs:225` |
| Сплайн в проекте уже используется — как дескриптор зоны ветра | `Assets/_Project/Scripts/Ship/SplineWindZone.cs` (`[RequireComponent(typeof(SplineContainer))]`) |
| Процедурный генератор мешей уже есть и оформлен как статический класс в `Generation` | `Assets/_Project/Scripts/World/Generation/MountainMeshGenerator.cs` |
| Готовые меши складываются в `Assets/_Project/Generated/<Домен>/` | `Assets/_Project/Generated/{Ships,Primum,HLOD,EdgeBaked}` |
| Коллайдеры живут отдельной группой-ребёнком и пересобираются идемпотентно | `docs/world/SETTLEMENT_COLLIDERS.md` (`<Root>_Colliders`), `Assets/_Project/Editor/CollectCollidersTool.cs` (`_COLLIDER`) |
| Вершины меша обязаны быть в **локальных** координатах, иначе floating origin ломает коллайдер | `docs/world/COMBINE_MESHES_TO_COLLIDER.md` §2 |
| Earth/FO: геометрия статична, сцены не дублировать | `docs/world/floatingorigin/*`, правило проекта по сценам |
| Кирпич — **объектное** пространство: `_Brick_Scale 5.8`, `_Row_Offset 0.5`, `_Joint_Width_X/Y`, `Position` с `m_Space: 0` | `Assets/_Project/Materials/Brick/Brick.shadergraph:7052-7083`, `M_PC_Brick.mat` |
| Сталь v2 — процедурный шум по объектному пространству, текстур нет | `M_PC_Steel_v2.mat` (`_Noise_*`, `_Rust_*`, `_Wear_*`), `Assets/_Project/Shaders/ProceduralSteel.shader:8` («FO-safe: только object-space») |
| vertex-color контракт (`_UseVertexColors`, R=стык/G=кромка) есть **только** у `ProceduralSteel.shader`, а все его материалы лежат в `Materials/Steel/archive/` | `Assets/_Project/Editor/Materials/EdgeMaskBaker.cs:8`, grep по `guid 8db166e1…` → только `archive/*.mat` |
| Слои: `PRIMUM`=8, `PRIMUM_DOORS`=10, `ZoneCollider`=31; общего «Environment» нет | `get_tags_and_layers` |
| Terrain в проекте есть (`Terrain_0_0_TerrainData`) → высота земли доступна рейкастом | `Assets/_Project/Data/World/Terrain_0_0_TerrainData (…).asset` |

---

## 2. Принятые решения (одно на вопрос)

| Вопрос | Решение | Почему |
|---|---|---|
| Чем строить геометрию | **Mesh API Unity** (`MeshBuilder` внутри инструмента), edit-time | Забор пересобирается из сплайна при каждом изменении. ProBuilder пришлось бы реконструировать каждый раз (его face-graph — авторский инструмент, а не генератор), и он красит вершины в белый — ровно то, о чём предупреждает `EdgeMaskBaker.cs:42`. |
| ProBuilder | **не используем** в генераторе | См. выше. Остаётся доступен художнику, если секцию нужно доработать руками. |
| Blender | **не используем** | Параметрическая связь «сплайн → забор» обязана жить внутри Unity; round-trip через `.blend` её убивает. |
| Когда строим | **при авторской правке** (editor-time bake в ассеты), не в рантайме | Мир статичен, FO сдвигает корень — меш не должен пересчитываться в игре. |
| Данные типа | `FenceTypeDefinition : ScriptableObject` | Переключение типа = смена ссылки + пересборка. |
| Адаптивность к углам | **одна метрика «угол излома»**, без пользовательского переключателя | §5. |
| Коллайдер | `BoxCollider` на панель + на столб, всё в группе `<Fence>_COLLIDER` | §8. |

---

## 3. Архитектура и файлы

```
Assets/_Project/Scripts/World/Generation/SplineFence/
  FencePath.cs          — сплайн → кадры/прогоны + классификация углов + тесселяция
  FenceMeshBuilder.cs   — низкоуровневый билдер (quad / box / prism, без сглаживания нормалей)
  IFenceStyle.cs        — интерфейс стиля: Build(FencePath, FenceTypeDefinition, FenceBuildOutput)
  BarFenceStyle.cs      — тип «прутковый»
  MasonryFenceStyle.cs  — тип «кирпичный»
  SplineFence.cs        — MonoBehaviour-дескриптор (на GO со SplineContainer)
  FenceSection.cs       — маркер сгенерированной секции (по нему идёт очистка при пересборке)

Assets/_Project/Data/World/Fences/
  FenceTypeDefinition.cs
  FT_Fence_Bar_Steel.asset
  FT_Fence_Brick_Block.asset

Assets/_Project/Editor/World/
  SplineFenceTool.cs    — меню + публичный static API для MCP/CI
  SplineFenceWindow.cs  — окно: тип, параметры, Rebuild, Dry Run

Assets/_Project/Generated/Fences/<FenceName>/
  <FenceName>_<section>_mesh.asset
```

Пространства имён: `ProjectC.World.Generation` (runtime, как `MountainMeshGenerator`), `ProjectC.EditorTools` (editor, как `EdgeMaskBaker`).
Ассемблей-файлов в `Scripts/` нет — значит `#if UNITY_EDITOR` для `UnityEditor.*` (тот же приём, что в `SplineWindZone.cs:280`).

**Поток данных:**

```
SplineContainer.Spline
   → FencePath (кадры + прогоны + углы)          [чистая геометрия, без стиля]
   → IFenceStyle.Build(...)                      [тип решает: балки | кладка]
   → FenceBuildOutput { List<Mesh> sections, List<BoxSpec> colliders }
   → Editor: меши в ассеты, секции в иерархию, боксы в <Fence>_COLLIDER
```

**Модель данных:**

```csharp
public enum FenceStyle { Bar = 0, Masonry = 1 }

[CreateAssetMenu(menuName = "ProjectC/World/Fence Type")]
public class FenceTypeDefinition : ScriptableObject
{
    public FenceStyle style;
    public Material primaryMaterial;     // Bar → M_PC_Steel_v2 | Masonry → M_PC_Brick
    public Material secondaryMaterial;   // столбы/шапка → M_PC_Concrete (может быть null)

    [Header("Геометрия")] public float height = 1.9f;
    public float thickness = 0.06f;      // толщина «полотна» (у кладки — стены)
    public float postSpacing = 2.5f;     // максимальный шаг столбов
    public float postSize = 0.06f;       // сторона столба (Bar) / 0.38 (Masonry)

    [Header("Адаптивность")] public float maxDeviation = 0.01f;   // м, стрелка прогиба
    public float sharpAngleDeg = 25f;                              // порог «резкий угол»
    public float panelChordError = 0.02f;                          // для Masonry: прогиб хорды

    [Header("Профиль (по стилю)")] public BarParams bars; public MasonryParams masonry;
}
```

`SplineFence`:

```csharp
[RequireComponent(typeof(SplineContainer))]
public class SplineFence : MonoBehaviour
{
    public FenceTypeDefinition type;
    public int splineIndex = 0;
    public float groundOffset = 0f;
    public LayerMask groundMask = ~0;
    public bool autoRebuildOnSplineChange = false;
    public string fenceId = "";   // имя ассетов; пусто → имя GameObject
}
```

---

## 4. Извлечение пути: кадры и прогоны

### 4.1 Кадр (Frame)

```csharp
public struct FenceFrame
{
    public Vector3 position;   // в локальных координатах корня забора
    public Vector3 forward;    // касательная сплайна, нормализованная, горизонтально-проецированная
    public Vector3 right;      // = normalize(cross(Vector3.up, forward))
    public Vector3 up;         // всегда Vector3.up
    public float distance;     // погонный метр от начала прогона
}
```

**`up` всегда мировой `Vector3.up`** — забор вертикален независимо от того, куда «смотрит» касательная (иначе на спуске/подъёме полотно завалится). `forward` проецируется на горизонталь; если сплайн почти вертикальный — warning и кадр берётся по предыдущему.

### 4.2 Пересэмплирование по погонному метру

Сплайн параметризован по узлам, а забор — по метрам. Пересчёт единиц пакет умеет:

```csharp
float dKnot = spline.ConvertIndexUnit(knotIndex, PathIndexUnit.Knot, PathIndexUnit.Distance); // verified: SplineUtility.cs:843
float tNorm = SplineUtility.GetNormalizedInterpolation(spline, distance, PathIndexUnit.Distance);
float3 p    = SplineUtility.EvaluatePosition(spline, tNorm);
```

`PathIndexUnit.Distance` существует и поддерживается (`Runtime/SplineData.cs:32-48`, `SplineUtility.cs:866`), где `Distance` — метры вдоль сплайна (`Spline.GetLength()`).

### 4.3 Тесселяция по стрелке прогиба

Шаг адаптивный, а не фиксированный: на прямой — крупный, на дуге — мелкий, ровно до требуемой точности силуэта.

```
maxStep ≈ sqrt(8 · R · maxDeviation)        // R — локальный радиус, maxDeviation — стрелка (дефолт 1 см)
```

| R (м) | maxDeviation 10 мм → шаг |
|---|---|
| ∞ (прямая) | ограничен `postSpacing`, шаг 2.5 м |
| 20 | ≈ 1.26 м |
| 5 | ≈ 0.63 м |
| 1 | ≈ 0.28 м |

Плюс жёсткое ограничение снизу: `R ≥ thickness/2 + 0.02` для кладки — иначе внутренний откос стены схлопывается. Нарушение → `Debug.LogWarning` с расстоянием вдоль сплайна, участок строится прямыми хордами.

---

## 5. Адаптивность к углам — ядро инструмента

Требование: «округлое построение по сплайну **так и** 90°/резкие углы, **без переключений**, адаптивно». Значит тип угла определяется **по геометрии**, а не по режиму, который выставил художник.

### 5.1 Метрика «угол излома на базе ε»

Не смотрим на `TangentMode` узла (`AutoSmooth/Linear/Mirrored/Continuous/Broken` — `Runtime/BezierMode.cs:6`): художник может нарисовать 90° и в `AutoSmooth` (тогда это дуга), и в `Linear` (тогда это слом). Смотрим на саму кривую — угол между хордами ±ε метров вокруг узла:

```csharp
// ε — в метрах. Метрика не зависит от длины сплайна и не даёт NaN
// на Linear/Auto-узлах с нулевой касательной (поэтому по позициям, а не по tangent).
static float BendAngleDeg(Spline spline, float dKnot, float eps = 0.05f)
{
    float len  = spline.GetLength();
    float dIn  = math.max(0f, dKnot - eps);
    float dOut = math.min(len, dKnot + eps);

    float3 a = SplineUtility.EvaluatePosition(spline, dIn  / len);
    float3 c = SplineUtility.EvaluatePosition(spline, dKnot / len);
    float3 b = SplineUtility.EvaluatePosition(spline, dOut / len);

    float3 dirIn  = math.normalize(c - a);
    float3 dirOut = math.normalize(b - c);
    return math.degrees(math.acos(math.clamp(math.dot(dirIn, dirOut), -1f, 1f)));
}
```

Одна метрика одновременно ловит **и слом, и слишком тесную дугу**: при ε = 5 см дуга R = 1 м даёт ≈ 5.7°, R = 5 м ≈ 1.1°, прямой слом 90° даёт 90°. Поэтому «порог резкости» — это просто порог по углу.

### 5.2 Таблица решений (это и есть «адаптивно, без переключателя»)

| Угол излома (ε = 5 см) | Что это | Что делает генератор |
|---|---|---|
| < 2° | прямая / пологая дуга | один прогон, шаг по §4.3, никаких разрывов |
| 2°…`sharpAngleDeg` (25°) | плавный изгиб | сечение идёт по кадру (прутки гнутся); у кладки — разбивка на прямые панели-хорды с проверкой `panelChordError`, лишние столбы появляются автоматически |
| 25°…150° | **резкий угол** | прогон **рвётся** на два, у точки два кадра (вход и выход), оба конца подрезаются miter'ом, ставится угловой столб |
| > 150° | шпилька / реверс | `Debug.LogWarning`, путь рвётся без столба (забор физически не сходится) |

Художнику не нужно ничего включать: нарисовал дугу — получил дугу; нарисовал 90° — получил столб и два прогона.

### 5.3 Резкий угол: подрезка miter'ом (точная формула)

Столб — коробка со стороной `P` (`postSize`), стоит **на биссектрисе** угла, центр — в узле. Прогон обрезается на расстоянии `trim` от узла так, чтобы его торец не влезал в столб. Для коробки с полустороной `h = P/2`, повёрнутой на `α` к оси прогона, опорная функция даёт точную величину:

```
trim = h · (|cos α| + |sin α|)          α — угол между forward прогона и осью столба (= биссектрисой)
```

| Угол между прогонами | α | trim при P = 0.38 (кладка) |
|---|---|---|
| 180° (прямая) | 0° | 0.19 (не применяется — угла нет) |
| 90° | 45° | 0.269 м |
| 60° | 30° | 0.260 м |
| 30° | 15° | 0.233 м |

Почему не «обрезать на h»: у повёрнутой коробки вылет по оси прогона больше полустороны, и торец балки/кладки вылезет сквозь грань столба. Формула выше — это ровно проекция коробки на ось прогона, поэтому перекрытия нет ни при каком угле в диапазоне 25°…150°.

После подрезки:
* у **Bar** — концевые балки/прутки не заходят в столб, столб накрывает стык;
* у **Masonry** — торец стены закрыт столбом, отдельных «заглушек» не нужно;
* коллайдер прогона тоже укорачивается на `trim` (§8).

Внутри одного прогона дополнительных подрезок нет: miter нужен только там, где направление меняется скачком.

### 5.4 Плавный участок

* `Bar`: полотно идёт по кадру непрерывно. Вертикальные прутки — прямые, ставятся по погонному метру (шаг `barSpacing`), поперёк кадра; продольные балки (верх/низ/середина) гнутся — это полилиния по кадрам с шагом из §4.3.
* `Masonry`: **стена между столбами всегда прямая** (как настоящая кирпичная кладка между простенками). На дуге панель = хорда; если стрелка хорды на панели > `panelChordError` (2 см) — панель короче, ставится дополнительный столб. Это тоже адаптивность, но управляемая той же геометрией, а не флагом.

### 5.5 Рельеф (высота земли по кадрам)

Позиция кадра корректируется рейкастом сверху:

```csharp
if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out var hit, 200f, groundMask))
    p.y = hit.point.y + groundOffset;
```

| Тип | Поведение по высоте | Зачем так |
|---|---|---|
| `Bar` | низ и верх балок наклоняются вместе с рельефом, столбы стоят на земле | сварной забор повторяет уклон, ступенек быть не должно |
| `Masonry` | стена горизонтальна в пределах панели, подъём — только целыми венцами (`courseHeight`), ступень спрятана в столбе | кирпич не умеет наклонный ряд; ступень по столбу — так кладут в реальности |

---

## 6. Тип A — `Bar` (прутковый, сварная арматура)

Стартовые параметры (`FT_Fence_Bar_Steel.asset`, согласовать с художником):

| Параметр | Значение | Комментарий |
|---|---|---|
| `height` | 1.9 м | |
| `postSize` | 0.06 м (квадрат) | столб на каждом углу + по шагу |
| `postSpacing` | 2.5 м | максимум |
| `railSize` | 0.04 × 0.04 м | верх y = 1.86, низ y = 0.12 |
| `barSize` | 0.012 м | пруток, шаг `barSpacing` = 0.12 м, от y = 0.18 до y = 1.80 |
| `midRail` | нет | опционально y = 0.95 |
| материал | `M_PC_Steel_v2` | процедурный шум, текстур нет |

Построение (на панель 2.5 м): коробка-столб + 2 продольные балки-призмы + ~19 прутков.
Трис на панель ≈ 300 → 100 м забора ≈ 12 тыс. трис. Материал один → 1–3 DC на секцию (SRP Batcher).

---

## 7. Тип B — `Masonry` (кирпич из блоков)

Стартовые параметры (`FT_Fence_Brick_Block.asset`):

| Параметр | Значение | Комментарий |
|---|---|---|
| `height` (полотно) | 1.35 м | + шапка |
| `thickness` | 0.25 м | |
| `postSize` | 0.38 × 0.38, высота 1.55 м | столбы-простенки |
| `postSpacing` | ≤ 2.6 м | панель-хорда |
| `coping` | 0.30 × 0.06, свес 0.025 | `M_PC_Concrete` |
| `cap` на столб | 0.45 × 0.45 × 0.06 | `M_PC_Concrete` |
| материал полотна | `M_PC_Brick` (варианты `M_PC_Brick_Red`, `M_PC_Brick_Yellow`) | |

### 7.1 Кирпич — это шейдер, а не геометрия

`Brick.shadergraph` строит кладку **сам**: `_Brick_Scale 5.8` (ячейка ≈ 0.172 м при масштабе 1), `_Brick_Aspect 1.15`, `_Row_Offset 0.5` (перевязка в полкирпича), `_Joint_Width_X/Y` (швы), `_Mortar_Color`, `_Crack_*`, `_Efflo_*`, `_Dirt_*`. Реальные кирпичи в геометрии дали бы ~7 400 блоков на 100 м забора (≈ 75 тыс. трис) — на забор это неоправданно.

Поэтому: **полотно = extruded-призма (стена)**, а «блоки» рисует материал. Геометрию даёт только то, что должно читаться в силуэте: столбы, шапка, оголовки.

### 7.2 Обязательное следствие: привязка секции к локальным осям

`Position` в графе работает в **объектном** пространстве (`m_Space: 0`, `Brick.shadergraph:7052-7083`), то есть ряды кладки выровнены по локальным осям объекта. Значит стену нельзя класть «как попало» в один общий меш: под 45° к локальной X кирпич пойдёт по диагонали.

Правило: **секция = цепочка почти-сонаправленных панелей**, её локальная `X` идёт вдоль стены, `Y` — вверх, `Z` — поперёк. Панели с направлением, отличающимся от `X` секции более чем на `sectionAlignDeg` (дефолт 1°), уходят в следующую секцию. Итог: прямая стена 100 м = одна секция; 90° = две; дуга = несколько секций по граням.

> Проверка на первом билде (1 минута): повернуть тестовый куб с `M_PC_Brick` на 45° вокруг Y. Вращается кладка вместе с кубом → пространство объектное, правило выше обязательно. «Прибита» к миру → правило можно ослабить (но тогда рушится FO-безопасность — скорее всего она вращается).

### 7.3 Раскладка по длине

Длина панели режется на целое число модулей `brickCell = 1/_Brick_Scale ≈ 0.172 м`; остаток (до ±½ модуля) съедается в конце панели и прячется в столбе — кирпич не «режется пополам», швы кладки не съезжают относительно столбов.

### 7.4 Как это сделано (`MasonryFenceStyle`)

`Assets/_Project/Scripts/World/Generation/SplineFence/MasonryFenceStyle.cs`; включается через `FenceStyleBuilder` по `FenceStyle.Masonry`. Раскладка (`MasonryFenceStyle.Layout`) — публичная: ею же пользуется self-check и будущее окно инструмента.

1. **Столбы.** Шаг `≤ postSpacing`, но панель укорачивается, пока стрелка её хорды не влезет в `panelChordError`. Стрелка считается по настоящим кадрам пути (`FenceRun.Sample`), а не по оценке радиуса: кандидат делится на 0.7 и пересчитывается — на законе `s ≈ L²/8R` сходится за 2–3 шага. Огрызок в конце прогона не оставляется: последняя панель добирает остаток.
2. **Полотно.** От центра столба вдоль хорды на `wallLength = ⌊span / brickCell⌋ · brickCell`; привязка применяется только если остаток `< postSize/2`, то есть гарантированно спрячется в столбе (§7.3). У торца, который примыкает к резкому углу, столба нет — там полотно идёт на всю длину и упирается в угловой столб.
3. **Шапка и оголовки.** Шапка `copingHeight` лежит на полотне со свесом `copingOverhang` с каждой стороны (толщина = `thickness + 2·copingOverhang` = 0.30), оголовок `capSize × capSize × capHeight` — на столбе. Оба — сабмеш `secondaryMaterial` (`M_PC_Concrete`).
4. **Секции.** Идут подряд по панелям, пока направление панели расходится с направлением секции не больше чем на `sectionAlignDeg`. Секция ставится объектом-ребёнком с `localRotation`, где локальная `X` — вдоль стены, `Y` — вверх, `Z` — поперёк; меш собирается в координатах секции. Поэтому кладка в object space всегда параллельна стене, а не корню забора.
5. **Углы.** Прогон подрезается miter'ом штатно (`FencePath`, `postHalfSize = postSize/2 = 0.19`), угловой столб ставится один — по биссектрисе в узле. Торец прогона у угла своего столба не получает, иначе на 90° в узле стояли бы две коробки 0.38 почти друг в друге.
6. **Шов замкнутого прогона.** Столб один — на конце прогона, начало проходит под ним (как у `Bar`).
7. **Ступень на уклоне (§5.5).** Панель горизонтальна, её низ берётся по нижнему из двух торцов: на уклоне получается ступень по столбу, стена не висит в воздухе.

Цена правила §7.2 — числом: на дуге секций почти столько же, сколько панелей (у панели R=10 при допуске хорды 2 см поворот ≈ 5°, а порог 1°), то есть круг R=10 даёт 70 секций и 140 коллайдеров на 62.8 м. Это осознанный размен в пользу корректной кладки; ручка — `sectionAlignDeg` (5° склеит ≈4 панели в секцию, но кладка уйдёт на 5° от стены).

---

## 8. Коллайдеры

Простая коробка на панель и на столб — по конвенции проекта (`SETTLEMENT_COLLIDERS.md`: боксы, а не MeshCollider, потому что с ними дружит и `CharacterController`, и `Rigidbody` корабля).

```
<FenceName>                        (SplineFence + SplineContainer)
├─ Fence_<section>                 (MeshFilter + MeshRenderer + FenceSection)
├─ Fence_<section>                 …
└─ <FenceName>_COLLIDER
   ├─ BC_panel_000                 (BoxCollider, static)
   ├─ BC_post_000
   └─ …
```

* размер панельного бокса: `(длина панели − trim по краям, height, thickness)`, центр — середина панели, поворот — по `forward`;
* размер боксов столба: `(postSize, postHeight, postSize)`;
* для `Bar` толщина бокса = `max(thickness, barSize*2)` — иначе пруток проскочит, а `CharacterController` по тонкой стенке «залипает» (та же ловушка, что «мин. толщина бокса» в `SETTLEMENT_COLLIDERS.md`);
* всё в **локальных** координатах группы (центр/размер локальные) — floating origin везёт бесплатно (`SETTLEMENT_COLLIDERS.md` §Floating origin);
* слой: параметр на `SplineFence`, дефолт `Default`; внутри Примума — `PRIMUM` (8).

---

## 9. Материалы

| Слот | Ассет | Примечание |
|---|---|---|
| `Bar` полотно и столбы | `Assets/_Project/Materials/Steel/M_PC_Steel_v2.mat` | процедурный шум/ржавчина, `_Smoothness` залочен |
| `Bar` (мелкие посты) | опционально `M_PC_Steel_v2_Dark` / `_Hull` | из той же папки |
| `Masonry` полотно | `Assets/_Project/Materials/Brick/M_PC_Brick.mat` | кладка от объектного пространства |
| `Masonry` столбы/шапка | `Assets/_Project/Materials/Concrete/M_PC_Concrete.mat` | + `_Limewash` / `_Rose` варианты |

Ограничения, важные для генератора:

1. **`lossyScale` корня забора = 1** и без неоднородного масштаба у родителей. Оба графа работают в объектном пространстве: `_Brick_Scale` и `_Noise_Scale` заданы в метрах объектного пространства, при масштабе 0.5 кирпич станет вдвое крупнее, при 2 — вдвое мельче. Инструмент должен проверять `lossyScale` и ругаться.
2. **Vertex colors пекать не нужно.** Контракт R/G существует только у `ProceduralSteel.shader`, а все его материалы — в `Materials/Steel/archive/` (проверено grep'ом по guid). У `M_PC_Steel_v2` и `M_PC_Brick` свойства `_UseVertexColors` нет вообще. `EdgeMaskBaker` для забора не вызываем.
3. **UV0 генерируем всё равно**: `U` = погонный метр / 1 м, `V` = высота / 1 м, без разрывов на панели. Сейчас графы UV не читают, но это дешёвая страховка на будущее (и на `SMALL_`-варианты материалов).
4. Нормали — по граням (жёсткие кромки), `RecalculateNormals()` на всём меше **не** вызываем: он сгладит балки и швы.

---

## 10. Ассеты и папки

| Что | Куда |
|---|---|
| Меши секций | `Assets/_Project/Generated/Fences/<FenceName>/<FenceName>_<section>_mesh.asset` |
| Типы забора | `Assets/_Project/Data/World/Fences/*.asset` |
| Скрипты генератора | `Assets/_Project/Scripts/World/Generation/SplineFence/` |
| Editor-инструмент | `Assets/_Project/Editor/World/SplineFence*.cs` |

Меши сохраняются ассетами (как `COMBINE_MESHES_TO_COLLIDER.md` §4): runtime-меш не сериализуется в сцену и после перезапуска стал бы `null`.

---

## 11. Editor UX

**Меню**

```
Tools/ProjectC/World/Spline Fence/1. Создать забор из выделенного сплайна     (приоритет 860)
Tools/ProjectC/World/Spline Fence/2. Пересобрать выделенные                  (861)
Tools/ProjectC/World/Spline Fence/3. Пересобрать все в сцене                 (862)
Tools/ProjectC/World/Spline Fence/4. Dry Run (лог без изменений)             (863)
Tools/ProjectC/World/Spline Fence/5. Снести сгенерированное                  (864)
```

**Окно `SplineFenceWindow`** (UI Toolkit — в проекте UI по умолчанию):
`SplineFence` (readonly, из выделения) · dropdown типа · поля параметров · `Rebuild` / `Dry Run` · сводка: секций, трис, боксов, предупреждения (узкие радиусы, шпильки, масштаб ≠ 1).

**Идемпотентность.** «Сгенерированное» = всё, что помечено `FenceSection` (или лежит в `<Fence>_COLLIDER`). Rebuild: собрать в память → валидировать → только потом снести старое и создать новое (не оставить пользователя с пустым забором при ошибке). `Undo.RecordObject` на `SplineFence`, `EditorSceneManager.MarkSceneDirty`, `AssetDatabase` для мешей.

**Автопересборка** — опция `autoRebuildOnSplineChange`, подписка на `Spline.Changed` (событие пакета, `SplineContainer.cs:194`) с дебаунсом; по умолчанию выключена (пересборка на каждый сдвиг узла в тяжёлой сцене — не то, что нужно).

**Публичный API для MCP/CI** (как `CombineMeshesToCollider.CombineInto`):

```csharp
SplineFenceTool.Rebuild(SplineFence fence, bool dryRun = false);   // без диалогов
SplineFenceTool.RebuildAllInScene(bool dryRun = false);
```

---

## 12. Бюджет

| Сценарий | Трис | Секций | Коллайдеров |
|---|---|---|---|
| `Bar`, 100 м | ≈ 12 000 | 1 | ≈ 40 панелей + 40 столбов |
| `Masonry`, 100 м | ≈ 4 000 | 1 (прямая) / N (дуга) | ≈ 38 + 38 |
| `Masonry` на дуге R = 10 м, 100 м | ≈ 4 500 | ≈ 14 | ≈ 40 + 54 |

Материалов 1–2 на секцию → DC держится SRP Batcher'ом. Дополнительных LOD в пилоте нет (забор — мелкий силуэт, дальний cull делает существующая система дистанций).

---

## 13. Чего в пилоте нет

* ворот и проёмов (в модели данных — будущее поле `List<Gap> gaps` по погонному метру);
* LOD / HLOD для забора;
* пересборки в рантайме (забор статичен, как весь мир);
* других типов (камень, бетонные плиты, дерево) — добавляются как новый `IFenceStyle` + `FenceTypeDefinition`, без правок пути;
* ручного сглаживания/подгонки узлов сплайна инструментом — художник правит сплайн сам.

---

## 14. Ловушки (проверено по проекту)

| Ловушка | Что делать |
|---|---|
| `com.unity.splines` **не в `manifest.json`** — приходит через `com.unity.cinemachine` (depth 2). Смена Cinemachine/фич-сета уронит компиляцию (`SplineWindZone.cs` и весь забор) | добавить явно: `"com.unity.splines": "2.9.0"` |
| Пространство имён `UnityEngine.Splines` | старые примеры в интернете пишут `Unity.Splines` — не компилируется |
| Мировые координаты в меш | только локальные координаты секции (ловушка из `COMBINE_MESHES_TO_COLLIDER.md` §2: FO сдвинет корень, геометрия улетит) |
| Масштаб корня ≠ 1 | едет процедурный кирпич/шум (§9.1) |
| Кирпич под углом к локальной X | кладка идёт по диагонали → правило секций (§7.2) |
| Нулевая касательная у `Linear`/`AutoSmooth` узла | считать угол по позициям, не по `EvaluateTangent` (§5.1) |
| `MeshCollider` вместо боксов | `Rigidbody` корабля туннелит через тонкие плоскости (`SETTLEMENT_COLLIDERS.md`) |
| `RecalculateNormals()` на всём меше | сгладит кромки балок и швы кладки |
| ProBuilder-умолчание «все вершины белые» | не касается нас, пока не используем ProBuilder (`EdgeMaskBaker.cs:42`) |
| Сцена | только `WorldScene_0_0.unity`; новых сцен и дублей не создавать |

Play Mode не запускаю: визуальную проверку делает пользователь.

---

## 15. Решения, которые стоит подтвердить до кода

| Вопрос | Рекомендация из этой ноты | Альтернатива |
|---|---|---|
| Кирпич: шейдер или настоящие блоки | **шейдер** (`M_PC_Brick`), геометрия только столбы/шапка | настоящие блоки = ×18 трис, зато честная геометрия швов |
| Забор следует рельефу | **да**: `Bar` наклоном, `Masonry` ступенями по венцу | фиксированная высота, ступень только на узлах-углах |
| Габариты пилота | `Bar` 1.9 м / `Masonry` 1.35 м + шапка | цифры художника |

---

## 16. Порядок реализации

1. ✅ `FencePath` + self-check на дуге и на 90° (без мешей): кадры, `BendAngleDeg`, `trim`.
   * `Assets/_Project/Scripts/World/Generation/SplineFence/FencePath.cs`
   * `Assets/_Project/Editor/World/SplineFenceProbe.cs` — меню `Tools → ProjectC → World → Spline Fence → 0. Self-check FencePath (лог)`
   * `FencePathSettings.Default`: `maxDeviation` 1 см, `sharpAngleDeg` 25°, `smoothAngleDeg` 2°, `breakAngleDeg` 150°, база излома 5 см, `maxStep` 2.5 м, `minStep` 5 см, `miterGap` 2 мм.
   * Замеры (12/12 PASS): прямая — шаг ровно 2.50 м (= `maxStep`); окружность R=10 — шаг 0.888…0.904 м при расчётных 0.894; R=2 — 0.398…0.403 м; 90° при P=0.06 — `bend` 90.00°, `cornerAngle` 90.00°, `trim` 0.0444 м, прогоны по 9.956 м; шпилька 180° — `Reversed`, `trim` 0, путь разорван; плавный 90° в `AutoSmooth` — `bend` 2.11° → `Smooth`, путь не рвётся; наклонный сплайн — все кадры `forward.y = 0`, `up = Vector3.up`.
2. ✅ `FenceMeshBuilder` — квады и коробки, UV0 в метрах, жёсткие нормали (без `RecalculateNormals`).
   * `Assets/_Project/Scripts/World/Generation/SplineFence/FenceMeshBuilder.cs`
   * Конвенция грани: фронт квада `(a,b,c,d)` = `cross(b-a, c-a)` (как у `Mesh.RecalculateNormals`); 4 вершины на грань, `RecalculateBounds` только для bounds.
   * Базис коробки: `az = cross(ax, ay)`, `ay` доортонормируется — наклонные балки не дают скошенных коробок.
   * UV0 в метрах: `U` вдоль `a→b`, `V` вдоль `a→d`. У боковых граней стены (±Z) это `U` = длина, `V` = высота — именно то, что нужно кладке; у горизонтальных граней (верх/низ) `V` приходится на длину, что для шапки/крошки не важно.
3. ✅ `BarFenceStyle` + `FenceTypeDefinition` + `SplineFence` + editor-инструмент — «чёрный» забор по сплайну.
   * `FenceBuildOutput.cs` — `FenceSectionMesh`, `FenceBoxSpec` (базис `axisX/axisY` как у меша), `IFenceStyle`, `FenceStyleBuilder` (там же точка расширения для `Masonry`).
   * `FenceTypeDefinition.cs` — SO-тип (`Create → ProjectC → World → Fence Type`), поля = параметры §6, `ToPathSettings()`.
   * `BarFenceStyle.cs` — столбы через `postSpacing` + ровно в торцах, угловой столб в секции прогона ПОСЛЕ узла, балки по кадрам (не хордами), прутки с шагом `barSpacing` и вычетом столба, коллайдеры: 1 бокс на столб + 1 бокс на панель.
   * `SplineFence.cs` + `FenceSection.cs` — дескриптор (`type`, `splineIndex`, `groundOffset`, `colliderLayer`, `fenceId`) и маркер сгенерированного.
   * `Assets/_Project/Editor/World/SplineFenceTool.cs` — меню `Tools → ProjectC → World → Spline Fence → 1. Создать забор из выделенного сплайна` / `2. Пересобрать выделенные` / `3. Сухой прогон` / `5. Снести сгенерированное`.
   * Меши пишутся в `Assets/_Project/Generated/Fences/<id>/`, коллайдеры в `<id>_COLLIDER`; пересборка идемпотентна (маркер `FenceSection` + пересоздание папки, чужие объекты не трогаются).
   * Ассет типа: `Assets/_Project/Data/World/Fences/FT_Fence_Bar_Steel.asset` (`primary` = `M_PC_Steel_v2`, `secondary` = `M_PC_Steel_v2_Dark`).
   * Замеры (17/17 PASS в self-check): прямая 10 м — 1116 трис, 5 столбов + 4 панели = 9 боксов, bounds `x −0.03…10.03`, `y 0…1.95`, `z ±0.03`; 90° — 2 секции, 19 боксов, столб в узле, торец подрезан; круг R=10 — один столб в шве (52 бокса); дуга R=8 — 6576 трис на 50.3 м (~13k/100 м); UV метричные (`maxU` 2.50 м = шаг кадра, `maxV` 2.50 м), 4 вершины на квад, NaN нет.
4. ⏳ Коллайдеры `<Fence>_COLLIDER` + idempotent Rebuild — сделано; осталось окно `SplineFenceWindow` и снос/пересборка «всех заборов в открытых сценах».
5. ✅ `MasonryFenceStyle` + правило секций/панелей (§7.2, §7.3) + `FT_Fence_Brick_Block.asset`.
   * `Assets/_Project/Scripts/World/Generation/SplineFence/MasonryFenceStyle.cs` — раскладка (столбы по стрелке хорды, привязка к `brickCell`), секции с собственной ориентацией, шапка/оголовки, боксы на столб и на панель. Подробно — §7.4.
   * `FenceTypeDefinition.cs` — поля кирпичного блока: `brickScale` 5.8, `panelChordError` 0.02, `sectionAlignDeg` 1, `copingHeight` 0.06, `copingOverhang` 0.025, `capSize` 0.45, `capHeight` 0.06 (+ клампы в `OnValidate`).
   * `FenceStyleBuilder` — ветка `FenceStyle.Masonry`; `default:` теперь ругается только на неизвестный стиль.
   * Ассет типа: `Assets/_Project/Data/World/Fences/FT_Fence_Brick_Block.asset` (`primary` = `M_PC_Brick`, `secondary` = `M_PC_Concrete`, `height` 1.35, `thickness` 0.25, столб 0.38/1.55, `postSpacing` 2.6, `colliderHeight` 1.41).
   * **Важно:** `FenceSectionMesh.localPosition/localRotation` (были в контракте с шага 3, но `Bar` их не использовал) теперь реально работают — на них держится ориентация кладки.
   * Замеры (22/22 PASS в self-check; T16–T20 — кирпич):
     * T16 прямая 10 м — 4 панели, 1 секция, 2 сабмеша, 9 боксов (5 столбов + 4 стены), модуль 0.1724 м, все полотна кратны модулю, макс. остаток 0.129 м < 0.19 (спрятан в столбе), bounds `x −0.23…10.23`, `y 0…1.61` (= `postHeight + capHeight`), `z ±0.23` (= `capSize/2`).
     * T17 дуга R=10 — 70 панелей/70 секций, худшая стрелка 0.0199 м при допуске 0.020 (панель всегда хорда), у всех секций локальные X и Z горизонтальны.
     * T18 90° — 2 секции, 17 боксов = 2×(4 столба + 4 стены) + 1 угловой, в узле ровно один столб, торец прогона подрезан (`bounds.max.x` < 10).
     * T19 замкнутый круг R=10 — 70 секций, в шве ровно 1 столб, всего 140 боксов.
     * T20 круг R=6 — при `panelChordError` 2 см 55 столбов, при 0.5 см 69: шаг столбов диктует геометрия дуги, а не флаг.
   * Живая сборка (edit mode, без Play): `FenceDemo_Brick` (0,0,0)→(12,0,0)→(12,0,8) — 2 прогона, 19.46 м, 2 секции, 408 трис, 17 боксов, 0 `MeshCollider`; секции встали как задумано (`Fence_run0_s0`: `X=+X`, `Z=+Z`; `Fence_run1_s0`: `localPos` (12, 0, 0.27), `X=+Z`, `Z=−X`), габариты меша и коллайдеров сходятся (коллайдер меньше ровно на свес оголовка).
6. ⏳ Проход по факту: масштаб корня, дуга R = 10 м, 90°, шпилька, рельеф; замер трис/DC. (Кирпич и прут уже прогнаны в edit mode — осталось посмотреть глазами в сцене и на реальном рельефе.)

---

## 17. Связанные документы

* `docs/world/COMBINE_MESHES_TO_COLLIDER.md` — world→local и сохранение мешей ассетами
* `docs/world/SETTLEMENT_COLLIDERS.md` — почему боксы, а не MeshCollider
* `docs/world/floatingorigin/00_ARCHITECTURE_AND_PLAN.md` — почему геометрия локальна
* `docs/world/Materials and shaders/EdgeDetection-Borderlands-Outline.md` — контекст процедурных графов
* `Assets/_Project/Scripts/Ship/SplineWindZone.cs` — рабочий пример `SplineContainer` в проекте
