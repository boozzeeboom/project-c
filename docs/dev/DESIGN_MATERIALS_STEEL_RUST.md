# Дизайн-нота: процедурные материалы, первый — SteelRust

Date: 2026-10-04. Статус: SPEC, кода нет. Автор: агент (исследование + спецификация).

## Постановка

95% объектов без текстур, генеративные адаптивные материалы.
Текстуры — только герой и ключевые персонажи. Старт — сталь с процедурной
ржавчиной на углах/соединениях. Вопрос пользователя: реально ли чисто
материалом; нужны ли доп. пакеты (ставить только с одобрения).

## Prior art в проекте (факты, проверены 04.10.2026)

- `Assets/_Project/Materials`: 44 `.mat`, все просмотренные — плоский URP Lit
  (`Ship/heavy.mat` — синий, `Ship/Medium.mat` — красный, Metallic 0).
  Процедурки нет. ShaderGraph-ассетов — 0 (поиск через unity-mcp).
- `Assets/_Project/Shaders`: рукописные HLSL (EdgeDetection, VolumetricClouds,
  DistantFocus и др.). Команда HLSL умеет — аргумент за рукописный шейдер,
  а не ShaderGraph-YAML (диффы/ревью/мержи).
- Рендер — Forward (`m_RenderingMode: 0` в `ProjectC_URP_Renderer.asset`).
  Deferred-гайды (GBuffer metalness-driven эффекты) неприменимы.
- Активные фичи: EdgeDetection (Sobel по глубине+нормалям) + VolumetricClouds
  + DistantFocus. Новый fullscreen-pass — с оглядкой на них.
- URP 17.5.0, Unity 6000.5.2f1, ProBuilder 6.1.2 уже стоит. Polybrush нет.

## Ресерч (интернет, кратко)

- Классика жанра: curvature map (bake в xNormal/Substance/Blender Pointiness)
  как маска кромок; AO/cavity — маска стыков; Voronoi+Noise — паттерн ржавчины;
  top-facing + streak — потёки. Blender-подход (Pointiness→ColorRamp→Mix)
  переносится в Unity 1:1 с заменой Pointiness на G-канал вертексов.
- «Чисто пиксельная детекция кромок без bake» — это либо screen-space cavity
  (нормали+глубина с камеры: malyawka/URP-ScreenSpaceCavity, cavifree,
  платный Cavity Shader) с артефактами и ценой прохода, либо завышенные
  обещания. Форумные треды (Unity, MakeGamesSA) сходятся: кромки — bake
  (текстура curvature или vertex colors), остальное — математика.
- Global Dirt (Asset Store, URP 17+, volume+renderer feature, без изменения
  материалов) — красивый ориентир «состарить сцену слайдером», но: требует
  Deferred для металла/ржавчины, платный, внешний dependency. Не наш путь
  для v1, держать в уме как слой пыли поверх всего позже.
- Triplanar в Shader Graph — только для текстур; нам нужен object-space шум
  без текстур (gradient noise 3D→ спроецированный или 3×2D), что в HLSL
  пишется прямо, в графе — громоздко.

## Решение

1. Шейдер — рукописный HLSL URP Lit `ProjectC/ProceduralSteel` (не Shader Graph):
   контроль деривативов/шума, читаемые диффы, SRP Batcher, Forward.
2. Маски размещения — vertex colors R/G (+B вариация), bake из Blender.
   Шум/градиенты — object-space в шейдере. FO-safe по построению.
3. Два качества Near/Far одним keyword. Пресеты — материалами.
4. v1 без новых пакетов. Polybrush — опция (решение пользователя).
   Screen-space cavity и платные паки — отказ в v1 с обоснованием.

## Открытые вопросы к пользователю

1. Писать HLSL-прототип сейчас (шейдер + 3 пресета + тестовая сцена-инструкция)?
2. Ставить Polybrush для покраски масок в редакторе?
3. Нужен ли уже сейчас общий слой пыли/высветления поверх всех материалов
   (типа Global Dirt лайт) или сначала только SteelRust?

## Пивот 04.10.2026 (v5): деривативы мертвы, путь — бейк+фаски

Серия `ProjectC/DiagDeriv`-тестов со скриншотами (user-authorized) доказала:
`ddx`/`ddy`/`cross`/`normalize` живы, а `fwidth()` попиксельно-постоянного
поля = 0 везде (квады не смешивают треугольники в этом окружении).
Пиксельный детект удалён из шейдера. Ядро полосы: печёный G (EdgeMaskBaker,
проверен числами) + силуэтный рим. Полоса требует плотности меша
(bevel+subdivide, оба шага — через unity-mcp manage_probuilder).
Проверено глазами: EdgeBand-полосы + финал с ржавчиной под key light.
Диагностические артефакты (DiagDeriv, тест-кубы, скриншоты) удалены.
Открытый вопрос: читаемость металла в тёмных сценах (key light + env,
`TODO-LIGHT`).

## v7 + ресерч субагентов 04.10.2026: рецепт подтверждён, домены разделены

Параллельный ресерч 4 субагентов (шейдеры / бейк-пайплайны / другие движки /
готовые решения) сошёлся: per-pixel геометрической кривизны без предрасчёта
в продакшене НЕ существует — везде бейк (текстуры/вертексы/primvar):
Substance Edge Wear требует baked maps, Arnold/V-Ray/Redshift — лучи,
Blender Pointiness — повершинный предрасчёт, UE5 — Modeling Mode bake
в вертексы, Houdini deprecated shade-time curvature в пользу primvar.
Наш путь (бейк двугранных углов в вертексы + шум в шейдере) = индустриальный
стандарт, а не костыль. Готового «стабильные кромки без текстур из коробки»
не найдено (всё либо экранное, либо текстурное, либо Deferred).

Тот самый рецепт (NormalObject→DDX/DDY→Length→Add→Noise→Smoothstep→Lerp)
возвращён в шейдер как аддитив для сглаженных изгибов (`_DerivGain`,
кривизна `curv=|dN|/|dP|` в 1/м — стабильна к дистанции/сдвигу FO).
Доказано скриншотами рядом: сфера = живая серая кривизна, жёсткий куб =
чёрный (норма: сплиты невидимы), скошенный+бейк = белые полосы.
Правило простое: bake = жёсткие рёбра, curv = плавные изгибы.

## Следующий шаг (обновлено 2026-10-04, v5)

Прототип готов: `Assets/_Project/Shaders/ProceduralSteel.shader` +
`Assets/_Project/Materials/Steel/M_PC_Steel_{Clean,Rusted,Dark}.mat`
(созданы через MCP `manage_shader`/`manage_material`, `.meta` сгенерировал
Unity). Compile: 0 errors. Дальше — Manual по инструкции из
`docs/Materials/SteelRust/` (визуал + F8/F9) делает пользователь.
Play Mode и скриншоты — только пользователь.
