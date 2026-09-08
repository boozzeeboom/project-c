# Lighting Implementation Log — Project C

## Статус

- **Дата старта:** 2026-09-08
- **Рабочий источник правды:** `docs/world/lights/lighting-plan.md`
- **Текущий этап:** Stage 0 / T-LIGHT01 — аудит и утверждение плана
- **Следующий этап:** Stage 1 / T-LIGHT02 — `Additional Lights: Per Pixel`
- **Статус Unity Editor:** не подключён к MCP for Unity bridge на момент аудита

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
| 1 | T-LIGHT02 | Additional Lights → Per Pixel | ⏳ Ожидает подключения Unity Editor |
| 2 | T-LIGHT03 | LightingSettings + пилотный bake `WorldScene_0_0` | ⏳ Не начат |
| 3 | T-LIGHT04 | Light Probe Groups в пилотной сцене | ⏳ Не начат |
| 4 | T-LIGHT05 | Пилотные Point/Spot Lights | ⏳ Не начат |
| 5 | T-LIGHT06 | Reflection Probes | ⏳ Не начат |
| 6 | T-LIGHT07 | Emissive материалы | ⏳ Не начат |
| 7 | T-LIGHT08 | Day/Twilight/Night Volume tuning | ⏳ Не начат |

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

После подключения Unity Editor выполнить только Stage 1 / T-LIGHT02, сохранить изменение URP Asset, проверить импорт/компиляцию и зафиксировать отдельный baseline до перехода к LightingSettings.
