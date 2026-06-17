# Гайд: Создание правильного тестового корабля

**Дата:** 11 апреля 2026  
**Версия ShipController:** v2.0 (SmoothDamp)

---

## 🎯 Проблема

**Корабль переворачивается от малейшего касания** — это НЕ проблема ShipController, это **проблема физической формы коллайдера**.

Куб 1×1×1:
- ❌ Слишком маленький — центр масс слишком высоко
- ❌ Нет "палубы" — не за что зацепиться при посадке
- ❌ Collider = маленький куб — корабль "качается" на точке
- ❌ Mass = 1kg (дефолт) — слишком лёгкий для баржи

---

## ✅ Правильная Создание Корабля (Пошагово)

### Шаг 1: Создать GameObject корабля

```
Hierarchy → Right Click → 3D Object → Cube
Назвать: "Ship_Test"
```

### Шаг 2: Настроить Transform

**Для лёгкого корабля (баржа):**

| Параметр | X | Y | Z |
|----------|---|---|---|
| **Position** | 0 | 1.5 | 0 |
| **Rotation** | 0 | 0 | 0 |
| **Scale** | **8** | **1.5** | **4** |

> **Почему:** Длина 8м (баржа), высота 1.5м (плоская), ширина 4м (стабильная).

### Шаг 3: Добавить компоненты

#### 3.1 Box Collider (уже есть на кубе)

**Настроить:**
- ✅ **Is Trigger:** ❌ ВЫКЛ (должен быть твёрдым)
- **Center:** (0, 0, 0)
- **Size:** (8, 1.5, 4) — должно совпадать с Scale куба

> **Важно:** Collider должен быть БОЛЬШИМ — это "палуба" баржи. Маленький collider = корабль качается на точке.

#### 3.2 Rigidbody (Add Component → Rigidbody)

**Настроить:**

| Параметр | Значение | Почему |
|----------|----------|--------|
| **Mass** | **1000** | Лёгкая баржа = 1 тонна (не 1kg!) |
| **Drag** | **0** | ShipController сам управляет linearDrag |
| **Angular Drag** | **0** | ShipController сам управляет angularDrag |
| **Use Gravity** | ✅ ВКЛ | Гравитация нужна |
| **Is Kinematic** | ❌ ВЫКЛ | Должен быть динамическим |
| **Interpolate** | **Interpolate** | Плавная интерполяция для NetworkTransform |
| **Collision Detection** | **Discrete** | Обычная (не Continuous) |
| **Constraints → Freeze Position** | Всё ❌ | Не замораживать |
| **Constraints → Freeze Rotation** | Всё ❌ | Не замораживать |

> **Почему Mass = 1000:** ShipController рассчитывает силы с учётом массы. Mass = 1 = корабль слишком лёгкий, переворачивается от ветра.

#### 3.3 ShipController (Add Component → ShipController)

**Настроить:**

| Параметр | Значение |
|----------|----------|
| **Thrust Force** | 350 |
| **Max Speed** | 40 |
| **Yaw Force** | 12 |
| **Pitch Force** | 20 |
| **Vertical Force** | 120 |
| **Yaw Smooth Time** | 0.6 |
| **Pitch Smooth Time** | 0.7 |
| **Lift Smooth Time** | 1.0 |
| **Thrust Smooth Time** | 0.3 |
| **Yaw Decay Time** | 1.0 |
| **Pitch Decay Time** | 0.8 |
| **Anti Gravity** | 1.0 |
| **Linear Drag** | 0.4 |
| **Angular Drag** | 8.0 |
| **Pitch Stab Force** | 15.0 |
| **Roll Stab Force** | 20.0 |
| **Max Pitch Angle** | 20 |
| **Auto Stabilize** | ✅ ВКЛ |
| **Min Altitude** | 1200 |
| **Max Altitude** | 4450 |
| **Max Lift Speed** | 2.5 |
| **Cargo System** | (оставить пустым) |

#### 3.4 NetworkObject (Add Component → NetworkObject)

**Настроить:**

| Параметр | Значение |
|----------|----------|
| **Scene Integration Mode** | **Auto** |
| **Always Replicate** | ❌ ВЫКЛ |
| **Sync Position** | ✅ ВКЛ |
| **Sync Rotation** | ✅ ВКЛ |
| **Sync Scale** | ❌ ВЫКЛ |

#### 3.5 NetworkTransform (Add Component → NetworkTransform)

**Настроить:**

| Параметр | Значение |
|----------|----------|
| **Sync Mode** | **Server Authority** |
| **Sync Position** | ✅ ВКЛ |
| **Sync Rotation** | ✅ ВКЛ |
| **Sync Scale** | ❌ ВЫКЛ |
| **Position Threshold** | 0.001 |
| **Rot Angle Threshold** | 0.01 |
| **Scale Threshold** | 0.01 |
| **Use Unreliable RPC** | ❌ ВЫКЛ |
| **Interpolation** | ✅ ВКЛ |

### Шаг 4: Настроить Tag

```
Inspector → Tag → Add Tag...
→ Создать тег: "Ship"
→ Назначить на Ship_Test
```

### Шаг 5: Создать Platform (платформу для посадки)

```
Hierarchy → Right Click → 3D Object → Cube
Назвать: "Platform_01"
```

**Transform:**

| Параметр | X | Y | Z |
|----------|---|---|---|
| **Position** | 0 | 0 | 0 |
| **Rotation** | 0 | 0 | 0 |
| **Scale** | **20** | **0.5** | **20** |

**Компоненты:**
- ✅ Box Collider (Is Trigger = ❌)
- ❌ НЕ добавлять Rigidbody (статический объект)

> **Почему:** Платформа — твёрдая поверхность. Корабль садится на неё.

### Шаг 6: Поставить корабль на платформу

**Ship_Test Transform:**

| Параметр | Значение |
|----------|----------|
| **Position.Y** | **1.5** | (половина высоты платформы 0.25 + половина высоты корабля 0.75 + небольшой зазор) |

> **Проверка:** Корабль должен СТОЯТЬ на платформе, не парить в воздухе.

---

## 📊 Итоговая Иерархия

```
Hierarchy
├── Ship_Test (Cube)
│   ├── Transform: (0, 1.5, 0) | Scale: (8, 1.5, 4)
│   ├── Box Collider (Size: 8, 1.5, 4)
│   ├── Rigidbody (Mass: 1000, Interpolate)
│   ├── ShipController (все параметры выше)
│   ├── NetworkObject
│   ├── NetworkTransform (Server Authority)
│   └── Tag: "Ship"
│
├── Platform_01 (Cube)
│   ├── Transform: (0, 0, 0) | Scale: (20, 0.5, 20)
│   └── Box Collider (Is Trigger = ❌)
│
└── Player (Capsule)
    └── ... (ваш игрок)
```

---

## 🔧 Проверка Физики

### Тест 1: Корабль стоит на платформе

1. **Play Mode**
2. Корабль должен **стоять** на платформе, не падать
3. **Антигравитация = 1.0** компенсирует гравитацию — корабль "зависает" на месте

**Если падает:**
- ❌ Anti Gravity < 1.0 → поставить 1.0
- ❌ Mass слишком маленький → поставить 1000

### Тест 2: Корабль устойчив

1. **Play Mode**
2. Подождать 5 секунд
3. Корабль **НЕ должен** качаться или вращаться

**Если качается:**
- ❌ Angular Drag слишком маленький → поставить 8.0
- ❌ Roll Stab Force слишком маленький → поставить 20.0

### Тест 3: Посадка работает

1. **Play Mode**
2. Подойти к кораблю (в пешем режиме)
3. Нажать **F** — игрок должен сесть в корабль

**Если не находит корабль:**
- ❌ Тег "Ship" не назначен
- ❌ Корабль слишком далеко (> 5м)

### Тест 4: Управление работает

1. **Сесть в корабль (F)**
2. **W** — корабль плавно разгоняется вперёд
3. **A/D** — медленный поворот (~12°/s)
4. **Мышь Y** — плавный тангаж
5. **Q/E** — очень медленный лифт

**Если корабль переворачивается:**
- ❌ Mass слишком маленький → 1000
- ❌ Angular Drag слишком маленький → 8.0
- ❌ Roll Stab Force слишком маленький → 20.0

---

## ⚠️ Частые Проблемы и Решения

### Проблема: Корабль "плавает" на платформе

**Причина:** Collider слишком маленький или позиция неправильная.

**Решение:**
1. Проверить Scale куба: **(8, 1.5, 4)**
2. Проверить Position.Y: **1.5** (над платформой)
3. Проверить Collider Size: должен совпадать с Scale

### Проблема: Корабль вращается от малейшего касания

**Причина:** Mass слишком маленький или Angular Drag недостаточный.

**Решение:**
1. **Mass → 1000** (не 1!)
2. **Angular Drag → 8.0** (не 0!)
3. **Roll Stab Force → 20.0**

### Проблема: Корабль проваливается сквозь платформу

**Причина:** Collider платформы отсутствует или Is Trigger = ✅.

**Решение:**
1. Проверить что у Platform_01 есть Box Collider
2. **Is Trigger = ❌** (должен быть твёрдым)

### Проблема: Корабль улетает вверх

**Причина:** Anti Gravity > 1.0 или Vertical Force слишком большой.

**Решение:**
1. **Anti Gravity → 1.0** (не больше!)
2. Проверить что никто не шлёт ввод лифта

### Проблема: NetworkTransform не реплицирует

**Причина:** NetworkTransform не в Server Authority mode.

**Решение:**
1. NetworkTransform → **Sync Mode = Server Authority**
2. NetworkObject → **Scene Integration Mode = Auto**

---

## 🎨 Визуализация (как должно выглядеть)

```
        Корабль (Ship_Test)
        ╔══════════════════════════════════════╗
        ║        ← 8 метров →                ║
        ║  ┌────────────────────────────┐      ║
        ║  │                            │ 1.5м ║
        ║  │        ПАЛУБА              │      ║
        ║  │                            │      ║
        ║  └────────────────────────────┘      ║
        ║         ← 4 метра →                  ║
        ╚══════════════════════════════════════╝
                    ↓ (стоит на)
        ╔══════════════════════════════════════╗
        ║        Платформа (Platform_01)       ║
        ║  ┌────────────────────────────┐      ║
        ║  │                            │ 0.5м ║
        ║  │                            │      ║
        ║  └────────────────────────────┘      ║
        ║         ← 20 метров →                ║
        ╚══════════════════════════════════════╝
```

---

## 📝 Чек-лист Перед Тестированием

- [ ] Ship_Test имеет Scale: **(8, 1.5, 4)**
- [ ] Ship_Test имеет Rigidbody: **Mass = 1000**
- [ ] Ship_Test имеет Rigidbody: **Interpolate**
- [ ] Ship_Test имеет ShipController с **Angular Drag = 8.0**
- [ ] Ship_Test имеет ShipController с **Roll Stab Force = 20.0**
- [ ] Ship_Test имеет тег: **"Ship"**
- [ ] Platform_01 имеет Box Collider (**Is Trigger = ❌**)
- [ ] Ship_Test стоит на Platform_01 (Position.Y = **1.5**)
- [ ] NetworkTransform → **Server Authority**
- [ ] NetworkObject → **Scene Integration = Auto**

---

## 🚀 Быстрая Команда (для продвинутых)

Если лень настраивать вручную, можно создать через Editor Script:

```csharp
// Assets/_Project/Editor/CreateTestShip.cs
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ProjectC.Player;

public class CreateTestShip
{
    [MenuItem("Tools/Create Test Ship")]
    public static void Create()
    {
        // Platform
        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Platform_01";
        platform.transform.localScale = new Vector3(20, 0.5f, 20);
        platform.transform.position = Vector3.zero;

        // Ship
        var ship = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ship.name = "Ship_Test";
        ship.transform.localScale = new Vector3(8, 1.5f, 4);
        ship.transform.position = new Vector3(0, 1.5f, 0);
        ship.tag = "Ship";

        // Rigidbody
        var rb = ship.AddComponent<Rigidbody>();
        rb.mass = 1000f;
        rb.drag = 0f;
        rb.angularDrag = 0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // ShipController
        var sc = ship.AddComponent<ShipController>();

        // NetworkObject
        ship.AddComponent<NetworkObject>();

        // NetworkTransform
        var nt = ship.AddComponent<NetworkTransform>();
        nt.SyncMode = NetworkTransform.SyncModeType.Server;

        Selection.activeGameObject = ship;
        Debug.Log("Test ship created!");
    }
}
```

**Использование:**
```
Unity Editor → Tools → Create Test Ship
```

---

*Гайд создан: 11 апреля 2026*  
*Проблема: корабль переворачивался из-за маленького коллайдера и массы*
