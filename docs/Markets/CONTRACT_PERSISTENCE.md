# Contract Persistence (T-X5 persist)

## Date
2026-08-05

## Problem
`ContractWorld` хранил всё состояние контрактов в in-memory словарях. При перезагрузке сервера (`ContractServer.OnNetworkSpawn`) все активные контракты игроков, долги и сгенерированные контракты терялись — `Initialize()` всегда вызывал `GenerateContractsForAllLocations()` заново.

`IPlayerDataRepository` уже имел персистенцию credits/warehouse/cargo, но не имел методов для контрактов.

## Solution
Расширен `IPlayerDataRepository` методами `SaveContracts(ContractSaveData)` / `TryLoadContracts(out ContractSaveData)` с явным результатом `RepositoryLoadStatus`. `SaveContracts()` возвращает `bool`, чтобы post-save retention выполнялся только после успешной записи. Добавлен `ContractSaveData` — `[Serializable]` DTO, агрегирующий всё состояние `ContractWorld`. Реализовано в обоих репозиториях (`ServerFileRepository` → JSON-файл, `PlayerPrefsRepository` → PlayerPrefs).

`ContractWorld` теперь:
- При `Initialize`: сначала `LoadAll()` из репозитория; новые контракты генерируются только при `RepositoryLoadStatus.NoSaveFound`
- `ValidEmptySave` загружается как валидное состояние и не запускает регенерацию
- `CorruptSave` и `UnsupportedSchema` блокируют последующую запись, чтобы не затереть исходный snapshot
- После успешного сохранения terminal history ограничивается `MaxTerminalRecordsPerPlayer` (по умолчанию `50`); Active/Pending records и debts не удаляются
- После каждой мутации (`TryAccept`/`TryComplete`/`TryFail`/`Tick` при изменениях) — `SaveAll()`
- При `Shutdown` — `SaveAll()`

## Files changed

| File | Change |
|---|---|
| `Assets/_Project/Trade/Scripts/Dto/ContractSaveData.cs` | **NEW** — DTO с `List<ContractData>`, `List<ContractDebtEntry>`, `List<PlayerContractEntry>`, `List<LocationContractEntry>` |
| `Assets/_Project/Trade/Scripts/Repository/IPlayerDataRepository.cs` | + `SaveContracts` / `TryLoadContracts` |
| `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs` | + реализация (JSON `ServerData/contracts.json`) |
| `Assets/_Project/Trade/Scripts/Repository/PlayerPrefsRepository.cs` | + реализация (`PD2_Contracts` + best-effort `_bak`/`_tmp` recovery keys) |
| `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs` | + `SaveAll()` / `LoadAll()`, изменён `Initialize`, добавлены вызовы `SaveAll()` после мутаций |

## Data format
```json
{
  "schemaVersion": 2,
  "contracts": [ { "contractId": "...", "type": 0, "state": 1, "terminalAtUtcTicks": 0, ... } ],
  "debts": [ { "playerId": 123, "currentDebt": 150.0, "lastDecayTime": 42.5 } ],
  "playerContracts": [ { "playerId": 123, "contractIds": ["contract_primium_oak_4231"] } ],
  "locationContracts": [ { "locationId": "primium", "contractIds": ["...", "..."] } ]
}
```

## Verification
- Запустить сервер, принять контракт → перезапустить сервер → контракт должен остаться активным в UI
- Проверить логи: `[ContractWorld] Loaded N contracts, M debts from repository`
- Долги переживают рестарт
- Пустой, но валидный JSON возвращает `ValidEmptySave`, а отсутствие ключа/файла — `NoSaveFound`
- Повреждённый JSON возвращает `CorruptSave`; future schema возвращает `UnsupportedSchema` и не перезаписывается
- После превышения retention limit старые `Completed/Failed` records удаляются из runtime registry после успешной записи
- Для host persistence используются `PD2_Contracts`, `PD2_Contracts_bak` и `PD2_Contracts_tmp`; при повреждении primary repository пытается восстановить snapshot
