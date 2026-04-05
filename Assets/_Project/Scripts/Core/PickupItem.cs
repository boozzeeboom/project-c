using UnityEngine;
using ProjectC.Core;

namespace ProjectC.Items
{
    /// <summary>
    /// Компонент подбираемого предмета в мире.
    /// Навесить на GameObject с триггер-коллайдером.
    /// При нажатии E рядом — предмет подбирается и попадает в Inventory.
    /// Работает с NetworkInventory (синхронизация) и legacy Inventory (локальный).
    /// </summary>
    public class PickupItem : MonoBehaviour
    {
        [Header("Данные предмета")]
        public ItemData itemData;

        [Header("Настройки")]
        public float floatSpeed = 1f;
        public float floatAmplitude = 0.2f;

        private Vector3 _startPosition;
        private bool _isCollected = false;

        private void Start()
        {
            _startPosition = transform.position;

            // Проверка что есть коллайдер-триггер
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<SphereCollider>();
            }
            collider.isTrigger = true;
        }

        private void Update()
        {
            // Визуальное покачивание
            if (!_isCollected)
            {
                transform.position = _startPosition + Vector3.up * Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
                transform.Rotate(Vector3.up, 30f * Time.deltaTime);
            }
        }

        /// <summary>
        /// Подобрать предмет. Вызывается из NetworkPlayer.TryPickup().
        /// </summary>
        public void Collect()
        {
            if (_isCollected || itemData == null) return;
            _isCollected = true;

            // Скрыть предмет
            gameObject.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1.5f);
        }
    }
}
