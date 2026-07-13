# Respawn System — Использование

## Быстрый старт

1. Открой `BootstrapScene.unity`
2. Найди GameObject `RespawnManager`
3. В инспекторе добавь элементы в список `Respawn Points`

## Добавление точки респавна

### Через инспектор RespawnManager

1. Нажми **Add Respawn Point** в кастомном инспекторе RespawnManager
2. Настрой поля:
   - **Fallback Position** — позиция если spawnPoint не назначен
   - **Spawn Point** — ссылка на Transform-пустышку в сцене (опционально)
   - **Trigger Zone** — ссылка на Collider с `isTrigger = true` (опционально)

### Размещение якоря в WorldScene

1. Открой нужную WorldScene (например `WorldScene_0_0.unity`)
2. Создай пустой GameObject в нужной позиции
3. Назови его `Respawn_Default` (или по смыслу)
4. В BootstrapScene, в RespawnManager, перетащи этот Transform в поле Spawn Point

### Триггер-зона

1. Создай GameObject с Collider (BoxCollider, SphereCollider), `isTrigger = true`
2. Размести в нужной сцене
3. В RespawnManager перетащи Collider в поле Trigger Zone нужного элемента
4. При входе игрока в эту зону — его точка респавна меняется

## Проверка в Play Mode

1. Запусти Host
2. Спрыгни с платформы вниз (Y < 0)
3. Игрок должен телепортироваться на fallback-точку
4. В консоли появится: `[PlayerRespawnTracker] Teleporting player to ...`

## API

### Ручной респавн (из кода)
```csharp
var tracker = GetComponent<PlayerRespawnTracker>();
tracker.RequestRespawnServerRpc();
```

### Смена точки респавна (из кода)
```csharp
var manager = FindAnyObjectByType<RespawnManager>();
tracker.SetRespawnIndex(manager, newIndex);
```
