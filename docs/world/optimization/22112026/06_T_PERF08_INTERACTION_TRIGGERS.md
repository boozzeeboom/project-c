# T-PERF08 — CPU: interaction-триггеры кораблей

Основание: `ProfilerCaptures/ProjectC_client_2026-11-22_15-10-49.data` после T-PERF07.
Полнокадровое сравнение с базой — `05_T_PERF07_ZONE_CPU.md`.

## Установившийся режим

Для отделения загрузки и останова дополнительно разобраны кадры с порядковыми номерами 30..250 — 221 кадр main thread.
Это не оценка FPS: Allocation Call Stacks и редактор добавляют собственную стоимость.

| Маркер | Total, мс/кадр | Self, мс/кадр | Вызовы за 221 кадр |
|---|---:|---:|---:|
| `Physics.SendTriggerEvents` | 38,20 | 37,88 | 1 644 |
| `ShipCargoConsole.OnTriggerEnter` | 0,022 | 0,021 | 2 082 |
| `ShipCargoConsole.OnTriggerExit` | 0,074 | 0,074 | 10 786 |
| `ShipController.OnTriggerEnter` | 0,016 | 0,016 | 4 050 |
| `ShipController.OnTriggerExit` | 0,080 | 0,080 | 15 616 |

Собственные managed-колбэки дёшевы; стоимость сосредоточена в native dispatch. Поэтому следующий рычаг — сокращение
числа событий, а не дальнейшая микрооптимизация тел колбэков.

Чтение префабов через `AssetDatabase` показало активные `SphereCollider` радиуса 1,35 м на объектах `Exchanger`
с `ShipCargoConsole`. На корнях кораблей отдельных включённых trigger-коллайдеров не найдено. `ShipController`
находится на корне с `Rigidbody` и получает события дочерних trigger-коллайдеров; точный остаток его событий после
отключения консолей определяется следующим замером, а не приписывается консолям целиком.

## Изменения

### 1. `ShipCargoConsole`: реестр вместо физического входа/выхода

`InteractableManager.FindNearestShipCargoConsole` уже выбирает терминал по дистанции; `NetworkPlayer` передаёт прежний
`pickupRange = 3 м` и при подсказке, и при взаимодействии. Физический вход игрока не является источником истины.

- Регистрация перенесена в `OnEnable`, снятие остаётся в `OnDisable`.
- `OnTriggerEnter`, `OnTriggerExit` и их фильтр игрока удалены.
- Сериализованная форма `SphereCollider` сохранена для редакторских данных, но в `Awake` коллайдер выключается.
- Радиус поиска и проверки доступа к кораблю не изменены. Список активных терминалов обходится только при поиске,
  без физических событий от городской геометрии и движущихся кораблей.

### 2. `DockingPadTriggerBox`: ранний фильтр статической геометрии

В `OnTriggerEnter` и `OnTriggerExit` до `GetComponentInParent<ShipController>` добавлена проверка
`other.attachedRigidbody == null`. У `ShipController` обязателен `Rigidbody`; дочерние compound-коллайдеры корабля
возвращают его через `attachedRigidbody`. Логика владельца, RPC, `IsShipInside` и состав коллайдеров площадки не изменены.

## Проверка

- Compile и domain reload завершены, `is_compiling == false`; ошибок C# в Console нет.
- В error-выдаче остались только transport-сообщения коротких MCP-подключений (`Client handler exited`, записи
  о разрыве соединения), не ошибки проекта.
- Play Mode агентом **не запускался**, тесты и Build через MCP не запускались.

## Подтверждение — `ProjectC_client_2026-11-22_17-05-19.data`

605 кадров, 1 740 маркеров, 764 MB. Для steady-state взяты кадры 30..599 — 570 кадров; старт и пять
последних кадров, включая останов, исключены. Сравнение с предыдущим steady-state участком (221 кадр):

| Маркер | До T-PERF08 | После T-PERF08 |
|---|---:|---:|
| `Physics.SendTriggerEvents`, total/self мс на кадр | 38,20 / 37,88 | **0,189 / 0,068** |
| `ShipCargoConsole.OnTriggerEnter`, вызовы | 2 082 | **0** |
| `ShipCargoConsole.OnTriggerExit`, вызовы | 10 786 | **0** |
| `ShipController.OnTriggerEnter`, вызовы | 4 050 | **71** |
| `ShipController.OnTriggerExit`, вызовы | 15 616 | **125** |
| `MarketZone.Update`, total/self мс на кадр | 1,377 / 1,322 | 0,612 / 0,584 |

Правка подтверждена: колбэки консоли исчезли, события `ShipController` сократились на 99%, native self-time
trigger-dispatch — на 99,8%. По всем 605 кадрам, включая загрузку и останов, `Physics.SendTriggerEvents` —
57,34 мс self суммарно, 0,0948 мс/кадр. Это уже не следующий кандидат оптимизации.

Оставшиеся managed self-кандидаты steady-state: `Player.NetworkPlayer.Update` — **5,32 мс/кадр**,
`NpcShipWorld.FixedUpdate` — 1,69, `WindManager.FixedUpdate` — 1,08, `MarketZone.Update` — 0,58,
`OuterCommZone.Update` — 0,58. Построчной CPU-атрибуции внутри `NetworkPlayer.Update` в захвате нет:
движение, ввод и троттлинг без отдельного инструментирования не меняются. Замер сам добавляет
`Profiler.FlushMemoryCounters` 8,51 и `Profiler.Callstack` 2,28 мс/кадр.

`ProfilerCaptures/console.txt` по-прежнему содержит экспорт 14:39:42 и не использовался для этого прогона.
Новых правок кода при разборе подтверждения не было; Play Mode агентом не запускался.

## Регрессионная проверка

1. Пешком подойти к грузовому терминалу своего корабля: на прежней дистанции появляется подсказка `F`, окно груза
   открывается после серверной проверки доступа; при выходе за 3 м взаимодействие недоступно.
2. Повторить подход/отход несколько раз и проверить другой корабль без ключа: ложного доступа быть не должно.
3. Совершить посадку на docking pad: срабатывает `NotifyTouchedDownRpc`, визуальный маркер занятости и выход с площадки
   работают как раньше.
4. При следующих изменениях interaction-кода повторить `.data`: `ShipCargoConsole.OnTriggerEnter/Exit` должны
   отсутствовать, а native self-time `Physics.SendTriggerEvents` — оставаться около 0,068 мс/кадр на
   сопоставимом steady-state участке.
