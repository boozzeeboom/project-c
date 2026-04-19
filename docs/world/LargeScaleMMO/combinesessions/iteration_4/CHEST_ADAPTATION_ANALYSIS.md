# Iteration 4: Анализ адаптации существующих систем

**Дата:** 19.04.2026, 12:30 MSK  
**Статус:** В РАБОТЕ

---

## 📊 СУЩЕСТВУЮЩИЕ СИСТЕМЫ

### Что уже реализовано:

| Система | Файл | Статус | Проблема |
|---------|------|--------|----------|
| ItemData | `Core/ItemType.cs` | ✅ Работает | Нет network sync ID |
| ItemType enum | `Core/ItemType.cs` | ✅ Работает | 8 типов (Resources, Equipment, Food...) |
| Inventory | `Core/Inventory.cs` | ✅ Работает | Локальный, без сети |
| NetworkInventory | `Core/NetworkInventory.cs` | ⚠️ Частично | Нужен интеграция с сундуками |
| LootTable | `Core/LootTable.cs` | ✅ Работает | Нет network передачи |
| ChestContainer | `Core/ChestContainer.cs` | ❌ Не адаптирован | Нет NetworkObject, RPC |
| PickupItem | `Core/PickupItem.cs` | ✅ Работает | Нужен NetworkObject |
| ItemPickupSystem | `Player/ItemPickupSystem.cs` | ✅ Работает | Локальный подбор |
| InteractableManager | `Core/InteractableManager.cs` | ⚠️ Не используется | ItemPickupSystem использует FindObjectsByType |

---

## 🔴 ПРОБЛЕМЫ АДАПТАЦИИ

### 1. ChestContainer НЕ имеет NetworkObject

```csharp
// Текущий код (ChestContainer.cs)
public class ChestContainer : MonoBehaviour, Core.IInteractable
{
    // НЕТ NetworkObject!
    // НЕТ RPC!
}
```

**Проблема:** 
- Сундук открывается только локально
- Другие игроки не видят открытие
- Предметы не добавляются в NetworkInventory

### 2. LootTable не передаётся по сети

```csharp
// ChestContainer.Open()
public void Open()
{
    if (_isOpen) return;
    _isOpen = true;
    // Animation only — НЕТ добавления в инвентарь!
}
```

**Проблема:**
- Предметы из LootTable.GenerateLoot() не передаются
- Нет ServerRpc для запроса лута

### 3. ChunkNetworkSpawner не спавнит сундуки

```csharp
// ChunkNetworkSpawner.cs — найти сундуки?
```

**Проблема:**
- Нет префаба сундука для спавна
- Нет регистрации сундуков в системе чанков

---

## ✅ ПЛАН АДАПТАЦИИ

### Этап 1: NetworkChestContainer (АДАПТАЦИЯ)

```csharp
// Новый файл: Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs
public class NetworkChestContainer : NetworkBehaviour, Core.IInteractable
{
    // Существующие поля из ChestContainer
    public LootTable lootTable;
    public float openRadius = 3f;
    
    // Network fields
    private NetworkVariable<bool> _isOpen = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    
    // RPC: запрос открытия (от клиента к серверу)
    [ServerRpc]
    private void RequestOpenChestServerRpc(ServerRpcParams rpcParams = default)
    {
        // Валидация: проверка дистанции
        // Генерация лута из LootTable
        // Добавление предметов в NetworkInventory игрока
        // Установка _isOpen = true
        // Вызов ClientRpc для анимации у всех
    }
    
    // RPC: анимация открытия (от сервера всем)
    [ClientRpc]
    private void OpenChestClientRpc()
    {
        // Запустить анимацию открытия
        PlayOpenAnimation();
    }
}
```

### Этап 2: Интеграция с NetworkInventory

```csharp
// В RequestOpenChestServerRpc:
public void RequestOpenChestServerRpc()
{
    var player = GetComponent<NetworkPlayer>();
    if (player == null) return;
    
    // Получаем NetworkInventory игрока
    var inventory = player.GetComponent<Core.NetworkInventory>();
    if (inventory == null) return;
    
    // Генерируем лут
    var loot = lootTable.GenerateLoot();
    
    // Добавляем предметы в инвентарь
    foreach (var item in loot)
    {
        int itemId = Core.NetworkInventory.GetItemId(item);
        inventory.AddItem(itemId, item.itemType);
    }
    
    // Открываем сундук
    _isOpen.Value = true;
    OpenChestClientRpc();
}
```

### Этап 3: Регистрация в ChunkNetworkSpawner

```csharp
// ChunkNetworkSpawner.cs — добавить:
// [SerializeField] private NetworkObject chestPrefab;
// [SerializeField] private float chestSpawnChance = 0.3f;
```

### Этап 4: ItemPickupSystem — использовать InteractableManager

```csharp
// ItemPickupSystem.FindNearestInteractable() — изменить:
// Было: FindObjectsByType<ChestContainer>(FindObjectsInactive.Include)
// Стало: InteractableManager.FindNearestChest(position, range)
```

---

## 📁 ФАЙЛЫ ДЛЯ АДАПТАЦИИ

### Изменение существующих:
| Файл | Изменение |
|------|-----------|
| `ChestContainer.cs` | Добавить NetworkObject + RPC или создать NetworkChestContainer |
| `ItemPickupSystem.cs` | Использовать InteractableManager |
| `ChunkNetworkSpawner.cs` | Добавить спавн сундуков |

### Новые файлы:
| Файл | Описание |
|------|----------|
| `World/Chest/NetworkChestContainer.cs` | Сетевой сундук |
| `World/Chest/ChestLootHandler.cs` | Обработка лута на сервере |
| `docs/gdd/GDD_11_Inventory_Items.md` | Обновить документацию |

---

## 🎯 ПРИОРИТЕТЫ

| # | Задача | Приоритет | Сложность |
|---|--------|-----------|-----------|
| 1 | NetworkChestContainer | КРИТИЧНЫЙ | Средний |
| 2 | Интеграция с NetworkInventory | КРИТИЧНЫЙ | Средний |
| 3 | ChunkNetworkSpawner спавн сундуков | ВЫСОКИЙ | Низкий |
| 4 | ItemPickupSystem оптимизация | СРЕДНИЙ | Низкий |
| 5 | Обновление документации | СРЕДНИЙ | Низкий |

---

## 🔜 СЛЕДУЮЩИЕ ШАГИ

1. Создать `NetworkChestContainer.cs` на основе `ChestContainer.cs`
2. Добавить ServerRpc/ClientRpc для открытия
3. Интегрировать с `NetworkInventory.AddItem()`
4. Добавить префаб сундука в `ChunkNetworkSpawner`
5. Протестировать в multiplayer

---

**Обновлено:** 19.04.2026, 12:30 MSK  
**Автор:** Claude Code