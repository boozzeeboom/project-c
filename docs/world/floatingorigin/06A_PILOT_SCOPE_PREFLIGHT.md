# T-FO06A — preflight конкретного пилотного scope

Дата: 2026-09-09. Предыдущий завершённый этап: T-FO05E, `4b1c8f05`.
Тикет T-FO06A назначен как первый подготовительный подэтап T-FO06 из roadmap; существующего отчёта/занятого подэтапа 06A до этой работы не обнаружено.

## 1. Результат и граница

**Read-only preflight завершён. Пилотная конфигурация BLOCKED, не создана и не включена.**

Проверены текущий выбранный PlayerPrefab, реально загруженный Bootstrap, активная настройка NGO-регистрации и metadata зависимостей WorldScene_0_0. Никакие пользовательские сцены не открывались/не сохранялись, preview scenes и GameObjects не создавались. Это не миграция контента, не закрытый scene catalog и не runtime acceptance.

Воспроизводимый отчёт: `06A_PILOT_SCOPE_BASELINE.json`.
Read-only C# аудит: `tools/AuditFo06APilot.cs`, public static `Execute()`, запускается внутри Unity Editor в стабильном Edit Mode из корня проекта. Скрипт находится вне Assets: не добавляет runtime/Editor-компонентов в сборку проекта. Единственный публикуемый им файл — JSON отчёта; повторный запуск обновляет этот снимок, поэтому исторический результат перед новым этапом следует сохранить в Git.

## 2. Подтверждённая конфигурация

| Объект | Фактическое состояние |
|---|---|
| Editor / NGO | Unity 6000.5.2f1 / NGO 2.13.0 |
| Загруженные сцены | Только `Assets/_Project/Scenes/BootstrapScene.unity`, 56 roots, `isDirty=true` |
| NetworkManager | `NetworkManager[3]`; выбран `Assets/_Project/Prefabs/NetworkPlayer.prefab` |
| NMC global references | `_globalMotionProfile`, `_globalSpawnBootstrap`, `_globalSessionCoordinator`: UNASSIGNED |
| NGO list | `Assets/DefaultNetworkPrefabs.asset`, 58 записей, `IsDefault=true`, asset dirty=false |
| NGO project setting | `GenerateDefaultNetworkPrefabs=true` |
| WorldScene_0_0 | `Assets/_Project/Scenes/World/WorldScene_0_0.unity`; asset существует, сцена НЕ загружена |

`protectedFiles[].dirty` относится к asset-объекту в AssetDatabase, а `loadedScenes[].dirty` — к открытому экземпляру сцены. У Bootstrap первое false, второе true; это не противоречие и не разрешение сохранять сцену.

## 3. PlayerPrefab: четыре группы блокеров

Текущий canonical prefab остаётся legacy; заменять его на месте нельзя в рамках этого preflight.

1. Нет `GlobalMotionReplicator`.
2. Нет `GlobalMotionPoseAdapter` с explicit coordinate opt-in.
3. На root присутствует штатный `NetworkTransform` — запрещён ограниченной G player factory.
4. Требуются global-only overrides флагов NetworkObject:
   - сейчас `AutoObjectParentSync=true`, `SynchronizeTransform=true`, `SceneMigrationSynchronization=true`;
   - `ActiveSceneSynchronization=false`, `DontDestroyWithOwner=false` уже соответствуют ограничению G.

Подтверждены enabled root NetworkPlayer и CharacterController. У CC: height=1.8, radius=0.3, center=(0,0.9,0), stepOffset=0.3, slopeLimit=45, skinWidth=0.08. Проверка не выявила Rigidbody/Joint/NavMeshAgent/2D body, дополнительных colliders вне root CC или дополнительных root `IGlobalMotionActorParticipant` сверх NetworkPlayer.

Иерархия, порядок NetworkBehaviour и object references, включая текущий мужской rig, Animator, материалы и сохранённые конфигурационные ссылки, записаны в JSON. Наличие связанной визуальной конфигурации не является проверкой анимации. Ничего из этого не перестраивалось.

Источник требований: `GlobalMotionPlayerBootstrap.ValidateNetworkStart`, в частности строки 75–97 на момент аудита, и существующий layout contract. Полный `ValidateNetworkStart` не вызывался: prepared source/frames/native executor отсутствуют. Четыре группы выше — инвентаризация явно обнаруженных несовместимостей, а не исчерпывающий сертификат совместимости будущего variant.

## 4. Реальный блокер изоляции регистрации

В установленном NGO 2.13.0 `Editor/Configuration/NetworkPrefabProcessor.cs`:

- `OnPostprocessAllAssets` проверяет `GenerateDefaultNetworkPrefabs` (строки 31–36);
- каждый импортированный GameObject с корневым NetworkObject добавляется в default list (строки 39–70);
- фильтра по папке `Prefabs`, `Resources`, `Editor` или условной `PilotAssets` здесь нет.

Следовательно, новый variant с root NetworkObject сейчас автоматически изменит активный список, даже если не назначать его в PlayerPrefab и положить в отдельную папку. Предположение «вне Prefabs не зарегистрируется» неверно.

**Сама регистрация НЕ спавнит игрока и НЕ включает global mode.** Проблема — незаявленное изменение рабочего prefab manifest; его влияние на совместимость сетевой конфигурации также должно быть учтено. Регистрация и активация — разные события.

В этом этапе не отключалась автогенерация, не удалялись/не добавлялись записи списка и не патчился PackageCache. Временно отключить настройку только на время создания asset недостаточно для устойчивой изоляции: последующий import вновь попадёт в обработчик.

## 5. Bootstrap / scene scope

Три missing component entries остаются с неизвестной идентичностью:

- `Inventory[1]/[ShipKeyServer][2]`, component index 2;
- `Toasts_and_meta[22]/[ShipKeyToast][0]`, component index 2;
- `Toasts_and_meta[22]/[ShipOwnershipRegistry][3]`, component index 1.

Имена GameObject НЕ доказывают имя отсутствующего C# класса. Компоненты не удалялись, не заменялись и не объявлялись безопасно исключёнными.

Присутствуют enabled legacy services:

- `[ShipPositionServer][52]`: `ProjectC.Core.ShipPosition.ShipPositionServer`;
- `[PlayerPositionServer][53]`: `ProjectC.Core.ShipPosition.PlayerPositionServer`.

Они блокируют E global-start; простое выключение компонента не удовлетворяет E, поскольку учитываются и disabled instances. Их legacy callbacks и save ownership нельзя оставлять параллельно global persistence.

Также активен `Runtime[9] / ProjectC.World.Scene.ClientSceneLoader`, который запрещён G startup. `ScenePlacedObjectSpawner[7]` зафиксирован как существующий компонент, без заявления о готовности его native runtime-ветки.

В NMC найдено 36 **лексических мест** `go.AddComponent`/`gameObject.AddComponent`, включая альтернативные ветки и повторные обращения к типам. Это НЕ 36 реально созданных объектов и НЕ их semantic review. Runtime DDOL/client services и добавления NetworkPlayer.OnNetworkSpawn требуют отдельного coverage: отсутствие компонента в Edit Mode не означает отсутствия в игре.

У WorldScene_0_0 получен только список зависимостей. Root positions, native colliders/nav, activation intent, spatial actors, markers и точность scene frame не проверены. Предыдущий H census (25 uninspected scenes, 61 unreviewed observations) здесь не пересчитывался и не закрывается этим preflight.

## 6. Следующая согласуемая последовательность

1. **Явно согласовать контролируемую регистрацию prefab.** Сохранить текущие 58 legacy entries без удаления/перестановки и canonical PlayerPrefab. Предлагаемая политика для следующего этапа: отключить `GenerateDefaultNetworkPrefabs`, после чего вести регистрацию явно; isolated global player variant и отдельный pilot list не подключать к работающей Bootstrap автоматически. Это изменение рабочего процесса всего проекта — новые сетевые prefab больше не будут автоматически появляться в default list; до отдельного подтверждения настройка остаётся true.
2. После подтверждения политики — создать отдельный global player variant через Unity prefab API; только в нём удалить stock NetworkTransform, добавить global components и изменить необходимые NO flags. Проверить inherited visual references, CC параметры, layout/hash и сохранность base prefab. Не менять native roster только на одной стороне сети.
3. Согласовать состояние несохранённой Bootstrap перед сценовыми правками. Затем исследовать missing-script identity и global/legacy service ownership, а не удалять неизвестные компоненты по имени. Scene YAML не переписывать.
4. Выполнить реальный read-only native audit выбранной WorldScene, только затем принимать catalog/markers/frames/physics/activation intent. Не считать dependency metadata заменой осмотра и не обходить unsupported spatial actors молчаливым исключением.
5. Подключать profile/source/session/store и trusted account issuer лишь после закрытия этих prerequisites. Account provider в E не был подтверждён (inconclusive); этот этап его не искал повторно и не создаёт fake identity из clientId или implicit first-spawn.
6. Host + client, CC readiness, restore/checkpoint, rebase и visual acceptance — только пользовательский runtime gate. Существующие ограничения I/G, ships/ParentLocal/streaming/regions не исчезают после подготовки одного player prefab.

Это порядок оставшихся работ, а не заявление об их выполнении. Полные T-FO03–09 остаются открыты.

## 7. Проверки выполненного этапа

- Аудит успешно выполнен из сохранённого `tools/AuditFo06APilot.cs` внутри Editor.
- JSON сериализует все вложенные baseline DTO; перед публикацией выполняется round-trip с проверкой обязательных вложенных данных. Для native identity используется совместимый с EntityId/старыми Unity read-only доступ без obsolete compile calls.
- Три integrity assertions PASS: набор loaded scenes, dirty flags и hierarchy/component fingerprint неизменны; настройка, полный serialized registry и его dirty flag неизменны; SHA256 восьми защищённых файлов (canonical player, Bootstrap, WorldScene_0_0, registry и их meta) неизменны.
- Unity: **No compile errors**. Runtime C# проекта не менялся; отдельного нового прогона прежних 678 pure tests нет. Их результат относится к T-FO05E, не к этому preflight.
- No Play Mode, screenshots, physics, network sessions, native executor invocation, builds, auth, реальные save files или directory storage.
- Global mode/world shift выключены. `jitter fixed` и готовность пилота не заявляются.

## 8. Состав коммита

Этот отчёт, JSON baseline, воспроизводимый C# аудит вне Assets, roadmap и существующий `Assets/_Project/Docs/ITERATIONS.md`.

Не включать TMP fallback, Temp runner, scene/prefab/profile/catalog assets, NGO settings/default registry, packages, исторические отчёты и результаты предыдущих этапов. Один прямой Git-коммит без push/rebase и без отдельного коммита для собственного хеша.
