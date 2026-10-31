# T-CULL01 / Stage 0 — brief для отдельного агента (baseline + фиксы)

**Дата:** 2026-10-31 · **Базовый коммит:** `831fd759` · **Проект:** `C:\Projects\ProjectC_client`
**Разрешение пользователя:** **этому агенту разрешено самостоятельно запускать Play Mode** для Stage 0.
Ограничения: не сохранять сцену, не делать скриншоты без отдельного разрешения, не менять `ProjectSettings`/
`Packages`/сцены, не push; после замеров — остановить Play Mode и вернуть рабочее дерево чистым.

**Родительские документы (прочитать до работы):**
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md` — Stage 0.
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md` — правила, будущие пакеты H1–H7.
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\02_FACTS_AND_MEASUREMENTS.md` — текущие числа/настройки.
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\06_EXPERIMENTS_AND_ROADMAP.md` — методика замеров.

**Статус Unity:** редактор уже открыт, `WorldScene_0_0` загружена, Play Mode **не** запущен.
**Git:** рабочее дерево должно быть чистым на старте; коммит Stage 0 — только после gate, без push.

---

## 0. Цель Stage 0

1. Снять **baseline** «до» так, чтобы все последующие этапы можно было сравнивать.
2. Убрать два известных бага/иллюзии:
   - `ViewDistanceApplier` меняет `QualitySettings.shadowDistance`, который URP игнорирует (no-op);
   - `DetailDistanceCuller` использует `Camera.main` при двух камерах с тегом `MainCamera`.
3. Не менять визуал/геймплей/сцены. Всё, что меняется в коде — минимальные диффы + комментарии на русском.

**Время:** 2–3 дня, из них 1–2 дня — замеры в Play Mode, 0.5–1 день — фиксы.

---

## 1. Baseline: как мерить

### 1.1. Подготовка

- Открыть сцену `WorldScene_0_0` (+ `BootstrapScene` аддитивно, если ещё не загружена).
- Запустить **Play Mode → Host** (нужен спавн игрока/рига; без Host в NGO игрок не появится).
- Дождаться прогрева ~**1500 кадров**; FPS/`UnityStats` в первых кадрах недостоверны.
- Камера: **геймплейный риг** (`ThirdPersonCamera` / `SpringArmCamera`), **far 30 000 м** (пресет Medium),
  `near` по умолчанию; **не** брать статичную `BootstrapScene/MainCamera` (она в 240 км).
- Тени: URP-ассет `ProjectC_URP.asset`: `m_ShadowDistance = 1000`, `m_ShadowCascadeCount = 2`
  (не менять в Stage 0).
- `AtmosphereCamera` (меню, far 10) — выключена/вне замера.
- Scene View — увести в небо; Game View — на геймплейную камеру.
- Включить `Window → Analysis → Rendering Statistics` (или читать `UnityStats` программно).
- Записать: GPU/CPU, разрешение Game View, качество, версию Unity/URP, `frameCount` в момент снятия.

### 1.2. Четыре точки (на каждой — 3 замера, медиана)

| # | Точка | Позиция / вид |
|---|---|---|
| 1 | Центр площади | `(39593.1, 2509.6, 39833.8)`, yaw 337.5°, fov 60 |
| 2 | Узкая улица | улица в PRIMUM, камера на уровне глаз, смотрит вдоль улицы |
| 3 | Крыша | крыша дома с обзором квартала/площади |
| 4 | Порт / корабли | у тяжёлого корабля: `(40064, 2512, 39449)`, смотреть на корабль |

Как выставлять камеру: допустим **временный runtime-скрипт/Editor-окно**, которое в Play Mode двигает
камеру рига и пишет CSV. **Сцену не сохранять**; после остановки Play Mode все изменения откатываются.
Если удобнее — двигать камеру вручную в Play Mode и записывать показания; главное — одинаковая методика во
всех прогонах.

### 1.3. Что записывать

Колонки CSV:
`point,far,shadowDistance,shadowCascades,drawCalls,setPassCalls,triangles,fps,frameCount,gpuCpu,resolution,notes`

Дополнительно на каждой точке:
- Frame Debugger → разложение: `Camera` (opaque/depth-prepass) и `Directional Shadow Map` (каскад 0/1);
  записать число `Draw Mesh` в каждом проходе;
- Profiler → Rendering: CPU `Culling`/`Camera.Render`, если доступно;
- Console: 0 errors (warnings — записать);
- `AtmosphereCamera` точно выключена.

### 1.4. Ожидаемые ориентиры (для проверки стенда)

| Конфиг | DC | Комментарий |
|---|---|---|
| A: shadow 1000/2, двери вкл | **~26 981** | измерение 30.10 |
| B: shadow 250/1, двери вкл | ~21 118 | для сравнения, если решите снять |
| A без дверей | ~4 954 | |
| B без дверей | ~3 352 | |
| Все корабли (модель) | ~4 917 | в порту могут доминировать |

Если baseline отличается от ориентира больше чем на **5–10 %** — не начинать фиксы, сначала выяснить
причину (камера не та, far не тот, сцена/качество, shadow-настройки).

### 1.5. FO-регресс

- В Play Mode: **F8** → `runtimeRebase.Completed`, объекты на месте, 0 errors.
- **F9** → возврат, 0 errors.
- Записать в notes.

---

## 2. Фиксы (после baseline)

### 2.1. `Camera.main` в `DetailDistanceCuller` — делать обязательно

Файл: `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\DetailDistanceCuller.cs`
Проблема: строка **87** — `var cam = Camera.main;`. В проекте **две** камеры с тегом `MainCamera`
(статичная `BootstrapScene/MainCamera` в 240 км и runtime-риг `ThirdPersonCamera`), `Camera.main` может
вернуть статичную — дистанции руин считаются не от игрока.

Сделать:
- заменить `Camera.main` на выбор «настоящей» геймплейной камеры: приоритет — `SpringArmCamera.CameraComponent`
  / `ThirdPersonCamera` с тегом `MainCamera`, ближайшая к игроку/руину; не использовать `Camera.main`;
- эталон: `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\PrimumLightPool.cs` — **33, 159–219**
  (`FindFocusCamera`, явный выбор камеры);
- сохранить `checkInterval`, гистерезис 10 %, FO-safe поведение (позиции читать живьём);
- комментарий на русском: почему не `Camera.main` (T-CULL01/Stage 0).

Приёмка: в Play Mode руины куллятся по камере игрока; F8/F9 без ошибок; Console 0 errors.

### 2.2. `ViewDistanceApplier` shadowDistance — no-op

Файл: `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\ViewDistanceApplier.cs`
Строка **75**: `QualitySettings.shadowDistance = p.shadowDistance;`. URP читает
`UniversalRenderPipelineAsset.shadowDistance` (`ProjectC_URP.asset:58–59`), поэтому строка ничего не делает.

**Решение по умолчанию (если Lead/пользователь не сказал иначе): вариант A**
- убрать мёртвое присваивание `QualitySettings.shadowDistance` из `ApplyAll()` и убрать/пометить как
  deprecated поле `shadowDistance` в `ViewDistanceConfig` (не менять URP-ассет в Stage 0);
- обновить комментарий/лог: тени управляются URP-ассетом (1000/2), позже — `shadowCastingMode`-бюджет
  (Stage 2/H3);
- **не** менять `ProjectC_URP.asset` и `ProjectSettings` без отдельного согласования.

**Вариант B (только по явному указанию):** писать `shadowDistance` в URP-ассет в рантайме — не делать
по умолчанию (глобально влияет на мир).

Приёмка: в коде не остаётся вводящего в заблуждение присваивания; пресеты дальности больше не обещают
управление тенями; Console 0 errors.

### 2.3. Проверки без изменений

- `AtmosphereCamera` — меню-камера, far 10; в куллинг/union не включать.
- `ProjectSettings/GraphicsSettings.asset:58,79–80`: `m_BrgStripping=0`, camera-relative 1/1 — **только
  зафиксировать в отчёте**, не менять.
- `QualitySettings.shadowDistance` больше не считать рычагом.

---

## 3. Артефакты и коммиты

1. CSV baseline:
   `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\K_baseline_<YYYY-MM-DD>.csv`
2. Отчёт Stage 0 (5–10 строк в ответе + при необходимости `08_DATA/K_stage0_report.md`):
   что измерено, какие числа, какие фиксы, что осталось непроверенным.
3. Обновить трекер Stage 0 в `10_EXECUTION_PLAN.md` (⬜→✅) и, если нужно, `README` раздела.
4. Коммит **один**: `T-CULL01/0: Stage 0 baseline + fixes` (без push). В коммит: CSV, фиксы, обновление плана.
5. `git status` после — чисто; сцена не изменена (Play Mode не сохранять).

---

## 4. Gate Stage 0 (не переходить дальше, пока не выполнено)

- [ ] baseline CSV заполнен для 4 точек; повторный замер на одной точке ±5 %;
- [ ] известные ориентиры (26 981 / 21 118) подтверждены или объяснено расхождение;
- [ ] `DetailDistanceCuller` больше не использует `Camera.main`, проверено в Play Mode;
- [ ] `ViewDistanceApplier` больше не делает вид, что управляет тенями (вариант A) — либо решение B
      зафиксировано письменно;
- [ ] F8/F9: 0 errors, объекты на месте;
- [ ] Console 0 errors; сцена/Assets вне разрешённого scope не изменены;
- [ ] коммит `T-CULL01/0` создан; отчёт с absolute paths и числами сдан Lead.

---

## 5. Жёсткие запреты

1. Не сохранять сцену/префабы; не трогать `PRIMUM_CITY.prefab`, `PRIMUM_DOORS`, `PRIMUM_COLLIDERS`,
   `Terrain`, `NetworkManager`, `ClientSceneLoader`, `ScenePlacedObjectSpawner`, `GlobalMotion*`.
2. Не менять `ProjectSettings/`, `Packages/`, URP-ассеты (кроме случая B с письменным согласованием).
3. Не запускать Play Mode во время компиляции; не редактировать код при активном Play Mode.
4. Не делать скриншоты без разрешения; не push.
5. Не «улучшать» по пути другие системы — только Stage 0 scope.

---

## 6. Что сдать Lead

Короткий отчёт:
- commit hash;
- 4 строки baseline (DC/SetPass/tris/FPS по точкам) + CSV path;
- что изменено в двух файлах (absolute paths + line refs);
- результаты F8/F9 и Console;
- что осталось непроверенным;
- подтверждение, что `git status` чистый, сцена не сохранена.
