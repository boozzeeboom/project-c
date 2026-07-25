// ProjectC: NGO Metrics Collector — T-PERF-09
// Design: docs/world/admin_tool/perfomance/PERFORMANCE_MONITORING_RESEARCH.md §4.5
// Collects basic NGO network statistics: RTT, connected clients, bandwidth estimates.
// Note: NGO 2.13 does not expose NetworkMetrics API publicly — uses available hooks.
using UnityEngine;
using Unity.Netcode;
using Unity.Profiling;

namespace ProjectC.Core
{
    /// <summary>
    /// Collects and exposes network metrics for the runtime HUD.
    /// Attach to same GameObject as NetworkManager.
    /// </summary>
    public class NgoMetricsCollector : MonoBehaviour
    {
        public static NgoMetricsCollector Instance { get; private set; }

        [Header("Update Interval")]
        [SerializeField] private float _updateInterval = 1f;

        // Public metrics (read by HUD)
        public int ConnectedClients { get; private set; }
        public float RttMs { get; private set; }
        public ulong BytesSent { get; private set; }
        public ulong BytesReceived { get; private set; }
        public int RPCSentThisInterval { get; private set; }
        public int RPCReceivedThisInterval { get; private set; }

        private NetworkManager _nm;
        private float _timer;

        // Profiler recorders for built-in network stats
        private ProfilerRecorder _netMsgsSentRecorder;
        private ProfilerRecorder _netMsgsRecvdRecorder;

        private void Awake() => Instance = this;

        private void Start()
        {
            _nm = NetworkManager.Singleton;
            if (_nm != null)
            {
                _nm.OnClientConnectedCallback += OnClientConnected;
                _nm.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        private void OnEnable()
        {
            _netMsgsSentRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Network, "Messages Sent Count", 15);
            _netMsgsRecvdRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Network, "Messages Received Count", 15);
        }

        private void OnDisable()
        {
            _netMsgsSentRecorder.Dispose();
            _netMsgsRecvdRecorder.Dispose();
        }

        private void OnDestroy()
        {
            if (_nm != null)
            {
                _nm.OnClientConnectedCallback -= OnClientConnected;
                _nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void Update()
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < _updateInterval) return;
            _timer = 0f;

            if (_nm == null || !_nm.IsListening) return;

            ConnectedClients = _nm.ConnectedClientsIds.Count;

            // RTT from transport (returns ulong, typically in ms)
            if (_nm.NetworkConfig.NetworkTransport != null)
            {
                ulong rawRtt = _nm.NetworkConfig.NetworkTransport.GetCurrentRtt(_nm.LocalClientId);
                RttMs = rawRtt > 0 ? rawRtt : 0f;
            }
            else
            {
                RttMs = 0f;
            }

            // RPC counts from profiler
            if (_netMsgsSentRecorder.Valid)
                RPCSentThisInterval = (int)(_netMsgsSentRecorder.LastValueAsDouble);
            if (_netMsgsRecvdRecorder.Valid)
                RPCReceivedThisInterval = (int)(_netMsgsRecvdRecorder.LastValueAsDouble);

            // Update counters for HUD display
            ProjectCPerfCounters.RpcSentPerInterval = RPCSentThisInterval;
            ProjectCPerfCounters.RpcReceivedPerInterval = RPCReceivedThisInterval;
            ProjectCPerfCounters.NetworkRttMs = RttMs;
        }

        private void OnClientConnected(ulong clientId) { }
        private void OnClientDisconnected(ulong clientId) { }

        /// <summary>
        /// Returns a one-line summary for HUD display.
        /// </summary>
        public string GetSummary()
        {
            return $"Net: {ConnectedClients} clients, RTT:{RttMs:F0}ms, " +
                   $"RPC↑{RPCSentThisInterval} ↓{RPCReceivedThisInterval}";
        }
    }
}
