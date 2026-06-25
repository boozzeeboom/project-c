# Turn-Based Battles — пошаговые бои как мини-игра

> **Подсистема:** Character Progression → Skill Tree → Combat branch → **Turn-Based Battles** (mini-game)
> **Статус:** 🟡 Проектирование (v0.1, 2026-06-25)
> **Базовый документ:** `docs/Character/Skills/Battle/ERPR_collaboration.md` (ERPR-пакет), `docs/Character/Skills/Battle/10_DESIGN.md §7` (damage-формула)
> **Scope сессии:** research + design-doc only. **Без кода.** Реализация — отдельные сессии по тикетам T-TB01..T-TB10 (см. `30_PITFALLS_AND_OPEN_QUESTIONS.md`).
> **Ключевая идея:** реализовать ERPR-ядро в полном объёме (сетка + 3 сек на ход + ГМ-эквивалент = сервер-ИИ) как **мини-игру** внутри MMO-сэндбокса, в **специальных локациях** (PvE-данж, PvP-дуэль, фракционные ивенты).

---

## TL;DR

**Что:** встроенный пошаговый бой на сетке (8x8 / 10x10) с 3 секундами на ход, формулой урона по ERPR (damage dice + crit + hit location + defense) и серверным ИИ для NPC-противников. Реализует ERPR-ядро **в полном объёме** (которое не вошло в real-time combat-движок, см. `Battle/01_ANALYSIS.md §3.10`).

**Где активируется:**
- **PvE-соло-данж** — игрок заходит в «вход в данж» (спец. зона на карте, см. `30_SCENARIOS.md §1`) → сервер создаёт TurnBasedBattle-инстанс → бой → награды/потери.
- **PvP-дуэль** — игрок приглашает другого через UI → оба принимают → TB-инстанс → победитель получает ставку (credits / honor), проигравший — permadeath или 20% XP loss.
- **Фракционные ивенты** — глобальный серверный тик запускает TB-арены на 4-8 игроков (антиграв-мина, защита базы) → топ-3 получают награды.
- **Boss-енкаунтеры** — некоторые NPC-боссы (например, лидер Гильдии) **только** через TB-бой (защита от zerg-стратегий в real-time).

**Что НЕ делаем:**
- ❌ Не заменяем real-time combat (GDD_00 пиллар 2: «Исследование над боем»).
- ❌ Не делаем пошаговость основной боевой системой (TB = **mini-game**, не replacement).
- ❌ Не вводим магию (lore запрет).
- ❌ Не делаем TB в открытом мире (только в спец. зонах).

**Связь с Battle/:**
- TB использует **те же навыки** (из `Battle/20_SKILL_TREES.md`) — proficiency, technique, damage dice/crit.
- TB использует **ту же damage-формулу** (ERPR `Battle/10_DESIGN.md §7`).
- TB использует **тот же CharacterWindow** для отображения навыков/брони (sub-tab «Навыки» уже работает).
- TB добавляет **новый UI** — `TurnBasedBattleWindow` (отдельный UIDocument).

**Ключевые отличия от real-time combat (когда появится):**
- **Real-time**: быстрые реакции, анимации, физика пуль, попадание по hit-box. HitLocation + crit **опциональны** (отключены в MVP).
- **Turn-based**: пошаговость, 3 сек на ход, **полная ERPR-формула** (damage dice + hit_location + crit + defense), инициатива по DEX.

**Структура подсистемы:**
```
TurnBasedBattle (POCO singleton, server)
├── TurnBasedBattleInstance (один бой) — сетка, участники, состояние хода
├── TurnBasedBattleServer (NetworkBehaviour, scene-placed) — RPC hub
├── TurnBasedBattleClientState (singleton) — OnBattleStarted, OnTurnStarted, OnActionResult
├── TurnBasedBattleWindow (UIDocument) — UI для игрока
├── TurnBasedAI (server-side) — простой ИИ для NPC-противников
├── DungeonConfig (SO) — конфиг соло-данжей (враги, лут, сложность)
└── DuelConfig (SO) — конфиг PvP-дуэлей
```

**Трудозатраты (оценка):** ~40-60 ч кодинга (3-4 сессии). Зависит от готовности T-CB01..T-CB09 (навыки).

---

## Карта документов

```
docs/Character/Skills/turn-based-battles/
├── 00_README.md                 ← этот файл (манифест + TL;DR)
├── 01_ANALYSIS.md               ← что есть / scope / готовые подсистемы
├── 10_DESIGN.md                 ← архитектура: сетка, инициатива, 3 сек, формула урона
├── 20_TECHNICAL.md              ← NGO RPC, server-authoritative, state machine
├── 30_SCENARIOS.md              ← PvE-данж, PvP-дуэль, фракционные ивенты, boss-enкаунтеры
├── 30_PITFALLS_AND_OPEN_QUESTIONS.md  ← антипаттерны + открытые решения
└── 40_REFERENCES.md             ← file:line индекс всего, на что опираемся
```

---

## Ключевые механики (краткий обзор)

| Механика | Где описано | Статус |
|---|---|---|
| **Поле боя — сетка квадратов** (10x10 / 8x8) | `10_DESIGN.md §2` | проектирование |
| **Движение по прямым** (без диагоналей) | `10_DESIGN.md §2.3` | проектирование |
| **3 секунды на ход** (боевые AP) | `10_DESIGN.md §3` | проектирование |
| **Инициатива по DEX** | `10_DESIGN.md §4` | проектирование |
| **Damage dice 1dN + base + STR** | `Battle/10_DESIGN.md §7` (ERPR-формула) | готова |
| **Hit location 1d4** (×0.5/1/2) | `Battle/10_DESIGN.md §7.2` | готова (ERPR) |
| **Crit 1d100 + critMod** | `Battle/10_DESIGN.md §7.2` | готова (ERPR) |
| **Defense (armorDefense × typeMultiplier)** | `Battle/10_DESIGN.md §7.2` | готова (ERPR) |
| **AI для NPC** (простой, rule-based) | `10_DESIGN.md §6` | проектирование |
| **PvE-данж** (вход → бой → награды) | `30_SCENARIOS.md §1` | проектирование |
| **PvP-дуэль 1v1** | `30_SCENARIOS.md §3` | проектирование |
| **Boss-enкаунтер** (TB-only) | `30_SCENARIOS.md §4` | проектирование |
| **Фракционные ивенты** (4-8 игроков) | `30_SCENARIOS.md §5` | проектирование |
| **Death / XP loss** | `10_DESIGN.md §7` | проектирование |
| **NGO RPC + state machine** | `20_TECHNICAL.md` | проектирование |
| **UI: TurnBasedBattleWindow** | `10_DESIGN.md §8` | проектирование |

---

## Сценарии использования (5 типов)

### Сценарий 1: «Соло-данж» (PvE)
1. Игрок подходит к «входу в руины» (спец. GameObject в сцене).
2. Нажимает F → UI показывает `DungeonConfig: rank 1, 2 врага, лут: медь + антиграв-кристалл`.
3. Подтверждает → сервер создаёт `TurnBasedBattleInstance` (8x8 сетка, 1 игрок + 2 NPC-гоблина).
4. Инициатива: игрок (DEX 10) vs Goblin1 (DEX 8) vs Goblin2 (DEX 6). Порядок: игрок, Goblin1, Goblin2.
5. Игрок ходит первым (3 сек): перемещение 1 клетка (1 сек) + атака мечом (2 сек) = 0 сек осталось.
6. Удар: roll d6=4, base=3, STR=10 → 17 base. locRoll=3 (Torso) ×1. Hit_crit=87+0=87 (no crit). skillMult=1.0. final=17. defense NPC=2. итог: 15 урона.
7. Goblin1: 20-15=5 HP. Ход Goblin1: перемещение + атака. AI rule: «if HP<10, flee».
8. Бой 3-5 раундов → игрок побеждает → лут добавляется в инвентарь → возврат в обычный мир.

### Сценарий 2: «PvP-дуэль 1v1»
1. Игрок A в социальном хабе нажимает «Вызвать на дуэль» → вводит имя игрока B.
2. Сервер шлёт `DuelInviteTargetRpc` игроку B.
3. Игрок B принимает → сервер создаёт `TurnBasedBattleInstance` (6x6, 1v1).
4. Инициатива: A (DEX 12) vs B (DEX 11). A ходит первым.
5. Бой идёт по 3-секундным ходам. UI: кнопки «Атаковать», «Передвинуться», «Защита», «Навык», «Конец хода».
6. Игрок A побеждает → получает credits + honor. Игрок B: permadeath или 20% XP loss (consent-based, см. `30_SCENARIOS.md §3.4`).

### Сценарий 3: «Boss-enкаунтер» (TB-only)
1. Игрок летит к пику, на котором находится босс Гильдии (например, «Главарь Пиратов»).
2. В радиусе 50м от босса → авто-триггер TB-сцены.
3. Бой: 1v1 (или 1v2 с приспешниками), сетка 10x10, инициатива по DEX.
4. Босс: HP 200, level 10, оснащён `Weapon_GreatSword_Antigrav` (d10, base=8, critMod=+10) + `Clothing_PlateArmor` (armor=20).
5. Бой сложный, требует mastery навыков (`melee_parry` для парирования, `melee_precision_strike` для hit location Head).
6. Победа → drops legendary loot + квест-флаг.

### Сценарий 4: «Фракционный ивент» (4-8 игроков)
1. Серверный тик запускает глобальный ивент «Оборона пика» (например, раз в неделю).
2. Все игроки в зоне получают приглашение.
3. 4-8 принимают → сервер создаёт `TurnBasedBattleInstance` (12x12, 4-8 игроков vs 5-10 NPC).
4. Цель: продержаться 10 раундов или уничтожить NPC.
5. Топ-3 по урону получают награды.

### Сценарий 5: «Подземелье (соло-данж, расширенный)»
1. Игрок заходит в подземелье (3-5 комнат, каждая с боем + лут).
2. Каждая комната — отдельный `TurnBasedBattleInstance`.
3. Между комнатами — короткий real-time сегмент (ловушки, поиск).
4. Финальная комната — босс (TB-only, сложный).

---

## Roadmap реализации (предложение)

| # | Тикет | Что | Зависимости | Сложность |
|---|---|---|---|---|
| T-TB01 | `TurnBasedBattle` (POCO singleton) | server-side state, dict of active battles | T-CB01..T-CB09 (навыки) | ~3 ч |
| T-TB02 | `TurnBasedBattleInstance` | один бой: сетка, участники, очередь ходов | T-TB01 | ~4 ч |
| T-TB03 | `TurnBasedBattleServer` (NetworkBehaviour) | RPC hub, scene-placed в BootstrapScene | T-TB01, T-TB02 | ~4 ч |
| T-TB04 | `TurnBasedBattleClientState` | singleton + events | T-TB03 | ~2 ч |
| T-TB05 | NGO RPC: `RequestStartBattleRpc`, `SubmitActionRpc`, `EndTurnRpc` | client→server | T-TB03 | ~3 ч |
| T-TB06 | TargetRPC: `BattleStartedTargetRpc`, `TurnStartedTargetRpc`, `ActionResultTargetRpc`, `BattleEndedTargetRpc` | server→client | T-TB05 | ~3 ч |
| T-TB07 | `DamageCalculator` (static class) | ERPR-формула, переиспользуется real-time combat | T-CB03 (WeaponItemData) | ~2 ч |
| T-TB08 | `TurnBasedBattleWindow` (UIDocument) | UI: сетка, кнопки действий, лог | T-TB04, T-TB06 | ~6 ч |
| T-TB09 | `TurnBasedAI` (rule-based) | простой ИИ для NPC: attack / move / flee | T-TB01 | ~4 ч |
| T-TB10 | `DungeonConfig` + `DuelConfig` SOs + 3 примера | контент | T-TB01..T-TB09 | ~4 ч |
| T-TB11 | `TurnBasedBattleZone` (GameObject в сцене) | триггер входа в PvE-данж | T-TB10 | ~2 ч |
| T-TB12 | PvP-duel flow (invite/accept/decline) | NetworkBehaviour + UI | T-TB05..T-TB06 | ~4 ч |
| T-TB13 | Boss-enкаунтер (TB-only) | босс-конфиг + триггер | T-TB10..T-TB12 | ~3 ч |
| T-TB14 | Persistence (TB-результаты, лут) | JsonCharacterDataRepository extension | T-P06 | ~2 ч |

**ИТОГО: ~46 ч кодинга** (3-4 сессии).

**Зависимость от T-CB01..T-CB09**: TB использует `WeaponItemData.damageDice/baseDamage/critModifier`, `ClothingItemData.armorDefense`, `SkillNodeConfig` (proficiency/technique). Без T-CB01..T-CB09 — TB бессмысленно (урон 0, proficiency fail).

**Зависимость от Combat-движка (real-time)**: НЕТ. TB = **отдельная подсистема**, может реализовываться параллельно.

---

## Связь с другими подсистемами

| Подсистема | Связь |
|---|---|
| `docs/Character/Skills/Battle/` | навыки (proficiency, technique), damage-формула, 5 дисциплин |
| `docs/Character/` (базовая v2) | CharacterWindow, SkillsServer, EquipmentServer, StatsServer |
| `docs/Character-menu/` (Inventory v2) | лут после TB, инвентарь для ресурсов |
| `docs/Crafting_system/` | рецепты гранат/мин (если ExplosivesRecipeUnlock интегрирован) |
| `docs/NPC_quests/` | квест-флаги после boss-enкаунтеров |
| `docs/Markets/` | rewards в credits, награда за дуэли |
| `docs/Ships/` | НЕ связано (TB = пехотный) |
| `docs/World/` | dungeon-локации (отдельный документ, не в нашем scope) |
| `docs/gdd/GDD_20_Progression_RPG.md` | НЕ связано (корабельный бой, не наш scope) |

---

## Следующий шаг

1. Прочитай `01_ANALYSIS.md` — scope, готовые подсистемы, что переиспользовать.
2. Прочитай `10_DESIGN.md` — архитектура: сетка, инициатива, 3 сек, damage-формула.
3. Прочитай `20_TECHNICAL.md` — NGO RPC, server-authoritative, state machine.
4. Прочитай `30_SCENARIOS.md` — PvE-данж, PvP-дуэль, boss-enкаунтеры, фракционные ивенты.
5. Прочитай `30_PITFALLS_AND_OPEN_QUESTIONS.md` — антипаттерны и список решений.
6. Прочитай `40_REFERENCES.md` — file:line индекс.

**Параллельно:** `docs/Character/Skills/Battle/` — навыки и ERPR-пакет. Без T-CB01..T-CB09 TB не запустится.
