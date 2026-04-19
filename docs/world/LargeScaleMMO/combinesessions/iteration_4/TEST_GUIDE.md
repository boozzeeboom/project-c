# Iteration 4: Руководство по тестированию

**Дата:** 19.04.2026  
**Цель:** Проверить работоспособность системы сундуков в Unity

---

## 📋 ПЕРЕД НАЧАЛОМ ТЕСТИРОВАНИЯ

### Что проверить в проекте:

1. ✅ Код компилируется без ошибок
2. ✅ Все файлы на месте:
   - `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs`
   - `Assets/_Project/Scripts/Player/ItemPickupSystem.cs`
   - `Assets/_Project/Scripts/World/Streaming/ChunkNetworkSpawner.cs`

---

## ⚠️ КЛЮЧЕВЫЕ ПРИЧИНЫ НЕРАБОТЫ

### 1. NetworkObject НЕ добавлен на префаб!
Это ГЛАВНАЯ причина. Без NetworkObject сундук не работает.

**Проверка:**
- Выбрать префаб сундука в Project
- В Inspector: должен быть компонент `NetworkObject`
- Если нет → добавить: `Add Component → NetworkObject`

### 2. Префаб НЕ зарегистрирован в NetworkManager

**Проверка:**
- Edit → Project Settings → Netcode → Network Prefabs
- Добавить префаб в список

### 3. Нужен режим HOST (не чистый клиент)

ServerRpc работает ТОЛЬКО когда есть сервер.

**Проверка:**
- `Multiplayer > Start New Session > Play as Host`
- Или использовать ParrelSync для второго клиента + запустить как Host

### 4. Player не в пешем режиме

ItemPickupSystem работает только когда `IsWalking = true`.

**Проверка:**
- Игрок должен быть пешком (не на корабле/глайдере)
- PlayerStateMachine.IsWalking должен возвращать true

---

## 🧪 ТЕСТ 1: Одиночная игра (Play Mode) — НЕ РАБОТАЕТ

### Если не работает, проверить по порядку:

#### Шаг 1: Создать правильный префаб

```
1. Создать пустой GameObject
2. Добавить компоненты:
   - NetworkObject            ← ОБЯЗАТЕЛЬНО!
   - BoxCollider (isTrigger = true)
   - NetworkChestContainer
3. Перетащить в папку Prefabs
4. Добавить на сцену
```

#### Шаг 2: Включить Debug режим

В Inspector NetworkChestContainer:
- Поставить галочку `Debug Mode = true`

#### Шаг 3: Запустить как Host

```
В Unity: Multiplayer > Start New Session > Play as Host
```

#### Шаг 4: Смотреть логи консоли

**При спавне (OnNetworkSpawn):**
```
[NetworkChestContainer] OnNetworkSpawn - IsServer=True, OwnerClientId=X
```

**При нажатии E (TryOpen):**
```
[NetworkChestContainer] TryOpen - Spawned=True, IsServer=True, IsHost=True, IsOpen=False
[NetworkChestContainer] RequestOpenChestServerRpc received from client
```

**Если видишь это — значит работает:**
```
[NetworkChestContainer] TryOpen FAILED: Not spawned (no NetworkObject?)
                          ↑↑↑ ЭТО ГЛАВНАЯ ПРИЧИНА
```

---

## 🧪 ТЕСТ 2: Проверка ItemPickupSystem

Включить `debugMode = true` в ItemPickupSystem.

**Ожидаемые логи:**
```
[ItemPickupSystem] Found 1 chests
[ItemPickupSystem] Chest ChestPrefab: dist=2.5, radius=3.0
[ItemPickupSystem] Nearest chest: ChestPrefab at (X,Y,Z)
[ItemPickupSystem] E pressed! Walking=True, Chest=True, Pickup=False
[ItemPickupSystem] Opening chest: ChestPrefab
```

---

## 📊 ЧЕКЛИСТ ПРОВЕРКИ

| # | Проверка | Где | Статус |
|---|----------|-----|--------|
| 1 | NetworkObject на префабе | Inspector | ☐ |
| 2 | NetworkChestContainer на префабе | Inspector | ☐ |
| 3 | LootTable привязан | Inspector | ☐ |
| 4 | Debug Mode включён | Inspector | ☐ |
| 5 | Запуск как Host | Unity Menu | ☐ |
| 6 | IsWalking = true | Debug log | ☐ |

---

## 🔍 РАСШИФРОВКА ЛОГОВ

### NetworkChestContainer.TryOpen():
- `Spawned=False` → Нет NetworkObject на префабе!
- `IsServer=False` → Нужно запустить как Host
- `IsHost=False` → Нужно запустить как Host

### ItemPickupSystem.OnPickupPressed():
- `Walking=False` → Игрок не в пешем режиме

---

## 📝 ЗАМЕТКИ ДЛЯ ТЕСТИРОВАНИЯ

```
Дата: _______
Тестировщик: _______

Проверка NetworkObject: ☐ Есть / ☐ Нет
Проверка Debug Mode: ☐ Включён / ☐ Выключен

Лог OnNetworkSpawn: _____________________
Лог TryOpen: _____________________________

Тест 1: ☐ Успешно / ☐ Неудачно
Заметки: 
```

---

**Автор:** Claude Code  
**Дата создания:** 19.04.2026, 14:20 MSK  
**Версия:** v2 — Добавлена диагностика проблем