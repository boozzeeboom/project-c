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

### Шаг 2: Убедитесь что есть Main Camera

В сцене должна быть камера с тегом **MainCamera**:
1. Выберите камеру в Hierarchy
2. В Inspector проверьте тег (вверху справа от имени объекта)
3. Если тег не **MainCamera** — выберите его из выпадающего списка

### Шаг 3: Добавьте компоненты на сцену

**Вариант A (Автоматически):**
- Запустите Play Mode — компонент `StreamingTest_AutoRun` автоматически добавится на камеру
- Или: `Tools → Project C → World → Add Test Component to Camera`

**Вариант B (Вручную):**
1. В Hierarchy создайте пустой объект: **Right Click → Create Empty**
2. Переименуйте в `WorldStreamingManager`
3. Добавьте компоненты (Add Component):
   - `ProjectC.World.WorldStreamingManager`
   - `ProjectC.World.Streaming.WorldChunkManager`
   - `ProjectC.World.Streaming.ProceduralChunkGenerator`
   - `ProjectC.World.Streaming.ChunkLoader`
   - `ProjectC.World.Streaming.FloatingOriginMP` (опционально)

### Шаг 4: Настройте ссылки

В Inspector компонента `WorldStreamingManager`:
1. Assign **WorldData** (найдите в Assets/_Project/Data/ или создайте новый)
2. Остальные поля оставьте пустыми — они найдутся автоматически (auto-find)

### Шаг 5: Запустите Play Mode

Нажмите **Play** (или Ctrl+P)

**В Console вы должны увидеть:**
```
[StreamingTest_AutoRun] ✅ Awake called - компонент работает!
[StreamingTest_AutoRun] ✅ Start() called!
[StreamingTest_AutoRun] Camera: Main Camera
[StreamingTest_AutoRun] 🎮 Управление: F5=след.точка, F6=пред.точка...
```

Если вы видите эти логи — значит всё работает! Теперь нажимайте F-клавиши.

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
|---------|---------|
| **F5** | Телепортация к следующей точке |
| **F6** | Телепортация к предыдущей точке |
| **F7** | Загрузить чанки вокруг позиции |
| **F8** | Сбросить FloatingOrigin |
| **F9** | Toggle Chunk Grid визуализация |
| **F10** | Toggle Debug HUD |
| **Escape** | Выход из Play Mode |

---

## 🖥️ Network Test Menu (Тестирование мультиплеера)

### Обзор

Сессия 16.04.2026 добавила **NetworkTestMenu** — простое UI меню для тестирования мультиплеера без отдельного UI.

### Компоненты

| Файл | Назначение |
|------|------------|
| `Assets/_Project/Scripts/UI/NetworkTestMenu.cs` | UI компонент меню |
| `Assets/_Project/Scripts/Editor/PrepareTestScene.cs` | Editor скрипт для создания тестовой сцены |
| `Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs` | Спавн игроков при подключении |

### Использование PrepareTestScene

1. **Unity → ProjectC → Prepare Test Scene (Phase 2)**
2. Включите опции:
   - ✅ Network Manager
   - ✅ Network Test Menu (Host/Client)
   - ✅ Test Player (опционально)
3. Нажмите **Create Test Scene**
4. Сцена создастся в `Assets/_Project/Scenes/Test/`

### Создаваемые объекты

```
NetworkTestCanvas (Canvas)
├── NetworkTestMenu
│   └── MenuPanel
│       ├── Title ("Network Test Menu")
│       ├── Status (TextMeshProUGUI)
│       ├── HostButton
│       ├── ClientButton
│       └── ServerButton

EventSystem (EventSystem + InputModule)

NetworkManagerController (dont destroy on load)

TestPlayer (опционально)
├── CharacterController
├── NetworkObject
├── NetworkPlayerSpawner
├── PlayerController
└── Body (placeholder mesh)
```

### Кнопки меню

| Кнопка | Действие |
|--------|----------|
| **Host** | Запускает игру как хост (сервер + клиент) |
| **Client** | Подключается к localhost:7777 |
| **Server** | Запускает выделенный сервер |

### Важные замечания

1. **Используйте NetworkManagerController** — не прямой NetworkManager
2. **Основная сцена** — для полной синхронизации игроков используйте `ProjectC_1.unity`
3. **Тестовая сцена** — для базового тестирования сети

### Ограничения тестовой сцены

- Нет настоящего NetworkPlayer префаба
- Для синхронизации игроков нужен PlayerPrefab в NetworkConfig
- Рекомендуется: основная сцена + добавление NetworkTestMenu

### Добавление в существующую сцену

```csharp
// В PrepareTestScene.CreateNetworkTestMenu()
var nmc = FindAnyObjectByType<NetworkManagerController>();
CreateButton(panel, "Host", new Vector2(0, -20), () => {
    nmc?.StartHost();
    menuObj.SetActive(false);
});
```

### Известные проблемы

1. **NullReferenceException в NMC** — если NetworkManager не инициализирован
2. **Игроки не видят друг друга** — тестовая сцена не имеет NetworkPlayer префаба
3. **Используйте основную сцену** — `ProjectC_1.unity` для полной синхронизации

### Быстрое тестирование

1. Откройте `ProjectC_1.unity`
2. Запустите Host
3. Запустите Client в другом окне
4. Оба должны видеть одинаковый мир

---

## 🧪 Тестирование мультиплеера (Фаза 2)

### Предварительные требования

1. **Unity 6** с установленным **Netcode for GameObjects** (NGO 2.x)
2. **ParrelSync** плагин для тестирования нескольких клиентов (опционально)
3. Все компоненты World Streaming должны быть настроены на сцене

### Настройка для тестирования

1. **NetworkManager настройка:**
   ```
   - Transport: Unity Transport
   - Port: 7777
   - Server Listen Address: 127.0.0.1
   ```

2. **WorldStreamingManager настройка (Host):**
   ```
   - Убедитесь что WorldStreamingManager имеет NetworkBehaviour
   - Добавьте NetworkObject компонент
   - Настройте NetworkPrefab для спавна
   ```

### Тест 6: Host + 1 клиент (Базовая интеграция)

**Цель:** Проверить что клиент получает команды загрузки чанков от сервера

**Шаги:**
1. Host запускает игру (Play as Host)
2. В Console проверяем:
   ```
   [WorldStreamingManager] Host mode: streaming enabled
   [WorldStreamingManager] Waiting for clients...
   ```
3. Запускаем второй экземпляр (Play as Client)
4. Вводим IP: 127.0.0.1
5. Подключаемся
6. Host телепортируется (F5/F6)
7. Наблюдаем в Console клиента:
   ```
   [Client] Received LoadChunkRpc for Chunk(1, 0)
   [Client] Loading chunk Chunk(1, 0)
   ```

**Ожидаемые результаты:**
- ✅ Клиент получает RPC от сервера
- ✅ Чанки загружаются у клиента
- ✅ Горы и облака идентичны у Host и Client
- ✅ FloatingOrigin сдвигается синхронно

### Тест 7: FloatingOrigin синхронизация

**Цель:** Проверить что сдвиг мира происходит одновременно на Host и Client

**Шаги:**
1. Host перемещается к удалённой точке (F5 несколько раз)
2. Host нажимает F8 для сброса FloatingOrigin
3. Наблюдаем в Console Host:
   ```
   [FloatingOriginMP] Before ResetOrigin: cameraPos=50000, totalOffset=0
   [FloatingOriginMP] WorldShiftClientRpc sent with offset=(50000, 0, 0)
   ```
4. Наблюдаем в Console Client:
   ```
   [FloatingOriginMP] ApplyWorldShift (from server): offset=(50000, 0, 0)
   ```

**Ожидаемые результаты:**
- ✅ Host инициирует сдвиг
- ✅ Client получает синхронизацию
- ✅ Позиции игроков относительно мира синхронизированы

### Тест 8: NetworkObject spawn/despawn

**Цель:** Проверить спавн/деспавн объектов с чанками

**Шаги:**
1. Host загружает чанк с фермами
2. Наблюдаем в Network Hierarchy:
   ```
   - Spawned
     - Farm_001 (NetworkObject)
     - Farm_002 (NetworkObject)
   ```
3. Host выходит за пределы радиуса (unloadRadius)
4. Наблюдаем:
   ```
   [ProceduralChunkGenerator] Despawning objects for Chunk(0, 0)
   [NetworkObject] Farm_001 Despawn()
   [NetworkObject] Farm_002 Despawn()
   ```

**Ожидаемые результаты:**
- ✅ NetworkObjects спавнятся с чанком
- ✅ NetworkObjects деспавнятся при выгрузке чанка
- ✅ Client видит те же объекты что Host

---

## 📋 Чеклист тестирования мультиплеера

### Перед тестом:
- [ ] NetworkManager настроен
- [ ] WorldStreamingManager имеет NetworkBehaviour
- [ ] Port открыт (7777)
- [ ] Firewall не блокирует соединение

### Во время теста:
- [ ] Host загружается первым
- [ ] Client подключается успешно
- [ ] ChunkRpc вызывается
- [ ] FloatingOrigin синхронизирован
- [ ] NetworkObjects спавнятся правильно

### После теста:
- [ ] Нет errors в Console
- [ ] Нет десинхронизации позиций
- [ ] Чанки выгружаются корректно

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