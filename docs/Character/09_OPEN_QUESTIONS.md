# Open Questions — Character Progression

> **Дата:** 2026-06-14
> **Что нужно:** ответы от тебя на вопросы ниже. Каждый ответ заблокирует или разрешит конкретные design-decisions в `03_DATA_MODEL.md`, `04_STATS_PROGRESSION.md`, `05_CLOTHING_AND_MODULES.md`, `06_SKILL_TREE.md`.
> **Формат:** каждая секция — одна область решений. После твоих ответов — design-doc обновится (в следующей сессии).

---

## 1. Stats (3 характеристики: Сила / Ловкость / Интеллект)

### Q1.1 Стартовые значения

**Текущая догадка:** все начинают с 0 XP (tier 0). Tier promotion начинается с 100 XP (default `_tierBaseXp`).

**Вопрос:** Какой стартовый XP для каждой характеристики?

| Вариант | Описание |
|---------|----------|
| (a) **0/0/0** | все начинают равны, дифференциация через 1-2 действия |
| (b) **5/5/5** | лёгкий стартовый буфер для каждой характеристики |
| (c) **10/10/10** | ощутимый старт, но без значимого преимущества |
| (d) **0/5/10** | "Интеллект для диалогов" важнее в начале игры (диалоги с NPC — первый контакт) |
| (e) **Per-archetype** (если будут фракции) | Каждая фракция/профессия имеет свои стартовые значения |

**Моя рекомендация:** **(a) 0/0/0** — самый простой для понимания игрока. Tier 0 → 1 после первого действия = видимый прогресс.

**ОТВЕТ:** а. все 0

### Q1.2 Per-stat base XP разные?

**Текущая догадка:** одна `_tierBaseXp = 100` для всех 3 характеристик.

**Вопрос:** Нужна ли разная "цена" тира для разных характеристик?

| Вариант | Описание |
|---------|----------|
| (a) **Все одинаковые** | `_tierBaseXp = 100` для STR/DEX/INT |
| (b) **INT дороже** | `_tierBaseXp_intelligence = 200` (INT = "advanced stat") |
| (c) **STR дешевле** | `_tierBaseXp_strength = 50` (mining = самый простой источник) |

**Моя рекомендация:** **(a) все одинаковые** в MVP — простота. Phase 2 — возможно дифференцировать.

**ОТВЕТ:** все одинаковые. главное реализовать не в хардкоде разные множители за разные действия. по сути нет разницы 10 силы или 10 инт. если чтобы что-то совершилось\открылось - нужен свой коэффицент и проходной бал.

### Q1.3 Глобальный множитель (тестовость)

**Текущая догадка:** `globalMultiplier = 1.0` default. Range `[0.01f, 10f]`.

**Вопрос:** Подходит ли диапазон? Или нужно шире?

| Вариант | Диапазон | Назначение |
|---------|----------|------------|
| (a) **0.01..10** | двойной XP event, или фактически отключить XP |
| (b) **0.1..5** | узкий диапазон, только умеренный буст |
| (c) **0..100** | широкий, экстремальные тесты |

**Моя рекомендация:** **(a) 0.01..10** — даёт тестировщику полный контроль (от "выключить рост" до "10x XP event").

**ОТВЕТ:** без ограничений. единицы юнити не совсем понятны, гдето и 10 000 мало. буду тестами подбирать, но нужно думать единицу (1) как ориентир на норму.

### Q1.4 NPC-spam cooldown — фиксированный или по NPC?

**Текущая догадка:** 60 sec per `(playerId, npcId)`.

**Вопрос:** Cooldown одинаковый для всех NPC или разный (важные NPC = меньше cooldown)?

| Вариант | Описание |
|---------|----------|
| (a) **60 sec per NPC** | одинаковый для всех |
| (b) **Per-NPC cooldown** | каждый NPC имеет свой `_dialogCooldownSeconds` в NPC definition |
| (c) **Hybrid** | base 60s + per-NPC multiplier |

**Моя рекомендация:** **(a) 60 sec per NPC** в MVP — проще. Phase 2 — per-NPC tuning.

**ОТВЕТ:** здесь не должен быть кулдаун. должен быть per uniq dialog\нажатие или т.п. тоесть когда есть уникальное событие, встретил впервые - даются очки, поговрил на новую тему - которую раньше не жал, даются очки, сделал квест - даются очки и .т.п в такой системе не нужен кулдаун. так как любой кулдаун объодится автокликером.

### Q1.5 Walk threshold — настраиваемый?

**Текущая догадка:** `walkDistanceXpThreshold = 10` (1 XP per 10m walked).

**Вопрос:** Слишком часто/редко?

| Вариант | XP per N meters |
|---------|-----------------|
| (a) **1 XP per 10m** | частый feedback, но XP медленно копится |
| (b) **1 XP per 50m** | менее частый, но XP копится быстрее |
| (c) **1 XP per 100m** | редкий feedback, "исследовательская" прогрессия |

**Моя рекомендация:** **(a) 1 XP per 10m** — частый feedback мотивирует к движению. Параллельно threshold для pilot = 100m (медленнее, потому что корабль = "ленивый" прогресс).

**ОТВЕТ:**во первых - все эти пункты должны быть настраиваемые, сейчас per 1m копится хорошо. также на будущее оставим зацемпу для ачивок и трекеров - типа сколько персонаж прошел всего.

### Q1.6 Pilot XP — считать только пешее управление или boarding?

**Текущая догадка:** XP растёт от `_pilotXpPer100m` пока игрок в `_pilots` set (`ShipController._pilots`).

**Вопрос:** Boarding (вход в корабль) даёт XP или только длительное пилотирование?

| Вариант | Описание |
|---------|----------|
| (a) **Только distance** | boarding = 0 XP, только пилотирование даёт XP |
| (b) **Boarding bonus + distance** | +5 XP при boarding + distance XP |
| (c) **Boarding + disembark bonus** | XP даётся за "активность" (board + distance + disembark) |

**Моя рекомендация:** **(a) только distance** — boarding сам по себе не "intelligence activity". Player выбирает летать ради исследования/контента, XP — побочный эффект.

**ОТВЕТ:**только пилотирование, так как в будущем будет реализовано нахождение в корабле не только как пилот.

### Q1.7 Stat-bonus от equipment — additive vs multiplicative

**Текущая догадка:** `effective_stat = (base + sum_additive) * (1 + sum_multiplicative)` — оба слота.

**Вопрос:** Нужны оба типа bonus'ов или один?

| Вариант | Описание |
|---------|----------|
| (a) **Additive only** | flat +1 STR per item, простая математика |
| (b) **Multiplicative only** | +10% STR per item, exponential growth |
| (c) **Both (current)** | additive + multiplicative, более гибкая формула |

**Моя рекомендация:** **(c) Both** в MVP — designer имеет больше контроля (helm = +1 STR flat, legendary chestplate = +50% STR ×1.5). Если (a) слишком просто, designer может дать только additive bonuses.

**ОТВЕТ:** С.

---

## 2. Equipment (одежда и модули)

### Q2.1 Что считается "одеждой" vs "модулем"?

**Текущая догадка:** Одежда = wearable (Helmet, Chestplate, Boots). Модули = импланты (Sensor, Processor).

**Вопрос:** Какая граница?

| Вариант | Описание |
|---------|----------|
| (a) **Wearable vs Implant** | clothing = физическое (видно), module = имплант (не видно) |
| (b) **Cosmetic vs Functional** | clothing = cosmetic (только стат-bonus), module = functional (sensor/processor/etc) |
| (c) **Single Equipment system** | всё в EquipmentData, разные слоты, но один UI |

**Моя рекомендация:** **(a) Wearable vs Implant** — чёткая семантика, два отдельных под-таба в UI ("Одежда" / "Модули"). Реюз слоты но разный контент.

**ОТВЕТ:** А. но и то и другое будет видно. 

### Q2.2 Максимальное количество экипированных предметов?

**Текущая догадка:** 13 слотов (10 clothing + 3 module). Каждый слот = max 1 item.

**Вопрос:** Нужны ли Accessory3/4 или Weapon slots — это MVP scope?

| Вариант | Слоты |
|---------|-------|
| (a) **13 слотов (current)** | 10 clothing + 3 module — минимальный набор |
| (b) **15 слотов** | + Accessory3, Accessory4 |
| (c) **11 слотов** | - Accessory2 (только 1 accessory) |

**Моя рекомендация:** **(a) 13 слотов** в MVP. Phase 2 — расширение если нужно.

**ОТВЕТ:** А.

### Q2.3 Required skills — soft or hard requirement?

**Текущая догадка:** hard requirement — если навык не изучен, equip deny с reason.

**Вопрос:** Hard requirement или soft (warning)?

| Вариант | Описание |
|---------|----------|
| (a) **Hard requirement** (current) | нельзя надеть без навыка |
| (b) **Soft requirement** | можно надеть, но stat-bonus = 50% |
| (c) **Both** | hard для уникальных предметов, soft для обычных |

**Моя рекомендация:** **(a) Hard requirement** в MVP — проще для игрока (чёткая причина отказа). Phase 2 — soft requirement для редких предметов.

**ОТВЕТ:** С.

### Q2.4 Stat-bonus при unequip — мгновенный или gradual?

**Текущая догадка:** unequip → recompute stats → snapshot sync → мгновенно.

**Вопрос:** Анимация unequip или мгновенное обновление?

| Вариант | Описание |
|---------|----------|
| (a) **Мгновенно** | snapshot приходит сразу после unequip RPC |
| (b) **С задержкой 1 sec** | визуальная "cooldown" unequip |
| (c) **Без изменений** | пока не сделать, потом решить |

**Моя рекомендация:** **(a) мгновенно** в MVP. Phase 2 — анимация unequip (например, модель персонажа "снимает" предмет).

**ОТВЕТ:** а.

### Q2.5 Модули — персонажные или корабельные?

**Текущая догадка:** Модули = **персонажные импланты** (как в Cyberpunk). Корабельные модули уже через `ShipController.modules[]`.

**Вопрос:** Какая семантика?

| Вариант | Описание |
|---------|----------|
| (a) **Персонажные импланты** (current) | модули экипируются в CharacterWindow, дают бонусы игроку |
| (b) **Корабельные** | модули = апгрейды корабля, отдельная система |
| (c) **Оба** | персонажные + корабельные модули в разных подсистемах |

**Моя рекомендация:** **(a) персонажные импланты** в MVP. Корабельные модули — отдельная подсистема (если будет — отдельный roadmap).

**ОТВЕТ:** а.

---

## 3. Skills (навыки, ноды, social/combat)

### Q3.1 Какие навыки в MVP?

**Текущая догадка:** 8 навыков (4 combat + 4 social) — примеры из `06_SKILL_TREE.md §1.3`.

**Вопрос:** Подходят ли эти навыки или нужны другие?

| Combat (4) | Social (4) |
|------------|-----------|
| BasicStrike (+2 STR) | BasicTalk (+2 INT) |
| DodgeRoll (+3 DEX) | Barter (+3 INT, ×0.95 market) |
| HeavySwing (+5 STR ×1.2, requires BasicStrike) | Persuasion (+10% dialog XP, requires BasicTalk) |
| PrecisionStrike (+5 DEX ×1.3, requires DodgeRoll+HeavySwing) | Leadership (recruit_npc ability, requires Barter+Persuasion) |

**Вопросы:**
- (a) **Эти 8 навыков** — базовый набор, проверить работоспособность системы
- (b) **Другие 8 навыков** — какие именно? (требует списка от тебя)
- (c) **Больше (12-16)** — расширенный MVP для демонстрации разнообразия

**Моя рекомендация:** **(a) эти 8** в MVP. После M3 (T-P14) видно работает ли система — тогда решаем добавлять ли ещё.

**ОТВЕТ:**просто проверить работу системы 8 достаточно, главное деревья и зависимость 1 навык от другого.

### Q3.2 Стартовые навыки — какие?

**Текущая догадка:** 3 стартовых (BasicStrike, DodgeRoll, BasicTalk) — все с XP cost = 0.

**Вопрос:** Какие стартовые навыки?

| Вариант | Стартовые |
|---------|-----------|
| (a) **3 starter** (current) | BasicStrike, DodgeRoll, BasicTalk |
| (b) **Никаких** | игрок начинает с 0 навыков, учит все сам |
| (c) **5 starter** | + Persuasion, + Barter |
| (d) **По выбору игрока** | при создании персонажа выбирает 2-3 навыка |

**Моя рекомендация:** **(a) 3 starter** — даёт немедленный gameplay feel, не overwhelming.

**ОТВЕТ:** b. 

### Q3.3 Skill XP cost — с балансом или все одинаковые?

**Текущая догадка:** 0/100/200 XP для starter/intermediate/advanced навыков.

**Вопрос:** Подходит ли эта шкала?

| Вариант | Шкала |
|---------|-------|
| (a) **0/100/200** (current) | линейная прогрессия стоимости |
| (b) **0/50/150/300** | быстрее средние, медленнее продвинутые |
| (c) **0/200/500** | средние дороже, продвинутые очень дорогие |

**Моя рекомендация:** **(a) 0/100/200** — простая линейная шкала. Если окажется слишком быстро/медленно — подкрутить.

**ОТВЕТ:** a. но также - это не должно быть в хардкоде. нужно настраиваемое.

### Q3.4 Забывание навыков — поддерживать?

**Текущая догадка:** skills permanent (нет forget). Возможно Phase 2.

**Вопрос:** Respec / forget нужен в MVP?

| Вариант | Описание |
|---------|----------|
| (a) **Permanent** (current) | нет forget, MVP проще |
| (b) **Respec за credits** | можно забыть навык за N credits |
| (c) **Free respec** | в любой момент, но теряешь XP spent |

**Моя рекомендация:** **(a) permanent** в MVP. Если пользователь попросит — добавим в Phase 2.

**ОТВЕТ:** в любой момент  без потерь. 

### Q3.5 Skill prerequisites — DAG или tree?

**Текущая догадка:** DAG (DodgeRoll + HeavySwing → PrecisionStrike). Несколько parents OK.

**Вопрос:** Структура prerequisite-графа?

| Вариант | Описание |
|---------|----------|
| (a) **DAG** (current) | несколько parents, diamond shape OK |
| (b) **Pure tree** | каждый навык имеет только 1 parent (linear progression) |
| (c) **Mixed** | tree внутри category, cross-category = нет prereq |

**Моя рекомендация:** **(a) DAG** — больше flexibility для designer'а, не блокирует продвинутые навыки одним линейным путём.

**ОТВЕТ:** в идеале сделать нодовую систему. чтобы можно было 1 навык соединять с другим и получать усиление. но это можно запланировать на фазу 2 (сильно позже) а в этой реализовать а.

### Q3.6 Skill tree UI — MVP список или Painter2D graph?

**Текущая догадка:** MVP = список с prerequisite-arrows (ListView, без визуального графа).

**Вопрос:** Нужен ли Painter2D graph в MVP?

| Вариант | Трудозатраты |
|---------|-------------|
| (a) **List + arrows** (current) | ~1 сессия (T-P14) |
| (b) **Painter2D graph** | ~2 сессии (T-P14 + T-P19) |

**Моя рекомендация:** **(a) List + arrows** в MVP. Phase 2 (T-P19) — Painter2D graph если пользователь попросит.

**ОТВЕТ:** сразу с графом. нужно посмотреть насколько в игре это комофртно даже на мвп этапе.

---

## 4. UI (CharacterWindow расширение)

### Q4.1 Tab placement — nested sub-tabs или sidebar?

**Текущая догадка:** nested sub-tabs под "ПРОГРЕССИЯ" (1 top-level + 4 sub-tabs).

**Вопрос:** Подходит ли nested layout?

| Вариант | Описание |
|---------|----------|
| (a) **Nested sub-tabs** (current) | 7 top-level + 4 sub внутри "ПРОГРЕССИЯ" |
| (b) **Top-level 9 tabs** | без вложенности, оборачивается на 2 ряда |
| (c) **Sidebar** | вертикальное меню слева, контент справа |
| (d) **DropdownField** | "Раздел:" + контент |

**Моя рекомендация:** **(a) Nested sub-tabs** — соответствует `CharacterWindow.cs:636-706` pattern (SwitchTab).

**ОТВЕТ:** а. проверим будет ли удобно. если что потом другие.

### Q4.2 Progress bar visual — вариант C (tier + fill + value)?

**Текущая догадка:** `[Tier-N] Сила: 7.3 [████████░░] 73%` — комбинация вариантов (variants из RPG-канона).

**Вопрос:** Подходит ли этот вариант?

| Вариант | Описание |
|---------|----------|
| (a) **Tier + fill + value** (current) | `[Tier 3] Сила: 7.3 [█████░░░] 73% (730/1000 XP)` |
| (b) **Fill + value** | `Сила: 7.3 [█████░░░] 73%` |
| (c) **Tier only** | `[Tier 3]` без fill (минимализм) |
| (d) **Fill only** | `[████████░░]` без чисел |

**Моя рекомендация:** **(a) Tier + fill + value** — максимум информации. Phase 2 — настройки UI если слишком cluttered.

**ОТВЕТ:**b достаточно, тиры ни к чему.

### Q4.3 Tier color coding — по порогам или per-category?

**Текущая догадка:** color по порогам: tier 0-4 = gray (low), 5-9 = blue (mid), 10-14 = orange (high), 15+ = pink (master).

**Вопрос:** Подходит ли эта схема?

| Вариант | Описание |
|---------|----------|
| (a) **По порогам** (current) | tier 0-4 gray, 5-9 blue, 10-14 orange, 15+ pink |
| (b) **Per-category** | STR = red, DEX = green, INT = blue |
| (c) **Gradient (continuous)** | плавный переход цвета по tier |

**Моя рекомендация:** **(a) по порогам** — визуально сигнализирует "высокий уровень". Phase 2 — добавить category tint если попросят.

**ОТВЕТ:**b. но можно делать свечение в зависимости от уровня(очков) чем больше тем ярче или от бледного цвета к более яркому +свечение.

### Q4.4 Tier-up notification — toast или inline animation?

**Текущая догадка:** progress bar плавно обновляется (transition-duration 0.3s). Нет toast/flash.

**Вопрос:** Нужен ли tier-up toast/flash в MVP?

| Вариант | Описание |
|---------|----------|
| (a) **Без toast** (current) | progress bar обновляется плавно |
| (b) **Toast** | "Тир 5 достигнут!" (используем QuestToast pattern) |
| (c) **Inline animation** | bar fill flash gold + label pulse |

**Моя рекомендация:** **(a) без toast** в MVP. Phase 2 — добавить toast если пользователь попросит (копия QuestToast pattern).

**ОТВЕТ:**мне все 3 нравятся. давай их реализовывать комплексно.

### Q4.5 Skill row action button — "ИЗУЧИТЬ" vs drag-drop?

**Текущая догадка:** кнопка "ИЗУЧИТЬ" на каждой row (по pattern existing buttons в CharacterWindow).

**Вопрос:** Нужен ли drag-drop в MVP?

| Вариант | Описание |
|---------|----------|
| (a) **Button** (current) | простой click для learn |
| (b) **Drag-drop** | drag skill в "learned" панель |

**Моя рекомендация:** **(a) button** в MVP. Phase 2 (T-P20) — drag-drop.

**ОТВЕТ:** без панели. наши скилы не будут требовать панели. игра будет вообще без панели навыков и тд. достаточно кнопок.

---

## 5. Persistence (save / load)

### Q5.1 Save format — JSON или binary?

**Текущая догадка:** JSON через `JsonUtility` (копия `JsonInventoryRepository`).

**Вопрос:** Подходит ли JSON?

| Вариант | Описание |
|---------|----------|
| (a) **JSON** (current) | читаемый, easy debug, медленнее |
| (b) **Binary** | компактнее, быстрее, hard to debug |

**Моя рекомендация:** **(a) JSON** в MVP — соответствует существующему паттерну.

**ОТВЕТ:** а. у нас уже есть сохранение инвентаря и квестпрогресса. 

### Q5.2 Atomic write — tmp + Move или direct?

**Текущая догадка:** `tmp + Move` pattern из `JsonQuestStateRepository.cs:74-85`.

**Вопрос:** Подходит ли этот pattern?

| Вариант | Описание |
|---------|----------|
| (a) **tmp + Move** (current) | atomic, no partial write |
| (b) **Direct WriteAllText** | проще, но risk of partial write |

**Моя рекомендация:** **(a) tmp + Move** — копия quest pattern (battle-tested).

**ОТВЕТ:** не знаю. делаем по общему подходжу без костылей.

### Q5.3 Save триггеры — какие?

**Текущая догадка:** OnNetworkDespawn + OnClientDisconnect + periodic (5 min).

**Вопрос:** Достаточно ли?

| Триггер | Когда |
|---------|-------|
| (a) **OnNetworkDespawn** | server shutdown |
| (b) **OnClientDisconnect** | player leave |
| (c) **Periodic 5 min** | safety net |
| (d) **After tier-up** | "major event" |
| (e) **After large XP gain (>10 XP)** | safety net |

**Моя рекомендация:** **(a) + (b) + (c)** в MVP. (d) + (e) — Phase 2 если будет проблема с потерей XP.

**ОТВЕТ:** a b c - как ты предложил достаточно.

### Q5.4 Load триггеры — какие?

**Текущая догадка:** OnClientConnected (player rejoins).

**Вопрос:** Подходит?

| Вариант | Описание |
|---------|----------|
| (a) **OnClientConnected** (current) | load при connect |
| (b) **Manual load** | админ может загрузить save по команде |

**Моя рекомендация:** **(a) OnClientConnected** в MVP.

**ОТВЕТ:** а - достаточно.

---

## 6. Placeholder data — сколько итераций?

### Q6.1 Сколько placeholder-итераций до полноценного сервера?

**Текущая догадка:** M1 (Stats core) — сразу работающий сервер. Placeholder только для UI в MVP.

**Вопрос:** Подходит ли такой подход?

| Вариант | Описание |
|---------|----------|
| (a) **M1 = working server** (current) | сразу серверный код, без placeholder |
| (b) **M1 = placeholder UI, M2-M3 = server** | сначала UI с fake data, потом сервер |
| (c) **M1 = server core, M2-M3 = UI polish** | сервер + базовый UI, UI polish в M4 |

**Моя рекомендация:** **(a) M1 = working server** — проще, чем потом рефакторить.

**ОТВЕТ:** сразу все. а.

### Q6.2 Стартовый StatsConfig — какие значения по умолчанию?

**Текущая догадка:** `StatsConfig_Default.asset` с reasonable defaults (mining = 1 XP per item, walk = 1 XP per 10m, dialog cooldown = 60 sec).

**Вопрос:** Эти значения подходят для тестирования?

| Параметр | Default | Альтернатива |
|----------|---------|--------------|
| `miningXpPerItem` | 1 | 5 (быстрее feedback) |
| `walkXpPer10m` | 1 | 0.5 (медленнее) |
| `tierBaseXp` | 100 | 50 (быстрее tier-up) |
| `tierGrowthRate` | 1.5 | 2.0 (быстрее cap) |
| `globalMultiplier` | 1.0 | — |

**Моя рекомендация:** **current defaults** — тестировщик может менять `_globalMultiplier` для ускорения.

**ОТВЕТ:** уже писал. подходят но они не должны бытьь в хардкоде. все настраиваемо + глобальный мультипликатор для тестирования\ивентов (включение х10 событие и тп)

---

## 7. Out-of-scope (явно НЕ делаем в MVP)

### Q7.1 Что точно не входит?

Из спецификации пользователя и design-doc:

- ❌ **Уровни и опыт персонажа** — игра без уровней, нет level/XP системы (только Stats)
- ❌ **Внешний вид одежды** — отображение внешности = Phase 2 (позднее)
- ❌ **Полная боевая система** — STR/DEX/INT влияют на будущее оружие (Phase 2)
- ❌ **Диалоговое влияние** — INT от диалогов в MVP (anti-spam), но реплики NPC не меняются по INT (Phase 2)
- ❌ **Crafting bonuses** — INT даёт XP от crafting, но не изменяет crafting success rate (Phase 2)
- ❌ **Player visual feedback** — pulse/scale при tier-up — Phase 2
- ❌ **Respec/forget skill** — skills permanent в MVP
- ❌ **Anti-cheat** — dedicated server = trusted
- ❌ **Seasonal reset** — XP/equipment не сбрасываются
- ❌ **Save format migration** — добавлять слоты = ломает совместимость

**Подтверди:** эти пункты НЕ в MVP?

**ОТВЕТ:**все верно.

---

## 8. Приоритеты — что делаем первым?

### Q8.1 Если бы остался только 1 milestone, какой?

**Вопрос:** Если время ограничено, какой milestone самый важный?

| Milestone | Что даёт |
|-----------|----------|
| M1 (Stats core) | базовая progression система, mining/crafting/dialog → stat growth |
| M2 (Clothing & Modules) | equip/unequip, stat-bonuses, visible items |
| M3 (Skill Tree) | навыки, prerequisites, tree structure |
| M4 (UI Integration) | CharacterWindow таб "ПРОГРЕССИЯ" с sub-tabs |

**Моя рекомендация:** **M1** (Stats core) — фундамент. Без него M2-M4 не имеют смысла (нет stat-bonus без stats).

**ОТВЕТ:**все по порядку. не будем делать куски. пока не доделаем. другие части игры не будут разрабатываться. главное правило - делаем без костылей.

### Q8.2 Если бы можно было разделить M4 на части?

**Вопрос:** M4 (UI Integration) — что важнее?

| Sub-task | Что даёт |
|----------|----------|
| Stats UI (T-P16) | видим прогресс Сила/Ловкость/Интеллект |
| Clothing/Modules UI (T-P17) | видим экипированные предметы + unequip |
| Skill UI (T-P14 в M3) | видим навыки + learn |

**Моя рекомендация:** **Stats UI** первым — это фидбек для игрока, что система работает.

**ОТВЕТ:** деление по приоритетам - только если нужно. шлавное делаем без костылей.

---

## 9. Cross-cutting concerns

### Q9.1 Переводы / локализация?

**Текущая догадка:** display names на русском (hardcoded в SO или localization keys).

**Вопрос:** Нужна ли локализация в MVP?

| Вариант | Описание |
|---------|----------|
| (a) **Hardcoded Russian** | display names в .asset = русский текст |
| (b) **Localization keys** | SO хранит key, runtime lookup |
| (c) **Both** | default Russian + optional English |

**Моя рекомендация:** **(a) hardcoded Russian** в MVP — проект пока не имеет localization системы. Phase 2 — добавить если попросят.

**ОТВЕТ:** локализация будет. пока можно а. b - откладываем на момент локализации.

### Q9.2 Тестирование — automated или manual?

**Текущая догадка:** user runs Play Mode tests manually (AGENTS.md preference).

**Вопрос:** Нужны ли EditMode/PlayMode tests?

| Вариант | Описание |
|---------|----------|
| (a) **Manual** (current) | user проверяет в Editor |
| (b) **EditMode tests** | NUnit tests для чистой логики (формула tier, XP gain) |
| (c) **PlayMode tests** | UnityTest для integration |

**Моя рекомендация:** **(a) Manual** — соответствует AGENTS.md + project convention. Phase 2 — добавить tests если будет регрессия.

**ОТВЕТ:** полностью провожу все я. ты настраиваешь код, bootstrap загрузочную сцену и игровую world_0_0 мир если нужно через nity-mcp . даешь мне отчет и что проверять - я проверяю.

### Q9.3 Production code organization — где лежит код?

**Текущая догадка:**
- `Assets/_Project/Scripts/Stats/`
- `Assets/_Project/Scripts/Equipment/`
- `Assets/_Project/Scripts/Skills/`
- `Assets/_Project/Resources/Stats/`
- `Assets/_Project/Resources/Items/Clothing/`
- `Assets/_Project/Resources/Items/Modules/`
- `Assets/_Project/Resources/Skills/`

**Вопрос:** Подходит ли структура?

| Вариант | Описание |
|---------|----------|
| (a) **Per-subsystem folders** (current) | чёткое разделение |
| (b) **Single `Character/` folder** | всё в одном месте |
| (c) **Hybrid** | `Character/Stats/`, `Character/Equipment/`, `Character/Skills/` |

**Моя рекомендация:** **(a) Per-subsystem folders** — соответствует существующему pattern (`Trade/`, `Quests/`, `MetaRequirement/`, `ResourceNode/`).

**ОТВЕТ:** тут твой анализ сабагентами, я ничего не перемещаю.

---

## 10. Открытые вопросы для следующей сессии

### Q10.1 Какая формула геометрического роста?

**Текущая догадка:** `XP_for_next_tier = baseXp * (growthRate ^ currentTier)`.

**Возможные варианты:**
- (a) `baseXp * growthRate^tier` (current)
- (b) `baseXp * tier * growthRate^tier` (linear+geometric)
- (c) `tier^2 * baseXp` (quadratic)
- (d) `baseXp + tier * stepXp` (linear, NOT geometric)

**Моя рекомендация:** **(a) classic geometric** — соответствует "тут скрыта стандартная лвл система".

**ОТВЕТ:** а подойдет

### Q10.2 Что считается "тяжёлым оружием" для STR XP?

**Текущая догадка:** в MVP STR растёт только от mining. Оружие — Phase 2.

**Вопрос:** STR также от "использования тяжёлого оружия" (позднее) — какие системы учитывать?

| Вариант | Описание |
|---------|----------|
| (a) **Только mining в MVP** | оружие = Phase 2, отдельный event |
| (b) **Mining + weapon usage** | два источника STR сразу |
| (c) **Weapon placeholder** | mining пока = "оружие proxy" |

**Моя рекомендация:** **(a) только mining в MVP** — оружие отдельная подсистема (когда будет).

**ОТВЕТ:** а. задокументировать вход для оружейной системы - не более.

### Q10.3 Mining = "тяжёлая работа" = STR по умолчанию?

**Текущая догадка:** mining → STR. Но если игрок фармит руду для крафта (INT-активность)?

**Вопрос:** Mapping конфигурируемый или hardcoded?

| Вариант | Описание |
|---------|----------|
| (a) **Configurable** (current) | `StatsConfig._miningTarget` field, designer changes |
| (b) **Hardcoded mining → STR** | всегда STR |
| (c) **Per-resource mapping** | IronOre → STR, CrystalDust → INT (per-item) |

**Моя рекомендация:** **(a) Configurable** в MVP. Designer может A/B testить mappings. Phase 2 — per-resource если попросят.

**ОТВЕТ:** нет майнинг - это str тут не нужна вариативность.

### Q10.4 Как назвать глобальный множитель в UI?

**Текущая догадка:** поле `globalMultiplier` — только в инспекторе StatsConfig.

**Вопрос:** Показывать ли значение в UI?

| Вариант | Описание |
|---------|----------|
| (a) **Скрыт** (current) | только в инспекторе |
| (b) **"Season bonus" в UI** | player видит "Double XP event active!" |
| (c) **Debug HUD** | только в debug-режиме |

**Моя рекомендация:** **(a) Скрыт** в MVP — это debug tool для тестировщика, не player-facing.

**ОТВЕТ:** а.

### Q10.5 Когда добавлять новые события в WorldEventBus?

**Текущая догадка:** добавляем в M1 (T-P05).

**Вопрос:** Не сломает ли это существующие subscribers?

| Source | Files |
|--------|-------|
| `GatheringServer.cs:159` | +1 `WorldEventBus.Publish<MiningCompletedEvent>` |
| `CraftingServer.cs:86-103` | +1 `WorldEventBus.Publish<CraftingCompletedEvent>` |
| `ExchangeServer.cs:154,214` | +2 |
| `MarketServer.cs:134,150` | +2 |
| `QuestServer.cs:455` | +2 |

**Вопрос:** все 8 минимальных правок в одном тикете (T-P05) или по одному?

| Вариант | Описание |
|---------|----------|
| (a) **Все в T-P05** (current) | один тикет, ~8 правок по 1 строке |
| (b) **По одному** | 8 тикетов, T-P05a..T-P05h |
| (c) **Batched в T-P05** + verify checklist | один тикет + comprehensive verify |

**Моя рекомендация:** **(a) все в T-P05** — минимальное изменение (5 строк), легко проверить через git diff.

**ОТВЕТ:** все 8 в одном. с возможностью дебага вкл\выкл в инпекторе.

---

## Что дальше?

**После твоих ответов:**

1. Обновлю `03_DATA_MODEL.md`, `04_STATS_PROGRESSION.md`, `05_CLOTHING_AND_MODULES.md`, `06_SKILL_TREE.md` соответственно
2. Скорректирую roadmap `08_ROADMAP.md` если нужно (добавить/убрать тикеты)
3. Подготовлю verify checklist для T-P01 (первый тикет кодинга)

**Если не ответишь на все вопросы — использую default'ы** из дизайн-документа (отмечены **(current)** в каждой Q). Если что-то критично — попрошу пересмотра.

---

## Сводка — самые критичные ответы (топ-5)

1. **Q1.1 Стартовые значения** (0/0/0 vs другие)
2. **Q2.5 Модули — персонажные или корабельные** (двусмысленность в спеке)
3. **Q3.1 Какие навыки в MVP** (8 предложенных или другие)
4. **Q3.2 Стартовые навыки** (3 или другие)
5. **Q7.1 Out-of-scope подтверждение** (точно ли НЕ в MVP)

Остальные Q имеют разумные defaults, которые я могу использовать.
