# L1 Admin-панель — детальный дизайн-док

> Дата: 2026-09-15. Автор: Mavis. Статус: дизайн принят, код не начат.
> Основание: `00_ADMIN_PANEL_SURVEY.md §7` — все 6 вопросов отвечены пользователем
> (ответы зафиксированы там же, ниже — выжимка решений D1–D6).
> Инвентарь точек врезки: `01_TOGGLE_INVENTORY.md`.

## 0. Решения пользователя (D1–D6) + 1 reversal

| # | Решение | Эффект на дизайн |
|---|---|---|
| D1 | `ShipDebugHUD` — **удалить** (нерабочий legacy, S-HUD-05 уже объявлял его заменённым `ShipHudController`); PerfHUD — в админ-панель без хоткея | T-ADM-00: удаление файла + `InitializeDebugHUD`-ветки в `ShipController.cs:2180–2200`. F3 освобождается, никому не отдаём (резерв). **Reversal**: рекомендовал оставить Ship на F3 — пользователь поправил: код уже считает его мёртвым (комментарий S-HUD-05 + флаг `_showLegacyMeziyHud`) |
| D2 | Старые хоткеи стриминга (F5–F10 в `StreamingTest`) — **снять с клавиш**, функции увести в панель кнопками | T-ADM-06: флаг-гейт + публичные методы уже есть (`TeleportToCoordinates`, `TeleportToPeak`, `LoadChunksAroundPlayer`) |
| D3 | God + noclip + speed — **делать в L1** | T-ADM-02/05: единственный кусок новой логики; серверная часть god — отдельным подпунктом с investigation |
| D4 | Мастер-mute логов — **делать в L1**, поля не удалять | T-ADM-03: `AdminLogBus` + прямое выставление ~8 известных флагов |
| D5 | Объём — **максимально полный**, всё найденное | 8 вкладок (§3), включая Маршрут и Респавн |
| D6 | Хоткей панели — **F12**; backquote резервируем под консоль L2 | T-ADM-01: новый слот в конец `GameAction` |

---

## 1. Архитектура

```
┌─────────────────────────────────────────────────┐
│ AdminRuntimeWindow (UI Toolkit, F12)            │  новый файл, вкладки §3
│  читает F12 через NetworkPlayer-совместимый     │
│  хелпер (IsActionJustPressed pattern)           │
└──────────────┬──────────────────────────────────┘
               │ только вызовы public методов
┌──────────────▼──────────────────────────────────┐
│ AdminFacade (Singleton MonoBehaviour)           │  новый файл, §2
│  Полёт/Телепорт/Респавн/HUD/Мир/Маршрут/Логи/Сейвы│
└──┬───┬───┬────┬───┬───┬─────┬──────┬────────────┘
   │   │   │    │   │   │     │      │
   │   │   │    │   │   │     │      └─ PersistenceDebugTools.Delete* (готово, static)
   │   │   │    │   │   │     └─ AdminLogBus: 8 флагов напрямую (T-ADM-03)
   │   │   │    │   │   └─ RouteOverlay (LineRenderer по NpcShipRoute) — новое, маленькое
   │   │   │    │   └─ ServerStormManager / StormCellDirector / WindManager / DayNightController / WorldStreamingManager (всё public, §4)
   │   │   │    └─ PerfHUD.Enable / SceneDebugHUD.SetVisible (новые однострочные методы в своих файлах)
   │   │   └─ RespawnManager.GetEffectivePosition + PlayerRespawnTracker (T5), PlayerPositionServer.AdminTeleportPlayer (новый public, T-ADM-05)
   │   └─ WorldCamera.ToggleFlyMode/TeleportToPeak (готово, public) + AdminMoveCheats (god/noclip/speed, новое)
   └─ (редактор) AdminEditorWindow — те же кнопки фасада в Edit Mode (T-ADM-07)
```

**Принципы (из AGENTS.md, обязательны для каждого тикета):**

1. Additive-only: новые файлы в `Scripts/Admin/`; в чужих файлах — только добавление
   `public` методов/свойств в конец класса, никакого изменения существующей логики.
2. `BootstrapScene` не трогаем: фасад — `FindAnyObjectByType` + `DontDestroyOnLoad`
   на своём объекте, создаётся лениво (`AdminFacade.EnsureExists()` по образцу
   `HUDManager.EnsureExists()`, `UI/HUDManager.cs:312`).
3. Floating Origin 🟡: фасад **не хранит** мировые `Vector3` между кадрами.
   Координаты из полей ввода читаются в момент нажатия кнопки. Исключение —
   `RouteOverlay`: точки маршрута читаются каждый кадр из `NpcShipRoute`
   (scene-объект, едет с корнем бесплатно 🟢), своего кэша нет.
4. NGO: клиентские кнопки вызывают серверные методы **только** через новые
   `[Rpc(SendTo.Server)]` на самом `AdminFacade` (он `NetworkBehaviour`)
   с серверной проверкой `IsServer`; прямых вызовов чужих `[Rpc]` нет.
   До L4 (ролей админа) серверные команды работают только в host/single (см. §6).
5. Неймспейс `ProjectC.Admin`, `[SerializeField] private` + `_camelCase`, `[Header]`,
   комментарии на русском, тикеты `T-ADM-NN` в шапках файлов.

---

## 2. Новые файлы

| Файл | Класс | Ответственность |
|---|---|---|
| `Scripts/Admin/AdminFacade.cs` | `AdminFacade : NetworkBehaviour` (синглтон, `EnsureExists`, `DontDestroyOnLoad`) | Все методы-обёртки §3–§4; поиск таргетов через `FindAnyObjectByType`/ `Instance`; отсутствие таргета = `Warning`, не исключение |
| `Scripts/Admin/AdminMoveCheats.cs` | `AdminMoveCheats : NetworkBehaviour` (на игроке, добавляется фасадом по кнопке) | God (флаг урона, клиент+сервер, T-ADM-05), noclip (отключение коллайдеров игрока + `CharacterController.enabled=false` на время), speed multiplier (×1/×2/×5/×10) |
| `Scripts/Admin/AdminLogBus.cs` | `static AdminLogBus` | `public static bool Muted`; `Register(Action<bool>)/Unregister`; фасад дёргает `SetMuted(bool)` |
| `Scripts/Admin/AdminRuntimeWindow.cs` | `AdminRuntimeWindow : MonoBehaviour` (UI Toolkit) | Окно, 8 вкладок, паттерн `CharacterWindow` (Clear+CloneTree+Add, Resources.Load fallback); чтение F12 — новый слот `GameAction.AdminPanel` через копию `IsActionJustPressed` (код `NetworkPlayer.cs:2683–2698` как образец, дублируем 20 строк хелпера у себя, `NetworkPlayer` не правим) |
| `Scripts/Admin/RouteOverlay.cs` | `RouteOverlay : MonoBehaviour` | `LineRenderer` по точкам активного `NpcShipRoute`; вкл/выкл из фасада; точек не кэширует (читает каждый кадр — FO-безопасно) |
| `Scripts/Admin/Editor/AdminEditorWindow.cs` | `AdminEditorWindow : EditorWindow` (`Tools/Project C/Admin`) | Те же кнопки фасада для Edit Mode: регены облаков, `Setup Altitude Corridors`, вайп сейвов, список ContextMenu §4 как кнопки |

Минимальные правки чужих файлов (каждая — отдельным подпунктом тикета, ревью по диффу):

| Файл | Правка |
|---|---|
| `Input/InputBindingsConfig.cs` | В конец `enum GameAction` (после `CameraZoom`! сериализация): `AdminPanel // F12`; в конец дефолтов: `{ AdminPanel, Debug, F12 }` |
| `Core/ProjectCPerfHUD.cs` | `#if FALSE` → `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR` (как велит шапка файла); + `public void SetVisible(bool)` |
| `UI/SceneDebugHUD.cs` | + `public void SetVisible(bool)` (прячет `_panel`), + `public static SceneDebugHUD EnsureExists()` |
| `World/Streaming/StreamingTest.cs` | `[SerializeField] private bool handleHotkeys = false;` + `if (!handleHotkeys) return;` в начале `HandleKeyboardInput()`; методы телепортов уже public |
| `Core/ShipPosition/PlayerPositionServer.cs` | + `public bool AdminTeleportPlayer(ulong clientId, Vector3 pos)` (сервер-гард `IsServerSafe()`, делегирует приватному `TeleportPlayer`) |
| `Player/ShipController.cs` | Удалить `InitializeDebugHUD()` + поле `_showLegacyMeziyHud` (T-ADM-00; вместе с файлами ниже) |
| Удалить (через Unity Editor, не `rm`): `Ship/ShipDebugHUD.cs`, `Ship/MeziyStatusHUD_Legacy.cs` (+ `.meta` уйдут сами) | Основание D1 + S-HUD-05; `ShipHudController` — замена. Перед удалением: `grep` по сценам/префабам на висячие ссылки (сейчас ссылки только в 4 `.cs`, сцен/префабов нет — проверено 2026-09-15) |
| `AI/NpcSpawner.cs`, `PlayerPositionServer.cs`, `ShipPositionServer.cs`, `DayNightController.cs`, `ConstellationController.cs`, `PlayerRespawnTracker.cs`, `World/Clouds/LocalDensityBuffer.cs` | + однострочная подписка на `AdminLogBus` **или** + `public void SetDebugLogs(bool)` сеттеры (на выбор исполнителя, по 1 строке на файл; сами поля и `if` не трогаем) |

---

## 3. Вкладки окна (8, по D5 — максимально полно)

Каждая строка = кнопка/поле → метод фасада → существующий API.

### В1 «Полёт» (WorldCamera + читы)

| Элемент | Вызывает |
|---|---|
| Toggle fly (статус: вкл/выкл) | `WorldCamera.ToggleFlyMode()` (`Core/WorldCamera.cs:520`) |
| Next / Prev / Random peak, Return to height | `TeleportToNextPeak/Previous/Random`, возврат (`WorldCamera.cs:588–624,146`) |
| God on/off (статус) | `AdminMoveCheats.SetGod(bool)` → T-ADM-05 |
| Noclip on/off (статус) | `AdminMoveCheats.SetNoclip(bool)` (коллайдеры + CharacterController) |
| Speed ×1/×2/×5/×10 | `AdminMoveCheats.SetSpeedMult(float)` |

### В2 «Телепорт»

| Элемент | Вызывает |
|---|---|
| X/Y/Z поля + «Телепорт меня» | `PlayerPositionServer.AdminTeleportPlayer(ownClientId, pos)` (T-ADM-05; до сервера — через `StreamingTest.TeleportToCoordinates` как клиентский fallback, пометить в UI) |
| Список пиков (индекс + имя) + Go | `WorldCamera.TeleportToPeak(i)` |
| Тест-позиции стриминга (список + Go) | `StreamingTest.TeleportToPeak/TeleportToCoordinates` (бывшие F5/F6 по D2) |
| Координаты текущего фокуса (read-only строка) | Grove `playerRoot.position` — только чтение, не храним (FO 🟡) |

### В3 «Респавн»

| Элемент | Вызывает |
|---|---|
| Список точек `RespawnManager` (Count + `GetEffectivePosition(i)`) + «Респавн сюда» | `RespawnManager.GetEffectivePosition`, `GetPoint` (`World/RespawnManager.cs:36–86`) |
| Fallback-позиция (read-only) | `GetFallbackPosition()` |
| Кнопка «Убить и респавн» (debug) | `PlayerRespawnTracker` через существующий `PerformRespawn`-путь |

### В4 «HUD» (реестр H1–H7 из `01_§2`, минус удалённый ShipDebugHUD по D1)

PerfHUD (вкл/выкл), SceneDebugHUD (вкл/выкл — новый `SetVisible`), DayNight overlay
(одна кнопка ставит `showDebugOverlay` + `profile.showDebugInfo`), NGO summary
(текст `NgoMetricsCollector.GetSummary()`, обновление 1/с), Streaming HUD
(напрямую `WorldStreamingManager` вместо F10-рефлексии — заменить рефлексию
публичным свойством в том же тикете), Meziy legacy — **нет** (удалён по D1).

### В5 «Мир» (всё public, кноп
...[truncated 6154 chars]