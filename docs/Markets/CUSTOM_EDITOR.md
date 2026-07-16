# MarketConfig Custom Editor — Документация

**Тикет:** T-TRADE01
**Дата:** 2026-07-09
**Коммит:** `9997d4a` — T-TRADE01: Кастомный редактор MarketConfig + новые параметры рынка

---

## 1. Обзор изменений

Добавлен кастомный редактор для `MarketConfig` (`MarketConfigEditor`), компактный property drawer для `MarketItemConfig` (`MarketItemConfigDrawer`), окно обзора рынков (`MarketsOverviewWindow`) и новый SO `GlobalBuyPriceConfig`.

### Новые файлы

| Файл | Назначение |
|------|------------|
| `Scripts/Config/GlobalBuyPriceConfig.cs` | SO: глобальные цены скупки для режима buyAnyItem |
| `Scripts/Editor/MarketConfigEditor.cs` | Кастомный Editor + MassAddWindow |
| `Scripts/Editor/MarketItemConfigDrawer.cs` | Компактный PropertyDrawer для элементов списка |
| `Scripts/Editor/MarketsOverviewWindow.cs` | Окно: тепловая карта цен по рынкам |
| `Data/GlobalBuyPriceConfig.asset` | Инстанс GlobalBuyPriceConfig |

### Изменённые файлы

| Файл | Что изменено |
|------|-------------|
| `Config/MarketConfig.cs` | +tradeMode, buyAnyItem, globalBuyPriceConfig, sellCommission, buyCommission, priceFloorRatio, priceCeilingRatio, decayHalfLifeSeconds |
| `Core/MarketItemState.cs` | RecalculatePrice с опциональными floorRatio/ceilingRatio |
| `Core/MarketState.cs` | Кэширование per-market overrides, using ProjectC.Trade.Service |
| `Core/TradeWorld.cs` | TryBuy/TrySell: tradeMode, buyAnyItem, комиссии, per-market ApplyBuy/ApplySell |
| `Dto/TradeResultCode.cs` | +NotAllowed = 50 |
| `Service/PriceFormula.cs` | CalculatePrice/ApplyBuy/ApplySell с опциональными per-market параметрами |

---

## 2. Новые поля MarketConfig

### Trade Mode
- `tradeMode` (enum `MarketTradeMode`): `BuyAndSell` (по умолчанию), `BuyOnly`, `SellOnly`
- `buyAnyItem` (bool): если true — рынок принимает любой товар по глобальной цене
- `globalBuyPriceConfig` (GlobalBuyPriceConfig ref): источник цен для buyAnyItem

### Commissions
- `sellCommission` (float 0..1): доля от цены при продаже игроком на рынок. По умолчанию 0.8 (было захардкожено)
- `buyCommission` (float 1..2): множитель к цене при покупке у рынка. 1.0 = без наценки

### Price Corridor
- `priceFloorRatio` (float 0.1..1): минимальная цена = basePrice × floorRatio. По умолчанию 0.5
- `priceCeilingRatio` (float 1..10): максимальная цена = basePrice × ceilingRatio. По умолчанию 5.0
- `decayHalfLifeSeconds` (float 60..86400): полупериод затухания demand/supply. По умолчанию 1800 (30 мин)

---

## 3. Кастомный Editor

Открывается автоматически при выборе MarketConfig в Project View.

### Возможности

- **Trade Mode**: выпадающий enum + опциональный GlobalBuyPriceConfig
- **Commissions**: слайдеры sellCommission (0..1) и buyCommission (1..2)
- **Price Corridor**: слайдеры floor/ceiling + decay half-life
- **Bulk Actions**:
  - `Clear All Items` — удалить все товары
  - `Mass Add from Database` — окно выбора из TradeItemDatabase с дефолтными настройками
  - `Validate` — проверить itemId на дубликаты и наличие в TradeItemDatabase
  - `Duplicate Config` — клонировать SO с суффиксом `_Copy`
  - `% Buy Price` / `% Sell Price` — применить процентное изменение ко всем basePrice
  - `Set Stock to All` — установить initialStock всем товарам
- **Search**: фильтрация по itemId / displayName
- **Items List**: компактный вид с индикаторами Buy/Sell, ценой, стоком, регеном; кнопка ✕ для удаления

### Mass Add Window

`Tools → ProjectC → Trade → Mass Add Items` (или кнопка в Editor)

- Список всех TradeItemDefinition из TradeItemDatabase
- Фильтр по itemId
- Select All / Deselect All
- Дефолтные Price / Stock / Regen для новых товаров
- Автоматический пропуск дубликатов

---

## 4. Markets Overview

`Tools → ProjectC → Trade → Markets Overview`

- Таблица: строки = itemId, столбцы = locationId (displayName)
- Ячейки: basePrice + цвет (зелёный = дёшево, красный = дорого)
- Автоматически находит все MarketConfig в проекте

---

## 5. GlobalBuyPriceConfig

`Create Asset Menu → ProjectC/Trade/Global Buy Price Config`

Используется когда `MarketConfig.buyAnyItem = true`. Содержит список:
- `itemId` — ID товара
- `definition` — ссылка на TradeItemDefinition (для UI)
- `buyPrice` — цена скупки за единицу

---

## 6. Логика в TradeWorld

### TryBuy
- При `tradeMode == SellOnly` → отказ `NotAllowed`
- Цена умножается на `buyCommission`
- ApplyBuy с per-market floor/ceiling

### TrySell
- При `tradeMode == BuyOnly` → отказ `NotAllowed`
- При `buyAnyItem == true` → приём любого itemId, цена из GlobalBuyPriceConfig
- Revenue = `sellPrice × quantity × sellCommission` (вместо хардкода 0.8)
- ApplySell с per-market floor/ceiling

### MarketTick
- DecayFactor использует `market.DecayHalfLifeSeconds`
- RecalculatePrice использует `market.PriceFloorRatio` / `market.PriceCeilingRatio`

---

## 7. Обратная совместимость

- Все новые поля имеют значения по умолчанию, идентичные старому поведению
- `tradeMode = BuyAndSell`, `buyAnyItem = false`, `sellCommission = 0.8f`, `buyCommission = 1.0f`
- `priceFloorRatio = 0.5f`, `priceCeilingRatio = 5.0f`, `decayHalfLifeSeconds = 1800f`
- Старые MarketConfig.asset работают без изменений
