# T-U03 BFS Tree Layout — переработка

**Дата:** 2026-07-31
**Текущее:** Колоночный лейаут (NPC | Dialog | Quest↓)
**Цель:** Древовидный BFS-лейаут с группировкой по NPC

---

## 1. Что сейчас

```
Col 0        Col 1          Col 2          Col 3
┌──────┐    ┌────────┐    ┌──────────┐    ┌──────────┐
│ 👤   │    │ 💬     │    │ 📜 Q_A   │    │ 📜 Q_B   │
│ Mira │───→│ Dialog │───→│          │    │          │
└──────┘    └────────┘    └────┬─────┘    └────┬─────┘
┌──────┐    ┌────────┐    ┌───↓──────┐    ┌───↓──────┐
│ 👤   │    │ 💬     │    │ 🟢 Stg0  │    │ 🟢 Stg0  │
│Zipun │    │ Dialog2│    └────┬─────┘    └────┬─────┘
└──────┘    └────────┘    ┌───↓──────┐    ┌───↓──────┐
                          │ 🟢 Stg1  │    │ 🎁 Rew   │
                          └────┬─────┘    └──────────┘
                          ┌───↓──────┐
                          │ 🎁 Rew   │
                          └──────────┘
```

**Проблемы:**
1. Dialog-ноды не сгруппированы под «своим» NPC — все в одной куче
2. Нет визуальной связи NPC→Dialog (только edge-линии)
3. Если у NPC 5 dialog-нод, а у другого 1 — колонка разъезжается

---

## 2. Целевой BFS-лейаут

Каждый NPC → корень своего поддерева. Поддеревья располагаются слева направо.

```
     Mira                     Zipun
┌──────────┐            ┌──────────┐
│ 👤 Mira  │            │ 👤 Zipun │
└──┬───┬───┘            └────┬─────┘
   │   │                     │
   ↓   ↓                     ↓
┌────┐ ┌────┐           ┌────────┐
│💬  │ │💬  │           │ 💬     │
│Hi! │ │Help│           │ Hello  │
└──┬─┘ └──┬─┘           └────────┘
   │      │
   │      ↓
   │  ┌──────────┐
   │  │ 📜 Help  │
   │  └────┬─────┘
   │       ↓
   │  ┌──────────┐
   │  │ 🟢 Stg 0 │
   │  └────┬─────┘
   │       ↓
   │  ┌──────────┐
   │  │ 🟢 Stg 1 │
   │  └────┬─────┘
   │       ↓
   │  ┌──────────┐
   │  │ 🎁 Rew   │
   │  └──────────┘
```

### Алгоритм:

```
1. Найти корни: NpcNodes + standalone QuestNodes + standalone DialogTrees
2. Для каждого корня:
   а. Построить дерево children:
      - NPC → его DialogTree.n nodes[] (диалоговые ноды)
      - DialogNode.edge с OfferQuest → QuestRoot
      - QuestRoot → Stage[0]
      - Stage[i] → Stage[i+1] (по nextStageId)
      - Last Stage → Reward
      - DialogNode.edge с SwitchDialogTree → другой DialogTree
   б. Рекурсивно разместить: корень сверху, дети снизу
3. Поддеревья разместить слева направо с отступом
```

---

## 3. План реализации

### Шаг 1: Модель — построение дерева children
Добавить в `QuestGraphModel`:
```csharp
public Dictionary<object, List<object>> BuildChildrenMap()
```
Возвращает: parent node info → list of child node infos.

### Шаг 2: ApplyLayout — рекурсивное размещение
```csharp
private float LayoutTree(object root, float x, float y, HashSet<object> visited)
```
Возвращает занятую высоту. Размещает root → затем детей под ним с отступом.

### Шаг 3: Позиции — save/restore
`PersistKey`-based сохранение позиций при ручном перемещении (уже есть).
При авто-лейауте: если позиция сохранена — не трогаем (preserve manual).

### Сложность: ~2 часа

---

## 4. Краевые случаи

| Случай | Решение |
|---|---|
| Один квест связан с двумя NPC | Показать под первым NPC, у второго — dashed edge к тому же квесту |
| DialogTree без NPC (standalone) | Корень = первая dialog-нода дерева |
| Quest без dialog (standalone) | Отдельное поддерево справа |
| SwitchDialogTree → другой диалог | Дети под dialog-нодой |

---

## 5. Константы

```
NODE_W = 260f
H_GAP = 60f   (horizontal gap между поддеревьями)
V_GAP = 40f   (vertical gap между parent и child)
CHILD_INDENT = 30f (отступ детей вправо от родителя)
```
