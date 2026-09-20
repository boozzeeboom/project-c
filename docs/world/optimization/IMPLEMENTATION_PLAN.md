# План реализации: дальность прорисовки (LOD + ESC‑Видео)

**Дизайн:** `LOD_VIEW_DISTANCE_DESIGN.md` | **Дата:** 2026-09-20
**Принцип правок:** минимальные диффы, стиль `ProjectC.*`, `[SerializeField] private`, комментарии на русском, тикеты в комментариях. Не трогать: `Library/Temp/Builds`, `ProjectSettings/*` без спроса, `NetworkManager`, `ClientSceneLoader`, `ScenePlacedObjectSpawner`, `.meta`/`.asmdef` руками.

---

## Phase 0 — Замеры базы (без кода, ~0.5 дня)

1. Открыть `WorldScene_0_0`, записать текущие значения: `Terrain_0_0.heightmapPixelError` (ожид. 200), `basemapDistance` (ожид. 20000), камеры `far` (ожид. 1000000), `QualitySettings.lodBias`, `shadowDistance`.
2. Снять 3 скриншота с одной точки (Примум → вид на Main/East/West): общий план + средний + ближний. Это эталон «нравится как выглядит» — L2‑фон после изменений сверяем с ним.
3. Profiler: кадр в покое + в полёте; записать FPS, draw calls, tris, время Rendering. Источник цифр для калибровки колец.
4. Проверить туман/видимость горизонта: на какой дистанции силуэт тонет в тумане (это верхняя граница осмысленного `far`).

**Выход:** таблица «было» + эталонные скрины. Без них числа Phase 1 — гадание.

## Phase 1 — `SettingsManager.ViewDistance` (~0.5 дня)

Файл: `Assets/_Project/Scripts/Core/SettingsManager.cs` (+ `ViewDistance.cs` при желании отдельным enum‑файлом в `Core/`).

1. `public enum ViewDistance { Near = 0, Medium = 1, Far = 2, Ultra = 3 }` (namespace `ProjectC.Core`).
2. Ключ `KEY_VIEW_DISTANCE = "Settings.ViewDistance"`; свойство `ViewDistance ViewDistance { get; private set; } = ViewDistance.Medium`.
3. Событие `OnViewDistanceChanged`.
4. `SetViewDistance(ViewDistance v)`: ранний return при равенстве; `PlayerPrefs.SetInt + Save + Invoke`. `Ultra` **не блокировать в менеджере** (блокировка — слой UI + апplier), менеджер хранит честно.
5. `Load()`: `ViewDistance = (ViewDistance)PlayerPrefs.GetInt(KEY_VIEW_DISTANCE, 1)` + clamp 0..3.
6. `Save()`: дописать ключ.
7. `ApplyAll()`: ничего нового не применять напрямую (применяет апplier на сцене); только лог текущего значения. Причина: `SettingsManager` — static без ссылок на сцену, как уже сделано для DoF/Edge (применение — через `GraphicsEffectsApplier`).

**Проверка:** Compile, Console 0 errors. Unit‑проверка руками: `SetViewDistance` → PlayerPrefs пережил перезапуск.

## Phase 2 — `ViewDistanceApplier` + `ViewDistanceConfig` (~1–2 дня)

Новые файлы в `Assets/_Project/Scripts/World/` (или `Rendering/` — решить по prior art на месте; террейн — `World`, камеры — `Rendering`; предложение: конфиг в `World/`, апplier в `World/` рядом со стримингом):

1. `ViewDistanceConfig : ScriptableObject` (`CreateAssetMenu ProjectC/View Distance Config`):
   - 3 активных пресета (массив/структура `ViewDistancePresetData { cameraFar, lodBias, terrainPixelError, terrainBasemapDistance, shadowDistance, detailCullDistance }`) со стартовыми числами из дизайна §3.
   - `Ultra` в конфиге **нет** (резерв без данных — нечего настраивать).
2. `ViewDistanceApplier : MonoBehaviour`:
   - `[SerializeField] ViewDistanceConfig config`; разовый поиск `Terrain_0_0` (`FindAnyObjectByType<Terrain>` + проверка имени/тега; кэш ссылки в `OnEnable`, не координат).
   - `OnEnable`: подписаться на `SettingsManager.OnViewDistanceChanged` + применить текущее значение.
   - `OnDisable`: отписаться.
   - `Apply(ViewDistance v)`:
     - `Ultra` → ранний return + `Debug.Log("[ViewDistance] Ultra зарезервирован: сцен 2+ нет")` (заготовка под Phase 5: `if (loadedScenes > 1) ...`).
     - Камеры: `SpringArmCamera` (через `FindAnyObjectByType`, поле `CameraComponent` — проверить имя на месте) + все `WorldCamera` → `farClipPlane = preset.cameraFar`.
     - `QualitySettings.lodBias`, `QualitySettings.shadowDistance`.
     - `terrain.heightmapPixelError`, `terrain.basemapDistance`.
     - Детали: `DetailCuller` (см. п.3) → `detailCullDistance = preset.detailCullDistance`.
   - Floating Origin: мировых `Vector3` не хранить — 🟢 без хуков. В комментарии кода зафиксировать запрет кэширования мировых позиций.
3. `DetailCuller` (минимальный, решение на месте — отдельный компонент или метод апplier):
   - Вход: корень `RuinsValleys` (60 хамлетов‑групп), дистанция `detailCullDistance`.
   - Каждый N‑й кадр (дефолт 0.5 с, как `WorldStreamingManager.updateInterval`): дистанция от камеры до центра группы → `Renderer.enabled` (или `GameObject.SetActive` на группе — выбрать по замеру инстансинга; старт: `Renderer.enabled`, т.к. дешевле и не трогает иерархию при FO‑сдвиге).
   - Только визуально; чанки/стриминг не трогает.
4. Разместить `ViewDistanceApplier` в `WorldScene_0_0` (под корень сцены, рядом со стримингом) + создать `ViewDistanceConfig.asset` в `Assets/_Project/Data/World/`.

**Проверка:** Compile → `refresh_unity` (force, compile=request, wait_for_ready) → `read_console` 0 errors. F8 (сдвиг мира) → `runtimeRebase.Completed` + террейн/руины на месте. Смена значения в инспекторе конфига → картинка меняется без рестарта.

## Phase 3 — ESC‑меню «Видео → Дальность прорисовки» + локализация (~0.5–1 день)

Файл: `Assets/_Project/Scripts/UI/EscMenu/GraphicsSettingsSection.cs` (дописать секцию, не переписывать).

1. После секции «Качество»/«Экран», до «Эффектов»:
   ```csharp
   panel.Add(SettingsWidgets.CreateSectionHeader("ui.esc_menu.section.view_distance"));
   panel.Add(SettingsWidgets.CreateDropdown("ui.esc_menu.label.view_distance",
       new List<string> { Loc.Get("ui.esc_menu.view_distance.near"), ... },
       (int)SettingsManager.ViewDistance,
       idx => SettingsManager.SetViewDistance((ViewDistance)idx)));
   ```
   Детали по месту: проверить, умеет ли `CustomDropdown` disabled‑пункт. Если нет — пункт «Ультра (скоро)» либо (а) не добавляем в список, а показываем отдельной `Label`‑хинтом `ui.esc_menu.view_distance.ultra_hint`, либо (б) добавляем пункт, но при выборе откатываем значение + показываем хинт. Решение зафиксировать в ITERATIONS.
2. Локализация `UI_Table`: добавить ключи (минимум ru+en, остальные — следующим LOC‑проходом по образцу «Эффектов»):
   - `ui.esc_menu.section.view_distance` — «Дальность прорисовки» / «View distance»
   - `ui.esc_menu.label.view_distance` — «Дальность прорисовки» / «View distance»
   - `ui.esc_menu.view_distance.near` — «Близкая — максимум FPS» / «Near — max FPS»
   - `ui.esc_menu.view_distance.medium` — «Средняя — баланс» / «Medium — balanced»
   - `ui.esc_menu.view_distance.far` — «Дальняя — кинематографично» / «Far — cinematic»
   - `ui.esc_menu.view_distance.ultra_hint` — «Ультра появится, когда откроются соседние регионы» / «Ultra unlocks with new regions»
   - Источник правды — `Assets/_Project/Localization/Export/UI_Table.csv` + пересборка `Settings/Localization/UI_Table_*.asset` штатным пайплайном (руками ассеты не править).
3. RU‑fallback до LOC‑прохода — русскими литералами, как уже сделано для «Эффектов» (пометить `// LOC-проход` комментарием).

**Проверка:** ESC → Видео → dropdown переключает картинку мгновенно; перезапуск → значение сохранилось; Ultra‑пункт недоступен/показывает хинт.

## Phase 4 — Верификация и калибровка (~0.5–1 день)

1. Compile: Console 0 errors после каждого изменения (обязательно `refresh_unity` + `read_console` по правилу MCP).
2. Скриншоты 3 пресетов с эталонной точки Phase 0: L2‑фон узнаваем (силуэт Main/East/West + снег), поп‑артефактов при переключении нет.
3. Profiler до/после на «Близкой»: ожидаем падение shadow‑нагрузки и overdraw; записать цифры в ITERATIONS.
4. F8 → `runtimeRebase.Completed` + 0 errors; F9 → возврат. Руины/террейн на месте на всех пресетах.
5. Чек‑лист приёмки:
   - [ ] Террейн виден на всех пресетах (off невозможен конструкцией — нет кода выключения).
   - [ ] Ultra нельзя включить игроком.
   - [ ] Перезапуск хранит пресет.
   - [ ] Стриминг чанков работает как раньше (фильтр сцен не тронут).
   - [ ] `DistantFocus` (взгляд вниз → горы плывут; взгляд на горы 1 с → резкие) не сломан.
6. Записать `ITERATIONS.md` в этой папке: что замерено, какие числа калибровки изменены относительно стартовых, скрины (пути).

## Phase 5 — Ultra (отложено, условие: игровых сцен ≥ 2)

1. `ViewDistanceApplier.Apply(Ultra)`: снять ранний return; дальний фон = импостор/упрощение соседних `WorldScene_X_Z` ( billboard‑силуэты или `basemapDistance=max` + отключение их деталей), включается только при `WorldSceneManager` loadedScenes > 1.
2. UI: разблокировать 4‑й пункт dropdown.
3. Отдельный дизайн‑апдейт этого раздела + новые замеры (межсценовые дистанции ~80–350 км по `WorldLandscape_Design.md` §2.3 — far и туман пересчитываются).

---

## Файлы‑кандидаты (итог)

| Действие | Путь |
|---|---|
| Изменить | `Assets/_Project/Scripts/Core/SettingsManager.cs` |
| Создать | `Assets/_Project/Scripts/World/ViewDistance.cs` (или внутри SettingsManager) |
| Создать | `Assets/_Project/Scripts/World/ViewDistanceConfig.cs` + `Assets/_Project/Data/World/ViewDistanceConfig.asset` |
| Создать | `Assets/_Project/Scripts/World/ViewDistanceApplier.cs` (+ `DetailCuller.cs` при необходимости) |
| Изменить | `Assets/_Project/Scripts/UI/EscMenu/GraphicsSettingsSection.cs` |
| Изменить | `Assets/_Project/Localization/Export/UI_Table.csv` + пересборка `Settings/Localization/UI_Table_*.asset` |
| Изменить | `Assets/_Project/Scenes/World/WorldScene_0_0.unity` (размещение апplier) |
| Создать | `docs/world/optimization/ITERATIONS.md` (по факту работ) |

## Оценка

Phase 0–4: **3–5 дней** одного разработчика (без арта, без сетевых изменений). Phase 5: отдельная оценка после появления 2‑й игровой сцены.
