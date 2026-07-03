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

*Документация ведётся агентом Aura. 2026-07-19*
