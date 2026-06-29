// Project C: Equipment Visual System — Phase 2 (2026-06-29)
// CharacterEquipmentVisualApplier: применяет ItemData.visualPrefab к персонажу M на основе
// EquipmentSnapshotDto. Спавнит/уничтожает GameObject под каждый slot, parent к кости.
//
// Аналог NpcVisualApplier (T-NPC-05, Assets/_Project/Scripts/AI/), но для equipment slots
// на скелете персонажа (parent → HumanBodyBones, не root).
//
// Триггер: EquipmentClientState.OnEquipmentUpdated (T-P10, существующее событие).
// Логика: diff snapshot ↔ _currentItems, spawn/destroy по слоту.
// Anti-restrictive: если Animator не humanoid или EquipmentClientState.Instance == null —
// warning + no-op, без падения.
//
// Design: docs/Character/EquipmentVisual/00_DESIGN.md §3.2, 02_CHARACTER_APPLIER.md.
// Additive-only: новый компонент. Не модифицирует NetworkPlayer / EquipmentServer /
// EquipmentClientState / ItemData (поля visualPrefab добавлены в Phase 1).

using System.Collections.Generic;
using ProjectC.Equipment;
using ProjectC.Equipment.Dto;
using ProjectC.Equipment.Visual;
using ProjectC.Items;
using UnityEngine;

namespace ProjectC.Player
{
    /// <summary>
    /// Phase 2: визуальный аппликатор экипировки на персонаже M (HumanM_Model).
    /// Подписывается на EquipmentClientState.OnEquipmentUpdated и поддерживает
    /// _spawnedVisuals в синхронизации со снапшотом.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterEquipmentVisualApplier : MonoBehaviour
    {
        [Tooltip("Animator с humanoid rig (должен быть на Visual_Model или root). " +
                 "Если не задан — FindFirstValidAnimator() ищет первый Animator с непустым " +
                 "runtimeAnimatorController.")]
        [SerializeField] private Animator _animator;

        [Tooltip("Показывать warning при отсутствии visualPrefab/кости/Animator. " +
                 "Default true для debug; можно отключить в release.")]
        [SerializeField] private bool _logWarnings = true;

        // slot → spawned visual GameObject (null если слот пуст или без визуала)
        private readonly Dictionary<EquipSlot, GameObject> _spawnedVisuals = new();

        // slot → ItemData (для diff'а snapshot vs current)
        private readonly Dictionary<EquipSlot, ItemData> _currentItems = new();

        private EquipmentClientState _clientState;

        // === Lifecycle ===

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = FindFirstValidAnimator();
            }
        }

        private void OnEnable()
        {
            _clientState = EquipmentClientState.Instance;
            if (_clientState == null)
            {
                if (_logWarnings)
                {
                    Debug.LogWarning($"[CharacterEquipmentVisualApplier] EquipmentClientState.Instance == null on '{name}'. Visual equip skipped (anti-restrictive).", this);
                }
                return;
            }

            _clientState.OnEquipmentUpdated += OnEquipmentUpdated;

            // Race: если snapshot уже пришёл до OnEnable — применить немедленно.
            if (_clientState.CurrentSnapshot.HasValue)
            {
                OnEquipmentUpdated(_clientState.CurrentSnapshot.Value);
            }
        }

        private void OnDisable()
        {
            if (_clientState != null)
            {
                _clientState.OnEquipmentUpdated -= OnEquipmentUpdated;
                _clientState = null;
            }
            DestroyAllVisuals();
            _currentItems.Clear();
        }

        // === Snapshot handler ===

        private void OnEquipmentUpdated(EquipmentSnapshotDto snapshot)
        {
            if (_animator == null || !_animator.isHuman)
            {
                if (_logWarnings)
                {
                    Debug.LogWarning($"[CharacterEquipmentVisualApplier] Animator not humanoid on '{name}'. Visual equip skipped.", this);
                }
                return;
            }

            // Идём по всем EquipSlot (None пропускаем).
            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                if (slot == EquipSlot.None) continue;
                ProcessSlot(slot, snapshot);
            }
        }

        private void ProcessSlot(EquipSlot slot, EquipmentSnapshotDto snapshot)
        {
            // Получить ItemData для слота из снапшота.
            ItemData newItem = null;
            if (snapshot.equip.TryGetItemId(slot, out int itemId) && itemId > 0)
            {
                var inv = InventoryWorld.Instance;
                if (inv != null)
                {
                    newItem = inv.GetItemDefinition(itemId);
                }
            }

            _currentItems.TryGetValue(slot, out ItemData oldItem);

            // Diff: нет изменений → continue (избегаем flicker и лишних операций).
            if (ReferenceEquals(newItem, oldItem)) return;

            // 1. Убрать старый визуал, если был.
            if (oldItem != null)
            {
                DestroyVisual(slot);
            }

            // 2. Навесить новый, если есть И у него есть visualPrefab.
            if (newItem != null && newItem.visualPrefab != null)
            {
                SpawnVisual(slot, newItem);
            }

            // 3. Обновить tracking.
            if (newItem != null)
            {
                _currentItems[slot] = newItem;
            }
            else
            {
                _currentItems.Remove(slot);
            }
        }

        // === Spawn / Destroy ===

        private void SpawnVisual(EquipSlot slot, ItemData item)
        {
            // 1. Resolve bone (override или default).
            if (!EquipSlotToBone.TryGetBoneTransformWithOverride(
                    slot, item.attachBoneOverride, _animator, out Transform bone))
            {
                if (_logWarnings)
                {
                    Debug.LogWarning($"[CharacterEquipmentVisualApplier] Bone not found for slot {slot} (override={item.attachBoneOverride}) on '{name}'. Visual skipped for '{item.itemName}'.", this);
                }
                return;
            }

            // 2. Instantiate visualPrefab.
            GameObject go = Instantiate(item.visualPrefab);
            go.name = $"Visual_{slot}_{SanitizeName(item.itemName)}";

            // 3. Parent + transform.
            go.transform.SetParent(bone, worldPositionStays: false);
            go.transform.localPosition = item.attachPositionOffset;
            go.transform.localEulerAngles = item.attachRotationOffset;
            go.transform.localScale = item.attachScale;

            // 4. Disable colliders (visual only — нет физики на экипировке).
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.enabled = false;
            }

            // 5. Track.
            _spawnedVisuals[slot] = go;

            if (Debug.isDebugBuild && _logWarnings)
            {
                Debug.Log($"[CharacterEquipmentVisualApplier] Spawned '{go.name}' on bone '{bone.name}' (slot={slot}).", this);
            }
        }

        private void DestroyVisual(EquipSlot slot)
        {
            if (!_spawnedVisuals.TryGetValue(slot, out GameObject go)) return;
            if (go == null)
            {
                _spawnedVisuals.Remove(slot);
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
            _spawnedVisuals.Remove(slot);
        }

        private void DestroyAllVisuals()
        {
            foreach (var kvp in _spawnedVisuals)
            {
                if (kvp.Value != null)
                {
                    if (Application.isPlaying) Destroy(kvp.Value);
                    else DestroyImmediate(kvp.Value);
                }
            }
            _spawnedVisuals.Clear();
        }

        // === Helpers ===

        /// <summary>
        /// Найти первый валидный Animator (с непустым runtimeAnimatorController).
        /// Копия логики NetworkPlayer.FindFirstValidAnimator — ищет на root и в детях.
        /// </summary>
        private Animator FindFirstValidAnimator()
        {
            var animators = GetComponentsInChildren<Animator>(true);
            foreach (var a in animators)
            {
                if (a != null && a.runtimeAnimatorController != null)
                {
                    return a;
                }
            }
            return null;
        }

        /// <summary>
        /// Убрать невалидные символы из имени для имени spawned GO (Unity naming rules).
        /// </summary>
        private static string SanitizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Item";
            // Убираем пробелы (могут ломать path queries) и спецсимволы.
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == ' ' || chars[i] == '/' || chars[i] == '\\' || chars[i] == '.')
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }

        // === Public API (для тестов и отладки) ===

        /// <summary>Сколько visual'ов сейчас на персонаже (для UI/debug).</summary>
        public int ActiveVisualCount => _spawnedVisuals.Count;

        /// <summary>VisualPrefab для слота (или null).</summary>
        public GameObject GetVisualForSlot(EquipSlot slot)
        {
            _spawnedVisuals.TryGetValue(slot, out var go);
            return go;
        }

        /// <summary>
        /// Принудительно переприменить текущий snapshot (для отладки/Editor).
        /// </summary>
        [ContextMenu("DEBUG: Force re-apply current snapshot")]
        public void DebugReapply()
        {
            if (_clientState != null && _clientState.CurrentSnapshot.HasValue)
            {
                OnEquipmentUpdated(_clientState.CurrentSnapshot.Value);
            }
            else if (_logWarnings)
            {
                Debug.LogWarning($"[CharacterEquipmentVisualApplier] No client state / snapshot — nothing to re-apply.", this);
            }
        }
    }
}