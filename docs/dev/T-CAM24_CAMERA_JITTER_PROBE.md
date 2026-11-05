# T-CAM24 — Пробник дрожи камеры + фикс зума в префабе

> **Файлы:** `Assets/_Project/Scripts/Core/SpringArmCamera.cs`,
> `Assets/_Project/Prefabs/ThirdPersonCamera.prefab`
> **Контекст:** T-CAM22 / T-CAM23 (фильтры и platform-follow) и T-PAROM-25
> (интерполяция `s` на клиенте) уже в HEAD, но жалоба «камера дёргается на пароме,
> персонаж нормально» пришла повторно — дальше без цифр менять камеру вслепую нельзя.

## 1. Фикс префаба (реальный баг)

В `ThirdPersonCamera.prefab` стояло `zoomStepPercent: 50` при коде T-CAM21, который
трактует это поле как **долю дистанции** (`0.2` = +20% за щелчок):

```
zoomDelta = notches * max(_userDistance * 50, 0.25) * sens   // 5 м * 50 = 250 м за щелчок
```

Один щелчок колеса уводил камеру либо в `zoomMinDistance` (0.5 м — внутрь персонажа),
либо в `zoomMaxDistance` (50 м), и она там оставалась. На движущемся пароме и то и
другое читается как «камеру мотает, а персонаж нормально».

**Исправлено через редактор** (`SerializedObject`, не ручная правка YAML):
`zoomStepPercent 50 → 0.2`, `zoomChaseSpeed 50 → 8` (код-дефолты T-CAM21).
Других мест с `zoomStepPercent` в проекте нет (проверено грепом по `.prefab`/`.unity`).

## 2. Пробник дрожи (`debugJitterProbe`, выкл по умолчанию)

Новое поле в секции **Diagnostics** префаба `ThirdPersonCamera`. При включении раз в
секунду пишет в Console одну строку (поведение камеры не меняет):

```
[CAM-JITTER] frames=… follow=… targetStepMax=…mm camStepMax=…mm
             dist=… .. …m (spread=…mm) platStepMax=…mm dtMax=…ms hitches=…
             lagSpeed=… colliding=… target=…
```

Как читать:

| Наблюдение | Вывод |
|---|---|
| `targetStepMax` большой, `camStepMax`/`spread` маленькие | дрожит **источник** (позиция персонажа: сеть/платформа/carry), камера честно сглаживает |
| `targetStepMax` маленький, `camStepMax` или `spread` большие | дрожит **камера** (лаг/коллизия/anti-pop/adaptive) |
| `platStepMax` большой (>50 мм/кадр) | платформа идёт ступеньками сети → это `T-PAROM-25` (интерполяция `s`) |
| `follow=0` на пароме | зонд платформы не срабатывает → `platformFollow` не активен |
| `hitches>0` при `dtMax` 100 мс+ | это фризы (перф/стриминг), камера тут не виновата |

## 3. Быстрая изоляция без кода (инспектор префаба)

1. `lagEnabled = false` → если дрожь ушла, виноват лаг (камера догоняет ступенчатый
   сигнал); если осталась — смотреть цель (`targetStepMax`).
2. `platformFollowEnabled = false` → сравнить ощущение на пароме.
3. `adaptiveDistanceEnabled = false` → исключить shrink/recover у стен.

## 4. Откат

Поле `debugJitterProbe` выключено по умолчанию — откат не нужен.
Значения зума в префабе — как в коде T-CAM21; при желании правятся в инспекторе.
