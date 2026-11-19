# CLOUD_system 3.0 — Итоговый статус

**Дата:** 2026-11-20
**Версия:** 3.0 → 4.0 (поэтапная интеграция cloudscape)
**Статус:** 🟡 T-CLOUD50 (фикс мерцания displacement ~3 Гц) реализован и ожидает визуальной приёмки; T-CLOUD47/48/49 приняты пользователем («остальное принимаем пока»); T-CLOUD46 принят; T-FO09T ожидает F8/F9; GPU-замеры отложены по решению пользователя

---

## Архитектура (кратко)

Один volumetric raymarch-рендерер (`VolumetricClouds.shader` + `RenderFeature`) на весь мир.
4 слоя облаков с цветовыми рампами день/закат + штормовые ячейки + интерактивный displacement.

```
CloudDensity(worldPos) =
    Σ(layer × HeightProfile × LayerNoiseShape × CoverageNoise × ColorRamp)
    (реализация в коде: GhibliRamp)
  - LocalDensity (корабельный след)
  + StormDensity(originalPos)  ← шторм immune к displacement
```

---

## Фаза 1 — Визуальное ядро ✅ ЗАВЕРШЕНО

| Подфаза | Суть | Статус |
|---|---|---|
| 1.1 | HLSL-порт CloudMath.cs → `CloudNoise.hlsl`, `CloudCommon.hlsl` | ✅ |
| 1.2 | Бейк 128³ 3D-текстуры (`CloudNoise3D.asset`) | ✅ |
| 1.3 | `VolumetricCloudsRenderFeature` + `VolumetricClouds.shader` | ✅ |
| 1.4 | Height profile, coverage, wind (`_WindOffset` из WindManager) | ✅ |
| 1.5 | Light marching (6 шагов), HG, multi-scatter, цветовые рампы | ✅ |
| 1.6 | Half-res, depth-aware upscale, temporal reprojection | 🟡 Half-res и Composite (T-CLOUD47) и temporal History с neighborhood clamp (T-CLOUD48) реализованы; приёмка и F8/F9 NOT RUN |
| 1.7 | `CloudPerfMonitor` | 🟡 Инструмент есть; подтверждённых GPU-замеров cloud-pass нет |
| v4.1 | T-CLOUD43: dual-HG, 3-член Beer–Lambert, powder, crease | ✅ Пользователь: «визуально лучше»; F8/F9 не подтверждены |
| v4.2 | T-CLOUD44: coverage-дом, перистый низ, разделение шума слоёв | 🟡 Код и импорт готовы; визуальная приёмка NOT RUN |
| v4.3 | T-CLOUD45: mid→fine эрозия, early-out, remap края | 🟡 Компиляция 0 errors; визуальная приёмка и F8/F9 NOT RUN |
| v4.4 | T-CLOUD46: mip-chain, footprint LOD, статический IGN | ✅ Пользователь: «оставляем»; F8/F9 не подтверждены |
| v4.5 | T-CLOUD47: half-res RGBA16F + depth-aware Composite | 🟡 Компиляция и шейдер 0 errors; приёмка, F8/F9 и GPU-замеры NOT RUN |
| v4.6 | T-CLOUD48: temporal History — ping-pong, репроекция prevVP, 3×3 clamp, сброс при ребейзе/телепорте | 🟡 Компиляция и шейдер 0 errors; приёмка, F8/F9 и GPU-замеры NOT RUN |
| v4.7 | T-CLOUD49: sun-space карта пропускания, self-shadowing слоёв, лучи; FO-safe (пересчёт каждый кадр) | 🟡 Компиляция и шейдер 0 errors; приёмка, F8/F9 и GPU-замеры NOT RUN |
| v4.8 | T-CLOUD50: displacement — компенсация CenterDelta + trilinear advection, сброс буфера при ребейзе | 🟡 Компиляция 0 errors; приёмка и F8/F9 NOT RUN |
| FO | T-FO09T: authored→runtime высоты облаков/штормов на сервере и клиенте | 🟡 Компиляция 0 errors; F8/F9 NOT RUN |

**Итог:** 4-слойный реймарч работает. Утверждение о приемлемом перформансе снято до отдельных GPU-замеров. Переключатели v4 и `HalfResRender` позволяют независимо сравнивать этапы. Последнее наблюденное состояние в Unity: `LightingV4Strength = 0.70`, `ShapeV4Strength = 0.21`, `DetailV4Strength = 1.00`, `StableV4Strength = 1.00`, `HalfResRender = true`, `DepthAwareUpscale = true`, `DepthUpscaleSigma = 0.03`, `TemporalReprojection = true`, `TemporalBlend = 0.85`, `TemporalTeleportResetDistance = 100`, `CloudShadowsV4 = true`, `ShadowMapResolution = 1024`, `ShadowAreaSize = 8192`, `ShadowMarchSteps = 48`, `ShadowV4Strength = 0.65`. Temporal реализован (T-CLOUD48): сброс истории при ребейзе (F8/F9/авто), телепорте камеры >100 м и смене размера history-RT. Тени реализованы (T-CLOUD49): sun-space карта пересчитывается каждый кадр, истории нет; геометрия мира не затемняется.

---

## Фаза 2 — Интерактивность ✅ ЗАВЕРШЕНО

| Подфаза | Суть | Статус |
|---|---|---|
| 2.1 | `LocalDensityBuffer` — 96³ тор-окно, ping-pong compute | ✅ |
| 2.2 | SplatDensity API + displacement (корабельный след) | ✅ |
| 2.3 | Конденсационные следы (Contrail VFX) | 🟡 VFX требует ручной настройки. C# готов. |
| 2.4 | Штормовые ячейки (форма + цвет) | ✅ Готово |
| 2.4 VFX | Молнии в грозовых ячейках | 🔜 v3.5 |
| 2.5 | Мезий-харвест | 🔜 v3.5 |
| 2.6 | Перф-замеры | ✅ `CloudPerfMonitor` |

### Displacement (2.1–2.2)
- Корабль расталкивает обычные облака через `LocalDensityBuffer` в режиме Displacement
- Штормовые облака **иммунны** к displacement (T-CLOUD41) — не продавливаются насквозь
- `LocalDensityBuffer` — singleton на сцене, compute-based, 96³

### Конденсационные следы (2.3)
- `ShipContrailVfx.cs` — контроллер на каждом корабле (2 скрипта нужно добавить)
- `Contrail.vfx` — VFX Graph, требует ручной доводки
- 🔜 Доработка в v3.5

### Штормовые ячейки (2.4)
- `StormCellDirector` — singleton, управляет ячейками
- Тестовый спавн: вокруг игрока (в будущем — случайно в зоне 0–79999)
- Форма: procedural `abs(Perlin3D)` FBM — organические кластеры, не цилиндры
- Параметры тюнинга: шум, warp, контраст, anti-banding, vertical peak
- Сохранение/загрузка параметров через EditorPrefs
- Кастомный инспектор с кнопками Regenerate / Respawn / Save / Apply
- 🔜 VFX молний → v3.5

---

## Фаза 3 — Интеграция в мир

| Подфаза | Суть | Статус |
|---|---|---|
| 3.1 | Облачное море как пол | 🔜 v3.5 |
| 3.2 | Завеса как нижняя граница | 🔜 v3.5 |
| 3.3 | Погодные ячейки от сервера | 🟡 C# готов (StormCellDirector). Серверная часть → v3.5 |
| 3.4 | Сетевые shared-возмущения | 🔜 v3.5 |
| 3.5 | Выпиливание старых Veil-рендереров | 🔜 v3.5 |
| 3.6 | Перф-аудит полного кадра | 🔜 v3.5 |

### Погодные ячейки (3.3)
- `StormCellDirector` — источник ячеек (в тесте: спавн у игрока)
- В будущем: `WeatherCellManager` будет создавать ячейки случайно в мировой зоне 0–79999
- Отдельные настройки для грозовых (anti-banding, vertical noise, warp)
- 🔜 Серверное управление → v3.5

---

## Что НЕ ТРОГАЕМ (старая система)

- `StormController.cs` / `ServerStormManager.cs` / `StormCloudGenerator.cs` — мёртвый код, не используется
- `VeilSystem`, `HorizonVeilRenderer`, `VeilRaymarchBlit` — старая система v1.5, оставлена выключенной
- Старые облачные префабы — не выпиливаются

---

## Ключевые файлы

| Файл | Роль |
|---|---|
| `VolumetricClouds.shader` | Основной реймарч-шейдер (+ StormDensity) |
| `CloudNoise.hlsl` | Perlin3D, Fbm, Worley3D, InvertedWorley |
| `CloudCommon.hlsl` | HeightProfile, HG, ColorRamp (реализация `GhibliRamp`), RaySlabIntersection |
| `BakeCloudNoise.compute` | Бейк 128³ текстуры |
| `VolumetricCloudsRenderFeature.cs` | URP RenderGraph feature |
| `LocalDensityBuffer.cs` | Интерактивный 3D-буфер плотности |
| `StormCellDirector.cs` | Управление штормовыми ячейками |
| `StormCellDirectorEditor.cs` | Кастомный инспектор |
| `StormLightningVfx.cs` | VFX-транслятор молний |
| `ShipContrailVfx.cs` | Контроллер конденсационных следов |
| `WindManager.cs` | Ветер (используется всеми) |
| `CloudPerfMonitor.cs` | Мониторинг перформанса |

---

## Коммиты (хронология)

| Коммит | Тикет | Суть |
|---|---|---|
| `fbc91eea` | T-CLOUD39 | Procedural storm form + runtime save/load + anti-banding |
| `c1284e85` | T-CLOUD40 | Anti-banding tuning params (vertical noise/warp) |
| `4cc74700` | T-CLOUD41 | Storm clouds immune to ship displacement |

*Более ранние коммиты: см. `ITERATIONS.md`*
