# Контекст для дальнейшей работы — Project C

**Дата:** 13 апреля 2026  
**Ветка:** `qwen-gamestudio-agent-dev`  
**Статус:** Продолжаем работу по плану `WorldPrototype_SessionPlan.md`

---

## 📋 Текущее состояние проекта

### ✅ ЗАВЕРШЕНО:

**Ядро мира:**
- [x] WorldData.asset — главный ассет мира
- [x] 5 MountainMassif.asset — файлы массивов (Himalayan, Alpine, African, Andean, Alaskan)
- [x] 5 BiomeProfile.asset — климатические профили
- [x] 6 AltitudeCorridorData.asset — коридоры высот
- [x] PeakDataFiller — заполнение 29 пиков в массивах
- [x] Rock материалы — 6 материалов (5 rock + 1 snow)

**Облака:**
- [x] CloudSystem.cs — работает
- [x] CloudLayer.cs — работает
- [x] CloudGhibli shader — работает
- [x] CloudLayerConfig ассеты — созданы (B3)
- [x] CloudClimateTinter.cs — климатический тинт (B3)
- [x] CumulonimbusManager.cs — кумуло-дождевые (B2)
- [x] VeilSystem.cs — завеса на Y=1200 (B1)
- [x] Day/night cycle — работает
- ⚠️ Визуал облаков — БЕЛЫЕ СФЕРЫ, требует улучшения

**Навигация:**
- [x] WorldCamera.cs — free-flight camera с телепортацией
- [x] PeakNavigationUI.cs — debug UI для телепортации к пикам

**Документация:**
- [x] docs/world/WorldLandscape_Design.md — полный дизайн ландшафта
- [x] docs/world/Landscape_TechnicalDesign.md — техническая спецификация
- [x] docs/world/WorldPrototype_SessionPlan.md — план 14 сессий
- [x] docs/WORLD_LORE_BOOK.md — лор мира
- [x] docs/gdd/GDD_02_World_Environment.md — GDD (устарел, требует обновления)
- [x] docs/gdd/GDD_24_Narrative_World_Lore.md — narrative lор

---

## ⛔ ЗАКРЫТО (отложено):

**Генерация горных пиков:**
- [x] MountainMeshBuilder.cs — создаёт меши, НО формы ужасные
- [x] MountainMassifBuilder.cs — editor tool, НО масштаб неверный
- [x] MountainMeshBuilderV2.cs — V2 система, НО материалы не работают
- [x] PeakDataScaler.cs — editor utility для размеров

**Проблема:** Пики выглядят как "странные вытянутые пипки", масштаб в 40-60 раз меньше нужного, формы НЕ горные.

**Временное решение:** Scale родителя Mountains = 40-60 (костыль).

**Подробнее:** docs/world/PEAK_GENERATION_SESSIONS_SUMMARY.md

**Облака — визуал:**
- ⚠️ Облака — БЕЛЫЕ СФЕРЫ/ПЛОСКОСТИ, далёкие от Ghibli-стиля
- ⚠️ CloudGhibli.shader требует настройки (noise-текстуры, rim glow)
- ⚠️ Нет объёмных форм — примитивы вместо детализированных мешей
- ✅ Архитектура ярусной генерации работает — может быть взята за основу

**Подробнее:** docs/world/PEAK_GENERATION_SESSIONS_SUMMARY.md §☁️

---

## 🔜 СЛЕДУЮЩИЕ ЗАДАЧИ (по плану WorldPrototype_SessionPlan.md)

### Приоритет P0 (блокируют прототип):

| # | Задача | Трек | Описание | Зависимости |
|---|--------|------|----------|-------------|
| 1 | **B1: VeilSystem** | Облака | Завеса на Y=1200, фиолетовая, с молниями | ✅ ЗАВЕРШЕНО |
| 2 | **B2: Cumulonimbus** | Облака | Вертикальные столбы кумуло-дождевых облаков | ✅ ЗАВЕРШЕНО (с оговорками) |
| 3 | **A5: FarmPlatform** | Ландшафт | Фермерские префабы (террасы, здания, теплицы) | A3 (хребты) |
| 4 | **A6: FixedWorldGenerator** | Ландшафт | Замена процедурного WorldGenerator | A1-A5 |
| 5 | **A7: Городские префабы** | Ландшафт | Архитектура 5 городов | A2 (пики), A4 (LOD) |

### Приоритет P1 (важные, но не блокируют):

| # | Задача | Трек | Описание |
|---|--------|------|----------|
| 6 | **GDD_02 синхронизация** | Документация | Обновить устаревшие данные |
| 7 | **BiomeProfile ассеты** | Ландшафт | Проверить/создать если нет |
| 8 | **Спецификация силуэтов** | Ландшафт | Как игрок опознаёт пики |
| 9 | **Связь ферм с хребтами** | Ландшафт | Конкретные позиции террас |

---

## 🎯 Рекомендуемый порядок (с учётом заблокированных пиков)

Так как пики (A2, A3, A4) заблокированы, **продолжаем с задачами которые НЕ зависят от пиков**:

### ✅ Шаг 1: B1 — VeilSystem — ЗАВЕРШЕНО
**Статус:** ✅ Работает, завеса на Y=1200, молнии, туман, предупреждения

### ✅ Шаг 2: B2 — Cumulonimbus — ЗАВЕРШЕНО (с оговорками)
**Статус:** ⚠️ Техническая основа готова, визуал требует улучшения
**Что сделано:** 4 случайных столба от Y=1200 до Y=9000, фиолетовые молнии
**Проблема:** Столбы НЕ похожи на кумуло-дождевые облака (цилиндр, нет наковальни)
**Документация:** `docs/world/SESSION_B2_Report.md`, `docs/bugs/BUG-0001_Cumulonimbus_Crash.md`

### ✅ Шаг 3: B3 — Обновление слоёв облаков — ЗАВЕРШЕНО
**Статус:** ✅ Код готов, требуется настройка в Unity Editor
**Что сделано:** CloudClimateTinter, 3 CloudLayerConfig ассета, 3 материала, улучшение CloudSystem
**Проблема:** Облака — БЕЛЫЕ СФЕРЫ, архитектура ярусной генерации может быть взята за основу
**Документация:** `docs/world/SESSION_B3_Prompt.md`, `docs/world/SESSION_B3_Report.md`

### Шаг 4: A5 — FarmPlatform
**Почему:** Можно сделать БЕЗ хребтов (A3) — просто префабы
**Что:** Создать FarmPlatform.prefab с террасами, зданиями, теплицами
**Файлы:** FarmPlatform.prefab, TerraceBuilder.cs
**Время:** ~2 часа

### Шаг 5: A7 — Городские префабы
**Почему:** Можно сделать БЕЗ LOD (A4) — просто префабы  
**Что:** Создать архитектурные модули для 5 городов  
**Файлы:** CityBuilding.prefab, Beacon.prefab, Bridge.prefab, etc.  
**Время:** ~2 часа

### Шаг 6: A6 — FixedWorldGenerator
**Почему:** Зависит от A5, A7 — загрузка всего мира  
**Что:** Создать FixedWorldGenerator.cs, заменить WorldGenerator  
**Файлы:** FixedWorldGenerator.cs  
**Время:** ~1.5 часа

---

## 📂 Структура проекта (актуальная)

```
Assets/_Project/
├── Scripts/
│   ├── Core/
│   │   ├── WorldGenerator.cs          ← ЗАМЕНИТЬ на FixedWorldGenerator
│   │   ├── WorldCamera.cs             ← Работает
│   │   └── TestPlatformCreator.cs     ← Работает
│   ├── World/
│   │   ├── Core/
│   │   │   ├── WorldData.cs           ← Работает
│   │   │   ├── WorldDataTypes.cs      ← Работает
│   │   │   ├── MountainMassif.cs      ← Работает
│   │   │   └── BiomeProfile.cs        ← Работает
│   │   └── Generation/
│   │       ├── MountainMeshBuilder.cs      ← ПЛОХО (отложено)
│   │       ├── MountainMeshBuilderV2.cs    ← НЕ РАБОТАЕТ (отложено)
│   │       ├── MountainMeshGenerator.cs    ← V2 (отложено)
│   │       ├── MountainProfile.cs          ← V2 (отложено)
│   │       └── NoiseUtils.cs               ← Работает
│   ├── Editor/
│   │   ├── MountainMassifBuilder.cs        ← ПЛОХО (отложено)
│   │   ├── MountainMassifBuilderV2.cs      ← НЕ РАБОТАЕТ (отложено)
│   │   ├── PeakDataFiller.cs               ← Работает
│   │   ├── PeakDataScaler.cs               ← V2 (отложено)
│   │   ├── RockMaterialCreator.cs          ← Работает
│   │   └── WorldAssetCreator.cs            ← Работает
│   └── UI/
│       └── PeakNavigationUI.cs        ← Работает
├── Data/World/
│   ├── WorldData.asset              ← Работает
│   ├── BiomeProfiles/               ← 5 файлов
│   └── Massifs/                     ← 5 файлов
├── Materials/World/
│   ├── Rock_Himalayan.mat           ← Работает
│   ├── Rock_Alpine.mat              ← Работает
│   ├── Rock_African.mat             ← Работает
│   ├── Rock_Andean.mat              ← Работает
│   ├── Rock_Alaskan.mat             ← Работает
│   └── Snow_Generic.mat             ← Работает
└── Shaders/
    ├── CloudGhibli.shader           ← Работает
    └── VeilShader.shader            ← Работает
```

---

## 🚫 НЕ ТРОГАТЬ (отложенные задачи)

**Генерация пиков:**
- MountainMeshBuilder.cs
- MountainMeshBuilderV2.cs
- MountainMeshGenerator.cs
- MountainProfile.cs
- MountainMassifBuilder.cs
- MountainMassifBuilderV2.cs
- PeakDataScaler.cs

**Почему:** Формы пиков ужасные, масштаб неверный, требуется полная переработка.

**Когда вернёмся:** После завершения основных задач (B1-B6, A5-A7), когда будет работающий прототип мира.

---

## 📝 Заметки по контексту

### Масштаб мира:
- 1 Unity unit = 1 метр (для X,Z — расстояния)
- Расстояния: 1:2000 от реальности (3000-8700 units между пиками)
- Высоты: 1:100 от реальности (Эверест Y=88.48)
- **ПРОБЛЕМА:** Y используется и для worldPosition (scaled), и для mesh generation (реальные метры)

### Текущие пики:
- 29 пиков в 5 массивах
- Координаты XZ правильные
- Координаты Y scaled (88.48, 48.08, etc.)
- meshHeight, baseRadius — явные значения (V2), но НЕ работают правильно

### Облака:
- 3 слоя: Upper (70-90), Middle (40-70), Lower (15-40)
- CloudGhibli shader работает
- Day/night cycle работает
- Завеса: НЕ РЕАЛИЗОВАНА (B1)
- Cumulonimbus: НЕ РЕАЛИЗОВАНЫ (B2)

### Фермы:
- 9 ферм в данных
- Координаты есть
- Префабы: НЕ СОЗДАНЫ (A5)

### Города:
- 5 городов на главных пиках
- Префабы: НЕ СОЗДАНЫ (A7)

---

## 🎯 Ближайшая цель

**Следующая задача: A5 — FarmPlatform**

Создать фермерские префабы (террасы, здания, теплицы) для 9 ферм

**Файл плана:** docs/world/SESSION_A5_Prompt.md

**Завершено:** B1 (VeilSystem) ✅, B2 (Cumulonimbus) ⚠️, B3 (Cloud Layers) ✅
**Следующая задача:** A5 (FarmPlatform) 🚀
