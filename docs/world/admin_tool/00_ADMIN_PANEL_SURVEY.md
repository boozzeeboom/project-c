# Admin-панель (система админа) — сводка «от простого к сложному»

> Тип документа: **survey** (skill `project-c-design-doc-workflow`, вариант 📋).
> Только аналитика, без кода. Тикетов и roadmap здесь нет — следующий шаг выбирается пользователем.
> Дата: 2026-09-15. Автор: Mavis.
> Связано: `docs/world/admin_tool/perfomance/PERFORMANCE_MONITORING_RESEARCH.md`,
> `docs/Character/input-system/` (10_CURRENT_STATE, 20_KEYBIND_INVENTORY),
> `docs/world/floatingorigin/README.md`.

## TL;DR

Все «админские» возможности уже есть в коде, но разбросаны по **~25 точкам в 6+ подсистемах**:
кнопки F3–F10 заняты тремя разными владельцами, free-fly живёт только в `WorldCamera`
(редакторская камера мира, клавиша V), god/noclip **отсутствуют вообще**,
телепорты — это 4 несвязанных API, лог-флаги — это ~20 локальных `bool` без общего рубильника,
а вайп сейвов — только через MainMenu debug-панель (`PersistenceDebugTools`).

Предложение — уровни сложности L0→L4 (таблица ниже). Рекомендация: **L0 сейчас
(этот документ + шпаргалка), L1 следующим тикетом** (одно Editor/Runtime окно,
только вызывающее существующие публичные методы, ничего не ломая).
L3–L4 — только после решения открытых вопросов в §7.

### Уровни

| Уровень | Что даёт | Трудоёмкость | Зависимости / риск |
|---|---|---|---|
| **L0 — Инвентарь + шпаргалка (этот документ)** | Одна страница: где что лежит, какая кнопка, какой файл:строка | ✅ готово (2 файла) | Нет кода, ничего не ломает |
| **L1 — Единое Admin-окно (фасад, additive-only)** | Один Runtime-UI + один Editor-Window: кнопки вызывают существующие `public` методы (ToggleFly, TeleportToPeak, RequestControlledRebase, DeleteAllSaves, HUD-тогглы). Никакой новой логики | ~4–8 ч | Только новые файлы; правит F-конфликты чтением `InputBindingsConfig` |
| **L2 — Runtime-консоль + реестр команд** | Тильда-консоль (`help`, `fly`, `tp x y z`, `rebase`, `hud perf on`, `wipe saves`), реестр `AdminCommandRegistry`, история, автодополнение | ~8–16 ч поверх L1 | UI Toolkit; команды — тонкие обёртки над L1-фасадом |
| **L3 — AdminConfig (SO) + ребинд + пресеты** | `AdminConfig` ScriptableObject: все хоткеи через `InputBindingsConfig`-совместимые слоты, пресеты («Тест стриминга», «Полёт», «Тихий режим без логов»), per-system log levels | ~8–12 ч поверх L2 | Паттерн `v2-so-config-default-fallback`; нужен `.asset` (создаёт Unity, не руками) |
| **L4 — Серверный админ (NGO)** | Сервер-авторитетные команды: `[Rpc(SendTo.Server)]` + роль админа, кик/бан/телепорт игрока сервером, audit-лог | ~16–30 ч, отдельный дизайн-док | Netcode-паттерны (`project-c-netcode-patterns`); **не** раньше 90% механик |

---

## 1. Что уже есть в проекте (reuse map)

Детальная таблица со всеми `файл:строка` — в `01_TOGGLE_INVENTORY.md`. Здесь — сжатая карта по группам.

### 1.1 Полёт / свободная камера

| Возможность | Где | Как включить |
|---|---|---|
| Free-fly + телепорты по пикам | `Assets/_Project/Scripts/Core/WorldCamera.cs:65–66,136–153,520–624` | Клавиши **V** (ToggleFly), **N**/**B** (след./пред. пик), **R** (случайный), **H** (возврат к высоте). Свои `InputAction` внутри класса, мимо `InputBindingsConfig` |
| God-mode / noclip игрока | — | **Отсутствуют.** Игрок — `CharacterController`/NetworkPlayer, коллизии не отключаются нигде |

### 1.2 HUD / оверлеи (все OnGUI, все разрозненные)

| HUD | Файл | Тоггл | Статус |
|---|---|---|---|
| ShipDebugHUD (fuel/thrust/meziy) | `Ship/ShipDebugHUD.cs:45–53` | **F3** | Работает |
| ProjectCPerfHUD (FPS/NPC/Ships/Clouds) | `Core/ProjectCPerfHUD.cs:70` | **F3** (`_toggleKey`) | **Отключён** — весь файл под `#if FALSE`, шапка требует ручного включения |
| MeziyStatusHUD_Legacy | `Ship/MeziyStatusHUD_Legacy.cs:73–77` | **F4** | Работает (legacy) |
| SceneDebugHUD (сетка сцен/чанки) | `UI/SceneDebugHUD.cs` (весь файл) | Нет тоггла — `showGridOnUpdate=true`, `DontDestroyOnLoad` | Всегда включён |
| DayNight overlay | `Core/DayNight/DayNightController.cs:42,935` + `DayNightProfile.cs:78` | `showDebugOverlay && profile.showDebugInfo` | Два флага в двух файлах |
| NGO метрики (без UI) | `Core/NgoMetricsCollector.cs:15–111` | Нет тоггла | Только `GetSummary()` для чужого HUD |
| StreamingTest встроенный HUD | `World/Streaming/StreamingTest.cs:306` | **F10** (рефлексией ставит `showDebugHUD` стриминг-менеджеру) | Работает, но через рефлексию приватного поля |

**Конфликт F3**: `ShipDebugHUD`, `ProjectCPerfHUD` и слот `GameAction.DebugF3`
в `Input/InputBindingsConfig.cs:182` — три владельца одной кнопки.
`DebugF3/DebugF4` в конфиге при этом **никто не читает** (только дефолты в списке).

### 1.3 Сдвиг мира / стриминг (самая болезненная зона — два владельца F8/F9)

| Путь | Файл | Клавиши |
|---|---|---|
| Новый (канон): `GlobalMotionControlledRebaseSlice` | `World/FloatingOrigin/Network/GlobalMotionControlledRebaseSlice.cs:182–209` | **F8** = rebase, **F9** = rebase-with-rollback; плюс `[ContextMenu]` `RequestControlledRebase` / `...WithRollback` |
| Старый: `StreamingTest` + `StreamingTest_AutoRun` | `World/Streaming/StreamingTest.cs:243–310`, `StreamingTest_AutoRun.cs:163–173` | **F5** next pos, **F6** prev pos, **F7** load chunks, **F8** → `FloatingOriginMP.ResetOrigin()` (!), **F9** → `ChunkVisualizer.ToggleChunkGrid` (Editor only, через рефлексию), **F10** HUD |

F8/F9 обрабатываются **обоими** путями одновременно: слайс делает controlled rebase,
а `StreamingTest` — прямой `ResetOrigin()`. Это главный кандидат на объединение в L1
(оставить только слайс, старый путь — за флаг `AdminConfig.legacyStreamingKeys`).

### 1.4 Телепорты / респавн / маршрут

- Телепорт игрока (сервер): `Core/ShipPosition/PlayerPositionServer.cs:140–185`
  (`TeleportPlayer` + `restore`, флаг `_debugMode=true` по умолчанию — §1.6).
- Телепорт корабля (сервер): `PeacefulShip/Stations/NpcShipController.cs:275` (`ServerTeleport`).
- Респавн игрока: `Player/PlayerRespawnTracker.cs:103–144` (`PerformRespawn`, `TeleportToClientRpc`),
  точки — `World/RespawnManager.cs` + `Player/RespawnPointData.cs`.
- Телепорт камеры: `WorldCamera.TeleportToPeak(i)` (§1.1).
- Телепорт тестовых позиций: `StreamingTest.TeleportToTestPosition(i)` (§1.3).
- Маршрут NPC-кораблей: гизмо-отрисовка `PeacefulShip/Editor/NpcShipRouteDrawer.cs`,
  точки — `PeacefulShip/Core/NpcShipRoute.cs`, расписание — `NpcShipSchedule.cs`.
  Отдельного «показать маршрут» в рантайме нет — только Editor-отрисовка.
- Сквозь объекты (noclip): отсутствует (см. §1.1).

### 1.5 Мир: погода/шторм/сутки/ветер (крутилки без единого пульта)

`ServerStormManager`, `ServerWeatherController`, `StormCellDirector`
(`[ContextMenu] Force Regenerate Storm`, `Save Current as Defaults`),
`WindManager`, `AltitudeCorridorSystem` (`MenuItem Tools/Project C/Setup Altitude Corridors`),
`DayNightController`, `CloudManager/CloudSystem/CloudLayer`
(`[ContextMenu] Regenerate/Clear/Log Cloud Count`). Полный список ContextMenu — в `01_§4`.

### 1.6 Лог-флаги (~20 локальных bool, общего рубильника нет)

Паттерн везде один: `[SerializeField] private bool _showDebugLogs/_debugMode/_verbose`
+ `if (_flag) Debug.Log(...)`. Крупнейшие:
`AI/NpcSpawner.cs:58` (+ `NpcSpawnerConfig.cs:200 showDebugLogs`),
`Core/ShipPosition/PlayerPositionServer.cs:30` (`_debugMode=true` — **включён по умолчанию**),
`ShipPositionServer.cs:47`, `World/Clouds/LocalDensityBuffer.cs:240` (`_verboseLogging`, дамп по T),
`Core/DayNight/ConstellationController.cs:61` (`showDebugGizmos`),
`Player/PlayerRespawnTracker.cs:31–33` (`_debugLog`),
`World/FloatingOrigin/*` — события `runtimeRebase.*` через `GlobalMotionRuntimeEvidenceProbe`
(читать через probe, отдельного вьювера нет).
Флаги `showDebugLogs/showDebugHUD` умеет массово включать `Core/StreamingSetupRuntime.cs:217–223`
(рефлексией по `SerializedObject` — хрупко, только Editor-инициализация).

### 1.7 Сейвы / вайп

`UI/MainMenu/PersistenceDebugTools.cs:12–83` — статические `Delete*Saves()`
(всё, позиции, инвентарь, прогрессия, кастомизация, квесты, скилл-бинды, ключи, время мира, торговля).
Вызываются из MainMenu debug-панели; отдельного хоткея нет.

### 1.8 Ребинд-инфраструктура (что переиспользовать, а не изобретать)

- `Input/InputBindingsConfig.cs` — SO-список `ActionBinding {action, category, key, mouseButtonRaw}`,
  категории `ActionCategory` (есть `Debug`), дефолты с `DebugF3/DebugF4`. Игрок уже правит через…
- `UI/Settings/KeybindingsWindow.cs` + `SkillBindingWindow.cs` — UI ребинда (паттерн для L1/L3).
- `Player/PlayerInputReader.cs` — **dead code** (события не подписаны), не трогать
  (зафиксировано в `docs/Character/input-system/`).
- `Player/NetworkPlayer.cs:2672–2698` — `IsActionHeld/IsActionJustPressed` поверх `InputBindingsConfig`
  (образец чтения конфига без ломки прямых `Keyboard.current` чтений).

---

## 2. Gap-анализ (чего нет и почему неудобно)

1. **Нет единой точки входа.** В Play Mode надо помнить: V — камера, F3 — корабль,
   F4 — мезий, F5–F10 — стриминг, F8/F9 — два разных ребейза, T — дамп плотности облаков,
   Esc — закрыть панели. Список живёт только в этом документе.
2. **F3/F8/F9-конфликты** (§1.2–1.3). Нажатие делает два действия сразу.
3. **Нет god/noclip/speed.** Для пролёта сквозь геометрию сейчас только WorldCamera-fly,
   но это отдельная камера, не игрок.
4. **Нет «показать маршрут».** NPC-маршруты видны только в Editor (drawer), в Play Mode — нет.
5. **Нет общего mute логов.** Чтобы заглушить спам, надо лазить по ~20 инспекторам.
6. **Нет серверной стороны.** Всё перечисленное — локальные клиентские тогглы;
   телепортнуть другого игрока / дать роль админа — нечем (это L4, осознанно позже).

---

## 3. Как реализовать: L0→L4 подробно

### L0 — Шпаргалка (готово)

Файлы: этот `00_ADMIN_PANEL_SURVEY.md` + `01_TOGGLE_INVENTORY.md`.
Держать актуальным: при добавлении нового F-тоггла — дописать строку в `01_`, иначе
документ протухнет за месяц (прецедент: `CUSTOMIZATION_ANALYSIS.md` протух после
появления `ShipModuleServer`).

### L1 — Admin-фасад (рекомендуемый следующий шаг)

**Принцип: только новый код, только вызовы существующих `public` методов.**
Запрещено: менять `NetworkPlayer.Update`, `PlayerInputReader` (dead code),
`ClientSceneLoader`, переносить `NetworkManager`, удалять `ScenePlacedObjectSpawner`
(см. AGENTS.md §Scene architecture), писать `.meta`/`.asmdef` руками.

Новый код (2 файла + 1 окно):

- `Assets/_Project/Scripts/Admin/AdminFacade.cs` — синглтон-фасад, методы-обёртки:
  `ToggleFly()`, `TeleportToPeak(i)`, `TeleportPlayerTo(Vector3)`,
  `RequestRebase()/RequestRebaseWithRollback()`, `SetHudVisible(name, bool)`,
  `SetAllLogFlags(bool)`, `WipeSaves(scope)` (делегирует `PersistenceDebugTools`),
  `ToggleRouteOverlay(bool)`. Внутри — `FindAnyObjectByType` + вызов найденного
  компонента; если компонент отсутствует — `Debug.LogWarning`, не исключение.
- `Assets/_Project/Scripts/Admin/AdminRuntimeWindow.cs` — UI Toolkit окно
  (паттерн `CharacterWindow`: `Clear+CloneTree+Add`, `Resources.Load` fallback —
  по памяти проекта `CharacterWindow`-паттерн работает, `UI_TOOLKIT_GUIDE §2` — нет).
  Вкладки: Полёт / Телепорт / HUD / Мир / Логи / Сейвы. Открытие — новая запись
  в `InputBindingsConfig` (`GameAction.AdminPanel`, дефолт **F12** — свободна, см. `01_§6`),
  чтение через `NetworkPlayer.IsActionJustPressed`-совместимый хелпер.
- `Assets/_Project/Scripts/Admin/Editor/AdminEditorWindow.cs` — Editor-версия
  тех же кнопок (работает и в Edit Mode: `Regenerate Clouds`, `Setup Altitude Corridors`,
  `RequestControlledRebase`, вайп сейвов).

Floating Origin: фасад **не хранит** мировые `Vector3` между кадрами
(иначе нужен `ApplyRebaseTranslation`-хук, AGENTS.md §Floating Origin 🟡).
Все точки телепорта резолвятся в момент нажатия кнопки. Если позже появится
«сохранённая точка возврата» — ей понадобится хук сдвига + маркер `runtimeRebase.*`.

NGO: фасад вызывает только клиентские методы и серверные методы **своего** объекта
(`PlayerPositionServer` уже серверный синглтон). Чужих `[Rpc]` не дёргает.

Проверка L1: Play → F12 → каждая кнопка → ожидаемый эффект из `01_` + 0 errors;
F8 → `runtimeRebase.Completed` + объект на месте; F9 → возврат.

### L2 — Консоль

`AdminCommandRegistry` (`Dictionary<string, Func<string[], string>>`) + UI Toolkit
консоль на тильду (backquote — свободна, см. `01_§6`). Команды — те же методы фасада:
`fly`, `tp <x> <y> <z>`, `peak <i>`, `rebase[/rollback]`, `hud <name> on|off`,
`logs mute|unmute`, `wipe <scope>`, `route show|hide`, `help`.
История + автодополнение по Tab. Лог-вывод консоли — через существующие probe-события
(`runtimeRebase.*`, `respawn.*`), не новый логгер.

### L3 — AdminConfig (SO)

`AdminConfig : ScriptableObject` + `AdminConfig.Default` (паттерн
`v2-so-config-default-fallback`): хоткеи панели/консоли/каждого HUD, флаг
`legacyStreamingKeys` (глушит старый F5–F10 путь из `StreamingTest`),
`muteAllLogs` (мастер-флаг поверх per-system bool — сами bool не удаляются),
пресеты вкладок. Ребинд — копией паттерна `KeybindingsWindow` (не нового UI-парсера).
`.asset` создаёт Unity через `CreateAssetMenu` (руками `.meta` не писать).

### L4 — Серверный админ (отдельный дизайн-док, после 90% механик)

Роль админа на сервере (`AdminServer : NetworkBehaviour`, `HashSet<ulong> adminClientIds`),
команды `[Rpc(SendTo.Server)]` с проверкой роли, ответы `[Rpc(SendTo.RequestingClient)]`
или targeted ClientRpc, audit-лог (кто/что/когда). Кейсы: телепорт игрока сервером,
кик, выдача предметов через существующие `Trade`/инвентарные серверы, стоп шторма.
Обязательно: threat-model (клиент не может сам себя назначить админом),
rate-limit (прецедент — `NpcSpawner.CheckRateLimit`), сохранение ролей
(паттерн `PERSIST01`: мировые координаты + кумулятив сдвига).

---

## 4. Industry-справка (что подсмотреть)

| Проект | Что украсть для нас |
|---|---|
| Rust / Garry's Mod (F1-консоль, `noclip`, `god`, `teleport`) | L2-синтаксис команд; разделение client/server команд — прообраз L4 |
| Unreal `cheat manager` (`Ghost`, `Fly`, `Walk`, `Teleport`) | L1: отдельные режимы перемещения как переключаемый enum, а не bool-флаги |
| Unity FPS-sample admin overlay | L1: вкладки + мастер-тоггл логов, как у нас задумано |
| EVE Online GM-инструменты | L4: audit-лог каждого админ-действия (кто/кого/куда) |

---

## 5. Зависимости и порядок (когда L1 не конфликтует)

- Параллельно можно: L1-фасад + `T-FO09R` (каталог сцен) — разные файлы.
- До L1 желательно: решить **F3** (кто владелец: ShipDebugHUD vs PerfHUD) и **F8/F9**
  (слайс vs StreamingTest) — иначе фасад унаследует конфликты. Решение — 1 строка
  в `01_§6`, от пользователя.
- Не трогать до L4: `NetworkPlayerSpawner`, `ClientSceneLoader`, `ScenePlacedObjectSpawner`,
  `NetworkPrefabsList` (известная поломка динамического спавна — отдельный тикет).

---

## 6. Что дальше (handoff)

1. Пользователь отвечает на 6 вопросов из §7 (строки `**ответ:**` — прямо в файле).
2. Следующая сессия пишет дизайн-док L1 (`docs/world/admin_tool/L1_DESIGN.md`):
   схема `AdminFacade`, layout окна по вкладкам, список кнопок → методов,
   тикеты T-ADM-01… с оценкой, чеклист приёмки.
3. Без ответов из §7 — только L0 (этот документ), код не пишем.

---

## 7. Открытые вопросы (ответь прямо в файле)

### Q1. Владелец F3

Сейчас F3 одновременно у `ShipDebugHUD` (работает), `ProjectCPerfHUD` (молчит под
`#if FALSE`) и слота `DebugF3` (никто не читает). Кому оставить F3 в L1?

- а. ShipDebugHUD остаётся на F3, PerfHUD переезжает в админ-панель без хоткея (рекомендую).
- б. Наоборот: PerfHUD на F3, ShipDebugHUD — только кнопкой из панели.
- в. Оба без хоткеев, F3 полностью отдаём админ-панели.

**ответ:** шип дебаг - вобще нерабочий старый его нужно убирать. перфхуд в админ панель как рекомендуешь.

> ✅ Финальное решение D1: `ShipDebugHUD` (+ `MeziyStatusHUD_Legacy` как его пара по S-HUD-05) — удалить (T-ADM-00);
> PerfHUD — только кнопкой из панели, без хоткея; F3 — в резерв, никому.
> Reversal: рекомендовался вариант «а» (Ship остаётся на F3) — пользователь поправил:
> код уже считает ShipDebugHUD мёртвым (S-HUD-05 в `ShipController.cs:2184`).

### Q2. F8 / F9 — один владелец

Оставить канонический путь (`GlobalMotionControlledRebaseSlice`: F8 = rebase,
F9 = rollback) и погасить старый (`StreamingTest`: F8 = прямой `ResetOrigin`,
F9 = `ChunkVisualizer`), или сохранить оба?

- а. Только слайс; старый путь за флагом `legacyStreamingKeys=false` (рекомендую).
- б. Сохранить оба, в панели подписать какой есть какой.

**ответ:** убираем с хоткеев уберем в админ-панель

> ✅ Финальное решение D2: `handleHotkeys=false` по умолчанию (T-ADM-06), все функции —
> кнопками вкладок В2/В4/В7.

### Q3. God / noclip / speed — нужны ли в L1?

Их нет в коде, это единственный кусок новой логики в L1 (отключение коллизий
игрока + множитель скорости). Делаем в L1 или откладываем до L2?

- а. Да, минимальный god+noclip+speed в L1 (рекомендую — это твои «полет, сквозь объекты»).
- б. Нет, L1 только фасад над существующим; читы движения — позже.

**ответ:** да по рекомендации

> ✅ Финальное решение D3: god+noclip+speed в L1 (T-ADM-02 клиент, T-ADM-05 сервер).

### Q4. Мастер-mute логов

`PlayerPositionServer._debugMode=true` и `ConstellationController.showDebugGizmos=true`
включены по умолчанию и спамят. В L1 добавить мастер-тоггл, глушащий все per-system
флаги, или оставить каждый флаг ручным?

- а. Мастер-mute в L1 (сами поля не удаляем, только поверх) (рекомендую).
- б. Оставить ручное управление.

**ответ:** да по рекомендации

> ✅ Финальное решение D4: мастер-mute в L1 через `AdminLogBus`, сами поля не трогаем (T-ADM-03).

### Q5. Объём L1: какие вкладки в первую очередь?

Предлагаю 6 вкладок: Полёт / Телепорт / HUD / Мир / Логи / Сейвы. Урезать?

- а. Все 6 (рекомендую).
- б. Только Полёт + Телепорт + HUD (минимум для ежедневной работы).
- в. Свой набор (напиши).

**ответ:** все что найдешь - максимально полный

> ✅ Финальное решение D5: 8 вкладок (Полёт/Телепорт/Респавн/HUD/Мир/Маршрут/Логи/Сейвы), §3 L1_DESIGN.

### Q6. Хоткей панели и консоли

Свободны F1, F2, F11, F12 и backquote. Предложение: **F12** — панель,
**backquote (`)** — консоль L2.

- а. Согласен (рекомендую).
- б. Свои кнопки (напиши).

**ответ:** ф12 пойдет

> ✅ Финальное решение D6: F12 — панель (T-ADM-01/04), backquote — резерв под консоль L2.

---

## 8. Связанные документы

- `L1_DESIGN.md` — детальный дизайн L1 (фасад, 8 вкладок, тикеты T-ADM-00…09).
- `01_TOGGLE_INVENTORY.md` — детальный инвентарь (хоткеи, HUD, телепорты, ContextMenu, лог-флаги).
- `perfomance/PERFORMANCE_MONITORING_RESEARCH.md` — мониторинг производительности (§4.2 про PerfHUD).
- `docs/Character/input-system/10_CURRENT_STATE.md`, `20_KEYBIND_INVENTORY.md` — карта игровых
  биндов (не пересекаться с WASD/Space/Shift/E/F/P/T/Esc).
- `docs/world/floatingorigin/README.md` — правила нового контента при сдвиге мира.