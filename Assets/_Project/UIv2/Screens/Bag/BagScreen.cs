using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Screens.Bag
{
    [RequireComponent(typeof(UIDocument))]
    public class BagScreen : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset bagUxml;
        [SerializeField] private StyleSheet bagUss;
        [SerializeField] private StyleSheet designTokensUss;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _grid;
        private Label _status;
        private BagViewModel _vm;
        private bool _built = false;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _vm = new BagViewModel();
            if (bagUxml == null)
                bagUxml = Resources.Load<VisualTreeAsset>("UIv2/Screens/Bag/Bag");
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
            if (bagUxml == null) { Debug.LogError("[BagScreen] UXML not assigned"); return; }

            _root = _doc.rootVisualElement;
            if (designTokensUss != null && !_root.styleSheets.Contains(designTokensUss))
                _root.styleSheets.Add(designTokensUss);
            if (bagUss != null && !_root.styleSheets.Contains(bagUss))
                _root.styleSheets.Add(bagUss);

            _grid = _root.Q<VisualElement>("grid");
            _status = _root.Q<Label>("status");

            // Generate slots
            if (_grid != null)
            {
                for (int i = 0; i < 24; i++)
                {
                    var slot = new VisualElement();
                    slot.AddToClassList("bag-slot");
                    if (i < 8) slot.AddToClassList("bag-slot--filled");
                    var icon = new VisualElement();
                    icon.AddToClassList("bag-slot-icon");
                    slot.Add(icon);
                    _grid.Add(slot);
                }
            }

            if (_status != null)
                _vm.OnStatusChanged += s => _status.text = "Status: " + s;

            _root.style.display = DisplayStyle.Flex;
            _built = true;
            Debug.Log($"[BagScreen] Built. styleSheets={_root.styleSheets.count}, slots=24");
            StartCoroutine(ProjectC.UIv2.Core.Animation.UIAnimator.FadeIn(_root, 0.25f));
        }
    }
}
