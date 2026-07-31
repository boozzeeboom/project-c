# T-U08: ConditionNode — детальный анализ

**Дата:** 2026-07-31
**Статус:** Анализ. Реализация — отложена до необходимости.

---

## 1. Текущая система условий (уже работает)

### Где хранятся условия
Каждый `DialogueEdge` (выбор игрока) имеет:
```csharp
public DialogueCondition condition;          // одиночное (legacy)
public DialogueCondition[] conditions;        // массив — AND-комбинация
public bool hideIfUnavailable = true;         // скрыть если условие не пройдено
```

### Runtime-ветвление УЖЕ существует
В `QuestServer.EvaluateConditions()` (строка 1259):
1. Для каждого edge диалоговой ноды проверяются ВСЕ conditions (AND)
2. Edge прошедшие проверку → показываются игроку
3. Edge не прошедшие → скрываются (или серые, если `hideIfUnavailable=false`)

### Пример if/else через edge conditions:
```
Диалог: "Поможешь мне?"
├─ Edge "Да" → target: "thanks",    condition: null               → всегда видно
├─ Edge "Я уже помог" → "already",  condition: QuestCompleted("X") → только когда пройден
└─ Edge "Убью!" → "fight",          conditions: [ReputationAtLeast(Hostile,-50)]
```

Это полноценное ветвление: видимость выбора зависит от состояния игры.

---

## 2. Что такое ConditionNode (по плану T-U08)

Визуальная нода-ромб в графе:

```
        ┌─────────┐
  ← In  │  🔷     │
        │ Quest X │
        │ done?   │
        └────┬────┘
       ✓ True │ False ✗
    ┌────────┐ ┌────────┐
    │ Dialog │ │ Dialog │
    │ "Спа-  │ │ "Иди   │
    │ сибо!" │ │ делай!"│
    └────────┘ └────────┘
```

### Что она МОГЛА БЫ дать:
1. **Визуальная ясность** — ветвление видно на графе, не надо заходить в edge
2. **Переиспользование** — одна ConditionNode может соединяться с несколькими dialog-нодами
3. **Flowchart-ментальная модель** для дизайнеров

---

## 3. Цена реализации

### Проблема №1: Mapping ConditionNode → DialogueEdge при сохранении
ConditionNode существует только в редакторе. При сохранении её нужно «сплющить» обратно в `DialogueEdge.conditions[]`. Это требует:

```
Граф:
  DialogNode ──→ ConditionNode ──→ DialogNode_A (True)
                            └──→ DialogNode_B (False)

Сохраняется как:
  DialogNode.edges[0]:
    conditions = [condition из ConditionNode]
    targetNodeId = DialogNode_A.nodeId
  
  DialogNode.edges[1]:
    conditions = [NOT(condition из ConditionNode)]  ← проблема: нет NOT-логики!
    targetNodeId = DialogNode_B.nodeId
```

**NOT-логика отсутствует** в текущем `DialogueConditionType`. План (§9 Risk 5) сам признаёт: "При сохранении разбирается в `DialogueEdge.conditions[]`" но НЕ говорит как обрабатывать False-ветку.

### Проблема №2: Граф vs SO — двунаправленная синхронизация
При загрузке нужно обратно «развернуть» `DialogueEdge.conditions[]` в ConditionNode. Это обратное преобразование:
- Что если у двух edges одинаковые conditions? Одна ConditionNode или две?
- Что если conditions различаются частично? (AND-комбинация из 3 условий, одно разное)

### Проблема №3: Edge.condition vs Edge.conditions[]
Текущая система уже поддерживает два способа задать условия. ConditionNode добавляет ТРЕТИЙ способ — визуальный. Это увеличивает путаницу, а не уменьшает.

---

## 4. Вердикт

**ConditionNode НЕ НУЖЕН в текущей архитектуре.**

Причины:
1. Ветвление уже работает через `DialogueEdge.conditions[]` — ConditionNode это чисто визуальная абстракция
2. Mapping ConditionNode ↔ Edge.conditions — сложная двунаправленная трансформация с потерями
3. Отсутствует NOT-логика для False-ветки
4. Добавляет третий способ задать условия (вдобавок к `condition` и `conditions[]`)
5. План сам оценил риск как 🟡 (medium)

### Что реально нужно вместо ConditionNode:

**Улучшить визуализацию edge-conditions в DialogNode:**
- Сейчас в IMGUI DialogNode edge показывает `action` PropertyField (который включает и condition)
- Можно добавить цветовой индикатор на порту: 🟢 = без условий, 🟡 = conditional, 🔴 = hidden
- Можно показывать condition-preview в label порта: `→ "Yes (if QuestCompleted)"`

Это даст ту же «визуальную ясность» без ломки архитектуры.

### Альтернатива на будущее (v6):
Если понадобится полноценный визуальный flowchart — делать НЕ ConditionNode, а **ConditionGroup** на уровне DialogNode:
- Группировка edges по общему условию
- Визуальный блок «If Quest X completed» вокруг группы edges
- Без изменения runtime-модели

---

## 5. Рекомендация

**Пропустить T-U08.** Снять с плана. Заменить на:
- **T-U08a**: цветовые индикаторы условий на портах DialogNode (30 мин)
- **T-U08b**: preview condition в label порта (30 мин)
