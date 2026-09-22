# T-SHIP-FIX14 — посадка в корабли без ключа

> Тикет: `T-SHIP-FIX14`. Дата: 2026-09-22. Статус: **ЗАЯВКА (репорт плейтестера,
> репро и ресерч нужны, не чинено)**.
> Источник: репорт «доступность в корабли без ключа» (эксплойт/баг доступа).

## Симптом (со слов)

Посадка (F) на корабль срабатывает без ключа в инвентаре. Точные условия
(какой корабль, хост/клиент, был ли ключ раньше, чей корабль) — УТОЧНИТЬ
у плейтестера, без репро чинить нельзя.

## Где смотреть (цепочка посадки, проверено чтением)

1. Клиент: `NetworkPlayer.Update` F-flow (`Scripts/Player/NetworkPlayer.cs:1011`) →
   `MetaRequirementClientState.RequestCanUse(netId)`; соседние вызовы `:1976`,
   `:2004` (другие interact-пути — проверить, не обходят ли ключ).
2. Запрос: `MetaRequirementClientState.RequestCanUse` (`Scripts/MetaRequirement/MetaRequirementClientState.cs:98`)
   → `MetaRequirementRegistry.RequestCanUseRpc` (`Scripts/MetaRequirement/MetaRequirementRegistry.cs:138`).
3. Сервер: `MetaRequirementRegistry.CanPlayerUse(clientId, netId)` (`:53`) +
   `ShipOwnershipRequirement.CanPlayerUse` (`Scripts/Ship/Key/ShipOwnershipRequirement.cs:93`).
4. Защита в точке посадки: `NetworkPlayer.SubmitSwitchModeRpc` (`:1619`) —
   defense-in-depth через `MetaRequirementRegistry` (прямой вызов, ownership-priority).
5. **Прайм-подозреваемый:** allow-by-default — если на корабль не зарегистрировано
   требование (нет записи в реестре), `CanPlayerUse` может отвечать true.
   Проверить: есть ли requirement-записи на ВСЕ корабли сцен (особенно NPC/
   новые), что возвращает сервер при отсутствии записи.

## Направление фикса (после репро)

- Реестр: явный deny по умолчанию для кораблей без requirement-записи
  (fail-closed, как пилотный каталог) — если подтвердится allow-by-default.
- Аудит всех F-путей посадки на предмет обхода `RequestCanUse`.
- Репро-кейсы: чужой корабль без ключа (хост и клиент), свой корабль,
  корабль без requirement-записи.

## Проверки (за пользователем)

- Console → 0 errors.
- F на чужом корабле без ключа: отказ (сообщение), посадки нет — хост и клиент.
- F со своим ключом: посадка как раньше (не сломать легитимный путь).
