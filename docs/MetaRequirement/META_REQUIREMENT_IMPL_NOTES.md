# MetaRequirement — Заметки реализации (Этап 1)

**Документ:** рабочие заметки по миграции `ShipKey` → `MetaRequirement` + первый не-корабельный тест-кейс.
**Дата:** 2026-06-06
**Связанные документы:**
- `docs/MetaRequirement/00_OVERVIEW.md` — дизайн
- `docs/MetaRequirement/RECIPES.md` — рецепты
- `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` — migration guide
- `docs/Ships/Key-subsystem/00_OVERVIEW.md` — старая подсистема
- `docs/Ships/Key-subsystem/KNOWN_ISSUES.md` — история R2-SHIP-KEY-001

---

## 1. Скоуп этой сессии

1. **Реализовать MetaRequirement Этап 1** (обобщение Ship Key) с backward-compat алиасами.
2. **Сделать тестовый не-корабельный кейс** — три цветных блока с анимацией, ключ-в-инвентаре открывает, без ключа — toast.
3. **НЕ трогаем** старые `ShipKey*` компоненты в сценах — они продолжат работать через алиасы.

---

## 2. Структура файлов (что создаём)

```
Assets/_Project/Scripts/MetaRequirement/
├── RequirementLogic.cs        # enum All / Any / AtLeastN
├── ProgressInfo.cs            # struct для UI tooltip
├── MetaRequirement.cs         # NetworkBehaviour (компонент на Interactable)
├── MetaRequirementRegistry.cs # NetworkBehaviour hub (server-side)
├── MetaRequirementClientState.cs  # MonoBehaviour singleton (client projection)
└── MetaRequirementDto.cs      # INetworkSerializable DTO

Assets/_Project/UI/Resources/UI/
└── MetaRequirementPanelSettings.asset   # копия ShipKeyPanelSettings (dedicated)

Assets/_Project/Resources/Items/
├── Item_Key_Blue.asset        # ключ от BlueLockBox
├── Item_Key_Red.asset         # ключ от RedLockBox
└── Item_Key_Green.asset       # ключ от GreenLockBox
```

**Алиасы (в старых файлах `Assets/_Project/Scripts/Ship/Key/`):**
- `ShipKeyBinding : MetaRequirement {}` (empty subclass)
- `ShipKeyServer : MetaRequirementRegistry {}` (empty subclass)
- `ShipKeyClientState : MetaRequirementClientState {}` (empty subclass)
- `ShipKeyToast : MetaRequirementToast {}` (empty subclass) — см. §6

---

## 3. Скоуп тест-кейса (НЕ-корабль)

Три блока `LockBox_{Blue,Red,Green}` (по центру WorldScene_0_0, рядом с координатами ключей кораблей — на расстоянии ~5 м от `[Ship_Key_Container]`):

| Объект | Цвет | Анимация | Ключ |
|---|---|---|---|
| `[LockBox_Blue]` | ярко-синий, emissive | лёгкое свечение, ramp-цвет ярче, scale-up 1.0 → 1.2 (0.5s) при открытии | `Item_Key_Blue` |
| `[LockBox_Red]` | ярко-красный | то же | `Item_Key_Red` |
| `[LockBox_Green]` | ярко-зелёный | то же | `Item_Key_Green` |

**Поведение игрока:**
1. Подходим к `[Key_Blue_Pickup]` (1 м от блока, цвет ключа = цвету блока) → E → ключ в инвентаре.
2. F (или E — единая кнопка взаимодействия с Interactable) на `[LockBox_Blue]`:
   - **Есть ключ** → анимация открытия (один раз), emit `OnOpened` event на сервере, `MetaRequirementRegistry` помечает как "использовано" (для MVP — без `consumeOnUse`).
   - **Нет ключа** → toast внизу экрана «Нужен ключ для [Синий Сундук]» через `MetaRequirementToast`.

**F или E:** см. §5 (решающий вопрос: единый entry point).

---

## 4. Сетевой flow

```
[Client] F/E нажимаем
    ↓
[Client] NetworkPlayer → FindNearestInteractable → нашёл GameObject с MetaRequirement
    ↓
[Client] NetworkPlayer.RequestCanUseMetaRequirementRpc(netId) → Server
    ↓
[Server] MetaRequirementRegistry.RequestCanUseRpc → CanPlayerUse(clientId, netId)
    ↓
[Server] Если allowed=True → посылает ответ OK
    ↓
[Server] Если allowed=False → посылает ответ DENY + reason
    ↓
[Client] NetworkPlayer.ReceiveMetaRequirementResponseTargetRpc → MetaRequirementClientState
    ↓
[Client] MetaRequirementClientState: if allowed → вызывает IInteractable.Open(); else → OnAccessDenied event → MetaRequirementToast
```

**LockBox.Open()** — запускает локальную анимацию. Не требует server-side logic в MVP (открытие визуальное).

---

## 5. Открытые вопросы / решения

### 5.1 F vs E для блоков

**Решение:** для блоков используем **E** (как сундуки/chests). F остаётся за ship boarding. В `NetworkPlayer.Update` уже есть ветка для E → сначала chest, потом pickup, потом market. Добавляем туда же: если рядом есть `LockBox` с `MetaRequirement` — `RequestCanUseMetaRequirement`.

**Реализация:** отдельная проверка в E-блоке ДО chest/pickup — если `InteractableManager.FindNearestLockBox(position, range)` → RequestCanUse.

### 5.2 Animation — `OnOpened` event

**Решение:** у `MetaRequirement` есть C# event `event Action<MetaRequirement> OnServerAllowed` (вызывается на сервере при успешном `CanPlayerUse`) и клиентский `event Action<MetaRequirement> OnClientAllowed` (через client state projection). `LockBox` подписывается на `OnClientAllowed` → запускает анимацию.

**Проще для MVP:** `LockBox` сам подписан на `MetaRequirementClientState.OnUseAllowed(netId)` → `if (netId == myNetId) → AnimateOpen()`. Делаем так.

### 5.3 _consumeOnUse

**Решение:** в MVP — НЕ реализуем потребление. Добавим поле в `MetaRequirement`, но в тесте оставим `false`. TODO — отдельный тикет.

### 5.4 Тест на дроп ключа после открытия

**Решение:** НЕ делаем. Ключ остаётся в инвентаре, можно открыть блок повторно (анимация проиграется заново). Это упрощает тест.

### 5.5 Идентификация клиента-владельца

**Решение:** `RequestCanUseRpc(ulong netId, RpcParams)` — `InvokePermission = Owner`. Сервер берёт `SenderClientId` из rpcParams. Тот же паттерн что и в `ShipKeyServer.RequestCanBoardRpc`.

### 5.6 Тест на двойное E

**Решение:** race protection в `NetworkPlayer` через `_lastCanUseRequestTime` (по аналогии с `_lastCanBoardRequestTime`). 1.5 сек timeout.

### 5.7 Singleton: MetaRequirementClientState в BootstrapScene или root?

**Решение:** auto-spawn через `NetworkManagerController.CreateMetaRequirementClientState()` (как `ShipKeyClientState` сейчас). В BootstrapScene НЕ кладём — будет дубль. Если уже есть scene-placed в старой сцене — заменяется root-инстансом (как FIX 2026-06-04).

---

## 6. Алиасы — почему пустые subclass, а не partial?

**Решение:** пустые `class ShipKeyBinding : MetaRequirement { }` (subclass) в старых файлах. `.meta`-GUID сохраняется → scene-prefab references работают. Через 1-2 релиза удаляем.

**ShipKeyToast** — алиас сложнее, потому что `ShipKeyToast` подписывается на `ShipKeyClientState.OnBoardDenied` (старый event). Новый `MetaRequirementToast` подписывается на `MetaRequirementClientState.OnAccessDenied`. **Решение:** ShipKeyToast становится пустым subclass `MetaRequirementToast` БЕЗ логики подписки (потеряет совместимость). Если в BootstrapScene уже есть `[ShipKeyToast]` — убираем его и кладём `[MetaRequirementToast]`.

**Проверим** при имплементации — есть ли в BootstrapScene `[ShipKeyToast]` сейчас.

---

## 7. Сцены: что меняем

### BootstrapScene
- ❌ Убираем `[ShipKeyServer]` GameObject (если есть) → кладём `[MetaRequirementRegistry]`.
- ❌ Убираем `[ShipKeyToast]` (если есть) → кладём `[MetaRequirementToast]` с PanelSettings `MetaRequirementPanelSettings.asset`.
- ✅ Остальное не трогаем.

### WorldScene_0_0
- ✅ Корабли с `ShipKeyBinding` — оставляем как есть (работают через алиас).
- 🆕 Добавляем 3 `LockBox` GameObject'а с `NetworkObject` + `MetaRequirement` + 3 Pickup-ключа рядом.

---

## 8. Чек-лист теста для пользователя

1. Запустить Editor → BootstrapScene → Play → StartHost.
2. Подобрать `[Key_Blue_Pickup]` (E).
3. F или E на `[LockBox_Blue]`:
   - Должна проиграться анимация свечения + scale-up.
   - В Console: `[MetaRequirementRegistry] CanPlayerUse: client=0, obj=N, allowed=True`.
4. Перезапустить Editor без ключа. F на `[LockBox_Blue]`:
   - Toast внизу: "Нужен ключ для Синий Сундук" (или похожее).
   - В Console: `[MetaRequirementRegistry] CanPlayerUse: ... allowed=False reason='...'`.
5. Повторить для Red и Green.
6. F на `Ship_Light` (с ключом от Light) → должен по-прежнему садиться (regression check на алиас).

---

## 9. Что НЕ делаем в этой сессии

- Не удаляем алиасы ShipKey*.
- Не делаем _consumeOnUse.
- Не делаем каскады / Conditions / Multi-progress UI.
- Не пишем юнит-тесты (по AGENTS.md юзер сам тестит).
- Не правим docs/gdd/ и docs/WORLD_LORE_BOOK.md.
- Не коммитим (юзер сам git).
- Не делаем скриншоты (юзер сам).
