# 🔴 РАЗБОР ПОЛЁТОВ: Что сломалось и как починили

**Дата:** 14 апреля 2026  
**Агент:** Qwen Code Agent  
**Контекст:** После внедрения WorldStreamingManager (Фаза 1) всё сломалось —  
камера персонажа не работала, иерархия сцены разрушалась.

---

## 🔍 Выявленные баги (от критического к менее критичному)

---

### 🔴 БАГ #1 (КРИТИЧЕСКИЙ): ThirdPersonCamera → FloatingOriginMP → уничтожение иерархии сцены

**Файл:** `Assets/_Project/Scripts/Core/ThirdPersonCamera.cs`  
**Строки (до фикса):** 79–105

**Что происходило:**

```
NetworkPlayer.OnNetworkSpawn()
  └─ SpawnCamera()
       └─ Instantiate(cameraPrefab, transform)   ← камера создаётся как ДОЧЕРНИЙ объект игрока
            └─ ThirdPersonCamera.Awake()
                 └─ gameObject.AddComponent<FloatingOriginMP>()  ← FloatingOriginMP добавляется на камеру
                      └─ FloatingOriginMP.Awake()
                           └─ FindOrCreateWorldRoots()
                                ← "Mountains", "Clouds" и т.д. НЕ НАЙДЕНЫ в сцене
                           └─ CollectWorldObjects()   ← ☠️ КАТАСТРОФА
                                └─ scene.GetRootGameObjects()  → [NetworkPlayer, NetworkManager, WorldStreamingManager, ...]
                                └─ РАПАРЕНЧИВАЕТ ВСЕ ROOT-ОБЪЕКТЫ ПОД "WorldRoot"
                                     ← CharacterController ломается (сменился parent)
                                     ← NetworkManager теряет DontDestroyOnLoad
                                     ← Вся сетевая логика перестаёт работать
```

**Результат:**  
- Камера персонажа не следит за игроком  
- CharacterController падает с ошибками  
- Все NetworkObject теряют иерархию  
- Сцена в полном хаосе после первого же подключения игрока

**Исправление:**
```csharp
// УДАЛЕНО из ThirdPersonCamera.Awake():
// var floatingOriginMP = gameObject.AddComponent<FloatingOriginMP>();
// FloatingOriginMP должен быть на ОТДЕЛЬНОМ объекте в сцене,
// а НЕ динамически добавляться на камеру.
```

---

### 🔴 БАГ #2 (КРИТИЧЕСКИЙ): FloatingOriginMP.CollectWorldObjects() — опасный fallback

**Файл:** `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`  
**Метод:** `FindOrCreateWorldRoots()` → `CollectWorldObjects()`

**Что происходило:**

Когда `FloatingOriginMP` не находил объекты с именами из `worldRootNames[]`  
(["Mountains", "Clouds", "Farms", "TradeZones", "World", "WorldRoot"]),  
он создавал новый "WorldRoot" и перемещал под него **все** объекты сцены.

Это был "защитный" механизм, который стал причиной катастрофы.

**Исправление:**
```csharp
// БЫЛО (опасно):
if (_worldRoots.Count == 0) {
    var worldRoot = new GameObject("WorldRoot");
    CollectWorldObjects(worldRoot);  // ☠️ рапаренчивает ВСЁ
}

// СТАЛО (безопасно):
if (_worldRoots.Count == 0) {
    Debug.LogWarning("[FloatingOriginMP] Не найдено ни одного world root. " +
        "Создайте в сцене пустой GameObject с именем 'WorldRoot'...");
    // Компонент сам отключается через проверку _worldRoots.Count == 0 ниже
}
```

> ⚠️ **Для работы FloatingOriginMP в сцене теперь НУЖЕН** GameObject с именем `WorldRoot`  
> (или "Mountains", "Clouds", "Farms", "World"). Горы, облака и world-объекты должны быть его детьми.

---

### 🟡 БАГ #3 (СРЕДНИЙ): Камера как дочерний объект игрока

**Файл:** `Assets/_Project/Scripts/Player/NetworkPlayer.cs`  
**Метод:** `SpawnCamera()`

**Что происходило:**

```csharp
// БЫЛО (неправильно):
var camObj = Instantiate(cameraPrefab.gameObject, transform);  // ← transform = игрок!
// Камера становилась дочерним объектом NetworkPlayer
// Это создавало двойное смещение позиции:
// 1. Камера двигается с игроком через parenting
// 2. ThirdPersonCamera.LateUpdate() перемещает камеру на орбиту
// = мерцание и неправильное положение камеры
```

**Исправление:**
```csharp
// СТАЛО (правильно):
var camObj = Instantiate(cameraPrefab.gameObject);  // ← без parent
camObj.name = $"ThirdPersonCamera_{OwnerClientId}";
_myCamera = camObj.GetComponent<ThirdPersonCamera>();
if (_myCamera != null) {
    _myCamera.SetTarget(transform);
    _myCamera.InitializeCamera();  // ← новый метод
}
```

---

### 🟡 БАГ #4 (СРЕДНИЙ): Отсутствие cursor lock в ThirdPersonCamera

**Файл:** `Assets/_Project/Scripts/Core/ThirdPersonCamera.cs`

**Что происходило:**  
`WorldCamera.cs` лочил курсор в `Start()`. После перехода на `ThirdPersonCamera` курсор  
оставался видимым и не залоченным → мышь двигала UI вместо камеры.

**Исправление — новый метод `InitializeCamera()`:**
```csharp
public void InitializeCamera() {
    // ...
    Cursor.lockState = CursorLockMode.Locked;
    Cursor.visible = false;
    // ...
    _cameraInitialized = true;
}
```

---

### 🟡 БАГ #5 (СРЕДНИЙ): Два конфликтующих FloatingOrigin

**Файлы:**
- `Assets/_Project/Scripts/World/Core/FloatingOrigin.cs` — старый, используется в `WorldCamera.cs`
- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs` — новый, для мультиплеера

**Ситуация:**  
Если в сцене есть `WorldCamera` (использует старый `FloatingOrigin`) И `ThirdPersonCamera`  
(использовала новый `FloatingOriginMP`) → два сдвига мира за кадр → объекты улетают бесконечно.

**Статус:** Частично исправлено (убрали добавление FloatingOriginMP из ThirdPersonCamera).  
**Рекомендация:** Проверить, есть ли `WorldCamera` в сцене. Если используется `ThirdPersonCamera` —  
`WorldCamera` должна быть отключена или удалена из сцены.

---

## ✅ Что изменено (файлы)

| Файл | Изменение |
|------|-----------|
| `ThirdPersonCamera.cs` | Убран auto-add FloatingOriginMP; добавлен `InitializeCamera()` с cursor lock; убран `using ProjectC.World.Streaming` |
| `FloatingOriginMP.cs` | `CollectWorldObjects()` больше не вызывается — graceful disable при отсутствии roots |
| `NetworkPlayer.cs` | `SpawnCamera()` — камера спавнится без parent; вызывает `InitializeCamera()` |

---

## 📋 Что делать дальше в Unity Editor

### 1. Создать WorldRoot в сцене
```
Hierarchy → Create Empty → переименовать в "WorldRoot"
Все горы, облака, world-объекты → перетащить под WorldRoot
```

### 2. Добавить FloatingOriginMP правильно
```
WorldStreamingManager (или любой другой объект) → Add Component → FloatingOriginMP
Назначить Camera (ThirdPersonCamera или основная Camera) в поле Camera
```

> **ИЛИ**: оставить FloatingOriginMP на Camera-объекте в сцене (не на спавнящемся prefab).

### 3. Проверить наличие WorldCamera в сцене
```
Если ThirdPersonCamera используется → WorldCamera должна быть DISABLED или удалена
Два camera-компонента с FloatingOrigin → двойной сдвиг мира
```

### 4. Протестировать
```
1. Enter Play Mode
2. Start Host (NetworkUI)
3. Проверить: камера следует за игроком ✓
4. Проверить: WASD двигает персонажа ✓
5. Проверить: мышь вращает камеру ✓
6. Проверить Console: нет ошибок FloatingOriginMP ✓
```

---

## 🧠 Урок: архитектурная ошибка

**Проблема была в "умном" инициализаторе** (`CollectWorldObjects`), который пытался  
автоматически "починить" отсутствие конфигурации, но делал это деструктивно.

**Правило:** Если компонент не может найти нужные объекты — он должен **отключиться с предупреждением**,  
а НЕ пытаться "исправить ситуацию" рапаренчиванием всей сцены.

---

*Документ создан агентом после анализа кода. Следующий шаг: тестирование в Play Mode.*
