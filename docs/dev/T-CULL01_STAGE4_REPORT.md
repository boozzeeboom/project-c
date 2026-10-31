# T-CULL01 / Stage 4 (H5) — отчёт: GPU Resident Drawer + GPU occlusion + DOTS-аудит шейдеров

**Дата:** 2026-10-31 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `82d06500`
**Unity** `6000.5.2f1` · **URP** `17.5.0` · Forward+ · `colorSpace = Linear`
**Scope:** город/двери. **Корабли не оптимизировались:** `Ship_havyII_base` не менялся, medium/light/20 NPC
не менялись; на время city-замера их `MeshRenderer` выключались runtime-ом, сцена не сохранялась.

**Статус: CONDITIONAL GREEN.** Решение пользователя 2026-10-31 23:16 — **GRD оставлен включённым**.
Числовой гейт `DC ≤2 500` **не выполнен**: P1 = **3 031** (−52.6 % от нового baseline 6 400).
`Hybrid Batch Group` подтверждён счётчиками движка, 0 shader errors, FO/двери/тени без измеренных регрессий.
GPU occlusion в этом мире **не даёт ничего** (§4) — весь выигрыш даёт инстансинг GRD.

---

## 1. Что сделано

| Файл | Что |
|---|---|
| `ProjectSettings/GraphicsSettings.asset` | `m_BrgStripping: 0 → 2 (KeepAll)` — единственная строка |
| `Assets/_Project/Settings/ProjectC_URP.asset` | `m_GPUResidentDrawerMode: 0 → 1`, `m_GPUResidentDrawerEnableOcclusionCullingInCameras: 0 → 1` |
| `Assets/_Project/Shaders/ProceduralSteel.shader` | `unity_ObjectToWorld` → `UNITY_MATRIX_M` (DOTS-вариант не компилировался; §5) |
| `Assets/_Project/Editor/Culling/Stage4GrBench.cs` | **новый** bench: city-точки, runtime-выключение кораблей, чанк-пилот, A/B-конфиг в имени файла, `Hybrid Batch Group`-метрики |
| `docs/.../08_DATA/O_stage4_*.txt/.csv` | новый baseline, GRD run1, GRD run2 (после фикса шейдера), GRD без окклюзии |

Настройки правились только с явного разрешения пользователя (получено 2026-10-31 22:4x).
Бэкап до правки: `_local_backups/T-CULL01/Stage4/ProjectC_URP.asset.bak`, `GraphicsSettings.asset.bak`.

## 2. Методика

Воспроизведена методика Stage 2/Stage 0 без изменений: Play Mode → Host (`MainMenuWindow.OnHostClicked`),
фаза Midday forced через `DayNightController` (компонент выключен), геймплейный риг `ThirdPersonCamera_0`,
`SpringArmCamera` выключен, far 30 000, FOV 60, near 0.1, прогрев **1500** кадров, **40** кадров успокоения
на точку, **3** замера с шагом 8 кадров через `UnityStats`, медиана.

Отличия Stage 4:
- все рендереры под корнями с `ShipRootReference` (4 корабля + 20 NPC = **1 350** рендереров) выключаются
  на время city-замера; отдельный контрольный кейс `C1_heavy` возвращает только heavy (619);
- кейс `PILOT` — только чанк `PRIMUM/PRIMUM_HOUSES_S9_TRD-S2_P1_45H` (габариты + 4 м), камера ставится
  относительно чанка; это тот же чанк, что назначен пилотом в H4;
- в конфиг-строку пишутся `grdMode / occlusion / brgStripping / тени / quality / lodBias`, а имя файла
  само получает метку `baseline` / `grd` / `grd_noocc`.

Актуальные настройки на момент замеров: `shadowDistance 400`, `cascades 4`, soft on, shadowmap 2048,
`quality = Medium (2)`, **`lodBias = 0.70`** (в Stage 3 отчётности фигурировал 1.30 — числа Stage 3
и Stage 4 сравнивать нельзя ни по теням, ни по LOD).

## 3. Новый baseline (GRD выключен, корабли выключены)

| Точка | DC | SetPass | tris | shadowCasters | рендереров в кадре | дверей в кадре |
|---|---:|---:|---:|---:|---:|---:|
| C1 площадь (Midday) | **6 400** | 304 | 15 998 182 | 1 382 | 3 826 | 3 154 |
| C1 повтор | 6 400 | 304 | 15 998 182 | 1 225 | 3 826 | 3 154 |
| C1 ночь | 5 013 | 249 | 8 256 162 | 0 | 3 826 | 3 154 |
| C2 узкая улица | 963 | 191 | 3 301 178 | 320 | 165 | 77 |
| C3 крыша | 958 | 259 | 4 330 894 | 327 | 559 | 282 |
| PILOT (чанк S9_TRD-S2) | **2 812** | 177 | 4 262 466 | 603 | 508 | 335 |
| C1 + heavy | 6 390 | 310 | 15 997 282 | 1 366 | 3 826 | 3 154 |

Старые P1 6 094 / heavy 1 538 — **исторические**, не используются: тени сменились на 400/4/soft.
Отдельно видно, что heavy на дистанции площади даёт **≈0–10 DC** (6 390 vs 6 400) — на этой позе он
вне теневой дистанции и почти вне кадра.

## 4. GRD + GPU occlusion: результат

| Точка | baseline | **GRD** | Δ DC | SetPass | `hybridDC` | инстансов |
|---|---:|---:|---:|---:|---:|---:|
| C1 площадь | 6 400 | **3 031** | **−52.6 %** | 304 → 216 | 2 876 | 18 196 |
| C1 повтор | 6 400 | 3 031 | −52.6 % | 304 → 217 | 2 876 | 18 196 |
| C1 ночь | 5 013 | **1 767** | **−64.7 %** | 249 → 161 | 1 621 | 16 833 |
| C2 улица | 963 | 573 | −40.5 % | 191 → 154 | 449 | 835 |
| C3 крыша | 958 | 1 005 | **+4.9 %** | 259 → 213 | 820 | 2 122 |
| PILOT чанк | 2 812 | **1 178** | **−58.1 %** | 177 → 152 | 1 072 | 2 708 |
| C1 + heavy | 6 390 | 3 015 | −52.8 % | 310 → 211 | 2 866 | 18 180 |

- `srpBatcherDC` 6 261 → **8**: SRP Batcher полностью уступил Hybrid Batch Group.
- Воспроизводимость: run1 и run2 (после фикса шейдера) совпали до единиц DC (3 031 / 3 031, 1 178 / 1 178,
  573 / 573, 1 005 vs 1 009, 1 767 / 1 767).
- **C3 (крыша) — единственная точка с ростом** +4.9 %; сырые замеры там шумят (988|1003|1003),
  причина не установлена; на гейт не влияет, но в визуальную приёмку попадает.
- tris выросли на 3–6 % (15.998 → 16.481 млн на C1): GRD рисует часть геометрии, которую frustum-куллинг
  раньше отбрасывал, а окклюзия этого не компенсирует (§5).

### 4.1. GPU occlusion отдельным прогоном: вклад ≈ 0

Прогон `grd_noocc` (GRD включён, `m_GPUResidentDrawerEnableOcclusionCullingInCameras = 0`):

| Точка | GRD + occlusion | GRD без occlusion | Δ |
|---|---:|---:|---:|
| C1 площадь | 3 031 | 3 029 | −0.07 % |
| C1 ночь | 1 767 | 1 759 | −0.45 % |
| PILOT | 1 178 | 1 176 | −0.17 % |
| C2 | 573 | 571 | −0.35 % |
| C3 | 1 005 | 1 003 | −0.20 % |

**Вывод:** Hi-Z окклюзия GRD в этом мире не снимает ни одного вызова — разница в пределах шума.
Флаг возвращён в `1` (стоимости не даёт, конфиг соответствует брифу), но рассчитывать на окклюзию как на
источник экономии нельзя. Это закрывает вопрос из открытых решений Stage 5 («окклюзия ≥35 %»):
**порог не выполняется, окклюзия = 0 %.**

## 5. Shader-аудит (15 город + 5 дверей)

Все шейдеры города и дверей — Shader Graph, и **у всех уже есть варианты** `DOTS_INSTANCING_ON` и
`INSTANCING_ON` (`shader.keywordSpace.keywordNames`, `ShaderUtil.ShaderHasError == false`):

```
Wood, Bronze, IronRust, Concrete, Glass, Cobble, Tiles, Fabric, Marble, Brick,
Plate, Lantern, Foliage, Shader Graphs/Steel_v2  +  URP Lit, URP Unlit
```

Дверные материалы — `M_PC_Wood_Dark`, `M_PC_IronRust`, `M_PC_Bronze`, `M_PC_Concrete`, `M_PC_Wood`,
`M_PC_Glass_Green` (5 219 слотов на 4 949 рендереров): все на DOTS-способных графах.
**Править графы не потребовалось** — Shader Graph 17.5 генерирует DOTS-варианты сам (закрыт открытый
вопрос §13.5 handoff).

Найдено и исправлено (это и был единственный shader error):

| Что | Где | Суть |
|---|---|---|
| `ProjectC/ProceduralSteel` | `Assets/_Project/Shaders/ProceduralSteel.shader:233` | `unity_ObjectToWorld` не существует в DOTS-варианте → `ForwardLit + DOTS_INSTANCING_ON` не компилировался. Заменено на `UNITY_MATRIX_M` (DOTS-осведомлён, в обычном пути тот же `unity_ObjectToWorld`) |

До фикса ошибка появлялась только после включения GRD (вариант компилируется по требованию):
`Shader error in 'ProjectC/ProceduralSteel': undeclared identifier 'unity_ObjectToWorld' ... with DOTS_INSTANCING_ON`.
После фикса: `shadersWithError = 0`, в прогоне run2 новых ошибок нет. Затронут 1 тестовый рендерер
(`WorldRoot_0_0/Cube/Sphere`, материал `M_PC_Steel_Clean`).

Остаточные (предсуществующие, не блокирующие) замечания:
- `TestMat_GhibliGradient` использует `Hidden/InternalErrorShader` (`TestObjects/ShaderTest_Clouds`) — тестовый материал, вне scope;
- предупреждения компилятора (`implicit truncation`, `duplicate keyword line`, unroll) — не ошибки;
- `Project C/Stars/Stars`, `Project C/Moon/MoonLunarPhase`, `TextMeshPro/*` не имеют `DOTS_INSTANCING_ON`
  (43 TMP-слота, небо) — GRD их не батчит, вклад в DC пренебрежим.

## 6. Frame Debugger: нерабочий инструмент при включённом GRD

При `grdMode = 1` Frame Debugger отдаёт **0 событий** (`frame_debugger_enable` → `event_count: 0`,
`frame_debugger_get_events` → пусто) и в покое, и после pause/unpause. Для сравнения: в Stage 3 при
GRD off он давал 217 событий. То есть **Frame Debugger нельзя использовать как доказательство
`Hybrid Batch Group` в этом проекте** — заменой служат счётчики движка:

```
UnityStats.hybridBatcherDrawCalls   = 2876   (было 0)
UnityStats.hybridBatcherInstances   = 18196  (было 0)
UnityStats.srpBatcherDrawCalls      = 8      (было 6261)
UnityStats.totalIndirectDrawCalls   = 0
```

2 876 hybrid-батчей на 18 196 инстансов — это и есть Hybrid Batch Group; SRP Batcher при этом погашен.
`indirectDC = 0` — поэтому `hybrid*` счётчики являются единственным надёжным признаком (не путать с
`standardIndirectDrawCalls`, которые к GRD не относятся).

## 7. Двери, тени, эмиссия, FO

| Проверка | Результат |
|---|---|
| Двери в кадре на C1 | 3 154 рендерера (то же множество, что в baseline) — двери участвуют в гибридных батчах и не выпали |
| `DoorController` / коллайдеры | не трогались; слой 10 и `layerCullDistances[10] = 150` без изменений |
| Тени | `shadowCasters` 1 382 → 1 259 на C1 (следствие батчинга, не отключения теней); 400/4/soft не менялись |
| Эмиссия/стекло/terrain | материалы не менялись; шейдерные варианты те же, кроме исправленного `ProceduralSteel` |
| FO | GRD не трогает Transform'ы; сцена не сохранялась; авто-rebase идёт, `no_rebase_plan` в логе — предсуществующее поведение (как в Stage 3) |
| Сцена | `isDirty = False`, не сохранялась; правок сцены/префабов/FBX нет (`git status`: только 3 файла + bench + docs) |

Наблюдение по логу (не регрессия): после возврата игры в главное меню во время паузы (22:59:44–22:59:53)
всплыли `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class...` —
это меню-код на legacy `Input` при активном Input System, к GRD отношения не имеет.

## 8. Вердикт по гейту

| Критерий | Факт | Итог |
|---|---|---|
| DC ≤ 2 500 | C1 = **3 031** (−52.6 %), ночь 1 767, PILOT 1 178 | ❌ **не выполнен** (перебор 21 %) |
| `Hybrid Batch Group` есть | `hybridDC 2876 / 18196` инстансов, `srpBatcherDC 8` | ✅ |
| 0 shader errors | 0 после фикса `ProceduralSteel` | ✅ |
| двери/тени/FO без регрессий | измеренных регрессий нет; визуальная приёмка — за пользователем | ⚠️ частично |
| корабли: heavy / medium / light / NPC не изменены | 0 diff префабов, рендереры только runtime-off | ✅ |
| сцена не сохранена | `isDirty=False` | ✅ |

**Решение пользователя:** оставить GRD как **conditional** (не откатывать), т.к. −52.6 % DC и −29 % SetPass
дороже, чем 2 500 в текущей геометрии; числовой гейт остаётся открытым.

## 9. Что осталось / почему 2 500 не взялись

DC после GRD ограничен **числом уникальных пар (меш, материал)**, а не числом рендереров:
2 876 батчей на 18 196 инстансов (≈6.3 инстанса на батч). В городе 8 801 рендерер, но всего
2 267 уникальных мешей, при этом `PRIMUM` — это 1 207 рендереров почти с уникальными мешами, а 4 949
дверных рендереров дают всего 12 мешей и 6 материалов. То есть двери уже «схлопнуты» инстансингом,
а город — нет, потому что каждый дом собран из отдельных мешей.

Отсюда порядок дальнейших шагов:
1. **Stage 5/B2 (дедуп/переклейка мешей)** — единственный прямой способ уронить DC ниже 2 500;
   пилот на `S9_TRD-S2` уже измерим этим же bench'ем (кейс `PILOT`).
   Внимание: формальное условие плана «окклюзия ≥35 %» **не выполнено** (§4.1) — начинать Stage 5 нужно
   отдельным решением пользователя, а не автоматически.
2. `m_SmallMeshScreenPercentage` (GRD small-mesh culling) — не включался: срезает мелкие меши, даёт pop-in,
   требует отдельного замера + визуального допуска.
3. Визуальная приёмка GRD пользователем: переходы дверей/теней, стекло, эмиссия, ночь, крыша (C3 +4.9 %).
4. Реальный ввод F8/F9 после включённого GRD (API-тест `no_rebase_plan` его не заменяет).
5. Кейс C3 стоит перепроверить отдельно: единственная точка с ростом и с шумными сырыми замерами.

## 10. Откат

```
git checkout -- ProjectSettings/GraphicsSettings.asset Assets/_Project/Settings/ProjectC_URP.asset
```
либо копии из `_local_backups/T-CULL01/Stage4/*.bak`. Возврат к состоянию `grdMode=0 / occlusion=0 /
brgStripping=0` даёт 6 400 DC на P1. Фикс `ProceduralSteel.shader` откатывать не нужно — он устраняет
ошибку компиляции и на GRD-off путь не влияет.

## 11. Артефакты

```
docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/O_stage4_baseline_2026-10-31_raw.txt
docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/O_stage4_grd_2026-10-31_run1_raw.txt
docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/O_stage4_grd_2026-10-31_run2_raw.txt   (после фикса шейдера)
docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/O_stage4_grd_noocc_2026-10-31_raw.txt
docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/O_stage4_2026-10-31.csv                 (4 прогона, 28 строк)
Assets/_Project/Editor/Culling/Stage4GrBench.cs                                            (меню: Project C → Culling → Stage 4 Bench → Run / Abort)
```
