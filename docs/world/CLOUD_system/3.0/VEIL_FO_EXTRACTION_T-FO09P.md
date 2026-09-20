# T-FO09P — Veil вынесен из CloudManager в отдельный VeilController + FO-адаптация

Date: 2026-09-20. Коммиты: `4e61650e` (вынос + FO), `8ced077c` (фикс startup gate).

## Исходное состояние

`VeilRaymarchMeshController` висел компонентом на `CloudManager` в `BootstrapScene`.
`CloudManager` выключен целиком (`m_IsActive: 0`, скрипт disabled) и FO-адаптацию не
проходил — вместе с ним молчал и veil.

## Что сделано

1. **`VeilRaymarchMeshController.cs`** — FO-адаптация (класс 🟡: хранит мировую Y):
   - синглтон `Instance`, `Awake` + `DontDestroyOnLoad` (убрано из `Initialize`);
   - `ApplyRebaseTranslation(t)`: `BaseVeilHeight += t.y`, пуш uniforms
     `_VeilBottom/_VeilTop`, перестановка плейна по Y. XZ не трогается —
     `Update` каждый кадр дотягивает плейн за игроком (самолечение).
   - знак сверен с остальными хуками (`_deathY += t.y`, штормы `+= t`).
2. **`GlobalMotionControlledRebaseSlice.cs`** — `ShiftVeilPlane` + маркер
   `runtimeRebase.VeilShifted` на всех трёх путях: success, rollback (−T),
   клиентский broadcast-handler (`;veil=` в `ClientShiftApplied`).
3. **`BootstrapScene.unity`** — новый активный корень `VeilController`
   (Transform + контроллер с прежними настройками, без `NetworkObject`,
   по образцу `WindManager`); компонент veil удалён с `CloudManager`.
   Старый `CloudManager` не тронут, остаётся выключенным.

## Инцидент startup gate (важно для будущих выносов)

Первая версия `VeilController` несла самодельный `GlobalSceneSourceMarker`
с выдуманным `_sourceId` — pilot отказывал в старте:
`scene_preparation:extra_baked_marker_outside_authored_roots_or_network_objects`
(`GlobalSceneNativeExecutor.cs:209-210`: любой маркер обязан входить в запечённый
каталог). Фикс: маркер снят — обычный authored-корень без маркера гейт не проверяет.
Правило: **новые GO в BootstrapScene — без GlobalSceneSourceMarker** (маркер получают
только через bake-инструмент каталога). FO-хук от маркера не зависит (резолв по синглтону).

## Семантика высоты: authored vs runtime (почему в игре −1300, а в сцене 1200)

- `1200` в сцене (на момент работ; позже поднято до `2200`, см. ниже) — высота
  в исходном фрейме, до сдвигов.
- Игрок спавнится в ~40 км от origin → почти сразу срабатывает авторебейс
  (`auto_threshold`, порог 256 м) и сдвигает мир на `T` (`T.y ≈ −2500`).
- Veil едет вместе с миром (`1200 → ~−1300`), игрок — на тот же `T`.
  Относительная геометрия не меняется: завеса ~1300 ниже игрока до и после.
- Рантайм-значение после ребейса руками не тюнить: «поднять до −250» ломает
  авторское соотношение (завеса заедет в слой города). Тюнинг — только сценой.
- 2026-09-20: authored поднят `1200 → 2200` правкой пользователя в редакторе
  (подхвачено в рабочий коммит документации).

## Проверка

- Compile: validate обоих скриптов — 0 errors.
- Ручная: Play → Start Host (гейт проходит) → F8 → `VeilShifted layers=1` + `Completed`,
  дельта `playerY − veilPlaneY` константа (~1300); F9 → возврат.
- `Test_VeilRaymarch` в сцене — статичный тестовый квад без скрипта, не дубликат, не трогали.

## Открытое

- Межсессионный стык: veil всегда стартует с authored 1200, игрок — из сейва
  с persist-коррекцией. Если проявится рассогласование между сессиями — отдельный тикет.
