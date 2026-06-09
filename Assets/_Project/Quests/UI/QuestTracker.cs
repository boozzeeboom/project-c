// T-Q12: QuestTracker — singleton HUD overlay (top-right corner).
// Показывает отслеживаемый квест (имя + текущая цель + кнопка "Скрыть").
// MVP Variant C: локальный state `_trackedQuestId` на клиенте (не серверный).
// T-Q15+ мигрирует на server-side через QuestServer.RequestTrackQuestRpc.
//
// Pattern: как DialogWindow / MarketWindow — UIDocument singleton, scene-placed
// в BootstrapScene [QuestTracker] GameObject, DontDestroyOnLoad.
//
// Документация: docs/dev/T-Q12_DESIGN_NOTE.md.

using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Quests.Client;
using ProjectC.Quests.Dto;

namespace ProjectC.Quests.UI
{
 [RequireComponent(typeof(UIDocument))]
 public class QuestTracker : MonoBehaviour
 {
 public static QuestTracker Instance { get; private set; }

 [Header("UI Assets (можно Resources fallback)")]
 [SerializeField] private VisualTreeAsset questTrackerUxml;
 [SerializeField] private StyleSheet questTrackerUss;
 [SerializeField, Tooltip("DontDestroyOnLoad для переживания scene loads.")]
 private bool dontDestroyOnLoad = true;

 private UIDocument _doc;
 private VisualElement _root;
 private VisualElement _panel;
 private Label _nameLabel;
 private Label _objectiveLabel;
 private Button _hideBtn;

 private bool _built;
 private bool _subscribed;

 // MVP local tracking state (Variant C).
 private string _trackedQuestId;

 public string TrackedQuestId => _trackedQuestId;
 public bool HasTrackedQuest => !string.IsNullOrEmpty(_trackedQuestId);

 // T-Q21 fix: event чтобы CharacterWindow обновил кнопки "Следить"/"Не следить" при изменении tracking state.
 public event System.Action OnTrackChanged;

 // T-Q21: lazy Instance init — fallback для случая когда [QuestTracker] есть в сцене,
 // но Awake не отработал к моменту UI click.
 private static QuestTracker _cachedInstance;
 public static QuestTracker GetOrFindInstance()
 {
     // T-Q21 fix: не вызываем GameObject.Find во время shutdown (Assertion failed on go.IsActive()).
     if (Instance != null) return Instance;
     if (_cachedInstance != null) return _cachedInstance;
     // Application.isPlaying == false означает edit mode или shutdown — Find небезопасен.
     if (!Application.isPlaying) return null;
     try {
         var go = GameObject.Find("[QuestTracker]");
         if (go != null) {
             _cachedInstance = go.GetComponent<QuestTracker>();
             if (_cachedInstance != null) return _cachedInstance;
         }
     } catch (System.Exception) {
         return null;
     }
     return null;
 }

 private void Awake()
 {
 if (Instance == null) Instance = this;
 else if (Instance != this) { Destroy(gameObject); return; }

 _doc = GetComponent<UIDocument>();
 if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

 if (questTrackerUxml == null)
 questTrackerUxml = Resources.Load<VisualTreeAsset>("UI/QuestTracker");
 if (questTrackerUss == null)
 questTrackerUss = Resources.Load<StyleSheet>("UI/QuestTracker");

 if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
 }

 private void OnEnable()
 {
 EnsureBuilt();
 TrySubscribe();
 }

 // T-Q12-fix: UIDocument.OnEnable может сработать ПОСЛЕ QuestTracker.OnEnable. Start() гарантирует
 // второй проход EnsureBuilt ПОСЛЕ всех OnEnable.
 private void Start()
 {
 EnsureBuilt();
 }

 private void OnDisable()
 {
 TryUnsubscribe();
 }

 private void OnDestroy()
 {
 TryUnsubscribe();
 if (Instance == this) Instance = null;
 if (_cachedInstance == this) _cachedInstance = null;  // T-Q21 fix: clear cache.
 }

 private void Update()
 {
 // Lazy-subscribe (race с NMC.Awake и QuestClientState.AutoSpawn).
 if (!_subscribed) TrySubscribe();
 }

 private void EnsureBuilt()
 {
 if (_doc == null) _doc = GetComponent<UIDocument>();
 if (_doc == null)
 {
 Debug.LogError("[QuestTracker] нет UIDocument на GameObject");
 return;
 }
 if (_doc.rootVisualElement == null) return;

 if (questTrackerUxml == null)
 questTrackerUxml = Resources.Load<VisualTreeAsset>("UI/QuestTracker");
 if (questTrackerUss == null)
 questTrackerUss = Resources.Load<StyleSheet>("UI/QuestTracker");
 if (questTrackerUxml == null)
 {
 Debug.LogError("[QuestTracker] UXML не найден ни в Inspector, ни в Resources/UI/");
 return;
 }

 // КРИТИЧНО (T-Q11c fix): clear + styleSheets.Add + CloneTree.
 _doc.rootVisualElement.Clear();
 if (questTrackerUss != null)
 _doc.rootVisualElement.styleSheets.Add(questTrackerUss);

 _root = questTrackerUxml.CloneTree();
 _root.style.position = Position.Absolute;
 _root.style.left =0;
 _root.style.top =0;
 _root.style.right =0;
 _root.style.bottom =0;
 _root.pickingMode = PickingMode.Ignore;
 _doc.rootVisualElement.Add(_root);

 _panel = _root.Q<VisualElement>("panel");
 _nameLabel = _root.Q<Label>("quest-name");
 _objectiveLabel = _root.Q<Label>("quest-objective");
 _hideBtn = _root.Q<Button>("hide-btn");

 if (_hideBtn != null) _hideBtn.clicked += OnHideClicked;

 // Initially hidden (no tracked quest).
 if (_root != null) _root.style.display = DisplayStyle.None;

 _built = true;
 if (Debug.isDebugBuild)
 Debug.Log($"[QuestTracker] Built: rootVE.children={_doc.rootVisualElement.childCount}");
 }

 private void OnHideClicked()
 {
 Untrack();
 }

 // ============================================================
 // Public API (UI → QuestTracker)
 // ============================================================

 public void Track(string questId)
 {
 if (string.IsNullOrEmpty(questId))
 {
 Debug.LogWarning("[QuestTracker] Track: questId is empty");
 return;
 }
 _trackedQuestId = questId;
 Debug.Log($"[QuestTracker] Track: questId={questId} _built={_built}");
 OnTrackChanged?.Invoke();
 RefreshDisplay();
 }

 public void Untrack()
 {
 if (string.IsNullOrEmpty(_trackedQuestId))
 {
 RefreshDisplay(); // и так ничего не показываем
 return;
 }
 if (Debug.isDebugBuild) Debug.Log($"[QuestTracker] Untrack: questId={_trackedQuestId}");
 _trackedQuestId = null;
 OnTrackChanged?.Invoke();
 RefreshDisplay();
 }

 public void Toggle(string questId)
 {
 if (_trackedQuestId == questId) Untrack();
 else Track(questId);
 }

 // ============================================================
 // Subscribe + refresh
 // ============================================================

 private void TrySubscribe()
 {
 if (_subscribed) return;
 var qs = QuestClientState.Instance;
 if (qs == null) return;
 qs.OnSnapshotUpdated += HandleQuestSnapshotUpdated;
 _subscribed = true;
 if (Debug.isDebugBuild) Debug.Log("[QuestTracker] Subscribed to QuestClientState.OnSnapshotUpdated");
 }

 private void TryUnsubscribe()
 {
 if (!_subscribed) return;
 var qs = QuestClientState.Instance;
 if (qs == null) { _subscribed = false; return; }
 qs.OnSnapshotUpdated -= HandleQuestSnapshotUpdated;
 _subscribed = false;
 }

 // R3-005 cross-tab pattern: cache ALREADY updated в QuestClientState; UI refresh gated on `_built`.
 private void HandleQuestSnapshotUpdated(QuestSnapshotDto snap)
 {
 // Обновляем отображение (quest мог быть Completed → перестать показывать).
 RefreshDisplay();
 }

 private void RefreshDisplay()
 {
 Debug.Log($"[QuestTracker] RefreshDisplay: _built={_built} _root={(_root!=null?_root.name:"null")} _trackedQuestId={_trackedQuestId}");
 if (!_built || _root == null) return;
 if (string.IsNullOrEmpty(_trackedQuestId))
 {
 _root.style.display = DisplayStyle.None;
 return;
 }

 var qs = QuestClientState.Instance;
 if (qs == null || !qs.CurrentSnapshot.HasValue)
 {
 // Нет snapshot — скрываем (quest state неизвестен).
 _root.style.display = DisplayStyle.None;
 return;
 }

 var quests = qs.CurrentSnapshot.Value.quests;
 QuestProgressDto tracked = default;
 bool found = false;
 if (quests != null)
 {
 foreach (var q in quests)
 {
 if (q.questId == _trackedQuestId)
 {
 tracked = q;
 found = true;
 break;
 }
 }
 }

 if (!found)
 {
 // Quest удалён или недоступен — untrack.
 if (Debug.isDebugBuild) Debug.Log($"[QuestTracker] RefreshDisplay: tracked quest '{_trackedQuestId}' not in snapshot — auto-untrack");
 _trackedQuestId = null;
 _root.style.display = DisplayStyle.None;
 return;
 }

 // Показываем panel с данными.
 _root.style.display = DisplayStyle.Flex;
 if (_nameLabel != null)
 _nameLabel.text = !string.IsNullOrEmpty(tracked.displayName) ? tracked.displayName : tracked.questId;

 // Текущая цель = первая не-completed objective (MVP).
 if (_objectiveLabel != null)
 {
 string objText = BuildObjectiveText(tracked);
 _objectiveLabel.text = objText;
 }
 }

 private static string BuildObjectiveText(QuestProgressDto q)
 {
 var objs = q.objectives;
 if (objs == null || objs.Length ==0) return "Цель: (нет целей)";
 int completed =0;
 foreach (var o in objs) if (o.completed) completed++;
 // T-Q21 fix: первая не-completed objective — показываем current/required для HUD counter.
 foreach (var o in objs)
 {
 if (!o.completed && !string.IsNullOrEmpty(o.description))
 {
 int req = o.requiredQuantity >0 ? o.requiredQuantity :1;
 // Показываем counter только если requiredQuantity > 1 (для 1-цели counter избыточен).
 if (req >1) return $"Цель: {o.description} ({o.currentValue}/{req})";
 return $"Цель: {o.description}";
 }
 }
 // Все completed — выводим общий счётчик.
 return $"Цель: ({completed}/{objs.Length}) выполнено";
 }
 }
}
