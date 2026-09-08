# Lighting Implementation Log — Project C

## Статус

- **Дата старта:** 2026-09-08
- **Рабочий источник правды:** `docs/world/lights/lighting-plan.md`
- **Завершённый этап:** Stage 1 / T-LIGHT02 — `Additional Lights: Per Pixel`
- **Следующий этап:** Stage 2 / T-LIGHT03 — LightingSettings и пилотный bake `WorldScene_0_0`
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
| 2 | T-LIGHT03 | LightingSettings + пилотный bake `WorldScene_0_0` | ⏳ Не начат |
| 3 | T-LIGHT04 | Light Probe Groups в пилотной сцене | ⏳ Не начат |
| 4 | T-LIGHT05 | Пилотные Point/Spot Lights | ⏳ Не начат |
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

Выполнить Stage 2 / T-LIGHT03: открыть `WorldScene_0_0`, создать/назначить `LightingSettings_World.asset`, проверить параметры bake и запечь только пилотную сцену. До визуальной приёмки пилота остальные 23 сцены не изменять.
