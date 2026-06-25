# Battle Skills — боевые навыки поверх существующей skill tree

> **Подсистема:** Character Progression → Skill Tree → Combat branch
> **Статус:** 🟡 Проектирование (v0.2, 2026-06-25) — **актуализировано под вариант B** (ERPR-пакет принят)
> **Базовый документ:** `docs/Character/06_SKILL_TREE.md` (T-P11..T-P14 уже реализовано)
> **Коллаборация с ERPR:** `ERPR_collaboration.md` (damage dice + crit + hit location, без магии, без сетки, без пошаговости)
> **Scope сессии:** research + design-doc only. **Без кода.** Реализация — отдельные сессии по тикетам T-CB01.. (см. `30_PITFALLS_AND_OPEN_QUESTIONS.md`).
> **Смежный документ:** `docs/Character/Skills/turn-based-battles/` — **отдельная подсистема** для пошаговых мини-игр (PvE-данж, PvP-дуэли, ивенты). Реализует ERPR-ядро в полном объёме (сетка + 3 сек + ГМ-эквивалент = сервер-ИИ).

---

## TL;DR

Сегодня Combat-навыки в проекте — это 4 placeholder-ноды в `docs/Character/06_SKILL_TREE.md §1.3`:
`BasicStrike (+2 STR)`, `DodgeRoll (+3 DEX)`, `HeavySwing`, `PrecisionStrike`. Все четыре — обычные `SkillEffect.StatMod`, **не привязаны к оружию**. CHANGELOG 2026-06-17 фиксирует: *«Skills click handlers deferred до battle system»*. Combat-системы как таковой в коде нет: `WeaponItemData` не существует, `EquipSlot.WeaponMain/Off` объявлены, но реальное оружие не описано; `CombatWorld/CombatServer` отсутствуют.

**Решение сессии (вариант B из `ERPR_collaboration.md`):**
- **Damage dice 1dN** (d6/d8/d10/d12/d20) → 3 новых поля в `WeaponItemData` (`damageDice`, `baseDamage`, `critModifier`).
- **Crit 1d100 + crit_modifier** → нативная замена через `Random.Range(1, 101) + critModifier >= 100`.
- **Hit location 1d4** (Limbs/Torso/Head, ×0.5/1/2) → `HitLocation` enum + multiplier.
- **Защита от экипировки** → новое поле `armorDefense` в `ClothingItemData` (в Defense-ветке).
- **Сила/Ловкость/Интеллект** (модификаторы) → уже 1:1 совпадают с `StatsConfig`.

**Что остаётся вне Combat-навыков (отдельная подсистема):**
- ❌ **Пошаговость** (3 сек на ход) — это **только** в `turn-based-battles/` (отдельный документ).
- ❌ **Сетка квадратов/гексов** — только в `turn-based-battles/` (PvE-данж, PvP-дуэль).
- ❌ **ГМ-свобода предложений** — не применимо, сервер-авторитативный.
- ❌ **Магия** — запрещена лором.

**Задача сессии (актуализировано под B):**

1. **Расширяем существующий `SkillNodeConfig` + `SkillEffect`** (T-P11) — не ломая 8 уже созданных .asset.
2. **Добавляем 3 поля в `WeaponItemData`** (T-CB03) — `damageDice`, `baseDamage`, `critModifier` (ERPR-пакет).
3. **Добавляем `armorDefense` в `ClothingItemData`** — для Defense-ветки.
4. **Строим 5 боевых дисциплин** (Melee / Ranged / Explosives / Antigrav / Defense), каждая = отдельная подветка Combat. Навык разблокирует владение классом оружия/брони, технику (парирование, прицельный выстрел, рикошет, подкат, и т.п.) или рецепт (граната, мина).
5. **Опционально** — `HitLocation` enum для Combat-движка (Phase 2, отдельная подсистема).
6. **Опционально** — damage dice / crit / hit location используются в `turn-based-battles/` (пошаговый бой, отдельный документ).

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

## Roadmap реализации (обновлён под B)

| # | Тикет | Что | Зависимости | Сложность |
|---|---|---|---|---|
| T-CB01 | Расширить `SkillEffect` enum | 5 новых Type | T-P11 уже есть | ~1-2 ч |
| T-CB02 | Добавить `CombatDiscipline` enum + поле в `SkillNodeConfig` | display + filter | T-P11 уже есть | ~0.5 ч |
| T-CB03 | `WeaponItemData` SO (extends ItemData) **+ 3 поля ERPR (damageDice/baseDamage/critModifier)** | новый тип предмета + ERPR | T-P07 pattern | ~1.5 ч |
| T-CB04 | `ExplosiveItemData` SO (extends ItemData) | гранаты/мины | T-P07 pattern | ~1.5 ч |
| T-CB05 | `WeaponClass` + `ArmorClass` + `WeaponTechnique` lookup SOs | справочники | новые | ~2 ч |
| T-CB06 | `EquipmentServer.TryEquip` + `ClothingItemData.armorDefense` | reuse Q2.3 + ERPR | T-P07+T-P09 | ~1.5 ч |
| T-CB07 | `SkillsServer.ApplySkillEffects` — обработка новых Type | reuse T-P13 | T-CB01 | ~2 ч |
| T-CB08 | `Resources/Skills/Combat/*.asset` — 5 веток, ~25-30 нод **с damage-параметрами** | контент | T-CB01..07 | ~4-5 ч |
| T-CB09 | `CharacterWindow` — фильтр по `CombatDiscipline` в combat-sub-tab | UI | T-CB02 | ~1.5 ч |
| T-CB10 | Combat-движок (real-time, hit/damage/projectile) — **ERPR-формула в полном объёме** | future | T-CB08 + параллельная работа | большая (~30-40 ч) |
| T-TB01..T-TB10 | **Пошаговые бои** (отдельная подсистема) | PvE/PvP/ивенты | T-CB01..08 (навыки) + Combat-движок | большая (~40-60 ч) |

**Оценка:** T-CB01..T-CB09 = **~16-21 ч кодинга** (2-3 сессии). ERPR-пакет добавляет ~0.5 ч к T-CB03 (3 поля в SO) + 0.5 ч к T-CB06 (1 поле в ClothingItemData). Без combat-движка — навыки только **разблокируют** классы/техники/рецепты, а реальный бой — отдельный engine + пошаговые бои (`turn-based-battles/`).

---

## Связанные документы

### Внутренние (Project C)
- `docs/Character/06_SKILL_TREE.md` — базовый skill tree (T-P11..T-P14 уже реализовано)
- `docs/Character/03_DATA_MODEL.md` — паттерн `SkillNodeConfig` / `SkillEffect`
- `docs/Character/05_CLOTHING_AND_MODULES.md` — `EquipSlot` / `EquipmentData` / `TryEquip` (T-P08/T-P09)
- `docs/Character/09_OPEN_QUESTIONS.md` — решённые вопросы базовой системы (Q1-Q6)
- `docs/Character/Skills/turn-based-battles/` — **отдельная подсистема пошаговых боёв** (PvE-данж, PvP-дуэль)

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
