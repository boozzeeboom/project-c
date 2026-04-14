# Large-Scale MMO World Streaming — Index

**Проект:** ProjectC_client  
**Unity версия:** Unity 6 (6000.x LTS), URP  
**Дата создания:** 14 апреля 2026

---

## 📁 Структура каталога

```
docs/world/LargeScaleMMO/
├── README.md                                           ← Этот файл (навигация)
├── 01_Architecture_Plan.md                              ← Архитектура и план реализации
├── 02_Technical_Research.md                            ← Техническое исследование
├── ADR-0002_WorldStreaming_Architecture.md            ← Architecture Decision Record
├── SESSION_PROMPT_Phase1_Foundation.md                 ← Промт Фазы 1
├── SESSION_PROMPT_Phase1_Foundation_STATUS.md         ← Статус Фазы 1
├── SESSION_PROMPT_Phase2_MultiplayerIntegration.md    ← Промт Фазы 2
├── SESSION_2026-04-14.md                              ← Summary сессии 14.04.2026
└── TESTING_INSTRUCTIONS.md                             ← Инструкции по тестированию
```

---

## 📄 Документы

### 01_Architecture_Plan.md
**Полный путь:** [01_Architecture_Plan.md](./01_Architecture_Plan.md)

**Содержание:**
- Резюме проблемы и текущее состояние проекта
- Сравнительный анализ трёх решений (World Streamer 2, Custom Streaming, Separate Scenes)
- **Рекомендуемая архитектура:** Кастомная система стриминга
- Детальное описание компонентов:
  - WorldChunkManager (Server-Authoritative)
  - ChunkLoader (Client-Side)
  - ProceduralChunkGenerator
  - FloatingOriginMP (Multiplayer-Synced)
- Интеграция с NGO (Netcode for GameObjects)
- Риски и стратегии смягчения
- **4 фазы реализации** с детальными задачами и критериями приёмки
- Editor Navigation Solutions
- Технические ограничения и Best Practices
- Итоговые рекомендации

**Ключевой вывод:** Выбрана кастомная система стриминга (Вариант B) как единственная, полностью решающая требования MMO проекта.

---

### 02_Technical_Research.md
**Полный путь:** [02_Technical_Research.md](./02_Technical_Research.md)

**Содержание:**
- Floating Point Precision Problem (официальная позиция Unity, технические ограничения)
- Unity 6 Scene Management (Additive Loading, NGO Synchronization, Server Validation)
- Addressables for Dynamic Content (CDN, Best Practices)
- ECS SubScene & Scene Sections (готовность к продакшену, ограничения для NGO проектов)
- World Partition — есть ли аналог в Unity?
- Dedicated Server Build
- **Детальное сравнение 7 Asset Store решений:**
  - World Streamer 2
  - SECTR Complete
  - BigWorldStreamer
  - MapMagic 2
  - Gaia Pro 2023
  - RTP v3.3
  - Custom (Addressables)
- MMO Architecture: Area-Based Sharding
- Reference: Megacity Metro (Unity Demo)
- Итоговые рекомендации

**Ключевой вывод:** Ни один готовый ассет не решает задачу MMO-стриминга "из коробки". Кастомная система — единственный путь.

---

## 🚀 Следующие шаги

### Приоритет 1 (Начать СРАЗУ)

1. ✅ **Исследование завершено** — этот документ
2. ⬜ Создать `WorldChunkManager` — реестр чанков, grid-based lookup
3. ⬜ Создать `FloatingOriginMP` — мультиплеер-синхронизированный сдвиг
4. ⬜ Создать Editor Tool для навигации в Scene View
5. ⬜ Исправить Floating Origin bug — сдвигать ВСЕ объекты, не только Mountains

### Приоритет 2 (После валидации Приоритета 1)

6. ⬜ Создать `ProceduralChunkGenerator` — генерация гор + облаков per chunk
7. ⬜ Создать `ChunkLoader` — client-side загрузка/выгрузка
8. ⬜ Интегрировать с NGO — NetworkObject spawn/despawn per chunk

### Приоритет 3 (Оптимизация)

9. ⬜ Preloading система — загрузка соседних чанков заранее
10. ⬜ Job System оптимизация — генерация мешей off-main-thread
11. ⬜ Memory budgeting — мониторинг и контроль памяти
12. ⬜ Cyclic world support — если потребуется

---

## 📊 Сводная информация о проекте

### Текущее состояние
| Параметр | Значение |
|----------|----------|
| Мир | Радиус ~350,000 units |
| Горные массивы | 5 (Himalayan, Alpine, African, Andean, Alaskan) |
| Пику | 29 |
| Облака | 890+ процедурных мешей |
| Сцена | Одна (ProjectC_1.unity) |
| Стриминг | Отсутствует |
| Floating Origin | Реализован, но buggy |
| Мультиплеер | NGO (Netcode for GameObjects) |

### Выбранная архитектура
| Компонент | Решение |
|-----------|---------|
| Стриминг мира | Кастомная chunk-based система |
| Загрузка контента | Addressables + procedural generation |
| Floating Point fix | FloatingOriginMP (server-synced) |
| Мультиплеер | Server-authoritative chunk management |
| Сцены | Subscenes для дизайна, runtime streaming кастомный |

---

## 🔗 Связанные документы

- [Prompt: Editor навигация для больших миров](../prompt_editor_navigation_large_world.md)
- [SCALE_ANALYSIS.md](../SCALE_ANALYSIS.md)
- [MASTER_PLAN_WorldPrototype.md](../MASTER_PLAN_WorldPrototype.md)
- [NETWORK_ARCHITECTURE.md](../../NETWORK_ARCHITECTURE.md) (если существует)
- [QWEN.md](../../../QWEN.md) — контекст проекта

---

## 📝 История изменений

| Дата | Изменение | Автор |
|------|-----------|-------|
| 14.04.2026 | Создан каталог и документы | Qwen Code Agent |
| 14.04.2026 | **Фаза 1 завершена** — World Streaming Foundation | Qwen Code Agent |
| | | |
