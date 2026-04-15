# Sprint Planning Workflow

```yaml
name: Sprint Planning
description: Планирование спринта
trigger: user:/sprint-plan

steps:
  - id: review_context
    description: Обзор контекста
    action: |
      1. Прочитать milestone файл
      2. Проверить git историю
      3. Посмотреть открытые баги
      4. Определить velocity

  - id: gather_stories
    description: Собрать истории
    action: |
      From backlog:
      - Check docs/backlog/
      - Check existing issues
      - Prioritize by dependencies

  - id: estimate_effort
    description: Оценить усилия
    action: |
      Для каждой истории:
      - Определить сложность (1-5)
      - Оценить время (h)
      - Назначить владельца

  - id: commit_stories
    description: Зафиксировать план
    action: |
      Generate sprint plan:
      
      ## Sprint [N] — [Date Range]
      
      ### Goal
      [One sentence]
      
      ### Stories
      - [STORY-001] [Title] — [Points] — [@owner]
      
      ### Definition of Done
      - [ ] Code reviewed
      - [ ] Tested in editor
      
      Save to: docs/sprints/sprint-[N]-[date].md

  - id: present_plan
    description: Представить план
    action: |
      Показать:
      - Цель спринта
      - Выбранные истории
      - Stretch goals
      - Capacity vs commitment
```

## Использование

```
/sprint-plan new
/sprint-plan update
/sprint-plan status
```

## Output

Creates: `docs/sprints/sprint-[N]-[date].md`