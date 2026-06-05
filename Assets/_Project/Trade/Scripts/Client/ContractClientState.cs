using System;
using ProjectC.Trade.Dto;
using UnityEngine;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// Клиентская проекция серверного состояния контрактной подсистемы.
    /// Один инстанс на клиентский процесс (НЕ NetworkBehaviour).
    /// Получает snapshot'ы и результаты от <see cref="ProjectC.Trade.Network.ContractServer"/>,
    /// держит последний известный снепшот, дёргает события для UI.
    ///
    /// UI читает ИСКЛЮЧИТЕЛЬНО из этого класса (аналогично <see cref="MarketClientState"/>).
    /// Сервер — single source of truth, этот класс — projection layer.
    ///
    /// Создание: auto-spawn в <c>NetworkManagerController.Awake</c> (FIX C2 паттерн, см. docs/dev/CONTRACT_V2_MIGRATION.md §3.6).
    ///
    /// C2-этап миграции контрактов на v2-архитектуру.
    /// </summary>
    public class ContractClientState : MonoBehaviour
    {
        public static ContractClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [Tooltip("Не уничтожать при загрузке сцены (клиент переживает стриминг)")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        public ContractSnapshotDto? CurrentSnapshot { get; private set; }
        public string CurrentLocationId => CurrentSnapshot.HasValue ? CurrentSnapshot.Value.locationId : null;

        // Последний результат (для UI feedback)
        public ContractResultDto? LastResult { get; private set; }

        // Подписки
        public event Action<ContractSnapshotDto> OnSnapshotUpdated;
        public event Action<ContractResultDto> OnContractResult;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
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

        public void OnSnapshotReceived(ContractSnapshotDto snapshot)
        {
            CurrentSnapshot = snapshot;
            OnSnapshotUpdated?.Invoke(snapshot);
        }

        public void OnTradeResultReceived(ContractResultDto result)
        {
            LastResult = result;
            OnContractResult?.Invoke(result);
        }

        // ========================================================
        // CONVENIENCE API для UI и NetworkPlayer
        // ========================================================

        /// <summary>Попросить сервер прислать актуальный snapshot для locationId (вызывается из ContractInteractor при E).</summary>
        public void RequestList(string locationId)
        {
            if (string.IsNullOrEmpty(locationId))
            {
                Debug.LogWarning("[ContractClientState] RequestList: locationId is empty");
                return;
            }
            if (ProjectC.Trade.Network.ContractServer.Instance == null)
            {
                Debug.LogWarning("[ContractClientState] RequestList: ContractServer.Instance is NULL (network not started?)");
                return;
            }
            ProjectC.Trade.Network.ContractServer.Instance.RequestListRpc(locationId);
        }

        public void RequestAccept(string contractId)
        {
            if (ProjectC.Trade.Network.ContractServer.Instance == null) return;
            ProjectC.Trade.Network.ContractServer.Instance.RequestAcceptRpc(contractId);
        }

        public void RequestComplete(string contractId)
        {
            if (ProjectC.Trade.Network.ContractServer.Instance == null) return;
            ProjectC.Trade.Network.ContractServer.Instance.RequestCompleteRpc(contractId);
        }

        public void RequestFail(string contractId)
        {
            if (ProjectC.Trade.Network.ContractServer.Instance == null) return;
            ProjectC.Trade.Network.ContractServer.Instance.RequestFailRpc(contractId);
        }

        // ========================================================
        // LOCALIZATION
        // ========================================================

        /// <summary>Маппинг ContractResultCode → локализованная строка для UI.</summary>
        public static string LocalizeResultCode(ContractResultCode code)
        {
            switch (code)
            {
                case ContractResultCode.Ok: return "OK";
                case ContractResultCode.NotInZone: return "Вы должны быть в зоне NPC-агента";
                case ContractResultCode.ContractNotFound: return "Контракт не найден";
                case ContractResultCode.ContractNotPending: return "Контракт уже принят или истёк";
                case ContractResultCode.ContractNotActive: return "Контракт не активен";
                case ContractResultCode.ContractNotAssigned: return "Это не ваш контракт";
                case ContractResultCode.MaxActiveReached: return "Слишком много активных контрактов";
                case ContractResultCode.TooMuchDebt: return "Слишком большой долг";
                case ContractResultCode.TimerExpired: return "Время контракта истекло";
                case ContractResultCode.WrongDestination: return "Вы не в целевой локации";
                case ContractResultCode.CargoMissing: return "Нет нужного груза";
                case ContractResultCode.WarehouseFull: return "Нет места на складе";
                case ContractResultCode.ItemNotFound: return "Товар не найден";
                case ContractResultCode.RateLimited: return "Слишком много запросов";
                case ContractResultCode.InternalError: return "Внутренняя ошибка";
                default: return code.ToString();
            }
        }
    }
}
