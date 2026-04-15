# Code Review Workflow

```yaml
name: Code Review
description: Проверка качества кода перед коммитом
trigger: user:/code-review

steps:
  - id: identify_files
    description: Определить файлы для проверки
    action: |
      Parse the argument for file path or directory
      If directory, find all .cs files
      If specific file, review just that file

  - id: check_architecture
    description: Проверка архитектуры и паттернов
    action: |
      - [ ] Follows Project C architecture patterns?
      - [ ] Uses composition over inheritance?
      - [ ] Proper separation of concerns?

  - id: check_unity_standards
    description: Проверка C#/Unity стандартов
    action: |
      - [ ] No Find(), FindObjectOfType() in production
      - [ ] Component references cached in Awake()
      - [ ] [SerializeField] private for inspector fields

  - id: check_performance
    description: Проверка производительности
    action: |
      - [ ] No allocations in hot paths
      - [ ] Uses StringBuilder for concatenation in loops
      - [ ] Uses NonAlloc API variants

  - id: check_network
    description: Проверка сетевого кода
    action: |
      - [ ] NetworkVariable for persistent state
      - [ ] RPCs for one-shot actions
      - [ ] No GetComponent<>() in Update()

  - id: generate_report
    description: Сгенерировать отчёт
    output: |
      ## Code Review Report
      
      ### Files Reviewed
      [list]
      
      ### Issues Found
      - 🔴 Critical: [issues]
      - 🟡 Warning: [issues]
      - 🔵 Suggestion: [issues]
      
      ### Summary
      - Overall health: [score]
      - Must-fix: [N]
      - Nice-to-have: [N]
```

## Использование

Вызови: `/workflow codereview` или `/code-review [path]`

## Пример

```
/code-review Assets/_Project/Scripts/Player