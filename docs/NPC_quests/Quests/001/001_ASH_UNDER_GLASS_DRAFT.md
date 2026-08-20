# Q001 — «Пепел под стеклом»

> **Статус:** draft / content design only
> **Версия:** 0.1
> **Дата:** 2026-08-20
> **Реализация:** не начата
> **Мировые координаты:** намеренно не заданы
> **Канонический гайд:** `docs/NPC_quests/Quests/00_UNIVERSAL_QUEST_GUIDE.md`

---

## 1. Замысел

Игрок получает от архивистки Лиры странный сигнал из заброшенного стеклянного архива. Сигнал повторяется уже много лет и связан с исчезновением исследовательской группы. В архиве остались фрагменты журнала, чёрный ящик и несколько взаимоисключающих свидетельств.

Главная интрига: сигнал выглядит как военный вызов, но на самом деле является аварийным протоколом карантинного хранилища. Часть NPC пытается разобраться, часть хочет продать находку, а часть намеренно подсовывает игроку сфальсифицированные доказательства.

Квест строится вокруг:

- сбора нескольких физических фрагментов и предметов-доказательств;
- исследования через посещение зон и разговоры с разными специалистами;
- проверки противоречивых версий;
- выбора одного из трёх маршрутов расследования;
- двух ошибочных провальных развязок и одной осознанной предательской развязки;
- финального отчёта Лире, который зависит от выбранного маршрута.

Это не квест «принеси один предмет»: игрок сначала собирает материал, затем проверяет версии, и только после этого выбирает, кому доверить продолжение расследования.

---

## 2. Техническая форма квест-пакета

Чтобы сохранить совместимость с текущей моделью `QuestDefinition → Stage → Objectives`, пакет состоит из одного главного квеста и трёх взаимоисключающих по смыслу веток:

```text
q_001_ash_under_glass
  ├─ q_001a_signal_reconstruction   — научная ветка, истинная разгадка
  ├─ q_001b_blackbox_salvage        — прагматичная ветка, частичный успех
  └─ q_001c_silent_exchange          — тайная продажа, провальная развязка
```

Главный квест ждёт общий event:

```text
evt_q001_branch_resolved
```

Каждая успешная ветка испускает этот event в `onCompleteActions` своего финального stage. Поэтому главный квест не требует неподдерживаемой логики `OR` между objectives. Выбранная ветка определяется через `QuestCompleted(q_001a_...)` / `QuestCompleted(q_001b_...)` в финальном диалоге.

Флаги намеренно не используются как основной механизм ветвления: состояние ветки хранится отдельным quest asset и проверяется поддерживаемыми условиями диалога.

---

## 3. Стабильные ID

### 3.1 Quest IDs

| ID | Назначение | Состояние/повторяемость |
|---|---|---|
| `q_001_ash_under_glass` | Главный квест | `oneShot=true`, `discoverable=true` |
| `q_001a_signal_reconstruction` | Научная ветка | `oneShot=true`, открывается только во время main quest |
| `q_001b_blackbox_salvage` | Ветка salvage/торга | `oneShot=true`, открывается только во время main quest |
| `q_001c_silent_exchange` | Тайная ветка | `oneShot=true`, открывается только во время main quest |

Для всех четырёх квестов `rewards` выдаются только при `Completed → TurnedIn`.

### 3.2 NPC IDs

| ID | Имя | Роль | Фракция |
|---|---|---|---|
| `npc_lyra_01` | Лира Вейл | архивистка, quest giver и финальный turn-in | `GuildOfThoughts` |
| `npc_bram_01` | Брам Клин | спасатель/сборщик, прагматичный эксперт по обломкам | `FreeTraders` |
| `npc_veska_01` | Веска Орн | инженер по сигналам и старым системам | `GuildOfCreation` |
| `npc_noll_01` | Нолл Рейк | брокер, продающий сведения обеим сторонам | `FreeTraders` |
| `npc_kael_01` | Каэль Сайр | контрабандист, предлагает «тихое решение» | `Pirates` |
| `npc_sela_01` | Села Морн | бывшая операторша хранилища, ключевой свидетель | `Neutral` |

Все новые NPC должны быть экземплярами канонического NPC prefab. Канонический prefab не изменять; на scene instance назначать соответствующий `NpcDefinition`.

### 3.3 DialogTree IDs

| ID | NPC | Назначение |
|---|---|---|
| `dlg_lyra_q001` | `npc_lyra_01` | предложение main quest, исследовательский брифинг, финальный отчёт |
| `dlg_bram_q001` | `npc_bram_01` | сборочная версия и предложение salvage-ветки |
| `dlg_veska_q001` | `npc_veska_01` | техническая версия и предложение научной ветки |
| `dlg_noll_q001` | `npc_noll_01` | проверка достоверности улик, ложный манифест |
| `dlg_kael_q001` | `npc_kael_01` | скрытая ветка и предательский обмен |
| `dlg_sela_q001` | `npc_sela_01` | свидетельство бывшей операторши архива |

### 3.4 Scene/zone IDs

В draft все зоны предполагаются в существующей канонической мировой сцене `WorldScene_0_0`. Точные `targetPosition` и `targetRadius` заполняются позднее вручную.

| Zone ID | Назначение | Тип использования |
|---|---|---|
| `zone_q001_old_dock` | старый док, первый узел сбора | `ReachLocation` |
| `zone_q001_salt_archive` | соляной архив, место старых журналов | `ReachLocation` |
| `zone_q001_glass_well` | стеклянная шахта/колодец с резонансом | `ReachLocation` |
| `zone_q001_null_lighthouse` | глухой маяк с приёмником | `ReachLocation` |
| `zone_q001_quarantine_cove` | карантинная бухта, место чёрного ящика | `ReachLocation` |
| `zone_q001_broker_cache` | тайник брокера | `ReachLocation` |

Статичные точки/якоря для последующей ручной расстановки:

- `anchor_q001_lyra_archive_desk`;
- `anchor_q001_old_dock_debris`;
- `anchor_q001_salt_archive_terminal`;
- `anchor_q001_glass_well_receiver`;
- `anchor_q001_null_lighthouse_receiver`;
- `anchor_q001_quarantine_cache`;
- `anchor_q001_broker_cache`.

### 3.5 Event IDs

| Event ID | Кто испускает | Значение |
|---|---|---|
| `evt_q001_branch_resolved` | финальный успешный stage ветки A или B | выбранный маршрут расследования завершён |
| `evt_q001_signal_decoded` | резервный research event, если позже появится отдельный terminal/console emitter | сигнал расшифрован |
| `evt_q001_archive_truth_confirmed` | опциональный финальный world event | истинная версия зафиксирована после turn-in |

В первой реализации достаточно `evt_q001_branch_resolved`, испускаемого через поддерживаемый `EmitEvent` из финального branch stage. Отдельный console emitter для `evt_q001_signal_decoded` не обязателен.

---

## 4. Предметы и доказательства

Все предметы ниже должны быть реальными `ItemData` assets с object reference в objectives/actions. Строковые ID использовать только как согласованный debug fallback.

| Item ID | Название | Источник | Использование |
|---|---|---|---|
| `item_q001_sealed_note` | запечатанная записка Лиры | выдаётся при принятии main quest | ведёт к первому месту, не сдаётся |
| `item_q001_fragment_dock` | фрагмент журнала из дока | `zone_q001_old_dock` | обязательная улика |
| `item_q001_fragment_archive` | фрагмент журнала из архива | `zone_q001_salt_archive` | обязательная улика |
| `item_q001_fragment_sela` | повреждённый протокол Селы | разговор/исследование с Селой | обязательная улика |
| `item_q001_vault_key` | ключ карантинного хранилища | выдаёт Села после правильного разговора | открывает путь к исследованию |
| `item_q001_resonance_lens` | резонансная линза | выдаёт Веска в научной ветке | декодирование сигнала |
| `item_q001_blackbox_core` | чёрный ящик экспедиции | `zone_q001_quarantine_cove` | salvage/тайная ветка |
| `item_q001_false_seal` | поддельная печать хранилища | Нолл или Каэль | ошибочная улика, может вызвать провал |
| `item_q001_false_manifest` | фальшивый манифест перевозки | Нолл | ошибочная улика, может вызвать провал |
| `item_q001_truth_packet` | подтверждённый пакет доказательств | выдаётся при успешной branch turn-in | нужен для финального отчёта |
| `item_q001_broker_packet` | скомпрометированный пакет данных | выдаётся в salvage-ветке | фиксирует частичный успех |
| `item_q001_archive_shard` | осколок архивного ядра | финальная награда main quest | основной уникальный reward |

### Правила потребления

- `DeliverItem` для `item_q001_blackbox_core` должен потреблять предмет при успешном turn-in ветки B или C.
- `item_q001_false_seal` и `item_q001_false_manifest` не должны автоматически считаться правильными доказательствами через `HaveItem`.
- Нельзя заменять реальные ItemData на абстрактный `Resources`-тип.
- `item_q001_truth_packet` и `item_q001_broker_packet` — промежуточные предметы доказательства; их не выдавать через `QuestDefinition.rewards` main quest.

---

## 5. Сюжетная структура

### Акт I — Сигнал

Лира сообщает, что из района заброшенного архива снова пришёл старый сигнал. Она не просит «найти артефакт», а просит собрать независимые подтверждения: кто оставил сигнал, зачем, и почему экспедиция исчезла.

Игрок получает записку и отправляется в старый док.

### Акт II — Сбор и проверка

Игрок собирает первые фрагменты, встречает Брама, посещает соляной архив и разговаривает с Селой. Версии расходятся:

- Брам считает, что экспедиция погибла из-за жадности перевозчиков;
- Веска считает, что это был отказ системы;
- Нолл утверждает, что архив скрывает военный груз;
- Села помнит только часть протокола и боится назвать владельца хранилища.

### Акт III — Выбор метода

После получения ключа хранилища и осмотра стеклянной шахты main quest доходит до stage выбора. Игрок не получает три кнопки в одном меню. Он сам решает, к кому обратиться:

- к Веске — научное восстановление сигнала;
- к Браму — извлечение чёрного ящика и прагматичная проверка;
- к Каэлю — скрытая продажа информации.

Нолл остаётся промежуточным персонажем: он может снабдить игрока ложной уликой и проверяет, насколько игрок готов принимать удобную версию без перепроверки.

### Акт IV — Развязка

Успешная научная или salvage-ветка испускает общий event и переводит main quest на финальный разговор с Лирой. Финальный текст и последствия различаются по `QuestCompleted` конкретной branch quest.

Каэльская ветка и предъявление ложных доказательств переводят main quest в `Failed` без награды.

---

## 6. Главный квест `q_001_ash_under_glass`

### Общие поля

```text
questId       = q_001_ash_under_glass
discoverable  = true
oneShot       = true
prerequisites = [] в текущем draft
faction       = GuildOfThoughts
```

`questOfferRefs` NPC Лиры содержит main quest. `questTurnInRefs` NPC Лиры содержит main quest.

### Stage 0 — `st_q001_follow_the_note`

**Описание:** Найти место, указанное в записке Лиры.

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001_reach_old_dock` | `ReachLocation` | `zone_q001_old_dock`, `WorldScene_0_0`, radius TBD | да |

**onEnterActions:**

1. `GiveItem(item_q001_sealed_note, 1)`.

**Переход:** `st_q001_collect_first_evidence`.

### Stage 1 — `st_q001_collect_first_evidence`

**Описание:** Собрать первые независимые следы экспедиции и поговорить с Брамом.

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001_talk_bram` | `TalkToNpc` | `npc_bram_01` | да |
| `obj_q001_fragment_dock` | `HaveItem` | `item_q001_fragment_dock`, qty 1 | да |
| `obj_q001_fragment_archive` | `HaveItem` | `item_q001_fragment_archive`, qty 1 | да |

**Сюжетный смысл:** Брам не даёт готовый ответ. Он сообщает, что один из контейнеров был вскрыт изнутри, а не снаружи.

**Переход:** `st_q001_sela_testimony`.

### Stage 2 — `st_q001_sela_testimony`

**Описание:** Найти Селу в соляном архиве и восстановить пропущенный кусок протокола.

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001_reach_salt_archive` | `ReachLocation` | `zone_q001_salt_archive`, radius TBD | да |
| `obj_q001_talk_sela` | `TalkToNpc` | `npc_sela_01` | да |
| `obj_q001_fragment_sela` | `HaveItem` | `item_q001_fragment_sela`, qty 1 | да |

**Диалог Селы:**

- при обычном разговоре она признаёт, что была оператором хранилища;
- при наличии всех предыдущих фрагментов выдаёт `item_q001_fragment_sela`;
- после этого выдаёт `item_q001_vault_key` через `GiveItem`;
- не раскрывает финальную правду до осмотра стеклянной шахты.

**Переход:** `st_q001_resonance_check`.

### Stage 3 — `st_q001_resonance_check`

**Описание:** Проверить, связан ли сигнал со стеклянной шахтой, а не с военным маяком.

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001_have_vault_key` | `HaveItem` | `item_q001_vault_key`, qty 1 | да |
| `obj_q001_reach_glass_well` | `ReachLocation` | `zone_q001_glass_well`, radius TBD | да |
| `obj_q001_talk_veska` | `TalkToNpc` | `npc_veska_01` | да |

**Диалог Вески:**

- она подтверждает, что сигнал имеет аварийную частоту;
- предупреждает: данные нельзя передавать Ноллу до проверки чёрного ящика;
- открывает stage выбора, но сама не принимает решение за игрока.

**Переход:** `st_q001_choose_investigation_route`.

### Stage 4 — `st_q001_choose_investigation_route`

**Описание:** Решить, кому доверить продолжение расследования.

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001_talk_lyra_choice` | `TalkToNpc` | `npc_lyra_01` | да |

В диалоге Лиры игрок получает только контекст, а не все ветки одной кнопкой. После разговора main quest переходит в stage ожидания ветки.

**Переход:** `st_q001_wait_branch_resolution`.

### Stage 5 — `st_q001_wait_branch_resolution`

**Описание:** Завершить выбранный маршрут расследования.

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001_branch_resolved` | `EventDriven` | `evt_q001_branch_resolved` | да |

Возможные источники event:

- успешный финал `q_001a_signal_reconstruction`;
- успешный финал `q_001b_blackbox_salvage`.

Ветка `q_001c_silent_exchange` event не испускает и переводит main quest в `Failed`.

**Переход:** `st_q001_final_report`.

### Stage 6 — `st_q001_final_report`

**Описание:** Вернуться к Лире и оформить итог расследования.

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001_final_talk_lyra` | `TalkToNpc` | `npc_lyra_01` | да |

Финальный turn-in выполняется только через правильную реплику Лиры и action `CompleteObjective` с `toNpcId=npc_lyra_01`.

#### Истинная развязка — после `q_001a_signal_reconstruction`

Conditions:

- `QuestStageEquals(q_001_ash_under_glass, st_q001_final_report)`;
- `QuestCompleted(q_001a_signal_reconstruction)`.

Actions в порядке:

1. `AddReputation(GuildOfThoughts, +20)`;
2. `AddNpcAttitude(npc_lyra_01, +15)`;
3. `CompleteObjective`;
4. `EndConversation`.

Текстовая идея: Лира признаёт, что архив не скрывал оружие. Он автоматически запечатал заражённый груз и годами повторял аварийный сигнал.

#### Частичная развязка — после `q_001b_blackbox_salvage`

Conditions:

- `QuestStageEquals(q_001_ash_under_glass, st_q001_final_report)`;
- `QuestCompleted(q_001b_blackbox_salvage)`.

Actions в порядке:

1. `AddReputation(GuildOfThoughts, -10)`;
2. `AddReputation(FreeTraders, +15)`;
3. `CompleteObjective`;
4. `EndConversation`.

Текстовая идея: игрок добыл рабочее доказательство, но часть данных прошла через брокера и была скомпрометирована. Лира принимает результат, но не считает дело полностью закрытым.

### Main Quest Rewards

Выдаётся ровно один раз при успешном turn-in у Лиры:

- `credits = 800`;
- `item_q001_archive_shard ×1`;
- дополнительных предметов из `onCompleteActions` нет.

---

## 7. Ветка A — `q_001a_signal_reconstruction`

### Назначение

Научный маршрут: доказать, что сигнал является аварийным протоколом, а не военным вызовом.

```text
questId      = q_001a_signal_reconstruction
discoverable = false
oneShot      = true
offer NPC    = npc_veska_01
turn-in NPC  = npc_veska_01
```

### Offer conditions

В диалоге Вески доступны отдельные edges:

- `QuestStageEquals(q_001_ash_under_glass, st_q001_wait_branch_resolution)`;
- `ReputationAtLeast(GuildOfThoughts, 10)` **или отдельный edge** с `NpcAttitudeAtLeast(npc_lyra_01, 5)`.

OR не кодировать в одном массиве conditions: использовать два самостоятельных edges.

### Stage A0 — `st_q001a_lens_calibration`

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001a_talk_veska` | `TalkToNpc` | `npc_veska_01` | да |
| `obj_q001a_have_lens` | `HaveItem` | `item_q001_resonance_lens`, qty 1 | да |

После принятия ветки Веска выдаёт линзу через `GiveItem`.

**Переход:** `st_q001a_decode_lighthouse`.

### Stage A1 — `st_q001a_decode_lighthouse`

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001a_reach_lighthouse` | `ReachLocation` | `zone_q001_null_lighthouse`, radius TBD | да |
| `obj_q001a_have_lens` | `HaveItem` | `item_q001_resonance_lens`, qty 1 | да |
| `obj_q001a_talk_sela` | `TalkToNpc` | `npc_sela_01` | да |

Села подтверждает, что частота совпадает с протоколом карантинного хранилища.

**Переход:** `st_q001a_report_veska`.

### Stage A2 — `st_q001a_report_veska`

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001a_talk_veska_final` | `TalkToNpc` | `npc_veska_01` | да |

Финальная реплика Вески доступна только после подтверждения сигнала. На turn-in:

- `GiveItem(item_q001_truth_packet, 1)`;
- `CompleteObjective` у Вески;
- финальный `onCompleteActions`:
  - `EmitEvent(evt_q001_branch_resolved)`;
  - `AddNpcAttitude(npc_veska_01, +10)`.

### Ошибка научной ветки: `fail_q001_false_seal`

Если игрок выбирает явную реплику «Предъявить найденную печать как ключ расшифровки» при наличии `item_q001_false_seal`:

1. `TakeItem(item_q001_false_seal, 1)`;
2. `AddReputation(GuildOfCreation, -15)`;
3. `FailQuest(q_001a_signal_reconstruction)`;
4. `FailQuest(q_001_ash_under_glass)`;
5. `EndConversation`.

Это не просто отсутствие предмета: игрок сознательно подменил доказательство. Main quest становится `Failed`, награды не выдаются.

### Rewards ветки A

- `credits = 200`;
- `reputation GuildOfCreation = +10`;
- `item_q001_truth_packet` выдаётся как промежуточное доказательство до перехода main quest, а не как финальная награда main quest.

---

## 8. Ветка B — `q_001b_blackbox_salvage`

### Назначение

Прагматичный маршрут: извлечь чёрный ящик и получить доказательство через Брама, не доверяя красивой теории.

```text
questId      = q_001b_blackbox_salvage
discoverable = false
oneShot      = true
offer NPC    = npc_bram_01
turn-in NPC  = npc_bram_01
```

### Stage B0 — `st_q001b_bram_method`

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001b_talk_bram` | `TalkToNpc` | `npc_bram_01` | да |

Брам объясняет, что чёрный ящик находится в карантинной бухте, но открывать его на месте опасно.

**Переход:** `st_q001b_recover_blackbox`.

### Stage B1 — `st_q001b_recover_blackbox`

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001b_reach_quarantine_cove` | `ReachLocation` | `zone_q001_quarantine_cove`, radius TBD | да |
| `obj_q001b_have_blackbox` | `HaveItem` | `item_q001_blackbox_core`, qty 1 | да |

**Переход:** `st_q001b_check_broker_story`.

### Stage B2 — `st_q001b_check_broker_story`

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001b_talk_noll` | `TalkToNpc` | `npc_noll_01` | да |

Нолл предлагает «упростить отчёт» и выдаёт `item_q001_false_manifest` только после отдельного выбора игрока.

Правильный ответ — не принять манифест как доказательство и вернуть чёрный ящик Браму.

**Переход:** `st_q001b_return_blackbox`.

### Stage B3 — `st_q001b_return_blackbox`

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001b_talk_bram_final` | `TalkToNpc` | `npc_bram_01` | да |
| `obj_q001b_deliver_blackbox` | `DeliverItem` | `item_q001_blackbox_core`, qty 1, target `npc_bram_01` | да |

На правильном turn-in:

- предмет потребляется через `DeliverItem` при `TryTurnIn`;
- `GiveItem(item_q001_broker_packet, 1)`;
- `CompleteObjective` у Брама;
- `onCompleteActions`: `EmitEvent(evt_q001_branch_resolved)`.

### Ошибка salvage-ветки: `fail_q001_false_manifest`

Если игрок выбирает реплику «Передать Ноллу фальшивый манифест» при наличии `item_q001_false_manifest`:

1. `TakeItem(item_q001_false_manifest, 1)`;
2. `AddReputation(FreeTraders, -20)`;
3. `FailQuest(q_001b_blackbox_salvage)`;
4. `FailQuest(q_001_ash_under_glass)`;
5. `EndConversation`.

Смысл провала: игрок не просто ошибся в предмете, а помог Ноллу легализовать ложную версию.

### Rewards ветки B

- `credits = 350`;
- `reputation FreeTraders = +15`;
- `item_q001_broker_packet` — промежуточный результат ветки.

---

## 9. Ветка C — `q_001c_silent_exchange`

### Назначение

Скрытая ветка: Каэль предлагает не расследовать происхождение сигнала, а продать чёрный ящик тому, кто заплатит больше.

Это осознанная предательская развязка и должна завершать main quest провалом.

```text
questId      = q_001c_silent_exchange
discoverable = false
oneShot      = true
offer NPC    = npc_kael_01
turn-in NPC  = npc_kael_01
```

### Offer conditions

- `QuestStageEquals(q_001_ash_under_glass, st_q001_wait_branch_resolution)`;
- `NpcAttitudeAtLeast(npc_kael_01, 10)`.

Каэльская ветка не показывается до открытия stage выбора и не появляется в fallback-диалоге раньше времени.

### Stage C0 — `st_q001c_offer_silence`

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001c_talk_kael` | `TalkToNpc` | `npc_kael_01` | да |

Каэль обещает крупную выплату и утверждает, что «правда никому не нужна».

**Переход:** `st_q001c_take_blackbox`.

### Stage C1 — `st_q001c_take_blackbox`

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001c_reach_cache` | `ReachLocation` | `zone_q001_quarantine_cove`, radius TBD | да |
| `obj_q001c_have_blackbox` | `HaveItem` | `item_q001_blackbox_core`, qty 1 | да |

**Переход:** `st_q001c_exchange`.

### Stage C2 — `st_q001c_exchange`

| Objective ID | Тип | Параметры | Required |
|---|---|---|---|
| `obj_q001c_talk_kael_final` | `TalkToNpc` | `npc_kael_01` | да |
| `obj_q001c_deliver_blackbox` | `DeliverItem` | `item_q001_blackbox_core`, qty 1, target `npc_kael_01` | да |

На явном выборе «продать чёрный ящик»:

1. `TakeItem(item_q001_blackbox_core, 1)`;
2. `AddReputation(Pirates, +15)`;
3. `AddReputation(GuildOfThoughts, -25)`;
4. `FailQuest(q_001c_silent_exchange)`;
5. `FailQuest(q_001_ash_under_glass)`;
6. `EndConversation`.

`evt_q001_branch_resolved` не испускается. Main quest остаётся без доказательства и переводится в `Failed`.

### Rewards ветки C

Нет. Любая компенсация от Каэля — только сюжетный текст и изменение репутации, не `QuestReward`.

---

## 10. Нолл и ложная улика

Нолл нужен не как ещё один «квестодатель», а как проверка исследовательского поведения игрока.

### Его функция

1. Подтвердить, что вокруг архива существует рынок информации.
2. Выдать `item_q001_false_manifest` только при выборе соответствующей реплики.
3. Создать правдоподобную, но ложную версию: будто экспедиция перевозила военный груз.
4. Дать игроку возможность самостоятельно принять ложь или перепроверить её чёрным ящиком.

### Правило дизайна

Нолл не должен напрямую завершать main quest. Он только меняет набор доступных диалоговых edges, выдаёт ложный предмет или направляет к Браму/Каэлю.

---

## 11. Диалоговые ветвления

### `dlg_lyra_q001`

Основные node ID:

- `node_q001_lyra_greeting`;
- `node_q001_lyra_offer_main`;
- `node_q001_lyra_briefing`;
- `node_q001_lyra_choice_context`;
- `node_q001_lyra_final_truth`;
- `node_q001_lyra_final_compromised`;
- `node_q001_lyra_failed_fallback`.

Обязательные условия:

- предложение main quest: `QuestDiscovered` / корректный offer flow;
- briefing: `QuestStateEquals(Active)` + `QuestStageEquals` соответствующего stage;
- final truth: `QuestCompleted(q_001a_signal_reconstruction)`;
- final compromised: `QuestCompleted(q_001b_blackbox_salvage)`;
- после `Failed` финальная реплика не должна выдавать reward или снова предлагать main quest.

### `dlg_veska_q001`

- До main stage выбора — только ambient/исследовательские реплики.
- В `st_q001_wait_branch_resolution` — offer q001a при выполнении reputation/attitude gate.
- В q001a — calibration, lighthouse, report.
- При `HasItem(item_q001_false_seal)` — отдельная явно помеченная ошибка, а не автоматический выбор.

### `dlg_bram_q001`

- В `st_q001_wait_branch_resolution` — offer q001b.
- В q001b — инструкции по чёрному ящику и final turn-in.
- Не скрывать правильную реплику, если у игрока одновременно есть false manifest: игрок сам должен выбрать, что предъявить.

### `dlg_noll_q001`

- `QuestStageEquals(st_q001b_check_broker_story)` — проверка версии;
- edge «взять фальшивый манифест» выдаёт предмет;
- edge «отказаться от удобной версии» возвращает игрока к Браму;
- Нолл не должен иметь `CompleteObjective` для main quest.

### `dlg_kael_q001`

- До stage выбора — обычный нейтральный разговор;
- после gate — скрытое предложение q001c;
- финальный edge предательства вызывает `FailQuest`, а не `CompleteObjective`.

### `dlg_sela_q001`

- До получения первых фрагментов — неполное свидетельство;
- при наличии `item_q001_fragment_dock` и `item_q001_fragment_archive` — выдача `item_q001_fragment_sela` и `item_q001_vault_key`;
- после посещения `zone_q001_glass_well` — подтверждение карантинной природы сигнала.

---

## 12. Матрица исходов

| Исход | Условие | Main state | Rewards | Последствие |
|---|---|---|---|---|
| Истина восстановлена | завершена q001a, turn-in у Лиры | `TurnedIn` | main + A | сильный рост доверия Лиры/Гильдии Мыслей |
| Частичная правда | завершена q001b, turn-in у Лиры | `TurnedIn` | main + B | FreeTraders довольны, Лира считает данные скомпрометированными |
| Ложная печать | предъявлена `item_q001_false_seal` Веске | `Failed` | нет | игрок подменил доказательство |
| Ложный манифест | передан `item_q001_false_manifest` Ноллу | `Failed` | нет | игрок легализовал ложную версию |
| Тихий обмен | чёрный ящик передан Каэлю | `Failed` | нет | данные проданы, расследование потеряно |
| Неполное расследование | игрок не выбрал ветку | `Active` | нет | квест остаётся в ожидании event |

Failed-варианты должны быть видны в журнале как отдельные провальные результаты, но после `Failed` main quest не должен снова появляться среди доступных one-shot offers.

---

## 13. Регистрация NPC и квестов

### NPC `questOfferRefs`

| NPC | Offers |
|---|---|
| `npc_lyra_01` | `q_001_ash_under_glass` |
| `npc_veska_01` | `q_001a_signal_reconstruction` |
| `npc_bram_01` | `q_001b_blackbox_salvage` |
| `npc_kael_01` | `q_001c_silent_exchange` |
| `npc_noll_01` | `[]` |
| `npc_sela_01` | `[]` |

### NPC `questTurnInRefs`

| NPC | Turn-ins |
|---|---|
| `npc_lyra_01` | `q_001_ash_under_glass` |
| `npc_veska_01` | `q_001a_signal_reconstruction` |
| `npc_bram_01` | `q_001b_blackbox_salvage` |
| `npc_kael_01` | `q_001c_silent_exchange` |
| `npc_noll_01` | `[]` |
| `npc_sela_01` | `[]` |

Все QuestDefinition, NpcDefinition и DialogTree регистрируются additive-операцией в `QuestDatabase.asset`; существующие массивы не заменять целиком.

---

## 14. Localization draft

Префиксы ключей:

```text
quest.q001.name
quest.q001.description
quest.q001.stage.follow_note
quest.q001.stage.collect_first_evidence
quest.q001.stage.sela_testimony
quest.q001.stage.resonance_check
quest.q001.stage.choose_route
quest.q001.stage.wait_branch
quest.q001.stage.final_report

quest.q001a.name
quest.q001a.description
quest.q001b.name
quest.q001b.description
quest.q001c.name
quest.q001c.description

npc.lyra_01.name
npc.bram_01.name
npc.veska_01.name
npc.noll_01.name
npc.kael_01.name
npc.sela_01.name

item.q001.*
dialog.dlg_lyra_q001.*
dialog.dlg_bram_q001.*
dialog.dlg_veska_q001.*
dialog.dlg_noll_q001.*
dialog.dlg_kael_q001.*
dialog.dlg_sela_q001.*
```

Для каждого ключа подготовить RU и EN варианты. В assets сохранять fallback-текст вместе с ключом, но literal key не должен отображаться игроку.

Минимальный набор локализуемых строк:

- имя и описание всех четырёх quest assets;
- название и описание каждого stage/objective;
- имена шести NPC;
- текст всех dialog nodes;
- подписи branch choices;
- предупреждения перед ошибочными действиями;
- тексты провальных исходов;
- финальные реплики истинной и частичной развязок;
- подписи предметов-доказательств и зон.

---

## 15. Static checkpoint перед будущей реализацией

- [ ] Утвердить название, тон и финальную правду сюжета.
- [ ] Подтвердить, что `WorldScene_0_0` остаётся сценой маршрута.
- [ ] Расставить все зоны и заполнить реальные `targetPosition`/`targetRadius` для `ReachLocation`.
- [ ] Создать все ItemData и связать object references.
- [ ] Создать шесть NpcDefinition и шесть DialogTree.
- [ ] Проверить, что новые NPC используют канонический prefab без его изменения.
- [ ] Проверить уникальность всех quest/npc/tree/stage/objective/item/event IDs.
- [ ] Проверить, что ветки A/B испускают реальный `EmitEvent(evt_q001_branch_resolved)`.
- [ ] Проверить, что branch failure actions могут перевести оба quest instance в `Failed`.
- [ ] Проверить, что `DeliverItem` действительно потребляет blackbox только на правильном turn-in.
- [ ] Проверить, что `CompleteObjective` вызывается только у правильного turn-in NPC.
- [ ] Проверить, что main reward находится только в `q_001_ash_under_glass.rewards`.
- [ ] Проверить `GetUnreachableStages() == 0` для всех четырёх QuestDefinition.
- [ ] Проверить достижимость всех DialogTree nodes и отсутствие dangling edges.
- [ ] Проверить RU/EN localization keys.
- [ ] Выполнить compile-check.
- [ ] После реализации отдельно пройти Play Mode: правильные ветки, каждую ошибочную ветку, повторное открытие диалогов и отсутствие повторной награды.

---

## 16. Что намеренно не входит в этот draft

- мировые координаты и размеры радиусов;
- расстановка NPC и pickup объектов в сцене;
- создание `.asset` файлов;
- изменения `QuestDatabase.asset`;
- CSV-импорт;
- реализация новых runtime-механик;
- UI и редакторские изменения;
- озвучка, портреты и финальный литературный текст всех реплик.

Следующий этап после согласования этого документа: отдельно зафиксировать ID в data-schema, проверить реальные поля текущих `QuestDefinition`/`DialogTree`/`NpcDefinition`, затем подготовить asset-by-asset implementation plan.
