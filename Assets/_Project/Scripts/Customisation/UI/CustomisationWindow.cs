// Project C: Character Customisation — T-CUS-06
// CustomisationWindow: full-screen overlay для смены пола/внешности персонажа.
// Pattern: copy SkillTreeWindow (T-INP-09) — UXML/USS + singleton + EnsureBuilt + SetOpen.
// Триггер: кнопка "ИЗМЕНИТЬ ВНЕШНОСТЬ" в CharacterWindow header → Show().
//
// Phase 1 (L1, T-CUS-06): body type (Male/Female). Persistence через JsonCharacterDataRepository.
// Phase 2 (L3): heightScale/widthScale sliders.
// Phase 3 (L4): skin/hair colors + clothing overrides.
//
// Variant A (client-only persistence): CustomisationClientState.ApplyCustomisationSnapshot(snap)
// + JsonCharacterDataRepository.Save в конце изменения. Никакого RPC.

using System;
using ProjectC.Customisation;
using ProjectC.Customisation.Dto;
using ProjectC.Stats.Persistence;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ProjectC.Customisation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class CustomisationWindow : MonoBehaviour
    {
        public static CustomisationWindow Instance { get; private set; }

        [Header("Resources paths")]
        [SerializeField] private string _uxmlResourcePath = "UI/CustomisationWindow";
        [SerializeField] private string _ussResourcePath  = "UI/CustomisationWindow";
        [SerializeField] private string _panelSettingsPath = "UI/CustomisationPanelSettings";

        private UIDocument _doc;
        private VisualElement _rootContainer;
        private VisualElement _maleCard;
        private VisualElement _femaleCard;
        private Label _messageLabel;
        private bool _built;

        // Текущий CustomisationSave в памяти (владеем этим — мутируем по UI actions, потом Save).
        private CustomisationSave _working;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc != null && _doc.panelSettings == null)
            {
                var ps = Resources.Load<PanelSettings>(_panelSettingsPath);
                if (ps != null) _doc.panelSettings = ps;
            }
        }

        private void OnEnable() { EnsureBuilt(); }
        private void OnDisable() { /* no-op */ }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void Start() { EnsureBuilt(); }

        public void Toggle() { if (IsOpen()) SetOpen(false); else SetOpen(true); }
        public void Show() => SetOpen(true);
        public void Hide() => SetOpen(false);

        private bool IsOpen() => _rootContainer != null && _rootContainer.style.display.value == DisplayStyle.Flex;

        private void Update()
        {
            // Escape закрывает окно.
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && IsOpen()) { SetOpen(false); return; }
            // T-CUS-06 fix (per skill project-c-ui-as-tab): Escape-handler ДО network-guard.
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null || _doc.rootVisualElement == null) return;

            var uxml = Resources.Load<VisualTreeAsset>(_uxmlResourcePath);
            var uss  = Resources.Load<StyleSheet>(_ussResourcePath);
            if (uxml == null) { Debug.LogError("[CustomisationWindow] UXML not found: " + _uxmlResourcePath); return; }

            _doc.rootVisualElement.Clear();
            if (uss != null) _doc.rootVisualElement.styleSheets.Add(uss);

            _rootContainer = uxml.CloneTree();
            _rootContainer.name = "customisation-container";
            if (uss != null && !_rootContainer.styleSheets.Contains(uss))
                _rootContainer.styleSheets.Add(uss);
            _doc.rootVisualElement.Add(_rootContainer);

            // Stretch positioning.
            _rootContainer.style.position = Position.Absolute;
            _rootContainer.style.left = 0; _rootContainer.style.top = 0;
            _rootContainer.style.right = 0; _rootContainer.style.bottom = 0;
            _rootContainer.pickingMode = PickingMode.Ignore;
            _rootContainer.style.display = DisplayStyle.None;
            // Center root via flex.
            _rootContainer.style.alignItems = Align.Center;
            _rootContainer.style.justifyContent = Justify.Center;

            // Cache refs.
            _maleCard   = _rootContainer.Q<VisualElement>("cw-male-card");
            _femaleCard = _rootContainer.Q<VisualElement>("cw-female-card");
            _messageLabel = _rootContainer.Q<Label>("cw-message");

            InitActionButtons();
            LoadWorkingFromSave();

            _built = true;
            SetOpen(false);
            Debug.Log($"[CustomisationWindow] Built. uxml={uxml.name} uss={(uss != null ? uss.name : "<none>")}");
        }

        private void InitActionButtons()
        {
            if (_maleCard != null)
            {
                _maleCard.RegisterCallback<ClickEvent>(_ => OnBodyTypeClicked(CharacterBodyType.Male));
            }
            if (_femaleCard != null)
            {
                _femaleCard.RegisterCallback<ClickEvent>(_ => OnBodyTypeClicked(CharacterBodyType.Female));
            }
            var btnClose = _rootContainer.Q<VisualElement>("btn-close");
            if (btnClose != null) btnClose.RegisterCallback<ClickEvent>(_ => SetOpen(false));
        }

        private void SetOpen(bool open)
        {
            if (!_built) EnsureBuilt();
            if (_rootContainer == null) return;
            if (open)
            {
                _rootContainer.style.display = DisplayStyle.Flex;
                _rootContainer.pickingMode = PickingMode.Position;
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                _rootContainer.MarkDirtyRepaint();
                _rootContainer.schedule.Execute(() => _rootContainer.MarkDirtyRepaint()).StartingIn(50);

                LoadWorkingFromSave();   // перечитать актуальный save перед показом
                RefreshDisplay();
            }
            else
            {
                _rootContainer.style.display = DisplayStyle.None;
                _rootContainer.pickingMode = PickingMode.Ignore;
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
        }

        // === Persistence (Variant A — client-only) ===

        private void LoadWorkingFromSave()
        {
            var nm = NetworkManager.Singleton;
            ulong clientId = (nm != null && nm.IsListening) ? nm.LocalClientId : 0UL;
            var repo = new JsonCharacterDataRepository();
            if (repo.TryLoad(clientId, out var data) && data.customisation != null)
            {
                _working = data.customisation;
            }
            else
            {
                _working = new CustomisationSave();
            }
        }

        private void SaveWorking()
        {
            var nm = NetworkManager.Singleton;
            ulong clientId = (nm != null && nm.IsListening) ? nm.LocalClientId : 0UL;
            var repo = new JsonCharacterDataRepository();
            if (!repo.TryLoad(clientId, out var data))
            {
                data = new CharacterSaveData();
            }
            data.customisation = _working;
            repo.Save(clientId, data);

            // Применяем через ClientState → applier подхватит.
            var snap = SnapshotFromSave(_working);
            CustomisationClientState.Instance?.ApplyCustomisationSnapshot(snap);
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

        // === Display ===

        private void RefreshDisplay()
        {
            if (_maleCard != null)
            {
                _maleCard.EnableInClassList("cw-body-card-active", _working.bodyType == CharacterBodyType.Male);
            }
            if (_femaleCard != null)
            {
                _femaleCard.EnableInClassList("cw-body-card-active", _working.bodyType == CharacterBodyType.Female);
            }
            if (_messageLabel != null)
            {
                _messageLabel.text = _working.bodyType == CharacterBodyType.Female
                    ? "Текущий выбор: Женский. Персонаж переключится при закрытии окна."
                    : "Текущий выбор: Мужской. Персонаж переключится при закрытии окна.";
            }
        }

        // === Actions ===

        private void OnBodyTypeClicked(CharacterBodyType bodyType)
        {
            if (_working == null) _working = new CustomisationSave();
            if (_working.bodyType == bodyType)
            {
                // Повторный клик — просто no-op.
                return;
            }
            _working.bodyType = bodyType;
            SaveWorking();
            RefreshDisplay();
            if (Debug.isDebugBuild)
                Debug.Log($"[CustomisationWindow] Body type → {bodyType}, saved + applied.", this);
        }

        // === Public API для тестов ===

        [ContextMenu("DEBUG: Force SaveCurrent")]
        public void DebugSave() => SaveWorking();
    }
}