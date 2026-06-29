# Equipment Visual System — дизайн

> **Подсистема:** визуальное отображение надеваемых предметов на персонаже-гуманоиде M
> **Дата:** 2026-06-29
> **Базируется на:** `ItemData` / `ClothingItemData` / `ModuleItemData` / `WeaponItemData` (T-P07..T-P09),
> `EquipSlot` enum, `EquipmentServer` (T-P09), `EquipmentClientState` (T-P10), `NetworkPlayer` (`Assets/_Project/Scripts/Player/`),
> `NpcVisualApplier` (T-NPC-05) — паттерн SkinnedMeshRenderer override.
> **Назначение:** каждый пикап-айтем (одежда, модуль, оружие) получает **3D-меш/prefab**, который:
>   1. отображается в мире при дропе с трупа/сундука (Phase 1);
>   2. **при надевании** спавнится на скелете персонажа M, прикреплённый к кости по EquipSlot (Phase 2).
>
> **Архитектурный принцип:** additive-only — **не ломаем** существующие ItemData/Equipment/Stats подсистемы.
> Только добавляем опциональные поля и новый компонент-аппликатор. Если visualPrefab == null →
> всё работает как сейчас (no visual, anti-restrictive default).

---

## 0. TL;DR

| Аспект | Решение |
|---|---|
| **Корень проблемы** | В `ItemData` (и всех 3 наследниках) нет поля для 3D-меша. `icon` (Sprite) только для UI. `EquipmentServer` одевает «в данные», но не «в кадр» — персонаж остаётся голым. |
| **Что не ломаем** | ItemData поля, EquipmentServer RPCs, EquipmentWorld, DTO, EquipSlot enum, StatsServer — никаких breaking changes. Только аддитивные поля и новый компонент. |
| **Phase 1 (3D-меши для предметов)** | Добавить `GameObject visualPrefab` в `ItemData` (по умолчанию null). Использовать в `PickupItem` для дропа в мире. Существующие ~500 ассетов останутся без меша — это OK. |
| **Phase 2 (надевание на M)** | Новый компонент `CharacterEquipmentVisualApplier` (MonoBehaviour) на `NetworkPlayer`. Подписывается на `EquipmentClientState.OnEquipmentUpdated`, instantiates visualPrefab из `EquipmentSnapshotDto`, парent к кости скелета по `EquipSlot → HumanBodyBones`. |
| **Слоты для костей** | Head→Head, Chest→Spine, Legs→Hips/UpperLeg, Feet→Foot, Back→Spine (offset back), Hands→Hand, Accessory1/2→Chest (decorative), WeaponMain→RightHand, WeaponOff→LeftHand, Module1..3→Chest (decorative glow). Все маппинги в одном `EquipSlotToBone.cs` файле. |
| **Риск** | Минимальный — additive-only паттерн, по аналогии с `NpcVisualApplier`. Модель HumanM_Model (Kevin Iglesias) уже в `NetworkPlayer.prefab`. |
| **Модель персонажа** | HumanM_Model — стандартный Unity humanoid (Head/Spine/Hips/Left/Right Hand/Foot/UpperArm/LowerArm/UpperLeg/LowerLeg). HumanBodyBones enum покрывает всё. |

---

## 1. Контекст: что уже есть

### 1.1 Персонаж M (NetworkPlayer)

* `Assets/_Project/Prefabs/NetworkPlayer.prefab` — root prefab.
* Дочерний `Visual_Model` (GameObject) — HumanM_Model от Kevin Iglesias. Имеет `Animator` + SkinnedMeshRenderer со стандартным humanoid-скелетом.
* Кости доступны через `Animator.GetBoneTransform(HumanBodyBones.Head / RightHand / ...)`.
* Подтверждено через `grep`: `Visual_Model` есть в prefab. См. `Assets/_Project/Editor/SetupPlayerVisual.cs` — это код, который подложил HumanM_Model.

### 1.2 ItemData (базовый ScriptableObject)

* `Assets/_Project/Scripts/Core/ItemType.cs` — `ItemData` базовый:
  ```csharp
  public class ItemData : ScriptableObject {
      public string itemName;
      public ItemType itemType;
      public string description;
      public Sprite icon;
      public int maxStack = 1;
      public float weightKg = 0.1f;
  }
  ```
* **Нет** поля `visualPrefab`. **Нет** поля `mesh` или `prefab`.

### 1.3 Наследники ItemData

* `ClothingItemData` (T-P07) — slot, tier, stat bonuses, requiredSkills, requirementType. **Нет** visual.
* `ModuleItemData` (T-P07) — slot, moduleType, sensor/crafting/weapon effects. **Нет** visual.
* `WeaponItemData` (T-CB03) — weaponClass, ERPR-пакет. **Нет** visual.

### 1.4 EquipmentServer / EquipmentClientState

* `RequestEquipRpc(itemId, slot)` (T-P09) — перемещает item из inventory в slot. НЕ spawn'ит визуал.
* `EquipmentSnapshotDto` — replicated state с `slotItemIds[13]` + `slotOccupied[13]`. Уже идёт по сети на owner.
* `EquipmentClientState.OnEquipmentUpdated(snapshot)` (T-P10) — событие на клиенте при обновлении снапшота. **Идеальная точка хука для визуала**.

### 1.5 PickupItem (дропы в мире)

* `Assets/_Project/Scripts/Core/PickupItem.cs` — спавнит объект-пикап в мире. Сейчас использует **только** `icon` (Sprite). Без меша.

### 1.6 Паттерн NpcVisualApplier (T-NPC-05) — основной reference

* `Assets/_Project/Scripts/AI/NpcVisualApplier.cs` — MonoBehaviour, применяет `NpcVisualConfig` к NPC-префабу:
  - Ищет SkinnedMeshRenderer по имени (`HumanM_BodyMesh`).
  - Material override через `sharedMaterials` (без instance leak).
  - Tint через MaterialPropertyBlock.
* Используется в `NpcSpawnerConfig.visualConfig` (опциональное поле).
* **Для нашего случая** — аналог по структуре, но:
  - Применяется не к одному SMR, а **к нескольким префабам** (одежда + оружие + модули).
  - Парent делается не к root, а к **конкретной кости** через `HumanBodyBones`.
  - Триггер — `OnEquipmentUpdated`, а не spawn NPC.

---

## 2. Цель

Реализовать визуальное отображение любого `ItemData` на персонаже M по аналогии с Risk of Rain 2:

1. **Каждый** `ItemData` (ClothingItemData / ModuleItemData / WeaponItemData) может иметь `visualPrefab` — GameObject с low-poly мешем.
2. При экипировке префаб **спавнится как child кости** скелета персонажа M и следует за анимациями (благодаря SkinnedMesh/Animator parent transform).
3. При разэкипировке префаб **уничтожается** чисто.
4. Все маппинги EquipSlot → кость — в **одном static-словаре**, легко расширяемом.
5. При отсутствии visualPrefab → дефолтное поведение (как сейчас), без warning-спама.
6. **Phase 1** (отдельная): при дропе в мире `PickupItem` использует `visualPrefab` для отображения (опционально).

---

## 3. Архитектурные решения

### 3.1 Решение A: visualPrefab в базовом ItemData

Почему в **базовом** классе, а не в каждом наследнике:
- Любой pickable item теоретически может иметь визуал (даже расходники, ключи).
- Унификация: `PickupItem`, `EquipmentServer`, UI tooltip — все используют один и тот же путь.
- Default = null → существующие ~500 .asset'ов останутся без изменений.
- Если нужно «только для ClothingItemData» — designer оставляет null для других типов. Конвенция, не enforcement.

```csharp
[CreateAssetMenu(...)]
public class ItemData : ScriptableObject {
    // ... existing fields ...
    [Header("Visual (Phase 1 — optional)")]
    [Tooltip("3D-меш/prefab для отображения предмета в мире (PickupItem) " +
             "и на персонаже при экипировке. Если null — отображается только иконка.")]
    public GameObject visualPrefab;
}
```

**Additive-only:** новое поле, default = null. Существующие ассеты продолжают работать.

### 3.2 Решение B: CharacterEquipmentVisualApplier (новый компонент)

Новый MonoBehaviour на `NetworkPlayer.prefab`. Подписывается на `EquipmentClientState.OnEquipmentUpdated`, применяет/убирает меши. Полная аналогия с `NpcVisualApplier`, но для **нескольких слотов одновременно**.

```csharp
public class CharacterEquipmentVisualApplier : MonoBehaviour {
    private Animator _animator;
    private EquipmentClientState _clientState;
    private readonly Dictionary<EquipSlot, GameObject> _spawnedVisuals = new();

    void OnEnable() {
        _clientState = EquipmentClientState.Instance;
        if (_clientState != null) _clientState.OnEquipmentUpdated += OnEquipmentUpdated;
    }
    void OnDisable() {
        if (_clientState != null) _clientState.OnEquipmentUpdated -= OnEquipmentUpdated;
        DestroyAllVisuals();
    }

    void OnEquipmentUpdated(EquipmentSnapshotDto snapshot) {
        // Для каждого слота: если item есть и изменился → spawn, иначе destroy.
        // Diff = (previous ∖ current) ∪ (current ∖ previous).
    }
}
```

### 3.3 Решение C: EquipSlotToBone — единая таблица маппинга

Один static-класс `EquipSlotToBone` с методом `TryGetBoneTransform(EquipSlot, Animator, out Transform)`. Не enum-mapping внутри аппликатора — чтобы легко править централизованно и тестировать.

```csharp
public static class EquipSlotToBone {
    public static bool TryGetBoneTransform(EquipSlot slot, Animator animator, out Transform bone) {
        bone = null;
        if (animator == null || !animator.isHuman) return false;
        bone = slot switch {
            EquipSlot.Head       => animator.GetBoneTransform(HumanBodyBones.Head),
            EquipSlot.Chest      => animator.GetBoneTransform(HumanBodyBones.Spine),
            EquipSlot.Legs       => animator.GetBoneTransform(HumanBodyBones.Hips),
            EquipSlot.Feet       => animator.GetBoneTransform(HumanBodyBones.LeftFoot),
            EquipSlot.Back       => animator.GetBoneTransform(HumanBodyBones.Spine),
            EquipSlot.Hands      => animator.GetBoneTransform(HumanBodyBones.LeftHand),
            EquipSlot.Accessory1 => animator.GetBoneTransform(HumanBodyBones.Spine),
            EquipSlot.Accessory2 => animator.GetBoneTransform(HumanBodyBones.Spine),
            EquipSlot.WeaponMain => animator.GetBoneTransform(HumanBodyBones.RightHand),
            EquipSlot.WeaponOff  => animator.GetBoneTransform(HumanBodyBones.LeftHand),
            EquipSlot.Module1    => animator.GetBoneTransform(HumanBodyBones.Spine),
            EquipSlot.Module2    => animator.GetBoneTransform(HumanBodyBones.Spine),
            EquipSlot.Module3    => animator.GetBoneTransform(HumanBodyBones.Spine),
            _ => null,
        };
        return bone != null;
    }
}
```

**Почему Spine для Back/Accessory/Module:** стандартная практика — декоративные предметы на торсе крепятся к Spine с offset. Designer правит offset через `localPositionOffset` (см. п. 3.4).

### 3.4 Решение D: EquipVisualAttachData — per-item override

Один slot может давать разный визуал для разных предметов (меч в правой руке vs пистолет в правой руке — разное положение). Добавляем в `ItemData` опциональные поля **attach override**:

```csharp
[Header("Visual Attach (Phase 2 — optional)")]
[Tooltip("Кость для прикрепления. Если None — используется EquipSlot→HumanBodyBones default.")]
public HumanBodyBones? attachBoneOverride = null;

[Tooltip("Локальный offset прикреплённого visualPrefab к кости.")]
public Vector3 attachPositionOffset = Vector3.zero;
[Tooltip("Локальное вращение (Euler) прикреплённого visualPrefab.")]
public Vector3 attachRotationOffset = Vector3.zero;
[Tooltip("Локальный масштаб visualPrefab относительно кости. 1.0 = без изменений.")]
public Vector3 attachScale = Vector3.one;
```

**Почему nullable override:**
- 99% предметов подходят к default-кости слота.
- Только специфичные случаи (большой двуручный меч, шляпа-цилиндр) нуждаются в override.
- Default = default, существующие ассеты работают.

### 3.5 Решение E: visualPrefab для NPC pickup (Phase 1.5, опционально)

Для **визуала подбираемых предметов в мире** — расширяем `PickupItem.cs`:
- Если `itemData.visualPrefab != null` → spawn копию префаба как child PickupItem.
- Иначе — fallback к SpriteRenderer с icon (как сейчас).

Не критично для основного flow (экипировка), но логически завершает Phase 1.

---

## 4. Что **НЕ** меняем (anti-break list)

| Файл / система | Почему не трогаем |
|---|---|
| `ItemType` enum | Уже 9 значений (T-KEY-01). Добавлять не нужно. |
| `EquipSlot` enum | 13 значений (T-P07). Достаточно для любого типа брони/оружия. |
| `EquipmentData` (parallel arrays) | T-P08 hard-tested. Не трогаем. |
| `EquipmentServer` RPCs | T-P09 work. Не трогаем. |
| `EquipmentSnapshotDto` | T-P10 + T-INP-09 уже используют `GetWeaponInSlot` отсюда. |
| `EquipmentClientState.OnEquipmentUpdated` event | **Используем** как hook, не модифицируем. |
| `CharacterWindow` UI | UI-логика не меняется. |
| `StatsServer` recompute | Additive stat bonuses не трогаем. |
| `NetworkPlayer` existing fields | Не трогаем. Добавляем только новый component. |
| `HumanM_Model` fbx | Не редактируем. |
| `PlayerAnimation.controller` | Не трогаем. |
| `PickupItem` existing API | Расширяем опционально (Phase 1.5). |

---

## 5. План реализации (TL;DR)

Подробный план — в `03_PHASES.md`. Краткая сводка:

| Phase | Что | Где | Размер |
|---|---|---|---|
| **1.1** | Добавить `visualPrefab` в `ItemData` | `Assets/_Project/Scripts/Core/ItemType.cs` | 5 строк |
| **1.2** | Расширить `PickupItem` для отображения visualPrefab в мире | `Assets/_Project/Scripts/Core/PickupItem.cs` | ~30 строк |
| **1.3** | Создать 2-3 тестовых visualPrefab (helmet/boots/blade) | `Assets/_Project/Resources/Visuals/Equipment/` | 3 меша |
| **2.1** | Создать `EquipSlotToBone.cs` — таблица маппинга | `Assets/_Project/Scripts/Equipment/Visual/EquipSlotToBone.cs` | ~60 строк |
| **2.2** | Добавить attach-поля в `ItemData` (Phase 2: `attachBoneOverride` и т.д.) | `Assets/_Project/Scripts/Core/ItemType.cs` | 10 строк |
| **2.3** | Создать `CharacterEquipmentVisualApplier` MonoBehaviour | `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs` | ~120 строк |
| **2.4** | Добавить компонент на `NetworkPlayer.prefab` | через `SetupPlayerVisual`-стиль Editor script или MCP | 1 раз |
| **2.5** | Smoke-test: надеть seed-шлем "Рабочая каска" → увидеть 3D-меш на голове | Play Mode | - |

---

## 6. Открытые вопросы (для будущей сессии)

Не блокируют MVP, но могут появиться:

1. **Материалы экипировки:** сейчас default material. Дизайнер может хотеть per-tier material. Ответ: в `visualPrefab` уже можно положить кастомные меши с кастомными материалами — вопрос отпадает.
2. **Двуручное оружие:** для 2H меча дефолтный WeaponMain → RightHand норм, но нужно проверить, что визуал не пересекается с анимацией.
3. **Скрытие стандартной одежды персонажа:** если дизайнер хочет "костюм сапёра" вместо стандартных штанов — нужно скрывать оригинальный SMR. Это **отдельная задача** (post-MVP), тут не рассматриваем.
4. **Multiplayer sync визуала:** визуал — чисто client-side (как NpcVisualConfig). Сервер не реплицирует. Если другой игрок не видит твой шлем — это нормально в MVP (по аналогии с NPC visual).
5. **Модули-импланты:** как они выглядят на персонаже? В GDD — "индикатор/свечение". В Phase 2 — просто парent visualPrefab к Spine, с glow-материалом внутри префаба.
6. **Hide при boarding в ship:** сейчас `_inShip` делает кое-что с рендерерами. Нужно проверить, что экипировка не «торчит» из корабля. Возможно — отключать visual при `_inShip`.

Ответы на 1-5 простые (на уровне convention), 6 — отдельный багфикс если возникнет.

---

## 7. Связанные документы

| Документ | Назначение |
|---|---|
| `01_DATA_MODEL.md` | Точные .cs-сигнатуры и поля SO |
| `02_CHARACTER_APPLIER.md` | Подробный разбор `CharacterEquipmentVisualApplier` (логика diff, edge cases) |
| `03_PHASES.md` | Пошаговый план с командами верификации |
| `docs/Character/05_CLOTHING_AND_MODULES.md` | Базовый дизайн Clothing/Module (откуда растём) |
| `docs/Character/00_README.md` | Навигация по Character Progression |
| `Assets/_Project/Scripts/AI/NpcVisualApplier.cs` | Паттерн SkinnedMeshRenderer override (reference) |