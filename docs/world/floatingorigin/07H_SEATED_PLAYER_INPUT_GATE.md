# T-FO07H: гейт координат глушит весь ввод у посаженного игрока

## Симптом (инпут_1.txt + ответы пользователя)

- В кресле мёртво всё: WASD (корабль не двигается), ENTER, T (и в поле, и в доке), P, F-выход. Жив только ESC (идёт мимо `NetworkPlayer.Update`).
- F-выход мёртв = софтлок в кресле.

## Цепочка (доказана логом и кодом)

1. `GlobalMotionActorState.RecordBaseline` ставит `CoordinatesRequired = true` навсегда (sticky, сброса нет) — у каждого заспавненного пилота `Required == true`.
2. Посадка: `NetworkPlayer.SubmitSwitchModeRpc` делает `transform.SetParent(_currentShip.ShipRoot, true)` (строка 1445).
3. Адаптер игрока переходит в `WaitingForParent, baselinePlaced=False` — в `инпут_1.txt` 103 occurrences начиная ровно с frame 1170 (посадка на 1167).
4. `CanSimulate = !Required || AllowsSimulation(adapterPresent, IsReadyForSimulation)` → false.
5. `NetworkPlayer.Update:786` — `if (!CanSimulateInCurrentCoordinates) return` каждый кадр. Весь диспетч (P/T/F/Enter/WASD) ниже гейта мёртв. `Update.begin`-проба (строка 785) при этом продолжает писаться — поэтому в логе «Update крутится, ввода нет».

## Почему это FO-баг, а не инпут-баг

Биндинги P/T/Enter в `InputBindingsConfig.asset` на месте (action 8/18/22), F идёт через тот же `FindActionBinding` и работает. FO-дифф диспетч не трогал — виноват гейт T-FO04D/T-FO05 над диспетчем.

## Фикс (минимальный)

В `Update`-гейте исключение для посаженного игрока: `if (!CanSimulateInCurrentCoordinates && !_inShip)`.
Обоснование: в кресле координаты игрока ведёт парентирование к корню корабля, симулируемый актор — корабль (`ShipController`), собственный адаптер игрока иррелевантен. Пеший режим без изменений. Гейты `FixedUpdate`/`ProcessMovement` не трогаем (в кресле контроллер выключен, им гейт не мешает).

## Что НЕ делаем здесь

- Перепривязку адаптера игрока к фрейму корабля при посадке (уровень T-FO07, отдельный тикет).
- Сброс `CoordinatesRequired` при посадке (ломает sticky-инвариант baseline).

## Приёмка

Сесть в кресло → WASD двигает корабль, ENTER запускает двигатель, T открывает CommPanel в доке, P открывает CharacterWindow, F выходит. В логе после посадки нет вечного `WaitingForParent`-молчания ввода.
