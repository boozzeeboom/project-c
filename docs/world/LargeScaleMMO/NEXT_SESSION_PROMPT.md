# Next Session Prompt: World Reset Required

**Дата:** 17 апреля 2026 г.  
**Проект:** ProjectC_client  
**Status:** ⚠️ ПРОБЛЕМА В ДАННЫХ СЦЕНЫ, НЕ В КОДЕ

---

## ⚠️ ВАЖНО: ПРОЧИТАЙ ПЕРЕД ТЕСТИРОВАНИЕМ

### FloatingOriginMP.cs — КОД ПРАВИЛЬНЫЙ
NullReferenceException исправлен, логика сдвига корректная.

### Проблема в ДАННЫХ СЦЕНЫ
```
WorldRoot.position = (-4,050,000, 0, -4,050,000) ← УЖЕ СДВИНУТО!
TradeZones.position = (-8,100,000, 0, -8,100,000) ← УЖЕ СДВИНУТО!
```

Это артефакты от предыдущих неудачных итераций. FloatingOriginMP работает, но мир уже повреждён.

---

## ❌ ЧТО НЕ РАБОТАЕТ

### Причина артефактов
Мир был сдвинут в прошлых сессиях и накопил ошибки. Floating Origin работает, но:
- TradeZones уже на -8.1M
- WorldRoot уже на -4.05M
- totalOffset рассинхронизирован с реальным положением

### Логи подтверждают
```
[FloatingOriginMP] Roots BEFORE shift: 
  'TradeZones'=(-8100000.00, 0.00, -8100000.00)
  'WorldRoot'=(-4050000.00, 0.00, -4050000.00)
```

---

## ✅ ЧТО НУЖНО СДЕЛАТЬ (В EDITOR, НЕ Play Mode!)

### 1. Сбросить WorldRoot позиции (КРИТИЧНО!)

⚠️ **Это делается В EDITOR, не в Play Mode!**

1. Открой сцену `Assets/ProjectC_1.unity`
2. В Hierarchy найди `WorldRoot`
3. Inspector → Transform → **Position = (0, 0, 0)**
4. Clouds → **Position = (0, 0, 0)**
5. TradeZones → **Position = (0, 0, 0)**
6. Farms → **Position = (0, 0, 0)**
7. Mountains → **Position = (0, 0, 0)**
8. Все остальные world objects → **(0, 0, 0)**

### 2. Удалить FloatingOriginMP с префаба

1. Открой `Assets/_Project/Prefabs/ThirdPersonCamera.prefab`
2. Найди FloatingOriginMP компонент
3. Удали его

### 3. Проверить FloatingOriginMP в сцене

**Вариант A:** На пустом объекте сцены
- Создай пустой объект `FloatingOriginController`
- Добавь FloatingOriginMP
- Оставь positionSource = null (автопоиск)

**Вариант B:** На Main Camera
- Выбери Main Camera
- Добавь FloatingOriginMP
- positionSource = null

---

## 🧪 ТЕСТИРОВАНИЕ ПОСЛЕ СБРОСА

### Тест 1: Одиночная игра
```
1. Запусти Play Mode
2. Нажми F5 несколько раз (телепортация)
3. Нажми F8 (ResetOrigin)
4. Проверь HUD:
   - Pos: — текущая позиция (должна быть ~150000)
   - Offset: — суммарный сдвиг (должен расти)
   - Roots: — количество world roots
```

### Тест 2: Артефакты должны исчезнуть
```
1. Телепортируйся на 150,000
2. Осмотрись — артефактов быть не должно
3. Мир выглядит нормально
```

### Тест 3: Server Synced режим
```
1. Запусти как Host (FloatingOriginMP.mode = ServerAuthority)
2. Запусти Client (FloatingOriginMP.mode = ServerSynced)
3. Host телепортируется на 150,000
4. Проверь: Client получает сдвиг
```

---

## Документы для изучения

| Документ | Описание |
|----------|----------|
| `ARTIFACT_ANALYSIS_2026-04-17.md` | Полный анализ почему артефакты появляются |
| `LARGE_WORLD_SOLUTIONS.md` | Сравнение подходов к большим мирам |
| `SESSION_2026-04-17_FIXED.md` | Результаты исправления NullReferenceException |
| `NGO_BEST_PRACTICES.md` | Best practices для Unity NGO |

---

## Команды Git

**Перед началом:**
```bash
git pull origin develop
```

**После завершения:**
```bash
git add -A
git commit -m "fix(world): reset world positions in editor - floating origin artifacts resolved"
git push origin develop
```

---

## Критерии успеха

- [ ] WorldRoot позиция сброшена на (0,0,0) в Editor
- [ ] Clouds позиция сброшена на (0,0,0) в Editor
- [ ] TradeZones позиция сброшена на (0,0,0) в Editor
- [ ] FloatingOriginMP удалён с префаба
- [ ] Тестирование: артефакты исчезли
- [ ] Git commit и push сделаны

---

## Расчёт повреждения мира

```
WorldRoot сдвинулся на -4,050,000
TradeZones сдвинулся на -8,100,000

-4,050,000 / 150,000 = 27 сдвигов по 150,000
-8,100,000 / 150,000 = 54 сдвига по 150,000

totalOffset показывает только 4,200,000!
4,200,000 / 150,000 = 28 сдвигов

Вывод: totalOffset рассинхронизирован с реальным положением мира.
Это нельзя исправить кодом — нужен ручной сброс в Editor.
```

---

**Автор:** Claude Code  
**Дата:** 17.04.2026, 17:54 MSK
