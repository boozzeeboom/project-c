# Next Session Prompt: Phase 2 Sprint 2 — Network Object Spawn

**Дата:** 16 апреля 2026 г.
**Проект:** ProjectC_client
**Статус предыдущей сессии:** Sprint 1 завершён

---

## Цель сессии

Продолжить Phase 2 — Sprint 2:
1. Завершить интеграцию ChunkNetworkSpawner
2. Протестировать спавн сундуков с чанками
3. Проверить server-authoritative логику

---

## Что реализовано в Sprint 1 ✅

| Задача | Файл | Статус |
|--------|------|--------|
| PlayerChunkTracker | `PlayerChunkTracker.cs` | ✅ Работает |
| LoadChunkRpc/UnloadChunkRpc | `WorldStreamingManager.cs` | ✅ Работает |
| FloatingOriginMP sync | `FloatingOriginMP.cs` | ✅ Работает |
| PrepareTestScene | `PrepareTestScene.cs` | ✅ Работает |

---

## Sprint 2: Network Object Spawn

### Day 1-2: ChunkNetworkSpawner Integration

**Цель:** Существующие сундуки/NPC должны спавниться с чанками

**Задачи:**
1. Проверить что ChunkNetworkSpawner работает в тестовой сцене
2. Назначить chestPrefab в Inspector
3. Протестировать спавн/деспавн

**Файлы:**
- `ChunkNetworkSpawner.cs` — проверка логики
- `TestScene: ProjectC_ChunkTest_1`

---

### Day 3-4: Server-Authoritative Validation

**Цель:** Клиент НЕ может загрузить чанк без команды сервера

**Задачи:**
1. Запустить как Host
2. Запустить Client
3. Client пытается вызвать загрузку напрямую
4. Убедиться что команда не проходит

---

### Day 5: QA Testing

**Тесты:**
- T1: Host + Client в разных чанках
- T2: Переход между чанками (Preload)
- T3: Сундуки спавнятся с чанком

---

## Документы для изучения

| Документ | Описание |
|----------|----------|
| `docs/world/LargeScaleMMO/TESTING_NOTES.md` | Результаты тестирования |
| `docs/world/LargeScaleMMO/TESTING_GUIDE.md` | Инструкции тестирования |

---

## Команды Git

**Перед началом:**
```bash
git pull origin develop
```

**После завершения:**
```bash
git add -A
git commit -m "feat(world-streaming): Sprint 2 complete - network spawn validated"
git push origin develop
```

---

## Критерии завершения Sprint 2

- [ ] ChunkNetworkSpawner интегрирован в сцену
- [ ] Сундуки спавнятся/деспавнятся с чанками
- [ ] T1-T3 тесты выполнены
- [ ] Git commit и push сделаны

---

**Следующий спринт:** Sprint 3 — Preload + Polish
