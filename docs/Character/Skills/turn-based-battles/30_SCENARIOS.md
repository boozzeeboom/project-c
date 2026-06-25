# Scenarios — PvE-данж, PvP-дуэль, фракционные ивенты, boss-enкаунтеры

> **Дата:** 2026-06-25
> **Базируется на:** `10_DESIGN.md` (архитектура), `20_TECHNICAL.md` (NGO RPC), `Battle/ERPR_collaboration.md` (формула)
> **5 сценариев:** PvE-соло-данж (§1), PvE-кооп-данж (§2), PvP-дуэль 1v1 (§3), Boss-enкаунтер (§4), Фракционный ивент (§5).

---

## 1. PvE-соло-данж (основной use case)

### 1.1 Концепция

**Самый частый TB-сценарий.** Игрок находит вход в подземелье/руины, входит → бой 1v1 / 1v2 / 1v3 на сетке 10x10 → победа/поражение → лут.

**Целевая аудитория:** все игроки, соло-PvE-контент.

**Сложность:** RANK 1-10 (RANK 10 = босс-уровень).

### 1.2 DungeonConfig (SO)

**Файл:** `Assets/_Project/Scripts/TurnBased/Config/DungeonConfig.cs`
**Namespace:** `ProjectC.TurnBased.Config`
**Path:** `Resources/Dungeons/GoblinRuins_Rank1.asset`

```csharp
[CreateAssetMenu(fileName = "Dungeon_", menuName = "Project C/TurnBased/Dungeon Config")]
public class DungeonConfig : ScriptableObject {
    [Header("Identity")]
    public string dungeonId;          // "goblin_ruins_rank1"
    public string displayName;        // "Гоблинские руины (Ранг 1)"
    public string description;
    public Sprite icon;

    [Header("Grid")]
    [Range(6, 16)] public int gridWidth = 10;
    [Range(6, 16)] public int gridHeight = 10;

    [Header("NPC Spawns")]
    public NpcSpawnConfig[] npcSpawns;  // 1-5 NPC

    [Header("Loot (при победе)")]
    public LootDropConfig[] lootTable;

    [Header("Rewards")]
    public int xpReward = 50;        // XP за победу (бонус к mining/crafting/etc.)
    public int creditsReward = 100;
    public int rankPoints = 1;       // для гильдейского рейтинга

    [Header("Penalty (при поражении)")]
    [Range(0f, 1f)] public float xpLossPercent = 0.20f;  // 20% по умолчанию

    [Header("Zone (для триггера)")]
    public string zoneName;          // "GoblinRuins_Zone_01"
    public Vector3 zoneCenter;       // world position
    public float zoneRadius = 5f;

    [Header("Visual")]
    public Color themeColor = new Color(0.6f, 0.4f, 0.2f);
    public GameObject dungeonPrefab;  // опционально: префаб для визуала
}

[Serializable]
public struct NpcSpawnConfig {
    public string npcConfigId;       // "goblin_worker"
    public Vector2Int spawnPos;      // (5, 5)
    public int level;                // 1
    public NpcAggression aggression; // Normal
}

[Serializable]
public struct LootDropConfig {
    public string itemId;            // "Item_AntigravCrystal"
    public int minQuantity = 1;
    public int maxQuantity = 1;
    [Range(0f, 1f)] public float dropChance = 1.0f;  // 100%
}
```

### 1.3 Sequence: «Игрок заходит в Goblin Ruins»

**Действия:**

1. **Игрок** подходит к GameObject `[DungeonEntrance_GoblinRuins]` в `WorldScene_X_Z`.
2. Появляется UI-tooltip: «Гоблинские руины (Ранг 1) — Нажмите F для входа».
3. Игрок нажимает F → сервер получает `RequestStartPvEBattleRpc("dungeon_goblin_ruins_rank1")`.
4. **Сервер** валидирует: игрок в зоне (`DungeonConfig.zoneRadius` от `zoneCenter`).
5. Сервер создаёт `TurnBasedBattleInstance`:
   - 10x10 grid (DifficultTerrain в центре, Wall по краям).
   - 1 игрок (spawn = (5, 1), bottom edge).
   - 3 NPC-goblin (spawns из `DungeonConfig.npcSpawns`).
6. Сервер публикует `BattleStartedEvent`, отправляет `BattleStartedTargetRpc` игроку.
7. **Клиент** открывает `TurnBasedBattleWindow`, рисует сетку, подсвечивает игрока.
8. **Сервер** публикует `TurnStartedEvent`, отправляет `TurnStartedTargetRpc`.
9. **Игрок** ходит 3 сек (move + attack + end).
10. NPC-AI: 3 NPC делают ходы.
11. После 3-5 раундов → бой заканчивается → `BattleEndedTargetRpc`.
12. **Клиент** показывает result screen → закрывает TB window → игрок в open world.

**Пример (step 9):**

```
Round 1:
  Игрок (DEX 10) ходит первым.
  Ход 1: Move (1, 5) → (2, 5). 1 sec.
  Ход 2: Attack Goblin1 на (5, 5). dist=3 клетки (6м), в радиусе оружия (2м → 1 клетка, нет).
        → ошибка "Out of range".
  Ход 3: Move (2, 5) → (3, 5). 1 sec.
  Ход 4: Attack Goblin1 на (5, 5). dist=2 клетки (4м), weapon.range=2м → 1 клетка, ДА.
        DamageCalculator:
          roll d6=4, base=3, STR=10 → baseAttack=17.
          locRoll=3 (Torso) ×1.0.
          critRoll=87+0<100 → no crit ×1.0.
          skillMult=1.0.
          preDefense=17.
          Goblin1 armor=2, weapon.damageType=Physical → effectiveDefense=2.
          final=17-2=15.
        Goblin1: 20-15=5 HP.
  currentSeconds=0, end of turn.
Goblin1 (DEX 8) ходит.
  AI: HP=5/20=25% < 25% threshold → "Flee (HP<25%)".
  Move (5, 5) → (5, 6).
Goblin2 (DEX 6) ходит.
  AI: HP=20, dist=3 → Attack.
  Move (4, 7) → (3, 7) (1 sec).
  Move (3, 7) → (3, 6) (1 sec). currentSeconds=1.
  Attack Игрок (2, 5) dist=1 → 1 sec. currentSeconds=0.
    roll d6=2, base=2, STR=6 → 10. no crit. no loc bonus. Игрок armor=3. final=10-3=7.
    Игрок: 20-7=13 HP.
End of Round 1.
```

**Сложность:** 5-7 раундов, требует 1-2 лечебных предмета или хорошую броню.

### 1.4 Edge cases

| Случай | Решение |
|---|---|
| Игрок входит в данж, не имея weapon | Ошибка: «Нужен меч/лук/кинжал» → нет входа |
| Игрок входит в данж, имея ранг 10, в данж ранга 1 | ОК (overgeared) → победа trivially, но лут снижен (Phase 3) |
| Один и тот же данж — повторный вход | ОК, каждый вход = новый бой. Лимит «раз в день» — Phase 3 |
| Игрок disconnect в бою | Бой отменяется, без XP/loss (escape) |
| NPC-AI «застрял» (не может дойти) | Auto-move к случайной клетке через 5 сек (fallback, см. `20_TECHNICAL.md §6.1`) |

### 1.5 Примеры DungeonConfig (3 для MVP)

**MVP-1: Goblin Ruins Rank 1** (easy)
- 10x10, 3 гоблина (Goblin_Worker, level 1, Normal aggression)
- Лут: `Item_CopperBar` ×2, `Item_AntigravCrystal` ×1 (10% drop)
- XP: 30, Credits: 50, Rank: 1

**MVP-2: Bandit Camp Rank 3** (medium)
- 10x10, 4 бандита (Bandit_Scavenger, level 3, Aggressive)
- Лут: `Item_SteelPlate` ×1, `Item_CrossbowBolt` ×10, `Item_AntigravCrystal` ×2 (30% drop)
- XP: 80, Credits: 200, Rank: 3

**MVP-3: Forgotten Temple Rank 5** (hard)
- 10x10, 2 стража + 1 жрец (Temple_Guard, level 5, Defensive + Temple_Adept, level 5, Normal)
- Лут: `Item_AntigravCrystal` ×3, `Item_MesiumCrystal` ×1, legendary `Item_LegendarySword` (5% drop)
- XP: 200, Credits: 500, Rank: 5

### 1.6 Trigger — `TurnBasedBattleZone` (GameObject)

**Файл:** `Assets/_Project/Scripts/TurnBased/Zone/TurnBasedBattleZone.cs`
**Namespace:** `ProjectC.TurnBased`
**Сцена:** `WorldScene_X_Z` (рядом с другими интерактивными объектами)

```csharp
public class TurnBasedBattleZone : NetworkBehaviour, IInteractable {
    [SerializeField] private DungeonConfig _config;
    [SerializeField] private float _interactRadius = 5f;

    // F-key trigger (через NetworkPlayer)
    public void OnInteract(ulong clientId) {
        if (!IsServer) return;
        if (Vector3.Distance(transform.position, GetPlayerPos(clientId)) > _interactRadius) return;
        TurnBasedBattleServer.Instance.RequestStartPvEBattleRpc(_config.dungeonId, new RpcParams {
            Send = new RpcSendParams { Target = RpcTarget.Single(clientId, RpcTargetUse.Temp) }
        });
    }

    public string GetInteractionPrompt() => $"Войти в {_config.displayName} (F)";
}
```

---

## 2. PvE-кооп-данж (4 игрока vs NPC)

### 2.1 Концепция

**Групповой контент.** Party из 2-4 игроков заходит в кооп-данж → бой 4-8v10 на сетке 12x12 → топ-3 по урону получают бонусные награды.

**Целевая аудитория:** MMO-party (3-4 человека), guild-активити.

**Сложность:** RANK 5-10 (только для прокачанных).

### 2.2 Отличия от соло-данжа

| Параметр | Соло | Кооп |
|---|---|---|
| Grid | 10x10 | 12x12 |
| Участники | 1 игрок + 1-3 NPC | 2-4 игрока + 5-10 NPC |
| Инициатива | по DEX | по DEX (все игроки + NPC в одной очереди) |
| Сетка | bottom-edge player | 4 corners (NW, NE, SW, SE) — каждый игрок в своём углу |
| Действия | каждый сам по себе | каждый сам по себе, но **общая** победа/поражение |
| Лут | всё игроку | дроп делится поровну (или каждому свой, designer choice) |
| XP loss | 20% | 0% (групповой ивент) |
| Timeout | нет | 30 мин (если бой не закончился, ничья) |

### 2.3 Sequence

1. **Party leader** (player A) подходит к `[CoopDungeonEntrance_BanditStronghold]`.
2. A нажимает F → UI: «Пригласить party: [B, C, D]».
3. A выбирает участников → `RequestStartCoopBattleRpc(dungeonId, [A, B, C, D])`.
4. **Сервер** валидирует: все 4 игрока в зоне, все online, все не в бою.
5. Сервер создаёт `TurnBasedBattleInstance` (12x12, 4 players + 8 NPC-bandits).
6. Сервер отправляет `BattleStartedTargetRpc` всем 4 игрокам.
7. Бой идёт по 3-секундным ходам. UI каждого игрока показывает его perspective.
8. **Win condition:** все NPC мертвы → victory → loot (split, см. §2.4).
9. **Lose condition:** все 4 игрока мертвы → defeat → 0% XP loss.
10. **Timeout:** 30 мин → draw → 0% rewards, 0% loss.

### 2.4 Loot split

**Вариант A (equal split, MVP):** дроп делится поровну между всеми живыми игроками. `Item_CopperBar` ×4 → каждый получает 1.

**Вариант B (contribution-based, Phase 2):** топ-3 по урону получают бонусные награды, остальные — базовые.

**MVP = A**.

### 2.5 UI: party-aware

`TurnBasedBattleWindow` отображает:
- Сетка 12x12 (все участники, включая других игроков).
- HP/AP каждого участника (свой — крупно, остальные — мелко).
- «Ход: Игрок C (3/3 сек) | Раунд 5».
- Кнопки действий (только когда ход игрока).

**Multiplayer sync:** каждый игрок видит ту же сетку (все NPC + все игроки), но **действует только своим ходом**. Остальные игроки ждут.

---

## 3. PvP-дуэль 1v1

### 3.1 Концепция

**Social PvP.** Два игрока вызывают друг друга на дуэль → бой 1v1 на сетке 6x6 → победитель получает ставку (credits + honor), проигравший — permadeath или 20% XP loss.

**Целевая аудитория:** социальные игроки, рейтинговые бойцы.

**Сложность:** определяется снаряжением + навыками игроков (PvP = no PVE scaling).

### 3.2 DuelConfig (SO)

**Файл:** `Assets/_Project/Scripts/TurnBased/Config/DuelConfig.cs`
**Namespace:** `ProjectC.TurnBased.Config`
**Path:** `Resources/Duels/StandardDuel_1v1.asset`

```csharp
[CreateAssetMenu(fileName = "Duel_", menuName = "Project C/TurnBased/Duel Config")]
public class DuelConfig : ScriptableObject {
    [Header("Identity")]
    public string duelId;            // "standard_1v1"
    public string displayName;        // "Стандартная дуэль 1v1"
    public Sprite icon;

    [Header("Grid")]
    [Range(4, 12)] public int gridSize = 6;  // 6x6

    [Header("Rules")]
    public int creditsStake = 100;     // победитель получает, проигравший теряет
    public int honorPoints = 10;
    [Tooltip("Permadeath при поражении? Если false — 20% XP loss.")]
    public bool permadeathOnDefeat = false;
    [Range(0f, 1f)] public float xpLossPercent = 0.20f;
    [Tooltip("Максимальное время на ход (anti-stalling)")]
    public int turnTimeLimitSeconds = 30;
    [Tooltip("Anti-ragequit: при выходе соперника через сколько секунд — бой отменяется")]
    public int disconnectGraceSeconds = 30;
}
```

### 3.3 Sequence

1. **Игрок A** в социальном хабе нажимает «Вызвать на дуэль» → вводит имя B.
2. A нажимает «Отправить» → `RequestStartPvPDuelRpc(B.clientId, "standard_1v1")`.
3. **Сервер** валидирует: B online, B не в бою, B не отказал в последние 5 мин.
4. Сервер отправляет `DuelInviteTargetRpc(B)`.
5. **Игрок B** видит UI: «A вызывает вас на дуэль (ставка 100 credits). Принять? [✓] [✗]».
6. B нажимает [✓] → `RespondDuelInviteRpc(battleId, accept=true)`.
   Или B нажимает [✗] → decline → A получает уведомление «B отказался».
   Или **30 сек timeout** → decline (B не ответил).
7. Если B accept → сервер создаёт `TurnBasedBattleInstance` (6x6, A vs B).
8. Сервер отправляет `BattleStartedTargetRpc` обоим.
9. Бой идёт.
10. **Win:** победитель получает credits + honor, проигравший — XP loss или permadeath (по DuelConfig).
11. **Disconnect:** grace 30 сек → если соперник не вернулся, бой отменяется, no XP/loss.

### 3.4 Death / XP loss

```csharp
public void HandleDeathPvP(ulong loserId, DuelConfig config) {
    if (config.permadeathOnDefeat) {
        // Permadeath: удалить персонажа (или transfer to alt)
        Debug.Log($"[PvP] {loserId} permadeathed in duel {config.duelId}");
        // Transfer to safe zone with 0 HP, full inventory
    } else {
        // XP loss
        var stats = StatsWorld.Instance.GetOrCreateStats(loserId);
        float xpLoss = stats.strength * config.xpLossPercent;
        StatsServer.Instance.ApplyXpDirect(loserId, StatType.Strength, -xpLoss);
        Debug.Log($"[PvP] {loserId} lost {xpLoss} STR ({config.xpLossPercent * 100}%)");
    }
}
```

### 3.5 Edge cases

| Случай | Решение |
|---|---|
| Оба игрока disconnect | Бой отменяется, no rewards/loss |
| Один disconnect (grace 30 сек) | Бой отменяется, no rewards/loss для оставшегося |
| Один «ragequit» сразу | grace 30 сек, потом отмена |
| Оба игрока AFK > 30 сек на ход | auto-end turn (без действий) |
| B отказался | A уведомление, A может пригласить другого |
| A пытается пригласить B, B уже в бою | Ошибка «B занят» |

### 3.6 Honor / рейтинг

**Honor** — отдельный stat (future, `StatsWorld` не имеет honor). Сейчас — просто инкрементируем в `CharacterSaveData.honorPoints`.

**Вердикт:** honor-система — Phase 2. MVP: просто credits + 0/20% XP loss.

---

## 4. Boss-enкаунтер (TB-only)

### 4.1 Концепция

**High-stakes PvE.** Некоторые NPC-боссы (например, лидер Гильдии) **доступны только через TB**. Защита от zerg-стратегий в real-time (когда появится). Boss = сложный бой 1v1 или 1v3, уникальные механики.

**Целевая аудитория:** hardcore-игроки, квест-энкаунтеры, raid-контент.

**Сложность:** RANK 8-10 (для high-level игроков).

### 4.2 Особенности

| Параметр | Стандартный данж | Boss-enкаунтер |
|---|---|---|
| NPC | обычные (1-5 шт) | 1 босс + 0-2 приспешника |
| HP босса | 20-50 | 100-300 |
| Босс-weapon | обычный | legendary / antigrav (d10-d12, critMod +10) |
| Босс-ai | rule-based, Normal aggression | rule-based + специальные правила (фазы, AoE) |
| Лут | обычный (медь, кристаллы) | legendary (именной меч, antigrav-артефакт) |
| XP | 30-200 | 500-2000 |
| Credits | 50-500 | 1000-5000 |
| Квест-флаг | нет | да (триггер квеста) |
| Cooldown | нет | 1 раз в неделю (per player) |

### 4.3 BossConfig (SO)

```csharp
[CreateAssetMenu(fileName = "Boss_", menuName = "Project C/TurnBased/Boss Config")]
public class BossConfig : ScriptableObject {
    [Header("Identity")]
    public string bossId;
    public string displayName;
    public string description;

    [Header("Combat")]
    public int maxHp;
    public int strength;
    public int dexterity;
    public WeaponItemData weapon;        // ассет ссылки
    public ClothingItemData[] armor;      // ассет ссылки
    public NpcAggression aggression;      // Berserk (no flee)
    public BossPhaseConfig[] phases;      // см. §4.4

    [Header("Spawns")]
    public NpcSpawnConfig[] adds;         // приспешники (если есть)
    public Vector2Int bossSpawnPos;
    public int gridSize = 10;

    [Header("Rewards")]
    public LootDropConfig[] loot;
    public int xpReward;
    public int creditsReward;

    [Header("Quest Integration")]
    public string[] questFlags;           // "killed_pirate_chief"
    public string dialogueOnVictory;     // "Спасибо, путник! Вот награда."

    [Header("Cooldown")]
    public float respawnCooldownHours = 168f;  // 1 неделя
}

[Serializable]
public struct BossPhaseConfig {
    [Range(0f, 1f)] public float hpThreshold;  // 0.5 = на 50% HP переключиться
    public ActionType preferredAction;          // на этой фазе
    public float specialActionChance;           // 0.3 = 30% шанс AoE-атаки
}
```

### 4.4 Boss-фазы (Phase 2, опционально)

**Phase 1 (HP 100% → 50%):** обычные действия, attack/move.
**Phase 2 (HP 50% → 25%):** чаще AoE-атака (3 клетки вокруг).
**Phase 3 (HP 25% → 0%):** berserk, +dmg, +speed (Phase 3, без MVP).

**MVP = 1 фаза (без фаз).** Просто сложный NPC с высоким HP и хорошим оружием.

### 4.5 Sequence

1. **Игрок** подходит к NPC-боссу (например, `[Boss_PirateChief]` в `WorldScene_5_3`).
2. В радиусе 50м → авто-триггер (только на localPlayer, остальные видят визуально).
3. UI: «Босс [Пиратский Вожак] (Уровень 10). Начать бой? [⚔️ Атаковать] [✗ Отказаться]».
4. Игрок нажимает [⚔️ Атаковать] → `RequestStartBossBattleRpc("boss_pirate_chief")`.
5. **Сервер** валидирует: cooldown (не в течение 168 часов после прошлой победы).
6. Сервер создаёт `TurnBasedBattleInstance` (10x10, 1 player + 1 boss + 2 adds).
7. Бой идёт.
8. **Победа:** boss "умирает" → drops legendary loot → `questFlags` set → dialogueOnVictory.
9. **Поражение:** respawn + 20% XP loss.

### 4.6 Boss-локации (примеры)

| Boss | Location | Loot | Cooldown |
|---|---|---|---|
| `PirateChief_Korg` | Остров Пиратов, лагерь | `Weapon_GreatSword_Pirate` (d12, base=8, critMod=+15) | 168h |
| `TempleGuardian_Rex` | Забытый Храм, алтарь | `Armor_PlateArmor_Legendary` (armor=20, STR+5) | 168h |
| `BanditLord_Vex` | Лагерь Бандитов, шатёр вождя | `Item_LegendaryRing_Power` (+10% damage) | 168h |

---

## 5. Фракционный ивент (4-8 игроков vs NPC)

### 5.1 Концепция

**Глобальный серверный ивент.** Раз в неделю (или реже) сервер запускает глобальный ивент, например «Оборона пика» (5 Гильдий сражаются с волной NPC). 4-8 игроков принимают → бой 4-8v10-20 на сетке 12x12 → топ-3 по урону получают награды.

**Целевая аудитория:** все игроки (средний уровень), guild-активити.

**Сложность:** event-scaling (зависит от количества участников).

### 5.2 EventConfig (SO)

```csharp
[CreateAssetMenu(fileName = "Event_", menuName = "Project C/TurnBased/Event Config")]
public class EventConfig : ScriptableObject {
    [Header("Identity")]
    public string eventId;            // "weekly_peak_defense"
    public string displayName;        // "Еженедельная оборона пика"
    public string description;

    [Header("Schedule")]
    public DayOfWeek dayOfWeek = DayOfWeek.Saturday;
    public int startHour = 20;        // 20:00 UTC
    public int durationMinutes = 60;

    [Header("Combat")]
    public int gridSize = 12;
    public int minPlayers = 4;
    public int maxPlayers = 8;
    public int npcWaves = 5;          // волны NPC
    public int npcPerWave = 6;
    public NpcSpawnConfig[] npcTypes;

    [Header("Rewards (top-3)")]
    public LootDropConfig[] firstPlace;
    public LootDropConfig[] secondPlace;
    public LootDropConfig[] thirdPlace;
    public LootDropConfig[] participation;  // для всех

    [Header("Zone")]
    public string zoneName;
    public Vector3 zoneCenter;
    public float zoneRadius = 50f;
}
```

### 5.3 Sequence

1. **Серверный тик** (например, `DayNightPhaseChangedEvent`): проверка scheduled events.
2. Если `now >= event.startTime` → создать `EventInstance` (server-side).
3. **EventInstance** публикует `EventStartedEvent` всем в зоне.
4. Игроки в зоне получают приглашение: «[Оборона пика] началась! Принять? [✓] [✗]».
5. 4-8 принимают → `RequestJoinEventRpc(eventId)`.
6. Сервер создаёт `TurnBasedBattleInstance` (12x12, N players + 6 NPC wave 1).
7. Бой идёт. После убийства всех NPC → следующая волна (если есть).
8. **Win condition:** все 5 волн убиты → victory → top-3 получают награды.
9. **Lose condition:** все игроки мертвы → defeat → 0 XP loss (event), 0 credits.
10. **Timeout:** 60 мин → draw → все получают `participation` loot, top-3 — нет.

### 5.4 Top-3 по урону

```csharp
public class EventLeaderboard {
    public Dictionary<ulong, int> totalDamagePerPlayer = new();

    public void RecordDamage(ulong clientId, int damage) {
        if (!totalDamagePerPlayer.ContainsKey(clientId)) totalDamagePerPlayer[clientId] = 0;
        totalDamagePerPlayer[clientId] += damage;
    }

    public List<ulong> GetTopPlayers(int count) {
        return totalDamagePerPlayer
            .OrderByDescending(kv => kv.Value)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();
    }
}
```

### 5.5 Edge cases

| Случай | Решение |
|---|---|
| < minPlayers (4) за 10 мин после старта | Event отменяется, reschedule |
| Player joins mid-event | Нет (snapshot-based, no late join) |
| Player disconnects mid-event | Без penalty, бот-NPC занимает его слот (Phase 3) |
| Server crash mid-event | Бой отменяется, no rewards |

---

## 6. Сводная таблица сценариев

| Сценарий | Grid | Участники | Сложность | XP loss | Лут | Cooldown |
|---|---|---|---|---|---|---|
| **PvE-соло-данж** | 10x10 | 1 + 1-3 NPC | RANK 1-10 | 20% | обычный | нет |
| **PvE-кооп-данж** | 12x12 | 2-4 + 5-10 NPC | RANK 5-10 | 0% | делится | нет |
| **PvP-дуэль 1v1** | 6x6 | 2 | по снаряжению | 20% или permadeath | credits + honor | нет |
| **Boss-enкаунтер** | 10x10 | 1 + 1 boss (+ 0-2 adds) | RANK 8-10 | 20% | legendary | 168h (1 week) |
| **Фракционный ивент** | 12x12 | 4-8 + 5-20 NPC (5 волн) | event-scaling | 0% | top-3 + participation | 1 week |

---

## 7. Что НЕ делаем (явные запреты)

- ❌ Real-time combat (отдельная подсистема).
- ❌ Сложный boss-AI (фазы, summons, AoE) — Phase 2.
- ❌ Multi-player TB (4-8 игроков) в реальном времени — Phase 3.
- ❌ PvP-фракции (5 Гильдий, враждебные игроки в real-time) — future.
- ❌ Voice-chat в PvP-дуэли.
- ❌ Replay-система.
- ❌ Анимации, sound, voice — отдельные отделы.
- ❌ Магия (lore).
- ❌ Open-world TB (только в спец. зонах).
