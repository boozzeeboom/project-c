# Lighting Implementation Log — Project C

## Статус

- **Дата старта:** 2026-09-08
- **Рабочий источник правды:** `docs/world/lights/lighting-plan.md`
- **Завершённые этапы:** Stage 1 / T-LIGHT02, Stage 4 / T-LIGHT05 и Stage 7 / T-LIGHT08
- **Текущий этап:** Stage 13 / T-LIGHT15 — живая настройка групп в Play Mode и перенос значений в сцену
- **Статус этапа:** реализовано; проверка сценария «правка → Stop → сохранение» за пользователем
- **Статус Unity Editor:** подключён; изменения выполнены через Unity MCP

## Решение по плану

План утверждён как рабочий с обязательным пилотированием. До визуальной и runtime-приёмки `WorldScene_0_0` нельзя запускать массовые изменения и bake всех 24 мировых сцен.

## Проверенный baseline

- Unity Editor: `6000.5.2f1`
- URP: `17.5.0`
- Рендер-путь: Forward
- HDR: отключён в `Assets/_Project/Settings/ProjectC_URP.asset`
- Additional Lights: `m_AdditionalLightsRenderingMode: 2` = `Per Vertex`
- Additional Lights per object: `4`
- Additional light shadows: отключены
- Reflection Probe blending: отключено
- Reflection Probe box projection: отключено
- World scenes: 24 файла `WorldScene_0_0`–`WorldScene_5_3`
- Lighting Settings: `m_LightingSettings: {fileID: 0}` в просмотренных WorldScene
- Профили Day/Night/Twilight существуют:
  - `Assets/_Project/ScriptableObjects/DayNight/Volumes/DayVolumeProfile.asset`
  - `Assets/_Project/ScriptableObjects/DayNight/Volumes/NightVolumeProfile.asset`
  - `Assets/_Project/ScriptableObjects/DayNight/Volumes/TwilightVolumeProfile.asset`

## Решения и ограничения

1. P1.1 выполняется первым и отдельно от сценового bake.
2. P1.2 начинается с `WorldScene_0_0`, а не с полного набора мировых сцен.
3. Baked GI не считается автоматически совместимым с phase-driven Day/Night; этот риск проверяется отдельно.
4. Light Probe Groups добавляются после получения валидных lighting data пилотной сцены.
5. Reflection Probe с экстремальным мировым размером из исходного плана не принимается без memory/GPU-проверки.
6. Emissive и Volume Profile tuning выполняются отдельными этапами, чтобы можно было изолировать визуальный результат.
7. Текущие несвязанные изменения в рабочем дереве не входят в lighting-этап:
   - `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`
   - `Assets/_Project/Quests/Data/QuestDatabase.asset`

## Этапы

| Этап | Тикет | Содержание | Статус |
|---|---|---|---|
| 0 | T-LIGHT01 | Аудит, утверждение плана, baseline и контрольные ворота | ✅ Документально завершён |
| 1 | T-LIGHT02 | Additional Lights → Per Pixel | ✅ Завершён |
| 2 | T-LIGHT03 | LightingSettings + пилотный bake `WorldScene_0_0` | ⚠️ Инфраструктура создана; bake заблокирован |
| 3 | T-LIGHT04 | Light Probe Groups в пилотной сцене | ⏳ Не начат |
| 4 | T-LIGHT05 | Пилотные Point/Spot Lights | ✅ Realtime-пилот фонарей завершён |
| 5 | T-LIGHT06 | Reflection Probes | ⏳ Не начат |
| 6 | T-LIGHT07 | Emissive материалы | ⏳ Не начат |
| 7 | T-LIGHT08 | Day/Twilight/Night Volume tuning | ⚠️ Ночная экспозиция исправлена; полный tuning не завершён |
| 8 | T-LIGHT09 | Глобальный контроль интенсивности дополнительных источников по группам и времени суток | ✅ Реализован; Play Mode-приёмка pending |
| 9 | T-LIGHT10 | Освещение интерьеров днём: ключевой свет, тени солнца, перераспределение direct/ambient | ✅ Шаги 1–5 выполнены; визуальная приёмка pending |
| 10 | T-LIGHT11 | Качество теней солнца (грубая кромка, soft shadows выключены) | ⏳ Не начат — зафиксирован по итогам T-LIGHT10 |
| 11 | T-LIGHT13 | Расширенное управление группой: переопределения range / type / spot angles / color / color temperature / shadows / bounce | ✅ Реализован; Play Mode-приёмка pending |
| 12 | T-LIGHT14 | Случайное мерцание каждой лампы группы (`flickerEnabled`, границы Гц, глубина и длительность провала) | ✅ Реализован; Play Mode-приёмка pending |
| 13 | T-LIGHT15 | Живое применение правок инспектора в Play Mode (`OnValidate` → ревизия) и перенос настроенных групп из Play Mode в редактируемую сцену | ✅ Реализован; сценарий проверяется пользователем |

## Результат Stage 1 / T-LIGHT02

- Изменён ассет `Assets/_Project/Settings/ProjectC_URP.asset` через `manage_graphics.pipeline_set_settings`.
- `m_AdditionalLightsRenderingMode`: `PerVertex` → `PerPixel`.
- Остальные значения URP Asset не изменялись намеренно: HDR off, MSAA 1x, shadow distance 1000, 2 cascades, limit 4 additional lights.
- `check_compile_errors`: `No compile errors`.
- Unity Console: ошибок нет; найденные предупреждения не относятся к Stage 1.
- Сцены и LightingSettings на этом этапе не изменялись.

## Результат Stage 2 / T-LIGHT03

- Создан `Assets/_Project/Settings/LightingSettings_World.asset`.
- Настройки: Progressive GPU, `bakedGI=true`, `realtimeGI=false`, `mixedBakeMode=IndirectOnly`, resolution `25`, AO off, max bounces `2`.
- Settings назначены сцене `Assets/_Project/Scenes/World/WorldScene_0_0.unity`.
- При первом bake активной была только `WorldScene_0_0`: `lights=0`, `meshRenderers=7413`, `contributeRenderers=0`, `lightmapCount=0`.
- После additive-загрузки Bootstrap live-инвентаризация показала: `Sun` и `Moon` существуют только в Bootstrap и оба имеют `LightmapBakeType=Realtime`; статической геометрии в Bootstrap нет.
- `WorldScene_0_0` получил `LightingSettings_World`, Bootstrap остался без LightingSettings.
- Пустой результат bake очищен через `bake_clear`; generated `LightingData.asset` в рабочем дереве не остаётся.
- Sun/Moon, DayNightController и режимы источников света не изменялись.
- `manage_scene.validate` выявил существующий missing script на `[Ship_Key_Container]/[KeyRod_ShipHeavy]`; он не относится к T-LIGHT03 и не исправлялся.

**Вывод:** P1.2 нельзя считать завершённым. Для валидного GI требуется отдельное решение: какие источники участвуют в bake и какие renderer-и мира получают `Contribute GI`. До этого P1.3 и массовый bake не запускаются.

## Результат Stage 4 / T-LIGHT05

- Активная сцена: `Assets/_Project/Scenes/World/WorldScene_0_0.unity`.
- Внутри `WorldRoot_0_0/Primum/gorod port_3_3_unity_1` найдено 11 housing-объектов: `MD2_Lamp_01_Housing`–`MD2_Lamp_11_Housing`.
- Созданы дочерние источники `MD2_Lamp_01_PointLight`–`MD2_Lamp_11_PointLight`.
- Параметры каждого источника: `LightType.Point`, `Realtime`, intensity `50`, range `30`, color `#FFB070`, shadows `None`.
- Проверка сцены: 11 включённых Point Lights, каждый является дочерним своего housing.
- `check_compile_errors`: `No compile errors`.
- Сцена сохранена; bake не запускался.

**Ограничение:** визуальная проверка в Play Mode и screenshots не выполнялись автоматически; визуальный результат должен подтвердить пользователь.

## Коррекция Night Volume / T-LIGHT08

- В `Assets/_Project/ScriptableObjects/DayNight/Volumes/NightVolumeProfile.asset` найдено `ColorAdjustments.postExposure = -7`.
- Значение изменено через Unity Editor API на `-0.8`.
- Причина исправления: realtime Point Lights были визуально подавлены экстремальным ночным exposure.
- Синий фильтр `colorFilter`, contrast `5`, saturation `-10`, Bloom и temperature volume не изменялись.
- Проверка ассета после записи: `m_Value: -0.8`.
- `check_compile_errors`: `No compile errors`.
- Runtime-проверка после изменения не выполнялась; DayNightController создаёт runtime-копии профилей при старте, поэтому нужен новый Play Mode-прогон.

**Статус:** это точечная коррекция причины проблемы, а не завершение всего P3.7.

## Результат Stage 8 / T-LIGHT09

- В `Assets/_Project/Scenes/World/WorldScene_0_0.unity` параметры 11 источников `MD2_Lamp_01_PointLight`–`MD2_Lamp_11_PointLight` изменены на intensity `50` и range `30`.
- Создан `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs`.
- Контроллер добавлен в `Assets/_Project/Scenes/BootstrapScene.unity` на глобальный объект `DayNightController`.
- Контроллер обнаруживает все не-Directional источники во всех загруженных сценах, сохраняет их авторскую базовую интенсивность и применяет первый совпавший профиль группы.
- В профиле по умолчанию есть группа `CityLamps` для `MD2_Lamp_` и catch-all группа `AllAdditionalLights` для остальных дополнительных источников.
- Интенсивность задаётся через `AnimationCurve` по времени `0–24` и плавно изменяется при переходе между периодами суток.
- Источник времени — `DayNightController.ServerTimeOfDay`, с fallback на `ServerWeatherController.TimeOfDay`.
- Проверки: `check_compile_errors` — `No compile errors`; scene readback — 11 источников, intensity `50–50`, range `30–30`.
- Автоматический Play Mode и screenshots не выполнялись; визуальная приёмка остаётся за пользователем.

## Проверки Stage 0

- [x] План прочитан полностью.
- [x] Package/editor versions сверены с проектом.
- [x] URP Asset сверен с YAML baseline.
- [x] 24 WorldScene assets найдены.
- [x] Day/Night/Twilight Volume Profiles найдены.
- [x] Отсутствие LightingSettings в просмотренных сценах зафиксировано.
- [x] Тикет `T-LIGHT01` выбран после проверки отсутствия существующего lighting-трекера.
- [ ] Unity-side visual verification — невозможна до запуска Unity Editor.
- [ ] Play Mode screenshot verification — выполняется только пользователем после запуска проекта.

## Результат Stage 9 / T-LIGHT10 — шаги 1–5

- Baseline для отката: `a6710796`.
- `Assets/_Project/Scenes/BootstrapScene.unity`:
  - удалён `DemoKey_Temp` (`GameObject 2082049667`, `Light 2082049669`, `Transform 2082049670`, `UniversalAdditionalLightData 2082049668`) вместе с записью в `SceneRoots`;
  - `Sun`: `m_Shadows.m_Type 0 → 2` (Soft), `m_Shadows.m_Strength 0 → 0.9`.
- `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs`, `Initialize()`:
  - `RenderSettings.ambientMode = AmbientMode.Skybox;`
  - `if (sunLight != null) RenderSettings.sun = sunLight;`
- `WorldScene_0_0.unity` не изменялся; dirty-флаг снят, рабочее дерево по сцене чистое.
- `check_compile_errors`: `No compile errors`.

**Шаги 3–5 — применены разово по запросу пользователя после визуального подтверждения шагов 1–2:**

- `Assets/_Project/ScriptableObjects/DayNight/MiddayPhase.asset`: `ambientIntensity 0.60 → 0.20`, `sunIntensity 1.0 → 1.4`.
- `Assets/_Project/ScriptableObjects/DayNight/MorningPhase.asset`: `ambientIntensity 0.40 → 0.12`, `sunIntensity 0.8 → 1.0`.
- `Assets/_Project/ScriptableObjects/DayNight/EveningPhase.asset`: `ambientIntensity 0.45 → 0.15`, `sunIntensity 0.7 → 0.9`.
- `Assets/_Project/ScriptableObjects/DayNight/TwilightPhase.asset`: `ambientIntensity 0.25 → 0.08`.
- `Assets/_Project/ScriptableObjects/DayNight/NightPhase.asset`: `ambientIntensity 0.10 → 0.05`.
- `Assets/_Project/ScriptableObjects/DayNight/Volumes/DayVolumeProfile.asset`: `ColorAdjustments.postExposure 1.5 → 0.7`, `contrast 6 → 13` (`saturation −10` не менялось).
- Значения прочитаны обратно из загруженных ассетов через `AssetDatabase.LoadAssetAtPath` — совпадают с применёнными.
- Смысл: яркость дня перенесена с неокклюдированной скайбокс-заливки на прямой свет. `Sun intensity` тронут осознанно: запрет `lighting-plan.md` пересмотрен в объёме фазовых ассетов, логика `DayNightController` не правилась.

**Отменённое решение:** перевод ambient в `Trilight` отклонён — `RenderSettings.ambientIntensity` действует только в режиме `Skybox`.

**Зафиксированное ограничение (→ `T-LIGHT11`):** тени работают, но грубые. Причина — настройки URP Asset (`m_ShadowDistance 1000`, 2 каскада, `m_Cascade2Split 0.25`, shadowmap `2048`, `m_SoftShadowsSupported 0`): ≈0.12 м/тексель в каскаде 1 и ≈0.37 м/тексель в каскаде 2, soft-фильтрация в ассете выключена. В `T-LIGHT10` не исправлялось.

**Документ этапа:** `docs/world/lights/T-LIGHT10-interior-lighting.md`.

## Результат Stage 11 / T-LIGHT13

- В `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs` добавлен вложенный `[Serializable]` класс `LightOverrides` и поле `LightGroup.overrides`.
- Переопределяемые свойства: `range`, `type`, `spotAngle`, `innerSpotAngle`, `color`, `useColorTemperature`, `colorTemperature`, `shadows`, `shadowStrength`, `bounceIntensity`. Каждое включается отдельным флагом `override*`; выключенный флаг не трогает авторское значение источника.
- Базовые значения источника снимаются один раз при первом обнаружении в новый `LightState` (вместо прежнего словаря `_baseIntensityByLight`); при смене группы выполняется `RestoreBaseline` → `ApplyOverrides`.
- Порядок применения: сначала `type`, затем `range` и углы Spot (проверка `LightType.Spot` идёт по уже применённому типу). Клэмпы: `range ≥ 0.01`, `spotAngle 1…179`, `innerSpotAngle ≤ spotAngle − 0.1`, `colorTemperature 1000…20000`, `shadowStrength 0…1`, `bounceIntensity ≥ 0`.
- Плавный переход интенсивности теперь считается в `LightState.smoothedIntensity`, а не от текущего `Light.intensity`: это готовит почву для мерцания (`T-LIGHT14`), которое должно применяться поверх сглаженного значения и не попадать обратно в сглаживание.
- `LightType.Directional` в `overrideType` игнорируется: направленные источники группами не управляются.
- Сцены не изменялись: в `BootstrapScene.unity` массив `groups` сериализован без новых полей, все флаги `override*` = `false`, поведение существующих групп не изменилось. `WorldScene_0_0.unity` не трогалась.
- `refresh_unity` (`scope: scripts`, `compile: request`) и `check_compile_errors`: `No compile errors`.
- Play Mode и визуальная проверка переопределений — **NOT RUN**, выполняется пользователем.

**Документ этапа:** `docs/world/lights/T-LIGHT13-light-group-overrides.md`.

## Результат Stage 12 / T-LIGHT14

- В `LightGroup` добавлены поля мерцания: `flickerEnabled`, `flickerHzMin`, `flickerHzMax`, `flickerDepthMin`, `flickerDepthMax`, `flickerDurationMin`, `flickerDurationMax`; всё по умолчанию — мерцание выключено (`flickerEnabled = false`).
- Мерцание считается на каждый источник отдельно (`LightState` + собственный `FlickerRandom` — xorshift на хеше имени и `EntityId`). `UnityEngine.Random` не используется, поэтому глобальный поток случайности не сдвигается.
- Провал — треугольный импульс: `factor = 1 − depth · (1 − |2u − 1|)` внутри `[start…end]`. Частота, глубина и длительность перебрасываются заново на каждое событие, поэтому синхронной пульсации группы нет.
- Множитель мерцания накладывается **после** `Mathf.Lerp` (поверх `state.smoothedIntensity`): иначе сглаживание съедало бы короткие провалы.
- `Update()` вызывает `ApplyIntensity` каждый кадр при `_anyFlickerEnabled` — мерцание работает и при замороженном времени суток; флаг пересчитывается в `EnsureGroupData` и в каждом рескне.
- Для выключенных пулом источников (`light.enabled == false`) события не планируются — накопившийся провал не срабатывает в момент включения.
- `SanitizeFlicker()` в `Awake` восстанавливает нулевые/перевёрнутые границы, оставшиеся от старой сериализации сцен.
- Замена `GetInstanceID()` → `EntityId.ToULong(light.GetEntityId())`: в Unity `6000.5.2f1` `GetInstanceID` помечен `[Obsolete(error: true)]` (CS0619).
- Сцены не изменялись. `check_compile_errors`: `No compile errors`. Play Mode — **NOT RUN**.

**Документ этапа:** `docs/world/lights/T-LIGHT14-lamp-flicker.md`.

## Результат Stage 13 / T-LIGHT15

- `OnValidate()` (`#if UNITY_EDITOR`) поднимает `_settingsRevision` и `_forceApply`; `ApplyIntensity` переприменяет `RestoreBaseline` → `ApplyOverrides` при изменении ревизии, `Update()` вызывает его немедленно (`immediate: true`). Раньше переопределения применялись только при смене группы, поэтому правка в Play Mode ни на что не влияла.
- `RestoreBaseline`/`ApplyOverrides` стали идемпотентными (запись только при отличии): перетаскивание слайдера не дёргает `Light.type`/`Light.range` у ~1 900 источников каждый кадр.
- `EnsureGroupData(bool sanitizeFlicker)`: в `OnValidate` — `false` (чтобы набираемый ноль не перебивался дефолтом), в `Awake` — `true`.
- Новый `Assets/_Project/Editor/Lighting/LightGroupPlayModeCapture.cs` (`[InitializeOnLoad]`): на `ExitingPlayMode` снимает массив `groups` через `EditorJsonUtility` в `SessionState`, на `EnteredEditMode` сравнивает JSON с редакторским объектом и при отличии переносит значения (`Undo.RecordObject`, `SetDirty`, `MarkSceneDirty`). Переносятся только `groups`, не весь компонент.
- В контроллере появилось editor-only свойство `EditorGroups` для доступа инструмента к приватному массиву.
- Проверка раунд-трипа JSON через рефлексию приватных `ToJson`/`FromJson`: `groups 4 → 4`, `jsonLen = 7532`, длины кривых сохранены (7 / 3 / 7 / 7), `overrides != null`, `flickerHz 0.5 / 3`.
- `check_compile_errors`: `No compile errors`. Play Mode-прогон сценария — **NOT RUN**.
- Сцены не изменялись и не сохранялись (в рабочем дереве остаются правки пользователя).

**Документ этапа:** `docs/world/lights/T-LIGHT15-live-tuning.md`.

**Замечание по данным (не менялось):** у группы `PRIMUM_INLIGHT` кривая `intensityByHour` задана в диапазоне 0…1 по X (3 ключа), а `Evaluate()` читает 0…24 — после первого часа кривая упирается в последний ключ и отдаёт `1.0`, то есть группа горит круглосуточно с множителем 15.

## Следующий шаг

Пользователь должен перезапустить Play Mode и проверить ночную зону фонарей. Если свет стал виден, следующим техническим шагом остаётся отдельный tuning синего Night Volume и temperature overlay. T-LIGHT04 и массовый bake остаются заблокированными решением по GI.
