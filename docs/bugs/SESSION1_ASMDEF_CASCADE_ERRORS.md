# Баг-отчёт: Каскад ошибок от asmdef (Сессия 1)

**Дата:** 11 апреля 2026  
**Серьёзность:** 🔴 КРИТИЧЕСКИЙ — полная поломка компиляции  
**Статус:** ✅ Откачено к рабочему комиту `d403073`

---

## 📊 Сводка

**57 ошибок компиляции** после создания `ProjectC.Runtime.asmdef` и `ProjectC.Tests.asmdef`.

### Типы ошибок

| Категория | Количество | Причина |
|-----------|-----------|---------|
| **Missing: UnityEngine.InputSystem** | 10 файлов | asmdef не включил Unity.InputSystem |
| **Missing: TMPro** | 8 файлов | asmdef не включил Unity.TextMeshPro |
| **Missing: ProjectC.Trade** | 1 файл | Trade/不在 asmdef scope |
| **Missing: CargoSystem** | 1 файл | CargoSystem не в asmdef scope |
| **Burst assembly resolution** | 1 ошибка | Burst не нашёл ProjectC.Tests |

---

## 🔍 Root Cause Analysis

### Что произошло

1. **До asmdef:** Все скрипты в `Assets/_Project/Scripts/` были в **Assembly-CSharp** (дефолтная сборка Unity). Unity автоматически подключал все пакеты (InputSystem, TMPro, Netcode и т.д.).

2. **После создания `ProjectC.Runtime.asmdef`:**
   - Скрипты переместились из `Assembly-CSharp` в `ProjectC.Runtime`
   - `ProjectC.Runtime.asmdef` имел только `Unity.Netcode.Runtime` в references
   - **НЕ было:** `Unity.InputSystem`, `Unity.TextMeshPro`
   - **Результат:** 57 ошибок — все using statements не работают

3. **Burst compiler ошибка:**
   ```
   Failed to resolve assembly: 'ProjectC.Tests, Version=0.0.0.0'
   ```
   Burst сканирует все assemblies для entry-points, но `ProjectC.Tests` не была собрана из-за ошибок.

### Почему это произошло

| Ошибка | Описание |
|--------|----------|
| **Неполный анализ зависимостей** | Не проверили какие пакеты используют скрипты перед созданием asmdef |
| **Отсутствие тестирования на малом scope** | Сразу создали 2 asmdef без проверки компиляции |
| **Не учли cross-assembly зависимости** | Trade/, CargoSystem были в других папках, не попали в asmdef |
| **Burst совместимость** | Не проверили что Burst сможет найти test assembly |

---

## ✅ Решение (Применено)

**Откат к комиту `d403073`:**
```bash
git reset --hard d403073
```

**Результат:** Проект компилируется без ошибок.

---

## 📋 Lessons Learned

### ❌ НЕ делать

1. **НЕ создавать asmdef без полного анализа зависимостей**
   - Проверить все `using` statements во всех скриптах папки
   - Убедиться что все пакеты в references asmdef

2. **НЕ выносить скрипты из Assembly-CSharp без необходимости**
   - Assembly-CSharp автоматически получает все пакеты
   - asmdef нужен только для: разделения client/server, addressables, тестов

3. **НЕ создавать несколько asmdef одновременно**
   - Один asmdef → проверка компиляции → следующий

4. **НЕ забывать про Burst**
   - Burst сканирует все assemblies
   - Test assemblies могут мешать Burst если неправильно настроены

5. **НЕ коммитить asmdef без проверки в Unity Editor**
   - asmdef изменения требуют перекомпиляции в Unity
   - Проверить Console перед коммитом

### ✅ Делать

1. **Перед созданием asmdef:**
   ```
   1. Найти все using statements в папке
   2. Сопоставить с пакетами (InputSystem, TMPro, etc)
   3. Добавить все в references asmdef
   4. Проверить cross-assembly зависимости
   ```

2. **После создания asmdef:**
   ```
   1. Открыть Unity Editor
   2. Подождать перекомпиляции
   3. Проверить Console на ошибки
   4. Только потом коммитить
   ```

3. **Для тестов:**
   - Использовать `includePlatforms: []` (PlayMode)
   - Ссылаться на основную assembly проекта
   - Добавить `UnityEngine.TestRunner` в references

---

## 🎯 План для Сессии 1 (Начать заново)

### Что делать по-другому

1. **НЕ создавать asmdef файлы** — оставить скрипты в Assembly-CSharp
2. **Тесты:** Создать простой тестовый скрипт без asmdef (Unity автоматически подхватит)
3. **Или:** Создать asmdef только для тестов с ПОЛНЫМИ зависимостями:
   ```json
   {
     "references": [
       "Assembly-CSharp",
       "Unity.Netcode.Runtime",
       "UnityEngine.TestRunner",
       "Unity.InputSystem",
       "Unity.TextMeshPro"
     ]
   }
   ```

### Рабочий подход

```
1. Переписать ShipController.cs (уже было сделано ✅)
2. НЕ создавать asmdef файлы
3. Тесты положить в папку которую Unity видит без asmdef
4. Проверить компиляцию в Unity
5. Только потом коммитить
```

---

## 📁 Связанные файлы

| Файл | Статус |
|------|--------|
| `Assets/_Project/Scripts/ProjectC.asmdef` | ❌ Удалён (откат) |
| `Assets/_Project/Tests/ProjectC.Tests.asmdef` | ❌ Удалён (откат) |
| `Assets/_Project/Scripts/Player/ShipController.cs` | ✅ Сохранён (в коммите 67e1f87) |
| `Assets/_Project/Tests/ShipMovementTests.cs` | ✅ Сохранён (в коммите 67e1f87) |

---

*Отчёт создан: 11 апреля 2026*  
*Агент: @qa-tester*  
*Статус: Откачено к рабочему состоянию*
