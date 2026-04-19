using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectC.Core;

namespace ProjectC.World.Npc
{
    /// <summary>
    /// Singleton manager for NPC dialogues.
    /// Handles dialogue UI, node navigation, and player choices.
    /// 
    /// Uses UI Toolkit or uGUI depending on project settings.
    /// This implementation uses uGUI for compatibility.
    /// </summary>
    public class NpcDialogueManager : MonoBehaviour
    {
        #region Singleton
        
        private static NpcDialogueManager _instance;
        public static NpcDialogueManager Instance
        {
            get
            {
                if (_instance == null)
                {
                _instance = UnityEngine.Object.FindAnyObjectByType<NpcDialogueManager>();
                    if (_instance == null)
                    {
                        Debug.LogWarning("[NpcDialogueManager] No instance found in scene!");
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region UI References
        
        [Header("UI Canvas")]
        [Tooltip("Main dialogue panel")]
        [SerializeField] private GameObject dialoguePanel;

        [Tooltip("NPC name text")]
        [SerializeField] private TextMeshProUGUI npcNameText;

        [Tooltip("NPC title text")]
        [SerializeField] private Text npcTitleText;

        [Tooltip("Portrait image")]
        [SerializeField] private Image portraitImage;

        [Tooltip("Dialogue text area")]
        [SerializeField] private TextMeshProUGUI dialogueText;

        [Tooltip("Container for dialogue options")]
        [SerializeField] private Transform optionsContainer;

        [Tooltip("Option button prefab")]
        [SerializeField] private GameObject optionButtonPrefab;

        [Tooltip("Close button")]
        [SerializeField] private Button closeButton;

        [Tooltip("Continue indicator when no options")]
        [SerializeField] private GameObject continueIndicator;

        #endregion

        #region Settings
        
        [Header("Settings")]
        [Tooltip("Typewriter effect speed (chars per second)")]
        [SerializeField] private float typewriterSpeed = 30f;

        [Tooltip("Allow skipping typewriter effect")]
        [SerializeField] private bool allowSkipTyping = true;

        [Header("Audio")]
        [Tooltip("Play sound on text reveal")]
        [SerializeField] private bool playTypingSound = false;

        [Tooltip("Typing sound clip")]
        [SerializeField] private AudioClip typingSound;

        private AudioSource _audioSource;

        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        #endregion

        #region State
        
        // Current dialogue state
        private NpcData _currentNpcData;
        private NpcEntity _currentNpcEntity;
        private DialogueNode _currentNode;
        private bool _isDialogueActive = false;
        private bool _isTyping = false;
        private string _fullText = "";
        private float _typeTimer = 0f;

        // Pool for option buttons
        private List<GameObject> _optionButtons = new List<GameObject>();
        private const int MAX_OPTIONS = 6;

        // Events
        public event Action<string> OnDialogueStarted;
        public event Action OnDialogueEnded;
        public event Action<DialogueOption> OnOptionSelected;

        #endregion

        #region Unity Lifecycle
        
        private void Awake()
        {
            // Singleton setup
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[NpcDialogueManager] Duplicate instance detected, destroying.");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Setup audio source
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;

            // Initialize option button pool
            InitializeOptionPool();

            // Hide panel on start
            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(false);
            }

            // Setup close button
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseDialogue);
            }
        }

        private void Update()
        {
            // Handle typewriter effect
            if (_isTyping && !string.IsNullOrEmpty(_fullText))
            {
                UpdateTypewriter();
            }

            // Allow skip typing with space/input
            if (_isTyping && allowSkipTyping && Input.GetKeyDown(KeyCode.Space))
            {
                SkipTypewriter();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion

        #region Public API
        
        /// <summary>
        /// Start a dialogue with an NPC.
        /// </summary>
        public void StartDialogue(NpcData npcData, NpcEntity npcEntity = null)
        {
            if (npcData == null)
            {
                if (debugMode)
                {
                    Debug.LogWarning("[NpcDialogueManager] NpcData is null!");
                }
                return;
            }

            // Stop any existing dialogue
            if (_isDialogueActive)
            {
                EndDialogue();
            }

            _currentNpcData = npcData;
            _currentNpcEntity = npcEntity;
            _isDialogueActive = true;

            if (debugMode)
            {
                Debug.Log($"[NpcDialogueManager] Starting dialogue with: {npcData.displayName}");
            }

            // Show UI
            ShowDialoguePanel();

            // Update UI with NPC info
            UpdateNpcInfo();

            // Start with root node
            var rootNode = npcData.GetRootNode();
            if (rootNode != null)
            {
                ShowNode(rootNode);
            }
            else
            {
                // Fallback: show default greeting
                StartTypewriter(npcData.greetingText);
                ShowContinueOption();
            }

            // Fire event
            OnDialogueStarted?.Invoke(npcData.npcId);

            // Pause game (optional, based on settings)
            // Time.timeScale = 0f; // Uncomment to pause
        }

        /// <summary>
        /// Close the current dialogue.
        /// </summary>
        public void CloseDialogue()
        {
            EndDialogue();
        }

        /// <summary>
        /// Select a dialogue option by index.
        /// </summary>
        public void SelectOption(int optionIndex)
        {
            if (_currentNode == null || _currentNode.options == null)
            {
                return;
            }

            if (optionIndex < 0 || optionIndex >= _currentNode.options.Length)
            {
                if (debugMode)
                {
                    Debug.LogWarning($"[NpcDialogueManager] Invalid option index: {optionIndex}");
                }
                return;
            }

            var option = _currentNode.options[optionIndex];

            if (debugMode)
            {
                Debug.Log($"[NpcDialogueManager] Selected option: {option.text}");
            }

            // Fire option selected event
            OnOptionSelected?.Invoke(option);

            // Process option effects
            ProcessOptionEffects(option);

            // Navigate to next node
            if (!string.IsNullOrEmpty(option.nextNodeId))
            {
                var nextNode = _currentNpcData.GetNode(option.nextNodeId);
                if (nextNode != null)
                {
                    ShowNode(nextNode);
                }
                else
                {
                    if (debugMode)
                    {
                        Debug.LogWarning($"[NpcDialogueManager] Next node not found: {option.nextNodeId}");
                    }
                    EndDialogue();
                }
            }
            else
            {
                // No next node, end dialogue
                EndDialogue();
            }
        }

        /// <summary>
        /// Check if dialogue is currently active.
        /// </summary>
        public bool IsDialogueActive()
        {
            return _isDialogueActive;
        }

        /// <summary>
        /// Get current NPC data.
        /// </summary>
        public NpcData GetCurrentNpcData()
        {
            return _currentNpcData;
        }

        #endregion

        #region Private Methods
        
        private void InitializeOptionPool()
        {
            if (optionButtonPrefab == null || optionsContainer == null)
            {
                return;
            }

            // Pre-instantiate option buttons
            for (int i = 0; i < MAX_OPTIONS; i++)
            {
                var buttonObj = Instantiate(optionButtonPrefab, optionsContainer);
                buttonObj.SetActive(false);
                _optionButtons.Add(buttonObj);
            }
        }

        private void ShowDialoguePanel()
        {
            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(true);
                // Could add fade animation here
            }
        }

        private void HideDialoguePanel()
        {
            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(false);
            }
        }

        private void UpdateNpcInfo()
        {
            if (_currentNpcData == null) return;

            if (npcNameText != null)
            {
                npcNameText.text = _currentNpcData.displayName;
            }

            if (npcTitleText != null)
            {
                npcTitleText.text = _currentNpcData.title;
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = _currentNpcData.portrait;
                portraitImage.enabled = _currentNpcData.portrait != null;
            }
        }

        private void ShowNode(DialogueNode node)
        {
            _currentNode = node;

            if (debugMode)
            {
                Debug.Log($"[NpcDialogueManager] Showing node: {node.nodeId}");
            }

            // Process node effects
            ProcessNodeEffects(node);

            // Show dialogue text
            StartTypewriter(node.text);

            // Show options or continue indicator
            if (node.options != null && node.options.Length > 0)
            {
                ShowOptions(node.options);
            }
            else
            {
                ShowContinueOption();
            }
        }

        private void StartTypewriter(string text)
        {
            _fullText = text;
            _typeTimer = 0f;
            _isTyping = true;

            if (dialogueText != null)
            {
                dialogueText.text = "";
            }
        }

        private void UpdateTypewriter()
        {
            if (!_isTyping || string.IsNullOrEmpty(_fullText))
            {
                _isTyping = false;
                return;
            }

            _typeTimer += Time.deltaTime;
            int charsToShow = Mathf.FloorToInt(_typeTimer * typewriterSpeed);
            charsToShow = Mathf.Min(charsToShow, _fullText.Length);

            if (dialogueText != null)
            {
                dialogueText.text = _fullText.Substring(0, charsToShow);
            }

            // Play typing sound
            if (playTypingSound && _audioSource != null && !_audioSource.isPlaying)
            {
                // Could play sound here with timing
            }

            // Check if typing complete
            if (charsToShow >= _fullText.Length)
            {
                _isTyping = false;
                OnTypewriterComplete();
            }
        }

        private void SkipTypewriter()
        {
            _isTyping = false;
            if (dialogueText != null)
            {
                dialogueText.text = _fullText;
            }
            OnTypewriterComplete();
        }

        private void OnTypewriterComplete()
        {
            // All text shown - options are already displayed
        }

        private void ShowOptions(DialogueOption[] options)
        {
            if (continueIndicator != null)
            {
                continueIndicator.SetActive(false);
            }

            // Clear existing buttons
            ClearOptions();

            // Show valid options
            int buttonIndex = 0;
            for (int i = 0; i < options.Length && buttonIndex < MAX_OPTIONS; i++)
            {
                var option = options[i];
                if (option.IsAvailable())
                {
                    CreateOptionButton(buttonIndex, option);
                    buttonIndex++;
                }
            }
        }

        private void CreateOptionButton(int index, DialogueOption option)
        {
            if (index >= _optionButtons.Count || optionButtonPrefab == null)
            {
                return;
            }

            var buttonObj = _optionButtons[index];
            buttonObj.SetActive(true);

            // Set button text
            var textComponent = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = option.text;
            }

            // Set button click handler
            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                // Remove existing listeners
                button.onClick.RemoveAllListeners();

                // Add new listener with captured index
                int capturedIndex = index;
                button.onClick.AddListener(() => SelectOption(capturedIndex));
            }

            // Could set disabled style for unavailable options
            // button.interactable = option.IsAvailable();
        }

        private void ClearOptions()
        {
            for (int i = 0; i < _optionButtons.Count; i++)
            {
                _optionButtons[i].SetActive(false);
            }
        }

        private void ShowContinueOption()
        {
            ClearOptions();

            if (continueIndicator != null)
            {
                continueIndicator.SetActive(true);
            }
        }

        private void ProcessNodeEffects(DialogueNode node)
        {
            // Give item if specified
            if (!string.IsNullOrEmpty(node.giveItemId))
            {
                Debug.Log($"[NpcDialogueManager] Giving item: {node.giveItemId}");
                // TODO: Add to player inventory
            }

            // Add reputation if specified
            if (node.reputationGain != 0 && _currentNpcData != null)
            {
                Debug.Log($"[NpcDialogueManager] Reputation change: {_currentNpcData.faction} +{node.reputationGain}");
                // TODO: Add to faction reputation system
            }

            // Trigger event if specified
            if (!string.IsNullOrEmpty(node.triggerEvent))
            {
                Debug.Log($"[NpcDialogueManager] Trigger event: {node.triggerEvent}");
                // TODO: Trigger UnityEvent or send notification
            }

            // Handle special node types
            switch (node.nodeType)
            {
                case DialogueNodeType.QuestOffer:
                    Debug.Log($"[NpcDialogueManager] Quest offer node - contract: {node.contractId}");
                    break;

                case DialogueNodeType.QuestComplete:
                    Debug.Log($"[NpcDialogueManager] Quest complete node - contract: {node.contractId}");
                    break;

                case DialogueNodeType.Trade:
                    Debug.Log($"[NpcDialogueManager] Trade node - opening trade UI");
                    // TODO: Open trade interface
                    break;

                case DialogueNodeType.Service:
                    Debug.Log($"[NpcDialogueManager] Service node");
                    // TODO: Open service UI (repair, refuel, etc.)
                    break;

                case DialogueNodeType.Goodbye:
                    // Will end dialogue after showing text
                    break;
            }
        }

        private void ProcessOptionEffects(DialogueOption option)
        {
            // Give reward item
            if (!string.IsNullOrEmpty(option.rewardItemId))
            {
                Debug.Log($"[NpcDialogueManager] Giving reward: {option.rewardItemId}");
                // TODO: Add to player inventory
            }

            // Play sound
            if (!string.IsNullOrEmpty(option.soundCue))
            {
                Debug.Log($"[NpcDialogueManager] Play sound: {option.soundCue}");
                // TODO: Play audio cue
            }
        }

        private void EndDialogue()
        {
            if (debugMode)
            {
                Debug.Log($"[NpcDialogueManager] Ending dialogue");
            }

            _isDialogueActive = false;
            _currentNpcData = null;
            _currentNpcEntity = null;
            _currentNode = null;

            HideDialoguePanel();
            ClearOptions();

            // Resume game
            // Time.timeScale = 1f; // Uncomment to resume

            // Notify NPC entity
            _currentNpcEntity?.EndDialogue();

            // Fire event
            OnDialogueEnded?.Invoke();
        }

        #endregion

        #region Debug/Gizmos
        
        private void OnDrawGizmosSelected()
        {
            // Could draw debug info for current dialogue state
        }

        #endregion
    }
}