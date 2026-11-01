# Подотчёт E — Синтез prior research репозитория

**Сабагент:** explore · **Дата:** 2026-11-01 · **Задача:** синтез существующих исследований репозитория, релевантных свету.

## 1. Масштаб и бюджеты

- Источников `Light`: **2 087** (`08_LIGHTING/01_LIGHTING.md` §4) — 1 557 по узлам `LAMPS` + 530 по `ADMIN_FOR_COLLIDER`; в culling-research (`10_CULLING_RESEARCH/README.md` §4) фигурирует **2 238** (Stage 6); комментарий пула говорит «~1 900». Расхождение не разрешено в доках.
- 7 префабов `PM_LIGHT_*` (Point, range 3.5–14 м, intensity 1.0–3.0, цвет тёплый), `LightShadows.None`, `LightmapBakeType.Realtime`, слой `PRIMUM` (8), `Light.enabled = false` в префабе.
- Emissive: `PM_LAMP_GLOW_WARM` (URP Unlit HDR, `_BaseColor = (2.40,1.68,0.86)`), назначен **1 293** рендерерам `M_EMIT_LAMP`/`M_TQ_LAMP` вне ADMIN/дворцов.
- `PrimumLightPool`: maxActive 96, activeRadius 140 м, checkInterval 0.25 с, hysteresis 0.1. Комментарий пула: «Forward-путь не зависит от 1 900 ламп — ограничен бюджет включённых, а не наличие компонентов».
- URP: Additional Lights = Per Pixel, per-object limit 4, additional shadows off; main light 2048 px / 2 cascades, shadow distance 1000 (но `QualitySettings.shadowDistance` в URP — no-op).
- `AdditionalLightIntensityController`: группы по имени узла — `PRIMUM_OUTLIGHT` (753, улица), `PRIMUM_INLIGHT` (1 334, интерьеры + ADMIN), `CityLamps` (`MD2_Lamp_`), catch-all `AllAdditionalLights`. Кривые 0–5 ч → 1.0, 6–17 ч → 0, 19 ч → 0.25, 21–24 ч → 1.0; с 2026-10-21 добавлен `Intensity Multiplier`.

## 2. Принятые решения

- Префаб несёт **только `Light`**, ставится в центр светящейся части (`GLOW→BULB→LAMP→…`), **0 новых рендереров** (`08_LIGHTING` §2.1).
- Emissive через URP **Unlit** с HDR (не URP/Lit `_EMISSION`) — персистентно после реимпорта, без keywords (`08_LIGHTING` §2.3).
- Деление по близости (имя = образец, физические приборы — по пересечению габаритов) дало **+104** источника; «часть важнее ключа».
- Контракт размещения: `WorldRoot_0_0/PRIMUM_LIGHTS` — сосед корня города, **не** ребёнок `PRIMUM` (иначе теряется при пересохранении префаба); identity, слой PRIMUM (8), static-флаги = 0 (FO).
- ADMIN: 530 источников по `ADMIN_FOR_COLLIDER` (Y 2 502.4…2 521.4, радиусы 3.5…8 м).
- День/ночь: две группы `PRIMUM_OUTLIGHT`/`PRIMUM_INLIGHT` в контроллере; emissive `PM_LAMP_GLOW_WARM` **не слушает** день/ночь (открытый хвост §6.5).
- Легаси `MD2_Lamp_*` (11 шт, intensity 50, range 30) в порту не тронуты — снимаются в Э8.

## 3. Упоминания в culling-research

- Матрица контента (`10_CULLING_RESEARCH/05_RECOMMENDED_ARCHITECTURE.md` §3.2, строка «Лампы/emissive»): «пул света (готово) + distance корпусов; свет 140 м; корпуса с батчем; гранулярность группа/чанк».
- D-отчёт (`D_FO_WORLD_STREAMING.md` §2.10, §3): `PrimumLightPool` крутит `Light.enabled` (2 087, ≤96, 140 м); запрещено путать бюджет света с рендером; лампы в матрице — «свет — пул (готово); корпуса — D с батчем».
- J-отчёт: 151 `Light` в фермах (не в пуле PRIMUM, ≤96).
- T-CULL01 Stage 6 (`T-CULL01_STAGE6_REPORT.md` §1): `Light.enabled` — владелец `PrimumLightPool`; директор **не пишет** его; группа `Lamps` только наблюдается (счётчики). `PRIMUM_LIGHTS` = 2 238 источников.
- FO-правило: позиции читаются живьём (`transform.position`), мировой `Vector3` между кадрами не хранится → хук ребейза не нужен (`PrimumLightPool.cs:6-8`, `89-157`).

## 4. Переиспользуемые концепции для light culling

- **VisibilityDirector группы/ячейки** (Stage 6): мир → чанк → дом; отдельные классы `Doors`, `Ruins`, `Farm`, `Satellite`, `Ship`, `Interior`, `Lamps`, `Misc`. Каждая группа — владелец видимости/теней. Директор применяет `Renderer.forceRenderingOff` (единственный в проекте), тик **3 Гц**, порог **< 2 px** во всех камерах union, возврат **2.24 px**, гистерезис **12 %**.
- **Мультикамерный union**: Game-камеры, активные, без RT, far ≥ 50, `cullingMask & слой мира (10)`, и камера дотягивается до якоря хотя бы одного чанка (`ReachesWorld`). Публичный API: `WorldCameras`, `TryGetNearestCameraDistance`. `AtmosphereCamera` и парковочная `MainCamera` исключены.
- **FO-safe смещения**: участник хранит только смещение центра bounds относительно живого `WorldRoot_0_0` + габарит; мировая точка = `WorldRoot.position + offset` каждый тик. Сброс кэша: смена `frameGeneration` (success) и скачок `WorldRoot` без смены `frameGeneration` (rollback/внешний перенос); скачок камеры > 50 м — немедленный пересчёт.
- **HLOD-уровни**: L0 полный 0–3 км; L1 HLOD 3–12 км; L2 импостор 12–80 км (1–2 вызова на город). Квад ночных огней для L2 берётся из `placement_manifest.csv` (готовый список точек) (`04_synthesis_and_plan.md` §C2, `08_LIGHTING` §8).
- **Floating Origin**: первый авто-ребейз ≈ 56 км, дальше 0.26–1.5 км каждые ~35 с; публичного хука нет — поллинг `CumulativeRebaseOffset`/`FrameGeneration` или фаза `TryRebuild` (no-op). Static-флаги запрещены.
- **Budgets**: culling ≤ 2 мс (среднее 1.33–1.69 мс); директор 0.02 мс/кадр, тик 0.18 мс средн; L0 1.5–3k DC, L1 ≤ 500, L2 ≤ 10.
- **Shadow-budgets**: `CityShadowBudget` (200 м), `DoorVisibilityBudget` (слой 10), `ShipShadowBudget` (LODGroup `Ship_havyII_base`), `ShadowBudget` (фермы 60/50 м) — концепт дистанционного гашения теней переиспользуем для света (но additional shadows у ламп выключены by design).
- **`DetailDistanceCuller`** (RuinsValleys, `Renderer.enabled`, `Camera.main`-мина уже починена через паттерн пула) — образец frame-local culling.
- **LayerCullDistances** (слой 10, двери 120–150 м) — дешёвый рычаг; для света нет отдельного слоя, но группировка по `PRIMUM_OUTLIGHT`/`PRIMUM_INLIGHT` даёт аналогичный эффект через пул.

## 5. Пробелы (что НЕ покрыто prior research)

- **Fade/тайминг**: пул мгновенно toggles `Light.enabled` (on/off) — нет плавного fade intensity по дистанции/времени; нет анимации включения/выключения. `checkInterval = 0.25 с` фиксирован, нет адаптивного интервала.
- **Конус/направленность (cone/spot)**: все источники — **Point**; Spot/Area не используются. Нет cone-frustum culling для направленных светов; нет «веера» (fan) распределения.
- **День/ночь emissive**: `PM_LAMP_GLOW_WARM` горит всегда (HDR-накал), днём выглядит как яркое стекло без света вокруг; контроллера `_BaseColor` по времени суток нет (`08_LIGHTING` §6.5).
- **Occlusion для света**: пул куллит по дистанции, не по видимости — стены/здания не гасят свет внутри помещений; portal-based light culling не реализован.
- **LOD источников**: нет дистанционного масштабирования range/intensity (только on/off по 140 м).
- **Интеграция с VisibilityDirector**: группа `Lamps` только наблюдается; `forceRenderingOff` не применяется к свету (`Light.enabled ≠ Renderer.enabled`). Корпуса ламп в батчах города куллятся директором как дома, но свет — отдельно, через пул.
- **Мультикамера для пула**: пул выбирает **одну** камеру (ближайшую к светильникам), а не union; при нескольких игровых камер не учитывает все.
- **Телеметрия/бюджет света в директоре**: только `ActiveCount`; нет отчёта по группам/камерам для света.
- **Расхождение counts** 1 900 / 2 087 / 2 238 не разрешено.
- **Shadow для дополнительных источников**: additional shadows off by design — не покрыто как опция (осознанно).
- **Тайминг первого кадра**: `Rebuild()` на `OnEnable` выключает все 2 087 источников, первый чек через `checkInterval` — возможна задержка первого включения.

## 6. Рекомендуемые точки интеграции

1. **`PrimumLightPool`**: fade (lerp intensity 0↔базовая); использовать `VisibilityDirector.WorldCameras` union вместо `FindFocusCamera()`; адаптивный `checkInterval`; FO-safe сохранить.
2. **`AdditionalLightIntensityController`**: day/night-контроллер для emissive `PM_LAMP_GLOW_WARM._BaseColor`.
3. **`VisibilityDirector`**: политика для группы `Lamps` — не перехватывать `Light.enabled` (владелец — пул), но применять distance-cap/forceRenderingOff к корпусам через под-пиксельный механизм; арбитраж через провайдер.
4. **HLOD L2**: квад ночных огней из `placement_manifest.csv` (готовый список точек); emissive окна в HLOD-меше (L1 3–12 км).
5. **Portal-based culling** (Stage 7): при невидимости ячейки (интерьер) гасить `Light.enabled` + emissive корпуса; двери = порталы.
6. **`CityShadowBudget`/`DoorVisibilityBudget`**: тени ламп не используются (additional shadows off) — не трогать.
7. **FO-регресс** (F8/F9): пул уже frame-local; при ребейзе `Light.enabled` не меняется. Проверить, что fade/новые компоненты тоже frame-local.
8. **Телеметрия**: добавить в `CullingDiagnostics` счётчики света (активных/зарегистрированных по группам `PRIMUM_OUTLIGHT`/`PRIMUM_INLIGHT`).

*Источник: подотчёт сабагента E.*