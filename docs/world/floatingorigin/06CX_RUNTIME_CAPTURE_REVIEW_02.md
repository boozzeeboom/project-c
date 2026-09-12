# T-FO06CX — Runtime capture review 02

Дата: 2026-09-12.
Источник: `Q:\Project-c_logs\02.txt`.
Статус: **BASELINE/MOVEMENT/PASSENGER OBSERVATION PASS; REBASE EVIDENCE ABSENT**.

## Объём capture

- Размер: `2,444,598` bytes.
- Строк: `9,580`.
- `[T-FO06Y]` records: `827`.
- Наблюдаемый диапазон: `frame=257..1080`, `fixedTime=2.90..27.06`.
- Уникальных summary frames: `165`.
- Binding: `4979100810912560704/94/1/1/2`.

## Подтверждено

### Initial baseline

На `frame=257` зафиксированы:

- `ngo.ControlAccepted(revision=1, active=True)`;
- `physics.SyncTransforms.begin/end(baseline=True, role=Authority)`;
- `baseline.ActorApplied`;
- `spawn.InitialGateReleased`;
- `baseline.AcknowledgeApplied`;
- `baseline.AdapterReady(status=Ready)`.

Во всех 165 summary records сохранялись `baselinePlaced=True`, `adapter=Ready`, `decks=20`, camera `activeOwner=True` и passenger observations с `active=True`, `proxy=True`, `onNav=True`, `navActive=True`.

### Player movement

- `moveNonZero`: `313` записей;
- `jump=True`: `1` запись;
- `grounded=True`: `745` записей;
- `onPlatform=True`: `0`;
- `inShip=True`: `0`.

Movement действительно выполнялся. Capture не является idle-only. Палубный carry/ship occupancy в этом прогоне не проверялись.

## Критическое наблюдение

Игрок стартует около `[39992, 1, 40000]`, затем без движения падает ниже `deathY=0` уже на ранних кадрах. В поздних summary игрок стабилен около `[40005, 2502.17, 40001.79]`.

В этом файле нет явных строк `TeleportToClientRpc`, `rebase.*`, `rollback.*`, `manifest`, `admission` или `runtimeRebase`. Поэтому переход к высоте около `2502` в данном capture не может быть доказан как controlled rebase. Причинный writer перехода этим файлом не установлен; отдельные `PlayerRespawnTracker` записи недостаточны для доказательства полного порядка writer-а.

## Отсутствует

Counts по полному log:

- `rebase`: `0`;
- `rollback`: `0`;
- `manifest`: `0`;
- `admission`: `0`;
- `runtimeRebase`: `0`;
- `Error`: `0`;
- `Exception`: `0`.

Сохраняются `240` сообщений `Failed to create agent because it is not close enough to the NavMesh`. Это не отменяет pointwise наблюдение `20` готовых deck/passenger состояний, но не позволяет считать общий ShipDeck/NavMesh runtime gate полностью чистым.

Camera evidence неполная: поздние записи содержат `camera.LateUpdate.skip(... cursor=None)`, поэтому непрерывность camera history не доказана.

## Решение

Capture закрывает только следующие части:

- initial baseline: **PASS**;
- NGO control/tick observation: **PASS**;
- player movement: **PASS**;
- jump observation: **PARTIAL PASS**;
- deck/passenger readiness observation: **POINTWISE PASS**;
- controlled rebase: **NOT OBSERVED**;
- post-rebase continuity: **NOT OBSERVED**;
- rollback: **NOT OBSERVED**;
- manifest/admission: **NOT OBSERVED**;
- camera continuity: **INCONCLUSIVE**;
- NavMesh warnings: **OPEN**.

`runtimeRebaseReadiness` остаётся **NOT_READY**.

## Следующий шаг

Не добавлять новый pure contract и не устанавливать driver. Следующий узкий этап — source/runtime instrumentation для точной фиксации writer-а перехода `y≈0 → y≈2502` и отдельный capture с явным user-controlled rebase marker. После этого можно решать, создавать ли concrete adapter/readiness package или сначала исправлять placement/respawn boundary.
