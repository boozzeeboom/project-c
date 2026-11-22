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

Ожидаемый эффект по захвату: убрать `OuterCommZone.OnTriggerEnter` и его вклад в `Physics.SendTriggerEvents`.
Фактический выигрыш подтверждается только следующим плейтестом.

### 2. `MarketZone`: один проход по сетевым актёрам вместо двух OverlapSphere

Серверный polling рынка делал `OverlapSphereNonAlloc` отдельно для игроков (`tradeRadius`) и кораблей
(`shipDockRadius`). Теперь `PollActorsInRadius` проходит по `SpawnedObjects`, вычисляет квадрат дистанции один раз
и заполняет два переиспользуемых набора. Радиусы, состав состояний и debounce удаления игрока не изменены.
Физический trigger малого радиуса оставлен как быстрый путь.

## Проверка

- Compile: компиляция и domain reload завершены; ошибок C# нет. В Console остались только transport-сообщения
  `MCP-FOR-UNITY: Client handler exited` от коротких аналитических подключений, не ошибки проекта.
- `git diff --check`: чисто.
- Play Mode: **не запускался**. Скриншоты и новый замер не делались.

## Ручная проверка и следующий замер

1. Повторить маршрут замера 14-39-13: зайти в мир, подойти к зоне связи и рынку, проверить пешком и на корабле.
2. Убедиться, что CommPanel открывается только в `commRange`, рынок — в `tradeRadius`; посадочный корабль виден
   рынку в `shipDockRadius`. Допустимая задержка polling — до 0,25 с (удаление игрока — прежние три промаха).
3. Проверить выход из зон: CommPanel/рынок закрываются по прежним правилам, RPC покупки и стыковки не дают `NotInZone`.
4. Снять новый `.data` с теми же настройками. Критерии: `OuterCommZone.OnTriggerEnter` отсутствует,
   `Physics.SendTriggerEvents` существенно снижен, `MarketZone.Update` ниже исходных 4,56 мс/кадр.
5. Следующие кандидаты по этому захвату, не тронутые здесь: `NetworkPlayer.Update`, `NpcShipWorld.FixedUpdate`,
   `WindManager.FixedUpdate`. Их троттлинг меняет поведение и требует отдельного анализа/замера.
