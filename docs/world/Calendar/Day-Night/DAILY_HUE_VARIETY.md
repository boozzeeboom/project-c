# Daily Hue Variety — суточный сдвиг hueShift

**Date:** 2026-09-20
**Status:** ✅ Реализовано, проверено в Play Mode

Точечная добавка поверх настроенного `DayNightController`: каждый новый игровой день
`hueShift` (ColorAdjustments) в day/twilight/night Volume-профилях получает небольшой
случайный оффсет. Диапазон настраивается в профиле, по умолчанию −10…+10°.
Ничего из существующей настройки не меняется — авторские значения из `.asset` остаются базой.

---

## 1. Настройки (где крутить)

`DayNightProfile`, секция **Daily Hue Variety**:

| Поле | Тип | Default | Описание |
|------|-----|---------|----------|
| `enableDailyHueShift` | bool | `true` | Вкл/выкл фичи |
| `dailyHueShiftRange` | Vector2 | `(-10, 10)` | Min/max суточного оффсета в градусах |

Сериализовано в `Assets/_Project/ScriptableObjects/DayNight/DayNightProfile.asset`
(`enableDailyHueShift`, `dailyHueShiftRange`).

---

## 2. Как работает

- **Только рантайм-копии.** Контроллер уже делает `Instantiate()` трёх профилей при старте
  (`InitializeVolumeProfileInstances`). Оффсет пишется только в них — `.asset`-файлы
  никогда не мутируют. Авторский `hueShift` читается с копий один раз (`CacheBaseHueShiftValues`)
  и используется как база: `итог = clamp(база + оффсет, −180, 180)`.
- **Детерминированный сид.** Оффсет считается от номера игрового дня
  (`floor(TotalGameDays)` из `ServerWeatherController` через существующий `seededRand`
  с golden-ratio хешем) — все клиенты в один день видят одинаковый сдвиг.
- **Разные значения на профиль.** К сиду прибавляется своя соль:
  Day `0.13`, Twilight `0.47`, Night `0.71` — иначе все три профиля получили бы одинаковый оффсет.
- **Дешёвый пересчёт.** В `UpdateDayNight` — один float-compare в кадр
  (`ApplyDailyHueShiftIfNeeded`); пересчёт только при смене дня + один раз при
  инициализации/пересоздании инстансов (кейс domain reload).
- **Temperature-volume не тронут.** Его `hueShift` принудительно `0` в `ApplyTemperatureFilter` — так и осталось.
- **Выключение в рантайме** (`enableDailyHueShift = false`) один раз возвращает авторские значения.

Фаза-вариативность (`TimeOfDayPhase.hueShiftRange`, солнце/туман/эмбиент) не затрагивалась.

## 3. Public API (`DayNightController`)

- `CurrentDayHueOffset / CurrentTwilightHueOffset / CurrentNightHueOffset` — текущие оффсеты (для админки).
- `ForceRefreshDailyHueShift()` — принудительный пересчёт (после смены диапазона в инспекторе).
- Дебаг-оверлей: строка `HueShift D:… T:… N:…` (рядом с весами бленда).

## 4. Проверка

1. Compile: Console → 0 errors.
2. Play, оверлей Day/Night → строка `HueShift` показывает три разных оффсета в пределах диапазона.
3. Перемотать время на следующий день (`SetTimeOfDay` / серверное время / быстрый цикл через
   `ServerWeatherController._dayCycleRealHours`) → оффсеты сменились, картинка чуть другая.
4. Выключить `enableDailyHueShift` в рантайме → `hueShift` вернулся к авторским (оверлей `0.0`).
5. Смена дня детерминирована: перезапуск на том же игровом дне даёт те же значения.

## 5. Файлы

```
Scripts:
- Assets/_Project/Scripts/Core/DayNight/DayNightController.cs  (блок Daily Hue Shift Variety + вызовы + оверлей)
- Assets/_Project/Scripts/Core/DayNight/DayNightProfile.cs     (секция Daily Hue Variety)

Assets (авто-сериализация Unity, руками не править):
- Assets/_Project/ScriptableObjects/DayNight/DayNightProfile.asset
```
