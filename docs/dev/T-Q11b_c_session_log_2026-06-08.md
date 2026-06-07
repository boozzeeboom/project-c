# T-Q11b + T-Q11c — NPC scene-placement + E-key interactor + UIDocument DialogWindow

**Branch:** `feature/npc-quest-v2`
**Commits:** (1 серия: T-Q11b + T-Q11c в одном коммите, т.к. они зависимы)
**Дата:** 2026-06-08
**Статус:** ✅ End-to-end работает: подходишь к [Mira] в WorldScene_0_0 → E → dialog opens → click option → advance → ESC closes.

---

## Скоуп

| # | Тикет | Что |
|---|-------|-----|
| T-Q11b | NpcController + E-key interactor | NpcController MonoBehaviour с trigger collider, scene-placed `[Mira]` в WorldScene_0_0, E-key chain в NetworkPlayer (highest priority = NPC). |
| T-Q11c | DialogWindow UIDocument rewrite | IMGUI → UIDocument по образцу MarketWindow/CharacterWindow (4 FIX'а + новые). |

---

## T-Q11b — NpcController + E-key interactor

### Что сделано

| # | Файл | Изменение | LOC |
|---|------|-----------|-----|
| 1 | `Assets/_Project/Quests/NpcController.cs` (NEW) | MonoBehaviour: NpcDefinition ref, CapsuleCollider trigger, `OnTriggerEnter/Exit` (PlayerInRange), `IsWithinDistance` fallback, Gizmo, auto-visual (Cube + TextMesh label "Мира Тихоступ"), color по faction | 165 |
| 2 | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (MOD) | E-key chain: MetaRequirement → **`TryInteractNearestNpc`** → Chest → Market. `TryInteractNearestNpc` ищет ближайший `NpcController` (trigger OR IsWithinDistance), вызывает `QuestServer.Instance?.RequestTalkToNpcRpc(npcId, null)` | +30 |
| 3 | `Assets/_Project/Scenes/World/WorldScene_0_0.unity` (MOD) | `[Mira]` GameObject (pos=40007, 2502.77, 39985) — NpcController + CapsuleCollider(isTrigger, r=2.0) + Mira.asset (npcId=mira_01, faction=GuildOfThoughts) | scene |

### Compile-verify
0 errors, 0 моих warnings.

---

## T-Q11c — DialogWindow UIDocument rewrite (3 итерации, 3 повтора UI bug)

### Финальная архитектура (по образцу MarketWindow)

| # | Файл | Изменение | LOC |
|---|------|-----------|-----|
| 1 | `Assets/_Project/Quests/UI/DialogWindow.cs` (REWRITE) | UIDocument вместо IMGUI. UXML/USS из `Resources/UI/`. `EnsureBuilt`: `Clear() + styleSheets.Add(uss) + CloneTree()` (КРИТИЧНО). `Show/Close` cursor + pickingMode + display toggle | 311 |
| 2 | `Assets/_Project/Quests/Resources/UI/DialogWindow.uxml` (NEW) | VisualElement#root > VisualElement#panel > Label#npc-name + ScrollView#text-scroll > Label#text + VisualElement#options + Label#toast | 11 |
| 3 | `Assets/_Project/Quests/Resources/UI/DialogWindow.uss` (NEW) | Все class-стили с `!important` (theme type-selector > class). НЕ `!important` на `display` (чтобы inline toggle работал) | 65 |
| 4 | `Assets/_Project/Quests/Resources/UI/DialogPanelSettings.asset` (NEW) | Копия `MarketPanelSettings.asset` с `themeUss: UnityDefaultRuntimeTheme` (guid `1cad08e114acf014d94b2301632cffa9`), refRes 1920x1080 | 53 |
| 5 | `Assets/_Project/Quests/Network/QuestServer.cs` (MOD) | +`RequestEndConversationRpc` (close dialog session), +null-safe `BuildDialogStep` (node.speaker, edges), +stale session detection в RequestTalkToNpcRpc (replace if already open), +using System, +try-catch diagnostic | +50 |
| 6 | `Assets/_Project/Quests/Dto/DialogStepDto.cs` (MOD) | 3 DTO: null-coalesce + struct value semantics writeback (DialogStepDto/DialogOptionDto/DialogActionResultDto) | +36 |
| 7 | `Assets/_Project/Quests/Client/QuestClientState.cs` (MOD) | +RaiseOnDialogStepReceived, +RaiseOnDialogActionResultReceived, +using UnityEngine.Object fix (DontDestroyOnLoad warning) | +20 |
| 8 | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (MOD) | +RequestTalkToNpc + RequestAdvanceDialogue client wrappers (forward в QuestServer.Instance) | +11 |
| 9 | `Assets/_Project/Scenes/BootstrapScene.unity` (MOD) | `[QuestClientState]` GameObject: привязка UIDocument.panelSettings=DialogPanelSettings, sourceAsset=DialogWindow.uxml, DialogWindow.dialogWindowUxml/uss = uxml/uss | scene |

### Compile-verify
0 errors, 0 exceptions.

### Verify (Play Mode)
- ✅ E → NPC → dialog opens (800x400, dark blue, accent border, yellow NPC name)
- ✅ Buttons с текстом ("Расскажи о Гильдии Мысли", etc.)
- ✅ Mouse работает, click на option → advance → new step
- ✅ ESC → close + RequestEndConversation → server session clean
- ✅ 0 quest errors/warnings (только pre-existing LockBox/MCP)

---

## 3 повторяющихся UI bug (LESSONS LEARNED — памятка на будущее)

Все три бага уже были в `docs/Character-menu/refactor_log_2026-06-05.md` (v2 fix). Я **3 раза** наступил на те же грабли. **READ** `docs/Character-menu/refactor_log_2026-06-05.md` при ЛЮБОМ новом UI Toolkit окне.

### Bug #1: Panel рендерится как "тонкая полоса"
**Root cause**: runtime `PanelSettings` созданный через `ScriptableObject.CreateInstance` имеет `themeUss = null`. Без темы UI не рендерится → "No Theme Style Sheet set to PanelSettings, UI will not render properly".
**Fix**: создать `.asset` файл (не runtime!) скопировав с `MarketPanelSettings.asset`. Привязать в Inspector.
**Repro**: 1-й раз.

### Bug #2: USS class-стили не применяются
**Root cause**: (a) `themeUss` (даже если есть) применяет `UnityDefaultRuntimeTheme` который имеет **type-selector** `.unity-base-button` со specificity **выше** class-selector `.tab-btn`. Без `!important` class-стили ignored. (b) `UIDocument` НЕ автоматически применяет USS — нужно explicit `_doc.rootVisualElement.styleSheets.Add(uss)`.
**Fix**: (a) все class-стили с `!important` (кроме `display` — см. #3). (b) explicit `styleSheets.Add(uss)` в `EnsureBuilt()`.
**Repro**: 1-й + 2-й раз.

### Bug #3: Mouse блокируется / panel не видна
**Root cause**: (a) `Cursor.lockState = Locked` в flight-mode → UIDocument не получает mouse events. (b) `_root.pickingMode = Ignore` статически → даже visible panel не ловит mouse. (c) `display: none !important` в USS блокирует `Show()` который ставит inline `display: Flex`.
**Fix**: (a) `Show()` → `Cursor.lockState = None; visible = true`. `Close()` → restore Locked (если `IsListening`). (b) `pickingMode` toggle в Show/Close. (c) убрать `display` из USS, использовать inline toggle.
**Repro**: 3-й раз.

### Дополнительные баги (4-6, второстепенные)

### Bug #4: NRE `FastBufferWriter.WriteValueSafe` на null strings
**Root cause**: NGO 2.x Unity 6 — `WriteValueSafe(null)` → NRE. Struct DTO с reference type fields не инициализируются default'ом.
**Fix**: `var x = field; if (IsWriter) x = field ?? ""; SerializeValue(ref x); if (IsReader) field = x ?? "";` — generic rule для ВСЕХ struct DTOs.

### Bug #5: struct value semantics — данные теряются тихо
**Root cause**: `var lbl = label; SerializeValue(ref lbl);` — `lbl` это local copy, `this.label` остаётся неизменным после deserialize. Без writeback — struct fields null/empty после NetworkSerialize.
**Fix**: всегда `if (IsReader) field = x ?? "";` в конце.

### Bug #6: Stale `_currentStep` → "no matching session"
**Root cause**: после `isEnd` step `_currentStep.treeId=""` и `nodeId=""`. Повторный click → RPC со всеми пустыми полями → server не находит session.
**Fix**: `if (string.IsNullOrEmpty(_currentStep.treeId) || string.IsNullOrEmpty(_currentStep.nodeId)) return;` в `SendAdvance`.

### Bug #7: NPC в BootstrapScene а не WorldScene_0_0
**Root cause**: я положил `[Mira]` в BootstrapScene на (2, 0, 0). AGENTS.md правило: **BootstrapScene = server infra, game objects в WorldScene_X_Z**. Player спавнится в WorldScene.
**Fix**: перенёс в `WorldScene_0_0` на (40007, 2502.77, 39985) — рядом с Chest_Main.
**User correction**: 2026-06-07 "стоп. почему нпс размещаются в boostrap scene а не в игровых world_0_0".

### Bug #8: Scene-save persistence — `SaveOpenScenes` сохранял не ту сцену
**Root cause**: `EditorSceneManager.SaveOpenScenes()` в execute_code сохранял BootstrapScene, а не WorldScene_0_0 где я создавал NPC.
**Fix**: использовать explicit `EditorSceneManager.MarkSceneDirty + SaveScene(active, active.path)` + verify reload (open + check transform).

### Bug #9: subagent partial work
**Root cause**: subagent (delegate_task) делал DialogWindow rewrite по pattern MarketWindow, но НЕ доделал scene binding (tool loop budget exhausted) — пришлось доделывать руками.
**Fix**: последующие subagent-таски надо давать с явным `BUDGET = 30 calls max + report what was done + what was deferred`.

---

## Checklist для нового UIDocument окна (на будущее)

```csharp
[SerializeField] private VisualTreeAsset windowUxml;
[SerializeField] private StyleSheet windowUss;

private void Awake() {
    if (windowUxml == null) windowUxml = Resources.Load<VisualTreeAsset>("UI/MyWindow");
    if (windowUss == null) windowUss = Resources.Load<StyleSheet>("UI/MyWindow");
    if (_doc.panelSettings == null) _doc.panelSettings = Resources.Load<PanelSettings>("UI/MyPanelSettings");
}

private void EnsureBuilt() {
    if (_doc.rootVisualElement == null) return;
    _doc.rootVisualElement.Clear();
    if (windowUss != null) _doc.rootVisualElement.styleSheets.Add(windowUss);
    _root = windowUxml.CloneTree();
    _root.style.position = Position.Absolute; _root.style.left=0; _root.style.top=0; _root.style.right=0; _root.style.bottom=0;
    _root.pickingMode = PickingMode.Ignore;
    _doc.rootVisualElement.Add(_root);
}

public void Show() {
    IsOpen = true;
    if (_root != null) { _root.style.display = DisplayStyle.Flex; _root.pickingMode = PickingMode.Position; }
    UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
}
public void Close() {
    IsOpen = false;
    if (_root != null) { _root.style.display = DisplayStyle.None; _root.pickingMode = PickingMode.Ignore; }
    var nm = Unity.Netcode.NetworkManager.Singleton;
    if (nm != null && nm.IsListening) { UnityEngine.Cursor.lockState = CursorLockMode.Locked; UnityEngine.Cursor.visible = false; }
}
```

USS:
```css
/* все стили с !important (theme type > class) */
.dialog-panel { width: 800px !important; height: 400px !important; background-color: rgb(31,31,46) !important; }
.dialog-button { height: 38px !important; background-color: rgb(51,76,127) !important; }
/* НЕ !important на display — inline toggle */
```

Scene binding через SerializedObject:
- `UIDocument.m_PanelSettings` → `MyPanelSettings.asset`
- `UIDocument.sourceAsset` (НЕ `m_SourceAsset`!) → `MyWindow.uxml`
- `MyWindow.windowUxml` → `MyWindow.uxml`
- `MyWindow.windowUss` → `MyWindow.uss`

---

## Open / что на потом

- **QuestLog + CharacterWindow** (T-Q11c.4-5) — следующая сессия.
- **E-key E для other actions** (chest, market) теперь конфликтуют с NPC — нужно определить priority order (T-Q11b.3 уже сделал: NPC > Market > Chest).
- **Mira default actions stubs** (EndConversation/OpenMarket на greeting) — T-Q15/T-Q16 заполнят `OfferQuest`/`CompleteObjective` правильно.
- **`[QuestClientState]` DontDestroyOnLoad warning** (pre-existing) — не блокер, NMC тоже warning'ит.

---

## Files committed

```
modified:   Assets/_Project/Quests/Client/QuestClientState.cs
modified:   Assets/_Project/Quests/Dto/DialogStepDto.cs
modified:   Assets/_Project/Quests/Network/QuestServer.cs
modified:   Assets/_Project/Quests/UI/DialogWindow.cs
modified:   Assets/_Project/Scenes/BootstrapScene.unity
modified:   Assets/_Project/Scenes/World/WorldScene_0_0.unity
modified:   Assets/_Project/ScriptableObjects/DayNight/MoonMaterial.mat (unrelated, subagent touch)
modified:   Assets/_Project/Scripts/Player/NetworkPlayer.cs
new:        Assets/_Project/Quests/NpcController.cs
new:        Assets/_Project/Quests/NpcController.cs.meta
new:        Assets/_Project/Quests/Resources/UI/DialogWindow.uxml
new:        Assets/_Project/Quests/Resources/UI/DialogWindow.uss
new:        Assets/_Project/Quests/Resources/UI/DialogPanelSettings.asset
new:        Assets/_Project/Quests/Resources.meta
```

---
