// ProjectC Main Menu — connects the live UI Toolkit menu to the atmosphere layer.
// Ported from the mod's ui.js pointer wiring: a menu button nudges the clouds
// (hover = half gust, press = full gust, direction outward from the panel centre),
// opening a sub-panel parts the clouds, and the menu being hidden hides the camera.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCMainMenu
{
    [RequireComponent(typeof(ProjectCMainMenuAtmosphere))]
    public class ProjectCMainMenuMenuBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UIDocument menuDocument;
        [Tooltip("The ported menu (step 9). Resolved at runtime when left empty, because the " +
                 "UI prefab and this atmosphere prefab are separate assets.")]
        [SerializeField] private ProjectCMainMenuUiHost uiHost;
        [SerializeField] private ProjectCMainMenuAtmosphere atmosphere;

        [Header("Gust response (mod panel defaults)")]
        [SerializeField, Range(0f, 2f)] private float hoverGust = 0.5f;
        [SerializeField, Range(0f, 2f)] private float pressGust = 1f;
        [SerializeField] private float hoverGustMs = 620f;
        [SerializeField] private float pressGustMs = 900f;
        [SerializeField] private float pollInterval = 0.15f;

        [Header("Pointer lift")]
        [SerializeField] private bool pointerLiftEnabled = true;

        [Header("Menu backdrop")]
        [Tooltip("MainMenuStyles.uss paints main-menu-root with rgba(8,12,22,0.92), which hides the 3D atmosphere completely. Clears it at runtime (the USS asset itself is not modified).")]
        [SerializeField] private bool clearMenuBackdrop = true;

        private VisualElement _root;
        private VisualElement _buttons;
        private readonly List<Button> _hooked = new List<Button>();
        private readonly List<EventCallback<PointerEnterEvent>> _enterCallbacks = new List<EventCallback<PointerEnterEvent>>();
        private readonly List<EventCallback<PointerDownEvent>> _downCallbacks = new List<EventCallback<PointerDownEvent>>();
        private EventCallback<PointerMoveEvent> _moveCallback;

        private float _pollTimer;
        private bool _lastVisible = true;
        private float _lastFocus = -1f;
        private bool _backdropCleared;
        private ProjectCMainMenuWindow _hookedWindow;

        private void Awake()
        {
            if (atmosphere == null) atmosphere = GetComponent<ProjectCMainMenuAtmosphere>();
        }

        private void OnEnable()
        {
            _pollTimer = 0f;
            _lastFocus = -1f;
            _lastVisible = true;
        }

        private void OnDisable()
        {
            Unhook();
        }

        private void Update()
        {
            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = Mathf.Max(0.05f, pollInterval);

            // Step 9: once the ported window is in the scene it IS the menu. It reports its
            // own gusts, pointer and screen state, so nothing has to be scraped out of a DOM.
            var host = ResolveUiHost();
            if (host != null)
            {
                var window = host.Window;
                if (window != null)
                {
                    SyncFromWindow(host, window);
                    return;
                }
            }

            var root = _root != null && _root.panel != null ? _root : ResolveRoot();
            if (root == null)
            {
                // Fail closed: if the menu cannot be found, the opaque atmosphere camera
                // must not sit on top of the game.
                if (atmosphere != null) atmosphere.SetVisible(false);
                _lastVisible = false;
                return;
            }

            if (root != _root)
            {
                Unhook();
                _root = root;
                _buttons = root.Q<VisualElement>("main-menu-buttons");
                Hook();
            }

            SyncBackdrop();
            SyncState();
        }

        /// <summary>
        /// MainMenuWindow wraps <c>main-menu-root</c> in the element returned by CloneTree()
        /// and Hide() sets display=None on THAT wrapper, so checking only main-menu-root's
        /// own display always reported "visible" and the opaque atmosphere camera kept
        /// covering the loaded world. Walk the whole ancestor chain instead.
        /// </summary>
        private static bool IsShown(VisualElement element)
        {
            if (element == null || element.panel == null) return false;

            for (var e = element; e != null; e = e.parent)
            {
                if (!e.visible) return false;
                if (e.resolvedStyle.display == DisplayStyle.None) return false;
            }

            return true;
        }

        /// <summary>
        /// The live stylesheet gives main-menu-root a 92%-opaque full-screen fill, so the
        /// atmosphere camera behind it is never visible. An inline style beats the sheet
        /// and is re-applied if the menu rebuilds its tree.
        /// </summary>
        private void SyncBackdrop()
        {
            if (!clearMenuBackdrop || _root == null || _root.panel == null) return;
            if (_backdropCleared && Mathf.Approximately(_root.resolvedStyle.backgroundColor.a, 0f)) return;

            _root.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _backdropCleared = true;
        }

        private VisualElement ResolveRoot()
        {
            if (menuDocument != null && menuDocument.rootVisualElement != null)
            {
                var found = menuDocument.rootVisualElement.Q<VisualElement>("main-menu-root");
                if (found != null) return found;
            }

            var documents = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (var i = 0; i < documents.Length; i++)
            {
                var doc = documents[i];
                if (doc == null || doc.rootVisualElement == null) continue;
                var found = doc.rootVisualElement.Q<VisualElement>("main-menu-root");
                if (found == null) continue;
                menuDocument = doc;
                return found;
            }

            return null;
        }

        private void Hook()
        {
            if (_root != null && pointerLiftEnabled)
            {
                _moveCallback = OnRootPointerMove;
                _root.RegisterCallback(_moveCallback);
            }

            if (_buttons == null) return;

            foreach (var child in _buttons.Children())
            {
                if (child is not Button button) continue;

                EventCallback<PointerEnterEvent> enter = _ => OnButtonHover(button);
                EventCallback<PointerDownEvent> down = _ => OnButtonPress(button);

                button.RegisterCallback(enter);
                button.RegisterCallback(down);

                _hooked.Add(button);
                _enterCallbacks.Add(enter);
                _downCallbacks.Add(down);
            }
        }

        private void Unhook()
        {
            for (var i = 0; i < _hooked.Count; i++)
            {
                if (_hooked[i] == null) continue;
                _hooked[i].UnregisterCallback(_enterCallbacks[i]);
                _hooked[i].UnregisterCallback(_downCallbacks[i]);
            }

            _hooked.Clear();
            _enterCallbacks.Clear();
            _downCallbacks.Clear();

            if (_root != null && _moveCallback != null)
            {
                _root.UnregisterCallback(_moveCallback);
            }

            _moveCallback = null;
            _root = null;
            _buttons = null;
            _backdropCleared = false;

            if (_hookedWindow != null)
            {
                _hookedWindow.OnBlow = null;
                _hookedWindow = null;
            }
        }

        private void OnRootPointerMove(PointerMoveEvent evt)
        {
            if (!pointerLiftEnabled || atmosphere == null || _root == null) return;

            var height = Mathf.Max(1f, _root.worldBound.height);
            var local = _root.WorldToLocal(evt.position);
            var y01 = Mathf.Clamp01(local.y / height);
            atmosphere.PointerLift = 1f - y01;
        }

        private void OnButtonHover(Button button)
        {
            GustFrom(button, hoverGust, hoverGustMs);
        }

        private void OnButtonPress(Button button)
        {
            GustFrom(button, pressGust, pressGustMs);
        }

        private void GustFrom(Button button, float strength, float durationMs)
        {
            if (atmosphere == null || button == null) return;

            var originBounds = _root != null ? _root.worldBound : button.worldBound;
            var width = Mathf.Max(1f, originBounds.width);
            var height = Mathf.Max(1f, originBounds.height);

            var p = button.worldBound.center;
            var origin = new Vector2(
                Mathf.Clamp01((p.x - originBounds.x) / width),
                Mathf.Clamp01(1f - (p.y - originBounds.y) / height));

            var panelCenter = _buttons != null && _buttons.worldBound.width > 0.1f
                ? _buttons.worldBound.center
                : originBounds.center;

            var direction = (p - panelCenter) / Mathf.Max(1f, Mathf.Max(width, height));
            if (direction.sqrMagnitude < 1e-6f) direction = new Vector2(0.6f, -1f);

            // Menu buttons are below the panel centre, so the blow points down and out.
            if (Mathf.Abs(direction.y) < 1e-3f) direction.y = -1f;

            atmosphere.Blow(direction, origin, strength, durationMs);
        }

        private void SyncState()
        {
            if (atmosphere == null || _root == null) return;
            if (_root.panel == null) return;

            var rootVisible = IsShown(_root);
            if (rootVisible != _lastVisible)
            {
                _lastVisible = rootVisible;
                atmosphere.SetVisible(rootVisible);
            }

            var buttonsVisible = _buttons != null
                && _buttons.panel != null
                && _buttons.resolvedStyle.display != DisplayStyle.None;

            var focus = buttonsVisible ? 0f : 1f;
            if (!Mathf.Approximately(focus, _lastFocus))
            {
                _lastFocus = focus;
                atmosphere.SetFocus(focus);
            }
        }

        // ------------------------------------------------- ported window (step 9)

        private ProjectCMainMenuUiHost ResolveUiHost()
        {
            if (uiHost != null) return uiHost;

            // inactive objects are skipped on purpose: an inactive host shows no menu, and
            // the legacy fallback below then keeps the camera hidden as it always did
            uiHost = Object.FindAnyObjectByType<ProjectCMainMenuUiHost>();
            return uiHost;
        }

        /// <summary>
        /// Same three jobs the DOM path does, but read from the window's own state instead
        /// of from resolved styles: gusts arrive through OnBlow, visibility through the
        /// host, and «a sub-screen is open» through SettingsOpen / IsIpOpen.
        /// </summary>
        private void SyncFromWindow(ProjectCMainMenuUiHost host, ProjectCMainMenuWindow window)
        {
            if (_hookedWindow != window)
            {
                if (_hookedWindow != null) _hookedWindow.OnBlow = null;
                _hookedWindow = window;
                _hookedWindow.OnBlow = OnWindowBlow;
            }

            if (atmosphere == null) return;

            var shown = host.IsShown;
            if (shown != _lastVisible)
            {
                _lastVisible = shown;
                atmosphere.SetVisible(shown);
            }

            // the altimeter's pointer already comes from the window; the gas reads the
            // same 0..1 height, flipped to the shader's bottom-up convention
            if (pointerLiftEnabled) atmosphere.PointerLift = 1f - window.PointerY;

            var focus = window.SettingsOpen || window.IsIpOpen ? 1f : 0f;
            if (!Mathf.Approximately(focus, _lastFocus))
            {
                _lastFocus = focus;
                atmosphere.SetFocus(focus);
            }
        }

        private void OnWindowBlow(ProjectCMainMenuWindow.Gust gust)
        {
            if (atmosphere == null) return;

            // Gust.Y is panel space (0 = top); _GustOrigin in PCMM_Veil.shader is quad UV
            // (0 = bottom), so the origin flips — exactly what the DOM path did when it
            // built origin01 by hand. The direction is already in screen space.
            var origin = new Vector2(gust.X, 1f - gust.Y);
            atmosphere.Blow(new Vector2(gust.DirX, gust.DirY), origin, gust.Strength, gust.DurationMs);
        }
    }
}
