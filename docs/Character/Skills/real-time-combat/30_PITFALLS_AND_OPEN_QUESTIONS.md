# Pitfalls & Open Questions — Real-Time Combat Engine

> **Дата:** 2026-06-25 (v0.3 — после ответов пользователя, новый sequencing)
> **Pitfalls** — TB-специфичные (пешие + ship) — антипаттерны для движка.
> **Open Questions** — вопросы для решения. После ответов → design-doc обновится.

---

## 1. Pitfalls (антипаттерны)

### 1.1 Damage-формула без hitLocation — спорно с дизайнером

**Сценарий:** в TB hitLocation = ×0.5/1/2 (Limbs/Torso/Head). В real-time = ×1.0 (отключён, 2.17). Дизайнер может спросить: "почему в TB есть hitLocation, а в real-time нет?"

**Решение:** **объяснить** в дизайн-доке, что hitLocation требует **визуальной обратной связи** (анимация попадания в голову). В real-time такой визуальной обратной связи **нет** (или слишком быстрая), поэтому hitLocation = декоративный множитель. **В Phase 3**, если добавим slow-motion при попадании в голову, hitLocation можно включить.

**Вердикт:** задокументировано. Дизайнер принимает.

### 1.2 hitChance формула — tuning в Playtest

**Сценарий:** базовая hitChance = 0.85, `dexMod = 0.7 + (DEX - 10) * 0.03`. На DEX 10 → hitChance = 0.595. Это **низковато** (игрок промахивается 40% времени).

**Решение:** CombatConfig.baseMeleeHitChance = 0.95 (default), dexHitMultiplier = 0.02. На DEX 10 → 0.95 * (0.7 + 0) = 0.665. **Лучше, но всё ещё <70%**.

**Альтернатива:** `dexMod = 0.85 + (DEX - 10) * 0.015`. На DEX 10 → 0.85. hitChance = 0.85 * 0.95 = 0.81. **Лучше**.

**Вердикт:** tuning в Playtest. CombatConfig даёт designer-контроль.

### 1.3 SkillMult без cap (2.18) — over-stacking

**Сценарий:** игрок учит ВСЕ Combat-навыки (4 generic + 8 Melee + 6 Ranged + 5 Explosives + 6 Antigrav + 6 Defense = 35). Из них StatMod-с-multiplier: HeavySwing ×1.2, PrecisionStrike ×1.3, GreatSword ×1.15, **AimedShot ×1.0**, **MasterDefender ×1.2**. Если взять **максимум** — `mult = 1.2 × 1.3 × 1.15 × 1.2 = 2.15`. С Head (×2) + Crit (×2) → **×8.6 финального множителя**. **Overkill** в PvE.

**Решение:** пользователь явно сказал «без ограничений» (2.18). **Design-time balance**: designer-ы создают навыки так, чтобы **разумный игрок** (3-4 навыка по своему стилю) имел `mult ~1.2-1.5`. **Min-maxer** (все 35) получает `mult ~2.0+`, но это reward для dedication.

**Вердикт:** **принимаем** per 2.18. **Задокументировано** как design-choice.

### 1.4 Ship combat адаптация ломает CombatServer

**Сценарий:** Phase 3, добавляем `ShipAttacker`. CombatServer приходится менять (например, проверять `attacker is PlayerAttacker || attacker is ShipAttacker`).

**Решение:** **anti-restrictive design**: `IAttacker` interface. `CombatServer` работает через `attacker.GetDamageSource(sourceId)`, **не** через `attacker is PlayerAttacker`. **0 изменений** в `CombatServer` для ship combat.

**Вердикт:** задокументировано в `10_DESIGN.md §10`.

### 1.5 Default damage source без ERPR-полей (до T-CB03)

**Сценарий:** T-RTC01..T-RTC10 (MVP) работают **до** T-CB03 (WeaponItemData). Движок не может прочитать `weapon.damageDice` — поля ещё нет.

**Решение:** `DefaultDamageSource : IDamageSource` — fallback, **жёстко** зашитые defaults: `d6, base=1, critMod=0, range=2m`. Когда T-CB03 реализован — `WeaponDamageSource` использует реальные поля.

```csharp
public class DefaultDamageSource : IDamageSource {
    public ulong GetSourceId() => 0;
    public DamageType GetDamageType() => DamageType.Physical;
    public DamageDice GetDamageDice() => DamageDice.d6;
    public int GetBaseDamage() => 1;
    public int GetCritModifier() => 0;
    public float GetRange() => 2f;
    // ... defaults
}
```

**Вердикт:** MVP работает на defaults. После T-CB03 — заменяем на `WeaponDamageSource` (с теми же интерфейсами).

### 1.6 NPC-AI не существует

**Сценарий:** T-RTC01..T-RTC10 готовы. NPC-враги (placeholder) — `NpcAttacker` + `NpcTarget`. **Но** NPC-AI (когда атаковать, flee, и т.п.) — **отдельная подсистема**, не наш scope.

**Решение:** `NpcAttacker` имеет **минимальный AI stub** (например, "атакуем ближайшего игрока раз в 3 сек"). Реальный AI — отдельная подсистема.

**Вердикт:** AI stub в T-RTC03, real AI в отдельной подсистеме.

### 1.7 Anti-cheat: target switching на мёртвом NPC

**Сценарий:** игрок нажимает ЛКМ → target уже мёртв (только что убит) → атака "проходит" без видимого feedback.

**Решение:** server проверяет `target.IsAlive()` → `AttackErrorCode.AlreadyDead` → UI: "Target is dead". См. `20_TECHNICAL.md §5.3`.

### 1.8 Anti-cheat: distance spoofing

**Сценарий:** игрок подкручивает позицию, чтобы бить издалека.

**Решение:** все distance checks на **сервере** (`rangePolicy.IsInRange` в `ResolveAttack`). Клиент не может обмануть. См. `20_TECHNICAL.md §5.4`.

### 1.9 Anti-cheat: cooldown bypass

**Сценарий:** игрок спамит атаки быстрее, чем cooldown.

**Решение:** `attacker.CanAttack(source, now)` на сервере. `RateLimit` (10 ops/sec) защищает от RPC-spam. См. `20_TECHNICAL.md §5.5`.

### 1.10 Network lag: client видит "мертвого" NPC, который ещё жив

**Сценарий:** на 200ms ping клиент A убил NPC, но клиент B ещё видит NPC с HP>0.

**Решение:** `NetworkVariable<int> _currentHp` на `NpcTarget` реплицируется NGO автоматически. Клиент B получает обновление через 100-200ms. **Приемлемо для MVP**.

**Phase 2:** client prediction (предсказать HP изменения, скорректировать по server).

### 1.11 Pitfall: rate limit 10 ops/sec слишком жёсткий для авто-атаки

**Сценарий:** если игрок зажимает ЛКМ (auto-attack), 10 ops/sec = 10 атак/сек. Но cooldown меча = 1 сек → 1 атака/сек. Получается 9 из 10 RPC rejected rate-limit'ом.

**Решение:** rate limit = **не** per RPC, **не** per attack. Это **anti-spam** мера. На реальный gameplay не влияет.

**Вердикт:** OK, rate limit работает как expected.

### 1.12 Pitfall: ship turret требует line-of-sight, но `RequiresLineOfSight = false` в MVP

**Сценарий:** Phase 3, ship combat, `ShipRangePolicy.RequiresLineOfSight = true`. Но MVP `MeleeRangePolicy.RequiresLineOfSight = false`. **Inconsistency**.

**Решение:** MVP — `RequiresLineOfSight = false` для всех (нет raycast). Phase 2 — добавить raycast для всех ranged attacks.

**Вердикт:** задокументировано. Phase 2 = LOS для всех.

### 1.13 Pitfall: server tick rate 30 Hz vs NetworkBehaviour update

**Сценарий:** CombatServer наследует `NetworkBehaviour`, у которого есть `Update/FixedUpdate`. Используем `FixedUpdate` (50 Hz default). Это **выше** нашего target (30 Hz).

**Решение:** в `FixedUpdate` — early-out: `if (Time.fixedTime - _lastTick < _serverTickInterval) return;`. Эффективная частота = 30 Hz.

**Вердикт:** OK, fixed-deltaTime даёт 50 Hz, мы используем 30.

### 1.14 Pitfall: scene-placed CombatServer в WorldScene (для ship combat)?

**Сценарий:** `WorldScene_0_0` (24 стриминговых сцен) — `CombatServer` нужно scene-placed в **каждой** сцене? Или **только в BootstrapScene** (как сейчас)?

**Решение:** CombatServer = **singleton, scene-placed в BootstrapScene**, persistent через `DontDestroyOnLoad` (как другие серверы). **Не** нужно scene-placed в каждой `WorldScene_X_Z`.

**Вердикт:** singleton, persistent. См. `20_TECHNICAL.md §1.1`.

### 1.15 Pitfall: NetworkVariable на ShipTarget

**Сценарий:** `ShipTarget._currentHp` — `NetworkVariable<int>`. NGO 2.x реплицирует **все NetworkVariable** на все клиенты. Для корабля (высокий HP) — это 4-8 bytes per update. Приемлемо.

**Решение:** используем `NetworkVariable<int>` для HP. При ship-vs-ship (10 клиентов) — ~100 bytes/sec total. Приемлемо.

**Вердикт:** OK.

### 1.16 Pitfall: RPC target broadcast на 100+ клиентов

**Сценарий:** `BroadcastAttackLanded` отправляет на `RpcTarget.Everyone` (все клиенты). При 100 игроков = 100 RPC per attack. NGO 2.x это делает, но **нагрузка на сервер**.

**Решение Phase 3:** AreaOfInterest (AOI) — broadcast только клиентам в радиусе 200м. **MVP:** `RpcTarget.Everyone` (приемлемо для 100 игроков).

**Вердикт:** MVP = Everyone. Phase 3 = AOI.

### 1.17 Pitfall: ship combat — GDD 20 расхождение

**Сценарий:** GDD 20 описывает **корабельный** Pilot/Merchant/Explorer (50 уровней, 4 стата корабля). v2 character progression — **пехотный** (Сила/Ловкость/Интеллект). **Конфликт**.

**Решение:** **не трогаем GDD 20** (gdd/ read-only). v2 character progression = пехотный. Ship combat = **отдельная подсистема** (T-RTC16..T-RTC20, Phase 3), использует **те же `IAttacker/IDamageTarget`** интерфейсы.

**Вердикт:** задокументировано в `Battle/01_ANALYSIS.md §3.2`.

### 1.18 Pitfall: hitLocation = 0 (Limbs) vs 1 (Torso) vs 2 (Head) — real-time отключён

**Сценарий:** в damage log hitLocation = 1 (Torso, default). Дизайнер может подумать, что hitLocation активен.

**Решение:** в `DamageResult` всегда `hitLocation = 1` (Torso) в real-time. `locMult = 1.0`. Дизайнер видит, что hitLocation не используется. **Явно задокументировано**.

**Вердикт:** OK, документация достаточна.

### 1.19 Pitfall: NPC-враги в `WorldScene_X_Z` — отдельная подсистема, не наш scope

**Сценарий:** `NpcAttacker + NpcTarget` спроектированы в T-RTC03. **Но** спавн NPC-врагов, их AI, фракции — **отдельная подсистема**. Движок **может** работать с NPC-врагами, но **кто их спавнит** — другая задача.

**Решение:** T-RTC03 создаёт **только** `NpcAttacker/NpcTarget` (компоненты). **Не** спавнит NPC. **Placeholder** для тестирования (1 NPC-враг в `WorldScene_0_0` создаётся вручную или через тестовую сцену).

**Вердикт:** двигатель готов, NPC-spawn = другая подсистема.

### 1.20 Pitfall: turn-based-battles/ — parking, не удалять

**Сценарий:** TB-подсистема отложена на неопределённый срок. `turn-based-battles/` — 7 файлов, 155 КБ. **Не удалять** (ЗБТ может пересмотреть приоритеты).

**Решение:** добавить **DISCLAIMER** в `turn-based-battles/00_README.md`: «PARKING — отложено до пересмотра после ЗБТ». Документы остаются как reference.

**Вердикт:** **не удаляем**, **не правим** (кроме DISCLAIMER), **не развиваем**.

### 1.21 Pitfall: race condition registration (RESOLVED v0.1.2)

**Сценарий:** `NetworkPlayer.OnNetworkSpawn` может сработать **раньше** `CombatServer.OnNetworkSpawn` (порядок scene-spawn не гарантирован в NGO 2.x). `RegisterWithCombatServer` пытался зарегистрировать PlayerAttacker/Target в `CombatServer.Instance` — но Instance ещё null. С ранним `return` (v0.0) `AddComponent` пропускался → компонентов нет → push-up `OnNetworkSpawn` не сработает → Player не зарегистрирован.

**Решение (v0.1.1 + v0.1.2):** двухсторонняя защита:
- **Pull-up:** `PlayerAttacker/Target/NpcAttacker/Target : NetworkBehaviour` + `OnNetworkSpawn` override → если `CombatServer.Instance != null` → `Register`.
- **Push-down:** `CombatServer.OnNetworkSpawn → RecoverExistingEntities()` → `FindObjectsByType` всех PlayerAttacker/Target/NpcAttacker/Target → `Register` тех, кого ещё нет.
- **Second-chance:** `CombatServer.OnNetworkSpawn → Invoke(nameof(RecoverExistingEntities), 1.0f)` — для тех, кто spawned'ится позже.

**Также исправлено:** `RecoverExistingEntities` имел `if (id == 0) continue;` для Player — отфильтровывал host player (clientId=0). Убран. Для NPC — оставлен (0 = не инициализирован).

**Вердикт:** РЕШЕНО в v0.1.2. Подробности: `50_IMPL_CHANGELOG.md §v0.1.1, v0.1.2` и `20_TECHNICAL.md §2`.

### 1.22 Pitfall: K-key debug → InvalidSource (RESOLVED v0.1.3)

**Сценарий:** host player без экипированного оружия (WeaponMain/WeaponOff пусты). `PlayerAttacker.RebuildSources` → пустой `_activeSources`. K шлёт `sourceId=0` → `GetDamageSource(0) == null` → `InvalidSource` error.

**Решение (v0.1.3):** `PlayerAttacker.EnsureUnarmedFallback()` — если `_activeSources.Count == 0` после RebuildSources, добавить `DefaultDamageSource(0UL, "Unarmed")`. Debug K-key работает, unarmed combat возможен.

**Вердикт:** РЕШЕНО в v0.1.3. После T-CB03 (WeaponItemData) — `DefaultDamageSource` заменится на `WeaponDamageSource` (id != 0), fallback останется для случая "no weapon equipped".

### 1.23 Pitfall: NPC corpse остаётся видимый (RESOLVED v0.1.4)

**Сценарий:** после `HP=0` (EntityKilled) — `VisualMarker` капсула оставалась видимой. `NpcTarget` не скрывал GameObject.

**Решение (v0.1.4):** `NpcTarget.ApplyDamage` — после `_currentHp.Value = 0` → `Destroy(gameObject, 3.0f)`. 3 сек corpse delay (клиенты видят анимацию/feedback, EntityKilledTargetRpc дойдёт). После — `NetworkObject` NGO-удалится у всех клиентов автоматически.

**Вердикт:** РЕШЕНО в v0.1.4. Подробности: `50_IMPL_CHANGELOG.md §v0.1.4`.

---

## 2. Open Questions (новый раздел для движка)

> **Формат:** каждый раздел — одна область решений. После твоих ответов → обновлю design-doc.
> **Мои рекомендации** отмечены `**РЕК:**`.

### 2.1 HitChance формула — какая именно?

**Текущая догадка:** `hitChance = baseMeleeHitChance × dexMod`, где `dexMod = 0.7 + (DEX - 10) * 0.03`. На DEX 10 → 0.595, на DEX 20 → 0.85.

| Вариант | Что |
|---|---|
| (a) **0.7 + (DEX-10)*0.03** (текущая) | 0.595 на DEX 10, 0.85 на DEX 20 |
| (b) **0.85 + (DEX-10)*0.015** | 0.85 на DEX 10, 0.925 на DEX 20 (мягче) |
| (c) **Линейная**: 0.5 + (DEX/20) | 0.5 на DEX 10, 1.0 на DEX 20 (крутая) |

**РЕК:** **(b)** — мягче, игроку приятнее. Тюнинг в Playtest.

**ответ:** б.

### 2.2 Damage-types при ship combat — те же 5 или отдельный enum?

**Текущая догадка:** те же 5 (Physical/Ballistic/Antigrav/Explosive/Mesium).

| Вариант | Что |
|---|---|
| (a) **Те же 5** (текущая) | один enum, простая формула |
| (b) **Отдельный ShipDamageType** | HullPiercing / Explosive / Energy / Ion |
| (c) **Расширить 5 → 6** | добавить `Energy` (щит-пробивание) |

**РЕК:** **(a) те же 5** — та же физика, нет причин множить. Открыто для game-designer'а.

**ответ:** а.

### 2.3 Cooldown для пешего — где источник истины?

**Текущая догадка:** `CooldownTracker` (server-side, в `CombatServer`).

| Вариант | Что |
|---|---|
| (a) **`CooldownTracker` в `CombatServer`** (текущая) | централизованно, server-authoritative |
| (b) **`CooldownTracker` в `PlayerAttacker`** (per-entity) | децентрализованно, каждый сам считает |
| (c) **В `WeaponDamageSource` (static)** | глобально, не per-entity |

**РЕК:** **(a)** — централизованно, легче дебажить и логировать.

**ответ:** а.

### 2.4 NPC-AI — наш scope или нет?

**Текущая догадка:** **НЕ наш scope**. NPC-AI = отдельная подсистема.

| Вариант | Что |
|---|---|
| (a) **НЕ наш scope** (текущая) | engine готов, NPC-AI отдельно |
| (b) **Минимальный AI в `NpcAttacker`** (агрессия + flee) | сэкономит время, но смешивает concerns |

**РЕК:** **(a)** — separation of concerns. NPC-AI = отдельный тикет T-NPC01+ (другая подсистема).

**ответ:** а.

### 2.5 Ship combat — когда начинать?

**Текущая догадка:** **Phase 3**, после ЗБТ. Hooks (`IAttacker/IDamageTarget/IDamageSource`) уже есть в T-RTC01..T-RTC10.

| Вариант | Что |
|---|---|
| (a) **Phase 3 (после ЗБТ)** (текущая) | пеший MVP → ЗБТ → ship combat |
| (b) **Phase 2 (параллельно с PvP)** | ship combat + PvP-дуэль одновременно |
| (c) **Phase 1 (сразу после движка)** | ship combat = MVP+1 |

**РЕК:** **(a)** — пеший — фундамент, ship combat — расширение. Сначала ЗБТ пешего.

**ответ:** а.

### 2.6 Turn-based — вернёмся?

**Текущая догадка:** **НЕ** в обозримом будущем. Parking.

| Вариант | Что |
|---|---|
| (a) **Parking** (текущая) | не развиваем, ЗБТ может пересмотреть |
| (b) **Вернёмся после ЗБТ** | если ЗБТ покажет, что нужен TB |
| (c) **Удалить** | `turn-based-battles/` удалить из docs/ |

**РЕК:** **(a) parking** — НЕ удалять, не править, не развивать. ЗБТ пересмотрит.

**ответ:** а.

### 2.7 UI damage numbers — когда?

**Текущая догадка:** **Phase 2**, после базового combat.

| Вариант | Что |
|---|---|
| (a) **Phase 2 (отложено)** (текущая) | MVP = без UI, потом добавим |
| (b) **MVP-1 (сразу)** | UI с первого дня |
| (c) **Только для ship combat** | UI нужен только для турелей |

**РЕК:** **(a)** — MVP без UI, фокус на engine. UI в Phase 2.

**ответ:** а.

### 2.8 NetworkVariable для всех HP?

**Текущая догадка:** `NetworkVariable<int> _currentHp` на `PlayerTarget` и `NpcTarget`.

| Вариант | Что |
|---|---|
| (a) **NetworkVariable per entity** (текущая) | авто-репликация, просто |
| (b) **Manual broadcast** (RPC only) | контроль, но сложнее |
| (c) **NetworkVariable для player, RPC для NPC** | hybrid |

**РЕК:** **(a)** — просто, надёжно, NGO 2.x оптимизирует. Для NPC — то же, что для player.

**ответ:**а.

### 2.9 CombatServer в каждой WorldScene или singleton?

**Текущая догадка:** **singleton в BootstrapScene**, persistent через `DontDestroyOnLoad`.

| Вариант | Что |
|---|---|
| (a) **Singleton в Bootstrap** (текущая) | persistent, работает на всех сценах |
| (b) **Scene-placed в каждой `WorldScene_X_Z`** | per-scene, без persistent |
| (c) **Singleton + per-scene** | двойная регистрация, fallback |

**РЕК:** **(a)** — singleton, persistent. Per-scene не нужно.

**ответ:** а.

### 2.10 Как CombatServer узнаёт о PlayerAttacker / NpcAttacker / ShipAttacker?

**Текущая догадка:** `NetworkPlayer.OnNetworkSpawn` вызывает `CombatServer.RegisterAttacker/RegisterTarget`. NPC-враги — отдельная подсистема, тоже регистрирует.

| Вариант | Что |
|---|---|
| (a) **Manual registration в OnNetworkSpawn** (текущая) | explicit, контроль |
| (b) **Auto-discovery через FindObjectsOfType** | ленивый, не контроль |
| (c) **Event-based** (`IAttacker.Awake → CombatServer`) | loose coupling |

**РЕК:** **(a) manual** — explicit, проще дебажить.

**ответ:** а.

### 2.11 Фаза 2: PvP-дуэль flow

**Текущая догадка:** стандартный combat с consent + rewards. **0 специальной защиты** (rate limit + server-authoritative).

| Вариант | Что |
|---|---|
| (a) **Стандартный combat + consent** (текущая) | просто, использует движок как есть |
| (b) **Специальный DuelMode** (отдельный combat-rule) | сбалансированно (×1.5 HP, ×0.8 damage) |
| (c) **Без XP-loss, без permadeath** | проще, для casual |

**РЕК:** **(a)** — стандартный combat. Баланс через DesignerConfig. **Открыто для game-designer'а**.

**ответ:**а.

### 2.12 Что включаем в MVP-1 (T-RTC01..T-RTC10)?

**Текущая догадка:** 9 тикетов (T-RTC01..T-RTC09), T-RTC10 (UI) = Phase 2.

| Тикет | Включить в MVP-1? |
|---|---|
| T-RTC01: core interfaces | ✅ да |
| T-RTC02: PlayerAttacker/Target | ✅ да |
| T-RTC03: NpcAttacker/Target + NpcCombatData | ✅ да |
| T-RTC04: WeaponDamageSource + range policies | ✅ да |
| T-RTC05: DamageCalculator | ✅ да |
| T-RTC06: CombatServer | ✅ да |
| T-RTC07: CombatClientState | ✅ да |
| T-RTC08: NGO RPC + DTO | ✅ да |
| T-RTC09: CombatConfig + 4 WorldEvent | ✅ да |
| T-RTC10: UI (damage numbers, hit flash) | ⏸ Phase 2 |
| **ИТОГО** | **9 тикетов, ~23-32 ч** |

**РЕК:** все 9 в MVP-1, T-RTC10 = Phase 2.

**ответ:** делаем по рекомендации.

### 2.13 Что делаем с PvP-factions (5 Гильдий, враждебные игроки)?

**Текущая догадка:** **отдельная подсистема**, после ЗБТ. **НЕ** в скоупе CombatEngine.

| Вариант | Что |
|---|---|
| (a) **Отдельная подсистема** (текущая) | faction-aware combat, open-world |
| (b) **PvP-дуэль = всё** (1v1 consent) | без faction war |
| (c) **Оба** (Phase 2 + Phase 4) | дуэли сейчас, faction war позже |

**РЕК:** **(c)** — PvP-дуэль (consent) в Phase 2. Faction war (open-world) в Phase 4.

**ответ:** по рекомендации - будем позже делать.

### 2.14 Anti-cheat — насколько строгий?

**Текущая догадка:** server-authoritative, rate limit, distance check, cooldown check. **Нет** сложных проверок (signature, replay, etc.).

| Вариант | Что |
|---|---|
| (a) **Server-authoritative + базовые checks** (текущая) | достаточно для MVP |
| (b) **+ signature на RPC** (anti-tamper) | сложно, нужно HMAC |
| (c) **+ server-side replay** (запись боёв) | Phase 3 |

**РЕК:** **(a)** — базовый, MVP. **Открыто для game-designer'а**.

**ответ:**  а.

### 2.15 Ship combat — turret как отдельный класс или подкласс `WeaponDamageSource`?

**Текущая догадка:** `Turret : IDamageSource` (отдельный класс).

| Вариант | Что |
|---|---|
| (a) **Turret : IDamageSource** (текущая) | чистый, generic |
| (b) **Turret : WeaponDamageSource** (наследование) | переиспользует код, но связывает |
| (c) **TurretData = ScriptableObject + IDamageSource** | композиция |

**РЕК:** **(a)** — Turret = IDamageSource (composition over inheritance). Переиспользует через `DamageCalculator`, не через `WeaponDamageSource`.

**ответ:** а.

### 2.16 Подключение к существующим подсистемам — что нужно изменить?

**Текущая догадка:** только **add-only** в существующем коде.

| Файл | Изменить? |
|---|---|
| `NetworkPlayer.cs` | ➕ add `PlayerAttacker/PlayerTarget` в `OnNetworkSpawn/OnNetworkDespawn` (add-only) |
| `NetworkManagerController.cs` | ➕ add `CreateCombatClientState()` в `Awake()` (add-only) |
| `StatsWorld.cs` (T-P03) | ➖ NO, читаем через `StatsWorld.GetOrCreateStats` |
| `EquipmentWorld.cs` (T-P09) | ➖ NO, читаем через `EquipmentWorld.GetEquipment` |
| `WorldEvent.cs` | ➕ add 4 new event classes (add-only) |
| `SkillsWorld.cs` (T-P12) | ➖ NO, читаем через `SkillsWorld.GetLearnedSkills` (после T-CB01..T-CB09) |
| `ShipController.cs` (Phase 3) | ➕ add `GetCurrentHp/GetArmorHull/RequestTurretFireRpc` (add-only) |

**РЕК:** add-only везде. **0 изменений** в существующих системах.

**ответ:** делаем по рекомендации

---

## 3. Промежуточный сводный список (обновлено v0.3, ответы пользователя)

> Все 16 вопросов имеют ответы пользователя. Зафиксированы в §2 выше. Ниже — финальная таблица решений.

| # | Область | **Ответ пользователя** | Рекомендация (совпала?) | Влияние на design |
|---|---|---|---|---|
| 2.1 | HitChance формула | **б. 0.85 + (DEX-10)*0.015** | ✅ | Меняем `MeleeRangePolicy` и `RangedRangePolicy` — dexMod: `0.85 + (DEX-10)*0.015`. На DEX 10 → 0.85, на DEX 20 → 0.925. `hitChance = 0.85 × 0.85 = 0.81`. |
| 2.2 | ShipDamageType | **а. те же 5** | ✅ | `DamageType` единый enum для пешего и корабельного. |
| 2.3 | Cooldown tracker | **а. в CombatServer** | ✅ | `CooldownTracker` централизованно в `CombatServer`. |
| 2.4 | NPC-AI scope | **а. НЕ наш** | ✅ | AI stub в T-RTC03, full AI — отдельная подсистема. |
| 2.5 | Ship combat когда | **а. Phase 3 (после ЗБТ)** | ✅ | Hooks в T-RTC01..T-RTC10, реализация в T-RTC16..T-RTC20. |
| 2.6 | Turn-based | **а. parking** | ✅ | Не удаляем, не развиваем. |
| 2.7 | UI damage numbers | **а. Phase 2** | ✅ | T-RTC10 отложен. MVP без UI. |
| 2.8 | NetworkVariable HP | **а. per entity** | ✅ | ✅ для PlayerTarget + NpcTarget + ShipTarget. |
| 2.9 | CombatServer placement | **а. singleton Bootstrap** | ✅ | Persistent через DontDestroyOnLoad. |
| 2.10 | Registration | **а. manual OnNetworkSpawn** | ✅ | Explicit, проще дебажить. |
| 2.11 | PvP-duel flow | **а. стандартный combat** | ✅ | Без спец. DuelMode. Баланс через DesignerConfig. |
| 2.12 | MVP тикеты | **9 (T-RTC01..T-RTC09)** | ✅ | По рекомендации. |
| 2.13 | PvP-factions | **по рекомендации — позже** | ✅ | Дуэли Phase 2, faction war Phase 4. |
| 2.14 | Anti-cheat | **а. server-authoritative + базовые checks** | ✅ | MVP без сложных проверок. |
| 2.15 | Turret class | **а. Turret : IDamageSource** | ✅ | Composition over inheritance. |
| 2.16 | Подключение к существующему | **add-only везде** | ✅ | 0 refactor существующего кода. |

**Ключевое изменение дизайна после ответов:**
- **HitChance формула** — меняем в `MeleeRangePolicy` / `RangedRangePolicy` на `dexMod = 0.85 + (DEX - 10) * 0.015`. Базовая hitChance = `0.85 * dexMod`. На DEX 10 → 0.81. На DEX 20 → 0.92.

**Итоговый sequencing (подтверждён):**
1. **T-RTC01..T-RTC09** — Real-Time Combat Engine (MVP, ~23-32 ч).
2. **T-CB01..T-CB09** — навыки (MVP+1, ~16-21 ч).
3. **T-RTC11..T-RTC15** — PvP-дуэль (Phase 2, ~15-20 ч).
4. **T-RTC16..T-RTC20** — ship combat (Phase 3, ~25-33 ч).
5. **T-TB01..T-TB14** — turn-based (PARKING, ~46 ч).

---

## 4. Что НЕ обсуждаем (вне scope)

- ❌ Turn-based battles (parking, `turn-based-battles/`).
- ❌ NPC-AI для open world (отдельная подсистема).
- ❌ VFX, sound, animations (3D/audio отделы).
- ❌ UI (damage numbers) — T-RTC10 Phase 2.
- ❌ PvP-faction war (Phase 4).
- ❌ GDD 20 (корабельный Pilot/Merchant/Explorer, не наш scope).
- ❌ Magic / spells (lore запрет).
- ❌ Real-time CombatEngine НЕ пишем код в этой сессии.
- ❌ Real-time CombatEngine НЕ редактирует существующий код (add-only).
- ❌ Real-time CombatEngine НЕ включает NPC-AI, VFX, sound, animations.
