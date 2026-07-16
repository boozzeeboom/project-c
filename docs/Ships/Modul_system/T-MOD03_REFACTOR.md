# Module System — Refactoring T-MOD03

**Дата:** 2026-07-09
**Коммит:** объединение ModuleShopEntry в ShipModule

---

## Что изменилось

Раньше:
- `ShipModule` (MODULE_LIFT_ENH.asset) — характеристики модуля
- `ModuleShopEntry` (ShopEntry_MODULE_LIFT_ENH.asset) — цена + ресурсы, ссылка на ShipModule

Теперь:
- `ShipModule` содержит всё: характеристики + `costCredits` + `requiredResources[]`
- `ModuleShopDatabase.entries` = `List<ShipModule>` (прямые ссылки)
- `ModuleShopEntry` помечен `[Obsolete]`

---

## Новые поля ShipModule

| Поле | Тип | Описание |
|------|-----|----------|
| `costCredits` | `int` | Стоимость установки в кредитах (default: 500) |
| `requiredResources` | `ResourceRequirement[]` | Ресурсы для установки |

---

## Миграция

`Tools → ProjectC → Ship → Migrate ShopEntry → ShipModule`

Переносит costCredits и requiredResources из старых ShopEntry_*.asset в соответствующие ShipModule, обновляет ModuleShopDatabase. После миграции старые ShopEntry_*.asset можно удалить.

---

## Затронутые файлы

| Файл | Изменение |
|------|-----------|
| `ShipModule.cs` | +costCredits, +requiredResources |
| `ModuleShopDatabase.cs` | entries: List<ModuleShopEntry> → List<ShipModule> |
| `ModuleShopEntry.cs` | [Obsolete], ResourceRequirement struct оставлен |
| `ShipModuleCatalog.cs` | итерация напрямую по ShipModule |
| `ShipModuleServer.cs` | FindModuleById упрощён |
| `RepairManagerWindow.cs` | mod.costCredits вместо entry.costCredits |
| `RepairManagerEditor.cs` | работа с ShipModule напрямую |
| `ModuleShopDatabaseEditor.cs` | работа с ShipModule напрямую |
| `CreateModuleShopEntries.cs` | обновлён под новую архитектуру |
| `ModuleShopMigration.cs` | новый: скрипт миграции |

---

## Редакторы

### ModuleShopDatabase
- Скан папки Modules — авто-добавление ShipModule
- Очистить все / Валидация / Сортировка по цене/имени
- Массовая установка costCredits
- Фильтры по типам модулей (Propulsion/Utility/Special/Engine)
- + Add Module — выбрать ShipModule через ObjectField
- + Mass Add from Catalog — чекбокс-список всех ShipModule в проекте

### RepairManager (inline)
- Весь функционал ModuleShopDatabaseEditor доступен при выборе RepairManager на сцене
- Редактирование costCredits прямо в списке
- Кнопка 📍 Ping для перехода к файлу модуля
