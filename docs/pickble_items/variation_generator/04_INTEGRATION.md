# 04 — Интеграция генератора с игровыми системами

Статус: **план**, в T-VAR01 не реализовано. Документ фиксирует правильный путь,
чтобы не сломать сеть/сейвы и не размножать `.asset`-файлы.

---

## 1. Текущее состояние

- `ItemData` — общий ассет предмета; `itemId` однозначно определяет **тип**, но не
  конкретный экземпляр.
- `InventoryData` хранит стеки по `itemId`; `instanceId` есть только у `ItemType.Key`.
- `PickupItem.itemData` — ссылка на общий ассет; `itemId` вычисляется при подборе.
- `InventoryItemDto` не содержит `instanceId` для обычных предметов.
- `LootTable` возвращает `ItemData`, без вариаций.
- `WeaponItemDamageSource` читает статы из `WeaponItemData`.

Следствие: **нельзя** просто мутировать общий `ItemData` при ролле — это изменит все
предметы этого типа у всех игроков. Нельзя и создавать по `.asset` на каждый ролл.

Правильная модель — **серверный инстанс** (`ItemVariationService`), а `ItemData`
остаётся определением.

---

## 2. Целевая схема

```text
Server
  ItemVariationService
    ├── profiles: profileId → ItemVariationProfile (из JSON)
    └── instances: instanceId → ItemVariationResult
          │
          ├── LootTable / PickupItem / Crafting: seed → TryCreate → instanceId
          └── Inventory / Trade / Combat: instanceId → TryGetInstance → stats

Client
  ItemInstanceDto (по сети) → карточка, цена, визуал, экипировка
```

Инварианты:

1. Ролл делает только сервер.
2. `instanceId` уникален в пределах серверного мира (или названного пространства).
3. Клиент не доверяет своим статам: он показывает присланный DTO.
4. Сохранение инстансов — `ExportInstancesJson`/`ImportInstancesJson` (или замена на
   БД в будущем).

---

## 3. Шаги внедрения

### T-VAR02 — instanceId в инвентаре

Добавить `instanceId` в слоты обычных предметов (по аналогии с Key):

- `InventorySlot` уже имеет `itemId + instanceId` (сейчас используется для Key);
- `InventoryItemDto` — добавить `int instanceId` и сериализовать его;
- при стеке `instanceId = 0` означает «обычный предмет без вариации»;
- для equipment/оружия с вариацией — отдельный слот (не стек), `instanceId > 0`.

Снимок инвентаря передаёт `instanceId`, а клиент может:

- запросить `ItemInstanceDto` по id у сервера, **или**
- получить пачку `ItemInstanceDto[]` в том же снапшоте (проще для UI).

### T-VAR03 — источники предметов

- `LootTable`: добавить `variationProfileId` + `variationChance`/`luckBps` к записи;
  при генерации лута сервер вызывает `TryCreate`.
- `PickupItem`: для сценовых пикапов добавить `variationProfileId` + `seed` (или
  серверный `instanceId`); при подборе сервер проверяет/создаёт инстанс.
- `Crafting`: при завершении крафта — `TryCreate` с seed из серверного RNG; качество
  можно связать с навыком/станцией.
- `NpcLootPickup`/сундуки — через `LootTable`.

Важно: `seed` должен генерироваться на сервере и сохраняться, иначе предмет нельзя
воспроизвести после рестарта.

### T-VAR04 — UI и карточка

- В `CharacterWindow`/тултипе показывать `displayName`, `tierName`, `quality`,
  `finalPrice`, список `rolls`.
- Цвет — из `tierColorHex`.
- Для статов использовать `ItemStatCatalog.Format`.

### T-VAR05 — бой

`WeaponItemDamageSource`/`ICombatDamageProvider` сейчас получает `WeaponItemData`.
Нужен адаптер, который для оружия с `instanceId` берёт:

- `stats.damage` вместо `baseDamage`;
- `stats.critChance` вместо `critModifier`;
- `stats.rangeMilli` вместо `range`;
- `damageDice`/`damageType` — из `WeaponItemData` (они не варьируются).

Не менять `WeaponItemData` ради вариации: статы инстанса живут отдельно.

### T-VAR06 — торговля

- `NpcMerchantTable`/`TradeItemDefinition` при наличии `instanceId` используют
  `finalPrice` как базу для buy/sell.
- Рыночная формула (`PriceFormula`) может применять спрос/предложение к
  `finalPrice`, а не к `ItemData.basePrice`.

### T-VAR07 — сохранение

На сервере:

- при старте — загрузить профили (`RegisterProfileJson`) и инстансы
  (`ImportInstancesJson`);
- при создании — `TryCreate` и сохранить реестр (сейчас JSON, позже БД);
- при удалении предмета — `Instances.Remove(instanceId)`, чтобы не течь.

Ограничение: `instanceId` должен восстанавливаться вместе с инвентарём. Если
инвентарь ссылается на `instanceId`, которого нет в реестре, — fail-closed: не
показывать статы, писать warning, не давать экипировать.

---

## 4. Пример потока (лут)

```csharp
// Сервер, при открытии сундука.
foreach (LootEntry entry in table.entries)
{
    if (Random.value > entry.chance) continue;

    if (!string.IsNullOrEmpty(entry.variationProfileId))
    {
        long seed = serverRng.NextLong();
        if (variations.TryCreate(entry.variationProfileId, seed,
                out ItemInstanceDto variant, out string error, luckBps: entry.luckBps))
        {
            // variant.instanceId → в инвентарь/сундук
            GiveItem(player, variant);
            continue;
        }
        Debug.LogWarning(error);
    }

    GivePlainItem(player, entry.item);
}
```

---

## 5. Риски и решения

| Риск | Решение |
|---|---|
| общий `ItemData` мутирует | никогда не писать статы в ассет; только в `ItemVariationResult` |
| клиент подделывает статы | клиенту только DTO с сервера; ролл/цена на сервере |
| разбухание `.asset` | не создавать asset на ролл; инстансы — JSON/БД |
| старые сейвы без `instanceId` | `instanceId = 0` = обычный предмет; миграция не нужна |
| изменили порядок аффиксов → старые seed'ы другие | инстансы хранят `finalStats`, пересчёт не нужен; новые seed'ы — новая версия профиля |
| нет профиля на сервере | fail-closed: обычный предмет + warning, не падать |
| одинаковые имена у разных инстансов | это нормально; уникальность — `instanceId`, не имя |

---

## 6. Критерии приёмки интеграции

- [ ] Сервер создаёт вариант и выдаёт его в инвентарь с `instanceId`.
- [ ] Клиент видит имя/тир/статы/цену из DTO.
- [ ] Рестарт сервера восстанавливает инстансы и статы.
- [ ] Один seed после рестарта даёт тот же предмет.
- [ ] Обычные предметы без профиля работают как раньше (`instanceId = 0`).
- [ ] Нет изменений `ItemData`-ассетов во время игры.
