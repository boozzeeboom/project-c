# Lighting Implementation Log — Project C

## Статус

- **Дата старта:** 2026-09-08
- **Рабочий источник правды:** `docs/world/lights/lighting-plan.md`
- **Завершённый этап:** Stage 1 / T-LIGHT02 — `Additional Lights: Per Pixel`
- **Текущий этап:** Stage 2 / T-LIGHT03 — LightingSettings и пилотный bake `WorldScene_0_0`
- **Статус этапа:** инфраструктура создана; bake заблокирован результатом `0 lightmaps`
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
| 7 | T-LIGHT08 | Day/Twilight/Night Volume tuning | ⏳ Не начат |

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
- Параметры каждого источника: `LightType.Point`, `Realtime`, intensity `2`, range `15`, color `#FFB070`, shadows `None`.
- Проверка сцены: 11 включённых Point Lights, каждый является дочерним своего housing.
- `check_compile_errors`: `No compile errors`.
- Сцена сохранена; bake не запускался.

**Ограничение:** визуальная проверка в Play Mode и screenshots не выполнялись автоматически; визуальный результат должен подтвердить пользователь.

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

## Следующий шаг

Пользователь должен проверить в Play Mode зону фонарей. Если тёплое локальное освещение видно и качество приемлемо, следующим техническим шагом остаётся расширение realtime-пилота на согласованные доковые зоны. T-LIGHT04 и массовый bake остаются заблокированными решением по GI.
