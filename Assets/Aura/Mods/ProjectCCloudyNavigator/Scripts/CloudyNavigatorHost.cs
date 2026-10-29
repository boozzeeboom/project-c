// Cloudy Navigator — шаг 2 плана переноса (docs/UI/full rebuild plans/Cloudy_redesign/04_PORT_RULES_AND_PLAN.md §4).
//
// Окно живёт собственной папкой и ничего вне неё не меняет: UIDocument + CN_PanelSettings принадлежат окну,
// легаси-окно персонажа (P) продолжает работать как было. Пока окно скрыто, его корень в DisplayStyle.None —
// иначе прозрачный контейнер перехватывал бы клики поверх игры.
//
// Регистрация в UIManager — не правка UIManager, а его публичный API (см. 02 §6):
//   OpenPanel("CloudyNavigator", priority, onClose: Hide, gameObject) при показе,
//   ClosePanel("CloudyNavigator") при скрытии.
// Это даёт блокировку игрового ввода, разблокировку курсора и Esc → CloseTopPanel → наш Hide.
//
// Тик окна идёт отсюда, а не из собственного планировщика: элементы до попадания в панель не планируются,
// поэтому монтирование ленивое (первый Update, когда у UIDocument уже есть panel).
using ProjectC.UI;
using ProjectC.UI.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class CloudyNavigatorHost : MonoBehaviour
    {
        /// <summary>Имя панели в стеке UIManager — единственная строка, по которой нас находят Esc и блокировка ввода.</summary>
        public const string PanelName = "CloudyNavigator";

        /// <summary>Приоритет в стеке: выше HUD и легаси-окна, ниже модальных подтверждений.</summary>
        public const int PanelPriority = 60;

        [Header("References")]
        [Tooltip("Defaults to the UIDocument on this GameObject.")]
        [SerializeField] private UIDocument document;

        [Tooltip("CN_Window.uss — единственный источник вида окна. Назначается префабом (шаг 8), не грузится самим окном.")]
        [SerializeField] private StyleSheet styleSheet;

        [Header("Diagnostics")]
        [Tooltip("Пишет [CN] в консоль при показе и скрытии — для сверки с легаси в Play Mode (02 §8).")]
        [SerializeField] private bool debugLogging;

        private CloudyNavigatorWindow _window;
        private bool _open;

        public bool IsOpen => _open;
        public CloudyNavigatorWindow Window => _window;

        private void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            ApplyVisibility(false);
        }

        private void Update()
        {
            // Корень UIDocument создаётся в его собственном OnEnable, то есть позже нашего Awake: пока окно закрыто,
            // видимость подтверждаем каждый кадр, иначе прозрачный контейнер останется поверх игры и съест клики.
            if (!_open)
            {
                ApplyVisibility(false);
                return;
            }

            // Пока окно открыто, оно не сосуществует с легаси: если по P открылось старое окно — закрываемся сами.
            // Только публичные IsVisible()/Hide(), файл легаси не правится (02 §6, решение пользователя №3).
            var legacy = CharacterWindow.Instance;
            if (legacy != null && legacy.IsVisible())
            {
                Hide();
                return;
            }

            if (_window == null) return;
            if (document == null || document.rootVisualElement == null) return;
            if (document.rootVisualElement.panel == null) return;

            _window.Update(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        }

        public void Show()
        {
            if (_open) return;
            if (!EnsureMounted()) return;

            var legacy = CharacterWindow.Instance;
            if (legacy != null && legacy.IsVisible()) legacy.Hide();

            _open = true;
            ApplyVisibility(true);

            var ui = UIManager.Instance;
            if (ui != null && !ui.IsPanelOpen(PanelName)) ui.OpenPanel(PanelName, PanelPriority, Hide, gameObject);

            Log("открыто");
        }

        public void Hide()
        {
            if (!_open) return;
            _open = false;

            ApplyVisibility(false);

            var ui = UIManager.Instance;
            if (ui != null && ui.IsPanelOpen(PanelName)) ui.ClosePanel(PanelName);

            Log("закрыто");
        }

        public void Toggle()
        {
            if (_open) Hide();
            else Show();
        }

        private bool EnsureMounted()
        {
            if (_window != null) return true;
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) return false;

            var root = document.rootVisualElement;
            if (root == null) return false;

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet)) root.styleSheets.Add(styleSheet);

            _window = new CloudyNavigatorWindow(debugLogging);
            _window.Mount(root);

            ApplyVisibility(_open);
            return true;
        }

        private void ApplyVisibility(bool visible)
        {
            if (document == null) return;
            var root = document.rootVisualElement;
            if (root == null) return;

            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            root.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
        }

        private void OnDestroy()
        {
            if (_open)
            {
                var ui = UIManager.Instance;
                if (ui != null && ui.IsPanelOpen(PanelName)) ui.ClosePanel(PanelName);
                _open = false;
            }

            _window?.Dispose();
            _window = null;
        }

        private void Log(string message)
        {
            if (debugLogging) Debug.Log("[CN] " + message);
        }
    }
}
