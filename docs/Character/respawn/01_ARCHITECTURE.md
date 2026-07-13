# Respawn System — Архитектура

## Обзор

Система респавна отвечает за возврат игрока на заданную точку при:
- Падении ниже Y = 0 (смертельная высота)
- Ручном вызове (через API)

Точка респавна может меняться в рантайме при входе в триггер-зоны.

## Компоненты

### RespawnPointData (`Assets/_Project/Scripts/Player/RespawnPointData.cs`)
`[Serializable]` структура, описывающая одну точку респавна:
- `fallbackPosition` (Vector3) — позиция по умолчанию
- `spawnPoint` (Transform) — ссылка на пустышку в мире (приоритетнее fallback)
- `triggerZone` (Collider) — опциональная зона, назначающая эту точку активной

### RespawnManager (`Assets/_Project/Scripts/World/RespawnManager.cs`)
MonoBehaviour на GameObject в BootstrapScene. Хранит `List<RespawnPointData>`.
- `GetEffectivePosition(int index)` — возвращает позицию (spawnPoint ?? fallback)
- `FindRespawnIndex(Collider trigger)` — ищет индекс точки по триггер-зоне
- Кастомный инспектор `RespawnManagerEditor`

### PlayerRespawnTracker (`Assets/_Project/Scripts/Player/PlayerRespawnTracker.cs`)
MonoBehaviour на префабе NetworkPlayer. Отвечает за:
- Проверку `transform.position.y <= 0f` каждый кадр (только сервер)
- `OnTriggerEnter` — поиск RespawnManager, смена активной точки
- `ServerRpc RequestRespawn()` → `Rpc TeleportTo(Vector3)` — телепорт + сброс скорости

## Flow

```
1. Игрок входит в TriggerZone (Collider.isTrigger)
   → PlayerRespawnTracker.OnTriggerEnter
   → RespawnManager.FindRespawnIndex(collider)
   → _currentRespawnIndex = index

2. Игрок падает ниже Y = 0
   → PlayerRespawnTracker.Update() (IsServer)
   → _currentRespawnIndex = 0 (fallback) если не назначена
   → RespawnManager.GetEffectivePosition(_currentRespawnIndex)
   → TeleportTo(position)

3. Клиент получает Rpc_TeleportTo
   → transform.position = position
   → _velocity = Vector3.zero
   → Physics.SyncTransforms()
```

## Сетевая модель

Сервер авторитативен: проверка Y < 0 и телепорт происходят на сервере.
Клиент получает позицию через `ClientRpc`.

## Настройка в BootstrapScene

BootstrapSceneGenerator создаёт GameObject `RespawnManager` с компонентом `RespawnManager`.
В инспекторе можно добавлять/удалять точки респавна, назначать Transform-якоря и триггер-зоны.

## WorldScene_0_0

В сцене размещён якорь `Respawn_Default` на координатах (39992, 2502.77, 40000) —
это fallback-точка по умолчанию.
