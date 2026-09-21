# T-FO09S — Пайплайн добавления нового контента на сцену (ретроспектива парома)

Date: 2026-09-21. Статус: действует. Первый реальный кейс: `ParomRoute_01`
(`T-PAROM-01`) уронил старт хоста пилота; починено регистрацией, игра едет.

## 1. Что случилось (факты)

1. После ввода FO на вопрос «как быть с новыми системами и объектами» был дан
   совет: «помещаем под рут — и всё работает».
2. По нему в `WorldScene_0_0` под `WorldRoot_0_0` добавлен `ParomRoute_01`
   (менеджер парома + якоря + тросы + кабинка).
3. Нажатие Host упало fail-closed:
   `[T-FO06L] Pilot global startup refused: scene_preparation:
   uncontrolled_network_source_before_native_sweep:name=ParomRoute_01...`.
4. Диагностика и фикс заняли отдельную итерацию: маркер + observation + entry
   в `GlobalMotionPilotSceneCatalog.asset` (стало 151 запись), пересчёт
   `SceneLayoutDigest` в `GlobalMotionPilotProfile.asset`, плюс по ходу —
   репарент якорей сиблингами, `Rigidbody` на кастомную кабинку, фикс
   дублирования тележки. После этого Host стартует, тележка ездит.
5. Детали кейса — `docs/world/parom_road/00_PAROM_DESIGN.md` §8.

## 2. Почему совет был неполон (корень ошибки)

Совет описывал **только одну из двух** независимых подсистем FO. У них разные
правила, и «под рут» закрывает лишь первую:

| # | Подсистема | Что проверяет | Правило «под рут» |
|---|---|---|---|
| A | Сдвиг мира (`GlobalMotionControlledRebaseSlice`, F8/авто/F9) | Едут ли объекты вместе с миром | ✅ Достаточно: дети корней `WorldScene_*` едут бесплатно; кэши `Vector3` — через `ApplyRebaseTranslation`; рантайм-спавн вне корней — через регистрацию участником |
| B | Допуск при старте (`GlobalMotionPilotRuntime` → `GlobalSceneNativeExecutor.BuildPreparation`) | Нет ли в загруженных сценах **неизвестных системе** `NetworkObject` | ❌ Недостаточно: каждый scene-placed `NetworkObject` обязан иметь `GlobalSceneSourceMarker` + парную запись в каталоге + сходящийся digest, иначе Host не стартует вообще |

Путаница была возможна, потому что:

- До парома новый контент (сундуки, статик, разметка) был **без `NetworkObject`**,
  а гейт B цензит **только** `NetworkObject` (плюс маркеры). Обычные объекты
  ему невидимы — поэтому «всё переживало» и казалось, что правила A покрывают всё.
- `ParomRoute_01` — первый новый **сетевой** объект сцены после ввода FO,
  первый и упёрся в гейт B.
- `09R_BAKE_TOOL_GAP.md` (2026-09-14) зафиксировал тогдашний регламент
  «новый контент — БЕЗ маркеров», верный для подсети A и для эпохи, когда
  строгий старт пилота на практике не встречал новых NO. Этот регламент
  **больше не действует для объектов с `NetworkObject`** (см. §5).

Вывод без самобичевания: правило A остаётся верным в своей области; ошибка —
в умолчании, что области две. Этот документ закрывает умолчание.

## 3. Пайплайн: новый объект на сцену (действует)

### Шаг 0. Классифицируй объект (один вопрос)

Есть ли на объекте (или появится в рантайме до старта сети) компонент
`NetworkObject`?

**НЕТ → ветка «простой контент» (§3.1). ДА → ветка «сетевой объект» (§3.2).**

Смотреть надо весь сабтри: гейт считает `GetComponentsInChildren` (дочерний
`Rigidbody`/коллайдер тоже влияют на ownership, см. таблицу ниже).

### 3.1. Простой контент (без NetworkObject): каталог НЕ нужен

Маркер не ставить, каталог не трогать, digest не пересчитывать. Действуют
только правила подсети A (см. `AGENTS.md`, раздел Floating Origin):

- 🟢 Под корень `WorldScene_*` — едет бесплатно (якоря парома `Parom_A_Start…`
  лежат сиблингами под `WorldRoot_0_0` именно поэтому — их можно двигать
  свободно, каталог их не видит, хэши не меняются).
- 🟡 Скрипт хранит мировые `Vector3` между кадрами — `ApplyRebaseTranslation`
  + вызов в shift-блоке слайса (оба пути: success и rollback).
- 🔴 Рантайм-спавн вне корней сцен — регистрация участником или parent
  под участника.
- 💾 Сейвы мировых координат — хранить + применять кумулятив при загрузке.
- Проверка: F8 → `runtimeRebase.Completed` + объект на месте + 0 errors; F9.

### 3.2. Сетевой объект (с NetworkObject): обязательна регистрация

Без неё Host упадёт на `uncontrolled_network_source_before_native_sweep`.
Порядок (прецедент — паром, корабли, `Road to Quartus`):

1. **Положи объект под корень своей сцены** (`WorldScene_*`). Все якоря ветки —
   в пределах одной сцены. Объект активен (для `Unmanaged` активность разрешена).
2. **Проверь физику до регистрации:** для `ownership = ShipOrRigidbodyRoot`
   в сабтри обязан быть `Rigidbody` (считается через `GetComponentsInChildren`,
   включая неактивных); для `SceneOwnedNetworkGameplay` — наоборот, его быть
   не должно. Кастомные префабы без коллайдеров/тел — дополнить (пример:
   `ParomTrolley.EnsurePhysics`).
3. **Запеки маркер** `GlobalSceneSourceMarker` на корне объекта:
   `sourceId = {sceneGuid}:{targetObjectId}:{targetPrefabId}`
   (`GlobalObjectId.GetGlobalObjectIdSlow`, руками не выдумывать),
   `frameId = 0`, `activateWhenReady = false` (для `Unmanaged`).
4. **Добавь observation** в блок своей сцены в `GlobalMotionPilotSceneCatalog`:
   `parentSourceId` = sourceId ближайшего предка **с маркером**
   (у парома — `WorldRoot_0_0` `...:14177344:0`; предок без маркера не подходит —
   будет `authored_parent_binding_mismatch`), `isRoot`/`isNetworkObject` по факту,
   `layoutHash` — строго алгоритмом `AuditGlobalSceneCatalog.LayoutHash`
   (SHA256 `PCFO_SCENE_STRUCTURE_V1` по всему сабтри: имена, позиции, компоненты).
5. **Добавь entry** (пример — запись парома `...:834841855:0`):

   | Поле | Новый геймплей (дефолт) | Комментарий |
   |---|---|---|
   | `treatment` | `Unmanaged (5)` | Исполнитель оставляет как есть; спавн — `ScenePlacedObjectSpawner`/NGO |
   | `ownership` | `ShipOrRigidbodyRoot (4)` если есть Rigidbody, иначе `SceneOwnedNetworkGameplay (3)` | См. `ValidateOwnership`; статика без NO — `AuthoredSceneContent (1)` |
   | `spatial` | `false` | `Unmanaged` + spatial = отказ компилятора |
   | `poseKind`/`worldPosition`/`parentLocalPosition`/`rotation`/`scale` | `None`/ноль/ноль/identity/one | `ValidPose` требует нули для non-spatial |
   | `parentSourceId` | = observation.parentSourceId | Иначе `unreviewed_..._reparented_entry` |
   | `reviewNote` | **Непустая, осмысленная** | Человек аттестует решение; пустая = отказ |

6. **Обнови хэши соседей:** `layoutHash` всех предков-кандидатов, чей сабтри
   изменился (у парома — `WorldRoot_0_0`: +1 потомок), и `dependencyHash` сцены
   (`AssetDatabase.GetAssetDependencyHash` после финального сейва сцены).
7. **Пересчитай digest:** `TryCompile` → `DigestHex` → поле `_sceneLayoutDigest`
   в `GlobalMotionPilotProfile.asset`. Проверь связку `TryCompile=True` +
   `TryMatchDigest=True` на перечитанных с диска ассетах.
8. **Проверки перед Host:** консоль 0 errors; `AuditGlobalSceneCatalog.Run()`
   не должен показать новых расхождений по твоей сцене
   (чужие stale — см. §4 — фиксировать отдельно, не в этом чейнже).

### 3.3. Что можно менять потом без перерегистрации

- ✅ Двигать якоря/позиции **вне** сабтри зарегистрированного объекта;
  крутить сериализованные поля (скорость, ожидание, материалы).
- ⚠️ Менять **структуру сабтри** объекта (другая кабинка-FBX, ±дети,
  переименования, вкл/выкл, сдвиг самого корня): меняется `layoutHash`.
  Host НЕ падает (хэш сверяется только будущими аудитами), но каталог
  становится stale — пересчитать хэш + digest отдельной правкой.
- ⚠️ Новая ветка/новый NO-объект = полный §3.2 с нуля.

## 4. Известный несвязанный дрейф (не чинить в контентных чейнжах)

- `BootstrapScene`: 3 незакаталогизированных plain-корня
  (`DistantFocus`, `ChartWindow`, `VeilController` — без NO и маркеров) +
  stale `dependencyHash`. Host не блокируют (гейт цензит только NO и маркеры),
  но `AuditGlobalSceneCatalog` помечает каталог stale. Отдельная задача.
- Общее правило: каталог — снапшот. Любой сейв сцены после пересчёта делает
  `dependencyHash` устаревшим; структурные правки — и `layoutHash`.
  Контентный чейндж трогает только свои записи (§3.2 пп. 4–7).

## 5. Статус 09R

`09R_BAKE_TOOL_GAP.md` §«Текущий регламент» (п. 1–3: контент без маркеров,
маркер руками не ставить, каталог не править) **отменён для объектов
с `NetworkObject`** настоящим документом. Для простого контента (§3.1)
регламент 09R действует как раньше. Сама Bake-тулза из 09R по-прежнему
отсутствует; осознанно не автоматизируем: `reviewNote` — это аттестация
человека, штамповка без неё противоречит смыслу closed-world допуска.

## 6. Использованные тулзы (для повторения)

- `read_console` + `editor/state` (MCP) — цикл правка→компиляция→проверка.
- `execute_code` (MCP) — точечные чтение/правка сцены и ассетов каталога
  (репарент, маркер, хэши, digest) без открытия окон.
- `unity_reflect search` — подтверждение, что типы в сборках.
- Исходники-аудиты в дереве: `Assets/_Project/Editor/FloatingOrigin/
  AuditGlobalSceneCatalog.cs` (алгоритмы `SourceId`/`LayoutHash`, read-only
  меню `ProjectC/World/Floating Origin/...`), контракт —
  `GlobalSceneNativeExecutor.cs:190–211` (гейт), `GlobalSceneCatalogCompiler.cs`
  (правила), `GlobalSceneSourceMarker.cs:ValidateOwnership` (таблица ownership).
- Прецеденты в каталоге: корабли (`ownership 4`), `Road to Quartus`
  (`...:1987571259:0`, `Unmanaged`/`ownership 3`), док `06L` §315.
