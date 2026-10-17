# 07. Индустриальная практика: как это делается в Unity (и что из этого берём мы)

Дата: 2026-10-17. Источник — официальный мануал Unity 6 (6000.0), проверенный на нашем проекте
(`unity_reflect` подтвердил наличие API в сборках проекта).

---

## 1. Официальная таблица методов (URP) — что включать, что нет

| Метод | Тип | Многопоточный | **Рекомендация Unity для URP** |
|---|---|---|---|
| **SRP Batcher** | сокращает обновления render state | только с Graphics Jobs (DX12/Vulkan) | **Включить** |
| **GPU Resident Drawer** | GPU-инстансинг через `BatchRendererGroup` | да | **Включить** |
| `BatchRendererGroup` (BRG) вручную | GPU-инстансинг | да | Использовать GPU Resident Drawer (BRG — только для особых задач) |
| Галочка GPU Instancing в материалах | GPU-инстансинг | нет | **Отключить** (лишние шейдерные варианты) |
| **Batching (static batching)** | объединение мешей | нет | **Отключить** — несовместим с BRG и GPU Resident Drawer |

Дополнительно Unity советует: максимум **общих материалов**, вместо разных материалов —
**Material Variants**, не использовать `MaterialPropertyBlock` в URP/HDRP, а для множества объектов —
**LOD** (`lod-landing`).

Источник: `docs.unity3d.com/6000.0/Documentation/Manual/optimizing-draw-calls-choose-method.html`.

## 2. Что это значит для нас (важная поправка к плану)

1. **Объединение мешей мы делаем НЕ ради draw calls, а ради зернистости** (редактор, CPU на объект,
   память). Ручное слияние мешей — не то же самое, что «статический батчинг»: оно совместимо с
   GPU Resident Drawer, а вот **галочка Static Batching — нет** (её надо выключить, как советует Unity,
   и это идеально совпадает с FO: мир ездит, статик-флаги нам всё равно запрещены).
2. **Render-путь города = SRP Batcher + GPU Resident Drawer** (в URP это рекомендованный стек).
   Наличие API в проекте проверено: `UnityEngine.Rendering.GPUResidentDrawer`,
   `GPUResidentDrawerMode`, `GPUResidentDrawerSettings` — сборка `Unity.RenderPipelines.GPUDriven.Runtime`.
3. **Drawer требует Forward+** и включённых вариантов BRG (см. §3). Проверить и переключить.
4. **Drawer сам даёт GPU-окклюжн**: у него своя система GPU occlusion culling + поддержка Dynamic
   Occlusion — это ровно то, что нам нужно вместо запечённой окклюжн-карты (запечённая сломается
   при сдвиге мира).
5. **Инстансинг выгоден, когда меши общие.** У нас каждый дом собран из своих мешей, поэтому основной
   выигрыш дадут SRP Batcher (много объектов — мало материалов) и GPU-culling, а не инстансинг.
   Если позже понадобится — сделать «дедуп» модулей (одна дверь/окно/печь = один меш + инстансы).

## 3. Как включается GPU Resident Drawer в URP (пошагово, из мануала)

1. **Project Settings → Graphics → Shader Stripping → BatchRendererGroup Variants = Keep All**
2. **URP Asset:** проверить, что **SRP Batcher = On** (если свойства не видно — More ⋮ → Show All Advanced Properties)
3. **URP Asset:** **GPU Resident Drawer = Instanced Drawing**
4. **Universal Renderer:** **Rendering Path = Forward+**
5. Помогает (советы Unity): **Player → Other Settings → Static Batching = Off**;
   **Lighting → Lightmapping Settings:** Fixed Lightmap Size = On, Use Mipmap Limits = Off.

Требования: Forward+, графический API с compute shaders (не OpenGL ES), объекты с `MeshRenderer`.
Если условие не выполнено — Unity просто рисует объект без GPU-инстансинга (без ошибок).

## 4. Как измеряем (методика Unity, а не «на глаз»)

| Инструмент | Что смотрим |
|---|---|
| **Rendering Statistics** (`SetPass calls`, батчи, FPS) | главный числовой ориентир |
| **Frame Debugger** | группы `Hybrid Batch Group` = работа GPU Resident Drawer |
| **Profiler → Rendering** | draw calls, время CPU на кадр |
| **Rendering Debugger** | режимы отладки URP |
| **Профайлер памяти** | память мешей/ассетов до и после |

## 5. Референсы Unity на большие миры (доказательство подхода)

* **Fantasy Kingdom in Unity 6** — демо на GPU Resident Drawer (большая сцена, много объектов).
* **Megacity Metro** — демо на `BatchRendererGroup` (очень много объектов в кадре).
* **SRP Batcher: Speed up your rendering** (блог Unity) — почему SRP Batcher снимает основную
  нагрузку render state.

Вывод: «сотни тысяч объектов и миллионы полигонов» в Unity 6 решаются комбинацией
**GPU-driven рендера (Drawer/BRG) + LOD/HLOD + грамотной зернистости объектов**, а не выключением
дальнего и не дроблением на сцены.

## 6. Что из этого уже в нашем плане

| Практика Unity | Где в наших документах |
|---|---|
| SRP Batcher + GPU Resident Drawer | `03_UNITY_ASSEMBLY.md` §4 (добавить пункт про включение), `04_LOD_AND_DISTANCE.md` §5 |
| Не использовать Static Batching | `04` §5 + `05` §1 (FO и так запрещает static-флаги) |
| Material Variants вместо новых материалов | `03` §1 (материалы) — добавить как правило |
| LOD/HLOD/импостор | `04_LOD_AND_DISTANCE.md` целиком |
| Измерять SetPass/Profiler/Frame Debugger | `06_ACCEPTANCE.md` §1 (колонка «чем меряем») |
| Зернистость: блоки, объединение | `02_BLOCKS_AND_GEOMETRY.md`, `03` §4 |

---

## 7. Включено в проекте (17.10.2026, порт 7890)

| Настройка | Было | Стало | Где |
|---|---|---|---|
| SRP Batcher | вкл | вкл (без изменений) | `Assets/_Project/Settings/ProjectC_URP.asset` |
| **GPU Resident Drawer** | Disabled | **Instanced Drawing** | тот же URP-ассет (`m_GPUResidentDrawerMode`) |
| **GPU occlusion culling Drawer'а** | выкл | **вкл** | URP-ассет (`m_GPUResidentDrawerEnableOcclusionCullingInCameras`) |
| **Rendering Path** | Forward | **Forward+** | `ProjectC_URP_Renderer` (`m_RenderingMode = 2`) |
| **BatchRendererGroup Variants** | Strip All | **Keep All** | `ProjectSettings/GraphicsSettings.asset` (`m_BrgStripping = 2`) |
| Static Batching | выкл | выкл (уже было) | `ProjectSettings/ProjectSettings.asset` (`m_StaticBatching = 0`) |
| Dynamic Batching | выкл | выкл | там же |
| Граф. API | — | Direct3D12 (compute shaders ✅) | — |

Проверка после включения: консоль без ошибок рендера; мир (`WorldScene_0_0`) отрисовался как раньше
(кадр Scene View снят).

**Что меняется визуально:** Forward+ включает кластерное освещение — свет/тени могут отличаться от
прежнего Forward. Это ожидаемо и принимается: при необходимости адаптируем свет и материалы позже.

**Как это проверить в игре (пользователь):** Window → Analysis → Rendering Statistics — смотреть
`SetPass calls` и FPS; Frame Debugger — искать группы **Hybrid Batch Group** (это работа Drawer'а);
Profiler → Rendering — время CPU на кадр. Результаты фиксировать в `ITERATIONS.md`.