# FloatingOriginMP — Проблемы и Решения (17.04.2026)

## Статус: Частично Исправлено

---

## Проблема 1: Двойной вызов OnWorldShifted

### Симптомы
- `OnWorldShifted` вызывался ДВАЖДЫ за один сдвиг мира
- Один раз с правильной позицией игрока, второй — с позицией WorldRoot

### Причина
1. **Два экземпляра FloatingOriginMP в сцене**:
   - `Ship_1` — mode: ServerAuthority
   - `Player` — mode: Local

2. **Два NetworkPlayer с одинаковым OwnerClientId=0**:
   - Собственный игрок на хосте
   - Ghost/clone (NetworkObject без владельца)
   - Оба подписаны на `OnWorldShifted`

3. **RPC Loop**: ServerAuthority отправлял `BroadcastWorldShiftRpc`, который приходил обратно на сервер

### Решения

**1. Синглтон в FloatingOriginMP:**
```csharp
void Awake()
{
    if (_instance != null && _instance != this)
    {
        Destroy(this); // Уничтожаем дубликат
        return;
    }
    _instance = this;
}
```

**2. Защита от RPC Loop:**
```csharp
[ClientRpc]
private void BroadcastWorldShiftRpc(...)
{
    if (mode == OriginMode.ServerAuthority)
    {
        return; // Игнорируем свой же RPC
    }
    // ...
}
```

**3. Защита в NetworkPlayer.OnWorldShifted:**
```csharp
private void OnWorldShifted(Vector3 offset)
{
    // Только владелец
    if (!IsOwner) return;
    
    // Пропускаем если позиция огромная (WorldRoot)
    if (transform.position.magnitude > 500000) return;
    
    // Сбрасываем коррекцию...
}
```

### Результат
- ✅ `OnWorldShifted` теперь вызывается ровно 1 раз за сдвиг
- ✅ Ghost NetworkPlayer игнорируется

---

## Проблема 2: Игра продолжается при остановке

### Симптомы
- При нажатии Stop в Unity Play mode, консоль продолжает показывать логи
- `LateUpdate` продолжает вызываться
- `GetWorldPosition` вызывается бесконечно

### Причина (предполагаемая)
1. FloatingOriginMP или другой компонент застрял в каком-то цикле
2. Может быть связано с `OnWorldShifted` подпиской
3. Может быть зацикливание в позиции игрока

### Наблюдаемые логи
```
[FloatingOriginMP] GetWorldPosition: positionSource=(-752328, 1, -244450), totalOffset=(-680000, 0, -220000), truePos=(-72328, 1, -24450)
[FloatingOriginMP] Debug: cameraWorldPos=(-72328, 1, -24450), _totalOffset=(-680000, 0, -220000), dist=76349, threshold=100000
```

### Требуется анализ
- [ ] Почему `GetWorldPosition` вызывается после остановки?
- [ ] Есть ли бесконечный цикл в позиции игрока?
- [ ] Правильно ли отписывается от `OnWorldShifted` при остановке?

---

## Архитектура FloatingOriginMP

### Режимы работы
| Mode | Описание | Использование |
|------|----------|---------------|
| Local | Локальный сдвиг | Singleplayer |
| ServerSynced | Ждёт сдвиг от сервера | Multiplayer client |
| ServerAuthority | Сервер рассылает сдвиг | Multiplayer host/server |

### Как работает сдвиг
1. `LateUpdate()` проверяет расстояние от origin
2. Если > threshold — вычисляет offset (округлённый)
3. `ApplyShiftToAllRoots()` сдвигает WorldRoot, World, Mountains, etc.
4. TradeZones **НЕ сдвигается** (восстанавливается после)
5. `OnWorldShifted?.Invoke()` — уведомляет подписчиков
6. Если ServerAuthority — `BroadcastWorldShiftRpc()` рассылает клиентам

### Подписчики OnWorldShifted
- `NetworkPlayer.OnWorldShifted()` — сбрасывает клиентскую коррекцию
- Другие компоненты должны подписываться для синхронизации

---

## Проблема 3: Артефакты при перемещении после сдвига мира

### Симптомы
- После сдвига мира игрок продолжает двигаться с артефактами
- Может быть "рывок" или неправильное положение

### Причина (выявлена subagent)
**Конфликт между FloatingOriginMP и NetworkTransform:**
1. FloatingOriginMP сдвигает WorldRoot
2. NetworkTransform фиксирует огромную дельту позиции
3. Сервер отправляет `ApplyServerPositionRpc`
4. Lerp запускает коррекцию, но offset мира уже применён → **артефакты**

### Решение: Cooldown на коррекцию

```csharp
// В NetworkPlayer.cs
private float _worldShiftCooldown = 0f;
private const float WORLD_SHIFT_COOLDOWN_DURATION = 1f;

private void OnWorldShifted(Vector3 offset)
{
    // ...
    _worldShiftCooldown = WORLD_SHIFT_COOLDOWN_DURATION;
}

private void FixedUpdate()
{
    if (_worldShiftCooldown > 0)
    {
        _worldShiftCooldown -= Time.fixedDeltaTime;
        return; // Игнорируем коррекцию
    }
    // ...
}

[Rpc(SendTo.Owner)]
public void ApplyServerPositionRpc(Vector3 serverPosition, ...)
{
    if (_worldShiftCooldown > 0) return; // Игнорируем
    // ...
}
```

### Результат
- ✅ После сдвига мира — 1 секунда cooldown
- ✅ Серверная коррекция игнорируется во время cooldown
- ✅ Артефакты должны исчезнуть

---

## Следующие шаги

1. [x] Исследовать почему игра не останавливается (нормально для Editor)
2. [x] Найти причину артефактов (конфликт с NetworkTransform)
3. [x] Добавить cooldown на коррекцию
4. [ ] Протестировать с реальным игроком (не на хосте)
5. [ ] Удалить дубликат FloatingOriginMP из сцены (Ship_1)
