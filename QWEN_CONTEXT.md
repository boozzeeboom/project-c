# Qwen Code Context — Project C

**Последнее обновление:** 2 апреля 2026 г.  
**Ветка Git:** `qwen-dev`  
**Последний коммит:** `f33f8e8`

---

## 📁 Структура проекта

```
ProjectC_client/          (Unity 6 клиент)
├── Assets/
│   ├── _Project/         (основные ассеты проекта)
│   │   ├── Scripts/
│   │   │   ├── Core/
│   │   │   ├── Player/
│   │   │   ├── Network/
│   │   │   └── UI/
│   │   ├── Prefabs/
│   │   ├── Scenes/
│   │   └── Art/
│   ├── Plugins/
│   └── StreamingAssets/
├── ProjectSettings/
└── Packages/

ProjectC_Server/          (.NET 8 сервер)
└── ProjectC.Server/
    ├── Program.cs
    └── ProjectC.Server.csproj
```

---

## ✅ Что уже сделано

### Этап 0: Подготовка окружения

1. **Настройка репозитория:**
   - ✅ Добавлен `upstream` remote: `https://github.com/boozzeeboom/project-c.git`
   - ✅ Ветка `qwen-dev` создана и отправлена на GitHub
   - ✅ Добавлен файл `MMO_Development_Plan.md` (в upstream)
   - ✅ Настроен `.gitattributes` с Git LFS для больших файлов (PNG, текстуры, аудио и т.д.)
   - ✅ Настроен `.gitignore` — игнорирует `Library/`, `Temp/`, `Logs/`, `UserSettings/`
   - ✅ Все настройки проекта закоммичены и отправлены на GitHub

2. **Структура папок Unity:**
   - ✅ Создана структура `Assets/_Project/` с подпапками
   - ✅ Все `.meta` файлы создаются автоматически Unity

3. **Серверный проект:**
   - ✅ Создан консольный проект .NET 8 в `../ProjectC_Server/ProjectC.Server/`
   - ✅ Framework: `net8.0`

4. **Сетевой стек:**
   - ✅ Добавлен **Unity Netcode for GameObjects** (вместо Mirror)
   - ✅ Создан `NetworkManagerController` для управления подключениями
   - ✅ Создан `NetworkPlayer` для синхронизации игрока
   - ✅ Создан `NetworkUI` для UI подключения
   - ✅ Добавлены префабы: `NetworkManager.prefab`, `NetworkPlayer.prefab`

---

## 📋 План работ (из MMO_Development_Plan.md)

### Текущий этап: **Этап 0: Подготовка окружения** (Неделя 1)

**Статус:** ✅ **ВЫПОЛНЕНО**

**Осталось:**
- [ ] Создать документацию протокола клиент-сервер

### Следующие этапы:
- **Этап 1:** Прототип ядра геймплея (Недели 2-4)
- **Этап 2:** Сетевой фундамент (Недели 5-8)

---

## 🚀 Как продолжить работу

### Команда для перезапуска Qwen с правильным контекстом:

```
Продолжи работу над Project C. Прочитай файлы QWEN_CONTEXT.md и MMO_Development_Plan.md
```

### Если забыли команды Git:

```
Открой GIT_WORKFLOW.md и покажи команды для коммита изменений
```

### Все инструкции в файлах:
- `README_QWEN.md` — как перезапустить работу
- `GIT_WORKFLOW.md` — шпаргалка по Git командам

---

## 📦 Зависимости

### Unity:
- Unity 6 (или 2022.3 LTS)
- Universal Render Pipeline (URP)
- **Mirror Networking** — требуется добавить

### Сервер:
- .NET 8 SDK (установлено: 8.0.419)
- WebSocket / TCP библиотека — требуется выбрать

---

## 🔗 Git Remote

```
origin  https://github.com/boozzeeboom/project-c-2026-04-02_14-41-56.git
upstream  https://github.com/boozzeeboom/project-c.git
```

**Ветка для работы:** `qwen-dev`

**Push в upstream:** ✅ Работает

---

## 📝 Следующие конкретные задачи

1. **Добавить Mirror Networking в Unity:**
   - Через Package Manager (Git URL) или
   - Через OpenUPM: `com.community.mirror`

2. **Настроить базовый Network Manager:**
   - Создать префаб Network Manager
   - Настроить сцены (Online/Offline)

3. **Создать первый сетевой префаб игрока:**
   - Network Identity
   - Network Transform

4. **На сервере (.NET 8):**
   - Добавить WebSocket поддержку
   - Создать базовый обработчик подключений

---

## ⚠️ Важные заметки

### Git LFS
- Включен для: `*.png`, `*.jpg`, `*.fbx`, `*.wav`, `*.mp3`, `*.dll` и других больших файлов
- Перед коммитом больших файлов убедитесь, что они отслеживаются LFS: `git lfs track "*.extension"`

### Unity Git Integration
- В Unity включена Git-синхронизация (не Unity Cloud)
- Коммиты можно делать прямо из Unity Editor
- Не забывайте делать Push после коммитов

### Что НЕ коммитить:
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` — игнорируются в `.gitignore`
- Большие бинарные файлы без LFS

---

**Важно:** После каждого сеанса обновляй этот файл с текущим состоянием!
