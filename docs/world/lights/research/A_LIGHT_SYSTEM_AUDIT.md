# Подотчёт A — Аудит текущей системы света (PRIMUM)

**Сабагент:** explore · **Дата:** 2026-11-01 · **Задача:** факты по текущей системе света.

## 1. Источники (PM_LIGHT_*)

- 7 префабов: `PM_LIGHT_STREET_POST`, `_STREET_WALL`, `_FACADE_WALL`, `_FACADE_SCONCE`, `_INTERIOR_PENDANT`, `_INTERIOR_SCONCE`, `_INTERIOR_TABLE` (`PrimumLampLighting.cs:107-128`).
- 2 087 инстансов в `WorldScene_0_0.unity`: `PRIMUM_OUTLIGHT_*` = 753, `PRIMUM_INLIGHT_*` = 1 334 (из них 530 — ADMIN по `ADMIN_FOR_COLLIDER`).
- По семействам (placed): StreetPost 83, StreetWall 302, FacadeWall 368, InteriorPendant 323, InteriorSconce 518, InteriorTable 493, FacadeSconce 0. Сумма 2 087.
- Наружные (`IsOutdoor`) → `PRIMUM_OUTLIGHT` (753); интерьерные → `PRIMUM_INLIGHT` (1 334).
- Префаб несёт только `Light` (Point, shadows None, realtime bake, `enabled=false` на старте) — `PrimumLampLighting.cs:357-377`. Emissive корпусов — `PM_LAMP_GLOW_WARM` (`MatDir`, `EnsureGlowMaterial`).

## 2. PrimumLightPool

Файл `Assets/_Project/Scripts/World/PrimumLightPool.cs`. Параметры (сериализованы в `WorldScene_0_0.unity:1210627-1210632`):

| Параметр | Значение |
|---|---|
| checkInterval | 0.25 с |
| maxActive | 96 |
| activeRadius | 140 м |
| hysteresis | 0.1 (10%) |
| focus | null (автопоиск камеры) |
| lightsRoot | null → transform объекта |

- `Update` (`:61-68`): таймер += deltaTime, при достижении checkInterval — `Rebuild()` (если пусто) + `Check()`.
- `Rebuild` (`:70-87`): собирает все Light под корнем (включая неактивные), сбрасывает `_on[]`, гасит все.
- `Check` (`:89-157`): onDist=140, offDist=154; уже включённые держатся до offDist; новые кандидаты в onDist; сортировка по дистанции; cap = maxActive (96); применение diff к `Light.enabled`.
- Фокус (`TryGetFocusPosition` `:165-187`, `FindFocusCamera` `:189-219`): если `focus != null` — его позиция; иначе среди всех активных камер выбирается та, у которой ближайший светильник ближе всех (поиск раз в 1 с). `Camera.main` не используется. Позиции читаются живьём (FO-safe).
- Создание пула (`PrimumLampLighting.Place()` `:382-523`): корень `PRIMUM_LIGHTS` под `worldRoot` (identity, слой 8, static-флаги 0) `:401-407`; `root.AddComponent<PrimumLightPool>()` на `:408`.

## 3. AdditionalLightIntensityController (ALIC)

Файл `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs`. Хостер — `BootstrapScene.unity` (`:4930-4941`).

- Discovery (`RefreshLights` `:389-419`): `FindObjectsByType<Light>` (включая неактивные), пропускает Directional при `controlOnlyAdditionalLights=1`. Rescan каждые `rescanInterval=2` с (`:183`).
- Группы (`LightGroup`, `:75-180`): `groupId`, `nameContains` (case-insensitive substring по имени Light; пусто → матчит все), `intensityMultiplier`, `intensityByHour` (AnimationCurve 0–24 → доля 0..1), `overrides`, flicker. `Evaluate` `:130-142`.
- Порядок групп (top→bottom, первая совпавшая): в сцене:
  1. `PRIMUM_OUTLIGHT` (`nameContains: PRIMUM_OUTLIGHT`) — multiplier 20, кривая 0–5=1, 6–17=0, 19=0.25, 21–24=1 (`BootstrapScene.unity:4948-5016`).
  2. `PRIMUM_INLIGHT` (`nameContains: PRIMUM_INLIGHT`) — multiplier 1.76, кривая 0→1, 13.33→0.752, 24.04→0.990 (`:5048-5083`); overrides: range=48.3 (`overrideRange=1`, `:5085-5086`), type=Point (`overrideType=1`), innerSpotAngle=21, color=(0.883,0.991,0.923), flickerEnabled=1, flickerHzMin=0.25, flickerHzMax=1, flickerDepthMin=0.053, flickerDepthMax=0.351, flickerDurationMin=0.04, flickerDurationMax=0.12 (`:5087-5111`).
  3. `CityLamps` (`MD2_Lamp_`) — multiplier 1, кривая night 1 / day 0.
  4. `AllAdditionalLights` (catch-all) — multiplier 1, кривая 0–5=1, 6–17=0.15, 19=0.4, 21–24=1.
- Применение (`ApplyIntensity` `:440-478`): smooth Lerp (transitionSpeed=4) при смене времени; при смене группы/ревизии — `RestoreBaseline` + `ApplyOverrides`; мерцание поверх сглаженной яркости (`EvaluateFlickerFactor` `:539-583`). Flicker: треугольный провал, детерминированный xorshift на источник (seed = hash имени + EntityId). Для выключенных пулом ламп события не планируются.
- Переопределения (`LightOverrides` `:23-73`): range, type, spot/inner angles, color, color temp, shadows, shadowStrength, bounceIntensity — каждый флагом. Наружная группа переопределений не использует (все override*=0).

## 4. VisibilityDirector и лампы

Файл `Assets/_Project/Scripts/World/VisibilityDirector.cs`.

- `GroupClass.Lamps` (`:71`), `Owner.PrimumLightPool` (`:74`).
- В `Build()` (`:367-420`): для дочернего узла WorldRoot, имя которого оканчивается на `_LIGHTS` → `AddLampGroup(child, world)` (`:398`).
- `AddLampGroup` (`:533-538`): создаёт группу класса `Lamps`, `visibilityOwner = PrimumLightPool`, `shadowOwner = None`, `directed = false`; `rendererCount = root.GetComponentsInChildren<Light>(true).Length` (2 087).
- Директор **не пишет** `Light.enabled` — владение PrimumLightPool (`:15`, `:17-18`: лампы только наблюдаются, не трогаются; `forceRenderingOff` директор пишет только у домов/ферм/спутников). Лампы учитываются как счётчики в `DescribeGroups()` (`:287-308`).
- `DescribeProviders()` (`:603-613`) фиксирует `PrimumLightPool = active/total` среди провайдеров.

CityShadowBudget.cs, ShadowBudget.cs, DoorVisibilityBudget.cs — свет не трогают.

## 5. Файлы и сцены

- Сцена города: `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — корень `PRIMUM_LIGHTS` (id 1321687946) + компонент `PrimumLightPool` (`:1210615-1210632`), 21 чанк-узел-ребёнок (`:1210591-1210612`).
- Сцена бутстрапа: `Assets/_Project/Scenes/BootstrapScene.unity` — `AdditionalLightIntensityController` (`:4930-4941`) с группами (`:4947+`).
- Настройки пула — сериализованы в `WorldScene_0_0.unity`.
- Настройки групп/кривых — сериализованы в `BootstrapScene.unity`.
- Спецификации (range/intensity/color по семействам) — в коде `PrimumLampLighting.SpecOf` (`:107-128`).
- Документация: `docs/world/primum_city/08_LIGHTING/01_LIGHTING.md`, `lamps_audit.csv` (2 849 строк), `placement_manifest.csv` (3 379 строк, 2 087 place).
- Другие сцены WorldScene_* не содержат PRIMUM_LIGHTS.

## 6. Ключевые числа

- Источников всего: **2 087** (`PRIMUM_LIGHTS` в `WorldScene_0_0.unity`, out 753 + in 1 334).
- Мешей в LAMPS: **5 888**; кластеров: **2 045** (`01_LIGHTING.md:13-14`).
- Пул: maxActive **96**, radius **140 м**, checkInterval **0.25 с**, hysteresis **0.1**.
- Cap в коде: `Mathf.Max(1, maxActive)` (`PrimumLightPool.cs:100`); offDist = onDist*(1+hysteresis) (`:97`).
- Уличные (out): 753 = StreetPost 83 + StreetWall 302 + FacadeWall 368.
- Интерьерные (in): 1 334 = InteriorSconce 518 + InteriorTable 493 + InteriorPendant 323; ADMIN 530.
- Группы ALIC: PRIMUM_OUTLIGHT multiplier **20** (`BootstrapScene.unity:4950`), PRIMUM_INLIGHT multiplier **1.76** (`:5050`), range override 48.3 (`:5086`), цвет (0.883,0.991,0.923) (`:5094`).
- Flicker INLIGHT: 0.25–1 Гц, глубина 0.053–0.351, длительность 0.04–0.12 с (`:5105-5111`); OUTLIGHT flicker выкл.
- VisibilityDirector: лампы — группа `Lamps`, owner `PrimumLightPool`, directed=false, rendererCount = число Light. Директор лампы не направляет (только наблюдает).
- Rescan ALIC: 2 с (`AdditionalLightIntensityController.cs:183`, scene `:4942`); transitionSpeed 4 (`:188`, scene `:4945`).
- Узлов LAMPS: 22 (21 чанк) (`01_LIGHTING.md:12`). Чанк-узлов под PRIMUM_LIGHTS в сцене: 21 (`WorldScene_0_0.unity:1210591-1210612`).

*Источник: подотчёт сабагента A.*