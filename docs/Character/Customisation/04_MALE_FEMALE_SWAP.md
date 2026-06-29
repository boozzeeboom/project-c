# Male ↔ Female Swap — глубокий разбор

> **Дата:** 2026-06-30
> **Цель:** детальный технический разбор переключения персонажа между M и F. Это уровень **L1** из `03_LEVELS_OF_CUSTOMISATION.md` — наш первый приоритет.
> **Скоуп:** что менять в NetworkPlayer, как создать F-версию AnimatorOverrideController, как работает mesh swap, что делать с SkillAnimationPlayer и CharacterEquipmentVisualApplier, как тестировать.

---

## 0. TL;DR

| Аспект | Решение |
|---|---|
| **Главный факт** | `HumanM_Model.fbx` и `HumanF_Model.fbx` используют **одинаковый generic Humanoid skeleton** (HumanBodyBones enum покрывает оба). |
| **Animator State Machine** | Не меняется. Один `PlayerAnimation.controller` + два AnimatorOverrideController-а (M и F) → runtime swap. |
| **Mesh** | `SkinnedMeshRenderer.sharedMesh` swap. Никакого re-skin, parent-relations к костям сохраняются. |
| **Skill animation** | SkillAnimationPlayer подменяет motion в state "Skill" через свой override controller. Работает прозрачно. |
| **Equipment visuals** | CharacterEquipmentVisualApplier держит spawned visuals как children костей. При смене меша — visuals **остаются** (parent к bone, не к SMR). |
| **Stats / Combat / Skills** | Никаких изменений. Подсистемы не знают о поле. |
| **Persistence** | `CharacterSaveData.customisation.bodyType` (1 байт). |
| **Трудоёмкость** | ~3-5 дней одного разработчика. |

---

## 1. Почему M↔F swap тривиален

### 1.1 Один skeleton, два mesh

В `Assets/Kevin Iglesias/Human Animations/Models/`:
- `HumanM_Model.fbx` — мужская модель, humanoid rig.
- `HumanF_Model.fbx` — женская модель, **тот же humanoid rig**.

Unity import для обоих настроен как **Humanoid avatar** (`Avatar Configuration` в Inspector). Это значит:
- Один и тот же набор костей (Head, Spine, Hips, LeftHand, RightHand, ...).
- Один и тот же `HumanBodyBones` enum (52 кости).
- `Animator.GetBoneTransform(HumanBodyBones.Head)` возвращает корректную Transform на обоих мешах.

**Следствие:** `EquipSlotToBone.TryGetBoneTransform(slot, animator, out bone)` работает **одинаково** для M и F. Никаких изменений в `CharacterEquipmentVisualApplier` или `EquipSlotToBone`.

### 1.2 Анимации совпадают по именам

В `Assets/Kevin Iglesias/Human Animations/Animations/`:

| Male | Female |
|---|---|
| `Male/Movement/Walk/HumanM@Walk01_Forward.fbx` | `Female/Movement/Walk/HumanF@Walk01_Forward.fbx` |
| `Male/Movement/Run/HumanM@Run01_Forward.fbx` | `Female/Movement/Run/HumanF@Run01_Forward.fbx` |
| `Male/Idles/HumanM@Idle01.fbx` | `Female/Idles/HumanF@Idle01.fbx` |
| `Male/Movement/Jump/HumanM@Jump01.fbx` | `Female/Movement/Jump/HumanF@Jump01.fbx` |

**Имена клипов:** `Walk01_Forward`, `Run01_Forward`, `Idle01`, `Jump01`, ... — совпадают.

**Следствие:** AnimatorOverrideController для F создаётся **drag-and-drop'ом** — для каждого motion в state-машине указываем соответствующий F-клип.

### 1.3 Skill клипы — отдельная история

`SkillAnimationPlayer.Play(SkillNodeConfig skill, ...)` использует **свой собственный** AnimatorOverrideController для подмены motion в state "Skill" на `skill.attackClip`. Это работает **поверх** текущего `runtimeAnimatorController` (M или F) — потому что `AnimatorOverrideController.this[name]` подменяет motion в **корневом** controller-е, который может быть override-ом M или F.

**Следствие:** если игрок сменил пол на F, и потом активировал skill — SkillAnimationPlayer создаст F-aware override controller (потому что `Animator.runtimeAnimatorController` теперь F-версия). Skill clips сами по себе не gendered (это анимации типа "Standing Melee Attack 360 Low") — но они применяются через Humanoid retargeting, который работает с любым скелетом, удовлетворяющим humanoid rig.

---

## 2. Что нужно создать

| Файл | Тип | Трудоёмкость |
|---|---|---|
| `PlayerAnimation_Female.overrideController` | AnimatorOverrideController asset | 5 минут (Editor drag-and-drop) |
| `Assets/_Project/Resources/Customisation/HumanM_Avatar.asset` (опционально) | Avatar reference | 5 минут |
| `Assets/_Project/Resources/Customisation/HumanF_Avatar.asset` (опционально) | Avatar reference | 5 минут |
| `Assets/_Project/Scripts/Customisation/CharacterBodyType.cs` | enum | 1 минута |
| `Assets/_Project/Scripts/Customisation/CustomisationSave.cs` | [Serializable] class | 10 минут |
| `Assets/_Project/Scripts/Customisation/Dto/CustomisationSnapshotDto.cs` | struct | 5 минут |
| `Assets/_Project/Scripts/Customisation/CustomisationClientState.cs` | MonoBehaviour singleton | 20 минут |
| `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` | MonoBehaviour | 30 минут |
| `Assets/_Project/Editor/SetupCharacterCustomisationApplier.cs` | Editor script | 10 минут |
| Расширение `CharacterSaveData.cs` | +1 поле | 1 минута |
| Расширение `NetworkPlayer.cs` | +1 AddComponent | 5 минут |
| UI: sub-tab "ВНЕШНОСТЬ" в CharacterWindow | UXML + handler | 1-2 часа |

**Итого: ~10-15 файлов, 3-5 дней.**

---

## 3. Создание PlayerAnimation_Female.overrideController

### 3.1 Пошагово (Editor)

1. **В Unity Editor:**
   - `Project` → `Assets/_Project/Animations/`
   - Правый клик → `Create` → `Animator Override Controller`
   - Имя: `PlayerAnimation_Female`
   - В Inspector: **Controller** = `PlayerAnimation.controller` (тот же что для M).

2. **Подменить motion-ы** — для каждого state в стейт-машине с motion перетащить F-версию:
   - `Walk01_Forward` → `Female/Movement/Walk/HumanF@Walk01_Forward.fbx` (clip "Walk01_Forward")
   - `Walk01_Backward`, `Walk01_Left`, `Walk01_Right`, `Walk01_ForwardLeft`, `Walk01_ForwardRight`, `Walk01_BackwardLeft`, `Walk01_BackwardRight` → соответствующие F-клипы.
   - `Run01_*` → F-версии.
   - `Sprint01_*` → F-версии.
   - `StrafeRun01_*` → F-версии (если используются).
   - `Jump01`, `Fall01`, `Land01` → F-версии.
   - `Idle01`, `Idle02`, `Idle01-Idle02`, `Idle02-Idle01` → F-версии.
   - `Turn01_Left`, `Turn01_Right` → F-версии.
   - `Combat*` → F-версии (Attack1H, Attack2H, Polearm, Shield, Thrown).
   - `Death01`, `CombatDeath01` → F-версии.
   - `Gathering01/02/03`, `Mining*`, `Hammering*`, `Fishing*`, `Farming*` → F-версии (опционально — для полноты).

3. **Не трогать:**
   - `Mine`, `Lumber`, `Gather`, `Skill`, `Attack1H` state-ы если motion = null (placeholder для runtime skill подмены через `SkillAnimationPlayer`).
   - BlendTree-ы — Unity сам подменит child motion-ы если имена совпадают.

4. **Save asset.**

### 3.2 Альтернатива: кодогенерация через Editor script

Если Designer хочет сделать это программно (быстрее для ~50 motion-ов):

```csharp
// Assets/_Project/Editor/SetupFemaleAnimationOverride.cs
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectC.Editor
{
    public static class SetupFemaleAnimationOverride
    {
        [MenuItem("Tools/ProjectC/Player/Setup Female Animation Override")]
        public static void Setup()
        {
            const string baseControllerPath = "Assets/_Project/Animations/PlayerAnimation.controller";
            const string overridePath = "Assets/_Project/Animations/PlayerAnimation_Female.overrideController";

            var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(baseControllerPath);
            if (baseController == null) { Debug.LogError("Base controller not found"); return; }

            AnimatorOverrideController overrideCtrl = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if (overrideCtrl == null)
            {
                overrideCtrl = new AnimatorOverrideController(baseController);
                AssetDatabase.CreateAsset(overrideCtrl, overridePath);
            }

            // Собрать все motion-ы из base controller.
            var clips = new System.Collections.Generic.List<KeyValuePair<AnimationClip, AnimationClip>>();
            GatherClips(baseController, clips);

            int swapped = 0;
            foreach (var kvp in clips)
            {
                var maleClip = kvp.Key;
                if (maleClip == null) continue;

                // Ищем F-версию: заменяем "HumanM@" на "HumanF@" в пути.
                string malePath = AssetDatabase.GetAssetPath(maleClip);
                string femalePath = malePath.Replace("/Male/", "/Female/").Replace("HumanM@", "HumanF@");
                var femaleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(femalePath);
                if (femaleClip != null)
                {
                    overrideCtrl[maleClip] = femaleClip;
                    swapped++;
                }
                else
                {
                    Debug.LogWarning($"[SetupFemale] F-version not found for '{maleClip.name}' at '{femalePath}'");
                }
            }

            EditorUtility.SetDirty(overrideCtrl);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupFemale] Swapped {swapped}/{clips.Count} clips → {overridePath}");
        }

        private static void GatherClips(AnimatorController ctrl, List<KeyValuePair<AnimationClip, AnimationClip>> clips)
        {
            foreach (var layer in ctrl.layers)
            {
                GatherFromStateMachine(layer.stateMachine, clips);
            }
        }

        private static void GatherFromStateMachine(AnimatorStateMachine sm, List<KeyValuePair<AnimationClip, AnimationClip>> clips)
        {
            foreach (var state in sm.states)
            {
                if (state.state.motion is AnimationClip clip) clips.Add(new(clip, null));
                if (state.state.motion is BlendTree tree) GatherFromBlendTree(tree, clips);
            }
            foreach (var sub in sm.stateMachines)
                GatherFromStateMachine(sub.stateMachine, clips);
        }

        private static void GatherFromBlendTree(BlendTree tree, List<KeyValuePair<AnimationClip, AnimationClip>> clips)
        {
            foreach (var child in tree.children)
                if (child.motion is AnimationClip c) clips.Add(new(c, null));
        }
    }
}
```

**Запуск:** `Tools` → `ProjectC` → `Player` → `Setup Female Animation Override`. За 1 секунду подменяет все motion-ы по path convention.

---

## 4. Добавление компонента на NetworkPlayer.prefab

### 4.1 Через Editor script (по аналогии с SetupEquipmentVisualApplier)

**Новый файл:** `Assets/_Project/Editor/SetupCharacterCustomisationApplier.cs`

```csharp
using UnityEditor;
using UnityEngine;

namespace ProjectC.Editor
{
    public static class SetupCharacterCustomisationApplier
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/NetworkPlayer.prefab";
        private const string ComponentTypeName = "ProjectC.Player.CharacterCustomisationApplier";

        [MenuItem("Tools/ProjectC/Player/Add CharacterCustomisationApplier to NetworkPlayer")]
        public static void AddComponent()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("[Setup] Prefab not found: " + PrefabPath); return; }

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (contents.GetComponent(System.Type.GetType(ComponentTypeName)) != null)
                {
                    Debug.Log("[Setup] CharacterCustomisationApplier already present, skipping.");
                    return;
                }

                contents.AddComponent(System.Type.GetType(ComponentTypeName));
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[Setup] CharacterCustomisationApplier added to NetworkPlayer.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
```

**Запуск:** `Tools` → `ProjectC` → `Player` → `Add CharacterCustomisationApplier to NetworkPlayer`.

### 4.2 Через MCP

```python
mcp.manage_components(action="add", target="NetworkPlayer.prefab", component_type="ProjectC.Player.CharacterCustomisationApplier")
mcp.refresh_unity()
mcp.read_console()
```

---

## 5. Полный код CharacterCustomisationApplier (L1: M↔F swap)

**Новый файл:** `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs`

```csharp
// Project C: Character Customisation — T-CUS-05 (Phase 1, L1: M↔F swap)
// CharacterCustomisationApplier: применяет CustomisationSnapshotDto к визуалу персонажа.
//
// Phase 1 (L1): mesh swap + AnimatorOverrideController swap (M↔F).
// Phase 2 (L3-L4): пропорции (transform.localScale) + покраска (MaterialPropertyBlock).
//
// Pattern: copy CharacterEquipmentVisualApplier (Phase 2, 2026-06-29).
// Триггер: CustomisationClientState.OnCustomisationUpdated (T-CUS-03).
//
// Additive-only: новый компонент на NetworkPlayer.prefab.
//   - Не модифицирует NetworkPlayer.Update.
//   - Не модифицирует Stats/Equipment/Skills.
//   - Не модифицирует SkillAnimationPlayer (продолжает работать с любым runtimeAnimatorController).
//   - Не модифицирует CharacterEquipmentVisualApplier (spawned visuals — child кости, не меш).

using UnityEngine;
using ProjectC.Customisation;
using ProjectC.Customisation.Dto;

namespace ProjectC.Player
{
    [DisallowMultipleComponent]
    public class CharacterCustomisationApplier : MonoBehaviour
    {
        [Header("Refs (auto-found если пусто)")]
        [Tooltip("Child Transform с моделью персонажа (обычно 'Visual_Model'). Если пусто — ищем первый child с SkinnedMeshRenderer.")]
        [SerializeField] private Transform _visualRoot;
        [Tooltip("Animator на Visual_Model. Если пусто — GetComponentInChildren на _visualRoot.")]
        [SerializeField] private Animator _animator;
        [Tooltip("SkinnedMeshRenderer основного тела персонажа. Если пусто — GetComponentInChildren на _visualRoot.")]
        [SerializeField] private SkinnedMeshRenderer _bodyRenderer;

        [Header("Body meshes (Phase 1)")]
        [Tooltip("Mesh для Male (HumanM_Model.sharedMesh). Назначить в Inspector из 'Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx'.")]
        [SerializeField] private Mesh _maleMesh;
        [Tooltip("Mesh для Female (HumanF_Model.sharedMesh). Назначить в Inspector из 'Assets/Kevin Iglesias/Human Animations/Models/HumanF_Model.fbx'.")]
        [SerializeField] private Mesh _femaleMesh;

        [Header("Override Controllers (Phase 1)")]
        [Tooltip("AnimatorOverrideController для Male. Назначить PlayerAnimation_Default.overrideController.")]
        [SerializeField] private RuntimeAnimatorController _maleController;
        [Tooltip("AnimatorOverrideController для Female. Назначить PlayerAnimation_Female.overrideController.")]
        [SerializeField] private RuntimeAnimatorController _femaleController;

        [Header("Behavior")]
        [SerializeField] private bool _logWarnings = true;

        // === Runtime state ===

        private CustomisationSnapshotDto _currentSnapshot;
        private bool _hasSnapshot;
        private CustomisationClientState _clientState;

        // === Lifecycle ===

        private void Awake()
        {
            AutoFindRefs();
        }

        private void AutoFindRefs()
        {
            if (_visualRoot == null)
            {
                // Ищем child с SkinnedMeshRenderer (Visual_Model).
                var smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null) _visualRoot = smr.transform;
            }

            if (_animator == null && _visualRoot != null)
            {
                var animators = _visualRoot.GetComponentsInChildren<Animator>(true);
                foreach (var a in animators)
                {
                    if (a != null && a.runtimeAnimatorController != null) { _animator = a; break; }
                }
            }

            if (_bodyRenderer == null && _visualRoot != null)
            {
                _bodyRenderer = _visualRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
            }
        }

        private void OnEnable()
        {
            _clientState = CustomisationClientState.Instance;
            if (_clientState == null)
            {
                if (_logWarnings)
                {
                    Debug.LogWarning("[CharacterCustomisationApplier] CustomisationClientState.Instance == null — visual customisation skipped.", this);
                }
                return;
            }
            _clientState.OnCustomisationUpdated += OnCustomisationUpdated;
            if (_clientState.CurrentSnapshot.bodyType != CharacterBodyType.Male || _clientState.CurrentSnapshot.hairStyle != HairStyleId.Short)
            {
                // Non-default snapshot already received → apply now.
                OnCustomisationUpdated(_clientState.CurrentSnapshot);
            }
        }

        private void OnDisable()
        {
            if (_clientState != null)
            {
                _clientState.OnCustomisationUpdated -= OnCustomisationUpdated;
                _clientState = null;
            }
        }

        // === Snapshot handler ===

        private void OnCustomisationUpdated(CustomisationSnapshotDto snapshot)
        {
            if (_bodyRenderer == null || _animator == null)
            {
                if (_logWarnings)
                {
                    Debug.LogWarning("[CharacterCustomisationApplier] Body renderer / animator not assigned — visual swap skipped.", this);
                }
                return;
            }

            // === L1: Body type (mesh + animator controller swap) ===
            if (!_hasSnapshot || _currentSnapshot.bodyType != snapshot.bodyType)
            {
                ApplyBodyType(snapshot.bodyType);
            }

            _currentSnapshot = snapshot;
            _hasSnapshot = true;
        }

        // === L1: ApplyBodyType ===

        private void ApplyBodyType(CharacterBodyType bodyType)
        {
            Mesh targetMesh = bodyType == CharacterBodyType.Female ? _femaleMesh : _maleMesh;
            RuntimeAnimatorController targetCtrl = bodyType == CharacterBodyType.Female ? _femaleController : _maleController;

            if (targetMesh == null)
            {
                if (_logWarnings) Debug.LogWarning($"[CharacterCustomisationApplier] {bodyType} mesh not assigned in Inspector.", this);
            }
            else
            {
                _bodyRenderer.sharedMesh = targetMesh;
            }

            if (targetCtrl == null)
            {
                if (_logWarnings) Debug.LogWarning($"[CharacterCustomisationApplier] {bodyType} controller not assigned in Inspector.", this);
            }
            else
            {
                _animator.runtimeAnimatorController = targetCtrl;
                // Reset trigger-ы чтобы не было залипших state-ов после смены controller-а.
                foreach (var p in _animator.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Trigger)
                        _animator.ResetTrigger(p.nameHash);
                }
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[CharacterCustomisationApplier] Applied bodyType={bodyType} (mesh='{(targetMesh != null ? targetMesh.name : "null")}', ctrl='{(targetCtrl != null ? targetCtrl.name : "null")}').", this);
            }
        }

        // === Public API (для тестов) ===

        public CharacterBodyType CurrentBodyType => _hasSnapshot ? _currentSnapshot.bodyType : CharacterBodyType.Male;

        [ContextMenu("DEBUG: Force re-apply current snapshot")]
        public void DebugReapply()
        {
            if (_clientState != null && _clientState.CurrentSnapshot.bodyType != CharacterBodyType.Male)
            {
                OnCustomisationUpdated(_clientState.CurrentSnapshot);
            }
            else if (_logWarnings)
            {
                Debug.LogWarning("[CharacterCustomisationApplier] No client state / snapshot — nothing to re-apply.", this);
            }
        }
    }
}
```

**~140 строк. Минимально для L1.** L2-L4 расширения добавляются позже через дополнительные методы.

---

## 6. Как это работает в runtime — сценарий

### 6.1 Host запускается (CharacterSaveData: Male)

1. `StatsServer` загружает `character_0.json` → `customisation = new CustomisationSave()` → `bodyType = Male`.
2. `StatsServer` отправляет `CustomisationSnapshotDto{Male}` через target RPC → `NetworkPlayer`.
3. `NetworkPlayer` вызывает `CustomisationClientState.OnCustomisationSnapshotReceived(snapshot)`.
4. `CustomisationClientState.CurrentSnapshot = snapshot` → fires `OnCustomisationUpdated`.
5. `CharacterCustomisationApplier.OnCustomisationUpdated(snapshot)` → первый раз: `bodyType = Male` → `ApplyBodyType(Male)` → swap к M mesh + M controller (но он уже установлен — no-op).
6. **Ничего не меняется.** Персонаж как раньше.

### 6.2 Игрок переключает на Female через UI

1. UI sub-tab "ВНЕШНОСТЬ" → кнопка "Ж" → handler вызывает `CustomisationClientState.RequestChange(bodyType=Female)` (RPC на сервер).
2. Сервер: `CustomisationServer.RequestChangeRpc(playerId, bodyType=Female)` → валидация → `customisation.bodyType = Female` → save через `JsonCharacterDataRepository` → broadcast snapshot через target RPC.
3. Клиент: `CustomisationClientState.OnCustomisationSnapshotReceived(snapshot={Female, ...})` → fires `OnCustomisationUpdated`.
4. `CharacterCustomisationApplier.OnCustomisationUpdated(snapshot)` → diff: `_currentSnapshot.bodyType = Male ≠ snapshot.bodyType = Female` → `ApplyBodyType(Female)`.
5. `ApplyBodyType(Female)`:
   - `_bodyRenderer.sharedMesh = _femaleMesh` (мгновенно).
   - `_animator.runtimeAnimatorController = _femaleController` (на следующем Animator.Update клипы подменяются).
   - Reset всех trigger-ов чтобы не было залипания.
6. **Персонаж мгновенно становится F.** Анимации идут на F-клипах. Skill-ы продолжают работать.

### 6.3 Игрок потом активирует Skill

1. `SkillInputService.TryActivate(Primary)` → `SkillAnimationPlayer.Play(skillNodeConfig, slot)`.
2. `SkillAnimationPlayer` создаёт **свой** `AnimatorOverrideController` поверх текущего `_animator.runtimeAnimatorController` (= F-версия).
3. Подменяет motion в state "Skill" на `skill.attackClip` → SetTrigger("SkillPlay").
4. Проигрывается skill animation (Humanoid retargeting работает с любым humanoid skeleton).
5. После окончания — `Restore()` → возвращает runtimeAnimatorController = F-версия.

**Skill pipeline не замечает разницы между M и F.**

### 6.4 Игрок надевает шлем (после M↔F swap)

1. `EquipmentServer.TryEquip(helmetId)` → broadcast `EquipmentSnapshotDto`.
2. `EquipmentClientState.OnEquipmentUpdated` → fires.
3. `CharacterEquipmentVisualApplier.OnEquipmentUpdated(snapshot)` → diff → `SpawnVisual(Head, helmetItemData)`.
4. `SpawnVisual` → resolve bone = `animator.GetBoneTransform(HumanBodyBones.Head)` → instantiate helmet prefab → SetParent(bone, ...).
5. **Шлем висит на голове** (parent к Head bone — работает для M и F одинаково, потому что skeleton generic humanoid).

**Equipment pipeline не замечает разницы между M и F.**

---

## 7. Что может пойти не так (edge cases)

### 7.1 Animator не humanoid (если кто-то заменит Visual_Model на non-humanoid mesh)

**Симптом:** `Animator.GetBoneTransform(HumanBodyBones.Head)` возвращает `null`.

**Где ломается:**
- `ApplyBodyType` — mesh swap работает, но bones не находятся.
- `CharacterEquipmentVisualApplier.SpawnVisual` — warning + skip.
- `EquipSlotToBone.TryGetBoneTransform` — возвращает false.

**Решение:** в текущем коде уже есть guards. Если mesh не humanoid — equipment не спавнится, но кастомизация работает (mesh swap виден).

### 7.2 Animator isHuman == true, но Avatar configuration отсутствует

**Симптом:** `Animator.isHuman == false`, хотя mesh импортирован как Humanoid.

**Решение:** проверить Inspector на fbx: `Rig` → `Animation Type = Humanoid` → `Apply`. Если Avatar не сгенерирован — кнопка "Configure...".

### 7.3 F-mesh имеет другие bone-weights (другая топология)

**Маловероятно:** Kevin Iglesias F-Model использует тот же скелет и ту же топологию mesh (только разные пропорции и текстуры).

**Если всё же:** визуально будет видно как "дёргается" одежда при смене пола. Решается подгонкой bone weights или использованием общего skeleton для clothing (Phase 3.4 в `EquipmentVisual/03_PHASES.md`).

### 7.4 Network race — server snapshot приходит до того как Animator initialized

**Симптом:** `OnCustomisationUpdated` срабатывает до `_animator` готов.

**Решение:** в `OnEnable` проверяем `if (_clientState.CurrentSnapshot.bodyType != default)` — если да, делаем немедленный apply. Плюс в `ApplyBodyType` есть guards (warning + skip если null).

### 7.5 Equip visuals были созданы на M, потом swap на F, equip нового

**Сценарий:**
1. Персонаж — M, надел шлем → spawned `Visual_Head_HelmetCone` на Head bone (parent к Head кости).
2. Swap на F → mesh = F, animator = F controller.
3. Шлем **остался** на Head bone (parent к кости, не к SMR).
4. Надевает ботинки → новый `Visual_Feet_BootsCone` спавнится на Foot bone.
5. **Оба visual'а на новом теле.** Может быть визуальный glitch если шлем сильно M-specific (но мы используем generic cone — OK).

**Решение:** ничего не нужно. Если визуальный артефакт — дизайнер подгоняет prefab (attach offsets).

### 7.6 CharacterController height не подстраивается (если у F-модели другие пропорции)

**Где важно:** F-модель Kevin Iglesias короче M (~5-10%). CharacterController.height = 1.8f может быть слишком высок для F.

**Решение:** в `NetworkPlayer.Awake` или `ApplyBodyType` подстраивать `characterController.height` и `center` под bodyType. **Не критично для L1** (визуально незаметно на статичной сцене), но стоит добавить в Phase 2.

---

## 8. UI sub-tab "ВНЕШНОСТЬ"

### 8.1 UX

```
[ПЕРСОНАЖ] [КОРАБЛЬ] [РЕПУТАЦИЯ] [КОНТРАКТЫ] [ИНВЕНТАРЬ] [КВЕСТЫ]
                          ↓ (выбран ПЕРСОНАЖ)
[Одежда] [Модули] [Характеристики] [Скиллы] [Внешность]   ← sub-tab
                          ↓ (выбран Внешность)
┌─────────────────────────────────────────────────────┐
│  Пол:  ( ◯ Мужской   ● Женский )                    │
│                                                     │
│  [Превью персонажа]    [Применить] [Отмена]         │
│                                                     │
│  (L3) Рост:    [─────●─────] 1.0                    │
│  (L3) Полнота: [────●──────] 1.0                    │
│                                                     │
│  (L4) Цвет кожи: [██████]                           │
│  (L4) Цвет волос: [██████]                          │
└─────────────────────────────────────────────────────┘
```

### 8.2 UXML фрагмент (добавить в CharacterWindow.uxml)

```xml
<!-- Sub-tab button (добавить к существующим sub-tabs) -->
<ui:Button name="tab-customisation" text="ВНЕШНОСТЬ" class="sub-tab-btn" />

<!-- Sub-section (новый, рядом с progression sub-sections) -->
<ui:VisualElement name="customisation-sub-section" class="list-sub-section" style="display: none;">
  <ui:Label text="Внешность" class="section-title" />

  <ui:VisualElement class="customisation-row">
    <ui:Toggle name="customisation-male-toggle"   label="Мужской"   class="customisation-toggle" value="true" />
    <ui:Toggle name="customisation-female-toggle" label="Женский"   class="customisation-toggle" />
  </ui:VisualElement>

  <ui:VisualElement class="customisation-preview">
    <ui:Label text="Превью: 3D character rotates" class="placeholder-hint" />
  </ui:VisualElement>

  <!-- L3/L4 слайдеры добавляются в Phase 2 -->
</ui:VisualElement>
```

### 8.3 Handler в CharacterWindow.cs

```csharp
// В CharacterWindow.cs, в методе BuildUI() рядом с другими sub-tab обработчиками:

private Toggle _maleToggle;
private Toggle _femaleToggle;

void InitCustomisationTab()
{
    _maleToggle = _root.Q<Toggle>("customisation-male-toggle");
    _femaleToggle = _root.Q<Toggle>("customisation-female-toggle");

    if (_maleToggle != null) _maleToggle.RegisterValueChangedCallback(evt =>
    {
        if (evt.newValue) RequestBodyTypeChange(CharacterBodyType.Male);
    });
    if (_femaleToggle != null) _femaleToggle.RegisterValueChangedCallback(evt =>
    {
        if (evt.newValue) RequestBodyTypeChange(CharacterBodyType.Female);
    });
}

void RequestBodyTypeChange(CharacterBodyType bodyType)
{
    // Собираем CustomisationSnapshotDto из текущего + новый bodyType.
    var current = CustomisationClientState.Instance?.CurrentSnapshot ?? default;
    var newSnapshot = current;
    newSnapshot.bodyType = bodyType;

    // Отправляем на сервер (RPC реализован в T-CUS-04, см. 05_PHASES_ROADMAP.md).
    var player = NetworkPlayer.LocalInstance;
    if (player != null)
    {
        player.RequestCustomisationRpc(newSnapshot);  // T-CUS-04
    }
}

void RefreshCustomisationDisplay()
{
    if (CustomisationClientState.Instance == null) return;
    var snap = CustomisationClientState.Instance.CurrentSnapshot;
    if (_maleToggle != null) _maleToggle.SetValueWithoutNotify(snap.bodyType == CharacterBodyType.Male);
    if (_femaleToggle != null) _femaleToggle.SetValueWithoutNotify(snap.bodyType == CharacterBodyType.Female);
}
```

**Полная интеграция UI — отдельный тикет T-CUS-07 в `05_PHASES_ROADMAP.md`.**

---

## 9. Верификация (Play Mode smoke test)

```bash
# 1. Compile — открыть Unity Editor, проверить Console:
#    Ожидаемо: 0 errors.

# 2. Запустить `Tools/ProjectC/Player/Setup Female Animation Override` →
#    Console: "[SetupFemale] Swapped N clips → PlayerAnimation_Female.overrideController"

# 3. Запустить `Tools/ProjectC/Player/Add CharacterCustomisationApplier to NetworkPlayer` →
#    Console: "[Setup] CharacterCustomisationApplier added to NetworkPlayer.prefab"

# 4. На NetworkPlayer.prefab в Inspector:
#    - Выбрать CharacterCustomisationApplier компонент
#    - Назначить _maleMesh = HumanM_Model.sharedMesh
#    - Назначить _femaleMesh = HumanF_Model.sharedMesh
#    - Назначить _maleController = PlayerAnimation_Default.overrideController
#    - Назначить _femaleController = PlayerAnimation_Female.overrideController
#    - _visualRoot = Visual_Model (child)

# 5. Запустить BootstrapScene → Play (Host).
#    Подождать ~3 секунды.
#    Console: "[CharacterCustomisationApplier] Applied bodyType=Male (...)".

# 6. Открыть CharacterWindow → sub-tab "ВНЕШНОСТЬ".
#    Кликнуть "Женский".
#    Console:
#      [CharacterCustomisationApplier] Applied bodyType=Female (mesh='HumanF_Model', ctrl='PlayerAnimation_Female').
#    Визуально: персонаж = F-модель, idle/walk/run на F-клипах.

# 7. Переместить WASD — персонаж бегает на F-клипах (визуально отличается от M).
#    Прыжок (Space) — F-jump.
#    Cast скилла (ЛКМ) — работает (SkillAnimationPlayer использует F-controller как base).

# 8. Надеть шлем через Inventory → шлем на голове F-персонажа. Работает.

# 9. Выйти из Play, зайти в character_0.json (Application.persistentDataPath/Character/):
#    "customisation": { "bodyType": 1, ... }
#    back-compat: удалить "customisation" → загрузить → default = Male. OK.
```

---

## 10. Что в итоге

| Шаг | Файл/действие | Трудоёмкость |
|---|---|---|
| 1 | Создать enum `CharacterBodyType` (1 файл, ~10 строк) | 5 минут |
| 2 | Создать `CustomisationSave` (1 файл, ~50 строк) | 10 минут |
| 3 | Создать `CustomisationSnapshotDto` (1 файл, ~40 строк) | 5 минут |
| 4 | Создать `CustomisationClientState` (1 файл, ~80 строк) | 20 минут |
| 5 | Создать `CharacterCustomisationApplier` (1 файл, ~140 строк) | 30 минут |
| 6 | Создать Editor script `SetupFemaleAnimationOverride` | 30 минут |
| 7 | Создать Editor script `SetupCharacterCustomisationApplier` | 10 минут |
| 8 | Расширить `CharacterSaveData` (+1 строка) | 5 минут |
| 9 | Расширить `NetworkPlayer` (+1 AddComponent, optional RPC) | 30 минут |
| 10 | UI: sub-tab "ВНЕШНОСТЬ" + handlers | 2-4 часа |
| 11 | Тест в Play Mode | 1-2 часа |
| 12 | Борьба с edge cases | 1-2 дня |

**Итого: 3-5 дней** одного разработчика до рабочего L1.

**Дальше:** L4 (покраска) — 3-5 дней. L3 (слайдеры через transform.localScale) — 2-3 дня. L5/L2 — по запросу.