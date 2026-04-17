# FloatingOriginMP — CRITICAL ANALYSIS — 17.04.2026 (UPDATED)

**Дата:** 17.04.2026, 19:50  
**Проект:** ProjectC_client  
**Status:** ✅ ИСПРАВЛЕНО

---

## ✅ ИСПРАВЛЕНИЯ ВНЕСЕНЫ

### БАГ НАЙДЕН И ИСПРАВЛЕН:

**3 файла добавляли TradeZones обратно:**

| Файл | Проблема | Статус |
|------|----------|--------|
| `FloatingOriginMP.cs` | TradeZones в worldRootNames | ✅ Исправлен |
| `StreamingSetupRuntime.cs` | TradeZones в worldObjectNames | ✅ Исправлен |
| `PrepareMainScene.cs` | TradeZones в _worldRootNames | ✅ Исправлен |

### КОРЕНЬ ПРОБЛЕМЫ:

```
TradeZones добавлялся в worldRootNames в 3-х местах!
→ При запуске TradeZones сдвигался
→ TradeZones накапливал offsets
→ TradeZones исключён, но offsets остались
```

---

## 🚨 ГЛАВНАЯ ПРОБЛЕМА (ДО ИСПРАВЛЕНИЯ)

### TradeZones и WorldRoot УЖЕ сдвинуты на МИЛЛИОНЫ!

```
Roots BEFORE shift: 
'TradeZones'=(25760000.00, 0.00, -46020000.00)
'WorldRoot'=(12880000.00, 0.00, -23010000.00)
```

Это накопилось от предыдущих запусков!

**НУЖНО ПЕРЕЗАПУСТИТЬ СЦЕНУ!**

---

## 📊 АНАЛИЗ ЛОГОВ

### Что работает:
```
NetworkPlayer IsOwner=(-374311, 1, 662343) ← ПРАВИЛЬНАЯ позиция ✓
CRITICAL SHIFT срабатывает ✓
Player tag используется корректно ✓
```

### Что НЕПРАВИЛЬНО:

| Объект | Позиция | Проблема |
|--------|---------|----------|
| TradeZones | 25,760,000 | Сдвинут от предыдущих тестов! |
| WorldRoot | 12,880,000 | Сдвинут от предыдущих тестов! |
| NetworkPlayer | 374,311 | Правильно |
| NetworkPlayer(Clone) | -7, 76 | Рядом с origin (не используется) |

---

## 🔍 КОРЕНЬ ПРОБЛЕМЫ

### 1. TradeZones исключён из worldRootNames

```csharp
worldRootNames = new string[]
{
    "WorldRoot",
    "Mountains",
    "Clouds",
    // TradeZones ИСКЛЮЧЁН
}
```

Но TradeZones УЖЕ был сдвинут в предыдущих тестах! Исключение не отменяет уже накопленные offsets!

### 2. Scene НЕ перезапущена

Пользователь НЕ перезапустил сцену после изменения worldRootNames. TradeZones и WorldRoot сохранили свои offsets.

### 3. Floating Origin работает, но накапливает

```
Игрок двигается → FloatingOriginMP сдвигает мир → 
→ TradeZones сдвигается (хотя исключён?) → 
→ offsets накапливаются
```

---

## ❓ ВОПРОСЫ ДЛЯ subagent АНАЛИЗА

### Q1: Почему TradeZones сдвигается если он исключён?

TradeZones в логах показывает 25,760,000 единиц. Это значит:
- Либо TradeZones был сдвинут ДО исключения
- Либо какой-то другой код сдвигает TradeZones
- Либо TradeZones — это Child другого объекта который сдвигается

### Q2: Почему totalOffset продолжает расти?

```
playerPos=374311, offset=0 → shift → offset=?
```

После сдвига offset должен = 0 (или близко к 0). Но в логах offset = -13,250,000!

Это значит что сдвиги накапливаются неправильно.

### Q3: Как исправить накопление?

Варианты:
1. **Перезапустить сцену** — сбросит все offsets
2. **Сохранять totalOffset в PlayerPrefs** — восстанавливать при загрузке
3. **Изменить логику сдвига** — не накапливать offsets

---

## 📁 ДОКУМЕНТЫ ДЛЯ АНАЛИЗА

1. `SESSION_2026-04-17_PROGRESS.md` — текущий прогресс
2. `SESSION_2026-04-17_ALL_ATTEMPTS.md` — все попытки
3. `SESSION_2026-04-17_SUBAGENT_RESULTS.md` — результаты анализа
4. `FLOATING_ORIGIN_STATUS_2026-04-17.md` — статус
5. `FloatingOriginMP.cs` — текущий код

---

## 🎯 ЗАДАЧИ ДЛЯ subagent

1. **Проанализировать структуру сцены** — почему TradeZones сдвигается?
2. **Проанализировать код** — где накапливается totalOffset?
3. **Предложить решение** — как сбросить offsets правильно?
4. **Предложить архитектуру** — как избежать накопления в будущем?

---

**Автор:** Claude Code  
**Дата:** 17.04.2026, 19:44 MSK
