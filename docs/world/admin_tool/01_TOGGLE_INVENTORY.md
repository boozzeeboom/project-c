# Инвентарь тогглов, хоткеев и точек входа для админ-панели

> Детализация к `00_ADMIN_PANEL_SURVEY.md`. Все пути — от корня репозитория.
> Проверено grep 2026-09-15. Если строка уехала — ищи по имени метода/поля.

## §1. Полная карта хоткеев

| Клавиша | Действие | Файл:строка | Владелец | Конфликт? |
|---|---|---|---|---|
| V | ToggleFly (WorldCamera) | `Scripts/Core/WorldCamera.cs:136,150,520` | WorldCamera | Нет |
| N / PageUp | TeleportToNextPeak | `WorldCamera.cs:138,151,588` | WorldCamera | Нет |
| B / PageDown | TeleportToPreviousPeak | `WorldCamera.cs:142,152,602` | WorldCamera | Нет |
| R | TeleportToRandomPeak | `WorldCamera.cs:145,153,616` | WorldCamera | ⚠️ R также может использоваться скиллами (см. input-system/20) |
| H | ReturnToHeight | `WorldCamera.cs:146` | WorldCamera | Нет |
| WASD + Mouse + Shift(boost) + Space | Движение WorldCamera | `WorldCamera.cs:126–147,484` | WorldCamera | Пересекается с игровыми WASD — но это отдельная камера |
| F3 | ShipDebugHUD visible | `Ship/ShipDebugHUD.cs:45–53` | Ship | ⚠️ КОНФЛИКТ: та же F3 у PerfHUD + слот DebugF3 |
| F3 | ProjectCPerfHUD visible | `Core/ProjectCPerfHUD.cs:70` | Core | ⚠️ тот же F3; файл под `#if FALSE` — молчит |
| F3 | `GameAction.DebugF3` дефолт | `Input/InputBindingsConfig.cs:182` | Input | Никто не читает |
| F4 | MeziyStatusHUD_Legacy visible | `Ship/MeziyStatusHUD_Legacy.cs:73–77` | Ship | + слот DebugF4 никто не читает |
| F5 | Next test position + teleport | `World/Streaming/StreamingTest.cs:243` (+ AutoRun дублирует `:165`) | Streaming | Нет |
| F6 | Prev test position + teleport | `StreamingTest.cs:250` | Streaming | Нет |
| F7 | LoadChunksAroundPlayer | `StreamingTest.cs:257` | Streaming | Нет |
| F8 | Controlled rebase (канон) | `World/FloatingOrigin/Network/GlobalMotionControlledRebaseSlice.cs:182–197` | FloatingOrigin | ⚠️ КОНФЛИКТ: тот же F8 в StreamingTest → `FloatingOriginMP.ResetOrigin()` (`StreamingTest.cs:281`) |
| F9 | Controlled rebase with rollback (канон) | `GlobalMotionControlledRebaseSlice.cs:191–209` | FloatingOrigin | ⚠️ КОНФЛИКТ: тот же F9 в StreamingTest → `ChunkVisualizer.ToggleChunkGrid` (`StreamingTest.cs:292`) |
| F10 | Debug HUD (рефлексией `showDebugHUD`) | `StreamingTest.cs:306` | Streaming | Нет |
| F12 | — свободна — | — | — | ✅ кандидат под Admin-панель |
| Backquote (`) | — свободна — | — | — | ✅ кандидат под консоль L2 |
| T | CommPanel (игра) / дамп плотности облаков (`LocalDensityBuffer.cs:240`, только при `_verboseLogging`) | `Player/*`, `World/Clouds/LocalDensityBuffer.cs:240` | Игра+Clouds | Осторожно с T |
| Esc | CloseTopPanel | `InputBindingsConfig.cs:186` + ~6 окон (Character, SkillTree, Crafting, Dialog, NetworkUI, UIManager) | UI | Не занимать |

Свободные F: **F1, F2, F11, F12** (F1 — классика под help/админку, F12 — под панель).

## §2. HUD-реестр (для вкладки «HUD» в L1)

| # | HUD | Класс → файл | Тоггл сейчас | Предложение L1 |
|---|---|---|---|---|
| H1 | Ship | `ProjectC.Ship.ShipDebugHUD` → `Ship/ShipDebugHUD.cs` | F3 | Кнопка + чекбокс; решить владельца F3 |
| H2 | Perf | `ProjectC.Core.ProjectCPerfHUD` → `Core/ProjectCPerfHUD.cs` | F3, но `#if FALSE` | Сначала включить (`#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR`), потом кнопка |
| H3 | Meziy legacy | `Ship/MeziyStatusHUD_Legacy.cs` | F4 | Кнопка; пометить legacy |
| H4 | Scene grid | `ProjectC.UI.SceneDebugHUD` → `UI/SceneDebugHUD.cs` | Нет (всегда вкл) | Добавить `SetVisible(bool)` + кнопку; сейчас `showGridOnUpdate`, `updateIntervalMs` |
| H5 | DayNight | `DayNightController.showDebugOverlay` + `DayNightProfile.showDebugInfo` | Два флага | Одна кнопка ставит оба |
| H6 | NGO summary | `NgoMetricsCollector.GetSummary()` → `Core/NgoMetricsCollector.cs:111` | Нет UI | Строка в админ-панели (обновление 1/с) |
| H7 | Streaming | Приватное `showDebugHUD` стриминг-менеджера через F10 | F10 рефлексией | Заменить рефлексию прямым свойством при L1 |

## §3. Телепорты и респавн (для вкладки «Телепорт»)

| # | API | Сигнатура | Сторона |
|---|---|---|---|
| T1 | `WorldCamera.TeleportToPeak(int)` + Next/Prev/Random | `Core/WorldCamera.cs:540–624` | Клиент (камера) |
| T2 | `StreamingTest.TeleportToTestPosition(int)` | `World/Streaming/StreamingTest.cs` + массив `testPositions`, `teleportHeight` | Клиент (тест) |
| T3 | `PlayerPositionServer.TeleportPlayer(np, pos)` + restore | `Core/ShipPosition/PlayerPositionServer.cs:140–185` | Сервер |
| T4 | `NpcShipController.ServerTeleport(worldPos, worldRot)` | `PeacefulShip/Stations/NpcShipController.cs:275` | Сервер |
| T5 | `PlayerRespawnTracker.PerformRespawn()` → `TeleportToClientRpc` | `Player/PlayerRespawnTracker.cs:103–144` | Сервер→клиент |
| T6 | Точки: `RespawnManager` (`World/RespawnManager.cs`) + `RespawnPointData` | Данные | Оба |

## §4. ContextMenu / MenuItem — кандидаты в кнопки L1 (всё additive, уже есть)

| Кнопка L1 | Источник |
|---|---|
| Regenerate/Clear Layer, Regenerate/Clear All, Log Cloud Count | `Core/CloudLayer.cs:310,319`, `CloudManager.cs:216,248`, `CloudSystem.cs:272,285` |
| Validate Profile, Log All Phases | `Core/DayNight/DayNightProfile.cs:244,250` |
| Force Regenerate Storm, Save Current as Defaults | `World/Clouds/StormCellDirector.cs:356,420` |
| Request Controlled Rebase [/With Rollback] | `World/FloatingOrigin/Network/GlobalMotionControlledRebaseSlice.cs:197,203` |
| Re-Discover NPC Ships | `PeacefulShip/Network/NpcShipServer.cs:142` |
| Refresh Colliders | `PeacefulShip/Stations/NpcProximityZoneBuilds.cs:56` |
| DEBUG Force re-apply snapshot (×2) | `Player/CharacterCustomisationApplier.cs:391`, `CharacterEquipmentVisualApplier.cs:313` |
| DEBUG Force SaveCurrent | `Customisation/UI/CustomisationWindow.cs:512` |
| Force Refresh (cargo visual) | `Ship/Cargo/ShipCargoVisual.cs:559` |
| Force Reinitialize (streaming setup) | `Core/StreamingSetupRuntime.cs:297` |
| Regenerate World | `Core/WorldGenerator.cs:475` |
| Tools/Project C/Create Port Station… | `Scripts/Editor/PortStationCreator.cs:41` |
| Tools/Project C/Setup Altitude Corridors | `Ship/AltitudeCorridorSystem.cs:218` |
| ProjectC/Clouds/Bake 3D Noise, Generate Blue Noise, Compare HLSL vs C# | `World/Clouds/Editor/CloudNoiseBaker.cs:17,103,147` |
| Tools/Cloud/Diagnostic – Quick Check | `Scripts/Editor/CloudDiagnostic.cs:11` |

## §5. Лог-флаги (для вкладки «Логи» — мастер-mute поверх, сами поля не удалять)

| Система | Поле | Файл:строка |
|---|---|---|
| NPC спавнер | `_showDebugLogs` + `NpcSpawnerConfig.showDebugLogs` | `AI/NpcSpawner.cs:58`, `AI/NpcSpawnerConfig.cs:200` |
| Позиция игрока (сервер) | `_debugMode = true` ⚠️ по умолчанию вкл | `Core/ShipPosition/PlayerPositionServer.cs:30` |
| Позиция корабля (сервер) | `debugMode = false` | `Core/ShipPosition/ShipPositionServer.cs:47` |
| Облака плотность | `_verboseLogging` (дамп по T) | `World/Clouds/LocalDensityBuffer.cs:240` |
| Созвездия | `showDebugGizmos = true` ⚠️ по умолчанию вкл | `Core/DayNight/ConstellationController.cs:61` |
| Сутки | `showDebugOverlay = true` ⚠️ + `DayNightProfile.showDebugInfo = false` | `DayNightController.cs:42`, `DayNightProfile.cs:78` |
| Респавн игрока | `_debugLog` | `Player/PlayerRespawnTracker.cs:31` |
| Ребейз/сдвиг | События `runtimeRebase.*` через `GlobalMotionRuntimeEvidenceProbe` | `World/FloatingOrigin/Network/GlobalMotionControlledRebaseSlice.cs:113–699` |
| Респавн-мир | `RespawnManager` флаги (см. файл) | `World/RespawnManager.cs` |
| Массовое включение (хрупко, рефлексия) | `StreamingSetupRuntime` ставит `showDebugLogs/showDebugHUD = true` | `Core/StreamingSetupRuntime.cs:217–223` |
| Боевые (только Debug-билд) | `if (Debug.isDebugBuild)` | `Combat/Client/CombatClientState.cs:78–112`, `TargetLockService.cs:161,230`, `PlayerAttacker.cs:133`, `Core/PickupItem.cs:128`, `AI/NpcLootPickup.cs:232` |

## §6. Маршруты (для вкладки «Маршрут» — сейчас только Editor)

- Отрисовка: `PeacefulShip/Editor/NpcShipRouteDrawer.cs` (Editor-only, гизмо).
- Данные: `PeacefulShip/Core/NpcShipRoute.cs`, `NpcShipSchedule.cs`,
  редактор расписания `PeacefulShip/Editor/NpcShipScheduleEditor.cs`,
  контроллер `PeacefulShip/Editor/NpcShipControllerEditor.cs`.
- L1: рантайм-оверлей (`LineRenderer` по точкам `NpcShipRoute`) + список «корабль → статус»
  из `NpcShipWorld` / `NpcShipClientState`. Отдельного рантайм-API показа нет — писать новое.

## §7. Сейвы (для вкладки «Сейвы»)

Все методы — `UI/MainMenu/PersistenceDebugTools.cs:18–83` (static, возвращают строку-отчёт):
`DeleteAllSaves`, `DeleteCharacterPositionSaves`, `DeleteCharacterInventory`,
`DeleteCharacterProgression`, `DeleteCharacterCustomisation`, `DeleteQuestSaves`,
`DeleteSkillBindingSaves` (+ PlayerPrefs `ProjectC.InputBindings.v1`),
`DeleteKeyInstanceSaves` (`KeyRodInstances.json`), `DeleteWorldTimeSaves`, `DeleteTradeSaves`.
L1: кнопки 1-в-1 + поле вывода отчёта + confirm-диалог на `DeleteAllSaves`.

## §8. Файлы-кандидаты под L1 (создать, не править чужое)

| Новый файл | Назначение |
|---|---|
| `Scripts/Admin/AdminFacade.cs` | Фасад-обёртки над T1–T6, H1–H7, §4–§5, §7 |
| `Scripts/Admin/AdminRuntimeWindow.cs` | UI Toolkit окно (паттерн CharacterWindow), 6 вкладок |
| `Scripts/Admin/Editor/AdminEditorWindow.cs` | Editor-окно тех же кнопок |
| `Scripts/Admin/AdminConfig.cs` (L3) | SO хоткеев/пресетов (позже) |
| `Scripts/Admin/AdminCommandRegistry.cs` (L2) | Реестр команд консоли (позже) |

## §9. Что НЕ делать (hard rules, выжимка из AGENTS.md)

1. Не переносить `NetworkManager`, не добавлять второй; не трогать `ClientSceneLoader`
   (обычный `SceneManager`, не `NetworkSceneManager`); не удалять `ScenePlacedObjectSpawner`;
   не разворачивать `WorldSceneManager`/стриминг без запроса.
2. Не править `PlayerInputReader` (dead code) — только добавлять новые слоты в конец
   `InputBindingsConfig` (сериализация!).
3. Не писать `.meta`/`.asmdef` руками; `.asset` — только через Unity `CreateAssetMenu`.
4. Не хранить мировые `Vector3` между кадрами без `ApplyRebaseTranslation`-хука (🟡);
   не спавнить мировой контент в `BootstrapScene` без регистрации (🔴);
   сейвы — мировые координаты + кумулятив сдвига (паттерн `ShipPositionServer`).
5. Не запускать `run_tests`/Build через MCP; коммиты — только по явной просьбе, без push.
6. `docs/gdd/`, `WORLD_LORE_BOOK.md`, `Library/`, `Temp/`, `Builds/` — не трогать.
