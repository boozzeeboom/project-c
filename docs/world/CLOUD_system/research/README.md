# Ресерч: сравнение облаков ProjectC 3.0 и `Fishypants/cloudscape`

Дата: 2026-11-19. Исходный режим: анализ. После сверки с актуальным `index.html` и лицензией MIT начата реализация: **T-CLOUD43 / шаг 1 готов, визуальная приёмка NOT RUN**. Результат первоначального разбора:
- разбор внешнего репозитория — `EXTERNAL_cloudscape_analysis.md` (исходник: `github.com/Fishypants/cloudscape`, один `index.html` ~107 КБ, WebGL2/GLSL ES 3.00, MIT);
- разбор нашего текущего состояния — `LOCAL_3.0_analysis.md` (доки `3.0/*`, код в `Assets/_Project`, ассеты URP).

Ниже — синтез, выводы и приоритеты. Все числа — из прочитанного кода/доков; где данных нет, сказано прямо.

---

## 1. Что это за два объекта сравнения

| | **cloudscape (Fishypants)** | **ProjectC CLOUD_system 3.0** |
|---|---|---|
| Суть | Один HTML-файл, WebGL2 без движка, «volumetric sky you can fly through» | Кастомный raymarcher, инжектированный в URP 17.5 через RenderGraph `ScriptableRendererFeature` |
| Масштаб | **1 юнит = 1 км**, изогнутая планета `Rg=6360`, `Rt=6460`, радиус ландшафта 120 км | **1 юнит ≈ 1 м**, мир 80 000×80 000, **floating origin** сдвигает корни сцены |
| Рендер | ~14 проходов, кастомный MRT×4 (half-res volumetrics) + temporal + bilateral upsample + свой HDR/post | 1 fullscreen-пасс (`BeforeRenderingTransparents`), прямой бленд в `colorTarget`, 6 light steps, 4 слоя |
| Шумы | GPU-бейк на старте: shape 128³, detail 64³, foliage 64³, weather 512², curl 128² RGBA16F, mips | Запечённый `CloudNoise3D.asset` 128³ RGBA8 (compute `BakeCloudNoise`), coverage — процедурная 2D FBM; шторм — процедурный `abs(Perlin)` FBM |
| Атмосфера | Hillaire 2020: LUT transmittance 256×64 + multiple scattering 32×32 + irradiance 2×1 | `pow(transmittance, 0.5)`, хардкод-рампы день/закат (`GhibliRamp`), `rampBlend = saturate(sunDir.y*2)` |
| Качество | 4 профиля (Low..Ultra), dynamic resolution по GPU-таймеру, разрешение vol 0.35–0.75 | Флаги HalfRes/BlueNoise/Temporal **есть в ассете, но в коде заглушки**; HDR выключен в URP |
| Уникальное | Тени облаков на мир + sun shafts от облаков **и** гор; 3 каскада теней; деревья целиком в вершинном шейдере; ACES + линзы |

**Главное расхождение с документацией:** STATUS.md/`IMPLEMENTATION_LOG.md` заявляют half-res, blue-noise и temporal как сделанные, но реальный код рендерит Pass 1 в full-res напрямую, а Pass 2 (`Composite`) и Pass 3 (`History`) — `return half4(0,0,0,0)`. `_TemporalBlend`, `_BlueNoiseTex`, `_prevViewProj` выставляются/аллоцируются, но не используются. Причина (IMPLEMENTATION_LOG, Приложение B.3): при отладке «почему не видно облака» конвейер history убрали и не вернули. Любое сравнение должно исходить из **фактического** full-res кода, а не из STATUS.

---

## 2.Что реально можно заимствовать (приоритеты)

### P0 — «бесплатные» улучшения качества, почти не требующие архитектуры

1. **Трёхчлен Beer–Lambert вместо `pow(T,0.5)`.** В cloudscape: `ph0·e^-od + 0.5·ph1·e^-od/2 + 0.25·ph2·e^-od/4` — мультискаттер почти бесплатно, даёт светлые края без выгорания. У нас есть `MultiScatterApprox`, но.indexOf он простой. **Совместимо: да**, меняется 1–2 строки в `VolumetricClouds.shader`.
2. **Две HG-фазы с миксом** (`mix(HG(-0.25k), HG(0.82k), 0.72)`) вместо одной `HG(cosTheta,0.7)`. `HG_DualLobe` уже написан в `CloudCommon.hlsl`, но не применяется — достаточно включить. **Совместимо: да.**
3. **Powder effect** `mix(1, 1-exp(-sE*0.15), …)` и **crease shading** `depthP*vertP*1.6` — тёмные складки и яркие/sunlit рёбра. Заменяет/дополняет наш `SilverLining`. **Совместимо: да.**
4. **«Куполообразование» coverage** `cov *= mix(1, 0.5, hn²)` и **переворот шума по высоте** `mix(n, 1-n, saturate(h*8))` (wispy низ / billowy верх). Даёт stratus→cumulus силуэты из одного шума. **Совместимо: да** — у нас `_LayerNoiseMask=0`, все 4 слоя на одном шуме, это именно то, чего не хватает.
5. **MIP по угловому footprint** (`lod = clamp(log2(t)+log2(pixAng*128/scale)-sharp, 0,5)`) — убирает шиммер далёких облаков. У нас 3D-текстура без mip-цепочки; нужно проверить генерацию mips на D3D11 (`Graphics.GenerateMips` для Texture3D) — **совместимо с оговоркой**.
6. **Empty-skip** `t += dt*2` и адаптивные шаги `mix(0.4N, N, seg/12)` + early-out `T<0.01` с ренормализацией `col /= max(1-T,1e-4)`. У нас только early-out по `a>=0.99`. **Совместимо: да**, экономит заметную часть 37 шагов.

> Суммарно P0 даёт «кинематографичный» прирост при минимальном риске и без включения недоработанных pass'ов.

### P1 — средняя сложность, большой выигрыш в качестве/стабильности

7. **Включить half-res + temporal + blue-noise, которые уже заложены.** В cloudscape: allocate-once RTHandle на максимальный scale, меняется только viewport; репроекция через `prevVP` **без motion-vector текстуры**; depth-aware 3×3 + Catmull-Rom при однородном footprint + neighborhood clamp; `blendA = mix(0.5, 0.12, conf)` (~8 кадров); bilateral upsample `exp(-|log dLow - log dFull|*6)`. У нас инфраструктура частично есть (`_CloudHistoryA/B`, `_prevViewProj`, флаги, `VolumetricCloudsPass` Pass 2/3), нужно довести. **Совместимо: да, но осторожно** — RenderGraph 17 требует ping-pong (нельзя read+write одной текстуры в пассе), а `prevVP` надо синхронизировать с jitter'ом камеры. safeguards: `resetHistory` на ребейз/телепорт камеры.
8. **Cloud shadow map отдельным проходом в sun-space** (512–1024², 20 шагов по `cloudBase(lod=2)`, запись `exp(-od*45*density*0.35)`, снэп текселей) + приминение на геометрию/атмосферу. У нас тени облаков **на мир отсутствуют** — только self-shadow 6 шагами. **Совместимо с адаптацией**: в URP это ещё один `ScriptableRenderPass` + генерация/sun-space матрица; нужно учесть FO (тени считаются в мировых координатах → нужна привязка к ребейзу, как у `StormCellDirector`).
9. **Sun shafts как переиспользование cloud-shadow** (в cloudscape отдельного прохода нет: `vis = mix(1, cloudShadowAt*terrainShadowAt, uShafts)` в in-scattering атмосферы). **Совместимо: да**, если появится cloud shadow (п.8). Дешевле, чем классический radial blur / god rays.
10. **Атмосферные LUT (Hillaire 2020)**: transmittance 256×64 (60 шагов), multi-scatter 32×32 (8×8 направлений × 20 шагов), irradiance 2×1 (48 направлений, счёт каждый кадр); пересчёт только при смене haze. Даёт физические закаты/ночь/physical haze и единую модель неба вместо хардкод-рамп. **Совместимо с адаптацией**: compute-бейк LUT + сэмплинг в cloud/atmosphere шейдере; надо решить, кто рисует небо (сейчас skybox URP, в cloudscape — композит).
11. **Dynamic resolution только для volume-таргетов** по GPU-таймеру (пороги >14 мс −0.05, <11.5 мс +0.025, коррекция раз в 700 мс, hold 4 с). У нас `FrameTimingManager` в `CloudPerfMonitor` бесполезен без Dynamic Resolution в URP-ассете. **Совместимо с адаптацией**: кастомный `ScalableBufferManager`/свой ScaleFactor + `resetHistory`.

### P2 — большой объём / высокий риск, только точечно

12. **Baked weather map (512², coverage+type)** вместо процедурной 2D FBM — `cloudscape` берёт `thr=1-coverage*1.3` и type из карты. У нас coverage — FBM с `_CoverageScale=0.0001`; запечённая карта дала бы управляемые «ячейки погоды» и основу для будущих server-driven disturbance. **Совместимо: да** (compute + 2D текстура), но тянет за собой дизайн-решения по GDD-02.
13. **Front/back atmospheric split по cloud depth** с}R32F-дистанцией и солнцем позади облака (`col += sunAdd*v0.a`). У нас depth только для обрезки реймарча. **Совместимо с адаптацией** — потребует кастомный depth в MRT или аккуратную работу с `_CameraDepthTexture`.
14. **3 каскада теней + terrain-only копии + деревья в вершинном шейдере** — касается ландшафта, а не облаков; для ProjectC это **не приоритет** (своя система мира). Фишка «один instanced draw» ценна как идея, но к нашему ландшафту отношение косвенное.

---

## 3. Что НЕ совместимо / не стоит переносить

| Вещь в cloudscape | Почему не переносится |
|---|---|
| **Километровая шкала + кривизна планеты** `Rg=6360`, радиус 120 км | Прямой конфликт с floating origin и метровой шкалой сцен (80 000 юнитов). Нужна **отдельная система координат с центром на камере** или `WORLD_SCALE`; проще — оставить наш плоский slab 800–7000 и не трогать масштаб. |
| **Кастомный MRT×4 + R32F depth + D24 shadow array** | RenderGraph 17.5 в URP накладывает свои ограничения (нельзя read+write, MRT-бленд ненадёжен — уже зафиксировано в наших A.4). Часть можно адаптировать, но «как есть» — нет. |
| WebGL2/GLSL-специфика (`#version 300 es`, `texStorage3D`, `blitFramebuffer`, `drawBuffers`, IGN fullscreen triangle, `EXT_disjoint_timer_query_webgl2`) | Это HLSL/DX11 SM5.0 у нас. Переносится только логика, не синтаксис. |
| JS UI (SCHEMA, слайдеры), single-file, free-fly, 25× time-lapse | Не имеет отношения к нашему геймплею/UI (New Input System, своя панель, MMO-контекст). |
| Hardware PCF из `sampler2DArrayShadow`, 5 срезов каскадов | У нас свои тени URP; ради облаков переделывать систему теней нецелесообразно. |
| Авто-экспозиция через ACES с INPUT/OUTPUT матрицами + свой final-lens pass | У нас HDR **выключен** (`m_SupportsHDR: 0`); чтобы получить выгоду, сначала надо включать HDR и выстроить порядок постобработки — это отдельное решение, не «копипаста». |

---

## 4. Ключевые риски именно для ProjectC

1. **Floating origin.** Всё новое с мировыми координатами обязано иметь `ApplyRebaseTranslation(t)`. Для cloud shadow/sun-space и weather map это означает: пересчёт/сдвиг при каждом ребейзе (аналог `ShiftStormCells()` в `GlobalMotionControlledRebaseSlice.cs`). `LocalDensityBuffer` явного хука не имеет — держится на `FollowTarget`, сплаты живут 1 кадр; при добавлении долгоживущих данных нужен явный контракт.
2. **RenderGraph vs легаси.** Новый код — только `RecordRenderGraph`/`AddRasterRenderPass`. `VeilRaymarchBlit` (`OnRenderImage`) — легаси, не образец для подражания.
3. **Рассинхрон доков и кода.** STATUS.md v3.0 и IMPLEMENTATION_LOG describing фичи как сделанные, а они заглушки; любая оценка «что у нас уже есть» должна сверяться с кодом. Аналогично VFX-молнии/контраилы уже в коде, а докалы пишут «v3.5».
4. **Перформанс не измерен.** `PERF_RESULTS.md` отсутствует; `CloudPerfMonitor` меряет кадр целиком, а не cloud-pass. При full-res и ~37×7 вызовов `CloudDensity` бюджет ≤3–5 мс, скорее всего, превышен. Поэтому **включение temporal/half-res (P1.7) должно идти первым** — оно компенсирует стоимость новых фич P0.
5. **Отсутствующие ассеты.** `BlueNoiseTex` (ссылка есть, файла/`.meta` нет) и `Cloud_Noise1.png` для контраилов не найдены — перед включением дизеринга/доводкой VFX нужно их создать/привести.

---

## 5. Рекомендованный порядок (если когда-то применим)

1. **Восстановить честный пайплайн**: Pass 1 → half-res RT → Pass 3 History (temporal с `prevVP` + clamp) → Pass 2 Composite (bilateral upsample) в `colorTarget`. Снять флаг «сделано» в STATUS.md или реально включить. Без этого шага любое качество P0 съест FPS.
2. **P0-фишки в шейдере** (3-член Beer, 2 HG, powder, crease, купола, MIP, empty-skip) — компактный дифф, сразу видный результат.
3. **Cloud shadow + sun shafts** (P1.8–9) — самый большой визуальный выигрыш для MMO-неба: тени облаков на ландшафт и просветы.
4. **Атмосферные LUT** (P1.10) — только вместе с решением про HDR и роль неба.
5. **Weather map и seeded cells** (P2.12) — уже как часть Фазы 3/GDD-02, не изолированно.

---

## 6. Короткий ответ на вопрос «что совместимо, что нет»

- **Совместимо почти напрямую (HLSL-логика):** мультискаттер, двойная фаза, powder/crease, куполообразование coverage, MIP-by-footprint, empty-skip/адаптивные шаги, temporal-репроекция через `prevVP`, bilateral upsample, cloud shadow как отдельный пасс, LUT-бейк в compute.
- **Совместимо с адаптацией под URP 17.5 RenderGraph:** half-res инъекция (allocate-once + viewport), dynamic resolution, MRT/глубина, каскадные тени, порядок постобработки + включение HDR.
- **Не совместимо:** километровая шкала и кривизна планеты (конфликт с floating origin), WebGL2/GLSL-синтаксис, JS UI, single-file/free-fly, D24 hardware PCF «как есть», кастомный final-lens до включения HDR.

Итог: **external cloudscape — это не «готовый движок облаков, который можно взять», а кладезь конкретных вычислительных трюков.** Наша архитектурная база (RenderGraph, slab, 3D-шум, 4 слоя, compute-плотность, FO-контракты) уже правильная; не хватает именно «последней мили» качества (та же самая temporal/half-res, которая у нас заглушена) и отсутствующих cloud shadows/sun shafts. Переносить стоит алгоритмы из §2–4 внешнего отчёта, а не его.scaled мир и пайплайн целиком.
