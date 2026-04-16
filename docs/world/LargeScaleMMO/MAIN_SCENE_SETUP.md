# Настройка основной сцены для World Streaming Phase 2

**Дата:** 17 апреля 2026  
**Проект:** ProjectC_client  

---

## Проблема

При координатах >100,000 единиц появляются артефакты рендеринга из-за потери точности float.  
В тестовой сцене всё работает, в основной — нет.

## Причина

FloatingOriginMP требует правильной настройки:
1. **WorldRoot** — пустой объект на сцене, содержащий все world objects
2. **World objects** должны быть **children** WorldRoot
3. **FloatingOriginMP** должен быть на Main Camera
4. **worldRootNames** должны включать имена всех world объектов

---

## Быстрое решение

### Вариант 1: Runtime Setup (рекомендуется)

1. Откройте сцену `ProjectC_1.unity`
2. Создайте пустой объект: `GameObject → Create Empty`
3. Назовите его `StreamingSetup`
4. Добавьте компонент: `Add Component → StreamingSetupRuntime`
5. Play Mode — скрипт автоматически настроит сцену

### Вариант 2: Editor Setup

1. Откройте сцену `ProjectC_1.unity`
2. Menu: `ProjectC → Prepare Main Scene (Phase 2)`
3. Нажмите "Apply to Current Scene"
4. Или: `ProjectC → Fix WorldRoot Hierarchy`

---

## Ручная настройка

### 1. Создать WorldRoot

```
Hierarchy:
└── WorldRoot (пустой, position = 0,0,0)
    ├── Mountains
    ├── Clouds
    ├── Farms
    └── TradeZones
```

### 2. Добавить FloatingOriginMP

1. Выберите Main Camera в иерархии
2. Add Component → FloatingOriginMP
3. В Inspector настройте:

```
Mode: Local
threshold: 100000
shiftRounding: 10000
showDebugLogs: ✓
showDebugHUD: ✓

worldRootNames:
- Mountains
- Clouds
- Farms
- TradeZones
- World
- WorldRoot
- ChunksContainer
```

### 3. Добавить StreamingTest

1. Создайте пустой объект: `GameObject → Create Empty`
2. Назовите `StreamingTest`
3. Add Component → StreamingTest
4. В Inspector:

```
useLocalPlayerPosition: ✓
teleportPlayer: ✓
```

### 4. Проверка

1. Запустите Play Mode
2. В консоли должны появиться логи:
   - `[StreamingSetupRuntime] Initializing...`
   - `[StreamingTest] Found local player: ...`
3. Нажмите F5 — персонаж должен телепортироваться
4. Нажмите F7 — чанки должны загрузиться

---

## Структура иерархии

### Правильная иерархия:

```
Scene
├── Main Camera (has FloatingOriginMP)
├── NetworkManagerController
├── WorldStreamingManager (has WorldChunkManager, ChunkLoader, etc.)
├── StreamingTest
├── StreamingSetupRuntime (временно, для настройки)
└── WorldRoot (position = 0,0,0)
    ├── Mountains
    │   ├── Massif_0
    │   │   └── Peak_0
    │   └── Massif_1
    ├── Clouds
    │   └── CloudLayer
    ├── Farms
    │   └── Farm_0
    └── TradeZones
```

### Неправильная иерархия (артефакты):

```
Scene
├── Mountains          ← НЕПРАВИЛЬНО! Не под WorldRoot
├── Clouds             ← НЕПРАВИЛЬНО!
├── Player
│   └── Camera         ← FloatingOriginMP здесь, но WorldRoot не найден
└── WorldRoot          ← Пустой, ничего не содержит
```

---

## FloatingOriginMP настройки

| Параметр | Значение | Описание |
|----------|---------|----------|
| `mode` | `Local` | Одиночная игра. Для мультиплея: ServerAuthority |
| `threshold` | `100000` | Порог сдвига мира (units) |
| `shiftRounding` | `10000` | Округление сдвига для избежания accumulation |
| `worldRootNames` | Список | Имена объектов для поиска и сдвига |

### Режимы для мультиплея:

| Режим | Когда использовать |
|-------|------------------|
| `Local` | Singleplayer, тестирование |
| `ServerAuthority` | Host (Server + Client) |
| `ServerSynced` | Выделенный клиент |

---

## NetworkPlayer проблема

**Проблема:** NetworkPlayer имеет систему коррекции позиции. При телепортации персонаж плавно возвращается к серверной позиции.

**Решение:** Используйте RPC для телепортации:

```csharp
// В NetworkPlayer, добавьте ServerRpc:
[ServerRpc]
public void TeleportServerRpc(Vector3 position)
{
    transform.position = position;
    BroadcastPositionSyncRpc(position); // Синхронизация
}
```

Или отключите коррекцию позиции на время тестирования.

---

## Отладка

### Включить все логи:

1. FloatingOriginMP → showDebugLogs = true
2. WorldStreamingManager → showDebugHUD = true  
3. PlayerChunkTracker → showDebugLogs = true
4. StreamingSetupRuntime → проверьте консоль

### Ожидаемые логи:

```
[StreamingSetupRuntime] Initializing...
[StreamingSetupRuntime] WorldRoot organized: 5 children
[StreamingSetupRuntime] FloatingOriginMP configured...
[StreamingTest] Initializing...
[StreamingTest] Camera: Main Camera
[StreamingTest] Found local player: NetworkPlayer(Clone)
[StreamingTest] Tracking local player transform
[StreamingTest] Test controls: F5=next...
```

### Если персонаж не телепортируется:

1. Проверьте что `useLocalPlayerPosition = true` в StreamingTest
2. Проверьте что `teleportPlayer = true` в StreamingTest
3. Проверьте что NetworkPlayer найден (лог в консоли)
4. Проверьте NetworkTransform/NetworkObject компоненты

### Если артефакты при >100k:

1. Проверьте что WorldRoot существует и содержит все объекты
2. Проверьте что FloatingOriginMP найден (лог в консоли)
3. Проверьте что threshold = 100000
4. Нажмите F8 для ручного сброса origin

---

## Скрипты для настройки

| Скрипт | Путь | Описание |
|--------|------|----------|
| `PrepareMainScene.cs` | Editor | Editor window для настройки сцены |
| `StreamingSetupRuntime.cs` | Core | Runtime настройка (Play Mode) |
| `FixWorldRootHierarchy` | Menu | Быстрое исправление иерархии |

### Меню:

```
ProjectC
├── Prepare Main Scene (Phase 2)    ← Editor window
├── Prepare Test Scene (Phase 2)   ← Editor window
└── Fix WorldRoot Hierarchy         ← Quick fix
```

---

## Следующие шаги

1. [ ] Настроить сцену одним из способов выше
2. [ ] Протестировать F5/F6/F7
3. [ ] Проверить FloatingOrigin при >100k координатах
4. [ ] Настроить режим ServerAuthority для мультиплея

---

**Автор:** Claude Code  
**Дата:** 17.04.2026
