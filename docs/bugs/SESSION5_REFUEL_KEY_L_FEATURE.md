# Фича: Кнопка дозаправки мезиевых паров (L)

**Сессия:** 5 | **Дата:** 12 апреля 2026 | **Приоритет:** P2
**Статус:** 💡 Предложение

## Описание
Добавить кнопку `L` для принудительной дозаправки мезиевых паров из атмосферы.
Это позволит игроку восстанавливать топливо на лету, но с компромиссом (например, корабль теряет скорость или тягу на время дозаправки).

## Мотивация
Без системы доков/станций игрок не может восстановить топливо. Кнопка `L` даёт временное решение.

## Предлагаемая механика
- **Кнопка:** `L` (зажми = дозаправка)
- **Скорость восстановления:** 2.0 fuel/s (в 6.6x быстрее idle regen 0.3)
- **Компромисс:** Пока идёт дозаправка:
  - thrust снижается на 50%
  - maxSpeed снижается на 30%
  - визуальный эффект (частицы "сбор паров")
- **UI:** Индикатор "Сбор паров..." на HUD

## Реализация
```csharp
// ShipFuelSystem.cs
public bool isRefueling { get; private set; }
public void StartRefueling() => isRefueling = true;
public void StopRefueling() => isRefueling = false;

// В FixedUpdate ShipController:
if (isRefueling)
{
    fuelSystem.RegenFuelMethane(dt); // 2.0 fuel/s
    // Применить штрары к тяге и скорости
}
```

## Затронутые файлы
- `Assets/_Project/Scripts/Ship/ShipFuelSystem.cs` (добавить refuel mode)
- `Assets/_Project/Scripts/Player/ShipController.cs` (интеграция ввода L)

## Связанные баги
- `docs/bugs/SESSION5_FUEL_EMPTY_CONTROLS_NOT_BLOCKED.md` — regen при fuel=0
