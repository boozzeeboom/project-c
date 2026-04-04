# 📚 Документация Project C

## 🚀 Для Qwen Code (AI-ассистент)

| Файл | Когда использовать |
|------|-------------------|
| [QWEN_CONTEXT.md](QWEN_CONTEXT.md) | **Первый файл для чтения** — текущий контекст сессии |
| [MMO_Development_Plan.md](MMO_Development_Plan.md) | Общий план разработки MMO |
| [STEP_BY_STEP_DEVELOPMENT.md](STEP_BY_STEP_DEVELOPMENT.md) | Принцип пошаговой разработки |
| [README_QWEN.md](README_QWEN.md) | Как продолжить работу с Qwen Code |
| [README_CONTINUE.md](README_CONTINUE.md) | Быстрый старт |

---

## 🎮 Геймдизайн

| Файл | Описание |
|------|----------|
| [CONTROLS.md](CONTROLS.md) | Документация по управлению |
| [WORLD_LORE_BOOK.md](WORLD_LORE_BOOK.md) | **Полный лор книги «Интеграл Пьявица»** — мир, технологии, персонажи, сюжет |
| [WORLD_LORE_GRAVITY.md](WORLD_LORE_GRAVITY.md) | Краткий справочник лора (антигравий, мезий, корабли) |
| [SHIP_LORE_AND_MECHANICS.md](SHIP_LORE_AND_MECHANICS.md) | Механики кораблей из лора |
| [SHIP_SYSTEM_DOCUMENTATION.md](SHIP_SYSTEM_DOCUMENTATION.md) | Текущая реализация системы кораблей |
| [INVENTORY_SYSTEM.md](INVENTORY_SYSTEM.md) | **Система инвентаря** — круговое колесо, подбор, группировка |
| [SHIP_CONTROLLER_PLAN.md](SHIP_CONTROLLER_PLAN.md) | План разработки контроллера корабля |

---

## 🔧 Git & Версионирование

| Файл | Описание |
|------|----------|
| [GIT_WORKFLOW.md](GIT_WORKFLOW.md) | Шпаргалка Git команд |
| [GIT_WORKFLOW_ADVANCED.md](GIT_WORKFLOW_ADVANCED.md) | Продвинутый Git workflow |
| [QUICK_GIT_COMMANDS.md](QUICK_GIT_COMMANDS.md) | Быстрые команды Git |
| [VERSION_BACKUP.md](VERSION_BACKUP.md) | Резервное копирование |

---

## 📘 Unity 6

В папке [`unity6/`](unity6/) будет документация по Unity 6:
- Шейдеры и URP
- Netcode for GameObjects
- Оптимизация
- И т.д.

---

## 📁 Структура проекта

```
ProjectC_client/
├── docs/                     (эта папка)
│   ├── QWEN_CONTEXT.md       (контекст сессии)
│   ├── MMO_Development_Plan.md
│   ├── STEP_BY_STEP_DEVELOPMENT.md
│   ├── README_QWEN.md
│   ├── README_CONTINUE.md
│   ├── CONTROLS.md
│   ├── GIT_WORKFLOW.md
│   ├── GIT_WORKFLOW_ADVANCED.md
│   ├── QUICK_GIT_COMMANDS.md
│   ├── VERSION_BACKUP.md
│   └── unity6/               (документация Unity 6)
├── Assets/                   (ассеты Unity)
├── ProjectSettings/          (настройки проекта)
├── Packages/                 (Unity пакеты)
├── run_qwen.bat              (запуск Qwen Code)
└── README.md                 (общее описание проекта)
```

---

**Последнее обновление:** 4 апреля 2026 г.
**Ветка:** `qwen-dev`
**Версия:** `v0.0.7-chest-system`
