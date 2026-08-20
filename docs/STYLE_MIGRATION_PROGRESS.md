# Миграция визуальной терминологии документации

## Цель
Обновить документацию ProjectC_client в `docs/`, не изменяя `docs/archive/`: убрать упоминания стилистики Ghibli/Ghibli-inspired и заменить их контекстно на актуальный визуальный язык проекта:

- западные комиксы / comic-book визуальный язык;
- мягкие цветовые и световые переходы;
- outline / контурная обводка;
- упрощённые, читаемые формы и силуэты;
- симплификация как художественный приём, но не casual/hyper-casual.

## Ограничения
- Не редактировать и не считать обязательными к исправлению файлы внутри `docs/archive/`.
- Не переименовывать Unity-ассеты и технические идентификаторы автоматически: `CloudGhibli.shader`, `CloudGhibli_OutlineV*.shader`, `GhibliRamp` и связанные имена сначала считаются фактическими именами. В документации сохранять путь/имя, меняя только стилистическое описание.
- Не делать механическую замену `Ghibli -> comics` во всех строках.
- После каждого тематического батча повторять поиск `ghibli|гибли` вне архива.

## Подтверждённые проектом опорные термины
По текущим материалам/шейдерам и документации проекта подтверждены:

- `CloudInstanced.shader`: `Comic Outline`, silhouette edge, softness, gradient lighting.
- `EdgeDetection.shader` и `EdgeDetectionRenderFeature.cs`: Borderlands-style post-process edge detection, pencil stroke, adaptive color, distance falloff.
- `TargetOutline.shader`: inverted-hull outline.
- `GDD_14_Visual_Art_Pipeline.md`: градиентная окраска, плавные контуры, outline/Edge Detection.
- `WorldLandscape_Design.md`: мягкие переходы между биомами.

## Первичный инвентарь совпадений вне `docs/archive/`
### Основные документы
- `docs/ART_BIBLE.md`
- `docs/COLABORATION.md`
- `docs/MMO_Development_Plan.md`
- `docs/gdd/GDD_00_Overview.md`
- `docs/gdd/GDD_02_World_Environment.md`
- `docs/gdd/GDD_11_Inventory_Items.md`
- `docs/gdd/GDD_13_UI_UX_System.md`
- `docs/gdd/GDD_14_Visual_Art_Pipeline.md`
- `docs/gdd/GDD_15_Audio_System.md`
- `docs/world/WorldLandscape_Design.md`
- `docs/world/lights/lighting-plan.md`
- `docs/UI/full rebuild plans/00_immersive_menu_strategy.md`
- `docs/Fun/index.html`
- `docs/roadmap.html`
- `docs/unity6/UNITY6_URP_SETUP.md`

### Cloud Ocean / cloud legacy documents
- `docs/world/CLOUD_system/ITERATIONS.md`
- `docs/world/CLOUD_system/1.0/CLOUD_ARCHITECTURE.md`
- `docs/world/CLOUD_system/1.0/CLOUD_IMPLEMENTATION_PLAN.md`
- `docs/world/CLOUD_system/1.0/CLOUD_ONBOARDING.md`
- `docs/world/CLOUD_system/1.0/CLOUD_TECHNICAL_SUMMARY.md`
- `docs/world/CLOUD_system/1.0/CLOUD_VISUAL_DESIGN.md`
- `docs/world/CLOUD_system/1.0/DEEP_ANALYSIS_2026-05-14.md`
- `docs/world/CLOUD_system/1.0/MASTER_PROMPT_GUIDE.md`
- `docs/world/CLOUD_system/2.0/DEEP_ANALYSIS_2026-06-02.md`
- `docs/world/CLOUD_system/3.0/CLOUD_OCEAN_MEDIUM_DETAILED_STEPS.md`
- `docs/world/CLOUD_system/3.0/CLOUD_OCEAN_MEDIUM_IMPLEMENTATION_PLAN.md`
- `docs/world/CLOUD_system/3.0/IMPLEMENTATION_LOG.md`
- `docs/world/CLOUD_system/3.0/STATUS.md`
- `docs/dev/RETROSPECTIVE_2026-08-04.md`
- `docs/dev/global roadmap/GLOBAL_ROADMAP.md`
- `docs/dev/global roadmap/GLOBAL_ROADMAP_EN.md`

### Вторичные документы
- `docs/Character/Skills/Battle/02_LORE.md`
- `docs/Character/Skills/real-time-combat/02_LORE.md`
- `docs/Character/Character-menu/sub_inventory-tab/INVENTORY_V2_REFACTOR.md`

## Текущий статус
- Инвентаризация выполнена.
- Архив обнаружен и исключён из рабочего контура.
- Батчи 1–3 выполнены.
- Финальная очистка описательных упоминаний выполнена.
- В рабочих документах остались только технические имена/идентификаторы: `CloudGhibli*`, `GhibliRamp` и ссылки на реальные ассеты/функции.
- Финальная проверка выполнена 20 августа 2026 г. по рабочей зоне `docs/` вне `docs/archive/`.
- Описательных стилистических упоминаний не осталось; технические идентификаторы сохранены намеренно.

## Правило контекстной замены
- Общий стиль: `западные комиксы + мягкие градиенты + контурная обводка + упрощённые формы`.
- UI: `comic-book UI language`, `мягкие переходы`, `outline`, `soft shadows`, без casual/hyper-casual подачи.
- Облака: `стилизованные объёмные облака`, `мягкие градиенты`, `контур/силуэт`, `упрощённое volumetric shading`.
- Техническое имя: сохранить путь/имя, но заменить комментарий/описание на нейтральное или актуальное.

## Продолжение
1. Прочитать канонические файлы перед редактированием.
2. Внести один тематический батч.
3. Повторно просканировать изменённые файлы и весь рабочий контур вне архива.
4. Обновить этот журнал: список изменённых файлов, оставшиеся технические упоминания и следующий батч.

## Журнал батчей

### Батч 0 — инвентаризация
- Статус: выполнен.
- Изменено: создан этот журнал.
- Остатки на момент инвентаризации: стилистические и технические упоминания Ghibli в рабочих документах; архив исключён.

### Батч 1 — канонические документы
- Статус: выполнен.
- Изменено:
  - `docs/ART_BIBLE.md`
  - `docs/COLABORATION.md`
  - `docs/gdd/GDD_00_Overview.md`
  - `docs/gdd/GDD_02_World_Environment.md`
  - `docs/gdd/GDD_11_Inventory_Items.md`
  - `docs/gdd/GDD_13_UI_UX_System.md`
  - `docs/gdd/GDD_14_Visual_Art_Pipeline.md`
  - `docs/gdd/GDD_15_Audio_System.md`
  - `docs/world/WorldLandscape_Design.md`
  - `docs/world/lights/lighting-plan.md`
  - `docs/UI/full rebuild plans/00_immersive_menu_strategy.md`
  - `docs/Character/Skills/Battle/02_LORE.md`
  - `docs/Character/Skills/real-time-combat/02_LORE.md`
  - `docs/Character/Character-menu/sub_inventory-tab/INVENTORY_V2_REFACTOR.md`
- Результат: стилистические формулировки переведены на западные комиксы, мягкие градиенты/переходы, outline и читаемую симплификацию; отдельно зафиксировано, что это не casual/hyper-casual.
- Проверка: в `ART_BIBLE`, `COLABORATION`, GDD 13, лоре персонажей и вспомогательных UI-документах стилистических совпадений больше нет. В GDD 00/02/14 и `WorldLandscape_Design` остались только ссылки на реальные технические имена `CloudGhibli*`.

### Батч 2 — Cloud Ocean и roadmap
- Статус: выполнен.
- Изменено: Cloud Ocean 3.0 plans/status/log/iterations, Cloud 1.0 visual/architecture/technical docs, global roadmap RU/EN и retrospective.
- Результат: стилистические `Ghibli-рампы`/`Ghibli style` заменены на цветовые рампы, мягкие градиенты, comic-book визуальный язык и читаемый силуэт. `GhibliRamp` сохранён только как техническое имя функции.

### Батч 3 — план разработки, HTML-прототипы и URP
- Статус: выполнен.
- Изменено: `docs/MMO_Development_Plan.md`, `docs/Fun/index.html`, `docs/roadmap.html`, `docs/unity6/UNITY6_URP_SETUP.md`, Cloud 1.0/2.0 onboarding, implementation и master prompt документы.
- Результат: UI/CSS-комментарии, roadmap-классификация и описания пайплайна используют comic-book/outline/soft-gradient терминологию.

### Финальная очистка описаний
- `docs/world/CLOUD_system/1.0/DEEP_ANALYSIS_2026-05-14.md`: два описательных упоминания переведены на нейтральный термин `cloud shader (техническое имя CloudGhibli)`.
- `docs/Fun/index.html`: последний CSS-комментарий переведён на `soft comic-book treatment`.
- Технические совпадения после очистки считаются допустимыми только если это реальные имена ассетов или функций.

### Финальная проверка — 20 августа 2026 г.
- Проверены: GDD, Cloud Ocean, world, dev, UI, Character, HTML-прототипы, roadmap и Unity 6 setup.
- `Ghibli/гибли` больше не используется как описание визуального стиля.
- Сохранены только реальные технические ссылки: `CloudGhibli.shader`, `CloudGhibli_OutlineV*.shader`, `CloudGhibli_OutlineV2`, `GhibliRamp` и связанные таблицы фактических материалов/ассетов.
- `docs/archive/` не редактировался и не включался в рабочий контур.
- Итоговый словарь: западные комиксы, мягкие цветовые/световые переходы, outline, читаемые силуэты и упрощённые формы без casual/hyper-casual стилистики.
