# STORM/EVENT CLOUD INTEGRATION — DOCUMENTATION

**Дата:** 21 мая 2026 | **Status:** 🚀 Phase 1-2 Complete, ⚠️ Physics + Patterns Need Testing

---

## 1. ЧТО СДЕЛАНО

### ✅ Phase 1: StormCloudGenerator + CloudSpherePhysics

**StormCloudGenerator.cs** — создаёт storm objects:
- Пул через `Dictionary<uint, Storm>`
- `SpawnStorm(stormId, position, pattern, intensity)`
- Интеграция с `generator7.0.Generate()`
- Создаёт `GameObject` на каждую сферу
- Добавляет `Rigidbody` (isKinematic) + `CloudSpherePhysics`

**StormCloudGenerator prefab:** `Assets/_Project/Prefabs/StormController.prefab`

**StormController prefab:** `Assets/_Project/Prefabs/StormController.prefab`
- ⚠️ **ВАЖНО:** НЕ имеет NetworkObject компонента!
- Это local-only MonoBehaviour, НЕ сетевой объект
- Причина: чтобы избежать GlobalObjectIdHash конфликтов

**CloudSpherePhysics.cs** — parting physics:
- `FixedUpdate()` — проверяет дистанцию до игрока
- `ApplyParting()` — applies impulse когда игрок близко
- SpringBack force для возврата сфер
- PartingCooldown для защиты от спама

### ✅ Phase 2: ServerStormManager + patternGUID

**ServerStormManager.cs:**
- `SpawnInitialStorms()` — спавнит `_maxStorms` штормов при старте
- `StormSpawnClientRpc(id, position, intensity, patternGUID)` — отправляет на клиенты
- Параметры:
  - `_stormPatterns[]` — массив CloudLayerConfig пресетов
  - `_useRandomPattern` — случайный выбор паттерна
  - `_stormControllerPrefab` — префаб для инстанциирования

**StormController.cs:**
- Статический словарь `ClientControllers` для регистрации
- `Initialize(id, position, intensity, patternGUID)` — инициализация шторма
- `LoadPatternByGUID()` — загрузка CloudLayerConfig по GUID (editor-only)

---

## 2. АРХИТЕКТУРА (ТЕКУЩАЯ)

```
Server                                    Client
─────────────────────────────────────────────────────────────
ServerStormManager                        StormController (local MonoBehaviour)
│                                          │
├── OnNetworkSpawn()                      │
│   └── SpawnInitialStorms()               │
│       └── SpawnStorm() × N              │
│           └── StormData{pos, guid}      │
│                                          │
└── StormSpawnClientRpc() ────────────────┼── Instantiate(prefab)
     (id, position, intensity, guid)        │
                                            │
                                            └── StormController.Initialize()
                                                  │
                                                  └── _cloudGenerator.SpawnStorm()
                                                        │
                                                        └── generator7.0.Generate()
                                                              │
                                                              └── ~48-500 spheres (local GameObjects)
```

### Ключевые принципы:

1. **Сервер контролирует:**
   - КОГДА спавнить (timing)
   - ГДЕ спавнить (worldPosition)
   - КАКОЙ паттерн (patternGUID)
   - Интенсивность (intensity)

2. **Клиент создаёт локально:**
   - Получает команду через ClientRpc
   - Загружает паттерн по GUID (editor-only API)
   - Генерирует сферы через generator7.0
   - Создаёт GameObject'ы с physics

3. **StormController — НЕ сетевой объект:**
   - Нет NetworkVariable
   - Нет NetworkObject компонента
   - Нет синхронизации позиций сфер
   - Parting physics — client-side only

---

## 3. ФАЙЛЫ И СТАТУС

| Файл | Назначение | Status |
|------|------------|--------|
| `CloudGenerator.cs` | generator7.0 | ✅ Работает |
| `CloudTypes.cs` | Типы данных | ✅ Работает |
| `StormCloudGenerator.cs` | Storm spawning | ✅ Работает |
| `CloudSpherePhysics.cs` | Parting physics | ✅ Работает |
| `StormController.cs` | Storm management | ✅ Работает |
| `ServerStormManager.cs` | Server-side control | ✅ Работает |
| `StormController.prefab` | Prefab для спавна | ✅ Работает (БЕЗ NetworkObject!) |
| `CloudLayerConfig.cs` | Паттерн конфиг | ✅ Работает |
| `Storm_Column_light.asset` | Storm паттерн | ✅ Работает |

---

## 4. ЧТО НУЖНО ДЛЯ ТЕСТА PHYSICS

### Проблема:
Паттерн `Storm_Column_light` с параметрами:
- Floors: 80, RingsPerFloor: 30 → **2440 сфер на шторм**
- 5 штормов × 2440 = **12,200 GameObject'ов**
- Это слишком много для тестирования physics

### Решение для теста:
В `Storm_Column_light.asset` уменьшить:
```
Floors: 80 → 8
Rings Per Floor: 30 → 6
```
Это даст ~48 сфер на шторм, легко увидеть parting.

### Проверка parting:
1. Запустить игру
2. Нажать **T** для спавна тестового шторма
3. Пролететь сквозь шторм на самолёте
4. Сферы должны расступаться при приближении

---

## 5. PHASE 3: EVENTCLOUD (PENDING)

### Задачи:
1. ❌ Создать RuntimeMeshSampler для ParentMesh
2. ❌ Добавить поддержку ParentMeshPath в CloudLayerConfig
3. ❌ Интегрировать с серверным триггером

### Ограничение:
`CloudParentMesh.SampleSurface()` работает только в Editor (#if UNITY_EDITOR). Нужен runtime аналог.

---

## 6. ИЗВЕСТНЫЕ ПРОБЛЕМЫ

### 6.1 AssetDatabase в runtime
`StormController.LoadPatternByGUID()` использует `AssetDatabase.GUIDToAssetPath()` который **работает только в Editor**.

**Временное решение:** Паттерн передаётся через preset reference в `_availablePatterns[]`.

**Правильное решение:** Использовать Addressables или Resources.Load().

### 6.2 Производительность
- 12,000 GameObject'ов = очень тяжело
- Каждая сфера: MeshFilter + MeshRenderer + Rigidbody + CloudSpherePhysics
- **Решение:** Уменьшить количество сфер через параметры паттерна

### 6.3 GlobalObjectIdHash конфликт (РЕШЕНО)
`StormController.prefab` изначально имел NetworkObject с хэшем 804704506. При инстанциировании Unity Netcode пытался зарегистрировать объект, но хэш уже использовался.

**Решение:** Удалён NetworkObject компонент из префаба. StormController — local-only.

---

## 7. НАСТРОЙКА В EDITOR

### ServerStormManager (на CloudManager):
```
_maxStorms: 2 (для теста)
_stormPatterns: [Storm_Column_light.asset]
_useRandomPattern: true
_stormControllerPrefab: StormController.prefab
```

### Storm_Column_light.asset (для теста physics):
```
Archetype: Column
CascadeDepth: 3
BumpsPerLevel: 24
ChildRatio: 30
ColumnParams:
  Floors: 8
  RingsPerFloor: 6
  BaseRadius: 150
  TopRadius: 250
  Wobble: 0.3
```

---

## 10. ИЗВЕСТНЫЕ ПРОБЛЕМЫ И_pending РАБОТА

### 🔧 Physics (CloudSpherePhysics)

**Статус:** ⚠️ Требует тестирования и отладки

**Известные проблемы:**
1. **Не подтверждено визуально** — parting physics работает в коде, но не проверен визуально
2. **PartingDistance = 30m** — может быть недостаточно для больших штормов (Column 4000м высотой)
3. **SpringBack возвращает сферы** — после пролёта сферы возвращаются обратно, а не остаются разбросанными
4. **FindLocalPlayer() ищет тег "Player"** — если тег другой, physics не работает

**Что нужно сделать:**
1. [ ] Уменьшить паттерн до ~48 сфер (Floors: 8, Rings: 6)
2. [ ] Подтвердить визуально что сферы расступаются при пролёте
3. [ ] Проверить правильный тег игрока (может быть "Player" или другой)
4. [ ] Настроить PartingDistance под размер шторма
5. [ ] Решить: должны ли сферы возвращаться (SpringBack) или оставаться разбросанными

**Параметры для настройки:**
```
CloudSpherePhysics:
  PartingDistance: 30-100m (подобрать под размер шторма)
  PartingStrength: 50 (сила impulse)
  SpringBack: true/false (по желанию)
  SpringK: 8 (жёсткость пружины)
  PartingCooldown: 0.5s (защита от спама)
```

---

### 🎨 Паттерны (CloudLayerConfig)

**Статус:** ⚠️ Требуют оптимизации

**Известные проблемы:**
1. **Storm_Column_light генерирует 2440 сфер** — слишком много для GameObject per sphere подхода
2. **80 Floors × 30 Rings = 2400 сфер** — создаёт 12,000+ GameObject'ов при 5 штормах
3. **Нет промежуточных пресетов** — только один паттерн Storm_Column_light

**Параметры для создания новых паттернов:**
```
Light:    Floors=8,  Rings=6,  BaseR=150, TopR=250  (~48 spheres)
Medium:   Floors=12, Rings=8,  BaseR=200, TopR=400  (~200 spheres)
Heavy:    Floors=16, Rings=12, BaseR=300, TopR=600  (~500+ spheres)
```

**Что нужно сделать:**
1. [ ] Создать Storm_Column_Medium пресет
2. [ ] Создать Storm_Column_Heavy пресет (если нужно больше сфер)
3. [ ] Настроить правильное соотношение: визуал vs производительность

---

## 11. HOTKEYS ДЛЯ ТЕСТА

| Клавиша | Действие |
|---------|----------|
| T | Spawn тестовый шторм (через TestStormSpawner) |
| Y | Despawn storm 1 |
| U | Despawn all storms |

---

## 12. СЛЕДУЮЩИЕ ШАГИ

### Высокий приоритет:
1. ✅ Storm clouds спавнятся
2. ✅ Server control через patternGUID
3. ⏳ **Протестировать parting physics** — главная задача
4. ⏳ **Уменьшить паттерн** для теста physics (~48 сфер)

### Средний приоритет:
5. ⏳ Подтвердить визуально что сферы расступаются
6. ⏳ Настроить PartingDistance / PartingStrength
7. ⏳ Правильная загрузка паттернов в runtime (Addressables или Resources)
8. ⏳ Phase 3: EventCloud с ParentMesh

### Низкий приоритет:
9. ⏳ Phase 4: Server отправляет паттерны по имени
10. ⏳ Lightning эффекты на StormController
11. ⏳ Создать Medium/Heavy storm пресеты

---

**Status:** 🚀 Core functionality complete, physics and patterns need testing, debugging and optimization