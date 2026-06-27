# Skill Tree 2D Graph — Implementation Plan

> **Дата:** 2026-06-26 (сессия #5)
> **База:** SkillTreeWindow overlay, docs/Character/Skills/Battle/20_SKILL_TREES.md

## Архитектура

Заменяем левую колонку (список навыков) на ScrollView с 2D канвасом:

```
┌─ SkillTreeWindow ─────────────────────────────────┐
│ [Все] [⚔] [🏹] [💣] [🌌] [🛡]  [🔍 ...]           │
├──────────────────────┬────────────────────────────┤
│  ┌── canvas ──────┐  │ Детальная панель           │
│  │  (scroll 600%)  │  │ ──────────────────────     │
│  │                 │  │ Название                   │
│  │  [BasicSword]──►│  │ Описание                   │
│  │   └─[GreatSw]  │  │ Эффекты                    │
│  │       └─[Prec]  │  │ Стоимость                  │
│  │                 │  │ Требуется: (prereq list)   │
│  │  [BasicStrike]─►│  │ Откроет: (dependents)      │
│  │   └─[HeavySw]─►│  │ [Изучить]  [Забыть]        │
│  │       └─[Prec]  │  │                            │
│  └─────────────────┘  └────────────────────────────┘
│               [Закрыть (Esc)]                      │
└────────────────────────────────────────────────────┘
```

## Компоненты

### Canvas (ScrollView → content container)
- Content container: 2000×2000 px (для масштаба treeX/treeY * 2.5)
- Все узлы — `position: Absolute` внутри контейнера
- Линии — VisualElement с `generateVisualContent` callback

### Узлы (Nodes)
- VisualElement 120×36 с `position: Absolute`
- left = treeX * 2.5 + offset
- top = treeY * 2.5 + offset
- Внутри: state badge (✅/○/✕) + название
- Цвет рамки/фона по state (зелёный/жёлтый/серый)
- selected: яркая рамка + glow
- Click → SelectSkill(skillId) → детальная панель

### Линии (Edges)
- Рисуются через `generateVisualContent` с `ctx.painter2D` (Unity 6)
- Цвет: по state родительского навыка
- Стрелка от prereq → skill

### Логика
- Chips filter: скрывает узлы других дисциплин (и их рёбра)
- Search: подсвечивает matching узлы, остальные полупрозрачные
- Pan/Scroll: через ScrollView (native UI Toolkit)
- При выборе узла: ScrollView скроллит к нему (`ScrollTo`)

## План реализации

| # | Шаг | Сложность |
|---|---|---|
| 1 | Canvas: заменить `skill-list-container` на `tree-canvas` + `tree-content` | 15м |
| 2 | Узлы: создать `VisualElement` для каждого навыка, абсолютное позиционирование по treeX/treeY | 30м |
| 3 | Выбор узла: клик → SelectSkill(skillId) + визуальный highlight | 15м |
| 4 | Линии: `generateVisualContent` с Painter2D для prereq→skill edges | 30м |
| 5 | Chips + search: скрывать/подсвечивать узлы | 15м |
| 6 | ScrollTo: при выборе узла из search/click — скролл к нему | 10м |
| 7 | USS: стили для узлов (state-цвета, hover, selected) | 15м |
| 8 | Compile + verify | 5м |

**Итого ~2ч.**

## Реализация (SkillTreeWindow.cs, ключевые изменения)

```csharp
// Вместо RebuildSkillList():

private void RebuildSkillTree()
{
    if (_treeContent == null) return;
    _treeContent.Clear();
    
    var learned = SkillsClientState.Instance?.CurrentSkills ?? new HashSet<string>();
    
    // 1. Создаём узлы
    var nodes = new Dictionary<string, VisualElement>();
    foreach (var s in _visibleSkills)
    {
        var node = MakeTreeNode(s, learned);
        node.style.left = s.treeX * 2.5f + 20;  // scale + padding
        node.style.top = s.treeY * 2.5f + 10;
        _treeContent.Add(node);
        nodes[s.skillId] = node;
    }
    
    // 2. Рисуем линии (через generateVisualContent)
    _treeContent.generateVisualContent += (ctx) => {
        var painter = ctx.painter2D;
        painter.lineWidth = 2;
        foreach (var s in _visibleSkills)
        {
            if (s.prerequisites == null) continue;
            foreach (var prereq in s.prerequisites)
            {
                if (prereq == null || !nodes.ContainsKey(prereq.skillId) || !nodes.ContainsKey(s.skillId)) continue;
                var fromNode = nodes[prereq.skillId];
                var toNode = nodes[s.skillId];
                float x1 = fromNode.resolvedStyle.left + fromNode.resolvedStyle.width / 2;
                float y1 = fromNode.resolvedStyle.top + fromNode.resolvedStyle.height;
                float x2 = toNode.resolvedStyle.left + toNode.resolvedStyle.width / 2;
                float y2 = toNode.resolvedStyle.top;
                
                painter.strokeColor = learned.Contains(prereq.skillId) ? Color.green : Color.gray;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x1, y1));
                painter.LineTo(new Vector2(x2, y2));
                painter.Stroke();
            }
        }
    };
}
```

## Узлы (USS)

```css
.tree-node {
    position: absolute !important;
    width: 120px !important;
    min-height: 32px !important;
    background-color: rgba(25, 32, 48, 0.85) !important;
    border-width: 2px !important;
    border-radius: 4px !important;
    padding: 2px 4px !important;
    cursor: link !important;   // ← but we said NO cursor:link earlier!
    // Use background hover instead
}
.tree-node:hover {
    background-color: rgba(60, 90, 130, 0.7) !important;
}
.tree-node-learned {
    border-color: rgb(80, 200, 120) !important;
}
.tree-node-available {
    border-color: rgb(180, 200, 60) !important;
}
.tree-node-locked {
    border-color: rgba(100, 100, 110, 0.5) !important;
}
.tree-node-selected {
    border-color: rgb(100, 180, 255) !important;
    box-shadow: 0 0 8px rgba(100, 180, 255, 0.6) !important;  // doesn't work in UI Toolkit
    // instead: wider border or background glow
    border-width: 3px !important;
}
```

## Изменения UXML

В UXML заменить:
```xml
<ui:VisualElement name="skill-list-container" class="stw-list-container" />
```
на:
```xml
<ui:ScrollView name="tree-canvas-scroll" class="stw-scroll">
  <ui:VisualElement name="tree-content" class="tree-content" />
</ui:ScrollView>
```