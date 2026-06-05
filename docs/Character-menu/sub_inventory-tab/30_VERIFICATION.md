# Inventory Sub-System — Verification (manual smoke checklist)

**Дата:** 2026-06-05
**Зависит от:** `00_OVERVIEW.md`, `20_IMPLEMENTATION_PLAN.md`
**Кто делает:** пользователь (Mavis не делает скриншоты по AGENTS.md)

---

## Чек-лист готовности к тестированию

Перед тем как начать, проверь:
- [ ] Unity Editor открыт, компиляция прошла (0 моих errors)
- [ ] В Hierarchy BootstrapScene видны: `[InventoryWheel]`, `[InventoryServer]`, `[NetworkManager]`, `[CharacterWindow]`
- [ ] В `Resources/Items/` есть 24 файла `Item_*.asset`

Если что-то не совпадает — пришли скрин, я починю.

---

## 1. Compile check (DONE в этой сессии)

```
1. Открой Unity Editor
2. Дождись компиляции
3. Window → General → Console
4. Ожидаемо: 0 errors, 0 моих warnings
   (может быть 1 pre-existing warning: "We have detected that your project includes custom elements added to the Unity Editor's main toolbar")
```

**Статус:** ✅ подтверждено через `mcp_unity_client.py read_console`.

---

## 2. Play mode smoke — single client

### 2.1 Запуск
```
1. Нажми ▶ (Play)
2. В NetworkManagerController inspector (или через меню) → Start Host
3. Spawn player (должен быть в WorldScene_0_0)
```

**Ожидаемо в Console:**
- `[NetworkManager] Started host`
- `[InventoryClientState]` (если логируешь OnEnable) — singleton создан
- `[InventoryServer] OnNetworkSpawn. IsServer=True, _itemCache=24`
- `[InventoryWorld] Created. Items registered: 24`

### 2.2 Открой TAB-колесо

```
1. Нажми Tab
```

**Ожидаемо:**
- Появляется окно `InventoryWheel` в центре экрана
- 8 секторов в круге (Resources, Equipment, Food, Fuel, Antigrav, Meziy, Medical, Tech)
- Все сектора СЕРЫЕ (инвентарь пуст)
- Center label "—", count "0"
- Sublist справа: "Выберите сектор"

### 2.3 Закрой TAB-колесо

```
1. Нажми Tab ещё раз (или Esc, или ZАКРЫТЬ)
```

**Ожидаемо:** колесо исчезает, cursor lock возвращается (если был включён flight-режим).

### 2.4 Подбери предмет

```
1. Встань рядом с PickupItem (любой со сцены, например в WorldScene_0_0)
2. Нажми E
```

**Ожидаемо:**
- В Console: `[PickupItem] <ItemName> успешно подобран` (через 100-300ms после E)
- PickupItem **исчезает** из мира
- TAB → соответствующий сектор стал ЗЕЛЁНЫМ, label показывает тип + `[1]`
- P → таб "ИНВЕНТАРЬ" → запись (icon пуст, name, type, qty "1")
- Если есть несколько предметов одного типа — сектор покажет `[N]`

### 2.5 Открой сундук (если есть на сцене)

```
1. Встань рядом с NetworkChestContainer
2. Нажми E
```

**Ожидаемо:**
- Chest opens (анимация)
- Все предметы из LootTable добавлены в инвентарь
- TAB-колесо: 1-3 сектора стали зелёными (в зависимости от LootTable)
- P-таб: список пополнился
- ⚠️ **Известное ограничение Phase 1-7:** `NetworkChestContainer` пока использует СТАРЫЙ `NetworkInventory`, не новый `InventoryServer`. Сундук **может не работать** на 100% (см. `60_KNOWN_ISSUES.md`). Если не работает — это Phase 8.

### 2.6 Cross-tab sync (TAB ↔ P-таб)

```
1. TAB → подбери предмет (если ещё есть PickupItem на сцене)
2. Закрой TAB → открой P
3. Таб "ИНВЕНТАРЬ" → запись есть
4. Закрой P → TAB → запись в sublist (если выбран нужный сектор)
```

**Ожидаемо:** оба UI показывают одни и те же данные, обновляются одновременно.

### 2.7 Cross-tab feedback

```
1. TAB → подбери предмет
2. НЕ закрывая TAB → открой P (CharacterWindow)
3. В P-табе "Инвентарь" → должна появиться запись
4. Вернись в TAB → колесо обновилось
```

**Ожидаемо:** message label в обоих UI показывает feedback (зелёный "OK" / "Подобран предмет").

### 2.8 Credits (TODO, Phase 8+)

```
1. Если в snapshot.credits != 0 → header в P-табе и TAB показывает кредиты
2. Если == 0 → "0 CR" (заглушка)
```

**Ожидаемо:** `Кредиты: 0 CR` в header. Phase 8: подключить к PlayerDataRepository.

---

## 3. Visual checks (для TAB-колеса)

### 3.1 Layout
- [ ] Колесо 380×380 px, по центру экрана
- [ ] 8 секторов расположены по окружности (см. `10_DESIGN.md` §1.3)
- [ ] Sublist справа (flex-grow, минимум 280px)
- [ ] Header сверху, actions снизу, message внизу
- [ ] Нет наложений, всё читаемо

### 3.2 Цвета секторов
- [ ] Пустой: тёмно-серый (rgba 45,45,50,0.5)
- [ ] С предметами: зелёный (rgba 50,90,50,0.7)
- [ ] Hover: жёлтый (rgba 180,150,50,0.6), scale 1.1
- [ ] Selected: золотой (rgba 220,180,60,0.7), scale 1.15, толстая золотая рамка

### 3.3 Sublist
- [ ] Каждая row 32px высотой
- [ ] Icon 24×24 (если icon = null — серая заглушка)
- [ ] Name — белый, flex-grow
- [ ] Qty — справа, "×N" если > 1
- [ ] Hover row — синяя подсветка

### 3.4 Frame rate
- [ ] 60 FPS (UI Toolkit лёгкий)
- [ ] Sublist Rebuild не лагает (тест: добавить 10 предметов подряд)

---

## 4. Multi-client smoke (ParrelSync, опционально)

### 4.1 Setup
```
1. Установи ParrelSync (https://github.com/Verior/ParrelSync)
2. File → ParrelSync → Create clone → "InventoryMultiClient"
3. Открой оба проекта (текущий + клон)
4. В текущем: Play → Start Server
5. В клоне: Play → Start Client
```

### 4.2 Cross-client verification
```
1. В Server: подбери предмет на Client 1
2. На Client 2: TAB → видно тот же предмет
```

**Ожидаемо:** оба клиента видят общее состояние инвентаря. (Phase 7 не проверено в реальности — это первая попытка multi-client.)

Если не работает:
- Пришли скрин + Console обоих клиентов
- Скорее всего: `InventoryServer` шлёт snapshot ТОЛЬКО Owner'у (это by design), нужно проверить, что `[InventoryServer].Instance != null` на сервере

---

## 5. Известные баги (см. `60_KNOWN_ISSUES.md`)

1. **NetworkChestContainer использует СТАРЫЙ NetworkInventory** — сундук может не работать (Phase 8)
2. **`ScenePlacedObjectSpawner`** — должен спавнить `[InventoryServer]`. Проверь в Console: `ScenePlacedObjectSpawner] Scene (0,0): spawned=N` — N должно включать InventoryServer
3. **ItemType.cs: maxStack/weightKg** — старые `Item_Type1..8` НЕ обновлены (имеют 1/0.1). Новые `Item_*.asset` — OK.
4. **Скриншоты** — делаешь ты, Mavis не делает (per AGENTS.md)

---

## 6. Что делать если что-то не работает

1. **Compile errors** — пришли текст, я починю
2. **Runtime NRE в OnInventoryResult** — скорее всего, отсутствует подписка. Проверь `InventoryClientState.Instance != null` в Console
3. **TAB-колесо не появляется** — проверь что `[InventoryWheel]` GameObject в сцене + `UIDocument.sourceAsset = InventoryWheel.uxml` + `UIDocument.panelSettings != null`
4. **Сектора не зелёные после подбора** — проверь Console: `[InventoryServer] OnNetworkSpawn` + `IsServer=True`. Если `IsServer=False` (т.е. ты client-only) — должно работать через NetworkVariable
5. **P-таб "Инвентарь" пустой после подбора** — проверь `CharacterWindow.HandleInventorySnapshotUpdated` срабатывает (можно добавить `Debug.Log`)

---

## 7. Success criteria

Сессия считается успешной, если:
- [x] Компиляция 0 errors
- [ ] TAB-колесо открывается по Tab
- [ ] Подбор предмета (E) → оба UI обновляются
- [ ] Клик на сектор → sublist с предметами этого типа
- [ ] P-таб "Инвентарь" показывает тот же набор
- [ ] Закрытие/открытие UI сохраняет данные

Phase 8 (cleanup + stackable + drop) — отдельная сессия.
