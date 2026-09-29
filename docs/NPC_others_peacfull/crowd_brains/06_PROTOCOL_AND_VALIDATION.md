# 06 — Протокол и валидация: что летит по сети и как не дать читерить

> Толпа не реплицируется. По сети ходят только: сводки, лизы (`HERO_UP/DOWN`), дельты героев и обычные боевые/диалоговые RPC. Клиентскому слову сервер не верит — каждая заявка проверяется дистанцией, LOS и лимитами.

## 6.1 Что НЕ летит (явно)

- Трансформы толпы — никогда (ни `NetworkTransform`, ни кастом).
- Состояние FSM толпы — никогда (его нет).
- Полный ростер — никогда (только сводка зоны при входе + дельты героев).
- Аниматор-параметры толпы — никогда.

## 6.2 Что летит (минимум)

```
C→S (заявки, существующими RPC где возможно):
  RequestAttackRpc(crowdTargetId, sourceId)      // как сейчас (CombatServer)
  RequestTalkToNpcRpc(crowdNpcId, treeIdHint?)   // как сейчас (QuestServer)
  RequestAdvanceDialogueRpc(...)                 // как сейчас
  ReportAlarmHeard / ReportCrowdBump (опционально, троттлированные)

S→O (наблюдателям зоны, тонкие дельты):
  ZONE_SUMMARY {zoneId, entries: [{crowdId, appearSeed, homeAnchorId, alive, heroUntil?}], density, scenario}
  HERO_UP   {crowdId, targetId?, appearSeed, anchorHint, reason, ttl, heroUntil?}
  HERO_DOWN {crowdId, outcome: Calm|Fled|Down|Dead|Surrendered|DialogClosed, attitudeDelta?}
  CROWD_HP  {crowdId, hp}                        // только героям и ударенным, не всем
  DIALOG_STEP (существующий DialogStepDto)       // только участникам диалога
```

Частота: сводка — при входе в зону/по запросу; `HERO_*` — по событию; HP — по событию; тик героев — только героям (5 Гц бой, диалог — по репликам). Никаких per-frame рассылок на толпу.

## 6.3 Идентификаторы (чтобы не пересекались)

- `clientId` — малые числа (NGO).
- `NetworkObjectId` — обычные сетевые тела (герои по должности, игроки-объекты, корабли).
- `crowdTargetId` — синтетика толпы с префиксом диапазона (предложение: старший бит + маркер `0xC...`, по аналогии с `NpcInstanceId | 0x8000...` для NPC-кораблей). Предикат `IsCrowdTarget(id)` — одно сравнение, без лукапа.
- `crowdNpcId` (строка `cr_<zone>_<index>`) — диалоговый ключ (как `NpcDefinition.npcId`, резолвится через `CrowdNpcResolver`, а не `NpcController.Registry`).
- Связка: `crowdNpcId ↔ crowdTargetId ↔ crowdId` — одна запись ростера, три представления. Путать их нельзя (диалог идёт по строке, урон — по числу).

## 6.4 Валидация заявок (античит и антилаг)

Каждая заявка проходит 4 фильтра (порядок — от дешёвого к дорогому):

1. **RateLimit.** Существующие: `CombatServer.RateLimit`, `QuestServer.maxOpsPerMinute=30`, `NpcSpawner`-подобный `maxSpawnsPerPlayerPerMinute`. Добавить: `maxCrowdFightsPerPlayerPerMinute` (например, 6) и `maxCrowdTalksPerPlayerPerMinute` (например, 10) — иначе скрипт-грифер повесит пул ложными заявками.
2. **Distance.** `MeleeRangePolicy/RangedRangePolicy` для урона; `IsWithinDistance`-эквивалент для разговора (в ship-local на корабле). Для толпы — с допуском +0.5–1.0м (компенсация клиентской петли vs серверной `coarsePos`). Превышение → reject с причиной (`OutOfRange/too far`), без штрафа, но с кулдауном заявки.
3. **LOS/Obstruction.** Тот же рейкаст, что `CombatServer.cs:209–232`: стена → miss; чужой `IDamageTarget` на пути → перенаправление (для толпы это фича: «задел соседа»).
4. **Budget/Admit.** Есть ли слот (см. `04 §4.4`)? Занят ли `crowdId` другим игроком? Жив ли? Нет → вежливый отказ (`Busy/NoSlot`), клиент показывает фолбэк, сервер ничего не тикает.

Важно: валидация заявки ≠ гарантия попадания. Попадание решает `DamageCalculator` как сейчас (hit/miss/crit), затем `ApplyDamage` в `CrowdHitProxy`.

## 6.5 NGO-специфика (наши грабли)

- **`NetworkPrefabsList` не присвоен** (известный тикет) → динамический `Spawn` ненадёжен. Схема это обходит: толпа не спавнится сетью вообще; герои-бойцы — из предзаспавненного пула слотов зоны (scene-placed слоты, как `NpcShipController`/пады через `ScenePlacedObjectSpawner`), а не `Instantiate+Spawn` в момент пинка. Диалоги — вообще без спавна (сессии).
- **Scene-placed vs dynamic.** Герои по должности — scene-placed (как сейчас). Герои толпы — динамические привязки слот↔crowdId (данные), не динамические `NetworkObject`. Визуал подмены — клиентский (переодеть пуловое тело), серверный объект не создаётся.
- **Нет второго `NetworkManager`, нет `NetworkSceneManager`** (см. AGENTS.md запреты). Схема их не требует: зоны — это `zoneId` в ростере + AOI-фильтр рассылки, а не сетевые сцены.
- **`destroyWithScene=true`** для героев толпы не применять как для обычных спавнов бездумно: слоты пула переживают смену наблюдателей; чистить — только по демоуту/рестарту зоны.
- **Host+Client jitter** (T-JITTER01: `NetworkTransform.Interpolate=false` на хосте для NPC) героям толпы не грозит в той же мере: их тик 5 Гц + Brawl без погонь даёт меньше дёргания, чем NavMesh-погоня.

## 6.6 Дельты героев (что видит наблюдатель)

Герой-боец: поза/стойка (событием), HP (событием), `aggroTarget` (событием), позиция — только если герой реально перемещается (шаг в Brawl, не каждый тик). Герой-разговорник: поворот к собеседнику + `DialogStepDto` только участникам (не всей зоне — спойлеры ни к чему).

Потеря пакетов: `HERO_UP` — reliable (иначе клиент не узнает героя); HP-дельты — unreliable-повторы допускаются (следующая дельта перезапишет); `HERO_DOWN` — reliable (иначе «вечный герой» на клиенте — чистить по TTL на клиенте тоже, см. `07`).
