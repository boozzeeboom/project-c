# Markets & Contracts — глубокий архитектурный аудит и план рефакторинга

**Дата аудита:** 17 августа 2026 г.  
**Область:** `Assets/_Project/Trade/Scripts/`, `Assets/_Project/Scripts/UI/Client/`, `Assets/_Project/Quests/Bridges/`, `docs/Markets/`  
**Статус:** статический аудит исходников и документации; этапы 1–15B реализованы 17–18 августа 2026 г.; verification pass и оставшиеся P0/P1/P2/P3 тикеты остаются открытыми.
**Проверки:** Unity compile check пройден; Play Mode, domain tests и screenshot-регрессия не запускались.

> Этот документ является рабочим планом исправления. Он не заменяет отдельный migration design и не должен приводить к массовой переписи YAML-сцен без отдельного согласования.

---

## 1. Executive summary

Текущая реализация уже разделена на server-authoritative слой и клиентскую проекцию:

```text
MarketServer / ContractServer
        ↓ RPC / TargetRpc
TradeWorld / ContractWorld
        ↓
MarketState / ContractData / ContractDebt
        ↓ persistence
IPlayerDataRepository
        ↓
MarketClientState / ContractClientState
        ↓
MarketWindow / ContractsTab / CharacterWindow
```

Основная проблема не в отсутствии слоёв, а в том, что **модель жизненного цикла контракта не разделяет offer, active, completed и failed состояния на уровне хранения**. Из-за этого методы генерации доски, принятия, завершения, истечения таймера и persistence работают с одними и теми же коллекциями, но предполагают разные семантики.

Самый опасный дефект:

> `ContractWorld.GenerateContractsForLocation()` удаляет все ID из `_locationContracts[locationId]` из `_availableContracts`, включая активные контракты, если их ID всё ещё находится в `_locationContracts`. `TryAccept()` не удаляет принятое ID из `_locationContracts`. При повторной генерации доски активный контракт исчезает из доступных данных, хотя `_playerContracts` продолжает на него ссылаться.

Это объясняет симптом из `docs/Markets/KNOWN_ISSUES.md`: активные контракты перестают отображаться в `P → КОНТРАКТЫ` после регенерации списка.

Приоритет исправлений:

1. **P0 — целостность контрактов:** запрет удаления active contract, очистка stale IDs, единые инварианты коллекций.
2. **P0 — экономика:** проверка и списание cargo при delivery completion; явная реализация Receipt semantics.
3. **P1 — persistence:** сохранение debts-only, schema version, atomic write, retention completed/failed данных.
4. **P1 — клиентский контракт API:** локализованные ошибки и feedback при rate limit.
5. **P2 — архитектурная чистка:** удаление дублирующей UI-логики, dead RPC, разделение `MarketWindow`.
6. **P2/P3 — расширяемость и документация:** устранение hardcoded locations/types, обновление каталогов и migration-документов.

---

## 2. Что было проверено

### Runtime-код

- `Assets/_Project/Trade/Scripts/Core/ContractWorld.cs`
- `Assets/_Project/Trade/Scripts/Core/ContractData.cs`
- `Assets/_Project/Trade/Scripts/Core/ContractDebt.cs`
- `Assets/_Project/Trade/Scripts/Core/TradeWorld.cs`
- `Assets/_Project/Trade/Scripts/Core/MarketState.cs`
- `Assets/_Project/Trade/Scripts/Core/MarketConfig.cs`
- `Assets/_Project/Trade/Scripts/Core/MarketZone.cs`
- `Assets/_Project/Trade/Scripts/Core/MarketZoneRegistry.cs`
- `Assets/_Project/Trade/Scripts/Core/MarketTimeService.cs`
- `Assets/_Project/Trade/Scripts/Core/WorldEventBus.cs`
- `Assets/_Project/Trade/Scripts/Network/ContractServer.cs`
- `Assets/_Project/Trade/Scripts/Network/MarketServer.cs`
- `Assets/_Project/Trade/Scripts/Network/NetworkPlayer.cs` — contract/market delivery regions
- `Assets/_Project/Trade/Scripts/Dto/ContractDto.cs`
- `Assets/_Project/Trade/Scripts/Dto/ContractSaveData.cs`
- `Assets/_Project/Trade/Scripts/Repository/IPlayerDataRepository.cs`
- `Assets/_Project/Trade/Scripts/Repository/PlayerPrefsRepository.cs`
- `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs`
- `Assets/_Project/Trade/Scripts/Core/WorldEventBus.cs`
- `Assets/_Project/Trade/Scripts/Exchange/ExchangeWorld.cs` и связанный exchange-код
- `Assets/_Project/Trade/Scripts/Core/ContractWorldItemResolver.cs`

### UI и интеграции

- `Assets/_Project/Scripts/UI/Client/MarketWindow.cs`
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs`
- `Assets/_Project/Scripts/UI/Client/CharacterWindow/ContractsTab.cs`
- `Assets/_Project/Quests/Bridges/ContractMetaBridge.cs`
- связанные участки `QuestServer` и `WorldEventBus`

### Документация

- `docs/Markets/README.md`
- `docs/Markets/FILES_INDEX.md`
- `docs/Markets/ARCHITECTURE.md`
- `docs/Markets/TRADE_V2_DESIGN.md`
- `docs/Markets/CONTRACT_V2_MIGRATION.md`
- `docs/Markets/CONTRACTS_AS_MARKET_TAB_REFACTOR.md`
- `docs/Markets/CONTRACT_PERSISTENCE.md`
- `docs/Markets/KNOWN_ISSUES.md`

---

## 3. Критические инварианты, которые сейчас нарушаются

До дальнейшего рефакторинга необходимо зафиксировать следующие правила.

### 3.1. Offer и active contract — разные сущности жизненного цикла

- `_locationContracts[locationId]` содержит только ID офферов, показанных на доске конкретной локации.
- Принятый контракт удаляется с доски, но не удаляется из хранилища активных контрактов.
- Активный контракт должен находиться в `_playerContracts[playerId]` и разрешаться через стабильное хранилище contract data.
- Регенерация доски не имеет права удалять active, completed или failed contract data.

### 3.2. Active count считает только реально активные контракты

`GetPlayerActiveCount()` и `MaxActiveContractsPerPlayer` не должны считать:

- `Completed`;
- `Failed`;
- истёкшие контракты, уже обработанные `Tick()`;
- отсутствующие/неразрешимые IDs.

Если коллекция содержит только active IDs, её нельзя оставлять с failed/completed IDs после обработки результата.

### 3.3. Completion — атомарная server-side транзакция

Для delivery-контракта в одной серверной операции должны быть проверены и изменены:

1. владелец контракта;
2. статус и deadline;
3. текущая локация/зона;
4. наличие требуемого товара;
5. списание точного количества товара из правильного cargo/warehouse;
6. перевод контракта в `Completed`;
7. начисление награды;
8. публикация события и persistence.

При любой ошибке до commit не должны происходить частичные изменения.

### 3.4. Persistence должна сохранять экономически значимое состояние

Состояние только с debt должно считаться валидными данными. Загрузка не должна генерировать новые контракты поверх существующей debt-информации.

### 3.5. Клиент не должен угадывать значения enum

Сервер отправляет стабильный result code, а UI превращает его в локализованный текст. `code.ToString()` не должен быть пользовательским сообщением.

---

## 4. Findings — функциональные и экономические дефекты

## MKT-CON-001 — активные контракты удаляются регенерацией доски

**Severity:** P0 / data integrity  
**Файл:** `ContractWorld.cs`  
**Участки:** `GenerateContractsForLocation()`, `TryAccept()`, `RequestListRpc()`-flow.

### Наблюдение

`GenerateContractsForLocation()` сначала удаляет из `_availableContracts` все IDs, перечисленные в `_locationContracts[locationId]`, затем генерирует свежие контракты. `TryAccept()` не удаляет принятый ID из `_locationContracts` и не переводит его в отдельное active-хранилище.

### Сценарий воспроизведения

1. Получить список контрактов локации.
2. Принять все отображённые контракты.
3. Оставить `_locationContracts[locationId]` с принятыми IDs.
4. Повторно запросить список.
5. `GetAvailableForLocation()` возвращает пустой/неполный список.
6. Запускается генерация.
7. Старые IDs удаляются из `_availableContracts`.
8. `_playerContracts[playerId]` всё ещё содержит IDs, но объект контракта больше не разрешается.

### Причина

Одна коллекция `_availableContracts` используется одновременно как источник офферов и как registry для active contracts.

### План исправления

1. Временно добавить защиту в `GenerateContractsForLocation()`:
   - не удалять ID, если он присутствует в любом `_playerContracts[playerId]`;
   - не удалять ID со статусом `Active`;
   - логировать нарушение инварианта.
2. В `TryAccept()` удалить ID с board-коллекции сразу после успешного commit.
3. Вынести стабильное хранилище контрактов из board semantics:
   - `_contractsById` — все живые contract records;
   - `_locationOffers[locationId]` — только IDs офферов;
   - `_activeContractsByPlayer[playerId]` — только active IDs.
4. Удаление/retention выполнять только отдельной политикой, а не при генерации доски.

### Acceptance criteria

- Регенерация доски никогда не удаляет active contract.
- Active contract отображается в `ContractClientState` после любого количества запросов списка.
- Повторный `RequestListRpc()` не меняет active/completed/failed records.
- Есть тест на принятие всех офферов и повторный запрос списка.

---

## MKT-CON-002 — истёкшие контракты остаются в active index

**Severity:** P0 / state integrity  
**Файл:** `ContractWorld.cs`  
**Участки:** `Tick()`, `TryComplete()` expired branch, `GetPlayerActiveCount()`.

### Наблюдение

`Tick()` переводит контракт в `Failed`, вызывает `HandleFailedContract()` и собирает ID в `expired`, но не удаляет его из `_playerContracts[playerId]`. В ветке истёкшего timer в `TryComplete()` наблюдается та же проблема.

### Последствия

- failed IDs увеличивают active count;
- игрок может навсегда потерять слот в `MaxActiveContractsPerPlayer`;
- persistence сохраняет устаревший active index;
- после перезапуска сервер может восстановить неактивные IDs как активные;
- UI получает контракт, который уже failed, но находится в активном списке.

### План исправления

Сделать единый метод:

```text
FinalizeContract(contractId, terminalState, failureReason)
```

Он должен:

1. изменить state;
2. удалить ID из active index;
3. применить debt/reward policy;
4. записать историю или terminal record;
5. опубликовать одно событие;
6. сохранить данные.

`Tick()` и `TryComplete()` должны использовать один и тот же путь завершения.

### Acceptance criteria

- После `Tick()` failed contract отсутствует в active count.
- Повторный `Tick()` не выдаёт повторный debt/event.
- Expired `TryComplete()` не оставляет ID в active index.
- Reload не превращает failed record в active.

---

## MKT-CON-003 — delivery completion не проверяет и не списывает cargo

**Severity:** P0 / economic exploit  
**Файл:** `ContractWorld.cs`  
**Участок:** `TryComplete()`; рядом с TODO по cargo validation.

### Наблюдение

Текущая логика проверяет статус, owner, deadline и destination, но не требует наличие нужного количества товара и не списывает cargo.

### Риск

Игрок может завершить Standard/Urgent delivery contract без груза, получить reward и сохранить товар. Это делает contract reward источником бесплатной валюты и нарушает server-authoritative economic model.

### План исправления

1. Через `ContractWorldItemResolver` определить конкретный inventory source:
   - ship cargo;
   - warehouse, если контракт допускает складскую доставку;
   - иной явно разрешённый storage.
2. Ввести единый метод `TryConsumeDeliveryCargo(...)`.
3. Проверять item ID, variant/quality при наличии, quantity и storage owner.
4. Списывать товар до выдачи reward, но в рамках одного transaction boundary.
5. Если текущая repository/inventory API не поддерживает atomic mutation, сначала добавить server-side transactional command или compensating rollback.
6. Добавить telemetry/logging для попыток completion без груза.

### Acceptance criteria

- Без нужного cargo completion получает детерминированный failure code.
- При успешном completion quantity уменьшается ровно на требуемое значение.
- Повторный completion не списывает груз и не выдаёт reward повторно.
- Ошибка списания не меняет status и не выдаёт reward.
- Standard/Urgent/прочие delivery-типы покрыты тестами.

### Реализовано на этапе 4

- Добавлен `TradeWorld.TryConsumeDeliveryCargo(...)` как единая server-side команда списания.
- Перед списанием проверяется суммарное количество `itemId` в трюме указанного корабля и destination warehouse.
- Источник выбирается детерминированно: сначала cargo, затем warehouse; при недостатке количества ничего не изменяется.
- Списанные cargo/warehouse сохраняются до перевода контракта в `Completed` и начисления reward.
- Ошибка persistence выполняет компенсирующее восстановление in-memory и repository snapshots.
- `RequestCompleteRpc` принимает `shipNetworkObjectId`; сервер проверяет присутствие корабля в destination zone и ownership.
- Клиент передаёт текущий корабль владельца; `0` означает completion только из destination warehouse.

### Ограничения этапа 4

- Receipt flow не изменён и остаётся scope `MKT-CON-004`.
- Domain tests, persistence round-trip, Play Mode/network smoke test и screenshots не запускались.

---

## MKT-CON-004 — Receipt contracts реализованы только частично

**Severity:** P0/P1 / incomplete domain behavior  
**Файл:** `ContractWorld.cs`, `ContractData.cs`, inventory integration.

### Наблюдение

В `TryAccept()` прямо отмечено, что выдача cargo для `Receipt` не реализована. В результате Receipt использует debt-on-failure semantics, но не формирует полноценный цикл:

```text
accept → receive cargo → transport → return/submit → consume cargo → settle
```

### План решения

До начала кода нужно выбрать и зафиксировать семантику Receipt:

- что именно выдаётся игроку;
- из какого server storage списывается товар при accept;
- где считается ownership товара во время перевозки;
- что происходит при failure/expiry;
- возвращается ли товар владельцу или превращается в debt;
- требуется ли физическая сдача в destination.

После этого сделать отдельный acceptance flow, не смешивая его с delivery flow. Если Receipt пока не готов, сервер должен явно запрещать его публикацию/accept с отдельным `UnsupportedContractType`, а не создавать неполный экономический цикл.

### Acceptance criteria

- Receipt либо полностью реализован и покрыт тестами, либо недоступен игроку.
- Нет contract type, который можно принять, но нельзя корректно settle.
- Failure policy и cargo ownership описаны в `CONTRACT_PERSISTENCE.md` или отдельном design-документе.

### Реализовано на этапе 5

- Receipt больше не генерируется на доске новых контрактов.
- Старые pending Receipt-офферы не попадают в доступный snapshot и удаляются при следующей регенерации доски.
- `TryAccept()` возвращает `UnsupportedContractType` вместо активации неполного экономического flow.
- Старый активный Receipt нельзя завершить и получить reward; его можно безопасно провалить/отменить по существующей debt policy.

### Ограничения этапа 5

- Полная Receipt semantics (выдача товара при accept, ownership и возврат/settlement) всё ещё не реализована.
- Существующие active Receipt records не мигрируются автоматически при загрузке; игрок должен завершить их через fail/expiry path.
- Localization key для нового кода может потребовать отдельного добавления; текущие блокировки передают конкретное сообщение через result DTO.

---

## 5. Findings — persistence и жизненный цикл данных

## MKT-PER-001 — debts-only save data теряется

**Severity:** P0/P1 / data loss  
**Файлы:** `ContractSaveData.cs`, `PlayerPrefsRepository.cs`, `ServerFileRepository.cs`.

### Наблюдение

`ContractSaveData.HasData` проверяет только `contracts.Count > 0`. Если у игрока нет contract records, но есть debt state, загрузка возвращает false. `ContractWorld.Initialize()` воспринимает это как отсутствие данных, генерирует новые контракты и сохраняет состояние, перезаписывая debt.

### Исправление

`HasData` должен учитывать все значимые поля:

```text
contracts.Count > 0
|| debts.Count > 0
|| location mappings.Count > 0
|| schema/version metadata exists
```

Для пустого, но валидного snapshot должна существовать разница между:

- `NoSaveFound`;
- `ValidEmptySave`;
- `CorruptSave`;
- `UnsupportedSchema`.

### Acceptance criteria

- Игрок только с debt сохраняет и восстанавливает debt после reload.
- Валидный пустой snapshot не вызывает ошибочную regeneration-политику.
- Corrupt/unsupported JSON не затирает старое сохранение.

---

## MKT-PER-002 — completed/failed records не очищаются

**Severity:** P1 / unbounded growth  
**Файлы:** `ContractWorld.cs`, `ContractSaveData.cs`, repository implementations.

### Наблюдение

Completed и failed `ContractData` остаются в `_availableContracts` и JSON. При длительной работе растут memory footprint, save size и время сериализации.

### План исправления

Разделить runtime и history:

- active records — обязательны для runtime;
- current offers — обязательны для board;
- terminal history — ограниченная история или компактные settlement records;
- archived records — optional/offline, если нужен аудит.

Добавить retention policy:

- max age;
- max records per player/location;
- retention only for records referenced by support/audit requirements.

Очистку выполнять после successful persistence, а не перед ней.

### Acceptance criteria

- Размер save не растёт бесконечно при повторной генерации и completion.
- Cleanup не удаляет записи, необходимые для debt/audit/event idempotency.
- Повторная загрузка сохраняет текущие active contracts и ограниченную history.

### Реализовано на этапе 8

- `ContractData` получил `terminalAtUtcTicks`; legacy snapshots без поля остаются совместимыми.
- `ContractSaveData` переведён на schema `2`; schema `0/1` мигрируются к текущей версии.
- `IPlayerDataRepository.SaveContracts()` возвращает `bool`, поэтому retention запускается только после успешной записи.
- `ContractWorld` сохраняет максимум `MaxTerminalRecordsPerPlayer` terminal records на игрока; по умолчанию — `50`.
- Pending/Active records, debts и active indexes не удаляются retention-политикой.

---

## MKT-PER-003 — отсутствует schema version и atomic persistence

**Severity:** P1 / migration and corruption risk  
**Файлы:** `ContractSaveData.cs`, `MarketSaveData`/market persistence, `ServerFileRepository.cs`, `PlayerPrefsRepository.cs`.

### Наблюдение

JSON не содержит явного schema version и migration pipeline. Persistence для market/contract также не выглядит как единый atomic snapshot: часть данных сохраняется раздельно, а credits исторически используют `.txt` и частичный in-memory cache.

### План исправления

1. Добавить `schemaVersion` в root save DTO.
2. Ввести `ISaveMigration<T>` или эквивалентный последовательный migration registry.
3. Сохранять во временный файл/ключ.
4. Проверять deserialize и минимальные invariants.
5. Делать atomic replace/commit.
6. Оставлять backup предыдущего snapshot.
7. Разделить `SaveContracts`, `SaveMarkets`, credits и cargo по чёткой transaction policy либо ввести общий player-economy snapshot.
8. Добавить checksum/length validation, если это соответствует текущей security model.

### Acceptance criteria

- Старый snapshot либо мигрируется, либо отклоняется без потери предыдущих данных.
- Сбой записи не оставляет обрезанный JSON как единственную копию.
- Dedicated server и local repository используют одинаковые DTO/version rules.

### Реализовано на этапе 6

- `ServerFileRepository` принимает legacy snapshots без поля `schemaVersion` как schema `0` и нормализует их к текущей версии: markets — `1`, contracts — `2`.
- Future schema (`schemaVersion > CurrentSchemaVersion`) отклоняется без тихого downgrade.
- При повреждённом основном `markets.json`/`contracts.json` репозиторий пытается прочитать соответствующий `.bak` и восстановить primary snapshot.
- JSON migration write использует тот же atomic temp/replace путь.
- Временный файл теперь очищается и при ошибке записи самого `.tmp`.

### Ограничения этапа 6

- `PlayerPrefsRepository` не имеет filesystem-level atomic rename; для markets/contracts реализован best-effort temp/backup key protocol.
- `IPlayerDataRepository` различает `NoSaveFound`, `Loaded`, `ValidEmptySave`, `CorruptSave` и `UnsupportedSchema`; это закрыто на этапе 7.
- Concurrency lock и единый transaction snapshot для credits/cargo/contracts/markets не реализованы.

---

## 6. Findings — server/network/API

## MKT-NET-001 — raw enum codes отображаются в UI

**Severity:** P1 / UX and API contract  
**Файл:** `ContractServer.cs`; обработчик в `MarketWindow.cs`.

`ContractResultDto_Fail()` использует `code.ToString()` как message для zone/rate-limit failures. В UI появляются технические значения вроде `NotInZone` и `WrongDestination`.

### План

- DTO должен передавать `resultCode`, а не готовый raw message.
- Клиентский mapper переводит code в локализованный string key.
- Server message использовать только для dev diagnostics, не для пользовательского UI.
- Добавить fallback `Trade.Error.Unknown`.

### Acceptance criteria

- Ни один enum name не показывается в release UI.
- Один и тот же code одинаково отображается в `MarketWindow` и `ContractsTab`.
- Missing localization не приводит к пустому сообщению.

---

## MKT-NET-002 — market rate limit молча отбрасывает запросы

**Severity:** P1 / inconsistent network contract  
**Файл:** `MarketServer.cs`.

`MarketServer.CheckRateLimit()` возвращает `false` без `TradeResultDto`. Контрактный сервер при аналогичном ограничении отправляет fail result. Клиент не может отличить rate limit от потери RPC/соединения.

### План

Унифицировать rate-limit policy:

- один result code;
- единый cooldown/retry hint при необходимости;
- одинаковый TargetRpc delivery path через `NetworkPlayer`;
- серверный log только с throttling, чтобы не спамить консоль.

### Acceptance criteria

- Повторный market request получает явный `RateLimited` result.
- UI показывает локализованное сообщение и не меняет client projection как будто операция выполнена.
- Contract и market используют общий rate-limit response contract.

---

## MKT-NET-003 — dead NetworkBehaviour RPCs создают двусмысленность

**Severity:** P2 / maintenance risk  
**Файлы:** `MarketServer.cs`, `ContractServer.cs`, `NetworkPlayer.cs`.

Project-wide search по `Assets` не нашёл ссылок на `ReceiveMarketSnapshotClientRpc`, `ReceiveTradeResultClientRpc`, `ReceiveContractSnapshotClientRpc` или `ReceiveContractResultClientRpc`. Фактическая доставка выполняется через `NetworkPlayer.*TargetRpc` из-за NGO 2.x owner-routing ограничения.

### План

1. Подтвердить отсутствие ссылок через project-wide search и compile references.
2. Удалить dead RPC после подтверждения.
3. Зафиксировать единственный delivery path в `ARCHITECTURE.md`.
4. Добавить integration test на owner-only delivery.
5. Не менять TargetRpc workaround без отдельной проверки NGO 2.x поведения.

### Acceptance criteria

- В коде остаётся один понятный server→owner delivery path.
- Snapshot/result не дублируются.
- Disconnect/despawn не вызывает попытку доставки в уничтоженный `NetworkPlayer`.

### Реализовано на этапе 11

- Из `MarketServer.cs` удалены `ReceiveMarketSnapshotClientRpc` и `ReceiveTradeResultClientRpc`.
- Из `ContractServer.cs` удалены `ReceiveContractSnapshotClientRpc` и `ReceiveContractResultClientRpc`.
- Project-wide `grep` по `Assets` подтвердил отсутствие ссылок на удалённые методы.
- Единственным server→owner delivery path остаются `NetworkPlayer.ReceiveMarketSnapshotTargetRpc`, `ReceiveTradeResultTargetRpc`, `ReceiveContractSnapshotTargetRpc` и `ReceiveContractResultTargetRpc`.
- `check_compile_errors`: **No compile errors** после удаления методов.
- Integration/network smoke test и screenshots не выполнялись.

---

## 7. Findings — UI и boundary ownership

## MKT-UI-001 — дублируется contract UI в CharacterWindow

**Severity:** P1/P2 / conflicting ownership  
**Файлы:** `CharacterWindow.cs`, `CharacterWindow/ContractsTab.cs`.

После переноса контрактов в третий tab `MarketWindow` и выделения `ContractsTab` старый `CharacterWindow` всё ещё содержит:

- `HandleContractSnapshot()`;
- `HandleContractResult()`;
- `ApplyContractFilters()`;
- `MakeContractRow()`;
- `BindContractRow()`;
- `_contractState` subscriptions/unsubscriptions.

### Риск

Две точки подписки и построения UI могут расходиться по фильтрам, loading/error state, обработке snapshot и unsubscribe lifecycle. Исправление в одном месте не исправляет второе.

### План

1. Составить список фактических вызовов каждого legacy method.
2. Удалить мёртвые handlers из `CharacterWindow`.
3. Оставить `CharacterWindow` только как host tab/navigation coordinator.
4. Сделать `ContractsTab` единственным owner contract UI для CharacterWindow.
5. Проверить lifecycle `OnEnable/OnDisable`, repeated open/close и network disconnect.
6. Обновить docs, где ещё указана старая ownership model.

### Acceptance criteria

- В проекте один renderer/handler для каждого contract snapshot/result в CharacterWindow flow.
- Нет двойной подписки на `ContractClientState`.
- Старый `ApplyContractFilters()` удалён, а не оставлен как неиспользуемая копия.

### Реализовано на этапе 12

- `CharacterWindow.cs` больше не содержит contract snapshot/result handlers, legacy filter code, contract row factories или contract action handlers.
- `ContractsTab` остаётся единственным владельцем contract UI внутри `CharacterWindow`.
- `CharacterWindow` сохраняет только host/navigation orchestration и чтение contract projection для общих character stats.

---

## MKT-UI-002 — legacy filter code в CharacterWindow повреждён и должен быть удалён

**Severity:** P1/P2  
**Файл:** `CharacterWindow.cs`, `ApplyContractFilters()`.

В старом методе присутствуют повторяющиеся условия вида `src = src.Where(c => c.state == (byte)ContractState.Pending);` и сложная ветвящаяся логика, которая не соответствует новой ownership model.

Рекомендуемое действие — удалить метод вместе с legacy contract rendering paths. Ремонтировать старый метод имеет смысл только если после dependency audit выяснится, что `CharacterWindow` всё ещё обязан обслуживать отдельный contract list.

### Реализовано на этапе 12

- `CharacterWindow.ApplyContractFilters()` удалён.
- Повреждённые дублирующие условия по `ContractState.Pending` удалены вместе с legacy contract rendering path.

---

## MKT-UI-003 — MarketWindow стал монолитом

**Severity:** P2 / refactorability  
**Файл:** `MarketWindow.cs`.

Один controller обслуживает market, contracts, exchanger и UI state. Уже наблюдались inline style fixes для quantity rows, потому что `styleSheets.count == 0`. Это показывает, что presentation concerns и tab lifecycle трудно тестировать изолированно.

### План

Разделить по tab/view-model boundary:

- `MarketTabController`;
- `ContractsMarketTabController`;
- `ExchangeTabController`;
- общий `MarketWindowHost` для navigation, modal state и shared error/result presentation.

Не смешивать этот UI refactor с исправлением domain transaction semantics. Сначала стабилизировать server state, затем переносить view code.

### Acceptance criteria

- Каждый tab имеет отдельный lifecycle и subscription set.
- Shared state не дублируется между tabs.
- USS подключён явно и проверяется editor/runtime smoke test.
- Удаление одного tab не ломает snapshot других tabs.

---

## 8. Findings — domain extensibility и configuration

## MKT-DOM-001 — hardcoded locations и contract types

**Severity:** P2 / extensibility  
**Файл:** `ContractWorld.cs`.

Найдены hardcoded IDs `primium`, `secundus`, `tertius`, `quartus`, switch в `LocationIdToIndex()` и фиксированный набор трёх contract types. Генерация выбирает один item/destination и производит три type variants, что ограничивает data-driven design.

Дополнительный риск: `MarketConfigCollector.NormalizeLocationId()` возвращает uppercase IDs, а `ContractWorld.LocationIdToIndex()` ожидает lowercase. Нормализация должна быть единой до входа в domain layer.

### План

1. Ввести canonical `LocationId` value object или хотя бы единый `NormalizeLocationId()`.
2. Убрать switch из domain logic.
3. Получать locations/types из validated config/data asset.
4. Добавить validation: уникальность IDs, наличие destinations, допустимые contract types.
5. Перевести generator на `ContractDefinition`/`ContractTemplate`, а не на fixed branches.
6. Сохранить deterministic seed/test seam для тестов.

### Acceptance criteria

- Добавление location не требует изменения `ContractWorld.cs`.
- Uppercase/lowercase input даёт один canonical ID.
- Некорректная конфигурация обнаруживается до запуска live market.
- Генератор позволяет добавлять новый type через data/config layer.

### Реализовано на этапе 15A

- `MarketConfigCollector.NormalizeLocationId()` стал единым runtime-нормализатором: `null`/whitespace → пустой ID, остальные значения → `Trim().ToUpperInvariant()`.
- `MarketConfig`, `TradeWorld`, `ContractWorld`, `MarketZoneRegistry`, `DockingZoneRegistry` и cache keys используют один canonical representation.
- Legacy contract snapshots с lowercase/padded `fromLocationId`, `toLocationId` и location-board mappings нормализуются при загрузке с дедупликацией mapping IDs.
- `ContractWorld` больше не использует `ToLower()` в `LocationIdToIndex()`; current distance table получает canonical IDs.

Этап 15A закрыл canonical normalization. Оставшаяся часть `MKT-DOM-001` закрыта этапом 15B: locations, distances и publishable contract type definitions вынесены в validated `ContractCatalog`.

---

## MKT-DOM-002 — registry cleanup привязан к двум server components

**Severity:** P2 / lifecycle bug  
**Файлы:** `MarketServer.cs`, `ContractServer.cs`, `MarketZoneRegistry.cs`.

Оба `OnNetworkDespawn()` вызывают `MarketZoneRegistry.Clear()`. Если один server component despawn-ится раньше второго, registry может быть очищен, пока другой компонент ещё использует зоны.

### План

Сделать владельца registry явным:

- либо один `MarketZoneRegistryHost` управляет register/clear;
- либо registry использует reference counting/session token;
- либо cleanup вызывается только при уничтожении общего market session root.

### Acceptance criteria

- Despawn одного server component не очищает активные зоны другого.
- Reload scene/session очищает registry ровно один раз.
- Повторная регистрация не оставляет stale entries.

### Реализовано на этапе 14

- `MarketZoneRegistry` хранит владельцев server-сессии отдельно от самих `MarketZone`.
- `MarketServer` и `ContractServer` регистрируют себя как session owners при `OnNetworkSpawn()` и освобождают владение при `OnNetworkDespawn()`.
- Реестр очищается только после освобождения последнего server owner; порядок despawn двух компонентов больше не влияет на активные зоны.
- Публичный безусловный `Clear()` удалён, поэтому отдельный server component не может случайно очистить общий реестр.

---

## 9. Предлагаемая целевая модель данных

Минимальная целевая структура:

```text
ContractCatalog / ContractDefinitions
    └─ immutable definitions and config

ContractRuntimeStore
    ├─ ContractsById
    │   └─ active + terminal records needed for retention/audit
    ├─ LocationOffers
    │   └─ offer IDs only
    ├─ ActiveByPlayer
    │   └─ active IDs only
    └─ TerminalHistory
        └─ bounded compact settlement records
```

### State machine

```text
Offered
  ├─ accept → Active
  ├─ expire/replace → RemovedFromBoard
  └─ admin cleanup → Archived

Active
  ├─ complete → Completed
  ├─ deadline → Failed
  ├─ abandon/cancel → Failed/Cancelled (если поддерживается)
  └─ invalid data → Quarantined, не silently deleted

Completed / Failed / Cancelled
  └─ retention → TerminalHistory / Archived
```

Переходы должны быть централизованы. Нельзя менять `ContractData.state` в нескольких местах без удаления из соответствующих indexes и idempotency guard.

---

## 10. Приоритизированный refactoring backlog

### Sprint 0 — Safety net и воспроизводимость

**REF-0001 — Добавить domain test harness для ContractWorld**

- fake repository;
- fake inventory/cargo;
- deterministic clock;
- deterministic contract generator;
- test locations and players.

**REF-0002 — Зафиксировать invariants checker**

Проверять в development/test builds:

- каждый active ID разрешается в `ContractsById`;
- active ID не находится на location board;
- failed/completed ID не считается active;
- offer не назначен двум игрокам;
- debt не дублируется после повторного event.

### Sprint 1 — P0 state integrity

**REF-1001 — Исправить регенерацию location board**  
Закрывает `MKT-CON-001`.

**REF-1002 — Ввести единый terminal transition**  
Закрывает `MKT-CON-002`.

**REF-1003 — Добавить cargo validation/consume transaction**  
Закрывает `MKT-CON-003`.

**REF-1004 — Запретить или полноценно реализовать Receipt**  
Закрывает `MKT-CON-004`.

### Sprint 2 — Persistence and economy safety

**REF-2001 — Исправить `HasData` и load result semantics**  
Закрывает `MKT-PER-001`.

**REF-2002 — Добавить schema version + migrations**  
Закрывает первую часть `MKT-PER-003`.

**REF-2003 — Atomic save + backup + corruption handling**  
Закрывает вторую часть `MKT-PER-003`.

**REF-2004 — Retention policy для terminal records**  
Закрывает `MKT-PER-002`.

### Sprint 3 — Network/UI contract

**REF-3001 — Result code/localization mapper**  
Закрывает `MKT-NET-001`.

**REF-3002 — Единую rate-limit response policy**  
Закрывает `MKT-NET-002`.

**REF-3003 — Удалить dead RPC после подтверждения ссылок**  
Закрывает `MKT-NET-003`.

**REF-3004 — Удалить дублирующую contract UI из CharacterWindow**  
Закрывает `MKT-UI-001` и `MKT-UI-002`.

### Sprint 4 — Structural refactor

**REF-4001 — Разделить MarketWindow по tabs**  
Закрывает `MKT-UI-003`.

**REF-4002 — Унифицировать LocationId normalization**  
Часть `MKT-DOM-001`.

**REF-4003 — Перевести locations/types на data-driven definitions**  
Закрыто этапом 15B (`ContractCatalog`): locations, distance graph и publishable contract types больше не задаются в `ContractWorld`.

**REF-4004 — Сделать MarketZoneRegistry lifecycle explicit**  
Закрывает `MKT-DOM-002`.

### Sprint 5 — Legacy cleanup and documentation

**REF-5001 — Удалить C1/C5 legacy trade files после project-wide reference audit**

Кандидаты: `ContractSystem.cs`, `ContractBoardUI.cs`, `PlayerTradeStorage.cs`, `TradeMarketServer.cs`, `TradeUI.cs` и другие файлы из legacy inventory. Удалять только после проверки ссылок, scene/prefab references и compile.

**REF-5002 — Обновить documentation catalog**

- `README.md` — указать текущую contract-as-tab архитектуру и persistence status;
- `FILES_INDEX.md` — пересобрать по текущему `Assets/_Project/Trade/Scripts`;
- `KNOWN_ISSUES.md` — закрыть устаревший persistence issue, заменить его реальными unresolved issues;
- migration docs — пометить completed sections;
- ссылки на отсутствующий `MARKETS_V2_AUDIT_2026-06-05.md` заменить на этот документ либо восстановить ссылку только если файл реально нужен.

---

## 11. Порядок реализации и зависимости

```text
REF-0001/0002
      ↓
REF-1001 + REF-1002
      ↓
REF-1003 + REF-1004
      ↓
REF-2001 + REF-2002 + REF-2003
      ↓
REF-2004
      ↓
REF-3001 + REF-3002
      ↓
REF-3003 + REF-3004
      ↓
REF-4001/4002/4003/4004
      ↓
REF-5001/5002
```

Нельзя начинать с удаления legacy UI или переписывания `MarketWindow`, пока не стабилизированы domain state и persistence. Иначе новый UI только скроет рассинхронизацию данных.

---

## 12. Verification plan

### Unit/domain tests

- accept one offer;
- accept all offers at a location;
- request list after accepting all offers;
- regenerate board while active contracts exist;
- tick exactly at deadline;
- tick repeatedly after failure;
- complete without cargo;
- complete with exact cargo;
- complete with insufficient cargo;
- duplicate completion request;
- wrong owner;
- wrong destination;
- Receipt accept/fail/complete;
- max active contract limit after failures;
- debts-only save/load;
- corrupt save fallback;
- migration from previous schema;
- terminal retention.

### Integration/network tests

- owner-only snapshot delivery through `NetworkPlayer`;
- rate-limit result for market and contracts;
- disconnect during completion;
- server restart with active contracts and debts;
- scene/session despawn order for `MarketZoneRegistry`.

### UI smoke tests

- open/close `MarketWindow` repeatedly;
- switch market/contracts/exchange tabs;
- open/close CharacterWindow repeatedly;
- verify only one contract subscription path;
- display each result code through localization mapper;
- verify no raw enum text;
- verify USS loaded for quantity rows and contract rows.

### Required validation before marking P0 complete

1. Unity compile succeeds.
2. Domain tests pass.
3. Dedicated/local repository round-trip passes.
4. Manual Play Mode smoke test passes.
5. Screenshots confirm active contract remains visible after board regeneration.
6. No new errors/warnings in Console related to Trade/Contract.

---

## 13. Documentation corrections required

The following documentation is currently inconsistent with code and must be updated after runtime changes are approved:

| Документ | Исправление |
|---|---|
| `docs/Markets/README.md` | Указать contract v2 как текущую архитектуру, а не pending migration. Добавить ссылку на этот аудит. |
| `docs/Markets/FILES_INDEX.md` | Обновить список `ContractServer`, `ContractWorld`, DTO, Exchange и актуальных UI-файлов. |
| `docs/Markets/KNOWN_ISSUES.md` | Пометить persistence issue как устаревший/исправленный только после проверки; добавить P0 defects из этого аудита. |
| `docs/Markets/CONTRACT_PERSISTENCE.md` | Описать debts-only, schema version, atomic save и retention policy. |
| `docs/Markets/CONTRACT_V2_MIGRATION.md` | Отметить фактически выполненные этапы и оставить только открытые migration tasks. |
| `docs/Markets/CONTRACTS_AS_MARKET_TAB_REFACTOR.md` | Синхронизировать ownership с `ContractsTab` и удалить утверждения о старом `ContractZone` flow. |
| отсутствующий `MARKETS_V2_AUDIT_2026-06-05.md` | Заменить ссылки на этот документ либо восстановить отдельный документ осознанно. |

---

## 14. Open questions перед изменением кода

Эти вопросы нельзя безопасно решить только механическим рефакторингом:

1. Receipt contract должен выдавать физический cargo или только создавать liability/debt?
2. Delivery допускает warehouse delivery или только cargo конкретного ship?
3. Нужно ли хранить полную terminal history для поддержки/аналитики?
4. Должен ли failed contract возвращаться на board как новый offer или всегда получать новый ID?
5. Что является источником истины для market/contract save: отдельные DTO или общий player-economy snapshot?
6. Какие contract types и locations должны быть data-driven уже в текущем milestone?
7. Какой точный NGO 2.x delivery path считается публичным контрактом: только `NetworkPlayer` TargetRpc или допустим fallback?

До ответа на вопросы 1–3 нельзя окончательно фиксировать Receipt и retention implementation.

---

## 15. Итоговая оценка

Система находится в состоянии рабочей v2-архитектуры с несколькими опасными переходными слоями. Главный риск — не UI и не отсутствие отдельных сервисов, а **рассинхронизация индексов контрактов и отсутствие атомарной state transition модели**. Исправление следует начинать с `ContractWorld` и persistence, затем стабилизировать network result contract, и только после этого удалять legacy UI и дробить `MarketWindow`.

На момент первоначального аудита исходники, сцены и ScriptableObjects не изменялись. Ниже зафиксирован первый реализованный этап; остальные тикеты требуют отдельного выполнения и compile/Play Mode проверки.

---

## 16. Реализованный этап 1 — P0 state integrity

**Дата:** 17 августа 2026 г.
**Scope:** `REF-1001`, `REF-1002` и защитная часть `MKT-PER-001`.

### Изменения

- `ContractWorld.GenerateContractsForLocation()` теперь удаляет при регенерации только `Pending` офферы.
- `Active`, `Completed` и `Failed` records не удаляются из `_availableContracts` во время обновления доски.
- Успешный `TryAccept()` сразу удаляет contract ID из `_locationContracts[fromLocationId]`.
- Expired contracts удаляются из `_playerContracts` в ветке `TryComplete()` и в server tick.
- `GetPlayerActiveCount()` учитывает только реально разрешимые `Active` records назначенного игрока.
- При загрузке старого snapshot удаляются stale active-index ссылки.
- `ContractSaveData.HasData` теперь признаёт debts-only, player mapping и location mapping валидными данными.
- Load path защищён от null-списков в старых/неполных JSON snapshots.

### Проверка

- `check_compile_errors`: **No compile errors**.
- Play Mode, domain tests и screenshots не выполнялись; отдельная ручная проверка остаётся обязательной перед закрытием P0.

### Что ещё не закрыто

- cargo validation и списание при delivery completion (`MKT-CON-003`);
- полноценная Receipt semantics (`MKT-CON-004`);
- schema version и atomic persistence (`MKT-PER-003`);
- удаление legacy UI и дальнейшее разделение `MarketWindow`.

---

## 18. Реализованный этап 3 — persistence foundation

**Дата:** 17 августа 2026 г.
**Scope:** базовая часть `MKT-PER-003`.

### Изменения

- `ContractSaveData` получил `schemaVersion` с текущей версией `1`.
- `MarketSaveData` получил `schemaVersion` с текущей версией `1`.
- `MarketSaveData.HasData` стал null-safe для неполных snapshots.
- `ServerFileRepository.SaveMarkets()` и `SaveContracts()` теперь пишут через временный файл и замену target-файла с `.bak` backup при поддерживаемой файловой системе.
- При fallback на файловой системе без `File.Replace` используется overwrite-copy, а временный файл очищается.

### Ограничения этапа

- Миграции schema `0 → 1` пока не требуют преобразования полей, поэтому старые JSON принимаются как legacy snapshots.
- Явное отклонение будущей неподдерживаемой schema version и recovery из `.bak` остаются отдельным тикетом.
- PlayerPrefs не получил файловую atomic-replace семантику: `PlayerPrefs.Save()` остаётся ограничением host-репозитория.

### Проверка

- Первый compile check выявил отсутствие квалификации `PlatformNotSupportedException`; исправлено через `System.PlatformNotSupportedException`.
- Повторный `check_compile_errors`: **No compile errors**.
- Persistence round-trip, corruption recovery и Play Mode не выполнялись.

---

## 17. Реализованный этап 2 — network result contract

**Дата:** 17 августа 2026 г.
**Scope:** `MKT-NET-001` и `MKT-NET-002`.

### Изменения

- `MarketServer.CheckRateLimit()` теперь принимает контекст операции и отправляет `TradeResultDto` с `RateLimited` вместо молчаливого `return false`.
- Rate-limit result использует тот же owner TargetRpc path, что и остальные market operation results.
- `ContractServer.ContractResultDto_Fail()` больше не отправляет `code.ToString()` в `message`.
- Ошибки, созданные через `ContractResultDto_Fail()`, теперь локализуются клиентом по `ContractResultCode` и не показывают raw enum names.
- Server-provided messages из `ContractWorld` сохранены для операций, где требуется конкретный текст (например, лимит активных контрактов или wrong destination).

### Проверка

- `check_compile_errors`: **No compile errors**.
- Play Mode, network smoke test и screenshots не выполнялись.

### Что ещё не закрыто

- полноценная Receipt semantics (`MKT-CON-004`);
- полноценные schema migrations для будущих версий (`MKT-PER-003`);
- удаление dead RPC после подтверждения ссылок;
- удаление legacy UI и дальнейшее разделение `MarketWindow`.

---

## 19. Реализованный этап 4 — delivery cargo validation/consumption

**Дата:** 17 августа 2026 г.
**Scope:** `MKT-CON-003` / `REF-1003`.

### Изменения

- `TradeWorld.TryConsumeDeliveryCargo(...)` проверяет и списывает точное количество delivery item из cargo и/или destination warehouse.
- Нехватка товара возвращает `ContractResultCode.CargoMissing` без изменения cargo, warehouse, contract state и credits.
- Cargo/warehouse persistence выполняется до `ContractData.Complete()` и начисления reward.
- Ошибка записи выполняет compensating rollback и возвращает `InternalError`.
- `ContractServer.RequestCompleteRpc(...)` принимает ship ID и проверяет zone/ownership на сервере.
- `ContractClientState` передаёт текущий ship ID; при `0` completion может использовать только destination warehouse.

### Проверка

- `check_compile_errors`: **No compile errors**.
- Play Mode, domain tests, persistence round-trip, network smoke test и screenshots не выполнялись.

### Что ещё не закрыто

- полная Receipt semantics (`MKT-CON-004`);
- retention terminal records (`MKT-PER-002`);
- future schema migrations/recovery (`MKT-PER-003`);
- dead RPC и legacy UI cleanup.

---

## 21. Реализованный этап 6 — schema migration и backup recovery

**Дата:** 17 августа 2026 г.
**Scope:** оставшаяся безопасная часть `MKT-PER-003`.

### Изменения

- `ServerFileRepository.TryLoadMarkets()` и `TryLoadContracts()` используют общий primary/`.bak` recovery path.
- Legacy schema `0` мигрируется к schema `1` с null-safe списками.
- Future schema versions отклоняются.
- Мигрированный snapshot записывается обратно атомарно.
- `WriteJsonAtomically()` гарантированно удаляет `.tmp` после ошибок.

### Проверка

- Unity compile check: **No compile errors**.
- Persistence round-trip, corrupt-primary recovery, future-schema rejection, Play Mode и screenshots не выполнялись.

### Что ещё не закрыто

- retention terminal records (`MKT-PER-002`);
- PlayerPrefs backup/atomicity limitation;
- concurrency locking и общий transaction boundary;
- dead RPC и legacy UI cleanup.

---

## 20. Реализованный этап 5 — fail-closed Receipt contracts

**Дата:** 17 августа 2026 г.
**Scope:** безопасная часть `MKT-CON-004` / `REF-1004`.

### Изменения

- `ContractWorld.GenerateContractsForLocation()` больше не публикует Receipt-офферы.
- `GetAvailableForLocation()` фильтрует старые pending Receipt-записи.
- `TryAccept()` отклоняет Receipt с `ContractResultCode.UnsupportedContractType`.
- `TryComplete()` не позволяет старому активному Receipt выдать reward.
- `ContractResultCode` получил отдельный код `UnsupportedContractType = 14`.

### Проверка

- Unity compile check: **No compile errors**.
- Play Mode, domain tests, persistence round-trip, network smoke test и screenshots не выполнялись.

### Что ещё не закрыто

- Полная Receipt semantics: accept → receive cargo → transport → settle;
- retention terminal records (`MKT-PER-002`);
- PlayerPrefs backup/atomicity limitation;
- dead RPC и legacy UI cleanup.

---

## 22. Реализованный этап 7 — explicit persistence load status (`MKT-PER-001`)

**Дата:** 17 августа 2026 г.
**Scope:** различение отсутствующего, пустого, повреждённого и неподдерживаемого snapshot.

### Изменения

- `RepositoryLoadStatus` добавлен в `IPlayerDataRepository` и используется для загрузки markets/contracts.
- Оба repository различают `NoSaveFound`, `Loaded`, `ValidEmptySave`, `CorruptSave` и `UnsupportedSchema`.
- Legacy schema `0/1` по-прежнему мигрируются к текущим версиям: markets — schema `1`, contracts — schema `2`; valid-empty snapshot больше не возвращает `false`.
- `ContractWorld.Initialize()` регенерирует доску только при `NoSaveFound`.
- `TradeWorld.LoadAll()` применяет overlay и для valid-empty path без проверки `HasData`.
- После corruption/future-schema rejection world блокирует `SaveAll()`, чтобы не перезаписать исходный snapshot пустым runtime-состоянием.

### Проверка

- Unity compile check: **No compile errors**.
- Play Mode, persistence round-trip, corrupt-primary recovery, future-schema runtime test и screenshots не выполнялись.

### Что ещё не закрыто

- PlayerPrefs backup/atomicity limitation;
- concurrency locking и общий transaction boundary;
- dead RPC и legacy UI cleanup.

---

## 23. Реализованный этап 8 — terminal records retention (`MKT-PER-002`)

**Дата:** 17 августа 2026 г.
**Scope:** ограничение роста `Completed/Failed` contract records.

### Изменения

- `ContractData` хранит `terminalAtUtcTicks`; старые snapshots без этого поля остаются совместимыми.
- `ContractSaveData` переведён на schema `2`; schema `0/1` мигрируются к текущей версии.
- `IPlayerDataRepository.SaveContracts()` возвращает `bool`.
- `ContractWorld` запускает retention только после успешной записи contracts snapshot.
- По умолчанию сохраняется максимум `50` terminal records на игрока.
- `Pending`, `Active`, debts и active indexes retention-политикой не удаляются.

### Проверка

- Unity compile check: **No compile errors**.
- Play Mode, domain tests, persistence round-trip, retention stress test и screenshots не выполнялись.

### Что ещё не закрыто

- concurrency locking и общий transaction boundary;
- dead RPC и legacy UI cleanup.

---

## 24. Реализованный этап 9 — best-effort PlayerPrefs recovery (`MKT-PER-003`)

**Дата:** 17 августа 2026 г.
**Scope:** backup/temp recovery для host persistence markets/contracts.

### Изменения

- `PlayerPrefsRepository` использует primary, `_bak` и `_tmp` keys для market/contract snapshots.
- Snapshot сначала пишется во временный key и сохраняется, затем primary заменяется с сохранением предыдущего значения в backup key.
- При повреждённом primary repository пытается восстановить snapshot из temp, затем из backup.
- Valid-empty, corrupt и unsupported-schema statuses сохраняются в текущем `RepositoryLoadStatus` API.
- Filesystem-level atomic rename для PlayerPrefs недоступен; это best-effort recovery, а не полноценная транзакция.

### Проверка

- Unity compile check: **No compile errors**.
- PlayerPrefs corruption/restart recovery, Play Mode и screenshots не выполнялись.

### Дальнейшие ограничения после этапа 9

- concurrency locking и общий transaction boundary;
- dead RPC и legacy UI cleanup.

---

## 25. Реализованный этап 10 — concurrency lock и transaction policy (`MKT-PER-003`)

**Дата:** 17 августа 2026 г.
**Scope:** сериализация server-side economy mutations и явная policy для cross-key persistence.

### Изменения

- Добавлен `RepositoryTransactionScope` с process-wide re-entrant critical section.
- `IPlayerDataRepository` получил `AcquireTransactionLock()`; compound operations `TradeWorld` и `ContractWorld` выполняются внутри общей блокировки.
- `SetCredits`, `SetWarehouse`, `SetCargo` и `SaveMarkets` теперь возвращают `bool`, поэтому ошибка записи не маскируется как успешная мутация.
- `TradeWorld.SaveAll()` и `ContractWorld.SaveAll()` возвращают результат записи; retention contracts выполняется только после успешного save.
- Delivery completion сохраняет snapshots cargo/warehouse/credits и выполняет compensating rollback при ошибке reward или contracts persistence.

### Transaction policy

- Lock закрывает read/validate/mutate/persist command boundary внутри одного server process.
- `ServerFileRepository` сохраняет atomicity на уровне каждого файла, `PlayerPrefsRepository` — best-effort temp/backup protocol.
- Общего crash-safe файла для credits/cargo/markets/contracts пока нет; при ошибке применяется compensating rollback и запись блокируется там, где нельзя безопасно продолжать.
- `PlayerPrefsRepository` по-прежнему должен вызываться с Unity main thread; lock защищает только concurrent access внутри процесса.

### Проверка

- Unity compile check: **No compile errors**.
- Play Mode, persistence round-trip, concurrent stress test и screenshots не выполнялись.

### Что ещё не закрыто

- legacy UI cleanup.

---

## 26. Реализованный этап 11 — удаление dead server→client RPCs (`MKT-NET-003`)

**Дата:** 17 августа 2026 г.
**Scope:** удаление дублирующих private `[Rpc(SendTo.Owner)]` методов из server singleton компонентов.

### Изменения

- `MarketServer.cs`: удалены `ReceiveMarketSnapshotClientRpc` и `ReceiveTradeResultClientRpc`.
- `ContractServer.cs`: удалены `ReceiveContractSnapshotClientRpc` и `ReceiveContractResultClientRpc`.
- Delivery path не изменён: server-код находит `NetworkPlayer` владельца и вызывает его `*TargetRpc`.
- Поиск по `Assets` не нашёл оставшихся ссылок на удалённые RPC.

### Проверка

- `refresh_unity(mode=if_dirty, scope=scripts, compile=request)` выполнен.
- `check_compile_errors`: **No compile errors**.
- Play Mode, integration/network smoke test и screenshots не выполнялись.

### Что ещё не закрыто

- разделение монолитного `MarketWindow` (`MKT-UI-003`);
- canonical `LocationId` и data-driven locations/types (`MKT-DOM-001`);
- явный lifecycle `MarketZoneRegistry` (`MKT-DOM-002`);
- полная Receipt semantics и localization для `UnsupportedContractType`.

---


## 27. Реализованный этап 12 — устранение дублирующего contract UI в `CharacterWindow` (`MKT-UI-001/002`)

**Дата:** 17 августа 2026 г.
**Scope:** оставить `ContractsTab` единственным владельцем contract UI в `CharacterWindow`.

### Изменения

- Из `CharacterWindow.cs` удалены legacy `ContractDto` cache/list state, contract action handlers, snapshot/result handlers и duplicate row factories.
- Удалён повреждённый `ApplyContractFilters()` с дублирующими условиями `ContractState.Pending`.
- Удалена дублирующая подписка `CharacterWindow` на `ContractClientState`; подписка и кнопки остаются в `ContractsTab`.
- `CharacterWindow` больше выполняет только host/navigation orchestration и читает `ContractClientState` для общих статистик персонажа.
- Project-wide поиск по `CharacterWindow.cs` не находит старые contract handlers, `ApplyContractFilters`, duplicate contract cache или legacy action methods.

### Проверка

- `validate_script(CharacterWindow.cs)`: ошибок нет; присутствуют только два прежних performance warnings вне scope.
- `refresh_unity(mode=if_dirty, scope=scripts, compile=request)` выполнен.
- `check_compile_errors`: **No compile errors**.
- Play Mode/UI smoke test не выполнялись; stale refresh после accept/fail нужно проверить после завершения `MKT-UI-003`.

### Что ещё не закрыто

- разделение монолитного `MarketWindow` (`MKT-UI-003`);
- повторная UI-проверка accept/fail/complete без переключения вкладок;
- canonical `LocationId` и data-driven locations/types (`MKT-DOM-001`);
- явный lifecycle `MarketZoneRegistry` (`MKT-DOM-002`);
- полная Receipt semantics и localization для `UnsupportedContractType`.

---

## 28. Реализованный этап 13 — разделение `MarketWindow` (`MKT-UI-003`)

**Дата:** 17 августа 2026 г.
**Scope:** разбиение монолитного market UI host и исправление refresh после contract result.

### Изменения

- `MarketWindow.cs` оставлен совместимым facade с прежним `MarketWindow.Instance` и публичными `Show/Hide/Toggle/IsVisible` API.
- `MarketWindowHost.cs` владеет UIDocument, общим feedback, modal visibility и navigation между вкладками.
- `MarketTabController.cs` владеет вкладками Рынок и Склад/Трюм, trade actions, quantity controls и per-ship cargo projection.
- `ContractsMarketTabController.cs` владеет contract list/actions и обновляет список после каждого `ContractResultDto`, включая failure result.
- `ExchangeTabController.cs` владеет inventory/warehouse exchange UI, Pack/Unpack actions и subscriptions.
- Исправлена индексация выбранного market item при включённом фильтре «мои товары»: действие использует текущий `itemsSource`, а не исходный snapshot array.
- После accept/complete/fail contract controller явно вызывает `RequestList()`, поэтому UI больше не требует переключения вкладок для получения свежей projection.

### Проверка

- `check_compile_errors`: **No compile errors**.
- Play Mode, UI smoke test и screenshots не выполнялись; требуется отдельная ручная проверка accept/fail/complete без переключения вкладок.

### Что ещё не закрыто

- полная Receipt semantics и localization для `UnsupportedContractType`;
- Play Mode/network/persistence verification pass для этапов 4–13;
- canonical `LocationId`, data-driven locations/types (`MKT-DOM-001`);
- явный lifecycle `MarketZoneRegistry` (`MKT-DOM-002`).

---

## 29. Реализованный этап 14 — explicit lifecycle `MarketZoneRegistry` (`MKT-DOM-002`)

**Дата:** 18 августа 2026 г.
**Scope:** `REF-4004` / lifecycle safety для общего реестра зон.

### Изменения

- Добавлен reference-counted session ownership через `AcquireServerSession()` / `ReleaseServerSession()`.
- `MarketServer` и `ContractServer` больше не вызывают безусловный `MarketZoneRegistry.Clear()` в `OnNetworkDespawn()`.
- Реестр очищается ровно один раз — после освобождения последнего server owner.
- Клиентская регистрация зон и `LocalPlayerZone` сохраняются, пока существует хотя бы один server owner; отдельный despawn `MarketServer` или `ContractServer` не ломает второй компонент.

### Проверка

- Исходники изменены только в `MarketZoneRegistry`, `MarketServer`, `ContractServer`.
- Первый compile check выявил устаревший `GetInstanceID()` в Unity 6; владельцы registry переведены на reference-based `HashSet<UnityEngine.Object>`.
- После исправления Unity editor отключился во время domain reload, поэтому финальный `check_compile_errors` не удалось получить через bridge; `git diff --check` пройден.
- Play Mode, scene/session despawn-order smoke test и screenshots не выполнялись.

### Что ещё не закрыто

- Полная Receipt semantics и localization для `UnsupportedContractType`.
- Data-driven locations и contract types (`MKT-DOM-001`).
- Отдельный runtime storage `ContractsById` / `LocationOffers` / `ActiveByPlayer` / `TerminalHistory`.
- Полный verification pass этапов 4–14.
- Legacy cleanup и документационный каталог (`REF-5001/5002`).

---

## 30. Реализованный этап 15A — canonical `LocationId` normalization (`MKT-DOM-001`)

**Дата:** 18 августа 2026 г.
**Scope:** безопасная часть `REF-4002`: единое представление location IDs без изменения scene YAML.

### Изменения

- `MarketConfigCollector.NormalizeLocationId()` теперь обрабатывает `null`, пробелы и различия регистра одинаково.
- `MarketConfig.OnValidate()` использует тот же helper, поэтому новые и изменённые `MarketConfig` сохраняются в canonical uppercase form.
- `TradeWorld` хранит canonical location IDs в `MarketState` и warehouse cache.
- `ContractData.Create()` и `ContractWorld.LoadAll()` нормализуют маршрутные IDs для новых и legacy records.
- `ContractWorld` использует canonical location list для текущей distance table и больше не зависит от lowercase switch.
- `DockingZoneRegistry` переведён с собственного `Norm()` на общий helper.
- `MarketServer` использует canonical location ID в selected-ship cache key.

### Проверка

- `refresh_unity(scope=scripts, compile=request)` выполнен.
- `check_compile_errors`: **No compile errors**.
- Поиск по `Assets/_Project/Trade/Scripts` не находит `locationId.ToLower()` или lowercase switch в `ContractWorld`.
- `git diff --check` выполняется перед коммитом.
- Play Mode, NPC trade smoke test и screenshots не выполнялись.

### Что ещё не закрыто

- Полный verification pass этапов 4–15B.
- Полная Receipt semantics и localization для `UnsupportedContractType`.
- Разделение runtime storage контрактов на `ContractsById` / `LocationOffers` / `ActiveByPlayer` / `TerminalHistory`.

---

## 31. Реализованный этап 15B — data-driven contract catalog (`MKT-DOM-001` / `REF-4003`)

**Дата:** 18 августа 2026 г.
**Scope:** locations, route distances и publishable contract type definitions без изменения scene YAML.

### Изменения

- Добавлен `Assets/_Project/Trade/Scripts/Config/ContractCatalog.cs` — validated ScriptableObject-каталог.
- Добавлен `Assets/_Project/Trade/Resources/ContractCatalog.asset` с текущими четырьмя локациями, GDD_25 distance graph и `Standard/Urgent` publishable definitions.
- `ContractWorld` получает каталог при инициализации и генерирует варианты циклом по publishable definitions; hardcoded `DefaultLocationIds`, fixed distance matrix и generator branches удалены.
- `ContractData.CreateConfigured()` принимает reward/time parameters из catalog definitions; старый `Create()` сохранён как compatibility wrapper.
- `ContractServer` загружает каталог из инспектора или `Resources/ContractCatalog`; при отсутствии используется проверенный runtime fallback.
- Receipt остаётся fail-closed: catalog validation запрещает publishable Receipt definitions.

### Проверка

- `refresh_unity(scope=scripts, compile=request)` выполнен.
- `check_compile_errors`: **No compile errors**.
- `ContractCatalog.asset` найден в Unity AssetDatabase.
- Scene YAML и serialized scene references не изменялись.
- Play Mode, NPC trade smoke test и screenshots не выполнялись.
