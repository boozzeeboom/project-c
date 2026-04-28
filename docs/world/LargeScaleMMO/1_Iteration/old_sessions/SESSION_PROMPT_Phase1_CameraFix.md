# SESSION PROMPT: Исправление Camera + Input системы

**Проект:** ProjectC_client — Unity 6 URP + NGO (Netcode for GameObjects)  
**Ветка:** `qwen-gamestudio-agent-dev`  
**Агенты:** `@gameplay-programmer` + `@network-programmer`  
**Приоритет:** 🔴 КРИТИЧНО — игра не запускается корректно

---

## 🔥 Текущие симптомы (после предыдущей сессии)

1. **NetworkUI кнопки не кликаются** — Host/Start Server не нажимаются при старте
2. **Персонаж крутится, но не ходит** — визуально реагирует на мышь, WASD не работает
3. **Всё это ДО подключения к сети** — баг происходит на экране меню

---

## 🔍 Корневая причина (диагностирована)

### Проблема: ThirdPersonCamera в сцене с заданным target

**Сценарий:**
```
Play Mode → ThirdPersonCamera.Start() → target != null (задан в Inspector)
         → InitializeCamera() → Cursor.lockState = Locked
         → NetworkUI меню появляется, но курсор ЗАЛОЧЕН → кнопки не кликаются
```

**Пользователь вручную** поместил ThirdPersonCamera в сцену и назначил игрока как target в Inspector. Это означает что камера инициализируется сразу при старте — до меню.

### Проблема: PlayerController/PlayerStateMachine реагирует без сети

`PlayerController.cs` и `PlayerStateMachine.cs` — legacy компоненты, они читают input без проверки IsOwner/IsConnected. Если они активны на player-объекте в сцене → персонаж реагирует на мышь (поворот), но не ходит (CharacterController есть, но что-то блокирует).

---

## 🎯 Что нужно сделать

### Задача 1: Правильная архитектура Camera + Cursor

**Правило:** Cursor должен быть залочен ТОЛЬКО когда игрок подключён и играет.

**Решение:**

```
Состояние меню:    Cursor = FREE  (видимый, кликабельный)
После Start Host:  NetworkPlayer спавнится → SpawnCamera() → InitializeCamera()
                   → Cursor = LOCKED
После Disconnect:  Camera.OnDestroy() → Cursor = FREE  ← уже реализовано
```

**Требуется:**
- ThirdPersonCamera в сцене НЕ должна иметь target в Inspector
- ThirdPersonCamera.InitializeCamera() должна проверять что NetworkManager активен перед блокировкой курсора
- ИЛИ: убрать ThirdPersonCamera из сцены совсем — она спавнится через NetworkPlayer.SpawnCamera()

**Конкретный фикс в ThirdPersonCamera.InitializeCamera():**
```csharp
public void InitializeCamera()
{
    if (_cameraInitialized) return;
    if (target == null) return;

    // ПРОВЕРКА: блокируем курсор только если NetworkManager активен
    // (т.е. игрок реально в игре, а не на экране меню)
    bool inActiveGame = Unity.Netcode.NetworkManager.Singleton != null 
                     && Unity.Netcode.NetworkManager.Singleton.IsListening;
    
    if (inActiveGame)
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    // Иначе — курсор остаётся свободным
    
    _yaw = 0f;
    _pitch = 15f;
    _currentDistance = distance;
    _currentHeight = height;
    UpdateCameraPosition();
    CreateControlHintsUI();
    _cameraInitialized = true;
}
```

### Задача 2: Отключить legacy PlayerController в сцене

**Проблема:** В сцене есть player-объект с `PlayerController` и/или `PlayerStateMachine`.  
Эти компоненты реагируют на input без сетевого соединения.

**Решение:**
- Убедиться что PlayerController.enabled = false по умолчанию (или удалить из сцены)
- PlayerStateMachine только активен когда IsOwner = true (нужно добавить проверку)
- Любой объект игрока в сцене должен быть NetworkObject — не статический

**В PlayerController.cs добавить Guard:**
```csharp
private void Update()
{
    // НЕ обрабатывать ввод если не в сети
    if (!enabled) return;
    // ... существующий код
}
```

### Задача 3: Правильная настройка сцены

**В Hierarchy проверить:**

```
✅ Должно быть:
- NetworkManager (с NetworkManagerController)
- Canvas → NetworkUI (с кнопками)
- WorldStreamingManager (опционально)
- EventSystem

❌ НЕ должно быть standalone:
- ThirdPersonCamera с target в Inspector (спавнится NetworkPlayer'ом!)
- PlayerController/PlayerStateMachine на обычном (не-Network) объекте
- WorldCamera с Cursor.lockState = Locked в Start (уже исправлено)
```

**Правило:** Всё что относится к игроку должно быть в NetworkPlayer prefab'е.  
Сцена = только менеджеры + UI + мир.

---

## 📁 Файлы для изучения

| Файл | Статус | Задача |
|------|--------|--------|
| `Scripts/Core/ThirdPersonCamera.cs` | ✅ Исправлен (частично) | Добавить NetworkManager.IsListening проверку |
| `Scripts/Player/NetworkPlayer.cs` | ✅ Исправлен | Проверить SpawnCamera логику |
| `Scripts/Player/PlayerController.cs` | ⚠️ Требует проверки | Убедиться disabled по умолчанию |
| `Scripts/Player/PlayerStateMachine.cs` | ⚠️ Требует проверки | Legacy? Конфликт с NetworkPlayer? |
| `Scripts/UI/NetworkUI.cs` | ✅ OK | Кнопки работают, если cursor free |
| `Prefabs/NetworkPlayer.prefab` | ❓ Не проверен | Проверить состав компонентов |
| `Prefabs/ThirdPersonCamera.prefab` | ❓ Не проверен | Убедиться target = null |

---

## 🧪 Как тестировать

```
1. Play Mode → Cursor ДОЛЖЕН быть свободен
2. Кнопки Host/Server/Client ДОЛЖНЫ кликаться
3. Start Host → NetworkPlayer спавнится
4. Cursor ДОЛЖЕН залочиться
5. WASD → персонаж ходит
6. Мышь → камера вращается
7. Escape → Disconnect button появляется
8. Disconnect → Cursor ДОЛЖЕН разблокироваться
9. Меню снова кликабельно ✓
```

---

## 🚫 Что НЕ трогать

- `FloatingOriginMP.cs` — исправлен, работает
- `WorldCamera.cs` — cursor lock убран в Start, норм
- `NetworkManagerController.cs` — OK
- `NetworkUI.cs` — OK
- Trade/Contract системы — не трогать

---

## 📝 Контекст предыдущих сессий

**Исправлено в прошлой сессии (2026-04-14):**
- `ThirdPersonCamera` больше не добавляет `FloatingOriginMP` (разрушало иерархию)
- `FloatingOriginMP` не рапаренчивает всю сцену при отсутствии WorldRoot
- `NetworkPlayer.SpawnCamera()` создаёт камеру standalone (без parent)
- `WorldCamera.Start()` не лочит курсор
- `ProjectCSceneSetup.cs` компилируется без ошибок

**Детальный разбор:** `docs/world/LargeScaleMMO/BUGFIX_2026-04-14_CameraSystem.md`

---

## ✅ Критерии завершения

- [ ] Cursor свободен на экране меню
- [ ] NetworkUI кнопки кликабельны
- [ ] После Start Host: персонаж спавнится, cursor лочится
- [ ] WASD работает в пешем режиме  
- [ ] Мышь вращает камеру вокруг персонажа
- [ ] Disconnect → cursor разблокируется
- [ ] 0 ошибок компиляции
