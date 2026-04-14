# Инструкция по тестированию World Streaming системы

**Проект:** ProjectC_client  
**Дата:** 14 апреля 2026  
**Unity версия:** Unity 6 (6000.x LTS), URP

---

## 🎯 Быстрый старт

### Шаг 1: Откройте сцену
```
Assets → ProjectC_1.unity
```

### Шаг 2: Добавьте компоненты на сцену

1. В Hierarchy создайте пустой объект: **Right Click → Create Empty**
2. Переименуйте в `WorldStreamingManager`
3. Добавьте компоненты (Add Component):
   - `ProjectC.World.WorldStreamingManager`
   - `ProjectC.World.Streaming.WorldChunkManager`
   - `ProjectC.World.Streaming.ProceduralChunkGenerator`
   - `ProjectC.World.Streaming.ChunkLoader`
   - `ProjectC.World.Streaming.FloatingOriginMP` (опционально)

### Шаг 3: Настройте ссылки

В Inspector компонента `WorldStreamingManager`:
1. Assign **WorldData** (найдите в Assets/_Project/Data/ или создайте новый)
2. Остальные поля оставьте пустыми — они найдутся автоматически (auto-find)

### Шаг 4: Запустите Play Mode

Нажмите **Play** (или Ctrl+P)

---

## 🧪 Тестирование функций

### Тест 1: Чанк Grid визуализация

**Цель:** Проверить отображение сетки чанков в Scene View

1. В Play Mode нажмите **G** (или меню: `Tools → Project C → World → Toggle Chunk Grid`)
2. В Scene View должны появиться wireframe кубы (серые/зелёные/жёлтые)
3. Цвета обозначают:
   - 🟢 Зелёный = Loaded (загружен)
   - 🟡 Жёлтый = Loading (загружается)
   - ⬜ Серый = Unloaded (не загружен)
   - 🔴 Красный = Unloading (выгружается)

### Тест 2: Телепортация между точками

**Цель:** Проверить работу FloatingOrigin и генерацию чанков

1. Play Mode → нажмите **W** несколько раз
2. Камера должна плавно перемещаться к тестовым точкам
3. Наблюдайте:
   - Загрузку чанков вокруг камеры
   - Генерацию гор (появление новых объектов)
   - Сдвиг мира через FloatingOrigin при больших координатах

### Тест 3: Ручная загрузка чанков

**Цель:** Проверить принудительную загрузку

1. Нажмите **Space** для загрузки чанков вокруг текущей позиции
2. В Console должны появиться логи:
   ```
   [WorldStreamingManager] Loading chunk Chunk(0, 0)
   [ProceduralChunkGenerator] Начало генерации Chunk(0, 0)
   ```

### Тест 4: Сброс FloatingOrigin

**Цель:** Проверить работу сдвига мира

1. Телепортируйтесь к удалённой точке (нажмите W несколько раз)
2. Нажмите **T** для сброса FloatingOrigin
3. В Console проверьте:
   ```
   [FloatingOriginMP] Shifted world by offset=...
   ```

### Тест 5: Debug HUD

**Цель:** Отслеживать состояние системы

1. Нажмите **H** для включения Debug HUD
2. На экране появится панель с информацией:
   - Loaded Chunks: количество
   - Center Chunk: координаты
   - Total Offset: сдвиг мира

---

## 🔧 Ручная настройка компонентов

### WorldChunkManager

**Inspector настройки:**
- **World Data** — ScriptableObject с данными мира
- Все остальное auto-find

**Public методы:**
```csharp
// Получить чанк по позиции
ChunkId chunkId = worldChunkManager.GetChunkAtPosition(playerPosition);

// Получить чанки в радиусе
List<ChunkId> chunks = worldChunkManager.GetChunksInRadius(playerPosition, 2);

// Получить все чанки
var all = worldChunkManager.GetAllChunks();

// Общее количество
int total = worldChunkManager.TotalChunkCount;
```

### ProceduralChunkGenerator

**Inspector настройки:**
- **Farm Prefab** — префаб фермы (опционально)
- **Farm Placeholder Material** — материал для placeholder
- **Mountain Material** — материал для гор
- **LOD Level** — качество (0=высокое, 2=низкое)

### ChunkLoader

**Inspector настройки:**
- **Chunk Manager** — ссылка на WorldChunkManager
- **Chunk Generator** — ссылка на ProceduralChunkGenerator
- **Chunks Parent Transform** — контейнер для чанков (создаётся автоматически)
- **Global Seed** — seed для детерминированной генерации
- **Fade Duration** — время fade-in/out (0.5-3 секунды)

**Events:**
- `OnChunkLoaded(ChunkId)` — подписаться на загрузку
- `OnChunkUnloaded(ChunkId)` — подписаться на выгрузку

### FloatingOriginMP

**Inspector настройки:**
- **Threshold** — расстояние сдвига (по умолчанию 100,000)
- **Shift Rounding** — округление (по умолчанию 10,000)
- **World Root Names** — имена объектов для поиска
- **Show Debug Logs** — логи в Console
- **Show Debug HUD** — HUD на экране

---

## 📊 Ожидаемые результаты

### В Console должны быть логи:

```
[WorldStreamingManager] Initializing streaming system...
[WorldStreamingManager] WorldData: OK
[WorldStreamingManager] ChunkManager: OK (25 chunks)
[WorldStreamingManager] ChunkGenerator: OK
[WorldStreamingManager] ChunkLoader: OK
[WorldStreamingManager] FloatingOrigin: OK
[WorldStreamingManager] Streaming system initialized.
```

### При телепортации (W):

```
[StreamingTest] Moving to test position 1: (500, 300)
[ProceduralChunkGenerator] Начало генерации Chunk(X, Z)
[ProceduralChunkGenerator] Chunk(X, Z) — генерация 2 гор
[ProceduralChunkGenerator] Создана гора everest_001 (height=600, radius=333, seed=42)
[ProceduralChunkGenerator] Chunk(X, Z) — генерация облаков
[ProceduralChunkGenerator] Chunk(X, Z) — генерация облаков завершена
[ChunkLoader] Чанк Chunk(X, Z) полностью загружен
```

### При сбросе FloatingOrigin (T):

```
[FloatingOriginMP] Before ResetOrigin: cameraPos=50000, totalOffset=0, offset=50000
[FloatingOriginMP] After ResetOrigin: newCameraPos=0, distFromOrigin=0
[FloatingOriginMP] Shifted world by offset=(50000, 0, 0)
```

---

## ❓ Устранение проблем

### Проблема: Чанки не загружаются

**Проверьте:**
1. WorldData назначен в WorldChunkManager?
2. ProceduralChunkGenerator назначен в ChunkLoader?
3. Есть ли пики в WorldData?

### Проблема: Горы не генерируются

**Проверьте:**
1. MountainMeshGenerator существует?
2. MountainProfile.CreatePreset() работает?
3. Material назначен?

### Проблема: FloatingOrigin не сдвигает мир

**Проверьте:**
1. На сцене есть объекты с именами: Mountains, Clouds, Farms, TradeZones, World, WorldRoot?
2. Threshold не слишком большой?

### Проблема: Chunk Grid не отображается в Scene View

**Проверьте:**
1. Play Mode запущен?
2. G нажата?
3. Gizmos включены в Scene View?

---

## 🎮 Управление в Play Mode (F5-F10)

**Важно:** Используем F-клавиши чтобы НЕ конфликтовать с основным управлением игры (WASD, Space и т.д.)

| Клавиша | Действие |
|---------|----------|
| **F5** | Телепортация к следующей точке |
| **F6** | Телепортация к предыдущей точке |
| **F7** | Загрузить чанки вокруг позиции |
| **F8** | Сбросить FloatingOrigin |
| **F9** | Toggle Chunk Grid визуализация |
| **F10** | Toggle Debug HUD |
| **Escape** | Выход из Play Mode |

---

## 📁 Где найти компоненты

```
Assets/_Project/Scripts/World/Streaming/
├── WorldChunkManager.cs          — Реестр чанков
├── ProceduralChunkGenerator.cs   — Генерация контента
├── ChunkLoader.cs                — Загрузка/выгрузка
├── FloatingOriginMP.cs           — Сдвиг мира
├── WorldStreamingManager.cs      — Координатор (новый)
└── StreamingTest.cs               — Тестовый компонент (новый)

Assets/_Project/Scripts/Editor/
└── WorldEditorTools.cs           — Scene Navigator + Chunk Visualizer

Assets/_Project/Data/
└── (ваш WorldData ScriptableObject)
```

---

## 📝 Следующие шаги после тестирования

Если всё работает ✅:
1. Зафиксируйте изменения: `git add . && git commit -m "World Streaming Phase 1 - Foundation"`
2. Переходите к **Фазе 2: Multiplayer Integration**

Если есть проблемы ❌:
1. Проверьте логи в Console
2. Убедитесь что WorldData содержит данные
3. Проверьте связи между компонентами