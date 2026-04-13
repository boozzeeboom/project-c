# План внедрения Mountain Mesh V2

**Дата:** 13 апреля 2026  
**Статус:** ГОТОВ К ВЫПОЛНЕНИЮ  
**Приоритет:** P0 — блокирует весь ландшафт  
**Ветка:** `qwen-gamestudio-agent-dev`

---

## 📋 Резюме проблемы

После **5 неудачных итераций** текущая система генрации пиков полностью провалилась:
- ❌ Пики в **60 раз меньше** нужного размера
- ❌ Формы — "странные вытянутые пипки", НЕ горы
- ❌ CapsuleCollider = сферы, НЕ горные формы
- ❌ 4 типа форм неразличимы визуально
- ❌ Пользователь использовал scale=60 как костыль

**Корень проблемы:** Cylinder/Ellipsoid/Dome с radiusFactor Lerp + неправильная формула `meshHeight = baseRadius * hRatio`

---

## 🎯 Новое решение (ADR-0001)

### Ключевые инновации:

1. **Power-Law Cone Profile**: `r(h) = R_base * (1 - h/H)^exponent`
   - Tectonic: exponent=1.4 (concave, крутые)
   - Volcanic: exponent=0.65 (convex, пологие)
   - Dome: exponent=0.5 (очень convex)
   - Isolated: exponent=1.1 (массивные плечи)

2. **Shoulder Bulge**: Gaussian bump на mid-height = характерное "горное плечо"

3. **3-Layer Noise**:
   - Large-scale (silhouette): polar FBM
   - Small-scale (surface detail): Cartesian FBM
   - Ridge noise (Tectonic only)

4. **Явные размеры**: meshHeight=400-800, baseRadius=200-500 (НЕ формулы!)

5. **MeshCollider (convex=false)**: точное соответствие формы, упрощённый меш

---

## 📦 Создаваемые файлы

### 1. `MountainProfile.cs` (Runtime)
**Путь:** `Assets/_Project/Scripts/World/Generation/MountainProfile.cs`  
**Назначение:** Mathematical model для горных профилей  
**Содержание:**
- `MountainProfilePreset` class (exponent, shoulderHeight, shoulderAmplitude, noiseParams)
- Presets для Tectonic/Volcanic/Dome/Isolated
- Helper методы для получения профиля по типу

### 2. `MountainMeshGenerator.cs` (Runtime)
**Путь:** `Assets/_Project/Scripts/World/Generation/MountainMeshGenerator.cs`  
**Назначение:** Runtime mesh generation V2  
**Алгоритм:**
1. Power-law cone base mesh (vertices по формуле r(h))
2. Shoulder bulge (Gaussian deformation)
3. Large-scale noise (polar FBM)
4. Small-scale noise (Cartesian FBM)
5. Ridge noise (Tectonic only)
6. Asymmetry deformation
7. Recalculate normals
8. Return mesh

**Методы:**
- `GenerateMountainMesh(MountainProfile profile, float height, float baseRadius, int segments, int rings)`
- `GenerateColliderMesh(...)` — упрощённая версия для MeshCollider

### 3. `MountainMeshBuilderV2.cs` (Runtime)
**Путь:** `Assets/_Project/Scripts/World/Generation/MountainMeshBuilderV2.cs`  
**Назначение:** Runtime component (заменяет MountainMeshBuilder)  
**Функционал:**
- Awake: get MeshFilter, MeshRenderer, MeshCollider
- Start: generate mesh via MountainMeshGenerator
- Assign materials (rock/snow)
- Setup MeshCollider (convex=false, simplified mesh)
- Position at worldPosition.xz, base at Y=0

### 4. `MountainMassifBuilderV2.cs` (Editor)
**Путь:** `Assets/_Project/Scripts/Editor/MountainMassifBuilderV2.cs`  
**Назначение:** Editor tool для построения всех гор  
**Menu:** `Tools → Project C → Build All Mountain Meshes (V2)`  
**Функционал:**
- Clear existing (optional)
- Load all 5 massifs
- For each peak: create GameObject, add MountainMeshBuilderV2, generate mesh
- Assign materials by massif
- Log statistics

### 5. `PeakDataScaler.cs` (Editor)
**Путь:** `Assets/_Project/Scripts/Editor/PeakDataScaler.cs`  
**Назначение:** Editor utility для обновления PeakData dimensions  
**Menu:** `Tools → Project C → Scale Peak Data (V2)`  
**Функционал:**
- Для каждого пика в 5 массивах:
  - Вычислить meshHeight по role/importance
  - Вычислить baseRadius по target h/r ratio
  - Обновить PeakData в asset
- Сохранить assets

---

## 🔧 Изменяемые файлы

### `WorldDataTypes.cs`
**Путь:** `Assets/_Project/Scripts/World/Core/WorldDataTypes.cs`  
**Изменения:**
- Добавить `public float meshHeight = 600f;` в PeakData
- Изменить default `baseRadius` с 100f на 300f (если meshHeight не задан)

---

## 📝 План выполнения (пошагово)

### Шаг 1: Создание MountainProfile.cs
**Время:** ~15 минут  
**Задача:**
- Создать `MountainProfilePreset` class
- Создать presets для 4 типов
- Helper методы

### Шаг 2: Создание MountainMeshGenerator.cs
**Время:** ~30 минут  
**Задача:**
- Power-law cone vertex generation
- Shoulder bulge deformation
- 3-layer noise integration
- Normal calculation
- Collider mesh generation (simplified)

### Шаг 3: Создание MountainMeshBuilderV2.cs
**Время:** ~15 минут  
**Задача:**
- Runtime component
- Mesh generation call
- Material assignment
- MeshCollider setup
- Positioning

### Шаг 4: Создание MountainMassifBuilderV2.cs
**Время:** ~20 минут  
**Задача:**
- Editor window
- Build all peaks
- Material assignment
- Logging

### Шаг 5: Создание PeakDataScaler.cs
**Время:** ~15 минут  
**Задача:**
- Load all 5 massifs
- Compute meshHeight/baseRadius для каждого пика
- Update PeakData assets
- Save

### Шаг 6: Модификация WorldDataTypes.cs
**Время:** ~5 минут  
**Задача:**
- Добавить meshHeight field в PeakData

### Шаг 7: Тестирование в Unity
**Время:** ~10 минут  
**Задача:**
- Открыть Unity
- Run PeakDataScaler
- Run MountainMassifBuilderV2
- Проверить визуально
- Проверить коллайдеры
- Проверить performance

---

## ✅ Критерии успеха

### Минимальные требования:
- [ ] Все 29 пиков построены без ошибок компиляции
- [ ] Визуальный размер: 400-800 units tall (проверить в Scene view)
- [ ] Формы ГОРНЫЕ, НЕ "пипки" (пользователь подтвердит)
- [ ] h/r ratio = 1.5-2.0 для большинства пиков
- [ ] MeshCollider соответствует форме горы

### Идеальный результат:
- [ ] Эверест выглядит как Эверест (двойная вершина с Лхоцзе)
- [ ] Кибо выглядит как вулкан (пологий, с кратером)
- [ ] Денали выглядит как одинокая громада
- [ ] FPS >= 60 в игре
- [ ] Пользователь доволен ✅✅✅

---

## 🚨 Риски и mitigation

| Риск | Вероятность | Влияние | Mitigation |
|------|------------|---------|------------|
| Noise artefacts | Medium | High | Clamp noise amplitude, test frequencies |
| Performance drop | Low | Medium | Simplified collider mesh, LOD |
| Scaling still wrong | Low | Critical | Explicit values (НЕ formulas), user validation |
| Unity API differences | Low | Medium | Test in Unity 6 URP immediately |

---

## 📊 Таблица размеров пиков (V2)

### Himalayan Massif
| Пик | meshHeight | baseRadius | h/r | Тип |
|-----|-----------|-----------|-----|-----|
| Эверест | 750 | 420 | 1.79 | Tectonic |
| Лхоцзе | 620 | 350 | 1.77 | Tectonic |
| Макалу | 580 | 320 | 1.81 | Tectonic |
| Чо-Ойю | 520 | 290 | 1.79 | Tectonic |
| Шишапангма | 480 | 270 | 1.78 | Tectonic |
| Пик Северный | 420 | 240 | 1.75 | Tectonic |
| Пик Южный | 380 | 220 | 1.73 | Tectonic |
| Пик Восточный | 450 | 250 | 1.80 | Tectonic |

### Alpine Massif
| Пик | meshHeight | baseRadius | h/r | Тип |
|-----|-----------|-----------|-----|-----|
| Монблан | 650 | 350 | 1.86 | Tectonic |
| Гранд-Жорасс | 520 | 290 | 1.79 | Tectonic |
| Маттерхорн | 550 | 300 | 1.83 | Tectonic |
| Финстераархорн | 500 | 280 | 1.79 | Tectonic |
| Вайсхорн | 480 | 260 | 1.85 | Tectonic |
| Пик ЮЗ | 400 | 230 | 1.74 | Tectonic |

### African Massif
| Пик | meshHeight | baseRadius | h/r | Тип |
|-----|-----------|-----------|-----|-----|
| Кибо | 550 | 400 | 1.38 | Volcanic |
| Мавензи | 480 | 320 | 1.50 | Volcanic |
| Шира | 420 | 280 | 1.50 | Volcanic |
| Пик Восточный | 380 | 250 | 1.52 | Dome |

### Andean Massif
| Пик | meshHeight | baseRadius | h/r | Тип |
|-----|-----------|-----------|-----|-----|
| Аконкагуа | 720 | 420 | 1.71 | Tectonic |
| Охос-дель-Саладо | 600 | 340 | 1.76 | Tectonic |
| Невадо-Трес-Крусес | 550 | 310 | 1.77 | Tectonic |
| Пик Северный | 480 | 270 | 1.78 | Tectonic |
| Пик Южный | 450 | 250 | 1.80 | Tectonic |
| Пик Западный | 420 | 240 | 1.75 | Tectonic |

### Alaskan Massif
| Пик | meshHeight | baseRadius | h/r | Тип |
|-----|-----------|-----------|-----|-----|
| Денали | 680 | 380 | 1.79 | Isolated |
| Форакер | 520 | 320 | 1.63 | Dome |
| Хантер | 480 | 280 | 1.71 | Tectonic |
| Пик СЗ | 420 | 250 | 1.68 | Dome |
| Пик Восточный | 400 | 230 | 1.74 | Tectonic |

---

## 🎨 Визуальные target'ы

### Tectonic (Everest-style):
```
       /\
      /  \
     / || \    ← Sharp peak, narrow ridges
    / /  \ \
   / /    \ \   ← Steep concave slopes
  /_/      \_\
 ──────────────
```

### Volcanic (Kilimanjaro-style):
```
      ____
    /      \
   /        \   ← Rounded top, crater
  /          \
 /            \  ← Gentle convex slopes
/______________\
```

### Dome (Foraker-style):
```
    ________
  /          \
 /            \  ← Broad, flat-ish top
/              \ ← Very convex
/________________\
```

### Isolated (Denali-style):
```
       /\
      /  \
     /    \      ← Massive shoulders
    /|      |\
   / |      | \   ← Tall, imposing
  /  |      |  \
 /___|______|___\
```

---

## 📝 Заметки по реализации

### Power-Law Cone формула:
```csharp
float normalizedHeight = (float)ring / rings;  // 0..1
float radiusFactor = Mathf.Pow(1f - normalizedHeight, exponent);
float radius = baseRadius * radiusFactor;
```

### Shoulder Bulge формула:
```csharp
float shoulderCenter = 0.35f;  // 35% высоты
float shoulderWidth = 0.2f;
float shoulderAmplitude = 0.15f;  // 15% от радиуса

float gaussian = Mathf.Exp(-Mathf.Pow(normalizedHeight - shoulderCenter, 2f) / (2f * shoulderWidth * shoulderWidth));
float shoulderOffset = gaussian * shoulderAmplitude * baseRadius;
radius += shoulderOffset;
```

### Noise application:
```csharp
// Polar coordinates для large-scale
float angle = Mathf.Atan2(vertex.z, vertex.x);
float noiseAngle = angle + NoiseUtils.FBM(angle * freq, normalizedHeight * freq, ...) * amplitude;

// Cartesian для small-scale
float noiseX = NoiseUtils.FBM(vertex.x * freq, vertex.z * freq, ...) * smallAmplitude;
vertex.x += noiseX;
vertex.z += noiseZ;
```

---

## 🔜 Следующие шаги после V2

После успешного внедрения Mountain Mesh V2:
1. **Ridge generation** (соединить пики хребтами)
2. **Farm terraces** (разместить террасы на хребтах)
3. **LOD system** (3 levels + billboard)
4. **Snow caps** (автоматическое назначение snow material выше snow line)
5. **Pre-baked meshes** (для release, не runtime generation)

---

**ГОТОВ К НАЧАЛУ ВЫПОЛНЕНИЯ.**  
**Приоритет: P0**  
**Ожидаемое время: ~2 часа**
