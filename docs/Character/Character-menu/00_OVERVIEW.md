# Character Menu — Обзор

**Дата:** 2026-06-05
**Автор:** Mavis (Mavis)
**Статус:** 📐 Дизайн-док / план реализации
**Scope:** новое окно `CharacterWindow` (UI Toolkit) с 5 вкладками, адаптивное, готовое к будущим табам

---

## 1. Зачем

Сейчас у игрока **нет единого "личного кабинета"**. Связанные с персонажем сущности разбросаны:

- **Колесо инвентаря** (InventoryUI / GTA-стиль) — открывается Tab, рисуется IMGUI, локальный кэш
- **Контракты** — 3-й таб в `MarketWindow` (дёргается через E у рынка)
- **Корабль** — нет UI для обзора (только HUD/сцены)
- **Репутация** — нет UI вообще (GDD-23 описана, реализации 0)
- **Квесты** — нет UI (GDD-21 описана, реализации 0)
- **Одежда, гаджеты** — только в GDD упоминаются, реализации 0

**Проблема:** если завтра понадобится "экран персонажа" — мы будем лепить его в лучшем случае параллельным UIDocument (повторяя 4 FIX'а MarketWindow), в худшем — отдельным GO-окном с pickingMode-багом.

**Решение:** новое окно `CharacterWindow` по образцу `MarketWindow` (UI Toolkit, singleton, 4 FIX'а сразу бесплатно), внутри — **5 табов + слоты под будущие**. Каждый таб — отдельный контроллер-секция в UXML, переключение через `SwitchTab(...)` — паттерн полностью копирует C2-этап (CONTRACTS_AS_MARKET_TAB_REFACTOR.md).

## 2. Архитектурное правило (от Mavis для subagent'ов)

```
CharacterWindow (singular) ≠ CharacterMenu (singular) — это ОДНО ОКНО с табами.
MarketWindow (singular) ≠ TradeBoard / ContractBoard — то же самое.

НЕ создавать CharacterMenuWindow, CharacterInventoryWindow,
CharacterReputationWindow, CharacterContractsWindow, CharacterShipWindow —
всё идёт ВНУТРЬ CharacterWindow как <VisualElement name="tab-*-section" />.
```

Это правило = **прямое продолжение** `project-c-ui-as-tab` skill, теперь на уровень выше (на новое окно целиком).

## 3. Окна и табы — финальная карта

| Окно | Таб | Серверная сущность | Клиентская проекция | Статус |
|------|-----|--------------------|----------------------|--------|
| **MarketWindow** (3 таба) | Рынок | `MarketServer` | `MarketClientState` | ✅ v2 готов |
| | Склад / Трюм | `MarketServer` | `MarketClientState` | ✅ v2 готов |
| | Контракты | `ContractServer` | `ContractClientState` | ✅ v2 готов (C2-этап) |
| **CharacterWindow** (5+ табов) | Персонаж | (none yet) | `CharacterStatsClientState` (новый) | 🆕 MVP — заглушка + хард-стат |
| | Корабль | (none yet) | (read from `NetworkPlayer` local) | 🆕 MVP — заглушка + локальные данные |
| | Репутация | (нет сервера) | (read from local placeholder) | 🆕 MVP — заглушка + план в GDD-23 |
| | Контракты / Квесты | `ContractServer` | `ContractClientState` (re-use!) | 🆕 — расширенный, как в MarketWindow + фильтры |
| | Инвентарь | (none, local) | `NetworkPlayer.Inventory` (re-use!) | 🆕 — расширенный, как колесо + фильтры |

**Дополнительные вкладки (на будущее, добавляются за 1 сессию каждая):**
- Одежда (cosmetic)
- Гаджеты (abilities / implants)
- Достижения
- Настройки

UI Toolkit-структура окна спроектирована так, что **добавление нового таба = 1 новая секция в UXML + 1 case в `SwitchTab` + (опционально) подписка на новый singleton** — без рефакторинга header/footer/tabs.

## 4. Что это НЕ

- ❌ **Не** замена `InventoryUI` (GTA-колесо по Tab) — оба живут, оба читают **тот же** `NetworkPlayer.Inventory` (single source of truth). Колесо остаётся быстрым доступом; вкладка "Инвентарь" в CharacterWindow — детальный список с фильтрами.
- ❌ **Не** замена `MarketWindow` (таб Контракты) — обе подписки идут на **тот же** `ContractClientState`. В MarketWindow таб "Контракты" остаётся (потому что открывается при подходе к NPC-агенту). CharacterWindow даёт глобальный обзор + историю + фильтры.
- ❌ **Не** система репутации / квестов / одежды — это **только UI-оболочка** под будущее. Серверных RPC пока нет — табы показывают placeholder-данные + plan-комментарии.

## 5. Пре-реквизиты (что УЖЕ есть)

| Артефакт | Файл | Что даёт |
|----------|------|----------|
| `MarketWindow` | `Assets/_Project/Trade/Scripts/Client/MarketWindow.cs` + `.uxml` + `.uss` | Эталон UI Toolkit-окна, 4 FIX'а, `SwitchTab` паттерн, `MakeXxxRow`/`BindXxxRow` |
| `ContractClientState` | `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs` | Singleton-проекция контрактов с `RequestList/Accept/Complete/Fail`, события `OnSnapshotUpdated` / `OnContractResult` |
| `ContractDto` / `ContractSnapshotDto` | `Assets/_Project/Trade/Scripts/Dto/ContractDto.cs` и др. | Сетевые DTO, готовы к ре-use из CharacterWindow |
| `Inventory` (MonoBehaviour) | `Assets/_Project/Scripts/Core/Inventory.cs` | Локальный инвентарь игрока, API `GetItemsByType/GetCountByType/HasItemsInType` |
| `NetworkPlayer.Inventory` | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (line 52, 263) | Прямой доступ из Owner: `NetworkPlayer._inventory` |
| `ItemType` / `ItemTypeNames` | `Assets/_Project/Scripts/Core/ItemType.cs` / `ItemTypeNames.cs` | 8 типов (Resources, Equipment, Food, Fuel, Antigrav, Meziy, Medical, Tech), локализованные имена |
| `MarketPanelSettings` | `Assets/_Project/Trade/Resources/UI/MarketPanelSettings.asset` | Готовый PanelSettings — CharacterWindow может реюзать или создать свой |

## 6. Правила (от AGENTS.md / project rules)

1. **Никаких `.meta`/`.asmdef`** — Unity создаст сам
2. **Single source of truth:** инвентарь читаем ТОЛЬКО из `NetworkPlayer.Inventory`; контракты — ТОЛЬКО из `ContractClientState`; репутация/корабль — из локальных плейсхолдеров
3. **Билингва комментарии** (RU primary), ticket-ID'ы не теряем
4. **`[Header("...")]` для группировки** инспектор-полей
5. **Безопасный GameObject на сцене:** `CharacterWindow` + `UIDocument` в `BootstrapScene` (аналог `[MarketWindow]`)
6. **Singleton `Instance` + `DontDestroyOnLoad`** НЕ нужен (окно на сцене, переживает streaming только если родитель — DontDestroyOnLoad, как MarketWindow)
7. **Проверка готовности сети** перед подпиской (`NetworkManager.Singleton != null && IsListening`)
8. **FIX 2026-06-04 (MarketWindow):** 4 бага, которые нужно сразу применить:
   - `pickingMode = Ignore` на `_root` при Hide, `Position` при Show
   - `ApplyInlineFallbackStyles()` на `main-container` при Show (resolvedStyle=initial на 1-м кадре)
   - Cursor lock/unlock при Show/Hide
   - `_doc.rootVisualElement.MarkDirtyRepaint()` + `schedule.Execute(...).StartingIn(50)`
9. **Никаких дублирующих singleton-state'ов** — UI читает ИСКЛЮЧИТЕЛЬНО из существующих state-классов (`ContractClientState`, `NetworkPlayer.Inventory`)

## 7. Открытые вопросы для пользователя

| 1. **Открытие окна:** какая кнопка? Варианты:
   - `P` (P like "P"ress / "P"rofile / "P"erson) — **выбрано пользователем 2026-06-05**
   - `C` (Character, как в TES) — отвергнуто
   - `I` (Inventory, как в Minecraft) — семантически про инвентарь, не подходит
   - `Tab` (но Tab уже занят колесом инвентаря) |
2. **Контекст репутации:** в GDD-23 описано 5 гильдий + 4 мануфактуры + военные + чёрный рынок. Сколько показывать в MVP-заглушке? 5 гильдий хватит, или сразу 9?
3. **Контекст корабля:** в MVP-заглушке — что показывать? Варианты:
   - (a) только локальные данные с NetworkPlayer (текущий корабль, статус Walking/Flying)
   - (b) + статический список "известные игроку корабли" (нужен новый client-state)
   - **предложение Mavis:** (a) — потому что (b) требует серверного списка, которого нет
4. **История контрактов:** хранить локально в `ContractClientState.History`? Или сервер шлёт? (Сейчас — никак; завершённые контракты теряются при перезаходе в рынок).
5. **Куда поместить окно:** аналогично `MarketWindow` — `Assets/_Project/UI/Scripts/Client/CharacterWindow.cs` (новая папка) или `Assets/_Project/Trade/Scripts/Client/`? Торговля — это чуть другое, и Character — это player-state, не trade. **предложение Mavis:** `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` рядом с `UITheme`, `UIManager`. UXML/USS — в `Assets/_Project/UI/Resources/UI/CharacterWindow.{uxml,uss}`.

---

## Связанные документы

- `docs/Character-menu/10_DESIGN.md` — UXML/USS дизайн, лейауты, контролы
- `docs/Character-menu/20_IMPLEMENTATION_PLAN.md` — пошаговый план для сабагентов
- `docs/Character-menu/30_VERIFICATION.md` — чек-листы ручной проверки
- `docs/dev/CONTRACTS_AS_MARKET_TAB_REFACTOR.md` — C2-референс табов
- `docs/Markets/FIXES_HISTORY.md` (2026-06-04) — 4 FIX'а, которые бесплатно применяются к новому окну
- `AGENTS.md` — хард-рулы проекта
- `GDD_23_Faction_Reputation.md` — что показывать в табе "Репутация" (план)
- `GDD_10_Ship_System.md` — что показывать в табе "Корабль" (план)
- `GDD_22_Economy_Trading.md` — контракты (что уже есть)
- `GDD_21_Quest_Mission_System.md` — квесты (план)
