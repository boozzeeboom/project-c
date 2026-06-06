# Crafting System — Inspector Reference (поля SO)

> **Цикл:** Проектирование. Этот документ — **reference** для дизайнера/контент-мейкера при создании рецептов и станций в Editor.

---

## 1. `RecipeData` (ScriptableObject)

**Где создать:** `Project → Create → Project C → Crafting → Recipe`. Файл сохранить в `Assets/_Project/Resources/Crafting/Recipes/`.

### Поля

| Поле | Тип | Описание | Обязательно? | Default |
|------|-----|----------|--------------|---------|
| `displayName` | string | Человекочитаемое имя (UI, tooltip). | Да | (пусто) |
| `icon` | Sprite | Иконка для UI ListView / tooltip / world-label. | Нет | null |
| `description` | TextArea | Описание (lore, hints). | Нет | (пусто) |
| `category` | enum RecipeCategory | Категория (Module / Consumable / Ship / Material / Misc). Используется для фильтрации в UI. | Нет | Misc |
| `ingredients` | List<RecipeIngredient> | Что нужно положить в буфер. | Да | (пусто) |
| `outputs` | List<RecipeOutput> | Что выдаётся. **Минимум 1.** | Да | (пусто) |
| `craftSeconds` | float | Длительность в **серверных** секундах. Умножается на `CraftingStationConfig.craftSpeedMultiplier`. | Да | 600 |
| `requiredSkillLevel` | int | MVP: 0 = нет требования. Phase 2+ — минимальный уровень скилла. | Нет | 0 |
| `requiredSkill` | enum SkillType | Какой скилл. MVP: None. | Нет | None |

### Вложенные типы

#### `RecipeIngredient` (struct)

| Поле | Тип | Описание |
|------|-----|----------|
| `item` | `ItemData` (SO) | Какой предмет. Должен лежать в `Resources/Items/`. |
| `quantity` | int | Сколько штук. Минимум 1. |

#### `RecipeOutput` (struct)

| Поле | Тип | Описание |
|------|-----|----------|
| `item` | `ItemData` (SO) | **XOR** `shipKeyBinding` — обычный предмет. null если это корабль. |
| `quantity` | int | Сколько штук. Default 1. |
| `shipKeyBinding` | `ShipKeyBinding` (SO) | **XOR** `item` — привязка к кораблю (для рецептов-кораблей). null если это предмет. |

**Валидация на OnValidate:** см. `10_DESIGN.md` §2.2.

### Enums

#### `RecipeCategory`

| Value | Описание |
|-------|----------|
| `Module` | Модуль корабля (крыло, двигатель, ...). |
| `Consumable` | Расходник (еда, медикаменты, ...). |
| `Ship` | Корабль целиком. |
| `Material` | Базовый материал (доска, слиток, ...). |
| `Misc` | Прочее. |

#### `SkillType`

| Value | Описание |
|-------|----------|
| `None` | Без скилла (MVP). |
| `Engineering` | Phase 2+. |
| `Piloting` | Phase 2+. |
| `Trading` | Phase 2+. |
| `Combat` | Phase 2+. |

### Примеры

#### Пример 1: Модуль крыла

```
displayName: "Стальное крыло (Лёгкий)"
icon: icon_steel_wing.png
description: "Усиливает маневренность лёгких кораблей."
category: Module
ingredients:
  - item: Item_SteelIngot, qty: 3
  - item: Item_BoltIron, qty: 5
outputs:
  - item: Item_WingSteel_Light, qty: 1
craftSeconds: 600  # 10 мин
requiredSkillLevel: 0
requiredSkill: None
```

#### Пример 2: Корабль

```
displayName: "Лёгкий разведчик 'Ветер'"
icon: icon_light_scout.png
description: "Быстрый корабль для разведки облачных течений."
category: Ship
ingredients:
  - item: Item_SteelPlate, qty: 5
  - item: Item_AntigravCrystal, qty: 1
  - item: Item_FuelCell, qty: 1
outputs:
  - shipKeyBinding: ShipKeyBinding_LightScout
craftSeconds: 3600  # 1 час
requiredSkillLevel: 0
requiredSkill: None
```

#### Пример 3: Материал

```
displayName: "Деревянная доска"
icon: icon_wooden_plank.png
description: "Базовый строительный материал."
category: Material
ingredients:
  - item: Item_Log, qty: 2
outputs:
  - item: Item_Plank_Wood, qty: 4
craftSeconds: 120  # 2 мин
```

---

## 2. `CraftingStationConfig` (ScriptableObject)

**Где создать:** `Project → Create → Project C → Crafting → Station Config`. Сохранить в `Assets/_Project/Resources/Crafting/Stations/`.

### Поля

| Поле | Тип | Описание | Обязательно? | Default |
|------|-----|----------|--------------|---------|
| `displayName` | string | Имя (UI, tooltip, world-label). | Да | (пусто) |
| `icon` | Sprite | Иконка. | Нет | null |
| `description` | TextArea | Описание. | Нет | (пусто) |
| `stationType` | enum StationType | Тип (Shipyard / CraftingTable / Forge / Loom / Alchemy). | Да | CraftingTable |
| `allowedRecipes` | List<RecipeData> | Какие рецепты можно крафтить. | Да | (пусто) |
| `maxConcurrentJobs` | int | MVP: 1. | Нет | 1 |
| `craftSpeedMultiplier` | float | 1.0 = базовая скорость. 2.0 = в 2 раза быстрее. | Нет | 1.0 |
| `tintColor` | Color | Подкрашивает UI-элементы этой станции. | Нет | White |

### Enums

#### `StationType`

| Value | Описание | Пример |
|-------|----------|--------|
| `Shipyard` | Верфь — для кораблей. | Длинный таймер (1-8 часов). |
| `CraftingTable` | Стол крафта — для модулей и материалов. | Средний таймер (5-30 мин). |
| `Forge` | Наковальня (Phase 2). | Короткий таймер. |
| `Loom` | Ткацкий станок (Phase 2). | |
| `Alchemy` | Химическая станция (Phase 2). | |

### Пример

```
displayName: "Верфь 'Новая Надежда'"
icon: icon_station_shipyard.png
description: "Здесь можно построить корабль."
stationType: Shipyard
allowedRecipes:
  - R_LightScoutKey
  - R_MediumScoutKey
maxConcurrentJobs: 1
craftSpeedMultiplier: 1.0
tintColor: #4488CC (blue)
```

---

## 3. Привязка станции к сцене (GameObject в сцене)

**Где:** `WorldScene_X_Z.unity` (или `WorldScene_Crafting_Test.unity`).

### GameObject `[Station_xxx]`

| Компонент | Поле | Значение |
|-----------|------|----------|
| Transform | position | (X, Y, Z) — где стоит станция в мире. |
| NetworkObject | — | Обязательно. |
| `CraftingStation` | `_config` | (drag) `CraftingStationConfig` SO. |
| `CraftingStation` | `_interactRadius` | 4.0 (default). |
| `CraftingStation` | `_displayNameOverride` | "" (default — берёт из config). |
| `BoxCollider` | `isTrigger` | true (для raycast). |
| MeshFilter + MeshRenderer | — | Визуал (Cube / модель). |

**Проверка:** выделить GameObject → в Inspector должен быть `[CraftingStation]` с правильным `_config`.

---

## 4. Создание ресурсов (с чего начать контент-мейкеру)

**Минимальный набор для MVP:**

1. **3 `ItemData`** в `Resources/Items/`:
   - `Item_SteelIngot` (ItemType=Resources, maxStack=10)
   - `Item_SteelPlate` (ItemType=Resources, maxStack=10)
   - `Item_WingSteel_Light` (ItemType=Equipment, maxStack=1)

2. **1 `ShipKeyBinding`** (если ещё нет — из `Assets/_Project/Scripts/Ship/Key/`):
   - `ShipKeyBinding_LightScout` (serverKeyItem=Item_KeyLightShip, ...)

3. **3 `RecipeData`** в `Resources/Crafting/Recipes/`:
   - `R_SteelModule` (Module, 600 сек)
   - `R_LightShipKey` (Ship, 3600 сек)
   - `R_SimpleMaterial` (Material, 120 сек)

4. **2 `CraftingStationConfig`** в `Resources/Crafting/Stations/`:
   - `Station_Shipyard` (Shipyard, allowedRecipes=[R_LightShipKey])
   - `Station_CraftingTable` (CraftingTable, allowedRecipes=[R_SteelModule, R_SimpleMaterial])

5. **2 `CraftingStation` GameObject** в сцене:
   - `[Station_Shipyard]`: position=(50, 0, 0), config=Station_Shipyard
   - `[Station_CraftingTable]`: position=(-50, 0, 0), config=Station_CraftingTable

После этого — Play → host → подойти к станции → E → крафтить.

---

## 5. Соглашения по именованию

- `RecipeData` ассеты: `R_<name>.asset` (например, `R_SteelModule.asset`).
- `CraftingStationConfig` ассеты: `Station_<name>.asset`.
- `CraftingStation` GameObject в сцене: `[Station_<name>]` (в квадратных скобках, по конвенции бутстрапа).
- Папки: `Resources/Crafting/Recipes/`, `Resources/Crafting/Stations/`.

---

## 6. Локализация (Phase 2)

**В MVP:** все строки захардкожены в C# (`CraftingClientState.LocalizeResultCode`).

**В Phase 2:** использовать существующий `ProjectC.Localization` (если есть) или `UnityEngine.Localization` пакет. `RecipeData.displayName` и `description` — в `LocalizedString` (если пакет добавлен).

**На текущий момент:** строки `displayName` / `description` — **обычные string** (русский). Локализация — позже.
