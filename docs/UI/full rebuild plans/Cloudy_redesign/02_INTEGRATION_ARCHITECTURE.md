# 02 — Архитектура интеграции: окно рядом с легаси, без влияния на игру

> **Обновлено 2026-10-29:** документ писался как план. Сейчас код и префаб созданы и окно установлено в
> `BootstrapScene` — фактическое состояние, цифры и ограничения в **§9** (остальные разделы оставлены как проект
> архитектуры и ему соответствуют).

## 1. Что не трогается (красные линии)

| Система | Файл | Почему |
|---|---|---|
| Легаси-окно персонажа | `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (+ `CharacterWindow/MyShipsTab.cs`, `ContractsTab.cs`, `InventoryTab.cs`) | продолжает работать по **P** |
| Вызов по P | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (≈стр. 1074–1082: `IsActionJustPressed(GameAction.OpenCharacter)` → `CharacterWindow.Instance.Toggle()`) | без изменений |
| Бинды | `Assets/_Project/Scripts/Input/InputBindingsConfig.cs` (`GameAction`, `actions`) | новый `GameAction` не добавляется на этом этапе |
| Менеджер окон | `Assets/_Project/Scripts/UI/UIManager.cs` | API `OpenPanel/ClosePanel` используется как есть, файл не правится |
| Все `*ClientState`, `*Server`, DTO | см. `03_DATA_CONTRACT.md` | новое окно только читает и вызывает существующие публичные методы |

## 2. Где живёт новое окно

```
Assets/Aura/Mods/ProjectCCloudyNavigator/        ← рядом с ProjectCMainMenu, тот же принцип
  Scripts/
    CloudyNavigatorHost.cs          UIDocument + PanelSettings, жизненный цикл, видимость
    CloudyNavigatorHotkey.cs        тестовая клавиша (§3)
    CloudyNavigatorWindow*.cs       дерево (partial: Tree / Behaviour / Motion / Controls)
    Data/                           ПРОВАЙДЕРЫ: только чтение ClientState → view-модели (§4)
      CnQuestsProvider.cs  CnContractsProvider.cs  CnInventoryProvider.cs  CnShipsProvider.cs
      CnStatsProvider.cs   CnReputationProvider.cs CnCalendarProvider.cs   CnAltitudeProvider.cs
      CnReceiptsJournal.cs          вычисляемый журнал поступлений (нет в игре, только визуал)
    Actions/
      CnActions.cs                  единственная точка вызова существующих Request*-методов
    Preview/
      CnPreviewStage.cs             RenderTexture-сцена для фигуры и корабля (§5)
  UI/CN_Window.uss                  единственный источник вида, опора 1920×1080
  UI/CN_PanelSettings.asset
  Prefabs/CloudyNavigatorUI.prefab  ставит пользователь в BootstrapScene
```

- Сборка: как у меню — `Assembly-CSharp` (в `ProjectCMainMenu` нет `.asmdef`). Общие куски меню (`ProjectCMainMenuGamma`,
  `ProjectCMainMenuRamps`-подход, `ProjectCMainMenuSmokeTextures`, `ProjectCMainMenuSmokeField`,
  `ProjectCMainMenuTextBlower`, `ProjectCMainMenuFonts`) **переиспользуются вызовом**, а не копируются; менять их ради
  окна нельзя — при нехватке API делается свой класс в папке окна.
- Пространство имён: `ProjectCCloudyNavigator` (по образцу `ProjectCMainMenu`).
- Удаление папки и префаба полностью откатывает окно; ни один файл вне папки на него не ссылается.

## 3. Тестовая клавиша

- `CloudyNavigatorHotkey` читает `Keyboard.current` сам (тот же способ, что `AdminRuntimeWindow` для F12) и
  **не** проходит через `InputBindingsConfig` — чтобы не трогать общий конфиг и `NetworkPlayer`.
- Условия срабатывания — те же, что у P: `NetworkManager.Singleton != null`, локальный игрок заспавнен; не срабатывает,
  пока открыто другое окно с вводом текста.
- Занятые клавиши (найдено поиском): P, Tab, M, T, E, F, Q, WASD, Space, Shift, Enter, K, Esc, F3, F4, F12;
  F5–F10 — тестовые (`StreamingTest`, `StreamingTest_AutoRun`), **F8/F9** — `GlobalMotionControlledRebaseSlice` (floating origin).
- **Решение пользователя (2026-10-28): клавиша `[`** — рядом с P. В Input System это `Key.LeftBracket` (физическая
  позиция US-раскладки; на русской раскладке та же клавиша — «х»). Поиск по `Assets/_Project` совпадений
  `Key.LeftBracket` / `leftBracketKey` / `KeyCode.LeftBracket` не нашёл — клавиша свободна. Поле в инспекторе `toggleKey`.
- **Когда новое окно отлажено — P отключается** (отдельная задача, по команде пользователя). Тогда клавиша переезжает в
  `InputBindingsConfig`: либо `GameAction.OpenCharacter` перевешивается на новое окно, либо добавляется новый
  `GameAction` **в конец enum** (правило файла: «новые действия добавлять ТОЛЬКО в конец, не ломать сериализацию»).
  До этого момента `NetworkPlayer` и `InputBindingsConfig` не трогаются.

## 4. Данные: провайдеры только на чтение

- Каждый провайдер подписывается на событие своего ClientState при показе окна и отписывается при скрытии
  (ленивая подписка, как в `CharacterWindow.TrySubscribe*`: синглтоны появляются позже окна).
- Провайдер отдаёт неизменяемую view-модель с полями, названными как в DTO игры. Окно ничего не кэширует сверх этого.
- Опрос только там, где в игре нет события (HP из `PlayerTarget`, время из `ServerWeatherController`, высота) —
  с тем же шагом, что у легаси (`TIME_INFO_REFRESH_INTERVAL = 2f`) или по `OnCalendarChanged`.
- Полная карта полей — `03_DATA_CONTRACT.md`.

## 5. Превью 3D (персонаж и корабль)

В игре нет компонента превью (поиск `new RenderTexture`/`targetTexture` по `_Project` — только облака и диагностика).

- `CnPreviewStage`: своя камера с `targetTexture`, отдельный слой (как `PCMM_Atmosphere` у меню), изображение — в
  `VisualElement.style.backgroundImage` через `Background.FromRenderTexture`.
- В сцену превью попадают **только визуальные копии**: `MeshFilter/MeshRenderer`/`SkinnedMeshRenderer` с материалами,
  без `NetworkObject`, `ShipController`, коллайдеров и любых MonoBehaviour. Префабы кораблей
  (`Assets/_Project/Prefabs/Ships/*.prefab`) напрямую не инстанцируются — у них сетевые компоненты.
- Камера выключена, пока окно скрыто. Floating origin превью не касается: сцена превью стоит в собственной точке и не
  регистрируется в системах ребейза (проверить при реализации).

## 6. Ввод, Esc, курсор, блокировка геймплея

- `UIManager.IsGameplayInputBlocked()` знает только жёстко перечисленные окна и стек `_openPanels`. Чтобы атаки и
  клавиши не проходили сквозь новое окно **без правки UIManager**, окно при показе вызывает
  `UIManager.Instance.OpenPanel("CloudyNavigator", priority, onClose: Hide, gameObject)`, при скрытии — `ClosePanel`.
  Это даёт: блокировку ввода, разблокировку курсора, Esc → `CloseTopPanel` → наш `Hide`.
- Внутренний порядок Esc (попап → карточка → окно) обрабатывается окном до UIManager только для собственных попапов;
  проверить порядок `Update` (`UIManager` — `DefaultExecutionOrder(-200)`).
- **Одновременно с легаси окно не открывается (решение пользователя).** Без правки легаси:
  при показе нового окна — `if (CharacterWindow.Instance?.IsVisible() == true) CharacterWindow.Instance.Hide();`;
  пока новое окно открыто, оно в `Update` проверяет `CharacterWindow.Instance.IsVisible()` и, если по P открылось легаси,
  закрывается само (`Hide` + `ClosePanel`). Используются только публичные `IsVisible()` / `Hide()`.
- `ClosePanel` при пустом стеке запирает курсор (`CursorLockMode.Locked`) — то же поведение, что у других стековых окон.

## 7. Действия (только существующие запросы, только в легаси-контекстах)

| Действие в окне | Вызов | Контекст |
|---|---|---|
| Следить / снять | `QuestTracker.GetOrFindInstance().Toggle(questId)` | как в легаси; серверный `RequestTrackQuestRpc` окно не вызывает |
| Принять квест | `QuestClientState.Instance.RequestAcceptQuest(questId, fromNpcId)` | как кнопка легаси |
| Отказаться | сообщение `Loc.Get("ui.quest.reject_unavailable")` | сервера нет — как в легаси |
| Сдать квест | **не вызывается**; бирка «сдать у {NPC}» | сдача идёт через диалог |
| Контракт: взять/сдать/провалить/получить груз | `ContractClientState.Instance.RequestAccept / RequestComplete / RequestFail / RequestReceiveCargo` | как `ContractsTab` |
| Перенос багаж ↔ трюм | `ShipCargoServer.Instance.RequestStoreToCargoRpc / RequestRetrieveFromCargoRpc` с кратным количеством | **только в триггер-зоне обменника** (решение пользователя): зона рынка — `MarketZoneRegistry.LocalPlayerZone != null`; консоль трюма — `InteractableManager.FindNearestShipCargoConsole(позиция игрока, радиус)` (консоль регистрируется в `OnTriggerEnter`, радиус `_interactionRadius = 3`). Вне зоны кнопки неактивны с причиной **«подойди к обменнику»**; в зоне — подпись «рынок: {MarketZone.DisplayName}» / «консоль трюма: {корабль}» |

## 8. Телеметрия и логи

Отдельной UI-телеметрии в проекте не найдено. Окно пишет только `Debug.Log` с префиксом `[CN]` за флагом
`_debugLogging` (как у `CharacterWindow`), чтобы логи Play Mode можно было сверить с легаси.

## 9. Фактическая установка (2026-10-29)

- **Префаб:** `Assets/Aura/Mods/ProjectCCloudyNavigator/Prefabs/CloudyNavigatorUI.prefab` — один корень без детей,
  `active = true`: `Transform` + `UIDocument` (`panelSettings = CN_PanelSettings`, `sortingOrder = 200` — шаг 9a) +
  `CloudyNavigatorHost` (`document` → свой `UIDocument`, `styleSheet` → `CN_Window.uss`, `debugLogging = false`) +
  `CloudyNavigatorHotkey` (`toggleKey = Key.LeftBracket`, значение 11). Зависимости — только 4 файла внутри папки
  порта, внешних ссылок нет (`get_asset_meta`).
- **В сцене:** `Assets/_Project/Scenes/BootstrapScene.unity`, корневой объект `CloudyNavigatorUI` (68-й, последний
  sibling). `BootstrapScene` — build index 0, грузится первой в Single, `WorldScene_0_0` добавляется аддитивно
  (`ClientSceneLoader.cs:956`, `GlobalMotionPilotRuntime.cs:65`), поэтому глобальный `UIDocument` живёт именно здесь.
  Отдельного контейнера для глобального UI в сцене нет: каждый UI-документ — свой корень.
- **`sortingOrder = 200`** (шаг 9a; до него было `20`). Сравнивать надо не с `UIDocument`, а с uGUI-канвасами
  `BootstrapScene`: `HUD_Canvas` (`ControlHintsUI`) — ScreenSpaceOverlay с `sortingOrder = 100`, и при `20` окно
  гарантированно рисовалось под подсказками управления. 200 — выше HUD (`100`) и легаси-окон, ниже
  `Customisation` (`300`) и модальных подтверждений (`900`); всего в проекте 24 `PanelSettings` (`ShipHud 50`,
  `Chart 100`, остальные `0`). **`ConstantPixelSize`, `scale = 1`** — с `ScaleWithScreenSize` 1920×1080 панель
  получала `scaledPixelsPerPoint 0.7776` при окне редактора 1493×847, то есть 1 px USS не был пикселем экрана.
- **Ввод подтверждён по коду:** `ProjectSettings.activeInputHandler = 1` (только новый Input System). `P` идёт
  `Resources/InputBindingsConfig.asset` (`GameAction.OpenCharacter`, `Key.P`) → `InputBindingsRuntime.Instance.Config`
  → `NetworkPlayer.Update` (`:1074-1082`) → `Keyboard.current[...].wasPressedThisFrame` (`:2987`) →
  `CharacterWindow.Instance.Toggle()`. `[` читает `CloudyNavigatorHotkey` (`:24`, `:37-40`) прямо из `Keyboard.current`,
  мимо конфига. В `InputBindingsConfig.asset` нет клавиши `11` (`Key.LeftBracket`) — 24 бинда проверены, конфликта нет;
  `InputBindingsConfig` и `NetworkPlayer` не менялись (`git status` — чисто вне папки порта и сцены).
- **Условия открытия те же, что у `P`:** живая сессия и заспавненный локальный игрок (`CloudyNavigatorHotkey.CanToggle`),
  плюс фокус не в поле ввода. Открытие нового и легаси взаимоисключающее (`Show` прячет легаси, `Update` прячет себя,
  если легаси открыто по `P`).
- **В git установка не попала:** `.gitignore:153` исключает `*.prefab`, `.gitignore:182` — `*.meta`. Сцена
  (`BootstrapScene`) изменена в рабочем дереве на 58 строк (одна `PrefabInstance`). Если сцену закоммитить без префаба
  и его `.meta`, ссылка разорвётся на клоне — тогда нужен `git add -f` для префаба, его `.meta` и мета скриптов порта.
- **Не проверено:** поведение в Play Mode (Esc, блокировка ввода, курсор, легаси `P`), движение и превью в живом окне,
  альтиметр с реальным коридором (D16), floating origin, измерения пула (D20). Это — приёмка пользователя.
