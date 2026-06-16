# Chest + Pickup — Manual Test Guide (R3-005)

**Дата:** 2026-06-05
**Зависит от:** `00_OVERVIEW.md`, `20_IMPLEMENTATION_PLAN.md`, `50_TESTING_GUIDE.md`
**Связанный дoк:** `refactor_log_2026-06-05.md` (Phase 7.5)

Сценарий **тестирует связку Pickup + Chest → InventoryClientState → UI** в Play mode.

> **⚠️ Скриншоты делаешь ты** (per AGENTS.md, Mavis не делает скриншотов).

---

## 0. Подготовка

```bash
# 1. Открой Unity Editor (6000.4.1f1)
# 2. Дождись компиляции → 0 errors (Phase 7.5 + R3-005)
# 3. BootstrapScene должна содержать:
#    - [NetworkManager]
#    - [InventoryWheel] (UI Toolkit TAB-колесо)
#    - [InventoryServer] (RPC hub)
#    - [CharacterWindow]
```

**Сделай скриншот Hierarchy** — для протокола.

---

## 1. Тестовая карта: WorldScene_0_0 @ (40000, 2512, 40000)

### Что расставлено (R3-005, 2026-06-05)

| GO | Координаты (относ. центра) | Назначение |
|---|---|---|
| `Chest_Main`   | (0,    0,    0)  | Главный сундук |
| `Chest_North`  | (0,    0,    10) | Северный сундук |
| `Chest_East`   | (10,   0,    0)  | Восточный сундук |
| `Pickup_Res_1` | (-8,   0.5,  0)  | Железная руда (Resources) |
| `Pickup_Res_2` | (-6,   0.5,  4)  | Железная руда (Resources) |
| `Pickup_Res_3` | (8,    0.5,  0)  | Железная руда (Resources) |
| `Pickup_Food_1`| (0,    0.5,  6)  | Бутыль воды (Food) |
| `Pickup_Food_2`| (0,    0.5, -6)  | Бутыль воды (Food) |
| `Pickup_Fuel_1`| (6,    0.5,  4)  | Антигравитационное топливо (Fuel) |

**LootTable_TestCommon** (на всех 3 сундуках):
- **Гарантированные** (3 шт): первый `Resources` + первый `Food` + первый `Fuel` (из датасета)
- **Entries** (1 шт): `Medical` 70% × 1-2 шт

**Pickup** имеют `NetworkObject` (для spawn) + `BoxCollider(isTrigger)` 1.2×1.2×1.2 + visual Cube 0.4×0.4×0.4.

### Prefab'ы
- `Assets/_Project/Prefabs/PickupItem_Test.prefab` — pickup
- `Assets/_Project/Prefabs/NetworkChestContainer_Test.prefab` — chest
- `Assets/_Project/Resources/Items/LootTable_TestCommon.asset` — LootTable

---

## 2. Перед Play mode: проверь в Editor

### 2.1 Проверь что сцена содержит тестовые GO

```
Hierarchy → WorldScene_0_0 → должны быть:
  ✓ Chest_Main
  ✓ Chest_North
  ✓ Chest_East
  ✓ Pickup_Res_1, Pickup_Res_2, Pickup_Res_3
  ✓ Pickup_Food_1, Pickup_Food_2
  ✓ Pickup_Fuel_1
```

### 2.2 Проверь что InventoryClientState есть в BootstrapScene

```
Hierarchy → BootstrapScene → должен быть [InventoryClientState] (root GO, auto-spawn)
```

### 2.3 Проверь `[InventoryServer]` в BootstrapScene

```
Hierarchy → BootstrapScene → [InventoryServer] (root GO, с InventoryServer + NetworkObject)
```

**Скриншот Hierarchy + Inspector** для протокола.

---

## 3. Play mode — Pickup test (T1)

### 3.1 Запуск
```
1. ▶ Play
2. В NetworkManagerController inspector: StartHost
3. Spawn player в WorldScene_0_0 (используй dev spawn: 0,0 → 40000, 2512, 40000)
```

**Если spawn в (0,0,0):** используй `/teleport 40000 2512 40000` (если есть dev-консоль) или найди WorldSceneSpawner.

### 3.2 Подойди к Pickup_Res_1 (в 8m к западу от центра)

```
4. Console должен показать:
   [NetworkPlayer] Spawned at (40000, 2512, 40000)
   [InventoryClientState] OnEnable
   [InventoryServer] OnNetworkSpawn. IsServer=True, _itemCache=N
   [InventoryWorld] Created. Items registered: 32 (24 новых + 8 старых)
5. Лети/иди к (39992, 2513, 40000) — координата Pickup_Res_1
6. Нажми E
```

**Ожидаемо в Console:**
```
[NetworkPlayer] NearestInteractable = Pickup_Res_1, distance=...
[PickupItem] <ItemName> RequestPickup sent (itemId=N)
[InventoryServer] RequestPickupRpc received from client 0
[InventoryWorld] TryPickup: client 0, itemId=N, type=Resources, dist=...
[InventoryClientState] OnSnapshotReceived
[InventoryUI] HandleSnapshotUpdated: 1 items total
[CharacterWindow] HandleInventorySnapshotUpdated: 1 items
```

**Ожидаемо в мире:**
- Pickup_Res_1 **деактивируется** (через 100-300ms после E)

**Ожидаемо в UI:**
- TAB-колесо: сектор "РЕСУРСЫ" стал **зелёным**, label показывает `[1]`
- P-меню → таб "Инвентарь": 1 запись (Железная руда, qty=1)

**Скриншоты** для протокола:
- `Assets/Screenshots/t1a_after_pickup_tab.png`
- `Assets/Screenshots/t1b_after_pickup_p_tab.png`

### 3.3 Подбери остальные Pickup'ы (T2)

```
7. Подбери Pickup_Res_2 (должен дать ещё 1 руду)
8. Подбери Pickup_Res_3 (3-я руда)
9. Подбери Pickup_Food_1, Pickup_Food_2 (2 еды)
10. Подбери Pickup_Fuel_1 (1 топливо)
```

**Ожидаемо:**
- TAB-колесо: 3 сектора зелёные (Resources=[3], Food=[2], Fuel=[1])
- P-таб: 6 записей (3 Resources, 2 Food, 1 Fuel)
- Все 6 Pickup'ов в мире деактивированы

**Скриншот:** `Assets/Screenshots/t2_full_pickup.png`

### 3.4 Cross-tab verify (T3)

```
11. Открой TAB → проверь зелёные сектора
12. Закрой TAB → открой P
13. Таб "Инвентарь" → проверь список
14. Вернись в TAB → список тот же
```

**Ожидаемо:** данные согласованы через `InventoryClientState` (pitfall #11 — cross-tab feedback).

---

## 4. Play mode — Chest test (T4)

### 4.1 Открой сундук

```
15. Подойди к Chest_Main (координаты (40000, 2512, 40000))
16. Нажми E
```

**Ожидаемо в Console:**
```
[NetworkChestContainer] TryOpen - Spawned=True, IsServer=True
[NetworkChestContainer] RequestOpenChestServerRpc received from client 0
[NetworkChestContainer] ServerRpc: Generated 4 loot items for client 0
[NetworkChestContainer] v2 AddItem: itemId=N1, type=Resources, ok=True
[NetworkChestContainer] v2 AddItem: itemId=N2, type=Food, ok=True
[NetworkChestContainer] v2 AddItem: itemId=N3, type=Fuel, ok=True
[NetworkChestContainer] v2 AddItem (Medical): itemId=N4, type=Medical, ok=True
[InventoryServer] SendSnapshot to client 0
[InventoryClientState] OnSnapshotReceived
```

**Ожидаемо в UI:**
- TAB-колесо: 4 сектора зелёные (Resources, Food, Fuel, **Medical** — НОВЫЙ!)
- P-таб: список пополнился на 4 записи (3 гарантированных + 1 Medical)

**Ожидаемо в мире:**
- `Chest_Main` крышка открылась (анимация поворота)

**Скриншот:** `Assets/Screenshots/t4_chest_opened.png`

### 4.2 Открой остальные сундуки (T5)

```
17. Подойди к Chest_North (40000, 2512, 40010) → E
18. Подойди к Chest_East (40010, 2512, 40000) → E
```

**Ожидаемо:**
- Каждый сундук добавляет по 3-4 предмета
- TAB: все 4 сектора растут (Resources, Food, Fuel, Medical)
- P-таб: 12-18 записей

**Скриншот:** `Assets/Screenshots/t5_all_chests.png`

### 4.3 Повторное открытие (T6) — должно быть no-op

```
19. Подойди к Chest_Main ещё раз → E
```

**Ожидаемо в Console:**
```
[NetworkChestContainer] TryOpen SKIPPED: Already open
```

**Скриншот не нужен** (это просто verify).

### 4.4 Distance check (T7) — anti-cheat

```
20. Отойди от Chest_North на 100m (например, телепортируйся)
21. Нажми E
```

**Ожидаемо в Console:**
```
[NetworkChestContainer] Client 0 too far: 100.0m (max: 3.0m)
```

**Скриншот не нужен.**

---

## 5. Edge cases (T8-T10)

### 5.1 T8: Существующий NetworkInventory на Player (legacy)

Если на PlayerPrefab есть `NetworkInventory` (старый компонент), он всё ещё может быть. Тогда:
- `InventoryServer.Instance != null` → используется **v2** (новое)
- `NetworkInventory` остаётся как legacy fallback

Проверь: после открытия сундука через `v2`, устаревший `NetworkInventory` НЕ должен показывать items в TAB (TAB читает только `InventoryClientState`).

### 5.2 T9: InventoryServer missing (safety)

Если по какой-то причине `[InventoryServer]` не заспавнился (например, `ScenePlacedObjectSpawner` не сработал), то:
- `InventoryServer.Instance == null`
- `NetworkChestContainer` упадёт в **legacy fallback** (`NetworkInventory.AddItem`)
- TAB-колесо НЕ обновится (legacy `NetworkInventory` шлёт NetworkVariable, но UI подписан только на `InventoryClientState`)
- Console warning: `[NetworkChestContainer] InventoryServer missing — used legacy NetworkInventory. UI may not update.`

**Диагностика:** если T4-T5 не обновляют UI — проверь что `[InventoryServer]` есть в BootstrapScene и `IsServer=True`.

### 5.3 T10: PickupItem already collected

Если E нажат дважды на тот же Pickup (race condition):
- Первый E: `_isAwaitingServer = true`, RPC отправлен
- Второй E: `Collect()` early-return (если `_isCollected || _isAwaitingServer`)
- После server confirmation: `_isCollected = true`, `gameObject.SetActive(false)`

**Ожидаемо:** дубля нет.

---

## 6. Multi-client (T11 — опционально, требует ParrelSync)

### 6.1 Setup
```
1. Установи ParrelSync
2. File → ParrelSync → Create clone → InventoryTest
3. Открой оба проекта
4. Project A: Play → Start Server
5. Project B: Play → Start Client
```

### 6.2 Cross-client pickup
```
6. Client A: подбери Pickup_Res_1
7. Client B: TAB → видно ли тот же предмет?
```

**Ожидаемо:** 
- Client A: TAB показывает зелёный сектор
- Client B: TAB показывает зелёный сектор (server-authoritative snapshot)
- Pickup_Res_1 деактивирован на обоих

### 6.3 Cross-client chest
```
8. Client B: подойди к Chest_Main → E
9. Client A: TAB → видны добавленные предметы?
10. Client B: TAB → видны те же предметы
```

**Ожидаемо:** оба клиента видят общее состояние инвентаря.

**Если не работает:**
- Пришли Console обоих клиентов
- Скорее всего `ScenePlacedObjectSpawner` спавнит `[InventoryServer]` ТОЛЬКО на host, не на client. Это by design (RPC server-only).

---

## 7. Compile check после всех тестов (regression)

```
Stop Play → Window → General → Console → Clear
0 errors expected
```

---

## 8. Success criteria

Сессия считается успешной, если:
- [x] Compile 0 errors
- [ ] T1-T3: Pickup работает (E → оба UI обновляются)
- [ ] T4-T5: Chest работает (E → loot добавлен, оба UI обновляются)
- [ ] T6: повторное открытие chest = no-op
- [ ] T7: distance check работает
- [ ] T11 (optional): multi-client sync

---

## 9. Что делать если что-то не работает

### T1-T3 (Pickup) не работает
1. Console: `[InventoryServer] OnNetworkSpawn. IsServer=True`?
2. Если `IsServer=False` → ты на client-only, нужен Host
3. Console: `[InventoryWorld] Created. Items registered: 32`? (если 0 — `ItemDatabase` пустая, `Resources/Items/` не загрузилась)
4. Console: `[PickupItem] ItemName RequestPickup sent (itemId=N)`? Если нет — pickup не дошёл до `Collect()`

### T4-T5 (Chest) не работает
1. Console: `[NetworkChestContainer] ServerRpc: Generated N loot items`? Если 0 — `LootTable` пустая
2. Console: `[NetworkChestContainer] v2 AddItem: ... ok=True`? Если `ok=False` — `[InventoryServer]` не заспавнился, или `IsServer=False`
3. UI не обновляется: проверь `InventoryClientState.Instance.OnSnapshotUpdated` срабатывает

### T11 (Multi-client) не работает
1. Оба клиента видят `IsServer=True` (на хосте) и `IsServer=False` (на клиенте)
2. `[InventoryServer]` заспавнен на обоих (scene-placed → да)
3. `InventoryClientState.OnSnapshotReceived` срабатывает на обоих

---

## 10. Сводный список тестов

| # | Название | Длительность | Что проверяет |
|---|---|---|---|
| 0  | Compile check | 30s | 0 errors |
| 0a | Editor verify | 1 min | GO в Hierarchy |
| 1  | Pickup один предмет | 1 min | E → оба UI обновляются |
| 2  | Pickup все 6 | 3 min | 3 сектора зелёные |
| 3  | Cross-tab verify | 1 min | TAB ↔ P-таб sync |
| 4  | Chest open | 1 min | loot → 4-й сектор (Medical) |
| 5  | All chests | 2 min | 3 сундука |
| 6  | Chest repeat | 30s | no-op |
| 7  | Distance check | 30s | anti-cheat |
| 8  | Legacy NetworkInventory | 1 min | v2 приоритет |
| 9  | InventoryServer missing | 1 min | fallback на legacy |
| 10 | Double-pickup race | 30s | защита от дубля |
| 11 | Multi-client (ParrelSync) | 10 min | cross-client sync (опционально) |

**Итого без T11:** ~15 мин. С T11: ~25 мин.

---

## 11. Что коммитить после успешного тестирования

```bash
# Изменённые:
git add Assets/_Project/Scripts/World/Chest/NetworkChestContainer.cs   # R3-005 миграция
git add Assets/_Project/Scenes/World/WorldScene_0_0.unity              # 3 сундука + 6 pickups
git add Assets/_Project/Resources/Items/LootTable_TestCommon.asset      # LootTable
git add Assets/_Project/Resources/Items/LootTable_TestCommon.asset.meta
git add Assets/_Project/Prefabs/PickupItem_Test.prefab                 # Pickup prefab
git add Assets/_Project/Prefabs/PickupItem_Test.prefab.meta
git add Assets/_Project/Prefabs/NetworkChestContainer_Test.prefab      # Chest prefab
git add Assets/_Project/Prefabs/NetworkChestContainer_Test.prefab.meta
git add docs/Character-menu/sub_inventory-tab/70_CHEST_PICKUP_TESTS.md  # Этот файл
git add docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md           # Обновить §5, §10
git add docs/Character-menu/sub_inventory-tab/60_KNOWN_ISSUES.md       # Перенести #1 в resolved
git add docs/Character-menu/sub_inventory-tab/COMMIT_MESSAGE.txt       # +R3-005

# Commit:
git commit -F docs/Character-menu/sub_inventory-tab/COMMIT_MESSAGE.txt
```

---

## 12. См. также

- `docs/Character-menu/sub_inventory-tab/00_OVERVIEW.md` — общий обзор
- `docs/Character-menu/sub_inventory-tab/20_IMPLEMENTATION_PLAN.md` — план (Phases 0-7 + R3-005)
- `docs/Character-menu/sub_inventory-tab/40_CHANGES_SUMMARY.md` — diff по файлам
- `docs/Character-menu/sub_inventory-tab/50_TESTING_GUIDE.md` — тесты TAB-колеса (без chest)
- `docs/Character-menu/sub_inventory-tab/60_KNOWN_ISSUES.md` — баги + TODO
- `docs/Character-menu/sub_inventory-tab/INVENTORY_V2_REFACTOR.md` — главный дизайн-док
- `docs/Character-menu/refactor_log_2026-06-05.md` — Phase 7.5 визуальный фикс стилей
- `AGENTS.md` — hard rules
