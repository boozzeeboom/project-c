# FarFocus — дизайн: фокус дальних объектов своим проходом (DF-001 rev.7)

> Статус: дизайн утверждён к реализации. Bokeh-серия (rev.1–6) закрыта.

## 1. Ресёрч: почему стоковый URP DoF не подходит (анализ)

| Требование | Bokeh (stock) | Gaussian (stock) | Вывод |
|---|---|---|---|
| Дальние размыты, пока не смотрим | Да, но CoC тонкой линзы: 50мм f/5.6 на 2–12м даёт ~2px — не видно; 85мм f/2 — рваные пятна | Нет (фиксированная полоса глубин, не от взгляда) | Нужен свой CoC с управляемой силой |
| Персонаж НЕ мылится никогда | Невозможно: screen-space, тонкая ГРИП режет тело кусками | Невозможно | Нужна глубинная гарантия + gather с CoC=0 |
| Взгляд держит даль 1с → ближние чуть мылятся | Нет dwell-логики | Нет | Свой контроллер с таймером |
| Читаемый стабильный фокус | Плавает (луч дёргается на границах) | — | Гистерезис уже есть, переиспользуем |

Дополнительные факты из кода проекта:
- `EdgeDetectionRenderFeature` — канонический RenderGraph-паттерн: `ConfigureInput(Normal|Depth)`,
  копия цвета через `AddCopyPass`, запись назад в `activeColorTexture`, фулскрин-треугольник,
  UV через `positionCS`. Копируем архитектуру.
- Глубина доступна в проходе через `SampleSceneDepth` + `LinearEyeDepth` (метры от камеры).
- Луч из центра попадает в персонажа: `CharacterController` ловит `Physics.Raycast`.
- Камера игрока: `far=1000000`, `near=0.1` → шаг глубины на 4км ≈ 10м. Для плавного
  блюра терпимо (переходы 800→4000м), для резких порогов — нет. Учитываем широкими smoothstep.
- Порядок: наш проход `AfterRenderingOpaques`, добавляется ПОСЛЕ Edge → блюрим и контуры
  (консистентно). Облака (`BeforeRenderingTransparents`) идут позже и остаются резкими — ок.

## 2. Решение: свой far-field проход + контроллер взгляда

Bokeh Volume выключается (`Mode=Off`, тестовая фаза без боке по требованию).
`GazeAutofocus` отключается на GO (файл остаётся). Новые файлы:

- `Shaders/DistantFocus.shader` (`Hidden/ProjectC/DistantFocusFar`)
- `Scripts/Rendering/DistantFocusRenderFeature.cs` (пассивные пороги/сила)
- `Scripts/Rendering/FarFocusController.cs` (взгляд → `_FarFocusLock`, `_CenterDepth`)

Включение фичи в `ProjectC_URP_Renderer.asset` — скриптом в Editor
(`AddObjectToAsset` + `rendererFeatures.Add`, как суб-ассеты Edge/Clouds).

## 3. Математика шейдера (всё — ручки фичи, хардкода нет)

```
depth = LinearEyeDepth(SampleSceneDepth(uv))              // метры
farZone  = smoothstep(_FarStart,_FarEnd,depth) * (1-_FarLock) * _FarStrength
nearZone = smoothstep(_CharMax,_CharMax*2,depth)
         * (1-smoothstep(_FarStart*0.5,_FarStart,depth))
         * _NearStrength * _FarLock
coc = max(farZone, nearZone)                              // 0..1
radius = coc * _MaxRadius                                  // px
```

- Персонаж (`depth < _CharMax`, дефолт 40м > ship-зум 35м): обе зоны = 0 → **ранний выход,
  пиксель возвращается бит-в-бит**. Исключение гарантировано конструкцией, не настройкой.
- Анти-ореол: при `coc>0` тапы с `tapDepth < _CharMax` давятся до 0.15 (персонаж не течёт в фон).
- Небо (`depth≈far`): мылится пока не смотрим вдаль; при локе (`_FarLock=1`) farZone=0 → горы резкие.
- Gather 12 тапов Пуассона из копии цвета; запись `Blend One Zero`.

Поля фичи: `Active`, `FarStart=800`, `FarEnd=4000`, `FarStrength=1`,
`NearStrength=0.35`, `MaxRadius=12px`, `CharMax=40`, шейдер-ссылка + OverrideMaterial.

## 4. Контроллер взгляда

Каждый N-й кадр (дефолт 4): луч из центра активной камеры (ленивая привязка к ригу,
как в rev.4) + фолбэк на якорь, если луч рядом с ним:
`centerDepth = hit ?? (rayNearAnchor ? anchorDist : far)`.
Dwell: `centerDepth > FarThreshold` (дефолт 800 = FarStart фичи) → `lockT += dt`,
иначе распад; `_FarLock = smoothstep(0,1,lockT/LockTime)`, `LockTime=1с`.
Глобалы шейдеру: `_FarFocusLock`, `_CenterDepth` (для отладки/логики).
Ручки: слои, частота, `FarThreshold`, `LockTime`, `SkyDepth`, дебаг-лог.
Поведение: смотрим на персонажа/под ноги → `centerDepth` мал → даль в молоке;
увели камеру на горы → через 1с даль резкая, средний план чуть плывёт.

## 5. Проверка

1. Console 0 errors; рефлексия 2 типов; YAML: фича в rendererFeatures, DoF=Off.
2. Play: взгляд вниз → горы плывут; камера на горы 1с → резкие + средний план чуть мылится;
   персонаж резкий всегда, на любом зуме.
3. Профайлер: фулскрин 13 тапов — ворота наращивания (half-res при просадке).

## История

| Дата | Сессия | Изменения |
|---|---|---|
| 2026-09-16 | Mavis | Дизайн far-field прохода; закрытие Bokeh-серии rev.1–6 |
