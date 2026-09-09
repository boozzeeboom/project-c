# T-FO05E — session orchestration, checkpoint cadence и безопасный выход

Дата: 2026-09-09. Baseline: `685826ac` (D, 620 pure PASS).

**Итог:** код управления подготовленной global-сессией подключён к существующей global-profile ветке NetworkManagerController. Explicit peer identity/policy → deferred D spawn plan → confirmed player → C/B checkpoint; штатный global stop требует подтверждённого checkpoint либо явного abandon. **Compile PASS; 58 новых + 620 прежних = 678 pure PASS / 0 FAIL.**

Это не включение новой архитектуры в текущей игре. Компонент/конфигурация E, profiles/catalog/markers/frames не назначались; scene/prefab assets не менялись. Реальные auth/NGO/native source/GUI/disk/save операции не вызывались.

## 1. Подтверждённые исходные границы

Прочитаны реальные `NetworkManagerController`, `GlobalMotionNetworkStartup`, D/G source/factory, C collector, `PlayerPositionServer`, участки `ShipPositionServer`, `NetworkPlayer` и EscMenu callsite.

- NMC уже имеет `_globalMotionProfile` и `_globalSpawnBootstrap`, а не внешний выбор «сетевого профиля через Steam». При unassigned/disabled global profile остаётся legacy путь с отдельным reserved-protocol guard.
- GlobalMotionNetworkStartup уже владеет approval callback и 72-byte compatibility hello. `Approve` проверяет contract/content, не account credentials. Existing approval/payload отклоняются как требующие явной auth composition. E **не заменяет callback и не добавляет account ID в hello**.
- Исследование account/auth по imported Assets и текущему player/network пути не подтвердило конкретный доверенный provider. Результат **inconclusive**, не утверждение об отсутствии любой внешней системы. Display name, PlayerPrefs key и NGO clientId не признаны account identity.
- `ShipPositionServer` подписывается на OnServerStarted из **Awake**, а отписывается при уничтожении. Поэтому `enabled=false` не доказывает, что legacy restore/save не будет вызван.
- NMC вызывает legacy `PrepareForServerStart` перед Host/Server и `SaveNow` перед MainMenu shutdown. Ранее прочие deliberate disconnect/reconnect пути сразу делали Shutdown.
- NetworkPlayer global actor блокировал legacy restore только после ожидания старых services до 30 секунд.
- EscMenu.Hide + `_exitInProgress=true` ожидали только успешный callback; отказ checkpoint при выходе без отдельного callback оставлял меню зависшим.

Справка по NGO approval, использованная как дополнение к установленному коду: `https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/connection-approval.html`. Compatibility approval не выдается за аутентификацию, а payload не считается защищённым каналом credentials.

## 2. Новые файлы

- `Assets/_Project/Scripts/World/FloatingOrigin/Persistence/GlobalSessionPersistencePolicy.cs`
  - immutable explicit `GlobalSessionPeerPlan`;
  - bounded `GlobalSessionTiming`;
  - phase/save statuses;
  - role, legacy-service, repository, peer-reference/session-token и stop gates;
  - monotonic checkpoint scheduler с backoff и latched write failure.
- `.../Persistence/GlobalMotionSessionCoordinator.cs`
  - concrete MonoBehaviour orchestration без автоматической установки/настройки;
  - pre-start configuration и связывание D;
  - trusted peer receipt intake и обработка после World/frame readiness;
  - C/B cadence и pre-stop checkpoint;
  - cancellation/retirement/failure diagnostics.
- `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalSessionOrchestration.cs`
  - 58 pure checks, не запускающих E или реальные NGO objects.

Точечно изменены NMC, G bootstrap, D source, NetworkPlayer coroutine и EscMenu callback. Не менялись A/B save formats, F005 wire/protocol, packages, legacy ShipPosition source-файлы или реальные сохранения.

## 3. Порядок подготовки и identity

E требует **явного** `ConfigurePreparedSession(frames, replicaFrameId, repository, timing, trustedHostPlan)` на Unity thread до network start. Prepared scenes/physics/frames должны реально существовать; E не загружает их, не создаёт origin и не выбирает directory/store по умолчанию.

NMC global start теперь:

1. Требует active E на том же manager GameObject.
2. Вызывает `PrepareNetworkStart`, проверяющий fresh configuration, matching G/D source и role.
3. Проверяет отсутствие **любых loaded ShipPositionServer/PlayerPositionServer**, включая inactive/disabled. Они не удаляются автоматически — текущая Bootstrap с этими services будет честно блокировать global start до отдельной миграции.
4. На Host/Server инспектирует явно предоставленный B repository, допускает только Ready/Empty. Клиент не требует server repository.
5. На Host требует заранее предоставленный trusted Host PlayerId/spawn policy. Никакого `player_ + clientId`.
6. Конфигурирует D, затем существующий GlobalMotionNetworkStartup выполняет contract/catalog/native validation и G/I preparation.
7. False/exception при старте освобождает prepared lease; отказ cleanup помечается и запрещает повторное использование, а не игнорируется.

Remote identity приходит только через `SubmitVerifiedPeer(expectedSessionToken, actualNetworkClient, trustedPeerPlan)`. Нужны текущий случайный session configuration token, exact reference в ConnectedClients, ещё отсутствующий PlayerObject, явный prepared frame, допустимые rules и уникальный PlayerId. Старый async auth result другой сессии или connection instance не принимается. Request является **результатом работы доверенного caller**, не реализованной аутентификацией.

Host callback может предшествовать World.Started. E не регистрирует native frame раньше времени: identity/request ставится в очередь; есть fallback после World startup только на уже configured trusted Host plan. Role matrix исключает запуск другого NGO role поверх подготовленной роли.

В Update после G (E execution order 10000) выполняются до четырёх подготовок peer plans за кадр. D authorization/plan вызываются после получения actual server session/run и регистрации выбранного frame в World. Несогласованные policies не получают implicit fallback; remote peer отклоняется, Host error останавливает сессию.

Нельзя считать эти API завершённым login, UGS/Steam или auth transport. Внешний доверенный issuer и actual integration данных аккаунта остаются открытой зависимостью. Compatibility-approved peer без verified identity не получает player и ограничен существующим G timeout.

## 4. Confirmation и периодические checkpoint

D хранит actual confirmed PlayerObject и spawn lifetime после successful initial placement. Дополнительно перепроверяется точный accepted sample после C identity handoff: callback не может незаметно сменить исходную точку. `TryGetConfirmedPlayer` требует ту же identity/reservation/connection, тот же PlayerObject и actor lifetime.

E создаёт C coordinator после actual World session start. Save cadence:

- timings задаются явно: interval 1–3600 s, sample age и plan lifetime >0 и ≤60 s, bounded retry backoff;
- нет записи немедленно при старте;
- полный current ConnectedClients roster должен иметь accepted identity, D resolution и confirmed player; pending/unverified/unready peers не молча пропускаются;
- нет live peers — existing offline snapshots сохраняются без выдуманной empty capture;
- source readiness/capture preparation failure возвращает Deferred с backoff;
- C immutable batch + B CAS используются без изменения схем;
- **Applied — единственный успешный commit**, включая подтверждённую публикацию после exception;
- Conflict/NotApplied/Pending/RecoveryRequired/Indeterminate/Unavailable блокируют cadence до явного retry. Нет автоматических quarantine/recovery/default overwrite;
- UTC берётся из server DateTimeOffset, монотонный runtime clock управляет cadence. Более старый UTC не клэмпится: C отклоняет замену новой записи старой;
- `TryCheckpointNow(explicitRetryAfterFailure, ...)` — явная операция; retry не удаляет Pending и не исправляет corruption.

E повторно проверяет отсутствие legacy position services при записи. Появление непроверенного legacy writer после запуска не является поддерживаемым dynamic-content сценарием; полный closed-world/dynamic lifecycle gate по-прежнему обязателен. Этот этап не доказывает отсутствие любых сторонних writers во всех ещё не осмотренных сценах.

Полный roster policy может задержать checkpoints готовых игроков, пока новый peer не подготовлен. Настоящая auth-before-admission composition и более широкая persistence policy требуют отдельной интеграции; E не скрывает эту ограниченность.

## 5. Stop, failure и меню

Все deliberate NMC disconnect/reconnect/restart paths используют `TryStopNetworkSession`. Для owned global server session перед Shutdown:

- требуется successful current checkpoint;
- либо реально нет live PlayerObject, из которого возможно снять final sample — old checkpoints остаются, новый final snapshot не выдумывается;
- либо caller явно выбрал `DisconnectWithoutGlobalCheckpoint` / `ShutdownForMainMenuWithoutGlobalCheckpoint`.

При отказе обычный stop не продолжается автоматически. Last status/error/transaction доступны для диагностики. Уже начатый emergency/transport shutdown не переименовывается в чистый checkpointed stop; E хранит `LastStopWasUnplanned`. Неожиданное отключение peer после despawn нельзя превратить в final capture — остаётся последний periodic checkpoint. Полный individual pre-disconnect final-save/resumption/respawn lifecycle ещё открыт.

Global paths не вызывают старые PrepareForServerStart/SaveNow/ClientSceneLoader.ResetForMainMenu. Legacy branch без global profile сохраняет прежний путь. Старые persistence компоненты не отключались и не удалялись в этом этапе.

`ShutdownForMainMenu` получил отдельный optional `onBlocked` callback. EscMenu сбрасывает `_exitInProgress` и открывает уже существующее меню при блокировке. Не вызывается ложный OnReturnedToMainMenu, нет новой layout/строк локализации/кнопки автоматического abandon. Actual GUI не тестировался.

Для global session ожидание teardown учитывает ShutdownInProgress. По таймауту вызывается blocked callback, а не successful completion. Новый native scene unload/reset controller здесь не реализован: G/I release и последующая fresh configuration остаются обязательными.

## 6. Проверки

Компиляция: **No compile errors**. Два read-only review подтвердили startup/peer/save/stop fences, отсутствие fake auth и исправление blocked-menu deadlock. После дополнительного teardown-timeout guard compile и все pure suites повторены.

`Temp/Aura/ValidateFo05E.cs` → `05E_STATIC_VALIDATION.json`:

- E policies/schedule/receipt/stop + real C/B memory model: **58 PASS / 0 FAIL**.
- Все прежние suites D/C/B/A/I/H/G/F/E-contracts/actor/application/transport/protocol/foundation: **620 PASS / 0 FAIL**.
- **Всего 678 PASS / 0 FAIL.**

Проверены чистые immutable policies, role matrix, explicit host input, no-legacy gate, ref/token identity, timing/clock bounds, fault latch/manual retry, Applied-only success, stop-vs-abandon, deferred readiness, exact global doubles/offline retention, partial staging, post-publication exception, Pending/quarantine, CAS и compiled native/menu seams.

Native E/D/C/G, реальный NetworkClient lifecycle, UI reopening, настоящие storage/root paths и callbacks не вызывались. Проверки не создают GameObjects/scenes/NetworkObjects и не являются runtime PASS.

Read-only asset guard: **58 candidate prefabs**; opt-in/profile assets/loaded global profiles/adapters/markers/executors/checkpoint sources/session coordinators=**0**. Scene/prefab/profile/catalog assets и реальное save-содержимое не читались/не менялись ради runtime-проверки; только предусмотренный prefab guard читал asset metadata. Play Mode/physics/network/screenshots/builds не запускались.

## 7. Что осталось после E

Закрыт **код orchestration**, а не actual session configuration или gameplay gate. Следующий практический участок — подготовка и проверка конкретного pilot scope в основном build:

1. Подтверждённый account identity issuer/его trusted calls в E; explicit Host/peer policies и store ownership/path/configuration. Нынешний поиск account provider inconclusive.
2. Reviewed catalog/markers/prepared frames/physics и миграция выбранного player prefab/profile; отсутствие старых persistence instances, конкурирующих writers и неподдержанных participants.
3. Реальный startup/retirement/native readiness + filesystem acceptance с безопасной backup/migration процедурой. Не перезаписывать unified ShipPositions.json.
4. Пользовательский fixed-frame Host+client прогон до настоящего rebase; дальнейшие client shift, server regions, ships/ParentLocal, nav/AI, RPC/DTO, graphics/global height и regression gates.

Оценка из §8 D остаётся ориентиром, а не механическим обратным счётчиком: E реализовал одно связующее звено, но неизвестный auth/content/native scope не уменьшился на фиксированный процент. **678 pure tests не измеряют готовность полной интеграции.** Global mode/world shift всё ещё выключены; jitter fixed не заявляется.

Один commit кода/meta/результатов/этого отчёта/roadmap/существующего ITERATIONS. TMP fallback, Temp runner, historical A–D reports/JSON, scenes/prefabs/profile/catalog, packages и legacy save files исключены. Без push/rebase/YAML edits.
