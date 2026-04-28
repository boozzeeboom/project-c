# Iteration 2: Test Checklist

**Дата:** 18.04.2026  
**Статус:** ГОТОВ К ТЕСТИРОВАНИЮ

---

## ✅ Изменения в коде

### WorldStreamingManager.cs
- Добавлен метод `SubscribeToChunkLoaderEvents()` — подписка на события ChunkLoader
- Добавлен обработчик `OnChunkLoadedHandler(ChunkId)` — логирует загрузку чанка
- Добавлен обработчик `OnChunkUnloadedHandler(ChunkId)` — логирует выгрузку чанка
- Добавлен метод `UnsubscribeFromChunkLoaderEvents()` — отписка в OnDestroy()
- Обновление пиковой статистики загруженных чанков

---

## 🧪 Критерий приёмки

> WorldStreamingManager получает события OnChunkLoaded/OnChunkUnloaded.
> Console показывает "Chunk loaded: X,Y" при загрузке.

---

## 📋 Тестовые шаги

### Тест 1: Базовый стриминг чанков
- [ ] Запустить Play Mode
- [ ] Нажать F7 → загрузка чанков вокруг позиции
- [ ] Проверить Console: `[WorldStreamingManager] Chunk loaded: X,Y`

### Тест 2: Выгрузка чанков
- [ ] Подождать пока чанки выгрузятся (или нажать F8)
- [ ] Проверить Console: `[WorldStreamingManager] Chunk unloaded: X,Y`

### Тест 3: Подписка на события
- [ ] Проверить Console при старте: `[WorldStreamingManager] Subscribed to ChunkLoader events`

### Тест 4: HUD информация
- [ ] Включить showDebugHUD в инспекторе
- [ ] Проверить отображение PeakLoadedChunks

---

## 📊 Ожидаемые результаты

| Метрика | До | После |
|---------|-----|-------|
| WorldStreamingManager events | None | Subscribed |
| Console logs | Нет логов "Chunk loaded" | "Chunk loaded: X,Y" |
| Console logs unload | Нет логов "Chunk unloaded" | "Chunk unloaded: X,Y" |
| Preload система | Не работает | Работает (события нужны) |

---

## 🔍 Проверочные вопросы

1. ✅ ChunkLoader вызывает OnChunkLoaded после загрузки?
2. ✅ ChunkLoader вызывает OnChunkUnloaded после выгрузки?
3. ✅ WorldStreamingManager подписан на эти события?
4. ✅ Логи появляются в Console?

---

## 📁 Связанные файлы

- `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs` — изменён
- `Assets/_Project/Scripts/World/Streaming/ChunkLoader.cs` — без изменений (события уже были)

---

**Автор:** Claude Code  
**Дата:** 18.04.2026
