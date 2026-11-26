# Магазины квестовых и именных NPC — Полный гайд и спецификация (`T-MERCH01`)

> **Статус:** реализовано и активно.
> **Область:** торговля pickable-предметами (`ItemData` из `ItemRegistry`) через стандартный диалог `E` у именных NPC.
> **Отличие от биржи портов (`MarketWindow`):** биржа портов торгует корабельными ящиками/грузами (`TradeItemDefinition` для трюма и склада станции), тогда как магазин NPC торгует личными предметами персонажа (`ItemData` — ресурсы, еда, медикаменты, оружие, одежда, модули, техника, антигравий, мезий), которые лежат в рюкзаке игрока (`InventoryWorld`).

---

## 1. Полный чек-лист возможностей магазина NPC (что учтено в системе)

Ниже собран полный перечень механик, проверок и UX-деталей, необходимых для надёжной работы магазина в MMO-песочнице:

### 1.1. Взаимодействие и окно диалога (`DialogWindow`)
1. **Единая точка входа по клавише `E`**:
   - Игрок подходит к NPC и нажимает `E` — запускается обычный диалог (`RequestTalkToNpcRpc`).
   - Если NPC не является торговцем — отображается классическое центрированное окно диалога (`800px`).
   - Если NPC является торговцем — окно диалога автоматически переключается в широкий двухколоночный режим:
     - **Левая колонка (500px)**: стандартный диалог (имя NPC, бейдж отношения `❤`, текст реплики с typewriter, кнопки ответов и квестов). Игрок может одновременно вести квестовый диалог и торговать.
     - **Правая колонка (620px)**: интерфейс магазина (`Merchant Panel`).
2. **Динамическая синхронизация при открытии и сделках**:
   - При открытии диалога с торговцем сервер сразу присылает `MerchantShopSnapshotDto` с актуальным ассортиментом, остатками (`stock`), балансом кредитов игрока, заполненностью рюкзака и точными (с десятыми долями) значениями отношения NPC и репутации фракции.

### 1.2. Настройка со стороны Редактора (Data-Driven + Test Mode)
3. **Личный прайс-лист (`NpcMerchantTable` ScriptableObject)**:
   - Создаётся через `Create > ProjectC > Quests > NPC Merchant Table`.
   - Привязывается в поле `merchantTable` на `NpcDefinition`.
   - Для каждой позиции (`NpcMerchantItemEntry`) задаются:
     - `item` (`ItemData` — drag & drop любого pickable-предмета проекта);
     - `customBuyPrice` (цена покупки у NPC; если `0` — вычисляется автоматически по формуле ценности предмета);
     - `customSellPrice` (цена скупки у игрока; если `0` — `buyPrice × sellBackPriceMultiplier`);
     - `stock` (`-1` = бесконечный запас; `> 0` = ограниченный запас на сервере);
     - `minNpcAttitude` (порог личного отношения `❤`, ниже которого товар виден в списке, но заблокирован с подсказкой причины);
     - `minFactionReputation` (порог репутации фракции NPC);
     - `requiredQuest` (опциональный квест, который нужно завершить для разблокировки товара).
4. **Режим «Продаёт ВСЁ» (`sellAllItemsForTesting`) + Warning в Инспекторе**:
   - В `NpcDefinition` есть флаг `sellAllItemsForTesting` и базовая цена `testModeDefaultPrice`.
   - При включении флага в кастомном инспекторе `NpcDefinitionEditor` рисуется жёлтый/оранжевый блок предупреждения:
     `⚠️ WARNING FOR TESTS ONLY: NPC продаёт все pickable-предметы из ItemRegistry! Не оставлять включённым в продакшене.`
   - Уникальные корабельные ключи (`ItemType.Key`) автоматически исключаются из тестовой выдачи «всё», чтобы не создавать невалидные слоты без `KeyRodInstance`.
5. **Автоматическое ценообразование (`MerchantPriceResolver`)**:
   - В базовый `ItemData` добавлено поле `basePrice` (по умолчанию `0`, не ломает существующие ~500 ассетов).
   - Если ни в таблице торговца, ни в `ItemData` цена не задана явно (`<= 0`), резолвер вычисляет разумную цену по классу предмета (`WeaponItemData` — по тиру, базовому урону и кубику; `ClothingItemData` — по тиру, броне и стат-бонусам; `ModuleItemData` — по тиру; остальные — по категории `ItemType` и весу).

### 1.3. Покупка и Продажа (Двусторонняя торговля)
6. **Два режима в UI магазина**:
   - Вкладка **«Купить»** — товары торговца (из его таблицы или все предметы `ItemRegistry`).
   - Вкладка **«Продать»** — предметы из рюкзака игрока, которые этот торговец готов купить (настраивается флагом `allowPlayerSellToNpc` и маской разрешённых категорий). Ключи кораблей (`ItemType.Key`) продать торговцу нельзя (защита от потери корабля).
7. **Фильтрация и поиск**:
   - Быстрые кнопки фильтра по `ItemType`: `Все`, `Ресурсы`, `Оборуд.`, `Еда`, `Топливо`, `Антиграв`, `Мезий`, `Медицина`, `Техника`.
   - Текстовое поле поиска по названию предмета.
8. **Инспектор наведения (Hover / Selection Details) и выбор количества**:
   - При наведении мыши на строку товара (или клике по ней) в нижней карточке деталей показываются:
     - иконка/бейдж категории, полное название, тип предмета и вес (`кг`);
     - боевые или экипировочные характеристики (для оружия: класс, кубик урона, базовый урон, крит, дальность, тип урона; для одежды: слот, тир, защита, бонусы STR/DEX/INT; для имплантов: слот, тип, тир, бонусы);
     - художественное описание (`ItemData.description`);
     - сколько таких предметов уже лежит в рюкзаке игрока (`В инвентаре: N шт.`).
   - Выбор количества для сделки: кнопки `-`, `+`, пресеты `x1`, `x5`, `x10`, `MAX` (автоматически считает максимум по кредитам игрока, свободным слотам рюкзака и остатку у NPC).
   - Предпросмотр суммы сделки в `CR` и точного прироста репутации (`+0.X ❤ / ⚑`).

### 1.4. Серверная авторитетность, анти-чит и репутация
9. **Строго серверная валидация каждой транзакции**:
   - Проверка rate-limit (`QuestServer.CheckRateLimit`).
   - Проверка дистанции от игрока до `NpcController` (`IsWithinDistance`) и активной сессии диалога.
   - Проверка наличия товара в списке торговца, выполнения требований `minNpcAttitude` / `minFactionReputation` / `requiredQuest` и наличия остатка (`stock`).
   - Проверка свободного места в рюкзаке игрока (`InventoryWorld`: `TotalCount + quantity <= MaxSlots`) **до** списания кредитов.
   - Атомарное списание/начисление кредитов через `TradeWorld.Instance.Repository` (с безопасным серверным fallback-кошельком в памяти, если `TradeWorld` не поднят в изолированном тесте) + синхронизация `InventoryServer.PushSnapshot` и `ContractServer.PushPlayerSnapshot`.
10. **Серверный расчёт репутации `+0.1 за каждые 100 CR`**:
    - Формула: `repGain = (creditsSpentByPlayer / 100f) * 0.1f = creditsSpentByPlayer * 0.001f`.
    - Начисляется одновременно:
      1. В личное отношение с этим NPC (`NpcAttitude` по `npcId`).
      2. В репутацию фракции этого NPC (`FactionReputation` по `npc.EffectiveFactionWireId`, если у NPC задана фракция).
    - Так как базовые таблицы `_reputation` и `_npcAttitude` в `QuestWorld` целочисленные (`int`), в `QuestWorld` заведены серверные дробные аккумуляторы `_npcTradeRepProgress[(clientId, npcId)]` и `_factionTradeRepProgress[(clientId, factionWireId)]`.
    - Когда накопленный прогресс достигает `>= 1.0`, целая часть переносится в основную таблицу через `ModifyNpcAttitude` / `ModifyReputation` (с отправкой всех событий `WorldEventBus`, проверкой квестовых триггеров и сохранением), а дробный остаток сохраняется в `QuestSaveData` (`quest_state_<clientId>.json`).
    - В UI диалога и магазина отображается точное значение с десятыми долями (`❤ +1.4`, `⚑ +3.4`), чтобы игрок видел мгновенный отклик от каждой покупки.

---

## 2. Архитектура файлов

| Файл | Роль |
|---|---|
| `Assets/_Project/Quests/Merchants/NpcMerchantTable.cs` | `ScriptableObject` личного магазина NPC + `NpcMerchantItemEntry` + `MerchantPriceResolver`. |
| `Assets/_Project/Quests/Npcs/NpcDefinition.cs` | Поля `merchantTable`, `sellAllItemsForTesting`, `testModeDefaultPrice` и helper `IsMerchant`. |
| `Assets/_Project/Quests/Editor/NpcDefinitionEditor.cs` | Блок `🛒 Merchant / Trade` в инспекторе NPC, кнопка создания таблицы, `WARNING FOR TESTS ONLY`. |
| `Assets/_Project/Quests/Editor/NpcMerchantTableEditor.cs` | Кастомный инспектор `NpcMerchantTable` с быстрым поиском/добавлением предметов и превью авто-цен. |
| `Assets/_Project/Quests/Dto/MerchantDtos.cs` | `MerchantShopSnapshotDto`, `MerchantItemDto`, `MerchantTradeResultDto` (`INetworkSerializable`). |
| `Assets/_Project/Quests/Core/QuestWorld.cs` | Серверный сток торговцев, дробные аккумуляторы репутации (`0.1 / 100 CR`), методы `TryMerchantBuy` / `TryMerchantSell`. |
| `Assets/_Project/Quests/Persistence/QuestSaveData.cs` | Сериализация дробного прогресса торговли (`npcTradeRepRemainders`, `factionTradeRepRemainders`) в JSON. |
| `Assets/_Project/Quests/Network/QuestServer.cs` | RPC `RequestMerchantShopRpc`, `RequestMerchantBuyRpc`, `RequestMerchantSellRpc` и пуш снапшотов. |
| `Assets/_Project/Quests/Client/QuestClientState.cs` | Клиентский кэш `CurrentMerchantShop` и события `OnMerchantShopReceived`, `OnMerchantTradeResultReceived`. |
| `Assets/_Project/Quests/UI/DialogWindow.cs` (+ `.uxml`, `.uss`) | Правый блок магазина в диалоговом окне, фильтрация, тултип при наведении, контрол количества и сделка. |

---

## 3. Как настроить торговца в Unity Editor (Пошагово)

### Вариант А — Персональный прайс-лист (Production)
1. Выделите `NpcDefinition` нужного NPC (например, в `Assets/_Project/Quests/Data/Npcs/...`).
2. В блоке **🛠 Services** включите флаг `Trade` (либо просто назначьте таблицу в блоке **🛒 Merchant / Trade** — `IsMerchant` определит торговца автоматически).
3. В блоке **🛒 Merchant / Trade** нажмите **`+ Create Merchant Table`** (или создайте вручную через `Create > ProjectC > Quests > NPC Merchant Table` и перетащите в поле `Merchant Table`).
4. В открывшейся `NpcMerchantTable`:
   - Добавьте нужные `ItemData` в список `Items`.
   - Укажите `Custom Buy Price` (или оставьте `0` для автоматического расчёта цены).
   - Укажите `Stock` (`-1` для бесконечного товара или число `> 0` для лимитированного).
   - При необходимости задайте `Min Npc Attitude`, `Min Faction Reputation` или `Required Quest`.

### Вариант Б — Продаёт ВСЁ (Только для тестов!)
1. Выделите `NpcDefinition` нужного NPC.
2. В блоке **🛒 Merchant / Trade** поставьте галочку **`Sell ALL Items (Test Only)`**.
3. В инспекторе появится предупреждение `⚠️ WARNING FOR TESTS ONLY`.
4. Теперь при нажатии `E` на этого NPC в правом блоке диалога будут доступны все pickable-предметы из `ItemRegistry` с фильтрацией по категориям и поиском.
