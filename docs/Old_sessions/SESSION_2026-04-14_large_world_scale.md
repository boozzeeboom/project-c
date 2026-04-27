# Сессия: Масштабирование мира XZ ×50 и настройка Unity для больших миров

**Дата:** 14 апреля 2026
**Ветка:** qwen-gamestudio-agent-dev
**Цель:** Адаптация Unity Editor для работы с миром радиусом ~350,000 units

---

## 1. ИЗМЕНЁННЫЕ ФАЙЛЫ

### Код (XZ ×50 масштаб):

| Файл | Изменение |
|------|-----------|
| `Assets/_Project/Scripts/Editor/PeakDataFiller.cs` | Все 29 пиков: XZ координаты ×50, massifRadius ×50 |
| `Assets/_Project/Scripts/Editor/FarmPlacementEditor.cs` | 9 ферм: XZ координаты ×50 |

### Код (Floating Origin система):

| Файл | Изменение |
|------|-----------|
| `Assets/_Project/Scripts/World/Core/FloatingOrigin.cs` | **НОВЫЙ** — предотвращает floating point jitter |
| `Assets/_Project/Scripts/Editor/FloatingOriginSetup.cs` | **НОВЫЙ** — Editor utility для настройки |
| `Assets/_Project/Scripts/Core/WorldCamera.cs` | Добавлена интеграция с FloatingOrigin |
| `Assets/_Project/Scripts/Core/ThirdPersonCamera.cs` | Добавлена интеграция с FloatingOrigin |

### Уже настроено (ПРОВЕРЕНО):

| Параметр | Значение | Файл |
|----------|----------|------|
| Camera Far Clip Plane | 1,000,000 | WorldCamera.cs, ThirdPersonCamera.cs |
| URP Shadow Distance | 500,000 | ProjectC_URP.asset |
| QualitySettings Shadow Distance | 500,000 | QualitySettings.asset (Ultra) |
| LOD Bias | 5.0 | QualitySettings.asset (Ultra) |
| WorldRadius | 350,000 | WorldGenerationSettings.cs |
| Camera Relative Culling | Включено | GraphicsSettings.asset |

---

## 2. ИТОГОВЫЕ МАСШТАБЫ

| Параметр | Было (×1) | Стало (×50) |
|----------|-----------|-------------|
| Радиус мира | ~7,000 | ~350,000 |
| Эверест→Аконкагуа | ~4,680 | ~234,000 |
| Эверест→Денали | ~4,850 | ~242,500 |
| Монблан→Кибо | ~3,550 | ~177,500 |
| Макс. расстояние | ~12,000 | ~550,000 |

**Y координаты НЕ менялись:**
- Города: Y = 48-88 (scaled 1:100)
- Фермы: Y = 2,500-4,500 (метры)

---

## 3. НЕЗАВЕРШЁННАЯ ПРОБЛЕМА

### Проблема: Editor Scene View не перемещается к удалённым пикам

**Симптомы:**
- При нажатии N/B/R (телепортация к пику) — камера остаётся на месте
- Unity Editor показывает предупреждение: "Due to floating-point precision limitations..."
- В Play Mode FloatingOrigin работает, но в Editor Scene View — нет

**Причина:**
- FloatingOrigin работает только в Play Mode (через LateUpdate)
- Editor Scene View Camera — это отдельная камера Unity, не контролируемая нашими скриптами
- При координатах ±260,000 Unity Editor имеет встроенные ограничения навигации

**Текущее состояние:**
- ✅ Play Mode: камера телепортируется, FloatingOrigin сдвигает мир
- ❌ Editor Scene View: невозможно переместиться к дальним пикам для размещения объектов

---

## 4. ИНСТРУКЦИЯ ДЛЯ UNITY

### Генерация мира:
```
Tools → Project C → Fill MountainMassif Peak Data
Tools → Project C → Scale Peak Data (V2)
Tools → Project C → Build All Mountain Meshes (V2)
```

### Управление камерой (Play Mode):
- **WASD** — полёт
- **Mouse** — вращение
- **N/B** — следующий/предыдушчий пик
- **R** — случайный пик
- **H** — возврат на высоту облаков
- **Left Shift** — ускорение (boost)
- **V** — переключить режим полёта

---

## 5. СЛЕДУЮЩИЕ ШАГИ

**Для новой сессии:**
1. Найти решения для навигации в Editor Scene View при координатах >100,000
2. Возможно: SceneView camera scripting, Editor extensions, custom tools
3. Возможно: World Partitioning система (как в Unreal Engine)
4. Возможно: Asset Store пакеты для large-scale world editing

**Промт-файл:** `docs/prompt_editor_navigation_large_world.md`
