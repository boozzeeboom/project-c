// =====================================================================================
// GatheringClientState.cs — клиентская проекция сбора (T-G04, полная версия)
// =====================================================================================
// Документация:
//   • docs/Mining/10_DESIGN.md §1.4
//   • docs/Mining/ROADMAP.md T-G04
//
// T-G03 создал stub. T-G04 добавляет:
//   - Events для UI: OnGatherProgress, OnGatherCompleted, OnGatherInterrupted,
//                    OnGatherDenied, OnGatherCancelled
//   - Queue + таймаут (если сервер не тикает 2.5 сек → Interrupted)
//   - Управление состоянием: текущий nodeNetId, lastResult
//   - В T-G05 NetworkPlayer будет вызывать RequestStartGather перед MetaReq check
//
// Сценарий: F → NetworkPlayer.TryGatherNearestNode → MetaRequirementClientState.RequestCanUse
// → OnAccessAllowed → ResourceNode.OnMetaAccessAllowed → GatheringClientState.RequestStartGather
// → GatheringServer.RequestStartGatherRpc → ResourceNode.TryStartGather → SendGatherResult(InProgress(0))
// → NetworkPlayer.ReceiveGatherResultTargetRpc → GatheringClientState.OnGatherResultReceived
// → OnGatherProgress(0) → GatheringToastController показывает ProgressBar
// =====================================================================================

using System;
using System.Collections;
using UnityEngine;

namespace ProjectC.ResourceNode
{
    public class GatheringClientState : MonoBehaviour
    {
        public static GatheringClientState Instance { get; private set; }

        [SerializeField] private bool _dontDestroyOnLoad = true;

        [Header("Timeout")]
        [Tooltip("Если сервер не прислал InProgress/Completed в течение этого времени (сек) — " +
                 "считаем сбор прерванным. Защита от зависшего сервера.")]
        [SerializeField] private float _serverTimeoutSec = 2.5f;

        // ==========================================================
        // Events (UI / логика подписывается)
        // ==========================================================

        /// <summary>Сервер прислал тик с прогрессом 0..1. UI обновляет ProgressBar.</summary>
        public event Action<float> OnGatherProgress;

        /// <summary>Сбор завершён. itemName — что собрано, quantity — сколько, isDepleted — узел ушёл в Depleted.</summary>
        public event Action<string, int, bool> OnGatherCompleted;

        /// <summary>Сбор прерван (reason — human-readable).</summary>
        public event Action<string> OnGatherInterrupted;

        /// <summary>Сервер отказал в начале сбора (reason — human-readable).</summary>
        public event Action<string> OnGatherDenied;

        /// <summary>Сбор отменён игроком (F повторно или Cancel RPC).</summary>
        public event Action OnGatherCancelled;

        // ==========================================================
        // State
        // ==========================================================

        /// <summary>Текущий netId ноды, на которой идёт сбор (0 = idle).</summary>
        public ulong CurrentNodeNetId { get; private set; }

        public bool IsGathering => CurrentNodeNetId != 0;
        public float LastProgress { get; private set; }

        // ==========================================================
        // Lifecycle
        // ==========================================================

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (_dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ==========================================================
        // Outgoing: client → server
        // ==========================================================

        /// <summary>Клиент → сервер: запросить старт сбора на указанном ноде.</summary>
        public void RequestStartGather(ulong nodeNetId)
        {
            if (GatheringServer.Instance == null)
            {
                Debug.LogWarning("[GatheringClientState] RequestStartGather: GatheringServer.Instance==null. " +
                                 "(сервер ещё не стартовал?)");
                return;
            }
            CurrentNodeNetId = nodeNetId;
            LastProgress = 0f;
            _serverTimeoutCoroutine = StartCoroutine(ServerTimeoutWatcher());
            GatheringServer.Instance.RequestStartGatherRpc(nodeNetId);
        }

        /// <summary>Клиент → сервер: отменить активный сбор.</summary>
        public void RequestCancelGather()
        {
            if (GatheringServer.Instance == null) return;
            if (CurrentNodeNetId == 0) return;
            GatheringServer.Instance.RequestCancelGatherRpc();
            // OnGatherCancelled придёт через RPC (Cancelled result)
        }

        // ==========================================================
        // Incoming: server → client (RPC result)
        // ==========================================================

        /// <summary>Вызывается из NetworkPlayer.ReceiveGatherResultTargetRpc.</summary>
        public void OnGatherResultReceived(GatherResult result)
        {
            switch (result.Result)
            {
                case GatherResultCode.InProgress:
                    LastProgress = result.progress;
                    try { OnGatherProgress?.Invoke(result.progress); }
                    catch (Exception ex) { Debug.LogError("[GatheringClientState] OnGatherProgress handler threw: " + ex); }
                    break;

                case GatherResultCode.Completed:
                    StopTimeoutWatcher();
                    CurrentNodeNetId = 0;
                    LastProgress = 0f;
                    try { OnGatherCompleted?.Invoke(result.itemName, result.quantity, result.isDepleted); }
                    catch (Exception ex) { Debug.LogError("[GatheringClientState] OnGatherCompleted handler threw: " + ex); }
                    break;

                case GatherResultCode.Interrupted:
                    StopTimeoutWatcher();
                    CurrentNodeNetId = 0;
                    LastProgress = 0f;
                    try { OnGatherInterrupted?.Invoke(result.reason); }
                    catch (Exception ex) { Debug.LogError("[GatheringClientState] OnGatherInterrupted handler threw: " + ex); }
                    break;

                case GatherResultCode.Denied:
                    StopTimeoutWatcher();
                    CurrentNodeNetId = 0;
                    LastProgress = 0f;
                    try { OnGatherDenied?.Invoke(result.reason); }
                    catch (Exception ex) { Debug.LogError("[GatheringClientState] OnGatherDenied handler threw: " + ex); }
                    break;

                case GatherResultCode.Cancelled:
                    StopTimeoutWatcher();
                    CurrentNodeNetId = 0;
                    LastProgress = 0f;
                    try { OnGatherCancelled?.Invoke(); }
                    catch (Exception ex) { Debug.LogError("[GatheringClientState] OnGatherCancelled handler threw: " + ex); }
                    break;
            }
        }

        // ==========================================================
        // Server timeout watcher
        // ==========================================================

        private Coroutine _serverTimeoutCoroutine;

        private IEnumerator ServerTimeoutWatcher()
        {
            float elapsed = 0f;
            while (elapsed < _serverTimeoutSec && CurrentNodeNetId != 0)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (CurrentNodeNetId != 0)
            {
                // Сервер не отвечает — считаем сбор прерванным
                Debug.LogWarning("[GatheringClientState] Server timeout (" + _serverTimeoutSec + "s) — сбор прерван");
                CurrentNodeNetId = 0;
                LastProgress = 0f;
                try { OnGatherInterrupted?.Invoke("Сервер не отвечает"); }
                catch (Exception ex) { Debug.LogError("[GatheringClientState] OnGatherInterrupted (timeout) handler threw: " + ex); }
            }
            _serverTimeoutCoroutine = null;
        }

        private void StopTimeoutWatcher()
        {
            if (_serverTimeoutCoroutine != null)
            {
                StopCoroutine(_serverTimeoutCoroutine);
                _serverTimeoutCoroutine = null;
            }
        }
    }
}
