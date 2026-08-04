# Phase 2.3 — VFX Contrail (Конденсационные следы)

**Дата:** 2026-08-04  
**Статус:** 🟡 Код завершён, VFX Graph требует ручной настройки  
**План:** `CLOUD_OCEAN_MEDIUM_DETAILED_STEPS_PHASE2_3.md`

---

## Что сделано

### Ассеты

| Файл | Роль |
|---|---|
| `Assets/_Project/VFX/Contrail.vfx` | VFX Graph (Simple_Trail template, 17.5.0) |
| `Assets/_Project/Scripts/Ship/ShipContrailVfx.cs` | C# контроллер трейлов |
| `Assets/_Project/Prefabs/Ships/Ship_Light_root.prefab` | Префаб с ContrailVFX дочерним объектом |

### ShipContrailVfx.cs — возможности

| Фича | Параметр | Описание |
|---|---|---|
| Мульти-точки | `TrailCount` (1–5) | Центр + боковые по геометрии |
| Авторазмер | `UseShipBounds` | Ищет самый большой enabled MeshRenderer → Platform (6×0.9×12) |
| Ширина размаха | `TrailWidth` (0.1–1.5) | Доля от полуширины корабля |
| Глубина | `TrailDepth` (0.5–2) | Смещение назад от центра |
| Масштабирование | `BaseLifetime/Size/SpawnRate` | Умножаются на `sizeScale = shipBounds.z / 15` |
| Анти-отрыв | `StopDelay` (0.4s) | Задержка перед Stop() → плавный конец трейла |
| Условия эмиссии | `MinSpeed` (5 м/с) | + проверка `Ship.IsDocked` |

### GetShipVisualSize()

```
Ship_Light_root
├── (корень: Cube, disabled)  ← ПРОПУЩЕН
├── PilotSeat: Cube 1.1×1.8×1.2
├── Door: Cube 0.2×1.3×1.6
├── Exchanger: Sphere 2.4×0.9×0.8
├── Slot_Engine/*: Cylinder/Cube
└── Platform: Cube 6.0×0.9×12.0  ← ВЫБРАН (наибольший объём)
```

---

## Архитектура рантайма

```
Ship_Light_root                    ← ShipController, Rigidbody
├── Platform (визуал)
├── ContrailVFX                    ← дочерний GO
│   ├── VisualEffect (Contrail.vfx)
│   └── ShipContrailVfx
│       ├── Vfx → self.VisualEffect
│       └── Ship → GetComponentInParent<ShipController>()
├── Contrail_Side1 (runtime)       ← создаётся скриптом
│   └── VisualEffect (Contrail.vfx)
└── Contrail_Side2 (runtime)
    └── VisualEffect (Contrail.vfx)
```

**Жизненный цикл:**
1. `Start()` → определяет размер корабля → вычисляет spawn offsets → создаёт боковые VFX → Stop
2. `Update()` → проверяет скорость/dock → Play/Stop с задержкой → двигает VFX за кораблём
3. `OnDestroy()` → чистит боковые VFX

---

## Анти-отрыв трейла

### Проблема
При замедлении корабля `Stop()` обрывает эмиссию мгновенно. Последние частицы имеют альфу = 1.0 на старте → жёсткий край отрыва.

### Решение (два уровня)

**Уровень 1 — VFX Graph (основной):**
Добавить `Alpha over Life` в `Update Particle`:
```
0%:    alpha = 0    ← плавное появление
15%:   alpha = 1
70%:   alpha = 1
100%:  alpha = 0    ← растворение
```
Каждая частица стартует прозрачной → даже при мгновенном Stop'е край мягкий.

**Уровень 2 — C# (страховка):**
`StopDelay = 0.4s` — после падения скорости ниже `MinSpeed`, скрипт ждёт 0.4с перед `Stop()`. За это время последние частицы успевают пройти фазу fade-in (15% от ~2с жизни = 0.3с).

---

## Что осталось (ручная настройка)

VFX Graph (`Contrail.vfx`) требует однократной ручной правки — см. [`CONTRAIL_VFX_GUIDE.md`](CONTRAIL_VFX_GUIDE.md):

1. **Alpha over Life** — fade-in 0→1, fade-out 1→0
2. **Set Color** — заменить сине-оранжевый на белый градиент
3. **Set Size** → 2.5
4. **Set Lifetime** → 3.5
5. **Main Texture** → `Cloud_Noise1.png`

---

## Коммиты

| Хеш | Описание |
|---|---|
| `ad1f2364` | Создание Contrail.vfx + ShipContrailVfx + префаб |
| `e03e2422` | Мульти-точки спавна + GetShipVisualSize + гайд |
| `c331f17c` | Фикс GetShipVisualSize (ENABLED renderer, не корневой Cube) |
| текущий | StopDelay 0.4s + документация фазы |
