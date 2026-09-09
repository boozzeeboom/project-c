# Floating origin — архитектура и поэтапное внедрение

Дата: 2026-09-09. Задача: полный переход Project C на локальные вычислительные координаты для MMO Host + Clients, с дальнейшей возможностью dedicated server.
Тикеты серии: **T-FO01** (аудит/решение), **T-FO02** (координатное ядро и проверки); последующие номера резервируются таблицей ниже. Поиск по существующей документации не обнаружил T-FO01/T-FO02.
Baseline: `53c86fe2` (`minor changes - before floatingorigin`). Несвязанное изменение `LiberationSans SDF - Fallback.asset` не включать.

## Статус и границы

Это новая, явно запрошенная пользователем широкая задача, а не продолжение renderer-only T-JITTER18. Старые запреты на расширение scope T-JITTER17 описывают историческое решение, не текущее поручение. T-JITTER18 остаётся отрицательным результатом; его код не возвращать.

**Полный floating origin пока НЕ внедрён.** Первый этап вводит контракты координат и проверяемое основание миграции. Пока сетевые/сценовые/физические границы не перенесены, НЕ включать ни новый сдвиг мира, ни старые `FloatingOrigin`/`FloatingOriginMP`. Успешная компиляция ядра не устраняет микротряску сама по себе.

Все Play Mode, игровые билды для приёмки и скриншоты выполняет пользователь. Автоматические проверки ограничены компиляцией, анализом исходников и чистыми Edit Mode проверками математики/сериализации без запуска сцен или физики.

Один коммит на завершённый этап: код + результаты + обновление существующего `Assets/_Project/Docs/ITERATIONS.md`. Не создавать второй журнал, не добавлять хеш своего коммита отдельным коммитом. Без push, rebase, массового git add и перезаписи scene/prefab YAML.

## 1. Подтверждённая исходная точка

- Текущий Editor: Unity **6000.5.2f1**, установленный NGO **2.13.0** (проверено через PackageInfo редактора). В предшествующем аудите URP 17.5.0; перед изменениями рендера повторно проверить пакет.
- `SceneID.SCENE_SIZE = 79999f`. Номер сцены не является физической локальной системой координат.
- Исторически подтверждённый default spawn `(39999.5, 3000, 39999.5)` уже далеко от Unity-origin; T-JITTER18 не помог на пользовательской позиции около 56.5 км.
- Рабочая архитектура использует NGO NetworkTransform, позиционные RPC и float-позиции сохранений. Существуют также отдельные deck-local и nav-sandbox координаты.
- Наличие старого класса в репозитории НЕ подтверждает его использование. Текущая задача не оживляет legacy origin-код.

Подробные доказательства, ограничения и карта границ: `01_AUDIT_AND_SOURCES.md`.

## 2. Единственная выбранная архитектура

`GlobalPosition(double X/Y/Z) → explicit LocalCoordinateFrame → Unity Vector3`.

1. **Глобальная точка** независима от origin клиента, сессии, кадра рендера и physics region. Сеть и persistence сохраняют эту точку, а не временную локальную позицию.
2. **Локальная система координат** имеет явный глобальный origin. Вычитание origin выполняется в double ДО преобразования результата в float. Не использовать `(float)global - (float)origin`.
3. **Клиенты имеют независимые origin.** Сдвиг клиента не является игровым телепортом и не требует broadcast-команды, двигающей остальные клиенты. Локальный rebase не меняет глобальный snapshot объекта.
4. **Серверные физические регионы** имеют собственные frame и PhysicsScene. Нельзя объявлять один общий origin достаточным для всех далеко разошедшихся игроков.
5. **Host** совмещает роли в одном процессе; NGO server/client экземпляр NetworkObject у хоста не становится двумя независимыми Transform. Хостовая видимая область может совпадать с её simulation frame; удалённые серверные регионы требуют изоляции физики/навигации и невидимого для камеры представления. Межрегиональные взаимодействия требуют явного handoff, не вычитания offset в случайных Update.
6. SceneID и ChunkId остаются адресами контента/интереса. Размер сетки 79999 и форматы имён сцен не менять. Физический регион существенно меньше сцены контента и не обязан совпадать с SceneID.
7. **Направления, скорости, силы, rotations, scale и локальные точки палубы** не переводить как глобальные позиции. Frame в первом ядре — только перенос, без поворота и масштаба.
8. Глобальная высота отделена от локального Y. Полный XYZ rebase допустим только после миграции altitude/cloud/fog/shader систем. Сохранение Y без сдвига не решает произвольно большую высоту.

### Сетевой контракт

- Для позиционных данных использовать явный сериализуемый global-double тип; finite/range проверки на границе.
- В первой версии не вводить агрессивную delta/half-float оптимизацию: сначала правильность. Размер точки без заголовка — 24 байта; трафик измеряется отдельно.
- Снимок движения потребует timestamp/tick, teleport generation, authority/ownership generation и parent identity. Позиция parent-relative должна иметь отдельный признак/тип, не угадываться по величине.
- Interpolation buffer хранит глобальные точки (или явно маркированный устойчивый frame), а отображение использует текущий local frame. Пакет до rebase не должен интерпретироваться в origin отправителя/получателя случайным образом.
- Инициализация, late join, pooled spawn, despawn, reconnect, parent/unparent и смена authority — обязательные части адаптера, а не последующие косметические исправления.
- Не менять NetworkBehaviour набор/порядок только на одной стороне runtime AddComponent. Изменения prefab layout должны быть одинаковыми у сервера/клиентов, через Unity API, с отдельной миграцией ассетов и проверкой.
- Не патчить `Library/PackageCache`; два notification hook NetworkTransform не являются универсальной заменой wire serialization (см. аудит исходника NGO).
- Смешанные старые/новые билды должны отвергаться согласованием версии протокола; сохранения требуют своей версии и backup. Это интеграционные задачи, не выполненные наличием структуры GlobalPosition.

### Rebase-транзакция

В будущем runtime-координатор должен работать в одной определённой фазе игрового цикла до physics/authoritative movement и сетевой выборки состояний:

1. Проверить готовность адаптеров, стабильность загрузки/спавна и полный набор зарегистрированных участников региона.
2. Подготовить новый frame и список объектов. Любой неизвестный обязательный компонент блокирует активацию, не молча исключается.
3. Перенести корни ровно один раз. Дочерние Transform автоматически следуют root; вложенные Rigidbody, CharacterController, joints и nav agents требуют согласованного snapshot/restore, а не повторного offset.
4. Обновить physics poses, interpolation history и пространственные кэши; сохранить velocities/sleep/constraints. Синхронизировать physics representation перед следующими запросами.
5. Перенести/перерегистрировать world navmesh в согласованной точке; sandbox-прокси не сдвигать автоматически вместе с игровым миром. Сохранить либо явно восстановить цели/path state.
6. Скорректировать camera lag, platform-last-position, trajectories, particles/trails/lines, pooled и inactive объекты.
7. Commit frame. Ошибка после начала изменений должна остановить симуляцию/сетевую публикацию с диагностикой; нельзя продолжать полу-сдвинутый мир.

Публичная смена глобального статического offset без транзакции запрещена. Ядро T-FO02 само ничего не перемещает.

## 3. Этапы и критерии завершения

| Тикет | Этап | Результат / gate |
|---|---|---|
| T-FO01 | Актуальный аудит, первоисточники, архитектура | Зафиксированные пути/семантика, риски Host, roadmap; без runtime изменений |
| T-FO02 | Double-coordinate foundation | GlobalPosition, immutable frame, grid bridge, чистая проверка точности и NGO сериализации; без подключения к работающим системам |
| T-FO03 | Полный migration census и spatial adapters design | Машинный список кандидатов + ручная классификация каждой границы; asset/runtime coverage; неизвестные места остаются BLOCKED |
| T-FO04 | NGO spatial replication | Собственная global-aware репликация и миграция prefab компонентов; initial sync, tick, authority, parent, reconnect; Host + 2 clients gate пользователя |
| T-FO05 | RPC/DTO и persistence | Точки боя/телепорта/докинга/штормов/FX и сохранения в global; backward-read старых сохранений, backups, protocol gate; пользовательские reconnect/save/load |
| T-FO06 | Контент и client rebase | Загрузка сцен до spawn с явным placement, DDOL/pools/static geometry/camera/shaders, independent client origins; пользовательский дальний Idle и streaming |
| T-FO07 | Серверные регионы и physics routing | PhysicsScene + отдельное управление регионами, scene-aware запросы, ship/passenger/crew/joints handoff; два далёких клиента на Host |
| T-FO08 | NavMesh, AI, корабли и остальные механики | World/deck/proxy адаптеры, cached goals, combat/docking/cruise/altitude/weather/VFX/UI; полный регрессионный прогон |
| T-FO09 | Приёмка и включение по умолчанию | Результаты пользователя, профилирование, packet loss/late join, повторные shift, restart; только после этого объявлять jitter fixed |

Номера — план, не заявление о выполнении. Подготовительные изменения можно выполнять без игровых тестов, если они не меняют рабочий runtime. Переход через интеграционный runtime gate допускается только с результатами пользователя. Пилот одного близкого клиента не считается завершением MMO-варианта.

### Фактическое продвижение на 2026-09-09

- T-FO01: завершён, коммит `ba56bdc4`.
- T-FO02: завершён как изолированное ядро, коммит `d3fb5511`; 22 исходные проверки пройдены. В T-FO03 добавлен guard extreme float translation и 23-я regression проверка.
- T-FO03: scanner и машинный census выполнены (639 C#, 48 shader files, 75 prefab assets, Bootstrap); первичная классификация 16 RPC и docking/NPC DTO сохранена. Полный semantic/closed-world-scene gate ещё открыт. Отчёт `03_BOUNDARY_REVIEW.md`.
- T-FO04A: реализованы versioned global/parent-local motion snapshots и bounded receive/interpolation buffer. Pure Edit Mode checks: 33 новых + 23 foundation, все PASS. Отчёт `04A_NETWORK_PROTOCOL_AND_BUFFER.md`. NGO transport, выдача generations и prefab/scene integration ещё не подключены; это НЕ полный T-FO04.
- T-FO04B: реализованы explicit session/generation issuer, reliable control + server admission и настоящий NGO GlobalMotionReplicator. Компонент не установлен на префабы/сцены и не пишет Transform. 32 новых + 56 прежних pure проверок = 88 PASS. Отчёт `04B_NGO_TRANSPORT.md`. Живые сетевые сессии не тестировались.
- T-FO04C: реализованы GlobalMotionWorld (session/frame registry), GlobalMotionPoseAdapter и pure application policy. Новые компоненты не установлены на сцены/префабы. Compile PASS; 32 новых + 88 прежних pure проверок = 120 PASS / 0 FAIL. Отчёт `04C_FRAME_POSE_ADAPTER.md`. Native lifecycle/physics и gameplay не тестировались; unsupported nav/joints/nested bodies и interpolated-parent server replica блокируются.
- T-FO04D: добавлены dormant hooks в player/ship/NPC/skills и отдельные social/NavTick пути; opt-in по умолчанию false, baseline placement отделён от actor/native readiness. 26 новых + 120 прежних pure проверок = 146 PASS / 0 FAIL, compile PASS. Отчёт `04D_ACTOR_READINESS.md`. Текущие префабы/сцены не изменены, native preparation/полный runtime gate не реализованы.
- T-FO04E: завершена dormant подготовка startup/layout contracts: explicit prefab SHA256 + выбранный PlayerPrefab, strict 72-byte hello, scoped approval gate и interface заранее подготовленного spawn/parent bootstrap. Профиль-ассет не создан; scenes/prefabs не редактировались. Compile PASS; **48 новых + 146 прежних = 194 pure PASS / 0 FAIL**. Read-only audit: 58 candidate prefabs, opt-in=0, profile assets=0; это не полный scene census. Отчёт `04E_NETWORK_STARTUP_CONTRACTS.md`, результаты `04E_STATIC_VALIDATION.json` и `04E_PREFAB_CONTRACT_AUDIT.json`.
- T-FO04F: реализовано limited custom hierarchy placement из нового trusted baseline; prospective parent/pose preflight до публикации control, actual SetParent + pose до callbacks/ACK. Same-binding motion hierarchy не меняет. Cross-scene/frame, циклы, Rigidbody/joints/произвольные colliders/enabled NavMeshAgent changes блокируются; CC допускается в ограниченной конфигурации, native rollback отсутствует. Global protocol=0xF002, старый 0xF001 reserved/incompatible. Compile PASS; **40 новых + 194 прежних = 234 pure PASS / 0 FAIL**. Отчёт `04F_CUSTOM_HIERARCHY_BASELINES.md`, результаты `04F_STATIC_VALIDATION.json`. Текущая игра не активирована и runtime parenting не проверен.
- T-FO04G: реализован dormant concrete **player-only** GlobalMotionPlayerBootstrap: approved-peer tickets, delayed registration подготовленных frames, typed global pre-instantiation seed через NGO 2.13 handler, local pose/scene до Awake, PostSpawn Bind/first World baseline и отдельный initial CC latch. Первый server spawn синхронный и ограничен root NetworkPlayer/CC; general native async readiness, pools и scene-object lifecycle не реализованы. Source prepared content/global persistence остаётся обязательным интерфейсом **без реализации**, startup блокируется без него; активный legacy ClientSceneLoader не допускается. Global protocol=0xF003. Compile PASS; **44 новых + 234 прежних = 278 pure PASS / 0 FAIL**, opt-in/profile assets/loaded profiles/adapters=0. Отчёт `04G_PLAYER_SPAWN_BOOTSTRAP.md`, результаты `04G_STATIC_VALIDATION.json`. Сцены/префабы/профиль не изменялись; настоящий prefab/NGO/CC runtime не проверен.
- T-FO04H: реализованы strict compiler/digest для **заявленного reviewed scene catalog**, immutable parent-first/reverse plan, lifecycle receipt ledger (load generations, parent/frame order, exact retirement tokens для network и обычного content), read-only scene audit. Profile hello теперь связывает scene digest с каталогом и проверяет replacement prefab hash/role; protocol=0xF004. Compile PASS; **56 новых + 278 прежних = 334 pure PASS / 0 FAIL**. Реальный candidate audit: 26 сцен, 1 loaded/dirty, 25 uninspected, 61 Unreviewed root/NetworkObject entries, 3 missing-component subtree diagnostics; catalog assets=0. Это НЕ утверждённый closed-world catalog и НЕ native spawn/retire executor. Отчёт `04H_SCENE_CATALOG_AND_LIFECYCLE.md`, результаты `04H_STATIC_VALIDATION.json` / `04H_SCENE_CATALOG_AUDIT.json`. Существующая untracked schema GlobalMotionSceneCatalog.cs сохранена без переписывания; сцены/префабы не изменялись.
- Следующая интеграционная часть T-FO04 — native scene-source binding и безопасный spawn/retire executor: учитывать NGO in-scene sweep ДО OnServerStarted, настоящие prepared-content receipts и loading transaction. Отдельно остаются review реального scope, dirty/missing-component blockers и обследование unloaded scenes; источники global persistence/physics/nav согласуются с T-FO05–08. Ledger и catalog digest не заменяют native bridges или runtime coverage. Prefab/scene activation и T-FO05–09 ещё не выполнены. Runtime floating origin не включён, устранение микротряски не подтверждено.

## 4. Приёмка полного решения

- Позиция прежнего FAIL (~56.5 км), 30 км+, origin: обычные Idle/walk/run/jump, без замороженного Animator и без hide/render tricks; пользовательское видео/скриншоты.
- Host и минимум два клиента: рядом, далеко друг от друга, повторно вместе; каждый локальный игрок остаётся в принятом точностном бюджете.
- Принудительный и пороговый rebase при движении/прыжке/бое/пилотировании, на палубе и в момент посадки/выхода.
- Корабль с пассажирами/именованным экипажем, rotation, docking/undocking, recall, autopilot/cruise, NPC торговля, navmesh readiness/handoff.
- Телепорт/respawn/save/load/reconnect/late join, scene load/unload, pool reuse, смена authority/parent, пакеты до и после shift.
- Pickup/chest/resources, Q001 dialogue/quest zones, combat projectiles/AOE/animation events, equipment/body swap и modal input gate.
- Глобальная высота/коридоры, штормы/ветер/облака/lighting, карта/компас/индикаторы, world-space shaders/particles/trails/lines.
- Измерения CPU/GC/rebase spikes, трафика и расстояний физики от origin. Не обещать абсолютное отсутствие любого джиттера по одному compile PASS.

## 5. Работа с результатами

См. отдельные отчёты этапов в этом каталоге. Не переписывать отрицательную историю T-JITTER18 и не выдавать ненайденные/непрочитанные данные за доказательства. Неизвестные active components, mesh-local bounds, shader semantics и runtime порядок явно маркируются UNVERIFIED до прямой проверки.
