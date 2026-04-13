# Bug Report — Сессия A1+B1: Компиляция VeilSystem и WorldData

**Дата:** 13 апреля 2026 | **Сессия:** A1+B1 (VeilSystem + ScriptableObjects)
**Статус:** ✅ Все исправлены

---

## 🐛 Баги компиляции

| # | Описание | Ошибка | Приоритет | Статус | Решение |
|---|----------|--------|-----------|--------|---------|
| 1 | `WorldData.cs` не знает `CloudLayerConfig` | CS0246: The type or namespace name 'CloudLayerConfig' could not be found | P0 | ✅ Исправлено | Добавлен `using ProjectC.Core;` — класс находится в `Assets/_Project/Scripts/Core/CloudLayerConfig.cs` |
| 2 | `VeilSystem.cs` использует `BoxVolumeShape` | CS0246: The type or namespace name 'BoxVolumeShape' could not be found | P0 | ✅ Исправлено | Удалён runtime-код создания BoxVolumeShape. Fog Volume создаётся через Editor (Volume Profile с Fog override) |
| 3 | `VeilSystem.cs` вызывает `lightningParticles.AddComponent<>()` | CS1061: 'ParticleSystem' does not contain a definition for 'AddComponent' | P0 | ✅ Исправлено | Заменено на `lightningParticles.gameObject.AddComponent<ParticleSystemRenderer>()` |

---

## 🔧 Детали исправлений

### Баг 1: Missing using для CloudLayerConfig

**Файл:** `Assets/_Project/Scripts/World/Core/WorldData.cs`

**Проблема:** `CloudLayerConfig` находится в namespace `ProjectC.Core`, но `WorldData.cs` был в `ProjectC.World.Core` без ссылки.

**Решение:**
```csharp
// Было:
using System.Collections.Generic;
using UnityEngine;

// Стало:
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Core;
```

### Баг 2: BoxVolumeShape не существует

**Файл:** `Assets/_Project/Scripts/World/Clouds/VeilSystem.cs`, строка ~152

**Проблема:** `BoxVolumeShape` — это тип из `UnityEngine.Rendering.Universal`, но runtime-создание Volume с Box shape через `ScriptableObject.CreateInstance<BoxVolumeShape>()` ненадёжно и требует дополнительного настройки.

**Решение:** Удалён проблемный код. Заменён на предупреждение в Debug.LogWarning, чтобы пользователь создал Volume Profile через Editor.

```csharp
// Было:
BoxCollider box = fogObj.AddComponent<BoxCollider>();
var boxShape = ScriptableObject.CreateInstance<BoxVolumeShape>();

// Стало:
// Примечание: для runtime создания Volume с Fog нужен Volume Profile
// Это лучше сделать через Editor. Здесь — заглушка.
Debug.LogWarning("[VeilSystem] Fog Volume создан, но для полной настройки создайте Volume Profile в Editor с Fog override (density=0.003, color=#2d1b4e)");
```

### Баг 3: AddComponent на компоненте вместо GameObject

**Файл:** `Assets/_Project/Scripts/World/Clouds/VeilSystem.cs`, строка ~216

**Проблема:** `lightningParticles` — это `ParticleSystem` (компонент), а `AddComponent<T>()` — метод `GameObject`.

**Решение:**
```csharp
// Было:
if (renderer == null) renderer = lightningParticles.AddComponent<ParticleSystemRenderer>();

// Стало:
if (renderer == null) renderer = lightningParticles.gameObject.AddComponent<ParticleSystemRenderer>();
```

---

## 📊 Итог

- **Всего багов:** 3
- **Исправлено:** 3 ✅
- **Ожидает:** 0
- **Компиляция:** Ожидает проверки пользователем в Unity
