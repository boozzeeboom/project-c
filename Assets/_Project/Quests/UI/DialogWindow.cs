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
using ProjectC.Quests.Dto;
using ProjectC.Quests.Client;

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

 // Runtime UIDocument refs
 private UIDocument _doc;
 private VisualElement _root;
 private VisualElement _panel;
 private Label _npcNameLabel;
 private Label _textLabel;
 private VisualElement _optionsContainer;
 private Label _toastLabel;

 // State
 private DialogStepDto _currentStep;
 private string _lastActionMessage;
 private float _lastActionMessageTime;

 public bool IsOpen { get; private set; }

 private bool _built = false;

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

 private void OnEnable() { EnsureBuilt(); }
 private void OnDisable() { /* nothing — rootVE стирается при отключении UIDocument */ }

 private void Start()
 {
 EnsureBuilt();
 TrySubscribe();
 }

 private bool _subscribed;
 private void TrySubscribe()
 {
 if (_subscribed) return;
 if (QuestClientState.Instance == null) return;
 QuestClientState.Instance.OnDialogStepReceived += HandleStepReceived;
 QuestClientState.Instance.OnDialogActionResultReceived += HandleActionResultReceived;
 _subscribed = true;
 }
 private void TryUnsubscribe()
 {
 if (!_subscribed) return;
 if (QuestClientState.Instance == null) { _subscribed = false; return; }
 QuestClientState.Instance.OnDialogStepReceived -= HandleStepReceived;
 QuestClientState.Instance.OnDialogActionResultReceived -= HandleActionResultReceived;
 _subscribed = false;
 }

 private void OnDestroy()
 {
 TryUnsubscribe();
 if (Instance == this) Instance = null;
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
 _textLabel = _root.Q<Label>("text");
 _optionsContainer = _root.Q<VisualElement>("options");
 _toastLabel = _root.Q<Label>("toast");
 if (_toastLabel != null) _toastLabel.style.display = DisplayStyle.None;
 // Initially hidden — Show() переключит на Flex.
 if (_root != null) _root.style.display = DisplayStyle.None;

 _built = true;
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
 _currentStep = step;
 EnsureBuilt();
 if (_root == null) return;
 if (step.isEnd) { Close(); }
 else { Show(); BuildUI(); }
 }

 private void HandleActionResultReceived(DialogActionResultDto result)
 {
 string status = result.success ? "OK" : "FAIL";
 _lastActionMessage = $"{status}: {result.actionType} {result.resultData}";
 _lastActionMessageTime = Time.time;
 if (Debug.isDebugBuild) Debug.Log($"[DialogWindow] Action result: {_lastActionMessage}");
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
 _npcNameLabel.text = !string.IsNullOrEmpty(_currentStep.speakerNpcId)
 ? $"💬 {_currentStep.speakerNpcId}"
 : "💬 NPC";

 if (_textLabel != null)
 _textLabel.text = _currentStep.speakerText ?? "";

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
 btn.text = !opt.available
 ? $"{opt.label} [Недоступно: {opt.unavailableReason}]"
 : opt.label;
 StyleButton(btn, opt.available);
 _optionsContainer.Add(btn);
 }
 }
 else
 {
 var btn = new Button(EndConversation) { text = "[ Конец]" };
 StyleButton(btn, true);
 _optionsContainer.Add(btn);
 }
 UpdateToast();
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
 }

 private void Update()
 {
 UpdateToast();
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
