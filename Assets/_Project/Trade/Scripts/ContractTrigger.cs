using UnityEngine;
using ProjectC.Player;

/// <summary>
/// Триггер-зона NPC-агента НП (доска контрактов).
/// GDD_25 секция 6: Контрактная Система.
/// Решение 2A: игрок подходит к NPC-агенту → нажимает C.
///
/// C2-refactor: контракты теперь живут как 3-й таб внутри MarketWindow
/// (см. docs/dev/CONTRACTS_AS_MARKET_TAB_REFACTOR.md). ContractTrigger остаётся
/// как scene-marker (для UI hint и как будущая точка спавна NPC), но больше
/// не открывает отдельное окно контрактов — вызывает MarketInteractor.TryOpenMarket,
/// который открывает MarketWindow с активной подпиской на контракты.
///
/// Legacy ContractBoardUI и ContractBoardWindow удалены. ContractSystem остаётся
/// для регресса (тоже удалится в C1-cleanup).
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

            // C2-refactor: контракты в табе MarketWindow, а не в отдельном окне.
            // Закрываем MarketWindow при выходе из зоны.
            if (ProjectC.Trade.Client.MarketWindow.Instance != null)
            {
                ProjectC.Trade.Client.MarketWindow.Instance.Hide();
            }
        }
    }

    private void Update()
    {
        if (_nearbyPlayer == null) return;

        var kb = UnityEngine.InputSystem.Keyboard.current;
        // C2-refactor: ключ C — открыть рынок (включая вкладку КОНТРАКТЫ)
        if (kb != null && kb.cKey.wasPressedThisFrame)
        {
            OpenContractBoard(_nearbyPlayer);
        }
    }

    /// <summary>
    /// Открыть рынок с активной подпиской на контракты для этой локации.
    /// </summary>
    public void OpenContractBoard(NetworkPlayer player)
    {
        if (string.IsNullOrEmpty(locationId))
        {
            Debug.LogWarning($"[ContractTrigger] locationId не задан для {npcAgentName}!");
            return;
        }

        // C2-refactor: Открываем MarketWindow (вместо ContractBoardWindow).
        // MarketInteractor.TryOpenMarket() сам подпишется на ContractClientState.RequestList(locationId)
        // и откроет MarketWindow. Таб "КОНТРАКТЫ" внутри покажет pending+active для этой локации.
        ProjectC.Trade.Client.MarketInteractor.TryOpenMarket();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        var collider = GetComponent<SphereCollider>();
        float radius = collider != null ? collider.radius : triggerRadius;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
