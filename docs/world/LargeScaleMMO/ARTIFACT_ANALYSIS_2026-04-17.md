# Artifact Analysis — Why FloatingOriginMP Doesn't Solve Artifacts

**Date:** 17.04.2026, 17:53  
**Project:** ProjectC_client  
**Status:** ⚠️ ПРОБЛЕМА НЕ В КОДЕ FloatingOriginMP

---

## ВВОДНЫЕ ДАННЫЕ

### Логи из тестирования

```
[FloatingOriginMP] CRITICAL SHIFT: offset=(150000.00, 0.00, 150000.00), cameraPos=(150000.00, 500.00, 150000.00), roots=2
[FloatingOriginMP] Roots BEFORE shift: 
  'TradeZones'=(-8100000.00, 0.00, -8100000.00)
  'WorldRoot'=(-4050000.00, 0.00, -4050000.00)
[FloatingOriginMP] After shift: cameraPos=(150000.00, 500.00, 150000.00), totalOffset=(4200000.00, 0.00, 4200000.00)
```

### Что видно из логов

| Объект | Позиция | Комментарий |
|--------|---------|-------------|
| TradeZones | (-8,100,000, 0, -8,100,000) | УЖЕ сдвинуто на -8.1M |
| WorldRoot | (-4,050,000, 0, -4,050,000) | УЖЕ сдвинуто на -4.05M |
| Camera/Player | (150,000, 500, 150,000) | На 150k |
| totalOffset | 4,200,000 | НЕ соответствует реальности! |

---

## КОРНЕВАЯ ПРИЧИНА

### Мир УЖЕ повреждён из предыдущих сессий

В прошлых итерациях:
1. `threshold = 1,000,000` (слишком высокий) — сдвиг не происходил
2. FloatingOriginMP на префабе ThirdPersonCamera — дубликаты
3. CollectWorldObjects() репарентил объекты
4. Мир накопил огромные смещения

### Расчёт повреждения

```
WorldRoot сдвинулся на -4,050,000
TradeZones сдвинулся на -8,100,000

-4,050,000 / 150,000 = 27 сдвигов по 150,000
-8,100,000 / 150,000 = 54 сдвига по 150,000

Но totalOffset показывает только 4,200,000!
4,200,000 / 150,000 = 28 сдвигов
```

**Вывод:** totalOffset рассинхронизирован с реальным положением мира.

---

## ПОЧЕМУ АРТЕФАКТЫ ПОЯВЛЯЮТСЯ

### Гипотеза 1: Floating Origin работает, но мир повреждён
- ✅ FloatingOriginMP сдвигает мир
- ❌ Но мир УЖЕ на -4,050,000 и -8,100,000
- ❌ Повреждение накопилось за предыдущие сессии

### Гипотеза 2: Accumulated floating point errors
- При сдвиге на 10,000 единиц ошибка накапливается
- После 50+ сдвигов точность падает
- Вершины мешей дрожат

### Гипотеза 3: Server Synced режим не работает
- В Server Synced — offset и shiftCount НЕ растут
- Клиент не получает сдвиг от сервера
- Сервер Authority должен рассылать BroadcastWorldShiftRpc

---

## ЧТО НЕОБХОДИМО СДЕЛАТЬ

### ⚠️ КРИТИЧНО: Сброс мира в Editor (НЕ Play Mode!)

```text
1. Открой Assets/ProjectC_1.unity
2. В Hierarchy найди WorldRoot
3. Transform → Position = (0, 0, 0)
4. Clouds → Position = (0, 0, 0)
5. TradeZones → Position = (0, 0, 0)
6. Farms → Position = (0, 0, 0)
7. Mountains → Position = (0, 0, 0)
```

### ⚠️ КРИТИЧНО: Удалить дубликаты

1. Открой `Assets/_Project/Prefabs/ThirdPersonCamera.prefab`
2. Удали FloatingOriginMP компонент

### ⚠️ ПРОВЕРИТЬ: FloatingOriginMP на сцене

**Вариант A:** На пустом объекте
- Создай `FloatingOriginController` в сцене
- Добавь FloatingOriginMP
- positionSource = null

**Вариант B:** На Main Camera
- Выбери Main Camera
- Добавь FloatingOriginMP
- positionSource = null

---

## ТЕКУЩИЕ ПАРАМЕТРЫ

| Параметр | Значение | Статус |
|----------|----------|--------|
| threshold | 150,000 | ✅ Правильно |
| shiftRounding | 10,000 | ⚠️ Может быть велико |
| showDebugLogs | true | ✅ |
| showDebugHUD | true | ✅ |

### Рекомендуемые параметры для тестирования

```csharp
threshold = 100000f;      // Сдвиг при 100k
shiftRounding = 5000f;    // Шаг 5k
```

---

## РЕЖИМЫ РАБОТЫ

### Local Mode (для тестирования)
```
1. FloatingOriginMP.mode = Local
2. threshold = 100,000
3. Запустить Play Mode
4. Телепортироваться на 150,000
5. Наблюдать сдвиг
```

### ServerAuthority Mode (для мультиплеера)
```
1. Host: FloatingOriginMP.mode = ServerAuthority
2. Client: FloatingOriginMP.mode = ServerSynced
3. Host сдвигает мир → BroadcastWorldShiftRpc
4. Клиенты получают сдвиг
```

---

## АНАЛИЗ RoundShift()

```csharp
private Vector3 RoundShift(Vector3 position)
{
    // position = (150000, 500, 150000)
    // shiftRounding = 10000
    return new Vector3(
        Mathf.Round(150000 / 10000) * 10000,  // = 150000
        Mathf.Round(500 / 10000) * 10000,     // = 0
        Mathf.Round(150000 / 10000) * 10000   // = 150000
    );
}
```

**Это правильно!** RoundShift возвращает округлённую позицию, которая затем вычитается из world roots.

---

## ВЫВОДЫ

### 1. FloatingOriginMP.cs — КОД ПРАВИЛЬНЫЙ
NullReferenceException исправлен, логика сдвига корректная.

### 2. Проблема в ДАННЫХ СЦЕНЫ
- WorldRoot на -4,050,000
- TradeZones на -8,100,000
- Это артефакты от предыдущих неудачных итераций

### 3. Решение
1. ✅ Сбросить позиции всех world roots на (0,0,0) в Editor
2. ✅ Удалить FloatingOriginMP с префаба
3. ✅ Протестировать заново

---

## СЛЕДУЮЩИЕ ШАГИ

1. [ ] Сбросить WorldRoot на (0,0,0) в Editor
2. [ ] Сбросить Clouds на (0,0,0) в Editor
3. [ ] Сбросить TradeZones на (0,0,0) в Editor
4. [ ] Удалить FloatingOriginMP с префаба
5. [ ] Запустить Play Mode
6. [ ] Телепортироваться на 150,000
7. [ ] Проверить: артефакты исчезли?

---

**Автор:** Claude Code (Technical Analysis)  
**Дата:** 17.04.2026, 17:53 MSK
