# Bug Fix Workflow

```yaml
name: Bug Fix
description: Стандартный процесс исправления багов
trigger: user:/bugfix

steps:
  - id: understand_bug
    description: Понять баг
    action: |
      1. Что ожидаемое поведение?
      2. Что фактическое поведение?
      3. Как воспроизвести?
      4. Серьёзность (P0-P4)?

  - id: find_cause
    description: Найти причину
    action: |
      1. Прочитать соответствующий код
      2. Проверить логику
      3. Найти корневую причину
      4. Определить файлы для изменения

  - id: implement_fix
    description: Реализовать исправление
    action: |
      1. Показать план изменений
      2. Спросить "Можно исправить в [files]?"
      3. Реализовать после одобрения

  - id: test
    description: Протестировать
    action: |
      1. Воспроизвести баг
      2. Применить исправление
      3. Проверить что баг исправлен
      4. Проверить что ничего не сломалось

  - id: document
    description: Документировать
    action: |
      Создать BUG-YYYY-MM-DD.md в docs/bugs/
      - Summary
      - Steps to reproduce
      - Root cause
      - Fix applied
      - Testing notes
```

## Использование

```
/bugfix Описание бага
/bugfix @qa-tester "Протестировать баг #42"
```

## Пример

```
/bugfix Игрок не может открыть сундук в облаках