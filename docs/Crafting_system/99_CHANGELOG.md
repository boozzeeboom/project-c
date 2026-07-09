# Crafting System — Changelog

> История изменений **этой документации** (не кода). Код — см. git log.

---

## v0.0.1-design (2026-06-07)

**Сессия:** Документация, анализ, проектирование. Без кода.

**Что сделано:**
- ✅ Создан `docs/Crafting_system/00_OVERVIEW.md` — обзор, скоуп, REUSE-список, Open Questions.
- ✅ Создан `docs/Crafting_system/10_DESIGN.md` — детальный дизайн классов, sequence-диаграммы, edge-cases.
- ✅ Создан `docs/Crafting_system/20_IMPLEMENTATION_PLAN.md` — пошаговый план (5 фаз, ~16-22 ч).
- ✅ Создан `docs/Crafting_system/30_VERIFICATION.md` — ручные сценарии + exit-критерии.
- ✅ Создан `docs/Crafting_system/40_INSPECTOR_REFERENCE.md` — reference для дизайнера/контент-мейкера.
- ✅ Создан `docs/Crafting_system/50_KNOWN_ISSUES.md` — open risks, design decisions.

**Что НЕ сделано (по запросу):**
- ❌ Код (по запросу пользователя "ничего не кодим").
- ❌ Тесты (дождаться имплементации).

**Аналитические артефакты, использованные при анализе:**
- Прямое чтение `ProjectC.Items.InventoryWorld.cs` (445 строк), `InventoryServer.cs` (305 строк), `InventoryClientState.cs` (234 строки), `InventoryData.cs` (162 строки), `ItemType.cs` (35 строк).
- Прямое чтение `ProjectC.Items.Network.NetworkChestContainer.cs` (335 строк) — образец для CraftingStation.
- Прямое чтение `ProjectC.Trade.Core.Warehouse.cs` (149 строк), `TradeWorld.cs` (486 строк), `MarketState.cs` (77 строк), `MarketServer.cs` (522 строки), `MarketTimeService.cs` (170 строк), `MarketClientState.cs` (225 строк), `CargoSystem.cs` (287 строк).
- Прямое чтение `ProjectC.MetaRequirement.MetaRequirement.cs` (292 строки), `MetaRequirementClientState.cs` (176 строк), `MetaRequirementRegistry.cs` (231 строка).
- Прямое чтение `ProjectC.Ship.Key.ShipKeyServer.cs` (210 строк) — legacy алиас.
- Прямое чтение `ProjectC.Scripts.Player.NetworkPlayer.cs` (936 строк, секции 848-934 — все target-RPC методы).
- Прямое чтение `ProjectC.Scripts.UI.Client.MarketWindow.cs` (1270 строк, начало) — tab-pattern.
- `unity-mcp-orchestrator` SKILL.md — refresh_unity schema fix.
- `project-c-ui-as-tab` SKILL.md — R3-005 cache rule, 4 FIX'ы.

**Ключевые архитектурные решения (ADR):**
- D1-D10 — см. `50_KNOWN_ISSUES.md` §7.
- Источники ресурсов: `InventoryWorld` + `Warehouse` (НЕ cargo в MVP).
- Soft-lock (RESERVE), не hard-lock.
- Server-time, не real-time.
- Одна станция = один Job.
- Server-authoritative, client projection.
- UI = tab в `MarketWindow` (по `project-c-ui-as-tab` skill).

**Что нужно от тебя до старта имплементации:**
- Закрыть Q1, Q2, Q5 в `00_OVERVIEW.md` §9.
- Согласовать список рецептов (минимум 3).

---

## v0.0.0-pre (2026-06-07)

- Каталог `docs/Crafting_system/` создан (пустой). Все файлы выше — это первая итерация.

---

## v0.1.0-audit (2026-07-09)

**Сессия:** Глубокий аудит существующего Crafting-кода (16 `.cs` файлов, ~2 700 строк). Сравнение с дизайн-документацией и предыдущим аудитом.

**Что сделано:**
- ✅ Создан `AUDIT_2026-07-09.md` — полный анализ: 5 критических багов (B1-B5), 7 техдолгов (T1-T7), 5 косметических (L1-L5). Все 11 проблем из предыдущего аудита (2026-06-17) живы — ни одна не исправлена.
- ✅ Поэтапный план исправлений на 2 сессии (~5 ч):
  - **Сессия 1:** owner-guards, двойной возврат, валидация buffer→recipe, MetaReq check, CraftSpeedMultiplier, убрать reflection
  - **Сессия 2:** единый реестр ItemId, клиентский кеш UI, убрать рефлексию GetInteractRadius, удалить мёртвый файл, поправить ServerCollect
- ✅ Зафиксировано, что `docs/Crafting_system/` отражает v0.0.1-design (план), а код ушёл вперёд и содержит баги.

**Ключевые выводы:**
- **Самый опасный баг:** B1 — CollectRpc без owner-guard (любой игрок может украсть крафт)
- **Архитектурная проблема:** CraftingWindow (клиент) вызывает CraftingWorld (server-only статику)
- **Рефакторинг не нужен** — все проблемы лечатся точечными патчами, система не требует переписывания
- **0 тестов** — после фиксов нужен Play Mode прогон

**Что НЕ сделано (сознательно):**
- ❌ Никаких изменений кода (план на следующие сессии)
- ❌ Не затронуты фьючеры (очередь, drag-and-drop, cargo, persistence) — это Phase 2+
- ❌ Не сдвинут ROADMAP (ожидает завершения баг-фиксов)

---

## v0.1.1-fixes-session1+2 (2026-07-09)

**Сессия:** Исправление критических багов и техдолга по плану AUDIT_2026-07-09.md.

**Что сделано — Сессия 1 (критические баги):**
- ✅ **B1** — owner-guard в `CollectRpc`: проверка `job.OwnerClientId != clientId`, возврат `NotOwner` (`CraftingServer.cs`)
- ✅ **B2** — двойной возврат ресурсов: `CancelCraftRpc` возвращает Buffer+Committed один раз; `ServerCancelCraft()` больше не копирует Committed→Buffer (`CraftingServer.cs`, `CraftingStation.cs`)
- ✅ **B3** — валидация buffer→recipe: новый метод `ValidateBufferMatchesRecipe`, вызов в `StartCraftRpc` (`CraftingServer.cs`)
- ✅ **B4** — MetaReq tool check: вызов `station.CanStartCraft()` в `StartCraftRpc` (`CraftingServer.cs`)
- ✅ **B5** — CraftSpeedMultiplier: `recipe.CraftSeconds / station.Config.CraftSpeedMultiplier` вместо хардкода `1f` (`CraftingServer.cs`)
- ✅ **T4** — owner-guard в `CancelCraftRpc`: проверка `job.OwnerClientId != clientId` (`CraftingServer.cs`)
- ✅ **T1** — замена reflection `GetMethod("CompleteCraft")` на прямой вызов `cs.CompleteCraft()` (`CraftingWorld.cs`)

**Что сделано — Сессия 2 (техдолг):**
- ✅ **T2** — удалён двойной реестр ItemId из `CraftingWorld`: поля `_itemsById`, `_idsByItem`, `_nextItemId` удалены; методы `RegisterItem()`, `GetItemId()`, `GetItem()` удалены; все вызовы заменены на `InventoryWorld.Instance` (`CraftingWorld.cs`, `CraftingServer.cs`, `CraftingStation.cs`)
- ✅ **T3** — клиентский кеш рецептов/предметов в `CraftingClientState`: методы `GetRecipe()`, `GetRecipeDisplayName()`, `GetItemId()`, `GetItem()`; `CraftingWindow` обновлён использовать `CraftingClientState` вместо `CraftingWorld` (`CraftingClientState.cs`, `CraftingWindow.cs`)
- ✅ **T6** — замена reflection `GetMethod("GetInteractRadius")` на `(station as IInteractable)?.InteractionRadius` (`CraftingServer.cs`)
- ✅ **T7** — удалён мёртвый файл `Dto/CraftingDtos.cs`
- ✅ **T5** — `CollectRpc`: выдача предметов обёрнута в try-finally, `ServerCollect()` в finally (`CraftingServer.cs`)
- ✅ Добавлены недостающие using'и (`ProjectC.Core`, `ProjectC.Items`)

**Компиляция:** 0 errors ✅

**Файлы изменены:** `CraftingServer.cs`, `CraftingStation.cs`, `CraftingWorld.cs`, `CraftingClientState.cs`, `CraftingWindow.cs`
**Файлы удалены:** `Dto/CraftingDtos.cs`
