# FloatingOriginMP — Progress Report — 17.04.2026 (UPDATED)

**Дата:** 17.04.2026, 19:36  
**Проект:** ProjectC_client  
**Status:** 🔄 В РАБОТЕ

---

## ✅ ДОСТИГНУТО (19:36)

### 1. СДВИГ РАБОТАЕТ! ✅

```
CRITICAL SHIFT: offset=(-250000.00, 0.00, 650000.00)
Roots BEFORE shift: 'TradeZones'=(36920000.00, ...), 'WorldRoot'=(18460000.00, ...)
```

Сдвиг происходит! TradeZones и WorldRoot уже сдвинуты на 18-37 миллионов!

### 2. TradeZones ИСКЛЮЧЁН из сдвига

**Было:** TradeZones в worldRootNames → сдвигался неправильно  
**Стало:** TradeZones исключён

```csharp
worldRootNames = new string[]
{
    "WorldRoot",         // Основной контейнер (СДВИГАЕТСЯ)
    "Mountains",
    "Clouds",
    "farms",
    "World",
    // TradeZones ИСКЛЮЧЁН — там камера!
}
```

### 3. Приоритет источников: NetworkPlayer ПЕРВЫЙ

```csharp
// 1. NetworkPlayer — ПРИОРИТЕТ!
foreach (var netObj in networkPlayers)
{
    if (netObj.name.Contains("NetworkPlayer") && netObj.IsOwner)
    {
        return netObj.transform.position;  // (-253098, 1, 654872) ✓
    }
}
```

---

## ⚠️ ПРОБЛЕМА: TradeZones и WorldRoot уже сдвинуты на 18-37 миллионов!

### Лог:
```
Roots BEFORE shift: 
'TradeZones'=(36920000.00, 0.00, -95860000.00)
'WorldRoot'=(18460000.00, 0.00, -47930000.00)
```

**Это было сделано ДО изменения!** Нужно ПЕРЕЗАПУСТИТЬ сцену!

---

## 📋 ТЕКУЩАЯ АРХИТЕКТУРА

### Что сдвигается (worldRootNames)

| Объект | Сдвигается? | Статус |
|--------|-------------|--------|
| WorldRoot | ДА ✓ | Сдвигается |
| Mountains | ДА ✓ | Как child WorldRoot |
| Clouds | ДА ✓ | Как child WorldRoot |
| TradeZones | НЕТ ✓ | Исключён |

### Что НЕ сдвигается

| Объект | Не сдвигается? | Причина |
|--------|----------------|---------|
| TradeZones | ✓ | Исключён |
| Camera (TradeZones) | ✓ | На TradeZones |
| NetworkPlayer | ✓ | На верхнем уровне |
| ThirdPersonCamera | ✓ | На NetworkPlayer |

---

## ⚠️ ОСТАВШИЕСЯ ПРОБЛЕМЫ

### 1. ⚠️ TradeZones и WorldRoot уже сдвинуты на 18-37M

**Решение:** Перезапустить сцену!

### 2. ⚠️ Offset продолжает расти когда игрок стоит

```
playerPos=650000, offset=0 → offset растёт → shift срабатывает медленно
```

**Причина:** Threshold срабатывает, но сдвиг округляется до 10k. Игрок продолжает двигаться.

### 3. ⚠️ Артефакты

Возможно из-за накопления сдвигов TradeZones.

---

## ✅ СЛЕДУЮЩИЕ ШАГИ

1. **ПЕРЕЗАПУСТИТЬ СЦЕНУ** — TradeZones больше не должен сдвигаться
2. Проверить что WorldRoot сдвигается правильно
3. Проверить артефакты

---

**Автор:** Claude Code  
**Дата:** 17.04.2026, 19:36 MSK
