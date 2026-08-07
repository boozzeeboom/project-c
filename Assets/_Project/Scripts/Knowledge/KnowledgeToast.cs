// Project C: Knowledge System V3
// KnowledgeToast: UI toast «Открыто знание» при получении новых знаний.
// Подписывается на 4 события клиентских стейтов и показывает diff.
// Design: docs/Character/Knowledges/07_KNOWLEDGE_SYSTEM_V3_INTEGRATION_PLAN.md §4 V3.8
// V3.9: UI Toolkit toast (pattern: QuestToast) — runtime-built VisualElement, queue, bottom-center.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Skills;
using ProjectC.Crafting;
using ProjectC.Reputation;
using ProjectC.Quests.Dto;
using ProjectC.Factions;
using ProjectC.Localization;

namespace ProjectC.Knowledge
{
    [RequireComponent(typeof(UIDocument))]
    public class KnowledgeToast : MonoBehaviour
    {
        public static KnowledgeToast Instance { get; private set; }

        [Header("Toast Settings")]
        [SerializeField] private float _toastDuration = 3f;
        [SerializeField] private float _queueDelay = 0.8f;
        [SerializeField] private int _maxToastLines = 3;

        // Previous state for diff
        private HashSet<string> _prevKnownSkills = new HashSet<string>();
        private HashSet<string> _prevKnownRecipes = new HashSet<string>();
        private HashSet<byte> _prevKnownFactions = new HashSet<byte>();
        private HashSet<string> _prevKnownNpcs = new HashSet<string>();

        // UI Toolkit
        private UIDocument _doc;
        private VisualElement _container;
        private Label _label;
        private bool _built;
        private bool _subscribed;
        private Queue<string> _queue = new Queue<string>();
        private Coroutine _queueCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _doc = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDisable()
        {
            TryUnsubscribe();
            if (_queueCoroutine != null) { StopCoroutine(_queueCoroutine); _queueCoroutine = null; }
        }

        private void OnDestroy()
        {
            TryUnsubscribe();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_built) TryBuild();
            if (!_subscribed) TrySubscribe();
        }

        private void TryBuild()
        {
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null) return;
            if (_doc.rootVisualElement == null) return;
            if (_doc.panelSettings == null) return;

            var root = _doc.rootVisualElement;

            _container = new VisualElement
            {
                name = "knowledge-toast",
                pickingMode = PickingMode.Ignore
            };
            _container.style.position = Position.Absolute;
            _container.style.bottom = 48;
            _container.style.left = 0;
            _container.style.right = 0;
            _container.style.alignItems = Align.Center;
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            _label = new Label
            {
                name = "knowledge-toast-label",
                text = "",
                pickingMode = PickingMode.Ignore
            };
            _label.style.color = new StyleColor(new Color(0.9f, 0.85f, 1f, 1f));
            _label.style.fontSize = 17;
            _label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.whiteSpace = WhiteSpace.Normal;
            _label.style.backgroundColor = new StyleColor(new Color(0.08f, 0.06f, 0.15f, 0.88f));
            _label.style.paddingTop = 8;
            _label.style.paddingBottom = 8;
            _label.style.paddingLeft = 20;
            _label.style.paddingRight = 20;
            _label.style.borderTopLeftRadius = 6;
            _label.style.borderTopRightRadius = 6;
            _label.style.borderBottomLeftRadius = 6;
            _label.style.borderBottomRightRadius = 6;
            _label.style.textShadow = new TextShadow
            {
                offset = new Vector2(1, 1),
                blurRadius = 2,
                color = new Color(0, 0, 0, 0.9f)
            };

            _container.Add(_label);
            root.Add(_container);
            _built = true;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;

            var skillsState = SkillsClientState.Instance;
            if (skillsState != null)
            {
                skillsState.OnSkillsUpdated += OnSkillsUpdated;
                _prevKnownSkills = new HashSet<string>(skillsState.KnownSkillIds ?? new HashSet<string>());
            }

            var recipeState = RecipeKnowledgeClientState.Instance;
            if (recipeState != null)
            {
                recipeState.OnRecipeKnowledgeUpdated += OnRecipesUpdated;
                _prevKnownRecipes = new HashSet<string>(recipeState.KnownRecipeIds ?? new HashSet<string>());
            }

            var repState = ReputationClientState.Instance;
            if (repState != null)
            {
                repState.OnReputationUpdated += OnReputationUpdated;
                _prevKnownFactions = new HashSet<byte>(repState.KnownFactionIds ?? new HashSet<byte>());
            }

            var npcState = NpcAttitudeClientState.Instance;
            if (npcState != null)
            {
                npcState.OnNpcAttitudeUpdated += OnNpcAttitudeUpdated;
                _prevKnownNpcs = new HashSet<string>(npcState.KnownNpcIds ?? new HashSet<string>());
            }

            _subscribed = true;
        }

        private void TryUnsubscribe()
        {
            if (!_subscribed) return;
            if (SkillsClientState.Instance != null) SkillsClientState.Instance.OnSkillsUpdated -= OnSkillsUpdated;
            if (RecipeKnowledgeClientState.Instance != null) RecipeKnowledgeClientState.Instance.OnRecipeKnowledgeUpdated -= OnRecipesUpdated;
            if (ReputationClientState.Instance != null) ReputationClientState.Instance.OnReputationUpdated -= OnReputationUpdated;
            if (NpcAttitudeClientState.Instance != null) NpcAttitudeClientState.Instance.OnNpcAttitudeUpdated -= OnNpcAttitudeUpdated;
            _subscribed = false;
        }

        private void OnSkillsUpdated(HashSet<string> learnedSkills)
        {
            var state = SkillsClientState.Instance;
            if (state == null) return;
            var current = state.KnownSkillIds ?? new HashSet<string>();
            DiffAndToast(Loc.Get("ui.knowledge.category_skill", "Skill"), _prevKnownSkills, current, GetSkillDisplayName);
            _prevKnownSkills = new HashSet<string>(current);
        }

        private void OnRecipesUpdated(HashSet<string> knownRecipes)
        {
            var current = knownRecipes ?? new HashSet<string>();
            DiffAndToast(Loc.Get("ui.knowledge.category_recipe", "Recipe"), _prevKnownRecipes, current, GetRecipeDisplayName);
            _prevKnownRecipes = new HashSet<string>(current);
        }

        private void OnReputationUpdated(ReputationSnapshotDto dto)
        {
            var state = ReputationClientState.Instance;
            if (state == null) return;
            var current = state.KnownFactionIds ?? new HashSet<byte>();
            DiffAndToastByte(Loc.Get("ui.knowledge.category_faction", "Faction"), _prevKnownFactions, current, GetFactionDisplayName);
            _prevKnownFactions = new HashSet<byte>(current);
        }

        private void OnNpcAttitudeUpdated(NpcAttitudeSnapshotDto dto)
        {
            var state = NpcAttitudeClientState.Instance;
            if (state == null) return;
            var current = state.KnownNpcIds ?? new HashSet<string>();
            DiffAndToast("NPC", _prevKnownNpcs, current, GetNpcDisplayName);
            _prevKnownNpcs = new HashSet<string>(current);
        }

        private void DiffAndToast(string category, HashSet<string> prev, HashSet<string> current,
            System.Func<string, string> getName)
        {
            var newItems = new List<string>();
            foreach (var id in current)
            {
                if (!prev.Contains(id))
                    newItems.Add(getName(id));
            }
            if (newItems.Count > 0) ShowToast(category, newItems);
        }

        private void DiffAndToastByte(string category, HashSet<byte> prev, HashSet<byte> current,
            System.Func<byte, string> getName)
        {
            var newItems = new List<string>();
            foreach (var id in current)
            {
                if (!prev.Contains(id))
                    newItems.Add(getName(id));
            }
            if (newItems.Count > 0) ShowToast(category, newItems);
        }

        private void ShowToast(string category, List<string> names)
        {
            int count = Mathf.Min(names.Count, _maxToastLines);
            string displayNames = string.Join(", ", names.GetRange(0, count));
            if (names.Count > _maxToastLines)
                displayNames += " " + Loc.Get("ui.knowledge.toast_and_more", "и ещё {0}").Replace("{0}", (names.Count - _maxToastLines).ToString());

            string template = Loc.Get("ui.knowledge.toast_format", "📖 Открыто знание — {0}: {1}");
            string message = string.Format(template, category, displayNames);
            Debug.Log($"[KnowledgeToast] {message}");
            EnqueueToast(message);
        }

        private void EnqueueToast(string message)
        {
            if (!_built) TryBuild();
            if (_container == null || _label == null) return;
            _queue.Enqueue(message);
            if (_queueCoroutine == null) _queueCoroutine = StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            while (_queue.Count > 0)
            {
                var msg = _queue.Dequeue();
                _label.text = msg;
                _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                yield return new WaitForSecondsRealtime(_toastDuration);
                if (_queue.Count == 0)
                {
                    _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                    yield return new WaitForSecondsRealtime(_queueDelay);
                }
            }
            _queueCoroutine = null;
        }

        private string GetSkillDisplayName(string skillId)
        {
            var state = SkillsClientState.Instance;
            if (state != null && state.TryGetSkillConfig(skillId, out var cfg))
                return !string.IsNullOrEmpty(cfg.displayName) ? cfg.displayName : skillId;
            return skillId;
        }

        private string GetRecipeDisplayName(string recipeId)
        {
            var recipe = RecipeClientRegistry.GetRecipe(recipeId);
            return recipe != null ? recipe.DisplayName : recipeId;
        }

        private string GetFactionDisplayName(byte factionIdByte)
        {
            var factionId = (FactionId)factionIdByte;
            var catalog = FactionCatalog.Instance;
            if (catalog != null) return catalog.GetDisplayName(factionId);
            return factionId.ToString();
        }

        private string GetNpcDisplayName(string npcId) => npcId;
    }
}
