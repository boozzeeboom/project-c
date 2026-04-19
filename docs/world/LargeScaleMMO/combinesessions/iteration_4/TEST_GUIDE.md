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

## 🧪 ТЕСТ 1: Одиночная игра (Play Mode)

### Цель: Проверить базовое открытие сундука

### Шаги:

1. **Создать тестовый префаб сундука:**
   - Создать пустой GameObject
   - Добавить компоненты:
     - `NetworkObject`
     - `NetworkChestContainer`
   - Перетащить в папку Prefabs
   - Добавить на сцену

2. **Настроить LootTable:**
   - Создать `LootTable` (ScriptableObject) если нет
   - Привязать к NetworkChestContainer в Inspector

3. **Запустить Play Mode:**
   - Нажать Play в Unity
   - Видеть сундук на сцене

4. **Взаимодействие:**
   - Подойти к сундуку (нужно Player с ItemPickupSystem)
   - Нажать E
   - Сундук должен открыться (анимация поворота/масштаба)

5. **Проверить:**
   - Существует ли Player с ItemPickupSystem на сцене?
   - Работает ли проверка `IsWalking`?

---

## 🧪 ТЕСТ 2: Локальный мультиплеер (Host)

### Цель: Проверить сетевую синхронизацию

### Шаги:

1. **Подготовить NetworkManager:**
   - Проверить настройки `NetworkManager` на сцене
   - Убедиться что сундук зарегистрирован в `NetworkPrefabs`

2. **Запустить как Host:**
   - В Unity: `Multiplayer > Start New Session > Play as Host`
   - Или использовать ParrelSync для второго клиента

3. **Подойти к сундуку:**
   - Host нажимает E рядом с сундуком
   - Наблюдать: сундук открывается анимация

4. **Проверить синхронизацию:**
   - Client видит открытие сундука?
   - _isOpen синхронизируется на все клиенты?

---

## 🧪 ТЕСТ 3: ChunkNetworkSpawner

### Цель: Проверить спавн сундуков через систему чанков

### Шаги:

1. **Найти ChunkNetworkSpawner:**
   - На сцене или в сцене должен быть GameObject с `ChunkNetworkSpawner`

2. **Проверить настройки:**
   - В Inspector должен быть `chestPrefab` — префаб сундука

3. **Запустить игру:**
   - ChunkNetworkSpawner должен автоматически спавнить сундуки
   - Проверить в Hierarchy: появляются ли NetworkObject сундуков

4. **Открыть консоль:**
   - Должны быть логи:
     - `[ChunkNetworkSpawner] Spawning network objects for chunk X`
     - `[NetworkChestContainer] Added N items to player X`

---

## 📊 ЧЕКЛИСТ ПРОВЕРКИ

| # | Проверка | Статус |
|---|----------|--------|
| 1 | Код компилируется | ☐ |
| 2 | NetworkChestContainer на префабе | ☐ |
| 3 | NetworkObject компонент добавлен | ☐ |
| 4 | LootTable привязан | ☐ |
| 5 | ItemPickupSystem на игроке | ☐ |
| 6 | PlayerStateMachine доступен | ☐ |
| 7 | ChunkNetworkSpawner на сцене | ☐ |
| 8 | chestPrefab настроен | ☐ |

---

## 🔍 ОЖИДАЕМЫЕ ЛОГИ В КОНСОЛИ

### При открытии сундука:

```
[NetworkChestContainer] Client 0 too far: 4.2m (max: 3.0m)  ← клиент далеко
[NetworkChestContainer] Added 2 items to player 0             ← успех
```

### При спавне ChunkNetworkSpawner:

```
[ChunkNetworkSpawner] Spawning network objects for chunk (0,0,0)
[ChunkNetworkSpawner] Spawned 1 network objects for chunk (0,0,0)
```

---

## ⚠️ ВОЗМОЖНЫЕ ПРОБЛЕМЫ И РЕШЕНИЯ

| Проблема | Решение |
|----------|---------|
| Сундук не открывается | Проверить IsWalking = true у Player |
| Клиент не видит открытие | Проверить NetworkObject на префабе |
| Нет предметов в инвентаре | Проверить LootTable, NetworkInventory |
| ChunkNetworkSpawner не работает | Проверить chestPrefab, WorldChunkManager |

---

## 📝 ЗАМЕТКИ ДЛЯ ТЕСТИРОВАНИЯ

```
Дата: _______
Тестировщик: _______

Тест 1: ☐ Успешно / ☐ Неудачно
Заметки: 

Тест 2: ☐ Успешно / ☐ Неудачно  
Заметки:

Тест 3: ☐ Успешно / ☐ Неудачно
Заметки:

Общие замечания:
```

---

**Автор:** Claude Code  
**Дата создания:** 19.04.2026, 13:10 MSK