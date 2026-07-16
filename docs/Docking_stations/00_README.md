# Docking Stations — Architecture & Roadmap

> **Статус:** ✅ **MVP реализован (2026-06-20)**. Все 5 фаз `T-DOCK-00..13` выполнены, compile = 0 errors. Требуется переработка визуальных маркеров падов (`DockPadVisualMarker`).
> **Сессия:** 2026-06-19 → 2026-06-21 (Mavis, profile `project-c`)
> **Каталог:** `docs/Docking_stations/`
> **Конвенция:** См. `AGENTS.md` и `project-c-bootstrap` skill (Unity 6, NGO 2.11, UI Toolkit, .NET 8).

---

## Назначение

Реализовать **стыковочные порты** — инфраструктуру для безопасной посадки
кораблей в городе/платформе/анклаве. Игрок подлетает в большой триггер-зоне
связи с диспетчером → открывает **CommPanel** → запрашивает посадку →
получает назначенный **pad** под свой класс корабля → летит к нему →
при касании попадает в состояние **Docked** (двигатель блокируется).
Если сел на чужой свободный pad — **toast-предупреждение** (MVP).
Phase 2 (после MVP) добавляет **автопилот стыковки** (модуль
`MODULE_AUTO_DOCK`).

### MVP-скоуп (этот каталог проектирует)
1. **OuterCommZone** — большая sphere-зона (~500-1500 м) для контакта с диспетчером.
2. **DockStation** (composite GameObject) — структурная единица порта:
   несколько **DockingPadTriggerBox** (маленькие зоны для физического касания)
   + список слотов с совместимыми классами кораблей.
3. **DockingServer** — server-hub `NetworkBehaviour` в `BootstrapScene`
   (по канону v2: `QuestServer`, `CraftingServer`, `ExchangeServer`).
   Держит реестр станций и pads, назначает pads, обрабатывает запросы
   `RequestDockingRpc`/`RequestTakeoffRpc`, шлёт DTO клиенту.
4. **DockingClientState** — singleton projection: какая станция ближе,
   назначен ли игроку pad, текущий статус стыковки.
5. **CommPanelWindow** — UI Toolkit окно (диалог с диспетчером):
   список доступных pads, кнопка "Запросить посадку", информация о
   назначенном pad.
6. **Docked state в ShipController.FSM** — новые состояния **Docking** /
   **Docked** (см. GDD-10 §8) с интеграцией в существующую машину состояний.
7. **Wrong-pad toast** — UI-предупреждение при касании свободного, но
   чужого по классу pad.

### Out of MVP-scope (Phase 2)
- Автопилот: модуль `MODULE_AUTO_DOCK` + маршрутизация (GDD-10 §6.3, P3).
- Учёт пропускной способности, очереди, traffic-controller (только когда
  будет реальная нагрузка в Play Mode).
- NPC-диспетчер с ИИ (сейчас серверный stub с рандомными фразами).
- Визуальные эффекты стыковки (магнитные захваты, швартовы) — Phase 3.
- Стыковка под углом / на ходу / форсаж — Phase 5.

---

## Карта документации

| # | Файл | О чём | Слов |
|---|------|-------|------|
| **00** | `00_README.md` | Этот файл — навигация, TL;DR, **финальные решения**, статусы. | ~700 |
| **01** | `01_CURRENT_STATE_AUDIT.md` | Что уже описано в GDD-10 §7, что есть в коде (Composite Ship, MarketZone, QuestServer), пробелы. | ~1500 |
| **02** | `02_V2_ARCHITECTURE.md` | Namespaces, SO `DockStationDefinition`, сервер-хаб `DockingServer` (вкл. `RequestConfirmAssignmentRpc`), DTO, `DockingClientState` (с заделом на NPC-корабли Phase 2), FSM. | ~3500 |
| **03** | `03_ZONES_AND_TRIGGERS.md` | `OuterCommZone` (настраиваемый радиус), `DockStation` composite (любое кол-во pads), `DockingPadTriggerBox`, маппинг pad ↔ shipClass, цифры на mesh'е. | ~2500 |
| **04** | `04_DIALOG_AND_DISPATCHER_UI.md` | `CommPanelWindow` UI Toolkit с **двусторонним диалогом** (`[Хорошо] [Отбой]`), USS, подписка. | ~2000 |
| **05** | `05_FLOW_AND_INTERACTION.md` | Полный поток стыковки **+ departure** (отдельная подсистема), F/T клавиши, edge-cases. | ~2500 |
| **06** | `06_ROADMAP.md` | 14 тикетов docking (`T-DOCK-00..13`) + 6 тикетов departure (`T-DEPART-00..05`, Phase 1.5) + тикет `T-DOCK-14` (visual markers v2), milestones, фазы. | ~2500 |
| **07** | `07_OPEN_QUESTIONS.md` | ✅ **Все Q закрыты** (2026-06-19). Финальные решения + архив Q&A. | ~2500 |
| **08** | `08_DEPARTURE_SUBSYSTEM.md` | **Отдельная подсистема Departure** (вылет по запросу через T) — Q8. | ~1500 |
| **09** | `09_REFERENCES.md` | Ссылки на все референсные файлы проекта (file:line), GDD, и pitfalls. | ~1500 |
| **11** | `11_VISUAL_MARKERS_PLAN.md` | 🔬 **План T-DOCK-14** — полная переработка `DockPadVisualMarker` (6 тикетов, ~8 ч). | ~2500 |
| **CHANGELOG** | `CHANGELOG.md` | Лог Q→A→action по каждому вопросу. | ~1000 |

**Суммарно:** ~17 000 слов. Самодостаточно — можно читать в любом порядке,
но рекомендую начать с README → 07 (вопросы) → 06 (roadmap) → 02 (архитектура)
→ остальные по необходимости.

---

## Резюме анализа (TL;DR)

### Что уже спроектировано (GDD-10 §7) — ✅ реализовано
- **§7.1** даёт полный поток: вход в зону → запрос → ответ с pad/курсом/окном → подлёт → касание → Docked.
- **§7.2** определяет структуру `DispatcherMessage` (padId, approachPoint, altitude, heading, landingWindow, voiceLine) — реализована как `DockingAssignmentDto`.
- **§8** добавляет состояния **Docking** и **Docked** в FSM корабля — реализованы через `ShipController._netIsDocked` (NetworkVariable<bool>).

### Что есть в коде (use, don't reinvent) — ✅
- **Composite Ship** (`ShipRootReference`, `ShipComponentLocator`, `PilotSeatController`) — фундамент для **DockStation** как композитного объекта.
- **`MarketZone` + `MarketZoneRegistry`** (`Assets/_Project/Trade/Scripts/Network/`) — готовый шаблон для **OuterCommZone** + **DockStationRegistry**.
- **`QuestServer`** (`Assets/_Project/Quests/Network/`) — каноничный v2 server-hub (rate limiting, RPCs, ClientState projection).
- **`CharacterWindow` UI Toolkit** (`Assets/_Project/Scripts/UI/Client/`) + `docs/UI/UI_TOOLKIT_GUIDE.md` — канонический шаблон UI Toolkit окна с `!important`-стилями и темой `UnityDefaultRuntimeTheme`.
- **`ShipController.ShipFlightClass`** enum (`Light` / `Medium` / `Heavy` / `HeavyII`) — готовый источник правды для совместимости с pads.

### Финальные архитектурные решения — все реализованы (2026-06-20)

| Q | Решение | Где реализовано | Статус |
|---|---------|-----------------|--------|
| Клавиша | **T** (временное) | `PlayerInputReader.OnCommPanelPressed` → `NetworkPlayer.OnCommPanelPressed` | ✅ |
| Фразы | Статичный набор + шаблоны `{0}` | `DispatcherVoiceLines` SO | ✅ |
| Persistence | **Сервер — SOT** | `DockingWorld._occupiedPads` | ✅ |
| Кол-во pads | **Без хардкода**, ≤10 на класс | `DockPadLayout` SO | ✅ |
| Радиус OuterCommZone | Настраивается в Inspector | `OuterCommZone.commRange` (1000m для Примум) | ✅ |
| Координаты Primium | (40500, 2510, 40500) | `DockStation_Primium` в WorldScene_0_0 | ✅ |
| Связь | **Двусторонняя** (простая MVP) | `RequestConfirmAssignmentRpc` + UI [Хорошо]/[Отбой] | ✅ |
| Вылет | F = boarding; T → «Запросить вылет» | **Отдельная подсистема Departure** (см. `08_DEPARTURE_SUBSYSTEM.md`) — Phase 1.5 | ⏳ |
| F внутри CommPanel | Стандартное + закрыть панель | `NetworkPlayer` F-handler | ✅ |
| T вне кресла | Игнорируется | `IsLocalPlayerPilotingShip()` check | ✅ |
| KeyRod в Docked | НЕ обрабатываем | F = выход из кресла | ✅ |
| Звук | Только текст | — | ✅ |
| Floating labels | **Цифры на mesh'е** | TMP labels созданы на каждом паде (001-005) | ⚠️ частично (см. ниже) |
| Live update | Только при Assigned | — | ✅ |
| FSM корабля | Bool-флаги | `ShipController._netIsDocked` (NetworkVariable<bool>) | ✅ |
| Time окна | **5 минут** (300 сек) | `DockStationDefinition.landingWindowSeconds` | ✅ |
| Физическая блокировка | `rb.isKinematic = true` | `EnterDocked()` / `ExitDocked()` | ✅ |
| Initial scan | Корабли на паде при старте | `DockingWorld.ScanExistingOccupants()` | ✅ |

### Что осталось / Known issues

- 🔬 **`DockPadVisualMarker`** — **план v2 утверждён** (2026-07-12). Полная переработка: holographic-маркеры, 7 визуальных состояний, сетевая синхронизация через `PadStateSync`. Подробно: `11_VISUAL_MARKERS_PLAN.md`, тикет `T-DOCK-14`.
- ⏳ **Departure subsystem** — Phase 1.5, отдельный roadmap в `08_DEPARTURE_SUBSYSTEM.md`.

### Чего не было — теперь реализовано ✅

| # | Компонент | Где | Статус |
|---|-----------|-----|--------|
| 1 | **DockingServer hub** (RPCs включая Q7 `RequestConfirmAssignmentRpc`) | `Assets/_Project/Scripts/Docking/Network/DockingServer.cs` | ✅ |
| 2 | **DockingClientState** (singleton projection) | `Assets/_Project/Scripts/Docking/Client/DockingClientState.cs` | ✅ |
| 3 | **CommPanelWindow UI Toolkit** (двусторонний диалог) | `Assets/_Project/Scripts/Docking/UI/CommPanelWindow.cs` | ✅ |
| 4 | **DockStation / DockingPadTriggerBox** (композитная структура) | `Assets/_Project/Scripts/Docking/Stations/` | ✅ |
| 5 | **FSM Docking/Docked** (bool-флаги в `ShipController`) | `_netIsDocked` NetworkVariable | ✅ |
| 6 | **Wrong-pad toast** | `CommPanelWindow.HandleStatusReceived` (WrongPad ветка) | ✅ (текст в message) |
| 7 | **DockingWorld на сервере** (single source of truth) | `Assets/_Project/Scripts/Docking/Core/DockingWorld.cs` | ✅ |
| 8 | **HUD Dispatch column** (K5 в ShipHudController) | `ShipHudController.UpdateDispatchColumn()` | ✅ |

---

## Финальные архитектурные решения (TBD после 07)

Перед началом кодинга нужно получить ответы на 15 вопросов в `07_OPEN_QUESTIONS.md`. Ключевые из них:

| # | Вопрос | Моя рекомендация |
|---|--------|------------------|
| Q1 | Какая клавиша для запроса стыковки? | **T** (новая, см. GDD-01 reserved) |
| Q2 | Что показывает диспетчер — рандомные фразы или статичный набор? | **Статичный набор 8-12 фраз** по контексту |
| Q3 | Сохранять ли занятость pads в JSON? | **Session-only** для MVP |
| Q4 | Сколько pads на станцию для MVP? | **6 pads (3 Light + 2 Medium + 1 Heavy)** |
| Q5 | Можно ли высадиться на чужой свободный pad в MVP? | **Да, но warning toast** (per user) |
| Q6 | Где размещается первый порт? | **Примум (на месте `MarketZone_Primium`)** |
| Q7 | Двусторонняя связь (игрок-диспетчер) или только диспетчер→игрок? | **Двусторонняя** (кнопка "Спасибо", "Отменить") |

Полный список — в `07_OPEN_QUESTIONS.md`.

---

## Что нужно от тебя (порядок действий)

1. **Открой `07_OPEN_QUESTIONS.md`** — там 15 вопросов с моими рекомендациями.
2. **Дай ответы** прямо в файле под `**ответ:**` строками (либо в чате, я внесу).
3. После ответов я обновлю `02_V2_ARCHITECTURE.md` и `06_ROADMAP.md` под финальные
   решения, приготовлю commit-сообщение для документации и можно стартовать
   `T-DOCK-00` (server-hub skeleton) в следующей code-writing сессии.

---

## Связанные документы

| Документ | Путь | Зачем |
|----------|------|-------|
| GDD-10 §7 (Стыковка) | `docs/gdd/GDD_10_Ship_System.md:394-432` | Дизайн-источник правды (уже написан) |
| GDD-10 §8 (FSM корабля) | `docs/gdd/GDD_10_Ship_System.md:436-449` | Состояния Docking/Docked |
| GDD-01 §3 (Controls) | `docs/gdd/GDD_01_Core_Gameplay.md:108-138` | Управление в корабле |
| Composite Ship | `docs/Ships/00_COMPOSITE_SHIP_SUMMARY.md` | Архитектура композитного объекта |
| MarketZone pattern | `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` | Референс zone-trigger |
| QuestServer pattern | `Assets/_Project/Quests/Network/QuestServer.cs` | Референс server-hub |
| DialogWindow pattern | `Assets/_Project/Quests/UI/DialogWindow.cs` | Референс UI Toolkit window |
| ShipController | `Assets/_Project/Scripts/Player/ShipController.cs` | FSM хост для Docking/Docked |

---

*Документ создан: 19 июня 2026 | Агент: Mavis | Не кодим, только документация.*