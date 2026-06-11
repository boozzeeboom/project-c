# GDD-22: Economy & Trading — Project C: The Clouds

**Версия:** 5.1 | **Дата:** 10 июня 2026 г. (дизайн-контент без изменений с 5 июня 2026 г.; добавлена §X «Реализация в коде») | **Статус:** ✅ V2 развёрнута + Contract→Quest bridge + ItemRegistry
**Автор:** Qwen Code (3.0) + Mavis (4.0 — рефакторинг под bootstrap+24 сцены) + Mavis (5.0 — финальный v2 cleanup, GDD sync) + Mavis (5.1, 2026-06-10 — доп. про Contract bridge + ItemRegistry)

---

## 1. Overview

Экономическая система Project C: The Clouds — **живой рынок «Дальнобойщики над облаками»**. Игроки торгуют между городами, выполняют контракты НП, занимаются контрабандой. Рынок динамически реагирует на действия **игроков** и **NPC-трейдеров**.

**Ключевые компоненты:**
- **Валюта:** Кредиты (CR)
- **Ресурсы:** 4+ типов с динамическими ценами
- **Торговля:** NPC-магазины, P2P, чёрный рынок
- **Контракты:** НП, мануфактуры, военные, контрабанда
- **Динамическая экономика:** Спрос/предложение, события, коллапсы
- **Серверная авторитетность:** Вся экономика — на сервере

**Этап реализации:** Этап 3-4
**Архитектура (v4.0):** Bootstrap + 24 World-сцены, server-authoritative, UI Toolkit, см. [docs/Markets/TRADE_V2_DESIGN.md](../Markets/TRADE_V2_DESIGN.md) и [docs/Markets/TRADE_V2_INTEGRATION.md](../Markets/TRADE_V2_INTEGRATION.md)

**Связанный документ:** [GDD_25_Trade_Routes.md](GDD_25_Trade_Routes.md) — полные спецификации маршрутов, логистики, контрактов

---

## 2. Currency System

### Кредиты

| Параметр | Описание |
|----------|----------|
| Название | Кредиты Новой Цивилизации |
| Обозначение | CR |
| Тип | Основная валюта |
| Получение | Контракты, торговля, квесты, контрабанда |
| Расход | Заправка, ремонт, покупка ресурсов, улучшения, налоги |
| Хранение | Аккаунт игрока (сервер) |
| Максимум | [🔴 Запланировано] 9,999,999 CR |

### Заработок

| Источник | Диапазон | Описание |
|----------|----------|----------|
| НП-контракт доставки | 50-500 CR | 30% от стоимости груза |
| Свободная торговля | Вариативно | Купи дёшево, продай дорого |
| Контрабанда | 200-2000 CR | Высокий риск, высокая награда |
| Военный контракт | 300-1500 CR | ×2-3 к стандартной цене |
| Квесты гильдий | 100-5000 CR | Зависит от ранга |
| Под расписку | 30% стоимости | Туториал-крючок, первые 2 часа |

---

## 3. Resources

### Базовые ресурсы

| Ресурс | Тип | Редкость | Базовая цена | Вес/ед | Объём/ед | Описание |
|--------|-----|----------|-------------|--------|----------|----------|
| **Мезий (канистра)** | Топливо | Обычный | 10 CR | 10 кг | 0.5 м³ | Заправка кораблей |
| **Антигравий (слиток)** | Компонент | Необычный | 50 CR | 5 кг | 0.2 м³ | Ремонт двигателей |
| **МНП (контейнер)** | Медикамент | Редкий | 100 CR | 3 кг | 0.3 м³ | Стимуляторы, медицина |
| **Латекс (рулон)** | Технический | Обычный | 5 CR | 8 кг | 1.0 м³ | Изоляция, уплотнители |

### Торговые ресурсы (контракты)

| Ресурс | Тип | Базовая цена | Вес/ед | Объём/ед | Описание |
|--------|-----|-------------|--------|----------|----------|
| **Двигатель (блок)** | Компонент | 500 CR | 50 кг | 2.0 м³ | Тяжёлый, хрупкий |
| **Броня (плита)** | Военный | 200 CR | 30 кг | 1.5 м³ | Военный товар |
| **Продовольствие** | Массовый | 8 CR | 15 кг | 1.0 м³ | Объёмный, дешёвый |
| **Контрабанда (ящик)** | Нелегальный | 300 CR | 12 кг | 0.8 м³ | Высокий риск |

### Источники и спрос

| Ресурс | Источники | Основной спрос | Где дёшево | Где дорого |
|--------|-----------|---------------|------------|------------|
| Мезий | НП-конвои, заправка | Все города | Примум (производство) | Квартус (удалённость) |
| Антигравий | Мануфактура «Аврора» | Ремонт, улучшения | Примум (Аврора) | Тертиус (дефицит) |
| МНП | Мануфактура «Прометей» | Медицина, квесты | Квартус (Прометей) | Секунд (военный спрос) |
| Латекс | Мануфактура «Гермес» | Технический | Тертиус (Гермес) | Примум (удалённость) |
| Двигатели | Мануфактура «Аврора» | Улучшения кораблей | Примум | Все удалённые |
| Броня | Мануфактура «Титан» | Военные нужды | Секунд | Квартус |
| Контрабанда | Чёрный рынок | Подполье | Заброшенные платформы | НП-города (риск) |

---

## 4. Pricing Model — Динамическая Экономика

### Формула цены

```
price(location, item) = base_price(item) 
    × (1 + demand_factor - supply_factor) 
    × reputation_discount 
    × event_multiplier
    × route_multiplier
```

| Множитель | Диапазон | Описание |
|-----------|----------|----------|
| `base_price` | Фиксированная | Базовая цена товара |
| `demand_factor` | 0.0 … +1.5 | Спрос в локации (растёт при покупках) |
| `supply_factor` | 0.0 … +1.5 | Предложение в локации (растёт при продажах) |
| `reputation_discount` | 0.7 … 1.3 | Скидка/наценка от репутации (-30% … +30%) |
| `event_multiplier` | 0.5 … 3.0 | Глобальные события |
| `route_multiplier` | 0.8 … 2.5 | Статус маршрута (заблокирован = ×2.5) |

### Пример расчёта

| Параметр | Значение |
|----------|----------|
| Ресурс | Мезий |
| Базовая цена | 10 CR |
| Спрос в Тертиусе | +0.6 (торговый hub, высокая активность) |
| Предложение | +0.2 (регулярные поставки НП) |
| Репутация игрока | +50 (Уважаемый) → discount = 0.85 |
| Событие | 1.0 (нет событий) |
| Маршрут | 1.0 (открыт) |
| **Итого** | 10 × (1 + 0.6 - 0.2) × 0.85 × 1.0 × 1.0 = **11.9 CR** |

### Обновление рынка (Tick-система)

| Параметр | Host (1-2 игрока) | Host (3-4 игрока) | Dedicated Server |
|----------|-------------------|-------------------|------------------|
| Частота тика | 5 мин | 3 мин | 2 мин |
| NPC-трейдеров | 4 | 6 | 8 |
| Затухание спроса/предложения | time-based (half-life 30 мин) | time-based | time-based |

**Каждый тик сервер:**
1. NPC-трейдеры перемещают товары между точками
2. Спрос/предложение затухают к базовым (time-based, half-life 30 мин — одинаково при любом multiplier)
3. Глобальные события обновляются
4. Статус маршрутов обновляется
5. Цены пересчитываются
6. Обновление отправляется клиентам (ClientRpc) только подписчикам зон (не всем)

### 4.5. Time-based Economy (v4.0)

В v4.0 частота тиков управляется **множителем** `marketTimeMultiplier` (см. <see cref="MarketTimeService"/>).

```
tickIntervalSeconds = baseIntervalSeconds / marketTimeMultiplier
```

| Множитель | Скорость тика (5 мин base) | Назначение |
|-----------|----------------------------|------------|
| 0.1x | 50 мин | Замедление для долгих сессий |
| 1.0x | 5 мин | Production default |
| 5.0x | 1 мин | Быстрая проверка баланса |
| 10.0x | 30 сек | Отладка / демо |
| 100.0x | 3 сек | «Мгновенный» режим |

**Time-based decay:** скорость затухания спроса/предложения описывается half-life в секундах (по умолчанию 1800 с = 30 мин), а не в процентах за тик. Это значит:

```
factor(t) = factor(t0) * exp(-k * (t - t0)),  k = ln(2) / halfLifeSeconds
```

При multiplier=10x тики случаются чаще, но `dt` каждого тика меньше — затухание идёт с той же скоростью по реальному времени. Ускорение multiplier означает «движение цен в единицу времени», а не «то же движение чаще».

**Опциональная подписка на `ServerWeatherController`:**
- `useWeatherFactor = true` (off по умолчанию) — multiplier умножается на weather-фактор (день 1.0, ночь 0.5, плавно между).
- Позволяет в будущем ввести «ночью рынки спят» без переписывания.

**RPC для отладки:** клиент может запросить `RequestSetTimeMultiplierRpc(multiplier)` (любой клиент; в будущем — admin-only). Сервер применяет и бродкастит обновлённый snapshot.

### Влияние игроков на рынок

```
# Игрок купил N единиц:
demand_factor += N × 0.02

# Игрок продал N единиц:
supply_factor += N × 0.02

# Максимальное накопление: ±1.5
# Максимальная цена: ×5 от базовой (cap)
# Затухание: time-based, half-life 30 мин (НЕ tick-based, как было в v1-v3)
```

---

## 5. Trading System

### 5.1 Торговля с NPC (торговые посты)

| Параметр | Описание |
|----------|----------|
| **Торговые посты** | Фиксированные точки в городах и на платформах |
| **Цены** | Динамические (спрос/предложение, tick-система) |
| **Ассортимент** | Зависит от локации и фракции |
| **Репутация** | Скидки при высокой репутации (до 30%) |
| **Налог** | 5% на каждую продажу (в пользу НП) |

### 5.2 Контрактная система

| Тип контракта | Источник | Описание | Обязательство |
|---------------|----------|----------|---------------|
| **НП-доставка** | НП (доска) | Взять товар → доставить | Договор (долг при провале) |
| **Под расписку** | НП (первые 2ч) | Получить товар бесплатно → доставить → 30% | Долговая расписка |
| **Мануфактура** | Агент мануфактуры | Эксклюзивная доставка | Контракт (штраф) |
| **Чёрный рынок** | Свободные торговцы | Контрабанда | Неформальный (риск) |
| **Военный** | Военный анклав | Доставка оружия/сопровождение | Контракт (высокий штраф) |

**Система «под расписку» (туториал-крючок):**
1. Игрок в первые 2 часа получает товар бесплатно от НП
2. Должен доставить в указанный город
3. При успехе: 30% стоимости + XP + репутация
4. При провале: **долг = стоимость × 1.5**, -30 репутации НП
5. Долг **не списывается** — НП присылает патрули при приближении

### 5.5 Multi-ship trading (v4.0)

В v4.0 игрок может владеть **несколькими кораблями** в одной зоне рынка. Торговля с конкретным кораблём требует выбора.

**Архитектура:**
- `MarketZone` (scene-placed компонент) имеет два радиуса:
  - `tradeRadius` (по умолчанию 5 м) — игрок должен быть в этой зоне, чтобы открыть рынок
  - `shipDockRadius` (по умолчанию 30 м) — корабли в этой зоне считаются «у причала» и доступны для Load/Unload
- SphereCollider (trigger) детектит NetworkPlayer и ShipController, попадающие в зону
- `MarketZoneRegistry` (server + client) — реестр всех зон по `locationId`
- На сервере: `MarketZone._shipsInZone` (HashSet<ulong>) — NetworkObjectId кораблей
- На клиенте: `MarketZoneRegistry.LocalPlayerZone` — для UI prompt'а

**UI (MarketWindow):**
- Если в зоне 1 корабль — `ship-selector-container` скрыт, Load/Unload идут в этот корабль
- Если в зоне 2+ корабля — виден `DropdownField` со списком «{name} ({shipClass})», выбор запоминается
- Имя корабля = `GameObject.name` префаба (например «Корабль #3» или «Primium Trader»)
- Переключение корабля НЕ обнуляет склад (только выбор целевого трюма)

**Валидация на сервере:**
- Любой RPC (buy/sell/load/unload) проверяет, что clientId игрока в `_playersInZone[locationId]`
- Для load/unload дополнительно: `shipNetworkObjectId` в `_shipsInZone[locationId]`
- Если проверка не прошла — `TradeResultCode.NotInZone` / `ShipNotInZone`, операция не выполняется

**Edge case — ни одного корабля в зоне:**
- Buy/Sell работают (товар попадает на склад игрока на этой локации, можно позже забрать кораблём)
- Load/Unload возвращают `ShipNotInZone` — UI показывает сообщение «Нет корабля у причала»

### 5.3 P2P торговля между игроками

| Фича | Описание | Этап |
|------|----------|------|
| UI обмена | Окно торговли между 2 игроками | Этап 3 |
| Предложение | Каждый игрок предлагает предметы/груз | Этап 3 |
| Подтверждение | Оба игрока подтверждают сделку | Этап 3 |
| Серверная валидация | Проверка на читы, логирование | Этап 3 |
| Налог | 5% налог на сделку | Этап 3 |

### 5.4 Чёрный рынок (контрабанда)

| Параметр | Описание |
|----------|----------|
| **Доступ** | Через вступление в Свободные торговцы (репутация +30) |
| **Локации** | Заброшенные платформы, секретные точки |
| **Товары** | Контрабандный мезий, краденые компоненты, поддельные коды СОЛ |
| **Цены** | ×1.5-3 к НП-ценам (риск) |
| **Без налога** | 0% налог (нелегально) |
| **Риск обнаружения** | 15% базовый + route_danger × stealth_mod |

---

## 6. Supply & Demand — Динамика рынка

### Факторы влияния

| Фактор | Влияние | Пример |
|--------|---------|--------|
| **Массовая скупка** | demand_factor ↑ → цены ×2-5 | Дефицит мезия в Примум |
| **Массовая продажа** | supply_factor ↑ → цены /2 | Переизбыток латекса в Тертиусе |
| **Блокада маршрута** | route_multiplier ↑ → цены ×2.5 | Военный конфликт |
| **NPC-конвой** | supply_factor ↑ → стабилизация | НП отправил мезий |
| **Мануфактура бум** | base_price ↓ → цены /2 | Аврора увеличила производство |
| **Эпидемия** | demand_factor МНП ↑ → ×3 | Вспышка болезни |

### Глобальные события

| Событие | Триггер | Эффект | Длительность |
|---------|---------|--------|-------------|
| **Дефицит мезия** | Массовая скупка + поломка | Цена мезия ×2-3 | 3-5 тиков |
| **Бум антигравия** | Мануфактура «Аврора» | Цена антигравия ×0.5 | 4-6 тиков |
| **Блокада маршрута** | Военный конфликт | Маршрут закрыт, цены ×2 | 2-4 тика |
| **Налоговая проверка** | НП усиливает контроль | Контрабанда ×2 риск | 1-2 тика |
| **Фестиваль** | Праздник | Продовольствие ×0.5 | 2-3 тика |
| **Эпидемия** | Вспышка | МНП ×3, все ×1.2 | 3-5 тиков |
| **Война гильдий** | Конфликт | Военные ×2, маршруты блоки | 5-10 тиков |

---

## 7. Cargo & Transport

### Грузовые слоты по классу корабля

| Параметр | Лёгкий | Средний | Тяжёлый I | Тяжёлый II |
|----------|--------|---------|-----------|-----------|
| **Грузовые слоты** | 4 | 10 | 20 | 30 |
| **Грузоподъёмность** | 100 кг | 500 кг | 2000 кг | 5000 кг |
| **Объём (м³)** | 3 | 12 | 40 | 80 |
| **Влияние на скорость** | -5% при 100% | -8% при 100% | -10% при 100% | -12% при 100% |

### Влияние груза на физику

```
speed_multiplier = 1.0 - (cargo_weight / max_capacity) × penalty_factor

# Перегруз: дополнительный штраф -20% за каждые 10% сверх лимита
```

### Типы грузов

| Тип | Особенность | Риск |
|-----|-------------|------|
| **Опасный** (мезий) | Протекает при столкновении (5% шанс) | Потеря груза, штраф |
| **Хрупкий** (двигатели, МНП) | Повреждается при столкновении (10%) | Снижение стоимости |
| **Контрабанда** | Обнаруживается патрулём | Штраф, бой, репутация |
| **Стандартный** | Без особенностей | Нет |

---

## 8. Мануфактуры — Независимые производители

### 4 Мануфактуры

| Мануфактура | Город | Товар | Бонус | Цвет |
|-------------|-------|-------|-------|------|
| **Аврора** | Примум | Антигравийные двигатели | Двигатели +20% эффективность | `#f0c27a` |
| **Титан** | Секунд | Военные модули, броня | Броня +15% защита | `#F44336` |
| **Гермес** | Тертиус | Текстиль, латекс, быт. товары | Латекс -30% цена | `#4CAF50` |
| **Прометей** | Квартус | МНП, научные разработки | МНП +25% эффект | `#9C27B0` |

### Отношения с НП

- НП хочет монополизировать → мануфактуры хотят свободы
- Мануфактуры платят налог НП за воздушное пространство
- Игрок может **выбрать сторону**: НП или мануфактуры
- Контрабанда компонентов мануфактур = максимальная прибыль, но -репутация НП

---

## 9. Economy Events — События рынка

События изменяют рынок, создавая возможности и риски.

| Событие | Эффект на цены | Длительность | Триггер |
|---------|---------------|-------------|---------|
| **Дефицит мезия** | Мезий ×2-3 | 3-5 тиков | Массовая скупка |
| **Бум антигравия** | Антигравий ×0.5 | 4-6 тиков | Производство ↑ |
| **Контрабандисты** | Дешёвый мезий на чёрном рынке | 2-3 тика | Событие подполья |
| **Война Гильдий** | Военные товары ×1.5 | 5-7 тиков | Конфликт фракций |
| **Фестиваль** | Продовольствие ×0.5 | 2-3 тика | Календарь |
| **Блокада** | Маршрут закрыт, цены ×2 | 2-4 тика | Военный конфликт |

---

## 10. Anti-Exploit Measures

### Защита от читов

| Мера | Описание |
|------|----------|
| **Серверная авторитетность** | Все транзакции, цены, расчёты — только на сервере |
| **Лимит транзакций** | Макс. 10 сделок в минуту |
| **Детекция дюпов** | Отслеживание дублирования предметов (уникальные ID) |
| **Контроль инфляции** | Сервер мониторит денежную массу |
| **Максимальная цена** | ×5 от базовой (нельзя накрутить бесконечно) |
| **Бан** | Блокировка за экономические читы |
| **Логирование** | Все аномальные транзакции логируются |

---

## 11. Техническая Архитектура (v5.0, post-cleanup)

> **История:** v4.0 описывала смешанную v1+v2 архитектуру (TradeMarketServer / TradeUI / ContractSystem / ContractBoardUI как основные компоненты). После C1+C4+C5 cleanup 2026-06-05 (-27913 LOC) **v1-слой полностью удалён**. Ниже — актуальная v2-архитектура (один источник истины — `MarketServer` + `ContractServer` на сервере, UI Toolkit на клиенте). Полные ссылки — `docs/Markets/ARCHITECTURE.md` (канонический), `docs/Markets/FLOW_TRADE.md` (флоу), `docs/Markets/INTEGRATION.md` (связи с остальным проектом).

### Слои

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  КЛИЕНТ (per-client MonoBehaviour + UI Toolkit)                            │
│  ┌────────────────────────────────────────────────────────────────────┐    │
│  │ MarketWindow (UIDocument, UI Toolkit)                              │    │
│  │   • читает MarketClientState.CurrentSnapshot                      │    │
│  │   • дергает MarketClientState.RequestXxx()                         │    │
│  │   • Esc = Hide, остальное = через callbacks UXML/USS               │    │
│  │   • ТАБЫ: РЫНОК / СКЛАД+ТРЮМ / КОНТРАКТЫ                         │    │
│  └────────────────────────────────────────────────────────────────────┘    │
│           ▲ OnSnapshotUpdated / OnTradeResult events                       │
│  ┌────────┴───────────────────────────────────────────────────────────┐    │
│  │ MarketClientState (singleton MonoBehaviour, DontDestroyOnLoad)    │    │
│  │   • держит последний MarketSnapshotDto + последний TradeResultDto  │    │
│  │   • forwardит NetworkPlayer.ReceiveMarketSnapshotTargetRpc/Rpc     │    │
│  │   • Per-ship cargo cache: CurrentShipCargos[shipId]                │    │
│  │   • Convenience API: RequestBuy/Sell/Load/Unload/Subscribe         │    │
│  └────────┬───────────────────────────────────────────────────────────┘    │
│           │ RPC: FindNetworkPlayer(clientId).ReceiveXxxTargetRpc(...)      │
│  ┌────────┴───────────────────────────────────────────────────────────┐    │
│  │ NetworkPlayer (NetworkBehaviour)                                   │    │
│  │   • [Rpc(SendTo.Owner)] ReceiveMarketSnapshotTargetRpc(...)        │    │
│  │   • [Rpc(SendTo.Owner)] ReceiveTradeResultTargetRpc(...)           │    │
│  │   • [Rpc(SendTo.Owner)] ReceiveContractSnapshotTargetRpc(...)      │    │
│  │   • [Rpc(SendTo.Owner)] ReceiveContractResultTargetRpc(...)        │    │
│  └────────┬───────────────────────────────────────────────────────────┘    │
│           │ NetworkObject.Netcode (server→owner transport)                 │
└───────────┼─────────────────────────────────────────────────────────────────┘
            │
            ▼  SendTo.Server
┌─────────────────────────────────────────────────────────────────────────────┐
│  СЕРВЕР (NetworkBehaviour + server-only state)                             │
│  ┌────────────────────────────────────────────────────────────────────┐    │
│  │ MarketServer (NetworkBehaviour, 1 шт, DontDestroyOnLoad)           │    │
│  │   • [Rpc(SendTo.Server, Owner)] RequestBuy/Sell/Load/UnloadRpc     │    │
│  │   • [Rpc(SendTo.Server, Owner)] SubscribeMarketRpc                 │    │
│  │   • [Rpc(SendTo.Server, Owner)] SetSelectedShipRpc                 │    │
│  │   • [Rpc(SendTo.Server, Owner)] SetMarketTimeMultiplierRpc         │    │
│  │   • Rate limit (maxOpsPerMinute, default 30)                       │    │
│  │   • Position validation через MarketZoneRegistry                   │    │
│  │   • Делегирует в TradeWorld.TryXxx()                                │    │
│  │   • SendSnapshotToClient() / BroadcastSnapshotsToAll()             │    │
│  │   • BuildItemPriceDtos / BuildWarehouseDtos / BuildTradeResultDto  │    │
│  └────────┬───────────────────────────────────────────────────────────┘    │
│  ┌────────┴───────────────────────────────────────────────────────────┐    │
│  │ ContractServer (NetworkBehaviour, 1 шт, в BootstrapScene)         │    │
│  │   • [Rpc(SendTo.Server, Owner)] RequestListRpc(locationId)         │    │
│  │   • [Rpc(SendTo.Server, Owner)] RequestAcceptRpc(contractId)       │    │
│  │   • [Rpc(SendTo.Server, Owner)] RequestCompleteRpc(contractId)     │    │
│  │   • [Rpc(SendTo.Server, Owner)] RequestFailRpc(contractId)         │    │
│  │   • [Rpc(SendTo.Server, Owner)] RequestAvailableContractsRpc(      │    │
│  │       fromLocationId, toLocationId) — for NPC inter-city travel    │    │
│  │   • Zone validation через MarketZoneRegistry (одна зона на локацию)│    │
│  │   • Делегирует в ContractWorld.TryXxx()                             │    │
│  │   • FixedUpdate: ContractWorld.Tick() — таймеры + auto-fail        │    │
│  └────────┬───────────────────────────────────────────────────────────┘    │
│           │                                                                 │
│  ┌────────┴───────────────────────────────────────────────────────────┐    │
│  │ TradeWorld (POCO singleton, server-only)                           │    │
│  │   • Markets: Dictionary<locationId, MarketState>                  │    │
│  │   • _npcTraders: List<NPCTrader> (ГосКонвой, Ветер, Караванщик...) │    │
│  │   • _activeEvents: List<MarketEvent> (Мезиевая лихорадка)          │    │
│  │   • _cargoCache: Dictionary<shipId, CargoData> (per-ship)          │    │
│  │   • Repository: IPlayerDataRepository (PlayerPrefsRepository/      │    │
│  │     ServerFileRepository P1)                                       │    │
│  │   • Resolver: TradeItemDefinitionResolver (DatabaseResolver)       │    │
│  │   • TryBuy / TrySell / TryLoadToShip / TryUnloadFromShip           │    │
│  │   • MarketTick(dtSeconds) — NPC, events, decay, regen              │    │
│  │   • GetOrLoadWarehouse / GetOrLoadCargo (in-memory cache)          │    │
│  └────────┬───────────────────────────────────────────────────────────┘    │
│  ┌────────┴───────────────────────────────────────────────────────────┐    │
│  │ ContractWorld (POCO singleton, server-only)                        │    │
│  │   • _availableContracts: Dictionary<contractId, ContractData>     │    │
│  │   • _playerContracts: Dictionary<playerId, List<contractId>>      │    │
│  │   • _playerDebts: Dictionary<playerId, ContractDebt>              │    │
│  │   • TryAccept / TryComplete / TryFail → ContractResult             │    │
│  │   • Tick(deltaTime) — таймеры, auto-fail                           │    │
│  │   • InitDistanceTable() — таблица расстояний (4 локации)          │    │
│  │   • GenerateContractsForLocation() — стандарт/срочный/расписка     │    │
│  └────────┬───────────────────────────────────────────────────────────┘    │
│           │ owns                                                            │
│  ┌────────┴───────────────────────────────────────────────────────────┐    │
│  │ POCO State: MarketState, MarketItemState, Warehouse, CargoData     │    │
│  │   • in-memory, не MonoBehaviour, не сериализуются в сцену          │    │
│  │   • создаются в TradeWorld.Initialize() / ContractWorld.Init()     │    │
│  └────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
│  ┌────────────────────────────────────────────────────────────────────┐    │
│  │ MarketTimeService (server-only MonoBehaviour)                      │    │
│  │   • Update() → tick timer → TradeWorld.MarketTick(dt)              │    │
│  │   • OnMarketTick event → MarketServer.BroadcastSnapshotsToAll()    │    │
│  │   • MarketTimeMultiplier (0.1x..100x, Range attribute)              │    │
│  └────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
│  ┌────────────────────────────────────────────────────────────────────┐    │
│  │ MarketZone (scene-placed MonoBehaviour, ×N в WorldScene_X_Z)      │    │
│  │   • SphereCollider (radius = tradeRadius) для player detection     │    │
│  │   • OverlapSphere (radius = shipDockRadius) для ship detection     │    │
│  │   • _playersInZone: HashSet<ulong> (server)                        │    │
│  │   • _shipsInZone: HashSet<ulong> (server)                          │    │
│  │   • Регистрирует себя в MarketZoneRegistry по locationId           │    │
│  │   • BuildNearbyShipsDtos() — для снапшота                          │    │
│  │   • Один источник истины для zone-validation (MarketServer +        │    │
│  │     ContractServer читают MarketZone.IsPlayerInZone)               │    │
│  └────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Цепочка RPC покупки (v2)

```
MarketWindow.OnBuyClicked
  → MarketClientState.RequestBuy(locationId, itemId, qty)
  → MarketServer.Instance.RequestBuyRpc(...)   [SendTo.Server, Owner]
     ├─ CheckRateLimit (sliding 60s window, 30 ops)
     ├─ ValidateInZone (MarketZoneRegistry.Get → zone.IsPlayerInZone)
     └─ TradeWorld.TryBuy(clientId, locationId, itemId, qty)
        ├─ warehouse.TryAdd (лимиты: weight, volume, types)
        ├─ Repository.TryModifyCredits(-totalCost)
        ├─ item.availableStock -= qty
        ├─ PriceFormula.ApplyBuy → item.RecalculatePrice (demand ↑)
        └─ Repository.SetWarehouse (persist)
     → BuildTradeResultDto → SendTradeResultToOwner(clientId, dto)
        → NetworkPlayer.ReceiveTradeResultTargetRpc(dto)  [SendTo.Owner]
           → MarketClientState.OnTradeResultReceived(dto)
              → MarketWindow.HandleTradeResult (зелёное сообщение, refresh credits)
     → SendSnapshotToClient (обновлённый снапшот с новыми ценами)
        → NetworkPlayer.ReceiveMarketSnapshotTargetRpc(snapshot)  [SendTo.Owner]
           → MarketClientState.OnSnapshotReceived(snapshot)
              → CurrentShipCargos[shipId] = snapshot.shipCargos[]
              → MarketWindow.HandleSnapshot (перерисовка списков)
```

### Цепочка RPC контракта (v2, через таб КОНТРАКТЫ)

```
MarketWindow (таб КОНТРАКТЫ) → OnAcceptClicked(contractId)
  → ContractClientState.RequestAccept(contractId)
  → ContractServer.Instance.RequestAcceptRpc(contractId)  [SendTo.Server, Owner]
     ├─ ValidateInZone (MarketZoneRegistry, single source of truth)
     ├─ ContractWorld.TryAccept(clientId, contractId)
     │    ├─ MaxActiveReached? TooMuchDebt? → ContractResult.Fail
     │    └─ _availableContracts[contractId].state = Active
     │         _playerContracts[clientId].Add(contractId)
     └─ BuildContractResultDto → SendContractResultToOwner
        → NetworkPlayer.ReceiveContractResultTargetRpc(dto)  [SendTo.Owner]
           → ContractClientState.OnTradeResultReceived(dto)
              → MarketWindow.HandleContractResult (state=Active, row зеленеет)
     + SendContractSnapshotToOwner (обновлённый список available+active)
        → NetworkPlayer.ReceiveContractSnapshotTargetRpc(snapshot)  [SendTo.Owner]
           → ContractClientState.OnSnapshotReceived(snapshot)
              → MarketWindow.HandleContractSnapshot (rebuild contracts list)
```

### Сетевая синхронизация (v2)

| Данные | Частота | Метод | Направление |
|--------|---------|-------|-------------|
| Snapshot рынка (цены+сток+склад+корабли+контракты) | Каждый тик (5 мин × 1/multiplier) | `MarketServer.SendSnapshotToClient` → `ReceiveMarketSnapshotTargetRpc` | Server → Owner |
| Trade result (BUY/SELL/LOAD/UNLOAD) | По событию | `MarketServer.SendTradeResultToOwner` → `ReceiveTradeResultTargetRpc` | Server → Owner |
| Запрос на подписку | При входе в зону (E) | `MarketServer.SubscribeMarketRpc` | Client → Server |
| Запрос на действие (BUY/SELL/LOAD/UNLOAD) | По действию | `MarketServer.RequestBuyRpc`/etc. | Client → Server |
| SetSelectedShip | По смене корабля в dropdown | `MarketServer.SetSelectedShipRpc` | Client → Server |
| Contract snapshot | По подписке + по accept/complete/fail | `ContractServer.SendContractSnapshotToOwner` → `ReceiveContractSnapshotTargetRpc` | Server → Owner |
| Contract result | По accept/complete/fail | `ContractServer.SendContractResultToOwner` → `ReceiveContractResultTargetRpc` | Server → Owner |
| Запрос на действие с контрактом | По клику ВЗЯТЬ/СДАТЬ/ПРОВАЛИТЬ | `ContractServer.RequestAcceptRpc`/etc. | Client → Server |
| SetMarketTimeMultiplier | По нажатию в dev-консоли | `MarketServer.SetMarketTimeMultiplierRpc` | Client → Server |

### ScriptableObject (v2)

```
MarketConfig (CreateAssetMenu, read-only) — ОДИН на локацию (MarketConfig_Primium.asset, ...)
├── locationId: string — "primium"/"secundus"/"tertius"/"quartus"
├── displayName: string — "Примум (Столица)" / и т.д.
├── items: List<MarketItemConfig> — статические параметры товаров на этой локации
│   └── MarketItemConfig: TradeItemDefinition, allowBuy, allowSell, basePriceOverride,
│                          stockRegenPerTick, factionRestriction
├── initialEvents: List<MarketEventConfig> — стартовые события локации
└── (НЕ mutable! runtime-state в TradeWorld._markets[locationId] = MarketState)

TradeItemDefinition (CreateAssetMenu) — ОДИН на товар (TradeItem_Mesium_v01.asset, ...)
├── itemId: string — уникальный ID ("mesium_canister_v01")
├── displayName: string — "Мезий (канистра)"
├── icon: Sprite
├── basePrice: float
├── weight: float — кг за единицу
├── volume: float — м³ за единицу
├── slots: int — слотов за единицу
├── isDangerous: bool — мезий (протечка при столкновении)
├── isFragile: bool — двигатели, МНП (повреждение)
├── isContraband: bool — нелегальный товар
└── requiredFaction: Faction — кто может продавать (None = все)

TradeDatabase (CreateAssetMenu) — единственный asset (TradeItemDatabase.asset)
├── allItems: List<TradeItemDefinition>
├── GetItemById(string id)
├── GetItemByDisplayName(string name)
├── GetItemsByFaction(Faction f)
└── GetContrabandItems()
```

> **Удалено в C1 cleanup:** `LocationMarket` (ScriptableObject с mutable state), `MarketItem` (Serializable, внутри LocationMarket). Заменены на read-only `MarketConfig` (SO) + `MarketItemConfig` (внутри MarketConfig.items[]) + runtime `MarketState`/`MarketItemState` (POCO в `TradeWorld`).

### Хранение данных (v2)

```
# IPlayerDataRepository (interface) — два имплемента:
#   - PlayerPrefsRepository (default, host) — host-only testing
#   - ServerFileRepository (P1, dedicated) — JSON в Application.persistentDataPath/ServerData/

PD2_Credits_{clientId}                 — кредиты (ОБЩИЕ для всех локаций)
PD2_Warehouse_{clientId}_{locationId}  — склад (привязан к локации)
PD2_Cargo_{shipNetworkObjectId}        — груз корабля (per-ship, persistent)

# TradeWorld._cargoCache[shipId] — in-memory cache, persistent в PlayerPrefs/JSON
# Repository.GetCredits/AddCredits/SetWarehouse/SetCargo — единый API
```

---

## 12. Formulas

| Формула | Описание |
|---------|----------|
| `price = base × (1 + demand - supply) × rep × event × route` | Цена товара |
| `profit = sell_price × 0.8 - buy_price - tax` | Прибыль от торговли (sell = 80% цены, NPC-маржа) |
| `tax = sell_price × 0.05` | Налог на сделку (5%, v2+) |
| `contract_reward = base_price × quantity × 0.3 × distance × rep_bonus` | Награда за контракт |
| `debt = cargo_value × 1.5` | Долг при провале «под расписку» |
| `speed_penalty = 1.0 - (cargo_weight / max_capacity) × penalty_factor` | Влияние груза на скорость |
| `demand_change = quantity × 0.02` | Влияние покупки на спрос |
| `supply_change = quantity × 0.02` | Влияние продажи на предложение |
| `decay(t) = factor × exp(-ln(2) × dt / halfLife)` | Time-based затухание (v4.0) |
| `tickInterval = baseInterval / marketTimeMultiplier` | Частота тика (v4.0) |
| `clamp price ∈ [base × 0.5, base × 5.0]` | Защита от runaway-цен |

---

## 13. Tuning Knobs

| Параметр | Мин | Макс | Текущее | Влияние |
|----------|-----|------|---------|---------|
| `base_mezium_price` | 5 | 20 | 10 | Базовая цена мезия |
| `base_antigrav_price` | 25 | 100 | 50 | Базовая цена антигравия |
| `trade_tax` | 0.01 | 0.15 | 0.05 | Налог на торговлю |
| `demand_change_per_unit` | 0.005 | 0.05 | 0.02 | Влияние покупки на спрос |
| `max_price_multiplier` | 3.0 | 10.0 | 5.0 | Макс. множитель цены |
| `debt_multiplier` | 1.0 | 3.0 | 1.5 | Множитель долга |
| `contraband_detect_base` | 0.05 | 0.30 | 0.15 | Базовый шанс обнаружения |
| `base_tick_interval_seconds` | 30 | 3600 | 300 | Базовый интервал тика (сек) |
| `market_time_multiplier` | 0.1 | 100 | 1.0 | Множитель скорости (v4.0) |
| `demand_decay_half_life_seconds` | 60 | 86400 | 1800 | Half-life спроса (v4.0) |
| `use_weather_factor` | bool | — | false | Включить time-of-day модуляцию (v4.0) |
| `npc_trader_count` | 2 | 20 | 8 | Количество NPC-трейдеров |
| `max_ops_per_minute` | 0 | 200 | 30 | Rate limit (0 = off) |

---

## 14. Acceptance Criteria

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 1 | Кредиты отображаются в UI | Проверить HUD | ✅ |
| 2 | Рынок каждой локации уникален | Сравнить цены в 2+ городах | ✅ |
| 3 | NPC-торговец открывает магазин | Взаимодействие с NPC | ✅ |
| 4 | Динамические цены работают | Цены меняются после тика | ✅ |
| 5 | Покупка/продажа влияет на цены | Купить 10 ед. → цена выросла | ✅ |
| 6 | Контракт НП работает | Взять → доставить → получить | ✅ |
| 7 | Система «под расписку» работает | Не доставить → долг | ✅ |
| 8 | Контрабанда обнаруживается | Провезти → штраф | 🔴 (Этап 5) |
| 9 | Репутация влияет на цены | Сравнить при разной репутации | 🔴 (Этап 5) |
| 10 | Груз влияет на скорость | Загрузить → проверить скорость | ✅ |
| 11 | P2P торговля работает | Обмен между 2 игроками | 🔴 (Этап 5) |
| 12 | Серверная валидация | Попытка чита → отклонение | ✅ |
| 13 | События рынка работают | Создать событие → цены изменились | ✅ |
| 14 | Маршрут блокируется | Событие → маршрут закрыт | 🔴 (Этап 5) |
| 15 | (v4.0) Time multiplier ускоряет/замедляет рынок | Inspector → MarketTimeService → multiplier 10x → цены реально двигаются за секунды | 🟡 (v4.0) |
| 16 | (v4.0) При multiplier=10x цены не скатываются в 0 | Time-based decay (half-life 30 мин) не зависит от частоты тиков | 🟡 (v4.0) |
| 17 | (v4.0) Multi-ship: 2 корабля в зоне → UI dropdown | Поставить 2 ShipController в trigger MarketZone | 🟡 (v4.0) |
| 18 | (v4.0) Multi-ship: 1 корабль в зоне → dropdown скрыт | Убрать второй корабль | 🟡 (v4.0) |
| 19 | (v4.0) Игрок вне MarketZone → RPC отклоняется с NotInZone | Сесть в корабль, уплыть далеко, попробовать купить | 🟡 (v4.0) |
| 20 | (v4.0) Корабль вне MarketZone → Load/Unload отклоняется с ShipNotInZone | Уплыть корабль далеко, попробовать погрузить | 🟡 (v4.0) |
| 21 | **ItemRegistry** (single source of truth для item IDs) | см. §X ниже | 🟢 DONE (M14, 2026-06-09) |
| 22 | **Contract → Quest bridge** (`ContractMetaBridge`) | см. §X ниже | 🟢 DONE (T-X5+T-Q15, 2026-06-08) |
| 23 | **Resources Exchanger** (Pack/Unpack inventory↔warehouse) | см. §X.5 | 🟢 DONE (T-E01–T-E05, 2026-06-11) |

---

## X. Реализация в коде (дополнения 2026-06-08..09)

> **Секция добавлена Mavis 2026-06-10.** Дизайн-контент (валютная система, ресурсы, формулы pricing, динамическая экономика) остаётся в зоне economy-designer'а. Здесь — **только статус реализации** ItemRegistry + Contract→Quest bridge.

### X.1 ItemRegistry (M14, 2026-06-09)

**Проблема:** `InventoryWorld.GetOrRegisterItemId()` и `QuestWorld.ResolveItemId()` использовали **независимые** нумерации, которые **случайно** совпадали (alphabetical order из `Resources.LoadAll`). При добавлении item'а вне `Resources/Items/` — id **молча** разъедутся, квесты перестанут работать.

**Решение:** **`ItemRegistry`** (singleton SO) — single source of truth для `id ↔ ItemData` mapping. 32 items, id 1-32. Деталь — `docs/NPC_quests/old_session_log/M14_DESIGN_NOTE.md` + см. GDD_11 §X.2.

### X.2 Contract → Quest bridge (T-X5 + T-Q15, 2026-06-08)

**Проблема:** Контракты и квесты — **две независимые** системы. Контракт может быть "доставить cargo из A в B", квест — "принести 3 руды NPC". Нет моста.

**Решение:** **`ContractMetaBridge`** (server-side singleton, scene-placed в `BootstrapScene`, DontDestroyOnLoad).

**Поток:**
1. `ContractServer` публикует 3 events в `WorldEventBus`:
   - `ContractAcceptedEvent { contractId, playerId, fromNpcId, timestamp }`
   - `ContractCompletedEvent { contractId, playerId, timestamp, wasReceipt }`
   - `ContractFailedEvent { contractId, playerId, timestamp, debtIncurred }`
2. `ContractMetaBridge` подписан → `QuestWorld.MarkContractAccepted/MarkContractCompleted/Failed`
3. `QuestTriggerService.Evaluate($"ContractCompleted:{contractId}")` — квесты могут следить за состоянием контрактов через `HasContractAccepted/HasContractCompleted` objectives

**Деталь:** `docs/NPC_quests/old_session_log/T-Q15_DESIGN_NOTE.md`.

**Что НЕ покрыто:**
- ⏳ **TradeItemDefinition → FactionId migration** (T-X2, DEFERRED) — 2 разных enum'а (8 manufacturer factions vs 12 lore guilds), пересекаются только в `FreeTraders`. Дизайн дискуссия нужна.

### X.3 Persistence pattern (M8, T-Q18, 2026-06-08)

**Референс для market state:** в NPC+Quests v2 сделан `JsonQuestStateRepository` (T-Q18) — atomic JSON в `Application.persistentDataPath`, immediate save на каждый state change, единый JSON на игрока. Можно скопировать паттерн для market state (demand/supply factors, active events, tick-timer) — см. `docs/MMO_Development_Plan.md` §3.4 п.4.

### X.4 Где смотреть актуальный статус

- **`docs/NPC_quests/08_ROADMAP.md`** — главный roadmap (50+ тикетов, M1–M19)
- **`docs/NPC_quests/old_session_log/M14_DESIGN_NOTE.md`** — ItemRegistry
- **`docs/NPC_quests/old_session_log/T-Q15_DESIGN_NOTE.md`** — ContractMetaBridge
- **`docs/Markets/Resources_exchanger/`** — Resources Exchanger (T-E01–T-E05)
- **`docs/Markets/`** — детали trade v2 (C1 cleanup, market state)
- **`docs/MMO_Development_Plan.md`** §3.2, §3.3, §3.4 — общий план

### X.5 Resources Exchanger (T-E01–T-E05, 2026-06-11)

**Зачем:** Две независимые системы предметов — pickable (инвентарь, int id, 1 кг) и boxed (склад, string id, 100 кг). Моста между ними не было. Crafting не может использовать склад, mining не может отправить добычу на склад.

**Решение:** Обменник-упаковщик — 4-я вкладка «Обменник» в MarketWindow. Левая панель = pickable из инвентаря, правая = boxed со склада. Pack: 100 pickable → 1 box на склад. Unpack: 1 box → 100 pickable в инвентарь.

**Архитектура (Hybrid D, см. анализ в docs/Markets/Resources_exchanger/01_ANALYSIS.md):**

```
ExchangeRateConfig (SO) → ResourceExchangeResolver (lookup) → ExchangeWorld (POCO, rollback) → ExchangeServer (RPC) → ExchangeClientState + MarketWindow tab
```

**Ключевые решения:**
- **Zero-touch** — ни одна строка в InventoryWorld, TradeWorld, Crafting, Mining, Quests не менялась
- **Config-driven** — новая пара = запись в `DefaultExchangeRate.asset`
- **MAX_SLOTS = 1000** — временно (нет stacked inventory; каждый id = 1 запись)
- **PushPlayerSnapshot** — после операции зовём и InventoryServer, и MarketServer (обновить и инвентарь, и склад)

**4 базовых курса:** IronOre, CopperOre, Wood, Antigrav — 100:1.

**Детали:** `docs/Markets/Resources_exchanger/02_IMPLEMENTATION.md` + `03_FIXES_HISTORY.md`.

---

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [GDD_23_Faction_Reputation.md](GDD_23_Faction_Reputation.md) | [GDD_25_Trade_Routes.md](GDD_25_Trade_Routes.md) | [GDD_10_Ship_System.md](GDD_10_Ship_System.md) | [`docs/NPC_quests/08_ROADMAP.md`](../NPC_quests/08_ROADMAP.md) | [`docs/Markets/`](../Markets/)
