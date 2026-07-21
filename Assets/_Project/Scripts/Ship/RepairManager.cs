using UnityEngine;
using ProjectC.Core;
using ProjectC.Ship.UI;

namespace ProjectC.Ship
{
    /// <summary>
    /// RepairManager — MonoBehaviour на NPC в доке.
    /// При взаимодействии (E) открывает RepairManagerWindow с каталогом модулей.
    ///
    /// Настройка в инспекторе:
    ///   1. Повесить на GameObject NPC в доке
    ///   2. Назначить ModuleShopDatabase
    ///   3. GameObject должен иметь Collider (isTrigger=true) для регистрации в InteractableManager
    /// </summary>
    public class RepairManager : MonoBehaviour, IInteractable
    {
        [Header("Каталог модулей")]
        [Tooltip("База данных модулей с ценами и ресурсами. Будет передана в RepairManagerWindow.")]
        [SerializeField] private ModuleShopDatabase _shopDatabase;

        [Header("Покраска")]
        [Tooltip("Стоимость покраски корабля в кредитах.")]
        [SerializeField] private int _repaintCost = 500;

        [Header("Ремонт корпуса")]
        [Tooltip("Стоимость ремонта корпуса в кредитах.")]
        [SerializeField] private int _hullRepairCost = 300;

        [Header("Ship Recall")]
        [Tooltip("Стоимость вызова корабля на ближайший пад в кредитах.")]
        [SerializeField] private int _shipRecallCost = 500;

        [Header("Interaction")]

        [Tooltip("Радиус взаимодействия с NPC")]
        [SerializeField] private float _interactionRadius = 3f;

        [Tooltip("Текст подсказки при наведении")]
        [SerializeField] private string _interactHint = "🛠 Ремонтный менеджер [E]";

        // IInteractable
        public string InstanceId => gameObject.name;
        public string DisplayName => _interactHint;
        public float InteractionRadius => _interactionRadius;
        public Vector3 Position => transform.position;

        // ============================================================
        // Trigger registration (как CraftingStation)
        // ============================================================

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            InteractableManager.RegisterRepairManager(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            InteractableManager.UnregisterRepairManager(this);
        }

        // ============================================================
        // Interaction
        // ============================================================

        /// <summary>Открыть окно ремонтного менеджера. Вызывается из NetworkPlayer.TryInteractNearestRepairManager.</summary>
        public void Interact()
        {
            if (RepairManagerWindow.Instance == null)
            {
                Debug.LogWarning("[RepairManager] RepairManagerWindow.Instance is null. " +
                    "Убедитесь что RepairManagerWindow создан в сцене (как CommPanelWindow).");
                return;
            }

            RepairManagerWindow.Instance.Show(_shopDatabase, _repaintCost, _shipRecallCost, _hullRepairCost);
            Debug.Log("[RepairManager] RepairManagerWindow opened");

        }

        /// <summary>Закрыть окно (можно вызвать извне).</summary>
        public void CloseWindow()
        {
            if (RepairManagerWindow.Instance != null)
                RepairManagerWindow.Instance.Hide();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _interactionRadius);
        }
#endif
    }
}
