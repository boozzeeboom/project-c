#  Быстрые команды Git для Project C

**Сохраните этот файл!**

---

## 📋 Проверка состояния

```bash
# Какая сейчас ветка и изменения
git status

# Последние 5 коммитов
git log --oneline -5

# Текущая версия (тег)
git describe --tags
```

---

## 💾 Создать резервную копию

```bash
# 1. Создать тег (версию)
git tag backup/2026-04-02

# 2. Отправить на GitHub
git push upstream --tags

# Или создать резервную ветку
git checkout -b backup/2026-04-02
git push -u upstream backup/2026-04-02
```

---

## ↩️ Откатиться к рабочей версии

```bash
# Получить последнюю версия с GitHub
git fetch upstream

# Откатиться к ней (ВСЕ ИЗМЕНЕНИЯ БУДУТ ПОТЕРЯНЫ!)
git reset --hard upstream/qwen-dev

# Очистить кэш Unity (в проводнике)
# Удалить папку: C:\UNITY_PROJECTS\ProjectC_client\Library\
```

---

## 🔄 Переключиться на версию из тега

```bash
# Посмотреть все теги
git tag --list

# Переключиться на версию
git checkout v0.2.1-base-working

# Вернуться на рабочую ветку
git checkout qwen-dev
```

---

##  Создать ветку для тестов

```bash
# Создать и переключиться
git checkout -b test/cloud-system

# Вернуться на основную
git checkout qwen-dev

# Удалить тестовую ветку
git branch -D test/cloud-system
```

---

## 📦 Закоммитить изменения

```bash
# Добавить все файлы
git add .

# Сделать коммит
git commit -m "Описание изменений"

# Отправить на GitHub
git push upstream qwen-dev
```

---

## 🎯 Текущая версия

- **Ветка:** `qwen-dev`
- **Базовый тег:** `v0.2.1-base-working`
- **GitHub:** https://github.com/boozzeeboom/project-c/tree/qwen-dev

---

## 📚 Полная документация

- [`GIT_WORKFLOW_ADVANCED.md`](GIT_WORKFLOW_ADVANCED.md) — продвинутый workflow
- [`VERSION_BACKUP.md`](VERSION_BACKUP.md) — резервное копирование
- [`GIT_WORKFLOW.md`](GIT_WORKFLOW.md) — шпаргалка

---

**Последнее обновление:** 2 апреля 2026 г.
