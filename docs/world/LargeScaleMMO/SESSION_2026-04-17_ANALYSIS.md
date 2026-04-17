# Session Analysis: FloatingOriginMP — 17.04.2026

**Статус:** ❌ НЕ РАБОТАЕТ — требует исправления  
**Автор:** Claude Code  
**Дата:** 17.04.2026, 17:13  

---

## ПРОБЛЕМА: NullReferenceException в ResetOrigin()

### Стек ошибки:
```
NullReferenceException: Object reference not set to an instance of an object
  at FloatingOriginMP.ResetOrigin()  ← Строка 337
  at StreamingTest_AutoRun.TeleportToTestPosition()  ← Строка 281
```

### Анализ:

В `ResetOrigin()` (строка ~337) происходит обращение к `_camera`, но `_camera == null`.

**Почему:**
1. FloatingOriginMP на пустом объекте сцены (НЕ на камере)
2. `_camera = GetComponent<Camera>()` возвращает null
3. `Camera.main` тоже null (камера ещё не инициализирована или tagged неправильно)
4. `_camera.transform.position` → NullReferenceException

---

## КОРНЕВАЯ ПРИЧИНА

### Проблема архитектуры:

FloatingOriginMP **ТРЕБУЕТ** камеру для работы, но:
- При размещении на пустом объекте — камера не найдена
- При размещении на префабе камеры — создаются дубликаты

### Две модели использования конфликтуют:

| Модель | Где должен быть | Требует |
|--------|----------------|---------|
| **A: Single Camera** | На Main Camera | Камера на объекте ✓ |
| **B: Multi Camera** | На пустом объекте | Camera.main для fallback ✗ |

**Текущий код поддерживает только Model A**, но пользователь разместил компонент по Model B.

---

## СТРУКТУРНЫЕ ПРОБЛЕМЫ

### 1. Camera зависимость

```csharp
// Текущий код (НЕПРАВИЛЬНО)
private Camera _camera;

void Awake() {
    _camera = GetComponent<Camera>();  // Работает только если на Camera
    if (_camera == null) {
        _camera = Camera.main;  // Fallback, но нестабильно
    }
}

void LateUpdate() {
    Vector3 pos = _camera.transform.position;  // ✗ _camera может быть null
}
```

### 2. Мир на 90 миллионах

WorldRoot и другие объекты мира находятся на позициях ~90,000,000.
Это произошло в предыдущих итерациях когда FloatingOriginMP работал неправильно.

### 3. Threshold слишком высокий (исправлено)

| Версия | threshold | Проблема |
|--------|-----------|----------|
| Было | 1,000,000 | Слишком большой — сдвиг не происходил |
| Стало | 150,000 | Должен работать |

---

## ЧТО ИСПРАВЛЕНО В ЭТОЙ СЕССИИ

1. ✅ threshold изменён на 150,000
2. ✅ shiftRounding изменён на 10,000
3. ✅ Добавлен Camera.main fallback
4. ✅ Создана документация FLOATING_ORIGIN_STATUS_2026-04-17.md
5. ✅ Создана эта сессия анализа

---

## ЧТО НУЖНО ИСПРАВИТЬ В СЛЕДУЮЩЕЙ СЕССИИ

### Приоритет 1: Исправить NullReferenceException

**Вариант A: Использовать позицию игрока вместо камеры**

```csharp
// В LateUpdate:
Vector3 worldPos;
if (_camera != null) {
    worldPos = _camera.transform.position;
} else {
    // Найти локального игрока
    var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
    if (player != null) {
        worldPos = player.transform.position;
    }
}
```

**Вариант B: Потребовать камеру явно**

```csharp
[RequiredComponent]
[SerializeField] private Camera _camera;
```

### Приоритет 2: Исправить позиции мира

В Editor (НЕ Play Mode):
1. Выбрать WorldRoot в Hierarchy
2. Position → (0, 0, 0)
3. Clouds → (0, 0, 0)
4. TradeZones → (0, 0, 0)
5. Все остальные world objects → ~0, 0, 0

### Приоритет 3: Убрать дубликаты

1. Удалить FloatingOriginMP с префаба ThirdPersonCamera
2. Оставить только на пустом объекте сцены ИЛИ на Main Camera

---

## АРХИТЕКТУРНОЕ ПРЕДЛОЖЕНИЕ

### Решение: Отделить позицию от компонента

```csharp
public class FloatingOriginMP : MonoBehaviour
{
    [Header("Position Source")]
    [Tooltip("Откуда брать позицию для проверки threshold. Null = использовать Camera.main.")]
    public Transform positionSource;
    
    private Vector3 GetWorldPosition() {
        if (positionSource != null) return positionSource.position;
        if (_camera != null) return _camera.transform.position;
        return Camera.main?.transform.position ?? Vector3.zero;
    }
}
```

Это позволит:
- Размещать на пустом объекте
- Указать конкретный Transform для позиции
- Fallback на Camera.main

---

## СЛЕДУЮЩИЕ ШАГИ (ДЛЯ СЛЕДУЮЩЕЙ СЕССИИ)

1. [ ] Исправить ResetOrigin() — добавить null check для _camera
2. [ ] Добавить positionSource field
3. [ ] Исправить LateUpdate() — использовать позицию игрока если камера null
4. [ ] В Editor: сбросить WorldRoot позиции на (0,0,0)
5. [ ] Убрать FloatingOriginMP с префаба
6. [ ] Протестировать телепорт к 150,000
7. [ ] Проверить что артефакты исчезают

---

## ФАЙЛЫ СВЯЗАННЫЕ С ПРОБЛЕМОЙ

| Файл | Статус | Комментарий |
|------|--------|-------------|
| `FloatingOriginMP.cs` | ⚠️ Требует правки | NullReferenceException |
| `StreamingTest_AutoRun.cs` | ✅ Работает | Вызывает ResetOrigin |
| `PlayerChunkTracker.cs` | ⚠️ Отдельный баг | MissingReferenceException |
| `ChunkLoader.cs` | ⚠️ Warning only | chunksParentTransform |

---

## ДОКУМЕНТАЦИЯ СОЗДАННАЯ В ЭТОЙ СЕССИИ

1. `docs/world/LargeScaleMMO/FLOATING_ORIGIN_STATUS_2026-04-17.md` — статус и план
2. `docs/world/LargeScaleMMO/SESSION_2026-04-17_ANALYSIS.md` — этот документ

---

**Закрытие сессии:** 17.04.2026, 17:13 MSK  
**Следующая сессия:** Исправление FloatingOriginMP.NullReferenceException
