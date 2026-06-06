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
