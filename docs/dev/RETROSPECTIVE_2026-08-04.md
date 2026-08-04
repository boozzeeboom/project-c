# Ретроспектива — неделя 28.07.2026 – 04.08.2026 (0.0.85)

**Период:** 28.07.2026 17:38 → 04.08.2026 23:02 (7 дней)
**Диапазон коммитов:** `238aa1f` ("docs and plans") → `fe12c365` (T-CLOUD42) — **196 коммитов**
**Версия:** v0.0.85
**Дата сборки:** 04.08.2026
**Каталог:** `docs/dev/` (по запросу пользователя)

---

## Резюмирующее саммари (TL;DR)

Неделя рекордной интенсивности: **196 коммитов** против 71 на прошлой неделе (+176%). Весь фокус — **два крупных направления** вместо 13 мелких:

1. **Cloud Ocean 3.0** (~75 коммитов, T-CLD01 / T-CLOUD02 / T-CLOUD03–42) — новая volumetric-система облаков доведена до продакшн-статуса 🟢: визуальное ядро (raymarch + Ghibli-рампы), интерактивность (корабельный след, displacement, кильватерный конус), конденсационные следы (VFX), штормовые ячейки с procedural-формой «цветная капуста» вместо гофротрубы. Зафиксирована в `docs/world/CLOUD_system/3.0/STATUS.md` как единственный источник правды.
2. **Unified Quest Graph v5** (~41 коммит, T-QEDIT v1–v5.22 + T-U01–U10 + T-DLG01) — единый нодовый редактор NPC + Dialog + Quest в одном окне GraphView для не-технарей. Плюс DialogTreeEditor v2 и кастомный QuestDefinitionEditor.
3. **Knowledge System v2/v3** (~18 коммитов) — знания/скиллы/рецепты как механика прогрессии: открытие через триггеры, потеря при смерти, гейты крафта, строковый recipeId, кастомные редакторы.

Побочно: NPC Activity Anchors (9), Edge Detection пост-процесс (2), mesh collider combiner, фиксы (краш билда, WindZone→ShipWindZone, авто-снап камеры, persistentShipId).

**Главные цифры:** 200 файлов, +37 078 / −863 строк; C# 11 376, Shader/VFX 8 066, Markdown 8 135; 37 новых `.cs`-файлов; всего **4 TODO/FIXME-маркера** — техдолг не растёт.

**Главный урок недели:** material property shadowing глобалов — параметры, объявленные в `Properties` шейдера, перекрывают `Shader.SetGlobal*` при `DrawProcedural`. Целый день диагностики (T-CLOUD35→37d) ушёл на то, что фикс занял 1 строку: убрать storm-секцию из `Properties`.

---

## Метрики

| Метрика | Значение |
|---------|----------|
| Всего коммитов | 196 |
| Дней работы | 7 (28.07 – 04.08) |
| Коммитов/день (среднее) | 28 |
| Файлов изменено | 200 |
| Строк добавлено / удалено | +37 078 / −863 |
| Новых `.cs`-файлов | 37 |
| Изменённых `.cs`-файлов | 48 |
| C# строк | 11 376 |
| Shader/VFX строк | 8 066 |
| Markdown строк | 8 135 |
| TODO/FIXME/HACK маркеров | 4 (в изменённых файлах) |
| Самая итеративная система | Cloud Ocean 3.0 (~75) и Quest Graph v5 (~41) |

### Коммиты по дням

| Дата | Коммитов | Основная работа |
|------|----------|-----------------|
| 28.07 | 2 | T-FIX01: краш билда, PerfHUD |
| 29.07 | 18 | T-NPC-S23 (Activity Anchors), T-VFX01 (edge detection), T-CAM15, mesh combiner |
| 30.07 | 23 | T-DLG01 (DialogTreeEditor v2), T-QUEDIT, T-QREWARD, T-NPC24 |
| 31.07 | 41 | T-QEDIT v5 (Unified Quest Graph, ~30 итераций) |
| 01.08 | 19 | T-KNOWLEDGE-V2 (Phase A), T-SOC-01, T-SKILL-EDITOR |
| 02.08 | 16 | T-CLD01 (Phase 1 — визуальное ядро), T-CLOUD03 |
| 03.08 | 19 | T-CLOUD02 (Phase 2.1–2.4), T-KNOW-V3, T-KEY-FIX |
| 04.08 | 59 | T-CLOUD08–42 (depth, contrail, storm cells, финальные доки) |

---

## Детальный отчёт по направлениям

## 1. Cloud Ocean 3.0 — объёмная система облаков (~75 коммитов)

**Зачем:** центральный визуальный элемент сеттинга (Stage 2.5) — облачное море. Старая система (billboard-сферы, Veil-рендереры) заменяется единым volumetric raymarch-рендерером. Это самая крупная инвестиция недели — и она закрыта до «продакшн-готово».

### Фаза 1 — Визуальное ядро (T-CLD01, 02.08, ~5 коммитов)
| Коммит | Суть |
|--------|------|
| `a4e18df` | Детальный план Phase 1 (1.1–1.7) |
| `6add42ac` | Реализация 1.1–1.7: `CloudNoise.hlsl` (HLSL-порт `src/CloudMath.cs`), `CloudCommon.hlsl`, `VolumetricClouds.shader`, `BakeCloudNoise.compute` (128³), `VolumetricCloudsRenderFeature.cs` (URP RenderGraph), `CloudNoiseBaker.cs`, `CloudPerfMonitor.cs` |
| `18ba8c6e` | Fix: Remap→CloudRemap (конфликт имён с URP), Color32 readback, убраны undeclared keywords |
| `f2c96001` | Fix: синхронный `Graphics.CopyTexture` по Z-слайсам — `AsyncGPUReadback` читал 1 слайс вместо всего объёма |
| `0e92a87f` | Phase 1 complete: 1.5 colored light-march (HG g=0.7 + multi-scatter + Ghibli), 1.6 half-res + blue-noise + temporal + генератор blue-noise |

### Фаза 2.1–2.2 — Интерактивность (T-CLOUD02, 02–03.08)
| Коммит | Суть |
|--------|------|
| `04624af9` | Phase 2.1: `LocalDensityBuffer` — 96³ тор-окно, ping-pong compute |
| `7ae4d4a7` | Phase 2.2: SplatDensity API + `ShipWakeCloudCutter` demo |
| `f26db724`…`a39aeedb` | Цикл диагностики: отрицательные сплаты → положительные + вычитание в шейдере; Input System вместо UnityEngine.Input; singleton вместо serialized ref (нельзя scene-объекты в asset-инспектор); стартовые логи Create/AddRenderPasses |
| `ffc90819` | Все хардкоды убраны — полный inspector-тюнинг через `Shader.SetGlobal*` |
| `d2ec02f4` | **Multi-Layer Cloud System** — 4 слоя (800–1200, 1200–2500, 2500–4500, 4500–7000), per-layer coverage/density/ramps |
| `4e6fb51e` | **Variant B — displacement** (радиальный push шума) вместо вычитания плотности; A/B-переключение через `LocalDensityBuffer.Mode` |
| `00315935` | Доклад: pipeline OK, видимого разреза нет |

**Кильватерный конус** (design note `docs/dev/CLOUD_OCEAN_PHASE2_WAKE_CONE.md`): одиночный сплат в позицию корабля давал разрыв **над** кораблём (торроидальный буфер следует за кораблём). Заменён на серию гауссовых сплатов **позади** — классический кильватер: облака расходятся конусом за кормой (`ConeSegments`, `ConeSpacing`, `ConeRadiusGrowth`). Очередь сплатов 16→64.

### Фаза 2.3 — Конденсационные следы (T-CLOUD11–16, 04.08)
| Коммит | Суть |
|--------|------|
| `ad1f2364` | `Contrail.vfx` (VFX Graph из Simple_Trail) + `ShipContrailVfx` (GetComponentInParent) |
| `e03e2422` | Мульти-точки спавна + гайд VFX Graph |
| `c331f17c` | Фикс обнаружения геометрии (`GetShipVisualSize`) |
| `8996c137` | StopDelay + документация (анти-отрыв трейла) |
| `87622cf1` | Size over Life fix + random size (плоские квадраты fix) |
| `d5100b01` | Чистка [VolClouds] логов, закрытие Phase 2.3 |

### Depth-фиксы (T-CLOUD08–10, 04.08) — три итерации подряд
| Коммит | Суть |
|--------|------|
| `32480d50` | CopyDepthMode AfterTransparents → **AfterOpaques** (облака были только за объектами) |
| `c1c75851` | Reverse-Z: `sceneDepth < 0.999` → `Linear01Depth(...)` (после фикса №1 облака исчезли) |
| `e887c4ee` | `ZTest LEqual` → **Always** — RenderGraph pass не имеет depth attachment |

### Фаза 2.4 — Штормовые ячейки (T-CLOUD17–42, 04.08, ~30 коммитов)
| Коммит | Суть |
|--------|------|
| `b67c3ce6` | Phase 2.4: `StormCellDirector` + переписан `StormLightningVfx` |
| `a85a61a5` | `LightningBolt.vfx` + VFX wiring |
| `4903c894`/`c18c7ca7` | LightningBolt.vfx — полный VFX Graph (YAML, 5 контекстов); пересоздан из Simple_Burst (YAML crash fix) |
| `2f332e93`…`8ae71f0f` | Цикл дебаг-визуализации: Gizmos → DrawLine → сферы/кресты/лучи → вертикальные столбы 800→5000м → маркеры-столбы → всё в инспектор |
| `46eca84e` | Отложенный спавн (2 сек) + маркеры URP |
| `ea7b07f3` | Camera.main → `FindGameObjectWithTag(Player)` |
| `0159e08e` | Документация debug positioning + ITERATIONS.md |
| `bcd64790` | **T-CLOUD35: analytic storm density** в VolumetricClouds — тёмные грозовые кластеры внутри ячеек |
| `186f91cc` | T-CLOUD36: organic storm shape — 3D-noise деформация границ |
| `18d422f0` | T-CLOUD37: cellular FBM + domain warp — «цветная капуста» вместо гофротрубы |
| `3e7dbbd2`/`d3aab97f` | Fix noise scale + cluster contrast; PushStormCellsToShader каждый кадр |
| `e9c67277` | T-CLOUD37d: cellular IS the shape — кластер пузырей, не текстура внутри цилиндра |
| `88b222de` | T-CLOUD37e: честная документация — что работает, что нет |
| `fbc91eea` | **T-CLOUD39:** procedural storm form + runtime save/load + anti-banding |
| `c1284e85` | T-CLOUD40: anti-banding tuning params + vertical noise/warp |
| `4cc74700` | **T-CLOUD41:** штормовые облака иммунны к ship displacement |
| `fe12c365` | **T-CLOUD42:** `STATUS.md` — единственный источник правды |

**Дизайн-нота** `docs/dev/STORM_CELLS_FORM_RUNTIME_DESIGN_NOTE.md` фиксирует две корневые причины:
1. **Рантайм-твикинг не работал** — material property shadowing: все `_Storm*` параметры были в `Properties` шейдера → материал перекрывал `Shader.SetGlobal*` (доказательство: `_NoiseTileSize` и др. работали, их в Properties нет). Фикс: убрать storm-секцию из Properties.
2. **Форма «гофротруба»** — 5 факторов: масштаб cellular-шума на 1–2 порядка меньше радиуса; InvertedWorley ~0.65 > порога 0.5 → сплошной цилиндр; envelope доминирует; warp привязан к радиусу; нет per-cell seed. Фикс: cellSize авто-масштаб `radius*2.8`, порог `0.5+contrast*0.3`, envelope только safety-клип, per-cell seed offset, fine-октава (cauliflower), асимметричный вертикальный профиль через `_StormVerticalPeak`.

**Статус на конец недели:** `docs/world/CLOUD_system/3.0/STATUS.md` — 🟢 продакшн-готово, фазы 1.1–2.4 закрыты. Оставшееся → v3.5: VFX молний в ячейках, Мезий-харвест, серверные погодные ячейки, сетевые shared-возмущения, выпиливание Veil-рендереров, перф-аудит полного кадра. Contrail требует ручной доводки VFX.

---

## 2. Unified Quest Graph v5 — единый нодовый редактор квестов (~41 коммит, 30–31.07)

**Зачем** (план `docs/NPC_quests/UNIFIED_QUEST_GRAPH_PLAN.md`): дизайнер прыгает между 4 редакторами (DialogTreeEditor, QuestDefinitionEditor, QuestNodeGraphWindow, NpcDefinitionEditor) и не видит целого. Нужен один граф: «этот диалог ведёт к этому квесту», «этот NPC даёт эти квесты». Выбран **подход A — тонкий слой** над существующими SO (не ломает обратную совместимость, нет дублирования данных, низкий риск).

| Этап | Коммиты | Суть |
|------|---------|------|
| T-U01–U10 | `e821c7e3`…`610ef892` | Model-driven OnGraphViewChanged, Node↔SO binding, BFS auto-layout, цветные порты, DialogNodeView, связи Dialog↔Quest, интеграция с QuestDefinitionEditor и DialogTreeEditor |
| v3 | `13055de5` | DialogNodeView с IMGUI — переиспользование PropertyDrawer'ов для drag-and-drop |
| v4 | `ea92f1ba` | Multi-NPC — NpcCardNode + авто-рёбра из SO |
| v5.1–5.4 | `a13ed07f`…`e68b4bbc` | Контекстное меню, создание ассетов, цепочки стейджей, Undo.RecordObject + SaveAssets, CRUD через SerializedObject |
| v5.5–5.8 | `5150d46d`…`ac2b848d` | DeleteArrayElementAtIndex/InsertArrayElementAtIndex, delayCall, прямой CRUD без SerializedObject, перехват Delete key |
| v5.9–5.15 | `6c93b448`…`03f56901` | Порты диалога live при +/- choice, пин-кнопки, полный Rebuild на OnModified, авто-загрузка цепочки, BFS Tree → колоночный лейаут (откат), инкрементальный CRUD |
| v5.16–5.22 | `e5ce82b6`…`974a7a67` | Resizable ноды, полноширинные TextArea, TextEditPopup, фикс обрыва связей, StageNode OnModified live rebuild |

**UI Toolkit-итерации** (показательны для техдолга UITK): TextArea height feedback loop (resolvedStyle.height → NaN-guard → откат на PropertyField), ExpandHeight, полноширинные TextArea — целых 6 итераций только на вёрстку нод.

**Итог:** единый граф NPC + Dialog + Quest в одном окне, все мутации пишутся прямо в существующие SO, с Undo/Redo.

---

## 3. DialogTreeEditor v2 (T-DLG01, 12 коммитов, 30.07)

**Зачем:** редактор диалогов для не-технарей: карточки нод, drag-and-drop условий и speaker'а, редактируемые рёбра.

| Коммит | Суть |
|--------|------|
| `93d7fd11` | Карточки нод, drag-and-drop условий и speaker'а |
| `daa452da`…`a5772080` | Фиксы вёрстки: легенда, тултипы, Identity-блок в полную ширину, nodeId редактируемый, авто-уникальные имена |
| `60575fd6` | Переписан DrawEdgesSection — каждое ребро с полным набором редактируемых полей |
| `a2d8870e`/`fac4b57e` | Фикс GetPropertyHeight — всегда max высота, фолбэк всегда видим (dimmed) |
| `883ebc49` | **КРИТИЧЕСКИЙ ФИКС:** `enumValueIndex` → `intValue` во всех Drawer'ах |
| `fda01ece` | +itemRef (ItemData) в DialogueAction, возврат на PropertyDrawer'ы |

---

## 4. NPC Activity Anchors (T-NPC-S23, 9 коммитов, 29.07)

**Зачем:** NPC-жители должны осмысленно заниматься активностями (Work/Sit/Sleep/Socialize), а не стоять столбом.

| Коммит | Суть |
|--------|------|
| `fb95076a` | Transform-якоря для idle-активностей + patrolWaypointMarkers в NpcSocialBrain |
| `3f00aeeb` | Activity Anchors v2 — массивы Transform[] для Work/Sit/Sleep/Socialize с циклическим обходом |
| `60315cb7` | Fix: вложенные FoldoutHeaderGroup → EditorGUILayout.Foldout |
| `a5419a9f` | Random выбор следующей точки — пришёл → активность n+random сек → random точка (≠ текущей) |
| `fe48be5f` | Fix: таймеры стартовали с 0 — NPC мгновенно менял точку |
| `d81eff2f`/`34e8a118` | Fix: Vector3.Distance → `_agent.remainingDistance` (stoppingDistance 2.25 vs threshold 1.5); порог max(patrolArrivalThreshold, stoppingDistance+0.5) |
| `7835b753` | **Полный рефактор:** стейт-машина AnchorState (NeedMove→Moving→Active) |

---

## 5. Knowledge System v2/v3 (~18 коммитов, 01–02.08)

**Зачем:** знания/скиллы/рецепты — механика прогрессии: навыки открываются через триггеры, при смерти частично теряются, гейтят крафт.

| Тикет | Суть |
|-------|------|
| T-KNOWLEDGE-V2 (8) | Phase A: skills/recipes knowledge, death loss (`PlayerTarget.TriggerDeathRespawn`), NetworkPlayer RPC + CraftingServer broadcast + NMC auto-spawn + KnowledgeLossConfig.asset; update ITERATIONS.md после merge-conflict recovery |
| T-KNOW-V3 (5) | **V3.0:** стабильный строковый recipeId (миграция RecipeData, registry, DTO, клиент, сеть, CraftingWindow); **V3.1–3.11:** KnowledgeManager фасад, KnowledgeRevealTrigger (server-authoritative), knowledge-фильтры SkillTreeWindow, KnowledgeToast, FactionCatalog в NMC, кастомный редактор SkillNodeConfig (Hidden/AlwaysVisible) |
| T-QUEDIT (4) | Кастомный QuestDefinitionEditor для не-технарей: drag-and-drop строковых ID (NPC, квесты, сцены, диалоги) |
| T-QREWARD (5) | Анализ + drag-and-drop наград: pickupItem→ItemData, cargoItem→TradeItemDefinition; QuestObjective.pickupItem |
| T-SKILL-EDITOR (3) | Кастомный Editor для SkillNodeConfig (Social/Combat группы), fix nested foldout headers |
| T-SOC-01 (2) | SocialSkillTreeWindow — реюз UXML/USS боевого окна |

---

## 6. Фиксы и побочные направления

| Тикет | Коммиты | Суть |
|-------|---------|------|
| T-FIX01 | `f1b8dc9` (28.07), `01060274`, `34aaa243`, `188efeb5` | **Краш билда:** `InvestigateAnimator.cs` в папке `_Editor` (не специальная Editor-папка) попадал в билд → обёрнут в `#if UNITY_EDITOR`; PerfHUD отключён через `#if FALSE` + документирован; **WindZone → ShipWindZone** (конфликт со встроенным компонентом Unity) |
| T-CAM15 | `bcf2d9b`, `c79479bb` | Авто-снап SpringArmCamera при телепортации/загрузке сохранения (>10m детект + Snap()) |
| T-KEY-FIX | `9791fe9d`, `8bc3c60f`, `f26db724` | persistentShipId для KeyRodInstance — потеря доступа к кораблю между сессиями; архитектурный пост-мортем `docs/dev/ship-key post-mortem` |
| T-NPC24 | `acce9b11`, `4744c3d8` | NpcDefinition — drag-and-drop квестов, кастомный редактор с блоками |
| T-VFX01 | `18b3a85f`, `b5b1d036` | **Borderlands-style edge detection** пост-процесс (distance falloff, adaptive color, pencil stroke) + перенос доки в `docs/world/` |
| tools | `1959c505` | Mesh collider combiner — revive `CombineMeshesToCollider.cs` |
| T-DOCS01 | `15885b31` | Перенос EdgeDetection документации |

---

## 7. Документация (прошёлся по связанным)

| Файл | Роль |
|------|------|
| `docs/world/CLOUD_system/3.0/STATUS.md` | **Единственный источник правды** по Cloud 3.0 (архитектура, статусы фаз, файлы, коммиты) |
| `docs/world/CLOUD_system/3.0/ITERATIONS.md` | Итерации облаков (depth-фиксы, contrail, phase 2.3) |
| `docs/world/CLOUD_system/3.0/STORM_FORM_RUNTIME_INVESTIGATION.md` | Полный разбор формы штормовых ячеек |
| `docs/world/CLOUD_system/3.0/DEBUG_POSITIONING_INVESTIGATION.md` | Дебаг-позиционирование ячеек |
| `docs/dev/STORM_CELLS_FORM_RUNTIME_DESIGN_NOTE.md` | Дизайн-нота: 2 корневые причины + фиксы |
| `docs/dev/CLOUD_OCEAN_PHASE2_WAKE_CONE.md` | Кильватерный конус (design note) |
| `docs/dev/ITERATIONS.md` | Итерации структуризации docs (T-DOCS01), T-CAM15 |
| `docs/dev/RETROSPECTIVE_0.0.70.md` | Предыдущая ретроспектива (22–28.07) |
| `docs/NPC_quests/UNIFIED_QUEST_GRAPH_PLAN.md` | План Quest Graph v5 (подход A) |
| `docs/NPC_quests/T-U01…T-U10`, `T-U03_BFS_LAYOUT_PLAN`, `T-U08_CONDITION_NODE_ANALYSIS.md` | Детальные ноты итераций U-тикетов |
| `docs/NPC_quests/T-NPC-S23_activity_anchors.md` | Activity Anchors |
| `docs/NPC_quests/ANALYSIS_QuestRewardItem_refactor.md` | Анализ QREWARD |
| `docs/Character/Knowledges/07_KNOWLEDGE_SYSTEM_V3_INTEGRATION_PLAN.md`, `08_KNOWLEDGE_SYSTEM_V3_INTEGRATION_LOG.md` | План и лог Knowledge V3 |

---

## Тренд vs прошлая неделя (0.0.70)

| Метрика | 0.0.70 (22–28.07) | 0.0.85 (28.07–04.08) | Δ |
|---------|-------------------|----------------------|---|
| Коммитов | 71 | 196 | +176% |
| Направлений | 13 мелких | 2 крупных + поддержка | — |
| Коммитов/день | ~10 | 28 | +180% |
| TODO/FIXME | — | 4 | минимален |
| Статус | ретроспектива | 🟢 Cloud 3.0 продакшн-готово | — |

**Тренд: рост.** Скорость выросла почти в 3 раза. Причина: смена модели работы — вместо 13 параллельных мелких задач (камера, jitter, перф, доки) неделя концентрируется на двух сквозных фичах (Cloud 3.0, Quest Graph), которые итеративно доводятся до конца. Коммиты стали мельче и чаще (по одной итерации/фиксу), что делает историю читаемой, а откаты — точечными.

## Технический долг

- TODO/FIXME/HACK: **4 маркера** в изменённых C#-файлах — техдолг не растёт (прошлая неделя: чистка ~35 warnings + лог-спама).
- Открытые пункты Cloud 3.0 (v3.5): VFX молний, Мезий-харвест, серверные погодные ячейки, shared-возмущения, выпиливание Veil, перф-аудит кадра. Contrail требует ручной доводки VFX.
- Известная проблема NGO: `DefaultNetworkPrefabs.asset` не присвоен — динамический спавн сломан (из AGENTS.md, не трогали).
- Микротряска персонажа — открыта с 0.0.70 (11 итераций не хватило; отдельное расследование).

## Уроки недели

1. **Material property shadowing глобалов** — параметры в `Properties` шейдера перекрывают `Shader.SetGlobal*` при DrawProcedural. Правило: если параметр твикается глобалом в рантайме — не объявлять его в Properties (T-CLOUD35→37d).
2. **RenderGraph не имеет depth attachment** — ZTest Always + CopyDepthMode AfterOpaques + reverse-Z → `Linear01Depth` (T-CLOUD08–10).
3. **Кильватер ≠ вырез на месте корабля** — торроидальный буфер следует за целью; сплаты надо ставить позади вектора движения (wake cone).
4. **AsyncGPUReadback читает 1 слайс** — для объёмных текстур нужен синхронный CopyTexture по Z-слайсам (T-CLD01 fix).
5. **Вложенные FoldoutHeaderGroup ломаются** — заменять на EditorGUILayout.Foldout (NPC-S23, SKILL-EDITOR).
6. **enumValueIndex vs intValue** в PropertyDrawer'ах — критический класс багов сериализации (T-DLG01).
7. **Vector3.Distance игнорирует stoppingDistance** — для NavMeshAgent использовать `remainingDistance` с порогом max(threshold, stoppingDistance+0.5).
8. **Код в не-Editor папке попадает в билд** — `_Editor` не является специальной папкой; `#if UNITY_EDITOR` обязателен.
9. **UI Toolkit: resolvedStyle.height feedback loop** → NaN; TextArea↔PropertyField — 6 итераций на вёрстку нод. Знакомая боль, паттерн уже в skills.
10. **Документация:** STATUS.md как единый источник правды для большой системы — работает; design note перед правками кода — окупилась (STORM_CELLS_FORM_RUNTIME_DESIGN_NOTE).

## Action Items на следующую неделю

| # | Действие | Приоритет |
|---|----------|-----------|
| 1 | Cloud 3.0 v3.5: VFX молний в грозовых ячейках + перф-аудит полного кадра (Phase 3.6) | High |
| 2 | Contrail VFX — ручная доводка (форма/жизнь частиц), закрыть Phase 2.3 🟡 | Med |
| 3 | Quest Graph v5 — playtest редактора на реальных квестах, фикс найденных багов | Med |
| 4 | Knowledge V3 — интеграционное тестирование в Play Mode (триггеры → потеря при смерти → крафт) | Med |
| 5 | Присвоить DefaultNetworkPrefabs.asset (NGO) — разблокирует динамический спавн | Low |
| 6 | Отдельное расследование микротряски персонажа (структурный подход, не итерации) | Low |

---

## История

| Дата | Сессия | Изменения |
|------|--------|-----------|
| 04.08.2026 | Ретроспектива недели 28.07–04.08 | Создан файл: 196 коммитов, Cloud Ocean 3.0, Unified Quest Graph v5, Knowledge v2/v3, метрики, уроки, action items |
