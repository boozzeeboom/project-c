# T-FO01 — baseline, границы координат и первоисточники

Дата: 2026-09-09. Baseline `53c86fe2`. Статус: начальный аудит, НЕ утверждение полной семантической проверки всех механик.

## 1. Метод и подтверждения

Прочитаны прежние `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER_SOLUTIONS_RESEARCH.md` и `IMPLEMENTATION_CHARACTER_LOCAL_SKINNING_EXPERIMENT.md`. Они фиксируют пользовательский FAIL near-origin skinning и откат. Новая задача явно расширяет scope до общей архитектуры.

Live Editor: Play Mode=false, compile errors=false, открыт `Assets/_Project/Scenes/BootstrapScene.unity`. `Application.unityVersion=6000.5.2f1`, `PackageInfo.FindForAssembly(typeof(NetworkObject).Assembly).version=2.13.0`.
Установленный исходник NGO: `Library/PackageCache/com.unity.netcode.gameobjects@0f12e689d980/Runtime/Components/NetworkTransform.cs`. Пакет только прочитан, не изменён.

Предыдущая Bootstrap-инвентаризация в T-JITTER17 подтверждала активные ClientSceneLoader, ScenePlacedObjectSpawner и persistence, отсутствие origin-менеджеров. Здесь это историческое доказательство; полный повторный scene/prefab census необходим перед активацией. Нельзя считать неактивные legacy классы рабочим путём только по наличию файла.

## 2. Проверенные границы исходников

Пути относительно `Assets/_Project/Scripts/`.

| Файл | Подтверждённый участок | Семантика / необходимое действие |
|---|---|---|
| `World/Scene/SceneID.cs` | 17, 29–59 | Сетка 79999, глобальные float origin/center/conversions. Сохранить legacy API; добавить явный double bridge без смены старой семантики |
| `World/Scene/SceneID.cs` | 122–149 | SceneTransitionData уже несёт SceneID + local float. Это не raw absolute; проверить диапазон local внутри большой сцены |
| `Player/NetworkPlayer.cs` | RPC 1814, 1848, 1884, 1913, 1943 | HidePickup/OpenChest ищут по точке, ApplyServerPosition отключён, TeleportServer/All принимают Vector3. Все call sites подлежат парному переносу |
| `Player/PlayerRespawnTracker.cs` | TeleportToClientRpc, 224 | Позиционная граница respawn |
| `Player/ShipController.cs` | RecallShipToPadServerRpc, 2392 | Точка pad приходит с клиента; кроме frame conversion нужна существующая server validation, не перенос authority клиенту |
| `Combat/Network/CombatServer.cs` | RequestSkillCastAtPointRpc, 171 | Точка боя; нельзя принимать client-local как server-local |
| `Core/ServerStormManager.cs` | StormSpawnClientRpc 220; SyncStormStatesClientRpc 256; EventCloudSpawnClientRpc 296 | Координаты облаков/штормов и массивы позиций — отдельные wire границы, не только NetworkTransform |
| `Core/ServerWeatherController.cs` | BroadcastWindClientRpc, 214 | Vector3 direction — НЕ точка, translation не применять |
| `Docking/Dto/DockingDto.cs` | DockingAssignmentDto и другие DTO | Найдены сериализуемые контейнеры: требуется дальнейшее чтение всех полей и производителей; не объявлять их уже мигрированными |
| `PeacefulShip/Dto/NpcShipSpawnDto.cs`, `NpcShipStatusDto.cs` | INetworkSerializable | Кандидаты полного census; семантику каждого поля ещё классифицировать |
| `Core/ShipPosition/ShipPositionSaveData.cs` | 30, 46–47, 73–79 | float position/cruise/liftStartY/player position. Новый формат требует версии и явного legacy-read; не сохранять local frame как мир |
| `Ship/ShipDeckNav.cs` | 60,64,93–107,141–152,180–215 | Отдельные navFrameOrigin/lastRegisteredShipPos; local/world/nav conversions. RegisterUnderShip ставит sandbox в позицию корабля на момент регистрации. Нельзя автоматически вычитать один offset из всего |
| `AI/NpcBrain.cs` | 185,193,202 | Реальные кэши `_spawnPoint`, `_rideLastPos`, `_proxyLastPos`; разные системы координат требуют разных адаптеров |
| `AI/NpcBrain.cs` | 984–989 | Захваченная impactPos в callback projectile — rebase может произойти до выполнения callback |
| `AI/NpcSocialBrain.cs` | 164,168 | `_wanderTarget`, `_fleeTarget` — сохраняемые точки целей |
| `AI/NpcLootPickup.cs` | 52 | `_startPosition` используется движением pickup; root translation недостаточен |
| `World/Streaming/FloatingOriginMP.cs` | 39,98–139,279–377,497–550 | Legacy MonoBehaviour, name-based исключения игроков/камеры, эвристики расстояния и RPC-атрибуты. Не использовать как доказанную сетевую реализацию |

В исходнике ShipDeckNav комментарий заявляет ≤1 registration/frame, однако счётчик сбрасывается при каждом вызове ProcessPendingRegistrations. Комментарий не является измерением/гарантией. Этот отдельный perf-дефект здесь не исправляется.

## 3. NGO: почему нельзя ограничиться двумя hooks

В текущем установленном NGO 2.13.0:

- `OnSynchronize` **1902** сначала вызывает `SynchronizeState.NetworkSerialize(serializer)`, **1904** затем `OnAuthorityPushTransformState`.
- `TryCommitTransform` **2015** сначала вызывает `UpdateTransformState()`, **2031** затем notification hook.
- `OnNetworkStateChanged` **3345** применяет `ApplyUpdatedState(newState)`, **3368** затем `OnNetworkTransformStateUpdated`.
- `OnBeforeUpdateTransformState()` не принимает изменяемый snapshot.
- PositionX/Y/Z и текущие/interpolated Vector3 остаются float; даже удачная замена значения не превращает wire format в double.

Вывод ограничен проверенными путями: **переопределение двух уведомительных методов не является корректным codec для независимых origins**, особенно для initial sync и внутренних buffers. Нельзя выдавать описание «just prior» из API docs за фактический порядок установленного исходника. Это не утверждение невозможности любой кастомной реализации NGO: запланирована собственная spatial replication с явным протоколом, без патча PackageCache.

## 4. Host, физика, навигация

Unity поддерживает независимые physics scenes и требует явного управления ими [S3]. Это основа проектируемых серверных регионов, не готовая MMO система.

В Host сервер и локальный клиент разделяют объекты. Один frame не держит двух удалённых игроков одновременно возле нуля. Отдельный origin на каждом remote клиенте улучшит его представление, но **сам по себе не исправит удалённую серверную Rigidbody симуляцию**. Поэтому серверные регионы/physics query routing входят в полный scope, даже до dedicated server.

`Physics.SyncTransforms` синхронизирует изменения Transform с физикой [S4]; это не решение network buffers/NavMesh/camera lag. NavMesh AddNavMeshData допускает placement при регистрации [S5]; ground nav и неподвижные ship sandbox нужно обрабатывать раздельно. Не добавлять фиктивные offsets всем raycast hit/direction или всем Vector3.

## 5. Остальная область обязательного census

Ниже задачи проверки, НЕ список уже сломанных механик:

- Active additive scene lifecycle: Awake/OnEnable до sceneLoaded, initial spawn, scene-placed NetworkObject и позднее подключение.
- DDOL, inactive/pools, nested NetworkObject/Rigidbody, joints, CharacterController, physics interpolation и sleep states.
- SpringArmCamera lag, platform carry прошлого кадра, pilot/boarding/disembark, корабельные cruise/lift targets и NPC route scheduler.
- Ground NavMeshSurface/links, AI home/wander/flee/cover targets, deck proxies и их независимые sandboxes.
- World-space particles/trails/lines/projectiles, damage numbers/target highlights, cloud movement, shader absolute height/noise, lighting.
- Global maps/scene/interest/grid queries, distances across regions, trade/quest/resource positions.
- Bounds/mesh-local vertices большого городского FBX и static batching. **Числовые пределы mesh-local вершин не измерены**; размер FBX-файла не доказывает проблемную точность. Не назначать re-export без измерений.

Поиск исходников выявляет кандидатов, но не доказывает runtime активность и не гарантирует отсутствие других границ. Полная ручная классификация и scene/prefab coverage остаются условием включения.

## 6. Проверяемые первоисточники

Просмотрены 2026-09-09; Unity/NGO версии закреплены, нет предложения обновлять движок/сеть.

- **S1 — NGO 2.13 NetworkTransform API:** `https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.Components.NetworkTransform.html`. Объявления hooks; фактический порядок дополнительно проверен в установленном исходнике, см. §3.
- **S2 — NGO 2.13 BufferSerializer:** `https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/api/Unity.Netcode.BufferSerializer-1.html`. Основание для явного INetworkSerializable coordinate type, не готовый movement protocol.
- **S3 — Unity 6.5 Multi-scene physics:** `https://docs.unity3d.com/6000.5/Documentation/Manual/physics-multi-scene.html`. Independent physics scenes и их явное управление.
- **S4 — Unity 6.5 Physics.SyncTransforms:** `https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Physics.SyncTransforms.html`.
- **S5 — Unity 6.5 NavMesh.AddNavMeshData:** `https://docs.unity3d.com/6000.5/Documentation/ScriptReference/AI.NavMesh.AddNavMeshData.html`.
- **S6 — coherence World Origin Shifting:** `https://docs.coherence.io/1.2/coherence-sdk-for-unity/world-origin-shifting`. Пример разделения global double и индивидуальных client origins в другом SDK. Это НЕ подтверждение наличия аналогичного адаптера в NGO и НЕ рекомендация мигрировать сеть.

## 7. Итог T-FO01

Выбран explicit global-double/local-frame путь, отделены client rebase и серверные регионы. План и аудит документированы до C# изменений. Старый origin не активирован, T-JITTER18 не восстановлен, сцены/префабы/сохранения не менялись. Результат исследования достаточен для изолированного координатного ядра, **недостаточен для безопасного включения общего world shift**.
