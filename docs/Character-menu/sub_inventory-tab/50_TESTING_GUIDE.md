# Inventory Sub-System — Testing Guide

**Дата:** 2026-06-05
**Зависит от:** `30_VERIFICATION.md`

Этот документ — **пошаговые сценарии тестирования**. Скриншоты делает **пользователь** (per AGENTS.md, Mavis не делает скриншоты).

---

## 0. Подготовка

```bash
# 1. Открой проект в Unity 6000.4.1f1
# 2. Дождись компиляции
# 3. Проверь Console → 0 моих errors
# 4. Открой BootstrapScene
# 5. Убедись в Hierarchy:
#    - [NetworkManager]
#    - [InventoryWheel]  ← новый
#    - [InventoryServer] ← новый
#    - [CharacterWindow]
```

**Если чего-то нет:**
- `[InventoryWheel]` → см. `20_IMPLEMENTATION_PLAN.md` Phase 4 (MCP-команды)
- `[InventoryServer]` → см. Phase 2

---

## 1. Test #1: Compile check (DONE)

**Что:** Убедиться, что код компилируется.

**Шаги:**
1. Открой Unity Editor
2. Дождись компиляции (status bar внизу)
3. `Window → General → Console`
4. Сбрось фильтры → `Clear`

**Ожидаемо:** `0 errors`. Допустим 1 pre-existing warning: "We have detected that your project includes custom elements added to the Unity Editor's main toolbar".

**Если ошибки есть:**
- Сделай скрин Console
- Пришли мне
- Я починю

---

## 2. Test #2: TAB-колесо открывается

**Что:** Проверить, что новое UI Toolkit-колесо появляется по Tab.

**Шаги:**
1. `▶` (Play)
2. `NetworkManagerController → StartHost` (через Inspector или меню)
3. Spawn player в WorldScene_0_0
4. Нажми `Tab`

**Ожидаемо:**
- Появляется окно `InventoryWheel` в центре экрана
- 8 секторов в круге
- Все сектора серые (инвентарь пуст)
- Sublist справа: "Выберите сектор"
- Header: "ИНВЕНТАРЬ"
- Actions: "ИСПОЛЬЗОВАТЬ", "ЗАКРЫТЬ"
- Message: "Откройте инвентарь по TAB"

**Скриншот:** сохрани `Assets/Screenshots/test2_tab_open.png` (для протокола).

**Если не работает:**
- Console → `[InventoryUI] Built`? (если нет — UXML не найден)
- Console → `[InventoryClientState] OnSnapshotUpdated`? (если нет — singleton не создан)
- Если HUD зависает / колесо не реагирует на Tab → пришли скрин + Console

---

## 3. Test #3: TAB-колесо закрывается

**Что:** Tab повторно / Esc / кнопка "ЗАКРЫТЬ" — колесо исчезает.

**Шаги:**
1. (после Test #2) Нажми `Tab` ещё раз

**Ожидаемо:** колесо исчезает, cursor lock возвращается.

**Альтернативы:**
- `Esc` (в P-меню есть, в TAB-колесе — TODO Phase 8)
- Клик "ЗАКРЫТЬ" (action button)

---

## 4. Test #4: Подбор предмета → обновляются оба UI

**Что:** Проверить, что подбор PickupItem работает и обновляет TAB-колесо + P-таб.

**Предусловие:** на сцене есть `PickupItem` (любой со ссылкой на `ItemData`).

**Шаги:**
1. (после Test #3) Встань рядом с PickupItem
2. Нажми `E`
3. Подожди 100-300ms (RPC round-trip)

**Ожидаемо:**
- В Console: `[PickupItem] <ItemName> успешно подобран` (через InventoryClientState.HandlePickupResult)
- PickupItem **исчезает** из мира
- TAB-колесо: соответствующий сектор стал **зелёным** (`sector-has-items` class), label показывает `[1]`
- P-меню: TAB → P → таб "Инвентарь" → запись в списке (имя, тип, qty "1")

**Скриншоты:**
- `Assets/Screenshots/test4a_tab_after_pickup.png`
- `Assets/Screenshots/test4b_p_tab_inventory.png`

**Если не работает:**
- PickupItem не деактивируется → значит server не подтвердил pickup. Проверь:
  - `[InventoryServer] OnNetworkSpawn` в Console (должен быть)
  - `[InventoryWorld] Created. Items registered: 24` (ItemDatabase заполнена)
  - NetworkManager.IsServer = true
- PickupItem деактивируется, но UI не обновляется → значит UI не подписан. Проверь:
  - `[InventoryUI] HandleSnapshotUpdated` в Console (добавь `Debug.Log` если нужно)
  - `CharacterWindow.HandleInventorySnapshotUpdated` в Console

---

## 5. Test #5: Подбор нескольких предметов одного типа

**Что:** Проверить стэкинг (даже если MVP quantity=1 на itemId).

**Шаги:**
1. Размести на сцене 3 PickupItem с одним и тем же ItemData (например, "Железная руда")
2. Подбери все 3 (E + E + E)

**Ожидаемо:**
- TAB: сектор "РЕСУРСЫ" показывает `[3]` (count = 3)
- P-таб: 3 записи "Железная руда" (каждая qty=1) **ИЛИ** 1 запись с qty=3 (зависит от того, как `RefreshInventoryCache` группирует)

**Текущее поведение (Phase 7):** `RefreshInventoryCache` группирует по `itemId`, суммирует quantity. Ожидаемо: 1 запись с qty=3.

**Скриншот:** `Assets/Screenshots/test5_stacking.png`

---

## 6. Test #6: Подбор разных типов

**Что:** Проверить, что разные ItemType попадают в разные сектора.

**Шаги:**
1. Подбери 1 Resources (Железная руда) + 1 Food (Сухпаёк) + 1 Tech (Батарея)

**Ожидаемо:**
- TAB-колесо: 3 сектора зелёные (Resources, Food, Tech)
- P-таб: 3 записи в списке (ресурс, еда, техника — все разные типы)

---

## 7. Test #7: Cross-tab sync (TAB ↔ P-таб)

**Что:** Подбор в TAB виден в P-табе и наоборот.

**Шаги:**
1. Подбери предмет → TAB-колесо показывает зелёный сектор
2. Закрой TAB → открой P (`P`)
3. Таб "ИНВЕНТАРЬ" → запись есть
4. Закрой P → TAB → та же запись в sublist (выбери нужный сектор)

**Ожидаемо:** данные идентичны, синхронизированы через `InventoryClientState.CurrentSnapshot`.

---

## 8. Test #8: Cross-tab feedback (message label)

**Что:** Когда подбор происходит, message label обновляется в ОБОИХ UI.

**Шаги:**
1. TAB открыт
2. Нажми E рядом с PickupItem
3. **НЕ закрывая TAB** — открой P (через Alt+Tab или как удобно)
4. В P-табе "Инвентарь" — message label показывает результат операции

**Ожидаемо:** оба UI показывают feedback (зелёный "OK" / "Подобран предмет").

**Pitfall #11:** cross-tab feedback обязателен. Проверь, что `_messageLabel` обновляется в `HandleInventoryResultReceived` БЕЗ проверки `_activeTab == "inventory"`.

---

## 9. Test #9: Visual layout (TAB-колесо)

**Что:** Проверить, что 8 секторов расположены по окружности, не наезжают друг на друга.

**Скриншот:** `Assets/Screenshots/test9_layout.png`

**Ожидаемо:**
- 8 секторов в круге 380×380 px
- Размер каждого сектора 110×110 px
- Лейблы внутри секторов
- Центр: "—" + "0" (или имя типа + count после выбора)
- Sublist справа (flex-grow)

**Если сектора наезжают:**
- Это означает, что USS `position: absolute` + top/left не сработали
- Скорее всего: тема `UnityDefaultRuntimeTheme` перебивает (проверь, что `.sector-N` стили с `!important`)

---

## 10. Test #10: Visual states (sector classes)

**Что:** Пустой/с-предметами/hover/selected — все 4 состояния видны.

**Шаги:**
1. TAB открыт, инвентарь пуст → все сектора серые
2. Подбери предмет → соответствующий сектор стал зелёным
3. Наведи мышь на зелёный сектор → стал жёлтым, увеличился (scale 1.1)
4. Уведи мышь → вернулся зелёный
5. Кликни на сектор → стал золотой, увеличился больше (scale 1.15)
6. Кликни на другой сектор → первый вернулся к зелёному, новый стал золотой

**Скриншоты:**
- `Assets/Screenshots/test10a_empty.png` (все серые)
- `Assets/Screenshots/test10b_has_items.png` (1 зелёный)
- `Assets/Screenshots/test10c_hover.png` (1 жёлтый, scale)
- `Assets/Screenshots/test10d_selected.png` (1 золотой, scale)

---

## 11. Test #11: Sublist (правая панель)

**Что:** Клик на сектор → справа появляется список предметов.

**Шаги:**
1. Подбери 5 разных предметов (Resources, Food, Tech, Equipment, Meziy)
2. Кликни на сектор "Resources" → справа sublist с железной рудой, медной рудой, крист. пылью (если подобрал все)
3. Кликни на "Food" → sublist обновился (показывает сухпаёк и т.д.)

**Ожидаемо:**
- `RefreshSublist(type)` вызывается
- `ListView.itemsSource = state.GetItemsByType(type)` — массив предметов этого типа
- Center label показывает имя типа + count

**Скриншот:** `Assets/Screenshots/test11_sublist.png`

---

## 12. Test #12: P-таб фильтры

**Что:** В P-табе работают фильтры по типу и поиск.

**Шаги:**
1. P → таб "ИНВЕНТАРЬ"
2. Filter dropdown: выбери "Ресурсы" → список сократился
3. Filter dropdown: "Все типы" → вернулся полный
4. Search field: введи "руда" → только железная и медная руда

**Ожидаемо:** фильтры работают корректно.

**Скриншот:** `Assets/Screenshots/test12_p_filters.png`

---

## 13. Test #13: Compile после тестов (regression check)

**Что:** После всех тестов компиляция всё ещё 0 errors.

**Шаги:**
1. Stop Play
2. `Window → General → Console` → `Clear`
3. Дождись re-compile (если менял код)
4. `0 errors expected`

---

## 14. Test #14 (advanced, optional): Multi-client sync

**Что:** Два клиента видят общее состояние инвентаря.

**Предусловие:** ParrelSync установлен (https://github.com/Verior/ParrelSync).

**Шаги:**
1. Создай клон проекта через ParrelSync
2. Открой оба проекта
3. Project A: `Play → Start Server`
4. Project B: `Play → Start Client` (подключись к A)
5. На A: подбери предмет
6. На B: TAB → видно тот же предмет

**Ожидаемо:** оба клиента синхронизированы через `InventoryServer.SendSnapshot`.

**Если не работает:**
- Пришли Console обоих клиентов
- Проверь, что `IsServer` корректно на обоих
- Скорее всего, проблема в `ScenePlacedObjectSpawner` (нужно проверить, что он спавнит `[InventoryServer]`)

**⚠️ Phase 7 не проверено в реальности.** Если не работает — Phase 8 (cleanup + multi-client verify).

---

## 15. Test #15: Edge case — закрытие UI не теряет данные

**Что:** Закрытие TAB-колеса / P-таба не теряет состояние инвентаря.

**Шаги:**
1. Подбери 3 предмета
2. TAB → видишь 3 зелёных сектора
3. Закрой TAB
4. P → таб "Инвентарь" → видишь 3 записи
5. Закрой P
6. TAB → опять видишь 3 зелёных сектора

**Ожидаемо:** данные сохраняются в `InventoryClientState.CurrentSnapshot` (singleton, DontDestroyOnLoad).

---

## 16. Сводка по багам (что фиксить)

| # | Bug | Workaround |
|---|---|---|
| 1 | NetworkChestContainer не использует InventoryServer | Phase 8 (cleanup) |
| 2 | Esc не закрывает TAB-колесо | Клик "ЗАКРЫТЬ" |
| 3 | Use-кнопка ничего не делает | Phase 8 |
| 4 | Multi-client не проверен | Phase 8 (ParrelSync verify) |
| 5 | Item_Type1..8 не обновлены (maxStack/weightKg) | Используй Item_*.asset (новые) |
| 6 | Drop в мир не работает | Phase 8 |
| 7 | Stackable inventory не работает (qty=1 hardcoded) | Phase 8 |
| 8 | Cargo / weightKg не используется | Phase 8+ |

**Если найдёшь новый баг** — пришли скрин + Console + шаги для repro, я починю.

---

## 17. Checklist для коммита

После успешного тестирования:
- [ ] Все Test #1-13 прошли
- [ ] Скриншоты в `Assets/Screenshots/` (опционально, для протокола)
- [ ] Compile 0 errors
- [ ] Файлы в git working tree соответствуют ожиданиям

**Что коммитить** (см. также `docs/dev/INVENTORY_V2_REFACTOR.md` §11.4):
- `Assets/_Project/Items/` (8 .cs + 24 .asset + .meta)
- `Assets/_Project/UI/Resources/UI/InventoryWheel.{uxml,uss}` + .meta
- `Assets/_Project/UI/Client/InventoryUI.cs` + .meta
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` (патч)
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (патч)
- `Assets/_Project/Scripts/Core/PickupItem.cs` (переписан)
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (патч)
- `Assets/_Project/Scripts/Core/ItemType.cs` (патч)
- `Assets/_Project/Scenes/BootstrapScene.unity` (MCP-патч)
- `docs/dev/INVENTORY_V2_REFACTOR.md`
- `docs/Character-menu/sub_inventory-tab/*.md` (7 файлов)

**Сообщение коммита (предложение):**
```
inventory v2 refactor: server-authoritative InventoryClientState + TAB/P-tab UI

- New: ProjectC.Items subsystem (DTO + World + ClientState + Server)
- New: TAB-колесо (UI Toolkit) с подпиской на ClientState
- New: P-таб "Инвентарь" в CharacterWindow подключён к ClientState
- New: ItemData + 24 .asset (тестовый датасет)
- New: [InventoryWheel] и [InventoryServer] в BootstrapScene
- New: PickupItem → RequestPickup RPC (предметы теперь подбираются)
- Docs: docs/dev/INVENTORY_V2_REFACTOR.md + sub_inventory-tab/* (8 файлов)
- Parallel stack: Inventory.cs / старый InventoryUI.cs живут (cleanup в Phase 8)
- 0 compile errors, 0 моих warnings
```

> **ВАЖНО:** Коммитит **пользователь** (per AGENTS.md, Mavis не делает `git commit`).
