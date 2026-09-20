# optimization — Iteration Log

## Итерация от 2026-09-20 (T-LOD01)

**Задача (слова пользователя):** продумать систему прорисовки LOD + контроль игроку в ESC → Видео → Дальность прорисовки. Удалённые террейны выглядят хорошо — не выключать, а сделать фоном. 4 уровня (ближнее/среднее/дальнее/ультрадальнее). Ультрадальнее пока выключить (2+ игровых сцен нет). Точных замеров нет — значения не хардкодить, вынести в менеджер.

**Дизайн:** `LOD_VIEW_DISTANCE_DESIGN.md`. **План:** `IMPLEMENTATION_PLAN.md` (Phase 0–4 выполнены как код, Phase 5 отложена).

### Что реализовано

- `Scripts/Core/ViewDistance.cs` (NEW): enum `Near/Medium/Far/Ultra` (дефолт `Medium`).
- `Scripts/Core/SettingsManager.cs`: `ViewDistance` + `KEY_VIEW_DISTANCE` + `OnViewDistanceChanged` + `SetViewDistance` + Load/Save/лог; плюс `OnQualityLevelChanged` (инвок в `SetQualityLevel`).
- `Scripts/World/ViewDistanceConfig.cs` (NEW): ScriptableObject-менеджер значений (3 пресета: near/medium/far). Все числа правятся в инспекторе без перекомпиляции; `DefaultFor` — кодовый fallback. Ассет `Assets/_Project/Resources/Config/ViewDistanceConfig.asset` (создан через `AssetDatabase.CreateAsset`, .meta сгенерировал Unity).
- `Scripts/World/ViewDistanceApplier.cs` (NEW): static-апplier (образец `GraphicsEffectsApplier`): `RuntimeInitializeOnLoadMethod` + `sceneLoaded` + оба события; владеет `camera.far / lodBias / shadowDistance / terrain.heightmapPixelError+basemapDistance`; террейн ищет по имени `Terrain_0_0`, иначе первый активный; `Ultra` → лог + Far.
- `Scripts/World/DetailDistanceCuller.cs` (NEW): визуальный culling групп `RuinsValleys` целыми хамлетами (`Renderer.enabled`, гистерезис 10%, чек 0.5 с, позиции живьём — FO-safe). Хостер `ViewDistanceRuntime` (DontDestroyOnLoad) создаёт апplier, правок сцен не потребовалось.
- `Scripts/Core/SpringArmCamera.cs`, `WorldCamera.cs`: `far` в `Awake` — из `ViewDistanceApplier.ResolveCameraFar()` (было `1000000f`).
- `Scripts/UI/EscMenu/GraphicsSettingsSection.cs`: секция «Дальность прорисовки» — dropdown 3 пресета + хинт про Ультра.

### Отклонения от плана (зафиксировано)

1. **Applier — static, а не MonoBehaviour в сцене.** Правок `WorldScene_0_0.unity` не потребовалось вообще (0 scene-диффов). Камеры берут far в своём `Awake` (покрывает runtime-спавн рига), остальное — по `sceneLoaded`/событиям.
2. **Ultra — не 4-й disabled-пункт, а хинт-лейбл.** `CustomDropdown` не умеет disabled-пункты; 4-й выбираемый пункт был бы ловушкой (выбрал → откат). Dropdown из 3 + серый хинт «Ультра … появится с новыми сценами».
3. **Конфиг — в `Resources/Config/`**, чтобы static-апplier грузил без ссылок сцены. Паттерн `Resources.Load` в проекте уже есть (`WorldData`).

### LOC-проход (отдельно, UI_Table)

Ключи (сейчас RU-fallback в коде, перевод подхватится автоматически для choices через `Loc.Get(key, ru)`):
`ui.esc_menu.section.view_distance`, `ui.esc_menu.label.view_distance`,
`ui.esc_menu.view_distance.near|medium|far`, `ui.esc_menu.view_distance.ultra_hint`.

### Верификация (сделано)

- `refresh_unity` (force, compile=request, wait_for_ready) → Console 0 errors (единственная ошибка в пути — `CS0118 Scene vs ProjectC.World.Scene` — исправлена квалификацией типа).
- Значения ассета проверены чтением: near/far 30000, medium 60000, far 120000.

### Ручная проверка (пользователь, Play не запускался агентом)

1. ESC → Видео → «Дальность прорисовки»: 3 пункта + хинт; переключение меняет картинку мгновенно; перезапуск хранит пресет.
2. Эталон Phase 0: 3 скрина с одной точки (Примум) на каждом пресете — L2-фон узнаваем (силуэты Main/East/West + снег).
3. F8 → `runtimeRebase.Completed` + 0 errors; F9 → возврат; руины/террейн на месте на всех пресетах.
4. Profiler до/после на «Близкой» (тени/overdraw) — записать цифры сюда.
5. `DistantFocus` (взгляд вниз → горы плывут; на горы 1 с → резкие) не сломан.
6. Калибровка чисел — правится в `ViewDistanceConfig`-ассете, код не трогать.

### Запреты соблюдены

- Террейн никогда не выключается (кода выключения нет конструкцией). Стриминг/`loadRadius` не тронуты. Нет второго `NetworkManager`, `ClientSceneLoader`/`ScenePlacedObjectSpawner` не тронуты. `.meta`/`.asmdef` руками не писались. Мировые `Vector3` между кадрами не хранятся.
