# CharacterEquipmentVisualApplier — подробный разбор

> Companion к `00_DESIGN.md` §3.2 и `01_DATA_MODEL.md` §3.
> Полный .cs + логика diff'а + edge cases.

---

## 1. Контракт

| Аспект | Значение |
|---|---|
| **Компонент** | `CharacterEquipmentVisualApplier : MonoBehaviour` |
| **Где сидит** | На `NetworkPlayer.prefab` (root). Авто-AddComponent при `OnEnable`. |
| **Зависимости** | `Animator` (humanoid), `EquipmentClientState.Instance` |
| **События** | Subscribe на `EquipmentClientState.OnEquipmentUpdated` |
| **Идемпотентность** | Повторный snapshot с тем же набором → no-op. Повторный `OnEnable`/`OnDisable` — безопасно. |
| **Анти-leak** | `DestroyAllVisuals` в `OnDisable`. Нет instance materials — visualPrefab приходит со своим материалом. |
| **Anti-restrictive** | Если `EquipmentClientState.Instance == null` или Animator не humanoid → warning + no-op, не падать. |

---

## 2. Алгоритм применения снапшота

```
OnEquipmentUpdated(EquipmentSnapshotDto snapshot):
  for each EquipSlot in 13 slots:
    ItemData newItem = snapshot.equip.GetItem(slot)   // null если пусто
    ItemData oldItem = _currentItems.GetOrNull(slot)

    if newItem == oldItem:
      continue  // нет изменений

    if oldItem != null:
      DestroyVisual(slot)   // убрать старый visual

    if newItem != null && newItem.visualPrefab != null:
      SpawnVisual(slot, newItem)   // навесить новый

    _currentItems[slot] = newItem
```

**Ключевая идея:** diff только по `_currentItems`. Это даёт:
- Снапшот с тем же набором → 13 continue, без аллокаций.
- Equip нового → destroy old (если был) + spawn new.
- Unequip → destroy visual.
- Замена одного шлема на другой → destroy первого + spawn второго.

**Почему не «на каждый снапшот всё destroy+spawn»:** это вызовет flicker на анимациях и не нужно.

---

## 3. Логика SpawnVisual

```
SpawnVisual(EquipSlot slot, ItemData item):
  // 1. Resolve bone (override или default).
  if !EquipSlotToBone.TryGetBoneTransformWithOverride(
       slot, item.attachBoneOverride, _animator, out bone):
    LogWarning($"Bone not found for slot {slot} on {name}")
    return

  // 2. Instantiate visualPrefab.
  var go = Instantiate(item.visualPrefab)
  go.name = $"Visual_{slot}_{item.itemName}"

  // 3. Parent + transform.
  go.transform.SetParent(bone, worldPositionStays: false)
  go.transform.localPosition = item.attachPositionOffset
  go.transform.localEulerAngles = item.attachRotationOffset
  go.transform.localScale = item.attachScale

  // 4. Disable colliders (visual only — no physics interaction).
  foreach (var col in go.GetComponentsInChildren<Collider>())
    col.enabled = false

  // 5. Сохранить для destroy.
  _spawnedVisuals[slot] = go
```

### 3.1 Зачем отключать коллайдеры

VisualPrefab может прийти с CapsuleCollider (для мира). На персонаже он не нужен — может конфликтовать с CharacterController, застревать в геометрии, вызывать OnTriggerEnter.

### 3.2 Зачем `worldPositionStays: false`

Bone двигается с анимацией (Humanoid rig). Если `worldPositionStays: true`, на момент Instantiate кость уже сместилась → визуал «прыгнет» в сторону. С `false` — localPosition/Offset применяются относительно текущего положения кости.

---

## 4. Логика DestroyVisual

```
DestroyVisual(EquipSlot slot):
  if !_spawnedVisuals.TryGetValue(slot, out var go) or go == null:
    return
  if Application.isPlaying:
    Destroy(go)
  else:
    DestroyImmediate(go)  // на случай editor-time тестов
  _spawnedVisuals.Remove(slot)
```

---

## 5. Полный код компонента

```csharp
// Project C: Equipment Visual System — Phase 2
// CharacterEquipmentVisualApplier: применяет ItemData.visualPrefab к персонажу по скелету.
// Аналог NpcVisualApplier (T-NPC-05) для equipment slots.
// Design: docs/Character/EquipmentVisual/00_DESIGN.md, 02_CHARACTER_APPLIER.md
//
// Триггер: EquipmentClientState.OnEquipmentUpdated (T-P10).
// Логика: diff snapshot ↔ _currentItems, spawn/destroy по слоту.

using System.Collections.Generic;
using ProjectC.Equipment;
using ProjectC.Equipment.Dto;
using ProjectC.Equipment.Visual;
using ProjectC.Items;
using UnityEngine;

namespace ProjectC.Player
{
    /// <summary>
    /// Phase 2: визуальный аппликатор экипировки на персонаже M (HumanM_Model).
    /// Подписывается на EquipmentClientState.OnEquipmentUpdated и поддерживает
    /// _spawnedVisuals в синхронизации со снапшотом.
    /// </summary>
    /// <remarks>
    /// Additive-only: новый компонент. Не модифицирует NetworkPlayer/EquipmentServer/EquipmentClientState.
    /// Anti-restrictive: если Animator не humanoid или EquipmentClientState.Instance == null — warning + no-op.
    /// </remarks>
    [DisallowMultipleComponent]
    public class CharacterEquipmentVisualApplier : MonoBehaviour
    {
        [Tooltip("Animator с humanoid rig. Если не задан — FindFirstValidAnimator() " +
                 "(поиск первого Animator с непустым runtimeAnimatorController).")]
        [SerializeField] private Animator _animator;

        [Tooltip("Показывать warning при отсутствии visualPrefab/кости/Animator. " +
                 "Default true для debug, можно отключить в release.")]
        [SerializeField] private bool _logWarnings = true;

        // slot → spawned visual GameObject (null если слот пуст)
        private readonly Dictionary<EquipSlot, GameObject> _spawnedVisuals = new();

        // slot → ItemData (для diff'а snapshot vs current)
        private readonly Dictionary<EquipSlot, ItemData> _currentItems = new();

        private EquipmentClientState _clientState;

        // === Lifecycle ===

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = FindFirstValidAnimator();
            }
        }

        private void OnEnable()
        {
            _clientState = EquipmentClientState.Instance;
            if (_clientState == null)
            {
                if (_logWarnings)
                {
                    Debug.LogWarning($"[CharacterEquipmentVisualApplier] EquipmentClientState.Instance == null on '{name}'. Visual equip skipped.", this);
                }
                return;
            }

            _clientState.OnEquipmentUpdated += OnEquipmentUpdated;

            // Если snapshot уже есть (race condition) — применить немедленно.
            if (_clientState.CurrentSnapshot.HasValue)
            {
                OnEquipmentUpdated(_clientState.CurrentSnapshot.Value);
            }
        }

        private void OnDisable()
        {
            if (_clientState != null)
            {
                _clientState.OnEquipmentUpdated -= OnEquipmentUpdated;
                _clientState = null;
            }
            DestroyAllVisuals();
            _currentItems.Clear();
        }

        // === Snapshot handler ===

        private void OnEquipmentUpdated(EquipmentSnapshotDto snapshot)
        {
            if (_animator == null || !_animator.isHuman)
            {
                if (_logWarnings)
                {
                    Debug.LogWarning($"[CharacterEquipmentVisualApplier] Animator not humanoid on '{name}'. Visual equip skipped.", this);
                }
                return;
            }

            // Идём по всем EquipSlot из enum (None пропускаем).
            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                if (slot == EquipSlot.None) continue;
                ProcessSlot(slot, snapshot);
            }
        }

        private void ProcessSlot(EquipSlot slot, EquipmentSnapshotDto snapshot)
        {
            // Получить ItemData для слота из снапшота.
            ItemData newItem = null;
            if (snapshot.equip.TryGetItemId(slot, out int itemId) && itemId > 0)
            {
                var inv = ProjectC.Items.InventoryWorld.Instance;
                if (inv != null)
                {
                    newItem = inv.GetItemDefinition(itemId);
                }
            }

            ItemData oldItem = null;
            _currentItems.TryGetValue(slot, out oldItem);

            // Diff: нет изменений → continue.
            if (ReferenceEquals(newItem, oldItem)) return;

            // 1. Убрать старый визуал, если был.
            if (oldItem != null)
            {
                DestroyVisual(slot);
            }

            // 2. Навесить новый, если есть и у него есть visualPrefab.
            if (newItem != null && newItem.visualPrefab != null)
            {
                SpawnVisual(slot, newItem);
            }

            // 3. Обновить tracking.
            if (newItem != null)
            {
                _currentItems[slot] = newItem;
            }
            else
            {
                _currentItems.Remove(slot);
            }
        }

        // === Spawn / Destroy ===

        private void SpawnVisual(EquipSlot slot, ItemData item)
        {
            // 1. Resolve bone.
            if (!EquipSlotToBone.TryGetBoneTransformWithOverride(
                    slot, item.attachBoneOverride, _animator, out Transform bone))
            {
                if (_logWarnings)
                {
                    Debug.LogWarning($"[CharacterEquipmentVisualApplier] Bone not found for slot {slot} (override={item.attachBoneOverride}) on '{name}'. Visual skipped for '{item.itemName}'.", this);
                }
                return;
            }

            // 2. Instantiate.
            GameObject go = Instantiate(item.visualPrefab);
            go.name = $"Visual_{slot}_{item.itemName}";

            // 3. Parent + transform.
            go.transform.SetParent(bone, worldPositionStays: false);
            go.transform.localPosition = item.attachPositionOffset;
            go.transform.localEulerAngles = item.attachRotationOffset;
            go.transform.localScale = item.attachScale;

            // 4. Disable colliders (visual only).
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.enabled = false;
            }

            // 5. Track.
            _spawnedVisuals[slot] = go;

            if (Debug.isDebugBuild && _logWarnings)
            {
                Debug.Log($"[CharacterEquipmentVisualApplier] Spawned '{go.name}' on bone '{bone.name}' (slot={slot}).", this);
            }
        }

        private void DestroyVisual(EquipSlot slot)
        {
            if (!_spawnedVisuals.TryGetValue(slot, out GameObject go)) return;
            if (go == null)
            {
                _spawnedVisuals.Remove(slot);
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
            _spawnedVisuals.Remove(slot);
        }

        private void DestroyAllVisuals()
        {
            foreach (var kvp in _spawnedVisuals)
            {
                if (kvp.Value != null)
                {
                    if (Application.isPlaying) Destroy(kvp.Value);
                    else DestroyImmediate(kvp.Value);
                }
            }
            _spawnedVisuals.Clear();
        }

        // === Helpers ===

        /// <summary>
        /// Найти первый валидный Animator (с непустым runtimeAnimatorController).
        /// Копия логики из NetworkPlayer.FindFirstValidAnimator — ищет на root и в детях.
        /// </summary>
        private Animator FindFirstValidAnimator()
        {
            var animators = GetComponentsInChildren<Animator>(true);
            foreach (var a in animators)
            {
                if (a != null && a.runtimeAnimatorController != null)
                {
                    return a;
                }
            }
            return null;
        }

        // === Public API (для тестов и отладки) ===

        /// <summary>Сколько visual'ов сейчас на персонаже (для UI/debug).</summary>
        public int ActiveVisualCount => _spawnedVisuals.Count;

        /// <summary>VisualPrefab для слота (или null).</summary>
        public GameObject GetVisualForSlot(EquipSlot slot)
        {
            _spawnedVisuals.TryGetValue(slot, out var go);
            return go;
        }
    }
}
```

---

## 6. Edge cases

### 6.1 EquipmentClientState.Instance == null (client ещё не инициализирован)

* `OnEnable` warning + early return.
* Когда `EquipmentClientState` появится позже — `_currentItems` останется пустым, следующий snapshot применится штатно.

### 6.2 Animator не humanoid

* `OnEquipmentUpdated` warning + return. Visual'ы не спавнятся.
* Не падаем — это anti-restrictive default.

### 6.3 visualPrefab == null

* Designer не задал меш → `ProcessSlot` early-return, никакого spawn. **Никаких warning'ов** (это нормальное состояние для ~500 существующих ассетов).

### 6.4 Snapshot приходит с тем же itemId, но ItemData пересоздан (edit-time)

* `ReferenceEquals(newItem, oldItem)` будет false → пересоздаём visual.
* Это безопасно, но чуть дороже. Edit-time сценарий — нормальный.

### 6.5 visualPrefab приходит со своей SkinnedMesh + костями

* Не пытаемся re-skin. SkinnedMesh внутри visualPrefab уже настроен на свой dummy-скелет внутри префаба. Двигается только через `parent → bone transform`.
* Если нужен re-skin (визуал персонажа использует тот же skeleton, что и одежда) — отдельный тикет, **не блокирует MVP**.

### 6.6 Multiple items in same slot (race / double equip)

* `EquipmentWorld.TryEquip` уже проверяет «slot occupied» (T-P08). Дубликатов в snapshot не будет.

### 6.7 Disable/Enable компонента во время экипировки

* `OnDisable` → `DestroyAllVisuals` + `_currentItems.Clear()`.
* `OnEnable` → subscribe + apply current snapshot (если есть).
* Чистый reset, нет утечек.

### 6.8 Персонаж не NetworkPlayer (тест в Edit mode)

* Можно положить компонент на любой GameObject с Animator + создать stub-EquipmentClientState.
* `EquipmentClientState.Instance` singleton — тестовый override.

---

## 7. Добавление компонента на NetworkPlayer.prefab

**Вариант A (через Editor script, по аналогии с SetupPlayerVisual):**

`Assets/_Project/Editor/SetupEquipmentVisualApplier.cs`:
```csharp
[MenuItem("Tools/ProjectC/Player/Add EquipmentVisualApplier to NetworkPlayer")]
public static void AddComponent() {
    var prefabPath = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
    var contents = PrefabUtility.LoadPrefabContents(prefabPath);
    try {
        if (contents.GetComponent<CharacterEquipmentVisualApplier>() == null) {
            contents.AddComponent<CharacterEquipmentVisualApplier>();
            Debug.Log("[SetupEquipmentVisualApplier] Added component to NetworkPlayer.prefab");
        }
        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
    } finally {
        PrefabUtility.UnloadPrefabContents(contents);
    }
}
```

**Вариант B (через MCP `unity-mcp`):**
1. `manage_components add --prefab Assets/_Project/Prefabs/NetworkPlayer.prefab --type ProjectC.Player.CharacterEquipmentVisualApplier`
2. `refresh_unity` + `read_console`

**Предпочтительно A** — проще воспроизвести, идемпотентно, без зависимости от MCP.

---

## 8. Тестирование

### 8.1 Edit Mode smoke test

* Открыть NetworkPlayer.prefab в Prefab Mode.
* Вручную положить `CharacterEquipmentVisualApplier` + указать `_animator = Visual_Model/Animator`.
* Inspector должен показать 0 spawned visuals.

### 8.2 Play Mode smoke test

1. Запустить BootstrapScene → Host.
2. Подождать, пока `EquipmentClientState` инициализируется.
3. Открыть Inventory → Drag "Рабочая каска" на Head slot.
4. Через 0.5s на голове персонажа должен появиться cone-визуал.
5. Console: `[CharacterEquipmentVisualApplier] Spawned 'Visual_Head_Рабочая каска' on bone 'Head' (slot=Head)`.
6. Unequip → cone исчезает, console: implicit (нет лога на destroy, можно добавить в Debug).

### 8.3 Visual debug helper (опционально)

Добавить `[ContextMenu]` метод для теста:
```csharp
[ContextMenu("DEBUG: Force re-apply current snapshot")]
public void DebugReapply() {
    if (_clientState != null && _clientState.CurrentSnapshot.HasValue) {
        OnEquipmentUpdated(_clientState.CurrentSnapshot.Value);
    }
}
```

Позволяет тестировать без перезапуска.

---

## 9. Будущие расширения (НЕ в MVP)

| Идея | Когда делать |
|---|---|
| Скрытие базового меша персонажа под одеждой (как в Skyrim) | После Phase 2. Реализуется через `SkinnedMeshRenderer.enabled = false` для body + armor как новый SMR на тот же rig. |
| 2H оружие с двумя grip-точками | Расширить `attachBoneOverride` массивом + secondary bone для «second hand». |
| Material override по tier | Если designer хочет цвет по tier — добавить поле `tierMaterialOverride`. |
| Sync через NetworkVariable | Если нужно, чтобы другие игроки видели экипировку — добавить NetworkVariable<int> для каждого EquipSlot. **Не MVP**. |
| Hide при boarding в ship | Hook на `NetworkPlayer.IsInShip` → disable all visuals. Если проблема — отдельный багфикс. |