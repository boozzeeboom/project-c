// CraftingStation.cs (T-C04) - scene-placed NetworkBehaviour. Server: state machine +
// CompleteCraft. Client: trigger register + IInteractable. Pattern: ResourceNode T-G02.
using Unity.Netcode;
using UnityEngine;
using ProjectC.Core;
using MetaReq = ProjectC.MetaRequirement.MetaRequirement;

namespace ProjectC.Crafting
{
    [DisallowMultipleComponent]
    public class CraftingStation : NetworkBehaviour, IInteractable
    {
        [Header("Config")]
        [SerializeField] private CraftingStationConfig _config;

        [Header("MetaRequirement (tool check, optional)")]
        [SerializeField] private MetaReq _metaRequirement;

        // ==========================================================
        // Replicated state
        // ==========================================================
        private readonly NetworkVariable<CraftingJobState> _replicatedState = new NetworkVariable<CraftingJobState>(
            CraftingJobState.Empty,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> _jobOwnerClientId = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _activeRecipeId = new NetworkVariable<int>(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public CraftingStationConfig Config => _config;
        public CraftingJobState CurrentState => _replicatedState.Value;
        public ulong CurrentOwner => _jobOwnerClientId.Value;
        public int ActiveRecipeId => _activeRecipeId.Value;

        // IInteractable
        public string InstanceId => NetworkObjectId.ToString();
        public string DisplayName => _config != null ? _config.DisplayName : "Станция";
        public float InteractionRadius => _config != null ? _config.InteractRadius : 4f;
        public Vector3 Position => transform.position;

        // ==========================================================
        // Unity lifecycle
        // ==========================================================
        private void Awake()
        {
            if (_metaRequirement == null) _metaRequirement = GetComponent<MetaReq>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                if (CraftingServer.Instance == null)
                {
                    Debug.LogWarning($"[CraftingStation {NetworkObjectId}] OnNetworkSpawn: CraftingServer.Instance==null. Регистрация не произойдёт.", this);
                }
                else
                {
                    // Регистрируем рецепты этой станции (idempotent — повторный RegisterRecipe вернёт тот же id)
                    if (_config != null)
                    {
                        foreach (var r in _config.AllowedRecipes)
                        {
                            if (r != null) CraftingWorld.RegisterRecipe(r);
                        }
                    }
                    // Регистрируем станцию в CraftingWorld (этот GameObject — MonoBehaviour, подходит под late-bound reflection)
                    CraftingWorld.RegisterStation(NetworkObjectId, this);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer && CraftingServer.Instance != null)
            {
                CraftingWorld.UnregisterStation(NetworkObjectId);
            }
        }

        // ==========================================================
        // Trigger регистрация
        // ==========================================================
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            InteractableManager.RegisterCraftingStation(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            InteractableManager.UnregisterCraftingStation(this);
        }

        // ==========================================================
        // Server-side API (called by CraftingServer)
        // ==========================================================

        /// <summary>Можно ли начать крафт? (Server-only, вызывается из CraftingServer.StartCraftRpc)</summary>
        public bool CanStartCraft(ulong clientId, out string reason)
        {
            reason = "";
            if (_config == null) { reason = "CraftingStation: _config == null"; return false; }
            if (_replicatedState.Value == CraftingJobState.InProgress) { reason = "Крафт уже идёт"; return false; }
            if (_replicatedState.Value == CraftingJobState.Completed) { reason = "Крафт завершён, заберите результат"; return false; }

            // MetaReq tool check
            if (_metaRequirement != null && !_metaRequirement.CanPlayerUse(clientId, out reason))
            {
                return false;
            }
            return true;
        }

        /// <summary>Запуск крафта. Server-only. CraftingServer вызывает после проверок.</summary>
        public void ServerStartCraft(ulong clientId, int recipeId, float startTime, float duration, System.Collections.Generic.List<CommittedIngredientDto> committed, string resultItemName)
        {
            _jobOwnerClientId.Value = clientId;
            _activeRecipeId.Value = recipeId;
            _replicatedState.Value = CraftingJobState.InProgress;
            // обновим CraftingWorld.GetJob(this.netId)
            var job = CraftingWorld.GetJob(NetworkObjectId);
            if (job != null)
            {
                job.OwnerClientId = clientId;
                job.RecipeId = recipeId;
                job.State = CraftingJobState.InProgress;
                job.StartTime = startTime;
                job.Duration = duration;
                job.ResultItemName = resultItemName;
                job.Committed.Clear();
                job.Committed.AddRange(committed);
            }
        }

        /// <summary>Отмена. Server-only.</summary>
        public void ServerCancelCraft()
        {
            _replicatedState.Value = CraftingJobState.Buffered;
            _jobOwnerClientId.Value = 0;
            _activeRecipeId.Value = -1;
            var job = CraftingWorld.GetJob(NetworkObjectId);
            if (job != null)
            {
                job.Buffer.Clear();
                for (int i = 0; i < job.Committed.Count; i++)
                {
                    job.Buffer.Add(new BufferedIngredientDto { itemId = job.Committed[i].itemId, quantity = job.Committed[i].quantity, source = (byte)CraftingSourceType.Inventory, ownerClientId = job.Committed[i].ownerClientId });
                }
                job.Committed.Clear();
                job.State = CraftingJobState.Buffered;
                job.RecipeId = -1;
                job.StartTime = 0f;
                job.Duration = 0f;
                job.ResultItemName = null;
            }
        }

        /// <summary>Сбор готового результата. Server-only. Сбрасывает в Empty.</summary>
        public void ServerCollect()
        {
            _replicatedState.Value = CraftingJobState.Empty;
            _jobOwnerClientId.Value = 0;
            _activeRecipeId.Value = -1;
            var job = CraftingWorld.GetJob(NetworkObjectId);
            if (job != null) job.Reset();
        }

        /// <summary>
        /// Завершение крафта (CraftingWorld.OnTick). Server-only. Переход InProgress → Completed.
        /// Имя метода зафиксировано: CraftingWorld.OnTick вызывает его через reflection (T-C02 forward-binding).
        /// </summary>
        public void CompleteCraft()
        {
            if (_replicatedState.Value != CraftingJobState.InProgress) return;
            _replicatedState.Value = CraftingJobState.Completed;
            // CraftingWorld.GetJob остаётся в InProgress до ServerCollect (так клиент видит что готово)
        }

        // ==========================================================
        // Client-side: Open crafting window on F (handled in NetworkPlayer)
        // ==========================================================
        public void OnInteracted()
        {
            // T-C06: CraftingWindow.Show(NetworkObjectId, _config)
            // T-C05: CraftingClientState.RequestSubscribe(NetworkObjectId)
            // Пока — заглушка
            Debug.Log($"[CraftingStation {NetworkObjectId}] OnInteracted: name='{DisplayName}' state={_replicatedState.Value}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            float r = _config != null ? _config.InteractRadius : 4f;
            Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, r);
        }

        private void OnValidate()
        {
            if (_config == null) Debug.LogWarning($"[CraftingStation] '{gameObject.name}': _config не задан.", this);
        }
#endif
    }
}