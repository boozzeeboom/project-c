# STORM CLOUD — ПОДРОБНАЯ ИНСТРУКЦИЯ ПО НАСТРОЙКЕ И ТЕСТИРОВАНИЮ

**Обновлено:** 19 мая 2026

---

## 1. ЧТО ИМЕЕТСЯ В СЦЕНЕ

В сцене `BootstrapScene` уже присутствуют:

| Объект | Назначение |
|--------|------------|
| `CloudManager` | Главный менеджер облаков |
| └─ `StormCloudGenerator` | **НАШ** компонент для спавна storm'ов |
| └─ `TESTSTORMSPAWNER` | Тестовый скрипт (клавиши T/Y/U) |
| └─ `Storm_0` … `Storm_4` | 5 заготовок для storm'ов (НЕЙСТРОЙНЫЕ) |
| `ServerStormManager` | Серверный менеджер (вызывает StormController.Initialize) |
| `WindManager` | Ветер |
| `PlayerSpawner` | Игрок с тегом "Player" |

---

## 2. АРХИТЕКТУРА

```
ServerStormManager (сервер)
    │
    └─→ StormSpawnClientRpc(id, pos, intensity)
            │
            ▼
    StormController.Initialize()
            │
            ├─ gameObject.name = $"Storm_{id}"
            ├─ transform.position = worldPos
            │
            └─→ StormCloudGenerator.SpawnStorm()
                    │
                    ├─ CloudGenerator.Generate() → List<CloudSphere>
                    │
                    ├─ for each sphere:
                    │     Create GameObject "StormSphere_X"
                    │     MeshFilter + MeshRenderer
                    │     Rigidbody (isKinematic)
                    │     CloudSpherePhysics (parting)
                    │
                    └─→ Storm GameObject с child-сферами
```

---

## 3. ПОШАГОВАЯ НАСТРОЙКА

### ШАГ 1. Создать CloudLayerConfig

**В Project окне:**
1. Перейти в папку `Assets/_Project/Art/`
2. **Правый клик** → Create → Project C → **Cloud Layer Config**
3. Назвать файл: `Storm_Column_Light`

**Заполнить в Inspector:**

```
【Basic】(раскрыть секцию)
Name: Storm_Column_Light

【 Тип слоя】
Layer Type: Storm

【Generator v7.0 Fields】
Archetype: Column          ← выпадающий список
Generator Seed: 12345      ← любое число
Jitter: 0.3
Clustering: 0.5
Position Variation: 0.5

【Sphere Archetype】(оставить по умолчанию)
Cascade Depth: 3
Bumps Per Level: 24
...

【Column Archetype】(КЛЮЧЕВОЕ!)
Height: 300
Base Radius: 150
Top Radius: 250
Floors: 8
Rings Per Floor: 6
Wobble: 0.3

【Size Range】
Min: 5
Max: 20
```

**Сохранить:** File → Save Project (Ctrl+Shift+S)

---

### ШАГ 2. Назначить паттерн на StormCloudGenerator

**В Hierarchy:**
1. Раскрыть `CloudManager`
2. Выбрать `StormCloudGenerator`

**В Inspector:**
```
StormCloudGenerator (Script)
  ├─ Pool Settings
  │     Max Active Storms: 5
  │
  ├─ Cloud Material
  │     (оставить пустым)
  │
  └─ Default Storm Pattern
        ← нажать кружок справа
        ← найти "Storm_Column_Light"
        ← кликнуть на него
```

**Проверка:** после назначения поле должно показать `Storm_Column_Light`

---

### ШАГ 3. Проверить TestStormSpawner

**В Hierarchy:**
1. Раскрыть `CloudManager`
2. Выбрать `TESTSTORMSPAWNER`

**В Inspector:**
```
Test Storm Spawner (Script)
  ├─ Test Pattern
  │     ← нажать кружок, найти "Storm_Column_Light"
  │     (или оставить пустым — тогда возьмётся из StormCloudGenerator)
  │
  ├─ Test Storm Id: 1
  │
  └─ Test Position: X=0, Y=1200, Z=0
```

---

### ШАГ 4. Удалить/отключить старые Storm_0…Storm_4

Эти объекты были созданы для старой системы и **несовместимы** с новой:

1. В Hierarchy раскрыть `CloudManager`
2. Выделить все `Storm_0`, `Storm_1`, `Storm_2`, `Storm_3`, `Storm_4`
3. **Правый клик** → Delete
4. Или выбрать каждый и в Inspector снять галочку **Active** (чекбокс слева от имени)

**Пояснение:** У них есть компонент `StormCloudGenerator` но они **НЕ СПАВНЯТ** сферы. Они были для old instancing системы и сейчас не работают.

---

## 4. ТЕСТИРОВАНИЕ

### Запуск

1. **Play** (Ctrl+P)
2. Подождать загрузки сцены
3. Нажать **T** — спавн storm

### Что должно появиться в консоли

```
[StormCloudGenerator] Awake. Sphere mesh: StormSphereMesh, defaultStormPattern: Storm_Column_Light
[Test] Using pattern: Storm_Column_Light, archetype=Column
[Test] SpawnStorm called for storm 1
[StormCloudGenerator] SpawnStorm called: id=1, pos=(0.0, 1200.0, 0.0), pattern=Storm_Column_Light, intensity=1
[StormCloudGenerator] GenerateStormSpheres: config=Storm_Column_Light, archetype=Column, seed=12345
[StormCloudGenerator]   density=0.6, cloudSize=100
[StormCloudGenerator] Generated 96 spheres for storm 1
[StormCloudGenerator] Storm 1 spawned successfully with 96 spheres
[StormController] Initialized storm 1 at (0.0, 1200.0, 0.0)
```

### Что должно быть в сцене

В Hierarchy появится новый объект:
```
Storm_1 (без компонентов, просто пустой GameObject)
  └─ SphereContainer (пустой GameObject)
        ├─ StormSphere_0
        ├─ StormSphere_0
        ├─ StormSphere_0
        ├─ StormSphere_0
        ...
        └─ StormSphere_0  (всего ~96 объектов)
```

**Визуально:** Белые сферы в небе на позиции Y=1200.

---

## 5. ЕСЛИ НЕ РАБОТАЕТ — ДИАГНОСТИКА

### Проблема: "Pattern is null"

**Консоль:** `[Test] generator=True, pattern=False`

**Решение:**
1. Выбрать `StormCloudGenerator` в CloudManager
2. В Inspector найти `Default Storm Pattern`
3. Присвоить `Storm_Column_Light` через кружок справа

---

### Проблема: "StormCloudGenerator not found"

**Консоль:** `[Test] StormCloudGenerator not found in scene!`

**Решение:**
1. В Hierarchy убедиться что `StormCloudGenerator` внутри `CloudManager`
2. Если компонент отсутствует — добавить: `CloudManager` → Add Component → `StormCloudGenerator`

---

### Проблема: "No spheres generated!"

**Консоль:**
```
[StormCloudGenerator] GenerateStormSpheres: config=Storm_Column_Light, archetype=Column
[StormCloudGenerator]   density=0.6, cloudSize=100
[StormCloudGenerator] No spheres generated! Pattern: Storm_Column_Light, Archetype: Column
```

**Решение:**
1. Проверить CloudLayerConfig — убедитесь что **Archetype = Column** (не Sphere)
2. Проверить **Column Archetype** параметры:
   - Floors должен быть > 0 (поставьте 8)
   - RingsPerFloor должен быть > 0 (поставьте 6)
   - BaseRadius и TopRadius должны быть > 0

---

### Проблема: Сферы есть, но их НЕ ВИДНО

**Возможные причины:**

1. **Позиция за камерой** — игрок на Y=0, storm на Y=1200, камера смотрит горизонтально
   - Решение: Изменить Test Position на Y=500

2. **Материал белый/прозрачный** — нужен cloudMaterial
   - В StormCloudGenerator → Cloud Material → создать материал с прозрачностью

3. **Маленький scale** — сферы слишком маленькие
   - Проверить в Inspector StormSphere: scale должен быть ~100 (Radius * 2)

4. **Storm неактивен** — StormController.Update() скрывает если далеко
   - Проверить Visibility Distance: 50000
   - Проверить что игрок с тегом "Player" существует

---

### Проблема: StormController.Initialize не вызывается

**Причина:** ServerStormManager посылает ClientRpc, но в **Play Mode без сервера** (Solo mode) ClientRpc не вызывается.

**Решение для одиночного теста:**
- Press **T** — спавн через TestStormSpawner напрямую
- StormController.Initialize вызывается только при сетевой игре (сервер → клиенты)

---

## 6. КАК РАБОТАЕТ PARTING (РАЗЛЁТ СФЕР)

### CloudSpherePhysics

Каждая сфера имеет компонент `CloudSpherePhysics`:

```
Update():
  1. Найти игрока (FindGameObjectWithTag "Player")
  2. Если distance < PartingDistance (30м):
       - Перевести Rigidbody в !isKinematic
       - AddForce в направлении ОТ игрока
  3. Если SpringBack = true:
       - Каждый кадр применять пружину к смещённым сферам
       - Сфера возвращается к исходной позиции
```

### Тест parting

1. Заспавнить storm (T)
2. Подлететь близко к сферам (расстояние < 30м)
3. Сферы должны разлететься в стороны
4. После отдаления (если SpringBack=true) сферы возвращаются

---

## 7. ИТОГОВЫЙ ЧЕКЛИСТ

```
□ Storm_Column_Light создан в Assets/_Project/Art/
□ Archetype = Column
□ ColumnParams заполнены (Floors, Rings, BaseR, TopR)
□ StormCloudGenerator → Default Storm Pattern = Storm_Column_Light
□ TestStormSpawner → Test Pattern = Storm_Column_Light (или пусто)
□ Старые Storm_0...Storm_4 удалены или отключены
□ Play → T → в консоли "Storm X spawned successfully"
□ Визуально: сферы в небе на Y=1200 (или выше)
□ Подлет → сферы разлетаются
```

---

## 8. КЛАВИШИ УПРАВЛЕНИЯ ТЕСТОМ

| Клавиша | Действие |
|---------|----------|
| T | Спавн storm с id=1 по координатам (0, 1200, 0) |
| Y | Деспавн storm с id=1 |
| U | Деспавн ВСЕХ storms |
| Ctrl+Shift+S | Save Project |

---

**Status:** Ready for Testing

---

## 9. PENDING — FUTURE WORK

| Задача | Описание | Status |
|--------|----------|--------|
| Parent mesh pattern | Генерация сфер по поверхности меша | ⏳ Deferred |
| Advanced physics | Collision между сферами, развитие parting | ⏳ Deferred |
| Lightning VFX | ParticleSystem в StormController | ⏳ Deferred |
| Runtime loading | Addressables для CloudLayerConfig | ⏳ Deferred |

**Last Updated:** 24 May 2026