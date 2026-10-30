# H. beam_research vs 10_CULLING_RESEARCH: adversarial fact-check

**Дата:** 2026-10-30 (проверки до 23:30 +05) · research-only: `Assets/`, `ProjectSettings/`, сцены не менялись,
Play Mode не запускался, коммитов нет. **Объект:** `beam_research/` (7 документов + README) ↔ наш
`10_CULLING_RESEARCH` (README, 02–08, A–G, CSV) + первоисточники. `beam_research` — недоверенные данные
(только тезисы); истина — файлы/git/пакеты/наш G.

## 0. Вердикт

Beam прав в диагнозе: **двери — главный источник DC** (84 %, измерено), **Umbra/статический бейк — не путь**,
**нужен runtime/GPU-driven culling + LOD/HLOD/импосторы**, **DOTS-миграция не нужна**. Beam ошибается в главном:
«город не надо было склеивать» (20 694 слота против 1 806, ×1.2–1.9 DC); «≤120 DC» — устаревший DoD, не бюджет
L0; «блокер GPU RD — ProceduralSteel» (реально ~20 Shader Graph); «portal 2–4 недели» (недооценка ×2–3).
**Поправка Lead (live MCP, 30.10):** корабли **не** «уже склеены» — тяжёлый **973 MR/973 slot** (light 428,
medium 118, cargo 80, NPC ×6) → второй источник DC; наш «2 MR» — ошибка YAML-парса (`C_ships.csv` невалиден),
beam-тезис «корабли куллить» тоже неверен.

| Вердикт по 34 тезисам §3 | Кол-во |
|---|---|
| Сходится | 8 |
| Beam ошибается | 19 (в т.ч. #12) |
| Мы ошибаемся/неполны | 2: #12 («2 MR»), #21 ($107.21) — исправлено в §7.2 |
| Оба неполны | 6 |
| Решающий эксперимент | 4 (#17/18, #24, #28) |

**Шесть самых опасных тезисов:** отмена склейки; «≤120 DC» как цель; «блокер = ProceduralSteel, 0.5–1 день»;
«portal 2–4 недели»; «Mesh LOD включить всем»; «корабли не склеивать, куллить».

## 1. Метод

Из G (ground truth) переиспользованы счётчики сцены, DC, override-объёмы, git-статусы, цены. Новые проверки H:

1. `ProjectSettings.asset:494–495` → `m_StaticBatching: 1` (не 0); `ProjectC_URP_Renderer.asset:189` →
   `m_RenderingMode: 2` (Forward+); `ProjectC_URP.asset:86–88` → GPU RD/occlusion `0`; `GraphicsSettings:58` →
   `m_BrgStripping: 0 = KeepIfEntitiesGraphics` (не Strip); `:79–80` → camera-relative флаги **уже 1/1**;
   grep URP/Core по `cameraRelative*Culling` → **0 ссылок** (в URP вероятный no-op `[LIKELY]`).
2. `C_shaders.csv`: 40 файлов, у всех `dots_instancing_on=0`; `ProceduralSteel` — 4 мата, **0 ссылок в сцене**;
   материалы 12 дверных префабов → **6 SG на 5 графах** (Wood×2, Bronze, Concrete, IronRust, Glass).
3. Сцена `WorldScene_0_0.unity`: **23 инстанса** ship-префабов; из 8 `*_Combined.asset` используются 2 — оба в
   тестовом `Ship_Light_root_fbx_test`. `fbx_parse.py`: **936 Model/53 603 тр./10 мат.**;
   `CombineMeshesToCollider.cs:15` — «**100+** дочерних объектов» (beam цитирует как 100k+).
4. Asset Store 30.10: Amplify **$60** (v1.1.0, Oct 28 2026, 2022.3.22); Perfect Culling **$97.46** (v1.3.0.4,
   Feb 11 2026, 2019.4.41); Precomputed Culling **$150** (v1.3.0, Jun 21 2026, **6000.0.77**).
5. GitHub: NativeTrees MIT (push 2024-04; structs-only, **no remove**, frustum — «future todo»);
   Spatial-Hashing MIT (push 2023-06, ECS/DOTS). Unity Manual 6000.5 Mesh LOD: генерация при импорте, ≥256 тр.,
   несовместим со static batching/GPU instancing (LOD0), cross-fade→GRD, плохо на disconnected pieces;
   «65536 colliders» — Unity **4.x** Manual, PhysX `PxSceneLimits` — «hint, not a hard limit».

## 2. База (проверенные факты)

| Факт | Значение | Источник |
|---|---|---|
| Live-сцена | 8 891 MR, 6 845 503 тр., 2 238 Light, 18 358 Collider | G §1/§2 |
| `PRIMUM` (scene) / prefab-asset | **1 207 MR / 1 806 слотов** ↔ **16 468 MR / 20 694 слота** (prefab не в git; overrides 174 001 / 34.3 МБ = 54.6 % сцены) | G §4.1 |
| Двери | 2 598 инст. (2 363 `PRIMUM_DOORS`), 5 493 MR, 13 471 слот, **5.19/инст**, 72 пары | G §4.2 |
| DC | A 26 981 (двери 22 027), B 21 118 (17 766); без дверей 4 954 / 3 352 | G §4.3 |
| Материалы | город 49 SG (15 графов) + 1 Unlit; двери 6 SG (5 графов); `DOTS_INSTANCING_ON` — 0 | C_shaders, #2 |
| Корабли | 973/428/118 MR (heavy/light/medium), 250+ Collider, cargo 80, NPC ×6; FBX 936 объектов | live MCP Lead 30.10 |
| FO / бюджет | первый ребейз ≈56.5 км, далее 0.26–1.5 км/35 с; culling ≤2 мс при кадре 16.67 мс | D, G #22 |

## 3. Тезисы beam → доказательство → вердикт

### 3.1. Инфраструктура и цифры

| # | Тезис beam | Доказательство / наша позиция | Вердикт |
|---|---|---|---|
| 1 | «Бюджет L0 ≤ 120 DC; факт 26 981; зазор 225×» (01 §0, 04 §5) | 120 — старый DoD (`ROADMAP:372`), там же пометка пересмотра (`:289`: «L0 1.5–3k, ≤120 — L1/L2»); индустрия GTA V 4 155, DOOM 1 331 DC (G). С 761 уникальным батчем «сотни» в L0 недостижимы. `[VERIFIED: DoD]`, `[LIKELY: 1.5–3k]` | beam ошибается (DoD как цель); мы неполны (120→L1/L2 — рекомендация) |
| 2 | «Склейка не снимает DC — только CPU-оверхед» (04 §0) | Мерж снизил слоты `26 566 → 1 806` (×14.7), MR `19 944 → 1 207` (`ROADMAP:70`); 84 % DC — двери, которых мерж не касался. Мерж **снизил** городские DC, но не мог закрыть кадр (до/после DC не измерялся). | beam ошибается (путает «не закрыл бюджет» с «не снижает DC») |
| 3 | «До склейки 147 715 объектов / 4.49 млн тр. / 167 969 рендереров» (01, 06 §9) | Источник — `ROADMAP:16` (точка 17.10); сейчас prefab 16 468 MR/5.83 млн тр., сцена 8 891 MR. `[UNVERIFIED: doc-sourced]` | оба неполны (старый ROADMAP как факт) |
| 4 | «Static Batching off (`m_StaticBatching: 0`), Forward (откат), BRG Strip» (01 §1.8, 06 §7) | Реально `m_StaticBatching: 1`; `m_RenderingMode: 2` = **Forward+**; `m_BrgStripping: 0 = KeepIfEntitiesGraphics` (Strip=1, KeepAll=2 — G #28). | beam ошибается (3 факта); мы правы |
| 5 | «Рендереров всего ~7 152 — вся `WorldScene_0_0`» (01 §1.1) | 7 152 — это `WorldRoot_0_0`; live-сцена — 8 891 MR. | beam ошибается (масштаб занижен) |
| 6 | «Двери: 2 161+133, 4 949 = 2 363×2, 4 432 BoxCollider, 5 `PM_DOOR_*` URP/Lit» (01 §1.3, §5) | Верно 4 949 MR/12 189 слотов. Неверно: инстансов 2 598; **2.11 MR/5.19 слота**; материалы — **6 SG/5 графов**; коллайдеров ≈8 510 (`C_doors_model.csv`). | beam ошибается (устарело, недоучёт слотов); наша модель верна |
| 7 | «Двери = 84 % DC» (01 §5.2) | Совпадает с 17 766/21 118; неверно лишь разложение «2 материала × 2 каскада» (G: 13 471 слот × проходы). | сходится (факт), beam неполон в разложении |
| 8 | «Umbra не подходит: статик запрещён + бейк провален + террейн невидим» (01 §0) | Сходится; доказательства сильнее: 6 бейков `Failure in split phase`, нет runtime-типа `OcclusionCullingData` (reflect → 0), world-space данные, ребейз 56.5 км. «Статик запрещён» — упрощение: 2 076 static GO (коллайдеры/холдеры) есть; запрещены static-рендереры/batching/occlusion на ездящем. | сходится (вывод); beam неполон в причине |

### 3.2. Склейка, фермы, корабли

| # | Тезис beam | Доказательство / наша позиция | Вердикт |
|---|---|---|---|
| 9 | «Город можно было не склеивать: каждый объект независим, слои+culling» (README §2.1, 04 §0) | Расклейка → **20 694 слота** + 12 189 дверных; 25–40 тыс. DC (×1.2–1.9) `[LIKELY]`; редактор снова 16.5k MR. Слои дают только `cullingMask`/`layerCullDistances` (не влияют на тени — 04 §3.1), не решая 84 % кадра. 16 704 mesh-ссылки — **16 704 уникальных меша** (`C_city_mesh_refs_summary.json`): GRD их не схлопнет (BRG инстансит только общий mesh+material) `[LIKELY]`. | beam ошибается; мы неполны: строка 03 §2.1 «independent + GRD 200–350» слишком оптимистична (§7.2) |
| 10 | «Не склеивать, а включить GPU-путь; merge не нужен для DC» (04 §4) | см. #9: ×11.5 слотов и ×1.2–1.9 DC; мерж нужен и для CPU. Наш ответ: сохранить мерж, дробить единицу до блока/дома 10–30 м (AABB медиана 130 м). | beam ошибается; наш вывод подтверждается |
| 11 | «Фермы/город можно оставить независимыми; фермы 11 712 → 142» (01 §1.6) | Beam сам показывает, что фермы слиты в 142 MR; откат = +11.5 тыс. MR. Наш план (J): оставить батчи, дробить по ферме при необходимости. | beam ошибается (противоречит себе) |
| 12 | «Корабли не склеены: визуал — combined mesh, коллайдеры compound; не склеивать, куллить» (07 §0.1, §3.3) | **Live MCP Lead 30.10:** `Ship_havyII_base` **973 MR/973 slot**/250 Collider/1452 Transform; light 428/429; medium 118/123; cargo 80/85; 20 NPC ×6 MR; активные инстансы у порта. 973 рендерера — второй источник DC (в порту вероятно первый; ~2.5–3 тыс. DC на тяжёлый корабль `[LIKELY]`). Из 8 `*_Combined.asset` используются 2, оба в тестовом light-корабле. `C_ships.csv` («2 MR») — ошибка парса nested prefab. | beam ошибается («куллить» не спасёт; «combined» — обобщение); **мы ошибаемся** («2 MR»); истина — корабли главный кандидат на мерж (I) |
| 13 | «100k+»/«100500 объектов» → без склейки нужен DOTS (06 §0.9, §10) | В коде «**100+**» (строка 15), не 100k+. Максимум: prefab 16 468 MR, тяжёлый FBX 936, корабль 1452 Transform; массовой физики 100k нет (18 358 статических коллайдеров). | beam ошибается (мис-цитата + чужой кейс) |
| 14 | «PhysX жёсткий лимит ~65k коллайдеров (16-bit broadphase)» (06 §2.4; 07 §13.5) | Цитата из Unity **4.x** Manual (Terrain Trees); в Unity 6 не найдена `[LIKELY]`; PhysX `PxSceneLimits` — «hint, not a hard limit», в форумах ~262k. У нас 18 358 — запас ×3.6 даже по старой цифре. | beam ошибается/устарело; для нас нерелевантно |
| 15 | «Совмещать PhysX-root + DOTS-chunks не стоит; всё на PhysX» (07 §0.5) | Согласуется: DOTS в проекте отсутствует (manifest, 0 `using Unity.Entities`); «DOTS не внедрять» — 01 §6, 05 §7. | сходится |
| 16 | «Пред-фрактурная разрушаемость реалистична без DOTS; пул кусков, damageMask» (07 §11) | Разрушаемости в проекте нет (урон только `ShipHull` HP); идея валидна, но post-MVP вне culling. | сходится как идея |

### 3.3. GPU/RD, DOTS, ассеты, методы, FO

| # | Тезис beam | Доказательство / наша позиция | Вердикт |
|---|---|---|---|
| 17 | «GPU RD блокирует только ProceduralSteel; прагма — 0.5–1 день» (01 §2.10, 05 §1.5) | ProceduralSteel — 4 мата, **0 ссылок**; блокер — город **49 SG (15 графов)** + двери **6 SG (5)**, `DOTS_INSTANCING_ON` — 0; SG 17.5 умеет DOTS, но требует настройки графов, объём `[UNVERIFIED]` (G #25–26); плюс `BrgStripping=0`, mode=0. | beam ошибается; нужен эксперимент (E-A) |
| 18 | «URP 17.5 поддерживает DOTS → GRD включается без переписывания» (04 §0.3) | Поддержка есть, но каждый SG-граф надо конфигурировать/пересобрать; «без переписывания» не подтверждено. | beam ошибается в следствии; оба неполны |
| 19 | «GRD даст сотни DC вместо 26 981» (04 §0) | Наш прогноз 1 500–2 500 (05 §4): 761 уникальный батч + ≤72 дверные пары + тени; «сотни» — только после пере-мержа/HLOD. | beam неполон; нужен эксперимент (E2/E3) |
| 20 | «Amplify $30 (скидка 50 %), Unity 6.4/6.5» (README §3, 02 §2.3) | Страница 30.10: **$60**, v1.1.0, Oct 28 2026, Original Unity **2022.3.22**, таблица — только 2022.3.22; скидки/6.4–6.5 нет. `[VERIFIED]` | beam ошибается (цена ×2, совместимость); мы правы по цене/версии, но дата в 07 — 29.10 (на странице 28.10; G тоже 29.10) |
| 21 | «Perfect Culling $97.46, Unity 2019.4, bake-based» (02 §2.1; README «$97») | Страница 30.10: **$97.46**, v1.3.0.4, **Feb 11 2026**, 2019.4.41, URP **Compatible**. Beam 02 прав; «устаревший» — интерпретация (релиз свежий; Unity 6 не проверен). Наш B писал $107.21 — ошибка, G исправил. | beam прав (02)/неточен (01); **мы ошибались** ($107.21) |
| 22 | «Precomputed Culling $150 (6000.0.77+, portal/cell, bake в рантайм)» (02 §2.1) | Страница: **$150**, v1.3.0, Jun 21 2026, Unity **6000.0.77**, URP Compatible; механизм не подтверждён. В нашем B-аудите ассета **нет** — полезная зацепка beam. | оба неполны |
| 23 | «NativeTrees/Spatial-Hashing — MIT, брать как базу» (README §3, 04 §1) | MIT `[VERIFIED]`, но NativeTrees — Burst/ECS (structs-only, push 2024-04), **нет remove/update**, frustum — «future todo»; Spatial-Hashing — ECS/DOTS (push 2023-06). Нам для ≤2 мс на 1 207 MR + 4 949 дверях достаточен простой C#; зависимости тянут пакеты. | beam ошибается в применимости; нам не нужны |
| 24 | «Portal-based 2–4 недели, высокий приоритет; 100 ячеек/200 порталов» (README §3, 05 §3) | У нас **6–10 недель** + rebake (04 §3.5, F); beam сам в 05 ставит 3–5 недель. Масштаб: 2 363 двери + 10 355 боксов; экстерьер порталы не решают (beam §3.6); нужен пере-мерж до дома/блока (единица 130 м); интерьеры — малая доля (`_INT_` 306 live MR). | beam ошибается (срок/приоритет); нужен эксперимент E7 |
| 25 | «Статик запрещён FO, но portal/PVS совместим (локальные координаты)» (04 §2.2, 05 §3.4) | По сути верно: portal — authoring-геометрия, static-флаги не нужны, локальные координаты переживают ребейз (F §2.4). Но PVS с запечёнными наборами — только при локальных данных + rebake; Umbra — нет; beam противоречит себе (03 §3.1/§4: «baked occlusion для чанков»). | beam прав в принципе, но неполон/противоречив; мы точнее |
| 26 | «Camera-Relative Culling (Lights+Shadows) включить в C0 для FO» (02 §1.3, 04 §2.2) | Уже включено (`GraphicsSettings:79–80` = 1/1); URP/Core не содержат обращений (grep 0) → вероятный no-op `[LIKELY]`; beam смешивает camera-relative rendering (HDRP) и culling-флаги. | beam вводит в заблуждение; у нас позиции не было (§7.2) |
| 27 | «Mesh LOD вместо LODGroup: включить для всех mesh» (02 §1.6, 04 §0.5) | Manual 6000.5: генерация при импорте, ≥256 тр.; не работает со static batching/GPU instancing (LOD0); cross-fade требует GRD; плохо на disconnected pieces — а батчи это и есть. Городские меши — результат merge/rebase `[LIKELY]`; двери/пропсы часто <256 тр. `[UNVERIFIED]`. | beam неполон/опасен; правильно — HLOD/LOD по блокам (Э6/Ф2) |
| 28 | «GPU occlusion в indoor даст ~5 %; не «настоящее» occlusion» (02 §1.8) | Не проверено; для открытого города эффект неизвестен — измерить в GRD-пилоте. | оба неполны; нужен эксперимент |
| 29 | «Своя Hi-Z 4–8 недель (опционально)» (05 §7) | Согласуется: свой BRG/Hi-Z 6–10 недель, «80 % уже в engine», не первый шаг (04 §3.4, 05 §1). | сходится |
| 30 | «Купить Amplify + NativeTrees; Perfect/Frustum/Bundle не покупать» (README §3) | Цена/применимость покупок частично неверны (#20, #23); по существу «покупка нашу боль не решает» — наш вывод (07 §1). | сходится по выводу |
| 31 | «GRD требует Keep All, Forward+, SRP Batcher; LOD Group cross-fade несовместим» (02 §1.7) | Совпадает с исходниками (G #26): Forward+ есть, KeepAll нужен, SRP Batcher on. | сходится |
| 32 | «Occlusion culling baked (cell-based) — для городских чанков» (03 §3.1, §4) | Противоречит FO и собственному выводу «Umbra не подходит»: бейк world-space/статик, город ездит. | beam ошибается (внутреннее противоречие) |
| 33 | «HLOD/импосторы: L1 3–12 км, L2 12–80 км за 1–2 вызова; bake в Editor» (04 §0.5) | Согласуется с нашим Э6/L0/L1/L2 (ROADMAP §7.3, 05 §3.2); детали требуют пилота. | сходится по направлению |
| 34 | «Свой BRG/custom culling = контроль; можно написать лучше ассетов» (05 §7) | Наш F: свой BRG — 6–10 недель дубля engine; «лучше» = тонкий Visibility/Budget Director (05 §1/§3). | beam неполон; наш вывод полнее |

## 4. Полезные идеи beam, которых у нас нет

1. **Precomputed Culling** ($150, Unity 6000.0.77, URP) — единственный ассет с явным Unity 6; в нашем B-аудите
   (27 строк) отсутствует; возможный «измеритель потолка» портал/ячейка-окклюзии — добавить в B, проверить
   механизм/FO.
2. **MIT-импосторы** (Abdns/ImpostorSystem_Unity URP 17.3+, Rawteous) — альтернатива Amplify для L2. `[LIKELY]`.
3. **«Чёрный список» ассетов** (Frustum Culling, Ultimate Bundle, Per-Layer Camera Culling, HLOD System) — у нас
   есть вердикты по 27, но нет сжатого списка «не тратить время».4. **Interest management (NGO `CheckObjectVisibility`/`NetworkShow/Hide`)** — будущий MMO-слой, ортогональный
   culling; для host-only не нужен, зафиксировать в roadmap.

## 5. Опасные для решения тезисы beam

1. **Отмена склейки** (#9–10): 20 694 слота, 25–40 тыс. DC, редактор снова на 16.5k MR — худший вариант.
2. **«≤120 DC» как цель** (#1): провоцирует PVS/порталы «до бюджета», которого L0 из 761 батча не даст.
3. **«Блокер = ProceduralSteel, 0.5–1 день»** (#17–18): недооценка ~20 SG → риск провала спринта.
4. **«Portal 2–4 недели»** (#24): недооценка ×2–3 + зависимость от пере-мержа + rebake.
5. **«Mesh LOD включить всем»** (#27): не работает для слитых батчей, риск артефактов.
6. **«Camera-Relative Culling для FO»** (#26): уже включено, вероятно no-op в URP — иллюзия рычага.
7. **«PhysX 65k hard cap»** (#14): цифра из Unity 4.x; может подтолкнуть к ненужной DOTS-миграции.
8. **«Корабли не склеивать, куллить»** (#12): 973 рендерера в порту culling не спасёт — нужен мерж по
   модулям/палубам/материалам + LOD/импостор; обратная ошибка (наша «уже склеены») скрыла второй источник DC.
9. **«Baked occlusion для чанков»** (#32): прямое нарушение FO-правил.

## 6. Что beam подтверждает в нашем плане

Двери — главный источник DC (84 %); Umbra/статический бейк — не путь для FO; GPU RD + GPU occlusion — главный
доступный путь; DOTS/ECS-миграция не нужна; нужен runtime culling + LOD/HLOD/импосторы, а не один ассет;
static batching/occlusion несовместимы с FO; склейка дала 19 944 → 1 207; покупать culling-ассеты не нужно;
потолок окклюзии надо мерить; корабли — отдельный класс «куча объектов» (beam прав в постановке, ошибся в
решении).

## 7. Что осталось непроверенным + поправки к нашим документам

### 7.1. Эксперименты (E-A…E-I)

- **E-A:** объём DOTS-настройки ~20 Shader Graph + GRD (Graph Settings 20 графов; ветка `BrgStripping=2`, GRD,
  Frame Debugger `Hybrid Batch Group`).
- **E-B:** portal/PVS на 2 363 двери/10 355 боксов (E7 pre-check 2–3 дня + пилот 2–3 недели).
- **E-C:** разложение DC по проходам, факт prepass (Frame Debugger).
- **E-D:** GRD и 16 704 уникальных меша (доки BRG + пилот на 2 материалах).
- **E-E:** Mesh LOD на слитых батчах (тест на одном чанке в копии проекта).
- **E-F:** camera-relative culling в URP (A/B с флагом и без).
- **E-G:** Precomputed Culling: механизм/FO (доки/триал, не покупка).
- **E-H:** PhysX-лимит в Unity 6 (профилировщик 30–60k коллайдеров или офиц. доки).
- **E-I:** гранулярность мержа кораблей (пилот `Ship_havyII_base`: палубы/модули/материалы, счёт слотов/DC).

### 7.2. Поправки к нашим документам

1. **`03_MERGE_VS_INDEPENDENT.md` §2.1**, строка «independent + GPU RD ≈ 200–350 indirect»: модель для **слитых**
   мешей; 16 468 уникальных мешей GRD не инстансит — иначе противоречие с «не расклеивать».
2. **ОШИБКА в README §0.3 / 02 / брифе:** «корабли уже склеены оптимально (2 MR)» — неверно (YAML-парс не видел
   nested prefab; live: 973/428/118 MR; второй источник DC); `C_ships.csv` не использовать.
3. **README §0.4 / 04 §3.6:** «static-флаги запрещены» → «запрещены static-рендереры/batching/occlusion на
   ездящем; 2 076 static GO (коллайдеры/холдеры) есть».
4. **Ассеты:** добавить Precomputed Culling и MIT-импосторы; дата Amplify — 28.10.2026 (сейчас 29.10).
5. **Camera-relative culling:** оба флага уже 1, URP-пакет их не использует — не считать рычагом.
6. **Mesh LOD:** отдельная опция «только импортируемые меши, не батчи» с оговорками Manual 6000.5.
7. **Дверные коллайдеры:** в доках 4 432 (ROADMAP 23.10), `C_doors_model.csv` — ≈8 510; пометить `[LIKELY]`.
8. **147 715 / 167 969:** помечать как doc-sourced историю, не замер.

## Handoff

**Главный вывод:** beam полезен как «второе мнение» по стратегии, но его центральный тезис — «город не надо
было склеивать» — противоречит измеренным слотам/DC и отклоняется. Наш план (сохранить мерж → low-code двери →
GRD → дробление единицы мержа → опционально PVS/HLOD) устойчив. **Поправка Lead:** корабли не «уже склеены» —
973 MR тяжёлого (light 428, medium 118, cargo 80, NPC ×6) — второй источник DC; вопрос мержа приоритетен (I),
beam-тезис «куллить» отклонён.

**Четыре факта:** (1) 1 806 слотов/1 207 MR против 20 694/16 468 (×11.5) — мерж нужен; (2) блокер GPU RD — не
ProceduralSteel, а ~20 SG (15+5) с 0 `DOTS_INSTANCING_ON`; (3) beam ошибается в трёх настройках:
`m_StaticBatching: 1`, Forward+ (`m_RenderingMode: 2`), `m_BrgStripping: 0 = KeepIfEntitiesGraphics`;
(4) корабли — 973 MR (не 2): второй источник DC.

**Не проверено:** E-A…E-I (§7.1) — DOTS-аудит 20 графов, portal/PVS на реальном масштабе, эффект GPU occlusion,
гранулярность мержа кораблей.
**Риски:** beam-тезисы в решении без проверки («≤120», «portal 2–4 недели», «корабли куллить»); GRD-прогноз —
гипотеза; prefab-источник мержа не в git; YAML-счётчики nested prefab (`C_ships.csv`) — только live MCP.

## Источники

**Наши:** `10_CULLING_RESEARCH/README.md`, `02`–`08`, `G_VERIFICATION.md`, `08_DATA/C_*.csv` (C_shaders,
C_city_materials, C_doors_model, C_city_mesh_refs_summary, C_combined_assets, C_rebased_meshes),
`C_tools/fbx_parse.py`; `ROADMAP.md` (§1:16, §2.1:70–76, :289, :372). **⚠️ `C_ships.csv` невалиден;** ship-факты —
live MCP Lead 30.10 (973/428/118 MR, 250 Collider, 1452 Transform, cargo 80, NPC ×6).
**Beam:** `beam_research/README.md` + `01`–`07`.
**Проект:** `ProjectSettings/ProjectSettings.asset:494–495`, `GraphicsSettings.asset:58,79–80`;
`ProjectC_URP.asset:58–88`, `ProjectC_URP_Renderer.asset:189`; `WorldScene_0_0.unity` (regex `m_SourcePrefab`,
23 ship instances); `Prefabs/Ships/*.prefab`; `Prefabs/City/Doors/*.prefab`;
`Editor/CombineMeshesToCollider.cs:15`; `Art/Models/Ships/HeavyII_base/*.fbx` (936 Model/53 603 тр.).
**Пакеты:** `Library/PackageCache/{universal@748d7458cc62, core@9fe9377312a8, shadergraph@2df19e8719bc}`;
UnityCsReference `Runtime/Export/Graphics/GraphicsSettings.bindings.cs`; Unity Manual 6000.5 Mesh LOD:
`https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/lod/mesh-lod` (30.10.2026).

**Веб (30.10.2026):** https://assetstore.unity.com/packages/tools/utilities/amplify-impostors-119877 ($60, v1.1.0,
Oct 28 2026, 2022.3.22); https://assetstore.unity.com/packages/tools/utilities/perfect-culling-occlusion-culling-system-193611
($97.46, v1.3.0.4, Feb 11 2026, 2019.4.41); https://assetstore.unity.com/packages/tools/utilities/precomputed-culling-221372
($150, v1.3.0, Jun 21 2026, 6000.0.77; механизм `[UNVERIFIED]`); https://github.com/bartofzo/NativeTrees (MIT,
push 2024-04, structs-only, no remove, frustum — future todo); https://github.com/Sylmerria/Spatial-Hashing (MIT,
push 2023-06, ECS/DOTS); 65 536 — `https://docs.unity3d.com/400/Documentation/Components/terrain-Trees.html` +
PhysX `PxSceneLimits` («hint»); GTA V 4 155 / DOOM 1 331 — из G (adriancourreges, 30.10).
