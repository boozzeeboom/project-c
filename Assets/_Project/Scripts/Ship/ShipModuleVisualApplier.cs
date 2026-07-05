// =====================================================================================
// ShipModuleVisualApplier.cs — визуальный аппликатор модулей корабля (L1)
// =====================================================================================
// Паттерн: см. docs/Ships/customisation/01_MODULE_VISUAL_WITHOUT_BONES.md §3.1.
//
// Компонент вешается на root корабля (рядом с ShipModuleServer и ShipModuleManager).
// Подписывается на ShipModuleServer.OnModuleChanged и спавнит/уничтожает
// visualPrefab модулей под соответствующими ModuleSlot.transform.
//
// «Без костей»: ModuleSlot.transform — это и есть «кость» корабля.
// Никаких Animator, HumanBodyBones, EquipSlotToBone не нужно.
// =====================================================================================

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Ship
{
    [DisallowMultipleComponent]
    public class ShipModuleVisualApplier : NetworkBehaviour
    {
        [Header("Зависимости (автоопределение)")]
        [Tooltip("Менеджер модулей. Если не задан — ищется GetComponent<ShipModuleManager>().")]
        [SerializeField] private ShipModuleManager _manager;

        [Tooltip("Показывать warning при отсутствии visualPrefab/слота. Default true.")]
        [SerializeField] private bool _logWarnings = true;

        // slot.gameObject.name → spawned visual GameObject
        private readonly Dictionary<string, GameObject> _spawned = new();

        // ============================================================
        // Lifecycle
        // ============================================================

        private void Awake()
        {
            if (_manager == null)
                _manager = GetComponent<ShipModuleManager>();
        }

        public override void OnNetworkSpawn()
        {
            ShipModuleServer.OnModuleChanged += OnModuleChanged;

            // Late-join: применить текущее состояние
            ApplyAllFromManager();
        }

        public override void OnNetworkDespawn()
        {
            ShipModuleServer.OnModuleChanged -= OnModuleChanged;
            DestroyAllVisuals();
        }

        // ============================================================
        // Event → Apply
        // ============================================================

        private void OnModuleChanged(ulong shipNetId)
        {
            if (shipNetId != NetworkObjectId) return;
            ApplyAllFromManager();
        }

        private void ApplyAllFromManager()
        {
            if (_manager == null)
            {
                if (_logWarnings)
                    Debug.LogWarning($"[ShipModuleVisualApplier] ShipModuleManager == null on '{name}'", this);
                return;
            }

            foreach (var slot in _manager.slots)
            {
                if (slot == null) continue;
                string key = slot.gameObject.name;

                if (slot.isOccupied && slot.installedModule.visualPrefab != null)
                {
                    SpawnOrReplaceVisual(slot, slot.installedModule);
                }
                else
                {
                    DestroyVisual(key);
                }
            }
        }

        // ============================================================
        // Spawn / Destroy
        // ============================================================

        private void SpawnOrReplaceVisual(ModuleSlot slot, ShipModule module)
        {
            string key = slot.gameObject.name;

            // Уничтожить старый визуал если есть
            if (_spawned.TryGetValue(key, out var existing) && existing != null)
                Destroy(existing);

            // Найти родительский Transform: слот или дочерний socket
            Transform parent = ResolveSocket(slot, module);

            // Спавн
            var go = Instantiate(module.visualPrefab, parent);
            go.transform.localPosition = module.attachPositionOffset;
            go.transform.localEulerAngles = module.attachRotationOffset;
            go.transform.localScale = module.attachScale;

            // Применить ориентацию по attachAxis
            if (module.attachAxis != ModuleAttachAxis.Slot)
                go.transform.localRotation = GetAttachmentRotation(module);

            // Коллайдеры
            ApplyColliderMode(go, module.colliderMode);

            _spawned[key] = go;
        }

        private void DestroyVisual(string key)
        {
            if (_spawned.TryGetValue(key, out var go) && go != null)
                Destroy(go);
            _spawned.Remove(key);
        }

        private void DestroyAllVisuals()
        {
            foreach (var kv in _spawned)
                if (kv.Value != null) Destroy(kv.Value);
            _spawned.Clear();
        }

        // ============================================================
        // Helpers
        // ============================================================

        /// <summary>Найти родительский Transform с учётом visualSocketPath.</summary>
        private static Transform ResolveSocket(ModuleSlot slot, ShipModule module)
        {
            var parent = slot.transform;
            if (!string.IsNullOrEmpty(module.visualSocketPath))
            {
                var socket = parent.Find(module.visualSocketPath);
                if (socket != null)
                    parent = socket;
                // Если не найден — fallback на сам слот (анти-restrictive)
            }
            return parent;
        }

        /// <summary>Вычислить rotation с учётом attachAxis.</summary>
        private Quaternion GetAttachmentRotation(ShipModule module)
        {
            var baseRot = Quaternion.Euler(module.attachRotationOffset);
            return module.attachAxis switch
            {
                ModuleAttachAxis.ShipForward => baseRot * Quaternion.LookRotation(transform.forward),
                ModuleAttachAxis.ShipDown    => baseRot * Quaternion.LookRotation(-transform.up),
                ModuleAttachAxis.WorldUp     => baseRot * Quaternion.LookRotation(Vector3.up),
                _                            => baseRot,
            };
        }

        /// <summary>Настроить коллайдеры согласно colliderMode.</summary>
        private static void ApplyColliderMode(GameObject go, ModuleColliderMode mode)
        {
            switch (mode)
            {
                case ModuleColliderMode.None:
                    foreach (var col in go.GetComponentsInChildren<Collider>(true))
                        col.enabled = false;
                    break;

                case ModuleColliderMode.Trigger:
                    foreach (var col in go.GetComponentsInChildren<Collider>(true))
                    {
                        col.enabled = true;
                        col.isTrigger = true;
                    }
                    break;

                case ModuleColliderMode.Solid:
                    foreach (var col in go.GetComponentsInChildren<Collider>(true))
                    {
                        col.enabled = true;
                        col.isTrigger = false;
                    }
                    break;
            }
        }
    }
}
