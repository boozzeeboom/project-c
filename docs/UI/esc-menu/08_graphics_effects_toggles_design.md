# Графика: переключатели эффектов в Esc-меню (дизайн)

> **Дата:** 2026-09-16
> **Статус:** дизайн → реализация
> **Контекст:** `docs/UI/esc-menu/` (аддитивный файл `08_*`, существующие не трогаем)

## Задача

Вывести в Esc → Настройки → Графика основные переключатели постобработки:
глубинный фокус, контурный едж, температура день/ночь.

## Инвентаризация (что реально живёт в проекте)

| Эффект | Реализация | Точка выключения |
|---|---|---|
| Глубинный фокус (Bokeh DoF) | `GazeAutofocus` (Scripts/Rendering) — пишет `focusDistance` в рантайм-копию Volume-профиля; уже есть `SetFocusEnabled(bool)` + `_focusEnabled` (Volume weight 0, скрипт спит) | `SetFocusEnabled` |
| Дальний фокус | `DistantFocusRenderFeature.Active` (проверяется в `AddRenderPasses`, `if (!Active) return`) + `FarFocusController` (пишет `_FarFocusLock`/`_FarFocusCenterDepth` глобалы + Gaussian-драйв DoF) | `Active=false` + `FarFocusController.enabled=false` |
| Контурный едж | `EdgeDetectionRenderFeature : ScriptableRendererFeature` (Scripts/Core) — своего флага нет | `ScriptableRendererFeature.SetActive(false)` на инстансе фичи |
| Температура день/ночь | `DayNightController.ApplyTemperatureFilter` — крутится **каждый кадр** из `Update()` (`ApplyTemperatureFilter(currentTemperature)`), пишет `weight` в отдельный temperature-volume (priority 200) | гард внутри метода — подписка не нужна |

## Решение

Один мастер-тумблер «Глубина резкости» гасит всю фокус-цепочку целиком
(`GazeAutofocus` + `DistantFocusRenderFeature.Active` + `FarFocusController.enabled`),
а не по частям — иначе получим «половину блюра» (Bokeh выкл, а far-pass мылит даль).

- `SettingsManager` (ProjectC.Core): 3 новых bool-настройки с PlayerPrefs-персистом
  и событиями — `DepthOfField` (default true), `EdgeDetection` (default true),
  `TemperatureFilter` (default true). Ключи `Settings.DepthOfField` и т.д.
- `GraphicsEffectsApplier` (новый static, ProjectC.Rendering): применяет значения
  к живым объектам. RendererFeature-инстансы ищет через
  `Resources.FindObjectsOfTypeAll<ScriptableRendererData>()` (единственный рендерер
  в проекте; YAML ассета руками не трогаем). Подписывается на события
  SettingsManager + `RuntimeInitializeOnLoadMethod` для начального применения.
  Всегда null-safe (объектов может не быть в Bootstrap/сценах без фокуса).
- `DayNightController.ApplyTemperatureFilter`: гард
  `if (!SettingsManager.TemperatureFilter) { _temperatureBlendVolume.weight = 0; return; }`.
  Ассет `DayNightProfile.enableTemperatureFilter` не мутирует — пользовательский тумблер
  работает поверх авторской настройки.
- `GazeAutofocus.OnEnable`: начальный `_focusEnabled = SettingsManager.DepthOfField`
  (сцена может пересоздать объект после смены настройки).
- `FarFocusController`: добавлен `OnDisable`, сбрасывающий шейдер-глобалы
  (`_FarFocusLock=0`) и Gaussian-полосу в покой — иначе выключение компонента
  замораживает последний блюр.
- `GraphicsSettingsSection`: новая подсекция «Эффекты» с тремя тумблерами,
  поверх существующих (качество/разрешение/экран не трогаем).

## Локализация

Новые ключи (`ui.esc_menu.section.effects`, `ui.esc_menu.label.dof`,
`ui.esc_menu.label.edge`, `ui.esc_menu.label.tempfilter`) в `UI_Table` вносятся
отдельным LOC-проходом через нативный экспорт/импорт CSV (см. skill
project-c-localization — ручная правка `.asset` запрещена). До тех пор в коде —
русские литералы (стратегия RU-fallback из UXML: `Loc.Bind` не вызывается,
`MakeLabel` локализует только `ui.*`-ключи, литерал показывается как есть).

## Верификация (делает пользователь)

1. Compile: Console → 0 errors.
2. Play → Esc → Настройки → Графика → секция «Эффекты», 3 тумблера.
3. Глубина резкости OFF → фон перестаёт мылиться (Bokeh + far-pass погашены);
   ON → возвращается. Переключение переживает перезаход в мир.
4. Едж OFF → контуры пропадают; ON → возвращаются.
5. Температура OFF → при жаре/холоде картинка не тонируется
   (temperature-volume weight 0); ON → тонирование возвращается.
6. Настройки переживают рестарт клиента (PlayerPrefs).
7. F8/F9 (floating origin): тумблеры не хранят мировых Vector3 — хуки сдвига не нужны.

## Открыто

- LOC-ключи для 4 новых строк (см. выше) — отдельным тикетом.
- Слайдер силы эффектов (например, `FarStrength`) — только если попросит пользователь.
