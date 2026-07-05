# Repair Manager — Ship Repainting (Покраска корабля)

**Дата:** 2026-07-19

---

## Objective
Через RepairManagerWindow добавить покраску корабля: выбор цвета, списание кредитов (цена из инспектора), синхронизация цвета через NetworkVariable, сохранение после рестарта сервера.

## Changes

| Файл | Действие |
|------|----------|
| `Ship/Network/ShipTelemetryState.cs` | Добавить 3 байта `shipColorR/G/B` |
| `Player/ShipController.cs` | `_shipColor` + `ApplyShipColor()` по MeshRenderer |
| `Ship/ShipModuleServer.cs` | `RepaintShipRpc` — валидация + списание + ClientRpc |
| `Ship/RepairManager.cs` | `[SerializeField] _repaintCost` |
| `Ship/UI/RepairManagerWindow.cs` | Палитра цветов + кнопка «Покрасить» |
| `Resources/UI/RepairManagerWindow.uxml` | Блок `.repair-paint-section` |
| `Resources/UI/RepairManagerWindow.uss` | Стили `.paint-color-btn` |

## Architecture

```
Пользователь: выбирает цвет → «Покрасить (N кр.)»
       ↓ RepairManagerWindow.OnPaintClicked(color)
       ↓ ShipModuleServer.RequestRepaintShip(keyInstanceId, color)
       ↓ [ServerRpc]
Сервер: валидация ключа + docked
       ↓ TradeWorld.Repository.TryModifyCredits(-cost)
       ↓ ShipController._shipColor = color
       ↓ UpdateTelemetryState() → telemetry.shipColorR/G/B
       ↓ [ClientRpc] ShipController.ApplyShipColor()
Все клиенты: MaterialPropertyBlock на все MeshRenderer → цвет меняется
```

Цвет сохраняется в `ShipTelemetryState` → NetworkVariable → переживает рестарт сервера.

## Steps

### 1. ShipTelemetryState — 3 байта цвета
- Добавить `public byte shipColorR, shipColorG, shipColorB`
- `NetworkSerialize`: `serializer.SerializeValue(ref shipColorR/G/B)`
- `Equals`: сравнить все 3
- `GetHashCode`: `HashCode.Combine(shipColorR, shipColorG, shipColorB)`
- Дефолт = 0,0,0 → клиент трактует как «цвет не задан» (пропускаем ApplyShipColor)

### 2. ShipController — ApplyShipColor
- Добавить `private Color _shipColor` (локальный кеш)
- `ApplyShipColor(Color c)`: собрать все `MeshRenderer` (GetComponentsInChildren), `MaterialPropertyBlock.SetColor("_BaseColor", c)` (URP) или `"_Color"` (Built-in fallback)
- В `HandleTelemetryValueChanged`: если `next.shipColorR+G+B != 0`, применить цвет
- В `OnNetworkSpawn` (клиент): применить цвет из текущего telemetry один раз

### 3. ShipModuleServer — RepaintShipRpc
- `[ServerRpc] RepaintShipRpc(int keyInstanceId, byte r, byte g, byte b)`
- Валидация: владение ключом + корабль в доке (как в InstallModule)
- `TradeWorld.Instance.Repository.TryModifyCredits(clientId, -repaintCost, out _, out failReason)`
- Если fail → NotifyClientError
- Если OK → `_shipController.SetShipColor(new Color32(r,g,b,255))` → триггерит UpdateTelemetryState
- `[ClientRpc] ApplyPaintClientRpc(byte r, byte g, byte b)` → вызывает `ShipController.ApplyShipColor` на всех клиентах

### 4. RepairManager — цена покраски
- Добавить `[SerializeField] private int _repaintCost = 500` (default 500 CR)
- Передавать `_repaintCost` в `RepairManagerWindow` при открытии

### 5-7. UXML + USS + C# — палитра цветов
- UXML: `<ui:VisualElement name="repair-paint-section" class="repair-section">` с лейблом «Цвет корабля:» и контейнером для кнопок-цветов + кнопка «🎨 Покрасить»
- USS: `.paint-color-btn` — квадрат 32×32px с border-radius 4px, активный — обводка 2px белым
- C#: Кнопки preset-цветов (8-10 цветов: белый, серый, чёрный, красный, синий, зелёный, жёлтый, оранжевый, фиолетовый, бирюзовый), выбор через `_selectedPaintColor`, вызов `ShipModuleServer.RequestRepaintShip(keyInstanceId, r, g, b)`

### 8. Проверить компиляцию
