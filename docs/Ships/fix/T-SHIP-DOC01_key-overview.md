# T-SHIP-DOC01 — перепроверка: Key OVERVIEW vs P1-факт

> Тикет: `T-SHIP-DOC01`. Дата: 2026-09-18. Статус: **ПОДТВЕРЖДЕНО**.
> Scope: только перепроверка + пометки в доке. Кода нет.

## Что проверяли

Утверждение ревью (§1 несостыковок): `Key-subsystem/00_OVERVIEW.md §§2.1/2.4/12`
говорят «удалены», а `§§1.1a/1.1b/6.1/7/8` до сих пор описывают до-P1 API.
По рецепту из `§1.1a` сейчас не собрать.

## Факт на текущем main (сверено чтением)

- P1-удаление подтверждено: файлов `ShipKeyBinding.cs`, `ShipKeyServer.cs`,
  `ShipKeyClientState.cs`, `ShipKeyToast.cs`, `ShipOwnershipRegistry.cs`,
  `KeyRodInstanceBinding.cs` в `Assets/` нет (в `Scripts/Ship/Key/` только 4 файла:
  `KeyRodInstance`, `KeyRodInstanceWorld`, `KeyRodInstanceRepository`, `ShipOwnershipRequirement`).
  Оставшиеся упоминания — только комменты/deprecated-заметки
  (`NetworkPlayer.cs:1460`, `NetworkManagerController.cs:183,348,374`,
  `PickupItem.cs:182,206`, `MyShipsTab.cs:8,54`, `MetaRequirement*.cs`, `ShipHudController.cs:13,116`).
- P1-путь создания подтверждён: `ShipController.cs:812` (`StartCoroutine(CreateKeyInstanceWhenReady)`),
  `:855` (корутина), `:876` (`KeyRodInstanceWorld.CreateInstance(itemId, NetworkObjectId, OWNER_NONE, ShipPersistentId)`).
- Устаревшие параграфы подтверждены чтением `00_OVERVIEW.md` (360 строк):
  - `§1.1a` (строки 25–55): `Add Component → Ship Key Binding`, `ShipKeyBinding.OnNetworkSpawn → ShipKeyServer.RegisterBinding`,
    `PushBindingsRpc`, лог `[ShipKeyServer] Registered binding` — классы удалены, не собрать.
  - `§1.1b` (строки 57–65): таблица ссылается на `ShipKeyBinding`, `ShipKeyClientState.Instance`,
    `ShipKeyServer.Instance`, `[ShipKeyServer]` в Bootstrap — всё удалено.
  - `§6.1` (строки 194–217): код с `ShipKeyClientState.Instance.RequestCanBoard` — удалён.
  - `§6.2` (строки 219–221): `ShipKeyServer собирает биндинги на OnNetworkSpawn` + `SceneManager.sceneLoaded` — удалён.
  - `§6.4` (строки 227–229): `PushBindingsRpc` на `OnClientConnected` — удалён.
  - `§7` (строки 237–256): серверный guard через `ShipKeyServer.Instance.CanPlayerBoard` — удалён
    (заменён `ShipOwnershipRequirement` + `MetaRequirementRegistry`, см. `§2.1` — актуален).
  - `§8` (строки 260–278): расстановка `[Ship_KeyServer]` в Bootstrap + `ShipKeyBinding` на кораблях — удалена.
- Актуальны и не тронуты: `§2.1` (World + `ShipController.OnNetworkSpawn` + `ShipOwnershipRequirement` +
  `MetaRequirementRegistry`), `§2.2–2.4`, `§3`, `§4`, `§12`.

## Решение (утверждено планом SHIP_FIX_PLAN_2026-09-18)

Статус ПОДТВЕРЖДЕНО. Фикс docs-only: в `00_OVERVIEW.md` повесить баннеры
`> ⚠️ УСТАРЕЛО (P1 2026-07-21)` на `§§1.1a/1.1b/6.1/6.2/6.4/7/8` со ссылкой на `§2.1`/`§12`,
текст параграфов не переписывать (история сохраняется).
Stale комменты в коде (`KeyRodInstanceWorld.cs:32`, `PickupItem.cs:182`, `MyShipsTab.cs:8,54`)
— НЕ этот тикет, уйдут в `T-SHIP-FIX13` (чистка).

## Проверка

- Читать `00_OVERVIEW.md`: баннеры на месте, актуальные `§2.1/§12` без баннеров.
- Compile: не требуется (docs only). Console → 0 errors тривиально.
