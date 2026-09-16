# DistantFocus — автофокус зрения (DoF по взгляду)

> Статус: анализ + план. Кода нет. Папка-трекера фичи: `docs/world/distantfocus/`.

Идея: объекты не в фокусе взгляда размываются. Смотрим вдаль — даль резкая,
ближнее слегка мылит; смотрим под ноги — наоборот. Эффект «фокус зрения».

## 1. Что сейчас в пост-стеке (аудит 2026-09-16)

| Слой | Где | Состав |
|---|---|---|
| URP asset | `Assets/_Project/Settings/ProjectC_URP.asset` | `SupportsHDR: 0`, `RequireDepthTexture: 0`, `RequireOpaqueTexture: 0`, `VolumeProfile: none`, post-processing включён |
| Камера | `BootstrapScene / MainCamera` | `far: 1000`, `near: 0.3`, `RenderPostProcessing: 1`, depth/opaque = UsePipelineSettings |
| DayVolumeProfile | `ScriptableObjects/DayNight/Volumes/` | ColorAdjustments + Vignette (0.388) + Bloom (0.3) |
| NightVolumeProfile | там же | ColorAdjustments + Bloom (0.21, синий тинт) |
| TwilightVolumeProfile | там же | ColorAdjustments + Vignette (0.3) + Bloom (0.22) |
| Бленд | `DayNightController` (рантайм) | 3 child-Volume `priority=100` (weight крутится в `ApplyVolumeBlend`), 4-й temperature-Volume `priority=200` со своим ColorAdjustments |
| RendererFeatures | `ProjectC_URP_Renderer.asset` | EdgeDetection (`AfterRenderingOpaques`) + VolumetricClouds (`BeforeRenderingTransparents`) |

**DoF сейчас нет нигде.** Вывод: эффект ни с чем не конфликтует, его некуда
«встроить» — добавляется новым отдельным слоем (см. §3).

## 2. Ответ: да, можно, штатным URP DepthOfField

URP 17.0.3 из коробки даёт `DepthOfField` в Volume:
режимы `Gaussian` (дешёвый) / `Bokeh` (красивый, дорогой),
`Focus Mode: Fixed / Camera / Custom`, `focusDistance`, `focalLength`,
`aperture`, `bladeCount`. Никаких кастомных RenderFeature/шейдеров не нужно.

Поведение «смотрим → фокус» делается скриптом-автофокусом (~60 строк):
каждый N-й кадр рейкаст из центра камеры → дистанция попадания =
целевая `focusDistance` → плавное демпфирование (`Mathf.SmoothDamp`).
Нет попадания (небо) → увод фокуса на даль (`focusDistance = far`).
Промах по небу = «смотрим вдаль» — ровно желаемое поведение из запроса.

Почему не Custom RenderFeature с CoC-шейдером: дороже в разработке и поддержке,
а визуально для «простого но эффектного» штатного Gaussian хватает.
Bokeh — опция на потом (требует больше fillrate; при `SupportsHDR: 0`
боке-блики беднее, чем в HDR-проектах).

## 3. Куда класть, чтобы не сломать DayNight

1. Новый ассет `FocusVolumeProfile` (только компонент `DepthOfField`, active).
   НЕ добавлять DoF в Day/Night/Twilight-профили: их рантайм-копии блендятся
   по weight, и фокус начал бы «мигать» при смене фаз.
2. Новый `Volume` (global, `priority=300` — выше temperature-Volume 200,
   т.к. DoF ортогонален цвету и не должен тонуть в бленде).
   Создавать по паттерну `DayNightController.CreateBlendVolume` — рантайм-child,
   чтобы префаб/сцена не расползались, либо один статичный Volume на сцене
   рядом с `globalVolume` (проще, BootstrapScene всё равно правится только
   добавлением объекта — согласовать отдельно, см. §5).
3. Скрипт `ProjectC.Rendering.GazeAutofocus` (папка `Scripts/Rendering/`,
   рядом с `VolumetricCloudsRenderFeature`): читает камеру, пишет
   `focusDistance` в рантайм-копию профиля (профили в рантайме не мутировать —
   правило DayNight: `Instantiate(profile)`).
4. Настройки по умолчанию (под `far: 1000`): режим `Gaussian`, quality `Medium`,
   `focusDistance` старт `100`, `focalLength` ~50, `aperture` ~5.6 —
   точные числа подобрать визуально в Editor (Volume override → Play → крутить).

## 4. Нюансы проекта (проверено по коду)

- **Порядок проходов:** EdgeDetection и облака рисуются ДО transparents/post,
  URP DoF — в post-цепочке, т.е. ПОСЛЕ них. Контур distant-объекта тоже
  размоется вместе с объектом — это консистентно с «фокусом», не баг.
- **Depth:** DoF сам запрашивает depth внутри прохода; `RequireDepthTexture: 0`
  в ассете не блокер (камера стоит на UsePipelineSettings).
- **Floating Origin:** хука сдвига НЕ нужно (🟢-класс по правилам FO).
  Фокусная дистанция каждый кадр пересчитывается из рейкаста в камерных
  координатах; мировых `Vector3` между кадрами скрипт не хранит —
  только скаляр `focusDistance` + скорость демпфирования.
- **NGO/сеть:** эффект чисто клиентский, на камеру. Никаких NetworkVariable/RPC.
- **Перф:** Gaussian Medium на полном разрешении — самый дешёвый DoF;
  если просядет — `HighQualitySampling: off`, либо гнать DoF в half-res
  уже кастомным проходом (запасной план, не делать сразу).
- **Настройки графики:** добавить тумблер «Фокус зрения» в SettingsManager
  (вкл/выкл Volume weight 1/0) — по аналогии с существующими аудио/видео-секциями.

## 5. Открытое (решить перед кодом)

1. Куда вешать Volume+скрипт: `BootstrapScene` правится только добавлением
   (не переносить MainCamera/NetworkManager) — подтвердить, что добавление
   GO `FocusVolume` рядом с `globalVolume` ок.
2. Рейкаст каждый кадр или каждый 3–5-й (дешевле, фокус всё равно сглажен)?
   По умолчанию — каждый 4-й + SmoothDamp ~0.15–0.25 c.
3. Что считать «попаданием»: только terraine/корабли/острова (layer mask),
   или всё подряд? Предложение: маска без триггеров/UI/частиц.
4. Нужен ли Bokeh-режим как «Ультра»-пресет, или Gaussian единственный?
   Предложение: сначала только Gaussian.

## 6. План работ (выполнено 2026-09-16, DF-001)

1. ✅ `Assets/_Project/Scripts/Rendering/GazeAutofocus.cs` (`ProjectC.Rendering`):
   рейкаст из центра камеры → `focusDistance` в рантайм-копию профиля.
   Все ручки — инспектор: слои, частота рейкаста, дальность луча (0 = far камеры),
   фокус неба, кламп, сглаживание, вкл/выкл + `SetFocusEnabled(bool)` для меню.
2. ✅ `Assets/_Project/ScriptableObjects/Focus/FocusVolumeProfile.asset`:
   только `DepthOfField`, Mode=Bokeh, focus 200 / focal 50 / aperture 5.6 / blades 5.
   Весь «характер» боке крутится здесь в Editor, скрипт не трогает.
3. ✅ GO `DistantFocus` в `BootstrapScene`: Volume (global, priority=300, weight=1)
   + GazeAutofocus. Больше сцена не тронута.
4. Проверка: Console 0 errors, рефлексия `GazeAutofocus` OK,
   верификация в Editor: prio=300, global, Bokeh f=200 ap=5.6.
5. Осталось пользователю (Play): взгляд вдаль/вблизь → плавный фокус;
   F8/F9; профайлер кадра с/без DoF.

## 7. Ручки настройки (где крутить)

| Что | Где |
|---|---|
| Сила/характер боке (aperture, focalLength, blades, curvature) | `FocusVolumeProfile.asset` → DepthOfField |
| Поведение фокуса (слои, частота, дистанции, сглаживание) | GO `DistantFocus` → GazeAutofocus (инспектор) |
| Вкл/выкл эффекта | `Focus Enabled` там же (в рантайме — `SetFocusEnabled`) |
| Приоритет над DayNight | Volume → Priority (300 > temperature 200) |

## 8. Rev.2 — якорь-фокус (2026-09-16, фикс инверсии)

Симптом: даль резкая всегда, ближнее/персонаж мылятся при приближении и зуме.
Причина: база фокуса ехала только по лучу из центра кадра; персонаж в нём
не участвовал — луч уходил в фон, фокус залипал на дали.
Камера — third-person SpringArm (всегда смотрит на персонажа), поэтому:
база = дистанция до якоря (`SpringArmCamera.TargetTransform` вживую, смена
персонаж/корабль подхватывается; есть ручной `_manualAnchor`).
Луч лишь корректирует: ближе якоря → препятствие; дальше якоря + якорь
в центре → держим якорь; дальше + якорь вне центра (камеру увели на объект) →
фокус на дальнее. Новые ручки: `_cameraRig`, `_manualAnchor`,
`_anchorSnapRadius` (2 м), `_anchorHeightOffset` (1.2 м).

| Дата | Сессия | Изменения |
|---|---|---|
| 2026-09-16 | Mavis | Создан файл: аудит пост-стека, ответ и план DistantFocus |
| 2026-09-16 | Mavis | DF-001 реализован (Bokeh): GazeAutofocus.cs + FocusVolumeProfile + GO DistantFocus в BootstrapScene |
| 2026-09-16 | Mavis | DF-001 rev.2: якорь-фокус на цель SpringArmCamera (фикс инверсии: персонаж резкий, фон мылится) |
| 2026-09-16 | Mavis | DF-001 rev.3: фикс пустого профиля (AddObjectToAsset) + fallback-DoF в рантайме |
| 2026-09-16 | Mavis | DF-001 rev.4: ленивая привязка к камере игрока (риг спавнится после Bootstrap) + HasAnchor + debugLog |
| 2026-09-16 | Mavis | DF-001 rev.5: авто-диафрагма (персонаж не мылится) + база профиля 85мм f/2 (CoC-фикс) |
| 2026-09-16 | Mavis | DF-001 rev.6: гистерезис фокуса (deadband+settle) + мягкое боке 65мм f/2.8 HQ |
| 2026-09-16 | Mavis | DF-001 rev.7: свой far-field проход (шейдер+фича+FarFocusController), Bokeh=Off, ресёрч в DESIGN_farfocus.md |
