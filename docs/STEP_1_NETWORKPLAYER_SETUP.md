# Шаг 1: Зарегистрировать NetworkPlayer как PlayerPrefab

**Дата:** 4 апреля 2026 г.
**Фаза:** 0 — Починить инфраструктуру
**Ветка:** `qwen-dev`

---

## 🎯 Цель

Связать `NetworkManager` с `NetworkPlayer` чтобы при подключении клиента сервер знал какой префаб спавнить.

---

## ⚠️ ВАЖНО

Этот шаг **выполняется вручную в Unity Editor**. Редактировать `.prefab` файлы как текст нельзя — сломаются ссылки.

---

## 📋 Инструкции (делай по порядку)

### Действие 1: Добавить NetworkPlayer.cs на префаб NetworkPlayer

1. Открой Unity Editor, открой проект `ProjectC_client`
2. В окне **Project** перейди: `Assets/_Project/Prefabs/`
3. Дважды кликни на **`NetworkPlayer.prefab`** — откроется режим редактирования префаба
4. Выбери корневой объект `NetworkPlayer` (вверху иерархии)
5. В окне **Inspector** нажми **Add Component**
6. Найди и добавь **`NetworkPlayer`** (скрипт)
   - Файл: `Assets/_Project/Scripts/Player/NetworkPlayer.cs`
7. Убедись что на префабе теперь **3 компонента**:
   - `NetworkTransform`
   - `NetworkObject`
   - `NetworkPlayer` (новый!)
8. Нажми **Save** (вверху окна префаба, кнопка Save)

---

### Действие 2: Указать PlayerPrefab в NetworkManager

1. В окне **Project** дважды кликни на **`NetworkManager.prefab`**
2. Выбери корневой объект `NetworkManager`
3. В **Inspector** найди компонент **Network Manager**
4. Раскрой секцию **Network Config**
5. Найди поле **PlayerPrefab** (сейчас там `None`)
6. Перетащи **`NetworkPlayer.prefab`** из окна Project в это поле
   - Или нажми кружок-селектор рядом с полем и выбери NetworkPlayer
7. Убедись что поле теперь показывает `NetworkPlayer`
8. Нажми **Save**

---

### Действие 3: Добавить NetworkPlayer в DefaultNetworkPrefabs

1. В окне **Project** найди файл **`DefaultNetworkPrefabs`** (в корне `Assets/`)
2. Выбери его — в **Inspector** увидишь список **List** (сейчас пустой)
3. Нажми **+** (плюс) чтобы добавить элемент
4. В появившемся поле **Network Prefab** перетащи **`NetworkPlayer.prefab`**
5. Убедись что в списке один элемент: `NetworkPlayer`
6. Нажми **Ctrl+S** для сохранения

---

## ✅ Проверка

1. Открой сцену `ProjectC_1`
2. В иерархии найди объект с `NetworkManagerController` (или создай его если нет)
3. Нажми **Play**
4. В UI нажми **Start Host**
5. В **Console** должен появиться лог:
   ```
   Local player spawned
   ```
6. В **Hierarchy** во время игры должен появиться объект `NetworkPlayer(clone)`

---

## ❌ Если что-то не работает

| Проблема | Решение |
|----------|---------|
| NetworkPlayer.cs не находится в Add Component | Проверь что файл в `Assets/_Project/Scripts/Player/NetworkPlayer.cs` и нет ошибок компиляции |
| PlayerPrefab не принимает перетаскивание | Убедись что на NetworkPlayer есть компонент NetworkObject |
| DefaultNetworkPrefabs не показывает список | Перезагрузи Unity (Assets → Refresh) |
| Нет лога "Local player spawned" | Проверь что NetworkManagerController вызывает StartHost() |

---

## 🔗 Связанные файлы

- `Assets/_Project/Prefabs/NetworkPlayer.prefab`
- `Assets/_Project/Prefabs/NetworkManager.prefab`
- `Assets/DefaultNetworkPrefabs.asset`
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`

---

**После выполнения:** сообщи мне результат, закоммитим изменения.
