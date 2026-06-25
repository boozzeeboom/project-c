# Analysis — что есть / scope / готовые подсистемы

> **Дата:** 2026-06-25
> **Метод:** read_file существующих .cs + grep по `Assets/` и `docs/`
> **Цель:** зафиксировать, что уже есть, чего не хватает, и что нужно создать для turn-based battles.

---

## 1. Что УЖЕ есть (на что опираемся)

### 1.1 Skill tree (T-P11..T-P13) — база для TB-навыков

`Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` (98 строк) + `SkillsServer.cs` (206 строк) + `SkillsWorld.cs` + `SkillsClientState.cs` (реализованы).

**Для TB нужно:**
- `WeaponProficiencyUnlock` effect — проверка в `TryEquip` (Combat-движок не существует, но TB-движок может читать).
- `WeaponTechniqueUnlock` effect — маркер для TB-движка (какие техники доступны).
- `ArmorProficiencyUnlock` effect — маркер для TB-движка (какие классы брони доступны).

**Вердикт:** навыки готовы (после T-CB01..T-CB09). TB использует `SkillsWorld.GetLearnedSkills(clientId)` для проверки.

### 1.2 Equipment (T-P07..T-P09) — экипировка для TB

`Assets/_Project/Scripts/Equipment/ClothingItemData.cs` + `EquipmentData.cs` + `EquipmentServer.cs` (реализованы).

**Для TB нужно:**
- `WeaponItemData` (после T-CB03) — `damageDice`, `baseDamage`, `critModifier`, `range` (ERPR-пакет).
- `ClothingItemData.armorDefense` (после T-CB06) — для defense-формулы.
- `EquipmentData` (13 слотов) — `WeaponMain`/`WeaponOff` уже есть.

**Вердикт:** после T-CB03 + T-CB06 — TB может читать `EquipmentWorld.GetEquipment(clientId)`.

### 1.3 Stats (T-P01..T-P06) — STR/DEX/INT для TB-формулы

`Assets/_Project/Scripts/Stats/StatsConfig.cs` (реализован) + `StatsWorld.cs` + `PlayerStats.cs`.

**Для TB нужно:**
- `PlayerStats.strength` → модификатор урона (ERPR: `baseAttack = roll + base + STR_mod`).
- `PlayerStats.dexterity` → инициатива (`turnOrder`).
- `PlayerStats.intelligence` → не используется в TB-формуле напрямую (но нужен для `RequiredIntelligenceTier` гейта навыков).

**Вердикт:** готов. TB читает `StatsWorld.GetOrCreateStats(clientId)`.

### 1.4 NetworkManager + NGO 2.x — для RPC

`Assets/_Project/Scripts/Core/NetworkManagerController.cs` (реализован) + `Unity.Netcode`.

**Для TB нужно:**
- `NetworkBehaviour` (T-TB03) — scene-placed в `BootstrapScene` рядом с другими серверами.
- `[Rpc(SendTo.Server)]` для client→server.
- `[Rpc(SendTo.Owner)]` или `[Rpc(SendTo.NotMe)]` для server→client.
- `NetworkVariable<T>` для replicated state (HP, turn, currentSeconds).

**Вердикт:** pattern уже отработан (см. `Battle/01_ANALYSIS.md §1.2-1.4` — EquipmentServer, SkillsServer, StatsServer). Копируем.

### 1.5 WorldEventBus — для publish/subscribe

`Assets/_Project/Core/WorldEventBus.cs` (82 строки, реализован).

**Для TB нужно:**
- `BattleStartedEvent` — опубликовать при старте TB.
- `BattleEndedEvent` — опубликовать при завершении (victory/defeat/escape).
- `TurnStartedEvent` — опубликовать в начале каждого хода.
- `ActionResultEvent` — опубликовать после каждого действия (атака, перемещение, навык).

**Вердикт:** добавляем 4 новых event-класса в `WorldEvent.cs`. Минимальные изменения.

### 1.6 CharacterWindow + UI — навыки/статы уже отображаются

`Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (3400 строк, реализован).

**Для TB нужно:**
- `TurnBasedBattleWindow` — **отдельный** UIDocument, **не** модифицирует CharacterWindow.
- Pattern: sub-sub-window, открывается из `BattleStartedTargetRpc`.

**Вердикт:** новый файл `Assets/_Project/Scripts/UI/Client/TurnBasedBattleWindow.cs` + UXML + USS. Pattern копируется из `CharacterWindow.cs` (но проще — ~600 строк вместо 3400).

### 1.7 Persistence (T-P06) — JsonCharacterDataRepository

`Assets/_Project/Scripts/Stats/JsonCharacterDataRepository.cs` (реализован).

**Для TB нужно:**
- Сохранять TB-результаты (победы, поражения, total_damage_dealt) для ачивок.
- Не сохранять in-flight battle state (battle прерывается при disconnect — игрок может вернуться в зону, но бой начинается заново).

**Вердикт:** расширение `JsonCharacterDataRepository` (T-TB14).

### 1.8 NPC-система (минимально) — для PvE-врагов

В проекте:
- `Assets/_Project/Quests/NpcController.cs` (130 строк) — `NpcId` getter, для квестов.
- `Assets/_Project/NPC_quests/` — квесты NPC, диалоги.
- **НЕТ**: NPC-AI (агрессия, патрулирование, боевое поведение в real-time).
- **НЕТ**: NPC-конфиг (HP, damage, weapons, faction).

**Для TB нужно:**
- `NpcCombatData` (struct) — HP, damage dice, weapon, armor (аналог PlayerStats, но для NPC).
- `NpcAiConfig` (SO) — rule-based AI (см. `10_DESIGN.md §6`).
- `SpawnNpcForBattle` — server-side spawn NPC в TB-сцене (если бой не в общей сцене).

**Вердикт:** **много нового**. Создаём с нуля. NPC в TB — это **отдельный scope**, не пересекается с NPC-quest (где NPC только диалоги).

---

## 2. Чего НЕТ (gaps)

| # | Gap | Что делаем |
|---|---|---|
| G1 | `TurnBasedBattle` (POCO singleton) | T-TB01 — server-side state, dict of active battles |
| G2 | `TurnBasedBattleInstance` (один бой) | T-TB02 — сетка, участники, очередь ходов, состояние |
| G3 | `TurnBasedBattleServer` (NetworkBehaviour) | T-TB03 — RPC hub, scene-placed в BootstrapScene |
| G4 | `TurnBasedBattleClientState` | T-TB04 — singleton + events |
| G5 | NGO RPC (`RequestStartBattleRpc` и т.д.) | T-TB05 |
| G6 | TargetRPC (`BattleStartedTargetRpc` и т.д.) | T-TB06 |
| G7 | `DamageCalculator` (static) | T-TB07 — переиспользуется Combat-движком (real-time) |
| G8 | `TurnBasedBattleWindow` (UIDocument) | T-TB08 — UI: сетка, кнопки, лог |
| G9 | `TurnBasedAI` (rule-based) | T-TB09 — простой ИИ для NPC |
| G10 | `DungeonConfig`, `DuelConfig` (SO) | T-TB10 — конфиги сценариев |
| G11 | `TurnBasedBattleZone` (GameObject) | T-TB11 — триггер входа в PvE-данж |
| G12 | PvP-duel flow | T-TB12 — invite/accept/decline |
| G13 | Boss-enкаунтер (TB-only) | T-TB13 — босс-конфиг + триггер |
| G14 | Persistence (TB-результаты) | T-TB14 — JsonCharacterDataRepository extension |
| G15 | NPC-конфиги (HP, оружие, AI) | в TB — минимальные (1-2 типа NPC для MVP) |
| G16 | `HitLocation` enum | T-CB10 (Combat) / T-TB10 — для hit_location множителя |
| G17 | `DamageDice` enum | T-CB03 — для ERPR-формулы |
| G18 | 4 новых event-класса в `WorldEvent.cs` | T-TB01 + T-TB03 |

**Вердикт:** **14 новых тикетов** + 3 зависимости от Combat-пакета (T-CB03, T-CB06, T-CB10). **~46 ч кодинга**.

---

## 3. Scope (что в этой подсистеме, что нет)

### 3.1 В scope

| Что | Где |
|---|---|
| **Поле боя — сетка квадратов/гексов** (10x10 / 8x8) | `10_DESIGN.md §2` |
| **Движение по прямым** (без диагоналей) | `10_DESIGN.md §2.3` |
| **3 секунды на ход** (боевые AP) | `10_DESIGN.md §3` |
| **Инициатива по DEX** | `10_DESIGN.md §4` |
| **Damage-формула ERPR** (damage dice + crit + hit location + defense) | `Battle/10_DESIGN.md §7` (готова), `10_DESIGN.md §5` (использование в TB) |
| **AI для NPC** (rule-based) | `10_DESIGN.md §6` |
| **PvE-данж** (вход → бой → награды) | `30_SCENARIOS.md §1` |
| **PvP-дуэль 1v1** | `30_SCENARIOS.md §3` |
| **Boss-enкаунтер** (TB-only) | `30_SCENARIOS.md §4` |
| **Фракционные ивенты** (4-8 игроков) | `30_SCENARIOS.md §5` |
| **Death / XP loss** | `10_DESIGN.md §7` |
| **NGO RPC + state machine** | `20_TECHNICAL.md` |
| **UI: TurnBasedBattleWindow** | `10_DESIGN.md §8` |
| **Persistence (TB-результаты)** | T-TB14 |

### 3.2 НЕ в scope (отдельные подсистемы)

| Что | Почему |
|---|---|
| **Real-time combat-движок** (hit, projectile) | отдельная подсистема, `Battle/` T-CB10, ~30-40 ч |
| **NPC-faction AI** (враждебные NPC в открытом мире) | отдельная подсистема, не в нашем scope |
| **PvP-фракции** (5 Гильдий, враждебные игроки в real-time) | будущее, не в нашем scope |
| **Сложный ИИ** (ML, pathfinding, tactical retreat) | rule-based в MVP, ML — Phase 3 |
| **Boss-механики** (фазы, special abilities, summons) | базовые в MVP (1 босс = много HP), сложные — Phase 3 |
| **Voice-chat в PvP-дуэли** | не MMO-sandbox core feature |
| **Replay-система** (запись боёв) | Phase 3, требует много места |
| **Анимации атаки/блока** | 3D-отдел, не в нашем scope |
| **Sound effects** (звуки ударов) | audio-отдел, не в нашем scope |

### 3.3 Конфликты с другими подсистемами

| Конфликт | Решение |
|---|---|
| **TB vs real-time combat** | TB = **mini-game** в спец. зонах. Real-time — в открытом мире. Не пересекаются. |
| **TB vs NPC-quest** | NPC в TB-сцене — отдельный от NPC-quest. NPC-quest остаётся для диалогов и триггеров. |
| **TB vs корабль** | Корабли не участвуют в TB (TB = пехотный). Игрок «выходит» из корабля перед TB. |
| **TB vs Market/Trade** | Торговля не работает внутри TB. Отдельный scope. |
| **TB vs Crafting** | Крафт не работает внутри TB. После TB → лут → можно крафтить. |

---

## 4. Конфликт с GDD 00 пиллар 2

> «Исследование над боем» — основной геймплей = исследование мира, не сражения. Бой — через стелс и избегание. (`docs/gdd/GDD_00_Overview.md §1.2`)

**TB-как-mini-game — компромисс:**
- Real-time combat (когда появится) — стелс/избегание в открытом мире.
- TB — **вынужденный** пошаговый бой в спец. зонах (PvE-данж, PvP-дуэль, boss-enкаунтер).
- Это **не противоречит** GDD 00, т.к. TB = **выбор** игрока (вход в данж, вызов на дуэль), а **не** enforced.

**Вердикт:** TB как **opt-in** mini-game — совместим с GDD 00.

---

## 5. Сводка рисков (TB-специфичные)

| # | Риск | Severity | Mitigation |
|---|---|---|---|
| R1 | TB-сервер десинхронизирован с клиентом (player делает 2 действия, сервер видит 1) | high | server-authoritative + `[Rpc(SendTo.Server)]` + валидация `currentSecondsRemaining` |
| R2 | AI-баг: NPC-враг «застревает» (не может дойти до игрока) | medium | rule-based AI + fallback «random move» |
| R3 | PvP-дуэль: один игрок disconnect → бой зависает | high | auto-cancel через 30 сек, без XP/loss |
| R4 | Сетка слишком большая → производительность | low | 8x8/10x10, не больше 16x16 в MVP |
| R5 | Hit location + crit → формула даёт экстремальный урон | medium | soft cap `skillMult <= 2.0` (см. `Battle/30_PITFALLS §1.11`) |
| R6 | NPC-спавн в TB-сцене: server-spawn или scene-placed? | medium | server-spawn (NetworkObject, scene-placed не подходит для per-instance) |
| R7 | TB-результаты не сохраняются при disconnect | high | periodic snapshot в `JsonCharacterDataRepository` |
| R8 | Босс-енкаунтер запускается у всех в радиусе | medium | trigger только на `localPlayer`, остальные видят визуально |
| R9 | Инициатива при равном DEX → non-deterministic | low | tie-break по `clientId` (детерминированно) или `Random.Range` (visual) |
| R10 | Damage формула выполняется на сервере — кубики не «честные» для игрока | low | server log показывает детали (roll/loc/crit), UI предсказывает client-side для UX |
| R11 | Маппинг `SkillNodeConfig` → TB-навык (какой навык какое действие даёт) | medium | `WeaponClass ↔ SkillNodeConfig` lookup, designer-конфигурируемый |
| R12 | Multiplayer TB (4-8 игроков) → action queue | high | последовательная обработка, server tick rate 1 Hz |
| R13 | TB-зона vs streaming world | medium | TB = «отдельная сцена» (scene-placed), загружается additive |

---

## 6. Что НЕ делаем в этой сессии (явные запреты)

- ❌ Не пишем код (research + design-doc only)
- ❌ Не модифицируем `docs/gdd/`
- ❌ Не модифицируем `docs/WORLD_LORE_BOOK.md`
- ❌ Не пишем .meta / .asmdef
- ❌ Не запускаем `run_tests` MCP
- ❌ Не делаем git commit / push
- ❌ Не проектируем real-time combat-движок (отдельная подсистема)
- ❌ Не проектируем NPC-AI для открытого мира
- ❌ Не вводим магию (lore запрет)
- ❌ Не делаем TB заменой real-time combat (TB = mini-game)
- ❌ Не делаем TB в открытом мире (только в спец. зонах)
