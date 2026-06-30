// Project C: Character Customisation — T-CUS-03
// CharacterCustomisationApplier: применяет CustomisationSnapshotDto к визуалу персонажа.
// Phase 1 (L1, T-CUS-03): mesh swap + AnimatorOverrideController swap (M↔F).
// Phase 2 (L3): пропорции (transform.localScale).
// Phase 3 (L4): покраска (MaterialPropertyBlock).
// Phase 4 (L4): clothing color overrides.
//
// Pattern: copy CharacterEquipmentVisualApplier (Phase 2, 2026-06-29).
// Триггер: CustomisationClientState.OnCustomisationUpdated (T-CUS-02).
//
// Additive-only: новый компонент на NetworkPlayer.prefab.
//   - Не модифицирует NetworkPlayer.Update.
//   - Не модифицирует Stats/Equipment/Skills/Combat.
//   - Не модифицирует SkillAnimationPlayer (продолжает работать с любым runtimeAnimatorController).
//   - Не модифицирует CharacterEquipmentVisualApplier (spawned visuals — child костей, не меш).

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

        [Header("Body meshes (L1)")]
        [Tooltip("Mesh для Male (HumanM_Model.sharedMesh). Назначить в Inspector из 'Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx'.")]
        [SerializeField] private Mesh _maleMesh;
        [Tooltip("Mesh для Female (HumanF_Model.sharedMesh). Назначить в Inspector из 'Assets/Kevin Iglesias/Human Animations/Models/HumanF_Model.fbx'.")]
        [SerializeField] private Mesh _femaleMesh;

        [Header("Override Controllers (L1)")]
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

        // Cached Shader.PropertyToID — поиск по имени в каждом кадре дорого.
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _colorId     = Shader.PropertyToID("_Color"); // legacy Standard shader

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
                    Debug.LogWarning("[CharacterCustomisationApplier] CustomisationClientState.Instance == null — visual customisation skipped (anti-restrictive).", this);
                }
                return;
            }
            _clientState.OnCustomisationUpdated += OnCustomisationUpdated;

            // T-CUS-09 fix v2 (pitch: при старте персонаж сбрасывается на дефолт М,
            // хотя JSON сохранён): пробуем загрузить сохранённый customisation_<clientId>.json
            // на старте, чтобы не ждать пока CustomisationWindow прочитает его.
            var snap = LoadSnapshotFromDisk();
            if (snap.HasValue)
            {
                // Сохранённые настройки есть — пихаем в ClientState (для UI) и применяем.
                _clientState.ApplyCustomisationSnapshot(snap.Value);
            }
            else
            {
                // JSON нет — используем safe defaults (не struct default с heightScale=0).
                var fallback = _clientState.CurrentSnapshot;
                if (fallback.heightScale < 0.5f || fallback.widthScale < 0.5f)
                {
                    fallback.heightScale = 1f;
                    fallback.widthScale = 1f;
                }
                _clientState.ApplyCustomisationSnapshot(fallback);
            }
        }

        /// <summary>
        /// T-CUS-09 fix v2: загрузить customisation_&lt;clientId&gt;.json и смаппить в snapshot.
        /// Возвращает null если файла нет или он битый.
        /// </summary>
        private CustomisationSnapshotDto? LoadSnapshotFromDisk()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            ulong clientId = (nm != null && nm.IsListening) ? nm.LocalClientId : 0UL;
            string folder = System.IO.Path.Combine(Application.persistentDataPath, "Customisation");
            string path = System.IO.Path.Combine(folder, $"customisation_{clientId}.json");
            try
            {
                if (System.IO.File.Exists(path))
                {
                    var json = System.IO.File.ReadAllText(path);
                    var save = JsonUtility.FromJson<CustomisationSave>(json);
                    if (save != null)
                    {
                        if (Debug.isDebugBuild)
                            Debug.Log($"[CharacterCustomisationApplier] Loaded customisation from disk: body={save.bodyType}, h={save.heightScale:F2}, w={save.widthScale:F2}");
                        return SnapshotFromSave(save);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CharacterCustomisationApplier] Load failed: {ex.Message}");
            }
            return null;
        }

        private static CustomisationSnapshotDto SnapshotFromSave(CustomisationSave s)
        {
            ClothingColorOverrideDto[] overrides = null;
            if (s.clothingColorOverrides != null && s.clothingColorOverrides.Length > 0)
            {
                overrides = new ClothingColorOverrideDto[s.clothingColorOverrides.Length];
                for (int i = 0; i < s.clothingColorOverrides.Length; i++)
                {
                    overrides[i] = ClothingColorOverrideDto.FromSave(s.clothingColorOverrides[i]);
                }
            }
            return new CustomisationSnapshotDto
            {
                bodyType    = s.bodyType,
                presetId    = s.presetId,
                heightScale = s.heightScale,
                widthScale  = s.widthScale,
                skinColorR  = s.skinColorR, skinColorG = s.skinColorG, skinColorB = s.skinColorB, skinColorA = s.skinColorA,
                hairColorR  = s.hairColorR, hairColorG = s.hairColorG, hairColorB = s.hairColorB, hairColorA = s.hairColorA,
                hairStyle   = s.hairStyle,
                clothingOverrides = overrides,
            };
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

            // === L1: body type (mesh + animator controller) ===
            if (!_hasSnapshot || _currentSnapshot.bodyType != snapshot.bodyType)
            {
                ApplyBodyType(snapshot.bodyType);
            }

            // === L3: proportions (visualRoot.localScale) ===
            if (!_hasSnapshot
                || !Mathf.Approximately(_currentSnapshot.heightScale, snapshot.heightScale)
                || !Mathf.Approximately(_currentSnapshot.widthScale,  snapshot.widthScale))
            {
                ApplyProportions(snapshot.heightScale, snapshot.widthScale);
            }

            // === L4: colors (MaterialPropertyBlock) ===
            if (!_hasSnapshot || ColorsDiffer(_currentSnapshot, snapshot))
            {
                ApplyColors(snapshot);
            }

            // === L4: hair style ===
            if (!_hasSnapshot || _currentSnapshot.hairStyle != snapshot.hairStyle)
            {
                // TODO T-CUS-10: spawn/destroy hair mesh (через EquipSlotToBone.Head + GameObject[]).
                // Сейчас только логируем — actual hair mesh swap вне scope L1.
                if (Debug.isDebugBuild)
                    Debug.Log($"[CharacterCustomisationApplier] Hair style changed: {_currentSnapshot.hairStyle} → {snapshot.hairStyle} (mesh spawn — TODO T-CUS-10).", this);
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
                Debug.Log($"[CharacterCustomisationApplier] Applied bodyType={bodyType} " +
                          $"(mesh='{(targetMesh != null ? targetMesh.name : "null")}', " +
                          $"ctrl='{(targetCtrl != null ? targetCtrl.name : "null")}').", this);
            }
        }

        // === L3: ApplyProportions ===

        private void ApplyProportions(float heightScale, float widthScale)
        {
            if (_visualRoot == null) return;
            // Clamp в разумных пределах (защита от случайных значений в JSON).
            float h = Mathf.Clamp(heightScale, 0.4f, 1.6f);
            float w = Mathf.Clamp(widthScale,  0.4f, 1.6f);
            _visualRoot.localScale = new Vector3(w, h, w);

            if (Debug.isDebugBuild)
                Debug.Log($"[CharacterCustomisationApplier] Applied proportions: h={h:F2}, w={w:F2}.", this);
        }

        // === L4: ApplyColors (skin + hair base) ===

        private static bool ColorsDiffer(CustomisationSnapshotDto a, CustomisationSnapshotDto b)
        {
            return Mathf.Abs(a.skinColorR - b.skinColorR) > 0.001f
                || Mathf.Abs(a.skinColorG - b.skinColorG) > 0.001f
                || Mathf.Abs(a.skinColorB - b.skinColorB) > 0.001f
                || Mathf.Abs(a.skinColorA - b.skinColorA) > 0.001f
                || Mathf.Abs(a.hairColorR - b.hairColorR) > 0.001f
                || Mathf.Abs(a.hairColorG - b.hairColorG) > 0.001f
                || Mathf.Abs(a.hairColorB - b.hairColorB) > 0.001f
                || Mathf.Abs(a.hairColorA - b.hairColorA) > 0.001f;
        }

        private void ApplyColors(CustomisationSnapshotDto snapshot)
        {
            if (_bodyRenderer == null) return;

            var mpb = new MaterialPropertyBlock();
            _bodyRenderer.GetPropertyBlock(mpb);

            Color skin = snapshot.GetSkinColor();
            // Пишем в оба property: URP/Lit использует _BaseColor, legacy Standard — _Color.
            // MaterialPropertyBlock.SetColor работает для обеих, но материал может иметь только одну из них.
            mpb.SetColor(_baseColorId, skin);
            mpb.SetColor(_colorId,     skin);

            _bodyRenderer.SetPropertyBlock(mpb);

            if (Debug.isDebugBuild)
                Debug.Log($"[CharacterCustomisationApplier] Applied skin color: ({skin.r:F2}, {skin.g:F2}, {skin.b:F2}).", this);
        }

        // === Public API (для тестов) ===

        public CharacterBodyType CurrentBodyType => _hasSnapshot ? _currentSnapshot.bodyType : CharacterBodyType.Male;

        [ContextMenu("DEBUG: Force re-apply current snapshot")]
        public void DebugReapply()
        {
            if (_clientState != null)
            {
                OnCustomisationUpdated(_clientState.CurrentSnapshot);
            }
            else if (_logWarnings)
            {
                Debug.LogWarning("[CharacterCustomisationApplier] No client state — nothing to re-apply.", this);
            }
        }
    }
}