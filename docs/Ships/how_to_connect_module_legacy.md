Инструкция по настройке модулей (для будущих кораблей)
Шаг 1: Подготовка компонентов на корне

GameObject корабля должен иметь: ShipController (NetworkBehaviour), Rigidbody, NetworkObject, BoxCollider (корпус)
Add Component → ShipModuleManager (MonoBehaviour) — задать availablePower
Add Component → MeziyModuleActivator (MonoBehaviour)
Add Component → ShipFuelSystem (MonoBehaviour)
Шаг 2: Привязки в ShipController В Inspector ShipController:

Module Manager → drag ShipModuleManager с этого же объекта
Meziy Activator → drag MeziyModuleActivator с этого же объекта
Fuel System → drag ShipFuelSystem с этого же объекта
Шаг 3: Привязка в MeziyModuleActivator В Inspector MeziyModuleActivator:

Module Manager → drag ShipModuleManager с этого же объекта
Шаг 4: Создание слотов Под кораблем (или как child Ship_Root):

Создать пустой GameObject (например, Slot_EngineLeft)
Add Component → ModuleSlot
В Inspector:
Slot Type = Propulsion / Utility / Special (должен совпадать с Type устанавливаемого ShipModule)
Installed Module → drag ShipModule (ScriptableObject) из Assets/_Project/Data/Modules/
Если модуль несовместим с классом корабля или превышена энергия — installedModule обнулится (с warning в console)
Альтернативно: можно поставить ModuleSlot без модуля и установить в рантайме через ShipModuleManager.InstallModule(slot, module)
Шаг 5: Инициализация (однократно при старте)

ShipController.Awake() автоматически вызывает InitializeFuelSystem() + InitializeMeziySystem()
ShipModuleManager.Initialize(shipClass) нужно вызвать вручную после установки всех слотов (обычно в Start)
MeziyModuleActivator.Initialize() автоматически в ShipController.Awake
Шаг 6: Замена ShipModule в слоте (рантайм)

Code
· csharp
ShipModuleManager mgr = shipGo.GetComponent<ShipModuleManager>();
mgr.InstallModule(slot, newModule); // false если несовместим
mgr.RemoveModule(slot);