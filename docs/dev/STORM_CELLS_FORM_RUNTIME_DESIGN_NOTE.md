# Storm Cells Phase 2.4 — Design Note: Form + Runtime Tweaking

**Дата:** 2026-08-04
**Автор:** Mavis
**Статус:** План до правок кода
**Связанные тикеты:** T-CLOUD35…T-CLOUD37d (история), ITERATIONS.md 🔴

---

## 1. Проблема 1 — рантайм-твикинг не работает (ползунки не влияют)

### Симптом
`PushStormCellsToShader()` вызывается каждый кадр из `Update()`, в консоли раз в секунду виден лог с актуальными значениями. Но изменения ползунков в инспекторе во время Play Mode визуально не отражаются.

### Корневая причина (найдена)
**Material property shadowing глобалов.**

1. `VolumetricClouds.shader` объявляет ВСЕ `_Storm*` параметры в секции `Properties` (с `[HideInInspector]`), а не только в HLSLINCLUDE:
   ```
   [HideInInspector] _StormDensityMult ("Storm Density Mult", Float) = 2.0
   [HideInInspector] _StormNoiseScale ("Storm Noise Scale", Float) = 800
   ... и ещё 8
   ```
2. `VolumetricCloudsRenderFeature.GetOrCreateMaterial()` создаёт материал через `new Material(shader)` — **Unity копирует дефолты из Properties в материальные свойства** при создании.
3. При `DrawProcedural(material, ...)` приоритет значений: **material property > global**. `Shader.SetGlobalFloat("_StormDensityMult", x)` от `StormCellDirector` игнорируется, потому что материал держит своё значение (2.0 из Properties).
4. Доказательство от противного: `_NoiseTileSize`, `_LightAbsorption`, `_CloudOpacity`, `_CloudColorIntensity`, `_SunDirection` — их НЕТ в Properties, только в HLSLINCLUDE → `Shader.SetGlobal*` для них работает (облака реагируют на глобальные настройки фичи).
5. Шторм вообще виден только потому, что дефолты Properties (2.0 / 800 / 0.6 / …) близки к дефолтам директора (1.5 / 500 / 0.4 / …) — рендерится с материальными дефолтами, а не с твикнутыми значениями.

### Фикс
Удалить storm-секцию из `Properties` (10 свойств). Оставить только объявления в HLSLINCLUDE — они станут чистыми глобалами, как `_NoiseTileSize`. Материал перестанет их shadow'ить → твики директора дойдут до шейдера.

---

## 2. Проблема 2 — форма «гофрированная труба», а не кластеры

### Симптом
Столбы выглядят как цилиндры с мелкой периодической волнистостью («гофротруба»), а не как хаотичные грозовые кластеры.

### Корневая причина (найдена)
Три фактора:

**А. Масштаб cellular-шума на 1–2 порядка меньше радиуса ячейки.**
- `StormCellularFbm(cellularPos, _StormNoiseScale)` → `baseCellSize = _StormNoiseScale` (в сцене 500м, дефолт 800м).
- Текстура `CloudNoise3D` — 128³, канал A = `InvertedWorley(p, freq=8)` (см. BakeCloudNoise.compute) → **8 Worley-ячеек на тайл**.
- Worley-долька = cellSize / 8 = 500 / 8 = **62.5м**.
- При CellRadius 1000–5000м (диаметр 2000–10000м) → **30–160 долек на диаметр** — мелкая «капуста», а не кластеры.

**Б. InvertedWorley в среднем ~0.65, а порог — 0.5.**
- `smoothstep(0.5 − contrast, 0.5 + contrast, cellular)` с contrast=0.25 → band 0.25…0.75.
- InvertedWorley почти везде > 0.65 → `shape ≈ 1` на большей части объёма → **сплошной цилиндр**; cellular виден только как мелкая рябь на границах Worley-ячеек = «гофра».

**В. Envelope доминирует над формой.**
- `envelope = 1.0 − smoothstep(radius*0.7, radius*1.5, distXZ)` → до 70% радиуса envelope = 1 (сплошная заливка). Cellular-форма не может «рвать» тело цилиндра.

Дополнительно:
- **Г**: warpStrength = `radius * _StormNoiseStrength * 0.5` — привязан к радиусу, а не к масштабу долек; при смене радиуса поведение скачет.
- **Д**: вертикальный профиль однородный (`smoothstep` на 5% высоты), параметр `_StormVerticalPeak` вообще не используется в шейдере.
- **Е**: у всех ячеек один и тот же паттерн шума (нет per-cell seed) — все столбы выглядят одинаково.

### Фикс (переписать StormDensity)
1. **cellSize авто-масштабируется с радиусом**: `cellSize = max(radius * 2.8, _StormNoiseScale * 4.0)` → ~3–5 Worley-долек на диаметр ячейки (долька ≈ 0.35×radius). При radius=1000 → cellSize≈2800м → дольки ~350м.
2. **Порог выше среднего InvertedWorley**: `threshold = 0.5 + contrast*0.3`, band = `contrast*0.5` → дольки раздельные, с рваными краями.
3. **Envelope — только safety-клип**: `smoothstep(radius*0.8, radius*1.4, distXZ)`; все дешёвые gate'ы (envelope → vEnvelope → vProfile) — ДО сэмпла шума (перф: Worley-сэмпл только внутри ячейки).
4. **Per-cell seed offset**: `float3(frac(i*137.3), frac(i*57.1), frac(i*91.7)) * 1000` → разные формы у разных ячеек.
5. **Fine-октава (cauliflower)** на `cellSize*0.35` — текстура поверхности долек: `inner = 0.6 + 0.4*smoothstep(0.3,0.7,fine)` — ядро дольки плотное, края рваные.
6. **Асимметричный вертикальный профиль** через `_StormVerticalPeak`: пик плотности на выбранной высоте, резкий спад выше пика (наковальня), плавный ниже. `vProfile = 1 − |h01 − peak| / (h01 > peak ? 1−peak : peak)`.
7. **warpStrength = dollySize * _StormNoiseStrength** (dollySize ≈ 0.35×radius) — стабилен, не зависит от радиуса напрямую.

---

## 3. Файлы

| Файл | Действие |
|---|---|
| `Assets/_Project/Shaders/Clouds/VolumetricClouds.shader` | Удалить storm-секцию из Properties; переписать `StormDensity` |
| `Assets/_Project/Scripts/World/Clouds/StormCellDirector.cs` | Без изменений логики (PushStormCellsToShader уже корректен) |
| `docs/world/CLOUD_system/3.0/ITERATIONS.md` | Добавить итерацию с фиксами |
| `docs/world/CLOUD_system/3.0/STORM_FORM_RUNTIME_INVESTIGATION.md` | Новый — полный разбор |

## 4. Верификация

1. `refresh_unity` (force, compile=request, wait_for_ready=true) → `read_console` — 0 errors.
2. Play Mode (Start Host): в инспекторе StormDirector изменить `StormNoiseScale` / `StormClusterContrast` / `StormDensityMultiplier` — форма должна меняться в реальном времени.
3. Визуально: столбы должны стать кластерами долек (~3–5 на диаметр), с рваными краями, разные у разных ячеек, сужение кверху.
4. `StormNoiseScale` теперь = «размер дольки в метрах» (через cellSize ≈ scale*4 при малых радиусах) — твикать осмысленно.
