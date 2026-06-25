# Pitfalls & Open Questions

> **Pitfalls** — антипаттерны, которых избегаем при реализации. Часть уже зафиксирована в `06_SKILL_TREE.md §6-7` (skill tree pitfalls) — здесь переиспользуем.
> **Open Questions** — вопросы, требующие решения от тебя. Каждый раздел = одна область решений. После твоих ответов → design-doc обновится (в следующей сессии) → тикеты T-CB01..T-CB09 получают финальные ответы.
> **Обновлено v0.2 (2026-06-25):** добавлены вопросы по ERPR-пакету (§2.14-2.20) и по turn-based-battles (§2.21-2.25).

---

## 1. Pitfalls (антипаттерны)

### 1.1 Расширение `SkillEffect.Type` ломает существующий ApplySkillEffects (no-op)

**Сценарий:** T-CB01 расширяет enum (5 новых Type), T-CB07 (применить эффекты) — отдельный тикет. Между ними — `ApplySkillEffects` остаётся no-op, новые Type молча игнорируются.

**Решение:** в T-CB01 добавить **stub switch**:
```csharp
private void ApplySkillEffects(ulong clientId, SkillNodeConfig skill) {
    if (skill.effects == null) return;
    foreach (var effect in skill.effects) {
        switch (effect.type) {
            case SkillEffect.Type.StatMod: /* T-CB07 */ break;
            // ... existing ...
            case SkillEffect.Type.WeaponProficiencyUnlock: /* T-CB06 */ break;
            // ... new (no-op until T-CB07) ...
            default:
                Debug.LogWarning($"[SkillsServer] Unimplemented effect type: {effect.type}");
                break;
        }
    }
}
```

Вердикт: **никаких NRE** (default ловит), поведение = «как раньше» (no-op).

### 1.2 `WeaponItemData.maxStack` override ломает базовый ItemData.maxStack

**Сценарий:** `ItemData.maxStack` — public поле в `ItemType.cs:34` (Unity-сериализация). Если в `WeaponItemData` объявить `new int maxStack => 1`, Unity-сериализатор **запутается** (не знает, какое поле сериализовать).

**Решение (варианты):**
- (a) Не override, а **отдельное поле** `weaponMaxStack = 1` + warning в `OnValidate` если `maxStack != 1`.
- (b) Сделать `ItemData.maxStack` `virtual` + override в `WeaponItemData`/`ExplosiveItemData`. Меняет базовый класс (минимально, additive).
- (c) Просто задать `maxStack = 1` в `OnEnable()` (runtime) + warning в `OnValidate` (Editor).

**Рекомендация:** вариант (a) — наименее invasive. Подробности в `30_PITFALLS_AND_OPEN_QUESTIONS.md §2.6`.

### 1.3 `CombatDiscipline` поле ломает 8 существующих .asset

**Сценарий:** T-CB02 добавляет `public CombatDiscipline discipline = CombatDiscipline.None` в `SkillNodeConfig`. Unity при перезагрузке сериализует default. **OK**, не ломает.

**Но:** если сделать `[SerializeField]` — Unity сериализует `0` (None). Если existing .asset был сохранён до добавления поля — Unity допишет default. **Не ломает**.

**Вердикт:** safe. Verified by `OnValidate` patterns (см. `06_SKILL_TREE.md §6.7`).

### 1.4 Cycle detection ломает cross-tree prereq

**Сценарий:** `explosives_antigrav_mine` prereq = `explosives_mine_setting` + `antigrav_basic`. `antigrav_basic` prereq = none. `explosives_mine_setting` prereq = `explosives_basic_grenade`. Нет циклов → OK.

**Pitfall:** если designer случайно сделает `antigrav_basic` prereq = `explosives_basic_grenade` — цикл (через antigrav-mine). `SkillNodeConfig.OnValidate` ловит (warning, не throw).

**Вердикт:** в T-CB08 (создание .asset) — designer обязан проверить warning. Edge-case covered.

### 1.5 `TryEquip` — weapon без proficiency → reason текст зависит от локализации

**Сценарий:** `reason = $"Нужен навык владения: {weapon.weaponClass}"` — это **жёстко** русский текст. Если/когда добавим локализацию (i18n) — нужно переделать на enum + локаль.

**Решение (отложенное):** в MVP — hardcoded русский (как везде в проекте). При добавлении i18n — вынести в `LocalizationKeys.cs` (Phase 3).

### 1.6 Forward-declare stub (T-CB01..T-CB08) — compile-clean per ticket

Каждый тикет **должен компилироваться** без зависимости от следующего:
- T-CB01 (SkillEffect enum +5) — компилируется. ApplySkillEffects **no-op для новых** (default switch).
- T-CB02 (CombatDiscipline) — компилируется. UI не использует (Phase 2).
- T-CB03 (WeaponItemData) — компилируется. ItemRegistry **не регистрирует** (нужен `RegisterWeaponAssets()` аналог `RegisterEquipmentAssets()`) — добавляем в T-CB03.
- T-CB04 (ExplosiveItemData) — компилируется. Аналогично T-CB03.
- T-CB05 (lookup-каталоги) — компилируется. SO существует, но не используется до T-CB06.
- T-CB06 (EquipmentServer.TryEquip + armorDefense) — компилируется. `instance is WeaponItemData` + proficiency check + armorDefense. **Зависит от T-CB05**.
- T-CB07 (SkillsServer.ApplySkillEffects) — компилируется. Switch по 8 Type (3 existing + 5 new). Default = warning.
- T-CB08 (создание .asset) — последний, делается когда всё компилируется.
- T-CB09 (UI filter) — Phase 2.

### 1.7 Pitfall #30 из design-doc-session — не оставлять TODO без stub

Если `case WeaponProficiencyUnlock:` в `ApplySkillEffects` оставлен **пустым** без `// TODO: T-CB06` — будущий разработчик не поймёт, что это **намеренно** no-op.

**Решение:** все no-op cases помечаем:
```csharp
case SkillEffect.Type.WeaponProficiencyUnlock:
    // T-CB06: handled by EquipmentServer.TryEquip (proficiency check via SkillsWorld.GetLearnedSkills).
    // This effect is a marker — no runtime apply needed.
    break;
```

### 1.8 Уже известные skill tree pitfalls (из `06_SKILL_TREE.md §6-7`)

Переиспользуем без изменений:
- §6.1 Skill prerequisites цикл — T-P11 OnValidate ловит ✓
- §6.2 Skill ID collision — T-P12 LoadAllSkills warning ✓
- §6.7 Skill learned, but prereq array modified later — accepted (no revoke) ✓
- §7.1 50 nodes performance — Phase 2 (Painter2D + zoom), MVP — фильтр по discipline ✓
- §7.7 Skill effect floatValue negative — additive работает с negative, multiplicative нет (0..5) ✓
- §7.8 SkillSnapshot DTO size — sync on-change, не periodic ✓

### 1.9 Pitfall: prefix-collision между skillId

**Сценарий:** `melee_basic_sword` vs `melee_basicSword` (CamelCase vs snake_case) — оба валидные skillId, но **визуально одинаковы** в UI.

**Решение:** стандартизировать — **snake_case** для skillId (`melee_basic_sword`, `ranged_basic_pneumatic`). В displayName — русский + CamelCase для читаемости (`Владение мечом`).

### 1.10 Pitfall: Antigrav-prereq для Explosives — designer-зависимо

`explosives_antigrav_mine` требует `antigrav_basic`. Если player хочет идти **только по Explosives** (без antigrav) — он **не сможет** сделать antigrav-мину. Это **намеренно** (lore) — но может фрустрировать.

**Решение:** отображать в UI причину блокировки (`Требуется: Базовый антигравий (Ветка: Antigrav)`). Не пытаться обойти.

### 1.11 Pitfall (ERPR): damage формула + skillMult stack — risk of over-stacking

**Сценарий:** Игрок учит `melee_great_sword` (skillMult ×1.15) + `Skill_Combat_HeavySwing` (skillMult ×1.2) + `ranged_aimed_shot` (×1.0) + оружие d10 → может достичь **множителя ×1.38+** в формуле. С Head (×2) + Crit (×2) → ×5.5 финального множителя. **Overkill** в PvE.

**Решение:**
- Документировать в `T-CB08`: максимальный множитель per-цепочка (например, Melee Tier 2 = ×1.5 max).
- Combat-движок: clamp `skillMult <= 2.0` (hard cap).
- Или: каждый skillMult заменяет предыдущий, а не стекается (альтернативный дизайн).

**Вердикт:** в Phase 2 — soft clamp в Combat-движок. В MVP — дизайнеры должны проверять.

### 1.12 Pitfall (ERPR): `armorDefense` в ClothingItemData — обратная совместимость

**Сценарий:** 5 существующих .asset (`Clothing_WorkerHelmet.asset` и т.п.) получат `armorDefense = 0` при добавлении поля. Если designer не обновит — все 5 предметов = 0 защиты. **Броня не работает**.

**Решение:** при T-CB06 — designer **обязан** вручную обновить 5 .asset (или написать migration script):
- `Clothing_WorkerHelmet` (Head, Tier 1) → `armorDefense = 2`
- `Clothing_SteelChestplate` (Chest, Tier 2) → `armorDefense = 8`
- `Clothing_TravelerBoots` (Feet, Tier 1) → `armorDefense = 1`
- `Clothing_MerchantCloak` (Back, Tier 2) → `armorDefense = 3`
- `Clothing_SmithApron` (Chest, Tier 1) → `armorDefense = 1`

**Вердикт:** в MVP — ручное обновление. В Phase 2 — migration script через `ResourcesCsvImporter` (уже есть в `Assets/_Project/Items/Editor/`).

### 1.13 Pitfall (ERPR): HitLocation в real-time — нет визуальной обратной связи

**Сценарий:** `locRoll = 4` (Head) → `locMult = 2.0`, но в real-time combat-движке **нет анимации** «попал в голову». Игрок видит обычный урон. HitLocation — **декоративный** множитель, если не анимировать.

**Решение:** для real-time MVP — `locMult = 1.0` (отключить). HitLocation **только** в turn-based battles (где есть пошаговая анимация). См. `turn-based-battles/`.

**Вердикт:** реально используется в TB. В real-time — Phase 3 (после анимаций).

### 1.14 Pitfall (ERPR): формула в `CalculateDamage` — кто кидает кубики?

**Сценарий:** на сервере 100 игроков, каждый бросает 1dN каждую секунду в бою → нагрузка.

**Решение:** кубики бросает **только сервер** (authoritative). `CalculateDamage` — server-only. На клиенте — UI-предсказание (показ кубика до server-подтверждения), но **результат** — от сервера.

### 1.15 Pitfall (turn-based): reentrancy в `TurnBasedBattle` — длинные расчёты

**Сценарий:** игрок делает цепочку действий в turn-based (3 сек): перемещение (1) + атака (2) + перемещение (1) = 4 сек. Как обработать «не хватает секунд»?

**Решение:** server проверяет `currentSecondsRemaining` перед каждым action. Если `cost > remaining` → отказ. UI показывает сколько секунд осталось (см. `turn-based-battles/10_DESIGN.md §5.2`).

### 1.16 Pitfall (turn-based): бой между двумя игроками — оба должны быть online

**Сценарий:** игрок A приглашает игрока B на PvP-дуэль. Если B offline — что делать?

**Решение:** дуэль только при `both online`. Если один выходит — бой отменяется, XP/loss не начисляются. Подробности в `turn-based-battles/30_SCENARIOS.md §3`.

### 1.17 Pitfall (turn-based): determinism в distributed RNG

**Сценарий:** `CalculateDamage` использует `Random.Range` — но в TB два клиента могут видеть **разные** результаты (если server ленив).

**Решение:** **server-authoritative** — клиенты шлют `SubmitActionRpc`, сервер **сам** считает урон, шлёт `ActionResultTargetRpc` обоим. Клиенты **не** кидают кубики.

---

## 2. Open Questions (для пользователя)

> **Формат:** каждый раздел — одна область решений. После твоих ответов — design-doc обновится (в следующей сессии) → тикеты T-CB01..T-CB09 получают финальные ответы.
> **Мои рекомендации** отмечены `**РЕК:**`.

### 2.1 Какие дисциплины в MVP?

**Текущая догадка:** все 5 (Melee / Ranged / Explosives / Antigrav / Defense).

| Вариант | Что |
|---|---|
| (a) **Все 5** (текущая) | полная система, 35 нод, ~14-18 ч кодинга |
| (b) **3 базовых** (Melee / Ranged / Defense) | ~12 нод, ~6-8 ч, без antigrav/explosives |
| (c) **Только Melee** | ~8 нод, ~4-5 ч, проверяем систему |

**РЕК:** **(a) все 5** — навыки без antigrav/explosives не дают полной картины combat. Antigrav — уникальная фишка проекта (лор), должна быть в MVP.

### 2.2 Стартовые combat-навыки — какие?

**Текущая догадка:** **0 starter combat-навыков** (по решению Q3.2 = b для базовой системы). Игрок учит basic_sword/basic_dagger/basic_crossbow/etc. сам за XP.

| Вариант | Что |
|---|---|
| (a) **0 starter** | игрок учит всё сам (consistent с Q3.2 = b) |
| (b) **basic_sword (XP 0)** | один бесплатный стартовый — меч |
| (c) **basic_sword + basic_dagger (XP 0)** | два стартовых — выбор ближнего боя |

**РЕК:** **(a) 0 starter** — consistency с Q3.2. Игрок тратит первые XP на выбор оружия. Это часть онбординга.

### 2.3 ItemType.Meziy для оружия — конфликт с ресурсом

**Текущая догадка:** `WeaponItemData` с `itemType = ItemType.Equipment` + внутреннее поле `WeaponSubType.MeziyBased` (см. `01_ANALYSIS.md §3.3`).

| Вариант | Что |
|---|---|
| (a) **Оставить ItemType.Meziy** для ресурса, мезиевое оружие = `ItemType.Equipment` + `WeaponSubType` | минимальные изменения, чисто семантически |
| (b) **Ввести ItemType.MeziyWeapon** (8 → 9 категорий) | полная ясность, но миграция |
| (c) **Игнорировать** — игрок не фильтрует инвентарь по Meziy | минимум работы |

**РЕК:** **(a)** — чистое разделение без миграции. Открыто для game-designer'а.

### 2.4 Антиграв-щит в Defense-ветке

**Текущая догадка:** **нет** `ArmorClass.AntigravShield` — нет подтверждения в lore. Если game-designer подтвердит — добавим.

| Вариант | Что |
|---|---|
| (a) **Нет антиграв-щита** | Defense-ветка = 6 нод (без antigrav) |
| (b) **Есть антиграв-щит** | +1 навык `defense_antigrav_shield`, prereq = `antigrav_basic` |

**РЕК:** **(a)** пока не подтверждено. Открыто для game-designer'а (см. `02_LORE.md §6.2`).

### 2.5 Штраф DEX за тяжёлую броню

**Текущая догадка:** `defense_heavy_armor` = StatMod(STR+3) **без штрафа**. Опционально — `StatMod(DEX, -2)`.

| Вариант | Что |
|---|---|
| (a) **Без штрафа** | простая математика, нет «cost» для тяжёлой брони |
| (b) **Штраф DEX-2** | реалистично, тяжёлая броня = медленнее |
| (c) **Multiplicative DEX ×0.9** | сильнее штраф, но multiplicative (Phase 2) |

**РЕК:** **(b)** — реалистично, помогает балансу (тяжёлая броня не всегда лучший выбор). Стоимость = STR-tier tradeoff.

### 2.6 `WeaponItemData.maxStack` — как override

**Текущая догадка:** отдельное поле `weaponMaxStack = 1` (вариант (a) из `§1.2`).

| Вариант | Что |
|---|---|
| (a) **Отдельное поле** `weaponMaxStack` + warning в OnValidate | наименее invasive |
| (b) **`ItemData.maxStack` virtual + override** | чище, но меняет базовый класс |
| (c) **OnEnable runtime set** | хак, не persistent в editor |

**РЕК:** **(a)** — наименее invasive, не ломает существующие .asset. OnValidate warning если designer выставит maxStack != 1.

### 2.7 Что делать со старыми Combat-навыками (T-P11)?

**Текущая догадка:** **сохраняем как есть** + `discipline = None` (default). Они в фильтре «All».

| Вариант | Что |
|---|---|
| (a) **Сохраняем как есть** | `discipline = None`, generic StatMod навыки |
| (b) **Привязываем к Melee** | `Skill_Combat_BasicStrike.discipline = Melee` |
| (c) **Удаляем (replaced by melee_basic_sword etc.)** | ломает обратную совместимость 4 .asset |

**РЕК:** **(a)** — generic roots, доступны всем, не привязаны к оружию. Это **намеренно** — базовый stat-bonus, который работает в любом бою.

### 2.8 Tier cap для SkillNodeConfig

**Текущая догадка:** без tier cap (по аналогии с StatsConfig, Q1.3). `RequiredIntelligenceTier` = 0..N.

| Вариант | Что |
|---|---|
| (a) **Без cap** | Q3.3a (0/100/200), рекомендация из базовой системы |
| (b) **Tier cap 5** | mastery tier = 5 max, за tier 5 = закрытая зона |
| (c) **Tier cap 10** | больше глубины для долгоиграющих |

**РЕК:** **(a)** — consistency с Q3.3a, дизайнер может добавить навыки tier 6+ позже.

### 2.9 Combat-движок — когда и как?

**Текущая догадка:** **real-time combat-движок — отдельная подсистема** (вне scope этого диздока). **Turn-based battles — отдельный документ** (см. `turn-based-battles/`). Навыки — **только разблокируют** классы/техники/рецепты.

| Вариант | Что |
|---|---|
| (a) **Сначала combat-движок real-time, потом навыки** | навыки сразу работают end-to-end |
| (b) **Сначала навыки, потом combat-движок + turn-based** | навыки готовы, движок подключается |
| (c) **Параллельно** | риск рассинхрона |

**РЕК:** **(b)** — навыки **маркеры** готовятся первыми (T-CB01..T-CB09), real-time combat-движок + turn-based battles — отдельные подсистемы, подключаются позже. ERPR-формула готова (`10_DESIGN.md §7`) и используется **обоими**.

### 2.10 PvP / мультиплеер-combat

**Текущая догадка:** **PvP через turn-based battles** (см. `turn-based-battles/30_SCENARIOS.md §3`).

| Вариант | Что |
|---|---|
| (a) **Только PvE** | turn-based только для NPC |
| (b) **PvP-aware** | turn-based для игрок vs игрок, server-authoritative |

**РЕК:** **(b)** — TB-подсистема с самого начала проектируется с поддержкой PvP (1v1 duel). Не усложняет.

### 2.11 Integration с Crafting — когда?

**Текущая догадка:** `ExplosiveRecipeUnlock` effect = флаг. Реальный рецепт регистрируется в `CraftingSystem` (T-CB07 part 4 — stub).

| Вариант | Что |
|---|---|
| (a) **Flag-only** | навык даёт флаг "знаю рецепт", CraftingSystem читает флаг и добавляет рецепт в UI |
| (b) **Прямая регистрация** | T-CB07 дёргает CraftingSystem.RegisterRecipe(playerId, recipeId) |
| (c) **Отложить** | T-CB07 — no-op для ExplosiveRecipeUnlock, CraftingSystem подключаем позже |

**РЕК:** **(c)** — MVP = no-op. Когда CraftingSystem станет полноценным (T-Q22+, M19 уже реализован, см. `docs/NPC_quests/`) — подключим. Не блокируем combat-скилы ожиданием Crafting.

### 2.12 Сколько нод создавать в T-CB08?

**Текущая догадка:** **все 35** (4 generic + 8 Melee + 6 Ranged + 5 Explosives + 6 Antigrav + 6 Defense).

| Вариант | Что |
|---|---|
| (a) **Все 35** | полная система, ~4 ч кодинга + .asset |
| (b) **MVP = 10** (4 generic + 6 дисциплин-Tier-0) | проверяем систему, добиваем позже |
| (c) **MVP = 20** (4 generic + 5 дисциплин × 3-4 ноды) | middle ground |

**РЕК:** **(a) все 35** — навыки создаются один раз, не блокирует ничего, экономит сессии. ~4 ч.

### 2.13 Где расположить `CombatDiscipline` enum и новые Weapon/Armor классы?

**Текущая догадка:** в `Assets/_Project/Scripts/Skills/CombatDiscipline.cs` + `Assets/_Project/Scripts/Equipment/WeaponClass.cs` etc.

| Вариант | Что |
|---|---|
| (a) **В существующих namespace** (`ProjectC.Skills`, `ProjectC.Equipment`) | минимально новых файлов |
| (b) **Новый namespace** `ProjectC.Combat` | явная изоляция, больше файлов |

**РЕК:** **(a)** — consistency с `ProjectC.Equipment`, `ProjectC.Skills`. Избегаем плодящиеся namespace.

### 2.14 (ERPR) Damage dice — default значения для новых .asset

**Текущая догадка:** `damageDice = d6`, `baseDamage = 1`, `critModifier = 0` для всех новых `WeaponItemData`.

| Вариант | Что |
|---|---|
| (a) **Default d6/base=1/crit=0** | безопасные defaults, designer меняет вручную |
| (b) **Default 0 baseDamage, 0 dice** | явно «не готово», designer заполняет перед использованием |
| (c) **Default по weaponClass** | автоматический выбор (sword=d6, dagger=d4, ...) |

**РЕК:** **(c)** — `OnEnable()` или `OnValidate()` ставит дефолт по `weaponClass`. Например, `Sword → d6/3/0`, `Crossbow → d8/4/+5`, `AntigravBlade → d8/3/+10`. Минимизирует designer-error.

### 2.15 (ERPR) critModifier — диапазон

**Текущая догадка:** `[Range(-20, 20)] int critModifier`. Положительные = чаще крит, отрицательные = реже.

| Вариант | Что |
|---|---|
| (a) **-20..+20** | текущая догадка, широкий диапазон |
| (b) **0..+30** | только positive, нет «анти-crit» (нелогично) |
| (c) **-10..+10** | узкий диапазон, проще балансить |

**РЕК:** **(a)** — для будущих легендарных оружий может быть нужно `-10` (проклятое) или `+20` (мифическое). Дизайнер сам решает, что нормально.

### 2.16 (ERPR) armorDefense — диапазон и накопительный эффект

**Текущая догадка:** `[Range(0, 50)] int armorDefense` в `ClothingItemData`. Суммируется по слотам.

| Вариант | Что |
|---|---|
| (a) **0..50 per item, суммируется** | текущая, простая |
| (b) **0..30, штраф за «over-armor»** (например, `>40` total = -DEX) | баланс против стек-tank |
| (c) **0..50, multiplicative set bonus** | тяжёлая броня из 3-х частей = ×1.2 (Phase 3) |

**РЕК:** **(a)** для MVP. **(b)/(c)** — Phase 3 после playtest.

### 2.17 (ERPR) Hit location — везде или только в TB?

**Текущая догадка:** HitLocation в MVP-1 = **только в turn-based battles** (`turn-based-battles/`). В real-time combat-движок = `locMult = 1.0` (отключено).

| Вариант | Что |
|---|---|
| (a) **Только в TB** | текущая, real-time без hit_location |
| (b) **Везде** | real-time + TB используют hit_location (но без анимации попадания) |
| (c) **Нигде** | упрощение, `locMult = 1.0` всегда |

**РЕК:** **(a)** — TB = основное применение hit_location (есть пошаговая анимация). Real-time Phase 3 (после анимаций).

### 2.18 (ERPR) SkillMult cap — насколько жёстко ограничивать?

**Текущая догадка:** soft cap через дизайнера (см. §1.11). Combat-движок Phase 2 имеет hard cap `skillMult <= 2.0`.

| Вариант | Что |
|---|---|
| (a) **Soft cap (дизайнер)** | минимум ограничений, designer error = баг |
| (b) **Hard cap 2.0** | безопасный, но может мешать экстремальным билдам |
| (c) **Hard cap 3.0** | более либеральный |

**РЕК:** **(b)** — 2.0 (×2) достаточно для «легендарного мастера». Если нужно больше — Phase 3 пересмотр.

### 2.19 (ERPR) damage formula — где находится?

**Текущая догадка:** `10_DESIGN.md §7.2` — псевдокод `CalculateDamage`. Реальная реализация — в `Assets/_Project/Scripts/Combat/DamageCalculator.cs` (отдельная подсистема, future).

| Вариант | Что |
|---|---|
| (a) **Отдельный файл** `Combat/DamageCalculator.cs` (static class) | чисто, переиспользуемо |
| (b) **Внутри CombatServer** (real-time) и TurnBasedBattleServer (TB) | дублирование, но близко к вызову |
| (c) **Внутри WeaponItemData** (метод `RollDamage()`) | ООП-чисто, но много логики в SO |

**РЕК:** **(a)** — `Combat/DamageCalculator.cs` static class, используется обоими (real-time и TB).

### 2.20 (ERPR) Combat-движок — насколько сейчас?

**Текущая догадка:** **Combat-движок — отдельная подсистема**, ~30-40 ч. Навыки + ERPR-пакет = **16-21 ч** (без движка). Turn-based battles = **40-60 ч** (отдельный документ).

| Что | Трудозатраты | Когда |
|---|---|---|
| **T-CB01..T-CB09** (навыки + ERPR-пакет) | **~16-21 ч** | 2-3 сессии |
| T-CB10 (real-time Combat-движок) | ~30-40 ч | позже (отдельный) |
| T-TB01..T-TB10 (turn-based battles) | ~40-60 ч | позже (отдельный) |
| **ИТОГО до играбельного combat** | **~86-121 ч** | 10-15 сессий |

**РЕК:** начать с T-CB01..T-CB09 (навыки), потом **параллельно** T-CB10 + T-TB01..T-TB10 (можно одной сессией — навыки готовы).

### 2.21 (Turn-based) PvE данж — что именно?

**Текущая догадка:** PvE данж = **соло-данж** (1 игрок vs N NPC). См. `turn-based-battles/30_SCENARIOS.md §1`.

| Вариант | Что |
|---|---|
| (a) **Соло-данж** | 1 игрок vs 1-3 NPC на сетке |
| (b) **Кооп-данж** | 2-4 игрока vs 5-10 NPC на большой сетке |
| (c) **Оба** | соло + кооп, разные сложности |

**РЕК:** **(c)** — соло простой (для онбординга), кооп-сложный (для долгоиграющих). Начать с (a) в MVP.

### 2.22 (Turn-based) PvP дуэль — формат

**Текущая догадка:** **1v1 дуэль** между игроками на сетке 8x8. См. `turn-based-battles/30_SCENARIOS.md §3`.

| Вариант | Что |
|---|---|
| (a) **1v1 duel** | стандарт, простая синхронизация |
| (b) **2v2 team** | сложнее, но интереснее |
| (c) **Free-for-all (3-4 игрока)** | максимально сложно, server-state |

**РЕК:** **(a) 1v1** для MVP. (b)/(c) — Phase 3.

### 2.23 (Turn-based) Сетка — какой размер по умолчанию?

**Текущая догадка:** сетка 10x10 клеток для соло-данжа, 8x8 для дуэли. 1 клетка = 2м (см. `turn-based-battles/10_DESIGN.md §2`).

| Вариант | Что |
|---|---|
| (a) **10x10 соло, 8x8 дуэль** | текущая |
| (b) **Один размер 8x8** | упрощение UI |
| (c) **Адаптивный** (по числу участников) | максимум 16x16, минимум 6x6 |

**РЕК:** **(c)** — адаптивный по сценарию. Реализация через `BattleConfig.gridSize`.

### 2.24 (Turn-based) Инициатива — кто ходит первым?

**Текущая догадка:** по `DEX` (высший DEX ходит первым). При равном DEX — `Random.Range` для tie-break. См. `turn-based-battles/10_DESIGN.md §4`.

| Вариант | Что |
|---|---|
| (a) **По DEX** | текущая, тактично |
| (b) **По Speed stat** (отдельный, future) | больше контроля |
| (c) **Random для всех** | максимально просто, нет тактики |

**РЕК:** **(a)** — `DEX` уже есть в `StatsConfig`, не нужно нового stat.

### 2.25 (Turn-based) Смерть в бою — что дальше?

**Текущая догадка:** HP = 0 → выход с сетки, **respawn через 5 сек**, **потеря 20% XP** текущего уровня (ERPR §2.3 «При потере всех ОЗ, судьбу персонажа решает ГМ» → MMO-эквивалент = respawn + XP penalty).

| Вариант | Что |
|---|---|
| (a) **Respawn + 20% XP loss** | текущая, по ERPR |
| (b) **Respawn без XP loss** | проще для игроков, но слабее stakes |
| (c) **Permadeath в PvP-дуэли** | жёстко, но PvP обычно consent-based |

**РЕК:** **(a)** для PvE, **(c)** для PvP (consent-based). Подробности в `turn-based-battles/30_SCENARIOS.md §3.4`.

---

## 3. Промежуточный сводный список (обновлено v0.2)

| # | Область | Рекомендация | Альтернативы |
|---|---|---|---|
| 2.1 | Дисциплины в MVP | **(a) все 5** | 3 базовых, только Melee |
| 2.2 | Стартовые combat | **(a) 0** | basic_sword free, sword+dagger free |
| 2.3 | ItemType.Meziy | **(a) оставить** | ItemType.MeziyWeapon, игнор |
| 2.4 | Антиграв-щит | **(a) нет** | добавить если подтвердит game-designer |
| 2.5 | DEX-штраф heavy armor | **(b) StatMod(DEX, -2)** | без штрафа, multiplicative |
| 2.6 | maxStack override | **(a) отдельное поле** | virtual, OnEnable |
| 2.7 | Старые Combat-скилы | **(a) сохраняем** | привязать к Melee, удалить |
| 2.8 | Tier cap | **(a) без cap** | cap 5, cap 10 |
| 2.9 | Combat-движок | **(b) навыки сначала** | сначала движок, параллельно |
| 2.10 | PvP | **(a) PvE в TB** + **(b) PvP-aware** | только PvE |
| 2.11 | Crafting integration | **(c) отложить** | flag, прямая регистрация |
| 2.12 | Кол-во нод T-CB08 | **(a) все 35** | 10, 20 |
| 2.13 | Namespace | **(a) существующие** | новый ProjectC.Combat |
| 2.14 (ERPR) | Damage dice defaults | **(c) по weaponClass** | безопасные defaults, 0 base |
| 2.15 (ERPR) | critModifier range | **(a) -20..+20** | 0..+30, -10..+10 |
| 2.16 (ERPR) | armorDefense range | **(a) 0..50, суммируется** | штраф over-armor, set bonus |
| 2.17 (ERPR) | Hit location | **(a) только в TB** | везде, нигде |
| 2.18 (ERPR) | SkillMult cap | **(b) hard cap 2.0** | soft cap, cap 3.0 |
| 2.19 (ERPR) | DamageCalculator | **(a) static class `Combat/`** | в server, в SO |
| 2.20 (ERPR) | Объём combat | **16-21 ч навыки + 30-40 ч RT + 40-60 ч TB** | — |
| 2.21 (TB) | PvE данж | **(c) соло + кооп** | только соло |
| 2.22 (TB) | PvP формат | **(a) 1v1** для MVP | 2v2, FFA |
| 2.23 (TB) | Размер сетки | **(c) адаптивный** | 10x10/8x8, один 8x8 |
| 2.24 (TB) | Инициатива | **(a) по DEX** | Speed, Random |
| 2.25 (TB) | Смерть | **(a) respawn+XP для PvE**, **(c) permadeath для PvP** | без XP loss |

---

## 4. Что НЕ обсуждаем (вне scope)

- ❌ Damage-формулы, попадание, крит (combat-движок) — см. `10_DESIGN.md §7`
- ❌ NPC-враги, faction AI (future)
- ❌ Real-time Combat-движок (hit/projectile) — отдельная подсистема, `10_DESIGN.md §1`
- ❌ Анимации ударов/блока (3D-отдел)
- ❌ VFX-эффекты (3D-отдел + шейдеры)
- ❌ Sound effects (audio-отдел)
- ❌ Баланс конкретных уронов/бронепробитий (требует playtest)
- ❌ Skill respec с возвратом XP (Q3.4 = free respec без возврата)
- ❌ Skill tree визуализация Painter2D (T-P19, Phase 2)
- ❌ Drag-and-drop equip (T-P20, Phase 2)
