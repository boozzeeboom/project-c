# Next Session Prompt: Sprint 2 — Network Spawn Validation

**Дата:** 17 апреля 2026 г.  
**Проект:** ProjectC_client  
**Статус:** ✅ КОД ИСПРАВЛЕН — требуется тестирование  

---

## ✅ ЧТО СДЕЛАНО В ПРЕДЫДУЩЕЙ СЕССИИ

### FloatingOriginMP NullReferenceException — ИСПРАВЛЕНО

**Проблема:** `_camera == null` вызывал NullReferenceException в ResetOrigin()

**Решение:** Добавлен метод `GetWorldPosition()` с 4 уровнями fallback:
1. `positionSource` (явный Transform)
2. `_camera` (камера на объекте)
3. `Camera.main`
4. `NetworkManager.Singleton.LocalClient.PlayerObject`

**Файлы:**
- `FloatingOriginMP.cs` — добавлен positionSource, GetWorldPosition()

**Документы:**
- `SESSION_2026-04-17_FIXED.md` — результаты исправления
- `DEEP_ANALYSIS.md` — анализ цикла проблем
- `NGO_BEST_PRACTICES.md` — best practices для NGO

---

## ⚠️ ЧТО НУЖНО СДЕЛАТЬ В UNITY EDITOR (НЕ Play Mode!)

### 1. Сбросить WorldRoot позиции (КРИТИЧНО!)

⚠️ **Это делается В EDITOR, не в Play Mode!**

1. Открой сцену `Assets/ProjectC_1.unity`
2. В Hierarchy найди `WorldRoot`
3. Inspector → Transform → **Position = (0, 0, 0)**
4. Clouds → **Position = (0, 0, 0)**
5. TradeZones → **Position = (0, 0, 0)**
6. Все остальные world objects → **(0, 0, 0)**

**Почему:** WorldRoot на 90 миллионах — это артефакт от предыдущих неудачных итераций.

### 2. Удалить FloatingOriginMP с префаба

1. Открой `Assets/_Project/Prefabs/ThirdPersonCamera.prefab`
2. Найди FloatingOriginMP компонент
3. Удали его

### 3. Проверить позицию FloatingOriginMP

**Вариант A:** На пустом объекте сцены
- Создай пустой объект `FloatingOriginController`
- Добавь FloatingOriginMP
- Оставь positionSource = null (автопоиск)

**Вариант B:** На Main Camera
- Выбери Main Camera
- Добавь FloatingOriginMP
- positionSource = null

---

## 🧪 ТЕСТИРОВАНИЕ

### Тест 1: Одиночная игра
```
1. Запусти Play Mode
2. Нажми F5 несколько раз (телепортация)
3. Нажми F8 (ResetOrigin)
4. Проверь: НЕ должно быть NullReferenceException!
```

### Тест 2: Чанки
```
1. Нажми F7 (загрузить чанки)
2. Проверь: чанки загружаются
```

### Тест 3: HUD
```
1. Проверь HUD в правом верхнем углу:
   - Pos: — текущая позиция
   - Offset: — суммарный сдвиг
   - Roots: — количество world roots
```

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

### Day 3-4: Server-Authoritative Validation

**Цель:** Клиент НЕ может загрузить чанк без команды сервера

**Задачи:**
1. Запустить как Host
2. Запустить Client
3. Client пытается вызвать загрузку напрямую
4. Убедиться что команда не проходит

### Day 5: QA Testing

**Тесты:**
- T1: Host + Client в разных чанках
- T2: Переход между чанками (Preload)
- T3: Сундуки спавнятся с чанком

---

## Документы для изучения

| Документ | Описание |
|----------|----------|
| `SESSION_2026-04-17_FIXED.md` | Результаты исправления FloatingOriginMP |
| `DEEP_ANALYSIS.md` | Анализ почему решения не работали |
| `NGO_BEST_PRACTICES.md` | Best practices для Unity NGO |
| `PHASE2_COMPONENT_STATUS.md` | Статус всех Phase 2 компонентов |

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

- [x] FloatingOriginMP NullReferenceException исправлен
- [ ] WorldRoot позиция сброшена на (0,0,0) в Editor
- [ ] FloatingOriginMP удалён с префаба
- [ ] ChunkNetworkSpawner интегрирован в сцену
- [ ] Сундуки спавнятся/деспавнятся с чанками
- [ ] T1-T3 тесты выполнены
- [ ] Git commit и push сделаны

---

**Следующий спринт:** Sprint 3 — Preload + Polish
