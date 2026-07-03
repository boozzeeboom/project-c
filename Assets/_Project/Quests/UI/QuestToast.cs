// =====================================================================================
// QuestToast.cs — T-Q23: UI-компонент тоста для quest events.
// =====================================================================================
// По аналогии с ShipKeyToast (DEPRECATED, но полезен как reference pattern):
//   - Runtime-constructed VisualElement (без UXML/USS files)
//   - Bottom-center positioned
//   - Singleton subscribed to QuestClientState events
//   - 2.5 sec display, 0.3 sec cooldown
// =====================================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.Quests.Client;
using ProjectC.Quests.Dto;

namespace ProjectC.Quests.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class QuestToast : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Длительность показа тоста в секундах.")]
        [SerializeField] private float _duration = 2.5f;

        [Tooltip("T-Q25 fix: delay между последовательными toast'ами в queue (несколько rewards подряд).")]
        [SerializeField] private float _queueDelay = 1.2f;

        private UIDocument _doc;
        private VisualElement _container;
        private Label _label;
        private Coroutine _hideCoroutine;
        private Coroutine _queueCoroutine;
        private System.Collections.Generic.Queue<string> _queue = new System.Collections.Generic.Queue<string>();
        private float _lastShowTime = -10f;
        private bool _built = false;
        private bool _subscribed = false;

        private void Awake()
        {
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
            Unsubscribe();
            if (_hideCoroutine != null) { StopCoroutine(_hideCoroutine); _hideCoroutine = null; }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            var qs = QuestClientState.Instance;
            if (qs != null)
            {
                qs.OnDialogActionResultReceived -= HandleDialogActionResult;
                qs.OnQuestResult -= HandleQuestResult;
                qs.OnQuestDiscovered -= HandleQuestDiscovered;
            }
            _subscribed = false;
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
                name = "quest-toast",
                pickingMode = PickingMode.Ignore
            };
            _container.style.position = Position.Absolute;
            _container.style.bottom = 80;  // higher than ShipKeyToast (48) to not overlap
            _container.style.left = 0;
            _container.style.right = 0;
            _container.style.alignItems = Align.Center;
            _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            _label = new Label
            {
                name = "quest-toast-label",
                text = "",
                pickingMode = PickingMode.Ignore
            };
            _label.style.color = new StyleColor(new Color(0.85f, 0.95f, 1f, 1f));
            _label.style.fontSize = 18;
            _label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.whiteSpace = WhiteSpace.Normal;
            _label.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.1f, 0.88f));
            _label.style.paddingTop = 10;
            _label.style.paddingBottom = 10;
            _label.style.paddingLeft = 24;
            _label.style.paddingRight = 24;
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
            var qs = QuestClientState.Instance;
            if (qs == null) return;
            qs.OnDialogActionResultReceived += HandleDialogActionResult;
            qs.OnQuestResult += HandleQuestResult;
            qs.OnQuestDiscovered += HandleQuestDiscovered;
            _subscribed = true;
        }

        private void HandleQuestDiscovered(string questId, string displayName)
        {
            string name = string.IsNullOrEmpty(displayName) ? questId : displayName;
            ShowToast($"✨ Найден квест: {name}");
        }

        private void HandleQuestResult(QuestResultDto result)
        {
            // QuestResultDto.code: 0=Ok. Show "Accepted" / "Turned in" / "Already..." messages.
            if (result.code != 0) return;
            if (string.IsNullOrEmpty(result.message)) return;
            // T-Q24: lookup displayName from QuestDatabase for nice text.
            string displayName = LookupQuestDisplayName(result.questId);
            string icon = "📜";
            if (result.message.StartsWith("Turned")) icon = "✅";
            else if (result.message.StartsWith("Already")) icon = "ℹ";
            string text = $"{icon} {result.message}{(string.IsNullOrEmpty(displayName) ? "" : ": " + displayName)}";
            ShowToast(text);
        }

        private static string LookupQuestDisplayName(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return "";
            var qs = QuestClientState.Instance;
            if (qs == null) return questId;
            var snap = qs.CurrentSnapshot;
            if (snap == null || !snap.HasValue || snap.Value.quests == null) return questId;
            var arr = snap.Value.quests;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].questId == questId)
                {
                    return string.IsNullOrEmpty(arr[i].displayName) ? questId : arr[i].displayName;
                }
            }
            return questId;
        }

        private void HandleDialogActionResult(DialogActionResultDto result)
        {
            // T-Q28 fix: при success=false И type=OfferQuest/AcceptQuest — показать toast с reason.
            // Раньше игрок кликал 🔒-кнопку → диалог закрывался без feedback, юзер не понимал почему.
            // result.resultData содержит server-side message (ru), e.g. "Сначала выполните квест «q_002_0»".
            if (!result.success)
            {
                if (result.actionType == (byte)ProjectC.Dialogue.DialogueActionType.OfferQuest
                    || result.actionType == (byte)ProjectC.Dialogue.DialogueActionType.AcceptQuest)
                {
                    string reason = string.IsNullOrEmpty(result.resultData) ? "Не удалось взять квест" : result.resultData;
                    ShowToast($"🔒 {reason}");
                }
                return;
            }

            // result.actionType: 20=GiveItem, 21=TakeItem, 22=GiveCredits, 23=AddReputation,
            //                   24=AddNpcAttitude, 60=CompleteObjective
            string msg = FormatActionMessage(result);
            if (!string.IsNullOrEmpty(msg))
            {
                ShowToast(msg);
            }
        }

        private string FormatActionMessage(DialogActionResultDto r)
        {
            string data = r.resultData ?? "";
            // T-Q25: intParam приоритетнее (delta value), fallback на resultData.
            int delta = r.intParam;
            // Action type IDs: GiveCredits=30, AddReputation=31, AddNpcAttitude=32, CompleteObjective=11
            switch (r.actionType)
            {
                case 20: return string.IsNullOrEmpty(data) ? "📦 +1 предмет" : $"📦 +1 {data}";
                case 21: return string.IsNullOrEmpty(data) ? "📦 -1 предмет" : $"📦 -1 {data}";
                case 30: return $"💰 +{delta} CR";
                case 31:
                    // resultData: "{faction}:{newValue}" — e.g. "GuildOfThoughts:75".
                    if (string.IsNullOrEmpty(data)) return $"📈 Репутация +{delta}";
                    var fparts = data.Split(':');
                    if (fparts.Length == 2) return $"📈 {fparts[0]} +{delta}";
                    return $"📈 {data}";
                case 32:
                    // resultData: "{npcId}:{newAttitude}" — e.g. "mira_01:5".
                    if (string.IsNullOrEmpty(data)) return $"💚 Отношение +{delta}";
                    var parts = data.Split(':');
                    if (parts.Length == 2) return $"💚 {parts[0]} +{delta}";
                    return $"💚 {data}";
                case 11: return string.IsNullOrEmpty(data) ? "✅ Цель выполнена" : $"✅ {data}";
                default: return data;
            }
        }

        private void ShowToast(string message)
        {
            if (!_built) TryBuild();
            if (_container == null || _label == null) return;
            // T-Q25 fix: queue-based. Все toast'ы показываются по очереди.
            // Cooldown убран — он дропал reward'ы. Дубли в одном frame крайне редки.
            _queue.Enqueue(message);
            if (_queueCoroutine == null) _queueCoroutine = StartCoroutine(ProcessQueue());
        }

        private System.Collections.IEnumerator ProcessQueue()
        {
            while (_queue.Count > 0)
            {
                var msg = _queue.Dequeue();
                _label.text = msg;
                _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                _lastShowTime = Time.unscaledTime;
                yield return new WaitForSecondsRealtime(_duration);
                if (_queue.Count == 0)
                {
                    _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                    yield return new WaitForSecondsRealtime(_queueDelay);
                }
            }
            _queueCoroutine = null;
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_container != null)
            {
                _container.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            }
            _hideCoroutine = null;
        }
    }
}
