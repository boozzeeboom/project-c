// T-Q11a: DialogWindow — клиентский UI для NPC dialog.
// Получает DialogStepDto через QuestClientState.OnDialogStepReceived.
// T-Q11a scope: IMGUI (минимально, без assets). T-Q11b: переписать на UIDocument + UXML.
//
// Controls:
//   - Click on option button → RequestAdvanceDialogueRpc
//   - ESC or click X → close (только UI hide, server session остаётся)
//   - E to "use" — вызывается извне (PlayerInteractor)

using UnityEngine;
using ProjectC.Quests.Dto;
using ProjectC.Quests.Client;

namespace ProjectC.Quests.UI
{
    /// <summary>
    /// Простое dialog-окно: текст NPC + кнопки options.
    /// </summary>
    /// <remarks>
    /// T-Q11a: IMGUI fallback (no assets needed).
    /// T-Q11b: replace с UIDocument + UXML/USS + animations.
    /// </remarks>
    public class DialogWindow : MonoBehaviour
    {
        public static DialogWindow Instance { get; private set; }

        [Header("Layout")]
        [SerializeField] private Vector2 windowSize = new Vector2(800, 400);
        [SerializeField] private int padding = 20;
        [SerializeField] private int buttonHeight = 40;
        [SerializeField] private int buttonSpacing = 5;
        [SerializeField] private int titleHeight = 30;

        [Header("Style")]
        [SerializeField] private Color backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color buttonColor = new Color(0.2f, 0.3f, 0.5f);
        [SerializeField] private Color buttonHoverColor = new Color(0.3f, 0.4f, 0.7f);
        [SerializeField] private Color buttonDisabledColor = new Color(0.3f, 0.3f, 0.3f);

        // State
        private bool _isOpen;
        private DialogStepDto _currentStep;
        private string _lastActionMessage;
        private float _lastActionMessageTime;

        // Cached GUI styles
        private GUIStyle _windowStyle;
        private GUIStyle _textStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _buttonDisabledStyle;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            TryUnsubscribe();
        }

        private void Start()
        {
            // Re-attempt subscription in case QuestClientState was created after this component.
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

        private void HandleStepReceived(DialogStepDto step)
        {
            _currentStep = step;
            _isOpen = !step.isEnd;
            if (step.isEnd)
            {
                Debug.Log($"[DialogWindow] Dialog ended: tree={step.treeId}");
            }
        }

        private void HandleActionResultReceived(DialogActionResultDto result)
        {
            string status = result.success ? "OK" : "FAIL";
            _lastActionMessage = $"{status}: {result.actionType} {result.resultData}";
            _lastActionMessageTime = Time.time;
            if (Debug.isDebugBuild) Debug.Log($"[DialogWindow] Action result: {_lastActionMessage}");
        }

        private void Update()
        {
            // ESC → close window (only hide, server session remains)
            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                _isOpen = false;
            }
        }

        public bool IsOpen => _isOpen;

        public void Open()
        {
            _isOpen = true;
        }

        public void Close()
        {
            _isOpen = false;
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;
            // Solid color texture for background
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, backgroundColor);
            bgTex.Apply();
            _windowStyle = new GUIStyle(GUI.skin.box);
            _windowStyle.normal.background = bgTex;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = textColor },
                alignment = TextAnchor.MiddleLeft
            };

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = textColor },
                alignment = TextAnchor.UpperLeft
            };

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            _buttonDisabledStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            _buttonDisabledStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        }

        private void OnGUI()
        {
            if (!_isOpen) return;
            EnsureStyles();

            // Center window
            float w = windowSize.x;
            float h = windowSize.y;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;
            var windowRect = new Rect(x, y, w, h);

            GUI.Box(windowRect, GUIContent.none, _windowStyle);

            // Layout
            GUILayout.BeginArea(new Rect(x + padding, y + padding, w - 2 * padding, h - 2 * padding));

            // Title (NPC name)
            string npcName = !string.IsNullOrEmpty(_currentStep.speakerNpcId) ? _currentStep.speakerNpcId : "NPC";
            GUILayout.Label($"💬 {npcName}", _titleStyle, GUILayout.Height(titleHeight));
            GUILayout.Space(5);

            // Speaker text
            GUILayout.Label(_currentStep.speakerText ?? "", _textStyle, GUILayout.ExpandHeight(true));

            // Options
            if (_currentStep.options != null && _currentStep.options.Length > 0)
            {
                GUILayout.Space(5);
                for (int i = 0; i < _currentStep.options.Length; i++)
                {
                    var opt = _currentStep.options[i];
                    var label = !opt.available ? $"{opt.label}  [Недоступно: {opt.unavailableReason}]" : opt.label;
                    if (GUILayout.Button(label, opt.available ? _buttonStyle : _buttonDisabledStyle, GUILayout.Height(buttonHeight)))
                    {
                        if (opt.available)
                        {
                            SendAdvance(opt.index);
                        }
                    }
                    GUILayout.Space(buttonSpacing);
                }
            }
            else
            {
                if (GUILayout.Button("[ Конец ]", _buttonStyle, GUILayout.Height(buttonHeight)))
                {
                    _isOpen = false;
                }
            }

            // Action result toast (bottom)
            if (Time.time - _lastActionMessageTime < 5f && !string.IsNullOrEmpty(_lastActionMessage))
            {
                GUILayout.Space(5);
                GUILayout.Label($"[Action] {_lastActionMessage}", _textStyle);
            }

            GUILayout.EndArea();
        }

        private void SendAdvance(int optionIndex)
        {
            var localPlayer = GetLocal();
            if (localPlayer == null)
            {
                Debug.LogWarning("[DialogWindow] No local NetworkPlayer — cannot advance");
                return;
            }
            // Route: NetworkPlayer.RequestAdvanceDialogue → QuestServer.RequestAdvanceDialogueRpc.
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
