using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectC.UIv2.Screens.MainMenu
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuScreen : MonoBehaviour
    {
        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset mainMenuUxml;
        [SerializeField] private StyleSheet mainMenuUss;

        private UIDocument _doc;
        private VisualElement _root;
        private Label _status;
        private Button _btnPlay;
        private Button _btnSettings;
        private Button _btnQuit;
        private MainMenuViewModel _vm;
        private bool _built = false;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _vm = new MainMenuViewModel();

            if (mainMenuUxml == null)
                mainMenuUxml = Resources.Load<VisualTreeAsset>("UIv2/Screens/MainMenu/MainMenu");
        }

        private void OnEnable()
        {
            StartCoroutine(BuildWhenReady());
        }

        private void OnDisable()
        {
            TryUnsubscribe();
        }

        private IEnumerator BuildWhenReady()
        {
            // Wait until UIDocument has mounted its visual tree
            while (_doc == null || _doc.rootVisualElement == null)
            {
                if (_doc == null) _doc = GetComponent<UIDocument>();
                yield return null;
            }
            EnsureBuilt();
            TrySubscribe();
        }

        public void EnsureBuilt()
        {
            if (_built) return;
            if (_doc == null) _doc = GetComponent<UIDocument>();
            if (_doc == null || _doc.rootVisualElement == null) return;
            if (mainMenuUxml == null) { Debug.LogError("[MainMenuScreen] UXML not assigned"); return; }

            _root = _doc.rootVisualElement;

            if (mainMenuUss != null && !_root.styleSheets.Contains(mainMenuUss))
                _root.styleSheets.Add(mainMenuUss);

            // Tokens loaded via USS @import in MainMenu.uss.
            _status = _root.Q<Label>("status");
            _btnPlay = _root.Q<Button>("btn-play");
            _btnSettings = _root.Q<Button>("btn-settings");
            _btnQuit = _root.Q<Button>("btn-quit");

            if (_btnPlay != null) { _btnPlay.clicked -= OnPlay; _btnPlay.clicked += OnPlay; }
            if (_btnSettings != null) { _btnSettings.clicked -= OnSettings; _btnSettings.clicked += OnSettings; }
            if (_btnQuit != null) { _btnQuit.clicked -= OnQuit; _btnQuit.clicked += OnQuit; }

            _root.style.display = DisplayStyle.Flex;
            _built = true;
            Debug.Log($"[MainMenuScreen] Built. styleSheets={_root.styleSheets.count}, children={_root.childCount}");
        }

        private void TrySubscribe()
        {
            if (_vm != null)
                _vm.OnStatusChanged += s => { if (_status != null) _status.text = "Status: " + s; };
        }

        private void TryUnsubscribe() { }

        private void OnPlay() { _vm?.SetStatus("Play clicked"); Debug.Log("[MainMenuScreen] Play"); }
        private void OnSettings() { _vm?.SetStatus("Settings clicked"); Debug.Log("[MainMenuScreen] Settings"); }
        private void OnQuit() { _vm?.SetStatus("Quit clicked"); Debug.Log("[MainMenuScreen] Quit"); }
    }
}
