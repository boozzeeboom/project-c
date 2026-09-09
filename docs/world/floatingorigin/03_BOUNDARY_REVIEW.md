# T-FO03 — инвентаризация и первичная классификация границ

Дата: 2026-09-09. Предыдущие этапы: T-FO01 `ba56bdc4`, T-FO02 `d3fb5511`.
Статус: **машинная инвентаризация выполнена; полная семантическая классификация и интеграция НЕ завершены**. Runtime floating origin остаётся выключенным.

## 1. Реализованный инструмент и результаты

`Assets/_Project/Editor/FloatingOrigin/FloatingOriginMigrationCensus.cs`.
Меню: `ProjectC/World/Floating Origin/Write Migration Census`.

Фактический повторный запуск после исправлений:

| Область | Результат |
|---|---:|
| Импортированные C# под Assets | 639 |
| Shader/HLSL/CGINC | 48 |
| Лексические source candidates | 2033 |
| Prefab assets под Assets/_Project | 75 |
| Пространственно релевантные prefab object records | 1391 |
| Уже открытые сцены | 1 — BootstrapScene |
| Пространственные object records в открытой сцене | 197 |
| Ошибки чтения/сканирования итогового запуска | 0 |
| Найденные NetworkTransform в prefab records | 50 — Owner=24, Server=26 |

Это не 2033 дефекта и не 2033 отдельных места исправления: правила могут пересекаться и включают Editor/legacy/comments. Перечень содержит строки и evidence для дальнейшего чтения, а не автоматически подтверждённый call graph.

Сгенерированы:
- `03_SOURCE_CANDIDATES.csv` — source path/line/category/evidence;
- `03_PREFAB_CANDIDATES.csv` — asset path/object path/component layout, NT asset settings и mesh-local bounds;
- `03_OPEN_SCENE_CANDIDATES.csv` — уже загруженная Bootstrap без открытия/сохранения других сцен;
- `03_CENSUS_SUMMARY.md` — машинные счётчики и ограничения.

## 2. Важные дополнения к T-FO01

**Инвентарь находится за пределами Scripts/:** `Assets/_Project/Items/Network/InventoryServer.cs` передаёт координаты Pickup/Drop. Поэтому поиск только в `Assets/_Project/Scripts` был бы недостаточен. Census сканирует все импортированные C#/shader файлы под Assets, включая сторонние.

### 2.1. Позиционные RPC, найденные этим правилом

Пути ниже относительно `Assets/_Project/`, кроме отдельно указанных.

| RPC | Producers / receivers по прочитанным исходникам | Классификация и решение |
|---|---|---|
| `Items/Network/InventoryServer.RequestPickupRpc` | `Items/Client/InventoryClientState.cs:127,134`; server сравнивает worldPos с server PlayerObject.position и передаёт в InventoryWorld.TryPickup | Точка pickup: отправлять global, затем server frame/double distance; не изменять item/instance ID |
| `InventoryServer.RequestDropRpc` | InventoryClientState:175; TryDrop затем Instantiate(_dropPickupPrefab, worldPos) и Spawn | worldPos/playerPos — точки. Не доверять клиентской playerPos как authoritative validation; получать server player position. Это отдельный guard для интеграции, здесь логика не менялась |
| `Scripts/Combat/Network/CombatServer.RequestSkillCastAtPointRpc` | `Scripts/Skills/SkillInputService.cs:559` | targetPoint — точка; конвертация только на spatial boundary, направления луча не менять |
| `Scripts/Core/ServerStormManager.StormSpawnClientRpc` | SpawnStorm:128 → controller.Initialize/Instantiate:220–237 | Global cloud point; конвертировать при отображении |
| `ServerStormManager.SyncStormStatesClientRpc` | Update:196–200 → StormController.UpdateState:264 | Массив global positions, не массив local одного клиента; `_activeStorms.WorldPosition` также global |
| `ServerStormManager.EventCloudSpawnClientRpc` | TriggerEventCloud:290 → Initialize/Instantiate:296–310 | Global event-cloud position |
| `Scripts/Core/ServerWeatherController.BroadcastWindClientRpc` | signature direction/speed, 214 | **Direction, не позиция.** Не прибавлять origin |
| `Scripts/Player/NetworkPlayer.HidePickupRpc` | interaction:1790 → proximity lookup:1816–1824 | Передаётся точка; sender/receiver обязаны согласовать global. Нельзя случайно скрыть объект с похожей local position другого региона |
| `NetworkPlayer.OpenChestRpc` | Метод читает targetPos и chest.position:1848–1861; текущий caller этим поиском не найден | Точка, но активность пути **UNVERIFIED**. Не объявлять используется/не используется без дальнейшей проверки |
| `NetworkPlayer.ApplyServerPositionRpc` | Пустое тело 1884–1889 | Legacy disabled correction; не оживлять как часть механической смены Vector3 |
| `NetworkPlayer.TeleportServerRpc` | ClientSceneLoader:415,427; TeleportLocal:1988 → TeleportToPosition | Raw position должна стать global teleport contract; отдельно authority и owner application |
| `NetworkPlayer.TeleportAllClientRpc` | TeleportToPosition:1974 → non-owner Transform assignment:1945–1950 | Точка с generation/state reset; origin shift НЕ должен проходить как teleport/сброс velocity |
| `Scripts/Player/PlayerRespawnTracker.TeleportToClientRpc` | ship exits/nearest owned ship/respawn points:115,128,140,167,342 | Несколько producers разных происхождений, каждый явно переводить в global; затем destination frame |
| `Scripts/Player/ShipController.RecallShipToPadServerRpc` | `Scripts/Ship/UI/RepairManagerWindow.cs:680` передаёт nearestPad.transform.position | Global pad point либо устойчивый pad ID; сохранить проверки и стоимость, не доверять произвольной client point |
| Legacy `FloatingOriginMP.BroadcastWorldShiftRpc` | Старый класс | В новой архитектуре не нужен как broadcast origin всех клиентов; старый компонент не включать |
| Legacy `FloatingOriginMP.RequestWorldShiftRpc` | Старый класс | Не использовать; собственный origin каждого клиента не является server teleport request |

Всего 16 сигнатур-кандидатов этим правилом. DTO-wrapped позиции, RPC с другим именованием, world events, delegates и runtime reflection могут не попасть в эту категорию; другие категории и ручное чтение обязательны.

### 2.2. DTO: точки нельзя путать с размерами и local данными

Прочитан `Scripts/Docking/Dto/DockingDto.cs`:

- `DockStationInfoDto.platformCenter` — world point; `platformAltitude` — глобальная высота.
- `DockPadInfoDto.localPosition` — local точки pad, **не global**; `localEulerAngles` — углы; `triggerBoxSize` — размеры. Не применять origin к этим трём полям.
- `DockingAssignmentDto.approachPoint` — world point; `approachAltitude` — абсолютная высота; `approachHeading` — угол.
- `DockingStatusDto` — status/IDs/timestamp, пространственной точки нет.

Прочитаны `Scripts/PeacefulShip/Dto/NpcShipSpawnDto.cs` и `NpcShipStatusDto.cs`: в их текущих полях **нет пространственных координат**. Они не требуют механического добавления GlobalPosition только из-за слов Spawn/Ship. Ссылка shipNetworkObjectId требует сохранения корректной network identity, но не offset.

## 3. Asset/open-scene подтверждения и неизвестные

- В prefab configurations найдено 50 NT: среди них NetworkPlayer owner-authority, корабли owner-authority и NPC server-authority. Значение prefab не доказывает runtime ownership; нельзя заменить весь список на server authority.
- В открытой Bootstrap census **не обнаружил ни одного FloatingOrigin компонента**. В `Assets/_Project/Prefabs/MainCamera.prefab` legacy FloatingOriginMP существует; его наличие в asset не означает использование активной камерой Bootstrap.
- Bootstrap содержит ClientSceneLoader, ScenePlacedObjectSpawner, InventoryServer, ServerStormManager и обе position persistence службы. Объект PlayerSpawner имеет activeSelf=false. CSV не хранит enabled каждого Behaviour и activeInHierarchy: не интерпретировать activeSelf как полную runtime активность.
- В двух prefab object records и трёх Bootstrap records обнаружен `MISSING_SCRIPT`. Среди Bootstrap — `[ShipKeyServer]`, `[ShipKeyToast]`, `[ShipOwnershipRegistry]`. Это обнаруженные исходные references, **не созданные этим этапом**; не удалялись/не исправлялись. Назначение missing компонентов до полной миграции требует выяснения.
- После census наблюдалось `BootstrapScene.isDirty=true`; начальный dirty flag отдельно не фиксировался. Сцену не сохраняли/не перезагружали и не делали предположений о причине dirty. Сам scanner только читает GameObject/компоненты и пишет четыре plain-text отчёта.
- Городская WorldScene не открывалась. По prefab/local bounds невозможно объявить все city meshes проверенными; исходное утверждение о больших mesh-local вершинах города остаётся **UNVERIFIED**, а не поводом для re-export.

## 4. Исправления и проверки самого инструмента

Первый запуск census остановился на Unity missing-component wrapper: `GetComponent<MeshFilter>()?.sharedMesh` недостаточен для Unity fake-null. Исправлено на явные Unity `!= null` проверки. После исправления повторный запуск завершился с `errors=[]`. Префабы и сценовые components не исправлялись/не модифицировались.

Дополнительный numerical review выявил крайний случай пользовательской конфигурации ядра: огромный local bound/quantum мог дать finite double shift, который при cast становился infinite float LocalTranslation. Добавлена finite-проверка в `OriginRebasePlan` и отдельный regression case валидатора. Это защита математического API, не изменение обычных игровых настроек.

Итог после всех правок:
- Unity compile: **No compile errors**.
- Фактически выполненный foundation Run: **23 PASS, 0 FAIL**.
- Фактически выполненный census Run: **errors=[]**, числа §1.
- Play Mode, билды, physics simulation и screenshots: **не выполнялись**.

## 5. Условия продолжения / что ещё не выполнено

T-FO03 не закрывает полный semantic gate. До интеграции T-FO04 остаются:

1. Разобрать кандидатов группами gameplay-domain, читать определения и все callers/serialized targets; помечать каждый значимый контракт global/frame-local/parent-local/nav-local/direction, а также runtime/legacy/Editor.
2. Проверить SceneRegistry/build/NetworkManager prefab registration и закрытые WorldScenes без перезаписи пользовательских YAML. Недостаточно 75 prefab assets, если существуют scene-only NetworkObjects.
3. Проверить NetworkObject spawn/parent/ownership совместно с replacement movement-компонентом и Rigidbody integrations. Не добавить второй конкурирующий writer к тем же Transform.
4. Согласовать/реализовать единую транзакционную phase для scene placement и rebase: до первой physics/network выборки, с registration/caches и failure handling.
5. Довести серверные регионы, physics queries и nav isolation: один Host frame не является достаточной MMO реализацией.

**Полная floating-origin реализация пока отсутствует.** Ни T-FO01, ни T-FO02, ни машинный census не дают runtime PASS и не являются основанием утверждать, что микротряска устранена. Следующий рабочий участок — завершение semantic inventory и глобальная NGO spatial replication, затем перенос RPC/persistence/контента/физики/кэшей с пользовательскими gates.
