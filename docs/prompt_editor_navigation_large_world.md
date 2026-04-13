# ПРОМТ: Unity Editor навигация для больших миров (Open World)

**Контекст проекта:**
- Unity 6 URP
- Мир радиусом ~350,000 units (XZ координаты до ±260,000)
- 5 горных массивов, 29 пиков
- Координаты пиков: Эверест (0,0), Аконкагуа (-208800,-105500), Денали (62750,234250), и т.д.

**Проблема:**
В Unity Editor Scene View НЕВОЗМОЖНО переместиться к удалённым объектам (координаты >100,000 units). При телепортации камеры к дальнему пику:
- Камера остаётся на месте или перемещается некорректно
- Unity показывает предупреждение: "Due to floating-point precision limitations, it is recommended to bring the world coordinates of the GameObject within a smaller range."
- Невозможно размещать окружение (фермы, здания, детали) вокруг удалённых пиков

**Что уже сделано:**
- ✅ Floating Origin система для Play Mode (работает)
- ✅ Far Clip Plane = 1,000,000
- ✅ URP Shadow Distance = 500,000
- ✅ Camera Relative Culling включено
- ❌ Editor Scene View navigation — НЕ РАБОТАЕТ

---

## ЗАДАЧИ ДЛЯ ИССЛЕДОВАНИЯ

### 1. Поиск в интернете (ОБЯЗАТЕЛЬНО):
- "Unity Editor large world navigation"
- "Unity SceneView camera large coordinates"
- "Unity open world editor tools"
- "Unity massive world scene editing"
- "Unity Editor floating point precision scene view"
- "Unity world partitioning editor"
- "Unity subscene large world"

### 2. Asset Store исследование:
- Найти пакеты для large-scale world editing
- Проверить: "World Streamer", "MapMagic", "Gaia Pro", "Unity Terrain Tools"
- Есть ли инструменты для Editor навигации?

### 3. .agents анализ:
Использовать специализированных агентов:
- `@unity-specialist` — поиск решений для Editor навигации
- `@technical-artist` — world partitioning, LOD системы
- `@gameplay-programmer` — Editor extensions, custom tools

### 4. Технические решения для исследования:

**Вариант A: SceneView Camera Scripting**
- Можно ли программно управлять Scene View камерой?
- `SceneView.lastActiveSceneView` API
- `SceneView.FrameSelected()` для фокусировки на объектах

**Вариант B: World Partitioning**
- Разбить мир на "subscenes" или "chunks"
- Загружать/выгруживать части мира по мере необходимости
- Как в Unreal Engine World Partition

**Вариант C: Editor Tool для телепортации**
- Создать Editor Window с кнопками "Go to Peak: [Name]"
- При нажатии — SceneView фокусируется на пике
- Использовать `SceneView.lastActiveSceneView.LookAtDirect()`

**Вариант D: World Origin Shifting в Editor**
- Floating Origin но для Editor Mode
- Сдвигать ВСЕ объекты сцены когда редактор далеко от origin
- Сохранять relative positions

**Вариант E: Relative Coordinates Workflow**
- Работать не с абсолютными координатами, а с relative
- При редактировании пика — временно сдвигать его к origin
- После редактирования — возвращать на место

---

## ОЖИДАЕМЫЙ РЕЗУЛЬТАТ

1. **Список найденных решений** (ссылки на документацию, форумы, Asset Store)
2. **Рекомендация** — какой подход лучше для Project C
3. **План реализации** — пошагово, что нужно сделать
4. **Если нужно** — новый Editor Tool скрипт

---

## ДОПОЛНИТЕЛЬНЫЙ КОНТЕКСТ

**Цель игры:**
- Игрок летает над облаками на корабле
- Посещает горные массивы
- Фермы на склонах гор (9 ферм на разных пиках)
- Нужно размещать окружение: здания, террасы, платформы, детали

**Workflow разработчика:**
1. Генерируются горы (29 пиков)
2. Нужно разместить фермерские платформы, здания, террасы
3. Нужно настроить окружение вокруг каждого пика
4. ВСЁ это делается в Unity Editor Scene View

**Критично:**
- Developer должен иметь возможность легко перемещаться между пиками в Editor
- Размещать объекты на склонах гор
- Видеть и редактировать окружение каждого пика

---

## ВОПРОСЫ ДЛЯ ИССЛЕДОВАНИЯ

1. Как другие студии делают open-world игры в Unity с мирами >100km²?
2. Есть ли встроенные инструменты Unity для работы с большими мирами?
3. Какие Asset Store пакеты решают эту проблему?
4. Можно ли кастомизировать Scene View камеру?
5. Как работает World Partitioning в Unity 6?
