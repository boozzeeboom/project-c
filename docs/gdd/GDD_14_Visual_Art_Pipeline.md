# GDD-14: Visual & Art Pipeline — Project C: The Clouds

**Версия:** 1.1 | **Дата:** 14 июля 2026 г. | **Статус:** ✅ Документировано
**Автор:** Малков Леонид Андреевич

---

## 1. Overview

Визуальная система Project C: The Clouds построена на **Unity 6 URP 17.0.3** с кастомными шейдерами и процедурной генерацией. Стиль — **Sci-Fi + Ghibli**: промышленный дизайн + мягкие облака, градиенты, объёмный свет.

### Ключевые особенности
- **URP Pipeline** — Universal Render Pipeline 17.0.3
- **CloudGhibli.shader** — кастомный URP Unlit (noise + rim glow + vertex displacement) — `Assets/_Project/Art/Shaders/CloudGhibli.shader`
- **ProceduralNoiseGenerator** — FBM noise текстуры 512x512
- **MaterialURPConverter** — авто-конвертация Standard → URP
- **3 слоя облаков** — Upper/Middle/Lower, движение, морфинг
- **Day/Night Cycle** — URP Volume Profiles (Day, Night, Twilight) с Bloom, Color Grading, Temperature Filter
- **Veil System** — Raymarch-шейдеры для Завесы (VeilRaymarch.shader, VeilShader.shader)

---

## 2. Art Style Guide

### Sci-Fi + Ghibli эстетика

| Принцип | Описание | Пример |
|---------|----------|--------|
| **Промышленный дизайн** | Сталь, трубы, гранит, кирпич | Города НП, платформы |
| **Мягкие облака** | Объёмные, светящиеся, градиентные | CloudGhibli.shader |
| **Тёплые закаты** | Розово-оранжевые тона | Цикл дня/ночи |
| **Плавные контуры** | Нет острых углов, обтекаемые формы | Корабли |
| **Градиентная окраска** | Переходы цветов на поверхностях | Корабли, здания |
| **Объёмный свет** | Световые лучи через облака | Освещение |

### Цветовая палитра

| Элемент | Цвет | HEX | Описание |
|---------|------|-----|----------|
| Небо (день) | Голубой | `#87CEEB` | Основной цвет неба |
| Небо (ночь) | Тёмно-синий | `#1a1a2e` | Ночное небо |
| Облака (день) | Белый | `#FFFFFF` | Дневные облака |
| Облака (закат) | Розовый | `#FFB6C1` | Закатные облака |
| Завеса | Тёмно-фиолетовый | `#2d1b4e` | Ядовитый слой |
| Молнии Завесы | Фиолетовый | `#9C27B0` | Электрические разряды |
| Антигравий | Голубой | `#4FC3F7` | Свечение двигателей |
| Металл | Серый | `#78909C` | Конструкции |
| Гранит | Серо-коричневый | `#8D6E63` | Горные пики |
| Кирпич | Красно-коричневый | `#A1887F` | Здания |
| Акцент UI | Голубой | `#4FC3F7` | Кнопки, активные элементы |

---

## 3. URP Pipeline Setup

### Настройки Pipeline

| Параметр | Значение |
|----------|----------|
| Версия URP | 17.0.3 |
| Pipeline Asset | ProjectC_URP (Assets/_Project/Settings/ProjectC_URP.asset) |
| Renderer | ProjectC_URP_Renderer (Assets/_Project/Settings/ProjectC_URP_Renderer.asset) |
| Render Type | Forward |
| Depth Priming | ✅ Не требуется (URP 17+ управляет автоматически) |

### Настройка (критично!)

> **НЕ создавать URP ассеты через C# скрипты!** API отличается.
> 
> Правильный путь:
> 1. Project окно → Create → Rendering → URP Pipeline Asset
> 2. Edit → Project Settings → Graphics → назначить Pipeline Asset
> 3. Edit → Render Pipeline → URP → Upgrade Project Materials to URP Materials

### UniversalRendererData

| Параметр | Описание |
|----------|----------|
| ScriptableRenderer | UniversalRendererData |
| rendererDataList | readonly (не редактируется) |
| Post-processing | Через Volume Profile |

---

## 4. Shader Library

### CloudGhibli.shader — `Assets/_Project/Art/Shaders/CloudGhibli.shader`

| Фича | Описание | Реализация |
|------|----------|-----------|
| **FBM Noise** | Два слоя шума (крупные формы + мелкие детали) | ProceduralNoiseGenerator |
| **Rim Glow** | Свечение по краям (Fresnel) | rimColor, rimPower |
| **Vertex Displacement** | Смещение вершин по noise | morphAmount |
| **Morph** | Анимация формы облаков | _Time-based |
| **URP Unlit** | Без освещения | Unlit includes |
| **Отдельная папка** | Шейдеры в `Assets/_Project/Art/Shaders/` | ✅ |

### Актуальные шейдеры проекта

| Шейдер | Путь | Статус |
|--------|------|--------|
| **CloudGhibli.shader** | `Assets/_Project/Art/Shaders/` | ✅ Реализован |
| **CloudGhibli_OutlineV5.shader** | `Assets/_Project/Art/Shaders/` | ✅ Реализован |
| **DistantCloudHSV.shader** | `Assets/_Project/Shaders/` | ✅ Реализован |
| **Stars.shader** | `Assets/_Project/Shaders/` | ✅ Реализован |
| **VeilRaymarch.shader** | `Assets/_Project/Shaders/` | ✅ Завеса-шейдер (ray marching) |
| **VeilShader.shader** | `Assets/_Project/Shaders/` | ✅ Завеса-шейдер |
| **VeilRaymarchMesh.shader** | `Assets/_Project/Shaders/` | ✅ Меш-шейдер Завесы |
| **TargetOutline.shader** | `Assets/_Project/Shaders/` | ✅ Outline для таргетинга |
| **DistantCloudDebug.shader** | `Assets/_Project/Shaders/` | ✅ Дебаг облаков |

### [🔴 Запланировано] Будущие шейдеры

| Параметр | Тип | Описание |
|----------|-----|----------|
| `_MainTex` | Texture2D | Процедурная noise-текстура |
| `_BaseColor` | Color | Базовый цвет облака |
| `_RimColor` | Color | Цвет свечения (rim) |
| `_RimPower` | Float | Сила свечения |
| `_MorphAmount` | Float | Амплитуда морфинга |
| `_MorphSpeed` | Float | Скорость морфинга |
| `_NoiseScale` | Float | Масштаб шума |

### [🔴 Запланировано] Будущие шейдеры

| Шейдер | Описание | Этап |
|--------|----------|------|
| URP Character | Шейдер персонажа (Mixamo) | Этап 2.5 |
| URP Ship | Шейдер корабля (FBX) | Этап 2.5 |
| Water/Ocean | Вода/Завеса (future) | Этап 3 |
| Building | Шейдер зданий | Этап 2.5 |
| Terrain | Шейдер террас/ферм | Этап 3 |
| Holographic | Голографические элементы СОЛ | Этап 3 |

---

## 5. Material Pipeline

### Конвертация материалов

| Метод | Описание |
|-------|----------|
| **MaterialURPConverter.cs** | Авто-конвертация при запуске |
| **MaterialURPUpgrader.cs** | Editor-скрипт (ProjectC → Upgrade Materials to URP) |

### Соответствие шейдеров

| Old (Standard) | New (URP) | Описание |
|----------------|-----------|----------|
| Standard | Universal Render Pipeline/Lit | Основные материалы |
| Standard (Unlit) | Universal Render Pipeline/Unlit | Облака, светящиеся |
| Standard (Character) | Universal Render Pipeline/Lit (Character) | Персонаж |
| CloudMaterial | CloudGhibli.shader | Кастомный шейдер облаков |

### Текущие материалы

| Материал | Шейдер | Путь | Статус |
|----------|--------|------|--------|
| character_URP.mat | URP/Lit | `Assets/_Project/Materials/Material/` | ✅ |
| CloudMaterial_URP.mat | CloudGhibli | `Assets/_Project/Materials/Material/` | ✅ |
| IslandMaterial.mat | URP/Lit | `Assets/_Project/Art/` | ✅ |
| Material_Cloud_Upper.mat | CloudGhibli | `Assets/_Project/Materials/Clouds/` | ✅ |
| Material_Cloud_Middle.mat | CloudGhibli | `Assets/_Project/Materials/Clouds/` | ✅ |
| Material_Cloud_Lower.mat | CloudGhibli | `Assets/_Project/Materials/Clouds/` | ✅ |
| DistantCloud.mat | DistantCloudHSV | `Assets/_Project/Materials/Clouds/` | ✅ |
| character.mat | Legacy Standard | `Assets/_Project/Materials/Material/` | ⚠️ (конвертируется при запуске) |
| CloudMaterial.mat | Legacy Standard | `Assets/_Project/Materials/Material/` | ⚠️ (конвертируется при запуске) |
| Rock\_*.mat (5 биомов) | URP/Lit | `Assets/_Project/Materials/World/` | ✅ |
| VeilRaymarch.mat | VeilRaymarch | `Assets/_Project/Materials/Clouds/` | ✅ |
| TestMat_OutlineV2.mat | CloudGhibli_OutlineV2 | `Assets/_Project/Materials/Clouds/` | ✅ |

---

## 6. Procedural Generation Art

### ProceduralNoiseGenerator — `Assets/_Project/Scripts/Core/ProceduralNoiseGenerator.cs`

| Параметр | Значение |
|----------|----------|
| Размер текстуры | 512x512 |
| Алгоритм | FBM (Fractal Brownian Motion) |
| Слои шума | 2 (крупные формы + мелкие детали) |
| Кеширование | Да, ClearCache() для перегенерации |
| Noise-текстуры | `Assets/_Project/Art/Textures/Cloud_Noise*.png` |

### Генерация мира — `Assets/_Project/Scripts/Core/WorldGenerator.cs`

| Компонент | Файл | Назначение |
|-----------|------|------------|
| WorldGenerator | `Scripts/Core/WorldGenerator.cs` | Генерация мира с пиками и облаками |
| WorldGenerationSettings | `Scripts/Core/WorldGenerationSettings.cs` | Настройки генерации |
| MountainMeshBuilder | `Scripts/World/Generation/MountainMeshBuilder.cs` | Генерация мешей гор |
| NoiseUtils | `Scripts/World/Generation/NoiseUtils.cs` | Утилиты шума |
| CloudLayerConfig | `Data/Clouds/CloudLayerConfig_Upper.asset` и др. | Конфиги слоёв облаков |
| Biome профили | `Data/World/BiomeProfiles/` | 5 биомов (African, Alaskan, Alpine, Andean, Himalayan) |
| CloudLayerConfig | `Art/CloudLayerConfig.asset` | Конфигурация облаков |

### Облака

| Параметр | Значение |
|----------|----------|
| Слои | 3 (Upper/Middle/Lower) + Distant |
| Форма | Сферы/Planes с морфингом |
| Движение | Скорость по слою через сервер (WindManager) |
| Морфинг | Анимация формы через shader |
| Дальние облака | DistantCloudManager с DistantCloudHSV.shader |
| Конфигурация слоёв | CloudLayerConfig_Lower/Middle/Upper.asset |
| Облачные префабы | `Assets/_Project/Data/Clouds/` |

### Горные пики

| Параметр | Метод |
|----------|-------|
| Форма | Perlin noise конусы (MountainMeshBuilder) |
| Подход | V2: MountainMeshBuilderV2 — процедурная网格 |
| Материал | URP/Lit (Rock\_*.mat по биомам) |
| Биомы | 5 биомов (African, Alaskan, Alpine, Andean, Himalayan) |
| Снег | Snow_Generic.mat для вершин |
| Текстуры гор | Poly Haven CC0 (запланировано) |

---

## 7. Post-Processing Stack

### ✅ URP Volume Profiles — Реализовано

Day/Night цикл использует 3 VolumeProfile через `DayNightController.cs`:

| Профиль | Путь | Назначение |
|---------|------|------------|
| **DayVolumeProfile** | `Assets/_Project/ScriptableObjects/DayNight/Volumes/DayVolumeProfile.asset` | День (Morning, Midday, Evening) |
| **NightVolumeProfile** | `Assets/_Project/ScriptableObjects/DayNight/Volumes/NightVolumeProfile.asset` | Ночь (Twilight, Night) |
| **TwilightVolumeProfile** | `Assets/_Project/ScriptableObjects/DayNight/Volumes/TwilightVolumeProfile.asset` | Переходные состояния |

### Override-параметры в `TimeOfDayPhase` (пофазово)

Каждая фаза цикла (Morning, Midday, Evening, Twilight, Night) переопределяет:

| Параметр | Тип | Описание |
|----------|-----|----------|
| **Bloom Intensity** | Float (0.3 default) | Интенсивность Bloom |
| **Bloom Threshold** | Float (0.8 default) | Порог Bloom |
| **Saturation Offset** | Float | Коррекция насыщенности |
| **Exposure Offset** | Float (EV) | Коррекция экспозиции |
| **Contrast Offset** | Float | Коррекция контраста |
| **Color Tint Overlay** | Color | Цветовая накладка |
| **Temperature Filter** | Bool/Float | Температурный фильтр (по погоде) |

### Система Volume Blending

`DayNightController` использует **Volume Weight** для плавного перехода между профилями:
- `DayVolumeProfile.weight` → `NightVolumeProfile.weight` по времени суток
- `TwilightVolumeProfile` активируется в переходные периоды
- Blend через `useVolumeBlending = true`

### Активные Volume-эффекты в проекте

| Эффект | День | Ночь | Сумерки | Реализация |
|--------|------|------|---------|------------|
| **Bloom** | ✅ Threshold: 0.8, Intensity: 0.3 | ✅ Threshold: 0.7, Intensity: 0.5 | ✅ Threshold: 0.75, Intensity: 0.4 | VolumeProfile override |
| **Tonemapping** | ✅ Mode: ACES | ✅ Mode: ACES | ✅ Mode: ACES | VolumeProfile |
| **Vignette** | ✅ | ✅ | ✅ | VolumeProfile |
| **Color Adjustments** | ✅ Post-Exposure: 0 | ✅ Post-Exposure: -0.5 | ✅ Post-Exposure: -0.2 | Volume + TimeOfDayPhase |
| **Temperature Filter** | ✅ По времени суток | ✅ Холодный тон | ✅ Тёплый тон | TemperatureFilterConfig |
| **Fog** | ✅ Exponential | ✅ Exponential | ✅ Exponential | Render Pipeline + Volume |

### Global Volume

| Параметр | Описание |
|----------|----------|
| Компонент | `Volume` на Global Volume GameObject |
| Mode | Global |
| Profile | Переключается между Day/Night/Twilight через DayNightController |

---

## 8. Asset Pipeline

### Импорт ассетов

| Тип | Формат | Настройки |
|-----|--------|-----------|
| 3D модели | FBX | Read/Write: Off, Generate Colliders: On |
| Текстуры | PNG | sRGB: On, Generate Mip Maps: On |
| Материалы | .mat | URP шейдеры |
| Префабы | .prefab | NetworkObject для сетевых |

### Структура ассетов

```
Assets/_Project/
├── Art/                  # 3D модели, текстуры
│   ├── CloudLayerConfig.asset
│   ├── IslandMaterial.mat
│   ├── Textures/         # Noise-текстуры (Cloud_Noise*.png)
│   └── Shaders/          # CloudGhibli (все варианты Outline)
├── Materials/            # Материалы по категориям
│   ├── Clouds/           # Cloud, Veil, Distant материалы
│   ├── Material/         # character_URP, CloudMaterial_URP
│   ├── Moon/             # Материалы луны
│   ├── Ship/             # Материалы кораблей
│   ├── Skybox/           # Скайбоксы
│   ├── Stars/            # Материалы звёзд
│   └── World/            # Rock_*.mat по биомам, Snow, Ground
├── Shaders/              # Системные шейдеры URP
│   ├── CloudGhibli.shader (ссылка?)
│   ├── DistantCloud*.shader
│   ├── Veil*.shader
│   ├── TargetOutline.shader
│   └── Stars.shader
├── ScriptableObjects/    # Volume Profiles, конфиги
│   └── DayNight/Volumes/  # DayVolumeProfile, NightVolume, TwilightVolume
├── Prefabs/
│   ├── NetworkPlayer.prefab
│   ├── CloudSystem.prefab
│   ├── Npc_Goblin.prefab
│   └── ...
├── Scripts/
│   ├── Core/             # ProceduralNoiseGenerator, WorldGenerator, DayNightController
│   ├── Customisation/    # CharacterCustomisationApplier
│   └── ...
├── Items/                # ScriptableObject предметов
├── Data/                 # CloudLayerConfig, BiomeProfiles, Scene
├── Settings/             # ProjectC_URP.asset, Renderer
└── Volumes/              # DayNight/ (GameObject Volume)
```

### [🔴 Запланировано] Именование ассетов

| Тип | Формат | Пример |
|-----|--------|--------|
| Материалы | M_{Name} | M_CloudGhibli, M_Character |
| Текстуры | T_{Name}_{Type} | T_Mountain_Albedo, T_Mountain_Normal |
| Модели | M_{Name} | M_Ship_Light, M_Character_Male |
| Префабы | PF_{Name} | PF_NetworkPlayer, PF_Chest |
| Шейдеры | SH_{Name} | SH_CloudGhibli |

---

## 9. Character Pipeline

### ✅ Модель персонажа — Реализовано

| Параметр | Описание | Статус |
|----------|----------|--------|
| Источник | Mixamo | ✅ |
| Формат | FBX | ✅ |
| Анимации | Idle, Walk, Run, Jump через AnimatorController | ✅ |
| Шейдер | URP/Lit (Character) через character_URP.mat | ✅ |
| Customisation | CharacterCustomisationApplier + EquipmentVisualApplier | ✅ |
| AnimatorOverrideController | PlayerAnimation_Default.overrideController + PlayerAnimation_Female.overrideController | ✅ |
| Blend Trees | PlayerAnimation.controller с Blend Tree | ✅ |
| Network | NetworkPlayer.prefab с NetworkObject | ✅ |

### Система кастомизации

| Компонент | Путь | Назначение |
|-----------|------|------------|
| CharacterCustomisationApplier | `Scripts/Player/CharacterCustomisationApplier.cs` | Применение кастомизации к модели |
| CharacterEquipmentVisualApplier | `Scripts/Player/CharacterEquipmentVisualApplier.cs` | Визуализация экипировки |
| CustomisationClientState | `Scripts/Customisation/CustomisationClientState.cs` | Состояние кастомизации клиента |
| CustomisationSave | `Scripts/Customisation/CustomisationSave.cs` | Сохранение кастомизации |
| CharacterBodyType | `Scripts/Customisation/CharacterBodyType.cs` | Типы телосложения |
| HairStyleId | `Scripts/Customisation/HairStyleId.cs` | ID причёсок |
| BodyPresetId | `Scripts/Customisation/BodyPresetId.cs` | ID пресетов тела |

### Анимации — Animator System

| Анимация | Контроллер | Описание |
|----------|-----------|----------|
| Idle | PlayerAnimation.controller | Стойка покоя |
| Walk | PlayerAnimation.controller + Blend Tree | Ходьба |
| Run | PlayerAnimation.controller + Blend Tree | Бег |
| Jump | PlayerAnimation.controller | Прыжок |
| Override (Default) | PlayerAnimation_Default.overrideController | Базовая замена анимаций |
| Override (Female) | PlayerAnimation_Female.overrideController | Женская замена анимаций |

---

## 10. Ship Pipeline

### [🔴 Запланировано] Модели кораблей

| Параметр | Описание | Этап |
|----------|----------|------|
| Инструмент | Blender | Этап 2.5 |
| Формат | FBX | Этап 2.5 |
| Полигонаж | 5-8k tri (лёгкий) | Этап 2.5 |
| Форма | Торообразная (лёгкий) | Этап 2.5 |
| Замена | Сфера → FBX модель | Этап 2.5 |

### Компоненты корабля

| Компонент | Описание | Статус |
|-----------|----------|--------|
| Корпус | Основная модель | 🔴 Запланировано |
| Ветровые лопасти | Отдельный меш, вращение | 🔴 Запланировано |
| Антиграв-двигатели | Emissive + Bloom | 🔴 Запланировано |
| Цвет | Градиентная окраска | 🔴 Запланировано |

---

## 11. Particle Systems

### ⚡ Частично реализовано

VFX-система использует **Visual Effect Graph 17.4.0** и стандартные Particle Systems.

| Система | Путь/Компонент | Статус |
|---------|----------------|--------|
| Explosion Impact | `Assets/_Project/Resources/Vfx/PF_VFX_Impact_Explosion.prefab` | ✅ |
| Melee Impact | `Assets/_Project/Resources/Vfx/PF_VFX_Impact_Melee.prefab` | ✅ |
| Muzzle Flash | `Assets/_Project/Resources/Vfx/PF_VFX_MuzzleFlash_Basic.prefab` | ✅ |
| Arrow Projectile | `Assets/_Project/Resources/Vfx/PF_VFX_Projectile_Arrow.prefab` | ✅ |
| ParticleSystemVfxProvider | `Scripts/Skills/Vfx/ParticleSystemVfxProvider.cs` | ✅ |
| CreateVfxPrefabs Editor | `Editor/CreateVfxPrefabs.cs` | ✅ |
| AssignVfxToSkills Editor | `Editor/AssignVfxToSkills.cs` | ✅ |
| Пыль при беге | — | 🔴 |
| Двигатели корабля | — | 🔴 |
| Дождь | — | 🔴 |
| Молнии Завесы | — | 🔴 |
| Посадка/взлёт | — | 🔴 |

---

## 12. Performance Targets

### Целевые показатели

| Параметр | Цель | Текущее состояние |
|----------|------|------------------|
| FPS | 60 | Целевой кадррейт |
| Draw calls | < 2000 | 24 streaming scenes |
| Полигоны | < 500k на сцену | Low-poly стиль |
| Текстуры | 512x512 max | Noise-текстуры 512x512 |
| Облака | 3 слоя + Distant | Оптимизированы через GPU Instancing |
| Пики | Процедурные (MountainMeshBuilderV2) | Low-poly конусы с биомами |
| VFX | Particle + VEG 17.4.0 | Impact/Muzzle/Projectile VFX |

### Оптимизация

| Метод | Описание | Статус |
|-------|----------|--------|
| LOD | BillboardEffect — для дальних объектов | ⚡ Тестируется |
| Occlusion Culling | Отсечение закрытых объектов | 🔴 |
| GPU Instancing | Для облаков | ⚡ Проверяется |
| Object Pooling | Переиспользование объектов | 🔴 |
| Streaming Scenes | 24 сцены 6×4, аддитивная загрузка | ✅ |
| FloatingOriginMP | Сдвиг мира при удалении от центра | ✅ Написан, не развёрнут |

---

## 13. Acceptance Criteria

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 1 | URP Pipeline активен | Project Settings → Graphics | ✅ |
| 2 | CloudGhibli.shader работает | Rim glow на облаках | ✅ |
| 3 | Procedural Noise генерируется | Текстуры 512x512 | ✅ |
| 4 | Материалы URP | Нет розовых материалов | ✅ |
| 5 | Облака двигаются | 3 слоя + Distant, движение | ✅ |
| 6 | Цикл дня/ночи | Смена освещения, Volume Profiles | ✅ |
| 7 | Мир генерируется | Процедурные пики с биомами | ✅ |
| 8 | Post-Processing (Bloom, Vignette, Color Adjustments) | DayVolume, NightVolume, TwilightVolume активны | ✅ |
| 9 | Модель персонажа (Mixamo) | NetworkPlayer.prefab с Customisation | ✅ |
| 10 | Модель корабля (FBX) | [🔴 Запланировано] | 🔴 |
| 11 | Текстуры Poly Haven | [🔴 Запланировано] | 🔴 |
| 12 | Частицы (VFX Impact) | PF_VFX_Impact_* префабы | ⚡ Частично |
| 13 | AnimatorOverrideController | PlayerAnimation_*.overrideController | ✅ |
| 14 | Fog (Завеса) | Exponential fog через Volume | ✅ |

---

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [ART_BIBLE.md](../ART_BIBLE.md) | [UNITY6_URP_SETUP.md](../unity6/UNITY6_URP_SETUP.md)
