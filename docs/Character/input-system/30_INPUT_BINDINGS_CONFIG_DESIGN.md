# InputBindingsConfig — дизайн ScriptableObject

> **Дата:** 2026-06-28
> **Цель:** единый источник правды для биндов боевых навыков с настройкой через инспектор (MVP) → UI-меню (Phase 2).

---

## 1. Архитектура

```
┌─────────────────────────────────┐
│  InputBindingsConfig (SO)       │  ← дефолты через инспектор
│  Assets/_Project/Resources/     │
│  InputBindingsConfig.asset      │
└────────────┬────────────────────┘
             │ Resources.Load
             ↓
┌─────────────────────────────────┐
│  InputBindingsRuntime           │  ← runtime singleton (in-memory)
│  - Загружается при StartHost    │  - Может быть изменён через UI
│  - Subscribers подписываются    │  - Save в PlayerPrefs (Phase 2)
│    на изменения (Phase 2)       │
└────────────┬────────────────────┘
             │ Dictionary<SkillInputSlot, InputBinding>
             ↓
┌─────────────────────────────────┐
│  SkillInputService              │
│  - Update() polls InputBindings │  ← минимальная инвазия
│  - TryActivate(slot) on match   │
└─────────────────────────────────┘
```

**MVP (Phase 1):**
- Только `InputBindingsConfig` SO с дефолтами.
- `SkillInputService` читает из него при инициализации.
- Изменение = перекомпиляция.

**Phase 2 (будущее):**
- `InputBindingsRuntime` — in-memory копия.
- `Settings UI` — UI Toolkit окно с rebind.
- `PlayerPrefs` — сохранение между сессиями.

---

## 2. InputBindingsConfig.cs — минимальный каркас

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectC.Skills;
using System.Collections.Generic;

namespace ProjectC.Input
{
    /// <summary>
    /// ScriptableObject с дефолтными биндами боевых навыков.
    /// MVP: настраивается через инспектор. Phase 2: через UI-меню.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectC/Input/InputBindingsConfig", fileName = "InputBindingsConfig")]
    public class InputBindingsConfig : ScriptableObject
    {
        [System.Serializable]
        public struct SkillKeyBinding
        {
            public SkillInputSlot slot;
            public MouseButton mouseButton;       // 0=none, 1=left, 2=right, 3=middle
            public Key modifier;                   // UnityEngine.InputSystem.Key.None / LeftCtrl / LeftShift
            public Key fallbackKey;                // опционально (например K для Primary)
            [Tooltip("Имя для UI (напр. 'ЛКМ', 'Ctrl+ЛКМ')")]
            public string displayName;
        }

        [Header("Боевые навыки (Q-INP-02)")]
        [Tooltip("6 биндов: ЛКМ, ПКМ, Ctrl+ЛКМ, Ctrl+ПКМ, Shift+ЛКМ, Shift+ПКМ")]
        public List<SkillKeyBinding> combatSkills = new List<SkillKeyBinding>
        {
            new SkillKeyBinding { slot = SkillInputSlot.Primary,   mouseButton = MouseButton.LeftMouse,  modifier = Key.None,       fallbackKey = Key.K, displayName = "ЛКМ" },
            new SkillKeyBinding { slot = SkillInputSlot.Secondary, mouseButton = MouseButton.RightMouse, modifier = Key.None,       fallbackKey = Key.None, displayName = "ПКМ" },
            new SkillKeyBinding { slot = SkillInputSlot.Slot1,     mouseButton = MouseButton.LeftMouse,  modifier = Key.LeftCtrl,  fallbackKey = Key.None, displayName = "Ctrl+ЛКМ" },
            new SkillKeyBinding { slot = SkillInputSlot.Slot2,     mouseButton = MouseButton.RightMouse, modifier = Key.LeftCtrl,  fallbackKey = Key.None, displayName = "Ctrl+ПКМ" },
            new SkillKeyBinding { slot = SkillInputSlot.Slot3,     mouseButton = MouseButton.LeftMouse,  modifier = Key.LeftShift, fallbackKey = Key.None, displayName = "Shift+ЛКМ" },
            new SkillKeyBinding { slot = SkillInputSlot.Slot4,     mouseButton = MouseButton.RightMouse, modifier = Key.LeftShift, fallbackKey = Key.None, displayName = "Shift+ПКМ" },
        };
    }
}
```

---

## 3. Почему ЭТО работает

1. **Не хардкод.** Меняется через инспектор → asset перезагружается → SkillInputService перечитывает.
2. **Не ломает старое.** Никаких правок в NetworkPlayer.Update, PlayerInputReader, UI-окнах, Esc-handlers.
3. **Минимальная инвазия.** Только SkillInputService получает `Update()`, который polls бинды.
4. **Расширяемо.** Phase 2 можно добавить UI-меню: `Settings UI` → меняет runtime-копию → оповещает SkillInputService.

---

## 4. Где будет жить polling

```csharp
// SkillInputService.cs — добавить Update()
private void Update()
{
    if (!IsOwnerReady()) return;
    var cfg = InputBindingsRuntime.Instance?.Config;
    if (cfg == null) return;

    foreach (var binding in cfg.combatSkills)
    {
        if (IsBindingPressed(binding))
        {
            TryActivate(binding.slot);
            break;  // один бинд за кадр (приоритет: первый матч)
        }
    }
}

private bool IsBindingPressed(SkillKeyBinding b)
{
    var mouse = Mouse.current;
    var kb = Keyboard.current;

    // Проверка модификатора (если задан — должен быть зажат)
    if (b.modifier != Key.None && kb != null && !kb[b.modifier].isPressed) return false;

    // Проверка мыши
    if (b.mouseButton != MouseButton.None && mouse != null)
    {
        switch (b.mouseButton)
        {
            case MouseButton.LeftMouse:  if (mouse.leftButton.wasPressedThisFrame)  return true; break;
            case MouseButton.RightMouse: if (mouse.rightButton.wasPressedThisFrame) return true; break;
            case MouseButton.MiddleMouse: if (mouse.middleButton.wasPressedThisFrame) return true; break;
        }
    }

    // Проверка fallback-клавиши
    if (b.fallbackKey != Key.None && kb != null && kb[b.fallbackKey].wasPressedThisFrame) return true;

    return false;
}
```

**Примечание:** `break` после первого матча важен — иначе при нажатии ЛКМ сработают И Primary, И Slot1 (потому что ЛКМ нажата и без модификатора тоже). Нужно уточнить: приоритет — **первый матч в списке** (Primary идёт первым → ЛКМ без модификатора → матч → break).

---

## 5. Defaults (.asset)

Файл: `Assets/_Project/Resources/InputBindingsConfig.asset`

Создаётся через меню: **Create → ProjectC → Input → InputBindingsConfig**

Содержит 6 записей `combatSkills` (см. default `List<>` в коде выше).

---

## 6. Phase 2 (не входит в MVP)

- `InputBindingsRuntime.cs` — in-memory singleton с авто-сохранением в PlayerPrefs
- `RebindUI.cs` — UI Toolkit окно с listview биндов, click → listen next key
- `InputActionAsset` — переход на полноценные Input Actions (опционально, рекомендуется если UI rebind появится)

---

## 7. Защита от регрессий

| Регрессия | Защита |
|-----------|--------|
| ЛКМ триггерит несколько скиллов | `break` после первого матча в Update |
| Shift+ЛКМ при беге триггерит Skill3 | Проверить: на бегу Shift зажат → Slot3 матчит? Нужно условие "Shift нажат **в этом кадре**" + ЛКМ. Решается через `wasPressedThisFrame` для Shift, или флаг "бежит ли сейчас". |
| Изменение .asset в инспекторе во время игры | `OnValidate()` в SO → оповестить runtime (Phase 2). MVP: изменения только до StartHost. |
| Конфликт с Input.GetAxis (legacy) | InputBindingsConfig использует только `Keyboard.current` / `Mouse.current` (новый API). Legacy не задействован. |
