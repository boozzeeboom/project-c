# T-PERF07 — CPU: trigger-события и polling зон

Основание: `ProfilerCaptures/ProjectC_client_2026-11-22_14-39-13.data` (289 кадров, Allocation Call Stacks ON)
и `ProfilerCaptures/console.txt` (511 записей). Захват разобран через `RawFrameDataView` по всем кадрам main thread.

## Горячие участки захвата

Средние значения ниже включают загрузку и останов, поэтому это адреса для оптимизации, а не оценка steady-state FPS.

| Маркер | Вызовы | Total, мс/кадр | Self, мс/кадр |
|---|---:|---:|---:|
| `Physics.SendTriggerEvents` | 2 797 | 82,83 | 61,50 |
| `OuterCommZone.OnTriggerEnter` | 5 541 | 18,38 | 17,18 |
| `NetworkPlayer.Update` | 271 | 8,21 | 7,69 |
| `NpcShipWorld.FixedUpdate` | 2 532 | 7,32 | 5,36 |
| `MarketZone.Update` | 3 190 | 4,56 | 3,97 |
| `WindManager.FixedUpdate` | 2 797 | 3,66 | 3,63 |

Отдельный вклад самого замера: `Profiler.Callstack` + `Profiler.FlushMemoryCounters` — около 17,9 мс/кадр.

## Решение

### 1. `OuterCommZone`: polling вместо физического trigger

`PollActorsInRange` уже определяет и игроков, и кораблей одним проходом по `NetworkManager.SpawnManager.SpawnedObjects`.
Клиентская зона локального игрока также независимо обновляется в `PollLocalPlayerZone`. Физический SphereCollider
был только быстрым путём, но в плотной геометрии города порождал лавину событий от посторонних коллайдеров.

В `Awake` коллайдер выключается (`_sphere.enabled = false`). Объект и сериализованные данные сцены сохранены;
состояние зон по-прежнему обновляется с прежним `pollInterval` и прежним debounce.

### 2. `MarketZone`: один проход по сетевым актёрам вместо двух OverlapSphere

Серверный polling рынка делал `OverlapSphereNonAlloc` отдельно для игроков (`tradeRadius`) и кораблей
(`shipDockRadius`). Теперь `PollActorsInRadius` проходит по `SpawnedObjects`, вычисляет квадрат дистанции один раз
и заполняет два переиспользуемых набора. Радиусы, состав состояний и debounce удаления игрока не изменены.
Физический trigger малого радиуса оставлен как быстрый путь.

## Подтверждение — `ProjectC_client_2026-11-22_15-10-49.data`

267 кадров, 1 771 маркер, 580 MB; тот же разбор всех кадров main thread через `RawFrameDataView`.

| Маркер | До: total/self, мс на кадр | После: total/self, мс на кадр |
|---|---:|---:|
| `Physics.SendTriggerEvents` | 82,83 / 61,50 | **53,44 / 50,63** |
| `OuterCommZone.OnTriggerEnter` | 18,38 / 17,18; 5 541 вызов | **отсутствует** |
| `MarketZone.Update` | 4,56 / 3,97 | **3,89 / 3,69** |
| `NetworkPlayer.Update` | 8,21 / 7,69 | 7,39 / 6,89 |
| `NpcShipWorld.FixedUpdate` | 7,32 / 5,36 | 6,27 / 4,28 |
| `OuterCommZone.Update` | 3,51 / 3,34 | 3,32 / 3,15 |
| `WindManager.FixedUpdate` | 3,66 / 3,63 | 2,42 / 2,39 |
| `DockingPadTriggerBox.OnTriggerEnter` | 2,21 / 2,08 | 2,11 / 1,97 |

Отключение физического триггера связи подтверждено точно: его managed-колбэк полностью исчез. Native self-time
`Physics.SendTriggerEvents` снизился на 17,7%, но остаётся главным узким местом: оставшийся native dispatch нельзя
без дополнительной атрибуции приписывать одному источнику. Рынок улучшился умеренно. Средние включают загрузку и
останов; для нового захвата участок 30..250 (221 кадр) разобран отдельно в `06_T_PERF08_INTERACTION_TRIGGERS.md`.

`ProfilerCaptures/console.txt` не относится к новому прогону: экспорт в файле по-прежнему датирован 14:39:42.
Поэтому консоль 15:10 из него не восстанавливалась и в сравнении не использовалась.

## Проверка

- Compile после правок: компиляция и domain reload завершены; ошибок C# не было.
- `git diff --check`: чисто.
- Runtime-проверка: новый захват снят пользователем. Агент Play Mode не запускал и скриншоты не делал.

## Следующий этап

Оставшиеся trigger-события корабельных interaction-коллайдеров и ранний фильтр посадочных площадок вынесены в
`06_T_PERF08_INTERACTION_TRIGGERS.md`. Троттлинг `NpcShipWorld`/`WindManager` без отдельного анализа не меняется:
это уже изменение поведения, а не только стоимости диспетчеризации.
