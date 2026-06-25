# Battle Skills — боевые навыки поверх существующей skill tree

> **Подсистема:** Character Progression → Skill Tree → Combat branch
> **Статус:** 🟡 Проектирование (v0.3, 2026-06-25) — **новый sequencing, реверс v0.2**
> **Базовый документ:** `docs/Character/06_SKILL_TREE.md` (T-P11..T-P14 уже реализовано)
> **Коллаборация с ERPR:** `ERPR_collaboration.md` (damage dice + crit + hit location, без магии, без сетки, без пошаговости)
> **Связанный документ (MVP):** `../real-time-combat/` — **Real-Time Combat Engine**, который переиспользует ERPR damage-формулу (см. `Battle/10_DESIGN.md §7`).
> **Scope сессии:** research + design-doc only. **Без кода.** Реализация навыков — T-CB01..T-CB09 (отложены в roadmap, после T-RTC01..T-RTC09).
> **Turn-based battles** (`turn-based-battles/`) — **PARKING** (отложен на неопределённый срок). ЗБТ может пересмотреть.

---

## TL;DR

Сегодня Combat-навыки в проекте — это 4 placeholder-ноды в `docs/Character/06_SKILL_TREE.md §1.3`:
`BasicStrike (+2 STR)`, `DodgeRoll (+3 DEX)`, `HeavySwing`, `PrecisionStrike`. Все четыре — обычные `SkillEffect.StatMod`, **не привязаны к оружию**. CHANGELOG 2026-06-17 фиксирует: *«Skills click handlers deferred до battle system»*. Combat-системы как таковой в коде нет: `WeaponItemData` не существует, `EquipSlot.WeaponMain/Off` объявлены, но реальное оружие не описано; `CombatWorld/CombatServer` отсутствуют.

**Решение сессии (v0.3, после ответов пользователя) — новый sequencing:**
- **Real-Time Combat Engine** (`../real-time-combat/`) = **MVP**. Движок делаем **сначала**, навыки подключаются позже («когда уже можно будет»).
- **Combat-навыки (T-CB01..T-CB09)** = **MVP+1**, после движка. Без блокировки движка.
- **Turn-based battles** = **PARKING** (отложен на неопределённый срок). ЗБТ пересмотрит.
- **PvP-aware** с самого начала (2.10) — duel-флоу (Phase 2).
- **Ship combat** = future, но движок **уже extensible** (anti-restrictive design, см. `../real-time-combat/10_DESIGN.md`).

**ERPR-пакет (variant B, принят):**
- **Damage dice 1dN** (d6/d8/d10/d12/d20) → 3 новых поля в `WeaponItemData` (`damageDice`, `baseDamage`, `critModifier`).
- **Crit 1d100 + crit_modifier** → нативная замена через `Random.Range(1, 101) + critModifier >= 100`.
- **Hit location 1d4** (Limbs/Torso/Head, ×0.5/1/2) → **только в TB** (parking). В real-time = `locMult = 1.0` (отключён, 2.17).
- **Защита от экипировки** → новое поле `armorDefense` в `ClothingItemData` (T-CB06).
- **Сила/Ловкость/Интеллект** (модификаторы) → уже 1:1 совпадают с `StatsConfig`.

**Что остаётся вне Combat-навыков (отдельные подсистемы):**
- ❌ **Real-time Combat Engine** — `../real-time-combat/` (новый каталог, MVP).
- ❌ **Turn-based** — `../turn-based-battles/` (PARKING, отложен).
- ❌ **NPC-AI** для open world — отдельная подсистема (вне scope).
- ❌ **Damage-system** как таковой — реализуется в `../real-time-combat/10_DESIGN.md §7` (ERPR-формула).
- ❌ **Ship combat** — Phase 3 (после ЗБТ), extensible hooks уже в движке.

**Задача сессии (актуализировано под v0.3):**

1. **Расширяем существующий `SkillNodeConfig` + `SkillEffect`** (T-P11) — не ломая 8 уже созданных .asset.
2. **Добавляем 3 поля в `WeaponItemData`** (T-CB03) — `damageDice`, `baseDamage`, `critModifier` (ERPR-пакет).
3. **Добавляем `armorDefense` в `ClothingItemData`** (T-CB06).
4. **Строим 5 боевых дисциплин** (Melee / Ranged / Explosives / Antigrav / Defense), каждая = отдельная подветка Combat. Навык разблокирует владение классом оружия/брони, технику или рецепт.
5. **Подключаем навыки** к Real-Time Combat Engine как opt-in слой (skillMult, critMod, и т.п.).

**НЕ делаем:**
- ❌ Не трогаем `GDD_20_Progression_RPG.md` (gdd/ read-only) — там корабельный бой, не пехотный. См. `01_ANALYSIS.md §3.2`.
- ❌ Не переписываем существующий `06_SKILL_TREE.md` — это **расширение**, additive-only.
- ❌ Не вводим `SkillPoint` за уровень персонажа (GDD 20 фича) — у нас геометрический `tier` (T-P01).
- ❌ Не делаем real-time FPS-shooter / melee-anim combat, damage-system как таковой в этой сессии — здесь **только навыки + их damage-параметры**. Combat-движок (real-time) — отдельная подсистема. Пошаговый бой — `turn-based-battles/`.
- ❌ Не пишем код, .meta, .asmdef.

---

## Карта документов

```
docs/Character/Skills/Battle/
├── 00_README.md                 ← этот файл (манифест + TL;DR)
├── 01_ANALYSIS.md               ← что есть / чего нет / расхождение с GDD 20
├── 02_LORE.md                   ← лор-факты: антигравий, мезий, оружие, броня
├── ERPR_collaboration.md        ← анализ ERPR-пакета, mapping, варианты A/B/C
├── 10_DESIGN.md                 ← архитектура combat-навыков + ERPR-формула §7
├── 20_SKILL_TREES.md            ← 5 веток (Melee/Ranged/Explosives/Antigrav/Defense), DAG, эффекты + damage dice/crit
├── 30_PITFALLS_AND_OPEN_QUESTIONS.md  ← антипаттерны + открытые решения (включая ERPR-вопросы)
└── 40_REFERENCES.md             ← file:line индекс + ссылка на ERPR_collaboration
```

**Смежно (отдельная подсистема):**
```
docs/Character/Skills/turn-based-battles/
├── 00_README.md                 ← обзор пошаговых мини-игр (PvE-данж, PvP-дуэль)
├── 01_ANALYSIS.md               ← scope, готовые подсистемы, что переиспользовать
├── 10_DESIGN.md                 ← поле, инициатива, 3 сек на ход, ERPR-формула в полном объёме
├── 20_TECHNICAL.md              ← NGO RPC, server-authoritative, state machine
├── 30_SCENARIOS.md              ← PvE-данж, PvP-дуэль, фракционные ивенты
├── 30_PITFALLS_AND_OPEN_QUESTIONS.md  ← антипаттерны
└── 40_REFERENCES.md             ← file:line индекс
```

---

## Что лежит на 5 дисциплинах

| Дисциплина | Оружие / средства | Статы-источники | Примеры навыков | Damage dice (типичные) |
|---|---|---|---|---|
| **Melee (холодное)** | мечи, копья, булавы, кинжалы, древковое; **антигравийные клинки** | STR (сила удара), DEX (уклонение, точность) | «Рубящий удар», «Подкат», «Парирование щитом», «Антигравийный клинок» | d6 (меч), d8 (копьё), d10 (двуручник) |
| **Ranged (стрелковое)** | арбалеты, пневматическое (сжатый воздух); **мезиевое** стрелковое | DEX (точность), INT (тактика) | «Прицельный выстрел», «Заряжание болта», «Мезиевый разряд» | d8 (арбалет), d10 (пневматика), d12 (мезиевое) |
| **Explosives (взрывчатка)** | гранаты, мины, заряды; антигравийные мины (манипуляция g) | INT (тактика), DEX (установка) | «Метание гранаты», «Установка мины», «Антигравийная мина-переворот» | damage dice = static (навык), не оружие |
| **Antigrav (гравитационное)** | спец. холодное + мины, нарушающие/усиливающие g | INT (наука), STR (масса воздействия) | «Гравитационный рывок», «Утяжеление цели», «Гравитационная стена» | d10 (blade), d8 (hammer) + crit_mod +10 |
| **Defense (защита)** | броня, щиты, стойки; **антигравийная** защита (отталкивание g-волн) | STR (ношение), INT (стойки) | «Тяжёлая броня I/II/III», «Стойка в обороне», «Антигравийный щит» | armorDefense stat (не damage dice) |

Подробное дерево каждой дисциплины (с damage-параметрами по ERPR) — в `20_SKILL_TREES.md`.

---

## ERPR-пакет (принят, вариант B)

Из `ERPR_collaboration.md §3` — что переносимо:

| ERPR-элемент | Где в Project C | Статус |
|---|---|---|
| Damage dice 1dN (d6/d8/d10/d12/d20) | `WeaponItemData.damageDice` | ✅ T-CB03 (добавляем поле) |
| `baseDamage` оружия | `WeaponItemData.baseDamage` | ✅ T-CB03 (добавляем поле) |
| `critModifier` | `WeaponItemData.critModifier` | ✅ T-CB03 (добавляем поле) |
| Crit 1d100 + critMod >= 100 → ×2 | Combat-движок (`Random.Range(1, 101)`) | 🔜 future (Combat-движок + TB-battles) |
| Hit location 1d4 (×0.5/1/2) | `HitLocation` enum + multiplier | 🔜 future (Combat-движок + TB-battles) |
| Сила/Ловкость/Интеллект как модификаторы | `StatsConfig` (уже есть) | ✅ совпадает 1:1 |
| Защита от экипировки | `ClothingItemData.armorDefense` | ✅ T-CB06 (добавляем поле) |
| **Не переносимо**: сетка, пошаговость, ГМ, магия, ОД-за-день, 20 ОЗ | — | ❌ |

**Главное отличие от ERPR:** в Project C урон НЕ привязан к «сетке» или «пошаговости». **Damage dice используется как вероятностная характеристика** в любом бою (real-time или turn-based). В пошаговых боях (`turn-based-battles/`) — формула применяется **буквально** как в ERPR. В real-time — combat-движок может использовать ту же формулу или упрощённую (среднее значение d6+d4=5 и т.п.).

---

## Расширения существующих систем (актуализировано)

| Что | Где | Что добавляем |
|---|---|---|
| `SkillNodeConfig` | T-P11, реализован | новое поле `CombatDiscipline discipline` (для combat-only skill) — display + фильтр; остальное без изменений |
| `SkillEffect` enum | T-P11, реализован | **новые Type:** `WeaponProficiencyUnlock`, `ArmorProficiencyUnlock`, `WeaponTechniqueUnlock`, `ExplosiveRecipeUnlock`, `AntigravTechniqueUnlock` |
| `EquipSlot` | T-P08, реализован | слоты `WeaponMain` / `WeaponOff` уже есть; новые `Grenade` / `Mine` (опц.) |
| `ItemData` (Clothing/Module) | T-P07, реализован | новый наследник **`WeaponItemData`** (Melee/Ranged/Antigrav) + **`ExplosiveItemData`** (Grenade/Mine) |
| **`WeaponItemData`** (новое) | T-CB03 | +3 поля: `damageDice`, `baseDamage`, `critModifier` (ERPR-пакет) |
| **`ClothingItemData`** (расширение) | T-CB06 | +1 поле: `armorDefense` (ERPR-пакет) |
| `EquipmentServer.TryEquip` | T-P09, реализован | расширяем валидацию: hard/soft `requiredSkills[]` уже есть (Q2.3) — переиспользуем |
| `SkillsServer` | T-P13, реализован | ничего не меняем; новые effect types читаются в `ApplySkillEffects` |
| `CharacterWindow` | T-P14, реализован | новый sub-tab-фильтр `CombatDiscipline` внутри combat-списка (Phase 2) |
| **Damage формула** (Combat-движок) | future | `final = (STR + 1dN + base) × hitLocation × crit × skillMult` (ERPR §3.1) |
| **`HitLocation` enum** | future | `Limbs=0.5, Torso=1, Head=2` (ERPR §3.2) |

Детали — в `10_DESIGN.md` (архитектура) + `20_SKILL_TREES.md` (дерево с damage dice).

---

## Roadmap реализации (обновлён v0.3, новый sequencing)

**Sequencing (v0.3):** Real-Time Combat Engine (MVP) → навыки (MVP+1) → PvP-duel (Phase 2) → ship combat (Phase 3) → turn-based (PARKING).

| # | Тикет | Что | Зависимости | Сложность |
|---|---|---|---|---|
| **T-RTC01..T-RTC09** | **Real-Time Combat Engine (MVP)** | **см. `../real-time-combat/`** | **самодостаточный** | **~23-32 ч (3-4 сессии)** |
| T-CB01 | Расширить `SkillEffect` enum | 5 новых Type | T-P11 уже есть | ~1-2 ч |
| T-CB02 | Добавить `CombatDiscipline` enum + поле в `SkillNodeConfig` | display + filter | T-P11 уже есть | ~0.5 ч |
| T-CB03 | `WeaponItemData` SO (extends ItemData) **+ 3 ERPR-поля** | новый тип предмета + ERPR | T-P07 pattern | ~2 ч |
| T-CB04 | `ExplosiveItemData` SO (extends ItemData) | гранаты/мины | T-P07 pattern | ~1.5 ч |
| T-CB05 | `WeaponClass` + `ArmorClass` + `WeaponTechnique` lookup SOs | справочники | новые | ~2 ч |
| T-CB06 | `EquipmentServer.TryEquip` + `ClothingItemData.armorDefense` | reuse Q2.3 + ERPR | T-P07+T-P09 | ~2 ч |
| T-CB07 | `SkillsServer.ApplySkillEffects` — обработка новых Type | reuse T-P13 | T-CB01 | ~2 ч |
| T-CB08 | `Resources/Skills/Combat/*.asset` — 5 веток, 35 нод **с damage-параметрами** | контент | T-CB01..07 | ~4-5 ч |
| T-CB09 | `CharacterWindow` — фильтр по `CombatDiscipline` в combat-sub-tab | UI | T-CB02 | ~1.5 ч |
| T-RTC11..T-RTC15 | PvP-дуэль flow (Phase 2) | duel invite, accept, duel HUD | T-RTC* | ~15-20 ч |
| T-RTC16..T-RTC20 | Ship combat (Phase 3, после ЗБТ) | `ShipAttacker`, `Turret`, `ShipRangePolicy` | T-RTC* | ~25-33 ч |
| T-TB01..T-TB14 | **Turn-based battles (PARKING, отложен)** | см. `../turn-based-battles/` | T-CB01..T-CB09 | ~46 ч (отложено) |

**Оценка (v0.3, фокус — MVP):**
- **MVP (пеший combat без навыков):** T-RTC01..T-RTC09 = **~23-32 ч** (3-4 сессии).
- **MVP+1 (пеший + навыки):** T-CB01..T-CB09 = **~16-21 ч** (2-3 сессии).
- **Phase 2 (PvP-дуэль + UI + NPC-AI):** ~30-40 ч.
- **Phase 3 (ship combat + ЗБТ):** ~25-33 ч.
- **PARKING (turn-based):** ~46 ч (отложено).

**ИТОГО до играбельного combat (пеший MVP + skills):** **~40-53 ч** (5-7 сессий).
**ИТОГО до играбельного combat (включая ship + PvP):** **~95-126 ч** (12-17 сессий).

**Ключевое:** движок спроектирован **anti-restrictive** (см. `../real-time-combat/10_DESIGN.md §1, §4`) — добавление ship combat = **0 изменений** в ядре, только новые классы-реализации.
### Внутренние (Project C)
- `docs/Character/06_SKILL_TREE.md` — базовый skill tree (T-P11..T-P14 уже реализовано)
- `docs/Character/03_DATA_MODEL.md` — паттерн `SkillNodeConfig` / `SkillEffect`
- `docs/Character/05_CLOTHING_AND_MODULES.md` — `EquipSlot` / `EquipmentData` / `TryEquip` (T-P08/T-P09)
- `docs/Character/09_OPEN_QUESTIONS.md` — решённые вопросы базовой системы (Q1-Q6)
- `../real-time-combat/` — **Real-Time Combat Engine** (MVP, **переиспользует** ERPR damage-формулу из `Battle/10_DESIGN.md §7`)
- `../turn-based-battles/` — **PARKING** (отложен на неопределённый срок, не развиваем)

### Игровой дизайн (не модифицируем)
- `docs/gdd/GDD_00_Overview.md` — пиллар 2: «Исследование над боем» (важно для баланса)
- `docs/gdd/GDD_20_Progression_RPG.md` — **расхождение** (см. `01_ANALYSIS.md §3.2`)

### Лор
- `docs/COLABORATION.md` — без магии; антиграв, мезий, генераторы
- `docs/ART_BIBLE.md` — палитра свечений (голубой=антиграв, зелёный=мезий)

### Шаблоны
- `MOON_SYSTEM.md` — шаблон тех. спецификации подсистемы
- `AGENTS.md` — hard rules (не трогать gdd/, не писать код/мета/asmdef в design-сессии)

### ERPR-источник
- `ERPR_collaboration.md` (этот каталог) — анализ ERPR-пакета, варианты A/B/C
- `F:\yandex\Yandex.Disk\Glob file\homerule\erpr\ERPR1.2.pdf` — исходник настолки (9 страниц)

---

## Следующий шаг

1. Прочитай `01_ANALYSIS.md` — что есть в коде, чего не хватает, явные конфликты (GDD 20 + ERPR).
2. Прочитай `02_LORE.md` — зафиксированные лор-факты (что подтверждено в проекте, что требует уточнения у game-designer'а).
3. Прочитай `ERPR_collaboration.md` — почему именно B, что переносимо, что нет.
4. Прочитай `10_DESIGN.md` — расширения существующих систем + ERPR-формула §7.
5. Прочитай `20_SKILL_TREES.md` — детальные деревья 5 дисциплин (с damage dice/crit).
6. Прочитай `30_PITFALLS_AND_OPEN_QUESTIONS.md` — антипаттерны и список решений, которые нужно от тебя.
7. **Отдельно**: прочитай `docs/Character/Skills/turn-based-battles/` — пошаговые бои как мини-игра.
