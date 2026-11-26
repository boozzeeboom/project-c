# 03 — Окно генератора вариаций

Меню: **Tools → Project C → Items → Variation Generator**.

Окно не требует Play Mode и не меняет сцены/префабы.

---

## 1. Создание профиля

1. Выберите `ItemData` в Project. Поддерживаются:
   - `ItemData` — цена/вес/стек;
   - `WeaponItemData` — урон, крит, дальность;
   - `ClothingItemData` — броня, STR/DEX/INT;
   - `ModuleItemData` — STR/DEX/INT.
2. В окне нажмите **Create from Selected ItemData**.
3. Профиль создаётся в `Assets/_Project/Items/Data/Variation/` с именем
   `var_<asset_name>.asset`.

При создании:

- снимается снапшот базовых статов (`profile.baseStats`);
- подставляются дефолтные тиры (обычный…легендарный);
- подставляются аффиксы по категории предмета;
- `profileId` = `var_` + имя ассета в нижнем регистре (не-ASCII → `_`);
- `seedSalt` = хэш от `profileId` (уникальные роллы на разных профилях).

---

## 2. Инспектор профиля

Если открыть `ItemVariationProfileAsset` в Inspector:

- **Capture base stats from item** — перечитать статы из `baseItem`;
- **Open in Generator** — открыть окно на этом профиле;
- **Validate** — проверить структуру, результат в Console/диалоге;
- **Export server JSON…** — выгрузить `ProfileDocument`.

Поля `profile` редактируются как обычный вложенный объект:

- `tiers` — список редкостей (добавляйте/удаляйте элементы);
- `modifiers` — список аффиксов;
- `baseStats` — можно править вручную, если база не из `ItemData`.

> Внимание: порядок `tiers` и `modifiers` — часть контракта seed'а. После выдачи
> предметов не переставляйте элементы; новые добавляйте в конец.

---

## 3. Preview

Справа:

- **Seed** — длинное целое. Один и тот же seed при тех же правилах = один и тот же предмет.
- **Random** — новый случайный seed.
- **Roll** — пересчитать превью.
- **Luck (bps)** — сдвиг весов тиров (`+1000` = +10% к весу каждого следующего тира).

Карточка показывает:

- имя, тир, качество, цену;
- `profileId`, seed, luck, количество роллов;
- таблицу «база → итог» по всем ненулевым статам (зелёный/красный дельта);
- список аффиксов с их вкладом и множителем тира.

**Copy result JSON** копирует `ItemVariationJson.ResultToJson(result)` — удобно для
баг-репорта.

---

## 4. Batch

1. Задайте `Count` (1..500) и `Start Seed`.
2. **Generate Batch** — seeds идут подряд: `start`, `start+1`, …
3. Строка `→` подставляет seed в preview.
4. **Export Batch JSON…** — сохраняет `ItemInstanceRegistrySnapshot` с
   `instanceId = 1..N`.
5. **Clear** — очистить список.

Пакетный JSON можно скормить `ItemVariationService.ImportInstancesJson` для
фикстур/тестов или использовать как заготовку лута.

---

## 5. Проверки

### 5.1 Validate

Кнопка **Validate** (в окне и в инспекторе) проверяет:

- `profileId` не пуст;
- есть тиры и хотя бы один тир с весом > 0;
- есть модификаторы и хотя бы один с диапазоном;
- диапазоны не перевёрнуты (предупреждение, генератор нормализует);
- `minRolls <= maxRolls`;
- базовые статы не все нулевые;
- множители тира не <= 0.

Error блокирует регистрацию профиля на сервере; Warning — нет.

### 5.2 Self-test (all profiles)

**Tools → Project C → Items → Variation Self-Test (all profiles)**

Проверяет каждый `ItemVariationProfileAsset`:

1. один seed дважды → одинаковый JSON;
2. 200 seeds: роллы в диапазонах с учётом множителя тира (±1 на округление);
3. статы неотрицательны, качество 0..100, цена неотрицательна;
4. `finalPrice == finalStats.price`;
5. разные seeds дают разные имена (если аффиксов достаточно);
6. структура профиля без error-level замечаний.

Результат — в Console и диалоге.

### 5.3 Smoke test (in-memory)

**Tools → Project C → Items → Variation Smoke Test (in-memory)**

Не требует ассетов: создаёт временный `WeaponItemData`, профиль, гоняет self-test,
проверяет JSON профиля, `RegisterProfile`, 5 инстансов, экспорт/импорт реестра.
Объект уничтожается сразу после теста.

Этот же метод доступен как `ItemVariationSelfTest.RunSyntheticReport()` для CI/execute_code.

---

## 6. Экспорт на сервер

**Server JSON…** сохраняет `ProfileDocument`:

- `schemaVersion`;
- `exportedUtc`;
- `exporter`;
- `profile` целиком, включая `baseStats`.

Сервер читает этот файл через `ItemVariationService.RegisterProfileJson`. `ItemData`
на сервере не нужен.

---

## 7. Troubleshooting

| Симптом | Причина / решение |
|---|---|
| «Нет выбранного профиля» | выберите `ItemVariationProfileAsset` в поле сверху или создайте из `ItemData` |
| «Профиль не задан (null)» | у ассета пустой `profile` — пересоздайте через **Create** |
| «Нет ни одного модификатора» | пустой `modifiers`; нажмите **Rebuild default tiers + affixes** |
| Все seeds дают одинаковое имя | мало аффиксов/диапазоны вырождены; добавьте аффиксы или расширьте диапазоны |
| Цена 0 | у тира `priceMultiplierBps <= 0` или у базы `price = 0` |
| Статы не меняются | `baseStats` нулевые либо аффиксы не для этих статов; нажмите **Capture** |
| Сервер отклоняет профиль | **Validate** и исправить Error-сообщения |
| После правки профиля старые seed'ы дают другой предмет | изменился порядок/содержимое тиров или аффиксов — это ожидаемо |

---

## 8. Рекомендуемый workflow

1. Создать профиль из предмета.
2. Поправить аффиксы/диапазоны, нажать **Validate**.
3. Покрутить seeds, проверить крайние тиры (можно временно поднять вес легендарного).
4. Прогнать **Self-test**.
5. Выгрузить **Server JSON…** в `docs`/папку сборки.
6. Вернуть веса тиров в прод-значения и ещё раз прогнать Self-test.
