# Сессия 5: Meziy Thrust & Advanced Modules — Fix Round

**Дата:** 12 апреля 2026 | **Статус:** ⚠️ Исправлены P1 баги, готовы к тесту | **Ветка:** `qwen-gamestudio-agent-dev`
**ShipController версия:** v2.4 → v2.4b (fix round)

---

## Исправления (Fix Round)

### ✅ Исправлено
- **P1:** При fuel=0 блокируются yaw/pitch/lift (было: только thrust)
- **P1:** Fuel regen работает при fuel=0 (было: RegenFuel блокировался при IsEmpty)
- **P2:** Крен перенесён с A/D на Z/C (было: конфликт с yaw)
- **P2:** Кнопка L — атмосферная дозаправка (2.0 fuel/s, штраф -50% thrust, -30% speed)
- **P2:** MeziyThrusterVisual — добавлены Debug.Log для отладки визуала

### ⚠️ Остаётся
- Визуал сопел не виден в Play Mode — нужна настройка ParticleSystem в Inspector (документировано)

## Таблица багов (актуальная)

| Баг | Статус | Файл |
|-----|--------|------|
| Fuel=0 не блокирует yaw/pitch/lift | ✅ Исправлен | `docs/bugs/SESSION5_FUEL_EMPTY_CONTROLS_NOT_BLOCKED.md` |
| Regen не работает при fuel=0 | ✅ Исправлен | `docs/bugs/SESSION5_FUEL_EMPTY_CONTROLS_NOT_BLOCKED.md` |
| Визуал сопел не виден | ⚠️ Нужна настройка | `docs/bugs/SESSION5_MEZIY_VISUAL_NOT_VISIBLE.md` |
| Крен на A/D неудобен | ✅ Исправлен → Z/C | `docs/bugs/SESSION5_ROLL_KEYS_ZC.md` |
| Кнопка L — дозаправка | ✅ Реализовано | `docs/bugs/SESSION5_REFUEL_KEY_L_FEATURE.md` |

---

## Что Реализовано (Технически)

### 1. ShipFuelSystem.cs
**Путь:** `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs`

**Добавлено в fix round:**
- `atmosphericRefuelRate = 2.0f` — скорость дозаправки из атмосферы
- `thrustPenaltyDuringRefuel = 0.5f` — штраф тяги при дозаправке
- `speedPenaltyDuringRefuel = 0.7f` — штраф скорости при дозаправке
- `isRefueling` property — идёт ли дозаправка
- `thrustPenaltyMult` / `speedPenaltyMult` — множители штрафов
- `StartRefueling()` / `StopRefueling()` / `RefuelAtmospheric(dt)` — методы дозаправки
- `RegenFuel()` теперь работает при fuel=0 (убрана проверка IsEmpty)

### 2. ShipController.cs v2.4b

**Добавлено в fix round:**
- engineStalled обнуляет avgYaw, avgPitch, avgVertical (не только thrust)
- Обработка клавиши L → `fuelSystem.RefuelAtmospheric(dt)`
- Штраф к тяге при дозаправке: `thrustMult = isRefueling ? fuelSystem.thrustPenaltyMult : 1f`
- `ClampVelocity(isRefueling)` — штраф к скорости при дозаправке
- `GetCurrentRollInput()` → KeyCode.Z/C вместо A/D
- Debug.Log в MeziyThrusterVisual

---

## Управление (актуальное)

| Действие | Клавиша | Условия |
|----------|---------|---------|
| Тяга вперёд | W | |
| Торможение | S | |
| Рыскание влево | A | |
| Рыскание вправо | D | |
| Лифт вверх | Q | |
| Лифт вниз | E | |
| Буст | Left Shift | |
| **Крен влево** | **Z** | MODULE_ROLL установлен |
| **Крен вправо** | **C** | MODULE_ROLL установлен |
| **Дозаправка** | **L** | fuel < maxFuel, не engineStalled |

---

## Рекомендации по Тестированию (Fix Round)

### Тест 1: Fuel=0 полная блокировка
1. Запусти Play Mode
2. Лети пока fuel=0
3. Убедись: thrust=0, yaw=0, pitch=0, lift=0 — корабль НЕ управляется
4. Подожди ~30с (regen 0.3/s) → fuel должен восстановиться
5. Когда fuel > 0 — управление должно вернуться

### Тест 2: Крен на Z/C
1. Установи MODULE_ROLL
2. Z = крен влево, C = крен вправо
3. A/D = только yaw (крен НЕ влияет)

### Тест 3: Дозаправка (L)
1. Зажми L
2. Топливо должно расти быстрее (~2.0/s вместо 0.3/s)
3. Тяга должна снизиться на 50%
4. Скорость должна снизиться на 30%
5. При fuel=max — дозаправка авто-стоп

---

*Документ обновлён: 12 апреля 2026 | Fix Round*
