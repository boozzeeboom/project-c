# T-FO06D — минимальный пилотный реестр и фиксация остатка

Дата: 2026-09-09. Предыдущий этап: T-FO06C, `fedad976`.

## 1. Решение и результат

Выбран путь сокращения: вместо классификации всех 58 зарегистрированных префабов создан **отдельный минимальный список только для пилота**, чтобы проверить игрока раньше. Общий реестр не изменялся.

**Пилот пока никуда не подключён.** Global mode/world shift выключены, runtime-проверок не было.

Создано: `Assets/_Project/Prefabs/FloatingOrigin/GlobalPilotNetworkPrefabs.asset`

- ровно 1 запись — пилотный префаб игрока, hash `3692800100`;
- `Override = None`;
- `IsDefault = false` — список не является авто-генерируемым реестром NGO;
- ни одна сцена на него не ссылается; `NetworkManager` по-прежнему использует только default-список.

Проверено после создания: `DefaultNetworkPrefabs.asset` — 58 записей, serialized-содержимое и байты идентичны, пилот в него не попал.

## 2. Классификация всех 58 записей (доказательная)

Прогон реального валидатора контракта по каждой записи реестра. Отчёт: `06D_REGISTRY_CLASSIFICATION.json`, скрипт `tools/AuditFo06DRegistryClassification.cs` (read-only).

| Итог | Кол-во |
|---|---:|
| Готовы как Spatial без изменений | **0** |
| Готовы как NonSpatial без изменений | **1** |
| Требуют миграции | **57** |
| Пустые/битые записи | 0 |

Причины отказа по роли Spatial:

| Причина | Кол-во |
|---|---:|
| `missing_global_motion_components_or_optin` | 54 |
| `invalid_behaviour_count` | 2 |
| `inactive_network_behaviour_object` | 2 |

Единственная запись, готовая как NonSpatial: `NEWCHESTPREFAB` (`Assets/_Project/Prefabs/NEWCHESTPREFAB.prefab`).

`NonSpatial` присваивался только при фактическом отсутствии пространственного содержимого. Записи с рендерерами, коллайдерами или телами отнесены к миграции даже там, где валидатор формально принял бы разметку — иначе классификация была бы недостоверной.

## 3. Структурный блокер полного реестра

Четыре записи не проходят **ни одну** роль, потому что соответствующие проверки выполняются до ветвления по роли:

- нет ни одного NetworkBehaviour (`invalid_behaviour_count`): `TestPlayer`, `PickupItem_Test`;
- NetworkBehaviour на выключенном GameObject (`inactive_network_behaviour_object`): `NetworkChestContainer_Test`, `SPAWN_TEST`.

Это означает, что путь «классифицировать все 58» блокирован не только объёмом миграции: эти четыре префаба нельзя включить в классифицированный каталог в текущем виде, их придётся исправлять или исключать из реестра. Ни одно из этих изменений в этом этапе не выполнялось.

## 4. Что останется вне пилота

Пилотный список содержит только игрока. Следовательно, в пилотной сессии **не будет работать спавн** ничего из перечисленного ниже, поскольку NGO отклоняет спавн незарегистрированного префаба:

- корабли (включая NPC-корабли и тестовые варианты);
- NPC, торговые зоны, зоны квестов;
- предметы, сундуки, контейнеры, pickup-объекты;
- ресурсные узлы и станции крафта;
- тестовые спавнеры и вспомогательные тестовые префабы.

Также вне пилота остаются, по данным предыдущих этапов:

- legacy `ShipPositionServer` и `PlayerPositionServer` — включены в Bootstrap и блокируют global-start;
- активный `ClientSceneLoader` — запрещён G startup;
- три неопознанных missing-компонента в Bootstrap;
- иерархия `WorldScene_0_0` — не осмотрена;
- trusted account issuer и explicit store ownership — не подтверждены.

## 5. Что ещё требуется, чтобы пилот реально запустился

Перечислено как остаток, а не как выполненное:

1. **Замена списка в сцене.** `TryBuildHello` требует, чтобы эффективный реестр точно совпадал с каталогом профиля. Эффективный реестр — это все списки в `NetworkPrefabsLists` плюс встроенные записи. Поэтому пилотный список должен **заменить** default-список, а не добавиться к нему: иначе эффективный размер станет 59 и каталог обязан описывать все 59. Это правка BootstrapScene, которая не выполнялась.
2. `GlobalMotionNetworkProfile` с классифицированным каталогом из одной Spatial-записи и корректным `SceneLayoutDigest`.
3. Reviewed scene catalog, markers, prepared frames и native scene executor.
4. Выбор пилотного префаба как `PlayerPrefab`, вывод legacy position services из global-scope.
5. Trusted account issuer и explicit store/directory ownership.
6. Пользовательский runtime gate: Host + client, CC readiness, restore/checkpoint.

## 6. Проверки

- Классификационный аудит: default-реестр не изменён (serialized-содержимое и байты), ничего не создано и не миграциировано в ходе аудита.
- Создание списка: контракт пилота перепроверен перед записью; после импорта — 1 запись, `Override=None`, `IsDefault=false`, hash совпадает с layout; default-реестр не тронут, пилот в него не утёк.
- `.gitignore`: точечное исключение расширено на `*.asset.meta` внутри пилотной папки — на список будут ссылаться профиль и сцена, поэтому GUID должен быть стабилен. Проверено `git check-ignore`: `.meta` пилотного списка отслеживается, `NetworkPlayer.prefab.meta` и остальные метаданные проекта по-прежнему игнорируются.
- Unity: **No compile errors**. Runtime C# проекта не изменялся.
- Не выполнялось: Play Mode, physics, сетевые сессии, native executor, screenshots, builds, auth, доступ к save-файлам.

## 7. Состав коммита

Пилотный список с `.meta`, `.gitignore`, отчёт классификации `06D_REGISTRY_CLASSIFICATION.json`, `tools/AuditFo06DRegistryClassification.cs`, этот отчёт, roadmap и `Assets/_Project/Docs/ITERATIONS.md`.

Не включаются: TMP fallback, Temp-скрипты, `DefaultNetworkPrefabs.asset`, canonical префаб и сцены.
