# Project C: The Clouds
**Version:** 0.0.5.1 | **Stage:** Early Pre-Alpha (Prototype)

---

## 📖 О проекте

Sci-fi flight simulator в альтернативной реальности по книге *«Интегральная Пявица»*.
Торговля, исследование мира над облаками, взаимодействие с фракциями.

**Ссылки:** [TheGravity.ru](https://thegravity.ru) | [Project-C](https://thegravity.ru/project-c/)

---

## 🚀 Быстрый старт

### Разработка
```
Продолжи работу над Project C. Прочитай файлы docs/QWEN_CONTEXT.md и docs/MMO_Development_Plan.md
```

### Репозиторий
- **GitHub:** https://github.com/boozzeeboom/project-c
- **Ветка:** `qwen-dev`

---

## 📚 Документация

Вся документация находится в папке [`docs/`](docs/):

| Файл | Описание |
|------|----------|
| [`docs/QWEN_CONTEXT.md`](docs/QWEN_CONTEXT.md) | **Текущий контекст** — что сделано, задачи |
| [`docs/MMO_Development_Plan.md`](docs/MMO_Development_Plan.md) | Полный план разработки MMO |
| [`docs/SHIP_SYSTEM_DOCUMENTATION.md`](docs/SHIP_SYSTEM_DOCUMENTATION.md) | Система кораблей (архитектура, настройка) |
| [`docs/WORLD_LORE_GRAVITY.md`](docs/WORLD_LORE_GRAVITY.md) | Лор мира (антигравий, мезий, гильдии) |
| [`docs/CONTROLS.md`](docs/CONTROLS.md) | Карта клавиш управления |
| [`docs/STEP_BY_STEP_DEVELOPMENT.md`](docs/STEP_BY_STEP_DEVELOPMENT.md) | Пошаговая разработка |
| [`docs/GIT_WORKFLOW_ADVANCED.md`](docs/GIT_WORKFLOW_ADVANCED.md) | Git workflow |
| [`docs/VERSION_BACKUP.md`](docs/VERSION_BACKUP.md) | Резервное копирование |

---

## 1. Сеттинг

**1930-е:** Метеориты с веществом «Мезий» и металлом «Антигравий» отравили нижнюю атмосферу Земли. Человечество построило искусственный барьер (Завесу).

**2050-е:** Цивилизации выжили над облаками на антигравийных платформах и горных вершинах.

**Технологии:**
- **Антигравий** — металл, искажающий гравитацию при подаче тока
- **МАГ-генераторы** — вырабатывают энергию из жидкого мезия
- **Корабли** — рамка-контур из антигравия + ветровые лопасти

---

## 2. Текущее состояние (0.0.5.1)

### ✅ Реализовано
- 🎮 **Персонаж:** WASD + Space (прыжок) + Shift (бег)
- 🚢 **Корабль:** Тяга W/S, рыскание A/D, лифт Q/E, тангаж мышью
- 🔄 **Переключение F:** подойти к кораблю (< 5м) → сесть/выйти
- 📷 **Камера:** от третьего лица, адаптируется к режиму
- 🌍 **Мир:** 15 горных пиков + 890+ облаков (3 слоя)
- 📦 **Input System:** полная миграция на Unity Input System

### ⏳ В работе
- Плавность физики корабля (инерция, banking)
- Система топлива (мезий)
- Подбор предметов и инвентарь

### 📋 Полный план
См. [`docs/MMO_Development_Plan.md`](docs/MMO_Development_Plan.md)

---

## 3. Управление (текущее)

### Пешеход
| Клавиша | Действие |
|---------|----------|
| W/S | Вперёд/назад |
| A/D | Стрейф |
| Мышь | Вращение камеры |
| Space | Прыжок |
| Shift | Бег |
| **F** | Сесть в корабль |

### Корабль
| Клавиша | Действие |
|---------|----------|
| W/S | Тяга вперёд/назад |
| A/D | Рыскание |
| Q/E | Вниз/Вверх (лифт) |
| Мышь Y | Тангаж |
| Shift | Ускорение |
| **F** | Выйти из корабля |

Полная карта: [`docs/CONTROLS.md`](docs/CONTROLS.md)

---

## 4. Технологии

| Компонент | Версия |
|-----------|--------|
| Unity | 6 (URP) |
| .NET Server | 8.0 |
| Networking | Unity Netcode for GameObjects |
| Input | Unity Input System 1.13.1 |

---

## 5. Структура проекта

```
ProjectC_client/
├── Assets/_Project/
│   ├── Scripts/
│   │   ├── Core/          # WorldGenerator, CloudSystem, Camera
│   │   ├── Player/        # PlayerController, ShipController, StateMachine
│   │   └── UI/            # ControlHintsUI, NetworkUI
│   └── InputActions/
├── docs/                  # Вся документация
├── ProjectC_Server/       # .NET 8 сервер
└── Packages/
```

---

## 6. Разработка

**Принцип:** «Медленнее = Быстрее» — маленькие шаги, тест после каждого, коммит только рабочего.

**Правило:** НЕ ТРОГАТЬ `.meta` файлы — Unity создаёт их автоматически.

Подробности: [`docs/STEP_BY_STEP_DEVELOPMENT.md`](docs/STEP_BY_STEP_DEVELOPMENT.md)

---

**Контакт:** [@indeed174](https://t.me/indeed174)
