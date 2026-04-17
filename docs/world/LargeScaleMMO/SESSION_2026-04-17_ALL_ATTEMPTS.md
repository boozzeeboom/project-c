# FloatingOriginMP — Все попытки решения — 17.04.2026

**Дата:** 17.04.2026, 19:10  
**Проект:** ProjectC_client  
**Status:** 🔴 ВСЕ ПОПЫТКИ НЕУДАЧНЫ

---

## СВОДКА ПРОБЛЕМЫ

### Исходная проблема
`LocalClient.PlayerObject.transform.position` — это ИНТЕРПОЛИРОВАННАЯ позиция.
- Во время игры: (-78, 1, 61) — неправильная
- На паузе: (-304970, 901228) — правильная

---

## ВСЕ ПОПЫТКИ

### Попытка 1: Server Authority Mode
```
mode = OriginMode.ServerAuthority
```
**Результат:** ❌ НЕ ПОМОГЛО — та же проблема

**Лог:**
```
cameraWorldPos=(-10, 1, 93) — во время игры
cameraPos=(49086.87, 0.58, 936510.10) — на паузе
```

**Вывод:** Проблема НЕ в режиме работы, а в ИСТОЧНИКЕ позиции

---

### Попытка 2: Camera.main вместо LocalClient.PlayerObject
```
Было: positionSource > LocalClient.PlayerObject > Camera.main > _camera
Стало: positionSource > Camera.main > _camera
```

**Результат:** ❌ КАТАСТРОФА — теперь (0,0,0)!

**Лог:**
```
GetWorldPosition: using _camera=(0, 0, 0)
cameraWorldPos=(0, 0, 0)
adjustedPos=(0, 0, 0)
dist=0
```

**Причина катастрофы:**
- FloatingOriginMP ПОВЕШЕН НА TradeZones/WorldRoot
- `_camera = GetComponent<Camera>()` находит камеру НА этом объекте
- Эта камера — ЧАСТЬ TradeZones, поэтому ОНА ТОЖЕ СДВИГАЕТСЯ!
- После сдвига мира позиция этой камеры = (0,0,0) относительно origin

**Визуально:**
```
TradeZones/WorldRoot (НА ЭТОМ ОБЪЕКТЕ)
├── FloatingOriginMP ← ЗДЕСЬ ПОВЕШЕН СКРИПТ
├── Camera ← ЭТА КАМЕРА СДВИГАЕТСЯ ВМЕСТЕ С МИРОМ
├── WorldRoot
│   ├── Mountains
│   ├── Clouds
│   └── ...
```

---

## АРХИТЕКТУРНАЯ ПРОБЛЕМА

### Где находится FloatingOriginMP?

**Текущая архитектура:**
```
TradeZones (GameObject сцены)
├── FloatingOriginMP ← ПОВЕШЕН ЗДЕСЬ
├── Camera ← ЭТО НЕПРАВИЛЬНАЯ КАМЕРА!
└── WorldRoot
    ├── Mountains (сдвигается)
    ├── Clouds (сдвигается)
    └── ...
```

**Правильная архитектура:**
```
Main Camera (НА ВЕРХНЕМ УРОВНЕ СЦЕНЫ!)
├── ThirdPersonCamera
└── FloatingOriginMP ← ДОЛЖЕН БЫТЬ ЗДЕСЬ ИЛИ НА CAMERA

TradeZones (НЕ СДВИГАЕТСЯ)
├── WorldRoot
│   ├── Mountains (сдвигается)
│   └── Clouds (сдвигается)
└── ...
```

### Проблема: FloatingOriginMP на TradeZones

Когда FloatingOriginMP на TradeZones:
1. `_camera = GetComponent<Camera>()` находит камеру НА TradeZones
2. Эта камера — часть TradeZones, поэтому сдвигается вместе с ней
3. `Camera.main` тоже может быть этой камерой
4. Позиция = (0,0,0) потому что мир уже сдвинут

---

## РЕШЕНИЯ

### Решение 1: Назначить positionSource вручную

В Inspector:
1. Найти ThirdPersonCamera в сцене
2. Перетащить её в поле `positionSource` на FloatingOriginMP

**Код уже поддерживает это:**
```csharp
if (positionSource != null)
{
    return positionSource.position; // Приоритет 1
}
```

### Решение 2: Изменить архитектуру сцены

1. Переместить FloatingOriginMP на Main Camera (верхний уровень)
2. Убрать камеру с TradeZones
3. Использовать ThirdPersonCamera

### Решение 3: Искать камеру по тегу

```csharp
// Ищем Main Camera (с тегом "MainCamera")
Camera mainCam = GameObject.FindGameObjectWithTag("MainCamera") as Camera;
if (mainCam != null && mainCam.transform.parent == null) // Не child TradeZones
{
    return mainCam.transform.position;
}
```

### Решение 4: Убрать камеру с TradeZones

Удалить компонент Camera с TradeZones/WorldRoot чтобы `GetComponent<Camera>()` не находил её.

---

## СЛЕДУЮЩИЕ ШАГИ

1. **Immediate:** Назначить `positionSource` вручную на ThirdPersonCamera
2. **Verify:** Убедиться что ThirdPersonCamera НЕ под TradeZones
3. **Alternative:** Изменить архитектуру сцены

---

**Автор:** Claude Code  
**Дата:** 17.04.2026, 19:10 MSK
