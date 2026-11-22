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
