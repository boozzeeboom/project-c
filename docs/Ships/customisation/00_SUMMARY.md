# Ship Customisation — Сводная Аналитика

> **Дата:** 2026-07-04
> **Тип документа:** аналитика + сводка, **БЕЗ детального плана реализации**
> **Назначение:** каталог подходов к кастомизации кораблей, от простого к сложному, применительно к нашему стеку
> **Читать вместе с:** `../CUSTOMIZATION_ANALYSIS.md` (2026-06-29, Equipment Visual focus), `../../Character/Customisation/03_LEVELS_OF_CUSTOMISATION.md` (L1-L5 для персонажа), `../Modul_system/01_ARCHITECTURE.md` (текущая модульная система)

---

## TL;DR — что у нас есть и куда двигаться

| Слой кастомизации | Что это | Готовность сейчас | Куда копать |
|---|---|---|---|
| **A. Статические части префаба** | PilotSeat, Door, Engine_Left/Right в иерархии | ✅ Phase 0-3 done | Базис. Дизайнер собирает в Editor. |
| **B. Модули в слотах (статика)** | `ShipModule SO` + `ModuleSlot.installedModule` | ✅ Phase 4 done | Логика влияния (тяга/yaw/pitch...) работает. |
| **C. Runtime установка модулей** | `ShipModuleServer` + RPC + `RepairManagerWindow` | ✅ **Done (2026-07)** | Установка/снятие в доке. Валидация сервером. |
| **D. Визуал модулей** | Никакого visualPrefab в `ShipModule` | ❌ Не начато | Нужен applier. **Самый дешёвый big-impact.** |
| **E. Визуал частей корабля** | Цвет/паттерн/материал на hull / крыльях | ❌ Не начато | По аналогии с персонажем (L4 coloring). |
| **F. Модификация геометрии** | Слайдеры пропорций (длина/ширина/высота) | ❌ Не начато | По аналогии с `CharacterCustomisationApplier.ApplyProportions` (L3). |
| **G. Свободная композиция** | Игрок добавляет/убирает крылья/пушки в runtime | ❌ Не начато | Самое дорогое. Нужны attachment points + snap + network sync. |
| **H. Полная уникализация** | Кастомная геометрия / текстуры | ❌ Не нужно в MVP | За пределами stage 2.5. |

**Главное архитектурное решение, которое надо принять ДО кода:** ставим ли мы в центр `ShipModule` (текущая ось) или строим параллельную подсистему `ShipCustomisationServer/World/ClientState` (как `EquipmentWorld` для одежды). Короткий ответ — **оставляем `ShipModuleServer`**, расширяем минимально, НЕ плодим второй state. Детали в §3.

---

## 1. Наш стек и что он уже даёт

### 1.1 Что уже реализовано (лето 2026)

| Компонент | Файл | Что делает |
|---|---|---|
| **Composite Ship (Phase 0-3)** | `00_COMPOSITE_SHIP_SUMMARY.md` | Root + дочерние (PilotSeat, Door, Engine_Left, ModuleSlot...). Rigidbody — только на root. |
| **ShipRootReference** | `Assets/_Project/Scripts/Ship/ShipRootReference.cs` | Маркер на каждой части → быстрый доступ к корню. `[DefaultExecutionOrder(-100)]`, кэширует `ShipController/Rigidbody/NetworkObject`. |
| **ShipComponentLocator** | `ShipComponentLocator.cs` | Статический helper: marker → GetComponent → Parent → Children. |
| **ShipModule (SO)** | `ShipModule.cs` | Данные: множители (thrust/yaw/pitch/roll/lift), power, cargo bonuses, совместимость. **Нет visualPrefab** (TODO). |
| **ModuleSlot** | `ModuleSlot.cs` | MonoBehaviour на child GO. `slotType` (Propulsion/Utility/Special), `installedModule` (ссылка на SO). |
| **ShipModuleManager** | `ShipModuleManager.cs` | На root. `slots: List<ModuleSlot>`, валидация (power, class, required/incompatible). |
| **ShipModuleServer (NetworkBehaviour)** | `ShipModuleServer.cs` | ✅ **Сделан.** RPC install/remove, server-authoritative валидация (key ownership + docked + compat). ClientRpc синхронизирует всем. |
| **ModuleShopDatabase** | `ModuleShopDatabase.cs` | Каталог `ModuleShopEntry` (модуль + цена + ресурсы). |
| **ShipModuleCatalog** | `ShipModuleCatalog.cs` | Static lookup по `moduleId`. |
| **RepairManager / Window** | `RepairManager.cs`, `UI/RepairManagerWindow.cs` | NPC в доке → UI Toolkit окно выбора корабля/слота/модуля → RPC. |
| **Cargo visual** | `Cargo/ShipCargoVisual.cs` | ✅ **Готов как образец для подражания.** Subscribe `ShipTelemetryClientState.OnShipStateChanged` → pool-driven визуал ящиков внутри BoxCollider-зоны. Инкрементальный refresh, без Destroy на каждом change. |
| **Character Customisation L1-L4** | `CharacterCustomisationApplier.cs`, `CustomisationClientState.cs` | ✅ **Зрелый паттерн.** Mesh swap (L1) → proportions (L3) → MaterialPropertyBlock colors (L4). |
| **CustomisationSave** (JSON per clientId) | `CharacterCustomisationApplier.cs` `LoadSnapshotFromDisk` | Локальная персистенция через JSON в `Application.persistentDataPath`. |

### 1.2 Чего НЕ хватает (gap-анализ)

| Что | Блокер | Критичность |
|---|---|---|
| `ShipModule.visualPrefab` поля | Дизайнер не может привязать меш к модулю | 🟡 High |
| `ShipModuleVisualApplier` (spawn/destroy prefab при install/remove) | Игрок ставит модуль → визуально ничего не меняется | 🔴 **Critical** |
| Материалы на hull с `_BaseColor` override | Цвет корабля не меняется | 🟡 Mid |
| Slot composition (add/remove parts in runtime) | Только статика в префабе | 🟢 Low (post-MVP) |
| MyShipsTab / customisation window для кораблей (по аналогии с CharacterWindow) | Управление визуалом из UI отсутствует | 🟡 Mid |

---

## 2. Уровни кастомизации — от простого к сложному

**Принцип:** каждый уровень **опциональный**, можно делать независимо. Делать в порядке снизу вверх, проверять compile-clean + Play Mode после каждого.

### L0 — Готовый фундамент (✅ есть)

Дизайнер в Editor собирает префаб: `Ship_Root` с дочерними `Engine_Left`/`Engine_Right`/`PilotSeat`/`Door`, вешает `ModuleSlot` + `ShipRootReference` на каждый слот, кидает `ShipModule_Meziy.asset` в поле `installedModule`. Корабль работает: thrust × meziy множитель, RPC install/remove в доке.

**Что нужно игроку:** ничего — это и так работает.

**Что НЕ даёт:** игрок не видит, как смена модуля меняет корабль визуально (только цифры в HUD колонке K1).

### L1 — Визуал модулей (visualPrefab)

**Что даёт игроку:** установил двигатель — из Engine_Left "вырос" новый мех-корпус. Снял — мех убрался. Видно другим игрокам (через существующий `OnModuleChangedClientRpc`).

**Что нужно:**
1. Добавить 5 полей в `ShipModule` (по аналогии с `ItemData`):
   ```csharp
   [Header("Visual")]
   public GameObject visualPrefab;
   public Vector3 attachPositionOffset = Vector3.zero;
   public Vector3 attachRotationOffset = Vector3.zero;
   public Vector3 attachScale = Vector3.one;
   public Vector3[] attachBoneSlots; // (опционально, для продвинутых)
   ```
2. Создать `ShipModuleVisualApplier` MonoBehaviour на корне корабля:
   - Subscribe → `ShipModuleServer.OnModuleChanged` (static event уже есть!)
   - Diff current vs new state по `slot.gameObject.name`
   - Spawn / destroy `visualPrefab` под соответствующим `ModuleSlot.transform`
   - Отключить все Colliders на visual (он не interactable)
   - Object pool по аналогии с `ShipCargoVisual`
3. Опционально — `attachBoneSlots` для привязки к конкретным child-GO (если у слота сложная иерархия).

**Трудоёмкость:** 3-5 дней (включая дизайнерские меши для 8 существующих модулей).

**Зависимости:** ✅ все уже есть. `ShipModuleServer.OnModuleChanged` уже дёргает `OnModuleChanged?.Invoke(_netObj.NetworkObjectId)` после ClientRpc.

**Промышленные аналоги:**
- **EVE Online** — модули это буквально `Item.visualModel` → parent к hardpoint socket.
- **Star Citizen** — `ShipLoadout.Manifest.Items[].EntityClassName` → spawn at `EntityComponentPort`.
- **Sea of Thieves** — `ShipCustomizationStorageComponent` хранит cosmetic item references.
- Общий паттерн во всех: **data-driven prefab lookup по ключу**.

### L2 — Цвет корабля (MaterialPropertyBlock)

**Что даёт игроку:** слайдер/палитра в UI → покрасить корпус/крылья/парус.

**Что нужно:**
1. Material на hull / крыльях должен иметь URP/Lit `_BaseColor` (если нет — заменить или добавить override).
2. `ShipPaintApplier` MonoBehaviour на root:
   - Inspector: `[SerializeField] Renderer[] _paintTargets` (mesh на hull, wings, deck)
   - Inspector: `[SerializeField] Color[] _paintColors` (по `PaintSlot` enum: Hull, Wing, Sail)
   - Subscribe → `ShipPaintClientState.OnPaintChanged(paintSnapshot)`
3. `ShipPaintClientState` (singleton, как `CustomisationClientState`):
   - `LoadSnapshotFromDisk` → JSON `paint_<clientId>.json` в persistentDataPath (по аналогии с `CustomisationSave`).
   - `SaveSnapshot(snapshot)` — local, не networked (у каждого свой цвет).
4. UI: sub-tab "ВНЕШНОСТЬ" → ColorField + palette picker (по аналогии с CharacterWindow skin RGB).

**Трудоёмкость:** 3-5 дней. **Прямая копипаста с `CharacterCustomisationApplier.ApplyColors`** — паттерн уже обкатан.

**Промышленные аналоги:**
- **Sea of Thieves** — выбор hull/sails/trim color через Outfit UI.
- **ARK** — per-region `MaterialPropertyBlock` override на SkinnedMeshRenderer.
- **No Man's Sky** — `BasePart.ColourPalette` per layer.

**Caveat:** в MP все видят твой цвет? **Нет по умолчанию** — pattern из персонажа local-only persistence. Если нужно shared — выделить в отдельный `NetworkVariable` на `ShipCustomisationServer`. См. §3.

### L3 — Пропорции корпуса (transform.localScale)

**Что даёт игроку:** удлинить/расширить/уменьшить корпус через слайдеры. Дёшево и сердито.

**Что нужно:**
1. `ShipProportionsApplier` MonoBehaviour на root:
   - Inspector: `[SerializeField] Transform _proportionRoot` (например, GO с мешем корпуса внутри ShipRoot)
   - Subscribe → `ShipProportionsClientState.OnProportionsChanged`
2. `ShipProportionsClientState` (mirror `CustomisationClientState`):
   - JSON `proportions_<clientId>.json`: `{length, width, height}` clamps 0.7-1.3.
3. UI: sub-tab "ПРОПОРЦИИ" → 3 слайдера + preview.

**Caveats (критично):**
- `BoxCollider.size` на корне **НЕ** подстраивается автоматически. Нужно отдельно пересчитать в `ShipProportionsApplier`.
- Если применяется во время движения — `rigidbody` collider прыгает. **Применять только когда корабль docked** (по аналогии с module install).
- Если вы делаете _не_ docked — будьте готовы к "прыжкам" физики. По умолчанию — блокируем через `if (!shipController.IsDocked) return;`.
- В MP у каждого игрока свой scale **визуально**? Или общий? По умолчанию **local** (как у персонажа). Если хотите общий — см. §3.

**Трудоёмкость:** 2-3 дня (мелкая копипаста с `CharacterCustomisationApplier.ApplyProportions`).

**Промышленные аналоги:**
- **Dreadnought** — слайдеры hull length/width/height с превью.
- **Cosmoteer** — block-based, но аналогия та же: per-segment scale.
- **Frostpunk** — нет, у них 2D.

### L4 — Decals / Emblems / Numbers (texture overlay)

**Что даёт игроку:** нарисовать герб/эмблему на корпусе или бортовой номер.

**Что нужно:**
1. Decal Projector (URP 17.x имеет `DecalProjector`) на hull.
2. `ShipDecalApplier` MonoBehaviour: `[SerializeField] DecalProjector _decal; Texture _defaultTexture;`
3. UI: file picker (локально — `Application.persistentDataPath` images) или выбор из пресетов.
4. `ShipDecalClientState` (mirror).

**Промышленные аналоги:**
- **EVE Online** — alliance logo на hull.
- **War Thunder** — clan emblem.
- **WoT** — clan emblem на башне.

**Трудоёмкость:** 3-5 дней (включая UI Toolkit file picker). **Можно отложить — низкий player value на старте.**

### L5 — Module-specific shader (свечение / эффекты)

**Что даёт игроку:** установленный мезий-модуль — выхлоп сопла светится. Установленный STEALTH — корпус становится матовым/невидимым.

**Что нужно:**
1. Расширить `ShipModule`:
   ```csharp
   [Header("Shader effects (L5)")]
   public string[] materialPropertyOverrides; // ["_EmissionColor", "_BaseColor"]
   public Color emissionColor = Color.black;
   public float emissionIntensity = 0f;
   public string[] enabledShaderKeywords; // ["_EFFECT_MEZIY_ON", "_EFFECT_STEALTH"]
   ```
2. В `ShipModuleVisualApplier` после spawn — добавить post-step `ApplyMaterialOverrides(visualGo, module)`.
3. Для продвинутых: VFX Graph asset per module (у нас уже есть VFX Graph 17.4).

**Промышленные аналоги:**
- **Warframe** — модуль = mod card, видимые эффекты на корпусе (glow, particle trails).
- **Destiny 2** — shader overlay на каждый слот.

**Трудоёмкость:** 5-10 дней (включая shader work). **Только если у нас уже есть художник по VFX.**

### L6 — Slot Composition (добавление/удаление частей)

**Что даёт игроку:** снять крылья, добавить башню, переставить руль. Полная пересборка.

**Что нужно (огромный объём):**
1. `ShipPartDefinition` SO с attachment points (`AttachSocket[]` с position/rotation/snap rules).
2. `ShipPartRegistry` SO с префабами доступных частей.
3. `ShipPartVisualApplier` — ghost preview, snap, rotation control.
4. `ShipCustomisationServer` для replicated state (это требует нового серверного хаба, см. §3).
5. UI с drag & drop.
6. Валидация: физические пересечения, центры масс (Rigidbody.massDistribution), структурная целостность.
7. Network sync: ownership, placement broadcast, conflict resolution.

**Трудоёмкость:** 6-12 недель. **Не для stage 2.5.** Отдельный roadmap item.

**Промышленные аналоги:**
- **MechWarrior Online** — hardpoint slots с crit slots и weight budget.
- **X4: Foundations** — full station building.
- **Cosmoteer** — block-by-block construction.

---

## 3. Ключевое архитектурное решение: где хранить state кастомизации

**Три варинта, каждый со своими trade-offs.**

### Вариант A — Local-only persistence (как у персонажа сейчас)

```
[UI Window] → ShipXxxApplier.OnValueChanged()
            → ShipXxxClientState.ApplySnapshot(snap)
            → JSON в Application.persistentDataPath/<key>_<clientId>.json
            → Subscribe: ShipXxxClientState.OnXxxUpdated → визуал
```

| ✅ Плюсы | ❌ Минусы |
|---|---|
| Простейшая реализация (1-2 файла на слой) | Другие игроки **не видят** мой цвет/пропорции |
| Уже обкатан на персонаже (L1-L4 done) | Не работает для L1 (module visual) — нужно server sync |
| Не плодит NetworkBehaviour | Нельзя "примерять" перед покупкой |

**Когда выбирать:** L2 (цвет), L3 (пропорции), L4 (decals) — где вид "от первого лица" не критичен.

### Вариант B — Расширить `ShipModuleServer` (минимум)

`ShipModuleServer` уже есть и уже синхронизирует module state. Расширяем:

```
[UI Window] → ShipXxxApplier
            → [Rpc(SendTo.Server)] RequestXxxRpc(...)
            → ShipModuleServer валидирует → пишет в replicated state
            → [Rpc(SendTo.Everyone)] OnXxxChangedClientRpc
            → ShipXxxApplier subscribe → визуал
```

| ✅ Плюсы | ❌ Минусы |
|---|---|
| Другие игроки видят | Дополнительная нагрузка на NGO sync |
| Используем существующий серверный хаб | Нужен ship ownership check (кто может менять) |
| Не нужен новый NetworkBehaviour | Увеличивает scope `ShipModuleServer` (God class risk) |

**Когда выбирать:** L1 (module visual), если хотим чтобы другие видели мой корабль.

**Caveat:** server-authoritative state для визуала — **спорно**. Многие игры (Sea of Thieves) делают это **client-local** для визуала, потому что:
- Кастомизация не влияет на gameplay (visual only).
- Снижает bandwidth в MP (10 кораблей × все цвета/пропорции = болтливо).
- Другие игроки видят только если подошли близко (LOD).

### Вариант C — Отдельный `ShipCustomisationServer` (как EquipmentWorld)

Полная копипаста паттерна `EquipmentServer` + `EquipmentWorld` + `EquipmentClientState`:

```
[UI] → ShipCustomisationServer.RequestChangeXxxRpc()
     → ShipCustomisationWorld (replicated state)
     → ShipCustomisationClientState (client projection + events)
     → ShipXxxApplier subscribe → визуал
```

| ✅ Плюсы | ❌ Минусы |
|---|---|
| Чистое разделение (логика/визуал/state) | Больше файлов (4-5 новых) |
| Легко расширять (decals, emblems, etc.) | God class risk, если потом пихать всё подряд |
| Паттерн уже есть в EquipmentVisual | Duplication with ShipModuleServer |

**Когда выбирать:** если в L1+ выясняется, что нужно много разных типов state, и мы хотим один replicated hub. **Преждевременная абстракция** для MVP.

### Рекомендация

| Слой | State strategy |
|---|---|
| **L1 module visualPrefab** | **Вариант B** — расширяем существующий `ShipModuleServer.OnModuleChanged` (event уже есть!). Spawn visual prefab под slot.transform при получении event. |
| **L2 paint colors** | **Вариант A** — local-only JSON, как `CustomisationSave`. Не всем видно — это ОК для stage 2.5. |
| **L3 proportions** | **Вариант A** — local-only JSON. **Применять только при docked** чтобы не плодить physics баги. |
| **L4 decals** | **Вариант A** — local-only JSON. |
| **L5 module shader effects** | **Вариант B** — расширяем `ShipModuleServer.OnModuleChanged` чтобы пушить shader uniforms через event. |
| **L6 slot composition** | **Вариант C** — отдельный серверный хаб (отдельный roadmap). |

**Принцип:** default — local-only (как у персонажа). Только если **другой игрок должен видеть** — replicated через NGO.

---

## 4. Где хранить данные: ScriptableObject vs JSON vs NetworkVariable

| Подход | Где | Когда | Наш опыт |
|---|---|---|---|
| **SO asset** | `Assets/_Project/Data/Modules/*.asset` | Определения модулей, рецепты, эффекты | ✅ `ShipModule`, `ModuleShopEntry` |
| **Scene-placed reference** | `[SerializeField] public ShipModule` | Designer-time настройка слотов | ✅ `ModuleSlot.installedModule` |
| **NetworkVariable** | Внутри `NetworkBehaviour` | Replicated runtime state | ✅ `ShipController._netIsDocked` |
| **Local JSON** | `Application.persistentDataPath` | Per-client persistence | ✅ `CustomisationSave.json` |
| **ItemRegistry asset** | `Assets/_Project/Resources/` | Lookup по ID, persistent items | ✅ используется для ключей |

**Применить для каждого уровня:**

| L | State | Storage |
|---|---|---|
| L1 (module visual) | `ModuleSlot.installedModule` | SO (уже есть, расширить `visualPrefab` полем) |
| L2 (paint) | `ShipPaintClientState.CurrentSnapshot` | JSON per clientId |
| L3 (proportions) | `ShipProportionsClientState.CurrentSnapshot` | JSON per clientId |
| L4 (decal) | `ShipDecalClientState.CurrentTexture` | JSON per clientId (path к PNG) |
| L5 (module shader) | Через тот же `ShipModule.OnModuleChanged` event | не нужен отдельный state |

---

## 5. Референсы из реальных проектов (best practices)

| Игра/проект | Что ценного | Применимо к нам |
|---|---|---|
| **EVE Online** | Hardpoint sockets + item visualModel | L1 (спавн prefab в слот) |
| **Sea of Thieves** | Outfit UI: hull/sails/trim color → MP sync через ShipCustomizationStorageComponent | L2 (color), с ремаркой что мы local-only |
| **Star Citizen** | ShipLoadout.Manifest + EntityComponentPort (XML-based) | L6 (composition), out of scope |
| **Warframe** | Mod cards → визуально на корпусе | L5 (shader effects) |
| **Cosmoteer** | Block-by-block с structural integrity check | L6 (composition) |
| **Dreadnought** | Слайдеры hull dimensions | L3 (proportions) |
| **ARK** | Material per-region override (SkinMaterial) | L2 (color через MPB) |

**Общий паттерн во всех mature проектах:** **data-driven prefab lookup по ключу**. У нас уже это есть через `ShipModule.moduleId` → `ShipModuleCatalog.Find()`. Нужно лишь добавить `visualPrefab` поле.

---

## 6. Что НЕ нужно делать (anti-patterns)

- ❌ **НЕ плодить второй `NetworkBehaviour` для визуала.** Использовать существующий `ShipModuleServer.OnModuleChanged` event для L1.
- ❌ **НЕ делать client-side prediction для визуала.** Не нужен — кастомизация не time-critical. Ждём RPC.
- ❌ **НЕ хранить visualPrefab на root корабля.** Это плохой дизайн: меши дублируются, сложно синхронизировать. **VisualPrefab живёт в SO модуля** (как сейчас `ItemData.visualPrefab`).
- ❌ **НЕ делать slot composition (L6) в stage 2.5.** Это отдельная подсистема, требует physics rebuild + ownership model.
- ❌ **НЕ использовать `MaterialPropertyBlock` на shared materials** без копирования. MPB per-renderer — ОК; на shared instance — нет.
- ❌ **НЕ реплицировать color/proportions через NGO** для stage 2.5. Local JSON хватает. Если в будущем нужен shared — выделить в L6+.
- ❌ **НЕ блокировать gameplay на visual изменениях.** Player может менять визуал когда угодно (не только в доке), кроме L1 (module install — только docked) и L3 (proportions — только docked).

---

## 7. Карта рисков

| Риск | Где | Митигация |
|---|---|---|
| **Rigidbody.mass сбрасывается при добавлении child** | При добавлении visualPrefab к slot в Edit Mode | После добавления visualPrefab → `rb.mass = classMass; col.size = ...; EditorUtility.SetDirty` |
| **VisualPrefab содержит Collider** | Дизайнер забыл убрать collider с меша модуля | В applier — `foreach (var col in spawned.GetComponentsInChildren<Collider>()) col.enabled = false` |
| **JSON не подхватывается при cold start** | CustomisationClientState уже инициализирован, но JSON ещё не прочитан | `OnEnable → LoadSnapshotFromDisk → ApplySnapshot` (паттерн из `CharacterCustomisationApplier`) |
| **Module visual спавнится несколько раз при reconnect** | Player reconnects → NGO respawns ship → applier subscribe ещё раз | Использовать `OnModuleChanged` event + идемпотентный diff (по `slot.gameObject.name`) |
| **BoxCollider.size не подстраивается под proportions** | L3 меняет visualScale, а физический размер остаётся старый | В `ShipProportionsApplier` — отдельно пересчитывать `col.size` через `* proportionFactor` |
| **Пропорции меняются во время полёта** | Корабль в воздухе, scale прыгает | Блокировать `ApplyProportions` пока `!IsDocked` |

---

## 8. Что дальше (next session priorities)

**Рекомендуемый порядок если пользователь скажет "поехали":**

1. **L1 (module visual)** — самый high-impact. 3-5 дней. Делается **поверх существующего `ShipModuleServer` без новых NetworkBehaviour**.
2. **L2 (paint colors)** — копипаста с персонажа. 3-5 дней. Local JSON persistence.
3. **L3 (proportions)** — копипаста. 2-3 дня. Только docked.

L4-L6 — отдельно по запросу.

**Документы, которые нужно создать перед code work:**
- `01_L1_MODULE_VISUAL_DESIGN.md` — конкретный план L1
- `02_L2_PAINT_DESIGN.md` — конкретный план L2 (если пойдёт)
- `03_L3_PROPORTIONS_DESIGN.md` — конкретный план L3 (если пойдёт)
- `04_PERSISTENCE_PATTERN.md` — общий шаблон local JSON persistence (для всех L2-L4)
- `99_CHANGELOG.md` — log прогресса

**Существующий `../CUSTOMIZATION_ANALYSIS.md` (2026-06-29)** остаётся как исторический документ, фокусирующийся на Equipment Visual System. Этот `00_SUMMARY.md` — расширенная версия с учётом того, что **module server уже сделан** (на момент 2026-06-29 его ещё не было).

---

## 9. Open questions (на усмотрение пользователя)

1. **Должны ли другие игроки видеть мой цвет/пропорции?** Если да — нужен replicated state (вариант B/C из §3). Если нет — local JSON (вариант A).
2. **Сколько mesh-вариантов мы хотим для L1?** 1 prefab per module, или 2-3 (random при установке)? Random даёт разнообразие, но усложняет валидацию.
3. **L1 visualPrefab — это полная замена меха в слоте, или дополнительный overlay?** Полная замена проще; overlay даёт более глубокую кастомизацию но усложняет hierarchy.
4. **Применять пропорции (L3) только docked?** По умолчанию да. Но если хотим "прямо на лету" — нужна физическая корректировка collider + проверка не-столкновения.
5. **Persistence — local JSON (как персонаж) или server-side (в TradeWorld как ключи)?** Local проще; server-side позволяет видеть кастомизацию на других клиентах (но из §3 — обычно local хватает).
6. **Какие именно L делать в первом проходе?** По умолчанию L1 → L2 → L3 (high impact, низкая стоимость). L4-L6 — по запросу.

---

## 10. Связанные документы

| Документ | Назначение |
|---|---|
| `../CUSTOMIZATION_ANALYSIS.md` (2026-06-29) | Предыдущий анализ (Equipment Visual focus). Содержит детальный разбор архитектурных решений, но устарел — не учитывает ShipModuleServer (сделан 2026-07). |
| `../../Character/Customisation/03_LEVELS_OF_CUSTOMISATION.md` | L1-L5 для **персонажа**. Прямой паттерн для L1-L4 корабля. |
| `../../Character/Customisation/00_OVERVIEW.md` | Обзор Customisation subsystem персонажа. |
| `../Modul_system/01_ARCHITECTURE.md` | Текущая архитектура модулей (после Phase 1). |
| `../Modul_system/02_REPAIR_MANAGER.md` | UI RepairManager — уже работает. |
| `../00_COMPOSITE_SHIP_SUMMARY.md` | Composite Ship Phase 0-3 (базовая иерархия). |
| `../Cargo/ShipCargoVisual.cs` | Образец для подражания: pool-driven визуал из replicated state. |
| `../../Character/EquipmentVisual/` | Equipment Visual pattern — для inspiration, не 1:1 (slot = enum, не MonoBehaviour). |
| `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` | Готовый код, который надо копировать (L1 mesh swap, L3 proportions, L4 colors). |
| `Assets/_Project/Scripts/Customisation/CustomisationClientState.cs` | Готовый singleton, на который надо ориентироваться. |