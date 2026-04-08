using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Серверное хранение кредитов игрока.
/// Сессия 5: Авторитетный сервер — кредиты хранятся и проверяются только на сервере.
/// </summary>
public class PlayerCreditsManager : NetworkBehaviour
{
    // NetworkVariable для синхронизации кредитов (сервер → клиент)
    private NetworkVariable<float> _credits = new NetworkVariable<float>(
        1000f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public float Credits
    {
        get => _credits.Value;
        set
        {
            if (IsServer)
            {
                _credits.Value = Mathf.Max(0f, value);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Загружаем сохранённые кредиты
        float savedCredits = PlayerPrefs.GetFloat($"Credits_{OwnerClientId}", 1000f);
        _credits.Value = Mathf.Max(0f, savedCredits);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            // Сохраняем при отключении
            PlayerPrefs.SetFloat($"Credits_{OwnerClientId}", Credits);
            PlayerPrefs.Save();
        }
    }

    private void OnApplicationQuit()
    {
        if (IsServer)
        {
            PlayerPrefs.SetFloat($"Credits_{OwnerClientId}", Credits);
            PlayerPrefs.Save();
        }
    }
}
