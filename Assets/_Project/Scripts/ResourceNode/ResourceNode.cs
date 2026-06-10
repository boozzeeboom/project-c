// =====================================================================================
// ResourceNode.cs — 3D-объект сбора ресурса (Project C: The Clouds, ResourceNode)
// =====================================================================================
// Документация:
//   • docs/Mining/00_OVERVIEW.md
//   • docs/Mining/10_DESIGN.md §1.2, §1.3
//   • docs/Mining/ROADMAP.md T-G02
//
// Назначение: NetworkBehaviour-узел сбора. Содержит state machine
// (Idle / Occupied / Depleted / Cooldown) + ссылку на MetaRequirement для tool check.
// Спавнится scene-placed в WorldScene_X_Z. Сервер авторизует сбор, выдаёт предмет
// в инвентарь, переключает состояния. Клиент реплицирует состояние через NetworkVariable
// + запускает визуальную анимацию (фаза T-G06).
//
// Поток сбора (MVP):
//   1. Игрок подходит к узлу (trigger) → InteractableManager.RegisterResourceNode
//   2. Игрок нажимает F → NetworkPlayer.TryGatherNearestNode()
//      → MetaRequirementClientState.RequestCanUse(netId)
//   3. Сервер проверяет MetaRequirement.CanPlayerUse() → allow/deny (deny → toast)
//   4. OnAccessAllowed(netId) → ResourceNode.OnMetaAccessAllowed() (этот файл)
//      → GatheringClientState.RequestStartGather(netId) → RPC
//   5. Сервер: TryStartGather → _state = Occupied → register in GatheringServer
//   6. Tick (every 0.5s) → progress → SendGatherResult → клиент показывает ProgressBar
//   7. Tick на N сек → CompleteGather → AddItemDirect + Depleted/Idle
//   8. Depleted → Cooldown (n sec) → Idle
//
// MVP-граница (T-G02):
//   - Таймер идёт независимо от позиции игрока (Q2: "пусть бегает и рубит")
//   - Один игрок = один активный сбор (race protection на сервере)
//   - Анимация (T-G06) пока stub — на этом тикете не делаем
//   - Cooldown-переход и анимация — следующие тикеты
// =====================================================================================

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Core;
using ProjectC.Items;
// NOTE: `using ProjectC.MetaRequirement;` убран намеренно — внутри namespace
// ProjectC.ResourceNode компилятор C# считает `MetaRequirement` вложенным namespace
// (даже при разных именах) и путает с одноимённым type. Используем FQN ниже.
using MetaReq = ProjectC.MetaRequirement.MetaRequirement;
using MetaReqClientState = ProjectC.MetaRequirement.MetaRequirementClientState;

namespace ProjectC.ResourceNode
{
    /// <summary>
    /// Состояние узла. Реплицируется клиентам через NetworkVariable для UI-реакции.
    /// </summary>
    public enum ResourceNodeState : byte
    {
        Idle = 0,       // готов к сбору
        Occupied = 1,   // кто-то собирает (soft-lock)
        Depleted = 2,   // _currentHarvests == 0, ждёт cooldown
        Cooldown = 3,   // невидим/недоступен, таймер
    }

    [DisallowMultipleComponent]
    public class ResourceNode : NetworkBehaviour
    {
        [Header("Config")]
        [Tooltip("ScriptableObject с параметрами сбора. ОБЯЗАТЕЛЕН.")]
        [SerializeField] private ResourceNodeConfig _config;

        [Header("MetaRequirement (tool check)")]
        [Tooltip("MetaRequirement компонент на этом же GameObject, задающий инструменты сборщика. " +
                 "Пустой _requiredItems + RequirementLogic.All = нет требований. " +
                 "Если null — узел всегда доступен по инструментам.")]
        [SerializeField] private MetaReq _metaRequirement;

        // ==========================================================
        // Replicated state
        // ==========================================================

        // Реплицируется клиентам. Сервер пишет, клиенты читают (для UI/анимации).
        private readonly NetworkVariable<ResourceNodeState> _replicatedState = new NetworkVariable<ResourceNodeState>(
            ResourceNodeState.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public ResourceNodeConfig Config => _config;
        public ResourceNodeState CurrentState => _replicatedState.Value;
        public MetaReq MetaRequirementRef => _metaRequirement;

        // ==========================================================
        // Server-only state
        // ==========================================================

        private int _currentHarvests;              // сколько осталось до истощения
        private ulong _currentGathererClientId;    // кто сейчас собирает (0 = nobody)
        private float _gatherStartServerTime;      // server-time начала сбора
        private float _cooldownEndServerTime;      // server-time окончания cooldown

        // Кэш сколько собрано всего (для ID в UI "Собрано N").
        private int _totalHarvestedThisLife;

        // ==========================================================
        // Client-side: lazy-subscribe
        // ==========================================================

        private bool _subscribedToMetaReq = false;

        // ==========================================================
        // Client-side: animation (T-G06)
        // ==========================================================

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Vector3 _baseScale;
        private static readonly int _emissionColorId = Shader.PropertyToID("_EmissionColor");
        private Coroutine _gatherAnimCoroutine;
        private bool _onValueChangedSubscribed = false;

        // ==========================================================
        // Unity lifecycle
        // ==========================================================

        private void Awake()
        {
            // Попытаемся автоматически найти MetaRequirement на этом же GameObject,
            // если поле не задано в инспекторе.
            if (_metaRequirement == null)
            {
                _metaRequirement = GetComponent<MetaReq>();
            }

            // T-G06: инициализация материала для анимации (как LockBox)
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                _mpb = new MaterialPropertyBlock();
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(_emissionColorId, _config != null ? _config.AnimIdleEmission : Color.black);
                _renderer.SetPropertyBlock(_mpb);
            }
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            // Lazy-subscribe: если MetaRequirementClientState уже создан — подпишемся.
            TrySubscribeToMetaRequirement();
        }

        private void OnDisable()
        {
            UnsubscribeFromMetaRequirement();
        }

        private void Update()
        {
            // Подстраховка: MetaRequirementClientState может быть создан ПОСЛЕ нашего OnEnable.
            TrySubscribeToMetaRequirement();
        }

        // ==========================================================
        // NetworkBehaviour lifecycle
        // ==========================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                // Резолвим item ids (InventoryWorld должен быть уже жив)
                if (_config != null)
                {
                    _config.ResolveItemIds();
                }

                // Регистрация в GatheringServer (registry для RPC dispatch).
                if (GatheringServer.Instance != null)
                {
                    GatheringServer.Instance.RegisterNode(NetworkObjectId, this);
                }
                else
                {
                    Debug.LogWarning("[ResourceNode] OnNetworkSpawn: GatheringServer.Instance==null. " +
                                     "Сбор работать не будет. Убедитесь, что [GatheringServer] GO в BootstrapScene.");
                }

                // Инициализируем счётчик на максимум (1-й сбор сразу доступен)
                _currentHarvests = 0;  // начнём с 0 — инкрементируемся после CompleteGather
            }

            // T-G06: подписка на смену состояния для анимации (все клиенты, не сервер).
            if (!_onValueChangedSubscribed)
            {
                _replicatedState.OnValueChanged += OnReplicatedStateChanged;
                // Если нод уже не Idle (spawned как Occupied/Depleted) — сразу применить
                OnReplicatedStateChanged(ResourceNodeState.Idle, _replicatedState.Value);
                _onValueChangedSubscribed = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            // T-G06: отписка от анимации
            if (_onValueChangedSubscribed)
            {
                _replicatedState.OnValueChanged -= OnReplicatedStateChanged;
                _onValueChangedSubscribed = false;
            }

            if (IsServer)
            {
                if (GatheringServer.Instance != null)
                {
                    GatheringServer.Instance.UnregisterNode(NetworkObjectId);
                }
            }

            base.OnNetworkDespawn();
        }

        // ==========================================================
        // Trigger регистрация (по аналогии с PickupItem)
        // ==========================================================

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            InteractableManager.RegisterResourceNode(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            InteractableManager.UnregisterResourceNode(this);
        }

        // ==========================================================
        // Public Server-side API (вызывается из GatheringServer)
        // ==========================================================

        /// <summary>Сервер-only: можно ли начать сбор?</summary>
        public bool CanStartGather(ulong clientId, out string reason)
        {
            reason = "";

            if (_config == null)
            {
                reason = "ResourceNode: _config == null";
                return false;
            }

            if (_replicatedState.Value != ResourceNodeState.Idle)
            {
                reason = $"{_config.NodeDisplayName} сейчас недоступен (state={_replicatedState.Value})";
                return false;
            }

            // MetaRequirement tool check (если компонент есть).
            if (_metaRequirement != null)
            {
                if (!_metaRequirement.CanPlayerUse(clientId, out reason))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Сервер-only: начать сбор. Возвращает true если успешно.</summary>
        public bool TryStartGather(ulong clientId, float serverTime)
        {
            string reason;
            if (!CanStartGather(clientId, out reason))
            {
                return false;
            }

            _currentGathererClientId = clientId;
            _gatherStartServerTime = serverTime;
            _replicatedState.Value = ResourceNodeState.Occupied;

            return true;
        }

        /// <summary>Сервер-only: тик таймера сбора. Вызывается из GatheringServer.Update каждые 0.5s.
        /// Возвращает результат: InProgress(progress) | Completed(name, qty, depleted) | Interrupted(reason).
        /// НЕ прерывает сбор при удалении игрока (Q2: "пусть бегает и рубит").</summary>
        public GatherTickResult TickGather(float serverTime)
        {
            // Проверка таймера
            if (_config == null)
            {
                return GatherTickResult.Interrupted("ResourceNode: _config == null");
            }

            float elapsed = serverTime - _gatherStartServerTime;
            if (elapsed < _config.GatherSeconds)
            {
                // InProgress
                return GatherTickResult.InProgress(elapsed / _config.GatherSeconds);
            }

            // Время вышло — завершаем сбор
            return CompleteGather();
        }

        private GatherTickResult CompleteGather()
        {
            if (_config == null)
            {
                return GatherTickResult.Interrupted("ResourceNode: _config == null");
            }

            if (InventoryWorld.Instance == null)
            {
                // Не критично: инвентарь ещё не инициализирован. Оставим Idle.
                _currentGathererClientId = 0;
                _replicatedState.Value = ResourceNodeState.Idle;
                return GatherTickResult.Interrupted("Инвентарь не инициализирован");
            }

            // Добавить предмет (AddItemDirect возвращает InventoryResultDto)
            var addResult = InventoryWorld.Instance.AddItemDirect(
                _currentGathererClientId,
                _config.ResultItemId,
                _config.ResultItemType);

            if (!addResult.IsSuccess)
            {
                // Не влезло / не найден — оставим Idle, предмет остаётся доступным.
                _currentGathererClientId = 0;
                _replicatedState.Value = ResourceNodeState.Idle;
                return GatherTickResult.Interrupted("Инвентарь полон");
            }

            _currentHarvests++;
            _totalHarvestedThisLife++;
            int currentQty = _currentHarvests;
            string itemName = _config.NodeDisplayName;
            ulong gathererId = _currentGathererClientId;

            // Узел истощён?
            if (_currentHarvests >= _config.MaxHarvests)
            {
                _replicatedState.Value = ResourceNodeState.Depleted;
                _cooldownEndServerTime = serverTimeSafe() + _config.CooldownSeconds;

                // Через gathering state.tick — этот tick завершает, дальше cooldown-режим
                return GatherTickResult.Completed(itemName, currentQty, depleted: true);
            }

            // Ещё есть что собирать → возвращаем в Idle
            _currentGathererClientId = 0;
            _replicatedState.Value = ResourceNodeState.Idle;

            return GatherTickResult.Completed(itemName, currentQty, depleted: false);
        }

        private static float serverTimeSafe()
        {
            // Единые часы с GatheringServer.Update (Time.realtimeSinceStartup) — иначе
            // cooldown (запущенный на tick) использует ServerTime.Time, а
            // _cooldownEndServerTime считается от realtimeSinceStartup → рассогласование.
            return Time.realtimeSinceStartup;
        }

        /// <summary>Сервер-only: отменить активный сбор (disconnect / внешний cancel).
        /// НЕ вызывается при движении игрока (Q2).</summary>
        public void CancelGather()
        {
            if (_replicatedState.Value != ResourceNodeState.Occupied) return;
            _currentGathererClientId = 0;
            _replicatedState.Value = ResourceNodeState.Idle;
        }

        /// <summary>Сервер-only: вызывается из GatheringServer.Update каждый кадр —
        /// проверяет cooldown и возвращает узел из Depleted → Idle когда время вышло.</summary>
        public void TickCooldown(float serverTime)
        {
            if (_replicatedState.Value != ResourceNodeState.Depleted) return;
            if (serverTime < _cooldownEndServerTime) return;

            // Cooldown закончился — узел снова готов
            _currentHarvests = 0;
            _replicatedState.Value = ResourceNodeState.Idle;
        }

        // ==========================================================
        // Client-side: обработка MetaRequirementClientState.OnAccessAllowed
        // ==========================================================

        private void TrySubscribeToMetaRequirement()
        {
            if (_subscribedToMetaReq) return;
            if (MetaReqClientState.Instance == null) return;
            MetaReqClientState.Instance.OnAccessAllowed += OnMetaAccessAllowed;
            _subscribedToMetaReq = true;
        }

        private void UnsubscribeFromMetaRequirement()
        {
            if (!_subscribedToMetaReq) return;
            if (MetaReqClientState.Instance != null)
            {
                MetaReqClientState.Instance.OnAccessAllowed -= OnMetaAccessAllowed;
            }
            _subscribedToMetaReq = false;
        }

        private void OnMetaAccessAllowed(ulong netId)
        {
            if (netId != NetworkObjectId) return;

            // MetaReq tool check прошёл → стартуем сбор на сервере.
            GatheringClientState.Instance?.RequestStartGather(netId);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Сфера = зона сбора
            if (_config != null)
            {
                Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, _config.GatherRange);
            }
        }

        private void OnValidate()
        {
            if (_config == null)
            {
                Debug.LogWarning($"[ResourceNode] '{gameObject.name}': _config не задан. " +
                                 "Сбор работать не будет.", this);
            }
        }
#endif

        // ==========================================================
        // T-G06: Client-side animation
        // ==========================================================

        private void OnReplicatedStateChanged(ResourceNodeState previous, ResourceNodeState current)
        {
            if (IsServer) return; // анимация только на клиентах

            // Остановить текущую анимацию
            if (_gatherAnimCoroutine != null)
            {
                StopCoroutine(_gatherAnimCoroutine);
                _gatherAnimCoroutine = null;
            }

            switch (current)
            {
                case ResourceNodeState.Idle:
                    // Вернуть scale к базовому, emission к покою
                    transform.localScale = _baseScale;
                    ApplyEmission(_config != null ? _config.AnimIdleEmission : Color.black);
                    gameObject.SetActive(true);
                    break;

                case ResourceNodeState.Occupied:
                    // LOOP: scale-pulse + emissive flash
                    _gatherAnimCoroutine = StartCoroutine(GatherPulse());
                    break;

                case ResourceNodeState.Depleted:
                    // Плавное исчезание (scale → 0)
                    _gatherAnimCoroutine = StartCoroutine(Disappear());
                    break;

                case ResourceNodeState.Cooldown:
                    gameObject.SetActive(false);
                    break;
            }
        }

        private System.Collections.IEnumerator GatherPulse()
        {
            if (_config == null) yield break;
            float amplitude = _config.AnimScaleAmplitude;
            float period = _config.AnimPulsePeriod;
            Color idleEm = _config.AnimIdleEmission;
            Color gatherEm = _config.AnimGatherEmission;

            while (true)
            {
                float t = Mathf.Sin(Time.time * (2f * Mathf.PI / Mathf.Max(0.01f, period)));
                // scale: 1.0 ± amplitude
                transform.localScale = _baseScale * (1.0f + amplitude * t);
                // emissive: плавно между idle и gather
                float emLerp = (t + 1f) * 0.5f; // 0..1
                ApplyEmission(Color.Lerp(idleEm, gatherEm, emLerp));
                yield return null;
            }
        }

        private System.Collections.IEnumerator Disappear()
        {
            if (_config == null) yield break;
            float duration = _config.AnimHiddenDuration;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                // scale плавно → 0, emission → idle перед исчезанием
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, k);
                if (_config != null) ApplyEmission(Color.Lerp(_config.AnimGatherEmission, _config.AnimIdleEmission, k));
                yield return null;
            }
            transform.localScale = Vector3.zero;
            gameObject.SetActive(false);
            // Когда сервер переключит Cooldown → Idle, OnReplicatedStateChanged покажет снова
        }

        private void ApplyEmission(Color color)
        {
            if (_renderer == null || _mpb == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionColorId, color);
            _renderer.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>
    /// Результат одного тика сбора. Сериализуется в RPC.
    /// </summary>
    public struct GatherTickResult
    {
        public enum ResultType : byte
        {
            InProgress = 0,
            Completed = 1,
            Interrupted = 2,
        }

        public ResultType Type;
        public float Progress;        // 0..1 для клиента (ProgressBar)
        public string Message;        // для toast'а при Interrupted
        public string ItemName;       // имя предмета при Completed
        public int Quantity;          // сколько уже собрано (при Completed)
        public bool IsDepleted;       // узел ушёл в Depleted? (при Completed)

        public static GatherTickResult InProgress(float progress)
            => new GatherTickResult { Type = ResultType.InProgress, Progress = Mathf.Clamp01(progress) };

        public static GatherTickResult Completed(string itemName, int quantity, bool depleted)
            => new GatherTickResult
            {
                Type = ResultType.Completed,
                ItemName = itemName,
                Quantity = quantity,
                IsDepleted = depleted,
            };

        public static GatherTickResult Interrupted(string message)
            => new GatherTickResult
            {
                Type = ResultType.Interrupted,
                Message = message,
            };
    }
}
