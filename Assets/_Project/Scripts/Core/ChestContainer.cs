using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Items
{
    /// <summary>
    /// Компонент сундука/контейнера с несколькими предметами.
    /// При нажатии E рядом — открывается, выдаёт все предметы из LootTable в инвентарь.
    /// </summary>
    public class ChestContainer : MonoBehaviour
    {
        [Header("Таблица добычи")]
        public LootTable lootTable;

        [Header("Настройки")]
        public float openRadius = 3f;
        public bool autoDestroy = true;

        [Header("Анимация")]
        public float openDuration = 0.8f;
        public Vector3 openRotationOffset = new Vector3(0, 0, -45f);
        public Vector3 openScaleOffset = new Vector3(0.1f, 0.1f, 0.1f);

        private bool _isOpen = false;
        private Vector3 _startRotation;
        private Vector3 _startScale;
        private float _openTimer = 0f;

        private void Start()
        {
            _startRotation = transform.eulerAngles;
            _startScale = transform.localScale;

            // Проверка коллайдера
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                Debug.LogWarning($"[ChestContainer] {gameObject.name} — нет Collider! Добавлен BoxCollider.");
                collider = gameObject.AddComponent<BoxCollider>();
            }
            collider.isTrigger = true;
        }

        private void Update()
        {
            // Анимация открытия
            if (_isOpen && _openTimer < openDuration)
            {
                _openTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_openTimer / openDuration);
                t = Mathf.SmoothStep(0, 1, t);

                transform.eulerAngles = Vector3.Lerp(_startRotation, _startRotation + openRotationOffset, t);
                transform.localScale = Vector3.Lerp(_startScale, _startScale + openScaleOffset, t);
            }
        }

        /// <summary>
        /// Открыть сундук. Вызывается из ItemPickupSystem.
        /// Возвращает список подобранных предметов.
        /// </summary>
        public List<ItemData> Open()
        {
            if (_isOpen || lootTable == null) return new List<ItemData>();
            _isOpen = true;

            // Генерируем лут
            var loot = lootTable.GenerateLoot();

            // Добавляем всё в инвентарь
            if (Inventory.Instance != null)
            {
                Inventory.Instance.AddMultipleItems(loot);

                // Запускаем визуальную вспышку в UI
                var inventoryUI = FindAnyObjectByType<InventoryUI>();
                if (inventoryUI != null)
                {
                    inventoryUI.TriggerSectorFlash();
                }
            }
            else
            {
                Debug.LogError("[ChestContainer] Inventory.Instance == null!");
            }

            // Автоуничтожение после открытия
            if (autoDestroy)
            {
                Destroy(gameObject, openDuration + 0.5f);
            }

            return loot;
        }

        /// <summary>
        /// Расстояние взаимодействия.
        /// </summary>
        public float GetOpenRadius()
        {
            return openRadius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, openRadius);
        }
    }
}
