# T-LIGHT13 — расширенное управление группой света

**Статус:** реализовано; Play Mode-приёмка за пользователем
**Дата:** 2026-10-23
**Файл:** `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs`
**Связанные:** `T-LIGHT09` (исходный контроллер), `T-LIGHT11` (качество теней солнца), `PrimumLightPool` (Э3)

## 1. Проблема

До этапа контроллер управлял только `Light.intensity`: базовая интенсивность × множитель группы × кривая по времени суток. Всё остальное — radius, цвет, тип, углы Spot, тени, bounce — задавалось авторски на каждом источнике и не могло правиться из одного места. Город PRIMUM содержит ~1 900 экземпляров `PM_LIGHT_*`, поэтому правка `range`/`color` у группы вручную нереализуема.

## 2. Решение

В `LightGroup` добавлен блок `LightOverrides` — те же свойства, что на компоненте `Light`, каждое со своим флагом «переопределять». Флаг выключен → авторское значение источника не меняется вообще.

```csharp
public sealed class LightOverrides
{
    public bool overrideRange;          public float range = 10f;
    public bool overrideType;           public LightType type = LightType.Point;
    public bool overrideSpotAngle;      public float spotAngle = 30f;
    public bool overrideInnerSpotAngle; public float innerSpotAngle = 21f;
    public bool overrideColor;          public Color color = Color.white;
    public bool overrideUseColorTemperature; public bool useColorTemperature;
    public bool overrideColorTemperature;    public float colorTemperature = 6500f;
    public bool overrideShadows;        public LightShadows shadows = LightShadows.None;
    public bool overrideShadowStrength; public float shadowStrength = 1f;
    public bool overrideBounceIntensity; public float bounceIntensity = 1f;
}
```

## 3. Семантика применения

1. При первом обнаружении источника снимается **база**: `intensity`, `range`, `color`, `type`, `spotAngle`, `innerSpotAngle`, `useColorTemperature`, `colorTemperature`, `shadows`, `shadowStrength`, `bounceIntensity` (`CaptureState`).
2. При смене группы (включая «группа → null») выполняется `RestoreBaseline`, затем `ApplyOverrides` новой группы. Один и тот же источник никогда не получает два разных набора переопределений.
3. Порядок в `ApplyOverrides` важен: сначала `type`, потом `range`/углы Spot — проверка `light.type == LightType.Spot` идёт уже по новому типу.
4. `innerSpotAngle` ограничивается сверху `spotAngle − 0.1°`, `range` — снизу `0.01`, `colorTemperature` — `1000…20000 K`, `shadowStrength` — `0…1`.
5. Интенсивность по-прежнему `база × множитель × кривая`; блок переопределений её не касается.

Проверка идёт только при смене группы, а не каждый кадр: переопределения не перезаписывают свойства источника постоянно.

## 4. Ограничения

| Ограничение | Причина |
|---|---|
| `LightType.Directional` в `overrideType` не применяется | Группы намеренно не управляют направленными источниками (`Matches` отбрасывает `Directional`): Sun/Moon ведутся `DayNightController`/`MoonController`. |
| Углы Spot действуют только при `overrideType = Spot` или авторском типе Spot | `spotAngle`/`innerSpotAngle` у Point-источника ни на что не влияют. |
| `colorTemperature` действует только при включённой цветовой температуре | Требование самого `Light`: без `useColorTemperature` значение игнорируется. |
| `shadows`/`shadowStrength` не дадут видимых теней, пока в URP Asset выключены additional light shadows | Текущее состояние `ProjectC_URP.asset`; см. `T-LIGHT11` для теней солнца. |
| `LightType.Area` в рантайме не рендерится в URP | Area lights — только для bake. Значение оставлено для полноты, но практического смысла в realtime-пути не имеет. |
| Смена типа в рантайме не бесплатна | Источник пересоздаётся в рендер-пайплайне. Происходит только при смене группы, не покадрово. |

## 5. Взаимодействие с существующей архитектурой

- **`PrimumLightPool`** владеет только `Light.enabled` (бюджет 96 источников). Этот контроллер владеет только `Light.intensity` и переопределениями свойств. Пересечения нет: выключенный пулом источник просто не рисуется, его `intensity` при этом корректна и готова к включению.
- **`DayNightController`** владеет Sun/Moon, ambient, туманом, скайбоксом и Volume. Контроллер групп их не трогает.
- **Групповой порядок** в `BootstrapScene` не менялся: `PRIMUM_OUTLIGHT` → `PRIMUM_INLIGHT` → `CityLamps` → `AllAdditionalLights` (catch-all последним).

## 6. Влияние на сцены

Сцены не изменялись. `BootstrapScene.unity` содержит сериализованный массив `groups` без новых полей, поэтому на диске у блока `overrides` значения по умолчанию — все флаги `false`, то есть **поведение уже настроенных групп не изменилось**. Unity допишет поля в сцену при следующем сохранении `BootstrapScene` пользователем.

`WorldScene_0_0.unity` не трогалась.

## 7. Проверки

- `refresh_unity` (`scope: scripts`, `compile: request`) — компиляция запрошена.
- `check_compile_errors` — `No compile errors`.
- Play Mode, визуальная проверка новых переопределений — **NOT RUN** (по правилу проекта runtime-тесты выполняет пользователь).

## 8. Как пользоваться

1. `BootstrapScene` → объект с `DayNightController`/`AdditionalLightIntensityController` → `Groups`.
2. Раскрыть нужную группу → `Overrides`.
3. Включить флаг нужного свойства (например `Override Range`) и задать значение — оно применится ко всем источникам группы одновременно.
4. Чтобы вернуть авторские значения, снять флаг: при следующей смене группы (`RestoreBaseline`) источник получит исходные настройки сцены. Для немедленного возврата проще перезапустить Play Mode.
