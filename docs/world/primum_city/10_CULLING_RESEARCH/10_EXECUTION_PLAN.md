# 10. План исполнения culling/visibility (Project C)

**Дата:** 2026-10-31 · **Основание:** `10_CULLING_RESEARCH` (A–J), коммит `6b49ac51`
**Статус:** Stage 0–3 закрыты; Stage 4/4b дали **benchtop 2 423 DC**, final validation это не подтвердила
(2 811) — **Stage 4c fix закрыт: реальный компонент даёт P1 2 394 DC ≤2 500, повтор 0.0 %, F8/F9 PASS**.
**Stage 6 и Stage 7 — утверждены пользователем как фундамент, НЕ опциональны**; следующий шаг — Stage 6,
затем Stage 7. Другие корабли сейчас не делаем.
**Тикет сложного кода:** `T-CULL01` —
[`docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md`](../../../dev/T-CULL01_COMPLEX_CODE_HANDOFF.md).
**Правило:** сложные куски кода не делает Lead; они выносятся в `T-CULL01` и передаются отдельному
агенту по одному этапу, когда до этапа дошли. Простые правки — отдельными маленькими коммитами.

---

## 0.1. TL;DR для заказчика

1. План — **8 этапов** (Stage 0–7). Первые четыре дают основной эффект за ~4–6 недель.
2. **Stage 0** — baseline + два фикса; **Stage 1** — корабли P0 (0.5–1 день); **Stage 2** — двери
   (2–4 дня) + бюджет теней; **Stage 2b** — фермы (1 день); **Stage 3** — **только `Ship_havyII_base`**
   (пилот; остальные корабли — future scale-out, не трогаем).
3. **Stage 4** — GRD + GPU occlusion; **Stage 4b** — городской shadow budget 200 м; **бенч 2 423 DC**,
   final validation 2 811 → **Stage 4c fix: реальный компонент 2 394 DC ≤2 500 (причина — `AtmosphereCamera`
   в union камер), F8/F9 PASS**; **Stage 6/7 — утверждены пользователем как фундамент (не опциональны)**:
   VisibilityDirector → HLOD/L1/L2. Stage 5/B2 закрыт (`B2_increases`).
4. Сложный код — **7 пакетов H1–H7** в [`T-CULL01`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md);
   простые правки (сцена/слой/настройки/двери) — отдельными коммитами по этому плану.
5. Каждый этап заканчивается замером по gate и **маленьким коммитом**; без gate следующий этап не
   начинается. Play Mode/скриншоты — только пользователь.

### 0.2. Трекер этапов

| Stage | Статус | Зависит от | Gate (кратко) | Owner | Артефакт |
|---|---|---|---|---|---|
| 0 | 🟡 done with caveats (R0.1–R0.3) | — | baseline CSV ±5 %, фиксы, F8/F9 | Lead/пользователь | `08_DATA/K_baseline_2026-10-31.csv`, `K_stage0_report.md` |
| 1 | 🟡 partial pass (240/250, 9 doors inside; reimport OK) | Stage 0 | 250 боксов/9 дверей после reimport; 0 missing; scene-diff пуст | агент T-CULL01/H1 | prefab + `H1_prefab_repair.csv` |
| 2 | ✅ conditional pass (P1 6 094, −77.5 %) | Stage 0 | P1 27 131 → 6 094; повтор +0.25 %; визуал/F8/F9 открыты | агент T-CULL01/H3 | слой + `DoorVisibilityBudget`/`ShadowBudget` |
| 2b | ✅ done (doors inherited; shadow ≈0) | Stage 2 | 270 дверных рендереров ферм уже на слое 10; ShadowBudget на 5.7–6 км — ≈0 | автоматически | — |
| 3 | ✅ P1/P2 green (conditional: visual/F8 open) | Stage 1 | P1 973→616 MR; P2 d200 1 170 / d300 1 236 ≤1 300; другие 0 diff | агент T-CULL01/H2b | `N_stage3c_*`, `T-CULL01_STAGE3C_REPORT.md` |
| 3b | ⛔ deferred — **не сейчас** | — | другие корабли типовые; если мешают — выключить рендереры в замере | — | — |
| 4 | ✅ conditional green — visual OK; gate open | Stage 2 | P1 3 031 (−52.6 %); Hybrid Batch Group ✅; 0 shader errors ✅; **DC ≤2 500 ❌**; окклюзия ≈0 | агент T-CULL01/H5 | настройки + шейдеры + `T-CULL01_STAGE4_REPORT.md` |
| 4b | ✅ green — visual OK | Stage 4 | бенч P1 **2 423** (sb200); но фактический компонент даёт 2 811 | агент T-CULL01/H5b | `P_stage4b_*`, `CityShadowBudget.cs` |
| V | ❌ failed gate — 2 811 > 2 500 | Stage 4b | кандидатов 1 301 вместо 1 482; F8 ok, F9 критерий неверен | агент T-CULL01/V | `Q_final_validation_*`, `T-CULL01_FINAL_VALIDATION_REPORT.md` |
| 4c | ✅ green — gate taken | Stage V | реальный `CityShadowBudget` **2 394 DC** ≤2 500 (запас 106); повтор 2 394 (0.0 %); с кораблями 2 406; F8/F9 PASS; 0 shader errors; clean | агент T-CULL01/H5c | `R_stage4c_*`, `T_stage4c_probe_*`, `T-CULL01_STAGE4C_REPORT.md` |
| 5 | ⛔ closed by dry-run — **B2 не делать** | — | `B2_increases`: 330 батчей против 160 | — | — |
| 6 | 🟡 approved — required | Stage 4c | VisibilityDirector: culling ≤2 мс; −60…−80 % DC; F8/F9 | агент T-CULL01/H6 | `VisibilityDirector` |
| 7 | 🟡 approved — required | Stage 6 | HLOD/L1 + L2-импостор/PVS: L1 ≤500, L2 ≤10 | агент T-CULL01/H7 | HLOD/impostor |
| 6 | ⬜ optional | Stage 4/5 | culling ≤2 мс; −60…−80 % DC без окклюзии | агент T-CULL01/H6 | VisibilityDirector |
| 7 | ⬜ optional | Stage 5/6 | L1 ≤500, L2 ≤10; FO | агент T-CULL01/H7 | HLOD/impostor/PVS |

Статусы обновлять здесь после каждого gate; коммит этапа — с префиксом `T-CULL01/<H>`.

**Stage 0 (2026-10-31):** baseline — `08_DATA/K_baseline_2026-10-31.csv`, разбор — `08_DATA/K_stage0_report.md`.
Baseline зафиксирован как **Midday + тени URP 1000/2**: в фазе `Night (Noch)`/`Twilight (Sumerki)` главный свет
выключен и теневого прохода нет, тот же кадр даёт 17 020 DC вместо 27 131. Оба бага (`Camera.main` в куллере,
no-op `QualitySettings.shadowDistance`) исправлены. F8/F9 прошли без фактического сдвига мира (фокус уже в кванте),
поэтому реальный ребейз за порогом `256 м` остаётся открытым — см. `K_stage0_report.md` §6.

---

## 0. Что считаем результатом

Цель — стабильные **60 fps** в L0-городе и порту, без переделки геймплея/сети/FO:

| Метрика | Сейчас | Цель после этапов |
|---|---|---|
| Draw calls, конфиг A (shadow 1000/2) | 26 981 | **≤ 2 500–3 000** |
| Draw calls, конфиг B (shadow 250/1) | 21 118 | ≤ 2 000–2 500 |
| Двери в кадре | до ~17 766 DC | сотни DC |
| Корабли (все 23) | ~4 917 DC (модель) | ≤480 — **future scale-out после пилота HeavyII** |
| Город (не двери) | 2 292 / 1 647 DC (модель J) | не хуже +10 % |
| Culling CPU | не измерено | **≤ 2 мс** |
| FO F8/F9 | работает | 0 errors, без сдвига/pop |
| Визуал | — | без заметных pop-in; тени/двери/эмиссия по согласованию |

Бюджет «L0 ≤ 120 DC» **отменён**: это бюджет L1/L2. L1 — ≤500, L2 — ≤10.

---

## 1. Порядок этапов (сверху вниз, не перескакивать)

```
Stage 0  Baseline + фиксы (2–3 дня)                     — простой код/ручные проверки
Stage 1  P0 корабли: вынос коллайдеров/дверей (0.5–1 день) — СЛОЖНЫЙ КОД → T-CULL01/H1
Stage 2  Ф0 двери + бюджет теней (3–5 дней)              — средний код → T-CULL01/H3
Stage 2b Фермы: двери на слой, тени off (1 день)          — простые настройки
Stage 3  Корабли P1/P2: greeble-мерж + LOD (3–5 дней)    — СЛОЖНЫЙ КОД → T-CULL01/H2
Stage 4  GRD + GPU occlusion (2–3 недели)                — СЛОЖНЫЙ КОД/ШЕЙДЕРЫ → T-CULL01/H5
Stage 5  Воспроизводимость мержа + B2 города (2–4 нед)   — СЛОЖНЫЙ КОД → T-CULL01/H4
Stage 6  VisibilityDirector (3–4 недели, ✅ утверждён) — СЛОЖНЫЙ КОД → T-CULL01/H6
Stage 7  HLOD/L1 + L2-импостор / PVS (✅ утверждён)        — СЛОЖНЫЙ КОД → T-CULL01/H7
```

Stage 0–3 можно частично параллелить: Stage 1 не зависит от Stage 2; Stage 3 — после Stage 1.
Stage 2b — после Stage 2 (тот же слой дверей). Stage 4 — после Stage 2 (нужны измерения и шиммер-настройки).
Stage 5 — закрыт (B2 increases). **Stage 6 — после Stage 4c (утверждён), Stage 7 — после Stage 6 (утверждён).**

---

## 2. Stage 0 — Baseline + фиксы (2–3 дня)

**Brief для отдельного агента (Play Mode разрешён пользователем):**
[`T-CULL01_STAGE0_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE0_AGENT_BRIEF.md).

**Цель:** иметь сравнимые «до/после» и убрать два известных бага.

### 0.1. Методика и точки
- Камера: геймплейный риг, **far 30 км** (не 1000), 3 точки: центр площади / узкая улица / крыша;
  плюс 1 точка у порта (корабли).
- `UnityStats` (DC/SetPass/tris), Frame Debugger, Profiler; равный `frameCount`; FPS после ~1500 кадров.
- `AtmosphereCamera` (меню, far 10) — вне замера; `Camera.main` не использовать.
- FO-регресс: F8 → F9, 0 errors, без сдвига.

**Артефакт:** `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/K_baseline_<дата>.csv`
(колонки: сцена, точка, far, shadow, DC, SetPass, tris, FPS, frameCount).

### 0.2. Фиксы (простые, отдельный маленький коммит)
1. **`ViewDistanceApplier` shadowDistance — no-op.** URP читает `UniversalRenderPipelineAsset.shadowDistance`,
   а не `QualitySettings`. Файл:
   [`C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\ViewDistanceApplier.cs`](file:///C:/Projects/ProjectC_client/Assets/_Project/Scripts/World/ViewDistanceApplier.cs) — строки **75, 82**.
   Решение: перенести shadow-пресет в URP-ассет (правка `ProjectC_URP.asset` — согласовать) **или**
   убрать из `ViewDistanceConfig` и управлять через `shadowCastingMode`-бюджет.
2. **`Camera.main` в `DetailDistanceCuller`.** Файл:
   [`C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DetailDistanceCuller.cs`](file:///C:/Projects/ProjectC_client/Assets/_Project/Scripts/World/DetailDistanceCuller.cs) — строка **87**.
   В проекте **две** камеры с тегом `MainCamera`; `Camera.main` может вернуть статичную в 240 км.
   Эталон выбора камеры — `PrimumLightPool`:
   [`C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\PrimumLightPool.cs`](file:///C:/Projects/ProjectC_client/Assets/_Project/Scripts/World/PrimumLightPool.cs) — строки **33, 159–219**.
3. **`AtmosphereCamera` (far 10)** не включать в union/камеры куллинга — это меню-камера.
4. Проверить `GraphicsSettings`: `m_CameraRelativeLightCulling/ShadowCulling` уже 1/1
   ([`C:\Projects\ProjectC_client\ProjectSettings\GraphicsSettings.asset`](file:///C:/Projects/ProjectC_client/ProjectSettings/GraphicsSettings.asset) — строки **58, 79–80**).

**Gate Stage 0:** baseline CSV заполнен; повторный замер ±5 %; оба бага воспроизведены/исправлены;
F8/F9 зелёный.

### 2.4. Stage 0 review (Lead, 2026-10-31)

**Принято:**
- P1 baseline **27 131 DC** против ориентира 26 981 (**+0.56 %**), повтор P1 — **0.00 %**;
  P1 без городских дверей **4 607** против 4 954 (**−7.0 %**, в допуске ±10 %).
- Зафиксировано условие: **Midday forced + URP 1000/2**; ночью/в сумерках главный свет выключен,
  теневого прохода нет: тот же кадр **17 020 DC** (−37 %). Будущие замеры — только при Midday
  (или явно подписывать фазу).
- `DetailDistanceCuller` больше не использует `Camera.main`; проверено: в центре хэмлетов
  21/496 руин включены, при камере в 300 км — 0/496.
- `ViewDistanceApplier` больше не присваивает `QualitySettings.shadowDistance`; поле в конфиге
  помечено устаревшим; URP-ассет не менялся.

**Открытые пункты (не блокируют Stage 1–2, блокируют Stage 3/5):**

| ID | Что | Почему важно | Как закрыть |
|---|---|---|---|
| **R0.1** | F8/F9 прошли **без фактического сдвига** (фокус уже в кванте) | реальный ребейз за 256 м не проверен; нужен перед Stage 5/6 | вывести фокус за порог и повторить F8→F9; 0 errors, без сдвига |
| **R0.2** | Раньше тяжёлый корабль падал/уезжал; **пользователь поправил 31.10**: `Ship_havyII_base` стоит у города и должен быть на платформе | нужен стабильный корабль и ship-baseline для Stage 3 (H2) | подтвердить в Play Mode, что корабль не падает/на платформе; снять ship-baseline (973 MR, DC) |
| **R0.3** | SetPass **316 vs 232** (+36 % при совпавших DC); инвентарь «17 751 MR» расходится с live 8 891 | не влияет на P1 DC, но нельзя публиковать как точный инвентарь | Frame Debugger/повторный подсчёт сцены; зафиксировать методику |
| **R0.4** | Baseline снят **до** фикса куллера; P1 как контроль сохраняется | сравнение Stage 2 делать на пост-фикс P1 27 131 | использовать P1 27 131 как новый контроль; руины — отдельно |

**Вердикт:** Stage 0 — **частичный pass**: baseline и оба фикса приняты; Stage 1/2 можно начинать;
Stage 3 (H2) — только HeavyII, после подтверждения стабильности корабля/баселайна (R0.2); Stage 5/6 — после R0.1.

**Brief для Stage 1:** [`T-CULL01_STAGE1_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE1_AGENT_BRIEF.md).

---

## 3. Stage 1 — P0 корабли: вынос коллайдеров/дверей (0.5–1 день)

**Цель:** убрать хрупкость: **238 из 250 BoxCollider** и **9 `DoorController`** лежат внутри
инстанса FBX. Реимпорт модели их сносит. Это **не** мерж визуала — только перестройка префаба.

**Сложный код:** `T-CULL01 / H1` (`ShipPrefabRepair`).
**Файлы:**
- Префаб: [`C:\Projects\ProjectC_client\Assets\_Project\Prefabs\Ships\Ship_havyII_base.prefab`](file:///C:/Projects/ProjectC_client/Assets/_Project/Prefabs/Ships/Ship_havyII_base.prefab)
- FBX: [`C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Ships\HeavyII_base\project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx`](file:///C:/Projects/ProjectC_client/Assets/_Project/Art/Models/Ships/HeavyII_base/project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx)
- Скрипты: `ShipController.cs` (39–41, 100–106, 461–462), `DoorController.cs` (30–32),
  `ShipRootReference.cs`, `ShipComponentLocator.cs`, `ShipDeckNav.cs` (26–30, 62, 67).
- Тул-референс: `BuildSettlementColliders.cs` (shipMode: 34–35, 89, 160, 268–269, 1438) и
  `ShipPresetCreator.cs` (40, 43, 526, 578–589).

**Действия:** вынести 250 боксов и 9 дверей в обычные дети корня префаба; узлы FBX не трогать;
сохранить локальные трансформы; двери/коллайдеры — вне инстанса FBX; проверить `DoorController`,
`ShipRootReference`, `ModuleSlot`, `PilotSeat`, `CargoVisual`, `ContrailVFX`.

**Gate:** реимпорт FBX не теряет коллайдеры/двери; `DoorController` работает; 0 missing refs;
scene-diff ≤ нескольких строк (правки только в префабе); F8/F9 зелёный.

### 3.1. Stage 1 review (Lead, 2026-10-31)

**Принято частично.** Фактически после H1 (`870ff534`, живая проверка prefab):
- быков всего **250**; внутри FBX было **248**, стало **10**; снаружи **240**;
- 110 user-дерер коллайдеров вынесены в `SHIP_COLLIDERS`; `collider_trees_left_inside_fbx = 0`;
- 9 `DoorController` и 10 leaf-`BoxCollider` **остались внутри FBX** осознанно: leaf-коллайдер
  обязан ехать с анимируемым узлом модели, а `DoorController` требует `Collider` на том же объекте;
- **reimport FBX выполнен**, состояние сохранилось (250/10/9/973 MR, 0 missing), CSV `H1_prefab_repair.csv`;
- визуал 973 MR/973 MF, source FBX и сцена не менялись; prefab/FBX — вне git (backup:
  `C:\Projects\ProjectC_client\_local_backups\T-CULL01\H1\Ship_havyII_base.prefab.bak`).

**Остаток H1 (не блокирует Stage 2):**
| ID | Что | Как закрыть |
|---|---|---|
| **H1-R1** | 9 дверей + 10 leaf-коллайдеров остаются в nested FBX | после любого reimport FBX **повторно прогонять** `ProjectC/Ships/Repair Heavy Prefab`; при H2 проверить, что мерж визуала их не сломал |
| **H1-R2** | `Doors` root не создан (двери на FBX-узлах) | отдельным решением, только если H2/фрактура потребуют вынести створки; сейчас не делать |

**Вердикт:** Stage 1 — **частичный pass**, можно переходить к Stage 2; Stage 3 (H2) — **только HeavyII**
и после подтверждения, что корабль стабилен у города/на платформе (R0.2) и снят ship-baseline.

**Brief для Stage 1:** [`T-CULL01_STAGE1_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE1_AGENT_BRIEF.md).

---

## 4. Stage 2 — Ф0 двери + бюджет теней (3–5 дней)

> ⚠️ **Нужно разрешение на `ProjectSettings/TagManager.asset`:** новый слой `PRIMUM_DOORS` (слот 10).
> Без него `layerCullDistances` невозможен; fallback — только per-object distance-менеджер (меньший эффект).
> **Brief для агента:** [`T-CULL01_STAGE2_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE2_AGENT_BRIEF.md).

**Цель:** снять 84 % DC без переделки геометрии. Контроль — P1 **27 131** (Stage 0, Midday); без городских
дверей **4 607**, без всех дверей **2 730**.

### 2.1. Слой дверей
- Создать слой `PRIMUM_DOORS` (свободен, слот **10**; см. `TagManager.asset`).
- Перенести 11 дверных префабов (`PM_DOOR_*`, `PM_ADM_ENTRANCE_PORTAL`) на слой; инстансы наследуют.
- `Camera.layerCullDistances[10] = 120–150 м`, `layerCullSpherical = false`.
- **`layerCullDistances` не влияет на тени** — отдельный `shadowCastingMode`-бюджет.

### 2.2. Бюджет теней/видимости (средний код → T-CULL01/H3)
- `DoorVisibilityBudget` + `ShadowBudget`: группы, гистерезис 10–15 %, 2–4 Гц, мультикамерный union,
  только `Renderer.enabled`/`shadowCastingMode`, без `SetActive`.
- Файлы-референсы: `DetailDistanceCuller.cs` (гистерезис, 0.5 с), `PrimumLightPool.cs` (выбор камеры),
  `GlobalMotionControlledRebaseSlice.cs` (FO, `TryRebuild`).

**Gate:** 26 981 → **5 000–9 000 DC**; двери в кадре — сотни; тени по согласованному визуалу;
F8/F9 зелёный; 0 errors.

### 4.0. Stage 2 review (Lead, 2026-10-31)

**Принято условно.** Runtime-замеры (Play Mode Host, Midday, `L_stage2_2026-10-31.csv`):
- **P1: 27 131 → 6 094 DC (−77.5 %)**, повтор P1 6 109 (**+0.25 %**); SetPass 316→283, tris 15.98→15.09 млн;
- P2 улица: 907 → **765** (−15.7 %); P3 крыша: 2 220 → **819** (−63.1 %);
- ночь: 17 020 → **5 019** (−70.5 %); `shadowCasters` 10 115 → 1 083 (P1), слой режет и тени.
- Двери: 5 493 рендерера на слое 10; `PRIMUM` 6 352 → 859; 2 598 групп теней; 150 м дистанция.
- Физика/raycast: 200 сэмплов, 0 ignore-пар, 199/200 OverlapSphere на слое 10; маски не ограничены PRIMUM.
- `DoorVisibilityBudget`/`ShadowBudget` авто-хостятся через `RuntimeInitializeOnLoadMethod` на `ViewDistanceRuntime`;
  в сцене/Bootstrap ссылок нет; `Camera.main` не используется.

**Гейт Stage 2 достигнут (P1 в коридоре 5–9k).** Условности:
| ID | Что | Как закрыть |
|---|---|---|
| **S2-R1** | В сцене остались **5 346 prefab-override `m_CastShadows: 0`** от edit-mode пробы; сцена была dirty | **уже откатано Lead** (`git checkout`; backup patch — `_local_backups/T-CULL01/Stage2/`), blob = HEAD; при следующем старте переоткрыть сцену |
| **S2-R2** | Визуальный pop-in дверей на 150 м, тени/ворота/ночь — не проверены глазами | пользователь: 3 точки, день/ночь, скрин/глаза |
| **S2-R3** | F8/F9 при реальном ребейзе не проверены (Stage 0 R0.1) | вывести фокус за 256 м, F8→F9 |
| **S2-R4** | Слой в **префабах вне git**; на чистом клоне нужен `Project C → Culling → Door Layer → Apply` | runbook: после импорта/клона запускать инструмент |
| **S2-R5** | `ShadowBudget` для ферм ≈0: фермы в 5.7–6 км, URP shadowDistance 1000 | не считать выигрышем; двери ферм уже на слое 10 |

### 4.0.1. Stage 2b — закрыт фактически

`Primum_farms_DOORS` (270 MR) — инстансы тех же префабов, слой 10 получен автоматически; отдельный агент
не нужен. `ShadowBudget` реализован, но при 5.7–6 км от P1 и shadowDistance 1000 даёт ≈0 — оставить как
задел на будущее. Визуальная проверка ворот — в S2-R2.

---

## 4.1. Stage 2b — Фермы: двери + тени (1 день, простые настройки)

**Цель:** убрать 270 дверных рендереров ферм из города и тени 1.27 млн тр. **Фактически уже закрыто:**
двери ферм — инстансы тех же префабов и получили слой 10 автоматически; `ShadowBudget` на 5.7–6 км
при shadowDistance 1000 даёт ≈0. Осталась только визуальная проверка (S2-R2).

- `Primum_farms_DOORS` (101 инстанс / **270 MR**) → слой `PRIMUM_DOORS` (тот же, что двери города);
  дистанция дверей 120–150 м.
- `shadowCastingMode = Off` геометрии ферм (`Primum_farms`: 142 MR / 150 slots / 99 ассетов / 116.6 МБ)
  — фермы в 963–1 051 м, их тени не читаются; **не клеить**: гранулярность уже правильная.
- Свет: 151 `Light` вне пула — проверить, что фермы не попадают в `PrimumLightPool` (≤96).
- Коллайдеры/3 mesh collider не трогать; HLOD L1 — позже (Stage 7).
**Gate:** −270 рендереров/≈−640 слотов из города, −1.27 млн тр в shadow pass; визуал ворот/теней ок;
F8/F9 зелёный.

---

## 5. Stage 3 — **только `Ship_havyII_base` (пилот)**; остальные корабли — вне scope

> **Brief для агента:** [`T-CULL01_STAGE3_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE3_AGENT_BRIEF.md).
> **Play Mode** — только с разрешением пользователя; сцена = HEAD, без override-дефекта Stage 2.

**Цель:** доказать весь пайплайн (P1 greeble → P2 LOD → позже P3 фрактура) на **одном тяжёлом
корабле**. Другие корабли в этих операциях **не трогаем**: medium, light и 20 NPC остаются как есть.
Если пилот зелёный — появится рабочий пайплайн, и остальные корабли выносятся в отдельный этап
масштабирования (§5.1).

**Текущее состояние (правка пользователя 2026-10-31):** `Ship_havyII_base` **стоит у города в сцене**
и должен быть **на платформе**. Ранняя запись о падении/уезде (`79953, −34884, 79367`) — историческая;
нужно подтвердить в Play Mode, что корабль стабилен на платформе, и снять ship-baseline (973 MR, DC).

| Объект | В Stage 3 | Кол-во | MR / slots | DC upper | Комментарий |
|---|---|---|---|---|---|
| **`Ship_havyII_base`** | **ДА — пилот** | **1** | **973 / 973** | **2 919** | H2 P1/P2, позже P3 |
| `Ship_medium_base` | **НЕТ** | 1 | 118 / 118 | 354 | не трогать; future scale-out §5.1 |
| `Ship_Light_root_fbx_test` | **НЕТ** | 1 | 428 / 428 | 1 284 | не трогать; future scale-out §5.1 |
| 20 NPC-заглушек | **НЕТ** | 20 | 120 / 120 | 360 | не трогать; 6 MR/820 тр. каждая |

**3a. Пилот heavy.**
- Предусловие: подтверждён стабильный `Ship_havyII_base` у города/на платформе; ship-baseline снят.
- H1 уже сделан для heavy (240/250 коллайдеров; 9 дверей/10 leaf — H1-R1).
- P1: greeble-мерж (359 объектов) → **973 → ~620 slot**; P2: `LODGroup` (300/900/2500 м);
  P3 — только после плана фрактуры → ~92 slot.
- Gate: Frame Debugger на **одном** инстансе; двери/cargo/слоты целы; reimport не ломает;
  heavy ≤~1 800 DC (P1) и ≤~1 300 (P2), визуал ок.
- **Другие корабли не трогать**, пока пилот не зелёный.

**3b. P3 / фрактура heavy — отдельно.**
- Только heavy, 8 «ячеек разрушения» × материал → ~92 slot; каждый кусок — отдельный GameObject
  с выпуклым коллайдером. Без входа геймдизайна по фрактуре **не начинать**.

**Сложный код:** `T-CULL01 / H2` (`ShipVisualMerger` + LODGroup builder) — **в версии «только HeavyII»**.
**Файлы/референсы:** `Ship_havyII_base.prefab`, heavy FBX, `ShipPresetCreator.cs` (40, 526, 578–589),
`CombineMeshesToCollider.cs` (31, 65–183, 114, 172 — **не как есть**), `I_ships_*.csv`;
выгрузка `.asset` в `Assets/_Project/Generated/Ships/**` (в git), prefab/FBX — Lore.
**Нельзя клеить:** 9 дверей, аппарель, `PilotSeat`, 11 `ModuleSlot`, `CargoVisual`,
`EngineThrusterVisual`/`ShipPartShake`, `ContrailVFX`, 250 коллайдеров, будущие фрактуры.

**Gate Stage 3 (только пилот):** heavy 973 → ~620 slot (P1) / ≤~1 300 DC; LOD работает;
двери/слоты/reimport/FO/сеть целы; **остальные корабли не изменены** (0 diff в их префабах).

### 5.0. Stage 3 review (Lead, 2026-10-31)

**H2 код:** `0bb07523`; **runtime-замеры:** `bd975340`; пользовательская правка сцены: `6ec32b4e`
(не агентская, не откатывать). Отчёт: `docs/dev/T-CULL01_STAGE3_REPORT.md`,
`T-CULL01_STAGE3_PLAYMODE_2026-10-31.md`, CSV `M_stage3_heavy_runtime_2026-10-31.csv`.

| Метрика | Результат | Вердикт |
|---|---|---|
| MR heavy | **973 → 616** (P1 greeble) | **PASS** (цель ~620) |
| DC heavy LOD0 200 м | **1 538** (было 2 916 модель) | P1 ≤1 800 **PASS**; P2 ≤1 300 **FAIL** |
| DC heavy LOD0 80 м | 1 015 (часть корпуса вне фрустума) | — |
| DC heavy LOD1 400 м | **235** (57 slot) | PASS |
| DC heavy LOD2 1200 м | **104** (52 slot) | PASS |
| Другие корабли | **0 diff** (mtime) | PASS |
| Двери/коллайдеры/слоты/VFX | целы; 9 дверей тест open/close; 250 Collider; 11 ModuleSlot | PASS |
| Reimport FBX | H1-R1 verify/apply OK | PASS |
| LOD пороги | raw 300/900; runtime `lodBias=1.30` → **эффективно 390/1170** | требует решения |
| F8/F9 | API `no_rebase_plan`, реальный ввод не проверен | OPEN |
| Визуал LOD/теней | глазами не принят | OPEN |

**Главный вывод Stage 3a:** P1 принят; **P2 тогда не был закрыт** (1 538 vs ≤1 300) — закрыт в Stage 3c.

### 5.0.1. Stage 3c — P2 ЗАКРЫТ (conditional)

**Код/замеры:** `e9fbe199` (ShipShadowBudget), `4a620f70` (T-LIGHT01 shadow config), `517f2f33` /
`57342a7e` / `6365b098` / `4de67173` (итерации бюджета и бенча). Отчёт: `docs/dev/T-CULL01_STAGE3C_REPORT.md`,
CSV `N_stage3c_heavy_2026-10-31.csv`, лог `N_stage3c_heavy_runtime_2026-10-31.txt`.

| Точка | ship_dc | Гейт ≤1 300 |
|---|---:|---|
| d80 | 971 | — |
| d120 | 815 | — |
| **d200** | **1 170** | **PASS** (+130) |
| **d200_repeat** | **1 161** | **PASS** (Δ 0.77 %) |
| **d300** | **1 236** | **PASS** (+64, запас 4.9 %) |
| d400 (LOD1) | 122 | — |
| d1200 (LOD2) | 104 | — |

**Механизм:** `ShipShadowBudget` (весь LODGroup, поштучно по дистанции; off >150 м, on <140 м) гасит
тени статики корабля дальше 150 м. На 300 м `shadowVisible=47` — остаётся только интерактив вне LODGroup
(двери/створки/ModuleSlot/двигатели/VFX). **Визуальная цена:** у корабля дальше ~150 м пропадает тень
крупных частей; силуэт на земле исчезает. Это **осознанный компромисс**, требует визуального «да» пользователя.

**T-LIGHT01 (не оптимизация, отдельный фикс):** тени переведены на `400 м / 4 каскада / soft on`
(ближний тексель 3.7 см вместо 57.5 см). **Следствие:** Stage 0/2 и Stage 3a замеры при `1000/2`
несравнимы с текущими; P1 6 094, heavy 1 538 — исторические. Текущий baseline надо перемерить
(поручение Stage 3b/Stage 4).

**Открыто:** визуальная приёмка теневого бюджета на 150–300 м и ночью; F8/F9 реальным вводом;
старый P1 baseline не перемерен. **Другие корабли — вне scope сейчас** (типовые; при помехах выключить рендереры).

**Вердикт:** Stage 3c — **conditional pass**; P2 закрыт замером; **другие корабли сейчас не делаем**.

**R0.2 (ship placement / save):** корабль без пилота падает на `DECK` (ожидаемо) и стоит; но
`ShipPositions.json` содержал позицию свободного падения и телепортировал корабль в пустоту (−41 570 м).
Агент исправил сохранение на позицию покоя; **но сохранение теперь перекрывает авторский hover 28.42 м над
`DECK`** — решение за пользователем (удалить/поправить save или оставить). Файл вне репозитория; не трогать без решения.

**Stage 3c (следующий шаг):** закрыть P2 на HeavyII. Brief:
[`T-CULL01_STAGE3C_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE3C_AGENT_BRIEF.md).

**Вариант 3c:** A) LOD-порог (200 м → LOD1), B) домерж LOD0-статики по материалам/зонам, C) теневой
бюджет корабля, D) принять 1 538 только по решению пользователя. Рекомендация: A + визуал; если
неприемлемо — B/C.

### 5.1. Остальные корабли — **не сейчас, вне scope Stage 4**

Решение пользователя 2026-10-31: **medium, light и 20 NPC сейчас не оптимизируем и по одному не смотрим**.
Они типовые, пойдут по тому же пайплайну HeavyII, когда/если понадобится. Никаких отдельных брифов,
H1b, LOD/мержей и «проверок каждого корабля» сейчас нет.

| Объект | Что делаем **сейчас** | Если мешает в кадре/замерах Stage 4 |
|---|---|---|
| `Ship_medium_base` | **ничего** | **выключить его MeshRenderer** на время замера, не трогая prefab |
| `Ship_Light_root_fbx_test` | **ничего** | **выключить его MeshRenderer** на время замера |
| 20 NPC | **ничего** | **выключить их MeshRenderer** на время замера |
| `Ship_havyII_base` | пилот, оставить как есть | не трогать |

Выключение — **только runtime, для чистоты замера**; не сохранять сцену, не менять префабы.
Пайплайн HeavyII считается рабочим образцом; future scale-out — отдельным решением позже, не в Stage 4.

---

## 6. Stage 4 — GRD + GPU occlusion (2–3 недели)

> **Brief для агента:** [`T-CULL01_STAGE4_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE4_AGENT_BRIEF.md).
> **Перед стартом:** перемерить baseline при текущих тенях `400 м / 4 каскада / soft on`;
> старые P1 6 094 / heavy 1 538 — исторические (T-LIGHT01). **Другие корабли сейчас не трогать**:
> если мешают в кадре — выключить их `MeshRenderer` на время замера (runtime only, без сохранения сцены).

**Цель:** выйти на 1 500–2 500 DC, включить Hi-Z.

**Сложный код/шейдеры:** `T-CULL01 / H5`.
**Файлы:**
- [`C:\Projects\ProjectC_client\Assets\_Project\Settings\ProjectC_URP.asset`](file:///C:/Projects/ProjectC_client/Assets/_Project/Settings/ProjectC_URP.asset)
  (строки 58–59, 73, 86–88) и `ProjectC_URP_Renderer.asset` (189–190).
- [`C:\Projects\ProjectC_client\ProjectSettings\GraphicsSettings.asset`](file:///C:/Projects/ProjectC_client/ProjectSettings/GraphicsSettings.asset) — строка **58**.
- Shader Graph: 15 графов города + 5 дверей; список — `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\C_city_materials.csv`
  и `C_shaders.csv`; папка `C:\Projects\ProjectC_client\Assets\_Project\Materials\**`.
- Package-источники URP 17.5:
  `C:\Projects\ProjectC_client\Library\PackageCache\com.unity.render-pipelines.core@9fe9377312a8\Runtime\GPUDriven\GPUResidentDrawer.Validator.cs`
  (18–19, 60),
  `...\universal@748d7458cc62\Runtime\UniversalRenderer.cs` (11–23),
  `...\UniversalRenderPipeline.cs` (1700, 1753),
  `...\UniversalRenderPipelineAsset.cs` (528, 1465–1468),
  `...\UniversalRendererRenderGraph.cs` (974–980, 1515–1524).

**Действия:** `BrgStripping=2 (KeepAll)`, `GPUResidentDrawerMode=InstancedDrawing`,
`GPUResidentDrawerEnableOcclusionCullingInCameras=1`; DOTS-варианты в ~20 графах; пилот на одном
чанке → вся сцена; проверить Hybrid Batch Group, тени, створки дверей, F8/F9.
**Gate:** DC ≤2 500; 0 shader errors; Hybrid Batch Group; двери/тени/FO ок. **Факт:** gate не взят
(3 031), но GRD принят условно по решению пользователя; следующий шаг — **Stage 4b**, не откат.

### 6.0. Stage 4 review (2026-10-31, `T-CULL01/H5`)

**Замеры (Play Mode, новый baseline при 400/4/soft; корабли выключены runtime-ом):**
[`T-CULL01_STAGE4_REPORT.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE4_REPORT.md),
`08_DATA/O_stage4_*`.

| Точка | baseline | GRD+occ | Δ |
|---|---:|---:|---:|
| C1 площадь | 6 400 | **3 031** | −52.6 % |
| C1 ночь | 5 013 | **1 767** | −64.7 % |
| C2 улица | 963 | 573 | −40.5 % |
| C3 крыша | 958 | 1 005 | +4.9 % |
| PILOT (чанк S9_TRD-S2) | 2 812 | **1 178** | −58.1 % |
| C1 + heavy | 6 390 | 3 015 | −52.8 % |

**Принято условно (решение пользователя 2026-10-31):** GRD остаётся включённым, хотя числовой гейт
2 500 не взят (3 031). `srpBatcherDC` 6 261 → 8, `hybridDC` = 2 876 на 18 196 инстансов.
**Визуально пользователь проверил в Play Mode 31.10 — «вроде всё окей»; это visual sign-off для GRD
(но не отменяет открытые F8/F9 и C3).**

**Ключевые выводы:**
- **GPU occlusion даёт 0 %:** прогон с `occlusion=0` совпал с включённой (3 029 vs 3 031). Условие Stage 5
  «окклюзия ≥35 %» не выполняется; Stage 5 требует отдельного решения, а не автоматического старта.
- **DC теперь упирается в уникальные пары (меш, материал)**, а не в рендереры: 2 267 уникальных мешей на
  8 801 рендерер; двери (4 949 рендереров, 12 мешей) уже схлопнуты, город — нет.
- **Все 20 Shader Graph уже имеют `DOTS_INSTANCING_ON`** — правок графов не потребовалось.
  Единственная ошибка — рукописный `ProjectC/ProceduralSteel` (`unity_ObjectToWorld` в DOTS-варианте),
  исправлен на `UNITY_MATRIX_M`; `shadersWithError = 0`.
- **Frame Debugger при GRD отдаёт 0 событий** — как доказательство Hybrid Batch Group не годится,
  использовать `UnityStats.hybridBatcherDrawCalls/hybridBatcherInstances`.
- Открыто: реальный ввод F8/F9, рост DC на C3 (+4.9 %), `lodBias 0.70` при quality Medium
  (числа Stage 3 при 1.30 несравнимы). Визуальный sign-off пользователя по GRD получен 31.10.

### 6.1. Stage 4b — gap close 3 031 → ≤2 500 — ЗАКРЫТ по числу

**Brief:** [`T-CULL01_STAGE4B_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE4B_AGENT_BRIEF.md).

Почему не B2 сразу: условие Stage 5 «окклюзия ≥35 %» **не выполнено (0 %)**, а данные J показывают, что
B2-переклейка увеличивает число units/slots (A 698/1 656 → B2 915/3 182; DC-модель 2 292 → 3 904).
Под GRD это может **увеличить** число unique (mesh, material)-пар и hybridDC. Сначала — безопасные
рычаги и dry-run-оценка B2, только потом решение о H4.

Порядок 4b: новый baseline → `m_SmallMeshScreenPercentage` (если доступен/согласован) → городской
shadow budget по дистанции → material/submesh atlas dry-run → **B2 dry-run** (не apply). Gate: P1 ≤2 500
без визуального регресса; если не выходит — зафиксировать и вынести решение по Stage 5/6.

**Результат (2026-11-01).** Отчёт: [`docs/dev/T-CULL01_STAGE4B_REPORT.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE4B_REPORT.md).
Данные: `08_DATA/P_stage4b_dryrun_2026-10-31.csv`, `P_stage4b_2026-10-31.csv`, четыре
`P_stage4b_grd_sm*_2026-10-31_raw.txt`. Бенч: `Stage4bGapBench` (11 кейсов), dry-run: `Stage4bDryRun`.

- **Гейт взят:** P1 3 047 → **2 423** DC при городском бюджете теней 200 м (−20.5 %, запас 3.1 %);
  150 м даёт **2 302** (−24.4 %). Baseline воспроизвёлся (+0.5 % к Stage 4, повтор −0.2 %).
  **Визуально пользователь проверил 01.11 — «вроде всё окей», visual sign-off получен.**
  `shadowCasters` 1254 → 636, `hybridDC` 2872 → 2255, `tris` 16.52 M → 12.36 M.
  Механизм: под GRD один снятый кастер снимает ровно один draw call, срез линеен и предсказуем.
- **`m_SmallMeshScreenPercentage` оставлен 0.** На 0.5 % P1 ухудшился (3 078, `hybridDC` 2 872 → 2 904
  при падении инстансов 18 204 → 17 832 — culling дробит батчи), 1–2 % дают pop-in и влияют на всю сцену.
  Свип 0→0.5→1→2 откачен; `git status` по `ProjectC_URP.asset` чист.
- **Atlas/submesh-мерж закрыт:** `shaderPairsInFrustum == pairsInFrustum` во всех кейсах, gain 0.
- **B2 закрыт данными:** на пилоте 160 пар против 532 у `J_B`; B2 дал бы 330 батчей
  (`verdict=B2_increases`), то есть ~6.3× текущего слитого состояния. Автоматического старта Stage 5 нет.
- **В игру добавлен** `Assets/_Project/Scripts/World/CityShadowBudget.cs`: тени off дальше 200 м,
  on ближе 180 м, ~3 Гц, слой дверей и корабли исключены, `Application.isPlaying`-guard против
  edit-mode записи `shadowCastingMode`. Включается сам; выключатель — `AutoBootstrap = false`.
- **Не сделано:** отдельный прогон steady state самого компонента (эквивалентность 2 423 доказана
  конструкцией, не замером); F8/F9 реальным вводом; перезамер `C3`.
- **Следующий шаг — финальная валидация:**
  [`T-CULL01_FINAL_VALIDATION_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_FINAL_VALIDATION_AGENT_BRIEF.md).

### 6.2. Final Validation — ❌ gate FAIL, нужен Stage 4c

**Факт 01.11 (Play Mode, реальный `CityShadowBudget`):**
- `CityShadowBudget` собрал **1 301** кандидата (ожидалось ~1 482), off 776; **P1 = 2 811 DC**,
  повтор 2 811; budget off = 3 047. **Гейт 2 500 не взят: +311 (12.4 %).**
- P1 с кораблями: 2 817; C2 565; C3 987/993; сцена чистая, prefab/FBX не менялись.
- F8: сдвиг 256 м, 0 errors — **PASS**.
- F9: `apply_refused:forced_user_validation_failure` / rollback `restore=True`, но bench ждал смену
  frameGen и записал `FAILED_no_transaction` — **критерий F9 в bench неверен**, продукт-путь требует
  отдельной проверки.
- Причина недобора: компонент собирает 1 301 рендерер вместо 1 482 (разница 181; вероятно фильтр
  `shadowCastingMode.Off` + момент Build/камеры). Бенч 2 423 с 1 482 кандидатами — **не подтверждён
  в игре**.

**Stage 4c:** диагностика candidate parity, исправление `CityShadowBudget`/Build, повторная валидация
P1 ≤2 500 и F9; после этого — утверждённые Stage 6 → Stage 7. Brief:
[`T-CULL01_STAGE4C_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE4C_AGENT_BRIEF.md).

---

## 7. Stage 5 — Воспроизводимость мержа + B2 города — ⛔ ЗАКРЫТ ДАННЫМИ

**Решение:** **B2 не делать.** Stage 4b dry-run на пилоте: 160 текущих пар против 532 у `J_B`;
B2 дал бы **330** батчей (`verdict=B2_increases`), то есть ~6.3× хуже. Окклюзия = 0 %, условие
«≥35 %» не выполнено. Stage 5 закрыт; H4 оставлен только как историческая справка, не запускать.

**Дополнительное предупреждение (Lead, 2026-10-31):** данные J показывают, что B2 **увеличивает**
units/slots относительно A (A 698/1 656 → B2 915/3 182; DC-модель 2 292 → 3 904). Под GRD DC зависит от
unique (mesh, material)-пар, поэтому B2 может **не уменьшить, а увеличить** hybridDC. До 2–4-недельной
переклейки обязателен **dry-run на пилоте S9_TRD-S2**: доказать снижение числа hybrid-батчей/DC, иначе
не делать. Решение о Stage 5 — только явное, после Stage 4b.

**Итог:** Stage 5 **закрыт**; H4 не выполнять. **Stage 6 и Stage 7 утверждены пользователем как
фундамент** — выполняются после Stage 4c, не «по необходимости».

**Сложный код:** `T-CULL01 / H4` (`PrimumCityRebatch` + `mapping.csv` + prefab-ассеты).
**Почему сложно:** 174 001 scene-override / 34.3 МБ (54.6 % сцены); prefab-источник
`PRIMUM_CITY.prefab` не в git; входные FBX/prefab — Lore; нельзя повторить scene-overrides.
**Файлы:**
- [`C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\PRIMUM_CITY.prefab`](file:///C:/Projects/ProjectC_client/Assets/_Project/Prefabs/City/PRIMUM_CITY.prefab)
- [`C:\Projects\ProjectC_client\Assets\_Project\Scenes\World\WorldScene_0_0.unity`](file:///C:/Projects/ProjectC_client/Assets/_Project/Scenes/World/WorldScene_0_0.unity)
- `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Primum\Rebased\**` (16 809 мешей)
- Референсы: `PrimumMeshRebase.cs` (32, 40, 48, 440–441), `PrimumMaterialMap.cs` (31, 38, 113–173),
  `DoorPrefabBuilder.cs` (63–70, 141, 219, 345, 514), `CombineMeshesToCollider.cs` (114).
- Данные J: `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\J_granularity_variants.csv`,
  `J_city_chunks_variants.csv`.

**Действия:** тул + манифест в git; dry-run (`Σtris ±0.1 %`, слоты = группы); prefab-ассет на чанк;
сцена подменяет child; пилот `S9_TRD-S2` (104 → ~330 B2); B2 = дом × материал только ≥1 м,
мелочь/интерьеры — чанк-батчи; **не** ячейки 10–30 м и не «по слоям».
**Gate:** цвет ±10 %, тени ≤+10 %, DC не растёт, Σtris ±0.1 %, F8/F9, reimport одного FBX;
при провале — откат к A.

---

## 8. Stage 6 — VisibilityDirector (3–4 недели) — ✅ УТВЕРЖДЁН

**Решение пользователя 01.11:** Stage 6 и Stage 7 **нужны как фундамент будущего**, не опциональны.
Stage 6 — первый после Stage 4c.

> **Brief для агента:** [`T-CULL01_STAGE6_AGENT_BRIEF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_STAGE6_AGENT_BRIEF.md).
> Текущий baseline: реальный P1 **2 394 DC** (4c), F8/F9 PASS; Stage 6 не должен ухудшить его.

**Когда:** мультикамерный union/бюджеты/корабли/интерьеры; CPU-куллинг; запас под будущий контент.
Также fallback, если GRD/тени не покроют новые сцены.

**Сложный код:** `T-CULL01 / H6`.
**Файлы:** новый `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\VisibilityDirector.cs`
(+ группы/FO-адаптер); референсы — `DetailDistanceCuller.cs` (13–107), `PrimumLightPool.cs` (33, 159–219),
`GlobalMotionControlledRebaseSlice.cs` (265–418, 1345, 1349–1354), `GlobalMotionActorContract.cs` (10–15).
**Требования:** ≤0.3–0.5 мс, 2–4 Гц, гистерезис, union только «настоящих» камер, FO-safe,
не трогать `DoorController`/коллайдеры/свет/сеть/VFX.
**Gate:** culling ≤2 мс; −60…−80 % DC без окклюзии; F8/F9 зелёный.

---

## 9. Stage 7 — HLOD/L1 + L2-импостор / PVS — ✅ УТВЕРЖДЁН

**Решение пользователя 01.11:** Stage 7 нужен как фундамент дальнего мира/L2; не опциональный.
Выполняется после Stage 6 (или параллельно, если Stage 6 не блокирует HLOD-пайплайн).

**Когда:** нужен L0 <1k или L1/L2; город заморожен; есть бюджет 6–10 недель.
**Сложный код:** `T-CULL01 / H7` (HLOD-baker/impostor bake/portal-PVS).
**Файлы:** `C:\Projects\ProjectC_client\Assets\_Project\Editor\` (новый HLOD tool),
`C:\Projects\ProjectC_client\docs\world\primum_city\01_SETUP\04_LOD_AND_DISTANCE.md`,
`C:\Projects\ProjectC_client\docs\world\distantfocus\README.md`.
**Gate:** L1 ≤500 DC, L2 ≤10; силуэт узнаваем; FO; без rebake-катастрофы.

---

## 10. Роли и как передавать сложный код

| Этап | Кто | Что передаём |
|---|---|---|
| Stage 0 | Lead/пользователь | простые фиксы, baseline |
| Stage 1 | **другой агент** по `T-CULL01/H1` | `ShipPrefabRepair` |
| Stage 2 | Lead/другой агент | дверной слой — простые правки; бюджет — `T-CULL01/H3` |
| Stage 2b | Lead/пользователь | двери ферм на слой, тени off — простые настройки (1 день) |
| Stage 3 | **другой агент** по `T-CULL01/H2` | `ShipVisualMerger` + LOD |
| Stage 4 | **другой агент** по `T-CULL01/H5` | GRD/шейдеры |
| Stage 5 | ⛔ closed (B2 increases) | — | не выполнять |
| Stage 6 | **approve** — после 4c | VisibilityDirector: culling ≤2 мс; −60…−80 % DC | агент T-CULL01/H6 | `VisibilityDirector` |
| Stage 7 | **approve** — после 6 | HLOD/L1 + L2/PVS: L1 ≤500, L2 ≤10 | агент T-CULL01/H7 | HLOD/impostor/PVS |

**Протокол передачи:** перед этапом открыть `T-CULL01`, взять соответствующий раздел `H*`,
приложить актуальный baseline/ветку и попросить агента:
1. прочитать `T-CULL01` + указанные absolute paths;
2. сделать только этот этап (не трогать соседние);
3. сдать diff + отчёт с замерами по gate;
4. не запускать Play Mode/не коммитить без разрешения;
5. при провале gate — откат по чек-листу.

---

## 11. Общие правила (для всех этапов)

- **Research-only документы не дают права менять сцену.** Перед кодом — отдельная задача/одобрение.
- Никаких **scene-overrides** для мержа; только prefab-ассеты/`.asset` в git.
- Никаких **static-флагов** на ездящем контенте; 2 076 static GO (коллайдеры/холдеры) не трогать.
- Не трогать `Terrain`, `PRIMUM_COLLIDERS`, `DoorController`, `PrimumLightPool`, `NetworkManager`,
  `ClientSceneLoader`, `ScenePlacedObjectSpawner`.
- FO: только frame-local данные или локальные координаты ячеек; после F8/F9 — сброс кэшей;
  публичного события ребейза нет (хук `TryRebuild`/поллинг `CumulativeRebaseOffset`).
- Бюджет culling ≤2 мс; всё дороже — off by default.
- Каждый этап — отдельный маленький коммит с тикетом `T-CULL01`; push не делать.

---

## 12. Ссылки

- План/обоснование: [`09_BEAM_COMPARISON_AND_DECISIONS.md`](09_BEAM_COMPARISON_AND_DECISIONS.md),
  [`05_RECOMMENDED_ARCHITECTURE.md`](05_RECOMMENDED_ARCHITECTURE.md),
  [`06_EXPERIMENTS_AND_ROADMAP.md`](06_EXPERIMENTS_AND_ROADMAP.md).
- Тикет сложного кода: [`C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md`](file:///C:/Projects/ProjectC_client/docs/dev/T-CULL01_COMPLEX_CODE_HANDOFF.md).
- Отчёты: [`07_SUBAGENT_REPORTS/`](07_SUBAGENT_REPORTS/) — A–J; ключевые H/I/J.
- Данные: [`08_DATA/`](08_DATA/).
