# Clothing and Modules — слоты, equip/unequip, stat-bonuses

> **Дата:** 2026-06-14
> **Базируется на:** `ItemData` (базовый SO для всех предметов), `EquipSlot` enum (новый), `EquipmentData` struct (parallel arrays вместо Dictionary)
> **Подход:** Clothing и Module расширяют `ItemData` через наследование — переиспользуем `itemName`, `icon`, `ItemRegistry` registration

---

## 1. EquipSlot enum — дизайн

### 1.1 Слоты для одежды

Из спецификации пользователя:
> "одежда с характеристиками... слоты в P-персонаж"
> "модули — отдельная до одежда с характеристиками... слоты в P-персонаж"

**Слоты (13 штук, normalized to 0..12 в EquipmentData):**

```csharp
public enum EquipSlot : byte {
    None = 0,
    Head = 1,        // шлем, очки, шапка
    Chest = 2,       // нагрудник, куртка
    Legs = 3,        // поножи, штаны
    Feet = 4,        // ботинки, сапоги
    Back = 5,        // плащ, ранцы
    Hands = 6,       // перчатки
    Accessory1 = 7,  // кольцо 1
    Accessory2 = 8,  // кольцо 2
    WeaponMain = 9,  // основное оружие (будущее)
    WeaponOff = 10,  // второе оружие (будущее)
    Module1 = 20,    // имплант 1
    Module2 = 21,    // имплант 2
    Module3 = 22,    // имплант 3
}
```

**Нормализация в EquipmentData** (для NGO 2.x serialization):

```csharp
private static int SlotToIndex(EquipSlot slot) {
    int v = (int)slot;
    if (v == 0) return -1;          // None → invalid
    if (v <= 10) return v;          // Head..WeaponOff → 1..10 → индекс 1..10
    if (v >= 20 && v <= 22) return 11 + (v - 20);  // Module1..3 → индекс 11..13
    return -1;
}

public const int SLOT_COUNT = 13;  // 1..10 (10) + 11..13 (3 modules)
```

### 1.2 Альтернативные подходы (отвергнуты)

| Подход | Почему нет |
|--------|-----------|
| `Dictionary<EquipSlot, int>` в EquipmentData | NGO 2.x не сериализует Dictionary через INetworkSerializable |
| 2 EquipmentData struct (ClothingEquipment + ModuleEquipment) | дубликация кода, два snapshot'а |
| Один EquipmentData с 13 полями (slot1, slot2, ..., slot13) | verbose, менее типизированный |
| Slot через string ("Head", "Chest") | типизация лучше через enum |
| Equipment как новый ItemType | `ItemType.Equipment` уже существует (`ItemType.cs:8`), используется как generic bucket; слоты — отдельная размерность |

---

## 2. ClothingItemData — структура

### 2.1 Наследование

```csharp
[CreateAssetMenu(fileName = "Clothing_", menuName = "Project C/Equipment/Clothing", order = 11)]
public class ClothingItemData : ItemData {
    // Наследует: itemName, itemType (auto: Equipment), description, icon, maxStack=1, weightKg
    // Добавляет: slot, tier, statBonuses, requiredSkills

    [Header("Equip")]
    public EquipSlot slot;

    [Header("Tier (visual + scaling)")]
    [Range(1, 10)] public int tier = 1;

    [Header("Stat Bonuses (additive, base)")]
    public float strengthBonus;
    public float dexterityBonus;
    public float intelligenceBonus;

    [Header("Stat Bonuses (multiplicative, applied after additive)")]
    [Range(0f, 5f)] public float strengthMultiplier;
    [Range(0f, 5f)] public float dexterityMultiplier;
    [Range(0f, 5f)] public float intelligenceMultiplier;

    [Header("Skill Requirements")]
    [Tooltip("All listed skills must be unlocked to equip this item.")]
    public SkillNodeConfig[] requiredSkills = Array.Empty<SkillNodeConfig>();
}
```

### 2.2 Stat-bonus: additive vs multiplicative

**Из спецификации пользователя:**
> "одежда с характеристиками"

**Не специфицировано:** бонусы additive (flat) или multiplicative (%)?

**Предлагаемая формула:**
```
effective_stat = (base_stat + sum_of_additive_bonuses) * (1 + sum_of_multiplicative_bonuses)
```

**Пример:**
- Base STR = 10
- Шлем: +2 STR (additive)
- Нагрудник: +20% STR (multiplier)
- Итог: (10 + 2) * (1 + 0.20) = 14.4 STR

**В StatsClientState / StatsServer при пересчёте** (после equip/unequip):
```csharp
private float RecomputeEffectiveStat(StatType stat, PlayerStats base, EquipmentData equip, List<SkillEffect> skillEffects) {
    float baseValue = GetBaseStat(stat, base);
    float additiveSum = 0f;
    float multiplicativeSum = 0f;

    // Sum bonuses from all equipped items
    foreach (var slotIdx in EnumerateOccupiedSlots(equip)) {
        int itemId = equip.slotItemIds[slotIdx];
        if (!InventoryWorld.Instance.GetItemDataById(itemId, out var itemData)) continue;
        if (itemData is ClothingItemData clothing) {
            additiveSum += GetStatBonus(clothing, stat);
            multiplicativeSum += GetStatMultiplier(clothing, stat);
        } else if (itemData is ModuleItemData module) {
            additiveSum += GetStatBonus(module, stat);
            multiplicativeSum += GetStatMultiplier(module, stat);
        }
    }

    // Sum bonuses from learned skills
    foreach (var effect in skillEffects) {
        if (effect.type != SkillEffect.Type.StatMod || effect.statType != stat) continue;
        additiveSum += effect.floatValue;
        multiplicativeSum += effect.multiplier;
    }

    float effective = (baseValue + additiveSum) * (1f + multiplicativeSum);

    // Clamp для overflow protection
    if (effective < 0.1f) effective = 0.1f;
    if (effective > 100000f) effective = 100000f;
    return effective;
}
```

**Recompute trigger:** в `EquipmentServer.TryEquip/TryUnequip` → recompute for player → send snapshot → UI updates.

### 2.3 Какие предметы — одежда (примеры для теста)

| Filename | Slot | Tier | Bonuses | Skills |
|----------|------|------|---------|--------|
| `Clothing_WorkerHelmet.asset` | Head | 1 | STR +1 | none |
| `Clothing_SteelChestplate.asset` | Chest | 2 | STR +3, DEX -1, STR×0.10 | Skill_Mining1 |
| `Clothing_TravelerBoots.asset` | Feet | 1 | DEX +2 | Skill_Walk1 |
| `Clothing_MerchantCloak.asset` | Back | 2 | INT +3 | Skill_Trade1 |
| `Clothing_SmithApron.asset` | Chest | 1 | STR +1, INT +1 | none |
| `Clothing_RingOfVitality.asset` | Accessory1 | 1 | STR +1, INT +1 | none |
| `Clothing_RingOfAgility.asset` | Accessory2 | 1 | DEX +2 | none |
| `Clothing_PirateHat.asset` | Head | 2 | DEX +2, INT +1 | Skill_Persuasion1 |

---

## 3. ModuleItemData — структура

### 3.1 Наследование

```csharp
[CreateAssetMenu(fileName = "Module_", menuName = "Project C/Equipment/Module", order = 12)]
public class ModuleItemData : ItemData {
    public enum ModuleType : byte { Sensor = 0, Processor = 1, Weapon = 2, Utility = 3 }

    [Header("Equip")]
    public EquipSlot slot = EquipSlot.Module1;

    [Header("Type")]
    public ModuleType moduleType;

    [Header("Tier")]
    [Range(1, 10)] public int tier = 1;

    [Header("Effects (per-type)")]
    [Header("Sensor")]
    [Tooltip("Sensor range bonus (meters) — future use")]
    public float sensorRangeBonus;

    [Header("Processor")]
    [Tooltip("Crafting speed multiplier for Processor modules")]
    [Range(0f, 5f)] public float craftingSpeedMultiplier;

    [Header("Weapon")]
    [Tooltip("Damage bonus for Weapon modules — future use")]
    public float weaponDamageBonus;

    [Header("Utility")]
    [Tooltip("Generic stat bonuses")]
    public float strengthBonus;
    public float dexterityBonus;
    public float intelligenceBonus;

    [Header("Skill Requirements")]
    public SkillNodeConfig[] requiredSkills = Array.Empty<SkillNodeConfig>();

    [Header("Power Consumption (future)")]
    [Tooltip("Watts — for future ship power system")]
    public float powerConsumption;
}
```

### 3.2 Какие модули (примеры для теста)

| Filename | Slot | Type | Bonuses | Skills |
|----------|------|------|---------|--------|
| `Module_RangefinderMk1.asset` | Module1 | Sensor | sensorRange +10 | Skill_Tech1 |
| `Module_CraftingAssistant.asset` | Module2 | Processor | craftSpeed ×1.5 | Skill_Craft1 |
| `Module_DataAnalyzer.asset` | Module3 | Utility | INT +2 | Skill_Analysis1 |
| `Module_PowerEfficiency.asset` | Module1 | Utility | power ×0.85 (future) | Skill_Tech2 |

**Важно:** модули в этой сессии — **персонажные импланты** (как в Cyberpunk), НЕ модификации корабля. Корабельные модули уже есть через `ShipController.modules[]` — это отдельная подсистема.

---

## 4. EquipmentServer — RPC hub

### 4.1 Расположение

**Файл:** `Assets/_Project/Scripts/Equipment/EquipmentServer.cs`
**Namespace:** `ProjectC.Equipment`
**Scene-placed:** `BootstrapScene.unity` рядом с `[GatheringServer]`, `[CraftingServer]`, `[StatsServer]`

### 4.2 RPCs

```csharp
public class EquipmentServer : NetworkBehaviour {
    public static EquipmentServer Instance { get; private set; }

    public override void OnNetworkSpawn() {
        if (!IsServer) return;
        Instance = this;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn() {
        if (NetworkManager.Singleton != null) {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        if (Instance == this) Instance = null;
    }

    // === Client → Server ===

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestEquipRpc(int itemId, EquipSlot slot, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;

        if (!EquipmentWorld.Instance.TryEquip(clientId, itemId, slot, out var reason)) {
            SendEquipResult(clientId, EquipResultDto.Denied(reason));
            return;
        }
        SendEquipResult(clientId, EquipResultDto.Equipped(itemId, slot));
        // Recompute effective stats
        _statsServer?.RecomputeAndSendSnapshot(clientId);
        SendEquipmentSnapshotToOwner(clientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestUnequipRpc(EquipSlot slot, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;

        if (!EquipmentWorld.Instance.TryUnequip(clientId, slot, out var reason)) {
            SendEquipResult(clientId, EquipResultDto.Denied(reason));
            return;
        }
        SendEquipResult(clientId, EquipResultDto.Unequipped(slot));
        _statsServer?.RecomputeAndSendSnapshot(clientId);
        SendEquipmentSnapshotToOwner(clientId);
    }

    // === Server → Client (target RPCs через NetworkPlayer) ===
    private void SendEquipResult(ulong clientId, EquipResultDto result) {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
        netPlayer?.ReceiveEquipResultTargetRpc(result);
    }

    private void SendEquipmentSnapshotToOwner(ulong clientId) {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
        var equip = EquipmentWorld.Instance.GetEquipment(clientId);
        netPlayer?.ReceiveEquipmentSnapshotTargetRpc(BuildSnapshot(equip));
    }
}
```

### 4.3 EquipmentWorld.TryEquip — валидация

```csharp
public bool TryEquip(ulong clientId, int itemId, EquipSlot slot, out string reason) {
    reason = "";

    // 1. Item exists?
    if (!InventoryWorld.Instance.GetItemDataById(itemId, out var itemData)) {
        reason = "Предмет не найден"; return false;
    }

    // 2. Item is clothing/module?
    EquipSlot requiredSlot;
    SkillNodeConfig[] requiredSkills;
    if (itemData is ClothingItemData clothing) {
        requiredSlot = clothing.slot;
        requiredSkills = clothing.requiredSkills;
    } else if (itemData is ModuleItemData module) {
        requiredSlot = module.slot;
        requiredSkills = module.requiredSkills;
    } else {
        reason = "Этот предмет не надевается"; return false;
    }

    // 3. Slot match?
    if (requiredSlot != slot) {
        reason = $"Слот не подходит: нужен {requiredSlot}"; return false;
    }

    // 4. Skill requirements?
    if (requiredSkills != null) {
        var learned = SkillsWorld.Instance?.GetLearnedSkillIds(clientId) ?? new HashSet<string>();
        foreach (var skill in requiredSkills) {
            if (skill != null && !learned.Contains(skill.skillId)) {
                reason = $"Требуется навык: {skill.displayName}"; return false;
            }
        }
    }

    // 5. Item ownership (в инвентаре игрока)?
    var inventory = InventoryWorld.Instance.GetInventoryData(clientId);
    if (!inventory.ContainsItem(itemId)) {
        reason = "Предмета нет в инвентаре"; return false;
    }

    // 6. Slot empty (или unequip старого)?
    var equip = GetOrCreateEquipment(clientId);
    if (equip.TryGetItemId(slot, out var oldItemId)) {
        reason = "Слот занят, сначала снимите предмет"; return false;
        // (для MVP: не auto-unequip. Игрок сам unequip → equip)
    }

    // All checks passed
    equip.SetItem(slot, itemId);
    SetEquipment(clientId, equip);
    Debug.Log($"[EquipmentWorld] Player {clientId} equipped {itemData.itemName} in {slot}");
    return true;
}
```

### 4.4 EquipmentWorld.TryUnequip — простой

```csharp
public bool TryUnequip(ulong clientId, EquipSlot slot, out string reason) {
    reason = "";
    var equip = GetOrCreateEquipment(clientId);
    if (!equip.TryGetItemId(slot, out _)) {
        reason = "Слот пуст"; return false;
    }
    equip.ClearSlot(slot);
    SetEquipment(clientId, equip);
    return true;
}
```

---

## 5. EquipmentData — parallel arrays для NGO 2.x

### 5.1 Проблема и решение

**Проблема:** NGO 2.x `INetworkSerializable` не сериализует `Dictionary<EquipSlot, int>` (compile error или runtime NRE).

**Решение:** Fixed-size `byte[]` (slot occupied) + `int[]` (slot itemId). 13 slots × 5 bytes = 65 байт на snapshot — терпимо.

### 5.2 Структура

```csharp
public enum EquipSlot : byte {
    None = 0, Head = 1, Chest = 2, Legs = 3, Feet = 4, Back = 5, Hands = 6,
    Accessory1 = 7, Accessory2 = 8, WeaponMain = 9, WeaponOff = 10,
    Module1 = 20, Module2 = 21, Module3 = 22,
}

[Serializable]
public struct EquipmentData : INetworkSerializable {
    public const int SLOT_COUNT = 13;

    public byte[] slotOccupied;   // [i] = 0 or 1
    public int[] slotItemIds;     // [i] = itemId (если slotOccupied[i] == 1)

    public static EquipmentData Empty => new() {
        slotOccupied = new byte[SLOT_COUNT],
        slotItemIds = new int[SLOT_COUNT],
    };

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        if (slotOccupied == null) slotOccupied = new byte[SLOT_COUNT];
        if (slotItemIds == null) slotItemIds = new int[SLOT_COUNT];
        for (int i = 0; i < SLOT_COUNT; i++) {
            serializer.SerializeValue(ref slotOccupied[i]);
            serializer.SerializeValue(ref slotItemIds[i]);
        }
    }

    public bool TryGetItemId(EquipSlot slot, out int itemId) {
        int idx = SlotToIndex(slot);
        if (idx < 0 || slotOccupied[idx] == 0) { itemId = 0; return false; }
        itemId = slotItemIds[idx];
        return true;
    }

    public void SetItem(EquipSlot slot, int itemId) {
        int idx = SlotToIndex(slot);
        slotOccupied[idx] = 1;
        slotItemIds[idx] = itemId;
    }

    public void ClearSlot(EquipSlot slot) {
        int idx = SlotToIndex(slot);
        slotOccupied[idx] = 0;
        slotItemIds[idx] = 0;
    }

    public IEnumerable<EquipSlot> EnumerateOccupiedSlots() {
        for (int i = 0; i < SLOT_COUNT; i++) {
            if (slotOccupied[i] == 0) continue;
            yield return IndexToSlot(i);
        }
    }

    private static int SlotToIndex(EquipSlot slot) {
        int v = (int)slot;
        if (v == 0) return -1;
        if (v <= 10) return v;
        if (v >= 20 && v <= 22) return 11 + (v - 20);
        return -1;
    }

    private static EquipSlot IndexToSlot(int idx) {
        if (idx <= 10) return (EquipSlot)idx;
        return (EquipSlot)(20 + (idx - 11));
    }
}
```

### 5.3 Альтернативы (отвергнуты)

| Подход | Почему нет |
|--------|-----------|
| `Dictionary<EquipSlot, int>` | NGO 2.x не сериализует Dictionary |
| `FixedList64Bytes<EquipSlotItemId>` (NGO native) | verbose API, требует struct, менее типизированный |
| Per-slot NetworkVariable (13 vars на NetworkPlayer) | network bandwidth overhead, sync lag |
| `string[] slotNames` + `int[] slotItemIds` (parallel arrays) | хрупкий (имена могут typo), enum — лучше |

---

## 6. EquipmentClientState + UI

### 6.1 EquipmentClientState

```csharp
public class EquipmentClientState : MonoBehaviour {
    public static EquipmentClientState Instance { get; private set; }

    public EquipmentSnapshotDto? CurrentSnapshot { get; private set; }
    public event Action<EquipmentSnapshotDto> OnEquipmentUpdated;
    public event Action<EquipResultDto> OnEquipResult;

    private void Awake() { Instance = this; }

    public void OnEquipmentSnapshotReceived(EquipmentSnapshotDto snap) {
        CurrentSnapshot = snap;
        OnEquipmentUpdated?.Invoke(snap);
    }

    public void OnEquipResultReceived(EquipResultDto result) {
        OnEquipResult?.Invoke(result);
        // UI shows toast: "Надето: Шлем рабочего" или "Снято: Сапоги"
    }
}
```

### 6.2 UI integration в CharacterWindow

**UXML секция (clothing):**
```xml
<ui:VisualElement name="clothing-section" class="list-sub-section" style="display: none;">
    <ui:Label text="Одежда и экипировка" class="section-title" />
    <ui:VisualElement class="stats-grid">
        <!-- Per-slot row: Slot name | Equipped item | [EQUIP] [UNEQUIP] buttons -->
    </ui:VisualElement>
    <ui:Label text="Экипированные предметы дают бонусы к Силе/Ловкости/Интеллекту" class="placeholder-hint" />
</ui:VisualElement>

<ui:VisualElement name="modules-section" class="list-sub-section" style="display: none;">
    <ui:Label text="Модули" class="section-title" />
    <ui:VisualElement class="stats-grid">
        <!-- Per-slot row: Module slot | Equipped module | [EQUIP] [UNEQUIP] buttons -->
    </ui:VisualElement>
    <ui:Label text="Модули — импланты персонажа" class="placeholder-hint" />
</ui:VisualElement>
```

**CharacterWindow.cs — handler:**
```csharp
private EquipmentClientState _equipmentState;
private bool _isEquipmentSubscribed = false;
private List<EquipSlotRow> _equipmentCache = new();

private void SubscribeEquipment() {
    if (_isEquipmentSubscribed) return;
    _equipmentState = EquipmentClientState.Instance;
    if (_equipmentState == null) return;
    _equipmentState.OnEquipmentUpdated += HandleEquipmentSnapshot;
    _isEquipmentSubscribed = true;
}

private void UnsubscribeEquipment() { /* standard pattern */ }

private void HandleEquipmentSnapshot(EquipmentSnapshotDto snap) {
    RefreshEquipmentCache(snap);
    if (_activeTab == "progression" && _activeProgressionTab == "clothing") {
        RebuildClothingListView();
    }
}

private void RefreshEquipmentCache(EquipmentSnapshotDto snap) {
    _equipmentCache.Clear();
    foreach (var slot in snap.equip.EnumerateOccupiedSlots()) {
        var itemId = snap.equip.slotItemIds[EquipmentData.SlotToIndex(slot)];
        var itemName = InventoryWorld.Instance.GetItemName(itemId);
        _equipmentCache.Add(new EquipSlotRow { Slot = slot, ItemId = itemId, DisplayName = itemName });
    }
}

private void RebuildClothingListView() {
    _clothingList.itemsSource = _equipmentCache;
    _clothingList.RefreshItems();
    _clothingList.MarkDirtyRepaint();
}
```

---

## 7. Persistence (в CharacterSaveData)

### 7.1 EquipmentSave DTO

```csharp
[Serializable]
public class EquipmentSave {
    public byte[] slotOccupied = new byte[EquipmentData.SLOT_COUNT];
    public int[] slotItemIds = new int[EquipmentData.SLOT_COUNT];
}
```

### 7.2 Repository integration

В `EquipmentWorld.SavePlayer(clientId)` (вызывается из StatsServer disconnect):
```csharp
public void SavePlayer(ulong clientId, CharacterSaveData data) {
    var equip = GetEquipment(clientId);
    Array.Copy(equip.slotOccupied, data.equipment.slotOccupied, EquipmentData.SLOT_COUNT);
    Array.Copy(equip.slotItemIds, data.equipment.slotItemIds, EquipmentData.SLOT_COUNT);
}
```

В `EquipmentWorld.LoadPlayer(clientId, data)`:
```csharp
public void LoadPlayer(ulong clientId, CharacterSaveData data) {
    var equip = new EquipmentData();
    Array.Copy(data.equipment.slotOccupied, equip.slotOccupied, EquipmentData.SLOT_COUNT);
    Array.Copy(data.equipment.slotItemIds, equip.slotItemIds, EquipmentData.SLOT_COUNT);
    SetEquipment(clientId, equip);
}
```

---

## 8. Edge cases

### 8.1 Item уже продан/потрачен (но экипирован)

**Сценарий:** игрок экипировал шлем. Потом продал на рынке. Должно ли экипировка автоматически сняться?

**Решение:** В `InventoryWorld.RemoveItem` НЕ проверяем equipment (orthogonal concerns). Но UI показывает warning: "Этот предмет продан, но экипирован — будет снят при следующем equip". Альтернатива: в `EquipmentServer.RequestUnequip` предупреждаем если item уже не в инвентаре.

**MVP:** упрощение — оставляем экипировку, бонусы остаются (item "lost" но still equipped). Это странно но упрощает MVP.

### 8.2 Item type не ClothingItemData/ModuleItemData (но в EquipmentData)

**Сценарий:** баг в EquipmentWorld — сохранили itemId не-clothing/module в слот. Load → equip snapshot → UI пытается отобразить.

**Решение:** В `EquipmentClientState.OnEquipmentSnapshotReceived` — фильтруем: только items с `itemData is ClothingItemData or ModuleItemData`. Остальные показываем как "Unknown item" placeholder.

### 8.3 Equip slot conflict (2 items в один слот)

**Сценарий:** race condition — игрок быстро жмёт equip на разные items в один слот.

**Решение:** Rate-limit на server (5 ops/sec per client). Conflict resolve — first-write-wins. UI получает error "Слот занят".

### 8.4 SkillsWorld ещё не инициализирован при equip

**Сценарий:** Game start, игрок открыл CharacterWindow до SkillsServer.OnNetworkSpawn.

**Решение:** `SkillsWorld.Instance` может быть null в первые секунды. В `TryEquip` → `SkillsWorld.Instance?.GetLearnedSkillIds(clientId) ?? new HashSet<string>()` — safe fallback.

### 8.5 ClothingItemData.requiredSkills содержит null

**Сценарий:** designer оставил пустой slot в массиве (забыл заполнить).

**Решение:** В `TryEquip` → `if (skill != null && !learned.Contains(...))` — null-safe. В OnValidate SO — warning на null entries.

### 8.6 ItemRegistry не знает про ClothingItemData casting

**Сценарий:** `InventoryWorld.GetItemDataById(id)` возвращает `ItemData`. Casting к `ClothingItemData` — может быть cast exception если это не clothing.

**Решение:** В `EquipmentWorld.TryEquip` — `if (itemData is ClothingItemData clothing) { ... }` — type pattern matching, безопасно.

### 8.7 Effective stats recompute → stat snapshot mismatch

**Сценарий:** игрок экипировал шлем → effective STR изменилась → `StatsClientState` показывает базовую STR (без бонусов).

**Решение:** `StatsSnapshotDto` имеет поле `effectiveStrength`, `effectiveDexterity`, `effectiveIntelligence` (computed в StatsServer). Recompute в `EquipmentServer.RequestEquip/Unequip` → call `StatsServer.RecomputeAndSendSnapshot(clientId)`.

---

## 9. Pitfalls

### 9.1 EquipmentData.SLOT_COUNT change → все сохранения сломаются

**Проблема:** Если добавим `Module4 = 23`, нужно менять `SLOT_COUNT = 14`. Все сохранённые файлы `character_<clientId>.json` имеют 13-байтные массивы — JsonUtility парсит корректно (zero-initialized), но mapping слотов смещается.

**Решение:** Версионирование сохранений: `CharacterSaveData.serializerVersion = 1`. При load — проверка, миграция если нужно. MVP: просто документируем что добавлять слоты = ломает совместимость.

### 9.2 StatsClientState не знает про Equipment changes

**Проблема:** Игрок экипировал предмет → effective STR изменился → `StatsClientState.CurrentStats` НЕ обновился (показывает базовую STR).

**Решение:** `EquipmentServer.RequestEquip` → `StatsServer.RecomputeAndSendSnapshot(clientId)` → `NetworkPlayer.ReceiveStatsSnapshotTargetRpc(effectiveStats)` → `StatsClientState.OnStatsSnapshotReceived(snap)` → UI обновляется.

### 9.3 Skill-bonus в Equipment ≠ Skill-bonus в SkillsWorld

**Проблема:** Если игрок изучил навык "+2 INT" и экипировал шлем "+1 INT" — оба применяются. Если потом забыл навык (forget) — бонус исчезает. Если unequip шлем — бонус исчезает.

**Решение:** Оба источника считаются в `RecomputeEffectiveStat` (см. §2.2). Recompute trigger: equip change + skill change. Возможно: `SkillsServer.RequestForgetSkill` → recompute → send snapshot.

### 9.4 ClothingItemData не регистрируется в ItemRegistry как clothing

**Проблема:** `ItemRegistry.RegisterAllItems` собирает все `ItemData` подтипы. `ClothingItemData : ItemData` — будет зарегистрирован как ItemData, type info сохранится в asset.

**Решение:** В `InventoryWorld.GetItemDataById` возвращается `ItemData`. Casting к `ClothingItemData`/`ModuleItemData` делаем в runtime. `is ClothingItemData` — pattern matching.

### 9.5 Equipment UI — много пустых слотов

**Сценарий:** у игрока 3 экипированных предмета из 13. UI показывает 13 строк с 10 пустыми — шумно.

**Решение:** UI показывает **только экипированные** (foreach occupied slots). Пустые слоты — "Drag item here" placeholder (Phase 2). MVP — показываем только equipped.

### 9.6 Drag-and-drop для equip (Phase 2)

**Проблема:** drag-and-drop для equip — это +1 тикет (T-P11 или позже).

**Решение:** MVP — кнопки `[EQUIP]` `[UNEQUIP]` рядом с предметом в инвентаре. Drag-and-drop — Phase 2.

### 9.7 Module `Module1` конфликтует с будущими модулями корабля

**Сценарий:** игрок экипировал `Module_CraftingAssistant` в слот `Module1`. Потом садится в корабль — корабль имеет свои модули.

**Решение:** Equip slot для персонажа — это **персонажные импланты**. Корабельные модули — отдельная подсистема (`ShipController.modules[]`). **НЕ путать.** В UI чётко разделено.

---

## 10. Что НЕ делаем

- ❌ Не делаем drag-and-drop для equip (MVP — кнопки)
- ❌ Не делаем Module4+ (только 3 module слота)
- ❌ Не делаем корабельные модули через EquipmentServer (отдельная подсистема)
- ❌ Не делаем versioning сохранений (MVP: добавлять слоты = ломает совместимость)
- ❌ Не делаем `Equipped` items как отдельный inventory list (slot в EquipmentData)
- ❌ Не пишем `.meta` / `.asmdef` файлы
- ❌ Не делаем equip через InventoryUI (Tab) — только через CharacterWindow (P)
- ❌ Не делаем power consumption для модулей в MVP (поле есть, но не используется)
