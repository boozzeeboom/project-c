// CraftingJob.cs (T-C02) - server-only state machine. Not INetworkSerializable.
// Lives in CraftingWorld; replicated state to clients via CraftingStation NetworkVariables
using System.Collections.Generic;
namespace ProjectC.Crafting
{
    /// <summary>Server-side authoritative job for one station. Mutated by CraftingWorld.</summary>
    public class CraftingJob
    {
        public ulong StationNetId;
        public ulong OwnerClientId;
        public int RecipeId;               // -1 = none
        public CraftingJobState State;
        public float StartTime;            // NetworkManager.ServerTime.Time when started
        public float Duration;             // recipe.CraftSeconds / station.SpeedMultiplier

        /// <summary>Ingredients placed by the player BEFORE start. Read-write until InProgress.</summary>
        public List<BufferedIngredientDto> Buffer = new List<BufferedIngredientDto>();

        /// <summary>Ingredients committed at start. FROZEN during InProgress.</summary>
        public List<CommittedIngredientDto> Committed = new List<CommittedIngredientDto>();

        /// <summary>Name of output item (resolved on CompleteCraft) for client toast.</summary>
        public string ResultItemName;

        public void Reset()
        {
            OwnerClientId = 0;
            RecipeId = -1;
            State = CraftingJobState.Empty;
            StartTime = 0f;
            Duration = 0f;
            Buffer.Clear();
            Committed.Clear();
            ResultItemName = null;
        }

        public float Progress01(float nowServerTime)
        {
            if (State != CraftingJobState.InProgress || Duration <= 0f) return 0f;
            float p = (nowServerTime - StartTime) / Duration;
            if (p < 0f) return 0f;
            if (p > 1f) return 1f;
            return p;
        }
    }
}