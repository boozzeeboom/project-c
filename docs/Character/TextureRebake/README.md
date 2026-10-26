# T-CHAR-UVRB v2 — адаптивная переразвёртка моделей

Пайплайн больше не привязан к персонажам: обрабатывает полигональные объекты по отдельности,
сохраняет структуру сборки и оценивает кандидаты UV численно. **Это не гарантия улучшения любой модели.**
Если безопасного кандидата нет, `auto` оставляет исходные UV/материалы и пишет `unchanged`.
Неподдерживаемый материал или повреждённая геометрия дают явную ошибку до публикации результата.

- [Алгоритм и первоисточники](01_ALGORITHM.md)
- [История и фактические проверки](ITERATIONS.md)
- Дизайн/план: `docs/dev/T-CHAR-UVRB_v2.md`
- Код: `Tools/Blender/CharacterRebake/`

## Контракт v2 и отличие от v1

1. **Нет join или weld исходных мешей.** Вершины, topology, веса, shape keys и объекты сохраняются.
   Точные дубли на однозначных встречных boundary edges могут соединяться только в аналитической копии;
   UV переносятся обратно по сохранённым corner IDs. `--connect-splits off` отключает это.
2. Существующие UV остаются. Добавляется `UVRB_v2`; исходные image/normal nodes закрепляются на старых UV.
3. Слоты материалов и индексы граней сохраняются. Константы, например Metallic/Roughness, не заменяются пресетом.
4. Связанные входы одного Principled запекаются отдельно: RGB/scalar через Emit, Normal через tangent-space Normal.
   Нормали перепекаются на исходной несваренной геометрии, не на аналитической копии.
5. **Основной выход теперь `.blend` с packed images**, а не автоматический FBX. Это намеренное изменение контракта:
   только нативное сохранение проверяется на структуру/данные/материал без потерь обменного формата.
6. `--export-format fbx|glb` дополнительно создаёт обменный файл. Его статус — `exported_NOT_VERIFIED`;
   наличие файла не подтверждает перенос Blender shader graphs/constraints/анимации в Unity.
7. Исходники и старый `Rebaked/` v1 не изменяются. Новая папка по умолчанию — `Rebaked_v2/`.

`rebake_character.py` оставлен как точка входа в v2. Реализация v1 находится в Git-коммите `dd07e70a`.
Старые параметры не полностью эквивалентны: ненулевой `--weld` запрещён, `--save-blend` может указывать
только основной native-файл внутри `--out-dir`. Проверка старого кэша v1 не означает успех v2.

## Входы и ограничения

Импортёры: `.blend`, `.fbx`, `.glb`, `.gltf`, `.obj`, `.ply`, `.stl`.
Поддерживается полигональная геометрия: triangles/quads/n-gons, несколько объектов и материалов,
UV-текстуры, постоянные цвета, поддерживаемые процедурные входы Principled, модели без UV.

Preflight отказывает при Mix Shader/другом surface BSDF, Volume/Displacement, node groups/OSL,
анимированных материалах, scene/view-dependent узлах, некоторых связанных vector-входах,
отсутствующих изображениях/UV, geometry modifiers кроме Armature, вырожденных полигонах и переполнении UV layers.
Это ограничения текущей реализации, а не фундаментальная неприменимость UV rebake.
Unity ShaderGraph этим Blender-пайплайном не вычисляется. Не-poly объекты сами не переразворачиваются.

Результаты численных тестов не означают, что все перечисленные форматы и все варианты рига проверены.
Точная матрица проверок находится в `ITERATIONS.md`. Визуальная приёмка и Unity import: **NOT RUN**.

## Запуск одной модели

Проверен Blender **5.2.3 LTS**, установленный в
`W:\SteamLibrary\steamapps\common\Blender\blender.exe`. Версия не обновлялась.

```bat
set "BLENDER_EXE=W:\SteamLibrary\steamapps\common\Blender\blender.exe"

rem Только анализ кандидатов; не пишет новую модель и текстуры.
"%BLENDER_EXE%" -b --factory-startup --python-exit-code 1 -P Tools\Blender\CharacterRebake\rebake_model.py -- ^
  --src Assets\Generated_Models\CL_F_WPN_KNIFE_01_STEEL\CL_F_WPN_KNIFE_01_STEEL.fbx --no-export

rem Native-результат вне Assets, чтобы Unity не импортировал промежуточную работу.
"%BLENDER_EXE%" -b --factory-startup --python-exit-code 1 -P Tools\Blender\CharacterRebake\rebake_model.py -- ^
  --src Assets\Generated_Models\CL_F_WPN_KNIFE_01_STEEL\CL_F_WPN_KNIFE_01_STEEL.fbx ^
  --out-dir Tools\Blender\CharacterRebake\_work\knife_output
```

`--audit-only` выполняет импорт и preflight без развёртки. `--no-export` дополнительно измеряет кандидаты,
но не печёт изображения. Полный режим публикует выход только после native roundtrip QA.

## Batch

```bat
rem Только явный набор папок. Без первого аргумента wrapper завершится ошибкой.
Tools\Blender\CharacterRebake\rebake_all.bat "PC_Char_*" "--max-res 4096"
```

Batch использует один Blender-процесс на модель последовательно. Кэш проверяется worker-ом по версии,
коду, аргументам, исходнику, импортированным данным и внешним image dependencies.
Сводка `Tools/Blender/CharacterRebake/_uvrb_summary_v2.csv` считается успешной только при нулевом exit code,
свежем `status=success`, успешном native QA и существующих выходных файлах.
Отсутствие моделей/неоднозначный исходник/ошибка worker дают ненулевой exit code.

`--force` явно разрешает новый прогон; предыдущая выходная папка сохраняется как `.backup_<timestamp>`.
Нельзя направлять выход в папку исходника или в её родителя. JSON/CSV/логи запрещены внутри `Assets/`.

## Параметры

| Параметр | Дефолт | Назначение |
|---|---|---|
| `--profile` | auto | auto/hard/organic; это выбор поиска, не распознавание семантики модели |
| `--policy` | auto | сохраняет исходник без измеримого выигрыша; rebuild запрашивает новый кандидат |
| `--res` | 0 | автоматический бюджет по текстурам/UV; ненулевое значение задаёт начальное разрешение |
| `--max-res` | 4096 | верхний предел разрешения; не выполняется молчаливое уменьшение большого исходника |
| `--supersample` / `--samples` | 2 / 4 | supersampling и Cycles samples |
| `--smooth-iters` | auto | несглаженный кандидат и, по признакам сетки, ограниченный сглаженный; override 0..600 |
| `--angle` | auto | Smart UV angle limit; ручной override 1..89° |
| `--sharp-angle` | 50 | геометрическая граница, которую не должен пересекать merge |
| `--connect-splits` | exact | только аналитическое соединение точных дубликатов; off отключает |
| `--min-island-frac` | 0.004 | порог мелкого чарта от площади его связной оболочки |
| `--border-smooth` | 2 | большинство по всем edge-neighbours, включая n-gons |
| `--unwrap` | MINIMUM_STRETCH | SLIM по умолчанию; ANGLE_BASED/CONFORMAL доступны явно |
| `--margin-px` / `--bleed-px` | 8 / 8 | фактический packing gap не меньше `2*bleed+2` |
| `--subpixel-area` | 0.001 | до 0.1% площади поверхности на островах уже 2 px; 0 = строго каждый остров |
| `--tex` | — | явный BaseColor только для одного mesh/slot; не распространяется на все материалы сборки |
| `--export-format` | native | дополнительно fbx/glb, но не утверждение о переносе в Unity |
| `--gpu` | off | явный запрос GPU; недоступное устройство — ошибка, не скрытый CPU fallback |

## Выход и отчёт

Native-файл: `<out-dir>/<Name>_Rebaked_v2.blend`.
Карты: `<out-dir>/<index>_<object>_<channel>.png|exr` — один набор на объект, исходные слоты остаются раздельными.
Base Color и Normal — PNG; остальные каналы — float EXR (HDR emission и scalar >1 не обрезаются до 8-bit).
Packed images позволяют открыть native-файл без внешних текстур исходника.

JSON по умолчанию: `Tools/Blender/CharacterRebake/_reports/<Name>/<Name>_Rebaked_v2_report.json`.
Диагностика имеет отдельные `_audit_report`/`_measure_report` и не заменяет успешный bake-кэш.

- Общий `status`: success / failed / audited / measured.
- На каждый объект: rebaked / unchanged / measured; причина, все кандидаты и фактические параметры.
- `valid` для нового UV требует нулевые flips/degenerate/overlap и координаты в unit square.
- `uv_area_sum` — сумма площадей UV. При overlap исходника это **не** площадь покрытия.
- `subpixel_surface_fraction` — отдельно измеренный риск потери микродеталей; не визуальный сертификат.
- `analytical_joined_vertices` — число объединённых вершин только копии для расчёта UV.
- `invariants` / `native_roundtrip` — проверки исходных данных и повторного открытия native-результата.

`success` с `unchanged` на всех объектах означает безопасное сохранение, **не улучшенный атлас**.
Рабочие папки `_work/`, `_reports/`, CSV и Generated_Models не коммитятся.

## Проверки

`test_uvrb_v2.py` создаёт синтетические тестовые меши только вне Assets и проверяет UV, данные и значения карт.
`test_project_models_v2.py` измеряет три явно перечисленных исходника проекта; default без экспорта.
Ни один из этих тестов не запускает Unity, Play Mode, рендер кадра или скриншот.

Unity import, выбор UV-канала в целевом шейдере, визуальные швы/детали, анимация в игре и приёмка FBX/GLB
остаются пользовательскими проверками, **NOT RUN**. Не считать `.blend`-roundtrip доказательством их выполнения.
