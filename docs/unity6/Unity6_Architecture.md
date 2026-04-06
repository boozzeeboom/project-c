# 🎮 Архитектура Unity 6 проекта

## Структура папок

```
Assets/
├── _Game/                  # Основной код игры
│   ├── Core/               # Ядро (GameManager, Events)
│   ├── Player/             # Скрипты игрока
│   ├── Enemies/            # Скрипты врагов
│   ├── Items/              # Предметы и инвентарь
│   ├── UI/                 # Интерфейс
│   ├── Audio/              # Аудио менеджер
│   └── Utils/              # Утилиты
├── Art/                    # Графика
│   ├── Characters/
│   ├── Environment/
│   └── UI/
├── Audio/                  # Звуки и музыка
├── Materials/              # Материалы
├── Prefabs/                # Префабы
│   ├── Characters/
│   ├── Environment/
│   └── UI/
├── Scenes/                 # Сцены
├── ScriptableObjects/      # Данные
│   ├── Items/
│   ├── Weapons/
│   └── Enemies/
└── Settings/               # Настройки проекта
    ├── URP/
    └── Input/
```

## Компоненты ядра

### GameManager
- Управление состоянием игры (Menu, Playing, Paused, GameOver)
- Инициализация систем
- Загрузка сцен

### EventManager
- Система событий без зависимостей
- Декойплинг компонентов

### SaveSystem
- Сохранение/загрузка через JSON
- PlayerPrefs для настроек

## Паттерны

### Observer (EventManager)
```csharp
// Подписка
EventManager.Subscribe("OnPlayerDeath", OnPlayerDeath);

// Триггер
EventManager.Trigger("OnPlayerDeath");

// Отписка
EventManager.Unsubscribe("OnPlayerDeath", OnPlayerDeath);
```

### State Machine
```csharp
// Состояния: Idle → Move → Attack → Dead
stateMachine.SwitchState(new MoveState(player));
```

### Object Pool
```csharp
var projectile = projectilePool.Get();
projectilePool.Return(projectile);
```

### ScriptableObject Database
```csharp
// Создание: Assets → Create → Game → Data → ItemDatabase
// Использование для предметов, оружия, врагов
```

## Naming Conventions

- **PascalCase** — классы, методы, свойства
- **camelCase** — поля, параметры
- **_camelCase** — приватные поля
- **UPPER_SNAKE_CASE** — константы
- **Hash суффикс** — Animator хэши (SpeedHash)

---

Архитектура проекта для организации кода.
