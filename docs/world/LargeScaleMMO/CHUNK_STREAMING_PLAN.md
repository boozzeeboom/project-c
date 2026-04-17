# Chunk Streaming — Переход от FloatingOriginMP

## Статус

- **FloatingOriginMP:** Работает, но не соответствует архитектуре MMO
- **Chunk Streaming:** Правильный подход для MMO (согласно 01_Architecture_Plan.md)

## План действий

### Фаза 1: Архивация FloatingOriginMP

1. **Отключить компонент** — убрать из сцены (не удалять код)
2. **Заархивировать код** — переместить в `archive/`
3. **Сохранить документацию** — оставить в docs/

### Фаза 2: Реализация Chunk Streaming

Согласно документу `01_Architecture_Plan.md`:

```
Сервер управляет:
├── WorldChunkManager — реестр чанков
├── PlayerChunkTracker — трекинг игроков
└── RPC LoadChunk/UnloadChunk

Клиенты:
├── ChunkLoader — загрузка чанков
├── ProceduralChunkGenerator — генерация контента
└── FloatingOrigin (упрощённый) — для краевых случаев
```

### Компоненты для реализации

| Компонент | Описание | Приоритет |
|-----------|---------|-----------|
| WorldChunkManager | Реестр чанков, grid-based | 1 |
| ChunkLoader | Загрузка/выгрузка чанков | 1 |
| ProceduralChunkGenerator | Генерация гор+облаков из Seed | 1 |
| PlayerChunkTracker | Трекинг позиции игрока | 2 |

## Архивация FloatingOriginMP

Файлы для архивации:
- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`
- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs.meta`

Документы:
- `docs/world/LargeScaleMMO/FLOATING_ORIGIN_*.md`

## Примечание

FloatingOriginMP можно оставить как "fallback" — если игрок каким-то образом окажется >1M, применить сдвиг.
