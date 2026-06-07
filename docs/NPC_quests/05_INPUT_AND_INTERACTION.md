# 05 — Input и Interaction pipeline

> **Цель:** вписать новую NPC-talk в существующий E-key flow + учесть текущие
> особенности (F = board ship, PlayerInputReader events = dead code).

---

## 5.1 Текущее состояние input

**Из subagent analysis:**

| Клавиша | Текущая роль | Источник |
|---------|--------------|----------|
| F | board/disembark ship (walking mode); "F-press comment says Reserved for future: docking/refueling" | `NetworkPlayer.cs:286` + `PlayerStateMachine.cs:58` |
| E | pickup / chest / market / MetaRequirement | `NetworkPlayer.cs:375` |
| W/A/S/D | walk | `NetworkPlayer.cs:365-368` |
| Space | jump (walking) / throttle up (ship) | `NetworkPlayer.cs:369, 340` |
| LeftShift | run / boost | `NetworkPlayer.cs:370` |
| P | toggle `CharacterWindow` | `NetworkPlayer.cs:316` |
| Q | throttle up (ship) | `NetworkPlayer.cs:340-341` |
| Esc | close top panel | `UIManager.cs:96` |

**`PlayerInputReader` (events):** 7 events declared (`OnMoveInput`, `OnJumpPressed`, `OnRunPressed`, `OnRunReleased`, `OnInteractPressed`, `OnModeSwitchPressed`, `OnMouseDelta`) — **all dead code, no subscribers anywhere in project**.

**`NetworkPlayer` direct `Keyboard.current.*Key.wasPressedThisFrame` polling** — actual input handler.

**`NpcDialogueManager.Update` legacy violation:** `Input.GetKeyDown(KeyCode.Space)` (line 163) — против конвенции AGENTS.md.

---

## 5.2 F vs E — design decision

**Запрос пользователя:** "подошел нажал F (наша кнопка интерактивности) \ иногда E"

**Реальность кода:** F занят (boarding). E свободен для расширения.

**Рекомендация:** **E для NPC talk**, не F.

**Обоснование:**
- F = boarding; переименование boarding → ломает весь существующий flow.
- E уже interact-key (pickup, chest) — естественное расширение.
- "иногда E" в задаче = "иногда E можно использовать" → совместимо.

**См. вопрос #1 в `09_OPEN_QUESTIONS.md`** (требует user-confirmation).

---

## 5.3 E-key pipeline — где добавить NPC branch

**Текущий flow** в `NetworkPlayer.cs:375-395`:

```csharp
// 1. MetaRequirement (lock/box/key requirements)
if (TryInteractNearestMetaRequirement()) return;

// 2. Chest / pickup / market
var interactable = FindNearestInteractable();
if (interactable is NetworkChestContainer chest) { TryPickup(chest); return; }
if (interactable is ChestContainer legacyChest) { TryPickup(legacyChest); return; }

// 3. Market zone
if (MarketInteractor.TryOpenMarket()) return;

// 4. Pickup fallback
TryPickup();
```

**Добавить NPC branch** перед MetaRequirement (NPC приоритетнее всего — это intentional conversation start):

```csharp
// 0. NPC (highest priority)
if (QuestInteractor.Instance != null && QuestInteractor.Instance.TryTalkToNpc()) return;

// 1. MetaRequirement
if (TryInteractNearestMetaRequirement()) return;

// 2. Chest / pickup / market (unchanged)
...
```

**`QuestInteractor.TryTalkToNpc()`:**

```csharp
public bool TryTalkToNpc()
{
    var npc = InteractableManager.FindNearestNpc(transform.position, talkRange);
    if (npc == null) return false;

    var npcData = npc.GetNpcData();
    if (npcData == null) return false;

    QuestClientState.Instance?.RequestTalkToNpc(npcData.npcId);
    return true;
}
```

---

## 5.4 PlayerInputReader — fix or extend

**Текущее:** события объявлены, не подписаны. **Два пути:**

### Option A (минимальный, рекомендую): оставить как есть, использовать NetworkPlayer inline

**Плюсы:** не ломает существующий flow; работает.
**Минусы:** события продолжают быть dead code (но это технический долг, не блокер).

### Option B (cleanup): подписать `PlayerInputReader` events на `NetworkPlayer` methods

```csharp
// В NetworkPlayer.Awake:
_playerInputReader = GetComponent<PlayerInputReader>();
if (_playerInputReader != null)
{
    _playerInputReader.OnInteractPressed += OnEKeyPressed;       // E
    _playerInputReader.OnModeSwitchPressed += OnFKeyPressed;     // F
    // ... etc
}

private void OnEKeyPressed() { /* existing inline E logic */ }
private void OnFKeyPressed() { /* existing inline F logic */ }
```

**Плюсы:** чище, убирает dead code, соответствует AGENTS.md конвенции.
**Минусы:** больше работы, рефактор input pipeline.

**Рекомендация:** **Option A для v1 Quest**, **Option B как отдельный cleanup-тикет** (помечен в `08_ROADMAP.md`).

---

## 5.5 Dialog-specific input

**Внутри DialogWindow** (когда открыт):

| Клавиша | Действие | Источник |
|---------|----------|----------|
| Space | skip typewriter (если в процессе) | `PlayerInputReader.OnJumpPressed` (пока диалог открыт, jump заблокирован) |
| Esc | закрыть диалог | `UIManager.CloseTopPanel` или `DialogWindow.Close` |
| 1-9 (или Tab+arrows) | выбрать option | UI Toolkit focus navigation |
| Enter | подтвердить выбранный option | UI Toolkit `Button` default |

**В DialogWindow.cs:**

```csharp
private void OnEnable()
{
    // Subscribe to input
    if (PlayerInputReader.Instance != null)
    {
        PlayerInputReader.Instance.OnJumpPressed += OnSpacePressed;  // skip typewriter
    }
}

private void OnDisable()
{
    if (PlayerInputReader.Instance != null)
    {
        PlayerInputReader.Instance.OnJumpPressed -= OnSpacePressed;
    }
}

private void OnSpacePressed()
{
    if (!IsVisible) return;
    if (_isTyping) SkipTypewriter();
    // else: ignore (don't conflict with jump when dialog is closed)
}

private void Update()
{
    if (!IsVisible) return;
    if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
    {
        Close();
    }
}
```

**Проблема:** `PlayerInputReader.Instance` сейчас не singleton (нет Instance getter!). Нужно либо:
- Добавить `public static PlayerInputReader Instance { get; private set; }` в `PlayerInputReader` + `Awake` setter.
- Или инжектить reference через SerializeField.

**Рекомендация:** добавить `Instance` (минимальный refactor, см. `08_ROADMAP.md` T-Q10).

---

## 5.6 InteractableManager — расширение

**Текущее:** `RegisterNpc/UnregisterNpc/FindNearestNpc` уже реализованы (lines 90-107, 232-251), но **никем не вызываются**.

**Изменения для v2:**

```csharp
// В ProjectC.Core.InteractableManager (existing file, no API break):

// Store a richer reference for v2 NpcInteraction (server-aware)
private static readonly List<NpcInteraction> _npcs = new(16);  // already exists, but typed as `object`

// Add type-safe accessor:
public static IReadOnlyList<NpcInteraction> GetNpcsTyped() => _npcs;  // new

// Update RegisterNpc to use the typed list (was List<object>):
public static void RegisterNpc(NpcInteraction npc)
{
    if (npc != null && !_npcs.Contains(npc))
        _npcs.Add(npc);
}
```

**Note:** `GetNpcs()` (current) returns `IReadOnlyList<object>` — change to typed `IReadOnlyList<NpcInteraction>`. Breaking change, requires grep for callers (currently zero).

**Also add `FindNearestNpc(Vector3, float)` already exists** — just wire it into `NetworkPlayer` (per §5.3).

---

## 5.7 NpcInteraction — server-side reference

**Проблема v1:** `NpcInteraction.GetNpcData()` возвращает `NpcData` SO, но в production коде нигде не используется. В v2 — `NpcDefinition` (новый SO), нужно аналогично.

```csharp
// v2 NpcInteraction
public class NpcInteraction : MonoBehaviour, IInteractable
{
    [SerializeField] private NpcDefinition _npcDefinition;  // changed from NpcData

    public NpcDefinition GetNpcDefinition() => _npcDefinition;
    public NpcData GetNpcData() => _npcDefinition as NpcData;  // backward compat, remove in v3
    // ... rest unchanged
}
```

**Backward compatibility:** на время миграции держим `GetNpcData()` returning `_npcDefinition as NpcData` (null в новом коде). Удалить в v3 cleanup-тикете.

---

## 5.8 Distance / line-of-sight check

**Текущее:** `Vector3.Distance(playerPos, npcPos) < range` (нет raycast).

**Возможные улучшения:**
- Line-of-sight raycast: `Physics.Raycast(playerPos, dirToNpc, out hit, range)` — но это overhead, и в проекте нет видимости-блокеров (облака прозрачные).
- Vertical distance clamp: `Mathf.Abs(delta.y) < verticalRange` (default 2m) — чтобы не interact'ить с NPC на 100m ниже.

**Рекомендация для v1:** оставить `Vector3.Distance` (consistent with chest/pickup). **Add vertical clamp** (1-line change).

---

## 5.9 Pitfall-лист (input/interaction)

| # | Pitfall | Источник |
|---|---------|---------|
| 1 | F = boarding; нельзя использовать для NPC без remap | `NetworkPlayer.cs:286` |
| 2 | `PlayerInputReader` events declared, no subscribers | search verified |
| 3 | `NpcDialogueManager.Input.GetKeyDown` violates AGENTS.md | `NpcDialogueManager.cs:163` |
| 4 | `NpcDialogueManager` (v1) — `FindAnyObjectByType` null в stream-сценах | `NpcDialogueManager.cs:22-36` |
| 5 | `FindNearestNpc` существует, но не вызывается из E-pipeline | `NetworkPlayer.cs:375-395` |
| 6 | `InteractableManager._npcs` typed as `List<object>`, не `List<NpcInteraction>` | `InteractableManager.cs:19, 127` |
| 7 | `PlayerInputReader` не singleton, нет `Instance` getter | `PlayerInputReader.cs` (search verified) |
| 8 | No vertical distance clamp для interactable (player на 100m ниже может interact'ить с NPC) | subagent analysis §6.6 |

---

## 5.10 Open questions (input-specific)

**См. `09_OPEN_QUESTIONS.md` §C.**

Ключевые:
- F vs E — пользователь сказал F, код говорит E. Как reconcile?
- Typewriter skip: Space (jumps) или новый key (e.g. E в dialog context = advance)?
- Gamepad: одинаковое поведение с keyboard? UI Toolkit `FocusController` из коробки.
- Remap support: разрешать ли per-player remap? (out of scope для v1).
