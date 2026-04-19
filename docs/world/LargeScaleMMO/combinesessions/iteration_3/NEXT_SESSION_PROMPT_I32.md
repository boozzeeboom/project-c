# Iteration 3.2 — Session Start Prompt

## Цель Сессии

**Исправить Chunk Oscillation** — позиция игрока oscills между двумя объектами при стоянии на месте. Итерация 3.1 не сработала.

---

## Известные Факты

### Iteration 3.1 Результаты

Исправление с кэшированием позиции в FloatingOriginMP **НЕ решило проблему**. 

**Причина:** В `PlayerChunkTracker` всё ещё работает **старая корутина** которая использует `transform.position` напрямую.

### Наблюдения из логов

1. **Oscillation продолжается**:
```
[PlayerChunkTracker] Player 0 moved from Chunk(8, 6) to Chunk(10, 0)
[PlayerChunkTracker] Player 0 moved from Chunk(10, 0) to Chunk(8, 6)
[PlayerChunkTracker] Player 0 moved from Chunk(8, 6) to Chunk(10, 0)
```
- Chunk(10, 0) и Chunk(8, 6) НЕ соседние — между ними ~6000 units
- Позиция oscills между двумя далёкими точками

2. **FloatingOriginMP позиция стабильна**:
```
[FloatingOriginMP] Debug: mode=ServerAuthority, cameraWorldPos=(20789, -339, 300), dist=20794
```
`cameraWorldPos` НЕ меняется между кадрами.

3. **ДВА источника обновления**:
   - `NetworkPlayer.FixedUpdate()` → `ForceUpdatePlayerChunk()` → использует `GetWorldPosition()` (кэш)
   - `PlayerChunkTracker.Update()` (корутина) → `UpdatePlayerChunk()` → использует `np.transform.position` (OSCILLATES!)

---

## Архитектурная Проблема

```
PlayerChunkTracker
    │
    ├── Update() [COROUTINE] --> UpdatePlayerChunk() --> np.transform.position (OSCILLATES!)
    │       ↑
    │       └─ FindObjectsByType<NetworkPlayer>() (unreliable!)
    │
    └── ForceUpdatePlayerChunk() --> UpdatePlayerChunk() --> worldPosition (STABLE)
            ↑
            └─ NetworkPlayer.FixedUpdate() --> GetWorldPosition() (uses cache)
```

**ДВА параллельных источника позиции** — корневая причина oscillation.

---

## Файлы для Анализа

### Обязательно прочитать:

1. **`Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs`**
   - Строки 100-150: Корутина `UpdatePlayerChunksCoroutine()`
   - Строки 200-280: Все вызовы `UpdatePlayerChunk()`
   - Строки 340-380: `ForceUpdatePlayerChunk()` с hysteresis

2. **`docs/world/LargeScaleMMO/combinesessions/iteration_3/SESSION_END_I31_v2.md`**
   - Полный анализ проблемы

---

## План Анализа (для сабагентов)

### Subagent 1: Gameplay Programmer

**Задача:** Проанализировать корутину и точки входа

```
Прочитай PlayerChunkTracker.cs и ответь:
1. Где запускается корутина UpdatePlayerChunksCoroutine()? (Start() или Awake())
2. Как отключить корутину? (StopAllCoroutines, флаг, etc)
3. Какие методы вызывают UpdatePlayerChunk() напрямую?
4. Какие из них используют hysteresis?
5. Предложи具体的 решение для устранения дублирования
```

### Subagent 2: Unity Specialist

**Задача:** Предложить архитектурное решение

```
Проанализируй архитектуру:
1. Почему есть два источника обновления? (FixedUpdate + корутина)
2. Как согласовать их? (единый источник позиции)
3. Нужна ли корутина вообще?
4. Предложи архитектурное решение
```

---

## Ожидаемый Результат

1. **Точная локализация** — в каком методе oscillation возникает
2. **Решение** — отключить корутину ИЛИ заставить её использовать кэш
3. **План изменений** —具体的 код для修改
4. **Тестирование** — как убедиться что oscillation прекратилась

---

## Метрики Успеха

После исправления, при стоянии на месте 10 секунд:
- ✅ Ноль логов "Player X moved from Chunk"
- ✅ Chunk loading/unloading вызывается только при РЕАЛЬНОМ движении

---

## Ключевой Вопрос

**Почему корутина всё ещё работает после Iteration 3?**

Возможные ответы:
1. Корутина запускается в `Start()` — нужно отключить
2. Корутина использует `FindObjectsByType` — нужно переписать
3. `ForceUpdatePlayerChunk()` не вызывается — нужно проверить

**Ответ на этот вопрос = решение проблемы.**
