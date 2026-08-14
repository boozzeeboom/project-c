# 🔍 Глубокий аудит системы квестов (NPC Quests v2) — третий проход

> **Дата:** 2026-08-13
> **Основание:** Глобальный ресерч + код-ревью по запросу пользователя (не стыковки, server-client корректность, нужен ли рефакторинг).
> **Метод:** полное чтение runtime-ядра (QuestServer 1677 строк, QuestWorld 1542 строки, QuestClientState, DialogWindow, QuestToast, QuestTracker, все модели Quests/Dialogue/Factions, QuestDatabase, NpcController, Triggers, Persistence, ContractMetaBridge), проверка всех ассетов `Data/` по YAML, grep-анализ call-graph (кто вызывает Attach/Refresh/Evaluate), сверка с аудитами 2026-07-09 и 2026-07-13, `git log` по Data/. Компиляция чистая (проверено).
> **Вердикт:** Каркас по-прежнему качественный, но найдено **8 критических дефектов server-client логики**, из них как минимум 3 — эксплойты уровня «одним RPC сдать любой квест без выполнения целей». Ассетная проблема 2026-07-13 закрыта лишь частично: контент восстановлен в минимуме (2 NPC / 3 диалога / 1 квест), и **весь текущий контент сломан на уровне данных** — ни один квест сейчас нельзя ни получить, ни корректно пройти.

---

## 1. Сверка с аудитом 2026-07-13

| Тема прошлого аудита | Реальность 2026-08-13 | Статус |
|---|---|---|
| 0 NpcDefinition/QuestDefinition/FactionDefinition ассетов | Восстановлены: 2 NPC (Mira, Zipun), 1 квест (collect_copper_ore), 3 диалога. **FactionDefinition по-прежнему 0** (`factions: []`) | 🟠 Частично |
| Дубль mira_default vs MiraDefault | Оба файла живы, **оба имеют `treeId: mira_default`** (коллизия lookup). Предыдущий аудит ошибочно считал mira_default «правильным» — фактически NPC ссылаются на MiraDefault | 🔴 Хуже |
| mira_default.asset corrupt (русский текст в targetNodeId) | Не исправлено | 🔴 Без изменений |
| 3 стаб-триггера | Без изменений | 🟡 |
| 6 стаб-действий | Хуже, чем считалось: **EmitEvent вообще не имеет case в switch** (не stub — полное отсутствие), FailQuest возвращает `success=true` | 🔴 Хуже |
| QuestWorld/QuestServer раздувание | Рекомендация не выполнена; QuestWorld прирос коплингом на Crafting/Skills | 🟡 Хуже |
| Устаревшие комментарии T-Q15/T-Q18 | Не исправлены; добавлен новый вводящий в заблуждение комментарий T-Q22 (см. C1) | 🟡 |
| Toast показывает ID вместо displayName (M15.1) | Не исправлено + то же в DialogWindow (speaker label) | 🟡 |
| Новые подсистемы (после 07-13) | T-CNPC-01 (AI↔attitude), T-Q28 fallback trees, T-QUEDIT v1/v2 (object-refs), T-QREWARD, T-DLG01, T-NPC24, T-KNOW, Unified Graph v5.1–v5.19 | ✅ Работа проделана большая |

---

## 2. 🔴 КРИТИЧЕСКИЕ дефекты (server-client)

### C1. TryTurnIn завершает квест БЕЗ проверки objectives — эксплойт «сдать любой квест одним RPC»

**Файлы:** `QuestWorld.cs:498-509` + `QuestWorld.cs:1126-1172` (TryAdvanceStage), вход: `QuestServer.RequestTurnInQuestRpc`.

```csharp
// QuestWorld.TryTurnIn — комментарий ВРЁТ:
// «Он сам проверит AreAllRequiredComplete + fire onCompleteActions»
if (instance.state == QuestState.Active) { ... TryAdvanceStage(clientId, instance, def2, curStage); }
```

`TryAdvanceStage` **не содержит** проверки `AreAllRequiredComplete` — он безусловно fire'ит onCompleteActions и переводит stage→next/Completed. Итог: квест в состоянии Active с невыполненными целями при turn-in молча становится Completed → TurnedIn → награды.

**Exploit-цепочка:** клиент шлёт `RequestTurnInQuestRpc(anyActiveQuestId, "")` → квест завершён. Валидация NPC при этом тоже обходится (см. C3). Rate limit 30 ops/min не спасает.

**Fix:** в TryTurnIn перед TryAdvanceStage проверять `instance.AreAllRequiredComplete(curStage)`; невыполнено → `Fail(InvalidState)`.

### C2. Двойная выдача наград (double rewards)

**Файл:** `QuestWorld.cs:1138-1142` + `QuestWorld.cs:543-546`.

Если у финального stage есть `onCompleteActions` — `TryAdvanceStage` вызывает `ApplyQuestRewards`. Затем `TryTurnIn` вызывает `ApplyQuestRewards` **повторно**. Квесты с onCompleteActions на финальном stage дают двойные кредиты/предметы/репутацию. Квесты без onCompleteActions — одинарные (расхождение поведения). Заголовок `QuestDefinition.rewards` («Этот же rewards выдаётся при CompleteObjective...») легализует путаницу.

**Fix:** единая точка выдачи — только TryTurnIn (или только completion, но тогда turn-in не должен дублировать). Убрать вызов из TryAdvanceStage.

### C3. TryTurnIn: валидация NPC обходится пустым `toNpcId`

**Файлы:** `QuestWorld.cs:515` (`if (!string.IsNullOrEmpty(toNpcId))`), `QuestServer.cs:1441`.

`FireDialogAction.CompleteObjective` вызывает `TryTurnIn(clientId, questId, string.Empty)` — хотя `talkingToNpcId` в `RequestAdvanceDialogueRpc` доступен и просто не проброшен. Клиентский `RequestTurnInQuestRpc(questId, "")` — то же самое. Проверка «этот NPC принимает этот квест» фактически отключена во всех основных путях.

**Fix:** CompleteObjective → `TryTurnIn(clientId, id, npcId)`; в RequestTurnInQuestRpc отклонять пустой toNpcId.

### C4. Нет server-side валидации дистанции до NPC

**Файл:** `QuestServer.cs:508` (RequestTalkToNpcRpc), docstring обещает «validates (rate, dist)» — дистанция не проверяется. Accept/TurnIn — аналогично. `NpcDefinition.interactionRadius` существует, но используется только клиентом (`NetworkPlayer.TryInteractNearestNpc`). Читер может открыть диалог/сдать квест у любого NPC с любой точки карты.

**Fix:** серверный distance-check через `FindNetworkPlayer(clientId).transform.position` против позиции NPC (нужен server-side реестр NPC-позиций — сейчас его нет: NpcController — не NetworkObject).

### C5. OfferQuest → Discovered: нет snapshot-push + клиент показывает «🔒 Discovered»

**Файлы:** `QuestWorld.cs:380-385` (TryOffer возвращает `code=Discovered(7)`), `QuestServer.cs:1345-1354`, `QuestToast.cs:189-197`.

FireDialogAction.OfferQuest реагирует только на `code==Ok(0)`:
- `SendQuestSnapshotToClient` **не вызывается** → CharacterWindow не видит новый Discovered-квест до переподключения (refresh RPC мёртв — см. S2);
- `DialogActionResultDto.success=false` → QuestToast рисует **«🔒 Discovered»** (лок-иконка при успешном получении квеста!), DialogWindow — «❌ Discovered».

Это основной путь выдачи квестов через T-Q28 fallback tree («Взять квест: X»). Заметность бага зависит от того, откроет ли игрок журнал.

**Fix:** считать `Discovered` успехом (`code == Ok || code == Discovered`) → push snapshot + success=true.

### C6. 8 из 13 типов DialogueCondition не реализованы — silently TRUE

**Файл:** `QuestServer.cs:1283-1327` (EvaluateSingleCondition). Обрабатываются только: HasItem, QuestStateEquals, ReputationAtLeast, FlagIsSet, TimeOfDayIn.

| Тип | Поведение |
|---|---|
| CargoHasItem (11) | `default → true` |
| QuestStageEquals (21) | `default → true` |
| QuestCompleted (22) | `default → true` |
| QuestDiscovered (23) | `default → true` |
| ReputationAtMost (31) | `default → true` |
| NpcAttitudeAtLeast (32) | `default → true` |
| PlayerInZone (41) | `default → true` |
| WasNodeVisited (43) | `default → true` |

Ветвление диалогов по «квест завершён», «отношение NPC ≥ X», «игрок в зоне» — **не работает, причём молча**: условие всегда проходит. Это ловушка для контент-дизайнера и прямое противоречие T-DLG01 (DialogueConditionDrawer позволяет выбрать любой тип).

**Fix:** реализовать как минимум QuestCompleted/QuestDiscovered/NpcAttitudeAtLeast/ReputationAtMost (тривиально через QuestWorld); для нереализованных — `Debug.LogWarning` + false, а не молчаливый true.

### C7. Триггерная система мертва целиком (Attach не вызывается никем)

**Файлы:** `QuestTriggerService.cs`, все подписчики в `QuestServer.cs:824-930`, `ContractMetaBridge.cs`.

- `QuestTriggerService.Attach()` — **0 вызовов в проекте** (grep). `_playerTriggers` всегда пуст → `Evaluate()` всегда возвращает 0.
- 11 подписок WorldEventBus в QuestServer + 3 в ContractMetaBridge шлют события в пустоту (`advances` всегда 0). Реальную работу делают только `Mark*`/`Broadcast*` побочные эффекты.
- Даже если Attach появится: `MatchesObjective` требует `trigger.TriggerId == obj.objectiveId` (конвенция «HaveItem:42»), чему не соответствует ни один ассет (objectiveId вида `obj_q_002_0_s1`). `IQuestTrigger.IsSatisfied()` не вызывается нигде.
- Фабрики `GameDay/GameWeekday/GameMonth/GameYear` триггеров существуют (`ConcreteTriggers.cs:157-220`), но не зарегистрированы в `RegisterDefaultFactories` — а QuestServer шлёт им hints.
- Фактическое продвижение objectives — только polling `QuestWorld.TickAll` (5 сек).

**Fix (два варианта, выбрать один):**
a) **Удалить** QuestTriggerService + ConcreteTriggers + Evaluate-вызовы (оставив Mark*/Broadcast), зафиксировав polling как каноническую модель — минус ~600 строк мёртвого кода;
б) Подключить Attach/Detach в TryAccept/TryAdvanceStage и привести MatchesObjective к type-aware matching — имеет смысл только ради мгновенной реакции вместо 5-секундного тика.

### C8. EmitEvent не обрабатывается вообще; EventDriven-путь мёртв end-to-end

`DialogueActionType.EmitEvent(51)` **отсутствует в switch** FireDialogAction (проверено перечислением всех case) — действие молча игнорируется, клиенту не шлётся даже stub-result. В проекте нет ни одного `WorldEventBus.Publish(new CustomEvent)`. Следовательно `WaitForEvent`/`EventDriven` objectives (`QuestWorld.cs:1103-1105` → `HasEventOccurred`) не могут быть выполнены никаким способом. §K-дизайн (EventDriven discovery) не работает, несмотря на «✅» в прошлых аудитах.

**Fix:** case EmitEvent → `w.MarkEventOccurred` + `Publish(new CustomEvent{...})`. DiscoverQuest → `TryOffer`.

---

## 3. 🟠 Данные: весь текущий контент сломан

### D1. collect_copper_ore.asset — единственный квест в DB, и он мёртв с двух сторон
- `prerequisites[0] = QuestCompleted "stage_intro_demo"` — такого квеста **нет в QuestDatabase** → `ArePrerequisitesMet`=false → TryOffer всегда `PrerequisitesNotMet`. Квест нельзя получить нигде (fallback-tree покажет «🔒 ...», диалог Миры — молчаливый обрыв).
- Единственный objective: `objectiveId` **пуст**, `required: 0` → `AreAllRequiredComplete` вакуумно true → **автозавершение за один тик после accept** (если бы квест удалось получить, например через M13QuestTriggerZone) → 200 CR + 25 репы бесплатно.
- `cargoItems[0]`: count=0, refs=null — мусорная запись (валидатор бы отловил: Error «count is 0»).
- Вывод: `QuestDefinitionValidator` ловит objectiveId/count, но **не ловит dangling prerequisite** (проверяет только пустоту и self-ref, не существование в DB).

### D2. Коллизия treeId «mira_default»
`mira_default.asset` (guid ae91887e, corrupt) и `MiraDefault.asset` (guid 828fa688, живой) — оба `treeId: mira_default`. В `QuestDatabase.dialogTrees` corrupt-ассет стоит раньше → `GetDialogTree("mira_default")` вернёт **corrupt** файл. NPC пока ссылаются на MiraDefault напрямую (по guid), но любой путь по treeId (treeIdHint в RequestTalkToNpcRpc, SwitchDialogTree, импортёры) получит сломанное дерево.

### D3. mira_default.asset — corrupt (не исправлено с прошлого аудита)
CSV column shift: `speaker.refId` = текст реплики, `text` = обрывок приветствия, `label` = «Npc: mira_01», `targetNodeId` = русский текст ответа («Я просто осматриваюсь» и т.п.). Все такие edges ведут в несуществующие ноды → диалог обрывается (isEnd). condition type=10(HasItem) с мусорным stringParam («QuestCompleted») — проходит случайно (itemId=0, CountOf=0 ≥ intParam=0).

### D4. MiraDefault.asset (живое дерево Миры) — тоже сломано
- greeting text = **«вааЫваыва»** (клавиатурный мусор, player-facing);
- единственный edge «У тебя есть работа для меня?» → `targetNodeId: offer_quest` — такой ноды **нет в nodes[]** (диалог закроется сразу после срабатывания action);
- edge gated двойным условием (legacy `condition` HasItem id=1 x1 **и** `conditions[]` HasItem мусор — сервер проверяет ОБА, см. S9); `hideIfUnavailable=1` → у свежего игрока без item id=1 edge скрыт → диалог Миры фактически = «вааЫваыва» + [До свидания];
- action OfferQuest → collect_copper_ore, который недоступен (D1) → «🔒» тост (после фикса C5 — внятная причина).

### D5. Zipun.asset — ленивая копия Mira
Тот же greetingText («Приветствую, искатель знаний»), тот же `animatorTriggerPrefix: Mira`, `questOffers/questTurnIns = find_artifact` — квеста **нет в DB** → fallback tree Зипуна = greeting + «До свидания». `npcId: Zipun` (camelCase) против конвенции `mira_01`.

### D6. FactionDefinition по-прежнему отсутствуют
`QuestDatabase.factions: []`. Регрессия 2026-07-13 не закрыта: ReputationTier/отображение фракций не из чего читать. (Коммит ff511d56 «use existing FactionDefinition assets» говорит об обратном намерении — но в Data/Factions пусто.)

### D7. Production-контент не импортирован
`Import/quests_bd_v1.csv` содержит полноценную БД квестов (q_002_x цепочки с prereq, наградами, целями TalkToNpc/HaveItem). В `Data/Quests/` — один сломанный collect_copper_ore. NPC из CSV (npc_002 «Мистер Фринли» и др.) отсутствуют в Data/Npcs. Пайплайн «CSV → SO» готов, но не применён; при этом импорт CSV без предварительной починки C6/S13 даст TalkToNpc-цели, работающие только через polling, и HaveItem по имени предмета — зависящие от ItemRegistry.

---

## 4. 🟡 Server-client: частичные некорректности

| # | Проблема | Файл:строка | Эффект |
|---|---|---|---|
| S1 | **GiveItem игнорирует количество**: `AddItemDirect(clientId, itemId, itemType)` — нет параметра count; лог врёт «x{intParam}». То же в ApplyQuestRewards (ri.count игнорируется). TakeItem количество учитывает — асимметрия | QuestServer.cs:1503, QuestWorld.cs:609 | Награды/действия «выдать N предметов» выдают 1 |
| S2 | **RequestRefreshQuests/Reputation/NpcAttitudeRpc — 0 клиентских вызовов** (grep). CharacterWindow читает только кэш snapshot | QuestServer.cs:755-793; CharacterWindow.cs:3263+ | Любой пропущенный push (C5) = вечный stale UI до reconnect |
| S3 | DeliverItem ≡ HaveItem (количество в инвентаре); предметы при turn-in **не изымаются**; `progress.completed` латчится — продал предметы после выполнения, цель остаётся выполненной | QuestWorld.cs:1074-1084, TryTurnIn | «Принеси предмет» не отнимает предмет |
| S4 | Мёртвые публичные контракты: `QuestState.Offered` никем не устанавливается; `TryAdvanceObjective` вызывается только мёртвым TriggerService; `NotifyQuestDiscoveredRpc` — пустой RPC без вызовов; `QuestResultCode.RateLimit/InventoryFull` не возвращаются нигде | — | Мёртвый API-мусор вводит в заблуждение |
| S5 | **minReputation и discoverable нигде не проверяются** на сервере (ArePrerequisitesMet их не читает) — поля-обманки в инспекторе | QuestDefinition.cs:40,64 | Гейты «по репутации» молча не работают |
| S6 | BuildNpcAttitudeSnapshot собирает NPC только из TalkToNpc-objectives квестов; комментарий T-Q15 обещает «all NpcDefinitions globally» | QuestServer.cs:1029-1046 | Badge ❤ пуст для NPC вне квестов (вcl. Миру, пока ни один квест не ссылается на неё) |
| S7 | ReachLocation: `targetSceneId` не проверяется (objective сработает в любой сцене на тех же координатах); `Vector3.zero` = «игрок не найден» (ломает легитимный origin); позиция client-authoritative → спуфится | QuestWorld.cs:1086-1094, QuestServer.cs:451 | Неточное/читерское срабатывание |
| S8 | Reflection-вызов private `SendQuestSnapshotToClient` из QuestWorld (1039-1043) и M13QuestTriggerZone (81-86) | — | Хрупко (переименование = silent fail), 2 места |
| S9 | EvaluateConditions проверяет И `condition`, И `conditions[]` — противоречит tooltip'у («Если задан — condition ignored») | QuestServer.cs:1266-1279 vs DialogueNode.cs:37-40 | Неожиданный двойной гейт (MiraDefault — живой пример) |
| S10 | DialogStepDto без speakerDisplayName → DialogWindow показывает «💬 mira_01» вместо имени | DialogWindow.cs:365-367, DialogStepDto.cs | M15.1 жив; клиенту не из чего резолвить displayName (QuestDatabase — серверный) |
| S11 | NpcController.OnTriggerEnter — любой collider с тегом Player, включая remote players → `_playerInRange` взводится чужим игроком; Cube-плейсхолдер спавнится на всех peer'ах | NpcController.cs:99-111, 68-83 | MP-ложные срабатывания |
| S12 | NpcBrain: attitude per-player, а BehaviorType (Aggressive/Passive) переключается **глобально** для NPC | NpcBrain.cs:359-376 | Игрок A нагрубил → NPC враждебен к игроку B |
| S13 | ResolveItemId fallback = индекс в Resources.LoadAll (i+1) — нестабильные ID; HasItem-condition использует только int.TryParse(stringParam) — имя предмета не резолвится (несогласовано с ResolveItemId) | QuestWorld.cs:934-941, QuestServer.cs:1296-1299 | Хрупкие ID; CSV-условия по имени молча падают |
| S14 | `_opTimestamps` не очищается при disconnect клиента | QuestServer.cs:55 | Медленная утечка памяти на long-lived сервере |
| S15 | WorldEventBus.Publish: комментарий «Snapshot-iterate» не соответствует коду (итерация живого List; unsubscribe во время publish сдвигает индексы) | WorldEventBus.cs:33-44 | Потенциальный пропуск подписчика |
| S16 | AddNpcAttitude action → ModifyNpcAttitude без npcDef → нет per-NPC clamp (personalAttitudeMin/Max) и нет cross-faction attitudeLinks | QuestServer.cs:1633 | Несогласованность с полным путём ModifyNpcAttitude |

---

## 5. 🟢 Архитектура / техдолг

1. **QuestServer (1677 строк) / QuestWorld (1542 строки)** — рекомендации обоих прошлых аудитов не выполнены; рост продолжается. QuestWorld дополнительно прирос знаниями (T-KNOW), рецептами (`BuildSaveData → Crafting.CraftingWorld.GetKnownRecipeIds`) и смертельной потерей знаний (`ApplyDeathKnowledgeLoss → SkillsWorld`) — квестовый модуль становится свалкой. **Выделить:** `DialogueActionRunner` (switch на 17 case), `QuestSnapshotBuilder`, `QuestWorld.Persistence.cs` (Save/Load), fallback-tree builder (~250 строк в QuestServer).
2. **Три параллельных граф-редактора:** `QuestGraphView/QuestGraphWindow` (VisualElement, M17 v8), `QuestNodeGraphView/QuestNodeGraphWindow` (GraphView, T-U01), `UnifiedQuestGraphView` (+QuestGraphModel, v5.19, канонический по ITERATIONS) + `UnifiedQuestGraphView_DEPRECATED.txt`. Оставить один (Unified), остальные удалить или явно пометить deprecated в заголовках файлов.
3. **CSV-парсер ×3** (QuestCsvSchema.cs:299+, NpcCsvImporter.cs:344+, DialogCsvImporter.cs:335+) — вынести в `CsvUtils.cs` (рекомендация прошлого аудита, не выполнена).
4. **QuestStateMirror** — дубль QuestState; комментарий «заменим когда T-Q04 введёт QuestState» — T-Q04 давно сделан, замена не выполнена.
5. **Стабы** (без изменений 2 аудита): GiveCargoItem/TakeCargoItem/**FailQuest** (возвращают `success=true` — контент «провалить квест» молча не работает), SetFlag/SwitchDialogTree/DiscoverQuest (no-op success), OpenService (success+close), триггеры CargoHasItem/LocationReached/KilledEntity (false), objective KillEntity (false).
6. **Документационный мусор:** `docs/NPC_quests/ITERATIONS.md` строки 191-199 — merge-артефакты («REPLACE», «=======»); второй ITERATIONS.md лежит в `Assets/_Project/Quests/` (дубль); аудит 07-13 утверждает «QuestGraphView.cs удалён» — файл существует (пересоздан M17 v8).
7. **3 копии BootstrapScene** (`Assets/BootstrapScene.unity`, `Assets/_Project/Scenes/BootstrapScene.unity`, «…копия с компьютера DESKTOP-K00O7HK…») — риск рассинхрона конфигурации [QuestServer]/[QuestClientState].
8. **Устаревшие комментарии** — все кейсы из аудита 07-13 (Приложение C) не исправлены; добавился новый вводящий: `QuestWorld.cs:494-497` (T-Q22 — утверждает проверку AreAllRequiredComplete, которой нет → привело к C1).
9. `M13QuestTriggerZone` (Testing) обходит TryOffer/prereq напрямую — ок для теста, но использует reflection (S8) и живёт в production-assembly.
10. `QuestClientState.AutoSpawn` — `GameObject.Find("[QuestClientState]")` по имени; races гасятся lazy-subscribe в Update (приемлемо, но хрупко).

---

## 6. ✅ Что подтверждено рабочим

- Server-authoritative каркас: RPC → QuestWorld → TargetRpc → DTO → ClientState → UI. Сессии диалога с валидацией npcId/treeId и per-nodeId.
- DTO-сериализация с null-guard через local vars (DialogStepDto/DialogOptionDto/DialogActionResultDto).
- Индексация видимых опций диалога консистентна (клиент шлёт позиционный индекс, сервер индексирует `session.visibleEdges` — десинка нет; `DialogOptionDto.index` при этом не используется — мёртвое поле).
- Persistence: atomic write (tmp+move), in-memory cache, полный набор состояний (вcl. knowledge/recipes) в BuildSaveData/LoadPlayer.
- Knowledge-система (T-KNOW) + auto-known Neutral.
- Rate limiting (30 ops/min) — есть, хоть и с утечкой (S14).
- T-CNPC-01 мост AI↔attitude: hit −2 / death −20 / порог враждебности — подписано и живо (с оговоркой S12).
- Editor-стек: AutoDiscover (scan Data/*), Validator (ловит часть дефектов D1), CSV pipeline (quests/npcs/dialogs + export), кастомные инспекторы (QuestDefinition/NpcDefinition/DialogTree), Unified Graph v5.19 (model-driven CRUD, undo/redo, авто-загрузка цепочек).
- `check_compile_errors` — чисто.

---

## 7. Приоритизированный план

### P0 — блокеры корректности (код, 1-2 дня)
- [x] **C1:** `AreAllRequiredComplete`-проверка в TryTurnIn перед TryAdvanceStage. ✅ исправлено (см. ITERATIONS.md)
- [x] **C2:** единая точка ApplyQuestRewards (убрать из TryAdvanceStage). ✅ исправлено (см. ITERATIONS.md)
- [ ] **C3:** проброс npcId в TryTurnIn из CompleteObjective; отказ пустому toNpcId в RPC.
- [ ] **C5:** `Discovered` = успех в FireDialogAction.OfferQuest → snapshot push + success=true.
- [ ] **C6:** реализовать QuestCompleted/QuestDiscovered/NpcAttitudeAtLeast/ReputationAtMost/QuestStageEquals; для остальных — Warning + false вместо silent true.
- [ ] **C8:** case EmitEvent (MarkEventOccurred + Publish CustomEvent); DiscoverQuest → TryOffer; FailQuest → реальный Active→Failed.

### P1 — контент (0,5 дня, после P0)
- [ ] Удалить `mira_default.asset` (corrupt) ИЛИ починить и убрать коллизию treeId (D2/D3).
- [ ] Починить `MiraDefault.asset`: greeting-текст, нода offer_quest, убрать мусорные conditions (D4).
- [ ] Починить `collect_copper_ore`: убрать prereq на stage_intro_demo (или восстановить квест), objectiveId задать, `required=1`, удалить мусорный cargoItems[0] (D1).
- [ ] Zipun: собственный greeting, убрать find_artifact или восстановить квест (D5).
- [ ] Восстановить FactionDefinition (16 значений enum) в Data/Factions (D6).
- [ ] Validator: +правила «prereq questId существует в DB», «хотя бы один required objective», «дубликат treeId в Data/Dialogs» (новый DialogTreeValidator).

### P2 — server-client добивка (1-2 дня)
- [ ] **C4:** server-side distance check (требует server-side реестра NPC — минимум: NetworkObject на NPC + позиция).
- [ ] **S1:** AddItemDirect(+count) или цикл; применить в GiveItem и ApplyQuestRewards.
- [ ] **S2:** подключить RequestRefresh*Rpc к открытию CharacterWindow/DialogWindow ИЛИ удалить RPC.
- [ ] **S3:** DeliverItem — изъятие предметов при turn-in (InventoryServer.TryRemove).
- [ ] **S5:** задействовать minReputation в ArePrerequisitesMet (или удалить поле); discoverable — фильтровать snapshot (или удалить).
- [ ] **S6:** attitude-snapshot от `questDatabase.npcs`, а не от objectives.
- [ ] **S10:** +speakerDisplayName в DialogStepDto (и quest displayName уже есть в snapshot — использовать в DialogWindow/Toast).
- [ ] **C7:** решение по триггерам — удалить (рекомендую) или подключить.

### P3 — техдолг (по желанию)
- [ ] Split QuestServer/QuestWorld (см. §5.1); убрать reflection (S8) — public API.
- [ ] CsvUtils.cs; удалить мёртвое (S4): Offered-state, NotifyQuestDiscoveredRpc, QuestStateMirror→QuestState.
- [ ] Консолидация граф-редакторов; почистить ITERATIONS.md (merge-артефакты) и дубль в Assets/_Project/Quests/.
- [ ] Удалить лишние BootstrapScene-копии; NpcController — фильтр IsLocalPlayer в trigger (S11).
- [ ] Стабы: GiveCargoItem/TakeCargoItem/OpenService — либо реализовать, либо fail=false с явной ошибкой (честнее для контента).

---

## 8. Итоговый вердикт

За месяц с прошлого аудита система обросла значимым функционалом (fallback-диалоги T-Q28, object-ref drag-and-drop во всех редакторах, Unified Graph v5, knowledge-зеркала, AI-мост). Но накопленный разрыв между **декларируемым** и **фактическим** поведением стал основным риском:

- **Награды и завершение квестов доверяют клиенту больше, чем задумано** (C1-C4) — это уровень «нужен фикс до любого публичного теста с реальными игроками».
- **Декларативный слой (условия диалогов, minReputation, discoverable, триггеры, EmitEvent) наполовину мёртв** (C6-C8, S5) — контент-дизайнер будет писать в пустоту без единой ошибки в консоли.
- **Весь текущий контент сломан на уровне данных** (D1-D6) — после P0-фиксов кода нужен отдельный контент-проход; Validator стоит усилить именно под найденные классы дефектов.

Рефакторинг нужен точечный, не большой переписи: P0 — это ~150 строк изменений в QuestWorld/QuestServer; дальше — удаление мёртвого кода (триггеры, RPC, состояния), которое само по себе уменьшит систему на ~700 строк и уберёт половину «не стыковок».

---

## Приложение A: Метрики (2026-08-13)

| Метрика | Значение |
|---|---|
| `.cs` в `Assets/_Project/Quests/` | 66 |
| Строк кода | ~9600 |
| QuestServer.cs / QuestWorld.cs | 1677 / 1542 строк |
| QuestDefinition / NpcDefinition / FactionDefinition / DialogTree ассетов | 1 / 2 / **0** / 3 (1 corrupt + 1 коллизия treeId) |
| Production CSV не импортирован | quests_bd_v1.csv (100+ строк) |
| Мёртвые подсистемы | QuestTriggerService (вся), EmitEvent, 3 Refresh-RPC, Offered-state |
| Нереализованные условия диалогов | 8 из 13 |
| Граф-редакторов параллельно | 3 |
| Compile errors | 0 |

---

*Аудит выполнен: 2026-08-13. Предыдущие: 2026-07-09, 2026-07-13.*
