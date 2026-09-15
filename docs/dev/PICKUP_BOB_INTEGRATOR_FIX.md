# Pickup-бобаинг как интегратор: root cause и фикс

Дата: 2026-09-15. Тикет-контекст: `T-PICKUP-RIDE-01` (carry-формула), симптом — до-FO бага:
при старте/телепортации все pickable-предметы синхронно прыгают «как на батуте»,
затем постепенно останавливаются.

## Root cause

`PickupItem.Update` / `NpcLootPickup.Update` каждый кадр делают:

```csharp
_deckRide?.RefreshWorldBase();                              // база = transform.position (ВКЛЮЧАЯ bob прошлого кадра!)
transform.position = _deckRide.WorldBasePosition + bob;     // + новый bob сверху
```

`RefreshWorldBase()` снимает слепок с позиции, в которой уже сидит прошлый bob.
Каждый кадр к базе прибавляется `A·sin(t)` — это дискретное интегрирование синуса.
Усиление интегратора ≈ `1/sin(w·dt/2)` ≈ 60× при 60 FPS: из задуманных ±0.2 м
получаются медленные колебания в метры. Все пикапы в фазе (общий `Time.time`
без оффсета) — поэтому «все подпрыгивают» синхронно.

Почему «останавливаются»: `_platformMask = ~0`, probe цепляет землю под пикапом,
`PickupDeckRide` аттачится (`DeckParent != null`) — и `Update` перестаёт писать
позицию. Дрейфующий пикап, спустившись в зону probe (0.4 м под пивотом), замирает
на случайной дрейфованной высоте. Выглядит как «пружина успокоилась».

Сопутствующие дефекты:
- `NpcLootPickup.Update` (и теоретически `PickupItem`): при `_deckRide == null`
  (кадры до `OnNetworkSpawn`) строка `transform.position = _deckRide.WorldBasePosition + bob`
  даёт NRE — `?.` стоит только на `RefreshWorldBase`.
- Синфазность: общий `Time.time` без per-instance фазы.

## Фикс (минимальный, carry-контракт T-PICKUP-RIDE-01 сохранён)

`PickupDeckRide` становится единственным владельцем базы:
- `ApplyFreeBob(bob)` — единая точка свободного режима: поглощение внешних сдвигов
  (телепорт/спавн/rebase) через drift-сравнение `transform.position` vs `база + прошлый bob`
  с порогом 0.05 м (макс. межкадровый шаг bob ≈ 0.01 м), затем `pos = база + bob`.
- carry в `LateUpdate`: `_worldBasePosition += deltaPos` вместе с `transform.position`.
- `ApplyRebaseTranslation`: сдвигает и `_platformLastPos`, и `_worldBasePosition` (T-FO07E + база).
- `RefreshWorldBase()` оставлен для совместимости, семантика исправлена:
  `база = позиция − прошлый bob` (сброс накопленной ошибки, не слепок с ней).
- Владельцы (`PickupItem`, `NpcLootPickup`): в свободном режиме только считают bob
  (со случайной per-instance фазой) и зовут `ApplyFreeBob`; на палубе позицию не трогают
  (как раньше). NRE-гард через `?.`.

Что НЕ трогаем: `PlatformRideHelper`, `GlobalMotionControlledRebaseSlice`,
`BootstrapScene`, probe-параметры.

## Проверка

1. Compile: `refresh_unity` → `read_console` → 0 errors.
2. Reflection: `ProjectC.Core.PickupDeckRide` содержит `ApplyFreeBob`.
3. Manual: сцена с пикапами → Play → пикапы качаются ±0.2 м на месте, синхронности нет,
   телепорт/стоп-плей не разбрасывает их; F8 → `runtimeRebase.Completed`, пикапы на месте.
