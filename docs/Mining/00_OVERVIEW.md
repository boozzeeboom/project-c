# Resource Gathering System — Обзор

> **Подсистема:** Сбор ресурсов (mining / harvesting / gathering)
> **Версия:** `v0.1.0` (2026-07-12)
> **Статус:** ✅ MVP завершён. Phase 1 CRITICAL fixes применены (WorldEventBus, disconnect handler, XP). Phase 2: префаб создан, Tree.asset добавлен, StatsConfig deprecation.
> **Связанные подсистемы:** `docs/MetaRequirement/` (lock-key), `docs/Character-menu/sub_inventory-tab/` (Inventory v2), `docs/Crafting_system/` (craft — следующая фаза после сбора)
> **Game Design:** `docs/gdd/GDD_11_Inventory_Items.md` (ресурсы как тип предметов)

---

## 1. Что такое сбор ресурсов в Project C

**Сбор ресурсов** — server-authoritative процесс взаимодействия игрока с миром, в котором:

1. Игрок подходит к 3D-объекту (ResourceNode — руда/жила/растение) в мире.
2. Нажимает F (action key).
3. Сервер проверяет: есть ли у игрока подходящий **инструмент** для этого узла (через `MetaRequirement`-like проверку инвентаря).
4. Начинается **таймер сбора** (например, 3 секунды). Игрок стоит на месте (или кастует).
5. По окончании таймера сервер зачисляет ресурс в инвентарь игрока (`InventoryWorld.AddItemDirect`).
6. На клиенте — тост: "Добыто: Железная руда × 1".
7. Узел может быть собран **N раз** подряд (`_maxHarvests`), после чего уходит на **перезарядку** (cooldown).
8. В конце cooldown узел появляется снова (полный ресурс — `_maxHarvests` восстанавливается).

### 1.1 ЧТО ЭТО НЕ ТАКОЕ

- ❌ Не крафт (нет станции, нет рецептов, нет буфера). Сбор → мгновенно в инвентарь.
- ❌ Не сундук (нет рандома LootTable). Сбор — предсказуемый предмет × N.
- ❌ Не quest pickup (не триггер события). Сбор — самостоятельная экономическая активность.
- ❌ Не tool durability в MVP (инструменты не ломаются). См. §9 Open Questions.

---

## 2. Скоуп MVP (для ЗБТ)

| # | Фича | В MVP? | Заметки |
|---|------|--------|---------|
| 1 | ResourceNode как NetworkObject в WorldScene_X_Z | ✅ | scene-placed, `destroyWithScene=true` |
| 2 | F-key → сбор (action key, не E) | ✅ | Уже обсуждали ремап E→F; сбор сразу на F |
| 3 | Tool check через MetaRequirement | ✅ | Должен быть инструмент `ItemType.Tool` в инвентаре |
| 4 | Таймер сбора на сервере | ✅ | Server-authoritative, не клиентский |
| 5 | Предмет сбора — в инвентарь (через `InventoryWorld.AddItemDirect`) | ✅ | Существующий API |
| 6 | MaxHarvests + cooldown Respwan | ✅ | Настраивается в ScriptableObject `ResourceNodeConfig` |
| 7 | Toast "Добыто: N предмета" | ✅ | Reuse `QuestToast` queue pattern |
| 8 | ResourceNode.asset (ScriptableObject) для конфига | ✅ | Все параметры не хардкод |
| 9 | Анимация/визуал сбора (3D-объект меняет состояние) | ⬜ α2 | MVP: только наличие/отсутствие объекта |
| 10 | Мини-игра (QTE) для ускорения сбора | ❌ | Фаза 2+ |
| 11 | Tool durability / расход инструментов | ❌ | Фаза 2+ |
| 12 | Multi-player на одном узле (конкуренция) | ❌ | MVP: soft-lock на одного игрока за раз |
| 13 | ResourceNode как world-pickup (не только scene-placed) | ❌ | MVP: только scene-placed |
| 14 | Уровни узлов (Tier 1/2/3) | ❌ | Фаза 2+ |

---

## 3. Как это вписывается в существующие системы

```
┌──────────────────────────────────────────────────────────────────┐
│                        ИГРОК                                     │
│  F-key → NetworkPlayer.Update()                                  │
│    → FindNearestResourceNode() (через InteractableManager)       │
│    → MetaRequirementClientState.RequestCanUse(наличие инструм.)  │
│         └── сервер: InventoryWorld.CountOf(clientId, toolItemId) │
│    → ResourceNodeClientState.RequestStartGather(nodeNetId)       │
│         └── сервер: таймер сбора, прерывание при движении       │
│    → InventoryWorld.AddItemDirect(clientId, resultItemId, type)  │
│         └── clients: OnSnapshotUpdated → toast                  │
└──────────────────────────────────────────────────────────────────┘
```

### 3.1 Какие системы НЕ трогаем

| Компонент | Почему не трогаем |
|-----------|-------------------|
| `InventoryWorld` core | Только используем `CountOf`, `AddItemDirect` — **никаких изменений** в существующий API |
| `InventoryServer` RPC hub | Не добавляем новые RPC в него — новый `GatheringServer` |
| `MetaRequirementRegistry` | Используем `CanPlayerUse` как is — без изменений |
| `MetaRequirementClientState` | Используем `OnAccessDenied` для отказа (нет инструмента) |
| `NetworkPlayer` F-key | Добавляем **одну строчку**: `TryGatherNearestNode()` в F-block |
| `QuestToast` | **Копируем паттерн** для `GatheringToast` (или просто переиспользуем очередь) |

### 3.2 Какие системы расширяем

| Компонент | Что меняем |
|-----------|------------|
| `InteractableManager.cs` | + `RegisterResourceNode` / `FindNearestResourceNode` (по аналогии с `RegisterPickup`/`RegisterChest`) |
| `NetworkPlayer.cs` | + `TryGatherNearestNode()` в F-block (перед boarding) |
| `NetworkManagerController.cs` | + `CreateGatheringClientState()` (auto-spawn, как для Inventory/MetaRequirement/Market) |

### 3.3 Какие системы создаём (новые)

| Файл | Назначение |
|------|------------|
| `Assets/_Project/Scripts/ResourceNode/ResourceNodeConfig.cs` | ScriptableObject: параметры узла (см. ниже) |
| `Assets/_Project/Scripts/ResourceNode/ResourceNode.cs` | NetworkBehaviour: состояние, логика сбора |
| `Assets/_Project/Scripts/ResourceNode/GatheringServer.cs` | NetworkBehaviour RPC hub: обработка сбора |
| `Assets/_Project/Scripts/ResourceNode/GatheringClientState.cs` | Client singleton: проекция, тост |
| `Assets/_Project/Scripts/ResourceNode/GatheringToast.cs` | UI тост (копия QuestToast с queue) |
| `Assets/_Project/Resources/ResourceNodes/` | Папка с .asset конфигами |
| `docs/Mining/` | Документация (эта папка) |

**Namespace:** `ProjectC.ResourceNode` (по аналогии с `ProjectC.MetaRequirement`, `ProjectC.World.Clouds`)

---

## 4. REUSE-список (что уже есть и используем)

| # | Существующий код | Как используем | Файл |
|---|-----------------|----------------|------|
| 1 | `MetaRequirement` (компонент) + `CanPlayerUse(clientId, out reason)` | Проверка инструмента (All/Any/AtLeastN) | `MetaRequirement/MetaRequirement.cs` |
| 2 | `MetaRequirementClientState.OnAccessDenied` | Бесплатный тост отказа | `MetaRequirement/MetaRequirementClientState.cs` |
| 3 | `InventoryWorld.AddItemDirect(clientId, itemId, type)` | Выдача ресурса после сбора | `Items/Core/InventoryWorld.cs:355` |
| 4 | `InventoryClientState.OnSnapshotUpdated` | Уведомление UI (P-таб) | `Items/Client/InventoryClientState.cs:80` |
| 5 | `InteractableManager` (регистр/дистанция) | `RegisterResourceNode`/`FindNearestResourceNode` | `Core/InteractableManager.cs` |
| 6 | `NetworkPlayer._lastCanUseRequestTime` / `_pendingCanUseInteractableId` | Race protection (двойной F) | `Player/NetworkPlayer.cs:84-86` |
| 7 | `LockBox` (анимация scale-pulse + emissive flash) | **Паттерн** для ResourceNode анимации (но LOOP, не одноразово) | `MetaRequirement/Test/LockBox.cs` |
| 8 | `QuestToast` (queue pattern) | **Копируем** для GatheringToast (но + ProgressBar, не текст) | `Quests/UI/QuestToast.cs` |
| 9 | `NetworkChestContainer` (NetworkBehaviour lifecycle) | **Образец** для ResourceNode | `World/Chest/NetworkChestContainer.cs` |
| 10 | `PickupItem` (IInteractable + trigger registration) | **Образец** для входа в зону узла | `Core/PickupItem.cs` |

---

## 5. Альтернативные архитектуры (анализ)

### A. Standalone NetworkBehaviour (РЕКОМЕНДУЕТСЯ)
**ResourceNode** — отдельный `NetworkBehaviour`, не наследник `MetaRequirement`.
- Плюсы: чистый lifecycle, своя логика таймера, не плодим алиасы
- Минусы: новый файл, новый RPC hub `GatheringServer`
- REUSE: проверка инструмента через `MetaRequirement` компонент на том же GameObject (`CanPlayerUse`)

### B. MetaRequirement наследник
**ResourceNode** — наследник `MetaRequirement` (+ его поля `_requiredItems`, `CanPlayerUse`)
- Плюсы: бесплатная проверка инструмента, бесплатный тост отказа
- Минусы: `MetaRequirement.CanPlayerUse` возвращает `bool` + `reason`, но не прерывает операцию (если инструмент есть — ок, таймер уже стартует). MetaRequirement спроектирован для *разовых* проверок доступа, не для *удержания* состояния во время сбора. Нам нужна **своя** machine state (GatheringIdle/GatheringInProgress/Cooldown).
- **Вердикт:** не подходит. MetaRequirement — для замок-ключ, сбор — процесс с состоянием.

### C. ResourceNode через CraftingStation pattern (из Crafting_system)
- Плюсы: reuse `CraftingStation` с буфером и таймером
- Минусы: крафт-станция спроектирована для *общей* зоны с *несколькими* игроками. Сбор — *личное* действие.
- **Вердикт:** over-engineering для MVP.

**Решение:** **Вариант A** (standalone NetworkBehaviour + `GatheringServer`).

---

## 6. Открытые вопросы (требуют ответа перед кодом)

### Q1: Какая клавиша — сбор?
- Сейчас F = посадка в корабль, E = interact (pickup/chest/npc).
- **Ты сказал:** "сейчас подобрать на Е, переделаем на F".
- **Вопрос:** сбор ресурсов сразу на F, или через E (interact) → показывает "собрать"?
- **Моя рекомендация:** **F** (action key). F сейчас в `NetworkPlayer.cs:293-308` — `Keyboard.current.fKey.wasPressedThisFrame` → посадка. Добавляем `TryGatherNearestNode()` **перед** посадкой (приоритет: сбор > посадка, т.к. если рядом и узел, и корабль — хотелось бы собрать).

<details>
<summary>F vs E детально</summary>

Оригинальный E вопросительный знак: игрок рядом с сундуком → нажать E → открыть.
Оригинальный F сцепление/действие: игрок рядом с кораблём → нажать F → сесть.

Сбор ресурсов — это **действие**, не **открытие**. Логично на F. Но если рядом узел И корабль — приоритет на узел? На корабль? Решение: узел выше (сбор быстрый, посадка — осознанное действие).

</details>

### Q2: Что с таймером, если игрок двигается?
- ✅ **Ничего — пусть бегает и рубит.** Сервер НЕ проверяет позицию во время сбора. Таймер идёт независимо от движения игрока. Единственная проверка дистанции — при старте (в RPC, clamp к `_gatherRange`).
- **Причина:** веселее, меньше кода, нет edge-case с прерываниями.
- **Защита от абуза:** один игрок = один активный сбор (`_activeGathers[clientId]`), начать второй сбор пока первый не завершён — нельзя.

### Q3: Tool check — через MetaRequirement или прямой CountOf?
- ✅ **Через MetaRequirement.** Игрок добавляет `MetaRequirement` компонент на ResourceNode (как на LockBox). Сервер проверяет инструмент через `MetaRequirement.CanPlayerUse()` — это даёт бесплатно: All/Any/AtLeastN, human-readable fail reason, автоматический toast при отказе. Пустой `_requiredItems` + `RequirementLogic.All` = тривиально true (нет требований).

### Q4: Несколько типов сборов (мин. руды / сбор растений / рубка деревьев)?
- Одна система `ResourceNode` с разными `ResourceNodeConfig`.
- Тип сбора влияет на: `_gatherSeconds` (руда 5с, растения 2с), `_maxHarvests` (руда 5, растение 1), `_cooldownSeconds`.
- **Не делаем** отдельные подсистемы для mining/harvesting/choping. Один `ResourceNode` с конфигом.

### Q5: Что если игрок начал сбор и вышел (disconnect)?
- Сервер отменяет сбор по `OnClientDisconnectCallback` (как и для движения).
- Ресурсы НЕ добавляются, узел НЕ декрементит `_currentHarvests`.

### Q6: Может ли игрок собирать в корабле (сидя за штурвалом)?
- **Нет.** `_inShip == true` → `TryGatherNearestNode()` не вызывается. F в корабле — только выход или взаимодействие с модулями.

### Q7: Что при переполнении инвентаря?
- Если `InventoryWorld.AddItemDirect` вернёт false (InventoryFull) — сбор прерван, тост "Инвентарь полон".
- Узел НЕ декрементит счётчик. Можно повторить после очистки.

### Q8: Требуется ли `PlayerInputReader` для сбора или можно прямой Input в NetworkPlayer?
- В AGENTS.md написано: "Не используйте `Keyboard.current.*.isPressed` напрямую — используйте `PlayerInputReader`".
- Но на данный момент F-key всё ещё обрабатывается через `Keyboard.current.fKey.wasPressedThisFrame` в `NetworkPlayer.cs:293`.
- **Рекомендация:** добавить `OnGatherPressed` event в `PlayerInputReader` и подписать `NetworkPlayer` на него. Это консистентно с AGENTS.md и подготавливает будущий ремап.
- Для MVP — можно оставить прямой input (как сейчас F).
- **Решение:** прямое использование `Keyboard.current.fKey.wasPressedThisFrame` в `NetworkPlayer.cs` (как сейчас). Ремап на `PlayerInputReader` — отдельный тикет (T-X4 из NPC_quests roadmap).

---

## 7. Первоначальная оценка

| Фаза | Что | Файлов | Часы |
|------|-----|--------|------|
| 1 | ScriptableObject `ResourceNodeConfig` | 2 (.cs + .asset example) | 0.5-1 |
| 2 | `ResourceNode` NetworkBehaviour (бизнес-логика) | 1 | 2-3 |
| 3 | `GatheringServer` RPC hub | 1 | 1.5-2 |
| 4 | `GatheringClientState` + `GatheringToast` (UIDocument + ProgressBar) | 2 | 2-3 |
| 5 | ResourceNode клиентская анимация (scale-pulse + emissive loop) | 1 | 1-1.5 |
| 6 | `InteractableManager` extension + F-key integration | 1 (правка) | 0.5-1 |
| 7 | Scene placement + NetworkManagerController + префаб | 1 (правка) + префаб | 0.5 |
| | **ИТОГО** | | **~8-11 ч** |

---

## 8. Документы в этом каталоге

| Файл | Содержание |
|------|------------|
| `00_OVERVIEW.md` (этот) | Что / зачем / скоуп / REUSE / альтернативы |
| `10_DESIGN.md` | Полный дизайн: классы, поля, sequence-диаграммы, edge-cases, DTO |
| `20_IMPLEMENTATION_PLAN.md` | Пошаговый план (по файлам, код не пишем) |
| `99_CHANGELOG.md` | История изменений этой документации |

---

## 9. Ссылки

- `docs/MetaRequirement/00_OVERVIEW.md` — lock-key система (для tool check)
- `docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md` — Inventory v2
- `docs/Crafting_system/00_OVERVIEW.md` — crafting (следующая фаза после сбора)
- `Assets/_Project/Scripts/Core/InteractableManager.cs` — регистр взаимодействий
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — F-key pipeline
- `Assets/_Project/Quests/UI/QuestToast.cs` — toast queue pattern (копия)
- `Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs` — образец NetworkBehaviour
