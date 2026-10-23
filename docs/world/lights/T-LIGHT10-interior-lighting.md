# T-LIGHT10 — Освещение интерьеров днём

> **Дата:** 2026-10-23
> **Статус:** Шаги 1–5 применены и закоммичены. Визуальная приёмка в Play Mode — за пользователем.
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

## 4. Выполненные изменения — шаги 1–2

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

## 5. Выполненные изменения — шаги 3–5

Шаги 3–5 применены разово по запросу пользователя после визуального подтверждения шагов 1–2 («в принципе уже хорошо выглядит, но давай проверим результат»). Смысл — перенести яркость дня с неокклюдированной ambient-заливки на прямой свет и вернуть «сочность» цел-шейдинга через Volume, а не через подъём экспозиции.

### Шаг 3 — `ambientIntensity` по фазовым ассетам

| Ассет | Было | Стало |
|---|---|---|
| `MiddayPhase.asset` | 0.60 | **0.20** |
| `MorningPhase.asset` | 0.40 | **0.12** |
| `EveningPhase.asset` | 0.45 | **0.15** |
| `TwilightPhase.asset` | 0.25 | **0.08** |
| `NightPhase.asset` | 0.10 | **0.05** |

Шкала сохранена монотонной и в той же пропорции (~3× вниз). Ambient больше не является основным источником яркости, но остаётся подложкой, поэтому полностью чёрных зон днём нет.

### Шаг 4 — `sunIntensity` по фазовым ассетам

| Ассет | Было | Стало |
|---|---|---|
| `MiddayPhase.asset` | 1.0 | **1.4** |
| `MorningPhase.asset` | 0.8 | **1.0** |
| `EveningPhase.asset` | 0.7 | **0.9** |

`TwilightPhase.sunIntensity` (0.3) и `NightPhase.sunIntensity` (0) не менялись: там ambient уже мал, а прямой свет либо отключён (`castShadows = 0`), либо выключен в рантайме (`DayNightController` гасит `Sun` для фаз с `Night`).

### Шаг 5 — `DayVolumeProfile.asset`, `ColorAdjustments`

| Свойство | Было | Стало |
|---|---|---|
| `postExposure` | 1.5 | **0.7** |
| `contrast` | 6 | **13** |
| `saturation` | −10 | −10 (не менялось) |

Снижение экспозиции убирает глобальный подъём, который одинаково тянул вверх и улицу, и интерьер; рост контраста возвращает це-шейдингу «сочность» без поднятия интерьеров.

### Проверка применённых значений

Значения прочитаны обратно из загруженных в редактор ассетов (`AssetDatabase.LoadAssetAtPath` → поля `TimeOfDayPhase` и `ColorAdjustments`), не из текста файлов:

```
MorningPhase  -> sunIntensity=1.0 ambientIntensity=0.12
MiddayPhase   -> sunIntensity=1.4 ambientIntensity=0.20
EveningPhase  -> sunIntensity=0.9 ambientIntensity=0.15
TwilightPhase -> sunIntensity=0.3 ambientIntensity=0.08
NightPhase    -> sunIntensity=0.0 ambientIntensity=0.05
ColorAdjustments -> postExposure=0.7 contrast=13 saturation=-10
```

### Нарушенное ограничение и его причина

`lighting-plan.md` §«Что НЕ делаем» запрещал трогать `Sun intensity`. Шаги 3–4 этот пункт нарушают осознанно: при `ambientMode = Skybox` ambient-заливка неокклюдирована и одинакова внутри и снаружи, поэтому без перераспределения direct/ambient задачу «интерьер темнее улицы» в no-bake URP решить нечем. Запрет пересмотрен только в объёме фазовых ассетов; логика `DayNightController` не менялась.

## 6. Что ожидать после шагов 1–5

- Интерьер без источников света получает только ambient `0.20` от `MiddayPhase` — он заметно темнее улицы, которую освещает прямой свет `1.4`.
- Из открытого проёма виден световой клин: прямой свет проходит через проём, но не через стены и крыши.
- Общая яркость открытых пространств сохраняется за счёт роста `sunIntensity` и снижения `postExposure` (−0.8 EV), то есть улица не должна «выключиться».
- Ночь: ambient `0.05` + `NightVolumeProfile.postExposure = −0.8` ⇒ неосвещённые интерьеры практически чёрные, работает только пул ламп.
- Контраст `13` усиливает границу «свет / тень», что для це-шейдинга желательно, но может подчеркнуть грубость теней (см. §7).

## 7. Известные ограничения — грубость теней (зафиксировано)

Пользователь подтвердил: **тени работают, но слишком грубые**. Это не регрессия T-LIGHT10, а следствие текущих настроек URP Asset, которые в этом этапе намеренно не менялись. Измерено в `Assets/_Project/Settings/ProjectC_URP.asset`:

| Параметр | Значение | Следствие |
|---|---|---|
| `m_MainLightShadowmapResolution` | 2048 | Мало для такой дистанции |
| `m_ShadowDistance` | 1000 | Огромный диапазон на 2048 px |
| `m_ShadowCascadeCount` | 2 | Всего два каскада |
| `m_Cascade2Split` | 0.25 | Каскад 1 ≈ 250 м, каскад 2 ≈ 750 м |
| `m_SoftShadowsSupported` | 0 | `Soft`-фильтрация выключена в ассете |
| `m_SoftShadowQuality` | 2 (Medium) | Игнорируется, пока `m_SoftShadowsSupported = 0` |
| `m_ShadowCascadeBorder` | 0.2 | Широкий блендинг между каскадами |
| `m_ShadowDepthBias` / `m_ShadowNormalBias` | 1 / 1 | Дефолт; при грубом теневом текселе даёт шаг пикселя |

Плотность: каскад 1 ≈ `250 / 2048 ≈ 0.12 м` на тексель, каскад 2 ≈ `750 / 2048 ≈ 0.37 м` на тексель. При таком текселе зубчатая кромка и «ступени» на стенах неизбежны, а `m_SoftShadowsSupported = 0` не даёт их сгладить — `Sun.shadows = Soft` в сцене и в `DayNightController` фактически упирается в этот флаг.

**Статус:** записано как отдельная задача `T-LIGHT11 — качество теней`, в этом этапе не исправлялось.

## 8. Критерий приёмки (Play Mode, выполняет пользователь)

1. Внутри дома без источников света поверхности заметно темнее улицы.
2. Из открытого проёма виден световой клин от солнца.
3. Ночью неосвещённые интерьеры практически чёрные.
4. Улица днём не выглядит «выключенной» после удаления `DemoKey_Temp` и снижения ambient.
5. Тени снова видны на открытых пространствах (не регрессировали относительно шагов 1–2).

## 9. Откат

Пошаговый откат (каждый шаг независим):

```
# шаги 1–2 (сцена + скрипт)
git checkout a6710796 -- Assets/_Project/Scenes/BootstrapScene.unity
git checkout a6710796 -- Assets/_Project/Scripts/Core/DayNight/DayNightController.cs

# шаги 3–5 (фазовые ассеты и Volume)
git checkout a6710796 -- Assets/_Project/ScriptableObjects/DayNight/MorningPhase.asset
git checkout a6710796 -- Assets/_Project/ScriptableObjects/DayNight/MiddayPhase.asset
git checkout a6710796 -- Assets/_Project/ScriptableObjects/DayNight/EveningPhase.asset
git checkout a6710796 -- Assets/_Project/ScriptableObjects/DayNight/TwilightPhase.asset
git checkout a6710796 -- Assets/_Project/ScriptableObjects/DayNight/NightPhase.asset
git checkout a6710796 -- Assets/_Project/ScriptableObjects/DayNight/Volumes/DayVolumeProfile.asset
```

`DemoKey_Temp` восстанавливается только вместе с `BootstrapScene.unity` — отдельной копии объекта нет, но его полное YAML-представление сохранено в истории Git (fileID `2082049667` / Light `2082049669` / Transform `2082049670`).

`WorldScene_0_0.unity` в этом этапе не участвует, поэтому никакого отката по нему не требуется.
