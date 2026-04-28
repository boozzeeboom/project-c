# Тестирование FloatingOriginMP

## Цель теста
Проверить что артефакты на персонаже при >1M units исчезли.

## Подготовка

1. Открыть сцену ProjectC_1
2. Найти FloatingOriginMP в сцене
3. Установить параметры:
   - `mode = Local` (для одиночной игры)
   - `threshold = 150000`
   - `shiftRounding = 10000`
   - `showDebugLogs = true`
   - `showDebugHUD = true`

## Тест 1: Singleplayer (Local mode)

### Шаги:
1. Запустить Play Mode
2. Нажать F5 или телепортироваться на Far Peak
3. Долететь до позиции > 150,000 units
4. Наблюдать за:

### Ожидаемые результаты:
- ✅ HUD показывает: `Pos: ~150000, 0, 0`
- ✅ HUD показывает: `Offset: 150000` после сдвига
- ✅ Горы/облака сдвигаются (визуально "уезжают")
- ✅ Игрок остаётся на месте
- ✅ **НЕТ jitter/дрожания персонажа**

## Тест 2: Multiplayer (ServerAuthority)

### Подготовка:
1. Host: `FloatingOriginMP.mode = ServerAuthority`
2. Client: `FloatingOriginMP.mode = ServerSynced`

### Шаги:
1. Запустить Host
2. Запустить Client и подключиться
3. Host телепортироваться на Far Peak
4. Проверить логи:

### Ожидаемые результаты:
- ✅ Host логи: `SERVER SHIFT: offset=...`
- ✅ Host логи: `BroadcastWorldShiftRpc sent to all clients`
- ✅ Client логи: `Received world shift from server: offset=...`
- ✅ **НЕТ артефактов на персонаже на обоих клиентах**

## Debug признаки проблем

### Проблема: Игрок уезжает от гор
- Лог: `TradeZones restored: 0/1` — TradeZones не найден
- Лог: `WARNING: No roots found!` — WorldRoot не найден

### Проблема: Jitter персонажа
- `GetWorldPosition()` возвращает неправильную позицию
- Проверить: NetworkPlayer существует с IsOwner=true
- Проверить: позиция игрока > 10,000 units

### Проблема: Двойной сдвиг
- `_shiftCount` увеличивается слишком быстро
- Проверить: cooldown работает (0.5 секунды)

## Как использовать F-клавиши

| Клавиша | Действие |
|---------|----------|
| F5 | Телепорт на Start Peak (~5000) |
| F6 | Телепорт на End Peak (~500000) |
| F7 | Загрузить чанки вокруг игрока |
| F8 | Сброс FloatingOrigin |
| F9 | Toggle grid visualization |
| F10 | Toggle debug HUD |
