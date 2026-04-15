using UnityEngine;
using ProjectC.Trade;
using ProjectC.Player;

/// <summary>
/// Триггер-зона NPC-агента НП (доска контрактов).
/// GDD_25 секция 6: Контрактная Система.
/// Утверждено решение 2A: игрок подходит к NPC-агенту в целевой локации → нажимает E.
/// Утверждено решение 5B: отдельный префаб ContractBoardUI.
///
/// Сессия 7: ContractSystem.
/// Поток: Игрок входит в триггер → нажимает E → открывается ContractBoardUI.
/// </summary>
public class ContractTrigger : MonoBehaviour
{
    [Header("NPC Agent Info")]
    [Tooltip("Рынок локации, к которой привязан агент")]
    public LocationMarket market;

    [Tooltip("Имя NPC-агента")]
    public string npcAgentName = "Агент НП";

    [Header("Settings")]
    [Tooltip("Радиус триггера (если коллайдера нет)")]
    [SerializeField] private float triggerRadius = 5f;

    private NetworkPlayer _nearbyPlayer;

    private void Awake()
    {
        // Если нет коллайдера — создаём сферический триггер
        var collider = GetComponent<Collider>();
        if (collider == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = triggerRadius;
        }
        else if (!collider.isTrigger)
        {
            collider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<NetworkPlayer>();
        if (player != null && player.IsOwner)
        {
            _nearbyPlayer = player;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<NetworkPlayer>();
        if (player == _nearbyPlayer)
        {
            _nearbyPlayer = null;

            // Закрыть доску контрактов при выходе из зоны
            if (ContractBoardUI.Instance != null)
            {
                ContractBoardUI.Instance.CloseBoard();
            }
        }
    }

    private void Update()
    {
        if (_nearbyPlayer == null) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.cKey.wasPressedThisFrame)
        {
            OpenContractBoard(_nearbyPlayer);
        }
    }

    /// <summary>
    /// Открыть доску контрактов
    /// </summary>
    public void OpenContractBoard(NetworkPlayer player)
    {
        if (market == null)
        {
            Debug.LogWarning($"[ContractTrigger] Рынок не назначен для {npcAgentName}!");
            return;
        }

        if (ContractBoardUI.Instance != null)
        {
            ContractBoardUI.Instance.OpenBoard(market, player);
        }
        else
        {
            // Создать ContractBoardUI динамически
            var go = new GameObject("[ContractBoardUI]");
            var boardUI = go.AddComponent<ContractBoardUI>();
            boardUI.OpenBoard(market, player);
        }
    }

    /// <summary>
    /// Завершить активный контракт у NPC-агента (целевая локация)
    /// </summary>
    public void CompleteContractAtAgent(string contractId)
    {
        if (market == null) return;

        if (_nearbyPlayer != null && _nearbyPlayer.IsOwner)
        {
            _nearbyPlayer.ContractCompleteServerRpc(contractId, market.locationId);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        var collider = GetComponent<SphereCollider>();
        float radius = collider != null ? collider.radius : triggerRadius;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
