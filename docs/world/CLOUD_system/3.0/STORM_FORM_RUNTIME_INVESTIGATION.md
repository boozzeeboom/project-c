# Storm System 3.0 — Form & Runtime Tweaking Investigation

**Date:** 2026-08-04 (updated)
**Status:** 🔴 Проблемы подтверждены. Фиксы: T-CLOUD38 (частично) + T-CLOUD39 (глубинный фикс формы)
**Связанные тикеты:** T-CLOUD35 → T-CLOUD38, T-CLOUD39

---

## TL;DR

| Проблема | Истинная корневая причина | Фикс |
|---|---|---|
| 1. Рантайм-твикинг не работает | Две причины: (а) `_Storm*` были в `Properties` → material shadowing (исправлено T-CLOUD38), (б) нет ручного триггера для перегенерации при структурных изменениях (добавлено T-CLOUD39) | Кнопка «Regenerate Storm» в инспекторе + ContextMenu |
| 2. Форма «гофротруба» | **Фундаментальная:** `frac(uvw)` на pre-baked 128³ текстуре создаёт периодическую решётку Worley-фич (8 шт. на тайл, ровно повторяются). Даже при domain warp и multi-octave — решётка доминирует. T-CLOUD38 улучшил масштабирование но не устранил корень. | Замена texture-based cellular на **procedural `abs(Perlin3D)` FBM** — непериодический, organический, без тайлинга |

---

## Проблема 1 — рантайм-твикинг не работает (ПОВТОРНЫЙ АНАЛИЗ)

### Статус T-CLOUD38
`_Storm*` удалены из `Properties` шейдера — это правильно. `Shader.SetGlobal*` от `StormCellDirector` больше не shadow'ятся материалом.

### Почему всё ещё может не работать

**Гипотеза A (наиболее вероятная): шейдер/material кеширован**
- `VolumetricCloudsRenderFeature.GetOrCreateMaterial()` создаёт `new Material(shader)` с `HideFlags.HideAndDontSave`
- При перекомпиляции шейдера старый материал может сохранять ссылку на старую версию шейдера
- Решение: кнопка принудительного обновления

**Гипотеза B: параметры меняются, но эффект незаметен**
- При текущей математике формы даже сильные изменения `_StormNoiseScale` или `_StormClusterContrast` могут быть визуально незаметны из-за доминирования envelope (проблема 2)
- Решение: после фикса формы твикинг станет заметнее

**Гипотеза C: StormCellDirector.Update() не выполняется**
- Если GameObject отключён, скрипт не пушит глобалы
- Решение: кнопка форсирует пуш даже при выключенном Update

### Фикс (T-CLOUD39)
1. `[ContextMenu("Force Regenerate Storm")]` на `PushStormCellsToShader()` + респавн тестовых ячеек
2. Кастомный Editor с кнопками «Regenerate Storm» и «Respawn Test Cells»
3. Авто-реген при изменении параметров в Editor (OnValidate для edit-mode, кнопка для play-mode)

---

## Проблема 2 — форма «гофрированная труба» (ГЛУБИННЫЙ АНАЛИЗ)

### Почему T-CLOUD38 не решил проблему до конца

T-CLOUD38 исправил **масштаб** (`cellSize = max(radius*2.8, ...)` — дольки стали крупнее), но не устранил **корень**:

### Корень: периодический `frac(uvw)` + pre-baked Worley-решётка

```
StormSampleNoise(pos, cellSize):
    uvw = pos / cellSize
    uvw = frac(uvw)              ← ОБОРАЧИВАЕТ координаты → ПЕРИОДИЧЕСКИЙ паттерн!
    return texture[uvw]
```

`BakeCloudNoise.compute` запекает `InvertedWorley(p, freq=8, period=128)` в канал A:
- 8 Worley-фич на 128³ текселей
- `period=128` → фичи расставлены **детерминированно по решётке** внутри каждого периода
- `frac(uvw)` → решётка повторяется каждые `cellSize` метров

**Визуальный результат:** цилиндр, «нарезанный» на одинаковые повторяющиеся дольки. Domain warp (тоже из той же текстуры!) лишь слегка искажает решётку, но не ломает её.

### Почему InvertedWorley из текстуры не бывает «organic»

Worley-фичи в pre-baked текстуре размещены через `Hash3Periodic(cell, seed, period)`:
```hlsl
uint3 cell = uint3(cx, cy, cz);
uint h = Hash3Periodic(cell, seed, 128);  // индекс ячейки MOD 128
sx = (h % 1000) / 1000.0;                 // позиция фичи внутри ячейки
```

Для данного `cellIndex mod 128` хеш ВСЕГДА возвращает одно и то же → позиция фичи ВСЕГДА одна и та же. При `frac(uvw)` world-space координата оборачивается → **каждые `cellSize` метров паттерн идеально повторяется**.

Multi-octave FBM (октавы с `freq×2`) берёт тот же периодический паттерн на других масштабах → решётка остаётся решёткой, просто с наложением гармоник.

### Решение: procedural `abs(Perlin3D)` FBM вместо texture-based Worley

**Ключевая идея:** использовать аналитические (procedural) шумовые функции из `CloudNoise.hlsl` напрямую, БЕЗ pre-baked текстуры:
- `Perlin3D_noPeriod(pos, seed)` — непериодический, organический шум
- `abs(Perlin)` — создаёт «турбулентные» биллоу-формы (как облака)
- Multi-octave с нецелочисленной lacunarity (2.3 вместо 2.0) — ломает выравнивание гармоник

**Почему `abs(Perlin)` а не Worley:**
- `abs(Perlin)` создаёт выпуклые «пузыри» там где Perlin > 0, и впадины где < 0
- В отличие от Worley (дискретные точки-фичи → расстояние → сглаженные шары), `abs(Perlin)` даёт естественные organические формы
- Вычислительно дешевле: 8 сэмплов на октаву vs 27 для Worley
- Не имеет артефактов решётки

### Новая математика StormDensity

```
Per cell (ранние gate'ы ДО шума):
  1. XZ envelope (0.8R…1.5R) — safety clip
  2. Vertical gate (bottomY…topY)
  3. Vertical profile — асимметричная наковальня через _StormVerticalPeak

  → ЕСЛИ прошли gate'ы:

  4. Procedural domain warp (Perlin × 3) — ломает радиальную симметрию
  5. Procedural abs(Perlin) FBM — organические биллоу-кластеры
     - baseScale: радиус-независимый размер кластеров
     - lacunarity 2.3 → гармоники не выравниваются
     - per-cell seed → разные формы у разных ячеек
  6. Fine cauliflower pass — ещё одна октава мелкого abs(Perlin)
     - erodes edges, оставляет ядро плотным
  7. Contrast threshold (smoothstep) — резкость границ долек

  = shape × envelope × vEnvelope × vProfile × intensity × _StormDensityMult
```

### Семантика параметров после T-CLOUD39

| Параметр | Было (T-CLOUD38) | Стало (T-CLOUD39) |
|---|---|---|
| `_StormNoiseScale` | Влиял на cellSize → косвенно на дольки | **Прямой** размер кластеров в метрах (200-1500м) |
| `_StormNoiseStrength` | Сила warp через текстуру | Сила **procedural** warp в метрах (0-1000м) |
| `_StormNoiseOctaves` | Октавы texture-based Worley | Октавы procedural abs(Perlin) FBM |
| `_StormClusterContrast` | Порог для Worley-долек | Порог для Perlin-биллоу (0.1=мыльно, 0.5=резко) |

### Ожидаемый визуальный результат
- Хаотичные organические кластеры разной формы у разных ячеек
- Отсутствие периодичности/повторяемости
- «Пузыри» и «карманы» разного размера
- Сужение кверху (наковальня)
- Рваные края (warp + contrast threshold)

---

## Файлы

| Файл | Изменение |
|---|---|
| `Assets/_Project/Shaders/Clouds/VolumetricClouds.shader` | Переписан `StormDensity`: procedural Perlin FBM вместо texture-based Worley |
| `Assets/_Project/Scripts/World/Clouds/StormCellDirector.cs` | Добавлен `[ContextMenu]` для регенерации |
| `Assets/_Project/Scripts/World/Clouds/Editor/StormCellDirectorEditor.cs` | **Новый.** Кастомный инспектор с кнопками |
| `docs/world/CLOUD_system/3.0/STORM_FORM_RUNTIME_INVESTIGATION.md` | Этот документ |
| `docs/world/CLOUD_system/3.0/ITERATIONS.md` | Будет обновлён после верификации |

---

## Верификация

1. **Компиляция:** 0 errors
2. **Play Mode (Start Host):** нажать «Regenerate Storm» в инспекторе → облака перегенерируются
3. **Твикинг:** менять `StormNoiseScale` (200–1500), `StormClusterContrast` (0.1–0.5), `StormNoiseStrength` (0–1) → форма меняется мгновенно
4. **Форма:** organические кластеры, не цилиндры, не гофротруба; разные у разных ячеек
5. **Перф:** procedural Perlin (8 сэмплов/октава) дешевле чем Worley (27 сэмплов/октава), gate'ы до шума сохраняются
