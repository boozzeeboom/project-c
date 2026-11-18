# 00 — STOP: работа по MotionBricks остановлена. Итоговый вердикт

> **Дата:** 2026-11-18
> **Статус:** **РАБОТА ОСТАНОВЛЕНА.** Тикеты `T-MB01…T-MB06` закрыты стопом, новые тикеты серии `T-MB` не открываются.
> **Ветка:** `main`, HEAD на момент стопа — `3e07ea6e`.
> **Область:** Unity 6.5 (`6000.5.2f1`), Windows x64, host-client co-op до 4 игроков.
> **Что не делалось:** код не менялся, Play Mode не запускался, тесты и скриншоты не запускались, коммит стопа не создавался.
> **Архив:** тикетные документы 07–18 и дубли research вынесены в [`archive/`](archive/) — карта в §7.

---

## 1. Вердикт заказчика (дословно)

> «вернемся к моушнбрикс как генератор переходных стадий анимационных а в игре продолжаем использовать наш аниматор»

Что это значит на уровне работ:

| Слой | Решение |
|---|---|
| **Игра (production)** | Остаётся **наш Animator**: `PlayerAnimation.controller` + библиотека Kevin Iglesias (286 клипов, все направления + `[RM]`-варианты) + `AnimatorOverrideController`. Ничего не заменяется, ничего не генерируется в рантайме. |
| **MotionBricks** | Возвращается к роли **офлайн-генератора переходных стадий** (in-between между позами). Результат — обычный `.anim`, который играет наш Animator или `MotionBricksBakedPlayback`. |
| **Runtime-нейро (T-MB06)** | Остановлен, в продукт не идёт. Код остаётся в проекте замороженным, `mode: 0`. |

---

## 2. Что именно остановлено

Остановлено (не развивается, новые работы не начинаются):

- live-генерация позы для игрока: Python/TensorRT sidecar + UDP `127.0.0.1:39360` + retarget SOMA→rig в `LateUpdate`;
- любые дальнейшие тикеты `T-MB06+`, включая предложенный, но **не начатый** jump-spike (канал `FLAG_INTERACT`, vertical root, `InteractionState`) и A/B `--engine-defaults`;
- правки тюнинга сайдкара (`RESPONSIVE_*`, `allowed_token_counts`, `position_scale`);
- доведение численного теста ретаргета C#-рига против позы из пакета.

Не остановлено (работает как раньше):

- офлайн-бейкер и валидаторы — это инструментарий будущей линии (§6);
- библиотека клипов, `PlayerAnimation.controller`, бой, NPC, сеть;
- `MotionBricksLocomotionBridge` в `NetworkPlayer.ProcessMovement` (единственный продовый хук, инертен — §5).

---

## 3. Почему остановлено (числа)

| Что проверяли | Результат | Вывод |
|---|---|---|
| Offline transition pack v1 (16.11) | 22 клипа, **0/22** прошли валидатор, slide до **11.32 см/кадр** | отклонён заказчиком: «ничуть не лучше того, что было» |
| Runtime T-MB06 (16–18.11) | сборка, self-test 480 кадров / 14 генераций, p50/p95 **5.107 / 32.302 мс** — PASS; **визуал хуже превью NVIDIA** | качество не принято |
| Причина №1 — наш тюнинг | `(6,7)` токенов против 8–14 у движка; `position_scale 0.58`; halflife `0.015` против `0.6`; `direction_regeneration_dot 0.995` против `0.92`; `max_dir_change_speed_reduction 0.0` против `0.25` | план обрывается на середине шага, разворот на полной скорости |
| Причина №2 — корень и фаза разъехались | `walkSpeed = 5`, `runSpeed = 10` (`NetworkPlayer.cs:39-40`), `Speed` бинарный (`:1327`), клипы играются **1×** (`animator.speed` в проекте не выставляется нигде), замеренный KL walk = **2.00 м/с** | слайд до **2.5×** — и в Animator, и в MB одинаково |
| Причина №3 — ретаргет не измерен | `MotionBricksSomaRetarget.cs:182` — формула «дельту слева»; ни один тест не сравнивал позу C#-рига с позой из пакета | «резиновость» не квантована |

Отдельно:

- **Прыжок MotionBricks не решает.** Smart Primitive `jump_0_5m` / `jump_0_75m` авторены на препятствия 0.5 / 0.75 м, а у нас `jumpForce = 8`, `gravity = -20` → v₀ ≈ 17.9 м/с, **апекс 8 м, 1.79 с в воздухе** (`NetworkPlayer.cs:44-45,1379`). Источник движения примитива — авторский BVH, то есть нового клипа он не даёт: `HumanM@Jump01`, `Fall01`, `Jump01 - Land` (и `[RM]`-варианты) у нас уже есть.
- **В кооперативе нейро-поза была бы только у owner:** `allowRemotePlayers: 0` в `MotionBricksRuntimeSettings.asset`. Персонажи друзей — на обычном Animator.

---

## 4. Чем этот «возврат» отличается от отклонённого offline-пака

Отклонён был **не офлайн-бейкинг как таковой**, а конкретная затея: матрица переходов локомоции, подключённая к боевой стейт-машине. Возврат разрешён только при этих условиях:

1. **Не матрица.** Один конкретный клип под один конкретный пробел (переходная стадия), а не 8 направлений × 3 походки × start/stop.
2. **Сначала библиотека.** Если нужный клип есть в 286 авторских — MB не применяется.
3. **Валидация обязательна.** `MotionBricksClipValidator`: slide ≤ 2 см/кадр, контакт/пенетрация в допуске, отчёт `*.bake.report.json`.
4. **Speed-match по замеру.** Клип отдаётся вместе со своей ground speed (как `ClipSpeed` в `MotionBricksBakedRootMotion`), иначе шаг поедет относительно `CharacterController`.
5. **Доставка без правок Animator.** `AnimatorOverrideController` / `MotionBricksBakedPlayback` (PlayableGraph поверх Animator). `PlayerAnimation.controller` и `NetworkPlayer.prefab` не редактируются.
6. **Runtime-нейро выключен.** `mode: 0`; sidecar не запускается в игре.

---

## 5. Состояние кода и влияние на билд

Диапазон работ: **58 коммитов** `c3ea92aa^..3e07ea6e`, из них MotionBricks — **54**, остальные 4 — посторонние (`T-FO06L` каталог пилота, забор/spline, «scene changed», «minor changes»).

| Что MB добавил в проект | Куда попадает | Влияние на билд |
|---|---|---|
| **7 runtime-скриптов** `Assets/_Project/Scripts/Animation/MotionBricks/` (`BakedPlayback`, `BakedRootMotion`, `LocomotionBridge`, `RuntimeClient`, `RuntimePlayback`, `RuntimeSettings`, `SomaRetarget`) | Assembly-CSharp | компилируются в билд как IL (несколько КБ); рантайм-стоимость ≈ 0, компоненты нигде не висят, кроме пробельного объекта |
| **Editor-скрипты** `Assets/_Project/Scripts/Editor/Animation/` (бейкер, валидатор, IK, экспортёры, меню) | папка `Editor` | **в билд не попадают** |
| `MotionBricksRuntimeSettings.asset` (`mode: 0`, `allowRemotePlayers: 0`) | `Assets/_Project/Settings/MotionBricks/` | ни одна сцена/префаб на него не ссылается → в билд не попадает |
| `Animations/Generated/MotionBricks/walk_to_jog/Male/WalkToJog_Male.bake.json` | `Assets/` | TextAsset без ссылок; нужен меню Review по пути — **не удалять** |
| **Пакеты** | — | MB **не добавил ни одного пакета**. Единственное изменение `Packages/manifest.json` в диапазоне — удаление `app.rive.rive-unity` (не MB). Sentis / Inference Engine не ставились. |
| **Модель 758 МБ / TensorRT / venv** | вне Unity (`C:\Projects\motion_bricks`, `docs/.../tools`) | в проекте и билде отсутствуют |

**Единственная ссылка из сцен:** объект `MB_Probe_Player` в `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — `Transform` + `MotionBricksBakedPlayback` + `MotionBricksBakedRootMotion`, без меша и Animator. Оба компонента fail-closed по `mode` → в игре молчат. **Рекомендация: удалить объект из сцены** (сейчас не удалён: сцена dirty от другой работы).

**Единственный продовый хук:** `NetworkPlayer.cs:1399` → `MotionBricksLocomotionBridge.TryConsumeDisplacement(...)`. Реестр источников пуст → метод возвращает `false`, движение не меняется. Оставлен осознанно: он же нужен baked-клипам с root motion (условие 4 в §4).

**Итог по билду: не влияет.** Ноль байт модели, ноль пакетов, один инертный объект в сцене, несколько КБ IL. Откат («выключить насовсем») описан в [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md); сейчас он не требуется.

---

## 6. Что оставить для дальнейшей работы

**Оставить (это и есть линия «генератор переходных стадий»):**

| Что | Где |
|---|---|
| Discovery-бейкер, batch, отчёты | `Assets/_Project/Scripts/Editor/Animation/MotionBricksBakeJob.cs`, `…SpikeBaker.cs`, `…BakeReport.cs` |
| Валидатор и пробы | `…ClipValidator.cs`, `…ClipProbe.cs` |
| Foot IK / leg IK / self-tests | `…FootIk.cs`, `…LegIk.cs`, `…GuardSelfTest.cs`, `…LoopSelfTest.cs` |
| Экспортёры контекста и офлайн-среза | `…LocomotionSourceExporter.cs`, `…OfficialSliceExporter.cs` |
| Меню Review (ручная приёмка клипа) | `…ReviewMenu.cs` |
| Доставка запечённого клипа в игру | `MotionBricksBakedPlayback.cs`, `MotionBricksBakedRootMotion.cs`, `MotionBricksLocomotionBridge.cs` |
| Python-пайплайн: `extract_soma_bind`, `locomotion_pack`, `official_vertical_slice`, `verify_*` | `docs/world/MOTIONBRICK_animations/tools/` |
| База знаний: что такое MB, пайплайн, альтернативы, источники, откат | `01`–`06` в корне папки |

**Заморозить (не удалять, не развивать):** `MotionBricksRuntimePlayback.cs`, `MotionBricksRuntimeClient.cs`, `MotionBricksSomaRetarget.cs`, `MotionBricksRuntimeReviewMenu.cs`, `tools/runtime_sidecar.py`. Это запись T-MB06 (что именно проверялось и с какими числами) и готовая A/B-обвязка, если runtime когда-нибудь понадобится снова. Удаление не даёт ничего, кроме потери истории.

---

## 7. Карта архива документации

Тикетные документы закрытых работ и дубли research перенесены в `archive/` (содержимое не менялось, только путь):

| Было | Стало |
|---|---|
| `07_T-MB02_WORKTREE_BASELINE_FINDINGS.md` | `archive/07_…` |
| `08_T-MB02_RETARGET_FINDINGS.md` | `archive/08_…` |
| `09_T-MB02_ANALYSIS_AND_NEXT.md` | `archive/09_…` |
| `10_T-MB04B_RUNTIME_HARNESS.md` | `archive/10_…` |
| `11_T-MB03_BAKER_AND_GUARDS.md` | `archive/11_…` |
| `12_T-MB04C_ROOT_MOTION_IN_PLAYER.md` | `archive/12_…` |
| `13_T-MB05_LEG_IK_AND_LOOP.md` | `archive/13_…` |
| `14_OFFICIAL_VERTICAL_SLICE.md` | `archive/14_…` |
| `15_LOCOMOTION_TRANSITION_PACK_V1.md` | `archive/15_…` |
| `16_OFFLINE_REJECTED_RUNTIME_NEXT.md` | `archive/16_…` |
| `17_T-MB06_RUNTIME_SIDECAR.md` | `archive/17_…` |
| `18_T-MB06_AXIS_AND_UNITY_REFERENCE_ANALYSIS.md` | `archive/18_…` |
| `batches/locomotion_v1.json` (batch отклонённого пака) | `archive/batches/locomotion_v1.json` |
| `beam_research.md`, `hy4_research.md`, `mimo_research.md` (три варианта одного ресерча 18.11) | `archive/research_2026-11-18/` |

Осталось в корне: `00` (этот файл), `01`–`06`, `README.md`, `HANDOFF.md`, `tools/`, `baseline/`.

Примечание к ссылкам: взаимные ссылки внутри `archive/` (07↔09↔16↔17) продолжают работать; ссылки внутри архивных документов на корневые файлы (`01`–`06`, `HANDOFF.md`) указывают на старые пути — искать по имени в корне папки. Содержимое архивных файлов не переписывалось намеренно.

---

## 8. Как вернуться к линии «генератор переходных стадий»

1. Сформулировать **один** недостающий клип (пример: вход в присед на бегу 4 м/с, которого нет в библиотеке).
2. Взять контекст из авторского клипа: 4 реальных кадра (`official_vertical_slice.py` / `extract_soma_bind.py`).
3. Задать 1–4 target-позы (поза из другого авторского клипа или ручная).
4. Сгенерировать (`sa-inbetween` CPU или официальный TensorRT — см. `05_LOCAL_RESOURCES.md`) и запечь бейкером.
5. Прогнать `MotionBricksClipValidator` + зафиксировать ground speed клипа.
6. Подключить через `AnimatorOverrideController` либо `MotionBricksBakedPlayback`; при root motion — `MotionBricksBakedRootMotion` с `ClipSpeed` по замеру.
7. Записать результат в `*.bake.report.json` и в тикет-док; Play Mode проверяет **заказчик**.

---

## 9. Правила, которые не изменились

- Агент не запускает Play Mode, тесты и скриншоты — это делает заказчик.
- Не редактируются: `Packages/manifest.json`, `PlayerAnimation.controller`, `NetworkPlayer.prefab`, `NpcBrain`, `SkillAnimationPlayer`, `Combat`.
- `baseline/check_baseline.ps1` — до и после любого шага, ожидается `BASELINE OK`.
- Никаких neural-поз для хитбоксов, урона и таймингов боя.
- Модель/TensorRT/CUDA не попадают в shipping build.
- `git reset --hard` / `git checkout -- .` в `main` не применяются.

---

*Связанные документы: [README.md](README.md) · [01_RESEARCH.md](01_RESEARCH.md) · [03_ALTERNATIVES.md](03_ALTERNATIVES.md) · [06_SAFE_ROLLOUT_AND_ROLLBACK.md](06_SAFE_ROLLOUT_AND_ROLLBACK.md) · [HANDOFF.md](HANDOFF.md) · архив [16](archive/16_OFFLINE_REJECTED_RUNTIME_NEXT.md), [17](archive/17_T-MB06_RUNTIME_SIDECAR.md), [18](archive/18_T-MB06_AXIS_AND_UNITY_REFERENCE_ANALYSIS.md).*
