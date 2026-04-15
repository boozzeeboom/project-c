# Feature Development Workflow

```yaml
name: Feature Development
description: Стандартный процесс разработки фичи
trigger: user:/feature

steps:
  - id: understand_requirement
    description: Понять требования
    action: |
      1. Прочитать дизайн-документ если есть
      2. Уточнить неясные моменты
      3. Определить границу (scope)

  - id: design_architecture
    description: Спроектировать архитектуру
    action: |
      1. Показать структуру классов
      2. Объяснить почему выбран этот подход
      3. Указать trade-offs
      4. Спросить одобрение

  - id: implement
    description: Реализовать
    action: |
      1. Показать код перед записью
      2. Спросить "Можу записать в [files]?"
      3. Реализовать после одобрения

  - id: write_tests
    description: Написать тесты
    action: |
      Для каждого компонента:
      - Unit test для логики
      - Интеграционный тест для взаимодействий

  - id: code_review
    description: Code review
    action: |
      1. Запустить /code-review для изменённых файлов
      2. Исправить найденные проблемы
      3. Получить финальное одобрение

  - id: commit
    description: Зафиксировать
    action: |
      1. Показать git diff
      2. Написать commit message
      3. Спросить "Коммитить?"
```

## Использование

```
/feature "Система торговли между игроками"
/feature "Новая способность: телепортация"
```

## Структура коммита

```
feat: add [feature name]

- Implementation details
- Testing notes
- Breaking changes (if any)