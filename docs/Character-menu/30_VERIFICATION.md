# CharacterWindow — Чек-листы проверки

**Дата:** 2026-06-05
**Зависит от:** `00_OVERVIEW.md`, `10_DESIGN.md`, `20_IMPLEMENTATION_PLAN.md`

> Mavis и сабагенты **не запускают** Unity, не делают `git commit`, не вызывают `run_tests`. Эти чек-листы — для **пользователя**, который проверяет результат.

---

## 0. Compile check (после каждой фазы)

```powershell
# В PowerShell: открыть Unity Editor → Console → проверить ошибки
# Ожидаемо: 0 errors, 0 warnings (warning о Inspector-полях допустимы)
```

Через MCP (если нужно проверить из bash):
```bash
mcp__unityMCP__refresh_unity '{"mode": "force", "compile": "request", "wait_for_ready": true}'
mcp__unityMCP__read_console '{"action": "get", "types": ["error", "warning"], "count": 20}'
```

Ожидаемый результат: `error_count: 0`, `warning_count: 0` (или только warnings о том, что `GameObject [CharacterWindow] не использует компонент X` — это не критично).

---

## 1. Фаза 0 — каркас

### 1.1. Файлы созданы

| Проверка | Как |
|----------|-----|
| UXML существует | `ls Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` |
| USS существует | `ls Assets/_Project/UI/Resources/UI/CharacterWindow.uss` |
| .cs существует | `ls Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` |
| `.meta` файлы созданы Unity'ом автоматически | `ls ...*.meta` — если есть, это нормально |
| **НЕТ `.asmdef` в `Assets/_Project/UI/`** | `find Assets/_Project/UI -name "*.asmdef"` → пусто |

### 1.2. GameObject в BootstrapScene

- Открыть `Assets/_Project/Scenes/BootstrapScene.unity` в Editor
- В Hierarchy должен быть `[CharacterWindow]` GameObject рядом с `[MarketWindow]`
- В Inspector: компоненты `UIDocument` + `CharacterWindow`
- `UIDocument.SourceAsset` → `UI/CharacterWindow.uxml` (или `null`, если используется Resources fallback)
- `UIDocument.PanelSettings` → `MarketPanelSettings.asset` (re-use)

### 1.3. Smoke test

- Play → Host (через `NetworkUI` или кнопку Host)
- В Console должен быть лог `[CharacterWindow] Built (phase 0): root.children=1` (или больше)
- Открыть контекстное меню `[CharacterWindow]` → вызвать `Show()` через `Window → General → Search → "CharacterWindow"` (если есть debug-menu) или добавить временный key-handler в NetworkPlayer (удалить после проверки)
- Окно появилось по центру экрана, header/tabs видны, контент-секции видны
- Esc → окно закрылось, курсор залочился
- Host/Server buttons в стартовом UI кликабельны (pickingMode=Ignore работает)

---

## 2. Фаза 1 — SwitchTab + таб "Персонаж"

### 2.1. Compile

- 0 errors в Console

### 2.2. SwitchTab работает

- Play → Host → открыть CharacterWindow (пока через context-menu или временный debug)
- Кликнуть "ПЕРСОНАЖ" → видны стат-поля (Имя, Уровень, Опыт, Кредиты, Долг, Активные контракты)
- Кликнуть "КОРАБЛЬ" → видно только секцию "Текущий корабль" (остальные скрыты)
- Кликнуть "РЕПУТАЦИЯ" → видно только секцию "Репутация по фракциям"
- Кликнуть "КОНТРАКТЫ" → видно только секцию "Контракты и квесты" (но пусто — подписки ещё нет)
- Кликнуть "ИНВЕНТАРЬ" → видно только секцию "Инвентарь" (но пусто)
- Активный таб визуально подсвечивается (желтая нижняя граница)

### 2.3. Закрытие/открытие

- Закрыть (Esc или ЗАКРЫТЬ) → окно скрылось, pickingMode=Ignore
- Открыть заново → окно появилось, pickingMode=Position
- Курсор: при открытом окне — свободен, при закрытом — залочен (если в игре)

---

## 3. Фаза 2 — таб "Контракты"

### 3.1. Compile

- 0 errors

### 3.2. Подписка

- Play → Host → подойти к `MarketZone_Primium` → E → открыть MarketWindow → вкладка "Контракты" → видны pending+active (как раньше)
- Закрыть MarketWindow → открыть CharacterWindow → таб "Контракты" → **те же контракты** видны (active + available этой локации)

### 3.3. Действия

- В CharacterWindow → таб "Контракты" → выбрать pending-контракт → клик "ВЗЯТЬ"
  - Ожидаемо: row стал зелёным + [ВЗЯТ] (optimistic update)
  - Через ~1.5с: pulse-эффект прошёл, state подтверждён сервером
  - Кредиты в header обновились (если был complete — кредиты увеличились)
- Выбрать active-контракт → "СДАТЬ" → сообщение в message-label "Запрос отправлен..." → через ~0.5с результат от сервера

### 3.4. Фильтры

- В CharacterWindow → таб "Контракты" → filter-state = "Активные" → видны только Active контракты
- filter-state = "Доступные" → видны только Pending
- filter-source = "Квесты" → пусто (квесты не реализованы) + сообщение в message-label
- filter-search = "мезий" → видны контракты с "мезий" в displayName/contractId
- Вернуть "Все"/"Все" → полный список

### 3.5. Реюз с MarketWindow

- Открыть **оба** окна нельзя (CharacterWindow не блокирует, но Esc закрывает верхнее — UIManager сортирует по приоритету если реализовано; если нет — Esc закрывает последнее открытое)
- Альтернатива: открыть MarketWindow → вкладка Контракты → ВЗЯТЬ → CharacterWindow → Контракты → контракт теперь Active (тот же singleton-проекция)

### 3.6. Race-conditions

- Быстро переключать вкладки 5 раз → нет утечек ListView, нет дублирования подписок
- Закрыть/открыть CharacterWindow 10 раз → нет зависших subscriptions, нет дублей events

---

## 4. Фаза 3 — Корабль, Репутация, Инвентарь

### 4.1. Compile

- 0 errors

### 4.2. Корабль

- CharacterWindow → таб "Корабль" → видны поля (Корабль, Состояние, Скорость, Топливо, Грузоподъёмность)
- Если игрок в корабле (нажал F) → "Состояние" = "В корабле"; иначе "На палубе"
- Остальные поля — "—" (плейсхолдер)

### 4.3. Репутация

- CharacterWindow → таб "Репутация" → видны 5 строк-фракций:
  - Гильдия Торговцев
  - Мануфактура «Аврора»
  - Военный Анклав
  - Сопротивление
  - Чёрный Рынок
- У каждой — bar 0% (т.к. value=0), label "+0"
- Под списком — placeholder-hint с ссылкой на GDD-23

### 4.4. Инвентарь

- До подбора предметов → CharacterWindow → таб "Инвентарь" → пусто (или "Нет предметов" в message)
- Подобрать предмет (подойти к сундуку → E)
- Открыть CharacterWindow → таб "Инвентарь" → видна 1 строка с displayName, тип (Ресурсы/Еда/...), qty "×1"
- Подобрать ещё 5 одинаковых (например, ещё один сундук с тем же item) → qty "×6" (группировка работает)
- Фильтр "Ресурсы" → видны только предметы типа Resources
- Фильтр "Все типы" → все предметы
- filter-search "мезий" → видны только Мезий

### 4.5. Адаптивность (важно!)

- Свернуть Unity до размера 800×600 → окно всё ещё читаемо, max-width 90% сработал, max-height 90% сработал, ничего не вылезло
- Развернуть на 2560×1440 → окно по центру, не разъехалось, не сжалось
- Табы — flex-wrap включён, на узком экране переносятся

---

## 5. Сквозные проверки (regression)

### 5.1. MarketWindow не сломан

- Открыть MarketWindow → все 3 таба (Рынок, Склад, Контракты) работают как раньше
- ВЗЯТЬ/СДАТЬ контракты работают
- КУПИТЬ/ПРОДАТЬ работают
- ПОГРУЗИТЬ/РАЗГРУЗИТЬ работают
- Таймер контракта идёт (визуально не проверяется, но при следующем snapshot обновляется)

### 5.2. InventoryUI (колесо) не сломан

- Tab → колесо появляется → видны 8 секторов
- Подобрать предмет → сектор подсвечивается (flash)
- Колесо и CharacterWindow → Инвентарь показывают **одинаковые** данные (single source of truth: `NetworkPlayer.Inventory`)

### 5.3. NetworkPlayer не сломан

- Движение WASD/Space работает
- F (сесть/выйти из корабля) работает
- E (открыть сундук / подобрать предмет / открыть MarketWindow) работает
- Tab (открыть колесо) работает

### 5.4. Стартовый UI не перекрыт

- До подхода к MarketZone → Host/Server/Reconnect buttons кликабельны
- CharacterWindow.Hide() → pickingMode=Ignore → клики на стартовый UI проходят
- CharacterWindow.Show() → клики на стартовый UI НЕ проходят (это нормально, окно перекрывает)

---

## 6. Известные ограничения (MVP, НЕ баги)

- Таб "Корабль" — большинство полей плейсхолдеры (нужен `ShipClientState` для скорости/топлива)
- Таб "Репутация" — все значения 0 (нужен `ReputationClientState` + RPC)
- Таб "Контракты" → фильтр "Квесты" пуст (квесты не реализованы)
- Таб "Контракты" → нет истории завершённых контрактов (ContractClientState не хранит)
- **Открытие:** `P` (P = "Press" / "Profile" / "Person"), handler в `NetworkPlayer.Update()` → `CharacterWindow.Toggle()`. Esc внутри окна — закрывает.

---

## 7. Если что-то не работает

### Симптом: UXML не найден
```
[CharacterWindow] UXML не найден в Resources/UI/
```
**Решение:** проверить что файл `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` существует и Unity переимпортировал ассеты (правый клик в Project view → Reimport).

### Симптом: ContractClientState.Instance is null
```
[CharacterWindow] ContractClientState.Instance is null, контракты UI не будут обновляться
```
**Решение:** это нормально, если игрок не в network. После StartHost() NetworkManagerController.CreateContractClientState() создаст singleton. Перезайти в таб Контракты.

### Симптом: Inventory пустой
- Если игрок только зашёл и ничего не подбирал — это нормально
- Если подбирал — проверить что Inventory не потерян (OnNetworkSpawn спавнит Inventory как child)

### Симптом: Esc не закрывает окно
- Проверить что Update() в CharacterWindow выполняется (Owner-объект должен быть активен)
- CharacterWindow на root-GO? Иначе DontDestroyOnLoad нужен (как MarketWindow — она на root)

### Симптом: Host/Server buttons не кликаются после открытия
- CharacterWindow.Hide() → pickingMode=Ignore выставлен? Проверить в коде
- Если проблема persistent — добавить `Debug.Log` в `Show/Hide` для диагностики

### Симптом: Окно "уезжает" в (-320, 0) при первом открытии
- `ApplyInlineFallbackStyles` не вызвался? Проверить `SetVisible(true)` ветку
- resolvedStyle.width=0 — это нормально на 1-м кадре, inline-стили фиксят мгновенно
