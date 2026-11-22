# T-PERF04 — триггер-колбэки корабля и грузового консоля

Рабочий документ этапа. Предшественник — `01_FIX_PLAN.md` (T-PERF03), замеры — `ITERATIONS.md`.

## Задача

Снять P0 из двух независимых замеров (`...11-00-03.data` сессия B и `...11-18-18.data`):
`GetComponentNullErrorMessage` — 70.6 MB из 131.0 MB всего GC (54%) при 111 853 вызовах,
93% объёма сидят под четырьмя колбэками корабля и грузового консоли.

## Причина

`other.GetComponent<CharacterController>() != null` выполняется на **каждом** чужом коллайдере,
входящем в триггер корабля/консоли. Если компонента нет, Unity строит null-сообщение
(`GetComponentNullErrorMessage`, ~0.64 KB) — это документированное поведение `GetComponent`,
а не наш лог. За сессию таких проверок 92 000+, отсюда половина мусора.

## Изменение

| Файл | Было | Стало |
|---|---|---|
| `Assets/_Project/Scripts/Player/ShipController.cs` | `if (other.CompareTag("Player") \|\| other.GetComponent<CharacterController>() != null)` в `OnTriggerEnter` (600) и `OnTriggerExit` (609) | `if (IsPlayerCollider(other))` |
| `Assets/_Project/Scripts/Ship/Cargo/ShipCargoConsole.cs` | то же в `OnTriggerEnter` (89) и `OnTriggerExit` (97) | `if (IsPlayerCollider(other))` |

В каждом классе добавлен приватный хелпер:

```csharp
private static bool IsPlayerCollider(Collider other)
    => other.CompareTag("Player") || other.TryGetComponent(out CharacterController _);
```

Семантика 1:1: сначала дешёвый `CompareTag("Player")` (короткое замыкание, как было), затем
проверка компонента на том же GameObject — но через `TryGetComponent`, который на отсутствии
компонента ничего не аллоцирует и ничего не логирует. `InteractableManager.Register*/Unregister*`
вызываются на тех же условиях, что раньше.

## Что измерено до правки (сессия C, `...11-18-18.data`, 348 геймплейных кадров)

| Метрика | Значение |
|---|---|
| `GetComponentNullErrorMessage` всего | 70 609 KB / 111 853 вызова |
| `ShipController.OnTriggerExit` | 24 969 KB / 37 770 вызовов |
| `ShipController.OnTriggerEnter` | 16 080 KB / 24 434 |
| `ShipCargoConsole.OnTriggerExit` | 15 542 KB / 23 188 |
| `ShipCargoConsole.OnTriggerEnter` | 9 112 KB / 13 629 |
| сумма по четырём колбэкам | 65 703 KB = 93% всех null-сообщений, 50% всего GC сессии |
| всего GC за сессию | 131 024 KB (среднее 376.5 KB/кадр, медиана 85.8 KB/кадр) |

Без изменений остаётся: `Physics.SendTriggerEvents` 9 680 ms за сессию (27.8 ms/кадр) —
это стоимость нативной доставки событий по количеству коллизий, а не обработчиков; `TryGetComponent`
её не уменьшит (см. «Остаток»).

## Ожидание после правки

| Метрика | Ожидание |
|---|---|
| `GetComponentNullErrorMessage` | исчезает из-под этих четырёх колбэков (было 65.7 MB/сессию) |
| всего GC за сессию | ~131 MB → ~65 MB |
| GC среднее на кадр | 376.5 → ~190 KB |
| GC медиана на кадр | 85.8 → ~50–60 KB |
| `PlayerLoop` медиана | почти без изменений: 67.5 ms — это CPU, а не мусор; возможен небольшой минус за счёт более редких GC |
| `Physics.SendTriggerEvents` | 19–28 ms/кадр, практически без изменений |

## Результат (замер 4, `...11-31-27.data`, 486 кадров)

| Метрика | Ожидание | Факт |
|---|---|---|
| GC четырёх колбэков | ~0 | **0 KB** при 102 925 вызовах (было 65 703 KB / 99 021) |
| `GetComponentNullErrorMessage` | −65.7 MB/сессию | 202.9 → 31.25 KB/кадр (−85%); остаток — FO-слой, не триггеры |
| GC среднее на кадр | ~190 KB | 376.5 → 202.9 KB (−46%) |
| `PlayerLoop` / `SendTriggerEvents` | без изменений | 67.5 → 68.3 ms / 27.8 → 27.57 ms |

Правка закрыта. Дальше по весу мусора: `LogStringToConsole` 61.3 KB/кадр (30% GC, Editor-артефакт частично),
`ShipDeckNav.LateUpdate` 34.2 KB/кадр (20 палуб × кадр, дрейф T-PERF02), null-сообщения FO-слоя 31.25 KB/кадр
(`GlobalMotionPoseAdapter` / `NetworkPlayer` / `NpcBrain` — тот же `GetComponent` на отсутствующем компоненте).

## Протокол замера (профайлер)

1. Console → Clear перед Play (иначе старые записи попадут в счётчики `LogStringToConsole`).
2. В Profiler Preferences **выключить** `Collect callstacks` (в захвате C он был включён и добавлял
   `Profiler.Callstack` 7.4 ms/кадр — абсолютный CPU такого замера несопоставим с A/B).
3. Play Mode, тот же маршрут и та же сцена, ~300–350 кадров (в редакторе это ~30 с при 10 fps).
4. Остановить Play, сохранить захват (`Profiler → Save`) и прислать `.data`.
5. Смотреть маркеры по именам: `GetComponentNullErrorMessage`, `Physics.SendTriggerEvents`,
   `ShipController.OnTriggerEnter/OnTriggerExit`, `ShipCargoConsole.OnTriggerEnter/OnTriggerExit`,
   а также итоговые `PlayerLoop` и `GC.Alloc` (столбец GC у строки `PlayerLoop` — это сумма по кадру).

## Проверки кроме профайлера (поведение)

| Что | Ожидаемо |
|---|---|
| Встать на палубу корабля и сойти | подсказка взаимодействия появляется и пропадает; `RegisterShip`/`UnregisterShip` без исключений |
| Подойти к грузовой консоли и отойти | интерфейс погрузки доступен на входе, недоступен на выходе |
| Console во время Play | ни одного сообщения про `GetComponent` / `CharacterController` |
| T-клавиша и CommPanel в зоне связи | работают как раньше (регресс-чек T-PERF03) |
| `ShipDeckNav.DumpStats()` | `spawn ≈ 20`, `drift ≈ 0` (регресс-чек T-PERF02/03) |

## Остаток (не входит в этот этап)

1. **Объём триггеров** — главный оставшийся рычаг по CPU: 111 853 колбэка за 348 кадров,
   `Physics.SendTriggerEvents` #1 в профиле. Требует правки геометрии триггерных коллайдеров
   на префабе корабля и `_interactionRadius` консоли — это данные префаба/сцены, менять только
   с согласия и с поведенческой проверкой.
2. `PxScene.simulate` 5.8 ms/кадр, `ScScene.updateCCDSinglePassStage3` 1.3, `SwapPhysXBuffersJob` 0.8
   — 21 842 коллайдера, матрица слоёв/статик.
3. Остаточные null-сообщения ~7 MB/сессию: `GlobalMotionWorld.FixedUpdate` 3.5 MB (5 404 вызова),
   `GlobalMotionPoseAdapter` 0.9, `NetworkPlayer.FixedUpdate` 0.9, `NpcBrain.Update` 0.8,
   `NetworkTickSystem` 0.6 — тот же класс бага в FO-слое, но объём в 10 раз меньше; трогать
   отдельным этапом.
4. `DockingPadTriggerBox.OnTriggerEnter` 2.3 ms/вызов — сосредоточено в 3 стартовых кадрах
   (f1/f98/f103 = 225 из 250 ms), то есть это пик одновременных входов при спавне, а не ровная цена.
