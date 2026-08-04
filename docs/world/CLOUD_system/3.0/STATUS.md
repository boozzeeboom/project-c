# CLOUD_system 3.0 — Итоговый статус

**Дата:** 2026-08-04  
**Версия:** 3.0 (Cloud Ocean Medium)  
**Статус:** 🟢 Продакшн-готово. Оставшиеся задачи → v3.5

---

## Архитектура (кратко)

Один volumetric raymarch-рендерер (`VolumetricClouds.shader` + `RenderFeature`) на весь мир.
4 слоя облаков с Ghibli-рампами день/закат + штормовые ячейки + интерактивный displacement.

```
CloudDensity(worldPos) =
    Σ(layer × HeightProfile × LayerNoiseShape × CoverageNoise × GhibliRamp)
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
| 1.5 | Light marching (6 шагов), HG, multi-scatter, Ghibli-рампы | ✅ |
| 1.6 | Half-res, blue-noise дизеринг, temporal reprojection | ✅ |
| 1.7 | `CloudPerfMonitor` | ✅ |

**Итог:** 4-слойный реймарч работает, облака визуально красивы, перформанс приемлем.

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
| `CloudCommon.hlsl` | HeightProfile, HG, GhibliRamp, RaySlabIntersection |
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
