# Repair Manager — Гайд по настройке

**Дата:** 2026-07-19  

---

## 1. Быстрый старт

### 1.1 Создать ModuleShopEntry для каждого модуля

В Project window: `Assets/_Project/Data/Modules/` → ПКМ → Create → ProjectC → Ship → Module Shop Entry.

Для каждого из 8 модулей:
1. `module` — перетащить соответствующий ShipModule (.asset)
2. `costCredits` — цена в кредитах
3. `requiredResources` — массив ресурсов (itemId + amount), может быть пустым

Или запустить скрипт генерации (см. ниже).

### 1.2 Создать ModuleShopDatabase

В `Assets/_Project/Data/Modules/` → ПКМ → Create → ProjectC → Ship → Module Shop Database.

Перетащить все созданные ModuleShopEntry в список `entries`.

### 1.3 Создать RepairManagerWindow GameObject

В BootstrapScene или DontDestroyOnLoad:
1. Создать пустой GameObject → назвать `[RepairManagerWindow]`
2. Add Component → `UIDocument`
3. Add Component → `RepairManagerWindow`
4. В инспекторе RepairManagerWindow:
   - `Shop Database` — перетащить ModuleShopDatabase.asset
5. В инспекторе UIDocument:
   - `Panel Settings` — MarketPanelSettings (или другой существующий)
   - `Source Asset` — RepairManagerWindow (UXML)

### 1.4 Добавить ShipModuleServer на корабли

На каждый корабль (Ship_Root):
1. Add Component → `ShipModuleServer`
2. `Shop Database` — перетащить ModuleShopDatabase.asset

### 1.5 Создать RepairManager NPC

В сцене дока:
1. Создать GameObject → назвать `[RepairManager_NPC]`
2. Add Component → `RepairManager`
3. `Shop Database` — перетащить ModuleShopDatabase.asset
4. Добавить коллайдер (SphereCollider, радиус 2-3)
5. Настроить Interactable (если используется InteractableManager) ИЛИ добавить свой обработчик E-key

---

## 2. Автогенерация ShopEntry через execute_code

```csharp
// Запустить в Unity Editor через Window → MCP For Unity → Execute Code
using UnityEditor;
using UnityEngine;

const string dir = "Assets/_Project/Data/Modules";
var prices = new (string id, int cost)[] {
    ("MODULE_LIFT_ENH", 500), ("MODULE_PITCH_ENH", 500),
    ("MODULE_YAW_ENH", 500), ("MODULE_ROLL", 800),
    ("MODULE_MEZIY_PITCH", 1200), ("MODULE_MEZIY_ROLL", 1200),
    ("MODULE_MEZIY_YAW", 1500), ("MODULE_MEZIY_THRUST", 2000),
};

foreach (var (id, cost) in prices)
{
    var mod = AssetDatabase.LoadAssetAtPath<ProjectC.Ship.ShipModule>($"{dir}/{id}.asset");
    if (mod == null) continue;

    var entry = ScriptableObject.CreateInstance<ProjectC.Ship.ModuleShopEntry>();
    entry.module = mod;
    entry.costCredits = cost;
    entry.requiredResources = new ProjectC.Ship.ResourceRequirement[0];

    AssetDatabase.CreateAsset(entry, $"{dir}/ShopEntry_{id}.asset");
}

// Затем: создать ModuleShopDatabase и перетащить все ShopEntry_*.asset в entries
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
```

---

## 3. Цены и ресурсы (рекомендуемые)

| Модуль | Цена (кр.) | Ресурсы |
|---|---|---|
| MODULE_LIFT_ENH | 500 | — |
| MODULE_PITCH_ENH | 500 | — |
| MODULE_YAW_ENH | 500 | — |
| MODULE_ROLL | 800 | GyroStabilizer ×2 |
| MODULE_MEZIY_PITCH | 1200 | MeziyCrystal ×5 |
| MODULE_MEZIY_ROLL | 1200 | MeziyCrystal ×5 |
| MODULE_MEZIY_YAW | 1500 | MeziyCrystal ×6 |
| MODULE_MEZIY_THRUST | 2000 | MeziyCrystal ×8, TitaniumAlloy ×3 |

---

## 4. Верификация

1. Открыть CharacterWindow (P) → вкладка КОРАБЛЬ → выбрать корабль → кнопка «🛠 Установить модуль»
2. Открывается RepairManagerWindow → выбрать слот → выбрать модуль → «Установить»
3. Проверить: модуль появился в MyShipsTab, на сервере затраты списаны
4. Проверить: несовместимый модуль не предлагается, нет энергии → кнопка заблокирована
5. Проверить: без ключа в инвентаре → корабль не появляется в dropdown

---

## 5. Ship Observation Camera (2026-07-19)

При открытии RepairManagerWindow камера переключается на наблюдение выбранного корабля.

### 5.1 Как работает

| Событие | Поведение |
|---------|-----------|
| Открытие окна (E на NPC) | UI-панель слева, камера на игроке (без изменений) |
| Выбор корабля в дропдауне | Камера «улетает» от персонажа и фиксируется на корабле (угол ~45° сверху-сбоку) |
| Стрелки справа (▲▼◀▶) | Вращение камеры вокруг корабля, ориджин — корабль, камера всегда смотрит на него |
| Закрытие окна (Esc / ✕) | Камера возвращается к персонажу, ThirdPersonCamera активна |

### 5.2 Архитектура

```
RepairManagerWindow (UIDocument)
  └── [ShipObservationCamera] (GameObject, создаётся в Awake)
        └── Camera (своя, disabled изначально)
```

- **`ShipObservationCamera`** (`Assets/_Project/Scripts/Ship/UI/ShipObservationCamera.cs`) — отдельная камера, не зависит от `ThirdPersonCamera`.
  - `FlyToShip(Transform ship, Camera playerCam)` — отключает камеру игрока, включает свою.
  - `ReturnToPlayer()` — возвращает управление камере игрока.
  - `Rotate(yawDelta, pitchDelta)` — орбитальное вращение.
  - Аудиолистенер **не создаёт** — остаётся на камере игрока (избегает спама «2 audio listeners»).

- **`RepairManagerWindow`** — интеграция:
  - `Awake()` — создаёт `ShipObservationCamera` дочерним объектом.
  - `SelectShip()` → `_obsCamera.FlyToShip(sc.transform, _playerCam)`.
  - `SetOpen(false)` → `_obsCamera.ReturnToPlayer()`.
  - `Update()` → `HandleCameraArrowHeld()` — зажатие стрелок вращает камеру.

### 5.3 UI-вёрстка

- `.repair-root`: `align-items: flex-start` (панель слева), `justify-content: center` (вертикальный центр).
- `.repair-panel`: `width: 580px`, `margin-left: 40px`.
- `.camera-arrows`: `position: absolute; right: 24px; top: 50%` — вне потока, не влияет на панель.
- Кнопки-стрелки (`cam-arrow-btn`): 48×48px, полупрозрачный фон, hover/active эффекты.

### 5.4 Файлы

| Файл | Роль |
|------|------|
| `Assets/_Project/Scripts/Ship/UI/ShipObservationCamera.cs` | Новая камера наблюдения |
| `Assets/_Project/Scripts/Ship/UI/RepairManagerWindow.cs` | Интеграция камеры + стрелок |
| `Assets/_Project/Resources/UI/RepairManagerWindow.uxml` | Блок `camera-arrows` с кнопками |
| `Assets/_Project/Resources/UI/RepairManagerWindow.uss` | Левый layout + стили стрелок |

---

## 6. Ship Recall — вызов корабля на пад (2026-07-22)

### 6.1 Описание

Игрок может вызвать свой корабль на ближайший свободный посадочный пад через RepairManagerWindow.
Корабль мгновенно телепортируется, снимается с freeze, списывается стоимость в кредитах.

### 6.2 Как работает

| Шаг | Действие |
|-----|----------|
| 1 | Игрок подходит к RepairManager NPC в доке → E |
| 2 | В окне выбирает свой корабль в дропдауне |
| 3 | Справа от дропдауна — кнопка «🚁 Вызвать» и цена |
| 4 | Клиент ищет все DockingPadTriggerBox → фильтрует свободные → ближайший к игроку |
| 5 | `RecallShipToPadServerRpc(padPosition, cost)` → сервер |
| 6 | Сервер: списывает кредиты через `TradeWorld.TryModifyCredits`, отстыковывает если нужно, телепортирует, снимает `_frozenByNoPilot` |

### 6.3 Архитектура

```
RepairManagerWindow (клиент)
  ├── Дропдаун выбора корабля + кнопка «🚁 Вызвать» + цена
  ├── OnRecallShipClicked()
  │     ├── Проверка credits ≥ _shipRecallCost
  │     ├── FindObjectsByType<DockingPadTriggerBox> → filter IsShipInside==false → nearest
  │     └── sc.RecallShipToPadServerRpc(nearestPad.position, cost)
  │
ShipController.RecallShipToPadServerRpc (сервер)
  ├── TradeWorld.TryModifyCredits(clientId, -cost)
  ├── ExitDocked() (если пристыкован)
  ├── rb.position = padPosition, velocity = zero
  └── _frozenByNoPilot = false
```

### 6.4 Настройка

| Параметр | Где | По умолчанию |
|----------|-----|-------------|
| `_shipRecallCost` | `RepairManager` (инспектор) | 500 кр. |

### 6.5 Файлы

| Файл | Роль |
|------|------|
| `Assets/_Project/Scripts/Ship/UI/RepairManagerWindow.cs` | Кнопка + логика вызова |
| `Assets/_Project/Scripts/Ship/RepairManager.cs` | `_shipRecallCost` → `Show()` |
| `Assets/_Project/Scripts/Player/ShipController.cs` | `RecallShipToPadServerRpc` |
| `Assets/_Project/Resources/UI/RepairManagerWindow.uxml` | `repair-recall-section` в строке с дропдауном |
| `Assets/_Project/Resources/UI/RepairManagerWindow.uss` | `.repair-ship-selector-row`, `.repair-recall-row`, `.repair-recall-cost` |

### 6.6 Верификация

1. Открыть RepairManagerWindow (E на NPC в доке)
2. Выбрать корабль в дропдауне — справа кнопка «🚁 Вызвать» с ценой
3. Нажать «🚁 Вызвать» — корабль должен телепортироваться на ближайший свободный пад
4. Проверить: кредиты списаны, корабль не в freeze, может взлететь

---

*Документация ведётся агентом Aura. 2026-07-22*

