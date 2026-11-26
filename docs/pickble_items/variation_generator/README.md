# T-VAR01 — Item Variation Generator

Генератор вариаций статов для pickable-предметов Project C.

Одна и та же система работает в двух режимах:

- **в редакторе** — дизайнер создаёт профиль из `ItemData`, крутит seed, смотрит превью,
  генерирует пакет и выгружает JSON;
- **на сервере** — тот же детерминированный ролл вызывается из кода по `profileId` + `seed`
  и создаёт конкретный инстанс предмета со статами, именем, тиром и ценой.

Ключевое свойство: **seed + профиль + базовые статы всегда дают один и тот же предмет**.
Поэтому клиент и сервер могут пересчитать/проверить ролл, а сохранённый `seed` достаточно
для воспроизведения инстанса.

---

## 1. Что уже реализовано

| Слой | Где | Назначение |
|---|---|---|
| Канонические статы | `Assets/_Project/Items/Variation/Core/ItemStatId.cs` | список статов + единицы + формулы |
| Детерминированный PRNG | `Assets/_Project/Items/Variation/Core/VariationRandom.cs` | SplitMix64, стабильный между платформами |
| Модель профиля/ролла | `Assets/_Project/Items/Variation/Core/ItemVariationTypes.cs` | тиры, аффиксы, результат |
| Генератор | `Assets/_Project/Items/Variation/Core/ItemVariationGenerator.cs` | ролл статов, имени, качества, цены |
| Фабрика/валидатор | `Assets/_Project/Items/Variation/Core/ItemVariationProfileFactory.cs` | профиль из `ItemData`, проверки |
| Реестр инстансов | `Assets/_Project/Items/Variation/Core/ItemVariationInstances.cs` | `instanceId`, серверный фасад, JSON-сейв |
| JSON | `Assets/_Project/Items/Variation/Core/ItemVariationJson.cs` | профиль/результат/реестр |
| DTO | `Assets/_Project/Items/Variation/Dto/ItemInstanceDto.cs` | сетевой пакет инстанса |
| Профиль-ассет | `Assets/_Project/Items/Variation/Data/ItemVariationProfileAsset.cs` | авторинг в редакторе |
| Окно генератора | `Assets/_Project/Items/Variation/Editor/ItemVariationWindow.cs` | превью, batch, экспорт |
| Self-test / smoke | `Assets/_Project/Items/Variation/Editor/ItemVariationSelfTest.cs` | проверка без Play Mode |

Сцены, префабы и существующий инвентарь **не изменены**: это отдельный инструмент и
серверный API. Подключение к `PickupItem`/`LootTable`/инвентарю описано в
[`04_INTEGRATION.md`](04_INTEGRATION.md).

---

## 2. Быстрый старт в редакторе

1. Выберите `ItemData` (в том числе `WeaponItemData`, `ClothingItemData`,
   `ModuleItemData`) в окне Project.
2. Откройте `Tools → Project C → Items → Variation Generator`.
3. Нажмите **Create from Selected ItemData** — профиль сохранится в
   `Assets/_Project/Items/Data/Variation/`.
4. Нажмите **Roll**. Меняйте `Seed`, чтобы увидеть разные варианты; один и тот же seed
   всегда даёт один и тот же предмет.
5. Для сервера — **Server JSON…**; для пакета — **Generate Batch** → **Export Batch JSON…**.

Подробно: [`03_EDITOR_TOOL.md`](03_EDITOR_TOOL.md).

---

## 3. Быстрый старт на сервере

```csharp
using System.IO;
using ProjectC.Items.Variation;
using ProjectC.Items.Variation.Dto;

var variations = new ItemVariationService();

// 1. Загрузить профили (JSON, выгруженный из редактора).
foreach (string path in Directory.GetFiles(profileDir, "*.json"))
{
    if (!variations.RegisterProfileJson(File.ReadAllText(path), out string error))
        Debug.LogError($"[Variations] {path}: {error}");
}

// 2. Создать предмет с вариацией статов.
long seed = 123456789L; // серверный seed из лута/квеста/крафта
if (variations.TryCreate("var_item_steel_sword", seed, out ItemInstanceDto item, out string createError))
{
    // item.instanceId, item.stats, item.displayName, item.finalPrice, item.rolls
}

// 3. Позже — достать статы уже выданного предмета по instanceId.
if (variations.TryGetInstance(item.instanceId, out ItemInstanceDto saved))
{
    // saved.stats ...
}

// 4. Сохранение/восстановление.
string saveJson = variations.ExportInstancesJson();
variations.ImportInstancesJson(saveJson, out string importError);
```

Подробно: [`02_API.md`](02_API.md).

---

## 4. Документы

| Файл | Содержание |
|---|---|
| [`01_DESIGN.md`](01_DESIGN.md) | модель статов, тиры, аффиксы, формулы, детерминизм |
| [`02_API.md`](02_API.md) | публичный API, JSON-схема, DTO, серверный пример |
| [`03_EDITOR_TOOL.md`](03_EDITOR_TOOL.md) | окно, инспектор, batch, self-test, troubleshooting |
| [`04_INTEGRATION.md`](04_INTEGRATION.md) | как подключить к pickup/loot/инвентарю/бою/торговле |
| [`99_CHANGELOG.md`](99_CHANGELOG.md) | журнал изменений T-VAR01 |

---

## 5. Проверка (выполнено)

- `check_compile_errors`: ошибок компиляции нет.
- `ItemVariationSelfTest.RunSyntheticReport()`: `[PASS]`, 120/120 роллов,
  14 разных имён на 120 seeds, JSON профиля читается обратно, `ItemVariationService`
  создаёт 5 инстансов, экспорт/импорт реестра сохраняет значения.
- Play Mode не запускался (правило проекта: Play Mode и скриншоты делает пользователь).

---

## 6. Границы текущего этапа

Реализован **генератор и его серверный API**, но ещё **не** изменены:

- `InventoryItemDto` / слоты инвентаря (там пока нет поля `instanceId` для обычных предметов);
- `PickupItem` / `LootTable` (нет хука «сгенерировать вариант при спавне/выпадении»);
- боевой расчёт (`WeaponItemDamageSource` пока читает статы из общего `WeaponItemData`);
- торговля (цена берётся из `ItemData.basePrice`).

Это сознательно вынесено в отдельные тикеты, чтобы не ломать текущую сеть и сейвы.
Пошаговый план — в [`04_INTEGRATION.md`](04_INTEGRATION.md).
