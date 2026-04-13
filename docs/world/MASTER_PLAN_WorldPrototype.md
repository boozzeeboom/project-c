# Мастер-план проработки прототипа мира — Project C: The Clouds

**Версия:** 1.0 | **Дата:** 13 апреля 2026 г. | **Статус:** Готов к утверждению
**Авторы:** Creative Director + Technical Director + Unity Specialist
**Назначение:** Единый документ для планирования сессий по проработке прототипа мира

---

## 0. Резюме для быстрого старта

### Что сделано до этой сессии
- Процедурные горные пики (15 штук, случайные)
- 890+ облаков (3 слоя, CloudGhibli.shader)
- AltitudeCorridorSystem (коридоры высот для кораблей)
- ShipController v2.7 (физика, модули, мезий)
- Мультиплеер (Host + Client + Dedicated Server)
- **A1+B1:** ScriptableObject ассеты, VeilSystem ✅
- **A2:** MountainMeshBuilder, 29 пиков, материалы скал ⚠️ (масштаб НЕ работает)
- **B2:** Cumulonimbus ⚠️ (техническая основа, визуал требует улучшения)
- **B3:** Обновление слоёв облаков ✅ (CloudClimateTinter, 3 слоя, Editor-скрипт)

### Что создаёт этот план
**14 сессий** в 2 треках (Ландшафт + Облака), которые превращают процедурный мир в **фиксированный, основанный на реальной географии**:

| Трек | Сессий | Время | Результат |
|------|--------|-------|-----------|
| **A: Ландшафт** | 8 | ~13ч | 5 массивов, 29 пиков ⚠️, 15 хребтов, 9 ферм, 5 городов |
| **B: Облака** | 6 | ~7.5ч | Завеса ✅, кумуло-дождевые ⚠️, 3 слоя ✅, коридоры, гарантии |
| **ИТОГО** | **14** | **~20.5ч** | Прототип мира (A2 требует исправления масштаба, B3 ✅) |

### Критический путь
```
A1 → A2 → A3 → A6 → A8  (Ландшафт: 8ч)
B1 → B2 → B6            (Облака: 4.5ч, параллельно)
```

### Ключевые технические решения (Technical Director)
| Решение | Выбор | Обоснование |
|---------|-------|-------------|
| Global max коридора | Поднять с 44.5 до 95.0 | 4 из 5 городов выше текущего максимума |
| Горы: runtime vs baked | Runtime для прототипа, baked для релиза | Быстрая итерация сейчас, оптимизация потом |
| Коллайдеры пиков | CapsuleCollider, не MeshCollider | 28 MeshCollider = неприемлемо для физики |
| Cumulonimbus | 3 секции (Base/Body/Anvil), Particle System | Один меш 78 units = проблемы с освещением |
| Сеть | Локальная генерация из ScriptableObject | Фиксированный мир = не нужно синхронизировать |
| Миграция | IWorldDataProvider + feature toggle | Безопасный переход без потери текущей сцены |

### Критические риски
| Риск | Уровень | Митигация |
|------|---------|-----------|
| **Масштаб гор НЕ работает** | 🔴 КРИТИЧЕСКИЙ | **A3:** Отладка позиционирования и масштаба. Документация: SESSION_A3_CONTEXT.md |
| Города выше глобального коридора | 🟡 СРЕДНИЙ | Сессия A1: обновить global max до 9500 |
| Масштаб координат: Y в метрах, XZ в 1:2000 | 🟡 СРЕДНИЙ | **Исправлено в A1+B1:** Все Y=реальные метры (8848 НЕ 88.48). Документация обновлена |
| Фермы внутри облачного слоя | 🟡 СРЕДНИЙ | CloudDensityModifier: разреженные облака над фермами |
| Draw calls от 950+ объектов | 🟡 СРЕДНИЙ | GPU Instancing для облаков (Session B3) |
| Горы выглядят одинаково | 🟡 СРЕДНИЙ | Ручные AnimationCurve для 5 главных пиков |

---

## 1. Аудит существующих документов

| Документ | Полнота | Статус |
|----------|---------|--------|
| `docs/world/WorldLandscape_Design.md` | 90% | Готов к реализации |
| `docs/world/Landscape_TechnicalDesign.md` | 95% | Готов к реализации |
| `docs/world/WorldPrototype_SessionPlan.md` | 100% | План сессий (Creative Director) |
| `docs/world/TechnicalDirector_Review.md` | 100% | Технический анализ рисков |
| `docs/world/UnitySpecialist_Implementation.md` | 100% | Практическое руководство Unity |
| `docs/gdd/GDD_02_World_Environment.md` | 60% | Устарел — требует обновления |
| `docs/gdd/GDD_24_Narrative_World_Lore.md` | 70% | Лор-основа есть |
| `docs/WORLD_LORE_BOOK.md` | 95% | Справочник |

---

## 2. Карта сессий с зависимостями

```
ПЕРВАЯ СЕССИЯ (параллельно):
┌─────────────────────────┐    ┌─────────────────────────┐
│ A1: ScriptableObject    │    │ B1: VeilSystem          │
│ WorldData + 5 Massifs   │    │ Завеса: плоскость+fog   │
│ + 5 BiomeProfiles       │    │ + молнии + триггеры     │
│ ~1.5ч                   │    │ ~1.5ч                   │
└───────────┬─────────────┘    └───────────┬─────────────┘
            │                              │
            ▼                              ▼
┌─────────────────────────┐    ┌─────────────────────────┐
│ A2: MountainMeshBuilder │    │ B2: Cumulonimbus        │
│ 4 типа форм, heightmap  │    │ Вертикальные столбы     │
│ профили 5 главных пиков │    │ 3 секции + молнии       │
│ ~2.5ч                   │    │ ~2ч                     │
└───────────┬─────────────┘    └───────────┬─────────────┘
            │                              │
            ▼                              │
┌─────────────────────────┐                │
│ A3: RidgeMeshBuilder    │                │
│ Catmull-Rom spline      │                │
│ 15+ хребтов             │                │
│ ~1.5ч                   │                ▼
└───────┬─────────────────┘    ┌─────────────────────────┐
        │                      │ B3: Обновление слоёв    │
        ▼                      │ CloudLayerConfig update │
┌─────────────────────────┐    │ Climate tinting         │
│ A4: MountainLOD         │    │ ~1ч                     │
│ 3 уровня + billboard    │    └───────────┬─────────────┘
│ ~1ч                     │                │
└───────────┬─────────────┘                │
            │                              ▼
            ▼                    ┌─────────────────────────┐
┌─────────────────────────┐    │ B4: Связь с коридорами  │
│ A5: FarmPlatform        │    │ AltitudeCorridor update │
│ Детализированные префабы│    │ Force pull-up           │
│ + FerryCableSystem      │    │ ~1ч                     │
│ ~2ч                     │    └───────────┬─────────────┘
└───────┬─────────────────┘                │
        │                                  ▼
        ▼                        ┌─────────────────────────┐
┌─────────────────────────┐    │ B5: Невидимость земли   │
│ A7: Городские префабы   │    │ 5 уровней защиты        │
│ Модули 5 городов        │    │ ~0.5ч                   │
│ ~2ч                     │    └───────────┬─────────────┘
└───────┬─────────────────┘                │
        │                                  ▼
        ▼                        ┌─────────────────────────┐
┌─────────────────────────┐    │ B6: Валидация облаков   │
│ A6: FixedWorldGenerator │    │ Чек-лист 6 критериев    │
│ Замена WorldGenerator   │    │ ~1ч                     │
│ ~1.5ч                   │    └─────────────────────────┘
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│ A8: Валидация ландшафта │
│ GDD_02 update           │
│ ~1.5ч                   │
└─────────────────────────┘
```

---

## 3. Детальное описание каждой сессии

### ТРЕК A: ЛАНДШАФТ

#### Сессия A1: ScriptableObject ассеты
**Вход:** WorldLandscape_Design.md (координаты), Landscape_TechnicalDesign.md (структуры)
**Выход:** WorldData.asset, 5× MountainMassif.asset, 5× BiomeProfile.asset, обновление 6× AltitudeCorridorData
**Критично:** Поднять global max коридора с 44.5 до 95.0 (4 города выше текущего максимума)
**Время:** 1.5ч

#### Сессия A2: MountainMeshBuilder
**Вход:** ScriptableObject из A1, алгоритмы из TechnicalDesign §2
**Выход:** MountainMeshBuilder.cs (4 типа: Tectonic, Volcanic, Dome, Isolated), ручные heightmap-профили 5 главных пиков
**Критично:** Без ручных AnimationCurve горы будут неотличимы — это P0
**Время:** 2.5ч

#### Сессия A3: RidgeMeshBuilder
**Вход:** Хребты из WorldLandscape_Design.md §3.1-3.5, меши из A2
**Выход:** RidgeMeshBuilder.cs (Catmull-Rom spline + V-profile), 15+ хребтов
**Время:** 1.5ч

#### Сессия A4: MountainLOD
**Вход:** Меши пиков из A2, хребты из A3
**Выход:** MountainLOD.cs (3 уровня + billboard), LOD0/LOD1/LOD2 меши
**Время:** 1ч

#### Сессия A5: FarmPlatform ⏳ СЛЕДУЮЩАЯ
**Вход:** Хребты из A3, координаты ферм из WorldLandscape_Design.md §4
**Выход:** FarmPlatform.prefab (плита, террасы, здания, теплица, ирригация, посадка), FerryStation.prefab, 9 ферм на хребтах, 8 паромных тросов
**Промт:** docs/world/SESSION_A5_Prompt.md
**Время:** 2ч

#### Сессия A6: FixedWorldGenerator
**Вход:** Все компоненты A1-A5, архитектура из TechnicalDesign §1
**Выход:** FixedWorldGenerator.cs (async загрузка), WorldBridge адаптер (IWorldDataProvider), WorldGenerator.cs отключён
**Время:** 1.5ч

#### Сессия A7: Городские префабы
**Вход:** GDD_02 §4, WORLD_LORE_BOOK.md, координаты городов
**Выход:** Модули зданий (Small, Medium, Platform, Bridge, Beacon), 5 городских наборов, ночная визуализация
**Время:** 2ч

#### Сессия A8: Валидация
**Вход:** Все результаты A1-A7, чек-лист WorldLandscape_Design.md §14
**Выход:** Полёт-тест всех 5 массивов, обновлённый GDD_02, PrototypeValidationReport.md
**Время:** 1.5ч

---

### ТРЕК B: ОБЛАКА

#### Сессия B1: VeilSystem
**Вход:** WorldLandscape_Design.md §6, TechnicalDesign §4.1
**Выход:** VeilSystem.cs (плоскость Y=12, URP Fog, молнии, Depth Fade, триггеры), VeilMaterial.mat
**Критично:** Завеса — визуальная угроза, должна отличаться от обычных облаков
**Время:** 1.5ч

#### Сессия B2: Cumulonimbus
**Вход:** WORLD_LORE_BOOK.md (фиолетовые молнии), WorldLandscape_Design.md §5
**Выход:** CumulonimbusCloud.cs (3 секции: Base/Body/Anvil), молнии (Particle System), 3-5 столбов на мир
**Критично:** Не создавать новый shader — модифицировать CloudGhibli
**Время:** 2ч

#### Сессия B3: Обновление слоёв ✅ ЗАВЕРШЕНО
**Вход:** WorldLandscape_Design.md §5.2, текущие CloudLayerConfig
**Выход:** Обновлённые CloudLayerConfig (Upper 70-90, Middle 40-70, Lower 15-40), CloudClimateTinter, CloudLayerConfigAssetsEditor
**Критично:** GPU Instancing — единственная P0 оптимизация для 950+ draw calls
**Статус:** ✅ Код готов, требуется настройка в Unity Editor. Облака — БЕЛЫЕ СФЕРЫ, архитектура может быть взята за основу.
**Время:** 1ч

#### Сессия B4: Связь с коридорами
**Вход:** WorldLandscape_Design.md §5.3-5.4, AltitudeCorridorData (6 штук)
**Выход:** Обновлённые коридоры (Global min=12, max=95), интеграция VeilSystem ↔ AltitudeCorridorSystem, Force Pull-Up
**Время:** 1ч

#### Сессия B5: Невидимость земли
**Вход:** TechnicalDesign §4.3 (5 уровней защиты)
**Выход:** 5 уровней: завеса → нижние облака → fog → camera cull → gameplay boundary
**Время:** 0.5ч

#### Сессия B6: Валидация
**Вход:** Все результаты B1-B5, чек-лист 6 критериев
**Выход:** CloudSystemValidationReport.md
**Время:** 1ч

---

## 4. Связь систем Ландшафта и Облаков

### 4.1. Высотная связка (единая вертикальная ось)

> ⚠️ **Все высоты в МЕТРАХ** (реальные значения, НЕ scaled units)

```
Y = 9500+  ════════════════════════════════  ВЫСОТНЫЙ КОРИДОР (обновлённый)
            Корабли с высотной модификацией

Y = 9000   ────────────────────────────────  Верхний слой облаков (Upper)
            ════════════════════════════════  Кумуло-дождевые наковальни

Y = 7000   ╔═══════════════════════════════  СНЕЖНЫЕ ВЕРШИНЫ
            ║   Города: Примум (8848), Тертиус (6962)
Y = 6000   ║   Города: Квартус (6190), Кибо (5895)

Y = 4450   ╠═══════════════════════════════  СТАРЫЙ global max (ОБНОВИТЬ до 9500)

Y = 4000-7000║   MIDDLE слой облаков
            ║   Города: Секунд (4808)
Y = 4000   ║   ФЕРМЫ: Y=2500-4500 (некоторые В облаках!)

Y = 1500-4000║   LOWER слой облаков (плотные)
            ║   Фермы: Эверест 110 (Y=3000), Дон 1010 (Y=2500)

Y = 1300   ────────────────────────────────  Зона предупреждения завесы
Y = 1200   ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  ЗАВЕСА (Y=1200, #2d1b4e, молнии)
Y = 800-1200 ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  КРАЕВАЯ ЗОНА
Y = 0-800  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  ПОД ЗАВЕСОЙ (смертельно)
Y = 0      ════════════════════════════════  ПОВЕРХНОСТЬ (скрыта)
```

### 4.2. Геймплейные связи

| Действие игрока | Влияние Ландшафта | Влияние Облаков |
|-----------------|-------------------|-----------------|
| **Полёт Примум → Секунд** | 3100 units, ~21 мин тяжёлым | Средние облака Y=40-70, турбулентность |
| **Спуск к ферме Y=25** | Ферма на хребте, видна посадка | Нижние облака Y=15-40, видимость 50% |
| **Подход к завесе** | Хребты уходят в завесу | Фиолетовый туман, молнии, тряска |
| **Ночной полёт** | Маяки городов — цветные точки | Облака отражают свет маяков |
| **Кумуло-дождевое рядом** | Горы создают подъёмные потоки | Столб Y=12-90, молнии, объезд |

---

## 5. Технические рекомендации (сводка)

### 5.1. URP настройки (приоритетные)
| Параметр | Сейчас | Рекомендация | Причина |
|----------|--------|-------------|---------|
| Shadow Distance | 50 | 500 | Горы на 5000+ units |
| Shadow Cascades | 1 | 4 | Тени хребтов |
| Depth Texture | Off | On | Post-processing, fog |
| GPU Instancing | Off | On для облаков | 950+ draw calls → 50 |

### 5.2. Оптимизация мешей
- **Коллайдеры:** CapsuleCollider на каждом пике (не MeshCollider!)
- **LOD0:** 2048 tris/пик → только для 5 главных пиков. Остальные: 1024 tris
- **LOD1:** 512-768 tris, дистанция 500-2000 units
- **LOD2:** Billboard (2 tris), дистанция 2000+ units
- **Генерация:** Coroutine-based, без GC spikes

### 5.3. Оптимизация облаков
- **GPU Instancing:** Добавить `#pragma multi_compile_instancing` в CloudGhibli.shader
- **Material Property Block:** НЕ использовать (ломает instancing) → vertex color tinting или 5 материалов по климатическим зонам
- **Cumulonimbus:** 3 секции (Base/Body/Anvil), Particle System для молний

### 5.4. Сеть
- Мир фиксированный → каждый клиент генерирует локально из ScriptableObject
- Синхронизировать только: корабли, вагонетки паромов, NPC
- WorldData checksum для верификации версий клиентов

### 5.5. Миграция
- IWorldDataProvider интерфейс
- WorldBridge адаптер (прокси между старым и новым)
- Feature toggle: ScriptableObject-based (не enum)
- WorldGenerator.cs отключить, не удалять

---

## 6. Структура файлов проекта

```
Assets/_Project/
├── Data/
│   └── World/
│       ├── WorldData.asset                    # Главный ассет мира
│       ├── BiomeProfiles/
│       │   ├── Biome_Himalayan.asset
│       │   ├── Biome_Alpine.asset
│       │   ├── Biome_African.asset
│       │   ├── Biome_Andean.asset
│       │   └── Biome_Alaskan.asset
│       ├── Massifs/
│       │   ├── HimalayanMassif.asset
│       │   ├── AlpineMassif.asset
│       │   ├── AfricanMassif.asset
│       │   ├── AndeanMassif.asset
│       │   └── AlaskanMassif.asset
│       └── Corridors/                         # Обновить существующие
│           ├── Corridor_Global.asset
│           ├── Corridor_Primus.asset
│           ├── Corridor_Secundus.asset
│           ├── Corridor_Kibo.asset
│           ├── Corridor_Tertius.asset
│           └── Corridor_Quartus.asset
├── Prefabs/
│   ├── World/
│   │   ├── MountainPeak_LOD0.prefab           # 5 вариантов для главных пиков
│   │   ├── MountainPeak_Generic.prefab        # Для остальных 23 пиков
│   │   ├── RidgeSegment.prefab
│   │   └── MountainBillboard.prefab
│   ├── Farms/
│   │   ├── FarmPlatform.prefab
│   │   ├── FerryStation.prefab
│   │   └── FerryCableCar.prefab
│   └── Cities/
│       ├── Modules/
│       │   ├── Building_Small.prefab
│       │   ├── Building_Medium.prefab
│       │   ├── LandingPlatform.prefab
│       │   ├── Bridge_Prefab.prefab
│       │   └── Beacon.prefab
│       └── CityLayouts/
│           ├── CityLayout_Primus.prefab
│           ├── CityLayout_Secundus.prefab
│           ├── CityLayout_Kibo.prefab
│           ├── CityLayout_Tertius.prefab
│           └── CityLayout_Quartus.prefab
├── Scripts/
│   └── World/
│       ├── Core/
│       │   ├── FixedWorldGenerator.cs         # A6
│       │   ├── WorldData.cs                   # A1
│       │   ├── MountainMassif.cs              # A1
│       │   ├── BiomeProfile.cs                # A1
│       │   ├── IWorldDataProvider.cs          # Миграция
│       │   └── WorldBridge.cs                 # Миграция
│       ├── Generation/
│       │   ├── MountainMeshBuilder.cs         # A2
│       │   ├── RidgeMeshBuilder.cs            # A3
│       │   ├── TerraceBuilder.cs              # A5
│       │   └── MountainLOD.cs                 # A4
│       ├── Farms/
│       │   ├── FarmPlatform.cs                # A5
│       │   └── FerryCableSystem.cs            # A5
│       ├── Debug/
│       │   ├── WorldDebugGizmos.cs            # Ridge gizmos
│       │   ├── AltitudeDebugHUD.cs            # Altitude indicator
│       │   └── FPSCounter.cs
│       └── Clouds/
│           ├── VeilSystem.cs                  # B1
│           ├── CumulonimbusCloud.cs           # B2
│           ├── CloudClimateTinter.cs          # B3
│           └── CorridorVeilIntegration.cs     # B4
├── Materials/
│   ├── World/
│   │   ├── Rock_Himalayan.mat
│   │   ├── Rock_Alpine.mat
│   │   ├── Rock_African.mat
│   │   ├── Rock_Andean.mat
│   │   ├── Rock_Alaskan.mat
│   │   ├── Snow_Generic.mat
│   │   ├── Terrace_Green.mat
│   │   └── AntiGrav_Glow.mat
│   ├── Clouds/
│   │   ├── Cloud_Upper.mat
│   │   ├── Cloud_Middle.mat
│   │   ├── Cloud_Lower.mat
│   │   ├── Cloud_Cumulonimbus_Base.mat
│   │   ├── Cloud_Cumulonimbus_Body.mat
│   │   ├── Cloud_Cumulonimbus_Anvil.mat
│   │   └── Veil.mat
│   └── Cities/
│       ├── City_Primus.mat
│       ├── City_Secundus.mat
│       ├── City_Kibo.mat
│       ├── City_Tertius.mat
│       └── City_Quartus.mat
├── Shaders/
│   ├── CloudGhibli.shader                     # Уже есть, обновить (B3)
│   └── VeilShader.shader                      # B1
└── Scenes/
    └── ProjectC_1.unity                       # Основная сцена (обновить)
```

---

## 7. Критерии готовности прототипа

### Ландшафт (8 критериев)

| # | Критерий | Проверка | Статус |
|---|----------|----------|--------|
| L1 | 5 массивов на правильных позициях | Координаты городов совпадают с WorldLandscape_Design.md | ⏳ |
| L2 | 28 пиков с уникальными силуэтами | Каждый пик опознаваем по форме | ⏳ |
| L3 | 15+ хребтов соединяют пики | Нет «висящих в воздухе» хребтов | ⏳ |
| L4 | 9 фермерских террас на хребтах | Каждая ферма привязана к хребту | ⏳ |
| L5 | 5 городов с архитектурой | Городские модули расставлены | ⏳ |
| L6 | LOD работает на всех объектах | FPS >= 60 при полном облёте | ⏳ |
| L7 | FixedWorldGenerator загружает мир | Загрузка < 2 секунд, без фриз | ⏳ |
| L8 | Паромные тросы между фермами | 8 тросов с catenary curve | ⏳ |

### Облака (6 критериев)

| # | Критерий | Проверка | Статус |
|---|----------|----------|--------|
| C1 | 3 слоя облаков на правильных высотах | Upper 70-90, Middle 40-70, Lower 15-40 | ⏳ |
| C2 | Завеса на Y=12, фиолетовая, с молниями | Визуальная проверка | ⏳ |
| C3 | Кумуло-дождевые столбы видны | 3-5 столбов, молнии внутри | ⏳ |
| C4 | Облака НЕ сливаются с завесой | Разные цвета, формы, шейдеры | ⏳ |
| C5 | Коридоры высот ограничивают полёт | Global min=12, max=95, city corridors работают | ⏳ |
| C6 | Игрок НЕ видит землю | 5 уровней защиты, тест на Y=5 | ⏳ |

---

## 8. Рекомендуемый порядок выполнения

### Однопользовательский режим (последовательно)

| День | Сессии | Время |
|------|--------|-------|
| **День 1** | A1 (1.5ч) → A2 (2.5ч) → B1 (1.5ч) | 5.5ч |
| **День 2** | A3 (1.5ч) → A4 (1ч) → B2 (2ч) | 4.5ч |
| **День 3** | A5 (2ч) → B3 (1ч) → B4 (1ч) | 4ч |
| **День 4** | A6 (1.5ч) → A7 (2ч) → B5 (0.5ч) | 4ч |
| **День 5** | A8 (1.5ч) → B6 (1ч) | 2.5ч |

**Итого:** 5 дней, ~20.5 часов

### Рекомендуемый порядок (приоритизация)

Начать с **критического пути + P0 риски**:

1. **A1** (ScriptableObject) — без данных ничего не работает
2. **B1** (VeilSystem) — параллельно, завеса — ключевая угроза
3. **A2** (MountainMeshBuilder) — горы — основа мира
4. **B2** (Cumulonimbus) — вертикальные облака связаны с завесой
5. **A3** (RidgeMeshBuilder) — хребты связывают пики
6. **B3** (Обновление слоёв) — GPU Instancing критичен для FPS
7. **A4** (MountainLOD) — оптимизация
8. **B4** (Коридоры) — геймплейная связь
9. **A5** (FarmPlatform) — детализация
10. **A6** (FixedWorldGenerator) — сборка всего
11. **A7** (Города) — визуальная идентичность
12. **B5** (Невидимость земли) — финальная проверка
13. **A8** (Валидация ландшафта)
14. **B6** (Валидация облаков)

---

## 9. Риски и митигация

| Риск | Вероятность | Влияние | Митигация | Сессия |
|------|------------|--------|-----------|--------|
| Горы выглядят одинаково | Средняя | Высокое | Ручные AnimationCurve для 5 главных пиков | A2 |
| Завеса видна как плоскость | Высокая | Среднее | Depth fade + horizon blending + curvature | B1 |
| Кумуло-дождевые перекрывают обзор | Средняя | Среднее | Ограничить 3-5 штуками, вдали от маршрутов | B2 |
| FPS падает при полном мире | Низкая | Высокое | LOD2 billboard + frustum culling + GPU instancing | A4, B3 |
| Фермы «висят» без привязки к хребтам | Средняя | Среднее | TerraceBuilder raycast к ridge mesh | A5 |
| Города — generic набор коробок | Высокая | Высокое | Уникальная расстановка модулей для каждого города | A7 |
| Конфликт старого и нового генератора | Средняя | Высокое | IWorldDataProvider + feature toggle | A6 |

---

## 10. Следующие шаги

1. **Утвердить план** — прочитать документы и подтвердить порядок сессий
2. **Начать сессию A1** — создание ScriptableObject ассетов (критический путь)
3. **Параллельно B1** — VeilSystem (завеса)
4. **После каждой сессии** — коммит + тег бэкапа
5. **После A8+B6** — валидация прототипа + обновление GDD_02

---

## 11. Связанные документы

| Документ | Путь | Назначение |
|----------|------|-----------|
| WorldLandscape_Design.md | `docs/world/WorldLandscape_Design.md` | Концептуальный дизайн мира |
| Landscape_TechnicalDesign.md | `docs/world/Landscape_TechnicalDesign.md` | Техническая архитектура |
| WorldPrototype_SessionPlan.md | `docs/world/WorldPrototype_SessionPlan.md` | План сессий (Creative Director) |
| TechnicalDirector_Review.md | `docs/world/TechnicalDirector_Review.md` | Технический анализ рисков |
| UnitySpecialist_Implementation.md | `docs/world/UnitySpecialist_Implementation.md` | Практическое руководство Unity |
| GDD_02_World_Environment.md | `docs/gdd/GDD_02_World_Environment.md` | GDD мира (требует обновления) |
| GDD_24_Narrative_World_Lore.md | `docs/gdd/GDD_24_Narrative_World_Lore.md` | Нарративный лор |
| WORLD_LORE_BOOK.md | `docs/WORLD_LORE_BOOK.md` | Справочник лора |

---

**Статус:** Готов к утверждению. Ожидает решения о порядке выполнения сессий.
