# ITERATION 4.4: NPC System — Master Prompt

**Дата создания:** 19.04.2026  
**Статус:** 🔄 В РАБОТЕ  
**Проблема:** NPC система не реализована (задача 4.4 из ITERATION 4)

---

## 📋 ЗАДАЧИ ITERATION 4.4

| # | Задача | Приоритет | Статус |
|---|--------|-----------|--------|
| 4.4.1 | NpcData.cs — ScriptableObject | 🔴 Высокий | ❌ |
| 4.4.2 | NpcEntity.cs — MonoBehaviour | 🔴 Высокий | ❌ |
| 4.4.3 | NpcInteraction.cs — компонент | 🟡 Средний | ❌ |
| 4.4.4 | Диалоговая система | 🟡 Средний | ❌ |
| 4.4.5 | Интеграция с контрактами | 🟢 Низкий | ❌ |

---

## 📚 РЕЛЕВАНТНЫЕ GDD ДОКУМЕНТЫ

### Обязательно к прочтению:

| Документ | Разделы | Зачем |
|----------|---------|-------|
| [GDD_01_Core_Gameplay.md](../../gdd/GDD_01_Core_Gameplay.md) | 3.1, 6 | Взаимодействие, Edge Cases |
| [GDD_21_Quest_Mission_System.md](../../gdd/GDD_21_Quest_Mission_System.md) | 2.1-2.5, 4.4 | Типы квестов, NPC диалоги |
| [GDD_11_Inventory_Items.md](../../gdd/GDD_11_Inventory_Items.md) | — | Система предметов |
| [GDD_12_Network_Multiplayer.md](../../gdd/GDD_12_Network_Multiplayer.md) | — | Сетевая синхронизация |

### Дополнительно:

| Документ | Зачем |
|----------|-------|
| [GDD_24_Narrative_World_Lore.md](../../gdd/GDD_24_Narrative_World_Lore.md) | Мир, фракции, персонажи |
| [GDD_20_Progression_RPG.md](../../gdd/GDD_20_Progression_RPG.md) | Система прогрессии |

---

## 🔧 ТЕКУЩЕЕ СОСТОЯНИЕ КОДОВОЙ БАЗЫ

### Реализованные системы (референс):

| Система | Файл | Описание |
|---------|------|----------|
| IInteractable | `Core/IInteractable.cs` | Базовый интерфейс |
| InteractableManager | `Core/InteractableManager.cs` | Централизованный поиск |
| NetworkChestContainer | `World/Chest/NetworkChestContainer.cs` | Сетевой сундук |
| ContractSystem | `Trade/Scripts/ContractSystem.cs` | Контракты (RPC) |
| Inventory | `Core/Inventory.cs` | Базовый инвентарь |
| PickupItem | `Core/PickupItem.cs` | Подбор предметов |

### Архитектурные паттерны проекта:

```
✅ ИНТЕРФЕЙСЫ: IInteractable для всех интерактивных объектов
✅ МЕНЕДЖЕР: InteractableManager для поиска (zero alloc)
✅ СЕТЕВОЙ СУНДУК: NetworkChestContainer с ServerRpc + ClientRpc
✅ RPC ПАТТЕРН: [ServerRpc] → серверная логика → [ClientRpc] ответ
✅ ИНВЕНТАРЬ: Inventory с AddItem/RemoveItem
```

---

## 🎯 АРХИТЕКТУРНОЕ РЕШЕНИЕ

### Структура NPC системы:

```
┌─────────────────────────────────────────────────────────┐
│                      NPC System                          │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐  │
│  │   NpcData   │    │  NpcEntity │    │NpcInteraction│  │
│  │ (SO asset)  │    │(MonoBehaviour)│  │ (Component)  │  │
│  └─────────────┘    └─────────────┘    └─────────────┘  │
│         │                   │                   │       │
│         ▼                   ▼                   ▼       │
│  ┌─────────────────────────────────────────────────┐   │
│  │              NpcDialogueManager                 │   │
│  │           (Singleton для диалогов)               │   │
│  └─────────────────────────────────────────────────┘   │
│                          │                              │
│                          ▼                              │
│  ┌─────────────────────────────────────────────────┐   │
│  │           ContractBoardUI (существует!)         │   │
│  │        — расширить для NPC диалогов             │   │
│  └─────────────────────────────────────────────────┘   │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### Паттерн реализации:

1. **NpcData** — ScriptableObject с информацией (имя, диалоги, фракция)
2. **NpcEntity** — MonoBehaviour с NetworkObject (опционально для синхронизации)
3. **NpcInteraction** — компонент взаимодействия (реализует IInteractable)
4. **NpcDialogueManager** — менеджер диалогов (UI)

### Сетевая синхронизация:

```
Вариант A: Локальный NPC (не синхронизируется)
├── Используется для: Статические NPC на станциях
├── Взаимодействие: Клиент обрабатывает локально
└── Ограничение: Только для одиночной игры

Вариант B: NetworkNpcEntity (синхронизируется)
├── Используется для: Важные NPC, квестовые NPC
├── ServerRpc: Запрос диалога
├── ClientRpc: Ответ с диалогом
└── Сложность: Выше, но нужно для мультиплеера
```

---

## 📝 ПОДЗАДАЧИ ДЛЯ СУБ-АГЕНТОВ

### Subtask 4.4.1: NpcData.cs

**Файл:** `Assets/_Project/Scripts/World/Npc/NpcData.cs`

**Задачи:**
- [ ] Создать ScriptableObject NpcData
- [ ] Поля: npcId, displayName, faction, portrait, dialogues[]
- [ ] Структура диалога: DialogueNode[] с условиями перехода

**Структура данных:**
```csharp
[CreateAssetMenu(fileName = "NewNpc", menuName = "Project C/NPC Data")]
public class NpcData : ScriptableObject
{
    public string npcId;
    public string displayName;
    public Faction faction; // enum
    public Sprite portrait;
    public DialogueNode[] dialogues;
}

[Serializable]
public class DialogueNode
{
    public string nodeId;
    [TextArea] public string text;
    public DialogueOption[] options;
    public bool isQuestGiver;
    public string contractId; // если выдаёт контракт
}

[Serializable]
public class DialogueOption
{
    public string text;
    public string nextNodeId;
    public string requiredItemId;
    public int requiredReputation;
}
```

---

### Subtask 4.4.2: NpcEntity.cs

**Файл:** `Assets/_Project/Scripts/World/Npc/NpcEntity.cs`

**Задачи:**
- [ ] Создать MonoBehaviour с NetworkObject (опционально)
- [ ] Привязка NpcData
- [ ] Состояния: Idle, Talking, Walking
- [ ] Базовая анимация (idle loop)

**Альтернатива:** Использовать существующий паттерн как у сундуков (ChestContainer → NetworkChestContainer)

---

### Subtask 4.4.3: NpcInteraction.cs

**Файл:** `Assets/_Project/Scripts/World/Npc/NpcInteraction.cs`

**Задачи:**
- [ ] Реализовать IInteractable
- [ ] Компонент на NpcEntity
- [ ] Метод Interact() → открывает диалог
- [ ] Интеграция с InteractableManager

```csharp
public class NpcInteraction : MonoBehaviour, IInteractable
{
    public string InstanceId => $"{npcData.npcId}_{gameObject.GetInstanceID()}";
    public string DisplayName => npcData.displayName;
    public float InteractionRadius => 3f;
    public Vector3 Position => transform.position;
    
    [SerializeField] private NpcData npcData;
    
    public void Interact()
    {
        NpcDialogueManager.Instance.StartDialogue(npcData);
    }
}
```

---

### Subtask 4.4.4: NpcDialogueManager.cs

**Файл:** `Assets/_Project/Scripts/World/Npc/NpcDialogueManager.cs`

**Задачи:**
- [ ] Singleton для управления диалогами
- [ ] UI панель диалога (использовать/расширить ContractBoardUI)
- [ ] Навигация по узлам диалога
- [ ] Обработка выбора игрока

**UI компоненты:**
- [ ] Портрет NPC
- [ ] Имя NPC
- [ ] Текст диалога
- [ ] Кнопки выбора (опции)
- [ ] Кнопка "Закрыть"

---

### Subtask 4.4.5: Интеграция с контрактами

**Файл:** `Trade/Scripts/ContractSystem.cs` (существует!)

**Задачи:**
- [ ] NPC выдаёт контракт через диалог
- [ ] ContractBoardUI открывается из диалога
- [ ] Награды за квест от NPC

---

## 🔍 ЛОГИРОВАНИЕ И ОТЛАДКА

### Debug режим:

```csharp
// Включить в Inspector
[SerializeField] private bool debugMode = false;

void Interact()
{
    if (debugMode)
        Debug.Log($"[NpcInteraction] Interact: {npcData.displayName}");
    
    NpcDialogueManager.Instance.StartDialogue(npcData);
}
```

### Логируемые события:

| Событие | Log Level | Пример |
|---------|-----------|--------|
| Взаимодействие с NPC | INFO | `[NpcInteraction] Player pressed E on: Trader Marcus` |
| Начало диалога | INFO | `[NpcDialogueManager] Starting dialogue: {npcId}` |
| Выбор опции | DEBUG | `[NpcDialogueManager] Player chose: option_1` |
| Ошибка | ERROR | `[NpcDialogueManager] NpcData is null!` |
| Контракт выдан | INFO | `[NpcDialogueManager] Contract issued: delivery_001` |

---

## 📊 ЖУРНАЛ ПОПЫТОК

### Формат записей (аналогично SOLUTION_ATTEMPTS_LOG.md):

```
### Попытка #N: [Краткое название] ([Дата])
**Файлы:** [Список файлов через запятую]
**Описание:** [Что было сделано]
**Изменения:**
```csharp
// код изменений
```
**Результат:** ✅/⚠️/❌
**Статус:** [Текущий статус]
**Причина провала/успеха:** [Анализ]
```

---

## ⚠️ ИЗВЕСТНЫЕ ОГРАНИЧЕНИЯ

1. **Локальная vs Сетевая синхронизация**
   - Локальные NPC работают в одиночной игре
   - Для мультиплеера нужна синхронизация состояния диалога

2. **UI система**
   - Использовать существующую ContractBoardUI или создать новую
   - UI Toolkit vs uGUI — согласовать с UX

3. **Анимации NPC**
   - Минимально: idle анимация
   - Полноценно: потребует модели и ассетов

---

## ✅ КРИТЕРИИ ПРИЁМКИ

- [ ] NpcData создаётся через контекстное меню Unity
- [ ] NPC в сцене имеет NpcData и Interaction
- [ ] E рядом с NPC открывает диалог
- [ ] Диалог отображает текст и опции
- [ ] Выбор опции переходит к следующему узлу
- [ ] Консольные ошибки отсутствуют
- [ ] Код компилируется без warnings

---

## 📁 СТРУКТУРА КАТАЛОГОВ

```
Assets/_Project/Scripts/World/
├── Chest/
│   └── NetworkChestContainer.cs  ← референс
├── Npc/                            ← новый каталог
│   ├── NpcData.cs                  ← ScriptableObject
│   ├── NpcEntity.cs                ← MonoBehaviour
│   ├── NpcInteraction.cs           ← Component (IInteractable)
│   ├── NpcDialogueManager.cs       ← Singleton
│   └── NpcDialogueUI.cs            ← UI (опционально)
└── ...

docs/world/LargeScaleMMO/combinesessions/iteration_4.4/
├── MASTER_PROMPT.md                ← этот файл
├── SOLUTION_ATTEMPTS_LOG.md        ← журнал попыток
└── NPC_ANALYSIS.md                 ← анализ архитектуры
```

---

## 🔗 СВЯЗАННЫЕ ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| [ITERATION_4_SESSION.md](../ITERATION_4_SESSION.md) | Общий отчёт итерации 4 |
| [iteration_4/TEST_GUIDE.md](../iteration_4/TEST_GUIDE.md) | Гайд по тестированию |
| [SOLUTION_ATTEMPTS_LOG.md](../iteration_3/SOLUTION_ATTEMPTS_LOG.md) | Референс формата логирования |

---

**Обновлено:** 19.04.2026, 14:42 MSK  
**Автор:** Claude Code  
**Версия:** iteration_4.4_v1
