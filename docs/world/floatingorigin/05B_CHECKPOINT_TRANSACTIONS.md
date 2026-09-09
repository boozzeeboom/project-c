# T-FO05B — транзакционный repository глобальных player checkpoints

Дата: 2026-09-09. Baseline: `67b6f37a`. **Код repository и отдельного disk adapter реализован, к игре не подключён. Compile PASS; 68 новых + 434 прежних = 502 pure PASS / 0 FAIL. Native filesystem, реальные saves и crash/power-loss поведение не проверялись.**

## 1. Scope и источники

A создал fileless player checkpoint codec и reviewed legacy import draft, но не storage/backup/restore integration. B добавляет executable repository с настоящим дисковым adapter и fault-injected in-memory storage для проверки протокола. Это подготовка зависимости genuine source G, не прохождение T-FO04 runtime gate.

Проверены:

- `Assets/_Project/Scripts/Core/ShipPosition/ShipPositionRepository.cs`: единый старый ships+players JSON, прямой File.WriteAllText. Этот repository не менялся.
- `Assets/_Project/Trade/Scripts/Repository/ServerFileRepository.cs:329–354`: pending → File.Replace с backup / File.Move при первой записи. Найденный File.Copy-overwrite fallback не перенесён: он не сохраняет требование publication без разрушительного overwrite.
- Live reflection подтверждает File.Replace(source, destination, backup, bool) и FileStream.Flush(bool).
- Microsoft File.Replace docs: source/destination должны быть на одном томе; существующий destinationBackupFileName заменяется прежним destination. Все owned файлы B — siblings одного явно заданного directory. Microsoft Flush(bool) описывает flush промежуточных buffers; это не универсальное доказательство durable directory metadata при отключении питания.

Первоисточники: `https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace`, `https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush`.

## 2. Файлы и отсутствие активации

Новые runtime helpers в `Assets/_Project/Scripts/World/FloatingOrigin/Persistence/`:

- `GlobalPlayerCheckpointSnapshot.cs`: immutable all-player snapshot + versioned envelope codec.
- `GlobalPlayerCheckpointStorage.cs`: storage interface и **DirectoryGlobalPlayerCheckpointStorage**.
- `GlobalPlayerCheckpointRepository.cs`: Inspect/Commit/Recover/Quarantine, observation tokens и transaction results.

Новый Editor validator: `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalCheckpointTransactions.cs`.

Нет MonoBehaviour/автоинициализации, default persistentDataPath, вызова SaveAll, присоединения к collector/restore/NGO, legacy data conversion, scene/profile/prefab edits или пакетов. A v1 codec/DTO/import не менялись. Никакие реальные данные сохранений не читались и не записывались.

## 3. Snapshot — цельный player-only набор

`GlobalPlayerCheckpointSnapshot` хранит StoreId, Revision, fresh CommitId, ParentCommitId и immutable отсортированный по PlayerId набор A records. Дубликаты/null/невалидная lineage отклоняются. Revision>=1, parent пуст только для первой revision; bounded count=4096.

Envelope `projectc.global-player-snapshot`, schemaVersion=1: canonical UTF8 JSON, records содержат canonical A v1 checkpoint strings; SHA256 защищает всю scope/lineage/collection. Encode/decode не пропускает missing/unknown/duplicate/unsorted fields, unsupported schemas или чужой StoreId. Максимум 8 MiB, shape/depth/UTF8 guards. Frozen v1 envelope/checksum test закрепляет формат; A golden tests остаются отдельными.

CommitId/ParentCommitId — **storage transaction identity**, не NGO id, authority или durable frame. Recovery создаёт свежий CommitId даже при совпадении revision number с потерянной веткой, поэтому старый optimistic-write token не становится действительным снова (ABA). Revision — последовательность данной проверенной lineage, не глобальное время/epoch всех возможных восстановлений.

Это полный snapshot **игроков**, не unified ships+players save. Не разрешено писать его поверх ShipPositions.json. Authoritative global collection/persistent account mapping по-прежнему предоставляет будущий доверенный server caller; codec/checksum не доказывает authentication.

## 4. Concrete disk adapter

Caller явно предоставляет canonical absolute local directory. Пустые/relative/noncanonical/root/UNC пути запрещены; reparse-point ancestors/leaf paths отвергаются. Используются только фиксированные имена:

- `global-player-checkpoints.v1.primary.json`
- `global-player-checkpoints.v1.backup.json`
- `global-player-checkpoints.v1.pending.json`
- `global-player-checkpoints.v1.lock`
- `global-player-checkpoints.v1.quarantine-{transactionId}.json`

PlayerId и StoreId никогда не подставляются в filename. Отдельный `.lock` открыт с FileShare.None; lease synchronous, thread-owned и non-reentrant. Разные cooperative processes/instances обязаны пользоваться этим lease. AcquireLease при реальном вызове создаёт directory/lock, если нужно; Inspect с native adapter поэтому не обещает абсолютно нулевых служебных disk writes. **Во время этого этапа native AcquireLease не вызывался.**

Pending создаётся через FileMode.CreateNew, без перезаписи найденного pending; payload Write → Flush(true) → Close. Publication: File.Replace с previous backup при существующем primary либо File.Move при первой записи. Нет File.Copy overwrite, File.Delete-before-move, удаления backup или fallback при PlatformNotSupported. При неподдерживаемой замене операция прекращается с сохранением доступных primary/pending evidence.

Read возвращает null только для FileNotFoundException; permission/IO/DirectoryNotFound/oversize не означают «сохранений нет». Чтение bounded и проверяет полный размер.

**Границы native guarantees:** требуется local filesystem с корректными sharing/Replace/Move semantics. Проверки UNC/reparse не доказывают свойства всех mapped/network/cloud/sync mounts и не защищают от non-cooperative external process, игнорирующего lease. Нет portable directory fsync, native crash/power-loss proof, тестов NTFS/IL2CPP/других ОС. No automatic garbage collector для quarantines; их retention/ручная диагностика — отдельная политика. Lock-файл остаётся, handle освобождается.

## 5. Observation, публикация и stale-write guards

`Inspect()` под lease читает primary/backup/pending, проверяет snapshot/backup lineage и выдаёт repository-instance-bound observation: уникальный owner + fingerprint **всех трёх raw byte sequences и фактов отсутствия**. Нельзя использовать token другого repository или snapshot, который изменился после Inspect.

| Status | Значение |
|---|---|
| Empty | Payload-файлов нет; это НЕ успешно сохранённый пустой список |
| Ready | Проверенный primary и требуемая previous-backup lineage; только здесь открыт active Snapshot |
| Pending | Есть оставшийся pending; автоматически не используется, active Snapshot не выдаётся |
| RecoveryAvailable | Primary missing/corrupt, backup известного формата пригоден; выдаётся только RecoveryCandidate |
| Blocked | Чужой/неподдерживаемый формат, broken backup/lineage, corruption без backup либо IO failure; нет default empty data |

`TryCommit(expected, allPlayers, authorizePlayerRemovals=false)`:

1. Проверяет принадлежность observation и fingerprint под storage lease.
2. Принимает только Empty/Ready; формирует новый immutable snapshot и новую transaction identity.
3. Отклоняет исчезновение прежних PlayerId без явной removal authorization — active-player-only collector не может молча стереть offline checkpoints. Более старый SavedAtUnix для того же PlayerId отвергается; равная секунда допустима.
4. Создаёт pending, повторно читает/сравнивает staged bytes, primary и backup.
5. Выполняет native publication и заново читает результат, даже если операция бросила exception.
6. Applied выдаётся только при проверенном новом primary, отсутствии pending и точном сохранении предыдущего primary в backup.

Это не authorization API: право передать `authorizePlayerRemovals=true`, records и StoreId должен обеспечить server caller. Ни auth, ни merging/offline collection logic B не выдумывает.

## 6. Явное recovery и quarantine

`TryRecoverBackup(expected)` допускается только для RecoveryAvailable. Backup не принимается автоматически при Load и не становится пустым fresh store. Foreign/future schemas не downgrade-ятся через known backup. Recovery создаёт **новый** snapshot с backup как parent, сохраняет known-good backup без изменения и помещает displaced corrupt primary bytes в новый quarantine через File.Replace. При missing primary — File.Move, backup сохраняется.

`TryQuarantinePending(expected)` явно переносит stale/partial/unknown pending в уникальный quarantine без overwrite/deletion. Проверяются сохранённые exact bytes и неизменность primary/backup. Даже полностью валидный pending не публикуется только потому, что он разбирается. После quarantine требуется свежее observation.

Transaction statuses:

- **Applied**: операция фактически подтверждена повторным чтением; exception после факта допускает Applied с diagnostic warning.
- **NotApplied**: primary/backup остались прежними либо запрос отклонён до записи. Pending может остаться и требует отдельного quarantine.
- **Conflict**: чужой/stale observation.
- **Unavailable**: IO/lease failure до попытки write.
- **Indeterminate**: запись могла произойти, но outcome невозможно подтвердить (например, потерян read access). Нельзя считать rollback и blindly retry старым token.
- **RecoveryRequired**: наблюдаемые payload/backup/quarantine invariants после операции не выполнены.

Applied при Quarantine означает успешный quarantine, а не commit нового primary. Повреждённый/чужой backup и некоторые blocked states намеренно требуют внешнего operator recovery; универсальный repair всех возможных файлов не заявляется.

## 7. Фактическая проверка

Compile: **No compile errors**. Static review проверил ownership/lineage/fallback/exception границы; blocking compile/safety defects не выявлены.

`Temp/Aura/ValidateFo05B.cs`, только стабильный Edit Mode:

| Suite | PASS | FAIL |
|---|---:|---:|
| ValidateGlobalCheckpointTransactions | 68 | 0 |
| ValidateGlobalPlayerPersistence | 69 | 0 |
| ValidateGlobalSceneExecution | 31 | 0 |
| ValidateGlobalSceneCatalog | 56 | 0 |
| ValidateGlobalMotionSpawn | 44 | 0 |
| ValidateGlobalMotionHierarchy | 40 | 0 |
| ValidateGlobalMotionNetworkContracts | 48 | 0 |
| ValidateGlobalMotionActorReadiness | 26 | 0 |
| ValidateGlobalMotionApplication | 32 | 0 |
| ValidateGlobalMotionTransport | 32 | 0 |
| ValidateGlobalMotionProtocol | 33 | 0 |
| ValidateFloatingOriginFoundation | 23 | 0 |
| **Total** | **502** | **0** |

Тестируется исполняемый repository против **in-memory fault model**, а не настоящий диск: staging before/partial/corrupt, exception before/after publication, unsupported replace, повреждённые head/backup, потеря read access после публикации, stale/reentrant writers, explicit quarantine/recovery, ABA, lineage/format/size/golden, retention offline players и timestamp guards. Проверка path constructors не вызывает native IO.

Guard: 58 prefab candidates; opt-in/profile assets/loaded assigned profiles/adapters/markers/executors=0. Новый аудит 25 unloaded сцен не проводился; H dirty/missing-component blockers не исправлялись.

Результат `05B_STATIC_VALIDATION.json` содержит nativeFilesystemTested=false и realSaveDataAccessed=false. Нет actual disk adapter calls, user-save reads/writes, native crash tests, networking, GameObjects, Play Mode, physics, screenshots или builds. Реальные backups во время этапа не создавались — проверено их поведение в модели.

## 8. Продолжение и ограничения приёмки

Следующий обязательный слой — trusted persistent identity mapping, authoritative global snapshot collection/merge и подключение к G spawn source с восстановлением через native readiness, не через legacy Transform teleport. Нужно согласовать ship/RPC DTO migration, unified data ownership и real-save migration/backup policy; существующий player-only repository не заменяет ships+players файл.

До включения: пользовательская проверка реального storage path/FS/Replace/locking/crash/restart/reconnect и runtime Host+clients. T-FO04 native/scene/AOI/physics/nav blockers сохраняются. B не изменяет A/player checkpoint schema или NGO protocol `0xF005`, не активирует global mode/world shift и не доказывает устранение jitter.

Один коммит кода/meta/результатов/roadmap/существующего ITERATIONS. Temp runners и несвязанный LiberationSans fallback исключены; historical A/I/H artifacts не переписываются.
