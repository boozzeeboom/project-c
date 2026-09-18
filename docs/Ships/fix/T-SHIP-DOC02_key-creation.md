# T-SHIP-DOC02 — перепроверка: где создаётся KeyRodInstance (21 vs P1)

> Тикет: `T-SHIP-DOC02`. Дата: 2026-09-18. Статус: **ПОДТВЕРЖДЕНО (частично)**.
> Scope: только перепроверка + пометки в доке. Кода нет.

## Что проверяли

Утверждение ревью (§2 несостыковок): `21_SHIP_OWNERSHIP_MODEL.md §§2–3`
создаёт instance в `KeyRodInstanceBinding.OnNetworkSpawn` на `[KeyRod_*] PickupItem`,
а P1-факт (`00_OVERVIEW.md §2.1`) — в `ShipController.OnNetworkSpawn` через корутину.

## Факт на текущем main (сверено чтением)

- `21_` — предизайн от 2026-06-18, шапка: «📋 Дизайн готов, код НЕ написан».
- Устарело (Binding удалён в P1, см. DOC01):
  - `§2.1` (строка 40–41): «Создаётся в `KeyRodInstanceBinding.OnNetworkSpawn`
    (Q11: explicit binding компонент на каждом `[KeyRod_*]` PickupItem)».
  - `§2.5` (строки 207–208): `ShipKeyClientState.Instance.RequestCanBoard` — класс удалён.
  - `§3.1` (строки 221–222): `KeyRodInstanceBinding.OnNetworkSpawn (на каждом [KeyRod_*] PickupItem, Q11)
    → CreateInstance(...)` — точка создания переехала.
  - `§3.2` (строки 235–236): `ShipKeyClientState.RequestCanBoard` + `ShipKeyServer (legacy)` — удалены.
  - `§4` (строка 282): `[KeyRodInstanceBinding] хранит ссылку на GameObject корабля`,
    `ShipOwnershipRegistry синхронизируется через NetworkList` (строка 313, `§6`) — Registry удалён.
- Актуально как дизайн (не трогаем): структура `KeyRodInstanceWorld` (3 индекса + persistence),
  `ShipOwnershipRequirement`-концепт, `TransferInstance`-флоу (`§§3.3–3.4`), edge-таблица по смыслу,
  MVP-границы (`§5`).
- P1-факт подтверждён: `ShipController.cs:812,855,876` — создание в `OnNetworkSpawn`
  с rebind по `ShipPersistentId`.

## Решение

Статус ПОДТВЕРЖДЕНО ЧАСТИЧНО. Фикс docs-only: шапка + баннеры на `§§2.1/2.5/3/4`
(«точка создания переехала в `ShipController` (P1), legacy-API оставлено как история»),
дизайн-модель владения не переписываем.

## Проверка

- Читать `21_SHIP_OWNERSHIP_MODEL.md`: баннеры на месте, `§5` без баннера.
- Compile: не требуется (docs only).
