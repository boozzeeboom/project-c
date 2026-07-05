# Визуал модулей корабля — разбор крепления без костей

> **Дата:** 2026-07-04
> **Контекст:** L1 из `00_SUMMARY.md` — модуль = 3D-меш, появляющийся при установке. У корабля нет humanoid-скелета (нет `Animator` на root, нет `HumanBodyBones`).
> **Вопрос:** как крепить `visualPrefab`, если в `ItemData`-паттерне для этого используется `HumanBodyBones`?
> **Читать вместе с:** `00_SUMMARY.md` (L1), `Assets/_Project/Scripts/Equipment/Visual/EquipSlotToBone.cs` (образец "с костями")

---

## TL;DR — короткий ответ

**"Без костей" у нас уже всё есть — это `ModuleSlot.transform`.** `ModuleSlot` — это MonoBehaviour на child GameObject в иерархии корабля (см. `00_COMPOSITE_SHIP_SUMMARY.md` §"что такое составной корабль"). Этот `Transform` — и есть "кость" корабля. Не нужны ни `Animator`, ни `HumanBodyBones`, ни `EquipSlotToBone` маппинг.

`visualPrefab` парентится напрямую к `slot.transform`. Это **проще**, чем для персонажа, и в нём **меньше движущихся частей**. См. §3.

---

## 1. Что такое "кость" и почему корабль без неё обходится

### 1.1 Что делает кость в Unity

Кость (bone) в `Animator`-based skeletal setup — это `Transform`, у которого есть:

| Свойство | Что даёт |
|---|---|
| `Transform.localPosition/Rotation/Scale` | Точка крепления в локальных координатах родителя |
| Иерархия parent-child | Наследование трансформации (поворот плеча → поворачивает предплечье) |
| `Animator.GetBoneTransform(HumanBodyBones)` | Стандартизированный lookup из 54 имён для humanoid |
| Skinning weights на `SkinnedMeshRenderer` | Деформация меша при движении кости |

**Ключевое:** для крепления **визуала (одежды, оружия)** кость даёт **точку в пространстве**, к которой parent'ится `visualPrefab`. Skinning и иерархия — это **побочные** функции, нужные для **самого скелета**, не для крепления.

### 1.2 Что у корабля вместо костей

| Аналог для корабля | Что это | Файл |
|---|---|---|
| **Точка крепления** | `ModuleSlot` MonoBehaviour на child GO | `Assets/_Project/Scripts/Ship/ModuleSlot.cs` |
| **Иерархия** | Уже есть — `Ship_Root → Engine_Left → ModuleSlot` (см. `00_COMPOSITE_SHIP_SUMMARY.md`) | — |
| **Lookup** | `slot.gameObject.name` (string) или `slot.GetInstanceID()` | `ShipModuleServer.cs` уже ищет по `gameObject.name` |
| **Skinning** | Не нужна — у корабля меш не деформируется | — |

**`ModuleSlot` — это и есть "кость" корабля.** Один `Transform`, привязанный к конкретному месту иерархии (например, `Engine_Left.transform`), с уникальным именем (`"Engine_Left"`), со своим `slotType` для валидации.

### 1.3 Визуально

```
Персонаж (с костями):              Корабль (без костей):

NetworkPlayer.prefab               Ship_Root.prefab
└── Visual_Model                    ├── Engine_Left ← ModuleSlot ("якорь")
    ├── Hips                        │   └── (сюда парентится visualPrefab)
    │   ├── Spine                   ├── Engine_Right ← ModuleSlot
    │   │   ├── RightHand           ├── PilotSeat ← PilotSeatController
    │   │   │   └── Sword.prefab    │   └── (визуал не нужен — это триггер)
    │   │   └── Head                ├── Door ← DoorController
    │   │       └── Helmet.prefab   │   └── (визуал — сама дверь, не visualPrefab)
    │   └── ...                     └── (другие слоты/части)
```

**Для персонажа:** `Helmet` parent'ится к кости `Head`, которая в свою очередь дочерняя от `Spine`, которая от `Hips`. Цепочка наследования трансформации.

**Для корабля:** `Engine_Meziy_Visual.prefab` parent'ится к `Engine_Left.transform`, который дочерний от `Ship_Root`. Цепочка короче (1 уровень), но **тот же самый parent-child parent**.

**Разница в НЕ-stuffing `visualPrefab` к кости:** ровно ноль. Это один и тот же Unity API: `transform.SetParent(parent)`.

---

## 2. Что в `ItemData` ИМЕННО переиспользуем, а что НЕТ

### 2.1 Переиспользуем напрямую

| `ItemData` поле | Для корабля |
|---|---|
| `visualPrefab` (GameObject) | ✅ То же. Префаб меша модуля. |
| `attachPositionOffset` (Vector3) | ✅ То же. Offset от "кости" (= `ModuleSlot.transform`) в local space. |
| `attachRotationOffset` (Vector3) | ✅ То же. Euler degrees. |
| `attachScale` (Vector3) | ✅ То же. Если меш импортирован в неправильном масштабе. |
| `[Header("Visual")]` group | ✅ То же. |

### 2.2 НЕ переиспользуем (специфика корабля)

| `ItemData` поле | Что вместо |
|---|---|
| `attachBoneOverride` (HumanBodyBones) | **Удалить.** Корабль не humanoid. |
| `_shopDatabase` lookup для `EquipSlot` enum | **Удалить.** Слоты — это MonoBehaviour, не enum. |
| `EquipSlotToBone.TryGetBoneTransform(slot, animator, out bone)` | **Удалить.** У нас нет Animator на root. |
| `CharacterEquipmentVisualApplier` (336 строк, сложный diff с подсписками слотов) | **Заменить на `ShipModuleVisualApplier`** (~80-100 строк, проще). |

### 2.3 Что ДОБАВЛЯЕМ (специфика корабля)

| Поле | Зачем |
|---|---|
| `visualSocketPath` (опц., string) | Если `ModuleSlot` имеет дочерние сокеты (например, `Engine_Left/Socket_A`), можно указать путь от слота. Default = сам слот (root). |
| `attachAxis` (опц., enum {Local, WorldUp, ShipForward}) | Специально для модулей вроде `meziy nozzle`, которые должны быть ориентированы вдоль вектора движения корабля, а не локального "вверха" слота. |
| `colliderMode` (опц., enum {None = визуал only, Trigger = для raycast only, Solid = влияет на физику}) | `ItemData` всегда выключает colliders (`col.enabled = false` в applier). Для корабля у нас нет эквивалента — дизайнер сам решает (см. §6). |

---

## 3. Как работает крепление без костей — пошагово

### 3.1 Базовая версия (минимум, ~80 строк)

**Сценарий:** установил модуль `MODULE_YAW_ENH` в слот `Engine_Left` (тип `Propulsion`). На `Engine_Left` уже висит `ModuleSlot` MonoBehaviour с `slotType = Propulsion`. У `MODULE_YAW_ENH` есть `visualPrefab = Engine_Meziy.prefab` (меш сопла).

```csharp
public class ShipModuleVisualApplier : NetworkBehaviour
{
    [SerializeField] private ShipModuleManager _manager;

    // slot.gameObject.name → spawned visual
    private readonly Dictionary<string, GameObject> _spawned = new();

    public override void OnNetworkSpawn()
    {
        // Подписка на event от ShipModuleServer (уже есть, статический)
        ShipModuleServer.OnModuleChanged += OnModuleChanged;
        // Применить текущее состояние (если мы late-join клиент)
        ApplyAllFromManager();
    }

    public override void OnNetworkDespawn()
    {
        ShipModuleServer.OnModuleChanged -= OnModuleChanged;
        DestroyAllVisuals();
    }

    private void OnModuleChanged(ulong shipNetId)
    {
        if (shipNetId != NetworkObjectId) return;
        ApplyAllFromManager();
    }

    private void ApplyAllFromManager()
    {
        if (_manager == null) return;
        foreach (var slot in _manager.slots)
        {
            if (slot == null) continue;
            var key = slot.gameObject.name;
            if (slot.isOccupied && slot.installedModule.visualPrefab != null)
            {
                SpawnOrReplaceVisual(slot, slot.installedModule);
                _spawned[key] = _spawned[key]; // tracked
            }
            else
            {
                DestroyVisual(key);
            }
        }
    }

    private void SpawnOrReplaceVisual(ModuleSlot slot, ShipModule module)
    {
        var key = slot.gameObject.name;
        // Если уже есть visual — destroy (модуль мог смениться)
        if (_spawned.TryGetValue(key, out var existing) && existing != null)
            Destroy(existing);

        // === САМО КРЕПЛЕНИЕ ===
        var go = Instantiate(module.visualPrefab, slot.transform);
        go.transform.localPosition = module.attachPositionOffset;
        go.transform.localEulerAngles = module.attachRotationOffset;
        go.transform.localScale = module.attachScale;
        // === КОНЕЦ КРЕПЛЕНИЯ ===

        // Опционально: отключить коллайдеры на визуале (см. §6)
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        _spawned[key] = go;
    }

    private void DestroyVisual(string key)
    {
        if (_spawned.TryGetValue(key, out var go) && go != null)
            Destroy(go);
        _spawned.Remove(key);
    }

    private void DestroyAllVisuals()
    {
        foreach (var kv in _spawned)
            if (kv.Value != null) Destroy(kv.Value);
        _spawned.Clear();
    }
}
```

**Это ВСЁ.** Никакого `Animator`. Никакого `HumanBodyBones`. Никакого `EquipSlotToBone`. Просто `Instantiate(prefab, parent)`.

### 3.2 Что даёт parent-child в Unity автоматически

После `Instantiate(prefab, slot.transform)` + `localPosition = offset`:

| Что работает само | Почему |
|---|---|
| Корабль двигается → меш модуля едет с ним | Дочерний Transform наследует parent transform |
| Корабль вращается (pitch/yaw/roll) → меш вращается | Наследование rotation через parent chain |
| Корабль масштабируется (L3 proportions, будущее) → меш тоже | Наследование scale (но см. §5 caveat) |
| `slot.transform` едет с кораблём | `ModuleSlot` — child `Ship_Root`, наследует все transform |

**Это и есть та "магия", которую делает кость в скелете персонажа.** Только в нашем случае parent — это `ModuleSlot.transform` (один уровень от root), а не `animator.GetBoneTransform(HumanBodyBones.RightHand)` (глубоко в skeleton).

### 3.3 Зачем тогда вообще нужны кости у персонажа?

Кости нужны не для крепления визуала, а для **самого скелета** — меш персонажа деформируется через skinning weights. У нас на корабле меш **не деформируется** (это статичная жестянка), поэтому скелет не нужен.

---

## 4. Сложные случаи и как их решать БЕЗ костей

### 4.1 Случай: модуль с подвижными частями (мезий сопло с анимацией)

**Пример:** `MODULE_MEZIY_PITCH` — при активации сопло "оживает" (свечение + частицы).

**Проблема:** как сделать "подвижность" без костей?

**Решение:** используем **вложенные пустышки** внутри `visualPrefab`:

```
Engine_Meziy_Visual.prefab (root)
├── Mesh (статичная оболочка сопла)
├── Nozzle (Transform) ← анимация покачивания через скрипт
├── ParticleSystem (VFX Graph)
└── Light (точечный источник света, мигает при активации)
```

Анимация делается либо **внутри prefab'а** (Animator-контроллер на root этого prefab, отдельный от корабля), либо **извне** (новый компонент `ModuleActivationAnimator`, который дёргает `Nozzle.Rotate(...)` или `Light.intensity = ...`).

**Сравнение с персонажем:** у персонажа аналогично — есть `Animator` на root, и state-машина переключает state. Но у персонажа animator управляет ВСЕМИ костями сразу (root motion + states). У модуля animator на корне своего prefab'а — изолированный, никого не ломает.

### 4.2 Случай: модуль, привязанный к forward-вектору корабля (а не к "верху" слота)

**Пример:** `MODULE_VEIL` (под-завесный спуск) — какой-то "присоска", которая должна смотреть вниз (в сторону земли), а не в localUp слота.

**Решение:** поле `attachAxis` (enum) в `ShipModule`:

```csharp
public enum ModuleAttachAxis { Slot, ShipForward, WorldUp, ShipDown }
```

В applier:
```csharp
Quaternion GetAttachmentRotation(ModuleSlot slot, ShipModule module)
{
    var baseRot = Quaternion.Euler(module.attachRotationOffset);
    return module.attachAxis switch
    {
        ModuleAttachAxis.Slot        => baseRot,
        ModuleAttachAxis.ShipForward => baseRot * Quaternion.LookRotation(transform.forward), // root forward
        ModuleAttachAxis.ShipDown    => baseRot * Quaternion.LookRotation(-transform.up),
        ModuleAttachAxis.WorldUp     => baseRot * Quaternion.LookRotation(Vector3.up),
        _ => baseRot
    };
}
```

**Сравнение с персонажем:** у персонажа это решается выбором **другой кости** (например, `RightHand` vs `LeftHand`). У корабля — выбором **другого reference vector**. Разные механизмы, тот же результат.

### 4.3 Случай: модуль с симметрией (лево/право)

**Пример:** модуль `Engine_Standard.prefab` имеет разный mesh для левого и правого двигателя.

**Решение:** используем `attachScale.x = -1f` для зеркалирования. Это **уже работает** (поле есть в `ItemData`):

```csharp
// В ApplyAllFromManager:
if (slot.name.Contains("Right"))  // по соглашению имён
    go.transform.localScale = new Vector3(
        -module.attachScale.x,
        module.attachScale.y,
        module.attachScale.z);
```

**Сравнение с персонажем:** у персонажа это делается через разные `EquipSlot` (WeaponMain = right, WeaponOff = left), и в `ItemData` у меча может быть своё зеркало. У корабля — единый prefab + флип по оси X через `attachScale`.

### 4.4 Случай: модуль с произвольным socket'ом внутри слота

**Пример:** у слота `Engine_Left` сложная иерархия:

```
Engine_Left
├── ModuleSlot (на root, "якорь")
├── Socket_A
│   └── NozzleCenter
├── Socket_B
│   └── NozzleLeft
└── Socket_C
└── NozzleRight
```

Дизайнер хочет, чтобы визуал спавнился в `Socket_B`, а не в `Engine_Left` напрямую.

**Решение:** `visualSocketPath` (string) в `ShipModule`:

```csharp
public string visualSocketPath = ""; // "" = сам слот; "Socket_B" = дочерний GO
```

В applier:
```csharp
Transform ResolveSocket(ModuleSlot slot, ShipModule module)
{
    var parent = slot.transform;
    if (!string.IsNullOrEmpty(module.visualSocketPath))
    {
        var socket = parent.Find(module.visualSocketPath);
        if (socket != null) parent = socket;
        // Если не нашли — fallback на сам слот (анти-restrictive)
    }
    return parent;
}
```

**Сравнение с персонажем:** у персонажа нет точного аналога. Ближайшее — `attachBoneOverride` (когда default маппинг не подходит). У корабля — явный путь к дочернему socket. **Более мощно и читаемо**, чем humanoid override.

### 4.5 Случай: модуль заменяет весь меш слота (а не добавляет)

**Пример:** вместо стандартного `Engine_Standard.prefab` ставим `Engine_Meziy_Heavy.prefab` — он больше и перекрывает весь engine mount.

**Решение:** **то же, что и для добавляющего модуля**. `Instantiate(prefab, slot.transform)` — prefab может перекрывать всё, что угодно. Никакой разницы.

**Сравнение с персонажем:** у персонажа если надеть тяжёлую броню, она тоже может перекрывать руки или ноги (через skinned mesh + blend shapes). Это решается **на уровне mesh setup** в Blender, не на уровне кода.

---

## 5. Caveats и грабли (НЕ-костные специфичные)

### 5.1 Масштабирование: `attachScale` × parent scale = финальный размер

Если в будущем появится L3 (proportions, `transform.localScale = (1.1, 1.0, 0.9)` на root корабля), то **visualPrefab модуля тоже растянется/сожмётся** автоматически (наследование scale).

**Грабля:** если модуль — точная копия `Engine_Standard`, а `localScale` у корня `(0.5, 0.5, 0.5)`, то сопло станет в 2 раза меньше. Обычно это **ОК** — пропорции корабля сохраняются. Но если дизайнер хочет "несжимаемый" модуль (например, иллюминатор круглый независимо от того, насколько сжат корпус) — это уже **отдельная задача** (заводить отдельный `unscaledParent` через `DontDestroyOnLoad` и т.п.). Для MVP — **игнорируем**.

### 5.2 `rigidbody.mass` сбрасывается при добавлении child в Edit Mode

Это **уже задокументированная** проблема (см. `project-c-composite-object-architecture` skill §"Rigidbody mass reset"). Если при добавлении `visualPrefab` к `slot.transform` в Edit Mode у root корабля слетит `Rigidbody.mass` → на runtime `ShipController.Awake()` re-apply'ит через `ApplyShipClass()`, но **в сцене** будет записано неверное значение. Применять тот же фикс: после добавления → `rb.mass = classMass; EditorUtility.SetDirty(rb); EditorSceneManager.SaveScene`.

### 5.3 Colliders на visualPrefab

В отличие от персонажа (где `ItemData.visualPrefab` ВСЕГДА имеет `col.enabled = false` после spawn), у корабля дизайнер может **захотеть** оставить colliders:

| Случай | Что делать |
|---|---|
| Меш модуля маленький, не влияет на физику | `col.enabled = false` (как у персонажа) |
| Меш модуля = большая надстройка (крыло, башня) | `col.enabled = true` → **влияет на rigidbody.mass** (см. §5.2) |
| Меш модуля = particle system (VFX) | `col.enabled = false` (particles не должны блокировать движение) |

**Решение:** добавить поле `colliderMode` в `ShipModule` (см. §2.3). Default = `None` (как у персонажа, безопасно).

### 5.4 Доступ к `slot` после смены модуля

В applier мы ищем слот по `slot.gameObject.name` (string). Что если дизайнер **переименует** слот в префабе после ship'а?

**Решение:** использовать `slot.GetInstanceID()` или стабильный `slotId` (string, заданный в `ModuleSlot` как поле). Для MVP — `gameObject.name` ОК (дизайнер не переименовывает после ship'а). Для продвинутого — `ModuleSlot.slotId` (поле, см. GDD_10 §4.1).

### 5.5 Prefab scale при импорте

Если дизайнер импортирует `Engine_Meziy.fbx` в Unity с разными unit'ами (m vs cm), префаб может быть в 100 раз больше или меньше ожидаемого. **Решение — стандартное:** `attachScale` в `ShipModule` (поле есть). Дизайнер подбирает. Это **тот же паттерн**, что в `ItemData.attachScale`.

---

## 6. Сводная таблица: "с костями" vs "без костей"

| Аспект | Персонаж (с костями) | Корабль (без костей) |
|---|---|---|
| **Точка крепления** | `animator.GetBoneTransform(HumanBodyBones.X)` | `slot.transform` |
| **Lookup точки** | `EquipSlotToBone.TryGetBoneTransform()` (статический) | `slot.gameObject.name` (просто) или `_manager.FindSlot(path)` |
| **Default маппинг** | enum EquipSlot (13 значений) → HumanBodyBones (54) | **Не нужен** — слоты уже индивидуальны по имени |
| **Override** | `attachBoneOverride` (HumanBodyBones) | `visualSocketPath` (string path к дочернему socket'у) |
| **Offset/Rotation/Scale** | `attachPositionOffset / Rotation / Scale` (Vector3) | ✅ То же |
| **Иерархия при parent'е** | `Helmet → Head → Spine → Hips → ...` | `Visual → ModuleSlot → Ship_Root` |
| **Skinning / деформация** | ✅ Нужна (персонаж двигается, меш деформируется) | ❌ Не нужна (корабль — статичная жестянка) |
| **Подвижность самого модуля** | Через анимацию костей | Через Animator внутри самого prefab'а или внешний `ModuleActivationAnimator` |
| **Сложность applier'а** | ~280 строк (`CharacterEquipmentVisualApplier`) | **~80-100 строк** (`ShipModuleVisualApplier`) |
| **Поля в SO** | 5 (visualPrefab, attachBoneOverride, PositionOffset, RotationOffset, Scale) | **4-5** (visualPrefab, attachSocketPath, PositionOffset, RotationOffset, Scale) — без `attachBoneOverride` |
| **Коллайдеры** | Всегда `col.enabled = false` (ItemData pattern) | Опционально, через `colliderMode` enum |

---

## 7. Когда "без костей" НЕ достаточно

| Случай | Почему без костей не хватит | Что делать |
|---|---|---|
| **Крылья/рули хотят поворачиваться во время полёта** | `slot.transform` статичен | Создать дочерний `Transform` ("крыло-петля") внутри prefab'а, иметь **внутри** prefab Animator со своей state-machine (например, roll → "крыло довёрнуто на 15°") |
| **Пушка на турели хочет следить за целью** | Нужна runtime-икация по target | Дочерний `TurretAim.cs` MonoBehaviour внутри prefab'а, берёт target из `ShipController` (или из сети) |
| **Якорь хочет качаться на ветру** | Внешняя сила | Дочерний `WindSway.cs` MonoBehaviour внутри prefab'а, читает `WindManager.Instance` |
| **Модуль должен следовать за игроком, когда он в PilotSeat** | Хотим анимацию "пилот крутит штурвал" | **За пределами модульной кастомизации** — это уже анимация самого персонажа, не модуля |

**Общий принцип:** если нужна **сложная runtime-логика на самом visualPrefab** — выносим её в **дочерние MonoBehaviour внутри prefab'а**. Если нужна **сложная логика на root корабля** — она пишется в `ShipController` (как сейчас).

---

## 8. Что в `ShipModule` добавить минимально (L1 MVP)

```csharp
// В существующий ShipModule.cs добавить 5 полей:
[Header("Visual (L1 — module visualPrefab)")]
[Tooltip("Префаб меша модуля. При install — спавнится как child ModuleSlot.transform. " +
         "При remove — уничтожается. Если null — модуль без визуала (логика работает).")]
public GameObject visualPrefab;

[Tooltip("Путь к дочернему socket'у внутри слота (например 'Socket_A'). " +
         "Пусто = сам слот. Используется когда у слота сложная иерархия " +
         "(например, Engine_Left с несколькими точками крепления).")]
public string visualSocketPath = "";

[Tooltip("Локальный offset от слота/socket'а к визуалу (local space родителя).")]
public Vector3 attachPositionOffset = Vector3.zero;

[Tooltip("Локальное вращение визуала относительно слота/socket'а (Euler degrees).")]
public Vector3 attachRotationOffset = Vector3.zero;

[Tooltip("Локальный масштаб визуала. (1,1,1) = без изменений. " +
         "Используется если меш импортирован в неправильном масштабе " +
         "(m vs cm), или для зеркалирования (например, x=-1 для правого слота).")]
public Vector3 attachScale = Vector3.one;
```

**Backward-compat:** все поля default = null/zero/one → существующие ~12 SO модулей продолжают работать без изменений. ✅

---

## 9. Когда выбирать какой подход (с костями / без / с socket path)

| Задача | Подход |
|---|---|
| Корабль L1 (module visual) | **Без костей** (§3.1) — `slot.transform` напрямую |
| Персонаж L2 (equipment visual) | **С костями** (как сейчас в `ItemData`) |
| Сложная иерархия в слоте (multiple sockets) | **Без костей + socket path** (§4.4) |
| Модуль с подвижными частями | **Без костей + дочерние компоненты внутри prefab'а** (§4.1) |
| L6 slot composition (add/remove parts) | **Отдельная подсистема**, не относится к этому разбору |

---

## 10. Связанные документы

| Документ | Что показывает |
|---|---|
| `Assets/_Project/Scripts/Equipment/Visual/EquipSlotToBone.cs` | Образец "с костями" — таблица `EquipSlot → HumanBodyBones` |
| `Assets/_Project/Scripts/Core/ItemType.cs` (ItemData) | Поля, которые копируем: visualPrefab + Position/Rotation/Scale offset |
| `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs` | Applier "с костями" для сравнения |
| `Assets/_Project/Scripts/Ship/ModuleSlot.cs` | MonoBehaviour на child GO — "якорь" вместо кости |
| `Assets/_Project/Scripts/Ship/ShipModuleManager.cs` | Список слотов, lookup `FindSlot(name)` |
| `Assets/_Project/Scripts/Ship/ShipModuleServer.cs` | Уже дёргает `OnModuleChanged` event (статический) — applier subscribe |
| `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` | Иерархия корабля (root + children) |
| `00_SUMMARY.md` | Общая сводка кастомизации (L0-L6) |
| `project-c-composite-object-architecture` skill | Паттерн marker + locator для composite ships |