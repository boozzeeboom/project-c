# 🔍 Комбинированный глубокий аудит системы квестов (NPC Quests v2)

> **Дата:** 2026-07-13
> **Основание:** Повторный полный аудит по запросу пользователя. Сравнение с предыдущим аудитом от 2026-07-09.
> **Метод:** поиск по 51 `.cs`, чтение ключевых файлов (QuestServer 1584 строки, QuestWorld 1350 строк, QuestDatabase, DialogTree, QuestToast, DialogWindow, ConcreteTriggers, Editor tools), проверка ассетов в `Data/`, сверка с предыдущим аудитом.
> **Вердикт:** за 4 дня с предыдущего аудита произошли существенные изменения — **часть квестовых данных была утеряна**. Это главный новый вывод.

---

## 1. Сводка изменений с аудита 2026-07-09

| Что было сказано в предыдущем аудите | Реальность 2026-07-13 | Статус |
|--------------------------------------|----------------------|--------|
| **11 FactionDefinition SO** существовали | **GUIDs есть в QuestDatabase.asset, но файлы `.asset` НЕ НАЙДЕНЫ** ни в `Data/Factions/`, ни где-либо ещё в проекте | 🔴 **РЕГРЕСС** |
| **106 NpcDefinition SO** (Mira + npc_002..npc_105) | GIDs есть в DB, **0 NpcDefinition.asset** найдено в проекте | 🔴 **РЕГРЕСС** |
| **~3+ QuestDefinition SO** (FindArtifact, EventDrivenQuest, collect_copper_ore, StageIntroDemo, StageMultiDemo) | GIDs есть в DB, **0 QuestDefinition.asset** найдено | 🔴 **РЕГРЕСС** |
| **2 DialogTree**: mira_default + MiraDefault | В `Data/Dialogs/` — оба существуют | ✅ Целы (но дубль) |
| **QuestDatabase.asset** со всеми GUID | Существует, GUIDs ссылаются в никуда | ⚠️ **DANGLING REFS** |
| **QuestGraphView.cs** (дублирующий editor) | **Удалён** (не найден) | ✅ Исправлено |
| **QuestGraphWindow.cs** (старый) | **Удалён** — не найден | ✅ Исправлено |
| **QuestServer.cs** ~1600+ строк | 1584 строк (почти то же) | 🟡 Без изменений |
| **QuestWorld.cs** ~1340 строк | 1350 строк (почти то же) | 🟡 Без изменений |
| **6 стаб-триггеров** | 3 заглушки (CargoHasItem, LocationReached, KilledEntity) — всё те же | 🟡 Без изменений |
| **6 стаб-действий** FireDialogAction | GiveCargoItem + TakeCargoItem + FailQuest всё ещё стабы, OpenService стаб, SetFlag/SwitchDialogTree/DiscoverQuest/EmitEvent — логируются | 🟡 Без изменений |
| **ContractMetaBridge** — подписка на 3 события | Работает (ContractAccepted/Completed/Failed) | ✅ |
| **Persistence (T-Q18)** — JSON-репозиторий | `JsonQuestStateRepository` — существует | ✅ |
| **QuestNodeGraphView** (GraphView) | 674 строк (было ~590) — расширен | ✅ Улучшено |

---

## 2. 🔴 КРИТИЧЕСКАЯ ПРОБЛЕМА: Потеря квестовых ассетов

### 2.1 Что произошло

QuestDatabase.asset ссылается на множество GUID:

| Тип | Количество GUID в DB | Файлов найдено |
|-----|---------------------|----------------|
| **FactionDefinition** | 11 | **0** |
| **NpcDefinition** | 56+ (возможно 106) | **0** |
| **DialogTree** | 2 | 2 (в Data/Dialogs/) |
| **QuestDefinition** | 115+ | **0** |

**Все FactionDefinition, NpcDefinition и QuestDefinition .asset файлы отсутствуют в проекте.**
Дирректории `Data/Factions/`, `Data/Npcs/`, `Data/Quests/` существуют, но **пусты** (содержат только `.meta`).

### 2.2 Что это значит

- `QuestServer.OnNetworkSpawn()` делает `QuestWorld.CreateAndInitialize(questDatabase.quests, ...)` — если `quests == null || quests.Length == 0`, **QuestWorld не будет знать ни об одном квесте**
- `QuestDatabaseAutoDiscover` не найдёт файлы в пустых дирректориях
- **В Play Mode ни один квест не будет работать** — `TryOffer("find_artifact")` вернёт `QuestNotFound`
- `NpcDefinition` для NPC в сцене — без ассета `NpcController.definition == null`, E-key не запустит диалог

### 2.3 Возможные причины

1. **Файлы никогда не были закоммичены** — CSV-импорт (M19) генерирует их, но они не были добавлены в git
2. **Удалены случайно** — при чистке v1-кода (T-Q19: 4 удалённых файла) могли быть затронуты
3. **Хранятся в другом месте** — возможно, GUID в базе указывают на ассеты, которые физически лежат вне `Assets/_Project/Quests/Data/`
4. **Были в `_Project/Resources/`** — не найдены поиском

### 2.4 Recommendation

> **НЕМЕДЛЕННО:**
> 1. Проверить `QuestDatabase.asset` в Unity Inspector — какие поля `factions[]`, `npcs[]`, `quests[]` показывают?
> 2. Если все массивы пусты — перегенерировать через `QuestCsvWindow` (импорт из CSV)
> 3. Если не пусты — найти GUID-ы физически через `AssetDatabase.GUIDToAssetPath` (или в Unity: выделить элемент в Inspector → Show in Project)
> 4. Закоммитить ассеты в git после восстановления

---

## 3. 🟡 Стаб-триггеры и стаб-действия (без изменений)

### 3.1 Триггеры-заглушки (`return false`)

| Триггер | Файл | Строка | Проблема |
|---------|------|--------|----------|
| **CargoHasItemTrigger** | `ConcreteTriggers.cs` | 158 | Всегда false. Интеграция с `TradeWorld.GetOrLoadCargo` не сделана |
| **LocationReachedTrigger** | `ConcreteTriggers.cs` | 172 | Всегда false. Интеграция с `PlayerChunkTracker` не сделана |
| **KilledEntityTrigger** | `ConcreteTriggers.cs` | 186 | Всегда false. Combat system не существует — оправданно |

**Дополнительно:** `ShipDockedAtTrigger` — упоминается в `06_TRIGGERS_AND_INTEGRATION.md`, но отсутствует в коде (ни класса, ни фабрики в `QuestTriggerService`).

### 3.2 Действия-заглушки в QuestServer.FireDialogAction()

| Действие | Enum value | Строка | Статус |
|----------|-----------|--------|--------|
| **GiveCargoItem** | 22 | 1555 | Stub |
| **TakeCargoItem** | 23 | 1555 | Stub |
| **FailQuest** | 12 | 1555 | Stub |
| **OpenService** | 41 | 1287 | Stub: «ServiceUI TBD». Dialog закрывается, UI не открывается |
| **SetFlag** | 50 | 1358 | Fallthrough: лог + no-op |
| **SwitchDialogTree** | 52 | 1358 | Fallthrough: лог + no-op |
| **DiscoverQuest** | 13 | 1358 | Fallthrough: лог + no-op |
| **EmitEvent** | 51 | 1358 | Fallthrough: лог + no-op |

### 3.3 Устаревшие комментарии в коде

Предыдущий аудит отметил, что комментарии «T-Q15 fill»/«T-Q18+ fill» устарели (T-Q15 и T-Q18 уже сделаны):
- `ConcreteTriggers.cs:5` — «T-Q15+ to fill» для CargoHasItem/LocationReached
- `QuestServer.cs:1358` — «T-Q15 stub — T-Q18+ fill» для SetFlag/SwitchDialogTree/etc.
- `QuestWorld.cs:328` — «T-Q15 fix: QuestServer.FireDialogAction.OfferQuest stub → real impl»

**Это не было исправлено.** Комментарии вводят в заблуждение — T-Q15 и T-Q18 завершены, а стабы остались.

---

## 4. 🟡 QuestWorld.cs — раздувание (без изменений)

`QuestWorld.cs` = **1350 строк** (было ~1340). Смешивает:
- State transitions (TryOffer/TryAccept/TryTurnIn)
- Reputation/NpcAttitude modifiers
- Objective evaluation (T-Q20 tick logic)
- Persistence (Save/Load)
- Event tracking (npcTalkedTo, events, contracts)
- Prerequisites (T-Q28)

**Рекомендация (та же, что и в предыдущем аудите):** Выделить partial class или композицию:
- `QuestWorld.State.cs` — transitions
- `QuestWorld.Reputation.cs` — modifiers
- `QuestWorld.Tick.cs` — evaluation
- `QuestWorld.Persistence.cs` — save/load

---

## 5. 🟡 QuestServer.cs — раздувание (без изменений)

`QuestServer.cs` = **1584 строки**. FireDialogAction — switch на 17+ case.

**Рекомендация:** Вынести `FireDialogAction` в `DialogueActionRunner.cs`.
Snapshot builders — в `QuestSnapshotBuilder.cs`.
Runtime DialogTree builder (T-Q28, ~200 строк) — в отдельный класс.

---

## 6. 🟢 Мелкие проблемы

### 6.1 Дублирование диалоговых ассетов
- `mira_default.asset` (правильный)
- `MiraDefault.asset` (дубль, case-different)

На Windows (case-insensitive FS) это **одна и та же директория**. Один из них нужно удалить.

### 6.2 Проблемы с содержимым mira_default.asset
- **targetNodeId содержит русский текст** (например «Я просто осматриваюсь») вместо корректного nodeId — это выглядит как ошибка CSV-импорта, где текст ответа попал в поле targetNodeId
- **condition.type=10** — это `QuestState` (=10). Сравните с `DialogueCondition.cs` чтобы убедиться, что сериализация корректна

### 6.3 NpcFaction naming collision
`ProjectC.Factions.FactionId` (enum, квесты) и `ProjectC.AI.NpcFaction` (SO, combat AI) — одинаковое имя для разных концепций. Не исправлено.

### 6.4 M15.1: Toast показывает ID вместо displayName
В `QuestToast.cs` сообщения тоста используют `mira_01` вместо `Mira`. Код находит NpcId, но не резолвит displayName.

### 6.5 NpcController — Cube placeholder
NPC отображаются как Cube + TMPro. Не production-ready.

---

## 7. ✅ Что работает хорошо (сверка с предыдущим аудитом)

| Категория | Статус | Комментарий |
|-----------|--------|-------------|
| Server-authoritative архитектура | ✅ | QuestServer → QuestWorld → RPC → DTO → ClientState |
| WorldEventBus (7+ подписок) | ✅ | Item, Reputation, NpcAttitude, CustomEvent, DialogVisited, DayNight |
| ContractMetaBridge | ✅ | Подписка на 3 contract-события, маркеры в QuestWorld, Evaluate |
| JSON Persistence | ✅ | JsonQuestStateRepository — Load/Save/Delete |
| UI Toolkit диалог | ✅ | DialogWindow — typewriter, options, attitude badges, OpenMarket |
| QuestTracker HUD | ✅ | Overlay с отслеживанием активного квеста |
| QuestToast очередь | ✅ | Queue-based, bottom-center, последовательные награды |
| CharacterWindow таб | ✅ | 4 секции: Active/Completed/Failed/Discovered |
| Editor: DatabaseWindow | ✅ | TreeView + edit |
| Editor: QuestNodeGraphView | ✅ | GraphView editable, nodes+edges > save to SO |
| Editor: CSV tools | ✅ | Schema + Importer + Exporter + Window |
| Editor: AutoDiscover | ✅ | QuestDatabaseAutoDiscover |
| Editor: Validator | ✅ | QuestDefinitionValidator |
| DTOs (8 файлов) | ✅ | Все INetworkSerializable |
| Rate limiting | ✅ | 30 ops/min/client |
| Debug guards | ✅ | debugMode + Debug.isDebugBuild |
| v1 cleanup (T-Q19) | ✅ | 4 старых файла удалены, 1447 LOC убрано |
| QuestGraphView.v8 удалён | ✅ | Исполнено по рекомендации аудита |
| Input refactor (T-X3) | ✅ | PlayerInputReader + Events |
| NPCTrader rename (T-X1) | ✅ | NPCTrader → MarketTrader |

---

## 8. Интеграционные точки — статус

| Подсистема | Точка интеграции | Статус |
|------------|-----------------|--------|
| **Inventory** | `InventoryWorld.AddItemDirect` / `InventoryServer.TryRemove` + `ItemAddedEvent`/`ItemRemovedEvent` → QuestWorld/Triggers | ✅ |
| **Trade/Contract** | `ContractAcceptedEvent/CompletedEvent/FailedEvent` → ContractMetaBridge → QuestWorld | ✅ |
| **Trade/Market** | `MarketInteractor.TryOpenMarket` (из DialogWindow при OpenMarket action) | ✅ |
| **Stats/XP** | `QuestAcceptedEvent`/`QuestCompletedEvent` → `StatsServer.ApplyXp` | ✅ |
| **Reputation** | `ReputationChangedEvent`/`NpcAttitudeChangedEvent` → QuestTriggerService + CharacterWindow | ✅ |
| **DayNight** | `DayNightPhaseChangedEvent` → DayNightPhaseTrigger | ✅ |
| **Combat** | KilledEntityTrigger → **стаб** (ждёт combat) | ⏳ |
| **Cargo** | CargoHasItemTrigger → **стаб** (ждёт TradeWorld интеграции) | ⏳ |
| **Location** | LocationReachedTrigger → **стаб** (ждёт PlayerChunkTracker) | ⏳ |
| **Ship** | ShipDockedAtTrigger → **не реализован** | ⏳ |
| **Player input** | PlayerInputReader → E-key NPC talk | ✅ |
| **World streaming** | NPC в WorldScene, не Bootstrap | ✅ |
| **Service UI** | OpenService action → **стаб** (ServiceUI TBD) | ⏳ |

---

## 9. Сводка рисков (обновлённая)

| # | Риск | Severity | Описание |
|---|------|----------|----------|
| **1** | **Потеря квестовых ассетов** | 🔴 **CRITICAL** | 0 FactionDefinition, 0 NpcDefinition, 0 QuestDefinition .asset файлов в проекте. GUIDs в QuestDatabase висят в никуда. Квесты не работают в Play Mode |
| 2 | Стаб-триггеры CargoHasItem/LocationReached | 🟡 | Всегда false. Квесты с этими objective не работают |
| 3 | Стаб-действия SetFlag/SwitchDialogTree/FailQuest | 🟡 | Заглушки. Branching quest chains не работают |
| 4 | OpenService stub | 🟡 | Сервисные NPC не открывают UI ремонта/заправки |
| 5 | Combat отсутствует | 🟡 | KilledEntityTrigger — стаб. Боевые квесты невозможны |
| 6 | QuestWorld раздувание | 🟢 | 1350 строк. Не блокер |
| 7 | NpcFaction naming | 🟢 | AI-система и квесты — «NpcFaction» для разных вещей |
| 8 | **Нет production-квестов + потеря тестовых** | 🔴 | Все тестовые квесты (FindArtifact, EventDrivenQuest и др.) были в .asset файлах, которых больше нет |
| 9 | Дублирование диалог-ассета | 🟢 | mira_default vs MiraDefault |
| 10 | DialogTree data quality | 🟡 | targetNodeId содержит русский текст, не nodeId |
| 11 | Нет локализации | 🟢 | Все строки хардкод-RU |
| 12 | Устаревшие комментарии в коде | 🟢 | «T-Q15 fill»/«T-Q18+ fill» — вводят в заблуждение |

---

## 10. Рекомендованный порядок действий

### 🆘 1. Восстановить квестовые ассеты (СРОЧНО)

**Первоочередная задача.** Без этого система квестов не работает.

- [ ] Открыть `QuestDatabase.asset` в Unity Inspector
- [ ] Проверить, пусты ли массивы `factions[]`, `npcs[]`, `quests[]`
- [ ] Если не пусты — для каждого GUID: `AssetDatabase.GUIDToAssetPath(guid)` чтобы найти физическое расположение файлов (возможно, они переехали в другую папку)
- [ ] Если пусты — восстановить из git history (искать коммит, где они ещё были): `git log --all --full-history -- "Assets/_Project/Quests/Data/Factions/*.asset"`
- [ ] **Если ассеты не восстанавливаются** — заново сгенерировать через `QuestCsvWindow` (CSV → SO pipeline)
- [ ] Убедиться, что `QuestDatabaseAutoDiscover` видит файлы после восстановления

### 🚀 2. Ближайшие (MVP+)

- [ ] Дописать `LocationReachedTrigger` (интеграция с `PlayerChunkTracker`)
- [ ] Дописать `CargoHasItemTrigger` (интеграция с `TradeWorld`)
- [ ] Удалить дублирующий `MiraDefault.asset`
- [ ] Починить `mira_default.asset` — targetNodeId содержит текст вместо nodeId
- [ ] Обновить устаревшие комментарии T-Q15/T-Q18

### 📋 3. Техдолг

- [ ] Вынести `FireDialogAction` в `DialogueActionRunner.cs`
- [ ] Рассмотреть split QuestWorld.cs
- [ ] M15.1: Toast displayName вместо ID
- [ ] Вынести CSV-парсер в общий `CsvUtils.cs` (3 копии парсера: QuestCsvSchema, NpcCsvImporter, DialogCsvImporter)
- [ ] NpcController — заменить Cube-плейсхолдер

### 🎮 4. Контент

- [ ] Production-квесты (5–10 штук, через CSV-импорт)
- [ ] Локализация

---

## 11. Итоговый вердикт

Кодовая база системы квестов — **одна из самых качественных в проекте**: чистая server-authoritative архитектура, event-driven, полный набор DTO, UI Toolkit, Editor tools, rate limiting, persistence.

**Однако физические квестовые данные (FactionDefinition, NpcDefinition, QuestDefinition ассеты) полностью утеряны с момента предыдущего аудита.** QuestDatabase.asset хранит GUID-ссылки в никуда. Директории Data/Factions/, Data/Npcs/, Data/Quests/ пусты. Из всех ассетов сохранились только 2 DialogTree (mira_default + дубль MiraDefault) и сам QuestDatabase.asset.

**Главное:** Пока ассеты не восстановлены, система квестов не запустится корректно — `QuestWorld.RegisterQuests()` получит пустой массив, ни один квест не будет найден по questId.

**Второстепенно:** 6 стабов (3 триггера + 3 действия) не были дописаны, хотя соответствуюшие тикеты (T-Q15/T-Q18) помечены как завершённые. Раздувание QuestWorld/QuestServer и дублирование CSV-парсеров остаются техническим долгом.

---

## Приложение A: Метрики системы квестов

| Метрика | Значение |
|---------|----------|
| `.cs` файлов в `Assets/_Project/Quests/` | 51 |
| Строк кода (приблизительно) | ~8000+ |
| Namespaces | 10 (`Quests`, `Quests.Dto`, `Quests.Client`, `Quests.UI`, `Quests.Editor`, `Quests.Triggers`, `Quests.Bridges`, `Quests.Persistence`, `Factions`, `Dialogue`) |
| QuestDefinition ассетов | **0** (было ~5) |
| NpcDefinition ассетов | **0** (было ~106) |
| FactionDefinition ассетов | **0** (было 11) |
| DialogTree ассетов | 2 (один — дубль) |
| QuestDatabase ассет | 1 (GUIDs dangling) |
| Server stubs (triggers) | 3 |
| Server stubs (actions) | 6 |
| User-facing UI окон | 3 (Dialog, Tracker, Toast) |
| Editor окон | 3 (DatabaseWindow, QuestNodeGraphView, QuestCsvWindow) |
| Интеграций с подсистемами | 7 работающих + 4 стаба |

---

## Приложение B: Файлы, которые были удалены (рекомендация из предыдущего аудита)

| Файл | Статус | Комментарий |
|------|--------|-------------|
| `QuestGraphView.cs` | ✅ **Удалён** | Был рекомендован к удалению — исполнено |
| `QuestGraphWindow.cs` | ✅ **Удалён** | Был рекомендован к удалению — исполнено |

---

## Приложение C: Старые комментарии в коде, требующие обновления

| Файл | Строка | Комментарий | Должно быть |
|------|--------|------------|-------------|
| `ConcreteTriggers.cs` | 5 | «T-Q15+ to fill» для CargoHasItem/LocationReached | Оставить стаб, но исправить номер тикета |
| `ConcreteTriggers.cs` | 151 | «Stubs (T-Q15+ to fill)» | Оставить, но T-Q15 done |
| `QuestServer.cs` | 9 | «Stub logic — real impl in T-Q06+» | ОК — T-Q06 действительно был после |
| `QuestServer.cs` | 1287 | «T-Q17 stub — ServiceUI TBD» | ОК — T-Q17 done, ServiceUI не сделан |
| `QuestServer.cs` | 1342 | «M11 demo — T-Q15 stub real impl» | OK для CompleteObjective |
| `QuestServer.cs` | 1358 | «T-Q15 stub — T-Q18+ fill» | Должно быть «T-Q19 pending» или «deferred» |
| `QuestServer.cs` | 1557 | «T-Q10 stub — T-Q15/T-Q16 fill» | GiveCargoItem/TakeCargoItem — перевести в «TBD: TradeWorld integration» |
| `QuestWorld.cs` | 328 | «T-Q15 fix: ... OfferQuest stub → real impl» | T-Q15 done, этот коммент неактуален |
| `QuestWorld.cs` | 1000 | «STUB: combat system не реализован» | Оставить — верно |

---

*Аудит выполнен: 2026-07-13. Предыдущий аудит: 2026-07-09.*
