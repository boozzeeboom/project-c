# Ретроспектива — 13.08.2026 – 17.08.2026

**Период:** 13.08.2026 10:02 → 17.08.2026 00:23 (5 дней)
**Диапазон коммитов:** `68cfd6fb` ("docs: old roadmap update") → `7d7237ff` (T-CORE Buiild errors) — **91 коммит**
**Каталог:** `docs/dev/` (по запросу пользователя)

---

## Резюмирующее саммари (TL;DR)

Неделя «аудита и починки»: главный результат — **глубокий аудит квест/NPC/диалоговой подсистемы (T-QAUDIT) и закрытие всех 8 критических server-client дефектов** (T-QC1..C8 + T-QS1..S11), найденных в нём. До аудита ни один квест нельзя было ни получить, ни корректно пройти; после — работает полный цикл onboarding-квеста (T-Q22, T-QST01/02, T-QST).

Второй по весу блок — **кастомизация персонажа (T-CUS-03, 10 коммитов)**: смена М/Ж модели тела переписана с mesh-swap на whole-model swap (модель целиком + avatar), включая race-фиксы в SkillAnimationPlayer/SkillInputService.

Третий — **MainMenu (T-UI03..08)**: версия в инспекторе, выбор языка, кнопки-ссылки, удалённый changelog (docs/changelogs.md), popup «solodev».

Побочно: persistence позиции (T-PERSIST01/02), ключи кораблей (T-KEY01, новый предмет `Key_heavyII_ship`), persistence экипировки (T-EQP01), spring-arm камера (T-CAM16), персонализация edge detection (T-CORE12), ресерч микротряски закрыт (T-JITTER12..14, первопричина не найдена), два ресерча-документа (T-CARRY01, T-NS-RESEARCH), фикс build errors (T-CORE).

**Главные цифры:** 91 коммит, 131 файл, +54 091 / −2 048 строк (из них +45 317 — закоммиченная `WorldScene_0_0.unity`); чистый код: C# +3 014 (64 файла), Markdown +3 927 (27 файлов), Shader +143, UXML/USS +321. 9 новых `.cs`, 9 удалённых `.cs` (мёртвая триггерная система + пробы/временные).

**Главный урок недели:** комментарии-враньё в `QuestWorld.TryTurnIn` («он сам проверит objectives») + отсутствие проверки целей = эксплойт «сдать любой квест одним RPC». Аудит (T-QAUDIT) показал, что 3 из 8 критических дефектов — именно эксплойты уровня «одним RPC». Фикс занял проверку `AreAllRequiredComplete` в `TryTurnIn` (T-QC1), но сам аудит — это отдельный коммит-документ, который окупился: все 8 дефектов закрыты в один день (14.08).

---

## Метрики

| Метрика | Значение |
|---------|----------|
| Всего коммитов | 91 |
| Дней работы | 5 (13.08 – 17.08) |
| Коммитов/день (среднее) | 18 |
| Файлов изменено | 131 |
| Строк добавлено / удалено | +54 091 / −2 048 |
| C# строк (добавлено) | +3 014 (64 файла) |
| Markdown строк | +3 927 (27 файлов) |
| Shader / UXML+USS | +143 / +321 |
| Сцены (закоммичено) | `WorldScene_0_0.unity` +45 317 |
| Новых `.cs`-файлов | 9 |
| Удалённых `.cs`-файлов | 9 (Triggers ×3, пробы ×2, temp ×4) |
| Самое итеративное направление | Quest-фиксы (~38 коммитов) и T-CUS-03 (10) |

### Коммиты по дням

| Дата | Коммитов | Основная работа |
|------|----------|-----------------|
| 13.08 | 19 | T-JITTER12..14 (ресерч №2 закрыт), T-NS-RESEARCH, T-UI05/06, global roadmap, T-CARRY01, **T-QAUDIT** (аудит) |
| 14.08 | 44 | **T-QC1..C8 + T-QS1..S11** (все критические фиксы), T-CUS-03 (whole-model swap), T-UI03 |
| 15.08 | 4 | T-EQP01 (persistence экипировки), T-CAM16 (spring arm), T-CORE12 (edge detection) |
| 16.08 | 23 | T-PERSIST01/02 (позиция), T-KEY01 (ключи кораблей), T-Q22/T-QST01/02 (onboarding), T-UI04/07, world assets |
| 17.08 | 1 | T-CORE: build errors (перенос _Editor_Temp, NetworkPlayer.OnDestroy) |

---

## Детальный отчёт по направлениям

### 1. Quest/NPC/Dialog subsystem — аудит + критические фиксы (~38 коммитов)

**T-QAUDIT** (`docs/NPC_quests/DEEP_AUDIT_2026-08-13.md`): полный прогон runtime-ядра (QuestServer 1677 строк, QuestWorld 1542 строки, все модели, Data-ассеты, call-graph). Вердикт: каркас качественный, но **8 критических дефектов server-client логики**, минимум 3 — эксплойты уровня «одним RPC сдать любой квест». Контент сломан на уровне данных.

Закрытые дефекты (все 14.08, каждый fix + docs-коммит в `docs/NPC_quests/ITERATIONS.md`):

| Тикет | Дефект | Фикс |
|-------|--------|------|
| T-QC1 | C1: TryTurnIn завершает квест без проверки objectives (эксплойт) | проверка `AreAllRequiredComplete` перед завершением |
| T-QC2 | C2: двойная выдача наград (onCompleteActions + turn-in) | единая точка выдачи в TryTurnIn |
| T-QC3 | C3: обход валидации NPC (пустой toNpcId) | проброс npcId в TryTurnIn + отказ пустому |
| T-QC4 | C4: отсутствие server-side distance check для talk-to-NPC | server-side проверка дистанции |
| T-QC5 | C5: OfferQuest не пушит snapshot при успехе | Discovered = успех (snapshot push + success=true) |
| T-QC6 | C6: недостающие DialogueCondition | реализованы недостающие условия диалогов |
| T-QC8 | C8: EmitEvent/FailQuest/DiscoverQuest не реализованы | реализованы (EmitEvent вообще не имел case в switch) |
| T-QC7 | мёртвая триггерная система | удалены `QuestTriggerService`, `IQuestTrigger`, `ConcreteTriggers` |
| T-QS1 | GiveItem/ApplyQuestRewards игнорировали количество | учёт количества |
| T-QS2 | мёртвые Refresh-RPC (RequestRefresh*) | удалены |
| T-QS3 | DeliverItem не изымал предметы | изъятие при turn-in |
| T-QS5 | minReputation + discoverable не задействованы | задействованы |
| T-QS6 | attitude-snapshot откуда-то | из questDatabase.npcs |
| T-QS10 | speakerDisplayName отсутствует в DialogStepDto | добавлен (фикс «toast показывает ID») |
| T-QS11 | NpcController trigger срабатывал на всех клиентов | фильтр IsLocalPlayer |

### 2. Onboarding-квест и гайдлайны (T-Q22, T-QST01/02, T-QST)

- **T-Q22** (16.08): исправлена архитектура TalkToNpc (onboarding-квест) — 3 итерации, фиксация в `docs/NPC_quests/T-Q22_TalkToNpc_event_fix.md`
- **T-QST01**: первый квест-гайд Onboarding alfa; новый контент: `onboarding_alfa.asset`, `OnboardingAlfaDialog.asset`, NPC `OnboardingAlfa.asset`
- **T-QST02**: исправлен возврат к Mira через TalkToNpc
- **T-QST**: универсальный гайдлайн квестов (`docs/NPC_quests/00_UNIVERSAL_QUEST_GUIDE.md`)

### 3. Character Customisation (T-CUS-03, 10 коммитов)

- Рефакторинг смены модели тела: **mesh-swap → whole-model swap** (модель целиком + avatar, M/Ж)
- Race-фиксы: active-only поиск Animator в `SkillAnimationPlayer` (respawn race), перерезолв `_animator` в `SkillInputService` после swap
- Документация: `06_RIG_SWAP_REFACTOR_PLAN.md`, `07_RIG_SWAP_IMPLEMENTATION.md`, `08_WHOLE_MODEL_SWAP.md`, ITERATIONS/CHANGELOG в `docs/Character/Customisation/`

### 4. MainMenu UI (T-UI03..08)

- **T-UI03**: версия в главном меню вынесена в поле инспектора
- **T-UI04**: вёрстка панели настроек (фон, border, скрытие заголовков) + debug-очистка persistence
- **T-UI05**: выбор языка в правом верхнем углу MainMenu
- **T-UI06**: кнопки-ссылки в левом нижнем углу
- **T-UI07**: удалённый changelog в главном меню — `docs/changelogs.md` + 404-фикс загрузки (6 коммитов, итерации зафиксированы)
- **T-UI08**: popup-уведомление «solodev»
- Локализация: обновлены `UI_Table_*` (9 языков) и `Dialogue_Table_*` (ru/en + Shared Data)

### 5. Persistence и предметы (T-PERSIST01/02, T-KEY01, T-EQP01)

- **T-PERSIST01**: сохранение позиции при выходе в меню
- **T-PERSIST02**: порядок восстановления позиции игрока (PlayerPositionServer/ShipPositionServer)
- **T-KEY01**: выдача ключей кораблей + runtime-регистрация; новый предмет `Key_heavyII_ship.asset`
- **T-EQP01**: persistence экипировки + removal debug seed

### 6. Камера и рендер (T-CAM16, T-CORE12)

- **T-CAM16**: стабилизация spring arm камеры (`SpringArmCamera.cs`)
- **T-CORE12**: персонализация edge detection — новый `EdgeDetectionTarget.cs` + `EdgeDetectionMask.shader` (маска по таргетам)

### 7. Ресерчи (T-JITTER12..14, T-CARRY01, T-NS-RESEARCH)

- **T-JITTER12..14** (микротряска костей): измерительная изоляция слоя шума, пробы `BoneJitterRuntimeProbe`/`JitterClipProbe`, фикс порога FloatingOrigin 100км→3км **откатнут** (revert) — ресерч №2 закрыт, **первопричина не найдена**. Документ: `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER.md`
- **T-CARRY01**: ресерч + дизайн переносимых физических объектов (`docs/world/placeble objects/00_DESIGN_CarryableObjects.md`, 426 строк)
- **T-NS-RESEARCH**: «корабли тупят в доках» (`docs/NPC_others_peacfull/npc_ship/11_DOCK_NAV_RESEARCH.md`)

### 8. Инфраструктура и build (T-CORE, misc)

- **T-CORE** (17.08): build errors — перенос `_Editor_Temp` (17 файлов) в `Assets/_Project/Editor/_Editor_Temp/` (GUID сохранены), `CloudNoiseBaker.cs` → `Editor/`, `NetworkPlayer.OnDestroy` → `public override` + `base.OnDestroy()`
- `WorldScene_0_0.unity` закоммичена в git (45 317 строк)
- `ItemRegistry.asset` → `Resources/Items/Data/`
- Packages: **+`com.unity.feature.characters-animation` 1.0.0**, удалены `com.unity.cinemachine` (явно) и `com.unity.timeline` 1.8.12 из manifest
- Удалены: `ShipPositions.json`, `_probe_bones.cs`, `_test_play.cs`, `update-graph.sh`
- Добавлены: `docs/dev/global roadmap/GLOBAL_ROADMAP.md` (+EN), `docs/dev/TESTS/first-auto-quest/README.md`, `docs/iterations.md`

---

## Что получилось / что открыто

**Получилось:**
- Quest/NPC/Dialog подсистема приведена в рабочее состояние: 8/8 критических дефектов закрыты, мёртвый код вычищен (Triggers, Refresh-RPC), полный цикл onboarding-квеста (получение → TalkToNpc → сдача → награды)
- Кастомизация: whole-model swap M/Ж стабилен, race-условия в skills закрыты
- MainMenu: 5 фич (версия, настройки, язык, ссылки, changelog, solodev-popup)
- Persistence: позиция + экипировка переживают выход в меню
- Два новых ресерча-документа (carryable objects, dock nav) + универсальный квест-гайдлайн

**Открыто / не закрыто:**
- **Микротряска костей** — первопричина не найдена (revert фикса FloatingOrigin, ресерч №2 закрыт без результата); пробы `BoneJitterRuntimeProbe`/`JitterClipProbe` остаются в коде для диагностики
- **NPC в доках** — ресерч зафиксирован, фикса нет (T-NS-RESEARCH — только документация)
- FactionDefinition по-прежнему 0 (`factions: []`) — из аудита 2026-07-13, не закрыто
- `docs/world/placeble objects/` — опечатка в имени папки («placeble»), дизайн-документ на 426 строк, реализации нет

**Риски для следующей итерации:**
- Quest-контент: после аудита контент (2 NPC / 3 диалога / 1 квест + onboarding_alfa) минимален — следующий шаг по гайдлайну T-QST, контент, а не ядро
- Jitter: без первопричины тряска останется «жирующим» багом; пробы уже есть, нужен следующий измерительный проход
