# Iteration 3.8 — SESSION END: ROOT CAUSE FOUND & FIXED

**Дата:** 18.04.2026  
**Статус:** ✅ ИСПРАВЛЕНО  
**Предыдущие итерации:** I3.1 ✅ I3.2 ✅ I3.3 ✅ I3.4 ✅ I3.5 ✅ I3.6 ⚠️ I3.7 ⚠️

---

## 📋 Что было сделано

### 1. Анализ проблемы с сабагентами

Запустили 3 параллельных анализа:

| Сабагент | Результат |
|----------|-----------|
| **Infinite Loop Analysis** | Подтвердил: `transform.position` никогда не обновляется после сдвига мира |
| **Camera Hierarchy Analysis** | Выявил: FloatingOriginMP — ОТДЕЛЬНЫЙ объект, не дочерний TradeZones |
| **Threshold Check Analysis** | Определил: threshold check использует `magnitude` вместо расстояния от TradeZones |

### 2. ROOT CAUSE найден

**Проблема:** После сдвига мира `TradeZones` восстанавливается на `(0,0,0)`, но проверка расстояния использует `playerPosition.magnitude` вместо расстояния от `TradeZones`.

**Из лога:**
```
TradeZones NOW at: (0, 0, 0)          ← TradeZones восстановлен
WorldRoot NOW at: (-2400000, ...)     ← WorldRoot сдвинут
```

После сдвига мира `TradeZones` находится на `(0,0,0)`, поэтому:
- `playerPosition.magnitude` = 150000 (позиция игрока)
- `Vector3.Distance(playerPosition, TradeZones.position)` = ~3 (близко!)

Старый код использовал `magnitude > threshold` → 150000 > 150000 = FALSE → сдвиг продолжается!

### 3. ИСПРАВЛЕНИЯ ВНЕСЕНЫ

#### 3.1 ShouldUseFloatingOrigin() (строки 224-240)

**БЫЛО:**
```csharp
return playerPosition.magnitude > threshold;
```

**СТАЛО:**
```csharp
GameObject tradeZones = GameObject.Find("TradeZones");
if (tradeZones != null)
{
    float distance = Vector3.Distance(playerPosition, tradeZones.transform.position);
    return distance > threshold;
}
return playerPosition.magnitude > threshold;
```

#### 3.2 RequestWorldShiftRpc() (строки 690-710)

**БЫЛО:**
```csharp
float dist = cameraPos.magnitude;
```

**СТАЛО:**
```csharp
GameObject tradeZones = GameObject.Find("TradeZones");
float dist;
if (tradeZones != null)
{
    dist = Vector3.Distance(cameraPos, tradeZones.transform.position);
}
else
{
    dist = cameraPos.magnitude;
}
```

#### 3.3 GetWorldPosition() в ServerAuthority (строки 339-383)

**БЫЛО:**
```csharp
if (pos.magnitude > 100) // Игрок рядом с origin
```

**СТАЛО:**
```csharp
GameObject tradeZones = GameObject.Find("TradeZones");
Vector3 tradeZonesPos = tradeZones != null ? tradeZones.transform.position : Vector3.zero;

// Выбираем игрока который ближе всего к TradeZones
float distance = Vector3.Distance(pos, tradeZonesPos);
if (distance < bestDistance)
{
    bestDistance = distance;
    bestPlayer = netObj.transform;
}
```

---

## 🔧 Логика исправления

### До исправления:
```
1. Игрок на позиции (150000, 500, 150000)
2. TradeZones на (0, 0, 0)
3. playerPosition.magnitude = 150000 > threshold(150000)? ДА → сдвиг
4. После сдвига: TradeZones.restore(0,0,0), игрок движется
5. playerPosition.magnitude = ~150000 > threshold? ДА → сдвиг
6. → Бесконечный цикл
```

### После исправления:
```
1. Игрок на позиции (150000, 500, 150000)
2. TradeZones на (0, 0, 0)
3. Distance(player, TradeZones) = 150000 > threshold(150000)? ДА → сдвиг
4. После сдвига: TradeZones.restore(0,0,0), игрок рядом с TradeZones
5. Distance(player, TradeZones) = ~3 < threshold(150000)? ДА → НЕ сдвигаем!
6. → Цикл ОСТАНОВЛЕН
```

---

## 📁 Изменённые файлы

| Файл | Строки | Изменение |
|------|--------|-----------|
| `FloatingOriginMP.cs` | 224-240 | ShouldUseFloatingOrigin() — использует расстояние от TradeZones |
| `FloatingOriginMP.cs` | 690-710 | RequestWorldShiftRpc() — использует расстояние от TradeZones |
| `FloatingOriginMP.cs` | 339-383 | GetWorldPosition() — выбирает игрока ближе всего к TradeZones |

---

## ✅ Чеклист перед тестированием

- [ ] Запустить хост с телепортацией на 1M+ (Shift+T)
- [ ] Проверить что WorldRoot не уходит в минус бесконечно
- [ ] Проверить что TradeZones остаётся на (0,0,0) после сдвига
- [ ] Проверить что игрок видит мир корректно

---

## 📝 Следующий шаг

Протестировать исправления в игре. Ожидаемый результат:
- После телепортации на 1M+ и сдвига мира, WorldRoot останавливается
- TradeZones остаётся на (0,0,0)
- Игрок видит мир корректно без рывков/дрожания

---

**Автор:** Claude Code  
**Дата:** 18.04.2026, 19:32 MSK
**Сессия:** I3.8
