# Equipment Visual System — связанные баг-тикеты

> **Статус:** TICKET-EV-002 ✅ ЗАКРЫТ (2026-06-29). Остальные — pending.
> **Дата:** 2026-06-29
> **Контекст:** обнаружены при Phase 1+2 verification.

TICKET-EV-002 был **главным блокером** equip UI из инвентаря — один клик надевал другой предмет / давал "Слишком быстро" из rate limit. Закрыт через unregister-via-userData fix. Остальные тикеты документированы для будущих сессий.

---

## TICKET-EV-001: Reflection-based RPC call в InventoryTab (DOWNGRADED, не blocker)

**Severity:** ~~blocker для всего equip-flow из UI~~ → **Не блокер.** RPC работает через reflection (server отвечает — стек `__rpc_handler_2247023513` показывает полный pipeline). EV-002 (double-callback) был настоящим blocker'ом.
**Subsystem:** `ProjectC.UI.Client.InventoryTab` (T-P19)
**Файл:** `Assets/_Project/Scripts/UI/Client/CharacterWindow/InventoryTab.cs:786`
**Обнаружено:** 2026-06-29 при тесте "надеть Wooden Sword из инвентаря"

### Статус
Открыто: думали что RPC не работает (поэтому тикет и был создан).
Закрыто частично: 2026-06-29 — после фикса EV-002, equip UI стал работать, RPC проходит успешно, server отвечает. Reflection call работает, просто не оптимально.

### Почему reflection call работает (а не блокирует)
Stack trace пользователя от 2026-06-29 показывает полный NGO pipeline:
```
ProjectC.UI.Client.InventoryTab:CallEquipRpc (line 786)
System.Reflection.MethodBase:Invoke (object,object[])
ProjectC.Equipment.EquipmentServer:RequestEquipRpc (line 266)
ProjectC.Equipment.EquipmentServer:__rpc_handler_2247023513
Unity.Netcode.RpcMessageHelpers:Handle
```

NGO 2.x IL-post-processing **всё равно** генерирует `__rpc_***` stub для методов с `[Rpc]`, и `MethodBase.Invoke` идёт через этот stub. Reflection call на `[Rpc]` метод работает в NGO 2.x. (Это НЕ `[ServerRpc]`/`[ClientRpc]` legacy — `[Rpc(SendTo.Server)]` с explicit generation работает через reflection.)

### Оставшиеся проблемы (для будущего, не urgent)
- **Reflection overhead** в hot path (UI click → reflection → invoke). Не критично для UI.
- **Fragile**: если кто-то рефакторит `EquipmentServer` (меняет сигнатуру RPC) — UI сломается без compile error. Compile-time safety отсутствует.
- **DI pollution**: `InventoryTab` тесно связан с `EquipmentServer` через reflection, не через DI.

### Рекомендация (отложено)
Если будет время — заменить reflection на прямой `EquipmentServer.Instance.RequestEquipRpc(dbItemId, slot)`. Это требует:
1. `EquipmentServer.Instance` экспортировать как `public static NetworkBehaviour` (сейчас `private set`).
2. Убрать `System.Type.GetType(...)` + `MethodInfo.Invoke`.

Не блокер, можно отложить.

---

## TICKET-EV-002: UI Toolkit double-callback на [НАДЕТЬ] / [СНЯТЬ] ✅ CLOSED 2026-06-29

**Severity:** ~~блокирует удобное использование~~ → **CLOSED**
**Subsystem:** `ProjectC.UI.Client.InventoryTab` (T-P19)
**Файл:** `Assets/_Project/Scripts/UI/Client/CharacterWindow/InventoryTab.cs:630-654`
**Обнаружено:** 2026-06-29 — "жму на один объект, надевается другой"
**Закрыто:** 2026-06-29 — userData-based callback storage (см. fix ниже)

### Симптом
- Клик на кнопку [НАДЕТЬ] в строке инвентаря иногда вызывает equip **другого** предмета.
- Дополнительный симптом: equip отрабатывает, но equipment `EquipResult = Denied("Слишком быстро")` из-за rate limit (один клик → N callbacks → N RPC за короткое время).

### Root cause (подтверждённый)
```csharp
// OLD InventoryTab.cs:633
equipBtn.UnregisterCallback<ClickEvent>(OnInventoryEquipBtnClick);  // ← placeholder, не реальный callback
equipBtn.RegisterCallback<ClickEvent>(evt => { ... });  // ← новый lambda каждый раз
```

Строка 633 unregister'ит **placeholder** метод (`OnInventoryEquipBtnClick` — пустой метод на line 650). Реальный lambda-callback, зарегистрированный на предыдущем refresh, **никогда не удаляется**.

При каждом `BindInventoryRow` (вызывается на каждый refresh ListView) → ещё один callback добавляется → кнопка получает N штук handlers → один click вызывает N handlers → пользователь видит хаос или rate limit.

### Fix (применён)
```csharp
// NEW InventoryTab.cs:640-654 — userData-based callback storage
var prevCb = equipBtn.userData as UnityEngine.UIElements.EventCallback<UnityEngine.UIElements.ClickEvent>;
if (prevCb != null)
{
    equipBtn.UnregisterCallback<UnityEngine.UIElements.ClickEvent>(prevCb);
}
UnityEngine.UIElements.EventCallback<UnityEngine.UIElements.ClickEvent> newCb = evt =>
{
    if (capturedIsEquipped)
        OnUnequipFromInventoryClicked(capturedItemId, capturedDisplayName);
    else
        OnEquipFromInventoryClicked(capturedItemId, capturedDisplayName);
    evt.StopPropagation();
};
equipBtn.userData = newCb;
equipBtn.RegisterCallback<UnityEngine.UIElements.ClickEvent>(newCb);
```

Ключевые моменты:
1. Храним callback в `userData` (стандартное поле VisualElement, любое значение).
2. Перед register нового — unregister старого через `userData as EventCallback<>`.
3. Используем `EventCallback<ClickEvent>` (UnityEngine.UIElements), **не** `System.Action<ClickEvent>` (несовместимы).

### Verification
- Compile clean (0 errors после fix).
- Visual indent нормализован через execute_code (Roslyn не делает auto-format).
- Play Mode smoke test pending (ты тестируешь в Editor).

---

## TICKET-EV-003: PickupItem "E не подбирает" в WorldScene_0_0 (диагностика нужна)

**Severity:** блокирует дроп-флоу с реальным мешем (нужно для Phase 1.4 acceptance test)
**Subsystem:** `PickC.Items.PickupItem` + Inventory pickup pipeline
**Файл:** `Assets/_Project/Scripts/Core/PickupItem.cs:106-151` (`Collect()` method)
**Обнаружено:** 2026-06-29 — Sword в WorldScene_0_0 с назначенным visualPrefab, виден на земле, крутится, но E не реагирует.

### Симптом
- Sword меш отображается (`EnsureVisualFromItemData` отработала — back-compat или auto-spawn).
- Visual bobbing работает (значит `Update()` крутится → `PickupItem` активен).
- E не подбирает. Никакой реакции.

### Что уже проверено
- **Мой код:** `EnsureVisualFromItemData` НЕ отключает родительский `SphereCollider` (line 60-63 — наоборот, создаёт trigger если нет). Smoke test (выполнен в edit-mode 2026-06-29) подтверждает: back-compat работает, auto-spawn работает.
- Console: нет ошибок связанных с pickup.

### Root cause candidates (требуется диагностика)

1. **`InteractableManager.RegisterPickup` race condition.** `OnTriggerEnter` (line 76-83) регистрирует pickup в `InteractableManager`. Если игрок не входит в триггер (например, sphere radius маленький, или игрок стоит далеко), `RegisterPickup` не вызывается → `NetworkPlayer` не получает ссылку → E не работает.

2. **Pickup зарегистрирован, но `NetworkPlayer._nearestPickup` уже занят другим pickup'ом.** В `NetworkPlayer` есть поле `_nearestPickup` — если рядом есть ещё один pickup, ближайший может "перехватить" фокус.

3. **E не доходит до PickupItem.Collect()** — input перехватывается UI окном (CharacterWindow открыт и блокирует input).

4. **Server-spawn pickup с itemId == 0** — `InventoryServer.RequestDropRpc` ставит itemId после Instantiate. До этого `PickupItem.itemId == 0`, fallback через `GetOrRegisterItemId(itemData)` — может не сработать.

5. **Phase 1.4 сломала `itemData`** — `EnsureVisualFromItemData` использует `itemData.itemName`. Если `itemData == null` в Start, мы возвращаемся (line 80), ничего не спавним — но pickup должен работать. Возможно, **другая** проблема.

### Диагностика (после Phase 2)
- Добавить `Debug.Log` в `PickupItem.Start()`: залогировать наличие child renderer'ов + `itemData != null`.
- Добавить `Debug.Log` в `PickupItem.Collect()`: каждый шаг (itemId resolve, client state, RPC).
- Проверить `InteractableManager.RegisteredPickups` count в runtime через reflection.
- Проверить NetworkPlayer._nearestPickup в момент нажатия E.

### Recommendation
Добавить временный diagnostic logging в PickupItem, воспроизвести баг, починить по диагностике. **Не блокирует Phase 2** — Phase 2 не зависит от pickup-флоу (мы делаем Equip→Visual, не Drop→Pickup→Equip).

---

## План исправления

| Приоритет | Тикет | Что делать | Статус |
|---|---|---|---|
| 🟢 P1 | TICKET-EV-002 | `userData`-based callback storage в `BindInventoryRow`. | ✅ **CLOSED 2026-06-29** |
| 🟡 P3 | TICKET-EV-001 | Reflection → direct call. **Downgraded** после диагностики: reflection работает. Только косметика. | 📋 Deferred (не urgent) |
| 🟡 P2 | TICKET-EV-003 | Диагностический logging → root cause → фикс. | 📋 Pending |

TICKET-EV-002 был истинной причиной "equip не работает" — один клик = N callbacks = N RequestEquipRpc → rate limit "Слишком быстро".

---

## Связанные документы

| Документ | Назначение |
|---|---|
| `docs/Character/EquipmentVisual/00_DESIGN.md` | Дизайн Equipment Visual System |
| `docs/Character/EquipmentVisual/03_PHASES.md` | План реализации (Phase 1 done → Phase 2 in progress) |
| `docs/dev/INVENTORY_V2_REFACTOR.md` | Phase 3 pickup pipeline (откуда растёт TICKET-EV-003) |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow/InventoryTab.cs:600-800` | Содержит TICKET-EV-001 и TICKET-EV-002 |