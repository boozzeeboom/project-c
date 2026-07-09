# Итерации разработки

## Итерация от 2026-07-09

**Задача:** Исправить USS и CS0618 варнинги в CharacterWindow.
**Коммит:** `d815235` — T-UI03: исправлены USS и CS0618 варнинги в CharacterWindow
**Изменения:**
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — удалены неподдерживаемые свойства (text-decoration, cursor, gap, overflow-y, z-index, inherit, align-items:baseline)
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — удалён [Obsolete] `_inventoryCache`, ссылки перенаправлены на `InventoryTab.InventoryCache`
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/InventoryTab.cs` — `InventoryListItem` сделан public, добавлен public accessor `InventoryCache`

## Итерация от 2026-07-16

**Задача:** Переработка блока характеристик в CharacterWindow — фикс полосок, цветов и позиционирования текста.
**Коммит:** `354e3d2` — T-UI04: переработка блока характеристик в CharacterWindow — фикс полосок, цветов и текста
**Изменения:**
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — фикс формулы бара (strength вместо effectiveStrength), новый формат текста, ApplyTierClass через CSS-классы
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` — per-stat цвета fill-баров, tier-рамки, горизонтальный layout строк
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — добавлены per-stat классы на fill-бары и лейблы, горизонтальная структура stat-row
