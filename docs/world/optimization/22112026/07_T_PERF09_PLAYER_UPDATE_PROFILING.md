# T-PERF09 — атрибуция `NetworkPlayer.Update`

Основание: steady-state захвата `ProjectC_client_2026-11-22_17-05-19.data` (570 кадров):
`Player.NetworkPlayer.Update` — 5,32 мс self/кадр. После T-PERF08 это главный managed self-кандидат,
но агрегированный маркер не разделяет ввод, подсказки и движение.

## Изменение

В `Assets/_Project/Scripts/Player/NetworkPlayer.cs` добавлены постоянные `ProfilerMarker` без строковых
аллокаций в горячих вызовах:

| Маркер | Что измеряет |
|---|---|
| `NetworkPlayer.Input.Held` | Все проверки удерживаемых клавиш, включая поиск binding и чтение Input System |
| `NetworkPlayer.Input.JustPressed` | Все проверки нажатий клавиш |
| `NetworkPlayer.InteractionHint` | Полный resolver контекстной подсказки |
| `NetworkPlayer.ChunkTracker` | `ForceUpdatePlayerChunk` |
| `NetworkPlayer.Movement` | Весь `ProcessMovement` |
| `NetworkPlayer.Movement.PlatformCarry` | Probe и перенос движущейся платформой |
| `NetworkPlayer.Movement.Animator` | Параметры Animator и чтение экипировки для `InCombat` |
| `NetworkPlayer.Movement.Wind` | Получение целевого сноса ветром |
| `NetworkPlayer.Movement.ControllerMove` | Непосредственно `CharacterController.Move` |

Используется `ProfilerMarker.Auto()` через `using`; ранние `return` корректно закрывают scope. Условия,
частота вызовов, порядок движения и ввода не изменены. Deep Profile для этой атрибуции не требуется.

## Проверка

- Diff содержит только объявления маркеров и измерительные scope; `git diff --check` — чисто.
- Compile и domain reload завершены, `is_compiling == false`; ошибок C# нет.
- Error-выдача Console содержит только transport-сообщения коротких MCP-подключений, не ошибки проекта.
- Play Mode, тесты и Build агентом не запускались.

## Замер пользователем

1. Обновить проект до коммита T-PERF09 и дождаться компиляции.
2. Profiler: CPU Usage, **Deep Profile OFF**, **Allocation Call Stacks OFF** — точечных маркеров достаточно,
   а запись стеков аллокаций добавляла около 10,8 мс/кадр в предыдущем захвате.
3. Войти в мир, дождаться окончания загрузки и записывать 30–60 секунд: часть времени стоять, часть — идти
   и бегать; затем сесть на корабль и немного полететь. Сохранить `.data` в `ProfilerCaptures`.
4. Сравнение будет по steady-state: total/self и число вызовов каждого `NetworkPlayer.*` маркера.
   После атрибуции следующий коммит должен адресовать измеренный источник, а не весь `Update` вслепую.

## Результат захвата `ProjectC_client_2026-11-22_18-35-43.data`

495 кадров, 1 775 маркеров; steady-state 30..489 — 460 кадров.

| Маркер | total/self, мс на steady-кадр | Вызовы |
|---|---:|---:|
| `Player.NetworkPlayer.Update` | 5,105 / 3,023 | 460 |
| `NetworkPlayer.InteractionHint` | **1,742 / 1,650** | 136; **5,893 мс/вызов** |
| `NetworkPlayer.Movement` | 0,154 / 0,0176 | 460 |
| `Movement.PlatformCarry` | 0,066 / 0,0056 | 460 |
| `Movement.ControllerMove` | 0,0634 / 0,0568 | 460 |
| `Movement.Animator` | 0,00661 | 460 |
| `Movement.Wind` | 0,00051 | 460 |
| `Input.JustPressed` | 0,00625 | 3 220 |
| `Input.Held` | 0,00417 | 2 300 |
| `ChunkTracker` | 0,00025 | 460 |

`Main Thread` — 69,81 мс/кадр, `PlayerLoop` — 48,49 мс/кадр. `Physics.SendTriggerEvents` —
0,500 / 0,133 мс/кадр: подтверждённая ранее оптимизация trigger-лавины сохраняется.

Ввод и движение больше не являются основными managed self-кандидатами. Горячий участок — resolver
подсказки: `InteractableManager.FindNearestDoor` последовательно вызывает `DistanceToInteraction`
примерно для 2 800 активных дверей, каждый вызов читает `Collider.bounds`.

В захвате остались `Profiler.FlushMemoryCounters` — 8,536 мс/кадр и `Profiler.Callstack` —
2,159 мс/кадр. Поэтому снижение стоимости профилирования относительно предыдущего прогона не заявляется.

## Broadphase для поиска дверей

`DoorController.InteractionBoundingRadius` лениво кэширует консервативный радиус сферы вокруг
`transform.position`, содержащей `BoxCollider` взаимодействия. Радиус учитывает локальные `center`,
`size` и `lossyScale`, поэтому остаётся корректным при перемещении и вращении двери или корабля.
Для других типов коллайдеров возвращается `PositiveInfinity` — старый точный путь сохраняется.

`InteractableManager.FindNearestDoor` пропускает дверь, если расстояние от запроса до её transform
больше `range + InteractionBoundingRadius`. Для оставшихся дверей используется прежний
`DistanceToInteraction`; порядок взаимодействий, дистанции и правило выбора ближайшей двери не изменены.

Compile и domain reload завершены, `is_compiling == false`; новых ошибок C# нет.
`git diff --check` — чисто. Play Mode, тесты и Build агентом не запускались. Величина выигрыша
будет зафиксирована только следующим захватом.


## Подтверждение broadphase захватом `ProjectC_client_2026-11-22_19-16-00.data`

434 кадра; steady-state 30..428 — 399 кадров. Сравнение с предыдущим steady-state:

| Маркер | 18-35 | 19-16 |
|---|---:|---:|
| `Player.NetworkPlayer.Update`, total/self мс/кадр | 5,105 / 3,023 | **4,270 / 2,784** |
| `NetworkPlayer.InteractionHint`, total/self мс/кадр | 1,742 / 1,650 | **1,189 / 1,160** |
| `NetworkPlayer.InteractionHint`, мс/вызов | 5,893 | **5,101 (−13,4 %)** |
| `NetworkPlayer.Movement`, total/self мс/кадр | 0,154 / 0,0176 | 0,147 / 0,0151 |

Broadphase дал измеренное снижение, но resolver остаётся горячим. В нём сохранялся полный скан
`FindObjectsByType<NpcController>`; общий маркер `FindObjectsOfType` на steady-state — 0,2005 мс/кадр.

## Поиск NPC через существующий реестр

В `NpcController` добавлен `FindNearest(Vector3, out float)`, который обходит существующий
`OnEnable/OnDisable`-реестр вместо полного скана сцены. Сохранено правило `PlayerInRange` ИЛИ
`IsWithinDistance` и выбор ближайшего NPC. `NetworkPlayer.HasNearbyNpcForInteraction` и фактический
`TryInteractNearestNpc` используют один общий поиск; приоритеты подсказки и E-flow не изменены.

Compile и domain reload завершены, `is_compiling == false`; ошибок C# нет (Error-выдача содержит
только transport-сообщения коротких MCP-подключений). `git diff --check` — чисто. Play Mode, тесты
и Build агентом не запускались. Следующий захват должен подтвердить остаточную стоимость resolver.


## Подтверждение реестра NPC захватом `ProjectC_client_2026-11-22_19-37-55.data`

1 313 кадров; steady-state 30..1307 — 1 278 кадров. Сравнение с 19-16:

| Маркер | 19-16 | 19-37 |
|---|---:|---:|
| `Player.NetworkPlayer.Update`, total/self мс/кадр | 4,270 / 2,784 | **3,922 / 2,851** |
| `NetworkPlayer.InteractionHint`, total/self мс/кадр | 1,189 / 1,160 | **0,730 / 0,727** |
| `NetworkPlayer.InteractionHint`, мс/вызов | 5,101 | **2,989 (−41,4 %)** |
| `NetworkPlayer.Movement`, total/self мс/кадр | 0,147 / 0,0151 | 0,154 / 0,0147 |

Реестр NPC дал второй измеренный результат. Основной остаток вне атрибутированных секций —
ежекадровый `FindNearestInteractable`: он выполнял `FindObjectsByType<NetworkChestContainer>`
с последующей фильтрацией и нужен UI-состоянию, поэтому сам вызов не удалён.

## Реестр сетевых сундуков

`NetworkChestContainer` регистрируется при network spawn и повторном `OnEnable`, удаляется при
`OnDisable`/`OnNetworkDespawn`. `FindInRange` сохраняет прежние проверки `activeSelf`, `IsSpawned`
и персонального `GetOpenRadius`, возвращая первый подходящий сундук. `FindNearestInteractable`
использует этот реестр; обновление `_nearest*` для UI и приоритеты F/E не изменены.

Compile и domain reload завершены, `is_compiling == false`; ошибок C# нет (Error-выдача содержит
только transport-сообщения MCP). `git diff --check` — чисто. Play Mode, тесты и Build агентом
не запускались. Следующий захват должен показать снижение self-time `Player.NetworkPlayer.Update`.


## Подтверждение реестра сундуков захватом `ProjectC_client_2026-11-22_20-27-55.data`

781 кадр; steady-state 30..775 — 746 кадров. `Player.NetworkPlayer.Update` вызывался на 686 кадрах
(остальное — состояние в корабле). Сравнение с 19-37 по steady-state:

| Маркер | 19-37 | 20-27 |
|---|---:|---:|
| `Player.NetworkPlayer.Update`, total мс/кадр | 3,922 | **0,886 (−77,4 %)** |
| `Player.NetworkPlayer.Update`, self мс/кадр | 2,851 | **0,033 (−98,8 %)** |
| `NetworkPlayer.InteractionHint`, total/self мс/кадр | 0,730 / 0,727 | 0,681 / 0,671 |
| `NetworkPlayer.Movement`, total/self мс/кадр | 0,154 / 0,0147 | 0,154 / 0,0156 |

Устранён большой неатрибутированный self-участок `FindNearestInteractable`; подсказка и движение
сохраняют прежний порядок стоимости. Общие маркеры `Main Thread`/`PlayerLoop` между прогонами
не используются для атрибуции выигрыша этой правки.

Следующие managed self-кандидаты — `MarketZone.Update` (1,382 мс/кадр, 8 206 вызовов) и
`OuterCommZone.Update` (1,134 мс/кадр, 6 714 вызовов). Их polling выполняется с периодом 0,25 с,
но каждая из 11/9 зон заново обходит `SpawnedObjects` на своём тике.
