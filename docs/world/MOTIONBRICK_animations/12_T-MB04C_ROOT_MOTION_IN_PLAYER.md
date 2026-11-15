# T-MB04c — baked root motion внутри движения NetworkPlayer

> **Дата:** 2026-11-15 (ночь, после T-MB03)
> **Ветка:** `main`
> **HEAD до работ:** `30688894` (`T-MB03: audit next-step risks (cyclic IK, skill/playback, quality FAILs) and plan`)
> **Статус:** код/доки готовы; компиляция 0 errors; **один контролируемый прогон Play Mode сделан агентом по разрешению пользователя** — замер поездки 2.00 м/с, Stop→стоп, body swap Female→Male с перепривязкой; визуал/экипировка — за пользователем (чек-лист §6).
> **Связанные:** [11_T-MB03_BAKER_AND_GUARDS.md](11_T-MB03_BAKER_AND_GUARDS.md) · [10_T-MB04B_RUNTIME_HARNESS.md](10_T-MB04B_RUNTIME_HARNESS.md) · [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [baseline/BASELINE_MANIFEST.md](baseline/BASELINE_MANIFEST.md) · [HANDOFF.md](HANDOFF.md)

---

## 1. Что требовалось (acceptance)

| Пункт | Требование | Статус |
|---|---|---|
| baseline | PASS до/после, хэш `NetworkPlayer.cs` обновлён **отдельным коммитом** | ✅ до — 12/12; после — 12/12 с новым хэшем (см. §7) |
| движение | игрок едет от baked root motion, горизонталь кадра = смещение клипа | ✅ runtime: **33.13 м / 16.56 с = 2.00 м/с** (§6.1) |
| один Move | второго `CharacterController.Move` нет | ✅ по коду (§4); в рантайме движение идёт через `ProcessMovement` |
| Stop | `Stop` → baseline Animator + обычное движение | ✅ runtime: позиция не меняется за 20 с, `canDrive=false` (§6.1) |
| body swap | `GlobalPilot` без T-pose/потери экипировки | ✅ runtime: Female→Male, playback и `B-hips` перепривязаны, скелет двигается (§6.1); T-pose/экипировка — визуально за пользователем |
| feature flag | всё под `MotionBricksRuntimeSettings`, `mode=0` не менялся | ✅ |

Правка `NetworkPlayer.cs` (baseline-файл) выполнена **с явного одобрения пользователя** в этой сессии.

---

## 2. Файлы

| Файл | Что | Baseline |
|---|---|---|
| `Assets/_Project/Scripts/Animation/MotionBricks/MotionBricksLocomotionBridge.cs` | **новый**: реестр активных источников + `TryConsumeDisplacement(NetworkPlayer, out Vector3)` | — |
| `Assets/_Project/Scripts/Animation/MotionBricks/MotionBricksBakedRootMotion.cs` | публикует смещение в мост вместо запрета/предупреждения; `[DefaultExecutionOrder(-100)]`; `ConfigureForReview` | — |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | +11 строк в `ProcessMovement`: чтение displacement из моста перед единственным `_controller.Move` | **да**, хэш обновлён отдельным коммитом |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksReviewMenu.cs` | на живого игрока ставится и root motion; замер поездки игрока; harness-лог моста | — |

`PlayerAnimation.controller`, `NetworkPlayer.prefab`, `NetworkPlayer_GlobalPilot.prefab`, `SkillAnimationPlayer`,
`CharacterCustomisationApplier`, `NpcBrain`, `Packages/*` **не менялись**.

---

## 3. Архитектура: один расчёт, один Move

```
MotionBricksBakedPlayback (PlayableGraph, клип)
        │  IsPlaying
        ▼
MotionBricksBakedRootMotion (Update, order -100)
        │  displacement кадра (ClipSpeed или HipsDelta)
        │  на объекте с NetworkPlayer — НЕ Move
        ▼
MotionBricksLocomotionBridge.TryConsumeDisplacement(player, out d)
        │  читается один раз за кадр, после чтения обнуляется
        ▼
NetworkPlayer.ProcessMovement
        controllerMotion.x/z = d + _platformDelta
        _controller.Move(controllerMotion)   ← единственный Move
```

### 3.1. Гейт (fail-closed)

Мост отдаёт смещение только если одновременно:

- `player != null && player.IsOwner` — удалённые игроки не едут от чужого клипа;
- у игрока есть зарегистрированный `MotionBricksBakedRootMotion`;
- `MotionBricksRuntimeSettings != null`, `allowPlayer == true`;
- `mode != Disabled` **или** редакторный `MotionBricksBakedPlayback.ReviewOverride` (приёмка);
- `playback.IsPlaying` — нет клипа, нет движения;
- компонент активен (`isActiveAndEnabled`).

Иначе `false`, и `NetworkPlayer` двигается ровно как baseline (ввод + ветер + гравитация).

### 3.2. Почему execution order −100

`NetworkPlayer.ProcessMovement` вызывается из его `Update`. Чтобы мост отдал смещение
**текущего** кадра, а не прошлого, `MotionBricksBakedRootMotion` помечен
`[DefaultExecutionOrder(-100)]` и обновляется раньше. Это атрибут класса, `ProjectSettings`
не трогается.

### 3.3. Что заменяется, а что нет

| Компонент движения | Поведение при активном playback |
|---|---|
| горизонталь ввода (`horizontalVel`) | **заменяется** baked-смещением |
| ветер (`windVel`) | на этот кадр **не применяется** (иначе стопы быстрее клипа) |
| вертикаль (гравитация/прыжок) | сохраняется |
| перенос платформы (`_platformDelta`) | сохраняется и складывается с baked-смещением |
| поворот | остаётся прежним: при вводе персонаж доворачивается (steering), без ввода — по `transform.forward` |

Для MVP это осознанно: playback управляет движением. Когда появятся клипы с переносом корня,
`HipsDelta` даст то же смещение через тот же мост — менять `NetworkPlayer` не придётся.

---

## 4. Почему второго `CharacterController.Move` нет

`MotionBricksBakedRootMotion.Apply` на объекте с `NetworkPlayer` **возвращается до** любых
`Move`/`transform.position`:

```csharp
if (_networkPlayer != null)
{
    _bridgeDisplacement = displacement;   // заберёт NetworkPlayer.ProcessMovement
    return;
}
if (_controller != null) { ... _controller.Move(displacement); return; }  // probe/не-игрок
```

`NetworkPlayer.cs` содержит ровно один `_controller.Move` (был и остался). Мост не двигает
`transform`, не трогает `NetworkTransform`, не включает/выключает контроллер.

Потребление одноразовое: `ConsumeBridgeDisplacement()` обнуляет значение, поэтому даже
гипотетический второй вызов `ProcessMovement` в том же кадре получит ноль, а не двойную скорость.

---

## 5. Что видно в логах

| Событие | Лог |
|---|---|
| первое смещение на игроке | `[MotionBricksBakedRootMotion] Смещение клипа уходит в NetworkPlayer.ProcessMovement через MotionBricksLocomotionBridge; CharacterController.Move отсюда не вызывается.` |
| `Review/Log harness state` | `player playback: ...` + `player root motion: enabled=... clipSpeed=... current=... canDrivePlayer=... bridgeSources=...` |
| `Review/Player/Log travel measurement` | `Замер поездки игрока: X м за T s → средняя V м/с. ...` |

Ожидаемо для KL Walk01: `V ≈ 2.00 м/с`. Просадка возможна на склонах/коллайдерах.

---

## 6. Приёмка в Play Mode (пользователь)

1. `ProjectC/MotionBricks/Review/Log harness state` — до старта: `player playback: не подключён`,
   `player root motion: не подключён (bridgeSources=0)`.
2. Войти в игру (Host/Client), дождаться своего персонажа.
3. `Review/Player/Attach + play KL Walk01 on my player`.
   Ожидаемо: два ожидаемых warning'а «MotionBricksRuntimeSettings не назначен» (AddComponent до
   конфигурации), затем `Play 'HumanM@Walk01_Forward'`, `root motion включён на 2,00 м/с`,
   и один лог про мост. **Персонаж едет вперёд сам** (без WASD) со скоростью ~2 м/с.
4. `Review/Player/Start travel measurement` → 3–5 с → `Review/Player/Log travel measurement`.
   Ожидаемо `≈ 2.00 м/с`. Руками: стопы не шаркают, персонаж не «плывёт».
5. `Review/Player/Stop on my player` → персонаж замирает на месте; WASD снова двигает его
   по-обычному (baseline). В логе — `root motion перестал отдавать смещение`.
6. Снова attach → `Review/Body swap/Body swap → Female` → `→ Male`.
   Ожидаемо: playback и root motion перепривязываются, персонаж продолжает ехать, T-pose нет,
   экипировка на месте.
7. `Review/Player/Detach playback from my player` → компоненты удалены; движение — baseline.
8. 0 errors в консоли за сеанс.

Отдельно (чек-лист T-MB08): 2 клиента, F8/F9 от нуля, каст скилла при активном playback.

### 6.1. Прогон агента 15.11 (Play Mode, по разрешению пользователя)

Сцена `BootstrapScene` → Host поднят через `GlobalMotionPilotRuntime.StartPilotHost()` (штатный путь
`MainMenuWindow`), локальный игрок — `NetworkPlayer_GlobalPilot(Clone)`, owner. Дальше — только пункты
`ProjectC/MotionBricks/Review/...`; код и ассеты не менялись.

| Шаг | Результат |
|---|---|
| Attach + play KL Walk01 | `playback=True`, `playing=True`, клип `HumanM@Walk01_Forward`, Animator `Body`, `rootMotion.enabled=True`, `ClipSpeed=2.00`, `current=2.00`, `canDrivePlayer=True`, `bridgeSources=1`, `guard=True` |
| Замер поездки | `33.126 м` за `16.563 с` → **2.00 м/с**; `isGrounded=True`, `transform.forward=(0,0,1)` — ровно по клипу |
| Stop | `playing=False`, `canDrive=False`, `current=0.00`; позиция `(-95.923, -72.637, 39.863)` не изменилась за **20 с** |
| Body swap → Female | `bodyType=Female`, `playing=True`, `canDrive=True`, controller `PlayerAnimation_Female`, кости `B-hips`/`B-upperArm.L` на месте, персонаж продолжает ехать |
| Body swap → Male | `bodyType=Male`, `playing=True`, `canDrive=True`, controller `PlayerAnimation_Default`, `applyRootMotion=False`, `guard=True` |
| Detach | оба компонента удалены, `bridgeSources=0`, controller `PlayerAnimation_Default` |
| Консоль | 0 errors за сеанс; Play Mode остановлен, сцена не сохранялась, `git status` по сцене пуст |

**Побочное наблюдение (не дефект интеграции):** в автоматическом прогоне камера осталась в 288 км от
персонажа (мы не управляли камерой). Новое тело после свапа приходит с FBX-дефолтом
`cullingMode = CullUpdateTransforms`, поэтому Animator был culled и поза на костях не обновлялась,
хотя playback играл и `ClipSpeed` вёз тело. После принудительного `AlwaysAnimate` (только сессия)
`B-upperArm.L` повернулась на **7.3°** за 1 с — playback действительно двигает новый скелет.
В обычной игре камера рядом с игроком, поэтому culling не мешает; это тот же класс находки, что
T-MB04b §5.3. Визуальную проверку T-pose/экипировки при свапе всё равно делает пользователь.

---

## 7. Baseline

До работ:

```
BASELINE OK: 12 files match.
```

После правки `NetworkPlayer.cs` (до обновления хэша):

```
CHANGED  Assets/_Project/Scripts/Player/NetworkPlayer.cs
         expected a9a89c46ed510d88c7d579d8d736bac023ecf4d6b3b957d750c9ff838170932a
         actual   9cc44bf740ed710ee1b72fa2d64b0c88212582fc43df5025eed1ae9317b4ddc4
BASELINE FAILED: 1 of 12 files changed/missing.
```

Процедура `BASELINE_MANIFEST.md` §5 соблюдена: изменение намеренное и одобрено, `git diff`
проверен (только вставка на уровне `ProcessMovement` + `using`), хэш пересчитан,
`baseline_hashes.tsv` обновлён **отдельным коммитом**. После обновления — снова 12/12.

`NetworkPlayer_GlobalPilot.prefab` в baseline **не добавлен** — это отдельное решение
(см. [11 §13.3](11_T-MB03_BAKER_AND_GUARDS.md)).

---

## 8. Риски и что осталось

| # | Риск | Куда |
|---|---|---|
| 1 | **Визуал не проверен агентом.** Поездка 2.00 м/с, Stop и перепривязка при свапе подтверждены кодом/логом (§6.1); T-pose/экипировка глазами — за пользователем. | Пользователь |
| 2 | **Playback не учитывает ввод.** Пока клип играет, горизонталь принадлежит клипу; ввод только доворачивает. Для MVP допустимо, для полноценного locomotion нужен blend/state. | T-MB05 |
| 3 | **Один клип, без переходов.** Idle→Walk→Run→Stop нет. | T-MB05 |
| 4 | **Нет runtime foot IK.** Контакт держится скоростью клипа, не IK; на неровностях стопа может висеть. | T-MB05 |
| 5 | **`WalkForward_Male`/`WalkToJog_Male` не проходят валидатор** (ось/асимметрия/разворот) — контент, не пайплайн. | Свои seed/target; T-MB08 |
| 6 | **Каст скилла во время playback не останавливает граф** (известный долг T-MB03 §14.2). | T-MB08 |
| 7 | **`NetworkPlayer_GlobalPilot.prefab` вне baseline.** | Отдельное решение |
| 8 | **Culling после body swap:** новое тело приходит с `CullUpdateTransforms`; если камера далеко, поза на костях замирает (движение при этом работает). Фикс — `AlwaysAnimate` для локального игрока/камеры — отдельным решением, не в T-MB04c. | T-MB05/T-MB08 |
| 9 | `tmp_tmb04_*.bat` в корне — temp, не коммитить. | Удалить |

---

## 9. Откат

| Уровень | Действие | Время |
|---|---|---|
| 0 | `MotionBricksRuntimeSettings.mode = Disabled` (уже 0) | секунды |
| 1 | Снять `MotionBricksBakedRootMotion`/`MotionBricksBakedPlayback` с игрока (в рантайме — `Player/Detach`) | минуты |
| 2 | `git revert` коммита T-MB04c (bridge/root motion/menu + `NetworkPlayer.cs`); baseline-хэш вернуть тем же revert'ом | <1 ч |

`NetworkPlayer.cs` при откате возвращается к хэшу `a9a89c46…`; мост при этом остаётся
невостребованным (в `ProcessMovement` его вызов исчезает вместе с правкой).

---

## 10. Абсолютные ссылки

| Что | Путь |
|---|---|
| Мост | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Animation\MotionBricks\MotionBricksLocomotionBridge.cs` |
| Root motion | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Animation\MotionBricks\MotionBricksBakedRootMotion.cs` |
| Playback | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Animation\MotionBricks\MotionBricksBakedPlayback.cs` |
| Точка входа движения | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Player\NetworkPlayer.cs` (`ProcessMovement`) |
| Review-меню | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksReviewMenu.cs` |
| Settings (mode=0) | `C:\Projects\ProjectC_client\Assets\_Project\Settings\MotionBricks\MotionBricksRuntimeSettings.asset` |
| Baseline | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1` |
| Baseline manifest | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\BASELINE_MANIFEST.md` |
