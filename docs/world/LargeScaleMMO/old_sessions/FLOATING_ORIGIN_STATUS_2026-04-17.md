# FloatingOriginMP — Статус и план действий

**Дата:** 17.04.2026, 17:08  
**Проект:** ProjectC_client  

---

## ЧТО МЫ ЗНАЕМ

### Текущие настройки FloatingOriginMP:
| Параметр | Значение |
|----------|----------|
| `threshold` | 1,000,000 (1 миллион) |
| `shiftRounding` | 100,000 |
| `showDebugLogs` | true |
| `showDebugHUD` | true |

### Проблемы выявленные сегодня:

1. **WorldRoot на позиции 90 миллионов** — мир уже был сдвинут в прошлых итерациях
2. **FloatingOriginMP дублируется** — был на префабе ThirdPersonCamera
3. **Компонент требует Camera** — теперь исправлено, использует Camera.main

### Ошибки из консоли:
```
[FloatingOriginMP] Camera not found on this GameObject!  ← ИСПРАВЛЕНО
[ChunkLoader] chunksParentTransform не назначен       ← НЕ КРИТИЧНО
MissingReferenceException: ...PlayerChunkTracker        ← ОТДЕЛЬНЫЙ БАГ
```

---

## ЧТО НУЖНО СДЕЛАТЬ (В EDITOR, НЕ Play Mode)

### 1. Сбросить позиции WorldRoot

**ВНИМАНИЕ: Это делается в Editor, не в Play Mode!**

1. Открой сцену в Unity
2. В Hierarchy найди WorldRoot
3. В Inspector: Position = (0, 0, 0)
4. Clouds → Position = (0, 0, 0)
5. TradeZones → Position = (0, 0, 0)
6. ВСЕ world objects должны быть около (0, 0, 0)

**Почему:** FloatingOriginMP работает сдвигая мир к origin. Если WorldRoot уже на 90 миллионах, сдвиг не работает.

### 2. Проверить что FloatingOriginMP только один

1. Найди ВСЕ объекты с FloatingOriginMP:
   - Используй поиск в Project: Search "FloatingOriginMP"
   - Проверь Assets/_Project/Prefabs/ThirdPersonCamera.prefab
2. **Удали FloatingOriginMP с префаба ThirdPersonCamera**
3. Оставь компонент только на пустом объекте сцены

### 3. Запустить Play Mode и проверить

Ожидаемые логи:
```
[FloatingOriginMP] ============= AWOKE CALLED =============
[FloatingOriginMP] Camera found: Main Camera
[FloatingOriginMP] After FindOrCreateWorldRoots: roots=3
[FloatingOriginMP] Initialized. threshold=1,000,000, roots=3
```

---

## КАК РАБОТАЕТ FloatingOriginMP

```
1. Игрок двигается в позицию (150000, 500, 150000)
2. |camera.x| > threshold (1000000)  ← условие НЕ выполняется!
3. FloatingOrigin НЕ срабатывает
4. Появляются артефакты потому что координаты слишком большие
```

**Проблема:** threshold=1,000,000 означает что сдвиг происходит только когда координата > 1 миллион!

**Решение:** Уменьши threshold до 150,000 (с небольшим запасом)

---

## ПЛАН ИСПРАВЛЕНИЯ

### Изменения в FloatingOriginMP:

1. **threshold = 150000** (而不是 1000000)
2. **shiftRounding = 10000** (而不是 100000)

Это позволит сдвигать мир когда игрок дальше 150,000 единиц.

### После изменения threshold:

1. Сбрось WorldRoot позиции на (0,0,0) в Editor
2. Запусти Play Mode
3. Телепортируйся к точке 150,000 (F5 несколько раз)
4. FloatingOrigin должен сработать:
   - Обнаруживает |camera.x| > 150000
   - Сдвигает WorldRoot на -150000
   - Camera position остаётся на месте (относительно игрока)
   - Артефакты пропадают

---

## ОЖИДАЕМЫЙ РЕЗУЛЬТАТ

```
До сдвига:
- Camera position: (150000, 500, 150000)
- WorldRoot position: (0, 0, 0)
- TotalOffset: (0, 0, 0)

После сдвига:
- Camera position: (150000, 500, 150000)  ← НЕ меняется
- WorldRoot position: (-150000, 0, -150000)  ← сдвигается
- TotalOffset: (150000, 0, 150000)
- Артефакты пропадают
```

---

## СЛЕДУЮЩИЕ ШАГИ

1. [ ] Уменьшить threshold до 150000 в FloatingOriginMP
2. [ ] Сбросить WorldRoot позиции на (0,0,0) в Editor
3. [ ] Запустить Play Mode
4. [ ] Телепортироваться к 150,000
5. [ ] Проверить что артефакты исчезли

---

**Автор:** Claude Code  
**Дата:** 17.04.2026
