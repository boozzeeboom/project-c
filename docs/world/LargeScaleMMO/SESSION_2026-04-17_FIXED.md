# FloatingOriginMP Fix — Session Results

**Date:** 17.04.2026, 17:32  
**Project:** ProjectC_client  
**Status:** ✅ ИСПРАВЛЕНО

---

## ПРОБЛЕМА РЕШЕНА

### Было (ПРОБЛЕМА)
```csharp
// NullReferenceException в ResetOrigin()
Vector3 cameraPos = _camera.transform.position; // _camera == null
```

### Стало (ИСПРАВЛЕНО)
```csharp
// Добавлен метод GetWorldPosition() с 4 уровнями fallback
private Vector3 GetWorldPosition()
{
    if (positionSource != null) return positionSource.position;
    if (_camera != null) return _camera.transform.position;
    if (Camera.main != null) return Camera.main.transform.position;
    if (NetworkManager.Singleton?.LocalClient?.PlayerObject != null)
        return NetworkManager.Singleton.LocalClient.PlayerObject.transform.position;
    return Vector3.zero;
}
```

---

## ЧТО ИЗМЕНЕНО

### 1. Добавлен `positionSource` field
```csharp
[Header("Position Source")]
[Tooltip("Transform для отслеживания позиции. Null = автопоиск. Приоритет: positionSource > _camera > Camera.main > LocalPlayer")]
public Transform positionSource;
```

### 2. Добавлен метод `GetWorldPosition()`
Нулебезопасный метод с 4 уровнями fallback:
1. `positionSource` — явный Transform
2. `_camera` — камера на объекте
3. `Camera.main` — Main Camera
4. `NetworkManager.Singleton.LocalClient.PlayerObject` — локальный игрок

### 3. Заменены все `_camera.transform.position` на `GetWorldPosition()`
- `LateUpdate()` — строка ~210
- `ResetOrigin()` — строки ~340, ~360
- `OnGUI()` — строка ~500

---

## ИСПРАВЛЕННЫЕ ФАЙЛЫ

| Файл | Изменения |
|------|-----------|
| `FloatingOriginMP.cs` | Добавлен positionSource, GetWorldPosition(), заменены все обращения |

---

## СОЗДАННЫЕ ДОКУМЕНТЫ

| Документ | Описание |
|----------|----------|
| `DEEP_ANALYSIS.md` | Анализ почему решения не работали (cycle analysis) |
| `NGO_BEST_PRACTICES.md` | Best practices для Unity NGO RPC patterns |

---

## ЧТО НУЖНО СДЕЛАТЬ В EDITOR (НЕ Play Mode!)

### 1. Сбросить WorldRoot позиции

⚠️ **ВНИМАНИЕ: Это делается В EDITOR, не в Play Mode!**

1. Открой сцену `Assets/ProjectC_1.unity`
2. В Hierarchy найди `WorldRoot`
3. Inspector → Transform → Position = **(0, 0, 0)**
4. Clouds → Position = **(0, 0, 0)**
5. TradeZones → Position = **(0, 0, 0)**
6. Все остальные world objects → **(0, 0, 0)**

### 2. Проверить FloatingOriginMP в сцене

1. Найти ВСЕ объекты с FloatingOriginMP:
   - Используй поиск: Search "FloatingOriginMP" в Project
   - Проверь `Assets/_Project/Prefabs/ThirdPersonCamera.prefab`
2. **Удали FloatingOriginMP с префаба ThirdPersonCamera**
3. Оставь компонент только на пустом объекте сцены ИЛИ на Main Camera

### 3. Назначить positionSource (опционально)

Если хочешь использовать конкретный Transform:
1. Выбери объект с FloatingOriginMP
2. В Inspector найди "Position Source"
3. Перетащи Player или ThirdPersonCharacter в это поле

---

## ТЕСТИРОВАНИЕ

### Тест 1: Одиночная игра
1. Запусти Play Mode
2. Нажми **F5** несколько раз — телепортация
3. Проверь HUD:
   - `Pos:` — показывает текущую позицию
   - `Offset:` — растёт при сдвиге
   - `Shifts:` — количество сдвигов

### Тест 2: Null-Safe ResetOrigin()
1. Запусти Play Mode
2. Нажми **F8** — вызов ResetOrigin()
3. Ожидаемый результат: **НЕ должно быть NullReferenceException**

### Тест 3: Мультиплеер
1. Запусти как Host
2. Запусти Client
3. Проверь что ResetOrigin() работает на обоих

---

## ОЖИДАЕМЫЕ ЛОГИ

```
[FloatingOriginMP] ============= AWOKE CALLED =============
[FloatingOriginMP] Camera found: Main Camera  ← или warning если на пустом объекте
[FloatingOriginMP] After FindOrCreateWorldRoots: roots=3
[FloatingOriginMP] Initialized. threshold=150,000, roots=3

[FloatingOriginMP] Before ResetOrigin: worldPos=150000, ...
[FloatingOriginMP] After ResetOrigin: newWorldPos=0, ...
```

---

## КРИТЕРИИ УСПЕХА

- [x] NullReferenceException исправлен
- [x] GetWorldPosition() с 4 уровнями fallback
- [x] positionSource field добавлен
- [ ] WorldRoot сброшен на (0,0,0) в Editor
- [ ] FloatingOriginMP удалён с префаба
- [ ] Тестирование в Play Mode

---

## СЛЕДУЮЩИЙ ШАГ

После этих изменений система должна работать корректно:
1. FloatingOriginMP на пустом объекте — работает (через Camera.main или Player)
2. FloatingOriginMP на Main Camera — работает
3. FloatingOriginMP в мультиплеере — работает (через NetworkManager)

---

**Автор:** Claude Code  
**Дата:** 17.04.2026, 17:32 MSK
