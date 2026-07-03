# T-MODUL: RepairManager — доковый менеджер модулей корабля

> **Дата:** 2026-07-01
> **Статус:** ✅ Готово (E-интеграция, UI Toolkit окно, продажа модулей)
> **Файлы:** `Assets/_Project/Scripts/Ship/UI/RepairManagerWindow.cs`, `Assets/_Project/Scripts/Ship/RepairManager.cs`, `Assets/_Project/Scripts/Ship/ShipModuleServer.cs`, `Assets/_Project/Resources/UI/RepairManagerWindow.*`

---

## Обзор

**RepairManager** — NPC в доке, открывающий UI Toolkit окно для установки и продажи модулей корабля. Игрок подходит к NPC → жмёт E → открывается окно ремонтного менеджера.

**Лор:** модули корабля — сложное оборудование. Установка возможна **только в доке/верфи** через менеджера, а не через CharacterWindow.

---

## Архитектура взаимодействия (E-key)

```
Игрок входит в триггер NPC
  → RepairManager.OnTriggerEnter → InteractableManager.RegisterRepairManager
  → Игрок жмёт E
  → NetworkPlayer.Update → TryInteractNearestRepairManager()
  → nearest.Interact() → RepairManagerWindow.Show(database)
```

### Затронутые файлы

| Файл | Изменение |
|---|---|
| `InteractableManager.cs` | Добавлен `_repairManagers` пул + `RegisterRepairManager` / `UnregisterRepairManager` / `FindNearestRepairManager` |
| `RepairManager.cs` | Реализован `IInteractable` + `OnTriggerEnter/Exit` для авторегистрации в InteractableManager |
| `NetworkPlayer.cs` | Добавлен `TryInteractNearestRepairManager()` в E-key handler (после MetaRequirement, до NPC-диалога) |

---

## RepairManagerWindow — UI Toolkit окно

### Файлы

| Файл | Назначение |
|---|---|
| `RepairManagerWindow.cs` | MonoBehaviour-контроллер окна (канон `docs/UI/UI_TOOLKIT_GUIDE.md` §3) |
| `RepairManagerWindow.uxml` | Вёрстка (VisualElement-дерево) |
| `RepairManagerWindow.uss` | Стили с `!important` |

### Структура UXML

```
repair-root (fullscreen overlay, rgba-димминг)
  └── main-container (центрированная панель 680px, твёрдый фон)
        ├── repair-header (заголовок + кнопка ✕)
        ├── repair-ship-selector (дропдаун выбора корабля)
        ├── repair-ship-info (класс + энергия)
        ├── repair-slot-selector (дропдаун выбора слота)
        ├── repair-installed-row (установленный модуль + кнопка «Продать»)
        ├── repair-modules-section (flex-grow: 1 — весь оставшийся объём)
        │     └── repair-scroll → repair-modules-container
        └── repair-footer (кредиты + статус)
```

### Ключевые исправления в процессе

| # | Проблема | Фикс |
|---|---|---|
| 1 | `Clear() + CloneTree()` в `EnsureBuilt` | Убран; `_doc.rootVisualElement` загружается автоматически |
| 2 | Окно видно с главного меню | `SetOpen(false)` в `EnsureBuilt`; `pickingMode: Ignore` когда скрыто |
| 3 | Нет фона (прозрачный) | `main-container` с `background-color: rgb(18,28,40)` + border |
| 4 | ESC не закрывает | `Update()` → `escapeKey.wasPressedThisFrame` → `Hide()` |
| 5 | UIManager перехватывает ESC | Добавлен в `IsAnyExternalWindowOpen()` |
| 6 | Дропдаун глючный (свой inline) | Заменён на проверенный `ProjectC.UI.Client.CustomDropdown` (попап на panel.visualTree через `LocalToWorld/WorldToLocal`) |
| 7 | Слоты/модули 50/50 | Слоты → компактный дропдаун; модули → `flex-grow: 1` |
| 8 | Нет обновления после установки/продажи | `DelayedRefresh(0.5f)` + `RenderShip()` |
| 9 | Скролбар толстый | Тонкий голубой (6px, `rgba(100,160,220,0.4)`) как в CharacterWindow |

---

## Продажа модулей

**Было:** «Снять» — удаляло модуль без компенсации.

**Стало:** «💰 Продать (+X кр.)» — серверный RPC удаляет модуль и начисляет 50% стоимости через `TradeWorld`.

### Flow

```
RepairManagerWindow.OnSellClicked
  → ShipModuleServer.RequestSellModule(keyInstanceId, slotName, sellCredits)
  → RequestSellModuleRpc [Server]:
      1. Валидация: ключ, док, слот
      2. RemoveModule(slot)
      3. TradeWorld.Instance.Repository.TryModifyCredits(clientId, +sellCredits)
      4. NotifyClientSuccess + OnModuleChangedClientRpc
```

### Метод в `ShipModuleServer.cs`

```csharp
public void RequestSellModule(int keyInstanceId, string slotName, int sellCredits)
```

Цена продажи = `entry.costCredits / 2` (вычисляется на клиенте из `ModuleShopDatabase`).

---

## CharacterWindow — убрана кнопка «Установить модуль»

**Файл:** `MyShipsTab.cs` (стр. 377-380 удалены)

**Причина:** модули — сложное оборудование, установка только через докового менеджера (лор).

---

## Сводка изменённых файлов

```
Assets/_Project/Scripts/Ship/UI/RepairManagerWindow.cs  — новый (UI Toolkit окно)
Assets/_Project/Scripts/Ship/RepairManager.cs             — изменён (IInteractable + OnTrigger)
Assets/_Project/Scripts/Ship/ShipModuleServer.cs          — изменён (+RequestSellModuleRpc)
Assets/_Project/Scripts/Core/InteractableManager.cs       — изменён (+RepairManager pool)
Assets/_Project/Scripts/Player/NetworkPlayer.cs           — изменён (+TryInteractNearestRepairManager)
Assets/_Project/Scripts/UI/UIManager.cs                   — изменён (+IsAnyExternalWindowOpen)
Assets/_Project/Scripts/UI/Client/CharacterWindow/MyShipsTab.cs — изменён (−installBtn)
Assets/_Project/Resources/UI/RepairManagerWindow.uxml     — новый
Assets/_Project/Resources/UI/RepairManagerWindow.uss      — новый
```

---

## Зависимости

- `docs/UI/UI_TOOLKIT_GUIDE.md` — канонический шаблон UI Toolkit окна
- `docs/UI/CUSTOM_DROPDOWN_DESIGN.md` — документация CustomDropdown
- `docs/UI/BUGS_PHASE_2.md` — баги Esc-закрытия и ExecutionOrder
- `ShipModuleServer.cs` — серверные RPC для установки/продажи
- `InteractableManager.cs` — статический реестр interactable-объектов
- `ModuleShopDatabase.asset` — база модулей с ценами
