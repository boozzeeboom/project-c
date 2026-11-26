// T-Q11c: DialogWindow rewrite — UIDocument + UXML/USS вместо IMGUI.
// Решает mouse block issue (IMGUI consume mouse events).
// UXML/USS загружаются из Resources/UI/ (DialogWindow.uxml + DialogWindow.uss).
//
// FIX (2026-06-07): переписан по образцу MarketWindow.cs (Trade):
// • [SerializeField] dialogWindowUxml + dialogWindowUss (Inspector-bound).
// • Resources.Load fallback в Awake (когда поля пустые — для editor convenience).
// • PanelSettings НЕ создаётся runtime — привязан в Inspector на [QuestClientState] GO.
// • EnsureBuilt() делает _doc.rootVisualElement.styleSheets.Add(uss) — критично
// для применения USS class-стилей (БЕЗ этого panel collapse to "strip").
// • pickingMode=Ignore на root (модальное окно — клик "снаружи" не пробрасывается
// в game, кнопки внутри ловят mouse).

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using ProjectC.Localization;
using ProjectC.Quests.Dto;
using ProjectC.Quests.Client;
using ProjectC.Player;
using ProjectC.Reputation;
using ProjectC.Items;
using ProjectC.Items.Client;

namespace ProjectC.Quests.UI
{
 /// <summary>
 /// Dialog window на UIDocument. Mouse events properly routed через Event System.
 /// </summary>
 /// <remarks>
 /// T-Q11c: replace IMGUI с UIDocument. UXML/USS — в Resources/UI/.
 /// Pattern: MarketWindow.cs (Trade) — использует UXML/USS из Resources/UI/.
 /// </remarks>
 [RequireComponent(typeof(UIDocument))]
 public class DialogWindow : MonoBehaviour
 {
 public static DialogWindow Instance { get; private set; }

 [Header("UI Assets (можно Resources fallback)")]
 [SerializeField] private VisualTreeAsset dialogWindowUxml;
 [SerializeField] private StyleSheet dialogWindowUss;
 [Header("T-Q12: Typewriter settings")]
 [SerializeField, Tooltip("Скорость печати символов в секунду.40=standard,25=drama,60=fast.")]
 private float charsPerSecond =40f;

 // Runtime UIDocument refs
 private UIDocument _doc;
 private VisualElement _root;
 private VisualElement _panel;
 private Label _npcNameLabel;
 private Label _npcAttitudeLabel; // T-Q13
 private Label _textLabel;
 private VisualElement _optionsContainer;
 private Label _toastLabel;

 // T-MERCH01: Merchant panel UI refs
 private VisualElement _merchantPanel;
 private Label _merchantTitleLabel;
 private Label _merchantTestBadge;
 private Label _merchantCreditsLabel;
 private Label _merchantSlotsLabel;
 private Label _merchantRepInfoLabel;
 private Button _tabBuyBtn;
 private Button _tabSellBtn;
 private TextField _merchantSearchField;
 private VisualElement _merchantCategoriesContainer;
 private VisualElement _merchantItemsList;
 private VisualElement _detailIconBox;
 private Label _detailIconLetter;
 private Label _detailTitleLabel;
 private Label _detailSpecsLabel;
 private Label _detailDescLabel;
 private Button _qtyMinusBtn;
 private Label _qtyValueLabel;
 private Button _qtyPlusBtn;
 private Button _qtyX1Btn;
 private Button _qtyX5Btn;
 private Button _qtyX10Btn;
 private Button _qtyMaxBtn;
 private Label _tradeSummaryLabel;
 private Button _tradeActionBtn;
 private Label _merchantStatusLabel;

 // State
 private DialogStepDto _currentStep;
 private string _lastActionMessage;
 private float _lastActionMessageTime;

 // T-MERCH01: Merchant runtime state
 private MerchantShopSnapshotDto _currentShopSnapshot;
 private bool _hasShopSnapshot;
 private bool _isBuyTab = true;
 private int _selectedCategoryFilter = -1; // -1 = Все, 0..7 = ItemType
 private string _searchQuery = "";
 private int _selectedItemId = -1;
 private int _hoveredItemId = -1;
 // T-PERF26: строки текущего списка по itemId — смена выбора без полной перестройки списка.
 private readonly Dictionary<int, VisualElement> _merchantRowsById = new Dictionary<int, VisualElement>();
 // T-PERF26: захват 2026-11-26_14-13-12 — тестовый режим «скупка всего» строил ~4 200 элементов
 // (25 МБ мусора, 160–180 мс на открытие/клик). Длинный список обрезается, остальное — через поиск/фильтр.
 private const int MaxMerchantRows = 120;
 private int _selectedQuantity = 1;
 private string _merchantStatusText = "";
 private bool _merchantStatusSuccess = true;
 private float _merchantStatusTime = -10f;

 // T-Q12: typewriter state.
 private Coroutine _typewriterCoroutine;
 private string _fullText;
 private int _displayedCharCount;
 private bool _inputSubscribed;
 // T-Q13: NpcAttitude subscribe state.
 private bool _attitudeSubscribed;

 public bool IsOpen { get; private set; }


 private void Awake()
 {
 if (Instance == null) Instance = this;
 else if (Instance != this) { Destroy(gameObject); return; }

 _doc = GetComponent<UIDocument>();
 if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

 // Resources fallback: если в Inspector поля не привязаны, грузим из Resources/UI/.
 // Это удобно для editor convenience; в BootstrapScene поля должны быть привязаны явно
 // (НЕ полагаемся только на fallback — .asset мог не импортироваться Unity).
 if (dialogWindowUxml == null)
 dialogWindowUxml = Resources.Load<VisualTreeAsset>("UI/DialogWindow");
 if (dialogWindowUss == null)
 dialogWindowUss = Resources.Load<StyleSheet>("UI/DialogWindow");
 }

 private void OnEnable()
 {
 EnsureBuilt();
 TrySubscribe();
 TrySubscribeInput();
 TrySubscribeAttitude(); // T-Q13
 }

 // T-Q12-fix: UIDocument.OnEnable может сработать ПОСЛЕ DialogWindow.OnEnable и подвесить
 // свой UXML-auto-load поверх нашего, делая _doc.rootVisualElement пустым → EnsureBuilt выходит рано.
 // Start() гарантирует второй проход EnsureBuilt ПОСЛЕ всех OnEnable.
 private void Start()
 {
 EnsureBuilt();
 }

 private void OnDisable()
 {
 TryUnsubscribeInput();
 // T-Q12: остановить typewriter если идёт (при domain reload coroutine теряется).
 StopTypewriterImmediate();
 }
 private void OnDestroy()
 {
 TryUnsubscribe();
 TryUnsubscribeAttitude(); // T-Q13
 if (Instance == this) Instance = null;
 }

 // T-Q12: PlayerInputReader.OnModeSwitchPressed (F) → skip typewriter (только при IsOpen + typewriter в процессе).
 private void TrySubscribeInput()
 {
 if (_inputSubscribed) return;
 var input = GetLocalPlayerInputReader();
 if (input == null) return;
 input.OnModeSwitchPressed += OnFSkipTypewriter;
 _inputSubscribed = true;
 }
 private void TryUnsubscribeInput()
 {
 if (!_inputSubscribed) return;
 var input = GetLocalPlayerInputReader();
 if (input == null) { _inputSubscribed = false; return; }
 input.OnModeSwitchPressed -= OnFSkipTypewriter;
 _inputSubscribed = false;
 }

 private static PlayerInputReader GetLocalPlayerInputReader()
 {
 var np = GetLocal();
 return np != null ? np.GetComponent<PlayerInputReader>() : null;
 }

 private bool _subscribed;
 private void TrySubscribe()
 {
 if (_subscribed) return;
 if (QuestClientState.Instance == null) return;
 QuestClientState.Instance.OnDialogStepReceived += HandleStepReceived;
 QuestClientState.Instance.OnDialogActionResultReceived += HandleActionResultReceived;
 QuestClientState.Instance.OnMerchantShopReceived += HandleMerchantShopReceived;
 QuestClientState.Instance.OnMerchantTradeResultReceived += HandleMerchantTradeResultReceived;
 _subscribed = true;
 }
 private void TryUnsubscribe()
 {
 if (!_subscribed) return;
 if (QuestClientState.Instance == null) { _subscribed = false; return; }
 QuestClientState.Instance.OnDialogStepReceived -= HandleStepReceived;
 QuestClientState.Instance.OnDialogActionResultReceived -= HandleActionResultReceived;
 QuestClientState.Instance.OnMerchantShopReceived -= HandleMerchantShopReceived;
 QuestClientState.Instance.OnMerchantTradeResultReceived -= HandleMerchantTradeResultReceived;
 _subscribed = false;
 }

 // T-Q13: subscribe NpcAttitudeClientState → update badge on attitude change.
 private void TrySubscribeAttitude()
 {
 if (_attitudeSubscribed) return;
 var att = NpcAttitudeClientState.Instance;
 if (att == null) return;
 att.OnNpcAttitudeUpdated += OnNpcAttitudeChanged;
 _attitudeSubscribed = true;
 }
 private void TryUnsubscribeAttitude()
 {
 if (!_attitudeSubscribed) return;
 var att = NpcAttitudeClientState.Instance;
 if (att == null) { _attitudeSubscribed = false; return; }
 att.OnNpcAttitudeUpdated -= OnNpcAttitudeChanged;
 _attitudeSubscribed = false;
 }
 private void OnNpcAttitudeChanged(NpcAttitudeSnapshotDto snapshot)
 {
 UpdateNpcAttitudeBadge();
 }

 // T-Q13: показать "❤ +N" / "❤ -N" / "" (если 0 или нет snapshot) для текущего NPC.
 // T-MERCH01: если открыт магазин с точным серверным float-отношением (с учётом +0.1 за 100 CR),
 // показываем точное значение с десятыми долями (например "❤ +1.3").
 private void UpdateNpcAttitudeBadge()
 {
 if (_npcAttitudeLabel == null) return;
 // DialogStepDto — struct, поэтому проверяем treeId+nodeId как "is set" indicator.
 string npcId = (_currentStep.treeId != null) ? _currentStep.speakerNpcId : null;
 if (string.IsNullOrEmpty(npcId)) { _npcAttitudeLabel.text = ""; return; }

 if (_hasShopSnapshot && _currentShopSnapshot.isMerchant && _currentShopSnapshot.npcId == npcId)
 {
     float exact = _currentShopSnapshot.npcAttitudeExact;
     bool hasFraction = Mathf.Abs(exact - Mathf.Round(exact)) > 0.005f;
     string formatted = hasFraction ? exact.ToString("0.0") : Mathf.RoundToInt(exact).ToString();
     _npcAttitudeLabel.text = exact > 0.001f ? $"❤ +{formatted}" : $"❤ {formatted}";
     return;
 }

 var att = NpcAttitudeClientState.Instance;
 if (att == null) { _npcAttitudeLabel.text = ""; return; }
 int value = att.GetAttitudeForNpc(npcId);
 if (value == 0) { _npcAttitudeLabel.text = "❤ 0"; return; }
 _npcAttitudeLabel.text = value > 0 ? $"❤ +{value}" : $"❤ {value}";
 }

 private void EnsureBuilt()
 {
 if (_doc == null) _doc = GetComponent<UIDocument>();
 if (_doc == null)
 {
 Debug.LogError("[DialogWindow] нет UIDocument на GameObject");
 return;
 }
 if (_doc.rootVisualElement == null) return;

 // Re-load fallback если Inspector не привязал.
 if (dialogWindowUxml == null)
 dialogWindowUxml = Resources.Load<VisualTreeAsset>("UI/DialogWindow");
 if (dialogWindowUss == null)
 dialogWindowUss = Resources.Load<StyleSheet>("UI/DialogWindow");
 if (dialogWindowUxml == null)
 {
 Debug.LogError("[DialogWindow] UXML не найден ни в Inspector, ни в Resources/UI/");
 return;
 }

 // КРИТИЧНО: очищаем и подвешиваем стили КАЖДЫЙ раз (после UIDocument.OnEnable
 // может подвесить свой UXML-auto-load поверх нашего, и USS слетит).
 _doc.rootVisualElement.Clear();
 if (dialogWindowUss != null)
 _doc.rootVisualElement.styleSheets.Add(dialogWindowUss);

 _root = dialogWindowUxml.CloneTree();
 // CloneTree возвращает TemplateContainer с position:relative0x0. Растягиваем на
 // весь rootVE — иначе .dialog-root (position:absolute) уезжает в (-W/2,0).
 _root.style.position = Position.Absolute;
 _root.style.left =0;
 _root.style.top =0;
 _root.style.right =0;
 _root.style.bottom =0;
 // pickingMode=Ignore на root — клики "снаружи" диалога не пробрасываются в game
 // во время модального окна. Кнопки внутри (.dialog-button) ловят mouse сами.
 _root.pickingMode = PickingMode.Ignore;
 _doc.rootVisualElement.Add(_root);

 _panel = _root.Q<VisualElement>("panel");
 _npcNameLabel = _root.Q<Label>("npc-name");
 _npcAttitudeLabel = _root.Q<Label>("npc-attitude"); // T-Q13
 _textLabel = _root.Q<Label>("text");
 _optionsContainer = _root.Q<VisualElement>("options");
 _toastLabel = _root.Q<Label>("toast");

 // T-MERCH01: query merchant UI elements
 _merchantPanel = _root.Q<VisualElement>("merchant-panel");
 _merchantTitleLabel = _root.Q<Label>("merchant-title");
 _merchantTestBadge = _root.Q<Label>("merchant-test-badge");
 _merchantCreditsLabel = _root.Q<Label>("merchant-credits");
 _merchantSlotsLabel = _root.Q<Label>("merchant-slots");
 _merchantRepInfoLabel = _root.Q<Label>("merchant-rep-info");
 _tabBuyBtn = _root.Q<Button>("tab-buy");
 _tabSellBtn = _root.Q<Button>("tab-sell");
 _merchantSearchField = _root.Q<TextField>("merchant-search");
 _merchantCategoriesContainer = _root.Q<VisualElement>("merchant-categories");
 _merchantItemsList = _root.Q<VisualElement>("merchant-items-list");
 _detailIconBox = _root.Q<VisualElement>("detail-icon");
 _detailIconLetter = _root.Q<Label>("detail-icon-letter");
 _detailTitleLabel = _root.Q<Label>("detail-title");
 _detailSpecsLabel = _root.Q<Label>("detail-specs");
 _detailDescLabel = _root.Q<Label>("detail-desc");
 _qtyMinusBtn = _root.Q<Button>("qty-minus");
 _qtyValueLabel = _root.Q<Label>("qty-value");
 _qtyPlusBtn = _root.Q<Button>("qty-plus");
 _qtyX1Btn = _root.Q<Button>("qty-x1");
 _qtyX5Btn = _root.Q<Button>("qty-x5");
 _qtyX10Btn = _root.Q<Button>("qty-x10");
 _qtyMaxBtn = _root.Q<Button>("qty-max");
 _tradeSummaryLabel = _root.Q<Label>("trade-summary");
 _tradeActionBtn = _root.Q<Button>("trade-action-btn");
 _merchantStatusLabel = _root.Q<Label>("merchant-status");

 BindMerchantCallbacks();
 SetMerchantPanelVisible(false);

 if (_toastLabel != null) _toastLabel.style.display = DisplayStyle.None;
 if (_npcAttitudeLabel != null) _npcAttitudeLabel.text = "";
 // Initially hidden — Show() переключит на Flex.
 if (_root != null) _root.style.display = DisplayStyle.None;

 // T-Q12: click на тексте → skip typewriter (НЕ на option button — те advance как обычно).
 if (_textLabel != null)
 {
 _textLabel.RegisterCallback<PointerDownEvent>(OnTextPointerDown);
 }

 if (Debug.isDebugBuild)
 Debug.Log($"[DialogWindow] Built: rootVE.children={_doc.rootVisualElement.childCount}, styleSheets={_doc.rootVisualElement.styleSheets.count}");
 }

 /// <summary>
 /// T-Q11c-fix: USS class добавляется на runtime-created кнопки. Дополнительно
 /// ставим минимальный inline-fallback на случай если USS не дошёл (debug builds).
 /// </summary>
 private void StyleButton(Button btn, bool enabled)
 {
 btn.AddToClassList("dialog-button");
 if (!enabled) btn.AddToClassList("dialog-button-disabled");
 }

 private void HandleStepReceived(DialogStepDto step)
 {
 string prevNpcId = _currentStep.speakerNpcId;
 _currentStep = step;
 EnsureBuilt();
 if (_root == null) return;
 if (step.isEnd) { Close(); }
 else
 {
     if (prevNpcId != step.speakerNpcId)
     {
         _selectedItemId = -1;
         _hoveredItemId = -1;
         _selectedQuantity = 1;
         _merchantStatusText = "";
     }
     Show();
     BuildUI();
     SetMerchantPanelVisible(step.hasMerchant);
     if (step.hasMerchant)
     {
         if (_hasShopSnapshot && _currentShopSnapshot.npcId == step.speakerNpcId)
             RefreshMerchantUI();
         else if (!string.IsNullOrEmpty(step.speakerNpcId))
             QuestClientState.Instance?.RequestMerchantShop(step.speakerNpcId);
     }
 }
 }

 // T-Q23: format DialogActionResultDto → human-readable text.
 // Used by HandleActionResultReceived (in-dialog label).
 // Action type IDs match DialogueActionType enum (DialogueAction.cs):
 //   GiveCredits=30, AddReputation=31, AddNpcAttitude=32, CompleteObjective=11
 // T-Q25: использует intParam (delta) + resultData (display name) для clean format.
 private static string FormatActionResultForDialog(DialogActionResultDto r)
 {
     string data = r.resultData ?? "";
     int delta = r.intParam;
     string prefix = r.success ? "✅" : "❌";
     switch (r.actionType)
     {
         case 20: return string.IsNullOrEmpty(data) ? $"{prefix} {Loc.Get("ui.dialog.action.add_item", "+1 предмет")}" : $"{prefix} +1 {data}";
         case 21: return string.IsNullOrEmpty(data) ? $"{prefix} {Loc.Get("ui.dialog.action.remove_item", "-1 предмет")}" : $"{prefix} -1 {data}";
         case 30: return $"{prefix} +{delta} CR";
         case 31:
             if (string.IsNullOrEmpty(data)) return $"{prefix} {Loc.Get("ui.dialog.action.reputation", "Репутация")} +{delta}";
             var fparts = data.Split(':');
             if (fparts.Length == 2) return $"{prefix} {fparts[0]} +{delta}";
             return $"{prefix} {data}";
         case 32:
             if (string.IsNullOrEmpty(data)) return $"{prefix} {Loc.Get("ui.dialog.action.attitude", "Отношение")} +{delta}";
             var parts = data.Split(':');
             if (parts.Length == 2) return $"{prefix} {parts[0]} +{delta}";
             return $"{prefix} {data}";
         case 11: return string.IsNullOrEmpty(data) ? $"{prefix} {Loc.Get("ui.dialog.action.objective_complete", "Цель выполнена")}" : $"{prefix} {data}";
         default: return string.IsNullOrEmpty(data) ? prefix : $"{prefix} {data}";
     }
 }

 private void HandleActionResultReceived(DialogActionResultDto result)
 {
     // T-Q23 fix: format human-readable text instead of "OK: 30 200".
     _lastActionMessage = FormatActionResultForDialog(result);
     _lastActionMessageTime = Time.time;
     if (Debug.isDebugBuild) Debug.Log($"[DialogWindow] Action result: {_lastActionMessage}");

     // T-Q17: client-side dispatch по action type.
     // OpenMarket → close dialog + MarketInteractor.TryOpenMarket (использует local player zone).
     if (result.success && result.actionType == (byte)ProjectC.Dialogue.DialogueActionType.OpenMarket)
     {
         if (Debug.isDebugBuild) Debug.Log("[DialogWindow] Action result: OpenMarket → close dialog + TryOpenMarket");
         Close();
         ProjectC.Trade.Client.MarketInteractor.TryOpenMarket();
     }
     else if (result.success && result.actionType == (byte)ProjectC.Dialogue.DialogueActionType.OpenService)
     {
         // T-Q17 stub: ServiceUI не существует. Log only. Close dialog.
         if (Debug.isDebugBuild) Debug.Log($"[DialogWindow] Action result: OpenService serviceId='{result.resultData}' (T-Q17 stub — ServiceUI TBD)");
         Close();
     }
 }

 public void Show()
 {
     IsOpen = true;
     if (_root != null)
     {
         _root.style.display = DisplayStyle.Flex;
         // T-Q11c-fix: root должен ловить mouse events когда visible (pickingMode=Ignore
         // ставится в EnsureBuilt чтобы скрытое окно НЕ блокировало game clicks).
         _root.pickingMode = PickingMode.Position;
     }
     // T-Q11c-fix: разлочить курсор (по умолчанию в flight-mode locked) — иначе
     // mouse events не доходят до UIDocument panel. Same fix as MarketWindow.cs:1084.
     UnityEngine.Cursor.lockState = CursorLockMode.None;
     UnityEngine.Cursor.visible = true;
 }

 public void Close()
 {
     IsOpen = false;
     _hasShopSnapshot = false;
     SetMerchantPanelVisible(false);
     if (_root != null)
     {
         _root.style.display = DisplayStyle.None;
         _root.pickingMode = PickingMode.Ignore; // невидимое окно не должно ловить клики
     }
     // T-Q11c-fix: вернуть курсор в locked mode если сеть запущена (player in-game).
     var nm = Unity.Netcode.NetworkManager.Singleton;
     if (nm != null && nm.IsListening)
     {
         UnityEngine.Cursor.lockState = CursorLockMode.Locked;
         UnityEngine.Cursor.visible = false;
     }
 }

 private void BuildUI()
 {
 if (_root == null) EnsureBuilt();
 if (_root == null) return;

 if (_npcNameLabel != null)
 {
 string npcLabel = !string.IsNullOrEmpty(_currentStep.speakerDisplayName)
     ? _currentStep.speakerDisplayName
     : _currentStep.speakerNpcId;
 _npcNameLabel.text = !string.IsNullOrEmpty(npcLabel)
     ? $"💬 {npcLabel}"
     : "💬 NPC";
 }

 // T-Q13: update NpcAttitude badge для текущего NPC.
 UpdateNpcAttitudeBadge();

 // T-Q12: запустить typewriter для speakerText (char-by-char).
 if (_textLabel != null)
 {
 var locText = ProjectC.Localization.Loc.Get(_currentStep.speakerText, _currentStep.speakerText);
StartTypewriter(locText ?? "");
 }

 // Clear old options
 if (_optionsContainer == null) return;
 _optionsContainer.Clear();

 // Add options
 if (_currentStep.options != null && _currentStep.options.Length >0)
 {
 for (int i =0; i < _currentStep.options.Length; i++)
 {
 int idx = i;
 var opt = _currentStep.options[i];
 var btn = new Button(() => OnOptionClicked(idx));
 var optionLabel = ProjectC.Localization.Loc.Get(opt.label, opt.label);
 btn.text = !opt.available
 ? $"{optionLabel} [{Loc.Get("ui.dialog.option_unavailable", "Недоступно")}: {opt.unavailableReason}]"
 : optionLabel;
 StyleButton(btn, opt.available);
 _optionsContainer.Add(btn);
 }
 }
 else
 {
 var btn = new Button(EndConversation) { text = ProjectC.Localization.Loc.Get("ui.dialog.end") };
 StyleButton(btn, true);
 _optionsContainer.Add(btn);
 }
 UpdateToast();
 }

 // ============================================================
 // T-Q12: Typewriter + skip (F / click)
 // ============================================================

 private void StartTypewriter(string fullText)
 {
 StopTypewriterImmediate();
 if (string.IsNullOrEmpty(fullText) || _textLabel == null)
 {
 if (_textLabel != null) _textLabel.text = fullText ?? "";
 return;
 }
 _fullText = fullText;
 _displayedCharCount =0;
 _typewriterCoroutine = StartCoroutine(TypewriterRoutine(fullText));
 if (Debug.isDebugBuild) Debug.Log($"[DialogWindow] Typewriter started: '{Truncate(fullText,40)}' ({fullText.Length} chars @ {charsPerSecond}/sec)");
 }

 private IEnumerator TypewriterRoutine(string fullText)
 {
 // Первый символ сразу.
 _textLabel.text = fullText.Substring(0,1);
 _displayedCharCount =1;
 float interval =1f / Mathf.Max(0.1f, charsPerSecond);
 while (_displayedCharCount < fullText.Length)
 {
 yield return new WaitForSeconds(interval);
 if (_textLabel == null) yield break;
 _displayedCharCount++;
 _textLabel.text = fullText.Substring(0, _displayedCharCount);
 }
 _typewriterCoroutine = null;
 }

 private void SkipTypewriter()
 {
 if (_typewriterCoroutine == null) return;
 StopTypewriterImmediate();
 if (_textLabel != null && _fullText != null) _textLabel.text = _fullText;
 if (Debug.isDebugBuild) Debug.Log($"[DialogWindow] Typewriter skipped (full={(_fullText?.Length ??0)} chars)");
 }

 private void StopTypewriterImmediate()
 {
 if (_typewriterCoroutine != null)
 {
 StopCoroutine(_typewriterCoroutine);
 _typewriterCoroutine = null;
 }
 }

 private void SetTextImmediate(string text)
 {
 StopTypewriterImmediate();
 _fullText = text;
 _displayedCharCount = text?.Length ??0;
 if (_textLabel != null) _textLabel.text = text ?? "";
 }

 private void OnFSkipTypewriter()
 {
 // F press → skip typewriter (только при IsOpen + typewriter в процессе).
 // Конфликт с PlayerStateMachine boarding безопасен: boarding без ship = no-op.
 if (!IsOpen) return;
 if (_typewriterCoroutine == null) return;
 SkipTypewriter();
 }

 private void OnTextPointerDown(PointerDownEvent evt)
 {
 // Click на тексте → skip. На option button НЕ реагируем (event уже consumed).
 // Безопасный путь: если typewriter в процессе — skip, иначе ничего не делаем.
 if (_typewriterCoroutine == null) return;
 SkipTypewriter();
 evt.StopPropagation();
 }

 private static string Truncate(string s, int max)
 {
 if (string.IsNullOrEmpty(s)) return "";
 return s.Length <= max ? s : s.Substring(0, max) + "…";
 }

 private void UpdateToast()
 {
 if (_toastLabel == null) return;
 if (Time.time - _lastActionMessageTime <5f && !string.IsNullOrEmpty(_lastActionMessage))
 {
 _toastLabel.text = $"[Action] {_lastActionMessage}";
 _toastLabel.style.display = DisplayStyle.Flex;
 }
 else
 {
 _toastLabel.style.display = DisplayStyle.None;
 }

 if (_merchantStatusLabel != null)
 {
     if (Time.time - _merchantStatusTime < 6f && !string.IsNullOrEmpty(_merchantStatusText))
     {
         _merchantStatusLabel.text = _merchantStatusText;
         _merchantStatusLabel.style.color = _merchantStatusSuccess
             ? new Color(0.55f, 0.95f, 0.65f)
             : new Color(0.95f, 0.45f, 0.45f);
     }
     else
     {
         _merchantStatusLabel.text = "";
     }
 }
 }

 // ============================================================
 // T-MERCH01: Правый блок магазина в DialogWindow
 // ============================================================

 private void BindMerchantCallbacks()
 {
     if (_tabBuyBtn != null)
         _tabBuyBtn.clicked += () => SwitchMerchantTab(true);
     if (_tabSellBtn != null)
         _tabSellBtn.clicked += () => SwitchMerchantTab(false);

     if (_merchantSearchField != null)
     {
         _merchantSearchField.RegisterValueChangedCallback(evt =>
         {
             _searchQuery = evt.newValue ?? "";
             RebuildMerchantItemsList();
         });
     }

     if (_qtyMinusBtn != null) _qtyMinusBtn.clicked += () => AdjustQuantity(-1);
     if (_qtyPlusBtn != null) _qtyPlusBtn.clicked += () => AdjustQuantity(+1);
     if (_qtyX1Btn != null) _qtyX1Btn.clicked += () => SetQuantity(1);
     if (_qtyX5Btn != null) _qtyX5Btn.clicked += () => SetQuantity(5);
     if (_qtyX10Btn != null) _qtyX10Btn.clicked += () => SetQuantity(10);
     if (_qtyMaxBtn != null) _qtyMaxBtn.clicked += SetMaxQuantity;
     if (_tradeActionBtn != null) _tradeActionBtn.clicked += OnExecuteMerchantTradeClicked;

     BuildCategoryFilterButtons();
 }

 private void BuildCategoryFilterButtons()
 {
     if (_merchantCategoriesContainer == null) return;
     _merchantCategoriesContainer.Clear();

     AddCategoryChip(-1, "Все");
     AddCategoryChip((int)ItemType.Resources, "Ресурсы");
     AddCategoryChip((int)ItemType.Equipment, "Оборуд.");
     AddCategoryChip((int)ItemType.Food, "Еда");
     AddCategoryChip((int)ItemType.Fuel, "Топливо");
     AddCategoryChip((int)ItemType.Antigrav, "Антиграв");
     AddCategoryChip((int)ItemType.Meziy, "Мезий");
     AddCategoryChip((int)ItemType.Medical, "Медицина");
     AddCategoryChip((int)ItemType.Tech, "Техника");
 }

 private void AddCategoryChip(int catValue, string title)
 {
     var chip = new Button(() =>
     {
         _selectedCategoryFilter = catValue;
         UpdateCategoryChipVisuals();
         RebuildMerchantItemsList();
     })
     {
         text = title,
         userData = catValue
     };
     chip.AddToClassList("merchant-cat-chip");
     if (catValue == _selectedCategoryFilter)
         chip.AddToClassList("merchant-cat-chip-active");
     _merchantCategoriesContainer.Add(chip);
 }

 private void UpdateCategoryChipVisuals()
 {
     if (_merchantCategoriesContainer == null) return;
     foreach (var child in _merchantCategoriesContainer.Children())
     {
         if (child is Button b && b.userData is int val)
         {
             b.EnableInClassList("merchant-cat-chip-active", val == _selectedCategoryFilter);
         }
     }
 }

 private void SetMerchantPanelVisible(bool visible)
 {
     if (_merchantPanel != null)
         _merchantPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
     if (_panel != null)
         _panel.EnableInClassList("dialog-panel-narrow", visible);
 }

 private void SwitchMerchantTab(bool isBuy)
 {
     if (_isBuyTab == isBuy) return;
     _isBuyTab = isBuy;
     _selectedItemId = -1;
     _hoveredItemId = -1;
     _selectedQuantity = 1;
     if (_tabBuyBtn != null) _tabBuyBtn.EnableInClassList("merchant-tab-active", _isBuyTab);
     if (_tabSellBtn != null) _tabSellBtn.EnableInClassList("merchant-tab-active", !_isBuyTab);
     RebuildMerchantItemsList();
     RefreshDetailAndActionPanel();
 }

 private void HandleMerchantShopReceived(MerchantShopSnapshotDto snapshot)
 {
     _currentShopSnapshot = snapshot;
     _hasShopSnapshot = true;
     if (!IsOpen) return;

     SetMerchantPanelVisible(snapshot.isMerchant);
     UpdateNpcAttitudeBadge();
     if (snapshot.isMerchant)
         RefreshMerchantUI();
 }

 private void HandleMerchantTradeResultReceived(MerchantTradeResultDto result)
 {
     _merchantStatusText = (result.IsSuccess ? "✅ " : "❌ ") + (result.message ?? "");
     _merchantStatusSuccess = result.IsSuccess;
     _merchantStatusTime = Time.time;
     UpdateToast();
     UpdateNpcAttitudeBadge();
 }

 private void RefreshMerchantUI()
 {
     if (!_hasShopSnapshot || !_currentShopSnapshot.isMerchant) return;

     if (_merchantTitleLabel != null)
         _merchantTitleLabel.text = !string.IsNullOrEmpty(_currentShopSnapshot.shopTitle)
             ? $"🛒 {_currentShopSnapshot.shopTitle}"
             : "🛒 Торговая лавка";

     if (_merchantTestBadge != null)
         _merchantTestBadge.style.display = _currentShopSnapshot.isTestSellAllMode ? DisplayStyle.Flex : DisplayStyle.None;

     if (_merchantCreditsLabel != null)
         _merchantCreditsLabel.text = $"💰 {Mathf.FloorToInt(_currentShopSnapshot.playerCredits):N0} CR";

     if (_merchantSlotsLabel != null)
         _merchantSlotsLabel.text = $"🎒 {_currentShopSnapshot.inventoryUsedSlots} / {_currentShopSnapshot.inventoryMaxSlots}";

     if (_merchantRepInfoLabel != null)
     {
         float att = _currentShopSnapshot.npcAttitudeExact;
         float rep = _currentShopSnapshot.factionReputationExact;
         string attSign = att > 0.001f ? "+" : "";
         string repSign = rep > 0.001f ? "+" : "";
         string facTitle = !string.IsNullOrEmpty(_currentShopSnapshot.factionDisplayName)
             ? _currentShopSnapshot.factionDisplayName
             : "Фракция";
         string discStr = "";
         if (_currentShopSnapshot.discountPercent < 0)
             discStr = $"  •  Скидка {-_currentShopSnapshot.discountPercent}%";
         else if (_currentShopSnapshot.discountPercent > 0)
             discStr = $"  •  Наценка +{_currentShopSnapshot.discountPercent}%";

         _merchantRepInfoLabel.text = $"❤ {attSign}{att:0.0}  •  ⚑ {facTitle}: {repSign}{rep:0.0}{discStr}";
     }

     if (_tabSellBtn != null)
     {
         _tabSellBtn.SetEnabled(_currentShopSnapshot.allowPlayerSell);
         if (!_currentShopSnapshot.allowPlayerSell && !_isBuyTab)
             _isBuyTab = true;
     }
     if (_tabBuyBtn != null) _tabBuyBtn.EnableInClassList("merchant-tab-active", _isBuyTab);
     if (_tabSellBtn != null) _tabSellBtn.EnableInClassList("merchant-tab-active", !_isBuyTab);

     RebuildMerchantItemsList();
     RefreshDetailAndActionPanel();
 }

 private void RebuildMerchantItemsList()
 {
     if (_merchantItemsList == null) return;
     _merchantItemsList.Clear();
     _merchantRowsById.Clear();
     if (!_hasShopSnapshot || !_currentShopSnapshot.isMerchant) return;

     string q = (_searchQuery ?? "").Trim().ToLowerInvariant();
     int shownCount = 0;
     int hiddenCount = 0;

     if (_isBuyTab)
     {
         var arr = _currentShopSnapshot.shopItems;
         if (arr != null)
         {
             for (int i = 0; i < arr.Length; i++)
             {
                 var item = arr[i];
                 if (_selectedCategoryFilter >= 0 && item.itemType != (byte)_selectedCategoryFilter)
                     continue;
                 if (!string.IsNullOrEmpty(q) && (item.itemName == null || item.itemName.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0))
                     continue;

                 if (_selectedItemId <= 0 && item.isUnlocked)
                     _selectedItemId = item.itemId;

                 if (shownCount >= MaxMerchantRows) { hiddenCount++; continue; }
                 var buyRow = CreateBuyItemRow(item);
                 _merchantRowsById[item.itemId] = buyRow;
                 _merchantItemsList.Add(buyRow);
                 shownCount++;
             }
         }
     }
     else
     {
         var arr = _currentShopSnapshot.playerSellableItems;
         if (arr != null)
         {
             for (int i = 0; i < arr.Length; i++)
             {
                 var item = arr[i];
                 if (_selectedCategoryFilter >= 0 && item.itemType != (byte)_selectedCategoryFilter)
                     continue;
                 if (!string.IsNullOrEmpty(q) && (item.itemName == null || item.itemName.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0))
                     continue;

                 if (_selectedItemId <= 0)
                     _selectedItemId = item.itemId;

                 if (shownCount >= MaxMerchantRows) { hiddenCount++; continue; }
                 var sellRow = CreateSellItemRow(item);
                 _merchantRowsById[item.itemId] = sellRow;
                 _merchantItemsList.Add(sellRow);
                 shownCount++;
             }
         }
     }

     if (hiddenCount > 0)
     {
         var moreLbl = new Label($"…и ещё {hiddenCount}. Уточните поиск или выберите категорию.")
         {
             style =
             {
                 color = new Color(0.65f, 0.7f, 0.8f),
                 paddingTop = 8,
                 paddingLeft = 10
             }
         };
         _merchantItemsList.Add(moreLbl);
     }

     if (shownCount == 0)
     {
         var emptyLbl = new Label(_isBuyTab ? "Нет подходящих товаров в ассортименте." : "В вашем рюкзаке нет предметов для продажи.")
         {
             style =
             {
                 color = new Color(0.65f, 0.7f, 0.8f),
                 paddingTop = 18,
                 paddingLeft = 10
             }
         };
         _merchantItemsList.Add(emptyLbl);
     }
 }

 // T-PERF26: клик по строке раньше перестраивал весь список (сотни строк) — теперь только классы двух строк.
 private void SelectMerchantRow(int itemId)
 {
     if (_merchantRowsById.TryGetValue(_selectedItemId, out var prevRow))
         prevRow.RemoveFromClassList("merchant-item-row-selected");
     _selectedItemId = itemId;
     if (_merchantRowsById.TryGetValue(itemId, out var newRow))
         newRow.AddToClassList("merchant-item-row-selected");
 }

 private VisualElement CreateBuyItemRow(MerchantItemDto item)
 {
     var row = new VisualElement();
     row.AddToClassList("merchant-item-row");
     if (item.itemId == _selectedItemId)
         row.AddToClassList("merchant-item-row-selected");
     if (!item.isUnlocked || item.stock == 0)
         row.AddToClassList("merchant-item-row-locked");

     var left = new VisualElement();
     left.AddToClassList("merchant-item-left");
     left.Add(CreateItemIconElement(item.itemId, (ItemType)item.itemType));

     var nameLbl = new Label(item.itemName);
     nameLbl.AddToClassList("merchant-item-name");
     left.Add(nameLbl);

     string metaText = $"[{ItemTypeNames.GetDisplayName((ItemType)item.itemType)}]";
     if (item.playerOwnedCount > 0)
         metaText += $" • В рюкз: {item.playerOwnedCount}";
     var metaLbl = new Label(metaText);
     metaLbl.AddToClassList("merchant-item-meta");
     left.Add(metaLbl);

     var right = new VisualElement();
     right.AddToClassList("merchant-item-right");

     if (!item.isUnlocked)
     {
         var lockLbl = new Label($"🔒 {item.lockedReason}");
         lockLbl.AddToClassList("merchant-item-locked-label");
         right.Add(lockLbl);
     }
     else
     {
         string stockStr = item.stock < 0 ? "Запас: ∞" : (item.stock > 0 ? $"Запас: {item.stock}" : "Нет в наличии");
         var stockLbl = new Label(stockStr);
         stockLbl.AddToClassList("merchant-item-stock");
         right.Add(stockLbl);

         var priceLbl = new Label($"{item.buyPrice} CR");
         priceLbl.AddToClassList("merchant-item-price");
         right.Add(priceLbl);
     }

     row.Add(left);
     row.Add(right);

     int capturedId = item.itemId;
     row.RegisterCallback<PointerEnterEvent>(_ =>
     {
         _hoveredItemId = capturedId;
         RefreshDetailAndActionPanel();
     });
     row.RegisterCallback<PointerLeaveEvent>(_ =>
     {
         if (_hoveredItemId == capturedId) _hoveredItemId = -1;
         RefreshDetailAndActionPanel();
     });
     row.RegisterCallback<ClickEvent>(_ =>
     {
         SelectMerchantRow(capturedId);
         _selectedQuantity = Mathf.Clamp(_selectedQuantity, 1, Mathf.Max(1, CalculateMaxAllowedQuantity()));
         RefreshDetailAndActionPanel();
     });

     return row;
 }

 private VisualElement CreateSellItemRow(MerchantPlayerSellItemDto item)
 {
     var row = new VisualElement();
     row.AddToClassList("merchant-item-row");
     if (item.itemId == _selectedItemId)
         row.AddToClassList("merchant-item-row-selected");

     var left = new VisualElement();
     left.AddToClassList("merchant-item-left");
     left.Add(CreateItemIconElement(item.itemId, (ItemType)item.itemType));

     var nameLbl = new Label(item.itemName);
     nameLbl.AddToClassList("merchant-item-name");
     left.Add(nameLbl);

     var metaLbl = new Label($"[{ItemTypeNames.GetDisplayName((ItemType)item.itemType)}]");
     metaLbl.AddToClassList("merchant-item-meta");
     left.Add(metaLbl);

     var right = new VisualElement();
     right.AddToClassList("merchant-item-right");

     var stockLbl = new Label($"В рюкзаке: x{item.ownedCount}");
     stockLbl.AddToClassList("merchant-item-stock");
     right.Add(stockLbl);

     var priceLbl = new Label($"+{item.sellPrice} CR");
     priceLbl.AddToClassList("merchant-item-price");
     right.Add(priceLbl);

     row.Add(left);
     row.Add(right);

     int capturedId = item.itemId;
     row.RegisterCallback<PointerEnterEvent>(_ =>
     {
         _hoveredItemId = capturedId;
         RefreshDetailAndActionPanel();
     });
     row.RegisterCallback<PointerLeaveEvent>(_ =>
     {
         if (_hoveredItemId == capturedId) _hoveredItemId = -1;
         RefreshDetailAndActionPanel();
     });
     row.RegisterCallback<ClickEvent>(_ =>
     {
         SelectMerchantRow(capturedId);
         _selectedQuantity = Mathf.Clamp(_selectedQuantity, 1, Mathf.Max(1, CalculateMaxAllowedQuantity()));
         RefreshDetailAndActionPanel();
     });

     return row;
 }

 private VisualElement CreateItemIconElement(int itemId, ItemType type)
 {
     var iconBox = new VisualElement();
     iconBox.AddToClassList("merchant-item-icon");
     iconBox.style.backgroundColor = GetCategoryColor(type);

     var def = InventoryClientState.Instance != null ? InventoryClientState.Instance.GetItemDefinition(itemId) : null;
     if (def != null && def.icon != null)
     {
         iconBox.style.backgroundImage = new StyleBackground(def.icon);
     }
     else
     {
         var letter = new Label(GetCategoryShortTag(type));
         letter.AddToClassList("merchant-icon-letter");
         iconBox.Add(letter);
     }
     return iconBox;
 }

 private void RefreshDetailAndActionPanel()
 {
     int inspectId = _hoveredItemId > 0 ? _hoveredItemId : _selectedItemId;
     var def = inspectId > 0 && InventoryClientState.Instance != null
         ? InventoryClientState.Instance.GetItemDefinition(inspectId)
         : null;

     if (_isBuyTab && TryFindBuyItem(inspectId, out var buyDto))
     {
         ApplyDetailCard(def, buyDto.itemName, (ItemType)buyDto.itemType,
             $"Цена: {buyDto.buyPrice} CR  •  Скупка: {buyDto.sellPrice} CR  •  У вас в рюкзаке: {buyDto.playerOwnedCount} шт.");
     }
     else if (!_isBuyTab && TryFindSellItem(inspectId, out var sellDto))
     {
         ApplyDetailCard(def, sellDto.itemName, (ItemType)sellDto.itemType,
             $"Цена скупки: +{sellDto.sellPrice} CR / шт.  •  В рюкзаке: {sellDto.ownedCount} шт.");
     }
     else
     {
         if (_detailTitleLabel != null) _detailTitleLabel.text = "Наведите курсор или кликните по товару";
         if (_detailSpecsLabel != null) _detailSpecsLabel.text = "Здесь появятся характеристики предмета, вес и описание.";
         if (_detailDescLabel != null) _detailDescLabel.text = "";
         if (_detailIconBox != null)
         {
             _detailIconBox.style.backgroundImage = StyleKeyword.None;
             _detailIconBox.style.backgroundColor = new Color(0.2f, 0.25f, 0.36f);
         }
         if (_detailIconLetter != null) _detailIconLetter.text = "?";
     }

     // Блок количества и кнопки действия считает по ВЫБРАННОМУ предмету (_selectedItemId)
     int maxAllowed = CalculateMaxAllowedQuantity();
     _selectedQuantity = Mathf.Clamp(_selectedQuantity, 1, Mathf.Max(1, maxAllowed));
     if (_qtyValueLabel != null) _qtyValueLabel.text = _selectedQuantity.ToString();

     if (_isBuyTab && TryFindBuyItem(_selectedItemId, out var selBuy))
     {
         int totalCost = selBuy.buyPrice * _selectedQuantity;
         float repGain = MerchantPriceResolver.CalculateReputationGain(totalCost);
         if (_tradeSummaryLabel != null)
             _tradeSummaryLabel.text = $"Итого: {totalCost} CR   •   Репутация: +{repGain:0.##} ❤/⚑";

         bool canBuy = selBuy.isUnlocked
                       && (selBuy.stock < 0 || selBuy.stock >= _selectedQuantity)
                       && _currentShopSnapshot.playerCredits + 1e-3f >= totalCost
                       && (_currentShopSnapshot.inventoryUsedSlots + _selectedQuantity <= _currentShopSnapshot.inventoryMaxSlots);

         if (_tradeActionBtn != null)
         {
             _tradeActionBtn.EnableInClassList("merchant-action-btn-sell", false);
             _tradeActionBtn.SetEnabled(canBuy);
             _tradeActionBtn.text = !selBuy.isUnlocked
                 ? "🔒 Заблокировано"
                 : (selBuy.stock == 0 ? "Нет в наличии" : $"Купить x{_selectedQuantity} ({totalCost} CR)");
         }
     }
     else if (!_isBuyTab && TryFindSellItem(_selectedItemId, out var selSell))
     {
         int totalEarn = selSell.sellPrice * _selectedQuantity;
         if (_tradeSummaryLabel != null)
             _tradeSummaryLabel.text = $"Выручка: +{totalEarn} CR";

         bool canSell = _currentShopSnapshot.allowPlayerSell && selSell.ownedCount >= _selectedQuantity;
         if (_tradeActionBtn != null)
         {
             _tradeActionBtn.EnableInClassList("merchant-action-btn-sell", true);
             _tradeActionBtn.SetEnabled(canSell);
             _tradeActionBtn.text = $"Продать x{_selectedQuantity} (+{totalEarn} CR)";
         }
     }
     else
     {
         if (_tradeSummaryLabel != null) _tradeSummaryLabel.text = "Выберите предмет";
         if (_tradeActionBtn != null)
         {
             _tradeActionBtn.SetEnabled(false);
             _tradeActionBtn.text = _isBuyTab ? "Купить" : "Продать";
         }
     }
 }

 private void ApplyDetailCard(ItemData def, string fallbackName, ItemType itemType, string economyLine)
 {
     if (_detailTitleLabel != null)
         _detailTitleLabel.text = def != null && !string.IsNullOrEmpty(def.itemName) ? def.itemName : fallbackName;

     if (_detailSpecsLabel != null)
     {
         string specs = def != null ? MerchantPriceResolver.BuildItemSpecsSummary(def) : $"Категория: {ItemTypeNames.GetDisplayName(itemType)}";
         _detailSpecsLabel.text = $"{specs}\n{economyLine}";
     }

     if (_detailDescLabel != null)
         _detailDescLabel.text = def != null && !string.IsNullOrEmpty(def.description) ? def.description : "";

     if (_detailIconBox != null)
     {
         _detailIconBox.style.backgroundColor = GetCategoryColor(itemType);
         if (def != null && def.icon != null)
         {
             _detailIconBox.style.backgroundImage = new StyleBackground(def.icon);
             if (_detailIconLetter != null) _detailIconLetter.text = "";
         }
         else
         {
             _detailIconBox.style.backgroundImage = StyleKeyword.None;
             if (_detailIconLetter != null) _detailIconLetter.text = GetCategoryShortTag(itemType);
         }
     }
 }

 private void AdjustQuantity(int delta)
 {
     SetQuantity(_selectedQuantity + delta);
 }

 private void SetQuantity(int newQty)
 {
     int maxAllowed = Mathf.Max(1, CalculateMaxAllowedQuantity());
     _selectedQuantity = Mathf.Clamp(newQty, 1, maxAllowed);
     RefreshDetailAndActionPanel();
 }

 private void SetMaxQuantity()
 {
     _selectedQuantity = Mathf.Max(1, CalculateMaxAllowedQuantity());
     RefreshDetailAndActionPanel();
 }

 private int CalculateMaxAllowedQuantity()
 {
     if (!_hasShopSnapshot) return 1;
     if (_isBuyTab && TryFindBuyItem(_selectedItemId, out var buyItem))
     {
         int freeSlots = Mathf.Max(0, _currentShopSnapshot.inventoryMaxSlots - _currentShopSnapshot.inventoryUsedSlots);
         int byCredits = buyItem.buyPrice > 0 ? Mathf.FloorToInt(_currentShopSnapshot.playerCredits / buyItem.buyPrice) : freeSlots;
         int byStock = buyItem.stock < 0 ? 999 : buyItem.stock;
         int max = Mathf.Min(freeSlots, Mathf.Min(byCredits, byStock));
         return Mathf.Clamp(max, 1, 999);
     }
     if (!_isBuyTab && TryFindSellItem(_selectedItemId, out var sellItem))
     {
         return Mathf.Clamp(sellItem.ownedCount, 1, 999);
     }
     return 1;
 }

 private void OnExecuteMerchantTradeClicked()
 {
     if (!_hasShopSnapshot || string.IsNullOrEmpty(_currentShopSnapshot.npcId) || _selectedItemId <= 0) return;
     int qty = Mathf.Max(1, _selectedQuantity);
     if (_isBuyTab)
         QuestClientState.Instance?.RequestMerchantBuy(_currentShopSnapshot.npcId, _selectedItemId, qty);
     else
         QuestClientState.Instance?.RequestMerchantSell(_currentShopSnapshot.npcId, _selectedItemId, qty);
 }

 private bool TryFindBuyItem(int itemId, out MerchantItemDto found)
 {
     found = default;
     if (itemId <= 0 || !_hasShopSnapshot || _currentShopSnapshot.shopItems == null) return false;
     var arr = _currentShopSnapshot.shopItems;
     for (int i = 0; i < arr.Length; i++)
     {
         if (arr[i].itemId == itemId) { found = arr[i]; return true; }
     }
     return false;
 }

 private bool TryFindSellItem(int itemId, out MerchantPlayerSellItemDto found)
 {
     found = default;
     if (itemId <= 0 || !_hasShopSnapshot || _currentShopSnapshot.playerSellableItems == null) return false;
     var arr = _currentShopSnapshot.playerSellableItems;
     for (int i = 0; i < arr.Length; i++)
     {
         if (arr[i].itemId == itemId) { found = arr[i]; return true; }
     }
     return false;
 }

 private static Color GetCategoryColor(ItemType type)
 {
     return type switch
     {
         ItemType.Resources => new Color(0.32f, 0.44f, 0.34f),
         ItemType.Equipment => new Color(0.34f, 0.42f, 0.58f),
         ItemType.Food      => new Color(0.55f, 0.42f, 0.24f),
         ItemType.Fuel      => new Color(0.58f, 0.34f, 0.22f),
         ItemType.Antigrav  => new Color(0.28f, 0.48f, 0.62f),
         ItemType.Meziy     => new Color(0.48f, 0.28f, 0.58f),
         ItemType.Medical   => new Color(0.58f, 0.25f, 0.32f),
         ItemType.Tech      => new Color(0.28f, 0.46f, 0.48f),
         _                  => new Color(0.35f, 0.38f, 0.45f)
     };
 }

 private static string GetCategoryShortTag(ItemType type)
 {
     return type switch
     {
         ItemType.Resources => "РЕС",
         ItemType.Equipment => "ЭКП",
         ItemType.Food      => "ЕДА",
         ItemType.Fuel      => "ТПЛ",
         ItemType.Antigrav  => "АГ",
         ItemType.Meziy     => "МЕЗ",
         ItemType.Medical   => "МЕД",
         ItemType.Tech      => "ТЕХ",
         _                  => "ПРД"
     };
 }

 private void Update()
 {
 UpdateToast();
 // T-Q12: lazy-subscribe PlayerInputReader если ещё не подписан (race с NMC.Awake).
 if (!_inputSubscribed) TrySubscribeInput();
 // T-Q12-fix: lazy-subscribe QuestClientState — race condition между scene-placed GO
 // (порядок OnEnable между DialogWindow и QuestClientState НЕ гарантирован).
 // Без этого — после domain reload _subscribed остаётся false и event не приходит.
 if (!_subscribed) TrySubscribe();
 // T-Q13: lazy-subscribe NpcAttitudeClientState (тот же race-condition pattern).
 if (!_attitudeSubscribed) TrySubscribeAttitude();
 if (IsOpen && UnityEngine.InputSystem.Keyboard.current != null
 && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
 {
 EndConversation();
 }
 }

 private void OnOptionClicked(int optionIndex)
 {
 SendAdvance(optionIndex);
 }

 private void EndConversation()
 {
 Close();
 var localPlayer = GetLocal();
 if (localPlayer != null) localPlayer.RequestEndConversation();
 }

 private void SendAdvance(int optionIndex)
 {
 var localPlayer = GetLocal();
 if (localPlayer == null) return;
 // T-Q11c-fix: игнор stale click (после isEnd step, _currentStep пустой, или button пересоздан)
 if (string.IsNullOrEmpty(_currentStep.treeId) || string.IsNullOrEmpty(_currentStep.nodeId)) return;
 localPlayer.RequestAdvanceDialogue(_currentStep.treeId, _currentStep.nodeId, optionIndex, _currentStep.speakerNpcId);
 }

 private static ProjectC.Player.NetworkPlayer GetLocal()
 {
 var nm = Unity.Netcode.NetworkManager.Singleton;
 if (nm == null || nm.LocalClient == null) return null;
 var po = nm.LocalClient.PlayerObject;
 return po != null ? po.GetComponent<ProjectC.Player.NetworkPlayer>() : null;
 }
 }
}
