# Pitfalls & Open Questions — Turn-Based Battles

> **Pitfalls** — антипаттерны, которых избегаем. Часть — общие с `Battle/30_PITFALLS_AND_OPEN_QUESTIONS.md`, часть — TB-специфичные.
> **Open Questions** — вопросы для решения. После ответов → design-doc обновится.

---

## 1. Pitfalls (TB-специфичные)

### 1.1 Pitfall: server-authoritative RNG (повтор Battle/ §1.17)

**Сценарий:** `DamageCalculator.Calculate` использует `Random.Range` — но в TB два клиента могут видеть **разные** результаты (если server ленив).

**Решение:** **server-authoritative** — клиенты шлют `SubmitActionRpc`, сервер **сам** считает урон, шлёт `ActionResultTargetRpc` обоим. Клиенты **не** кидают кубики. Псевдоклиентское предсказание (показать roll до server-подтверждения) — Phase 3.

### 1.2 Pitfall: reentrancy в TurnBasedBattle — длинные расчёты

**Сценарий:** игрок делает цепочку действий: перемещение (1) + атака (2) + перемещение (1) = 4 сек. Как обработать «не хватает секунд»?

**Решение:** server проверяет `currentSecondsRemaining` перед каждым action. Если `cost > remaining` → отказ + `BattleErrorTargetRpc(clientId, "INSUFFICIENT_SECONDS")`. UI показывает сколько секунд осталось (`tb-ap-text: "1/3"`).

### 1.3 Pitfall: бой между двумя игроками — оба должны быть online

**Сценарий:** игрок A приглашает игрока B на PvP-дуэль. Если B offline — что делать?

**Решение:** дуэль только при `both online`. Если один выходит → grace 30 сек → cancel. Подробности в `30_SCENARIOS.md §3.5`.

### 1.4 Pitfall: TB-инстанс живёт в `WorldScene`, но игрок может зайти в корабль → disconnect

**Сценарий:** игрок начал TB-бой → вышел (Alt+F4) → корабль уже не в `WorldScene_X_Z`, а в `WorldScene_Y_Z` → бой «зависает».

**Решение:** TB-инстанс **не привязан к сцене** — он в `TurnBasedBattle.Instance` (POCO singleton, server-side). Игрок может disconnect/reconnect, бой продолжается. Если reconnect > 30 сек (grace) → battle cancel, no XP/loss.

### 1.5 Pitfall: NPC-AI «застрял» в углу сетки

**Сценарий:** NPC-AI хочет дойти до игрока, но путь заблокирован Wall. Без pathfinding — AI не знает, как обойти.

**Решение:** fallback через 5 сек (`20_TECHNICAL.md §6.1`) — auto-move к случайной клетке. Phase 3 — pathfinding (A*).

### 1.6 Pitfall: damage formula + skillMult stack — over-stacking

**Сценарий:** игрок учит все 8 Melee-навыков (basic_sword + heavy_swing + great_sword + parry + riposte + precision_strike + ...) → skillMult может достичь ×2+.

**Решение:** hard cap `skillMult <= 2.0` в `DamageCalculator`. Превышение → warning в логе, но cap применяется. Phase 3 — UI показывает «cap reached».

### 1.7 Pitfall: TB-окно блокирует другие UI

**Сценарий:** игрок в TB-бою → открывает CharacterWindow (P-key) → TB-overlay конфликтует.

**Решение:** TB-окно — modal (блокирует другие UIDocument). При открытии TB → `CharacterWindow.Hide()`. При закрытии TB → `CharacterWindow.Show()`. Подробности в `20_TECHNICAL.md §11`.

### 1.8 Pitfall: Boss-enкаунтер триггерится у всех в радиусе

**Сценарий:** игрок A подходит к боссу → авто-триггер запускает TB-бой для **всех** в радиусе 50м (включая B, C, D, которые не хотят).

**Решение:** trigger только на `localPlayer` (`TurnBasedBattleZone.OnInteract` через F-key). Авто-триггер — **только** если в зоне **1 игрок** (через `Physics.OverlapSphereNonAlloc`). Phase 3 — multi-boss (для группы).

### 1.9 Pitfall: фракционный ивент — 1 игрок AFK, 1 живёт

**Сценарий:** 4 игрока в кооп-данже. 3 убиты, 1 жив. Должен ли выживший «тянуть» бой?

**Решение:** **all dead = lose**. 1 живой продолжает бой. Остальные в `dead` state (вне сетки, observer). Если 1-й умирает → defeat.

### 1.10 Pitfall: PvP-дуэль — anti-ragequit exploit

**Сценарий:** игрок A в дуэли понимает, что проиграет → ragequit → grace 30 сек → отмена → A не теряет XP.

**Решение (Phase 2):** ragequit = auto-loss + **дополнительный штраф** (10% XP loss). MVP: просто отмена.

### 1.11 Pitfall: dungeon entrance — игрок спамит вход

**Сценарий:** игрок заходит в данж, выходит (respawn), снова заходит — repeat 10 раз/мин → серверная нагрузка.

**Решение:** rate limit `RequestStartPvEBattleRpc` (5 ops/sec, как у Skills). Дополнительно: после выхода из боя — 30 сек cooldown перед повторным входом в тот же данж.

### 1.12 Pitfall: TB-окно не открывается (Unity scene issue)

**Сценарий:** `TurnBasedBattleWindow` создан через `UIDocument` + `PanelSettings`. Если `PanelSettings` ещё не загружен → NRE.

**Решение:** lazy init в `TurnBasedBattleClientState.HandleBattleStarted` — если PanelSettings == null, найти через `Resources.Load<PanelSettings>("UI/CharacterPanelSettings")` (уже используется в CharacterWindow).

### 1.13 Pitfall: 4-8 игроков в фракционном ивенте — desync

**Сценарий:** игроки A, B, C, D в кооп-бою. A видит `TurnStartedDto`, B — нет (lag). B пытается действовать → "Not your turn".

**Решение:** server **не** обрабатывает actions, пока все 4 клиента не подтвердили `BattleStartedTargetRpc` (через NetworkVariable<bool> ready). После `ready = true` для всех → начинается ход первого игрока.

### 1.14 Pitfall: boss loot — drop table race condition

**Сценарий:** 4 игрока убили босса. Каждый пытается забрать `Item_LegendarySword` (1 шт). Конфликт.

**Решение:** drop = **один** предмет на **одного** игрока (по max damage dealt, top-1). Остальные получают consolation. Или drop = «все получают 1/4» (если ItemStackable). Designer решает в `BossConfig.loot`.

### 1.15 Pitfall: TB-окно с 8+ игроками — UI lag

**Сценарий:** 8 игроков в кооп-данже → `TurnBasedBattleWindow` показывает 8+ участников → рендеринг лагает.

**Решение:** ограничить UI — показывать только top-4 по урону, остальных — «ещё 4 игрока в бою» (compact view). Phase 3 — virtualized list.

### 1.16 Pitfall: combat-движок (real-time) появится позже — конфликт с TB

**Сценарий:** когда появится real-time combat-движок, боссы могут быть «убиваемы» в real-time (zerg). Ломает boss-enкаунтер.

**Решение:** boss-enкаунтер = **TB-only**, помечен в `BossConfig.tbOnly = true`. Real-time combat-движок не может «добить» босса из open world. Только через `TurnBasedBattleZone` (TB-бой).

### 1.17 Pitfall: PvP-дуэль invite spam

**Сценарий:** игрок A спамит приглашения на дуэль игроку B (100 в минуту).

**Решение:** rate limit `RequestStartPvPDuelRpc` (5/minute, hard). Cooldown на declined invite (5 мин).

### 1.18 Pitfall: TB-save при server crash

**Сценарий:** игрок в TB-бою → server crash → in-flight battle lost.

**Решение:** accept loss. Бой отменяется, no rewards/loss. In-flight state **не** персистится (см. `20_TECHNICAL.md §5.3`).

---

## 2. Open Questions (для пользователя)

> **Формат:** каждый раздел — одна область решений. После твоих ответов → design-doc обновится.
> **Мои рекомендации** отмечены `**РЕК:**`.

### 2.1 Какой минимальный сценарий в MVP?

**Текущая догадка:** **PvE-соло-данж** (самый простой, базовый use case).

| Вариант | Что |
|---|---|
| (a) **PvE-соло-данж** | 1 игрок vs 1-3 NPC, 10x10, базовые NPC + лут |
| (b) **PvP-дуэль 1v1** | 2 игрока, 6x6, без NPC |
| (c) **Оба** (MVP+1) | PvE-соло + PvP-дуэль, оба в одном релизе |
| (d) **Все 5 сценариев** | ambitious, ~80 ч кодинга |

**РЕК:** **(a) PvE-соло-данж** для MVP-1. (b), (c), (d) — следующие релизы.

### 2.2 Размер сетки по умолчанию

**Текущая догадка:** 10x10 для соло-данжа, 6x6 для дуэли, 12x12 для кооп/ивента.

| Вариант | Что |
|---|---|
| (a) **10/6/12** (текущая) | масштабируется по сценарию |
| (b) **Все 10x10** | упрощение, единый UI |
| (c) **8/6/10** | компактнее, но менее эпично |

**РЕК:** **(a)** — адаптивный по сценарию. Реализация через `BattleConfig.gridSize`.

### 2.3 Стоимость действий (секунды) — точная таблица

**Текущая догадка:** ERPR §1.1.3 — перемещение 1 сек, удар 1-3 сек (по типу), навык 1-3 сек.

| Действие | Секунды (ERPR) | Секунды (наш вариант) |
|---|---|---|
| Move | 1 | 1 (базово) |
| Run (2 клетки) | 2 | 2 |
| Attack melee | 1-3 | 1 (меч) / 2 (двуручник) / 3 (копьё reach) |
| Attack ranged | 1-3 | 2 (арбалет) / 3 (пневматика reload) |
| Skill (базовый) | 1-3 | 1 (passive) / 2 (active) |
| Skill (master) | 1-3 | 3 |
| Defend | 1 | 1 (стойка на ход) |
| Item use | — | 1 (зелье) / 0 (мелкое) |
| End turn | — | 0 (досрочно) |

**РЕК:** **(a) — адаптивный по типу действия**, как ERPR. Указывается в `SkillNodeConfig` (новое поле `int secondsCost`) и `WeaponItemData` (новое поле `attackSecondsCost`).

### 2.4 AI для NPC — насколько умный?

**Текущая догадка:** rule-based, 3 правила (flee / attack if in range / move closer).

| Вариант | Что |
|---|---|
| (a) **Rule-based, 3 правила** (текущая) | минимум, MVP |
| (b) **Rule-based + phase** (1-3 фазы по HP) | средне, MVP+1 |
| (c) **Rule-based + utility AI** (Phase 3) | score-based, гибче |
| (d) **ML** (reinforcement learning) | future, не MVP |

**РЕК:** **(a)** для MVP. (b) для боссов (опционально).

### 2.5 Death penalty — какая именно?

**Текущая догадка:** **20% XP loss** в текущем тире (ERPR §2.3).

| Вариант | Что |
|---|---|
| (a) **20% XP loss** (текущая) | стандарт ERPR |
| (b) **10% XP loss** | мягче, для casual-игроков |
| (c) **Permadeath** | жёстко, hardcore |
| (d) **0% loss + respawn** | без stakes |

**РЕК:** **(a) для PvE, (c) для PvP-дуэли (consent-based)**. Подробности в `30_SCENARIOS.md §3.4`.

### 2.6 PvP-дуэль — какая ставка?

**Текущая догадка:** credits (100) + honor (10). Без items.

| Вариант | Что |
|---|---|
| (a) **Только credits + honor** (текущая) | минимальный риск |
| (b) **Credits + honor + item-stake** (игроки ставят предметы) | gambling, требует escrow |
| (c) **Только honor** (без credits) | социальный, без экономического риска |

**РЕК:** **(a) для MVP**. (b) — Phase 2 (требует escrow-систему).

### 2.7 Boss-enкаунтер — cooldown

**Текущая догадка:** 168 часов (1 неделя) per player.

| Вариант | Что |
|---|---|
| (a) **168h (1 week)** (текущая) | стандарт, anti-farming |
| (b) **24h (1 day)** | чаще, но менее эксклюзивно |
| (c) **Per-guild cooldown** (1 guild = 1 kill/week) | социальный |
| (d) **Нет cooldown** (server-spawn NPC, не убивается навсегда) | менее интересно |

**РЕК:** **(a) 168h per player** для MVP. (c) — Phase 2 (guild-контент).

### 2.8 Фракционный ивент — расписание

**Текущая догадка:** 1 раз в неделю (Saturday 20:00 UTC).

| Вариант | Что |
|---|---|
| (a) **1 раз в неделю (фиксированный день/час)** (текущая) | предсказуемо |
| (b) **2 раза в неделю** (Wed + Sat) | чаще, но больше нагрузки на сервер |
| (c) **Daily** (каждый день) | много событий, но менее эксклюзивно |
| (d) **On-demand** (guild-leader запускает) | гибко, но требует UI |

**РЕК:** **(a) 1 раз в неделю** для MVP. (b) — Phase 2.

### 2.9 TB-окно — модальное или нет?

**Текущая догадка:** модальное (блокирует другие UIDocument).

| Вариант | Что |
|---|---|
| (a) **Модальное** (текущая) | полная иммерсия |
| (b) **Немодальное** (можно свернуть) | меньше иммерсии, но удобно для AFK |
| (c) **Полноэкранный mode** | максимум иммерсии, нет UI-конфликтов |

**РЕК:** **(a) модальное** — бой требует внимания. (c) — Phase 3 (если пользователи жалуются на UX).

### 2.10 NPC-спавн в TB-сцене — server-spawn или scene-placed?

**Текущая догадка:** server-spawn (NetworkObject spawn-on-demand).

| Вариант | Что |
|---|---|
| (a) **Server-spawn** (текущая) | гибко, per-instance |
| (b) **Scene-placed** в `WorldScene_X_Z` | экономит spawn-cycle, но не per-battle |

**РЕК:** **(a) server-spawn** — для разных DungeonConfig нужны разные NPC. Scene-placed не масштабируется.

### 2.11 TB-зона — auto-trigger или manual F-key?

**Текущая догадка:** manual F-key (через `IInteractable.OnInteract`).

| Вариант | Что |
|---|---|
| (a) **Manual F-key** (текущая) | opt-in, игрок сам решает |
| (b) **Auto-trigger** (радиус 5м → авто-бой) | immersive, но нет escape |
| (c) **Both** (F-key default, auto-trigger если opt-in setting) | гибко |

**РЕК:** **(a) manual F-key** — consistency с реальным миром (все взаимодействия через F).

### 2.12 Persist in-flight battle — да или нет?

**Текущая догадка:** **нет** (см. `20_TECHNICAL.md §5.3`).

| Вариант | Что |
|---|---|
| (a) **Нет persist in-flight** (текущая) | disconnect = escape, no XP/loss |
| (b) **Да, persist** (snapshot every 5 sec) | reconnect = continue battle, сложно |
| (c) **Save turn-by-turn, resume on same player** | mid-game save, но как? |

**РЕК:** **(a) нет persist** — проще, anti-cheat. (b) — Phase 3 если нужно.

### 2.13 Что если игрок в TB-зоне, но в корабле?

**Текущая догадка:** игрок **не может** войти в TB-зону из корабля. Только после выхода (как в NPC-зонах).

| Вариант | Что |
|---|---|
| (a) **Нельзя из корабля** (текущая) | consistency с NPC-зонами |
| (b) **Можно (но корабль должен стоять)** | гибко, но нужна проверка `_pilots.Count == 0` |
| (c) **Нельзя ни при каких обстоятельствах** | strict, проще |

**РЕК:** **(a) нельзя из корабля** — игрок должен выйти (EnterShip / ExitShip). Phase 2 можно (b).

### 2.14 NPC-уровни в данж — scaling по игроку или фиксированные?

**Текущая догадка:** фиксированные (DungeonConfig.npcSpawns[].level = const).

| Вариант | Что |
|---|---|
| (a) **Фиксированные** (текущая) | предсказуемо, designer-controlled |
| (b) **Scaling по игроку** (NPC = playerLevel ± 2) | комфортно, но anti-farming |
| (c) **Mix** (фиксированный + scaling для высоких рангов) | гибко |

**РЕК:** **(a) фиксированные** для MVP. (b) — Phase 2 (но требует баланса).

### 2.15 Top-3 в фракционном ивенте — как меряем?

**Текущая догадка:** total damage dealt (см. `30_SCENARIOS.md §5.4`).

| Вариант | Что |
|---|---|
| (a) **Total damage dealt** (текущая) | самый простой |
| (b) **Total damage + kills + survive time** | комплексный score |
| (c) **MVP-vote** (после боя игроки голосуют) | социальный, но abusable |
| (d) **Last-man-standing** (кто выжил) | радикально иной |

**РЕК:** **(a) total damage** для MVP. (b) — Phase 2.

### 2.16 Какие NPC для MVP-1 (PvE-соло-данж)?

**Текущая догадка:** 1 NPC-тип (Goblin_Worker).

| NPC | HP | Weapon | Aggression | Лут |
|---|---|---|---|---|
| `Goblin_Worker` | 20 | club (d4, base=2) | Normal | copper ×1, antigrav ×1 (10%) |
| `Goblin_Chief` | 50 | sword (d6, base=3) | Aggressive | steel ×1, antigrav ×2 (30%) |
| `Bandit_Scavenger` | 30 | crossbow (d8, base=4, critMod+5) | Defensive | bolt ×5, copper ×2 |
| `Temple_Guard` | 60 | spear (d8, base=4) | Defensive | steel ×2, antigrav ×1 |

**РЕК:** 2 NPC-типа для MVP-1 (Goblin_Worker + Goblin_Chief). Bandit + Temple — MVP-2.

### 2.17 Dungeon entrance visual — как?

**Текущая догадка:** GameObject `[DungeonEntrance_X]` в `WorldScene_X_Z` с mesh + light + interaction-radius.

| Вариант | Что |
|---|---|
| (a) **GameObject в open world** (текущая) | immersion, но требует 3D-контента |
| (b) **NPC-квестодатель** (говорит «иди в руины, убей гоблинов») | story-driven, через NPC-quest |
| (c) **Item-trigger** (нашёл карту в сундуке → открыт вход) | discovery-driven |

**РЕК:** **(a) GameObject** для MVP. (b)/(c) — Phase 2 (story integration).

### 2.18 TB-окно и CharacterWindow — как разрешать конфликт?

**Текущая догадка:** TB модальное → CharacterWindow скрывается.

| Вариант | Что |
|---|---|
| (a) **TB модальное, Char hidden** (текущая) | полная иммерсия |
| (b) **TB компактный, Char мини-окно** | можно смотреть статы во время TB |
| (c) **TB отдельный screen, Char отдельный screen** | мультиэкран |

**РЕК:** **(a) TB модальное** — focus на бой, нет distractions.

### 2.19 Loot drop — анонс или surprise?

**Текущая догадка:** анонс (`lootTable[].dropChance` виден в DungeonConfig UI).

| Вариант | Что |
|---|---|
| (a) **Анонс** (текущая) | transparency, anti-pay-to-win |
| (b) **Surprise** (drop не показан заранее) | RPG-атмосфера, но frustruюще |

**РЕК:** **(a) анонс** — игрок видит `Item_AntigravCrystal (10% drop)` в DungeonConfig UI.

### 2.20 TB-зона — persistent или temporary?

**Текущая догадка:** persistent (зона всегда в мире, дроп 1 раз в cooldown).

| Вариант | Что |
|---|---|
| (a) **Persistent** (текущая) | всегда доступна, cooldown per player |
| (b) **Temporary** (зона появляется после server-event) | dynamic world |
| (c) **Scheduled** (зона активна только в определённое время) | time-based content |

**РЕК:** **(a) persistent** для MVP. (b)/(c) — Phase 2 (dynamic world).

---

## 3. Промежуточный сводный список (TB)

| # | Область | Рекомендация | Альтернативы |
|---|---|---|---|
| 2.1 | MVP сценарий | **(a) PvE-соло-данж** | PvP-дуэль, оба, все 5 |
| 2.2 | Размер сетки | **(a) 10/6/12** | Все 10x10, 8/6/10 |
| 2.3 | Стоимость действий | **(a) адаптивный** | фиксированный |
| 2.4 | AI | **(a) rule-based 3 правила** | +phase, +utility, ML |
| 2.5 | Death penalty | **(a) 20% PvE, (c) permadeath PvP** | 10%, 0%, всегда permadeath |
| 2.6 | PvP ставка | **(a) credits + honor** | +item-stake, только honor |
| 2.7 | Boss cooldown | **(a) 168h/player** | 24h, per-guild, none |
| 2.8 | Ивент расписание | **(a) 1 раз/неделю** | 2/нед, daily, on-demand |
| 2.9 | TB-окно | **(a) модальное** | немодальное, полноэкранный |
| 2.10 | NPC-спавн | **(a) server-spawn** | scene-placed |
| 2.11 | TB-триггер | **(a) manual F-key** | auto-trigger, both |
| 2.12 | Persist in-flight | **(a) нет** | snapshot, mid-game save |
| 2.13 | TB из корабля | **(a) нельзя** | можно (b) |
| 2.14 | NPC scaling | **(a) фиксированные** | scaling по игроку, mix |
| 2.15 | Top-3 score | **(a) total damage** | комплексный, vote, last-man |
| 2.16 | NPC для MVP-1 | **Goblin_Worker + Chief** | 1 тип, 4 типа |
| 2.17 | TB-зона visual | **(a) GameObject** | NPC, item-trigger |
| 2.18 | TB/Char конфликт | **(a) TB модальное** | компактный, отдельный screen |
| 2.19 | Loot drop | **(a) анонс** | surprise |
| 2.20 | TB-зона persistent | **(a) persistent** | temporary, scheduled |

---

## 4. Что НЕ обсуждаем (вне scope)

- ❌ Real-time combat-движок (отдельная подсистема, T-CB10, ~30-40 ч)
- ❌ NPC-AI для open world (отдельная подсистема)
- ❌ Boss-механики (фазы, summons, AoE) — Phase 2
- ❌ Multiplayer TB (4-8 игроков в реальном времени с задержками) — Phase 3
- ❌ Pathfinding (A*) — Phase 3
- ❌ ML AI — future
- ❌ Voice-chat — отдельный сервис
- ❌ Replay-система — Phase 3
- ❌ Анимации, sound — 3D/audio отделы
- ❌ Магия — lore
- ❌ Open-world TB — только в спец. зонах
