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
