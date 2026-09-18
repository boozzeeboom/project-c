# Project C — Iterations Log

## Итерация от 2026-07-24 (T-PERSIST-FIX)

**Задача:** Позиции кораблей перестали сохраняться — файл `ShipPositions.json` не создавался после перезапуска/очистки.

**Коммит:** `054386c` — T-PERSIST-FIX: deadlock _restoreCompleted + ThreadPool persistentDataPath

**Изменения:**
- `ShipPositionServer.cs` — `RestoreCoroutine`: `_restoreCompleted` выставляется даже при пустом save  
- `ShipPositionServer.cs` — `Update()`: синхронный save вместо ThreadPool
- `ShipPositionRepository.cs` — `SaveAll(wrapper)`: добавлен `Debug.Log`, убран `throw`

**Корневые причины:**
1. Deadlock `_restoreCompleted`: нет файла → нет кораблей для restore → `_restoreCompleted=false` → `Update()` не сохраняет → файл никогда не создаётся
2. ThreadPool: `Application.persistentDataPath` на фоновом потоке падал → файл писался в корень проекта мимо `persistentDataPath`

## Итерация от 2026-07-24

**Задача:** ShipController custom editor — группировка ~45 serialized полей в 8 foldout-секций + Runtime Info панель
**Коммит:** `86a87f3` — T-SHIP01: ShipController custom editor — 8 foldout-групп + Runtime Info панель
**Изменения:**
- `Assets/_Project/Scripts/Player/Editor/ShipControllerEditor.cs` (новый)
- `docs/Ships/shipcontroller-editor.md` (новый)

## Итерация от 2026-07-06 (Сводка периода 1–6 июля)

**Задача:** Подробное саммари периода 1–6 июля: анализ 67 коммитов, перекрёстная документация, сводка в `docs/dev/summary_01-06_july_2026.md`

**Коммит:** `b399b77` — docs: сводка разработки 1–6 июля 2026

**Файл:** `docs/dev/summary_01-06_july_2026.md` (560 строк, 27 KB)

**Охваченные подсистемы (13):**
- Ветер на корабли и персонажа
- Физика персонажа на палубе (PlatformRideHelper, единый Move)
- NPC на палубе (прокси-агент, NavMesh fix, anchor)
- Cargo: 4 эпика (UI-01, UI-02, VIS-01, NPC-01)
- MARKET-ID-REFACTOR (нормализация + авто-сбор)
- Cleanup (warnings ×15 файлов, debug логи, reflection → прямой доступ)
- Repair Manager (модули, камера наблюдения, repaint)
- Module Visual Preview (Editor tool)
- Двигатель ON/OFF + IDLE
- Ship Damage Subsystem (HP, столкновения, ремонт)
- SHIP_REFACTOR_PLAN P1–P5
- Переход на Unity 6000.5.2f1

**Ссылки на документацию:** 17 документов перекрёстно связаны в саммари.

---

## Итерация от 2026-07-21
=======
REPLACE

**Задача:** P1 Refactor Key Subsystem — удаление 7 obsolete/дублирующих файлов, приведение к single source of truth (KeyRodInstanceWorld)

**Ветка:** `refactor/key-subsystem-p1-2026-07-21` → merged to main

**Коммиты:**
- `9b7cf18` — docs: P1 analysis (5 проблем, 4-шаговый план)
- `d04c5e8` — refactor: удалить Obsolete legacy (ShipKeyBinding/Server/ClientState/Toast)
- `f97bdcf` — refactor: fix registeredShipId=0 при fallback CreateInstance
- `37f25a2` — refactor: удалить ShipOwnershipRegistry, ownerClientId из telemetry
- `6742a84` — refactor: удалить KeyRodInstanceBinding, ShipController создаёт instance
- `01a4d13` — fix: guard от дубликата ключа, корутина CreateKeyInstanceWhenReady
- `af0fd55` — docs: обновлены 00_OVERVIEW, 99_CHANGELOG, SHIP_REFACTOR_PLAN

**Изменения:**
- Удалено 7 файлов: ShipKeyBinding.cs, ShipKeyServer.cs, ShipKeyClientState.cs, ShipKeyToast.cs, ShipOwnershipRegistry.cs, KeyRodInstanceBinding.cs (+ .meta)
- Изменено: NetworkManagerController.cs, NetworkPlayer.cs, ShipController.cs, PickupItem.cs, InventoryWorld.cs, ShipTelemetryClientState.cs
- Создано: 31_KEY_ANALYSIS_2026-07-21.md, SHIP_REFACTOR_PLAN_2026-07-21.md
- Обновлено: 00_OVERVIEW.md, 99_CHANGELOG.md

**Итог:** -1139 строк, +651 строк (net -488). 0 reflection. 1 source of truth.

---

## Итерация от 2026-07-21 (P2+P3)

**Задача:** P2 — анализ speed penalty fix + удаление CargoSystem; P3 — актуализация документации

**Ветка:** `refactor/p3-doc-update` → merged to main

**Коммит:** `3e7aa92` — docs(ship): P3 — актуализация документации (CargoSystem, Key-subsystem, roadmap)

**P2 Анализ (без изменений кода):**
- CargoSystem.cs уже удалён (T-CARGO-05)
- ShipController уже использует _serverCargoPenalty NetworkVariable (T-CARGO-03)
- Цепочка penalty: TradeWorld.GetSpeedPenalty → OnCargoChanged → RecalculateCargoPenalty → _serverCargoPenalty → ApplyThrustForce
- ShipCargoRegistry для per-instance лимитов (T-CARGO-06)
- cargoPenalty не применяется к ClampSpeed — осознанное решение (влияет только на разгон)
- Ссылок CargoSystem в .unity/.prefab нет

**P3 Изменения:**
- roadmap-integration.md: T-CARGO-01..05 → T-CARGO-01..06, +ShipCargoRegistry
- legacy/AGENTS_SHIP_SYSTEM_SUMMARY.md: +ссылка на SHIP_REFACTOR_PLAN_2026-07-21.md
- Key-subsystem/00_OVERVIEW.md §12: миграция MetaRequirement — ЗАВЕРШЕНА

**Итог:** P2 закрыт без изменений кода (всё уже реализовано). P3: 3 документа актуализированы.

---

## Итерация от 2026-07-21 (P5)

**Задача:** P5 — Cargo ownership/security guard

**Ветка:** main (прямой коммит)

**Коммит:** `f4d2c9f` — feat(ship): P5 — cargo ownership guard (ShipCargoServer + MarketServer)

**Изменения:**
- `TradeResultCode.cs`: +`NotOwner = 36`
- `ShipCargoServer.cs`: `IsOwnerOfShip` guard в `RequestStoreToCargoRpc` + `RequestRetrieveFromCargoRpc`
- `MarketServer.cs`: `IsOwnerOfShip` guard в `RequestLoadToShipRpc` + `RequestUnloadFromShipRpc`
- `CARGO_OWNERSHIP_DESIGN.md`: диздок (новый)

**4 метода защищены:**
| Файл | Метод | Ошибка |
|------|-------|--------|
| ShipCargoServer | RequestStoreToCargoRpc | "Вы не владелец этого корабля" |
| ShipCargoServer | RequestRetrieveFromCargoRpc | "Вы не владелец этого корабля" |
| MarketServer | RequestLoadToShipRpc | TradeResultCode.NotOwner |
| MarketServer | RequestUnloadFromShipRpc | TradeResultCode.NotOwner |

**Итог:** +~40 строк, 4 ownership guard'а. Без циклических зависимостей.

---

## Итерация от 2026-07-14 (T-ENG02 — анализ engine visual)

**Задача:** Глубокий анализ подсистем для проектирования modular engine visual. Предотвращение повторения ошибок T-ENG01.

**Файл:** `docs/Ships/customisation/02_ENGINE_VISUAL_ANALYSIS_AND_PLAN.md`

**Анализ (8 подсистем):**
| Подсистема | Статус | Вывод |
|---|---|---|
| SlotType / ModuleType enums | ✅ Есть, нужно добавить Engine | Добавить в конец обоих (позиция 3) |
| ShipModule (SO) visual поля | ✅ Уже есть (visualPrefab, offsets, attachAxis) | Использовать как есть |
| ShipModuleVisualApplier (L1) | ✅ Уже есть (196 строк, спавн/уничтожение) | Автоматически заспавнит prefab на Engine-слоте |
| ShipController thrust chain | ✅ Работает, server-authoritative | Читать через ShipInputReader, НЕ через ShipController |
| ShipInputReader | 🟡 Нет публичных геттеров | Добавить ThrustNormalized / YawNormalized |
| ShipTelemetryState | 🟡 Нет thrustNormalized | Опционально, отдельным тикетом |
| EngineThrusterVisual | ❌ Новый компонент | Создать, client-side only, без Rigidbody |
| BootstrapScene | 🚫 НЕ ТРОГАТЬ | Залочена. Вся работа — в WorldScene. |

**Главные изменения:**
- Создан `02_ENGINE_VISUAL_ANALYSIS_AND_PLAN.md` (308 строк) — полный анализ архитектуры
- Определён паттерн: `EngineThrusterVisual` на `ModuleSlot` → `ShipRootReference` → `ShipInputReader` (read-only)
- Правило: НЕ модифицировать Transform/Rigidbody/RPC в EngineThrusterVisual
- Правило: НЕ трогать BootstrapScene
- Определён чеклист проверки (14 пунктов)

**Решение по архитектуре (ticket-based):**
- T-ENG02a: SlotType.Engine + ModuleType.Engine (enums)
- T-ENG02b: ShipInputReader публичные геттеры
- T-ENG02c: EngineThrusterVisual компонент
- T-ENG02d: ShipTelemetryState.thrustNormalized (опционально)
- T-ENG02e: Настройка в сцене (WorldScene_0_0)

---

## Итерация от 2026-09-18 (T-SHIP-REVIEW)

**Задача:** Полноценное код-ревью кораблей (ядро + модули/карго/ключи/телеметрия) + план исправления.
Вердикт: CHANGES REQUIRED (P0 — читы/десинхрон, не вкусовщина).

**Файлы (docs only, кода нет):**
- `docs/Ships/SHIP_CODE_REVIEW_2026-09-18.md` (новый) — Standards 4/9, Unity/Arch/SOLID issues,
  7×P0 (серверная клавиатура `:1324,1352-1396`; `SubmitShipInputRpc:1553` без Clamp; client-цены
  `ShipModuleServer:227,388,464` + `-cost` фарм; `Recall:2381` без владения; двойной dt `:2033`;
  топливо plain float; телеметрия 5 Гц full-snapshot), P1 баги, 10 несостыковок в доках.
- `docs/Ships/SHIP_FIX_PLAN_2026-09-18.md` (новый) — конвейер шага
  (перепроверка → утверждение → фикс → дока + коммит), фазы A (DOC01-10) / B (FIX01-07) /
  C (FIX08-12) / D (FIX13-15) / E (ARCH01), ~20 коммитов. Без аппрува код не трогаем.

---

## Итерация от 2026-09-18 (T-SHIP-DOC01)

**Задача:** Перепроверка несостыковки №1 (Key OVERVIEW vs P1-факт). Статус: ПОДТВЕРЖДЕНО.

**Перепроверка:** файлов `ShipKeyBinding/Server/ClientState/Toast`, `ShipOwnershipRegistry`,
`KeyRodInstanceBinding` в `Assets/` нет (только комменты); создание — `ShipController.cs:812,855,876`
(`CreateKeyInstanceWhenReady`); `§§1.1a/1.1b/6.1/6.2/6.4/7/8` описывают удалённый API.

**Изменения (docs only):**
- `docs/Ships/fix/T-SHIP-DOC01_key-overview.md` (новый) — протокол перепроверки.
- `docs/Ships/Key-subsystem/00_OVERVIEW.md` — баннеры `⚠️ УСТАРЕЛО (P1 2026-07-21)` на 7 параграфах,
  текст сохранён как история. Stale комменты в коде — НЕ этот тикет (уйдут в T-SHIP-FIX13).

---

## Итерация от 2026-09-18 (T-SHIP-DOC02)

**Задача:** Перепроверка несостыковки №2 (где создаётся instance: `21 §§2–3` vs P1). Статус: ПОДТВЕРЖДЕНО ЧАСТИЧНО.

**Перепроверка:** `21_` — предизайн 2026-06-18 («код НЕ написан»); точка создания
`KeyRodInstanceBinding.OnNetworkSpawn` + legacy `ShipKeyServer/ClientState/Registry` устарели;
модель владения/трансфер (`§§3.3–3.4`, `§5`) актуальна как дизайн; P1-факт — `ShipController.cs:812,855,876`.

**Изменения (docs only):**
- `docs/Ships/fix/T-SHIP-DOC02_key-creation.md` (новый) — протокол перепроверки.
- `docs/Ships/Key-subsystem/21_SHIP_OWNERSHIP_MODEL.md` — шапка + баннеры на `§§2.1/2.5/3/4`,
  текст сохранён как история.

---

## Итерация от 2026-09-18 (T-SHIP-DOC03)

**Задача:** Перепроверка несостыковки №3 (`28` vs P1: выкинуть World?). Статус: ПОДТВЕРЖДЕНО.

**Перепроверка:** `28` (2026-06-19) предлагает `KeyRegistry/KeyInstance`, Phase D — удалить
`KeyRodInstanceWorld/Instance/Repository` + `ShipOwnershipRequirement`; P1 (2026-07-21) сделал
наоборот (World = SSOT, удалены только обёртки); файлов `KeyRegistry.cs`/`KeyInstance.cs` нет;
метрики `§6` невалидны после P1.

**Изменения (docs only):**
- `docs/Ships/fix/T-SHIP-DOC03_key-28-vs-p1.md` (новый) — протокол перепроверки.
- `docs/Ships/Key-subsystem/28_KEY_ARCHITECTURE_REVIEW.md` — шапка + баннеры на `§§5/6/7/9/10`
  («не реализовано / невалидно после P1»), текст сохранён как история.

---

## Итерация от 2026-09-18 (T-SHIP-DOC04)

**Задача:** Перепроверка несостыковки №5 (карго «без ключа» vs P5). Статус: ПОДТВЕРЖДЕНО.

**Перепроверка (grep):** guard на месте — `ShipCargoServer.cs:115,242` (отказ «Вы не владелец»),
`MarketServer.cs:183,211` (`TradeResultCode.NotOwner=36`, `TradeResultCode.cs:41`);
бонус: `ContractServer.cs:252,304,323`. Строка `Key 00 §1.3` «не требуют ключа» ложна.

**Изменения (docs only):**
- `docs/Ships/fix/T-SHIP-DOC04_cargo-key-table.md` (новый) — протокол перепроверки.
- `docs/Ships/Key-subsystem/00_OVERVIEW.md §1.3` — строка карго исправлена на P5-статус.

---

## Итерация от 2026-09-18 (T-SHIP-DOC05)

**Задача:** Перепроверка несостыковки №6 (scope guard: план TradeWorld vs факт MarketServer).
Статус: ПОДТВЕРЖДЕНО (разрыв план/факт).

**Перепроверка (чтение кода):** guard на RPC-слое (`ShipCargoServer.cs:115,242`,
`MarketServer.cs:183,211`); `TradeWorld.TryLoadToShipCore (:753-790)` /
`TryUnloadFromShipCore (:881+)` — только `InvalidArgs`/`NotInZone`, `IsOwnerOfShip` нет
(прямой доменный вызов обходит владение). Попутно: P1-check из ревью закрыт —
сервер использует effective лимиты (`TryCheckEffectiveCargoLimits:803-849` + `SetLimitsOverride`).

**Изменения (docs only):**
- `docs/Ships/fix/T-SHIP-DOC05_cargo-guard-scope.md` (новый) — протокол перепроверки.
- `docs/Ships/cargo_system/CARGO_OWNERSHIP_DESIGN.md §2` + `SHIP_REFACTOR_PLAN_2026-07-21.md` шаг 5.2 —
  баннеры (RPC-слой закрыт, TradeWorld-уровень — разрыв, кандидат в T-SHIP-FIX11).

---

## Итерация от 2026-09-18 (T-SHIP-DOC06)

**Задача:** Перепроверка несостыковки №7 (Engine vs Broken). Статус: ПОДТВЕРЖДЕНО.

**Перепроверка (чтение доков):** ENGINE §2.5 (`fuel==0` → авто-OFF, расход всегда) vs
DAMAGE 00 §4/§4.1 (Broken: двигатель работает, ×0.1) — кросс-эффект (~x10 топлива
на дистанцию → авто-OFF → падение) нигде не зафиксирован, ссылок нет. Баланс не трогаем.

**Изменения (docs only):**
- `docs/Ships/fix/T-SHIP-DOC06_engine-vs-broken.md` (новый) — протокол перепроверки.
- Кросс-баннеры: `ENGINE_POWER_STATE.md §2.5` ↔ `damage_subsystem/00_DESIGN.md §4.1`.

---

## Итерация от 2026-09-18 (T-SHIP-DOC07)

**Задача:** Перепроверка несостыковки №8 (L1 visual done/не начат). Статус: ПОДТВЕРЖДЕНО (дока устарела, код готов).

**Перепроверка (grep кода):** L1 реализован (P4 21.07) — `ShipModule.cs:125-127` (`visualPrefab`),
`ShipModuleVisualApplier.cs` (`:81,108` runtime спавн), `ModuleSlotEditor.cs` (Preview);
`02_ENGINE_VISUAL_ANALYSIS §2.4` — «✅ Полностью готов». Устарел `customisation/00_SUMMARY.md`
(04.07, до P4): строки 17/36/51-52. Остаток (пул, client-guard) — кандидат в T-SHIP-FIX14.

**Изменения (docs only):**
- `docs/Ships/fix/T-SHIP-DOC07_l1-visual-status.md` (новый) — протокол перепроверки.
- `docs/Ships/customisation/00_SUMMARY.md` — шапка-баннер (L1-строки = предистория).

---

## Итерация от 2026-09-18 (T-SHIP-DOC08)

**Задача:** Перепроверка несостыковки №9 (Bootstrap: гайд vs запрет). Статус: ПОДТВЕРЖДЕНО ЧАСТИЧНО.

**Перепроверка:** гайд `02 §1.3` даёт альтернативу («BootstrapScene **или** DontDestroyOnLoad»);
`RepairManagerWindow.cs:30,96-99` — синглтон без `DontDestroyOnLoad` в коде; запрет T-ENG02 в силе.
Противоречие снимается выбором второго варианта, но приоритет не задан.

**Изменения (docs only):**
- `docs/Ships/fix/T-SHIP-DOC08_bootstrap-rule.md` (новый) — протокол перепроверки.
- `docs/Ships/Modul_system/02_REPAIR_MANAGER.md §1.3` — приоритет (DDOL/WorldScene по умолчанию,
  Bootstrap — с аппрува). `DontDestroyOnLoad` в коде — кандидат в T-SHIP-FIX15.

---

## Итерация от 2026-09-18 (T-SHIP-DOC09)

**Задача:** Перепроверка несостыковки №10 (Recall vs Persist/Dock/Ownership). Статус: ПОДТВЕРЖДЕНО.

**Перепроверка (код + док):** `RecallShipToPadServerRpc:2381-2422` — `padPosition/cost` от клиента,
владения/валидации пада нет; `02 §6.2-6.3` описывают доверие клиенту; связи с
`ShipPositionServer`, `postUndockGrace` отсутствуют (дропдаун — клиентский фильтр).

**Изменения (docs only):**
- `docs/Ships/fix/T-SHIP-DOC09_recall-links.md` (новый) — протокол перепроверки.
- `docs/Ships/Modul_system/02_REPAIR_MANAGER.md §6.1` — баннер (кандидат в T-SHIP-FIX04).

---

## Итерация от 2026-09-18 (T-SHIP-DOC10)

**Задача:** Перепроверка несостыковки №4 (Cargo P3 stale-доки). Статус: ПОДТВЕРЖДЕНО ЧАСТИЧНО.

**Перепроверка:** трёх файлов нет в `docs/Ships/` — удалены в `a86247a7`
(T-DOCS01, ~180 файлов в архив); `CARGO_DIAGNOSIS §TL;DR:22` — исторический срез 17.06
(сам диагноз актуален); строки P3 168–170 невыполнимы (файлов нет), остальной P3 выполнен (`3e7aa92`).

**Изменения (docs only):**
- `docs/Ships/fix/T-SHIP-DOC10_cargo-p3-stale.md` (новый) — протокол перепроверки.
- `SHIP_REFACTOR_PLAN_2026-07-21.md` P3-таблица — строки 168–170 зачёркнуты (архив T-DOCS01).
- Фаза A (T-SHIP-DOC01..10) — **ЗАКРЫТА**. Дальше фаза B (FIX01..07) — строго по одному с репродом,
  каждый требует отдельного твоего «давай».

---

## Итерация от 2026-09-18 (T-SHIP-FIX03)

**Задача:** P0 — серверный прайс в `ShipModuleServer` (фарм через client-цены). Статус: ИСПРАВЛЕНО (код).

**Перепроверка (до фикса, чтением — ПОДТВЕРЖДЕНО):** `RequestSellModuleRpc:227` кредитовал
` sellCredits` клиента; `RequestRepaintShipRpc:388` / `RequestRepairHullRpc:464` списывали `cost`
клиента (`cost=0` бесплатно, `cost<0` = начисление). Источники клиентских цен:
`RepairManagerWindow.ComputeSellPrice:750` (`max(1,cost/2)`), `RepairManager._repaintCost=500`,
`_hullRepairCost=300`. Попутно: install кредитов не списывает вообще (цена в UI есть) — НЕ этот
тикет. `ModuleShopEntry` (`[Obsolete]`): grep — 0 использований, `ShopEntry_*.asset` — 0 файлов,
редактор уже на `ShipModule` → точно мёртв, снос в T-SHIP-FIX13 (нужен `.meta`-аккуратный подход).

**Изменения (`ShipModuleServer.cs`, +63/-13, сигнатуры RPC не тронуты):**
- `+ _serverRepaintCost=500 / _serverHullRepairCost=300` (SerializeField, дефолты = `RepairManager`);
- `+ ComputeServerSellPrice()` (паритет формулы с клиентом, источник — серверный каталог; unknown → 0);
- sell/repaint/hull RPC — серверные цены + mismatch-`Warning` (чит-сигнал); клиентский `cost<=0` не влияет.

**Проверка:** Console → 0 CS-ошибок (MCP; refresh_unity по таймауту транспорта, ошибок скриптов нет).
Manual — за пользователем (кейсы в `docs/Ships/fix/T-SHIP-FIX03_server-prices.md`).

---

## Итерация от 2026-09-18 (T-SHIP-FIX04)

**Задача:** P0 — серверный авторитет Recall. Статус: ИСПРАВЛЕНО (код).

**Перепроверка (до фикса — ПОДТВЕРЖДЕНО):** `RecallShipToPadServerRpc:2381` принимал
`padPosition/cost` без проверок (чужой корабль в любую точку за 0). Grace покрыт
(`ExitDocked:162` ставит `_lastUndockTime`), Persist подхватывает автосейв
(`ShipPositionServer.Update` каждые 5 сек; риск — рестарт <5 сек после recall).

**Изменения (только `ShipController.cs`, сигнатура RPC и клиент не тронуты):**
- ownership-guard (`IsOwnerOfShip(clientId, NetworkObjectId)`, ключ от клиента не нужен);
- `+ _serverRecallCost=500` (дефолт = `RepairManager`), клиентский cost игнорируется;
- `+ TryResolveFreePad()` — сверка с серверными свободными `DockingPadTriggerBox`
  (толерантность 15 м), телепорт на серверную позицию; занят/нет рядом → отказ;
- по пути: `FindObjectsSortMode`-overload obsolete в Unity 6 → убран (CS0618).

**Проверка:** Console → 0 errors/warnings по файлу (MCP). Тесты — за пользователем:
реестр `docs/dev/global_needtotest/SHIP_TESTS.md` заведён (бэкфилл FIX03 + кейсы FIX04),
долгосрочные проверки вердикта — там же.

---

## Итерация от 2026-09-18 (T-SHIP-FIX01)

**Задача:** P0 — ввод мезии/ролла/дозаправки через RPC владельца (сервер читал клавиатуру хоста).
Статус: ИСПРАВЛЕНО (код).

**Перепроверка (до фикса — ПОДТВЕРЖДЕНО + находки):** серверный опрос (`IsKeyDown`,
дозаправка L, мезия, ролл Z/C) двигал чужие корабли с хоста. Meziy-events `ShipInputReader` —
0 подписчиков (оставлены, снос в FIX13). `GetCurrentPitch/YawInput` — 0 вызовов (удалены).
В `InputBindingsConfig` нет actions для Roll/Meziy/Refuel → упаковка читает те же 9 клавиш
(паритет 1:1, ребиндинг — отдельный тикет). Перекрытия клавиш (C/Z/Shift+) сохранены как дизайн.

**Изменения (`ShipController.cs` ~−140/+100, `NetworkPlayer.cs` +31/−1):**
- `NetworkPlayer.Update`: упаковка 6 интентов → расширенный `SendShipInput` (единственный вызывающий);
- `SubmitShipInputRpc`/`ApplyServerInput` (опционально для NPC): новые суммы/средние, reset-сайты ×4,
  isIdle/engineStalled-обнуления; все 10 float clamp `[-1,1]` (часть FIX02 сделана здесь, в FIX02 — AddPilot);
- сервер: refuel/meziy/roll на средних; удалены `IsKeyDown/KeyCodeToKey/GetCurrent*Input`
  (проверено поиском по диску; `:2033` double-dt не тронут — FIX05).

**Проверка:** Console → 0 errors/warnings (MCP). Manual — за пользователем
(кейсы в `docs/Ships/fix/T-SHIP-FIX01_server-input.md` + реестр `SHIP_TESTS.md`).
⚠️ Инцидент инструментов: первая серия правок тикета молча не попала на диск
(«success» без изменений); выявлено сверкой `git diff` по диску, все правки внесены повторно
и проверены поиском по диску до коммита. Правило: после каждого тикета — `git diff --stat` с диска.
