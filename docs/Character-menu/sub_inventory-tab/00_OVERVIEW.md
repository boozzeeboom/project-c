# CharacterWindow → Таб "Инвентарь" — Обзор

**Дата:** 2026-06-05
**Автор:** Mavis (Mavis)
**Статус:** ✅ Реализовано (Phases 1-7) — готово к тестированию
**Scope:** sub-system инвентаря на 2-х UI одновременно (TAB-колесо + P-таб)

---

## 1. Зачем

Игроку нужны **два способа работы с инвентарём**:

1. **TAB — быстрый доступ (GTA-стиль колесо).** 8 секторов = 8 типов предметов. Клик по сектору — список предметов этого типа. Заточено под быстрый выбор во время боя / полёта.

2. **P (Player menu) → вкладка "Инвентарь" — детальный список.** Все предметы, фильтры по типу, поиск по имени, qty, icon. Заточено под спокойный осмотр — "что у меня вообще есть?".

**Главное:** оба UI читают **одно и то же состояние** (server-authoritative `InventoryClientState`). Если подобрал предмет — оба UI обновились. Если удалил — оба убрали. Single source of truth.

---

## 2. Архитектурное правило (от Mavis)

```
InventoryUI (TAB-колесо) ⊂ CharacterWindow.tab-inventory ⊂ InventoryClientState ⊂ InventoryServer
                                                                                  ↑
                                                                            (NetworkBehaviour, RPC hub)
```

**InventoryClientState** — единственный источник истины для UI. Сервер — единственное место, где мутируется состояние.

> **Single source of truth rule:** UI (и TAB, и P-таб) читает ИСКЛЮЧИТЕЛЬНО из `InventoryClientState`. Никаких `GetComponentInChildren<Inventory>()`, никаких прямых обращений к NetworkVariable.

---

## 3. Связь TAB ↔ P-таб

| Операция | TAB-колесо (`InventoryUI`) | P-таб (`CharacterWindow` → `inventory-section`) |
|---|---|---|
| Открыть | TAB | P → клик "ИНВЕНТАРЬ" |
| Закрыть | TAB повторно / Esc | Esc / клик "ЗАКРЫТЬ" |
| Список предметов | Sublist справа (тип выбранного сектора) | ListView в `inventory-section` (все типы + фильтры) |
| Подсветка | USS class `sector-has-items` (зелёный) | Список с `InventoryListItem` (icon + name + type + qty) |
| Обновление | `InventoryClientState.OnSnapshotUpdated` → пересчёт sector'ов | `InventoryClientState.OnSnapshotUpdated` → `RefreshInventoryCache` + `Rebuild()` |
| Hover | `sector-hover` (жёлтый) | ListView item selection |
| Select | `sector-selected` (золотой) | `_selectedInventoryItem` index |
| Использовать предм. | Кнопка "ИСПОЛЬЗОВАТЬ" (TODO Phase 8) | (нет, TODO) |

**Сценарий smoke:** подобрал предмет → TAB-колесо: сектор зелёный; P → таб "Инвентарь": запись в списке. Закрыл TAB → P → та же запись. Подобрал ещё → оба UI обновились.

---

## 4. Слой за слоем (v2-архитектура)

### SERVER (host или dedicated)
```
[InventoryServer] : NetworkBehaviour        ← в BootstrapScene, DontDestroyOnLoad
    ├── InventoryWorld (POCO singleton)     ← бизнес-логика
    │     ├── Dictionary<int, ItemData>     ← ItemDatabase (Resources/Items/)
    │     ├── Dictionary<ulong, InventoryData>  ← per-player state
    │     ├── TryPickup(clientId, itemId, type, worldPos, playerPos) → result
    │     ├── TryDrop / TryMove / TryUse     (TODO Phase 8)
    │     ├── AddItemDirect(clientId, itemId, type)  ← для NetworkChestContainer
    │     └── BuildSnapshot(clientId, locationId) → InventorySnapshotDto
    ├── [Rpc(SendTo.Server, InvokePermission = Owner)] per operation
    └── [Rpc(SendTo.Owner)] через NetworkPlayer.ReceiveInventory*TargetRpc
```

### CLIENT
```
[InventoryClientState] : MonoBehaviour        ← auto-spawn в NetworkManagerController
    ├── CurrentSnapshot : InventorySnapshotDto?
    ├── LastResult : InventoryResultDto?
    ├── OnSnapshotUpdated → UI подписывается
    ├── OnInventoryResult → UI feedback
    ├── RequestPickup(itemId, type, worldPos)
    ├── RequestDrop / RequestMove / RequestUse / RequestRefresh
    └── Helpers: GetItems, GetItemsByType, GetCountByType, GetItemDefinition

[InventoryUI] : MonoBehaviour + UIDocument   ← TAB-колесо, UI Toolkit
    ├── Subscribe to OnSnapshotUpdated
    ├── 8 sector VisualElements + ListView sublist
    └── Update sectors: sector-empty / sector-has-items

[CharacterWindow] : MonoBehaviour + UIDocument   ← P-меню, 5 табов
    └── tab-inventory: ListView + filters
        └── Subscribe to OnSnapshotUpdated → RefreshInventoryCache + ApplyInventoryFilters
```

---

## 5. Файлы (что создано / изменено)

### Создано (12 файлов)
| # | Файл | Размер | Назначение |
|---|---|---|---|
| 1 | `Assets/_Project/Items/Dto/InventoryItemDto.cs` | 2.5 KB | Один предмет (struct, INetworkSerializable) |
| 2 | `Assets/_Project/Items/Dto/InventorySnapshotDto.cs` | 3.8 KB | Снимок инвентаря |
| 3 | `Assets/_Project/Items/Dto/InventoryResultDto.cs` | 3.3 KB | Результат операции |
| 4 | `Assets/_Project/Items/Dto/InventoryResultCode.cs` | 1.9 KB | Enum: Ok, NotInZone, InventoryFull, ... |
| 5 | `Assets/_Project/Items/Core/InventoryWorld.cs` | 13.8 KB | POCO singleton, бизнес-логика |
| 6 | `Assets/_Project/Items/Client/InventoryClientState.cs` | 10.9 KB | Клиентская проекция (singleton) |
| 7 | `Assets/_Project/Items/Network/InventoryServer.cs` | 12.4 KB | RPC hub (server-authoritative) |
| 8 | `Assets/_Project/Items/Editor/ItemDatasetGenerator.cs` | 11.4 KB | Editor-скрипт: 24 .asset'а одной кнопкой |
| 9 | `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` | 5.2 KB | Структура TAB-колеса |
| 10 | `Assets/_Project/UI/Resources/UI/InventoryWheel.uss` | 8.7 KB | Стили (radial layout) |
| 11 | `Assets/_Project/UI/Client/InventoryUI.cs` | 20 KB | UI Toolkit TAB-колесо (переписан с IMGUI) |
| 12 | (24 .asset) | — | Тестовый датасет (8 типов × 3) |

### Изменено (5 файлов)
| # | Файл | Diff |
|---|---|---|
| 1 | `NetworkManagerController.cs` | +30 строк: `CreateInventoryClientState()` |
| 2 | `NetworkPlayer.cs` | +30 строк: 2 TargetRpc; `SpawnInventory()` — no-op (legacy `_inventory` = null) |
| 3 | `PickupItem.cs` | +50 строк: `Collect()` → `RequestPickup` через `InventoryClientState` + подписка на `OnInventoryResult` |
| 4 | `CharacterWindow.cs` | ~80 строк: `using` + подписка + `RefreshInventoryCache` (v2 source) + handlers |
| 5 | `ItemType.cs` | +10 строк: `ItemData` + поля `maxStack`, `weightKg` |

### Сцена (BootstrapScene)
- `[InventoryWheel]` GO: UIDocument (PanelSettings=MarketPanelSettings, sourceAsset=InventoryWheel.uxml) + InventoryUI
- `[InventoryServer]` GO: NetworkObject + InventoryServer

---

## 6. Что НЕ делали (явные запреты)

- ❌ **Не** создавать параллельный UIDocument с собственным GO для инвентаря. (Был бы 4-й копипаст FIX'ов MarketWindow — избегаем.)
- ❌ **Не** создавать новый singleton-проекцию (есть `InventoryClientState`).
- ❌ **Не** трогать `docs/gdd/`, `docs/WORLD_LORE_BOOK.md`.
- ❌ **Не** писать `.meta`/`.asmdef` файлы.
- ❌ **Не** коммитить / пушить (Mavis — пользователь коммитит).
- ❌ **Не** запускать `run_tests` через MCP.

---

## 7. Текущее состояние (на 2026-06-05)

| Подсистема | Статус |
|---|---|
| ItemData (SO) + 24 .asset | ✅ Создано |
| InventoryWorld (POCO) | ✅ Готово (TryPickup — да; Drop/Move/Use — TODO) |
| InventoryClientState (singleton) | ✅ Готово |
| InventoryServer (RPC hub) | ✅ Готово, 5 RPC'шек |
| InventoryUI (TAB-колесо, UI Toolkit) | ✅ Готово, sublist, hover/select |
| CharacterWindow.tab-inventory | ✅ Готово, подписка на ClientState |
| PickupItem → RequestPickup | ✅ Готово, server confirmation |
| NetworkChestContainer → InventoryServer.AddItem | ⚠️ ЧАСТИЧНО: пока использует старый `NetworkInventory` (TODO Phase 8) |
| Multi-client sync | ⚠️ НЕ проверено (требует ParrelSync) |
| Stackable inventory (qty > 1) | ❌ TODO (сейчас 1 unit = 1 itemId) |
| Drop в мир (SpawnPickupItem) | ❌ TODO (TryDrop → "InternalError") |
| Cargo system (weightKg) | ❌ TODO (поле есть, не используется) |

---

## 8. Связанные документы (в этом каталоге)

- `00_OVERVIEW.md` — этот файл
- `10_DESIGN.md` — UXML/USS/classes детали (TAB-колесо + P-таб)
- `20_IMPLEMENTATION_PLAN.md` — пошаговый план (Phases 0-7)
- `30_VERIFICATION.md` — manual smoke checklist
- `40_CHANGES_SUMMARY.md` — diff'ы по файлам
- `50_TESTING_GUIDE.md` — manual + scripted тесты
- `60_KNOWN_ISSUES.md` — баги + TODO на Phase 8+
- `INVENTORY_V2_REFACTOR.md` — главный дизайн-док (37 KB, копия `docs/dev/INVENTORY_V2_REFACTOR.md`)

## 9. Связанные документы (вне этого каталога)

- `docs/dev/CONTRACT_V2_MIGRATION.md` — эталон v2-миграции
- `docs/dev/CONTRACTS_AS_MARKET_TAB_REFACTOR.md` — паттерн "merge в таб"
- `docs/Character-menu/00_OVERVIEW.md` — P-меню в целом
- `docs/Character-menu/10_DESIGN.md` — P-меню дизайн
- `docs/INVENTORY_SYSTEM.md` — старое состояние (v0.0.7)
- `docs/gdd/GDD_11_Inventory_Items.md` — game design
- `AGENTS.md` — hard rules проекта
- `unity-v2-subsystem-migration` skill — методология v2-миграции
