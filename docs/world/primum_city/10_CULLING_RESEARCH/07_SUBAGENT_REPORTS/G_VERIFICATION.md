# G. Верификация отчётов A–F и аудит противоречий

**Дата:** 2026-10-30 · **Автор:** верификатор (независимо, read-only) · **Статус:** final · **Режим:** ничего не
менялось; Play Mode не запускался; `scene.isDirty=false` до/после; MCP — только чтение (`execute_code`-запросы,
`unity_reflect`, `LoadAssetAtPath`). Сверены финальные версии: C (22:36), D (22:44, 35.8 КБ), F (22:47, 37.9 КБ).

## 0. Вердикт

**A — accept-with-corrections.** Стек подтверждён: `m_RenderingMode:2=ForwardPlus` (`UniversalRenderer.cs:11–23`),
отдельный `CullShadowCasters`, depth-prepass как код-путь, API/лимиты GPU RD, `maxShadowDistance=min(asset.shadowDistance,
far)`. Ошибки: (1) дверная модель на **2.02 сабмеша** — это 3 033 строки `C_primum_batches.csv` с «DOOR» (геометрия
батчей города), а не префабы; верно **5.19 material slot**/инстанс ⇒ «V≈4 100 в фрустуме» неверно (≈1 380 инстансов /
2 881 рендерер); (2) «93 % opaque+prepass» держится только на этой `m`; (3) `AtmosphereCamera` — не «far 120 000 и второй
полный кадр», а меню-камера far 10 с маской одного слоя. Нет §8 (формат брифа).

**B (draft) — accept-with-corrections.** Цены/версии в основном точны (GPU Instancer Pro $128/0.12.20/2026-10-07;
ACS2 $69.95/1.5.8/2026-09-02; Amplify $60/1.1.0; «ни один не декларирует Unity 6.x» — подтверждено). Ошибки: Perfect
Culling сейчас **$97.46** и релиз **11.02.2026** (в отчёте 107.21/12.02); Amplify — **29.10** (в отчёте 28.10).
Главное по смыслу: «2 363 двери — уникальные меши, дедуп — предусловие» неверно — инстансы делят меши 12 префабов,
уникальных пар `(mesh,material)` **72**; дедуп уже выполнен.

**C — accept-with-corrections.** Лучшая фактура: подтверждены scene-мерж (1 207 MR/1 806 слотов), prefab asset 16 468 MR,
174 001 override / 34 321 170 B, двери 2.11 MR/5.19 слота, 12 189 слотов `PRIMUM_DOORS`, 13 471 на все, «761 батч +
180 прочих + 266 ADMIN», 2 076 static GO без рендереров, 8 891 MR, настройки URP. Ошибки: prefab-слоты **20 694**
(не 20 954); `m_LocalScale` в PRIMUM-блоке **50 112** (не 16 704); «HEAD `5b41dd7c`» — это коммит, blob на HEAD
`52e4be48`; разложение дверного DC «цвет 12 189 + тени 5 577/9 838» молча предполагает 100 % видимость и **нет prepass**
(противоречит 04 §5 и код-пути). Риск мержа верный, но prefab-источник вообще не в git.

**D — accept-with-corrections.** FO подтверждён построчно (одна сцена-участник, порог/квант 256, 5 с/35 с, только
активные корни, `TryRebuild` no-op, публичного события нет, первый ребейз ≈56.5 км, пилот на одну сцену, каталог 154/2,
`AtmosphereCamera` far 10, `Camera.main`-мина, shadowDistance no-op). Ошибки: у двери «2 материала» — на деле **5 слотов
(2+3)**; 11 из 23 заглушек — 46 КБ/14 GO (не ~37/12) и имена корней транспонированы; `PRIMUM_DOORS` ADMIN не включает;
2 087 Light (сейчас 2 238); «17 172 бокса» (10 811); «×2 DC от 2 каскадов» (Δ 4 262–4 276).

**E — accept.** Внешние цитаты проверены дословно: GTA V 4 155 DC (G-buffer ≈1 900, CSM ≈1 000, planar 650,
transparent 11, cubemap ~30/грань); DOOM 1 331 DC (3 072 кластера, 8k×8k атлас, Umbra + GPU queries, depth prepass).
Мелочь: «8.7 % тр.» по сцене = 7.4 %.

**F (draft) — accept-with-corrections.** Верно: 13 471 слот/5.2 на инстанс, **72 пары**, публичный GPU-occlusion API,
нет `OcclusionCullingData` (0 типов), 143 `.mat`, 4 ProceduralSteel, 0 ссылок в сцене, FO-safe варианты. Критично
неверно: «12 `.mat` на 8 кастомных шейдерах; город/двери — URP-шейдеры, DOTS поддерживают» — **все 49 материалов города
(15 графов) и 6 дверей (5 графов) — Shader Graph**; блокер шире, «0.5–1 день на ProceduralSteel» и «GRD включается без
переписывания» — не подтверждено.

## 1. Метод и что проверено

| Источник | Результат |
|---|---|
| MCP `execute_code` (8 запросов) | 2 сцены (`WorldScene_0_0` активна, `BootstrapScene`), `isDirty=false`; 8 891 MR (8 803 active); `WorldRoot_0_0` 7 152/19 754 Transform/15 393 слота/72 материала/2 076 static GO; `PRIMUM` 1 207 (941+266 ADMIN; 1 133 active)/1 806; `PRIMUM_DOORS` 4 949/12 189; 2 238 Light; 18 375 Collider (10 811 в `PRIMUM_COLLIDERS`); 2 630 `DoorController`; тр. 6 845 503 |
| `LoadAssetAtPath` | `PRIMUM_CITY.prefab`: 16 468 MR/MF, **20 694** слота, 5 834 138 тр., 17 184 Transform |
| `unity_reflect` | `OcclusionCullingData` — **0 типов**; `BatchRendererGroupStrippingMode={KeepIfEntitiesGraphics=0, StripAll=1, KeepAll=2}`, текущее = 0 |
| Парс YAML/JSON | 174 001 override / 34 321 170 B (54.6 % сцены); 16 704 `m_Mesh`, 20 955 material-slot, 50 112 localScale, 2 307 `m_IsActive`; 4 906 PrefabInstance/4 648 GO/849 MR в сцене; конфиг ребейза; 12 дверных префабов; 23 заглушки; каталог 154/2 |
| Git | HEAD `49837d2d`; blob сцены = `52e4be48` (62 869 056 B) = рабочее дерево; сцена tracked; `PRIMUM_CITY.prefab` **никогда не коммитился** (`git log --all` пуст; `*.prefab` в `.gitignore:153`) |
| Пакеты (в точку) | URP: `UniversalRenderer.cs:11–23`, `UniversalRenderPipeline.cs:1700`, `UniversalRenderPipelineAsset.cs:528,1465–1468,1744–1777`, `UniversalRendererRenderGraph.cs:1515–1524`; Core: `GPUResidentDrawer.Validator.cs`; Shader Graph: `PragmaDescriptor.cs:48`, `PropertyCollector.cs:175–260` |
| Файловый скан | 393 shader-like файла — **0** с `DOTS_INSTANCING_ON`; 375 `.mat` (143 в `Assets/_Project/Materials`); §2 п.25–26 |
| Веб (30.10) | adriancourreges GTA V/DOOM (HTTP 200); 4 страницы Asset Store (цены/версии/даты/совместимость) |

## 2. Таблица исправлений

| # | Отчёт | Что записано | Правильно | Источник |
|---|---|---|---|---|
| 1 | A §2.5 | «сабмешей 2.02 (653/1768/502/110 из 3 033 строк)» | Это строки CSV с «DOOR» (среднее 2.0227) — батчи города. Дверь: **2.11 MR, 5.19 слота** | парс 12 префабов; `C_doors.csv` |
| 2 | A §2.5 | V≈4 090 дверей, S0≈700 | При 5.16 слота: V≈**1 380 инстансов** (2 881 рендерер, 04 §5) | §4.3 |
| 3 | A §0.10 | «93 % — opaque+prepass» | `[LIKELY]`, не измерено; prepass-путь есть, при 2 881 рендерере цифры = 2+0.5 прохода | §4.3; URP код |
| 4 | A §0.3 | depth prepass просят Clouds/DistantFocus | DistantFocus внутренне **`Active: 0`**; Depth — VolumetricClouds (`:447`), Normal\|Depth — EdgeDetection (`:230–231`) | asset:130; код фич |
| 5 | A §0.13, бриф, 04 §35 | `AtmosphereCamera` far **120 000**, «второй полный кадр» | **far 10**, near 0.01, mask `1<<9` (PCMM_Atmosphere), только меню; мира не видит | `ProjectCMainMenuAtmosphere.cs:151–155` |
| 7 | B §2.1 | Perfect Culling $107.21 / 2026-02-12 | **$97.46**, v1.3.0.4, **Feb 11, 2026** | Asset Store 30.10 |
| 8 | B §2.4 | Amplify релиз 2026-10-28 | **Oct 29, 2026** | Asset Store |
| 9 | B §3.2, §4 | «двери уникальны; дедуп — предусловие» | Инстансы делят меши/материалы 12 префабов; пар **72**; дедуп уже есть | парс префабов |
| 10 | C §2.1 | prefab asset 20 954 слота | **20 694** (20 954 — сумма субмешей Rebased-мешей, +1.2 %) | MCP |
| 11 | C §2.1 | «HEAD `5b41dd7c`» | Blob на HEAD — **`52e4be48`**; `5b41dd7c` — последний коммит, менявший сцену; содержимое идентично | git |
| 12 | C §2.1 | `m_LocalScale` 16 704 | В PRIMUM-блоке **50 112**; 16 704 — это `m_Mesh` | парс YAML |
| 13 | C §2.4/§3 | «цвет 12 189 + тени 5 577/9 838» | Подгонка под 100 % видимость без prepass; противоречит 04 §5/prepass; публиковать только измеренную разницу 17 766/22 027 | §4.3 |
| 14 | C §2.1/§6 | риск «мерж можно потерять» | Мерж закоммичен; **prefab-источник не в git вообще** → потерять можно только возможность пересборки prefab; риск = хрупкость overrides + 32.7 MiB + нечитаемые диффы | git |
| 15 | C §2.4 | «12 189×1.46=17 766, ×1.81=22 027 — точно» | 1.4575 и 1.8071; произведения 17 796/22 062 (+0.15 %) — округление | пересчёт |
| 16 | D §8.4 | дверь «2 материала»; «2 renderer × 2 материала × 2 каскада» | **5 слотов**: Leaf 2 + Frame 3 (4 уникальных материала) | YAML префаба |
| 17 | D §2.5 | «23 сцены ~37 КБ / 12 GO» | 12 сцен: 37.3–39.7 КБ/12 GO/6 MR; **11: 46.0–46.1 КБ/14 GO/7 MR** (+`NorthPoleBlocker`); имена корней транспонированы (`WorldScene_0_3` → `WorldRoot_3_0`) | YAML 24 сцен |
| 18 | D §2.6 | `PRIMUM_DOORS` «(+ADMIN 266)» 4 949 | 4 949 — только `PRIMUM_DOORS`; ADMIN 266 отдельно; всего городских дверных MR 5 215 | live |
| 19 | D §2.6 | «2 087 Light» | **2 238** под `PRIMUM_LIGHTS` (всего 2 240; enabled в редакторе 153) | live |
| 20 | D §2.6 | «`PRIMUM_COLLIDERS` 17 172 бокса» | **10 811**; всего в сцене 18 358 (18 232 Box) | live |
| 21 | D §3 | «2 каскада ⇒ ×2 DC дверей» | Δ(door DC) A−B = **4 262–4 276** (19–24 %), не ×2 | 04 §3 |
| 22 | D §0.3 | игрок (39 460; 2 475; 40 000), \|T\|=56 571 | Это позиция корня PRIMUM; спавн **(39 835; 2 533; 40 003)** → s=(39 936; 2 560; 39 936), **\|T\|=56 536 м** (вывод верен) | `BootstrapScene` RespawnManager |
| 23 | D §2.6 | «1 116 `_INT_`» | В live 306 MR с `_INT_` (1 116 — из prefab-asset) | live |
| 24 | E §2.1 | «двери 8.7 % тр.» | 505 656/6 845 503 = **7.4 %**; 8.7 % — оценка сверху | live |
| 25 | F §0.5 | «12 `.mat` на 8 кастомных шейдерах; город/двери — URP-шейдеры» | 143 `.mat` верно; но 81 на 15+ Shader Graph, 21 на 12 рукописных, 38 URP Lit/Unlit. **Город = 49 Shader Graph (15 графов)+1 Unlit; двери = 6 SG (5 графов)** | GUID-скан `.mat`→`.shadergraph`; `C_city_materials.csv` |
| 26 | F §0.5/Handoff | «блокер узкий, 0.5–1 день; GRD без переписывания» | ProceduralSteel: 4 мата в `Steel/archive/`, 0 ссылок — да; блокер — ~20 Shader Graph. SG 17.5 умеет DOTS-прагму/пропы, но требует настройки графов (`HybridPerInstance`); в текущих графах ключей нет → объём `[UNVERIFIED]` | пакет SG; JSON графов |
| 27 | F §1/§7, C §2.5 | ground truth «8 863 MR» | Live: **8 891** MR (8 803 active), 2 240 Light, 18 375 Collider | MCP |
| 28 | 02/E | «`m_BrgStripping: 0 = Strip`» | **0 = KeepIfEntitiesGraphics**; StripAll=1, KeepAll=2; блокер реален (нужен 2) | reflect |

## 3. Противоречия и разрешение

1. **«Мерж в git?»** C: «baseline не в git / на диске немёрженный город» → неверно: сцена tracked, blob=HEAD,
   `isDirty=false`, мерж в сцене. Итог: **мерж закоммичен в scene-overrides; prefab-ассет не смержен и не в git** →
   риск = хрупкость + 34.3 МБ балласта + нет источника в git, а не «потеря мержа».
2. **Дверь: 2.02 ↔ 5.19 ↔ 2 материала.** Прав **C**: 2.11 MR/5.19 слота/13 471/72 пары; 2.02 — чужая выборка,
   «2 материала» — недоучёт (5 слотов).
3. **Разложение 17 766 DC.** A: `m(2V+S0)`, m=2.02 → V≈4 100. C: 12 189×1.46 (цвет+тени). Логика: в B **один**
   каскад ⇒ без prepass ≤2 DC/видимый слот; при 2 881 рендерере в фрустуме (7 095 слотов) наблюдаемое 2.50 ⇒ **prepass
   есть**, «все двери видимы» (C) и m=2.02 (A) неверны. В синтез — только измеренная разница.
4. **RenderingMode.** Все сошлись: **Forward+**; `01_INDUSTRY_PRACTICE` §8 устарел (enum + ассет).
5. **AtmosphereCamera.** Бриф/04/A: 120 км/второй кадр; D: 10 м/меню → **D прав** (код, маска слоя 9). Ошибка исходных
   доков, не только A.
6. **Prepass.** A: «есть, просят Clouds/DistantFocus»; C: «потенциальный, не подтверждён» → prepass-путь реален
   (EdgeDetection Normal|Depth, VolumetricClouds Depth; DistantFocus выключен), фактическое наличие не замерено.
   Драйверы A неверны, вывод «может удваивать opaque» правдоподобен.
7. **Шейдеры/DOTS.** C: 40 шейдеров, 0 DOTS, город — SG; F: 12 `.mat`, город — URP-шейдеры → **C прав** по городу/дверям
   и 0 DOTS; F прав по ProceduralSteel (0 ссылок) и 72 парам.
8. **BrgStripping.** «0=Strip» (02/E) vs «нужен KeepAll» → блокер есть, но 0 = KeepIfEntitiesGraphics.
9. **Дверные счётчики.** 2 363 / 2 598 / 2 630 → 2 363 = `PRIMUM_DOORS`; 2 598 = +ADMIN 133 + farms 101 + портал 1;
   2 630 = +32 дверных `DoorController` на кораблях/пещерах/городах. В модель: 2 598/12 189/13 471.
10. **Static batching.** Флаг 1, но 2 076 static GO, ни у одного нет MR ⇒ батчить нечего (подтверждено).
11. **GPU RD/Forward+.** Требование Forward+ — не в core-валидаторе, а в URP (`usesClusterLightLoop`,
    `UniversalRenderPipelineAsset.cs:1744–1772`); валидатор требует RawBuffer, kernel `OccluderDepthDownscale`, KeepAll,
    mode≠Disabled, allowInEditMode, `IGPUResidentRenderPipeline`. У нас Forward+ ✓, `allowInEditMode` захардкожен `true`.

## 4. Ключевая арифметика

**4.1 Три уровня (не смешивать).** Prefab asset: 16 468 MR / **20 694** слота / 5 834 138 тр. Scene-instance (HEAD):
**1 207 MR / 1 806 слотов / 5 438 062 тр.** (941 структурных + 266 ADMIN; 761 батч-имя + 180 прочих). `PRIMUM_DOORS`:
**4 949 MR / 12 189 слотов / 505 656 тр.** `WorldRoot_0_0`: 7 152 MR / 19 754 Transform / 15 393 слота / 72 материала /
6 691 773 тр. / 2 076 static. Сцена: 8 891 MR / 17 138 слотов / **6 845 503 тр.** / 62 869 056 B; PRIMUM-блок overrides
**34 321 170 B (54.6 %)** / **174 001** мод (16 704 `m_Mesh`, 20 955 material-slot, 50 112 localScale, 2 307 `m_IsActive`).

**4.2 Двери.** 2 598 инст. × 2.114 MR = 5 493; × 5.185 = **13 471** слот; `PRIMUM_DOORS` 2 363 × 2.094/5.158 = 4 949/12 189.
6 уникальных материалов, 11 используемых типов, **72 пары `(mesh,material)`**.

**4.3 DC-модель (исправленная).** Измерено (04 §3): B 21 118, двери 17 766 (от 3 352); A 26 981, двери 22 027 (от 4 954);
84.2 %/81.6 %; Δ(door) A−B = 4 262–4 276. При 2 881 рендерере в фрустуме (58.2 %; 2 881/4 949) = **7 095 слотов**:
B 17 766/7 095 = **2.50 DC/слот** = 2 (opaque+prepass) + 0.50 тени; A = **3.11** = 2 + 1.11 (2 каскада). В B 1 каскад ⇒
без prepass максимум 2.0 < 2.50 ⇒ **prepass почти доказан** (при доверии 04 §5). A прав в наличии prepass, но m/V неверны;
C's «12 189 цвет + тени» — подгонка, не публиковать. Не-двери: слотов 15 393−12 189 = 3 204; 3 352 DC ⇒ 1.05 на слот сцены
(≈2.0 на видимый при 52 % видимости) — сходится.

**4.4 Unmerge.** Prefab asset 20 694 слота (не 20 954). C: «~40 тыс. DC» множителем слотов — верхняя оценка (та же доля
видимости+prepass); с per-object frustum culling ожидаемо **25–40 тыс.** ⇒ регресс ×1.2–1.9 — направление устойчиво,
число `[LIKELY]`; с GRD/инстансингом может быть дешевле (гипотеза).

**4.5 Прочее.** Первый ребейз: спавн (39 835; 2 533; 40 003) → s=(39 936; 2 560; 39 936), \|T\|=**56 536 м**. Каталог: 154
`entries`/2 сцены/134 299 B. Заглушки: 12×37.3–39.7 КБ/12 GO + 11×46.0–46.1 КБ/14 GO ≈0.96 МБ. Артефакт C: 12 189×1.46=17 796
(не 17 766), ×1.81=22 062 (не 22 027).

## 5. Что осталось непроверенным

| Вопрос | Чем проверять |
|---|---|
| Split DC по проходам, V/S0/S1, факт prepass | Frame Debugger: `Draw Mesh` в Depth Prepass/DepthNormals/Opaque/Shadow; A/B отключения EdgeDetection/VolumetricClouds (ожидание −25…−35 %) |
| Включение GRD: 20 Shader Graph, KeepAll, створки/тени | Ветка: `m_GPUResidentDrawerMode`, `m_BrgStripping=2`, лог валидатора, `Hybrid Batch Group`; откат |
| DOTS-настройки графов (`HybridPerInstance`/instancing) | Graph Settings 20 графов; A/B-сборка с `DOTS_INSTANCING_ON` |
| Реальные V/DC при far 30–120 км, 3 точки | Play Mode + Profiler/Frame Debugger, пресеты Near/Medium/Far |
| Стоимость ребейза (мс) | Stopwatch F8/F9 ×5 + Profiler |
| `layerCullDistances`: семантика 0, влияние на тени | Тестовая ветка: слой X vs 0, объект на 2X |
| Umbra `Failure in split phase` | Только повторный бейк (мутация) — вне ресерча |
| Механика ассетов B | Ручное чтение/триал (HTML описаний не отдаёт) |

## 6. Рекомендации Lead

**Как факты (с источником):** (1) двери = 84.2 % (B) / 81.6 % (A) — измеренная разница 17 766/22 027; (2) дверь =
2.11 MR/5.19 слота; 12 189/13 471 слотов; 72 пары; (3) сцена = 8 891 MR / 2 630 `DoorController` (2 598 городских) /
2 238 Light / 18 358 Collider / 6.84–6.85 млн тр. / 2 076 static без MR; (4) мерж закоммичен (blob=HEAD, `isDirty=false`),
174 001 override/34.3 МБ; prefab 16 468 MR/20 694 слота — не смержен и не в git; (5) Forward+; shadow 1000/2/2048/split 0.25;
доп. тени off; SRP Batcher on; GPU RD/occlusion off; `m_BrgStripping`=KeepIfEntitiesGraphics (нужен KeepAll=2); (6)
`QualitySettings.shadowDistance` — no-op (`UniversalRenderPipelineAsset.cs:528,1465–1468`; `UniversalRenderPipeline.cs:1700`);
(7) prepass-путь реален (EdgeDetection Normal|Depth; VolumetricClouds Depth; DistantFocus `Active:0`), но не замерен;
(8) требования GRD по исходникам (RawBuffer/kernel/KeepAll/mode/allowInEditMode/IGPUResident); Forward+ — URP-сторона,
выполнена; (9) Umbra: runtime-типа `OcclusionCullingData` нет; `m_OcclusionCullingData=0`; (10) FO: одна сцена/порог 256/
5 с/35 с/нет события/TryRebuild no-op/первый \|T\|≈56.5 км/пилот 1 сцена/каталог 154; (11) камеры: статичная `MainCamera`
(233 км) + риг с тем же тегом ⇒ `Camera.main`-мина (комментарии `PrimumLightPool.cs:33`, `GazeAutofocus.cs:116`);
`AtmosphereCamera` far 10; «1 000 000» — только комментарий; (12) город/двери — Shader Graph (49/6 материалов);
0 shader-файлов с `DOTS_INSTANCING_ON`; ProceduralSteel 4 мата/0 ссылок; (13) 23 заглушки: 12×37–39.7 КБ, 11×46 КБ,
имена корней транспонированы; (14) внешние ориентиры E и цены B (с поправками §2).

**Только как гипотезы:** любой split дверного DC (в т.ч. 12 189+5 577/9 838 и V≈4 100); факт наличия prepass в кадре;
«unmerge ⇒ 40 тыс.», «GRD ⇒ 1.5–2.5 тыс.», «low-code ⇒ 5–6 тыс.», «PVS ⇒ 500–1 200»; «GRD за 2–5 дней правок»
(объём DOTS для 20 графов не установлен); перенос норм GTA/DOOM; «8.7 % тр.»; Umbra как «принципиально FO-несовместимая».

**Дешёвые проверки:** Frame Debugger split + prepass A/B → `shadowCastingMode=Off` дальним дверям → `layerCullDistances`
(семантика 0!) → read-only аудит DOTS 20 графов → повтор базового замера при far 30 км.

## 7. Источники/команды

**MCP:** `execute_code` (сцены/`isDirty`; счётчики по иерархии; prefab через `LoadAssetAtPath`), `unity_reflect`
(`OcclusionCullingData`→0; `BatchRendererGroupStrippingMode` + значения enum).
**Git:** `rev-parse HEAD`, `ls-tree -l HEAD -- scene`, `hash-object`, `log --all -- prefab`, `check-ignore -v`, `ls-files`.
**Файлы:** сцены `WorldScene_0_0`/`BootstrapScene`/24 `WorldScene_*`, `PRIMUM_CITY.prefab`, 12 `Doors/*.prefab`,
`ThirdPersonCamera.prefab`, каталог пилота, `ProjectC_URP*.asset`, `ProjectSettings/*.asset`, FO-код, `ViewDistance*.cs`,
фичи `EdgeDetection/DistantFocus/VolumetricClouds`, `PrimumLightPool.cs`, `ProjectCMainMenuAtmosphere.cs`.
**Пакеты:** URP/Core/Shader Graph — пути и строки в §1 (версии `universal@748d7458cc62`, `core@9fe9377312a8`,
`shadergraph@2df19e8719bc`).
**Веб (30.10.2026, данные недоверенные — только факты):** adriancourreges GTA V / DOOM 2016 (4155/1331 DC и разбор);
Asset Store: gpu-instancer-pro-290293 ($128/0.12.20/2026-10-07), perfect-culling…-193611 ($97.46/1.3.0.4/2026-02-11),
advanced-culling-system-2…-272830 ($69.95/1.5.8/2026-09-02), amplify-impostors-119877 ($60/1.1.0/2026-10-29).

## Handoff

**Главный вывод:** A–F в основе верны; C — самый точный по мержу/дверям/настройкам, D — по FO/камерам, E — по внешним
цитатам. Решающие поправки: дверь = 5.19 слота (не 2.02); prepass почти доказан ограничением «1 каскад ⇒ ≤2 DC/слот»,
но split не измерен; мерж закоммичен, а prefab-источник не в git; город/двери — Shader Graph, поэтому DOTS-блокер шире,
чем «ProceduralSteel».

**Три факта:** 17 766/22 027 — измеренная разница; 2.11 MR/5.19 слота/72 пары; `Camera.main`-мина и `AtmosphereCamera`
far 10 подтверждены кодом, «120 000» — ошибка доков.

**Не проверено:** split по проходам/prepass (Frame Debugger), GRD/DOTS-аудит 20 графов, ребейз в мс, семантика
`layerCullDistances`, far 30–120 км, механика ассетов B.
