using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Screens.Calendar
{
    [RequireComponent(typeof(UIDocument))]
    public class CalendarScreen : MonoBehaviour
    {
        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset calendarUxml;
        [SerializeField] private StyleSheet calendarUss;
        [SerializeField] private StyleSheet designTokensUss;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _grid;
        private Label _status;
        private CalendarViewModel _vm;
        private bool _built = false;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _vm = new CalendarViewModel();
            if (calendarUxml == null)
                calendarUxml = Resources.Load<VisualTreeAsset>("UIv2/Screens/Calendar/Calendar");
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
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null || _doc.rootVisualElement == null) return;
            if (calendarUxml == null) { Debug.LogError("[CalendarScreen] UXML not assigned"); return; }

            _root = _doc.rootVisualElement;
            if (designTokensUss != null && !_root.styleSheets.Contains(designTokensUss))
                _root.styleSheets.Add(designTokensUss);
            if (calendarUss != null && !_root.styleSheets.Contains(calendarUss))
                _root.styleSheets.Add(calendarUss);

            _grid = _root.Q<VisualElement>("grid");
            _status = _root.Q<Label>("status");

            if (_grid != null)
            {
                for (int i = 0; i < 35; i++)
                {
                    var day = new VisualElement();
                    day.AddToClassList("calendar-day");
                    var label = new Label((i + 1).ToString());
                    label.AddToClassList("calendar-day-label");
                    day.Add(label);
                    _grid.Add(day);
                }
            }

            if (_status != null)
                _vm.OnStatusChanged += s => _status.text = "Status: " + s;

            _root.style.display = DisplayStyle.Flex;
            _built = true;
            Debug.Log($"[CalendarScreen] Built. styleSheets={_root.styleSheets.count}, days=35");
            StartCoroutine(ProjectC.UIv2.Core.Animation.UIAnimator.FadeIn(_root, 0.25f));
        }
    }
}