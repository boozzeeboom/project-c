using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Авто-установка сервера торговли при запуске Host/Server.
/// Сессия 5: TradeMarketServer создаётся один раз на сервере.
/// </summary>
public class TradeSetup : MonoBehaviour
{
    private static bool _initialized = false;

    private void Start()
    {
        if (_initialized) return;

        var networkManager = NetworkManager.Singleton;
        if (networkManager == null) return;

        // Подписываемся на старт сервера
        networkManager.OnServerStarted += OnServerStarted;
        _initialized = true;
    }

    private void OnServerStarted()
    {
        // Проверяем, есть ли уже TradeMarketServer на сцене
        var existing = FindAnyObjectByType<TradeMarketServer>();
        if (existing != null)
        {
            Debug.Log("[TradeSetup] TradeMarketServer уже существует на сцене");
            return;
        }

        // Создаём TradeMarketServer с NetworkObject
        var go = new GameObject("[TradeMarketServer]");
        var netObj = go.AddComponent<NetworkObject>();
        var server = go.AddComponent<TradeMarketServer>();

        // Спавним как серверный объект
        netObj.Spawn();

        Debug.Log("[TradeSetup] TradeMarketServer создан автоматически");
    }

    private void OnDestroy()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            networkManager.OnServerStarted -= OnServerStarted;
        }
    }
}
