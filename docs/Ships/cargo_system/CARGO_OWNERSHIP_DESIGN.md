# Cargo Ownership Guard — Design Document

**Дата:** 2026-07-21
**Задача:** P5 — закрыть cargo-операции на кораблях проверкой владения
**Источник:** `docs/Ships/SHIP_REFACTOR_PLAN_2026-07-21.md` P5

---

## 1. Проблема

Сейчас любой игрок может:
- Упаковать предметы из своего инвентаря в трюм **любого** корабля (ShipCargoServer)
- Распаковать предметы из трюма **любого** корабля в свой инвентарь (ShipCargoServer)
- Загрузить товары со склада рынка в трюм **любого** корабля (MarketServer → TradeWorld)
- Выгрузить товары из трюма **любого** корабля на склад рынка (MarketServer → TradeWorld)

**Решение клиента** — показывать в UI только корабли игрока. Но серверный guard обязателен (defense in depth — клиент может слать RPC на любой shipNetId).

---

## 2. Точки входа (4 метода, 2 файла)

### Точка А: ShipCargoServer (инвентарь ↔ трюм)

Файл: `Assets/_Project/Trade/Exchange/Network/ShipCargoServer.cs`

| Метод | Строка | Что делает | Где добавить guard |
|-------|--------|-----------|-------------------|
| `RequestStoreToCargoRpc` | 86 | Инвентарь → Трюм (через курс) | После `FindShipController` (стр. 106) |
| `RequestRetrieveFromCargoRpc` | 206 | Трюм → Инвентарь (через курс) | После `FindShipController` (стр. 226) |

### Точка Б: MarketServer (склад рынка ↔ трюм)

Файл: `Assets/_Project/Trade/Scripts/Network/MarketServer.cs`

| Метод | Строка | Что делает | Где добавить guard |
|-------|--------|-----------|-------------------|
| `RequestLoadToShipRpc` | 158 | Склад → Трюм | После `zone.IsShipInZone` (стр. 168) |
| `RequestUnloadFromShipRpc` | 180 | Трюм → Склад | После `zone.IsShipInZone` (стр. 190) |

---

## 3. Реализация

### 3.1 Проверка владения

Используем существующий метод:
```csharp
ProjectC.Ship.Key.KeyRodInstanceWorld.IsOwnerOfShip(clientId, shipNetId)
```

Метод уже работает (используется в `ShipOwnershipRequirement` для F-посадки).

### 3.2 Сообщение об ошибке

- **ShipCargoServer:** `"Вы не владелец этого корабля"` через `CreateFailResult`
- **MarketServer:** `TradeResultCode.NotOwner` (новый код) через `TradeResultDto_Fail`

### 3.3 Новый TradeResultCode

Добавить `NotOwner` в enum `TradeResultCode`:
```csharp
NotOwner,  // клиент не владеет кораблём
```

Файл: `Assets/_Project/Trade/Scripts/Core/TradeResultCode.cs` (или где определён enum)

---

## 4. Dependency chain

```
ShipCargoServer → KeyRodInstanceWorld.IsOwnerOfShip  (новый using)
MarketServer    → KeyRodInstanceWorld.IsOwnerOfShip  (новый using)
```

Namespace: `ProjectC.Ship.Key` — оба файла добавляют `using ProjectC.Ship.Key;`

**Без циклических зависимостей:** Trade → Ship.Key (односторонняя, Ship.Key не зависит от Trade).

---

## 5. Тестирование

### 5.1 ShipCargoServer
1. Игрок А (владелец) — StoreToCargo на свой корабль → ✅ успех
2. Игрок Б (не владелец) — StoreToCargo на корабль А → ❌ "Вы не владелец"
3. Игрок А — RetrieveFromCargo со своего корабля → ✅ успех
4. Игрок Б — RetrieveFromCargo с корабля А → ❌ "Вы не владелец"

### 5.2 MarketServer
1. Игрок А (владелец) — LoadToShip на свой корабль → ✅ успех
2. Игрок Б (не владелец) — LoadToShip на корабль А → ❌ NotOwner
3. Аналогично для UnloadFromShip
