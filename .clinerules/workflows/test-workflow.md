# Testing Workflow

```yaml
name: Testing
description: Тестирование изменений
trigger: user:/test

steps:
  - id: identify_changes
    description: Определить что изменилось
    action: |
      1. Показать git diff
      2. Определить затронутые системы
      3. Определить тестовые сценарии

  - id: unit_tests
    description: Модульные тесты
    action: |
      For each changed component:
      - Run existing unit tests
      - Add new tests if needed
      - Verify all pass

  - id: integration_tests
    description: Интеграционные тесты
    action: |
      For affected systems:
      - Test interactions
      - Test edge cases
      - Verify no regressions

  - id: manual_testing
    description: Ручное тестирование
    action: |
      Test scenarios:
      1. Happy path
      2. Edge cases
      3. Error handling
      4. Performance

  - id: network_test
    description: Сетевое тестирование (если есть)
    action: |
      For multiplayer changes:
      - Host + Client test
      - Dedicated server test
      - Origin shift test

  - id: report
    description: Отчёт
    action: |
      Generate test report:
      
      ## Test Report
      
      ### Changes
      [summary]
      
      ### Test Results
      - ✅ Unit tests: [N] passed
      - ✅ Integration: [N] passed
      - ⚠️ Manual: [notes]
      
      ### Status
      - [ ] Ready to merge
      - [ ] Needs fixes
```

## Использование

```
/test "После изменений в торговле"
/test @qa-tester "Протестировать фичу X"
```

## Чеклист

- [ ] Все unit тесты проходят
- [ ] Интеграционные тесты проходят
- [ ] Ручное тестирование пройдено
- [ ] Нет новых warnings
- [ ] Нет regression bugs