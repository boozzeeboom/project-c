# T-CULL01 / Stage 3a (H2) — Play Mode замеры HeavyII

**Дата:** 2026-10-31 · **База:** `6ec32b4e` · **Scope:** только `Ship_havyII_base`
**Инструмент:** `Assets/_Project/Editor/Culling/Stage3ShipBench.cs` (меню `Project C → Culling → Stage 3 Ship Bench → Run`)
**Данные:** `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/M_stage3_heavy_runtime_2026-10-31.csv`
(+ сырые прогоны `…_2026-10-31.txt`, `…_run1.txt`, `…_run2.txt` в той же папке)

Play Mode запускался по разрешению пользователя. Сцена не сохранялась (`isDirty=false` до и после каждого прогона),
пуш не выполнялся, префабы, FBX и `Assets/_Project/**` не изменялись.

---

## 1. Метод

- Play Mode → Host (`MainMenuWindow.OnHostClicked` через reflection), камера `ThirdPersonCamera_0`
  (`SpringArmCamera`/`ThirdPersonCameraDefault_EmptyTEST` выключены), `farClipPlane = 30000`, `fov = 60`,
  поза каждый тик = `shipBounds.center + normalize(1,0.35,1) × distance`, `LookRotation` в центр.
- Свет: `DayNightController.SetTimeOfDay(12)` + `ForceRefresh()`, компонент затем выключен (как в Stage 0/2).
  `Sun(enabled=True, shadows=Soft, intensity=1.24)`.
- Warmup 1500 кадров, settle 40, 3 сэмпла с шагом 8 кадров, метрика — медиана `UnityStats`.
- **Атрибуция DC корабля = A − B:** 3 сэмпла с кораблём, затем всем ship `MeshRenderer.enabled=false`
  (оригиналы кэшируются и восстанавливаются), 12 кадров settle, 3 сэмпла без корабля. `dc_withoutShip` включает город,
  поэтому город в кадре замеру не мешает.
- На время замеров корабль переводится в `isKinematic=true` (только runtime) после 600-кадрового probe.

Кейсы: `LOD0_80m`, `LOD0_80m_repeat`, `LOD0_full_200m`, `LOD1_400m`, `LOD2_1200m`, `LOD2_1200m_repeat`.

---

## 2. R0.2 — корабль без пилота падает, а сохранённая позиция телепортировала его в пустоту

Это главная находка прогона, она важнее самих DC.

- Runtime `Rigidbody`: масса **30 000** (`massHeavyII 2000 × massMultiplier 15`), `isKinematic=false`, `useGravity=true`,
  250 BoxCollider. `ShipController.ApplyAntiGravity()` делает early-return при `!_engineRunning && !_hasNpcPilot`
  («корабль падает сам»); `_frozenByNoPilot` включается только при работающем двигателе.
  Поэтому на старте корабль падает с 25.76 м над `DECK`, приземляется на кадре ~40 (`contacts=1`, `vy=0`) и дальше стоит
  неподвижно до кадра 600 — это ожидаемое поведение, не дефект.
- **Реальный отказ:** `ShipPositionServer.RestoreCoroutine()` (задержка ≈ 6 с) применяет сохранённый transform **поверх**
  сценового. В сохранении лежала `px=79953.80, py=-42311.10, pz=79366.48` — позиция свободного падения, записанная
  предыдущей сессией. Результат в `run1`: скачок `y -79.36 → -41649.59` между кадрами 100 и 110 (Δ ≈ −41 570 м),
  `groundDist=-1` (ничего в радиусе 200 км), дальнейшее падение до терминальной −24.30 м/с. В `run2` то же на кадре 80→90.
  `ApplyRebaseCorrection` вычитает только `rbx/rby/rbz` и упавшую координату исправить не может; каждая сессия
  перезаписывала сохранение снова, то есть состояние было самоподдерживающимся.
- После исправления запись имеет вид `px=127.37, py=-79.36, pz=-68.60, rbx/rby/rbz=(-39936,-2560,-39936), rbFrame=2` —
  позиция покоя на палубе в сдвинутом фрейме. Телепорт прекратился (итоговый прогон: `y=-79.36` все 600 кадров,
  `contacts=1`).
- **Открытое следствие для пользователя:** сохранение теперь перекрывает авторскую позицию корабля —
  в сцене он стоит на 28.42 м выше `DECK`. Удалять или править `ShipPositions.json`
  (`C:/Users/leon7/AppData/LocalLow/DefaultCompany/ProjectC_client/`) агент не стал — это решение пользователя.

Файл сохранения не является частью репозитория и в коммит не входит.

---

## 3. Результаты (итоговый прогон, корабль на `DECK`, `lodBias=1.30`, `Medium`)

| Кейс | дистанция | dc_all | dc_withoutShip | **ship_dc** | видимых слотов | ship shadowCasters |
|---|---:|---:|---:|---:|---|---:|
| LOD0_80m | 80 м | 1810 | 795 | **1015** | 335 (LOD0 296 + dyn 39) | 349 |
| LOD0_80m_repeat | 80 м | 1810 | 795 | **1015** | 335 | 349 |
| LOD0_full_200m | 200 м | 4395 | 2857 | **1538** | 555 (LOD0 509 + dyn 46) | 593 |
| LOD1_400m | 400 м | 3386 | 3151 | **235** | 57 (LOD1 10 + dyn 47) | 106 |
| LOD2_1200m | 1200 м | 2398 | 2294 | **104** | 52 | 0 |
| LOD2_1200m_repeat | 1200 м | 2398 | 2294 | **104** | 52 | 0 |

- **Воспроизводимость:** повторы внутри прогона совпали точно (1015/1015 и 104/104), то есть требование ±5 % выполнено
  с запасом. Дрожание самой медианы при неизменной композиции — до ±15 DC: в `run1` LOD2 дал 89 и 104 на одном и том же
  кадре сцены (`rawDc = 550|550|565`, `rawDcOff = 461`). В итоговом прогоне у `LOD1_400m` `rawDcOff = 3166|3151|3151`,
  поэтому 235 следует читать как 235 ± 15.
- **Пороги LOD другие, чем в отчёте H2.** В `T-CULL01_STAGE3_REPORT.md` указан `lodBias 0.7`, в рантайме фактически
  **1.30** (Medium), эффективные пороги 300/900 × 1.30 = **390/1170 м**. Именно поэтому 200 м ещё LOD0, а 400 м — LOD1.
  Расхождение реальное.
- **80 м не является «полным кораблём».** Корпус 325 м в мире, при дистанции 80 м census даёт `inFrustum=346`,
  `isVisible=335` из 615 — примерно половина корабля вне фрустума. Поэтому добавлен кейс `LOD0_full_200m`:
  только он показывает стоимость корабля целиком в LOD0 (555 слотов).
- **`LOD2` `shadowCasters=0`** — при этой дистанции теневые кастеры отсекаются, это не изменение корабля.
- **Трисетовая разность недостоверна**, когда между A и B меняется содержимое кадра (строки 200 м и 400 м дают
  `ship_tris=3032` и `147104` при неизменном корабле). DC-разность осталась чистой (`dc_withoutShip` постоянен внутри кейса),
  поэтому в отчёт идут только DC.
- **Прогоны `run1`/`run2`** (корабль в пустоте, 5/5 кейсов) дали 80 м → 804, 400 м → 220, 1200 м → 89/104.
  Композиция LOD (`vis_LOD0/1/2`) во всех трёх прогонах идентична; абсолютные DC отличаются, потому что в пустоте
  в кадр попадало другое число теневых кастеров корабля (462 против 349) — то есть ship_dc зависит от окружения,
  и приёмочным следует считать прогон с городом в кадре.
- **Runtime baseline «до H2» не снят.** Единственные данные до мержа — edit-mode модель (`973 MR / 972 слота / 2 916 DC`).
  Замеров «до» на стабильном инстансе в этой сессии нет, поэтому «после» сравнивается с моделью, а не с замером.

---

## 4. Приёмка по гейтам брифа

| Гейт | Факт | Итог |
|---|---|---|
| heavy `973 → ~620` slot (P1) | 616 MeshRenderer в runtime | **PASS** |
| heavy DC ≤ ~1800 (P1) на стабильном инстансе | 1538 при полном корабле в кадре (200 м) | **PASS** |
| heavy DC ≤ ~1300 (P2) | 1538 при полном корабле — не достигнут; 1015 получается только при срезанном фрустумом корабле (80 м) | **НЕ достигнут** |
| 9 дверей / 10 leaf / `PilotSeat` / 11 `ModuleSlot` / `CargoVisual` / VFX целы | см. §5 | **PASS** |
| reimport FBX не ломает | проверено в H2 (`Verify Heavy Visual Merge`, H1-R1) | **PASS** |
| F8/F9: 0 errors | API-вызовы без ошибок, но 0 транзакций — см. §6 | **не подтверждён** |
| сцена не сохранена, medium/light/NPC 0 изменений | `isDirty=false`, `sceneSaved=no`; файлы других кораблей не менялись | **PASS** |

Сверка с edit-mode моделью (`slots × 3`): LOD0 — 1538 против 1812 (**85 %** модели), LOD1 — 235 против 171 (**137 %**),
LOD2 — 104 против 156 (**67 %**). То есть `×3`-модель совпадает с замером только в полосе LOD0; в LOD1/LOD2 она
не является ни потолком, ни полом, и как приёмочный критерий не годится.

Stop-условие брифа «пилот heavy не уложился в gate → future scale-out не начинать» относится к P2 `≤~1300`:
на полном корабле он не выполнен, замер равен 1538.

---

## 5. Целостность корабля и двери

- `meshRenderers = 616`, `merged_renderers = 20`, `lossyScale = (6.00, 0.90, 12.00)`, `LODGroup.size = 27.07`, `lodLevels = 3`.
- `DoorController = 9` (7 `isActiveAndEnabled`), `BoxCollider = 250` (1 trigger), `ModuleSlot = 11`,
  `PilotSeat = True`, `CargoVisual = True`, `ContrailVFX = True` (`UseShipBounds = False`), `EngineThrusterVisual = 2`.
- Тест на `Door_Mess`: `Open()` → через 90 кадров `isOpen=True`, `progress=1.00`, `animating=False`,
  `localMove_m=0.15` (смещение в ship-local пространстве), `selfRotated=0.00`; затем `Close()` → `progress=0.00`,
  `localBack_m=0.00`. Двери анимируются и возвращаются.
- Двери корутинные; `SetOpenTarget` делает early-return при `!isActiveAndEnabled`, поэтому тест выбирает активную дверь
  (2 из 9 выключены).

---

## 6. F8/F9 (Floating Origin) — транзакций нет

`GlobalMotionControlledRebaseSlice.RequestControlledRebase()` и `RequestControlledRebaseWithRollback()` (reflection),
по 600 кадров ожидания приватного `_transactionActive`:

```
[T-FO06CY] Controlled rebase rejected: no_rebase_plan:localFocus=(21.44, -72.75, 113.28)
REBASE;phase=success;transactionsSeen=False;worldRootShift_m=0.00;shipShift_m=0.00;frames=601;errorsSoFar=0
REBASE;phase=rollback;worldRootShift_m=0.00;shipShift_m=0.00;frames=601;errorsSoFar=0
```

Оба вызова отбиты планировщиком (`no_rebase_plan`), смещений нет, ошибок в консоли нет. Автоматический ребейз на старте
сессии при этом проходит штатно:
`Controlled rebase completed: translation=(-39936.00, -2560.00, -39936.00)` и
`[ShipPositionServer] T-FO-PERSIST01 RebaseCorrected: offset=(-39936.00,-2560.00,-39936.00) frame=2`.

Значит API-путь исправен, но принудительный ребейз из этого состояния не создаёт план; приёмка «F8→F9 реальным вводом»
остаётся за пользователем.

---

## 7. Frame Debugger

Из bench-цикла FD недоступен: `FrameDebuggerInternal.SetEnabled(bool,int)` в 6000.5 возвращает `count=0 / limit=0`,
блокирующий `Thread.Sleep`-поллинг останавливает рендер (в `run2` записано `FD;events=0`). Шаг убран из бенча.
Кадр снят вручную через `manage_profiler`: сначала `manage_editor action=pause`, затем `frame_debugger_enable`.

- **217 событий** в кадре.
- `ThirdPersonCamera_0`: main-light shadowmap 38–71 (**34**), DepthNormal prepass 73–97 (**25**),
  `DrawOpaqueObjects` 98–128 (**31**), `DrawTransparentObjects` 135–152 (**18**); `MainCamera` post-FX в начале,
  `AtmosphereCamera` + UI-оверлеи в конце.
- **Ограничение:** имена событий уровня pass/batch (`…/Shadows.DrawSRPBatcher`, `…/RenderLoop.Draw/QuadTreeBatchNode.Render`),
  per-mesh имён нет. FD подтверждает состав проходов и батчинг, но не per-slot DC — поэтому DC в §3 получен
  A/B-переключением рендереров, а не FD.

---

## 8. Открыто (пользователь)

1. Визуальная приёмка корабля на 80/200/400/1200 м: переходы LOD, отсутствие «дыр» в корпусе, тени.
2. F8/F9 реальным вводом (API-путь даёт `no_rebase_plan`).
3. Решение по `ShipPositions.json`: сохранение перекрывает авторскую позицию на 28.42 м над `DECK`.
   Править или удалять файл — только по решению пользователя.
4. P2 `≤~1300` при полном корабле не достигнут (1538). Нужен отдельный шаг, если гейт остаётся обязательным;
   future scale-out на medium/light/NPC по stop-условию брифа не начинается.
5. `docs/dev/T-CULL01_STAGE3_REPORT.md` §4 и строка `manual_check,runtime,…` в `M_stage3_heavy_2026-10-31.csv`
   обновлены ссылкой на этот файл; сами замеры задокументированы здесь.
