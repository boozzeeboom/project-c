# LORE Setup — Project C: The Clouds

Дата: 2026-07-21
Статус: plan

## Что такое Lore (Epic Games)

Lore (https://github.com/EpicGames/lore) — это от Epic Games система управления данными и контентом для геймдева. Работает как CLI, читает YAML/CSV → генерирует C# код (и не только).

Ключевые фичи:
- **Data Classes** — декларативное описание структур (аналог ScriptableObject, но кодогенерация)
- **Enums** — централизованные enum'ы из YAML
- **Data Assets** — данные в CSV или YAML, привязанные к data classes
- **Code Generation** — C# классы из определений
- **Localization** — строковые ключи

## Зачем проекту C

Сейчас в проекте разброс данных:
- `Assets/_Project/Data/` — ScriptableObject'ы (CloudLayerConfig, SceneRegistry, и т.д.)
- `Assets/_Project/Trade/` — своя экономика
- Разрозненные конфиги в префабах и MonoBehaviour

Lore может дать:
1. Единый Source of Truth для игровых данных (YAML в репозитории)
2. Авто-генерацию C# классов вместо ручных ScriptableObject
3. CSV-импорт для балансных таблиц
4. Локализацию строк

## План

1. **Исследование** — понять возможности Lore, CLI, форматы
2. **Установка** — dotnet tool, инициализация в проекте
3. **Пилот** — взять одну подсистему (Trade Items или CloudLayerConfig) и перевести на Lore
4. **Интеграция с Unity** — codegen в `Assets/_Project/Scripts/Generated/`
5. **Документация** — записать workflow для команды

## Принятые решения (ADR placeholder)

- Используем `dotnet tool` установку (глобальную или локальную)
- Генерируемый C# кладём в `Assets/_Project/Scripts/Generated/Lore/`
- Исходные YAML — в `ProjectSettings/Lore/` или `Assets/_Project/Data/Lore/`
- CSV для балансных данных — в `Assets/_Project/Data/Lore/CSV/`
