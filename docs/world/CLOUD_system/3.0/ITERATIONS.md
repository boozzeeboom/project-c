# Итерации реализации Cloud System 3.0

---

## Итерация от 2026-08-04 (Phase 2.4 — Deep Form & Runtime Fix, T-CLOUD39) 🟢

**Коммит:** `fbc91eea` — T-CLOUD39: procedural storm form fix + runtime save/load + anti-banding

**Задача:** Глубинный фикс формы («гофротруба» → кластеры) + кнопка регенерации для рантайм-твикинга.

**Корневые причины (полный разбор: `STORM_FORM_RUNTIME_INVESTIGATION.md`, раздел «Проблема 2 — ГЛУБИННЫЙ АНАЛИЗ»):**

T-CLOUD38 исправил масштаб долек (cellSize = max(radius×2.8, ...)), но **НЕ устранил корень «гофротрубы»**:

1. **Периодическая решётка Worley-фич** — `frac(uvw)` на pre-baked 128³ текстуре + `InvertedWorley(freq=8, period=128)` создавал детерминированную решётку из 8 фич на тайл. Решётка идеально повторялась каждые `cellSize` метров. Domain warp (тоже из той же текстуры!) лишь слегка искажал решётку, но не ломал её.
2. **Pre-baked текстура принципиально не пригодна** для organic-кластеров из-за `period=128` → позиции фич зафиксированы хешем от индекса ячейки mod 128.

**Решение T-CLOUD39:** Полный отказ от pre-baked текстуры для штормов. Замена на **procedural `abs(Perlin3D)` FBM** с нецелочисленной lacunarity (2.3):
- `abs(Perlin)` создаёт organические биллоу-«пузыри» (естественная форма облачных кластеров)
- Нецелочисленная lacunarity (2.3) → гармоники никогда не выравниваются → хаотичная форма
- Нет `frac()`, нет периодичности, нет решётки
- Perlin дешевле Worley: 8 сэмплов/октава vs 27 сэмплов/октава

**Изменения:**
- `VolumetricClouds.shader`:
  - Удалены: `StormSampleNoise`, `StormCellularFbm`, `StormDomainWarp` (texture-based)
  - Добавлены: `StormBillow` (abs-Perlin), `StormBillowFbm` (multi-octave), `StormWarp3D` (procedural Perlin warp)
  - `StormDensity` переписан: procedural abs(Perlin) FBM с clusterScale, warp, contrast threshold, fine cauliflower
- `StormCellDirector.cs`:
  - `ForceRegenerateStorm()` — public метод с `[ContextMenu("Force Regenerate Storm")]`
  - Авто-респавн тестовых ячеек если список пуст
- `Editor/StormCellDirectorEditor.cs` — **новый.** Кастомный инспектор с кнопками:
  - «⟳ Regenerate Storm» — пушит все параметры в шейдер
  - «⛈ Respawn Test Cells» — пересоздаёт тестовые ячейки
  - Авто-реген при изменении параметров в Play Mode

**Статус:** 🟢 Реализовано и проверено (0 compile errors).

**Дополнительные изменения в ходе итерации:**
- `CloudNoise.hlsl`: фикс `Hash3Periodic` — `max(period, 1u)` для D3D11 FXC (divide-by-zero)
- `VolumetricClouds.shader`:
  - Порог contrast threshold опущен с 0.35 до 0.25 (меньше дырявости при 1 октаве)
  - Вертикальный профиль модулируется 2 октавами Perlin-шума — ломает горизонтальную «этажность»
  - Envelope смягчён: `smoothstep(0.6R, 1.6R)` вместо `(0.8R, 1.5R)` — менее заметная граница цилиндра
- `StormCellDirector.cs`:
  - `SaveToEditorPrefs()` / `LoadFromEditorPrefs()` — сохранение/загрузка через EditorPrefs
  - `Awake()` автоматически загружает сохранённые дефолты
  - `StormVerticalPeak` рандомизируется (0.3–0.7) при каждом спавне тестовых ячеек
- `Editor/StormCellDirectorEditor.cs`:
  - Кнопки: Regenerate Storm, Respawn Test Cells, Save as Defaults, Apply to Scene
  - Индикатор ✅/❌ наличия сохранённых дефолтов
  - Apply to Scene — запись значений в сцену для персистентности в Edit Mode

---

## Итерация от 2026-08-04 (Phase 2.4 — Form & Runtime Fix, T-CLOUD38) 🟡

**Задача:** Починить рантайм-твикинг и форму штормовых ячеек.

**Корневые причины:** material property shadowing + масштаб cellular-шума.

**Изменения:** Удалены `_Storm*` из `Properties` шейдера; переписан `StormDensity` с авто-масштабированием cellSize.

**Статус:** 🟡 Частичное решение. Твикинг починен (properties → globals), но форма осталась гофротрубой из-за периодической решётки pre-baked текстуры. Полный фикс в T-CLOUD39.

---

## Итерация от 2026-08-04 (Phase 2.4 — Storm Cloud Rendering) 🔴

**Задача:** Визуализировать штормовые ячейки.

**Итоговый статус:** 🔴 Исправлено в T-CLOUD39.

---

### Коммиты (хронологически)

| Коммит | Метка | Суть |
|---|---|---|
| `bcd64790` | T-CLOUD35 | Базовая StormDensity(): radial falloff + vertical profile → cylinder |
| `186f91cc` | T-CLOUD36 | Noise-модуляция радиуса (Perlin FBM) → «гофрированная труба» |
| `18d422f0` | T-CLOUD37 | Cellular FBM + domain warp (InvertedWorley) — попытка cauliflower |
| `3e7dbbd2` | T-CLOUD37b | Фикс noise scale + StormClusterContrast |
| `d3aab97f` | T-CLOUD37c | PushStormCellsToShader() каждый кадр, дефолты агрессивнее |
| `e9c67277` | T-CLOUD37d | Cellular IS the shape — не текстура внутри цилиндра |

---

### Что сделано

**C# — StormCellDirector.cs:**
- `PushStormCellsToShader()` — пакует до 8 ячеек в `Vector4[8]×2`, пушит через `Shader.SetGlobal*`
- Параметры в инспекторе (секции «Storm Cloud Shader» + «Storm Noise»):
  - `StormDensityMultiplier` (0.1–10, default 2.0) — общая плотность
  - `StormColorDark` / `StormColorLight` — цвет ядра и края
  - `StormEdgeSoftness` (0.01–0.5) — мягкость края конверта
  - `StormVerticalPeak` (0.1–0.9) — пик плотности по вертикали
  - `MaxStormCellsInShader` (1–8)
  - `StormNoiseScale` (50–5000, default 800) — размер cellular-долек (м)
  - `StormNoiseStrength` (0–1, default 0.6) — сила domain warp
  - `StormNoiseOctaves` (1–3, default 2)
  - `StormNoiseSpeed` (0–0.5) — эволюция от ветра
  - `StormClusterContrast` (0.1–0.5, default 0.25) — резкость cellular-кластеров
- Вызов `PushStormCellsToShader()` — каждый кадр в `Update()`, включая кадры без ячеек
- Дефолтный спавн тестовых ячеек через 2 секунды

**Шейдер — VolumetricClouds.shader:**
- `StormSampleNoise(pos, cellSize)` — сэмплинг CloudNoise3D в world-space
- `StormCellularFbm(pos, baseCellSize)` — fractal inverted-Worley FBM (канал A), multi-octave
- `StormDomainWarp(pos, cellSize, strength)` — 2D noise offset для разбивки симметрии
- `StormDensity(worldPos, out stormColor)` — основной entry point, инжектится в `CloudDensity()`
- Uniform-массивы: `_StormCellPos[8]`, `_StormCellParams[8]`, `_StormCellCount`
- Визуальные uniform'ы: `_StormDensityMult`, `_StormColorDark/Light`, `_StormEdgeSoftness`, `_StormVerticalPeak`, `_StormNoiseScale/Strength/Octaves/Speed`, `_StormClusterContrast`

**Pipeline (текущий, T-CLOUD37d):**
```
CellularFbm(warpedPos) → smoothstep(0.5±contrast) → shape (0/1 — cellular = граница)
                              ×
envelope(70%→150% радиуса)  → дальний safety-клип
                              ×
vEnvelope                    → высотный диапазон
                              =
                          density
```

---

### 🔴 Известные проблемы

**1. Визуал — «гофрированные трубы», не organic-кластеры**
- Несмотря на cellular FBM + domain warp, форма остаётся близкой к цилиндру
- Причины (гипотезы):
  - CloudNoise3D (32³ текстура) имеет слишком мало cellular-фич для 5км радиуса
  - Тайлинг шума на масштабе 800м создаёт видимые повторения вместо organic-структуры
  - Envelope (70%→150% радиуса) всё ещё доминирует над cellular-формой
  - Возможно, математически неверный подход к cellular FBM — нужно пересмотреть

**2. Рантайм-твикинг не работает**
- `PushStormCellsToShader()` вызывается каждый кадр и пушит все параметры
- В консоли виден дебаг-лог раз в секунду с актуальными значениями
- **НО:** изменения ползунков в инспекторе во время Play Mode визуально не отражаются
- Гипотеза: `Shader.SetGlobal*` перезаписывается где-то ещё, или шейдер кеширует значения
- Нужен инструмент верификации: дебаг-режим шейдера (вывод cellular-плотности как цвет)

---

### Next steps (предложения)

1. **Шейдерный дебаг-режим** — keyword `_STORM_DEBUG_CELLULAR`, выводящий raw cellular density как grayscale. Это позволит понять, вычисляется ли cellular вообще и как он выглядит.
2. **Пересмотр cellular-подхода** — возможно, нужен не InvertedWorley из 32³ текстуры, а собственная генерация cellular/Worley на лету (больше фич, меньше тайлинг).
3. **Альтернативный подход** — 3D SDF из нескольких overlapping сфер/эллипсоидов с noise-деформацией. Гарантированно даёт organic-кластеры.
4. **Фикс рантайм-твикинга** — найти кто перезаписывает глобалы или почему шейдер их не видит.

---

## Итерация от 2026-08-04 (Debug Positioning — завершено) ✅

**Задача:** Разобраться почему штормовые ячейки не видны. Настроить дебаг-визуализацию.

**Коммиты:**
- `ea7b07f` — T-CLOUD28: `Camera.main` → `FindGameObjectWithTag("Player")`, задержка 2→15 сек
- `243d8a8` — T-CLOUD29: маркеры-столбы 200×4200×200 вместо кубов
- `a46513b` — T-CLOUD30: все параметры маркеров/ветра в инспектор
- `00892c7` — T-CLOUD31: CellRadius до 50000, MarkerWidth=0→авто
- `e139e49` — T-CLOUD32: scale маркеров live-update
- `b28e40c` — T-CLOUD32b: сброс сериализованного MarkerWidth в сцене
- `8a38ea7` — T-CLOUD33: фиксированный MarkerWidth=500, без авто-привязки

**Результат:**
- Ячейки видны (розовые столбы в Game View, цилиндры/гизмо в Scene View)
- Все параметры управляются из инспектора
- Документация: `DEBUG_POSITIONING_INVESTIGATION.md`

**Статус:** ✅ Дебаг-позиционирование завершено.

---

## Итерация от 2026-08-04 (Phase 2.4 — начало) ✅

**Задача:** Phase 2.4 — VFX молнии в грозовых ячейках. Проектирование с нуля.

**Коммит:** `b67c3ce6` — T-CLOUD12: StormCellDirector + переписан StormLightningVfx

**Изменения:**
- `StormCellDirector.cs` — новый. Управление штормовыми ячейками.
- `StormLightningVfx.cs` — переписан. Отвязан от StormController.
- `PHASE_2_4_STORM_LIGHTNING_PLAN.md` — план реализации.

**Статус:** ✅ C# готов, сцена собрана, молнии работают.
