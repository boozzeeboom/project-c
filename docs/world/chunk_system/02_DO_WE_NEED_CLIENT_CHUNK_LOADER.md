# 02. Нужен ли клиентский чанк-лоадер и поможет ли он в оптимизации

Дата: 2026-09-27. Ответ: **в виде legacy 2K-чанк-стриминга — нет; в виде пяти отдельных функций —
частично да, но три из них уже покрыты другими системами.**

## 1. Распаковка термина: «чанк-лоадер» — это пять разных функций

Слово «чанк-лоадер» смешивает независимые задачи. Разбираем по одной — иначе спор «нужен/не нужен» бессмыслен:

| # | Функция | Что делает | Есть ли сейчас | Нужна ли |
|---|---|---|---|---|
| F1 | **Выгрузка геометрии** (unload далёкого) | Убрать из памяти/сцены то, что далеко | Частично (сцены целиком через `ClientSceneLoader`; внутри сцены — нет) | Только когда докажет профайлер (см. §3) |
| F2 | **Загрузка контента по требованию** (load по позиции) | Догрузить регион при приближении | Да, на уровне сцен (3×3, preload 10 км); на уровне пропсов — нет и не надо пока | Нет (1 сцена) → да при 2+ сценах, но на уровне **сцен**, не 2K-чанков |
| F3 | **Визуальное удешевление дали** (LOD/фон) | Дальнее рисуется дёшево, но не исчезает | **Да** (T-LOD01: `ViewDistanceApplier`, `DetailDistanceCuller`, LOD-группы гор) | Да — развивать здесь, а не в чанках |
| F4 | **Сетевой interest** (кому что реплицировать) | Сервер решает, что видит каждый клиент | Зачаток (`ServerSceneManager` Hide/Show) | Да — hardening, без чанк-RPC |
| F5 | **Генерация мира** (procedural build) | Построить геометрию из сида | **Не нужна**: мир authored (`Terrain_0_0`) | Нет — и это главный аргумент против legacy |

Ключевая мысль дизайна (`docs/world/optimization/LOD_VIEW_DISTANCE_DESIGN.md:9-12`):
**дальний террейн — фон, а не off**. F1 (выгрузка) убивает картинку; F3 (удешевление) сохраняет.
Пользователю нравится именно «фон» — его и защищаем.

## 2. Почему legacy-схема 2000 вредна именно нам (6 причин)

Legacy: `WorldChunkManager.ChunkSize = 2000` (`WorldChunkManager.cs:83`), радиусы 2/3
(`WorldStreamingManager.cs:53-64`, `PlayerChunkTracker.cs:22-27`), генерация гор/облаков/ферм
из сида (`ProceduralChunkGenerator.cs`), fade 1.5 с (`ChunkLoader.cs:34-36`), серверные
`LoadChunkClientRpc` (`PlayerChunkTracker.cs:334-374`), спавн сундуков на чанк (`ChunkNetworkSpawner.cs`).

1. **Конфликт с authored миром.** `Terrain_0_0` — ручная работа: база, герои 2800–5000, 32 пика хребтов,
   отроги под фермы, запреты на потолки/плато (`docs/world/terrain/README.md:43-55`).
   `ProceduralChunkGenerator` строил бы **вторые, процедурные горы** (`GenerateMountainForPeak`,
   `ProceduralChunkGenerator.cs:131-190`) поверх/сбоку — визуальный и физический мусор
   (два коллайдера, z-fighting силуэтов, рассинхрон с поселениями, у которых зазоры 1343/688/326 м
   выверены под текущий террейн — `docs/world/terrain/README.md:29-31`).
2. **Сетка 2 км не согласована ни с чем.** Сцена 80 км / 2 км = 40×40 = **1600 чанков на сцену**,
   38 400 на весь грид 6×4. Радиус 2 → 5×5 = 25 чанков = 10×10 км покрытия — меньше, чем
   `unloadDistance = 10000f` и `preloadDistance = 10000f` сцен (`ClientSceneLoader.cs:64-70`).
   Два уровня стримят друг друга: чанк-лоадер дергает 25 генераций там, где сценарный лоадер держит
   целую 80-км сцену загруженной. Результат — churn без экономии.
3. **Генерация в рантайме вместо bake.** `GenerateMountainMesh` 64×24 + `MeshCollider` на каждый пик
   (`ProceduralChunkGenerator.cs:137-190`), `CumulonimbusCloud` 1–3 на чанк с радиусом размещения 8000
   (`ProceduralChunkGenerator.cs:196-241` — радиус больше самого чанка!), плейсхолдеры ферм
   `GameObject.CreatePrimitive` (`ProceduralChunkGenerator.cs:306-343`). Это ~500 мс на мир по оценке
   `Landscape_TechnicalDesign.md:915-943`, но в чанк-варианте — **каждый раз при пересечении границы**,
   с фризами и GC-спайками (новые `Material`, новые `Mesh`, `Instantiate` в `Update`-цикле стриминга
   каждые 0.5 с — `WorldStreamingManager.cs:278-289`).
4. **Fade через `material.color.a` ломает URP Lit.** `FadeInClouds` / `FadeOutCoroutine`
   (`ChunkLoader.cs:226-333`) мутируют `renderer.material.color` в цикле — для opaque URP/Lit альфа
   игнорируется (нужен transparent mode + keywords), плюс `renderer.material` клонирует материал
   на каждый рендерер → утечка материалов и слом батчинга/инстансинга (у руин как раз 2 материала
   на 496 инстансов — `docs/world/terrain/README.md:33-37`; fade-лоадер это бы разрушил).
5. **On-demand реестр без границ.** `GetChunksInRadius` (`WorldChunkManager.cs:145-175`) «создаёт чанк,
   даже если его нет в реестре» — т.е. мир бесконечен по построению; `CanLoadChunk`/`HasSceneFilter`
   (`WorldStreamingManager.cs:508-534`) — единственный тормоз, и он выключен по умолчанию (фильтр null).
   Итог: улёт за край сцены → бесконечная генерация пустых чанков.
6. **Сетевой дизайн не переживает встречу с NGO 2.13 и FO.** Старые `[ClientRpc]` без `SendTo`
   (`PlayerChunkTracker.cs:334,357`) противоречат пину «`[Rpc(SendTo.X)]`, не устаревшие»
   (`AGENTS.md`); спавн вне корней сцен и без маркеров/каталога упрётся в гейт B
   (`uncontrolled_network_source_before_native_sweep`, `09S_NEW_CONTENT_PIPELINE.md:30,76-116`);
   сдвиг мира не покрыт (нет participant tokens, нет `ApplyRebaseTranslation`, см. `05_*`).

Итого по §2: чинить legacy нельзя — дешевле и правильнее закрыть его как deprecated
(что и делает `01_CURRENT_STATE.md` §6) и развивать F3/F4 точечно.

## 3. Перф-математика: где реально лежат миллисекунды

Бюджет кадра 16.67 мс (60 FPS) из `docs/world/Landscape_TechnicalDesign.md:809-822`
(в документе опечатка в пути — имеется в виду `Landscape_TechnicalDesign.md` §5):
рендеринг 8 мс, culling 2 мс, анимация 1 мс, физика 1 мс, скрипты 2 мс, аудио 0.5 мс, резерв 1.5 мс.

Фактическая сцена `WorldScene_0_0` (из `docs/world/optimization/README.md:15-17`,
`docs/world/terrain/README.md`, `ViewDistanceConfig.cs`):

| Слой | Стоимость сейчас | Что даёт чанк-выгрузка | Что даёт удешевление (F3) |
|---|---|---|---|
| `Terrain_0_0` (1 объект, `drawInstanced`, pixelError 200) | ~1 draw call + basemap; вершинный шейдер террейна | 0 (выгружать запрещено — убьёт фон) | pixelError 200→300 + basemap 20K→12K на Near: −вершины, силуэт цел |
| Руины 496 инст. / 2 материала | ~5–7 draw calls, тени выкл | 0 (уже дёшево) | `DetailDistanceCuller`: скрыть дальше 4/8/12 км по пресету |
| Камеры far 60K (Medium) | overdraw + depth precision | 0 | far 60K→30K на Near + `fogScale` 1.3: −overdraw, −z-fighting |
| Тени `shadowDistance` 1500 (Medium) | главный рычаг FPS | 0 | 1500→500 на Near |
| Облака ~300 объектов | ~1 мс/кадр движение (оценка §5.4 техдизайна) | спорно (облака — геймплейный ориентир) | LOD облаков, не выгрузка |
| Горы-процедуры legacy (если воскресить) | **+десятки draw calls + MeshCollider bake на каждое пересечение** | отрицательная экономия | — (не делать) |

Вывод: **чанк-лоадер экономит там, где у нас не болит, и стоит там, где болит**
(фризы генерации, churn материалов, RPC-штормы). Порог, после которого F1 имеет смысл:
профайлер показывает >2–3 мс в culling/render на объектах, которые реально можно выгрузить
(тысячи уникальных пропсов, сотни мегабайт текстур стриминга) — сейчас этого нет
(память мира ~26.5 МБ по оценке техдизайна §5.3; сценарий «очень большие масштабы» —
это будущие сцены, а не плотность текущей).

Правило возврата к чанкам (см. `06_*`): только по цифрам профайлера + при 2+ сценах,
и только как **визуальные addressable-группы/LOD-группы**, а не как процедурная генерация.

## 4. Что покрывает T-LOD01 и чего не хватает до «городов вдали»

Покрыто (код есть, нужна калибровка пользователем — `docs/world/optimization/README.md:25-27`):

- `ViewDistanceConfig` + `ViewDistanceApplier.EnsureCullerHost/ApplyAll`
  (`ViewDistanceApplier.cs:67-96,138-149`): far, lodBias, shadowDistance, pixelError/basemap,
  `detailCullDistance`, `fogScale` → `DayNightController`.
- Мгновенное применение из ESC → Видео → Дальность, persist `Settings.ViewDistance`,
  Ultra disabled (`LOD_VIEW_DISTANCE_DESIGN.md:59-69`).
- FO-чистота: мировых `Vector3` нет (`LOD_VIEW_DISTANCE_DESIGN.md:75`).

Не хватает (это и есть «клиентский чанк-лоадер», если понимать термин правильно):

1. **HLOD/импосторы городов**: силуэт города на 12–80 км должен рисоваться 1–2 draw calls
   (билборд/лоу-поли коробка + ночные огни), а не полным набором зданий. Сейчас LOD-группы
   запроектированы только для гор (`Landscape_TechnicalDesign.md:369-407`), для городов — нет.
2. **Согласование `DistantFocus` (800–4000 м) с пресетами far/fog**: фокус живёт в L0/L1,
   при смене пресета — визуальная проверка (`LOD_VIEW_DISTANCE_DESIGN.md:46,79`).
3. **Калибровка чисел**: стартовые 30K/60K/120K, pixelError, basemap, fogScale — без замеров
   (`ViewDistanceConfig.cs:1-6` прямо требует калибровки в инспекторе, не в коде).
4. **Межсценовый фон (L3)**: хук `ViewDistance.Ultra` + `loadedScenes > 1` уже заложен
   (`LOD_VIEW_DISTANCE_DESIGN.md:77`), реализация — когда появится 2-я сцена.

Детали импосторов и колец — в `03_DISTANT_CITIES_TERRAIN.md`.

## 5. Дефекты legacy, которые нельзя тащить в новый дизайн (чек-лист отказа)

Для истории — чтобы ревьюер видел, что «починить прототип» дороже, чем спроектировать заново:

- [ ] `ChunkId`/`SceneID` дублируют друг друга (две сетки, два `GetChunkAtPosition` —
  `WorldChunkManager.cs:131-136` vs `PlayerChunkTracker.cs:220-231,319-325` с хардкодом `2000f`).
- [ ] `PlayerChunkTracker.UpdatePlayerPosition` (`PlayerChunkTracker.cs:384-405`) шлёт
  `Unload+Load` на **каждое** пересечение без радиуса/гистерезиса — дребезг на границе.
- [ ] `LoadChunkClientRpc(clientId, chunkId)` без `ClientRpcParams` — вещание всем
  (`PlayerChunkTracker.cs:334-352`): каждый клиент фильтрует чужое сам; при 10 клиентах × 25 чанков —
  250 RPC на волну переселения.
- [ ] `ChunkNetworkSpawner` спавнит сундуки `Instantiate + Spawn()` в рантайме чанка
  (`ChunkNetworkSpawner.cs:250-266`) при сломанном `NetworkPrefabsList` (см. `01_*` §4) —
  гарантированный отказ динамического спавна.
- [ ] `FloatingOriginMP` ищет корни `GameObject.Find` по именам (`FloatingOriginMP.cs:794-839`),
  исключает `TradeZones/Player` эвристиками, восстанавливает `TradeZones` позиционно
  (`FloatingOriginMP.cs:945-1012`) — несовместимо с транзакцией `06N` (запрет включён явно:
  «`FloatingOriginMP` не включается», `06N_REBASE_TRANSACTION_CONTRACT.md:76-78`).
- [ ] `WorldSceneManager` дублирует preload-логику `ClientSceneLoader` (два триггера границы:
  `preloadTriggerDistance = 10000f` vs `preloadDistance = 10000f`) — двойные загрузки соседей.

## 6. Ответ одним абзацем (для пересказа на созвоне)

Клиентский чанк-лоадер как «выгружать/генерировать куски 2×2 км» не нужен и вреден:
мир authored и уже дёшев (террейн-фон 1 draw call, руины инстансингом, туман+пресеты T-LOD01).
Нужны: (1) LOD/HLOD + импосторы дальних городов, (2) калибровка ViewDistance-пресетов,
(3) серверный interest без чанк-RPC, (4) межсценовый фон при 2+ сценах. Всё это — эволюция
существующих `Terrain_0_0` / `ViewDistanceApplier` / `ServerSceneManager` / FO-каталога,
а не возврат к `WorldStreamingManager`-прототипам.
