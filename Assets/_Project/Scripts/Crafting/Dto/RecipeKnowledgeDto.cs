// Project C: Crafting — T-KNOWLEDGE-V2
// RecipeKnowledgeDto: server -> client sync payload for known recipe IDs.
// INetworkSerializable struct. Design: docs/Character/Knowledges/05_KNOWLEDGE_SYSTEM_V2_RESEARCH_REVIEW.md §4.5

using System;
using Unity.Netcode;

namespace ProjectC.Crafting.Dto
{
    [Serializable]
    public struct RecipeKnowledgeDto : INetworkSerializable, IEquatable<RecipeKnowledgeDto>
    {
        public int[] knownRecipeIds;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int len = knownRecipeIds?.Length ?? 0;
            serializer.SerializeValue(ref len);
            if (serializer.IsReader)
            {
                knownRecipeIds = new int[len];
            }
            for (int i = 0; i < len; i++)
            {
                int val = knownRecipeIds[i];
                serializer.SerializeValue(ref val);
                if (serializer.IsReader)
                    knownRecipeIds[i] = val;
            }
        }

        public bool Equals(RecipeKnowledgeDto other)
        {
            if ((knownRecipeIds == null) != (other.knownRecipeIds == null)) return false;
            if (knownRecipeIds == null) return true;
            if (knownRecipeIds.Length != other.knownRecipeIds.Length) return false;
            for (int i = 0; i < knownRecipeIds.Length; i++)
            {
                if (knownRecipeIds[i] != other.knownRecipeIds[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is RecipeKnowledgeDto o && Equals(o);
        public override int GetHashCode() => knownRecipeIds?.Length ?? 0;
    }
}
