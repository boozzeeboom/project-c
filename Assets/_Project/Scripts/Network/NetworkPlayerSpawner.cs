using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Network
{
    /// <summary>
    /// DIAGNOSTIC-ONLY monitor for player spawns.
    ///
    /// ИСТОРИЯ:
    ///   Раньше этот компонент вручную спавнил свой GameObject (scene-placed) как
    ///   PlayerObject через <see cref="NetworkObject.SpawnAsPlayerObject"/> в
    ///   <see cref="Update"/>. Это КОНФЛИКТОВАЛО с NGO 2.x auto-spawn через
    ///   <c>NetworkConfig.PlayerPrefab</c> и приводило к двум PlayerObject'ам с
    ///   одним OwnerClientId (видимый «ghost clone»).
    ///
    ///   Подробный разбор: <c>docs/dev/INVESTIGATION_GHOST_PLAYER_CLONE.md</c>.
    ///
    /// ТЕКУЩАЯ РОЛЬ:
    ///   Только логировать, что NGO PlayerPrefab auto-spawn сработал, и предупреждать,
    ///   если для какого-то clientId PlayerObject не назначен (аномалия).
    ///
    ///   ВАЖНО: компонент сохранён в сцене, потому что на его GameObject висят
    ///   <c>NetworkPlayer</c>, <c>CharacterController</c>, <c>PlayerInputReader</c>,
    ///   <c>NetworkObject</c> — на них могут быть ссылки из других систем. Полное
    ///   удаление GameObject = отдельная задача с референс-аудитом.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkPlayerSpawner : MonoBehaviour
    {
        [Header("DIAGNOSTIC")]
        [Tooltip("Логировать факт NGO PlayerPrefab auto-spawn. Полезно для отладки хост/клиент потока.")]
        [SerializeField] private bool logAutoSpawn = true;

        [Tooltip("Предупреждать, если у подключённого клиента нет PlayerObject (аномалия).")]
        [SerializeField] private bool warnIfNoPlayerObject = true;

        // Старое поле — оставлено как [SerializeField] obsolete, чтобы не падали
        // сериализованные сцены, где это поле ещё прописано в инспекторе.
        // Логика по нему была удалена вместе с Update() host-spawn loop.
        // CS0414 подавлен намеренно: поле нужно ТОЛЬКО для сохранения совместимости
        // с ранее сериализованной сценой BootstrapScene.unity, не для runtime-чтения.
#pragma warning disable 0414
        [System.Obsolete("useScenePlayerAsHost больше не используется. PlayerObject спавнится NGO через NetworkConfig.PlayerPrefab автоматически. См. docs/dev/INVESTIGATION_GHOST_PLAYER_CLONE.md")]
        [SerializeField] private bool useScenePlayerAsHost = true;
#pragma warning restore 0414

        private bool _hasSubscribed = false;
        private System.Collections.Generic.HashSet<ulong> _loggedClientIds = new System.Collections.Generic.HashSet<ulong>();

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                _hasSubscribed = true;
            }
        }

        private void OnDestroy()
        {
            if (_hasSubscribed && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                _hasSubscribed = false;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;

            // Только сервер (host = server) видит факт auto-spawn и состояние PlayerObject.
            if (!NetworkManager.Singleton.IsServer) return;

            // Защита от повторного логирования для одного clientId (NGO может вызвать
            // OnClientConnected несколько раз при reconnect'е).
            if (!_loggedClientIds.Add(clientId)) return;

            var playerObject = NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var cc)
                ? cc.PlayerObject
                : null;

            if (logAutoSpawn)
            {
                if (playerObject != null)
                {
                    Debug.Log($"[NetworkPlayerSpawner] NGO PlayerPrefab auto-spawned player for clientId={clientId} at {playerObject.transform.position} (prefab={playerObject.name})");
                }
                else if (warnIfNoPlayerObject)
                {
                    Debug.LogWarning($"[NetworkPlayerSpawner] ClientId={clientId} connected but PlayerObject is NULL. " +
                                     "Проверь NetworkConfig.PlayerPrefab в NetworkManagerController (BootstrapScene). " +
                                     "См. docs/dev/INVESTIGATION_GHOST_PLAYER_CLONE.md.");
                }
            }
        }

        // ─── УДАЛЁННАЯ ЛОГИКА (см. INVESTIGATION_GHOST_PLAYER_CLONE.md) ──────────
        //
        // private void Update()
        // {
        //     if (useScenePlayerAsHost && !_hasSpawnedHostPlayer && ...IsHost) {
        //         _hasSpawnedHostPlayer = true;
        //         SpawnLocalPlayer();   // ← спавнил scene-placed GO как PlayerObject
        //                                 //    → конфликт с NGO PlayerPrefab auto-spawn
        //     }
        // }
        //
        // private void SpawnLocalPlayer()
        // {
        //     var networkObject = GetComponent<NetworkObject>();
        //     if (networkObject != null && !networkObject.IsSpawned) {
        //         networkObject.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId);
        //     }
        // }
        //
        // private void SpawnPlayerForClient(ulong clientId)
        // {
        //     // Instantiate(thisNetworkObject.gameObject, ...) ← клонировал scene-placed
        //     // GO как player для remote client → дубль поверх NGO PlayerPrefab.
        // }
    }
}
