# T-FO06B — явная регистрация NGO и изолированный пилотный префаб игрока

Дата: 2026-09-09. Предыдущий этап: T-FO06A, `e52339f4`.

## 1. Результат

Выполнены два согласованных пункта §6 отчёта 06A: политика регистрации сетевых префабов переведена в явный режим, и создан отдельный пилотный префаб игрока, удовлетворяющий контракту Spatial.

**Global mode/world shift остаются выключенными. Пилот никуда не назначен и не зарегистрирован. Runtime-проверок не было.**

Верификация: `06B_PILOT_PLAYER_PREFAB.json`, read-only скрипт `tools/VerifyFo06BPilotPrefab.cs`.

## 2. Политика регистрации префабов

`GenerateDefaultNetworkPrefabs`: `true → false`, сохранено в `ProjectSettings/NetcodeForGameObjects.asset` (файл ранее не существовал, Git его не игнорирует).

Проверено после изменения:

- `Assets/DefaultNetworkPrefabs.asset` — 58 записей, serialized-содержимое, байты файла и `.meta` идентичны;
- canonical `NetworkPlayer.prefab` по-прежнему присутствует в списке;
- байты canonical префаба и его `.meta` не изменились.

Список остаётся рабочим: он указан в `NetworkConfig.Prefabs.NetworkPrefabsLists` сцены, а отключённая настройка влияет только на `NetworkPrefabProcessor` (AssetPostprocessor).

**Последствие для рабочего процесса:** новые сетевые префабы больше не добавляются в список автоматически — их нужно регистрировать явно. Также перестаёт работать автоматическое удаление записей при удалении префаба, поэтому список требует ручной поддержки.

## 3. Пилотный префаб игрока

`Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab`

Создан как **независимая копия**, а не Prefab Variant. Причина: в варианте удаление унаследованного `NetworkTransform` существует лишь как override, который может быть восстановлен изменением базы или `Revert`. Копия исключает тихое возвращение конкурирующего writer'а.

Цена решения зафиксирована как ограничение: правки canonical префаба (визуал, риг, геймплей) **не наследуются** и должны переноситься осознанно.

Применённые изменения:

- удалён `Unity.Netcode.Components.NetworkTransform`;
- добавлены `ProjectC.Combat.PlayerAttacker` и `ProjectC.Combat.PlayerTarget`;
- добавлен `GlobalMotionReplicator`;
- добавлен `GlobalMotionPoseAdapter`, `_coordinatesRequired=true`;
- `SynchronizeTransform=false`, `AutoObjectParentSync=false`, `SceneMigrationSynchronization=false`;
- `ActiveSceneSynchronization` и `DontDestroyWithOwner` уже были false и перепроверены.

Итоговый порядок NetworkBehaviour (6): `NetworkPlayer`, `PlayerRespawnTracker`, `PlayerAttacker`, `PlayerTarget`, `GlobalMotionReplicator`, `GlobalMotionPoseAdapter`. Поскольку это один asset, порядок одинаков у сервера и клиентов.

## 4. Уточнение к 06A: пятое требование

В 06A перечислены 4 группы блокеров по `ValidateNetworkStart`; там же отмечено, что это не исчерпывающий сертификат. Фактически `GlobalMotionNetworkContract.ValidateLayout` для роли Spatial содержит **пятое** требование: `PlayerAttacker` и `PlayerTarget` должны быть запечены в префабе (`player_combat_behaviours_not_baked`).

Это согласуется с существующим runtime-кодом: `NetworkPlayer` для global-игрока выдаёт ошибку, если эти компоненты не запечены, и блокирует их добавление в рантайме. Остальные добавляемые в рантайме компоненты (`SkillInputService`, `SkillAnimationPlayer`, `SkillAnimationEventPassthrough`) — обычные MonoBehaviour, поэтому порядок NetworkBehaviour не меняют; их поведение на пилоте не проверялось.

Оба компонента добавлены со значениями по умолчанию — теми же, что получались при рантайм-добавлении.

## 5. Проверки

Read-only верификация выполнена внутри Editor:

- контракт Spatial: **PASS**, features = RootNetworkObject, Adapter, Replicator, CoordinatesRequired, SpatialContent, Player, PlayerAttacker, PlayerTarget;
- prefab hash пилота отличен от canonical (`3692800100` против `186599647`);
- все пять флагов NetworkObject у пилота = false;
- у пилота нет `NetworkTransform`, вложенных NetworkObject, Rigidbody/Joint/NavMeshAgent/2D, colliders вне root CC и лишних `IGlobalMotionActorParticipant`;
- canonical префаб остаётся legacy: `NetworkTransform` на месте, global-компонентов нет;
- 8 защищённых файлов из снимка 06A побайтово не изменились (canonical, Bootstrap, WorldScene_0_0, registry и их `.meta`); сравнение идёт с зафиксированным JSON, а не с константами;
- автогенерация выключена, список = 58, пилот не зарегистрирован;
- выбранный в сцене PlayerPrefab по-прежнему canonical.

Unity: **No compile errors**. Runtime C# проекта не изменялся; 678 pure checks этапа E не перезапускались.

Не выполнялось: Play Mode, physics, сетевые сессии, вызовы native executor, screenshots, builds, auth, доступ к save-файлам.

## 6. Открытый вопрос публикации

`.gitignore:153` содержит `*.prefab`, поэтому пилотный префаб **не попадает в коммит** и существует только локально. Это то же ограничение, что и с `*.unity` для BootstrapScene.

Принудительное добавление (`git add -f`) — решение уровня политики репозитория, и в этом этапе оно не выполнялось. Требуется отдельное подтверждение.

## 7. Остающиеся блокеры

1. Пилот не зарегистрирован ни в одном `NetworkPrefabsList`. Для реальной сессии `TryBuildHello` требует, чтобы эффективный реестр **точно совпадал** с классифицированным каталогом профиля — то есть классификации потребуют все зарегистрированные префабы, не только игрок.
2. Нет `GlobalMotionNetworkProfile`, reviewed scene catalog и его digest, prepared frames, markers и native scene executor.
3. В Bootstrap остаются включённые legacy `ShipPositionServer`/`PlayerPositionServer`, активный `ClientSceneLoader` и 3 неопознанных missing-компонента; сцена по-прежнему не сохранена.
4. Иерархия WorldScene_0_0 не осмотрена; trusted account issuer и explicit store ownership не подтверждены.

Полные T-FO03–09 открыты, `jitter fixed` не заявляется.

## 8. Состав коммита

Этот отчёт, `06B_PILOT_PLAYER_PREFAB.json`, `tools/VerifyFo06BPilotPrefab.cs`, `ProjectSettings/NetcodeForGameObjects.asset`, roadmap и `Assets/_Project/Docs/ITERATIONS.md`.

Не включаются: пилотный префаб (игнорируется правилом `*.prefab`), TMP fallback, Temp-скрипты этапа, `DefaultNetworkPrefabs.asset`, сцены и исторические отчёты.
