# Large Scale MMO: Scene-Based Architecture (Iteration 2)

**Статус:** АРХИТЕКТУРА ПРИНЯТА
**Дата:** 28.04.2026

---

## Описание

Переход от chunk-only системы к двухуровневой сценовой архитектуре:
- **Scene Layer:** 30 сцен × 79,999 × 79,999 с 1,600 overlap
- **Chunk Layer:** существующая система 2,000 × 2,000

**Цель:** Решить проблему float precision (на 100k+ начинаются артефакты) путём разделения мира на загружаемые сцены.

---

## Документы

| Документ | Описание |
|----------|----------|
| [SCENE_ARCHITECTURE_DECISION.md](./SCENE_ARCHITECTURE_DECISION.md) | Архитектурное решение: почему 30×79,999 с overlap |
| [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) | Детальный план реализации по фазам |

---

## Ключевые решения

### 1. NGO Scene Management = Оставить включённым

`CheckObjectVisibility` работает правильно:
- Вызывается при спавне
- Возвращает `false` → объект НЕ синхронизируется клиенту
- Не нужен CustomMessaging для объектов

### 2. Scene = Unity Scene файлы

- 80,000 × 80,000 единиц на сцену
- Additive loading через SceneManager
- Preload триггеры на 10k до границы

### 3. Overlap для seamlessness

- 1,600 единиц (2%) перекрытие между сценами
- Terrain может не совпадать на границах (отложить)
- Clouds/farms не генерировать в overlap зонах

---

## Вердикт

**✅ РЕАЛИЗУЕМО** с учётом следующего:
- Фаза 1-2: Основные компоненты (SceneID, ServerSceneManager, ClientSceneLoader)
- Фаза 3: Тестирование с 2 клиентами
- Фаза 4: Preload система
- Фаза 5: Terrain overlap (ОТЛОЖИТЬ)

---

## Связанные документы

- [1_Iteration/CURRENT_STATE.md](../1_Iteration/CURRENT_STATE.md) - текущее состояние chunk системы
- [1_Iteration/FLOAT_PRECISION_ISSUE.md](../1_Iteration/FLOAT_PRECISION_ISSUE.md) - анализ проблемы precision