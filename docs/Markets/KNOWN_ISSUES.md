# Known Issues — Markets & Contracts

## [2026-06-17] 3+ активных контракта перестают отображаться в P → КОНТРАКТЫ

**Симптом:** При 3+ активных контрактах список в CharacterWindow (таб КОНТРАКТЫ) 
перестаёт отображать контракты — окно пустое. На MarketWindow не влияет.

**Предполагаемая причина:** Возможно рассинхрон между `ContractWorld.BuildSnapshot` 
(сервер) и `ContractClientState.CurrentSnapshot` (клиент) при большом количестве 
контрактов. Либо ListView virtualizer не корректен при `fixedItemHeight=48` 
с двухстрочными строками. Либо сервер обрезает active[] при определённом размере 
снапшота.

**Затронуто:** `ContractClientState.CurrentSnapshot.active[]` — возможно пуст хотя 
контракты есть; либо RPC с таргетом не доходит до клиента.

**Статус:** не воспроизведено в коде — нужен тест с 4+ контрактами и логами 
`[ContractClientState]` и `[ContractServer]` в консоли.

**Связанные файлы:**
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — `BuildSnapshot`, `GetActiveForPlayer`
- `Assets/_Project/Trade/Scripts/Client/ContractClientState.cs` — `CurrentSnapshot`
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs` — `RequestAcceptRpc`, `SendSnapshotToClient`
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/ContractsTab.cs` — `HandleContractSnapshot`

## [2026-06-17] Контракты не сохраняются при перезаходе (reconnect)

**Симптом:** После перезапуска сессии (reload сцены / переподключение / 
restart host) активные контракты, взятые на рынке, пропадают — персонаж 
снова без контрактов.

**Предполагаемая причина:** `ContractWorld._playerContracts` (серверное 
хранилище активных контрактов по playerId) живёт только в памяти сессии.
При перезапуске сервера или переподключении клиента `ContractWorld` 
пересоздаётся — все контракты теряются. Нет персистентного слоя 
(БД / файлового сохранения) для контрактов.

**Статус:** ожидает реализации персистентности — либо JSON-файловое 
сохранение (аналог T-Q18 repository), либо интеграция с инвентарной 
системой.

**Связанные файлы:**
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — `TryAccept`, `_playerContracts`
- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` — `PlayerContractEntry`
- `docs/Markets/TRADE_V2_DESIGN.md` (дизайн персистентности)

## [2026-06-16] ContractsTab — stale _contractState reference

**Статус:** FIXED — `OnTabShown()` теперь берёт `ContractClientState.Instance` свежим 
каждый раз, вместо кеширования в `BuildUI()`.
