using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Screens.Ships
{
    [RequireComponent(typeof(UIDocument))]
    public class ShipsScreen : MonoBehaviour
    {
        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset shipsUxml;
        [SerializeField] private StyleSheet shipsUss;
        [SerializeField] private StyleSheet designTokensUss;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _list;
        private Label _status;
        private ShipsViewModel _vm;
        private bool _built = false;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _vm = new ShipsViewModel();
            if (shipsUxml == null)
                shipsUxml = Resources.Load<VisualTreeAsset>("UIv2/Screens/Ships/Ships");
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
            if (shipsUxml == null) { Debug.LogError("[ShipsScreen] UXML not assigned"); return; }

            _root = _doc.rootVisualElement;
            if (designTokensUss != null && !_root.styleSheets.Contains(designTokensUss))
                _root.styleSheets.Add(designTokensUss);
            if (shipsUss != null && !_root.styleSheets.Contains(shipsUss))
                _root.styleSheets.Add(shipsUss);

            _list = _root.Q<VisualElement>("list");
            _status = _root.Q<Label>("status");

            if (_list != null)
            {
                string[] names = { "Aurora", "Boreas", "Cetus", "Draco", "Eos" };
                string[] classes = { "Light", "Medium", "Heavy", "Scout", "Cargo" };
                for (int i = 0; i < 5; i++)
                {
                    var item = new VisualElement();
                    item.AddToClassList("ships-item");
                    var nameLabel = new Label(names[i]);
                    nameLabel.AddToClassList("ships-item-name");
                    var classLabel = new Label(classes[i]);
                    classLabel.AddToClassList("ships-item-class");
                    item.Add(nameLabel);
                    item.Add(classLabel);
                    _list.Add(item);
                }
            }

            if (_status != null)
                _vm.OnStatusChanged += s => _status.text = "Status: " + s;

            _root.style.display = DisplayStyle.Flex;
            _built = true;
            Debug.Log($"[ShipsScreen] Built. styleSheets={_root.styleSheets.count}, ships=5");
            StartCoroutine(ProjectC.UIv2.Core.Animation.UIAnimator.FadeIn(_root, 0.25f));
        }
    }
}