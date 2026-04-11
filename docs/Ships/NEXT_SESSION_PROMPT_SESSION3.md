# ПРОМТ ДЛЯ СЕССИИ 3: Исправление Сессии 2 + Wind & Turbulence

**Дата:** 12 апреля 2026
**Проект:** Project C — MMO/Co-Op авиасимулятор над облаками
**Ветка:** `qwen-gamestudio-agent-dev`
**Текущий тег:** `backup-2_session-ship-improved`

---

## 📋 КРАТКАЯ ИНСТРУКЦИЯ ДЛЯ НОВОЙ СЕССИИ

Продолжай работу над **Project C**. Сессия 2 завершена ✅ но есть проблемы.

**Задача Сессии 3:**
1. 🔴 **Починить AltitudeUI HUD** — элементы создаются но не видны
2. 🔴 **Усилить турбулентность** — тряска недостаточная при DangerLower
3. 🟡 **Городские коридоры → BoxCollider-триггеры** вместо радиуса
4. 🟡 **Деградация → реальные модификаторы** в ShipController

---

## 📂 Ключевые Документы (ЧИТАТЬ В ЭТОМ ПОРЯДКЕ)

### 1. Начни отсюда:
| Документ | Путь | Зачем |
|----------|------|-------|
| **Сессия 2: Отчёт** | `docs/Ships/SESSION_2_COMPLETE.md` | Что реализовано, что НЕ работает |
| **QWEN_CONTEXT.md (секция Сессии 2)** | `docs/QWEN_CONTEXT.md` (внизу) | Известные проблемы, приоритеты |
| **GDD_10 v4.0** | `docs/gdd/GDD_10_Ship_System.md` | Дизайн кораблей, коридоров, физики |

### 2. Текущий код:
| Файл | Путь | Проблема |
|------|------|----------|
| **ShipController.cs** | `Assets/_Project/Scripts/Player/ShipController.cs` | v2.1 — коридоры работают, деградация не применяет модификаторы |
| **AltitudeCorridorData.cs** | `Assets/_Project/Scripts/Ship/AltitudeCorridorData.cs` | ✅ OK |
| **AltitudeCorridorSystem.cs** | `Assets/_Project/Scripts/Ship/AltitudeCorridorSystem.cs` | ✅ OK (радиус → нужно collider) |
| **TurbulenceEffect.cs** | `Assets/_Project/Scripts/Ship/TurbulenceEffect.cs` | 🔴 Тряска слабая |
| **SystemDegradationEffect.cs** | `Assets/_Project/Scripts/Ship/SystemDegradationEffect.cs` | 🟡 Модификаторы не применяются |
| **AltitudeUI.cs** | `Assets/_Project/Scripts/UI/AltitudeUI.cs` | 🔴 HUD не виден |

---

## 🔴 ПРИОРИТЕТ 1: Починить AltitudeUI HUD

**Проблема:** AltitudeUI создаёт Canvas/Panel/TextMeshProUGUI программно но элементы НЕ видны в игре.

**Что попробовать:**
1. Проверить что Canvas создан как Screen Space Overlay (не Camera)
2. Проверить RectTransform — элементы должны быть в пределах экрана
3. Проверить Canvas Scaler — scale mode, reference resolution
4. Проверить sorting order/layer
5. Проверить что TextMeshProUGUI имеют font assigned
6. Проверить Console на ошибки TMP

**Агент:** @unity-ui-specialist

---

## 🔴 ПРИОРИТЕТ 2: Усилить турбулентность

**Проблема:** При DangerLower (Y < 1200м) тряска недостаточная.

**Текущие параметры:**
- `turbulenceIntensity = 15`
- `forceMultiplier = 50`
- `updateInterval = 0.05`
- Силы: `intensity × severity × mass × forceMultiplier`

**Что сделать:**
1. Увеличить `turbulenceIntensity` до 30-50
2. Добавить **Cinemachine Impulse** для тряски камеры (не только корабля)
3. Добавить визуальный эффект — частицы/туман Завесы
4. Rate-limit Debug.Log (сейчас спам каждый FixedUpdate)

**Агент:** @gameplay-programmer + @unity-specialist (Cinemachine)

---

## 🟡 ПРИОРИТЕТ 3: Городские коридоры → BoxCollider-триггеры

**Проблема:** Сейчас проверка IsInCityZone = Vector3.Distance(point, center) <= radius. Это сфера, не зона города.

**Что сделать:**
1. Создать CityZoneTrigger — GameObject с BoxCollider(isTrigger)
2. При OnTriggerEnter(ship) → активировать городской коридор
3. При OnTriggerExit(ship) → вернуться к глобальному коридору
4. BoxCollider размеры настраиваются в Inspector для каждого города
5. AltitudeCorridorSystem подписывается на события триггеров

**Агент:** @engine-programmer + @unity-specialist

---

## 🟡 ПРИОРИТЕТ 4: Деградация → реальные модификаторы

**Проблема:** SystemDegradationEffect.GetModifiers() рассчитывает DegradationModifiers но они НЕ применяются к ShipController.

**Что сделать:**
1. В ShipController.ValidateAndApplyAltitudeEffects():
   - При DangerUpper: применять `_currentDegradationModifiers.thrust` к `thrustForce`
   - Применять `yaw` к `yawForce`, `pitch` к `pitchForce`
   - Применять `vertical` к `verticalForce`
   - Применять `extraDrag` к `_rb.linearDamping`
2. При Safe: сбрасывать модификаторы к 1.0

**Агент:** @engine-programmer

---

## 📝 Критерии Приёмки Сессии 3

- [ ] AltitudeUI HUD виден в игре (🟢🟡🔴 + тексты)
- [ ] Турбулентность при Y<1200м — корабль заметно трясёт + камера трясётся
- [ ] Городские коридоры работают через BoxCollider-триггеры
- [ ] Деградация при Y>4650м — корабль реально медленнее/менее маневренный
- [ ] Нет Console-спама (логи rate-limited)
- [ ] 0 ошибок компиляции
- [ ] Коммит + тег `backup-3_session-ship-fixed`

---

## ⚠️ Правила (из Сессии 1-2)

1. ❌ **НЕ создавать asmdef файлы** — ломают Assembly-CSharp
2. ❌ **НЕ коммитить без проверки компиляции**
3. ❌ **НЕ создавать несколько файлов одновременно** без промежуточной проверки
4. ✅ **Проверять в Unity после каждого файла**
5. ✅ **Один файл за раз → проверка → следующий**

---

*Документ создан: 12 апреля 2026*
*Сессия 2 завершена ✅ но требует фиксов*
*Следующий шаг: Сессия 3 — фиксы Сессии 2 + Wind & Turbulence*
