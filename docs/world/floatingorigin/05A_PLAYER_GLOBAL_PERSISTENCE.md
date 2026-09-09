# T-FO05A — глобальная запись позиции игрока и read-only legacy import

Дата: 2026-09-09. Baseline: `f973721d`. **Подготовительный код, не переключение работающего persistence. Compile PASS; 69 новых + 365 прежних = 434 pure PASS / 0 FAIL.** Полный T-FO04, T-FO05 и runtime acceptance остаются открытыми.

## 1. Почему этот шаг следующий

G bootstrap требует настоящий source сохранённой глобальной позиции. I добавил ограниченный native scene executor, но не реализовал global persistence, identity, frame routing или content preparation. Подготовительный независимый persistence-контракт допустим по §3 roadmap без прохождения runtime gate и устраняет часть этой зависимости. Он не заменяет остальные native bridges и не является их приёмкой.

Проверены реальные исходники:

- `Assets/_Project/Scripts/Core/ShipPosition/ShipPositionSaveData.cs`: `PlayerPositionSaveData` содержит ровно clientId, px/py/pz, inShip, shipPersistentId, savedAtUnix. Позиция float, ключ — NGO clientId. Общий `ShipPositionListWrapper` содержит **ships + players**.
- `PlayerPositionServer.cs:50–76`: server собирает `GetEffectivePosition()`, `OwnerClientId`, ship affinity; `RestorePlayer:141–182` ищет по clientId и выбирает ship exit или старую float-позицию. `TeleportPlayer:185–192` напрямую меняет Transform/CC и вызывает Physics.SyncTransforms. Этот путь новым модулем не вызывается.
- `ShipPositionRepository.cs:136–146`: текущий writer вызывает `JsonUtility.ToJson(wrapper, false)` и `File.WriteAllText`. В исследованном repository нет backup/replace transaction; комментарий «atomic» не доказывает crash-safe запись. Нельзя переключать рабочий файл на новый DTO до отдельного transactional storage этапа.
- `GlobalPosition.FromLegacyAbsolute` уже явно различает старую абсолютную float-точку; `LocalCoordinateFrame.ToGlobal` требует valid explicit frame. Ни float, ни clientId не превращаются в устойчивые данные от простого изменения типа поля.

В исследованном player persistence/restore пути нет подтверждённого сопоставления с постоянной account/player identity. Дополнительный проектный поиск не дал подтверждённой реализации такого provider — это **незакрытый результат поиска**, не доказательство отсутствия любой auth-подсистемы во всём проекте. NGO clientId здесь является legacy lookup key, а не доказанным постоянным идентификатором.

## 2. Изменённый scope

Добавлены только новые C# файлы:

- `Assets/_Project/Scripts/World/FloatingOrigin/Persistence/GlobalPlayerPositionRecord.cs`
- `Assets/_Project/Scripts/World/FloatingOrigin/Persistence/GlobalPlayerPositionCodec.cs`
- `Assets/_Project/Scripts/World/FloatingOrigin/Persistence/LegacyPlayerPositionImport.cs`
- `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalPlayerPersistence.cs`

Unity-generated meta, report/validation JSON, roadmap и существующий ITERATIONS входят в тот же коммит. **Legacy DTO, PlayerPositionServer, repository, NetworkPlayer и G/I runtime код не изменялись.** Packages не устанавливались, scenes/prefabs/profile не изменялись. Настоящий ShipPositions.json не читался и не записывался.

## 3. Глобальная запись и формат v1

`GlobalPlayerPositionRecord` — immutable CLR value holder:

- явно предоставленный постоянный `PlayerId`;
- origin-independent `GlobalPosition` с конечными double XYZ;
- `InShip` + согласованный `ShipPersistentId` как **restore intent**, а не parent-local pose;
- явно переданный `SavedAtUnix` в диапазоне 0..253402300799.

Нет client/session/network object/frame ID, Transform, scene origin и неявного default spawn. Текущий frame выбирается будущим coordinator из global point и актуальной topology, а не сохраняется как долговременная identity. Синтаксическая проверка PlayerId (bounded opaque ASCII identifier) не является проверкой account ownership/authentication; доверенная связь должна прийти извне. Числовая строка, переданная злоупотребляющим caller, не становится от codec доказанным account ID.

GlobalPosition — mutable struct, но запись хранит копию и возвращает копию; после проверки нельзя менять исходный point по ссылке. Отрицательный ноль нормализуется в обычный ноль: это одна и та же глобальная точка.

`GlobalPlayerPositionCodec` сериализует **одну запись**, не весь файл кораблей/игроков:

- format=`projectc.global-player-position`, schemaVersion=1, coordinateSpace=`global-double`;
- XYZ — invariant round-trip `R` строки double, без промежуточного float;
- checksum SHA256 от versioned typed binary sequence (length-prefixed strings + fixed-width little-endian doubles/bool/int64);
- codec не читает файлы, не делает backups, не восстанавливает игроков и не выбирает native frame.

**SHA256 — обнаружение повреждения, не authentication/signature.** Повторно вычисливший checksum отправитель не считается авторизованным; network admission/storage ownership проверяются другими компонентами.

### Строгий формат JSON

Новый v1 — compact canonical JSON с фиксированным порядком полей. Разрешены внешние JSON whitespace, но не pretty/reordered/extended payload. Перед JsonUtility ограничиваются размер, глубина, контейнеры, баланс и root object; после разбора требуется точное соответствие повторной сериализации. Missing/unknown/duplicate fields, coercion и noncanonical numbers не превращаются в default zero/false.

Это осознанное ограничение, а не обещание универсального JSON reader. Unity JsonUtility игнорирует незнакомые поля и оставляет значения отсутствующих полей; одной успешной FromJson недостаточно. Основание: Unity Manual, `https://docs.unity3d.com/6000.0/Documentation/Manual/json-serialization.html`, и подтверждённый live `UnityEngine.JsonUtility` API.

Frozen golden JSON/checksum v1 включён в suite. Порядок/типы/семантику полей v1 нельзя менять без новой версии и reader миграции. File schema v1 **независима** от NGO motion protocol, который остаётся `0xF005`; legacy protocol=0 не меняется.

## 4. Legacy import — только reviewed draft в памяти

`LegacyPlayerPositionImport.TryCreateDraft` читает переданный caller текст текущего **известного compact JsonUtility ShipPositionListWrapper layout**. Не обращается к persistentDataPath/repository.

Обязательны:

1. SHA256 конкретного supplied JSON text, с которым заранее сопоставлена review/configuration. Digest определён как strict UTF8 encoding этого текста, а не checksum исходного файла с возможным BOM/другой кодировкой.
2. Полный взаимно-однозначный набор bindings всех player records: legacy clientId, expected savedAtUnix, явный persistent PlayerId.
3. По каждой записи явно выбранная семантика: `AbsoluteWorldFloat` **без** frame либо `KnownLocalFrame` с valid explicit source frame. Unspecified/default/неизвестный frame не допускается.
4. Корректные point/ship affinity/timestamp; нет duplicate NGO ids, duplicate persistent identities, лишних/недостающих mappings или изменившегося snapshot.

Absolute branch использует `GlobalPosition.FromLegacyAbsolute`; known-local — `frame.ToGlobal`. **Потерянная при старом float-сохранении точность не восстанавливается.** Source frame используется лишь как внешняя provenance для миграции local data; его id/origin не записывается в новую durable checkpoint запись.

Изменённый/неизвестный/старый/pretty/reordered legacy layout требует отдельного рассмотренного reader и отклоняется. Следовательно, это backward-read текущего известного legacy layout, **не поддержка всех исторических сохранений**. Frozen legacy JSON тест не пересоздаёт fixture из изменяемого DTO и выявляет несовместимое изменение его формы.

Результат `LegacyPlayerPositionDraft` immutable и содержит все player records, original input text, text digest и число **неперенесённых** ship entries. Ошибка одного игрока оставляет out draft=null: частичная миграция не публикуется. Ships/NPC nav state не преобразуются и не теряются: весь supplied text удерживается неизменным. **Нельзя записывать Players из draft поверх общего ShipPositions.json.**

OriginalJson — сохранённый в памяти исходный текст, **не disk backup и не доказательство сохранения оригинальных file bytes/encoding/BOM**. Этот этап не создаёт ни резервных копий пользовательских данных, ни новых файлов сохранений.

## 5. Фактические проверки

`Temp/Aura/ValidateFo05A.cs` запускался в стабильном Edit Mode, результаты: `05A_STATIC_VALIDATION.json`.

| Suite | PASS | FAIL |
|---|---:|---:|
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
| **Total** | **434** | **0** |

Проверены golden v1/legacy compatibility, double extremes и 100 generated points, sub-float precision на 56.5 км, разные frames и locale, отрицательный ноль, checksum tampering, schema/shape/size/depth/unknown/duplicate/missing/truncated inputs, identity/timestamp/provenance guards, atomic all-player draft, ship-text retention и отсутствие изменений формы legacy DTO.

Compile: **No compile errors**. Source review не выявил blocking safety defects. Tests используют только in-memory fixtures, JsonUtility и математику; не вызывают actual repository, Gameplay, NativeExecutor или NGO.

Guard повторно: 58 prefab candidates; opt-in candidates, profile assets, loaded assigned profiles/adapters/markers/executors=0. Не заявляется новый аудит оставшихся 25 unloaded сцен или исправление H dirty/missing-component blockers.

## 6. Что остаётся

- Genuine stable identity/authorization mapping и reviewed provenance реальных legacy save snapshots; при отсутствии данных нельзя подставить NGO clientId или origin=0.
- Транзакционный server repository с byte-preserving backup, crash/rollback/recovery и явной политикой неподдерживаемых старых форматов. Текущий Float/JsonUtility writer нельзя объявить atomic из-за lock.
- Сбор authoritative global checkpoints из motion state, pending-save/reconnect/restore ownership и подключение к G source; клиентский Transform не источник canonical server position.
- Полный unified ships+players format: global ship pose, cruise target, altitude и прочие позиционные границы T-FO05, не затрагивая направления/velocities/rotations.
- RPC migration, настоящий prepared-content/frame/AOI provider, native/scene migration и пользовательская runtime приёмка.

**UNTESTED:** настоящие legacy saves, диск/backup/crash recovery, restart/reconnect/auth mapping, сбор и восстановление движущихся игроков/кораблей, реальный Host+clients, profiling/IL2CPP. Play Mode, сборки, screenshots и physics не запускались. Global mode/world shift выключены; runtime gates не пересечены; jitter fixed не заявляется.

Коммит этапа включает только код/meta/результаты/roadmap/ITERATIONS. Temp runners и несвязанный LiberationSans fallback не включаются. Исторические I/H/G artifacts не переписываются.
