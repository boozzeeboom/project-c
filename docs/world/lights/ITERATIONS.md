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
**Коммит:** будет добавлен после фиксации этапа.

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
**Коммит:** будет добавлен после фиксации этапа.

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
