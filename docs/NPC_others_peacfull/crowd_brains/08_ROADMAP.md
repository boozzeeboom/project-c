# 08 — Дорожная карта внедрения (поэтапно, с переиспользованием)

> Порядок — от дешёвого к дорогому. Каждый этап заканчивается ручной проверкой пользователем (агент Play Mode не запускает, скриншоты не делает — см. AGENTS.md, скиллы `dont-start-play`/`manual-playtest-only`). Все проверки ниже — NOT RUN до user-playtest.

## Этап A — Ростер и сводка (данные, без мозгов)

- Завести `zoneId` + `CrowdEntry`-таблицу для одной зоны («Гигант», палуба B, 40 записей) поверх смысла `ShipCrewManifest` (там уже `memberId/role/anchorId/respawnPolicy`).
- Сервер отдаёт `ZONE_SUMMARY` при входе в зону (руками через существующий hub-паттерн `DockingServer`/`QuestServer`: синглтон-`NetworkBehaviour` в `BootstrapScene` + `ScenePlacedObjectSpawner`).
- Клиент рисует 40 тел из пула на якорях (`CrewAnchors`-подход), 0 сети на тела, 0 тиков на сервере.
- Проверки: Compile (Console 0 errors); Manual: встать на палубу — 40 тел стоят/ходят по петлям; F8 — все на месте + 0 errors; F9 — возврат; Profiler: серверный кадр не вырос.

## Этап B — Пинок толпы (R-hit без R-fight)

- `CrowdHitProxy` как `IDamageTarget` (данные) + регистрация в `CombatServer` под синтетическим `crowdTargetId` (диапазон с префиксом, см. `06 §6.3`).
- `RequestAttackRpc(crowdTargetId)` идёт существующим трактом: rate → distance(+допуск) → LOS → `DamageCalculator` → `ApplyDamage` в прокси → дельта HP + хит-флеш. Без повышения (пул пока заглушен: все — flee/cower локально).
- Репутация/обида пишутся (общий хелпер из `NpcBrain.OnNpcHpChanged`: `-2` за удар, `-20` за убийство).
- Проверки: Manual: пнуть докера — флеш + крик + убежал; пнуть сквозь стену — miss; пнуть с 50м — reject; 10 пинков подряд — рейт-лимит; Profiler: тиков не прибавилось.

## Этап C — BrainPool и драка (R-fight по лизе)

- Пул 6 слотов на «Гигант», Brawl-режим героев (без NavMesh-погонь, `FaceTarget+Attack+separation`, тик 5 Гц), волна «1 + до 3 бойцов + шум» (`02 §2.4`), `Alarm` стражи.
- `HERO_UP/DOWN`, TTL 20–30 с с продлением уроном, демotion с возвратом на дом-якорь (`02 §2.7`).
- Капы: ≤3 атакующих на игрока; переполнение → фолбэк, не лаг.
- Проверки: Manual: пинок → через <1 с 1–3 бьют в ответ, остальные разбежались; тишина 30 с → разошлись по якорям; двое бьют одного — урон не спайкомится в один тик (стаггер); Profiler: сервер держит кадр при 2 драках одновременно.

## Этап D — Разговор с толпой (Dialog-лиза)

- `CrowdNpcId cr_<zone>_<index>` + `CrowdNpcResolver` (позиция/радиус) рядом с `NpcController.Registry`.
- `RequestTalkToNpcRpc(crowdNpcId)` существующим трактом: дистанция → `questDatabase`/fallback-дерево (`GetOrBuildFallbackTree`) → `OpenDialog` → `DialogStepDto`. Лок `crowdId` («занят»), grace 5–10 минут, `heroUntil` «на день» за содержательный разговор (`03 §3.3–3.4`).
- Шаблоны профессии (5–10 `DialogTree`) + генератор greeting+оффер. Именные деревья — только героям.
- Проверки: Manual: E на докере — диалог открылся; E со второго клиента тому же — «занят»; ESC — сессия закрыта; повторный подход в течение дня — помнит («тёплый»); уход из AOI — сессия закрыта по таймауту.

## Этап E — Клиентский лоск (пул, LOD, «герой на день»)

- `CrowdManager`: пул 40–60 тел, shared меши (3–5) + `NpcVisualApplier`-вариации, `LODGroup`, `CullUpdateTransforms`, тени только ближним/героям, гул толпы одним `AudioSource`, деспавн вне взгляда (GTA-приём), стаггер фаз петель.
- Бейджи: герой-боец/разговорник/знакомый/занят. «Герой на день» — приоритет в пуле + именной бейдж.
- Проверки: Manual: 60 тел на палубе — клиент держит целевой FPS нагруженной сцены `WorldScene_0_0`; скрин-пресеты ViewDistance (см. `docs/world/optimization/`) не ломают толпу; F8/F9 — толпа на месте (дети `ShipRoot`, вне FO-протокола).

## Этап F — Город (вторая зона, масштабирование)

- Перенос на квартал: `zoneId`-бюджеты (`BrainPool` 8, пул тел 50–80, AOI 80–120м), расписания смен (`homeAnchor + shiftTable`), слухи (`lastTopicSeed` + `TradeWorld`-хуки по образцу `NpcShipCargoManifest`-событий).
- Агрегатное потребление толпы в `MarketTick` (числами, не покупками).
- Проверки: Manual: вход в квартал — сводка + тела за <1 с без фриза; выход — демotion всех героев зоны; ночь — часть якорей пуста («по домам»).

## Переиспользовать (не писать заново)

- `CombatServer.ResolveAttack` + `MeleeRangePolicy/RangedRangePolicy` + LOS-рейкаст + `DamageCalculator` + кулдауны + `RateLimit`.
- `NpcTarget.ApplyDamage → OnHpChanged` логику агро-порогов (`aggroHpThreshold=25%`, `maxHitsPerMinute=3`) — вынести в хелпер ростера.
- `NpcBrain.HandleChase/HandleAttack` (дистанции, `attackExitRangeMultiplier=1.3`, `leashRange`) — как константы Brawl.
- `NpcSocialBrain`: `alarmHearingRadius=15`, `allyDeathRadius=20`, `flee/surrender`-пороги, `personalityConfig`, `grudge/vengeance`, `postCombat`, `reinforcementSeekRadius=50`, `SocialTrigger`-подход.
- `NpcGroupController`: «один думает за группу».
- `ShipCrewManifest/Manifest` + `CrewAnchors` + `SitPoint`/Activity-Anchors + `NpcIdleActivity` — якоря и расписание.
- `QuestServer.RequestTalkToNpc/AdvanceDialogue` + `QuestWorld.OpenDialog/CloseDialog` + `DialogStepDto` + `TalkedToNpcTrigger` + `GetOrBuildFallbackTree` — весь разговорный тракт.
- `QuestWorld.ModifyNpcAttitude` + `NpcAttitudeChangedEvent` + `hostilityThreshold` — отношение и переключение Passive→Aggressive.
- `NpcController.Find + IsWithinDistance` — образец резолвера для толпы.
- `NpcDefinition.services` (Trade/Repair/...) + `questOffers/questTurnIns` — дельта толпы.
- Hub-паттерн (`NpcShipServer`/`DockingServer`/`QuestServer` в `BootstrapScene` + `ScenePlacedObjectSpawner`, без второго `NetworkManager`, без `NetworkSceneManager`).
- Perf-задел: `ProjectCPerfCounters`, статические реестры вместо `FindObjectsByType`, round-robin/`WindManager`-стаггер, T-PERF02 уроки, ViewDistance-пресеты.

## Не делать (запреты)

- Не вешать `NetworkObject/NetworkTransform` на толпу. Не добавлять `NavMeshAgent` толпе. Не вводить толпу в FO-участники. Не плодить уникальные префабы/SO на каждого (шаблоны + сиды).
- Не трогать `NetworkManager`/`ClientSceneLoader`/`ScenePlacedObjectSpawner`/`WorldStreamingManager` архитектуру без отдельного тикета (см. AGENTS.md).
- Не писать `.meta`/`.asmdef` руками. Не коммитить без `git status/diff` и тикета; не push (см. AGENTS.md workflow).

## Тикеты-предложения (имена условные, нумерацию — по трекеру проекта)

- `T-CROWD-A`: ростер + `ZONE_SUMMARY` + клиентский пул на «Гиганте» (40 тел).
- `T-CROWD-B`: `CrowdHitProxy` + синтетические `targetId` + урон/обида без мозгов.
- `T-CROWD-C`: `BrainPool` + `FightLease` + Brawl + `HERO_UP/DOWN` + демotion.
- `T-CROWD-D`: `CrowdNpcId` + резолвер + Dialog-лиза + fallback/шаблонные деревья + `heroUntil`.
- `T-CROWD-E`: `CrowdManager` LOD/пул/бейджи/аудио + калибровка чисел Profiler'ом.
- `T-CROWD-F`: квартал города + расписания + слухи + агрегаты рынка.

## Ручные проверки (шаблон для каждого этапа, выполняет пользователь)

1. Compile: Console → 0 errors (после каждого этапа).
2. F8 → объекты/толпа на месте + 0 errors; F9 → возврат (Floating Origin контракт).
3. Profiler: серверный кадр и NGO-метрики до/после (сравнить, не «потеплело»).
4. Грифер-тест: 10 пинков подряд, двое бьют одного, E-спам, разговор+удар одновременно.
5. Доки: обновить `docs/dev/ITERATIONS.md` + этот каталог (что отклонено от плана и почему).
