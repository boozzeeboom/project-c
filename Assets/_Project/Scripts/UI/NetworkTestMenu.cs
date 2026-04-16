using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectC.Core;

namespace ProjectC.UI
{
    /// <summary>
    /// Simple network test menu using NetworkManagerController.
    /// Host/Client/Server buttons with proper connection handling.
    /// </summary>
    public class NetworkTestMenu : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button serverButton;
        
        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;
        
        // Singleton
        public static NetworkTestMenu Instance { get; private set; }
        
        private NetworkManagerController _nmc;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // Get NetworkManagerController
            _nmc = FindAnyObjectByType<NetworkManagerController>();
            
            // Button subscriptions
            if (hostButton != null)
                hostButton.onClick.AddListener(() => StartAsHost());
            
            if (clientButton != null)
                clientButton.onClick.AddListener(() => StartAsClient());
            
            if (serverButton != null)
                serverButton.onClick.AddListener(() => StartAsServer());

            // Subscribe to NMC events
            if (_nmc != null)
            {
                _nmc.OnConnectionStatusChanged += OnConnectionStatusChanged;
                _nmc.OnPlayerConnected += OnPlayerConnected;
                _nmc.OnPlayerDisconnected += OnPlayerDisconnected;
            }

            UpdateStatus("Select connection mode");
        }

        private void OnDestroy()
        {
            if (_nmc != null)
            {
                _nmc.OnConnectionStatusChanged -= OnConnectionStatusChanged;
                _nmc.OnPlayerConnected -= OnPlayerConnected;
                _nmc.OnPlayerDisconnected -= OnPlayerDisconnected;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            menuPanel?.SetActive(true);
            if (hostButton != null) hostButton.gameObject.SetActive(true);
            if (clientButton != null) clientButton.gameObject.SetActive(true);
            if (serverButton != null) serverButton.gameObject.SetActive(true);
            UpdateStatus("Select connection mode");
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void StartAsHost()
        {
            if (_nmc != null)
            {
                _nmc.StartHost();
                Hide();
            }
            else
            {
                UpdateStatus("Error: NetworkManagerController not found!");
            }
        }

        private void StartAsClient()
        {
            if (_nmc != null)
            {
                // Connect to localhost by default
                _nmc.ConnectToServer("127.0.0.1", 7777);
                Hide();
            }
            else
            {
                UpdateStatus("Error: NetworkManagerController not found!");
            }
        }

        private void StartAsServer()
        {
            if (_nmc != null)
            {
                _nmc.StartServer();
                Hide();
            }
            else
            {
                UpdateStatus("Error: NetworkManagerController not found!");
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
            Debug.Log($"[NetworkTestMenu] {message}");
        }

        private void OnConnectionStatusChanged(string status)
        {
            UpdateStatus(status);
        }

        private void OnPlayerConnected(ulong clientId)
        {
            Debug.Log($"[NetworkTestMenu] Player connected: {clientId}");
        }

        private void OnPlayerDisconnected(ulong clientId)
        {
            Debug.Log($"[NetworkTestMenu] Player disconnected: {clientId}");
        }

        public bool IsHost => _nmc?.IsHost ?? false;
        public bool IsServer => _nmc?.IsServer ?? false;
        public bool IsClient => _nmc?.IsClient ?? false;
    }
}