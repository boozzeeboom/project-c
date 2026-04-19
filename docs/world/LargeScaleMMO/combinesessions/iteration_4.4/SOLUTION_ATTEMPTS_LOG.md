# NPC System — Solution Attempts Log

**Iteration:** 4.4  
**Дата:** 19.04.2026  
**Статус:** ✅ ЗАВЕРШЕНО

---

## Созданные файлы

### Scripts (Assets/_Project/Scripts/World/Npc/)

| Файл | Описание | Статус |
|------|----------|--------|
| `NpcData.cs` | ScriptableObject с данными NPC | ✅ |
| `NpcEntity.cs` | MonoBehaviour с NetworkObject | ✅ |
| `NpcInteraction.cs` | IInteractable компонент | ✅ |
| `NpcDialogueManager.cs` | Singleton для диалогов | ✅ |

### Updated Files

| Файл | Изменение | Статус |
|------|-----------|--------|
| `InteractableManager.cs` | Добавлена поддержка NPC | ✅ |

### Assets

| Файл | Описание | Статус |
|------|----------|--------|
| `Example_TraderMarcus.asset` | Пример NPC (создать в Unity) | 📋 |

---

## Исправленные ошибки компиляции

### 1. CS0246: NpcInteraction type not found

**Причина:** Circular reference / namespace issue in InteractableManager.cs

**Решение:** Изменен тип списка на `List<object>` с явным приведением типов

**Файл:** `InteractableManager.cs`

```csharp
// До:
private static readonly List<World.Npc.NpcInteraction> _npcs = ...;

// После:
private static readonly List<object> _npcs = ...;
public static IReadOnlyList<object> GetNpcs() => _npcs;
```

---

### 2. CS0618: GetInstanceID() deprecated

**Причина:** Unity 6 deprecates `Object.GetInstanceID()`

**Решение:** Заменен на `GetHashCode()`

**Файлы:** `NpcInteraction.cs`, `NpcEntity.cs`

```csharp
// До:
? $"{npcData.npcId}_{gameObject.GetInstanceID()}"

// После:
? $"{npcData.npcId}_{GetHashCode()}"
```

---

### 3. CS0618: FindObjectOfType deprecated

**Причина:** Unity 6 deprecates `Object.FindObjectOfType<T>()`

**Решение:** Заменен на `Object.FindFirstObjectByType<T>()`

**Файл:** `NpcDialogueManager.cs`

```csharp
// До:
_instance = FindObjectOfType<NpcDialogueManager>();

// После:
_instance = Object.FindFirstObjectByType<NpcDialogueManager>();
```

---

### 4. GUID errors in .asset file

**Причина:** Нельзя создать ScriptableObject .asset вручную с валидным GUID

**Решение:** Создавать через Unity Editor: Right-click → Create → Project C → NPC Data

---

## Как создать NPC в Unity

### Шаг 1: Создать NpcData ScriptableObject

1. В Project window: Right-click → Create → Project C → NPC Data
2. Назвать файл, например: `TraderMarcus.asset`
3. Заполнить поля в Inspector:
   - `npcId`: `trader_marcus_01`
   - `displayName`: `Trader Marcus`
   - `faction`: `FreeTraders`
   - `rootNodeId`: `start`
   - Добавить диалоги

### Шаг 2: Создать NPC в сцене

1. Создать пустой GameObject
2. Добавить компоненты:
   - `NpcInteraction` (обязательно)
   - `NpcEntity` (опционально, для анимации и сетевой синхронизации)
3. В компоненте `NpcInteraction` указать `NpcData` asset

### Шаг 3: Настроить диалоги

В NpcData добавить DialogueNode массив:

```csharp
Dialogues = new DialogueNode[]
{
    new DialogueNode
    {
        nodeId = "start",
        nodeType = DialogueNodeType.Text,
        text = "Hello traveler!",
        options = new DialogueOption[]
        {
            new DialogueOption { text = "Hi", nextNodeId = "greeting" },
            new DialogueOption { text = "Goodbye", nextNodeId = "farewell" }
        }
    },
    // ... more nodes
}
```

### Шаг 4: Создать UI для диалогов

1. Создать Canvas с Panel
2. Добавить компонент `NpcDialogueManager` на пустой объект
3. Привязать UI элементы в Inspector:
   - `Dialogue Panel`
   - `Npc Name Text`
   - `Dialogue Text`
   - `Options Container`
   - `Option Button Prefab`

---

## Интеграция с системой взаимодействия

NPC использует существующую архитектуру:

```csharp
// NpcInteraction реализует IInteractable
public class NpcInteraction : MonoBehaviour, IInteractable
{
    public string InstanceId { get; }
    public string DisplayName { get; }
    public float InteractionRadius { get; }
    public Vector3 Position { get; }
    
    public void Interact()
    {
        // Открывает диалог через NpcDialogueManager
    }
}
```

InteractableManager регистрирует NPC:

```csharp
public static void RegisterNpc(NpcInteraction npc) { ... }
public static void UnregisterNpc(NpcInteraction npc) { ... }
public static NpcInteraction FindNearestNpc(Vector3 pos, float range) { ... }
```

---

## Сетевая синхронизация

NpcEntity поддерживает синхронизацию состояний:

```csharp
// NetworkVariable для состояния NPC
private NetworkVariable<NpcState> _networkState = new NetworkVariable<NpcState>(
    NpcState.Idle,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);

// Сервер управляет состоянием
public void SetState(NpcState newState)
{
    if (IsServer)
        _networkState.Value = newState;
    else
        _currentState = newState; // Локально для клиента
}
```

---

## Следующие шаги (TODO)

1. **Интеграция с контрактами** — подключить ContractSystem к диалогам
2. **UI диалогов** — создать prefab диалоговой панели
3. **Система репутации** — добавить проверки reputation в DialogueOption
4. **Система предметов** — подключить инвентарь к giveItemId
5. **Анимации NPC** — добавить базовые анимации (idle, walk, talk)

---

**Обновлено:** 19.04.2026, 20:47 MSK