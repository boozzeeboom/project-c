using System.Collections.Generic;
using ProjectC.Trade;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Runtime indexes for contract records.
    ///
    /// ContractsById is the stable record registry. LocationOffers contains only
    /// pending offer IDs, ActiveByPlayer contains only active IDs, and
    /// TerminalHistory indexes completed/failed records kept for bounded retention.
    /// Persistence remains backward-compatible through ContractSaveData.
    /// </summary>
    public sealed class ContractRuntimeStore
    {
        internal readonly Dictionary<string, ContractData> ContractsById = new Dictionary<string, ContractData>();
        internal readonly Dictionary<string, List<string>> LocationOffers = new Dictionary<string, List<string>>();
        internal readonly Dictionary<ulong, List<string>> ActiveByPlayer = new Dictionary<ulong, List<string>>();
        internal readonly HashSet<string> TerminalHistory = new HashSet<string>();

        public IReadOnlyDictionary<string, ContractData> Contracts => ContractsById;
        public IReadOnlyDictionary<string, List<string>> OffersByLocation => LocationOffers;
        public IReadOnlyDictionary<ulong, List<string>> ActiveContractsByPlayer => ActiveByPlayer;
        public IReadOnlyCollection<string> TerminalContractIds => TerminalHistory;

        internal void Clear()
        {
            ContractsById.Clear();
            LocationOffers.Clear();
            ActiveByPlayer.Clear();
            TerminalHistory.Clear();
        }

        internal void RebuildTerminalHistory()
        {
            TerminalHistory.Clear();
            foreach (var pair in ContractsById)
            {
                if (pair.Value != null
                    && (pair.Value.state == ContractState.Completed || pair.Value.state == ContractState.Failed))
                {
                    TerminalHistory.Add(pair.Key);
                }
            }
        }

        internal void AddPendingOffer(string locationId, ContractData contract)
        {
            if (contract == null || string.IsNullOrEmpty(contract.contractId)) return;

            ContractsById[contract.contractId] = contract;
            TerminalHistory.Remove(contract.contractId);
            RemoveFromActive(contract.assignedPlayerId, contract.contractId);
            AddUnique(LocationOffers, locationId, contract.contractId);
        }

        internal void MarkActive(ContractData contract, ulong playerId)
        {
            if (contract == null || string.IsNullOrEmpty(contract.contractId)) return;

            ContractsById[contract.contractId] = contract;
            TerminalHistory.Remove(contract.contractId);
            RemoveFromAllOffers(contract.contractId);
            AddUnique(ActiveByPlayer, playerId, contract.contractId);
        }

        internal void MarkTerminal(ContractData contract)
        {
            if (contract == null || string.IsNullOrEmpty(contract.contractId)) return;

            ContractsById[contract.contractId] = contract;
            RemoveFromAllOffers(contract.contractId);
            RemoveFromActive(contract.assignedPlayerId, contract.contractId);
            TerminalHistory.Add(contract.contractId);
        }

        internal void MarkActiveAgain(ContractData contract, ulong playerId)
        {
            if (contract == null || string.IsNullOrEmpty(contract.contractId)) return;

            ContractsById[contract.contractId] = contract;
            TerminalHistory.Remove(contract.contractId);
            RemoveFromAllOffers(contract.contractId);
            AddUnique(ActiveByPlayer, playerId, contract.contractId);
        }

        internal void RemoveContract(string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return;

            ContractsById.Remove(contractId);
            TerminalHistory.Remove(contractId);
            RemoveFromAllOffers(contractId);
            foreach (var playerId in new List<ulong>(ActiveByPlayer.Keys))
                RemoveFromActive(playerId, contractId);
        }

        private static void AddUnique<T>(Dictionary<T, List<string>> index, T key, string contractId)
        {
            if (!index.TryGetValue(key, out var ids))
            {
                ids = new List<string>();
                index[key] = ids;
            }

            if (!ids.Contains(contractId)) ids.Add(contractId);
        }

        private void RemoveFromAllOffers(string contractId)
        {
            foreach (var locationId in new List<string>(LocationOffers.Keys))
            {
                if (!LocationOffers.TryGetValue(locationId, out var ids)) continue;
                ids.Remove(contractId);
                if (ids.Count == 0) LocationOffers.Remove(locationId);
            }
        }

        private void RemoveFromActive(ulong playerId, string contractId)
        {
            if (!ActiveByPlayer.TryGetValue(playerId, out var ids)) return;
            ids.Remove(contractId);
            if (ids.Count == 0) ActiveByPlayer.Remove(playerId);
        }
    }
}
