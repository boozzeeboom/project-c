# Строительство на корабле — глубокий анализ подсистем

> **Дата:** 2026-07-04
> **Тип документа:** глубокий анализ — что у нас есть, что не хватает, что **не нужно** строить
> **Контекст:** гипотеза — дать игроку возможность "строить" корабль, закрепляя на нём стенки, отсеки и т.п. **Только визуал** (без влияния на физику движения).
> **Читать вместе с:** `../00_SUMMARY.md` (L0-L6 кастомизации), `../01_MODULE_VISUAL_WITHOUT_BONES.md` (L1 модулей), `../../../Ships/00_COMPOSITE_SHIP_SUMMARY.md` (базовая архитектура)

---

## TL;DR — короткий ответ

**У нас есть 80% нужной инфраструктуры.** Строительство — это **L6 из `00_SUMMARY.md`**, и в общем виде это:

```
Рецепт (RecipeData) → Крафт стенки/отсека → Inventory → Build Mode UI → Ghost preview → Snap → Confirm → Server RPC → Сохранение
```

**Но.** Это **большой проект** (6-12 недель по моей оценке в `00_SUMMARY.md`), и 90% стоимости — не код, а **продуктовые решения**: какие элементы доступны, как они физически крепятся, что считается "правильной" компоновкой, как они влияют (или НЕ влияют) на геймплей.

**Главный архитектурный вопрос ДО кода:** это **player expression** (как в Fortnite Creative, Sea of Thieves) или **player progression** (как в Cosmoteer, MechWarrior)? От ответа зависит всё остальное.

| Вариант | Трудоёмкость | Когда выбирать |
|---|---|---|
| **A. Чисто визуальное** (как Fortnite Creative) | 2-4 недели | Если главное — креатив + social |
| **B. Визуальное + грид-бонус** (как Cosmoteer) | 6-10 недель | Если главное — функциональная сборка |
| **C. Полное** (как X4, Space Engineers) | 6+ месяцев | ❌ Не для stage 2.5 |

**Моя рекомендация:** **вариант A**, и только после `MyShipsTab` (T-KEY08) и L1 модулей (см. `01_MODULE_VISUAL_WITHOUT_BONES.md`).

---

## 1. Что у нас уже есть (по подсистемам)

Полная инвентаризация релевантной инфраструктуры.

### 1.1 Subsystem inventory

| Подсистема | Файлы | LOC | Что даёт для "строительства" |
|---|---|---|---|
| **Inventory (per-client state)** | `Assets/_Project/Items/Core/InventoryWorld.cs`, `Items/Network/InventoryServer.cs`, `Items/Client/InventoryClientState.cs` | ~1500 | У игрока в инвентаре могут быть "стенки", "отсеки" как `ItemData` |
| **Crafting (рецепты + станции)** | `Assets/_Project/Scripts/Crafting/CraftingServer.cs`, `CraftingWorld.cs`, `RecipeData.cs`, `CraftingStation.cs` | ~1500 | Рецепт "сделать стенку из железа" → кладём в станцию → получаем ItemData |
| **Docking (зоны + trigger boxes)** | `Assets/_Project/Scripts/Docking/Network/DockStationController.cs`, `Docking/Core/DockingWorld.cs`, `Zones/OuterCommZone.cs`, `Stations/DockingPadTriggerBox.cs` | ~1200 | Паттерн "зона входа → регистрация в singleton" — применимо к "зоне строительства" |
| **KeyRodInstance (per-ship identity)** | `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` | ~600 | У каждого корабля есть уникальный instanceId — к нему можно привязать "постройку" |
| **ShipModuleServer (RPC для модулей)** | `Assets/_Project/Scripts/Ship/ShipModuleServer.cs` | ~400 | Готовый серверный хаб для модификации корабля — расширяем |
| **Customisation (client-side persistence)** | `Assets/_Project/Scripts/Customisation/CustomisationClientState.cs`, `Player/CharacterCustomisationApplier.cs` | ~700 | Локальный JSON persistence по `clientId` — паттерн для "мои постройки" |
| **Composite Ship (root + children)** | `Assets/_Project/Scripts/Ship/ShipRootReference.cs`, `ModuleSlot.cs` | ~250 | Маркер + дочерние компоненты — "куда крепить" |
| **NPC-ship (FSM + schedule)** | `Assets/_Project/Scripts/PeacefulShip/Stations/NpcShipController.cs` | ~1000 | Референс по сложной NetworkBehaviour-логике на корабле |
| **NpcSpawner (spawn + rate limit + leash)** | `Assets/_Project/Scripts/AI/NpcSpawner.cs` | ~400 | Референс по "server-side spawn с ограничениями" |
| **NpcBrain (FSM)** | `Assets/_Project/Scripts/AI/NpcBrain.cs` | ~800 | Референс по FSM в NetworkBehaviour |
| **MetaRequirement (gate по требованиям)** | `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs` | ~600 | "Можно ли строить?" — ключ, навык, репутация |
| **NPC visual config** | `Assets/_Project/Scripts/Npc/NpcVisualConfig.cs` | ~200 | Паттерн `visualPrefab` swap на сетевом объекте |

**~9000 LOC готовой инфраструктуры**, релевантной для строительства.

### 1.2 Какие паттерны можно прямо копировать

| Что | Где взять | Что строить |
|---|---|---|
| **Per-client persistence** | `InventoryWorld` (JSON repo) | Хранить "что построено на корабле X для clientId Y" |
| **Replicated state + RPC** | `ShipModuleServer.RequestInstallModuleRpc` | `BuildRequestBuildRpc(itemId, snapPosition, snapNormal, rotation)` |
| **Singleton state on server** | `KeyRodInstanceWorld` (static, server-only) | `BuildWorld` — реестр "какие объекты на каких кораблях" |
| **Recipe + Ingredients → Outputs** | `RecipeData` | Рецепт "собрать стенку из 4× Iron Ingot" |
| **NetworkBehaviour trigger zone** | `CraftingStation` (`OnTriggerEnter` → register) | `BuildZone` — где игрок может строить |
| **Client-side UI singleton** | `CustomisationClientState` + `CharacterCustomisationApplier` | `BuildClientState` + `BuildGhostApplier` (preview + apply) |
| **Composite marker pattern** | `ShipRootReference` + `ShipComponentLocator` | `BuildSlotReference` — точка крепления на корпусе |
| **NPC FSM** | `NpcBrain` (state-машина с сервера) | FSM "идёт крафт → готов к установке → установлен" |
| **Static event broadcast** | `ShipModuleServer.OnModuleChanged` (static `Action<ulong>`) | `BuildWorld.OnBuildChanged(shipNetId)` |

**Прямых аналогов "free placement с snap + ghost preview" у нас нет** — это нужно строить с нуля, но это не rocket science (см. §5).

---

## 2. Анализ требований — что игрок должен мочь

### 2.1 Что сказал пользователь

> "дать игроку возможность строить корабль, закрепляя на него штуки... стенки, отсеки и т.п... реч только о визуале"

**Разбираем:**

| Слово | Что значит в контексте | Что это значит для архитектуры |
|---|---|---|
| "строить" | Добавлять элементы в runtime (а не при создании префаба) | L6 — нужна build mode + ghost preview + snap + RPC |
| "штуки" | Атомарные элементы (стенка, отсек, пол, крыша, лестница...) | `BuildableItem` SO с типом, размером, attachment points |
| "закреплять" | Прикреплять к чему-то (корпус, другой элемент, специальная зона) | `SnapSocket` система (см. §4) |
| "стенки" | Плоские элементы, обычно скрепляются друг с другом | Gridded или free placement? |
| "отсеки" | Замкнутые объёмы (пол + 4 стенки + крыша) | Composition из "штук" — верхнеуровневая абстракция |
| "только визуал" | Никаких physics-эффектов, никаких gameplay-бонусов | Упрощает ВСЁ — нет валидации коллизий, нет mass rebalance |

### 2.2 Что **НЕ** входит (явно за рамками)

| Что | Почему не входит |
|---|---|
| **Физические эффекты** | `Rigidbody.mass` не меняется, drag не пересчитывается, центр масс не двигается. Всё "приклеено" визуально. |
| **Gameplay-бонусы** | Построенный отсек не даёт +cargo slots, не блокирует ветер, не влияет на stealth. Только эстетика. |
| **Звуки / партиклы** | Построенная стенка не скрипит, не свистит. (Можно потом.) |
| **Разрушаемость** | Строение нельзя сломать (не боёвка). |
| **Движущиеся части** | Двери, люки, окна с анимацией — это модули (L1), не строительство. |

**Это сильно упрощает систему** (см. §6 — анти-restrictive подход).

---

## 3. Главное архитектурное решение — что мы строим

### 3.1 Три варианта продуктового позиционирования

#### Вариант A — "Player Expression" (Fortnite Creative / Sea of Thieves)

```
Свободная компоновка:
- Игрок крафтит/покупает "стенку", "пол", "окно", "флаг"
- В build mode тащит ghost preview по snap-точкам
- Подтверждает → спавнится visualPrefab
- Можно удалить (в build mode или через меню)
- Можно вращать (R key, 90° шаги)
- Может быть любая форма (не "правильный" корпус)

Use case: "хочу корабль как у пирата — с пушками на крыше и парусами в клетку"
```

| ✅ Плюсы | ❌ Минусы |
|---|---|
| Простая логика (visual only) | Можно строить нелепые штуки |
| Высокий player value | Нет "правильного" ответа — баланс не сделать |
| Social (показать друзьям) | Нужно много контента (стенки, флаги, фонари...) |

#### Вариант B — "Functional Building" (Cosmoteer / MechWarrior)

```
Грид-система:
- Корабль = 3D-сетка (например, 1 unit = 1m)
- Игрок строит в клетках сетки
- Каждая клетка может содержать 1 элемент
- Соседние клетки влияют (стенка + пол = отсек)
- Грид бонусы (cargo, crew, weapons)

Use case: "хочу корабль с 4 турелями, 2 трюмами, кабиной"
```

| ✅ Плюсы | ❌ Минусы |
|---|---|
| Баланс возможен | Сложная логика валидации |
| Есть "правильные" конфигурации | Нужно ПЕРЕпроектировать физику корабля |
| Прогрессия (открываются новые блоки) | Требует хорошего UI (3D cursor в гриде) |

#### Вариант C — "Full Sandbox" (Space Engineers / X4)

```
Полная свобода:
- Свободное размещение + соединение
- Деформация terrain'а
- Программирование логики
- Multi-crew с разделением ролей

Use case: "хочу добывающую станцию с 6 крафтерами и конвейером"
```

| ✅ Плюсы | ❌ Минусы |
|---|---|
| Неограниченный creative freedom | Годы разработки |
| Огромный retention | Не подходит для "полетать по миру" |

### 3.2 Рекомендация: вариант A (player expression)

**Почему:**

1. **Пользователь сказал "только визуал"** → это чётко вариант A. B и C подразумевают геймплей-эффекты.
2. **Наш мир — MMO-sandbox в небе** → игроки ценят индивидуальность (как в Sea of Thieves), а не оптимизацию (как в Cosmoteer).
3. **У нас уже есть `MyShipsTab`** → это UI-хаб "владение кораблями". Build Mode — естественное расширение.
4. **Минимальные изменения в физике** → не трогаем `Rigidbody`, `ShipController`, `ShipModuleManager`. `BuildVisualApplier` — отдельный sibling-компонент на root корабля.
5. **Анти-restrictive** → старые корабли без buildable elements работают как раньше (как в `CharacterCustomisationApplier`).

**Что это НЕ означает:** не исключает вариант B в будущем. L6 → L6+ в `00_SUMMARY.md`. Сейчас делаем MVP, потом расширяем.

---

## 4. Архитектура системы (вариант A — player expression)

### 4.1 Высокоуровневая схема

```
┌─────────────────────────────────────────────────────────────────────┐
│                        BUILD SYSTEM (L6)                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  [Игрок]                          [Сервер]                          │
│     │                                  │                            │
│  1. Крафтит "стенку"             ┌────▼─────────────────┐          │
│     через CraftingStation        │ CraftingServer        │          │
│     → ItemRegistry               │ + RecipeData          │          │
│                                  └────┬─────────────────┘          │
│                                       │                            │
│  2. Подходит к кораблю           ┌────▼─────────────────┐          │
│     → InteractableManager        │ BuildZone trigger     │          │
│     (press B = "build mode")     │ (radius 5m от ship)   │          │
│                                  └────┬─────────────────┘          │
│                                       │                            │
│  3. Enter Build Mode             ┌────▼─────────────────┐          │
│     → InventoryClientState       │ BuildClientState      │          │
│     показывает доступные         │ + BuildGhostApplier   │          │
│     "стенки" в инвентаре         │ (client-only preview) │          │
│                                  └──────────────────────┘          │
│                                                                     │
│  4. Тащит ghost preview               [Server]                     │
│     → snap to: existing element,   ┌────▼─────────────────┐        │
│       ship hull socket,             │ BuildWorld (replicated│       │
│       free position                 │ per-ship state)       │       │
│     → R = rotate                    └────┬─────────────────┘       │
│     → click = confirm                   │                          │
│                                       │                            │
│  5. Confirm → RPC               ┌──────▼─────────────────┐         │
│     RequestBuildRpc(            │ ShipBuildServer         │        │
│     itemId, snapTarget,         │ (NetworkBehaviour)      │        │
│     rotation)                   │ + BuildWorld state      │        │
│     → server validates          └──────┬─────────────────┘         │
│     → adds to replicated                │                          │
│     → ClientRpc broadcasts             │                          │
│                                          │                        │
│  6. All clients see new build       ┌────▼─────────────────┐       │
│     → BuildVisualApplier spawns     │ BuildVisualApplier    │       │
│       visualPrefab at snap point    │ (per-ship, on root)   │       │
│     → updates snapshot              └──────────────────────┘       │
│                                                                     │
│  7. Persistence                  ┌──────────────────────────┐       │
│     → JSON file                  │ JsonBuildRepository      │       │
│     → per (shipInstanceId)       │ (по канону KeyRodInstance│       │
│                                  │  World / InventoryWorld)│       │
│                                  └──────────────────────────┘       │
└─────────────────────────────────────────────────────────────────────┘
```

### 4.2 Компоненты (что нужно создать)

| Компонент | Файл | LOC | Назначение |
|---|---|---|---|
| **`BuildableItem` SO** | `Assets/_Project/Scripts/Ship/Build/BuildableItem.cs` | ~150 | Данные одного элемента: type (Wall/Floor/Roof/Window/Decoration), size, visualPrefab, allowedSnapTargets, rotationSteps, maxStack |
| **`BuildItemRegistry` SO** | `Assets/_Project/Scripts/Ship/Build/BuildItemRegistry.cs` | ~100 | Список всех доступных buildable items (по аналогии с `ItemRegistry`) |
| **`BuildRecipeData` SO** (опц.) | расширение `RecipeData` | ~50 | Рецепты для крафта стенок (если хотим крафтить, а не покупать) |
| **`BuildClientState`** | `Assets/_Project/Scripts/Ship/Build/BuildClientState.cs` | ~200 | Singleton: текущий выбранный item, режим, snapshot текущего корабля |
| **`BuildGhostApplier`** | `Assets/_Project/Scripts/Ship/Build/BuildGhostApplier.cs` | ~250 | Ghost preview: цвет, transparency, snap highlight, follow mouse |
| **`ShipBuildServer`** | `Assets/_Project/Scripts/Ship/Build/ShipBuildServer.cs` | ~400 | NetworkBehaviour на корне корабля: RPC install/remove, validation |
| **`ShipBuildWorld`** | `Assets/_Project/Scripts/Ship/Build/ShipBuildWorld.cs` | ~300 | Server-only singleton: state per-ship (как `KeyRodInstanceWorld`) |
| **`BuildVisualApplier`** | `Assets/_Project/Scripts/Ship/Build/BuildVisualApplier.cs` | ~200 | Subscribe → spawn/destroy visualPrefab (как `ShipModuleVisualApplier`) |
| **`BuildZone`** | `Assets/_Project/Scripts/Ship/Build/BuildZone.cs` | ~100 | Триггер вокруг корабля: "press B to enter build mode" |
| **`BuildModeUI`** | `Assets/_Project/Ship/UI/Build/BuildModeWindow.cs` | ~400 | UI Toolkit окно: палитра элементов + кнопки действий |
| **`SnapResolver`** | `Assets/_Project/Scripts/Ship/Build/SnapResolver.cs` | ~300 | Утилита: ищет ближайший snap point от позиции (см. §5) |
| **`JsonBuildRepository`** | `Assets/_Project/Scripts/Ship/Build/JsonBuildRepository.cs` | ~200 | JSON persistence по `shipInstanceId` (по канону `JsonKeyRodInstanceRepository`) |

**Итого: ~12 новых файлов, ~2700 LOC.**

**Но.** ~50% из них — копипаста существующих паттернов (`KeyRodInstanceWorld`, `ShipModuleServer`, `CharacterEquipmentVisualApplier`, `CustomisationClientState`). Реально нового кода — ~1000-1500 LOC.

### 4.3 Состояние на сервере (replicated)

**Структура данных** (аналог `EquipmentData` parallel arrays):

```csharp
public struct BuildElementDto : INetworkSerializable
{
    public int elementId;        // ID в BuildItemRegistry
    public ulong snapTargetId;   // 0 = ship root; иначе = ID другого BuildElement
    public Vector3 localOffset;  // от snapTarget
    public Quaternion rotation;
    public byte snapFace;        // 0-5: +X/-X/+Y/-Y/+Z/-Z (для snap-to-face)
}

public struct ShipBuildSnapshot : INetworkSerializable
{
    public ulong shipInstanceId;    // KeyRodInstance (привязка к кораблю)
    public BuildElementDto[] elements;
}
```

**Хранение:** в `NetworkList<BuildElement>` на `ShipBuildServer` (server-authoritative). Синхронизация через `NetworkList` — те же гарантии, что у `EquipmentServer._equipmentList`.

**Persistence:** `JsonBuildRepository` хранит `Dictionary<int /*shipInstanceId*/, List<BuildElement>>`. Сохранение при `OnNetworkDespawn` + auto-save при изменении.

### 4.4 Состояние на клиенте (проекция)

```csharp
public class BuildClientState : MonoBehaviour
{
    public static BuildClientState Instance { get; private set; }

    // Текущая build mode state (input)
    public bool IsBuildMode { get; private set; }
    public int SelectedItemId { get; private set; }
    public Vector3 CursorWorldPosition { get; private set; }
    public ulong SnapTargetId { get; private set; }

    // Кэш для UI
    public IReadOnlyDictionary<ulong /*shipNetId*/, ShipBuildSnapshot> ShipSnapshots { get; }

    // Events
    public event Action OnBuildModeChanged;
    public event Action<ulong> OnShipBuildChanged;

    // API
    public void EnterBuildMode(int itemId);
    public void ExitBuildMode();
    public void RequestBuild(ulong snapTargetId, Vector3 offset, Quaternion rotation);
    public void RequestRemove(int elementIndex);
}
```

**Mirror:** `CharacterCustomisationApplier` (336 LOC) — структура точно такая же.

---

## 5. Snap-система — самая сложная часть

### 5.1 Что такое "snap"

В build mode игрок тащит ghost preview. Snap = автоматическое прилипание к "разумной" позиции:
- **К существующему build element** (стенка → стенка = продолжение)
- **К hull socket** (заранее определённая точка на корпусе)
- **К snap face** (грань элемента — стенку можно "прислонить" к другой стенке)

**Без snap'а:** игрок будет пытаться "воткнуть стенку в воздух" — выглядит плохо, frustating.

### 5.2 Snap target enumeration

| Тип snap target | Источник | Как найти |
|---|---|---|
| **`ShipRoot`** | сам root корабля | `slot.shipController.transform` |
| **`HullSocket`** | предзаданные точки в префабе корабля | `[SerializeField] Transform[] _hullSockets` на `ShipController` |
| **`BuildElement`** | другой построенный элемент | `dictionary<elementId, BuildElement>` |
| **`SnapFace`** | грань существующего элемента | raycast от cursor → `RaycastHit.collider.GetComponent<BuildElement>()` |
| **Free position** | земля / свободное место | `Physics.Raycast` → `hit.point` |

### 5.3 Snap resolution (как работает)

```csharp
public class SnapResolver
{
    private const float MAX_SNAP_DISTANCE = 2f; // 2 метра

    public SnapResult Resolve(Vector3 cursorPos, BuildableItem currentItem)
    {
        // 1. Raycast от cursor — попадаем в что-то?
        if (Physics.Raycast(cursorPos, Vector3.down, out RaycastHit hit, MAX_SNAP_DISTANCE))
        {
            // 2. Попали в build element? → snap to element
            var elem = hit.collider.GetComponentInParent<BuildElement>();
            if (elem != null && elem.AcceptsChild(currentItem))
            {
                return SnapResult.SnapToElement(elem, hit);
            }

            // 3. Попали в hull? → snap to hull socket (если рядом)
            var ship = hit.collider.GetComponentInParent<ShipController>();
            if (ship != null)
            {
                var socket = FindNearestHullSocket(ship, hit.point);
                if (socket != null && Vector3.Distance(socket.position, hit.point) < MAX_SNAP_DISTANCE)
                    return SnapResult.SnapToSocket(socket);
            }
        }

        // 4. Fallback: free position (с предупреждением UI "не привязано")
        return SnapResult.FreePosition(cursorPos);
    }
}
```

**Это ~250 строк.** Не rocket science, но требует тщательной работы с edge cases (см. §8).

### 5.4 Rotation

- **R** = rotate 90° по Y axis (стандарт для стенок).
- **Shift+R** = rotate free (опционально для декораций).
- **Snap to face** = rotation вычисляется автоматически (если стенку прислонить к face другой стенки, она встанет перпендикулярно).

---

## 6. Важные архитектурные решения

### 6.1 Где хранить "что построено" — 4 варианта

| Вариант | Плюсы | Минусы | Когда выбирать |
|---|---|---|---|
| **A. `JsonBuildRepository` per shipInstanceId** | Простая персистенция (как `KeyRodInstance`) | Нужно знать instanceId при reconnect | ✅ По умолчанию |
| **B. `NetworkList` на `ShipBuildServer`** | Replicated, multiplayer-ready | Сложнее, нужен network sync | Если multiplayer важен |
| **C. Local JSON per clientId** (как `CustomisationSave`) | Простейшая | Другие НЕ видят | ❌ Только single-player |
| **D. Hybrid: replicated + JSON backup** | Лучшее из двух | Дублирование | Для production |

**Рекомендация:** **A + B hybrid**:
- `NetworkList` на `ShipBuildServer` — replicated, multiplayer.
- `JsonBuildRepository` — persistence между сессиями. При старте сервера читает JSON → заполняет `NetworkList`.

Это **в точности** паттерн `EquipmentServer` + `JsonKeyRodInstanceRepository`. Не нужно ничего нового изобретать.

### 6.2 Построенный элемент — ItemData или отдельный BuildableItem?

| Аспект | `ItemData` (общий) | `BuildableItem` (новый) |
|---|---|---|
| Хранение в инвентаре | ✅ Да | ❌ Нет (хранится в BuildWorld) |
| Крафт через `RecipeData` | ✅ Да | ❌ Нет (отдельный рецепт) |
| `visualPrefab` поле | ✅ Есть | ✅ Есть |
| Stack (`maxStack`) | ✅ Есть | ❌ Не нужно (1 штука = 1 элемент) |
| Drop / pickup / trade | ✅ Да | ❌ Нет |

**Рекомендация:** **отдельный `BuildableItem`**. ItemData — для вещей в инвентаре (модули, расходники, ключи). `BuildableItem` — для вещей "прибитых" к кораблю.

**Но** крафт `BuildableItem` идёт через `RecipeData` (как сейчас), который на output даёт `BuildableItem` (не `ItemData`). Это требует **расширения `RecipeData`**: добавить поле `buildableItem` рядом с `outputs[]`.

### 6.3 Кто может строить — owner check

По канону `ShipOwnershipRequirement` (см. `docs/Ships/Key-subsystem/`) — только владелец корабля (по `KeyRodInstance`) может строить.

```csharp
// На сервере в ShipBuildServer.RequestBuildRpc:
if (!KeyRodInstanceWorld.IsOwnerOfShip(clientId, this.NetworkObjectId))
{
    NotifyClientError(clientId, "Не ваш корабль");
    return;
}
```

**Anti-cheat:** сервер валидирует snap target (нельзя строить на чужом корабле по ошибке).

### 6.4 Multi-crew

Что если на корабле 3 игрока (co-piloting, по `GDD_10 §6.2`)? Кто может строить?

**Варианты:**
- **Только captain** (по `PilotSeatType.Captain`) — простой, но жёсткий.
- **Любой пилот** — мягкий, но может создать хаос.
- **По согласованию** — сложно.

**Рекомендация:** **только owner** (по ключу), независимо от crew. Multi-crew co-pilot не строит. Это **упрощает** и согласуется с ownership моделью.

### 6.5 Удаление построенного

**По умолчанию:** owner может удалить (в build mode → клик на элемент → "Удалить"). Сервер проверяет `IsOwnerOfShip`.

**Результат:** `BuildVisualApplier` destroy'ит visualPrefab, `NetworkList` удаляет запись, JSON persistence обновляется.

**Refund:** можно вернуть `BuildableItem` в инвентарь (как salvage). Но это **отдельная фича**, не для MVP.

---

## 7. Что мы НЕ делаем (явные out-of-scope для MVP)

| Что | Почему не делаем |
|---|---|
| **Грид-система** (как Cosmoteer) | Усложняет ВСЁ — нужна валидация клеток, пересчёт физики, сложный UI. Не для MVP. |
| **Физические эффекты** (mass, drag, центр масс) | Сломает физический баланс корабля. Только визуал — `rb.mass` НЕ меняется. |
| **Gameplay-бонусы** (cargo +N, weapons +N) | Это уже **модули** (L1). Строительство — декорация, не progression. |
| **Движущиеся части** (двери, люки) | Это **модули** (L1) — `ShipModule.visualPrefab` с Animator внутри. |
| **Звуки / партиклы** | Дизайнер может добавить в `visualPrefab`. Наш код — тишина. |
| **Multi-crew build** | Только owner. Упрощает. |
| **Undo/redo** | `Ctrl+Z` — большой UI effort. Можно потом. |
| **Build recipes / skill tree** | "Строить может только инженер 3 уровня" — progression. Не для MVP. |
| **Visual damage** | Не ломается, не горит. |
| **Server-side physics** | Построенная стенка — **не collider** для других кораблей. См. §6 — это декорация. |

**Главный принцип:** buildable = визуальное "украшение" корабля, как декали на корпусе. Не физика, не gameplay.

---

## 8. Caveats и риски (build mode специфичные)

### 8.1 Ghost preview — производительность

**Проблема:** ghost preview обновляется каждый кадр (следует за мышью). Если у него сложный меш (LOD, shadows, materials) — может тормозить.

**Решение:**
- Ghost preview = **отдельный упрощённый prefab** (`ghostPrefab` в `BuildableItem`).
- Или тот же `visualPrefab`, но с `MeshRenderer.shadowCastingMode = Off` + материал с `SurfaceType = Transparent` + `alpha = 0.5`.
- `LODGroup` на ghost preview — none (простой меш).

### 8.2 Snap target под курсором — UI overlay

**Проблема:** игрок не видит, к чему именно snap. UI overlay ("краткая подсказка: 'Привязано к: Стена_3'") помогает.

**Решение:** `BuildGhostApplier` держит TextMesh / World-space Canvas с подсказкой. `OnHoverSnapTarget(snapTargetId)` event → UI реагирует.

### 8.3 Строительство на летящем корабле

**Проблема:** корабль двигается → snap targets ездят → ghost preview "прыгает". Если корабль поворачивает — всё ломается.

**Решение:** **строительство ТОЛЬКО когда корабль docked** (как module install). `BuildZone` активна только если `ShipController.IsDocked == true`. Это согласуется с `ShipModuleServer` (тоже требует docked).

**Anti-frustration:** tooltip "Постройка доступна только в доке".

### 8.4 Строительство в режиме пилотирования

**Проблема:** игрок сидит в PilotSeat → не может ходить по кораблю → не может строить.

**Решение:** build mode работает **из PilotSeat** (без выхода). PlayerInputReader разделяет "строительные" клавиши (B, R, click) от "пилотских" (W/A/S/D, Q/E). Это требует расширения `PlayerInputReader` — **маленький риск**.

**Альтернатива:** игрок должен выйти из PilotSeat чтобы строить. Проще, но frustating (после каждого элемента — залезать обратно).

### 8.5 Построенный элемент пересекается с collider'ом другого корабля

**Проблема:** я строю стенку, но она пересекает пол другого игрока на пирсе.

**Решение:** мы **не используем colliders** на buildable visualPrefab (как и для `ShipModule.visualPrefab`). Все построенные элементы — чистая декорация. Игнорируем коллизии.

### 8.6 Persistence между сессиями — order matters

**Проблема:** если сохранять `BuildElementDto` с `snapTargetId = previousElementId`, то при загрузке порядок важен: сначала parent, потом child.

**Решение:** `JsonBuildRepository.Save(List<BuildElement>)` сортирует по `snapTargetId == 0` (root) первыми. `Load()` читает в том же порядке.

**Реализация:** `OrderBy(e => e.snapTargetId == 0 ? 0 : 1)` (root first).

### 8.7 Что если сервер забыл owner'а

**Сценарий:** у корабля нет `KeyRodInstance` (например, NPC-ship). Может ли NPC-игрок строить на нём?

**Решение:** **нет**. Только player-owned корабли. NPC-корабли — `BuildWorld` для них пуст (или buildable objects hardcoded, как сейчас в префабе).

### 8.8 Много построенных элементов — производительность

**Проблема:** 100 стенок + 50 окон + 20 флагов = 170 GameObject'ов. Может быть медленно.

**Решение:**
- `BuildVisualApplier` использует **object pool** (как `ShipCargoVisual`).
- Static batching для одинаковых элементов.
- LOD: на расстоянии >50м скрывать мелкие детали (окна, фонарики).
- Hard limit: max 200 элементов на корабль.

---

## 9. Roadmap зависимостей (что нужно ДО этого)

### 9.1 Hard dependencies (блокируют)

| Что | Почему | Статус |
|---|---|---|
| **`MyShipsTab` (T-KEY08)** | UI-хаб "мои корабли" — естественное место для кнопки "Build" | ⏳ В roadmap |
| **`ShipModuleServer` extension** | Если хотим ещё и модули в build mode UI | ✅ Есть (2026-07) |
| **Customisation client-state pattern** | Паттерн для `BuildClientState` | ✅ Есть |

### 9.2 Soft dependencies (упростят, но не блокируют)

| Что | Что даст |
|---|---|
| **L1 модулей (см. `01_MODULE_VISUAL_WITHOUT_BONES.md`)** | Параллельный applier — паттерн для `BuildVisualApplier` |
| **L2 paint colors** | После строительства можно ещё и покрасить корпус — синергия |
| **`CustomisationWindow` sub-tab** | Расширяется sub-tab "Строение" |

### 9.3 Ничего не блокирует — start anytime после MyShipsTab

То есть: L1 модулей → MyShipsTab → L6 строительство.

---

## 10. Трудоёмкость (конкретные оценки)

### 10.1 MVP — "build mode для одного игрока на своём корабле"

| Тикет | Что | LOC | Дни |
|---|---|---|---|
| T-BLD-01 | `BuildableItem` SO + `BuildItemRegistry` | ~250 | 2 |
| T-BLD-02 | `ShipBuildServer` + `ShipBuildWorld` + RPC install/remove | ~700 | 4 |
| T-BLD-03 | `BuildVisualApplier` (по канону `ShipModuleVisualApplier`) | ~250 | 2 |
| T-BLD-04 | `BuildZone` + interaction (B-key) | ~150 | 1 |
| T-BLD-05 | `BuildGhostApplier` + SnapResolver (snap to root + element) | ~600 | 4 |
| T-BLD-06 | `BuildModeWindow` UI Toolkit (палитра + кнопки) | ~400 | 3 |
| T-BLD-07 | `BuildClientState` + events | ~250 | 2 |
| T-BLD-08 | `JsonBuildRepository` + persistence | ~250 | 2 |
| T-BLD-09 | 5 buildable items: стенка, пол, крыша, окно, флаг (assets) | — | 2 |
| T-BLD-10 | Recipe extension: крафт стенки из железа | ~100 | 1 |
| T-BLD-11 | Integration tests + Play Mode verification | — | 2 |
| **Итого MVP** | | **~3000** | **~25 дней (5 нед)** |

### 10.2 Сложные фичи (по запросу)

| Фича | Дни |
|---|---|
| Snap-to-hull-socket (предзаданные точки) | +3 |
| Snap-to-face (грань элемента) | +5 |
| Rotation 90° + free | +2 |
| Undo/redo (последние 10 действий) | +5 |
| Multi-crew build (captain only) | +3 |
| Visual variety (5-10 buildable items) | +3-7 |
| Salvage (вернуть в инвентарь при remove) | +3 |
| Animated decorations (вращающиеся фонарики) | +5 |
| **Total с фичами** | **~55 дней (~11 нед)** |

### 10.3 Industrial references — трудоёмкость у других

| Игра | Что делали | Время на систему |
|---|---|---|
| **Fortnite Creative** (Epic) | Полноценный sandbox с logic gates | Годы, ~50+ разработчиков |
| **Sea of Thieves** (Rare) | Outfit system (hull/sails/trim color) | ~6-12 мес, небольшая команда |
| **Cosmoteer** (Walternate Games) | Block-by-block ship building | Solo dev, ~5 лет |
| **MechWarrior Online** (Piranha Games) | Mech loadout с hardpoints | ~2-3 года |

**Наш ~5 недель MVP** — адекватно для варианта A (player expression, visual only).

---

## 11. Сводная карта — что копируем, что новое, что не нужно

| Компонент | Что | Откуда | Статус |
|---|---|---|---|
| **Inventory item storage** | Хранение buildable items в инвентаре игрока | `InventoryWorld` + `ItemRegistry` | ✅ Копируем (через `BuildableItem` reference в ItemData) |
| **Crafting integration** | Рецепт "собрать стенку" | `RecipeData` extension | ✅ Расширяем |
| **Per-ship server state** | "Что построено на этом корабле" | `KeyRodInstanceWorld` pattern | ✅ Копируем (`ShipBuildWorld` singleton) |
| **Replicated state + RPC** | Install / remove / rotate | `ShipModuleServer.RequestInstallModuleRpc` | ✅ Копируем |
| **Client-side projection** | UI + ghost preview state | `CustomisationClientState` | ✅ Копируем |
| **Visual application** | Spawn / destroy visualPrefab | `ShipModuleVisualApplier` (L1) | ✅ Копируем (отдельный sibling) |
| **JSON persistence** | `build_<shipInstanceId>.json` | `JsonKeyRodInstanceRepository` | ✅ Копируем |
| **Owner validation** | "Может ли этот игрок строить" | `ShipOwnershipRequirement` | ✅ Копируем |
| **BuildZone trigger** | "press B to enter build mode" | `OuterCommZone` | ✅ Копируем (меньше радиус, 5m) |
| **Snap resolution** | Найти snap target от cursor | — | 🆕 Новое (~300 LOC) |
| **Ghost preview** | Полупрозрачный preview следует за мышью | — | 🆕 Новое (~250 LOC) |
| **Build mode UI** | UI Toolkit окно | `RepairManagerWindow` | ✅ Копируем (паттерн известен) |
| **3D cursor** | Custom cursor в 3D | — | 🆕 Новое (если грид) / ⚠️ Можно без него |
| **Physics-aware placement** | Snap не проходит сквозь стены | — | ❌ Не нужно (visual only) |
| **Mass rebalance** | Mass центр / drag | — | ❌ Не нужно (visual only) |
| **Gameplay bonuses** | Cargo+ / Weapons+ | — | ❌ Не нужно (модули делают это) |

**~80% паттернов у нас есть. ~20% — действительно новый код (snap + ghost + UI).**

---

## 12. Open questions (на усмотрение пользователя)

1. **Player expression (A) или functional building (B)?** По умолчанию — A. Если хотите B — это другой scope (см. §3.1).
2. **Где живёт buildable — `ItemData` (общий) или отдельный `BuildableItem`?** По умолчанию — отдельный, потому что stackable semantics другие. Но можно расширить `ItemData`.
3. **Buildable крафтится или покупается?** По умолчанию — крафтится через `RecipeData` (бесплатные стенки = мусор, металл = ценность). Можно купить у NPC.
4. **Строительство в PilotSeat или выход обязателен?** По умолчанию — в PilotSeat (не выходить). Требует расширения `PlayerInputReader`.
5. **Multi-crew — может ли co-pilot строить?** По умолчанию — нет (только owner). Упрощает.
6. **Удалять и возвращать в инвентарь (salvage)?** По умолчанию — нет, исчезает. Salvage — отдельная фича.
7. **Decoration animation (вращающиеся фонари, машущие флаги)?** По умолчанию — нет. Дизайнер может добавить Animator в prefab.
8. **Максимум построенных элементов на корабль?** По умолчанию — 200. Защита от спама.
9. **Должны ли другие игроки видеть мои постройки?** По умолчанию — да (replicated state). Иначе теряется смысл social.
10. **Какие именно типы "штук" в MVP?** По умолчанию — стенка, пол, крыша, окно, флаг (5 типов, ~10 вариаций). Расширяется контентом.

---

## 13. Связанные документы

| Документ | Что показывает |
|---|---|
| `../00_SUMMARY.md` | Общая сводка кастомизации L0-L6 |
| `../01_MODULE_VISUAL_WITHOUT_BONES.md` | L1 модулей — applier как образец |
| `../../Ships/00_COMPOSITE_SHIP_SUMMARY.md` | Иерархия корабля (root + children) |
| `../../Ships/Modul_system/01_ARCHITECTURE.md` | ShipModuleServer + RepairManagerWindow — образец серверного хаба |
| `../../Ships/Key-subsystem/00_OVERVIEW.md` | KeyRodInstanceWorld + ownership pattern |
| `../../Crafting_system/00_OVERVIEW.md` | CraftingWorld + RecipeData + CraftingStation |
| `../../Docking_stations/02_V2_ARCHITECTURE.md` | OuterCommZone + DockStationController + DockingPadTriggerBox |
| `../../Character/Customisation/00_OVERVIEW.md` | CustomisationClientState + persistence pattern |
| `../../Character/EquipmentVisual/00_DESIGN.md` | EquipmentWorld + Replicated state pattern |
| `Assets/_Project/Scripts/Ship/ShipModuleServer.cs` | Готовый NetworkBehaviour с RPC — копируем для ShipBuildServer |
| `Assets/_Project/Scripts/Crafting/CraftingStation.cs` | Триггерная зона + NetworkBehaviour — копируем для BuildZone |
| `Assets/_Project/Scripts/Cargo/ShipCargoVisual.cs` | Pool-driven визуал из replicated state — копируем для BuildVisualApplier |
| `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` | Snapshot-driven applier — копируем для BuildClientState |
| `project-c-composite-object-architecture` skill | Marker pattern для build elements (build-to-ship reference) |