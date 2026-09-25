# T-PERF02 — Iteration Log

База: `00_CAPTURE_2026-09-25_ANALYSIS.md`. План: `01_FIX_PLAN.md`.

## Фаза 1 — выполнена (код), замер — за пользователем

Файлы: `Assets/_Project/Scripts/Docking/Zones/OuterCommZone.cs`,
`Assets/_Project/Trade/Scripts/Network/MarketZone.cs`.

Что сделано (поведение бит-в-бит, только распределение по кадрам + GC):
1. `_pollTimer = Random.value * pollInterval` в `OnEnable` — 22 зоны больше не поллят
   синхронно в один кадр (пилы f104/f131/f225).
2. Per-instance `_pollFound` (`HashSet<ulong>`) + `_pollToRemove` (`List<ulong>`),
   `.Clear()` вместо `new` каждый полл. Физический запрос (`OverlapSphere`) не тронут.
3. Маркеры `// T-PERF02` в коде.

Верификация: domain reload свежий, Console — 0 errors (только pre-existing CS0618
`FindObjectsSortMode` в несвязанных файлах + benign MCP-нотисы).

Ручная проверка (пользователь, NOT RUN агентом):
1. Снять захват профайлера в том же сценарии (20+ кораблей, те же зоны).
2. Ожидание: пилы >50 ms от зон пропали, сумма `OuterCommZone+MarketZone` упала в разы,
   GC этих скриптов → ~0. T-key/CommPanel/маркет-детект работают как раньше.
3. Цифры записать сюда.
