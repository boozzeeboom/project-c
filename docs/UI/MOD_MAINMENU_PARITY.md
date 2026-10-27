# Расхождения: меню мода `projectc-main-menu` ↔ наше главное меню

Дата: 2026-10-27
Источник мода: `.Aura/Mods/projectc-main-menu/` (`index.html`, `src/ui.js`, `src/ui.css`)
Источник проекта: `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs`,
`Assets/_Project/Resources/UI/MainMenuWindow.uxml`, `MainMenuStyles.uss`,
`Assets/_Project/Resources/UI/MainMenuPanelSettings.asset`

Цель: меню мода должно нести **всю нашу логику**, все наши надписи и все наши блоки.
Ничего из работающего меню не удаляем и не ломаем.

## 1. Таблица соответствия

> Столбец «Решение» фиксирует решения, принятые **на стороне мода** (что и куда переехало
> в `index.html` / `ui.css`). Все шесть блоков из строк «нет» уже существуют в Unity —
> см. §7.

| Наш элемент (MainMenuWindow) | В моде | Решение |
|---|---|---|
| Заголовок «PROJECT C: THE CLOUDS» | «PROJECT**C**» + «Сектор меззий · терминал доступа» | в моде: заголовок = наше название, eyebrow оставляем как подпись |
| Подзаголовок «Версия Alpha 0.1» (`versionText: 0.1.94`) | «Месзий поднимается сквозь чёрную завесу…» | в моде: подзаголовок = «Версия Alpha 0.1.94», слоган атмосферы уходит в footer |
| `main-host-btn` «ОДИНОЧНАЯ ИГРА» | «Продолжить» (3000 м) | заменить подпись/действие на `host`, высота 3000 м |
| `main-connect-btn` «ПОДКЛЮЧИТЬСЯ К СЕРВЕРУ» | «Новая игра» (2200 м) | заменить на `connect`, высота 2200 м |
| `main-settings-btn` «НАСТРОЙКИ» | «Настройки» (1200 м) | совпадает, действие `settings` |
| `main-quit-btn` «ВЫХОД» | «Выход» (800 м) | совпадает, действие `quit` |
| `main-ip-panel` (заголовок, «Введите IP-адрес сервера:», поле `127.0.0.1`, «ПОДКЛЮЧИТЬСЯ»/«НАЗАД») | **нет** | добавить в мод отдельным экраном в той же дымовой манере |
| `main-debug-panel` «[Debug]»: 10 админ-кнопок сброса (All / Position / Inventory / Progression / Customisation / Quests / Skills / Keys / Time / Trade) + строка статуса | **нет** | добавить блок «[Debug]» + 10 кнопок + статус |
| `main-changelog-panel` «ИЗМЕНЕНИЯ»: заголовок, кнопка «↻», статус, скролл markdown | **нет** | добавить окно changelog (raw GitHub `docs/changelogs.md`), разворачиваемое/сворачиваемое |
| `main-links`: THEGRAVITY.RU, PROJECT C, GITHUB, TELEGRAM, VK | **нет** | добавить ряд соцкнопок |
| `main-lang-selector`: 9 локалей (Русский, 中文, English, Español, Deutsch, Français, Português, 日本語, हिन्दी) | **нет** | добавить дропдаун языка (те же 9 подписей) |
| Дисклеймер первого запуска («ДИСКЛЕЙМЕР» + текст + OK, флаг в `PlayerPrefs`) | **нет** | добавить оверлей, флаг в `localStorage` |
| Настройки: `GraphicsSettingsSection` / `AudioSettingsSection` / `GameplaySettingsSection` (качество, разрешение, полный экран, VSync, AA; громкость; геймплей) | 4 вымышленные вкладки «Игра/Видео/Звук/Управление» | вкладки переименовать в «Графика/Звук/Геймплей», строки вымышленные заменить на пункты из наших секций |

## 2. Куда уходит «наполнение» мода

| Слой мода | Что это | Куда в Unity |
|---|---|---|
| `SKY_FRAG` | вертикальный градиент неба + fbm-дыхание + `uDim` | `PCMM_Sky.shader` (URP, unlit, quad) |
| `GAS_FRAG` | меззий: дом. варп, перо, rim | `PCMM_Gas.shader` |
| `STAR_FRAG` | звёзды 40 px-ячейки + мерцание | `PCMM_Stars.shader` (additive) |
| `VEIL_FRAG` + `createCloudTexture` | 4 слоя завесы, тайловая маска | `PCMM_Veil.shader` + маски, генерируемые в `Texture2D` |
| `ROPE_VERT/FRAG`, `HULL`, `GLOW` | 3 троса-ленты + паром | `PCMM_Rope/Hull/Glow.shader`, меш пересобирается в C# |
| `wind.js` (Points, `gl_PointSize`) | пыль ветра | `ParticleSystem` (правило points → ParticleSystem) |
| `daynight.js` | День/Сумерки/Ночь/Рассвет | `ProjectCMainMenuDayNight.cs` |
| `smoke.js` / `ui.css` | дымовая сборка элементов | остаётся в моде; в Unity UI-скин не переносится (наш UI Toolkit живой) |

## 3. Что остаётся нашим навсегда

`SettingsManager` и секции настроек, `LocaleSelector`/`Loc`/локализация, `NetworkManagerController`
(host/connect), `PersistenceDebugTools` (админ-сбросы), changelog с GitHub, дисклеймер,
`EscMenuWindow` (владеет `Esc`), `UIDocument` + `PanelSettings` (Screen Space Overlay).

## 4. Решения, принятые пользователем

- Меню мода должно заменить наше визуально, но не ломать наши окна/настройки/кнопки → сначала
  дорабатываем меню **в моде** до полного паритета, потом переносим «наполнение» в Unity.
- Размещение в Unity: префаб `ProjectCMainMenu` + экземпляр в `BootstrapScene`.

## 5. Статус

Паритет **в моде** закрыт; в Unity идёт порт меню мода 1в1, шаги **1–8 из 9**.
План и журнал порта — `docs/UI/MainMenu/02_smoke_menu_port.md`.

- [x] паритет по подписям и блокам в моде
- [ ] проверка меню мода пользователем
- [x] перенос «наполнения» в Unity (шейдеры, атмосфера, тросы, сутки, пыль)
- [x] порт самого меню в Unity, шаги 1–8: дымовые листы, USS, дерево, поле дыма,
  выдувание букв, алтиметр, поведение, наши блоки
- [ ] шаг 9 — переключение: префаб `ProjectCMainMenuUI` (`UIDocument` + `PanelSettings`),
  `MainMenuWindow` выключен как откат, `ProjectCMainMenuMenuBridge` перевешен
  с `main-menu-*` на новое окно

## 6. Перенос в Unity — что сделано

Дата: 2026-10-27. Всё лежит в `Assets/Aura/Mods/ProjectCMainMenu/`.

### 6.1 Файлы

| Файл | Роль |
|---|---|
| `Shaders/PCMM_{Sky,Gas,Stars,Veil,Rope,Hull,Glow}.shader` | 7 unlit-шейдеров URP, имена `ProjectC/Mods/MainMenu/*` |
| `Materials/PCMM_*.mat` | 7 материалов, созданы `AssetDatabase`-ом |
| `Scripts/ProjectCMainMenuPalette.cs` | палитра + `Damp`/`SmoothStep`/`Mulberry32` |
| `Scripts/ProjectCMainMenuDayNight.cs` | часы День/Сумерки/Ночь/Рассвет |
| `Scripts/ProjectCMainMenuCloudTexture.cs` | печёт тайловые маски завесы в `Texture2D` (192²) |
| `Scripts/ProjectCMainMenuRopeRig.cs` | 3 троса-ленты (57 узлов) + паром, меш пересобирается в кадре |
| `Scripts/ProjectCMainMenuWindDust.cs` | `ParticleSystem` вместо `THREE.Points` |
| `Scripts/ProjectCMainMenuAtmosphere.cs` | стек квадов, `Blow`/`SetFocus`/`SetVisible`, день-ночь |
| `Scripts/ProjectCMainMenuMenuBridge.cs` | читает живое меню, гоняет порывы и фокус |
| `Editor/ProjectCMainMenuBuilder.cs` | одноразовая сборка слоя, материалов и префаба |
| `Prefabs/ProjectCMainMenu.prefab` | корень + `Cables` + `WindDust` |

### 6.2 Как устроено

- Атмосфера живёт на **своей** камере (`AtmosphereCamera`, создаётся в рантайме): depth **100**
  (рисуется после `MainCamera`), `clearFlags = SolidColor` (`GasDeep`), fov **50**,
  `cullingMask = 1 << PCMM_Atmosphere`, near 0.01 / far 10. Слой `PCMM_Atmosphere` = индекс **9**
  (создан билдером в `TagManager.asset`).
- Порядок квадов (`sortingOrder`): Sky −100, Stars −90, Veil 20…23, Gas 30, тросы 34…36, пыль 46;
  все `ZTest Always`, `ZWrite Off`, `Cull Off`. Так Overlay-UI (`PanelSettings`) остаётся поверх.
- 4 слоя завесы различаются только `MaterialPropertyBlock` (общий `PCMM_Veil.mat`):
  `_PlaneScale`, `_Repeat`, `_Seed`, `_Band`, `_Parallax`, `_Opacity`, плюс маски из
  `ProjectCMainMenuCloudTexture.Create(192, seed, coverage, softness, puffs)`.
- Связь с меню — только через `ProjectCMainMenuMenuBridge`: `main-menu-buttons` наведение = 0.5 порыва,
  нажатие = 1.0, направление от центра панели, источник в UV; `main-menu-root` скрыт → камера
  выключена, открыта под-панель (`display != None`) → `SetFocus(1)` (облака расходятся).
  `MainMenuWindow.cs/uxml/uss` **не тронуты**.
- **До шага 9 мост смотрит на старое дерево.** `ProjectCMainMenuMenuBridge` ищет
  `main-menu-buttons` / `main-menu-root`, то есть живёт на `MainMenuWindow`. Новое окно
  `ProjectCMainMenuWindow` он пока не видит: после включения `ProjectCMainMenuUI` мост
  придётся перевесить, иначе порывы и фокус завесы перестанут срабатывать.

### 6.3 Что осталось нашим

Настройки (`SettingsManager` + секции), локализация (`Loc`/`LocaleSelector`), host/connect
(`NetworkManagerController`), админ-сбросы (`PersistenceDebugTools`), changelog с GitHub,
дисклеймер, `EscMenuWindow` (владеет `Esc`), `UIDocument` + `PanelSettings`.

### 6.4 Что переведено иначе, чем в моде

- `THREE.Points` + `gl_PointSize/gl_PointCoord` → `ParticleSystem` (32² точка, генерируется в C#).
- Кастомные атрибуты `aFade`/`aSide` → `uv.x = side`, `uv.y = fade` (у Unity нет своих вершинных стримов).
- `uResolution`/`gl_FragCoord` → `uv * _ScreenParams.xy`.
- `texture2D` → `SAMPLE_TEXTURE2D`, uniform-ы → `Properties` + `CBUFFER_START(UnityPerMaterial)`.
- Тайловые маски облаков — 192² (в плане упоминалось 256²; 192² как в моде), пекутся один раз на старт.

### 6.5 TODO

- Пользовательская проверка меню мода в канвасе (шаг 4) — не выполнена.
- Play Mode не запускался (правило проекта): поведение, `[PCMM]`-логи и внешний вид проверяет пользователь.
- `PCMM_Veil.shader` объявляет `_Soft`, но во фрагменте не использует — безвредно.
- Шаг 5 плана формально зависит от шага 4 (приёмка в моде); перенос сделан по нажатию Start.
- Шаг 9: префаб `ProjectCMainMenuUI`, `PanelSettings` `ScaleWithScreenSize` с опорой
  1920×1080, `MainMenuWindow` остаётся выключенным как откат.
- Шаг 9: перевесить `ProjectCMainMenuMenuBridge` с `main-menu-*` на `ProjectCMainMenuWindow`
  (`OnBlow`, `OnAction`, `Hovered`, `SettingsOpen`), иначе 3D-слой не реагирует на меню.
- Визуальная приёмка блоков шага 8 (`.pc-proj-*`) в реальной панели — за пользователем.
- `.gitignore` исключает `*.shader`, `*.prefab` и `*.meta`: шейдеры `PCMM_*`, префаб и все
  GUID-ы вне Git. Билдер меню пересоздаёт материалы и префаб, но **не** исходники шейдеров.

### 6.6 Исправлено после первого запуска (2026-10-27)

Пользователь сообщил: «в консоли много ошибок, меню старая». Найдены и исправлены три причины.

**1. Поток ошибок в консоли — NaN в вершинах тросов.** На последнем узле троса
`u = 1`, `Mathf.Sin(Mathf.PI * 1f)` = `−8.7e−8` (**отрицательное**), а `Mathf.Pow(<0>, 1.12f)` = `NaN`.
Одна NaN-вершина отравляла `RecalculateBounds` → `Invalid AABB a`,
`Invalid worldAABB. Object is too large or too far away from the origin.`
и `Mesh 'PCMM_Rope_N': abnormal mesh bounds` каждый кадр × 3 троса.
Исправление: `arc = Mathf.Max(0f, sin)` перед `Pow` (`ProjectCMainMenuRopeRig.cs`).
Численная проверка: 8 208 сэмплов (3 троса × 57 узлов × 48 моментов) — старая формула даёт
**144** нефинитных значения, новая — **0**.

**2. Атмосфера была физически невидима.** `.main-menu-root` в `MainMenuStyles.uss` —
единственный полноэкранный непрозрачный элемент: `background-color: rgba(8, 12, 22, 0.92)`.
Он перекрывал весь 3D-слой → «меню старое». Исправление без правки USS/UXML:
`ProjectCMainMenuMenuBridge` ставит `main-menu-root.style.backgroundColor` в прозрачное
(инлайн-стиль сильнее stylesheet и самовосстанавливается, если меню пересоберёт дерево).
Флаг `clearMenuBackdrop` (по умолчанию `true`) в префабе и в экземпляре — проверено.

**3. Тросы/паром/пыль не рендерились.** Камера атмосферы имеет
`cullingMask = 1 << PCMM_Atmosphere`, но `RopeRig` и `WindDust` создавали объекты на слое 0.
Добавлены `ApplyLayer(int)`, вызываемые из `Atmosphere.Build()` после `Initialize()`/перевешивания.

После этих правок ничего не сохранено: `BootstrapScene` остаётся dirty, сохранение за вами.

### 6.7 Как проверять

1. Открыть `Assets/_Project/Scenes/BootstrapScene.unity` — в корне лежит экземпляр `ProjectCMainMenu`.
2. Play. Ожидаемые логи: `[PCMM] atmosphere built (layer=9, veil layers=4, ropes=True, dust=True)`,
   `[PCMM] gust ...` при наведении/нажатии на кнопку, `[PCMM] atmosphere camera hidden` при уходе из меню.
3. Глазами: за меню небо/звёзды/4 слоя завесы/меззий/3 троса с паромом/пыль; наведение на кнопку
   раздвигает облака; «НАСТРОЙКИ» и IP-панель расфокусируют завесу; сутки идут по кругу.
4. Префаб пересобирается меню `Tools/ProjectC/MainMenu/Build Atmosphere Prefab` (идемпотентно).

## 7. Порт меню мода в Unity (шаги 1–8 из 9)

Дата: 2026-10-27. Всё лежит в `Assets/Aura/Mods/ProjectCMainMenu/`; план, пределы платформы
и журнал решений — `docs/UI/MainMenu/02_smoke_menu_port.md`.

Новое окно — `ProjectCMainMenuWindow`, объявленное `public sealed partial` и разложенное по
трём файлам. UXML нет: дерево строится кодом, единственный источник вида — `PCMM_Menu.uss`
на опоре 1920×1080.

### 7.1 Файлы по шагам

| Шаг | Файл | Роль | Отчёт |
|---|---|---|---|
| 1 | `Scripts/ProjectCMainMenuSmokeTextures.cs` | дымовые листы `Texture2D` + кэш | — |
| 2 | `UI/PCMM_Menu.uss` | перенос `ui.css` на USS, 197 правил | — |
| 3 | `Scripts/ProjectCMainMenuWindow.cs` | дерево, 221 элемент | `_tree_verify.txt` |
| 4 | `Scripts/ProjectCMainMenuSmokeField.cs` | поле дыма под буквами | `_smoke_field_verify.txt` |
| 5 | `Scripts/ProjectCMainMenuTextBlower.cs` | выдувание букв в дым | `_text_blower_verify.txt` |
| 6 | `Scripts/ProjectCMainMenuAltimeter.cs` | лента высот 800/1200/2200/3000 м | `_altimeter_verify.txt` |
| 7 | `Scripts/ProjectCMainMenuWindow.Behaviour.cs` | сборка/распад частей, кнопки, настройки, вкладки, тосты, густы | `_behaviour_verify.txt` |
| 8 | `Scripts/ProjectCMainMenuProjectPanels.cs` | наши блоки `.pc-proj-*` | `_project_panels_verify.txt` |

### 7.2 Куда переехал каждый наш элемент

| Наш элемент (`MainMenuWindow`) | В новом окне |
|---|---|
| `main-host-btn` «ОДИНОЧНАЯ ИГРА» | `.pc-btn` с действием `host`, высота 3000 м |
| `main-connect-btn` «ПОДКЛЮЧИТЬСЯ К СЕРВЕРУ» | `.pc-btn` `connect` → `OnConnectRequested` → `OpenIp()` |
| `main-settings-btn` «НАСТРОЙКИ» | `.pc-btn` `settings` → `OpenSettings()` |
| `main-quit-btn` «ВЫХОД» | `.pc-btn` `quit` |
| `main-ip-panel` | `.pc-proj-ip`: `pc-proj-ip-field`, `pc-proj-ip-connect`, `pc-proj-ip-back` |
| `main-debug-panel` | `.pc-proj-admin`: 10 × `pc-proj-reset-<id>` + `pc-proj-admin-status` |
| `main-changelog-panel` | `.pc-proj-changelog`: `…-head`, `…-refresh`, `…-status`, `…-body` |
| `main-links` | `.pc-proj-links` + 5 × `pc-proj-link-0…4` |
| `main-lang-selector` | `.pc-proj-lang` + `pc-proj-lang-select` |
| Дисклеймер первого запуска | `.pc-proj-disclaimer` |

Слой `.pc-proj-layer` — последний ребёнок `Root`, сосед `.pc-ui`, `pickingMode = Ignore`;
у каждого из шести блоков `pickingMode = Position`. Поэтому блоки рисуются поверх дыма,
карточки настроек и тоста, а пустой слой не перехватывает клики у меню.

Настоящие проектные API подключены швами, которые по умолчанию ничем не подменены:
`NetworkManagerController.ConnectToServer(ip, 7777)`, `PersistenceDebugTools`,
`LocaleSelector`, `Application.OpenURL`, `PlayerPrefs`
`ProjectC.MainMenu.FirstLaunchDisclaimerAccepted`.

### 7.3 Что пока остаётся легаси

- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` + `MainMenuWindow.uxml` +
  `MainMenuStyles.uss` + `MainMenuPanelSettings.asset` — в сцене остаются включёнными
  до шага 9 и служат откатом.
- `ProjectCMainMenuMenuBridge` — читает `main-menu-buttons` / `main-menu-root`, то есть
  старое дерево; перевешивается на новое окно шагом 9.
- Секции настроек (`GraphicsSettingsSection` / `AudioSettingsSection` /
  `GameplaySettingsSection`) — в новом окне три вкладки `pc-panel-*` пока собраны
  заглушками (`placeholder до AudioMixer` и строка «Страница повторяет … проекта»);
  живые виджеты остаются в старом меню.
- `EscMenuWindow` (владеет `Esc`) не тронут — новое окно `Esc` не обрабатывает.

### 7.4 Риск версий: часть файлов вне Git

`.gitignore` исключает `*.prefab`, `*.shader`, `*.shadergraph` и `*.meta`. В репозитории
лежат скрипты, `PCMM_Menu.uss`, материалы `PCMM_*.mat`, билдер и отчёты `_*_verify.txt`;
вне репозитория остаются семь шейдеров `Shaders/PCMM_*.shader`, префаб
`Prefabs/ProjectCMainMenu.prefab` и все GUID-ы. Из чистого клона
`Tools/ProjectC/MainMenu/Build Atmosphere Prefab` соберёт материалы и префаб, но
`Shader.Find` вернёт `null` (`[PCMM] shader not found: …`): исходники шейдеров билдер
не создаёт и должен получить отдельно. `.gitignore` в этом проходе не менялся.

