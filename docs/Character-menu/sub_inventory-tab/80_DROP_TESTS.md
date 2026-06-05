# Drop в мир — Manual Test Guide (Phase 10, 2026-06-05)

**Связанные:** `INVENTORY_V2_DROP_DESIGN.md`, `60_KNOWN_ISSUES.md` §12, `70_CHEST_PICKUP_TESTS.md`
**Тестирует:** Phase 10 — drop предмета из инвентаря в мир (server-spawn PickupItem)
**Предусловия:** Phase 1-9 пройдены, itemData, pickups, chests в WorldScene_0_0.

---

## Предусловия

1. **Player host'ит** (StartHost)
2. Player имеет хотя бы 1 предмет в инвентаре (из Phase 7 — pick up pickup'а в WorldScene_0_0)
3. Открой TAB → колесо видно → кликни на сектор с предметом → sublist показывает предмет
4. **Новая кнопка "БРОСИТЬ"** видна между "ИСПОЛЬЗОВАТЬ" и "ЗАКРЫТЬ" (оранжевая)

---

## T14: Drop одного предмета

**Шаги:**
1. Открой TAB → выбери сектор (например Food) → выбери предмет в sublist
2. Нажми **БРОСИТЬ**
3. Закрой TAB (Tab)
4. **Повернись** (Mouse) — ищи pickup в 1.5м впереди

**Ожидаемо:**
- `InventoryClientState` шлёт `RequestDropRpc(slotIndex, 1, dropPos, playerPos)`
- Server: `InventoryWorld.TryDrop` → удаляет itemId из `_foodIds`
- Server: spawn `PickupItem` prefab на `dropPos` (1.5м перед игроком)
- Server: `SendSnapshot` → client UI обновляется
- TAB-колесо и P-таб "Инвентарь" показывают на 1 меньше предмет

**Console ожидаемо:**
```
[InventoryWorld] Player 0 dropped Food ID=11 at (40000.0, 2512.0, 40001.5) (still has 0 of this type)
[InventoryServer] Dropped PickupItem at (40000.0, 2512.0, 40001.5): id=11 type=Food netObjId=...
[InventoryClientState] OnSnapshotReceived: items=N-1, handlers=2
[PickupItem] <ItemName> подобран (только при re-pickup)
```

---

## T15: Drop на расстоянии > 3м (анти-чит)

**Шаги:**
1. Открой TAB → выбери предмет
2. **Измени worldPos вручную** через какой-нибудь DevMenu (TODO: такого нет, пропусти если нельзя)
3. Или: попроси в чате друга (multi-client) дропнуть от тебя на 5м

**Ожидаемо (если возможно проверить):**
- Server: `Distance 5.2м > 3.0м` → `InventoryResultCode.NotInZone`
- Server: `SendResult` с ошибкой
- Client: `OnInventoryResult` → UI показывает "Слишком далеко"
- Pickup НЕ спавнится, инвентарь НЕ изменяется

**Skip:** если нет DevMenu. **Verify code path** — посмотри `InventoryWorld.TryDrop` строка 203-207.

---

## T16: Drop пустого слота

**Шаги:**
1. Открой TAB → сектор БЕЗ предметов (например Medical, если их нет)
2. **Стоп** — sublist не показывает ничего, ничего не выбрать
3. **Либо:** выбери слот через DevMenu, который не существует (slotIndex=99)
4. Нажми БРОСИТЬ

**Ожидаемо:**
- Server: `slotIndex=99` ≥ `MAX_SLOTS=32` → `InvalidSlot`
- Client: "Неверный слот"
- Никаких изменений

---

## T17: Drop того же предмета дважды

**Шаги:**
1. Возьми 2 одинаковых предмета (например, Chest выдаёт 2 Food)
2. Drop первого → pickup в мире
3. Drop второго → ещё один pickup в мире

**Ожидаемо:**
- 2 pickup'а в мире рядом (на расстоянии друг от друга, потому что playerPos между drop'ами может измениться)
- Inventory теперь пуст по этому типу
- Можно подобрать оба обратно

**Console:**
```
[InventoryWorld] Player 0 dropped Food ID=11 at ... (still has 1 of this type)
[InventoryServer] Dropped PickupItem ... netObjId=...
[InventoryWorld] Player 0 dropped Food ID=11 at ... (still has 0 of this type)
[InventoryServer] Dropped PickupItem ... netObjId=...
```

---

## T18 (multi-client, опционально): Drop виден другому игроку

**Предусловия:** ParrelSync или два Editor'а запущены (host + client)

**Шаги:**
1. Host дропает предмет
2. Client (второй Editor) смотрит на мир

**Ожидаемо:**
- Client видит pickup в мире (NGO NetworkObject.Spawn реплицирует)
- Client не может его подобрать (не его)
- Если client подбирает — pickup пропадает у обоих (server-side state)

---

## Что может пойти не так

| Симптом | Возможная причина | Fix |
|---|---|---|
| Console: `[InventoryServer] CRITICAL: dropPickupPrefab не задан!` | Prefab не привязан в инспекторе `[InventoryServer]` | Сериализуй через SerializedObject (см. INVENTORY_V2_DROP_DESIGN.md §11 Phase E) |
| Console: `[InventoryServer] Drop: prefab missing NetworkObject!` | Другой prefab без NetworkObject | Проверь что prefab = `PickupItem_Test.prefab` |
| Drop работает, pickup не появляется на клиенте | Prefab не зарегистрирован в NetworkPrefabsList | Зарегистрируй через `nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab })` |
| `OnSnapshotReceived: handlers=2` не срабатывает | NetworkManager не запущен | StartHost |
| `slotIndex=... пуст` | BuildSnapshot порядок != TryDrop порядок | Проверь оба — должны итерировать `Enum.GetValues(typeof(ItemType))` в одном порядке |
| Distance > 3м всегда | playerPos = (0,0,0) потому что localPlayer == null | Проверь `FindFirstObjectByType<NetworkPlayer>` |
| ItemId не найден после drop | `InventoryWorld.GetItemDefinition` не находит | Сначала дропни предмет через PickupItem (он auto-register'ит) |

---

## Проверка кода

- `Assets/_Project/Items/Core/InventoryWorld.cs` — `TryDrop` строки 161-220
- `Assets/_Project/Items/Network/InventoryServer.cs` — `RequestDropRpc` строки 115-160
- `Assets/_Project/Items/Client/InventoryClientState.cs` — `RequestDrop` строки 123-127
- `Assets/_Project/UI/Client/InventoryUI.cs` — `OnDropClicked` строки 429-455
- `Assets/_Project/UI/Resources/UI/InventoryWheel.uxml` — `<ui:Button name="drop-btn">` строка 77
- `Assets/_Project/UI/Resources/UI/InventoryWheel.uss` — `.action-btn.drop` строка 257

---

## Что дальше после Phase 10

- **Phase 11+ (опционально)**: Use (apply effect на игрока), Move (rearrange в инвентаре), Stackable
- **Phase 12**: Cargo (sector для хранения больших объёмов), Weight system
- **Phase 13**: Persistence (save на disconnect, restore на reconnect)
