# 02 — API генератора вариаций

Пространство имён: `ProjectC.Items.Variation`
(DTO — `ProjectC.Items.Variation.Dto`).

---

## 1. Публичные типы

| Тип | Назначение |
|---|---|
| `ItemStatId` | перечисление статов |
| `ItemStatCatalog` | отображение/форматирование/формулы статов |
| `DeterministicRandom` | SplitMix64 PRNG |
| `VariationSeed` | хэш/смешивание seed |
| `ItemVariationProfile` | правила вариации |
| `ItemVariationTier` | тир |
| `ItemVariationModifier` | аффикс |
| `ItemVariationBaseStats` | канонические базовые/итоговые статы |
| `ItemVariationResult` | результат ролла |
| `ItemVariationGenerator` | чистый ролл |
| `ItemVariationProfileFactory` | профиль из `ItemData`, валидация |
| `ItemVariationProfileAsset` | `ScriptableObject`-обёртка |
| `ItemVariationService` | серверный фасад + реестр |
| `ItemInstanceRegistry` | `instanceId → ItemVariationResult` |
| `ItemInstanceDto` | сетевой DTO инстанса |
| `ItemStatsDto` / `ItemStatRollDto` | вложенные DTO |
| `ItemVariationJson` | JSON профиля/результата/реестра |

---

## 2. Ролл (чистый API)

```csharp
using ProjectC.Items.Variation;

// Быстрый ролл по профилю (использует profile.baseStats).
if (!ItemVariationGenerator.TryRoll(
        profile, seed: 777L, out ItemVariationResult result,
        out string error, luckBps: 0))
{
    Debug.LogError(error);
}

// С явными базовыми статами (например, если база пришла из БД).
if (ItemVariationGenerator.TryRoll(
        profile, baseStats, 777L, out result, out error, luckBps: 500))
{
    // result.tierName, result.displayName, result.finalStats, result.finalPrice, result.rolls
}

// Вариант, который бросает исключение (только для доверенных данных).
ItemVariationResult guaranteed = ItemVariationGenerator.RollOrThrow(profile, seed);
```

`ItemVariationGenerator` не хранит состояния и не использует `UnityEngine`/`UnityEditor` —
его можно вызывать из любого серверного кода (в этой сборке — Unity dedicated server).

---

## 3. Серверный фасад

```csharp
using ProjectC.Items.Variation;
using ProjectC.Items.Variation.Dto;

var variations = new ItemVariationService();

// Профиль из JSON (файл, выгруженный кнопкой "Server JSON…").
if (!variations.RegisterProfileJson(File.ReadAllText(path), out string error))
    throw new InvalidOperationException(error);

// Создать инстанс и получить DTO.
if (variations.TryCreate("var_item_steel_sword", seed: 123456789L,
        out ItemInstanceDto dto, out string createError, luckBps: 0))
{
    // dto.instanceId — серверный id; отправить клиенту.
}

// Получить инстанс позже (например, при открытии карточки/экипировке).
if (variations.TryGetInstance(dto.instanceId, out ItemInstanceDto saved))
{
    int damage = saved.stats.damage;
    int price  = saved.finalPrice;
}

// Персистентность.
string allJson = variations.ExportInstancesJson();
variations.ImportInstancesJson(allJson, out string importError);
```

`ItemVariationService`:

- хранит ссылки на профили (последняя регистрация с тем же `profileId` заменяет профиль);
- выдаёт `instanceId` из монотонного счётчика, начиная с 1;
- отклоняет профили с error-level замечаниями;
- не является потокобезопасным: вызывайте из серверного тика/под мьютексом;
- `ImportInstancesJson` **заменяет** текущий реестр.

---

## 4. JSON

### 4.1 Профиль

Пример сокращён: рабочий профиль содержит все 5 тиров и полный набор аффиксов;
ниже показаны один тир и один аффикс, чтобы был виден формат.

```json
{
  "schemaVersion": 1,
  "exportedUtc": "2026-11-26T10:00:00.0000000Z",
  "exporter": "ProjectC.Items.Variation",
  "profile": {
    "schemaVersion": 1,
    "profileId": "var_item_steel_sword",
    "displayName": "Стальной клинок",
    "baseItemId": 2009,
    "baseItemName": "Стальной клинок",
    "seedSalt": 12345,
    "nameStyle": 0,
    "minRolls": 1,
    "maxRolls": 2,
    "tiers": [
      {
        "tierId": "common",
        "displayName": "Обычный",
        "colorHex": "#B8C0CC",
        "weight": 60,
        "extraRolls": 0,
        "valueMultiplierBps": 10000,
        "priceMultiplierBps": 10000,
        "qualityBase": 5
      },
      {
        "tierId": "legendary",
        "displayName": "Легендарный",
        "colorHex": "#FFB020",
        "weight": 1,
        "extraRolls": 4,
        "valueMultiplierBps": 17000,
        "priceMultiplierBps": 28000,
        "qualityBase": 88
      }
    ],
    "modifiers": [
      {
        "affixId": "sharpened",
        "affixName": "Закалённый",
        "isSuffix": false,
        "stat": 0,
        "flatMin": 0,
        "flatMax": 0,
        "percentMinBps": 500,
        "percentMaxBps": 1500,
        "weight": 100,
        "minTierIndex": 0,
        "maxTierIndex": 99,
        "unique": true
      }
    ],
    "baseStats": {
      "damage": 12,
      "critChance": 5,
      "rangeMilli": 2000,
      "weightMilli": 1500,
      "price": 120,
      "durability": 0,
      "armor": 0,
      "strengthMilli": 0,
      "dexterityMilli": 0,
      "intelligenceMilli": 0,
      "stackSize": 1,
      "moveSpeed": 0
    }
  }
}
```

`nameStyle` и `stat` сериализуются как числа (`JsonUtility`). Для `nameStyle`:
`0 = PrefixSuffix`, `1 = BaseWithAffixList`.

### 4.2 Результат

`ItemVariationJson.ResultToJson(result)` — отладочный/аудиторский JSON одного ролла.
Он не нужен серверу для игры, но удобен для логов и баг-репортов.

### 4.3 Реестр инстансов

```json
{
  "schemaVersion": 1,
  "nextId": 6,
  "instances": [
    {
      "schemaVersion": 1,
      "instanceId": 1,
      "baseItemId": 2009,
      "tierIndex": 2,
      "quality": 61,
      "finalPrice": 192,
      "priceMultiplierBps": 16000,
      "seed": 7000,
      "luckBps": 0,
      "profileId": "var_item_steel_sword",
      "baseItemName": "Стальной клинок",
      "displayName": "Закалённый Стальной клинок · Лёгкий",
      "tierId": "rare",
      "tierName": "Редкий",
      "tierColorHex": "#4FA3FF",
      "baseStats": { "...": "..." },
      "stats": { "...": "..." },
      "rolls": [
        {
          "stat": 0,
          "flat": 0,
          "percentBps": 1200,
          "modifierIndex": 2,
          "tierMultiplierBps": 12500
        }
      ]
    }
  ]
}
```

---

## 5. Сетевой DTO

`ItemInstanceDto : INetworkSerializable`:

| Поле | Тип | Смысл |
|---|---|---|
| `schemaVersion` | int | версия формата |
| `instanceId` | int | серверный id (0 = не зарегистрирован) |
| `baseItemId` | int | id базового предмета |
| `tierIndex` | int | индекс тира в профиле |
| `quality` | int | 0..100 |
| `finalPrice` | int | цена инстанса |
| `priceMultiplierBps` | int | множитель цены тира (аудит) |
| `seed` | long | исходный seed |
| `luckBps` | int | удача ролла |
| `profileId` | string | id профиля |
| `baseItemName` | string | имя базы |
| `displayName` | string | итоговое имя |
| `tierId` / `tierName` / `tierColorHex` | string | тир |
| `baseStats` | `ItemStatsDto` | базовые статы |
| `stats` | `ItemStatsDto` | итоговые статы |
| `rolls` | `ItemStatRollDto[]` | роллы аффиксов |

Особенности сериализации:

- строки пишутся с `hasValue`-флагом (null safety, как в `InventoryItemDto`);
- массив роллов — с length-prefix;
- всё в целых числах, без float.

`ItemInstanceDto.FromResult(result, instanceId)` и `dto.ToResult()` — конвертеры.

---

## 6. Ошибки и версии

- `TryRoll` / `TryCreate` / `TryGetInstance` возвращают `false` и текст ошибки, не бросая
  исключений на нормальных кейсах.
- `RollOrThrow` — только для доверенных данных (тесты/редактор).
- `schemaVersion` проверяется при чтении JSON: более новый формат отклоняется.
- При изменении семантики роллов поднимайте `ItemVariationJson.CurrentSchemaVersion`
  и `ItemVariationProfile.CurrentSchemaVersion`.

---

## 7. Потокобезопасность

- `ItemVariationGenerator` — статический, без общего состояния; профиль и базовые
  статы только читаются. Можно вызывать параллельно, если не мутировать профиль.
- `ItemVariationService` и `ItemInstanceRegistry` — не потокобезопасны (словари и
  счётчик). На сервере вызывайте их из одного тика или под lock.
