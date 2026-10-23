# T-LIGHT10 — Освещение интерьеров днём

> **Дата:** 2026-10-23
> **Статус:** Шаги 1–2 выполнены. Шаг 3 не применён — ожидает визуального теста пользователя.
> **Baseline для отката:** коммит `a6710796` (`0.1.100 other changes primum first colliders set outline light and etc`)

## 1. Проблема

В домах без источников света днём освещённость практически равна освещённости открытых пространств. Интерьер и улица визуально неразличимы по яркости.

## 2. Измеренное состояние до изменений

### 2.1. Источники света (`BootstrapScene`, additive к `WorldScene_0_0`)

| Источник | Тип | Intensity | Shadows | Shadow Strength | Комментарий |
|---|---|---|---|---|---|
| `Sun` | Directional | 0.15 (в сцене), 1.0 (Midday, runtime) | `None` | **0** | Управляется `DayNightController` |
| `DemoKey_Temp` | Directional | **3.0** | `None` | 1.0 | Временный ключевой свет, ссылок в коде нет |
| `Moon` | Directional | 1.0 (в сцене) | `None` | 1.0 | `MoonController`: `intensity = nightVisibility * 0.4` → **0** днём (05:00–21:00) |

Дополнительные источники: 1464 (префабы `PM_LIGHT_*`, включаются пулом только ночью).

### 2.2. Почему интерьер был светлым — пять независимых причин

1. **`DemoKey_Temp`** — Directional, intensity `3`, белый, `shadows = None`, culling mask `-1`, `activeSelf = true`, ни одного упоминания в коде. URP выбирает главным светом самый яркий directional → главным был `DemoKey_Temp`, а у него тени выключены. Следствие: **в сцене не было ни одной тени от солнца**, свет проходил сквозь крыши и стены.
2. **`Sun.shadowStrength = 0`** — даже при `LightShadows.Soft` (который `DayNightController` ставит в рантайме) сила тени `0` делает тень полностью прозрачной. То есть тени были отключены дважды: и типом, и силой.
3. **`ambientMode = Skybox`, `ambientIntensity` = 1 (сцена) / 0.6 (`MiddayPhase`)** — скайбокс-SH даёт **неокклюдированный** ambient: он одинаков и на площади, и внутри комнаты. Именно это делает «интерьер = улица» на уровне заливки.
4. **Нет окклюзии indirect-света**: `ContributeGI` renderers = 0, lightmaps = 0 (`T-LIGHT03` заблокирован), `LightProbeGroup` нет, SSAO в рендерере нет. Renderer features: `EdgeDetectionRenderFeature`, `VolumetricCloudsRenderFeature`, `DistantFocusRenderFeature`.
5. **`DayVolumeProfile`: `postExposure = +1.5`** — глобальный подъём экспозиции тянет вверх и тёмный интерьер, и улицу, сжимая разницу между ними.

### 2.3. Что проверено и оказалось в порядке

- Геометрия: 51 204 `MeshRenderer`, из них **50 623** `castShadows = On`, **51 119** `receiveShadows = True`. `StaticEditorFlags = 0` (для realtime-теней не требуется).
- URP Asset `ProjectC_URP`: `supportsMainLightShadows = true`, shadowmap `2048`, `shadowDistance = 1000`, 2 каскада, `m_AdditionalLightsRenderingMode = 1` (Per Pixel), лимит 4 на объект, `supportsAdditionalLightShadows = false`, `m_SoftShadowsSupported = false`, HDR off.
- `RenderSettings.m_Sun` в `BootstrapScene` уже указывает на `Sun` (fileID `180883750`).
- `Moon` днём не влияет: `MoonController.alwaysVisible = true` включает только сам объект, но `nightVisibility = 0` для 05:00–21:00 обнуляет интенсивность. `DayNightProfile.moonController = null`.

## 3. Корректировка шага 2 относительно первого предложения

Первоначально шаг 2 был сформулирован как «перевести `RenderSettings.ambientMode` в `Trilight`, чтобы ожили фазовые `ambientSky/Equator/Ground`».

**Проверка по документации Unity отменила это решение:** `RenderSettings.ambientIntensity` — это «Multiplier for the ambient light that Unity generates from the **skybox**», то есть множитель работает **только в режиме `Skybox`**. В режиме `Trilight` множитель игнорируется, и `DayNightController` потерял бы всю фазовую шкалу яркости ambient (0.6 → 0.1), а цвета фаз пришлось бы дополнительно масштабировать вручную.

Поэтому шаг 2 выполнен как **закрепление** уже рабочей схемы, а не её смена: режим `Skybox` фиксируется в коде, а `ambientIntensity` остаётся единственным регулятором заливки (это и есть рычаг шага 3).

## 4. Выполненные изменения (шаги 1–2)

### Шаг 1 — `Assets/_Project/Scenes/BootstrapScene.unity`

- Удалён объект `DemoKey_Temp`: `GameObject 2082049667`, `Light 2082049669`, `Transform 2082049670`, `UniversalAdditionalLightData 2082049668`, а также его запись в `SceneRoots`.
- `Sun`: `m_Shadows.m_Type` `0` → `2` (`LightShadows.Soft`), `m_Shadows.m_Strength` `0` → `0.9`.
  Тип тени в сцене теперь совпадает с тем, что `DayNightController` ставит в рантайме, поэтому Scene View без Play Mode показывает то же, что и игра.
- `WorldScene_0_0.unity` **не изменялся**: сцена принудительно оставлена не-dirty, рабочее дерево по ней чистое.

### Шаг 2 — `Assets/_Project/Scripts/Core/DayNight/DayNightController.cs`, метод `Initialize()`

```csharp
RenderSettings.ambientMode = AmbientMode.Skybox;
if (sunLight != null) RenderSettings.sun = sunLight;
```

- Режим ambient закреплён детерминированно, а не наследуется от той additive-сцены, чьи `RenderSettings` победят.
- `RenderSettings.sun` явно указывает на `Sun`, потому что `DemoKey_Temp` больше не существует; от этого значения зависит направление солнца в `VolumetricCloudsRenderFeature` (`false`-ветка уводит облака на фиктивное направление `(0.3, 0.7, -0.6)`).

**Компиляция:** `No compile errors`.

## 5. Что ожидать после шагов 1–2

- Появляются тени от солнца; крыши и стены перестают пропускать прямой свет.
- Общая яркость открытых пространств **снижается** — из сцены убран белый directional `3.0`, который раньше работал вторым «солнцем».
- Интерьеры перестают получать прямой свет, но **остаются залитыми** скайбокс-ambient (`0.6` в Midday). Контраст «улица / дом» появится частично.
- Это ожидаемое промежуточное состояние. Полное разделение даёт шаг 3.

## 6. Шаг 3 — не применён, ожидает теста

Цель: сделать ambient маленькой управляемой заливкой, а яркость дня перенести на прямой свет. Экспозиция этого не даёт (она поднимает обе стороны одинаково), ambient-заливка — даёт.

| Ассет | Свойство | Сейчас | Предлагается |
|---|---|---|---|
| `MiddayPhase.asset` | `ambientIntensity` | 0.60 | **0.20** |
| `MorningPhase.asset` | `ambientIntensity` | 0.40 | **0.12** |
| `EveningPhase.asset` | `ambientIntensity` | 0.45 | **0.15** |
| `TwilightPhase.asset` | `ambientIntensity` | 0.25 | **0.08** |
| `NightPhase.asset` | `ambientIntensity` | 0.10 | **0.05** |
| `MiddayPhase.asset` | `sunIntensity` | 1.0 | **1.3–1.5** |
| `MorningPhase.asset` | `sunIntensity` | 0.8 | **1.0** |
| `EveningPhase.asset` | `sunIntensity` | 0.7 | **0.9** |
| `DayVolumeProfile.asset` | `postExposure` | 1.5 | **0.6–0.8** |
| `DayVolumeProfile.asset` | `contrast` | 6 | **12–15** |

Пункты по `sunIntensity` нарушают прежний запрет `lighting-plan.md` «не трогаем Sun intensity / DayNightController»: без перераспределения direct/ambient задачу «интерьер темнее улицы» в no-bake URP решить нечем.

## 7. Критерий приёмки (Play Mode, выполняет пользователь)

1. Внутри дома без источников света поверхности заметно темнее улицы.
2. Из открытого проёма виден световой клин от солнца.
3. Ночью неосвещённые интерьеры практически чёрные.
4. Улица днём не выглядит «выключенной» после удаления `DemoKey_Temp`.

## 8. Откат

Полный откат шагов 1–2:

```
git revert <commit T-LIGHT10>
```

Точечный откат только сцены и скрипта:

```
git checkout a6710796 -- Assets/_Project/Scenes/BootstrapScene.unity
git checkout a6710796 -- Assets/_Project/Scripts/Core/DayNight/DayNightController.cs
```

`DemoKey_Temp` восстанавливается только вместе с `BootstrapScene.unity` — отдельной копии объекта нет, но его полное YAML-представление сохранено в истории Git (fileID `2082049667` / Light `2082049669` / Transform `2082049670`).

`WorldScene_0_0.unity` в этом этапе не участвует, поэтому никакого отката по нему не требуется.
