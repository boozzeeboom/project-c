# E. Open-world prior art: видимость и куллинг — и что делать с нашим городом

**Дата:** 2026-10-30 · **Автор:** субагент `prior-art` · **Статус:** draft для синтеза Lead
**Область:** prior art по visibility/culling в открытых мирах; ответ на вопрос «склеивать статику
в крупные батчи или оставлять объекты независимыми + инстансинг/GPU-куллинг»; двери и мелкие
интерактивные объекты; бюджеты draw calls и теней. Unity **6000.5.2f1**, URP **17.5.0**.

Пометки: `[VERIFIED]` — проверил по первоисточнику/коду; `[LIKELY]` — сильные косвенные данные;
`[UNVERIFIED]` — не проверял (сказано, как проверить). Все внешние ссылки открыты **2026-10-30**.

---

## 0. TL;DR для Lead

1. **Дилемма «merge vs instancing» ложная.** Индустрия держит 2–3 уровня гранулярности по дистанции:
   ближний — независимые/инстансированные объекты, отсекаемые поштучно; средний — HLOD по
   блокам/ячейкам; дальний — 1–2 прокси на город. Склейка не альтернатива куллингу, а **его
   предусловие**: она задаёт единицу отсечения. `[VERIFIED: UE HLOD; Nanite]`
2. **Наш merge (чанк × материал) — не ошибка, а неверная гранулярность.** 1 207 рендереров на 23
   чанка ⇒ единица отсечения ≈260×260 м — в 3–8 раз крупнее полезной для улицы; **дробить до блока
   60–80 м**, а не отменять склейку. `[LIKELY: расчёт §8]`
3. **Дорогое в кадре — не дома, а двери:** 17 766 из 21 118 DC (84 %) при 8.7 % треугольников —
   `1 главный проход + каскады` на каждый из 4 949 рендереров. `[VERIFIED: 04_…Research §3]`
4. **Двери лечатся инстансингом, а не склейкой:** **11 типов** префабов, 2 598 инстансов = 5 493
   рендерера и 13 471 слот; инстансинг по (mesh, material) даёт **десятки-сотни** DC вместо 17 766
   (порядок выигрыша сохраняется; «единицы» — только если типов 1–2). `[VERIFIED: данные Lead 30.10]`
5. **Бюджет «≤ 120 DC (L0)» для ближнего плана нереалистичен:** ориентиры AAA PC — GTA V **4 155** DC,
   DOOM 2016 **1 331**; для L0 разумно 1–3 тыс., «≤ 120» — бюджет L1/L2. `[VERIFIED; LIKELY: перенос]`
6. **GPU-driven — путь, где число объектов ≠ число draw calls:** Unity 6/URP 17.5
   `GPUResidentDrawer` = mip-пирамида глубин (Hi-Z) → compute → indirect; выключено
   (`m_GPUResidentDrawerMode: 0`). `[VERIFIED: исходники пакета]`
7. **Блокеров меньше, чем записано в проекте:** `m_RenderingMode: 2` = **Forward+**
   (`Forward=0, Deferred=1, ForwardPlus=2`) ⇒ требование Forward+ выполнено; мешают
   `m_BrgStripping: 0` (нужно «Keep All») и DOTS-инстансинг в шейдерах. `[VERIFIED]`
8. **Static batching фактически неактивен:** `m_StaticBatching: 1`, но 2 076 объектов со static-флагами
   **не имеют `MeshRenderer`** (BC_*-коллайдеры, корни, террейн) ⇒ на рендер не влияет;
   `m_BrgStripping: 0` — блокер GPU RD. `[VERIFIED: Lead 30.10]`
9. **Предвычисленная видимость в мире с FO — тупик** (кроме самописной в локальных координатах
   ячейки): Umbra печёт в пространстве сцены и требует static-флаги. Frame-local GPU-куллинг
   FO-safe по построению. `[VERIFIED локально; LIKELY: Umbra]`
10. **Тени — множитель:** `DC ≈ Σ N_видимых × (1 + K каскадов)`. GTA V: ~1000 DC на 4 каскада;
    DOOM кэширует статику карт теней; у нас 2 каскада × 4 949 дверей ≈ 15 тыс. сабмитов. `[VERIFIED]`

---

## 1. Вопросы и метод

Вопросы: (1) какие классы алгоритмов видимости используются и что каждый решает; (2) что решало
«много мелких объектов» в играх и какой ценой; (3) когда слияние оправдано; (4) как индустрия
обходится с дверями/пропсами; (5) какие бюджеты DC/кастеров — норма; (6) что применимо к нам.

Ограничения (чтобы Lead знал цену достоверности): **веб-поиск не работал** (нет API-ключа `deepseek`),
`browser` падал (`userRender is not a function`) ⇒ источники брал по известным адресам, а не поиском.
**Creation Kit wiki (первоисточник PreCombined/PreVis) недоступен:** `creationkit.com` →
`wiki.bethesda.net` (401), зеркало `ck.uesp.net` (403) ⇒ Fallout — `[LIKELY]` без цифр. Локальная
верификация — по файлам проекта и исходникам пакетов (`Library/PackageCache`, точная версия 17.5.0).
Мутаций нет: ни Play Mode, ни правок `Assets/`, ни MCP-записей.

---

## 2. Находки

### 2.1 Стоимостная модель

```
DC_кадр ≈ Σ по видимым объектам (1 главный проход + K каскадов теней, покрывающих объект)
        + отражения (cubemap/planar) + прозрачность + UI
```

Каждый **рендерер** платит `1 + K` раз ⇒ уменьшать надо число объектов, а не полигонов, и тени —
главный множитель.

| Кадр (разбор, PC) | Всего DC | Главный проход | Тени | Источник |
|---|---|---|---|---|
| **GTA V** (D3D11, 2015) | **4 155** | G-Buffer ≈1 900 | CSM **≈1 000** (4 карты в атласе 1024×4096) | `[VERIFIED]` adriancourreges.com/blog/2015/11/02/gta-v-graphics-study/ |
| **DOOM 2016** (Vulkan, GTX 980) | **1 331** | clustered forward 3072 кластера | атлас 8k×8k, статика **переиспользуется** | `[VERIFIED]` adriancourreges.com/blog/2016/09/09/doom-2016-graphics-study/ |

У GTA V ещё planar reflection 650 DC в 240×120, прозрачность 11, небо 1, cubemap 6×≈30 —
т.е. «много дешёвых проходов» вместо одного дорогого.

**Наш кадр** (`04_OCCLUSION_CULLING_RESEARCH.md` §3; центр города, fov 60, far 1000):

| Конфиг | Двери | DC | Тр., млн | FPS (равный кадр) |
|---|---|---|---|---|
| A: shadow 1000 / 2 каскада | вкл | **26 981–26 996** | 15.9–16.6 | 27.8 |
| B: shadow 250 / 1 каскад | вкл | **21 118** | 11.6 | 34.1 |
| A | выкл | 4 954 | 15.7 | 38.8 |
| B | выкл | **3 352** | 10.9 | 35.7 |

Разбор: ≈5 тыс. рендереров дверей × ~3.3–3.6 = 17 766 DC (84 % кадра B) при ~1 млн из 11.6 млн тр.
(8.7 %). Замер снят на 4 949 рендерерах; по данным Lead (30.10) их уже 5 493 (2 598 инстансов,
13 471 слот) — порядок сил тот же. Дорогое — число объектов, не геометрия. `[VERIFIED локально]`

### 2.2 Алгоритмы: что чем решается

**Frustum / distance / layer.** Линейно по рендерерам; в Unity считается на CPU для каждого
`Renderer`. Не помогает от окклюзии: фрустум оставляет 2 881 дверь, физически «видно» ~10
(`04_…` §5). `[LIKELY: стоимость; VERIFIED: числа]`

**Аппаратные occlusion queries.** DOOM: бокс вокруг группы мешей рисуется против depth-буфера — если
ни один пиксель не прошёл тест, группа скрыта. Результат **читается с задержкой на кадры** ⇒ алгоритм
консервативен, иначе popping `[VERIFIED: DOOM study]`. NVIDIA: мелкие запросы добавляют CPU-накладные
`[VERIFIED]` developer.nvidia.com/blog/advanced-api-performance-cpus/; в Unity/URP публичного
per-object API **нет** — только нативный плагин.

**Software occlusion (Intel Masked Occlusion Culling).** Разъединяет глубину и покрытие, работает по
иерархическому depth-буферу — «эффективная альтернатива HZB»; AVX2 (+SSE), binning + многопоточность,
на 4 потоках **≈3×** быстрее; запрос по bbox (`TestRect`) быстрее треугольника и «на практике даёт
лучший результат»; leakage — **часть алгоритма**; проект **заархивирован**, Apache 2.0 `[VERIFIED]`
github.com/GameTechDev/MaskedOcclusionCulling ⇒ CPU-растеризатор реалистичен и frame-local, но это
свой движковый код (C++/Burst + поток).

**Hi-Z / GPU-driven (наш главный путь).** Unity 6/URP 17.5, проверено по исходникам
`Library/PackageCache/com.unity.render-pipelines.core@…/Runtime/GPUDriven/` `[VERIFIED локально]`:
`IsInstanceOcclusionCullingEnabled()` — переключатель; `InstanceOcclusionTest(...)` — «run an occlusion
test in compute **to update indirect draws**»; `UpdateInstanceOccluders(...)` — «build a **mip pyramid of
closest depths**» (Hi-Z, compute-ядро `OccluderDepthDownscale`), URP-рендерер вызывает его в
`Runtime/UniversalRendererRenderGraph.cs`; валидатор требует **`BatchRendererGroup Variants =
Keep All`** (иначе «all DOTS instancing shaders will be stripped»). Механизм индустриальный:
**пирамида глубин → compute-тест по инстансам → indirect args**; CPU не сабмитит по объекту.

**Nanite (UE5)** — тот же принцип глубже: меш режется на **иерархические кластеры**, LOD
подменяется по кластерам без трещин, данные стримятся; Nanite **обходит традиционные draw calls**;
окклюзия **двухпроходная** (`NaniteStats`: бакеты **Main**/**Post**); **12 байт** на candidate-кластер,
**16 байт** на visible, лимиты `r.Nanite.MaxCandidateClusters`/`MaxVisibleClusters`; потолок
**16 млн инстансов**; нет distance culling, forward, MSAA; нужен DX12+SM6. В Valley of the Ancients
выключение Nanite даёт «**tens of thousands of regular draw calls**» `[VERIFIED]`
dev.epicgames.com/documentation/en-us/unreal-engine/nanite-virtualized-geometry-in-unreal-engine
(+`/nanite-technical-details`).

**Классика GPU-driven:** «GPU-Driven Rendering Pipelines», U. Haar & S. Aaltonen (Ubisoft), SIGGRAPH
2015 ARTR, 10.08.2015 `[VERIFIED: страница курса]` advances.realtimerendering.com/s2015/index.html.

### 2.3 Предвычисленная видимость: Umbra, порталы, PVS, ячейки

**Umbra.** Unity печёт через Umbra (**3.3.22**). В DOOM 2016 «**most of the occlusion culling** … is
done through the Umbra middleware», и **поверх** движок всё равно делает GPU occlusion queries
`[VERIFIED: DOOM]` — даже с предвычисленной видимостью AAA добавляет frame-local уточнение. Umbra =
данные в **пространстве сцены** + требование `OccluderStatic`/`OccludeeStatic`; у нас закрыто дважды:
технически (6 бейков, `04_…` §7) и архитектурно (FO: static-флаги запрещены, после ребейза данные
невалидны) `[VERIFIED локально]`.

**Порталы/PVS.** Классика: Airey/Rohlf/Frederick (UNC 1990), Teller & Séquin (SIGGRAPH 1991)
`[LIKELY: библиография]`. Работает для коридорных интерьеров; для улиц — тысячи порталов и ручная
разметка; Unity `Portal`/`OcclusionArea` требуют того же бейка.

**Ячейки + previs (Creation Engine)** `[LIKELY: первоисточник 30.10 недоступен]`: мир нарезан на
**cells**; статика ячейки слита в **PreCombined meshes** (наш merge, но движком и на ячейку); поверх —
**PreVis** («previsibines»); **интерактив (двери, контейнеры, скрипты, анимация) в precombine НЕ
попадает**; правка любого ref'а **инвалидирует** previs/precombine ячейки (отсюда «Previsibines
Repair Pack»). Это и подтверждение нашего подхода, и модель его рисков. У Skyrim (поколение раньше)
precombined мешей во внешнем мире нет — потому города были draw-call-тяжёлыми `[LIKELY]`.

### 2.4 HLOD, импосторы, стриминг

UE5 HLOD: «combine multiple Static Mesh Actors into a **single proxy mesh and Material** at long view
distances … reduces the number of draw calls … especially for large open worlds» `[VERIFIED]`
.../hierarchical-level-of-detail-in-unreal-engine — склейка **по дистанции и слою**, а не «всё в меш».

UE5 World Partition `[VERIFIED]` .../world-partition-in-unreal-engine: один persistent level, grid
cells (пример: **Cell Size 256 м**, **Loading Range 768 м**), streaming sources, OFPA, Data Layers,
HLOD-слои; «more than one grid can negatively impact performance». У нас единица стриминга — сцена
**80 км** (`SCENE_SIZE = 79999f`) ⇒ для города «всё уже загружено», вопрос только в отсечении.

### 2.5 Кейсы: что решало many-small-objects и какой ценой

| Кейс | Что делали | Компромисс | Нам |
|---|---|---|---|
| **GTA V** | отсечение+LOD в **compute shader**, фронт-в-бэк, дешёвые доп. проходы | много проходов, каждый дешёвый | GPU-отсечение реально на DX11 `[VERIFIED]` |
| **DOOM 2016** | **Umbra** + GPU queries поверх; кэш статических карт теней | PVS «коридорный»; латентность запросов | паттерн «precompute + frame-local уточнение» `[VERIFIED]` |
| **UE5 Nanite** | кластеры + GPU-отсечение + стриминг, обход draw calls | DX12/SM6, нет distance culling, вес данных | гранулярность **ниже объекта** `[VERIFIED]` |
| **UE5 WP + HLOD** | ячейки 256 м / 768 м; HLOD-прокси на дистанции | несколько сеток дорого | аналог «чанк/блок + HLOD» `[VERIFIED]` |
| **Fallout 4/76** | cells + **PreCombined** + **PreVis**, интерактив вне merge | правка ячейки ломает previs; моды пересобирают | **модель риска** нашего merge `[LIKELY]` |
| **Skyrim** | cells, комнаты/порталы в интерьерах | тяжёлые города | устаревшая база `[LIKELY]` |
| **Cities: Skylines / CS2** | тысячи мелких зданий; CS2 на DOTS | перф-проблемы на старте | **`[UNVERIFIED]`** |
| **Star Citizen** | **OCS**/SOCS — стриминг контейнеров и сущностей | сложность синхронизации | **`[UNVERIFIED]`** |
| **Unity GPU RD** | BRG-инстансинг + GPU-окклюзия (Hi-Z) + indirect | Forward+, Keep All, DOTS-шейдеры, static batching off | **наш путь** `[VERIFIED]` |
| **Unity Megacity / Fantasy Kingdom** | демо на BRG/GPU RD | демо ≠ игра | цифры **`[UNVERIFIED]`** |

### 2.6 Merge vs instancing+culling: правила

Склейка **определяет единицу отсечения**: какая единица нужна и сколько CPU-сабмитов я готов за неё
платить. **Merge оправдан:** объект не нужен по отдельности; дистанция (HLOD: город в 1–2 DC); единица
склейки = единица отсечения (ячейка/блок); редактор/CPU/память (наш случай 19 944 → 1 207).
**Вреден:** ближний план с пообъектной окклюзией (слитый блок нельзя скрыть частично);
интерактив/динамика; LOD (нельзя LOD'ить часть батча); разные материалы (=сабмеши: 1 806 слотов на
1 207 рендереров, 356 рендереров с >1 сабмешем); лимиты индексов (>65 535 вершин — 27 рендереров);
**огромный AABB** — почти всегда «в фрустуме», плохо окклюдится.

**Правила:** *merge by cell, not by city* (единица склейки = единица видимости) `[VERIFIED/LIKELY]`;
*внутри ячейки — по семейству материалов* (иначе сабмеши; у нас Э3.1 дал 53 материала) `[LIKELY]`;
*интерактив не склеивать* (Fallout) `[LIKELY]`; *дальний план — прокси, ближний — гранулярно*
(UE HLOD, Nanite clusters, GTA V LOD) `[VERIFIED]`; *число объектов ≠ число draw calls* `[VERIFIED]`.

### 2.7 Двери и мелкие интерактивные объекты

Приёмы, по убыванию частоты: **инстансинг** (один меш → много инстансов = 1 DC на проход; у нас
**11 типов**: `PLANK_A 956`, `PANEL_B 812`, `ARCH_C 419`, `DOUBLE_A 110`, `PALACE_A/B/C 143/7/77`,
`GATE_A/B 36/10`, `WIDE 27`, `NARROW 0`, `ADM 1`; 2 598 инстансов → 5 493 рендерера / 13 471 слот);
**тени off на дистанции** (80–150 м; кастеры — подмножество видимых); **LOD/импостор** (дверь в 200 м —
2–20 пикселей); **групповое отсечение** по дому/блоку (`PRIMUM_DOORS/<чанк>/`); **отдельный
проход/слой** без доп. эффектов; **portal/room culling** — только интерьеры. «Proxy door» (дверь
запечена в фасад, интерактивный объект поднимается вблизи) — известный приём, в Unity не проверял. `[LIKELY]`

Порядок величин: мелкие пропсы в AAA-городе — **сотни-тысячи инстансов, десятки DC**, кастеры
теней от них — доли общего числа; наши 17 766 DC хуже нормы на 2–3 порядка из-за отсутствия
инстансинга. `[LIKELY]`

### 2.8 Бюджеты теней

`DC_shadow ≈ Σ по каскадам (число кастеров в каскаде)`; вне `shadowDistance` кастеры не рисуются.
GTA V: CSM 4 каскада в атласе 1024×4096 = **≈1000 DC** (≈24 % кадра) `[VERIFIED]`; DOOM: атлас 8k×8k,
статические карты **переиспользуются**, перерисовывается только динамика `[VERIFIED]`; у нас
1000 м / 2 каскада / 2048 и 17 766 DC на двери (84 % кадра) `[VERIFIED локально]`.

Правила: дистанция теней для мелких пропсов **короче**, чем для крупных форм (один `shadowDistance` —
компромисс в пользу крупных); кастеры — подмножество (по LOD/дистанции); нельзя одновременно
`shadowDistance 1000` и тысячи мелких кастеров. Наш A/B: урезание теней дало **+22.7 % FPS** — это
и есть цена «теней для всего».

---

## 3. Принципы (оценка применимости 1–5) и матрица

| # | Принцип | Фит |
|---|---|---|
| P1 | Не откатывать merge; **дробить единицу склейки** до блока 60–80 м; 2 уровня представления (ближний гранулярный / дальний прокси) | **5** |
| P2 | **Инстансировать** двери/лампы/пропсы (одна геометрия × много инстансов) | **5** |
| P3 | Включить GPU-путь (GPU RD + Keep All + DOTS-шейдеры + GPU occlusion) | **5** |
| P4 | Тени: кастеры — подмножество; `shadowCastingMode` off за 80–150 м | **5** |
| P5 | Дальний план (5–80 км) — HLOD/импостор: город в 1–2 DC; LODGroup по L0/L1 (сейчас 0) | **4** |
| P6 | Групповое отсечение по чанку/блоку/дому, а не по одиночному объекту | **4** |
| P7 | Видимость считать **frame-local** (FO-safe), ничего не печь в мировых координатах | **5** |
| P8 | Если печь — только **в локальных координатах ячейки** (свой cell-PVS) | **3** |
| P9 | Не склеивать интерактив/интерьеры/корабли; merge только для статики | **5** |
| P10 | Мерить число **видимых объектов/инстансов**, а не только DC и FPS | **4** |

| Техника | Что решает | Precompute/память | Runtime | FO | Фит |
|---|---|---|---|---|---|
| Frustum culling (built-in) | внеэкранное | нет | O(n) CPU | ✅ | 5 |
| Distance/layer (свой) | классы объектов | нет | копейки | ✅ | 5 |
| **GPU instancing / BRG / GPU RD** | объекты → батчи | настройка шейдеров | GPU, indirect | ✅ | **5** |
| **GPU occlusion (Hi-Z)** | «за стеной не рисуем» | depth pyramid на кадр | compute+indirect | ✅ | **5** |
| Static merge (текущий) | редактор/CPU/память | меши (десятки МБ) | 1 DC/батч/проход | ✅ | 3 (дробить) |
| HLOD-прокси / импосторы | дальний план, силуэт | офлайн+меши/атласы | 1–2 DC | ✅ | 4 |
| LODGroup | полигоны по дистанции | LOD-меши | штатный | ✅ | 4 |
| Свой cell-PVS (локальные коорд.) | окклюзия без бейка | офлайн (МБ) | lookup | ⚠️ | 3 |
| Umbra (встроенный) | окклюзия «из коробки» | бейк + data | CPU-тест | ❌ | **1** |
| Аппаратные queries (плагин) | bbox-окклюзия | нет | латентность 1+ кадр | ✅ | 3 |
| Software occlusion (MOC-подобный) | окклюзия на CPU | нет | свой поток/Burst | ✅ | 3 |

---

## 4. Что это значит для Project C

### 4.1 Текущее состояние (проверено 30.10)

| Параметр | Значение | Комментарий |
|---|---|---|
| `ProjectC_URP_Renderer.asset → m_RenderingMode` | **2** | `Forward=0, Deferred=1, ForwardPlus=2` ⇒ **сейчас Forward+**, требование GPU RD выполнено `[VERIFIED]` |
| `ProjectC_URP.asset → m_GPUResidentDrawerMode` | 0 | GPU-путь выключен |
| `m_GPUResidentDrawerEnableOcclusionCullingInCameras` | 0 | GPU-окклюзия выключена |
| `GraphicsSettings.asset → m_BrgStripping` | **0** | валидатор требует «Keep All» — **блокер** `[VERIFIED]` |
| `ProjectSettings.asset → m_StaticBatching` | **1** | 2 076 static-объектов **без `MeshRenderer`** (BC_*, корни, террейн) ⇒ batching неактивен, на рендер не влияет `[VERIFIED: Lead 30.10]` |
| shadowDistance / каскады / shadowmap | 1000 / 2 / 2048 | множитель кадра дверей |
| SRP Batcher | вкл | снижает state changes, **не** число сабмитов |

### 4.2 Город: что делать со склейкой

Не откатывать: 1 207 рендереров вместо 19 944 — правильно по CPU/редактору. **Дробить единицу
склейки:** батчи «чанк × материал» имеют AABB в сотни метров и почти не окклюдятся; цель — **блок
60–80 м × материал**, ~400–900 батчей вместо 761 при тех же треугольниках; при GPU RD число батчей
перестаёт быть числом DC `[LIKELY → E-3]`. Дальний план — HLOD/импостор (сейчас нет). `Terrain_0_0`
без `MeshRenderer` ⇒ Umbra-окклюдером быть не может; для Hi-Z — открытый вопрос.

### 4.3 Двери (самый дешёвый выигрыш)

**Инстансинг** по (mesh, material): 11 типов ⇒ ~25–35 батчей × 3 прохода ≈ 75–105 DC вместо 17 766
(если створки открываются — плюс батчи по состояниям, «сотни»); **тени off** за 80–150 м (даже без
инстансинга режет 2/3); **групповое отсечение** по чанку/блоку; **LOD/импостор**, в пределе
«proxy door» `[LIKELY]`.

### 4.4 Корабли

Не склеивать интерьер в один меш: модули отдельными, отсечение по отсекам/палубам, тени внутренних
модулей off, на дистанции — HLOD-прокси корпуса. `CombineMeshesToCollider.cs` — **только для физики**,
не для рендера `[LIKELY]`.

### 4.5 Floating Origin

**Нельзя:** предвычисленные данные в мировых координатах (Umbra, PVS по мировым позициям, кэши
мировых `Vector3`) — Umbra ещё и требует static-флаги. **Можно:** всё frame-local (frustum, distance,
GPU Hi-Z, аппаратные/программные запросы). **Можно с оговоркой:** свой cell-PVS в **локальных
координатах ячейки** — ребейз его не инвалидирует `[LIKELY: контракт FO]`.

---

## 5. Рекомендованные эксперименты

Методика: равный `frameCount` (FPS первых ~1500 кадров врёт, `04_…` §2), `UnityStats.drawCalls` ±15,
точка — центр города `(39593.1, 2509.6, 39833.8)`, yaw 337.5°, fov 60, far 1000.
| # | Эксперимент | Ожидание | Kill criteria |
|---|---|---|---|
| **E-1** | Инстансинг дверей (4 меша + матрицы) | 21 118 → ≤4 000 DC (конфиг B) | FPS не вырос ≥20 % ⇒ узкое место не в сабмитах |
| **E-2** | DOTS-шейдеры → `BrgStripping=2` → Drawer+occlusion | DC кратно вниз, `Hybrid Batch Group` в Frame Debugger | шейдеры не заводятся за 2–3 дня ⇒ откат на E-1/E-3 |
| **E-3** | Дробление батчей чанка до блока 60–80 м | тр. в кадре −40…−80 % на улице | DC растут быстрее, чем падают тр. ⇒ дробить фасады |
| **E-4** | `shadowCastingMode=Off` для дверей/пропсов за 100–150 м | −10…−15 тыс. DC | артефакты теней |
| **E-5** | HLOD/импостор дальнего плана | 1–2 DC на город с 3–10 км | силуэт «плывёт» ⇒ гибрид HLOD+импостор |
| **E-6** | cell-PVS в локальных координатах (после E-2/E-3) | видимость ≈ физпрокси (99 % скрыто) | точность <90 % или >50 МБ на город ⇒ нет |

---

## 6. Риски и открытые вопросы

1. **GPU RD заблокирован шейдерами** (`DOTS_INSTANCING_ON`); это уже стоило отката 18.10
   (`01_INDUSTRY_PRACTICE.md` §8) — оценка работ за отчётом по шейдерам.
2. **Инстансинг дверей и состояние:** открытая створка требует per-instance матрицы (свой BRG-батч);
   `Graphics.DrawMeshInstanced` не даёт per-instance материал.
3. **Hi-Z: латентность и утечки** ⇒ возможен popping на быстрых поворотах, нужен консервативный запас.
4. **Террейн как окклюдер** — не решён; проверять `OccluderParameters`/`occluderSubviewUpdates` `[UNVERIFIED]`.
5. **FO + предвычисление:** формат данных закладывать локальным с самого начала.
6. **Бюджет ≤120 DC (L0)** несовместим с ближним планом из отдельных домов — нужно решение Lead.
7. **Нет метрики «видимые объекты»** — добавить счётчик, иначе эксперименты не сравнимы.
8. Cities: Skylines, Star Citizen, цифры демо Unity — `[UNVERIFIED]`, добирать при необходимости.

---

## 7. Источники (все открыты 2026-10-30)

**Внешние (открыты 2026-10-30; факты из них — в §2):**
1. `[VERIFIED]` GTA V — Graphics Study (02.11.2015, PC D3D11).
   <https://www.adriancourreges.com/blog/2015/11/02/gta-v-graphics-study/>
2. `[VERIFIED]` DOOM 2016 — Graphics Study (09.09.2016, Vulkan/GTX 980).
   <https://www.adriancourreges.com/blog/2016/09/09/doom-2016-graphics-study/>
3. `[VERIFIED]` Intel `MaskedOcclusionCulling` (README, архив; paper «Masked Software Occlusion Culling»,
   GDC 2018, Apache 2.0). <https://github.com/GameTechDev/MaskedOcclusionCulling>
4. `[VERIFIED]` SIGGRAPH 2015 ARTR, «GPU-Driven Rendering Pipelines» (U. Haar, S. Aaltonen, 10.08.2015).
   <https://advances.realtimerendering.com/s2015/index.html>
5. `[VERIFIED]` Epic, Nanite Overview (UE 5.8).
   <https://dev.epicgames.com/documentation/en-us/unreal-engine/nanite-virtualized-geometry-in-unreal-engine>
6. `[VERIFIED]` Epic, Nanite Technical Details.
   <https://dev.epicgames.com/documentation/en-us/unreal-engine/nanite-technical-details>
7. `[VERIFIED]` Epic, HLOD.
   <https://dev.epicgames.com/documentation/en-us/unreal-engine/hierarchical-level-of-detail-in-unreal-engine>
8. `[VERIFIED]` Epic, World Partition.
   <https://dev.epicgames.com/documentation/en-us/unreal-engine/world-partition-in-unreal-engine>
9. `[VERIFIED]` NVIDIA, Advanced API Performance: CPUs (17.05.2023).
   <https://developer.nvidia.com/blog/advanced-api-performance-cpus/>
10. `[LIKELY]` Creation Kit wiki (PreCombined/PreVis) — **недоступно** 30.10.2026 (401 на
    `wiki.bethesda.net`, 403 на `ck.uesp.net`).
11. `[LIKELY]` PVS: Airey/Rohlf/Frederick (UNC 1990); Teller & Séquin (SIGGRAPH 1991).
12. `[UNVERIFIED]` Cities: Skylines/CS2; Star Citizen OCS/SOCS; цифры демо Unity.

**Локальные:** `06_REFERENCE/04_OCCLUSION_CULLING_RESEARCH.md` (замеры, 6 бейков Umbra 3.3.22);
`06_REFERENCE/01_INDUSTRY_PRACTICE.md` (политика Unity, откат GPU-пути 18.10); `README.md` §3
(1 207 / 4 949 / 53); `ROADMAP.md` §2.1, §DoD; `optimization/LOD_VIEW_DISTANCE_DESIGN.md`;
`Assets/_Project/Settings/ProjectC_URP.asset`, `ProjectC_URP_Renderer.asset`;
`ProjectSettings/ProjectSettings.asset`, `GraphicsSettings.asset`;
`Library/PackageCache/com.unity.render-pipelines.core@…/Runtime/GPUDriven/*`;
`Library/PackageCache/com.unity.render-pipelines.universal@…/Runtime/UniversalRenderer.cs`,
`Runtime/UniversalRendererRenderGraph.cs`.

---

## 8. Приложение: расчёты

```
дверь в кадре: 1 main + K каскадов (K≈2 при shadow 1000/2)
  ≈5 тыс. рендереров × ~3.3 = 17 766 DC (конфиг B); DC 84.1 %, тр. 8.7 %
после инстансинга по (mesh, material): ~11 типов × ~2–3 материала ≈ 25–35 батчей
  × 3 прохода ≈ 75–105 DC ⇒ −17.7 тыс. DC
единица отсечения города: 1.4×1.1 км / 23 чанка ≈ 260×260 м; уличная видимость ≈ 30–80 м
  ⇒ текущая единица в 3–8× крупнее полезной
```

## Handoff

**Главный вывод:** индустрия не выбирает «merge или куллинг» — она держит уровни гранулярности по
дистанции. Наша проблема не в факте склейки, а в её **чрезмерной крупности** (единица ≈260 м) и в
**отсутствии инстансинга** (11 типов дверей на 5 493 рендерера).

**Три факта:** (1) двери = 17 766 из 21 118 DC (84 %) при 8.7 % треугольников; (2) Unity 6/URP 17.5 уже
содержит Hi-Z GPU-окклюзию (`GPUResidentDrawer`: mip-пирамида → compute → indirect), но она выключена;
`m_RenderingMode: 2` = **Forward+**, `m_BrgStripping: 0` — блокер; (3) ориентиры AAA-кадра: GTA V
4 155 DC, DOOM 1 331 DC — и оба не «склеили всё в один меш».

**Непроверено:** Fallout 4/76 PreCombined/PreVis (первоисточник недоступен), Cities: Skylines, Star
Citizen OCS, цифры демо Unity; поведение GPU RD с нашими шейдерами; террейн как окклюдер в Hi-Z.
