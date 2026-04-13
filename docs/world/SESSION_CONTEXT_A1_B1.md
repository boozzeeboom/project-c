# Сессия A1+B1: ScriptableObject ассеты + VeilSystem

**Дата:** 13 апреля 2026 г. | **Ветка:** `qwen-gamestudio-agent-dev`
**Треки:** A (Ландшафт) + B (Облака) — параллельный старт
**Оценка времени:** ~3 часа (A1: 1.5ч + B1: 1.5ч)

---

## 🎯 Цель сессии

**A1:** Создать все ScriptableObject ассеты мира — фундамент, на котором строится ВСЁ. Без этих данных ни один другой компонент не работает.

**B1:** Реализовать систему Завесы — визуальную угрозу, которая НЕ зависит от ландшафта и может разрабатываться полностью параллельно.

---

## 📋 Порядок выполнения (СТРОГО)

```
ШАГ 0: Контекст (5 мин)
  ├─ Прочитать ЭТОТ файл
  ├─ Прочитать docs/world/MASTER_PLAN_WorldPrototype.md (секции 0, 4, 5)
  └─ Проверить текущее состояние проекта

ШАГ 1: A1 — ScriptableObject ассеты (~1.5ч)
  ├─ 1.1. Прочитать Landscape_TechnicalDesign.md (структуры данных)
  ├─ 1.2. Прочитать WorldLandscape_Design.md (координаты, высоты)
  ├─ 1.3. Создать WorldData.cs (главный класс)
  ├─ 1.4. Создать MountainMassif.cs
  ├─ 1.5. Создать BiomeProfile.cs
  ├─ 1.6. Создать PeakData, RidgeData, FarmData (inline-классы)
  ├─ 1.7. Создать ScriptableObject ассеты через Editor:
  │     ├─ WorldData.asset
  │     ├─ 5× BiomeProfile (Himalayan, Alpine, African, Andean, Alaskan)
  │     └─ 5× MountainMassif (с заполненными данными из WorldLandscape_Design.md)
  ├─ 1.8. ⚠️ КРИТИЧНО: Обновить 6× AltitudeCorridorData
  │     └─ Global: max = 95.0 (БЫЛО 44.5!) — 4 города выше старого максимума
  ├─ 1.9. Коммит + тег: git tag -a v0.0.18-a1-scriptableobjects -m "A1: ScriptableObject ассеты мира"
  └─ 1.10. Чек-лист готовности A1 (ниже)

ШАГ 2: B1 — VeilSystem (~1.5ч)
  ├─ 2.1. Прочитать WorldLandscape_Design.md §6 (визуальный дизайн завесы)
  ├─ 2.2. Прочитать Landscape_TechnicalDesign.md §4.1 (архитектура VeilSystem)
  ├─ 2.3. Проверить текущую CloudSystem — НЕ ломать существующие облака
  ├─ 2.4. Создать VeilShader.shader (Exponential Fog + Lightning + Depth Fade)
  ├─ 2.5. Создать VeilMaterial.mat
  ├─ 2.6. Создать VeilSystem.cs:
  │     ├─ Плоскость Y=12.0 (20,000 × 20,000)
  │     ├─ URP Exponential Fog (цвет #2d1b4e, плотность 0.003)
  │     ├─ Particle System молний (#b366ff, 1-3 раза/мин)
  │     ├─ Depth Fade — растворение на горизонте
  │     ├─ Box Collider Y=14.0 — зона предупреждения
  │     └─ Под-завесный туман Y=8.0 (плотность 0.01)
  ├─ 2.7. Интеграция с AltitudeCorridorSystem: global min = 12.0 = высота завесы
  ├─ 2.8. Тест: камера спускается к Y=12 — видно завесу, молнии, туман
  ├─ 2.9. Коммит + тег: git tag -a v0.0.18-b1-veil -m "B1: VeilSystem"
  └─ 2.10. Чек-лист готовности B1 (ниже)

ШАГ 3: Финализация
  ├─ Оба трека должны быть закоммичены
  ├─ Обновить docs/world/MASTER_PLAN_WorldPrototype.md — отметить A1, B1 как ✅
  ├─ Создать docs/world/SESSION_A1_B1_Report.md — краткий отчёт сессии
  └─ Подготовить контекст для следующей сессии (A2 + B2)
```

---

## 📚 Обязательные документы для чтения

### Перед началом (обязательно):
| Документ | Что искать |
|----------|-----------|
| `docs/world/MASTER_PLAN_WorldPrototype.md` | Секции 0 (резюме), 4 (связь систем), 5 (тех. рекомендации) |
| `docs/world/WorldLandscape_Design.md` | §2 (масштаб), §3 (координаты 5 массивов), §5 (облака/коридоры), §6 (завеса) |
| `docs/world/Landscape_TechnicalDesign.md` | §1.2-1.7 (структуры ScriptableObject), §4.1 (VeilSystem архитектура) |
| `docs/world/TechnicalDirector_Review.md` | Риск #1 (масштаб/координаты — КРИТИЧНО), Риск #7 (Cumulonimbus) |

### Для справки (по мере необходимости):
| Документ | Когда читать |
|----------|-------------|
| `docs/gdd/GDD_02_World_Environment.md` | §6.5 (Altitude Corridors — текущие значения для сравнения) |
| `docs/WORLD_LORE_BOOK.md` | Раздел «Структура мира» (три уровня: небо/облака/завеса) |
| `docs/gdd/GDD_24_Narrative_World_Lore.md` | §4 (Технологии — для понимания Завесы) |
| `docs/world/UnitySpecialist_Implementation.md` | §1 (URP настройки), §3 (Cloud Shader), §8 (Debug tools) |

---

## 📂 Файлы проекта для работы

### Создать (новые):
```
Assets/_Project/
├── Scripts/
│   └── World/
│       ├── Core/
│       │   ├── WorldData.cs                    # A1: главный ScriptableObject
│       │   ├── MountainMassif.cs               # A1: массив
│       │   └── BiomeProfile.cs                 # A1: климатический профиль
│       └── Clouds/
│           └── VeilSystem.cs                   # B1: система завесы
├── Data/
│   └── World/
│       ├── WorldData.asset                     # A1: создать через Create menu
│       ├── BiomeProfiles/                      # A1: 5 файлов
│       │   ├── Biome_Himalayan.asset
│       │   ├── Biome_Alpine.asset
│       │   ├── Biome_African.asset
│       │   ├── Biome_Andean.asset
│       │   └── Biome_Alaskan.asset
│       └── Massifs/                            # A1: 5 файлов
│           ├── HimalayanMassif.asset
│           ├── AlpineMassif.asset
│           ├── AfricanMassif.asset
│           ├── AndeanMassif.asset
│           └── AlaskanMassif.asset
├── Materials/
│   └── Clouds/
│       └── Veil.mat                            # B1: материал завесы
├── Shaders/
│   └── VeilShader.shader                       # B1: шейдер завесы
└── Prefabs/
    └── World/
        └── VeilSystem.prefab                   # B1: префаб завесы
```

### Изменить (обновить):
| Файл | Что менять | Почему |
|------|-----------|--------|
| `Assets/_Project/Data/Corridors/Corridor_Global.asset` | maxAltitude: 44.5 → **95.0** | 4 города выше старого максимума |
| `Assets/_Project/Data/Corridors/Corridor_Primus.asset` | min: ?, max: ? → min: 41.0, max: 95.0 | Новая высота Примум = 88.48 |
| `Assets/_Project/Data/Corridors/Corridor_Secundus.asset` | min: 30.0, max: 55.0 | Секунд/Монблан Y=48.08 |
| `Assets/_Project/Data/Corridors/Corridor_Kibo.asset` | min: 40.0, max: 65.0 | Кибо/Килиманджаро Y=58.95 |
| `Assets/_Project/Data/Corridors/Corridor_Tertius.asset` | min: 45.0, max: 80.0 | Тертиус/Аконкагуа Y=69.62 |
| `Assets/_Project/Data/Corridors/Corridor_Quartus.asset` | min: 40.0, max: 70.0 | Квартус/Денали Y=61.90 |

### НЕ ТРОГАТЬ:
| Файл | Почему |
|------|--------|
| `Assets/_Project/Scripts/Core/WorldGenerator.cs` | Не удалять! Отключить позже через feature toggle |
| `Assets/_Project/Scripts/Core/CloudSystem.cs` | Работает, не ломать. Обновлять в B3 |
| `Assets/_Project/Scripts/Core/CloudLayer.cs` | Работает, не ломать |
| `Assets/_Project/Shaders/CloudGhibli.shader` | Не менять! Обновлять в B3 (climate tinting) |
| Все `.meta` файлы | НИКОГДА не создавать/редактировать вручную |

---

## ⚠️ Критические точки внимания

### 🔴 P0: Масштаб координат

**Проблема:** 4 из 5 городов ВЫШЕ текущего global max коридора (44.5).

| Город | Y (высота) | Старый max | Новый max |
|-------|-----------|-----------|-----------|
| Примум | **88.48** | 44.5 ❌ | **95.0** ✅ |
| Тертиус | **69.62** | 44.5 ❌ | **95.0** ✅ |
| Кибо | **58.95** | 44.5 ❌ | **95.0** ✅ |
| Квартус | **61.90** | 44.5 ❌ | **95.0** ✅ |
| Секунд | 48.08 | 44.5 ❌ | **95.0** ✅ |

**Действие:** В сессии A1 — обновить ВСЕ 6 AltitudeCorridorData ассетов. Global max: 44.5 → 95.0.

### 🔴 P0: Завеса ≠ обычные облака

Завеса должна **кардинально отличаться** от обычных облаков:

| Свойство | Обычные облака | Завеса |
|----------|---------------|--------|
| Цвет | Белый/серый/голубой | Тёмно-фиолетовый `#2d1b4e` |
| Форма | Мягкие, объёмные, Ghibli | Плоская, однородная, «стена» |
| Шейдер | CloudGhibli (noise + rim glow) | VeilShader (fog + lightning + depth fade) |
| Анимация | Плавное движение, морфинг | Статичная + редкие молнии |
| Прозрачность | Полупрозрачные | Непрозрачная |

**Действие:** В сессии B1 — создать ОТДЕЛЬНЫЙ шейдер, НЕ модифицировать CloudGhibli.

### 🟡 P1: Высота завесы = Global Corridor min

**Связка:** Завеса Y=12.0 должна РАВНЯТЬСЯ global corridor minAltitude=12.0.

Это создаёт автоматическую геймплейную связь: приближение к завесе = предупреждение от системы коридоров.

### 🟡 P1: Фермы внутри облачного слоя

5 из 9 ферм находятся на Y=25-45, а нижний слой облаков = Y=15-40.

**Это фича, не баг.** Фермы «в облаках» — атмосфера. Но облака над фермами должны быть разреженными. Это решится в B3 (CloudDensityModifier).

**Сейчас:** просто убедиться, что координаты ферм корректно записаны в ScriptableObject.

### 🟡 P1: ScriptableObject — НЕ редактировать .asset напрямую

Создавать ТОЛЬКО через Unity Editor:
- Правой кнопкой в Project window → Create → Project C → World Data / Mountain Massif / Biome Profile
- Заполнять через Inspector
- НЕ создавать .asset файлы вручную через write_file!

Классы C# (.cs) — создавать через write_file. Ассеты (.asset) — ТОЛЬКО через Unity.

---

## ✅ Чек-лист готовности A1

- [ ] `WorldData.cs` — класс создан, компилируется
- [ ] `MountainMassif.cs` — класс создан, компилируется
- [ ] `BiomeProfile.cs` — класс создан, компилируется
- [ ] `WorldData.asset` — создан через Unity Create menu
- [ ] 5× `BiomeProfile.asset` — созданы, цвета климата заполнены
- [ ] 5× `MountainMassif.asset` — созданы, пики и хребты заполнены по WorldLandscape_Design.md
- [ ] `Corridor_Global.asset` — maxAltitude обновлён: 44.5 → 95.0
- [ ] 5× городских коридоров — обновлены min/max по данным из WorldLandscape_Design.md §5.3
- [ ] Unity Console: 0 ошибок компиляции
- [ ] Git: коммит + тег `v0.0.18-a1-scriptableobjects`

---

## ✅ Чек-лист готовности B1

- [ ] `VeilShader.shader` — создан, компилируется в URP
- [ ] `VeilMaterial.mat` — создан, использует VeilShader
- [ ] `VeilSystem.cs` — создан, создаёт плоскость Y=12.0
- [ ] URP Exponential Fog — настроен, цвет `#2d1b4e`, плотность 0.003
- [ ] Particle System молний — фиолетовые `#b366ff`, 1-3 раза/мин
- [ ] Depth Fade — завеса растворяется на горизонте
- [ ] Box Collider Y=14.0 — зона предупреждения (триггер)
- [ ] Под-завесный туман Y=8.0 — плотность 0.01
- [ ] Интеграция: global corridor min = 12.0 = высота завесы
- [ ] Тест: камера на Y=15 → видна завеса внизу с молниями
- [ ] Тест: камера на Y=12 → завеса заполняет экран, фиолетовый туман
- [ ] Тест: обычные облака НЕ изменились (CloudSystem работает)
- [ ] Unity Console: 0 ошибок компиляции
- [ ] Git: коммит + тег `v0.0.18-b1-veil`

---

## 🔄 Переход к следующей сессии (A2 + B2)

После завершения A1+B1, контекст для следующей сессии:

**Сессия A2 (MountainMeshBuilder):**
- Вход: ScriptableObject ассеты из A1
- Задача: runtime-генерация горных мешей (4 типа: Tectonic, Volcanic, Dome, Isolated)
- Документ: `docs/world/Landscape_TechnicalDesign.md` §2 (алгоритмы), §2.5 (профили 5 пиков)

**Сессия B2 (Cumulonimbus):**
- Вход: VeilSystem из B1
- Задача: вертикальные столбы кумуло-дождевых облаков (Y=12 → Y=90+)
- Документ: `docs/world/WorldLandscape_Design.md` §5, `docs/world/TechnicalDirector_Review.md` Риск #7

---

## 📝 Шаблон отчёта сессии

После завершения создать `docs/world/SESSION_A1_B1_Report.md`:

```markdown
# Отчёт сессии A1+B1

**Дата:** [дата] | **Время:** [затрачено]

## A1: ScriptableObject ассеты
- ✅ / ❌ [что сделано]
- ⚠️ [проблемы/риски]
- 📊 [статистика: сколько ассетов создано]

## B1: VeilSystem
- ✅ / ❌ [что сделано]
- ⚠️ [проблемы/риски]
- 📊 [статистика]

## Проблемы и решения
| Проблема | Решение | Статус |

## Готовность к A2+B2
- [ ] A1 полностью завершена
- [ ] B1 полностью завершена
- [ ] Все чек-листы пройдены
- [ ] Git: коммиты + теги
```

---

**Статус:** Готов к запуску. Начни с ШАГА 0.
