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
        // T-CUS-09 (L3):
        private Slider _heightSlider;
        private Slider _widthSlider;
        private Label _heightValueLabel;
        private Label _widthValueLabel;
        // T-CUS-10 (L4): skin color.
        private Slider _skinRSlider;
        private Slider _skinGSlider;
        private Slider _skinBSlider;
        private Label _skinRValueLabel;
        private Label _skinGValueLabel;
        private Label _skinBValueLabel;
        private VisualElement _skinPreview;
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

        public bool IsOpen() => _rootContainer != null && _rootContainer.style.display.value == DisplayStyle.Flex;

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

            // T-CUS-09 (L3): слайдеры пропорций.
            _heightSlider = _rootContainer.Q<Slider>("cw-height-slider");
            _widthSlider  = _rootContainer.Q<Slider>("cw-width-slider");
            _heightValueLabel = _rootContainer.Q<Label>("cw-height-value");
            _widthValueLabel  = _rootContainer.Q<Label>("cw-width-value");

            // T-CUS-10 (L4): skin color sliders + preview.
            _skinRSlider = _rootContainer.Q<Slider>("cw-skin-r-slider");
            _skinGSlider = _rootContainer.Q<Slider>("cw-skin-g-slider");
            _skinBSlider = _rootContainer.Q<Slider>("cw-skin-b-slider");
            _skinRValueLabel = _rootContainer.Q<Label>("cw-skin-r-value");
            _skinGValueLabel = _rootContainer.Q<Label>("cw-skin-g-value");
            _skinBValueLabel = _rootContainer.Q<Label>("cw-skin-b-value");
            _skinPreview = _rootContainer.Q<VisualElement>("cw-skin-preview");

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

            // T-CUS-09 (L3): слайдеры пропорций — slider.RegisterValueChangedCallback (continuous, не дёргать при Show).
            if (_heightSlider != null)
            {
                _heightSlider.RegisterValueChangedCallback(evt => OnHeightSliderChanged(evt.newValue));
            }
            if (_widthSlider != null)
            {
                _widthSlider.RegisterValueChangedCallback(evt => OnWidthSliderChanged(evt.newValue));
            }
            var btnReset = _rootContainer.Q<VisualElement>("cw-reset-proportions");
            if (btnReset != null) btnReset.RegisterCallback<ClickEvent>(_ => OnResetProportionsClicked());

            // T-CUS-10 (L4): skin color sliders + reset button.
            if (_skinRSlider != null) _skinRSlider.RegisterValueChangedCallback(evt => OnSkinRSliderChanged(evt.newValue));
            if (_skinGSlider != null) _skinGSlider.RegisterValueChangedCallback(evt => OnSkinGSliderChanged(evt.newValue));
            if (_skinBSlider != null) _skinBSlider.RegisterValueChangedCallback(evt => OnSkinBSliderChanged(evt.newValue));
            var btnResetSkin = _rootContainer.Q<VisualElement>("cw-reset-skin");
            if (btnResetSkin != null) btnResetSkin.RegisterCallback<ClickEvent>(_ => OnResetSkinClicked());
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

        // === Persistence (Variant A — client-only, отдельный файл) ===
        // Используем customisation_<clientId>.json, НЕ CharacterSaveData (который StatsServer может перезаписать).

        private string GetCustomisationPath(ulong clientId)
        {
            string folder = System.IO.Path.Combine(Application.persistentDataPath, "Customisation");
            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);
            return System.IO.Path.Combine(folder, $"customisation_{clientId}.json");
        }

        private void LoadWorkingFromSave()
        {
            _working = new CustomisationSave(); // default бэкап: Male, scale=1, цвета=white
            var nm = NetworkManager.Singleton;
            ulong clientId = (nm != null && nm.IsListening) ? nm.LocalClientId : 0UL;
            var path = GetCustomisationPath(clientId);
            try
            {
                if (System.IO.File.Exists(path))
                {
                    var json = System.IO.File.ReadAllText(path);
                    var parsed = JsonUtility.FromJson<CustomisationSave>(json);
                    if (parsed != null) _working = parsed;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CustomisationWindow] Load failed for client {clientId}: {ex.Message}. Using defaults.");
            }
        }

        private void SaveWorking()
        {
            var nm = NetworkManager.Singleton;
            ulong clientId = (nm != null && nm.IsListening) ? nm.LocalClientId : 0UL;
            var path = GetCustomisationPath(clientId);
            try
            {
                var tmpPath = path + ".tmp";
                var json = JsonUtility.ToJson(_working, prettyPrint: false);
                System.IO.File.WriteAllText(tmpPath, json);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                System.IO.File.Move(tmpPath, path);

                // Применяем через ClientState → applier подхватит.
                var snap = SnapshotFromSave(_working);
                CustomisationClientState.Instance?.ApplyCustomisationSnapshot(snap);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CustomisationWindow] Save failed for client {clientId}: {ex.Message}");
            }
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

            // T-CUS-09 (L3): подтянуть значения слайдеров из _working.
            // SetValueWithoutNotify — чтобы не вызывать OnHeightSliderChanged → SaveWorking ping-pong во время Show().
            if (_heightSlider != null)
            {
                float clampedH = Mathf.Clamp(_working.heightScale, _heightSlider.lowValue, _heightSlider.highValue);
                if (!Mathf.Approximately(_heightSlider.value, clampedH))
                    _heightSlider.SetValueWithoutNotify(clampedH);
            }
            if (_widthSlider != null)
            {
                float clampedW = Mathf.Clamp(_working.widthScale, _widthSlider.lowValue, _widthSlider.highValue);
                if (!Mathf.Approximately(_widthSlider.value, clampedW))
                    _widthSlider.SetValueWithoutNotify(clampedW);
            }
            if (_heightValueLabel != null) _heightValueLabel.text = _working.heightScale.ToString("F2");
            if (_widthValueLabel  != null) _widthValueLabel.text  = _working.widthScale.ToString("F2");

            // T-CUS-10 (L4): skin color sliders + preview swatch.
            // Слайдеры работают в диапазоне [0, 1], а UI labels показывают 0-255 (прозрачно для пользователя).
            if (_skinRSlider != null && !Mathf.Approximately(_skinRSlider.value, _working.skinColorR))
                _skinRSlider.SetValueWithoutNotify(_working.skinColorR);
            if (_skinGSlider != null && !Mathf.Approximately(_skinGSlider.value, _working.skinColorG))
                _skinGSlider.SetValueWithoutNotify(_working.skinColorG);
            if (_skinBSlider != null && !Mathf.Approximately(_skinBSlider.value, _working.skinColorB))
                _skinBSlider.SetValueWithoutNotify(_working.skinColorB);
            if (_skinRValueLabel != null) _skinRValueLabel.text = Mathf.RoundToInt(_working.skinColorR * 255f).ToString();
            if (_skinGValueLabel != null) _skinGValueLabel.text = Mathf.RoundToInt(_working.skinColorG * 255f).ToString();
            if (_skinBValueLabel != null) _skinBValueLabel.text = Mathf.RoundToInt(_working.skinColorB * 255f).ToString();
            if (_skinPreview != null) _skinPreview.style.backgroundColor = new Color(_working.skinColorR, _working.skinColorG, _working.skinColorB, 1f);

            if (_messageLabel != null)
            {
                _messageLabel.text = _working.bodyType == CharacterBodyType.Female
                    ? "Текущий выбор: Женский. Изменения применяются сразу."
                    : "Текущий выбор: Мужской. Изменения применяются сразу.";
            }
        }

        // === T-CUS-09 (L3): slider actions ===

        private void OnHeightSliderChanged(float newValue)
        {
            if (_working == null) _working = new CustomisationSave();
            _working.heightScale = Mathf.Clamp(newValue, 0.7f, 1.3f);
            if (_heightValueLabel != null) _heightValueLabel.text = _working.heightScale.ToString("F2");
            SaveWorking();
        }

        private void OnWidthSliderChanged(float newValue)
        {
            if (_working == null) _working = new CustomisationSave();
            _working.widthScale = Mathf.Clamp(newValue, 0.7f, 1.3f);
            if (_widthValueLabel != null) _widthValueLabel.text = _working.widthScale.ToString("F2");
            SaveWorking();
        }

        private void OnResetProportionsClicked()
        {
            if (_working == null) _working = new CustomisationSave();
            _working.heightScale = 1.0f;
            _working.widthScale = 1.0f;
            // Обновить UI без триггера callback → пересобрать value, потом tick SaveWorking.
            if (_heightSlider != null) _heightSlider.SetValueWithoutNotify(1.0f);
            if (_widthSlider  != null) _widthSlider.SetValueWithoutNotify(1.0f);
            if (_heightValueLabel != null) _heightValueLabel.text = "1.00";
            if (_widthValueLabel  != null) _widthValueLabel.text  = "1.00";
            SaveWorking();
            if (Debug.isDebugBuild)
                Debug.Log("[CustomisationWindow] Proportions reset to defaults.", this);
        }

        // === T-CUS-10 (L4): skin color slider actions ===

        private void OnSkinRSliderChanged(float newValue)
        {
            if (_working == null) _working = new CustomisationSave();
            _working.skinColorR = Mathf.Clamp01(newValue);
            UpdateSkinPreviewAndLabel();
            SaveWorking();
        }

        private void OnSkinGSliderChanged(float newValue)
        {
            if (_working == null) _working = new CustomisationSave();
            _working.skinColorG = Mathf.Clamp01(newValue);
            UpdateSkinPreviewAndLabel();
            SaveWorking();
        }

        private void OnSkinBSliderChanged(float newValue)
        {
            if (_working == null) _working = new CustomisationSave();
            _working.skinColorB = Mathf.Clamp01(newValue);
            UpdateSkinPreviewAndLabel();
            SaveWorking();
        }

        private void UpdateSkinPreviewAndLabel()
        {
            if (_skinRValueLabel != null) _skinRValueLabel.text = Mathf.RoundToInt(_working.skinColorR * 255f).ToString();
            if (_skinGValueLabel != null) _skinGValueLabel.text = Mathf.RoundToInt(_working.skinColorG * 255f).ToString();
            if (_skinBValueLabel != null) _skinBValueLabel.text = Mathf.RoundToInt(_working.skinColorB * 255f).ToString();
            if (_skinPreview != null) _skinPreview.style.backgroundColor = new Color(_working.skinColorR, _working.skinColorG, _working.skinColorB, 1f);
        }

        private void OnResetSkinClicked()
        {
            if (_working == null) _working = new CustomisationSave();
            // Default skin — светлый (1.0, 0.8, 0.6) для visibility против тёмно-серого (50%) URP Lit.
            _working.skinColorR = 1.0f;
            _working.skinColorG = 0.8f;
            _working.skinColorB = 0.6f;
            if (_skinRSlider != null) _skinRSlider.SetValueWithoutNotify(1.0f);
            if (_skinGSlider != null) _skinGSlider.SetValueWithoutNotify(0.8f);
            if (_skinBSlider != null) _skinBSlider.SetValueWithoutNotify(0.6f);
            UpdateSkinPreviewAndLabel();
            SaveWorking();
            if (Debug.isDebugBuild)
                Debug.Log("[CustomisationWindow] Skin color reset to default (1.0, 0.8, 0.6).", this);
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