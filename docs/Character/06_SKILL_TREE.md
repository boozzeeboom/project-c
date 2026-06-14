# Skill Tree — навыки, нодовая система, social/combat split

> **Дата:** 2026-06-14
> **Базируется на:** `SkillNodeConfig` (новый SO), `SkillEffect` struct, `SkillsServer` (RPC hub), `SkillsWorld` (POCO singleton)
> **Подход:** каждый навык = отдельный SO. Prerequisites = `SkillNodeConfig[]` (refs). Effects = `SkillEffect[]` (struct + enum). MVP визуализация = список с prerequisite-arrows (НЕ Painter2D graph)

---

## 1. Навыки как ноды (data-model)

### 1.1 SkillNodeConfig — структура

**Файл:** `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs`
**CreateAssetMenu:** `"Project C/Skill Node"`
**Namespace:** `ProjectC.Skills`

```csharp
public enum SkillCategory : byte { Social = 0, Combat = 1 }

[CreateAssetMenu(fileName = "Skill_", menuName = "Project C/Skill Node", order = 13)]
public class SkillNodeConfig : ScriptableObject {
    [Header("Identity")]
    public string skillId;             // "social_diplomacy_1" — stable key
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Category")]
    public SkillCategory category;

    [Header("Prerequisites (DAG, no cycles)")]
    [Tooltip("All listed skills must be unlocked to learn this one.")]
    public SkillNodeConfig[] prerequisites = Array.Empty<SkillNodeConfig>();

    [Header("Effects (applied when learned)")]
    public SkillEffect[] effects = Array.Empty<SkillEffect>();

    [Header("XP Cost")]
    [Tooltip("XP spent from Intelligence pool to unlock. 0 = free (starter skill)")]
    [SerializeField, Min(0f)] private float _learnXpCost = 50f;

    [Header("Tier Requirement")]
    [Tooltip("Min Intelligence tier. 0 = no requirement.")]
    [SerializeField, Min(0)] private int _requiredIntelligenceTier = 0;

    [Header("UI Layout (for tree view)")]
    public int treeX;
    public int treeY;

    // === Public API ===
    public float LearnXpCost => _learnXpCost;
    public int RequiredIntelligenceTier => _requiredIntelligenceTier;

    #if UNITY_EDITOR
    private void OnValidate() {
        if (prerequisites == null || prerequisites.Length == 0) return;
        var visited = new HashSet<SkillNodeConfig>();
        var stack = new HashSet<SkillNodeConfig>();
        if (HasCycle(this, visited, stack)) {
            Debug.LogWarning($"[SkillNodeConfig] Cycle detected in prerequisites for '{skillId}'.", this);
        }
    }

    private static bool HasCycle(SkillNodeConfig node, HashSet<SkillNodeConfig> visited, HashSet<SkillNodeConfig> stack) {
        if (stack.Contains(node)) return true;
        if (visited.Contains(node)) return false;
        visited.Add(node);
        stack.Add(node);
        if (node.prerequisites != null) {
            foreach (var p in prerequisites) {
                if (p != null && HasCycle(p, visited, stack)) return true;
            }
        }
        stack.Remove(node);
        return false;
    }
    #endif
}
```

### 1.2 SkillEffect — atomic effect struct

```csharp
[Serializable]
public struct SkillEffect {
    public enum Type : byte {
        StatMod = 0,            // +X к STR/DEX/INT (или multiplier)
        AbilityUnlock = 1,      // открывает ability ID (будущее оружие)
        PassiveEffect = 2,      // generic passive (future use)
    }

    public Type type;
    public StatType statType;       // только для StatMod
    public float floatValue;        // additive bonus (StatMod) или duration (PassiveEffect)
    [Range(0f, 5f)] public float multiplier;   // multiplicative (StatMod), 0 = no multiplier
    public string stringParam;      // ability id / passive id (AbilityUnlock / PassiveEffect)
}
```

**Atomic design rationale:** один SkillEffect = один тип параметров (type + statType + value). Не composite struct (избегаем проблем с depth limit Unity serializer).

### 1.3 Категории: Social vs Combat

Из спецификации пользователя:
> "навыки - дерево зависимых навыков разделить на социальные и боевые. социальные будут доступны для разговора исследования, боевые для сражений."

**Реализация:**
- `SkillCategory` enum на каждом SkillNodeConfig
- UI фильтр (sub-tabs внутри навыкового таба: Боевые / Социальные)
- **Runtime effects НЕ зависят от category** — combat навык может дать `INT+1` (для tactical spells), social может дать `STR+1` (для intimidating presence). Category = display-only.

**MVP навыки (8 штук):**

| Skill | Category | Prereq | Effects | XP Cost | INT Tier |
|-------|----------|--------|---------|---------|----------|
| `Skill_Combat_BasicStrike` | Combat | none | StatMod(STR+2) | 0 | 0 |
| `Skill_Combat_DodgeRoll` | Combat | none | StatMod(DEX+3) | 0 | 0 |
| `Skill_Combat_HeavySwing` | Combat | BasicStrike | StatMod(STR+5, ×1.2) | 100 | 2 |
| `Skill_Combat_PrecisionStrike` | Combat | DodgeRoll, HeavySwing | StatMod(DEX+5, ×1.3) | 200 | 4 |
| `Skill_Social_BasicTalk` | Social | none | StatMod(INT+2) | 0 | 0 |
| `Skill_Social_Barter` | Social | BasicTalk | StatMod(INT+3, ×0.95) | 100 | 2 |
| `Skill_Social_Persuasion` | Social | BasicTalk | PassiveEffect("+10% dialog XP") | 100 | 2 |
| `Skill_Social_Leadership` | Social | Barter, Persuasion | AbilityUnlock("recruit_npc") | 200 | 4 |

**Default starting skills (XP cost = 0):** BasicStrike, DodgeRoll, BasicTalk — все новые игроки имеют их при старте (или учатся автоматически при первом connect).

---

## 2. SkillsServer — RPC hub

### 2.1 Расположение

**Файл:** `Assets/_Project/Skills/SkillsServer.cs`
**Namespace:** `ProjectC.Skills`
**Scene-placed:** `BootstrapScene.unity` рядом с другими серверами

### 2.2 Структура

```csharp
public class SkillsServer : NetworkBehaviour {
    public static SkillsServer Instance { get; private set; }

    private SkillsConfig _config;  // loaded из Resources/Skills/SkillsConfig_Default.asset

    public override void OnNetworkSpawn() {
        if (!IsServer) return;
        Instance = this;

        _config = Resources.Load<SkillsConfig>("Skills/SkillsConfig_Default");
        SkillsWorld.Instance.LoadAllSkills(_config);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    public override void OnNetworkDespawn() {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        if (Instance == this) Instance = null;
    }

    private void OnClientConnected(ulong clientId) {
        if (!IsServer) return;
        // Default starting skills
        SkillsWorld.Instance.GrantDefaultSkills(clientId, _config);
        // Send initial snapshot
        SendSnapshotToOwner(clientId);
    }

    // === Client → Server ===

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestLearnSkillRpc(string skillId, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;

        if (!SkillsWorld.Instance.TryLearnSkill(clientId, skillId, out var reason)) {
            SendSkillResult(clientId, SkillResultDto.Denied(reason));
            return;
        }

        SendSkillResult(clientId, SkillResultDto.Learned(skillId));
        // Recompute stats (Skill may have added bonuses)
        StatsServer.Instance?.RecomputeAndSendSnapshot(clientId);
        SendSnapshotToOwner(clientId);
    }

    /// <summary>
    /// Q3.4: free respec без потерь. Игрок может забыть любой learned skill в любой момент,
    /// XP потраченное на learn НЕ возвращается (Q3.4: "без потерь" = без денежных потерь,
    /// НЕ без потерь XP). После forget: убираем из learned, recompute stats (бонус снимается).
    /// </summary>
    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RequestForgetSkillRpc(string skillId, RpcParams rpcParams = default) {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!RateLimit(clientId)) return;

        if (!SkillsWorld.Instance.TryForgetSkill(clientId, skillId, out var reason)) {
            SendSkillResult(clientId, SkillResultDto.Denied(reason));
            return;
        }

        SendSkillResult(clientId, SkillResultDto.Forgotten(skillId));
        // Recompute stats (skill bonus removed)
        StatsServer.Instance?.RecomputeAndSendSnapshot(clientId);
        SendSnapshotToOwner(clientId);
    }

    // === Server → Client ===
    private void SendSnapshotToOwner(ulong clientId) {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
        var snap = SkillsWorld.Instance.BuildSnapshot(clientId);
        netPlayer?.ReceiveSkillsSnapshotTargetRpc(snap);
    }

    private void SendSkillResult(ulong clientId, SkillResultDto result) {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return;
        var netPlayer = client.PlayerObject?.GetComponent<NetworkPlayer>();
        netPlayer?.ReceiveSkillResultTargetRpc(result);
    }
}
```

### 2.3 SkillsConfig (SO) — для starting skills + global config

```csharp
[CreateAssetMenu(fileName = "SkillsConfig", menuName = "Project C/Skills/Skills Config")]
public class SkillsConfig : ScriptableObject {
    [Header("Default starting skills (auto-granted on connect)")]
    [Tooltip("Q3.2: по решению пользователя — ПУСТОЙ массив. Игрок учит все skills с нуля сам. Если designer захочет starter skills — добавит в .asset.")]
    public SkillNodeConfig[] defaultSkills = Array.Empty<SkillNodeConfig>();

    [Header("Rate limit")]
    [Tooltip("Max skill learn requests per second per client")]
    [SerializeField, Min(1)] private int _maxOpsPerSec = 5;
    public int MaxOpsPerSec => _maxOpsPerSec;
}
```

---

## 3. SkillsWorld — POCO singleton + state

### 3.1 Расположение

**Файл:** `Assets/_Project/Skills/SkillsWorld.cs`
**Namespace:** `ProjectC.Skills`

```csharp
public class SkillsWorld {
    public static SkillsWorld Instance { get; private set; }

    private Dictionary<string, SkillNodeConfig> _skillsById = new();
    private Dictionary<ulong, HashSet<string>> _learnedPerPlayer = new();

    public void LoadAllSkills(SkillsConfig config) {
        _skillsById.Clear();
        // Загружаем все SkillNodeConfig из Resources/Skills/
        var allSkills = Resources.LoadAll<SkillNodeConfig>("Skills");
        foreach (var skill in allSkills) {
            if (string.IsNullOrEmpty(skill.skillId)) {
                Debug.LogError($"[SkillsWorld] Skill '{skill.name}' has empty skillId — skipping.");
                continue;
            }
            _skillsById[skill.skillId] = skill;
        }
        Debug.Log($"[SkillsWorld] Loaded {_skillsById.Count} skills.");
    }

    public void GrantDefaultSkills(ulong clientId, SkillsConfig config) {
        if (!_learnedPerPlayer.ContainsKey(clientId)) _learnedPerPlayer[clientId] = new HashSet<string>();
        foreach (var skill in config.defaultSkills) {
            if (skill != null) _learnedPerPlayer[clientId].Add(skill.skillId);
        }
    }

    public HashSet<string> GetLearnedSkillIds(ulong clientId) {
        if (!_learnedPerPlayer.TryGetValue(clientId, out var learned)) {
            learned = new HashSet<string>();
            _learnedPerPlayer[clientId] = learned;
        }
        return learned;
    }

    // === TryLearnSkill — основная валидация ===
    public bool TryLearnSkill(ulong clientId, string skillId, out string reason) {
        reason = "";

        // 1. Skill exists?
        if (!_skillsById.TryGetValue(skillId, out var skill)) {
            reason = "Навык не найден"; return false;
        }

        // 2. Already learned?
        var learned = GetLearnedSkillIds(clientId);
        if (learned.Contains(skillId)) {
            reason = "Навык уже изучен"; return false;
        }

        // 3. Prerequisites?
        if (skill.prerequisites != null) {
            foreach (var prereq in skill.prerequisites) {
                if (prereq != null && !learned.Contains(prereq.skillId)) {
                    reason = $"Требуется: {prereq.displayName}"; return false;
                }
            }
        }

        // 4. Intelligence tier requirement?
        var stats = StatsWorld.Instance?.GetOrCreateStats(clientId);
        if (stats != null && stats.intelligenceTier < skill.RequiredIntelligenceTier) {
            reason = $"Требуется Интеллект тир {skill.RequiredIntelligenceTier}+"; return false;
        }

        // 5. XP cost (spend from Intelligence pool)?
        if (skill.LearnXpCost > 0) {
            if (stats == null) {
                reason = "Неизвестна статистика"; return false;
            }
            if (stats.intelligence < skill.LearnXpCost) {
                reason = $"Не хватает XP (нужно {skill.LearnXpCost:F0})"; return false;
            }
            // Spend XP
            StatsServer.Instance?.ApplyXpDirect(clientId, StatType.Intelligence, -skill.LearnXpCost);
        }

        // All checks passed
        learned.Add(skillId);
        ApplySkillEffects(clientId, skill);
        Debug.Log($"[SkillsWorld] Player {clientId} learned skill '{skill.displayName}'");
        return true;
    }

    private void ApplySkillEffects(ulong clientId, SkillNodeConfig skill) {
        // Skill effects применяются в StatsServer.RecomputeAndSendSnapshot
        // (см. Equipment.md §2.2 — RecomputeEffectiveStat)
        // Здесь просто сигнализируем: skill добавлен в learned set.
    }

    /// <summary>
    /// Q3.4: free respec. Убирает skill из learned set. XP НЕ возвращается.
    /// Down-stream skills (которые требуют этот skill как prereq) остаются
    /// (даже если их prereq-chain сломан) — не делаем recursive cleanup.
    /// </summary>
    public bool TryForgetSkill(ulong clientId, string skillId, out string reason) {
        reason = "";
        if (!_skillsById.TryGetValue(skillId, out var skill)) {
            reason = "Навык не найден"; return false;
        }
        var learned = GetLearnedSkillIds(clientId);
        if (!learned.Contains(skillId)) {
            reason = "Навык не изучен"; return false;
        }
        learned.Remove(skillId);
        Debug.Log($"[SkillsWorld] Player {clientId} forgot skill '{skill.displayName}' (XP not refunded)");
        return true;
    }

    public SkillsSnapshotDto BuildSnapshot(ulong clientId) {
        var learned = GetLearnedSkillIds(clientId);
        return new SkillsSnapshotDto {
            learnedSkillIds = learned.ToArray(),
            // Сервер отдаёт минимальный snapshot — UI сам подгружает SkillNodeConfig из Resources
        };
    }

    // === Persistence ===
    public SkillsSave BuildSaveData(ulong clientId) {
        var learned = GetLearnedSkillIds(clientId);
        return new SkillsSave { learnedSkillIds = learned.ToArray() };
    }

    public void LoadPlayer(ulong clientId, CharacterSaveData data) {
        var learned = GetLearnedSkillIds(clientId);
        learned.Clear();
        if (data.skills?.learnedSkillIds != null) {
            foreach (var id in data.skills.learnedSkillIds) learned.Add(id);
        }
    }
}
```

---

## 4. Skill tree визуализация (UI)

### 4.1 Прецеденты в проекте

| Файл | Доступен в runtime? |
|------|---------------------|
| `Assets/_Project/Quests/Editor/QuestNodeGraphView.cs` | ❌ Editor-only (`#if UNITY_EDITOR`, uses `UnityEditor.Experimental.GraphView`) |
| `Assets/_Project/Quests/Editor/QuestGraphView.cs` | ❌ Editor-only |

**Вердикт:** никакого runtime-прецедента нодового дерева в проекте. `UnityEditor.Experimental.GraphView` использовать в runtime НЕЛЬЗЯ (build error `CS0234`).

### 4.2 Три варианта визуализации

#### Вариант A: GraphView (Editor-only)

**+:** полноценные Nodes/Edges/Ports с zoom/pan
**-:** build error в runtime — `using UnityEditor.Experimental.GraphView;` не работает в player build
**Вердикт:** ❌ НЕ используем в runtime

#### Вариант B: Custom VisualElement + Painter2D (ПЕРВИЧНЫЙ — Q3.6)

**ОТВЕТ ПОЛЬЗОВАТЕЛЯ:** "сразу с графом. нужно посмотреть насколько в игре это комофртно даже на мвп этапе."

**+:** полный контроль над rendering, нодовый граф с zoom/pan, connections visible, runtime API
**-:** ~2 сессии (600+ строк), porting `QuestGraphView` (Editor-only) в runtime namespace, тестирование pan/zoom
**Вердикт (обновлён):** ✅ ПЕРВИЧНЫЙ подход для MVP. Painter2D graph — сразу в MVP.

**Подход:**
1. Создать `Assets/_Project/UI/Skills/SkillTreeView.cs` — custom `VisualElement` + `generateVisualContent` callback + `Painter2D`
2. Отделить от `QuestGraphView` (Editor-only): убрать `using UnityEditor`, переписать `AssetDatabase` paths на `Resources.Load`
3. Nodes — реальные `VisualElement` с child labels, позиционируются через `style.left/top`
4. Connections — `Painter2D` линии между parent и child
5. Pan/zoom — через `MouseDownEvent` / `MouseMoveEvent` / `WheelEvent` (copy из `QuestGraphView`)
6. SkillNodeConfig.treeX/treeY используются для layout позиционирования

**MVP vs Phase 2:** Painter2D сразу в MVP (T-P14 объединит оба подхода — Painter2D graph + ListView fallback). Если Painter2D окажется неудобным — откат на ListView (Вариант C).

#### Вариант C: ListView + prerequisite-arrows (FALLBACK)

**+:** 1 сессия (300 строк), переиспользует `MakeQuestRow`/`BindQuestRow` pattern, чисто UI Toolkit
**-:** не "дерево" визуально, просто список с метками prerequisites
**Вердикт (обновлён):** ⬅️ FALLBACK, если Painter2D окажется некомфортным

### 4.3 Вариант C — реализация (fallback)

**UXML:**
```xml
<ui:VisualElement name="skills-section" class="list-sub-section" style="display: none;">
    <!-- Sub-tabs: Боевые / Социальные -->
    <ui:VisualElement class="quest-sub">
        <ui:Label text="Боевые" class="quest-section-title" />
        <ui:ListView name="skills-combat-list" class="item-list quest-list" />
    </ui:VisualElement>
    <ui:VisualElement class="quest-sub">
        <ui:Label text="Социальные" class="quest-section-title" />
        <ui:ListView name="skills-social-list" class="item-list quest-list" />
    </ui:VisualElement>
</ui:VisualElement>
```

**CharacterWindow.cs — handler:**
```csharp
private SkillsClientState _skillsState;
private bool _isSkillsSubscribed = false;
private List<SkillRow> _skillsCombatCache = new();
private List<SkillRow> _skillsSocialCache = new();

private void SubscribeSkills() {
    if (_isSkillsSubscribed) return;
    _skillsState = SkillsClientState.Instance;
    if (_skillsState == null) return;
    _skillsState.OnSkillsUpdated += HandleSkillsSnapshot;
    _isSkillsSubscribed = true;
}

private void UnsubscribeSkills() { /* standard pattern */ }

private void HandleSkillsSnapshot(SkillsSnapshotDto snap) {
    RefreshSkillsCache(snap);
    if (_activeTab == "progression" && _activeProgressionTab == "skills") {
        RebuildSkillsListView();
    }
}

private void RefreshSkillsCache(SkillsSnapshotDto snap) {
    _skillsCombatCache.Clear();
    _skillsSocialCache.Clear();

    var allSkills = Resources.LoadAll<SkillNodeConfig>("Skills");
    var learned = new HashSet<string>(snap.learnedSkillIds ?? Array.Empty<string>());

    foreach (var skill in allSkills) {
        var row = BuildSkillRow(skill, learned);
        if (skill.category == SkillCategory.Combat) _skillsCombatCache.Add(row);
        else _skillsSocialCache.Add(row);
    }
}

private SkillRow BuildSkillRow(SkillNodeConfig skill, HashSet<string> learned) {
    bool isLearned = learned.Contains(skill.skillId);
    bool canLearn = !isLearned;
    if (canLearn && skill.prerequisites != null) {
        foreach (var prereq in skill.prerequisites) {
            if (prereq != null && !learned.Contains(prereq.skillId)) { canLearn = false; break; }
        }
    }
    if (canLearn) {
        var stats = StatsClientState.Instance?.CurrentStats;
        if (stats != null && stats.intelligenceTier < skill.RequiredIntelligenceTier) canLearn = false;
    }

    return new SkillRow {
        SkillId = skill.skillId,
        DisplayName = skill.displayName,
        Description = skill.description,
        Category = skill.category,
        State = isLearned ? SkillState.Learned : (canLearn ? SkillState.Available : SkillState.Locked),
        XpCost = skill.LearnXpCost,
        RequiredTier = skill.RequiredIntelligenceTier,
        Prerequisites = skill.prerequisites?.Where(p => p != null).Select(p => p.displayName).ToArray() ?? Array.Empty<string>(),
    };
}

private void RebuildSkillsListView() {
    _skillsCombatList.itemsSource = _skillsCombatCache;
    _skillsCombatList.RefreshItems();
    _skillsCombatList.MarkDirtyRepaint();
    _skillsSocialList.itemsSource = _skillsSocialCache;
    _skillsSocialList.RefreshItems();
    _skillsSocialList.MarkDirtyRepaint();
}

// Row factory (pattern из CharacterWindow.cs:1480-1574)
private VisualElement MakeSkillRow() {
    var row = new VisualElement();
    row.AddToClassList("skill-row");
    var top = new VisualElement();
    top.AddToClassList("skill-row-top");
    var state = new Label { name = "skill-state" };
    state.AddToClassList("skill-row-state");
    var title = new Label { name = "skill-title" };
    title.AddToClassList("skill-row-title");
    var cost = new Label { name = "skill-cost" };
    cost.AddToClassList("skill-row-cost");
    top.Add(state);
    top.Add(title);
    top.Add(cost);
    row.Add(top);
    var desc = new Label { name = "skill-desc" };
    desc.AddToClassList("skill-row-desc");
    row.Add(desc);
    var prereq = new Label { name = "skill-prereq" };
    prereq.AddToClassList("skill-row-prereq");
    row.Add(prereq);
    var learnBtn = new Button { name = "skill-learn-btn", text = "ИЗУЧИТЬ" };
    learnBtn.AddToClassList("skill-row-btn");
    row.Add(learnBtn);
    return row;
}

private void BindSkillRow(VisualElement row, int index) {
    var skill = _skillsCombatCache[index] ?? (object)_skillsSocialCache[index] as SkillRow;
    // ... bind state, title, cost, desc, prereq, button
    row.Q<Label>("skill-state").text = skill.State.ToString().ToUpper();
    row.Q<Label>("skill-title").text = skill.DisplayName;
    row.Q<Label>("skill-cost").text = skill.XpCost > 0 ? $"{skill.XpCost:F0} XP" : "FREE";
    row.Q<Label>("skill-desc").text = skill.Description;
    row.Q<Label>("skill-prereq").text = skill.Prerequisites.Length > 0
        ? $"Требует: {string.Join(", ", skill.Prerequisites)}"
        : "";
    var btn = row.Q<Button>("skill-learn-btn");
    btn.SetEnabled(skill.State == SkillState.Available);
    btn.clicked += () => SkillsClientState.Instance?.RequestLearnSkill(skill.SkillId);
    // Apply state-based class
    row.RemoveFromClassList("skill-row-locked");
    row.RemoveFromClassList("skill-row-available");
    row.RemoveFromClassList("skill-row-learned");
    row.AddToClassList($"skill-row-{skill.State.ToString().ToLower()}");
}

public enum SkillState : byte { Locked, Available, Learned }
```

### 4.4 USS стили

```css
.skill-row {
    flex-direction: column !important;
    padding: 6px 8px !important;
    border-bottom-width: 1px !important;
    border-bottom-color: rgba(80, 100, 130, 0.2) !important;
}
.skill-row-top {
    flex-direction: row !important;
    justify-content: space-between !important;
    align-items: center !important;
}
.skill-row-state {
    width: 90px !important;
    font-size: 10px !important;
    -unity-font-style: bold !important;
    color: rgb(180, 180, 200) !important;
}
.skill-row-title {
    flex-grow: 1 !important;
    font-size: 12px !important;
    -unity-font-style: bold !important;
    margin: 0 6px !important;
}
.skill-row-cost {
    width: 60px !important;
    font-size: 11px !important;
    -unity-text-align: middle-right !important;
    color: rgb(220, 200, 130) !important;
}
.skill-row-desc {
    font-size: 10px !important;
    color: rgb(180, 180, 200) !important;
    margin-top: 2px !important;
}
.skill-row-prereq {
    font-size: 10px !important;
    color: rgb(200, 180, 130) !important;
    margin-top: 2px !important;
    -unity-font-style: italic !important;
}
.skill-row-btn {
    height: 22px !important;
    margin-top: 4px !important;
    background-color: rgba(80, 160, 80, 0.7) !important;
    color: rgb(240, 240, 240) !important;
    border-width: 0 !important;
    border-radius: 3px !important;
}
.skill-row-btn:hover { scale: 1.05 1.05 !important; }
.skill-row-btn:disabled { background-color: rgba(80, 80, 80, 0.5) !important; opacity: 0.5 !important; }

/* State variants */
.skill-row-locked { opacity: 0.5 !important; -unity-font-style: italic !important; }
.skill-row-locked .skill-row-state { color: rgb(180, 100, 100) !important; }
.skill-row-available { background-color: rgba(80, 200, 100, 0.10) !important; }
.skill-row-available .skill-row-state { color: rgb(120, 220, 120) !important; }
.skill-row-learned { background-color: rgba(120, 150, 200, 0.20) !important; }
.skill-row-learned .skill-row-state { color: rgb(150, 180, 220) !important; }
```

---

## 5. Phase 2 (post-MVP) — Painter2D skill tree

### 5.1 Архитектурный план

Если MVP окажется успешным и дизайнер захочет визуальное дерево:

**Файлы:**
- `Assets/_Project/UI/Skills/SkillTreeView.cs` — custom VisualElement + Painter2D (port из `QuestGraphView`)
- `Assets/_Project/UI/Skills/SkillTreeView.uxml` (опционально) — wrapper
- `Assets/_Project/UI/Skills/SkillTreeView.uss` — стили

**Архитектура:**
- `SkillTreeView` extends `VisualElement`
- `generateVisualContent += DrawPainterContent` — рисует grid + connections через `Painter2D`
- `_content` (inner VisualElement) с `style.translate` + `style.scale` для zoom/pan
- `_nodes : List<SkillNodeVisual>` — каждый skill = VisualElement внутри `_content`
- `_connections : List<(VisualElement from, VisualElement to)>` — prerequisites → линии через Painter2D

**Precedent в проекте:** `Assets/_Project/Quests/Editor/QuestGraphView.cs:17-105` — гибридный custom VisualElement + Painter2D. Нужно портировать в runtime namespace (убрать `using UnityEditor`).

**Трудозатраты:** ~1-2 сессии (с тестом). Реалистично, но не для MVP.

---

## 6. Edge cases

### 6.1 Skill prerequisites цикл (A → B → A)

**Сценарий:** designer создал A requires B, B requires A.

**Решение:**
1. `OnValidate` SO → DFS cycle detection → warning (не блокирует, но signal)
2. Runtime `TryLearnSkill` → visited set → detect cycle → deny с reason "Циклическая зависимость"
3. **Tier cap (`maxDepth`)** — если prerequisites depth > 5 → warning в OnValidate (Phase 2)

### 6.2 Skill ID collision (2 SO с одинаковым skillId)

**Сценарий:** 2 SO имеют skillId = `"social_basic_talk"`. `Resources.LoadAll` → обе в `_skillsById`, второй перезаписывает первый.

**Решение:** В `SkillsWorld.LoadAllSkills` — warning если 2+ skill с одинаковым skillId. Designer должен исправить.

### 6.3 SkillsConfig.defaultSkills = null

**Сценарий:** Player connects → `GrantDefaultSkills` → NullReferenceException.

**Решение:** `if (config?.defaultSkills != null) { foreach ... }` — null-safe.

### 6.4 Skill XP cost > available Intelligence XP

**Сценарий:** игрок имеет INT=20 XP, пытается изучить навык за 50 XP.

**Решение:** В `TryLearnSkill` → проверка `stats.intelligence >= skill.LearnXpCost` → deny "Не хватает XP". UI показывает progress bar (currentXp / requiredXp).

### 6.5 Skill effects apply → stats recompute → snapshot mismatch

**Сценарий:** игрок изучил навык → effective STR изменилась → StatsClientState показывает базовую STR.

**Решение:** `SkillsServer.RequestLearnSkillRpc` → `StatsServer.Instance?.RecomputeAndSendSnapshot(clientId)` → `NetworkPlayer.ReceiveStatsSnapshotTargetRpc(effectiveStats)`.

### 6.6 Player forgets skill (Phase 2) — нет сейчас

**Сценарий:** В текущем дизайне нет "forget skill". Только learn.

**Решение:** `SkillsServer` имеет `RequestLearnSkillRpc`, но `RequestForgetSkillRpc` — stub. Если когда-то добавим forget → recompute stats → send snapshot → UI обновляется.

### 6.7 Skill learned but skillId missing from SkillNodeConfig

**Сценарий:** SO удалён, но learnedSkillIds содержит skillId.

**Решение:** В `SkillsClientState.OnSkillsSnapshotReceived` → orphan skillIds игнорируются. UI показывает только skills с загруженным SO.

### 6.8 Tier-up + skill XP cost race

**Сценарий:** игрок имеет INT=99 (tier 0). Изучает навык за 100 XP. Tier promotion: INT tier 0→1. После: INT=99-100=-1? Или 0?

**Решение:** В `StatsServer.ApplyXp` → если XP negative (spend) и currentXp < 0, НЕ tier-down. Clamp at 0. (Tier downgrade запрещён.)

---

## 7. Pitfalls

### 7.1 Skill tree visualization = 50 nodes performance

**Проблема:** Если навыков > 50, ListView всё ещё работает (recycled rows), но визуально шумно.

**Решение:** Phase 2 — Painter2D tree view с zoom out. MVP — только фильтр/поиск.

### 7.2 SkillNodeConfig.OnValidate cycle detection — false positives

**Проблема:** Если skills образуют diamond (A → B, A → C, B → D, C → D) — нет цикла. Но DFS может flag.

**Решение:** visited set (Node visited) + recursion stack (in current path) — корректный DFS algorithm, не flag diamond.

### 7.3 SkillsConfig.defaultSkills — race with grant

**Сценарий:** `GrantDefaultSkills` вызывается при `OnClientConnected`. Если игрок reconnect — `learned` set загружается из save, потом `GrantDefaultSkills` пытается добавить default → idempotent (HashSet.Add возвращает false если уже есть).

**Решение:** HashSet.Add — naturally idempotent. Если default skill был ранее learned (но потом "forgotten" в save) — будет re-added. Acceptable.

### 7.4 SkillsWorld.Instance not yet created on connect

**Сценарий:** Player connects до того как `SkillsServer.OnNetworkSpawn` отработал (race).

**Решение:** `SkillsWorld.Instance` создаётся при `SkillsServer.OnNetworkSpawn`. До этого — singleton == null. UI handle null gracefully (lazy subscribe).

### 7.5 Skill ID stability across session

**Проблема:** Designer rename `Skill_Social_BasicTalk.asset` → `Skill_Social_Talk.asset`. skillId остаётся `"social_basic_talk"`, asset path изменился.

**Решение:** В OnValidate SO — warning если skillId пустой. В runtime — orphan skillId пропускается (UI не показывает). Re-grant через `GrantDefaultSkills` при следующем connect.

### 7.6 Skill effect floatValue negative

**Сценарий:** Skill effect StatMod(STR-2) — negative bonus? Или это только positive?

**Решение:** `floatValue` — может быть negative (debuff). Но multiplicative `[Range(0f, 5f)]` — не может быть negative. В runtime — clamp effective stat к [0.1, 100000].

### 7.7 Skill learned, but SkillNodeConfig prerequisites array modified later

**Сценарий:** игрок изучил HeavySwing. Позже designer изменяет HeavySwing.prerequisites = [BasicStrike, DodgeRoll] (добавил DodgeRoll). HeavySwing остаётся learned.

**Решение:** `OnValidate` SO → warning. Runtime — уже learned навыки остаются learned (не revoking).

### 7.8 SkillSnapshot DTO size

**Проблема:** `learnedSkillIds[]` — может быть 50+ skills.

**Решение:** Sync только on-change (не periodic). 50 strings × 30 chars avg = 1500 bytes per snapshot — acceptable.

---

## 8. Что НЕ делаем

- ❌ Не используем `UnityEditor.Experimental.GraphView` в runtime
- ❌ Painter2D = **первичный подход** в MVP (Q3.6). ListView = fallback.
- ✅ **RequestForgetSkillRpc = ВКЛЮЧЁН в MVP** (Q3.4 — free respec без потерь)
- ❌ Не делаем drag-and-drop skill learning (MVP — кнопка "ИЗУЧИТЬ")
- ❌ Не делаем tier-downgrade при XP spend (clamp at 0)
- ❌ Не делаем skill leveling (1 skill learned = full effects, не partial)
- ❌ Не пишем `.meta` / `.asmdef` файлы
- ❌ Не делаем skill tree в отдельном окне (`SkillTreeWindow.cs`) — внутри CharacterWindow
- ❌ Не делаем skill effect visual feedback (UI pulse / flash) — Phase 2
