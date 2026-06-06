# MetaRequirement — Implementation Guide (Step-by-step)

**Документ:** практическое руководство по добавлению новых MetaRequirement-объектов в сцены.
**Дата:** 2026-06-06
**Связанные документы:**
- `00_OVERVIEW.md` — дизайн
- `RECIPES.md` — примеры конфигураций
- `20_INSPECTOR_REFERENCE.md` — описание полей
- `30_RUNTIME_FLOW.md` — что происходит в рантайме
- `40_TESTING_GUIDE.md` — тестирование

---

## 1. Что нужно для добавления нового "замка" в сцену

Минимальный набор:
1. **ItemData** (ScriptableObject) — что требуется. Должен лежать в `Assets/_Project/Resources/Items/` (НЕ в подпапках).
2. **Pickup-объект(ы)** в сцене — что игрок подбирает (опционально, но обычно нужен для теста).
3. **GameObject с MetaRequirement** — сам "замок" (должен иметь `NetworkObject` + `MetaRequirement`).
4. **GameObject с анимацией/визуалом** — что происходит при разрешении (например, `LockBox`).

Плюс (если ещё нет в сцене):
- `[MetaRequirementRegistry]` GameObject в `BootstrapScene` — server-side NetworkBehaviour.
- `[MetaRequirementToast]` GameObject в `BootstrapScene` — UI-фидбек при отказе.

---

## 2. Шаг за шагом: создание нового "Цветного Сундука"

В качестве примера — новый фиолетовый сундук `LockBox_Purple` с предметом `Key_Purple`.

### 2.1 Создать `ItemData` ключа

**Через Editor:**
1. Project window → правый клик → `Create → Project C → Item Data`
2. Сохранить в `Assets/_Project/Resources/Items/Item_Key_Purple.asset` (НЕ в подпапке)
3. Заполнить поля:
   - `itemName`: `Ключ: Фиолетовый Замок`
   - `itemType`: `Equipment`
   - `description`: `Открывает фиолетовый сундук`
   - `maxStack`: `1`
   - `weightKg`: `0.05`
4. **Опционально**: назначить `icon` (Sprite) — для UI-отображения в TAB-колесе

**Через MCP `execute_code`** (для скриптования):
```csharp
var so = ScriptableObject.CreateInstance<ProjectC.Items.ItemData>();
so.itemName = "Ключ: Фиолетовый Замок";
so.itemType = ProjectC.Items.ItemType.Equipment;
so.description = "Открывает фиолетовый сундук";
so.maxStack = 1;
so.weightKg = 0.05f;
UnityEditor.AssetDatabase.CreateAsset(so, "Assets/_Project/Resources/Items/Item_Key_Purple.asset");
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
```

**⚠️ КРИТИЧНО:** `ItemData` должен лежать в **корне** `Resources/Items/`. Если положить в подпапку
(`Resources/Items/Ship_key/Item_Key_Purple.asset`) — `Resources.LoadAll<ItemData>("Items")` в
`InventoryWorld.RegisterAllItems()` его НЕ подхватит (Unity `Resources.LoadAll` не рекурсивен).
См. R2-SHIP-KEY-001 в `docs/Ships/Key-subsystem/KNOWN_ISSUES.md`.

### 2.2 Создать материал (опционально)

Если хочешь чтобы pickup/блок имели цвет:
1. Project window → правый клик → `Create → Material`
2. Сохранить в `Assets/_Project/MetaRequirement_Test/Materials/` (или в любую другую папку)
3. Shader: `Universal Render Pipeline/Lit`
4. `Base Color`: фиолетовый (например `#A020F0`)
5. `Emission Color`: тот же фиолетовый, intensity 0.2
6. Включить `Enable Emission` keyword (если нужно для bloom)

### 2.3 Создать Pickup-объект в сцене

**Через Editor (ручной способ):**
1. Открыть сцену (например, `Assets/_Project/Scenes/World/WorldScene_0_0.unity`)
2. Hierarchy → правый клик → 3D Object → Sphere
3. Назвать `[Key_Purple_Pickup]`
4. Transform:
   - Position: рядом с будущим блоком (например, `(40000, 2510, 40000)`)
   - Scale: `(0.3, 0.3, 0.3)` (маленькая сфера)
5. Sphere Collider: `Is Trigger = true` (для InteractableManager)
6. Mesh Renderer → Material: перетащить свой материал (или оставить default)
7. Add Component → `Pickup Item`:
   - `Item Data`: `Item_Key_Purple.asset`
   - `Interaction Radius`: `1.5`

**Через MCP `execute_code`** (для батч-создания):
```csharp
var pickupGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
pickupGO.name = "[Key_Purple_Pickup]";
pickupGO.transform.position = new Vector3(40000f, 2510f, 40000f);
pickupGO.transform.localScale = Vector3.one * 0.3f;
pickupGO.GetComponent<Collider>().isTrigger = true;
// Material (если есть)
var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/MetaRequirement_Test/Materials/Key_Purple.mat");
if (mat != null) pickupGO.GetComponent<Renderer>().sharedMaterial = mat;
// PickupItem component
var itemData = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Project/Resources/Items/Item_Key_Purple.asset");
var pickup = pickupGO.AddComponent<ProjectC.Items.PickupItem>();
pickup.itemData = itemData;
pickup.itemId = 0; // будет вычислен InventoryWorld на StartHost
pickup.interactionRadius = 1.5f;
```

**Важно про `itemId`:** для scene-placed pickup'ов **НЕ выставляй** `itemId` вручную — оставь 0.
Сервер при `OnNetworkSpawn` InventoryServer'а вызовет `InventoryWorld.RegisterAllItems()`,
который зарегистрирует все `ItemData` из `Resources/Items/` под стабильными ID. Потом
`PickupItem.Collect()` использует `GetOrRegisterItemId(itemData)` для финального резолва.

**⚠️ Подводный камень (R2-SHIP-KEY-001):** если `ItemData` лежит в подпапке
`Resources/Items/SomeSub/`, `Resources.LoadAll` его не увидит. Тогда `RegisterAllItems()`
не зарегистрирует ID, и на сцене в `Start()` у `PickupItem.itemId` останется 0. При
`Collect()` через `GetOrRegisterItemId` ID будет вычислен, но нестабильно (зависит от
порядка итерации pickup'ов). Подробнее — в `KNOWN_ISSUES.md` R2-SHIP-KEY-001.

### 2.4 Создать GameObject с MetaRequirement (сам "замок")

**Через Editor:**
1. Hierarchy → правый клик → 3D Object → Cube
2. Назвать `[LockBox_Purple]`
3. Transform:
   - Position: рядом с pickup'ом (например, `(40003, 2510, 40000)`)
   - Scale: `(1.5, 1.5, 1.5)`
4. Add Component → `Network Object`:
   - **Обязательно** — без `NetworkObject` `MetaRequirement` (тоже NetworkBehaviour) не заспавнится
5. Add Component → `Meta Requirement`:
   - `_requiredItems` → Size: 1 → Element 0: drag `Item_Key_Purple.asset`
   - `_logic`: `All`
   - `_requiredCount`: `1` (только для AtLeastN, иначе игнорируется)
   - `_consumeOnUse`: `false` (не забирать предмет после использования)
   - `_interactableDisplayName`: `Фиолетовый Сундук`
   - `_customFailureMessage`: оставить пустым (авто-генерация: "Нужен предмет: Ключ: Фиолетовый Замок")
6. Add Component → `LockBox` (или свой скрипт-анимация):
   - `_baseColor`: фиолетовый
   - `_baseEmission`: тёмно-фиолетовый
   - `_animDuration`: `0.6`
   - `_animScaleMultiplier`: `1.2`
   - `_animEmissionMultiplier`: `3.0`
7. Box Collider: оставить `Is Trigger = false` (для физики)
8. Дополнительно: Add Component → `Sphere Collider` (отдельный, побольше):
   - `Is Trigger = true`
   - `Radius`: `2.5` (радиус взаимодействия для игрока)

**Через MCP `execute_code`:**
```csharp
var boxGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
boxGO.name = "[LockBox_Purple]";
boxGO.transform.position = new Vector3(40003f, 2510f, 40000f);
boxGO.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
boxGO.GetComponent<Collider>().isTrigger = false;
// Material
var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/MetaRequirement_Test/Materials/LockBox_Purple.mat");
if (mat != null) boxGO.GetComponent<Renderer>().sharedMaterial = mat;
// Триггер для игрока
var trigger = boxGO.AddComponent<SphereCollider>();
trigger.isTrigger = true;
trigger.radius = 2.5f;
// NetworkObject
boxGO.AddComponent<Unity.Netcode.NetworkObject>();
// MetaRequirement
var itemData = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Project/Resources/Items/Item_Key_Purple.asset");
var mr = boxGO.AddComponent<ProjectC.MetaRequirement.MetaRequirement>();
// Присваиваем private поля через reflection
var fldItems = typeof(MetaRequirement).GetField("_requiredItems", BindingFlags.NonPublic | BindingFlags.Instance);
fldItems.SetValue(mr, new ItemData[] { itemData });
var fldName = typeof(MetaRequirement).GetField("_interactableDisplayName", BindingFlags.NonPublic | BindingFlags.Instance);
fldName.SetValue(mr, "Фиолетовый Сундук");
// LockBox (анимация)
var lbox = boxGO.AddComponent<ProjectC.MetaRequirement.Test.LockBox>();
var fiBC = typeof(LockBox).GetField("_baseColor", BindingFlags.NonPublic | BindingFlags.Instance);
fiBC.SetValue(lbox, new Color(0.6f, 0.2f, 0.9f)); // фиолетовый
var fiBE = typeof(LockBox).GetField("_baseEmission", BindingFlags.NonPublic | BindingFlags.Instance);
fiBE.SetValue(lbox, new Color(0.15f, 0.05f, 0.25f));
```

### 2.5 Сохранить сцену

- `File → Save` или `Ctrl+S` (в Play mode — не сработает, сначала Stop)
- Через MCP: `manage_scene action=save`

### 2.6 Verify в Play mode

1. Play → StartHost
2. В Console ждать:
   - `[MetaRequirementRegistry] OnNetworkSpawn. IsServer=True, existing requirements=N`
   - `[MetaRequirementRegistry] Registered requirement: netId=N, displayName='Фиолетовый Сундук', logic=All, itemIds=[31]`
3. Подойти к `[Key_Purple_Pickup]` → E → в инвентаре появляется ключ
4. Подойти к `[LockBox_Purple]` → E:
   - **С ключом**: анимация (scale + flash), `OnAccessAllowed` event
   - **Без ключа**: toast внизу «Нужен предмет: Ключ: Фиолетовый Замок»

---

## 3. Шаг за шагом: создание MetaRequirement-объекта БЕЗ собственного визуала (двери, зоны)

Некоторые MetaRequirement'ы — "невидимые" (дверь, зона, триггер). Пример: `Zone_ChiefHall`,
которая требует `[Amulet_Sun, Amulet_Moon, Amulet_Star]` для входа.

### 3.1 Создать 3 ItemData (как в §2.1, для каждого амулета)

### 3.2 Создать GameObject с MetaRequirement (без MeshRenderer)

1. Hierarchy → правый клик → Create Empty
2. Назвать `[Zone_ChiefHall_Requirement]`
3. Add Component → `Network Object`
4. Add Component → `Meta Requirement`:
   - `_requiredItems` → Size: 3 → drag 3 амулета
   - `_logic`: `All` (все 3 нужны)
   - `_requiredCount`: `1` (игнорируется для All)
   - `_interactableDisplayName`: `Зал Вождя`
   - `_customFailureMessage`: `Соберите все 3 амулета`
5. Add Component → `Box Collider` (или `Sphere Collider`):
   - `Is Trigger = true`
   - `Size`: `(10, 5, 10)` — зона входа
6. **БЕЗ MeshRenderer** — невидимый объект
7. (опционально) Add Component → `MetaRequirementZoneVisualizer` (если есть — рисует gizmos)

### 3.3 Связать с дверью/триггером

Если в `[Zone_ChiefHall]` есть отдельный `DoorController` (NetworkBehaviour), он может:
- Слушать `OnTriggerEnter` → `NetworkPlayer` → читать `MetaRequirement.CanPlayerUse(clientId, out reason)`
- Если allowed — открыть дверь
- Если denied — показать локальный UI (или ждать `OnAccessDenied` event из `MetaRequirementClientState`)

**Альтернатива:** использовать `MetaRequirement` сам как точку входа. Дверь проверяет наличие
MetaRequirement на ТОМ ЖЕ или ближайшем объекте, читает `CanPlayerUse` и реагирует.

---

## 4. Шаг за шагом: создание MetaRequirement на СУЩЕСТВУЮЩЕМ объекте (ShipController, ChestContainer)

У вас уже есть `ShipController` (корабль), и вы хотите добавить на него MetaRequirement.
**Проблема:** на корабле уже есть `ShipKeyBinding` (старый алиас → MetaRequirement). См. §6.

### 4.1 Добавить MetaRequirement как 2-й компонент

1. Выбрать `[Ship_Light]` в Hierarchy
2. Add Component → `Meta Requirement`
3. Заполнить поля (как в §2.4)

**Что произойдёт:** оба компонента (`ShipKeyBinding` + `MetaRequirement`) попробуют зарегистрироваться
в `MetaRequirementRegistry` под одним `NetworkObjectId`. Registry использует `Dictionary<ulong, MetaRequirement>`,
поэтому **второй** выиграет (или первый, в зависимости от порядка `OnNetworkSpawn`).

### 4.2 Лучше: убрать старый `ShipKeyBinding` и оставить только `MetaRequirement`

См. документ-рекомендацию в `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` §"Шаг 8".

---

## 5. Создание MetaRequirement-объекта ДИНАМИЧЕСКИ (спавн из кода)

Если нужно спавнить MetaRequirement не в сцене, а в рантайме (например, сундук с лутом):

```csharp
// На сервере:
var go = new GameObject("[DynamicMetaReq]");
go.AddComponent<NetworkObject>();
var mr = go.AddComponent<MetaRequirement>();
// Заполнить _requiredItems через reflection (как в §2.4) или добавить public setter
go.GetComponent<NetworkObject>().Spawn();
```

**⚠️ Caveat:** scene-placed `NetworkObject` спавнятся через `ScenePlacedObjectSpawner` (см.
`docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md`). Динамически создаваемые — через
`NetworkManager.SpawnManager.InstantiateAndSpawn` или просто `Instantiate + Spawn()`.
Убедиться, что префаб зарегистрирован в `NetworkConfig.Prefabs.NetworkPrefabsLists` —
иначе NGO не будет знать, как его реплицировать клиентам.

---

## 6. Существующие Ship-корабли и алиасы

**Важно:** `ShipKeyBinding` — теперь `[Obsolete]` алиас `MetaRequirement`. На кораблях в
`WorldScene_0_0.unity` **по-прежнему** стоит `ShipKeyBinding`, и он **работает** через
наследование. Ничего менять не нужно (Этап 1 миграции).

**Если хотите мигрировать** (рекомендуется в след. сессии):
1. Remove Component → `Ship Key Binding` (на каждом корабле)
2. Add Component → `Meta Requirement`
3. Перетащить `_keyItemData` → `_requiredItems[0]`
4. `_shipDisplayName` → `_interactableDisplayName`
5. Save scene

См. `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` §"Шаг 8".

---

## 7. Создание кастомного визуала (вместо LockBox)

Если `LockBox` не подходит (нужна своя анимация, эффекты, дверь и т.п.):

```csharp
using ProjectC.MetaRequirement;
using UnityEngine;

public class MyDoorVisual : MonoBehaviour
{
    private void OnEnable()
    {
        // Подписка на event (auto-subscribe если Instance есть)
        if (MetaRequirementClientState.Instance != null)
        {
            MetaRequirementClientState.Instance.OnAccessAllowed += HandleAccessAllowed;
        }
    }

    private void OnDisable()
    {
        if (MetaRequirementClientState.Instance != null)
        {
            MetaRequirementClientState.Instance.OnAccessAllowed -= HandleAccessAllowed;
        }
    }

    private void Update()
    {
        // Lazy-subscribe (на случай если ClientState создан позже)
        if (MetaRequirementClientState.Instance != null && !_subscribed)
        {
            MetaRequirementClientState.Instance.OnAccessAllowed += HandleAccessAllowed;
            _subscribed = true;
        }
    }
    private bool _subscribed = false;

    private void HandleAccessAllowed(ulong netId)
    {
        // Проверяем, что это про НАС
        var no = GetComponent<Unity.Netcode.NetworkObject>();
        if (no == null || no.NetworkObjectId != netId) return;
        // Запускаем свою анимацию
        StartCoroutine(OpenDoor());
    }

    private System.Collections.IEnumerator OpenDoor()
    {
        // ... custom animation ...
        yield return null;
    }
}
```

**Альтернатива:** подписаться на `OnAccessDenied` для показа локального UI-сообщения.

---

## 8. Чек-лист перед сохранением сцены

- [ ] `ItemData` лежит в корне `Resources/Items/`, не в подпапке
- [ ] `MetaRequirement` компонент имеет заполненный `_requiredItems[]` (size >= 1)
- [ ] `MetaRequirement._interactableDisplayName` заполнен (не пустое, не "Object")
- [ ] На GameObject есть `NetworkObject` (без него `MetaRequirement` не заспавнится)
- [ ] В BootstrapScene есть `[MetaRequirementRegistry]` (иначе requirement не зарегистрируется)
- [ ] В BootstrapScene есть `[MetaRequirementToast]` (иначе игрок не увидит deny)
- [ ] Если у GameObject есть визуал (MeshRenderer) — на нём стоит `SphereCollider`/`BoxCollider` с `Is Trigger = true` для interact-distance (через `bounds.ClosestPoint`)

---

## 9. Что делать после добавления

1. Save scene
2. `refresh_unity` (через MCP или Unity Editor)
3. Проверить Console на 0 errors
4. Запустить Play → StartHost
5. Проверить, что в Console появилось:
   - `[MetaRequirementRegistry] OnNetworkSpawn. IsServer=True, existing requirements=N`
   - `[MetaRequirementRegistry] Registered requirement: netId=N, displayName='...', logic=..., itemIds=[...]`
6. Тестировать по чек-листу в `40_TESTING_GUIDE.md`

---

## 10. Типичные ошибки

| Симптом | Причина | Фикс |
|---|---|---|
| `[MetaRequirementRegistry] CanUse: ... allowed=False reason='не сервер'` | `MetaRequirement.CanPlayerUse` вызван на клиенте | Только сервер авторизует; на клиенте только UI/анимация |
| `OnNetworkSpawn not called on MetaRequirement` | Нет `NetworkObject` на GameObject | Add Component → Network Object |
| Toast не появляется | Нет `[MetaRequirementToast]` в BootstrapScene | Добавить (см. `bootstrap_setup.md`) |
| `allowed=True` без ключа | `_requiredItems[]` пуст в инспекторе | Заполнить (или это by design — All пустой = trivially true) |
| LockBox не анимируется | Подписка на `OnAccessAllowed` не сработала | Проверить что `LockBox` компонент на ТОМ ЖЕ GameObject, что и `MetaRequirement` |
| `OnEnable: MetaRequirementClientState.Instance is null` | Singleton ещё не создан (NetworkManagerController.Awake race) | Lazy-subscribe в Update (как в `LockBox.cs`) |
| `itemId не вычислен` (itemId=0 в Console при drop'е) | ItemData не зарегистрирован в InventoryWorld (см. R2-SHIP-KEY-001) | Положить в корень `Resources/Items/` |

---

## 11. Связанные документы

- `00_OVERVIEW.md` — концепция, архитектура, wire-протокол
- `RECIPES.md` — 10 типовых конфигураций
- `20_INSPECTOR_REFERENCE.md` — описание каждого поля Inspector
- `30_RUNTIME_FLOW.md` — что происходит в рантайме
- `40_TESTING_GUIDE.md` — пошаговое тестирование
- `50_KNOWN_ISSUES.md` — баги
- `99_CHANGELOG.md` — что сделано
- `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` — миграция старых ShipKey-объектов
- `docs/Ships/Key-subsystem/KNOWN_ISSUES.md` — история (R2-SHIP-KEY-001, R2-SHIP-KEY-002, R2-META-REQ-001)
- `docs/dev/META_REQUIREMENT_IMPL_NOTES.md` — рабочие заметки
