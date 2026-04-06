# GDD-14: Visual & Art Pipeline — Project C: The Clouds

**Версия:** 1.0 | **Дата:** 6 апреля 2026 г. | **Статус:** ✅ Документировано
**Автор:** Qwen Code (Game Studio: @art-director + @unity-shader-specialist)

---

## 1. Overview

Визуальная система Project C: The Clouds построена на **Unity 6 URP 17.4.0** с кастомными шейдерами и процедурной генерацией. Стиль — **Sci-Fi + Ghibli**: промышленный дизайн + мягкие облака, градиенты, объёмный свет.

### Ключевые особенности
- **URP Pipeline** — Universal Render Pipeline 17.4.0
- **CloudGhibli.shader** — кастомный URP Unlit (noise + rim glow + vertex displacement)
- **ProceduralNoiseGenerator** — FBM noise текстуры 512x512
- **MaterialURPConverter** — авто-конвертация Standard → URP
- **3 слоя облаков** — Upper/Middle/Lower, движение, морфинг

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
| Версия URP | 17.4.0 |
| Pipeline Asset | UniversalRenderPipelineAsset |
| Renderer | UniversalRendererData (URP 14+) |
| Render Type | Forward |
| Depth Priming | [🔴 Запланировано] |

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

### CloudGhibli.shader

| Фича | Описание | Реализация |
|------|----------|-----------|
| **FBM Noise** | Два слоя шума (крупные формы + мелкие детали) | ProceduralNoiseGenerator |
| **Rim Glow** | Свечение по краям (Fresnel) | rimColor, rimPower |
| **Vertex Displacement** | Смещение вершин по noise | morphAmount |
| **Morph** | Анимация формы облаков | _Time-based |
| **URP Unlit** | Без освещения | Unlit includes |

### Параметры CloudGhibli

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

| Материал | Шейдер | Статус |
|----------|--------|--------|
| character_URP.mat | URP/Lit | ✅ |
| CloudMaterial_URP.mat | CloudGhibli | ✅ |
| IslandMaterial.mat | URP/Lit | ✅ |
| character.mat | Legacy Standard | ⚠️ (конвертируется при запуске) |
| CloudMaterial.mat | Legacy Standard | ⚠️ (конвертируется при запуске) |

---

## 6. Procedural Generation Art

### ProceduralNoiseGenerator

| Параметр | Значение |
|----------|----------|
| Размер текстуры | 512x512 |
| Алгоритм | FBM (Fractal Brownian Motion) |
| Слои шума | 2 (крупные формы + мелкие детали) |
| Кеширование | Да, ClearCache() для перегенерации |

### Облака

| Параметр | Значение |
|----------|----------|
| Слои | 3 (Upper/Middle/Lower) |
| Общее количество | 890+ |
| Форма | Сферы/Planes |
| Движение | Скорость по слою |
| Морфинг | Анимация формы через shader |

### Горные пики

| Параметр | Метод |
|----------|-------|
| Форма | Perlin noise конусы |
| Распределение | Золотой угол (137.5°) |
| Материал | URP/Lit (серый камень) |
| [🔴 Запланировано] Текстуры | Poly Haven CC0 |

---

## 7. Post-Processing Stack

### [🔴 Запланировано] URP Volume Profile

| Эффект | Настройки | Этап |
|--------|-----------|------|
| **Bloom** | Threshold: 0.9, Intensity: 0.5 | Этап 2.5 |
| **Tonemapping** | Mode: ACE | Этап 2.5 |
| **Vignette** | Intensity: 0.3, Smoothness: 0.5 | Этап 2.5 |
| **Film Grain** | Intensity: 0.1 | Этап 2.5 |
| **Chromatic Aberration** | Intensity: 0.05 | Этап 2.5 |
| **Color Grading** | Temperature, Tint | Этап 2.5 |
| **Fog** | Exponential, Color: `#2d1b4e` (Завеса) | Этап 2.5 |

### DefaultVolumeProfile

| Параметр | Описание |
|----------|----------|
| Файл | DefaultVolumeProfile.asset |
| Назначение | Базовые настройки post-processing |
| Global Volume | [🔴 Запланировано] |

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
├── Art/              # 3D модели, текстуры
│   └── IslandMaterial.mat
├── Material/         # Материалы
│   ├── character_URP.mat
│   ├── CloudMaterial_URP.mat
│   ├── character.mat (legacy)
│   └── CloudMaterial.mat (legacy)
├── Shaders/          # Шейдеры
│   └── CloudGhibli.shader
├── Prefabs/          # Префабы
│   ├── NetworkPlayer.prefab
│   ├── CloudSystem.prefab
│   └── ...
├── Items/            # ScriptableObject предметов
└── Settings/         # Настройки (WorldGenerationSettings, CloudLayerConfig)
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

### [🔴 Запланировано] Модель персонажа

| Параметр | Описание | Этап |
|----------|----------|------|
| Источник | Mixamo | Этап 2.5 |
| Формат | FBX | Этап 2.5 |
| Анимации | Idle, Walk, Run, Jump | Этап 2.5 |
| Шейдер | URP/Lit (Character) | Этап 2.5 |
| Замена | Capsule → Mixamo модель | Этап 2.5 |

### Анимации

| Анимация | Источник | Описание |
|----------|----------|----------|
| Idle | Mixamo | Стойка покоя |
| Walk | Mixamo | Ходьба |
| Run | Mixamo | Бег |
| Jump | Mixamo | Прыжок |

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

### [🔴 Запланировано] Частицы

| Система | Описание | Этап |
|---------|----------|------|
| Пыль при беге | Частицы при беге по земле | Этап 2.5 |
| Двигатели корабля | Голубое свечение `#4FC3F7` | Этап 2.5 |
| Дождь | URP Particles | Этап 2.5 |
| Молнии Завесы | Фиолетовые вспышки | Этап 3 |
| Посадка/взлёт | Пыль/пар при посадке | Этап 3 |

---

## 12. Performance Targets

### Целевые показатели

| Параметр | Цель | Описание |
|----------|------|----------|
| FPS | 60 | Целевой кадррейт |
| Draw calls | < 2000 | Общее количество |
| Полигоны | < 500k | Общее количество на сцене |
| Текстуры | 512x512 max | Размер текстур |
| Облака | 890+ | Оптимизированы |
| Пики | 15 | Low-poly конусы |

### [🔴 Запланировано] Оптимизация

| Метод | Описание |
|-------|----------|
| LOD | Уровни детализации |
| Occlusion Culling | Отсечение закрытых объектов |
| GPU Instancing | Для облаков |
| Object Pooling | Переиспользование |

---

## 13. Acceptance Criteria

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 1 | URP Pipeline активен | Project Settings → Graphics | ✅ |
| 2 | CloudGhibli.shader работает | Rim glow на облаках | ✅ |
| 3 | Procedural Noise генерируется | Текстуры 512x512 | ✅ |
| 4 | Материалы URP | Нет розовых материалов | ✅ |
| 5 | Облака двигаются | 3 слоя, движение | ✅ |
| 6 | Цикл дня/ночи | Смена освещения | ✅ |
| 7 | Мир генерируется | 15 пиков, 890+ облаков | ✅ |
| 8 | Post-Processing | [🔴 Запланировано] | 🔴 |
| 9 | Модель персонажа (Mixamo) | [🔴 Запланировано] | 🔴 |
| 10 | Модель корабля (FBX) | [🔴 Запланировано] | 🔴 |
| 11 | Текстуры Poly Haven | [🔴 Запланировано] | 🔴 |
| 12 | Частицы (пыль, двигатели) | [🔴 Запланировано] | 🔴 |
| 13 | Bloom, Vignette | [🔴 Запланировано] | 🔴 |
| 14 | Fog (Завеса) | [🔴 Запланировано] | 🔴 |

---

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [ART_BIBLE.md](../ART_BIBLE.md) | [UNITY6_URP_SETUP.md](../unity6/UNITY6_URP_SETUP.md)
