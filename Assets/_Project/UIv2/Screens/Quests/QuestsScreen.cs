using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Screens.Quests
{
    [RequireComponent(typeof(UIDocument))]
    public class QuestsScreen : MonoBehaviour
    {
        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset questsUxml;
        [SerializeField] private StyleSheet questsUss;
        [SerializeField] private StyleSheet designTokensUss;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _list;
        private Label _status;
        private QuestsViewModel _vm;
        private bool _built = false;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _vm = new QuestsViewModel();
            if (questsUxml == null)
                questsUxml = Resources.Load<VisualTreeAsset>("UIv2/Screens/Quests/Quests");
        }

        private void OnEnable() => StartCoroutine(BuildWhenReady());
        private void OnDisable() { }

        private IEnumerator BuildWhenReady()
        {
            while (_doc == null || _doc.rootVisualElement == null)
            {
                if (_doc == null) _doc = GetComponent<UIDocument>();
                yield return null;
            }
            EnsureBuilt();
        }

        public void EnsureBuilt()
        {
            if (_built) return;
            if (_doc == null || _doc.rootVisualElement == null) return;
            if (questsUxml == null) { Debug.LogError("[QuestsScreen] UXML not assigned"); return; }

            _root = _doc.rootVisualElement;
            if (designTokensUss != null && !_root.styleSheets.Contains(designTokensUss))
                _root.styleSheets.Add(designTokensUss);
            if (questsUss != null && !_root.styleSheets.Contains(questsUss))
                _root.styleSheets.Add(questsUss);

            _list = _root.Q<VisualElement>("list");
            _status = _root.Q<Label>("status");

            if (_list != null)
            {
                string[] names = { "Пепел под стеклом", "Пропавший смотритель", "Чертёж антиграва", "Свидетель нижних ярусов", "Манифест Нового Правительства", "Тишина на Верхнем причале" };
                string[] states = { "Active", "Offered", "Completed", "Discovered", "Active", "Failed" };
                for (int i = 0; i < 6; i++)
                {
                    var item = new VisualElement();
                    item.AddToClassList("quests-item");
                    var nameLabel = new Label(names[i]);
                    nameLabel.AddToClassList("quests-item-name");
                    var stateLabel = new Label(states[i]);
                    stateLabel.AddToClassList("quests-item-state");
                    item.Add(nameLabel);
                    item.Add(stateLabel);
                    _list.Add(item);
                }
            }

            if (_status != null)
                _vm.OnStatusChanged += s => _status.text = "Status: " + s;

            _root.style.display = DisplayStyle.Flex;
            _built = true;
            Debug.Log($"[QuestsScreen] Built. styleSheets={_root.styleSheets.count}, quests=6");
            StartCoroutine(ProjectC.UIv2.Core.Animation.UIAnimator.FadeIn(_root, 0.25f));
        }
    }
}