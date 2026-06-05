using UnityEngine;
using ProjectC.Trade;
using ProjectC.Trade.Client;
using ProjectC.Trade.Network;
using ProjectC.Player;

/// <summary>
/// Триггер-зона NPC-агента НП (доска контрактов).
/// GDD_25 секция 6: Контрактная Система.
/// Утверждено решение 2A: игрок подходит к NPC-агенту в целевой локации → нажимает C.
/// Утверждено решение 5B: отдельный префаб ContractBoardWindow.
///
/// Сессия 7: ContractSystem.
/// Поток: Игрок входит в триггер → нажимает C → открывается ContractBoardWindow.
///
/// C2-этап миграции контрактов: scene-marker остался (для UI hint), но вместо
/// legacy ContractBoardUI → ContractInteractor.TryOpenContractBoard() (новый UI Toolkit).
/// legacy ContractSystem и ContractBoardUI продолжают работать параллельно (для регресса).
/// </summary>
public class ContractTrigger : MonoBehaviour
{
    [Header("NPC Agent Info")]
    [Tooltip("ID локации, к которой привязан NPC-агент (primium/secundus/tertius/quartus)")]
    [SerializeField] private string locationId = "primium";

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

            // Закрыть UI окно контрактов при выходе из зоны (v2)
            if (ContractBoardWindow.Instance != null)
            {
                ContractBoardWindow.Instance.Hide();
            }
        }
    }

    private void Update()
    {
        if (_nearbyPlayer == null) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        // C2-этап: ключ C для контрактов (раньше был тот же C, теперь не конфликтует с E-рынок)
        if (kb != null && kb.cKey.wasPressedThisFrame)
        {
            OpenContractBoard(_nearbyPlayer);
        }
    }

    /// <summary>
    /// Открыть доску контрактов (v2 — UI Toolkit).
    /// </summary>
    public void OpenContractBoard(NetworkPlayer player)
    {
        if (string.IsNullOrEmpty(locationId))
        {
            Debug.LogWarning($"[ContractTrigger] locationId не задан для {npcAgentName}!");
            return;
        }

        // FIX (C2-этап): Вместо legacy ContractBoardUI используем ContractInteractor.
        // ContractInteractor сам найдёт ContractZone, запросит snapshot и откроет окно.
        if (!ContractInteractor.TryOpenContractBoard())
        {
            // Fallback на legacy ContractBoardUI (если v2-зона не найдена, но v1-маркер есть)
            if (ContractBoardUI.Instance != null)
            {
                Debug.LogWarning("[ContractTrigger] v2 ContractInteractor не нашёл зону, fallback на legacy ContractBoardUI");
                ContractBoardUI.Instance.OpenBoard(null, player);
            }
            else
            {
                Debug.LogWarning($"[ContractTrigger] Не удалось открыть доску контрактов для {npcAgentName} (нет v2-зоны и v1-UI)");
            }
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
