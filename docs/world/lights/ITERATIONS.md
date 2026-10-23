# World Lighting — Итерации

## Итерация 1 от 2026-09-08

**Задача:** Провести аудит `lighting-plan.md`, утвердить поэтапную реализацию и зафиксировать baseline проекта перед изменениями Unity-ассетов.  
**Тикет:** `T-LIGHT01`  
**Коммит:** `15484f80` — T-LIGHT01: утвердить план освещения и зафиксировать baseline

**Изменения:**
- `docs/world/lights/lighting-plan.md` — обновлён до версии 1.1; добавлены результаты аудита, корректировки плана, этапы и контрольные ворота.
- `docs/world/lights/implementation-log.md` — создан журнал реализации и baseline.
- `docs/world/lights/ITERATIONS.md` — создан локальный журнал этапов lighting.

**Подтверждено:**
- Unity `6000.5.2f1`, URP `17.5.0`.
- `ProjectC_URP.asset`: Additional Lights = `Per Vertex`, лимит `4`, Reflection Probe blending/box projection выключены.
- Найдены 24 `WorldScene_X_Y`.
- Найдены Day/Night/Twilight Volume Profiles.
- В просмотренных WorldScene отсутствуют назначенные LightingSettings.

**Ограничение:** Unity Editor не подключён к MCP for Unity bridge, поэтому Stage 1 и последующие изменения Unity-сцен/ассетов не запускались. В рабочее дерево не включались несвязанные изменения.

**Следующий этап:** `T-LIGHT02` — перевести Additional Lights на Per Pixel через Unity Editor и выполнить отдельную проверку.

## Итерация 2 от 2026-09-08

**Задача:** Выполнить P1.1: перевести Additional Lights с `Per Vertex` на `Per Pixel` через Unity MCP и проверить компиляцию.
**Тикет:** `T-LIGHT02`
**Коммит:** `566349d2` — T-LIGHT09: добавить глобальный контроль дополнительного света

**Изменения:**
- `Assets/_Project/Settings/ProjectC_URP.asset` — `m_AdditionalLightsRenderingMode` изменён с `PerVertex` на `PerPixel` через Unity MCP.
- `docs/world/lights/lighting-plan.md` — зафиксирован результат Stage 1 и следующий gate.
- `docs/world/lights/implementation-log.md` — обновлён статус этапов и результат проверки.

**Проверки:**
- `pipeline_get_settings`: `m_AdditionalLightsRenderingMode: PerPixel`.
- `check_compile_errors`: `No compile errors`.
- Unity Console: ошибок нет.
- Сцены и LightingSettings не изменялись.

**Следующий этап:** `T-LIGHT03` — пилотный LightingSettings и bake только для `WorldScene_0_0`.

## Итерация 3 от 2026-09-08

**Задача:** Создать LightingSettings для `WorldScene_0_0` и проверить пилотный bake без изменения Sun/Moon и DayNightController.
**Тикет:** `T-LIGHT03`
**Коммит:** `5c562887` — T-LIGHT03: создать lighting settings и зафиксировать блокер bake

**Изменения:**
- `Assets/_Project/Settings/LightingSettings_World.asset` — создан через Unity Editor API.
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — LightingSettings назначен сцене.
- Пустой generated bake очищен после проверки.

**Проверки:**
- Settings: Progressive GPU, `IndirectOnly`, 25 texels/unit, AO off, 2 bounces.
- Пилотный bake: `lightmapCount=0`.
- `WorldScene_0_0`: 0 lights, 7413 mesh renderers, 0 renderer-и с собственным `Contribute GI`.
- Bootstrap: 2 directional lights (`Sun`, `Moon`), обе `Realtime`; статической геометрии нет.
- Ошибок компиляции после этапа нет.
- `manage_scene.validate` выявил существующий missing script на `[Ship_Key_Container]/[KeyRod_ShipHeavy]`; он не относится к T-LIGHT03 и не исправлялся.

**Статус:** инфраструктура создана, но GI bake заблокирован. До принятия решения по источникам GI и static flags переходить к T-LIGHT04 нельзя.

## Итерация 4 от 2026-09-08

**Задача:** Добавить realtime Point Lights к фонарным housing-объектам `MD2_Lamp_*_Housing` в городе `gorod port_3_3_unity_1` для визуального lighting-пилота.
**Тикет:** `T-LIGHT05`
**Коммит:** `c077c8bf` — T-LIGHT05: добавить realtime-свет фонарей MD2

**Изменения:**
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — добавлены 11 дочерних Point Lights.
- Источники названы `MD2_Lamp_01_PointLight`–`MD2_Lamp_11_PointLight`.
- Параметры: intensity `2`, range `15`, color `#FFB070`, shadows off.

**Проверки:**
- Найдены и обработаны все 11 housing-объектов.
- Все 11 источников включены и имеют правильного родителя.
- `check_compile_errors`: `No compile errors`.
- Play Mode-визуальная проверка остаётся за пользователем.

**Следующий этап:** ручная проверка результата в Play Mode; T-LIGHT04 и bake пока не запускать.

## Итерация 5 от 2026-09-08

**Задача:** Исправить ночную невидимость realtime Point Lights, вызванную экстремальным Night Volume exposure.
**Тикет:** `T-LIGHT08`
**Коммит:** `6aff2304` — T-LIGHT08: исправить ночную экспозицию lighting volume

**Изменения:**
- `Assets/_Project/ScriptableObjects/DayNight/Volumes/NightVolumeProfile.asset` — `ColorAdjustments.postExposure` изменён с `-7` на `-0.8`.

**Подтверждённая причина:**
- Ночной профиль имел `postExposure = -7`, тогда как утверждённое направление плана — около `-0.8`.
- Синий фильтр и дополнительные shader/temperature effects сохранены без изменений для изолированной проверки.

**Проверки:**
- Asset readback: `m_Value: -0.8`.
- `check_compile_errors`: `No compile errors`.
- Новый Play Mode-прогон ещё требуется, потому что DayNightController создаёт runtime-копию профиля при запуске.

## Итерация 6 от 2026-09-08

**Задача:** Усилить realtime-фонари и добавить глобальное управление интенсивностью всех дополнительных источников по группам и времени суток.
**Тикет:** `T-LIGHT09`
**Коммит:** будет добавлен после фиксации этапа.

**Изменения:**
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — 11 источников `MD2_Lamp_*_PointLight` переведены на intensity `50`, range `30`.
- `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs` — создан глобальный контроллер групповой интенсивности.
- `Assets/_Project/Scenes/BootstrapScene.unity` — контроллер добавлен на `DayNightController`.

**Архитектура:**
- Контролируются все не-Directional источники во всех загруженных сценах.
- Для каждого источника сохраняется базовая интенсивность; групповой профиль применяется как множитель.
- Группы проверяются сверху вниз по совпадению имени.
- `CityLamps` управляет `MD2_Lamp_`; `AllAdditionalLights` является catch-all группой.
- Профили используют `AnimationCurve` по времени суток `0–24` и плавную интерполяцию.
- Время читается из `DayNightController.ServerTimeOfDay` с fallback на `ServerWeatherController.TimeOfDay`.

**Проверки:**
- `check_compile_errors`: `No compile errors`.
- Readback WorldScene: `11` источников, intensity `50–50`, range `30–30`.
- Play Mode и screenshots не выполнялись; визуальная приёмка остаётся за пользователем.

## Итерация 7 от 2026-10-23

**Задача:** Сделать так, чтобы дома без источников света днём были заметно темнее открытых пространств.
**Тикет:** `T-LIGHT10`
**Коммит:** `main`, сообщение `T-LIGHT10: убрать временный key light и включить тени солнца`
**Baseline для отката:** `a6710796`

**Изменения (шаги 1–2):**
- `Assets/_Project/Scenes/BootstrapScene.unity` — удалён `DemoKey_Temp` (`GameObject 2082049667`, `Light 2082049669`, `Transform 2082049670`, `UniversalAdditionalLightData 2082049668`) и его запись в `SceneRoots`.
- `Assets/_Project/Scenes/BootstrapScene.unity` — `Sun`: `m_Shadows.m_Type 0 → 2`, `m_Shadows.m_Strength 0 → 0.9`.
- `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs` — в `Initialize()` добавлены `RenderSettings.ambientMode = AmbientMode.Skybox` и `RenderSettings.sun = sunLight`.
- Документация: `T-LIGHT10-interior-lighting.md` (новый), `lighting-plan.md`, `implementation-log.md`, `ITERATIONS.md`.

**Подтверждённые причины (до изменений):**
- `DemoKey_Temp`: Directional, intensity `3`, белый, `shadows = None`, `activeSelf = true`, ссылок в коде нет — становился главным светом URP, поэтому тени солнца не работали вообще.
- `Sun.shadowStrength = 0` — тень была прозрачной даже при `LightShadows.Soft`.
- `ambientMode = Skybox` с `ambientIntensity 0.6` (`MiddayPhase`) — неокклюдированная скайбокс-заливка, одинаковая в помещении и на улице.
- GI/AO/probes отсутствуют; `DayVolumeProfile.postExposure = +1.5` дополнительно поднимал и тени, и улицу.

**Отменённое решение:** перевод ambient в `Trilight` отклонён — `RenderSettings.ambientIntensity` документирован как множитель ambient, генерируемого из скайбокса, то есть работает только в режиме `Skybox`.

**Проверки:**
- `check_compile_errors`: `No compile errors`.
- `BootstrapScene` сохранена, `dirty = false`; `WorldScene_0_0` принудительно оставлена чистой и не изменялась.
- `git diff`: только `BootstrapScene.unity` (−129/+2) и `DayNightController.cs` (+9).

**Не сделано намеренно:** шаг 3 — понижение `ambientIntensity` по фазам, подъём `sunIntensity`, снижение `postExposure` и рост `contrast`. Ждёт визуального теста шагов 1–2 в Play Mode (gate пользователя).

## Итерация 8 от 2026-10-23

**Задача:** Проверить результат шагов 3–5 из `T-LIGHT10` — перенести яркость дня с ambient-заливки на прямой свет и скомпенсировать экспозицию Volume.
**Тикет:** `T-LIGHT10` (шаги 3–5)
**Baseline для отката:** `a6710796`

**Изменения:**
- `MiddayPhase.asset` — `ambientIntensity 0.60 → 0.20`, `sunIntensity 1.0 → 1.4`.
- `MorningPhase.asset` — `ambientIntensity 0.40 → 0.12`, `sunIntensity 0.8 → 1.0`.
- `EveningPhase.asset` — `ambientIntensity 0.45 → 0.15`, `sunIntensity 0.7 → 0.9`.
- `TwilightPhase.asset` — `ambientIntensity 0.25 → 0.08`.
- `NightPhase.asset` — `ambientIntensity 0.10 → 0.05`.
- `DayVolumeProfile.asset` — `ColorAdjustments.postExposure 1.5 → 0.7`, `contrast 6 → 13`.
- Документация: `T-LIGHT10-interior-lighting.md` (переписан, §5 шаги 3–5, §7 ограничение по теням, §9 откат), `lighting-plan.md` (+ `T-LIGHT11`), `implementation-log.md`, `ITERATIONS.md`.

**Проверки:**
- Значения прочитаны обратно из загруженных ассетов (`AssetDatabase.LoadAssetAtPath` → поля `TimeOfDayPhase` / `ColorAdjustments`) — совпадают.
- `check_compile_errors`: `No compile errors`.
- `WorldScene_0_0.unity` не изменялась и не сохранялась.

**Зафиксировано по итогам теста шагов 1–2:** тени солнца работают, но грубые. Причина — URP Asset: `m_ShadowDistance 1000`, 2 каскада, `m_Cascade2Split 0.25`, shadowmap `2048` (≈0.12 м/тексель в каскаде 1, ≈0.37 м — в каскаде 2), `m_SoftShadowsSupported 0`. Вынесено в отдельный тикет `T-LIGHT11`.

**Следующий шаг:** пользователь проверяет в Play Mode критерии §8 `T-LIGHT10`. Отдельно, когда будет воля — `T-LIGHT11` (качество теней). `T-LIGHT04` и массовый bake остаются заблокированными решением по GI.

## Итерация 9 от 2026-10-23

**Задача:** расширить `AdditionalLightIntensityController` с «только интенсивности» до полноценного управления группой (range, цвет, тип, углы Spot, тени, bounce) и добавить случайное мерцание каждой лампы группы со своими параметрами.
**Тикеты:** `T-LIGHT13` (переопределения), `T-LIGHT14` (мерцание)
**Файл:** `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs`

**Изменения `T-LIGHT13`:**
- Вложенный `[Serializable] LightOverrides` + `LightGroup.overrides`; каждое свойство включается своим флагом `override*`, выключенный флаг не трогает авторское значение.
- Новый `LightState` вместо `Dictionary<Light,float>`: снимает базовые `intensity/range/color/type/spotAngle/innerSpotAngle/useColorTemperature/colorTemperature/shadows/shadowStrength/bounceIntensity`.
- При смене группы — `RestoreBaseline` → `ApplyOverrides`; порядок применения: сначала `type`, потом `range` и углы Spot.

**Изменения `T-LIGHT14`:**
- Поля группы `flickerEnabled`, `flickerHzMin/Max`, `flickerDepthMin/Max`, `flickerDurationMin/Max`.
- На каждый источник — свой `FlickerRandom` (xorshift от хеша имени и `EntityId`); `UnityEngine.Random` не используется.
- Провал — треугольный импульс, частота/глубина/длительность перебрасываются на каждое событие → синхронной пульсации группы нет.
- Множитель мерцания применяется после `Mathf.Lerp`, чтобы сглаживание не съедало короткие провалы.
- `Update()` пересчитывает каждый кадр при `_anyFlickerEnabled` — работает и при замороженном времени суток; для ламп, выключенных пулом, события не планируются.
- `SanitizeFlicker()` восстанавливает нулевые границы от старой сериализации сцен.
- `GetInstanceID()` → `EntityId.ToULong(GetEntityId())`: в `6000.5.2f1` `GetInstanceID` помечен `[Obsolete(error: true)]` (CS0619).

**Документация:** `T-LIGHT13-light-group-overrides.md` (новый), `T-LIGHT14-lamp-flicker.md` (новый), `implementation-log.md`, `ITERATIONS.md`.

**Проверки:**
- `check_compile_errors`: `No compile errors`.
- Сцены не изменялись: `BootstrapScene`/`WorldScene_0_0` остались в рабочем дереве с правками пользователя (кривые `PRIMUM_INLIGHT`, дверные створки) и в коммиты не включались.
- Play Mode и визуальная оценка мерцания — **NOT RUN**, выполняет пользователь.

**Раздельные коммиты:** `1e6f99ac` — `T-LIGHT13`; второй — `T-LIGHT14`.
