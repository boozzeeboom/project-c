using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using ProjectC.Core;

namespace ProjectC.UI
{
    /// <summary>
    /// UI панель для подключения к серверу.
    /// Disconnect кнопка создаётся программно (не зависит от Inspector).
    /// Обновляется мгновенно через события сети.
    /// </summary>
    public class NetworkUI : MonoBehaviour
    {
        [Header("Ссылки на UI элементы")]
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button startClientButton;
        [SerializeField] private TMP_InputField serverIpInput;
        [SerializeField] private TMP_InputField serverPortInput;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject connectionPanel;
        [SerializeField] private TextMeshProUGUI playerCountText;

        // Disconnect создаётся программно
        private Button _disconnectButton;

        private NetworkManagerController networkManagerController;

        private void Awake()
        {
            networkManagerController = FindAnyObjectByType<NetworkManagerController>();
            if (networkManagerController == null)
            {
                Debug.LogError("[NetworkUI] NetworkManagerController не найден!");
                return;
            }

            // Кнопки
            if (startHostButton != null) startHostButton.onClick.AddListener(OnStartHostClicked);
            if (startClientButton != null) startClientButton.onClick.AddListener(OnStartClientClicked);

            // События сети
            networkManagerController.OnConnectionStatusChanged += UpdateStatus;
            networkManagerController.OnPlayerConnected += HandlePlayerConnected;
            networkManagerController.OnPlayerDisconnected += HandlePlayerDisconnected;

            CreateDisconnectButton();
            UpdateButtons(false);
        }

        private void HandlePlayerConnected(ulong clientId) => UpdateButtons(true);

        private void HandlePlayerDisconnected(ulong clientId)
        {
            if (!networkManagerController.IsConnected)
                UpdateButtons(false);
        }

        private void CreateDisconnectButton()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            var btnObj = new GameObject("DisconnectButton");
            btnObj.transform.SetParent(canvas.transform, false);

            var rt = btnObj.AddComponent<RectTransform>();
            
            // Позиционируем прямо по центру экрана
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(200, 50);

            Debug.Log($"[NetworkUI] Canvas: {canvas.name}, RenderMode: {canvas.renderMode}");
            
            var canvasRt = canvas.GetComponent<RectTransform>();
            
            // Исправляем Canvas: растягиваем на весь экран и центрируем
            canvasRt.anchorMin = Vector2.zero;
            canvasRt.anchorMax = Vector2.one;
            canvasRt.anchoredPosition = Vector2.zero;
            canvasRt.sizeDelta = Vector2.zero;
            
            Debug.Log($"[NetworkUI] Canvas RectTransform - anchorMin: {canvasRt.anchorMin}, anchorMax: {canvasRt.anchorMax}, " +
                      $"pivot: {canvasRt.pivot}, anchoredPosition: {canvasRt.anchoredPosition}, " +
                      $"localScale: {canvasRt.localScale}, sizeDelta: {canvasRt.sizeDelta}");
            
            Debug.Log($"[NetworkUI] Button created - anchorMin: {rt.anchorMin}, anchorMax: {rt.anchorMax}, " +
                      $"pivot: {rt.pivot}, anchoredPosition: {rt.anchoredPosition}, sizeDelta: {rt.sizeDelta}");

            var image = btnObj.AddComponent<Image>();
            image.color = new Color(0.9f, 0.2f, 0.2f, 0.95f);

            _disconnectButton = btnObj.AddComponent<Button>();
            var colors = new ColorBlock();
            colors.normalColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);
            colors.highlightedColor = new Color(1f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 1f);
            _disconnectButton.colors = colors;
            _disconnectButton.onClick.AddListener(OnDisconnectClicked);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform);
            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Disconnect";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 16;
            tmp.color = Color.white;

            btnObj.SetActive(false);
        }

        private void Update()
        {
            // Escape — toggle Disconnect
            if (networkManagerController != null && networkManagerController.IsConnected && _disconnectButton != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    _disconnectButton.gameObject.SetActive(!_disconnectButton.gameObject.activeSelf);
            }
        }

        private void UpdateButtons(bool connected)
        {
            if (startHostButton != null) startHostButton.gameObject.SetActive(!connected);
            if (startClientButton != null) startClientButton.gameObject.SetActive(!connected);
            if (_disconnectButton != null) _disconnectButton.gameObject.SetActive(connected);
        }

        private void OnStartHostClicked()
        {
            networkManagerController.StartHost();
            HideConnectionPanel();
            UpdateButtons(true); // Мгновенно показываем Disconnect
        }

        private void OnStartClientClicked()
        {
            string ip = serverIpInput?.text ?? "127.0.0.1";
            ushort port = 7777;
            if (serverPortInput != null && !string.IsNullOrEmpty(serverPortInput.text))
                ushort.TryParse(serverPortInput.text, out port);

            networkManagerController.ConnectToServer(ip, port);
            HideConnectionPanel();
            // Disconnect появится когда сработает OnPlayerConnected
        }

        private void OnDisconnectClicked()
        {
            networkManagerController.Disconnect();
            ShowConnectionPanel();
            UpdateStatus("Отключено");
        }

        private void UpdateStatus(string status)
        {
            if (statusText != null) statusText.text = status;
        }

        private void HideConnectionPanel()
        {
            if (connectionPanel != null) connectionPanel.SetActive(false);
        }

        private void ShowConnectionPanel()
        {
            if (connectionPanel != null) connectionPanel.SetActive(true);
        }

        private void OnDestroy()
        {
            if (startHostButton != null) startHostButton.onClick.RemoveListener(OnStartHostClicked);
            if (startClientButton != null) startClientButton.onClick.RemoveListener(OnStartClientClicked);

            if (networkManagerController != null)
            {
                networkManagerController.OnConnectionStatusChanged -= UpdateStatus;
                networkManagerController.OnPlayerConnected -= HandlePlayerConnected;
                networkManagerController.OnPlayerDisconnected -= HandlePlayerDisconnected;
            }
        }
    }
}
