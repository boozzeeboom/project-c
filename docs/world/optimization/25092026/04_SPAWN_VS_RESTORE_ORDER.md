# Порядок спавна против персистенции — ресерч (T-PERF02)

Гипотеза пользователя («персистенция ещё не загрузилась») — **подтверждена**,
с уточнением: грузится не «ещё», а через ~3.5 с после спавна, и бьёт дважды.

## Таймлайн host-старта (факты из кода)

| t | Событие | Файл |
|---|---|---|
| ~0 с | World-сцены грузятся, scene-placed NPC-корабли авто-спавнятся (`ScenePlacedObjectSpawner`); `ShipDeckNav.OnNetworkSpawn` ставит регистрацию в очередь; spawn-Add ×20 | `NpcShipServer.cs:56`, `ShipDeckNav.cs:OnNetworkSpawn` |
| ~0 с | Корабли спавнятся раньше серверов: `CombatServer.Instance==null` ×19, `MetaRequirementRegistry==null` ×19+, `GatheringServer==null` ×3, `InventoryWorld==null` ×3 — всё «deferred (pending)», каждый с варнингом + `CallLogCallback` + стектрейс | лог `оптим_сент_4.txt` |
| ~0 с | Экипаж спавнится (`ShipCrewSpawner:154` ×20), прокси-агенты создаются (после фикса — сразу на месте; до фикса — в origin) | `ShipCrewSpawner.cs:148`, `NpcBrain.cs:EnsureProxy` |
| ~3.5 с | `RestoreCoroutine` просыпается (`restoreDelaySec = 3.5`), грузит сейв, `ApplyRestore` **телепортирует** корабли на сохранённые позиции (км) | `ShipPositionServer.cs:45,161-217,396` |
| ~3.5 с | Телепорт срывает дрейф-проверку палуб (>2500 м) → drift-Add ×20 (до кулдаун-фикса — сразу; после — через 30 с; счётчики: `spawn=20 drift=20`) | счётчики `DumpStats()` |
| каждый Add | 6–50 ms + ~1.8 MB логов + реасессмент голодающих агентов (≈1 «Failed to create agent») | замеры 3–4 |

## Что это значит

1. Парный Add (spawn + drift) — структурный, не случайный. Кулдаун-фикс его только
   отложил (33.5 с), не убрал: после телепорта меш genuinely stale, перерегистрация нужна.
2. Правильное лечение — **не регистрировать до телепорта**: отложить начальную
   регистрацию палуб до `ShipPositionServer.RestoreCompleted` (см. ниже).
3. Спавн-гонки серверов — отдельная, но родственная тема: варнинги ожидаемы
   («deferred»), но каждый стоит колбэка со стектрейсом. Кандидаты на downgrade
   до `_debugLog`: `ShipHull.cs:225`, `ShipOwnershipRequirement.cs:78`,
   `ResourceNode.cs:178`, `ResourceNodeConfig.cs:142/149`, `MetaRequirement.cs:187`,
   `CraftingStation.cs:78`. НЕ трогать логику pending — только уровень шума.
4. `fo-rebuild/fo-restore = 0` — FO-путь в этой лавине не участвует; но механизм
   `NotifyWorldRebased → сброс кулдаунов → drift-Add ×N` остаётся взведённым
   на каждый rebase (см. `03_SHIPDECKNAV_REREG.md`).

## Реализация: отложенная регистрация (принято)

`ShipDeckNav.OnNetworkSpawn`: если сервер и `ShipPositionServer` ещё не завершил
restore — не вставать в очередь, ждать в `LateUpdate` флага `RestoreCompleted`
(дешёвая проверка bool). Страховки: `Instance==null` → сразу как раньше;
таймаут 60 с → регистрация принудительно (restore гарантированно завершается,
но вечное ожидание недопустимо конструкцией). Клиентский путь не меняется.
Ожидание: `spawn`-Add встаёт на финальную позицию → `drift`-Add исчезает
(`drift≈0` в `DumpStats`), −20 Add и −20 варнинг-штормов за сессию.
Цена: экипажная навигация стартует на ~5 с позже (невидимо).
