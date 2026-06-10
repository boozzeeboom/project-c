// =====================================================================================
// CraftingEnums.cs - ResultCode, JobState, SourceType (Project C: The Clouds, T-C02)
// Note: RecipeCategory lives in RecipeData.cs; StationType lives in CraftingStationConfig.cs
// Pattern: enums used by INetworkSerializable DTOs (must be : byte)
// =====================================================================================
using Unity.Netcode;

namespace ProjectC.Crafting
{
    /// <summary>Server -> Client result code. Sent in CraftingResultDto.</summary>
    public enum CraftingResultCode : byte
    {
        Ok                  = 0,
        NotEnoughResources  = 1,
        StationBusy         = 2,
        NotOwner            = 3,
        NotFound            = 4,
        AlreadyStarted      = 5,
        NotStarted          = 6,
        AlreadyCompleted    = 7,
        InvalidArgs         = 8,
        InternalError       = 9,
        MetaReqDenied       = 10,
        RateLimited         = 11,
    }

    /// <summary>Snapshot job state. Replicated via NetworkVariable on CraftingStation.</summary>
    public enum CraftingJobState : byte
    {
        Empty      = 0,
        Buffered   = 1,
        InProgress = 2,
        Completed  = 3,
    }

    /// <summary>Where the ingredient comes from. MVP: only Inventory.</summary>
    public enum CraftingSourceType : byte
    {
        Inventory = 0,
    }
}