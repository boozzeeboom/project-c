using UnityEngine;
using UnityEngine.UI;
using ProjectC.Core;

namespace ProjectC.UI
{
    /// <summary>
    /// UI панель для подключения к серверу
    /// </summary>
    public class NetworkUI : MonoBehaviour
    {
        [Header("Ссылки на UI элементы")]
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button startClientButton;
        [SerializeField] private InputField serverIpInput;
        [SerializeField] private InputField serverPortInput;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject connectionPanel;

        private NetworkManagerController networkManagerController;

        private void Awake()
        {
            // Находим Network Manager
            networkManagerController = FindObjectOfType<NetworkManagerController>();

            if (networkManagerController == null)
            {
                Debug.LogError("[NetworkUI] NetworkManagerController не найден!");
                return;
            }

            // Подписываемся на кнопки
            if (startHostButton != null)
                startHostButton.onClick.AddListener(OnStartHostClicked);

            if (startClientButton != null)
                startClientButton.onClick.AddListener(OnStartClientClicked);
        }

        private void OnStartHostClicked()
        {
            networkManagerController.StartHost();
            UpdateStatus("Хост запущен");
            HideConnectionPanel();
        }

        private void OnStartClientClicked()
        {
            string ip = serverIpInput?.text ?? "127.0.0.1";
            ushort port = 7777;
            
            if (serverPortInput != null && !string.IsNullOrEmpty(serverPortInput.text))
            {
                ushort.TryParse(serverPortInput.text, out port);
            }

            networkManagerController.ConnectToServer(ip, port);
            UpdateStatus($"Подключение к {ip}:{port}...");
        }

        private void UpdateStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = status;
            }
            Debug.Log($"[UI] Статус: {status}");
        }

        private void HideConnectionPanel()
        {
            if (connectionPanel != null)
            {
                connectionPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (startHostButton != null)
                startHostButton.onClick.RemoveListener(OnStartHostClicked);

            if (startClientButton != null)
                startClientButton.onClick.RemoveListener(OnStartClientClicked);
        }
    }
}
