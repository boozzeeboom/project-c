# Edge Detection — Borderlands-style outline (post-process)

> **URP 17.5 / Unity 6** — полноэкранный пост-процесс обводки в стиле Borderlands.
> Sobel-фильтр по depth + normal текстурам с pencil-jitter.

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
Камера → [Opaque рендер] → [EdgeDetection Pass] → [Transparent / UI / Post-process]
                                  ↑
                    ConfigureInput(Normal | Depth)
                    → URP генерирует _CameraNormalsTexture
                                  ↓
                    Sobel 3×3 по глубине (Linear01Depth)
                    Sobel 3×3 по нормалям (SampleSceneNormals)
                                  ↓
                    max(depthEdge, normalEdge) + pencil jitter
                                  ↓
                    Blend SrcAlpha OneMinusSrcAlpha поверх сцены
```

- **Depth edges**: силуэты объектов и складки (где глубина резко меняется)
- **Normal edges**: внутренние рёбра геометрии — hard edges, creases (то, чего нет у inverted hull)
- **Pencil jitter**: лёгкое дрожание линии для «рисованого» эффекта

UV вычисляется в фрагментном шейдере из `SV_POSITION / _ScreenParams` — автоматически корректный Y-flip для DirectX/OpenGL.

---

## Параметры в инспекторе RenderFeature

| Параметр | Дефолт | Описание |
|---|---|---|
| `Edge Color` | `(0.05, 0.05, 0.07, 1)` | Цвет линии |
| `Edge Width` | `2` | Толщина линии в пикселях (1-8) |
| `Depth Sensitivity` | `2.5` | Множитель глубинных граней |
| `Depth Threshold` | `0.06` | Порог срабатывания depth-Sobel |
| `Normal Sensitivity` | `1.5` | Множитель normal-граней |
| `Normal Threshold` | `0.08` | Порог срабатывания normal-Sobel |
| `Jitter Amount` | `0.0` | Сила pencil-дрожания (0 = ровные линии) |
| `Jitter Scale` | `8.0` | Частота шума для jitter |
| `Line Softness` | `0.06` | Мягкость края линии |
| `Override Material` | пусто | Опционально: свой материал (иначе создаётся из шейдера) |

---

## Параметры материала (`M_EdgeDetection.mat`)

Все параметры дублируются в материале — можно править через MaterialPropertyBlock из кода:

```
_EdgeColor         — цвет линии
_EdgeWidth         — толщина
_DepthSensitivity  — чувствительность глубины
_DepthThreshold    — порог глубины
_NormalSensitivity — чувствительность нормалей
_NormalThreshold   — порог нормалей
_JitterAmount      — pencil-дрожание
_JitterScale       — частота шума
_LineSoftness      — мягкость края
```

---

## Per-object обводка (TargetOutline)

Для гарантированного жирного силуэта на конкретном объекте используется отдельная система — **inverted hull**:

| Файл | Назначение |
|---|---|
| `Assets/_Project/Shaders/TargetOutline.shader` | Inverted-hull шейдер (Cull Front + extrusion) |
| `Assets/_Project/Resources/Materials/M_TargetOutline.mat` | Материал для TargetOutline |

### Как применить на персонаже

У `SkinnedMeshRenderer` (или `MeshRenderer`) есть массив **Materials**. Если добавить материал сверх числа submesh'ей — он отрендерит меш повторно:

1. Выделить объект с мешем
2. В инспекторе → **Materials** → нажать **+**
3. В новый слот перетащить `M_TargetOutline`
4. Настроить параметры в материале:
   - `_OutlineColor` — цвет силуэта
   - `_OutlineWidth` — толщина

Удаление: выделить слот → **−** или выбрать **None**.

---

## Сравнение двух систем

| | EdgeDetection (post-process) | TargetOutline (per-object) |
|---|---|---|
| Покрытие | Вся сцена | Конкретный меш |
| Тип граней | Силуэт + внутренние рёбра | Только силуэт |
| Как включить | URP Renderer → Add Feature | Второй материал на рендерере |
| Стиль | Тонкий, pencil, Borderlands | Жирный, гарантированный контур |

---

## Известные особенности

- **`ConfigureInput(Normal | Depth)` обязателен** — без него `_CameraNormalsTexture` не генерируется → normal-Sobel не работает.
- **`AccessFlags.ReadWrite` обязателен** — `Write` сбрасывает содержимое буфера → сцена затирается.
- **Не использовать `SetViewProjectionMatrices`** — меняет глобальные матрицы → ломает все последующие пассы (transparent, post-process).
- UV вычисляется из `SV_POSITION` для автоматического Y-flip под платформу.
- Если normal-текстура недоступна — шейдер пропускает normal-Sobel (guard против нулевых нормалей).
