# NPC System — Архитектурный анализ

**Дата:** 19.04.2026  
**Статус:** 🔴 НЕ РЕАЛИЗОВАНО

---

## 1. АНАЛИЗ ТРЕБОВАНИЙ

### 1.1 Из GDD

**GDD_01_Core_Gameplay.md** (строка 47):
> **Пеший** — Исследование, взаимодействие с миром | Ходьба, бег, прыжки, **подбор, сундуки, NPC** (future)

**GDD_01_Core_Gameplay.md** (строка 66):
> **Взаимодействия:**
> - Подбор предметов: E, радиус 3м, **приоритет сундукам**
> - Посадка в корабль: F, ближайший < 5м

**GDD_21_Quest_Mission_System.md** (строка 50-57):
> | Тип | Описание | 
> |-----|----------|
> | **Доставка** | Перевозка груза между точками |
> | **Разведка** | Исследование неизвестных зон |
> | **Сопровождение** | Защита NPC или другого игрока |
> | **Контрабанда** | Нелегальная перевозка |
> | **Поиск артефактов** | Обнаружение редких предметов |

**GDD_21_Quest_Mission_System.md** (строка 254-258):
> ```csharp
> stages: [
>     { id: 1, type: Dialogue, target: "Guild_Master" },
>     { id: 2, type: Delivery, target: "Outpost_Alpha", item: "Message" },
>     { id: 3, type: Dialogue, target: "Contact_Alpha" },
>     { id: 4, type: Return, target: "Guild_Master" }
> ]
> ```

### 1.2 Выводы

1. **NPC** — будущая фича, пока не реализована
2. **E** — клавиша взаимодействия (уже работает для сундуков)
3. **Dialogue** — тип этапа квеста
4. **NPC должен выдавать контракты** — связь с ContractSystem

---

## 2. АНАЛИЗ СУЩЕСТВУЮЩЕГО КОДА

### 2.1 Референс: NetworkChestContainer

**Путь:** `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs`

**Структура:**
```csharp
public class NetworkChestContainer : NetworkBehaviour
{
    // NetworkVariable для синхронизации
    private NetworkVariable<bool> _isOpen = new NetworkVariable<bool>();
    
    // ServerRpc для запроса открытия
    [ServerRpc(RequireOwnership = false)]
    public void RequestOpenChestServerRpc(...)
    
    // ClientRpc для подтверждения
    [ClientRpc]
    public void ChestOpenedClientRpc(...)
}
```

**Паттерны:**
- ✅ NetworkBehaviour + NetworkObject
- ✅ ServerRpc для запросов
- ✅ ClientRpc для ответов
- ✅ NetworkVariable для состояния

### 2.2 Референс: IInteractable

**Путь:** `Assets/_Project/Scripts/Core/IInteractable.cs`

**Интерфейс:**
```csharp
public interface IInteractable
{
    string InstanceId { get; }
    string DisplayName { get; }
    float InteractionRadius { get; }
    Vector3 Position { get; }
}
```

### 2.3 Референс: InteractableManager

**Путь:** `Assets/_Project/Scripts/Core/InteractableManager.cs`

**Методы:**
- FindNearestPickup()
- FindNearestChest()
- FindNearestShip()

---

## 3. АРХИТЕКТУРНОЕ РЕШЕНИЕ

### 3.1 Структура файлов

```
Assets/_Project/Scripts/World/Npc/
├── NpcData.cs              — ScriptableObject (данные)
├── NpcEntity.cs            — MonoBehaviour (3D объект)
├── NpcInteraction.cs      — Component (взаимодействие E)
├── NpcDialogueManager.cs   — Singleton (управление диалогами)
└── NpcFaction.cs          — Enum (фракции)
```

### 3.2 Варианты реализации

#### Вариант A: Локальный NPC (простой)

```
┌─────────────┐
│  NpcEntity │ ← БЕЗ NetworkObject
└──────┬──────┘
       │ SerializeField
       ▼
┌─────────────┐
│  NpcData    │ ← ScriptableObject
└─────────────┘
       │
       ▼
┌─────────────────────┐
│ NpcDialogueManager  │ ← Singleton
└─────────────────────┘
```

**Плюсы:**
- Просто реализовать
- Нет сетевых задержек

**Минусы:**
- Не работает в мультиплеере (для всех клиентов)
- NPC виден только локально

---

#### Вариант B: Сетевой NPC (рекомендуемый)

```
┌─────────────────────┐
│   NetworkNpcEntity  │ ← NetworkObject
├─────────────────────┤
│ NetworkVariable<bool>│
│     isTalking       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│      NpcData        │ ← ScriptableObject
└─────────────────────┘
           │
           ▼
┌─────────────────────┐
│ NpcDialogueManager  │
│    (ClientRpc)       │
└─────────────────────┘
```

**Плюсы:**
- Работает в мультиплеере
- Состояние синхронизируется

**Минусы:**
- Сложнее реализовать
- Больше кода

---

### 3.3 Рекомендуемое решение

**Выбор: Вариант A для MVP → Вариант B для продакшена**

Для ITERATION 4.4:
1. Начать с локального NPC (быстро)
2. Позже добавить NetworkObject (когда потребуется мультиплеер)

---

## 4. ДИАЛОГОВАЯ СИСТЕМА

### 4.1 Структура диалога

```csharp
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

### 4.2 UI компоненты

| Компонент | Описание |
|-----------|---------|
| Portrait | Портрет NPC (Sprite) |
| Name | Имя NPC |
| Text | Текст диалога |
| Options[] | Кнопки выбора |
| CloseButton | Закрыть диалог |

### 4.3 Навигация

```
[Начало] → [Текст] → [Опция 1] → [Следующий узел]
                    → [Опция 2] → [Следующий узел]
                    → [Опция 3] → [Квест выдан]
```

---

## 5. ИНТЕГРАЦИЯ С КОНТРАКТАМИ

### 5.1 Существующая система

**ContractSystem.cs:**
- RequestAvailableContractsServerRpc — запрос контрактов
- AcceptContractServerRpc — принять контракт
- CompleteContractServerRpc — завершить контракт

**ContractBoardUI:**
- Доска с доступными контрактами
- Кнопки принятия/завершения

### 5.2 Интеграция

```
NPC Dialog → "Принять контракт" → ContractBoardUI.Open(npcId)
                                    ↓
                              ContractSystem.Request()
                                    ↓
                              ClientRpc → UI обновляется
```

---

## 6. РИСКИ И ОГРАНИЧЕНИЯ

### 6.1 Риски

| Риск | Вероятность | Влияние | Митигация |
|------|-------------|---------|-----------|
| Сложность диалогового дерева | Средний | Высокий | Начать с простого |
| Сетевая синхронизация | Средний | Средний | Локально для MVP |
| UI конфликт с ContractBoardUI | Низкий | Средний | Расширить существующий |

### 6.2 Ограничения

1. **Анимации** — нет моделей NPC
2. **Озвучка** — нет звуковых файлов
3. **Локализация** — только английский/русский текст

---

## 7. ПЛАН РЕАЛИЗАЦИИ

### Sprint 1: Базовая структура
1. [ ] NpcFaction.cs (enum)
2. [ ] NpcData.cs (ScriptableObject)
3. [ ] NpcEntity.cs (MonoBehaviour)
4. [ ] Тест: NPC в сцене

### Sprint 2: Взаимодействие
5. [ ] NpcInteraction.cs (IInteractable)
6. [ ] Интеграция с InteractableManager
7. [ ] Тест: E рядом с NPC

### Sprint 3: Диалоги
8. [ ] NpcDialogueManager.cs
9. [ ] UI панель диалога
10. [ ] Навигация по узлам
11. [ ] Тест: Диалог открывается

### Sprint 4: Контракты
12. [ ] Интеграция с ContractSystem
13. [ ] Выдача контракта из диалога
14. [ ] Тест: Контракт выдан

---

## 8. РЕФЕРЕНСНЫЕ ФАЙЛЫ

| Файл | Зачем |
|------|-------|
| `NetworkChestContainer.cs` | Паттерн сетевого объекта |
| `IInteractable.cs` | Базовый интерфейс |
| `InteractableManager.cs` | Поиск объектов |
| `ContractSystem.cs` | Система контрактов |
| `ContractBoardUI.cs` | UI контрактов |

---

**Обновлено:** 19.04.2026, 14:44 MSK  
**Автор:** Claude Code  
**Версия:** iteration_4.4_v1
