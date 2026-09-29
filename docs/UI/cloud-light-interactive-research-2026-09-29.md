# Cloud UI — «облачный» лёгкий интерактивный UI: полный ресерч

> **Тип документа:** research-only, не план кодинга. Код не пишем, файлы проекта не меняем.
> **Дата:** 2026-09-29 · **Автор:** агентский ресерч (3 параллельных потока + сверка с проектом и доками Unity 6)
> **Статус:** готов к обсуждению, к коду не приступать без решения по §13–§14
> **Старый ресерч:** `docs/UI/full rebuild plans/00_immersive_menu_strategy.md` (2026-07-03, Mavis).
> Сравнение со старым — в §17 (заключение), как просили: старый смотрим только в конце.
> **Канон по UI Toolkit в проекте:** `docs/UI/UI_TOOLKIT_GUIDE.md` (single source of truth по 5 фатальным ошибкам).

---

## 0. Стек (pinned, сверено 2026-09-29)

| Component | Version | Источник |
|---|---|---|
| Unity Editor | **6000.5.2f1** | `ProjectSettings/ProjectVersion.txt` |
| URP | **17.5.0** | `Packages/manifest.json` |
| Input System | **1.19.0** | `Packages/manifest.json` |
| NGO | **2.13.0** | `Packages/manifest.json` |
| Localization | **1.5.12** | `Packages/manifest.json` |
| uGUI | **2.5.0** | `Packages/manifest.json` (остатки живут) |
| Build target | **только StandaloneWindows64** | AGENTS.md |

Вывод для ресерча: десктопный билд снимает мобильные ограничения (overdraw полупрозрачности терпим),
но оставляет все десктопные требования — кириллица + CJK, 1080p–1440p скейл, мышь+клавиатура, NGO-синхронизация.

---

## 1. Что у нас сейчас: «рабочая заглушка» (факты, не оценки)

### 1.1 Масштаб

- **~25 UI-поверхностей.** ~20 на UI Toolkit (`UIDocument` singleton + `UXML+USS` + `EnsureBuilt`), остатки на uGUI/TMP (`ConfirmationDialog`, `NetworkUI`, `SceneDebugHUD`, `ControlHintsUI`, `AltitudeUI`, `PeakNavigationUI`), дебаг-оверлеи на IMGUI (`OnGUI`).
- **20 `PanelSettings`-ассетов — по одному на окно.** Полный список: `Character, Chart, Crafting, Customisation, Gathering, Inventory, SkillTree, ShipKey(орфан), MetaRequirement` (в `UI/Resources/UI/`) + `MainMenu, EscMenu, Keybindings, SkillBinding, RebindPrompt` (в `Resources/UI/`) + `Market, ShipCargo` (Trade) + `Dialog, QuestTracker` (Quests) + `CommPanel` (Docking) + `ShipHud` (Ship). Все с `sortingOrder 0` в ассете — реальный z-порядок задаётся кодом (`sortingOrder 10/100`, uGUI `10000`, `HUD 100`, `UIFactory 5000`).
- **Канонический шаблон окна** (`UI_TOOLKIT_GUIDE.md` §3): `RequireComponent(UIDocument)` + `EnsureBuilt` через `Clear() + CloneTree()` (исключение — `CommPanelWindow`, единственный без `Clear`, будущий cloud-код обязан это знать) + `display Flex/None` + `pickingMode Position/Ignore` + свой `Esc` + регистрация в `UIManager.IsAnyExternalWindowOpen()` + `Loc.Get` + подписка на `*ClientState` с lazy-retry.
- **Дубликаты-ловушки:** `CharacterWindow.uxml/.uss` и `CraftingWindow.uxml/.uss` лежат в двух копиях; `ShipCargoConsoleWindow.uxml/.uss` — вне `Resources/` (fallback `Resources.Load` подозрителен); `ShipHud.uxml/ShipHudPanel.uxml` — орфаны (контроллер строит runtime без UXML); `ShipKeyPanelSettings.asset` — орфан без потребителя.

### 1.2 Что работает хорошо (фундамент не трогаем)

- `UIManager` — единственный Esc-роутер (`DefaultExecutionOrder(-200)`, цепочка `Keybindings -205 → UIManager -200 → EscMenu -150`, стек `OpenPanel/ClosePanel`, `IsGameplayInputBlocked()`).
- `HUDManager` — единый uGUI `ScreenSpaceOverlay Canvas sortOrder 100` для TMP-мелочей.
- `UITheme` (ScriptableObject, 51+ цветов) — но его видят только uGUI/`UIFactory`/`ConfirmationDialog`; USS его не видит (стили дублируются).
- `CustomDropdown` (VisualElement-based, попап на `panel.visualTree`) — полная стилизация USS вместо нестилизуемого `DropdownField`.
- Кастомные бары `bg+fill` (вместо жёсткого `ProgressBar`) с `transition width 0.3s`.
- Тонкие скролбары (6px, голубой dragger) в 13 USS — актуальные классы Unity 6 (`unity-scroller`, не `unity-scrollbar`).
- `Loc` + `UI_Table` (423 записи после T-UI10), 9 локалей `ru/zh/en/es/de/fr/pt/ja/hi`, контекстные хинты `E/F` (T-UI10, проверено пользователем в Play Mode).
- `ShipHudController` — образцовый паттерн (5-step guard, `pickingMode Ignore`, компас `MarkDirty` только при `Δ>0.5°`, чтение `WindManager/AltitudeCorridor` без RPC).
- `MyShipsTab.EqualsApprox` — образцовый клиентский throttle поверх серверных 5 Hz телеметрии.

### 1.3 Главные дыры именно под «облачный лёгкий интерактивный»

1. **Визуал — тёмный opaque.** База `rgba(20,25,35,0.95)` / `rgb(20,25,35)` везде. Это антипод «неба»: убивает living background, хотя мир за меню и не блокируется.
2. **Немота.** `Click/Open/Close/ErrorSound` объявлены на `UIManager`, но реальные вызовы `PlayClick` — только в `ConfirmationDialog` (2 шт). 20+ UITK-окон в стек не входят → полностью немые. Hover-звуков нет вообще.
3. **Нет motion-слоя.** USS `transition width` (1 случай) + `schedule.Execute(50ms)` repaint-костыль во всех `Show()` (не анимация). Ни DOTween, ни своего твинера, ни motion-токенов в `UITheme`.
4. **Perf-бомба: 20 панелей красятся всегда.** `docs/UI/PERF_UIElementsRepaintPanels_INVESTIGATION.md`: `PostLateUpdate.UIElementsRepaintPanels` ~3.1 MB/кадр (~390K Character + ~283K Market + ~2.4M остальные = ~186 MB/сек GC). `display:none` repaint не останавливает; `UIDocument.enabled=false` — **0 совпадений в коде**. Официальные доки Unity 6 прямо говорят: *«Multiple UIDocument components can point to the same PanelSettings object, which optimizes performance»* — мы делаем наоборот.
5. **Ввод фрагментирован.** `Tab` — свой `InputAction` мимо ребинда; `F` перегружен 5 путями без облачного приоритета; `I` свободна; `OpenSkillTree` без дефолтного ключа; `IsGameplayInputBlocked` читают только 2 места (атака).
6. **Тосты столкнутся.** `Gathering bottom=48` vs `QuestToast bottom=80` vs `Meta/Knowledge` без межтиповой очереди; `QuestTracker` — локальный `_trackedQuestId` (серверного трекинга нет).
7. **Ловушки копипасты** (§1.1 дубликаты/орфаны) — новое окно строго по образцу `CraftingWindow` (полный fallback) + `CommPanel sortingOrder=10`, иначе получим «thin-strip»/`inlineStyle`-мусор повторно.

---

## 2. Что значит «облачный, лёгкий, интерактивный» — декомпозиция требования

Чтобы не получить «over-designed», фиксируем измеримые критерии вместо прилагательных:

| Требование | Измеримый критерий | Слой (§3 старого ресерча) |
|---|---|---|
| **Облачный** | Светлые полупрозрачные карточки `0.72–0.85` + hairline-рамки + верхний блик; тёмный opaque только для тултипов/дебага; мир виден за UI | L1 Living Background + L3 Composition |
| **Лёгкий** | Не добавить ни одного `PanelSettings`; не вырастить `UIElementsRepaintPanels` сверх baseline 3.1 MB; анимации только `opacity/translate/scale`, 150–300ms | Perf + L2 Motion |
| **Интерактивный** | Любое изменение анимировано (fade+rise 8–12px, stagger 25ms, тикающие числа 300–500ms); hover 120ms + звук; hint/toast/progress отвечают <1 кадра на смену состояния | L2 Motion + L4 Audio |

Приоритет по отдаче (подтверждено старым ресерчом и референсами NMS/BotW/Destiny): **L1 (прозрачность+мир) + L4 (звук) + L2 (micro-motion) дают ~80% ощущения. L3 (типографика/сетка) и L5 (реакция на мир) — вторая очередь.**

---

## 3. Решение по стеку: UI Toolkit остаётся, uGUI — sunset, IMGUI — только дебаг

| Вариант | Вердикт | Обоснование |
|---|---|---|
| **UI Toolkit runtime — основной** | ✅ Да | 20 окон уже на нём, канон отлажен кровью (`UI_TOOLKIT_GUIDE.md`), USS-токены, `CustomDropdown`, thin scrollbars, `Panel Text Settings`/SDF/CJK из коробки Unity 6. Проблема не во фреймворке, а в том, как используем (тёмный opaque + 20 панелей + немота). |
| **uGUI — только legacy-поддержка** | ⚠️ Sunset | Живые потребители: `ConfirmationDialog` (единственный со звуками!), `ControlHintsUI`, `NetworkUI`, `SceneDebugHUD`, `AltitudeUI`. Стратегия: не переписывать сейчас; новый cloud-код на uGUI не пишем; `UIFactory`/`UITheme`-цвета маппим в USS-токены вручную при редизайне. |
| **IMGUI — только дебаг** | ⛔ Не для игры | `AdminRuntimeWindow` (F12), `Graphy`-подобные оверлеи, `FloatingOrigin/CloudPerfMonitor OnGUI`. В cloud-дизайн не входит. |
| **Новый фреймворк / «всё 3D» / UI Builder-зависимость** | ⛔ Нет | Старый ресерч прав: гибрид Overlay + редкие WorldSpace-маркеры. Полностью диегетический UI убивает читаемость таблиц (рынок, контракты, груз). |

---

## 4. Cloud-эстетика в UI Toolkit Unity 6: что реально можно, а что эмулируем

Сверено с официальными доками Unity 6 (6000.0–6000.7) 2026-09-29 + runtime-инспекцией проекта.

### 4.1 Жёсткие ограничения (не бороться, обходить)

- **`filter: blur()` блюрит сам элемент, не фон.** Пустой прозрачный слой с `blur` блюрит пустоту. Для живого 3D-мира за меню неприменим.
- **`backdrop-filter` — не ours.** В 6000.6/6000.7 доки появилась страница `backdrop-filter` с пометкой *«only renders on Screen Space Overlay, no effect on World Space»*; в тредах по 6.3 — «не поддерживается». На нашей **6000.5.2f1** полагаться нельзя: версионно хрупко + цена полноэкранного grab+blur каждый кадр. **Решение: считаем что настоящего backdrop-blur нет.**
- **`drop-shadow()` в USS-фильтрах не поддерживается** (явно в доке `USS filter`). Мягкую тень — вложенным слоем/9-slice, не CSS-тенью.
- **Gradient borders / `border-image` — нет.** Рамка только сплошная. «Градиентную» рамку эмулируем двумя вложенными слоями (внешний hairline + внутренний блик).
- **Попап `DropdownField`/`GenericDropdownMenu` не стилизуется локальным USS** (создаётся сиблингом `rootVisualElement`, дотягивается только через TSS панели). Поэтому **`CustomDropdown` оставляем**; редизайним только кнопку-триггер.
- **`ProgressBar` жёсткий** — оставляем структуру `bg+fill`, перекрашиваем только их.

### 4.2 Как эмулировать glassmorphism/cloud без blur (рекомендуемый путь)

1. Светлый полупрозрачный фон карточки вместо тёмного opaque.
2. Тонкая светлая рамка `1px` + внешняя hairline.
3. Верхний хайлайт: верхняя половина на 4–8% светлее (вложенный оверлей `white 0.10 → transparent`).
4. Плоская «воздушная» тень малой альфы только на модалках/карточках верхнего уровня, не на каждой строке списка.
5. Лёгкая светлая вуаль поверх мира `0.15–0.25` только на время модалки — читается как облако без единого blur-пасса.

### 4.3 Токены cloud-палитры (первая версия, правится визуально в UI Builder до кода)

```css
/* surfaces */
--surface-card: rgba(242, 247, 252, 0.82);
--surface-soft: rgba(232, 240, 248, 0.62);
--veil-world:   rgba(200, 220, 240, 0.18);   /* вуаль поверх живого мира */
--surface-tip:  rgba(20, 28, 40, 0.92);      /* тёмный только для тултипов/дебага */
/* ink (тёмный текст на светлых карточках — обязательно для контраста поверх неба) */
--ink-primary:   #1E2A38;
--ink-secondary: rgba(30, 42, 56, 0.66);
/* lines */
--line-hair:  rgba(255, 255, 255, 0.65);
--line-outer: rgba(130, 160, 190, 0.35);
/* accents (редко, цифры/статусы) */
--accent-sky: #5AB8E8;
--accent-sun: #FFD98A;
--danger:     #E86A6A;
```

Удалить как базовый фон `rgba(20,25,35,0.95)` / `rgb(20,25,35)`. Тёмный оставить только для тултипов/дебага.

### 4.4 Радиусы / спейсинг / типографика

- **Радиусы:** карточка `16–20px`, кнопки/чипы `pill 999px`, инпуты/дропдаун-триггеры `12px`, тултипы `10px`, прогресс `999px` (радиус = половина высоты). Один радиус на семью.
- **Спейсинг:** `4 / 8 / 12 / 16 / 24 / 32 / 48`; карточка `padding 20–24`, gap в списках `8–12`, межсекционный `24–32`. Плотные таблицы — только внутри данных, снаружи воздух.
- **Типографика:** заголовки Light `20–28`, body Regular `13–15`, межстрочный `1.4–1.6`, капсы `letter-spacing 0.08–0.12em`, одна акцентная цифра крупно. Пустота = лёгкость. Жирность — отдельным face, не фейк-болдом.
- **Бары:** трек светлый `rgba(255,255,255,0.35)`, fill sky/cyan (двухслойная имитация градиента: base + highlight-top), высота `6–10px` обычные / `12–16px` HP. Существующие `bg+fill` контролы (`SHIP_WINDOW.md` §4, `CHARACTER_WINDOW_STATS.md`) — только рескин, не рефактор.
- **Скролбары:** существующие 6px-тонкие оставить; при светлой теме проверить контраст dragger `rgba(100,160,220,0.4)` на светлом треке (вероятно затемнить до `0.55–0.7`).

---

## 5. Motion: USS + свой микро-твинер, без DOTween

| Средство | Роль | GC/цена |
|---|---|---|
| **USS transitions** (`transition-property/duration/timing-function`) | hover/press/focus-fade, 120–150ms | ~0 после установки |
| **Свой tiny-tweener (~300 LOC, struct+pool, 1 Updater)** | появление/исчезновение окон, слайды, stagger, animated numbers, callbacks/sequences | 0 при пулинге |
| **DOTween** | ⛔ не тащить | ~700B/tween, соблазн анимировать `width/margin` (= repaint-бомба), зависимость + IL2CPP-вес, требует approval на `manifest.json` |

**Motion-токены:** `fade 150ms ease-out`, `rise+fade 220ms easeOutCubic (8–12px)`, `scale-in 200ms (0.97→1)`, `stagger 25ms`, `numbers-tick 300–500ms`, `hover 120ms`.

**Железные правила (иначе вернём 3.1 MB/кадр):**
- Анимируем только `opacity/translate/scale/color` + `transform-origin` (дешёвые, `Fully animatable`). Порядок Unity: `Scale → Rotate → Translate`.
- НЕ анимируем `width/height/margin/padding/flex/position/overflow/display` — каждый кадр пересчёт layout всего поддерева.
- `DynamicTransform` usageHint на анимируемых листьях; не анимировать родителей.
- Скрытое — в `display:none` (не `opacity:0 + visible`, невидимое не красится).
- `schedule.Execute(50ms)` repaint-костыль оставить только в `Show()`/смене таба (как сейчас), никогда в `Update()`.
- `!important`-простыню не наращивать: токены в TSS/базовых USS, состояние — классами-модификаторами (`.is-open/.is-active`), inline-стили только из твинера на время анимации с очисткой.

---

## 6. Living background + URP: «окно в мир» бесплатно

- У UI Toolkit **нет `ScreenSpaceCamera`** как у uGUI. В `PanelSettings → Render Mode` только `Screen Space Overlay` (2D поверх всего) и `World Space` (объект в мире, окклюзия). **Для меню нужен только Overlay.**
- Рецепт L1: `PanelSettings.clearColor = transparent (alpha 0)` + прозрачный корень UXML + игра/камера не на паузе + `pickingMode Ignore` на фоне. Мир продолжает рендериться, UI поверх. **Никакого второго рендера, никакого RenderTexture — бесплатно архитектурно.**
- Затемнение мира — только лёгкая вуаль `0.15–0.25` на время модалки; контент — маленькие центрированные карточки, не полноэкранные `rgba(*,0.95)`.
- `World Space` для меню не использовать (окклюзия, коллайдеры, Update Mode, сортировка по камере).
- Связка с существующим: тумблеры графики (`DepthOfField`, `EdgeDetection`, `TemperatureFilter` из `08_graphics_effects_toggles_design.md`) должны жить рядом с cloud-настройкой «плотность облаков/отключить L1» — один паттерн через `SettingsManager` + `SettingsWidgets`.

---

## 7. Audio: разбудить существующие поля, новых AudioSource не плодить

- Клипы уже объявлены (`UIManager.Click/Open/Close/ErrorSound`) — наполнить ассетами и проверить вызовы через стек (`EscMenu 100 / Keybindings 200 / SkillBinding 150 / ConfirmationDialog 999` уже получают `Open/CloseSound` через `OpenPanel/ClosePanel`).
- Звуковая сетка минимум: `open / close / hover / select / error` — разные короткие soft sine/blip 0.05–0.15s, без «металла» (BotW-урок).
- Hover-звуки — только на кнопках верхнего уровня (не на каждой строке списка, иначе шум).
- Маппинг громкости — через существующие `AudioSettingsSection` (мастер/каналы UI/SFX/Ambient), новых микшеров не заводить без необходимости.
- Эмбиент (ветер/облака) за меню продолжается, микрофон-подобные захваты во время меню отсутствуют (у нас их нет — фиксируем как есть).

---

## 8. World response (L5): read-only подписки, не poll каждый кадр

- Подписки — не на `NetworkVariable` напрямую, а на `*ClientState.OnSnapshotUpdated` (Rpc → snapshot → event). Источники: `ShipTelemetryClientState` (серверный throttle 5 Hz, `TELEMETRY_UPDATE_INTERVAL=0.2`), `Inventory/Stats/Quest/Market/Contract/CraftingClientState`, прямые NV только `PlayerTarget/NpcTarget HP`, `NetworkChestContainer._isOpen`, `ParomRoute` точки.
- **Правила «тикающих» чисел** (иначе `60 string/sec` + layout/кадр поверх 3.1 MB):
  1. Писать только если видимо (`IsVisible()` + `_doc.enabled`, как `MarketWindowHost.IsVisible`).
  2. Dirty-check перед записью (`EqualsApprox` как в `MyShipsTab`, float — эпсилон, деньги/топливо — округление до отображаемой точности).
  3. Числа — накопление `0.2–0.5с` + кеш строк/`StringBuilder` для целых; бары — через `style.width %` без смены `text`.
  4. Тяжёлые списки (маркет/инвентарь/квесты) — никогда `Rebuild` на telemetry, только на свой `OnSnapshotUpdated` + проверка активного таба.
- Если позже понадобится `GameEventBus` (`DayChanged/WeatherChanged/CombatStarted`) — заводить отдельным тикетом; cloud-pilot обходится существующими `ClientState` + прямым чтением `WindManager/AltitudeCorridorSystem` (образец — `ShipHudController.Update`).

---

## 9. Input: убрать фрагментацию до cloud-слоя

Факты (`NetworkPlayer.IsActionJustPressed`, `InputBindingsConfig.GameAction`, T-UI10):

| Ключ | Сейчас | Для cloud |
|---|---|---|
| `E` | NPC-разговор + часть non-NPC flow | hint уже есть (`ui.interaction_hint.talk/use`); cloud-hint наследует тот же resolver-приоритет (F-candidate → NPC E) |
| `F` | Перегружен: pickup → gather → craft → cargo → door → ship | Не менять flow; cloud-прогресс (`GatheringToast`-тип) только наблюдает, не выбирает target |
| `Tab` | Своя `InputAction` в `InventoryUI`, мимо `InputBindingsConfig.OpenInventory` (не ребиндится) | Завести в ребинд до cloud-слоя, иначе cloud-колесо не переназначить |
| `I` | Свободна | Кандидат под cloud-панель/журнал (решение — отдельным тикетом, не в pilot) |
| `P/M/T` | `Character/Chart/CommPanel`, свои `Esc` | Cloud не добавляет своих Esc-handler'ов вообще |
| `Esc` | Цепочка `-205/-200/-150`, `UIManager` — единственный роутер | Новое UI Esc не обрабатывает; фон всегда `pickingMode Ignore` → `IsGameplayInputBlocked()==false` |
| `F1/F12` | hints / Admin | Не трогать |

---

## 10. Локализация и шрифты: CJK уже в таблице, атлас — главный риск

- `UI_Table` — 423 записи (T-UI10: `421 → 423`), 9 локалей. Полный `Bind` — только `MainMenu/Keybindings/SkillTree`; остальные — `Loc.Get` + ручной `HandleLocaleChanged` (утечки отписок у лямбд — чинить точечно, не миграцией).
- Правила добавления cloud-ключей (`ui.cloud.*`, `ui.interaction.*`): только аддитивно через официальный API/CSV, never `Rebuild Tables`, never ручной YAML (урок T-UI09 постмортема).
- **Шрифты (UITK Text = TextCore на TMP, отдельный TMP не нужен):** основной «воздушный» Light/Regular (латиница+кириллица, SDF, `Include Font Data = true`) → fallback-цепочка `Noto Sans (Latin/Greek/Cyrillic catch-all первым)` → `Noto Sans JP / SC` → `Noto Sans Devanagari (+Tamil/Thai/Khmer при наличии)` → `Liberation Sans`. Порядок load-bearing: catch-all первым, иначе кириллица отрисуется японским шрифтом; JP первым — китайский отрисуется японскими глифами (хан-унификация). Локальный fallback-список на Font Asset + глобальный в `Panel Text Settings`.
- В Editor всё «выглядит ок» за счёт OS-шрифтов; **проверять только на билде StandaloneWindows64** + прогрев атласа перед первым CJK-экраном (иначе первый кадр оплачивает все глифы сразу).
- `text-shadow` в USS есть; для «воздуха» — тёмная тень 10–15% на светлом тексте поверх мира, не обводка.

---

## 11. Perf-план: shared PanelSettings + kill-switch repaint

Официальная позиция Unity 6: *shared PanelSettings для нескольких UIDocument — оптимизация*. Наш план:

1. **Ноль новых `PanelSettings`.** Весь cloud-pilot — **один `[CloudUI]` UIDocument** (клон `ShipHudPanelSettings` как референс fallback-качества, `sortingOrder` ниже модалок, `themeUss` назначен — см. `UI_TOOLKIT_GUIDE.md` п.4) + внутри слои `Background/Hints/Toasts/Progress` как обычные `VisualElement`.
2. **Kill-switch:** скрытие = `_doc.enabled=false` (убивает `PrepareRepaint` целиком) + перед этим мягкий фейд + `display:none + pickingMode Ignore` (паттерн `CharacterWindow.cs:681`). Сейчас `enabled=false` не использует никто — pilot станет первым образцом.
3. **Миграцию старых 20 панелей на `enabled=false`** — отдельной задачей после A/B-профилирования гипотезы из `PERF_...md` (проверить в Profiler: отключить `UIDocument` у скрытых панелей → снять `UIElementsRepaintPanels`), не в scope cloud-pilot.
4. **Атлас:** один динамический атлас на shared панель; не дёргать `SetPixels`-текстурами в UI-цикле; иконки — атласировать (гайд Unity 6 `Optimizing performance`: смена текстур рвёт батчи).
5. **Бюджет pilot:** дельта `UIElementsRepaintPanels` vs baseline 3.1 MB ≈ 0 в простое; в анимации — только листья `opacity/translate/scale`.

---

## 12. Floating Origin: ScreenSpace не участник, исключения поименованы

- Все `UIDocument`-окна и uGUI `ScreenSpaceOverlay` — не под `WorldScene_*` корнями, в `GlobalMotionPilotSceneCatalog` UI-записей нет. Правила подсети A / гейта B (`09S_NEW_CONTENT_PIPELINE.md`) к ним неприменимы: **каталог не трогать, маркеры не ставить, digest не пересчитывать, хуки сдвига не нужны**.
- Исключения (world-anchored, им хуки нужны — но это вне cloud-pilot): `ChartWindow._dividerWorld/_freeX/_freeZ` (кэш мировых XZ; сам док пишет «хук не нужен, пересчёт каждый кадр» — при pilot не трогаем), `Billboard` (`LookAt`, кэшей нет), `ShipHudController._markManager` пеленги, любые `WorldSpace`-маркеры портов. Им — `ApplyRebaseTranslation(delta)` по `09S §3.1`, если pilot их коснётся (не должен).
- Ручная проверка pilot: F8 → объект на месте + 0 errors; F9 → возврат (как весь новый контент).

---

## 13. Архитектура встраивания: A2+A3 гибрид (не ломать 10+ окон)

Старый ресерч предложил **A2 (инкрементально) + A3 (слой поверх)** — подтверждаем, уточняем точками касания:

```
[CloudUI] (GameObject, DontDestroyOnLoad как QuestTracker)
├── UIDocument (visualTreeAsset=CloudUI.uxml, panelSettings=CloudSharedPanelSettings, sortingOrder=-10)
├── CloudAmbientController (L1 фон + L5 read-only подписки, Esc не обрабатывает)
└── MotionDirector (L2: Fade/Slide/Pulse только style.opacity/translate, без MarkDirtyRepaint/CloneTree/Load<StyleSheet>)
L3: существующие окна — без изменений (UIManager стек, display/picking как сейчас)
L4: только UIManager.PlaySound + AudioSettingsSection (новых AudioSource не плодить)
```

- `CloudAmbientController` **не регистрируется** в `UIManager._openPanels` → `Esc`, `CloseAllPanels`, `IsGameplayInputBlocked` его не видят.
- `UIFeel` — статик-утилиты (`Fade/Slide/Pulse`), запрет `Resources.Load<StyleSheet>` (мусор `inlineStyle`) и `Clear()+CloneTree()` (двойной подвес) — см. `UI_TOOLKIT_GUIDE.md` §2.
- Настройки cloud («плотность облаков / отключить L1») — новая секция в `GraphicsSettingsSection` через `SettingsWidgets.CreateToggle/CreateSlider` + `SettingsManager`-персист (паттерн `08_graphics_effects_toggles_design.md`); старые секции не трогаем.
- Стиль — 1 `.uss` с `!important` на каждый класс (требование гайда; образец тотального `!important` — `ShipCargoConsoleWindow.uss`; анти-пример — `display:none !important` на руте).
- Стек `UIManager.OpenPanel/ClosePanel` сигнатуры не меняем (защита от переписывания 10+ окон — старый ресерч §6 прав).

---

## 14. Пилотный вертикальный срез (первый инкремент, не весь редизайн)

Не «переписать все окна», а один срез на shared панели, который доказывает L1+L2+L4:

**Scope pilot:** контекстный hint (`E/F`, наследник T-UI10 resolver) + межтиповая очередь тостов (Gathering/Quest/Meta/Knowledge арбитр) + тонкий прогресс (gather/craft) — всё в `[CloudUI]`, светлые cloud-токены §4.3, motion-токены §5, звуки через `UIManager`.

**Почему это:** hint + toast + progress — самые видимые, самые дешёвые, поверх всего, не требуют рефактора окон. `ControlHintsUI` (legacy TMP) при pilot не переписываем — новый hint живёт в cloud-слое, старый остаётся до отдельного sunset-тикета.

**Вне scope pilot:** редизайн Character/Market/Crafting окон, `GameEventBus`, шрифт-замена, `enabled=false`-миграция 20 панелей, WorldSpace-маркеры, `I`-биндинг (отдельные тикеты).

**Acceptance pilot (без Play Mode со стороны агента):**
- Compile: Console → 0 errors.
- Profiler-дельта `UIElementsRepaintPanels` vs baseline ≈ 0 в простое (меряет пользователь).
- Hint: NPC → E-строка, F-объекты → F-строка, выход из радиуса/вход в корабль → скрыт; локаль переключается без рестарта.
- Тосты разных типов не перекрываются (единая очередь, `pickingMode Ignore`).
- F8/F9: 0 errors, возврат позиций.
- Play Mode + screenshots — **только пользователь, в доках NOT RUN до его прогона.**

---

## 15. Риски и что НЕ делать (защита от over-engineering)

| Риск | Митигация |
|---|---|
| `backdrop-filter`/blur не заведётся на 6000.5.2f1 | Не закладываться вообще; дизайн §4.2 работает без blur |
| Fullscreen-вуаль + изменение поддерева каждый кадр → repaint | Вуаль статична, анимируются только листья; скрытое — `display:none` + `enabled=false` |
| `!important`-простыня станет нечинимой | Токены в TSS, состояние — модификаторами, inline только из твинера с очисткой |
| `Resources.Load<StyleSheet>` вернёт `inlineStyle`-мусор | Грузить стили ссылками UXML/TSS, не `Load` в цикле |
| Per-row inline-стили в списках (память + инвалидация) | Классы `row-even/odd/selected`, не per-row `style.*` |
| Тёмный текст на светлом поверх яркого неба нечитаем | Тень 10–15%, вуаль при модалке, проверка на дневном/ночном цикле (`Volumes/DayNight`) |
| CJK-боксы в билде | Fallback-цепочка §10 + проверка только на билде + прогрев атласа |
| Соблазн «один motion на всё» / «всё 3D» / новый фреймворк | Разный motion на окно (Esc 0.15s fade, Character 0.3s slide+scale, Dialog typewriter 40 ch/s); таблицы остаются плоскими |
| Сломать Esc-цепочку `-205/-200/-150` | Cloud Esc не обрабатывает вообще |
| Потрогать `UIManager` API / `ClientSceneLoader` / `NetworkManager` / каталог FO | Запрещено (AGENTS.md + §12) |

---

## 16. Верификация без Play Mode (агент Play не запускает, скриншоты не делает)

- [ ] Console → 0 errors после создания скриптов (`create_script` сам триггерит компиляцию; `refresh_unity` не дёргать; ждать `is_compiling == false` в `mcpforunity://editor/state`).
- [ ] `Grep MarkDirtyRepaint|schedule.Execute` в новых файлах — только в `Show()`/по событию, нет в `Update()`.
- [ ] `Grep _doc.enabled|display|pickingMode` — у `[CloudUI]` есть `enabled=false` путь + `Ignore` у фона.
- [ ] UXML: все `name=` из `Q<>()` существуют; `UIDocument.visualTreeAsset` назначен (гайд п.1).
- [ ] USS: каждый класс из кода имеет `!important` (гайд п.5); нет `display:none !important` на руте; `Resources.Load<StyleSheet>` отсутствует (гайд п.3).
- [ ] `PanelSettings`: `themeUss` назначен (гайд п.4), `sortingOrder` ниже EscMenu/Keybindings, `scaleMode` как у `ShipHudPanelSettings`.
- [ ] NGO: подписки парные (`+=` в `OnEnable`, `-=` в `OnDisable/OnDestroy` как `MarketWindowHost`); новых `NetworkVariable` нет; `TELEMETRY_UPDATE_INTERVAL=0.2` не уменьшен.
- [ ] FO: `Grep WorldRoot|NetworkObject|GlobalSceneSourceMarker` в новых файлах — пусто; кэшей `Vector3` без `ApplyRebaseTranslation` нет.
- [ ] Ввод/Esc: `Grep escapeKey|OpenPanel|ClosePanel` в новых файлах — пусто; фон не ставит `IsGameplayInputBlocked=true`.
- [ ] Ручное (пользователь, NOT RUN агентом): F8/F9, Profiler `UIElementsRepaintPanels` vs baseline, Play-прогон hint/toast/progress + screenshots.

---

## 17. Заключение: сверка со старым ресерчом `00_immersive_menu_strategy.md` (как просили — в конце)

Старый документ (2026-07-03) — стратегический разбор «почему сейчас vista-style»: 11 приёмов immersive, 5 слоёв (L1 Living Background → L5 World Response), 3 стратегии миграции (A1/A2/A3), motion-библиотека (B1/B2/B3), тиры окон, pre-requisites, анти-over-engineering. Написан когда `UI_Table` ещё не имела hint-ключей, T-UI10 не существовал, perf-расследование 3.1 MB ещё не было оформлено, `08_graphics_effects_toggles_design.md` (тумблеры DoF/Edge/Temp) ещё не было.

### 17.1 Что общего (подтверждаем оба ресерча)

1. **Диагноз «vista-style» верен и воспроизводится.** Тёмные opaque панели, мгновенный show/hide, немота, «panel-header + content + actions» везде — всё на месте спустя ~3 месяца.
2. **5 слоёв L1–L5 — рабочая таксономия.** Новый ресерч её наследует (§2, §13) и маппит на конкретные файлы/строки вместо абстракций.
3. **A2+A3 гибрид.** Старый рекомендовал «сначала A3 (слой поверх за спринт), потом A2 (по одному окну)». Новый подтверждает и конкретизирует: `[CloudUI] + MotionDirector/UIFeel` без регистрации в стеке, без смены `UIManager` API.
4. **B1 для старта (USS transitions), B3 позже (свой твинер), B2 отложить.** Дословно совпадает (§5): DOTween не тащить (GC + зависимость + approval на manifest).
5. **«Не трогать `UIManager` API / не вводить новый фреймворк / не всё 3D / не блокировать мир / не один motion на всё».** Все пять запретов старого §6 — дословно в нашем §15.
6. **L4 аудио-пустота.** Старый: «ни одного вызова `PlaySound(ClickSound)`». Новый: спустя месяцы — те же 2 вызова, только в `ConfirmationDialog`. Звуковая сетка по-прежнему нулевая.
7. **L3 частично, L1/L2/L5 отсутствуют.** Оценка старого (§2.1) держится: L1 — только `InventoryUI` прозрачен; L2 — ноль; L5 — ноль (UI не подписан на `DayNight/Weather/PlayerState`).

### 17.2 Что нового (этого в старом нет — проект ушёл вперёд)

1. **Perf-данные: 3.1 MB/кадр.** Старого расследования не было; теперь есть `PERF_UIElementsRepaintPanels_INVESTIGATION.md` + подсчёт 20 `PanelSettings` + факт «`enabled=false` — 0 совпадений». Отсюда новое железное правило «ноль новых PanelSettings + kill-switch», которого в старом нет.
2. **Официальное подтверждение shared PanelSettings** (доки Unity 6: shared = оптимизация) — раньше была гипотеза, теперь doc-backed решение.
3. **`backdrop-filter` прояснён.** Старый про blur не писал; новый сверил доки 6000.0–6000.7: на нашей версии не полагаться, дизайн без blur (§4.1–§4.2).
4. **T-UI10 (хинты E/F + 2 ключа, 423 записи) и `hints/`-пайплайн.** Старого hint-контракта не было; теперь cloud-hint наследует готовый resolver-приоритет и localization-правила (аддитивность, no rebuild, no YAML).
5. **Тумблеры графики (`08_graphics_effects_toggles_design.md`).** Появился паттерн `SettingsManager + SettingsWidgets + Applier`, на который cloud-настройка («плотность облаков») ложится бесплатно.
6. **`CustomDropdown`, thin scrollbars, `bg+fill`-бары, `MyShipsTab.EqualsApprox`, `ShipHudController`-guard** — готовые образцы «как надо», которых в старом нет; новый ресерч их канонизирует (§1.2).
7. **Ловушки копипасты поименованы** (дубликаты UXML, орфаны PanelSettings/UXML, `CommPanel`-исключение, `RepairManager` без fallback) — в старом их нет, а cloud-код убьётся о первую же.
8. **CJK-цепочка и SDF-риски** — в старом только «шрифт как часть ассета»; теперь 9 локалей в таблице и конкретный fallback-порядок (§10).
9. **Пилот сужен до hint+toast+progress на одной shared панели** вместо «Tier 1: EscMenu/Character/Inventory редизайн». Старый Tier-план остаётся для второй очереди; pilot даёт L1+L2+L4 за минимальный diff.

### 17.3 Что в старом устарело / требует поправки

1. **«0 из 11 приёмов реализованы (частично №4 typewriter, №6 мир не блокируется)».** Поправка: `DialogWindow` typewriter жив и подтверждён; T-UI10 добавил контекстные хинты (приём «иерархия фокуса» частично); `QuestToast/GatheringToast` очереди существуют (примитивный L2). Счёт теперь «2–3 из 11 частично», не 0.
2. **«Проверить `grep PlaySound(ClickSound)`» — проверено:** 2 вызова, оба в `ConfirmationDialog`. Гипотеза закрыта.
3. **Доки-ссылки старого (§8 UI_TOOLKIT_GUIDE):** гайд с тех пор стал каноном (5 фатальных ошибок задокументированы кровью Character/CommPanel) — новый ресерч строится поверх него, а не параллельно.
4. **B1-оценка «хватит на 70%».** Уточнение: USS хватает на hover/fade; sequences/stagger/callbacks/animated numbers — только твинер (§5). Процент держать не будем, держим разделение ролей.
5. **D2 (`GameEventBus`) как pre-requisite.** Смягчаем: для pilot event bus не нужен (хватает `ClientState` + прямого чтения `Wind/Altitude`); bus — отдельный тикет второй очереди (§8).

### 17.4 Итог одной фразой

Старый ресерч правильно поставил диагноз и слои; новый подтверждает стратегию A2+A3 и запреты, но добавляет то, чего в июле не было: **perf-крышу (20 панелей / 3.1 MB / shared PanelSettings), blur-правду (без backdrop-blur), готовые образцы (CustomDropdown/bg+fill/EqualsApprox/ShipHud-guard), hint-контракт T-UI10, CJK-цепочку — и сужает первый шаг до pilot hint+toast+progress на одной shared панели вместо редизайна трёх витринных окон.**

---

## 18. История

| Дата | Событие |
|---|---|
| 2026-07-03 | Старый ресерч `full rebuild plans/00_immersive_menu_strategy.md` (Mavis): диагноз vista-style, L1–L5, A1/A2/A3, B1/B2/B3 |
| 2026-07-26 | `PERF_UIElementsRepaintPanels_INVESTIGATION.md`: 3.1 MB/кадр GC, гипотезы A/B/C (проверка — за пользователем) |
| 2026-08-07 | `MainMenu/01_implementation.md`: UITK MainMenu вместо UGUI NetworkTestCanvas |
| 2026-08-25 | T-UI10 (`hints/`): контекстные E/F-хинты, `UI_Table` 421→423, Play Mode подтверждён пользователем |
| 2026-09-16 | `esc-menu/08_graphics_effects_toggles_design.md`: DoF/Edge/Temp тумблеры, паттерн SettingsManager+Applier |
| 2026-09-29 | **Этот документ**: cloud-ресерч, токены §4.3/§5, архитектура §13, pilot §14, сверка со старым §17 |

*Проверки Play Mode / Profiler / screenshots в этом ресерче — NOT RUN (агент их не запускает; см. скиллы `dont-start-play`, `manual-playtest-only`).*
