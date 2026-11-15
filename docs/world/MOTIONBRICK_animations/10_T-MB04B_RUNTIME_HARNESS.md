# T-MB04b — root motion на пробе, body swap и стенд приёмки

> **Дата:** 2026-11-15 (вечер)
> **Ветка:** `main` (основная)
> **HEAD до работ:** `7301bf62` (`T-MB04a: analysis, main-primary handoff and runtime acceptance notes`)
> **Статус:** код и стенд готовы; Play Mode не запускался (правило `dont-start-play`) — визуальная приёмка за пользователем.
> **Связанные:** [09](09_T-MB02_ANALYSIS_AND_NEXT.md) · [06](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [HANDOFF.md](HANDOFF.md) · [baseline/](baseline/)

---

## 1. Что требовалось (T-MB04b)

| Пункт | Требование |
|---|---|
| baseline | PASS до/после |
| root motion | кукла едет, foot slide ≤ 2 см/кадр, `Stop` → baseline |
| body swap | M→F→M без T-pose/потери экипировки |
| сеть/FO | 2 клиента + F8/F9 без errors |
| дублирование | root motion не добавляет второй `CharacterController.Move` |

## 2. Что сделано в коде

| Файл | Изменение | Зачем |
|---|---|---|
| `Assets/_Project/Scripts/Animation/MotionBricks/MotionBricksBakedRootMotion.cs` | переработан | гейт с редакторным обходом; перепривязка после body swap; чтение таза в `LateUpdate`; жёсткий запрет `Move` на сетевом персонаже |
| `Assets/_Project/Scripts/Animation/MotionBricks/MotionBricksBakedPlayback.cs` | +`ConfigureForReview`, +сброс `ReviewOverride` на старте Play Mode | сборка playback в рантайме для приёмки на живом игроке; защита от «залипшего» флага при выключенном domain reload |
| `Assets/_Project/Scripts/Editor/Animation/MotionBricksReviewMenu.cs` | расширено меню | поездка probe, замер скорости, playback на моём игроке, body swap, диагностика |

Baseline-скрипты (`NetworkPlayer.cs`, `CharacterCustomisationApplier.cs`, `SkillAnimationPlayer.cs`,
`NpcBrain.cs`) **не менялись** — 12/12 до и после (см. §7).

### 2.1. Root motion: три исправления против T-MB04a

1. **Гейт.** `Mode=Disabled` остаётся; для приёмки компонент, как и playback, принимает
   `MotionBricksBakedPlayback.ReviewOverride` (только `#if UNITY_EDITOR`) и сам себя выключает
   при `Disabled`. Меню включает его перед стартом клипа. В билд обход не попадает.
2. **Body swap.** Компонент подписан на `CharacterCustomisationApplier.BodySwapped`: после M↔F
   Animator и кости пересоздаются, поэтому старый `_hips` — «мёртвая» ссылка. Теперь компонент
   перепривязывается к новому Animator, заново ищет `B-hips` и сбрасывает историю дельты.
   Раньше (T-MB04a) это было риском №3 ревью.
3. **`HipsDelta` после Animator.** Дельта таза читается в `LateUpdate` (после humanoid-пасса),
   а применяется из одного места в `Update`. Чтение в `Update` давало позу прошлого кадра
   (риск №5 ревью). Для текущего `ClipSpeed` это не критично, но путь `HipsDelta` теперь верный.

### 2.2. «Не дублировать `CharacterController.Move`» — как это обеспечено

`MotionBricksBakedRootMotion` — единственное место, где вызывается `Move`:

```csharp
private void Apply(Vector3 displacement)
{
    if (_networkPlayer != null) { /* warn once */ return; }   // сетевой персонаж двигается сам
    if (_controller != null) { if (_controller.enabled) _controller.Move(displacement); return; }
    if (_allowTransformFallback) { transform.position += displacement; return; }
}
```

- На объекте/в родителях есть `NetworkPlayer` → компонент **не двигает ничего** и пишет
  предупреждение: смещение должно быть встроено в `NetworkPlayer.ProcessMovement`.
- На пробе `CharacterController` отсутствует вовсе, поэтому второго `Move` нет физически —
  работает явный `_allowTransformFallback` (dev-объект, не сеть).
- В префабы и на игрока компонент не добавлен: `NetworkPlayer.prefab` не тронут.

Следствие: **полная интеграция root motion в движение игрока не сделана** — это отдельный шаг
(нужен `NetworkPlayer.cs`, а он в baseline). Сейчас для игрока проверяется только playback
(анимация) и body swap; едет только probe.

### 2.3. Почему не variant prefab `NetworkPlayer_MB_Test`

Бриф предлагал два варианта: variant prefab или applier на пробе. Выбран третий, более дешёвый
и честный:

- variant prefab **никто не спавнит** — `NetworkPlayerSpawner` работает с исходным префабом;
  чтобы вариант попал в игру, пришлось бы менять спавнер (вне T-MB04b).
- вручную поставленный variant — это неспавненный `NetworkBehaviour`: нет `OnNetworkSpawn`,
  Animator-параметры/движение не работают, тест получился бы про неспавненного игрока.
- `CharacterCustomisationApplier` на пробе тянет материалы/сокеты и **второй** подписчик на
  `CustomisationClientState` (побочные свапы в общем состоянии).

Поэтому playback **добавляется в рантайме** на живого локального игрока из меню Review
(`AddComponent` + `ConfigureForReview`). Префаб, контроллер и baseline-скрипты не меняются;
компонент живёт только в сессии Play Mode.

### 2.4. Body swap: честный обход одной проблемы

`CharacterCustomisationApplier` подписывается на `CustomisationClientState` в `OnEnable`. Если в
этот момент `CustomisationClientState.Instance == null` (порядок инициализации сцен), applier
пишет warning и **не подписывается** — снапшот до него не дойдёт.

Меню делает два шага:

1. штатный путь — `CustomisationClientState.ApplyCustomisationSnapshot(bodyType)`;
2. для applier'ов, которые не отреагировали (не были подписаны), — прямой вызов настоящего
   `CharacterCustomisationApplier.ApplyBodyType(bodyType)` через reflection (только редактор).
   Это тот же код и то же событие `BodySwapped`, но без правки baseline-скрипта ради теста.

Лог меню печатает, сколько applier'ов отреагировало штатно и сколько пришлось вызвать напрямую.

Модели для свапа берутся из `NetworkPlayer.prefab` (`testing.fbx` / `1_5.fbx`, legacy-ассеты
Blender). Это существующая конфигурация проекта, не наша правка: приёмка проверяет, что
**playback и экипировка переживают свап**, а не финальный арт персонажа.

---

## 3. Стенд в сцене

### 3.1. Probe (`WorldScene_0_0 / WorldRoot_0_0 / MB_Probe_Player`)

| Что | Значение | Почему |
|---|---|---|
| `MotionBricksBakedPlayback` | `_settings` = `MotionBricksRuntimeSettings` (mode 0), `_clip` = `WalkToJog_Male_Humanoid` | как было; клип ходьбы задаётся меню |
| `MotionBricksBakedRootMotion` | `_settings` = тот же ассет, `_source` = `ClipSpeed`, `_clipSpeed` = **2.00**, `_acceleration` = **0**, `_allowTransformFallback` = **true**, `_logVerbose` = true | скорость = замер KL Walk01; без разгона скорость тела равна скорости клипа с первого кадра — иначе первый шаг проскальзывает |
| Animator `HumanM_Model` | `applyRootMotion = false`, `cullingMode = AlwaysAnimate` | `applyRootMotion` на пробе был **включён**: Animator мог сам применять root-кривые клипа и складывать движение с нашим. Для сетевого персонажа в проекте `applyRootMotion = 0` (baseline) — на пробе теперь так же |

Позиция пробы (user-change, не наша): `(39836.16, 2489.64, 40020.61)`, лицо +Z.
Проверено замером: направление «носок−стопа» в rest-позе = `(0.028, 0, 1.000)`, `dot` с
`transform.forward` = **1.000** ⇒ `ClipSpeed` везёт ровно туда, куда идёт клип.

### 3.2. Гейт

`MotionBricksRuntimeSettings.asset` — **`mode: 0` (Disabled)**, не менялся. Всё, что играет в
сессии, включает `ReviewOverride` из меню; в билд этот путь не попадает.

Ассет настроек — Lore-контент рабочего дерева, как и сцена: git его не отслеживает
(`git ls-files` пуст, `.meta` игнорируется). Это существующая модель проекта, не наша правка.

---

## 4. Приёмка (пользователь, Play Mode)

Все пункты — меню `ProjectC/MotionBricks/Review/...`. Play Mode запускает пользователь.

### 4.1. Root motion на пробе

1. Войти в Play Mode, дождаться загрузки `WorldScene_0_0`.
2. **`Review/Focus probe in Scene view`** — камера Scene View наводится на куклу.
   Если удобнее смотреть из Game View: **`Review/Probe/Bring probe in front of my player`**
   переносит пробу на 5 м перед локальным игроком (только в сессии, сцена не сохраняется).
3. **`Review/Play KL Walk01 Forward on probe (рабочая ходьба)`**.
   Ожидаемо в логе: `Играю KL Walk01 Forward ... Root motion: 2,00 м/с`, затем
   `[MotionBricksBakedPlayback] Play 'HumanM@Walk01_Forward' ...`.
4. Смотреть: кукла **едет вперёд** по своему лицу, стопы не «шаркают» по земле.
5. **`Review/Probe/Start travel measurement`** → подождать 3–5 с →
   **`Review/Probe/Log travel measurement`**.
   Ожидаемо: средняя скорость ≈ **2.00 м/с** (допуск на глаз ±0.1).
6. **`Review/Stop on probe`** → `Playback остановлен, baseline Animator восстановлен`;
   кукла замирает в **rest/bind-позе пробы** (у probe нет контроллера — это его baseline),
   главное: она не остаётся в середине шага и больше не едет.

Критерий: едет, стопы не скользят, `Stop` → baseline, 0 errors.

### 4.2. Playback и body swap на живом персонаже

1. Войти в игру (Host/Client), дождаться своего персонажа.
2. **`Review/Player/Attach + play KL Walk01 on my player`**.
   Ожидаемо: персонаж играет ходьбу на месте, экипировка (оружие/плащ) на месте.
   В логе одно ожидаемое предупреждение `MotionBricksRuntimeSettings не назначен` — оно
   приходит от `AddComponent` **до** конфигурации; затем `Play 'HumanM@Walk01_Forward'`.
3. **`Review/Body swap/Body swap → Female`**.
   Ожидаемо: тело сменилось, ходьба продолжается (playback перепривязался), T-pose нет,
   экипировка на месте.
4. **`Review/Body swap/Body swap → Male`** — то же обратно.
5. **`Review/Player/Stop on my player`** → baseline Animator; **`Detach playback from my player`**
   убирает компонент сессии.

Если после свапа playback сам пишет `[MotionBricksBakedPlayback] Play 'HumanM@Walk01_Forward' ...` —
перепривязка к новому Animator сработала (это делает `OnBodySwapped`).

### 4.3. Сеть и Floating Origin

1. Два клиента (Host + Client) на одной сцене.
2. Проверить: позиции/анимации совпадают, ошибок в консолях нет.
3. F8 (сдвиг мира) → probe и игрок на месте, 0 errors; F9 → возврат.

Это делает пользователь; агент Play Mode не запускает.

---

## 5. Что доказано, а что нет

| Пункт | Статус |
|---|---|
| baseline 12/12 до/после | **ДА** (§7) |
| `applyRootMotion` на пробе выключен, направление движения совпадает с клипом | **ДА**, замер (§3.1) |
| второй `CharacterController.Move` невозможен на игроке | **ДА по коду** (§2.2), компонент на игрока не ставится |
| скорость 2.00 м/с и отсутствие slide | **клип** — замер валидатора (0.07/0.11 см/кадр); **рантайм** — за пользователем (§4.1) |
| body swap переживает playback | **код + стенд**; визуал — за пользователем (§4.2) |
| 2 клиента + F8/F9 | **НЕ ПРОВЕРЕНО** (пользователь) |
| root motion внутри `NetworkPlayer` | **НЕ СДЕЛАНО** (осознанно, §2.2) |

## 6. Ограничения, которые остаются

1. **Root motion игрока не интегрирован.** Для этого нужен `NetworkPlayer.cs` (baseline);
   без согласования файл не менялся. Компонент на игроке запрещён и не двигает ничего.
2. **Нет blend/state/events.** Playback играет один клип через `PlayableGraph`; переходов
   Idle→Walk→Run нет. Это T-MB05.
3. **Нет runtime foot IK.** Контакт обеспечивается скоростью (`ClipSpeed`), а не IK; на
   неровностях стопа может «висеть»/проникать. IK — T-MB03.
4. **Body swap использует legacy-модели префаба** (`testing.fbx` / `1_5.fbx`), не KL M/F.
   Замена моделей — отдельное решение по контенту, префаб в T-MB04b не трогали.
5. **Reflection-обход в меню** — только редактор, только приёмка; в билд не попадает.
6. **`WalkToJog` на пробе остаётся клипом по умолчанию** (`_clip` в сцене). Для ходьбы —
   пункт меню KL, как и раньше.

---

## 7. Baseline

```
check_baseline.ps1 (main)
  OK  PlayerAnimation.controller, PlayerAnimation_Default/Female.overrideController,
      NetworkPlayer.prefab, Npc_Goblin.prefab, NpcAnimator_Goblin.overrideController,
      NetworkPlayer.cs, CharacterCustomisationApplier.cs, SkillAnimationPlayer.cs, NpcBrain.cs,
      Packages/manifest.json, Packages/packages-lock.json
BASELINE OK: 12 files match.
```

До работ — `BASELINE OK: 12 files match`; после — то же (baseline-файлы не редактировались).

---

## 8. Откат

| Уровень | Действие |
|---|---|
| 0 | `MotionBricksRuntimeSettings.mode = Disabled` (уже 0) |
| 1 | Убрать `MotionBricksBakedRootMotion` с `MB_Probe_Player`; playback-обход и так только из меню |
| 2 | В Play Mode компонент с игрока снимается пунктом `Player/Detach` или выходом из Play Mode |
| 3 | `git revert` коммита T-MB04b (код/доки; сцена — Lore) |

---

## 9. Абсолютные ссылки

| Что | Путь |
|---|---|
| Root motion | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Animation\MotionBricks\MotionBricksBakedRootMotion.cs` |
| Playback | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Animation\MotionBricks\MotionBricksBakedPlayback.cs` |
| Review menu | `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Editor\Animation\MotionBricksReviewMenu.cs` |
| Settings (mode 0) | `C:\Projects\ProjectC_client\Assets\_Project\Settings\MotionBricks\MotionBricksRuntimeSettings.asset` |
| Probe | `WorldScene_0_0 / WorldRoot_0_0 / MB_Probe_Player` |
| Baseline check | `C:\Projects\ProjectC_client\docs\world\MOTIONBRICK_animations\baseline\check_baseline.ps1` |

*Связанные: [09_T-MB02_ANALYSIS_AND_NEXT.md](09_T-MB02_ANALYSIS_AND_NEXT.md) · [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [HANDOFF.md](HANDOFF.md)*
