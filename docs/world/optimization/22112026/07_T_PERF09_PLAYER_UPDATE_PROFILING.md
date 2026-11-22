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
