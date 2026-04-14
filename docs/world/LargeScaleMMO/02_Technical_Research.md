# Technical Research: Large-Scale MMO Worlds in Unity 6

**Дата:** 14 апреля 2026  
**Проект:** ProjectC_client  
**Исследование проведено:** Qwen Code Agent + Web Search + Official Unity Documentation

---

## 1. Executive Summary

Это документ содержит результаты глубокого исследования проблемы навигации и стриминга в больших мирах Unity для MMO проектов. Исследование включает:

- Анализ официальной документации Unity 6 (6000.x LTS)
- Сравнение 7+ Asset Store решений для world streaming
- Анализ архитектурных паттернов для MMO
- Исследование мультиплеерной синхронизации сцен
- Best practices от Unity и сообщества

---

## 2. Floating Point Precision Problem

### 2.1. Официальная позиция Unity

**Из Unity Discussions (март 2025):**
> "We're not working on support for double-precision coordinate systems at this time."

Unity **не планирует** добавлять double precision координаты в ближайшее время.

### 2.2. Технические ограничения

| Параметр | Значение | Последствия |
|----------|----------|-------------|
| Transform позиция | float (32-bit) | ~7 значащих цифр точности |
| Безопасный радиус | ~10,000 units | За пределами — артефакты |
| Рекомендуемый радиус | ~20-50 km | Максимум для стабильной работы |
| ProjectC мир | 350,000 units radius | **КРИТИЧЕСКИ БОЛЬШОЙ** |

### 2.3. Симптомы проблемы

- Дрожание камеры при удалении от origin
- Нестабильная физика и коллизии
- Scene View не может перемещаться к координатам >100,000 units
- Предупреждение Unity: *"Due to floating point precision limitations..."*

### 2.4. Решение: Floating Origin

**Принцип:** Периодически сдвигать **весь мир и игрока** обратно к (0,0,0), сохраняя относительные позиции.

**Для MMO — модификация:**
- Сервер хранит координаты в `double` (C# double)
- Сервер — единственный источник сдвигов мира
- Клиенты применяют сдвиг синхронно по RPC
- Сдвиг в `LateUpdate` ДО NetworkTransform snapshot

---

## 3. Unity 6 Scene Management

### 3.1. Additive Scene Loading

**API:** `UnityEngine.SceneManagement`

```csharp
// Асинхронная аддитивная загрузка
var asyncOp = SceneManager.LoadSceneAsync("Zone_Himalayan", LoadSceneMode.Additive);
asyncOp.completed += (op) => Debug.Log("Zone loaded");

// Выгрузка
var scene = SceneManager.GetSceneByName("Zone_Himalayan");
SceneManager.UnloadSceneAsync(scene);
```

**Best Practices:**
- ВСЕГДА использовать `LoadSceneAsync`, НЕ синхронный `LoadScene`
- Для MMO — разбивать мир на зоны/регионы, каждая в отдельной сцене
- Сцены должны быть в **Build Settings → Scenes in Build**
- Unity 6 вводит **Scene Templates** для стандаризации

### 3.2. NGO Scene Synchronization

**Пакет:** `com.unity.netcode.gameobjects@2.4`

**Ключевое правило:** Используйте `NetworkManager.Singleton.SceneManager.LoadScene()`, НЕ `UnityEngine.SceneManagement`.

```csharp
// ПРАВИЛЬНО (сервер):
var status = NetworkManager.SceneManager.LoadScene("Zone_A", LoadSceneMode.Additive);

// Подписка на события:
NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;

private void OnSceneEvent(SceneEvent sceneEvent) {
    if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted) {
        // Безопасно вызывать RPC
    }
}
```

**Late Joining Clients:** Новые клиенты автоматически получают все загруженные сервером сцены.

### 3.3. Server-Side Scene Validation

```csharp
NetworkManager.SceneManager.VerifySceneBeforeLoading = ServerSideSceneValidation;

private bool ServerSideSceneValidation(int sceneIndex, string sceneName, LoadSceneMode mode) {
    // Запретить загрузку restricted сцен
    if (sceneName == "AdminScene") return false;
    return true;
}
```

---

## 4. Addressables for Dynamic Content

**Пакет:** `com.unity.addressables@2.5`

### 4.1. Scene Loading via Addressables

```csharp
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;

private AsyncOperationHandle<SceneInstance> loadHandle;

// Загрузка
loadHandle = Addressables.LoadSceneAsync("Zone_Himalayan", LoadSceneMode.Additive);

// Выгрузка
Addressables.UnloadSceneAsync(loadHandle);
```

### 4.2. Remote Content Delivery (CDN)

- Адресуемые группы настраиваются через `RemoteBuildPath` / `RemoteLoadPath`
- В `Settings` включить **Build Remote Catalog**
- На CDN загружаются: `.bundle` файлы, `catalog_timestamp.json`, `catalog_timestamp.hash`
- Работает с любой CDN или Unity Cloud Content Delivery (CCD)

### 4.3. Best Practices

- Создавать **bootstrap-сцену** (не Addressable), которая загружает первую игровую сцену
- НЕ размещать Addressable-ассеты напрямую в не-Addressable сценах (создаёт дубликаты)
- Использовать `AssetReference` для ссылок на Addressable-ассеты
- Разделять сцены на логические группы для независимого управления памятью

---

## 5. ECS SubScene & Scene Sections

**Пакет:** `com.unity.entities@6.5` (для Unity 6000.4+)

### 5.1. Архитектура SubScene

- ECS использует SubScene **вместо** обычных сцен — ядро сцен Unity несовместимо с ECS
- При закрытии SubScene автоматически запускается **Baking** — конвертация GameObjects в Entities
- **Инкрементальный Baking** — изменения пересчитываются частично
- В билдах SubScene всегда работают в режиме стриминга (Closed)

### 5.2. Scene Sections — стриминг внутри SubScene

- Сущности группируются по секциям. По умолчанию — секция `0`
- Каждая сущность получает `SceneSection` (shared-компонент: `Hash128` GUID + `int` index)
- Секция `0` **обязана** загружаться первой и выгружаться последней
- Перекрёстные ссылки возможны только внутри одной секции или с секцией `0`

### 5.3. API для загрузки/выгрузки секций

```csharp
// Загрузка секции
var sectionBuffer = EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity);
var sectionEntity = sectionBuffer[i].SectionEntity;
EntityManager.AddComponent<RequestSceneLoaded>(sectionEntity);

// Выгрузка секции — удалить компонент RequestSceneLoaded
EntityManager.RemoveComponent<RequestSceneLoaded>(sectionEntity);
```

### 5.4. Готовность к продакшену

- Официально НЕ "preview" или "experimental" — стабильный релиз 6.5
- **Netcode for Entities 1.6.2** выпущен в июле 2025
- **Megacity Metro** — демо-проект Unity: DOTS + Netcode for Entities + стриминг для 100+ игроков

### 5.5. Критическое ограничение для Project C

**NGO и Netcode for Entities — НЕСОВМЕСТИМЫ между собой.**

Project C использует NGO → НЕЛЬЗЯ использовать ECS SubScene для стриминга. Нужно использовать обычные сцены + Addressables.

---

## 6. World Partition — есть ли в Unity?

**Вывод:** В Unity **нет аналога Unreal Engine World Partition**.

World Partition — исключительно функция Unreal Engine. В Unity для больших миров используются:

- **Addressables** для стриминга контента по зонам/регионам
- **ECS SubScene + Scene Sections** для стриминга Entity-контента (НЕ совместимо с NGO)
- **Megacity Metro** демонстрирует large-scale streaming через кастомную систему на базе DOTS

Для MMO в Unity 6 стандартный подход — разделить мир на секции/зоны, каждая как отдельная Addressable-сцена, загружаемая по мере приближения игрока.

---

## 7. Dedicated Server Build

### 7.1. Сборка

```bash
# Командная строка
Unity -batchmode -buildTarget Linux64 -standaloneBuildSubtarget Server -executeMethod BuildScript.Build
```

### 7.2. Особенности

- Удаляет ненужные ресурсы и код (рендеринг, аудио, UI)
- Заменяет текстуры и аудио на мелкие dummy-ассеты для экономии RAM
- Поддерживает обычный `SceneManager` и `Addressables.LoadSceneAsync`

### 7.3. Стриминг на сервере

- Сервер загружает сцены/секции так же, как клиент, но без рендеринга
- Можно использовать те же Addressables, но сервер не нуждается в визуальных ассетах
- Для MMO-сервера рекомендуется загружать сцены по мере необходимости, а не весь мир целиком

---

## 8. Asset Store Solutions Comparison

### 8.1. World Streamer 2 (NatureManufacture)

| Параметр | Значение |
|----------|----------|
| Цена | $60 |
| Версия | 1.8.8 (март 2026) |
| Рейтинг | 168 отзывов, 942 в избранном |
| Unity 6 | Да (до 6000.3.x) |
| URP/HDRP | Да |

**Функции:**
- Потоковая загрузка террейнов, объектов, LOD, теней и настроек освещения
- Floating point correction система
- Зацикленный мир (looped world system)
- Замена фоновых террейнов на low-poly meshes
- Интеграция с Addressables

**Мультиплеер:** Встроенной поддержки НЕТ. Требуется глубокая кастомизация под NGO.

**Вердикт:** НЕ готов для MMO "из коробки". Придется вручную синхронизировать загрузку чанков между клиентом и сервером.

---

### 8.2. SECTR Complete (Pixel Crushers)

| Параметр | Значение |
|----------|----------|
| Цена | $99 |
| Версия | 2019.0.9 (март 2025) |
| Рейтинг | 1503 в избранном |
| Unity 6 | Да (отдельная версия) |

**Функции:**
- Модульная система: Stream, Cull, Audio, Visibility
- Occlusion culling (главное преимущество)
- Детальное зонирование сцен/чанков

**Мультиплеер:** Встроенной поддержки НЕТ. Однако четкое зонирование упрощает синхронизацию загрузки контента по сети.

**Вердикт:** Архитектурно ближе к тому, что нужно для MMO, но требует значительной доработки.

---

### 8.3. BigWorldStreamer (IGDMaster)

| Параметр | Значение |
|----------|----------|
| Цена | $38 |
| Unity 6 | 6000.0.49+ |

**Функции:**
- Quadtree-based система разделения сцен/террейнов
- Бесшовная загрузка террейнов
- Поддержка AssetBundle (`.ab`)

**Вердикт:** Дешевый вариант с quadtree-подходом, но непроверенный. Высокий риск.

---

### 8.4. MapMagic 2 (Denis Pahunov)

| Параметр | Значение |
|----------|----------|
| Цена | БЕСПЛАТНО (базовая), Bundle $112.50 |
| Рейтинг | 133 отзыва, 3286 в избранном |

**Функции:**
- Нодовый/графовый редактор процедурной генерации террейнов
- Бесконечная (infinite) генерация карт
- Генерация "на лету" в playmode

**Мультиплеер:** Встроенной поддержки НЕТ. Главная проблема: процедурная генерация должна быть детерминированной и синхронизированной.

**Вердикт:** Отлично для генерации мира, НЕ для стриминга/синхронизации. Можно комбинировать с кастомной системой загрузки.

---

### 8.5. Gaia Pro 2023 (Procedural Worlds)

| Параметр | Значение |
|----------|----------|
| Цена | ~$100 (часто со скидкой 50%) |
| Версия | 4.0.5 |
| Статус | Unity Verified Solution |

**Функции:**
- Генерация террейнов и сцен
- Multi-terrain support с потоковой загрузкой
- Stamping, spawning, erosion, masking

**Вердикт:** Инструмент для генерации мира, не для сетевого стриминга.

---

### 8.6. RTP (Relief Terrain Pack) v3.3

| Параметр | Значение |
|----------|----------|
| Последнее обновление | Январь 2021 (v3.3r) |

**Вердикт:** Ассет ФОКУСИРУЕТСЯ на шейдерах/визуале террейнов, а НЕ на стриминге. Фактически заброшен. НЕ РЕКОМЕНДУЕТСЯ.

---

### 8.7. Сводная таблица

| Решение | Цена | Unity 6 | URP/HDRP | Мультиплеер | Сложность | MMO-готовность |
|---------|------|---------|----------|-------------|-----------|----------------|
| **World Streamer 2** | $60 | Да | Да | Нет (нужна доработка) | Низкая | Низкая |
| **SECTR Complete** | $99 | Да (отдельная версия) | Да | Нет (но архитектурно ближе) | Высокая | Средняя |
| **BigWorldStreamer** | $38 | Да | URP/HDRP | Заявлено, но неясно | Средняя | Низкая |
| **MapMagic 2** | Free/$112 | Да | Да | Нет | Средняя | Низкая (генерация, не стриминг) |
| **Gaia Pro 2023** | ~$100 | Да | Да | Нет | Средняя | Низкая (генерация, не стриминг) |
| **RTP v3.3** | ~$40 | Нет | Нет | Нет | Высокая | Заброшен |
| **Кастомное (Addressables)** | Бесплатно | Да | Да | Полный контроль | Очень высокая | Высокая |

---

## 9. MMO Architecture: Area-Based Sharding

### 9.1. Архитектура

```
              +-----------------+
              |  World Server   |
              |  (маршрутизация, |
              |   глоб. состояние)|
              +--------+--------+
                       |
  +--------------------+--------------------+
  |                    |                    |
+-------v-------+ +-------v-------+ +-------v-------+
| Area Server A | | Area Server B | | Area Server C |
| (Зона 1-4)    | | (Зона 5-8)    | | (Зона 9-12)   |
+-------+-------+ +-------+-------+ +-------+-------+
        |               |                   |
        +---------------+-------------------+
                        |
               +--------v--------+
               |  Unity 6 Client |
               |  Addressables   |
               |  Origin Shift   |
               +-----------------+
```

### 9.2. Бесшовные переходы между зонами

- При приближении игрока к границе текущий сервер заранее отправляет его состояние соседнему
- На границах отображаются **ghost-объекты** из смежной зоны — создаёт иллюзию непрерывного мира
- Кросс-граничные действия (снаряды, квесты) обрабатываются в **перекрывающихся буферных регионах**

### 9.3. Синхронизация

- Игроки: позиция, здоровье, баффы
- Локальная логика: ИИ, бой, квесты
- Глобальное состояние: погода, события, спавн (управляется World Server)

---

## 10. Reference: Megacity Metro

**Ссылка:** https://unity.com/demos/megacity-competitive-action-sample

**Технологии:**
- DOTS + Netcode for Entities (НЕ NGO!)
- Крупномасштабный стриминг открытых локаций
- Server-authoritative архитектура
- 100+ игроков, кроссплатформенность (ПК, мобильные, Nintendo Switch)
- Интеграция с UGS: Matchmaker, Authentication, Vivox

**Важно:** Megacity Metro использует **Netcode for Entities**, а НЕ NGO. Project C использует NGO → нельзя напрямую использовать архитектуру Megacity.

---

## 11. Итоговые рекомендации

### 11.1. Для Project C (NGO-based MMO)

**Рекомендуемая архитектура:**

1. **Кастомная система стриминга** на базе chunk-based подхода
2. **Addressables** для динамической загрузки контента
3. **Floating Origin** с серверной синхронизацией (FloatingOriginMP)
4. **NGO NetworkSceneManager** для управления сценами
5. **Процедурная генерация** per-chunk из детерминированного Seed
6. **Server-authoritative** спавн/деспавн NetworkObjects per chunk

### 11.2. Почему не готовые ассеты?

| Ассет | Проблема для MMO |
|-------|-----------------|
| World Streamer 2 | Нет мультиплеерной синхронизации |
| SECTR | Высокая сложность, всё равно нужна доработка |
| MapMagic 2 | Генерация, не стриминг |
| Gaia Pro | Генерация, не стриминг |

**Вывод:** Ни один готовый ассет не решает задачу MMO-стриминга "из коробки". Все они рассчитаны на singleplayer.

### 11.3. Быстрый старт vs Полное решение

**Если нужен быстрый результат:**
- World Streamer 2 как база + глубокая кастомизация сетевого слоя
- Ожидать значительных доработок

**Если нужно полное решение:**
- Кастомная система с нуля (как описано в `01_Architecture_Plan.md`)
- Больше начальных затрат, но полный контроль

---

## 12. Полезные ссылки

### Официальная документация
- [Unity 6 Scene Management](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/SceneManagement.LoadSceneMode.Additive.html)
- [Addressables 2.5 - Scene Loading](https://docs.unity3d.com/Packages/com.unity.addressables@2.5/manual/LoadingSceneAsync.html)
- [ECS SubScene 6.5](https://docs.unity3d.com/Packages/com.unity.entities@6.5/manual/conversion-subscenes.html)
- [NGO Scene Management](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/basics/scenemanagement/using-networkscenemanager.html)
- [Dedicated Server](https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server.html)

### Сообщество
- [Floating Point Problem - Unity Discussions](https://discussions.unity.com/t/floating-point-problem-on-large-maps/945102)
- [Code Monkey: Solution for HUGE WORLDS](https://unitycodemonkey.com/text.php?v=r5WtbelFC-E)
- [MMO Architecture: Area-Based Sharding](https://prdeving.wordpress.com/2025/05/12/mmo-architecture-area-based-sharding-shared-state-and-the-art-of-herding-digital-cats/)

### Asset Store
- [World Streamer 2](https://assetstore.unity.com/packages/tools/terrain/world-streamer-2-176482)
- [SECTR Complete](https://assetstore.unity.com/publishers/1468)
- [MapMagic 2](https://assetstore.unity.com/packages/tools/terrain/mapmagic-2-infinite-lands-163616)
- [Gaia Pro 2023](https://assetstore.unity.com/packages/tools/terrain/gaia-pro-2023-terrain-scene-generator-202320)

### Референсы
- [Megacity Metro (Unity Demo)](https://unity.com/demos/megacity-competitive-action-sample)
