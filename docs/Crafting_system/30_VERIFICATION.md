# Crafting System — Verification (как тестировать)

> **Цикл:** Проектирование. Этот документ — **для тебя**, чтобы прогнать после имплементации.
> **Без кода.** Только сценарии и ожидаемые результаты.

---

## 1. Compile-verify (после каждого шага)

**Что делать:**
1. Открыть Unity Editor → Window → General → Console.
2. `refresh_unity` через MCP (skill `unity-mcp-orchestrator`).
3. Читать Console: должно быть `0 errors`, `0 warnings related to Crafting`.

**Ожидаемое:**
- `Compilation finished successfully.`
- Если есть `warning CS0414: field assigned but never used` — это OK, оставляем placeholder.

**Pitfall:** `mavis mcp call unityMCP refresh_unity '{"scope":"all","compile":"request","wait_for_ready":true}'` — **не** `mode=force` (см. memory + `unity-mcp-orchestrator` SKILL.md).

---

## 2. EditMode tests (когда настроим `.asmdef`)

**Не делаем в MVP** (AGENTS.md: "Не создавать `.asmdef` спекулятивно"). Когда `.asmdef` для `Crafting.Tests` будет — добавим:

- `CraftingWorld_RegisterRecipe_AssignsUniqueId`
- `CraftingWorld_OnTick_CompletesJob_WhenTimeElapsed`
- `CraftingWorld_TryAddIngredient_Fails_WhenSourceInsufficient`
- `CraftingWorld_TryCollect_IssuesOutput_ToInventoryWorld`
- `CraftingWorld_TryCancel_RefundsResources_ToSource`
- `CraftingWorld_Rejects_Collect_IfNotOwner`
- `CraftingWorld_Rejects_StartCraft_IfBufferIncomplete`

**Оценка EditMode-набора:** 8-10 тестов, ~3-4 ч (после создания `.asmdef`).

---

## 3. PlayMode / ручные сценарии

### Сценарий 1: «Стартовый крафт материала» (самый простой, smoke-test)

**Предусловие:**
- Host зашёл.
- У него в инвентаре: 3 × `Item_SteelIngot`, 0 × `Item_WoodenPlank`.
- Рецепт `R_WoodenPlank` существует: 3 steel → 1 wooden plank, 120 сек.
- Станция `Station_CraftingTable` имеет `allowedRecipes=[R_WoodenPlank]`.
- Станция размещена в `WorldScene_0_0` (или в тестовой сцене).

**Шаги:**
1. Подойти к станции (дистанция ≤ `_interactRadius` = 4м).
2. Нажать E → открывается MarketWindow, активный tab = "Крафт".
3. Drag&Drop 3 × steel из inventory на buffer-слот.
4. UI: "Буфер: 3 steel. Рецепт: ✓ хватает."
5. Клик "Старт".
6. UI: "Прогресс: 0%".
7. **Ускорить таймер** (debug): `MarketTimeService.MarketTimeMultiplier = 30` (через Inspector или `_timeService.MarketTimeMultiplier = 30f` в коде).
8. Подождать 4 секунды (120 / 30 = 4).
9. UI: "Готово! 1 × Wooden Plank."
10. Клик "Забрать".
11. Инвентарь: +1 × wooden plank, 0 × steel (списано 3).

**Ожидаемый Console:**
```
[CraftingWorld] Player 0 added ingredient steel x3 to station #5
[CraftingWorld] Player 0 started craft on station #5 recipe=1
[CraftingWorld] Tick dt=300s — checking jobs
[CraftingWorld] Station #5: job 0 complete, issuing outputs
[CraftingServer] Sent snapshot to client 0 (state=Completed, pendingOutputs=[wooden_plank x1])
[CraftingWorld] Player 0 collected output wooden_plank x1
```

**PASS критерии:** UI обновляется на каждом шаге, инвентарь изменяется, Console чистый.

---

### Сценарий 2: «Рецепт-корабль через MetaRequirement»

**Предусловие:**
- У игрока: 5 × `Item_SteelPlate`, 1 × `Item_AntigravCrystal`, 1 × `Item_FuelCell`.
- Рецепт `R_LightShipKey`: 5 steel + 1 antigrav + 1 fuel → ключ на `Ship_Light_v01` (NetworkObject в сцене), 3600 сек.
- Станция `Station_Shipyard` в `WorldScene_0_0`.
- `Ship_Light_v01` — NetworkObject + MetaRequirement (требует `Key_LightShip`).

**Шаги:**
1. Подойти к верфи, E → MarketWindow → Crafting.
2. Drag&Drop всех 3 типов.
3. UI: "Буфер: 5 steel, 1 antigrav, 1 fuel. Рецепт: ✓ хватает."
4. Старт. Прогресс-бар.
5. **Ускорить** до 300x: подождать ~12 секунд.
6. UI: "Готово! Корабль: Light Ship."
7. Забрать → в инвентарь НЕ добавился item (это ключ, не предмет). Вместо этого:
8. Toast: "Ключ на Light Ship выдан."
9. Открыть инвентарь → иконка ключа `Key_LightShip` x1.
10. Подойти к `Ship_Light_v01` в мире, E → "Борт возможен" → "OK".
11. Сел в корабль.

**Ожидаемый Console:**
```
[CraftingWorld] Job complete, shipKeyBinding.ShipNetworkObjectId=12
[MetaRequirementRegistry] GrantKeyToClient: ship=12, client=0
[NetworkPlayer.ReceiveMetaRequirementGrantTargetRpc] client=0, ship=12, keyItemId=42
[MetaRequirementClientState] OnKeyGranted: shipNetId=12, keyItemId=42
```

**PASS критерии:** ключ в инвентаре, корабль можно взять.

---

### Сценарий 3: «Anti-grief — не-owner не может Collect»

**Предусловие:**
- Игрок A: в инвентаре 3 × steel.
- Игрок B (host): в инвентаре 0 × steel.
- A подходит к станции, кладёт 3 steel в буфер, **НЕ** нажимает StartCraft.
- B подходит, видит "Буфер: 3 steel".
- B нажимает "Старт" → Job InProgress, owner=B.

**Шаги:**
1. Пока Job InProgress, A нажимает "Забрать" → toast: "Вы не заказчик".
2. A нажимает "Отмена" → toast: "Вы не заказчик".
3. B нажимает "Старт" повторно (после Completed) → toast: "Станция занята, очистите".
4. B нажимает "Забрать" → ок, модуль в инвентарь B.

**PASS критерии:** A не может вмешаться.

---

### Сценарий 4: «Coop — два игрока скидываются на верфь»

**Предусловие:**
- A: 2 × steel, 1 × antigrav.
- B: 3 × steel, 1 × fuel.
- Рецепт `R_LightShipKey`: 5 steel + 1 antigrav + 1 fuel.
- Станция: верфь.

**Шаги:**
1. A подходит, кладёт 2 steel + 1 antigrav.
2. B подходит, кладёт 3 steel + 1 fuel.
3. UI: "Буфер: 5 steel, 1 antigrav, 1 fuel. ✓ хватает."
4. B нажимает "Старт" → owner=B.
5. ...

**PASS критерии:** Job стартует, B становится owner, выдача идёт B.

---

### Сценарий 5: «Отмена + возврат ресурсов»

**Предусловие:** Job InProgress, owner=A, в буфере 3 steel.

**Шаги:**
1. A нажимает "Отмена".
2. UI: "Крафт отменён, ресурсы возвращены в инвентарь."
3. Инвентарь A: 3 × steel (восстановлено).

**Edge:** инвентарь полон → ресурсы уходят в warehouse (или в `CompletedJobs[]`-как inbox — см. Q5 в `00_OVERVIEW.md` §9).

---

### Сценарий 6: «Reconnect во время InProgress»

**Предусловие:** A нажал StartCraft, вышел из игры.

**Шаги:**
1. A заходит обратно.
2. Console: `[CraftingServer] OnClientConnected 0 → SendSnapshotsTo`.
3. A открывает MarketWindow → Crafting tab → видит "Активный крафт: Steel Module. Прогресс: 40%."
4. A может отменить или подождать.

**PASS критерии:** snapshot доставлен при reconnect.

---

### Сценарий 7: «Station unload (scene streaming)»

**Предусловие:** A крафтит в `WorldScene_0_0`. Уходит в `WorldScene_1_0` через стриминг.

**Шаги:**
1. `WorldScene_0_0` выгружается → `CraftingStation.OnNetworkDespawn`.
2. `CraftingServer.UnregisterStation(stationNetId)` → Job уничтожается, ресурсы возвращаются A.
3. A возвращается в `WorldScene_0_0` → станции нет (или другая) → `CraftingClientState.RequestSubscribe` → нет snapshot → UI: "Станция не найдена".

**PASS критерии:** ресурсы возвращены, Job потерян (это MVP поведение, см. Q2 в `00_OVERVIEW.md` §9).

---

## 4. Network-stress test (опционально, для Phase 2)

- Спам RPC: 100 `RequestAddIngredient` за 1 сек → rate limit срабатывает на 60+ → `RateLimited` в Console.
- 10 станций одновременно × 3 игрока × крафт → snapshot lag < 100мс.

---

## 5. UI-specific проверки

### 5.1 Tab switching

- Открыть Crafting tab → кнопка подсвечена (`.active`), секция `display: flex`.
- Перейти на Warehouse tab → секция `display: none`, кнопка без `.active`.
- Crafting tab НЕ перекрывает UGUI Canvas (Host/Server buttons) — проверка `pickingMode = Ignore` (см. `project-c-ui-as-tab` SKILL.md FIX #1).

### 5.2 Drag-and-drop

- Drag из inventory → drop на buffer → optimistic update (UI обновляется мгновенно).
- Через ~200мс — server snapshot перезаписывает (идемпотентно).
- Если drop вне buffer → cancel.
- Drop уже-полного слота (если 1 слот = 1 тип) → "Нельзя смешивать".

### 5.3 Pitfall R3-005 (cache update)

**Тест:** переключиться между табами быстро:
1. Открыть Crafting tab → видно 3 рецепта.
2. Переключиться на Warehouse.
3. Другой игрок (или host от своего лица) добавил новый рецепт на сервере → `CraftingClientState.OnSnapshotUpdated` дёргается.
4. Переключиться обратно на Crafting → **должен видеть 4 рецепта** (cache обновился, даже когда tab был неактивен).

**PASS критерии:** cache update ВСЕГДА (см. `project-c-ui-as-tab` SKILL.md).

---

## 6. Console-error whitelist (для теста)

Эти ошибки **МОЖНО** игнорировать в MVP:
- `RateLimited` (защита от спама, ожидаемо).
- `NotInZone` (игрок ушёл, ожидаемо).
- `BufferOverflow` (превышен лимит, ожидаемо).
- `ItemNotInWarehouse` (если игрок пытается положить из warehouse не в той зоне).

**НЕ игнорируем:** NRE, `CraftingWorld.Instance==null` при старте, `NetworkObject hash=0` warning.

---

## 7. Связь с другими тест-сценариями

- **Inventory v2 tests:** см. `Assets/_Project/Tests/EditMode/` (если есть) — Crafting не должен ломать Pickup/Drop.
- **MetaRequirement tests:** см. `docs/MetaRequirement/40_TESTING_GUIDE.md` — после `GrantKeyToClient` добавь тест «craft grants key».
- **Market tests:** см. `docs/Markets/30_VERIFICATION.md` — Crafting не должен ломать Market tick.

---

## 8. Exit-критерии MVP

✅ **Считаем MVP готовым**, когда:
- Compile чистый.
- 3 базовых рецепта работают end-to-end.
- 2 типа станций (верфь + стол) работают.
- 1 анти-гриф сценарий работает (не-owner не может Collect).
- 1 coop сценарий работает (2 игрока скидываются).
- 1 cancel сценарий работает (ресурсы возвращаются).
- 1 reconnect сценарий работает.
- 0 NRE в Console.
- 0 NetworkObject hash=0 warning.

Когда всё ✅ — пишем `99_CHANGELOG.md` запись «v0.0.1-crafting-mvp».
