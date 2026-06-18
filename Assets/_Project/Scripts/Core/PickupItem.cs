// =====================================================================================
// PickupItem.cs — подбираемый предмет в мире (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/dev/INVENTORY_V2_REFACTOR.md — Phase 3 (PickupItem → InventoryClientState)
//
// Phase 3 ИЗМЕНЕНИЯ:
//   • Раньше: Collect() просто gameObject.SetActive(false). Предмет исчезал, но НЕ
//     попадал в инвентарь — цепочка была разорвана.
//   • Теперь: Collect() просит InventoryClientState → InventoryServer → валидация на
//     сервере → NetworkVariable<InventoryData> → InventoryClientState.OnSnapshotUpdated
//     → UI обновляется → ВСЁ через OnInventoryResult мы деактивируем (или реактивируем
//     при ошибке) GameObject.
//
// LEGACY: collectOld() оставлен для совместимости, вызывается только в edit-mode / тестах.
// =====================================================================================

using UnityEngine;
using ProjectC.Items.Client;
using ProjectC.Items.Dto;

namespace ProjectC.Items
{
    /// <summary>
    /// Подбираемый предмет в мире.
    /// Имеет trigger-коллайдер, покачивается, при подборе через E отправляет RPC на сервер.
    /// </summary>
    public class PickupItem : MonoBehaviour, Core.IInteractable
    {
        [Header("Item Data")]
        public ItemData itemData;
        [Tooltip("itemId в InventoryWorld._itemDatabase. Проставляется сервером при server-spawn (drop). Если 0 — будет вычислен через GetOrRegisterItemId при Collect().")]
        public int itemId;   // public — server пишет после Instantiate (см. InventoryServer.RequestDropRpc)

        [Header("Settings")]
        public float floatSpeed = 1f;
        public float floatAmplitude = 0.2f;

        [Header("Interaction")]
        [Tooltip("Радиус взаимодействия (используется IInteractable)")]
        public float interactionRadius = 3f;

        private Vector3 _startPosition;
        private bool _isCollected = false;
        private bool _isAwaitingServer = false;   // защита от двойного E

        // IInteractable implementation
        public string InstanceId => gameObject.name + "_" + GetHashCode();
        public string DisplayName => itemData != null ? itemData.itemName : "Unknown Item";
        public float InteractionRadius => interactionRadius;
        public Vector3 Position => transform.position;

        private void Start()
        {
            _startPosition = transform.position;

            // Ensure trigger collider exists
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<SphereCollider>();
            }
            collider.isTrigger = true;
        }

        private void Update()
        {
            // Visual bobbing (остановлено если собран)
            if (!_isCollected)
            {
                transform.position = _startPosition + Vector3.up * Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
                transform.Rotate(Vector3.up, 30f * Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Register with InteractableManager when player enters trigger
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                Core.InteractableManager.RegisterPickup(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Unregister from InteractableManager when player exits trigger
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                Core.InteractableManager.UnregisterPickup(this);
            }
        }

        private void OnDisable()
        {
            // Ensure cleanup when object is disabled
            Core.InteractableManager.UnregisterPickup(this);
        }

        /// <summary>
        /// Phase 3 (INVENTORY_V2_REFACTOR.md): запросить pickup у сервера.
        /// Вызывается из NetworkPlayer.Update при E (или из ItemPickupSystem).
        /// НЕ деактивирует сразу — ждёт server confirmation через OnInventoryResult.
        /// T-KEY-05: для Key-предметов читает instanceId из KeyRodInstanceBinding.
        /// </summary>
        public void Collect()
        {
            if (_isCollected || itemData == null) return;
            if (_isAwaitingServer)
            {
                // Защита от двойного E / спама
                return;
            }

            // Получить itemId: сначала пробуем прямое поле (если server-spawn).
            // Fallback: вычислить через InventoryWorld (для scene-placed pickup'ов).
            int itemId = this.itemId;
            if (itemId <= 0)
            {
                itemId = ProjectC.Items.InventoryWorld.Instance?.GetOrRegisterItemId(itemData) ?? -1;
            }
            if (itemId < 0)
            {
                Debug.LogWarning($"[PickupItem] Cannot resolve itemId for {itemData?.itemName} (InventoryWorld.Instance == null? Network not started?)");
                return;
            }

            // T-KEY-05: читаем instanceId из KeyRodInstanceBinding (если есть)
            int instanceId = 0;
            var keyBinding = GetComponent<ProjectC.Ship.Key.KeyRodInstanceBinding>();
            if (keyBinding != null)
            {
                keyBinding.TryGetInstanceId(out instanceId);
            }

            // Попробовать отправить запрос через новый v2 client state
            var clientState = ProjectC.Items.Client.InventoryClientState.Instance;
            if (clientState != null)
            {
                _isAwaitingServer = true;
                // T-KEY-05: передаём instanceId (0 для обычных предметов)
                clientState.RequestPickup(itemId, itemData.itemType, instanceId, transform.position,
                    HandlePickupResult);
            }
            else
            {
                // Крайний случай: нет v2 client state. Fallback на legacy — деактивируем молча.
                Debug.LogWarning($"[PickupItem] No InventoryClientState, falling back to legacy collect. {itemData.itemName}");
                ForceCollect();
            }
        }

        /// <summary>
        /// Обработка результата pickup от сервера.
        /// Подписка одноразовая: после обработки — отписка.
        /// </summary>
        private void HandlePickupResult(InventoryResultDto result)
        {
            _isAwaitingServer = false;

            if (result.IsSuccess)
            {
                _isCollected = true;
                gameObject.SetActive(false);
                Core.InteractableManager.UnregisterPickup(this);
                Debug.Log($"[PickupItem] {itemData?.itemName} успешно подобран");
            }
            else
            {
                // Failure: не деактивируем — пусть висит, попробуют ещё раз
                string msg = !string.IsNullOrEmpty(result.message)
                    ? result.message
                    : InventoryClientState.LocalizeResultCode((InventoryResultCode)result.code);
                Debug.LogWarning($"[PickupItem] Pickup failed: {msg}");
            }
        }

        /// <summary>
        /// LEGACY: принудительно деактивировать (без server RPC).
        /// Используется ТОЛЬКО в edge-cases: нет network, edit-mode, тесты.
        /// </summary>
        public void ForceCollect()
        {
            if (_isCollected || itemData == null) return;
            _isCollected = true;
            gameObject.SetActive(false);
            Core.InteractableManager.UnregisterPickup(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
