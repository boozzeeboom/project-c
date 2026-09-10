# Журнал итераций

## Итерация от 2026-09-10 (T-FO06L — исправить binding executor)

**Задача:** Продолжить T-FO06L после отказа native preparation на `catalog_source_not_bound`, не восстанавливая удалённый `GroundPlane_0_0`.
**Результат:** `GlobalSceneNativeExecutor.BuildPreparation()` теперь перечисляет все `GlobalSceneSourceMarker` внутри reviewed roots, включая marked descendants без `NetworkObject`. Добавлена обязательная проверка согласованности `catalog / markers / bound`; диагностика `catalog_source_not_bound` теперь содержит счётчики. Внешние runtime roots без marker, duplicate, wrong-scene и parent identity guards сохранены.
**Проверки:** Compile — `No compile errors`; `Validate Native Scene Execution Contracts` — `32 pure checks passed`. Runtime native preparation, Play Mode, Host/client, grounding и screenshots не запускались.
**Граница:** `GroundPlane_0_0` подтверждённо удалён и не восстанавливается. Текущий catalog/digest не изменялся. Независимый `LiberationSans SDF - Fallback.asset` в этап не входит.
**Следующий шаг:** Передать текущую сборку пользователю для ручного Host/client Play Mode-прогона. При повторном отказе использовать фактические значения `catalog=...;markers=...;bound=...`; не разрешать silent skip.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, обновлён этот журнал.

---

## Итерация от 2026-09-09 (T-FO06L)

**Задача:** Подготовить и зафиксировать первый global static-world pilot: `NetworkPlayer_GlobalPilot`, reviewed catalog для `BootstrapScene` + `WorldScene_0_0`, explicit `Unmanaged` treatment и native scene admission до NGO spawn.
**Результат:** Добавлен `GlobalSceneTreatment.Unmanaged` с обязательной catalog/review binding и сохранением authored state без placement/activation/spawn/retirement. Обновлены catalog compiler, policy, executor seams и protocol `0xF005 → 0xF006`. Созданы/подключены `GlobalMotionPilotSceneCatalog`, `GlobalMotionPilotProfile`, `GlobalPilotNetworkPrefabs`, `GlobalMotionPilotSpawnSource` и `GlobalMotionPilotRuntime`; global startup временно заменяет `NetworkConfig.PlayerPrefab` только в памяти и восстанавливает legacy prefab при release/failure. Каталог содержит 150 observations (`BootstrapScene=61`, `WorldScene_0_0=89`) с digest `bfe8008aa885a818b05f799799c504a1b174f87fdc0d73df2043a3ab91d93199`.
**Проверки:** Compile ранее подтверждён как `No compile errors`; pure execution validator — `32 passed / 0 failed`. Edit Mode snapshot содержит 150 уникальных live markers. Runtime native preparation остановилась до player spawn с `scene_preparation:catalog_source_not_bound:336a190646b19bc46b22dd4e78f99800:1044316355:0`; поэтому персонаж не появляется и Host/client acceptance не пройдены.
**Граница:** Play Mode, screenshots и игровые acceptance-прогоны выполняет пользователь; автоматически не запускаются. `GroundPlane_0_0` намеренно не восстанавливается. Независимый `LiberationSans SDF - Fallback.asset` в этап не входит.
**Следующий шаг:** Исправить executor candidate binding так, чтобы все 150 catalog entries связывались со всеми marked descendants reviewed roots; добавить диагностику `catalog/markers/bound`, затем передать сборку пользователю для ручного Host/client прогона. Только после пользовательского PASS проверять grounding, движение, камеру и release/restore.
**Документация:** `docs/world/floatingorigin/06L_GLOBAL_STATIC_WORLD_PILOT.md`, обновлён этот журнал.

---

## Итерация от 2026-09-09 (T-FO06K)

**Задача:** Проверить результат ручной уборки и составить каталог пилота.
**Ручная правка сцены:** Пользователь убрал мёртвые ссылки вручную. Diff шире трёх компонентов: 165 добавлений, 1996 удалений, удалено 15 объектов — `Boundaries_0_0` со всем содержимым, `GroundPlane_0_0` (локально `39999.5, 0, 39999.5`) и весь объект `[KeyRod_ShipHeavy]`. `WorldRoot_0_0`, `[Ship_Key_Container]`, `Pad_10`, `PAD-007_npc` сохранились. Земля и границы были соседями под одним родителем, поэтому удаление контейнера границ не могло удалить землю попутно — это отдельные удаления. Удаление земли под исторической точкой спавна отмечено как риск: пилотному игроку нужна опора, а восстановление после составления каталога сделает digest устаревшим.
**Блокеры каталога снялись:** missing-компонентов 0 в обеих сценах, ошибок осмотра 0, observations 150, флаги dirty сброшены сохранением идентичного содержимого без изменения байтов. Отдельно установлено: флаг dirty поднимает перезагрузка домена при компиляции, поэтому авторинг каталога обязан сбрасывать его внутри собственного запуска, иначе аудит выведет `saved = false`.
**Главный результат — каталог не покрывает эти сцены:** из 150 наблюдений поддержано 58, НЕ поддержано 92. По сценам: `BootstrapScene` 54 против 7, `WorldScene_0_0` 4 против 85. Политика допускает только три комбинации — статичный контент как spatial, статичный контент как неспатиальный и неспатиальный сетевой сервис. `Exclude` и `ReplaceWithNetworkPrefab` отвергаются (`replacement_and_exclusion_need_extended_executor`), spatial+NetworkObject отвергается (`spatial_scene_network_actor_bridge_missing`), поддерево spatial-записи ограничено Transform/MeshFilter/Renderer/Collider/маркером, а неспатиальной — запрещает Renderer/Collider/Camera/Light/Animator/ParticleSystem.
**Семь проблемных наблюдений Bootstrap:** `Clouds` (VFX), `PlayerSpawner` (NetworkPlayer/CC/Animator), `MainCamera` (Camera/AudioListener), `Sun` (Light), `Moon` (Light/MeshRenderer/MoonController), `TestObjects` (Canvas/UI/коллайдеры), `ConstellationController` (LineRenderer). В `WorldScene_0_0` поддержаны только `Crossbow`, `Throw_grenade`, `Crossbow_weapon` и `Respawn_Default`.
**Развилка контракта:** вариант A — ввести проверенную категорию «не управляется» с обязательным `reviewNote`, без маркера, позы и контроля активации; затрагивает treatment, компилятор, политику, исполнителя и версию протокола, но снимает блокировку пилота и сохраняет принцип «ничего не исключается молча». Вариант B — расширить исполнителя до spatial-сетевых актёров, что соответствует объёму T-FO07/T-FO08. Рекомендован A; ни один не реализован, так как изменение затрагивает версию протокола и согласование всех участников.
**Файлы:** `WorldScene_0_0.unity` (правка пользователя), `06K_CATALOG_FEASIBILITY_LIMIT.md`, `06K_OBSERVATION_FEASIBILITY.json`, `tools/ClassifyPilotObservations.cs`, roadmap и этот журнал.
**Проверки:** классификация выполнена повторением фактических правил политики и проверки поддерева исполнителя, а не по предположению; обе сцены загружены и совпадают с файлами на диске. No compile errors. Play Mode/physics/network/screenshots/builds/auth/save access не использовались.
**Остаток:** подтвердить вариант расширения контракта; решить судьбу удалённой земли; каталог на 150 записей; профиль с digest и ссылкой на пилотный список; markers/frames/native executor; выбор пилота как PlayerPrefab и вывод legacy position services и ClientSceneLoader; issuer/store; пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один scene/feasibility/docs commit; TMP fallback и Temp-скрипты исключены.

---

## Итерация от 2026-09-09 (T-FO06J)

**Задача:** Загрузить обе сцены пилота, осмотреть живьём и подготовить их к каталогу.
**Исправление оценки 06I:** живой осмотр опроверг разбор YAML. Корней в `WorldScene_0_0` — 58, а не 26; в Bootstrap — 56, а не 55; мёртвых компонентов в WorldScene_0_0 — 3, а не 1; observations — **150**, а не 95. Причины: корни prefab-инстансов в YAML сцены представлены блоками `PrefabInstance` и текстовым разбором не видны, а у двух мёртвых компонентов `m_Script` не содержит GUID, из-за чего парсер их пропускал. Авторитетная цифра — 150 записей от собственного аудита проекта на двух загруженных сценах.
**Опознание:** `WorldRoot_0_0/Boundaries_0_0/South` и `.../SouthPoleBlocker` — `Assembly-CSharp-Editor::ProjectC.Editor.PoleBlockerComponent`, класс не найден нигде в проекте и объявлен в Editor-сборке, то есть в рантайм-сцене неработоспособен по определению. `[Ship_Key_Container]/[KeyRod_ShipHeavy]` — `ProjectC.Ship.Key.KeyRodInstanceBinding`. Все три — обычные объекты сцены, не prefab-инстансы.
**Блокер:** удалить эти слоты публичными API Unity не удалось. `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` не удаляет ничего, так как ссылки на скрипт нет вовсе; `SerializedObject.m_Component` не поддаётся ни `DeleteArrayElementAtIndex` (первое удаление лишь обнуляет элемент), ни изменению `arraySize`. Все попытки прерваны собственными проверками ДО сохранения; обе сцены побайтово совпадают с зафиксированными файлами.
**Почему это блокирует каталог:** аудит вычисляет `inspectedComplete` как отсутствие ошибок при осмотре сцены, а отсутствующий компонент в поддереве наблюдаемого корня даёт ошибку. Оба проблемных объекта лежат в поддеревьях `WorldRoot_0_0` и `[Ship_Key_Container]`. Компилятор отвергает источник с `inspectedComplete = false`.
**BootstrapScene готова:** ошибок осмотра 0 — уборка T-FO06H подтверждена живым аудитом. Безобидный флаг dirty сброшен сохранением идентичного содержимого, байты файла не изменились (проверено хешем). Флаг важен не косметически: аудит выводит из него `saved`, а компилятор требует `saved = true`. Установлено, что флаг поднимает перезагрузка домена при компиляции скриптов, а не правки содержимого.
**Варианты решения:** точечная правка YAML сцены (не делаю без разрешения); удаление вручную через контекстное меню Inspector (рекомендуется, несколько кликов); каталог только для Bootstrap с добавлением WorldScene позже (противоречит заявленной области пилота).
**Инфраструктура:** повреждена ссылка `refs/remotes/origin/main (копия с компьютера DESKTOP-K00O7HK)` — `git log -S` падает с `bad object`; к задаче не относится. Связь с редактором дважды обрывалась на стороне ответа инструментов при живых процессах и чистом логе Unity; состояние проверялось через файловую систему.
**Файлы:** `06J_LIVE_SCENE_REVIEW.md`, `06J_LIVE_MISSING_COMPONENTS.json`, `tools/ListMissingComponentsLive.cs`, roadmap и этот журнал. Сцены не изменены по содержимому и в коммит не входят.
**Проверки:** обе сцены побайтово равны зафиксированным файлам; аудит — `loadedInspected=2`, `uninspected=24`, observations `150`, ошибок `2`, каталогов `0`. Play Mode/physics/network/screenshots/builds/auth/save access не использовались.
**Остаток:** убрать три мёртвые ссылки; каталог на 150 записей; профиль с digest и ссылкой на пилотный список; markers/frames/native executor; выбор пилота как PlayerPrefab и вывод legacy position services и ClientSceneLoader; issuer/store; пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один diagnostics/docs commit; TMP fallback, Temp-скрипты и сцены исключены.

---

## Итерация от 2026-09-09 (T-FO06I)

**Задача:** Зафиксировать область пилота и измерить реальный объём работы по каталогу до его составления.
**Область:** По указанию пользователя пилот работает только с `BootstrapScene` и `WorldScene_0_0`; остальные 24 сцены содержат только разметку границ. Прежний открытый вопрос про 25 неосмотренных сцен снят: они вне пилота осознанно.
**Требование компилятора:** observation — это корневой объект ИЛИ NetworkObject, и на каждую нужна ровно одна запись с непустым `reviewNote`, иначе `review_does_not_cover_observations`. Дополнительно проверяются соответствие treatment сетевой природе объекта, совпадение parentSourceId, корректность позы, запрет spatial-потомка под неспатиальным родителем, обязательное исключение потомков retired-родителя и отсутствие циклов.
**Измеренный объём:** Bootstrap — 222 GameObject, 55 корней, 18 NetworkObject (13 корневых), 60 observations, 0 missing scripts. WorldScene_0_0 — 1191 GameObject, 26 корней, 23 NetworkObject (14 корневых), 35 observations, 1 missing script. Итого **95 проверенных записей**, а не 1413: вложенная геометрия города в каталог не попадает, объём посильный.
**Оговорка по цифрам:** прежний аудит сообщал 61 observation и 56 корней для Bootstrap против 60 и 55 здесь. Причина — способ подсчёта: аудит использует `GlobalObjectId` и учитывает prefab-instance, замер разбирает YAML. Источником для авторинга остаётся собственный аудит проекта; эти числа — оценка объёма, не окончательный состав.
**Четвёртый мёртвый компонент:** `[KeyRod_ShipHeavy]` в WorldScene_0_0 (GO fileID 1602512893, компонент 1602512899), guid `41bc0fc7a014832489bfd2db2ba89173`, класс `ProjectC.Ship.Key.KeyRodInstanceBinding`. Класс намеренно удалён: «P1-refactor: instanceId=0 (KeyRodInstanceBinding удалён)» в PickupItem.cs, а InventoryUI.cs защитно резолвит тип через `Type.GetType` и корректно работает без него. Мёртвая ссылка, не потерянная функциональность. Не удалён: сцена не загружена, удаление требует загрузки и сохранения. В Bootstrap отсутствующих скриптов теперь 0.
**Файлы:** `06I_PILOT_CATALOG_SIZING.md`, `06I_PILOT_CATALOG_SIZING.json`, `tools/SizePilotSceneCatalog.cs`, roadmap и этот журнал.
**Проверки:** замер по сохранённому YAML обеих сцен; ни одна сцена не загружалась, не изменялась и не сохранялась; каталог не создавался. No compile errors, Bootstrap не dirty. Play Mode/physics/network/screenshots/builds/auth/save access не использовались.
**Остаток:** Убрать мёртвый KeyRodInstanceBinding из WorldScene_0_0 с восстановлением исходно открытой сцены; получить черновик каталога собственным аудитом на обеих загруженных сценах; проставить 95 решений; профиль с digest и ссылкой на пилотный список; markers/frames/native executor; выбор пилота как PlayerPrefab и вывод legacy position services и ClientSceneLoader; issuer/store; пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один sizing/docs commit; TMP fallback, Temp-скрипты, сцены, префабы, реестры и профиль исключены.

---

## Итерация от 2026-09-09 (T-FO06H)

**Задача:** Убрать три мёртвых компонента BootstrapScene после того, как пользователь сохранил сцену.
**Несохранённые изменения:** оказались безобидными — сохранение добавило ровно три строки `_globalMotionProfile`, `_globalSpawnBootstrap`, `_globalSessionCoordinator` со значением `fileID: 0`, то есть сериализацию полей, добавленных кодом на T-FO05E. Посторонней работы в несохранённом состоянии не было.
**Результат:** Удалены три компонента через штатный `GameObjectUtility.RemoveMonoBehavioursWithMissingScript`, применённый ровно к трём опознанным объектам, без сплошной чистки сцены. Сцена сохранена.
**Подтверждение опознания:** diff показал `m_EditorClassIdentifier` удалённых компонентов — `ProjectC.Ship.Key.ShipKeyToast`, `ProjectC.Ship.Key.ShipKeyServer`, `ProjectC.Ship.Network.ShipOwnershipRegistry`. Это независимо подтверждает опознание T-FO06G по GUID и комментариям в коде. Замечание по методу: это поле стоило читать сразу в диагностике — оно даёт имя класса напрямую.
**Проверки:** скрипт отказывался работать при dirty-сцене, при числе missing-компонентов не равном трём, при неоднозначном имени и при любом отклонении счётчиков. После операции: missing `3 → 0`, количество GameObject и корней без изменений, компонентов ровно `−3`, сцена не dirty, файл на диске изменился, ни один из трёх GUID в файле не найден. Diff: 3 добавленные строки пользователя и 39 удалённых — три блока MonoBehaviour и три ссылки в `m_Component`, других изменений нет.
**Состояние редактора:** после сохранения Unity перестала отвечать на запросы инструментов; процессы живы, лог показывает успешный импорт сцены без ошибок и далее тишину. Работа проверена на файловой системе, данные не под угрозой; проверка компиляции отложена до восстановления связи.
**Файлы:** `BootstrapScene.unity`, `06H_DEAD_COMPONENT_CLEANUP.md`, roadmap и этот журнал.
**Остаток:** Восстановить связь с редактором и перепроверить компиляцию; минимальный набор сцен пилота и каталог только для него (теперь у Bootstrap есть основание для `saved = true` и нет missing-скриптов); профиль с digest и ссылкой на пилотный список; markers/frames/native executor; выбор пилота как PlayerPrefab и вывод legacy position services и ClientSceneLoader; issuer/store; пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один scene/docs commit; TMP fallback, Temp-скрипты, префабы, реестры и профиль исключены.

---

## Итерация от 2026-09-09 (T-FO06G)

**Задача:** Начать подготовку каталога в правильном порядке зависимостей и снять неопределённость по missing-компонентам Bootstrap.
**Порядок:** Профиль требует каталог и совпадающий digest, каталог требует осмотра сцен, поэтому начат осмотр, а не профиль. Установлено жёсткое требование компилятора: источник каталога обязан иметь `inspectedComplete` И `saved`, иначе `missing_uninspected_dirty_or_invalid_scene_source`. Каталог для несохранённой сцены невозможен, так как `dependencyHash` считается по содержимому на диске.
**Аудит сцен (собственный инструмент проекта):** 26 кандидатов, 1 осмотрена, 25 не осмотрены, 1 dirty, 61 unreviewed observation, 0 каталогов, 3 ошибки. Аудит прямо предупреждает, что dependency hash Bootstrap относится к сохранённому файлу, не к живой сцене.
**Опознание missing-скриптов:** По GUID из YAML сцены плюс подтверждение в коде установлены все три ранее UNRESOLVED компонента: `[ShipKeyToast]` `7e4592bffcae97841b2e9d1893479783` (DEPRECATED по комментарию в QuestToast.cs), `[ShipKeyServer]` `d915eb76076361f4bad13b481fe4c1cc` (CanPlayerBoard DEPRECATED, заменён MetaRequirementRegistry), `[ShipOwnershipRegistry]` `75a1a6152c04c0040b2c48c6fe27c18b` (удалён как дублирующий KeyRodInstanceWorld). Классы отсутствуют в коде, GUID отсутствуют во всех `.meta`. Вывод: мёртвые ссылки на намеренно удалённые классы, а не потерянная функциональность. Удаление не выполнялось.
**Исправление ложной диагностики:** первый прогон дал 21 «отсутствующий» компонент, но 18 оказались ссылками на встроенные объекты движка (`fileID 19102`, guid `0000…e000…`), у которых закономерно нет пути ассета. Ссылкой на скрипт считается только `fileID 11500000`. После исправления ровно 3, что совпадает с 3 ошибками собственного аудита — два независимых источника согласуются. По сохранённому файлу: 136 MonoBehaviour, 118 script refs, 18 built-in refs, 84 различных GUID.
**Файлы:** `06G_SCENE_REVIEW_AND_MISSING_SCRIPTS.md`, `06G_MISSING_SCENE_SCRIPTS.json`, `tools/DiagnoseMissingSceneScripts.cs`, roadmap и этот журнал. Сцены, префабы, реестры и runtime C# не изменялись.
**Проверки:** Диагностика выполнена по сохранённому YAML, сцена не загружалась и не сохранялась, компоненты не удалялись. No compile errors. Play Mode/physics/network/screenshots/builds/auth/save access не использовались.
**Блокеры:** Несохранённые изменения сцены (предшествуют этой работе, авторство не подтверждено) и связанная невозможность убрать мёртвые компоненты без сохранения всего текущего состояния сцены. Требуется решение пользователя.
**Остаток:** После решения по сцене — минимальный набор сцен пилота (25 world-сцен входить не обязаны), каталог только для него, профиль с digest и ссылкой на пилотный список, markers/frames/native executor, выбор пилота как PlayerPrefab, вывод legacy position services и ClientSceneLoader, issuer/store, пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один diagnostics/docs commit; TMP fallback, Temp-скрипты, сцены, префабы, реестры и профиль исключены.

---

## Итерация от 2026-09-09 (T-FO06F)

**Задача:** Реализовать вариант B — применять пилотный реестр только в global-пути, не меняя сцену и не ломая legacy.
**Результат:** `GlobalMotionNetworkProfile` получил декларативное `_registryLists` (пустое = конфигурация сцены без изменений, поведение по умолчанию не меняется). В подготовке запуска добавлены `TryApplyProfileRegistry`/`RestoreRegistry`: подмена только над живой конфигурацией, ни сцена, ни ассеты списков не записываются. Исходный экземпляр списка сохраняется и восстанавливается дословно — через `Release` при остановке/отмене и через `finally` на путях отказа до создания gate; владение передаётся gate в момент создания, поэтому двойного восстановления нет. Восстановление срабатывает только если текущий список — установленный нами, тем же приёмом, что уже применён для `ConnectionData`.
**Ключевая деталь:** `Prefabs.Prefabs` — агрегированный `[NonSerialized]` кэш `m_Prefabs`, пересобираемый в `Initialize()`; в редакторе он уже заполнен из default-списка через `OnValidate`. Проверка каталога перечисляет и списки, и кэш, поэтому подмена только `NetworkPrefabsLists` оставила бы старые 58 записей в эффективном реестре. После подмены и после восстановления вызывается `Prefabs.Initialize()`. Подмена выполняется до сборки hello и до снятия хеша конфигурации, поэтому защита `bootstrap_changed_network_start_ownership` продолжает проверять именно вмешательство bootstrap.
**Принятое последствие:** `Initialize()` подписывает `OnAdd`/`OnRemove` на списки без предварительной отписки, поэтому наш вызов плюс собственный вызов NGO при старте дают повторную подписку. Обработчики срабатывают только при изменении списка в рантайме, чего пилот не делает; пакет не правился.
**Валидация:** состав реестра вынесен в чистый `GlobalMotionNetworkContract.ValidateRegistryComposition` — отклоняются `null`-элементы, дубликаты и превышение предела; пустое объявление означает отсутствие подмены. Добавлен `ValidateGlobalRegistryComposition` — 13 чистых проверок на in-memory `NetworkConfig`/`NetworkPrefabsList`, без GameObject, NetworkManager, сцен, записи ассетов и Play Mode; проверяется и поведение NGO, на которое опирается подмена.
**Файлы:** изменены `GlobalMotionNetworkProfile.cs`, `GlobalMotionNetworkStartup.cs`, `GlobalMotionNetworkContract.cs`; новый `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalRegistryComposition.cs` с `.meta` (в этой папке метаданные скриптов исторически отслеживаются, общее правило `*.meta` не менялось). Отчёт `06F_GLOBAL_PATH_REGISTRY_SWAP.md`, roadmap и этот журнал.
**Проверки:** прогон всех валидаторов floating-origin — **16 из 16, 691 PASS / 0 FAIL** (678 прежних без изменений + 13 новых), регрессий нет. No compile errors. Фактическая подмена в реальном запуске НЕ проверялась: профиль не создан, запуск требует каталога, frames и executor. Play Mode/physics/network/native executor/screenshots/builds/auth/save access не использовались.
**Остаток:** Создать профиль с каталогом из одной Spatial-записи и ссылкой на пилотный список; scene catalog/markers/frames/native executor; выбрать пилота как PlayerPrefab и вывести legacy position services и ClientSceneLoader; три missing-компонента и несохранённое состояние Bootstrap; иерархия WorldScene_0_0; issuer/store; пользовательский Host+client gate. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один code/validator/docs commit; TMP fallback, Temp-скрипты прогона, профиль, сцены, префабы и реестры исключены.

---

## Итерация от 2026-09-09 (T-FO06E)

**Задача:** Добавить BootstrapScene в Git и определить, как применять пилотный реестр.
**Versioning:** В `.gitignore` добавлены точечные исключения по полным путям для `BootstrapScene.unity` и её `.meta`; общее правило `*.unity` сохранено, `BootstrapScene (копия с компьютера DESKTOP-K00O7HK).unity` остаётся игнорируемой. Проверено `git check-ignore`.
**Поправка к 06C/06D:** формулировка «остальные префабы и сцены остаются за Lore VCS» была неточной. `git ls-files` показывает, что `WorldScene_0_0.unity` и 43 префаба (включая 20 капитанских) уже отслеживаются: правила игнорирования добавлялись позже и на tracked-файлы не действуют. Правила блокируют добавление нового контента, но часть существующего ведётся в Git. Утверждения об изоляции пилотных ассетов остаются верны.
**Несохранённая сцена:** В коммит вошла версия файла с диска, байты идентичны снимку 06A. Несохранённые изменения редактора (`isDirty=true`, зафиксировано ещё на 06A и предшествует этой работе) в коммит не входят; сцена не сохранялась, так как авторство изменений не подтверждено и сохранение — решение пользователя.
**Развилка реестра:** Эффективный реестр перед старом собирается из всех списков `NetworkPrefabsLists` плюс встроенных `Prefabs.Prefabs`, поэтому пилотный список обязан заменять default, а не добавляться. Вариант A — замена в сцене: ломает спавн всех 58 префабов в legacy-режиме, обратимо только откатом сцены. Вариант B — условная подмена в ветке, которая включается лишь при назначенном и включённом `_globalMotionProfile`: legacy сохраняется, требует правки кода и аккуратности с существующей проверкой хеша конфигурации при старте. Вариант C — отдельная пилотная bootstrap-сцена с дублированием. Рекомендован B; ни один вариант не реализован, так как выбор влияет на работоспособность текущей игры.
**Файлы:** `.gitignore`, `BootstrapScene.unity` + `.meta`, `06E_SCENE_TRACKING_AND_REGISTRY_FORK.md`, roadmap и этот журнал.
**Проверки:** No compile errors. Сцены, префабы, реестры, настройки и runtime C# не изменялись — этап касается только версионирования и анализа. Play Mode/physics/network/screenshots/builds/auth/save access не использовались.
**Остаток:** Global mode/world shift выключены, пилот не подключён. Далее: подтвердить вариант применения реестра, profile с каталогом из одной Spatial-записи и SceneLayoutDigest, scene catalog/markers/frames/native executor, вывод legacy position services, issuer/store, пользовательский Host+client gate. Jitter fixed не заявляется.
**Коммит:** один versioning/scene/docs commit; TMP fallback, Temp-скрипты, прочие сцены/префабы и DefaultNetworkPrefabs.asset исключены.

---

## Итерация от 2026-09-09 (T-FO06D)

**Задача:** Сократить реестр до минимального пилотного, чтобы проверить игрока раньше, и точно зафиксировать остаток.
**Результат:** Создан `Assets/_Project/Prefabs/FloatingOrigin/GlobalPilotNetworkPrefabs.asset` — ровно 1 запись (пилотный игрок, hash 3692800100), `Override=None`, `IsDefault=false`, ни одной ссылки из сцен. `DefaultNetworkPrefabs.asset` не изменён: 58 entries, serialized-содержимое и байты идентичны, пилот в него не утёк.
**Классификация 58 записей:** прогон реального валидатора контракта. Spatial-ready=0, NonSpatial-ready=1 (`NEWCHESTPREFAB`), требуют миграции=57, битых=0. Причины по Spatial: 54 `missing_global_motion_components_or_optin`, 2 `invalid_behaviour_count`, 2 `inactive_network_behaviour_object`. NonSpatial присваивался только при фактическом отсутствии пространственного содержимого, иначе запись отнесена к миграции.
**Структурный блокер:** `TestPlayer` и `PickupItem_Test` не имеют ни одного NetworkBehaviour; `NetworkChestContainer_Test` и `SPAWN_TEST` держат NetworkBehaviour на выключенном GameObject. Эти проверки выполняются до ветвления по роли, поэтому 4 записи не проходят ни одну роль — путь полного реестра блокирован структурно, а не только объёмом. Исправления не выполнялись.
**Остаток вне пилота:** спавн кораблей, NPC, торговых и квестовых зон, предметов, сундуков, pickup-объектов, ресурсных узлов, станций крафта и тестовых спавнеров не будет работать. Также остаются legacy ShipPositionServer/PlayerPositionServer, активный ClientSceneLoader, 3 неопознанных missing-компонента, неосмотренная WorldScene_0_0, issuer и store ownership.
**Чтобы пилот запустился:** пилотный список должен ЗАМЕНИТЬ default в BootstrapScene (эффективный реестр = все списки + встроенные записи, иначе станет 59 и каталог обязан описывать все 59); затем profile с каталогом из одной Spatial-записи и корректным SceneLayoutDigest, scene catalog/markers/frames/native executor, выбор пилота как PlayerPrefab, вывод legacy services, issuer/store и пользовательский Host+client gate.
**Файлы:** пилотный список + `.meta`, `.gitignore` (исключение расширено на `*.asset.meta` в пилотной папке для стабильных GUID), `06D_PILOT_REGISTRY_AND_REMAINDER.md`, `06D_REGISTRY_CLASSIFICATION.json`, `tools/AuditFo06DRegistryClassification.cs`, roadmap и этот журнал.
**Проверки:** аудит не изменил default-реестр (содержимое и байты); после создания списка перепроверены запись/override/IsDefault/hash и неизменность default-реестра; `git check-ignore` — `.meta` пилотного списка отслеживается, `NetworkPlayer.prefab.meta` по-прежнему игнорируется. No compile errors. Play Mode/physics/network/native executor/screenshots/builds/auth/save access не использовались.
**Остаток:** Global mode/world shift выключены, пилот не подключён, jitter fixed не заявляется.
**Коммит:** один asset/versioning/results/docs commit; TMP fallback, Temp-скрипты, DefaultNetworkPrefabs.asset, canonical префаб и сцены исключены.

---

## Итерация от 2026-09-09 (T-FO06C)

**Задача:** Сделать пилотный префаб floating-origin отслеживаемым в Git по указанию пользователя.
**Результат:** Общее правило `*.prefab` из блока Lore VCS сохранено; в конец `.gitignore` добавлено узкое отрицание для `Assets/_Project/Prefabs/FloatingOrigin` — сам префаб, его `.meta` и `.meta` папки. Метаданные включены осознанно: без них GUID нестабилен при клонировании. Глобальное снятие правила отклонено, так как добавило бы в Git все префабы проекта (корабли, NPC, зоны) и конфликтовало с Lore VCS. Отрицания размещены после правил `*.prefab` и `*.meta`, иначе не срабатывают. Сам `.gitignore` перечисляет себя, но остаётся tracked, поэтому правка коммитится.
**Проверки:** `git check-ignore -v` — пилотный префаб и оба `.meta` отслеживаются; `Assets/_Project/Prefabs/NetworkPlayer.prefab` и `BootstrapScene.unity` по-прежнему игнорируются. Повторная read-only верификация после правки: Spatial contract PASS, 6 NetworkBehaviour, пять NO flags = false, canonical остался legacy, 8 protected файлов побайтово равны 06A, автогенерация off, registry=58, пилот не зарегистрирован и не выбран. No compile errors. Play Mode/physics/network/screenshots/builds не запускались.
**Файлы:** `.gitignore`, пилотный префаб + `.meta`, `.meta` папки, `06C_PILOT_PREFAB_TRACKING.md`, уточнение §6 в `06B_PILOT_PLAYER_PREFAB.md`, roadmap и этот журнал.
**Остаток:** Изменение касается только версионирования; пилот не активирован, global mode/world shift выключены. Далее: классификация profile/catalog (эффективный registry должен точно совпадать с classified catalog — это все 58+ записей, не только игрок), prepared frames/markers/native executor, dirty Bootstrap с legacy position services/ClientSceneLoader/3 missing scripts, native audit WorldScene_0_0, issuer/store. Jitter fixed не заявляется.
**Коммит:** один commit versioning/asset/docs; TMP fallback, Temp-скрипты, DefaultNetworkPrefabs.asset, canonical префаб и сцены исключены.

---

## Итерация от 2026-09-09 (T-FO06B)

**Задача:** Перевести регистрацию сетевых префабов в явный режим и создать изолированный пилотный префаб игрока, не активируя global mode.
**Registration:** `GenerateDefaultNetworkPrefabs=true → false`, персистится в новом `ProjectSettings/NetcodeForGameObjects.asset` (Git его не игнорирует). `DefaultNetworkPrefabs.asset` не изменён: 58 entries, serialized-содержимое, bytes и meta идентичны, canonical player остался в списке. Список продолжает использоваться через NetworkConfig; отключён только AssetPostprocessor. Следствие: новые сетевые префабы требуют явной регистрации, а автоудаление записей больше не работает.
**Pilot prefab:** `Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab` создан независимой копией, а не variant — override мог бы вернуть удалённый NetworkTransform. Удалён NetworkTransform, добавлены PlayerAttacker, PlayerTarget, GlobalMotionReplicator и GlobalMotionPoseAdapter с `_coordinatesRequired=true`; SynchronizeTransform/AutoObjectParentSync/SceneMigrationSynchronization выставлены false, остальные два флага перепроверены. Порядок NetworkBehaviour (6) одинаков у сервера и клиентов, так как это один asset.
**Уточнение 06A:** ValidateLayout для Spatial требует **пятое** условие — baked PlayerAttacker/PlayerTarget; это совпадает с существующей ошибкой NetworkPlayer для global-игрока. Skill-компоненты, добавляемые owner-only в рантайме, — обычные MonoBehaviour и на порядок NetworkBehaviour не влияют.
**Файлы:** `06B_PILOT_PLAYER_PREFAB.md`, `06B_PILOT_PLAYER_PREFAB.json`, `tools/VerifyFo06BPilotPrefab.cs`, `ProjectSettings/NetcodeForGameObjects.asset`, roadmap и этот журнал. Runtime C# проекта не менялся.
**Проверки:** Spatial contract PASS (features включают Adapter/Replicator/CoordinatesRequired/PlayerAttacker/PlayerTarget), pilot hash `3692800100` ≠ canonical `186599647`, у пилота нет stock writers/nested NO/body/joint/nav/2D и лишних participants, canonical остался legacy, 8 protected файлов побайтово равны committed 06A baseline, автогенерация off, registry=58, pilot не зарегистрирован, выбранный PlayerPrefab по-прежнему canonical. No compile errors; 678 pure checks E не перезапускались. No Play Mode/physics/network/screenshots/builds/auth/save access.
**Остаток:** Пилот попадает под `.gitignore` `*.prefab`, существует только локально — принудительная публикация не выполнялась и требует решения. Далее: классификация profile/catalog (эффективный registry должен точно совпадать с classified catalog), prepared frames/markers/native executor, dirty Bootstrap с legacy position services/ClientSceneLoader/3 missing scripts, native audit WorldScene_0_0, issuer/store. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один docs/results/tool/settings commit; пилотный префаб, TMP fallback, Temp-скрипты, DefaultNetworkPrefabs.asset и сцены исключены.

---

## Итерация от 2026-09-09 (T-FO06A)

**Задача:** Завершить read-only preflight конкретного pilot scope после E, не включая global mode и не меняя пользовательские assets/settings.
**Результат:** Проверены actual NetworkManager selection и canonical NetworkPlayer, загруженная Bootstrap, NGO default registry/settings и WorldScene_0_0 asset dependencies. Найдены 4 группы player blockers: отсутствуют replicator/opt-in adapter, присутствует stock NetworkTransform, требуются global-only NO flag overrides. CC параметры, hierarchy/NetworkBehaviour order и visual references сохранены в JSON. Bootstrap dirty=true, 56 roots, 3 missing component entries с UNRESOLVED identity, enabled legacy position services и ClientSceneLoader; ничего не удалено. WorldScene hierarchy не осмотрена, прежний полный H census не закрыт.
**Registration boundary:** GenerateDefaultNetworkPrefabs=true, active default list=58 entries. Установленный NGO processor добавляет любой импортированный prefab с root NetworkObject независимо от папки: отдельная PilotAssets не обеспечивает изоляцию. Registration сама не означает spawn/global activation. Настройка и список не изменены; proposed явная регистрация с отключением auto-generation требует отдельного подтверждения, поскольку меняет добавление будущих сетевых prefab.
**Файлы:** `docs/world/floatingorigin/06A_PILOT_SCOPE_PREFLIGHT.md`, `06A_PILOT_SCOPE_BASELINE.json`, `tools/AuditFo06APilot.cs` вне Assets, roadmap и этот журнал. Runtime C# проекта не менялся; Temp runner в коммит не входит.
**Проверки:** Сохранённый read-only audit успешно выполнен внутри Editor, nested JSON evidence прошёл round-trip. Три integrity assertions PASS: loaded scene/hierarchy/dirty fingerprint; полное serialized registry и setting/dirty; SHA256 восьми protected файлов (player/Bootstrap/World/registry + meta). No compile errors. Исторические 678 pure checks E не перезапускались и не выдаются за новую проверку. No scene open/save/preview, prefab/GameObject creation, Play Mode/screenshots/physics/network/builds/auth или actual save access.
**Остаток:** Pilot configuration BLOCKED, global mode/world shift выключены. Согласовать registration policy → isolated player variant/pilot list → dirty Bootstrap/missing scripts/legacy service ownership → native WorldScene/catalog/frames/physics → issuer/store/config → пользовательский Host+client acceptance. Полные T-FO03–09 не закрыты, jitter fixed не заявляется.
**Коммит:** один audit/results/docs commit; TMP fallback, Temp runner, engine assets/settings/packages и исторические отчёты исключены.

---

## Итерация от 2026-09-09 (T-FO05E)

**Задача:** Связать подготовленные D/G/C/B с actual NMC global-session lifecycle без активации в текущей игре и без fake account identity.
**Результат:** GlobalMotionSessionCoordinator принимает explicit prepared frames/repository/timing/trusted Host policy, конфигурирует D перед существующим startup gate, принимает session-token + actual NetworkClient trusted receipts и откладывает identity/plans до World/frame readiness. Проверяются role, source ownership, уникальный PlayerId, текущая connection и confirmed PlayerObject/spawn lifetime. Account provider в исследованном пути не подтверждён (inconclusive); approval/72-byte hello не заменяются и не объявляются auth.
**Persistence/stop:** C/B checkpoint по расписанию только для полного verified/confirmed roster, backoff при неподготовленном capture, Applied-only success и latched write fault без automatic retry/quarantine. NMC deliberate stop/reconnect/restart требуют checkpoint до Shutdown либо явного abandon API; no-live-player stop не выдумывает final snapshot. Emergency/transport teardown отмечается unplanned. Individual unexpected-disconnect final-save и same-connection respawn ещё открыты.
**Legacy/UI isolation:** Global start блокируется при любых loaded ShipPositionServer/PlayerPositionServer, даже disabled: ShipPositionServer подписывается в Awake. Компоненты не удалялись; legacy-файлы не менялись. NMC global paths пропускают old Prepare/SaveNow/ClientSceneLoader reset, global NetworkPlayer рано выходит из legacy restore coroutine. Отказ checkpoint вызывает отдельный onBlocked; EscMenu сбрасывает exit-progress и открывает существующее меню, без новой layout/localization. Global teardown timeout не вызывает false success.
**Файлы:** новые GlobalSessionPersistencePolicy.cs, GlobalMotionSessionCoordinator.cs, Editor ValidateGlobalSessionOrchestration.cs и 3 meta. Точечно изменены NetworkManagerController.cs, GlobalMotionPlayerBootstrap.cs, GlobalMotionCheckpointSpawnSource.cs, NetworkPlayer.cs и EscMenuWindow.cs. Report `05E_SESSION_ORCHESTRATION.md`, JSON, roadmap и этот журнал.
**Проверки:** No compile errors; **58 E + 620 прежних = 678 pure PASS / 0 FAIL**, после final teardown guard повторены. Pure policies/schedule/ref-token/role/stop и actual C/B с memory storage; compiled native/menu seams только inspected. Actual E/NMC/auth/GUI/NGO/disk/native readiness, реальные saves, сцены, GameObjects, Play Mode, physics, network, builds/screenshots не вызывались. Read-only prefab guard: 58 candidates; opt-in/profile/loaded profile/adapters/markers/executors/source/session=0. Wire F005 и A/B formats сохранены.
**Остаток:** Код orchestration готов, actual issuer/config/store ownership и pilot content/catalog/markers/frames/prefab/profile не подготовлены. Native disk/readiness/GUI/Host+client acceptance остаётся пользовательским. Оценка D — ориентир, не автоматический процент/обратный счётчик; полные T-FO04–09 и semantic T-FO03 открыты, global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один code/meta/results/docs commit; TMP fallback, Temp runner, historical A–D reports/JSON, legacy save files, scene/prefab/profile/catalog и packages исключены.

---

## Итерация от 2026-09-09 (T-FO05D)

**Задача:** Реализовать explicit pre-spawn identity/restore plans и concrete source G; после этапа оценить оставшуюся интеграцию и тесты.
**Результат:** GlobalPlayerSpawnPlanResolver + GlobalMotionCheckpointSpawnSource реально реализуют G interface для fixed prepared frames. Configure до старта требует actual scene/physics definitions и explicit replica frame; actual I.ValidatePreparation остаётся обязательным. После connection/World start trusted caller назначает stable PlayerId actual NetworkClient reference/session/run, затем готовит checkpoint либо явно заданный first-spawn с pose/rules/frame/nonce/deadline. Нет account ID из clientId, implicit zero или nearest frame; ship/out-of-frame/unknown checkpoint без explicit first-spawn блокируются.
**Интеграционный код:** G factory guard до instantiate, после Awake/OnEnable до SpawnAsPlayerObject, confirm actual initial-ready player и handoff identity в C; original source/client/queue/nonce проверяются. Новое поле ReservationId — только server-memory plan, не wire/save. B readonly IsObservationCurrent проверяет raw published lineage без lease через NGO callbacks. Source не подключает startup/auth/loading/save schedule самостоятельно.
**Исправления review:** C.TryRetire немедленно запрещает доступ при retirement внутри readiness callback и откладывает dictionary cleanup до unwind; D.Clear не вызывает throwing busy Dispose. Cancel не отзывает identity Completed player. Disconnect observer не удаляет still-live/ref-matching peer по позднему ID-only сигналу; stale cleanup выполняется до capacity check. Native partial spawn failure требует halt/despawn/session shutdown, а не заявления об атомарном rollback.
**Файлы:** новые GlobalPlayerSpawnPlanResolver.cs, GlobalMotionCheckpointSpawnSource.cs, Editor ValidateGlobalCheckpointSpawn.cs и 3 Unity-generated meta. Изменены GlobalMotionSpawnContracts.cs, GlobalMotionPlayerBootstrap.cs, GlobalPlayerCheckpointRepository.cs и GlobalMotionPlayerCheckpointSource.cs. Отчёт/оценка `docs/world/floatingorigin/05D_CHECKPOINT_SPAWN_SOURCE.md`, JSON, roadmap, этот журнал.
**Проверки:** No compile errors; **60 D + 560 прежних = 620 pure PASS / 0 FAIL**. Pure lookup/projection/pose/rules/nonce/deadline/cancel policy, actual B repository с memory storage и fake-interface guard dispatch. Native Mono sources, actual identity/NGO/CC/disposal, disk/crash/network/IL2CPP UNTESTED. Guard: 58 prefab candidates; opt-in/profile/loaded profiles/adapters/markers/executors/checkpointSources=0. Ни GameObjects, ни сцены/префабы, реальные saves, Play Mode, physics, networking, builds или screenshots не использовались.
**Остаток:** Concrete fixed-frame G source теперь есть, но actual auth input (поиск inconclusive), orchestration/configuration, content/catalog/frame/prefab migration и native save lifecycle отсутствуют. Полные T-FO04–09 и semantic T-FO03 не закрыты. Оценка после D: 4–6 этапов до узкого fixed-frame Host+client прогона; 8–12 до первого реального rebase; 20–35 итераций по 10 крупным блокам до полной интеграции плюс ориентировочно 2–3 пользовательских тестовых цикла. Рубежи включают предыдущие, не суммируются; unknown native/scene scope может увеличить оценку. World shift/global mode выключены, jitter fixed не заявляется.
**Коммит:** один code/meta/results/docs commit, без TMP fallback, Temp runner, historical A/B/C report/JSON, scenes/prefabs/profile/catalog или package changes.

---

## Итерация от 2026-09-09 (T-FO05C)

**Задача:** Подготовить executable authoritative global player capture/merge и live guard перед B publication, не включая save/load в игре.
**Результат:** GlobalMotionPlayerCheckpointSource читает только latest server-accepted WORLD motion, проверяет полный connected-player roster и выдаёт ephemeral snapshots по explicit trusted stable identity leases. Lease привязана к реальным NetworkClient/PlayerObject refs и session/run/spawn; native source только на World Unity thread. Новые readonly World/Replicator APIs не меняют wire, sequence или Transform. Ship/ParentLocal/unmapped/unready players блокируют весь capture, не пропускаются.
**Evidence/liveness:** Instance-issued snapshots (weak table), bounded freshness, exact full binding/owner/identity lease/roster. Новая обычная accepted motion допустима при монотонных seq/time — сохраняется исходный свежий point-in-time. Same-sequence rewrite, teleport/authority/respawn/reconnect/expiry отвергаются. Final capture pass без participant callbacks выявляет изменения во время обхода readiness.
**Merge/commit:** Offline записи сохраняются, active IDs обновляются по global point и trusted caller UTC timestamp; clock regression не клэмпится. Single-use prepared batches, B CAS original observation и optional synchronous publicationGuard до staging и перед publish с raw-state recheck. Pending после позднего отказа явно возвращается без авто-cleanup/retry; escaped storage failure после входа не маскируется как NotApplied. Не вызываются legacy restore/Transform teleport, реальные files или auto-save loop.
**Файлы:** новые GlobalPlayerCaptureContracts.cs, GlobalMotionPlayerCheckpointSource.cs, GlobalPlayerCheckpointCapture.cs и Editor ValidateGlobalCheckpointCapture.cs, четыре Unity-generated meta. Точечно изменены GlobalMotionWorld, GlobalMotionReplicator и GlobalPlayerCheckpointRepository; A/B schemas и F005 protocol сохранены. C report/validation JSON, roadmap и этот журнал.
**Проверки:** compile PASS; **58 capture/merge/guard + 502 прежних = 560 pure PASS / 0 FAIL**. Контролируемый source, pure GlobalMotionAdmission/policy и реальный B repository against in-memory storage: ownership/freshness/sequence/identity/scope guards, offline retention, non-starvation при движении, staged expiry/teleport, explicit pending quarantine, CAS/single-use/re-entrancy/thread cases. Compiled native seams только inspected, реальные NGO actors/source/disk не запускались.
**Границы:** auth mapping до spawn и genuine G provider/restore-plan не реализованы; поиск отдельного account provider в исследованном пути inconclusive. Ship/ParentLocal/NPC/RPC, disconnect final-save, native disk/crash/network/IL2CPP и scheduling/performance acceptance открыты. Guard повторно: 58 candidates, opt-in/profile/loaded profiles/adapters/markers/executors=0. No GameObjects/Play Mode/physics/network/screenshots/builds или actual save access. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один commit source/meta/results/docs. Temp/Aura runners и несвязанный LiberationSans fallback исключены; historical A/B/I reports/JSON не переписывались.

---

## Итерация от 2026-09-09 (T-FO05B)

**Задача:** Реализовать отдельный transactional global player checkpoint repository с backup/recovery, не подключая его к работающим saves и source G.
**Результат:** Immutable полный player snapshot с StoreId/revision/fresh commit/parent IDs и canonical UTF8 envelope поверх frozen A v1 records. Repository-instance observation fingerprint связывает primary/backup/pending bytes; cooperative lease сериализует чтение/публикацию. Нет persisted runtime frame/NGO ids, default save path или auth inference.
**Native adapter:** DirectoryGlobalPlayerCheckpointStorage требует явный canonical absolute local directory, использует фиксированные non-legacy filenames, FileShare.None lock, CreateNew pending + Flush(true), File.Replace с previous backup либо File.Move при первой записи. Нет File.Copy overwrite/delete-before-move fallback. Permission/IO errors не означают Empty; unknown/future/foreign schema и broken backup lineage блокируются. Native adapter фактически не вызывался.
**Recovery/guards:** Backup только как explicit RecoveryCandidate, не автоматическая загрузка; recovery сохраняет known-good backup и corrupt primary bytes в quarantine, mint-ит fresh CommitId против ABA. Pending никогда не авто-promote-ится, только явный byte-verified quarantine. После exception результат публикации перепроверяется; Applied/NotApplied/Conflict/Unavailable/Indeterminate/RecoveryRequired не маскируют partial/uncertain state. Missing offline players требуют explicit removal authorization, older timestamps отвергаются.
**Файлы:** GlobalPlayerCheckpointSnapshot.cs, GlobalPlayerCheckpointStorage.cs, GlobalPlayerCheckpointRepository.cs; Editor ValidateGlobalCheckpointTransactions.cs и четыре Unity-generated script meta. 05B_CHECKPOINT_TRANSACTIONS.md / 05B_STATIC_VALIDATION.json, roadmap и существующий журнал. A formats/legacy repository/current save-load и прочий runtime не изменялись.
**Проверки:** compile PASS; **68 transaction-model + 434 прежних = 502 pure PASS / 0 FAIL**. Golden envelope, immutable/sorted scope, staged partial/corrupt writes, exceptions before/after publication/quarantine, broken backup/head, unsupported replace, lost read access, stale/cooperative/reentrant writers, explicit recovery/ABA, offline/timestamp guards. Всё на in-memory storage model; не доказательство native disk crash atomicity.
**Границы:** user save data не читались/не писались, реальные backups не создавались. Native FS/Replace/Flush/locking/power-loss/IL2CPP acceptance, authoritative collector/merge, stable identity/auth mapping, G spawn source и ship/RPC migration открыты. Нет directory fsync/гарантии всех FS/cloud mounts или универсального recovery любых blocked файлов; quarantines не очищаются автоматически. No Play Mode/GameObjects/network/physics/screenshots/builds. Guard: 58 candidates, opt-in/profile/loaded profiles/adapters/markers/executors=0. Global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один коммит code/meta/results/docs; Temp/Aura runners и LiberationSans fallback исключены. Исторические A/I/H reports/JSON сохранены.

---

## Итерация от 2026-09-09 (T-FO05A)

**Задача:** Подготовить безопасный global player position persistence контракт как зависимость G source, без активации save/load миграции и пересечения runtime gate T-FO04.
**Исходная граница:** PlayerPositionSaveData сохраняет float px/py/pz и NGO clientId в общем ShipPositions.json с ships. PlayerPositionServer восстанавливает по clientId, repository пишет через JsonUtility + File.WriteAllText. Постоянная account identity в исследованном пути не подтверждена; нет оснований сохранять runtime frameId или считать clientId устойчивым ключом. Старый DTO/repository/collector/restore не изменялись.
**Результат:** GlobalPlayerPositionRecord (immutable global point/persistent ID/ship affinity/timestamp); fileless GlobalPlayerPositionCodec с отдельным schema v1, invariant round-trip double strings, typed SHA256 и exact canonical JSON validation. Missing/unknown/duplicate/malformed/default inputs отклоняются. Checksum не является authentication. Persisted frame/origin/NGO ids отсутствуют, нулевая точка возможна только как явно предоставленные данные.
**Legacy import:** LegacyPlayerPositionImport создаёт all-player draft только для текущего известного compact JsonUtility wrapper. Требуются digest конкретного input text, полное взаимно-однозначное identity сопоставление и reviewed absolute либо explicit valid local-frame provenance. Нельзя угадать origin, восстановить потерянную float precision или принять новый clientId за identity. Ships не конвертируются/не отбрасываются; весь supplied text удерживается, но это НЕ disk backup. Older/pretty/reordered/extended layouts fail-closed; ошибка одной записи не публикует частичный результат.
**Файлы:** три новых runtime helper в Scripts/World/FloatingOrigin/Persistence, Editor ValidateGlobalPlayerPersistence, четыре script meta + folder meta; 05A_PLAYER_GLOBAL_PERSISTENCE.md, 05A_STATIC_VALIDATION.json; roadmap и этот журнал. Другие runtime C#, scenes/prefabs/assets/packages не изменялись.
**Проверки:** compile PASS; **69 persistence + 365 прежних = 434 pure PASS / 0 FAIL**. Frozen v1 JSON/checksum и known legacy fixture, double extremes/100 generated points, independent frames/locale, integrity/shape/size bounds, identity/provenance/timestamp guards и unchanged legacy DTO. Всё выполнено в памяти; настоящий persistentDataPath/ShipPositions.json/repository не читались и не вызывались. Guard: 58 candidates, opt-in/profile assets/loaded profiles/adapters/markers/executors=0.
**Границы:** нет transactional storage/backup/recovery, auth mapping, native restore/checkpoint collection или genuine source G; ships/RPC и дальнейшие native bridges открыты. Нельзя записывать player-only draft поверх unified legacy file. Без Play Mode, сетевых сессий, GameObject/physics тестов, screenshots и builds; actual save/reconnect/runtime UNTESTED. NGO protocol остаётся 0xF005, legacy=0; global mode/world shift выключены, jitter fixed не заявляется.
**Коммит:** один коммит кода/meta/результатов/документации. Temp/Aura runners и несвязанный LiberationSans fallback исключены; исторические I/H/G reports и JSON не переписываются.

---

## Итерация от 2026-09-09 (T-FO04I)

**Задача:** Реализовать ограниченный native scene-source binding/executor поверх H ledger, не активируя неподготовленную игру.
**Результат:** GlobalSceneSourceMarker хранит baked identity/frame/activation intent; GlobalSceneNativeExecutor проверяет каждый catalog root/NetworkObject и полный preloaded initial scene set. До NGO Start размещается только уже inactive static content по явной GlobalPosition; server Spawn NONSPATIAL scene services выполняется после native sweep с receipts после факта. Клиент включает prepared sources в OnClientStarted до NGO lookup; server public Additive synchronization сохраняет prepared scene instances. Remote approval и G player spawn ждут scene receipts; host-local approval не блокируется циклом до OnServerStarted. Protocol=0xF005, legacy=0 не изменён.
**Retirement/guards:** CanRecordRetired — read-only preflight без потребления receipt. Local child-first Despawn(false)/deactivation → receipt; unknown runtime roots/NetworkObject descendants/persistent infrastructure блокируют retire. Explicit activation, enabled/inactive source, parent/frame/scene checks и post-native callback shutdown guards. Initial receipt timeout=60s. Rollback transform только до networking, для inactive unchanged identity; partial native failure означает fault/stop, не фиктивный atomic rollback.
**Файлы:** новые GlobalSceneSourceMarker.cs (включая pure policy/admission contract), GlobalSceneNativeExecutor.cs, Editor ValidateGlobalSceneExecution.cs и три Unity-generated meta; изменены GlobalMotionPlayerBootstrap/NetworkStartup/NetworkContract, GlobalSceneLifecycleLedger, исторический H validator. Добавлены I report и два JSON, обновлены roadmap и этот журнал.
**Проверки:** compile PASS; фактически выполнено **31 I + 56 H + 44 G + 40 hierarchy + 48 startup + 26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 365 pure PASS / 0 FAIL**. В static review исправлены client inactive/IsSpawned lookup deadlock и Single-mode reload hazard. Эти проверки НЕ выполняли native executor operations, не проверяли реальный NGO lifecycle, physics или gameplay.
**Аудит/блокеры:** 58 prefab candidates, opt-in/profile assets/loaded profiles/adapters/source markers/scene executors=0; 26 scene candidates, loaded/dirty=1, uninspected=25, 61 Unreviewed observations, catalog assets=0. Прежние missing-component subtree diagnostics Inventory, Inventory/[ShipKeyServer], Toasts_and_meta не изменялись; сцены не загружались/не сохранялись.
**Границы:** только inactive static content и nonspatial scene services. Spatial scene actors, bodies/nav/CC, replacements/exclusions, DDOL, pool/streaming/distributed unload/recovery и genuine prepared-content/global-persistence source ещё не реализованы. Baking/назначение markers/catalog/profile не выполнены. Без Play Mode, игровых сетевых сессий, тестовых GameObject, physics simulation и screenshots. Global mode/world shift выключены; полный T-FO04 и jitter fix не заявляются.
**Коммит:** один коммит кода/meta/результатов/документации без собственного хеша отдельным коммитом. Temp runner и несвязанный LiberationSans fallback исключены; H/G исторические отчёты и JSON сохранены.

---

## Итерация от 2026-09-09 (T-FO04H)

**Задача:** Подготовить reviewed scene catalog и lifecycle сценовых/дочерних объектов без активации native миграции.
**Результат:** GlobalSceneCatalogCompiler проверяет замкнутость заявленного scene/observation/review набора, координатную семантику, authored parent graph, replacement/exclusion; deterministic SHA256 и immutable ordered plan. Profile/hello требуют computed catalog digest = declared digest, replacement prefab hash/role сверяются с классификацией. Protocol=0xF004, G hash-only global контракт несовместим; legacy=0 сохраняется.
**Lifecycle:** pure receipt ledger с уникальными ledger/load generations, same-parent/frame registration, duplicate/stale network identity rejection и отдельными registration tokens также для non-network content. Child-first cleanup/exclusions, indexed child lookup, no silent clear of live/unseen sources. Fault остаётся fail-closed до внешнего recovery. Это НЕ native Spawn/Despawn/SetParent/scene loader и НЕ readiness certificate.
**Файлы:** существующая untracked GlobalMotionSceneCatalog.cs сохранена без переписывания; новые GlobalSceneCatalogCompiler.cs, GlobalSceneLifecycleLedger.cs, Editor AuditGlobalSceneCatalog.cs и ValidateGlobalSceneCatalog.cs; пять Unity-generated meta. Изменены profile/PrefabInspector/NetworkContract, исторический G validator, roadmap и журнал; добавлены H report и два generated JSON.
**Проверки:** compile PASS; реально выполнены **56 catalog/lifecycle + 44 spawn + 40 hierarchy + 48 startup + 26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 334 pure PASS / 0 FAIL**. Повторный prefab guard: 58 candidates, opt-in/profile assets/loaded profiles/adapters=0.
**Реальные ограничения аудита:** 26 сцен-кандидатов, inspected=1 (dirty), uninspected=25, observations=61 (все draft entries Unreviewed), catalog assets=0. Missing-component diagnostics в трёх пересекающихся/отдельных поддеревьях: Inventory, Inventory/[ShipKeyServer], Toasts_and_meta; это не обязательно три уникальных скрипта. Находки не исправлялись, dirty scene не сохранялась, неизвестные сцены не загружались. Реальный каталог не утверждён.
**Границы:** native NGO in-scene sweep происходит до OnServerStarted; следующий executor должен контролировать pre-start placement, а не только поздний callback. Runtime binding/baked scene IDs, native bridges, pools и full source provider ещё не реализованы. Scene/prefab/profile activation, Play Mode, physics simulation, сетевые сессии и screenshots не выполнялись; world shift выключен, jitter fixed не заявляется.
**Коммит:** один коммит source/meta/результатов/документации; без собственного хеша отдельным коммитом. Temp runner, LiberationSans fallback и исторические G/F artifacts не включаются/не переписываются.

---

## Итерация от 2026-09-09 (T-FO04G)

**Задача:** Реализовать ограниченный concrete player spawn bootstrap и initial local placement, не активируя неполную миграцию.
**Результат:** GlobalMotionPlayerBootstrap + startup installation/release; approved-peer tickets с epoch/serial, bounded queue, registration подготовленных frames после World.IsRunning; NGO typed global seed до instantiation; local scene/pose и initial CC hold до Awake; PostSpawn Bind → first World baseline → exact-binding initial release → ACK. Seed не передаёт origin/frame ID клиента, обновляется на network ticks для late join. Protocol=0xF003, F/E global peers несовместимы; legacy=0 сохраняется.
**Guards:** только enabled root NetworkPlayer/CC, без stock NT даже disabled, bodies/nav/joints/других colliders и дополнительных native participants; server initial World spawn синхронный. Frame lease не пересоздаётся молча; remote plans требуют game rules; foreign handler не заменяется при установке. Unspawned failures/Despawn/Release/OnDestroy очищают bookkeeping. Ошибки initial lifecycle приводят к fail-closed shutdown, не к фиктивному игроку. HP/velocities/input intent/docking/Animator не сбрасываются; SetInputEnabled не используется как coordinate pause.
**Изменения:** новые GlobalMotionSpawnContracts.cs, GlobalMotionPlayerBootstrap.cs, Editor ValidateGlobalMotionSpawn.cs и три Unity-generated meta; NetworkPlayer, GlobalMotionNetworkStartup/World/Replicator/NetworkContract и hierarchy validator; G report/JSON, roadmap и этот журнал.
**Проверки:** исправлены Scene namespace collision и только Editor serializer accessibility/definite-assignment ошибки. Compile PASS; фактически выполнены **44 spawn + 40 hierarchy + 48 startup + 26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 278 pure PASS / 0 FAIL**. Повторный audit: 58 candidates, opt-in=0/profile assets=0/loaded enabled profiles и adapters=0. Без Play Mode, тестовых GameObject, physics, реальных сетевых сессий и screenshots; runtime UNTESTED.
**Границы:** реализация IGlobalMotionPlayerSpawnSource отсутствует намеренно: prepared content/global persistence/AOI/scene coverage должны быть настоящими, startup без них блокируется. Legacy ClientSceneLoader должен быть заменён внешним content bridge, здесь не отключается. General scene-object/child lifecycle, pools, physics/nav/ship bridges и actual late join/reconnect ещё не завершены. Scene/prefab/profile activation и world shift не выполнялись; полный T-FO04/jitter fix не заявляются.
**Коммит:** один коммит кода/meta/результатов/документации, без собственного хеша отдельным коммитом. Исторические E/F artifacts сохраняются; Temp runner и несвязанный LiberationSans fallback не включаются.

---

## Итерация от 2026-09-09 (T-FO04F)

**Задача:** Подключить limited custom parent/unparent placement к новым global baselines, не активируя неподготовленные игровые объекты.
**Результат:** GlobalMotionHierarchy pure policy; server candidate preflight до InstallServerControl; parent resolve по session/object/spawn + same frame/scene + ready/no-cycle; SetParent(false) и явная pose только при новом binding до cache callbacks/ACK. World detach использует GlobalPosition, а не старый local Vector3. Same-binding drift не исправляется reparent на каждом sample.
**Guards:** Rigidbody/joints, произвольные colliders/2D, enabled NavMeshAgent и cross-scene/frame changes блокируются. Root CC допускается при unit-scale parent с восстановлением enabled. Не переписываются velocities/HP/docking/Animator; native hierarchy/physics rollback отсутствует. Ошибка после записи → Faulted/revoke/Stop при живой сети, требуется внешний recovery. Global protocol поднят до 0xF002, F000–F0FF reserved; legacy=0 не меняется.
**Изменения:** GlobalMotionHierarchy.cs, Editor ValidateGlobalMotionHierarchy.cs и два Unity-generated meta; GlobalMotionPoseAdapter, GlobalMotionReplicator, GlobalMotionWorld, GlobalMotionNetworkContract, NetworkManagerController; F report/validation JSON, roadmap и журнал.
**Проверки:** после исправления CS1729 только в новом validator compile PASS (pose получается через public buffer без расширения production API). Финальный source review пройден. Реально выполнены **40 hierarchy + 48 startup + 26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 234 pure PASS / 0 FAIL**. Повторный asset guard: 58 candidates, opt-in=0/profile assets=0/loaded enabled profiles и adapters=0. Play Mode, GameObject-тесты, native physics/network и screenshots не запускались; runtime UNTESTED.
**Границы:** сцены/префабы/профиль не изменялись, world shift выключен. NPC/crew/player gameplay parenting пока не перенаправлен; concrete spawn/bootstrap/initial placement, child lifecycle и native bridges T-FO05–08 ещё обязательны. Полный T-FO04 и jitter fix не заявляются.
**Коммит:** один коммит кода/meta/документации, без отдельного собственного хеша. E artifacts, Temp runner и несвязанный LiberationSans fallback не изменяются/не включаются.

---

## Итерация от 2026-09-09 (T-FO04E)

**Задача:** Завершить dormant startup/layout/spawn/parent contracts без активации неполной координатной миграции.
**Результат:** Канонический SHA256 prefab catalog с выбранным PlayerPrefab и порядком NB, strict 72-byte hello; opt-in profile class (asset не создан), read-only metadata preflight, per-manager approval gate и IGlobalMotionSpawnBootstrap. Startup не перезаписывает native hash/config поля, чужой callback или непустой payload. Approval не создаёт legacy player автоматически; concrete global placement/spawn/parenting ещё не реализованы.
**Guards:** NetworkManagerController проверяет контракт до всех трёх Start; adapter Bind требует startup gate и отключённые native spawn/parent flags; global player не добавляет combat NB поздно; legacy ScenePlacedObjectSpawner уступает активной global session provider. При unassigned/disabled profile сохраняется legacy ветка. ValidateNetworkStart — read-only контракт уже подготовленного мира, не native freeze/loader.
**Изменения:** четыре новых runtime source GlobalMotionNetworkContract/Profile/PrefabInspector/NetworkStartup; два новых Editor source ValidateGlobalMotionNetworkContracts и AuditGlobalMotionPrefabContracts; шесть Unity-generated meta; четыре существующих source (NetworkManagerController, NetworkPlayer, GlobalMotionPoseAdapter, ScenePlacedObjectSpawner); `04E_NETWORK_STARTUP_CONTRACTS.md`, два generated JSON, roadmap и этот журнал.
**Проверки:** compile PASS; реально выполнены **48 startup + 26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 194 pure PASS / 0 FAIL**. Audit: один prefab list, 58 candidate prefabs, opt-in candidates=0, profile assets=0, loaded enabled profiles/adapters=0. Проверки не создавали тестовых GameObject и не выполняли Play Mode/physics/network/screenshots. Live callbacks, runtime/gameplay и performance UNTESTED.
**Границы:** текущие scenes/prefabs не редактировались, world shift выключен; scene digest — заявленный будущий manifest, не доказанная полнота сцен. Concrete spawn/parent/native bridges и T-FO05–08 ещё обязательны. T-FO04 в целом не завершён; jitter fixed не заявляется.
**Коммит:** один коммит кода, meta, audit и документации; временный Temp runner и несвязанный LiberationSans fallback не включать. Собственный хеш отдельным коммитом не фиксируется.

---

## Итерация от 2026-09-09 (T-FO04D)

**Задача:** Подключить dormant coordinate readiness/cache hooks к игровым контроллерам, не активируя неполную миграцию.
**Результат:** `GlobalMotionActorContract.cs` (interface/state/link), serialized opt-in=false, two-stage baseline placed → actor ready → publication acknowledgement. Gates в NetworkPlayer Update/Fixed/Move, ShipController input/physics, NpcBrain, NpcSocialBrain.Tick, NpcShipController.NavTick, SkillInputService и SkillAnimationPlayer. Baseline обновляет пространственные кэши, но не velocity/HP/docking/aggro. Старые Vector3 player teleports/restore/correction блокируются только в global mode.
**Native граница:** Ship/NPC требуют внешней подготовки body/nav и exact-binding Confirm API. Сам gate НЕ замораживает Rigidbody/NavMeshAgent; native bridge ещё не реализован. SetInputEnabled/ExitDocked/ForceSurrender не используются как coordinate pause. Initial placement и полная активация префабов остаются впереди.
**Изменения:** новый contract + Editor `ValidateGlobalMotionActorReadiness.cs` и два Unity-generated meta; `GlobalMotionPoseAdapter.cs`, `GlobalMotionWorld.cs`; семь игровых исходников из `04D_ACTOR_READINESS.md`; отчёт, roadmap и этот журнал.
**Проверки:** compile PASS; реально выполнены **26 actor + 32 application + 32 transport + 33 protocol + 23 foundation = 146 PASS / 0 FAIL**. Только pure/state/compiled-contract checks. Play Mode, GameObject-тесты, physics simulation, сеть и screenshots не запускались; native/gameplay результат не заявляется.
**Границы:** сцены/префабы не менялись, opt-in нигде не включён; legacy gates проходят исходное поведение. Animator не отключается; stale cast cancellation касается opt-in teleport/handoff, не будущего rebase. Тикет T-FO04 в целом не завершён. Следующий T-FO04E — согласованные spawn/parent/prefab contracts; world shift выключен.
**Коммит:** один коммит кода, meta и документации; без отдельной фиксации хеша. Несвязанный LiberationSans fallback оставлен вне этапа.

---

## Итерация от 2026-09-09 (T-FO04C)

**Задача:** Добавить session/frame coordinator и безопасно ограниченный Unity pose adapter поверх global motion transport, не включая неподготовленную игру.
**Результат:** GlobalMotionWorld с одним issuer на живую NGO-сессию и immutable PhysicsScene frame registrations; GlobalMotionPoseAdapter с explicit Bind, baseline-before-publish, role separation, parent-chain readiness, synchronous pooled despawn и guarded Transform/CC/kinematic Rigidbody writes. Authority не получает обычные echo poses; remote dynamic body, активный nav teleport, вложенная физика/joints и интерполируемый parent render pose для server replica блокируются. В transport добавлены revoke acknowledgement и безопасный sequence resume.
**Изменения:** `GlobalMotionApplication.cs`, `GlobalMotionWorld.cs`, `GlobalMotionPoseAdapter.cs`, `GlobalMotionReplicator.cs`; Editor `ValidateGlobalMotionApplication.cs`; четыре Unity-generated meta; `docs/world/floatingorigin/04C_FRAME_POSE_ADAPTER.md`, roadmap и эта запись.
**Проверки:** compile PASS; фактически вызваны Run(): **32 application + 32 transport + 33 protocol + 23 foundation = 120 PASS / 0 FAIL**. Это чистые Edit Mode проверки, не native callbacks/physics/runtime tests. Play Mode, GameObject-тесты, screenshots и реальные сетевые сессии не выполнялись.
**Lifecycle:** disable намеренно unbind/stop; повторное включение требует explicit Bind/reactivation. Coordinator сохраняет issuer при disable/re-enable внутри той же NGO-сессии; реальный shutdown его очищает. Despawn cleanup не отправляет RPC.
**Границы:** компоненты не установлены на игровые префабы/сцены; gameplay controllers пока не читают IsReadyForSimulation. NT, Animator/root-motion, RPC/persistence и world shift не подключались/не менялись. Следующий T-FO04D — dormant actor readiness/lifecycle/cache hooks и согласованная spawn/parent/prefab подготовка. Полный T-FO04 не завершён, jitter fixed не заявляется.
**Коммит:** код, meta и документация вместе; без отдельного коммита хеша. Несвязанный LiberationSans fallback не включать.

---

## Итерация от 2026-09-09 (T-FO04B)

**Задача:** Связать global motion protocol с NGO lifecycle/control/motion без преждевременного подключения к игре.
**Результат:** Добавлены GlobalMotionSession, GlobalMotionControl/Receiver, GlobalMotionAdmission и настоящий NetworkBehaviour GlobalMotionReplicator. Reliable control/initial sync/keyframes, unreliable owner→server→clients motion, sender+owner+epoch/time/rate проверки, baseline acknowledgement, stop/rebind при ownership/parent изменениях и Tick cleanup. Компонент не пишет Transform и не установлен на существующие префабы.
**Изменения:** четыре новых source в `Assets/_Project/Scripts/World/FloatingOrigin/Network/` + Unity-generated meta; Editor `ValidateGlobalMotionTransport.cs` + meta; `docs/world/floatingorigin/04B_NGO_TRANSPORT.md`, roadmap и эта запись.
**Проверки:** compile PASS; реально выполнены **32 новых + 33 protocol + 23 foundation = 88 PASS / 0 FAIL** в Edit Mode. Control payload 3/144/132 байта. Реальные RPC-сессии, Host/clients, gameplay, physics и screenshots не запускались.
**Границы:** старый NT, scene/prefab layout, gameplay RPC и сохранения не менялись. Следующий T-FO04C — frame/pose adapter и session coordinator. World shift выключен; пользователю игровой тест пока не требуется.
**Коммит:** код и документация вместе, без отдельного коммита хеша; несвязанный LiberationSans fallback не включать.

---

## Итерация от 2026-09-09 (T-FO04A)

**Задача:** Продолжить floating-origin реализацию: независимый от origin сетевой формат и receive/interpolation state machine без замены работающих компонентов.
**Результат:** Добавлены MotionStreamBinding, GlobalMotionSnapshot, GlobalMotionPose, GlobalMotionBuffer в `Assets/_Project/Scripts/World/FloatingOrigin/Network/`. Разделены World/ParentLocal, session/spawn/authority/discontinuity/parent generations; atomic decode, strict binding, stale/reordered packet rejection, bounded interpolation без хранения client origin.
**Изменения:** четыре runtime source + Unity-generated meta; `Assets/_Project/Editor/FloatingOrigin/ValidateGlobalMotionProtocol.cs` + meta; `docs/world/floatingorigin/04A_NETWORK_PROTOCOL_AND_BUFFER.md`, статус roadmap и эта запись.
**Проверки:** compile PASS после исправления CS0165 в новом validator; реально выполнены **33 motion + 23 foundation = 56 PASS, 0 FAIL** вне Play Mode. NGO payload world=123, parent-local=111 байт. Игровые соединения, physics и screenshots не запускались.
**Ограничения:** это готовый codec/buffer, но НЕ подключённый NGO transport. T-FO03 semantic/closed-scene gate остаётся открыт. Existing NT, gameplay RPC, saves, prefab layout и scene transforms не менялись. Следующий T-FO04B — lifecycle/control/motion integration. Пользователю игровые тесты пока не нужны.
**Коммит:** код, отчёт и журнал вместе, без дополнительного коммита хеша; несвязанный LiberationSans fallback не включать.

---

## Итерация от 2026-09-09 (T-FO03 — census)

**Задача:** Сделать воспроизводимую инвентаризацию пространственных зависимостей до runtime миграции.
**Результат:** `FloatingOriginMigrationCensus.cs` прочитал 639 C#, 48 shader-файлов, 75 prefab assets и открытую Bootstrap; 2033 source candidates, 1391 prefab records, 197 scene records, errors=0. Найдены 50 prefab NetworkTransform и дополнительные Pickup/Drop RPC за пределами Scripts/. Классифицированы 16 RPC-кандидатов и поля docking/NPC DTO. Полный semantic/closed-scene gate остаётся открытым.
**Изменения:** Editor scanner + Unity-generated meta; четыре generated census отчёта и `03_BOUNDARY_REVIEW.md` в `docs/world/floatingorigin`; статус roadmap. Дополнительно finite guard extreme rebase translation в OriginRebasePlan и regression test.
**Проверки:** compile PASS; foundation **23 PASS / 0 FAIL**; census errors=[]. Unity fake-null ошибка первого запуска scanner исправлена, повторный запуск успешен. Play Mode/сеть/физика/скриншоты не запускались. Работающие gameplay consumers не менялись, world shift не включён.
**Ограничения:** 2033 совпадения — кандидаты, не список доказанных багов. Missing scripts исходных assets отмечены, но не изменялись. Bootstrap наблюдалась dirty, не сохранялась. Один коммит кода/результатов/документации без отдельного хеша.

---

## Итерация от 2026-09-09 (T-FO02)

**Задача:** Реализовать проверяемое global-double/local-frame ядро без включения частично мигрированного мира.
**Результат:** Добавлены GlobalPosition, LocalCoordinateFrame, GlobalGridCoordinates и OriginRebasePlan в `Assets/_Project/Scripts/World/FloatingOrigin/`; Editor validator — `Assets/_Project/Editor/FloatingOrigin/ValidateFloatingOriginFoundation.cs`.
**Проверки:** Unity compile PASS; фактический вызов Run() вне Play Mode: **22 PASS, 0 FAIL**. Проверены точность на 10^9 м, независимые frames, границы сетки, repeated rebases, finite/overflow guards, NGO double serialization и JSON. Это НЕ runtime/visual/network-session PASS.
**Документация:** `docs/world/floatingorigin/02_FOUNDATION_IMPLEMENTATION.md`. Игровые consumers, сцены, префабы и сохранения не изменялись; floating origin не включён. Полная миграция продолжается отдельными этапами.
**Коммит:** код, Unity-generated meta, отчёт и эта запись вместе; отдельный коммит хеша не создаётся.

---

## Итерация от 2026-09-09 (T-FO01)

**Задача:** Начать полноценную floating-origin миграцию MMO Host + Clients после отрицательного T-JITTER18.
**Результат:** Подтверждены Unity 6000.5.2f1 / NGO 2.13.0, прочитан baseline и исходник NetworkTransform. Зафиксированы global-double/local-frame архитектура, независимые клиентские origins, необходимость серверных регионов и карта позиционных RPC/кэшей. Старые origin-компоненты не включались.
**Изменения:** `docs/world/floatingorigin/00_ARCHITECTURE_AND_PLAN.md`, `01_AUDIT_AND_SOURCES.md`, эта запись.
**Проверки:** READ-ONLY осмотр редактора и исходников; нет runtime PASS. Аудит ещё не является полной семантической проверкой всех механик. Play Mode/билд/скриншоты только пользователя.
**Порядок:** Один коммит кода/документации на этап, без последующего коммита хеша. Baseline `53c86fe2`; чужой `LiberationSans SDF - Fallback.asset` не включать. Следующий T-FO02 — изолированное ядро, не включение world shift.

---

## Итерация от 2026-09-08 (T-JITTER18)

**Задача:** Подключить character-visual эксперимент к настоящей игре/билду вместо ограниченного стенда и проверить его на реальной проблемной дистанции.
**Результат:** Near-origin skinning копии текущей локальной позы был подключён к реальному `NetworkPlayer`. В пользовательском Play Mode персонаж был проверен на координатах примерно `(39877.62, 2502.17, 40026.44)` — около 56,5 км от Unity-origin. Персонажа продолжает трясти. Эксперимент **не устраняет микроджиттер**. Кодовый эксперимент откатан к baseline `57572dc006381f9523ed8d3c7d6a68e2baf7551f`; актуальная документация сохранена.
**Изменения:**
- Временно добавленные/изменённые файлы T-JITTER18 удалены или восстановлены к baseline: `CharacterLocalSkinningExperiment.cs`, `NetworkPlayer.cs`, `EdgeDetectionRenderFeature.cs`.
- `docs/Character/IMPLEMENTATION_CHARACTER_LOCAL_SKINNING_EXPERIMENT.md` — зафиксированы runtime FAIL и выполненный откат.
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER_SOLUTIONS_RESEARCH.md` — зафиксирован отрицательный результат D1/D2 и остановка эксперимента.
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — актуальный статус обновлён, исторические измерения сохранены.
- `Assets/_Project/Docs/ITERATIONS.md` — эта запись.
**Проверки:** Unity compile до пользовательского прогона был PASS («No compile errors»). Runtime-проверка выполнена в реальной игре на проблемной дальней позиции; активация эксперимента подтверждалась, но визуальный джиттер сохранился. Итог теста: **FAIL**. Скриншоты/видео пользователем не предоставлялись; дополнительные регрессионные проверки после отрицательного результата не выполнялись.
**Сравнение:** проверка проводилась на штатном пути с включённым T-JITTER18; валидация показала, что локализация skinning через proxy не меняет наблюдаемый симптом на дальней позиции. Аргумент `-disableCharacterLocalSkinning` после решения об откате больше не нужен для сохранения текущего baseline.
**Откат:** код восстановлен из `57572dc006381f9523ed8d3c7d6a68e2baf7551f` без `reset --hard` и без `rebase`. Несвязанные изменения пользователя сохранены и не включаются в этап.

---

## Итерация от 2026-09-08 (T-JITTER17)

**Задача:** Задокументировать актуальную архитектуру без FloatingOrigin, проверить готовые anti-jitter решения и ограничить область исправления персонажем.
**Результат:** Docs-only этап D0 завершён. Исследованы Origin Shift, Floating Origin Network, World Streamer 2 и SAM. Готовый plug-and-play фикс для Unity 6000.5.2f1 / URP 17.5 / NGO 2.13 и нашего Humanoid не подтверждён. Масштабная миграция координат мира отложена; предложен один изолированный visual-only эксперимент A/B/C с явными условиями PASS/STOP. Эксперимент не реализован и не запускался.
**Изменения:**
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER_SOLUTIONS_RESEARCH.md` — новый анализ, первоисточники, оценка пакетов и ограниченный порядок следующих этапов.
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — ссылка на T-JITTER17 и отметка, что исторический §9.5 не является текущим планом внедрения; результаты прошлых тестов сохранены.
- `Assets/_Project/Docs/ITERATIONS.md` — запись этого этапа в существующем журнале T-JITTER; новый журнал не создавался.
**Проверки:** только документация; код, сцены, префабы, настройки и сохранения не изменялись. Пакеты не покупались/не импортировались. Play Mode и скриншоты не выполнялись; исправление симптома не заявляется. Перед docs-коммитом проверяется точный состав diff и `git diff --check`.
**Следующий этап:** только после согласования — отдельный character-visual стенд, затем пользовательский прогон; не автоматическая перестройка MMO.

---

## Итерация от 2026-08-17

**Задача:** Проверить гипотезу `skinnedMotionVectors` после body-swap персонажа (T-JITTER15)  
**Коммит:** `3b74ab5a8c5fd5312ad4d72b0fb69fe6f9931047` — T-JITTER15: диагностический фикс откатан после runtime-проверки  
**Результат:** Гипотеза не подтверждена — после отключения `skinnedMotionVectors` тряска сохранилась. Кодовый фикс откатан; запись оставлена как отрицательный результат теста.
**Изменения:**
- `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` — диагностическое изменение удалено
- Следующий шаг — runtime-зонд вершин через `SkinnedMeshRenderer.BakeMesh()`

---

## Итерация от 2026-08-17 (T-JITTER16)

**Задача:** Измерить baked-вершины персонажа в рантайме на origin и в WorldScene_0_0  
**Коммит:** `2f5191ae8917065dd421b7ceb8ca893f8c035644` — T-JITTER16: runtime vertex probe и последующая очистка  
**Результат:** Подтверждено: на `distOrigin≈56493м` local baked-vertex deltas вырастают примерно в 3–5 раз относительно origin. `local` и `relative-world` совпадают, значит источник до камеры и `NetworkTransform` — в humanoid deformation/skinning-пути при больших абсолютных координатах.
**Изменения:**
- Создан и после теста удалён временный `SkinnedVertexRuntimeProbe`.
- Удалены временные `BoneJitterRuntimeProbe`, `JitterClipProbe`, `InvestigateAnimator`.
- Компонент `ProjectC.DebugTools.SkinnedVertexRuntimeProbe` удалён из `NetworkPlayer.prefab`.
- T-JITTER15 (`skinnedMotionVectors=false` после body-swap) откатан: симптом не изменился.
- Compile check после очистки: ошибок нет.

**Историческое архитектурное решение (отложено в T-JITTER17, 2026-09-08):** перейти к local-coordinate слою для MMO: `SceneID/ChunkID + localPosition`, чтобы humanoid и физика работали рядом с Unity origin; глобальные координаты не хранить в одном float `Transform.position`. Это больше не обязательный следующий шаг устранения текущего визуального бага: сначала ограниченный character-visual эксперимент, см. запись T-JITTER17 выше.

---

## Итерация от 2026-07-14

**Задача:** Исправить баг: при перезаходе теряется доступ к кораблю (ключ в инвентаре, но корабль заблокирован)  
**Коммит:** `4b95e65` — T-KEY-FIX: persistentShipId для KeyRodInstance — фикс потери доступа к кораблю между сессиями  
**Изменения:**
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstance.cs` — добавлено поле `persistentShipId`
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceRepository.cs` — `persistentShipId` в DTO и SaveAll
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` — индекс `_instancesByPersistentId`, rebind `registeredShipId` при спавне, очистка stale-инстансов
- `Assets/_Project/Scripts/Player/ShipController.cs` — `CreateKeyInstanceWhenReady` передаёт `ShipPersistentId`

**Корень бага:** `NetworkObjectId` нестабилен между сессиями → дубликаты `KeyRodInstance` → проверка владения находила новый instance с `owner=NONE`

---

## Итерация от 2026-07 (v2)
=======


**Задача:** Исправить микротряску персонажа при standing  
**Коммит:** `3866c59` — T-JITTER01-v2: корневая причина — NetworkTransform.Interpolate конфликтует с CharacterController.Move/NavMeshAgent  
**Изменения:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — `OnNetworkSpawn`: `nt.Interpolate = false` для owner; `using Unity.Netcode.Components`; фильтрация sleeping Rigidbody + delta threshold в platform carry
- `Assets/_Project/Scripts/AI/NpcBrain.cs` — `OnNetworkSpawn`: `nt.Interpolate = false` на хосте; `using Unity.Netcode.Components`
- `Assets/_Project/Docs/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — полный v2-диагноз
- `Assets/_Editor/InvestigateAnimator.cs` — diagnostic tool (создан)

**Стратегия отката:** `git revert 3866c59`

## Итерация от 2026-08-14

**Задача:** Вынести номер версии в главном меню в поле инспектора (слова локализованы, цифры подставляются)
**Коммит:** `731db58` — T-UI03: версия в главном меню вынесена в поле инспектора
**Изменения:**
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — добавлено поле `versionText` (секция Version), подпись через `Loc.BindFormat`
- `Assets/_Project/Scripts/Localization/Loc.cs` — добавлен `BindFormat` + `FormatWithFallback`
- `Assets/_Project/Settings/Localization/UI_Table_ru.asset` — `ui.main_menu.subtitle` → `Версия Alpha {0}`
- `Assets/_Project/Settings/Localization/UI_Table_en.asset` — `ui.main_menu.subtitle` → `Alpha {0}`
- `Assets/_Project/Editor/Localization/AddMainMenuLocKeys.cs` — seed обновлён на `{0}`

---

## Итерация от 2026-08-15

**Задача:** Исправить persistence экипировки персонажа и убрать отладочную выдачу одежды при подключении  
**Коммит:** `aff555584b6a13ccce2d319a6a205b284b6efd9b` — T-EQP01: исправить persistence экипировки и убрать debug seed  
**Изменения:**
- `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` — удалена hardcoded seed-выдача тестовых предметов; добавлена загрузка экипировки при подключении; equip/unequip теперь сохраняются сразу
- `Assets/_Project/Scripts/Stats/StatsServer.cs` — добавлена загрузка equipment из общего character persistence независимо от порядка спавна серверных объектов

---

## Итерация от 2026-08-16

**Задача:** Добавить в MainMenu debug-блок для удаления отдельных состояний persistence и всех игровых сохранений  
**Коммит:** `1a354294c7c0a1810180a5cb6c3d1fe752259dbc` — T-UI04: debug-очистка persistence в MainMenu  
**Изменения:**
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — добавлен блок Debug слева сверху с кнопками очистки состояний
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — добавлены стили debug-панели
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — подключены обработчики кнопок
- `Assets/_Project/Scripts/UI/MainMenu/PersistenceDebugTools.cs` — удаление JSON/TXT persistence и trade PlayerPrefs с сохранением настроек и input bindings

