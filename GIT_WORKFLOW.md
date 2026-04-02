# 📘 Инструкция по работе с Unity + Git для Project C

## 🎯 Полный цикл работы над проектом

---

## Этап 1: Начало работы

### 1.1 Запустите Unity Hub и откройте проект

```
Путь: C:\UNITY_PROJECTS\ProjectC_client
```

### 1.2 Перед началом работы проверьте Git

Откройте терминал в папке проекта и выполните:

```bash
cd C:\UNITY_PROJECTS\ProjectC_client
git status
```

**Ожидаемый результат:**
```
On branch qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb
nothing to commit, working tree clean
```

Если есть изменения — закоммитьте их (см. Этап 3).

---

## Этап 2: Работа в Unity

### 2.1 Делайте изменения в проекте

Примеры изменений:
- ✅ Добавили 3D модель (FBX) в `Assets/_Project/Art/Models/`
- ✅ Создали префаб в `Assets/_Project/Prefabs/`
- ✅ Написали скрипт в `Assets/_Project/Scripts/Player/`
- ✅ Изменили сцену в `Assets/_Project/Scenes/`
- ✅ Добавили текстуру (PNG) в `Assets/_Project/Art/Textures/`
- ✅ Изменили настройки в Unity (Edit → Project Settings)

### 2.2 Сохраните проект в Unity

```
File → Save Project
```

Или нажмите `Ctrl + Shift + S`

### 2.3 Закройте Unity (или оставьте открытым)

---

## Этап 3: Проверка и коммит изменений

### 3.1 Проверьте, что изменилось

Откройте терминал и выполните:

```bash
cd C:\UNITY_PROJECTS\ProjectC_client
git status
```

**Пример вывода:**
```
On branch qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb

Changes not staged for commit:
  (use "git add <file>..." to update what will be committed)
        modified:   Assets/_Project/Scripts/Player/PlayerController.cs
        modified:   ProjectSettings/ProjectSettings.asset

Untracked files:
  (use "git add <file>..." to include in what will be committed)
        Assets/_Project/Art/Models/Hero.fbx
        Assets/_Project/Prefabs/Player.prefab
```

### 3.2 Посмотрите детали изменений (опционально)

```bash
git diff Assets/_Project/Scripts/Player/PlayerController.cs
```

Покажет, какие строки кода изменились.

### 3.3 Добавьте изменения в индекс коммита

**Вариант A: Добавить все изменения**
```bash
git add -A
```

**Вариант B: Добавить конкретные файлы**
```bash
git add Assets/_Project/Art/Models/Hero.fbx
git add Assets/_Project/Prefabs/Player.prefab
git add Assets/_Project/Scripts/Player/PlayerController.cs
```

### 3.4 Проверьте, что готово к коммиту

```bash
git status
```

**Теперь должно быть:**
```
Changes to be committed:
        new file:   Assets/_Project/Art/Models/Hero.fbx
        new file:   Assets/_Project/Prefabs/Player.prefab
        modified:   Assets/_Project/Scripts/Player/PlayerController.cs
```

### 3.5 Сделайте коммит

```bash
git commit -m "Добавлен игрок: модель, префаб и контроллер"
```

**Правила хорошего коммита:**
- ✅ Кратко и по делу (до 50 символов в заголовке)
- ✅ Используйте повелительное наклонение: "Добавлен", "Исправлен", "Обновлён"
- ✅ Избегайте: "изменения", "фикс", "обновление" без деталей

**Примеры хороших сообщений:**
```
"Добавлена система инвентаря"
"Исправлен баг с прыжком на склонах"
"Обновлены текстуры облаков"
"Добавлен префаб сундука с сокровищами"
```

### 3.6 Отправьте изменения на GitHub

```bash
git push upstream qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb
```

**Ожидаемый результат:**
```
Enumerating objects: 15, done.
Counting objects: 100% (15/15), done.
Writing objects: 100% (12/12), 2.34 MiB | 1.50 MiB/s, done.
To https://github.com/boozzeeboom/project-c.git
   abc1234..def5678  qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb -> qwen-code-f3dbb381-a66e-4c3f-8475-9
37fd11685bb
```

---

## Этап 4: Проверка на GitHub

### 4.1 Откройте репозиторий в браузере

```
https://github.com/boozzeeboom/project-c/tree/qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb
```

### 4.2 Убедитесь, что ваши файлы появились

- Проверьте, что новые файлы отображаются
- Посмотрите историю коммитов (Commits)
- Убедитесь, что последний коммит — ваш

---

## Этап 5: Продолжение работы с Qwen Code

### 5.1 Если вы возвращаетесь после перерыва

**Шаг 1:** Откройте Qwen Code

**Шаг 2:** Убедитесь, что рабочая директория:
```
C:\UNITY_PROJECTS\ProjectC_client
```

**Шаг 3:** Напишите команду:

```
Продолжи работу над Project C. Прочитай файлы QWEN_CONTEXT.md и MMO_Development_Plan.md
```

### 5.2 Если нужно обновить контекст

После больших изменений обновите `QWEN_CONTEXT.md`:

```
Обнови QWEN_CONTEXT.md с текущим состоянием проекта
```

---

## 📋 Шпаргалка: Основные команды Git

| Команда | Описание |
|---------|----------|
| `git status` | Показать изменения |
| `git add <файл>` | Добавить файл в индекс |
| `git add -A` | Добавить все изменения |
| `git commit -m "сообщение"` | Сделать коммит |
| `git push upstream <ветка>` | Отправить на GitHub |
| `git log --oneline -n 5` | Последние 5 коммитов |
| `git pull upstream <ветка>` | Получить изменения с GitHub |
| `git diff` | Показать изменения в файлах |

---

## 🎨 Примеры сценариев

### Сценарий 1: Добавили 3D модель

**Что сделали в Unity:**
- Скопировали `Hero.fbx` в `Assets/_Project/Art/Models/`
- Unity создал `Hero.fbx.meta`

**Команды:**
```bash
git status
git add Assets/_Project/Art/Models/Hero.fbx Assets/_Project/Art/Models/Hero.fbx.meta
git commit -m "Добавлена 3D модель героя"
git push upstream qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb
```

---

### Сценарий 2: Написали новый скрипт

**Что сделали в Unity:**
- Создали `InventorySystem.cs` в `Assets/_Project/Scripts/UI/`

**Команды:**
```bash
git status
git add Assets/_Project/Scripts/UI/InventorySystem.cs
git commit -m "Добавлена система инвентаря"
git push upstream qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb
```

---

### Сценарий 3: Изменили настройки проекта

**Что сделали в Unity:**
- Изменили `Edit → Project Settings → Player`
- Unity обновил `ProjectSettings/ProjectSettings.asset`

**Команды:**
```bash
git status
git add ProjectSettings/ProjectSettings.asset
git commit -m "Обновлены настройки игрока (имя компании, версия)"
git push upstream qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb
```

---

### Сценарий 4: Большая сессия работы

**Что сделали:**
- Добавили 5 префабов
- Написали 3 скрипта
- Изменили 2 текстуры
- Обновили сцену

**Команды:**
```bash
# Проверить изменения
git status

# Добавить всё
git add -A

# Посмотреть, что добавлено
git status

# Сделать коммит
git commit -m "Добавлена базовая система взаимодействия с объектами"

# Отправить на GitHub
git push upstream qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb
```

---

## ⚠️ Важные заметки

### Что НЕ нужно коммитить

Эти папки игнорируются автоматически (в `.gitignore`):
- `Library/` — кэш Unity
- `Temp/` — временные файлы
- `Logs/` — логи
- `UserSettings/` — личные настройки
- `obj/`, `bin/` — скомпилированный код

### Git LFS (большие файлы)

Автоматически отслеживаются:
- `*.fbx`, `*.obj` — 3D модели
- `*.png`, `*.jpg`, `*.tga` — текстуры
- `*.wav`, `*.mp3` — аудио
- `*.dll` — библиотеки

**Проверка LFS:**
```bash
git lfs status
git lfs ls-files
```

---

## 🆘 Если что-то пошло не так

### Проблема: Unity не запускается

**Решение:**
```bash
# Удалить кэш Unity
rm -rf Library/ Temp/

# Перезапустить Unity
```

---

### Проблема: Конфликт при слиянии

**Решение:**
```bash
# Отменить слияние
git merge --abort

# Получить свежие изменения
git pull upstream qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb

# Попробовать снова
```

---

### Проблема: Случайно закоммитили лишнее

**Решение (если ещё не сделали push):**
```bash
git reset --soft HEAD~1  # Отменить последний коммит, сохранить изменения
git reset --hard HEAD~1  # Отменить коммит и удалить изменения
```

**Решение (если уже сделали push):**
```bash
# Создать новый коммит, который отменяет изменения
git revert <хеш_коммита>
git push upstream qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb
```

---

### Проблема: Git просит pull перед push

**Решение:**
```bash
# Получить изменения с GitHub
git pull upstream qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb

# Если нет конфликтов, отправить снова
git push upstream qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb
```

---

## ✅ Чек-лист перед завершением сессии

- [ ] Все изменения закоммичены: `git status` → чисто
- [ ] Изменения отправлены на GitHub: `git push`
- [ ] Файл `QWEN_CONTEXT.md` обновлён
- [ ] Вы знаете, как продолжить в следующий раз

---

## 📞 Быстрая справка для Qwen Code

**Продолжить работу:**
```
Продолжи работу над Project C. Прочитай QWEN_CONTEXT.md и MMO_Development_Plan.md
```

**Обновить контекст:**
```
Обнови QWEN_CONTEXT.md с текущим состоянием
```

**Помощь с Git:**
```
Как закоммитить изменения в Unity проекте?
```

**Проверка статуса:**
```
Какой текущий статус проекта?
```

---

**Последнее обновление:** 2 апреля 2026 г.  
**Ветка:** `qwen-code-f3dbb381-a66e-4c3f-8475-937fd11685bb`
