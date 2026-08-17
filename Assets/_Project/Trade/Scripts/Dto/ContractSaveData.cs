using System;
using System.Collections.Generic;

namespace ProjectC.Trade.Dto
{
    /// <summary>
    /// Serializable DTO for persisting ALL contract state through IPlayerDataRepository.
    /// Used by ContractWorld.SaveAll / ContractWorld.LoadAll.
    /// </summary>
    [Serializable]
    public class ContractSaveData
    {
        /// <summary>All contracts (Pending, Active, Completed, Failed).</summary>
        public List<ContractData> contracts = new List<ContractData>();

        /// <summary>Player debts (clientId → debt state).</summary>
        public List<ContractDebtEntry> debts = new List<ContractDebtEntry>();

        /// <summary>Player → active contract IDs mapping.</summary>
        public List<PlayerContractEntry> playerContracts = new List<PlayerContractEntry>();

        /// <summary>Location → available contract IDs mapping.</summary>
        public List<LocationContractEntry> locationContracts = new List<LocationContractEntry>();

        /// <summary>
        /// True when the snapshot contains any persisted contract subsystem state.
        /// Debt-only snapshots are valid and must not be treated as a missing save.
        /// </summary>
        public bool HasData =>
            (contracts != null && contracts.Count > 0)
            || (debts != null && debts.Count > 0)
            || (playerContracts != null && playerContracts.Count > 0)
            || (locationContracts != null && locationContracts.Count > 0);
    }

    /// <summary>
    /// Serializable snapshot of ContractDebt for a single player.
    /// </summary>
    [Serializable]
    public class ContractDebtEntry
    {
        public ulong playerId;
        public float currentDebt;
        public float lastDecayTime;
    }

    /// <summary>
    /// Serializable player → contractIds mapping entry.
    /// </summary>
    [Serializable]
    public class PlayerContractEntry
    {
        public ulong playerId;
        public List<string> contractIds = new List<string>();
    }

    /// <summary>
    /// Serializable location → contractIds mapping entry.
    /// </summary>
    [Serializable]
    public class LocationContractEntry
    {
        public string locationId;
        public List<string> contractIds = new List<string>();
    }
}
