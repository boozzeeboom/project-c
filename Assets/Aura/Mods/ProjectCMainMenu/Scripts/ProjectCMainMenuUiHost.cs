// ProjectC main menu — the component that owns the ported menu inside a UIDocument.
//
// Step 9 of docs/UI/MainMenu/02_smoke_menu_port.md. ProjectCMainMenuWindow is a plain
// C# object: it builds its tree in code, has no UXML and never touches a panel itself,
// because steps 3-8 were written to be mounted on whatever VisualElement the caller
// hands to Mount(). This is that caller in the game. It creates the window, mounts it
// on the UIDocument's root, gives it PCMM_Menu.uss, drives Update() and disposes it.
// UIDocument + PanelSettings stay the project's (Screen Space Overlay,
// ScaleWithScreenSize with a 1920x1080 reference) — the port does not replace them,
// it only takes them over from MainMenuWindow.
//
// The window ticks from here instead of from its own 16 ms scheduler, because Start()
// would schedule on an element that is not in a panel yet during scene load. Update()
// calls Start() itself on the first frame, so AutoTick stays off and the order is
// deterministic: mount -> panel exists -> first Update -> Start -> frames.
//
// Two project behaviours had to move off MainMenuWindow to keep the flow identical:
//
//   * «ОДИНОЧНАЯ ИГРА» -> GlobalMotionPilotRuntime.StartPilotHost() (the pilot path the
//     floating-origin project actually uses), then the menu hides;
//   * «ПОДКЛЮЧИТЬСЯ» in the IP sheet -> step 8 already calls ConnectToServer through
//     its IpConnectHandler seam and fires OnProjectAction("ip-connect"), so the menu
//     hides there too.
//
// «Скрыть» means «когда игра действительно началась», not «когда нажали кнопку»:
// the pilot loads WorldScene_0_0 and only later spawns the local player, and its own
// readiness handoff (GlobalMotionPilotRuntime.CloseStartupMenusWhenLocalPlayerReady)
// cannot see this menu because the legacy MainMenuWindow in BootstrapScene is disabled.
// So the host owns the same handoff here: it keeps the menu on screen and the cursor
// free until the local player is spawned, then hides and hands the cursor to the game.
// When the session ends (Esc -> «ВЫХОД В МЕНЮ») the menu comes back with a free cursor.
//
// The second one is not cosmetic. MainMenuWindow.Hide() hid the whole wrapper after a
// connect; without an equivalent the opaque atmosphere camera would stay on top of the
// loaded world, which is the exact regression the earlier IsShown() fix addressed.
using ProjectC.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCMainMenu
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class ProjectCMainMenuUiHost : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Defaults to the UIDocument on this GameObject.")]
        [SerializeField] private UIDocument document;

        [Tooltip("PCMM_Menu.uss — the port's only source of appearance. Assigned by " +
                 "Tools/ProjectC/MainMenu/Build UI Prefab; the window never loads it itself.")]
        [SerializeField] private StyleSheet styleSheet;

        [Header("Flow")]
        [Tooltip("Hide the menu once the local player is actually spawned, and bring it back " +
                 "when the session ends. The cursor stays free while the menu is visible.")]
        [SerializeField] private bool hideAfterStart = true;

        private ProjectCMainMenuWindow _window;
        private bool _disposed;
        private bool _shown = true;
        private bool _awaitingStart;
        private bool _sessionWasLive;

        public ProjectCMainMenuWindow Window => _window;
        public UIDocument Document => document;
        public bool IsShown => _shown;

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
        }

        private void Update()
        {
            if (_disposed) return;

            FollowSessionState();

            // UIDocument builds its root in its own OnEnable, and the component order in
            // the prefab is not a contract: mount lazily and let the next frame retry.
            if (_window == null) Mount();
            if (_window == null) return;

            // Nothing is schedulable before the document is in a panel: Start() would
            // register its timers on a detached element and the boot smoke would never run.
            if (document == null || document.rootVisualElement == null) return;
            if (document.rootVisualElement.panel == null) return;

            _window.Update(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        }

        private void OnDestroy()
        {
            if (_window == null) return;

            _disposed = true;
            _window.OnAction = null;
            _window.OnProjectAction = null;
            _window.Dispose();
            _window = null;
        }

        private void Mount()
        {
            if (_window != null || _disposed) return;

            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) return;

            var root = document.rootVisualElement;
            if (root == null) return;

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet)) root.styleSheets.Add(styleSheet);

            _window = new ProjectCMainMenuWindow { AutoTick = false };
            _window.Mount(root);
            _window.OnAction = OnMenuAction;
            _window.OnProjectAction = OnProjectAction;

            _shown = true;
            root.style.display = DisplayStyle.Flex;
            FreeCursor();
        }

        /// <summary>Shows or hides the whole menu. Hiding keeps the window state alive.</summary>
        public void SetShown(bool shown)
        {
            _shown = shown;

            if (document != null && document.rootVisualElement != null)
            {
                document.rootVisualElement.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (shown) FreeCursor();
        }

        // ------------------------------------------------------- session / cursor

        /// <summary>
        /// Меню и курсор следуют за реальной сессией, а не за нажатием кнопки.
        /// Пока локальный игрок не заспавнен, меню остаётся на экране и мышь свободна;
        /// после спавна меню уходит и мышь забирает игра; конец сессии возвращает меню.
        /// </summary>
        private void FollowSessionState()
        {
            bool live = IsSessionLive();
            if (live) _sessionWasLive = true;

            if (_shown) FreeCursor();

            if (_awaitingStart)
            {
                if (live && IsLocalPlayerReady())
                {
                    _awaitingStart = false;
                    HideForGameplay();
                }
                else if (!live)
                {
                    // Запуск не состоялся: меню — единственный путь назад, оставляем его.
                    _awaitingStart = false;
                    SetShown(true);
                }

                return;
            }

            if (!_shown && _sessionWasLive && !live)
            {
                // «ВЫХОД В МЕНЮ»: показываем это меню и отдаём мышь обратно игроку.
                _sessionWasLive = false;
                SetShown(true);
            }
        }

        /// <summary>Игра забирает мышь только теперь — когда локальный игрок действительно есть.</summary>
        private void HideForGameplay()
        {
            SetShown(false);
            UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        private static void FreeCursor()
        {
            if (UnityEngine.Cursor.lockState != UnityEngine.CursorLockMode.None)
                UnityEngine.Cursor.lockState = UnityEngine.CursorLockMode.None;
            if (!UnityEngine.Cursor.visible) UnityEngine.Cursor.visible = true;
        }

        private static bool IsSessionLive()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            return manager != null && manager.IsListening && !manager.ShutdownInProgress;
        }

        private static bool IsLocalPlayerReady()
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (manager == null || !manager.IsListening) return false;

            var localClient = manager.LocalClient;
            var player = localClient != null ? localClient.PlayerObject : null;
            return player != null && player.IsSpawned;
        }

        public void Show() => SetShown(true);

        // ------------------------------------------------------------- actions

        /// <summary>ui.js onAction — «settings» and «connect» never reach here, the window
        /// handles both itself (settings card, IP sheet).</summary>
        private void OnMenuAction(string action, string label)
        {
            switch (action)
            {
                case "host":
                    StartHost();
                    break;

                case "quit":
                    Quit();
                    break;
            }
        }

        private void StartHost()
        {
            // Pilot first, exactly like MainMenuWindow.OnHostClicked() and NetworkTestMenu.
            // GlobalMotionPilotRuntime is the only host launcher wired in BootstrapScene: it loads
            // WorldScene_0_0, retires the legacy scene owners and runs GlobalMotionNetworkStartup.TryPrepare
            // before NetworkManager.StartHost().
            //
            // NetworkManagerController.StartHost() is not an equivalent shortcut here: its profile has
            // EnforceGlobalContracts=true and protocol 0xF006, so PrepareGlobalMotionStart() demands a
            // GlobalMotionSessionCoordinator on this same GameObject. No such coordinator exists in the
            // scenes, so the controller returned silently (UpdateStatus only raises an event, no Debug.Log)
            // and the game never started.
            var pilot = Object.FindAnyObjectByType<ProjectC.World.FloatingOrigin.Pilot.GlobalMotionPilotRuntime>();
            if (pilot != null && pilot.isActiveAndEnabled)
            {
                pilot.StartPilotHost();
                if (hideAfterStart) _awaitingStart = true;
                return;
            }

            var controller = Object.FindAnyObjectByType<NetworkManagerController>();
            if (controller == null)
            {
                Debug.LogError("[ProjectCMainMenu] NetworkManagerController not found: cannot start a host.");
                return;
            }

            controller.StartHost();
            if (hideAfterStart) _awaitingStart = true;
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Step 8 fires this for ip-connect / debug-reset / locale / link.</summary>
        private void OnProjectAction(string action, string payload)
        {
            if (!hideAfterStart) return;
            if (action == "ip-connect") _awaitingStart = true;
        }
    }
}
