using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectC.UIv2.Core.Animation;

namespace ProjectC.UIv2.Screens.Home
{
    [RequireComponent(typeof(UIDocument))]
    public class HomeScreen : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset homeUxml;
        [SerializeField] private StyleSheet homeUss;
        [SerializeField] private StyleSheet designTokensUss;

        private UIDocument _doc;
        private VisualElement _root;
        private Label _status;
        private HomeViewModel _vm;
        private bool _built = false;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _vm = new HomeViewModel();
            if (homeUxml == null)
                homeUxml = Resources.Load<VisualTreeAsset>("UIv2/Screens/Home/Home");
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
            if (homeUxml == null) { Debug.LogError("[HomeScreen] UXML not assigned"); return; }

            _root = _doc.rootVisualElement;
            // Add DesignTokens first (variables), then Home USS
            if (designTokensUss != null && !_root.styleSheets.Contains(designTokensUss))
                _root.styleSheets.Add(designTokensUss);
            if (homeUss != null && !_root.styleSheets.Contains(homeUss))
                _root.styleSheets.Add(homeUss);

            _status = _root.Q<Label>("status");
            if (_status != null)
                _vm.OnStatusChanged += s => _status.text = "Status: " + s;

            var btnMap = new System.Collections.Generic.Dictionary<string, System.Action>
            {
                { "btn-open-map", () => _vm.SetStatus("Map opened") },
            };
            foreach (var kv in btnMap)
            {
                var btn = _root.Q<Button>(kv.Key);
                if (btn != null) { btn.clicked -= kv.Value; btn.clicked += kv.Value; }
            }

            _root.style.display = DisplayStyle.Flex;
            _built = true;
            Debug.Log($"[HomeScreen] Built. styleSheets={_root.styleSheets.count}");

            // Start fade-in animation
            StartCoroutine(ProjectC.UIv2.Core.Animation.UIAnimator.FadeIn(_root, 0.25f));

            // Hover scale on buttons
            foreach (var btnName in new[] { "btn-open-map", "btn-cargo", "btn-skills", "btn-quests" })
            {
                var btn = _root.Q<Button>(btnName);
                if (btn != null)
                    btn.RegisterCallback<PointerEnterEvent>(e => StartCoroutine(ProjectC.UIv2.Core.Animation.UIAnimator.Pulse(btn, 1.03f, 0.2f)));
            }
        }
    }
}
