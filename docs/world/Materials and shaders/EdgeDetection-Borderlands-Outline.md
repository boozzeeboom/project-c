# Edge Detection — Borderlands-style outline (post-process)

> **URP 17.5 / Unity 6** — полноэкранный пост-процесс обводки в стиле Borderlands.
> Sobel-фильтр по depth + normal текстурам. Distance falloff, adaptive color, pencil stroke.

---

## Файлы

| Файл | Назначение |
|---|---|
| `Assets/_Project/Shaders/EdgeDetection.shader` | HLSL-шейдер: Sobel depth + normal edge detection |
| `Assets/_Project/Scripts/Core/EdgeDetectionRenderFeature.cs` | `ScriptableRendererFeature` + `ScriptableRenderPass` (RenderGraph) |
| `Assets/_Project/Materials/M_EdgeDetection.mat` | Готовый материал с дефолтными параметрами |

---

## Как включить

1. Найти URP Renderer ассет: `Assets/_Project/Settings/ProjectC_URP_Renderer.asset`
2. В инспекторе: **Add Renderer Feature → Edge Detection**
3. Настроить параметры (см. ниже)

Готово — обводка рисуется на всей геометрии в кадре.

---

## Как работает

```
Камера → [Opaque рендер] → [CopyColorToTemp] → [EdgeDetection Pass] → [Transparent / UI / Post-process]
                                  ↑
                    ConfigureInput(Normal | Depth)
                    → URP генерирует _CameraNormalsTexture
                                  ↓
                    Sobel 3×3 по глубине (Linear01Depth)
                    Sobel 3×3 по нормалям (SampleSceneNormals)
                                  ↓
                    max(depthEdge, normalEdge)
                    → adaptive color: sample _EdgeSourceTex (копия сцены до прохода)
                    → pencil stroke: tapered ends
                    → distance falloff: thinner with depth
                                  ↓
                    Blend SrcAlpha OneMinusSrcAlpha поверх сцены
```

- **Depth edges**: силуэты объектов (где глубина резко меняется)
- **Normal edges**: внутренние рёбра геометрии — hard edges
- **Distance falloff**: линия истончается с удалением, исчезает на `Max Edge Distance`
- **Adaptive color**: обводка затемняет цвет объекта (семплит копию сцены до прохода)
- **Pencil stroke**: линия сужается к концам грани (как нажим карандаша)

---

## Параметры в инспекторе RenderFeature

### Edge
| Параметр | Дефолт | Описание |
|---|---|---|
| `Edge Color` | `(0.02, 0.02, 0.04, 1)` | Цвет линии |
| `Edge Width` | `1.5` | Толщина (0.1–8.0, float) |

### Distance Falloff
| Параметр | Дефолт | Описание |
|---|---|---|
| `Max Edge Distance` | `80` | На каком расстоянии (метры) линия исчезает |
| `Depth Falloff` | `0.8` | Крутизна затухания (0 = плавно, 2 = резко) |

### Depth Edges
| Параметр | Дефолт | Описание |
|---|---|---|
| `Use Depth Edges` | ✔ | Вкл/выкл depth-Sobel |
| `Depth Sensitivity` | `2.0` | Множитель глубинных граней |
| `Depth Threshold` | `0.04` | Порог срабатывания |

### Normal Edges
| Параметр | Дефолт | Описание |
|---|---|---|
| `Use Normal Edges` | ✔ | Вкл/выкл normal-Sobel |
| `Normal Sensitivity` | `0.8` | Множитель normal-граней |
| `Normal Threshold` | `0.25` | Порог срабатывания (высокий — только на hard edges) |

### Adaptive Color
| Параметр | Дефолт | Описание |
|---|---|---|
| `Use Adaptive Color` | ☐ | Вкл: обводка цвета объекта |
| `Adaptive Strength` | `0.6` | 0 = Edge Color, 1 = цвет объекта ×0.35 |

### Pencil Stroke
| Параметр | Дефолт | Описание |
|---|---|---|
| `Use Pencil Stroke` | ☐ | Вкл: линия сужается к концам |
| `Taper Amount` | `0.7` | Сила сужения (ищет концы грани через Sobel direction) |
| `Grain Strength` | `0.08` | Текстурная зернистость (0–0.3) |

### Softness
| Параметр | Дефолт | Описание |
|---|---|---|
| `Line Softness` | `0.03` | Мягкость края линии |
| `Override Material` | — | Свой материал (иначе auto-create) |

---

## Параметры материала (`M_EdgeDetection.mat`)

```
_EdgeColor          — цвет линии
_EdgeWidth          — толщина (float, 0.1–8.0)
_MaxEdgeDistance    — дистанция исчезновения
_DepthFalloff       — крутизна distance falloff
_UseDepthEdges      — 0/1
_DepthSensitivity   — чувствительность глубины
_DepthThreshold     — порог глубины
_UseNormalEdges     — 0/1
_NormalSensitivity  — чувствительность нормалей
_NormalThreshold    — порог нормалей
_UseAdaptiveColor   — 0/1
_AdaptiveStrength   — сила адаптивного цвета
_UsePencilStroke    — 0/1
_PencilTaper        — сила сужения концов
_PencilGrain        — зернистость
_LineSoftness       — мягкость края
```

---

## Per-object обводка (TargetOutline)

Для гарантированного жирного силуэта на конкретном объекте — **inverted hull**:

| Файл | Назначение |
|---|---|
| `Assets/_Project/Shaders/TargetOutline.shader` | Inverted-hull шейдер (Cull Front + extrusion) |
| `Assets/_Project/Resources/Materials/M_TargetOutline.mat` | Материал для TargetOutline |

### Применение на меше

1. Выделить объект с `SkinnedMeshRenderer`/`MeshRenderer`
2. Инспектор → **Materials** → **+**
3. В новый слот перетащить `M_TargetOutline`
4. `_OutlineColor` / `_OutlineWidth` — настройка

---

## Сравнение двух систем

| | EdgeDetection (post-process) | TargetOutline (per-object) |
|---|---|---|
| Покрытие | Вся сцена | Конкретный меш |
| Тип граней | Силуэт + внутренние рёбра | Только силуэт |
| Как включить | URP Renderer → Add Feature | Второй материал на рендерере |
| Стиль | Тонкий, pencil, Borderlands | Жирный, гарантированный контур |

---

## Детали реализации

### Vertex shader
Используется `GetFullScreenTriangleVertexPosition(vertexID)` — стандартная SRP-функция, работает с любыми VP-матрицами. Не требуется `SetViewProjectionMatrices`.

### Adaptive color
Копия сцены создаётся через `RenderGraph.AddCopyPass` (до прохода edge detection) и передаётся в шейдер как `_EdgeSourceTex`:

```csharp
var sourceTex = renderGraph.CreateTexture(desc);
RenderGraphUtils.AddCopyPass(renderGraph, colorTarget, sourceTex, "CopyColorForEdge", false);
// ...
builder.UseTexture(sourceTex, AccessFlags.Read);
builder.AllowGlobalStateModification(true);
// в render func:
ctx.cmd.SetGlobalTexture(Shader.PropertyToID("_EdgeSourceTex"), data.SourceTex);
```

### Pencil stroke (tapered ends)
`SobelDepthDir` вычисляет X/Y градиенты Sobel → направление грани. `PencilTaper` семплит силу грани вдоль направления в обе стороны — если грань обрывается (конец), плавно сужает линию.

### Distance falloff
`thickness = EdgeWidth × (1 − depth / MaxEdgeDistance)^DepthFalloff`. На `MaxEdgeDistance` метрах линия полностью исчезает.

### Known issues fixed
- **`SetViewProjectionMatrices`** — не используется. Меняло глобальные матрицы → ломало пост-процессинг.
- **`AllowGlobalStateModification(true)`** — обязателен для `SetGlobalTexture` в RenderGraph.
- **`builder.UseTexture`** — обязателен для регистрации `_EdgeSourceTex` в графе рендера.
- **Normal Threshold 0.25** (было 0.08) — не триггерит на flat-поверхностях.
