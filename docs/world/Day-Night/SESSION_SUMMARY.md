# Day-Night Cycle — Session Summary

## Analysis Complete ✅

### Old Integration Status

| Component | Status |
|-----------|--------|
| `CloudSystem.cs` `enableDayNightCycle` | **Already disabled** (`= 0` in prefab). Old sun/color logic at lines 173–246 will NOT run. No action needed. |
| `VeilRaymarchMesh.shader` line 295 | **Hardcoded `_LightDir`** — needs fix: replace `normalize(half3(-0.5, 0.5, -0.3))` with `normalize(_LightDir.xyz)`. This is the ONLY hardcoded light direction in any shader. |
| `VeilRaymarchMeshController.cs` | Does NOT push `_LightDir` to shader — needs `SetLightDir()` method added. |

### What's Active Now

| System | Status |
|--------|--------|
| `CloudManager` + `NearCloudRenderer` | Active (cloud geometry, NOT day-night lighting) |
| `ServerWeatherController` | Active (wind only) |
| `WindManager` | Active (singleton) |
| `VeilRaymarchMesh` | Active (poison fog) — but `_DayFactor = 0.5` static, not driven |
| Directional "Sun" light | **Missing from scene** — created only via `ProjectCSceneSetup.cs` tool |

### What's NOT Active

- No day-night lighting (sun/moon/ambient/skybox)
- No TimeOfDay synchronization
- No temperature-based post-filter
- No moon mesh
- No constellations

---

## Requirements Locked

1. **Moon mesh** — physical moon geometry in sky
2. **Constellations** — named star patterns for navigation (procedural)
3. **Time cycle speed** — server-configurable, default 24h / 30s
4. **Storm → darker lighting** — storms affect ambient/fog/sun intensity

---

## Documentation Created

| Doc | Path |
|-----|------|
| Main spec | `docs/world/Day-Night/SPEC.md` |
| Implementation plan | `docs/world/Day-Night/PLAN.md` |
| Requirements | `docs/world/Day-Night/REQUIREMENTS.md` |
| Shader fix notes | `docs/world/Day-Night/SHADER_FIX.md` |

---

## Implementation Order (Next Session)

### Step 1: Infrastructure
- [ ] Create folder `Scripts/Core/DayNight/`
- [ ] Create `TimeOfDayPhase.cs` (ScriptableObject)
- [ ] Create `DayNightProfile.cs` (ScriptableObject)
- [ ] Create `TemperatureFilterConfig.cs` (ScriptableObject)
- [ ] Create `TemperatureFilter.cs` (component)

### Step 2: Server Authority
- [ ] Extend `ServerWeatherController` with TOD + temperature fields + RPCs
- [ ] Add `SetTimeOfDayServerRpc` + `SetTemperatureServerRpc` handlers
- [ ] Add `BroadcastTimeOfDayClientRpc` + `BroadcastTemperatureClientRpc`

### Step 3: Client Lighting Controller
- [ ] Create `DayNightController.cs`
- [ ] Phase detection + interpolation with variability
- [ ] Sun directional light control
- [ ] Ambient light control
- [ ] Fog control
- [ ] URP Volume blend (day/night profiles)

### Step 4: Skybox + Moon + Stars
- [ ] Create `Skybox_Day.mat` + `Skybox_Night.mat`
- [ ] Create `MoonController.cs` (moon mesh + orbit)
- [ ] Create `ConstellationData.cs` (SO with star patterns)
- [ ] Create `ConstellationController.cs` (render stars + lines)
- [ ] Create `Stars.shader` (point stars with twinkle)

### Step 5: Shader Integration
- [ ] Fix `VeilRaymarchMesh.shader` line 295: use `_LightDir` uniform
- [ ] Add `SetDayNight()` to `VeilRaymarchMeshController`
- [ ] Call `SetDayNight()` from `DayNightController`

### Step 6: Storm Integration
- [ ] Add `OnStormIntensityChanged` event to `ServerStormManager`
- [ ] Subscribe `DayNightController` to storm intensity
- [ ] Apply darker ambient + denser fog during storms

### Step 7: Verification
- [ ] Verify server→client TOD sync
- [ ] Verify all 5 phases visually
- [ ] Verify temperature filter at thresholds
- [ ] Verify constellation visibility by phase
- [ ] Performance profiling

---

## Open Questions (Action Needed)

| # | Question |
|---|----------|
| 1 | **Lunar phases?** — Full cycle (new→full→new) or always full moon? |
| 2 | **Star click interaction?** — Players click stars for navigation info? |
| 3 | **Zone-based eternal day/night** — Separate biome zones with permanent twilight etc? |
| 4 | **How many constellations?** — 5 minimum, recommend 8-12 |

---

## Session 2026-05-29 — Constellation Debug

### Что сделано:

1. **ConstellationController.cs** — переработана архитектура:
   - `[ExecuteInEditMode]` — работает в Edit Mode
   - `OnEnable()` вместо `Start()` — инициализация сразу
   - Sky Dome подход — звезды на сфере вокруг камеры
   - Pre-allocated buffers — zero allocations
   - `_starVisibility = 1f` — принудительная видимость для теста

2. **ConstellationData.cs** — ScriptableObject для данных созвездий:
   - `ConstellationData` — SO с массивом созвездий
   - `Constellation` — имя + звезды + линии
   - `StarData` — имя + позиция (azimuth, altitude) + магнитуда

3. **Editor/StarFieldTest.cs** — тестовый скрипт для Edit Mode

### Логи (Play Mode):
```
[ConstellationController] OnEnable - Initializing sky dome star system...
[ConstellationController] Building sky dome with 9 stars from 1 constellations
[ConstellationController] BuildSkyDome: START
[ConstellationController] BuildSkyDome: Created GameObject, parent=null
[ConstellationController] BuildSkyDome: Set position to (239997.00, 3100.00, 159998.00)
[ConstellationController] Built sky dome with 9 stars, 36 vertices
[ConstellationController] Using assigned star material
[ConstellationController] Created 9 constellation lines
[ConstellationController] Time=19,73318 Visibility=0,1088187
```

### Проблема — ЗВЕЗДЫ НЕ ВИДНЫ:

- Visibility вычисляется ~0.1 (не 1.0 как должно быть)
- Mesh создается (36 vertices = 9 stars × 4 vertices)
- Линии создаются (9 lines)
- Позиция SkyDome: (239997, 3100, 159998) — далеко от камеры
- Материал назначается

### Возможные причины:
1. **Triangle winding** — quads созданы для outside rendering, но смотрим изнутри
2. **Layer** — SkyDome на "Ignore Raycast", но это не влияет на rendering
3. **Z-fighting** — слишком близко/далеко от камеры
4. **Material** — используется Unlit/Transparent, но alpha может не работать
5. **Skybox override** — другой rendering pipeline

### Что нужно проверить:
- [ ] Переключить на `MeshTopology.Quads` или исправить triangle order
- [ ] Добавить debug sphere чтобы визуально видеть SkyDome
- [ ] Проверить material rendering queue
- [ ] Проверить z-buffer depth
- [ ] Добавить визуальный маркер на позиции звезд

### Следующие шаги:
1. Добавить Debug.DrawRay для визуализации позиций звезд
2. Попробовать Point cloud вместо Quad mesh
3. Проверить Scene view — видны ли звезды там?
4. Добавить простой sphere на позицию SkyDome для ориентира

---

## Дополнительные изменения 2026-05-29 (продолжение):

### Исправления в ConstellationController.cs:

1. **Visibility всегда 1.0** — убран вызов `CalculateStarVisibility()`, теперь `_starVisibility = 1f` напрямую в Update()

2. **Gizmos для отладки** — добавлен `OnDrawGizmosSelected()`:
   - Желтая сфера — центр SkyDome
   - Белые сферы — позиции звезд
   - Голубая линия — от камеры к SkyDome

3. **CacheStarPositions()** — кэширование позиций звезд для Gizmos

4. **Расширенный debug лог** — теперь показывает Distance, MeshActive, Layer

### Что проверить в Unity:
1. В Scene View выбрать ConstellationController — должны появиться Gizmos
2. В Game View должны быть видны звезды
3. Лог должен показывать Visibility=1.0
