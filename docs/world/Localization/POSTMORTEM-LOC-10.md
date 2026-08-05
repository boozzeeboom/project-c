# Post-mortem: кастомный CSV-экспорт (LOC-10)

## Что произошло

В фазе 6 (LOC-10) был написан `LocalizationToolWindow.cs` — кастомный EditorWindow
для экспорта/импорта CSV из Unity Localization таблиц. Это было **ошибкой проектирования**.

## Почему это ошибка

Пакет `com.unity.localization` версии 1.5.12 **уже содержит**:

| Штатный инструмент | Где находится |
|---|---|
| CSV Export / Import | `Window → Asset Management → Localization Tables` → кнопка `⋮` → Export/Import CSV |
| Google Sheets синхронизация | Там же → Google Sheets Provider (встроен в пакет) |
| XLIFF Export / Import | Там же (для профессиональных переводческих тулов) |

Пути в пакете:
- `Editor/Plugins/CSV/` — штатный CSV-экспортёр
- `Editor/Plugins/Google/` — штатный Google Sheets провайдер
- `Editor/Plugins/Xliff/` — штатный XLIFF

## Корневая причина ошибки

1. **Не проверил штатные возможности пакета** перед написанием кастомного кода.
   Зашёл в цикл «данные не экспортируются → чиним экспортёр → reflection → ошибки»,
   вместо того чтобы открыть `Window → Asset Management → Localization Tables`
   и использовать встроенную кнопку Export.

2. **Проблема была в populate-скриптах**, а не в экспорте.
   `table.AddEntry(key, value)` не синхронизировал `SharedData` (баг/особенность версии),
   из-за чего `SharedData.Entries` возвращал 0. Штатный экспортёр тоже не видел бы данные.
   Правильный подход: `sharedData.AddKey(key)` → `table.AddEntry(sharedEntry.Id, value)`.

3. **Усложнение архитектуры без нужды.** Кастомный EditorWindow, reflection,
   SerializedObject, managedReferenceValue, парсинг CSV-строк — всё это лишнее.
   Пакет уже решает эти задачи.

## Что нужно исправить

1. Удалить `Assets/_Project/Editor/Localization/LocalizationToolWindow.cs`
2. Починить populate-скрипты: использовать `SharedTableData.AddKey()` + `AddEntry(id, value)`
3. Перезаполнить таблицы правильным API
4. Для экспорта/импорта использовать штатное окно Unity Localization Tables
5. Для совместного перевода использовать встроенный Google Sheets Provider

## Урок

**Всегда проверяй штатные возможности пакета перед написанием кастомного кода.**
Unity Localization — зрелый пакет с богатым Editor tooling.
Если что-то не работает — проблема скорее в данных/API вызовах, а не в отсутствии функционала.
