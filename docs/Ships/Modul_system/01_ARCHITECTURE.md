# Module System — Архитектура

**Дата:** 2026-07-19  
**Статус:** ✅ Phase 1 (Repair Manager + Runtime Install) реализован  
**Последнее изменение:** T-MOD04 — рефакторинг цен (все цены на NPC RepairManager)  

---

## 1. Обзор

Система модулей корабля позволяет менять характеристики корабля через установку модулей в предопределённые слоты. Модули добавляются разработчиком в редакторе (слоты — дочерние GameObject'ы на root корабля). Игрок может менять модули через ремонтного менеджера в доке за кредиты и ресурсы.

### 1.1 Ключевые компоненты

| Компонент | Тип | Назначение |
|---|---|---|
| `ShipModule` | ScriptableObject | Данные модуля: множители, совместимость, энергия |
| `ModuleSlot` | MonoBehaviour | Слот на корабле: тип слота, ссылка на установленный ShipModule |
| `ShipModuleManager` | MonoBehaviour | Менеджер слотов: InstallModule, RemoveModule, ReplaceModule, валидация |
| `ShipModuleServer` | NetworkBehaviour | Серверные RPC для установки/снятия в рантайме |
| `ModuleShopEntry` | ScriptableObject | Запись в каталоге: ShipModule + цена + ресурсы |
| `ModuleShopDatabase` | ScriptableObject | База каталога: список ModuleShopEntry |
| `ShipModuleCatalog` | static class | Статический реестр для lookup модулей по moduleId |
| `RepairManager` | MonoBehaviour | NPC в доке: открывает RepairManagerWindow |
| `RepairManagerWindow` | UIDocument | UI Toolkit окно ремонтного менеджера |

### 1.2 Поток установки модуля

```
Игрок подходит к RepairManager NPC → жмёт E
  → RepairManager.Interact()
  → RepairManagerWindow.Show(database)
  → Игрок выбирает корабль (dropdown) → слот → модуль → «Установить»
  → RepairManagerWindow.OnInstallClicked()
  → ShipModuleServer.RequestInstallModuleRpc(keyInstanceId, slotName, moduleId)
  → [Server] Валидация: владение ключом → IsDocked → совместимость → энергия
  → [Server] ShipModuleManager.ReplaceModule(slot, module)
  → [All Clients] OnModuleChangedClientRpc → обновление UI
```

---

## 2. Структура файлов

```
Assets/_Project/
├── Scripts/Ship/
│   ├── ShipModule.cs              # ScriptableObject: данные модуля
│   ├── ModuleSlot.cs              # MonoBehaviour: слот модуля
│   ├── ShipModuleManager.cs       # MonoBehaviour: управление слотами
│   ├── ShipModuleServer.cs        # NetworkBehaviour: RPC install/remove
│   ├── ShipModuleCatalog.cs       # static: реестр модулей
│   ├── ModuleShopEntry.cs         # ScriptableObject: модуль + цена
│   ├── ModuleShopDatabase.cs      # ScriptableObject: список ShopEntry
│   ├── RepairManager.cs           # MonoBehaviour: NPC в доке
│   └── UI/
│       └── RepairManagerWindow.cs # UI Toolkit окно
├── Data/Modules/
│   ├── MODULE_*.asset             # 8 ShipModule ScriptableObject'ов
│   ├── ShopEntry_*.asset          # 8 ModuleShopEntry (после создания)
│   └── ModuleShopDatabase.asset   # База каталога
└── Resources/UI/
    └── RepairManagerWindow.uxml   # UXML разметка окна
```

---

## 3. ShipModule (ScriptableObject)

Свойства:
- `moduleId` — уникальный ID (например `MODULE_YAW_ENH`)
- `displayName` — читаемое имя
- `type` — ModuleType: Propulsion / Utility / Special
- `tier` — 1-4
- Множители: thrustMultiplier, yawMultiplier, pitchMultiplier, rollMultiplier, liftMultiplier
- Модификаторы: maxSpeedModifier, windExposureModifier
- Cargo: cargoSlotsBonus, cargoWeightBonus, cargoVolumeBonus, cargoPenaltyReduction
- Совместимость: compatibleClasses, requiredModules, incompatibleModules
- Энергия: powerConsumption
- Мезий: isMeziyModule, meziyForce/Duration/Cooldown/FuelCost

---

## 4. ShipModuleServer (NetworkBehaviour)

Размещается на корне корабля (рядом с ShipController, ShipModuleManager).

### RPC:
- `RequestInstallModuleRpc(int keyInstanceId, string slotName, string moduleId)` — сервер
- `RequestRemoveModuleRpc(int keyInstanceId, string slotName)` — сервер
- `OnModuleChangedClientRpc(string slotName, string moduleId, bool isInstall)` — всем клиентам

### Валидация на сервере:
1. `KeyRodInstanceWorld.IsOwnerOfInstance(clientId, keyInstanceId)` — владение ключом
2. `instance.registeredShipId == this.NetworkObjectId` — ключ от этого корабля
3. `ShipController.IsDocked` — корабль в доке
4. `ShipModuleManager.InstallModule` — совместимость + энергия

### Inspector:
- `_shopDatabase` — ссылка на ModuleShopDatabase для lookup модулей на клиенте

---

## 5. RepairManagerWindow (UI Toolkit)

Паттерн: CommPanelWindow (UIDocument singleton).

### UXML структура:
- `repair-ship-dropdown-container` — CustomDropdown выбора корабля
- `repair-ship-class`, `repair-ship-power` — инфо о корабле
- `repair-slots-container` — список слотов с кнопками «Выбрать»/«Снять»
- `repair-modules-container` — список совместимых модулей с кнопкой «Установить»
- `repair-credits-label`, `repair-status-label` — footer

### Key-фильтрация кораблей:
Использует `InventoryWorld.GetMyShips(clientId)` → ShipController → dropdown.
Fallback: `KeyRodInstanceWorld.GetInstancesForPlayer(clientId)`.

---

## 6. Безопасность

- **Владение**: сервер проверяет KeyRodInstanceWorld.IsOwnerOfInstance перед каждой операцией
- **Док**: установка/снятие только когда корабль пристыкован (ShipController.IsDocked)
- **Совместимость**: ShipModuleManager валидирует класс корабля, энергию, несовместимости
- **Стоимость**: клиент знает цену из ModuleShopEntry, сервер авторитативно решает

---

*Документация ведётся агентом Aura. 2026-07-19*
