# Ship System — план исправления по ревью 2026-09-18

> База: `docs/Ships/SHIP_CODE_REVIEW_2026-09-18.md` (вердикт CHANGES REQUIRED).
> Принцип (твое условие): **технически всё работает → ничего не чиним вслепую.**
> Каждый пункт проходит ворота: **перепроверка → утверждение → фикс → дока + коммит.**
> Без аппрува код не трогаем. Пуш — никогда (только локальный коммит через Git CLI).

## Конвейер одного шага (обязателен для всех тикетов ниже)

1. **Перепроверка (docs only).** Открыть указанные строки, подтвердить/опровергнуть находку на текущем `main`.
   Итог — мини-дока `docs/Ships/fix/<TICKET>_<slug>.md` со статусом `ПОДТВЕРЖДЕНО / ЧАСТИЧНО / ОТКЛОНЕНО`.
2. **Утверждение.** Ты пишешь «давай» по тикету (или правишь scope). Без этого — стоп.
3. **Фикс.** Минимальный дифф, только файлы тикета. Никаких попутных рефакторингов/форматирований.
4. **Дока + коммит.** Обновить `docs/Ships/ITERATIONS.md` (одна секция на тикет) + коммит
   `"<TICKET>: <что сделано>"` через Git CLI, только относящиеся файлы. Затем проверка:
   Compile (Console → 0 errors) → Tests (Test Runner, делает пользователь) → Manual (сцена/действия/ожидание из доки шага).

Отклонённые при перепроверке пункты — тоже коммитим (docs only), чтобы след остался.

## Нулевой шаг (уже готов, ждёт коммита)

- `T-SHIP-REVIEW: Ship code review 2026-09-18 (docs only)` — файл `SHIP_CODE_REVIEW_2026-09-18.md` уже создан, в `git status` как untracked.
  Коммит отдельным шагом после твоего ок: `git add docs/Ships/SHIP_CODE_REVIEW_2026-09-18.md` + этот план.

---

## Фаза A — несостыковки в доках (docs only, безопасно, можно пачкой по 2–3)

Каждый тикет: сверить параграфы, вычеркнуть устаревшее, поставить кросс-ссылки. Кода нет.

| Тикет | Что перепроверить и утвердить | Дока шага |
|---|---|---|
| `T-SHIP-DOC01` | Key OVERVIEW §§1/6/7/8 vs P1-факт (§§2.1/2.4/12): рецепт `ShipKeyBinding/ShipKeyServer/[Ship_KeyServer]` мёртв? Удалить/пометить устаревшим | `docs/Ships/fix/T-SHIP-DOC01_key-overview.md` |
| `T-SHIP-DOC02` | Где создаётся instance: `21 §§2-3` (Binding на `[KeyRod_*]`) vs P1 (`ShipController.OnNetworkSpawn`). `21` — в архив? | `.../T-SHIP-DOC02_key-creation.md` |
| `T-SHIP-DOC03` | `28` (выкинуть World → KeyRegistry, 13ч) vs P1-факт (World = SSOT). Закрыть `28` как нереализованный, обновить метрики | `.../T-SHIP-DOC03_key-28-vs-p1.md` |
| `T-SHIP-DOC04` | Cargo «без ключа» (`Key 00 §1.3`) vs P5 guard (`NotOwner=36`). Обновить таблицу | `.../T-SHIP-DOC04_cargo-key-table.md` |
| `T-SHIP-DOC05` | Guard: план P5 (`ShipCargoServer + TradeWorld`) vs факт (`ShipCargoServer + MarketServer`). TradeWorld-уровень закрыт или нет? | `.../T-SHIP-DOC05_cargo-guard-scope.md` |
| `T-SHIP-DOC06` | Engine vs Broken: OFF=падение / fuel==0=OFF vs Broken=летит на 10% с полным расходом + мезий 100%. Зафиксировать x10-расход, кросс-ссылку | `.../T-SHIP-DOC06_engine-vs-broken.md` |
| `T-SHIP-DOC07` | L1 visual: `00_SUMMARY ❌` vs P4 `done` vs `Modul_system/01` (молчит). Editor-preview или runtime `VisualApplier`? | `.../T-SHIP-DOC07_l1-visual-status.md` |
| `T-SHIP-DOC08` | Bootstrap: `Modul_system/02 §1.3` (создать окно в Bootstrap) vs запрет `T-ENG02`. Какой гайд жив? | `.../T-SHIP-DOC08_bootstrap-rule.md` |
| `T-SHIP-DOC09` | Recall: `02 §6` (телепорт+кредиты) vs Persist/Dock/Ownership. Дописать связи или признать разрыв | `.../T-SHIP-DOC09_recall-links.md` |
| `T-SHIP-DOC10` | Cargo P3: старые доки (`roadmap-integration:200` и др. «CargoSystem отсутствует») — пометить устаревшими | `.../T-SHIP-DOC10_cargo-p3-stale.md` |

Проверка фазы A: только чтение доков, 0 errors тривиально. Коммиты `docs(ship): T-SHIP-DOC0X ...`.

## Фаза B — P0 безопасность/сеть (строго по одному, каждый с репродом)

| Тикет | Ворота перепроверки (подтвердить до фикса) | Scope фикса (после аппрува) | Manual-проверка |
|---|---|---|---|
| `T-SHIP-FIX01` | Серверная клавиатура: `ShipController.cs:1324,1352-1396,2128-2133` в серверном `FixedUpdate`. Хосту нажать L/C/V при чужом полёте — чужой корабль реагирует? | Мезия/ролл/дозаправка → RPC от владельца; удалить `GetCurrentPitchInput:2139`/`GetCurrentYawInput:2149` | Два клиента + хост: жмёт только владелец — летит; хост жмёт — чужой стоит |
| `T-SHIP-FIX02` | `SubmitShipInputRpc:1553` без Clamp; `AddPilot:1892` без ownership. Шлём `thrust=1e6` с мод-клиента — сервер принимает? | `Clamp(-1,1)` в RPC; ownership/authority-check в `AddPilot` (+ `RemovePilot`) | Мод-клиент с большим thrust — скорость как при 1.0; чужой `AddPilot` — отказ |
| `T-SHIP-FIX03` | Client-цены: `ShipModuleServer:227` (`sellCredits`), `:388/:464` (`cost`), `TryModifyCredits(-cost)` при `cost<0` начисляет? Подменённый `sellCredits=int.MaxValue` принимается? | Серверный прайс из `ModuleShopDatabase`; клиентскую цифру игнорировать/сверять; отклонить `cost<=0`; rate-limit + дистанция до NPC | Подмена `sellCredits/cost` — сервер режет; `cost=0/-100` — отказ, баланс не растёт |
| `T-SHIP-FIX04` | `RecallShipToPadServerRpc:2381-2422`: `padPosition/cost` от клиента, владения/пада нет. Телепорт чужого за 0 — воспроизводится? | Ownership-check, серверный `cost`, пад из реестра, списание до телепорта, синхрон через `NetworkTransform` | Чужой recall — отказ; свой — кредиты минус, корабль на валидном паде |
| `T-SHIP-FIX05` | Двойной dt: `:2033` `AddForce(... * dt, ForceMode.Force)`. Замер тяги мезии до/после — слабее ~x50? | Убрать `* dt` | Одинаковый импульс даёт ~x50 прирост (замер скорости до/после) |
| `T-SHIP-FIX06` | Топливо stale: `ShipFuelSystem:33` plain float, клиент читает `CurrentFuel` в HUD (`ShipController:1887`). Хост тратит — у клиента цифра стоит? | `NetworkVariable` на топливо ИЛИ запрет прямых чтений (только телеметрия) | Хост жжёт топливо — клиентский HUD едет синхронно |
| `T-SHIP-FIX07` | Телеметрия-шторм: `cargoDetail[32]` в `NetworkVariable` 5 Гц (`ShipController:931,1020-1143`); `Equals` без `lastUpdateServerTime`, `position` точно (`ShipTelemetryState:132-193`). Профайлер: трафик растёт с N кораблей? | Детали груза → on-demand RPC; починить `Equals/GetHashCode`; cap 32 + индикатор обрезки | N кораблей — трафик плоский по грузу; Heavy+модули — хвост не теряется молча |

Порядок B: FIX03+FIX04 (деньги/телепорт — самые эксплуатируемые) → FIX01+FIX02 (ввод) → FIX05/06/07.
Каждый — отдельный коммит `T-SHIP-FIX0X: ...`.

## Фаза C — P1 баги логики (по одному)

| Тикет | Перепроверка | Фикс | Manual |
|---|---|---|---|
| `T-SHIP-FIX08` | `OnModuleChangedClientRpc:305-341` в обход `Manager` (energy/compat); `Notify*` — `SendTo.Everyone` вместо TargetRpc (`:360-375`); двойная работа на хосте (`:329-331`) | Валидация через `Manager`; TargetRpc; хост-гард | Граничный модуль (не хватает энергии) — сервер и клиенты一致 (одинаково отказывают) |
| `T-SHIP-FIX09` | `MeziyModuleActivator:90-134` stale после install/remove; ключ `moduleId` вместо слота (`:84,113`); топливо разовое vs `/сек` (`:156` vs `:277`) | Подписка на `OnModuleChanged`; ключ по слоту; единые единицы топлива | Поставил мезий в доке — работает без рестарта; снял — погас; два одинаковых — оба живут |
| `T-SHIP-FIX10` | `ShipCargoConsoleWindow:397-419` считает слоты, не стаки; `RefreshData` до телеметрии (`:623-633` + 200мс лаг); `Retrieve` O(n) (`ShipCargoServer:306-319`); груз с любой точки (гейта IsDocked нет) | Считать стаки; refresh по событию телеметрии; батч-retrieve; зафиксировать решение по дистанции | Стакающиеся предметы — цифры сходятся; UI не мигает stale |
| `T-SHIP-FIX11` | Лимиты: `TradeWorld.TryAdd/GetSpeedPenalty` читают статику или `ShipCargoRegistry.GetEffectiveLimits`? Курс: клиент `Default` vs сервер `_exchangeRateConfig` | Один источник лимитов (реестр); один конфиг курса | Heavy + расширитель: HUD max = серверный max; «packable» везде одинаков |
| `T-SHIP-FIX12` | Застывшая поза (`PartShake:99-100`, `ThrusterVisual:130-131`); `angularVelocity.y` мировая (`:148-152`); `IsGrounded:1866` (1.5м без маски); `ClampPitchAngle` без dt/массы (`:1771,1776`); `maxLiftForce` размерность (`:1470`); геттер `ShipPersistentId:86-96` с сайд-эффектом | Сброс позы при OFF; локальная ось; маска+длина; dt+инерция; чистый геттер | OFF — детали в нуле; крен не врёт; Heavy поворачивает как задумано |

## Фаза D — перф/чистка (батчится, низкий риск)

| Тикет | Состав |
|---|---|
| `T-SHIP-FIX13` | `verboseLogging` дефолт `false` (оба SO); удалить `repairCostCredits`; `MeziyThrusterVisualEditor` → `Editor/`; комменты EN→RU в `Contrail`; нейминг (`_camelCase`, `PascalCase`) — отдельным диффом, без логики |
| `T-SHIP-FIX14` | Визуалы client-only guard (`Contrail/PartShake/Thruster/Meziy/VisualApplier`); `VFX ApplyVfxScale` из кадра (таймер); `GetComponentsInChildren` из кадров (`OverflowAlpha`, `ApplyShipColor` — кэш); `CreatePrimitive` → префабы/пул; `FindSlot/ValidateOwnership` хелперы; `Default`-лоадер generic; `GetActiveCorridor` → `sqrMagnitude` |
| `T-SHIP-FIX15` | Подписки: отписки `OnHullChanged/OnCargoChanged`, стоп корутин в `OnNetworkDespawn`, отписка `TelemetryClientState`, `DontDestroyOnLoad`/дубликат-синглтон, чистка `s_pendingRegistrations`, `Unregister` в `CargoRegistry/RepairManager`, `ShipDeckNav` N-за-кадр → 1, убрать глобальный `SetStackTraceLogType`, `AutoSave` не на каждую мутацию, `instanceId` персист, `ShipPosition` split-brain (модули/карго/цвет в снапшот), файловый IO с пути пилота |

## Фаза E — архитектура (только после B+C, отдельным решением)

- `T-SHIP-ARCH01`: разрез `ShipController` (God Object) — `ShipFlightPhysics` / `ShipDocking` / `ShipTelemetryPublisher` / `ShipRecallService`; декомпозиция `FixedUpdate` до методов ≤40 строк.
- Правило: дизайн-нота в `docs/dev/` до кода; рефакторинг только после зелёных B+C и с твоего аппрува.

## Карта коммитов (итого ~20)

- 1 коммит ревью (этот план + `SHIP_CODE_REVIEW_2026-09-18.md`, docs only).
- 10 × `T-SHIP-DOC0X` (docs only, можно группировать по 2–3).
- 7 × `T-SHIP-FIX01..07` (по одному, код).
- 5 × `T-SHIP-FIX08..12` (по одному, код).
- 3 × `T-SHIP-FIX13..15` (батч-чистки).
- E — отдельно, после всего.

Каждый код-коммит: `git status` + `git diff` → стейдж только файлов тикета → `ITERATIONS.md` секция → коммит. Push — нет.

## Что нужно от тебя сейчас

1. Ок на этот план (или правки по порядку/скоупу).
2. С какого тикета начинаем: предлагаю `T-SHIP-REVIEW` (коммит ревью+план) → `T-SHIP-DOC01..03` (ключи — самые запутанные доки).
3. Дальше я иду строго по конвейеру: перепроверка одного тикета → показываю мини-доку → жду «давай» → фикс → коммит.
