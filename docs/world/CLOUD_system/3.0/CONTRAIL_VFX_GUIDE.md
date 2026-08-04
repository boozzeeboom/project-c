# Contrail VFX — Гайд по настройке

**Дата:** 2026-08-04  
**Ассет:** `Assets/_Project/VFX/Contrail.vfx`  
**Скрипт:** `Assets/_Project/Scripts/Ship/ShipContrailVfx.cs`

---

## 1. Открыть VFX Graph

Window → Visual Effects → Visual Effect Graph → открыть `Contrail.vfx`

Или двойной клик по `Assets/_Project/VFX/Contrail.vfx` в Project window.

---

## 2. Структура графа (Simple_Trail)

```
[Spawning Context]          — частота спавна частиц
    ↓
[Initialize Particle]       — lifetime, size, color, position
    ↓
[Update Particle]           — физика, затухание
    ↓
[Output Particle Strip]     — рендер (quad или strip)
```

---

## 3. Что настраивать

### 3.1 Lifetime (длина следа)

**Где:** блок `Initialize Particle` → `Set Lifetime`

| Размер корабля | Lifetime | Длина следа (при 30 м/с) |
|---|---|---|
| Лёгкий (10-15м) | 1.5-2 с | ~45-60 м |
| Средний (20-30м) | 2.5-3.5 с | ~75-105 м |
| Тяжёлый (40-60м) | 4-6 с | ~120-180 м |

**Как:** В `Initialize` найти `Set Lifetime`, выставить `A = X` (константа или Random).

---

### 3.2 Size (толщина следа)

**Где:** блок `Initialize Particle` → `Set Size`

| Размер корабля | Size |
|---|---|
| Лёгкий | 0.5-1.0 |
| Средний | 1.5-3.0 |
| Тяжёлый | 3.0-6.0 |

**Альтернатива:** Size over Life — в `Update Particle` добавить блок `Size over Life` с кривой: 1.0 → 0.2 (угасает к концу жизни).

---

### 3.3 Color / Gradient (цвет)

Сейчас: синий → оранжевый (дефолт шаблона). Надо заменить.

**Где:** блок `Initialize Particle` → `Set Color` (или в Output → Material)

**Нужный цвет:** белый/светло-серый с плавным угасанием в прозрачность.
- RGB: (255, 252, 248) → (200, 200, 210) с альфой 1.0 → 0.0
- Либо через `Color over Life` в Update: белый → полупрозрачный → 0.

**Как:**
1. Удалить текущий `Set Color` (если есть сине-оранжевый градиент)
2. Добавить `Set Color` → `Gradient`, настроить:
   - 0%: RGBA(1, 1, 0.95, 0.8)
   - 50%: RGBA(0.9, 0.9, 0.95, 0.4)
   - 100%: RGBA(0.8, 0.85, 0.9, 0)

---

### 3.4 Spawn Rate (плотность частиц)

**Где:** `Spawning Context` → `Constant Spawn Rate`

| Размер | Rate |
|---|---|
| Лёгкий | 15-25 |
| Средний | 30-50 |
| Тяжёлый | 50-80 |

---

### 3.5 Texture (текстура частиц)

**Где:** `Output Particle Strip` → `Main Texture`

Текущая: дефолтная (или уже `Cloud_Noise1.png`).  
Нужная: `Cloud_Noise1.png` (`Assets/_Project/Art/Textures/Cloud_Noise1.png`).

Если не назначена — перетащить текстуру в поле `Main Texture` в Output контексте.

---

### 3.6 Blend Mode

**Где:** `Output Particle Strip` → настройки контекста

Должен быть **Alpha** (SrcAlpha / OneMinusSrcAlpha) — мягкое наложение.

---

## 4. Адаптация под разные корабли

### Вариант A: Один граф через Exposed Properties

Добавить exposed-свойства в граф (чёрную панель слева → `+`):

| Имя | Тип | Назначение |
|---|---|---|
| `TrailLifetime` | Float | Длина жизни частиц |
| `TrailSize` | Float | Толщина следа |
| `TrailSpawnRate` | Float | Плотность |
| `TrailColor` | Gradient | Цвет |

Затем в блоках использовать эти свойства вместо констант. C# скрипт сможет задавать их через `Vfx.SetFloat("TrailLifetime", value)`.

### Вариант B: Отдельный .vfx на размер

Создать варианты:
- `Contrail_Light.vfx` (лёгкий корабль)
- `Contrail_Medium.vfx` (средний)
- `Contrail_Heavy.vfx` (тяжёлый)

И назначать нужный в префабе корабля.

**Рекомендация:** начать с Варианта A (exposed properties) — меньше файлов, проще балансировать.

---

## 5. Боковые следы (геометрия корабля)

Скрипт `ShipContrailVfx` обновлён для поддержки нескольких точек спавна:

- **`UseShipBounds`** (bool) — автоматически вычисляет точки по bounding box корабля
- **`TrailWidth`** — ширина размаха для боковых точек (0.5 = половина ширины корабля)
- **`TrailCount`** — сколько точек спавна (1 = центр, 3 = центр + бока, 5 = центр + 2 пары)

Боковые точки располагаются вдоль задней кромки bounding box'а корабля:

```
       ┌─────────┐
       │  SHIP   │
       └─────────┘
         │  │  │
         ▼  ▼  ▼
      L   C   R    ← trail spawn points
```

### Масштабирование

Скрипт читает `ShipController` → `GetComponent<MeshFilter>()` → `mesh.bounds` для автоопределения размера. Для кораблей без MeshFilter можно задать размер вручную через `ManualBoundsSize`.

---

## 6. Быстрый старт (минимальные правки)

Если нужно СЕЙЧАС исправить цвет и толщину:

1. Открыть `Contrail.vfx` в VFX Graph
2. В `Initialize Particle`:
   - Найти сине-оранжевый `Set Color` → удалить
   - Добавить `Set Color` → выбрать белый + Gradient Alpha
3. В `Initialize Particle` → `Set Size`: поставить 2.5
4. В `Initialize Particle` → `Set Lifetime`: поставить 3.5
5. В Output → `Main Texture`: перетащить `Cloud_Noise1.png`
6. Сохранить (Ctrl+S)
